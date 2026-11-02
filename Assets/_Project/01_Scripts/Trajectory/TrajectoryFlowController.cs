using System.Collections.Generic;
using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Полный сценарий «два лазера». Единственная кнопка действия — ЛКМ;
/// её смысл определяется тем, какой лазер включён (Z — красный, X — зелёный).
///
///   НОВЫЙ АЛГОРИТМ ПОДТВЕРЖДЕНИЯ ТРАЕКТОРИИ (ТЗ):
///   Шаг 1. Красный лазер наводится на поверхность, ЛКМ — точка фиксируется.
///           Траектории сразу НЕ показываются: идёт просчёт ВОСЬМИ вариантов.
///   Шаг 2. Как только варианты найдены, показываются ВСЕ 8 траекторий («колбаски»)
///           и СРАЗУ по каждой пускается СВОЙ фантом: все 8 едут одновременно,
///           каждый по своему пути, со скоростью `robotMoveSpeed × phantomSpeedMultiplier`
///           (ТЗ: фантомы ровно в 3 раза быстрее реального робота).
///   Шаг 3. Зелёный лазер на нужную траекторию (колбаску) + ЛКМ — эта траектория выбрана:
///           фантомы остальных траекторий удаляются, остальные траектории приглушаются
///           (остаются в сцене, чтобы на них можно было переключиться — шаг 4),
///           фантом выбранной продолжает движение (или уже доехал).
///   Шаг 4. Зелёный лазер на выбранный фантом (в пути или в конечной точке) + ЛКМ — подтверждение.
///           Если зелёный наведён на ДРУГУЮ траекторию — фантом для неё создаётся заново
///           и анимация запускается с начала (переключение траектории).
///   Шаг 5. Реальный робот едет по выбранной траектории со скоростью `robotMoveSpeed`
///           (ускорена вдвое против прежней); траектории исчезают, фантом остаётся
///           видимым как маркер конечной позы и убирается по завершении движения.
///   Этап 0: только наведение мышью — меняется лишь цвет шарика (ядовитый неон).
///   ЛКМ без красного лазера — «Включите красный лазер»; оба лазера сразу — «Выберите один лазер».
///   РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ (`PointMoveMode`, ТЗ): из `PointSelected`/`TrajectoriesShown` по Enter —
///       точка больше не следует за мышью, клавиши QWEASD двигают её в МИРОВЫХ координатах,
///       каждый интервал её проверяет Reachability Oracle, HUD показывает координаты относительно
///       базы робота и статус (зелёный/жёлтый/красный). Enter — подтвердить (траектории считаются
///       заново), Esc — отмена (точка возвращается на прежнее место). ЛКМ в режиме не действует,
///       лазеры — только индикация (сходятся в перемещаемой точке).
/// Детерминированно, без LLM.
///
/// ТОЧКА СТАВИТСЯ РОВНО НА ПОВЕРХНОСТЬ (ТЗ сессии 13.09.2026): `toolOffset = 0` по умолчанию,
/// целевая точка TCP = `hit.point` без добавления `hit.normal * toolOffset`. Механизм смещения
/// оставлен (поле + `OffsetTarget`) как ручка на случай реального упора концевого звена.
/// </summary>
public class TrajectoryFlowController : MonoBehaviour
{
    /// <summary>Сколько траекторий генерируется и показывается для каждой точки (ТЗ: ровно 8).</summary>
    public const int MaxTrajectories = 8;

    [Header("Выбор точки")]
    [Tooltip("Сколько РАЗНЫХ траекторий генерировать и показывать для каждой точки. " +
             "ТЗ — ровно 8: перебор всех конфигураций IK (плечо влево/вправо × локоть вверх/вниз × " +
             "запястье с переворотом/без; у SCARA — вылет вперёд/назад × локоть вверх/вниз), " +
             "а если ветвей меньше 8 — добор вариациями seed'а планировщика (RRT даёт другой путь). " +
             "Допустимо 1…8 (меньше — только для отладки).")]
    [Range(1, MaxTrajectories)] public int trajectoryCount = MaxTrajectories;
    public float tubeRadius = 0.05f;
    public float planningSliceMs = 12f;
    public int candidatesPerSlice = 1;

    [Header("Генерация 8 вариантов")]
    [Tooltip("Сколько ВСЕГО попыток планирования разрешено, пока ищем УНИКАЛЬНЫЕ (не совпадающие " +
             "по форме пути) траектории. Дубликаты (IsSamePath) отбрасываются, и запрос повторяется " +
             "с новым seed'ом планировщика. СЕССИЯ 15.09.2026: 48 попыток не хватало SCARA — у неё " +
             "всего 2–4 конфигурации IK, BiRRT в свободном пространстве выпрямляет путь в ту же " +
             "прямую, набор доходил до 6 из 8. Бюджет увеличен С ЗАПАСОМ: ~40 попыток на каждый " +
             "из 8 уникальных путей. Работает вместе с `detourVariants` (разные формы обхода) и " +
             "`distinctBudgetSeconds` (страховка по времени).")]
    public int distinctAttemptCap = 320;
    [Tooltip("СТРАХОВКА ПО ВРЕМЕНИ на поиск уникальных, с. Попытки идут по кадру за раз " +
             "(`candidatesPerSlice`), и 320 попыток по ~0.3–0.5 с — это минуты ожидания: после " +
             "исчерпания бюджета поиск уникальных прекращается, набор добивается и оператор " +
             "честно получает «уникальных N из 8» (а не бесконечное «считаю…»).")]
    public float distinctBudgetSeconds = 30f;
    [Tooltip("Шаг боковой разводки «колбасок» (юниты). 8 траекторий идут почти по одному пути TCP, " +
             "поэтому визуально разводятся в стороны: 3.5 / 7 / 10.5 / 14 см от истинного пути.")]
    public float tubeSpreadStep = 0.035f;
    [Tooltip("СКОЛЬКО РАЗНЫХ ОБХОДОВ строить на каждую конфигурацию IK (было: булев флаг, т.е. 1). " +
             "Обход = путь через промежуточную позу в стороне от прямой (Planner.PlanViaWaypoint): " +
             "именно он даёт РАЗНЫЕ по форме траектории, потому что RRT в свободном пространстве " +
             "всегда выпрямляет путь в одну прямую и варианты отбрасываются как дубликаты. " +
             "12 — с запасом на 8 уникальных: каждый вариант уходит на свою глубину (0.06…0.44 хода " +
             "сустава) и со своим seed'ом. 0 — выключить (только конфигурации IK + seed'ы).")]
    public int detourVariants = 12;

    [Header("Фантомы")]
    [Tooltip("Сколько фантомов показывать одновременно: по ОДНОМУ на каждую траекторию (ТЗ — 8).")]
    [Range(1, MaxTrajectories)] public int phantomCount = MaxTrajectories;
    [Tooltip("ВО СКОЛЬКО РАЗ фантом БЫСТРЕЕ реального робота (ТЗ: 3). СКОРОСТЬ ФАНТОМА = " +
             "robotMoveSpeed × phantomSpeedMultiplier — отдельной «скорости фантомов» больше нет, " +
             "поэтому перепутать скорости нельзя: робот едет со скоростью robotMoveSpeed, " +
             "фантомы — ровно в phantomSpeedMultiplier раз быстрее. При 3 и robotMoveSpeed = 1/15 " +
             "фантом идёт 1 юнит за 5 с (робот — за 15 с).")]
    [SerializeField] public float phantomSpeedMultiplier = 3f;

    /// <summary>
    /// СКОРОСТЬ ФАНТОМОВ, юнитов/с — ВЫЧИСЛЯЕТСЯ, а не настраивается: robotMoveSpeed ×
    /// phantomSpeedMultiplier (ТЗ: «если робот движется со скоростью X, фантомы — 3X»).
    /// Свойство оставлено для совместимости (диагностика/UI читают его как раньше);
    /// ПРИСВАИВАНИЕ пересчитывает множитель (value / robotMoveSpeed), чтобы «одно поле скорости»
    /// не разошлось с множителем.
    /// </summary>
    public float phantomMoveSpeed
    {
        get { return Mathf.Max(0.0001f, robotMoveSpeed * Mathf.Max(0.001f, phantomSpeedMultiplier)); }
        set
        {
            if (robotMoveSpeed > 1e-6f)
                phantomSpeedMultiplier = Mathf.Max(0.001f, value / robotMoveSpeed);
        }
    }

    [Header("Движение робота")]
    [Tooltip("СКОРОСТЬ РЕАЛЬНОГО РОБОТА на шаге 5, юнитов/с (1/15 = 1 юнит за 15 с). " +
             "ФАНТОМЫ едут в phantomSpeedMultiplier раз БЫСТРЕЕ (по умолчанию ×3 = 1 юнит за 5 с): " +
             "их скорость вычисляется из этой, отдельной «скорости фантомов» нет.")]
    [SerializeField] public float robotMoveSpeed = 1f / 15f;
    public bool slowMotionEnabled = true;

    [Header("Режим перемещения точки (PointMoveMode)")]
    [Tooltip("Скорость перемещения точки клавишами, юнитов/с. Движение — в МИРОВЫХ координатах.")]
    public float pointMoveSpeed = 0.5f;
    [Tooltip("Множитель скорости при удержании Shift.")]
    public float pointMoveFastMultiplier = 3f;
    [Tooltip("Как часто (с) в режиме перемещения опрашивается Reachability Oracle: IK, лимиты, коллизии, сингулярности.")]
    public float pointMoveOracleInterval = 0.08f;
    [Tooltip("Показывать крупный HUD режима перемещения (координаты относительно базы робота + статус).")]
    public bool pointMoveHudEnabled = true;
    [Tooltip("Диагностика ориентации TCP (оси фланца/TCP в консоль) — один раз на привязку робота.")]
    public bool logTcpFrame = true;

    [Header("Смещение и ориентация инструмента")]
    [Tooltip("Смещение ТОЧКИ от поверхности вдоль её нормали, юниты. ПО УМОЛЧАНИЮ 0 — точка ставится " +
             "РОВНО в точку попадания луча (hit.point), без добавления hit.normal * toolOffset. " +
             "Значение > 0 поднимает точку над поверхностью; это нужно ТОЛЬКО если концевое звено " +
             "реально упирается в препятствие (планировщику нужен запас ≥ 0.02, а звено — капсула " +
             "радиусом linkRadius 0.06, то есть подъём ≈ 0.08). Для столешницы подъём НЕ нужен: стол, " +
             "на котором стоит робот, помечен в CollisionWorld как опора (support) и в зазор не входит. " +
             "Поле сериализуется — значение правится в инспекторе в PlayMode, без перекомпиляции.")]
    public float toolOffset = 0f;
    [Tooltip("SurfaceNormal — ОСНОВНОЕ решение: смещение вдоль нормали поверхности (hit.normal). " +
             "WorldUpHack — ВРЕМЕННЫЙ ХАК: смещение только вверх по Y (для сравнения/отката).")]
    public ToolOffsetMode offsetMode = ToolOffsetMode.SurfaceNormal;
    [Tooltip("ШАГ 3 (позже): выравнивать концевую плоскость робота по плоскости поверхности. " +
             "На шаге 1 ориентация TCP НЕ меняется — выключено по умолчанию " +
             "(задел готов: ToolAlign + AlignToolToSurface).")]
    public bool alignToolToSurface = false;
    [Tooltip("Допуск совпадения оси инструмента с нормалью поверхности, градусы (для шага 3).")]
    public float alignAngleToleranceDeg = 6f;
    [Tooltip("Диагностика отказов по точке: точное значение toolOffset и координаты пишутся в консоль " +
             "и в файл _dsh_offset_diag.txt в каталоге отчётов (<persistentDataPath>/KazistovVv/Reports, " +
             "ФИКС 10 — не в папке проекта) — подсказка для шага 2/3.")]
    public bool logOffsetDiagnostics = true;

    /// <summary>Как смещается выбранная точка: вдоль нормали поверхности (основное) или вверх по Y (хак).</summary>
    public enum ToolOffsetMode
    {
        SurfaceNormal,
        WorldUpHack
    }

    // Стенды (столы + роботы) лежат прямо в MainScene — в рантайме ничего не создаётся.

    private LaserManager lasers;
    private PhantomManager phantoms;
    private TrajectoryExecutor executor;
    private MotionExecutor motion;
    private PointMoveHud pointHud;
    private WorkspaceVisualizer viz;              // ЗАДАЧИ 3–4: зона достижимости + лимиты суставов
    private TrajectoryMetricsPanel metricsPanel;  // ЗАДАЧА 5: панель метрик 8 траекторий

    private readonly CollisionWorld world = new CollisionWorld();
    private readonly PoseValidator validator = new PoseValidator();
    private readonly IkSolver ik = new IkSolver();
    private readonly PostureSelector posture = new PostureSelector();
    private readonly Planner planner = new Planner();
    private readonly SafetyGate gate = new SafetyGate();
    private readonly SelectionState state = new SelectionState();
    private readonly PlanMetrics metrics = new PlanMetrics();
    private readonly ReachabilityOracle oracle = new ReachabilityOracle();

    private RobotController robot;
    private FreeFlyCameraController camSelector;
    private readonly List<RobotController> allRobots = new List<RobotController>();
    private float worldTimer;
    private bool started;
    private bool phantomSelectionLatch;    // одно нажатие = одно действие
    private float planStartTime;

    // Очередь тайм-слайсов. Элемент очереди — ИНДЕКС запроса в planRequests:
    // запрос либо «планировать к этой конфигурации IK», либо «свободный seed планировщика».
    private readonly Queue<int> planQueue = new Queue<int>();
    private readonly List<PlannedTrajectory> plannedSoFar = new List<PlannedTrajectory>();
    private double[] planningStart;
    private Vector3 planningTarget;
    private int planningWant;

    /// <summary>
    /// Запрос на генерацию ОДНОЙ траектории (ЗАДАЧА 1 ТЗ «8 траекторий»).
    /// hasGoal = true — планировать к конкретной конфигурации цели (ветвь IK);
    /// hasGoal = false — отдать планировщику свободу (он сам выберет ветвь по стоимости позы),
    /// разница между такими запросами — только seed RRT.
    /// </summary>
    private struct PlanRequest
    {
        public bool hasGoal;
        public double[] goalQ;
        public bool detour;      // строить ОБХОД через промежуточную позу (реально другой маршрут)
        public int variant;      // номер варианта обхода (глубина/направление — Planner.PlanViaWaypoint)
        public bool looseGoal;   // цель прошла лимиты, но БЕЗ запаса 3° (краевая точка SCARA)
        public string tag;
        public int seed;
    }

    private readonly List<PlanRequest> planRequests = new List<PlanRequest>();
    private readonly List<double[]> goalCycle = new List<double[]>();      // годные конфигурации IK (по кругу)
    private readonly List<string> goalCycleTags = new List<string>();      // их подписи («IK S-,E+»)
    // КОНФИГУРАЦИИ «БЕЗ ЗАПАСА»: прошли WithinLimits, но запас до лимита меньше 3° — типичная
    // КРАЕВАЯ точка SCARA (призма z_5 ровно в пределе). Строгий `PlanToGoal` такие цели
    // отклоняет, а свободный `Plan` берёт — и раньше на такой точке все попытки были
    // «свободными» и без обходов, поэтому уникальных путей набиралось 4 из 8.
    // Теперь к ним строятся ОБХОДЫ (`Planner.PlanViaWaypoint(..., requireGoalMargin: false)`),
    // а траектория честно помечается «не прошла SafetyGate» в списке вариантов.
    private readonly List<double[]> goalCycleLoose = new List<double[]>();
    private readonly List<string> goalCycleLooseTags = new List<string>();
    private int goalCycleCursor;
    private bool generating;              // идёт генерация вариантов для текущей точки
    private int planAttempts;             // сколько попыток планирования уже сделано
    private int seedCursor;               // счётчик seed-вариаций планировщика
    private int detourCursor;             // счётчик вариантов обхода (0…detourVariants-1)
    private int duplicatesFiltered;       // отброшено дубликатов (IsSamePath)
    private int paddedVariants;           // из них принято «добивкой» (похожая форма пути, последнее средство)
    private int planFailures;             // попыток, не давших пути
    private int ikBranchesFound;          // конфигураций IK найдено решателем
    private int ikBranchesUsable;         // из них прошли лимиты и годны как цель
    private bool acceptDuplicates;        // добиваем до 8 даже похожими путями (см. TopUpPlanQueue)

