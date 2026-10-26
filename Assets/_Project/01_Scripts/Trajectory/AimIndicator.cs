using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Индикатор прицела (E5 плана, упрощённый онлайн-вариант):
/// каждый кадр спрашивает Reachability Oracle про точку прицела активного робота
/// и красит маркер: зелёный — достижимо с запасом, жёлтый — предельно/лимит,
/// красный — столкновение или недостижимо. Мир столкновений пересобирается
/// периодически (стробоскопически), чтобы не тратить кадр на полный обход сцены.
/// </summary>
public class AimIndicator : MonoBehaviour
{
    [Header("Маркер цели")]
    public float markerSize = 0.045f;          // «шарик» на конце лазера
    public float markerEmission = 3.2f;        // яркость шарика (на уровне лазера или выше)
    public float worldRebuildInterval = 0.5f;
    [Tooltip("Насколько шарик «сплющивается» по нормали поверхности, когда ПРИЛИП к ней (1 = не сплющивать).")]
    public float surfaceFlatten = 0.55f;
    [Tooltip("Во сколько раз тусклее шарик, когда он ОТВЕДЁН колесом мыши от поверхности.")]
    public float freeEmissionScale = 0.55f;

    [Header("Выбор ветви IK (только при подтверждённой цели)")]
    [Tooltip("Разрешить агенту выбора ветви влиять на робота. Включает поток траекторий, а не сам прицел")]
    public bool allowSeedSelection = false;

    [Header("Запасы")]
    public float clearance = 0.02f;
    public float linkRadius = 0.06f;

    [Header("Целевая точка робота (как в потоке этапов)")]
    [Tooltip("Смещение проверяемой точки от поверхности вдоль её нормали: оракул проверяет именно ту " +
             "точку, куда поедет робот (поверхность + нормаль * toolOffset). ПО УМОЛЧАНИЮ 0 — оракул " +
             "проверяет ровно точку попадания луча (hit.point). Значение синхронизируется с потоком " +
             "этапов при старте (`FreeFlyCameraController.Awake`).")]
    public float toolOffset = 0f;
    [Tooltip("Смещать проверяемую точку вдоль НОРМАЛИ поверхности (основное). " +
             "Выключено — временный хак: смещение вверх по Y.")]
    public bool offsetAlongNormal = true;

    private readonly CollisionWorld world = new CollisionWorld();
    private readonly ReachabilityOracle oracle = new ReachabilityOracle();
    private readonly PoseValidator validator = new PoseValidator();
    private readonly IkSolver ik = new IkSolver();
    private readonly PostureSelector posture = new PostureSelector();
    private GameObject marker;
    private Renderer markerRenderer;
    private Material markerMaterial;
    private float nextRebuild;
    private RobotController lastRobot;
    private Vector3 lastSelectionPoint = new Vector3(9999f, 9999f, 9999f);

    public ReachResult Last { get; private set; }
    public bool HasResult { get; private set; }
    public string LastBranch { get; private set; } = "";

    private void Awake()
    {
        oracle.clearance = clearance;
        oracle.linkRadius = linkRadius;
        CreateMarker();
    }

