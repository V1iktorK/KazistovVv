using KazistovVvKinematics;
using UnityEngine;

/// <summary>
/// Контроллер 6-осного манипулятора на базе честной кинематики
/// по Денавиту–Хартенбергу (DH):
///   * Прямая задача — иерархия трансформов (Unity-цепочка) + DHForward (RobotDH);
///   * Обратная задача — ОСЕВОЙ CCD (RobotDH.DHInverse): каждый сустав
///     поворачивается строго вокруг СВОЕЙ оси вращения (jointAxesLocal),
///     без «свободных» вращений из старой версии (InverseKinematics больше не решает);
///   * Ограничители и отчёт углов — тоже вдоль осей, как у реального контроллера.
///
/// Оси по умолчанию для модели Robot.fbx (замерено диагностикой по геометрии):
///   J1 (Axis1) — вертикаль (yaw базы);        J2..J4 — поперечные оси (плоскость руки),
///   J5 (Axis5) — yaw запястья;                J6 (Axis6) — roll фланца (вдоль инструмента,
///   локальная ось Y Axis6 повёрнута моделью на 89° и указывает вдоль -X).
/// Если поставить другой робот — оси правятся в инспекторе (или включите
/// AutoDetectAxesFromLinks для автоподбора по перекрёстным произведениям звеньев).
/// </summary>
public class SixAxisController : RobotController
{
    [Header("6-Axis IK (DH, axis CCD)")]
    [Tooltip("Ссылка на legacy-компонент InverseKinematics (не используется для расчёта, оставлена для совместимости).")]
    public InverseKinematics ik;
    public Transform[] jointTransforms;
    public Transform baseTransform;
    public string baseName = "LS10-B702S_base_1";

    [Header("DH joint axes")]
    [Tooltip("Оси вращения суставов в ЛОКАЛЬНЫХ координатах каждого сустава. Для Robot.fbx: J1 — вертикаль (yaw), J2/J3 — поперечные (плечо/локоть), J4 — ВДОЛЬ ПРЕДПЛЕЧЬЯ (roll запястья!), J5 — поперечная (pitch кисти), J6 — roll фланца. Если ось J4 всё ещё Z (forward) — на первом решении она автоматически пересчитается вдоль звена 3→4.")]
    public Vector3[] jointAxesLocal = new Vector3[]
    {
        Vector3.up,      // Axis1 — yaw базы
        Vector3.forward, // Axis2 — плечо (в плоскости руки)
        Vector3.forward, // Axis3 — локоть
        new Vector3(-0.984f, 0.179f, 0f).normalized, // Axis4 — roll ЗАПЯСТЬЯ вдоль предплечья
        Vector3.forward, // Axis5 — pitch кисти (⊥ предплечью)
        Vector3.up       // Axis6 — roll фланца (локальная Y оси = вдоль инструмента)
    };
    [Tooltip("Автоподбор осей по геометрии (cross-произведения звеньев) при первом решении. Для Robot.fbx не требуется.")]
    public bool autoDetectAxesFromLinks = false;
    [Tooltip("Компенсация «кривых» пивотов FBX: меш предплечья (Axis4_2) переносится с запястья (Axis4) на локоть (Axis3), чтобы балка не вращалась вместе с roll-запястьем (артефакты/расстыковка с Axis3_2)")]
    public bool fixLegacyMeshParents = true;

    [Header("Smoothing")]
    [Range(0f, 0.99f)] public float positionSmoothing = 0.85f;
    [Range(0f, 0.99f)] public float rotationSmoothing = 0.9f;

    [Header("Joint Limits (degrees, along joint axes)")]
    public Vector2[] jointLimits = new Vector2[6];

    [Header("Self-collision guard")]
    [Tooltip("Разрешить кинематический самоколлизионный запрет движения")]
    public bool enableSelfCollisionGuard = true;
    [Tooltip("Радиус звеньев для проверки самопересечения звеньев")]
    public float linkRadius = 0.07f;
    [Tooltip("При коллизии откатывать только конфликтующие суставы (иначе стоп всего движения)")]
    public bool selectiveRollback = true;
    [Tooltip("Сколько секунд подряд может длиться самостолкновение, прежде чем цель будет снята совсем " +
             "(робот останавливается и не «дрожит» у препятствия).")]
    public float selfCollisionStopTime = 0.25f;

    // Плавная цель (движется к заданной со скоростью сглаживания).
    private Vector3 smoothedTargetPosition;
    private bool smoothingInitialized;
    private bool selfCollisionSetup;
    private bool collisionSkipLogged;   // предупреждение «коллайдеры не построены» — только один раз
    private bool jointLimitsInitialized;
    // TCP-прокси: у CAD-мешей пивот часто в начале координат модели, поэтому
    // реальная точка инструмента берётся как центр меша фланца.
    private Transform tcpProxy;
    private bool refsReported;
    private bool meshesFixed;

