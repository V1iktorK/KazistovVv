using UnityEngine;
using TrajectoryCore;

namespace KazistovVvXr
{
    /// <summary>
    /// ПОТОК ЭТАПОВ В ШЛЕМЕ (§42, пункт 4 задания; решение владельца 25.10.2026 — «максимально глубоко»).
    ///
    /// НАЙДЕННАЯ ПРИЧИНА. В сцене шлема не было `TrajectoryFlowController`: он создаётся единственной
    /// строкой в десктопном `FreeFlyCameraController`, которого в `XrMain` нет. Все службы визуализаций
    /// устроены как `if (flow == null) return;` — значит ни одна не привязывалась, а тумблеры `view.*`
    /// молча ничего не делали. Подтверждение: в журналах приёмки нет ни одной строки `[Workspace]`,
    /// `[Singularity]`, `[Zone]`, `[Forces]`.
    ///
    /// ЧТО ДЕЛАЕТ ЭТОТ КОМПОНЕНТ. Создаёт на камере шлема ТОТ ЖЕ поток, что на компьютере, и кормит его
    /// публичным кадровым входом `UpdateAim(...)`: точка попадания луча, попадание, нормаль, подтверждение.
    /// Он сам создаёт зону достижимости, панель метрик, лазеры и фантомы, а хабы (`FeatureHub`,
    /// `KvStageHub*`) привязывают к нему зоны, сингулярности, силы и теплокарты — второй реализации нет.
    ///
    /// ТРИ ПРАВИЛА:
    ///   1. ЛАЗЕРЫ — ЛУЧИ РУК, КАК НА КОМПЬЮТЕРЕ (пункт 2 владельца 02.11.2026). Прежнее правило
    ///      «лазеры потока выключены (`redLaserOn = false`, `greenLaserOn = false`)» снято: поток
    ///      не рисует линии сам (его `LaserManager` — это только геометрия для выбора), а рисовать
    ///      второй комплект лучей поверх принятых в шлеме нельзя — было бы четыре линии вместо двух.
    ///      Поэтому лазеры потока — это лучи рук: правая рука ЗЕЛЁНАЯ (выбор траектории/фантома),
    ///      левая КРАСНАЯ (новая точка). Курки включают их по одному: левый — красный, правый —
    ///      зелёный. И оба луча обрываются на первом попадании (`KvXrLaserRay`), а не идут сквозь.
    ///   2. ПОДТВЕРЖДЕНИЕ ТОЛЬКО ВНЕ ИНТЕРФЕЙСА (правило `KvXrFlowRules`, второго не заводится).
    ///   3. КАМЕРУ ПОТОК НЕ ДВИГАЕТ: `camSelector` в шлеме пуст, позу пишет только OpenXR.
    /// </summary>
    [DefaultExecutionOrder(-300)]
    [DisallowMultipleComponent]
    public class KvXrFlowBridge : MonoBehaviour
    {
        /// <summary>Ключ настройки «кормить поток прицелом».</summary>
        public const string PrefKeyFeedAim = "KazistovVv.XR.FlowAim";

        /// <summary>
        /// ПОРОГ КУРКА, С КОТОРОГО ЛАЗЕР СЧИТАЕТСЯ ВКЛЮЧЁННЫМ (пункт 2 владельца 02.11.2026).
        ///
        /// На компьютере лазеры — тумблеры Z/X. В шлеме отдельной кнопки нет, поэтому лазер горит,
        /// пока нажат курок своей руки: левый — КРАСНЫЙ (новая точка), правый — ЗЕЛЁНЫЙ (выбор
        /// траектории/фантома). Тот же порог используется для яркости луча в `KvXrLaserColors`,
        /// поэтому «какой лазер включён» видно и в шлеме, и в журнале.
        /// </summary>
        public const float LaserTriggerThreshold = 0.65f;

        /// <summary>Камера шлема (ставится ригом).</summary>
        public Camera headCamera;

        /// <summary>Кормить поток прицелом луча.</summary>
        public bool feedAim = true;

        /// <summary>Разрешать подтверждение точки курком вне интерфейса.</summary>
        public bool feedConfirm = true;

        /// <summary>Порог курка, с которого считается нажатие.</summary>
        public float triggerThreshold = LaserTriggerThreshold;

