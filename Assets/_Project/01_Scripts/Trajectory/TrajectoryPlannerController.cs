using System.Collections.Generic;
using System.Diagnostics;
using TrajectoryCore;
using UnityEngine;
using UnityEngine.InputSystem;
using Debug = UnityEngine.Debug;

/// <summary>
/// Оркестратор планирования (E2–E5 плана) на камере оператора:
///   P            — построить фантомные траектории к точке прицела активного робота;
///   1 / 2 / 3    — исполнить выбранный вариант (через SafetyGate);
///   Esc          — отменить (очистить фантомы, остановить исполнение);
///   F9           — автотест: N случайных целей, сводка метрик в консоль.
/// Никаких LLM: только кинематика, коллизии, планировщик и safety-слой.
/// </summary>
[System.Obsolete("Мёртвый код: не размещён ни в одной сцене (проверено §27, поиск по MainScene/XrMain). " +
    "Его роль выполняет TrajectoryFlowController + KvPlanKit/MotionExecutor (модуль функций). " +
    "Удалить после того, как владелец подтвердит ненужность (аудит CODE_AUDIT, §5.7).")]
public class TrajectoryPlannerController : MonoBehaviour
{
    [Header("Параметры ядра")]
    public int maxCandidates = 3;
    public float clearance = 0.02f;
    public float linkRadius = 0.06f;
    public int selfTestTargets = 20;
    public bool logEachTarget = false;

    private readonly CollisionWorld world = new CollisionWorld();
    private readonly PoseValidator validator = new PoseValidator();
    private readonly Planner planner = new Planner();
    private readonly ReachabilityOracle oracle = new ReachabilityOracle();
    private readonly SafetyGate gate = new SafetyGate();
    private readonly PlanMetrics metrics = new PlanMetrics();

    private GhostView ghostView;
    private TrajectoryExecutor executor;
    private RobotController robot;
    private List<PlannedTrajectory> candidates = new List<PlannedTrajectory>();
    private Vector3 aimPoint;
    private bool aimValid;
    private float worldRefreshTimer;

    public bool HasCandidates => candidates.Count > 0;

    private void Awake()
    {
        ghostView = gameObject.AddComponent<GhostView>();
        metrics.logEveryTarget = logEachTarget;
        gate.NotifyState();
    }

    /// <summary>Точка прицела от камеры (каждый кадр).</summary>
    public void UpdateAim(Vector3 point, bool hitSurface)
    {
        aimPoint = point;
        aimValid = hitSurface;
    }

    private static bool KeyDownLegacy(KeyCode code, out bool value)
    {
        try { value = Input.GetKeyDown(code); } catch { value = false; }
        return value;
    }

    private bool Key(KeyCode code)
    {
        if (Keyboard.current != null)
        {
            switch (code)
            {
                case KeyCode.P: return Keyboard.current.pKey.wasPressedThisFrame;
                case KeyCode.Alpha1: return Keyboard.current.digit1Key.wasPressedThisFrame;
                case KeyCode.Alpha2: return Keyboard.current.digit2Key.wasPressedThisFrame;
                case KeyCode.Alpha3: return Keyboard.current.digit3Key.wasPressedThisFrame;
                case KeyCode.Escape: return Keyboard.current.escapeKey.wasPressedThisFrame;
                case KeyCode.F9: return Keyboard.current.f9Key.wasPressedThisFrame;
            }
        }
        try { return Input.GetKeyDown(code); } catch { return false; }
    }

    private void Update()
    {
        // Стробоскопическое обновление мира столкновений.
        worldRefreshTimer -= Time.deltaTime;
        RobotController active = FindActiveRobot();
        if (active != robot)
        {
            robot = active;
            Reinit();
        }
        if (robot == null) return;

        if (worldRefreshTimer <= 0f)
        {
            world.Rebuild(robot, linkRadius);
            worldRefreshTimer = 0.5f;
        }
        gate.NotifyState(); // «живые» данные: watchdog получает отметку каждый кадр

        // Исполнение завершилось — возвращаем контроллер робота в работу.
        if (executor != null && !executor.IsRunning && robot != null && !robot.enabled)
            robot.enabled = true;

        if (Key(KeyCode.P)) PlanToAim();
        if (Key(KeyCode.Alpha1)) Execute(0);
        if (Key(KeyCode.Alpha2)) Execute(1);
        if (Key(KeyCode.Alpha3)) Execute(2);
        if (Key(KeyCode.Escape)) Cancel();
        if (Key(KeyCode.F9)) RunSelfTest();
    }