    /// <summary>
    /// Сколько попыток разрешено в фазе «добивки похожими путями» ПОСЛЕ исчерпания бюджета
    /// уникальных (`distinctAttemptCap` / `distinctBudgetSeconds`). Нужна ровно для одного:
    /// чтобы ОТКАЗЫ планировщика (попытка без пути) не оставили набор пустым — оператор
    /// по-прежнему получает 8 «колбасок» и 8 фантомов, но в отчёте видно «уникальных N из 8»
    /// и сколько из них добито похожими по форме. Ничего не скрывается.
    /// </summary>
    private const int DuplicatePadAttempts = 64;

    // Поверхность под прицелом (эта сессия): нормаль нужна для смещения TCP и выравнивания «пятака».
    private Vector3 aimNormalNow = Vector3.up;
    private bool aimOnSurfaceNow;
    private Vector3 surfacePointLocked;                 // точка на поверхности (до смещения)
    private Vector3 surfaceNormalLocked = Vector3.up;   // нормаль этой поверхности
    private bool lockedOnSurface;                       // точка зафиксирована НА поверхности
    private bool offsetHackUsed;                        // смещение сделано хаком по Y (нет нормали)
    private float lastOffsetDiagTime = -10f;            // троттлинг записи диагностики отказов
    private float pointMoveOracleTimer;                 // таймер онлайн-проверки оракулом в режиме перемещения
    private bool tcpFrameDiagPending;                   // ждём первого кадра для лога осей TCP

    // Кэш прежних вариантов на время режима перемещения точки: по Esc траектория ИСХОДНОЙ точки
    // восстанавливается мгновенно (ТЗ, шаг 4), по Enter — считается заново для НОВОЙ (ТЗ, шаг 3).
    private readonly List<PlannedTrajectory> moveSavedPlans = new List<PlannedTrajectory>();
    private readonly List<int> moveSavedQueue = new List<int>();
    private FlowState moveSavedPhase = FlowState.Idle;

    /// <summary>Соответствие «индекс фантома → индекс траектории-кандидата» (новый алгоритм, шаг 2).</summary>
    private readonly List<int> phantomOwner = new List<int>();

    public SelectionState State => state;

    private void Awake()
    {
        lasers = gameObject.AddComponent<LaserManager>();
        phantoms = gameObject.AddComponent<PhantomManager>();
        motion = gameObject.AddComponent<MotionExecutor>();
        pointHud = gameObject.AddComponent<PointMoveHud>();   // HUD режима перемещения точки
        viz = gameObject.AddComponent<WorkspaceVisualizer>(); // зона достижимости + лимиты суставов
        metricsPanel = gameObject.AddComponent<TrajectoryMetricsPanel>();  // метрики 8 траекторий
        camSelector = GetComponent<FreeFlyCameraController>();
    }

    /// <summary>Зона достижимости и индикаторы лимитов суставов (ЗАДАЧИ 3–4) — публичный доступ
    /// для UI/VR-контура: SetWorkspaceVisible/ToggleWorkspace/SetJointLimitsVisible/ToggleJointLimits.</summary>
    public WorkspaceVisualizer Workspace => viz;
    /// <summary>Панель метрик траекторий (ЗАДАЧА 5).</summary>
    public TrajectoryMetricsPanel Metrics => metricsPanel;

    /// <summary>Включить/выключить зону достижимости (ЗАДАЧА 3) — биндов не требует.</summary>
    public void SetWorkspaceVisible(bool on) { viz?.SetWorkspaceVisible(on); }
    /// <summary>Переключить зону достижимости.</summary>
    public void ToggleWorkspace() { viz?.ToggleWorkspace(); }
    /// <summary>Включить/выключить индикаторы лимитов суставов (ЗАДАЧА 4).</summary>
    public void SetJointLimitsVisible(bool on) { viz?.SetJointLimitsVisible(on); }
    /// <summary>Переключить индикаторы лимитов суставов.</summary>
    public void ToggleJointLimits() { viz?.ToggleJointLimits(); }
    /// <summary>Включить/выключить панель метрик траекторий (ЗАДАЧА 5).</summary>
    public void SetMetricsPanelVisible(bool on) { metricsPanel?.SetVisible(on); }
    /// <summary>Переключить панель метрик траекторий.</summary>
    public void ToggleMetricsPanel() { metricsPanel?.ToggleVisible(); }

    /// <summary>Активен ли режим перемещения точки (камера по этому флагу отдаёт QWEASD точке).</summary>
    public bool IsPointMoveMode => state.phase == FlowState.PointMoveMode;

    // ------------------------------------------------------------------ доступ для ПК-интерфейса
    // Только ЧТЕНИЕ/обёртки: переходы State Machine не меняются, новых биндов не появляется.
    // Нужны десктопному интерфейсу (дерево/свойства/статус-бар/кнопки тулбара).

    /// <summary>Робот, к которому привязан поток (может быть null до первого прицела).</summary>
    public RobotController Robot => robot;
    /// <summary>Менеджер лазеров (красная/зелёная указка).</summary>
    public LaserManager Lasers => lasers;
    /// <summary>Исполнитель движения с настраиваемой скоростью (шаг 5).</summary>
    public MotionExecutor Motion => motion;
    /// <summary>Низкоуровневый исполнитель траектории (пауза/статус).</summary>
    public TrajectoryExecutor Executor => executor;
    /// <summary>Менеджер фантомов (диагностика/визуализация в дереве).</summary>
    public PhantomManager Phantoms => phantoms;
    /// <summary>HUD режима перемещения точки.</summary>
    public PointMoveHud PointHud => pointHud;
    /// <summary>Валидатор позы привязанного робота (ТОЛЬКО чтение: лимиты/оси/TCP для панели свойств).</summary>
    public PoseValidator Validator => validator;

    /// <summary>
    /// Сброс сценария из интерфейса (та же операция, что Esc): точка, траектории,
    /// фантомы и движение очищаются, состояние возвращается в `Idle`.
    /// </summary>
    public void ResetFlow(string why = "Сброшено (кнопка интерфейса)")
    {
        ResetAll(why);
    }

    // ================================================================== интерфейс новых функций
    // Блок добавлен вместе с задачами этапов 1–20 (запись/позы/сравнение/ETA/Undo/сценарии).
    // Здесь ТОЛЬКО читающие свойства и обёртки над уже существующими операциями:
    // State Machine, лазеры, планировщик, IK, валидатор, оракул и порядок этапов не менялись.

    // --- диагностика планировщика (ЭТАП 14 ТЗ)

    /// <summary>Сколько попыток планирования сделано для текущей точки (этап 14).</summary>
    public int PlanAttempts { get { return planAttempts; } }
    /// <summary>Сколько попыток не дали пути (этап 14).</summary>
    public int PlanFailures { get { return planFailures; } }
    /// <summary>Сколько вариантов отброшено как дубликаты (этап 14).</summary>
    public int DuplicatesFiltered { get { return duplicatesFiltered; } }
    /// <summary>Сколько конфигураций IK нашёл решатель (этап 14).</summary>
    public int IkBranchesFound { get { return ikBranchesFound; } }
    /// <summary>Сколько из них прошли лимиты и годны как цель (этап 14).</summary>
    public int IkBranchesUsable { get { return ikBranchesUsable; } }
    /// <summary>Идёт генерация вариантов для текущей точки.</summary>
    public bool Generating { get { return generating; } }
    /// <summary>Время старта генерации (Time.realtimeSinceStartup).</summary>
    public float PlanStartTime { get { return planStartTime; } }
    /// <summary>Сколько запросов планирования ещё в очереди.</summary>
    public int PlanQueueLength { get { return planQueue.Count; } }
    /// <summary>Сколько вариантов уже готово.</summary>
    public int PlannedCount { get { return plannedSoFar.Count; } }
    /// <summary>
    /// Сколько из найденных вариантов УНИКАЛЬНЫ (форма пути отличается — `IsSamePath`).
    /// Это и есть «N из 8» из честного отчёта оператору (ТЗ сессии 15.09.2026).
    /// </summary>
    public int UniqueVariantCount { get { return Mathf.Max(0, plannedSoFar.Count - paddedVariants); } }
    /// <summary>
    /// Сколько вариантов добито ПОХОЖИМИ по форме путями (последнее средство, когда бюджет
    /// поиска уникальных исчерпан, а отказы RRT не дали добрать честно). 0 — все уникальны.
    /// </summary>
    public int PaddedVariantCount { get { return paddedVariants; } }
    /// <summary>Сколько запросов планирования построено для точки.</summary>
    public int PlanRequestCount { get { return planRequests.Count; } }
    /// <summary>Сколько вариантов запрошено (trajectoryCount).</summary>
    public int PlanTargetCount { get { return trajectoryCount; } }
    /// <summary>Итераций последнего запуска планировщика BiRRT (этап 14).</summary>
    public int PlannerIterations { get { return planner.LastIterations; } }
    /// <summary>Отладочная строка планировщика последнего запуска (этап 14).</summary>
    public string PlannerDebug { get { return planner.LastDebug; } }
    /// <summary>Строка о ветвях IK последнего запуска (этап 14).</summary>
    public string PlannerBranchInfo { get { return planner.LastBranchInfo; } }

    /// <summary>
    /// Штатный планировщик (ТОЛЬКО ЧТЕНИЕ) — ЭТАП 5 ТЗ: waypoint-редактор использует ЕГО ЖЕ
    /// (`PlanToGoal`/`PlanViaWaypoint`/`Plan` — тот же BiRRT с теми же зазорами, лимитами и
    /// самозазором), а не отдельный планировщик. Ни один метод и ни одно поле `Planner`
    /// не менялись — добавлено только читающее свойство.
    /// </summary>
    public Planner Planner { get { return planner; } }

    // --- выбор варианта и фиксация точки из интерфейса (сравнение, Undo/Redo, сценарии)

    /// <summary>
    /// Выбрать вариант траектории по индексу ТАК ЖЕ, как это делает зелёный луч с ЛКМ
    /// (`SelectTrajectory`): появляется фантом, состояние переходит в `PhantomsMoving`.
    /// Нужно интерфейсу сравнения траекторий и Undo/Redo — логика выбора не дублируется.
    /// </summary>
    public bool SelectCandidateByIndex(int index)
    {
        if (index < 0 || index >= state.candidates.Count) return false;
        if (state.phase != FlowState.TrajectoriesShown && state.phase != FlowState.PhantomsMoving)
            return false;
        if (state.phase == FlowState.PhantomsMoving && state.selectedTrajectory == index) return true;
        SelectTrajectory(index);
        return state.selectedTrajectory == index;
    }

    /// <summary>
    /// ПОДТВЕРДИТЬ ВЫБРАННУЮ ТРАЕКТОРИЮ БЕЗ ЛАЗЕРА — пуск робота «как кнопкой ЛКМ по фантому».
    ///
    /// Добавлено для интерфейсов этапов 13–36 (кнопка «ПУСК с проверкой», голосовая команда
    /// «пуск», мобильный пульт, макросы и деревья поведения): им нужен пуск без наведения
    /// зелёного лазера. Метод делает РОВНО ТО ЖЕ, что шаг 2 подтверждения
    /// (`Stage3_Confirm` → `SelectPhantom`), поэтому логика пуска и SafetyGate не дублируются.
    /// Возвращает true, только если робот действительно начал движение.
    /// </summary>
    public bool ConfirmSelectedTrajectory()
    {
        if (state.phase != FlowState.PhantomsMoving) return false;
        if (state.phantoms.Count == 0) return false;

        int phantom = state.selectedPhantom >= 0 ? state.selectedPhantom : state.hoveredPhantom;
        if (phantom < 0 || phantom >= state.phantoms.Count) phantom = state.selectedTrajectory;
        if (phantom < 0 || phantom >= state.phantoms.Count) phantom = 0;

        SelectPhantom(phantom);
        return state.phase == FlowState.RobotMoving;
    }

    /// <summary>
    /// Зафиксировать точку из интерфейса (Undo/Redo, повтор точки): ровно та же операция,
    /// что делает красный луч с ЛКМ — включая запуск расчёта вариантов.
    /// </summary>
    public bool LockPointFromUi(Vector3 target, Vector3 surfacePoint, Vector3 surfaceNormal, bool onSurface)
    {
        if (!validator.Ready || robot == null) return false;
        if (state.phase == FlowState.RobotMoving) return false;
        if (state.phase == FlowState.PointMoveMode) return false;
        LockPointExact(target, surfacePoint,
            surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal : Vector3.up, onSurface, true);
        return true;
    }

    // --- внешнее движение (позы, записанные траектории, сценарии)

    private bool externalMotion;

    /// <summary>Идёт движение, запущенное снаружи потока (поза/запись/сценарий).</summary>
    public bool ExternalMotionRunning { get { return externalMotion; } }

    /// <summary>
    /// Запустить ВНЕШНИЙ план штатным исполнителем проекта (тот же `MotionExecutor` +
    /// `TrajectoryExecutor` + `SafetyGate`, что и на этапе 4): так «перейти в позу»,
    /// «проиграть запись» и шаги сценариев едут ровно теми же механизмами, что и обычная
    /// траектория. Исполнитель создаётся лениво — как в `SelectPhantom` (до первого выбора
    /// фантома его ещё нет).
    /// </summary>
    public bool PlayExternalPlan(PlannedTrajectory plan, double[] goalQ, string why)
    {
        if (plan == null || plan.Path == null || plan.Path.Length < 2) return false;
        if (robot == null || !validator.Ready) return false;
        if (state.phase == FlowState.RobotMoving || externalMotion) return false;

        if (executor == null)
        {
            executor = robot.GetComponent<TrajectoryExecutor>();
            if (executor == null) executor = robot.gameObject.AddComponent<TrajectoryExecutor>();
        }
        executor.Init(validator, world, gate);
        executor.timeScale = 1f;
        motion.Bind(executor, validator, world, gate);
        motion.speedMps = Mathf.Max(0.005f, robotMoveSpeed);

        gate.NotifyState();
        if (!motion.Play(plan, goalQ))
        {
            Report("Внешнее движение отклонено: " + SafetyGate.Describe(gate.LastReason), Palette.Bad);
            return false;
        }

        externalMotion = true;
        robot.enabled = false;                 // ведёт исполнитель, как на этапе 4
        Report(why, Palette.Info);
        return true;
    }

    /// <summary>Завершить внешнее движение (вызывает владелец — хаб новых функций).</summary>
    public void EndExternalMotion()
    {
        if (!externalMotion) return;
        externalMotion = false;
        if (robot != null) robot.enabled = true;
    }

    /// <summary>Остановить внешнее движение штатным стопом исполнителя.</summary>
    public void StopExternalMotion(string why = "остановлено оператором")
    {
        if (!externalMotion) return;
        if (motion != null) motion.Stop();
        EndExternalMotion();
        Report(why, Palette.Warn);
    }