        /// <summary>Дальность прицела, метры.</summary>
        public float aimMaxDistanceMeters = 6f;

        /// <summary>Печатать строки о состоянии потока и зон.</summary>
        public bool logOnBind = true;

        private TrajectoryFlowController flow;
        private bool leftTriggerWasDown;
        private bool rightTriggerWasDown;
        private bool bindLogged;
        private float nextZoneLog;

        /// <summary>
        /// Запрос подтверждения кнопкой A (стандарт Quest/PICO, 27.10.2026). Ставится
        /// <c>KvXrActions.Confirm()</c>, съедается кадром ниже — ровно так же, как курок,
        /// включая арбитраж «не над интерфейсом».
        /// </summary>
        private static bool confirmRequested;

        /// <summary>Попросить подтверждение (кнопка A / эквивалент курка).</summary>
        public static void RequestConfirm() { confirmRequested = true; }

        /// <summary>Созданный поток (для проверок и журнала).</summary>
        public TrajectoryFlowController Flow { get { return flow; } }

        /// <summary>
        /// КРАСНЫЙ ЛАЗЕР ВКЛЮЧЁН В ЭТОМ КАДРЕ (новая точка, левый курок). Читается диагностикой и
        /// тестом: состояние лазеров больше не «выключено насовсем», как было до 02.11.2026.
        /// </summary>
        public bool RedLaserOn { get; private set; }

        /// <summary>ЗЕЛЁНЫЙ ЛАЗЕР ВКЛЮЧЁН В ЭТОМ КАДРЕ (выбор траектории/фантома, правый курок).</summary>
        public bool GreenLaserOn { get; private set; }

        /// <summary>Включает ли это значение курка лазер (правило одно на оба луча, проверяется тестом).</summary>
        public static bool LaserHeld(float triggerValue)
        {
            return triggerValue >= LaserTriggerThreshold;
        }

        /// <summary>
        /// ЛАЗЕР ЭТОЙ РУКИ ВКЛЮЧЁН? На компьютере Z — красный лазер левой руки, X — зелёный правой;
        /// в шлеме те же два лазера включаются курками. По этой проверке луч руки становится плотнее
        /// (`KvXrLaserColors.Ray(..., true)`), то есть цвет в шлеме говорит, каким лазером работает
        /// оператор — ровно то, что владелец назвал «лазеры не разноцветные как на ПК».
        /// </summary>
        public static bool HandLaserHeld(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Left
                ? LaserHeld(KvXrInputSource.LeftTrigger)
                : LaserHeld(KvXrInputSource.RightTrigger);
        }

        /// <summary>Активный мост (для кнопки B — сброс потока, эквивалент Esc).</summary>
        public static KvXrFlowBridge Active { get; private set; }

