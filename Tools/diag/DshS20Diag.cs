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
/// ДИАГНОСТИКА АГЕНТА (§20). НЕ переносится в копию пользователя (как и прочие Dsh*Diag).
///
/// Что проверяет ЖИВЫМ кодом проекта (без PlayMode — сцена открывается в EditMode,
/// робот берётся из сцены, `PoseValidator` инициализируется вручную):
///   • ФИКС 1 — аналитический S-профиль: 11 сценариев через НАСТОЯЩИЕ `KvTrajMath.Retime`
///     и `KvTrajMath.SProfileBuild` (не через отдельный порт), метрики — конечными разностями
///     по сэмплам, как их считает метрика проекта. Проверяются скорость, ускорение и рывок
///     отдельно по КАЖДОМУ суставу в нормированных единицах (отношение «факт / предел»);
///   • ФИКС 2 — время планировщика против постобработки на том же пути: `Planner.Plan`
///     и `KvTrajMath.Retime` по ОДНОМУ набору лимитов, ожидание ±5 %;
///   • ФИКС 3 — `KvTrajMath.FindSharpCorners` / `MaxCornerDeg` / `SmoothCorners`;
///   • попутно включает `playModeTestRunnerEnabled` (без него PlayMode-тесты в
///     предопределённой сборке не находятся раннером).
///
/// Запуск: Unity -batchmode -nographics -quit -executeMethod DshS20Diag.RunAll
/// </summary>
public static class DshS20Diag
{
    private const string ScenePath = "Assets/_Project/00_Scenes/MainScene.unity";
    private static readonly StringBuilder log = new StringBuilder();
    private static int fails;

    // ================================================================== вход

    public static void RunAll()
    {
        Line("=== DshS20Diag · сессия §20 (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + ") ===");
        try { EnablePlayModeTests(); }
        catch (Exception e) { Line("[!] playModeTestRunnerEnabled: " + e.Message); }

        try
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Line("сцена открыта: " + ScenePath);
        }
        catch (Exception e)
        {
            Line("[!] сцена не открылась: " + e.Message);
            Save();
            Finish();
            return;
        }

        try { RunJerkBench(); } catch (Exception e) { Fail("ФИКС 1 исключение", e.ToString()); }
        try { RunPlannerTimeCompare(); } catch (Exception e) { Fail("ФИКС 2 исключение", e.ToString()); }
        try { RunCornerChecks(); } catch (Exception e) { Fail("ФИКС 3 исключение", e.ToString()); }

