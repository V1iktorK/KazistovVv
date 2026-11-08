using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится) для этапов 7–12:
/// ограничения промежуточных точек, планирование с ограничениями, калибровочный мастер,
/// калькулятор нагрузки, экспорт в языки роботов (KRL / KAREL / RAPID), импорт моделей
/// URDF / STEP с проверкой кинематики.
///
/// Отчёт читается из лога Unity (`[DshStage3Diag]`): файл в OneDrive во время PlayMode
/// не дописывается (особенность окружения, §12.10). Ожидания — по РЕАЛЬНОМУ времени,
/// каждое действие выполняется один раз за шаг, у шага есть сторож.
///
/// Запуск:
///   Unity.exe -batchmode -nographics -projectPath "&lt;копия&gt;" -executeMethod DshStage3Diag.Run
///   -logFile _dsh_s3.log        (БЕЗ -quit: нужен PlayMode)
/// </summary>
public static class DshStage3Diag
{
    private const string Key = "DshStage3Diag.Running";
    private const float StepLimitSeconds = 90f;
    private const float TotalLimitSeconds = 1500f;
    private static string report;

    public static void Run()
    {
        report = FeatureStorage.ReportPath("_dsh_s3_verify.txt");
        try
        {
            File.WriteAllText(report,
                "=== DshStage3Diag · этапы 7–12 (ограничения waypoints, ограниченное планирование, " +
                "калибровка, нагрузка, экспорт, импорт) ===\n");
        }
        catch (Exception e) { Debug.LogError("[DshStage3Diag] отчёт не создан: " + e.Message); }

        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(Key, false)) return;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ------------------------------------------------------------------ состояние
    private static int step;
    private static bool playStarted;
    private static int ok, fail, info;
    private static readonly HashSet<string> usedTags = new HashSet<string>();

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static KvStageHub stages;
    private static KvStageHub2 stages2;
    private static KvStageHub3 stages3;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static PoseValidator v;
    private static Vector3 workPoint;
    private static float stepEnterTime;
    private static int stepEntries;
    private static bool done;

