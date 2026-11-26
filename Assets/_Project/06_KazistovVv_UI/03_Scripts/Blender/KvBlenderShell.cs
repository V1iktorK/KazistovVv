using System;
using System.Collections.Generic;
using KazistovVvFeatures;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// КАРКАС ОКНА РАСКЛАДКИ BLENDER (утверждённый дизайн: Blender-раскладка + стеклянные окна).
    ///
    /// Сверху — КАНТ ОКНА: заголовок «KazistovVv · Тренажёр операторов» и строка меню
    /// Файл / Правка / Вид / Робот / Сценарии / Сервис / Справка (пункты — из реестра
    /// `KvCommands` по `MenuPath`, существующие меню-команды). Дальше — тулбар 8 групп
    /// (`KvBlenderToolbar`), рабочий район с доками (`KvAutoHideDock`) и стеклянными окнами
    /// панелей (`KvGlassWindow`), внизу — статус-строка (`KvStatusBar`: режим/FPS/координаты).
    ///
    /// ОБОЛОЧКА ЖИВЁТ НА ОТДЕЛЬНОМ КАНВАСЕ и не трогает классическую раскладку: она
    /// строится поверх и прячет классическую видимость (`KazistovVvUIManager.SetUiVisible`),
    /// поэтому все существующие элементы и их идентификаторы остаются доступными.
    /// </summary>
    public class KvBlenderShell : MonoBehaviour
    {
        /// <summary>Заголовок канваса оболочки.</summary>
        public const string CanvasName = "KvBlenderShellCanvas";

        /// <summary>Ширина столбца панелей в доке (px).</summary>
        public const float DockPanelSize = 320f;
        /// <summary>Высота нижнего дока (px).</summary>
        public const float BottomDockSize = 250f;
        /// <summary>Высота статус-строки (px).</summary>
        public const float StatusHeight = 22f;

        private static KvBlenderShell instance;

        /// <summary>Живая оболочка (null — раскладка Blender не включена).</summary>
        public static KvBlenderShell Instance
        {
            get { return instance; }
        }

        // ------------------------------------------------------------------ части каркаса

        public RectTransform CanvasRect { get; private set; }
        public RectTransform WorkArea { get; private set; }
        public Text TitleText { get; private set; }
        public KvBlenderMenuStrip MenuStrip { get; private set; }
        public KvBlenderToolbar Toolbar { get; private set; }
        public KvStatusBar StatusBar { get; private set; }
        public KvCommandPalette Palette { get; private set; }

        private readonly List<KvAutoHideDock> docks = new List<KvAutoHideDock>();
        public IReadOnlyList<KvAutoHideDock> Docks
        {
            get { return docks; }
        }

        private readonly List<KvGlassWindow> windows = new List<KvGlassWindow>();
        public IReadOnlyList<KvGlassWindow> Windows
        {
            get { return windows; }
        }

        /// <summary>Окно панели по id («theme», «zones», …; null — нет).</summary>
        public KvGlassWindow FindWindow(string id)
        {
            for (int i = 0; i < windows.Count; i++)
                if (windows[i] != null && windows[i].Id == id) return windows[i];
            return null;
        }

        /// <summary>Заголовок окна (проверяется тестами каркаса).</summary>
        public string Title
        {
            get { return TitleText != null ? TitleText.text : ""; }
        }

        // ------------------------------------------------------------------ данные панелей

        private readonly List<Text> jointRows = new List<Text>();
        private readonly List<Text> toastRows = new List<Text>();
        private readonly List<Text> queueRows = new List<Text>();
        private readonly List<Text> timelineRows = new List<Text>();
        private readonly List<Text> consoleRows = new List<Text>();
        private readonly List<Button> favoriteRows = new List<Button>();
        private Text minimapState;
        private RectTransform minimapRobot;
        private RectTransform minimapCamera;
        private Text gizmoState;
        private RectTransform gizmoTriad;
        private Text favoritesState;
        private InputField favoritesPinField;

        // ------------------------------------------------------------------ жизненный цикл

        /// <summary>Построить оболочку (идемпотентно: повторный вызов возвращает живую).</summary>
        public static KvBlenderShell Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject(CanvasName);
            instance = go.AddComponent<KvBlenderShell>();
            instance.Build();
            return instance;
        }

        /// <summary>Разобрать оболочку (возврат к классической раскладке).</summary>
        public static void TearDown()
        {
            KvBlenderShell current = instance;
            instance = null;
            KvGlassStyle.ClearTracking();
            if (current != null) KvGlassStyle.SafeDestroy(current.gameObject);
        }

        private void Awake()
        {
            instance = this;
        }

        private void OnDestroy()
        {
            KvTheme.Changed -= OnThemeChanged;
            KvUiStates.Reported -= OnReported;
            if (instance == this) instance = null;
        }

        /// <summary>Собрать каркас: кант, меню, тулбар, доки с панелями, статус-строку.</summary>
        public void Build()
        {
            EnsureEventSystem();

            // --- канвас оболочки (поверх классического)
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 120;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            GameObject rootGo = new GameObject("Root", typeof(RectTransform));
            rootGo.transform.SetParent(transform, false);
            CanvasRect = (RectTransform)rootGo.transform;
            KvTheme.Stretch(CanvasRect);

            // --- кант сверху: заголовок окна
            Image titleBg = KvGlassStyle.CreateGlassRect(CanvasRect, "TitleBar", KvGlassRole.Header);
            RectTransform titleRt = titleBg.rectTransform;
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(1f, 1f);
            titleRt.pivot = new Vector2(0.5f, 1f);
            titleRt.sizeDelta = new Vector2(0f, 26f);
            titleRt.anchoredPosition = Vector2.zero;
            TitleText = KvTheme.CreateText(titleRt, "Title",
                KvLoc.T("blender.shell.title", "KazistovVv · Тренажёр операторов"),
                KvTheme.FontSizeTitle, TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(TitleText.rectTransform, 10f, 10f, 0f, 0f);

            float topY = 26f;

            // --- строка меню (Файл/Правка/Вид/Робот/Сценарии/Сервис/Справка)
            MenuStrip = gameObject.AddComponent<KvBlenderMenuStrip>();
            topY += MenuStrip.Build(CanvasRect, topY);

            // --- тулбар 8 групп (спрайты владельца, крупные иконки — переключатель)
            Toolbar = gameObject.AddComponent<KvBlenderToolbar>();
            topY += Toolbar.Build(CanvasRect, topY);
            topY += 4f;

            // --- рабочий район (между кантом и статус-строкой)
            GameObject workGo = new GameObject("WorkArea", typeof(RectTransform));
            workGo.transform.SetParent(CanvasRect, false);
            WorkArea = (RectTransform)workGo.transform;
            WorkArea.anchorMin = new Vector2(0f, 0f);
            WorkArea.anchorMax = new Vector2(1f, 1f);
            WorkArea.offsetMin = new Vector2(0f, StatusHeight);
            WorkArea.offsetMax = new Vector2(0f, -topY);

            // --- палитра команд Ctrl+K (существующая, встроена в новый стиль)
            Palette = KvCommandPalette.Create(CanvasRect);

            BuildDocks();
            BuildWindows();

            // --- статус-строка (Режим/FPS/координаты — существующие поля)
            StatusBar = gameObject.AddComponent<KvStatusBar>();
            StatusBar.Build(CanvasRect, canvas);
            StatusBar.SetTheme(KvTheme.PaletteLabel);

            // --- тосты привязаны к механизму Report
            KvUiStates.Reported += OnReported;

            // --- тема: стёкла перекрашиваются мгновенно
            KvTheme.Changed += OnThemeChanged;

            RefreshAll();
        }

        private void BuildDocks()
        {
            KvAutoHideDock left = gameObject.AddComponent<KvAutoHideDock>();
            left.Build(WorkArea, KvDockEdge.Left, DockPanelSize);
            docks.Add(left);

            KvAutoHideDock right = gameObject.AddComponent<KvAutoHideDock>();
            right.Build(WorkArea, KvDockEdge.Right, DockPanelSize);
            docks.Add(right);

            KvAutoHideDock bottom = gameObject.AddComponent<KvAutoHideDock>();
            bottom.Build(WorkArea, KvDockEdge.Bottom, BottomDockSize);
            docks.Add(bottom);
        }

        private void BuildWindows()
        {
            KvAutoHideDock left = docks[0];
            KvAutoHideDock right = docks[1];
            KvAutoHideDock bottom = docks[2];

            // --- левый док: зоны, суставы, миникарта
            AddWindow(left, CreateWindow("zones", "blender.panel.zones", "Зоны и коллизии",
                DockPanelSize - 8f, 240f, KvBlenderPanels.BuildZones));
            {
                KvGlassWindow w = CreateWindow("joints", "blender.panel.joints", "Суставы HUD",
                    DockPanelSize - 8f, 190f, null);
                KvBlenderPanels.BuildJoints(w, jointRows);
                left.AddWindow(w);
            }
            {
                KvGlassWindow w = CreateWindow("minimap", "blender.panel.minimap", "Миникарта",
                    DockPanelSize - 8f, 160f, null);
                KvBlenderPanels.BuildMinimap(w, out minimapState, out minimapRobot, out minimapCamera);
                left.AddWindow(w);
            }

            // --- правый док: тема, горячие клавиши, жесты, избранное, камера-гизмо
            AddWindow(right, CreateWindow("theme", "blender.panel.theme", "Тема",
                DockPanelSize - 8f, 130f, KvBlenderPanels.BuildTheme));
            {
                KvGlassWindow w = CreateWindow("hotkeys", "blender.panel.hotkeys", "Горячие клавиши",
                    DockPanelSize - 8f, 240f, null);
                KvBlenderPanels.BuildHotkeys(w);
                right.AddWindow(w);
            }
            AddWindow(right, CreateWindow("gestures", "blender.panel.gestures", "Жесты",
                DockPanelSize - 8f, 190f, KvBlenderPanels.BuildGestures));
            {
                KvGlassWindow w = CreateWindow("favorites", "blender.panel.favorites", "Избранное",
                    DockPanelSize - 8f, 220f, null);
                KvBlenderPanels.BuildFavorites(w, favoriteRows, out favoritesPinField, out favoritesState);
                right.AddWindow(w);
            }
            {
                KvGlassWindow w = CreateWindow("cameragizmo", "blender.panel.camera", "Камера-гизмо",
                    DockPanelSize - 8f, 150f, null);
                KvBlenderPanels.BuildCameraGizmo(w, out gizmoState, out gizmoTriad);
                right.AddWindow(w);
            }

            // --- нижний док: таймлайн, очередь, консоль, тосты
            {
                KvGlassWindow w = CreateWindow("timeline", "blender.panel.timeline", "Таймлайн",
                    420f, BottomDockSize - 10f, null);
                KvBlenderPanels.BuildTimeline(w, timelineRows);
                bottom.AddWindow(w);
            }
            {
                KvGlassWindow w = CreateWindow("queue", "blender.panel.queue", "Очередь задач",
                    420f, BottomDockSize - 10f, null);
                KvBlenderPanels.BuildQueue(w, queueRows);
                bottom.AddWindow(w);
            }
            {
                KvGlassWindow w = CreateWindow("console", "blender.panel.console", "Консоль",
                    520f, BottomDockSize - 10f, null);
                KvBlenderPanels.BuildConsole(w, consoleRows);
                bottom.AddWindow(w);
            }
            {
                KvGlassWindow w = CreateWindow("toasts", "blender.panel.toasts", "Тосты",
                    420f, BottomDockSize - 10f, null);
                KvBlenderPanels.BuildToasts(w, toastRows);
                bottom.AddWindow(w);
            }

            // --- «Команды» (Ctrl+K): плавающее окно у палитры
            {
                KvGlassWindow w = CreateWindow("commands", "blender.panel.commands", "Команды",
                    320f, 120f, null);
                KvBlenderPanels.BuildCommands(w, Palette);
                w.Root.anchoredPosition = new Vector2(760f, -220f);
            }
        }

        private delegate void PanelBuilder(KvGlassWindow window);

        private KvGlassWindow CreateWindow(string id, string titleKey, string fallbackTitle,
            float width, float height, PanelBuilder builder)
        {
            KvGlassWindow w = new KvGlassWindow(id,
                KvLoc.T(titleKey, fallbackTitle), CanvasRect, CanvasRect, WorkArea, width, height);
            if (builder != null) builder(w);
            windows.Add(w);
            return w;
        }

        private void AddWindow(KvAutoHideDock dock, KvGlassWindow window)
        {
            dock.AddWindow(window);
        }

        // ------------------------------------------------------------------ обновление

        private float nextRefresh;

        private void Update()
        {
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            RefreshAll();
        }

        /// <summary>
        /// Обновить все живые панели (телеметрия, журнал, очередь, таймлайн, статус-строка).
        /// Вызывается таймером оболочки и напрямую из тестов (в EditMode `Update` не работает).
        /// </summary>
        public void RefreshAll()
        {
            KvBlenderPanels.RefreshJoints(jointRows);
            KvBlenderPanels.RefreshToasts(toastRows);
            KvBlenderPanels.RefreshQueue(queueRows);
            KvBlenderPanels.RefreshTimeline(timelineRows);
            KvBlenderPanels.RefreshConsole(consoleRows);
            KvBlenderPanels.RefreshMinimap(minimapState, minimapRobot, minimapCamera);
            KvBlenderPanels.RefreshCameraGizmo(gizmoState, gizmoTriad);
            KvBlenderPanels.RefreshFavorites(favoriteRows, favoritesState);
            if (StatusBar != null)
            {
                StatusBar.SetTheme(KvTheme.PaletteLabel);
                StatusBar.SetState(KvLoc.T("blender.shell.mode", "Режим: ") +
                    (KvLayoutMode.IsBlender ? KvLoc.T("blender.layout.blender", "Blender")
                        : KvLoc.T("blender.layout.classic", "Классика")), KvTheme.TextDim);
            }
        }

        private void OnThemeChanged()
        {
            KvGlassStyle.ApplyTheme();
            if (TitleText != null)
                TitleText.text = KvLoc.T("blender.shell.title", "KazistovVv · Тренажёр операторов");
            if (StatusBar != null) StatusBar.SetTheme(KvTheme.PaletteLabel);
        }

        /// <summary>Тост от механизма Report (ошибки/события).</summary>
        private void OnReported(string message)
        {
            KvBlenderToasts.Report(message);
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<EventSystem>() != null) return;
            GameObject go = new GameObject("KvBlenderEventSystem",
                typeof(EventSystem), typeof(StandaloneInputModule));
            go.hideFlags = HideFlags.None;
        }
    }
}
