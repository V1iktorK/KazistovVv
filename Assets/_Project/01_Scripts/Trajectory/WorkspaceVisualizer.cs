using System.Collections.Generic;
using TrajectoryCore;
using UnityEngine;
using UnityEngine.UI;
using KazistovVvUI;

/// <summary>
/// Визуализация ВОЗМОЖНОСТЕЙ робота (ТЗ сессии 14.09.2026, ЗАДАЧИ 3 и 4).
///
/// ЗАДАЧА 3 — ЗОНА ДОСТИЖИМОСТИ (рабочее пространство), «куда робот может дотянуться»:
///   * РОБОТ — полупрозрачный КУПОЛ (сферическая оболочка) вокруг оси 1: радиус = сумма длин
///     звеньев (аналитическая оценка по пивотам суставов, БЕЗ перебора IK-решений), обрезанный
///     плоскостью опоры — оболочка «стоит» на столе, а не уходит под него. Граница — яркая
///     сетка (параллели + меридианы) и кольцо линии отсечения.
///     Дополнительно — ГРАНИЦА РАБОЧЕЙ ЗОНЫ оракула: радиус, за которым точка прицела уже
///     считается недостижимой (по ней видно, где шарик позеленеет).
///   * SCARA — КОЛЬЦО (annulus) вокруг базы: внутренний радиус = |L1 − L2| («мёртвая зона»),
///     внешний = L1 + L2 (вылет руки), плюс пояс по ходу призмы z_5 (низ/верх хода).
///     Границы — яркие окружности, заливка — полупрозрачная.
///   Точность НАМЕРЕННО приблизительная: это визуализация, а не расчёт зоны (ТЗ).
///   Зона пересобирается сама при смене конфигурации робота (другие длины звеньев, лимиты,
///   положение базы, тип робота) и по публичному вызову RebuildWorkspace().
///
/// ЗАДАЧА 4 — ЛИМИТЫ СУСТАВОВ: у каждого сустава робота и SCARA — полупрозрачное КОЛЬЦО
///   вокруг оси сустава, цвет которого показывает близость к пределу:
///     ЗЕЛЁНЫЙ — запас большой, ЖЁЛТЫЙ — близко к пределу, КРАСНЫЙ — предел достигнут.
///   Дополнительно — панель (screen space, слева): текущий угол, диапазон лимита и запас
///   по каждому суставу (та же цветовая индикация). Обновление онлайн (≈10 раз в секунду).
///
/// Производительность: геометрия строится ОДИН раз (пересобирается только при смене
/// конфигурации), в кадре — лишь смена цвета материалов (≈12 вызовов на обновление) и текст
/// панели. Коллайдеров нет, в CollisionWorld объекты не попадают; все служебные объекты
/// скрыты из Hierarchy (правило проекта, PROJECT_CONTEXT §4).
///
/// Компонент создаётся потоком этапов (`TrajectoryFlowController.Awake`) — сцена не меняется.
/// Включается/выключается публичными флагами и методами (БИНДЫ НЕ ИСПОЛЬЗУЮТСЯ):
///   showWorkspace      / SetWorkspaceVisible(bool) / ToggleWorkspace()
///   showJointLimits    / SetJointLimitsVisible(bool) / ToggleJointLimits()
/// </summary>
public class WorkspaceVisualizer : MonoBehaviour
{
    [Header("Зона достижимости (ЗАДАЧА 3)")]
    [Tooltip("Показывать зону достижимости. Публичный флаг: правится в инспекторе в PlayMode " +
             "или методом SetWorkspaceVisible(bool)/ToggleWorkspace(). Биндов нет.")]
    public bool showWorkspace = true;
    [Tooltip("Показывать зону для ОБОИХ роботов сцены (робот и SCARA) — иначе только для активного.")]
    public bool workspaceAllRobots = true;
    [Tooltip("Голубой цвет зоны. Специально НЕ бирюзовый: бирюзовые — фантомы, " +
             "оранжевые — траектории (ТЗ: цвета не должны путаться).")]
    public Color workspaceColor = new Color(0.30f, 0.55f, 0.98f, 1f);
    [Tooltip("Прозрачность заливки оболочки/кольца (0.06–0.14: «читается, но не мешает обзору»).")]
    [Range(0f, 0.5f)] public float workspaceAlpha = 0.09f;
    [Tooltip("Прозрачность ярких линий границы и сетки.")]
    [Range(0f, 1f)] public float workspaceEdgeAlpha = 0.55f;
    [Tooltip("Толщина линий сетки зоны (мировые единицы).")]
    public float workspaceLineWidth = 0.008f;
    [Tooltip("Радиус рабочей зоны оракула (юниты) — граница, за которой точка прицела уже " +
             "считается недостижимой. Поток синхронизирует значение с самим оракулом.")]
    public float oracleZoneRadius = 1.6f;
    [Tooltip("Показывать границу рабочей зоны оракула.")]
    public bool showOracleZone = true;
    [Tooltip("Показывать заливку оболочки. Выключено — остаётся только сетка (совсем не мешает обзору).")]
    public bool workspaceSurface = true;
    [Tooltip("Сегментов по кругу у зоны (больше — глаже; 48–72 достаточно).")]
    [Range(16, 128)] public int workspaceSegments = 64;
    [Tooltip("Сколько параллелей рисовать в сетке купола робота.")]
    [Range(1, 8)] public int workspaceGridSteps = 4;

