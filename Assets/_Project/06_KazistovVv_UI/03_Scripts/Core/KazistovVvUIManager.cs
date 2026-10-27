using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using TrajectoryCore;

namespace KazistovVvUI
{
    /// <summary>
    /// Менеджер ДЕСКТОПНОГО интерфейса KazistovVv в стиле FreeCAD (ПК-версия, не VR):
    ///
    ///   ┌──────────────────────────────── строка меню (Файл/Правка/Вид/Робот/Сервис/Справка)
    ///   │ [ 15 кнопок-иконок, 3 ряда × 5 ] ............... «верстак» (робот / SCARA)
    ///   ├──────────┬──────────────────────────────┬──────────┐
    ///   │ ДЕРЕВО   │            СЦЕНА             │ СВОЙСТВА │
    ///   │ моделей  │                              │          │
    ///   ├──────────┴──────────────────────────────┴──────────┤
    ///   │ dock-панель «Настройки/Справка» (снизу)             │
    ///   ├─────────────────────────────────────────────────────┤
    ///   │ статус-бар: состояние · робот · луч · сообщение · тема
    ///
    /// Панели — dockable (перетаскивание заголовка, прилипание к краям, изменение
    /// толщины, сворачивание, закрытие). Тема — Тёмная/Светлая/Системная (PlayerPrefs,
    /// мгновенное переключение). Кнопки и пункты меню строятся из реестра команд,
    /// поэтому интерфейс расширяется без правок панелей.
    ///
    /// ВАЖНО: этот класс НЕ трогает State Machine, лазеры, фантомы, планировщик и бинды —
    /// он только читает их состояние и вызывает уже существующие публичные методы.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class KazistovVvUIManager : MonoBehaviour
    {
        /// <summary>Версия оболочки (показывается на вкладке «О программе»).</summary>
        public const string UiVersion = "1.0 · FreeCAD-style desktop";

        /// <summary>Активный (не-дубликат) менеджер UI.</summary>
        public static KazistovVvUIManager Instance { get; private set; }

        [Header("Разрешение UI (16:9, 1920×1080)")]
        [Tooltip("Опорное разрешение канваса по горизонтали")]
        public float uiReferenceWidth = 1920f;
        [Tooltip("Опорное разрешение канваса по вертикали")]
        public float uiReferenceHeight = 1080f;

        [Header("Опции")]
        [Tooltip("Автоматически пересобирать дерево моделей при изменениях сцены")]
        public bool autoRebuildTree = true;
        [Tooltip("UI виден (TAB переключает вместе с курсором)")]
        public bool uiVisible = true;

        [Header("«Лампочка Ильича»")]
        [Tooltip("Тёплая точечная лампа над столом (только источник света)")]
        public bool enableWorkLamp = true;
        [Tooltip("Высота лампы над столешницей, метры")]
        public float lampHeight = 1.25f;
        [Tooltip("Яркость (HDRP, кандела)")]
        public float lampIntensity = 900f;
        [Tooltip("Дальность света, метры")]
        public float lampRange = 7f;
        [Tooltip("Тёплая температура, K")]
        public float lampTemperature = 2700f;

        [Header("Светоотражение рабочих объектов")]
        [Tooltip("Роботы и столы отражают свет сильнее (ярче/гладче), чем ангар/пол — без новых источников")]
        public bool enableLightBoost = true;
        [Tooltip("Множитель albedo (базовый цвет) роботов/столов")]
        public float lightBoostAlbedo = 1.35f;
        [Tooltip("Добавка к smoothness роботов/столов")]
        public float lightBoostSmoothness = 0.12f;
        [Tooltip("Собственная эмиссия (0..1 от albedo): объект «светится» сильнее, источники не добавляются")]
        public float lightBoostEmissive = 0.35f;

        [Header("Оболочка в стиле FreeCAD")]
        [Tooltip("Ширина панели «Дерево моделей» по умолчанию, px")]
        public float treePanelWidth = 272f;
        [Tooltip("Ширина панели «Свойства» по умолчанию, px")]
        public float propertiesPanelWidth = 296f;
        [Tooltip("Высота dock-панели «Настройки/Справка» по умолчанию, px")]
        public float settingsPanelHeight = 224f;

        [Header("Частота обновления панелей, с (производительность)")]
        public float statusInterval = 0.1f;
        public float propertiesInterval = 0.2f;
        public float treeInterval = 0.4f;

        [Header("Диагностика")]
        [Tooltip("Писать в консоль события интерфейса (нажатия кнопок, переключение темы/панелей)")]
        public bool logUiEvents = true;

        // ------------------------------------------------------------------ состояние оболочки

        /// <summary>
        /// ФИКС 8: желаемый максимум рядов кнопок в тулбаре. На широком экране сетка
        /// растягивается по ширине канваса и укладывается в 4 ряда (раньше — жёстко 5 в ряд,
        /// то есть 8–9 рядов на 42 кнопки, что на 1366×768 съедало треть экрана).
        /// </summary>
        private const int MaxToolbarRows = 4;

        private Canvas canvas;
        private RectTransform canvasRect;
        private CanvasScaler scaler;

        private KvMenuBar menuBar;
        private KvToolbar toolbar;
        private KvDockPanel treeDock;
        private KvDockPanel propertiesDock;
        private KvDockPanel settingsDock;
        private KvDockPanel hotkeyDock;
        private KvTreeView tree;
        private KvPropertiesView properties;
        private KvSettingsView settings;
        private KvHotkeyView hotkeys;
        private KvStatusBar status;
        private KvSelectionHighlight highlight;
        private KvSettingsCallbacks callbacks;
        private KvCommandPalette palette;
        private KvUiStates uiStates;
        private KvGamepadHud gamepadHud;
        private KvGamepadRouter gamepadRouter;
        private KvKeyboardNav keyboardNav;

        private ObjectSpawner spawner;
        private IdleCameraBrain idleBrain;
        private AimIndicator aimIndicator;
        private TrajectoryFlowController flow;
        private FreeFlyCameraController cameraController;
        private CenterWindow centerWindow;

        /// <summary>
        /// СПАВНЕР ОБЪЕКТОВ ДЛЯ ШЛЕМА (§42). В шлеме из стартового меню создаются столы и роботы;
        /// чтобы не заводить вторую фабрику, меню шлема пользуется ТЕМ ЖЕ `ObjectSpawner`, что и
        /// компьютерный интерфейс. Свойство только отдаёт экземпляр — поведение прежнего кода не меняется.
        /// </summary>
        public ObjectSpawner Objects
        {
            get
            {
                if (spawner == null) spawner = gameObject.AddComponent<ObjectSpawner>();
                return spawner;
            }
        }

        private bool built;
        private bool rebuilding;

        private float statusTimer;
        private float propertiesTimer;
        private float treeTimer;

        private ProjectNode selectedNode;
        private RobotController lastActiveRobot;
        private GameObject workLamp;
        private string treeSignature = "";
        private readonly List<Vector3> pointHistory = new List<Vector3>();
        private readonly List<ProjectNode> treeModel = new List<ProjectNode>();

        // Размещение роботов/столов (логика прежняя — перенесена без изменений поведения).
        private RobotController pendingRobotTemplate;
        private Transform pendingTable;
        private bool yawManual;
        private Transform yawTrackedTable;

        // ------------------------------------------------------------------ сохранение геометрии панелей

        private class PanelState
        {
            public KvDockSide Side;
            public float Thickness;
            public bool Collapsed;
            public bool Visible;
            public Vector2 FloatPos = new Vector2(80f, 140f);
        }

        private static readonly Dictionary<string, PanelState> panelStates =
            new Dictionary<string, PanelState>();

        private static PanelState StateOf(string key, KvDockSide side, float thickness, bool visible)
        {
            PanelState s;
            if (!panelStates.TryGetValue(key, out s))
            {
                s = new PanelState();
                s.Side = side;
                s.Thickness = thickness;
                s.Visible = visible;
                panelStates[key] = s;
            }
            return s;
        }

        /// <summary>Фактические состояния панелей (для панели настроек и диагностики).</summary>
        public bool IsPanelVisible(string id)
        {
            switch (id)
            {
                case "tree": return treeDock != null && treeDock.Shown;
                case "properties": return propertiesDock != null && propertiesDock.Shown;
                case "settings": return settingsDock != null && settingsDock.Shown;
                case "hotkeys": return hotkeyDock != null && hotkeyDock.Shown;
                case "metrics": return Flow != null && Flow.Metrics != null && Flow.Metrics.Visible;
                case "limits": return Flow != null && Flow.Workspace != null && Flow.Workspace.JointLimitsVisible;
                default: return false;
            }
        }

        private Camera MainCamera
        {
            get { return Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>(); }
        }

        /// <summary>Поток этапов (владелец лазеров, фантомов, визуализаций).</summary>
        public TrajectoryFlowController Flow
        {
            get
            {
                if (flow == null)
                {
                    Camera cam = MainCamera;
                    if (cam != null) flow = cam.GetComponent<TrajectoryFlowController>();
                }
                return flow;
            }
        }

        /// <summary>Контроллер оператора (WASD, лазеры, фонарик, шарик прицела).</summary>
        public FreeFlyCameraController CameraRig
        {
            get
            {
                if (cameraController == null)
                {
                    Camera cam = MainCamera;
                    if (cam != null) cameraController = cam.GetComponent<FreeFlyCameraController>();
                }
                return cameraController;
            }
        }

        // ------------------------------------------------------------------ статический API (совместимость)

        /// <summary>Показать/скрыть весь интерфейс (TAB).</summary>
        public static void SetUiVisible(bool visible)
        {
            if (Instance == null) return;
            Instance.SetVisibleInternal(visible);
        }

        private static string planStatusText = "";
        private static Color planStatusColor = Color.white;
        private static float planStatusTime = -999f;

        /// <summary>Сообщение от планировщика/метрик (одна строка в статус-баре).</summary>
        public static void SetPlanStatus(string text, Color color)
        {
            planStatusText = text;
            planStatusColor = color;
            planStatusTime = Time.realtimeSinceStartup;
        }

        private static string worldTipText = "";
        private static Vector3 worldTipPoint;
        private static float worldTipTime = -999f;

        /// <summary>Всплывающая подсказка у мировой точки (метрики траектории/фантома).</summary>
        public static void SetTooltip(string text, Vector3 worldPos)
        {
            worldTipText = text;
            worldTipPoint = worldPos;
            worldTipTime = Time.realtimeSinceStartup;
        }

        // ------------------------------------------------------------------ жизненный цикл

        void Awake()
        {
            if (FindObjectsByType<KazistovVvUIManager>(FindObjectsInactive.Include).Length > 1)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            EnsureEventSystem();
            spawner = gameObject.AddComponent<ObjectSpawner>();

            KvSettings.Reload();
            KvTheme.Reload();
            KvTheme.Changed += OnThemeChanged;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            KvTheme.Changed -= OnThemeChanged;
            KvLoc.Changed -= OnLanguageChanged;
            RuntimeRegistry.Changed -= OnRegistryChanged;
            KvCommands.Clear();
        }

        /// <summary>
        /// СМЕНА ЯЗЫКА ИНТЕРФЕЙСА (ЭТАП 2). Применяется МГНОВЕННО и без перезагрузки:
        /// пересобирается оболочка (меню, тулбар, dock-панели, статус-бар, настройки) —
        /// все подписи берутся из словаря заново. Выбор языка уже сохранён в PlayerPrefs
        /// (это делает `KvLoc.SetLanguage`), поэтому он переживает перезапуск PlayMode.
        /// </summary>
        private void OnLanguageChanged()
        {
            if (!built || rebuilding) return;
            ReleaseFonts();
            RebuildShell();
            RebuildTree(true);
            if (status != null) status.SetTheme(KvTheme.ModeLabel);
            KazistovVvFeatures.FeatureHub.LocalizeRefresh();
            Log("Язык интерфейса переключён: " + KvLoc.CurrentName + " (мгновенно, без перезагрузки)");
        }

        /// <summary>
        /// Пересоздать шрифты всех текстов после смены языка (для CJK Unity обязан
        /// пересобрать атлас глифов, иначе останутся «квадраты» от прежнего шрифта).
        /// </summary>
        public static void ReleaseFonts()
        {
            foreach (UnityEngine.UI.Text t in UnityEngine.Object.FindObjectsByType<
                UnityEngine.UI.Text>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                t.font = KvTheme.Font;
                t.SetAllDirty();
            }
        }

        void Start()
        {
            if (built) return;

            // ЭТАП 2 (мультиязычность): язык применяется ДО сборки оболочки, чтобы все
            // подписи строились сразу на выбранном языке, и пересобирается при смене.
            KvLoc.Changed += OnLanguageChanged;
            Log("Язык интерфейса: " + KvLoc.CurrentName + " · " + KvLoc.Status);

            // Б15: на устройстве (Android) словари лежат ВНУТРИ пакета, и обычное чтение файла
            // их не видит. Догружаем их асинхронно и после загрузки пересобираем подписи —
            // на компьютере этот путь не выполняется (папка видна, словари уже прочитаны).
            if (KazistovVvUI.KvLoc.NeedsAsyncFileLoad())
            {
                Log("Словари в пакете: догружаю с устройства (UnityWebRequest)");
                StartCoroutine(KazistovVvUI.KvLoc.LoadFilesAsync());
            }

            BuildShell();

            // ЭТАП 9: роутер геймпада живёт на объекте интерфейса и НЕ пересобирается вместе
            // с оболочкой (клавиши действуют, даже когда панели скрыты). Если геймпада нет —
            // он сам себя выключает, клавиатура и мышь работают как раньше.
            if (gamepadRouter == null) gamepadRouter = gameObject.AddComponent<KvGamepadRouter>();

            Camera cam = MainCamera;
            if (cam != null)
            {
                idleBrain = cam.GetComponent<IdleCameraBrain>();
                if (idleBrain == null) idleBrain = cam.gameObject.AddComponent<IdleCameraBrain>();
            }

            RuntimeRegistry.RebuildFromScene();
            RuntimeRegistry.Changed += OnRegistryChanged;

            if (spawner != null) spawner.RefreshRobotTemplates();
            EnsureWorkLamp();
            BoostLighting();

            ApplySettingsToScene();

            // Старт БЕЗ выбранного робота (правило проекта): ни один узел не активен,
            // ни один робот не подсвечен — оператор выбирает сам.
            SelectNode(null);
            RebuildTree(true);
            WarnAboutRobotCount();

            Log("Интерфейс KazistovVv собран · " + UiVersion + " · тема: " + KvTheme.ModeLabel +
                " · команд: " + KvCommands.All.Count);

            // ФИКС 6/8 (§23): ЭТАЛОН РАЗМЕРОВ проверяется при КАЖДОМ старте интерфейса.
            // В §22 метод ValidateSizes() был написан, но не вызывался ниоткуда; теперь
            // «квадратики», «огромные иконки» и «непонятные +/−» не смогут вернуться незаметно.
            // В консоль попадает одна строка, и только при провале — отчёт целиком.
            int sizeFailures = KvWidgets.ValidateSizesFailures();
            if (sizeFailures > 0)
                Debug.LogWarning("[UI] эталон размеров KvWidgets: провалов " + sizeFailures + "\n" +
                                 KvWidgets.ValidateSizes());
            else
                Log("эталон размеров KvWidgets: 13 проверок, провалов 0");

            // ФИКС 3/4 (§23): состояние курсора печатается один раз на старте — по этой строке
            // проверка «мышь восстанавливается» занимает секунды, без выхода в стартовое меню.
            Log("курсор на старте · " + KvMouseCursor.Describe());
        }

        void Update()
        {
            if (!built || rebuilding) return;

            HandleGlobalHotkeys();
            HandleRobotChooserKeys();
            HandlePlacementInput();
            TrackActiveRobotForProperties();

            float dt = Time.unscaledDeltaTime;

            statusTimer -= dt;
            if (statusTimer <= 0f)
            {
                statusTimer = Mathf.Max(0.02f, statusInterval);
                UpdateStatusBar();
                if (toolbar != null) toolbar.Refresh();
                if (menuBar != null) menuBar.Refresh();
            }

            propertiesTimer -= dt;
            if (propertiesTimer <= 0f)
            {
                propertiesTimer = Mathf.Max(0.05f, propertiesInterval);
                UpdateProperties();
            }

            treeTimer -= dt;
            if (treeTimer <= 0f)
            {
                treeTimer = Mathf.Max(0.05f, treeInterval);
                RebuildTree(false);
            }

            UpdateUiStates();
            UpdateWorldTooltip();
        }

        /// <summary>
        /// ЭТАП 5/7: ГЛОБАЛЬНЫЕ горячие клавиши интерфейса.
        /// Ctrl+P (и Ctrl+Shift+P) — палитра команд, F12 — окно горячих клавиш.
        /// Существующие бинды НЕ меняются: обе клавиши в проекте ранее не использовались.
        /// </summary>
        private void HandleGlobalHotkeys()
        {
            bool ctrl = false;
            bool paletteKey = false;
            bool hotkeysKey = false;
            if (Keyboard.current != null)
            {
                ctrl = Keyboard.current.ctrlKey.isPressed || Keyboard.current.leftCtrlKey.isPressed ||
                       Keyboard.current.rightCtrlKey.isPressed;
                paletteKey = Keyboard.current.pKey.wasPressedThisFrame;
                hotkeysKey = Keyboard.current.f12Key.wasPressedThisFrame;
            }
            else
            {
                try
                {
                    ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                    paletteKey = Input.GetKeyDown(KeyCode.P);
                    hotkeysKey = Input.GetKeyDown(KeyCode.F12);
                }
                catch { }
            }

            if (ctrl && paletteKey) OpenCommandPalette();
            if (hotkeysKey) ToggleHotkeys();
        }

        /// <summary>
        /// ЭТАП 8: состояния интерфейса — полоса загрузки на время планирования/записи
        /// и подсказка пустого состояния, когда ещё ничего не выбрано.
        /// </summary>
        private void UpdateUiStates()
        {
            if (uiStates == null) return;
            TrajectoryFlowController f = Flow;

            bool planning = f != null && f.State.phase == FlowState.PointSelected;
            bool recording = KazistovVvFeatures.FeatureHub.Instance != null &&
                             KazistovVvFeatures.FeatureHub.Instance.Recording != null &&
                             (KazistovVvFeatures.FeatureHub.Instance.Recording.IsRecording ||
                              KazistovVvFeatures.FeatureHub.Instance.Recording.IsPlaying);

            if (planning)
            {
                KvUiStates.Begin(KvLoc.T("loading.planning", "Планирование траекторий"));
            }
            else if (recording)
            {
                KvUiStates.Begin(KvLoc.T("loading.recording", "Запись / воспроизведение"));
            }
            else if (uiStates.Loading)
            {
                KvUiStates.End();
            }

            // Пустое состояние: точка ещё не выбрана — подсказываем, с чего начать (ТЗ ЭТАПА 8).
            bool hasPoint = f != null && f.State.hasPoint;
            if (!hasPoint && (f == null || f.State.phase == FlowState.Idle))
            {
                KvUiStates.SetEmptyHint(
                    KvLoc.T("empty.point", "Выберите точку красным лазером (Z + ЛКМ)"));
            }
            else
            {
                KvUiStates.SetEmptyHint("");
            }
        }

        private void Log(string message)
        {
            if (logUiEvents) Debug.Log("[KazistovVv] " + message);
        }

        // ------------------------------------------------------------------ сборка оболочки

        private void EnsureEventSystem()
        {
            if (EventSystem.current == null)
            {
                GameObject es = new GameObject("EventSystem", typeof(EventSystem));
                es.transform.SetParent(transform, false);
                var module = es.AddComponent<InputSystemUIInputModule>();
                module.AssignDefaultActions();
            }
        }

        private void BuildShell()
        {
            built = true;

            GameObject canvasGo = new GameObject("KazistovVvCanvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            ApplyUiScale();
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            canvasRect = (RectTransform)canvasGo.transform;
            canvasRect.sizeDelta = new Vector2(uiReferenceWidth, uiReferenceHeight);
            // Оболочка могла быть скрыта ДО пересборки (презентационный режим, стартовое
            // меню): новый канвас создаётся активным, поэтому состояние видимости
            // восстанавливается сразу — иначе интерфейс «вылезал» бы поверх меню.
            canvasGo.SetActive(uiVisible);

            RegisterCommands();
            callbacks = BuildCallbacks();

            menuBar = gameObject.AddComponent<KvMenuBar>();
            menuBar.Build(canvasRect, canvas);

            toolbar = gameObject.AddComponent<KvToolbar>();
            float menuH = KvMenuBar.Height;
            // ФИКС 8+9: раскладка тулбара и компактный режим берутся из настроек
            // (PlayerPrefs), поэтому переживают перезапуск и пересборку оболочки.
            toolbar.Build(canvasRect, canvas, ToolbarLayout(), SelectRobotByWorkbench,
                new[] { "Робот (6 осей)", "SCARA" },
                KvSettings.ToolbarLayout, MaxToolbarRows, KvSettings.ToolbarCompact);
            toolbar.Root.anchoredPosition = new Vector2(0f, -menuH);

            treeDock = MakeDock("tree", KvLoc.T("panel.tree", "Дерево моделей"), "tree",
                KvDockSide.Left, treePanelWidth, true);
            tree = gameObject.AddComponent<KvTreeView>();
            tree.Build(canvasRect, canvas, SelectNode, OnNodeVisibility, OnNodeRenamed,
                delegate { RebuildTree(true); });
            // ЭТАП 6: правый клик по узлу → контекстное меню (строит менеджер по типу узла
            // и по всему набору выделенных узлов).
            tree.ContextRequested = OnTreeContextMenu;
            FitPanel(treeDock, tree.Root);

            propertiesDock = MakeDock("properties", KvLoc.T("panel.properties", "Свойства"),
                "properties", KvDockSide.Right,
                propertiesPanelWidth, true);
            properties = gameObject.AddComponent<KvPropertiesView>();
            properties.Build(canvasRect, canvas);
            FitPanel(propertiesDock, properties.Root);

            settingsDock = MakeDock("settings", KvLoc.T("panel.settings", "Настройки и справка"),
                "settings", KvDockSide.Bottom,
                settingsPanelHeight, false);
            settings = gameObject.AddComponent<KvSettingsView>();
            settings.Build(settingsDock.Body, callbacks);

            // ЭТАП 7: ОКНО СПИСКА ГОРЯЧИХ КЛАВИШ — обычная dock-панель (плавающая по умолчанию),
            // поэтому её можно тащить, пристыковать к краю и растянуть за границы/углы (этапы 3–4),
            // а раскладка сохраняется в PlayerPrefs. Открывается по F12 и через «Справка».
            hotkeyDock = MakeDock("hotkeys", KvLoc.T("panel.hotkeys", "Горячие клавиши"),
                "keyboard", KvDockSide.Float, 340f, false);
            hotkeys = gameObject.AddComponent<KvHotkeyView>();
            hotkeys.Build(hotkeyDock.Body);
            if (hotkeyDock.Side == KvDockSide.Float)
                hotkeyDock.SetFloatRect(new Vector2(340f, 300f), new Vector2(430f, 330f));

            status = gameObject.AddComponent<KvStatusBar>();
            float statusH = status.Build(canvasRect, canvas);

            highlight = gameObject.AddComponent<KvSelectionHighlight>();
            highlight.SetTarget(null);

            KvTooltip.Ensure(canvasRect, canvas);

            centerWindow = gameObject.AddComponent<CenterWindow>();
            centerWindow.Build(canvasRect);

            // ЭТАП 5: палитра команд (Ctrl+P) и ЭТАП 8: состояния интерфейса + ЭТАП 9: геймпад.
            palette = KvCommandPalette.Create(canvasRect);
            uiStates = KvUiStates.Create(canvasRect);
            gamepadHud = KvGamepadHud.Create(canvasRect);
            // ЭТАП 11: навигация по интерфейсу с клавиатуры (Tab / стрелки / Enter).
            keyboardNav = KvKeyboardNav.Create(canvasRect, this, toolbar);

            LayoutDock(statusH);
            toolbar.Refresh();
            menuBar.Refresh();
            UpdateStatusBar();
        }

        private KvDockPanel MakeDock(string key, string title, string icon, KvDockSide side,
            float thickness, bool visible)
        {
            PanelState state = StateOf(key, side, thickness, visible);

            // ЭТАП 3: сохранённая раскладка (PlayerPrefs) имеет приоритет над значениями по умолчанию.
            KvPanelLayout saved = KvLayoutStore.Load(key);
            KvDockPanel panel = gameObject.AddComponent<KvDockPanel>();
            panel.Build(canvasRect, canvas, title, icon, side, thickness, key);
            if (saved != null)
            {
                panel.ApplySavedLayout(saved);
                state.Side = panel.Side;
                state.Thickness = panel.Thickness;
                state.Collapsed = panel.Collapsed;
                state.Visible = saved.Visible;
            }
            panel.SetCollapsed(state.Collapsed);
            panel.SetVisible(state.Visible);
            panel.LayoutDirty += delegate { SavePanelState(key, panel); LayoutDock(StatusHeight); };
            panel.LayoutChanged += delegate (string panelKey) { SavePanelState(panelKey, panel); };
            panel.Closed += delegate
            {
                SavePanelState(key, panel);
                Log("панель «" + title + "» скрыта (вернуть — меню «Вид» или панель настроек)");
            };
            return panel;
        }

        private void SavePanelState(string key, KvDockPanel panel)
        {
            PanelState s = StateOf(key, panel.Side, panel.Thickness, panel.Shown);
            s.Side = panel.Side;
            s.Thickness = panel.Thickness;
            s.Collapsed = panel.Collapsed;
            s.Visible = panel.Shown;
            // ЭТАП 3: раскладка пишется в PlayerPrefs — при следующем запуске окна на своих местах.
            if (!string.IsNullOrEmpty(key)) KvLayoutStore.Save(key, panel.CaptureLayout());
        }

        private static void FitPanel(KvDockPanel panel, RectTransform content)
        {
            if (panel == null || content == null) return;
            content.SetParent(panel.Body, false);
            KvTheme.Stretch(content);
        }

        private float StatusHeight { get { return KvStatusBar.Height; } }

        /// <summary>Разложить dock-панели (единая точка правды по геометрии).</summary>
        public void LayoutDock(float statusHeight)
        {
            if (canvasRect == null) return;
            float top = KvMenuBar.Height + (toolbar != null ? toolbar.Height : 0f);

            float leftInset = 0f;
            float rightInset = 0f;

            if (treeDock != null)
            {
                treeDock.ApplyLayout(top, statusHeight, 0f, 0f);
                if (treeDock.Shown && treeDock.Side == KvDockSide.Left)
                    leftInset = PanelExtent(treeDock);
            }
            if (propertiesDock != null)
            {
                propertiesDock.ApplyLayout(top, statusHeight, 0f, 0f);
                if (propertiesDock.Shown && propertiesDock.Side == KvDockSide.Right)
                    rightInset = PanelExtent(propertiesDock);
            }
            if (settingsDock != null)
            {
                settingsDock.ApplyLayout(top, statusHeight, leftInset, rightInset);
                if (settingsDock.Shown && settingsDock.Side == KvDockSide.Left)
                    leftInset += PanelExtent(settingsDock);
                if (settingsDock.Shown && settingsDock.Side == KvDockSide.Right)
                    rightInset += PanelExtent(settingsDock);
            }
            // ЭТАП 7: плавающее окно горячих клавиш раскладывается тем же методом
            // (для Float он только клампит позицию/размер в пределах канваса).
            if (hotkeyDock != null)
            {
                hotkeyDock.ApplyLayout(top, statusHeight, leftInset, rightInset);
                if (hotkeyDock.Shown && hotkeyDock.Side == KvDockSide.Left)
                    leftInset += PanelExtent(hotkeyDock);
                if (hotkeyDock.Shown && hotkeyDock.Side == KvDockSide.Right)
                    rightInset += PanelExtent(hotkeyDock);
            }

            // Панели, пристыкованные к «нижнему» краю, поднимаются над статус-баром.
            if (treeDock != null && treeDock.Side == KvDockSide.Bottom)
                treeDock.ApplyLayout(top, statusHeight, 0f, rightInset);
            if (propertiesDock != null && propertiesDock.Side == KvDockSide.Bottom)
                propertiesDock.ApplyLayout(top, statusHeight, leftInset, 0f);
        }

        /// <summary>Сколько места занимает панель по толщине (полочка/свёрнутая — меньше).</summary>
        private static float PanelExtent(KvDockPanel panel)
        {
            if (panel.InRail) return KvDockPanel.RailThickness;
            return panel.Collapsed ? KvDockPanel.CollapsedHeight : panel.Thickness;
        }

        private void ApplyUiScale()
        {
            if (scaler == null) return;
            float scale = Mathf.Clamp(KvSettings.UiScale, 0.6f, 1.6f);
            scaler.referenceResolution = new Vector2(uiReferenceWidth / scale,
                uiReferenceHeight / scale);
        }

        // ------------------------------------------------------------------ тема

        private void OnThemeChanged()
        {
            // Мгновенное применение: оболочка пересобирается, состояние панелей сохраняется.
            RebuildShell();
        }

        /// <summary>Пересобрать оболочку (смена темы/масштаба/плотности) — без перезагрузки.</summary>
        public void RebuildShell()
        {
            if (rebuilding || !built) return;
            rebuilding = true;

            if (canvas != null) Destroy(canvas.gameObject);
            if (menuBar != null) Destroy(menuBar);
            if (toolbar != null) Destroy(toolbar);
            if (tree != null) Destroy(tree);
            if (properties != null) Destroy(properties);
            if (settings != null) Destroy(settings);
            if (status != null) Destroy(status);
            if (highlight != null) Destroy(highlight);
            if (treeDock != null) Destroy(treeDock);
            if (propertiesDock != null) Destroy(propertiesDock);
            if (settingsDock != null) Destroy(settingsDock);
            if (hotkeyDock != null) Destroy(hotkeyDock);
            if (hotkeys != null) Destroy(hotkeys);

            canvas = null;
            menuBar = null;
            toolbar = null;
            tree = null;
            properties = null;
            settings = null;
            status = null;
            highlight = null;
            treeDock = null;
            propertiesDock = null;
            settingsDock = null;
            hotkeyDock = null;
            hotkeys = null;
            palette = null;
            uiStates = null;
            gamepadHud = null;
            keyboardNav = null;
            worldTipVisual = null;
            worldTipBg = null;

            built = false;
            BuildShell();
            built = true;
            rebuilding = false;

            if (status != null) status.SetTheme(KvTheme.ModeLabel);
            RebuildTree(true);
            Log("Оболочка пересобрана · тема: " + KvTheme.ModeLabel + " · масштаб: " +
                KvSettings.UiScale.ToString("0.00") + " · плотность: " + KvSettings.Density +
                " · тулбар: " + ToolbarLayoutLabel(KvSettings.ToolbarLayout) +
                (KvSettings.ToolbarCompact ? " (компактный)" : ""));
        }

        /// <summary>Подпись режима раскладки тулбара для журнала (ФИКС 8).</summary>
        private static string ToolbarLayoutLabel(int layout)
        {
            switch (layout)
            {
                case 1: return "широко";
                case 2: return "5 в ряд";
                default: return "авто";
            }
        }

        /// <summary>Переключить тему (кнопка/меню/настройки).</summary>
        public void CycleTheme()
        {
            KvThemeMode mode = KvTheme.Cycle();
            Log("Тема: " + KvTheme.ModeLabel + " (палитра " + KvTheme.PaletteLabel + ")");
            if (status != null) status.SetTheme(KvTheme.ModeLabel);
            if (settings != null) settings.RefreshValues();
        }

        // ------------------------------------------------------------------ команды

        /// <summary>
        /// Раскладка тулбара (ЭТАП 1): список id СОБИРАЕТСЯ ИЗ ГРУПП
        /// (<see cref="KvToolbarGroups.All"/>) — группы и есть раскладка, поэтому новая
        /// кнопка добавляется в одном месте (в группе), а порядок на панели и порядок
        /// в подсказках/меню группы всегда совпадают. Каждая группа рисуется отдельным
        /// блоком с вертикальной линией-разделителем; неизвестные команды игнорируются,
        /// а команды, не попавшие ни в одну группу, уходят в «Прочее».
        /// </summary>
        private static string[] ToolbarLayout()
        {
            List<string> ids = new List<string>();
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
            {
                if (group == null || group.Commands == null) continue;
                foreach (string id in group.Commands)
                {
                    if (string.IsNullOrEmpty(id) || ids.Contains(id)) continue;
                    ids.Add(id);
                }
            }
            return ids.ToArray();
        }

        private void RegisterCommands()
        {
            KvCommands.Clear();

            // --- Робот/инструменты
            KvCommands.Register(new KvCommand
            {
                Id = "point.select",
                Title = "Выбор точки (красный лазер)",
                Description = "Включает красную указку: ЛКМ фиксирует точку и запускает расчёт траекторий",
                Hotkey = "Z",
                Icon = "point",
                MenuPath = "Робот/Инструменты/Выбор точки (красный лазер)",
                Execute = delegate
                {
                    FreeFlyCameraController cam = CameraRig;
                    if (cam == null) return;
                    cam.leftHandEnabled = !cam.leftHandEnabled;
                    Log("Красный лазер: " + (cam.leftHandEnabled ? "включён" : "выключен"));
                },
                IsChecked = delegate { return CameraRig != null && CameraRig.leftHandEnabled; },
                CheckedTint = delegate { return KvTheme.LaserRed; }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "path.select",
                Title = "Выбор траектории (зелёный лазер)",
                Description = "Включает зелёную указку: ЛКМ выбирает траекторию/фантом",
                Hotkey = "X",
                Icon = "path",
                MenuPath = "Робот/Инструменты/Выбор траектории (зелёный лазер)",
                Execute = delegate
                {
                    FreeFlyCameraController cam = CameraRig;
                    if (cam == null) return;
                    cam.rightHandEnabled = !cam.rightHandEnabled;
                    Log("Зелёный лазер: " + (cam.rightHandEnabled ? "включён" : "выключен"));
                },
                IsChecked = delegate { return CameraRig != null && CameraRig.rightHandEnabled; },
                CheckedTint = delegate { return KvTheme.LaserGreen; }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "robot.playpause",
                Title = "Запуск / пауза",
                Description = "Пауза и продолжение движения робота на шаге 5 (траектория не сбрасывается)",
                Hotkey = "",
                Icon = "pause",
                IconChecked = "play",
                MenuPath = "Робот/Движение/Запуск / пауза",
                Execute = delegate
                {
                    TrajectoryFlowController f = Flow;
                    if (f == null || f.Motion == null) return;
                    bool paused = f.Motion.Paused;
                    f.Motion.SetPaused(!paused);
                    Log(paused ? "Движение робота продолжено" : "Движение робота поставлено на паузу");
                },
                IsChecked = delegate
                {
                    TrajectoryFlowController f = Flow;
                    return f != null && f.Motion != null && f.Motion.Paused;
                },
                IsEnabled = delegate
                {
                    TrajectoryFlowController f = Flow;
                    return f != null && f.Motion != null && f.Motion.IsRunning;
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "robot.stop",
                Title = "Остановка",
                Description = "Немедленно останавливает движение робота (штатный стоп оператора)",
                Hotkey = "",
                Icon = "stop",
                MenuPath = "Робот/Движение/Остановка",
                Execute = delegate
                {
                    TrajectoryFlowController f = Flow;
                    if (f == null || f.Motion == null) return;
                    f.Motion.Stop();
                    Log("Остановка робота (кнопка интерфейса)");
                },
                IsEnabled = delegate
                {
                    TrajectoryFlowController f = Flow;
                    return f != null && f.Motion != null && f.Motion.IsRunning;
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "edit.reset",
                Title = "Сброс сценария",
                Description = "То же, что Esc: точка, траектории, фантомы и движение очищаются",
                Hotkey = "Esc",
                Icon = "reset",
                MenuPath = "Правка/Сброс сценария",
                Execute = delegate
                {
                    TrajectoryFlowController f = Flow;
                    if (f == null) return;
                    f.ResetFlow("Сброшено (кнопка интерфейса)");
                    pointHistory.Clear();
                    Log("Сценарий сброшен кнопкой интерфейса");
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "robot.switch",
                Title = "Переключение робота (робот / SCARA)",
                Description = "Делает активным второго робота сцены (как F по прицелу)",
                Hotkey = "F",
                Icon = "swap",
                MenuPath = "Робот/Переключить робота",
                Execute = delegate { SwitchRobot(); },
                IsEnabled = delegate { return CountRobots() > 1; }
            });

            // --- Вид/визуализации
            KvCommands.Register(new KvCommand
            {
                Id = "view.workspace",
                Title = "Зона достижимости",
                Description = "Купол у робота и кольцо у SCARA; граница рабочей зоны оракула",
                Hotkey = "",
                Icon = "workspace",
                MenuPath = "Вид/Визуализации/Зона достижимости",
                Execute = delegate
                {
                    bool value = !KvSettings.ShowWorkspace;
                    KvSettings.ShowWorkspace = value;
                    ApplyVisualization("vis.workspace", value);
                },
                IsChecked = delegate { return CurrentWorkspace(); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "view.limits",
                Title = "Лимиты суставов",
                Description = "Кольца у суставов (зелёный/жёлтый/красный) и панель «угол / лимит / запас»",
                Hotkey = "",
                Icon = "limits",
                MenuPath = "Вид/Визуализации/Лимиты суставов",
                Execute = delegate
                {
                    bool value = !KvSettings.ShowJointLimits;
                    KvSettings.ShowJointLimits = value;
                    ApplyVisualization("vis.limits", value);
                },
                IsChecked = delegate { return CurrentLimits(); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "view.metrics",
                Title = "Панель метрик траекторий",
                Description = "Метрики 8 траекторий: длина, время, кривизна, зазор, запас лимитов",
                Hotkey = "",
                Icon = "metrics",
                MenuPath = "Вид/Визуализации/Метрики траекторий",
                Execute = delegate
                {
                    bool value = !KvSettings.ShowMetrics;
                    KvSettings.ShowMetrics = value;
                    ApplyVisualization("vis.metrics", value);
                },
                IsChecked = delegate { return CurrentMetrics(); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "tool.flashlight",
                Title = "Фонарик",
                Description = "Освещает рабочую зону (те же параметры света, что у бинда G)",
                Hotkey = "G",
                Icon = "flashlight",
                MenuPath = "Вид/Фонарик",
                Execute = delegate
                {
                    FreeFlyCameraController cam = CameraRig;
                    if (cam == null) return;
                    bool on = !cam.FlashlightOn;
                    cam.SetFlashlight(on);
                    KvSettings.Flashlight = on;
                    Log("Фонарик: " + (on ? "включён" : "выключен"));
                },
                IsChecked = delegate { return CameraRig != null && CameraRig.FlashlightOn; }
            });

            // --- Панели
            KvCommands.Register(new KvCommand
            {
                Id = "view.panel.tree",
                Title = "Дерево моделей",
                Description = "Панель слева: иерархия сцены (роботы, оси, столы, точки, траектории)",
                Hotkey = "",
                Icon = "tree",
                MenuPath = "Вид/Панели/Дерево моделей",
                Execute = delegate { TogglePanel("tree"); },
                IsChecked = delegate { return IsPanelVisible("tree"); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "view.panel.properties",
                Title = "Свойства",
                Description = "Панель справа: свойства выбранного объекта (только чтение)",
                Hotkey = "",
                Icon = "properties",
                MenuPath = "Вид/Панели/Свойства",
                Execute = delegate { TogglePanel("properties"); },
                IsChecked = delegate { return IsPanelVisible("properties"); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "view.panel.settings",
                Title = "Настройки и справка",
                Description = "Нижняя dock-панель: функции, управление, интерфейс, о программе, справка",
                Hotkey = "",
                Icon = "settings",
                MenuPath = "Вид/Панели/Настройки и справка",
                Execute = delegate { TogglePanel("settings"); },
                IsChecked = delegate { return IsPanelVisible("settings"); }
            });

            // --- Тема, сервис, справка
            KvCommands.Register(new KvCommand
            {
                Id = "view.theme",
                Title = "Тема: тёмная / светлая / системная",
                Description = "Переключает тему по кругу (мгновенно, выбор хранится в PlayerPrefs)",
                Hotkey = "",
                Icon = "theme-dark",
                MenuPath = "Вид/Тема",
                Execute = delegate { CycleTheme(); }
            });

            // --- ЭТАП 2: язык интерфейса (мгновенное переключение, выбор в PlayerPrefs)
            KvCommands.Register(new KvCommand
            {
                Id = "lang.cycle",
                Title = "Язык интерфейса",
                Description = "Переключить язык: RU / EN / ZH / ES / DE / FR / JA",
                Hotkey = "",
                Icon = "lang",
                MenuPath = "Сервис/Настройки/Язык интерфейса",
                Execute = delegate
                {
                    string code = KvLoc.Cycle();
                    Log("язык интерфейса: " + KvLoc.CurrentName + " (" + code + ")");
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "ui.settings",
                Title = "Настройки",
                Description = "Открывает панель настроек (вкладка «Функции»)",
                Hotkey = "",
                Icon = "settings",
                MenuPath = "Сервис/Настройки",
                Execute = delegate { ShowSettings(0); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "help.open",
                Title = "Помощь",
                Description = "Справка: шаги алгоритма, горячие клавиши, приёмы работы с панелями",
                Hotkey = "",
                Icon = "help",
                MenuPath = "Справка/Помощь",
                Execute = delegate { ShowSettings(4); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "help.about",
                Title = "О программе",
                Description = "Версия интерфейса, модули, среда",
                Hotkey = "",
                Icon = "info",
                MenuPath = "Справка/О программе",
                Execute = delegate { ShowSettings(3); }
            });

            // --- ЭТАП 5: палитра команд
            KvCommands.Register(new KvCommand
            {
                Id = "ui.palette",
                Title = "Палитра команд",
                Description = "Поиск по ВСЕМ командам интерфейса: тулбар, меню, настройки, скрытые функции",
                Hotkey = "Ctrl+P",
                Icon = "palette",
                MenuPath = "Вид/Палитра команд",
                Execute = delegate { OpenCommandPalette(); }
            });

            // --- ЭТАП 7: окно горячих клавиш
            KvCommands.Register(new KvCommand
            {
                Id = "help.hotkeys",
                Title = "Горячие клавиши",
                Description = "Окно со всеми биндами (клавиатура, мышь, геймпад), поиском и подсветкой конфликтов",
                Hotkey = "F12",
                Icon = "keyboard",
                MenuPath = "Справка/Горячие клавиши",
                Execute = delegate { ToggleHotkeys(); }
            });

            // --- ЭТАП 3: сброс раскладки окон
            KvCommands.Register(new KvCommand
            {
                Id = "ui.resetlayout",
                Title = "Сбросить раскладку окон",
                Description = "Вернуть панели и группы тулбара к расположению по умолчанию",
                Hotkey = "",
                Icon = "layout",
                MenuPath = "Вид/Панели/Сбросить раскладку",
                Execute = delegate { ResetLayout(); }
            });

            // --- ЭТАП 11: доступность
            KvCommands.Register(new KvCommand
            {
                Id = "ui.access",
                Title = "Доступность",
                Description = "Размер шрифта, высокий контраст, схема для дальтоников, навигация с клавиатуры",
                Hotkey = "",
                Icon = "contrast",
                MenuPath = "Сервис/Настройки/Доступность",
                Execute = delegate { ShowSettings(2); }
            });

            // --- ЭТАП 8: показать/скрыть интерфейс (было «то же, что TAB» — см. ФИКС 8 §22:
            // теперь это отдельная команда, а режим курсора переключает клавиша TAB —
            // §26: TAB = режим камеры ⟷ режим интерфейса; CAPS LOCK не делает ничего)
            KvCommands.Register(new KvCommand
            {
                Id = "ui.toggle",
                Title = "Показать / скрыть интерфейс",
                Description = "Убрать панели и вернуть управление мышью в сцену " +
                              "(режим курсора переключает TAB: камера ⟷ интерфейс; из режима " +
                              "камеры то же делает ESC)",
                Hotkey = "—",
                Icon = "layers",
                MenuPath = "Вид/Показать-скрыть интерфейс",
                Execute = delegate
                {
                    // ФИКС 7/10 (§22): вместе с панелями переключается и РЕЖИМ КУРСОРА —
                    // иначе скрытая оболочка оставляла мышь «мёртвой», а показанная не давала
                    // по себе кликать. Один источник правды — KvMouseCursor.
                    if (uiVisible) KvMouseCursor.Capture("команда: скрыть интерфейс");
                    else KvMouseCursor.Release("команда: показать интерфейс (системный курсор)");
                },
                IsChecked = delegate { return uiVisible; }
            });

            // --- ЭТАП 9: индикатор геймпада
            KvCommands.Register(new KvCommand
            {
                Id = "gamepad.hud",
                Title = "Виртуальный геймпад",
                Description = "Мини-индикатор на экране: стики, триггеры и нажатые кнопки геймпада",
                Hotkey = "",
                Icon = "gamepad",
                MenuPath = "Сервис/Настройки/Виртуальный геймпад",
                Execute = delegate
                {
                    bool value = !KvSettings.GamepadHud;
                    KvSettings.GamepadHud = value;
                    ApplyVisualization("ui.gamepadhud", value);
                },
                IsChecked = delegate { return KvSettings.GamepadHud; }
            });

            // --- Заглушки (кнопки есть, функционала пока нет)
            KvCommands.Register(new KvCommand
            {
                Id = "edit.undo",
                Title = "Отменить",
                Description = "Отмена последнего действия (история пока не ведётся)",
                Hotkey = "Ctrl+Z",
                Icon = "undo",
                MenuPath = "Правка/Отменить",
                Stub = true
            });

            KvCommands.Register(new KvCommand
            {
                Id = "edit.redo",
                Title = "Вернуть",
                Description = "Повтор отменённого действия (история пока не ведётся)",
                Hotkey = "Ctrl+Y",
                Icon = "redo",
                MenuPath = "Правка/Вернуть",
                Stub = true
            });

            KvCommands.Register(new KvCommand
            {
                Id = "file.export",
                Title = "Экспорт сценария",
                Description = "Сохранение точек и траекторий в файл (пункт задел)",
                Hotkey = "",
                Icon = "layers",
                MenuPath = "Файл/Экспорт сценария",
                Stub = true
            });

            KvCommands.Register(new KvCommand
            {
                Id = "file.import",
                Title = "Импорт сценария",
                Description = "Загрузка точек и траекторий из файла (пункт задел)",
                Hotkey = "",
                Icon = "layers",
                MenuPath = "Файл/Импорт сценария",
                Stub = true
            });

            KvCommands.Register(new KvCommand
            {
                Id = "tool.measure",
                Title = "Измерение",
                Description = "Линейка/замер расстояний в сцене (пункт задел)",
                Hotkey = "",
                Icon = "curve",
                MenuPath = "Сервис/Инструменты/Измерение",
                Stub = true
            });

            KvCommands.Register(new KvCommand
            {
                Id = "tool.grid",
                Title = "Сетка рабочей зоны",
                Description = "Плоская сетка на столешнице (пункт задел)",
                Hotkey = "",
                Icon = "workspace",
                MenuPath = "Сервис/Инструменты/Сетка рабочей зоны",
                Stub = true
            });

            // --- НОВЫЕ ФУНКЦИИ (этапы 1–20 ТЗ). Регистрируются ПОСЛЕ штатных команд,
            //     поэтому перекрывают заглушки «Отменить»/«Вернуть»/«Экспорт» реальными действиями.
            KazistovVvFeatures.FeatureHub.RegisterCommands(this);
            // --- функции ЭТАПОВ 1–8 этой сессии (экспорт, сингулярности, waypoints,
            //     мониторинг состояния, динамические препятствия, виртуальный пульт).
            KazistovVvFeatures.KvStageHub.RegisterCommands(this);
            // --- функции ЭТАПОВ 1–6 текущей сессии (главное меню, туториал, демонстрация,
            //     сглаживание траекторий, время-оптимальная траектория, эко-профиль).
            KazistovVvFeatures.KvStageHub2.RegisterCommands(this);
            // --- функции ЭТАПОВ 7–12 (ограничения waypoints, ограниченное планирование,
            //     калибровка, нагрузка, экспорт в языки роботов, импорт моделей).
            KazistovVvFeatures.KvStageHub3.RegisterCommands(this);
            // --- функции ЭТАПОВ 13–36 (прокси столкновений, стенд планировщиков, дерево RRT,
            //     камеры и PiP, силы и тепло, PDF-отчёт, сеть и дашборд, XR-ввод, макросы и
            //     деревья поведения, окружение и свет, материалы, кинорежим, титры, голос
            //     диктора, имитация отказов, проверка перед пуском, уровни журнала).
            KazistovVvFeatures.KvStageHub4.RegisterCommands(this);
        }

        private int CountRobots()
        {
            int count = 0;
            foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include))
            {
                if (rc == null || RuntimeRegistry.IsHidden(rc.gameObject)) continue;
                count++;
            }
            return count;
        }

        // ------------------------------------------------------------------ настройки/колбэки

        private KvSettingsCallbacks BuildCallbacks()
        {
            KvSettingsCallbacks c = new KvSettingsCallbacks();
            c.HasFeature = HasFeature;
            c.GetFeature = GetFeature;
            c.SetFeature = ApplyVisualization;
            c.SetTheme = delegate (KvThemeMode mode)
            {
                KvTheme.SetMode(mode);
                if (status != null) status.SetTheme(KvTheme.ModeLabel);
            };
            c.SetScale = delegate (float scale)
            {
                KvSettings.UiScale = scale;
                RebuildShell();
            };
            c.SetDensity = delegate (int density)
            {
                KvSettings.Density = density;
                RebuildShell();
            };
            c.SetToolbarRows = delegate (int rows)
            {
                KvSettings.ToolbarRows = rows;
                RebuildShell();
            };
            // ФИКС 8: раскладка тулбара (0 — авто, 1 — широко, 2 — «5 в ряд») —
            // мгновенно, через пересборку оболочки, как масштаб и плотность.
            c.SetToolbarLayout = delegate (int layout)
            {
                KvSettings.ToolbarLayout = layout;
                RebuildShell();
            };
            // ФИКС 9: компактный тулбар (мелкие иконки и без «редких» кнопок).
            c.SetToolbarCompact = delegate (bool compact)
            {
                KvSettings.ToolbarCompact = compact;
                RebuildShell();
            };
            c.SetPanelVisible = delegate (string id, bool visible) { SetPanel(id, visible); };
            c.GetPanelVisible = IsPanelVisible;

            // --- ЭТАП 11: доступность (размер шрифта, контраст, схема для дальтоников, клавиатура)
            c.SetFontSize = delegate (int size)
            {
                KvSettings.FontSize = size;
                ReleaseFonts();
                RebuildShell();
            };
            c.SetHighContrast = delegate (bool value)
            {
                KvSettings.HighContrast = value;
                RebuildShell();
            };
            c.SetColorBlind = delegate (int mode)
            {
                KvSettings.ColorBlind = mode;
                RebuildShell();
            };
            c.SetKeyboardNav = delegate (bool value)
            {
                KvSettings.KeyboardNav = value;
                if (keyboardNav != null) keyboardNav.SetEnabled(value);
            };
            c.SetGamepadHud = delegate (bool value)
            {
                KvSettings.GamepadHud = value;
            };

            // --- ЭТАПЫ 3/5/7: сервисные действия панели настроек
            c.OpenPalette = OpenCommandPalette;
            c.OpenHotkeys = delegate { ShowHotkeys(true); };
            c.ResetLayout = ResetLayout;

            c.ResetAll = delegate
            {
                KvSettings.ResetToDefaults();
                KvTheme.SetMode(KvThemeMode.Dark);
                ApplySettingsToScene();
                RebuildShell();
                Log("Настройки интерфейса сброшены к значениям по умолчанию");
            };
            return c;
        }

        /// <summary>Есть ли у функции-переключателя обработчик (иначе — «в разработке»).</summary>
        private static bool HasFeature(string id)
        {
            // Сначала спрашиваем хабы новых функций (свои id), затем штатные функции.
            if (KazistovVvFeatures.KvStageHub.HasFeature(id)) return true;
            if (KazistovVvFeatures.KvStageHub2.HasFeature(id)) return true;
            if (KazistovVvFeatures.KvStageHub3.HasFeature(id)) return true;
            if (KazistovVvFeatures.KvStageHub4.HasFeature(id)) return true;
            switch (id)
            {
                case "vis.workspace":
                case "vis.limits":
                case "vis.metrics":
                case "tool.flashlight":
                case "tree.autorefresh":
                case "scene.phantoms":
                case "ui.toolbar.layout":
                case "ui.toolbar.compact":
                case "ui.gamepadhud":
                    return true;
                default:
                    return false;
            }
        }

        private bool GetFeature(string id)
        {
            bool stageHandled;
            bool stageValue = KazistovVvFeatures.KvStageHub.GetFeature(id, out stageHandled);
            if (stageHandled) return stageValue;
            bool stage2Handled;
            bool stage2Value = KazistovVvFeatures.KvStageHub2.GetFeature(id, out stage2Handled);
            if (stage2Handled) return stage2Value;
            bool stage3Handled;
            bool stage3Value = KazistovVvFeatures.KvStageHub3.GetFeature(id, out stage3Handled);
            if (stage3Handled) return stage3Value;
            bool stage4Handled;
            bool stage4Value = KazistovVvFeatures.KvStageHub4.GetFeature(id, out stage4Handled);
            if (stage4Handled) return stage4Value;
            switch (id)
            {
                case "vis.workspace": return CurrentWorkspace();
                case "vis.limits": return CurrentLimits();
                case "vis.metrics": return CurrentMetrics();
                case "tool.flashlight": return CameraRig != null && CameraRig.FlashlightOn;
                case "tree.autorefresh": return autoRebuildTree;
                case "ui.gamepadhud": return KvSettings.GamepadHud;
                default: return KvSettings.ShowPhantoms;
            }
        }

        private bool CurrentWorkspace()
        {
            TrajectoryFlowController f = Flow;
            return f != null && f.Workspace != null ? f.Workspace.WorkspaceVisible : KvSettings.ShowWorkspace;
        }

        private bool CurrentLimits()
        {
            TrajectoryFlowController f = Flow;
            return f != null && f.Workspace != null ? f.Workspace.JointLimitsVisible : KvSettings.ShowJointLimits;
        }

        private bool CurrentMetrics()
        {
            TrajectoryFlowController f = Flow;
            return f != null && f.Metrics != null ? f.Metrics.Visible : KvSettings.ShowMetrics;
        }

        /// <summary>Применить переключатель функции (панель настроек и кнопки тулбара).</summary>
        private void ApplyVisualization(string id, bool value)
        {
            TrajectoryFlowController f = Flow;
            // Переключатели этапов 1–8 (сингулярности, препятствия, состояние, пульт…).
            if (KazistovVvFeatures.KvStageHub.SetFeature(id, value))
            {
                Log("Настройка «" + id + "»: " + (value ? "включена" : "выключена"));
                if (toolbar != null) toolbar.Refresh();
                if (settings != null) settings.RefreshValues();
                return;
            }
            // Переключатели этапов 1–6 этой сессии (меню при запуске, автосглаживание, верстак).
            if (KazistovVvFeatures.KvStageHub2.SetFeature(id, value))
            {
                Log("Настройка «" + id + "»: " + (value ? "включена" : "выключена"));
                if (toolbar != null) toolbar.Refresh();
                if (settings != null) settings.RefreshValues();
                return;
            }
            if (KazistovVvFeatures.KvStageHub3.SetFeature(id, value))
            {
                Log("Настройка «" + id + "»: " + (value ? "включена" : "выключена"));
                if (toolbar != null) toolbar.Refresh();
                if (settings != null) settings.RefreshValues();
                return;
            }
            if (KazistovVvFeatures.KvStageHub4.SetFeature(id, value))
            {
                Log("Настройка «" + id + "»: " + (value ? "включена" : "выключена"));
                if (toolbar != null) toolbar.Refresh();
                if (settings != null) settings.RefreshValues();
                return;
            }
            switch (id)
            {
                case "vis.workspace":
                    KvSettings.ShowWorkspace = value;
                    if (f != null) f.SetWorkspaceVisible(value);
                    break;
                case "vis.limits":
                    KvSettings.ShowJointLimits = value;
                    if (f != null) f.SetJointLimitsVisible(value);
                    break;
                case "vis.metrics":
                    KvSettings.ShowMetrics = value;
                    if (f != null) f.SetMetricsPanelVisible(value);
                    break;
                case "tool.flashlight":
                    KvSettings.Flashlight = value;
                    if (CameraRig != null) CameraRig.SetFlashlight(value);
                    break;
                case "tree.autorefresh":
                    autoRebuildTree = value;
                    break;
                // ЭТАП 9: индикатор геймпада (настройка «Виртуальный геймпад»)
                case "ui.gamepadhud":
                    break;
                case "scene.phantoms":
                    // ИСПРАВЛЕНО 18.09.2026: пункт «Фантомы» был объявлен в настройках, но
                    // обработчика не имел (в «Справке» так и было написано). Теперь он реально
                    // управляет показом: при выключении созданные копии убираются, при
                    // включении новые создаются как обычно.
                    KvSettings.ShowPhantoms = value;
                    if (f != null && f.Phantoms != null && !value) f.Phantoms.Hide();
                    break;
                default:
                    Log("Пункт настроек «" + id + "» пока не имеет обработчика (структура готова)");
                    return;
            }
            Log("Настройка «" + id + "»: " + (value ? "включена" : "выключена"));
            if (toolbar != null) toolbar.Refresh();
            if (settings != null) settings.RefreshValues();
        }

        /// <summary>Применить сохранённые настройки к сцене (старт и сброс).</summary>
        private void ApplySettingsToScene()
        {
            TrajectoryFlowController f = Flow;
            if (f != null)
            {
                f.SetWorkspaceVisible(KvSettings.ShowWorkspace);
                f.SetJointLimitsVisible(KvSettings.ShowJointLimits);
                f.SetMetricsPanelVisible(KvSettings.ShowMetrics);
            }
            FreeFlyCameraController cam = CameraRig;
            if (cam != null) cam.SetFlashlight(KvSettings.Flashlight);
            autoRebuildTree = KvSettings.AutoRefreshTree;
        }

        // ------------------------------------------------------------------ панели/окна

        /// <summary>Показать/скрыть dock-панель по id.</summary>
        public void SetPanel(string id, bool visible)
        {
            switch (id)
            {
                case "tree":
                    if (treeDock != null) treeDock.SetVisible(visible);
                    break;
                case "properties":
                    if (propertiesDock != null) propertiesDock.SetVisible(visible);
                    break;
                case "settings":
                    if (settingsDock != null) settingsDock.SetVisible(visible);
                    break;
                case "hotkeys":
                    if (hotkeyDock != null) hotkeyDock.SetVisible(visible);
                    break;
                case "metrics":
                    ApplyVisualization("vis.metrics", visible);
                    return;
                case "limits":
                    ApplyVisualization("vis.limits", visible);
                    return;
            }
            if (treeDock != null) SavePanelState("tree", treeDock);
            if (propertiesDock != null) SavePanelState("properties", propertiesDock);
            if (settingsDock != null) SavePanelState("settings", settingsDock);
            if (hotkeyDock != null) SavePanelState("hotkeys", hotkeyDock);
            LayoutDock(StatusHeight);
        }

        /// <summary>Переключить панель.</summary>
        public void TogglePanel(string id)
        {
            SetPanel(id, !IsPanelVisible(id));
        }

        /// <summary>Открыть панель настроек на вкладке (0 функции … 4 справка).</summary>
        public void ShowSettings(int tab)
        {
            // ФИКС 10 (§22): «настройки не открываются» — потому что команда лишь включала
            // панель, а САМА ОБОЛОЧКА могла быть скрыта (телеоперация или открытое стартовое
            // меню). Канвас выключен → панель «включалась» в никуда, и клик по ней был
            // невозможен. Теперь открытие настроек принудительно показывает оболочку
            // и освобождает курсор (панель обязана быть кликабельной).
            if (!uiVisible) SetVisibleInternal(true);
            if (settingsDock != null) settingsDock.SetVisible(true);
            if (settings != null) settings.SetTab(tab);
            LayoutDock(StatusHeight);
            KvMouseCursor.Release("открытие настроек (вкладка " + tab + ")");
        }

        /// <summary>
        /// Прямоугольник кнопки тулбара по id команды (ТОЛЬКО ЧТЕНИЕ). Нужен подсветке
        /// туториала (этап 2): рамка обводит САМУ кнопку, интерфейс при этом не
        /// перестраивается и не дублируется. null — кнопки с таким id в тулбаре нет
        /// или оболочка ещё не собрана.
        /// </summary>
        public RectTransform CommandButtonRect(string commandId)
        {
            if (toolbar == null || string.IsNullOrEmpty(commandId)) return null;
            IReadOnlyList<string> ids = toolbar.ButtonIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != commandId) continue;
                KvIconButton button = toolbar.ButtonAt(i);
                return button != null ? button.transform as RectTransform : null;
            }
            return null;
        }

        private void SetVisibleInternal(bool visible)
        {
            uiVisible = visible;
            if (canvas != null) canvas.gameObject.SetActive(visible);
        }

        // ------------------------------------------------------------------ дерево моделей

        private void OnRegistryChanged()
        {
            EnsureWorkLamp();
            BoostLighting();
            RebuildTree(true);
        }

        /// <summary>
        /// Пересборка дерева. При <paramref name="force"/>=false пересборка идёт только
        /// если «подпись» содержимого изменилась — это и есть защита от лишней работы в кадре.
        /// </summary>
        public void RebuildTree(bool force)
        {
            if (tree == null) return;
            if (!force && !autoRebuildTree) return;

            string signature = TreeSignature();
            if (!force && signature == treeSignature) return;
            treeSignature = signature;

            List<ProjectNode> roots = BuildTreeModel();
            treeModel.Clear();
            treeModel.AddRange(roots);
            tree.Rebuild(roots, selectedNode);
        }

        /// <summary>Текущая модель дерева (для диагностики/расширений).</summary>
        public IReadOnlyList<ProjectNode> TreeModel { get { return treeModel; } }

        private string TreeSignature()
        {
            StringBuilder sb = new StringBuilder(160);
            TrajectoryFlowController f = Flow;
            sb.Append("R").Append(CountRobots());
            int stands = 0;
            foreach (RegisteredObject mo in UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Include))
            {
                if (mo == null || RuntimeRegistry.IsHidden(mo.gameObject)) continue;
                stands++;
            }
            sb.Append("|T").Append(stands);
            sb.Append("|P").Append(pointHistory.Count);
            if (f != null)
            {
                sb.Append('|').Append(f.State.phase);
                sb.Append('|').Append(f.State.candidates.Count);
                sb.Append('|').Append(f.State.phantoms.Count);
                sb.Append('|').Append(f.State.selectedTrajectory);
                sb.Append('|').Append(f.State.hasPoint ? 1 : 0);
                if (f.State.hasPoint)
                {
                    sb.Append(f.State.point.x.ToString("0.000"))
                      .Append(f.State.point.y.ToString("0.000"))
                      .Append(f.State.point.z.ToString("0.000"));
                }
                if (f.Robot != null) sb.Append('|').Append(f.Robot.robotName);
            }
            if (selectedNode != null) sb.Append("|S").Append(selectedNode.Key);
            // Новые функции (этапы 1–20): записи, позы, зоны, метки сравнения, идущая запись —
            // чтобы дерево пересобиралось, когда эти ветки меняются.
            sb.Append("|F").Append(KazistovVvFeatures.FeatureHub.TreeSignaturePart());
            // Постобработка траекторий (этапы 4–6): уровень сглаживания, лимиты, энергия, демо.
            sb.Append("|G").Append(KazistovVvFeatures.KvStageHub2.TreeSignaturePart());
            // Ограничения, калибровка, нагрузка, экспорт, импорт (этапы 7–12).
            sb.Append("|H").Append(KazistovVvFeatures.KvStageHub3.TreeSignaturePart());
            // Этапы 13–36: прокси, стенд, камеры, тепло, отчёт, сеть, ввод, макросы, окружение,
            // свет, кино, титры, голос, отказы, проверка и уровни журнала.
            sb.Append("|I").Append(KazistovVvFeatures.KvStageHub4.TreeSignaturePart());
            return sb.ToString();
        }

        /// <summary>Построить иерархию: роботы, столы, точки, траектории (ТЗ — структура сцены).</summary>
        private List<ProjectNode> BuildTreeModel()
        {
            List<ProjectNode> roots = new List<ProjectNode>();
            TrajectoryFlowController f = Flow;

            // --- РОБОТЫ (с осями и TCP)
            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            Array.Sort(robots, delegate (RobotController a, RobotController b)
            {
                return TrajectoryCore.HierarchyOrder.Compare(a != null ? a.transform : null,
                    b != null ? b.transform : null);
            });

            foreach (RobotController rc in robots)
            {
                if (rc == null || RuntimeRegistry.IsHidden(rc.gameObject)) continue;
                string key = "robot:" + rc.gameObject.name;
                ProjectNode node = new ProjectNode(key,
                    LabelFor(rc, key), ProjectNodeKind.Robot, rc.transform, rc);
                node.Key = key;
                node.Tooltip = "Робот «" + rc.robotName + "»\n" +
                               "Позиция: " + Vec(rc.transform.position) + "\n" +
                               "Узлов в модели: " + (rc.GetJointAngles() != null
                                   ? rc.GetJointAngles().Length : 0);
                if (f != null && f.Robot == rc) node.Details = "активный";
                else if (rc.isActive) node.Details = "выбран";

                if (rc.GetJointAngles() != null)
                {
                    int count = rc.GetJointAngles().Length;
                    for (int i = 0; i < count; i++)
                    {
                        Transform joint = JointTransformOf(rc, i);
                        string axisKey = key + "/axis" + i;
                        ProjectNode axis = new ProjectNode(axisKey, KvLoc.T("tree.axis", "Ось") + " " + (i + 1),
                            ProjectNodeKind.Axis, joint, rc);
                        axis.Key = axisKey;
                        axis.Details = rc.GetJointAngles()[i].ToString("0.0") + "°";
                        node.Children.Add(axis);
                    }
                }

                if (rc.tcp != null)
                {
                    string tcpKey = key + "/tcp";
                    ProjectNode tcp = new ProjectNode(tcpKey, "TCP", ProjectNodeKind.Tcp,
                        rc.tcp, rc);
                    tcp.Key = tcpKey;
                    tcp.Details = VecShort(rc.tcp.position);
                    node.Children.Add(tcp);
                }

                roots.Add(node);
            }

            // --- СТОЛЫ (стенды)
            List<ProjectNode> tables = new List<ProjectNode>();
            var markers = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Include);
            Array.Sort(markers, delegate (RegisteredObject a, RegisteredObject b)
            {
                return TrajectoryCore.HierarchyOrder.Compare(a != null ? a.transform : null,
                    b != null ? b.transform : null);
            });
            foreach (RegisteredObject mo in markers)
            {
                if (mo == null || RuntimeRegistry.IsHidden(mo.gameObject)) continue;
                string key = "table:" + mo.gameObject.name;
                ProjectNode node = new ProjectNode(key, LabelFor(null, key, mo.DisplayName),
                    ProjectNodeKind.Table, mo.transform, null);
                node.Key = key;
                node.Details = VecShort(mo.transform.position);
                tables.Add(node);
            }
            if (tables.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:tables", KvLoc.T("group.tables", "Столы"),
                    ProjectNodeKind.Group, null, null);
                group.Key = "group:tables";
                group.Details = tables.Count.ToString();
                group.Children.AddRange(tables);
                roots.Add(group);
            }

            // --- ТОЧКИ (зафиксированные оператором)
            if (pointHistory.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:points", KvLoc.T("group.points", "Точки"),
                    ProjectNodeKind.Group, null, null);
                group.Key = "group:points";
                group.Details = pointHistory.Count.ToString();
                for (int i = 0; i < pointHistory.Count; i++)
                {
                    Vector3 p = pointHistory[i];
                    string key = "point:" + p.x.ToString("0.000") + ":" + p.y.ToString("0.000") +
                                 ":" + p.z.ToString("0.000");
                    ProjectNode node = new ProjectNode(key, "Точка " + (i + 1),
                        ProjectNodeKind.Point, null, f != null ? f.Robot : null);
                    node.Key = key;
                    node.Tag = p;
                    node.Details = VecShort(p);
                    node.Tooltip = "Точка TCP: " + Vec(p);
                    group.Children.Add(node);
                }
                roots.Add(group);
            }

            // --- ТРАЕКТОРИИ (сгенерированные) + фантомы
            if (f != null && f.State.candidates.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:trajectories", KvLoc.T("group.trajectories", "Траектории"),
                    ProjectNodeKind.Group, null, null);
                group.Key = "group:trajectories";
                group.Details = f.State.candidates.Count.ToString();

                for (int i = 0; i < f.State.candidates.Count; i++)
                {
                    TrajectoryCandidate c = f.State.candidates[i];
                    if (c == null) continue;
                    string key = "traj:" + c.id + ":" + c.label;
                    ProjectNode node = new ProjectNode(key,
                        ShortLabel(c, i), ProjectNodeKind.Trajectory,
                        c.view != null ? c.view.transform : null, f.Robot);
                    node.Key = key;
                    node.Tag = c;
                    node.Details = c.lengthM.ToString("0.00") + " ю · " + c.timeS.ToString("0.0") + " с";
                    node.Tooltip = TrajectoryTooltip(c, i);
                    if (f.State.selectedTrajectory == i) node.Details = "✓ " + node.Details;
                    group.Children.Add(node);
                }

                if (f.Phantoms != null && f.State.phantoms.Count > 0)
                {
                    for (int i = 0; i < f.State.phantoms.Count; i++)
                    {
                        TrajectoryCore.PhantomConfig cfg = f.State.phantoms[i];
                        GameObject ghost = cfg.ghost;
                        string key = "phantom:" + i;
                        ProjectNode node = new ProjectNode(key, "Фантом " + (i + 1),
                            ProjectNodeKind.Phantom, ghost != null ? ghost.transform : null,
                            f.Robot);
                        node.Key = key;
                        node.Tag = i;
                        float progress = f.Phantoms.ProgressOf(i);
                        node.Details = Mathf.RoundToInt(progress * 100f) + " %";
                        node.Tooltip = "Фантом " + (i + 1) + " · " + cfg.tag +
                                       "\nПуть: " + f.Phantoms.PathLengthOf(i).ToString("0.00") +
                                       " ю · пройдено " + (progress * 100f).ToString("0") + " %";
                        group.Children.Add(node);
                    }
                }

                roots.Add(group);
            }

            // --- НОВЫЕ ФУНКЦИИ (этапы 1–20 ТЗ): записи траекторий, позы, зоны запрета,
            //     метки сравнения и пометки «опасная траектория». Добавляются ПОСЛЕ штатных
            //     ветвей, ничего в них не меняя; если хаб не создан — ветвей просто нет.
            KazistovVvFeatures.FeatureHub.BuildTreeNodes(this, roots, f != null ? f.Robot : null);
            // --- ветка «Постобработка траекторий» (этапы 4–6: сглаживание, время, энергия).
            KazistovVvFeatures.KvStageHub2.BuildTreeNodes(this, roots, f != null ? f.Robot : null);
            // --- ветка «Ограничения, калибровка и экспорт» (этапы 7–12).
            KazistovVvFeatures.KvStageHub3.BuildTreeNodes(this, roots, f != null ? f.Robot : null);
            // --- ветка «Этапы 13–36: производительность, показ и безопасность».
            KazistovVvFeatures.KvStageHub4.BuildTreeNodes(this, roots, f != null ? f.Robot : null);

            return roots;
        }

        private static string ShortLabel(TrajectoryCandidate c, int index)
        {
            string label = c.label;
            if (string.IsNullOrEmpty(label)) label = "Траектория " + (index + 1);
            int cut = label.IndexOf(" · ", StringComparison.Ordinal);
            if (cut > 0) label = label.Substring(0, cut);
            return label;
        }

        private static string TrajectoryTooltip(TrajectoryCandidate c, int index)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Траектория ").Append(index + 1).Append(" · ").Append(c.label);
            sb.Append("\nДлина: ").Append(c.lengthM.ToString("0.000")).Append(" ю");
            sb.Append("\nВремя: ").Append(c.timeS.ToString("0.00")).Append(" с");
            if (c.plan != null)
            {
                sb.Append("\nКривизна: сред. ").Append(c.plan.Curvature.ToString("0.0"))
                  .Append("°, сумма ").Append(c.plan.CurvatureTotal.ToString("0.0"))
                  .Append("°, макс ").Append(c.plan.CurvatureMax.ToString("0.0")).Append('°');
                sb.Append("\nСэмплов: ").Append(c.plan.Path != null ? c.plan.Path.Length : 0);
            }
            sb.Append("\nЗазор: ").Append((c.minClearance * 1000f).ToString("0")).Append(" мм");
            sb.Append("\nЗапас лимитов: ").Append(c.limitMarginDeg.ToString("0.0")).Append('°');
            sb.Append("\nОценка: ").Append(c.score.ToString("0.000"));
            if (!c.safe) sb.Append("\nНЕ прошла SafetyGate: ").Append(c.why);
            return sb.ToString();
        }

        private static Transform JointTransformOf(RobotController robot, int index)
        {
            SixAxisController six = robot as SixAxisController;
            if (six != null && six.jointTransforms != null && index < six.jointTransforms.Length)
                return six.jointTransforms[index];
            return robot.transform;
        }

        private static string Vec(Vector3 v)
        {
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ", " +
                   v.z.ToString("0.000") + ")";
        }

        private static string VecShort(Vector3 v)
        {
            return v.x.ToString("0.00") + ", " + v.y.ToString("0.00") + ", " + v.z.ToString("0.00");
        }

        private string LabelFor(RobotController robot, string key, string fallback = null)
        {
            string def = fallback ?? (robot != null && !string.IsNullOrEmpty(robot.robotName)
                ? robot.robotName : key);
            return NodeLabels.Get(key, def);
        }

        private void OnNodeVisibility(ProjectNode node, bool visible)
        {
            Log("Объект «" + node.DisplayName + "»: " + (visible ? "показан" : "скрыт") +
                " (только рендереры)");
        }

        private void OnNodeRenamed(ProjectNode node, string value)
        {
            Log("Узел переименован: «" + value + "»");
            NodeLabels.Set(node, value);
        }

        // ------------------------------------------------------------------ выбор узла

        /// <summary>Выбрать узел дерева: подсветка в сцене + панель свойств.</summary>
        public void SelectNode(ProjectNode node)
        {
            selectedNode = node;
            if (properties != null)
            {
                properties.SetNode(node);
                properties.SetProperties(BuildProperties(node));
            }
            if (tree != null) tree.SetSelected(node);

            if (node != null && node.Robot != null && node.Kind == ProjectNodeKind.Robot)
            {
                node.Robot.SetActive(true);
                idleBrain?.PingActivity();
            }

            if (highlight != null)
            {
                if (node != null && node.WorldTransform != null) highlight.SetTarget(node.WorldTransform);
                else highlight.SetTarget(null);
            }

            KazistovVvFeatures.KvStageHub.OnNodeSelected(node);
            KazistovVvFeatures.KvStageHub2.OnNodeSelected(node);
            KazistovVvFeatures.KvStageHub3.OnNodeSelected(node);
            KazistovVvFeatures.KvStageHub4.OnNodeSelected(node);

            if (node != null)
                Log("Выбран узел: " + node.DisplayName + " [" + node.Kind + "]");
        }

        private void TrackActiveRobotForProperties()
        {
            if (properties == null) return;

            RobotController current = null;
            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc != null && rc.isActive) { current = rc; break; }
            }

            if (current != null && current != lastActiveRobot)
            {
                lastActiveRobot = current;
                idleBrain?.PingActivity();
                if (toolbar != null) toolbar.SetWorkbench(current is SCARAController ? 1 : 0);
            }
            else if (current == null && lastActiveRobot != null)
            {
                lastActiveRobot = null;
            }
        }

        // ------------------------------------------------------------------ свойства

        private void UpdateProperties()
        {
            if (properties == null) return;
            if (tree != null && tree.Selected != selectedNode) selectedNode = tree.Selected;
            properties.SetNode(selectedNode);
            properties.SetProperties(BuildProperties(selectedNode));
        }

        /// <summary>Собрать строки свойств для узла (ТОЛЬКО ЧТЕНИЕ).</summary>
        public List<KvProp> BuildProperties(ProjectNode node)
        {
            List<KvProp> list = new List<KvProp>();
            TrajectoryFlowController f = Flow;

            if (node == null)
            {
                // ЭТАП 8: пустое состояние — панель НЕ заполняется «псевдо-свойствами»:
                // показывается заголовок «Ничего не выбрано», подсказка «Выберите объект»
                // и пульсирующий скелетон (KvPropertiesView), как в современных редакторах.
                return list;
            }

            list.Add(KvProp.Section(KvLoc.T("prop.section.common", "Общее")));
            list.Add(KvProp.Row("Имя", node.DisplayName, KvTheme.TextMain));
            list.Add(KvProp.Row("Тип", node.Kind.ToString(), KvTheme.TextDim));
            if (node.WorldTransform != null)
            {
                Transform t = node.WorldTransform;
                list.Add(KvProp.Row("Объект сцены", t.name, KvTheme.TextDim));
                list.Add(KvProp.Row("Видимость", KvTreeView.IsObjectVisible(node)
                    ? "виден" : "скрыт (рендереры)", KvTheme.TextMain));
                list.Add(KvProp.Row("Мировая позиция", Vec(t.position), KvTheme.TextMain));
            }

            switch (node.Kind)
            {
                case ProjectNodeKind.Robot:
                case ProjectNodeKind.Axis:
                case ProjectNodeKind.Tcp:
                    AddRobotProperties(list, node, f);
                    break;
                case ProjectNodeKind.Point:
                    AddPointProperties(list, node, f);
                    break;
                case ProjectNodeKind.Trajectory:
                    AddTrajectoryProperties(list, node, f);
                    break;
                case ProjectNodeKind.Phantom:
                    AddPhantomProperties(list, node, f);
                    break;
                case ProjectNodeKind.Table:
                    AddTableProperties(list, node, f);
                    break;
                default:
                    if (node.Children.Count > 0)
                    {
                        list.Add(KvProp.Section(KvLoc.T("prop.section.content", "Содержимое")));
                        list.Add(KvProp.Row("Элементов", node.Children.Count.ToString(), KvTheme.TextMain));
                    }
                    break;
            }

            // НОВЫЕ ФУНКЦИИ (этапы 1–20 ТЗ): свойства записей, поз, зон запрета,
            // метка сравнения и пометка «пересекает зону запрета» у траектории.
            KazistovVvFeatures.FeatureHub.BuildExtraProperties(node, list);
            // Постобработка траектории (этапы 4–6): сглаживание, время-оптимальная, энергия.
            KazistovVvFeatures.KvStageHub2.BuildExtraProperties(node, list);
            // Ограничения, калибровка, нагрузка, экспорт (этапы 7–12).
            KazistovVvFeatures.KvStageHub3.BuildExtraProperties(node, list);
            // Производительность, показ и безопасность (этапы 13–36).
            KazistovVvFeatures.KvStageHub4.BuildExtraProperties(node, list);

            return list;
        }

        private void AddRobotProperties(List<KvProp> list, ProjectNode node,
            TrajectoryFlowController f)
        {
            RobotController robot = node.Robot;
            if (robot == null) return;
            Transform t = robot.transform;

            list.Add(KvProp.Section(KvLoc.T("prop.section.transform", "Позиция и ориентация")));
            list.Add(KvProp.Row("Позиция", Vec(t.position), KvTheme.TextMain));
            Vector3 euler = t.eulerAngles;
            list.Add(KvProp.Row("Поворот", "(" + euler.x.ToString("0.0") + "°, " +
                euler.y.ToString("0.0") + "°, " + euler.z.ToString("0.0") + "°)", KvTheme.TextMain));

            float[] angles = robot.GetJointAngles();
            list.Add(KvProp.Section(KvLoc.T("prop.section.joints", "Углы суставов")));
            if (angles == null || angles.Length == 0)
            {
                list.Add(KvProp.Row("Углы", "нет данных", KvTheme.TextDim));
            }
            else
            {
                // Текущая поза читается ОДИН раз (не по суставу) — панель дешёвая.
                double[] currentQ = null;
                if (f != null && f.Validator != null && f.Validator.Ready)
                    currentQ = f.Validator.CopyCurrent();

                for (int i = 0; i < angles.Length; i++)
                {
                    bool prismatic = f != null && f.Validator != null && f.Validator.IsPrismatic(i);
                    string value = prismatic
                        ? angles[i].ToString("0.000") + " м"
                        : angles[i].ToString("0.00") + "°";

                    float margin = float.NaN;
                    if (currentQ != null && i < currentQ.Length)
                        margin = PrismaticMargin(f, i, currentQ[i]);
                    string limitText = "";
                    if (f != null && f.Validator != null && f.Validator.Ready && f.Validator.Lower != null &&
                        i < f.Validator.Lower.Length)
                    {
                        limitText = prismatic
                            ? " [" + f.Validator.Lower[i].ToString("0.000") + " … " +
                              f.Validator.Upper[i].ToString("0.000") + " м]"
                            : " [" + f.Validator.Lower[i].ToString("0.0") + "° … " +
                              f.Validator.Upper[i].ToString("0.0") + "°]";
                    }
                    Color c = float.IsNaN(margin) ? KvTheme.TextDim
                        : margin <= 3f ? KvTheme.Error
                        : margin <= 10f ? KvTheme.Warn : KvTheme.Ok;
                    string marginText = float.IsNaN(margin) ? ""
                        : (prismatic ? " · запас " + margin.ToString("0.000") + " м"
                                     : " · запас " + margin.ToString("0.0") + "°");
                    list.Add(KvProp.Row("Ось " + (i + 1), value + marginText + limitText, c));
                }
            }

            list.Add(KvProp.Section(KvLoc.T("prop.section.motion", "Скорость")));
            if (f != null)
            {
                list.Add(KvProp.Row("Скорость робота", "1 юнит за " +
                    (1f / Mathf.Max(0.0001f, f.robotMoveSpeed)).ToString("0.0") + " с" +
                    "  (" + f.robotMoveSpeed.ToString("0.0000") + " ю/с)", KvTheme.TextMain));
                list.Add(KvProp.Row("Скорость фантомов", "×" + f.phantomSpeedMultiplier.ToString("0.0") +
                    "  (" + f.phantomMoveSpeed.ToString("0.000") + " ю/с)", KvTheme.TextMain));
                if (f.Motion != null)
                {
                    list.Add(KvProp.Row("Движение", f.Motion.IsRunning
                        ? (f.Motion.Paused ? "пауза" : "идёт")
                        : "остановлено", f.Motion.IsRunning ? KvTheme.Ok : KvTheme.TextDim));
                }
            }
            else
            {
                list.Add(KvProp.Row("Скорость", "поток не найден", KvTheme.TextDim));
            }

            list.Add(KvProp.Section(KvLoc.T("prop.section.telemetry", "Телеметрия")));
            list.Add(KvProp.Row("Температура", robot.jointTemperature.ToString("0.0") + " °C",
                robot.jointTemperature > 60f ? KvTheme.Warn : KvTheme.TextMain));
            list.Add(KvProp.Row("Наработка", robot.operatingHours.ToString("0.00") + " ч", KvTheme.TextMain));
            list.Add(KvProp.Row("Активен", robot.isActive ? "да" : "нет",
                robot.isActive ? KvTheme.Ok : KvTheme.TextDim));
            list.Add(KvProp.Row("Привязан к потоку",
                f != null && f.Robot == robot ? "да" : "нет",
                f != null && f.Robot == robot ? KvTheme.Ok : KvTheme.TextDim));
            list.Add(KvProp.Row("Состояние потока", f != null ? f.State.phase.ToString() : "—",
                KvTheme.TextMain));
        }

        private static float PrismaticMargin(TrajectoryFlowController f, int index, double q)
        {
            if (f == null || f.Validator == null || !f.Validator.Ready) return float.NaN;
            if (f.Validator.Lower == null || index >= f.Validator.Lower.Length) return float.NaN;
            double lo = f.Validator.Lower[index], hi = f.Validator.Upper[index];
            return (float)Math.Min(q - lo, hi - q);
        }

        private void AddPointProperties(List<KvProp> list, ProjectNode node,
            TrajectoryFlowController f)
        {
            Vector3 point = node.Tag is Vector3 ? (Vector3)node.Tag
                : (f != null ? f.State.point : Vector3.zero);

            list.Add(KvProp.Section(KvLoc.T("prop.section.point", "Координаты")));
            list.Add(KvProp.Row("X", point.x.ToString("0.000"), KvTheme.TextMain));
            list.Add(KvProp.Row("Y", point.y.ToString("0.000"), KvTheme.TextMain));
            list.Add(KvProp.Row("Z", point.z.ToString("0.000"), KvTheme.TextMain));

            if (f != null && f.Robot != null)
            {
                Vector3 local = f.Robot.transform.InverseTransformPoint(point);
                list.Add(KvProp.Row("От базы робота",
                    "X " + local.x.ToString("0.000") + " · Y " + local.y.ToString("0.000") +
                    " · Z " + local.z.ToString("0.000"), KvTheme.TextDim));
            }

            list.Add(KvProp.Section(KvLoc.T("prop.section.reach", "Достижимость")));
            AimIndicator aim = ResolveAim();
            if (aim != null && aim.HasResult)
            {
                TrajectoryCore.ReachResult r = aim.Last;
                list.Add(KvProp.Row("Вердикт", VerdictLabel(r.verdict), VerdictColor(r.verdict)));
                list.Add(KvProp.Row("Причина", r.reason, KvTheme.TextDim));
            }
            else
            {
                list.Add(KvProp.Row("Вердикт", "нет данных оракула", KvTheme.TextDim));
            }
            list.Add(KvProp.Row("Текущая точка потока",
                f != null && f.State.hasPoint ? Vec(f.State.point) : "не зафиксирована",
                KvTheme.TextDim));
        }

        private void AddTrajectoryProperties(List<KvProp> list, ProjectNode node,
            TrajectoryFlowController f)
        {
            TrajectoryCandidate c = node.Tag as TrajectoryCandidate;
            if (c == null) return;

            list.Add(KvProp.Section(KvLoc.T("prop.section.trajectory", "Траектория")));
            list.Add(KvProp.Row("Подпись", c.label, KvTheme.TextMain));
            list.Add(KvProp.Row("Длина", c.lengthM.ToString("0.000") + " ю", KvTheme.TextMain));
            list.Add(KvProp.Row("Время (профиль)", c.timeS.ToString("0.00") + " с", KvTheme.TextMain));
            if (f != null)
            {
                list.Add(KvProp.Row("Время (скорость робота)",
                    (c.lengthM / Mathf.Max(0.0001f, f.robotMoveSpeed)).ToString("0.0") + " с",
                    KvTheme.TextMain));
                list.Add(KvProp.Row("Время (фантом)", (c.lengthM / f.phantomMoveSpeed).ToString("0.0") + " с",
                    KvTheme.TextDim));
            }
            list.Add(KvProp.Row("Зазор", (c.minClearance * 1000f).ToString("0") + " мм",
                c.minClearance < 0.05f ? KvTheme.Warn : KvTheme.Ok));
            list.Add(KvProp.Row("Запас лимитов", c.limitMarginDeg.ToString("0.0") + "°",
                c.limitMarginDeg < 5f ? KvTheme.Error : c.limitMarginDeg < 15f ? KvTheme.Warn : KvTheme.Ok));

            list.Add(KvProp.Section(KvLoc.T("prop.section.curvature", "Кривизна и оценка")));
            if (c.plan != null)
            {
                list.Add(KvProp.Row("Средняя", c.plan.Curvature.ToString("0.00") + "°", KvTheme.TextMain));
                list.Add(KvProp.Row("Суммарная", c.plan.CurvatureTotal.ToString("0.0") + "°", KvTheme.TextMain));
                list.Add(KvProp.Row("Максимальная", c.plan.CurvatureMax.ToString("0.0") + "°", KvTheme.TextMain));
                list.Add(KvProp.Row("Сэмплов", (c.plan.Path != null ? c.plan.Path.Length : 0).ToString(),
                    KvTheme.TextDim));
                list.Add(KvProp.Row("Ветвь IK", c.plan.BranchTag, KvTheme.TextDim));
            }
            list.Add(KvProp.Row("Оценка (меньше — лучше)", c.score.ToString("0.000"), KvTheme.TextMain));
            list.Add(KvProp.Row("SafetyGate", c.safe ? "пройдена" : "отказ: " + c.why,
                c.safe ? KvTheme.Ok : KvTheme.Error));

            if (f != null && f.State.selectedTrajectory >= 0 &&
                f.State.selectedTrajectory < f.State.candidates.Count &&
                ReferenceEquals(f.State.candidates[f.State.selectedTrajectory], c))
            {
                list.Add(KvProp.Section(KvLoc.T("prop.section.selection", "Выбор")));
                list.Add(KvProp.Row("Статус", "выбрана зелёным лазером", KvTheme.Ok));
            }
        }

        private void AddPhantomProperties(List<KvProp> list, ProjectNode node,
            TrajectoryFlowController f)
        {
            if (f == null || f.Phantoms == null) return;
            int index = node.Tag is int ? (int)node.Tag : 0;
            if (index < 0 || index >= f.State.phantoms.Count) return;
            TrajectoryCore.PhantomConfig cfg = f.State.phantoms[index];

            list.Add(KvProp.Section(KvLoc.T("prop.section.phantom", "Фантом")));
            list.Add(KvProp.Row("Номер", (index + 1).ToString(), KvTheme.TextMain));
            list.Add(KvProp.Row("Ветвь IK", cfg.tag.ToString(), KvTheme.TextDim));
            float progress = f.Phantoms.ProgressOf(index);
            list.Add(KvProp.Row("Пройдено", (progress * 100f).ToString("0") + " %", KvTheme.TextMain));
            list.Add(KvProp.Row("Длина пути", f.Phantoms.PathLengthOf(index).ToString("0.000") + " ю",
                KvTheme.TextMain));
            list.Add(KvProp.Row("Время прохода", f.Phantoms.DurationOf(index).ToString("0.00") + " с",
                KvTheme.TextMain));
            list.Add(KvProp.Row("Доехал", f.Phantoms.ArrivedOf(index) ? "да" : "нет",
                f.Phantoms.ArrivedOf(index) ? KvTheme.Ok : KvTheme.TextDim));
        }

        private void AddTableProperties(List<KvProp> list, ProjectNode node,
            TrajectoryFlowController f)
        {
            Transform t = node.WorldTransform;
            if (t == null) return;

            list.Add(KvProp.Section(KvLoc.T("prop.section.table", "Стол")));
            list.Add(KvProp.Row("Позиция", Vec(t.position), KvTheme.TextMain));
            Vector3 scale = t.lossyScale;
            list.Add(KvProp.Row("Масштаб", "(" + scale.x.ToString("0.00") + ", " +
                scale.y.ToString("0.00") + ", " + scale.z.ToString("0.00") + ")", KvTheme.TextMain));
            Vector3 euler = t.eulerAngles;
            list.Add(KvProp.Row("Поворот", "(" + euler.x.ToString("0.0") + "°, " +
                euler.y.ToString("0.0") + "°, " + euler.z.ToString("0.0") + "°)", KvTheme.TextMain));

            Renderer[] rr = t.GetComponentsInChildren<Renderer>(true);
            if (rr != null && rr.Length > 0)
            {
                Bounds b = rr[0].bounds;
                for (int i = 1; i < rr.Length; i++) b.Encapsulate(rr[i].bounds);
                list.Add(KvProp.Row("Габарит", b.size.x.ToString("0.00") + " × " +
                    b.size.y.ToString("0.00") + " × " + b.size.z.ToString("0.00"), KvTheme.TextDim));
                list.Add(KvProp.Row("Верх плоскости", b.max.y.ToString("0.000"), KvTheme.TextMain));
            }
            list.Add(KvProp.Row("Робот на столе", RobotOnTable(t, f), KvTheme.TextDim));
        }

        private string RobotOnTable(Transform table, TrajectoryFlowController f)
        {
            if (table == null) return "—";
            Bounds b = new Bounds(table.position, Vector3.one);
            Renderer[] rr = table.GetComponentsInChildren<Renderer>(true);
            if (rr != null && rr.Length > 0)
            {
                b = rr[0].bounds;
                for (int i = 1; i < rr.Length; i++) b.Encapsulate(rr[i].bounds);
            }
            foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include))
            {
                if (rc == null || RuntimeRegistry.IsHidden(rc.gameObject)) continue;
                Vector3 p = rc.transform.position;
                if (p.x >= b.min.x - 0.2f && p.x <= b.max.x + 0.2f &&
                    p.z >= b.min.z - 0.2f && p.z <= b.max.z + 0.2f)
                    return rc.robotName + (f != null && f.Robot == rc ? " (активный)" : "");
            }
            return "нет";
        }

        private AimIndicator ResolveAim()
        {
            if (aimIndicator == null)
            {
                Camera cam = MainCamera;
                if (cam != null) aimIndicator = cam.GetComponent<AimIndicator>();
            }
            return aimIndicator;
        }

        private static string VerdictLabel(TrajectoryCore.ReachVerdict v)
        {
            switch (v)
            {
                case TrajectoryCore.ReachVerdict.Safe: return "ДОСТИЖИМО";
                case TrajectoryCore.ReachVerdict.Marginal: return "ПРЕДЕЛЬНО";
                case TrajectoryCore.ReachVerdict.Collision: return "СТОЛКНОВЕНИЕ";
                default: return "НЕДОСТИЖИМО";
            }
        }

        private static Color VerdictColor(TrajectoryCore.ReachVerdict v)
        {
            switch (v)
            {
                case TrajectoryCore.ReachVerdict.Safe: return KvTheme.Ok;
                case TrajectoryCore.ReachVerdict.Marginal: return KvTheme.Warn;
                default: return KvTheme.Error;
            }
        }

        // ------------------------------------------------------------------ статус-бар

        private void UpdateStatusBar()
        {
            if (status == null) return;
            TrajectoryFlowController f = Flow;

            // --- состояние
            FlowState phase = f != null ? f.State.phase : FlowState.Idle;
            status.SetState(StateLabel(phase), StateColor(phase));

            // --- робот и «верстак»
            RobotController robot = f != null ? f.Robot : null;
            if (robot == null) robot = ActiveRobot();
            status.SetRobot(robot != null
                ? KvLoc.T("status.robot", "Робот") + ": " + robot.robotName
                : KvLoc.T("status.robot", "Робот") + ": " + KvLoc.T("common.none", "нет"));
            if (toolbar != null) toolbar.SetWorkbench(robot is SCARAController ? 1 : 0);

            // --- координаты луча/курсора
            FreeFlyCameraController cam = CameraRig;
            Vector3 aim = Vector3.zero;
            bool hasAim = false;
            if (f != null && f.IsPointMoveMode)
            {
                aim = f.State.movePoint;
                hasAim = true;
            }
            else if (cam != null)
            {
                aim = cam.AimPointPublic;
                hasAim = true;
            }
            status.SetCursor(hasAim
                ? KvLoc.T("status.ray", "Луч") + ": " + Vec(aim)
                : KvLoc.T("status.ray", "Луч") + ": —");

            // --- сообщение (поток важнее прицела)
            if (Time.realtimeSinceStartup - planStatusTime < 3f)
            {
                status.SetMessage(planStatusText, planStatusColor);
            }
            else
            {
                AimIndicator aim2 = ResolveAim();
                if (aim2 != null && aim2.HasResult)
                {
                    status.SetMessage("Прицел: " + VerdictLabel(aim2.Last.verdict) + " — " +
                        aim2.Last.reason, VerdictColor(aim2.Last.verdict));
                }
                else
                {
                    status.SetMessage("", KvTheme.TextDim);
                }
            }

            status.SetTheme(KvTheme.ModeLabel);
            status.SetFps(1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime));
            // §26: справа в статус-баре — ИНДИКАТОР РЕЖИМА КУРСОРА (иконка + «Режим: Камера / UI»).
            // Раньше здесь была служебная подсказка «TAB — телеоперация/интерфейс»: она дублировала
            // индикатор по смыслу, поэтому её место занял сам индикатор, а текст про Tab переехал
            // в его tooltip (клик по индикатору переключает режим — то же, что Tab).
            status.SetCursorMode(KvMouseCursor.CurrentMode);

            if (menuBar != null)
                menuBar.SetInfo("KazistovVv · десктопный интерфейс (стиль FreeCAD) · тема: " +
                    KvTheme.ModeLabel);
        }

        private static RobotController ActiveRobot()
        {
            foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include))
            {
                if (rc != null && rc.isActive) return rc;
            }
            return null;
        }

        private static string StateLabel(FlowState phase)
        {
            // ЭТАП 2: название состояния остаётся английским (оно же — имя в коде и логе),
            // а пояснение переводится словарём.
            switch (phase)
            {
                case FlowState.PointSelected:
                    return "PointSelected · " + KvLoc.T("phase.pointSelected", "точка выбрана");
                case FlowState.TrajectoriesShown:
                    return "TrajectoriesShown · " + KvLoc.T("phase.trajectoriesShown", "траектории");
                case FlowState.PhantomsMoving:
                    return "PhantomsMoving · " + KvLoc.T("phase.phantomsMoving", "фантом выбран");
                case FlowState.RobotMoving:
                    return "RobotMoving · " + KvLoc.T("phase.robotMoving", "робот едет");
                case FlowState.PointMoveMode:
                    return "PointMoveMode · " + KvLoc.T("phase.pointMoveMode", "перенос точки");
                default:
                    return "Idle · " + KvLoc.T("phase.idle", "ожидание");
            }
        }

        private static Color StateColor(FlowState phase)
        {
            switch (phase)
            {
                case FlowState.Idle: return KvTheme.TextDim;
                case FlowState.RobotMoving: return KvTheme.Warn;
                case FlowState.PointMoveMode: return KvTheme.Accent;
                default: return KvTheme.Ok;
            }
        }

        // ------------------------------------------------------------------ точки (история)

        private void TrackPoints()
        {
            TrajectoryFlowController f = Flow;
            if (f == null || !f.State.hasPoint) return;
            Vector3 p = f.State.point;
            for (int i = 0; i < pointHistory.Count; i++)
            {
                if (Vector3.Distance(pointHistory[i], p) < 0.005f) return;
            }
            pointHistory.Add(p);
            while (pointHistory.Count > 8) pointHistory.RemoveAt(0);
        }

        // ------------------------------------------------------------------ переключение робота

        private void SwitchRobot()
        {
            List<RobotController> robots = new List<RobotController>();
            foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include))
            {
                if (rc == null || RuntimeRegistry.IsHidden(rc.gameObject)) continue;
                robots.Add(rc);
            }
            if (robots.Count < 2) return;

            int current = 0;
            for (int i = 0; i < robots.Count; i++)
                if (robots[i].isActive) { current = i; break; }
            int next = (current + 1) % robots.Count;
            ActivateRobotOnly(robots[next]);
            if (toolbar != null) toolbar.SetWorkbench(robots[next] is SCARAController ? 1 : 0);
            Log("Активный робот: " + robots[next].robotName);
        }

        /// <summary>Выбор робота из «верстака» тулбара (0 — робот, 1 — SCARA).</summary>
        private void SelectRobotByWorkbench(int typeIndex)
        {
            foreach (RobotController rc in UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include))
            {
                if (rc == null || RuntimeRegistry.IsHidden(rc.gameObject)) continue;
                bool isScara = rc is SCARAController;
                if ((typeIndex == 0 && !isScara) || (typeIndex == 1 && isScara))
                {
                    ActivateRobotOnly(rc);
                    Log("Верстак: активный робот — " + rc.robotName);
                    return;
                }
            }
        }

        private static void ActivateRobotOnly(RobotController keep)
        {
            if (keep == null) return;
            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc != null) rc.SetActive(rc == keep);
            }
        }

        // ------------------------------------------------------------------ подсказка у мировой точки

        private Text worldTipVisual;
        private RectTransform worldTipBg;

        private void UpdateWorldTooltip()
        {
            if (status == null || canvasRect == null) return;
            Camera cam = MainCamera;
            if (cam == null) return;

            if (worldTipVisual == null)
            {
                worldTipVisual = KvTheme.CreateText(canvasRect, "WorldTip", "",
                    KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextMain);
                Image bg = KvTheme.CreatePanel(canvasRect, "WorldTipBg", KvTheme.PanelBg);
                worldTipBg = bg.rectTransform;
                worldTipBg.SetSiblingIndex(worldTipVisual.rectTransform.GetSiblingIndex());
            }

            bool show = Time.realtimeSinceStartup - worldTipTime < 1.2f &&
                        !string.IsNullOrEmpty(worldTipText);
            worldTipVisual.gameObject.SetActive(show);
            if (worldTipBg != null) worldTipBg.gameObject.SetActive(show);
            if (!show) return;

            worldTipVisual.text = worldTipText;
            Vector3 sp = cam.WorldToScreenPoint(worldTipPoint);
            if (sp.z < 0f)
            {
                worldTipVisual.gameObject.SetActive(false);
                if (worldTipBg != null) worldTipBg.gameObject.SetActive(false);
                return;
            }

            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, sp, null, out local);
            RectTransform tr = worldTipVisual.rectTransform;
            tr.anchorMin = tr.anchorMax = new Vector2(0.5f, 0.5f);
            tr.pivot = new Vector2(0f, 0f);
            tr.anchoredPosition = local + new Vector2(16f, 10f);
            tr.sizeDelta = new Vector2(620f, 22f);

            worldTipVisual.color = KvTheme.TextMain;
            if (worldTipBg != null)
            {
                Image bgi = worldTipBg.GetComponent<Image>();
                if (bgi != null) bgi.color = KvTheme.PanelBg;
                worldTipBg.anchorMin = worldTipBg.anchorMax = new Vector2(0.5f, 0.5f);
                worldTipBg.pivot = new Vector2(0f, 0f);
                worldTipBg.anchoredPosition = local + new Vector2(8f, 4f);
                worldTipBg.sizeDelta = new Vector2(640f, 34f);
            }

            // Слой подсказок UI всегда сверху.
            if (KvTooltip.Current != null) KvTooltip.Current.transform.SetAsLastSibling();
        }

        // ------------------------------------------------------------------ размещение (логика прежняя)

        public void StartPlacement(SpawnKind kind)
        {
            if (centerWindow == null) return;
            if (kind == SpawnKind.Robot)
            {
                pendingRobotTemplate = null;
                centerWindow.StartPlacement(SpawnKind.None);
                centerWindow.ShowRobotChooser(OnRobotTypeChosen);
                return;
            }
            if (kind == SpawnKind.None)
            {
                pendingRobotTemplate = null;
                yawManual = false;
                yawTrackedTable = null;
            }
            centerWindow.StartPlacement(kind);
        }

        private void OnRobotTypeChosen(int typeIndex)
        {
            centerWindow.HideRobotChooser();
            if (spawner == null) return;
            spawner.RefreshRobotTemplates();
            RobotController template = null;
            foreach (RobotController rc in spawner.robotTemplates)
            {
                if (rc == null) continue;
                bool isSix = rc is SixAxisController;
                bool isScara = rc is SCARAController;
                if ((typeIndex == 0 && isSix) || (typeIndex == 1 && isScara))
                {
                    template = rc;
                    break;
                }
            }
            if (template == null)
            {
                Debug.LogWarning("[KazistovVv] Нет шаблона робота нужного типа (0=робот, 1=SCARA).");
                return;
            }

            pendingRobotTemplate = template;
            yawManual = false;
            yawTrackedTable = null;
            Log("Размещение робота: " + template.robotName +
                " (наведите на стол, ←/→ направление, ЛКМ/Enter — поставить)");
            centerWindow.SetPhantomTemplate(template);
            centerWindow.StartPlacement(SpawnKind.Robot);
        }

        private void HandleRobotChooserKeys()
        {
            if (centerWindow == null || !centerWindow.RobotChooserOpen) return;

            bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Keyboard.current == null && TryLegacyKeyDown(KeyCode.Escape));
            if (escDown)
            {
                centerWindow.HideRobotChooser();
                return;
            }

            int pick = -1;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.digit1Key.wasPressedThisFrame) pick = 0;
                else if (Keyboard.current.digit2Key.wasPressedThisFrame) pick = 1;
            }
            if (pick < 0)
            {
                try
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1)) pick = 0;
                    else if (Input.GetKeyDown(KeyCode.Alpha2)) pick = 1;
                }
                catch { }
            }
            if (pick >= 0) OnRobotTypeChosen(pick);
        }

        private void HandlePlacementInput()
        {
            if (centerWindow == null || centerWindow.ActiveKind == SpawnKind.None) return;

            bool escDown = (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                           || (Keyboard.current == null && TryLegacyKeyDown(KeyCode.Escape));
            if (escDown)
            {
                centerWindow.StartPlacement(SpawnKind.None);
                return;
            }

            if (centerWindow.ActiveKind == SpawnKind.Robot)
            {
                float rot = 0f;
                if (Keyboard.current != null)
                {
                    if (Keyboard.current.leftArrowKey.isPressed || Keyboard.current.aKey.isPressed) rot -= 90f;
                    if (Keyboard.current.rightArrowKey.isPressed || Keyboard.current.dKey.isPressed) rot += 90f;
                }
                else
                {
                    try
                    {
                        if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) rot -= 90f;
                        if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) rot += 90f;
                    }
                    catch { }
                }
                if (rot != 0f)
                {
                    yawManual = true;
                    centerWindow.RotateRobot(rot * Time.deltaTime);
                }
            }

            Camera cam = MainCamera;
            if (cam == null) return;

            Ray ray = GetAimRay(cam);
            RaycastHit hitInfo;
            bool hit = Physics.Raycast(ray, out hitInfo, 200f, ~0, QueryTriggerInteraction.Ignore);

            Vector3 placePoint = hit ? hitInfo.point : ray.origin + ray.direction * 10f;
            bool valid;
            pendingTable = null;
            if (centerWindow.ActiveKind == SpawnKind.Robot)
            {
                Transform tableRoot = null;
                bool onTable = false;
                if (hit) onTable = TryFindTableSurface(hitInfo, out placePoint, out tableRoot);
                valid = onTable;
                if (onTable) pendingTable = tableRoot;
            }
            else
            {
                valid = hit && hitInfo.normal.y > 0.3f;
            }

            if (centerWindow.ActiveKind == SpawnKind.Robot && !yawManual && hit && pendingTable != null)
                SuggestRobotYaw(hitInfo.point);

            centerWindow.UpdatePreview(placePoint, valid);

            bool confirm = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
                || (Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame)
                || (Gamepad.current != null && Gamepad.current.buttonSouth.wasPressedThisFrame);
            if (!confirm || !valid) return;

            ConfirmPlacement(placePoint);
        }

        private static bool TryFindTableSurface(RaycastHit hit, out Vector3 center,
            out Transform tableRoot)
        {
            center = hit.point;
            tableRoot = null;
            Collider col = hit.collider;
            if (col == null) return false;

            bool looksLikeTable = false;
            RegisteredObject reg = col.GetComponentInParent<RegisteredObject>();
            string name = col.transform.root.name.ToLowerInvariant();

            if (reg != null) looksLikeTable = true;
            else if (name.Contains("стол") || name.Contains("table") || name.Contains("desk"))
                looksLikeTable = true;

            if (name.Contains("plane") || name.Contains("floor") || name.Contains("level") ||
                name.Contains("hangar") || name.Contains("wall"))
                looksLikeTable = false;

            if (!looksLikeTable) return false;

            Transform root = col.transform.root;
            tableRoot = root;
            Bounds b = GetRenderBounds(root);
            if (b.size.y < 0.001f) return false;
            center = new Vector3(b.center.x, b.max.y, b.center.z);
            return true;
        }

        private void RemoveRobotsOnTable(Transform tableRoot)
        {
            if (tableRoot == null) return;
            Bounds b = GetRenderBounds(tableRoot);
            float minX = b.min.x - 0.15f, maxX = b.max.x + 0.15f;
            float minZ = b.min.z - 0.15f, maxZ = b.max.z + 0.15f;
            float minY = b.min.y - 0.2f, maxY = b.max.y + 2.5f;

            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                if (rc.GetComponent<RegisteredObject>() == null) continue;
                if (IsRobotTemplate(rc)) continue;
                Vector3 p = rc.transform.position;
                if (p.x >= minX && p.x <= maxX && p.z >= minZ && p.z <= maxZ &&
                    p.y >= minY && p.y <= maxY)
                {
                    Log("На столе '" + tableRoot.name + "' был робот '" + rc.robotName +
                        "' — удалён (1 робот на стол).");
                    UnityEngine.Object.Destroy(rc.gameObject);
                }
            }
        }

        private bool IsRobotTemplate(RobotController robot)
        {
            if (spawner == null || spawner.robotTemplates == null) return false;
            foreach (RobotController t in spawner.robotTemplates)
            {
                if (t != null && t == robot) return true;
            }
            return false;
        }

        private void SuggestRobotYaw(Vector3 aimPoint)
        {
            if (pendingTable == null || centerWindow == null) return;

            Bounds b = GetRenderBounds(pendingTable);
            Vector3 center = new Vector3(b.center.x, 0f, b.center.z);
            Vector3 delta = aimPoint - center;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.01f) return;

            float raw = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
            float snapped = Mathf.Round(raw / 90f) * 90f;

            bool newTable = yawTrackedTable != pendingTable;
            float current = centerWindow.RobotYaw;
            float diff = Mathf.DeltaAngle(current, snapped);
            if (newTable || Mathf.Abs(diff) > 45f)
            {
                yawTrackedTable = pendingTable;
                centerWindow.SetRobotYaw(snapped);
            }
        }

        private void ConfirmPlacement(Vector3 point)
        {
            SpawnKind kind = centerWindow.ActiveKind;
            float yaw = kind == SpawnKind.Robot ? centerWindow.RobotYaw : 0f;
            centerWindow.StartPlacement(SpawnKind.None);

            if (spawner == null) return;

            RobotController placedRobot = null;
            RegisteredObject spawned;
            if (kind == SpawnKind.Table)
            {
                spawned = spawner.SpawnTable(point);
            }
            else
            {
                RemoveRobotsOnTable(pendingTable);
                if (pendingRobotTemplate == null)
                {
                    Debug.LogWarning("[KazistovVv] Не выбран тип робота — размещение отменено.");
                    return;
                }
                spawned = spawner.SpawnRobot(point, yaw, pendingRobotTemplate);
                if (spawned != null)
                {
                    placedRobot = spawned.GetComponent<RobotController>();
                    ActivateRobotOnly(placedRobot);
                }
            }

            if (spawned == null) return;

            RuntimeRegistry.RebuildFromScene();
            RuntimeRegistry.NotifyChanged();

            if (placedRobot != null)
            {
                RebuildTree(true);
                ProjectNode node = tree != null ? FindNodeByTransform(tree, placedRobot.transform) : null;
                if (node != null) SelectNode(node);
            }
        }

        private static ProjectNode FindNodeByTransform(KvTreeView view, Transform target)
        {
            if (view == null) return null;
            ProjectNode sel = view.Selected;
            if (sel != null && sel.WorldTransform == target) return sel;
            return null;
        }

        private static bool TryLegacyKeyDown(KeyCode code)
        {
            try { return Input.GetKeyDown(code); }
            catch { return false; }
        }

        private static Ray GetAimRay(Camera cam)
        {
            if (InputManager.Instance != null &&
                InputManager.Instance.ActiveProvider is GamepadInputProvider)
            {
                return new Ray(InputManager.Instance.PointerPosition,
                    InputManager.Instance.PointerDirection);
            }

            Vector3 screen = new Vector3(Screen.width * 0.5f, Screen.height * 0.5f, 0f);
            return cam.ScreenPointToRay(screen);
        }

        // ------------------------------------------------------------------ свет (логика прежняя)

        private void EnsureWorkLamp()
        {
            if (!enableWorkLamp || workLamp != null) return;

            Transform table = FindFirstTableSurface();
            if (table == null) return;

            Bounds b = GetRenderBounds(table);
            if (b.size.y < 0.001f) return;

            Vector3 pos = new Vector3(b.center.x, b.max.y + lampHeight, b.center.z);
            GameObject go = new GameObject("IlyichLamp");
            go.transform.position = pos;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = lampIntensity;
            light.range = lampRange;
            light.color = Color.white;
            light.colorTemperature = lampTemperature;
            light.useColorTemperature = true;
            light.shadows = LightShadows.Soft;

            workLamp = go;
            Log("«Лампочка Ильича» над столом '" + table.name + "' (" + pos.ToString("0.00") + ")");
        }

        private static Transform FindFirstTableSurface()
        {
            Transform best = null;
            float bestTop = float.NegativeInfinity;
            foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include))
            {
                if (t.GetComponent<Renderer>() == null) continue;
                if (t.GetComponentInParent<RobotController>() != null) continue;
                if (!NameLooksLikeTable(t.name)) continue;
                Renderer r = t.GetComponent<Renderer>();
                if (r == null) continue;
                float top = r.bounds.max.y;
                if (top > bestTop)
                {
                    bestTop = top;
                    best = t;
                }
            }
            return best;
        }

        private static bool NameLooksLikeTable(string name)
        {
            string n = name.ToLowerInvariant();
            return n.IndexOf("стол", StringComparison.Ordinal) >= 0 ||
                   n.IndexOf("desk", StringComparison.Ordinal) >= 0 ||
                   n.IndexOf("table", StringComparison.Ordinal) >= 0;
        }

        private static Bounds GetRenderBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
                return new Bounds(root.position, Vector3.one);
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                b.Encapsulate(renderers[i].bounds);
            return b;
        }

        private void BoostLighting()
        {
            if (!enableLightBoost) return;

            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            foreach (RobotController rc in robots)
            {
                if (rc == null || rc.name.StartsWith("Phantom")) continue;
                BoostRenderers(rc.transform);
            }
            var markers = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Include);
            foreach (RegisteredObject mo in markers)
            {
                if (mo == null || mo.name.StartsWith("Phantom")) continue;
                BoostRenderers(mo.transform);
            }
            Transform table = FindFirstTableSurface();
            if (table != null && table.GetComponentInParent<RobotController>() == null)
                BoostRenderers(table.transform);
        }

        private void BoostRenderers(Transform root)
        {
            if (root == null) return;
            if (root.GetComponent<BoostedObjectMarker>() != null) return;
            root.gameObject.AddComponent<BoostedObjectMarker>();

            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                Material m = r.material;
                if (m == null) continue;
                if (m.HasProperty("_BaseColor"))
                {
                    Color c = m.GetColor("_BaseColor");
                    m.SetColor("_BaseColor", new Color(
                        Mathf.Min(1.4f, c.r * lightBoostAlbedo),
                        Mathf.Min(1.4f, c.g * lightBoostAlbedo),
                        Mathf.Min(1.4f, c.b * lightBoostAlbedo),
                        c.a));
                }
                if (m.HasProperty("_Smoothness"))
                    m.SetFloat("_Smoothness", Mathf.Min(0.95f, m.GetFloat("_Smoothness") + lightBoostSmoothness));

                if (m.HasProperty("_EmissiveColor"))
                {
                    Color c = m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : Color.white;
                    float k = Mathf.Clamp01(lightBoostEmissive);
                    m.SetColor("_EmissiveColor", new Color(c.r * k, c.g * k, c.b * k, 1f));
                    m.EnableKeyword("_EMISSION");
                }
            }
        }

        private static void WarnAboutRobotCount()
        {
            var robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include);
            string list = "";
            int count = 0;
            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
                count++;
                if (list.Length > 0) list += ", ";
                list += rc.name;
            }
            if (count == 2) Debug.Log("[KazistovVv] Роботов в сцене: 2 → " + list);
            else Debug.LogWarning("[KazistovVv] Роботов в сцене: " + count +
                " (ожидается 2 — робот на стенде 1 + SCARA на стенде 2) → " + list);
        }

        // ------------------------------------------------------------------ обновление точек истории

        private float pointTrackTimer;

        /// <summary>Периодический сбор точек для ветки «Точки» дерева (вызывается из Update).</summary>
        void LateUpdate()
        {
            if (!built) return;

            // §26 (Esc — ВАРИАНТ А): снимок «открыт ли контекст, который САМ обрабатывает Esc».
            // Снимается В КОНЦЕ кадра, поэтому в следующем кадре это ровно «состояние на начало
            // кадра» — ДО того, как панели отработали Esc. Без такого снимка одно нажатие Esc
            // закрывало бы меню И переключало режим курсора (двойное действие): порядок
            // выполнения Update у панелей и камеры не определён.
            EscContextWasOpen = EscContextOpen();

            pointTrackTimer -= Time.unscaledDeltaTime;
            if (pointTrackTimer > 0f) return;
            pointTrackTimer = 0.5f;
            TrackPoints();
        }

        /// <summary>
        /// §26, ЗАДАЧА 3 (ВАРИАНТ А): открыт ли контекст, который обрабатывает Esc САМ.
        /// Пока такой контекст есть, Esc принадлежит ему (закрыть меню, отменить действие),
        /// и режим курсора не переключается. Когда контекста нет — Esc работает как Tab.
        ///
        /// Контексты (только чтение состояния, ничего не меняем):
        ///   • палитра команд (Ctrl+P) и открытое меню верхней панели;
        ///   • всплывающие группы тулбара и контекстное меню узла дерева;
        ///   • размещение объекта / выбор типа робота (CenterWindow);
        ///   • активная область или фокус клавиатурной навигации (KvKeyboardNav);
        ///   • незавершённый сценарий потока и режим перемещения точки (сброс по Esc) —
        ///     State Machine только ЧИТАЕТСЯ, её логика не тронута.
        ///
        /// Док-панели (дерево, свойства, настройки, горячие клавиши) контекстом НЕ считаются:
        /// они не модальные, живут в раскладке рабочей среды и Esc их не закрывает.
        /// </summary>
        public bool EscContextOpen()
        {
            if (palette != null && palette.IsOpen) return true;
            if (menuBar != null && menuBar.OpenMenuIndex >= 0) return true;
            if (toolbar != null && toolbar.GroupMenuOpen) return true;

            KvContextMenu contextMenu = KvContextMenu.Current;
            if (contextMenu != null && contextMenu.IsOpen) return true;

            if (centerWindow != null &&
                (centerWindow.RobotChooserOpen || centerWindow.ActiveKind != SpawnKind.None)) return true;

            if (keyboardNav != null &&
                (keyboardNav.HasFocus || keyboardNav.Scope != KvKeyboardNav.NavScope.Toolbar)) return true;

            TrajectoryFlowController f = Flow;
            if (f != null && (f.IsPointMoveMode || f.State.phase != FlowState.Idle)) return true;

            return false;
        }

        /// <summary>
        /// §26: был ли Esc-контекст открыт В НАЧАЛЕ текущего кадра. Читают: контроллер камеры
        /// (вариант А) и диагностика. Обновляется один раз за кадр в <see cref="LateUpdate"/>.
        /// </summary>
        public static bool EscContextWasOpen { get; private set; }

        /// <summary>Публичное обновление интерфейса (для диагностики/тестов/меню редактора).</summary>
        public void Refresh()
        {
            RuntimeRegistry.RebuildFromScene();
            RebuildTree(true);
            RefreshPanels();
        }

        /// <summary>
        /// Обновление, которое реально идёт КАЖДЫЙ тик (статус-бар, свойства, кнопки) —
        /// без пересборки дерева и реестра. Именно этот путь определяет влияние UI на FPS.
        /// </summary>
        public void RefreshPanels()
        {
            UpdateStatusBar();
            UpdateProperties();
            if (toolbar != null) toolbar.Refresh();
            if (menuBar != null) menuBar.Refresh();
        }

        // ------------------------------------------------------------------ диагностика

        /// <summary>Сколько кнопок в верхней панели (ТЗ: 15).</summary>
        public int ToolbarButtonCount { get { return toolbar != null ? toolbar.ButtonCount : 0; } }
        /// <summary>Сколько строк в дереве.</summary>
        public int TreeRowCount { get { return tree != null ? tree.RowCount : 0; } }
        /// <summary>Меню (для диагностики/расширения).</summary>
        public KvMenuBar Menu { get { return menuBar; } }
        /// <summary>Верхняя панель инструментов.</summary>
        public KvToolbar Toolbar { get { return toolbar; } }
        /// <summary>Дерево моделей.</summary>
        public KvTreeView Tree { get { return tree; } }
        /// <summary>Панель свойств.</summary>
        public KvPropertiesView Properties { get { return properties; } }
        /// <summary>Панель настроек и справки.</summary>
        public KvSettingsView SettingsView { get { return settings; } }
        /// <summary>Статус-бар.</summary>
        public KvStatusBar StatusBar { get { return status; } }
        /// <summary>Подсветка выбранного объекта в сцене.</summary>
        public KvSelectionHighlight Highlight { get { return highlight; } }
        /// <summary>Dock-панель дерева.</summary>
        public KvDockPanel TreeDock { get { return treeDock; } }
        /// <summary>Dock-панель свойств.</summary>
        public KvDockPanel PropertiesDock { get { return propertiesDock; } }
        /// <summary>Dock-панель настроек.</summary>
        public KvDockPanel SettingsDock { get { return settingsDock; } }
        /// <summary>Сколько строк в свойствах.</summary>
        public int PropertyRowCount { get { return properties != null ? properties.RowCount : 0; } }
        /// <summary>Текст состояния в статус-баре.</summary>
        public string StatusState { get { return status != null ? status.StateText : ""; } }
        /// <summary>Текст сообщения в статус-баре.</summary>
        public string StatusMessage { get { return status != null ? status.MessageText : ""; } }
        /// <summary>Текст координат в статус-баре.</summary>
        public string StatusCursor { get { return status != null ? status.CursorText : ""; } }
        /// <summary>Индикатор темы в статус-баре.</summary>
        public string StatusTheme { get { return status != null ? status.ThemeText : ""; } }
        /// <summary>Текущий выбранный узел дерева.</summary>
        public ProjectNode SelectedNode { get { return selectedNode; } }
        /// <summary>Число известных точек (ветка «Точки»).</summary>
        public int PointCount { get { return pointHistory.Count; } }
        /// <summary>Текущая вкладка панели настроек (диагностика).</summary>
        public int SettingsTab { get { return settings != null ? settings.ActiveTab : -1; } }
        /// <summary>Сколько объектов в иерархии интерфейса (диагностика производительности).</summary>
        public int UiObjectCount { get { return canvasRect != null ? CountChildren(canvasRect) : 0; } }

        private static int CountChildren(Transform root)
        {
            int count = 1;
            foreach (Transform child in root) count += CountChildren(child);
            return count;
        }

        /// <summary>Показать/скрыть панель настроек (диагностика/меню).</summary>
        public void ToggleSettings()
        {
            TogglePanel("settings");
        }

        // ================================================================== ЭТАП 5: палитра команд

        /// <summary>Палитра команд (Ctrl+P) — только чтение для диагностики.</summary>
        public KvCommandPalette Palette { get { return palette; } }

        /// <summary>Открыть палитру команд (Ctrl+P, Start на геймпаде, кнопка тулбара).</summary>
        public void OpenCommandPalette()
        {
            if (palette == null) return;
            palette.Open();
            Log("палитра команд открыта (команд в реестре: " + KvCommands.All.Count + ")");
        }

        /// <summary>Закрыть палитру команд.</summary>
        public void CloseCommandPalette()
        {
            if (palette != null) palette.Close();
        }

        // ================================================================== ЭТАП 7: горячие клавиши

        /// <summary>Окно горячих клавиш (для диагностики).</summary>
        public KvHotkeyView Hotkeys { get { return hotkeys; } }
        /// <summary>Dock-панель окна горячих клавиш (для диагностики/раскладки).</summary>
        public KvDockPanel HotkeyDock { get { return hotkeyDock; } }

        /// <summary>Показать/скрыть окно горячих клавиш (F12, меню «Справка», Select геймпада).</summary>
        public void ToggleHotkeys()
        {
            if (hotkeyDock == null) return;
            bool show = !hotkeyDock.Shown;
            hotkeyDock.SetVisible(show);
            if (show && hotkeys != null) hotkeys.Rebuild();
            LayoutDock(StatusHeight);
            Log(show ? "окно горячих клавиш открыто (биндов: " + KvBindings.All().Count + ")"
                     : "окно горячих клавиш закрыто");
        }

        /// <summary>Показать окно горячих клавиш (без переключения).</summary>
        public void ShowHotkeys(bool show)
        {
            if (hotkeyDock == null) return;
            if (hotkeyDock.Shown == show) return;
            ToggleHotkeys();
        }

        // ================================================================== ЭТАП 3: сброс раскладки

        /// <summary>
        /// СБРОСИТЬ РАСКЛАДКУ ОКОН (ЭТАП 3): сохранённые в PlayerPrefs позиции/размеры/края
        /// удаляются, группы тулбара разворачиваются, оболочка пересобирается — все панели
        /// возвращаются на места по умолчанию.
        /// </summary>
        public void ResetLayout()
        {
            KvLayoutStore.Clear(new[] { "tree", "properties", "settings", "hotkeys" });
            panelStates.Clear();
            foreach (KvToolbarGroup group in KvToolbarGroups.All)
                KvSettings.SetToolbarGroupCollapsed(group.Id, false);
            RebuildShell();
            Log("раскладка окон сброшена к значениям по умолчанию (панели и группы тулбара)");
        }

        /// <summary>Состояния интерфейса (ошибка/загрузка/пустое состояние) — для диагностики.</summary>
        public KvUiStates States { get { return uiStates; } }
        /// <summary>Виртуальный геймпад (ЭТАП 9) — для диагностики.</summary>
        public KvGamepadHud GamepadHudView { get { return gamepadHud; } }
        /// <summary>Роутер геймпада (ЭТАП 9) — для диагностики.</summary>
        public KvGamepadRouter GamepadRouter { get { return gamepadRouter; } }
        /// <summary>Навигация по интерфейсу с клавиатуры (ЭТАП 11) — для диагностики.</summary>
        public KvKeyboardNav KeyboardNav { get { return keyboardNav; } }

        /// <summary>
        /// Навигация с клавиатуры ТЕКУЩЕГО интерфейса или null (безопасно, если интерфейса нет).
        /// Нужна внешним системам ввода, чтобы уступить клавишу навигации: пока у кнопки
        /// тулбара/меню есть фокус, Enter нажимает ЕЁ, а не подтверждает точку
        /// (ФИКС §27 — комбинации клавиш, см. `FreeFlyCameraController`).
        /// </summary>
        public static KvKeyboardNav KeyboardNavOrNull
        {
            get
            {
                KazistovVvUIManager ui = Instance;
                return ui != null ? ui.keyboardNav : null;
            }
        }

        /// <summary>Заново применить сохранённые настройки визуализаций к сцене (диагностика).</summary>
        public void ApplySettingsFromDiagnostics()
        {
            ApplySettingsToScene();
            if (toolbar != null) toolbar.Refresh();
            if (settings != null) settings.RefreshValues();
        }

        // ================================================================== ЭТАП 9: геймпад

        /// <summary>Переключить активного робота (D-Pad ← / → на геймпаде).</summary>
        public void SwitchRobotFromGamepad()
        {
            SwitchRobot();
        }

        // ================================================================== ЭТАП 6: контекстное меню дерева

        /// <summary>
        /// ПКМ по узлу дерева: меню строится ПОД ТИП УЗЛА и применяется ко ВСЕМУ набору
        /// выделенных узлов (Ctrl+клик — мультивыбор). Меню показывает только то, что
        /// реально поддержано: «Удалить»/«Дублировать» — для точек и waypoints,
        /// «Скрыть/Показать» — для узлов с объектом сцены, «Фокус камеры» — для объектов.
        /// </summary>
        public void OnTreeContextMenu(List<ProjectNode> nodes, Vector2 screenPosition)
        {
            if (nodes == null || nodes.Count == 0) return;
            ProjectNode primary = nodes[0];
            if (primary == null) return;

            List<KvContextMenuItem> items = new List<KvContextMenuItem>();
            bool single = nodes.Count == 1;

            // --- переименование
            items.Add(new KvContextMenuItem(
                KvLoc.T("ctx.rename", "Переименовать"), delegate { RenameNode(primary); },
                "rename", single, false, false, "двойной ЛКМ"));

            // --- дублирование (точки и waypoints)
            bool canDuplicate = AllOfKind(nodes, ProjectNodeKind.Point) ||
                                AllWaypoints(nodes);
            items.Add(new KvContextMenuItem(
                KvLoc.T("ctx.duplicate", "Дублировать"), delegate { DuplicateNodes(nodes); },
                "duplicate", canDuplicate));

            // --- удаление (точки и waypoints)
            bool canDelete = AllOfKind(nodes, ProjectNodeKind.Point) || AllWaypoints(nodes);
            items.Add(new KvContextMenuItem(
                KvLoc.T("ctx.delete", "Удалить"), delegate { DeleteNodes(nodes); },
                "trash", canDelete,
                false, false, "Del"));

            items.Add(KvContextMenuItem.Sep());

            // --- скрыть/показать (визуально: рендереры объекта сцены)
            bool canHide = false;
            foreach (ProjectNode n in nodes)
                if (n != null && n.CanHide) { canHide = true; break; }
            bool visibleNow = KvTreeView.IsObjectVisible(primary);
            items.Add(new KvContextMenuItem(
                visibleNow ? KvLoc.T("ctx.hide", "Скрыть") : KvLoc.T("ctx.show", "Показать"),
                delegate { ToggleNodesVisibility(nodes); }, visibleNow ? "eye-off" : "eye",
                canHide, true, !visibleNow));

            // --- фокус камеры
            items.Add(new KvContextMenuItem(
                KvLoc.T("ctx.focus", "Фокус камеры на объекте"),
                delegate { FocusCameraOn(primary); }, "focus",
                primary.WorldTransform != null));

            items.Add(KvContextMenuItem.Sep());

            // --- свойства и копирование имени
            items.Add(new KvContextMenuItem(
                KvLoc.T("ctx.properties", "Свойства"), delegate
                {
                    SelectNode(primary);
                    SetPanel("properties", true);
                },
                "properties", true, true, selectedNode == primary));

            items.Add(new KvContextMenuItem(
                KvLoc.T("ctx.copy", "Копировать имя"), delegate { CopyNodeName(primary); },
                "copy"));

            string title = single
                ? primary.DisplayName + " · " + KindLabel(primary.Kind)
                : KvLoc.T("ctx.multi", "Выбрано узлов") + ": " + nodes.Count;
            KvContextMenu.Show(canvasRect, screenPosition, title, items);
        }

        private static bool AllOfKind(List<ProjectNode> nodes, ProjectNodeKind kind)
        {
            if (nodes == null || nodes.Count == 0) return false;
            foreach (ProjectNode n in nodes)
                if (n == null || n.Kind != kind) return false;
            return true;
        }

        private static bool AllWaypoints(List<ProjectNode> nodes)
        {
            if (nodes == null || nodes.Count == 0) return false;
            foreach (ProjectNode n in nodes)
            {
                if (n == null || n.Key == null ||
                    !n.Key.StartsWith("waypoint:", StringComparison.Ordinal)) return false;
            }
            return true;
        }

        private static string KindLabel(ProjectNodeKind kind)
        {
            switch (kind)
            {
                case ProjectNodeKind.Robot: return KvLoc.T("tree.kind.robot", "робот");
                case ProjectNodeKind.Axis: return KvLoc.T("tree.kind.axis", "ось");
                case ProjectNodeKind.Tcp: return "TCP";
                case ProjectNodeKind.Table: return KvLoc.T("tree.kind.table", "стол");
                case ProjectNodeKind.Point: return KvLoc.T("tree.kind.point", "точка");
                case ProjectNodeKind.Trajectory: return KvLoc.T("tree.kind.trajectory", "траектория");
                case ProjectNodeKind.Phantom: return KvLoc.T("tree.kind.phantom", "фантом");
                case ProjectNodeKind.Group: return KvLoc.T("tree.kind.group", "группа");
                default: return KvLoc.T("tree.kind.object", "объект");
            }
        }

        /// <summary>ЭТАП 6: переименовать узел «на месте» (как двойным кликом).</summary>
        public void RenameNode(ProjectNode node)
        {
            if (node == null || tree == null) return;
            SelectNode(node);
            if (!tree.BeginRenameSelected())
            {
                SetPlanStatus(KvLoc.T("ctx.rename.manual",
                    "Переименование: двойной клик по узлу (меню доступно только для выбранного)"),
                    KvTheme.Warn);
            }
        }

        /// <summary>ЭТАП 6: скрыть/показать объекты выбранных узлов.</summary>
        public void ToggleNodesVisibility(List<ProjectNode> nodes)
        {
            if (nodes == null || tree == null) return;
            bool target = !KvTreeView.IsObjectVisible(nodes[0]);
            foreach (ProjectNode n in nodes)
            {
                if (n == null || !n.CanHide) continue;
                tree.SetObjectVisible(n, target);
                OnNodeVisibility(n, target);
            }
            RebuildTree(true);
            SetPlanStatus(target
                ? KvLoc.T("ctx.shown", "Объекты показаны")
                : KvLoc.T("ctx.hidden", "Объекты скрыты"), KvTheme.TextDim);
        }

        /// <summary>ЭТАП 6: навести камеру на объект узла (фокус).</summary>
        public void FocusCameraOn(ProjectNode node)
        {
            if (node == null || node.WorldTransform == null) return;
            FreeFlyCameraController cam = CameraRig;
            if (cam == null) return;
            Vector3 point = node.WorldTransform.position;
            Renderer r = node.WorldTransform.GetComponentInChildren<Renderer>();
            float distance = 2.4f;
            if (r != null)
            {
                point = r.bounds.center;
                distance = Mathf.Max(1.2f, r.bounds.extents.magnitude * 2.6f);
            }
            cam.FocusOn(point, distance);
            SetPlanStatus(KvLoc.T("ctx.focused", "Камера наведена на") + ": " + node.DisplayName,
                KvTheme.Accent);
        }

        /// <summary>ЭТАП 6: скопировать имя узла в буфер обмена.</summary>
        public void CopyNodeName(ProjectNode node)
        {
            if (node == null) return;
            try { GUIUtility.systemCopyBuffer = node.DisplayName; } catch { }
            SetPlanStatus(KvLoc.T("ctx.copied", "Имя скопировано в буфер") + ": " + node.DisplayName,
                KvTheme.Ok);
        }

        /// <summary>ЭТАП 6: удалить узлы (точки — из истории, waypoints — из маршрута).</summary>
        public void DeleteNodes(List<ProjectNode> nodes)
        {
            if (nodes == null) return;
            int removed = 0;
            foreach (ProjectNode node in nodes)
            {
                if (node == null) continue;
                if (node.Kind == ProjectNodeKind.Point && node.Tag is Vector3)
                {
                    Vector3 point = (Vector3)node.Tag;
                    bool isWaypoint = node.Key != null &&
                                      node.Key.StartsWith("waypoint:", StringComparison.Ordinal);
                    if (isWaypoint)
                    {
                        if (DeleteWaypoint(node)) removed++;
                    }
                    else
                    {
                        for (int i = 0; i < pointHistory.Count; i++)
                        {
                            if (Vector3.Distance(pointHistory[i], point) > 0.001f) continue;
                            pointHistory.RemoveAt(i);
                            removed++;
                            break;
                        }
                    }
                }
                else if (node.Key != null &&
                         node.Key.StartsWith("waypoint:", StringComparison.Ordinal))
                {
                    if (DeleteWaypoint(node)) removed++;
                }
            }
            if (removed == 0)
            {
                SetPlanStatus(KvLoc.T("ctx.delete.none",
                    "Удаление поддерживается для точек и промежуточных точек"), KvTheme.Warn);
                return;
            }
            tree.ClearMultiSelection();
            SelectNode(null);
            RebuildTree(true);
            Log("контекстное меню: удалено узлов — " + removed);
        }

        private bool DeleteWaypoint(ProjectNode node)
        {
            KazistovVvFeatures.KvStageHub hub = KazistovVvFeatures.KvStageHub.Instance;
            if (hub == null || hub.Waypoints == null || node == null || node.Key == null) return false;
            int index;
            if (!int.TryParse(node.Key.Substring("waypoint:".Length), out index)) return false;
            return hub.Waypoints.Remove(index);
        }

        /// <summary>ЭТАП 6: дублировать узлы (точки — со смещением, waypoints — со смещением).</summary>
        public void DuplicateNodes(List<ProjectNode> nodes)
        {
            if (nodes == null) return;
            KazistovVvFeatures.KvStageHub hub = KazistovVvFeatures.KvStageHub.Instance;
            int made = 0;
            foreach (ProjectNode node in nodes)
            {
                if (node == null) continue;
                bool isWaypoint = node.Key != null &&
                                  node.Key.StartsWith("waypoint:", StringComparison.Ordinal);
                Vector3 point = Vector3.zero;
                bool hasPoint = false;
                if (node.Tag is Vector3) { point = (Vector3)node.Tag; hasPoint = true; }
                else if (node.WorldTransform != null)
                {
                    point = node.WorldTransform.position;
                    hasPoint = true;
                }
                if (!hasPoint) continue;

                Vector3 copy = point + new Vector3(0.12f, 0f, 0.12f);
                if (isWaypoint && hub != null && hub.Waypoints != null)
                {
                    if (hub.Waypoints.Add(copy, "дубликат " + node.DisplayName)) made++;
                }
                else if (node.Kind == ProjectNodeKind.Point)
                {
                    pointHistory.Add(copy);
                    while (pointHistory.Count > 16) pointHistory.RemoveAt(0);
                    made++;
                }
            }
            if (made == 0)
            {
                SetPlanStatus(KvLoc.T("ctx.duplicate.none",
                    "Дублирование поддерживается для точек и промежуточных точек"), KvTheme.Warn);
                return;
            }
            RebuildTree(true);
            Log("контекстное меню: продублировано узлов — " + made);
        }
    }

    /// <summary>
    /// Пользовательские подписи узлов дерева: переименование во FreeCAD меняет
    /// ЯРЛЫК узла, а не имя объекта сцены (безопасно: имена объектов используют
    /// планировщик, столы и сборочные утилиты). Ключ — стабильный ключ узла.
    /// </summary>
    public static class NodeLabels
    {
        private static readonly Dictionary<string, string> labels = new Dictionary<string, string>();

        public static void Set(ProjectNode node, string label)
        {
            if (node == null) return;
            labels[node.Key] = label;
        }

        public static string Get(string key, string fallback)
        {
            string value;
            if (!string.IsNullOrEmpty(key) && labels.TryGetValue(key, out value) &&
                !string.IsNullOrEmpty(value))
                return value;
            return fallback;
        }

        public static void Clear()
        {
            labels.Clear();
        }
    }

    /// <summary>Маркер «объект уже получил усиление светоотражения» (как было в UI-менеджере).</summary>
    public class BoostedObjectMarker : MonoBehaviour
    {
    }
}
