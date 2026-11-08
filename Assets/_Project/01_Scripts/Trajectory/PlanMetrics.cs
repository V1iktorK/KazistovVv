using System.Collections.Generic;
using System.Diagnostics;
using TrajectoryCore;
using UnityEngine;
using Debug = UnityEngine.Debug;

/// <summary>
/// Метрики и автотест (E5 плана): замер латентностей оракула/планировщика, success rate,
/// длина пути, время, минимальный зазор, запас до лимитов, манипулируемость,
/// σ_min, число отклонений Safety. Отчёт — В КОНСОЛЬ (CSV не пишется, см. `SaveCsv`).
///
/// P3-5 (11.11.2026). ЧТО БЫЛО. В шапке было обещано «отчёт в консоль и в CSV», а поле `rows`
/// не пополнялось ни одним `rows.Add(...)`: `SaveCsv()` ВСЕГДА выходил на `if (rows.Count == 0)
/// return;` — то есть файла не появлялось НИКОГДА, и при этом `TrajectoryPlannerController.
/// RunSelfTest` вызывал его как будто записал отчёт.
/// ЧЕМ ЭТО ГРОЗИЛО. Оператор искал `plan_metrics.csv` в каталоге отчётов и не находил, а
/// «CSV-отчёт есть» оставалось неправдой месяцами: пустой список выглядел как «данных пока нет».
/// ТЕПЕРЬ обещание снято честно: шапка говорит «в консоль», `SaveCsv` помечен устаревшим.
/// </summary>
public class PlanMetrics : MonoBehaviour
{
    public int selfTestTargets = 60;
    public bool logEveryTarget = false;

    // P3-5 (11.11.2026): список строк CSV не пополняется ни одним rows.Add — из-за этого
    // SaveCsv всегда выходил на «rows.Count == 0». Поле оставлено (не удаляем молча): его
    // заполнение — отдельное решение владельца, а обещание «CSV» с SaveCsv уже снято.
    private readonly List<string> rows = new List<string>();
    private int plans, ok, rejected;
    private double latencyOracleSum, latencyPlanSum, latencyOracleMax, latencyPlanMax;
    private int latencySamples;
    private double pathLenSum, timeSum;
    private float minClearanceWorst = float.MaxValue;
    private float limitMarginWorst = float.MaxValue;

    public void RecordOracle(double ms)
    {
        latencyOracleSum += ms;
        latencyOracleMax = System.Math.Max(latencyOracleMax, ms);
    }

    public void RecordPlan(double ms, bool success, PlannedTrajectory best)
    {
        latencySamples++;
        latencyPlanSum += ms;
        latencyPlanMax = System.Math.Max(latencyPlanMax, ms);
        plans++;
        if (success && best != null)
        {
            ok++;
            pathLenSum += best.Length;
            timeSum += best.Time;
            minClearanceWorst = Mathf.Min(minClearanceWorst, best.MinClearance);
            limitMarginWorst = Mathf.Min(limitMarginWorst, best.LimitMargin);
            if (logEveryTarget)
                Debug.Log(string.Format("[Metrics] цель {0}: time={1:F2} с, length={2:F2}, зазор={3:F0} мм, σmin={4:F3}",
                    plans, best.Time, best.Length, best.MinClearance * 1000f, best.SigmaMin));
        }
        else rejected++;
    }

    public void PrintSummary(string title = "Итоги планирования")
    {
        double avgOracle = latencySamples > 0 ? latencyOracleSum / System.Math.Max(1, plans) : 0;
        double avgPlan = plans > 0 ? latencyPlanSum / plans : 0;
        double successRate = plans > 0 ? 100.0 * ok / plans : 0;
        Debug.Log(string.Format(
            "[Metrics] {0}: планов={1}, успех={2:F1}%, отклонено={3}\n" +
            "  латентность оракула: avg={4:F2} мс, max={5:F2} мс\n" +
            "  латентность плана:  avg={6:F1} мс, max={7:F1} мс\n" +
            "  среднее время движения={8:F2} с, средняя длина={9:F2}\n" +
            "  худший зазор={10:F0} мм, худший запас до лимитов={11:F1}°",
            title, plans, successRate, rejected,
            avgOracle, latencyOracleMax, avgPlan, latencyPlanMax,
            ok > 0 ? timeSum / ok : 0, ok > 0 ? pathLenSum / ok : 0,
            minClearanceWorst == float.MaxValue ? 0 : minClearanceWorst * 1000f,
            limitMarginWorst == float.MaxValue ? 0 : limitMarginWorst));
    }

