using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using KazistovVvFeatures;
using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// ДИАГНОСТИКА АГЕНТА (§27). НЕ переносится в копию пользователя (как и прочие Dsh*Diag).
///
/// ЗАДАЧА СЕССИИ — три жалобы оператора:
///   1) «роботы не плавно достигают конечной точки, а резко дёргаются где-то до середины
///      траектории, а далее ведут себя плавно»;
///   2) «не на всех роботах работает грамотно коллизия»;
///   3) «перепроверить комбинации нажатий программных кнопок».
///
/// ЧТО ДЕЛАЕТ (БЕЗ PlayMode — сцена открывается в EditMode, `PoseValidator` инициализируется
/// вручную, ровно как в `DshS20Diag`):
///   A. ПЛАВНОСТЬ. Берёт РЕАЛЬНЫЕ планы проекта и ПРОИГРЫВАЕТ их ровно так, как это делает
///      `TrajectoryExecutor` (шаг 1/60 с, линейная интерполяция между сэмплами по `Times`),
///      после чего считает по кадрам скорость / ускорение / рывок КАЖДОГО сустава и, главное,
///      СКАЧКИ СКОРОСТИ НА ГРАНИЦАХ СЭМПЛОВ (именно они читаются глазом как «дёрг»).
///      Сравниваются три источника движения:
///        • «планировщик»    — `Planner.Plan` (S-профиль уже внутри);
///        • «кнопка в позу»  — `KvPlanKit.MakeJointPlan` КАК ЕСТЬ (линейные времена);
///        • «кнопка + S»     — тот же план ПОСЛЕ `KvTrajMath.Retime` (предлагаемый фикс).
///      Все три дополнительно масштабируются `MotionTiming.RescaleToSpeed` под реальную
///      скорость робота (1/15 юнита в секунду) — именно это и едет в живом редакторе.
///   B. КОЛЛИЗИЯ. Для КАЖДОГО робота печатает капсульную цепочку, которую реально проверяет
///      `CollisionWorld.MinDistanceChain`, и СРАВНИВАЕТ её с независимым расчётом по ВСЕМ
///      звеньям цепочки. Дополнительно измеряет, насколько инструмент/палец выходит ЗА
///      последний узел цепочки (у SCARA в коде `toolDrop = 0`).
///   C. КОМБИНАЦИИ КЛАВИШ. Сводка реестра `KvBindings` + конфликты + фактическое чтение
///      клавиш в коде (чтобы в отчёте были не обещания, а числа).
///
/// Запуск:
///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;проект&gt;" -executeMethod DshS27Diag.RunAll -logFile Logs\_dsh_s27.log
/// Отчёт: &lt;persistentDataPath&gt;/KazistovVv/Reports/_dsh_s27_diag.txt
/// </summary>
public static class DshS27Diag
{
    private const string ScenePath = "Assets/_Project/00_Scenes/MainScene.unity";
    private const float RobotSpeedUps = 1f / 15f;    // robotMoveSpeed по умолчанию (TrajectoryFlowController)
    private const float FrameDt = 1f / 60f;          // шаг проигрывания «как в живом редакторе»

    private static readonly StringBuilder log = new StringBuilder();
    private static int fails, checks;
    private static int plannedDenseSamples;

    // ================================================================== вход

    public static void RunAll()
    {
        Line("=== DshS27Diag · сессия §27 (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ") ===");
        Line("проект: " + Path.GetFullPath(Path.Combine(Application.dataPath, "..")));
        Line("графика: " + SystemInfo.graphicsDeviceType + " · " + SystemInfo.graphicsDeviceName +
             " · batch=" + Application.isBatchMode);

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Line("сцена открыта: " + ScenePath);
        }
        catch (Exception e)
        {
            Fail("сцена", "не открылась: " + e.Message);
            Save();
            Finish();
            return;
        }