    private void CreateMarker()
    {
        marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        marker.name = "AimMarker";
        marker.hideFlags = HideFlags.HideInHierarchy;   // служебный маркер — не в иерархии
        Collider col = marker.GetComponent<Collider>();
        if (col != null)
        {
            // Сначала ВЫКЛЮЧАЕМ (Destroy сработает только в конце кадра): иначе в первом кадре
            // луч прицела попадал в собственный маркер и «прилипал» к нему.
            col.enabled = false;
            Destroy(col);
        }
        marker.transform.localScale = Vector3.one * markerSize;

        // Материал метки — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
        markerMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
            "Материал_прицела", Color.white);
        markerRenderer = marker.GetComponent<Renderer>();
        if (markerRenderer != null)
        {
            markerRenderer.material = markerMaterial;
            markerRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            markerRenderer.receiveShadows = false;
        }
        marker.SetActive(false);
    }

    private void Update()
    {
        RobotController robot = ResolveRobot();
        if (robot != lastRobot)
        {
            lastRobot = robot;
            oracle.Init(robot, world);
            validator.Init(robot);
            ik.Init(validator);
            posture.Init(validator);
            world.Rebuild(robot, linkRadius);
            nextRebuild = 0f;
            HasResult = false;
        }

        if (robot == null) { if (marker != null) marker.SetActive(false); return; }

        if (Time.realtimeSinceStartup >= nextRebuild)
        {
            world.Rebuild(robot, linkRadius);
            nextRebuild = Time.realtimeSinceStartup + worldRebuildInterval;
        }
    }

    /// <summary>
    /// Робот для оракула. При старте ничего не выбрано, поэтому работаем так же,
    /// как поток «два лазера»: выбор оператора → ближайший к точке прицела
    /// (с гистерезисом 0.5 юнита, чтобы робот не «мигал» между стендами).
    /// </summary>
    private RobotController ResolveRobot()
    {
        RobotController active = null;
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        var list = new System.Collections.Generic.List<RobotController>();
        foreach (RobotController rc in robots)
        {
            if (rc == null) continue;
            if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue; // фантомы
            list.Add(rc);
            if (active == null && rc.isActive) active = rc;
        }
        if (active != null) return active;
        if (list.Count == 0) return null;
        if (list.Count == 1 || !hasAim) return list[0];

        RobotController best = null;
        float bestD = float.MaxValue;
        foreach (RobotController rc in list)
        {
            float d = Vector3.Distance(rc.transform.position, lastAimPoint);
            if (d < bestD) { bestD = d; best = rc; }
        }
        if (lastRobot != null && list.Contains(lastRobot) && best != lastRobot)
        {
            float current = Vector3.Distance(lastRobot.transform.position, lastAimPoint);
            if (current - bestD < 0.5f) return lastRobot;   // гистерезис
        }
        return best;
    }

    private Vector3 lastAimPoint;
    private bool hasAim;
    private bool suspended;      // режим перемещения точки: шарик прицела не показываем (точку ведёт HUD)

    // Состояние шарика (глубина по колесу мыши): «прилип к поверхности» или отведён от неё.
    private bool onSurfaceNow;
    private Vector3 normalNow = Vector3.up;

    /// <summary>Шарик «прилип» к поверхности (визуал: сплющен по нормали) — для проверок.</summary>
    public bool BallOnSurface => onSurfaceNow;
    /// <summary>Мировая нормаль поверхности под шариком (для проверок/визуала).</summary>
    public Vector3 BallSurfaceNormal => normalNow;
    /// <summary>Текущий масштаб маркера (для автопроверки «сплющен / ровный шар»).</summary>
    public Vector3 MarkerScale => marker != null ? marker.transform.localScale : Vector3.zero;
    /// <summary>Виден ли шарик прицела (для проверок).</summary>
    public bool MarkerVisible => marker != null && marker.activeSelf;

    /// <summary>
    /// Приостановить индикатор прицела (используется в режиме перемещения точки: там точку
    /// показывает PointMoveHud, а наведение мышью не действует). Оракул в это время не опрашивается.
    /// </summary>
    public void SetSuspended(bool value)
    {
        suspended = value;
        if (suspended && marker != null) marker.SetActive(false);
    }

    /// <summary>Вызывается контроллером камеры: точка прицела и попадание в поверхность.</summary>
    public void UpdateAim(Vector3 aimPoint, bool hitSurface)
    {
        UpdateAim(aimPoint, hitSurface, Vector3.up, false);
    }

    /// <summary>
    /// То же, но с нормалью поверхности под прицелом: оракул проверяет ТУ ЖЕ точку, что
    /// зафиксирует поток этапов (точка поверхности + toolOffset вдоль нормали), а шарик
    /// остаётся на поверхности — оператор видит, куда наводит.
    /// </summary>
    public void UpdateAim(Vector3 aimPoint, bool hitSurface, Vector3 surfaceNormal, bool onSurface)
    {
        UpdateAim(aimPoint, hitSurface, hitSurface, surfaceNormal, onSurface);
    }

    /// <summary>
    /// Полное обновление прицела (после появления управления глубиной колесом мыши):
    ///   ballExists — шарик существует и его надо показывать (у него ВСЕГДА есть глубина,
    ///                даже если луч ушёл в пустоту);
    ///   hitSurface — луч встретил поверхность (реальную геометрию или рабочую плоскость y = 0);
    ///   surfaceNormal — нормаль поверхности под шариком (для смещения TCP и визуала «прилип»);
    ///   onSurface — шарик ПРИКЛЕЕН к поверхности (стоит ровно на ней; отведённый колесом — нет).
    /// Оракул проверяет ту же точку, что зафиксирует поток: приклеенный шарик — точка поверхности
    /// + toolOffset вдоль нормали, отведённый — он сам (точка свободного пространства, без смещения).
    /// </summary>
    public void UpdateAim(Vector3 aimPoint, bool ballExists, bool hitSurface, Vector3 surfaceNormal, bool onSurface)
    {
        lastAimPoint = aimPoint;
        hasAim = true;
        normalNow = surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal.normalized : Vector3.up;
        onSurfaceNow = ballExists && hitSurface && onSurface;

        if (suspended || !ballExists)
        {
            if (marker != null) marker.SetActive(false);
            HasResult = false;
            return;
        }
        if (lastRobot == null || !oracle.Ready)
        {
            if (marker != null) marker.SetActive(false);
            HasResult = false;
            return;
        }

        Vector3 queryPoint = TargetPoint(aimPoint, normalNow, onSurfaceNow);
        Last = oracle.Query(queryPoint);
        HasResult = true;
        if (marker != null)
        {
            marker.SetActive(true);
            marker.transform.position = aimPoint;
            ApplyColor(Last.verdict);
        }

        // Выбор «удобной» ветви IK (posture locking). ВАЖНО: влияет на робота только
    // когда это разрешено (allowSeedSelection) — иначе без подтверждённой точки
    // робот не должен никуда тянуться.
        if (allowSeedSelection && lastRobot is SixAxisController six && ik.Ready && validator.Ready)
        {
            if ((queryPoint - lastSelectionPoint).sqrMagnitude > 0.0004f) // > 2 см
            {
                lastSelectionPoint = queryPoint;
                double[] qNow = validator.CopyCurrent();
                var branches = ik.SolveAll(queryPoint, qNow);
                if (branches.Count > 0)
                {
                    IkSolution best = posture.Select(branches, qNow);
                    if (best.q != null && best.withinLimits)
                    {
                        six.SetPreferredSeed(best.q);
                        LastBranch = best.tag + " (ветвей " + branches.Count + ")";
                    }
                }
            }
        }
    }

    /// <summary>
    /// Точка, которую проверяет оракул: ровно то же правило, что и в потоке этапов —
    /// точка на РЕАЛЬНОЙ поверхности смещается на toolOffset вдоль нормали
    /// (при `offsetAlongNormal = false` — временный хак: вверх по Y);
    /// в свободном пространстве смещения нет, проверяется сама точка.
    /// С 13.09.2026 `toolOffset = 0` по умолчанию → проверяется ровно точка попадания луча.
    /// </summary>
    private Vector3 TargetPoint(Vector3 surfacePoint, Vector3 surfaceNormal, bool onSurface)
    {
        if (!onSurface) return surfacePoint;
        float off = Mathf.Max(0f, toolOffset);
        bool alongNormal = offsetAlongNormal && surfaceNormal.sqrMagnitude > 1e-6f;
        Vector3 dir = alongNormal ? surfaceNormal.normalized : Vector3.up;
        return surfacePoint + dir * off;
    }

    private void ApplyColor(ReachVerdict verdict)
    {
        if (markerMaterial == null) return;
        // Ядовитая неоновая палитра: кислотно-зелёный / ядовито-оранжевый / неон-маджента.
        Color c = verdict == ReachVerdict.Safe ? new Color(0.45f, 1f, 0.02f)
            : verdict == ReachVerdict.Marginal ? new Color(1f, 0.72f, 0f)
            : new Color(1f, 0.03f, 0.42f);
        // Цвет всегда показывает вердикт оракула; «прилип / отведён» видно по форме и яркости:
        // на поверхности шарик сплющен по её нормали (как прижатый), в воздухе — ровный и тусклее.
        float emission = onSurfaceNow ? markerEmission : markerEmission * Mathf.Clamp(freeEmissionScale, 0.05f, 1f);
        GhostMaterial.MakeNeon(markerMaterial, c, emission);
        markerPulse += Time.deltaTime * 6f;
        float k = 1f + 0.18f * Mathf.Sin(markerPulse);
        if (marker != null)
        {
            if (onSurfaceNow)
            {
                marker.transform.rotation = Quaternion.FromToRotation(Vector3.up, normalNow);
                marker.transform.localScale = new Vector3(markerSize * k,
                    markerSize * k * Mathf.Clamp(surfaceFlatten, 0.15f, 1f), markerSize * k);
            }
            else
            {
                marker.transform.rotation = Quaternion.identity;
                marker.transform.localScale = Vector3.one * (markerSize * k);
            }
        }
    }

    private float markerPulse;
}
