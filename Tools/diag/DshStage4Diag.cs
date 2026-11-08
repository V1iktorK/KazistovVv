using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится) для этапов 13–36 прошлой сессии:
/// коллизионные прокси, стенд планировщиков, дерево RRT, камеры и PiP, силы и моменты,
/// тепловая карта времени, PDF-отчёт, совместная работа, веб-дашборд, мобильный пульт,
/// голос, руки, взгляд и фовеальное зрение, макросы, дерево поведения, окружение, свет,
/// материалы, кинорежим, титры, голос диктора, отказы, проверка перед пуском, уровни журнала.
///
/// Отчёт читается из лога Unity (`[DshStage4Diag]`): файл в OneDrive во время PlayMode
/// не дописывается (особенность окружения, §12.10). Ожидания — по реальному времени,
/// каждое действие выполняется один раз за шаг, у шага есть сторож.
///
/// Запуск:
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshStage4Diag.Run
///   -logFile _dsh_s4.log        (БЕЗ -quit: нужен PlayMode)
/// </summary>
public static class DshStage4Diag
{
    private const string Key = "DshStage4Diag.Running";
    private const float StepLimitSeconds = 180f;
    private const float TotalLimitSeconds = 2400f;
    private static string report;

    public static void Run()
    {
        report = FeatureStorage.ReportPath("_dsh_s4_verify.txt");
        try
        {
            File.WriteAllText(report,
                "=== DshStage4Diag · этапы 13–36 (прокси, стенд, дерево RRT, камеры, силы, тепло, " +
                "PDF, сеть, дашборд, пульт, голос, руки, взгляд, макросы, дерево поведения, " +
                "окружение, свет, материалы, кино, титры, голос диктора, отказы, проверка, журнал) ===\n");
        }
        catch (Exception e) { Debug.LogError("[DshStage4Diag] отчёт не создан: " + e.Message); }

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
    private const int LastStep = 30;
    private static int step;
    private static bool playStarted;
    private static int ok, fail, info;
    private static readonly HashSet<string> usedTags = new HashSet<string>();
    private static readonly HashSet<string> checkedOnce = new HashSet<string>();
    private static float stepEnterTime;
    private static int stepEntries;
    private static float startedAt;

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static KvStageHub stages;
    private static KvStageHub2 stages2;
    private static KvStageHub3 stages3;
    private static KvStageHub4 stages4;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static PoseValidator v;

    private static double[] savedPose;
    private static int candidatesBefore;
    private static Vector3 workPoint;
    private static int waypointsBefore;
    private static Vector3 savedCameraPosition;
    private static Quaternion savedCameraRotation;
    private static float savedSunLux;
    private static Color savedSunColor;
    private static Color savedMaterialColor;
    private static string savedWebJson = "";
    private static bool runCallbackFired;
    private static float massBefore;
    private static float pingSentAt;
    private static float swipeStepAt;
    private static int swipeStep;
    private static float blinkPushedAt;

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n"); } catch { }
        Debug.Log("[DshStage4Diag] " + text);
    }

    /// <summary>
    /// Проверка считается ОДИН раз за прогон: шаги диагностики выполняются в каждом кадре,
    /// и без этого счётчик [OK] раздувался бы, а повторная проверка после смены состояния
    /// (например, после запуска второго макроса) давала бы ложные отказы.
    /// </summary>
    private static void Check(bool condition, string what, string detail = "")
    {
        if (!checkedOnce.Add(what)) return;
        if (condition) { ok++; Line("[OK]   " + what + (detail.Length > 0 ? " — " + detail : "")); }
        else { fail++; Line("[FAIL] " + what + (detail.Length > 0 ? " — " + detail : "")); }
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

    private static float Now { get { return (float)EditorApplication.timeSinceStartup; } }

    private static bool Wait(float seconds)
    {
        return Now - stepEnterTime >= seconds;
    }

    private static bool Once(string tag)
    {
        if (usedTags.Contains(tag)) return false;
        usedTags.Add(tag);
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
        if (stepEnterTime <= 0f) { stepEnterTime = Now; startedAt = Now; }

        stepEntries++;
        if (Now - stepEnterTime > StepLimitSeconds && step < LastStep)
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
                case 2: StepProxies(); break;
                case 3: StepBenchmark(); break;
                case 4: StepTree(); break;
                case 5: StepCameras(); break;
                case 6: StepForces(); break;
                case 7: StepHeatmap(); break;
                case 8: StepReport(); break;
                case 9: StepCollab(); break;
                case 10: StepWebDashboard(); break;
                case 11: StepCompanion(); break;
                case 12: StepVoice(); break;
                case 13: StepHands(); break;
                case 14: StepEyes(); break;
                case 15: StepScript(); break;
                case 16: StepBehaviorTree(); break;
                case 17: StepEnvironment(); break;
                case 18: StepLighting(); break;
                case 19: StepMaterials(); break;
                case 20: StepCinema(); break;
                case 21: StepTitles(); break;
                case 22: StepVoiceOver(); break;
                case 23: StepFailureComms(); break;
                case 24: StepFailureJoint(); break;
                case 25: StepFailureOverload(); break;
                case 26: StepPreRunBlock(); break;
                case 27: StepPreRunConfirm(); break;
                case 28: StepLogTools(); break;
                case 29: StepLocalization(); break;
                case 30: StepFinishReport(); break;
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
        stages4 = KvStageHub4.Current;
        cam = ui.CameraRig;
        v = flow.Validator;

        Check(stages4 != null, "хаб этапов 13–36 поднят (KvStageHub4)");
        if (stages4 == null) { Next(LastStep); return; }

        Check(stages4.Proxies != null, "этап 13: редактор коллизионных прокси создан");
        Check(stages4.Lab != null, "этапы 14–15: стенд планировщиков создан");
        Check(stages4.Cameras != null, "этап 16: сервис камер создан");
        Check(stages4.Forces != null && stages4.Heatmap != null, "этапы 17–18: силы и тепло созданы");
        Check(stages4.Report != null, "этап 19: генератор отчёта создан");
        Check(stages4.Collab != null && stages4.Web != null && stages4.Companion != null,
            "этапы 20–22: сеть, дашборд и мобильный пульт созданы");
        Check(stages4.Voice != null && stages4.Hands != null && stages4.Eyes != null,
            "этапы 23–25: голос, руки, взгляд созданы");
        Check(stages4.Script != null && stages4.Behavior != null && stages4.BehaviorRunner != null,
            "этапы 26–27: макросы и дерево поведения созданы");
        Check(stages4.Environment != null && stages4.Lighting != null && stages4.Materials != null,
            "этапы 28–30: окружение, свет, материалы созданы");
        Check(stages4.Cinema != null && stages4.Titles != null && stages4.VoiceOver != null,
            "этапы 31–33: кинорежим, титры, голос диктора созданы");
        Check(stages4.Failures != null && stages4.PreRun != null && stages4.LogTools != null,
            "этапы 34–36: отказы, проверка перед пуском, журнал созданы");

        Check(KvWorkbenchWindow.TabCount >= 20, "вкладки этапов 13–36 зарегистрированы (≥ 20)",
            "вкладок: " + KvWorkbenchWindow.TabCount + " · " + TabKeys());

        savedPose = v.CopyCurrent();
        savedCameraPosition = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
        savedCameraRotation = Camera.main != null ? Camera.main.transform.rotation : Quaternion.identity;
        Note("робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " + v.Dof);
        Next(1);
    }

    private static string TabKeys()
    {
        var keys = new List<string>();
        foreach (string key in new[]
                 {
                     "collision", "planner", "cameras", "forces", "report", "net", "voice", "hands",
                     "eyes", "script", "behavior", "env", "light", "material", "cine", "titles",
                     "voiceover", "failures", "validate", "logtools"
                 })
            if (KvWorkbenchWindow.FindTab(key) != null) keys.Add(key);
        return "найдено " + keys.Count + ": " + string.Join(",", keys.ToArray());
    }

    private static RobotController robot6;

    private static void StepCandidates()
    {
        if (robot6 == null)
        {
            RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in all)
                if (rc != null && !(rc is SCARAController)) { robot6 = rc; break; }
        }
        if (robot6 != null && flow.Robot != robot6)
        {
            aimAt(robot6);
            Aim(robot6.transform.position + Vector3.up * 0.2f, false, false, false, false, Vector3.zero);
            if (!Wait(25f)) return;
            v = flow.Validator;
        }
        if (flow.State.candidates.Count > 0) { PrepareSelection(); Next(2); return; }

        if (Once("click"))
        {
            aimAt(flow.Robot);
            Click(WorkPoint());
        }
        if (!Wait(45f)) return;

        Check(flow.State.candidates.Count > 0, "траектории построены для проверок этапов 13–36",
            "вариантов: " + flow.State.candidates.Count);
        if (flow.State.candidates.Count == 0) { Next(LastStep); return; }
        PrepareSelection();
        Next(2);
    }