    private void Reinit()
    {
        validator.Init(robot);
        validator.linkRadius = linkRadius;
        oracle.Init(robot, world);
        oracle.clearance = clearance;
        oracle.linkRadius = linkRadius;
        planner.Init(validator, world);
        planner.clearance = clearance;
        planner.maxIterations = 700;   // держим планирование в пределах кадра (~сотни мс)
        planner.stepSizeDeg = 15.0;
        gate.NotifyState();

        executor = robot != null ? robot.GetComponent<TrajectoryExecutor>() : null;
        if (executor == null && robot != null)
            executor = robot.gameObject.AddComponent<TrajectoryExecutor>();
        if (executor != null) executor.Init(validator, world, gate);

        ghostView.Clear();
        candidates.Clear();
    }

    private void PlanToAim()
    {
        if (robot == null || !aimValid || !validator.Ready || !planner.Ready)
        {
            KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Планирование: нет робота/цели", Color.gray);
            return;
        }

        var sw = Stopwatch.StartNew();
        var r = oracle.Query(aimPoint);
        metrics.RecordOracle(sw.Elapsed.TotalMilliseconds);

        double[] start = validator.CopyCurrent();
        sw.Restart();
        candidates = planner.Plan(start, aimPoint, maxCandidates, 12345) ?? new List<PlannedTrajectory>();
        sw.Stop();
        PlannedTrajectory best = candidates.Count > 0 ? candidates[0] : null;
        metrics.RecordPlan(sw.Elapsed.TotalMilliseconds, best != null, best);

        if (candidates.Count == 0)
        {
            KazistovVvUI.KazistovVvUIManager.SetPlanStatus(
                "План не найден (" + r.reason + ")", new Color(1f, 0.35f, 0.3f));
            ghostView.Clear();
            return;
        }

        ghostView.Show(candidates, validator);
        string txt = "Вариантов: " + candidates.Count + " · 1/2/3 — исполнить";
        if (best != null)
            txt += string.Format(" · лучший: {0:F2} с, зазор {1:F0} мм", best.Time, best.MinClearance * 1000f);
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus(txt, new Color(0.35f, 1f, 0.45f));
        Debug.Log("[Planner] " + txt);
    }

    private void Execute(int index)
    {
        if (executor == null || index >= candidates.Count) return;
        PlannedTrajectory t = candidates[index];
        gate.NotifyState();
        if (!executor.Play(t))
        {
            KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Safety: " + executor.StatusText, new Color(1f, 0.35f, 0.3f));
            return;
        }
        if (robot != null) robot.enabled = false; // не даём IK-контроллеру бороться с исполнителем
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Исполнение: " + t.Label, new Color(0.35f, 1f, 0.45f));
    }

    private void Cancel()
    {
        executor?.Stop(SafetyReason.OperatorStop);
        if (robot != null) robot.enabled = true;
        ghostView.Clear();
        candidates.Clear();
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus("Планирование отменено", Color.gray);
    }

    private void RunSelfTest()
    {
        metrics.logEveryTarget = logEachTarget;
        string s = metrics.SelfTest(robot, validator, world, oracle, planner, selfTestTargets);
        metrics.SaveCsv();
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus(s, new Color(0.8f, 0.9f, 1f));
        Debug.Log("[Metrics] " + s);
    }

    private static RobotController FindActiveRobot()
    {
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        foreach (RobotController rc in robots)
            if (rc != null && rc.isActive) return rc;
        return robots.Length > 0 ? robots[0] : null;
    }
}
