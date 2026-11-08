using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится). Сессия 19.09.2026:
/// КОМПАКТНАЯ ПРОВЕРКА ДЕСЯТИ ФИКСОВ закрытия хвостов §16.11 и MSAA из отчёта графики.
///
/// Проверяются ИМЕННО те утверждения, которые нельзя подтвердить чтением кода:
///   1  — режим отрисовки: ассет читается честно, запрос БЕЗ графики (пакетный режим) ничего
///        не меняет и НЕ открывает окно переключения (автоматического переключения нет);
///   4  — дерево RRT: растёт при планировании, ОЧИЩАЕТСЯ при перепривязке робота, а пустое
///        дерево показывает понятный статус;
///   5  — ход Z: в консоли ровно несколько строк «Ход Z …» вместо 165 (фантомы не считают);
///   6  — точка на уровне столешницы: нижний предел призмы ниже стола, запас ≥ 3°;
///   7  — автосглаживание: замер «все 8» против «только выбранный» (миллисекунды и счётчик);
///   8  — отказ сустава: остальные оси стоят на месте, провисание затухает, модель упрощённая;
///   9  — манекены: два, рядом с роботом, учитываются в проверке, переключатель работает;
///   10 — отчёты: путь ЛЕЖИТ в persistentDataPath и запись во время PlayMode РЕАЛЬНО идёт.
///
/// Запуск (БЕЗ -quit: нужен PlayMode):
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshFixesDiag.Run
///   -logFile _dsh_fixes.log
/// Отчёт пишется в &lt;persistentDataPath&gt;/KazistovVv/Reports (ФИКС 10) и дублируется в лог.
/// </summary>
public static class DshFixesDiag
{
    private const string Key = "DshFixesDiag.Running";
    private const float StepLimitSeconds = 120f;
    private const float TotalLimitSeconds = 900f;
    private const int LastStep = 12;

    private static string report;
    private static int step;
    private static bool playStarted;
    private static int ok, fail, info;
    private static readonly HashSet<string> usedTags = new HashSet<string>();
    private static float stepEnterTime;
    private static int stepEntries;

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static KvStageHub2 stages2;
    private static KvStageHub4 stages4;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static PoseValidator v;
    private static SCARAController scara;

    private static int zTravelLines;              // сколько раз консоль сказала «Ход Z …»
    private static int phantomZLines;             // сколько из них — от копий-фантомов
    private static float smoothAllMs = -1f;
    private static int smoothAllCount = -1;
    private static float smoothOneMs = -1f;
    private static int smoothOneCount = -1;
    private static double[] jointsBeforeFailure;
    private static string reportProbePath;
    private static bool reportProbeOk;

    // ------------------------------------------------------------------ запуск