    // Предпочтительная ветвь IK (селектор позы): CCD стартует с неё, поэтому робот
    // приходит в «удобную» конфигурацию, а не выворачивается.
    private double[] preferredSeed;
    private bool seedPending;

    /// <summary>Задать предпочтительную конфигурацию (ветвь IK) для следующей цели.</summary>
    public void SetPreferredSeed(double[] q)
    {
        if (q == null || jointTransforms == null || q.Length < jointTransforms.Length) return;
        preferredSeed = (double[])q.Clone();
        seedPending = true;
    }

    /// <summary>True, если задан непотреблённый сид (для диагностики).</summary>
    public bool HasPendingSeed => seedPending;

    private void ApplyPreferredSeed()
    {
        if (!seedPending || preferredSeed == null || jointTransforms == null) return;
        seedPending = false;
        EnsureAxisCache();
        int n = Mathf.Min(jointTransforms.Length, preferredSeed.Length);
        for (int i = 0; i < n; i++)
            SetJointAngle(i, (float)preferredSeed[i]);
    }
    private readonly Quaternion[] rollbackPose = new Quaternion[6];

    // Кэш DH-осей: q0 — «нулевая» поза сустава (при старте/первом решении),
    // u = q0*axisLocal — ось измерения/возврата в локальных координатах.
    private Quaternion[] jointBaseRot = new Quaternion[6];
    private Vector3[] jointAxisUnit = new Vector3[6];
    private Vector3[] jointRefLocal = new Vector3[6];
    private int cachedJointCount = -1;

    public bool SelfCollisionBlocked { get; private set; }
    private float selfCollisionBlockedTime;

    void Update()
    {
        ResolveRobotReferences();
        if (!meshesFixed)
        {
            FixLegacyMeshParents();
            meshesFixed = true;
        }
        SetupSelfCollision();
        MoveToTarget(Time.deltaTime);
        ApplyJointLimits();
        UpdateTelemetry(Time.deltaTime);
    }

    private void SetupSelfCollision()
    {
        if (selfCollisionSetup)
            return;

        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            // ДИАГНОСТИКА (02.11.2026): раньше здесь стоял «тихий» `return`, и по журналу нельзя
            // было понять, почему луч проходит сквозь робота. Теперь причина видна.
            if (!collisionSkipLogged)
            {
                collisionSkipLogged = true;
                Debug.LogWarning("[SixAxisController] jointTransforms пуст — строю коллайдеры " +
                                 "ПО ГАБАРИТАМ модели, чтобы луч всё равно упирался в робота");
            }

            // ЗАПАСНОЙ ПУТЬ (02.11.2026, пункт 2 владельца: «лазеры … не упираются в роботов»).
            // Даже без суставов робот обязан ловить луч: ставим боксы по габаритам геометрии.
            var gap = GetComponent<RobotSelfCollision>();
            if (gap == null) gap = gameObject.AddComponent<RobotSelfCollision>();
            gap.BuildFromRenderers();
            if (gap.ColliderCount > 0) selfCollisionSetup = true;
            return;
        }

        var collision = GetComponent<RobotSelfCollision>();
        if (collision == null)
            collision = gameObject.AddComponent<RobotSelfCollision>();

        collision.jointTransforms = jointTransforms;
        collision.disableSelfCollisions = true;
        collision.jointColliderRadius = 0.12f;   // было 0.08: луч проскакивал между суставами
        selfCollisionSetup = true;