        void Awake()
        {
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Start()
        {
            EnsureFlow();
        }

        /// <summary>СОЗДАТЬ ПОТОК, ЕСЛИ ЕГО ЕЩЁ НЕТ (идемпотентно).</summary>
        public bool EnsureFlow()
        {
            if (flow != null) return true;

            Camera camera = headCamera != null ? headCamera : Camera.main;
            if (camera == null) return false;

            flow = camera.GetComponent<TrajectoryFlowController>();
            if (flow == null) flow = camera.gameObject.AddComponent<TrajectoryFlowController>();
            return flow != null;
        }

        void Update()
        {
            if (!feedAim) return;
            if (flow == null && !EnsureFlow()) return;

            Vector3 origin;
            Vector3 direction;
            if (!TryAimRay(out origin, out direction)) return;

            Vector3 aimPoint = origin + direction * aimMaxDistanceMeters;
            Vector3 normal = Vector3.up;
            bool aimHit = false;

            // ЛУЧ ПРИЦЕЛА УПИРАЕТСЯ В МИР, А НЕ ПРОХОДИТ СКВОЗЬ (пункт 2 владельца 02.11.2026).
            //
            // Маска задана ЯВНО. `DefaultRaycastLayers` и так исключает слой `IgnoreRaycast`, но
            // перечислять это руками полезно: модели контроллеров лежат именно там, и случайное
            // «попадание в собственную руку» на нуле метров молча обнулило бы весь прицел. Физическая
            // длина берётся ДО первого попадания (робот, стол, пол), а не фиксированные 6 м:
            // иначе `aimPoint` уезжал за стол, и поток «не видел» робота под лучом.
            int aimMask = ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer);
            RaycastHit hit;
            if (Physics.Raycast(origin, direction, out hit, aimMaxDistanceMeters, aimMask,
                    QueryTriggerInteraction.Ignore))
            {
                aimPoint = hit.point;
                normal = hit.normal;
                aimHit = true;
            }

            bool overInterface = IsOverInterface(origin, direction);

            // КУРКИ = ЛАЗЕРЫ (возврат назначения 29.10.2026, владелец: «кнопки под указательный
            // палец + лазеры у нас заняты под зелёный и красный лазер - их верни назад»).
            // Ровно как Z/X на компьютере: левый курок — красный лазер (новая точка),
            // правый курок — зелёный (выбор траектории/фантома).
            float leftTrigger = KvXrInputSource.LeftTrigger;
            float rightTrigger = KvXrInputSource.RightTrigger;
            bool leftDown = leftTrigger >= triggerThreshold;
            bool rightDown = rightTrigger >= triggerThreshold;
            bool leftFront = leftDown && !leftTriggerWasDown;
            bool rightFront = rightDown && !rightTriggerWasDown;
            leftTriggerWasDown = leftDown;
            rightTriggerWasDown = rightDown;

            // ЛАЗЕР ГОРИТ, ПОКА КУРОК НАЖАТ, а не один кадр (02.11.2026).
            //
            // ЧТО БЫЛО. Сюда передавались только ФРОНТЫ (`leftFront`/`rightFront`) — флаг был
            // истинным ровно один кадр. Для самого подтверждения этого хватало, но «лазер включён»
            // означало «один кадр из девяноста»: состояние лазера нельзя было ни показать, ни
            // проверить, и строка журнала честно писала «лазеры потока выключены».
            //
            // ЧТО СТАЛО. Передаётся настоящее состояние курков: пока держишь левый — красный лазер
            // (новая точка), пока правый — зелёный (выбор траектории/фантома). Подтверждение
            // по-прежнему идёт по фронту (`triggerFront`), поэтому одно нажатие не делает два
            // действия (§27) — это разные вещи и смешивать их нельзя.
            bool redOn = leftDown;       // красный лазер = левый курок
            bool greenOn = rightDown;    // зелёный лазер = правый курок

            // Кнопка A (стандарт Quest) — то же подтверждение, что курок лучевой руки.
            if (confirmRequested)
            {
                if (KvXrInputSource.RayHand == KvXrQuickHand.Left) redOn = true;
                else greenOn = true;
            }

            RedLaserOn = redOn;
            GreenLaserOn = greenOn;

            // Фронты считаются раздельно: одно нажатие = одно действие (§27).
            bool triggerFront = leftFront || rightFront || confirmRequested;
            bool triggerDown = leftDown || rightDown;
            bool confirm = feedConfirm && triggerFront &&
                           ShouldFeedConfirm(overInterface, triggerDown || confirmRequested);
            confirmRequested = false;

            // ЛАЗЕРЫ ПОТОКА БОЛЬШЕ НЕ «ВЫКЛЮЧЕНЫ» (пункт 2 владельца 02.11.2026).
            //
            // Поток сам линий не рисует (`LaserManager` — это геометрия для выбора, а не
            // `LineRenderer`). Видимые лазеры в шлеме — лучи рук: правая ЗЕЛЁНАЯ, левая КРАСНАЯ,
            // и оба обрываются на первом попадании (`KvXrLaserRay`). Поэтому «включить лазеры
            // потока» = передать потоку настоящее состояние курков и не рисовать второй комплект
            // линий поверх принятых: иначе в шлеме было бы четыре луча вместо двух.
            flow.UpdateAim(aimPoint, aimHit && !overInterface, confirm, false,
                           redOn, greenOn, normal, aimHit);

            LogBindOnce();
            LogZones();
        }

        /// <summary>Луч прицела: сначала цель контроллера, затем камера (у камеры — направление взгляда).</summary>
        private bool TryAimRay(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            Vector3 position;
            Quaternion rotation;
            if (KvXrInputSource.TryRayAimPose(out position, out rotation))
            {
                origin = position;
                direction = rotation * Vector3.forward;
                return true;
            }

            Camera camera = headCamera != null ? headCamera : Camera.main;
            if (camera == null) return false;

            origin = camera.transform.position;
            direction = camera.transform.forward;
            return true;
        }