    /// <summary>Поза цели, к которой едет робот на этапе 4 (null — не едет).</summary>
    public double[] MotionGoalQ
    {
        get
        {
            if (state.selectedTrajectory < 0 || state.selectedTrajectory >= state.candidates.Count)
                return null;
            TrajectoryCandidate c = state.candidates[state.selectedTrajectory];
            return c != null && c.plan != null ? c.plan.GoalQ : null;
        }
    }

    /// <summary>
    /// Цель лазеров в режиме перемещения: сама точка и цвет по вердикту оракула
    /// (ТЗ: в режиме лазеры — только визуальная индикация перемещаемой точки и её цвета).
    /// </summary>
    public bool TryGetPointMoveTarget(out Vector3 point, out Color color)
    {
        point = state.movePoint;
        color = VerdictColor(state.moveVerdict);
        return state.phase == FlowState.PointMoveMode;
    }

    // ------------------------------------------------------------------ совместимость

    /// <summary>Совместимость: старое разделение «красная/зелёная» кнопка → одна ЛКМ.</summary>
    public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirmRed, bool confirmGreen,
                          bool cancel, bool redLaserOn, bool greenLaserOn)
    {
        UpdateAim(aimPoint, aimHit, confirmRed || confirmGreen, cancel, redLaserOn, greenLaserOn);
    }

    /// <summary>Кадровый вход без нормали поверхности (VR/старый поток): поведение прежнее —
    /// точка берётся как есть, без смещения и без выравнивания «пятака».</summary>
    public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirm, bool cancel,
                          bool redLaserOn, bool greenLaserOn)
    {
        UpdateAim(aimPoint, aimHit, confirm, cancel, redLaserOn, greenLaserOn, Vector3.up, false);
    }

    /// <summary>
    /// Кадровый вход: прицел, ЛКМ (одна кнопка действия), отмена, состояние лазеров,
    /// нормаль поверхности под прицелом (hit.normal) и признак «попали в реальную геометрию».
    /// redLaserOn — красный (Z), greenLaserOn — зелёный (X).
    /// </summary>
    public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirm, bool cancel,
                          bool redLaserOn, bool greenLaserOn, Vector3 aimNormal, bool aimOnSurface)
    {
        UpdateAim(aimPoint, aimHit, confirm, cancel, redLaserOn, greenLaserOn, aimNormal, aimOnSurface,
                  false, Vector3.zero, false);
    }

    /// <summary>
    /// Полный кадровый вход (добавлен режим перемещения точки):
    /// moveToggle — Enter (вход в режим из PointSelected/TrajectoriesShown и выход из него),
    /// moveAxis — МИРОВОЕ направление движения точки по QWEASD (считает контроллер камеры),
    /// moveFast — Shift (ускорение перемещения точки).
    /// </summary>
    public void UpdateAim(Vector3 aimPoint, bool aimHit, bool confirm, bool cancel,
                          bool redLaserOn, bool greenLaserOn, Vector3 aimNormal, bool aimOnSurface,
                          bool moveToggle, Vector3 moveAxis, bool moveFast)
    {
        aimNormalNow = aimNormal.sqrMagnitude > 1e-6f ? aimNormal.normalized : Vector3.up;
        aimOnSurfaceNow = aimHit && aimOnSurface;      // точка на РЕАЛЬНОЙ поверхности: пол/стол/стена/наклонная

        if (!started)
        {
            Rebind();
            started = true;
        }

        RobotController selected = ResolveSelectedRobot(aimPoint);
        if (selected != robot) { robot = selected; Rebind(); }

        // ЗАДАЧИ 3–5 ТЗ: зона достижимости, индикаторы лимитов суставов и панель метрик
        // обслуживаются в ЛЮБОМ состоянии потока (в т.ч. в режиме перемещения точки) —
        // у них собственная частота обновления, на логику этапов они не влияют.
        if (viz != null)
        {
            viz.oracleZoneRadius = oracle.workZoneRadius;   // граница рабочей зоны — один источник правды
            viz.Tick();
        }
        if (metricsPanel != null)
        {
            metricsPanel.robotSpeed = robotMoveSpeed;
            metricsPanel.phantomSpeed = phantomMoveSpeed;
            metricsPanel.Tick();
        }

        // --- РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ: обычная логика этапов и лазеры «по прицелу» не работают ---
        if (state.phase == FlowState.PointMoveMode)
        {
            // Лазеры — только индикация: сходятся в ПЕРЕМЕЩАЕМОЙ точке (за курсором не следуют).
            lasers.UpdateRays(state.movePoint, true);
            UpdatePointMoveMode(moveToggle, moveAxis, moveFast, cancel, confirm);
            return;
        }

        lasers.UpdateRays(aimPoint, aimHit);

        // ФИКС 4 (§22): ОТМЕНА (Esc) обрабатывается ДО проверки «робот найден прицелом».
        // Раньше при не выбранном роботе управление уходило в ранний возврат ниже, и Esc
        // не доходил до ResetAll — фантомы и визуал траекторий оставались в сцене.
        // Логика сброса не менялась: тот же ResetAll, что и раньше.
        if (cancel) { ResetAll("Сброшено (Esc)"); return; }

        if (robot == null)
        {
            if (confirm) Report("Наведите шарик лазера на робота и нажмите F", Palette.Warn);
            return;
        }

        ProcessPlanningSlices();
        phantoms.Tick(Time.deltaTime);

        worldTimer -= Time.deltaTime;
        if (worldTimer <= 0f) { world.Rebuild(robot, 0.06f); worldTimer = 0.5f; }
        gate.NotifyState();
        LogTcpFrameOnce();

        // До этапа 4 ни один робот не должен двигаться сам (в т.ч. второй стенд).
        if (state.phase != FlowState.RobotMoving) HoldAllRobots();

        // Enter — вход в РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ (этапы 1, 8 ТЗ): доступен, когда точка уже
        // зафиксирована (планирование идёт или варианты показаны). Дальше клавиши QWEASD
        // двигают точку, ЛКМ и лазеры «по прицелу» не действуют.
        if (moveToggle && state.hasPoint &&
            (state.phase == FlowState.PointSelected || state.phase == FlowState.TrajectoriesShown))
        {
            EnterPointMoveMode();
            return;
        }

        switch (state.phase)
        {
            case FlowState.Idle:
                if (moveToggle) Report("Сначала выберите точку красным лазером (ЛКМ)", Palette.Warn);
                else if (confirm) Stage1_TryLockPoint(aimPoint, aimHit, redLaserOn);
                break;

            case FlowState.PointSelected:
                if (confirm) Report("Считаю варианты траекторий…", Palette.Info);
                break;

            case FlowState.TrajectoriesShown:
                // Шаг 3 ТЗ: все траектории показаны, по каждой едет свой фантом — ждём выбор зелёным.
                UpdateTrajectoryHover(greenLaserOn);
                if (confirm) Stage2_Confirm(aimPoint, aimHit, redLaserOn, greenLaserOn);
                break;

            case FlowState.PhantomsMoving:
                // Шаги 3–4 ТЗ: траектория уже выбрана. Наведение и на «колбаску» (переключение
                // на другую траекторию), и на фантом (подтверждение → движение робота).
                UpdateTrajectoryHover(greenLaserOn);
                UpdatePhantomHover(aimPoint, greenLaserOn);
                if (confirm) Stage3_Confirm(aimPoint, aimHit, redLaserOn, greenLaserOn);
                break;

            case FlowState.RobotMoving:
                // Этап 4: нажатия игнорируются полностью.
                if (!executor.IsRunning) FinishMotion();
                break;
        }
    }

    // ------------------------------------------------------------------ этапы

    /// <summary>Этап 1: ЛКМ при точке, которая ещё не подтверждена.</summary>
    private void Stage1_TryLockPoint(Vector3 aimPoint, bool aimHit, bool redOn)
    {
        // Оба лазера → работаем как «только красный».
        if (!redOn)
        {
            Report("Включите красный лазер", Palette.Warn);
            return;
        }
        if (!aimHit)
        {
            Report("Наведите красный лазер на поверхность", Palette.Warn);
            return;
        }

        // Целевая точка для робота: точка на поверхности + смещение вдоль её нормали.
        Vector3 target = OffsetTarget(aimPoint);
        ReachResult verdict = oracleQuery(target);
        if (verdict.verdict == ReachVerdict.Unreachable || verdict.verdict == ReachVerdict.Collision)
        {
            Report("Выберите другую точку (" + verdict.reason + ")", Palette.Bad);
            LogOffsetRejection(aimPoint, target, verdict);
            return;
        }

        LockPoint(target, aimPoint);
    }

    /// <summary>Шаг 3 ТЗ: все траектории показаны, по каждой едет свой фантом — ждём ЛКМ.</summary>
    private void Stage2_Confirm(Vector3 aimPoint, bool aimHit, bool redOn, bool greenOn)
    {
        if (redOn && greenOn)
        {
            Report("Выберите один лазер", Palette.Warn);
            return;
        }
        if (!redOn && !greenOn)
        {
            Report("Включите лазер", Palette.Warn);
            return;
        }

        if (redOn)
        {
            // Красный: пересчёт под новую точку (траектории не исчезают от движения мыши).
            if (!aimHit) { Report("Наведите красный лазер на поверхность", Palette.Warn); return; }
            Vector3 target2 = OffsetTarget(aimPoint);
            ReachResult verdict = oracleQuery(target2);
            if (verdict.verdict == ReachVerdict.Unreachable || verdict.verdict == ReachVerdict.Collision)
            {
                Report("Выберите другую точку (" + verdict.reason + ")", Palette.Bad);
                LogOffsetRejection(aimPoint, target2, verdict);
                return;
            }
            LockPoint(target2, aimPoint);
            return;
        }

        // Зелёный: выбор траектории по «колбаске» (фантомы остальных вариантов убираются).
        if (state.hoveredTrajectory < 0)
        {
            Report("Наведите зелёный лазер на нужную траекторию («колбаску») и нажмите ЛКМ", Palette.Warn);
            return;
        }
        SelectTrajectory(state.hoveredTrajectory);
    }

    /// <summary>
    /// Шаг 4 ТЗ: траектория уже выбрана. Зелёный луч:
    ///   * по ВЫБРАННОМУ фантому (в пути или в конечной точке) + ЛКМ → подтверждение → шаг 5
    ///     (реальный робот едет по выбранной траектории);
    ///   * по ДРУГОЙ траектории + ЛКМ → переключение: фантом этой траектории создаётся заново
    ///     и анимация запускается с начала;
    ///   * красный луч + ЛКМ — как раньше: новая точка и пересчёт.
    /// </summary>
    private void Stage3_Confirm(Vector3 aimPoint, bool aimHit, bool redOn, bool greenOn)
    {
        if (redOn && greenOn)
        {
            Report("Выберите один лазер", Palette.Warn);
            return;
        }
        if (!redOn && !greenOn)
        {
            Report("Включите лазер", Palette.Warn);
            return;
        }

        if (redOn)
        {
            // Как на шаге 3: красным — новая точка, траектории пересчитываются.
            if (!aimHit) { Report("Наведите красный лазер на поверхность", Palette.Warn); return; }
            Vector3 target3 = OffsetTarget(aimPoint);
            ReachResult verdict = oracleQuery(target3);
            if (verdict.verdict == ReachVerdict.Unreachable || verdict.verdict == ReachVerdict.Collision)
            {
                Report("Выберите другую точку (" + verdict.reason + ")", Palette.Bad);
                LogOffsetRejection(aimPoint, target3, verdict);
                return;
            }
            LockPoint(target3, aimPoint);
            return;
        }

        // 1) Зелёный ПРЯМО на ДРУГОЙ траектории → переключение с новой анимацией (шаг 4 ТЗ).
        //    Приоритет у «колбаски»: она — точный и цветной ориентир, а выбранный фантом
        //    большим объёмом модели перекрывает соседние траектории (у SCARA путь всего
        //    ~0.65 юнита, все варианты идут в 3–12 см друг от друга — иначе переключиться
        //    было бы вообще нельзя). Условие «чужая трубка ближе своей» не даёт случайно
        //    переключиться, когда оператор целится в СВОЙ фантом вдоль своей траектории.
        int other = -1;
        float dOther = float.MaxValue;
        for (int i = 0; i < state.candidates.Count; i++)
        {
            if (i == state.selectedTrajectory) continue;
            TrajectoryCandidate c = state.candidates[i];
            if (c == null || c.view == null) continue;
            float d = c.view.DistanceToRay(lasers.GreenRay);
            if (d < dOther) { dOther = d; other = i; }
        }
        float dSelf = float.MaxValue;
        if (state.selectedTrajectory >= 0 && state.selectedTrajectory < state.candidates.Count)
        {
            TrajectoryCandidate own = state.candidates[state.selectedTrajectory];
            if (own != null && own.view != null) dSelf = own.view.DistanceToRay(lasers.GreenRay);
        }
        if (other >= 0 && dOther <= tubeRadius && dOther < dSelf)
        {
            SelectTrajectory(other);
            return;
        }

        // 2) Зелёный на ВЫБРАННОМ фантоме → подтверждение (шаг 4 → шаг 5).
        if (state.selectedTrajectory >= 0 && state.hoveredPhantom >= 0 &&
            state.hoveredPhantom == state.selectedPhantom)
        {
            SelectPhantom(state.hoveredPhantom);
            return;
        }

        // 3) Ни то, ни другое — подсказки (без изменений состояния).
        if (state.hoveredTrajectory == state.selectedTrajectory && state.selectedTrajectory >= 0)
            Report("Это выбранная траектория · наведите зелёный лазер на ФАНТОМ и нажмите ЛКМ " +
                   "(или на другую траекторию — переключиться)", Palette.Warn);
        else
            Report("Наведите зелёный лазер на фантом (или на другую траекторию) и нажмите ЛКМ", Palette.Warn);
    }

    // ------------------------------------------------------------------ режим перемещения точки

    /// <summary>
    /// Этап 1/8 ТЗ: вход в режим перемещения точки (Enter после выбора точки).
    /// Точка фиксируется (за курсором больше не следует), траектории прежней точки убираются,
    /// на экране появляется HUD с координатами относительно базы робота и статусом достижимости.
    /// </summary>
    private void EnterPointMoveMode()
    {
        state.moveOrigin = state.point;      // исходная позиция — для отмены по Esc
        state.movePoint = state.point;
        state.moveVerdictValid = false;
        moveSavedPhase = state.phase;

        // Варианты прежней точки НЕ выбрасываем, а откладываем: по Esc траектория ИСХОДНОЙ точки
        // восстанавливается мгновенно (ТЗ, шаг 4), по Enter — считается заново для НОВОЙ (шаг 3).
        moveSavedPlans.Clear();
        moveSavedPlans.AddRange(plannedSoFar);
        moveSavedQueue.Clear();
        foreach (int seed in planQueue) moveSavedQueue.Add(seed);

        state.phase = FlowState.PointMoveMode;

        // Просчёт траекторий приостановлен на время режима (ТЗ, шаг 2): очередь тайм-слайсов пуста,
        // найденное отложено; после Enter/Esc просчёт возобновляется.
        planQueue.Clear();
        plannedSoFar.Clear();
        generating = false;
        HideTrajectories();
        phantoms.Hide();

        pointMoveOracleTimer = 0f;
        if (pointHud != null)
        {
            pointHud.Show(state.movePoint, PointInRobotFrame(state.movePoint));
            pointHud.SetOrigin(PointInRobotFrame(state.moveOrigin));
            pointHud.SetHudVisible(pointMoveHudEnabled);   // флаг важнее: Show() включает индикатор сам
        }
        RefreshPointMoveVerdict();

        Vector3 local = PointInRobotFrame(state.movePoint);
        Report("РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ · X " + local.x.ToString("F3") + " · Y " + local.y.ToString("F3") +
               " · Z " + local.z.ToString("F3") + " (относительно робота) · " + state.moveReason +
               " · Q/E вверх-вниз, W/S вперёд-назад, A/D влево-вправо (Shift — быстрее) · " +
               "Enter — подтвердить, Esc — отмена", VerdictColor(state.moveVerdict));
        Debug.Log("[PointMove] вход в режим: точка " + state.movePoint.ToString("F3") +
                  " · в системе робота " + local.ToString("F3") +
                  " · отложено вариантов " + moveSavedPlans.Count + " (очередь " + moveSavedQueue.Count + ")");
    }

    /// <summary>
    /// Этапы 3–6 ТЗ: кадровая работа режима — движение точки мировыми осями QWEASD,
    /// онлайн-проверка оракулом (IK, лимиты, коллизии, сингулярности) и обновление HUD.
    /// ЛКМ в режиме не действует; выход — только Enter (подтвердить) или Esc (отмена).
    /// </summary>
    private void UpdatePointMoveMode(bool toggle, Vector3 moveAxis, bool fast, bool cancelPressed, bool confirm)
    {
        if (robot == null) { ExitPointMoveMode(false, "нет робота"); return; }

        if (cancelPressed) { ExitPointMoveMode(false, "отмена (Esc)"); return; }
        if (toggle) { ExitPointMoveMode(true, "подтверждено (Enter)"); return; }
        // Мышь в режиме точку не двигает и ничего не подтверждает (ТЗ, шаг 2) — только подсказка.
        if (confirm) Report("Мышь в режиме точку не двигает · Q/E, W/S, A/D — двигать · Enter — подтвердить · Esc — отмена",
                            Palette.Warn);

        HoldAllRobots();
        worldTimer -= Time.deltaTime;
        if (worldTimer <= 0f) { world.Rebuild(robot, 0.06f); worldTimer = 0.5f; }

        // Движение — ТОЛЬКО в мировых координатах (ТЗ, этап 3): направление считает контроллер камеры.
        // Коллизии точка игнорирует (это просто Vector3, без коллайдера) — достижимость показывает оракул.
        float speed = Mathf.Max(0.01f, pointMoveSpeed) * (fast ? Mathf.Max(1f, pointMoveFastMultiplier) : 1f);
        bool moved = moveAxis.sqrMagnitude > 1e-8f;
        if (moved)
        {
            state.movePoint += moveAxis.normalized * (speed * Time.deltaTime);
            state.moveVerdictValid = false;
        }

        // Этап 4: онлайн-проверка достижимости. Оракул не дешёвый, поэтому не каждый кадр, а через
        // интервал — и ОБЯЗАТЕЛЬНО сразу после остановки точки, чтобы цвет всегда отвечал её позиции.
        pointMoveOracleTimer -= Time.deltaTime;
        if (pointMoveOracleTimer <= 0f || (!moved && !state.moveVerdictValid)) RefreshPointMoveVerdict();

        // Этап 6: координаты обновляются онлайн (каждый кадр), статус — по последней проверке.
        if (pointHud != null && pointHud.IsVisible)
            pointHud.SetPoint(state.movePoint, PointInRobotFrame(state.movePoint));
    }

    /// <summary>Онлайн-проверка точки оракулом + обновление HUD (цвет/текст статуса).</summary>
    private void RefreshPointMoveVerdict()
    {
        if (robot == null) return;
        pointMoveOracleTimer = Mathf.Max(0.02f, pointMoveOracleInterval);

        // Оракул проверяет: рабочая зона, IK с лимитами, столкновения (сцена + другие роботы), сингулярности.
        ReachResult verdict = oracleQuery(state.movePoint);
        state.moveVerdict = verdict.verdict;
        state.moveReason = verdict.reason;
        state.moveVerdictValid = true;

        if (pointHud != null && (pointHud.IsVisible || pointHud.IsMarkerVisible))
            pointHud.SetVerdict(verdict.verdict, verdict.reason);
    }

    /// <summary>
    /// Маркер ЗАФИКСИРОВАННОЙ точки (ТЗ: «точка видна всегда»). Цвет — по вердикту оракула:
    /// зелёный — достижимо, жёлтый — близко к лимиту, красный — недостижимо/столкновение.
    /// Крупный индикатор режима при этом остаётся скрытым — он принадлежит только `PointMoveMode`.
    /// </summary>
    private void ShowLockedPointMarker(Vector3 point)
    {
        if (pointHud == null) return;
        pointHud.SetPoint(point, PointInRobotFrame(point));
        pointHud.SetMarkerVisible(true);
        pointHud.SetHudVisible(false);
        ReachResult verdict = oracleQuery(point);
        pointHud.SetVerdict(verdict.verdict, verdict.reason);
    }

    /// <summary>Цвет статуса по вердикту оракула (та же неоновая палитра, что у HUD/шарика).</summary>
    private static Color VerdictColor(ReachVerdict verdict)
    {
        return verdict == ReachVerdict.Safe ? Palette.Ok
             : verdict == ReachVerdict.Marginal ? Palette.Warn : Palette.Bad;
    }

    /// <summary>
    /// Этап 7/8 ТЗ: выход из режима. Enter — подтверждение НОВОЙ позиции (траектории считаются
    /// заново для неё), Esc — отмена (точка возвращается в исходную позицию, прежняя траектория
    /// восстанавливается). Смещение по нормали повторно НЕ применяется: точка уже стоит там,
    /// куда её поставил оператор.
    /// </summary>
    private void ExitPointMoveMode(bool confirmMove, string why)
    {
        if (pointHud != null) pointHud.SetHudVisible(false);   // индикатор режима скрыт в любом случае

        if (confirmMove)
        {
            Vector3 target = state.movePoint;
            Vector3 local = PointInRobotFrame(target);
            // Точка ставится РОВНО туда, куда её привёл оператор (смещение по нормали не добавляем).
            // Перемещённая вручную точка — это точка в СВОБОДНОМ пространстве: прежняя поверхность
            // и её нормаль к ней уже не относятся (и коллизии она игнорирует — ТЗ, шаг 2).
            LockPointExact(target, target, Vector3.up, false, true);
            Debug.Log("[PointMove] выход из режима (" + why + ") · точка " + target.ToString("F3") +
                      " · в системе робота " + local.ToString("F3"));
            return;
        }

        RestoreAfterPointMoveCancel(why);
    }

    /// <summary>
    /// Шаг 4 ТЗ (Esc): точка возвращается в ИСХОДНУЮ позицию, прежняя траектория восстанавливается
    /// МГНОВЕННО (варианты отложены на входе в режим), просчёт возобновляется с того места, где был
    /// остановлен. Смещение по нормали не применяется — точка и не двигалась.
    /// </summary>
    private void RestoreAfterPointMoveCancel(string why)
    {
        state.movePoint = state.moveOrigin;
        state.point = state.moveOrigin;
        state.hasPoint = true;
        state.ResetTrajectorySelection();
        phantoms.Hide();
        HideTrajectories();

        planningTarget = state.point;                 // точка та же — планировщик продолжает с прежней цели
        plannedSoFar.Clear();
        plannedSoFar.AddRange(moveSavedPlans);        // найденные варианты прежней точки — назад
        planQueue.Clear();
        foreach (int reqIndex in moveSavedQueue) planQueue.Enqueue(reqIndex);

        Vector3 local = PointInRobotFrame(state.point);
        string coords = "X " + local.x.ToString("F3") + " · Y " + local.y.ToString("F3") +
                        " · Z " + local.z.ToString("F3") + " (относительно робота)";

        if (plannedSoFar.Count > 0)
        {
            generating = planQueue.Count > 0;
            state.phase = planQueue.Count > 0 ? FlowState.PointSelected : FlowState.TrajectoriesShown;
            RebuildCandidateVisuals();                // траектория прежней точки снова на сцене
            // Траектории вернулись → по каждой снова пускаем фантом (шаг 2 ТЗ), чтобы
            // после отмены перемещения сцена была ровно такой же, как до входа в режим.
            bool back = planQueue.Count == 0 && BuildAllPhantoms();
            Report("Перемещение отменено (Esc) · точка возвращена: " + coords +
                   " · траектория восстановлена" + (planQueue.Count > 0 ? " (досчитываю варианты)" : "") +
                   (back ? " · фантомы запущены заново" : ""),
                   Palette.Info);
        }
        else
        {
            // Планирование ещё не дало ни одного варианта — считаем заново для той же точки.
            state.phase = FlowState.PointSelected;
            BuildPlanRequests();                      // тот же набор: ветви IK + seed-вариации
            for (int i = 0; i < planRequests.Count; i++) planQueue.Enqueue(i);
            generating = true;
            Report("Перемещение отменено (Esc) · точка возвращена: " + coords + " · считаю варианты…",
                   Palette.Info);
        }

        ShowLockedPointMarker(state.point);
        Debug.Log("[PointMove] выход из режима (" + why + ") · точка возвращена " + state.point.ToString("F3") +
                  " · в системе робота " + local.ToString("F3") +
                  " · восстановлено вариантов " + plannedSoFar.Count + " (фаза " + state.phase +
                  ", вход был из " + moveSavedPhase + ")");
    }

    /// <summary>Координаты точки в системе робота (ноль — корень робота, «его Base»).</summary>
    private Vector3 PointInRobotFrame(Vector3 worldPoint)
    {
        if (robot == null) return worldPoint;
        return robot.transform.InverseTransformPoint(worldPoint);
    }

    /// <summary>
    /// Диагностика ориентации TCP (разово на привязку робота): какой локальной осью TCP
    /// смотрит инструмент и как оси TCP расположены в мире. Нужна для проверки соглашения
    /// «ось X концевой точки смотрит вверх» (см. PROJECT_CONTEXT.md, открытый вопрос).
    /// </summary>
    private void LogTcpFrameOnce()
    {
        if (!tcpFrameDiagPending || !logTcpFrame) return;
        if (robot == null || robot.tcp == null) return;
        tcpFrameDiagPending = false;

        Transform tcp = robot.tcp;
        if (validator.Dof < 6)
        {
            Debug.Log("[TCPDiag] робот '" + robot.robotName + "' — ось фланца не определена (не 6 осей), пропуск");
            return;
        }
        Vector3 axisWorld = validator.AxisWorld(validator.Dof - 1, validator.CopyCurrent());
        Vector3 axisInTcp = tcp.InverseTransformDirection(axisWorld);
        Debug.Log(string.Format(
            "[TCPDiag] '{0}': ось инструмента в системе TCP = ({1:F3}, {2:F3}, {3:F3}) (|ось|={4:F2}); " +
            "TCP.up={5}, TCP.right={6}, TCP.forward={7}; угол оси инструмента к вертикали мира {8:F1}°",
            robot.robotName, axisInTcp.x, axisInTcp.y, axisInTcp.z, axisInTcp.magnitude,
            tcp.up.ToString("F3"), tcp.right.ToString("F3"), tcp.forward.ToString("F3"),
            Vector3.Angle(axisWorld, Vector3.up)));
    }

    // ------------------------------------------------------------------ планирование

    private void LockPoint(Vector3 target, Vector3 surfacePoint)
    {
        LockPointExact(target, surfacePoint, aimOnSurfaceNow ? aimNormalNow : Vector3.up, aimOnSurfaceNow);
    }

    /// <summary>
    /// Фиксация точки с ЯВНО заданной поверхностью. Цель берётся как есть: смещение по нормали
    /// уже применено там, где точка выбиралась (`OffsetTarget`) либо точка перемещена оператором
    /// вручную (режим перемещения) — второй раз offset НЕ добавляется.
    /// </summary>
    private void LockPointExact(Vector3 target, Vector3 surfacePoint, Vector3 surfaceNormal, bool onSurface,
                                bool movedByOperator = false)
    {
        // Поверхность фиксируется вместе с точкой: по её нормали смещён TCP и по ней же
        // будет выровнена концевая плоскость инструмента (см. AlignToolToSurface).
        surfacePointLocked = surfacePoint;
        surfaceNormalLocked = surfaceNormal.sqrMagnitude > 1e-6f ? surfaceNormal.normalized : Vector3.up;
        lockedOnSurface = onSurface;

        state.point = target;
        state.aimAtLock = surfacePoint;
        state.hasPoint = true;
        state.ResetTrajectorySelection();
        phantoms.Hide();
        HideTrajectories();

        planningStart = validator.CopyCurrent();
        planningTarget = target;
        BuildPlanRequests();                       // ЗАДАЧА 1 ТЗ: 8 запросов (ветви IK + seed'ы)
        plannedSoFar.Clear();
        planQueue.Clear();
        for (int i = 0; i < planRequests.Count; i++) planQueue.Enqueue(i);
        generating = true;
        state.phase = FlowState.PointSelected;
        planStartTime = Time.realtimeSinceStartup;

        // Точка зафиксирована — показываем её маркер (ТЗ: «точка видна всегда»), цвет по оракулу.
        // Крупный индикатор режима здесь НЕ показываем: он принадлежит только PointMoveMode.
        ShowLockedPointMarker(target);

        if (movedByOperator)
        {
            Vector3 local = PointInRobotFrame(target);
            Report("Точка перемещена вручную · X " + local.x.ToString("F3") + " · Y " + local.y.ToString("F3") +
                   " · Z " + local.z.ToString("F3") + " (относительно робота) · считаю варианты траекторий…",
                   Palette.Info);
        }
        else
        {
            Report("Точка принята · " + OffsetDescription() + " · считаю варианты траекторий…", Palette.Info);
        }
    }

    /// <summary>
    /// Целевая точка для робота: точка на поверхности + нормаль * toolOffset.
    ///
    /// С 13.09.2026 `toolOffset` по умолчанию **0**, поэтому здесь возвращается РОВНО
    /// точка попадания луча (`hit.point`) — визуального и целевого смещения нет.
    /// Смещение осталось ручкой: поднимать > 0 нужно только если концевое звено реально
    /// упирается в препятствие (запас планировщика 0.02 + радиус капсулы звена 0.06 ≈ 0.08).
    /// Для столешницы подъём не нужен — стол робота помечен как опора (`support`)
    /// и в расчёт зазора не входит (см. `CollisionWorld.Rebuild`).
    /// Для точки в свободном пространстве смещения нет ни при каком значении (ТЗ).
    /// Режим WorldUpHack — ВРЕМЕННЫЙ ХАК из ТЗ (п.4): смещение только вверх по Y, без нормали.
    /// </summary>
    private Vector3 OffsetTarget(Vector3 surfacePoint)
    {
        float off = Mathf.Max(0f, toolOffset);
        offsetHackUsed = false;

        if (offsetMode == ToolOffsetMode.WorldUpHack)
        {
            offsetHackUsed = true;
            return surfacePoint + Vector3.up * off;
        }
        if (!aimOnSurfaceNow) return surfacePoint;     // воздух/рабочая плоскость — не смещаем
        return surfacePoint + aimNormalNow * off;      // основное решение — вдоль нормали
    }

    /// <summary>Короткая строка для подсказки: чем и на сколько смещён TCP.</summary>
    private string OffsetDescription()
    {
        if (offsetHackUsed)
            return "смещение " + toolOffset.ToString("0.00") + " по Y (ВРЕМЕННЫЙ ХАК)";
        if (!aimOnSurfaceNow)
            return "точка не на поверхности — TCP без смещения";
        if (toolOffset <= 0.0001f)
            return "точка РОВНО на поверхности (toolOffset = 0)";
        return "TCP на " + toolOffset.ToString("0.00") + " по нормали поверхности";
    }

    /// <summary>
    /// ЗАДАЧА 1 ТЗ («8 траекторий вместо одной»). Набор ПОПЫТОК планирования для точки:
    ///   1) СНАЧАЛА все конфигурации IK цели, какие даёт решатель:
    ///      робот — до 8 ветвей (плечо влево/вправо × локоть вверх/вниз × запястье с переворотом/без),
    ///      SCARA — до 4 (вылет вперёд/назад × локоть вверх/вниз, аналитическая 2R-кинематика).
    ///      Каждая ветвь = СВОЯ траектория: робот приходит в ту же точку ДРУГОЙ позой.
    ///   2) Если ветвей меньше `trajectoryCount` — список добивается вариациями seed'а
    ///      планировщика: BiRRT-Connect детерминирован, но разные seed'ы дают разные обходы.
    ///   3) Если и этого мало (ветви отсеялись по лимитам, пути совпали) — вторая волна
    ///      seed'ов добавляется уже в `TopUpPlanQueue` во время просчёта.
    /// Ничего не рисуется до конца генерации (шаг 1 ТЗ): все «колбаски» появляются разом.
    /// </summary>
    private void BuildPlanRequests()
    {
        planRequests.Clear();
        goalCycle.Clear();
        goalCycleTags.Clear();
        goalCycleLoose.Clear();
        goalCycleLooseTags.Clear();
        goalCycleCursor = 0;
        planAttempts = 0;
        seedCursor = 0;
        detourCursor = 0;
        duplicatesFiltered = 0;
        paddedVariants = 0;
        planFailures = 0;
        ikBranchesFound = 0;
        ikBranchesUsable = 0;
        acceptDuplicates = false;

        planningWant = Mathf.Clamp(trajectoryCount, 1, MaxTrajectories);

        // --- 1) КОНФИГУРАЦИИ IK ЦЕЛИ -------------------------------------------------------
        // Единый с планировщиком список: аналитические ветви IK + CCD-резерв, если аналитика
        // не дала решений (для SCARA это ОСНОВНОЙ путь — её аналитика строит z_5 только в
        // пределах хода призмы, а точка на столе лежит ниже, поэтому ветвей «по лимитам» нет).
        if (planner.Ready && planningStart != null)
        {
            List<Planner.GoalConfig> branches =
                planner.SolveGoalConfigs(planningStart, planningTarget, MaxTrajectories, 1000 + 7919);
            ikBranchesFound = branches != null ? branches.Count : 0;
            if (branches != null)
            {
                for (int i = 0; i < branches.Count; i++)
                {
                    Planner.GoalConfig g = branches[i];
                    if (g.q == null) continue;
                    if (!validator.WithinLimits(g.q)) continue;
                    if (validator.LimitMargin(g.q) < planner.minLimitMarginDeg)
                    {
                        // Краевая цель: лимиты соблюдены, но запаса 3° нет (у SCARA — призма
                        // ровно в пределе). Прямой путь к ней процесс не берёт (SafetyGate
                        // отклонит), но ОБХОД к ней допустим и даёт РАЗНЫЕ формы пути —
                        // иначе такая точка давала только «свободные» пути-дубликаты.
                        goalCycleLoose.Add(g.q);
                        goalCycleLooseTags.Add("IK " + g.tag);
                        continue;
                    }
                    ikBranchesUsable++;
                    goalCycle.Add(g.q);
                    goalCycleTags.Add("IK " + g.tag);              // «IK S-,E+,W-» / «IK CCD»
                    if (planRequests.Count < planningWant)
                        planRequests.Add(new PlanRequest
                        {
                            hasGoal = true,
                            goalQ = g.q,
                            tag = "IK " + g.tag,
                            seed = 1000 + i * 7919
                        });
                }
            }
        }

        // --- 2) ДОБОР: конфигурации IK × вариации маршрута и seed'а планировщика ------------
        // ТЗ: «если IK даёт меньше 8 уникальных решений — комбинировать с вариациями seed'ов,
        // чтобы добить до 8». Конфигурации идут по кругу (у SCARA их 2–4), seed каждый раз новый;
        // часть вариаций строится ОБХОДОМ через промежуточную позу (PlanViaWaypoint) — иначе
        // RRT в свободном пространстве всегда выпрямляет путь в ту же прямую, и все 8 вариантов
        // оказываются дубликатами одной траектории.
        while (planRequests.Count < planningWant) AddSeedRequest();
    }

    /// <summary>
    /// Добавить один запрос-вариацию: конфигурация IK (по кругу) + НОВЫЙ seed планировщика.
    /// Каждый второй запрос — «обход» через промежуточную позу (реально другой маршрут),
    /// первый — прямой путь в ту же конфигурацию. Если годных конфигураций IK нет вовсе —
    /// запрос «свободный»: ветвь выбирает сам `Planner`.
    ///
    /// ТРИ ИСТОЧНИКА РАЗЛИЧИЯ (ТЗ: «добить до 8 вариациями seed'а»), все три работают сразу:
    ///   1) НОВЫЙ seed RRT — `seed = 1000 + seedCursor * 7919`;
    ///   2) ДРУГАЯ КОНФИГУРАЦИЯ IK (у SCARA — вылет вперёд/назад × локоть вверх/вниз) —
    ///      конфигурации идут по кругу `goalCycle`;
    ///   3) ДРУГАЯ ФОРМА ОБХОДА — номер `variant` идёт по кругу `detourVariants` и задаёт
    ///      глубину отклонения промежуточной позы (микро 0.06 … макро 0.44 хода сустава).
    /// </summary>
    private void AddSeedRequest()
    {
        seedCursor++;
        int seed = 1000 + seedCursor * 7919;

        // Цели идут по кругу: сначала СТРОГИЕ ветви IK (с запасом лимитов), а если таких нет
        // вовсе — «краевые» конфигурации без запаса (обход к ним допустим, см. goalCycleLoose).
        bool strict = goalCycle.Count > 0;
        bool loose = !strict && goalCycleLoose.Count > 0;
        bool useGoal = strict || loose;
        List<double[]> cycle = strict ? goalCycle : (loose ? goalCycleLoose : null);
        List<string> cycleTags = strict ? goalCycleTags : (loose ? goalCycleLooseTags : null);
        int gi = useGoal ? goalCycleCursor++ % cycle.Count : 0;

        bool detour = detourVariants > 0 && useGoal && (seedCursor % 2 == 1);
        int variant = detour ? detourCursor++ % Mathf.Max(1, detourVariants) : 0;
        planRequests.Add(new PlanRequest
        {
            hasGoal = useGoal,
            goalQ = useGoal ? cycle[gi] : null,
            detour = detour,
            variant = variant,
            looseGoal = loose,
            tag = (useGoal ? cycleTags[gi] : "CCD") + (detour ? " + обход" : " + seed"),
            seed = seed
        });
    }

    /// <summary>
    /// Просчёт в тайм-слайсах. За срез — не больше `candidatesPerSlice` попыток и не больше
    /// бюджета `planningSliceMs`. Варианты копятся в `plannedSoFar`, дубликаты (IsSamePath)
    /// отбрасываются и заменяются новой попыткой с другим seed'ом (ТЗ).
    /// Пока не набрано 8 — очередь добивается (TopUpPlanQueue).
    /// </summary>
    private void ProcessPlanningSlices()
    {
        if (!generating) return;

        if (planQueue.Count == 0) TopUpPlanQueue();
        if (planQueue.Count == 0) { FinishGeneration(); return; }

        var sw = System.Diagnostics.Stopwatch.StartNew();
        int done = 0;
        while (planQueue.Count > 0 && done < Mathf.Max(1, candidatesPerSlice) &&
               sw.Elapsed.TotalMilliseconds < planningSliceMs)
        {
            int reqIndex = planQueue.Dequeue();
            done++;
            if (reqIndex < 0 || reqIndex >= planRequests.Count) continue;
            PlanRequest req = planRequests[reqIndex];
            planAttempts++;

            PlannedTrajectory t = PlanOne(req);
            if (t == null) { planFailures++; continue; }
            t.BranchTag = req.tag;
            AddVariant(t);
        }
        sw.Stop();
        metrics.RecordPlan(sw.Elapsed.TotalMilliseconds, plannedSoFar.Count > 0,
            plannedSoFar.Count > 0 ? plannedSoFar[0] : null);

        // Набор уже полон: остальные запросы очереди не нужны — не тратим кадры на RRT
        // (иначе после находки 8-го уникального пути генерация «доигрывала» бы всю очередь).
        if (plannedSoFar.Count >= planningWant) planQueue.Clear();

        // ВАЖНО: очередь могла опустеть ИМЕННО в этом срезе, а вариантов ещё меньше 8 —
        // поэтому добор вызывается ещё раз ПЕРЕД завершением генерации. Без этого повторного
        // вызова генерация заканчивалась на первом же наборе запросов (найдено 3–4 из 8).
        if (planQueue.Count == 0) TopUpPlanQueue();

        // Промежуточные результаты НЕ показываем (шаг 1 ТЗ: «траектории не показываются сразу»):
        // все траектории появляются ОДНОВРЕМЕННО, когда генерация закончена, — вместе с фантомами (шаг 2).
        if (planQueue.Count == 0) FinishGeneration();
    }

    /// <summary>Одна попытка планирования по запросу (ветвь IK — точно в её конфигурацию; seed — свобода).</summary>
    private PlannedTrajectory PlanOne(PlanRequest req)
    {
        if (req.hasGoal && req.goalQ != null)
        {
            // Вариация-«обход»: сначала настоящий другой маршрут (со СВОЕЙ глубиной обхода
            // по номеру варианта), при неудаче — прямой путь.
            if (req.detour)
            {
                PlannedTrajectory detour = planner.PlanViaWaypoint(planningStart, req.goalQ, req.seed,
                                                                   req.tag, 12, req.variant,
                                                                   !req.looseGoal);
                if (detour != null) return detour;
            }
            // Краевая цель без запаса лимитов: строгий `PlanToGoal` её не возьмёт — идём
            // свободным путём (как раньше вёл себя весь такой запрос).
            if (!req.looseGoal)
                return planner.PlanToGoal(planningStart, req.goalQ, req.seed, req.tag);
        }

        // Свободный запрос: планировщик сам берёт лучшую ветвь IK и строит путь этим seed'ом.
        List<PlannedTrajectory> list = planner.Plan(planningStart, planningTarget, 1, req.seed);
        return list != null && list.Count > 0 ? list[0] : null;
    }

    /// <summary>
    /// Добавить найденную траекторию в набор. Дубликат (та же цель И та же форма пути —
    /// `IsSamePath`) отбрасывается СТРОГО: пока есть бюджет поиска (`acceptDuplicates == false`),
    /// дубликат не попадает в набор ни при каких условиях — вместо него будет новая попытка
    /// с другим seed'ом, другой конфигурацией IK или другой глубиной обхода.
    ///
    /// Только когда бюджет уникальных исчерпан (и по попыткам, и по времени), похожие пути
    /// принимаются — иначе оператор не увидел бы «8 колбасок и 8 фантомов» из-за отказов RRT.
    /// Каждый такой путь считается в `paddedVariants`: в отчёте и в подсказке оператор видит
    /// «уникальных N из 8» и сколько добито похожими, то есть ничего не скрывается.
    /// </summary>
    private void AddVariant(PlannedTrajectory t)
    {
        if (t == null || plannedSoFar.Count >= planningWant) return;

        bool dup = false;
        foreach (PlannedTrajectory ex in plannedSoFar)
            if (IsSamePath(ex, t)) { dup = true; break; }

        if (dup)
        {
            duplicatesFiltered++;
            if (!acceptDuplicates) return;      // фаза 1: строгая фильтрация — пробуем другой вариант
            paddedVariants++;                   // фаза 2 (последнее средство): похожий путь, но учтён
        }
        plannedSoFar.Add(t);
    }

    /// <summary>
    /// Добивание набора до `planningWant`, когда очередь опустела, а вариантов ещё мало.
    ///
    /// ФАЗА 1 (уникальные): пока не исчерпан бюджет — `distinctAttemptCap` попыток ИЛИ
    /// `distinctBudgetSeconds` секунд — добавляются новые seed'ы/конфигурации IK/глубины обхода,
    /// и все дубликаты (`IsSamePath`) отбрасываются. Запросов добавляется С ЗАПАСОМ (не ровно
    /// `need`): часть попыток отсеется дубликатами и отказами RRT.
    ///
    /// ФАЗА 2 (добивка): бюджет исчерпан, а уникальных всё ещё меньше 8 — похожие пути
    /// принимаются (учёт в `paddedVariants`), но с собственным запасом `DuplicatePadAttempts`,
    /// чтобы отказы планировщика не оставили набор короче 8. Если и этого не хватило —
    /// генерация завершается, и оператор ЧЕСТНО получает «Получено N из 8, причина — …».
    /// </summary>
    private void TopUpPlanQueue()
    {
        if (plannedSoFar.Count >= planningWant) return;
        int hardLimit = distinctAttemptCap + DuplicatePadAttempts;
        if (planAttempts >= hardLimit) return;                            // жёсткий предел попыток

        // Бюджет уникальных исчерпан по попыткам ИЛИ по времени — переходим к добивке.
        bool budgetSpent = planAttempts >= distinctAttemptCap ||
                           (distinctBudgetSeconds > 0f &&
                            Time.realtimeSinceStartup - planStartTime >= distinctBudgetSeconds);
        if (budgetSpent) acceptDuplicates = true;

        int need = planningWant - plannedSoFar.Count;
        // Фаза 1: запас ×4 (дубликаты + отказы RRT съедают попытки), минимум 8.
        int add = acceptDuplicates ? need : Mathf.Max(need * 4, MaxTrajectories);
        for (int i = 0; i < add; i++)
        {
            AddSeedRequest();
            planQueue.Enqueue(planRequests.Count - 1);
        }
    }

    /// <summary>
    /// Генерация закончилась: строим визуал по найденным вариантам (именно здесь заполняется
    /// `state.candidates` — промежуточные срезы больше не рисуются), пускаем по КАЖДОЙ
    /// траектории свой фантом и объясняем в лог, почему вариантов получилось не 8.
    /// ВАЖЕН ПОРЯДОК: сначала визуал, и только потом проверка «найдено/не найдено».
    /// </summary>
    private void FinishGeneration()
    {
        generating = false;

        if (plannedSoFar.Count > 0)
        {
            AlignAllCandidates();      // «пятак» (ШАГ 3, по умолчанию выключен) — ДО фантомов
            RebuildCandidateVisuals();
        }

        LogVariantReport();

        if (state.candidates.Count == 0)
        {
            state.phase = FlowState.Idle;
            // ФИКС 6: у SCARA причина отказа почти всегда одна и та же — точка лежит РОВНО
            // на уровне столешницы, то есть призма стоит в самом низу хода (запас 0°), а
            // планировщику нужно ≥ 3°. Оператор получает не «точка не найдена», а прямое
            // объяснение с числами и подсказкой. Геометрия SCARA при этом НЕ «чинится».
            string prismHint = PrismLowerLimitHint(planningTarget);
            if (prismHint != null)
            {
                Report(prismHint, Palette.Bad);
                Debug.LogWarning("[SCARA] " + prismHint);
            }
            else
            {
                Report("Выберите другую точку — траектория не найдена", Palette.Bad);
            }
            LogPlanFailure();
            return;
        }

        // ШАГ 2 ТЗ: траектории найдены → показываем ВСЕ и СРАЗУ пускаем по каждой
        // свой фантом (все едут одновременно со старой скоростью фантомов).
        state.phase = FlowState.TrajectoriesShown;
        bool phantomOk = BuildAllPhantoms();

        // ЧЕСТНЫЙ ОТЧЁТ (ТЗ сессии 15.09.2026): оператор всегда видит, сколько траекторий
        // УНИКАЛЬНЫ и, если их меньше 8, — почему. Ничего не скрывается: «добивка» похожими
        // путями (если она случилась) называется своим именем отдельным счётчиком.
        int unique = UniqueVariantCount;
        string head = unique < MaxTrajectories
            ? "Получено " + unique + " из " + MaxTrajectories + " уникальных — " + ShortVariantReason() +
              (paddedVariants > 0
                  ? " · ещё " + paddedVariants + " добито похожими по форме (показаны, но не уникальны)"
                  : "")
            : "Вариантов: " + state.candidates.Count + " (уникальных " + unique + " из " + MaxTrajectories + ")";
        // Честность до конца: если часть вариантов не прошла SafetyGate (у краевых точек SCARA
        // это обходы к позе, где призма стоит ровно в пределе), оператор узнаёт и об этом.
        int unsafeCount = 0;
        foreach (TrajectoryCandidate c in state.candidates)
            if (c != null && !c.safe) unsafeCount++;
        if (unsafeCount > 0)
            head += " · НЕ прошли SafetyGate: " + unsafeCount + " (показаны как «невыгодная»)";
        Report(head +
               (phantomOk ? " · по каждой траектории пущен фантом (скорость " +
                            phantomMoveSpeed.ToString("0.000") + " ю/с = " +
                            phantomSpeedMultiplier.ToString("0.##") + "× скорости робота, " +
                            FormatSpeed(phantomMoveSpeed) + ")"
                          : " · фантомы не созданы") +
               " · зелёным лучом (X) наведите на траекторию и нажмите ЛКМ" +
               " · Enter — режим перемещения точки",
               unique >= MaxTrajectories && phantomOk ? Palette.Info : Palette.Warn);
    }

    /// <summary>
    /// ФИКС 6. Понятное объяснение для случая «точка ровно на столешнице»: у SCARA ход
    /// призмы z_5 заканчивается на уровне стола, поэтому такая точка даёт запас 0° до
    /// нижнего предела, а планировщику нужно ≥ 3° (иначе SafetyGate отклоняет путь).
    /// Возвращает null, если случай не тот (робот не SCARA, IK не сошлась, запас достаточен).
    ///
    /// Числа берутся фактически: запас считается штатным `LimitMargin` (у призмы полный
    /// ход = 360°, см. `PoseValidator.LimitMargin`), порог — `Planner.minLimitMarginDeg`.
    /// </summary>
    private string PrismLowerLimitHint(Vector3 target)
    {
        if (validator == null || !validator.Ready) return null;
        if (validator.Dof != 3 || !validator.IsPrismatic(2)) return null;   // только SCARA
        if (!oracle.Ready) return null;

        double[] q;
        if (!validator.SolveIk(target, validator.CopyCurrent(), out q, 160) || q == null) return null;

        double lo = validator.Lower[2];
        double range = validator.Upper[2] - lo;
        if (range <= 1e-4) return null;

        float margin = validator.LimitMargin(q);
        bool atBottom = q[2] <= lo + 1e-4;
        int marginDeg = Mathf.Max(0, Mathf.RoundToInt(margin));
        if (!atBottom && marginDeg >= Mathf.RoundToInt(planner.minLimitMarginDeg)) return null;

        string where = atBottom || marginDeg <= 0
            ? "Точка на уровне столешницы"
            : "Точка у нижнего предела призмы";
        return where + " · запас до нижнего предела призмы " + marginDeg + "° < " +
               planner.minLimitMarginDeg.ToString("0") + "° требуемых · " +
               "выберите точку на 5–10 см выше";
    }

    /// <summary>
    /// Короткая причина, почему УНИКАЛЬНЫХ траекторий меньше 8 (для подсказки оператору).
    /// ТЗ сессии 15.09.2026: причина называется прямо — «дубликаты RRT» и «малая рабочая зона»,
    /// а не общими словами «планировщик не нашёл больше путей».
    /// </summary>
    private string ShortVariantReason()
    {
        // Малая рабочая зона: конфигураций IK нет вовсе или они не проходят лимиты —
        // у SCARA это точка слишком низко/высоко относительно хода призмы.
        if (ikBranchesFound == 0) return "решатель IK не дал ветвей — малая рабочая зона";
        if (ikBranchesUsable == 0) return "ветви IK не прошли лимиты — малая рабочая зона";
        if (ikBranchesUsable <= 2 && duplicatesFiltered > 0)
            return "малая рабочая зона (годных конфигураций IK " + ikBranchesUsable +
                   ") + дубликаты RRT";
        if (duplicatesFiltered > 0 && planFailures > 0)
            return "дубликаты RRT (IsSamePath) + отказы планировщика (" + planFailures + ")";
        if (duplicatesFiltered > 0) return "дубликаты RRT (IsSamePath)";
        if (planFailures > 0) return "отказы планировщика: RRT не нашёл путь (" + planFailures + ")";
        return "планировщик не нашёл больше путей — малая рабочая зона";
    }

    /// <summary>
    /// Отчёт о генерации 8 вариантов (ТЗ: «если появляется меньше — залогировать, почему»).
    /// Пишется ОДИН раз на точку, в консоль Unity. Уникальные и «добитые» пути считаются
    /// РАЗДЕЛЬНО — в отчёте видно и то, сколько нашлось честно, и то, сколько добито.
    /// </summary>
    private void LogVariantReport()
    {
        string line = string.Format(
            "[Variants] {0} · Dof={1} · конфигураций IK: {2} (годных по лимитам {3}) · " +
            "запросов: {4} · попыток: {5} (бюджет уникальных {6}) · УНИКАЛЬНЫХ путей: {7} из {10} · " +
            "добито похожими: {8} · отброшено дубликатов: {9} · отказов планировщика: {11} · " +
            "показано траекторий: {12}",
            robot != null ? robot.robotName : "нет робота", validator.Dof,
            ikBranchesFound, ikBranchesUsable, planRequests.Count, planAttempts, distinctAttemptCap,
            UniqueVariantCount, paddedVariants, duplicatesFiltered, MaxTrajectories, planFailures,
            state.candidates.Count);

        if (UniqueVariantCount < MaxTrajectories)
            Debug.LogWarning(line + " · ПРИЧИНА: " + ShortVariantReason());
        else
            Debug.Log(line);
    }

    /// <summary>
    /// Один и тот же путь или нет? Дубликатом считается только совпадение И цели,
    /// И ФОРМЫ пути (конфигурации робота в 25/50/75% пути). Раньше сравнивались лишь
    /// длина, время и цель — из-за этого разные обходы (разные seed RRT) схлопывались
    /// и «вариантов» оставалось 1–2, хотя робот проходит их РАЗНЫМИ позами.
    /// </summary>
    private static bool IsSamePath(PlannedTrajectory a, PlannedTrajectory b)
    {
        if (a == null || b == null) return false;
        if (System.Math.Abs(a.Length - b.Length) > 0.02) return false;
        double[] qa = a.GoalQ, qb = b.GoalQ;
        if (qa == null || qb == null || qa.Length != qb.Length) return false;
        double dGoal = 0;
        for (int i = 0; i < qa.Length; i++) dGoal += System.Math.Abs(qa[i] - qb[i]);
        if (dGoal >= 3.0) return false;                  // разные конфигурации цели

        for (int k = 1; k <= 3; k++)
        {
            double[] pa = SampleAt(a, k / 4.0), pb = SampleAt(b, k / 4.0);
            if (pa == null || pb == null) continue;
            double d = 0;
            int n = System.Math.Min(pa.Length, pb.Length);
            for (int i = 0; i < n; i++) d += System.Math.Abs(pa[i] - pb[i]);
            if (d >= 12.0) return false;                 // форма пути заметно другая (≈4° на сустав)
        }
        return true;
    }

    /// <summary>Конфигурация траектории в доле пути u (0..1) — по числу сэмплов.</summary>
    private static double[] SampleAt(PlannedTrajectory t, double u)
    {
        if (t == null || t.Path == null || t.Path.Length == 0) return null;
        int idx = Mathf.Clamp(Mathf.RoundToInt((float)u * (t.Path.Length - 1)), 0, t.Path.Length - 1);
        return t.Path[idx];
    }

    private void RebuildCandidateVisuals()
    {
        HideTrajectories();
        var plans = new List<PlannedTrajectory>(plannedSoFar);
        // Сортировка по критериям (длина, время, кривизна, зазор, запас лимитов, стоимость позы)
        // выполняется планировщиком в `Score`; здесь только порядок показа.
        // Показываются ВСЕ найденные варианты (до 8) — не «топ-N».
        plans.Sort((a, b) => a.Score.CompareTo(b.Score));
        int want = Mathf.Clamp(trajectoryCount, 1, MaxTrajectories);
        if (plans.Count > want) plans.RemoveRange(want, plans.Count - want);

        int total = plans.Count;
        int id = 0;
        foreach (PlannedTrajectory t in plans)
        {
            int rank = id;                      // 0 — лучшая по score
            id++;
            var cand = new TrajectoryCandidate
            {
                id = rank,
                color = VariantColor(rank, total),
                label = VariantLabel(rank, t),
                plan = t,
                timeS = (float)t.Time,
                minClearance = t.MinClearance,
                limitMarginDeg = t.LimitMargin,
                score = t.Score,
                safe = t.MinClearance >= gate.minClearance && t.LimitMargin >= gate.minLimitMarginDeg
            };
            if (!cand.safe) cand.why = "запас ниже порога";

            int n = t.Path.Length;
            int stride = Mathf.Max(1, n / 70);
            var pts = new List<Vector3>();
            for (int i = 0; i < n; i += stride) pts.Add(validator.TcpAt(t.Path[i]));
            if (pts.Count < 2) continue;
            cand.tube = SpreadPath(pts.ToArray(), rank, tubeSpreadStep);
            cand.lengthM = TubeMath.PolylineLength(cand.tube);
            if (slowMotionEnabled) MotionTiming.RescaleToSpeed(t, cand.lengthM, robotMoveSpeed);
            cand.timeS = (float)t.Time;   // после пересчёта времени (иначе в подсказке сырое значение)

            GameObject go = new GameObject(cand.label);
            go.hideFlags = HideFlags.HideInHierarchy;   // служебная «колбаска» — не в иерархии
            go.transform.SetParent(transform, false);
            cand.view = go.AddComponent<TrajectoryTube>();
            cand.view.radius = tubeRadius;
            // Подсветка/выбор — светлее своего оттенка (8 «колбасок» в одной оранжево-жёлтой
            // гамме должны читаться, а выбранная — отличаться от остальных).
            cand.view.hoverColor = Color.Lerp(cand.color, Color.white, 0.45f);
            cand.view.selectedColor = Color.Lerp(cand.color, Color.white, 0.80f);
            cand.view.Build(cand.tube, cand.color);
            state.candidates.Add(cand);
        }
        _ = planStartTime;

        // ЗАДАЧА 5 ТЗ: панель метрик получает ВСЕ варианты (строка на каждую траекторию,
        // цвет строки = цвет «колбаски» на сцене) — обновляется при каждой генерации.
        if (metricsPanel != null) metricsPanel.SetTrajectories(state.candidates, state.selectedTrajectory);
    }

    /// <summary>
    /// Цвет траектории (ЗАДАЧА 1 ТЗ): 8 РАЗНЫХ оттенков в оранжево-жёлтой гамме —
    /// тон от 20° (оранжевый) к 56° (жёлтый), плюс чередование светлоты, иначе соседние
    /// «колбаски» одного тона сливались бы в одно пятно.
    /// </summary>
    private static Color VariantColor(int index, int total)
    {
        float k = total > 1 ? Mathf.Clamp01((float)index / (total - 1)) : 0f;
        float hue = Mathf.Lerp(0.055f, 0.155f, k);            // 20°…56°: оранжевый → жёлтый
        float sat = Mathf.Lerp(1.00f, 0.80f, k);
        float val = (index % 2 == 0) ? 1.00f : 0.82f;         // чередование светлоты
        return Color.HSVToRGB(Mathf.Repeat(hue, 1f), sat, val);
    }

    /// <summary>Подпись траектории: номер, «лучшая» и метка источника (ветвь IK или seed).</summary>
    private static string VariantLabel(int rank, PlannedTrajectory t)
    {
        string tag = t != null && !string.IsNullOrEmpty(t.BranchTag) ? " [" + t.BranchTag + "]" : "";
        return rank == 0
            ? "Траектория 1 · лучшая" + tag
            : "Траектория " + (rank + 1) + " · вариант" + tag;
    }

    /// <summary>
    /// Разводит «колбаски» вариантов в стороны. Все конфигурации IK/seed'ы дают практически
    /// один и тот же путь TCP, поэтому без смещения 8 вариантов сливались бы в одну линию
    /// и казалось, что альтернатив нет. Смещение — только визуал/выбор (до 14 см);
    /// лучшая траектория рисуется точно по пути, а фантом всегда идёт по ИСТИННОМУ пути.
    /// </summary>
    private static Vector3[] SpreadPath(Vector3[] path, int rank, float step)
    {
        if (path == null || rank <= 0) return path;
        float mag = Mathf.Max(0.005f, step) * Mathf.CeilToInt(rank * 0.5f);
        float sign = (rank % 2 == 1) ? 1f : -1f;
        var res = new Vector3[path.Length];
        for (int i = 0; i < path.Length; i++)
            res[i] = path[i] + Vector3.right * (mag * sign);
        return res;
    }

    // ------------------------------------------------------------------ наведение и выбор

    private void UpdateTrajectoryHover(bool greenOn)
    {
        int best = -1;
        float bestDist = tubeRadius;
        for (int i = 0; i < state.candidates.Count; i++)
        {
            TrajectoryCandidate c = state.candidates[i];
            if (c.view == null) continue;
            float d = c.view.DistanceToRay(lasers.GreenRay);
            if (d < bestDist) { bestDist = d; best = i; }
        }
        state.hoveredTrajectory = best;
        for (int i = 0; i < state.candidates.Count; i++)
            if (state.candidates[i].view != null)
                state.candidates[i].view.SetHighlight(i == best && best >= 0,
                    i == state.selectedTrajectory && state.selectedTrajectory >= 0);

        if (best >= 0 && greenOn) ShowTrajectoryMetrics(state.candidates[best]);
    }

    private void UpdatePhantomHover(Vector3 aimPoint, bool greenOn)
    {
        int hover = phantoms.HoverIndex(lasers.GreenRay, aimPoint);
        state.hoveredPhantom = hover;
        phantoms.SetHighlight(hover);
        if (hover >= 0 && greenOn) ShowPhantomMetrics(hover);
    }

    private void ShowTrajectoryMetrics(TrajectoryCandidate c)
    {
        string curv = c.plan != null ? " · кривизна " + c.plan.Curvature.ToString("0.0") + "°/сэмпл" : "";
        string txt = string.Format("{0}: длина {1:F2} юнита · время {2:F0} с · зазор {3:F0} мм · запас лимитов {4:F1}°{5} · оценка {6:F2}",
            c.label, c.lengthM, c.timeS, c.minClearance * 1000f, c.limitMarginDeg, curv, c.score);
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus(txt, Palette.Info);
        KazistovVvUI.KazistovVvUIManager.SetTooltip(txt, c.tube != null && c.tube.Length > 0 ? c.tube[c.tube.Length / 2] : state.point);
    }

    private void ShowPhantomMetrics(int index)
    {
        var list = phantoms.Configs;
        if (index < 0 || index >= list.Count) return;
        // Фантомы идут по своим траекториям: показываем, какой это фантом и где он сейчас.
        int pct = Mathf.RoundToInt(phantoms.ProgressOf(index) * 100f);
        string where = state.selectedTrajectory >= 0 && state.selectedTrajectory < state.candidates.Count
            ? state.candidates[state.selectedTrajectory].label : "траектория не выбрана";
        string txt = "Фантом " + (index + 1) + " из " + list.Count + " · " + where +
                     " · пройдено " + pct + "%" +
                     (pct >= 100 ? " · доехал (остановился)" : " · едет (скорость " +
                                   phantomMoveSpeed.ToString("0.000") + " ю/с = " +
                                   phantomSpeedMultiplier.ToString("0.##") + "× скорости робота)") +
                     " · зелёным лучом → ЛКМ (подтвердить движение робота, " +
                     FormatSpeed(robotMoveSpeed) + ")";
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus(txt, Palette.Info);
        KazistovVvUI.KazistovVvUIManager.SetTooltip(txt,
            list[index].ghost != null ? list[index].ghost.transform.position + Vector3.up * 1.1f : state.point);
    }

    /// <summary>
    /// Шаг 3/4 ТЗ: выбор траектории зелёным лучом.
    ///   * ПЕРВЫЙ выбор (фантомы всех траекторий ещё едут): фантомы остальных траекторий
    ///     УДАЛЯЮТСЯ, а фантом выбранной СОХРАНЯЕТСЯ вместе со своим прогрессом —
    ///     он продолжает движение (или уже доехал до конечной позы).
    ///   * ПЕРЕКЛЮЧЕНИЕ на другую траекторию: фантом этой траектории создаётся ЗАНОВО
    ///     и анимация запускается с начала (ТЗ: «фантом перезапускается, либо создаётся заново»).
    /// Остальные «колбаски» остаются в сцене, но приглушаются — на них можно навести
    /// зелёный лазер и переключиться (иначе шаг 4 был бы невыполним); полностью
    /// траектории исчезают на шаге 5, когда робот начинает движение.
    /// </summary>
    private void SelectTrajectory(int index)
    {
        if (index < 0 || index >= state.candidates.Count) return;
        TrajectoryCandidate cand = state.candidates[index];
        if (cand == null || cand.plan == null) return;

        bool first = state.selectedTrajectory < 0;
        state.selectedTrajectory = index;
        // Выравнивание «пятака» (ШАГ 3, по умолчанию выключено) уже выполнено для ВСЕХ
        // вариантов при их создании (AlignAllCandidates) — здесь поза не меняется, иначе
        // фантом, созданный до правки пути, разошёлся бы с реальной конечной позой.

        phantoms.Init(robot, validator);
        bool ok;
        if (first)
        {
            // Продолжаем уже едущий фантом этой траектории (без перезапуска анимации).
            int phantomIndex = PhantomIndexOfCandidate(index);
            ok = phantomIndex >= 0 && phantoms.KeepOnly(phantomIndex);
            if (!ok) ok = phantoms.ShowAlongPath(cand.plan, validator.CopyCurrent(), phantomMoveSpeed);
        }
        else
        {
            // Переключение: новый фантом и новая анимация с текущей позы робота.
            ok = phantoms.ShowAlongPath(cand.plan, validator.CopyCurrent(), phantomMoveSpeed);
        }

        if (!ok)
        {
            state.selectedTrajectory = first ? -1 : state.selectedTrajectory;
            Report("Не удалось построить фантом по этой траектории — выберите другую", Palette.Bad);
            return;
        }

        BuildPhantomOwner();                            // после KeepOnly/ShowAlongPath — один фантом
        SyncPhantomState();
        state.selectedPhantom = phantoms.Count > 0 ? 0 : -1;
        state.hoveredPhantom = -1;
        ApplySelectionVisuals();

        state.phase = FlowState.PhantomsMoving;
        if (first)
        {
            Report("Траектория выбрана · «" + cand.label + "» · фантомы остальных траекторий убраны" +
                   " · фантом продолжает движение" +
                   " · зелёным лучом наведите на фантом и нажмите ЛКМ", Palette.Info);
        }
        else
        {
            Report("Переключено на «" + cand.label + "» · анимация фантома запущена заново" +
                   " · зелёным лучом наведите на фантом и нажмите ЛКМ", Palette.Info);
        }
        Debug.Log("[Flow] выбрана " + cand.label + " (индекс " + index + ", " +
                  (first ? "первый выбор" : "переключение") + ") · фантомов " + phantoms.Count +
                  " · прогресс " + (phantoms.Count > 0 ? phantoms.ProgressOf(0).ToString("F2") : "-"));
    }

    /// <summary>Приглушить все траектории, кроме выбранной (шаг 3 ТЗ).</summary>
    private void ApplySelectionVisuals()
    {
        bool anySelected = state.selectedTrajectory >= 0;
        for (int i = 0; i < state.candidates.Count; i++)
        {
            TrajectoryCandidate c = state.candidates[i];
            if (c == null || c.view == null) continue;
            c.view.SetDimmed(anySelected && i != state.selectedTrajectory);
            c.view.SetHighlight(i == state.hoveredTrajectory, i == state.selectedTrajectory);
        }
        // ЗАДАЧА 5 ТЗ: строка выбранной траектории подсвечивается на панели метрик.
        if (metricsPanel != null) metricsPanel.SetSelected(state.selectedTrajectory);
    }

    /// <summary>Индекс фантома, построенного по траектории-кандидату (шаг 2 ТЗ).</summary>
    private int PhantomIndexOfCandidate(int candidateIndex)
    {
        for (int i = 0; i < phantomOwner.Count; i++)
            if (phantomOwner[i] == candidateIndex) return i;
        return -1;
    }

    /// <summary>После операций с фантомами: соответствие «фантом → траектория» (обычно 1:1).</summary>
    private void BuildPhantomOwner()
    {
        phantomOwner.Clear();
        if (phantoms.Count == 1)
        {
            phantomOwner.Add(state.selectedTrajectory >= 0 ? state.selectedTrajectory : 0);
            return;
        }
        for (int i = 0; i < phantoms.Count; i++) phantomOwner.Add(i);
    }

    /// <summary>
    /// ШАГ 3 ТЗ (выравнивание «пятака», по умолчанию ВЫКЛЮЧЕНО через `alignToolToSurface`):
    /// прогоняется по ВСЕМ вариантам ДО создания фантомов. Иначе фантом, построенный по
    /// неизменённому пути, показывал бы не ту позу, в которую приедет реальный робот.
    /// </summary>
    private void AlignAllCandidates()
    {
        if (!alignToolToSurface) return;
        for (int i = 0; i < state.candidates.Count; i++)
            AlignToolToSurface(state.candidates[i]);
    }

    /// <summary>
    /// Выравнивает концевую плоскость инструмента по плоскости поверхности: ось вращения фланца
    /// (нормаль «пятака») приводится к нормали поверхности — «пятак» встаёт ПАРАЛЛЕЛЬНО столу/полу/стене.
    /// Правится ТОЛЬКО последняя поза выбранной траектории (её же возьмёт фантом и реальный робот).
    /// Точка в воздухе — ориентация свободная (ТЗ), выравнивание не выполняется.
    /// Любая неудача проверок (позиция/лимиты/зазор/подход) → поза остаётся прежней.
    /// </summary>
    private void AlignToolToSurface(TrajectoryCandidate cand)
    {
        if (!alignToolToSurface || cand == null || cand.plan == null) return;
        if (cand.plan.Path == null || cand.plan.Path.Length < 2) return;
        if (!lockedOnSurface) return;              // точка в свободном пространстве — не трогаем

        double[] goal = cand.plan.GoalQ;
        if (goal == null) return;

        var outcome = ToolAlign.AlignGoal(validator, world, goal, state.point, surfaceNormalLocked,
            gate.minClearance, 0.015f, gate.minLimitMarginDeg, 0.008f, alignAngleToleranceDeg);

        if (!outcome.ok)
        {
            Debug.Log("[Flow] «пятак» не выровнен (" + outcome.why +
                      ") — ориентация осталась свободной");
            return;
        }
        if (!ApproachClear(cand.plan, outcome.q, out float minClearance))
        {
            Debug.Log("[Flow] «пятак» выровнен, но подход к позе не прошёл проверки — " +
                      "ориентация осталась свободной");
            return;
        }

        cand.plan.Path[cand.plan.Path.Length - 1] = outcome.q;
        cand.plan.MinClearance = Mathf.Min(cand.plan.MinClearance, minClearance);
        Debug.Log(string.Format(
            "[Flow] «пятак» выровнен по нормали поверхности ({0}) · остаток {1:F1}° · TCP {2:F0} мм от цели",
            outcome.intoSurface ? "инструмент в поверхность" : "ось развёрнута", outcome.angleDeg,
            outcome.posErrM * 1000f));
    }

    /// <summary>
    /// Проверка подхода к НОВОЙ конечной позе: участок траектории перед целью сэмплируется и
    /// проверяется на лимиты (с запасом), зазор до мира и до своих звеньев.
    /// </summary>
    private bool ApproachClear(PlannedTrajectory plan, double[] qGoal, out float minClearance)
    {
        minClearance = float.MaxValue;
        int last = plan.Path.Length - 1;
        int from = Mathf.Max(0, last - 8);            // последние ~8 сэмплов пути
        const int steps = 12;
        for (int s = 0; s <= steps; s++)
        {
            double[] q = LerpConfig(plan.Path[from], qGoal, (double)s / steps);
            if (!validator.WithinLimits(q)) return false;
            if (validator.LimitMargin(q) < gate.minLimitMarginDeg) return false;
            float self = validator.SelfClearance(q, out _, out _);
            if (self < 0.01f) return false;
            float c = validator.ClearanceAt(q, world, out _, out _);
            if (c < gate.minClearance) return false;
            minClearance = Mathf.Min(minClearance, Mathf.Min(c, self));
        }
        return true;
    }

    /// <summary>Промежуточная конфигурация по кратчайшим доворотам (как Interp в планировщике).</summary>
    private double[] LerpConfig(double[] a, double[] b, double k)
    {
        int n = Mathf.Min(validator.Dof, Mathf.Min(a.Length, b.Length));
        var q = new double[Mathf.Max(1, n)];
        for (int i = 0; i < n; i++)
            q[i] = validator.IsPrismatic(i) ? a[i] + (b[i] - a[i]) * k
                                            : a[i] + Mathf.DeltaAngle((float)a[i], (float)b[i]) * k;
        return q;
    }

    /// <summary>
    /// ШАГ 2 ТЗ: по КАЖДОЙ сгенерированной траектории запускается СВОЙ фантом (8 траекторий —
    /// 8 фантомов). Все стартуют одновременно из текущей позы робота и едут каждый по своему
    /// пути со скоростью `phantomMoveSpeed` (старая скорость, независима от скорости робота).
    /// </summary>
    private bool BuildAllPhantoms()
    {
        if (state.candidates.Count == 0) return false;
        phantoms.Init(robot, validator);

        int want = Mathf.Clamp(phantomCount, 1, Mathf.Min(state.candidates.Count, MaxTrajectories));
        var plans = new List<PlannedTrajectory>();
        phantomOwner.Clear();
        for (int i = 0; i < want; i++)
        {
            TrajectoryCandidate c = state.candidates[i];
            if (c == null || c.plan == null || c.plan.Path == null || c.plan.Path.Length < 2) continue;
            plans.Add(c.plan);
            phantomOwner.Add(i);                     // индекс фантома → индекс траектории
        }
        if (plans.Count == 0) return false;

        // Оттенок фантома — свой на каждый (бирюзовая гамма, различимость конфигураций);
        // индекс оттенка совпадает с индексом траектории, поэтому «Фантом N» ↔ «Траектория N».
        // Настройка «Показ фантомов» (панель «Настройки → Функции»): при выключенном
        // показе копии не создаются вовсе — раньше пункт настройки не имел обработчика.
        if (!KazistovVvUI.KvSettings.ShowPhantoms)
        {
            phantoms.Hide();
            Debug.Log("[Flow] Показ фантомов выключен в настройках — копии не создаются");
        }
        else if (!phantoms.ShowAllAlongPaths(plans, validator.CopyCurrent(), phantomMoveSpeed))
        {
            phantomOwner.Clear();
            return false;
        }

        // Если какая-то копия не создалась, соответствие уточняем по факту (обычно 1:1).
        if (phantoms.Count != phantomOwner.Count)
        {
            phantomOwner.Clear();
            for (int i = 0; i < phantoms.Count; i++) phantomOwner.Add(i);
        }

        SyncPhantomState();
        state.selectedPhantom = -1;
        state.hoveredPhantom = -1;
        Debug.Log("[Flow] Фантомов: " + phantoms.Count + " (по одному на каждую из " +
                  state.candidates.Count + " траекторий) · скорость " + phantomMoveSpeed.ToString("0.000") +
                  " ю/с = " + phantomSpeedMultiplier.ToString("0.##") + "× robotMoveSpeed (" +
                  robotMoveSpeed.ToString("0.0000") + " ю/с) · старт из позы робота");
        return true;
    }

    /// <summary>Синхронизировать список фантомов в состоянии (для подсказок/UI).</summary>
    private void SyncPhantomState()
    {
        state.phantoms.Clear();
        foreach (PhantomConfig pc in phantoms.Configs) state.phantoms.Add(pc);
    }

    /// <summary>
    /// Добирает конфигурации-фантомы до want: зеркалим уже найденную позу
    /// (плечо / локоть / запястье) и доводим CCD до ТОЙ ЖЕ точки TCP (допуск 6 мм).
    /// Так фантомов стабильно 2–8 (ТЗ), а не 2–3. SCARA не трогаем — у неё своя аналитика.
    /// </summary>
    private List<IkSolution> TopUpVariants(List<IkSolution> source, double[] goalQ,
                                           int want, double[] ranges)
    {
        var res = new List<IkSolution>(source);
        if (goalQ == null || validator.Dof < 6 || goalQ.Length < 6) return res;

        for (int k = 0; k < 8 && res.Count < want; k++)
        {
            var seed = (double[])goalQ.Clone();
            var tag = new IkBranchTag();
            switch (k)
            {
                case 0: seed[0] += 180; tag.shoulderFar = true; break;
                case 1: seed[1] = -seed[1]; tag.elbowDown = true; break;
                case 2: seed[2] = -seed[2]; tag.elbowDown = true; break;
                case 3: seed[3] += 180; seed[4] = -seed[4]; seed[5] += 180; tag.wristFlip = true; break;
                case 4: seed[0] += 180; seed[1] = -seed[1]; tag.shoulderFar = tag.elbowDown = true; break;
                case 5: seed[2] = -seed[2]; seed[4] = -seed[4]; tag.elbowDown = tag.wristFlip = true; break;
                case 6: seed[0] += 180; seed[2] = -seed[2]; tag.shoulderFar = tag.elbowDown = true; break;
                case 7: seed[1] = -seed[1]; seed[4] = -seed[4]; tag.elbowDown = tag.wristFlip = true; break;
            }
            if (!validator.WithinLimits(seed)) continue;
            if (!validator.SolveIk(state.point, seed, out double[] q, 120, 0.004f)) continue;
            if (q == null || q.Length < validator.Dof || !validator.WithinLimits(q)) continue;

            float err = Vector3.Distance(validator.TcpAt(q), state.point);
            if (err > 0.006f) continue;                 // та же точка TCP (≤6 мм)

            bool dup = false;
            foreach (IkSolution ex in res)
                if (PhantomMath.ConfigDistance(ex.q, q, ranges) < 0.05) { dup = true; break; }
            if (dup) continue;

            res.Add(new IkSolution
            {
                q = q,
                tag = tag,
                fkError = err,
                withinLimits = true,
                nearSingularity = false
            });
        }
        return res;
    }

    /// <summary>
    /// ШАГ 5 ТЗ: подтверждение фантома — реальный робот едет по ВЫБРАННОЙ траектории
    /// со скоростью `robotMoveSpeed` (ускорена вдвое). Траектории при этом ИСЧЕЗАЮТ
    /// (визуал «колбасок» убирается), фантом остаётся видимым как маркер конечной позы
    /// и убирается по завершении движения (FinishMotion).
    /// </summary>
    private void SelectPhantom(int index)
    {
        if (index < 0 || index >= state.phantoms.Count) return;
        if (index >= phantoms.Count) return;
        if (phantomSelectionLatch) return;
        if (state.selectedTrajectory < 0 || state.selectedTrajectory >= state.candidates.Count) return;
        phantomSelectionLatch = true;
        state.selectedPhantom = index;

        if (executor == null)
        {
            executor = robot.GetComponent<TrajectoryExecutor>();
            if (executor == null) executor = robot.gameObject.AddComponent<TrajectoryExecutor>();
        }
        executor.Init(validator, world, gate);
        executor.timeScale = 1f;
        motion.Bind(executor, validator, world, gate);
        motion.speedMps = robotMoveSpeed;              // СКОРОСТЬ РОБОТА (не фантома!)

        PhantomConfig cfg = phantoms.Configs[index];
        PlannedTrajectory plan = state.candidates[state.selectedTrajectory].plan;
        string where = state.candidates[state.selectedTrajectory].label;

        // Финальная поза = выбранный фантом; приводим её к непрерывному виду,
        // иначе последний шаг исполнителя «провернёт» сустав на полный оборот.
        double[] lastQ = validator.ContinueFrom(
            plan.Path.Length > 1 ? plan.Path[plan.Path.Length - 2] : null, cfg.q);
        plan.Path[plan.Path.Length - 1] = lastQ;

        if (motion.Play(plan, lastQ))
        {
            state.phase = FlowState.RobotMoving;
            robot.enabled = false;
            HideTrajectoryVisuals();                   // ШАГ 5: траектории исчезают

            // ДИАГНОСТИКА ПУСКА (02.11.2026, пункт 4 владельца: «не все траектории корректно
            // включаются»). В журнале шлема не было НИ ОДНОЙ строки про пуск траектории —
            // поэтому по логам нельзя было понять, какая включилась, а какая нет.
            // Здесь пишем: какая именно, сколько шагов в плане и скорость.
            Debug.Log("[Flow] ПУСК траектории «" + where + "» · шагов " + plan.Path.Length +
                      " · индекс " + state.selectedTrajectory + " из " + state.candidates.Count +
                      " · скорость " + robotMoveSpeed.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture));

            Report("Движение робота по «" + where + "» · скорость " +
                   FormatSpeed(robotMoveSpeed) + " · фантом остаётся видимым до конца движения",
                   Palette.Ok);
        }
        else
        {
            phantomSelectionLatch = false;

            // ДИАГНОСТИКА ОТКАЗА (пункт 4): почему траектория НЕ включилась.
            Debug.Log("[Flow] ОТКАЗ пуска траектории «" + where + "» · причина: " +
                      SafetyGate.Describe(gate.LastReason));

            Report("Safety отклонила движение: " + SafetyGate.Describe(gate.LastReason), Palette.Bad);
        }
    }

    /// <summary>Человекочитаемая скорость: «1 юнит за 15.0 с» (для подсказок).</summary>
    private static string FormatSpeed(float unitsPerSec)
    {
        float s = Mathf.Max(0.001f, unitsPerSec);
        return "1 юнит за " + (1f / s).ToString("0.#") + " с (" + s.ToString("0.000") + " ю/с)";
    }

    /// <summary>
    /// Убрать ВИЗУАЛ траекторий, оставив список кандидатов (шаг 5 ТЗ: «траектории исчезают»).
    /// Список нужен дальше — по нему считается подпись и берётся план для исполнителя.
    /// </summary>
    private void HideTrajectoryVisuals()
    {
        foreach (TrajectoryCandidate c in state.candidates)
        {
            if (c == null || c.view == null) continue;
            Object.Destroy(c.view.gameObject);
            c.view = null;
        }
        // ШАГ 5 ТЗ: траектории исчезли — строки панели метрик тоже убираем.
        if (metricsPanel != null) metricsPanel.Clear();
    }

    private void FinishMotion()
    {
        if (state.phase != FlowState.RobotMoving) return;
        if (robot != null) robot.enabled = true;

        // ДИАГНОСТИКА (02.11.2026, пункт 3 владельца: «начальный робот не пропадает»).
        // Считаем фантомов ДО и ПОСЛЕ уборки — так видно, убрались ли они на самом деле,
        // а не «должны были убраться». Проверка журнала шлема показала, что этих чисел
        // не было вовсе, и вопрос решался гаданием.
        int beforeHide = PhantomManager.CountPhantomsInScene();
        phantoms.Hide();
        int afterHide = PhantomManager.CountPhantomsInScene();
        if (PhantomManager.logLifecycle)
        {
            Debug.Log("[Flow] движение завершено · фантомов до уборки " + beforeHide +
                      " · после " + afterHide +
                      (afterHide > 0 ? "  ⚠ ОСТАЛИСЬ (это и видит оператор)" : "  (убраны)"));
        }

        HideTrajectories();
        if (pointHud != null) pointHud.SetVisible(false);
        generating = false;
        state.ClearAll();
        phantomSelectionLatch = false;
        Report("Готово. Задайте новую точку красным лазером", Palette.Ok);
    }

    // ------------------------------------------------------------------ служебное

    private ReachResult oracleQuery(Vector3 point)
    {
        return oracle.Ready ? oracle.Query(point)
                            : new ReachResult { verdict = ReachVerdict.Safe, reason = "оракул не готов" };
    }

    /// <summary>
    /// Диагностика отказа по точке (ТЗ шаг 1, п.4): пишет ТОЧНОЕ значение toolOffset и координаты,
    /// где оракул увидел пересечение со сценой / малый запас / недостижимость, — в консоль и
    /// (при `logOffsetDiagnostics`) в файл `_dsh_offset_diag.txt` в корне проекта.
    /// По этим числам видно, чего не хватает: увеличить offset, менять TCP (шаг 2) или ориентацию (шаг 3).
    /// </summary>
    private void LogOffsetRejection(Vector3 surfacePoint, Vector3 target, ReachResult verdict)
    {
        string line = string.Format(
            "[OffsetDiag] {0} · toolOffset={1:F3} · clearance={2:F0} мм · поверхность ({3:F3}, {4:F3}, {5:F3}) · " +
            "нормаль ({6:F3}, {7:F3}, {8:F3}) · цель TCP ({9:F3}, {10:F3}, {11:F3}) · режим {12}",
            verdict.reason, toolOffset, verdict.clearance * 1000f,
            surfacePoint.x, surfacePoint.y, surfacePoint.z,
            aimNormalNow.x, aimNormalNow.y, aimNormalNow.z,
            target.x, target.y, target.z,
            offsetHackUsed ? "WorldUpHack" : (aimOnSurfaceNow ? "SurfaceNormal" : "без смещения"));
        Debug.LogWarning(line);
        WriteOffsetDiag(line);
    }

    /// <summary>
    /// Диагностика второго «красного» случая: точка прошла оракул, но планировщик не нашёл ни одной
    /// траектории (обычно запас у поверхности меньше `Planner.clearance` = 0.02). Пишет те же данные
    /// плюс отчёт планировщика — по ним видно, виноват зазор, IK или лимиты.
    /// </summary>
    private void LogPlanFailure()
    {
        string line = string.Format(
            "[OffsetDiag] траектория не найдена · toolOffset={0:F3} · поверхность ({1:F3}, {2:F3}, {3:F3}) · " +
            "нормаль ({4:F3}, {5:F3}, {6:F3}) · цель TCP ({7:F3}, {8:F3}, {9:F3}) · IK: {10} | ветви: {11}",
            toolOffset,
            surfacePointLocked.x, surfacePointLocked.y, surfacePointLocked.z,
            surfaceNormalLocked.x, surfaceNormalLocked.y, surfaceNormalLocked.z,
            state.point.x, state.point.y, state.point.z,
            planner.LastDebug, planner.LastBranchInfo);
        Debug.LogWarning(line);
        WriteOffsetDiag(line);
    }

    /// <summary>
    /// Запись строки диагностики в файл `_dsh_offset_diag.txt` (не чаще раза в секунду).
    /// ФИКС 10: файл лежит в `Application.persistentDataPath/KazistovVv/Reports`, а НЕ в корне
    /// проекта — проект находится в OneDrive, и во время PlayMode файл там не дописывался
    /// (§13.11): диагностика отказа по точке терялась.
    /// </summary>
    private void WriteOffsetDiag(string line)
    {
        if (!logOffsetDiagnostics) return;
        if (Time.realtimeSinceStartup - lastOffsetDiagTime < 1f) return;
        lastOffsetDiagTime = Time.realtimeSinceStartup;
        try
        {
            string path = KazistovVvFeatures.FeatureStorage.ReportPath("_dsh_offset_diag.txt");
            System.IO.File.AppendAllText(path,
                System.DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss") + " " + line + System.Environment.NewLine);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[OffsetDiag] не удалось записать файл диагностики: " + e.Message);
        }
    }

    /// <summary>Пока не этап 4 — у всех роботов нет цели (движение только по подтверждению).</summary>
    private void HoldAllRobots()
    {
        RefreshRobotList();
        foreach (RobotController rc in allRobots)
            if (rc != null) rc.ClearTarget();
    }

    private void RefreshRobotList()
    {
        allRobots.Clear();
        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        foreach (RobotController rc in robots)
            if (rc != null) allRobots.Add(rc);
    }

    private void ResetAll(string why)
    {
        phantoms.Hide();
        HideTrajectories();
        if (pointHud != null) pointHud.SetVisible(false);
        planQueue.Clear();
        plannedSoFar.Clear();
        generating = false;
        // Отложенные на время режима перемещения варианты сбрасываем вместе со всем остальным:
        // иначе после Esc они «воскресли» бы при следующем входе/выходе из режима.
        moveSavedPlans.Clear();
        moveSavedQueue.Clear();
        moveSavedPhase = FlowState.Idle;
        executor?.Stop(SafetyReason.OperatorStop);
        if (robot != null) robot.enabled = true;
        state.ClearAll();
        phantomSelectionLatch = false;
        Report(why, Color.gray);
    }

    private void HideTrajectories()
    {
        foreach (TrajectoryCandidate c in state.candidates)
            if (c != null && c.view != null) Object.Destroy(c.view.gameObject);
        state.candidates.Clear();
        phantomOwner.Clear();
        if (metricsPanel != null) metricsPanel.Clear();   // строки панели метрик (ЗАДАЧА 5)
    }

    private void Rebind()
    {
        if (robot == null) return;
        validator.Init(robot);
        validator.linkRadius = 0.06f;
        ik.Init(validator);
        posture.Init(validator);
        world.Rebuild(robot, 0.06f);
        planner.Init(validator, world);
        planner.clearance = 0.02f;
        planner.maxIterations = 700;
        oracle.Init(robot, world);
        gate.NotifyState();
        // ФИКС 4 (§22): при ПЕРЕПРИВЯЗКЕ робота (Rebind — смена робота прицелом/деревом)
        // фантомы СТАРОГО робота обязаны исчезнуть. Раньше здесь стоял только Init(),
        // который лишь перенастраивает менеджер (template/validator/контейнер) и НЕ трогает
        // уже созданные копии, — 8 фантомов прежнего робота оставались висеть в сцене.
        // Логика фантомов не менялась: это тот же штатный ClearPhantoms (полная уборка,
        // тот же вызов, что и при Esc / новой точке).
        phantoms.ClearPhantoms();
        phantoms.Init(robot, validator);
        robot.ClearTarget();
        tcpFrameDiagPending = logTcpFrame;      // разовая диагностика осей TCP после привязки
        // ФИКС 4: дерево RRT принадлежало ПРЕЖНЕМУ роботу — при перепривязке оно
        // ОЧИЩАЕТСЯ и версия сбрасывается в 0, иначе оператор видел бы «чужое» дерево
        // (или пустой экран без объяснения). Планировщик при этом не менялся.
        planner.ClearTreeRecording();
        // ЗАДАЧИ 3–4 ТЗ: зона достижимости строится по обоим роботам сцены, индикаторы
        // лимитов — по активному; при смене робота индикаторы пересобираются.
        if (viz != null)
        {
            viz.oracleZoneRadius = oracle.workZoneRadius;
            viz.Bind(robot);
        }
    }

    /// <summary>
    /// ФИКС 5/6: «Пересчитать кинематику SCARA» — явный запрос оператора. Заново снимает
    /// геометрию робота (пределы хода призмы, сдвиг нижней границы) и перепривязывает поток
    /// к тому же роботу. Вызывается ТОЛЬКО командой: сам по себе пересчёт не запускается
    /// (это тяжёлая операция, и пределы хода не должны «плавать» между кадрами).
    /// </summary>
    public void RebindKinematics(string why = "пересчёт кинематики")
    {
        if (robot == null) return;
        SCARAController scara = robot as SCARAController;
        if (scara != null) scara.RecalculateKinematics();
        Rebind();
        Debug.Log("[SCARA] " + why + ": геометрия и ход Z пересчитаны · ZMin = " +
                  (scara != null ? scara.ZMin.ToString("0.000") : "—") + " · ZMax = " +
                  (scara != null ? scara.ZMax.ToString("0.000") : "—"));
    }

    /// <summary>
    /// Активный робот потока. При старте НИ ОДИН робот не выбран (isActive = false),
    /// поэтому робота определяем сами, детерминированно:
    ///   1) явный выбор оператора (F / дерево KazistovVv);
    ///   2) пока идёт сценарий (фаза ≠ Idle) — прежняя привязка (без «прыжков» между стендами);
    ///   3) иначе — ближайший к точке прицела робот (подсвеченный имеет приоритет).
    /// Так этапы 0–4 работают и без предварительного выбора робота.
    /// </summary>
    private RobotController ResolveSelectedRobot(Vector3 aimPoint)
    {
        RobotController cameraChoice = camSelector != null ? camSelector.SelectedRobot : null;
        if (cameraChoice != null) return cameraChoice;

        if (robot != null && state.phase != FlowState.Idle) return robot;

        RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
        RobotController best = null;
        float bestScore = float.MaxValue;
        foreach (RobotController rc in robots)
        {
            if (rc == null) continue;
            if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue; // фантомы
            // Подсвеченный (выбранный в дереве/по F) робот «весит» вдвое ближе.
            float score = Vector3.Distance(rc.transform.position, aimPoint) * (rc.isActive ? 0.5f : 1f);
            if (score < bestScore) { bestScore = score; best = rc; }
        }

        // Гистерезис: если прежний робот не сильно дальше, привязку не меняем —
        // иначе на границе стендов робот «мигал» бы каждый кадр.
        if (robot != null && best != robot && best != null)
        {
            float cur = Vector3.Distance(robot.transform.position, aimPoint) * (robot.isActive ? 0.5f : 1f);
            if (cur - bestScore < 0.5f) return robot;
        }
        return best;
    }

    private static void Report(string text, Color color)
    {
        KazistovVvUI.KazistovVvUIManager.SetPlanStatus(text, color);
        Debug.Log("[Flow] " + text);
    }

    /// <summary>Палитра подсказок и шарика (ядовитые неоновые цвета).</summary>
    public static class Palette
    {
        public static readonly Color Ok = new Color(0.55f, 1f, 0.05f);      // кислотно-зелёный
        public static readonly Color Info = new Color(0.35f, 1f, 0.75f);    // неоновый циан
        public static readonly Color Warn = new Color(1f, 0.72f, 0f);       // ядовито-оранжевый
        public static readonly Color Bad = new Color(1f, 0.05f, 0.45f);     // неоновый маджента-красный
    }
}