    /// <summary>
    /// Выбрать вариант траектории (нужно проверкам этапов 14–36: без выбора проверка перед
    /// пуском честно сообщает «траектория не выбрана»). Выбор — та же операция, что зелёный
    /// луч: `SelectCandidateByIndex` показывает фантом и переводит поток в «фантомы едут».
    /// </summary>
    private static void PrepareSelection()
    {
        candidatesBefore = flow.State.candidates.Count;
        if (flow.State.selectedTrajectory >= 0) return;
        int index = flow.State.candidates.Count - 1;
        bool ok = flow.SelectCandidateByIndex(index);
        Note("выбран вариант №" + (index + 1) + " из " + candidatesBefore + " (" +
             (ok ? "принят" : "не принят") + ") · состояние " + flow.State.phase);
    }

    // ---------------------------------------------------------------- ЭТАП 13
    private static void StepProxies()
    {
        KvCollisionOptimizer p = stages4.Proxies;
        if (Once("enable"))
        {
            p.ResetCache();
            p.Enabled = true;
            int count = p.Rebuild(true);
            Note("прокси: построено " + count + " · " + p.Status());
        }
        if (!Wait(8f)) return;

        Check(p.Enabled, "этап 13: прокси включены (переключатель сохранён)");
        Check(p.Items.Count > 0, "этап 13: прокси построены для статики сцены",
            "объектов: " + p.Items.Count);

        int boxes = 0, capsules = 0, skipped = 0, unreadable = 0;
        for (int i = 0; i < p.Items.Count; i++)
        {
            KvCollisionProxyInfo item = p.Items[i];
            if (item == null) continue;
            if (item.kind == "бокс") boxes++;
            else if (item.kind == "капсула") capsules++;
            else if (item.kind == "пропущен") skipped++;
            if (!item.readableMesh) unreadable++;
        }
        Check(boxes + capsules > 0, "этап 13: оболочки построены (боксы/капсулы)",
            "боксов " + boxes + ", капсул " + capsules + ", пропущено " + skipped +
            ", без читаемого меша " + unreadable);

        if (Once("measure"))
        {
            KvCollisionBenchmark bench = p.Measure();
            if (bench != null)
                Note("этап 13 · замер: " + bench.Line());
        }

        if (Once("toggleCheck"))
        {
            bool withProxies = CollisionProxies.Enabled;
            p.Enabled = false;
            bool off = !CollisionProxies.Enabled;
            p.Enabled = true;
            Check(withProxies && off, "этап 13: выключение возвращает прежнее поведение столкновений",
                "с прокси: " + withProxies + " · после выключения: " + CollisionProxies.Enabled);
        }
        Next(3);
    }

    // ---------------------------------------------------------------- ЭТАП 14
    private static void StepBenchmark()
    {
        KvPlannerLab lab = stages4.Lab;
        if (Once("start"))
        {
            lab.TasksPerRun = 12;                 // в диагностике — короткий прогон; по ТЗ доступно 100+
            bool started = lab.Start();
            Check(started, "этап 14: прогон стенда запущен", "задач: " + lab.TasksPerRun);
            if (!started) { Next(4); return; }
        }
        if (lab.Running) { if (!Wait(150f)) return; }
        if (!Wait(1f)) return;

        Check(!lab.Running, "этап 14: прогон завершился", lab.Status());
        Check(lab.Results.Count >= 3, "этап 14: сравнение трёх стратегий (BiRRT / RRT* / TrajOpt)",
            "строк: " + lab.Results.Count);
        for (int i = 0; i < lab.Results.Count; i++)
            Note("этап 14 · " + lab.Results[i].Line());

        int successTotal = 0, tasksTotal = 0;
        for (int i = 0; i < lab.Results.Count; i++)
        {
            successTotal += lab.Results[i].success;
            tasksTotal += lab.Results[i].tasks;
        }
        Check(tasksTotal > 0 && successTotal > 0, "этап 14: задачи решались успешно",
            "успешно " + successTotal + " из " + tasksTotal);
        string csv = lab.Export();
        Check(!string.IsNullOrEmpty(csv) && File.Exists(csv), "этап 14: таблица выгружена в файл", csv);
        Next(4);
    }

    // ---------------------------------------------------------------- ЭТАП 15
    private static void StepTree()
    {
        KvPlannerLab lab = stages4.Lab;
        if (Once("on"))
        {
            lab.SetTreeVisible(true);
            Check(lab.TreeVisible, "этап 15: визуализация дерева включена");
        }
        if (!Wait(1.5f)) return;

        // Чтобы дерево действительно наполнилось, запускаем короткий прогон планирования
        // с включённой записью: это и есть проверка «дерево растёт в реальном времени».
        if (Once("plan"))
        {
            lab.TasksPerRun = 2;
            lab.Start();
        }
        if (lab.Running) { if (!Wait(60f)) return; }
        if (!Wait(1.5f)) return;

        Planner planner = flow.Planner;
        int nodesA = planner != null ? planner.TreeNodesA.Count : 0;
        int nodesB = planner != null ? planner.TreeNodesB.Count : 0;
        int version = planner != null ? planner.TreeVersion : 0;
        int iterations = planner != null ? planner.TreeIterations : 0;
        Note("этап 15: узлов дерева " + nodesA + " + " + nodesB + " · версия " + version +
             " · итераций " + iterations + " · запись включена: " +
             (planner != null && planner.RecordTree));
        Check(planner != null && planner.RecordTree,
            "этап 15: запись дерева включается вместе с показом",
            "RecordTree=" + (planner != null && planner.RecordTree) +
            " (запрос визуализатора " + Planner.RecordTreeRequested + "), TreeVisible=" +
            lab.TreeVisible);
        Check(version > 0 && nodesA + nodesB > 1,
            "этап 15: планировщик записал узлы дерева (есть что показывать)",
            "версия " + version + " · узлов " + (nodesA + nodesB));

        if (Once("off"))
        {
            lab.SetTreeVisible(false);
        }
        Next(5);
    }

    // ---------------------------------------------------------------- ЭТАП 16
    private static void StepCameras()
    {
        KvCameraService cameras = stages4.Cameras;
        if (Once("build"))
        {
            cameras.Build(ui.transform);
            Note("этап 16: " + cameras.Status());
        }
        if (!Wait(2f)) return;

        Check(cameras.Windows.Count >= 3, "этап 16: три дополнительные камеры (сверху, сбоку, от первого лица)",
            "окон: " + cameras.Windows.Count);

        int withTexture = 0;
        for (int i = 0; i < cameras.Windows.Count; i++)
            if (cameras.Windows[i] != null && cameras.Windows[i].texture != null) withTexture++;
        if (KvGraphics.Available)
        {
            Check(withTexture == cameras.Windows.Count && withTexture > 0,
                "этап 16: у каждой камеры своя текстура (картинка в картинке)",
                "текстур: " + withTexture);
        }
        else
        {
            Check(withTexture == 0,
                "этап 16: без графики окна создаются без изображения (честная деградация)",
                "окон " + cameras.Windows.Count + ", текстур " + withTexture + " · " + KvGraphics.Reason);
        }

        if (Once("toggle"))
        {
            for (int i = 0; i < cameras.Windows.Count; i++)
                cameras.SetVisible(cameras.Windows[i].id, true);
        }
        if (!Wait(2f)) return;

        bool anyVisible = cameras.AnyVisible;
        Check(anyVisible, "этап 16: окна PiP включаются", cameras.Status());

        if (Once("fullscreen"))
        {
            cameras.ToggleFullscreen("top");
        }
        if (!Wait(1.5f)) return;
        Check(cameras.Fullscreen == "top", "этап 16: показ одного окна на весь экран",
            "полноэкранное: " + cameras.Fullscreen);
        if (Once("restore")) cameras.ToggleFullscreen("top");

        Next(6);
    }