    [Header("Лимиты суставов (ЗАДАЧА 4)")]
    [Tooltip("Показывать индикаторы лимитов (кольца у суставов + панель). Публичный флаг: " +
             "правится в инспекторе в PlayMode или методом SetJointLimitsVisible(bool)/ToggleJointLimits().")]
    public bool showJointLimits = true;
    [Tooltip("Показывать панель с углами/лимитами/запасом по каждому суставу (текст).")]
    public bool showJointPanel = true;
    [Tooltip("Внешний радиус кольца-индикатора, юниты (внутренний = 0.55 от внешнего).")]
    public float jointRingRadius = 0.11f;
    [Tooltip("Прозрачность заливки кольца сустава (кольцо не перекрывает робот).")]
    [Range(0f, 1f)] public float jointRingAlpha = 0.30f;
    [Tooltip("Как часто (с) обновлять индикаторы и панель. 0.1 = 10 раз в секунду: плавно и дёшево.")]
    public float refreshInterval = 0.1f;
    [Tooltip("Запас (в «градусах»; у призмы — доля хода × 360°), ниже которого сустав считается " +
             "ЖЁЛТЫМ «близко к пределу».")]
    public float warningMarginDeg = 15f;
    [Tooltip("Запас, ниже которого сустав считается КРАСНЫМ «предел достигнут».")]
    public float criticalMarginDeg = 3f;
    [Tooltip("Доля хода сустава: ниже неё индикатор тоже жёлтый/красный (для длинных лимитов " +
             "абсолютных градусов мало).")]
    [Range(0f, 0.5f)] public float warningMarginFraction = 0.08f;
    [Range(0f, 0.5f)] public float criticalMarginFraction = 0.015f;

    [Header("Панель лимитов (screen space)")]
    public float panelWidth = 352f;
    public float panelLeftOffset = 20f;
    public float panelTopOffset = 180f;
    public int panelSortingOrder = 55;      // ниже HUD режима перемещения точки (60)

    private static readonly Color ColorOk = new Color(0.35f, 0.95f, 0.25f);       // в норме
    private static readonly Color ColorWarn = new Color(1f, 0.78f, 0.10f);        // близко к пределу
    private static readonly Color ColorBad = new Color(1f, 0.16f, 0.16f);         // предел достигнут

    /// <summary>Одна зона достижимости (робот или SCARA) со своей геометрией.</summary>
    private class Zone
    {
        public RobotController robot;
        public readonly PoseValidator validator = new PoseValidator();
        public GameObject root;
        public Material surfaceMat;
        public readonly List<Material> lineMats = new List<Material>();
        public string signature = "";
        public bool scara;
        public Vector3 center;            // центр купола (ось 1) / база SCARA
        public Vector3 up = Vector3.up;
        public float radius = 1f;         // внешний радиус (робот — вылет, SCARA — L1+L2)
        public float innerRadius;         // внутренний радиус (робот — «мёртвая зона», SCARA — |L1−L2|)
        public float planeY;              // плоскость опоры (верх стола внизу робота)
        public float zMin, zMax;          // ход призмы SCARA — ВЫСОТЫ над базой (из лимитов валидатора)
    }

    /// <summary>Индикатор одного сустава: кольцо в мире + строка панели.</summary>
    private class JointRing
    {
        public GameObject go;
        public Renderer renderer;
        public Material mat;
        public LineRenderer edge;
        public Material edgeMat;
    }

    private readonly List<Zone> zones = new List<Zone>();
    private readonly List<JointRing> rings = new List<JointRing>();
    private readonly List<Image> panelSwatches = new List<Image>();
    private readonly List<Text> panelTexts = new List<Text>();

    private PoseValidator activeValidator;          // валидатор активного робота (для лимитов)
    private RobotController activeRobot;
    private double[] jointQ;
    private Vector3[] jointPivots, jointAxes;
    private float[] jointMargins;

    private GameObject panelRoot;
    private RectTransform panelBody;
    private Text panelTitle;
    private Text panelHint;
    private bool built;

    private float refreshTimer;
    private float scanTimer;
    private bool warnedNoRobot;
    /// <summary>Строка «зон достижимости: N …» уже напечатана для текущего состава зон (ФИКС 5).</summary>
    private bool loggedZones;

    /// <summary>Зона достижимости показывается (публичный флаг).</summary>
    public bool WorkspaceVisible => showWorkspace;
    /// <summary>Индикаторы лимитов показываются (публичный флаг).</summary>
    public bool JointLimitsVisible => showJointLimits;
    /// <summary>Сколько зон построено (робот + SCARA) — для диагностики/проверок.</summary>
    public int ZoneCount => zones.Count;
    /// <summary>Сколько колец-индикаторов суставов построено — для диагностики.</summary>
    public int JointRingCount => rings.Count;
    /// <summary>Внешний радиус k-й зоны (юниты) — для диагностики.</summary>
    public float ZoneRadius(int index) => index >= 0 && index < zones.Count ? zones[index].radius : 0f;
    /// <summary>Внутренний радиус k-й зоны (мёртвая зона / кольцо SCARA).</summary>
    public float ZoneInnerRadius(int index) => index >= 0 && index < zones.Count ? zones[index].innerRadius : 0f;
    /// <summary>Имя робота k-й зоны.</summary>
    public string ZoneRobotName(int index)
        => index >= 0 && index < zones.Count && zones[index].robot != null ? zones[index].robot.robotName : "";
    /// <summary>Текст строки панели лимитов для сустава i (диагностика/проверки).</summary>
    public string JointPanelLine(int i)
        => i >= 0 && i < panelTexts.Count && panelTexts[i] != null ? panelTexts[i].text : "";
    /// <summary>Цвет индикатора сустава i — вердикт «норма / близко / предел» (диагностика).</summary>
    public Color JointPanelColor(int i)
        => i >= 0 && i < panelSwatches.Count && panelSwatches[i] != null ? panelSwatches[i].color : Color.white;

    // ------------------------------------------------------------------ публичное управление

    /// <summary>Включить/выключить зону достижимости (публичный метод — бинды не используются).</summary>
    public void SetWorkspaceVisible(bool on)
    {
        showWorkspace = on;
        if (on && zones.Count == 0) BuildZones();
        ApplyVisibility();
    }

    /// <summary>Переключить зону достижимости.</summary>
    public void ToggleWorkspace() { SetWorkspaceVisible(!showWorkspace); }

