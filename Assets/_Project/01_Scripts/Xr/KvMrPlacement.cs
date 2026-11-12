using UnityEngine;
using KazistovVvFeatures;
using KazistovVvRealWorld;

namespace KazistovVvXr
{
    /// <summary>
    /// «ПОСТАВИТЬ СЮДА»: ЛУЧ → РЕАЛЬНАЯ ПОВЕРХНОСТЬ → РОБОТ (создано 02.11.2026, сессия S45).
    ///
    /// ЧТО ДЕЛАЕТ. В MR показывает луч с маркером: зелёный — под лучом реальная горизонтальная
    /// поверхность (стол), красный — поверхности нет. По A («подтвердить», стандартная кнопка
    /// Quest/PICO) робот переносится на найденную точку и разворачивается лицом к оператору.
    ///
    /// ПОЧЕМУ КНОПКА A, А НЕ КУРОК. Курок в проекте уже несёт пять смыслов (иконка, лазер,
    /// подтверждение точки, перенос окна, колесо) — добавлять шестой значит получить «одно нажатие
    /// два действия» (дефект E-08). A — стандартное «подтвердить» в Quest/PICO, и подтверждение
    /// здесь по смыслу то же самое. Чтобы и это не задвоилось, перехват идёт в ОДНОМ месте —
    /// `KvXrActions.Confirm`: перехватили → обычное подтверждение не выполняется.
    ///
    /// ЧТО ЗНАЧИТ «ПОСТАВИТЬ». Позиция — точка попадания луча в реальную поверхность; поворот —
    /// лицом к оператору; масштаб — 1 (робот в проекте метрический, 1 юнит = 1 м, см. замер в
    /// `Docs/ОТЧЁТ_MR.md`). Между столом и роботом нет ни щели, ни провала: низ робота ставится
    /// ровно в точку поверхности.
    ///
    /// ЧЕСТНО ОБ АВТОПОСТАНОВКЕ. Пока оператор не подтвердил точку, робот ставится САМ на первую
    /// найденную реальную горизонтальную поверхность перед ним (иначе в MR было бы «пусто и
    /// непонятно, что делать»). Это тот же расчёт, что и по A; в журнале печатается источник:
    /// «авто» или «по кнопке A».
    ///
    /// ================== ГЛАВНОЕ ИСПРАВЛЕНИЕ 12.11.2026 (требование владельца) ==================
    ///
    /// СЛОВА ВЛАДЕЛЬЦА ДОСЛОВНО: «Меня не устраивает, что при первом запуске MR сразу запускается
    /// робот, когда он должен появиться после закрытия меню и должен появляться выбор установки
    /// робота на поверхность. После подтверждения робот остаётся на этом месте.»
    ///
    /// ЧТО БЫЛО. Отсчёт авто-постановки начинался в `Start()` — то есть СРАЗУ при входе в MR,
    /// независимо от меню. Через `autoPlaceDelaySeconds` (3 с) `TryAutoPlace` сам находил
    /// «подтверждённый стол», переносил на него робота и включал его в мире. Оператор ещё читал
    /// стартовое меню, а робот уже стоял в выбранном ПРОГРАММОЙ месте: выбора места не происходило
    /// вовсе — ровно то, на что он жалуется.
    ///
    /// ЧЕМ ПЛОХО. Порядок, который заказан владельцем: меню → ВЫБОР места робота → подтверждение, и
    /// только потом робот в мире. Авто-постановка этот порядок отменяла: место выбирала программа, а
    /// «подтверждение» подтверждало уже готовое.
    ///
    /// ЧТО СТАЛО. Правило вынесено в ЧИСТУЮ функцию <see cref="ShouldAutoPlace"/> и проверяется
    /// тестом без сцены и без шлема:
    ///   • пока меню открыто — авто-постановка не выполняется НИКОГДА, сколько бы времени ни прошло
    ///     (5 с, 60 с, 300 с — ответ один: «нет»);
    ///   • отсчёт таймера при открытом меню тоже НЕ идёт: опорное время сдвигается на каждом таком
    ///     кадре (см. `Update`), поэтому после закрытия меню робот не появляется мгновенно — он
    ///     ждёт свои секунды уже ПОСЛЕ выхода из меню;
    ///   • после подтверждения места робот закрепляется, и авто-постановка его больше не двигает.
    ///
    /// АНТИ-ВАКУУМ (что было бы, если меню вообще не открывалось). Ожидание включается ТОЛЬКО по
    /// факту «меню сейчас на виду». Запуск сразу в мире, подавленное меню на планшете
    /// (`KvXrStartMenu.SuppressAutoShow`) и «меню уже закрыто к созданию компонента» — все эти
    /// случаи ожиданием не блокируются: нет меню — нет и ожидания, авто-постановка работает как
    /// раньше. Это закреплено тестом `AutoPlace_WithoutMenu_IsNotBlockedForever`, чтобы правило не
    /// смогло однажды превратиться в «робот не появляется никогда».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvMrPlacement : MonoBehaviour
    {
        /// <summary>Дальность луча постановки, метры.</summary>
        public float maxDistance = 6f;

        /// <summary>Насколько «горизонтальной» должна быть поверхность, чтобы считаться столом.</summary>
        public float minUpNormalDot = 0.60f;

        /// <summary>
        /// Через сколько секунд ПОСЛЕ ЗАКРЫТИЯ МЕНЮ искать поверхность для автопостановки.
        ///
        /// ЧТО БЫЛО: «через сколько секунд после старта MR». Отсчёт шёл с запуска, поэтому робот
        /// появлялся сам, пока оператор ещё был в меню (жалоба владельца). ЧТО СТАЛО: отсчёт идёт от
        /// момента, когда меню ЗАКРЫЛОСЬ, а при открытом меню не идёт вовсе.
        /// </summary>
        public float autoPlaceDelaySeconds = 3f;

        /// <summary>Как часто повторять попытку автопостановки на реальную поверхность, секунды.</summary>
        public float autoPlaceRetrySeconds = 4f;

        /// <summary>
        /// Через сколько секунд, если реальных поверхностей нет, ставить робота на пол (запасной
        /// путь). 25 секунд, а не 12: меш комнаты на PICO появляется не мгновенно, и поспешный
        /// «пол вместо стола» выглядел бы как дефект постановки.
        /// </summary>
        public float fallbackFloorDelaySeconds = 25f;

        /// <summary>Камера оператора (нужна для направления «лицом к оператору» и запасного луча).</summary>
        public Camera headCamera;

        /// <summary>MR-комната: она знает, кого двигать.</summary>
        public KvXrMrRoom room;

        /// <summary>Текущий экземпляр (нужен перехвату кнопки A).</summary>
        public static KvMrPlacement Active { get; private set; }

        /// <summary>Робот поставлен хотя бы раз.</summary>
        public bool Placed { get; private set; }

        /// <summary>Сколько раз робота ставили (авто и по кнопке).</summary>
        public int PlacementCount { get; private set; }

        /// <summary>Подтверждал ли оператор точку кнопкой A (а не авто).</summary>
        public bool PlacedByOperator { get; private set; }

        /// <summary>
        /// РОБОТ СКРЫТ ДО ЗАКРЫТИЯ МЕНЮ (требование владельца 12.11.2026, пункт 1).
        ///
        /// ЧТО БЫЛО: признака не существовало вовсе — робот включался в мире сразу в
        /// `KvXrMrRoom.PrepareRobotForWorld` (`root.gameObject.SetActive(true)`), и постановка была
        /// не важна: объект уже был виден и стоял там, куда его поставила авто-постановка.
        /// ЧТО СТАЛО: пока меню открыто, признак равен `true`, и комната не включает робота; признак
        /// снимается в момент, когда начинается ВЫБОР МЕСТА (после выхода из меню или по команде
        /// «Переместить робота»). Признак публичный — его видно снаружи и он проверяется тестом.
        /// </summary>
        public bool RobotHiddenUntilMenuClosed { get; private set; }

        /// <summary>
        /// ВЫБОР МЕСТА РОБОТА ИДЁТ (после выхода из меню). Пока он идёт, авто-постановка молчит:
        /// место выбирает оператор, а не таймер.
        /// </summary>
        public bool AwaitingOperatorChoice { get; private set; }

        /// <summary>
        /// АВТО-ПОСТАНОВКА ЗАПРЕЩЕНА НАВСЕГДА. Ставится, когда оператор зафиксировал место
        /// (клавиша B) или когда место применено командой «Переместить робота»: после этого робота
        /// двигает только явная команда, а не таймер (пункты 4–5 владельца).
        /// </summary>
        public bool AutoPlaceLocked { get; private set; }

        /// <summary>Последняя точка постановки и нормаль поверхности — для журнала и отчёта.</summary>
        public Vector3 LastSurfacePoint { get; private set; }
        public Vector3 LastSurfaceNormal { get; private set; }
        public float LastYawDegrees { get; private set; }
        public string LastCause { get; private set; }

        /// <summary>Строка состояния одной строкой.</summary>
        public string Status
        {
            get
            {
                return "постановка: " + (Placed ? "робот поставлен (" + LastCause + ")" : "ждёт реальную поверхность") +
                       " · луч " + (hasHit ? (hitConfirmedTable ? "попадает в ПОДТВЕРЖДЁННЫЙ стол" :
                                              "попадает в поверхность, но стол не подтверждён") :
                                             "не попадает") +
                       " · по кнопке подтверждено " + (PlacedByOperator ? "да" : "нет") +
                       " · автопостановка " + (AutoPlaceLocked ? "ЗАПРЕЩЕНА (место зафиксировано)"
                                                 : RobotHiddenUntilMenuClosed ? "ЖДЁТ ЗАКРЫТИЯ МЕНЮ"
                                                 : AwaitingOperatorChoice ? "молчит — выбирает оператор"
                                                 : "включена");
            }
        }

        // ============================================================ ЧИСТЫЕ ПРАВИЛА (тесты без сцены)

        /// <summary>
        /// ВЫПОЛНЯТЬ ЛИ АВТО-ПОСТАНОВКУ ПРЯМО СЕЙЧАС — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без сцены).
        ///
        /// ЭТО ГЛАВНОЕ ПРАВИЛО ТРЕБОВАНИЯ ВЛАДЕЛЬЦА 12.11.2026: «при первом запуске MR [не] сразу
        /// запускается робот, когда он должен появиться после закрытия меню». Здесь одна строка
        /// правды, и её видно целиком:
        ///   1) меню ОТКРЫТО            → НЕТ. Ни через 5 секунд, ни через 5 минут: пока оператор в
        ///                                меню, робота в мире нет и место за него не выбирают;
        ///   2) место ЗАФИКСИРОВАНО     → НЕТ. После подтверждения робота двигает только явная
        ///                                команда «Переместить робота», а не таймер (пункт 5);
        ///   3) идёт ВЫБОР МЕСТА        → НЕТ. Иначе две руки тянут робота в разные стороны: таймер
        ///                                ставит на «подтверждённый стол», а режим ведёт за лучом;
        ///   4) время ещё не пришло     → НЕТ. Отсчёт идёт ОТ ЗАКРЫТИЯ МЕНЮ, а не от старта MR;
        ///   5) иначе                   → ДА.
        ///
        /// ПОЧЕМУ ПЕРВЫМ ИМЕННО МЕНЮ. Прежний код начинал отсчёт в `Start()` и про меню не знал
        /// вовсе — поэтому робот вставал на стол, пока оператор читал меню. Если бы проверка меню
        /// стояла последней, любое из верхних условий могло бы «разрешить» постановку в обход неё.
        /// </summary>
        public static bool ShouldAutoPlace(bool menuVisible, bool placeFixed, bool awaitingChoice,
            bool hiddenUntilMenuClosed, float secondsSinceMenuClosed, float delaySeconds)
        {
            if (menuVisible) return false;                 // 1. меню открыто — ждём, а не ставим
            if (placeFixed) return false;                  // 2. место зафиксировано — не трогаем
            if (awaitingChoice) return false;              // 3. выбирает оператор — молчим
            if (hiddenUntilMenuClosed) return false;       // 4. робот ещё скрыт — показывать нечего
            if (secondsSinceMenuClosed < delaySeconds) return false;   // 5. время ещё не пришло
            return true;
        }

        /// <summary>Настройка правила <see cref="ShouldAutoPlace"/> для ЭТОГО компонента.</summary>
        public bool CanAutoPlaceNow(bool menuVisible, float secondsSinceMenuClosed)
        {
            return ShouldAutoPlace(menuVisible, AutoPlaceLocked, AwaitingOperatorChoice,
                RobotHiddenUntilMenuClosed, secondsSinceMenuClosed, autoPlaceDelaySeconds);
        }

        /// <summary>
        /// ЖДАТЬ ЛИ ЗАКРЫТИЯ МЕНЮ, А НЕ ОТСЧИТЫВАТЬ ВРЕМЯ (чистая функция — «стоит ли сдвигать
        /// опорное время таймера на этом кадре»).
        ///
        /// ЧТО БЫЛО. Таймер шёл от `Start()`: три секунды после входа в MR — и авто-постановка
        /// срабатывала, даже если меню было открыто. «Меню открыто 5 минут» ничего не меняло.
        /// ЧЕМ ПЛОХО. Оператор не выбирал место — программа ставила робота сама, а требование
        /// владельца ровно обратное.
        /// ЧТО СТАЛО. Пока это правило истинно, опорное время сдвигается (см. `Update`), и отсчёт
        /// начинается заново ПОСЛЕ закрытия меню. Время, проведённое в меню, не засчитывается.
        /// </summary>
        public static bool WaitForMenuToClose(bool menuVisible, bool placeFixed)
        {
            return menuVisible && !placeFixed;
        }

        /// <summary>
        /// ПОКАЗЫВАТЬ ЛИ РОБОТА В МИРЕ (чистая функция, пункт 1 требования).
        ///
        /// ЧТО БЫЛО. Робот включался сразу в `KvXrMrRoom.PrepareRobotForWorld`: оператор ещё в меню,
        /// а робот уже стоит на столе. ЧЕМ ПЛОХО. «При первом запуске MR сразу запускается робот» —
        /// ровно слова владельца. ЧТО СТАЛО. Пока меню открыто, робот не показывается. Он появляется,
        /// когда оператор вышел из меню и начал выбирать место, и остаётся после подтверждения.
        /// </summary>
        public static bool ShouldShowRobot(bool menuVisible)
        {
            return !menuVisible;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА: РОБОТ СКРЫТ ДО ЗАКРЫТИЯ МЕНЮ — ЧИСТАЯ ФУНКЦИЯ.
        /// ФОРМА (требование владельца): `[KvMr] РОБОТ: скрыт до закрытия меню — выбор места начнётся
        /// после выхода из меню`.
        /// </summary>
        public static string DescribeRobotHidden()
        {
            return "[KvMr] РОБОТ: скрыт до закрытия меню — выбор места начнётся после выхода из меню";
        }

        /// <summary>
        /// ПОД ЛУЧОМ ПОДТВЕРЖДЁННЫЙ СТОЛ (а не просто горизонтальная поверхность).
        ///
        /// ЗАЧЕМ. ЧТО БЫЛО: под лучом в MR любая горизонтальная поверхность считалась столом — а пол
        /// в шлеме тоже горизонтален и его коллайдер есть (он часть меша комнаты). Поэтому «робот
        /// поставлен на реальную поверхность» одинаково означало и стол, и пол. ТЕПЕРЬ решение
        /// принимает классификатор (`KvRealTableClassifier`), и состояние честно говорит, подтверждён
        /// стол или нет. Постановка при этом не блокируется: место выбирает оператор.
        /// </summary>
        public bool HitConfirmedTable { get { return hasHit && hitConfirmedTable; } }

        private KvMrSurfaces surfaces;
        private Transform mrWorldRoot;
        private GameObject reticle;
        private LineRenderer beam;
        private Renderer reticleRenderer;
        private Material greenMaterial;
        private Material redMaterial;

        /// <summary>
        /// ПОКАЗЫВАТЬ ЛУЧ И МЕТКУ ПРИЦЕЛА В MR. ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНО — решение владельца
        /// 04.11.2026: «зоны включены, какой ужас — выключи все плюшки по умолчанию». Луч и метка
        /// это подсказка, а не рабочий орган: постановка идёт автопутём и кнопкой A, поэтому без них
        /// всё работает. Включается флагом (в инспекторе компонента), если владелец попросит.
        /// </summary>
        public bool showAimByDefault = false;

        /// <summary>
        /// ПОКАЗЫВАТЬ ЛИ ЛУЧ И МЕТКУ ПРИЦЕЛА (чистая функция — проверяется тестом без сцены).
        ///
        /// Решение владельца 04.11.2026 («убери все плюшки из режима по умолчанию») сводится РОВНО к
        /// этому правилу: пока владелец не включил флаг, ни луч, ни метка не рисуются — постановка
        /// идёт автопутём и кнопкой A. Без явного правила «выключено» держалось только на том, что
        /// `UpdateVisuals` вызывался после `applyFlag = false`, и любой рефакторинг мог вернуть плюшку.
        /// </summary>
        public static bool ShouldShowAim(bool enabledByOwner, bool hasAimDirection)
        {
            return enabledByOwner && hasAimDirection;
        }

        private bool hasHit;
        private Vector3 hitPoint;
        private Vector3 hitNormal;

        /// <summary>Под лучом поверхность, которую классификатор признал СТОЛОМ (а не полом/стеной).</summary>
        private bool hitConfirmedTable;

        /// <summary>Проверка поверхности под лучом словами — уходит в журнал постановки.</summary>
        private string hitSurfaceReason = "поверхность под лучом ещё не проверялась · уверенность 0.00";

        /// <summary>Поверхность, для которой вердикт уже посчитан (см. `ClassifyHit`: раз в кадр нельзя).</summary>
        private Collider lastClassifiedCollider;
        private bool lastClassifiedIsTable;
        private string lastClassifiedReason = "поверхность ещё не проверялась · уверенность 0.00";

        /// <summary>О неподтверждённой площадке предупреждаем один раз, а не каждый кадр.</summary>
        private bool warnedUnconfirmed;
        private float startedAt;

        /// <summary>
        /// ОПОРНОЕ ВРЕМЯ АВТО-ПОСТАНОВКИ — момент, от которого считается «сколько уже ждём».
        ///
        /// ЧТО БЫЛО: сравнение шло с `startedAt` (`Time.unscaledTime - startedAt`), то есть от СТАРТА
        /// MR. Пока оператор читал меню, время шло, и робот появлялся сам (жалоба владельца).
        /// ЧТО СТАЛО: при открытом меню это значение сдвигается на каждом кадре, поэтому отсчёт
        /// начинается заново ПОСЛЕ закрытия меню. Время, проведённое в меню, не засчитывается:
        /// оператор может держать меню 5 минут, и робот всё равно не появится.
        /// </summary>
        private float autoPlaceWaitFrom;

        private bool fallbackTried;
        private bool placedFromFallback;
        private bool warnedNoSurface;
        private bool aWasDown;

        /// <summary>Прошлое состояние «меню видно» — нужен ФРОНТ, а не повтор строки каждый кадр.</summary>
        private bool menuWasVisible;

        /// <summary>Последний известный факт «меню было открыто хотя бы раз за сеанс MR».</summary>
        private bool warnedHiddenLine;

        /// <summary>Создать компонент в рантайме (сцена не правится — её SHA256 закреплён тестом).</summary>
        public static KvMrPlacement Ensure(Transform parent, Camera camera, KvXrMrRoom room)
        {
            KvMrPlacement existing = null;
            if (parent != null) existing = parent.GetComponentInChildren<KvMrPlacement>(true);
            if (existing != null) return existing;

            var go = new GameObject("KvMrPlacement");
            if (parent != null) go.transform.SetParent(parent, false);
            KvMrPlacement placement = go.AddComponent<KvMrPlacement>();
            placement.headCamera = camera;
            placement.room = room;
            return placement;
        }

        /// <summary>
        /// ПЕРЕХВАТ ПОДТВЕРЖДЕНИЯ (A). Вызывается из `KvXrActions.Confirm`.
        /// Возвращает true, если нажатие израсходовано постановкой и обычное действие выполнять нельзя.
        /// </summary>
        public static bool TryConsumeConfirm()
        {
            KvMrPlacement active = Active;
            if (active == null || active.room == null || !active.room.MrActive) return false;

            // Пока робот не поставлен оператором — кнопка A принадлежит постановке.
            if (active.PlacedByOperator) return false;

            // В интерфейсе A — «подтвердить» для меню/окон; отбирать его там нельзя.
            if (KvXrStartMenu.Active != null && KvXrStartMenu.Active.Visible) return false;
            if (KvXrWindowHost.Active != null && KvXrWindowHost.Active.LastWindow != null) return false;

            if (!active.hasHit)
            {
                Debug.Log("[KvMr] A: под лучом НЕТ реальной поверхности — наведите луч на свой стол");
                return true;                 // израсходовано: второе действие по тому же A не нужно
            }

            // ЧЕСТНАЯ ПОДПИСЬ: если поверхность под лучом не подтверждена как стол, оператор узнаёт
            // об этом ДО постановки (а не читает потом «робот на столе», стоя на полу).
            if (!active.hitConfirmedTable)
            {
                Debug.LogWarning("[KvMr] A: поверхность под лучом НЕ подтверждена как стол — " +
                                 active.hitSurfaceReason + ". Робот будет поставлен по вашему выбору; " +
                                 "если это пол, перенесите луч на столешницу");
            }

            active.Place(active.hitPoint, active.hitNormal,
                active.hitConfirmedTable ? "по кнопке A (стол подтверждён)" : "по кнопке A (стол НЕ подтверждён)");
            active.PlacedByOperator = true;
            return true;
        }

        void Awake()
        {
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (reticle != null) Destroy(reticle);
            if (beam != null) Destroy(beam.gameObject);
            DestroyMaterial(greenMaterial);
            DestroyMaterial(redMaterial);
        }

        void Start()
        {
            startedAt = Time.unscaledTime;
            autoPlaceWaitFrom = Time.unscaledTime;
            if (headCamera == null) headCamera = Camera.main;
            // ТОЛЬКО ЧЕРЕЗ Ensure: второй экземпляр компонента построил бы вторую копию
            // коллайдеров комнаты — луч видел бы комнату дважды.
            surfaces = KvMrSurfaces.Ensure(null);
            surfaces.StartTracking();
            mrWorldRoot = KvXrMrRoom.EnsureWorldRoot();
            CreateVisuals();

            // РОБОТ СКРЫТ ДО ЗАКРЫТИЯ МЕНЮ (пункт 1 требования владельца 12.11.2026).
            //
            // ЧТО БЫЛО: `Start` печатал «наведите луч на реальный стол и нажмите A», а робот в этот
            // момент УЖЕ стоял в мире — его включил `KvXrMrRoom.PrepareRobotForWorld` до входа в меню.
            // ЧЕМ ПЛОХО: «при первом запуске MR сразу запускается робот» — ровно слова владельца.
            // ЧТО СТАЛО: пока меню открыто, признак `RobotHiddenUntilMenuClosed` держит робота
            // скрытым, и оператор видит выбор места, а не готового робота.
            if (IsMenuVisible())
            {
                RobotHiddenUntilMenuClosed = true;
                menuWasVisible = true;
                HideRobotInWorld();
                if (!warnedHiddenLine)
                {
                    warnedHiddenLine = true;
                    Debug.Log(DescribeRobotHidden());
                }
            }
            else
            {
                // АНТИ-ВАКУУМ: меню не открыто (запуск сразу в мире, подавленное меню на планшете) —
                // робот не скрывается НИКОГДА, иначе он не появился бы вовсе.
                RobotHiddenUntilMenuClosed = false;
            }

            Debug.Log("[KvMr] постановка: наведите луч на реальный стол и нажмите A — «поставить сюда»");
        }

        void Update()
        {
            if (room == null || !room.MrActive) return;

            UpdateAim();

            bool menuVisible = IsMenuVisible();
            FollowMenuVisibility(menuVisible);

            // ОТСЧЁТ ОТ ЗАКРЫТИЯ МЕНЮ, А НЕ ОТ СТАРТА MR (пункт 2 требования владельца 12.11.2026).
            //
            // ЧТО БЫЛО: `elapsed = Time.unscaledTime - startedAt`, где `startedAt` — момент `Start()`,
            // то есть вход в MR. Пока оператор читал меню, время шло: через `autoPlaceDelaySeconds`
            // авто-постановка ставила робота сама. «Меню открыто 5 минут» ничего не меняло.
            // ЧЕМ ПЛОХО: место выбирала программа, а не оператор, — ровно то, на что жалуется владелец.
            // ЧТО СТАЛО: пока `WaitForMenuToClose` истинно, опорное время сдвигается на каждом кадре
            // (см. `PushAutoPlaceWaitStart`), поэтому в меню время НЕ идёт вовсе. Длительность
            // ожидания после выхода остаётся прежней (`autoPlaceDelaySeconds`).
            float secondsSinceClose = Time.unscaledTime - autoPlaceWaitFrom;

            // АВТОПОСТАНОВКА: без неё в MR было бы «пусто и непонятно, что делать».
            //
            // ПОЧЕМУ ПОВТОРЯЕМ, А НЕ ОДИН РАЗ (найдено на устройстве 02.11.2026): меш комнаты на
            // PICO появляется не мгновенно (а если шлем только что надели — и вовсе через несколько
            // секунд). Однократная попытка через 3 с давала «поверхность не найдена» и оставляла
            // робота на полу. Теперь попытка повторяется, пока оператор не подтвердил точку.
            //
            // НО НЕ ПОВЕРХ РЕЖИМА ВЫБОРА МЕСТА И НЕ ПРИ ОТКРЫТОМ МЕНЮ (12.11.2026, пункты 1–3
            // владельца). ЧТО БЫЛО: две руки тянули робота в разные стороны — автопостановка раз в 4 с
            // переносила его на «подтверждённый стол», а режим выбора вёл его за правым лучом; а при
            // открытом меню она вообще срабатывала раньше, чем оператор что-либо выбрал. ОПЕРАТОР
            // видел, что робот «сам прыгает». ЧТО СТАЛО: решение принимает ОДНА чистая функция
            // `ShouldAutoPlace`, проверяемая тестом без сцены: меню открыто → нет, место
            // зафиксировано → нет, выбирает оператор → нет, время не пришло → нет.
            if (CanAutoPlaceNow(menuVisible, secondsSinceClose))
            {
                // ПОВТОР НЕ ЧАЩЕ, ЧЕМ РАЗ В `autoPlaceRetrySeconds`. Прежняя переменная держала
                // момент СЛЕДУЮЩЕЙ попытки; теперь, когда отсчёт может стоять на месте, проще хранить
                // момент ПОСЛЕДНЕЙ попытки — иначе значение «из будущего» пришлось бы пересчитывать.
                if (Time.unscaledTime - lastAutoTryAt >= Mathf.Max(1f, autoPlaceRetrySeconds) ||
                    lastAutoTryAt <= 0f)
                {
                    lastAutoTryAt = Time.unscaledTime;
                    TryAutoPlace();
                }
            }

            // ЗАПАСНОЙ ПУТЬ: реальных поверхностей нет — ставим робота на пол перед оператором,
            // но говорим об этом прямо, а не делаем вид, что стол найден. Если поверхности появятся
            // позже, постановка с пола будет перенесена на стол (см. TryAutoPlace).
            //
            // ТЕ ЖЕ ВОРОТА, ЧТО У АВТОПОСТАНОВКИ (12.11.2026): «пол через 25 с» — это тоже постановка
            // без участия оператора, и она обязана ждать закрытия меню и фиксации места. Иначе владелец
            // увидел бы ровно то, на что жалуется: робота, появившегося сам, пока он в меню.
            if (!Placed && !fallbackTried && !menuVisible && !AutoPlaceLocked &&
                !RobotHiddenUntilMenuClosed && secondsSinceClose >= fallbackFloorDelaySeconds)
            {
                fallbackTried = true;
                PlaceFallbackOnFloor();
            }

            UpdateVisuals();
        }

        // ---------------------------------------------------------------- меню и видимость робота

        /// <summary>МЕНЮ СЕЙЧАС НА ВИДУ (через реестр `KvXrStartMenu.Active`, без поиска по сцене).</summary>
        private static bool IsMenuVisible()
        {
            KvXrStartMenu menu = KvXrStartMenu.Active;
            return menu != null && menu.Visible;
        }

        /// <summary>
        /// СЛЕДИТЬ ЗА МЕНЮ: скрыт → показан → закрыт. Печатает строки ТОЛЬКО при СМЕНЕ состояния
        /// (требование «не чаще раза в секунду и только при смене»): проверка дешёвая, а журнал
        /// шлема не забивается одной и той же строкой 90 раз в секунду.
        /// </summary>
        private void FollowMenuVisibility(bool menuVisible)
        {
            if (menuVisible)
            {
                if (!menuWasVisible)
                {
                    // МЕНЮ ОТКРЫЛИ ЗАНОВО: робота в мире показывать не нужно, пока оператор не выйдет.
                    menuWasVisible = true;
                    if (!AutoPlaceLocked)
                    {
                        RobotHiddenUntilMenuClosed = true;
                        if (!warnedHiddenLine)
                        {
                            warnedHiddenLine = true;
                            Debug.Log(DescribeRobotHidden());
                        }
                    }
                }
                return;
            }

            if (!menuWasVisible && !RobotHiddenUntilMenuClosed) return;   // меню и не открывалось

            menuWasVisible = false;
            if (AutoPlaceLocked) return;                                  // место зафиксировано — не трогаем

            // ВЫХОД ИЗ МЕНЮ = РОБОТ ПОКАЗЫВАЕТСЯ И НАЧИНАЕТСЯ ВЫБОР МЕСТА (пункты 3 требования).
            if (RobotHiddenUntilMenuClosed)
            {
                RobotHiddenUntilMenuClosed = false;
                ShowRobotInWorld();
            }
            AwaitingOperatorChoice = true;
        }

        /// <summary>
        /// ЗАКРЫТИЕ МЕНЮ ОТМЕНЯЕТ ОЖИДАНИЕ (зовётся из `KvXrStartMenu.Hide` и из режима выбора места).
        ///
        /// СИНХРОННО, А НЕ «НА СЛЕДУЮЩЕМ КАДРЕ» (исправлено 13.11.2026 по падению теста).
        ///
        /// ЧТО БЫЛО. Метод возвращал `true` только если робот был скрыт ИМЕННО ЭТИМ компонентом
        /// (`wasHidden = RobotHiddenUntilMenuClosed`). У компонента, созданного уже после закрытия меню
        /// (а `KvMrRobotPick.Ensure` создаёт его как раз в этот момент), флага не было — и вызов
        /// отвечал «ничего не показал», хотя показывать было нужно. Правильный вопрос — не «прятал ли
        /// я его раньше», а «виден ли он теперь»: оператор вышел из меню, значит робот обязан быть
        /// в мире, кто бы его ни гасил.
        ///
        /// ЧЕМ ПЛОХО БЫЛО. Шаг 3 требования владельца («после закрытия меню включается выбор места и
        /// робот ПОКАЗЫВАЕТСЯ») держался на случайном совпадении порядка создания компонентов.
        ///
        /// ЧТО СТАЛО. Скрытость снимается всегда, робот включается всегда (если он найден), а
        /// возвращаемое значение говорит ФАКТ: «был скрыт и теперь показан» — для журнала.
        /// Никакого «следующего кадра» здесь нет и быть не может: всё выполняется в том же вызове.
        /// </summary>
        public bool BeginOperatorChoice()
        {
            // СВЕЖЕЕ ОПОРНОЕ ВРЕМЯ: отсчёт авто-постановки начинается ЗДЕСЬ и никогда раньше.
            PushAutoPlaceWaitStart();
            lastAutoTryAt = 0f;

            bool wasHidden = RobotHiddenUntilMenuClosed || !IsRobotActiveInWorld();
            RobotHiddenUntilMenuClosed = false;
            AwaitingOperatorChoice = true;
            ShowRobotInWorld();
            return wasHidden;
        }

        /// <summary>РОБОТ СЕЙЧАС ВКЛЮЧЁН В МИРЕ (или его нет — тогда показывать нечего).</summary>
        private bool IsRobotActiveInWorld()
        {
            if (room == null || room.robotOnTable == null) return false;
            return room.robotOnTable.gameObject.activeSelf;
        }

        /// <summary>
        /// ЗАКРЕПИТЬ МЕСТО: с этого момента авто-постановка выключена НАВСЕГДА (пункты 4–5 требования).
        ///
        /// ЧТО БЫЛО: робота можно было двигать авто-постановкой и ПОСЛЕ подтверждения — режим выбора
        /// ставил точку, но таймер продолжал переносить робота «на подтверждённый стол» раз в 4 с.
        /// ЧЕМ ПЛОХО: «после подтверждения робот остаётся на этом месте» не выполнялось.
        /// ЧТО СТАЛО: флаг `AutoPlaceLocked` выключает авто-постановку и запасной путь навсегда;
        /// двигать робота может только явная команда «Переместить робота».
        /// </summary>
        public void LockPlacement(string cause)
        {
            AutoPlaceLocked = true;
            AwaitingOperatorChoice = false;
            RobotHiddenUntilMenuClosed = false;
            ShowRobotInWorld();
            Debug.Log("[KvMr] МЕСТО РОБОТА ЗАКРЕПЛЕНО: авто-постановка выключена (" +
                      (string.IsNullOrEmpty(cause) ? "место подтверждено" : cause) + ")");
        }

        /// <summary>Опорное время авто-постановки — сдвинуть на «сейчас» (время в меню не считается).</summary>
        private void PushAutoPlaceWaitStart()
        {
            autoPlaceWaitFrom = Time.unscaledTime;
        }

        /// <summary>Момент последней попытки авто-постановки (для ограничения частоты повторов).</summary>
        private float lastAutoTryAt;

        /// <summary>
        /// СКРЫТЬ РОБОТА В МИРЕ (пункт 1 требования). ЧТО БЫЛО: признака не было, и робот был виден
        /// с первого кадра MR. ЧТО СТАЛО: одно место, где робот гасится, и одно, где показывается.
        /// </summary>
        private void HideRobotInWorld()
        {
            if (room == null || room.robotOnTable == null) return;
            room.robotOnTable.gameObject.SetActive(false);
        }

        /// <summary>ПОКАЗАТЬ РОБОТА В МИРЕ (после выхода из меню или при фиксации места).</summary>
        private void ShowRobotInWorld()
        {
            if (room == null || room.robotOnTable == null) return;
            room.robotOnTable.gameObject.SetActive(true);
        }

        // ---------------------------------------------------------------- луч и маркер

        /// <summary>
        /// АВТОПОСТАНОВКА МОЛЧИТ, ПОКА ОПЕРАТОР ВЫБИРАЕТ МЕСТО САМ (12.11.2026).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНАЯ ПРОВЕРКА. Автопостановка и режим выбора места двигают ОДНОГО и того же
        /// робота: первая «ставит на подтверждённый стол» раз в 4 секунды, второй ведёт его за
        /// правым лучом. Пока обе работают, робот прыгает между двумя местами, и выбор оператора
        /// отменяется без его ведома — ровно то, из-за чего он просил «задавалось бы окончательно до
        /// нажатия на кнопку "переместить робота"». Пока режим включён, автопостановка не решает.
        /// </summary>
        private static bool AutoPlaceSuspended()
        {
            KvMrRobotPick pick = KvMrRobotPick.Active;
            return pick != null && pick.PickActive;
        }

        private void UpdateAim()
        {
            hasHit = false;
            hitConfirmedTable = false;

            Vector3 origin;
            Vector3 direction;
            if (!TryAimDirection(out origin, out direction)) return;

            RaycastHit hit;
            if (!surfaces.TryRaycast(origin, direction, maxDistance, out hit)) return;
            if (hit.normal.y < minUpNormalDot) return;      // стена/наклон — не стол

            hasHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;
            hitConfirmedTable = ClassifyHit(hit.collider);
        }

        /// <summary>
        /// ПРОВЕРИТЬ ПОВЕРХНОСТЬ ПОД ЛУЧОМ КЛАССИФИКАТОРОМ. Возвращает true, только если это СТОЛ
        /// (высота над полом, высота относительно глаз и габариты — в норме), и запоминает причину
        /// для журнала. Пол, стена и «неизвестно» — false, с числами в причине.
        ///
        /// ПОЧЕМУ С ПАМЯТЬЮ ПО КОЛЛАЙДЕРУ. Метод вызывается каждый кадр из `UpdateAim`, а разбор
        /// строит строку с числами. Каждый кадр в шлеме — это мусор в куче ровно тогда, когда
        /// оператор ведёт луч; поэтому повторный разбор той же поверхности не выполняется.
        /// </summary>
        private bool ClassifyHit(Collider collider)
        {
            if (collider != null && collider == lastClassifiedCollider)
            {
                hitSurfaceReason = lastClassifiedReason;
                return lastClassifiedIsTable;
            }

            if (surfaces == null)
            {
                hitSurfaceReason = "проверка недоступна: нет компонента реальных поверхностей · уверенность 0.00";
                return false;
            }

            RealTableVerdict verdict;
            if (!surfaces.TryGetSurfaceVerdict(collider, out verdict))
            {
                hitSurfaceReason = verdict.Reason;
                lastClassifiedCollider = collider;
                lastClassifiedIsTable = false;
                lastClassifiedReason = hitSurfaceReason;
                return false;
            }

            hitSurfaceReason = surfaces.DescribeSurface(collider);
            lastClassifiedCollider = collider;
            lastClassifiedIsTable = verdict.IsTable;
            lastClassifiedReason = hitSurfaceReason;
            return verdict.IsTable;
        }

        /// <summary>Направление луча: из контроллера руки луча, иначе — из головы вниз-вперёд.</summary>
        private bool TryAimDirection(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            if (KvXrInputSource.TryRayAimPose(out origin, out Quaternion rotation))
            {
                direction = rotation * Vector3.forward;
                return true;
            }

            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) return false;

            origin = headCamera.transform.position;
            direction = Quaternion.Euler(30f, headCamera.transform.eulerAngles.y, 0f) * Vector3.forward;
            return true;
        }

        private void CreateVisuals()
        {
            greenMaterial = MakeMaterial(new Color(0.15f, 0.95f, 0.35f, 0.85f));
            redMaterial = MakeMaterial(new Color(0.95f, 0.20f, 0.20f, 0.85f));

            var beamObject = new GameObject("KvMrPlacementBeam");
            beamObject.transform.SetParent(transform, false);
            beam = beamObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = 0.012f;
            beam.endWidth = 0.006f;
            beam.material = greenMaterial;
            beam.enabled = false;

            reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "KvMrPlacementReticle";
            reticle.transform.SetParent(transform, false);
            // Плоский «блин» на поверхности: цилиндр высотой 2 см, сплющенный и утопленный в стол.
            reticle.transform.localScale = new Vector3(0.20f, 0.005f, 0.20f);
            Collider reticleCollider = reticle.GetComponent<Collider>();
            if (reticleCollider != null) Destroy(reticleCollider);        // маркер не должен ловить лучи
            reticleRenderer = reticle.GetComponent<Renderer>();
            if (reticleRenderer != null) reticleRenderer.sharedMaterial = greenMaterial;
            reticle.SetActive(false);
        }

        private void UpdateVisuals()
        {
            if (beam == null || reticle == null) return;

            // ПРИЦЕЛ СТРОИТСЯ ТОЛЬКО ПРИ ВКЛЮЧЁННОМ ФЛАГЕ: при выключенном `TryAimDirection` не
            // вызывается вовсе (это покадровый код), а решение «показывать / нет» принимает одно
            // правило — `ShouldShowAim`, проверяемое тестом.
            Vector3 origin = Vector3.zero;
            Vector3 direction = Vector3.forward;
            bool hasAim = showAimByDefault && TryAimDirection(out origin, out direction);
            if (!ShouldShowAim(showAimByDefault, hasAim))
            {
                // ЧИСТЫЙ ВИД (решение владельца 04.11.2026): луч и метка скрыты по умолчанию,
                // постановка работает без них. Если флаг включат — идём обычным путём ниже.
                beam.enabled = false;
                reticle.SetActive(false);
                return;
            }

            bool show = hasHit || PlacedByOperator == false;
            beam.enabled = hasAim;
            beam.startColor = hasHit ? new Color(0.15f, 0.95f, 0.35f, 0.9f) : new Color(0.95f, 0.2f, 0.2f, 0.6f);
            beam.endColor = beam.startColor;

            Vector3 end = hasHit
                ? hitPoint
                : origin + direction.normalized * Mathf.Min(maxDistance, 3f);
            beam.SetPosition(0, origin);
            beam.SetPosition(1, end);

            reticle.SetActive(hasHit && show);
            if (hasHit && show)
            {
                reticle.transform.position = hitPoint + hitNormal * 0.005f;
                reticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
                if (reticleRenderer != null)
                    reticleRenderer.sharedMaterial = hasHit ? greenMaterial : redMaterial;
            }
        }

        // ---------------------------------------------------------------- постановка

        private void TryAutoPlace()
        {
            // СТОРОЖ НА ВХОДЕ, А НЕ ТОЛЬКО В `Update` (12.11.2026).
            //
            // ЧТО БЫЛО: единственной защитой было условие в `Update`, поэтому любой ДРУГОЙ вызов
            // `TryAutoPlace` (а метод приватный, но код растёт) снова ставил робота при открытом меню.
            // ЧЕМ ПЛОХО: обещание «пока меню открыто, робота в мире нет» держалось бы на честном слове.
            // ЧТО СТАЛО: тот же сторож стоит и здесь — авто-постановка не выполняется, если место
            // зафиксировано, оператор ещё выбирает или меню открыто. Это защита, а не второй расчёт.
            if (AutoPlaceLocked || AwaitingOperatorChoice || RobotHiddenUntilMenuClosed || IsMenuVisible())
                return;

            Vector3 origin;
            Vector3 direction;
            if (headCamera == null) headCamera = Camera.main;

            // ЧТО БЫЛО: автопостановка ставила робота на первую горизонтальную поверхность под
            // лучом и писала «постановка на реальную поверхность» — то есть пол тоже годился и
            // назывался столом.
            // ЧТО СТАЛО: сначала ищем ПОДТВЕРЖДЁННЫЙ классификатором стол (луч руки, затем луч
            // взгляда). Если его нет — постановка не отменяется (иначе в MR было бы «пусто и
            // непонятно»), но подписывается честно: «стол не подтверждён», и в журнал уходит причина
            // с числами. Запасной путь «пол через 25 с» остаётся и тоже подписан честно.
            RaycastHit unconfirmed = default(RaycastHit);
            bool hasUnconfirmed = false;

            // Сначала рука: оператор обычно уже держит контроллер и смотрит на стол.
            if (KvXrInputSource.TryRayAimPose(out origin, out Quaternion rotation))
            {
                direction = rotation * Vector3.forward;
                RaycastHit fromHand;
                if (surfaces.TryRaycast(origin, direction, maxDistance, out fromHand) &&
                    fromHand.normal.y >= minUpNormalDot)
                {
                    if (ClassifyHit(fromHand.collider))
                    {
                        Place(fromHand.point, fromHand.normal, "авто (луч руки): стол подтверждён");
                        return;
                    }
                    unconfirmed = fromHand;
                    hasUnconfirmed = true;
                }
            }

            // Затем взгляд: сверху-вниз под 30° — так оператор видит стол перед собой.
            if (headCamera != null)
            {
                origin = headCamera.transform.position;
                direction = Quaternion.Euler(30f, headCamera.transform.eulerAngles.y, 0f) * Vector3.forward;
                RaycastHit fromHead;
                if (surfaces.TryRaycast(origin, direction, maxDistance, out fromHead) &&
                    fromHead.normal.y >= minUpNormalDot)
                {
                    if (ClassifyHit(fromHead.collider))
                    {
                        Place(fromHead.point, fromHead.normal, "авто (луч взгляда): стол подтверждён");
                        return;
                    }
                    if (!hasUnconfirmed)
                    {
                        unconfirmed = fromHead;
                        hasUnconfirmed = true;
                    }
                }
            }

            if (hasUnconfirmed)
            {
                if (!warnedUnconfirmed)
                {
                    warnedUnconfirmed = true;
                    Debug.LogWarning("[KvMr] автопостановка: под лучом горизонтальная поверхность, но " +
                                     "СТОЛОМ она не подтверждена — " + hitSurfaceReason +
                                     ". Ставлю на неё как на запасное место; если это пол, перенесите луч " +
                                     "на столешницу и нажмите A");
                }
                Place(unconfirmed.point, unconfirmed.normal,
                    "авто (луч): поверхность НЕ подтверждена как стол — возможно, пол");
                return;
            }

            if (!warnedNoSurface)
            {
                warnedNoSurface = true;
                Debug.Log("[KvMr] автопостановка: реальная горизонтальная поверхность перед оператором " +
                          "пока не найдена — повторяю попытку каждые " + autoPlaceRetrySeconds.ToString("0") +
                          " с · можно навести луч на стол и нажать A · " +
                          (surfaces != null ? surfaces.DescribeRealTable : "поверхностей нет"));
            }
        }

        /// <summary>Запасной путь: реальных поверхностей нет — робот на полу в 1,2 м перед оператором.</summary>
        private void PlaceFallbackOnFloor()
        {
            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) return;

            Vector3 forward = headCamera.transform.forward;
            // ОТСЕВ НЕЧИСЛА (E-21): поза головы приходит от рантайма и при потере трекинга может
            // содержать NaN; сравнение длины с NaN всегда false, поэтому проверяем явно.
            if (!IsFiniteVector(forward)) forward = Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            Vector3 point = headCamera.transform.position + forward.normalized * 1.2f;
            point.y = 0f;
            Place(point, Vector3.up, "запасной путь: реальных поверхностей нет, пол перед оператором");
            Debug.LogWarning("[KvMr] реальные поверхности не появились — робот поставлен на ПОЛ перед оператором. " +
                             "Сквозной вид включён, но физика комнаты недоступна. ЧТО ПРОВЕРИТЬ: " +
                             "(1) шлем надет; (2) в настройках PICO выполнена разметка комнаты (сквозной вид → " +
                             "разметка помещения); (3) разрешение на пространственные данные выдано. " +
                             "Если поверхности появятся позже, робот будет перенесён с пола на стол автоматически.");
        }

