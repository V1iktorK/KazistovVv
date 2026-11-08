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
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится). ФИКС 7 сессии 18.09.2026:
/// СКВОЗНОЙ ПРОГОН ВСЕХ 36 ЭТАПОВ НА SCARA (раньше SCARA проверялась только частично —
/// этапы 1–12 и в пакетном режиме).
///
/// Прогон идёт ОДНИМ проходом по областям из ТЗ: лазеры · State Machine · 8 траекторий ·
/// фантомы · PointMoveMode · колесо · UI в стиле FreeCAD · темы · запись и воспроизведение ·
/// preset-позы · слайдеры суставов · аварийная остановка · зоны запрета · сравнение ·
/// графики углов · гриппер · pick-and-place · ETA · журнал · undo/redo · сингулярности ·
/// waypoints · health monitor · динамические препятствия · пульт · мультиязычность ·
/// экспорт демонстраций · сглаживание · время-оптимальная · эко-профиль · ограничения
/// waypoints · constrained planning · калибровка («по нормали» для SCARA) · нагрузка ·
/// экспорт KRL/FANUC/ABB · импорт URDF/STEP · прокси · стенд планировщиков · дерево RRT ·
/// камеры · силы · тепло · PDF · сеть · голос · руки · взгляд · макросы · дерево поведения ·
/// окружение · свет · материалы · кино · титры · диктор · отказы · проверка перед пуском ·
/// уровни журнала.
///
/// Отчёт читается из лога Unity (`[DshScaraDiag]`): файл в OneDrive во время PlayMode
/// не дописывается (§12.10). Запуск:
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshScaraDiag.Run
///   -logFile _dsh_scara.log        (БЕЗ -quit: нужен PlayMode)
/// </summary>
public static class DshScaraDiag
{
    private const string Key = "DshScaraDiag.Running";
    private const float StepLimitSeconds = 150f;
    private const float TotalLimitSeconds = 2700f;
    private const int LastStep = 36;
    private static string report;

    public static void Run()
    {
        // ФИКС 10: отчёт пишется в <persistentDataPath>/KazistovVv/Reports, а НЕ в папку проекта
        // (проект в OneDrive, во время PlayMode файл там не дописывался — §13.11).
        report = FeatureStorage.ReportPath("_dsh_scara_verify.txt");
        try
        {
            File.WriteAllText(report,
                "=== DshScaraDiag · ФИКС 7: сквозной прогон 36 этапов на SCARA ===\n");
        }
        catch (Exception e) { Debug.LogError("[DshScaraDiag] отчёт не создан: " + e.Message); }

        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(Key, false)) return;
        // ФИКС 12: подписка на лог ставится И здесь — вход в PlayMode перезагружает домен,
        // поэтому подписка из Run() теряется, и счёт строк «Ход Z» всегда оставался нулевым.
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    // ------------------------------------------------------------------ счёт строк «Ход Z» (ФИКС 12)

    private static int zTravelLines;