        try { RunSmoothness(); } catch (Exception e) { Fail("A/плавность", e.ToString()); }
        try { RunCollisionAudit(); } catch (Exception e) { Fail("B/коллизия", e.ToString()); }
        try { RunHotkeySummary(); } catch (Exception e) { Fail("C/клавиши", e.ToString()); }

        Save();
        Finish();
    }

    // ================================================================== A. ПЛАВНОСТЬ

    private static void RunSmoothness()
    {
        Section("A. ПЛАВНОСТЬ ДВИЖЕНИЯ (проигрывание ровно как TrajectoryExecutor)");
        Line("Скорость робота в живом редакторе: robotMoveSpeed = 1/15 = " +
             RobotSpeedUps.ToString("0.0000") + " юнита/с; шаг кадров 1/60 с.");
        Line("«Скачок» ниже — изменение МГНОВЕННОЙ скорости на границе двух сэмплов плана:");
        Line("именно он читается глазом как дёрг, потому что между сэмплами исполнитель");
        Line("интерполирует ЛИНЕЙНО и внутри сэмпла скорость постоянна.");

        RobotController six = FindRobot(false);
        RobotController scara = FindRobot(true);

        if (six == null && scara == null)
        {
            Fail("A", "в сцене не найден ни один робот");
            return;
        }

        if (six != null) SmoothnessForRobot(six);
        if (scara != null) SmoothnessForRobot(scara);
    }

    private static void SmoothnessForRobot(RobotController robot)
    {
        PoseValidator v = ValidatorFor(robot, out string why);
        if (v == null) { Fail("A (" + robot.robotName + ")", why); return; }

        CollisionWorld world = new CollisionWorld();
        world.Rebuild(robot);
        Line("");
        Line("--- " + robot.robotName + " (осей " + v.Dof + ", препятствий: боксов " +
             world.Boxes.Count + " / капсул " + world.Capsules.Count + ") ---");

        double[] start = v.CopyCurrent();

        // Цель: сначала пробуем честный планировщик (как в §20), иначе — просто сдвиг суставов.
        double[] goal = null;
        Planner planner = new Planner();
        planner.MotionLimits = new KvMotionLimits();
        planner.Init(v, world);
        if (planner.Ready)
        {
            float[] radii = v.Dof > 3 ? new[] { 0.30f, 0.42f } : new[] { 0.32f, 0.44f };
            float[] heights = v.Dof > 3 ? new[] { 0.30f, 0.42f } : new[] { 0.20f, 0.26f };
            for (int i = 0; i < radii.Length && goal == null; i++)
            {
                Vector3 target = robot.transform.position + new Vector3(radii[i], heights[i], -0.25f);
                double[] q;
                string note;
                if (!KvPlanKit.SolvePoseForPoint(v, world, target, Vector3.down, start, out q, out note))
                    continue;
                if (!v.WithinLimits(q)) continue;
                goal = q;
            }
        }
        if (goal == null)
        {
            Fail("A (" + robot.robotName + ")", "не найдена достижимая целевая поза");
            return;
        }

        KvMotionLimits limits = new KvMotionLimits();
        Line("лимиты: " + limits.maxVelDeg + " °/с · " + limits.maxAccDeg + " °/с² · " +
             limits.maxJerkDeg + " °/с³");

        // 1) План планировщика (так едет реальный робот на шаге 5).
        PlannedTrajectory plannerPlan = null;
        if (planner.Ready)
        {
            List<PlannedTrajectory> candidates = planner.Plan(start, v.TcpAt(goal), 4, 2027);
            if (candidates != null && candidates.Count > 0) plannerPlan = candidates[0];
        }

        // 2) План «кнопки» — ровно то, что строит KvPlanKit.MakeJointPlan для поз/макросов/Pick&Place.
        PlannedTrajectory buttonPlan = KvPlanKit.MakeJointPlan(v, world, start, goal,
            "перейти в позу (как строит кнопка)", 0.08f, 40);

        Line("");
        Line("  " + Pad("источник движения", 34) + Pad("сэмпл.", 7) + Pad("t, с", 8) +
             Pad("скачок v", 10) + Pad("на 0…1", 9) + Pad("jerk/пред", 10) + "вердикт");

        if (plannerPlan != null)
            Report(robot, v, limits, "планировщик (шаг 5)", plannerPlan, true);

        if (buttonPlan != null)
        {
            Report(robot, v, limits, "кнопка «в позу» КАК ЕСТЬ", buttonPlan, true);

            // Предлагаемый фикс, шаг 1: тот же путь, время перепараметризовано S-профилем.
            PlannedTrajectory fixedPlan = KvTrajMath.Retime(v, buttonPlan, limits, 1f, 1f,
                buttonPlan.Label, true, false);
            if (fixedPlan != null)
                Report(robot, v, limits, "кнопка + S-профиль", fixedPlan, true);
            else
                Fail("A (" + robot.robotName + ")", "Retime не построил профиль для плана кнопки");

            // Предлагаемый фикс, шаг 1+2: уплотнение пути + S-профиль — РОВНО то, что делает
            // MotionExecutor.Play после фикса §27.
            PlannedTrajectory dense = KvTrajMath.Densify(v, buttonPlan,
                KvTrajMath.DefaultDensifyStep, 2400);
            if (dense != null)
            {
                plannedDenseSamples = dense.Path.Length;
                PlannedTrajectory denseFixed = KvTrajMath.Retime(v, dense, limits, 1f, 1f,
                    dense.Label, true, false);
                if (denseFixed != null)
                    Report(robot, v, limits, "кнопка + уплотнение + S", denseFixed, true);
                else
                    Fail("A (" + robot.robotName + ")", "Retime не построил профиль после уплотнения");
            }
            else
            {
                Fail("A (" + robot.robotName + ")", "Densify не уплотнил путь (нечего уплотнять?)");
            }
        }

        // 3) Идемпотентность: повторный Retime плана планировщика не должен менять времена.
        if (plannerPlan != null)
        {
            PlannedTrajectory again = KvTrajMath.Retime(v, plannerPlan, limits, 1f, 1f, null, true, false);
            if (again != null && plannerPlan.Times != null && again.Times != null &&
                again.Times.Length == plannerPlan.Times.Length)
            {
                float worst = 0f;
                for (int i = 0; i < again.Times.Length; i++)
                    worst = Mathf.Max(worst, Mathf.Abs(again.Times[i] - plannerPlan.Times[i]));
                Check("A (" + robot.robotName + "): повторный S-профиль не меняет времена планировщика",
                    worst <= 1e-3f, "макс. расхождение " + worst.ToString("0.0000") + " с");
            }
        }
    }

    /// <summary>
    /// Проиграть план так, как это делает TrajectoryExecutor, и напечатать метрики плавности.
    /// </summary>
    private static void Report(RobotController robot, PoseValidator v, KvMotionLimits limits,
        string name, PlannedTrajectory source, bool rescaleToRobotSpeed)
    {
        PlannedTrajectory plan = KvTrajMath.Clone(source, name);
        if (plan == null || plan.Path == null || plan.Path.Length < 3)
        {
            Line("  " + Pad(name, 34) + "— план непригоден");
            return;
        }

        // Ровно то, что делает MotionExecutor.Play: времена под заданную скорость робота.
        if (rescaleToRobotSpeed)
        {
            float length = KvTrajMath.PathLength(v, plan.Path);
            MotionTiming.RescaleToSpeed(plan, length, RobotSpeedUps);
        }

        Frame[] frames = Play(plan, FrameDt);
        if (frames.Length < 4)
        {
            Line("  " + Pad(name, 34) + "— слишком короткое движение для метрик");
            return;
        }

        int dof = plan.Path[0].Length;
        double[] scale = new double[dof];
        for (int j = 0; j < dof; j++) scale[j] = v.IsPrismatic(j) ? 10.0 : 1.0 / 90.0;

        // --- per-joint jerk по кадрам (конечные разности 3-го порядка)
        double jerkRatio = 0.0, accRatio = 0.0, velRatio = 0.0;
        for (int j = 0; j < dof; j++)
        {
            bool pris = v.IsPrismatic(j);
            double vLim = (pris ? limits.maxVelMps : limits.maxVelDeg) * (pris ? 10.0 : 1.0 / 90.0);
            double aLim = (pris ? limits.maxAccMps2 : limits.maxAccDeg) * (pris ? 10.0 : 1.0 / 90.0);
            double jLim = (pris ? limits.maxJerkMps3 : limits.maxJerkDeg) * (pris ? 10.0 : 1.0 / 90.0);
            for (int k = 0; k < frames.Length; k++)
            {
                double vel = Math.Abs(frames[k].vel[j]) * scale[j];
                double acc = k > 0 ? Math.Abs(frames[k].acc[j]) * scale[j] : 0.0;
                double jrk = k > 0 ? Math.Abs(frames[k].jerk[j]) * scale[j] : 0.0;
                velRatio = Math.Max(velRatio, vel / Math.Max(1e-12, vLim));
                accRatio = Math.Max(accRatio, acc / Math.Max(1e-12, aLim));
                jerkRatio = Math.Max(jerkRatio, jrk / Math.Max(1e-12, jLim));
            }
        }

        // --- СКАЧКИ скорости на границах сэмплов плана (в нормированных единицах, °/с-эквивалент)
        int n = plan.Path.Length;
        double[] segSpeed = new double[n - 1];
        for (int i = 0; i + 1 < n; i++)
        {
            float dt = plan.Times[i + 1] - plan.Times[i];
            if (dt <= 1e-6f) { segSpeed[i] = 0; continue; }
            double dist = 0.0;
            for (int j = 0; j < dof; j++)
            {
                double d = (plan.Path[i + 1][j] - plan.Path[i][j]) * scale[j];
                dist += d * d;
            }
            segSpeed[i] = Math.Sqrt(dist) / dt;
        }
        double maxJump = 0.0, sumJump = 0.0;
        int maxJumpAt = 0;
        for (int i = 1; i < segSpeed.Length; i++)
        {
            double jump = Math.Abs(segSpeed[i] - segSpeed[i - 1]);
            sumJump += jump;
            if (jump > maxJump) { maxJump = jump; maxJumpAt = i; }
        }
        double meanJump = segSpeed.Length > 1 ? sumJump / (segSpeed.Length - 1) : 0.0;
        double total = plan.Times[n - 1];
        double atFraction = total > 1e-6 ? plan.Times[maxJumpAt] / total : 0.0;

        int frames2 = frames.Length;
        bool smooth = jerkRatio <= 1.05 && maxJump <= Math.Max(0.02, meanJump * 6.0);

        Line("  " + Pad(name, 34) + Pad(n.ToString(), 7) +
             Pad(total.ToString("0.00"), 8) +
             Pad((maxJump * 90.0).ToString("0.0"), 10) +
             Pad((atFraction * 100.0).ToString("0") + " %", 9) +
             Pad(jerkRatio.ToString("0.00"), 10) +
             (smooth ? "плавно" : "РЫВОК"));
        Line("      кадров " + frames2 + " · макс. скачок " + DisplayJump(maxJump) +
             " (средний " + DisplayJump(meanJump) + ", отношение " +
             (meanJump > 1e-9 ? (maxJump / meanJump).ToString("0.0") : "∞") +
             "×) · макс. ускорение " + accRatio.ToString("0.00") + " от предела ·" +
             " макс. скорость " + velRatio.ToString("0.00") + " от предела");

        Check("A (" + robot.robotName + ") «" + name + "»: рывок в пределах и ступеньки скорости " +
              "не более 6× от среднего", smooth,
            "jerk " + jerkRatio.ToString("0.00") + " от предела" +
            (jerkRatio > 1.05 ? " — ПРЕДЕЛ РЫВКА ПРЕВЫШЕН" : "") +
            " · макс. ступенька " + DisplayJump(maxJump) + " против средней " + DisplayJump(meanJump) +
            " · самая резкая — на " + (atFraction * 100.0).ToString("0") + " % пути");
    }

    private static string DisplayJump(double norm)
    {
        // Нормированная единица пути = 90°, поэтому скачок в норм. ед./с — это °/с.
        return (norm * 90.0).ToString("0.0") + " °/с";
    }

    /// <summary>Кадр проигрывания: поза + скорость/ускорение/рывок по каждому суставу.</summary>
    private struct Frame
    {
        public double[] q;
        public double[] vel;
        public double[] acc;
        public double[] jerk;
    }

    /// <summary>
    /// ПРОИГРЫВАНИЕ ровно как `TrajectoryExecutor.Update`: playhead += dt, поиск сэмпла,
    /// ЛИНЕЙНАЯ интерполяция между Path[index] и Path[index+1] по Times.
    /// </summary>
    private static Frame[] Play(PlannedTrajectory plan, float dt)
    {
        var list = new List<Frame>(4096);
        float[] times = plan.Times;
        if (times == null || times.Length != plan.Path.Length || plan.Path.Length < 3)
            return new Frame[0];

        float total = times[times.Length - 1];
        if (total <= 1e-4f) return new Frame[0];

        int dof = plan.Path[0].Length;
        float playhead = 0f;
        int index = 0;
        double[] prevQ = null;
        double[] prevV = null;
        double[] prevA = null;

        while (playhead < total && list.Count < 200000)
        {
            playhead += dt;
            if (playhead > total) playhead = total;
            while (index + 1 < times.Length && times[index + 1] <= playhead) index++;

            double[] q;
            if (index + 1 >= times.Length)
            {
                q = plan.Path[plan.Path.Length - 1];
            }
            else
            {
                float t0 = times[index], t1 = times[index + 1];
                float k = t1 > t0 ? Mathf.Clamp01((playhead - t0) / (t1 - t0)) : 0f;
                double[] a = plan.Path[index], b = plan.Path[index + 1];
                q = new double[a.Length];
                for (int i = 0; i < a.Length; i++) q[i] = a[i] + (b[i] - a[i]) * k;
            }

            var f = new Frame { q = q, vel = new double[dof], acc = new double[dof], jerk = new double[dof] };
            if (prevQ != null)
                for (int j = 0; j < dof; j++) f.vel[j] = (q[j] - prevQ[j]) / dt;
            if (prevV != null)
                for (int j = 0; j < dof; j++) f.acc[j] = (f.vel[j] - prevV[j]) / dt;
            if (prevA != null)
                for (int j = 0; j < dof; j++) f.jerk[j] = (f.acc[j] - prevA[j]) / dt;

            list.Add(f);
            prevQ = q; prevV = f.vel; prevA = f.acc;
            if (playhead >= total) break;
        }
        return list.ToArray();
    }

    // ================================================================== B. КОЛЛИЗИЯ

    private static void RunCollisionAudit()
    {
        Section("B. КОЛЛИЗИЯ: что РЕАЛЬНО проверяется у каждого робота");
        Line("`CollisionWorld.MinDistanceChain` пропускает ПЕРВЫЙ отрезок цепочки");
        Line("(в коде: firstSegment = nodes.Count > 2 ? 1 : 0). Ниже видно, чем этот отрезок");
        Line("является у КАЖДОГО робота и не пробивает ли он мир столкновений.");

        RobotController six = FindRobot(false);
        RobotController scara = FindRobot(true);
        if (six != null) CollisionForRobot(six);
        if (scara != null) CollisionForRobot(scara);
    }

    private static void CollisionForRobot(RobotController robot)
    {
        PoseValidator v = ValidatorFor(robot, out string why);
        if (v == null) { Fail("B (" + robot.robotName + ")", why); return; }

        CollisionWorld world = new CollisionWorld();
        world.Rebuild(robot, v.linkRadius);

        Line("");
        Line("--- " + robot.robotName + " (linkRadius " + v.linkRadius.ToString("0.000") +
             " м; боксов " + world.Boxes.Count + ", капсул " + world.Capsules.Count +
             ", пол Y=" + (world.FloorY > float.NegativeInfinity
                 ? world.FloorY.ToString("0.000") : "не найден") + ") ---");

        double[] q = v.CopyCurrent();
        Vector3 tcp;
        Vector3[] nodes;
        float reported = v.ClearanceAt(q, world, out tcp, out nodes);

        Line("  узлов цепочки: " + nodes.Length);
        for (int i = 0; i < nodes.Length; i++)
            Line("    [" + i + "] " + Fmt(nodes[i]) +
                 (i + 1 < nodes.Length
                     ? "  → [" + (i + 1) + "]  длина " + Vector3.Distance(nodes[i], nodes[i + 1]).ToString("0.000") + " м"
                     : "  (конец цепочки = TCP)"));

        int firstSegment = nodes.Length > 2 ? 1 : 0;
        // Что РЕАЛЬНО проверяет проект: значение по умолчанию (пропуск первого отрезка у цепочки
        // длиннее двух узлов) против «проверять всё». Разные числа = пропуск влияет на запас.
        float projectDefault = world.MinDistanceChain(nodes, v.linkRadius);
        float projectAll = world.MinDistanceChain(nodes, v.linkRadius, 0);
        Line("  отрезок 0 (" + Fmt(nodes[0]) + " → " + Fmt(nodes[1]) + ", длина " +
             (nodes.Length > 1 ? Vector3.Distance(nodes[0], nodes[1]).ToString("0.000") : "?") + " м)");
        Line("    запас БЕЗ него (как проект зовёт сейчас): " + (projectDefault * 1000f).ToString("0") +
             " мм · С ним (firstSegment=0): " + (projectAll * 1000f).ToString("0") + " мм" +
             (Mathf.Abs(projectDefault - projectAll) > 1e-5f ? " ← отличие есть" : " ← отличия нет"));

        // Независимый расчёт: ВСЕ отрезки против всех препятствий мира (и против пола —
        // иначе числа несравнимы с `MinDistanceChain`, где пол учитывается).
        double minFull = world.FloorY > float.NegativeInfinity ? float.MaxValue : float.MaxValue;
        int minFullSeg = -1;
        string minFullName = "";
        if (world.FloorY > float.NegativeInfinity)
            for (int i = 0; i + 1 < nodes.Length; i++)
            {
                float df = Mathf.Min(nodes[i].y, nodes[i + 1].y) - world.FloorY - v.linkRadius;
                if (df < minFull) { minFull = df; minFullSeg = i; minFullName = "пол"; }
            }
        for (int i = 0; i + 1 < nodes.Length; i++)
        {
            for (int k = 0; k < world.Capsules.Count; k++)
            {
                ObstacleCapsule c = world.Capsules[k];
                if (c.support) continue;
                float d = CollisionWorld.SegmentSegmentDistance(nodes[i], nodes[i + 1], c.a, c.b) -
                          v.linkRadius - c.r;
                if (d < minFull) { minFull = d; minFullSeg = i; minFullName = "капсула " + c.name; }
            }
            for (int k = 0; k < world.Boxes.Count; k++)
            {
                ObstacleBox bx = world.Boxes[k];
                if (bx.support) continue;
                float d = CollisionWorld.SegmentBoxDistance(nodes[i], nodes[i + 1], bx) - v.linkRadius;
                if (d < minFull) { minFull = d; minFullSeg = i; minFullName = "бокс " + bx.name; }
            }
        }

        Line("  запас по ВСЕМ звеньям (независимый расчёт): " +
             (minFull == float.MaxValue ? "нет препятствий" : (minFull * 1000f).ToString("0") + " мм") +
             (minFullSeg >= 0 ? " · самое тесное — отрезок " + minFullSeg + " (ближе всего: " + minFullName + ")" : ""));
        Line("  запас, который видит ПРОЕКТ (MinDistanceChain): " +
             (float.IsNegativeInfinity(reported) ? "нет" : (reported * 1000f).ToString("0") + " мм"));

        bool hole = minFullSeg == 0 && minFull < reported - 1e-4f;
        Check("B (" + robot.robotName + "): пропущенный отрезок 0 не ближе к препятствиям, чем " +
              "проверяемые", !hole,
            hole
                ? "отрезок 0 даёт запас " + (minFull * 1000f).ToString("0") +
                  " мм против " + (reported * 1000f).ToString("0") + " мм у проверяемых — " +
                  "столкновение по нему НЕ ЛОВИТСЯ"
                : "отрезок 0 не является самым тесным");

        // Инструмент за последним узлом цепочки.
        Transform last = LastChainTransform(robot, v);
        if (last != null)
        {
            float belowExtent = ExtentBeyond(last, nodes[nodes.Length - 1], v);
            Line("  инструмент: последний узел цепочки = «" + last.name + "»; геометрия ниже/дальше " +
                 "этого узла простирается на " + (belowExtent * 1000f).ToString("0") + " мм");
            Check("B (" + robot.robotName + "): инструмент не выходит за последний узел цепочки",
                belowExtent <= v.linkRadius,
                "выходит на " + (belowExtent * 1000f).ToString("0") + " мм (linkRadius " +
                (v.linkRadius * 1000f).ToString("0") + " мм) — эта часть не проверяется");
        }

        // Сколько препятствий вообще попадает в мир (для отчёта).
        int named = 0;
        var names = new StringBuilder();
        for (int i = 0; i < world.Boxes.Count && named < 12; i++, named++)
            names.Append(world.Boxes[i].name + (world.Boxes[i].support ? "(опора) " : " "));
        Line("  боксы: " + names.ToString().Trim());
    }

    /// <summary>Последний трансформ цепочки (для SCARA — z_5, для 6-осевого — TCP).</summary>
    private static Transform LastChainTransform(RobotController robot, PoseValidator v)
    {
        if (robot is SCARAController sc) return sc.joint3;
        if (robot is SixAxisController six) return six.tcp != null ? six.tcp : six.endEffector;
        return null;
    }

    /// <summary>
    /// Насколько далеко геометрия инструмента выходит за последний узел цепочки
    /// (вниз — для вертикального инструмента SCARA, вдоль оси — для 6-осевого).
    /// Считается по габаритам рендереров под этим узлом: цепочка обязана накрывать инструмент.
    /// </summary>
    private static float ExtentBeyond(Transform tool, Vector3 chainEnd, PoseValidator v)
    {
        Renderer[] rs = tool.GetComponentsInChildren<Renderer>();
        float worst = 0f;
        foreach (Renderer r in rs)
        {
            if (r == null) continue;
            Bounds b = r.bounds;
            // Наибольшее расстояние от узла цепочки до угла габарита (по всем восьми углам).
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    for (int sz = -1; sz <= 1; sz += 2)
                    {
                        Vector3 corner = b.center + new Vector3(b.extents.x * sx,
                            b.extents.y * sy, b.extents.z * sz);
                        worst = Mathf.Max(worst, Vector3.Distance(corner, chainEnd));
                    }
        }
        return worst;
    }

    // ================================================================== C. КЛАВИШИ

    private static void RunHotkeySummary()
    {
        Section("C. КОМБИНАЦИИ КЛАВИШ (реестр биндов)");
        List<KazistovVvUI.KvBindEntry> all = KazistovVvUI.KvBindings.All();
        Line("биндов в реестре: " + all.Count);
        HashSet<string> conflicts = KazistovVvUI.KvBindings.Conflicts();
        Line("конфликтов по реестру: " + conflicts.Count +
             (conflicts.Count > 0 ? " → " + string.Join(", ", new List<string>(conflicts).ToArray()) : ""));
        Check("C: реестр биндов без конфликтов", conflicts.Count == 0,
            conflicts.Count + " конфликт(ов)");

        var keys = new Dictionary<string, int>();
        foreach (KazistovVvUI.KvBindEntry e in all)
        {
            if (e == null || string.IsNullOrEmpty(e.Keys)) continue;
            string k = e.Keys.Trim().ToUpperInvariant();
            int n;
            keys.TryGetValue(k, out n);
            keys[k] = n + 1;
        }
        var dup = new List<string>();
        foreach (KeyValuePair<string, int> kv in keys)
            if (kv.Value > 1) dup.Add(kv.Key + "×" + kv.Value);
        Line("клавиш, назначенных более одного раза: " + dup.Count +
             (dup.Count > 0 ? " → " + string.Join(", ", dup.ToArray()) : ""));
    }

    // ================================================================== вспомогательное

    private static RobotController FindRobot(bool scara)
    {
        RobotController[] all = UnityEngine.Object.FindObjectsByType<RobotController>();
        foreach (RobotController rc in all)
        {
            if (rc == null) continue;
            if (scara && rc is SCARAController) return rc;
            if (!scara && !(rc is SCARAController)) return rc;
        }
        return null;
    }

    private static PoseValidator ValidatorFor(RobotController robot, out string why)
    {
        why = "";
        try
        {
            PoseValidator v = new PoseValidator();
            v.Init(robot);
            if (!v.Ready) { why = "PoseValidator не стал Ready (робот «" + robot.robotName + "»)"; return null; }
            return v;
        }
        catch (Exception e) { why = "PoseValidator.Init: " + e.Message; return null; }
    }

    private static void Section(string title)
    {
        Line("");
        Line("================================================================");
        Line(title);
        Line("================================================================");
    }

    private static void Check(string what, bool ok, string detail)
    {
        checks++;
        if (ok) { Line("  [OK]   " + what + (string.IsNullOrEmpty(detail) ? "" : " · " + detail)); return; }
        fails++;
        Line("  [FAIL] " + what + (string.IsNullOrEmpty(detail) ? "" : " · " + detail));
    }

    private static string Fmt(Vector3 p)
    {
        return "(" + p.x.ToString("0.000") + ", " + p.y.ToString("0.000") + ", " + p.z.ToString("0.000") + ")";
    }

    private static string Pad(string s, int width)
    {
        if (s == null) s = "";
        return s.Length >= width ? s + " " : s + new string(' ', width - s.Length);
    }

    private static void Fail(string what, string detail)
    {
        fails++;
        Line("[FAIL] " + what + " — " + detail);
    }

    private static void Line(string text)
    {
        log.AppendLine(text);
        Debug.Log("[DshS27Diag] " + text);
    }

    private static void Save()
    {
        try
        {
            string path = FeatureStorage.ReportPath("_dsh_s27_diag.txt");
            File.WriteAllText(path, log.ToString(), new UTF8Encoding(false));
            Debug.Log("[DshS27Diag] отчёт: " + path);
            // Копия рядом с проектом — чтобы отчёт было видно и без просмотра persistentDataPath.
            string local = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "_dsh_s27_diag.txt"));
            File.WriteAllText(local, log.ToString(), new UTF8Encoding(false));
            Debug.Log("[DshS27Diag] копия отчёта: " + local);
        }
        catch (Exception e) { Debug.LogError("[DshS27Diag] отчёт не записан: " + e.Message); }
    }

    private static void Finish()
    {
        Debug.Log("[DshS27Diag] ИТОГ: проверок " + checks + ", отказов " + fails);
    }
}
