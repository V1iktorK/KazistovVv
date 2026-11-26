using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// ТЯГАНИЕ ОКНА ЗА ЗАГОЛОВОК с магнитным защёлкиванием к краям (снап).
    ///
    /// Снап: пока окно тянут, если оно подходит к краю канваса ближе чем
    /// <see cref="SnapDistance"/>, окно МАГНИТНО прилипает к этому краю (как док Blender).
    /// К какому краю прилипло — видно в <see cref="LastSnapEdge"/> (проверяется тестами).
    ///
    /// ТЕСТИРУЕМОСТЬ. Реальные pointer-события в EditMode не гоняются, поэтому вся логика
    /// живёт в публичных `BeginDragAt`/`DragTo`/`EndDrag`, которые вызывают и интерфейсы
    /// `I*DragHandler`, и тесты напрямую.
    /// </summary>
    public class KvBlenderWindowDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        /// <summary>Дистанция магнита (px канваса).</summary>
        public const float SnapDistance = 24f;

        private RectTransform window;
        private RectTransform canvasRect;
        private Vector2 grabOffset;
        private bool dragging;

        /// <summary>Край, к которому окно прилипло при последнем перетаскивании («» — нет).</summary>
        public string LastSnapEdge = "";

        /// <summary>Идёт ли перетаскивание (тесты/диагностика).</summary>
        public bool IsDragging
        {
            get { return dragging; }
        }

        public void Setup(RectTransform windowRect, RectTransform canvas)
        {
            window = windowRect;
            canvasRect = canvas;
        }

        // ------------------------------------------------------------------ публичная логика (и тесты)

        public void BeginDragAt(Vector2 screenPoint)
        {
            if (window == null || canvasRect == null) return;
            dragging = true;
            Rect r = canvasRect.rect;
            Vector2 local = ToLocal(screenPoint);
            // Текущий левый верх окна в локальных координатах канваса (anchoredPosition —
            // смещение от точки привязки, а не координата канваса).
            Vector2 topLeft = new Vector2(r.xMin + window.anchoredPosition.x,
                r.yMax + window.anchoredPosition.y);
            grabOffset = local - topLeft;
            LastSnapEdge = "";
        }

        public void DragTo(Vector2 screenPoint)
        {
            if (!dragging || window == null || canvasRect == null) return;
            Rect r = canvasRect.rect;
            Vector2 local = ToLocal(screenPoint);
            Vector2 topLeft = local - grabOffset;

            // МАГНИТНОЕ ЗАЩЁЛКИВАНИЕ к краям канваса: окно у края → прилипает к нему.
            // Судим по ЛОКАЛЬНОЙ точке канваса (край канваса), а не по пикселям экрана:
            // канвас масштабируется, и «24 px экрана» на разных разрешениях — разные щели.
            float w = window.rect.width;
            float h = window.rect.height;
            string snap = "";
            if (topLeft.x - r.xMin <= SnapDistance)
            {
                topLeft.x = r.xMin;
                snap = "left";
            }
            else if (r.xMax - (topLeft.x + w) <= SnapDistance)
            {
                topLeft.x = r.xMax - w;
                snap = "right";
            }
            if (r.yMax - topLeft.y <= SnapDistance)
            {
                topLeft.y = r.yMax;
                snap = snap.Length == 0 ? "top" : snap + "+top";
            }
            else if (topLeft.y - h - r.yMin <= SnapDistance)
            {
                topLeft.y = r.yMin + h;
                snap = snap.Length == 0 ? "bottom" : snap + "+bottom";
            }

            window.anchoredPosition = new Vector2(topLeft.x - r.xMin, topLeft.y - r.yMax);
            if (snap.Length > 0) LastSnapEdge = snap;
        }

        public void EndDrag()
        {
            dragging = false;
        }

        // ------------------------------------------------------------------ интерфейсы Unity

        public void OnBeginDrag(PointerEventData eventData)
        {
            BeginDragAt(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            DragTo(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EndDrag();
        }

        private Vector2 ToLocal(Vector2 screenPoint)
        {
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPoint, null, out local);
            return local;
        }
    }

    /// <summary>
    /// СТЕКЛЯННОЕ ОКНО ПАНЕЛИ (утверждённый дизайн: скруглённая рамка + полупрозрачный фон).
    ///
    /// Кнопки окна — у КАЖДОЙ панели, как просил владелец: «свернуть/развернуть/крестик».
    /// Свернуть прячет содержимое (остаётся заголовок), развернуть растягивает окно на весь
    /// рабочий район (повторное нажатие возвращает размер), крестик скрывает окно
    /// (показать снова — командой панели/меню).
    /// </summary>
    public class KvGlassWindow
    {
        /// <summary>Высота заголовка окна (px).</summary>
        public const float HeaderHeight = 22f;

        public readonly string Id;
        public readonly RectTransform Root;
        public readonly RectTransform Content;
        public readonly Text TitleText;
        public readonly Button MinimizeButton;
        public readonly Button MaximizeButton;
        public readonly Button CloseButton;
        public readonly KvBlenderWindowDrag Drag;

        /// <summary>Окно развёрнуто на весь рабочий район.</summary>
        public bool Maximized { get; private set; }

        /// <summary>Окно свёрнуто до заголовка.</summary>
        public bool Minimized { get; private set; }

        private readonly Vector2 normalSize;

        public bool Visible
        {
            get { return Root != null && Root.gameObject.activeSelf; }
        }

        public string Title
        {
            get { return TitleText != null ? TitleText.text : ""; }
        }

        public KvGlassWindow(string id, string title, RectTransform parent, RectTransform canvas,
            RectTransform workAreaRect, float width, float height)
        {
            Id = id;
            normalSize = new Vector2(width, height);

            Image border = KvGlassStyle.CreateGlassFrame(parent, "Win_" + id, out Image fill);
            Root = border.rectTransform;
            Root.anchorMin = new Vector2(0f, 1f);
            Root.anchorMax = new Vector2(0f, 1f);
            Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = normalSize;

            // --- заголовок
            Image header = KvGlassStyle.CreateGlassRect(fill.rectTransform, "Header", KvGlassRole.Header);
            RectTransform hr = header.rectTransform;
            hr.anchorMin = new Vector2(0f, 1f);
            hr.anchorMax = new Vector2(1f, 1f);
            hr.pivot = new Vector2(0.5f, 1f);
            hr.sizeDelta = new Vector2(0f, HeaderHeight);
            hr.anchoredPosition = Vector2.zero;
            header.raycastTarget = true;

            TitleText = KvTheme.CreateText(hr, "Title", title, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            TitleText.rectTransform.anchorMin = new Vector2(0f, 0f);
            TitleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            TitleText.rectTransform.offsetMin = new Vector2(7f, 0f);
            TitleText.rectTransform.offsetMax = new Vector2(-58f, 0f);

            // --- кнопки окна: свернуть / развернуть / крестик
            MinimizeButton = CreateTitleButton(hr, "Btn_Min", "collapse", Minimize);
            MaximizeButton = CreateTitleButton(hr, "Btn_Max", "expand", ToggleMaximize);
            CloseButton = CreateTitleButton(hr, "Btn_Close", "close", Close);
            PlaceTitleButtons(hr);

            // --- содержимое
            Image contentBg = KvGlassStyle.CreateGlassRect(fill.rectTransform, "Content", KvGlassRole.Fill);
            RectTransform cr = contentBg.rectTransform;
            cr.anchorMin = new Vector2(0f, 0f);
            cr.anchorMax = new Vector2(1f, 1f);
            cr.offsetMin = new Vector2(2f, 2f);
            cr.offsetMax = new Vector2(-2f, -HeaderHeight);
            contentBg.raycastTarget = true;
            Content = cr;

            // --- перетаскивание за заголовок (магнитный снап к краям)
            Drag = header.gameObject.AddComponent<KvBlenderWindowDrag>();
            Drag.Setup(Root, canvas);
        }

        /// <summary>Свернуть (оставить только заголовок).</summary>
        public void Minimize()
        {
            if (Minimized) return;
            Minimized = true;
            Maximized = false;
            Content.gameObject.SetActive(false);
            if (!Maximized) Root.sizeDelta = new Vector2(normalSize.x, HeaderHeight + 4f);
        }

        /// <summary>Развернуть на весь рабочий район / вернуть размер (тумблер).</summary>
        public void ToggleMaximize()
        {
            if (Maximized)
            {
                Maximized = false;
                Root.anchorMin = new Vector2(0f, 1f);
                Root.anchorMax = new Vector2(0f, 1f);
                Root.pivot = new Vector2(0f, 1f);
                Root.sizeDelta = normalSize;
                if (!Minimized) Content.gameObject.SetActive(true);
                return;
            }
            Maximized = true;
            Minimized = false;
            Content.gameObject.SetActive(true);
            Root.anchorMin = Vector2.zero;
            Root.anchorMax = Vector2.one;
            Root.pivot = new Vector2(0.5f, 0.5f);
            Root.anchoredPosition = Vector2.zero;
            Root.sizeDelta = Vector2.zero;
        }

        /// <summary>Вернуть из свёрнутого/развёрнутого в обычное состояние.</summary>
        public void Restore()
        {
            Minimized = false;
            Maximized = false;
            Content.gameObject.SetActive(true);
            Root.anchorMin = new Vector2(0f, 1f);
            Root.anchorMax = new Vector2(0f, 1f);
            Root.pivot = new Vector2(0f, 1f);
            Root.sizeDelta = normalSize;
        }

        /// <summary>Скрыть окно (крестик).</summary>
        public void Close()
        {
            Root.gameObject.SetActive(false);
        }

        /// <summary>Показать окно снова.</summary>
        public void Show()
        {
            Root.gameObject.SetActive(true);
        }

        private Button CreateTitleButton(RectTransform header, string name, string iconId,
            UnityEngine.Events.UnityAction action)
        {
            GameObject go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(header, false);
            Image img = go.GetComponent<Image>();
            img.sprite = KvGlassStyle.Rounded;
            img.type = Image.Type.Sliced;
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1.4f, 1.4f, 1.4f, 1f);
            cb.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            Image icon = KvTheme.CreatePanel((RectTransform)go.transform, "Icon", KvTheme.TextDim)
                .gameObject.GetComponent<Image>();
            icon.sprite = KvIcons.Get(iconId, 12);
            if (icon.sprite == null) icon.sprite = KvTheme.WhiteSprite;
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(11f, 11f);
            ir.anchoredPosition = Vector2.zero;

            if (action != null) btn.onClick.AddListener(action);
            return btn;
        }

        private void PlaceTitleButtons(RectTransform header)
        {
            float x = -3f;
            PlaceButton(CloseButton, header, ref x);
            x -= 1f;
            PlaceButton(MaximizeButton, header, ref x);
            x -= 1f;
            PlaceButton(MinimizeButton, header, ref x);
        }

        private static void PlaceButton(Button btn, RectTransform header, ref float x)
        {
            RectTransform rt = (RectTransform)btn.transform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(16f, 16f);
            rt.anchoredPosition = new Vector2(x, 0f);
            x -= 16f;
        }
    }
}