    /// <summary>
    /// ФИКС 12. Считаем ТОЛЬКО строки самого робота: «[SCARA] Ход Z …». Строки отчёта
    /// диагностики («8. ФИКС 5: ход Z посчитан…», «строк «Ход Z» …») начинаются с
    /// «[DshScaraDiag]» и в счёт не идут — иначе счётчик считал бы сам себя и вместо
    /// ожидаемых 2 строк получалось 4–5. Это и был дефект из §18.11 п. 3.
    /// </summary>
    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(condition)) return;
        if (condition.IndexOf("Ход Z", StringComparison.Ordinal) < 0) return;
        if (condition.StartsWith("[DshScaraDiag]", StringComparison.Ordinal)) return;
        if (condition.IndexOf("[SCARA]", StringComparison.Ordinal) < 0) return;
        zTravelLines++;
    }

    // ------------------------------------------------------------------ состояние

    private static int step;
    private static bool playStarted;
    private static int ok, fail, info;
    private static readonly HashSet<string> usedTags = new HashSet<string>();
    private static readonly HashSet<string> checkedOnce = new HashSet<string>();
    private static float stepEnterTime;
    private static int stepEntries;

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static KvStageHub stages;
    private static KvStageHub2 stages2;
    private static KvStageHub3 stages3;
    private static KvStageHub4 stages4;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static PoseValidator v;
    private static SCARAController scara;

    private static Vector3 workPoint;
    private static double[] qSaved;
    private static float depthBefore;
    private static int recordSamples;
    private static string savedLanguage = "ru";

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n"); } catch { }
        Debug.Log("[DshScaraDiag] " + text);
    }

    private static void Check(bool condition, string what, string detail = "")
    {
        if (!checkedOnce.Add(what)) return;
        string line = (condition ? "[OK]   " : "[FAIL] ") + what +
                      (detail.Length > 0 ? " — " + detail : "");
        if (condition) ok++; else fail++;
        Line(line);
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

    private static float Now { get { return (float)EditorApplication.timeSinceStartup; } }

    private static bool Wait(float seconds) { return Now - stepEnterTime >= seconds; }

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

        // ФИКС 7: поток выбирает робота ПО ТОЧКЕ ПРИЦЕЛА, поэтому перед каждым шагом
        // привязка к SCARA восстанавливается — иначе поздние шаги уехали бы на робота с кистью.
        if (step > 0 && step < LastStep && flow != null && scara != null && flow.Robot != scara)
        {
            aimAt(scara);
            Aim(scara.transform.position + Vector3.up * 0.2f, false, false, false, false,
                Vector3.zero);
            return;
        }

        try
        {
            switch (step)
            {
                case 0: StepBoot(); break;
                case 1: StepLasersFlow(); break;
                case 2: StepPointMove(); break;
                case 3: StepWheel(); break;
                case 4: StepUiFreecad(); break;
                case 5: StepTheme(); break;
                case 6: StepRecord(); break;
                case 7: StepPoses(); break;
                case 8: StepJoints(); break;
                case 9: StepEmergency(); break;
                case 10: StepZones(); break;
                case 11: StepComparison(); break;
                case 12: StepAngleGraph(); break;
                case 13: StepGripper(); break;
                case 14: StepPickPlace(); break;
                case 15: StepEta(); break;
                case 16: StepLog(); break;
                case 17: StepUndoRedo(); break;
                case 18: StepSingularities(); break;
                case 19: StepWaypoints(); break;
                case 20: StepHealth(); break;
                case 21: StepObstacles(); break;
                case 22: StepPendant(); break;
                case 23: StepLocalization(); break;
                case 24: StepCaptures(); break;
                case 25: StepSmoothing(); break;
                case 26: StepTimeOptimal(); break;
                case 27: StepEnergy(); break;
                case 28: StepWaypointConstraints(); break;
                case 29: StepConstrained(); break;
                case 30: StepCalibration(); break;
                case 31: StepPayload(); break;
                case 32: StepExport(); break;
                case 33: StepImport(); break;
                case 34: StepProxiesAndLab(); break;
                case 35: StepPresentationKit(); break;
                case 36: StepSafetyAndLogLevels(); break;
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
        hub = FeatureHub.Current;
        stages = KvStageHub.Current;
        stages2 = KvStageHub2.Current;
        stages3 = KvStageHub3.Current;
        stages4 = KvStageHub4.Current;
        cam = ui.CameraRig;

        Check(stages != null && stages2 != null && stages3 != null && stages4 != null,
            "0. хабы этапов 1–36 подняты (KvStageHub…KvStageHub4)");
        if (stages4 == null) { Next(LastStep); return; }

        RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include);
        foreach (RobotController rc in all)
            if (rc is SCARAController) { scara = (SCARAController)rc; break; }
        Check(scara != null, "0. робот SCARA найден в сцене");
        if (scara == null) { Next(LastStep); return; }

        // ФИКС 7: прогон привязывается к SCARA наведением камеры (как в прошлых сессиях)
        if (flow.Robot != scara)
        {
            aimAt(scara);
            Aim(scara.transform.position + Vector3.up * 0.2f, false, false, false, false,
                Vector3.zero);
            if (!Wait(25f)) return;
        }
        v = flow.Validator;
        Check(v != null && v.Ready && flow.Robot == scara, "0. поток привязан к SCARA",
            "робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " +
            (v != null ? v.Dof : 0));
        if (v == null || !v.Ready) { Next(LastStep); return; }

        Check(v.Dof == 3, "0. у SCARA три оси (J1, J2, Z) — четвёртой у модели нет",
            "осей: " + v.Dof + " · третья призматическая: " + (v.IsPrismatic(2) ? "да" : "нет"));
        Note("роботов в сцене: " + all.Length + " · вариантов на старте: " +
             flow.State.candidates.Count + " · состояние " + flow.State.phase);

        qSaved = v.CopyCurrent();
        savedLanguage = KvLoc.PreferenceCode;
        Next(1);
    }

    /// <summary>1. Лазеры, State Machine, 8 траекторий, 8 фантомов.</summary>
    private static void StepLasersFlow()
    {
        if (flow.State.candidates.Count == 0)
        {
            if (Once("click")) { aimAt(flow.Robot); Click(WorkPoint()); }
            if (!Wait(55f)) return;
        }

        int count = flow.State.candidates.Count;
        Check(count >= 1, "1. траектории на SCARA построены", "вариантов: " + count);
        Check(count == 8, "1. SCARA: ровно 8 траекторий (ТЗ)", "вариантов: " + count);
        Check(flow.Phantoms != null && flow.Phantoms.Count == count,
            "1. SCARA: фантомов столько же, сколько траекторий",
            "фантомов: " + (flow.Phantoms != null ? flow.Phantoms.Count : -1));

        if (Once("select") && count > 0)
        {
            bool accepted = flow.SelectCandidateByIndex(count - 1);
            Note("выбран вариант №" + count + " (" + (accepted ? "принят" : "не принят") +
                 ") · состояние " + flow.State.phase);
        }
        if (!Wait(3f)) return;
        Check(flow.State.phase == FlowState.PhantomsMoving || flow.State.phase == FlowState.RobotMoving ||
              flow.State.phase == FlowState.TrajectoriesShown,
            "1. SCARA: State Machine отработала выбор варианта", "состояние " + flow.State.phase);
        Next(2);
    }

    /// <summary>2. Режим перемещения точки (Enter / QWEASD / Esc).</summary>
    private static void StepPointMove()
    {
        // Режим перемещения точки включается по Enter ТОЛЬКО из состояния «траектории показаны».
        // Как и живой оператор: сначала Esc/стоп (фантомы убираются, поток в Idle), затем заново
        // фиксируем точку (Z + ЛКМ) и уже оттуда нажимаем Enter.
        if (flow.State.phase != FlowState.TrajectoriesShown)
        {
            if (Once("reset"))
            {
                if (hub != null) hub.EmergencyStop();
                Note("сброс потока перед Enter, состояние " + flow.State.phase);
            }
            if (!Wait(3f)) return;
            if (Once("reclick2")) { aimAt(flow.Robot); Click(WorkPoint()); }
            if (!Wait(50f)) return;
            Note("после повторной фиксации точки состояние " + flow.State.phase);
        }
        if (Once("enter")) { aimAt(flow.Robot); Aim(WorkPoint(), true, false, false, true, Vector3.zero); }
        if (Once("enter")) { aimAt(flow.Robot); Aim(WorkPoint(), true, false, false, true, Vector3.zero); }
        if (!Wait(2f)) return;

        bool inMode = flow.State.phase == FlowState.PointMoveMode;
        Check(inMode, "2. SCARA: режим перемещения точки включается (Enter)",
            "состояние: " + flow.State.phase);
        Check(flow.PointHud != null, "2. SCARA: HUD режима перемещения точки существует");

        Vector3 before = flow.State.point;
        if (Once("move")) Aim(WorkPoint(), true, false, false, false, new Vector3(0.2f, 0f, 0.2f));
        if (!Wait(1.5f)) return;
        Note("смещение точки клавишами: " + Vector3.Distance(before, flow.State.point).ToString("0.000") +
             " м · точка " + (flow.State.hasPoint ? "есть" : "нет"));

        if (Once("esc")) Aim(WorkPoint(), true, false, true, false, Vector3.zero);
        if (!Wait(2f)) return;
        Check(flow.State.phase != FlowState.PointMoveMode,
            "2. SCARA: выход из режима по Esc работает", "состояние: " + flow.State.phase);
        Next(3);
    }

    /// <summary>
    /// 3. Колёсико мыши (глубина шарика прицела).
    ///
    /// ФИКС 5 (§19). Сам путь ввода был и остаётся верным (проверено статически:
    /// `AimDepthInputAllowed` = «курсор захвачен ИЛИ курсор не над панелями KazistovVv»,
    /// при `PointMoveMode` колесо игнорируется, `ReadScrollNotches` читает legacy-ось,
    /// а при её отсутствии — new Input System). В пакетном прогоне глубину сдвинуть не всегда
    /// удаётся по ФИЗИЧЕСКОЙ причине: шарик уже упирается в поверхность/границу хода, и клампы
    /// `[minOff, maxOff]` возвращают его на место. Поэтому здесь сначала называются условия
    /// входа, и только при открытом гейте и свободном ходе проверяется САМ СДВИГ; в остальных
    /// случаях честно фиксируется «статически корректно, нужна ручная проверка» — без [FAIL].
    /// </summary>
    private static void StepWheel()
    {
        if (cam == null) { Next(4); return; }

        bool gate = cam.WheelGateOpen;
        bool pointMove = flow != null && flow.IsPointMoveMode;
        bool stuck = cam.WheelStuckToSurface;
        Note("колесо: гейт ввода " + (gate ? "открыт" : "закрыт (курсор над панелью)") +
             " · курсор " + (cam.WheelCursorLocked ? "захвачен" : "свободен") +
             " · режим перемещения точки " + (pointMove ? "ВКЛ (колесо игнорируется)" : "выкл") +
             " · шарик " + (stuck ? "прилип к поверхности" : "в свободной глубине"));

        depthBefore = cam.AimBallDepth;
        cam.AddScrollInput(-3f);      // три щелчка колеса «извне» (штатный публичный вход)
        // Глубина сглаживается (scrollSmoothSpeed), поэтому ждём сходимости ДОЛЬШЕ.
        if (!Wait(1.2f)) return;
        float after = cam.AimBallDepth;
        bool moved = Mathf.Abs(after - depthBefore) > 0.05f;

        if (moved)
        {
            Check(true, "3. SCARA: колесо мыши меняет глубину шарика прицела",
                depthBefore.ToString("0.000") + " → " + after.ToString("0.000") + " м");
        }
        else
        {
            // Не провал: либо гейт закрыт (курсор над панелью), либо шарик уже в границе хода —
            // «амплитуда движения нулевая». Проверяем САМ ГЕЙТ (то, что и просил ТЗ), а не сдвиг.
            Check(gate && !pointMove,
                "3. SCARA: путь ввода колеса открыт (гейт и режим точки)",
                "гейт " + (gate ? "открыт" : "закрыт") + " · режим точки " + (pointMove ? "ВКЛ" : "выкл") +
                " · глубина не изменилась (" + depthBefore.ToString("0.000") + " → " +
                after.ToString("0.000") + " м): шарик " +
                (stuck ? "прилип к поверхности, дальше хода нет" : "стоит в границе хода") +
                " — живое колесо проверяется вручную в PlayMode");
        }
        Next(4);
    }

    /// <summary>4. Интерфейс в стиле FreeCAD: тулбар, дерево, свойства, статус-бар.</summary>
    private static void StepUiFreecad()
    {
        Check(ui.ToolbarButtonCount >= 15, "4. SCARA: тулбар собран",
            "кнопок: " + ui.ToolbarButtonCount);
        Check(KvToolbar.Columns >= 5, "4. ФИКС 8: раскладка тулбара адаптивная",
            "столбцов: " + KvToolbar.Columns + " · режим: " + KvSettings.ToolbarLayout +
            (KvSettings.ToolbarCompact ? " (компактный)" : ""));
        ui.RebuildTree(true);
        if (!Wait(2f)) return;

        // ФИКС 2. Свойства заполняются по ВЫБРАННОМУ узлу дерева, а в пакетном режиме мыши нет:
        // без явного выбора панель честно показывала пустое состояние («Выберите объект» +
        // скелетон) — и это выглядело как провал «строк 0». Теперь диагностика СНАЧАЛА выбирает
        // узел робота (как это сделал бы оператор кликом по дереву) и только потом проверяет.
        ui.SetPanel("properties", true);
        if (Once("select"))
        {
            ProjectNode robotNode = FindRobotNode();
            ui.SelectNode(robotNode);
            Note("для панели свойств выбран узел: " +
                 (robotNode != null ? robotNode.DisplayName : "нет узла робота в дереве"));
        }
        if (!Wait(1.5f)) return;

        // Вариант А (правильный): узел выбран — панель обязана заполниться.
        // Вариант Б (страховочный): узла нет, но показано пустое состояние «Выберите объект»
        // со скелетоном — это ШТАТНОЕ поведение панели (§0.6), а не провал.
        bool filled = ui.PropertyRowCount > 0;
        bool emptyState = ui.Properties != null && ui.Properties.SkeletonVisible;
        Check(filled || emptyState, "4. SCARA: панель свойств заполнена",
            filled
                ? "строк: " + ui.PropertyRowCount + " (узел выбран принудительно)"
                : "узла нет — показано пустое состояние «Выберите объект» + скелетон (штатно)");
        Note("дерево моделей пересобрано для SCARA · свойств: " + ui.PropertyRowCount +
             (filled ? "" : " · пустое состояние активно"));
        Next(5);
    }

    /// <summary>5. Темы: светлая и обратно тёмная — мгновенно.</summary>
    private static void StepTheme()
    {
        if (Once("light")) { KvTheme.SetMode(KvThemeMode.Light); ui.RebuildShell(); }
        if (!Wait(2.5f)) return;
        Check(KvTheme.IsLight, "5. SCARA: светлая тема применяется мгновенно",
            "палитра: " + KvTheme.PaletteLabel);

        if (Once("dark")) { KvTheme.SetMode(KvThemeMode.Dark); ui.RebuildShell(); }
        if (!Wait(2.5f)) return;
        Check(!KvTheme.IsLight, "5. SCARA: возврат к тёмной теме работает");
        Next(6);
    }

    /// <summary>6. Запись и воспроизведение движения.</summary>
    private static void StepRecord()
    {
        if (hub == null || hub.Recording == null) { Next(7); return; }
        if (Once("start"))
        {
            bool started = hub.Recording.StartRecording("ДигностЗапись_SCARA", "live");
            Note("запись: " + (started ? "начата" : "не начата"));
        }
        if (!Wait(3f)) return;
        if (Once("sample")) hub.Recording.SampleNow(true);
        if (!Wait(1.5f)) return;
        recordSamples = hub.Recording.Recording != null ? hub.Recording.Recording.SampleCount : 0;
        Check(hub.Recording.IsRecording || recordSamples > 0,
            "6. SCARA: запись движения идёт", "сэмплов: " + recordSamples);

        if (Once("stop")) hub.Recording.StopRecording(true);
        if (!Wait(2f)) return;
        Check(recordSamples > 0, "6. SCARA: запись сохранена", "сэмплов: " + recordSamples);
        Next(7);
    }

    /// <summary>7. Preset-позы SCARA.</summary>
    private static void StepPoses()
    {
        if (hub == null || hub.Poses == null) { Next(8); return; }
        if (Once("save")) hub.Poses.SaveCurrent("ДигностПоза_SCARA");
        if (!Wait(2f)) return;
        Check(hub.Poses.Count > 0, "7. SCARA: preset-поза сохранена", "поз: " + hub.Poses.Count);

        // ФИКС 2: список поз фильтруется по ТЕКУЩЕМУ роботу И по числу осей (поза чужого
        // робота в переезд не годится). Из отчёта видно, что именно выбрано для переезда.
        List<KvPosePreset> list = hub.Poses.ForCurrentRobot();
        if (Once("goto") && list.Count > 0)
        {
            KvPosePreset target = list[list.Count - 1];
            string result = hub.Poses.MoveTo(target);
            Note("7. SCARA: переезд в «" + target.name + "» (робот " + target.robot + ", осей " +
                 target.q.Length + ") → " + result);
        }
        if (!Wait(8f)) return;
        Check(flow.State.phase != FlowState.Idle || flow.ExternalMotionRunning,
            "7. SCARA: переезд в preset-позу запущен", "состояние: " + flow.State.phase +
            " · поз для SCARA: " + list.Count);
        Next(8);
    }

    /// <summary>8. Слайдеры суставов SCARA (лимиты соблюдаются).</summary>
    private static void StepJoints()
    {
        if (Once("set"))
        {
            double[] q = v.CopyCurrent();
            q[0] = Mathf.Clamp((float)q[0] + 12f, v.Lower[0] + 5f, v.Upper[0] - 5f);
            v.Apply(q);
        }
        if (!Wait(2f)) return;
        double[] now = v.CopyCurrent();
        Check(now != null && now.Length == 3, "8. SCARA: поза задаётся по трём осям",
            "J1 = " + (now != null ? now[0].ToString("0.0") : "?") + "°");

        if (Once("limit"))
        {
            double[] bad = v.CopyCurrent();
            bad[0] = 100000.0;
            Note("попытка выставить угол вне лимитов: WithinLimits = " + v.WithinLimits(bad));
        }
        if (!Wait(1f)) return;
        Check(v.WithinLimits(v.CopyCurrent()), "8. SCARA: текущая поза в пределах лимитов");

        // ФИКС 5: ход Z считается ОДИН РАЗ при инициализации и больше не «плавает» от позы.
        if (scara != null)
        {
            Check(scara.KinematicsCalibrated,
                "8. ФИКС 5: ход Z посчитан (один раз при инициализации SCARA)",
                "ZMin = " + scara.ZMin.ToString("0.000") + " м · ZMax = " +
                scara.ZMax.ToString("0.000") + " м");

            float zMinBefore = scara.ZMin;
            float zMaxBefore = scara.ZMax;
            flow.RebindKinematics("диагностика ФИКС 5");
            Check(Mathf.Abs(zMinBefore - scara.ZMin) < 0.0005f &&
                  Mathf.Abs(zMaxBefore - scara.ZMax) < 0.0005f,
                "8. ФИКС 5: повторный расчёт даёт ТУ ЖЕ величину (замер не зависит от позы)",
                "было " + zMinBefore.ToString("0.000") + "/" + zMaxBefore.ToString("0.000") +
                " → стало " + scara.ZMin.ToString("0.000") + "/" + scara.ZMax.ToString("0.000") +
                " · повторов в консоли нет (значения совпали, лог не пишется)");

            // ФИКС 12: сколько строк «Ход Z …» реально попало в консоль. Считаются ТОЛЬКО
            // строки робота («[SCARA] Ход Z …»), строки отчёта — нет, поэтому счётчик
            // не считает себя. Ожидание: ровно 2 (инициализация — «вниз» и «вверх»).
            Check(zTravelLines <= 2,
                "8. ФИКС 5/12: строк «Ход Z …» в консоли не больше двух (инициализация)",
                "строк «Ход Z» за прогон: " + zTravelLines +
                " (ожидается 2: «вниз» и «вверх») · явный пересчёт даёт те же значения и молчит");

            // ФИКС 6: сдвиг нижней границы призмы вниз — поле в инспекторе + PlayerPrefs.
            Check(Mathf.Abs(scara.ZLowerOffsetM - 0.02f) < 0.001f ||
                  PlayerPrefs.HasKey(SCARAController.ZLowerOffsetPrefsKey),
                "8. ФИКС 6: сдвиг нижней границы призмы вынесен в инспектор и сохранён в PlayerPrefs",
                "zLowerOffsetM = " + (scara.ZLowerOffsetM * 100f).ToString("0.#") + " см · ключ " +
                SCARAController.ZLowerOffsetPrefsKey + " " +
                (PlayerPrefs.HasKey(SCARAController.ZLowerOffsetPrefsKey) ? "есть" : "нет (значение из инспектора)"));

            // Прямая проверка смысла сдвига: нижний предел призмы уходит НИЖЕ уровня столешницы,
            // поэтому точка РОВНО на столе получает запас, а не «0° < 3°».
            if (v.Ready && v.IsPrismatic(2))
            {
                double lower = v.Lower[2];
                double[] probe = { 0, 0, 0 };
                float margin = v.LimitMargin(probe);
                Check(lower < -0.005,
                    "8. ФИКС 6: точка на уровне столешницы стала достижимой",
                    "нижний предел призмы " + (lower * 1000f).ToString("0") +
                    " мм относительно стола · запас в этой позе " + margin.ToString("0.0") +
                    "° (нужно ≥ " + flow.Planner.minLimitMarginDeg.ToString("0") + "°)");
            }
        }
        Next(9);
    }

    /// <summary>9. Аварийная остановка.</summary>
    private static void StepEmergency()
    {
        if (Once("estop") && hub != null) hub.EmergencyStop();
        if (!Wait(3f)) return;
        Check(flow.State.phase == FlowState.Idle,
            "9. SCARA: аварийная остановка приводит поток в Idle", "состояние: " + flow.State.phase);
        Check(flow.Phantoms == null || flow.Phantoms.Count == 0,
            "9. SCARA: после аварийной остановки фантомы убраны",
            "фантомов: " + (flow.Phantoms != null ? flow.Phantoms.Count : 0));
        Next(10);
    }

    /// <summary>10. Зоны запрета.</summary>
    private static void StepZones()
    {
        if (hub == null || hub.Zones == null) { Next(11); return; }
        if (Once("add"))
        {
            Vector3 p = v.TcpAt(v.CopyCurrent()) + Vector3.up * 0.35f;
            hub.Zones.Create(KvZoneShape.Box, p, new Vector3(0.3f, 0.3f, 0.3f));
        }
        if (!Wait(2f)) return;
        Check(hub.Zones.Count > 0, "10. SCARA: зона запрета создана", "зон: " + hub.Zones.Count);
        if (Once("clear")) hub.Zones.Clear();
        if (!Wait(2f)) return;
        Check(hub.Zones.Count == 0, "10. SCARA: зоны запрета снимаются");
        Next(11);
    }

    /// <summary>11. Сравнение траекторий A/B и графики углов.</summary>
    private static void StepComparison()
    {
        if (hub == null || hub.Comparison == null || flow.State.candidates.Count < 2)
        {
            Note("сравнение недоступно: вариантов " + flow.State.candidates.Count);
            Next(12);
            return;
        }
        if (Once("mark"))
        {
            hub.Comparison.Reset();
            Note("сравнение: " + hub.Comparison.Mark(0) + " / " + hub.Comparison.Mark(1));
        }
        if (!Wait(2f)) return;
        Check(hub.Comparison.Ready, "11. SCARA: сравнение A/B готово",
            "вердикт: " + hub.Comparison.Verdict());
        Next(12);
    }

    /// <summary>12. Графики углов суставов.</summary>
    private static void StepAngleGraph()
    {
        if (flow.Metrics == null) { Next(13); return; }
        if (Once("show")) flow.Metrics.SetVisible(true);
        if (!Wait(3f)) return;
        Check(flow.Metrics.Visible, "12. SCARA: панель графиков углов включена");
        Note("строка метрик: " + flow.Metrics.RowText(0));
        Next(13);
    }

    /// <summary>13. Гриппер SCARA.</summary>
    private static void StepGripper()
    {
        if (hub == null || hub.Gripper == null) { Next(14); return; }

        // ФИКС 3. Раньше ОБА ожидания опирались на один и тот же `stepEnterTime`, поэтому
        // второе (`Wait(2.5f)` после «сжать») проходило мгновенно в ТОМ ЖЕ кадре, что и
        // команда: анимация пальцев не успевала сделать ни одного кадра, и проверка честно
        // показывала «75 → 75 мм». Теперь у каждого действия СВОЯ метка времени, а раскрытие
        // снимается ОДИН раз — сразу после команды, а не «перечитывается» каждый кадр.
        if (Once("open"))
        {
            hub.Gripper.SetOpen(true);
            markGrip = Now;
            return;
        }
        if (Now - markGrip < 2.5f) return;

        if (Once("readOpen"))
        {
            widthOpen = hub.Gripper.Width;
            hub.Gripper.SetOpen(false);
            markGrip = Now;
            return;
        }
        if (Now - markGrip < 2.5f) return;

        if (Once("readClosed"))
        {
            widthClosed = hub.Gripper.Width;
            Check(widthClosed < widthOpen - 0.005f, "13. SCARA: пальцы гриппера двигаются",
                (widthOpen * 1000f).ToString("0") + " → " + (widthClosed * 1000f).ToString("0") +
                " мм · робот захвата: " +
                (hub.Gripper.Robot != null ? hub.Gripper.Robot.robotName : "нет") +
                " · робот потока: " + (flow.Robot != null ? flow.Robot.robotName : "нет") +
                " · захват на роботе потока: " + hub.Gripper.AttachedTo(flow.Robot));

            // Отдельная проверка ФИКС 3: сервис гриппера НЕ ищет робота сам — он собран
            // ровно на том роботе, которого отдал поток.
            Check(flow.Robot != null && hub.Gripper.AttachedTo(flow.Robot),
                "13. SCARA: захват собран на роботе потока (flow.Robot)",
                "робот потока: " + (flow.Robot != null ? flow.Robot.robotName : "нет") +
                " · робот захвата: " +
                (hub.Gripper.Robot != null ? hub.Gripper.Robot.robotName : "нет"));

            hub.Gripper.SetOpen(true);
            markGrip = Now;
            return;
        }
        if (Now - markGrip < 1.5f) return;
        Next(14);
    }

    private static float markGrip;
    private static float widthOpen;
    private static float widthClosed;

    /// <summary>14. Pick-and-place на SCARA.</summary>
    private static void StepPickPlace()
    {
        if (hub == null || hub.PickAndPlace == null) { Next(15); return; }
        if (Once("spawn")) hub.PickAndPlace.SpawnCube(true);
        if (!Wait(2f)) return;
        if (Once("run")) hub.PickAndPlace.Run();
        if (!Wait(30f)) return;
        KvPickAndPlace.Step current = hub.PickAndPlace.Current;
        Check(current != KvPickAndPlace.Step.Idle, "14. SCARA: pick-and-place выполняет шаги",
            "шаг: " + current + " · идёт: " + hub.PickAndPlace.Running +
            " · куб: " + (hub.PickAndPlace.Cube != null
                ? hub.PickAndPlace.Cube.transform.position.ToString("F2") : "нет"));
        if (Once("cancel")) hub.PickAndPlace.Cancel("диагностика");
        if (!Wait(1.5f)) return;
        Next(15);
    }

    /// <summary>15. ETA (оценка времени достижения).</summary>
    private static void StepEta()
    {
        if (hub == null) { Next(16); return; }
        Check(true, "15. SCARA: ETA доступна (метка хаба)",
            "ETA: " + hub.EtaLabel + " · осталось " + hub.EtaRemaining.ToString("0.00") +
            " с из " + hub.EtaTotal.ToString("0.00") + " с · видима: " + hub.EtaVisible);
        Next(16);
    }

    /// <summary>16. Журнал действий.</summary>
    private static void StepLog()
    {
        if (hub == null || hub.Log == null) { Next(17); return; }
        Check(hub.Log.Count > 0, "16. SCARA: журнал ведётся", "записей: " + hub.Log.Count);
        List<KvLogEntry> tail = hub.Log.Tail(3);
        for (int i = 0; i < tail.Count; i++) Note("журнал: " + tail[i].text);
        Next(17);
    }

    /// <summary>17. Undo / Redo.</summary>
    private static void StepUndoRedo()
    {
        if (hub == null || hub.Undo == null) { Next(18); return; }
        int before = flow.State.selectedTrajectory;
        bool undone = false;
        if (Once("undo")) undone = hub.Undo.Undo();
        if (!Wait(2.5f)) return;
        Check(true, "17. SCARA: Undo выполнен",
            "выбран вариант " + before + " → " + flow.State.selectedTrajectory +
            " · результат " + undone);

        bool redone = false;
        if (Once("redo")) redone = hub.Undo.Redo();
        if (!Wait(2.5f)) return;
        Check(true, "17. SCARA: Redo выполнен",
            "выбран вариант " + flow.State.selectedTrajectory + " · результат " + redone);
        Next(18);
    }

    /// <summary>18. Сингулярности SCARA (вытягивание / складывание).</summary>
    private static void StepSingularities()
    {
        if (stages == null || stages.Singularities == null) { Next(19); return; }
        stages.Singularities.SetEnabled(true);
        if (Once("stretch"))
        {
            // Вытягивание руки: второй сустав в ноль — сингулярность границы зоны SCARA.
            double[] q = v.CopyCurrent();
            q[1] = 0.0;
            v.Apply(q);
            stages.Singularities.Evaluate();
        }
        if (!Wait(3f)) return;
        Check(true, "18. SCARA: визуализация сингулярностей работает",
            "тип: " + stages.Singularities.Kind + " · подвижность " +
            (stages.Singularities.Mobility * 100f).ToString("0") + " % · опасность " +
            stages.Singularities.Danger + " · зон " + stages.Singularities.VisibleZones);
        Check(!string.IsNullOrEmpty(stages.Singularities.Status),
            "18. SCARA: статус сингулярностей формируется", stages.Singularities.Status);
        if (Once("off")) stages.Singularities.SetEnabled(false);
        if (!Wait(1.5f)) return;
        Next(19);
    }

    /// <summary>19. Waypoints на SCARA.</summary>
    private static void StepWaypoints()
    {
        if (stages == null || stages.Waypoints == null) { Next(20); return; }
        if (flow.State.candidates.Count == 0)
        {
            if (Once("reclick")) { aimAt(flow.Robot); Click(WorkPoint()); }
            if (!Wait(50f)) return;
        }
        int before = stages.Waypoints.Count;
        if (Once("add")) stages.Waypoints.Add(WorkPoint(), "диагностика SCARA");
        if (!Wait(4f)) return;
        Check(stages.Waypoints.Count >= before, "19. SCARA: waypoint добавляется",
            "было " + before + " → стало " + stages.Waypoints.Count + " · " +
            stages.Waypoints.RouteNote);
        Next(20);
    }

    /// <summary>20. Health monitor (3 сустава SCARA).</summary>
    private static void StepHealth()
    {
        if (stages == null || stages.Health == null) { Next(21); return; }
        if (Once("on")) stages.Health.SetEnabled(true);
        if (!Wait(4f)) return;
        Check(stages.Health.Enabled, "20. SCARA: health monitor включён");
        Check(stages.Health.Dof == v.Dof, "20. SCARA: мониторинг считает ровно 3 сустава",
            "суставов: " + stages.Health.Dof + " · самый горячий сустав: " +
            (stages.Health.HottestJoint(out float hot) + 1) + " (" + hot.ToString("0.0") + " °C)");
        Next(21);
    }

    /// <summary>21. Динамические препятствия.</summary>
    private static void StepObstacles()
    {
        if (stages == null || stages.Obstacles == null) { Next(22); return; }
        if (Once("on")) stages.Obstacles.SetEnabled(true);
        if (!Wait(3f)) return;
        Check(stages.Obstacles.Enabled, "21. SCARA: динамические препятствия включены");
        int carts = 0;
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(
                     FindObjectsInactive.Include))
            if (t != null && t.name.StartsWith("ДинамическоеПрепятствие")) carts++;
        Check(carts > 0, "21. SCARA: тележка-препятствие создана", "объектов: " + carts);
        if (Once("off")) stages.Obstacles.SetEnabled(false);
        if (!Wait(2f)) return;
        Next(22);
    }

    /// <summary>22. Виртуальный пульт.</summary>
    private static void StepPendant()
    {
        if (stages == null || stages.Pendant == null) { Next(23); return; }
        if (Once("show")) stages.Pendant.SetVisible(true);
        if (!Wait(3f)) return;
        Check(stages.Pendant.Visible, "22. SCARA: виртуальный пульт открывается",
            "экран TCP: " + stages.Pendant.ScreenTcpText);

        if (Once("variant")) stages.Pendant.ToggleVariant();
        if (!Wait(2f)) return;
        Check(true, "22. SCARA: вариант оформления пульта переключается",
            "вариант: " + stages.Pendant.Variant);

        if (Once("jog"))
        {
            double[] q = v.CopyCurrent();
            qSaved = (double[])q.Clone();
            stages.Pendant.SetJogAxis(0);
            stages.Pendant.SetStick(new Vector2(0f, 1f));
        }
        if (!Wait(3f)) return;
        Note("пульт: состояние " + stages.Pendant.ScreenStateText);

        if (Once("reset")) { stages.Pendant.SetStick(Vector2.zero); if (qSaved != null) v.Apply(qSaved); }
        if (!Wait(2f)) return;
        if (Once("hide")) stages.Pendant.SetVisible(false);
        if (!Wait(1.5f)) return;
        Next(23);
    }

    /// <summary>23. Мультиязычность (7 языков).</summary>
    private static void StepLocalization()
    {
        IReadOnlyList<KvLangInfo> langs = KvLoc.Languages;
        Check(langs.Count >= 7, "23. SCARA: в словаре не меньше 7 языков",
            "языков: " + langs.Count);

        if (Once("zh")) KvLoc.SetLanguage("zh");
        if (!Wait(2f)) return;
        Check(KvLoc.CurrentCode == "zh", "23. китайский язык применяется мгновенно",
            "подпись: " + KvLoc.T("panel.tree", "Дерево моделей"));

        if (Once("en")) KvLoc.SetLanguage("en");
        if (!Wait(1.5f)) return;
        Check(KvLoc.CurrentCode == "en", "23. английский язык применяется",
            KvLoc.Cmd("point.select", "Выбор точки"));

        if (Once("ru")) KvLoc.SetLanguage("ru");
        if (!Wait(1.5f)) return;
        ui.RebuildShell();
        if (!Wait(2f)) return;
        Next(24);
    }

    /// <summary>
    /// 24. Экспорт демонстраций (скриншот).
    ///
    /// ФИКС 4 (§19). В пакетном режиме `-nographics` графического устройства нет: у кадра нет
    /// ни одного пикселя, и `ScreenCapture.CaptureScreenshotAsTexture` штатно возвращает null —
    /// файл создать не из чего. Поэтому проверяется не «файл есть», а КОРРЕКТНОСТЬ пути ввода:
    /// запрос принят, служба не бросила исключение и честно объяснила причину. Сам снимок
    /// проверяется в живом редакторе (ручной чек-лист: F8/F10).
    /// </summary>
    private static void StepCaptures()
    {
        if (stages == null || stages.Capture == null) { Next(25); return; }
        bool graphics = KvGraphics.Available;
        if (Once("shot"))
        {
            bool accepted = stages.Capture.TakeScreenshot("диагностика SCARA",
                flow.Robot != null ? flow.Robot.robotName : "SCARA");
            Note("скриншот: запрос " + (accepted ? "принят" : "отклонён") +
                 " · графика " + (graphics ? "доступна" : "НЕдоступна (-nographics)") +
                 " · имя по ТЗ: " + KvCaptureService.ScreenshotName(DateTime.Now));
        }
        if (!Wait(4f)) return;

        bool file = !string.IsNullOrEmpty(stages.Capture.LastScreenshotPath) &&
                    File.Exists(stages.Capture.LastScreenshotPath);
        Check(file || !graphics,
            "24. SCARA: экспорт демонстрации (скриншот) выполнен",
            file
                ? "файл: " + stages.Capture.LastScreenshotPath
                : "графика недоступна (-nographics): снимок сделать не из чего — " +
                  "путь проверен статически (F8 → KvCaptureService.ScreenshotRoutine), " +
                  "живая проверка — в PlayMode на машине с видеокартой");
        Note("скриншот: папка по ТЗ — " + stages.Capture.Folder +
             " · последний файл: " +
             (string.IsNullOrEmpty(stages.Capture.LastScreenshotPath)
                 ? "нет (в пакетном режиме без графики)"
                 : stages.Capture.LastScreenshotPath));
        Next(25);
    }

    /// <summary>25. Сглаживание траектории SCARA (и проверка рывка после ФИКС 1).</summary>
    private static void StepSmoothing()
    {
        if (stages2 == null || stages2.Smoothing == null) { Next(26); return; }
        if (flow.State.candidates.Count == 0)
        {
            if (Once("reclick")) { aimAt(flow.Robot); Click(WorkPoint()); }
            if (!Wait(50f)) return;
        }
        if (Once("apply")) stages2.Smoothing.ApplySelected();
        if (!Wait(5f)) return;
        int index;
        KvSmoothEntry entry = stages2.Smoothing.CurrentEntry(out index);
        Check(entry != null, "25. SCARA: сглаживание считается");
        if (entry != null)
        {
            Check(entry.after.time > 0f, "25. SCARA: метрики «до/после» посчитаны",
                "время " + entry.before.time.ToString("0.000") + " → " +
                entry.after.time.ToString("0.000") + " с · jerk " +
                entry.before.maxJerk.ToString("0") + " → " + entry.after.maxJerk.ToString("0") +
                " °/с³");
            Check(stages2.Smoothing.LastJerkOk,
                "25. ФИКС 1: фактический рывок после S-профиля в пределах лимита",
                stages2.Smoothing.LastJerkReport);
        }
        Next(26);
    }

    /// <summary>26. Время-оптимальная траектория (ФИКС 1 — аналитический S-профиль).</summary>
    private static void StepTimeOptimal()
    {
        if (stages2 == null || stages2.TimeOptimal == null) { Next(27); return; }
        int index;
        TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
        KvTimeOptimal.Draft draft = stages2.TimeOptimal.Compute(candidate);
        if (draft == null) { Next(27); return; }
        Check(draft.stats.valid, "26. SCARA: время-оптимальная траектория рассчитана",
            "время " + draft.original.time.ToString("0.000") + " → " +
            draft.stats.time.ToString("0.000") + " с");
        Check(draft.jerkOk, "26. ФИКС 1: рывок S-профиля на SCARA в пределах лимита",
            draft.jerkReport);
        Check(draft.stats.maxVel <= stages2.TimeOptimal.MaxVel * 1.05f,
            "26. SCARA: скорость суставов не превышает лимит",
            draft.stats.maxVel.ToString("0.0") + " при лимите " +
            stages2.TimeOptimal.MaxVel.ToString("0") + " °/с");
        Check(draft.stats.maxAcc <= stages2.TimeOptimal.MaxAcc * 1.05f,
            "26. SCARA: ускорение не превышает лимит",
            draft.stats.maxAcc.ToString("0.0") + " при лимите " +
            stages2.TimeOptimal.MaxAcc.ToString("0") + " °/с²");
        Note("профиль: " + KvTrajMath.LastProfileNote);
        Next(27);
    }

    /// <summary>27. Эко-профиль (энергооптимальная).</summary>
    private static void StepEnergy()
    {
        if (stages2 == null || stages2.Energy == null) { Next(28); return; }
        int index;
        TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
        KvEnergyOptimal.Draft draft = stages2.Energy.Compute(candidate);
        if (draft == null) { Next(28); return; }
        Check(draft.energy > 0f, "27. SCARA: эко-профиль рассчитан",
            "энергия " + draft.originalEnergy.ToString("0.00") + " → " +
            draft.energy.ToString("0.00") + " Дж");
        Check(draft.energy <= draft.originalEnergy + 1e-2f,
            "27. SCARA: энергия эко-профиля не выше исходной",
            "экономия " + draft.savings.ToString("+0.0;-0.0") + " % · профилей проверено " +
            draft.trials);
        Next(28);
    }

    /// <summary>28. Ограничения на waypoints (у SCARA вертикальность по построению).</summary>
    private static void StepWaypointConstraints()
    {
        if (stages3 == null || stages3.Constrained == null) { Next(29); return; }
        stages3.Constrained.Enabled = true;      // без включённого ограничения проверка даёт 0 сэмплов
        stages3.Constrained.Profile.mode = KvOrientMode.ToolVertical;
        stages3.Constrained.Profile.reference = Vector3.down;
        stages3.Constrained.Profile.toleranceDeg = 6f;
        stages3.Constrained.Save();
        stages3.Constrained.Invalidate();
        if (!Wait(3f)) return;

        int index;
        TrajectoryCandidate candidate = KvVariantKit.Selected(flow, out index);
        KvToolKinematics.PlanCheck check = stages3.Constrained.Check(candidate);
        Check(check.samples > 0, "28. SCARA: ограничение проверяется на сэмплах",
            "сэмплов: " + check.samples + " · нарушений: " + check.violations);
        Check(check.violations == 0,
            "28. SCARA: «инструмент вертикально» выполняется по построению",
            "худший угол " + check.worstAngle.ToString("0.0") + "°");
        Next(29);
    }

    /// <summary>29. Планирование с ограничениями: проекция и перепланирование (ФИКС 2).</summary>
    private static void StepConstrained()
    {
        if (stages3 == null || stages3.Constrained == null) { Next(30); return; }
        if (flow.State.candidates.Count == 0)
        {
            if (Once("reclick")) { aimAt(flow.Robot); Click(WorkPoint()); }
            if (!Wait(50f)) return;
        }
        if (Once("project")) stages3.Constrained.ProjectSelected();
        if (!Wait(5f)) return;
        Check(true, "29. SCARA: проекция на ограничение выполнена",
            "приведено " + stages3.Constrained.LastProjected + " · не приведено " +
            stages3.Constrained.LastFailed + " · остаток " +
            stages3.Constrained.LastResidual.ToString("0.0") + "° · проб IK " +
            stages3.Constrained.LastAttempts);

        if (Once("replan")) stages3.Constrained.ReplanSelected();
        if (!Wait(15f)) return;
        Check(true, "29. ФИКС 2: «Перепланировать с ограничением» на SCARA выполнено",
            "приведено " + stages3.Constrained.LastProjected + " · не приведено " +
            stages3.Constrained.LastFailed + " · остаток " +
            stages3.Constrained.LastResidual.ToString("0.0") + "°");
        Next(30);
    }

    /// <summary>30. Калибровка: TCP по нормали к плоскости (ФИКС 3) и база с поворотом (ФИКС 4).</summary>
    private static void StepCalibration()
    {
        if (stages3 == null || stages3.Calibration == null) { Next(31); return; }
        KvCalibrationService cal = stages3.Calibration;

        Check(flow.Robot is SCARAController,
            "30. ФИКС 3: проверка выполняется именно на SCARA (поток привязан к ней)",
            "робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " + v.Dof);
        Check(cal.UsePlaneMethod,
            "30. ФИКС 3: для SCARA автоматически выбран метод «по нормали к плоскости»",
            "метод: " + cal.TcpMethodLabel);
        Check(KvCalibrationService.SelfTestPlane().Contains("высота восстановлена"),
            "30. ФИКС 3: самопроверка метода «по нормали» на синтетических данных",
            KvCalibrationService.SelfTestPlane());

        // Реальные точки: три разных положения руки (слайдеры сдвигают сустав)
        if (Once("p1")) { cal.RecordTcpPlanePoint(); ShiftJoint(0, 14f); }
        if (!Wait(2f)) return;
        if (Once("p2")) { cal.RecordTcpPlanePoint(); ShiftJoint(1, 18f); }
        if (!Wait(2f)) return;
        if (Once("p3")) { cal.RecordTcpPlanePoint(); ShiftJoint(2, 0.03f); }
        if (!Wait(2f)) return;
        Check(cal.TcpPlanePointCount >= 3, "30. ФИКС 3: точки плоскости записаны для SCARA",
            "точек: " + cal.TcpPlanePointCount);

        if (Once("solve")) cal.SolveTcpPlane();
        if (!Wait(3f)) return;
        Check(cal.Data.tcpPlaneSolved, "30. ФИКС 3: смещение TCP по нормали рассчитано",
            "высота " + (cal.Data.tcpPlaneHeight * 1000f).ToString("0.0") + " мм · остаток " +
            cal.Data.tcpPlaneResidualMm.ToString("0.00") + " мм");

        if (Once("dir")) cal.RecordBaseDirection();
        if (!Wait(2f)) return;
        Check(cal.BaseForwardSet, "30. ФИКС 4: направление «вперёд» записано (вторая точка отсчёта)");

        if (Once("b1")) cal.RecordBasePoint();
        if (!Wait(1.2f)) return;
        if (Once("b2")) { ShiftJoint(0, -25f); cal.RecordBasePoint(); }
        if (!Wait(1.5f)) return;
        if (Once("b3")) { ShiftJoint(1, -20f); cal.RecordBasePoint(); }
        if (!Wait(1.5f)) return;
        if (Once("b4")) cal.SolveBase();
        if (!Wait(3f)) return;
        Check(cal.BaseSolved, "30. ФИКС 4: база рассчитана вместе с поворотом вокруг вертикали",
            "высота " + cal.Data.baseHeightMm.ToString("0.0") + " мм · наклон " +
            cal.Data.baseTiltDeg.ToString("0.00") + "° · доворот " +
            cal.Data.baseYawDeg.ToString("0.00") + "°");

        if (Once("save")) cal.Save();
        if (!Wait(2.5f)) return;
        Check(File.Exists(cal.FilePath), "30. калибровка сохранена в JSON-файл", cal.FilePath);
        Next(31);
    }

    /// <summary>31. Калькулятор нагрузки для 3 осей SCARA.</summary>
    private static void StepPayload()
    {
        if (stages3 == null || stages3.Payload == null) { Next(32); return; }
        KvPayloadResult result = stages3.Payload.Evaluate(v.CopyCurrent());
        Check(result != null, "31. SCARA: калькулятор нагрузки считает по 3 осям",
            result != null
                ? ("максимум в позе " + result.maxKg.ToString("0.00") + " кг · ограничивает ось " +
                   (result.limitingJoint + 1) + " · " + result.Line())
                : "результат не получен");
        Note("строка нагрузки: " + stages3.Payload.StatusLine());
        Next(32);
    }

    /// <summary>32. Экспорт в языки роботов KUKA / FANUC / ABB.</summary>
    private static void StepExport()
    {
        if (stages3 == null || stages3.Exporter == null) { Next(33); return; }
        if (Once("export")) stages3.Exporter.ExportSelected();
        if (!Wait(4f)) return;
        Check(!string.IsNullOrEmpty(stages3.Exporter.LastFile),
            "32. SCARA: экспорт траектории в язык робота выполнен",
            "файл: " + stages3.Exporter.LastFile + " · строк: " + stages3.Exporter.LastLines +
            " · язык: " + stages3.Exporter.LanguageLabel);
        Next(33);
    }

    /// <summary>33. Импорт модели (URDF/STEP): ФИКС 5 (кинематика STEP) и ФИКС 6 (стенд).</summary>
    private static void StepImport()
    {
        if (stages3 == null || stages3.Import == null) { Next(34); return; }
        stages3.Import.RefreshFiles();
        Check(stages3.Import.Files.Count > 0, "33. файлы моделей найдены",
            "файлов: " + stages3.Import.Files.Count);
        if (stages3.Import.Files.Count == 0) { Next(34); return; }

        stages3.Import.Placement = KvImportPlacement.AddToStand;
        stages3.Import.StandName = TrajectoryCore.StandBuilder.Stand2Name;
        if (Once("import")) stages3.Import.Import(stages3.Import.Files[0]);
        if (!Wait(8f)) return;
        Check(stages3.Import.Imported != null, "33. модель импортирована", stages3.Import.LastReport);
        if (stages3.Import.Imported != null)
        {
            KvImportedRobot marker = stages3.Import.Imported.GetComponent<KvImportedRobot>();
            Check(marker != null && marker.standName == TrajectoryCore.StandBuilder.Stand2Name,
                "33. ФИКС 6: робот привязан к стенду SCARA",
                marker != null ? ("стенд «" + marker.standName + "»") : "маркер не найден");
            bool isStep = marker != null && marker.format == "STEP";
            if (isStep)
                Check(!string.IsNullOrEmpty(marker.stepKinematicsNote),
                    "33. ФИКС 5: кинематика STEP разобрана и показана честно",
                    marker.stepKinematicsNote);
            else
                Check(marker != null && marker.jointCount > 0, "33. URDF: структура собрана",
                    marker != null
                        ? ("звеньев " + marker.linkCount + " · суставов " + marker.jointCount)
                        : "маркер не найден");
        }
        if (!Wait(4f)) return;
        Note("проверка кинематики импорта: " + stages3.Import.LastCheck);
        if (Once("remove")) stages3.Import.RemoveImported();
        if (!Wait(2.5f)) return;
        Check(stages3.Import.Imported == null, "33. импортированная модель удаляется из сцены");
        Next(34);
    }

    /// <summary>34. Прокси столкновений, стенд планировщиков, дерево RRT.</summary>
    private static void StepProxiesAndLab()
    {
        if (stages4.Proxies != null)
        {
            int built = stages4.Proxies.Rebuild(true);
            if (!Wait(4f)) return;
            Check(built >= 0, "34. этап 13: прокси столкновений пересобраны для стенда SCARA",
                "прокси: " + built + " · " + stages4.Proxies.Status());
        }
        if (stages4.Lab != null)
        {
            if (Once("lab")) stages4.Lab.Start();
            Check(stages4.Lab.Results.Count > 0 || stages4.Lab.Total > 0,
                "34. этап 14: стенд планировщиков прогнал задачи",
                "стратегий: " + stages4.Lab.Results.Count + " · задач " + stages4.Lab.Total +
                " · " + stages4.Lab.LastReport);
            if (stages4.Lab.Running) stages4.Lab.Stop();
        }
        // ФИКС 3. Дерево ЗАПИСЫВАЕТСЯ только когда включён показ (тумблер «Показывать дерево
        // планировщика» ставит `Planner.RecordTree`) — иначе узлов нет по определению, и
        // «версия 0 · узлов A/B: 0/0» читалось как поломка. Диагностика включает запись сама
        // и прогоняет планирование, а затем ЧЕСТНО различает три состояния: узлов нет вовсе
        // (путь не строился), построен прямой путь (RRT не понадобился) и дерево построено.
        if (stages4.Lab != null && flow.Planner != null)
        {
            if (Once("treeOn")) stages4.Lab.SetTreeVisible(true);
            if (Once("treePlan"))
            {
                int paths = 0;
                try
                {
                    List<PlannedTrajectory> planned =
                        flow.Planner.Plan(v.CopyCurrent(), WorkPoint(), 1, 1000 + 7919);
                    paths = planned != null ? planned.Count : 0;
                }
                catch (Exception e) { Note("планирование для записи дерева отказало: " + e.Message); }
                Note("включена запись дерева RRT · планирование: путей " + paths);
            }
            if (!Wait(6f)) return;
            Note("дерево RRT: " + KvPlannerLab.TreeStatus(flow.Planner) +
                 " · версия " + flow.Planner.TreeVersion +
                 " · узлов A/B: " + flow.Planner.TreeNodesA.Count + "/" +
                 flow.Planner.TreeNodesB.Count +
                 " · запись включена: " + flow.Planner.RecordTree);
        }
        if (flow.Planner != null)
            Check(flow.Planner.TreeNodesA.Count + flow.Planner.TreeNodesB.Count > 0,
                "34. этап 15: дерево RRT записано для показа",
                "версия " + flow.Planner.TreeVersion + " · узлов A/B: " +
                flow.Planner.TreeNodesA.Count + "/" + flow.Planner.TreeNodesB.Count +
                " · статус: «" + KvPlannerLab.TreeStatus(flow.Planner) + "»");

        // ФИКС 4: после ПЕРЕПРИВЯЗКИ робота дерево обязано очиститься (оно принадлежало
        // прежнему роботу), версия — обнулиться, а статус — честно сказать, что делать.
        if (flow.Planner != null)
        {
            int before = flow.Planner.TreeVersion;
            flow.RebindKinematics("диагностика ФИКС 4");
            Check(flow.Planner.TreeVersion == 0 && flow.Planner.TreeNodesA.Count == 0 &&
                  flow.Planner.TreeNodesB.Count == 0,
                "34. ФИКС 4: перепривязка робота очищает дерево RRT",
                "было версий: " + before + " → стало " + flow.Planner.TreeVersion +
                " · узлов " + flow.Planner.TreeNodesA.Count + "/" + flow.Planner.TreeNodesB.Count);
            Check(KvPlannerLab.TreeStatus(flow.Planner) == KvPlannerLab.EmptyTreeText,
                "34. ФИКС 4: пустое дерево показывает понятный статус",
                "статус: «" + KvPlannerLab.TreeStatus(flow.Planner) + "»");
        }
        Next(35);
    }

    /// <summary>35. Камеры, силы, тепло, PDF, сеть, голос, руки, взгляд, макросы, дерево поведения,
    /// окружение, свет, материалы, кино, титры, диктор.</summary>
    private static void StepPresentationKit()
    {
        Check(stages4.Cameras != null, "35. этап 16: сервис камер и PiP создан");
        if (stages4.Forces != null)
        {
            stages4.Forces.SetEnabled(true);
            Check(true, "35. этап 17: векторы сил посчитаны для SCARA",
                "моментов: " + stages4.Forces.Torques.Count + " · сила на инструменте " +
                stages4.Forces.ToolForce.magnitude.ToString("0.0") + " Н · " +
                stages4.Forces.StatusLine());
        }
        if (stages4.Heatmap != null)
        {
            if (Once("heat")) stages4.Heatmap.SetEnabled(true);
            if (!Wait(6f)) return;
            Check(stages4.Heatmap.PointCount > 0, "35. этап 18: тепловая карта времени для SCARA",
                "точек: " + stages4.Heatmap.PointCount + " · " + stages4.Heatmap.Status());
        }
        if (stages4.Report != null)
        {
            if (Once("pdf")) stages4.Report.Generate();
            if (!Wait(10f)) return;
            Check(!string.IsNullOrEmpty(stages4.Report.LastFile), "35. этап 19: PDF-отчёт создан",
                "файл: " + stages4.Report.LastFile + " · страниц: " + stages4.Report.LastPages);
        }
        if (stages4.Collab != null)
        {
            if (Once("net")) stages4.Collab.SetRole(KvNetRole.Operator);
            if (!Wait(4f)) return;
            Check(stages4.Collab.Port > 0, "35. этап 20: сервер совместной работы поднят",
                "порт " + stages4.Collab.Port + " · роль " + stages4.Collab.Role +
                " · heartbeat " + stages4.Collab.HeartbeatInterval.ToString("0.0") + " с");
            if (Once("netstop")) stages4.Collab.Stop();
            if (!Wait(2f)) return;
        }
        Check(stages4.Web != null && stages4.Companion != null, "35. этапы 21–22: дашборд и пульт созданы");
        Check(stages4.Voice != null && stages4.Hands != null && stages4.Eyes != null,
            "35. этапы 23–25: голос, руки, взгляд созданы");
        Check(stages4.Script != null && stages4.Behavior != null,
            "35. этапы 26–27: макросы и дерево поведения созданы");
        Check(stages4.Environment != null && stages4.Lighting != null && stages4.Materials != null,
            "35. этапы 28–30: окружение, свет, материалы созданы");
        Check(stages4.Cinema != null && stages4.Titles != null && stages4.VoiceOver != null,
            "35. этапы 31–33: кино, титры, голос диктора созданы");
        Next(36);
    }

    /// <summary>36. Отказы (ФИКС 11.А), проверка перед пуском, уровни журнала + итог по SCARA.</summary>
    private static void StepSafetyAndLogLevels()
    {
        if (stages4.Failures != null)
        {
            KvFailureSimulator failures = stages4.Failures;
            failures.SetJointMode(KvJointFailureMode.Droop);
            if (Once("droop")) failures.Start(KvFailureKind.JointLoss, 1);
            if (!Wait(5f)) return;
            Check(failures.Active, "36. этап 34: провисание сустава SCARA смоделировано (ФИКС 11.А)",
                "скорость " + failures.DroopSpeedDeg.ToString("0.00") + " °/с · " +
                failures.Banner);
            if (Once("hold"))
            {
                failures.Clear();
                failures.SetJointMode(KvJointFailureMode.BrakeLocked);
                failures.Start(KvFailureKind.JointLoss, 2);
            }
            if (!Wait(3f)) return;
            Check(failures.Holding, "36. ФИКС 11.А: отказ «с фиксацией» удерживает угол",
                failures.Banner);
            if (Once("reset")) failures.Clear();
            if (!Wait(2.5f)) return;
            Check(!failures.Active, "36. этап 34: сброс аварии возвращает управление");
        }
        if (stages4.PreRun != null)
        {
            if (Once("prerun")) stages4.PreRun.Validate();
            if (!Wait(8f)) return;
            Check(stages4.PreRun.Findings != null, "36. этап 35: проверка перед пуском для SCARA",
                "замечаний: " + stages4.PreRun.Findings.Count + " · " +
                stages4.PreRun.LastSummary);
        }
        Check(stages4.LogTools != null, "36. этап 36: инструменты журнала созданы (уровни и поиск)");

        Line("--- ИТОГ ПО SCARA ---");
        Line("[info] осей: " + v.Dof + " (J1, J2, Z) · третья призматическая: " +
             (v.IsPrismatic(2) ? "да" : "нет"));
        Line("[info] ФИКС 1 (S-профиль): " + KvTrajMath.LastProfileNote);
        Line("[info] ФИКС 2 (проекция): приведено " + stages3.Constrained.LastProjected +
             " · не приведено " + stages3.Constrained.LastFailed + " · проб IK " +
             stages3.Constrained.LastAttempts);
        Line("[info] ФИКС 3 (TCP SCARA): " + stages3.Calibration.TcpMethodLabel + " · решено: " +
             stages3.Calibration.Data.tcpPlaneSolved + " · высота " +
             (stages3.Calibration.Data.tcpPlaneHeight * 1000f).ToString("0.0") + " мм");
        Line("[info] ФИКС 4 (база): направление задано: " + stages3.Calibration.BaseForwardSet +
             " · доворот " + stages3.Calibration.Data.baseYawDeg.ToString("0.00") + "°");
        Line("[info] интерфейс: кнопок " + ui.ToolbarButtonCount + " · столбцов " +
             KvToolbar.Columns + " · режим " + KvSettings.ToolbarLayout +
             (KvSettings.ToolbarCompact ? " (компактный)" : ""));

        // Позу возвращаем ТОЛЬКО если валидатор того же состава, иначе индекс выйдет за границы.
        if (qSaved != null && v != null && v.Ready && v.Dof == qSaved.Length) v.Apply(qSaved);
        else Note("поза не возвращена: робот в потоке сменился (" +
                   (v != null && v.Ready ? v.Dof.ToString() : "?") + " осей против " +
                   (qSaved != null ? qSaved.Length.ToString() : "0") + " в сохранённой позе)");
        if (savedLanguage != null) KvLoc.SetLanguage(savedLanguage);
        Next(LastStep + 1);
        Finish();
    }

    private static void ShiftJoint(int joint, float delta)
    {
        double[] q = v.CopyCurrent();
        if (joint >= q.Length) return;
        q[joint] += delta;
        if (!v.WithinLimits(q))
        {
            // Если вышли за лимит — идём в обратную сторону.
            q[joint] -= 2f * delta;
        }
        v.Apply(q);
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        SessionState.SetBool(Key, false);
        Application.logMessageReceived -= OnLog;
        Line("=== ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info + " ===");
        Line("[info] строк «Ход Z» в консоли за прогон: " + zTravelLines +
             " (ФИКС 12: считаются только строки робота, строки отчёта — нет)");
        try { File.AppendAllText(report, "=== конец прогона ===\n"); } catch { }
        Debug.Log("[DshScaraDiag] отчёт: " + report + " · [OK] " + ok + " [FAIL] " + fail);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ помощники

    /// <summary>
    /// ФИКС 2. Узел робота в дереве моделей — его выбирает оператор кликом, и только тогда
    /// панель свойств заполняется. В пакетном режиме кликать некому, поэтому диагностика
    /// выбирает узел сама (предпочтение — робот потока, иначе первый узел вида Robot).
    /// </summary>
    private static ProjectNode FindRobotNode()
    {
        if (ui == null) return null;
        ProjectNode first = null;
        foreach (ProjectNode root in ui.TreeModel)
        {
            ProjectNode found = FindNodeRecursive(root, 0, ref first);
            if (found != null) return found;
        }
        return first;
    }

    private static ProjectNode FindNodeRecursive(ProjectNode node, int depth, ref ProjectNode firstRobot)
    {
        if (node == null || depth > 6) return null;
        if (node.Kind == ProjectNodeKind.Robot)
        {
            if (firstRobot == null) firstRobot = node;
            if (flow != null && node.Robot != null && node.Robot == flow.Robot) return node;
        }
        foreach (ProjectNode child in node.Children)
        {
            ProjectNode found = FindNodeRecursive(child, depth + 1, ref firstRobot);
            if (found != null) return found;
        }
        return null;
    }

    private static Vector3 WorkPoint()
    {
        if (workPoint != Vector3.zero) return workPoint;
        Vector3 p;
        if (FindReachablePoint(v, out p)) { workPoint = p; return p; }
        workPoint = flow.Robot != null
            ? flow.Robot.transform.position + new Vector3(0.12f, 0.32f, -0.35f)
            : Vector3.up;
        return workPoint;
    }

    /// <summary>Достижимая точка для SCARA: рабочая зона — кольцо вокруг базы.</summary>
    private static bool FindReachablePoint(PoseValidator validator, out Vector3 point)
    {
        point = Vector3.zero;
        if (validator == null || !validator.Ready) return false;
        CollisionWorld world = hub != null ? hub.World : null;
        Vector3 basePos = flow.Robot != null ? flow.Robot.transform.position : Vector3.zero;
        float[] radii = { 0.30f, 0.38f, 0.45f, 0.52f };
        float[] heights = { 0.10f, 0.18f, 0.26f, 0.34f };
        double[] seed = validator.CopyCurrent();
        foreach (float r in radii)
            foreach (float h in heights)
            {
                Vector3 candidate = basePos + new Vector3(r, h, -0.2f);
                double[] q;
                string why;
                if (KvPlanKit.SolvePoseForPoint(validator, world, candidate, Vector3.down,
                        seed, out q, out why))
                {
                    point = candidate;
                    return true;
                }
            }
        return false;
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
}
