using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Всплывающая подсказка в стиле FreeCAD: появляется у курсора через
    /// <see cref="Delay"/> секунд после наведения, состоит из заголовка,
    /// необязательного описания и «бейджа» горячей клавиши.
    /// Один слой на весь интерфейс (последний sibling канваса — всегда сверху).
    /// </summary>
    public class KvTooltip : MonoBehaviour
    {
        /// <summary>Задержка появления, с.</summary>
        public float delay = 0.35f;
        /// <summary>Максимальная ширина текста подсказки.</summary>
        public float maxWidth = 330f;
        /// <summary>Смещение от курсора.</summary>
        public Vector2 cursorOffset = new Vector2(16f, -18f);

        private static KvTooltip instance;

        private RectTransform root;
        private RectTransform canvasRect;
        private Text titleText;
        private Text bodyText;
        private Text hotkeyText;
        private Image frame;
        private Image hotkeyBg;

        private string pendingTitle = "";
        private string pendingBody = "";
        private string pendingHotkey = "";
        private float hoverStart = -1f;
        private bool visible;
        private bool pinned;
        private Canvas canvas;

        /// <summary>Подсказка (заголовок/описание/клавиша) для текущего наведения.</summary>
        public static void Request(string title, string body, string hotkey)
        {
            if (instance == null) return;
            // Обычное наведение отменяет закрепление (иначе после закреплённой
            // подсказки обычные перестанут сниматься при уходе курсора).
            if (instance.pinned) instance.pinned = false;
            if (instance.pendingTitle == title && instance.pendingBody == body &&
                instance.pendingHotkey == hotkey) return;
            instance.pendingTitle = title;
            instance.pendingBody = body;
            instance.pendingHotkey = hotkey;
            instance.hoverStart = Time.unscaledTime;
            if (instance.visible) instance.Paint();
        }

        /// <summary>
        /// Закреплённая подсказка: не снимается автоматическим уходом курсора
        /// (диагностика и явные подсказки системы). Снимается только Release(force: true).
        /// </summary>
        public static void RequestPinned(string title, string body, string hotkey)
        {
            if (instance == null) return;
            instance.pinned = true;
            instance.pendingTitle = title;
            instance.pendingBody = body;
            instance.pendingHotkey = hotkey;
            instance.hoverStart = Time.unscaledTime;
            if (instance.visible) instance.Paint();
        }

        /// <summary>Снять наведение. Для закреплённой подсказки требуется force: true.</summary>
        public static void Release(bool force = false)
        {
            if (instance == null) return;
            if (instance.pinned && !force) return;
            instance.pinned = false;
            instance.pendingTitle = "";
            instance.pendingBody = "";
            instance.pendingHotkey = "";
            instance.hoverStart = -1f;
            instance.SetVisible(false);
        }

        /// <summary>Создать слой подсказок (вызывается один раз менеджером UI).</summary>
        public static KvTooltip Ensure(RectTransform canvasTransform, Canvas ownerCanvas)
        {
            if (instance != null && instance.canvasRect == canvasTransform) return instance;
            GameObject go = new GameObject("KvTooltipLayer", typeof(RectTransform));
            go.transform.SetParent(canvasTransform, false);
            KvTooltip t = go.AddComponent<KvTooltip>();
            t.Build(canvasTransform, ownerCanvas);
            instance = t;
            return t;
        }

        /// <summary>Текущий слой подсказок (может быть null).</summary>
        public static KvTooltip Current { get { return instance; } }

        private void Build(RectTransform canvasTransform, Canvas ownerCanvas)
        {
            canvasRect = canvasTransform;
            canvas = ownerCanvas;
            root = (RectTransform)transform;
            root.anchorMin = root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 1f);
            root.sizeDelta = new Vector2(maxWidth, 44f);
            root.anchoredPosition = Vector2.zero;

            frame = KvTheme.CreatePanel(root, "Frame", KvTheme.PanelHeader);
            KvTheme.Stretch(frame.rectTransform);
            Outline o = frame.gameObject.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            titleText = KvTheme.CreateText(root, "Title", "", KvTheme.FontSize, TextAnchor.UpperLeft,
                KvTheme.TextMain);
            titleText.rectTransform.anchorMin = new Vector2(0f, 1f);
            titleText.rectTransform.anchorMax = new Vector2(1f, 1f);
            titleText.rectTransform.pivot = new Vector2(0f, 1f);
            titleText.rectTransform.offsetMin = new Vector2(8f, 0f);
            titleText.rectTransform.offsetMax = new Vector2(-40f, 0f);
            titleText.rectTransform.sizeDelta = new Vector2(-48f, 18f);
            titleText.rectTransform.anchoredPosition = new Vector2(0f, -4f);

            hotkeyBg = KvTheme.CreatePanel(root, "HotkeyBg", KvTheme.ButtonBg);
            hotkeyBg.rectTransform.anchorMin = new Vector2(1f, 1f);
            hotkeyBg.rectTransform.anchorMax = new Vector2(1f, 1f);
            hotkeyBg.rectTransform.pivot = new Vector2(1f, 1f);
            hotkeyBg.rectTransform.sizeDelta = new Vector2(34f, 16f);
            hotkeyBg.rectTransform.anchoredPosition = new Vector2(-6f, -4f);

            hotkeyText = KvTheme.CreateText(hotkeyBg.rectTransform, "Hotkey", "",
                KvTheme.FontSizeSmall, TextAnchor.MiddleCenter, KvTheme.TextDim);
            KvTheme.Stretch(hotkeyText.rectTransform);

            bodyText = KvTheme.CreateText(root, "Body", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, KvTheme.TextDim);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            bodyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            bodyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            bodyText.rectTransform.pivot = new Vector2(0f, 1f);
            bodyText.rectTransform.offsetMin = new Vector2(8f, 0f);
            bodyText.rectTransform.offsetMax = new Vector2(-8f, 0f);
            bodyText.rectTransform.sizeDelta = new Vector2(-16f, 16f);
            bodyText.rectTransform.anchoredPosition = new Vector2(0f, -22f);

            SetVisible(false);
        }

        /// <summary>Подсказка сейчас на экране (диагностика).</summary>
        public bool IsShown { get { return visible; } }
        /// <summary>Заголовок показанной подсказки (диагностика).</summary>
        public string CurrentTitle { get { return titleText != null ? titleText.text : ""; } }
        /// <summary>Текст описания показанной подсказки (диагностика).</summary>
        public string CurrentBody { get { return bodyText != null ? bodyText.text : ""; } }

        private void Update()
        {
            if (root == null) return;

            if (hoverStart >= 0f && !visible && Time.unscaledTime - hoverStart >= delay)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                    Input.mousePosition, null, out Vector2 local);
                root.anchoredPosition = local + cursorOffset;
                Paint();
                SetVisible(true);
            }
            else if (visible)
            {
                // Подсказка едет за курсором (как в FreeCAD), но остаётся в пределах канваса.
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,
                    Input.mousePosition, null, out Vector2 local);
                Vector2 pos = local + cursorOffset;
                float w = canvasRect.rect.width, h = canvasRect.rect.height;
                float pw = root.sizeDelta.x, ph = root.sizeDelta.y;
                pos.x = Mathf.Clamp(pos.x, -w * 0.5f + 2f, w * 0.5f - pw - 2f);
                pos.y = Mathf.Clamp(pos.y, -h * 0.5f + ph + 2f, h * 0.5f - 2f);
                root.anchoredPosition = pos;
            }
        }

        private void SetVisible(bool value)
        {
            visible = value;
            // ВАЖНО: сам корень подсказки НЕ выключается — иначе не работает Update(),
            // который и показывает подсказку по задержке (проверено прогоном: с
            // SetActive(false) подсказка не появлялась вообще). Гасим только содержимое.
            if (frame != null) frame.gameObject.SetActive(value);
            if (titleText != null) titleText.gameObject.SetActive(value);
            if (bodyText != null)
                bodyText.gameObject.SetActive(value && !string.IsNullOrEmpty(pendingBody));
            if (hotkeyBg != null)
                hotkeyBg.gameObject.SetActive(value && !string.IsNullOrEmpty(pendingHotkey));
            if (hotkeyText != null)
                hotkeyText.gameObject.SetActive(value && !string.IsNullOrEmpty(pendingHotkey));
        }

        private void Paint()
        {
            if (root == null) return;
            titleText.text = pendingTitle;
            bodyText.text = pendingBody;
            bool hasBody = !string.IsNullOrEmpty(pendingBody);
            bool hasKey = !string.IsNullOrEmpty(pendingHotkey);

            bodyText.gameObject.SetActive(hasBody);
            hotkeyBg.gameObject.SetActive(hasKey);
            hotkeyText.gameObject.SetActive(hasKey);
            hotkeyText.text = pendingHotkey;

            float width = Mathf.Min(maxWidth, Mathf.Max(120f, titleText.preferredWidth + 60f));
            if (hasBody) width = Mathf.Max(width, Mathf.Min(maxWidth, 220f));
            float height = 26f + (hasBody ? 30f : 0f);
            root.sizeDelta = new Vector2(width, height);

            if (hasBody)
            {
                bodyText.rectTransform.sizeDelta = new Vector2(-16f, height - 26f);
                bodyText.rectTransform.anchoredPosition = new Vector2(0f, -22f);
            }

            // Цвета — от текущей темы (подсказка живёт дольше одной пересборки).
            frame.color = KvTheme.PanelHeader;
            titleText.color = KvTheme.TextMain;
            bodyText.color = KvTheme.TextDim;
            hotkeyBg.color = KvTheme.ButtonBg;
            hotkeyText.color = KvTheme.TextDim;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }
    }

    /// <summary>
    /// Навешивается на любой элемент, у которого должна быть подсказка
    /// (кнопки-иконки тулбара, строки дерева, узлы статус-бара).
    /// Требует, чтобы у элемента был Graphic с raycastTarget = true.
    /// </summary>
    public class KvTooltipTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        /// <summary>Заголовок подсказки.</summary>
        public string title = "";
        /// <summary>Описание (может быть пустым).</summary>
        public string body = "";
        /// <summary>Горячая клавиша («Z», «ЛКМ», «Enter»).</summary>
        public string hotkey = "";
        /// <summary>Пометка «в разработке» (заглушка).</summary>
        public bool stub;

        /// <summary>Обновить текст подсказки (без пересоздания компонента).</summary>
        public void Set(string newTitle, string newBody = "", string newHotkey = "")
        {
            title = newTitle;
            body = newBody ?? "";
            hotkey = newHotkey ?? "";
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            string t = title;
            string b = body;
            if (stub && !string.IsNullOrEmpty(b)) b += "\n(в разработке)";
            else if (stub) b = "в разработке";
            KvTooltip.Request(t, b, hotkey);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            KvTooltip.Release();
        }

        private void OnDisable()
        {
            KvTooltip.Release();
        }
    }
}
