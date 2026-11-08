using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>Куда пристыкована панель.</summary>
    public enum KvDockSide
    {
        Left = 0,
        Right = 1,
        Bottom = 2,
        Float = 3
    }

    /// <summary>
    /// Dockable-панель в стиле FreeCAD (ЭТАПЫ 3–4):
    ///   • перетаскивание за ЗАГОЛОВОК: панель отклеивается, следует за курсором,
    ///     при поднесении к краю подсвечивается ЗОНА прикрепления, при отпускании
    ///     панель прилипает к краю (left / right / bottom), а в центре становится
    ///     ПЛАВАЮЩИМ окном;
    ///   • кнопка ОТКРЕПЛЕНИЯ/ПРИКРЕПЛЕНИЯ (иконка dock ⟷ undock);
    ///   • кнопка ЗАКРЫТИЯ (✕) сворачивает окно в боковую «полочку» (клик по ней возвращает);
    ///   • ПОЛЗУНКИ ПО КРАЯМ И УГЛАМ: 4 грани + 4 угла, курсор меняется при наведении,
    ///     минимум 150×100, максимум — родительский канвас;
    ///   • раскладка (край, толщина, размер, позиция, свёрнутость) хранится в PlayerPrefs
    ///     (<see cref="KvLayoutStore"/>) — при следующем запуске окна на своих местах.
    ///
    /// Сама панель НЕ занимается раскладкой всего интерфейса — она сообщает об изменениях
    /// событием <see cref="LayoutDirty"/>, а раскладку делает <see cref="KazistovVvUIManager"/>.
    /// </summary>
    public class KvDockPanel : MonoBehaviour
    {
        /// <summary>Заголовок панели.</summary>
        public string Title { get; private set; }
        /// <summary>Текущая пристыковка.</summary>
        public KvDockSide Side { get; private set; }
        /// <summary>Толщина (ширина для левой/правой, высота для нижней), px канваса.</summary>
        public float Thickness { get; private set; }
        /// <summary>Свёрнута (остаётся только заголовок).</summary>
        public bool Collapsed { get; private set; }
        /// <summary>Свёрнута в боковую «полочку» (кнопка ✕).</summary>
        public bool InRail { get; private set; }
        /// <summary>Панель показана.</summary>
        public bool Shown { get { return root != null && root.gameObject.activeSelf; } }
        /// <summary>Плавающее окно (не пристыковано).</summary>
        public bool Floating { get { return Side == KvDockSide.Float; } }

        /// <summary>
        /// ПАНЕЛЬ ЗАПРОШЕНА ОПЕРАТОРОМ В ЭТОМ СЕАНСЕ (R6-1, третий круг приёмки 12.11.2026).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ ПРИЗНАК. `Shown` приходит ещё и из сохранённой раскладки `PlayerPrefs`:
        /// панель может быть «показана» с прошлого запуска, и оператор её не открывал. А в AR и в
        /// шлеме десктопный интерфейс штатно погашен сторожем. Если бы мы оставляли рисование по
        /// одному `Shown`, то на планшете и в шлеме поверх камеры висел бы десктопный оверлей,
        /// которого владелец как раз не хочет. Запрошенная же панель — прямое указание оператора,
        /// её гасить нельзя ни при каких условиях.
        ///
        /// КТО СТАВИТ. Только `KazistovVvUIManager.ShowPanel/TogglePanel` — то есть путь команды.
        /// Снятие автоматическое: как только панель скрыли, признак сбрасывается.
        /// </summary>
        public bool OperatorRequested { get; private set; }

        /// <summary>Пометить панель как запрошенную оператором (или снять признак).</summary>
        public void MarkOperatorRequested(bool requested)
        {
            OperatorRequested = requested;
        }

        /// <summary>Геометрия панели изменилась — пересобрать раскладку.</summary>
        public event Action LayoutDirty;

        /// <summary>Пользователь закрыл панель (менеджер снимает галочку в меню «Вид»).</summary>
        public event Action Closed;

        /// <summary>Раскладка панели изменилась (для сохранения в PlayerPrefs).</summary>
        public event Action<string> LayoutChanged;

        /// <summary>Корень панели.</summary>
        public RectTransform Root { get { return root; } }
        /// <summary>Контейнер содержимого панели.</summary>
        public RectTransform Body { get { return body; } }
        /// <summary>Заголовок (для подписи/иконки).</summary>
        public RectTransform HeaderRect { get { return header; } }
        /// <summary>Ключ раскладки (для PlayerPrefs).</summary>
        public string LayoutKey { get; private set; }

        /// <summary>Минимальная/максимальная толщина пристыкованной панели.</summary>
        public float minThickness = 120f;
        public float maxThickness = 720f;
        /// <summary>Минимальный размер плавающего окна (ТЗ ЭТАПА 4: 150×100).</summary>
        public const float MinFloatWidth = 150f;
        public const float MinFloatHeight = 100f;
        /// <summary>Высота заголовка.</summary>
        public const float HeaderHeight = 19f;
        /// <summary>Высота свёрнутой панели.</summary>
        public const float CollapsedHeight = HeaderHeight + 2f;
        /// <summary>Ширина «полочки» (панель, свёрнутая кнопкой ✕).</summary>
        public const float RailThickness = 20f;
        /// <summary>Толщина зоны захвата границы (px).</summary>
        public const float EdgeGrip = 5f;
        /// <summary>Сторона углового маркера (px).</summary>
        public const float CornerGrip = 9f;

        private RectTransform root;
        private RectTransform header;
        private RectTransform body;
        private RectTransform splitter;
        private Image bodyBg;
        private Text titleText;
        private Image titleIcon;
        private string iconId = "properties";
        private KvIconButton collapseButton;
        private KvIconButton dockButton;
        private KvIconButton closeButton;
        private Canvas canvas;
        private RectTransform canvasRect;
        private float expandableThickness;
        private float floatX = 80f, floatY = 140f;
        private float floatW = 320f, floatH = 300f;
        private bool dockResizeActive;
        private Vector2 dockResizeStartPoint;
        private float dockResizeStartThickness;
        private readonly KvResizeHandle[] handles = new KvResizeHandle[8];

        // ------------------------------------------------------------------ сборка

        /// <summary>Собрать панель внутри канваса.</summary>
        public void Build(RectTransform parent, Canvas owner, string title, string iconId,
            KvDockSide side, float thickness, string layoutKey = null)
        {
            canvas = owner;
            canvasRect = parent;
            Title = title;
            Side = side;
            Thickness = thickness;
            expandableThickness = thickness;
            LayoutKey = layoutKey;
            this.iconId = string.IsNullOrEmpty(iconId) ? "properties" : iconId;

            GameObject go = new GameObject("Dock_" + title, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            root = (RectTransform)go.transform;
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(thickness, 300f);

            bodyBg = go.GetComponent<Image>();
            bodyBg.sprite = KvTheme.WhiteSprite;
            bodyBg.color = KvTheme.PanelBg;
            bodyBg.raycastTarget = true;   // панель перехватывает клики (не «протекают» в сцену)

            Outline frame = go.AddComponent<Outline>();
            frame.effectColor = KvTheme.Border;
            frame.effectDistance = new Vector2(1f, -1f);
            frame.useGraphicAlpha = false;

            // --- заголовок
            GameObject headerGo = new GameObject("Header", typeof(Image));
            headerGo.transform.SetParent(go.transform, false);
            header = (RectTransform)headerGo.transform;
            header.anchorMin = new Vector2(0f, 1f);
            header.anchorMax = new Vector2(1f, 1f);
            header.pivot = new Vector2(0.5f, 1f);
            header.sizeDelta = new Vector2(0f, HeaderHeight);
            header.anchoredPosition = Vector2.zero;
            Image headerBg = headerGo.GetComponent<Image>();
            headerBg.sprite = KvTheme.WhiteSprite;
            headerBg.color = KvTheme.PanelHeader;
            KvDockDrag drag = headerGo.AddComponent<KvDockDrag>();
            drag.Bind(this, canvasRect);

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(headerGo.transform, false);
            titleIcon = iconGo.GetComponent<Image>();
            titleIcon.sprite = KvIcons.Get(this.iconId, 13);
            titleIcon.color = KvTheme.TextDim;
            titleIcon.raycastTarget = false;
            titleIcon.rectTransform.anchorMin = titleIcon.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            titleIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
            titleIcon.rectTransform.sizeDelta = new Vector2(13f, 13f);
            titleIcon.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            if (titleIcon.sprite == null) iconGo.SetActive(false);

            titleText = KvTheme.CreateText(header, "Title", title, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            titleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(21f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-64f, 0f);

            // --- кнопки заголовка (справа): свернуть / открепить-прикрепить / закрыть (полочка)
            collapseButton = HeaderButton(header, "Collapse", "collapse", -48f, ToggleCollapsed,
                KvLoc.T("panel.collapse", "Свернуть/развернуть панель"));
            dockButton = HeaderButton(header, "Dock", "undock", -32f, ToggleFloat,
                KvLoc.T("panel.undock", "Открепить окно (плавающее) / прикрепить к краю"));
            closeButton = HeaderButton(header, "Close", "close", -16f, ToRail,
                KvLoc.T("panel.rail", "Свернуть окно в боковую панель (клик по полочке вернёт)"));

            // --- содержимое
            GameObject bodyGo = new GameObject("Body", typeof(RectTransform));
            bodyGo.transform.SetParent(go.transform, false);
            body = (RectTransform)bodyGo.transform;
            StretchBody();

            // --- кромка-разделитель (изменение толщины пристыкованной панели)
            GameObject splitGo = new GameObject("Splitter", typeof(Image));
            splitGo.transform.SetParent(go.transform, false);
            splitter = (RectTransform)splitGo.transform;
            Image splitImg = splitGo.GetComponent<Image>();
            splitImg.sprite = KvTheme.WhiteSprite;
            splitImg.color = new Color(0f, 0f, 0f, 0.01f);   // «почти прозрачная», но ловит мышь
            splitImg.raycastTarget = true;
            KvSplitter sp = splitGo.AddComponent<KvSplitter>();
            sp.Bind(this, canvasRect);

            // --- ПОЛЗУНКИ ПО КРАЯМ И УГЛАМ (ЭТАП 4): 4 грани + 4 угла
            BuildHandles();

            ApplyLayout(0f, 0f, 0f, 0f);
        }

        private void BuildHandles()
        {
            KvResizeEdge[] edges =
            {
                KvResizeEdge.Left, KvResizeEdge.Right, KvResizeEdge.Top, KvResizeEdge.Bottom,
                KvResizeEdge.TopLeft, KvResizeEdge.TopRight,
                KvResizeEdge.BottomLeft, KvResizeEdge.BottomRight
            };
            for (int i = 0; i < edges.Length; i++)
            {
                GameObject go = new GameObject("Resize_" + edges[i], typeof(Image));
                go.transform.SetParent(root, false);
                Image img = go.GetComponent<Image>();
                img.sprite = KvTheme.WhiteSprite;
                img.color = new Color(0f, 0f, 0f, 0.001f);     // невидимая, но ловит мышь
                img.raycastTarget = true;
                KvResizeHandle handle = go.AddComponent<KvResizeHandle>();
                handle.Bind(this, canvasRect, edges[i]);
                handles[i] = handle;
            }
        }

        private KvIconButton HeaderButton(RectTransform parent, string name, string icon,
            float x, Action action, string tip)
        {
            KvIconButton b = KvWidgets.IconButton(parent, name, icon, 13f, action);
            RectTransform rt = (RectTransform)b.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(13f, 13f);
            rt.anchoredPosition = new Vector2(x, 0f);
            LayoutElement le = b.GetComponent<LayoutElement>();
            if (le != null) Destroy(le);
            b.SetChecked(false);
            b.Icon.color = KvTheme.TextDim;
            b.Background.color = new Color(0f, 0f, 0f, 0f);
            b.Tooltip.Set(tip, "", "");
            return b;
        }

        private void StretchBody()
        {
            if (body == null) return;
            if (Collapsed || InRail)
            {
                body.gameObject.SetActive(false);
                if (splitter != null) splitter.gameObject.SetActive(false);
                SetHandlesActive(false);
                return;
            }
            body.gameObject.SetActive(true);
            if (splitter != null) splitter.gameObject.SetActive(true);
            SetHandlesActive(true);
            body.anchorMin = Vector2.zero;
            body.anchorMax = Vector2.one;
            body.pivot = new Vector2(0.5f, 0.5f);
            body.offsetMin = new Vector2(1f, 1f);
            body.offsetMax = new Vector2(-1f, -HeaderHeight);
        }

        private void SetHandlesActive(bool active)
        {
            for (int i = 0; i < handles.Length; i++)
            {
                if (handles[i] == null) continue;
                // Пристыкованная панель тянется только за ВНУТРЕННЮЮ кромку,
                // поэтому «внешние» маркеры у неё выключены (ЭТАП 4).
                bool usable = active && (Side == KvDockSide.Float || handles[i].IsInnerFor(Side));
                handles[i].gameObject.SetActive(usable);
            }
        }

        // ------------------------------------------------------------------ состояние

        /// <summary>Показать/скрыть панель.</summary>
        public void SetVisible(bool value)
        {
            if (root == null) return;
            root.gameObject.SetActive(value);
            // СКРЫЛИ — ЗНАЧИТ НЕ ЗАПРОШЕНА: иначе сторож оверлея продолжал бы держать
            // рисование десктопного канваса включённым из-за панели, которой на экране нет.
            if (!value) OperatorRequested = false;
            Notify();
        }

        /// <summary>Скрыть панель полностью (меню «Вид»/настройки).</summary>
        public void Hide()
        {
            SetVisible(false);
            if (Closed != null) Closed();
        }

        /// <summary>Свернуть/развернуть (остаётся один заголовок).</summary>
        public void ToggleCollapsed()
        {
            if (InRail) { FromRail(); return; }
            SetCollapsed(!Collapsed);
        }

        public void SetCollapsed(bool value)
        {
            Collapsed = value;
            if (value) InRail = false;
            if (collapseButton != null)
                collapseButton.SetIcon(Collapsed ? "expand" : "collapse");
            StretchBody();
            ApplyRailVisual();
            Notify();
        }

        /// <summary>Свернуть окно в боковую «полочку» (кнопка ✕, ТЗ ЭТАПА 3).</summary>
        public void ToRail()
        {
            InRail = true;
            Collapsed = false;
            StretchBody();
            ApplyRailVisual();
            Notify();
            KazistovVvUIManager.SetPlanStatus(
                KvLoc.T("panel.rail.hint", "Окно свёрнуто в боковую панель — клик по полочке вернёт его"),
                KvTheme.TextDim);
        }

        /// <summary>Развернуть из «полочки».</summary>
        public void FromRail()
        {
            InRail = false;
            StretchBody();
            ApplyRailVisual();
            Notify();
        }

        private void ApplyRailVisual()
        {
            if (titleText != null) titleText.gameObject.SetActive(!InRail);
            if (collapseButton != null) collapseButton.gameObject.SetActive(!InRail);
            if (dockButton != null) dockButton.gameObject.SetActive(!InRail);
            if (closeButton != null) closeButton.gameObject.SetActive(!InRail);
            if (InRail && titleIcon != null)
            {
                titleIcon.rectTransform.anchorMin = titleIcon.rectTransform.anchorMax =
                    new Vector2(0.5f, 0.5f);
                titleIcon.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                titleIcon.rectTransform.anchoredPosition = Vector2.zero;
            }
            else if (titleIcon != null)
            {
                titleIcon.rectTransform.anchorMin = titleIcon.rectTransform.anchorMax =
                    new Vector2(0f, 0.5f);
                titleIcon.rectTransform.pivot = new Vector2(0f, 0.5f);
                titleIcon.rectTransform.anchoredPosition = new Vector2(5f, 0f);
            }
        }

        /// <summary>Открепить/прикрепить: плавающее окно ⟷ пристыкованное (ТЗ ЭТАПА 3).</summary>
        public void ToggleFloat()
        {
            if (Side == KvDockSide.Float) SetSide(KvDockSide.Left);
            else
            {
                SetSide(KvDockSide.Float);
                // Плавающее окно появляется рядом с тем краем, где панель стояла.
                float w = Mathf.Clamp(Thickness, MinFloatWidth, canvasRect != null
                    ? canvasRect.rect.width - 40f : 480f);
                float h = Mathf.Max(MinFloatHeight, canvasRect != null
                    ? canvasRect.rect.height * 0.5f : 320f);
                float x = Side == KvDockSide.Right || Side == KvDockSide.Bottom ? 0f : 260f;
                SetFloatRect(new Vector2(x, 120f), new Vector2(w, h));
            }
        }

        /// <summary>Сменить пристыковку (Left → Right → Bottom → Float → Left).</summary>
        public void CycleDock()
        {
            if (InRail) FromRail();
            switch (Side)
            {
                case KvDockSide.Left: SetSide(KvDockSide.Right); break;
                case KvDockSide.Right: SetSide(KvDockSide.Bottom); break;
                case KvDockSide.Bottom: SetSide(KvDockSide.Float); break;
                default: SetSide(KvDockSide.Left); break;
            }
        }

        public void SetSide(KvDockSide value)
        {
            if (Side == KvDockSide.Float && value != KvDockSide.Float)
                Thickness = expandableThickness;
            Side = value;
            if (Side == KvDockSide.Bottom)
            {
                float t = Thickness;
                Thickness = Mathf.Clamp(t > 400f ? 240f : t, minThickness, maxThickness);
            }
            if (dockButton != null)
                dockButton.SetIcon(Side == KvDockSide.Float ? "dock" : "undock");
            SetHandlesActive(!Collapsed && !InRail);
            Notify();
        }

        /// <summary>Толщина задаётся ползунком или менеджером.</summary>
        public void SetThickness(float value)
        {
            Thickness = Mathf.Clamp(value, minThickness, maxThickness);
            if (Side != KvDockSide.Float) expandableThickness = Thickness;
            Notify();
        }

        /// <summary>Позиция/размер в режиме «плавающей» панели (px канваса, от левого-нижнего угла).</summary>
        public void SetFloatRect(Vector2 position, Vector2 size)
        {
            float maxW = canvasRect != null ? Mathf.Max(MinFloatWidth, canvasRect.rect.width - 20f) : 4000f;
            float maxH = canvasRect != null ? Mathf.Max(MinFloatHeight, canvasRect.rect.height - 20f) : 4000f;
            floatW = Mathf.Clamp(size.x, MinFloatWidth, maxW);
            floatH = Mathf.Clamp(size.y, MinFloatHeight, maxH);
            floatX = Mathf.Clamp(position.x, 0f, Mathf.Max(0f, maxW - floatW));
            floatY = Mathf.Clamp(position.y, 0f, Mathf.Max(0f, maxH - floatH));
            if (Side == KvDockSide.Float) Notify();
        }

        private void Notify()
        {
            if (LayoutDirty != null) LayoutDirty();
        }

        /// <summary>Сообщить владельцу, что раскладку пора сохранить (после перетаскивания).</summary>
        public void NotifyLayoutChanged()
        {
            if (LayoutChanged != null) LayoutChanged(LayoutKey);
        }

        /// <summary>Текущая раскладка панели (для сохранения в PlayerPrefs).</summary>
        public KvPanelLayout CaptureLayout()
        {
            KvPanelLayout l = new KvPanelLayout();
            l.Side = (int)Side;
            l.Thickness = Thickness;
            l.Collapsed = Collapsed;
            l.Visible = Shown;
            l.Rail = InRail;
            l.X = floatX;
            l.Y = floatY;
            l.Width = floatW;
            l.Height = floatH;
            return l;
        }

        /// <summary>Применить сохранённую раскладку (ЭТАП 3: «окна на своих местах»).</summary>
        public void ApplySavedLayout(KvPanelLayout l)
        {
            if (l == null) return;
            Side = (KvDockSide)Mathf.Clamp(l.Side, 0, 3);
            Thickness = Mathf.Clamp(l.Thickness, minThickness, maxThickness);
            expandableThickness = Thickness;
            floatX = l.X;
            floatY = l.Y;
            floatW = Mathf.Max(MinFloatWidth, l.Width);
            floatH = Mathf.Max(MinFloatHeight, l.Height);
            Collapsed = l.Collapsed;
            InRail = l.Rail;
            if (collapseButton != null) collapseButton.SetIcon(Collapsed ? "expand" : "collapse");
            if (dockButton != null)
                dockButton.SetIcon(Side == KvDockSide.Float ? "dock" : "undock");
            StretchBody();
            ApplyRailVisual();
            SetHandlesActive(!Collapsed && !InRail);
            Notify();
        }

        // ------------------------------------------------------------------ раскладка

        /// <summary>
        /// Разложить панель. Вызывается менеджером UI: он передаёт актуальные отступы
        /// (высота тулбара сверху, высота статус-бара снизу, занятые края).
        /// </summary>
        public void ApplyLayout(float topInset, float bottomInset, float leftInset, float rightInset)
        {
            if (root == null) return;
            float h = canvasRect != null ? canvasRect.rect.height : 1080f;
            float w = canvasRect != null ? canvasRect.rect.width : 1920f;
            float thickness = InRail ? RailThickness : (Collapsed ? CollapsedHeight : Thickness);

            switch (Side)
            {
                case KvDockSide.Left:
                    root.anchorMin = new Vector2(0f, 0f);
                    root.anchorMax = new Vector2(0f, 1f);
                    root.pivot = new Vector2(0f, 0.5f);
                    root.sizeDelta = new Vector2(thickness, -(topInset + bottomInset));
                    root.anchoredPosition = new Vector2(0f, (topInset - bottomInset) * 0.5f);
                    break;

                case KvDockSide.Right:
                    root.anchorMin = new Vector2(1f, 0f);
                    root.anchorMax = new Vector2(1f, 1f);
                    root.pivot = new Vector2(1f, 0.5f);
                    root.sizeDelta = new Vector2(thickness, -(topInset + bottomInset));
                    root.anchoredPosition = new Vector2(0f, (topInset - bottomInset) * 0.5f);
                    break;

                case KvDockSide.Bottom:
                    root.anchorMin = new Vector2(0f, 0f);
                    root.anchorMax = new Vector2(1f, 0f);
                    root.pivot = new Vector2(0.5f, 0f);
                    root.sizeDelta = new Vector2(-(leftInset + rightInset), thickness);
                    root.anchoredPosition = new Vector2((leftInset - rightInset) * 0.5f, bottomInset);
                    break;

                default: // Float — свободное окно
                    float maxW = Mathf.Max(MinFloatWidth, w - 20f);
                    float maxH = Mathf.Max(MinFloatHeight, h - 20f);
                    floatW = Mathf.Clamp(floatW, MinFloatWidth, maxW);
                    floatH = Mathf.Clamp(floatH, MinFloatHeight, maxH);
                    floatX = Mathf.Clamp(floatX, 0f, Mathf.Max(0f, maxW - floatW));
                    floatY = Mathf.Clamp(floatY, 0f, Mathf.Max(0f, maxH - floatH));
                    root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
                    root.pivot = new Vector2(0f, 0f);
                    root.sizeDelta = new Vector2(floatW, Collapsed ? CollapsedHeight : floatH);
                    root.anchoredPosition = new Vector2(floatX, floatY);
                    break;
            }

            // Сплиттер — на внутренней кромке.
            if (splitter != null && splitter.gameObject.activeSelf)
            {
                switch (Side)
                {
                    case KvDockSide.Left:
                        splitter.anchorMin = new Vector2(1f, 0f);
                        splitter.anchorMax = new Vector2(1f, 1f);
                        splitter.pivot = new Vector2(1f, 0.5f);
                        splitter.sizeDelta = new Vector2(4f, 0f);
                        splitter.anchoredPosition = Vector2.zero;
                        break;
                    case KvDockSide.Right:
                        splitter.anchorMin = new Vector2(0f, 0f);
                        splitter.anchorMax = new Vector2(0f, 1f);
                        splitter.pivot = new Vector2(0f, 0.5f);
                        splitter.sizeDelta = new Vector2(4f, 0f);
                        splitter.anchoredPosition = Vector2.zero;
                        break;
                    case KvDockSide.Bottom:
                        splitter.anchorMin = new Vector2(0f, 1f);
                        splitter.anchorMax = new Vector2(1f, 1f);
                        splitter.pivot = new Vector2(0.5f, 1f);
                        splitter.sizeDelta = new Vector2(0f, 4f);
                        splitter.anchoredPosition = Vector2.zero;
                        break;
                    default:
                        splitter.anchorMin = new Vector2(1f, 0f);
                        splitter.anchorMax = new Vector2(1f, 0f);
                        splitter.pivot = new Vector2(1f, 0f);
                        splitter.sizeDelta = new Vector2(6f, 6f);
                        splitter.anchoredPosition = Vector2.zero;
                        break;
                }
            }

            LayoutHandles();
        }

        /// <summary>Разложить 8 маркеров изменения размера по краям и углам окна (ЭТАП 4).</summary>
        private void LayoutHandles()
        {
            if (handles == null) return;
            float e = EdgeGrip, c = CornerGrip;
            for (int i = 0; i < handles.Length; i++)
            {
                KvResizeHandle handle = handles[i];
                if (handle == null) continue;
                RectTransform rt = handle.Rect;
                switch (handle.Edge)
                {
                    case KvResizeEdge.Left:
                        SetEdge(rt, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f),
                            new Vector2(e, -HeaderHeight - 2f), Vector2.zero);
                        break;
                    case KvResizeEdge.Right:
                        SetEdge(rt, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f),
                            new Vector2(e, -HeaderHeight - 2f), Vector2.zero);
                        break;
                    case KvResizeEdge.Top:
                        SetEdge(rt, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(-2f * e, e), Vector2.zero);
                        break;
                    case KvResizeEdge.Bottom:
                        SetEdge(rt, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(-2f * e, e), Vector2.zero);
                        break;
                    case KvResizeEdge.TopLeft:
                        SetEdge(rt, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(c, c), Vector2.zero);
                        break;
                    case KvResizeEdge.TopRight:
                        SetEdge(rt, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(c, c), Vector2.zero);
                        break;
                    case KvResizeEdge.BottomLeft:
                        SetEdge(rt, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(c, c), Vector2.zero);
                        break;
                    default:
                        SetEdge(rt, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(c, c), Vector2.zero);
                        break;
                }
            }
        }

        private static void SetEdge(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
            Vector2 pivot, Vector2 size, Vector2 position)
        {
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = pivot;
            rt.sizeDelta = size;
            rt.anchoredPosition = position;
        }

        /// <summary>Перекрасить панель под текущую тему (после смены темы).</summary>
        public void Repaint()
        {
            if (root == null) return;
            bodyBg.color = KvTheme.PanelBg;
            Image headerBg = header != null ? header.GetComponent<Image>() : null;
            if (headerBg != null) headerBg.color = KvTheme.PanelHeader;
            if (titleText != null) titleText.color = KvTheme.TextMain;
            if (titleIcon != null) titleIcon.color = KvTheme.TextDim;
            if (collapseButton != null) collapseButton.Icon.color = KvTheme.TextDim;
            if (dockButton != null) dockButton.Icon.color = KvTheme.TextDim;
            if (closeButton != null) closeButton.Icon.color = KvTheme.TextDim;
            Outline o = root.GetComponent<Outline>();
            if (o != null) o.effectColor = KvTheme.Border;
        }

        // ------------------------------------------------------------------ перетаскивание

        /// <summary>Перетаскивание заголовка: панель «отклеивается» и следует за курсором.</summary>
        public void DragTo(Vector2 canvasPoint)
        {
            if (InRail) return;
            if (Side != KvDockSide.Float)
            {
                Vector2 size = new Vector2(Mathf.Max(Thickness, 220f), 260f);
                SetSide(KvDockSide.Float);
                SetFloatRect(canvasPoint - new Vector2(size.x * 0.5f, 30f), size);
            }
            else
            {
                SetFloatRect(canvasPoint - new Vector2(floatW * 0.5f, 30f), new Vector2(floatW, floatH));
            }
        }

        /// <summary>
        /// Отпускание заголовка: прилипание к ближнему краю или свободное окно.
        /// Зона прилипания — та же, что подсвечивал индикатор (<see cref="DockZoneAt"/>).
        /// </summary>
        public void DropAt(Vector2 canvasPoint)
        {
            if (canvasRect == null) return;
            KvDockSide zone = DockZoneAt(canvasPoint);
            if (zone == KvDockSide.Bottom)
            {
                SetSide(KvDockSide.Bottom);
                SetThickness(Mathf.Max(200f, floatH));
            }
            else
            {
                SetSide(zone);
            }
            NotifyLayoutChanged();
            KvLayoutStore.Flush();
        }

        /// <summary>Какая зона прикрепления под точкой канваса (для индикатора и отпускания).</summary>
        public KvDockSide DockZoneAt(Vector2 canvasPoint)
        {
            if (canvasRect == null) return KvDockSide.Float;
            Rect r = canvasRect.rect;
            float edge = 64f;
            float fromLeft = canvasPoint.x - r.xMin;
            float fromRight = r.xMax - canvasPoint.x;
            float fromBottom = canvasPoint.y - r.yMin;
            float fromTop = r.yMax - canvasPoint.y;

            if (fromBottom < edge * 1.2f && fromBottom < fromLeft && fromBottom < fromRight)
                return KvDockSide.Bottom;
            if (fromLeft < edge && fromLeft <= fromRight && fromTop > edge)
                return KvDockSide.Left;
            if (fromRight < edge && fromRight < fromLeft && fromTop > edge)
                return KvDockSide.Right;
            return KvDockSide.Float;
        }

        internal void ResizeBy(float delta, KvDockSide side)
        {
            if (side == KvDockSide.Bottom) SetThickness(Thickness + delta);
            else SetThickness(Thickness - delta);
        }

        public float FloatWidth { get { return floatW; } }
        public float FloatHeight { get { return floatH; } }

        /// <summary>
        /// Изменить размер ПЛАВАЮЩЕГО окна перетаскиванием грани/угла (ЭТАП 4).
        /// Позиция/размер считаются от точки курсора — так окно не «уезжает» при упоре в предел.
        /// </summary>
        public void ResizeFloatTo(Vector2 canvasPoint, KvResizeEdge edge)
        {
            if (Side != KvDockSide.Float) return;
            float left = floatX, bottom = floatY, right = floatX + floatW, top = floatY + floatH;

            if (edge == KvResizeEdge.Left || edge == KvResizeEdge.TopLeft ||
                edge == KvResizeEdge.BottomLeft)
                left = Mathf.Min(canvasPoint.x, right - MinFloatWidth);
            if (edge == KvResizeEdge.Right || edge == KvResizeEdge.TopRight ||
                edge == KvResizeEdge.BottomRight)
                right = Mathf.Max(canvasPoint.x, left + MinFloatWidth);
            if (edge == KvResizeEdge.Top || edge == KvResizeEdge.TopLeft ||
                edge == KvResizeEdge.TopRight)
                top = Mathf.Max(canvasPoint.y, bottom + MinFloatHeight);
            if (edge == KvResizeEdge.Bottom || edge == KvResizeEdge.BottomLeft ||
                edge == KvResizeEdge.BottomRight)
                bottom = Mathf.Min(canvasPoint.y, top - MinFloatHeight);

            SetFloatRect(new Vector2(left, bottom), new Vector2(right - left, top - bottom));
        }

        /// <summary>Начать изменение толщины ПРИСТЫКОВАННОЙ панели (внутренняя кромка).</summary>
        public void BeginDockedResize(Vector2 canvasPoint)
        {
            dockResizeActive = true;
            dockResizeStartPoint = canvasPoint;
            dockResizeStartThickness = Thickness;
        }

        /// <summary>
        /// Изменить толщину ПРИСТЫКОВАННОЙ панели: смещение считается ОТ НАЧАЛА перетаскивания
        /// (внутренняя кромка идёт за курсором ровно, без накопления погрешности на пределах).
        /// </summary>
        public void ResizeDockedTo(Vector2 canvasPoint)
        {
            if (!dockResizeActive) BeginDockedResize(canvasPoint);
            Vector2 delta = canvasPoint - dockResizeStartPoint;
            switch (Side)
            {
                case KvDockSide.Left:
                    SetThickness(dockResizeStartThickness + delta.x);
                    break;
                case KvDockSide.Right:
                    SetThickness(dockResizeStartThickness - delta.x);
                    break;
                case KvDockSide.Bottom:
                    SetThickness(dockResizeStartThickness + delta.y);
                    break;
            }
        }

        /// <summary>Закончить изменение толщины + сохранить раскладку (ЭТАП 3).</summary>
        public void EndDockedResize()
        {
            if (!dockResizeActive) return;
            dockResizeActive = false;
            NotifyLayoutChanged();
            KvLayoutStore.Flush();
        }

        /// <summary>Клик по полочке возвращает окно.</summary>
        public void RailClick()
        {
            if (InRail) FromRail();
        }
    }

    /// <summary>
    /// Перетаскивание заголовка dock-панели (ЭТАП 3): панель следует за курсором,
    /// при поднесении к краю показывается ЗОНА прикрепления, при отпускании —
    /// прилипание к краю или плавающее окно в центре.
    /// </summary>
    internal class KvDockDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler,
        IPointerClickHandler
    {
        private KvDockPanel panel;
        private RectTransform canvasRect;
        private KvDockIndicator indicator;

        public void Bind(KvDockPanel owner, RectTransform canvas)
        {
            panel = owner;
            canvasRect = canvas;
        }

        private bool ToCanvas(PointerEventData e, out Vector2 point)
        {
            point = Vector2.zero;
            if (canvasRect == null) return false;
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                e.position, e.pressEventCamera, out point);
        }

        private void EnsureIndicator()
        {
            if (indicator != null || canvasRect == null) return;
            indicator = KvDockIndicator.Ensure(canvasRect);
        }

        public void OnBeginDrag(PointerEventData e)
        {
            Vector2 p;
            if (ToCanvas(e, out p)) panel.DragTo(p);
            EnsureIndicator();
        }

        public void OnDrag(PointerEventData e)
        {
            Vector2 p;
            if (!ToCanvas(e, out p)) return;
            panel.DragTo(p);
            if (indicator != null) indicator.Show(panel.DockZoneAt(p));
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (indicator != null) indicator.Hide();
            Vector2 p;
            if (ToCanvas(e, out p)) panel.DropAt(p);
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (panel.InRail)
            {
                panel.RailClick();
                return;
            }
            if (e.clickCount >= 2) panel.CycleDock();   // двойной клик — сменить пристыковку
        }
    }

    /// <summary>
    /// ИНДИКАТОР ЗОНЫ ПРИКРЕПЛЕНИЯ (ЭТАП 3): при перетаскивании окна к краю подсвечивается
    /// область, куда оно прикрепится (левая/правая/нижняя полоса) либо центр (плавающее окно).
    /// </summary>
    internal class KvDockIndicator : MonoBehaviour
    {
        private const float EdgeFraction = 0.22f;
        private static KvDockIndicator instance;

        private RectTransform canvasRect;
        private Image left;
        private Image right;
        private Image bottom;
        private Image center;
        private Image label;

        /// <summary>Создать (один раз на канвас) слой индикатора.</summary>
        public static KvDockIndicator Ensure(RectTransform canvas)
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("KvDockIndicator", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rt = (RectTransform)go.transform;
            KvTheme.Stretch(rt);
            instance = go.AddComponent<KvDockIndicator>();
            instance.canvasRect = canvas;
            instance.Build();
            return instance;
        }

        private void Build()
        {
            left = Zone("Left", new Vector2(0f, 0f), new Vector2(EdgeFraction, 1f));
            right = Zone("Right", new Vector2(1f - EdgeFraction, 0f), new Vector2(1f, 1f));
            bottom = Zone("Bottom", new Vector2(0f, 0f), new Vector2(1f, EdgeFraction * 0.7f));
            center = Zone("Center", new Vector2(0.3f, 0.25f), new Vector2(0.7f, 0.75f));

            GameObject labelGo = new GameObject("Label", typeof(Image));
            labelGo.transform.SetParent(transform, false);
            label = labelGo.GetComponent<Image>();
            label.sprite = KvTheme.WhiteSprite;
            label.color = new Color(0.1f, 0.1f, 0.1f, 0.75f);
            label.raycastTarget = false;
            RectTransform lr = label.rectTransform;
            lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
            lr.pivot = new Vector2(0.5f, 0.5f);
            lr.sizeDelta = new Vector2(320f, 26f);
            lr.anchoredPosition = new Vector2(0f, 0f);

            Text t = KvTheme.CreateText(lr, "Text", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleCenter, KvTheme.TextMain);
            KvTheme.Stretch(t.rectTransform, 6f, 6f, 2f, 2f);
            labelText = t;

            Hide();
        }

        private Text labelText;

        private Image Zone(string name, Vector2 min, Vector2 max)
        {
            GameObject go = new GameObject(name, typeof(Image));
            go.transform.SetParent(transform, false);
            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;
            img.color = new Color(KvTheme.Accent.r, KvTheme.Accent.g, KvTheme.Accent.b, 0.28f);
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = min;
            rt.anchorMax = max;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Accent;
            o.effectDistance = new Vector2(2f, -2f);
            o.useGraphicAlpha = false;
            return img;
        }

        /// <summary>Показать зону, куда прикрепится окно.</summary>
        public void Show(KvDockSide side)
        {
            if (left == null) return;
            gameObject.SetActive(true);
            left.gameObject.SetActive(side == KvDockSide.Left);
            right.gameObject.SetActive(side == KvDockSide.Right);
            bottom.gameObject.SetActive(side == KvDockSide.Bottom);
            center.gameObject.SetActive(side == KvDockSide.Float);
            if (label != null) label.gameObject.SetActive(true);
            if (labelText != null)
            {
                switch (side)
                {
                    case KvDockSide.Left:
                        labelText.text = KvLoc.T("dock.zone.left", "Прикрепить к левому краю");
                        break;
                    case KvDockSide.Right:
                        labelText.text = KvLoc.T("dock.zone.right", "Прикрепить к правому краю");
                        break;
                    case KvDockSide.Bottom:
                        labelText.text = KvLoc.T("dock.zone.bottom", "Прикрепить к нижнему краю");
                        break;
                    default:
                        labelText.text = KvLoc.T("dock.zone.float", "Оставить плавающим окном");
                        break;
                }
            }
            transform.SetAsLastSibling();
        }

        /// <summary>Скрыть индикатор.</summary>
        public void Hide()
        {
            if (left != null) left.gameObject.SetActive(false);
            if (right != null) right.gameObject.SetActive(false);
            if (bottom != null) bottom.gameObject.SetActive(false);
            if (center != null) center.gameObject.SetActive(false);
            if (label != null) label.gameObject.SetActive(false);
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }

    /// <summary>Изменение толщины пристыкованной панели перетаскиванием внутренней кромки.</summary>
    internal class KvSplitter : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerEnterHandler,
        IPointerExitHandler, IEndDragHandler
    {
        private KvDockPanel panel;
        private RectTransform canvasRect;

        public void Bind(KvDockPanel owner, RectTransform canvas)
        {
            panel = owner;
            canvasRect = canvas;
        }

        public void OnPointerDown(PointerEventData e)
        {
            Vector2 point;
            if (panel != null && canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, e.position,
                    e.pressEventCamera, out point))
                panel.BeginDockedResize(point);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (panel == null) return;
            KvCursors.Set(panel.Side == KvDockSide.Bottom ? KvResizeEdge.Top
                : panel.Side == KvDockSide.Right ? KvResizeEdge.Left : KvResizeEdge.Right);
        }

        public void OnPointerExit(PointerEventData e)
        {
            KvCursors.Reset();
        }

        public void OnDrag(PointerEventData e)
        {
            if (panel == null || canvasRect == null) return;
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, e.position,
                e.pressEventCamera, out point)) return;

            if (panel.Side == KvDockSide.Float) panel.ResizeFloatTo(point, KvResizeEdge.Right);
            else panel.ResizeDockedTo(point);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (panel != null) panel.EndDockedResize();
        }
    }

    /// <summary>
    /// ПОЛЗУНОК ГРАНИЦЫ ОКНА (ЭТАП 4): невидимая полоса по краю/углу панели.
    /// При наведении меняет системный курсор на «↔», «↕» или диагональ, при
    /// перетаскивании меняет размер окна (или толщину пристыкованной панели).
    /// </summary>
    internal class KvResizeHandle : MonoBehaviour, IDragHandler, IPointerDownHandler,
        IPointerEnterHandler, IPointerExitHandler, IEndDragHandler
    {
        /// <summary>Какой это маркер.</summary>
        public KvResizeEdge Edge { get; private set; }

        /// <summary>Прямоугольник маркера.</summary>
        public RectTransform Rect { get { return (RectTransform)transform; } }

        private KvDockPanel panel;
        private RectTransform canvasRect;

        public void Bind(KvDockPanel owner, RectTransform canvas, KvResizeEdge edge)
        {
            panel = owner;
            canvasRect = canvas;
            Edge = edge;
        }

        /// <summary>Внутренняя ли это кромка для данного края пристыковки.</summary>
        public bool IsInnerFor(KvDockSide side)
        {
            switch (side)
            {
                case KvDockSide.Left: return Edge == KvResizeEdge.Right;
                case KvDockSide.Right: return Edge == KvResizeEdge.Left;
                case KvDockSide.Bottom: return Edge == KvResizeEdge.Top;
                default: return true;
            }
        }

        public void OnPointerDown(PointerEventData e)
        {
            Vector2 point;
            if (panel != null && canvasRect != null &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, e.position,
                    e.pressEventCamera, out point))
                panel.BeginDockedResize(point);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            KvCursors.Set(Edge);
        }

        public void OnPointerExit(PointerEventData e)
        {
            KvCursors.Reset();
        }

        public void OnDrag(PointerEventData e)
        {
            if (panel == null || canvasRect == null) return;
            Vector2 point;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, e.position,
                e.pressEventCamera, out point)) return;

            if (panel.Side == KvDockSide.Float) panel.ResizeFloatTo(point, Edge);
            else panel.ResizeDockedTo(point);
        }

        public void OnEndDrag(PointerEventData e)
        {
            if (panel == null) return;
            panel.EndDockedResize();
            panel.NotifyLayoutChanged();
            KvLayoutStore.Flush();
        }

        private void OnDisable()
        {
            KvCursors.Reset();
        }
    }
}