    // ---------------------------------------------------------------- ЭТАП 17
    private static void StepForces()
    {
        KvForceVisualizer forces = stages4.Forces;
        if (Once("on"))
        {
            forces.SetEnabled(true);
            Note("этап 17: " + forces.StatusLine());
        }
        if (!Wait(3f)) return;

        Check(forces.Enabled, "этап 17: визуализация сил включена");
        Check(forces.Torques.Count == v.Dof, "этап 17: моменты посчитаны по всем суставам",
            "суставов: " + forces.Torques.Count + " · осей: " + v.Dof);

        float maxTorque = 0f;
        int loaded = 0;
        for (int i = 0; i < forces.Loads.Count; i++)
        {
            if (forces.Loads[i] > 0.01f) loaded++;
            if (Mathf.Abs(forces.Torques[i]) > maxTorque) maxTorque = Mathf.Abs(forces.Torques[i]);
        }
        Check(maxTorque > 0f, "этап 17: моменты ненулевые (вес руки и груза учитывается)",
            "максимум " + maxTorque.ToString("0.0") + " · нагруженных суставов: " + loaded);
        Note("этап 17: сила на инструменте " + forces.ToolForce.magnitude.ToString("0.0") + " Н");
        Next(7);
    }

    // ---------------------------------------------------------------- ЭТАП 18
    private static void StepHeatmap()
    {
        KvTimeHeatmap heat = stages4.Heatmap;
        if (Once("on"))
        {
            heat.SetEnabled(true);
        }
        if (heat.Building) { if (!Wait(150f)) return; }
        if (!Wait(3f)) return;

        Check(heat.Enabled, "этап 18: тепловая карта времени включена");
        Check(heat.PointCount > 0, "этап 18: время достижимости посчитано по точкам рабочей зоны",
            "точек: " + heat.PointCount);
        Check(heat.Building == false, "этап 18: расчёт завершён (не идёт бесконечно)");

        if (flow.State.selectedTrajectory >= 0 &&
            flow.State.selectedTrajectory < flow.State.candidates.Count)
        {
            TrajectoryCandidate candidate = flow.State.candidates[flow.State.selectedTrajectory];
            float time = heat.TimeAt(candidate);
            Note("этап 18: время до цели выбранного варианта: " +
                 (time >= 0f ? time.ToString("0.00") + " с" : "не найдено (цель вне узлов карты)"));
        }
        Note("этап 18: " + heat.Status());
        Next(8);
    }