        /// <summary>
        /// ПОСТАВИТЬ РОБОТА ТОЧКОЙ ПОВЕРХНОСТИ. Вызывается авто-путём и по A.
        /// </summary>
        public void Place(Vector3 surfacePoint, Vector3 surfaceNormal, string cause)
        {
            if (room == null)
            {
                Debug.LogWarning("[KvMr] постановка невозможна: нет MR-комнаты (KvXrMrRoom)");
                // ОТКАЗ — ТОЖЕ СОБЫТИЕ ЗАПИСИ (12.11.2026): «нажал A, робот не встал» — самый
                // частый вопрос разбора, и раньше ответ на него был только в консоли шлема.
                KvActionLog.Instance.Placement("робот на реальную поверхность", false,
                    "нет MR-комнаты (KvXrMrRoom)");
                return;
            }

            // НЕЧИСЛО — НЕ ТОЧКА (класс E-21). ЧТО БЫЛО: проверки не было вовсе, и NaN из позы
            // контроллера или луча доходил до `room.PlaceRobotAtSurface`, а робот с NaN-позицией
            // Unity НЕ РИСУЕТ: он исчезал бесследно, без единой ошибки в журнале. ТЕПЕРЬ отказ с
            // причиной и записью в журнал действий.
            if (!IsFiniteVector(surfacePoint) || !IsFiniteVector(surfaceNormal))
            {
                Debug.LogWarning("[KvMr] постановка не выполнена: точка или нормаль поверхности — не число " +
                                 "(NaN/бесконечность приходят от рантайма при потере трекинга)");
                KvActionLog.Instance.Placement("робот на реальную поверхность", false,
                    "точка поверхности не число (потеря трекинга)");
                return;
            }

            float yaw = YawFacingOperator(surfacePoint);
            if (!room.PlaceRobotAtSurface(surfacePoint, yaw))
            {
                Debug.LogWarning("[KvMr] постановка не выполнена: " + room.LastPlacementError);
                KvActionLog.Instance.Placement("робот на реальную поверхность", false,
                    room.LastPlacementError);
                return;
            }

            // РОБОТ ПОКАЗЫВАЕТСЯ ТЕМ ЖЕ ДЕЙСТВИЕМ, ЧТО ЕГО СТАВЯТ (12.11.2026).
            //
            // ЧТО БЫЛО: пока меню открыто, робот скрыт — и постановка (кнопка A, «переместить
            // робота», запасной путь) ставила его в точку, но НЕ ВКЛЮЧАЛА: приказ «покажи робота»
            // жил в другом месте, и оператор не видел результата.
            // ЧЕМ ПЛОХО: «нажал — робот не появился» — тот самый класс «нажал, ничего не произошло».
            // ЧТО СТАЛО: успешная постановка снимает скрытость и включает объект.
            RobotHiddenUntilMenuClosed = false;
            ShowRobotInWorld();

            Placed = true;
            PlacementCount++;
            bool fromFallback = cause != null && cause.StartsWith("запасной", System.StringComparison.Ordinal);
            bool wasFallback = placedFromFallback;
            placedFromFallback = fromFallback;
            LastSurfacePoint = surfacePoint;
            LastSurfaceNormal = surfaceNormal;
            LastYawDegrees = yaw;
            LastCause = cause;

            var c = System.Globalization.CultureInfo.InvariantCulture;
            if (wasFallback && !fromFallback)
            {
                Debug.Log("[KvMr] робот ПЕРЕНЕСЁН с пола на реальную поверхность: поверхности комнаты " +
                          "появились позже — постановка исправлена автоматически");
            }

            // ПРОВЕРКА ПЛОЩАДКИ В СТРОКЕ ПОСТАНОВКИ. Раньше строка звучала одинаково и для стола,
            // и для пола; теперь рядом с фактом постановки стоит вердикт классификатора с числами.
            bool fromRay = !fromFallback && cause != null &&
                           (cause.IndexOf("луч", System.StringComparison.Ordinal) >= 0 ||
                            cause.IndexOf("кнопке A", System.StringComparison.Ordinal) >= 0);
            string check = fromRay
                ? (hitConfirmedTable
                    ? " · площадка: СТОЛ подтверждён"
                    : " · площадка: стол НЕ подтверждён — " + hitSurfaceReason)
                : " · площадка: " + (fromFallback ? "пол (запасной путь)" : "без проверки лучом");

            Debug.Log("[KvMr] РОБОТ ПОСТАВЛЕН НА РЕАЛЬНУЮ ПОВЕРХНОСТЬ (" + cause + "): точка " +
                      surfacePoint.x.ToString("0.000", c) + "; " + surfacePoint.y.ToString("0.000", c) + "; " +
                      surfacePoint.z.ToString("0.000", c) + " м · нормаль вверх " +
                      surfaceNormal.y.ToString("0.00", c) + " · поворот " + yaw.ToString("0", c) + "° · масштаб " +
                      (room != null ? room.RobotSize.y.ToString("0.000", c) : "1.000") + " м (высота робота, замер)" +
                      check);

            // УСПЕХ — В ЗАПИСЬ, С ТОЧКОЙ И ПОВОРОТОМ. Раньше эта строка жила только в консоли
            // Unity, а файл записи о постановке молчал: по нему нельзя было сверить, куда встал
            // робот, с позой оператора в тех же строках. Числа идут через точку (InvariantCulture):
            // в русской локали «0,123» спутается с разделителем списка при разборе скриптами.
            KvActionLog.Instance.Placement("робот на реальную поверхность", true,
                "точка (" + surfacePoint.x.ToString("0.00", c) + "; " +
                surfacePoint.y.ToString("0.00", c) + "; " +
                surfacePoint.z.ToString("0.00", c) + ") · поворот " + yaw.ToString("0", c) +
                "° · " + cause);
        }

        /// <summary>Поворот робота лицом к оператору (робот смотрит на него передней стороной).</summary>
        private float YawFacingOperator(Vector3 point)
        {
            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) return 0f;

            Vector3 toHead = headCamera.transform.position - point;
            // ОТСЕВ НЕЧИСЛА (E-21): обе точки приходят от рантайма/луча и могут быть NaN.
            if (!IsFiniteVector(toHead)) return 0f;
            toHead.y = 0f;
            if (toHead.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
        }

        /// <summary>ЧИСЛО ЛИ ЭТО (отсев NaN/бесконечности — класс E-21).</summary>
        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        // ---------------------------------------------------------------- материалы

        private static Material MakeMaterial(Color color)
        {
            return KvRenderPipelineShaders.CreateUnlit("Материал_MR_маркера", color);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isEditor) DestroyImmediate(material);
            else Destroy(material);
        }
    }
}
