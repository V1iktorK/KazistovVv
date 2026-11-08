using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;
using TrajectoryCore;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (в копию пользователя НЕ переносится).
/// СЕССИЯ «8 ТРАЕКТОРИЙ ДЛЯ SCARA» (15.09.2026): проверяется РОВНО то, что просило ТЗ —
/// сколько УНИКАЛЬНЫХ траекторий (форма пути отличается, критерий `IsSamePath`) даёт SCARA
/// после правки бюджета попыток (`distinctAttemptCap`), вариаций обхода (`detourVariants`)
/// и честного отчёта «Получено N из 8».
///
/// Прогон: НЕСКОЛЬКО точек рабочей зоны SCARA по очереди. По каждой:
///   1) точка ставится красным лазером (та же операция, что ЛКМ оператора — `UpdateAim`);
///   2) ждём окончания генерации (`flow.Generating == false`);
///   3) считаем УНИКАЛЬНЫЕ пути ДВАЖДЫ: счётчиком самого потока (`UniqueVariantCount`) и
///      НЕЗАВИСИМО — попарным вызовом приватного `IsSamePath` через рефлексию
///      (если два способа расходятся — это находка, а не «зелёный отчёт»);
///   4) в отчёт пишутся попытки/дубликаты/отказы/ветви IK и подписи найденных вариантов.
///
/// Запуск (БЕЗ -quit: нужен PlayMode):
///   Unity.exe -batchmode -nographics -projectPath "&lt;проект&gt;" -executeMethod DshScaraEightDiag.Run
///   -logFile _dsh_scara8.log
/// Отчёт: KavistovVv/_dsh_scara8_verify.txt (пишется сразу — при входе в PlayMode домен
/// перезагружается, буфер в памяти потерялся бы).
/// </summary>
public static class DshScaraEightDiag
{
    private const string Key = "DshScaraEightDiag.Running";
    private const float PointLimitSeconds = 300f;    // одна точка: ожидание генерации
    private const float TotalLimitSeconds = 2400f;   // весь прогон
    private const int MaxPoints = 5;
    private const int Want = 8;                      // ТЗ: ровно 8 уникальных траекторий

    private static string report;
    private static readonly StringBuilder log = new StringBuilder();

    public static void Run()
    {
        // ВАЖНО (§12.10, ФИКС 10): файл ВНУТРИ проекта (OneDrive) во время PlayMode не
        // дописывается — прогон DshScaraDiag это уже показал (отчёт оставался из одной строки).
        // Отчёт пишется в общий каталог отчётов <persistentDataPath>/KazistovVv/Reports —
        // это локальный каталог вне синхронизации (раньше писали в профиль пользователя).
        // Строки всё равно дублируются в лог Unity.
        report = FeatureStorage.ReportPath("_dsh_scara8_verify.txt");
        try
        {
            File.WriteAllText(report,
                "=== DshScaraEightDiag · 8 уникальных траекторий для SCARA ===\n");
        }
        catch (Exception e) { Debug.LogError("[DshScaraEightDiag] отчёт не создан: " + e.Message); }

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
        Application.logMessageReceived -= OnLog;
        Application.logMessageReceived += OnLog;
    }

    // ------------------------------------------------------------------ состояние прогона

    private static int step;
    private static bool playStarted;
    private static int ok, fail, info;
    private static float stepEnterTime;
    private static float runStart;

    private static KazistovVvUIManager ui;
    private static FeatureHub hub;
    private static TrajectoryFlowController flow;
    private static FreeFlyCameraController cam;
    private static PoseValidator v;
    private static SCARAController scara;
    private static RobotController sixAxis;

    private static readonly List<Vector3> points = new List<Vector3>();
    private static int pointIndex;
    private static bool clickSent;
    private static bool wasGenerating;
    private static bool honestyPrepared;
    private static int bestUnique;

    // Регрессия по 6-осевому роботу: его рабочая зона, две фазы настроек (старые/новые) и счётчики.
    private static readonly List<Vector3> robotPoints = new List<Vector3>();
    private static bool rebound;
    private static bool resetSent;
    private static int robotRuns;
    private static int preparedRun = -1;

