using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// СТАРТОВОЕ МЕНЮ В ШЛЕМЕ (указание владельца 24.10.2026: «Меня не устраивает, что нет стартового
    /// меню. Сначала сделаем его, чтобы оно было похожим на оверлей SteamOS последней версии или как
    /// сами настройки PICO, скорее второе. Функционал берём из стартового меню в комп версии.
    /// …где можно будет выбрать стол и робота»).
    ///
    /// ЧТО ЭТО. Плоская панель перед оператором: слева разделы (Стол, Робот, Проект, Обучение,
    /// Настройки, Выход), справа — строки выбранного раздела. Раскладка и размеры — в
    /// `KvXrStartMenuLayout`, там же их проверки.
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ КОМПОНЕНТ, А НЕ ЭКРАН ПАНЕЛИ У РУКИ. Панель у руки ездит за кистью — это её
    /// смысл. Меню же должно стоять на своём месте перед человеком, поэтому у его панели
    /// `followHand = false`, а положение задаёт этот компонент.
    ///
    /// ПОЧЕМУ ВНУТРИ — ТА ЖЕ ПАНЕЛЬ. Кнопка, попадание лучом, нажатие, подсветка и подпись уже
    /// сделаны и приняты владельцем в `KvXrGroupPanel` (§40-исправление). Второй такой механизм
    /// означал бы, что дефект «видно, а луч не ловит» (карточка Б50) придётся чинить дважды.
    ///
    /// ЧТО ДЕЛАЕТСЯ С ПАНЕЛЬЮ У РУКИ, ПОКА МЕНЮ ОТКРЫТО. Она выключается, а указатель начинает
    /// целиться в панель меню: иначе два интерфейса висели бы друг на друге, и луч выбирал бы между
    /// ними наугад. При закрытии меню всё возвращается.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrStartMenu : MonoBehaviour
    {
        /// <summary>Живое меню (последнее созданное) — панель меню зовёт его, чтобы закрыться.</summary>
        public static KvXrStartMenu Active { get; private set; }

        /// <summary>Камера шлема: по ней меню ставится перед оператором.</summary>
        public Camera headCamera;

        /// <summary>Насколько далеко перед оператором встаёт меню, метры.</summary>
        public float distanceMeters = 0.55f;

        /// <summary>
        /// Насколько ниже уровня глаз ставится меню, метры. НОЛЬ — строго на высоте головы (указание
        /// владельца 24.10.2026: «перенеси из нулевых координат на высоту головы»).
        /// </summary>
        public float belowEyeMeters = 0f;

        /// <summary>Ниже этой высоты глаз поза считается недостоверной, метры (как в `KvXrInputSource`).</summary>
        public float minPlausibleEyeHeight = 0.5f;

        /// <summary>Показывать меню при запуске приложения (как в компьютерной версии).</summary>
        public bool showOnStart = true;

        /// <summary>Печатать строки в журнал.</summary>
        public bool logOnShow = true;

        private KvXrGroupPanel menuPanel;
        private KvXrGroupPanel handPanel;
        private KvXrUiPointer pointer;
        private bool visible;

        /// <summary>Меню уже поставлено в мире — при повторном открытии оно остаётся на месте.</summary>
        private bool placedOnce;

        /// <summary>Прошлое состояние нажатия стика (для переднего фронта).</summary>
        private bool stickClickWasDown;

        /// <summary>Меню сейчас на виду.</summary>
        public bool Visible { get { return visible; } }

        /// <summary>Панель меню (для проверок).</summary>
        public KvXrGroupPanel Panel { get { return menuPanel; } }

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
            Build();
            if (showOnStart) Show();
        }

        /// <summary>
        /// ВХОД В МЕНЮ — НАЖАТИЕ СТИКА НА ЛЮБОМ КОНТРОЛЛЕРЕ (указание владельца 24.10.2026: «нет
        /// кнопки выхода в меню при случайном/не случайном сворачивании стартового окна… добавь выход
        /// в меню на такое же нажатие на правом контроллере»).
        ///
        /// Стик есть у любого профиля, а курок и щипок заняты нажатием по панели. Работает и на
        /// левом, и на правом — владелец просил именно так.
        /// </summary>
        void Update()
        {
            // ЖДЁМ ДОСТОВЕРНУЮ ПОЗУ ГОЛОВЫ (указание владельца 24.10.2026: «перенеси из нулевых
            // координат на высоту головы»). В первые секунды поза нулевая; как только она появилась —
            // ставим меню на высоту головы и показываем.
            if (visible && !placedOnce)
            {
                if (PlaceInFront())
                {
                    placedOnce = true;
                    if (menuPanel != null)
                    {
                        menuPanel.gameObject.SetActive(true);
                        menuPanel.OpenStartMenu();
                    }

                    if (logOnShow)
                        Debug.Log("[KvXr] МЕНЮ: поставлено на высоту головы · " +
                                  transform.position.ToString("0.00") + " м");
                }

                return;
            }

            if (visible) { stickClickWasDown = false; return; }

            bool leftDown = KvXrInputSource.LeftStickClick;
            bool rightDown = KvXrInputSource.RightStickClick;
            bool down = leftDown || rightDown;

            if (down && !stickClickWasDown)
            {
                Debug.Log("[KvXr] МЕНЮ: открыто нажатием стика (" +
                          (leftDown ? "левый" : "правый") + " контроллер)");
                Show();
            }

            stickClickWasDown = down;
        }

        /// <summary>ПЕРЕСТАВИТЬ МЕНЮ ПЕРЕД ОПЕРАТОРОМ — только по явному вызову, не само.</summary>
        public void Reposition()
        {
            PlaceInFront();
            placedOnce = true;
            if (logOnShow) Debug.Log("[KvXr] МЕНЮ: переставлено перед оператором (по вызову)");
        }

        /// <summary>
        /// СОЗДАТЬ МЕНЮ, ЕСЛИ ЕГО ЕЩЁ НЕТ. Вызывается ригом при сборке начинки (§K: сцена не
        /// редактируется, всё создаётся в рантайме).
        /// </summary>
        public static KvXrStartMenu Ensure(Transform parent, Camera camera)
        {
            KvXrStartMenu existing = Object.FindAnyObjectByType<KvXrStartMenu>();
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return existing;
            }

            var go = new GameObject("KvXrStartMenu");

            // МЕНЮ ЖИВЁТ В МИРЕ, А НЕ В РИГЕ (указание владельца 24.10.2026: «до сих пор оверлей
            // прикреплён ко взгляду»). Пока оно было ребёнком рига, любое смещение рига тащило его за
            // собой — то есть оно вело себя как прикреплённое. Теперь это объект сцены.
            go.transform.SetParent(null, false);

            KvXrStartMenu menu = go.AddComponent<KvXrStartMenu>();
            menu.headCamera = camera;
            menu.Build();
            return menu;
        }

        /// <summary>Создать панель меню (внутри — та же панель, что у руки, но не следующая за кистью).</summary>
        public void Build()
        {
            if (menuPanel != null) return;

            var panelGo = new GameObject("KvXrStartMenuPanel");
            panelGo.transform.SetParent(transform, false);

            menuPanel = panelGo.AddComponent<KvXrGroupPanel>();
            menuPanel.headCamera = headCamera;
            menuPanel.followHand = false;
            menuPanel.logOnBuild = false;
            menuPanel.sortingOrder = 70;
            menuPanel.OpenStartMenu();
        }

        /// <summary>ПОКАЗАТЬ МЕНЮ: поставить перед оператором, отдать указатель ему.</summary>
        public void Show()
        {
            Build();
            visible = true;

            // МЕНЮ СТОИТ В МИРЕ И БОЛЬШЕ НЕ ДВИГАЕТСЯ (указание владельца: «не устраивает, что всё
            // прикреплено ко взгляду, надо сделать как оверлей у PICO»). Ставим ОДИН раз за запуск;
            // при повторном открытии оно на прежнем месте — даже если оператор отошёл (окна и меню
            // остаются висеть в воздухе, как он и просил). Переставить можно только ЯВНО —
            // `Reposition()`.
            if (!placedOnce)
            {
                placedOnce = PlaceInFront();

                if (!placedOnce)
                    Debug.Log("[KvXr] МЕНЮ: ждём достоверную позу головы — меню встанет на высоту головы, " +
                              "как только она появится (нулевая поза отвергается, иначе меню встало бы на пол)");
            }

            if (menuPanel != null)
            {
                // Пока позы нет — меню НЕ показываем: иначе оно висело бы из нулевых координат.
                menuPanel.gameObject.SetActive(placedOnce);
                if (placedOnce) menuPanel.OpenStartMenu();   // список столов и роботов — на текущий момент
            }

            FindHandPanel();
            if (handPanel != null) handPanel.gameObject.SetActive(false);

            FindPointer();
            if (pointer != null && menuPanel != null) pointer.groupPanel = menuPanel;

            if (logOnShow)
                Debug.Log("[KvXr] МЕНЮ: показано · раздел «" +
                          KvXrStartMenuLayout.SectionTitle((int)KvXrStartSection.Tables) +
                          "» · столов " + (menuPanel != null ? menuPanel.StartTableCount : 0) +
                          " · роботов " + (menuPanel != null ? menuPanel.StartRobotCount : 0) +
                          " · панель у руки скрыта до закрытия меню");
        }

        /// <summary>СКРЫТЬ МЕНЮ: вернуть панель у руки и указатель к ней.</summary>
        public void Hide()
        {
            visible = false;

            if (menuPanel != null) menuPanel.gameObject.SetActive(false);
            if (handPanel != null) handPanel.gameObject.SetActive(true);

            FindPointer();
            if (pointer != null && handPanel != null) pointer.groupPanel = handPanel;

            if (logOnShow) Debug.Log("[KvXr] МЕНЮ: закрыто · указатель и панель вернулись к руке");
        }

        /// <summary>Показать/скрыть меню (кнопка на панели, клавиша, команда).</summary>
        public void Toggle()
        {
            if (visible) Hide();
            else Show();
        }

        /// <summary>
        /// ПОСТАВИТЬ МЕНЮ ПЕРЕД ОПЕРАТОРОМ. Разворот — НА оператора, а не вдоль взгляда: у мирового
        /// канваса видна передняя сторона, и разворот «по взгляду» показал бы владельцу спину панели
        /// (этот дефект уже стоил сессии 16.10.2026).
        /// </summary>
        public bool PlaceInFront()
        {
            Vector3 eye;
            Vector3 forward;

            // ПОЗА ГОЛОВЫ С ПРОВЕРКОЙ ПРАВДОПОДОБИЯ ([F4]): в первые секунды после запуска она бывает
            // нулевой, и меню, поставленное по ней, оказалось бы «из нулевых координат» — на полу.
            if (!TryGetEye(out eye, out forward)) return false;

            float distance = Mathf.Clamp(distanceMeters, 0.35f, 1.50f);
            Vector3 position = eye + forward * distance;
            position.y = eye.y - Mathf.Clamp(belowEyeMeters, 0f, 0.40f);

            transform.position = position;

            // РАЗВОРОТ — ПЕРЕВЁРНУТ (указание владельца 24.10.2026: «переверни панель стартового
            // меню»). Было `LookRotation(-forward)`: меню разворачивалось НА оператора. Владелец
            // увидел обратную сторону — значит у канваса меню видна другая сторона, и разворот
            // нужен по взгляду. Теперь лицом к оператору смотрит та сторона, которую он видит.
            transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            return true;
        }

        /// <summary>
        /// ГДЕ ГЛАЗА И КУДА СМОТРИТ ОПЕРАТОР. Сначала спрашивается поза шлема у `KvXrInputSource` —
        /// она проходит проверку правдоподобия; камера — запасной путь. `false` — позы нет.
        /// </summary>
        private bool TryGetEye(out Vector3 eye, out Vector3 forward)
        {
            eye = Vector3.zero;
            forward = Vector3.forward;

            Vector3 headPosition;
            Quaternion headRotation;

            if (KvXrInputSource.TryHeadPose(out headPosition, out headRotation) &&
                headPosition.y >= minPlausibleEyeHeight)
            {
                eye = headPosition;
                forward = FlatForward(headRotation * Vector3.forward);
                return true;
            }

            Camera camera = headCamera != null ? headCamera : Camera.main;
            if (camera != null && camera.transform.position.y >= minPlausibleEyeHeight)
            {
                eye = camera.transform.position;
                forward = FlatForward(camera.transform.forward);
                return true;
            }

            return false;
        }

        /// <summary>Горизонтальное направление взгляда (без наклона головы вверх/вниз).</summary>
        private static Vector3 FlatForward(Vector3 direction)
        {
            Vector3 planar = Vector3.ProjectOnPlane(direction, Vector3.up);
            return planar.sqrMagnitude > 0.0001f ? planar.normalized : Vector3.forward;
        }

        /// <summary>Найти панель У РУКИ (ту, что следует за кистью), а не панель меню.</summary>
        private void FindHandPanel()
        {
            if (handPanel != null) return;

            KvXrGroupPanel[] panels = Object.FindObjectsByType<KvXrGroupPanel>(FindObjectsInactive.Include);
            for (int i = 0; i < panels.Length; i++)
            {
                if (panels[i] == null || panels[i] == menuPanel) continue;
                if (!panels[i].followHand) continue;
                handPanel = panels[i];
                return;
            }
        }

        private void FindPointer()
        {
            if (pointer != null) return;

            pointer = Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer == null)
                pointer = Object.FindAnyObjectByType<KvXrUiPointer>(FindObjectsInactive.Include);
        }
    }
}