        // ПРИНУДИТЕЛЬНО ПОСТРОИТЬ КОЛЛАЙДЕРЫ ПРЯМО СЕЙЧАС (исправлено 02.11.2026,
        // пункт 2 владельца: «лазеры … не упираются в роботов»).
        //
        // ЧТО БЫЛО. `RobotSelfCollision` строит коллайдеры в своём `Start()`. Компонент
        // добавляется в РАНТАЙМЕ из этого метода. `Start()` у только что добавленного компонента
        // выполняется в конце кадра — то есть уже ПОСЛЕ того, как луч за этот кадр пройдёт
        // сквозь робота. А если у компонента к моменту `Start()` оказывался пустой
        // `jointTransforms`, он вообще тихо выходил с предупреждением.
        //
        // Проверка журнала шлема 02.11.2026: строк `[RobotSelfCollision]` и `Collider_*` НЕТ
        // ВООБЩЕ, а лазер упирался только в стену ангара («Hangar_part_02») — значит коллайдеры
        // робота не появлялись.
        //
        // ТЕПЕРЬ коллайдеры строятся сразу, в этом же кадре, с уже заполненным `jointTransforms`.
        // `BuildNow()` идемпотентен: повторный вызов ничего не дублирует.
        collision.BuildNow();
    }

    private void ResolveRobotReferences()
    {
        Transform root = transform;

        // --- endEffector ---
        if (endEffector == null)
        {
            endEffector = FindChild(root, endEffectorName);
            if (endEffector == null)
                endEffector = FindChild(root, alternativeEndEffectorName);
            if (endEffector == null)
                endEffector = FindDeepestDescendant(root);
        }

        // tcp не назначен в инспекторе → берём фланец (endEffector),
        // чтобы телеоперация/телеметрия не падали с UnassignedReferenceException.
        if (tcp == null)
        {
            tcp = endEffector;
        }

        // Пивот меша фланца может быть в начале координат модели (кривой FBX) —
        // тогда IK «тянет» робота к точке у основания. Делаем TCP-прокси на кончике.
        if (tcpProxy == null && endEffector != null)
        {
            tcpProxy = CreateTcpProxy(endEffector);
            if (tcpProxy != null)
            {
                tcp = tcpProxy;
                Debug.Log("[SixAxis] TCP выставлен на кончик инструмента ('" + tcpProxy.name + "')");
            }
        }

        // --- base: должна быть ПРЕДКОМ endEffector, иначе IK-цепь не строится ---
        if (baseTransform == null)
        {
            baseTransform = FindChild(root, baseName);
            if (baseTransform == null)
                baseTransform = FindChild(root, "Root");
            if (baseTransform == null)
                baseTransform = root;
        }
        if (endEffector != null && !IsAncestorOf(baseTransform, endEffector))
        {
            // Root в этой модели — декоративная тумба (брат оси), а не кинематическая база.
            baseTransform = root;
        }
        fixedBase = baseTransform;

        // --- суставы: если не заданы или указывают на меши — нормализуем ---
        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            jointTransforms = new Transform[]
            {
                FindAxis(root, 1),
                FindAxis(root, 2),
                FindAxis(root, 3),
                FindAxis(root, 4),
                FindAxis(root, 5),
                FindAxis(root, 6)
            };
        }
        NormalizeJointTransforms();

        if (!jointLimitsInitialized)
        {
            InitializeJointLimits();
            jointLimitsInitialized = true;
        }
    }

    /// <summary>
    /// Создаёт TCP-прокси на кончике инструмента: если пивот меша фланца далеко
    /// от его геометрии (типично для CAD-экспорта «всё в начале координат»),
    /// возвращает пустой дочерний объект в центре меша. Иначе — null.
    /// </summary>
    private static Transform CreateTcpProxy(Transform ee)
    {
        if (ee == null) return null;
        Renderer r = ee.GetComponent<Renderer>();
        if (r == null) r = ee.GetComponentInChildren<Renderer>(true);
        if (r == null) return null;

        Vector3 tip = r.bounds.center;
        if ((tip - ee.position).sqrMagnitude < 0.0025f) return null; // пивот уже на геометрии

        GameObject go = new GameObject("TCP");
        go.transform.SetParent(ee, false);
        go.transform.position = tip;
        return go.transform;
    }

    /// <summary>Точка, которую ведёт IK (TCP-прокси либо сам фланец).</summary>
    private Transform IkTip
    {
        get { return tcpProxy != null ? tcpProxy : endEffector; }
    }

    /// <summary>Ищет «настоящую» ось AxisN (приоритет — точное имя, не меш AxisN_2).</summary>
    private static Transform FindAxis(Transform root, int n)
    {
        string plain = "Axis" + n;
        string[] all = { plain, plain.ToLower(), "AXIS" + n };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in all)
            {
                if (t != root && t.name == name)
                    return t;
            }
        }
        // fallback: AxisN_2/_1
        string[] suffixed = { plain + "_2", plain + "_1" };
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            foreach (string name in suffixed)
            {
                if (t != root && t.name == name)
                    return t;
            }
        }
        return null;
    }

    /// <summary>Если суставы указывают на меши (AxisN_2/_1) — поднимаемся к их родителю-оси.</summary>
    private void NormalizeJointTransforms()
    {
        if (jointTransforms == null) return;
        for (int i = 0; i < jointTransforms.Length; i++)
        {
            Transform j = jointTransforms[i];
            if (j == null) continue;
            // Меши вида AxisN_2/_1 — дети настоящих осей AxisN
            while (j.parent != null &&
                   (j.name.EndsWith("_2") || j.name.EndsWith("_1")) &&
                   j.parent.name.StartsWith("Axis"))
            {
                j = j.parent;
            }
            jointTransforms[i] = j;
        }
    }

    /// <summary>True, если candidate — предок target (или равен ему).</summary>
    private static bool IsAncestorOf(Transform candidate, Transform target)
    {
        Transform t = target;
        while (t != null)
        {
            if (t == candidate) return true;
            t = t.parent;
        }
        return false;
    }

    /// <summary>
    /// Компенсация «кривых» начал координат в FBX (все меши экспортированы с пивотом
    /// в одной точке модели): меш-звено, которое по геометрии лежит МЕЖДУ локтем
    /// (Axis3) и запястьем (Axis4), висит на ЗАПЯСТЬЕ (Axis4) и при roll-повороте
    /// запястья «разъезжается»/расстыковывается с корпусом локтя (Axis3_2).
    /// Решение: переносим такой меш на локоть (Axis3) с сохранением мировой позы —
    /// балка становится частью звена 3 и больше не вращается с запястьем.
    /// Для нормальных моделей (правильные пивоты) — не требуется (флаг fixLegacyMeshParents).
    /// </summary>
    private void FixLegacyMeshParents()
    {
        if (!fixLegacyMeshParents) return;
        if (jointTransforms == null || jointTransforms.Length < 4) return;

        Transform elbow = jointTransforms[2];   // Axis3
        Transform wrist = jointTransforms[3];   // Axis4
        if (elbow == null || wrist == null) return;
        if (!IsAncestorOf(elbow, wrist)) return;

        // Прямые дети запястья, у которых есть меш и которые не являются кинематическими осями.
        for (int i = wrist.childCount - 1; i >= 0; i--)
        {
            Transform child = wrist.GetChild(i);
            if (child == null) continue;
            if (child.name == jointTransforms[3].name) continue;
            // Кинематическую ось (Axis5 и т.п.) не трогаем.
            bool isKinematicAxis = false;
            for (int k = 0; k < jointTransforms.Length; k++)
            {
                if (jointTransforms[k] == child) { isKinematicAxis = true; break; }
            }
            if (isKinematicAxis) continue;
            if (child.GetComponent<Renderer>() == null &&
                child.GetComponentInChildren<Renderer>(true) == null) continue;

            // Сохраняем мировую позу и переносим под локоть.
            Vector3 worldPos = child.position;
            Quaternion worldRot = child.rotation;
            child.SetParent(elbow, false);
            child.position = worldPos;
            child.rotation = worldRot;
            Debug.Log("[SixAxis] Меш '" + child.name + "' перенесён с " + wrist.name +
                      " на " + elbow.name + " (компенсация пивота FBX)");
        }
    }

    private void InitializeJointLimits()
    {
        // Если массив пуст/мал ИЛИ все лимиты нулевые (битые значения из инспектора) —
        // заполняем рабочими дефолтами.
        bool allZero = jointLimits != null && jointLimits.Length >= 6;
        if (allZero)
        {
            for (int i = 0; i < 6; i++)
            {
                if (jointLimits[i].x != 0f || jointLimits[i].y != 0f) { allZero = false; break; }
            }
        }
        if (jointLimits == null || jointLimits.Length < 6 || allZero)
            jointLimits = new Vector2[6];

        jointLimits[0] = new Vector2(-170f, 170f);
        jointLimits[1] = new Vector2(-90f, 150f);
        jointLimits[2] = new Vector2(-70f, 225f);
        jointLimits[3] = new Vector2(-180f, 180f);
        jointLimits[4] = new Vector2(-120f, 120f);
        jointLimits[5] = new Vector2(-180f, 180f);
    }

    // ------------------------------------------------------------------
    //  DH-кэш осей
    // ------------------------------------------------------------------

    /// <summary>
    /// Готовит кэш осей по текущей позе суставов (эта поза становится «нулевой»:
    /// все измеренные углы считаются от неё). Семейство поз сустава:
    /// localRotation = AngleAxis(θ, u) * q0, где q0 — поза на момент кэша,
    /// u = q0*axisLocal — ось вращения в локальных координатах сустава.
    /// </summary>
    private void EnsureAxisCache()
    {
        int n = jointTransforms != null ? jointTransforms.Length : 0;
        if (cachedJointCount == n && n > 0)
            return;

        bool changed = cachedJointCount != n;
        if (n == 0)
        {
            cachedJointCount = n;
            return;
        }

        ResizeAxisCache(n);
        cachedJointCount = n;

        if (autoDetectAxesFromLinks && n >= 4)
            AutoDetectAxes();
        else
            AutoFixWristAxis();

        for (int i = 0; i < n; i++)
        {
            Transform j = jointTransforms[i];
            Quaternion q0 = j != null ? j.localRotation : Quaternion.identity;
            Vector3 e = i < jointAxesLocal.Length && jointAxesLocal[i].sqrMagnitude > 0.01f
                ? jointAxesLocal[i].normalized
                : Vector3.up;
            jointBaseRot[i] = q0;
            jointAxisUnit[i] = e;
            Vector3 u = (q0 * e).normalized; // ось в локальных координатах (неподвижна при вращении сустава)
            jointRefLocal[i] = PickPerpendicular(u);
        }

        if (changed || !refsReported)
            LogAxesOnce(n);
    }

    /// <summary>
    /// Миграция «битых» осей запястья (старая схема Y,Z,Z,Z,Y,Y, из-за которой
    /// предплечье Axis4_2 «разворачивалось на 180°» и задевало корпус Axis3_2):
    ///   J4 (индекс 3) был pitch (forward) → пересчитываем ВДОЛЬ ПРЕДПЛЕЧЬЯ
    ///   (roll запястья): направление звена 3→4 в локальных осях сустава 4
    ///   инвариантно к текущей позе;
    ///   J5 (индекс 4) был yaw (up) → становится pitch (forward), ⊥ предплечью.
    /// Ручные настройки не перезаписываются.
    /// </summary>
    private void AutoFixWristAxis()
    {
        if (jointTransforms == null || jointTransforms.Length < 5) return;
        if (jointAxesLocal == null || jointAxesLocal.Length < 5) return;

        // Миграция специфична для «сферического» запястья: J4 == J5 (одна точка).
        Transform j4 = jointTransforms[3];
        Transform j5 = jointTransforms[4];
        if (j4 == null || j5 == null) return;
        if ((j5.position - j4.position).sqrMagnitude > 0.0025f) return; // > 5 см — другая схема

        // J4: ось roll запястья (вдоль предплечья).
        if ((jointAxesLocal[3] - Vector3.forward).sqrMagnitude <= 0.01f)
        {
            Transform j3 = jointTransforms[2];
            if (j3 != null)
            {
                Vector3 worldDir = j4.position - j3.position; // предплечье (Axis3→Axis4)
                if (worldDir.sqrMagnitude > 1e-6f)
                {
                    worldDir.Normalize();
                    jointAxesLocal[3] = j4.InverseTransformDirection(worldDir);
                    Debug.Log("[SixAxis] J4: ось пересчитана вдоль предплечья: " +
                              jointAxesLocal[3].ToString("0.000"));
                }
            }
        }

        // J5: pitch кисти — поперечная ось (forward), а не вертикаль (up).
        if ((jointAxesLocal[4] - Vector3.up).sqrMagnitude <= 0.01f)
        {
            jointAxesLocal[4] = Vector3.forward;
            Debug.Log("[SixAxis] J5: ось пересчитана на pitch (forward)");
        }
    }

    private void ResizeAxisCache(int n)
    {
        if (jointBaseRot.Length != n) jointBaseRot = new Quaternion[n];
        if (jointAxisUnit.Length != n) jointAxisUnit = new Vector3[n];
        if (jointRefLocal.Length != n) jointRefLocal = new Vector3[n];
    }

    /// <summary>
    /// Автоподбор осей по геометрии: J1 — вертикаль; для остальных — нормаль
    /// к плоскости соседних звеньев (cross-произведение), концевые суставы —
    /// вдоль инструмента. Полезно для незнакомых моделей.
    /// </summary>
    private void AutoDetectAxes()
    {
        int n = jointTransforms.Length;
        var links = new Vector3[n];
        for (int i = 0; i < n; i++)
        {
            Vector3 p = jointTransforms[i] != null ? jointTransforms[i].position : Vector3.zero;
            Vector3 prev = i > 0 && jointTransforms[i - 1] != null
                ? jointTransforms[i - 1].position
                : p;
            links[i] = p - prev;
        }

        for (int i = 0; i < n && i < jointAxesLocal.Length; i++)
        {
            Vector3 axis;
            if (i == 0)
            {
                // База: вращение вокруг вертикали (локальная ось Y базы/корпуса).
                axis = Vector3.up;
            }
            else
            {
                Vector3 prevLink = links[i - 1];
                Vector3 curLink = links[i];
                if (curLink.sqrMagnitude < 1e-8f && i < n - 1) curLink = links[i + 1];
                if (prevLink.sqrMagnitude > 1e-8f && curLink.sqrMagnitude > 1e-8f)
                {
                    // Нормаль к плоскости двух соседних звеньев (в локальных осях сустава).
                    Vector3 worldAxis = Vector3.Cross(prevLink, curLink).normalized;
                    Transform j = jointTransforms[i];
                    if (worldAxis.sqrMagnitude < 0.5f && j != null)
                        worldAxis = Vector3.Cross(prevLink, j.TransformDirection(Vector3.forward)).normalized;
                    axis = j != null
                        ? j.InverseTransformDirection(worldAxis)
                        : worldAxis;
                }
                else
                {
                    axis = Vector3.up;
                }
            }

            if (axis.sqrMagnitude < 0.5f)
                axis = Vector3.up;
            jointAxesLocal[i] = axis.normalized;
        }
    }

    private static Vector3 PickPerpendicular(Vector3 u)
    {
        Vector3 seed = Mathf.Abs(u.y) < 0.9f ? Vector3.up : Vector3.right;
        Vector3 r = Vector3.Cross(seed, u).normalized;
        if (r.sqrMagnitude < 0.01f)
            r = Vector3.Cross(Vector3.right, u).normalized;
        return r;
    }

    private void LogAxesOnce(int n)
    {
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.Append("[SixAxis] DH-оси (на старте, в локальных осях суставов): ");
        for (int i = 0; i < n; i++)
        {
            Transform j = jointTransforms[i];
            if (i > 0) sb.Append(", ");
            sb.Append((j != null ? j.name : "J" + (i + 1)) + "=" +
                      jointAxisUnit[i].ToString("0.00").Replace("(", "").Replace(")", ""));
        }
        Debug.Log(sb.ToString());
        refsReported = true;
    }

    /// <summary>Текущий угол сустава i вокруг СВОЕЙ оси (от «нулевой» позы, градусы).</summary>
    private float MeasureJointAngle(int i)
    {
        Transform j = jointTransforms != null && i < jointTransforms.Length ? jointTransforms[i] : null;
        if (j == null) return 0f;
        EnsureAxisCache();
        if (i >= jointBaseRot.Length) return 0f;

        Quaternion q0 = jointBaseRot[i];
        Vector3 u = (q0 * jointAxisUnit[i]).normalized;
        Vector3 r = jointRefLocal[i];
        Quaternion rel = j.localRotation * Quaternion.Inverse(q0);
        Vector3 relR = rel * r;
        Vector3 a = Vector3.ProjectOnPlane(r, u);
        Vector3 b = Vector3.ProjectOnPlane(relR, u);
        if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 0f;
        return Vector3.SignedAngle(a, b, u);
    }

    /// <summary>Возвращает сустав в позу с заданным углом вокруг своей оси.</summary>
    private void SetJointAngle(int i, float degrees)
    {
        Transform j = jointTransforms != null && i < jointTransforms.Length ? jointTransforms[i] : null;
        if (j == null) return;
        EnsureAxisCache();
        if (i >= jointBaseRot.Length) return;

        Quaternion q0 = jointBaseRot[i];
        Vector3 u = (q0 * jointAxisUnit[i]).normalized;
        j.localRotation = Quaternion.AngleAxis(degrees, u) * q0;
    }

    // ------------------------------------------------------------------
    //  Управление целью
    // ------------------------------------------------------------------

    /// <summary>Сброс цели: робот стоит, пока позиция не подтверждена (логика двух лазеров).</summary>
    public override void ClearTarget()
    {
        base.ClearTarget();
        smoothingInitialized = false;
        preferredSeed = null;
        seedPending = false;
    }

    public override void SetTarget(Vector3 position)
    {        targetPosition = position;
        hasTarget = true;

        if (!smoothingInitialized)
        {
            smoothedTargetPosition = position;
            smoothingInitialized = true;
        }
    }

    public override void SetTarget(Vector3 position, Quaternion rotation)
    {
        // Осевая IK решает ПОЗИЦИЮ; выравнивание ориентации фланца в общем виде
        // недостижимо (для 6-осевого, как и для SCARA, оператор задаёт точку).
        SetTarget(position);
        targetRotation = rotation;
    }

    public override void MoveToTarget(float deltaTime)
    {
        if (!hasTarget)
        {
            return;
        }

        ResolveRobotReferences();
        EnsureAxisCache();

        int n = jointTransforms != null ? jointTransforms.Length : 0;
        if (n == 0 || endEffector == null)
        {
            if (!refsReported)
            {
                Debug.LogError("[" + name + "] SixAxis: не найдены суставы/фланец — проверьте иерархию (Axis1..Axis6).");
                refsReported = true;
            }
            return;
        }

        // Плавное движение цели (экспоненциальное сглаживание каждый кадр).
        float speedScale = Mathf.Max(0.05f, maxSpeed) * Mathf.Max(0.05f, movementSpeedScale);
        float rate = 1f - Mathf.Exp(-deltaTime * Mathf.Max(1f, (1f - positionSmoothing) * 40f) * speedScale);
        smoothedTargetPosition = Vector3.Lerp(smoothedTargetPosition, targetPosition, rate);
        if (Vector3.Distance(smoothedTargetPosition, targetPosition) < 0.0005f)
            smoothedTargetPosition = targetPosition;

        SnapshotPose(rollbackPose, n);
        ApplyPreferredSeed(); // сначала переводим робота в выбранную ветвь IK, затем CCD

        float settingsSpeed = SettingsData.Instance != null ? SettingsData.Instance.robotSpeed : 1f;
        int iterations = Mathf.Max(1, Mathf.RoundToInt(ikIterations * Mathf.Max(0.25f, settingsSpeed)));
        iterations = Mathf.Min(iterations, 40);

        Transform tip = IkTip;
        bool reached = DHInverse.SolveCCD(
            jointTransforms, jointAxesLocal, tip,
            smoothedTargetPosition, iterations, ikTolerance);

        // Визуальная метка цели (legacy IKTarget), если есть.
        if (ik != null && ik.target != null)
            ik.target.position = smoothedTargetPosition;

        if (enableSelfCollisionGuard)
            ResolveSelfCollision(deltaTime, rollbackPose, n);

        _lastReached = reached || (tip != null &&
            Vector3.Distance(tip.position, targetPosition) <= ikTolerance);
    }

    private bool _lastReached;

    public bool ReachedTarget { get { return _lastReached; } }

    private void SnapshotPose(Quaternion[] buffer, int count)
    {
        for (int i = 0; i < count && i < jointTransforms.Length && i < buffer.Length; i++)
        {
            buffer[i] = jointTransforms[i] != null ? jointTransforms[i].localRotation : Quaternion.identity;
        }
    }

    /// <summary>
    /// Проверяет самопересечение звеньев робота (капсульные отрезки между осями).
    /// Если пересечение есть — откатывает суставы в безопасную позу, чтобы корпус
    /// не проходил сквозь себя, либо (селективно) только конфликтующие.
    /// </summary>
    private void ResolveSelfCollision(float deltaTime, Quaternion[] pose, int count)
    {
        if (count < 2) return;

        // Ищем пару конфликтующих (не соседних) звеньев.
        bool collision = TryFindSelfCollision(out int a, out int b);

        if (!collision)
        {
            SelfCollisionBlocked = false;
            selfCollisionBlockedTime = 0f;
            return;
        }

        // Откат — МГНОВЕННЫЙ (полный возврат в позу ДО этого кадра IK), а не покадровый Slerp.
        // Раньше суставы подтягивались к безопасной позе «по чуть-чуть» (Slerp с blend = dt*12),
        // а CCD в следующем кадре снова тянул их к цели: система попадала в предельный цикл и
        // кончик робота дрожал («колбасило»), пока цель не снималась. В логе это выглядело как
        // пачки «Самостолкновение звеньев 0 и 3 — движение приостановлено» подряд.
        int lo = selectiveRollback ? Mathf.Max(0, a - 1) : 0;
        int hi = selectiveRollback ? Mathf.Min(count - 1, b + 1) : count - 1;
        for (int i = lo; i <= hi; i++)
        {
            if (i < 0 || i >= count) continue;
            var j = jointTransforms[i];
            if (j == null) continue;
            j.localRotation = pose[i];
        }

        if (!SelfCollisionBlocked)
        {
            SelfCollisionBlocked = true;
            Debug.LogWarning($"[SixAxis] Самостолкновение звеньев {a} и {b} — движение приостановлено, " +
                             "выполнен откат. Попробуйте другую цель/траекторию.");
        }

        // Конфликт не уходит — снимаем цель совсем: робот СТОИТ (не дрожит у препятствия)
        // и не «догоняет» недостижимую позу бесконечно.
        selfCollisionBlockedTime += Mathf.Max(0f, deltaTime);
        if (selfCollisionBlockedTime >= Mathf.Max(0.05f, selfCollisionStopTime))
        {
            selfCollisionBlockedTime = 0f;
            ClearTarget();
            Debug.LogWarning("[SixAxis] Самостолкновение не уходит за " +
                             Mathf.Max(0.05f, selfCollisionStopTime).ToString("0.0") +
                             " с — цель снята, робот остановлен (движение само не возобновляется).");
        }
    }

    /// <summary>
    /// Ищет первую пару НЕ-соседних физических звеньев, капсулы которых пересекаются.
    /// Совпадающие оси (Axis4 == Axis5 в запястье 6-осевого робота) схлопываются
    /// в один узел — иначе сегменты, сходящиеся в одной точке, всегда «пересекаются».
    /// </summary>
    private bool TryFindSelfCollision(out int indexA, out int indexB)
    {
        indexA = -1;
        indexB = -1;
        if (jointTransforms == null) return false;

        // 1. Строим цепочку уникальных узлов (пропускаем оси, совпадающие с предыдущей).
        var nodes = new System.Collections.Generic.List<Vector3>();
        var nodeJoints = new System.Collections.Generic.List<int>(); // исходный индекс для узла
        const float mergeEpsilonSqr = 1e-8f;
        for (int i = 0; i < jointTransforms.Length; i++)
        {
            if (jointTransforms[i] == null) continue;
            Vector3 p = jointTransforms[i].position;
            if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < mergeEpsilonSqr)
                continue; // та же точка (совпадающие оси запястья) — один узел
            nodes.Add(p);
            nodeJoints.Add(i);
        }

        if (nodes.Count < 3) return false;

        // 2. Звенья = отрезки между соседними уникальными узлами.
        // Проверяем только пары звеньев, которые НЕ делят узел (b >= a+3).
        for (int a = 0; a < nodes.Count - 2; a++)
        {
            for (int b = a + 3; b < nodes.Count; b++)
            {
                if (SegmentsOverlap(nodes[a], nodes[a + 1], nodes[b - 1], nodes[b], linkRadius))
                {
                    indexA = nodeJoints[a];
                    indexB = nodeJoints[b];
                    return true;
                }
            }
        }
        return false;
    }

    private static bool SegmentsOverlap(Vector3 a1, Vector3 a2, Vector3 b1, Vector3 b2, float radius)
    {
        return ClosestDistanceBetweenSegments(a1, a2, b1, b2) < radius * 2f;
    }

    /// <summary>Минимальное расстояние между двумя отрезками (3D).</summary>
    public static float ClosestDistanceBetweenSegments(
        Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
    {
        Vector3 u = p2 - p1;
        Vector3 v = q2 - q1;
        Vector3 w = p1 - q1;

        float a = Vector3.Dot(u, u);
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);

        float denom = a * c - b * b;
        float sN, sD = denom;
        float tN, tD = denom;

        if (denom < 1e-6f)
        {
            sN = 0f;
            sD = 1f;
            tN = e;
            tD = c;
        }
        else
        {
            sN = b * e - c * d;
            tN = a * e - b * d;
            if (sN < 0f) { sN = 0f; tN = e; tD = c; }
            else if (sN > sD) { sN = sD; tN = e + b; tD = c; }
        }

        if (tN < 0f)
        {
            tN = 0f;
            if (-d < 0f) sN = 0f;
            else if (-d > a) sN = sD;
            else { sN = -d; sD = a; }
        }
        else if (tN > tD)
        {
            tN = tD;
            if (-d + b < 0f) sN = 0f;
            else if (-d + b > a) sN = sD;
            else { sN = (-d + b); sD = a; }
        }

        float sc = Mathf.Abs(sN) < 1e-6f ? 0f : sN / sD;
        float tc = Mathf.Abs(tN) < 1e-6f ? 0f : tN / tD;

        Vector3 dP = w + (sc * u) - (tc * v);
        return dP.magnitude;
    }

    /// <summary>Ограничивает углы суставов вдоль ИХ осей (от «нулевой» позы).</summary>
    private void ApplyJointLimits()
    {
        if (jointTransforms == null || jointTransforms.Length == 0 ||
            jointLimits == null || jointLimits.Length == 0)
            return;

        EnsureAxisCache();
        int n = Mathf.Min(jointTransforms.Length, jointLimits.Length);
        for (int i = 0; i < n; i++)
        {
            if (jointTransforms[i] == null) continue;

            float raw = MeasureJointAngle(i);
            float clamped = Mathf.Clamp(raw, jointLimits[i].x, jointLimits[i].y);
            if (Mathf.Abs(clamped - raw) > 0.01f)
                SetJointAngle(i, clamped);
        }
    }

    public override float[] GetJointAngles()
    {
        if (jointTransforms == null)
            return new float[0];

        ResolveRobotReferences();
        EnsureAxisCache();
        float[] angles = new float[jointTransforms.Length];
        for (int i = 0; i < jointTransforms.Length; i++)
            angles[i] = jointTransforms[i] != null ? MeasureJointAngle(i) : 0f;
        return angles;
    }
}