    // Режимы замера: обычная проверка SCARA · проверка честности отчёта ·
    // БАЗА (старые значения настроек, только справка) · регрессия 6-осевого (проверка).
    private const int ModeScara = 0, ModeHonesty = 1, ModeBaseline = 2, ModeRegression = 3;

    private static void Line(string text)
    {
        try { File.AppendAllText(report, text + "\n"); } catch { }
        Debug.Log("[DshScaraEightDiag] " + text);
    }

    private static void Check(bool condition, string what, string detail = "")
    {
        string line = (condition ? "[OK]   " : "[FAIL] ") + what +
                      (detail.Length > 0 ? " — " + detail : "");
        if (condition) ok++; else fail++;
        Line(line);
    }

    private static void Note(string what) { info++; Line("[info] " + what); }

    private static float Now { get { return (float)EditorApplication.timeSinceStartup; } }

    private static void Next(int s)
    {
        if (s != step) Line("→ шаг " + s);
        step = s;
        stepEnterTime = Now;
    }

    private static void OnLog(string message, string stack, LogType type)
    {
        if (message == null) return;
        if (message.Contains("[Variants]") || message.Contains("[Flow] Получено") ||
            message.Contains("[Flow] Вариантов"))
        {
            if (log.Length < 60000) log.AppendLine("   " + message);
        }
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            if (playStarted) { Line("PlayMode завершился неожиданно"); Finish(); }
            return;
        }
        playStarted = true;
        if (stepEnterTime <= 0f) { stepEnterTime = Now; runStart = Now; }

