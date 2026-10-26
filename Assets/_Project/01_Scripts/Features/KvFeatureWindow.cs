using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Перетаскивание окна за заголовок.</summary>
    public class KvFeatureDrag : MonoBehaviour, IDragHandler, IBeginDragHandler
    {
        public RectTransform Target;
        private Vector2 offset;

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target.parent as RectTransform, eventData.position,
                eventData.pressEventCamera, out offset);
            offset = Target.anchoredPosition - offset;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (Target == null) return;
            Vector2 local;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                Target.parent as RectTransform, eventData.position,
                eventData.pressEventCamera, out local))
                Target.anchoredPosition = local + offset;
        }
    }

    /// <summary>
    /// ПАНЕЛЬ ФУНКЦИЙ (этапы 1–20 ТЗ) — одно плавающее окно с вкладками.
    ///
    /// Строится КОДОМ на собственном канвасе (`sortingOrder = 45`: ниже панели метрик (50)
    /// и HUD режима точки (60), выше основной оболочки), поэтому существующий интерфейс
    /// в стиле FreeCAD не перестраивается и не ломается: окно можно перетаскивать за заголовок,
    /// скрывать и открывать кнопкой тулбара, пунктом меню или клавишами F5–F7.
    ///
    /// Вкладки: Запись · Позы · Суставы · Зоны · Сравнение · Графики · Замер · Журнал ·
    /// Сценарии · Сессии. Плюс две служебные панели: ETA (этап 12) и текст презентационного
    /// режима (этап 19).
    /// </summary>
    public class KvFeatureWindow : MonoBehaviour
    {
        public int sortingOrder = 45;
        public float windowWidth = 760f;
        public float windowHeight = 520f;
        public float refreshInterval = 0.2f;

        /// <summary>Русские названия вкладок — ВНУТРЕННИЕ ключи (индексы используются кодом).</summary>
        private static readonly string[] TabTitles =
        {
            "Запись", "Позы", "Суставы", "Зоны", "Сравнение", "Графики",
            "Замер", "Журнал", "Сценарии", "Сессии"
        };

        /// <summary>Ключи словаря для вкладок (ЭТАП 2) — в том же порядке, что TabTitles.</summary>
        private static readonly string[] TabKeys =
        {
            "tab.record", "tab.poses", "tab.joints", "tab.zones", "tab.compare",
            "tab.graphs", "tab.perf", "tab.log", "tab.scenarios", "tab.sessions"
        };

        /// <summary>Подписи вкладок с учётом языка интерфейса.</summary>
        public static string[] TabTitlesLocalized()
        {
            string[] result = new string[TabTitles.Length];
            for (int i = 0; i < TabTitles.Length; i++)
                result[i] = KazistovVvUI.KvLoc.T(TabKeys[i], TabTitles[i]);
            return result;
        }

        private FeatureHub hub;
        private Canvas canvas;
        private RectTransform window;
        private RectTransform content;
        private RectTransform tabBar;
        private ScrollRect scroll;
        private Text titleText;
        private KvSegmented tabStrip;

        private bool visible;
        private int tab;

        private readonly List<Text> dynamicTexts = new List<Text>();
        private readonly List<Func<string>> dynamicProviders = new List<Func<string>>();
        private float refreshTimer;
        private int builtTab = -1;

        // --- элементы конкретных вкладок
        private readonly List<KvIconButton> tabButtons = new List<KvIconButton>();
        private List<KvTrajectoryRecord> records = new List<KvTrajectoryRecord>();
        private List<KvPosePreset> poses = new List<KvPosePreset>();
        private List<KvSession> sessions = new List<KvSession>();
        private readonly List<Slider> joints = new List<Slider>();
        private readonly List<Text> jointTexts = new List<Text>();
        private KvJointGraph graph;
        private bool graphOverlay;
        private Text recordSourceText;
        private Text nameFieldText;
        private int scenarioIndex;
        private int sessionIndex;
        private int recordIndex;
        private int poseIndex;
        private int zoneIndex;

        // --- панели ETA и презентации
        private RectTransform etaPanel;
        private Text etaLabel, etaTime;
        private Image etaFill;
        private Text presentationText;
        private RectTransform presentationPanel;

        public bool Visible { get { return visible; } }
        public int VisibleTab { get { return visible ? tab : -1; } }
        public bool EtaPanelEnabled = true;

        /// <summary>Идентификатор окна в реестре пояса ([D22]) — один на всё приложение.</summary>
        public const string WindowId = "panel.features";

        /// <summary>Свёрнуто ли окно (чёрточка в заголовке, решение владельца [D21]).</summary>
        private bool collapsed;

        /// <summary>Строка вкладок и область содержимого — их прячет сворачивание.</summary>
        private GameObject tabsRoot;
        private GameObject scrollRoot;

        /// <summary>Свёрнуто ли окно: заголовок остаётся, содержимое скрыто ([D21]).</summary>
        public bool Collapsed { get { return collapsed; } }

        /// <summary>
        /// ЭТАП 2 (мультиязычность): обновить подписи окна после смены языка — заголовок и
        /// полосу вкладок. Окно не пересоздаётся, состояние и выбранная вкладка сохраняются.
        /// </summary>
        public void RefreshLanguageLabels()
        {
            if (window == null) return;

            if (titleText != null)
                titleText.text = KazistovVvUI.KvLoc.T("panel.features", "Панель функций")
                    .ToUpperInvariant() + " · " + TabTitlesLocalized()[Mathf.Clamp(tab, 0, TabTitles.Length - 1)] +
                    "   (F5)";

            if (tabBar != null && tabStrip != null)
            {
                int current = Mathf.Clamp(tab, 0, TabTitles.Length - 1);
                Destroy(tabStrip.gameObject);
                tabStrip = KvWidgets.Segmented(tabBar, "FeatureTabs", TabTitlesLocalized(), current,
                    delegate (int index) { Show(index); }, 20f);
                RectTransform stripRect = tabStrip.GetComponent<RectTransform>();
                KvTheme.Stretch(stripRect, 4f, 4f, 2f, 2f);
            }

            if (visible) BuildTabContent();
        }
        public KvTrajectoryRecord SelectedRecord
        {
            get { return records.Count > 0 ? records[Mathf.Clamp(recordIndex, 0, records.Count - 1)] : null; }
        }
        public bool RecordSourcePhantom;

        // ================================================================== сборка

        /// <summary>Собрать окно (идемпотентно: повторный вызов ничего не ломает).</summary>
        public void Build(FeatureHub featureHub)
        {
            hub = featureHub;
            if (window != null) return;

            GameObject canvasGo = new GameObject("KvFeatureCanvas", typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            canvasGo.hideFlags = HideFlags.HideInHierarchy;
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            RectTransform canvasRect = (RectTransform)canvasGo.transform;

            // --- плавающее окно
            GameObject winGo = new GameObject("KvFeatureWindow", typeof(Image), typeof(Outline));
            winGo.transform.SetParent(canvasRect, false);
            window = (RectTransform)winGo.transform;
            window.anchorMin = new Vector2(0.5f, 0.5f);
            window.anchorMax = new Vector2(0.5f, 0.5f);
            window.pivot = new Vector2(0.5f, 0.5f);
            window.sizeDelta = new Vector2(windowWidth, windowHeight);
            window.anchoredPosition = new Vector2(-190f, 20f);

            Image bg = winGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.WindowBg;
            bg.raycastTarget = true;

            Outline outline = winGo.GetComponent<Outline>();
            outline.effectColor = KvTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);

            // --- заголовок
            GameObject headerGo = new GameObject("Header", typeof(Image), typeof(KvFeatureDrag));
            headerGo.transform.SetParent(winGo.transform, false);
            RectTransform header = (RectTransform)headerGo.transform;
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, 22f);
            header.anchoredPosition = Vector2.zero;
            headerGo.GetComponent<Image>().color = KvTheme.PanelHeader;
            KvFeatureDrag drag = headerGo.GetComponent<KvFeatureDrag>();
            drag.Target = window;

            titleText = KvTheme.CreateText(header, "Title", "ПАНЕЛЬ ФУНКЦИЙ · этапы 1–20",
                KvTheme.FontSizeTitle, TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(titleText.rectTransform, 8f, 30f, 0f, 0f);

            Button close = KvTheme.CreateSmallButton(header, "Close", "✕", delegate { Hide(); });
            RectTransform closeRect = close.GetComponent<RectTransform>();
            closeRect.anchorMin = new Vector2(1f, 0.5f);
            closeRect.anchorMax = new Vector2(1f, 0.5f);
            closeRect.pivot = new Vector2(1f, 0.5f);
            closeRect.sizeDelta = new Vector2(22f, 18f);
            closeRect.anchoredPosition = new Vector2(-3f, 0f);

            // --- «СВЕРНУТЬ» (чёрточка) рядом с «ЗАКРЫТЬ» — решение владельца [D21].
            // Закрытие уводит окно НА ПОЯС ([D22], см. Hide ниже), сворачивание оставляет его на месте.
            Button collapseButton = KvTheme.CreateSmallButton(header, "Collapse", "—",
                delegate { ToggleCollapse(); });
            RectTransform collapseRect = collapseButton.GetComponent<RectTransform>();
            collapseRect.anchorMin = new Vector2(1f, 0.5f);
            collapseRect.anchorMax = new Vector2(1f, 0.5f);
            collapseRect.pivot = new Vector2(1f, 0.5f);
            collapseRect.sizeDelta = new Vector2(22f, 18f);
            collapseRect.anchoredPosition = new Vector2(-27f, 0f);

            // --- строка вкладок
            GameObject tabGo = new GameObject("Tabs", typeof(Image));
            tabGo.transform.SetParent(winGo.transform, false);
            tabBar = (RectTransform)tabGo.transform;
            tabBar.anchorMin = new Vector2(0f, 1f);
            tabBar.anchorMax = new Vector2(1f, 1f);
            tabBar.pivot = new Vector2(0.5f, 1f);
            tabBar.sizeDelta = new Vector2(0f, 24f);
            tabBar.anchoredPosition = new Vector2(0f, -22f);
            tabGo.GetComponent<Image>().color = KvTheme.PanelBg;
            tabsRoot = tabGo;

            tabStrip = KvWidgets.Segmented(tabBar, "FeatureTabs", TabTitlesLocalized(), 0,
                delegate (int index) { Show(index); }, 20f);
            RectTransform stripRect = tabStrip.GetComponent<RectTransform>();
            KvTheme.Stretch(stripRect, 4f, 4f, 2f, 2f);

            // --- прокручиваемая область содержимого
            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect));
            scrollGo.transform.SetParent(winGo.transform, false);
            scrollRoot = scrollGo;
            RectTransform scrollRect = (RectTransform)scrollGo.transform;
            scrollRect.anchorMin = new Vector2(0f, 0f);
            scrollRect.anchorMax = new Vector2(1f, 1f);
            scrollRect.offsetMin = new Vector2(4f, 4f);
            scrollRect.offsetMax = new Vector2(-4f, -48f);

            GameObject viewportGo = new GameObject("Viewport", typeof(Image), typeof(RectMask2D));
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewport = (RectTransform)viewportGo.transform;
            KvTheme.Stretch(viewport);
            viewportGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.01f);

            GameObject contentGo = new GameObject("Content", typeof(VerticalLayoutGroup),
                typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewportGo.transform, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = contentGo.GetComponent<VerticalLayoutGroup>();
            layout.childControlHeight = false;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            layout.spacing = 3f;
            layout.padding = new RectOffset(4, 4, 4, 6);

            ContentSizeFitter fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
            scroll.viewport = viewport;
            scroll.content = content;

            BuildEtaPanel(canvasRect);
            BuildPresentationPanel(canvasRect);

            visible = false;
            window.gameObject.SetActive(false);
            // РЕЕСТР ОКОН И ПОЯС ([D21]/[D22]): окно умеет показаться, скрыться (уйти на пояс)
            // и свернуться. Действия передаются делегатами — реестру не нужны ни сцена, ни канвас.
            KazistovVvXr.KvWindowBelt.Register(WindowId, this,
                delegate { Show(tab); }, Hide, ToggleCollapse,
                KazistovVvUI.KvLoc.T("panel.features", "Панель функций"));

            Debug.Log("[Features] панель функций собрана (вкладок " + TabTitles.Length + ") · " +
                      KazistovVvXr.KvWindowBelt.Describe());
        }

        private void BuildEtaPanel(RectTransform canvasRect)
        {
            GameObject go = new GameObject("KvEtaPanel", typeof(Image));
            go.transform.SetParent(canvasRect, false);
            etaPanel = (RectTransform)go.transform;
            etaPanel.anchorMin = new Vector2(1f, 0f);
            etaPanel.anchorMax = new Vector2(1f, 0f);
            etaPanel.pivot = new Vector2(1f, 0f);
            etaPanel.sizeDelta = new Vector2(340f, 44f);
            etaPanel.anchoredPosition = new Vector2(-12f, 30f);
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelBg;
            bg.raycastTarget = false;

            etaLabel = KvTheme.CreateText(etaPanel, "Label", "ТРАЕКТОРИЯ · осталось", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextMain);
            KvWidgets.Fit(etaLabel.gameObject, 320f, 15f);
            etaLabel.rectTransform.anchorMin = new Vector2(0f, 1f);
            etaLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            etaLabel.rectTransform.pivot = new Vector2(0.5f, 1f);
            etaLabel.rectTransform.anchoredPosition = new Vector2(0f, -3f);

            GameObject barBg = new GameObject("BarBg", typeof(Image));
            barBg.transform.SetParent(etaPanel, false);
            RectTransform barRect = (RectTransform)barBg.transform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.sizeDelta = new Vector2(-12f, 8f);
            barRect.anchoredPosition = new Vector2(0f, 8f);
            barBg.GetComponent<Image>().color = KvTheme.InputBg;

            GameObject fillGo = new GameObject("BarFill", typeof(Image));
            fillGo.transform.SetParent(barBg.transform, false);
            RectTransform fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            etaFill = fillGo.GetComponent<Image>();
            etaFill.color = KvTheme.Ok;
            etaFill.raycastTarget = false;
            etaFill.type = Image.Type.Filled;
            etaFill.fillMethod = Image.FillMethod.Horizontal;
            etaFill.fillAmount = 0f;

            etaTime = KvTheme.CreateText(etaPanel, "Time", "", KvTheme.FontSizeSmall,
                TextAnchor.LowerRight, KvTheme.TextDim);
            KvWidgets.Fit(etaTime.gameObject, 320f, 14f);
            etaTime.rectTransform.anchorMin = new Vector2(0f, 0f);
            etaTime.rectTransform.anchorMax = new Vector2(1f, 0f);
            etaTime.rectTransform.pivot = new Vector2(0.5f, 0f);
            etaTime.rectTransform.anchoredPosition = new Vector2(0f, 18f);

            etaPanel.gameObject.SetActive(false);
        }

        private void BuildPresentationPanel(RectTransform canvasRect)
        {
            GameObject go = new GameObject("KvPresentationOverlay", typeof(Image));
            go.transform.SetParent(canvasRect, false);
            presentationPanel = (RectTransform)go.transform;
            presentationPanel.anchorMin = new Vector2(0.5f, 0f);
            presentationPanel.anchorMax = new Vector2(0.5f, 0f);
            presentationPanel.pivot = new Vector2(0.5f, 0f);
            presentationPanel.sizeDelta = new Vector2(1180f, 92f);
            presentationPanel.anchoredPosition = new Vector2(0f, 46f);
            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0.65f);
            bg.raycastTarget = false;

            presentationText = KvTheme.CreateText(presentationPanel, "Text",
                "KazistovVv — платформа управления роботами", KvTheme.FontSizeTitle + 3,
                TextAnchor.MiddleCenter, Color.white);
            KvTheme.Stretch(presentationText.rectTransform, 16f, 16f, 8f, 22f);
            presentationText.horizontalOverflow = HorizontalWrapMode.Wrap;
            presentationText.verticalOverflow = VerticalWrapMode.Truncate;

            Text hint = KvTheme.CreateText(presentationPanel, "Hint",
                KvPresentationMode.KeyNext, KvTheme.FontSizeSmall, TextAnchor.LowerCenter,
                new Color(1f, 1f, 1f, 0.65f));
            KvTheme.Stretch(hint.rectTransform, 16f, 16f, 0f, 4f);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0.5f, 0f);
            hint.rectTransform.sizeDelta = new Vector2(-32f, 16f);

            presentationPanel.gameObject.SetActive(false);
        }

        // ================================================================== показ/скрытие

        public void Show(int tabIndex)
        {
            visible = true;
            if (window != null) window.gameObject.SetActive(true);
            SetTab(tabIndex);
            KazistovVvXr.KvWindowBelt.NotifyShown(WindowId);   // показали — снять с пояса
        }

        public void Toggle(int tabIndex)
        {
            if (visible && tab == tabIndex) Hide();
            else Show(tabIndex);
        }

        /// <summary>
        /// СКРЫТЬ ОКНО. По решению владельца [D22] скрытое окно уходит НА ПОЯС, а не исчезает:
        /// его можно достать плиткой. Если на поясе нет места — окно ОСТАЁТСЯ ВИДИМЫМ, а в журнал
        /// идёт строка «на поясе нет места»: молча потерять окно нельзя (иначе оператор решит,
        /// что закрыл его, и не найдёт).
        /// </summary>
        public void Hide()
        {
            if (KazistovVvXr.KvWindowBelt.Find(WindowId) != null)
            {
                if (!KazistovVvXr.KvWindowBelt.SendToBelt(WindowId))
                {
                    if (hub != null)
                        hub.Log.Add(KvLogKind.Ui, KazistovVvUI.KvLoc.T("xr.belt.full", "На поясе нет места"));
                    return;
                }
                return;   // SendToBelt уже скрыл окно и положил плитку
            }

            visible = false;
            if (window != null) window.gameObject.SetActive(false);
        }

        /// <summary>
        /// СВЕРНУТЬ ИЛИ РАЗВЕРНУТЬ (чёрточка в заголовке, [D21]). Свёрнутое окно остаётся на месте:
        /// заголовок и кнопки видны, содержимое скрыто, высота окна уменьшается до заголовка.
        /// </summary>
        public void ToggleCollapse()
        {
            SetCollapsed(!collapsed);
        }

        /// <summary>Задать свёрнутость явно (например, при возврате окна с пояса).</summary>
        public void SetCollapsed(bool value)
        {
            collapsed = value;

            if (scrollRoot != null) scrollRoot.SetActive(!collapsed);
            if (tabsRoot != null) tabsRoot.SetActive(!collapsed);
            if (window != null)
            {
                float height = collapsed ? 26f : windowHeight;
                window.sizeDelta = new Vector2(window.sizeDelta.x, height);
            }

            KazistovVvXr.KvWindowBelt.NotifyCollapsed(WindowId, collapsed);
            if (hub != null)
                hub.Log.Add(KvLogKind.Ui, KazistovVvUI.KvLoc.T("panel.features", "Панель функций") + ": " +
                    (collapsed
                        ? KazistovVvUI.KvLoc.T("xr.window.collapse", "Свернуть")
                        : KazistovVvUI.KvLoc.T("xr.window.expanded", "Открыто")));
        }

        public void ToggleEta()
        {
            EtaPanelEnabled = !EtaPanelEnabled;
            if (!EtaPanelEnabled && etaPanel != null) etaPanel.gameObject.SetActive(false);
            if (hub != null) hub.Log.Add(KvLogKind.Ui, "панель ETA: " + (EtaPanelEnabled ? "вкл" : "выкл"));
        }

        private void SetTab(int tabIndex)
        {
            tab = Mathf.Clamp(tabIndex, 0, TabTitles.Length - 1);
            if (tabStrip != null) tabStrip.Set(tab);
            if (titleText != null)
                titleText.text = KazistovVvUI.KvLoc.T("panel.features", "Панель функций").ToUpperInvariant() + " · " + TabTitlesLocalized()[tab] + "   (F5)";
            BuildTabContent();
        }

        // ================================================================== построение вкладок

        private void ClearContent()
        {
            dynamicTexts.Clear();
            dynamicProviders.Clear();
            joints.Clear();
            jointTexts.Clear();
            graph = null;

            if (content == null) return;
            for (int i = content.childCount - 1; i >= 0; i--)
                Destroy(content.GetChild(i).gameObject);
        }

        private void BuildTabContent()
        {
            if (hub == null || content == null) return;
            ClearContent();
            builtTab = tab;
            ReloadLists();

            switch (tab)
            {
                case 0: BuildRecordTab(); break;
                case 1: BuildPoseTab(); break;
                case 2: BuildJointTab(); break;
                case 3: BuildZoneTab(); break;
                case 4: BuildCompareTab(); break;
                case 5: BuildGraphTab(); break;
                case 6: BuildPerfTab(); break;
                case 7: BuildLogTab(); break;
                case 8: BuildScenarioTab(); break;
                default: BuildSessionTab(); break;
            }
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        private void ReloadLists()
        {
            records = KvRecordStore.LoadAll();
            poses = hub.Poses != null ? new List<KvPosePreset>(hub.Poses.All) : new List<KvPosePreset>();
            sessions = new List<KvSession>(hub.Sessions.All);
            recordIndex = Mathf.Clamp(recordIndex, 0, Mathf.Max(0, records.Count - 1));
            poseIndex = Mathf.Clamp(poseIndex, 0, Mathf.Max(0, poses.Count - 1));
            sessionIndex = Mathf.Clamp(sessionIndex, 0, Mathf.Max(0, sessions.Count - 1));
            zoneIndex = Mathf.Clamp(zoneIndex, 0, Mathf.Max(0, hub.Zones.Count - 1));
        }

        // ---- вкладка 0: ЗАПИСЬ (этап 1)
        private void BuildRecordTab()
        {
            Header("ЗАПИСЬ И ВОСПРОИЗВЕДЕНИЕ ТРАЕКТОРИИ (этап 1)");
            Note("Запись идёт с частотой " + hub.Recording.rate.ToString("0") + " Гц: углы суставов, " +
                 "время и TCP-координаты. Формат — JSON (читаемый, расширяемый). Папка: " +
                 FeatureStorage.RecordingsDir);

            Row(delegate { return "Состояние: " + (hub.Recording.IsRecording
                ? "● ИДЁТ ЗАПИСЬ · " + hub.Recording.Recording.SampleCount + " сэмплов · " +
                  hub.Recording.Recording.duration.ToString("0.0") + " с"
                : hub.Recording.IsPlaying
                    ? "▶ воспроизведение «" + (hub.Recording.Playing != null ? hub.Recording.Playing.name : "") +
                      "» · " + (hub.Recording.Paused ? "пауза" : (hub.Recording.Progress01 * 100f).ToString("0") + " %")
                    : "ожидание"); }, KvTheme.TextMain);

            Buttons("Кнопки",
                new[] { "● Записать / Стоп (R)", "▶ Проиграть / Пауза (P)", "■ Остановить" },
                new[]
                {
                    (Action)delegate { hub.ToggleRecord(); RequestRefresh(); },
                    delegate { hub.TogglePlayback(); RequestRefresh(); },
                    delegate { hub.Recording.StopAll("остановлено кнопкой"); RequestRefresh(); }
                });

            Buttons("Множитель скорости",
                new[] { "×0.25", "×0.5", "×1", "×2", "×4" },
                new[]
                {
                    (Action)delegate { hub.playbackSpeed = 0.25f; hub.Recording.SpeedMultiplier = 0.25f; },
                    delegate { hub.playbackSpeed = 0.5f; hub.Recording.SpeedMultiplier = 0.5f; },
                    delegate { hub.playbackSpeed = 1f; hub.Recording.SpeedMultiplier = 1f; },
                    delegate { hub.playbackSpeed = 2f; hub.Recording.SpeedMultiplier = 2f; },
                    delegate { hub.playbackSpeed = 4f; hub.Recording.SpeedMultiplier = 4f; }
                });
            Row(delegate { return "Текущий множитель: ×" + hub.playbackSpeed.ToString("0.00"); }, KvTheme.Accent);

            Toggle("Писать движение ФАНТОМА, а не робота", RecordSourcePhantom, delegate (bool value)
            {
                RecordSourcePhantom = value;
                Note("Источник записи: " + (value ? "фантом (его текущая поза)" : "реальный робот"));
            });

            Header("Записи (" + records.Count + ")");
            if (records.Count == 0) Note("Записей пока нет — нажмите «Записать».");

            for (int i = 0; i < records.Count; i++)
            {
                int index = i;
                KvTrajectoryRecord record = records[i];
                Row(delegate
                {
                    return (index == recordIndex ? "▸ " : "  ") + record.ShortLabel +
                           (string.IsNullOrEmpty(record.robot) ? "" : " · " + record.robot);
                }, i == recordIndex ? KvTheme.Accent : KvTheme.TextMain);
                Buttons(null,
                    new[] { "Выбрать", "Проиграть", "Показать фантомом", "Удалить" },
                    new[]
                    {
                        (Action)delegate { recordIndex = index; RequestRefresh(); },
                        delegate { recordIndex = index; hub.Recording.Play(record, hub.playbackSpeed); },
                        delegate { ShowRecordAsPhantom(record); },
                        delegate
                        {
                            KvRecordStore.Delete(record);
                            hub.Log.Add(KvLogKind.Record, "запись «" + record.name + "» удалена");
                            RequestRefresh();
                        }
                    });
            }
        }

        private void ShowRecordAsPhantom(KvTrajectoryRecord record)
        {
            if (hub.Flow() == null || hub.Flow().Phantoms == null)
            {
                hub.Log.Add(KvLogKind.Error, "фантом недоступен");
                return;
            }
            TrajectoryCore.PlannedTrajectory plan = record.ToPlannedTrajectory(hub.playbackSpeed);
            if (plan == null)
            {
                hub.Log.Add(KvLogKind.Error, "в записи меньше двух сэмплов");
                return;
            }
            bool ok = hub.Flow().Phantoms.ShowAlongPath(plan, hub.Flow().Validator.CopyCurrent(),
                hub.Flow().phantomMoveSpeed);
            hub.Log.Add(KvLogKind.Record, ok
                ? "запись «" + record.name + "» показана фантомом (как обычная траектория)"
                : "не удалось показать фантом по записи");
        }

        // ---- вкладка 1: ПОЗЫ (этап 2)
        private void BuildPoseTab()
        {
            Header("PRESET-ПОЗЫ (этап 2)");
            Note("«Сохранить текущую позу как…» снимает углы суставов активного робота " +
                 "(для SCARA — её 4 оси). «Перейти в позу» строит путь и плавно ведёт робота.");

            Row(delegate
            {
                return "Активный робот: " + (hub.Flow() != null && hub.Flow().Validator != null &&
                                             hub.Flow().Validator.Ready
                    ? hub.Flow().Validator.RobotName + " · осей " + hub.Flow().Validator.Dof
                    : "не определён");
            }, KvTheme.TextMain);

            Header("Сохранить позу");
            Buttons(null, KvPoseStore.SuggestedNames, BuildSavePoseActions());
            Buttons(null, new[] { "Сохранить как «Поза <время>»" },
                new[] { (Action)delegate { SavePose("Поза " + FeatureStorage.TimeStamp()); } });

            Header("Позы (" + poses.Count + ")");
            if (poses.Count == 0) Note("Поз пока нет.");

            for (int i = 0; i < poses.Count; i++)
            {
                int index = i;
                KvPosePreset pose = poses[i];
                Row(delegate
                {
                    return (index == poseIndex ? "▸ " : "  ") + pose.name +
                           " · " + pose.robot + " · TCP " + pose.TcpVector.y.ToString("0.000");
                }, i == poseIndex ? KvTheme.Accent : KvTheme.TextMain);
                Buttons(null, new[] { "Выбрать", "Перейти в позу", "Удалить" },
                    new[]
                    {
                        (Action)delegate { poseIndex = index; RequestRefresh(); },
                        delegate
                        {
                            poseIndex = index;
                            string result = hub.Poses.MoveTo(pose);
                            hub.Log.Add(KvLogKind.Pose, "поза «" + pose.name + "»: " + result);
                        },
                        delegate
                        {
                            hub.Poses.Delete(pose);
                            RequestRefresh();
                        }
                    });
            }
        }

        private Action[] BuildSavePoseActions()
        {
            string[] names = KvPoseStore.SuggestedNames;
            Action[] actions = new Action[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                string name = names[i];
                actions[i] = delegate { SavePose(name); };
            }
            return actions;
        }

        private void SavePose(string name)
        {
            KvPosePreset pose = hub.Poses.SaveCurrent(name);
            if (pose != null)
            {
                hub.Log.Add(KvLogKind.Pose, "поза «" + pose.name + "» сохранена");
                KazistovVvUIManager.SetPlanStatus("Поза «" + pose.name + "» сохранена", KvTheme.Ok);
            }
            RequestRefresh();
        }

        // ---- вкладка 2: СУСТАВЫ (этап 3)
        private void BuildJointTab()
        {
            Header("РУЧНОЕ УПРАВЛЕНИЕ СУСТАВАМИ (этап 3)");
            TrajectoryFlowController flow = hub.Flow();
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Note("Робот ещё не определён — наведите лазер на робота.");
                return;
            }

            int dof = flow.Validator.Dof;
            Note("Робот «" + flow.Validator.RobotName + "» · осей " + dof +
                 (dof <= 4 ? " (SCARA: J1, J2, Z)" : " (Axis1…Axis6)") +
                 ". Слайдер ведёт сустав напрямую; лимиты и запас показаны рядом.");

            for (int i = 0; i < dof; i++)
            {
                int index = i;
                float min = flow.Validator.Lower != null && i < flow.Validator.Lower.Length
                    ? flow.Validator.Lower[i] : -180f;
                float max = flow.Validator.Upper != null && i < flow.Validator.Upper.Length
                    ? flow.Validator.Upper[i] : 180f;
                bool prismatic = flow.Validator.IsPrismatic(i);
                double[] current = flow.Validator.CopyCurrent();
                float value = current != null && i < current.Length ? (float)current[i] : min;

                Text label = Row(delegate
                {
                    if (hub.Flow() == null || !hub.Flow().Validator.Ready) return "—";
                    double[] q = hub.Flow().Validator.CopyCurrent();
                    float now = q != null && index < q.Length ? (float)q[index] : 0f;
                    float margin = hub.Flow().Validator.LimitMargin(q);
                    return (prismatic ? "Z (м)" : "Ось " + (index + 1)) +
                           " · сейчас " + (prismatic ? now.ToString("0.000") + " м" : now.ToString("0.0") + "°") +
                           " · лимит [" + (prismatic ? min.ToString("0.000") + "…" + max.ToString("0.000") + " м"
                                                     : min.ToString("0") + "°…" + max.ToString("0") + "°") + "]" +
                           " · запас " + (prismatic ? "—" : margin.ToString("0.0") + "°");
                }, KvTheme.TextMain);

                Slider slider = MakeSlider(min, max, value, delegate (float v)
                {
                    ApplyJoint(index, v);
                });
                joints.Add(slider);
                dynamicTexts.Add(label);
                dynamicProviders.Add(delegate { return ""; });
            }

            Note("ВНИМАНИЕ: движение вручную выполняется напрямую через валидатор позы " +
                 "(как исполнитель траектории) — State Machine и лазеры не затрагиваются.");
        }

        private void ApplyJoint(int index, float value)
        {
            TrajectoryFlowController flow = hub.Flow();
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            if (flow.Motion != null && flow.Motion.IsRunning)
            {
                hub.Log.Add(KvLogKind.Error, "слайдеры: робот едет — сначала остановите движение");
                return;
            }

            double[] q = flow.Validator.CopyCurrent();
            if (q == null || index >= q.Length) return;
            q[index] = value;
            if (!flow.Validator.WithinLimits(q))
            {
                KazistovVvUIManager.SetPlanStatus("Слайдер: выход за лимит сустава " + (index + 1),
                    KvTheme.Error);
                return;
            }
            flow.Validator.Apply(q);
            if (hub.Recording.IsRecording) hub.Recording.SampleNow(true);
        }

        // ---- вкладка 3: ЗОНЫ (этап 5)
        private void BuildZoneTab()
        {
            Header("ЗОНЫ ЗАПРЕТА (этап 5)");
            Note("Зона создаётся в точке прицела. Полупрозрачный красный объём без коллайдера: " +
                 "проверка идёт ЯВНО по траекториям, существующее планирование не меняется.");

            Buttons("Создать зону",
                new[] { "Куб", "Сфера", "Цилиндр" },
                new[]
                {
                    (Action)delegate { hub.CreateZone(KvZoneShape.Box); RequestRefresh(); },
                    delegate { hub.CreateZone(KvZoneShape.Sphere); RequestRefresh(); },
                    delegate { hub.CreateZone(KvZoneShape.Cylinder); RequestRefresh(); }
                });

            Toggle("Отбрасывать опасные траектории (иначе — только пометка)",
                hub.Zones.discardDangerous, delegate (bool value) { hub.Zones.discardDangerous = value; });

            Buttons(null, new[] { "Показать все", "Скрыть все", "Удалить все" },
                new[]
                {
                    (Action)delegate { hub.Zones.SetAllVisible(true); },
                    delegate { hub.Zones.SetAllVisible(false); },
                    delegate { hub.Zones.Clear(); RequestRefresh(); }
                });

            Header("Зоны (" + hub.Zones.Count + ")");
            if (hub.Zones.Count == 0) Note("Зон запрета нет.");

            for (int i = 0; i < hub.Zones.Count; i++)
            {
                int index = i;
                KvZone zone = hub.Zones.Zones[i];
                Row(delegate
                {
                    return (index == zoneIndex ? "▸ " : "  ") + zone.Data.name + " · " +
                           zone.Data.ShapeLabel + " · " + zone.Data.SizeText;
                }, i == zoneIndex ? KvTheme.Accent : KvTheme.TextMain);

                Buttons(null,
                    new[] { "Выбрать", "Больше", "Меньше", "Вверх", "Вниз", "Сдвиг+0.1", "Сдвиг−0.1", "Скрыть", "Удалить" },
                    new[]
                    {
                        (Action)delegate { zoneIndex = index; RequestRefresh(); },
                        delegate { ScaleZone(zone, 1.15f); },
                        delegate { ScaleZone(zone, 0.87f); },
                        delegate { hub.Zones.Move(zone, zone.Data.Center + Vector3.up * 0.05f); },
                        delegate { hub.Zones.Move(zone, zone.Data.Center - Vector3.up * 0.05f); },
                        delegate { hub.Zones.Move(zone, zone.Data.Center + ShiftDirection() * 0.1f); },
                        delegate { hub.Zones.Move(zone, zone.Data.Center - ShiftDirection() * 0.1f); },
                        delegate { hub.Zones.SetVisible(zone, !zone.Data.visible); },
                        delegate { hub.Zones.Delete(zone); RequestRefresh(); }
                    });
            }

            if (hub.Zones.Count > 0)
                Buttons(null, new[] { "Проверить траектории на пересечение" },
                    new[] { (Action)delegate { CheckZonesNow(); } });
        }

        private static Vector3 ShiftDirection()
        {
            return new Vector3(0.1f, 0f, 0.1f).normalized * 1.414f;
        }

        private static void ScaleZone(KvZone zone, float factor)
        {
            if (zone == null || zone.Data == null) return;
            Vector3 size = zone.Data.Size * factor;
            FeatureHub hub = FeatureHub.Instance;
            if (hub != null) hub.Zones.Resize(zone, size);
        }

        private void CheckZonesNow()
        {
            TrajectoryFlowController flow = hub.Flow();
            if (flow == null || flow.State.candidates.Count == 0)
            {
                hub.Log.Add(KvLogKind.Zone, "проверять нечего: траекторий нет");
                return;
            }
            int dangerous = 0;
            for (int i = 0; i < flow.State.candidates.Count; i++)
            {
                TrajectoryCandidate candidate = flow.State.candidates[i];
                if (candidate == null || candidate.plan == null) continue;
                KvZoneHit hit = hub.Zones.CheckPlan(candidate.plan, flow.Validator, flow.Validator.Dof);
                KvZoneMarks.Set(candidate, hit.hit, hit.Describe(), hit.penetration);
                if (hit.hit) dangerous++;
            }
            hub.Log.Add(KvLogKind.Zone, "проверка зон: опасных траекторий " + dangerous +
                                        " из " + flow.State.candidates.Count);
            KazistovVvUIManager.SetPlanStatus("Зоны запрета: опасных траекторий " + dangerous +
                                              " из " + flow.State.candidates.Count,
                dangerous > 0 ? new Color(1f, 0.35f, 0.25f) : KvTheme.Ok);
            if (KazistovVvUIManager.Instance != null)
                KazistovVvUIManager.Instance.RebuildTree(true);
        }

        // ---- вкладка 4: СРАВНЕНИЕ (этап 6)
        private void BuildCompareTab()
        {
            Header("СРАВНЕНИЕ ТРАЕКТОРИЙ (этап 6)");
            TrajectoryFlowController flow = hub.Flow();
            if (flow == null || flow.State.candidates.Count == 0)
            {
                Note("Траекторий нет: выберите точку красным лазером (ЛКМ), затем зелёным — вариант.");
                return;
            }

            Note("Отметьте две траектории: выберите узел «Траектория N» в дереве и нажмите " +
                 "«Отметить» — она получит метку A, следующая — B. Метки видны в дереве.");

            Buttons(null, new[] { "Отметить выбранную (A/B)", "Сбросить метки", "Сравнить с лучшей" },
                new[]
                {
                    (Action)delegate { hub.MarkForComparison(); RequestRefresh(); },
                    delegate { hub.Comparison.Reset(); RequestRefresh(); },
                    delegate
                    {
                        hub.Comparison.SlotA = 0;
                        hub.Comparison.SlotB = flow.State.selectedTrajectory >= 0
                            ? flow.State.selectedTrajectory : Mathf.Min(1, flow.State.candidates.Count - 1);
                        RequestRefresh();
                    }
                });

            Row(delegate
            {
                return "A: " + SlotLabel(hub.Comparison.SlotA) + "      B: " + SlotLabel(hub.Comparison.SlotB);
            }, KvTheme.Accent);

            List<string[]> rows = hub.Comparison.SideBySide();
            for (int i = 0; i < rows.Count; i++)
            {
                string[] row = rows[i];
                TableRow(row[0], row[1], row[2]);
            }

            Row(delegate { return hub.Comparison.Verdict(); }, KvTheme.TextMain);

            Buttons(null, new[] { "Переключиться на A", "Переключиться на B" },
                new[]
                {
                    (Action)delegate
                    {
                        string result = hub.Comparison.Activate(hub.Comparison.SlotA)
                            ? "выбрана траектория A" : "переключение на A недоступно";
                        hub.Log.Add(KvLogKind.Trajectory, result);
                    },
                    delegate
                    {
                        string result = hub.Comparison.Activate(hub.Comparison.SlotB)
                            ? "выбрана траектория B" : "переключение на B недоступно";
                        hub.Log.Add(KvLogKind.Trajectory, result);
                    }
                });
        }

        private string SlotLabel(int index)
        {
            TrajectoryFlowController flow = hub.Flow();
            if (flow == null || index < 0 || index >= flow.State.candidates.Count) return "—";
            TrajectoryCandidate candidate = flow.State.candidates[index];
            return "№" + (index + 1) + " (" + candidate.lengthM.ToString("0.00") + " ю)";
        }

        // ---- вкладка 5: ГРАФИКИ (этап 7)
        private void BuildGraphTab()
        {
            Header("ГРАФИКИ УГЛОВ СУСТАВОВ (этап 7)");
            TrajectoryFlowController flow = hub.Flow();
            if (flow == null || flow.State.candidates.Count == 0)
            {
                Note("Траекторий нет — график построить не по чему.");
                return;
            }

            Toggle("Наложить вторую траекторию (A и B из сравнения)", graphOverlay, delegate (bool value)
            {
                graphOverlay = value;
                RequestRefresh();
            });

            GameObject graphGo = new GameObject("GraphHost");
            graphGo.transform.SetParent(content, false);
            RectTransform host = (RectTransform)graphGo.transform;
            KvWidgets.Fit(graphGo, 720f, 300f);

            graph = graphGo.AddComponent<KvJointGraph>();
            graph.Build(host, 720f, 290f);

            int index = flow.State.selectedTrajectory >= 0 ? flow.State.selectedTrajectory : 0;
            TrajectoryCandidate candidate = flow.State.candidates[index];

            if (graphOverlay && hub.Comparison.Ready)
            {
                TrajectoryCandidate a = flow.State.candidates[hub.Comparison.SlotA];
                TrajectoryCandidate b = flow.State.candidates[hub.Comparison.SlotB];
                graph.SetPlanPair(a != null ? a.plan : null, "A · " + (a != null ? a.label : ""),
                    b != null ? b.plan : null, "B · " + (b != null ? b.label : ""));
            }
            else
            {
                graph.SetPlan(candidate != null ? candidate.plan : null,
                    candidate != null ? candidate.label : "");
            }

            Buttons(null, new[] { "Лучшая (0)", "Выбранная", "Следующая" },
                new[]
                {
                    (Action)delegate { ShowGraphFor(0); },
                    delegate { ShowGraphFor(hub.Flow().State.selectedTrajectory >= 0
                        ? hub.Flow().State.selectedTrajectory : 0); },
                    delegate { ShowGraphFor(Mathf.Min(hub.Flow().State.candidates.Count - 1, 1)); }
                });
        }

        private int graphIndex;
        private void ShowGraphFor(int index)
        {
            graphIndex = index;
            BuildTabContent();
        }

        // ---- вкладка 6: ЗАМЕР (этап 14)
        private void BuildPerfTab()
        {
            Header("ЗАМЕР ПРОИЗВОДИТЕЛЬНОСТИ ПЛАНИРОВЩИКА (этап 14)");
            Note("Считается по каждому прогону генерации вариантов. История — " +
                 "Logs/planner_runs.json, сводка дублируется в журнал действий.");

            List<string> lines = hub.Performance.Lines();
            for (int i = 0; i < lines.Count; i++) Note(lines[i]);

            Row(delegate
            {
                return "Среднее время генерации по истории: " +
                       hub.Performance.AverageSeconds().ToString("0.000") + " с (прогонов " +
                       hub.Performance.History.Count + ")";
            }, KvTheme.Accent);

            Buttons(null, new[] { "Обновить" }, new[] { (Action)delegate { RequestRefresh(); } });
        }

        // ---- вкладка 7: ЖУРНАЛ (этап 13)
        private void BuildLogTab()
        {
            Header("ЖУРНАЛ ДЕЙСТВИЙ (этап 13)");

            if (logFilterStrip == null || logFilterStrip.gameObject == null)
            {
                GameObject filterGo = new GameObject("LogFilter");
                filterGo.transform.SetParent(content, false);
                KvWidgets.Fit(filterGo, 720f, 22f);
                logFilterStrip = KvWidgets.Segmented((RectTransform)filterGo.transform, "Filter",
                    new[] { "Все", "Точка", "Траектория", "Движение", "Стоп", "Зоны", "Ошибки" },
                    logFilter, delegate (int value)
                    {
                        logFilter = value;
                        ApplyLogFilter();
                        BuildTabContent();
                    }, 20f);
                KvTheme.Stretch((RectTransform)logFilterStrip.transform, 2f, 2f, 2f, 2f);
            }

            Note("Файл журнала: " + hub.Log.FilePath);
            Row(delegate { return "Записей: " + hub.Log.Count + " · показано: " + hub.Log.Filtered().Count; },
                KvTheme.TextDim);

            Buttons(null, new[] { "Выгрузить в файл", "Очистить" },
                new[]
                {
                    (Action)delegate { hub.Log.Export(); },
                    delegate { hub.Log.Clear(); RequestRefresh(); }
                });

            List<KvLogEntry> tail = hub.Log.Tail(200);
            for (int i = tail.Count - 1; i >= 0; i--)
            {
                KvLogEntry entry = tail[i];
                Note(entry.Line, KvActionLog.KindColor(entry.kind));
            }
        }

        private int logFilter = -1;
        private KvSegmented logFilterStrip;

        private void ApplyLogFilter()
        {
            switch (logFilter)
            {
                case 1: hub.Log.Filter = KvLogKind.Point; break;
                case 2: hub.Log.Filter = KvLogKind.Trajectory; break;
                case 3: hub.Log.Filter = KvLogKind.Motion; break;
                case 4: hub.Log.Filter = KvLogKind.Stop; break;
                case 5: hub.Log.Filter = KvLogKind.Zone; break;
                case 6: hub.Log.Filter = KvLogKind.Error; break;
                default: hub.Log.Filter = null; break;
            }
        }

        // ---- вкладка 8: СЦЕНАРИИ (этап 18)
        private void BuildScenarioTab()
        {
            Header("МЕНЕДЖЕР СЦЕНАРИЕВ (этап 18)");
            Note("Сценарий — набор шагов, выполняемых последовательно. Пауза и отмена — ниже.");

            for (int i = 0; i < hub.Scenarios.All.Count; i++)
            {
                int index = i;
                KvScenario scenario = hub.Scenarios.All[i];
                Row(delegate
                {
                    return (index == scenarioIndex ? "▸ " : "  ") + scenario.Title +
                           " · шагов " + scenario.Steps.Count;
                }, i == scenarioIndex ? KvTheme.Accent : KvTheme.TextMain);
                Note("    " + scenario.Description, KvTheme.TextDim);
            }

            Buttons(null, new[] { "Выбрать следующий", "▶ Запустить", "❚❚ Пауза", "■ Отмена" },
                new[]
                {
                    (Action)delegate
                    {
                        scenarioIndex = (scenarioIndex + 1) % Mathf.Max(1, hub.Scenarios.All.Count);
                        RequestRefresh();
                    },
                    delegate { RunSelectedScenario(); },
                    delegate { hub.Scenarios.TogglePause(); RequestRefresh(); },
                    delegate { hub.Scenarios.Stop("отменён оператором"); RequestRefresh(); }
                });

            Row(delegate
            {
                if (!hub.Scenarios.Running) return "Сценарий не запущен";
                return "Идёт «" + hub.Scenarios.RunningTitle + "» · шаг " +
                       (hub.Scenarios.StepIndex + 1) + "/" + hub.Scenarios.StepCount +
                       (hub.Scenarios.Paused ? " · ПАУЗА" : "") +
                       "\n     " + hub.Scenarios.CurrentStepText;
            }, KvTheme.Ok);
        }

        /// <summary>Запустить выбранный в панели сценарий.</summary>
        public void RunSelectedScenario()
        {
            if (hub == null || hub.Scenarios.All.Count == 0) return;
            scenarioIndex = Mathf.Clamp(scenarioIndex, 0, hub.Scenarios.All.Count - 1);
            hub.Scenarios.Start(hub.Scenarios.All[scenarioIndex]);
            RequestRefresh();
        }

        // ---- вкладка 9: СЕССИИ (этап 20)
        private void BuildSessionTab()
        {
            Header("СОХРАНЕНИЕ / ЗАГРУЗКА СЕССИИ (этап 20)");
            Note("Сессия хранит: позы роботов, выбранную точку, зоны запрета, ссылки на записи " +
                 "и позы, состояние переключателей. Формат расширяемый — новые поля не ломают " +
                 "старые файлы.");

            Buttons(null, new[] { "Сохранить сессию", "Загрузить последнюю", "Обновить список" },
                new[]
                {
                    (Action)delegate
                    {
                        hub.Sessions.Save("Сессия " + FeatureStorage.TimeStamp());
                        RequestRefresh();
                    },
                    delegate
                    {
                        KvSession newest = hub.Sessions.Newest;
                        if (newest == null) hub.Log.Add(KvLogKind.Error, "сессий нет");
                        else hub.Sessions.Load(newest);
                        RequestRefresh();
                    },
                    delegate { hub.Sessions.Reload(); RequestRefresh(); }
                });

            Header("Сохранённые сессии (" + sessions.Count + ")");
            if (sessions.Count == 0) Note("Сессий нет.");

            for (int i = 0; i < sessions.Count; i++)
            {
                int index = i;
                KvSession session = sessions[i];
                Row(delegate
                {
                    return (index == sessionIndex ? "▸ " : "  ") + session.Summary;
                }, i == sessionIndex ? KvTheme.Accent : KvTheme.TextMain);
                Buttons(null, new[] { "Выбрать", "Загрузить", "Удалить" },
                    new[]
                    {
                        (Action)delegate { sessionIndex = index; RequestRefresh(); },
                        delegate { hub.Sessions.Load(session); RequestRefresh(); },
                        delegate { hub.Sessions.Delete(session); RequestRefresh(); }
                    });
            }
        }

        // ================================================================== кадровое обновление

        public void RequestRefresh()
        {
            builtTab = -1;
            if (visible) BuildTabContent();
        }

        /// <summary>Обновление динамических подписей (частота — `refreshInterval`).</summary>
        public void Refresh()
        {
            if (hub == null) return;
            float dt = Time.unscaledDeltaTime;

            // --- ETA (этап 12)
            if (etaPanel != null)
            {
                bool show = EtaPanelEnabled && hub.EtaVisible;
                if (etaPanel.gameObject.activeSelf != show) etaPanel.gameObject.SetActive(show);
                if (show)
                {
                    etaLabel.text = (string.IsNullOrEmpty(hub.EtaLabel) ? "ТРАЕКТОРИЯ" : hub.EtaLabel) +
                                    " · осталось " + hub.EtaRemaining.ToString("0.0") + " с";
                    etaTime.text = "прогресс " + (hub.EtaProgress * 100f).ToString("0") + " % · всего " +
                                   hub.EtaTotal.ToString("0.0") + " с";
                    etaFill.fillAmount = Mathf.Clamp01(hub.EtaProgress);
                    etaFill.color = hub.EtaProgress > 0.99f ? KvTheme.Ok : KvTheme.Accent;
                }
            }

            // --- презентационный режим (этап 19)
            if (presentationPanel != null)
            {
                bool show = hub.Presentation != null && hub.Presentation.Active;
                if (presentationPanel.gameObject.activeSelf != show)
                    presentationPanel.gameObject.SetActive(show);
                if (show && presentationText != null)
                {
                    presentationText.text = hub.Presentation.CurrentLine + "\n<size=11><color=#FFFFFFAA>" +
                                            (hub.Presentation.LineIndex + 1) + "/" +
                                            hub.Presentation.LineCount + "</color></size>";
                }
            }

            if (!visible) return;

            refreshTimer -= dt;
            if (refreshTimer > 0f) return;
            refreshTimer = Mathf.Max(0.05f, refreshInterval);

            for (int i = 0; i < dynamicTexts.Count; i++)
            {
                Text text = dynamicTexts[i];
                if (text == null) continue;
                Func<string> provider = i < dynamicProviders.Count ? dynamicProviders[i] : null;
                if (provider == null) continue;
                string value = provider();
                if (!string.IsNullOrEmpty(value) && text.text != value) text.text = value;
            }

            // список записей/поз/зон/сессий мог измениться — пересобираем редко и только при расхождении
            if (hub.Recording != null && (hub.Recording.IsRecording || hub.Recording.IsPlaying)) return;
        }

        // ================================================================== мелкие конструкторы UI

        private void Header(string text)
        {
            KvWidgets.SectionHeader(content, text, 20f);
        }

        private void Note(string text)
        {
            Note(text, KvTheme.TextDim);
        }

        private void Note(string text, Color color)
        {
            Text label = KvTheme.CreateText(content, "Note", text, KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            int lines = 1 + text.Length / 96;
            KvWidgets.Fit(label.gameObject, 720f, 14f * lines);
        }

        private Text Row(Func<string> provider, Color color)
        {
            Text label = KvTheme.CreateText(content, "Row", provider(), KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, color);
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            KvWidgets.Fit(label.gameObject, 720f, 16f);
            dynamicTexts.Add(label);
            dynamicProviders.Add(provider);
            return label;
        }

        private void TableRow(string caption, string left, string right)
        {
            RectTransform row = KvWidgets.CreateRow(content, "TableRow", 16f, 4f, TextAnchor.MiddleLeft);
            KvWidgets.Fit(row.gameObject, 720f, 16f);

            Text cap = KvTheme.CreateText(row, "Cap", caption, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            KvWidgets.Fit(cap.gameObject, 250f, 16f);
            Text a = KvTheme.CreateText(row, "A", left, KvTheme.FontSizeSmall, TextAnchor.MiddleLeft,
                KvTheme.TextMain);
            KvWidgets.Fit(a.gameObject, 180f, 16f);
            Text b = KvTheme.CreateText(row, "B", right, KvTheme.FontSizeSmall, TextAnchor.MiddleLeft,
                KvTheme.TextMain);
            KvWidgets.Fit(b.gameObject, 180f, 16f);
        }

        private void Toggle(string label, bool value, Action<bool> onChanged)
        {
            RectTransform row = KvWidgets.CreateRow(content, "ToggleRow", 18f, 4f, TextAnchor.MiddleLeft);
            KvWidgets.Fit(row.gameObject, 720f, 18f);
            KvWidgets.Switch(row, label, value, onChanged);
        }

        private void Buttons(string caption, string[] labels, Action[] actions)
        {
            if (labels == null || actions == null) return;
            if (!string.IsNullOrEmpty(caption)) Note(caption, KvTheme.TextDim);

            RectTransform row = KvWidgets.CreateRow(content, "Buttons", 22f, 4f, TextAnchor.MiddleLeft);
            KvWidgets.Fit(row.gameObject, 720f, 22f);

            int count = Mathf.Min(labels.Length, actions.Length);
            for (int i = 0; i < count; i++)
            {
                string label = labels[i];
                Action action = actions[i];
                Button button = KvTheme.CreateSmallButton(row, "Btn" + i, label, delegate
                {
                    if (action != null) action();
                });
                float width = Mathf.Clamp(label.Length * 7f + 14f, 44f, 220f);
                KvWidgets.Fit(button.gameObject, width, 20f);
            }
        }

        private Slider MakeSlider(float min, float max, float value, UnityEngine.Events.UnityAction<float> onChanged)
        {
            GameObject go = new GameObject("Slider", typeof(Slider));
            go.transform.SetParent(content, false);
            RectTransform rect = (RectTransform)go.transform;
            KvWidgets.Fit(go, 700f, 20f);

            GameObject bgGo = new GameObject("Background", typeof(Image));
            bgGo.transform.SetParent(go.transform, false);
            RectTransform bgRect = (RectTransform)bgGo.transform;
            bgRect.anchorMin = new Vector2(0f, 0.35f);
            bgRect.anchorMax = new Vector2(1f, 0.65f);
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            bgGo.GetComponent<Image>().color = KvTheme.InputBg;

            GameObject fillAreaGo = new GameObject("FillArea", typeof(Image));
            fillAreaGo.transform.SetParent(go.transform, false);
            RectTransform fillArea = (RectTransform)fillAreaGo.transform;
            fillArea.anchorMin = new Vector2(0f, 0.35f);
            fillArea.anchorMax = new Vector2(1f, 0.65f);
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = new Vector2(-14f, 0f);
            fillAreaGo.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0f);
            fillAreaGo.GetComponent<Image>().raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(fillAreaGo.transform, false);
            RectTransform fill = (RectTransform)fillGo.transform;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.sizeDelta = new Vector2(14f, 0f);
            fillGo.GetComponent<Image>().color = KvTheme.Accent;
            fillGo.GetComponent<Image>().raycastTarget = false;

            // ИСПРАВЛЕНО 15.09.2026 (ЭТАП 2 сессии UX/UI): объект создавался БЕЗ RectTransform
            // (`new GameObject("HandleArea")` даёт обычный Transform), и приведение
            // `(RectTransform)handleAreaGo.transform` падало с InvalidCastException —
            // вкладка «СУСТАВЫ» (команда `joints.panel`) не открывалась вообще.
            GameObject handleAreaGo = new GameObject("HandleArea", typeof(RectTransform));
            handleAreaGo.transform.SetParent(go.transform, false);
            RectTransform handleArea = (RectTransform)handleAreaGo.transform;
            handleArea.anchorMin = new Vector2(0f, 0f);
            handleArea.anchorMax = new Vector2(1f, 1f);
            handleArea.offsetMin = Vector2.zero;
            handleArea.offsetMax = new Vector2(-14f, 0f);

            GameObject handleGo = new GameObject("Handle", typeof(Image));
            handleGo.transform.SetParent(handleAreaGo.transform, false);
            RectTransform handle = (RectTransform)handleGo.transform;
            handle.sizeDelta = new Vector2(14f, 0f);
            handleGo.GetComponent<Image>().color = KvTheme.ButtonBg;

            Slider slider = go.GetComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleGo.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = Mathf.Min(min, max);
            slider.maxValue = Mathf.Max(min, max);
            slider.wholeNumbers = false;
            slider.value = Mathf.Clamp(value, slider.minValue, slider.maxValue);
            if (onChanged != null) slider.onValueChanged.AddListener(onChanged);
            return slider;
        }
    }
}