        /// <summary>Луч над интерфейсом (панель у руки, меню, окна)?</summary>
        private static bool IsOverInterface(Vector3 origin, Vector3 direction)
        {
            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel panel = KvXrPanels.At(i);
                if (panel == null || !panel.isActiveAndEnabled) continue;

                float along;
                if (panel.TryHitPanelSurface(origin, direction, out along)) return true;
            }
            return false;
        }

        /// <summary>Одна строка о привязке робота (дальше — только по запросу).</summary>
        private void LogBindOnce()
        {
            if (!logOnBind || bindLogged) return;
            if (flow == null || flow.Robot == null) return;

            bindLogged = true;
            Debug.Log(Describe(true, flow.Robot.robotName, VisibleVisualizers()));
        }

        /// <summary>СТРОКА-ДИАГНОСТИКА ЗОН раз в секунду ([S2] №6): сколько видно и почему нет.</summary>
        private void LogZones()
        {
            if (!logOnBind) return;
            if (Time.unscaledTime < nextZoneLog) return;
            nextZoneLog = Time.unscaledTime + 1f;

            int visible = VisibleVisualizers();
            bool robotBound = flow != null && flow.Robot != null;
            string reason = KvXrVisuals.Reason(flow != null, robotBound, true, visible > 0, false);
            Debug.Log(KvXrVisuals.Describe(visible, reason));
        }

        /// <summary>
        /// СКОЛЬКО ВИЗУАЛИЗАЦИЙ РЕАЛЬНО ВИДНО (§42/Д21). Раньше считались только две (зона достижимости
        /// и сингулярности), поэтому строка «ЗОНЫ: видимо 2» вводила в заблуждение: теплокарта, зазоры,
        /// запретные зоны и силы в счёт не шли, хотя команды `view.heatmap`, `view.clearance`, `zone.*`
        /// и `forces.tab` выполнялись. Найдено разбором журнала 25.10.2026.
        /// Теперь считаются ВСЕ шесть — ровно те, о которых говорит задание [S1] №4.
        /// </summary>
        private int VisibleVisualizers()
        {
            int visible = 0;

            // 1. Зона достижимости (создаёт сам поток)
            if (flow != null && flow.Workspace != null && flow.Workspace.ZoneCount > 0) visible++;

            // Визуализаторы — обычные классы: экземпляры держат хабы, поэтому спрашиваем у них.
            KazistovVvFeatures.KvStageHub hub = Object.FindAnyObjectByType<KazistovVvFeatures.KvStageHub>();
            KazistovVvFeatures.FeatureHub features = KazistovVvFeatures.FeatureHub.Current;

            // 2. Сингулярности («пятна» у базы, локтя, плеча)
            if (hub != null && hub.Singularities != null &&
                hub.Singularities.Enabled && hub.Singularities.VisibleZones > 0) visible++;

            // 3. Тепловая карта достижимости
            if (features != null && features.Heatmap != null && features.Heatmap.IsVisible) visible++;

            // 4. Тепловая карта зазоров
            if (features != null && features.Clearance != null && features.Clearance.Visible) visible++;

            // 5. Запретные зоны («границы»): считаем существующие зоны, видимые на сцене
            if (features != null && features.Zones != null && features.Zones.Zones.Count > 0) visible++;

            return visible;
        }

        /// <summary>Курок подтверждает точку только вне интерфейса.</summary>
        public static bool ShouldFeedConfirm(bool overInterface, bool triggerDown)
        {
            return KvXrFlowRules.ShouldFeedConfirm(overInterface, triggerDown);
        }

        /// <summary>Кормить ли прицел: только когда луч не над интерфейсом.</summary>
        public static bool ShouldFeedAim(bool overInterface)
        {
            return KvXrFlowRules.ShouldFeedAim(overInterface);
        }

        /// <summary>Строка журнала: создан ли поток, какой робот привязан, сколько визуализаций видно.</summary>
        public static string Describe(bool flowCreated, string robotName, int visualizersVisible)
        {
            return KvXrFlowRules.Describe(flowCreated, robotName, visualizersVisible);
        }
    }
}