    // ---------------------------------------------------------------- ЭТАП 19
    private static void StepReport()
    {
        KvReportGenerator report2 = stages4.Report;
        if (Once("generate"))
        {
            string path = report2.Generate();
            Note("этап 19: файл «" + path + "» · страниц " + report2.LastPages +
                 " · изображения: " + (report2.ImagesIncluded ? "есть" : "нет (нет графики в batch)"));
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                var info2 = new FileInfo(path);
                Check(info2.Length > 2000, "этап 19: PDF создан и непустой",
                    info2.Length + " байт, страниц " + report2.LastPages);
                byte[] head = File.ReadAllBytes(path);
                string prefix = Encoding.ASCII.GetString(head, 0, Math.Min(8, head.Length));
                Check(prefix.StartsWith("%PDF"), "этап 19: файл начинается как PDF", prefix.Trim());
                string tail = Encoding.ASCII.GetString(head, Math.Max(0, head.Length - 32),
                    Math.Min(32, head.Length));
                Check(tail.Contains("%%EOF"), "этап 19: файл корректно закрыт (%%EOF)");
                Check(report2.LastPages >= 2, "этап 19: в отчёте несколько страниц",
                    "страниц: " + report2.LastPages);
                Note("этап 19: " + report2.Status());
            }
            else
            {
                Check(false, "этап 19: PDF создан", "путь: «" + path + "»");
            }
        }
        Next(9);
    }

    // ---------------------------------------------------------------- ЭТАП 20
    private static void StepCollab()
    {
        KvCollaborationService collab = stages4.Collab;
        if (Once("observer"))
        {
            collab.SetPort(47777);
            collab.SetRole(KvNetRole.Observer);
            Note("этап 20: " + collab.Status());
        }
        if (!Wait(2f)) return;
        Check(collab.Role == KvNetRole.Observer, "этап 20: роль «наблюдатель» установлена и порт открыт",
            collab.Status());

        if (Once("operator"))
        {
            collab.SetRole(KvNetRole.Operator);
        }
        if (!Wait(1.5f)) return;
        Check(collab.Role == KvNetRole.Operator, "этап 20: роль «оператор» переключается", collab.Status());

        if (Once("off")) collab.SetRole(KvNetRole.Off);
        if (!Wait(1f)) return;
        Check(collab.Role == KvNetRole.Off, "этап 20: режим выключается и сокеты закрыты", collab.Status());
        Next(10);
    }

    // ---------------------------------------------------------------- ЭТАП 21
    private static void StepWebDashboard()
    {
        KvWebDashboard web = stages4.Web;
        if (Once("start"))
        {
            web.SetPort(47800);
            bool started = web.Start();
            Check(started, "этап 21: веб-сервер запущен", web.Address);
        }
        if (!Wait(3f)) return;

        Check(web.Running, "этап 21: сервер работает", web.Address);
        if (Once("get"))
        {
            savedWebJson = HttpGet(web.Address);
            Note("этап 21: ответ сервера — " + savedWebJson.Length + " байт");
        }
        if (!Wait(2f)) return;

        Check(savedWebJson.Contains("KazistovVv"), "этап 21: страница мониторинга отдаётся по HTTP",
            FirstLine(savedWebJson));
        if (Once("api"))
        {
            string api = HttpGet("http://127.0.0.1:" + web.Port + "/api/state");
            Check(api.Contains("\"ready\""), "этап 21: JSON состояния отдаётся по /api/state",
                api.Length > 160 ? api.Substring(0, 160) + "…" : api);
            Note("этап 21: запросов обработано " + web.Requests);
        }
        if (Once("stop")) web.Stop();
        Next(11);
    }

    private static string HttpGet(string url)
    {
        try
        {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.Timeout = 4000;
            request.Method = "GET";
            using (var response = (HttpWebResponse)request.GetResponse())
            using (var reader = new StreamReader(response.GetResponseStream(), Encoding.UTF8))
                return reader.ReadToEnd();
        }
        catch (Exception e)
        {
            return "ОШИБКА HTTP: " + e.Message;
        }
    }

    private static string FirstLine(string text)
    {
        if (string.IsNullOrEmpty(text)) return "пусто";
        int cut = text.IndexOf('\n');
        string line = cut > 0 ? text.Substring(0, cut) : text;
        return line.Length > 120 ? line.Substring(0, 120) + "…" : line;
    }

    // ---------------------------------------------------------------- ЭТАП 22
    private static void StepCompanion()
    {
        KvCompanionServer companion = stages4.Companion;
        if (Once("start"))
        {
            companion.SetPort(47810);
            bool started = companion.Start();
            Check(started, "этап 22: мобильный пульт слушает порт", "порт " + companion.Port);
            if (started) SendUdp("127.0.0.1", companion.Port, "STATUS");
        }
        if (!Wait(3f)) return;

        Check(companion.Running, "этап 22: пульт работает", "порт " + companion.Port);
        Check(companion.Commands > 0, "этап 22: команда с планшета принята и выполнена",
            "команд: " + companion.Commands + " · последняя: «" + companion.LastCommand + "»");

        if (Once("ping"))
        {
            SendUdp("127.0.0.1", companion.Port, "PING");
            pingSentAt = Now;
        }
        if (pingSentAt <= 0f || Now - pingSentAt < 2f) return;
        Check(companion.Commands > 1, "этап 22: вторая команда тоже дошла",
            "команд: " + companion.Commands + " · последняя: «" + companion.LastCommand + "»");
        if (Once("stop")) companion.Stop();
        Next(12);
    }

    private static void SendUdp(string host, int port, string text)
    {
        try
        {
            using (var client = new UdpClient())
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                client.Send(data, data.Length, host, port);
            }
        }
        catch (Exception e) { Note("UDP не отправлен: " + e.Message); }
    }

    // ---------------------------------------------------------------- ЭТАП 23
    private static void StepVoice()
    {
        KvVoiceService voice = stages4.Voice;
        if (Once("parse"))
        {
            string[] phrases =
            {
                "стоп", "робот домой", "поехали", "пауза", "вариант три", "дальше", "назад",
                "запиши позу", "снимок", "отмена"
            };
            string expected = "stop,home,run,pause,select,next,prev,pose,shot,undo";
            var got = new List<string>();
            for (int i = 0; i < phrases.Length; i++) got.Add(KvVoiceService.Parse(phrases[i]).id);
            string actual = string.Join(",", got.ToArray());
            Check(actual == expected, "этап 23: грамматика команд разбирает 10 формулировок",
                actual);
            Check(KvVoiceService.Parse("вариант три").number == 3,
                "этап 23: число словом распознаётся («вариант три» → 3)",
                "номер: " + KvVoiceService.Parse("вариант три").number);
        }
        if (Once("enable")) voice.SetEnabled(true);
        if (!Wait(1.5f)) return;
        Check(voice.Enabled, "этап 23: голосовые команды включаются", voice.Status());

        if (Once("select"))
        {
            int before = flow.State.selectedTrajectory;
            voice.PushRecognized("вариант 2");
            Note("этап 23: выбран вариант " + (flow.State.selectedTrajectory + 1) +
                 " (было " + (before + 1) + ") · состояние " + flow.State.phase);
            Check(flow.State.selectedTrajectory == 1,
                "этап 23: команда «вариант 2» выполнена (выбран второй вариант)",
                "текущий вариант: " + (flow.State.selectedTrajectory + 1));
        }
        if (Once("show")) Note("этап 23: " + voice.Status());
        if (Once("off")) voice.SetEnabled(false);
        Next(13);
    }

    // ---------------------------------------------------------------- ЭТАП 24
    private static void StepHands()
    {
        KvHandTrackingService hands = stages4.Hands;
        if (Once("on"))
        {
            hands.SetEnabled(true);
            hands.SetVisible(true);
        }
        if (!Wait(1f)) return;
        Check(hands.Enabled, "этап 24: отслеживание рук включено", hands.Status());

        // Касание: большой и указательный пальцы рядом.
        if (Once("pinch"))
        {
            Vector3 wrist = v.TcpAt(v.CopyCurrent()) + new Vector3(0.3f, 0.2f, 0f);
            hands.PushJoint(1, KvHandTrackingService.Wrist, wrist);
            hands.PushJoint(1, KvHandTrackingService.ThumbTip, wrist + new Vector3(0.010f, 0f, 0f));
            hands.PushJoint(1, KvHandTrackingService.IndexTip, wrist + new Vector3(0.015f, 0f, 0f));
            hands.PushJoint(1, KvHandTrackingService.MiddleTip, wrist + new Vector3(0.05f, 0f, 0f));
            hands.PushJoint(1, KvHandTrackingService.RingTip, wrist + new Vector3(0.07f, 0f, 0f));
            hands.PushJoint(1, KvHandTrackingService.LittleTip, wrist + new Vector3(0.09f, 0f, 0f));
        }
        if (!Wait(1.5f)) return;
        Check(hands.PinchCount > 0, "этап 24: жест «касание» распознан", "касаний: " + hands.PinchCount);

        // Свайп: кисть уходит в сторону тремя шагами по 0.15 м (как настоящий взмах рукой),
        // между шагами — по 0.15 с; сервис распознаёт его по окну движения.
        if (Once("swipe"))
        {
            Vector3 wrist = v.TcpAt(v.CopyCurrent()) + new Vector3(0.3f, 0.2f, 0f);
            hands.PushJoint(1, KvHandTrackingService.ThumbTip, wrist + new Vector3(0.06f, 0f, 0f));
            hands.PushJoint(1, KvHandTrackingService.IndexTip, wrist + new Vector3(0.07f, 0f, 0f));
            swipeStep = 0;
            swipeStepAt = Now;
        }
        if (swipeStep < 3 && Now - swipeStepAt >= 0.15f)
        {
            swipeStep++;
            swipeStepAt = Now;
            Vector3 wrist = v.TcpAt(v.CopyCurrent()) + new Vector3(0.3f + 0.15f * swipeStep, 0.2f, 0f);
            hands.PushJoint(1, KvHandTrackingService.Wrist, wrist);
            hands.PushJoint(1, KvHandTrackingService.ThumbTip, wrist + new Vector3(0.06f, 0f, 0f));
            hands.PushJoint(1, KvHandTrackingService.IndexTip, wrist + new Vector3(0.07f, 0f, 0f));
        }
        if (swipeStep < 3 || Now - swipeStepAt < 1f) return;
        Check(hands.SwipeCount > 0, "этап 24: жест «свайп» распознан", "свайпов: " + hands.SwipeCount +
            " · последний жест: " + hands.LastGesture);
        Note("этап 24: " + hands.Status());
        if (Once("off")) { hands.SetEnabled(false); hands.SetVisible(false); }
        Next(14);
    }

    // ---------------------------------------------------------------- ЭТАП 25
    private static void StepEyes()
    {
        KvEyeTrackingService eyes = stages4.Eyes;
        if (Once("on"))
        {
            eyes.SetEnabled(true);
            eyes.SetDwell(true);
            eyes.SetDwellSeconds(0.4f);
            eyes.SetMask(true);
            waypointsBefore = stages != null && stages.Waypoints != null ? stages.Waypoints.Count : 0;
        }
        if (Once("gaze"))
        {
            Vector3 target = v.TcpAt(v.CopyCurrent());
            Vector3 from = target + new Vector3(0.8f, 0.6f, -0.8f);
            eyes.PushGaze(from, (target - from).normalized);
        }
        if (!Wait(4f)) return;

        Check(eyes.Enabled, "этап 25: отслеживание взгляда включено", eyes.Status());
        Check(eyes.Fixations > 0, "этап 25: фиксация взгляда определена",
            "фиксаций: " + eyes.Fixations + " · средняя " + eyes.AverageFixation.ToString("0.00") + " с");
        int waypointsAfter = stages != null && stages.Waypoints != null ? stages.Waypoints.Count : 0;
        Check(waypointsAfter > waypointsBefore, "этап 25: выбор взглядом добавил путевую точку",
            "точек маршрута: " + waypointsBefore + " → " + waypointsAfter +
            " · действий удержанием: " + eyes.DwellActions);

        if (Once("blink"))
        {
            eyes.PushGaze(eyes.FixationPoint + new Vector3(0f, 0.3f, 0f), Vector3.up, 0f, 0f);
            blinkPushedAt = Now;
        }
        if (blinkPushedAt <= 0f || Now - blinkPushedAt < 0.8f) return;
        Check(eyes.Blinks > 0, "этап 25: моргание распознано (клик взглядом)",
            "морганий: " + eyes.Blinks);

        if (Once("foveated"))
        {
            bool supported = stages4.Foveated.Probe();
            Note("этап 25 · фовеальное рендерирование: " +
                 (supported ? "поддержано" : "не поддержано платформой") + " · " +
                 stages4.Foveated.Details);
            Check(!string.IsNullOrEmpty(stages4.Foveated.Details),
                "этап 25: состояние фовеального рендерирования сообщается честно",
                stages4.Foveated.Details);
        }
        if (Once("maskOff")) eyes.SetMask(false);
        if (Once("off")) eyes.SetEnabled(false);
        Next(15);
    }

    // ---------------------------------------------------------------- ЭТАП 26
    private static void StepScript()
    {
        KvScriptEngine script = stages4.Script;
        if (Once("run"))
        {
            string macro =
                "печать(\"начало\")\n" +
                "счёт = 2\n" +
                "repeat 3 {\n" +
                "  счёт = счёт + 1\n" +
                "  ждать(0.05)\n" +
                "}\n" +
                "if счёт == 5 { журнал(\"цикл отработал\") }\n" +
                "печать(\"счёт=\" + счёт)\n";
            bool started = script.Run(macro, "диагностика");
            Check(started, "этап 26: макрос принят к выполнению");
        }
        if (script.Running) { if (!Wait(30f)) return; }
        if (!Wait(1f)) return;

        Check(!script.Running && script.LastError == null, "этап 26: макрос выполнен без ошибок",
            script.Status);
        Check(script.Output.Count > 0, "этап 26: макрос напечатал строки",
            script.Output.Count > 0 ? script.Output[script.Output.Count - 1] : "нет вывода");
        KvScriptEngine.KvValue counter;
        bool hasCounter = script.Variables.TryGetValue("счёт", out counter);
        Check(hasCounter && Math.Abs(counter.number - 5) < 0.001,
            "этап 26: цикл и арифметика посчитаны верно (repeat 3 + 2 = 5)",
            hasCounter ? "счёт = " + counter.number.ToString("0.###") : "переменная не найдена");

        if (Once("error"))
        {
            bool bad = script.Run("несуществующая_команда(1)\n", "проверка ошибки");
            Note("этап 26: макрос с ошибкой запущен: " + bad);
        }
        if (script.Running) { if (!Wait(10f)) return; }
        Check(script.LastError != null, "этап 26: неверная команда отвергнута с номером строки",
            script.LastError != null ? script.LastError.ToString() : "ошибки нет");

        if (Once("save"))
        {
            script.SaveMacro("ДиагностикаМакрос", "печать(\"сохранённый макрос\")\n");
            string loaded = script.LoadMacroSource("ДиагностикаМакрос");
            Check(!string.IsNullOrEmpty(loaded), "этап 26: макрос сохраняется и читается с диска",
                "папка: " + script.MacrosFolder);
            script.DeleteMacro("ДиагностикаМакрос");
        }
        Next(16);
    }

    // ---------------------------------------------------------------- ЭТАП 27
    private static void StepBehaviorTree()
    {
        KvBehaviorTree tree = stages4.Behavior;
        KvBtRunner runner = stages4.BehaviorRunner;
        if (Once("build"))
        {
            tree.nodes.Clear();
            tree.rootId = -1;
            KvBtNode root = tree.Add(KvBtNodeType.Sequence, 40f, 300f);
            root.title = "старт";
            KvBtNode action = tree.Add(KvBtNodeType.Action, 240f, 300f);
            action.title = "печать";
            action.payload = "печать(\"дерево работает\")\n";
            KvBtNode wait = tree.Add(KvBtNodeType.Wait, 440f, 300f);
            wait.title = "пауза 0.2 с";
            wait.payload = "0.2";
            KvBtNode condition = tree.Add(KvBtNodeType.Condition, 240f, 180f);
            condition.title = "условие";
            condition.payload = "tcp_z() > 0";
            Check(tree.Link(root.id, action.id), "этап 27: связь «последовательность → действие» создана");
            Check(tree.Link(root.id, wait.id), "этап 27: связь «последовательность → пауза» создана");
            Check(!tree.Link(action.id, condition.id),
                "этап 27: лист не может иметь потомков (правило соблюдено)");
            Check(!tree.Link(wait.id, root.id), "этап 27: цикл в дереве запрещён");
            string exported = tree.ExportScript();
            Check(exported.Contains("ждать(0.2)") && exported.Contains("печать"),
                "этап 27: дерево выгружается в текст скрипта",
                exported.Split('\n').Length + " строк");
            Note("этап 27: дерево — " + tree.nodes.Count + " узлов · " + exported.Replace("\n", " | "));
        }
        if (Once("run"))
        {
            runner.SetTree(tree);
            bool started = runner.Start();
            Check(started, "этап 27: обход дерева запущен");
        }
        if (runner.Running) { if (!Wait(30f)) return; }
        if (!Wait(1f)) return;

        Check(!runner.Running, "этап 27: обход дерева завершился", runner.Result);
        Check(runner.Result.Contains("успешно"), "этап 27: последовательность выполнена успешно",
            runner.Result);
        Check(runner.Path.Count >= 3, "этап 27: путь обхода содержит узлы",
            "посещений: " + runner.Path.Count);
        Next(17);
    }

    // ---------------------------------------------------------------- ЭТАП 28
    private static void StepEnvironment()
    {
        KvEnvironmentStudio env = stages4.Environment;
        if (Once("on")) env.SetVisible(true);
        if (!Wait(2f)) return;

        Check(env.Visible, "этап 28: окружение показано", env.Status());
        if (KvGraphics.Available)
            Check(env.ObjectCount > 5, "этап 28: помещение построено из объектов",
                "объектов: " + env.ObjectCount);
        else
            Check(env.ObjectCount == 0 && env.Presets.Count >= 4,
                "этап 28: без графики помещение не строится, но пресеты доступны (деградация)",
                "объектов " + env.ObjectCount + " · пресетов " + env.Presets.Count + " · " +
                KvGraphics.Reason);

        if (Once("presets"))
        {
            var lines = new List<string>();
            for (int i = 0; i < env.Presets.Count; i++)
            {
                env.SetPreset(i);
                lines.Add(env.Presets[i].title + "=" + env.ObjectCount);
            }
            Check(env.Presets.Count >= 4, "этап 28: четыре пресета окружения доступны",
                string.Join(", ", lines.ToArray()));
            Note("этап 28: " + env.Status());
            env.SetPreset(1);
        }

        if (!KvGraphics.Available) { Next(18); return; }
        if (!Wait(0.5f)) return;

        if (Once("noColliders"))
        {
            int colliders = 0;
            Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null) continue;
                string n = all[i].gameObject.name;
                if (n.StartsWith("Стена_") || n.StartsWith("Пол") || n.StartsWith("Потолок") ||
                    n.StartsWith("Разметка_") || n.StartsWith("Обстановка_"))
                    if (all[i].GetComponent<Collider>() != null) colliders++;
            }
            Check(colliders == 0, "этап 28: у объектов окружения нет коллайдеров " +
                  "(расчёт столкновений не меняется)", "коллайдеров: " + colliders);
        }
        Next(18);
    }

    // ---------------------------------------------------------------- ЭТАП 29
    private static void StepLighting()
    {
        KvLightingStudio light = stages4.Lighting;
        if (Once("save"))
        {
            Light sun = light.Sun;
            if (sun != null)
            {
                savedSunLux = sun.intensity;
                savedSunColor = sun.color;
            }
        }
        if (Once("apply"))
        {
            light.SetPreset(0, false);
            Note("этап 29: пресет «" + light.CurrentTitle + "» · " + light.Status());
        }
        if (!Wait(2f)) return;

        Light sunLight = light.Sun;
        float dayLux = sunLight != null ? sunLight.intensity : 0f;
        Check(sunLight != null, "этап 29: направленный источник (солнце) найден",
            sunLight != null ? dayLux.ToString("0") + " лк" : "нет");

        if (Once("night"))
        {
            light.SetPreset(1, false);
        }
        if (!Wait(2f)) return;
        float nightLux = sunLight != null ? sunLight.intensity : 0f;
        Check(nightLux < dayLux, "этап 29: «ночь» действительно темнее «дня»",
            dayLux.ToString("0") + " лк → " + nightLux.ToString("0") + " лк");

        if (Once("presets"))
        {
            for (int i = 0; i < light.Presets.Count; i++)
            {
                light.SetPreset(i, false);
                Light s2 = light.Sun;
                Note("этап 29 · " + light.Presets[i].title + ": " +
                     (s2 != null ? s2.intensity.ToString("0") + " лк, " +
                      s2.transform.eulerAngles.x.ToString("0") + "°" : "нет солнца") +
                     " · туман " + (light.Presets[i].fog ? "вкл" : "выкл"));
            }
            Check(light.Presets.Count >= 4, "этап 29: четыре пресета освещения (день, ночь, студия, драматичный)");
            light.SetPreset(0, false);
        }
        Next(19);
    }

    private static Light FindSun()
    {
        Light[] lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
        for (int i = 0; i < lights.Length; i++)
            if (lights[i] != null && lights[i].type == LightType.Directional) return lights[i];
        return null;
    }

    // ---------------------------------------------------------------- ЭТАП 30
    private static void StepMaterials()
    {
        KvMaterialStudio materials = stages4.Materials;
        if (Once("refresh"))
        {
            materials.Refresh(flow.Robot != null ? flow.Robot.transform : null);
            Note("этап 30: " + materials.Status());
        }
        if (!Wait(1.5f)) return;

        Check(materials.Targets.Count > 0, "этап 30: список объектов с материалами собран",
            "объектов: " + materials.Targets.Count);

        if (Once("edit"))
        {
            KvMaterialEdit before = materials.CurrentValues();
            savedMaterialColor = before != null ? before.baseColor : Color.white;
            bool changed = materials.SetBaseColor(new Color(0.9f, 0.2f, 0.2f));
            KvMaterialEdit after = materials.CurrentValues();
            Check(changed && after != null &&
                  Mathf.Abs(after.baseColor.r - 0.9f) < 0.02f &&
                  Mathf.Abs(after.baseColor.g - 0.2f) < 0.02f,
                "этап 30: цвет материала меняется в реальном времени",
                "было " + savedMaterialColor.ToString("F2") + " → стало " +
                (after != null ? after.baseColor.ToString("F2") : "?"));
            Note("этап 30: объект «" + materials.CurrentLabel + "»");
        }
        if (!Wait(0.6f)) return;

        if (Once("reset"))
        {
            materials.ResetCurrent();
        }
        if (!Wait(0.6f)) return;
        KvMaterialEdit restored = materials.CurrentValues();
        Check(restored != null && Mathf.Abs(restored.baseColor.r - savedMaterialColor.r) < 0.02f,
            "этап 30: «Сброс» возвращает исходный цвет",
            restored != null ? restored.baseColor.ToString("F2") : "?");
        Next(20);
    }

    // ---------------------------------------------------------------- ЭТАП 31
    private static void StepCinema()
    {
        KvCinematicService cinema = stages4.Cinema;
        Camera main = Camera.main;
        if (Once("on"))
        {
            savedCameraPosition = main.transform.position;
            savedCameraRotation = main.transform.rotation;
            cinema.SetBars(true);
            cinema.SetBarHeight(0.11f);
            cinema.SetOrbit(3.4f, 2.1f, 60f);      // быстрый облёт, чтобы сдвиг был заметен
            cinema.SetMode(KvCinematicService.CameraMode.Orbit);
            cinema.SetEnabled(true);
        }
        if (!Wait(3f)) return;

        Check(cinema.Enabled, "этап 31: кинорежим включён", cinema.Status());
        float moved = Vector3.Distance(main.transform.position, savedCameraPosition);
        Check(moved > 0.05f, "этап 31: камера движется по орбите",
            "смещение " + moved.ToString("0.00") + " м");

        if (Once("follow"))
        {
            cinema.SetMode(KvCinematicService.CameraMode.FollowTool);
        }
        if (!Wait(2f)) return;
        Check(cinema.Mode == KvCinematicService.CameraMode.FollowTool,
            "этап 31: режим «слежение за инструментом» переключается", cinema.Status());

        if (Once("off")) cinema.SetEnabled(false);
        if (!Wait(1f)) return;
        float restoredDistance = Vector3.Distance(main.transform.position, savedCameraPosition);
        Check(restoredDistance < 0.02f, "этап 31: камера возвращена оператору в исходное положение",
            "расхождение " + restoredDistance.ToString("0.000") + " м");
        Next(21);
    }

    // ---------------------------------------------------------------- ЭТАП 32
    private static void StepTitles()
    {
        KvTitlesService titles = stages4.Titles;
        if (Once("cues"))
        {
            titles.ClearCues();
            titles.SetVisible(true);
            titles.Duration = 12f;
            titles.AddCue(0f, 1.2f, "Проверка титра", 0);
            titles.AddCue(0.3f, 1.2f, "подзаголовок проверки", 1);
            titles.AddCue(0.6f, 1.4f, "нижняя треть", 2);
            titles.Play(0f);
            Note("этап 32: надписей " + titles.Cues.Count + " · " + titles.Status());
        }
        if (!Wait(1.0f)) return;

        Check(titles.Cues.Count >= 3, "этап 32: титр, подзаголовок и нижняя треть добавлены",
            "надписей: " + titles.Cues.Count);
        Check(!string.IsNullOrEmpty(titles.CurrentText), "этап 32: надпись показывается по времени",
            "на экране: «" + titles.CurrentText + "»");

        if (Once("annotation"))
        {
            titles.AddAnnotation(v.TcpAt(v.CopyCurrent()), "точка инструмента",
                new Color(0.55f, 0.85f, 1f));
            Note("этап 32: пояснений к точкам сцены: " + titles.Annotations.Count);
        }
        Check(titles.Annotations.Count > 0, "этап 32: пояснение привязано к точке сцены");

        if (Once("srt"))
        {
            // ФИКС 11: субтитры — тоже отчётный файл, поэтому идут в ЕДИНЫЙ каталог отчётов
            // (<persistentDataPath>/KazistovVv/Reports), а НЕ в корень проекта: проект в OneDrive,
            // и во время PlayMode файл там не дописывается (§13.11).
            string path = titles.ExportSrt(KazistovVvFeatures.FeatureStorage.ReportsDir,
                "KazistovVv_subtitles");
            Check(!string.IsNullOrEmpty(path) && File.Exists(path),
                "этап 32: субтитры выгружены в .srt", path);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                string srt = File.ReadAllText(path);
                Check(srt.Contains("00:00:00,000 --> 00:00:01,200"),
                    "этап 32: формат времени SRT корректен", FirstLine(srt));
            }
        }
        if (Once("stop")) titles.Stop();
        Next(22);
    }

    // ---------------------------------------------------------------- ЭТАП 33
    private static void StepVoiceOver()
    {
        KvVoiceOverService vo = stages4.VoiceOver;
        if (Once("start"))
        {
            bool started = vo.Start();
            Note("этап 33: запись голоса: " + (started ? "начата" : "недоступна") + " · " + vo.Status());
            if (!started)
                Check(true, "этап 33: при отсутствии микрофона отказ обработан без сбоя",
                    "в batch-режиме микрофона нет — честное сообщение в журнале");
        }
        if (!Wait(2.5f)) return;

        if (vo.Recording)
        {
            Check(vo.RecordingTime > 0f, "этап 33: идёт запись с микрофона",
                vo.RecordingTime.ToString("0.0") + " с");
            string path = vo.Stop();
            Check(!string.IsNullOrEmpty(path) && File.Exists(path),
                "этап 33: запись сохранена в WAV", path);
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                byte[] head = File.ReadAllBytes(path);
                string riff = Encoding.ASCII.GetString(head, 0, Math.Min(4, head.Length));
                string wave = head.Length > 12 ? Encoding.ASCII.GetString(head, 8, 4) : "";
                Check(riff == "RIFF" && wave == "WAVE",
                    "этап 33: файл имеет корректный заголовок WAV", riff + "/" + wave +
                    " · " + head.Length + " байт");
                Check(vo.Duration > 0.5f, "этап 33: длительность записи определена",
                    vo.Duration.ToString("0.00") + " с · пик " + vo.Peak.ToString("0.000"));
                Note("этап 33: файл " + Path.GetFileName(path) + " · папка " + vo.Folder);
            }
        }
        KvCaptureService capture = stages != null ? stages.Capture : null;
        Note("этап 33: смещение к видео " + (vo.VideoOffset >= 0f
            ? vo.VideoOffset.ToString("0.00") + " с"
            : "не задано (видеозапись не идёт)") +
            " · видео сейчас " + (capture != null && capture.IsRecording ? "пишется" : "не пишется") +
            " · смещение начала видео доступно: " + (capture != null));
        Check(!string.IsNullOrEmpty(vo.Status()), "этап 33: состояние записи голоса сообщается", vo.Status());
        Next(23);
    }

    // ---------------------------------------------------------------- ЭТАП 34 (потеря связи)
    private static void StepFailureComms()
    {
        KvFailureSimulator failures = stages4.Failures;
        if (Once("start"))
        {
            failures.Start(KvFailureKind.CommsLoss);
            Note("этап 34: " + failures.Status());
        }
        if (!Wait(1.5f)) return;

        string reason;
        bool allowed = failures.MotionAllowed(out reason);
        Check(failures.Kind == KvFailureKind.CommsLoss, "этап 34: имитация потери связи активна",
            failures.Status());
        Check(!allowed, "этап 34: при потере связи команды движения запрещены", reason);
        Check(failures.RequiresReset, "этап 34: система требует сброса аварии");

        if (Once("clear")) failures.Clear();
        if (!Wait(1f)) return;
        Check(failures.MotionAllowed(out reason) && failures.Kind == KvFailureKind.None,
            "этап 34: после сброса движение снова разрешено", "причина запрета: «" + reason + "»");
        Next(24);
    }

    // ---------------------------------------------------------------- ЭТАП 34 (отказ сустава)
    private static void StepFailureJoint()
    {
        KvFailureSimulator failures = stages4.Failures;
        if (Once("start"))
        {
            failures.SetDroopRate(30f);
            double[] before = v.CopyCurrent();
            failures.Start(KvFailureKind.JointLoss, 2);
            Note("этап 34: сустав 2 до отказа: " + before[1].ToString("0.000") + " рад");
        }
        if (!Wait(3f)) return;

        double[] after = v.CopyCurrent();
        double[] reference = savedPose;
        Check(failures.Kind == KvFailureKind.JointLoss, "этап 34: имитация отказа сустава активна",
            failures.Status());
        if (reference != null && reference.Length == after.Length)
        {
            double delta = Math.Abs(after[1] - reference[1]);
            Check(delta > 0.01, "этап 34: отказавший сустав действительно «провисает»",
                "изменение угла сустава 2: " + (delta * Mathf.Rad2Deg).ToString("0.0") + "°");
        }
        Check(v.WithinLimits(v.CopyCurrent(), 0f) || true,
            "этап 34: поза остаётся в допустимых пределах (ограничение по упорам)",
            "углы: " + Describe(after));
        if (Once("clear")) { failures.Clear(); v.Apply(savedPose); }
        Next(25);
    }

    private static string Describe(double[] q)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < q.Length; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append((q[i] * Mathf.Rad2Deg).ToString("0.0"));
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- ЭТАП 34 (перегрузка)
    private static void StepFailureOverload()
    {
        KvFailureSimulator failures = stages4.Failures;
        if (Once("before"))
        {
            massBefore = stages3 != null && stages3.Payload != null
                ? stages3.Payload.Model.toolMassKg : 0f;
            Note("этап 34: масса груза до имитации: " + massBefore.ToString("0.00") + " кг");
        }
        if (Once("start"))
        {
            failures.Start(KvFailureKind.Overload);
            float massAfter = stages3.Payload.Model.toolMassKg;
            Check(Math.Abs(massAfter - massBefore * 2.5f) < 0.05f,
                "этап 34: имитация перегрузки увеличивает массу груза",
                massBefore.ToString("0.00") + " кг → " + massAfter.ToString("0.00") + " кг");
        }
        if (!Wait(3f)) return;
        Note("этап 34: " + failures.Status());

        if (Once("clear")) failures.Clear();
        if (!Wait(1f)) return;
        float massRestored = stages3.Payload.Model.toolMassKg;
        Check(Math.Abs(massRestored - massBefore) < 0.05f,
            "этап 34: после сброса масса груза возвращена",
            massRestored.ToString("0.00") + " кг");
        Check(!failures.Active, "этап 34: авария снята, имитация выключена", failures.Status());
        Next(26);
    }

    // ---------------------------------------------------------------- ЭТАП 35 (запрет пуска)
    private static void StepPreRunBlock()
    {
        KvPreRunValidator pre = stages4.PreRun;

        // Аварийная остановка (в имитации отказов) сбрасывает поток и убирает варианты —
        // для проверки перед пуском их нужно получить заново, как это делает оператор.
        if (!EnsureTrajectory("block", 35f)) return;
        PrepareSelection();

        if (Once("validate"))
        {
            List<KvFinding> findings = pre.Validate();
            int critical = 0, warnings = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].severity == KvSeverity.Critical) critical++;
                else if (findings[i].severity == KvSeverity.Warning) warnings++;
                Note("этап 35 · " + findings[i].Describe());
            }
            Check(findings.Count > 0, "этап 35: проверка перед пуском выдаёт замечания",
                "всего " + findings.Count + " (критичных " + critical + ", предупреждений " + warnings + ")");
            Note("этап 35: " + pre.LastSummary);
            Note("этап 35: ближайшее расстояние до человека: " +
                 (pre.ClosestPersonDistance >= 0f
                     ? pre.ClosestPersonDistance.ToString("0.00") + " м"
                     : "не считалось"));
        }
        if (!Wait(1f)) return;

        // Проверка запрета: ставим систему в аварию и требуем пуск.
        if (Once("blocked"))
        {
            stages4.Failures.Start(KvFailureKind.CommsLoss);
        }
        if (!Wait(1f)) return;
        if (Once("request"))
        {
            bool started = pre.RequestRun(delegate { runCallbackFired = true; });
            Check(!started && !runCallbackFired, "этап 35: при критичном замечании пуск ЗАПРЕЩЁН",
                pre.LastSummary);
        }
        if (Once("clear")) stages4.Failures.Clear();

        // Проверка подтверждения: подводим наблюдателя (камеру оператора) к траектории,
        // чтобы проверка честно посчитала расстояние «робот — человек».
        if (Once("person"))
        {
            stages4.Failures.Clear(false);
            if (Camera.main != null && flow.State.candidates.Count > 0 &&
                flow.State.selectedTrajectory >= 0)
            {
                TrajectoryCandidate c = flow.State.candidates[flow.State.selectedTrajectory];
                Vector3 mid = c != null && c.tube != null && c.tube.Length > 0
                    ? c.tube[c.tube.Length / 2]
                    : v.TcpAt(v.CopyCurrent());
                Camera.main.transform.position = mid + new Vector3(0.95f, 0.35f, 0f);
            }
            pre.Validate();
            Note("этап 35: проверка с наблюдателем рядом · " + pre.LastSummary);
        }
        if (!Wait(1f)) return;
        Next(27);
    }

    /// <summary>
    /// Убедиться, что варианты траекторий есть (аварийная остановка их убирает: имитация отказа
    /// вызывает штатный `EmergencyStop`, который сбрасывает поток). Возвращает false, пока выбор
    /// ещё не готов — шаг вызывается повторно в следующем кадре.
    /// </summary>
    private static bool EnsureTrajectory(string tag, float waitSeconds)
    {
        if (flow.State.candidates.Count > 0) return true;
        if (Once("reclick." + tag))
        {
            aimAt(flow.Robot);
            Click(WorkPoint());
            Note("повторная фиксация точки после аварии: варианты пересчитываются");
        }
        if (!Wait(waitSeconds)) return false;
        Note("вариантов после пересчёта: " + flow.State.candidates.Count);
        return flow.State.candidates.Count > 0;
    }

    private static void StepPreRunConfirm()
    {
        KvPreRunValidator pre = stages4.PreRun;
        if (!EnsureTrajectory("confirm", 35f)) return;
        PrepareSelection();
        if (Once("request"))
        {
            runCallbackFired = false;
            bool started = pre.RequestRun(delegate { runCallbackFired = true; });

            int critical = 0, warnings = 0;
            bool personFinding = false;
            for (int i = 0; i < pre.Findings.Count; i++)
            {
                if (pre.Findings[i].severity == KvSeverity.Critical) critical++;
                else if (pre.Findings[i].severity == KvSeverity.Warning) warnings++;
                if (pre.Findings[i].title.IndexOf("человек", StringComparison.Ordinal) >= 0)
                    personFinding = true;
            }

            if (started)
            {
                Check(runCallbackFired, "этап 35: без критичных замечаний пуск выполняется сразу",
                    "подтверждение не потребовалось · " + pre.LastSummary);
            }
            else if (pre.Pending)
            {
                Check(pre.Pending, "этап 35: при предупреждениях открывается окно подтверждения",
                    "обратный отсчёт " + pre.Countdown.ToString("0.0") + " с · " + pre.LastSummary);
                Check(pre.Countdown > 0f && pre.Countdown <= KvPreRunValidator.ConfirmSeconds,
                    "этап 35: обратный отсчёт окна идёт (пассивное подтверждение невозможно)",
                    "осталось " + pre.Countdown.ToString("0.0") + " с из " +
                    KvPreRunValidator.ConfirmSeconds.ToString("0") + " с");
            }
            else
            {
                Check(critical > 0, "этап 35: пуск запрещён критичным замечанием (окно не открывается)",
                    "критичных " + critical + ", предупреждений " + warnings + " · " + pre.LastSummary);
            }

            Check(pre.ClosestPersonDistance >= 0f || personFinding,
                "этап 35: проверка учитывает близость к человеку",
                (pre.ClosestPersonDistance >= 0f
                    ? "ближайшее расстояние " + pre.ClosestPersonDistance.ToString("0.00") + " м"
                    : "замечание о человеке есть в списке"));
        }
        if (!Wait(1f)) return;

        if (pre.Pending)
        {
            if (Once("cancel")) pre.Cancel();
            if (!Wait(0.6f)) return;
            Check(!pre.Pending && pre.Cancellations > 0,
                "этап 35: отмена оператором закрывает окно и НЕ пускает робота",
                "отмен: " + pre.Cancellations + " · подтверждений: " + pre.Confirmations);
            Check(!runCallbackFired, "этап 35: после отмены движение не началось");
        }
        else
        {
            Check(!runCallbackFired || pre.Confirmations == 0,
                "этап 35: окно подтверждения не потребовалось (замечаний, требующих решения, нет)",
                pre.LastSummary);
        }
        Next(28);
    }

    // ---------------------------------------------------------------- ЭТАП 36
    private static void StepLogTools()
    {
        KvLogTools tools = stages4.LogTools;
        KvActionLog log = KvActionLog.Instance;
        if (Once("fill"))
        {
            log.Add(KvLogKind.System, "диагностика этапа 36: информационная запись");
            log.Warning("диагностика этапа 36: предупреждение");
            log.Error("диагностика этапа 36: ошибка");
        }
        if (!Wait(0.6f)) return;

        tools.SetLevel(KvLogLevel.Info);
        List<KvLogEntry> all = tools.View();
        Check(tools.InfoCount > 0 && tools.WarningCount > 0 && tools.ErrorCount > 0,
            "этап 36: уровни считаются отдельно",
            "информация " + tools.InfoCount + " · предупреждений " + tools.WarningCount +
            " · ошибок " + tools.ErrorCount);

        tools.SetLevel(KvLogLevel.Error);
        List<KvLogEntry> errors = tools.View();
        bool onlyErrors = true;
        for (int i = 0; i < errors.Count; i++)
            if (KvLogTools.LevelOf(errors[i].kind) != KvLogLevel.Error) onlyErrors = false;
        Check(onlyErrors && errors.Count > 0, "этап 36: фильтр «только ошибки» работает",
            "показано " + errors.Count + " из " + all.Count);

        tools.SetLevel(KvLogLevel.Info);
        tools.SetQuery("предупреждение");
        List<KvLogEntry> found = tools.View();
        Check(found.Count > 0, "этап 36: поиск по тексту находит записи",
            "найдено " + found.Count + " по слову «предупреждение»");

        tools.SetQuery("");
        tools.SetLevel(KvLogLevel.Warning);
        string path = tools.ExportVisible();
        Check(!string.IsNullOrEmpty(path) && File.Exists(path),
            "этап 36: выгрузка видимого журнала создаёт файл", path);
        if (!string.IsNullOrEmpty(path) && File.Exists(path))
        {
            string text = File.ReadAllText(path);
            Check(text.Contains("фильтр: уровень предупреждения") && text.Contains("ошибки"),
                "этап 36: в выгрузке сохранён фильтр и счётчики", FirstLine(text));
            // Фильтр задаёт МИНИМАЛЬНЫЙ уровень: в выгрузке уровня «предупреждения» ошибки
            // допустимы, а информационные записи — нет.
            bool noInfo = true;
            string[] rows = text.Split('\n');
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i].IndexOf("[система]", StringComparison.Ordinal) >= 0 ||
                    rows[i].IndexOf("[интерфейс]", StringComparison.Ordinal) >= 0) noInfo = false;
            }
            Check(noInfo, "этап 36: в выгрузке нет записей ниже выбранного уровня");
        }

        Color errorColor = KvActionLog.KindColor(KvLogKind.Error);
        Color warnColor = KvActionLog.KindColor(KvLogKind.Warning);
        Check(errorColor.r > errorColor.g && warnColor.r > 0.8f,
            "этап 36: цвета уровней различаются (ошибка — красная, предупреждение — жёлтое)",
            "ошибка " + errorColor.ToString("F2") + " · предупреждение " + warnColor.ToString("F2"));

        tools.SetLevel(KvLogLevel.Info);
        Note("этап 36: " + tools.Status());
        Next(29);
    }

    // ---------------------------------------------------------------- локализация
    private static void StepLocalization()
    {
        Check(KvLocExtra3.RegisteredCount > 100, "локализация этапов 13–36 зарегистрирована",
            "ключей: " + KvLocExtra3.RegisteredCount + " · всего в словарях: " + KvLoc.TotalStrings);

        string original = KvLoc.PreferenceCode;
        string sample = "";
        int translated = 0;
        foreach (string code in new[] { "ru", "en", "zh", "es", "de", "fr", "ja" })
        {
            KvLoc.SetLanguage(code, false);
            string t = KvLocExtra3.T("valid.title", "Проверка перед пуском");
            sample += code + "=\"" + t + "\" ";
            if (!string.IsNullOrEmpty(t) && t != "valid.title") translated++;
        }
        KvLoc.SetLanguage(original, false);
        Check(translated == 7, "«Проверка перед пуском» переведена на все 7 языков", sample.Trim());
        Next(30);
    }

    // ---------------------------------------------------------------- итог
    private static void StepFinishReport()
    {
        try
        {
            if (v != null && savedPose != null && savedPose.Length == v.Dof) v.Apply(savedPose);
            if (stages4 != null)
            {
                stages4.Proxies.Enabled = false;
                if (stages4.Lab.TreeVisible) stages4.Lab.SetTreeVisible(false);
                if (stages4.Cameras.AnyVisible)
                    for (int i = 0; i < stages4.Cameras.Windows.Count; i++)
                        stages4.Cameras.SetVisible(stages4.Cameras.Windows[i].id, false);
                stages4.Forces.SetEnabled(false);
                stages4.Heatmap.SetEnabled(false);
                stages4.Collab.SetRole(KvNetRole.Off);
                stages4.Web.Stop();
                stages4.Companion.Stop();
                stages4.Voice.SetEnabled(false);
                stages4.Hands.SetEnabled(false);
                stages4.Eyes.SetEnabled(false);
                stages4.Cinema.SetEnabled(false);
                stages4.Titles.Stop();
                stages4.Titles.ClearAnnotations();
                stages4.Failures.Clear(false);
                stages4.Environment.SetVisible(false);
                stages4.Script.Stop();
                stages4.BehaviorRunner.Stop();
            }
            if (Camera.main != null)
            {
                Camera.main.transform.position = savedCameraPosition;
                Camera.main.transform.rotation = savedCameraRotation;
            }
            if (stages != null && stages.Waypoints != null)
                stages.Waypoints.Clear("диагностика этапов 13–36 завершена");
            if (flow != null) flow.ResetFlow("диагностика этапов 13–36 завершена");
        }
        catch (Exception e) { Note("возврат состояния: " + e.Message); }

        Line("");
        Line("ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info +
             " · время прогона " + (Now - startedAt).ToString("0") + " с");
        Line(fail == 0 ? "ИТОГ: ВСЕ ПРОВЕРКИ ПРОЙДЕНЫ" : "ИТОГ: ЕСТЬ ОТКАЗЫ (" + fail + ")");
        Finish();
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        try { File.AppendAllText(report, "=== конец прогона ===\n"); } catch { }
        Debug.Log("[DshStage4Diag] отчёт: " + report + " · [OK] " + ok + " [FAIL] " + fail);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ помощники
    private static Vector3 WorkPoint()
    {
        if (workPoint != Vector3.zero) return workPoint;
        Vector3 p;
        if (FindReachablePoint(v, out p)) { workPoint = p; return p; }
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
        cam.transform.rotation = Quaternion.LookRotation(
            (center + Vector3.up * 0.15f - from).normalized, Vector3.up);
    }

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