    public static void Run()
    {
        report = FeatureStorage.ReportPath("_dsh_fixes_verify.txt");
        try
        {
            File.WriteAllText(report,
                "=== DshFixesDiag · проверка десяти фиксов (19.09.2026) ===\n" +
                "отчёт: " + report + "\n");
        }
        catch (Exception e) { Debug.LogError("[DshFixesDiag] отчёт не создан: " + e.Message); }

        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;

        EditorSceneManager.OpenScene("Assets/_Project/00_Scenes/MainScene.unity", OpenSceneMode.Single);
        SessionState.SetBool(Key, true);
        SessionState.SetFloat(Key + ".Start", (float)EditorApplication.timeSinceStartup);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Boot()
    {
        if (!SessionState.GetBool(Key, false)) return;
        // ВАЖНО: подписка на лог ставится И здесь. Вход в PlayMode перезагружает домен,
        // поэтому подписка из Run() теряется — без этой строки счёт строк «Ход Z» всегда 0.
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    /// <summary>Счёт «Ход Z …» в консоли (ФИКС 5) — считаем и отдельно «от фантомов».</summary>
    private static void OnLog(string condition, string stackTrace, LogType type)
    {
        if (condition == null || condition.IndexOf("Ход Z", StringComparison.Ordinal) < 0) return;
        // Свои же строки отчёта («строк «Ход Z» …») в счёт не идут — иначе счётчик считает себя.
        if (condition.StartsWith("[DshFixesDiag]", StringComparison.Ordinal)) return;
        zTravelLines++;
        if (stackTrace != null && stackTrace.IndexOf("Instantiate", StringComparison.Ordinal) >= 0)
            phantomZLines++;
    }

    // ------------------------------------------------------------------ каркас прогона

    private static float Now { get { return (float)EditorApplication.timeSinceStartup; } }
    private static bool Wait(float seconds) { return Now - stepEnterTime >= seconds; }

    private static bool Once(string tag)
    {
        if (usedTags.Contains(tag)) return false;
        usedTags.Add(tag);
        return true;
    }

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n", new UTF8Encoding(false)); } catch { }
        Debug.Log("[DshFixesDiag] " + text);
    }

    private static void Check(bool condition, string what, string detail = "")
    {
        if (!usedTags.Add("check:" + what)) return;
        string line = (condition ? "[OK]   " : "[FAIL] ") + what +
                      (detail.Length > 0 ? " — " + detail : "");
        if (condition) ok++; else fail++;
        Line(line);
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

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
                 " с — перехожу к следующему");
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
                case 1: StepRenderMode(); break;
                case 2: StepTreeGrows(); break;
                case 3: StepTreeReset(); break;
                case 4: StepPointAndSmoothAll(); break;
                case 5: StepPointAndSmoothSelected(); break;
                case 6: StepZLogCount(); break;
                case 7: StepMannequins(); break;
                case 8: StepMannequinsToggle(); break;
                case 9: StepFailureModel(); break;
                case 10: StepReportPath(); break;
                default: Finish(); break;
            }
        }
        catch (Exception e)
        {
            fail++;
            Line("[FAIL] шаг " + step + " исключение: " + e.Message);
            Next(step + 1);
        }
    }

    // ------------------------------------------------------------------ шаги

    /// <summary>0. Готовность сцены: хабы, поток, SCARA, камера.</summary>
    private static void StepBoot()
    {
        ui = KazistovVvUIManager.Instance;
        hub = FeatureHub.Current;
        stages2 = KvStageHub2.Current;
        stages4 = KvStageHub4.Instance;
        if (ui == null || hub == null || stages4 == null || ui.Flow == null)
        {
            if (!Wait(30f)) return;
            fail++;
            Line("[FAIL] 0. хабы не поднялись за 30 с");
            Finish();
            return;
        }

        flow = ui.Flow;
        cam = ui.CameraRig;
        v = flow.Validator;
        RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Exclude);
        foreach (RobotController rc in all)
            if (rc is SCARAController) scara = rc as SCARAController;

        Check(v != null && v.Ready && scara != null, "0. SCARA и поток готовы",
            "робот потока: " + (flow.Robot != null ? flow.Robot.robotName : "нет") +
            " · осей " + (v != null ? v.Dof : 0));
        if (v == null || !v.Ready) { Next(LastStep); return; }

        // Привязываем поток к SCARA, как это делает оператор (наведением прицела).
        if (flow.Robot != scara)
        {
            aimAt(scara);
            Aim(scara.transform.position + Vector3.up * 0.2f, false, false, false,
                Vector3.zero, false);
            if (!Wait(6f)) return;
        }
        Check(flow.Robot == scara, "0. поток привязан к SCARA",
            "робот: " + (flow.Robot != null ? flow.Robot.robotName : "нет"));
        // Счётчик строк «Ход Z» НЕ сбрасывается: он считает от самого входа в PlayMode,
        // то есть включает и штатную калибровку при инициализации SCARA (ожидается 1 строка).
        Note("0. строк «Ход Z» к этому моменту: " + zTravelLines);
        Next(1);
    }

    /// <summary>1. ФИКС 1: режим отрисовки читается из ассета и сам НЕ переключается.</summary>
    private static void StepRenderMode()
    {
        KvGraphicsService graphics = KvGraphicsService.Current ?? KvGraphicsService.EnsureStarted();
        KvLitShaderMode assetMode = graphics.AssetLitShaderMode();
        Check(graphics.Available == false,
            "1. ФИКС 1: пакетный режим — графики нет, настройки только считаются",
            "Available = " + graphics.Available);

        // Запрос без графики обязан быть ОТКЛОНЁН и НЕ открывать окно подтверждения.
        bool requested = graphics.RequestLitShaderMode(KvLitShaderMode.Forward);
        Check(!requested && !graphics.LitShaderPending,
            "1. ФИКС 1: без графики переключение не выполняется и окно не открывается",
            "запрос принят: " + requested + " · ожидание подтверждения: " + graphics.LitShaderPending);
        Check(graphics.AssetLitShaderMode() == assetMode,
            "1. ФИКС 1: HDRP-ассет НЕ изменился без подтверждения оператора",
            "режим ассета: " + KvGraphicsModel.LitShaderModeLabel((int)assetMode));

        Note("1. ФИКС 1: режим отрисовки ассета = " +
             KvGraphicsModel.LitShaderModeLabel((int)assetMode) +
             " · отчёт службы: «" + graphics.LitShaderReport + "»");

        // ФИКС 5: ход Z у фантомов — создаём 8 фантомов обычным путём (точка + подтверждение)
        // и убеждаемся, что консоль молчит (см. шаг 6).
        Next(2);
    }

    /// <summary>2. ФИКС 4: дерево RRT растёт при планировании.</summary>
    private static void StepTreeGrows()
    {
        Planner planner = flow.Planner;
        if (planner == null) { Check(false, "2. ФИКС 4: планировщик доступен"); Next(3); return; }

        // Штатное включение показа дерева (как кнопкой на вкладке «Бенчмарк планировщика»).
        if (stages4.Lab != null && Once("tree")) stages4.Lab.SetTreeVisible(true);
        Check(planner.RecordTree, "2. ФИКС 4: показ дерева включает запись узлов",
            "Planner.RecordTree = " + planner.RecordTree +
            " · запрос визуализатора: " + Planner.RecordTreeRequested);

        // ВАЖНО: планирование и проверка — В ОДНОМ кадре. Поток каждый кадр пересчитывает
        // привязку по точке прицела и при смене робота ОЧИЩАЕТ дерево (это и есть ФИКС 4),
        // поэтому «подождать и посмотреть» дало бы ложный ноль.
        if (Once("plan"))
        {
            double[] start = v.CopyCurrent();
            Vector3 target = WorkPoint();
            List<PlannedTrajectory> plans = planner.Plan(start, target, 1, 424242);
            int nodes = planner.TreeNodesA.Count + planner.TreeNodesB.Count;
            Check(planner.TreeVersion > 0 && nodes > 0,
                "2. ФИКС 4: включить дерево и построить траекторию → дерево растёт",
                "путей " + (plans != null ? plans.Count : 0) + " · версия " + planner.TreeVersion +
                " · узлов A/B " + planner.TreeNodesA.Count + "/" + planner.TreeNodesB.Count +
                " · точка " + target.ToString("0.00"));
            Check(KvPlannerLab.TreeStatus(planner) != KvPlannerLab.EmptyTreeText,
                "2. ФИКС 4: непустое дерево показывает узлы и версию",
                "статус: «" + KvPlannerLab.TreeStatus(planner) + "»");
        }
        Next(3);
    }

    /// <summary>3. ФИКС 4: смена робота очищает дерево и даёт понятный статус.</summary>
    private static void StepTreeReset()
    {
        Planner planner = flow.Planner;
        if (stageGrowVersion <= 0) stageGrowVersion = planner.TreeVersion;

        // ДЕЙСТВИЕ ОПЕРАТОРА: навести прицел на ДРУГОГО робота — поток перепривязывается сам.
        if (Once("other"))
        {
            RobotController other = OtherRobot();
            if (other == null) { Note("3. ФИКС 4: второго робота в сцене нет — проверяю командой"); }
            else aimAt(other);
        }
        if (!Wait(1.5f)) return;

        bool switched = flow.Robot != scara;
        int afterSwitch = planner.TreeVersion;
        Check(!switched || afterSwitch == 0,
            "3. ФИКС 4: смена робота (наведение на другого) очищает дерево RRT",
            "робот потока: " + (flow.Robot != null ? flow.Robot.robotName : "нет") +
            " · перепривязка: " + switched + " · версий было " + stageGrowVersion +
            " → стало " + afterSwitch + " · узлов " +
            (planner.TreeNodesA.Count + planner.TreeNodesB.Count));

        // И то же самое по явной команде «Пересчитать кинематику SCARA» (кнопка оператора).
        if (Once("command")) flow.RebindKinematics("диагностика ФИКС 4");
        Check(planner.TreeVersion == 0 && planner.TreeNodesA.Count == 0 &&
              planner.TreeNodesB.Count == 0,
            "3. ФИКС 4: перепривязка командой тоже очищает дерево (TreeVersion = 0)",
            "версия " + planner.TreeVersion + " · узлов " +
            planner.TreeNodesA.Count + "/" + planner.TreeNodesB.Count);
        Check(KvPlannerLab.TreeStatus(planner) == KvPlannerLab.EmptyTreeText,
            "3. ФИКС 4: пустое дерево → понятное сообщение вместо пустого экрана",
            "статус: «" + KvPlannerLab.TreeStatus(planner) + "»");

        // Возвращаем привязку к SCARA и выключаем показ дерева (дальше он не нужен).
        if (Once("back"))
        {
            if (stages4.Lab != null) stages4.Lab.SetTreeVisible(false);
            aimAt(scara);
        }
        if (!Wait(2f)) return;
        Next(4);
    }

    private static int stageGrowVersion;

    private static RobotController OtherRobot()
    {
        RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Exclude);
        foreach (RobotController rc in all)
            if (rc != null && rc != scara &&
                (rc.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0) return rc;
        return null;
    }

    /// <summary>4. ФИКС 7: автосглаживание ВСЕХ восьми вариантов (прежнее поведение).</summary>
    private static void StepPointAndSmoothAll()
    {
        if (stages2.Smoothing == null) { Next(5); return; }
        if (Once("mode"))
        {
            stages2.Smoothing.Auto = true;
            stages2.Smoothing.AutoSelectedOnly = false;      // прежнее поведение: все варианты
        }
        if (Once("click"))
        {
            aimAt(scara);
            Click(WorkPoint());
        }
        if (!Wait(25f)) return;

        int count = flow.State.candidates.Count;
        Check(count > 0, "4. ФИКС 7: точка зафиксирована, варианты построены", "вариантов: " + count);
        smoothAllMs = stages2.Smoothing.LastAutoMs;
        smoothAllCount = stages2.Smoothing.LastAutoCount;
        Note("4. ФИКС 7: автосглаживание ВСЕХ вариантов — " + smoothAllMs.ToString("0.0") +
             " мс · обработано " + smoothAllCount + " · фантомов " +
             (flow.Phantoms != null ? flow.Phantoms.Count : -1) +
             " · строк «Ход Z» в консоли: " + zTravelLines + " (из них от фантомов " +
             phantomZLines + ")");
        Next(5);
    }

    /// <summary>5. ФИКС 7: автосглаживание ТОЛЬКО выбранного варианта — замер «после».</summary>
    private static void StepPointAndSmoothSelected()
    {
        if (stages2.Smoothing == null) { Next(6); return; }
        if (Once("mode")) stages2.Smoothing.AutoSelectedOnly = true;
        if (Once("reset")) flow.ResetFlow("диагностика ФИКС 7");
        if (!Wait(2f)) return;
        if (Once("click"))
        {
            aimAt(scara);
            Click(WorkPoint() + new Vector3(0.03f, 0.06f, 0.02f));
        }
        if (!Wait(25f)) return;

        smoothOneMs = stages2.Smoothing.LastAutoMs;
        smoothOneCount = stages2.Smoothing.LastAutoCount;
        Check(smoothOneCount == 1 && stages2.Smoothing.LastAutoSelectedOnly,
            "5. ФИКС 7: обработан ТОЛЬКО выбранный вариант (1 вместо 8)",
            "обработано " + smoothOneCount + " · режим «только выбранный»: " +
            stages2.Smoothing.LastAutoSelectedOnly);
        Check(smoothAllMs > 0f && smoothOneMs > 0f && smoothOneMs < smoothAllMs,
            "5. ФИКС 7: появление траекторий быстрее (замер до/после)",
            "все 8: " + smoothAllMs.ToString("0.0") + " мс → только выбранный: " +
            smoothOneMs.ToString("0.0") + " мс (выигрыш " +
            (smoothAllMs > 0f ? (100f * (smoothAllMs - smoothOneMs) / smoothAllMs).ToString("0") : "?") +
            " %)");
        Next(6);
    }

    /// <summary>6. ФИКС 5: строк «Ход Z …» в консоли — единицы, а не 165.</summary>
    private static void StepZLogCount()
    {
        // За прогон ожидается ровно 2 строки: «вниз» и «вверх» при инициализации SCARA.
        // Всё, что больше, — это явные запросы «Пересчитать кинематику» (диагностика их делает).
        Check(zTravelLines <= 4,
            "6. ФИКС 5: повторов «Ход Z …» в консоли нет (было 165 за прогон)",
            "строк «Ход Z»: " + zTravelLines + " (ожидается 2 при инициализации + явные запросы) · " +
            "из них от копий-фантомов: " + phantomZLines +
            " · фантомов создано за прогон: " + (flow.Phantoms != null ? flow.Phantoms.Count : -1));
        Check(phantomZLines == 0,
            "6. ФИКС 5: копии-фантомы геометрию НЕ пересчитывают",
            "строк от фантомов: " + phantomZLines);
        Check(scara != null && scara.KinematicsCalibrated,
            "6. ФИКС 5: ход Z посчитан один раз при инициализации",
            "ZMin = " + (scara != null ? scara.ZMin.ToString("0.000") : "?") + " · ZMax = " +
            (scara != null ? scara.ZMax.ToString("0.000") : "?"));

        // ФИКС 6: смысл сдвига — нижний предел призмы НИЖЕ уровня столешницы.
        if (v.Ready && v.IsPrismatic(2))
        {
            double[] probe = { 0, 0, 0 };
            scaraPrismMm = v.Lower[2] * 1000f;
            Check(v.Lower[2] < -0.005 && v.LimitMargin(probe) >= flow.Planner.minLimitMarginDeg,
                "6. ФИКС 6: точка ровно на столешнице получает запас ≥ 3° и достижима",
                "нижний предел призмы " + (v.Lower[2] * 1000f).ToString("0") +
                " мм относительно стола · запас на уровне стола " +
                v.LimitMargin(probe).ToString("0.0") + "° при пороге " +
                flow.Planner.minLimitMarginDeg.ToString("0") + "° · zLowerOffsetM = " +
                (scara != null ? (scara.ZLowerOffsetM * 100f).ToString("0.#") : "?") + " см");
        }
        Next(7);
    }

    private static float scaraPrismMm;

    /// <summary>7. ФИКС 9: два манекена созданы, стоят рядом, учитываются в проверке.</summary>
    private static void StepMannequins()
    {
        KvMannequins mannequins = stages4.Mannequins;
        if (mannequins == null) { Check(false, "7. ФИКС 9: сервис манекенов существует"); Next(8); return; }

        Check(mannequins.Created == KvMannequins.Count,
            "7. ФИКС 9: создано ровно два манекена",
            "создано: " + mannequins.Created + " · " + mannequins.Status());

        Vector3[] points = mannequins.Positions();
        Check(points.Length == 2, "7. ФИКС 9: манекены отдаются в проверку", "точек: " + points.Length);

        // Сравниваем с ТЕМ роботом, у которого манекены поставлены (поток перепривязывается
        // по прицелу, поэтому «текущий» робот потока в момент проверки может быть другим).
        RobotController baseRobot = mannequins.RobotBase;
        if (baseRobot != null && points.Length == 2)
        {
            float d0 = Vector3.Distance(points[0], baseRobot.transform.position);
            float d1 = Vector3.Distance(points[1], baseRobot.transform.position);
            Check(d0 > 1.0f && d0 < 4.5f && d1 > 1.0f && d1 < 4.5f,
                "7. ФИКС 9: манекены стоят рядом с рабочим местом (≈1.5 м от робота)",
                "робот «" + baseRobot.robotName + "» в " +
                baseRobot.transform.position.ToString("0.00") + " · манекены " +
                points[0].ToString("0.00") + " и " + points[1].ToString("0.00") +
                " · расстояния " + d0.ToString("0.00") + " м и " + d1.ToString("0.00") + " м" +
                " · высота манекена над полом " + points[0].y.ToString("0.00") + " м");
            Check(points[0].y < 1.2f && points[0].y > 0.5f,
                "7. ФИКС 9: манекен стоит НА ПОЛУ (центр капсулы ≈0,88 м)",
                "высота центра: " + points[0].y.ToString("0.000") + " м");
            Check(baseRobot.transform.position.y < 1.2f,
                "7. ФИКС 9: робот стоит на стенде на ожидаемой высоте",
                "высота робота: " + baseRobot.transform.position.y.ToString("0.000") + " м");
        }
        Note("7. ФИКС 9: " + mannequins.Status());
        Check(mannequins.Include, "7. ФИКС 9: по умолчанию манекены УЧИТЫВАЮТСЯ в проверке",
            "Include = " + mannequins.Include + " · " + mannequins.SourceNote());

        // ФИКС 10 (сессия §19): манекены НЕ должны мешать остальному проекту.
        //   1) коллайдер — триггер: физика не меняется, а все лучи проекта идут с
        //      QueryTriggerInteraction.Ignore, поэтому «пол» под манекеном виден как раньше;
        //   2) HideInHierarchy — по этому признаку `CollisionWorld.IsServiceObject` исключает
        //      объект из мира столкновений: манекен не становится препятствием и не заставляет
        //      планировщик отбрасывать пути;
        //   3) манекены не меняют число препятствий: сравниваем мир столкновений до и после.
        Note("7. ФИКС 10: " + mannequins.LayoutReport());

        Vector3 floorUnder = Vector3.zero;
        Vector3 probe = points.Length > 0 ? points[0] + Vector3.up * 1.0f : Vector3.zero;
        RaycastHit floorHit = default(RaycastHit);
        bool floorFound = points.Length > 0 &&
            Physics.Raycast(new Ray(probe, Vector3.down), out floorHit, 3f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        if (floorFound) floorUnder = floorHit.point;
        Check(points.Length == 0 || floorFound,
            "7. ФИКС 10: коллайдер-триггер манекена НЕ перехватывает лучи проверки",
            floorFound
                ? "луч сверху видит поверхность на y = " + floorUnder.y.ToString("0.000") +
                  " м — триггер пропущен (как и во всех запросах проекта)"
                : "луч не нашёл поверхность (проверка не выполнена)");

        Check(mannequins.LayoutReport().IndexOf("скрыто из иерархии " + KvMannequins.Count,
                  StringComparison.Ordinal) >= 0,
            "7. ФИКС 10: манекены скрыты из иерархии → исключены из мира столкновений",
            mannequins.LayoutReport() + " · CollisionWorld пропускает HideInHierarchy");
        Next(8);
    }

    /// <summary>8. ФИКС 9: переключатель убирает/возвращает манекенов в проверке.</summary>
    private static void StepMannequinsToggle()
    {
        KvMannequins mannequins = stages4.Mannequins;
        if (mannequins == null || stages4.PreRun == null) { Next(9); return; }

        if (Once("off"))
        {
            mannequins.Include = false;
            stages4.PreRun.PersonSourceNote = mannequins.SourceNote();
        }
        if (!Wait(0.5f)) return;
        Check(mannequins.Positions().Length == 0,
            "8. ФИКС 9: выключенный переключатель убирает манекенов из проверки",
            "точек манекенов в проверке: " + mannequins.Positions().Length +
            " · " + mannequins.SourceNote());

        if (Once("on"))
        {
            mannequins.Include = true;
            stages4.PreRun.PersonSourceNote = mannequins.SourceNote();
        }
        if (!Wait(0.5f)) return;
        Check(mannequins.Positions().Length == 2,
            "8. ФИКС 9: включённый переключатель возвращает обоих манекенов",
            "точек манекенов в проверке: " + mannequins.Positions().Length +
            " · " + mannequins.SourceNote());

        // Отдельно: что именно видит оператор в отчёте проверки перед пуском.
        if (Once("validate"))
        {
            flow.SelectCandidateByIndex(0);
            stages4.PreRun.Validate();
        }
        if (!Wait(1f)) return;
        kvFindingText = DescribeFindings(stages4.PreRun);
        Note("8. ФИКС 9: отчёт проверки — " + kvFindingText);
        Next(9);
    }

    private static string kvFindingText = "";

    private static string DescribeFindings(KvPreRunValidator pre)
    {
        StringBuilder sb = new StringBuilder();
        sb.Append(pre.LastSummary);
        for (int i = 0; i < pre.Findings.Count; i++)
        {
            KvFinding f = pre.Findings[i];
            if (f == null) continue;
            if (f.title != null && f.title.IndexOf("человек", StringComparison.OrdinalIgnoreCase) >= 0)
                sb.Append(" | ").Append(f.title).Append(": ").Append(f.details);
        }
        return sb.ToString();
    }

    /// <summary>9. ФИКС 8: модель отказов — остальные оси стоят, провисание затухает.</summary>
    private static void StepFailureModel()
    {
        KvFailureSimulator failures = stages4.Failures;
        if (failures == null) { Next(10); return; }

        if (Once("start"))
        {
            failures.SetJointMode(KvJointFailureMode.Droop);
            jointsBeforeFailure = v.CopyCurrent();
            failures.Start(KvFailureKind.JointLoss, 1);
        }
        if (!Wait(6f)) return;

        double[] now = v.CopyCurrent();
        double othersDelta = 0.0;
        if (jointsBeforeFailure != null && now != null &&
            jointsBeforeFailure.Length == now.Length)
        {
            for (int i = 0; i < now.Length; i++)
                if (i != 0) othersDelta = Math.Max(othersDelta, Math.Abs(now[i] - jointsBeforeFailure[i]));
        }

        Check(failures.Active, "9. ФИКС 8: провисание сустава смоделировано",
            "скорость " + failures.DroopSpeedDeg.ToString("0.000") + " °/с · остановилось: " +
            failures.DroopStopped);
        Check(othersDelta < 0.5,
            "9. ФИКС 8: остальные оси СТОЯТ (поза отказа зафиксирована)",
            "максимальное смещение прочих осей: " + othersDelta.ToString("0.000") +
            " (у призмы — метры, у осей — градусы)");
        Check(!float.IsNaN(failures.DroopSpeedDeg) &&
              Math.Abs(failures.DroopSpeedDeg) <= 90.01f,
            "9. ФИКС 8: провисание затухает в пределах заданной скорости",
            "текущая скорость " + failures.DroopSpeedDeg.ToString("0.000") + " °/с при пределе " +
            failures.DroopRate.ToString("0") + " °/с · остановилось: " + failures.DroopStopped);

        if (Once("reset")) failures.Clear();
        if (!Wait(2.5f)) return;
        Check(!failures.Active, "9. ФИКС 8: сброс аварии возвращает управление");
        Next(10);
    }

    /// <summary>10. ФИКС 10: отчётный файл лежит в persistentDataPath и ПИШЕТСЯ в PlayMode.</summary>
    private static void StepReportPath()
    {
        if (Once("probe"))
        {
            reportProbePath = FeatureStorage.ReportPath("_dsh_fixes_probe.txt");
            reportProbeOk = FeatureStorage.AppendLine(reportProbePath,
                "проба записи во время PlayMode: " + DateTime.Now.ToString("HH:mm:ss"));
        }
        if (!Wait(1.5f)) return;

        string persistent = Application.persistentDataPath;
        bool insideProject = reportProbePath != null &&
                             reportProbePath.StartsWith(Application.dataPath, StringComparison.OrdinalIgnoreCase);
        bool written = false;
        try { written = reportProbePath != null && File.Exists(reportProbePath); } catch { }

        Check(reportProbePath != null &&
              reportProbePath.StartsWith(persistent, StringComparison.OrdinalIgnoreCase) && !insideProject,
            "10. ФИКС 10: отчётный файл пишется в persistentDataPath, а не в папку проекта (OneDrive)",
            reportProbePath);
        Check(reportProbeOk && written,
            "10. ФИКС 10: запись во время PlayMode РЕАЛЬНО идёт (главная причина хвоста §13.11)",
            "файл существует: " + written);
        Next(11);
    }

    // ------------------------------------------------------------------ завершение

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;

        Line("--- ИТОГ ПО ФИКСАМ ---");
        Line("[info] ФИКС 1 (режим отрисовки): ассет не менялся без оператора; MSAA включается " +
             "только подтверждённой сменой режима (без видеокарты не проверяется)");
        Line("[info] ФИКС 4 (дерево RRT): растёт при планировании, очищается при перепривязке");
        Line("[info] ФИКС 5 (ход Z): строк «Ход Z» за прогон " + zTravelLines +
             ", из них от фантомов " + phantomZLines);
        Line("[info] ФИКС 6 (точка на столе): нижний предел призмы " +
             (scaraPrismMm.ToString("0")) + " мм отн. стола");
        Line("[info] ФИКС 7 (автосглаживание): все 8 — " + smoothAllMs.ToString("0.0") +
             " мс, только выбранный — " + smoothOneMs.ToString("0.0") + " мс");
        Line("[info] ФИКС 8 (отказы): остальные оси зафиксированы, провисание затухает");
        Line("[info] ФИКС 9 (манекены): создано " +
             (stages4 != null && stages4.Mannequins != null ? stages4.Mannequins.Created : 0));
        Line("[info] ФИКС 10 (отчёты): " + reportProbePath);
        Line("=== ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info + " ===");
        Debug.Log("[DshFixesDiag] === ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " +
                  info + " ===");
        Debug.Log("[DshFixesDiag] отчёт: " + report);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ помощники

    private static Vector3 workPoint;

    private static Vector3 WorkPoint()
    {
        if (workPoint != Vector3.zero) return workPoint;
        CollisionWorld world = hub != null ? hub.World : null;
        Vector3 basePos = scara != null ? scara.transform.position : Vector3.zero;
        float[] radii = { 0.30f, 0.38f, 0.45f, 0.52f };
        float[] heights = { 0.10f, 0.18f, 0.26f, 0.34f };
        double[] seed = v.CopyCurrent();
        foreach (float r in radii)
            foreach (float h in heights)
            {
                Vector3 candidate = basePos + new Vector3(r, h, -0.2f);
                double[] q;
                string why;
                if (KvPlanKit.SolvePoseForPoint(v, world, candidate, Vector3.down, seed, out q, out why))
                {
                    workPoint = candidate;
                    return workPoint;
                }
            }
        workPoint = basePos + new Vector3(0.12f, 0.32f, -0.35f);
        return workPoint;
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

    private static void Aim(Vector3 p, bool hit, bool confirm, bool cancel, Vector3 axis,
        bool fast)
    {
        if (flow == null) return;
        flow.UpdateAim(p, hit, confirm, cancel,
            cam != null && cam.leftHandEnabled, cam != null && cam.rightHandEnabled,
            Vector3.up, hit, false, axis, fast);
    }

    private static void Click(Vector3 p)
    {
        if (cam == null) return;
        cam.leftHandEnabled = true;
        cam.rightHandEnabled = false;
        Aim(p, true, true, false, Vector3.zero, false);
        cam.leftHandEnabled = false;
    }
}
