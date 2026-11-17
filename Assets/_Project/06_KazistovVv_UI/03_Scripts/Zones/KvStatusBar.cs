using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Нижняя панель — СТАТУС-БАР в стиле FreeCAD: слева состояние State Machine,
    /// выбранный робот и координаты луча; в центре — статусные сообщения (те же,
    /// что идут в лог/подсказки потока); справа — ИНДИКАТОР РЕЖИМА КУРСОРА (§26),
    /// индикатор графики, тема и FPS.
    /// Обновление текста — по вызову менеджера (10 раз в секунду), поэтому панель
    /// не влияет на производительность; собственный `Update` работает ТОЛЬКО пока идёт
    /// подсветка смены режима (0,5 с) и дальше молчит.
    /// </summary>
    public class KvStatusBar : MonoBehaviour
    {
        /// <summary>Высота статус-бара.</summary>
        public const float Height = 22f;

        /// <summary>Сколько миллисекунд-секунд подсвечивается индикатор при смене режима (§26, ТЗ).</summary>
        public const float BlinkSeconds = 0.5f;

        /// <summary>Ширина слота индикатора режима (прежний слот служебной подсказки).</summary>
        private const float ModeSlotWidth = 158f;

        /// <summary>Иконка режима КАМЕРЫ и режима ИНТЕРФЕЙСА — из единого набора KvIcons.</summary>
        private const string CameraIcon = "eye";
        private const string UiIcon = "layout";

        private RectTransform root;
        private Image bg;
        private Text stateText;
        private Text robotText;
        private Text cursorText;
        private Text messageText;
        private Text themeText;
        private Text fpsText;
        /// <summary>§26: ИНДИКАТОР РЕЖИМА КУРСОРА — текст, иконка, кликабельная подложка и подсказка.</summary>
        private Text modeText;
        private Image modeIcon;
        private Image modeBg;
        private Button modeButton;
        private KvTooltipTarget modeTip;
        private KvCursorMode cursorMode = KvCursorMode.CameraMode;
        private float blinkUntil = -1f;
        private bool blinkRunning;
        /// <summary>ЭТАП 7 ТЗ «Графика»: индикатор текущего пресета (иконка + название).</summary>
        private Text graphicsText;
        private Image graphicsIcon;

        /// <summary>Собрать статус-бар. Возвращает высоту.</summary>
        public float Build(RectTransform canvas, Canvas owner)
        {
            GameObject go = new GameObject("KvStatusBar", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(1f, 0f);
            root.pivot = new Vector2(0.5f, 0f);
            root.sizeDelta = new Vector2(0f, Height);
            root.anchoredPosition = Vector2.zero;
            bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelHeader;
            bg.raycastTarget = true;

            stateText = AddField("State", 6f, 158f, TextAnchor.MiddleLeft, KvTheme.TextMain);
            AddSeparator(166f);
            robotText = AddField("Robot", 174f, 132f, TextAnchor.MiddleLeft, KvTheme.TextDim);
            AddSeparator(308f);
            cursorText = AddField("Cursor", 316f, 250f, TextAnchor.MiddleLeft, KvTheme.TextDim);
            AddSeparator(568f);

            messageText = AddField("Message", 576f, 0f, TextAnchor.MiddleLeft, KvTheme.TextDim);
            messageText.rectTransform.anchorMax = new Vector2(1f, 1f);
            messageText.rectTransform.offsetMax = new Vector2(-566f, 0f);

            // Правая группа: ИНДИКАТОР РЕЖИМА (§26), индикатор графики, тема и FPS
            // (как индикаторы рабочей среды).
            // ЭТАП 7 ТЗ: маленький индикатор пресета — иконка и название режима.
            // §26: индикатор режима курсора занял прежний слот служебной подсказки
            // (−390…−232): текст −387…−257, иконка −253…−240; он кликабелен (дублирует Tab),
            // поэтому подсказка «TAB — …» переехала в его tooltip.
            graphicsText = AddRightField("Graphics", -396f, 152f, TextAnchor.MiddleRight, KvTheme.TextDim);
            graphicsIcon = AddRightIcon("GraphicsIcon", -550f);
            fpsText = AddRightField("Fps", -6f, 58f, TextAnchor.MiddleRight, KvTheme.TextDim);
            themeText = AddRightField("Theme", -68f, 158f, TextAnchor.MiddleRight, KvTheme.TextDim);
            AddModeIndicator(-232f);

            SetGraphics("");
            SetTheme(KvTheme.ModeLabel);
            // Индикатор режима (§26) подписывается на смену режима и сразу показывает текущий.
            // Минус-подписка перед плюсом — защита от двойной подписки при пересборке оболочки.
            KvMouseCursor.ModeChanged -= OnCursorModeChanged;
            KvMouseCursor.ModeChanged += OnCursorModeChanged;
            SetCursorMode(KvMouseCursor.CurrentMode);
            return Height;
        }

        /// <summary>
        /// ИНДИКАТОР РЕЖИМА КУРСОРА (§26, ЗАДАЧА 4). Выбран статус-бар (вариант А ТЗ): он всегда
        /// виден, не перекрывает сцену и соответствует стилю FreeCAD.
        ///
        /// Что умеет: показывает ТЕКУЩИЙ режим (иконка + «Режим: Камера / UI»), подсвечивается
        /// 0,5 с при смене режима, КЛИКАБЕЛЕН (дублирует Tab — <see cref="KvMouseCursor.Toggle"/>)
        /// и имеет подсказку «Tab — переключить режим (Камера / UI)».
        /// </summary>
        private void AddModeIndicator(float x)
        {
            GameObject go = new GameObject("CursorMode", typeof(Image));
            go.transform.SetParent(root, false);
            modeBg = go.GetComponent<Image>();
            modeBg.sprite = KvTheme.WhiteSprite;
            modeBg.color = new Color(KvTheme.ButtonBg.r, KvTheme.ButtonBg.g, KvTheme.ButtonBg.b, 0.55f);
            modeBg.raycastTarget = true;                  // клик по индикатору обязан доходить
            RectTransform rt = modeBg.rectTransform;
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(ModeSlotWidth, -4f);
            rt.anchoredPosition = new Vector2(x, 0f);

            modeButton = go.AddComponent<Button>();
            modeButton.targetGraphic = modeBg;
            ColorBlock colors = modeButton.colors;
            colors.normalColor = new Color(0.86f, 0.86f, 0.86f, 0.75f);
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.selectedColor = Color.white;
            modeButton.colors = colors;
            modeButton.onClick.AddListener(delegate
            {
                KvMouseCursor.Toggle("клик по индикатору режима (§26)");
            });

            modeText = AddRightField("CursorModeText", x - 25f, ModeSlotWidth - 28f,
                TextAnchor.MiddleRight, KvTheme.TextDim);
            modeIcon = AddRightIcon("CursorModeIcon", x - 8f, CameraIcon);
            modeTip = go.AddComponent<KvTooltipTarget>();
        }

        /// <summary>Маленькая иконка у правого края (для индикаторов режима и графики).</summary>
        private Image AddRightIcon(string name, float x, string iconId = "layout")
        {
            GameObject go = new GameObject(name, typeof(Image));
            go.transform.SetParent(root, false);
            Image img = go.GetComponent<Image>();
            img.sprite = KvIcons.Get(iconId, 13);
            img.color = KvTheme.TextDisabled;
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.sizeDelta = new Vector2(13f, 13f);
            rt.anchoredPosition = new Vector2(x, 0f);
            return img;
        }

        private Text AddField(string name, float x, float width, TextAnchor anchor, Color color)
        {
            Text t = KvTheme.CreateText(root, name, "", KvTheme.FontSizeSmall, anchor, color);
            t.rectTransform.anchorMin = new Vector2(0f, 0f);
            t.rectTransform.anchorMax = new Vector2(0f, 1f);
            t.rectTransform.pivot = new Vector2(0f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(width, 0f);
            t.rectTransform.anchoredPosition = new Vector2(x, 0f);
            return t;
        }

        private Text AddRightField(string name, float x, float width, TextAnchor anchor, Color color)
        {
            Text t = KvTheme.CreateText(root, name, "", KvTheme.FontSizeSmall, anchor, color);
            t.rectTransform.anchorMin = new Vector2(1f, 0f);
            t.rectTransform.anchorMax = new Vector2(1f, 1f);
            t.rectTransform.pivot = new Vector2(1f, 0.5f);
            t.rectTransform.sizeDelta = new Vector2(width, 0f);
            t.rectTransform.anchoredPosition = new Vector2(x, 0f);
            return t;
        }

        private void AddSeparator(float x)
        {
            Image sep = KvTheme.CreatePanel(root, "Sep", KvTheme.Separator);
            sep.rectTransform.anchorMin = new Vector2(0f, 0f);
            sep.rectTransform.anchorMax = new Vector2(0f, 1f);
            sep.rectTransform.pivot = new Vector2(0f, 0.5f);
            sep.rectTransform.sizeDelta = new Vector2(1f, -8f);
            sep.rectTransform.anchoredPosition = new Vector2(x, 0f);
        }

        // ------------------------------------------------------------------ значения

        /// <summary>Состояние State Machine (Idle / PointSelected / …).</summary>
        public void SetState(string text, Color color)
        {
            if (stateText == null) return;
            stateText.text = text;
            stateText.color = color;
        }

        /// <summary>Выбранный/активный робот.</summary>
        public void SetRobot(string text)
        {
            if (robotText != null) robotText.text = text;
        }

        /// <summary>Координаты курсора/луча.</summary>
        public void SetCursor(string text)
        {
            if (cursorText != null) cursorText.text = text;
        }

        /// <summary>Статусное сообщение (то же, что в логе потока).</summary>
        public void SetMessage(string text, Color color)
        {
            if (messageText == null) return;
            messageText.text = text;
            messageText.color = color;
        }

        /// <summary>Индикатор темы («Тёмная», «Светлая», «Системная»).</summary>
        public void SetTheme(string text)
        {
            if (themeText != null) themeText.text = KvLoc.T("status.theme", "Тема") + ": " + text;
        }

        /// <summary>FPS-счётчик (проверка «UI не влияет на FPS»).</summary>
        public void SetFps(float fps)
        {
            if (fpsText != null) fpsText.text = Mathf.RoundToInt(fps) + " FPS";
        }

        /// <summary>
        /// ЭТАП 7 ТЗ «Графика»: индикатор текущего пресета. Пустая строка скрывает
        /// и подпись, и иконку — панель остаётся такой же, как была.
        /// </summary>
        public void SetGraphics(string text)
        {
            bool show = !string.IsNullOrEmpty(text);
            if (graphicsText != null)
            {
                graphicsText.text = show ? text : "";
                graphicsText.gameObject.SetActive(show);
            }
            if (graphicsIcon != null) graphicsIcon.gameObject.SetActive(show);
        }

        // ------------------------------------------------------------------ ИНДИКАТОР РЕЖИМА (§26)

        /// <summary>
        /// Показать ТЕКУЩИЙ режим курсора: текст «Режим: Камера / UI», иконку и подсказку.
        /// Метод идемпотентный — вызывать можно сколько угодно (менеджер зовёт его из
        /// <c>UpdateStatusBar</c>, а сама панель — по событию <see cref="KvMouseCursor.ModeChanged"/>).
        /// </summary>
        public void SetCursorMode(KvCursorMode value)
        {
            cursorMode = value;
            bool camera = value == KvCursorMode.CameraMode;

            if (modeText != null)
                modeText.text = KvLoc.T("status.mode", "Режим") + ": " +
                    (camera ? KvLoc.T("status.mode.camera", "Камера") : KvLoc.T("status.mode.ui", "UI"));

            if (modeIcon != null)
            {
                Sprite sprite = KvIcons.Get(camera ? CameraIcon : UiIcon, 13);
                modeIcon.sprite = sprite;
                modeIcon.enabled = sprite != null;
            }

            if (modeTip != null)
                modeTip.Set(KvLoc.T("status.mode.tip", "Tab — переключить режим (Камера / UI)"),
                    KvLoc.T("status.mode.body", "Клик по индикатору делает то же, что Tab") +
                    " · " + KvLoc.T("status.mode.now", "сейчас") + ": " +
                    (camera ? KvLoc.T("status.mode.camera", "Камера") : KvLoc.T("status.mode.ui", "UI")),
                    "Tab");

            ApplyModeColors();
        }

        /// <summary>Цвет индикатора для текущего режима: интерфейс — акцентный, камера — спокойный.</summary>
        private Color ModeColor()
        {
            return cursorMode == KvCursorMode.CameraMode ? KvTheme.TextDim : KvTheme.Accent;
        }

        private void ApplyModeColors()
        {
            Color c = ModeColor();
            if (modeText != null) modeText.color = c;
            if (modeIcon != null) modeIcon.color = c;
        }

        /// <summary>Смена режима (событие от KvMouseCursor): обновить индикатор и мигнуть 0,5 с.</summary>
        private void OnCursorModeChanged(KvCursorMode value)
        {
            SetCursorMode(value);
            blinkUntil = Time.unscaledTime + BlinkSeconds;
            blinkRunning = true;
        }

        /// <summary>
        /// Подсветка смены режима (ТЗ: мигание 0,5 с). Работает только пока идёт подсветка —
        /// в остальное время кадра не трогает (на FPS не влияет).
        /// </summary>
        private void Update()
        {
            if (!blinkRunning) return;
            float left = blinkUntil - Time.unscaledTime;
            if (left <= 0f)
            {
                blinkRunning = false;
                ApplyModeColors();
                return;
            }
            float k = 0.5f + 0.5f * Mathf.Sin(left * 40f);      // ≈6 Гц — заметно, но не мельтешит
            Color c = Color.Lerp(ModeColor(), Color.white, k * 0.8f);
            if (modeText != null) modeText.color = c;
            if (modeIcon != null) modeIcon.color = c;
        }

        private void OnDestroy()
        {
            KvMouseCursor.ModeChanged -= OnCursorModeChanged;
        }

        /// <summary>Корень панели.</summary>
        public RectTransform Root { get { return root; } }

        /// <summary>Текущий текст состояния (диагностика).</summary>
        public string StateText { get { return stateText != null ? stateText.text : ""; } }
        /// <summary>Текущий текст сообщения (диагностика).</summary>
        public string MessageText { get { return messageText != null ? messageText.text : ""; } }
        /// <summary>Текущий текст координат (диагностика).</summary>
        public string CursorText { get { return cursorText != null ? cursorText.text : ""; } }
        /// <summary>Текущий текст робота (диагностика).</summary>
        public string RobotText { get { return robotText != null ? robotText.text : ""; } }
        /// <summary>Текущий текст темы (диагностика).</summary>
        public string ThemeText { get { return themeText != null ? themeText.text : ""; } }
        /// <summary>Текущий текст индикатора графики (диагностика).</summary>
        public string GraphicsText { get { return graphicsText != null ? graphicsText.text : ""; } }
        /// <summary>
        /// ТЕКУЩИЙ ТЕКСТ СЧЁТЧИКА FPS (диагностика/замер). Нужен снимку обхода (заказ 6a):
        /// поле `Fps` — одно из семи полей статус-бара, и без геттера его значение было
        /// недоступно ничему, кроме прямого поиска по иерархии.
        /// </summary>
        public string FpsText { get { return fpsText != null ? fpsText.text : ""; } }
        /// <summary>§26: текущий текст индикатора режима (диагностика).</summary>
        public string CursorModeText { get { return modeText != null ? modeText.text : ""; } }
        /// <summary>§26: режим, который показывает индикатор (диагностика).</summary>
        public KvCursorMode ShownCursorMode { get { return cursorMode; } }
        /// <summary>§26: идёт ли подсветка смены режима (диагностика).</summary>
        public bool ModeBlinking { get { return blinkRunning; } }
        /// <summary>§26: кликабельная кнопка индикатора — по клику переключает режим (диагностика/тесты).</summary>
        public Button ModeButton { get { return modeButton; } }

        /// <summary>Перекрасить статус-бар под текущую тему.</summary>
        public void Repaint()
        {
            if (bg != null) bg.color = KvTheme.PanelHeader;
            if (robotText != null) robotText.color = KvTheme.TextDim;
            if (cursorText != null) cursorText.color = KvTheme.TextDim;
            if (fpsText != null) fpsText.color = KvTheme.TextDim;
            if (themeText != null) themeText.color = KvTheme.TextDim;
            ApplyModeColors();                                   // §26: цвет индикатора зависит от режима
            if (graphicsText != null) graphicsText.color = KvTheme.TextDim;
            if (graphicsIcon != null) graphicsIcon.color = KvTheme.TextDisabled;
            if (modeBg != null)
                modeBg.color = new Color(KvTheme.ButtonBg.r, KvTheme.ButtonBg.g, KvTheme.ButtonBg.b, 0.55f);
        }
    }
}