    /// <summary>Включить/выключить индикаторы лимитов суставов (кольца + панель).</summary>
    public void SetJointLimitsVisible(bool on)
    {
        showJointLimits = on;
        if (on && rings.Count == 0) BuildRings();
        ApplyVisibility();
        if (on) { refreshTimer = 0f; RefreshJointLimits(); }
    }

    /// <summary>Переключить индикаторы лимитов суставов.</summary>
    public void ToggleJointLimits() { SetJointLimitsVisible(!showJointLimits); }

    /// <summary>
    /// Пересобрать зону достижимости вручную — например, после смены конфигурации робота
    /// (переехал, заменён, изменились лимиты/звенья). Автоматически вызывается и сама,
    /// когда смена конфигурации замечена (см. ConfigurationChanged).
    /// </summary>
    public void RebuildWorkspace()
    {
        DestroyZones();
        BuildZones();
        ApplyVisibility();
    }

    /// <summary>Пересобрать индикаторы лимитов (после смены активного робота).</summary>
    public void RebuildJointLimits()
    {
        DestroyRings();
        BuildRings();
        ApplyVisibility();
        RefreshJointLimits();
    }

    // ------------------------------------------------------------------ привязка

    /// <summary>Активный робот потока: по нему строятся индикаторы лимитов (зоны — по обоим).</summary>
    public void Bind(RobotController robot)
    {
        if (robot == activeRobot) return;
        activeRobot = robot;
        activeValidator = null;
        if (activeRobot != null)
        {
            activeValidator = new PoseValidator();
            activeValidator.Init(activeRobot);
            activeValidator.linkRadius = 0.06f;
            int n = Mathf.Max(1, activeValidator.Dof);
            jointQ = new double[n];
            jointPivots = new Vector3[n];
            jointAxes = new Vector3[n];
            jointMargins = new float[n];
        }
        RebuildJointLimits();
    }

    private void Awake()
    {
        Build();
        if (showWorkspace) BuildZones();
        if (showJointLimits) BuildRings();
        ApplyVisibility();
    }

    private void OnDestroy()
    {
        DestroyZones();
        DestroyRings();
        if (panelRoot != null) Destroy(panelRoot);
    }

    /// <summary>
    /// Кадровое обслуживание (зовёт поток этапов): раз в refreshInterval обновляет индикаторы
    /// лимитов и панель, раз в 0.5 с проверяет смену конфигурации робота (тогда зона пересобирается).
    /// Работа стоит доли миллисекунды — только цвета материалов и текст панели.
    /// </summary>
    public void Tick()
    {
        if (!showWorkspace && !showJointLimits) return;

        scanTimer -= Time.unscaledDeltaTime;
        if (scanTimer <= 0f)
        {
            scanTimer = 0.5f;
            if (showWorkspace && ConfigurationChanged()) RebuildWorkspace();
        }

        if (!showJointLimits) return;
        refreshTimer -= Time.unscaledDeltaTime;
        if (refreshTimer > 0f) return;
        refreshTimer = Mathf.Max(0.02f, refreshInterval);
        RefreshJointLimits();
    }

    // ------------------------------------------------------------------ зоны достижимости

    /// <summary>Роботы сцены (без копий-фантомов) — у каждого своя зона.</summary>
    private void BuildZones()
    {
        var list = new List<RobotController>();
        if (workspaceAllRobots)
        {
            foreach (RobotController rc in Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude))
            {
                if (rc == null) continue;
                if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;   // фантомы
                list.Add(rc);
            }
        }
        else if (activeRobot != null)
        {
            list.Add(activeRobot);
        }

        foreach (RobotController rc in list)
        {
            var z = new Zone { robot = rc };
            z.validator.Init(rc);
            z.validator.linkRadius = 0.06f;
            if (!z.validator.Ready) continue;
            z.scara = z.validator.Dof == 3;
            if (!MeasureZone(z)) continue;
            BuildZoneGeometry(z);
            z.signature = SignatureOf(z);
            zones.Add(z);
        }