        Save();
        Finish();
    }

    /// <summary>
    /// Включить PlayMode-тесты для предопределённых сборок (ProjectSettings.asset:
    /// `playModeTestRunnerEnabled`). Правим ЧЕРЕЗ САМ РЕДАКТОР (SerializedObject), а не текстом
    /// файла: ProjectSettings.asset — служебный ассет Unity.
    /// </summary>
    private static void EnablePlayModeTests()
    {
        UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
        if (assets == null || assets.Length == 0) { Line("[!] ProjectSettings.asset не найден"); return; }

        SerializedObject so = new SerializedObject(assets[0]);
        SerializedProperty prop = so.FindProperty("playModeTestRunnerEnabled");
        if (prop == null) { Line("[!] свойство playModeTestRunnerEnabled не найдено"); return; }

        Line("playModeTestRunnerEnabled: было " + prop.boolValue);
        if (!prop.boolValue)
        {
            prop.boolValue = true;
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Line("playModeTestRunnerEnabled: стало " + prop.boolValue +
                 " (PlayMode-тесты в Assembly-CSharp теперь находятся раннером)");
        }
    }

    // ================================================================== ФИКС 1: S-профиль

    /// <summary>
    /// 11 сценариев стенда §16.1, но прогнанные НАСТОЯЩИМ кодом проекта:
    /// `KvTrajMath.Retime` (внутри — аналитический `SProfileBuild`) на живой `PoseValidator`.
    /// Метрики — по фактическим сэмплам; для каждого сустава считается отношение «факт / предел»,
    /// поэтому призматическая ось SCARA сравнивается со СВОИМ пределом, а не с градусами.
    /// </summary>
    private static void RunJerkBench()
    {
        Line("");
        Line("=== ФИКС 1: аналитический S-профиль (живой KvTrajMath.Retime) ===");

        RobotController six = FindRobot(false);
        RobotController scara = FindRobot(true);

        if (six == null && scara == null)
        {
            Fail("ФИКС 1", "в сцене не найден ни один робот — прогон невозможен");
            return;
        }

        KvMotionLimits limits = new KvMotionLimits();
        Line("лимиты набора: " + limits.maxVelDeg + " °/с · " + limits.maxAccDeg + " °/с² · " +
             limits.maxJerkDeg + " °/с³ · призма " + limits.maxVelMps + " м/с / " +
             limits.maxAccMps2 + " м/с² / " + limits.maxJerkMps3 + " м/с³");

        int cases = 0, bad = 0;

        if (six != null)
        {
            PoseValidator v = ValidatorFor(six, out string why);
            if (v == null) { Fail("ФИКС 1 (робот)", why); }
            else
            {
                Line("");
                Line("--- робот «" + six.robotName + "» (осей " + v.Dof + ") ---");
                Line("  " + Pad("сценарий", 30) + Pad("t, с", 9) + Pad("v/пред", 9) +
                     Pad("a/пред", 9) + Pad("jerk/пред", 11) + "итог");

                foreach (int n in new[] { 40, 118, 240 })
                    cases += RunCase(v, limits, "прямая, сэмплов " + n, Straight(n, 6), ref bad);

                foreach (int n in new[] { 60, 118 })
                    cases += RunCase(v, limits, "дуга, сэмплов " + n, Arc(n, 6), ref bad);

                cases += RunCase(v, limits, "короткий путь 6°", Short(24, 6), ref bad);
                cases += RunCase(v, limits, "излом направления", Kink(90, 6), ref bad);
                cases += RunCase(v, limits, "jerk = 240 °/с³", Straight(118, 6), ref bad, 240f);
                cases += RunCase(v, limits, "jerk = 4800 °/с³", Straight(118, 6), ref bad, 4800f);
                cases += RunCase(v, limits, "v = 20 °/с, a = 40 °/с²", Straight(118, 6), ref bad,
                    1200f, 20f, 40f);
            }
        }

        if (scara != null)
        {
            PoseValidator vs = ValidatorFor(scara, out string why2);
            if (vs == null) { Fail("ФИКС 1 (SCARA)", why2); }
            else
            {
                Line("");
                Line("--- SCARA «" + scara.robotName + "» (осей " + vs.Dof +
                     ", призматическая ось учитывается отдельно) ---");
                Line("  " + Pad("сценарий", 30) + Pad("t, с", 9) + Pad("v/пред", 9) +
                     Pad("a/пред", 9) + Pad("jerk/пред", 11) + "итог");
                foreach (int n in new[] { 40, 118, 240 })
                    cases += RunCase(vs, limits, "прямая, сэмплов " + n, Straight(n, 3), ref bad);
                cases += RunCase(vs, limits, "излом направления", Kink(90, 3), ref bad);
                cases += RunCase(vs, limits, "SCARA (призма 0.18 м)", Scara(80), ref bad);
            }
        }

        Line("");
        if (bad == 0)
            Line("[OK]   ФИКС 1: во ВСЕХ " + cases + " прогонах скорость, ускорение и рывок " +
                 "в пределах лимитов (запас есть, превышений нет)");
        else
            Fail("ФИКС 1", "превышений лимитов: " + bad + " из " + cases);
    }

    private static int RunCase(PoseValidator v, KvMotionLimits limits, string name, double[][] path,
        ref int bad, float jerk = 1200f, float vel = 90f, float acc = 180f)
    {
        KvMotionLimits lim = limits.Clone();
        lim.maxJerkDeg = jerk;
        lim.maxVelDeg = vel;
        lim.maxAccDeg = acc;
        lim.maxJerkMps3 = jerk / 90f * 10f;      // та же шкала, что у вращательных
        lim.maxVelMps = vel / 90f;
        lim.maxAccMps2 = acc / 90f;

        PlannedTrajectory plan = new PlannedTrajectory { Path = path, Label = name };
        PlannedTrajectory timed = KvTrajMath.Retime(v, plan, lim, 1f, 1f, name);
        if (timed == null || timed.Times == null || timed.Times.Length != path.Length)
        {
            Line("  " + Pad(name, 30) + "—           профиль не построен");
            bad++;
            return 1;
        }

        double[] scale = new double[v.Dof];
        for (int j = 0; j < v.Dof; j++) scale[j] = v.IsPrismatic(j) ? 10.0 : 1.0 / 90.0;

        double vRatio = 0.0, aRatio = 0.0, jRatio = 0.0;
        double[][] vel2 = Deriv(path, timed.Times);
        double[][] acc2 = Deriv(vel2, timed.Times);
        double[][] jrk = Deriv(acc2, timed.Times);

        for (int j = 0; j < v.Dof; j++)
        {
            bool pris = v.IsPrismatic(j);
            double vLimUnit = (pris ? lim.maxVelMps : lim.maxVelDeg) * (pris ? 10.0 : 1.0 / 90.0);
            double aLimUnit = (pris ? lim.maxAccMps2 : lim.maxAccDeg) * (pris ? 10.0 : 1.0 / 90.0);
            double jLimUnit = (pris ? lim.maxJerkMps3 : lim.maxJerkDeg) * (pris ? 10.0 : 1.0 / 90.0);

            vRatio = Math.Max(vRatio, MaxColumn(vel2, j) * scale[j] / Math.Max(1e-9, vLimUnit));
            aRatio = Math.Max(aRatio, MaxColumn(acc2, j) * scale[j] / Math.Max(1e-9, aLimUnit));
            jRatio = Math.Max(jRatio, MaxColumn(jrk, j) * scale[j] / Math.Max(1e-9, jLimUnit));
        }

        bool ok = vRatio <= 1.02 && aRatio <= 1.05 && jRatio <= 1.05;
        if (!ok) bad++;

        Line("  " + Pad(name, 30) + Pad(timed.Time.ToString("0.000"), 9) +
             Pad(vRatio.ToString("0.00"), 9) + Pad(aRatio.ToString("0.00"), 9) +
             Pad(jRatio.ToString("0.00"), 11) + (ok ? "OK (запас " +
                 ((1.0 - Math.Max(jRatio, Math.Max(vRatio, aRatio))) * 100.0).ToString("0") + " %)"
                 : "FAIL"));
        return 1;
    }

    // ================================================================== ФИКС 2: время планировщика

    /// <summary>
    /// Планировщик против постобработки: для каждой построенной траектории сравнивается
    /// `plan.Time` (как отдал `Planner`) и время `KvTrajMath.Retime` ТОГО ЖЕ пути по ТЕМ ЖЕ
    /// лимитам. Ожидание ТЗ — совпадение в пределах ±5 % (раньше расходилось в 2–3 раза).
    /// </summary>
    private static void RunPlannerTimeCompare()
    {
        Line("");
        Line("=== ФИКС 2: время планировщика против постобработки (один набор лимитов) ===");

        RobotController six = FindRobot(false);
        RobotController scara = FindRobot(true);
        if (six == null && scara == null) { Fail("ФИКС 2", "роботов в сцене нет"); return; }

        KvMotionLimits limits = new KvMotionLimits();
        if (six != null) CompareForRobot(six, limits, false);
        if (scara != null) CompareForRobot(scara, limits, true);
    }

    private static void CompareForRobot(RobotController robot, KvMotionLimits limits, bool scara)
    {
        PoseValidator v = ValidatorFor(robot, out string why);
        if (v == null) { Fail("ФИКС 2 (" + robot.robotName + ")", why); return; }

        CollisionWorld world = new CollisionWorld();
        world.Rebuild(robot);

        Planner planner = new Planner();
        planner.MotionLimits = limits;
        planner.Init(v, world);
        if (!planner.Ready) { Fail("ФИКС 2 (" + robot.robotName + ")", "планировщик не готов"); return; }

        Line("");
        Line("--- " + robot.robotName + " (осей " + v.Dof + ", лимиты: " + limits.maxVelDeg +
             " °/с · " + limits.maxAccDeg + " °/с² · " + limits.maxJerkDeg + " °/с³) ---");

        double[] start = v.CopyCurrent();
        Vector3 basePos = v.BasePosition;
        int trajectories = 0, within = 0;
        double worstRatio = 0.0;

        float[] radii = scara ? new[] { 0.30f, 0.40f, 0.50f } : new[] { 0.30f, 0.42f, 0.55f };
        float[] heights = scara ? new[] { 0.16f, 0.24f, 0.12f } : new[] { 0.28f, 0.40f, 0.22f };

        for (int i = 0; i < radii.Length; i++)
        {
            Vector3 target = robot.transform.position + new Vector3(radii[i], heights[i], -0.25f);
            double[] q;
            string note;
            if (!KvPlanKit.SolvePoseForPoint(v, world, target, Vector3.down, start, out q, out note))
                continue;

            List<PlannedTrajectory> candidates = planner.Plan(start, target, 8, 1000 + i);
            if (candidates == null || candidates.Count == 0) continue;

            Line("  точка " + (i + 1) + " " + Fmt(target) + ": вариантов " + candidates.Count +
                 " · " + planner.LastDebug);

            for (int c = 0; c < candidates.Count; c++)
            {
                PlannedTrajectory plan = candidates[c];
                PlannedTrajectory post = KvTrajMath.Retime(v, plan, limits, 1f, 1f, null, true);
                if (post == null) continue;

                double plannerTime = plan.Time;
                double postTime = post.Time;
                if (postTime <= 1e-6) continue;

                double ratio = plannerTime / postTime;
                trajectories++;
                double dev = Math.Abs(ratio - 1.0);
                if (dev > worstRatio) worstRatio = dev;
                if (dev <= 0.05) within++;

                Line("    вариант " + (c + 1) + ": планировщик " + plannerTime.ToString("0.000") +
                     " с · постобработка " + postTime.ToString("0.000") + " с · отношение " +
                     ratio.ToString("0.000") + (dev <= 0.05 ? "  OK" : "  РАСХОЖДЕНИЕ"));
            }
        }

        if (trajectories == 0)
        {
            Fail("ФИКС 2 (" + robot.robotName + ")", "не построено ни одной траектории");
            return;
        }

        Line("  ИТОГ " + robot.robotName + ": траекторий " + trajectories + ", в допуске ±5 % — " +
             within + ", худшее отклонение " + (worstRatio * 100.0).ToString("0.0") + " %");
        if (within != trajectories)
            Fail("ФИКС 2 (" + robot.robotName + ")",
                "вне допуска ±5 %: " + (trajectories - within) + " из " + trajectories);
    }

    // ================================================================== ФИКС 3: изломы

    private static void RunCornerChecks()
    {
        Line("");
        Line("=== ФИКС 3: поиск и точечное сглаживание изломов ===");

        RobotController robot = FindRobot(false);
        if (robot == null) { Fail("ФИКС 3", "6-осевой робот не найден"); return; }
        PoseValidator v = ValidatorFor(robot, out string why);
        if (v == null) { Fail("ФИКС 3", why); return; }

        double[][] straight = Straight(118, v.Dof);
        double[][] kink = Kink(90, v.Dof);

        int[] none = KvTrajMath.FindSharpCorners(v, new PlannedTrajectory { Path = straight },
            KvTrajMath.SharpCornerDeg);
        int[] found = KvTrajMath.FindSharpCorners(v, new PlannedTrajectory { Path = kink },
            KvTrajMath.SharpCornerDeg);

        float straightAngle = KvTrajMath.MaxCornerDeg(v, new PlannedTrajectory { Path = straight });
        float kinkAngle = KvTrajMath.MaxCornerDeg(v, new PlannedTrajectory { Path = kink });

        Line("  прямой путь: изломов " + none.Length + " · макс. угол " +
             straightAngle.ToString("0.0") + "°");
        Line("  путь с изломом: изломов " + found.Length + " (индексы " + Join(found) +
             ") · макс. угол " + kinkAngle.ToString("0.0") + "°");

        if (none.Length != 0) Fail("ФИКС 3", "на прямом пути найдены ложные изломы: " + none.Length);
        if (found.Length == 0) Fail("ФИКС 3", "на пути с изломом излом не найден");
        if (kinkAngle <= KvTrajMath.SharpCornerDeg) Fail("ФИКС 3", "угол излома ниже порога");

        double[][] locally = KvTrajMath.SmoothCorners(kink, found, KvSmoothMethod.BSpline, 0.6f, 3);
        if (locally == null || locally.Length != kink.Length)
            Fail("ФИКС 3", "SmoothCorners вернул путь другой длины");
        else
        {
            int changed = 0, farChanged = 0;
            for (int i = 0; i < kink.Length; i++)
            {
                bool differs = false;
                for (int j = 0; j < v.Dof; j++)
                    if (Math.Abs(locally[i][j] - kink[i][j]) > 1e-9) differs = true;
                if (differs) changed++;
                bool near = false;
                for (int c = 0; c < found.Length; c++) if (Math.Abs(i - found[c]) <= 3) near = true;
                if (differs && !near) farChanged++;
            }
            Line("  точечное сглаживание: изменено сэмплов " + changed + " (вне окрестности излома — " +
                 farChanged + ")");
            if (farChanged != 0)
                Fail("ФИКС 3", "точечное сглаживание затронуло путь вне окрестности излома");
            else
                Line("[OK]   ФИКС 3: излом найден, точечное сглаживание меняет ТОЛЬКО его окрестность");
        }
    }

    // ================================================================== сценарии стенда

    private static double[][] Straight(int n, int dof)
    {
        double[][] p = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double u = i / (double)(n - 1);
            p[i] = new double[dof];
            for (int j = 0; j < dof; j++) p[i][j] = (60.0 - 12.0 * j) * u;
        }
        return p;
    }

    private static double[][] Arc(int n, int dof)
    {
        double[][] p = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double u = i / (double)(n - 1);
            p[i] = new double[dof];
            p[i][0] = 80.0 * u;
            if (dof > 1) p[i][1] = 30.0 * Math.Sin(Math.PI * u);
            if (dof > 2) p[i][2] = 20.0 * Math.Sin(2.0 * Math.PI * u);
            if (dof > 3) p[i][3] = 15.0 * u;
            if (dof > 5) p[i][5] = 25.0 * u;
        }
        return p;
    }

    private static double[][] Short(int n, int dof)
    {
        double[][] p = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double u = i / (double)(n - 1);
            p[i] = new double[dof];
            p[i][0] = 6.0 * u;
            if (dof > 1) p[i][1] = 3.0 * u;
        }
        return p;
    }

    private static double[][] Kink(int n, int dof)
    {
        double[][] p = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double u = i / (double)(n - 1);
            p[i] = new double[dof];
            p[i][0] = u < 0.5 ? 120.0 * u : 60.0 - 40.0 * (u - 0.5);
            if (dof > 1) p[i][1] = u < 0.5 ? 0.0 : 70.0 * (u - 0.5);
            if (dof > 2) p[i][2] = 30.0 * u;
        }
        return p;
    }

    private static double[][] Scara(int n)
    {
        double[][] p = new double[n][];
        for (int i = 0; i < n; i++)
        {
            double u = i / (double)(n - 1);
            p[i] = new[] { 35.0 * u, -20.0 * u, 0.18 * u };
        }
        return p;
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

    private static double[][] Deriv(double[][] q, float[] t)
    {
        int n = q.Length, dof = q[0].Length;
        double[][] d = new double[n][];
        for (int i = 0; i < n; i++) d[i] = new double[dof];
        for (int i = 0; i < n; i++)
        {
            int a = i > 0 ? i - 1 : i, b = i < n - 1 ? i + 1 : i;
            double dt = t[b] - t[a];
            if (dt < 1e-6) dt = 1e-6;
            for (int j = 0; j < dof; j++) d[i][j] = (q[b][j] - q[a][j]) / dt;
        }
        return d;
    }

    private static double MaxColumn(double[][] m, int col)
    {
        double worst = 0.0;
        for (int i = 0; i < m.Length; i++) worst = Math.Max(worst, Math.Abs(m[i][col]));
        return worst;
    }

    private static string Join(int[] values)
    {
        if (values == null || values.Length == 0) return "—";
        StringBuilder sb = new StringBuilder();
        for (int i = 0; i < values.Length && i < 12; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append(values[i]);
        }
        if (values.Length > 12) sb.Append(", …");
        return sb.ToString();
    }

    private static string Pad(string s, int width)
    {
        if (s == null) s = "";
        return s.Length >= width ? s + " " : s + new string(' ', width - s.Length);
    }

    private static string Fmt(Vector3 p)
    {
        return "(" + p.x.ToString("0.00") + ", " + p.y.ToString("0.00") + ", " + p.z.ToString("0.00") + ")";
    }

    private static void Fail(string what, string detail)
    {
        fails++;
        Line("[FAIL] " + what + " — " + detail);
    }

    private static void Line(string text)
    {
        log.AppendLine(text);
        Debug.Log("[DshS20Diag] " + text);
    }

    private static void Save()
    {
        try
        {
            string path = KazistovVvFeatures.FeatureStorage.ReportPath("_dsh_s20_diag.txt");
            File.WriteAllText(path, log.ToString(), new UTF8Encoding(false));
            Debug.Log("[DshS20Diag] отчёт: " + path);
        }
        catch (Exception e) { Debug.LogError("[DshS20Diag] отчёт не записан: " + e.Message); }
    }

    private static void Finish()
    {
        Debug.Log("[DshS20Diag] ИТОГ: " + (fails == 0 ? "все проверки пройдены" : "отказов " + fails));
    }
}