    /// <summary>
    /// P3-5 (11.11.2026). ЧТО БЫЛО. Метод выглядел рабочим, но `rows` не пополняется НИГДЕ
    /// (единственное чтение — условие `rows.Count == 0`), поэтому `SaveCsv` всегда возвращался на
    /// первой строке: обещанного `plan_metrics.csv` в каталоге отчётов не появлялось никогда.
    /// ЧЕМ ЭТО ГРОЗИЛО. «Отчёт сохранён» было ложью без единого признака отказа: файла нет, а
    /// ошибки тоже нет — искать причину негде.
    /// ТЕПЕРЬ: метод помечен устаревшим и НИЧЕГО не обещает. Он оставлен (правило проекта —
    /// сначала метка с причиной, удаление — отдельным решением владельца), а не вызывает его
    /// больше никто: сводка идёт в журнал через `PrintSummary`.
    /// </summary>
    [System.Obsolete("P3-5: CSV не пишется — поле rows не пополняется, файла не появляется никогда. " +
        "Сводку печатает PrintSummary в журнал; заполнение CSV — отдельная задача владельца.")]
    public void SaveCsv()
    {
        if (rows.Count == 0) return;
        // ФИКС 11: файл метрик — отчётный, поэтому идёт в ЕДИНЫЙ каталог отчётов
        // (<persistentDataPath>/KazistovVv/Reports), а не в корень persistentDataPath.
        string path = KazistovVvFeatures.FeatureStorage.ReportPath("plan_metrics.csv");
        System.IO.File.WriteAllLines(path, rows.ToArray());
        Debug.Log("[Metrics] CSV: " + path);
    }

    /// <summary>
    /// Автотест: N случайных точек в рабочей зоне вокруг робота → оракул → планировщик.
    /// Возвращает строку-сводку (для UI/лога), исполнение не запускается.
    /// </summary>
    public string SelfTest(RobotController robot, PoseValidator validator, CollisionWorld world,
        ReachabilityOracle oracle, Planner planner, int targets)
    {
        if (robot == null || validator == null || !validator.Ready)
            return "Автотест невозможен: нет робота/валидатора";

        var rng = new System.Random(20260101);
        Vector3 center = robot.transform.position + Vector3.up * 0.35f;
        int done = 0, successful = 0, oracleSafe = 0, oracleFalse = 0;
        for (int i = 0; i < targets; i++)
        {
            Vector3 p = center + new Vector3(
                (float)(rng.NextDouble() * 1.2 - 0.6),
                (float)(rng.NextDouble() * 0.6 - 0.1),
                (float)(rng.NextDouble() * 1.2 - 0.6));

            var sw = Stopwatch.StartNew();
            ReachResult r = oracle.Query(p);
            sw.Stop();
            RecordOracle(sw.Elapsed.TotalMilliseconds);
            if (r.verdict == ReachVerdict.Safe) oracleSafe++;

            double[] start = validator.CopyCurrent();
            sw.Restart();
            List<PlannedTrajectory> cand = planner.Plan(start, p, 2, 1000 + i);
            sw.Stop();
            PlannedTrajectory best = cand != null && cand.Count > 0 ? cand[0] : null;
            RecordPlan(sw.Elapsed.TotalMilliseconds, best != null, best);
            done++;
            if (best != null) successful++;
            // Оракул сказал «зелёный», а плана нет — ложное «зелёное» (недопустимо).
            if (r.verdict == ReachVerdict.Safe && best == null) oracleFalse++;
        }

        string summary = string.Format("Автотест: {0} целей, план найден {1}, «зелёных» оракула {2}, ложных «зелёных» {3}",
            done, successful, oracleSafe, oracleFalse);
        PrintSummary("Автотест " + targets + " целей");
        return summary;
    }
}