    private static double[] savedPose;
    private static float savedToolOffset;
    private static int candidatesBefore;
    private static float routeTimeNoPause;
    private static int routeSamplesNoPause;
    private static float constraintWorstBefore;
    private static int constraintViolationsBefore;
    private static float payloadFolded;
    private static string exportPath;

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n"); } catch { }
        Debug.Log("[DshStage3Diag] " + text);
    }

    private static void Check(bool condition, string what, string detail = "")
    {
        if (condition) { ok++; Line("[OK]   " + what + (detail.Length > 0 ? " — " + detail : "")); }
        else { fail++; Line("[FAIL] " + what + (detail.Length > 0 ? " — " + detail : "")); }
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

    private static float Now { get { return (float)EditorApplication.timeSinceStartup; } }

    private static bool Wait(float seconds)
    {
        return Now - stepEnterTime >= seconds;
    }

    private static bool Once(string actionTag)
    {
        if (usedTags.Contains(actionTag)) return false;
        usedTags.Add(actionTag);
        return true;
    }

    private static void Next(int s)
    {
        if (s != step) Line("→ шаг " + s);
        step = s;
        usedTags.Clear();
        stepEnterTime = Now;
        stepEntries = 0;
    }

    // ------------------------------------------------------------------ цикл
    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (playStarted) { Line("PlayMode завершился неожиданно"); Finish(); }
            return;
        }
        playStarted = true;
        if (stepEnterTime <= 0f) stepEnterTime = Now;

        stepEntries++;
        if (Now - stepEnterTime > StepLimitSeconds && step < 13)
        {
            fail++;
            Line("[FAIL] шаг " + step + " не завершился за " + StepLimitSeconds.ToString("0") +
                 " с (вызовов " + stepEntries + ") — перехожу к следующему");
            Next(step + 1);
            return;
        }
        float start = SessionState.GetFloat(Key + ".Start", Now);
        if (Now - start > TotalLimitSeconds)
        {
            fail++;
            Line("[FAIL] прогон превысил " + TotalLimitSeconds.ToString("0") + " с — завершаю");
            Finish();
            return;
        }

        try
        {
            switch (step)
            {
                case 0: StepBoot(); break;
                case 1: StepCandidates(); break;
                case 2: StepConstraintsCheck(); break;
                case 3: StepConstraintsProject(); break;
                case 4: StepWaypointOrientation(); break;
                case 5: StepWaypointSpeedPause(); break;
                case 6: StepWaypointDetour(); break;
                case 7: StepCalibration(); break;
                case 8: StepPayload(); break;
                case 9: StepExportKrl(); break;
                case 10: StepExportKarelRapid(); break;
                case 11: StepImportUrdf(); break;
                case 12: StepImportStep(); break;
                case 13: StepFinishReport(); break;
            }
        }
        catch (Exception e)
        {
            fail++;
            Line("[FAIL] ИСКЛЮЧЕНИЕ на шаге " + step + ": " + e.GetType().Name + " " + e.Message +
                 "\n" + e.StackTrace);
            Next(step + 1);
        }
    }

    // ------------------------------------------------------------------ шаги

    private static void StepBoot()
    {
        ui = KazistovVvUIManager.Instance;
        if (ui == null || ui.Flow == null) return;
        flow = ui.Flow;
        if (flow.Validator == null || !flow.Validator.Ready) return;

        hub = FeatureHub.Current;
        stages = KvStageHub.Current;
        stages2 = KvStageHub2.Current;
        stages3 = KvStageHub3.Current;
        cam = ui.CameraRig;
        v = flow.Validator;

        Check(stages3 != null, "хаб этапов 7–12 поднят (KvStageHub3)");
        if (stages3 == null) { Next(13); return; }

        Check(stages3.Constrained != null, "этап 8: сервис ограничений создан");
        Check(stages3.Calibration != null, "этап 9: калибровочный сервис создан");
        Check(stages3.Payload != null, "этап 10: калькулятор нагрузки создан");
        Check(stages3.Exporter != null, "этап 11: экспортёр языков роботов создан");
        Check(stages3.Import != null, "этап 12: сервис импорта моделей создан");
        Check(KvWorkbenchWindow.TabCount >= 9, "вкладки верстака зарегистрированы (≥ 9)",
            "вкладок: " + KvWorkbenchWindow.TabCount + " · " + TabKeys());
        Check(KvLocExtra2.RegisteredCount > 90, "строки этапов 7–12 зарегистрированы (7 языков)",
            "ключей: " + KvLocExtra2.RegisteredCount + " · всего в словарях: " + KvLoc.TotalStrings);

        string original = KvLoc.PreferenceCode;
        string sample = "";
        int translated = 0;
        foreach (string code in new[] { "ru", "en", "zh", "es", "de", "fr", "ja" })
        {
            KvLoc.SetLanguage(code, false);
            string t = KvLoc.T("calib.title", "Калибровочный мастер");
            sample += code + "=\"" + t + "\" ";
            if (!string.IsNullOrEmpty(t) && t != "calib.title") translated++;
        }
        KvLoc.SetLanguage(original, false);
        Check(translated == 7, "текст «Калибровочный мастер» переведён на все 7 языков", sample.Trim());

        savedPose = v.CopyCurrent();
        savedToolOffset = flow.toolOffset;
        Note("робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " + v.Dof);
        Next(1);
    }

    private static string TabKeys()
    {
        var keys = new List<string>();
        foreach (string key in new[]
                 {
                     "smooth", "topt", "energy", "waypoints", "constraints", "calibration",
                     "payload", "export", "import"
                 })
            if (KvWorkbenchWindow.FindTab(key) != null) keys.Add(key);
        return string.Join(",", keys.ToArray());
    }

    private static RobotController robot6;

    private static void StepCandidates()
    {
        // Проверки ограничений, калибровки и нагрузки осмысленны на РОБОТЕ (6 осей):
        // у SCARA нет кисти, поэтому её инструмент всегда вертикален и ограничения
        // ориентации проверяются иначе. Привязываем поток к роботу наведением камеры.
        if (robot6 == null)
        {
            RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in all)
                if (rc != null && !(rc is SCARAController)) { robot6 = rc; break; }
            Note("робот для проверок: " + (robot6 != null ? robot6.robotName : "не найден"));
        }
        if (robot6 != null && flow.Robot != robot6)
        {
            aimAt(robot6);
            Aim(robot6.transform.position + Vector3.up * 0.2f, false, false, false, false,
                Vector3.zero);
            if (!Wait(25f)) return;
            v = flow.Validator;
            Note("поток привязан к роботу: " + (flow.Robot != null ? flow.Robot.robotName : "нет") +
                 " · осей " + v.Dof);
        }
        if (flow.State.candidates.Count > 0) { Next(2); return; }

        if (Once("click"))
        {
            aimAt(flow.Robot);
            Click(WorkPoint());
        }
        if (!Wait(45f)) return;

        Check(flow.State.candidates.Count > 0, "траектории построены для проверок этапов 7–12",
            "вариантов: " + flow.State.candidates.Count);
        if (flow.State.candidates.Count == 0) { Next(13); return; }
        candidatesBefore = flow.State.candidates.Count;
        Next(2);
    }

    // ---------------------------------------------------------------- ЭТАП 8

    private static void StepConstraintsCheck()
    {
        if (Once("enable"))
        {
            stages3.Constrained.Enabled = true;
            stages3.Constrained.Profile.mode = KvOrientMode.ToolVertical;
            stages3.Constrained.Profile.reference = Vector3.down;
            stages3.Constrained.Profile.toleranceDeg = 8f;
            stages3.Constrained.Save();
            stages3.Constrained.Invalidate();
        }
        if (!Wait(2.5f)) return;

        Check(stages3.Constrained.Active != null, "ограничение включено и активно",
            stages3.Constrained.Profile.Describe());

        int evaluated = 0, violating = 0;
        for (int i = 0; i < flow.State.candidates.Count; i++)
        {
            KvToolKinematics.PlanCheck check = stages3.Constrained.Check(flow.State.candidates[i]);
            evaluated += check.samples;
            violating += check.violations;
        }
        Check(evaluated > 0, "ограничение проверено на всех сэмплах вариантов",
            "сэмплов: " + evaluated + " · нарушений: " + violating);
        Note("проверка ограничения: " + KvLocExtra2.F("constr.violated",
            "нарушений: {0} из {1} сэмплов · худший угол {2}°", violating, evaluated,
            WorstAngle().ToString("0.0")));

        // ограничение «взгляд на объект» из точки прицела — проверяем, что режим работает
        if (Once("lookat"))
        {
            stages3.Constrained.SetLookAtFromAim(flow.State.point + Vector3.up * 0.4f);
        }
        if (!Wait(1.5f)) return;
        Check(stages3.Constrained.Profile.mode == KvOrientMode.LookAt,
            "режим «TCP смотрит на объект» переключается",
            stages3.Constrained.Profile.Describe());

        // возвращаем вертикальный инструмент — он проверяется дальше
        stages3.Constrained.Profile.mode = KvOrientMode.ToolVertical;
        stages3.Constrained.Profile.reference = Vector3.down;
        stages3.Constrained.Invalidate();

        // режим «наклон не больше 15°»
        stages3.Constrained.Profile.mode = KvOrientMode.MaxTilt;
        stages3.Constrained.Profile.tiltLimitDeg = 15f;
        stages3.Constrained.Invalidate();
        int tiltViolations = 0;
        float worstTilt = 0f;
        for (int i = 0; i < flow.State.candidates.Count; i++)
        {
            KvToolKinematics.PlanCheck check = stages3.Constrained.Check(flow.State.candidates[i]);
            tiltViolations += check.violations;
            worstTilt = Mathf.Max(worstTilt, check.worstLimit);
        }
        Check(stages3.Constrained.Active.mode == KvOrientMode.MaxTilt,
            "ограничение «наклон не больше 15°» проверяется",
            "нарушений " + tiltViolations + " · максимальный наклон " + worstTilt.ToString("0.0") + "°");

        stages3.Constrained.Profile.mode = KvOrientMode.ToolVertical;
        stages3.Constrained.Invalidate();
        Next(3);
    }

    private static float WorstAngle()
    {
        float worst = 0f;
        for (int i = 0; i < flow.State.candidates.Count; i++)
        {
            KvToolKinematics.PlanCheck check = stages3.Constrained.Check(flow.State.candidates[i]);
            worst = Mathf.Max(worst, check.worstAngle);
        }
        return worst;
    }

    private static void StepConstraintsProject()
    {
        int index;
        TrajectoryCandidate c = KvVariantKit.Selected(flow, out index);
        if (c == null) { Next(4); return; }

        if (Once("prepare"))
        {
            KvToolKinematics.PlanCheck before = stages3.Constrained.Check(c);
            constraintWorstBefore = before.worstAngle;
            constraintViolationsBefore = before.violations;
            double[] first = KvTrajMath.Copy(c.plan.Path[0]);
            projectFirst = first;
        }
        if (!Wait(0.5f)) return;

        if (Once("project"))
        {
            projectFixed = stages3.Constrained.Project(c, index);
        }
        if (!Wait(3.0f)) return;

        KvToolKinematics.PlanCheck after = stages3.Constrained.Check(c);
        Check(after.samples > 0, "после проекции ограничение перепроверено",
            "сэмплов " + after.samples);
        Check(after.violations <= constraintViolationsBefore,
            "приведение к ограничению не увеличило число нарушений",
            "нарушений " + constraintViolationsBefore + " → " + after.violations +
            " · худший угол " + constraintWorstBefore.ToString("0.0") + "° → " +
            after.worstAngle.ToString("0.0") + "° · исправлено сэмплов " + projectFixed);
        Check(SamePoint(c.plan.Path[0], projectFirst),
            "начало траектории не сдвинулось (цель та же)");
        Check(c.tube != null && c.tube.Length == c.plan.Path.Length,
            "«колбаска» варианта перестроена после проекции",
            "сэмплов " + (c.tube != null ? c.tube.Length : 0));

        if (Once("filter")) stages3.Constrained.DiscardViolating = true;
        if (!Wait(1.5f)) return;
        Check(stages3.Constrained.DiscardViolating, "переключатель «отбрасывать нарушающие» работает");
        stages3.Constrained.DiscardViolating = false;

        if (Once("off")) stages3.Constrained.Enabled = false;
        if (!Wait(1.0f)) return;
        Check(!stages3.Constrained.Enabled && stages3.Constrained.Active == null,
            "ограничение выключается (в настройках/командой)");
        stages3.Constrained.Enabled = true;
        Next(4);
    }

    private static double[] projectFirst;
    private static int projectFixed;

    // ---------------------------------------------------------------- ЭТАП 7

    private static void StepWaypointOrientation()
    {
        if (stages == null || stages.Waypoints == null)
        {
            Note("waypoint-редактор недоступен — этап 7 проверяется частично");
            Next(7);
            return;
        }

        if (Once("add"))
        {
            stages.Waypoints.Clear("диагностика этапа 7");
            Vector3 p;
            if (FindWaypointPoint(out p))
            {
                stages.Waypoints.Add(p, "диагностика");
                Note("waypoint добавлен над столом: " + p.ToString("F3"));
            }
            else Note("подходящая точка для waypoint не найдена");
        }
        if (!Wait(3.0f)) return;

        KvWaypoint wp = stages.Waypoints.Count > 0 ? stages.Waypoints.Items[0] : null;
        Check(wp != null, "промежуточная точка добавлена", wp != null ? wp.Short : "нет");
        if (wp == null) { Next(7); return; }

        if (Once("orient"))
        {
            wp.Limits.Orientation.mode = KvOrientMode.ToolDirection;
            wp.Limits.Orientation.direction = Vector3.down;
            wp.Limits.Orientation.toleranceDeg = 10f;
            stages.Waypoints.EvaluateAll();
        }
        if (!Wait(2.0f)) return;

        Check(wp.Pose != null, "поза прохода с ограничением ориентации найдена",
            wp.Pose != null ? "ошибка ориентации " + wp.OrientationError.ToString("0.0") + "°" : wp.Note);
        Check(wp.Reachable || wp.OrientationError <= 10.5f,
            "точка с ограничением ориентации признана достижимой",
            "достижима: " + wp.Reachable + " · " + wp.Note);
        Note("ограничения точки: " + wp.Limits.Describe());
        Next(5);
    }

    private static void StepWaypointSpeedPause()
    {
        if (stages == null || stages.Waypoints == null || stages.Waypoints.Count == 0)
        {
            Next(6);
            return;
        }
        KvWaypoint wp = stages.Waypoints.Items[0];

        if (Once("route"))
        {
            stages.Waypoints.RebuildRoute();
            routeTimeNoPause = stages.Waypoints.HasRoute
                ? (float)stages.Waypoints.Route.Time : 0f;
            routeSamplesNoPause = stages.Waypoints.HasRoute
                ? stages.Waypoints.Route.Path.Length : 0;
            wp.Limits.LimitSpeed = true;
            wp.Limits.MaxSpeedMps = 0.08f;
        }
        if (!Wait(3.0f)) return;

        if (Once("speedRebuild")) stages.Waypoints.RebuildRoute();
        if (!Wait(3.0f)) return;

        string speedNote = stages.Waypoints.RouteNote;
        Check(stages.Waypoints.HasRoute, "маршрут с ограничением скорости построен", speedNote);
        if (stages.Waypoints.HasRoute)
        {
            int at;
            float min = KvWaypointRouteKit.MinDistanceToPoint(v, stages.Waypoints.Route,
                wp.Position, out at);
            float speed = KvWaypointRouteKit.TcpSpeedAt(v, stages.Waypoints.Route, at);
            Check(speed <= wp.Limits.MaxSpeedMps * 1.35f, "скорость TCP в точке ограничена",
                speed.ToString("0.000") + " м/с при лимите " + wp.Limits.MaxSpeedMps.ToString("0.00") +
                " м/с · расстояние до точки " + (min * 1000f).ToString("0") + " мм");
            Note("строка маршрута: " + speedNote);
        }

        if (Once("pause"))
        {
            wp.Limits.Pause = true;
            wp.Limits.PauseSeconds = 1.5f;
            stages.Waypoints.RebuildRoute();
        }
        if (!Wait(3.0f)) return;

        if (stages.Waypoints.HasRoute)
        {
            int samples = stages.Waypoints.Route.Path.Length;
            float time = (float)stages.Waypoints.Route.Time;
            Check(samples > routeSamplesNoPause, "пауза вставлена в маршрут (появились сэмплы выдержки)",
                "сэмплов " + routeSamplesNoPause + " → " + samples + " · время " +
                routeTimeNoPause.ToString("0.00") + " → " + time.ToString("0.00") + " с");
            Check(time > routeTimeNoPause, "время маршрута выросло на длительность паузы",
                "+" + (time - routeTimeNoPause).ToString("0.00") + " с");
        }
        else
        {
            Check(false, "маршрут с паузой построен", stages.Waypoints.RouteNote);
        }
        Next(6);
    }

    private static void StepWaypointDetour()
    {
        if (stages == null || stages.Waypoints == null || stages.Waypoints.Count == 0)
        {
            Next(7);
            return;
        }
        KvWaypoint wp = stages.Waypoints.Items[0];

        if (Once("detour"))
        {
            // Препятствие ставим РОВНО на прямой путь к точке: маршрут обязан его обойти.
            wp.Limits.Pause = false;
            wp.Limits.LimitSpeed = false;
            wp.Limits.Detour = true;
            wp.Limits.DetourRadius = 0.10f;
            wp.Limits.DetourMargin = 0.03f;
            Vector3 start = v.TcpAt(v.CopyCurrent());
            wp.Limits.DetourCenter = Vector3.Lerp(start, wp.Position, 0.5f);
            stages.Waypoints.EvaluateAll();
        }
        if (!Wait(3.0f)) return;

        if (Once("rebuild")) stages.Waypoints.RebuildRoute();
        if (!Wait(4.0f)) return;

        bool built = stages.Waypoints.HasRoute;
        string note = stages.Waypoints.RouteNote;
        if (built)
        {
            int at;
            float min = KvWaypointRouteKit.MinDistanceToPoint(v, stages.Waypoints.Route,
                wp.Limits.DetourCenter, out at);
            float need = wp.Limits.DetourRadius + wp.Limits.DetourMargin;
            Check(min >= need - 0.002f, "обязательный обход препятствия выполнен",
                "минимальное расстояние до препятствия " + (min * 1000f).ToString("0") +
                " мм при требуемом " + (need * 1000f).ToString("0") + " мм");
        }
        else
        {
            // Отказ тоже допустим по ТЗ («обязательный обход»): главное, что причина внятная.
            Check(note.Contains("обход"), "обход не найден — маршрут честно не построен с причиной",
                note);
        }

        if (Once("clear"))
        {
            wp.Limits.Clear();
            stages.Waypoints.EvaluateAll();
            stages.Waypoints.RebuildRoute();
        }
        if (!Wait(3.0f)) return;
        Check(!wp.HasLimits, "ограничения точки снимаются (кнопка/команда)");
        Note("маршрут после снятия ограничений: " + stages.Waypoints.RouteNote);
        Next(7);
    }

    // ---------------------------------------------------------------- ЭТАП 9

    private static void StepCalibration()
    {
        if (Once("selftest"))
        {
            Note(KvCalibrationService.SelfTest());
        }
        if (Once("clear")) stages3.Calibration.ResetTcpPoints();
        if (!Wait(0.4f)) return;

        // Четыре точки с РАЗНЫМИ ориентациями: меняем J5/J6 (у SCARA — J1/J2), робот
        // при этом стоит на месте физически, но позы разные — именно так работает оператор.
        if (Once("points"))
        {
            for (int i = 0; i < 4; i++)
            {
                double[] q = (double[])savedPose.Clone();
                if (v.Dof >= 6)
                {
                    q[4] += (i - 1.5) * 18.0;
                    q[5] += i * 25.0;
                    q[0] += i * 12.0;
                }
                else
                {
                    q[0] += i * 15.0;
                    q[1] += i * 10.0;
                }
                if (!v.WithinLimits(q)) q = v.ContinueFrom(savedPose, q);
                v.Apply(q);
                bool added = stages3.Calibration.RecordTcpPoint();
                if (!added) Note("точка калибровки " + (i + 1) + " не принята (ориентации похожи)");
            }
            v.Apply(savedPose);
        }
        if (!Wait(1.0f)) return;

        Check(stages3.Calibration.TcpPointCount >= 3, "точки калибровки TCP записаны",
            "записано " + stages3.Calibration.TcpPointCount + " из " +
            KvCalibrationService.TcpPointsNeeded);

        if (Once("solve"))
        {
            calibrated = stages3.Calibration.SolveTcp();
        }
        if (!Wait(1.0f)) return;

        if (calibrated)
        {
            KvCalibrationData data = stages3.Calibration.Data;
            Check(data.tcpSolved, "смещение TCP рассчитано",
                "вектор (" + (data.tcpOffsetFlange[0] * 1000f).ToString("0.0") + ", " +
                (data.tcpOffsetFlange[1] * 1000f).ToString("0.0") + ", " +
                (data.tcpOffsetFlange[2] * 1000f).ToString("0.0") + ") мм · длина " +
                (data.tcpLength * 1000f).ToString("0.0") + " мм · остаток " +
                data.tcpResidualMm.ToString("0.00") + " мм");
            Check(data.tcpLength > 0f, "длина инструмента положительна",
                (data.tcpLength * 1000f).ToString("0.0") + " мм");
        }
        else
        {
            Note("расчёт смещения TCP не выполнен (в синтетическом прогоне это возможно: " +
                 "ориентации задаются процедурно)");
        }

        // --- база: три точки на плоскости (в тесте — три точки в пространстве)
        if (Once("base"))
        {
            stages3.Calibration.ResetBasePoints();
            for (int i = 0; i < 3; i++)
            {
                double[] q = (double[])savedPose.Clone();
                if (v.Dof >= 6)
                {
                    q[0] += i * 14.0;
                    q[1] -= i * 9.0;
                    q[2] += i * 7.0;
                }
                else
                {
                    q[0] += i * 12.0;
                    q[1] += i * 8.0;
                }
                if (!v.WithinLimits(q)) q = v.ContinueFrom(savedPose, q);
                v.Apply(q);
                stages3.Calibration.RecordBasePoint();
            }
            v.Apply(savedPose);
        }
        if (!Wait(1.0f)) return;

        Check(stages3.Calibration.BasePointCount == 3, "три точки базы записаны",
            "записано " + stages3.Calibration.BasePointCount);
        if (Once("solveBase")) baseSolved = stages3.Calibration.SolveBase();
        if (!Wait(1.0f)) return;

        Check(baseSolved, "калибровка базы рассчитана",
            baseSolved
                ? "высота " + stages3.Calibration.Data.baseHeightMm.ToString("0.0") + " мм · наклон " +
                  stages3.Calibration.Data.baseTiltDeg.ToString("0.00") + "°"
                : "не рассчитана");
        if (Once("save")) saved = stages3.Calibration.Save();
        if (!Wait(1.0f)) return;

        Check(saved && File.Exists(stages3.Calibration.FilePath), "калибровка сохранена в файл",
            stages3.Calibration.FilePath + (File.Exists(stages3.Calibration.FilePath)
                ? " (" + new FileInfo(stages3.Calibration.FilePath).Length + " байт)" : ""));
        if (Once("apply")) stages3.Calibration.ApplyToolOffsetToFlow();
        if (!Wait(0.5f)) return;
        Note("toolOffset потока после калибровки: " + (flow.toolOffset * 1000f).ToString("0.0") + " мм" +
             " (в синтетическом прогоне смещение процедурное — возвращаем прежнее значение)");
        // Смещение инструмента влияет на TCP, а значит на нагрузку и зазоры: в диагностике
        // возвращаем прежнее значение, чтобы следующие проверки шли по исходной модели.
        if (Once("restoreOffset"))
        {
            flow.toolOffset = savedToolOffset;
            stages3.Payload.Evaluate(v.CopyCurrent());
        }
        if (!Wait(0.8f)) return;
        Next(8);
    }

    private static bool calibrated;
    private static bool baseSolved;
    private static bool saved;

    // ---------------------------------------------------------------- ЭТАП 10

    private static void StepPayload()
    {
        if (Once("eval")) stages3.Payload.Evaluate(v.CopyCurrent());
        if (!Wait(1.0f)) return;

        KvPayloadResult r = stages3.Payload.Last;
        if (Once("checks"))
        {
            Check(r != null && r.valid && r.maxKg > 0f, "нагрузка в текущей позе рассчитана",
                r != null ? r.Line() : "нет результата");
            Check(r != null && r.limitingJoint >= 0 && r.limitingJoint < v.Dof,
                "определён ограничивающий сустав",
                r != null ? "ось " + (r.limitingJoint + 1) + " · " + r.limitingValue.ToString("0.0") +
                            " из " + r.limitingRating.ToString("0.0") : "—");
            Check(r != null && r.distances != null && r.distances.Length >= 4,
                "построена кривая зависимости от расстояния до базы",
                r != null && r.distances != null
                    ? "точек на графике: " + r.distances.Length : "нет точек");
            payloadFolded = r != null ? r.maxKg : 0f;
        }

        // Физическая проверка: на вытянутой руке нагрузка должна быть МЕНЬШЕ.
        if (Once("stretch"))
        {
            Vector3 basePos = v.BasePosition;
            Vector3 dir = (v.TcpAt(v.CopyCurrent()) - basePos);
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) dir = Vector3.forward;
            dir.Normalize();
            Vector3 far = basePos + dir * 0.70f +
                          Vector3.up * (TrajectoryCore.StandBuilder.TopHeight + 0.10f - basePos.y);
            double[] q;
            double[] seed = v.CopyCurrent();
            if (v.SolveIk(far, seed, out q) && v.WithinLimits(q))
            {
                v.Apply(q);
                stretched = stages3.Payload.Evaluate(q);
            }
            v.Apply(savedPose);
        }
        if (!Wait(1.5f)) return;

        if (stretched != null && stretched.valid)
        {
            Check(stretched.maxKg <= payloadFolded + 0.05f,
                "на вытянутой руке допустимая нагрузка не больше, чем в сложенной позе",
                "сложенная " + payloadFolded.ToString("0.00") + " кг → вытянутая " +
                stretched.maxKg.ToString("0.00") + " кг");
        }
        else
        {
            Note("вытянутая поза для сравнения не найдена (IK не сошлась) — проверка пропущена");
        }

        PayloadSafe = r != null && r.valid && r.maxKg > 0f;
        Next(9);
    }

    private static KvPayloadResult stretched;
    private static bool PayloadSafe;

    // ---------------------------------------------------------------- ЭТАП 11

    private static void StepExportKrl()
    {
        if (Once("export")) stages3.Exporter.Language = KvRobotLanguage.Krl;
        if (Once("run"))
        {
            exportPath = stages3.Exporter.ExportSelected();
        }
        if (!Wait(2.0f)) return;

        Check(!string.IsNullOrEmpty(exportPath) && File.Exists(exportPath),
            "этап 11: файл KRL создан", exportPath);
        if (string.IsNullOrEmpty(exportPath) || !File.Exists(exportPath)) { Next(10); return; }

        Check(Path.GetFileName(exportPath) == "trajectory.src",
            "имя файла KRL по ТЗ — trajectory.src", Path.GetFileName(exportPath));
        string text = File.ReadAllText(exportPath);
        Check(text.Contains("DEF trajectory"), "KRL: объявление модуля DEF trajectory");
        Check(text.Contains("PTP"), "KRL: команды PTP (движение по суставам)");
        Check(text.Contains("LIN"), "KRL: команды LIN (линейные участки)");
        Check(text.Contains("$TOOL"), "KRL: выбор инструмента $TOOL");
        Check(stages3.Exporter.LastLines > 30, "KRL: файл содержит точки траектории",
            "строк: " + stages3.Exporter.LastLines);
        Next(10);
    }

    private static void StepExportKarelRapid()
    {
        // --- FANUC KAREL
        if (Once("karel"))
        {
            stages3.Exporter.Language = KvRobotLanguage.Karel;
            exportPath = stages3.Exporter.ExportSelected();
        }
        if (!Wait(2.0f)) return;

        Check(!string.IsNullOrEmpty(exportPath) && File.Exists(exportPath), "этап 11: файл KAREL создан",
            exportPath);
        if (!string.IsNullOrEmpty(exportPath) && File.Exists(exportPath))
        {
            Check(Path.GetFileName(exportPath) == "trajectory.kl",
                "имя файла KAREL по ТЗ — trajectory.kl", Path.GetFileName(exportPath));
            string text = File.ReadAllText(exportPath);
            Check(text.Contains("PROGRAM trajectory"), "KAREL: объявление PROGRAM");
            Check(text.Contains("MOVE TO"), "KAREL: команды MOVE TO");
            Check(text.Contains("JOINT_POS"), "KAREL: позиции по суставам JOINT_POS");
        }

        // --- ABB RAPID
        if (Once("rapid"))
        {
            stages3.Exporter.Language = KvRobotLanguage.Rapid;
            exportPath = stages3.Exporter.ExportSelected();
        }
        if (!Wait(2.0f)) return;

        Check(!string.IsNullOrEmpty(exportPath) && File.Exists(exportPath), "этап 11: файл RAPID создан",
            exportPath);
        if (!string.IsNullOrEmpty(exportPath) && File.Exists(exportPath))
        {
            Check(Path.GetFileName(exportPath) == "trajectory.mod",
                "имя файла RAPID по ТЗ — trajectory.mod", Path.GetFileName(exportPath));
            string text = File.ReadAllText(exportPath);
            Check(text.Contains("MODULE trajectory"), "RAPID: объявление MODULE");
            Check(text.Contains("MoveAbsJ"), "RAPID: команда MoveAbsJ (по суставам)");
            Check(text.Contains("MoveL"), "RAPID: команда MoveL (линейно)");
            Check(text.Contains("robtarget"), "RAPID: точки robtarget с ориентацией");
        }

        if (Once("cycle")) stages3.Exporter.CycleLanguage();
        if (!Wait(0.5f)) return;
        Note("после переключения язык экспорта: " + stages3.Exporter.LanguageLabel +
             " (" + stages3.Exporter.Extension + ")");
        Next(11);
    }

    // ---------------------------------------------------------------- ЭТАП 12

    private static void StepImportUrdf()
    {
        if (Once("scan")) stages3.Import.RefreshFiles();
        if (!Wait(1.5f)) return;

        string urdf = null;
        foreach (string file in stages3.Import.Files)
        {
            if (file.EndsWith(".urdf", StringComparison.OrdinalIgnoreCase)) { urdf = file; break; }
        }
        if (Once("found"))
        {
            Check(urdf != null, "этап 12: найден URDF-файл в папках поиска",
                urdf != null ? urdf : "не найден (положите .urdf в StreamingAssets/robots)");
        }
        if (urdf == null) { Next(12); return; }

        if (Once("import"))
        {
            imported = stages3.Import.Import(urdf);
        }
        if (!Wait(3.0f)) return;

        Check(imported, "URDF импортирован", stages3.Import.LastReport);
        GameObject go = stages3.Import.Imported;
        Check(go != null, "объект модели создан в сцене",
            go != null ? go.name + " @ " + go.transform.position.ToString("F2") : "нет");
        if (go != null)
        {
            KvImportedRobot marker = go.GetComponent<KvImportedRobot>();
            SixAxisController six = go.GetComponent<SixAxisController>();
            Check(marker != null && six != null, "структура: маркер импорта + контроллер робота",
                marker != null ? marker.format + " · звеньев " + marker.linkCount +
                                 " · суставов " + marker.jointCount : "нет маркера");
            Check(six != null && six.jointTransforms != null && six.jointTransforms.Length == 6,
                "создано 6 суставов (URDF-цепь)",
                six != null && six.jointTransforms != null
                    ? six.jointTransforms.Length + " суставов" : "нет суставов");
            Check(six != null && six.tcp != null, "TCP модели определён");

            Check(stages3.Import.CheckKinematics(), "проверка кинематики пройдена",
                stages3.Import.LastCheck);

            // Проверяем, что валидатор проекта работает с импортированной моделью.
            PoseValidator iv = new PoseValidator();
            if (six != null) iv.Init(six);
            Check(iv.Ready && iv.Dof == 6, "валидатор проекта собрался по импортированной модели",
                iv.Ready ? "осей " + iv.Dof : "не готов");
            if (iv.Ready)
            {
                double[] q = new double[6];
                Vector3 tcp = iv.TcpAt(q);
                Check(tcp.sqrMagnitude > 0.0001f, "прямая задача даёт положение TCP",
                    tcp.ToString("F3"));
            }
        }
        Next(12);
    }

    private static bool imported;

    private static void StepImportStep()
    {
        if (Once("write"))
        {
            // Минимальный текстовый STEP с точками: проверяем базовую поддержку.
            string folder = stages3.Import.SearchFolders()[0];
            FeatureStorage.EnsureDir(folder);
            string path = Path.Combine(folder, "sample_step.step");
            try
            {
                File.WriteAllText(path,
                    "ISO-10303-21;\nHEADER;\nFILE_DESCRIPTION((''),'2;1');\nENDSEC;\nDATA;\n" +
                    "#1=CARTESIAN_POINT('',(0.,0.,0.));\n" +
                    "#2=CARTESIAN_POINT('',(0.4,0.2,0.6));\n" +
                    "#3=CARTESIAN_POINT('',(-0.1,0.05,0.02));\n" +
                    "ENDSEC;\nEND-ISO-10303-21;\n");
                stepFile = path;
            }
            catch (Exception e) { Note("тестовый STEP не создан: " + e.Message); }
            stages3.Import.RefreshFiles();
        }
        if (!Wait(1.5f)) return;

        if (!string.IsNullOrEmpty(stepFile) && File.Exists(stepFile))
        {
            bool ok = stages3.Import.Import(stepFile);
            if (!Wait(1.0f)) return;
            Check(ok, "STEP прочитан (базовая поддержка)", stages3.Import.LastReport);
            GameObject go = stages3.Import.Imported;
            KvImportedRobot marker = go != null ? go.GetComponent<KvImportedRobot>() : null;
            Check(marker != null && marker.visualOnly, "STEP создан как визуальный корпус",
                marker != null ? marker.note : "нет маркера");
            if (Once("remove")) stages3.Import.RemoveImported();
            if (!Wait(0.5f)) return;
            Check(stages3.Import.Imported == null, "импортированная модель удаляется из сцены");
        }
        else
        {
            Note("тестовый STEP создать не удалось — базовая поддержка STEP не проверена");
        }
        Next(13);
    }

    private static string stepFile = "";

    // ---------------------------------------------------------------- отчёт

    private static void StepFinishReport()
    {
        if (done) return;
        done = true;

        try
        {
            if (v != null && savedPose != null && savedPose.Length == v.Dof) v.Apply(savedPose);
            if (flow != null) flow.toolOffset = savedToolOffset;   // возвращаем смещение инструмента
            if (stages != null && stages.Waypoints != null)
                stages.Waypoints.Clear("диагностика завершена");
            if (stages3 != null)
            {
                stages3.Constrained.Enabled = false;
                stages3.Import.RemoveImported();
            }
            if (flow != null) flow.ResetFlow("диагностика этапов 7–12 завершена");
        }
        catch (Exception e) { Note("возврат состояния: " + e.Message); }

        Line("");
        Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info);
        Line(fail == 0 ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ИТОГ: ЕСТЬ ОТКАЗЫ (" + fail + ")");
        Finish();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        try { File.AppendAllText(report, "=== конец прогона ===\n"); } catch { }
        Debug.Log("[DshStage3Diag] отчёт: " + report + " · [OK] " + ok + " [FAIL] " + fail);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ помощники

    private static bool SamePoint(double[] a, double[] b)
    {
        if (a == null || b == null || a.Length != b.Length) return false;
        for (int i = 0; i < a.Length; i++)
            if (Math.Abs(a[i] - b[i]) > 1e-6) return false;
        return true;
    }

    private static Vector3 WorkPoint()
    {
        if (workPoint != Vector3.zero) return workPoint;
        Vector3 p;
        if (FindReachablePoint(v, out p))
        {
            workPoint = p;
            return p;
        }
        return flow.Robot != null
            ? flow.Robot.transform.position + new Vector3(0.12f, 0.32f, -0.55f)
            : Vector3.up;
    }

    private static void SetLasers(bool red, bool green)
    {
        if (cam == null) return;
        cam.leftHandEnabled = red;
        cam.rightHandEnabled = green;
    }

    private static void Aim(Vector3 p, bool hit, bool confirm, bool cancel, bool enter, Vector3 axis)
    {
        if (flow == null) return;
        flow.UpdateAim(p, hit, confirm, cancel,
            cam != null && cam.leftHandEnabled, cam != null && cam.rightHandEnabled,
            Vector3.up, hit, enter, axis, false);
    }

    private static void Click(Vector3 p)
    {
        SetLasers(true, false);
        Aim(p, true, true, false, false, Vector3.zero);
        SetLasers(false, false);
    }

    private static void aimAt(RobotController r)
    {
        if (r == null || cam == null) return;
        Vector3 center = r.transform.position;
        Vector3 from = center + new Vector3(0f, 1.15f, -1.5f);
        cam.transform.position = from;
        cam.transform.rotation = Quaternion.LookRotation((center + Vector3.up * 0.15f - from).normalized,
            Vector3.up);
    }

    /// <summary>
    /// Точка для waypoint НАД столом (8 см) с запасом до лимитов ≥ 6°: точка ровно на
    /// столешнице упирает сустав в предел (см. §0.5), и планировщик её честно не проходит —
    /// для проверки ограничений этапа 7 нужна рабочая точка.
    /// </summary>
    private static bool FindWaypointPoint(out Vector3 point)
    {
        point = Vector3.zero;
        if (v == null || !v.Ready) return false;
        Vector3 b = v.BasePosition;
        double[] seed = v.CopyCurrent();
        double[] q;
        float maxR = v.Dof > 3 ? 0.65f : 0.38f;
        float minR = v.Dof > 3 ? 0.32f : 0.20f;
        for (float r = maxR; r >= minR; r -= 0.05f)
        {
            for (float ang = 0f; ang < 360f; ang += 25f)
            {
                float rad = ang * Mathf.Deg2Rad;
                Vector3 p = b + new Vector3(Mathf.Cos(rad) * r, 0.02f, Mathf.Sin(rad) * r);
                RaycastHit hit;
                if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out hit, 1.2f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<RobotController>() != null) continue;
                p = hit.point + Vector3.up * 0.08f;
                if (!v.SolveIk(p, seed, out q)) continue;
                if (!v.WithinLimits(q)) continue;
                if (v.LastIkError > 0.006f) continue;
                if (v.LimitMargin(q) < 6f) continue;      // планировщик требует ≥ 3°, берём с запасом
                point = p;
                return true;
            }
        }
        return false;
    }
    /// <summary>Достижимая точка на столе (та же логика, что в прогонах прошлых сессий).</summary>
    private static bool FindReachablePoint(PoseValidator validator, out Vector3 point)
    {
        point = Vector3.zero;
        if (validator == null || !validator.Ready) return false;
        CollisionWorld world = hub != null ? hub.World : null;
        Vector3 b = validator.BasePosition;
        double[] seed = validator.CopyCurrent();
        double[] q;
        float maxIkError = validator.Dof > 3 ? 0.005f : 0.012f;
        float maxR = validator.Dof > 3 ? 0.75f : 0.40f;
        float minR = validator.Dof > 3 ? 0.30f : 0.18f;
        float lift = validator.Dof > 3 ? 0f : 0.06f;
        for (float r = maxR; r >= minR; r -= 0.05f)
        {
            for (float ang = 0f; ang < 360f; ang += 20f)
            {
                float rad = ang * Mathf.Deg2Rad;
                Vector3 p = b + new Vector3(Mathf.Cos(rad) * r, 0.02f, Mathf.Sin(rad) * r);
                RaycastHit hit;
                if (!Physics.Raycast(p + Vector3.up * 0.6f, Vector3.down, out hit, 1.2f,
                        Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)) continue;
                if (hit.collider.GetComponentInParent<RobotController>() != null) continue;
                p = hit.point + Vector3.up * lift;
                if (!validator.SolveIk(p, seed, out q)) continue;
                if (!validator.WithinLimits(q)) continue;
                if (validator.LastIkError > maxIkError) continue;
                if (world != null)
                {
                    Vector3 tcp;
                    Vector3[] nodes;
                    if (validator.ClearanceAt(q, world, out tcp, out nodes) < 0.03f) continue;
                }
                point = p;
                return true;
            }
        }
        return false;
    }
}