        if (Now - runStart > TotalLimitSeconds)
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
                case 1: StepPoint(); break;
                case 2: StepHonesty(); break;
                case 3: StepRobotRegression(); break;
                case 4: StepSummary(); break;
            }
        }
        catch (Exception e)
        {
            fail++;
            Line("[FAIL] ИСКЛЮЧЕНИЕ на шаге " + step + ": " + e.GetType().Name + " " + e.Message +
                 "\n" + e.StackTrace);
            Next(step + 1);      // именно СЛЕДУЮЩИЙ шаг: иначе шаг 2 зациклился бы на себе
        }
    }

    // ------------------------------------------------------------------ шаги

    private static void StepBoot()
    {
        ui = KazistovVvUIManager.Instance;
        if (ui == null || ui.Flow == null) return;
        flow = ui.Flow;
        hub = FeatureHub.Current;
        cam = ui.CameraRig;

        RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>(
            FindObjectsInactive.Include);
        foreach (RobotController rc in all)
        {
            if (rc is SCARAController) { scara = (SCARAController)rc; continue; }
            if (sixAxis == null) sixAxis = rc;      // 6-осевой — для проверки РЕГРЕССИИ
        }

        Check(scara != null, "0. SCARA найдена в сцене");
        if (scara == null) { Next(2); return; }

        // Привязка потока к SCARA — наведением камеры (как в прошлых прогонах), затем кадр
        // с прицелом на робота (робот в потоке выбирается ПО ТОЧКЕ прицела).
        if (flow.Robot != scara)
        {
            aimAt(scara);
            Aim(scara.transform.position + Vector3.up * 0.2f, false, false, false, false, Vector3.zero);
            if (Now - stepEnterTime < 20f) return;
        }

        v = flow.Validator;
        Check(v != null && v.Ready && flow.Robot == scara, "0. поток привязан к SCARA",
            "робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " +
            (v != null ? v.Dof : 0));
        if (v == null || !v.Ready) { Next(2); return; }
        Check(v.Dof == 3, "0. у SCARA три оси (J1, J2, Z) — это и есть причина малой размерности",
            "осей: " + v.Dof);

        // Настройки генерации: подтверждаем, что прогон идёт по НОВЫМ значениям бюджета.
        FieldInfo capField = typeof(TrajectoryFlowController).GetField("distinctAttemptCap");
        FieldInfo detourField = typeof(TrajectoryFlowController).GetField("detourVariants");
        object capValue = capField != null ? capField.GetValue(flow) : null;
        object detourValue = detourField != null ? detourField.GetValue(flow) : null;
        Line("[info] настройки генерации: distinctAttemptCap=" + (capValue ?? "?") +
             " · detourVariants=" + (detourValue ?? "?") +
             " · distinctBudgetSeconds=" + GetField(flow, "distinctBudgetSeconds") +
             " · trajectoryCount=" + flow.PlanTargetCount);
        Check(capValue is int && (int)capValue >= 128,
            "0. бюджет поиска уникальных увеличен с запасом",
            "distinctAttemptCap = " + (capValue ?? "?"));
        Check(detourValue is int && (int)detourValue >= 8,
            "0. вариантов обхода увеличено с запасом",
            "detourVariants = " + (detourValue ?? "?"));

        // Точки рабочей зоны SCARA: кольцо вокруг базы × высоты (как в прошлых прогонах).
        float[] radii = { 0.30f, 0.40f, 0.50f, 0.26f, 0.58f };
        float[] heights = { 0.16f, 0.24f, 0.12f, 0.32f, 0.20f };
        CollisionWorld world = hub != null ? hub.World : null;
        double[] seed = v.CopyCurrent();
        for (int i = 0; i < radii.Length && points.Count < MaxPoints; i++)
        {
            Vector3 candidate = scara.transform.position + new Vector3(radii[i], heights[i], -0.2f);
            double[] q;
            string why;
            if (KvPlanKit.SolvePoseForPoint(v, world, candidate, Vector3.down, seed, out q, out why))
                points.Add(candidate);
        }
        Check(points.Count > 0, "0. найдены достижимые точки рабочей зоны SCARA",
            "точек: " + points.Count + " · первая: " + (points.Count > 0 ? Fmt(points[0]) : "—"));
        if (points.Count == 0) { Next(2); return; }

        Next(1);
    }

    /// <summary>Одна точка: клик → ожидание генерации → подсчёт уникальных путей.</summary>
    private static void StepPoint()
    {
        if (pointIndex >= points.Count) { Next(2); return; }
        RunPoint(points[pointIndex], "· точка " + (pointIndex + 1) + "/" + points.Count + " " +
                                     Fmt(points[pointIndex]), ModeScara);
    }

    /// <summary>
    /// ПРОВЕРКА ЧЕСТНОСТИ ОТЧЁТА (ТЗ п. 4: «если всё равно меньше 8 — честно писать
    /// «Получено N из 8, причина — дубликаты RRT / малая рабочая зона». Не скрывать»).
    ///
    /// Ситуацию «уникальных меньше 8» создаём ШТАТНЫМИ настройками, без правок логики:
    ///   detourVariants = 0 — выключить обходы (буквально «только конфигурации IK + seed'ы»,
    ///                       ровно так, как было ДО этой сессии, — тогда пути вырождаются в прямые);
    ///   distinctAttemptCap = 4 — исчерпать бюджет поиска уникальных почти сразу.
    /// Проверяем: набор всё равно добит до 8 «колбасок» и 8 фантомов, НО оператору сказано
    /// «Получено N из 8 уникальных … добито похожими» — то есть неполнота НЕ скрыта.
    /// </summary>
    private static void StepHonesty()
    {
        if (flow == null || points.Count == 0 || v == null || !v.Ready) { Next(3); return; }
        if (honestyPrepared && !clickSent) { Next(3); return; }

        if (!honestyPrepared)
        {
            SetField(flow, "detourVariants", 0);          // обходы выключены
            SetField(flow, "distinctAttemptCap", 4);      // бюджет уникальных исчерпан сразу
            SetField(flow, "distinctBudgetSeconds", 0f);  // и по времени тоже (0 = выключено)
            honestyPrepared = true;
            Line("· ПРОВЕРКА ЧЕСТНОСТИ: detourVariants=0, distinctAttemptCap=4 (штатные ручки)");
        }

        // Точка, на которой при выключенных обходах пути совпадают (в прогоне 15.09.2026
        // здесь было 4 уникальных из 8 при 324 отброшенных дубликатах).
        Vector3 point = points.Count > 1 ? points[1] : points[0];
        // Лог потока обнуляем ПЕРЕД этой точкой: проверки ниже должны опираться на её
        // собственные строки, а не на сообщения предыдущих точек.
        if (!clickSent) log.Clear();
        RunPoint(point, "· точка честности " + Fmt(point), ModeHonesty);
    }

    /// <summary>
    /// Прогон одной точки: клик красным лазером → ожидание генерации → замер.
    /// Вызывается каждый кадр, пока точка не обработана (клик и ожидание — разные кадры).
    /// </summary>
    private static void RunPoint(Vector3 point, string label, int mode)
    {
        if (!clickSent)
        {
            // Чистое состояние: Esc (сброс), затем ЛКМ красным лазером по точке.
            aimAt(flow != null ? flow.Robot : scara);
            Aim(Vector3.zero, false, false, true, false, Vector3.zero);      // Esc
            Aim(Vector3.zero, false, false, false, false, Vector3.zero);
            Line(label);
            Click(point);
            clickSent = true;
            stepEnterTime = Now;
            wasGenerating = true;
            return;
        }

        // Держим прицел на точке (лог/оракул), пока идёт просчёт.
        Aim(point, true, false, false, false, Vector3.zero);

        bool generating = flow.Generating;
        if (generating) wasGenerating = true;

        bool finished = !generating && wasGenerating && flow.State.candidates.Count > 0;
        bool timeout = Now - stepEnterTime > PointLimitSeconds;
        if (finished || timeout)
        {
            Measure(point, timeout, label, mode);
            clickSent = false;
            if (mode == ModeScara) { pointIndex++; StepPoint(); }
            else if (mode == ModeHonesty) Next(3);
            else { robotRuns++; StepRobotRegression(); }
        }
    }

    /// <summary>Замер результата по точке: два независимых подсчёта уникальных путей.</summary>
    private static void Measure(Vector3 point, bool timeout, string label, int mode)
    {
        int shown = flow.State.candidates.Count;
        int reported = flow.UniqueVariantCount;
        int padded = flow.PaddedVariantCount;
        // ВАЖНО: PlanStartTime — это Time.realtimeSinceStartup (от старта PlayMode), а не
        // EditorApplication.timeSinceStartup (от старта редактора) — складывать их нельзя.
        float elapsed = Time.realtimeSinceStartup - flow.PlanStartTime;

        // НЕЗАВИСИМАЯ проверка уникальности: попарно тем же критерием, что и поток
        // (приватный static IsSamePath) — по планам показанных кандидатов.
        List<PlannedTrajectory> plans = new List<PlannedTrajectory>();
        for (int i = 0; i < flow.State.candidates.Count; i++)
            if (flow.State.candidates[i] != null && flow.State.candidates[i].plan != null)
                plans.Add(flow.State.candidates[i].plan);
        int independent = CountUnique(plans);
        bestUnique = Mathf.Max(bestUnique, independent);

        Line("   показано: " + shown + " · уникальных (счётчик потока): " + reported +
             " · уникальных (независимый пересчёт IsSamePath): " + independent +
             " · добито похожими: " + padded +
             " · попыток: " + flow.PlanAttempts + " · дубликатов отброшено: " +
             flow.DuplicatesFiltered + " · отказов: " + flow.PlanFailures +
             " · ветвей IK: " + flow.IkBranchesFound + " (годных " + flow.IkBranchesUsable + ")" +
             " · время: " + elapsed.ToString("0.00") + " с");
        Line("   ветви IK: " + flow.PlannerBranchInfo);
        for (int i = 0; i < flow.State.candidates.Count; i++)
        {
            TrajectoryCandidate c = flow.State.candidates[i];
            if (c == null || c.plan == null) continue;
            Line("   вариант " + (i + 1) + ": " + (string.IsNullOrEmpty(c.plan.BranchTag) ? "—" : c.plan.BranchTag) +
                 " · длина " + c.plan.Length.ToString("0.000") +
                 " · время " + c.plan.Time.ToString("0.000") + " с" +
                 " · кривизна " + c.plan.Curvature.ToString("0.00") + "°" +
                 (c.safe ? "" : " · НЕ прошла SafetyGate: " + c.why));
        }

        string where = label + ": ";
        if (timeout)
        {
            fail++;
            Line("[FAIL] " + where + "генерация не завершилась за " +
                 PointLimitSeconds.ToString("0") + " с");
            return;
        }
        if (independent != reported)
        {
            fail++;
            Line("[FAIL] " + where + "счётчик потока (" + reported +
                 ") расходится с независимым пересчётом (" + independent + ")");
        }

        if (mode == ModeHonesty)
        {
            // Ожидаем ЧЕСТНЫЙ отчёт о нехватке уникальных (и всё равно 8 «колбасок» = добивка).
            string said = log.ToString();
            Check(shown == Want, where + "набор всё равно добит до 8 траекторий (8 фантомов)",
                "показано: " + shown);
            Check(independent < Want, where + "уникальных действительно меньше 8 (условие проверки)",
                "уникальных: " + independent + " из " + Want);
            Check(padded > 0, where + "нехватка добита похожими путями и это учтено",
                "добито похожими: " + padded);
            Check(said.Contains("Получено ") && said.Contains("из 8 уникальных"),
                where + "оператору ЧЕСТНО сказано «Получено N из 8 уникальных»",
                "строка потока: " + FirstLine(said, "[Flow] Получено"));
            Check(FirstLine(said, "[Flow] Получено").Contains("добито похожими"),
                where + "оператору сказано и про добивку похожими (в ТОЙ ЖЕ строке)",
                "строка потока: " + FirstLine(said, "[Flow] Получено"));
            Check(FirstLine(said, "ПРИЧИНА").Contains("дубликаты") ||
                  FirstLine(said, "ПРИЧИНА").Contains("малая рабочая зона"),
                where + "названа ПРИЧИНА (дубликаты RRT / малая рабочая зона)",
                "строка отчёта: " + FirstLine(said, "ПРИЧИНА"));
            log.Clear();
            SetField(flow, "detourVariants", 12);          // вернуть штатные настройки
            SetField(flow, "distinctAttemptCap", 320);
            SetField(flow, "distinctBudgetSeconds", 30f);
            return;
        }

        if (mode == ModeBaseline)
        {
            // БАЗА (старые настройки): только справка, без вердикта — это замер «как было».
            Note(where + "СТАРЫЕ настройки: показано " + shown + " · уникальных " + independent +
                 " из " + Want + " · добито похожими: " + padded + " · попыток: " + flow.PlanAttempts +
                 " · дубликатов: " + flow.DuplicatesFiltered);
            return;
        }

        Check(shown == Want, where + "РОВНО 8 траекторий показано",
            "показано: " + shown);
        Check(independent == Want, where + "РОВНО 8 УНИКАЛЬНЫХ траекторий (IsSamePath)",
            "уникальных: " + independent + (padded > 0 ? " · добито похожими: " + padded : "") +
            (independent < Want ? " · причина: " + Reason() : ""));
        Check(padded == 0, where + "все 8 путей уникальны — «добивки» похожими не потребовалось",
            padded == 0 ? "добито похожими: 0"
                        : "добито похожими: " + padded + " (в отчёте это видно оператору)");
    }

    /// <summary>Первая строка лога, содержащая подстроку (для отчёта о честности).</summary>
    private static string FirstLine(string text, string needle)
    {
        if (string.IsNullOrEmpty(text)) return "—";
        string[] lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
            if (lines[i].Contains(needle)) return lines[i].Trim();
        return "не найдено";
    }

    private static string Reason()
    {
        if (flow.IkBranchesFound == 0) return "решатель IK не дал ветвей — малая рабочая зона";
        if (flow.IkBranchesUsable == 0) return "ветви IK не прошли лимиты — малая рабочая зона";
        if (flow.DuplicatesFiltered > 0 && flow.PlanFailures > 0)
            return "дубликаты RRT + отказы планировщика";
        if (flow.DuplicatesFiltered > 0) return "дубликаты RRT (IsSamePath)";
        if (flow.PlanFailures > 0) return "отказы планировщика (RRT без пути)";
        return "малая рабочая зона";
    }

    private static void StepSummary()
    {
        Line("--- ИТОГ: 8 УНИКАЛЬНЫХ ТРАЕКТОРИЙ ДЛЯ SCARA ---");
        Line("[info] лучший результат: " + bestUnique + " из " + Want + " уникальных");
        if (log.Length > 0)
        {
            Line("--- строки потока и отчёт [Variants] ---");
            Line(log.ToString().TrimEnd());
        }
        Line("=== ИТОГ: [OK] " + ok + " · [FAIL] " + fail + " · [info] " + info + " ===");
        Finish();
    }

    /// <summary>
    /// РЕГРЕССИЯ ПО 6-ОСЕВОМУ РОБОТУ (ограничение ТЗ: «не трогай 6-осевого робота»).
    /// Код робота не менялся, но поток «8 траекторий» — ОБЩИЙ для обоих роботов, поэтому
    /// проверяем, что правки бюджета/вариаций не ухудшили его результат. Каждая точка
    /// считается ДВАЖДЫ: со СТАРЫМИ настройками (48 попыток, 1 обход — как было до сессии)
    /// и с НОВЫМИ (320, 12). База пишется справкой, новый режим проверяется как обычно,
    /// поэтому «было / стало» видно прямо в отчёте.
    /// </summary>
    private static void StepRobotRegression()
    {
        if (flow == null || sixAxis == null || v == null || !v.Ready) { Next(4); return; }

        if (!rebound)
        {
            // ВАЖНО: `ResolveSelectedRobot` держит ТЕКУЩЕГО робота, пока поток не в Idle
            // (`if (robot != null && state.phase != FlowState.Idle) return robot`), поэтому
            // сначала Esc (сброс состояния), и только потом наведение на 6-осевого робота.
            if (!resetSent)
            {
                resetSent = true;
                stepEnterTime = Now;
                Aim(Vector3.zero, false, false, true, false, Vector3.zero);     // Esc
                return;
            }
            aimAt(sixAxis);
            Aim(sixAxis.transform.position + Vector3.up * 0.2f, false, false, false, false, Vector3.zero);
            if (flow.Robot != sixAxis && Now - stepEnterTime < 20f) return;
            rebound = true;
            v = flow.Validator;
            Check(v != null && v.Ready && flow.Robot == sixAxis,
                "3. поток перепривязан к 6-осевому роботу",
                "робот: " + (flow.Robot != null ? flow.Robot.robotName : "?") + " · осей " +
                (v != null ? v.Dof : 0));
            if (v == null || !v.Ready) { Next(4); return; }

            // Точки его рабочей зоны (тот же подбор через оракул, что и для SCARA).
            float[] radii = { 0.40f, 0.50f, 0.55f };
            float[] heights = { 0.25f, 0.35f, 0.45f };
            CollisionWorld world = hub != null ? hub.World : null;
            double[] seed = v.CopyCurrent();
            for (int i = 0; i < radii.Length && robotPoints.Count < 2; i++)
            {
                Vector3 candidate = sixAxis.transform.position + new Vector3(radii[i], heights[i], -0.2f);
                double[] q;
                string why;
                if (KvPlanKit.SolvePoseForPoint(v, world, candidate, Vector3.down, seed, out q, out why))
                    robotPoints.Add(candidate);
            }
            Line("· 6-осевой робот: достижимых точек найдено " + robotPoints.Count);
            if (robotPoints.Count == 0) { Next(4); return; }
        }

        int perPoint = Mathf.Max(1, robotPoints.Count);
        if (robotRuns >= perPoint * 2) { Next(4); return; }
        int phase = robotRuns / perPoint;               // 0 — старые настройки, 1 — новые
        int index = robotRuns % perPoint;

        // Настройки ставим ДО клика этой точки: фаза «до сессии» — те же значения, что были
        // в коде (48 попыток, булев `detourVariants` = один обход, бюджета по времени не было).
        if (!clickSent && preparedRun != robotRuns)
        {
            preparedRun = robotRuns;
            if (phase == 0)
            {
                SetField(flow, "distinctAttemptCap", 48);
                SetField(flow, "detourVariants", 1);
                SetField(flow, "distinctBudgetSeconds", 0f);
            }
            else
            {
                SetField(flow, "distinctAttemptCap", 320);
                SetField(flow, "detourVariants", 12);
                SetField(flow, "distinctBudgetSeconds", 30f);
            }
        }

        RunPoint(robotPoints[index], "· 6-осевой (" + (phase == 0 ? "СТАРЫЕ 48/1" : "НОВЫЕ 320/12") +
                                     "), точка " + (index + 1) + "/" + perPoint + " " +
                                     Fmt(robotPoints[index]),
                 phase == 0 ? ModeBaseline : ModeRegression);
    }

    private static void Finish()
    {
        EditorApplication.update -= Tick;
        Application.logMessageReceived -= OnLog;
        SessionState.SetBool(Key, false);
        try { File.AppendAllText(report, "=== конец прогона ===\n"); } catch { }
        Debug.Log("[DshScaraEightDiag] отчёт: " + report + " · [OK] " + ok + " [FAIL] " + fail);
        EditorApplication.Exit(fail == 0 ? 0 : 1);
    }

    // ------------------------------------------------------------------ помощники

    private static object GetField(object target, string name)
    {
        if (target == null) return null;
        FieldInfo f = target.GetType().GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        return f != null ? f.GetValue(target) : null;
    }

    /// <summary>Записать поле потока (нужно проверке честности: она меняет ШТАТНЫЕ ручки —
    /// бюджет и вариации обхода — и возвращает их назад; логика при этом не правится).</summary>
    private static void SetField(object target, string name, object value)
    {
        if (target == null) return;
        FieldInfo f = target.GetType().GetField(name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        if (f != null) f.SetValue(target, value);
    }

    private static MethodInfo samePathMethod;

    /// <summary>Тот же критерий, что у потока: приватный static IsSamePath (через рефлексию).</summary>
    private static bool SamePath(PlannedTrajectory a, PlannedTrajectory b)
    {
        if (samePathMethod == null)
            samePathMethod = typeof(TrajectoryFlowController).GetMethod("IsSamePath",
                BindingFlags.NonPublic | BindingFlags.Static);
        if (samePathMethod == null) return false;
        return (bool)samePathMethod.Invoke(null, new object[] { a, b });
    }

    /// <summary>Сколько путей УНИКАЛЬНЫ: число КЛАССОВ совпадения (объединение множеств).
    /// Считается через систему непересекающихся множеств, а не «жадным» перебором: жадный
    /// результат зависит от порядка списка (варианты показываются отсортированными по score),
    /// поэтому «добитые» дубликаты могли «затенить» свои же уникальные пути и занизить счёт
    /// (в прогоне 15.09.2026 так вышло 5 вместо 6) — это дефект ПРОВЕРКИ, а не потока.</summary>
    private static int CountUnique(List<PlannedTrajectory> plans)
    {
        int n = plans.Count;
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;
        for (int i = 0; i < n; i++)
            for (int j = i + 1; j < n; j++)
                if (SamePath(plans[i], plans[j])) Union(parent, i, j);
        var roots = new HashSet<int>();
        for (int i = 0; i < n; i++) roots.Add(Find(parent, i));
        return roots.Count;
    }

    private static int Find(int[] parent, int i)
    {
        while (parent[i] != i) { parent[i] = parent[parent[i]]; i = parent[i]; }
        return i;
    }

    private static void Union(int[] parent, int a, int b)
    {
        int ra = Find(parent, a), rb = Find(parent, b);
        if (ra != rb) parent[rb] = ra;
    }

    private static string Fmt(Vector3 p)
    {
        return "(" + p.x.ToString("0.00") + ", " + p.y.ToString("0.00") + ", " +
               p.z.ToString("0.00") + ")";
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

    private static void SetLasers(bool red, bool green)
    {
        if (cam == null) return;
        cam.leftHandEnabled = red;
        cam.rightHandEnabled = green;
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