        if (zones.Count == 0 && !warnedNoRobot)
        {
            warnedNoRobot = true;
            Debug.Log("[Workspace] зона достижимости не построена: робот не найден или валидатор не готов.");
        }
        else if (zones.Count > 0 && !loggedZones)
        {
            // ФИКС 5 (§23): строка печатается ОДИН раз на состав зон, а не при каждой
            // пересборке. Раньше (см. ниже) зона пересобиралась каждые 0,5 с, и в консоль
            // с полным стеком вызова уходили тысячи одинаковых строк — это и «краснило»
            // консоль, и давало регулярные просадки кадра (камера «колбасила»).
            loggedZones = true;
            Debug.Log("[Workspace] зон достижимости: " + zones.Count + " · " + DescribeZones());
        }
    }

    private string DescribeZones()
    {
        var sb = new System.Text.StringBuilder();
        foreach (Zone z in zones)
        {
            if (sb.Length > 0) sb.Append(" | ");
            sb.Append(z.robot != null ? z.robot.robotName : "?").Append(z.scara ? " (кольцо)" : " (купол)")
              .Append(": внутр. ").Append(z.innerRadius.ToString("F2"))
              .Append(" … внеш. ").Append(z.radius.ToString("F2")).Append(" ю");
        }
        return sb.ToString();
    }

    /// <summary>
    /// Аналитическая оценка зоны (БЕЗ перебора IK): длины звеньев — расстояния между пивотами
    /// суставов (от позы не зависят), их сумма даёт вылет руки. SCARA — планарные L1/L2 и ход призмы.
    /// </summary>
    private bool MeasureZone(Zone z)
    {
        PoseValidator v = z.validator;
        double[] q = v.CopyCurrent();
        int n = Mathf.Max(1, v.Dof);
        var pivots = new Vector3[n];
        var axes = new Vector3[n];
        v.JointFrames(q, pivots, axes);

        if (!z.scara)
        {
            if (v.Dof < 2) return false;
            SixAxisController six = z.robot as SixAxisController;
            Transform tcpT = six != null ? (six.tcp != null ? six.tcp : six.endEffector) : null;

            float sum = 0f, first = 0f;
            for (int i = 0; i + 1 < v.Dof; i++)
            {
                float d = Vector3.Distance(pivots[i], pivots[i + 1]);
                if (i == 0) first = d;
                sum += d;
            }
            if (tcpT != null) sum += Vector3.Distance(pivots[v.Dof - 1], tcpT.position);
            if (sum < 0.05f) return false;

            // «Мёртвая зона» — приближение двумя группами звеньев: |L1 − (остальные)|.
            z.innerRadius = Mathf.Max(0f, first - (sum - first)) * 0.98f;
            z.center = pivots[0];                                  // ось 1 — база вращения
            z.up = six != null && six.baseTransform != null ? six.baseTransform.up : Vector3.up;
            z.radius = sum;
            z.planeY = z.robot.transform.position.y;               // плоскость опоры (стол) — низ робота
            return true;
        }

        // --- SCARA: кольцо (annulus) вокруг базы ---
        SCARAController sc = z.robot as SCARAController;
        if (sc == null || sc.joint1 == null || sc.joint2 == null || sc.joint3 == null) return false;
        float l1 = Vector3.Distance(pivots[1], pivots[0]);
        float l2 = Vector3.Distance(pivots[2], pivots[1]);
        if (l1 + l2 < 0.05f) return false;

        z.center = pivots[0];
        z.up = sc.baseTransform != null ? sc.baseTransform.up : Vector3.up;
        z.radius = l1 + l2;
        z.innerRadius = Mathf.Abs(l1 - l2);
        z.planeY = z.robot.transform.position.y;
        // Ход призмы: лимиты валидатора — АБСОЛЮТНЫЕ высоты z_5 над базой (PoseValidator.Init).
        z.zMin = v.Lower[2];
        z.zMax = v.Upper[2];
        return true;
    }

    /// <summary>
    /// СИГНАТУРА КОНФИГУРАЦИИ — печатается на момент сборки зоны и сравнивается в
    /// <see cref="ConfigurationChanged"/>.
    ///
    /// ФИКС 5 (§23) — НАЙДЕННАЯ ПРИЧИНА «КАМЕРА КОЛБАСИТ»: сборка зоны сохраняла ОДНУ строку,
    /// а проверка изменений строила ДРУГУЮ — с другим набором полей, другим их порядком и
    /// другим форматом чисел. Две строки не могли совпасть НИКОГДА, поэтому
    /// `ConfigurationChanged()` возвращала «true» на каждом вызове, и зона достижимости
    /// (два робота: меши, материалы, объекты сцены) пересобиралась КАЖДЫЕ 0,5 С, каждый раз
    /// печатая строку в консоль с полным стеком вызова. Это и была та самая регулярная
    /// просадка кадра, из-за которой камера «периодически колбасила».
    ///
    /// Теперь сигнатуру считает ОДНА функция <see cref="SignatureOf"/> и для сборки, и для
    /// сравнения, поэтому в неё входит ровно то, что описано в её имени: имя робота, число
    /// осей, длины звеньев, лимиты суставов, положение базы и (для SCARA) ход призмы.
    /// Намеренно НЕ входит вылет до TCP: он зависит от ТЕКУЩЕЙ позы, то есть «конфигурацией»
    /// не является — иначе зона пересобиралась бы при каждом движении робота.
    /// </summary>
    private static string SignatureOf(Zone z)
    {
        if (z.robot == null) return "none";
        PoseValidator v = z.validator;
        // Длины звеньев считаются по пивотам суставов от их взаимного расположения —
        // от позы это не зависит.
        double[] q = v.CopyCurrent();
        int n = Mathf.Max(1, v.Dof);
        var pivots = new Vector3[n];
        var axes = new Vector3[n];
        float sum = 0f;
        try
        {
            v.JointFrames(q, pivots, axes);
            for (int i = 0; i + 1 < v.Dof; i++) sum += Vector3.Distance(pivots[i], pivots[i + 1]);
        }
        catch (System.Exception) { }

        return SignatureBuild(z.robot.robotName, v.Dof, sum, z.robot.transform.position,
            v.Lower, v.Upper, z.scara);
    }

    /// <summary>Сборка строки сигнатуры — ЕДИНСТВЕННОЕ место, где задан её формат.</summary>
    private static string SignatureBuild(string robotName, int dof, float linkSum,
        Vector3 basePos, float[] lower, float[] upper, bool scara)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(robotName).Append("|dof").Append(dof)
          .Append("|L").Append(linkSum.ToString("F3"))
          .Append("|b").Append(basePos.x.ToString("F2")).Append(',')
          .Append(basePos.y.ToString("F2")).Append(',').Append(basePos.z.ToString("F2"));
        for (int i = 0; i < dof; i++)
        {
            float lo = lower != null && i < lower.Length ? lower[i] : 0f;
            float hi = upper != null && i < upper.Length ? upper[i] : 0f;
            sb.Append("|q").Append(i).Append(' ').Append((hi - lo).ToString("F1"));
        }
        if (scara)
        {
            float zlo = lower != null && lower.Length > 2 ? lower[2] : 0f;
            float zhi = upper != null && upper.Length > 2 ? upper[2] : 0f;
            sb.Append("|z").Append(zlo.ToString("F2")).Append('/').Append(zhi.ToString("F2"));
        }
        return sb.ToString();
    }

    /// <summary>Сменилась ли КОНФИГУРАЦИЯ (звенья/лимиты/база/тип) — тогда зона пересобирается.</summary>
    private bool ConfigurationChanged()
    {
        if (zones.Count == 0) return true;
        foreach (Zone z in zones)
        {
            if (z.robot == null) return true;
            if (!z.validator.Ready) return true;
            // ФИКС 5 (§23): сравнивается ТА ЖЕ строка, что сохранена при сборке (SignatureOf),
            // а не «похожая, но другая» — иначе зона пересобиралась каждые 0,5 с.
            if (SignatureOf(z) != z.signature) return true;
        }
        return false;
    }

    private void BuildZoneGeometry(Zone z)
    {
        z.root = new GameObject("WorkspaceZone_" + (z.robot != null ? z.robot.robotName : "?"));
        z.root.hideFlags = HideFlags.HideInHierarchy;
        z.root.transform.SetParent(transform, false);

        if (z.scara) BuildAnnulus(z);
        else BuildDome(z);
    }

    /// <summary>
    /// КУПОЛ РОБОТА: сферическая оболочка радиуса вылета вокруг оси 1, обрезанная плоскостью
    /// опоры (столом). Сетка: параллели + меридианы + яркое кольцо отсечения.
    /// </summary>
    private void BuildDome(Zone z)
    {
        int seg = Mathf.Clamp(workspaceSegments, 16, 128);
        Vector3 c = z.center;
        float r = z.radius;
        float cutCos = Mathf.Clamp((z.planeY - c.y) / Mathf.Max(0.01f, r), -0.999f, 0.999f);
        float cutAngle = Mathf.Acos(cutCos) * Mathf.Rad2Deg;    // полярный угол от «вверх» до плоскости
        Quaternion rot = Quaternion.FromToRotation(Vector3.up, z.up.sqrMagnitude > 1e-6f ? z.up.normalized : Vector3.up);

        if (workspaceSurface)
        {
            z.surfaceMat = CreateZoneMaterial(workspaceColor, workspaceAlpha, false);
            GameObject dome = AddMeshPart(z, "Dome", BuildDomeMesh(r, cutAngle, seg, 14), z.surfaceMat);
            if (dome != null) { dome.transform.position = c; dome.transform.rotation = rot; }
        }

        // Параллели (по полярному углу) — «сетка» купола.
        int steps = Mathf.Clamp(workspaceGridSteps, 1, 8);
        for (int i = 1; i <= steps; i++)
        {
            float ang = cutAngle * i / (steps + 1);
            AddCircleLine(z, "Parallel" + i, c, z.up, ang, r,
                Color.Lerp(workspaceColor, Color.white, 0.15f), workspaceEdgeAlpha);
        }
        // Меридианы.
        int meridians = Mathf.Max(4, seg / 8);
        for (int m = 0; m < meridians; m++)
            AddArcLine(z, "Meridian" + m, c, z.up, 360f * m / meridians, cutAngle, r,
                Color.Lerp(workspaceColor, Color.white, 0.05f), workspaceEdgeAlpha * 0.8f);

        // Кольцо линии отсечения — самая заметная граница («зона стоит на столе»).
        AddCircleLine(z, "CutBoundary", c, z.up, cutAngle, r, workspaceColor,
            Mathf.Min(1f, workspaceEdgeAlpha * 1.4f));

        // Граница рабочей зоны оракула — радиус, за которым точка уже недостижима.
        if (showOracleZone && oracleZoneRadius > 0.05f)
            AddCircleLine(z, "OracleZone", new Vector3(c.x, z.planeY + 0.006f, c.z), z.up, 90f,
                oracleZoneRadius, new Color(0.62f, 0.86f, 1f), 0.75f);
    }

    /// <summary>КОЛЬЦО (annulus) SCARA вокруг базы + пояс по ходу призмы z_5.</summary>
    private void BuildAnnulus(Zone z)
    {
        int seg = Mathf.Clamp(workspaceSegments, 16, 128);
        Vector3 flat = new Vector3(z.center.x, z.planeY + 0.005f, z.center.z);   // «чертёж» на столе
        Vector3 band = new Vector3(z.center.x, z.center.y, z.center.z);          // высоты хода Z

        if (workspaceSurface)
        {
            z.surfaceMat = CreateZoneMaterial(workspaceColor, workspaceAlpha, false);
            GameObject ring = AddMeshPart(z, "Annulus", BuildAnnulusMesh(z.innerRadius, z.radius, seg), z.surfaceMat);
            if (ring != null) ring.transform.position = flat;
            if (z.zMax - z.zMin > 0.01f)
            {
                GameObject outer = AddMeshPart(z, "WallOuter",
                    BuildCylinderMesh(z.radius, z.zMin, z.zMax, seg, false), z.surfaceMat);
                if (outer != null) outer.transform.position = band;
                GameObject inner = AddMeshPart(z, "WallInner",
                    BuildCylinderMesh(z.innerRadius, z.zMin, z.zMax, seg, true), z.surfaceMat);
                if (inner != null) inner.transform.position = band;
            }
        }

        AddCircleLine(z, "OuterEdge", flat, z.up, 90f, z.radius, workspaceColor,
            Mathf.Min(1f, workspaceEdgeAlpha * 1.4f));
        AddCircleLine(z, "InnerEdge", flat, z.up, 90f, z.innerRadius,
            Color.Lerp(workspaceColor, Color.white, 0.2f), workspaceEdgeAlpha);
        // Верх/низ хода призмы — граница вертикального хода.
        AddCircleLine(z, "ZTop", new Vector3(band.x, band.y + z.zMax, band.z), z.up, 90f, z.radius,
            Color.Lerp(workspaceColor, Color.white, 0.25f), workspaceEdgeAlpha * 0.7f);
        AddCircleLine(z, "ZBottom", new Vector3(band.x, band.y + z.zMin, band.z), z.up, 90f, z.radius,
            Color.Lerp(workspaceColor, Color.white, 0.25f), workspaceEdgeAlpha * 0.7f);
    }

    private GameObject AddMeshPart(Zone z, string name, Mesh mesh, Material mat)
    {
        if (mesh == null) return null;
        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.transform.SetParent(z.root.transform, false);
        var mf = go.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var mr = go.AddComponent<MeshRenderer>();
        mr.sharedMaterial = mat;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
        mr.allowOcclusionWhenDynamic = false;
        return go;
    }

    /// <summary>Яркая окружность (параллель): центр сферы + полярный угол + радиус.</summary>
    private void AddCircleLine(Zone z, string name, Vector3 center, Vector3 up, float polarDeg,
                               float radius, Color color, float alpha)
    {
        float ringRadius = Mathf.Abs(radius * Mathf.Sin(polarDeg * Mathf.Deg2Rad));
        if (ringRadius < 0.02f) return;                       // вырожденное кольцо у полюса — не рисуем

        Vector3 axis = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        Vector3 dir = Vector3.ProjectOnPlane(Vector3.forward, axis);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.ProjectOnPlane(Vector3.right, axis);
        dir.Normalize();
        Vector3 side = Vector3.Cross(axis, dir).normalized;
        Vector3 ringCenter = center + axis * (radius * Mathf.Cos(polarDeg * Mathf.Deg2Rad));

        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.transform.SetParent(z.root.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.loop = true;
        lr.numCapVertices = 0;
        lr.widthMultiplier = Mathf.Max(0.002f, workspaceLineWidth);
        Material zoneLineMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
            "Материал_зоны", Color.white);
        if (zoneLineMaterial != null) lr.material = zoneLineMaterial;
        if (lr.material != null) z.lineMats.Add(lr.material);
        Color c = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        lr.startColor = c;
        lr.endColor = c;
        int seg = 64;
        lr.positionCount = seg;
        for (int i = 0; i < seg; i++)
        {
            float a = Mathf.PI * 2f * i / seg;
            lr.SetPosition(i, ringCenter + (dir * Mathf.Cos(a) + side * Mathf.Sin(a)) * ringRadius);
        }
    }

    /// <summary>Меридиан (дуга) купола от полюса до плоскости отсечения.</summary>
    private void AddArcLine(Zone z, string name, Vector3 center, Vector3 up, float azimuthDeg,
                            float maxPolarDeg, float radius, Color color, float alpha)
    {
        Vector3 axis = up.sqrMagnitude > 1e-6f ? up.normalized : Vector3.up;
        Vector3 dir = Vector3.ProjectOnPlane(Vector3.forward, axis);
        if (dir.sqrMagnitude < 1e-6f) dir = Vector3.ProjectOnPlane(Vector3.right, axis);
        dir = (Quaternion.AngleAxis(azimuthDeg, axis) * dir.normalized).normalized;

        var go = new GameObject(name);
        go.hideFlags = HideFlags.HideInHierarchy;
        go.transform.SetParent(z.root.transform, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = true;
        lr.numCapVertices = 0;
        lr.widthMultiplier = Mathf.Max(0.002f, workspaceLineWidth) * 0.8f;
        Material zoneLineMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
            "Материал_зоны", Color.white);
        if (zoneLineMaterial != null) lr.material = zoneLineMaterial;
        if (lr.material != null) z.lineMats.Add(lr.material);
        Color c = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        lr.startColor = c;
        lr.endColor = new Color(c.r, c.g, c.b, c.a * 0.35f);
        int steps = 20;
        lr.positionCount = steps + 1;
        for (int i = 0; i <= steps; i++)
        {
            float pol = Mathf.Clamp(maxPolarDeg, 1f, 179f) * Mathf.Deg2Rad * i / steps;
            lr.SetPosition(i, center + (axis * Mathf.Cos(pol) + dir * Mathf.Sin(pol)) * radius);
        }
    }

    /// <summary>Сферическая оболочка (купол) радиусом radius: от полюса до polarLimitDeg.</summary>
    private static Mesh BuildDomeMesh(float radius, float polarLimitDeg, int seg, int lat)
    {
        lat = Mathf.Clamp(lat, 3, 32);
        float limit = Mathf.Clamp(polarLimitDeg, 1f, 179f) * Mathf.Deg2Rad;
        var verts = new List<Vector3>((lat + 1) * (seg + 1));
        var tris = new List<int>(lat * seg * 6);
        for (int i = 0; i <= lat; i++)
        {
            float pol = limit * i / lat;
            float y = Mathf.Cos(pol) * radius;
            float rr = Mathf.Sin(pol) * radius;
            for (int s = 0; s <= seg; s++)
            {
                float a = Mathf.PI * 2f * s / seg;
                verts.Add(new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr));
            }
        }
        for (int i = 0; i < lat; i++)
        {
            for (int s = 0; s < seg; s++)
            {
                int a = i * (seg + 1) + s, b = a + 1, c = a + seg + 1, d = c + 1;
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        return FinishMesh("WorkspaceDome", verts, tris);
    }

    /// <summary>Плоское кольцо (annulus) в плоскости XZ: от inner до outer.</summary>
    private static Mesh BuildAnnulusMesh(float inner, float outer, int seg)
    {
        inner = Mathf.Max(0.001f, inner);
        outer = Mathf.Max(inner + 0.01f, outer);
        var verts = new List<Vector3>((seg + 1) * 2);
        var tris = new List<int>(seg * 6);
        for (int s = 0; s <= seg; s++)
        {
            float a = Mathf.PI * 2f * s / seg;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            verts.Add(new Vector3(cs * inner, 0f, sn * inner));
            verts.Add(new Vector3(cs * outer, 0f, sn * outer));
        }
        for (int s = 0; s < seg; s++)
        {
            int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
            tris.Add(a); tris.Add(c); tris.Add(b);
            tris.Add(b); tris.Add(c); tris.Add(d);
        }
        return FinishMesh("WorkspaceAnnulus", verts, tris);
    }

    /// <summary>Стенка цилиндра (пояс хода Z): наружная или внутренняя.</summary>
    private static Mesh BuildCylinderMesh(float radius, float y0, float y1, int seg, bool inward)
    {
        radius = Mathf.Max(0.01f, radius);
        float lo = Mathf.Min(y0, y1), hi = Mathf.Max(y0, y1);
        var verts = new List<Vector3>((seg + 1) * 2);
        var tris = new List<int>(seg * 6);
        for (int s = 0; s <= seg; s++)
        {
            float a = Mathf.PI * 2f * s / seg;
            float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            verts.Add(new Vector3(cs * radius, lo, sn * radius));
            verts.Add(new Vector3(cs * radius, hi, sn * radius));
        }
        for (int s = 0; s < seg; s++)
        {
            int a = s * 2, b = a + 1, c = a + 2, d = a + 3;
            if (!inward)
            {
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }
            else
            {
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }
        return FinishMesh("WorkspaceWall", verts, tris);
    }

    private static Mesh FinishMesh(string name, List<Vector3> verts, List<int> tris)
    {
        var mesh = new Mesh { name = name };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Полупрозрачный материал зоны: HDRP-прозрачность включается так же, как у фантомов
    /// (GhostMaterial), но ZTest ОБЫЧНЫЙ (LEqual) — зона не рисуется поверх робота
    /// и траекторий: она фоновый ориентир, а не «рентген».
    /// </summary>
    private static Material CreateZoneMaterial(Color color, float alpha, bool throughGeometry)
    {
        // Материал зоны — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
        Color c = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        Material m = KazistovVvFeatures.KvRenderPipelineShaders.CreateUnlit("Материал_зоны", c);
        if (m == null) return null;
        GhostMaterial.MakeGhost(m, color, alpha);
        if (m.HasProperty("_ZTestTransparent")) m.SetFloat("_ZTestTransparent", throughGeometry ? 8f : 4f);
        if (m.HasProperty("_ZTestMode")) m.SetFloat("_ZTestMode", throughGeometry ? 8f : 4f);
        if (m.HasProperty("_EmissiveColor"))
            m.SetColor("_EmissiveColor", new Color(color.r, color.g, color.b) * 0.15f);
        if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
        m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        return m;
    }

    private void DestroyZones()
    {
        foreach (Zone z in zones)
        {
            for (int i = 0; i < z.lineMats.Count; i++)
                if (z.lineMats[i] != null) Destroy(z.lineMats[i]);
            if (z.surfaceMat != null) Destroy(z.surfaceMat);
            if (z.root != null) Destroy(z.root);
        }
        zones.Clear();
        warnedNoRobot = false;
        loggedZones = false;
    }

    // ------------------------------------------------------------------ индикаторы лимитов

    /// <summary>Кольца-индикаторы у каждого сустава активного робота + строки панели.</summary>
    private void BuildRings()
    {
        Build();
        if (activeValidator == null || !activeValidator.Ready) return;

        int n = activeValidator.Dof;
        for (int i = 0; i < n; i++)
        {
            var ring = new JointRing();
            ring.go = new GameObject("JointLimit_" + (i + 1));
            ring.go.hideFlags = HideFlags.HideInHierarchy;
            ring.go.transform.SetParent(transform, false);

            var mf = ring.go.AddComponent<MeshFilter>();
            mf.sharedMesh = BuildAnnulusMesh(jointRingRadius * 0.55f, jointRingRadius, 40);
            ring.renderer = ring.go.AddComponent<MeshRenderer>();
            ring.mat = CreateZoneMaterial(ColorOk, jointRingAlpha, false);
            ring.renderer.sharedMaterial = ring.mat;
            ring.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.renderer.receiveShadows = false;
            ring.renderer.allowOcclusionWhenDynamic = false;

            // Яркая кромка кольца — статус читается и на расстоянии.
            var edgeGo = new GameObject("Edge");
            edgeGo.hideFlags = HideFlags.HideInHierarchy;
            edgeGo.transform.SetParent(ring.go.transform, false);
            ring.edge = edgeGo.AddComponent<LineRenderer>();
            ring.edge.useWorldSpace = false;
            ring.edge.loop = true;
            ring.edge.numCapVertices = 0;
            ring.edge.widthMultiplier = Mathf.Max(0.004f, jointRingRadius * 0.06f);
            Material edgeMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                "Материал_кольца", Color.white);
            if (edgeMaterial != null) ring.edge.material = edgeMaterial;
            ring.edgeMat = ring.edge.material;
            int segs = 48;
            ring.edge.positionCount = segs;
            for (int s = 0; s < segs; s++)
            {
                float a = Mathf.PI * 2f * s / segs;
                ring.edge.SetPosition(s, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * jointRingRadius);
            }
            rings.Add(ring);
        }

        BuildPanelRows(n);
        if (rings.Count > 0)
            Debug.Log("[JointLimits] индикаторов суставов: " + rings.Count + " (" +
                      (activeRobot != null ? activeRobot.robotName : "робот не выбран") + ")");
    }

    private void DestroyRings()
    {
        foreach (JointRing r in rings)
        {
            if (r.mat != null) Destroy(r.mat);
            if (r.edgeMat != null) Destroy(r.edgeMat);
            if (r.go != null) Destroy(r.go);
        }
        rings.Clear();
    }

    /// <summary>Онлайн-обновление: угол, лимит, запас, цвет кольца и строки панели.</summary>
    private void RefreshJointLimits()
    {
        if (activeValidator == null || !activeValidator.Ready || rings.Count == 0) return;
        int n = Mathf.Min(rings.Count, activeValidator.Dof);
        if (jointQ == null || jointQ.Length < n)
        {
            jointQ = new double[n];
            jointPivots = new Vector3[n];
            jointAxes = new Vector3[n];
            jointMargins = new float[n];
        }
        activeValidator.CopyCurrentInto(jointQ);
        activeValidator.JointFrames(jointQ, jointPivots, jointAxes);
        activeValidator.LimitMargins(jointQ, jointMargins);

        for (int i = 0; i < n; i++)
        {
            JointRing r = rings[i];
            float margin = i < jointMargins.Length ? jointMargins[i] : 0f;
            float range = Mathf.Max(1e-4f, activeValidator.Upper[i] - activeValidator.Lower[i]);
            bool prism = activeValidator.IsPrismatic(i);
            // margin уже в «градусах» (для призмы — доля хода × 360°), поэтому frac — доля хода.
            float frac = Mathf.Clamp01(margin / 360f);
            Color c = margin <= criticalMarginDeg || frac <= criticalMarginFraction ? ColorBad
                    : (margin <= warningMarginDeg || frac <= warningMarginFraction ? ColorWarn : ColorOk);

            if (r.go != null)
            {
                r.go.transform.position = jointPivots[i];
                Vector3 axis = jointAxes[i].sqrMagnitude > 1e-6f ? jointAxes[i].normalized : Vector3.up;
                r.go.transform.rotation = Quaternion.FromToRotation(Vector3.up, axis);
            }
            if (r.mat != null) SetMaterialColor(r.mat, new Color(c.r, c.g, c.b, jointRingAlpha));
            if (r.edge != null)
            {
                Color e = new Color(c.r, c.g, c.b, 0.85f);
                r.edge.startColor = e;
                r.edge.endColor = e;
            }

            float angle = (float)activeValidator.FoldAngle(i, jointQ[i]);
            float lo = activeValidator.Lower[i], hi = activeValidator.Upper[i];
            string line = prism
                ? string.Format("J{0}  {1,7:F3} м  [{2:F3} … {3:F3}]  запас {4:F3} м",
                    i + 1, angle, lo, hi, Mathf.Min(angle - lo, hi - angle))
                : string.Format("J{0}  {1,7:F1}°  [{2:F0}° … {3:F0}°]  запас {4:F1}°",
                    i + 1, angle, lo, hi, Mathf.Min(angle - lo, hi - angle));
            if (i < panelTexts.Count && panelTexts[i] != null)
            {
                panelTexts[i].text = line;
                panelTexts[i].color = c;
            }
            if (i < panelSwatches.Count && panelSwatches[i] != null) panelSwatches[i].color = c;
        }

        if (panelTitle != null && activeRobot != null)
            panelTitle.text = "ЛИМИТЫ СУСТАВОВ · " + activeRobot.robotName;
        if (panelHint != null)
            panelHint.text = "зелёный — запас есть · жёлтый — близко к пределу (" +
                             warningMarginDeg.ToString("0") + "°) · красный — предел достигнут";
    }

    private static void SetMaterialColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_UnlitColor")) m.SetColor("_UnlitColor", c);
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        if (m.HasProperty("_EmissiveColor"))
            m.SetColor("_EmissiveColor", new Color(c.r, c.g, c.b) * 0.35f);
    }

    // ------------------------------------------------------------------ панель лимитов

    private void Build()
    {
        if (built) return;
        built = true;

        panelRoot = new GameObject("JointLimitsPanel", typeof(Canvas), typeof(CanvasScaler));
        panelRoot.transform.SetParent(transform, false);
        panelRoot.hideFlags = HideFlags.HideInHierarchy;
        Canvas canvas = panelRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = panelSortingOrder;
        CanvasScaler scaler = panelRoot.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        Image panel = KvTheme.CreatePanel((RectTransform)panelRoot.transform, "Panel",
            new Color(0.10f, 0.11f, 0.13f, 0.82f));
        panel.raycastTarget = false;            // панель — только индикация, клики не перехватывает
        panelBody = panel.rectTransform;
        panelBody.anchorMin = new Vector2(0f, 1f);
        panelBody.anchorMax = new Vector2(0f, 1f);
        panelBody.pivot = new Vector2(0f, 1f);
        panelBody.anchoredPosition = new Vector2(panelLeftOffset, -panelTopOffset);
        panelBody.sizeDelta = new Vector2(panelWidth, 54f);

        panelTitle = KvTheme.CreateText(panelBody, "Title", "ЛИМИТЫ СУСТАВОВ", 14,
            TextAnchor.UpperLeft, KvTheme.TextMain);
        RectTransform trt = panelTitle.rectTransform;
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.sizeDelta = new Vector2(-24f, 20f);
        trt.anchoredPosition = new Vector2(4f, -6f);

        panelHint = KvTheme.CreateText(panelBody, "Hint", "", 11, TextAnchor.UpperLeft, KvTheme.TextDim);
        RectTransform hrt = panelHint.rectTransform;
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.sizeDelta = new Vector2(-20f, 16f);
        hrt.anchoredPosition = new Vector2(4f, -26f);

        panelRoot.SetActive(false);
    }

    /// <summary>Строки панели: по одной на сустав (цветной квадратик + текст).</summary>
    private void BuildPanelRows(int count)
    {
        if (panelRoot == null || panelBody == null) return;
        for (int i = panelTexts.Count; i < count; i++)
        {
            Image sw = KvTheme.CreatePanel(panelBody, "Swatch" + i, ColorOk);
            sw.raycastTarget = false;
            RectTransform sr = sw.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f);
            sr.anchorMax = new Vector2(0f, 1f);
            sr.pivot = new Vector2(0f, 1f);
            sr.sizeDelta = new Vector2(9f, 15f);
            sr.anchoredPosition = new Vector2(8f, -48f - i * 19f);
            panelSwatches.Add(sw);

            Text t = KvTheme.CreateText(panelBody, "Row" + i, "", 12, TextAnchor.MiddleLeft, ColorOk);
            RectTransform tr = t.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.sizeDelta = new Vector2(-30f, 17f);
            tr.anchoredPosition = new Vector2(10f, -47f - i * 19f);
            panelTexts.Add(t);
        }
        panelBody.sizeDelta = new Vector2(panelWidth, 54f + count * 19f);
    }

    private void ApplyVisibility()
    {
        foreach (Zone z in zones)
            if (z.root != null) z.root.SetActive(showWorkspace && z.root != null);
        foreach (JointRing r in rings)
            if (r.go != null) r.go.SetActive(showJointLimits);
        if (panelRoot != null) panelRoot.SetActive(showJointLimits && showJointPanel);
    }
}
