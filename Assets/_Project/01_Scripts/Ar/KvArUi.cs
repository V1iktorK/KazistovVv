using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvAr
{
    /// <summary>
    /// ЭКРАННЫЙ ИНТЕРФЕЙС AR-РЕЖИМА: состояние, подсказки, кнопки и сообщения об отказе.
    ///
    /// ЗАЧЕМ СВОЙ КАНВАС, ЕСЛИ В ПРОЕКТЕ ЕСТЬ UI. Интерфейс проекта (110 команд) — это мир
    /// шлема: world-space панели, наведение лучом, мелкие иконки. На планшете нужен ОБЫЧНЫЙ
    /// экранный слой: крупные кнопки под палец и одна строка состояния, по которой наладчик
    /// читает факты (плоскости, якорь, кадры). Этот слой не заменяет меню команд, а служит
    /// пультом AR: поставить/переставить робота, открыть меню, включить режим точки, сбросить вид.
    ///
    /// ПОЧЕМУ КРУПНЫЕ РАЗМЕРЫ. Ориентир владельца — планшет 12,1" (Redmi Pad 2 Pro). Кнопка
    /// 96 px при опорном разрешении 1920×1080 — это ~13 мм на экране: палец попадает без
    /// прицеливания (мелкие цели 24 px из ПК-версии для пальца не годятся — это записано в
    /// отчёте разведки).
    ///
    /// ПОЧЕМУ БЕЗ ТЕМЫ ПРОЕКТА. `KvTheme` создаёт элементы через `KvTheme.CreateText` и зависит
    /// от шрифтов/палитры интерфейса, который поднимается позже. Экран AR обязан работать даже
    /// тогда, когда основной интерфейс ещё не собран (загрузочная сцена) — поэтому здесь только
    /// `UnityEngine.UI` и встроенный шрифт.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArUi : MonoBehaviour
    {
        /// <summary>Опорное разрешение экранного слоя (как у ПК-версии).</summary>
        public Vector2 referenceResolution = new Vector2(1920f, 1080f);

        /// <summary>Высота кнопки в пикселях опорного разрешения.</summary>
        public float buttonHeight = 96f;

        /// <summary>Ширина кнопки в пикселях опорного разрешения.</summary>
        public float buttonWidth = 300f;

        /// <summary>Строить интерфейс сразу при старте.</summary>
        public bool buildOnStart = true;

        /// <summary>Писать в журнал о построении (обязательная строка-доказательство).</summary>
        public bool logOnBuild = true;

        private Canvas canvas;
        private Text statusText;
        private Text hintText;
        private Image statusBackground;
        private GameObject hintsRow;
        private GameObject messageRoot;
        private Text messageText;
        private Button messageButton;
        private Text messageButtonText;
        private Action messageAction;
        private readonly List<Button> buttons = new List<Button>();

        /// <summary>Интерфейс построен (для проверок и журнала).</summary>
        public bool Built { get { return canvas != null; } }

        /// <summary>Текст строки состояния — как он выглядит на экране (для теста).</summary>
        public string StatusText { get { return statusText != null ? statusText.text : string.Empty; } }

        /// <summary>Текст подсказки (для теста).</summary>
        public string HintText { get { return hintText != null ? hintText.text : string.Empty; } }

        /// <summary>Сообщение об отказе показано (для проверок).</summary>
        public bool MessageVisible { get { return messageRoot != null && messageRoot.activeSelf; } }

        /// <summary>Сколько кнопок создано (для проверок).</summary>
        public int ButtonCount { get { return buttons.Count; } }

        void Start()
        {
            if (buildOnStart) EnsureBuilt();
        }

        /// <summary>
        /// ВСТРОЕННЫЙ ШРИФТ UNITY. `LegacyRuntime.ttf` — единственный шрифт, который есть в
        /// любом билде без импорта ассетов проекта (в Unity 6 прежний `Arial.ttf` удалён).
        /// </summary>
        public static Font BuiltinFont
        {
            get
            {
                Font font = null;
                try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
                catch (Exception) { font = null; }
                if (font != null) return font;
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch (Exception) { font = null; }
                return font;
            }
        }

        /// <summary>ПОСТРОИТЬ ИНТЕРФЕЙС (идемпотентно: второй вызов ничего не делает).</summary>
        public void EnsureBuilt()
        {
            if (canvas != null) return;

            EnsureEventSystem();

            GameObject canvasGo = new GameObject("ArUiCanvas", typeof(RectTransform));
            canvasGo.transform.SetParent(transform, false);
            canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;      // выше стартового меню (200), ниже ничего не прячем
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            BuildStatus(canvasGo.transform);
            BuildHint(canvasGo.transform);
            BuildButtonRow(canvasGo.transform);
            BuildMessage(canvasGo.transform);
            HideMessage();

            if (logOnBuild)
                Debug.Log("[KvAr] ИНТЕРФЕЙС AR: построен · опорное разрешение " +
                          referenceResolution.x.ToString("0") + "×" +
                          referenceResolution.y.ToString("0") + " · кнопка " +
                          buttonWidth.ToString("0") + "×" + buttonHeight.ToString("0") + " px");
        }

        private void BuildStatus(Transform parent)
        {
            GameObject bg = new GameObject("ArStatusBg", typeof(RectTransform));
            bg.transform.SetParent(parent, false);
            statusBackground = bg.AddComponent<Image>();
            statusBackground.color = new Color(0f, 0f, 0f, 0.55f);
            RectTransform bgRect = (RectTransform)bg.transform;
            bgRect.anchorMin = new Vector2(0f, 1f);
            bgRect.anchorMax = new Vector2(0f, 1f);
            bgRect.pivot = new Vector2(0f, 1f);
            bgRect.anchoredPosition = new Vector2(16f, -16f);
            bgRect.sizeDelta = new Vector2(1180f, 210f);

            GameObject textGo = new GameObject("ArStatusText", typeof(RectTransform));
            textGo.transform.SetParent(bg.transform, false);
            statusText = textGo.AddComponent<Text>();
            statusText.font = BuiltinFont;
            statusText.fontSize = 30;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.UpperLeft;
            statusText.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(12f, 8f);
            textRect.offsetMax = new Vector2(-12f, -8f);
            statusText.text = "AR: запуск…";
        }

        private void BuildHint(Transform parent)
        {
            GameObject row = new GameObject("ArHint", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            hintsRow = row;
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0.5f, 0f);
            rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, 132f);
            rowRect.sizeDelta = new Vector2(1600f, 70f);

            Image bg = row.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject textGo = new GameObject("ArHintText", typeof(RectTransform));
            textGo.transform.SetParent(row.transform, false);
            hintText = textGo.AddComponent<Text>();
            hintText.font = BuiltinFont;
            hintText.fontSize = 32;
            hintText.color = Color.white;
            hintText.alignment = TextAnchor.MiddleCenter;
            hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintText.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(10f, 4f);
            textRect.offsetMax = new Vector2(-10f, -4f);
            hintText.text = "Наведите камеру на стол";
        }

        private void BuildButtonRow(Transform parent)
        {
            GameObject row = new GameObject("ArButtons", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRect = (RectTransform)row.transform;
            rowRect.anchorMin = new Vector2(0.5f, 0f);
            rowRect.anchorMax = new Vector2(0.5f, 0f);
            rowRect.pivot = new Vector2(0.5f, 0f);
            rowRect.anchoredPosition = new Vector2(0f, 20f);
            rowRect.sizeDelta = new Vector2(1800f, buttonHeight);

            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 16f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            buttons.Clear();
            // Сами кнопки добавляет `KvArRuntime` через `AddButton`: интерфейс не знает,
            // каким системам они служат, — иначе его нельзя переиспользовать в загрузке.
            row.name = "ArButtons";
        }

        private void BuildMessage(Transform parent)
        {
            GameObject root = new GameObject("ArMessage", typeof(RectTransform));
            root.transform.SetParent(parent, false);
            messageRoot = root;
            RectTransform rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            Image dim = root.AddComponent<Image>();
            dim.color = new Color(0f, 0f, 0f, 0.82f);

            GameObject textGo = new GameObject("ArMessageText", typeof(RectTransform));
            textGo.transform.SetParent(root.transform, false);
            messageText = textGo.AddComponent<Text>();
            messageText.font = BuiltinFont;
            messageText.fontSize = 40;
            messageText.color = Color.white;
            messageText.alignment = TextAnchor.MiddleCenter;
            messageText.horizontalOverflow = HorizontalWrapMode.Wrap;
            messageText.verticalOverflow = VerticalWrapMode.Overflow;
            RectTransform textRect = (RectTransform)textGo.transform;
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.anchoredPosition = new Vector2(0f, 60f);
            textRect.sizeDelta = new Vector2(1500f, 600f);

            GameObject buttonGo = new GameObject("ArMessageButton", typeof(RectTransform));
            buttonGo.transform.SetParent(root.transform, false);
            RectTransform buttonRect = (RectTransform)buttonGo.transform;
            buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
            buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
            buttonRect.pivot = new Vector2(0.5f, 0.5f);
            buttonRect.anchoredPosition = new Vector2(0f, -320f);
            buttonRect.sizeDelta = new Vector2(560f, 110f);
            Image buttonBg = buttonGo.AddComponent<Image>();
            buttonBg.color = new Color(0.15f, 0.45f, 0.9f, 0.95f);
            messageButton = buttonGo.AddComponent<Button>();
            messageButton.targetGraphic = buttonBg;
            messageButton.onClick.AddListener(OnMessageButtonClicked);

            GameObject labelGo = new GameObject("ArMessageButtonText", typeof(RectTransform));
            labelGo.transform.SetParent(buttonGo.transform, false);
            messageButtonText = labelGo.AddComponent<Text>();
            messageButtonText.font = BuiltinFont;
            messageButtonText.fontSize = 34;
            messageButtonText.color = Color.white;
            messageButtonText.alignment = TextAnchor.MiddleCenter;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
        }

        /// <summary>ДОБАВИТЬ КНОПКУ В НИЖНИЙ РЯД. Возвращает созданную кнопку.</summary>
        public Button AddButton(string label, Action onClick)
        {
            EnsureBuilt();
            Transform row = canvas.transform.Find("ArButtons");
            if (row == null) return null;

            GameObject go = new GameObject("ArButton_" + label, typeof(RectTransform));
            go.transform.SetParent(row, false);
            RectTransform rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(buttonWidth, buttonHeight);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.16f, 0.28f, 0.88f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = bg;
            if (onClick != null) button.onClick.AddListener(delegate { onClick(); });

            GameObject labelGo = new GameObject("Text", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            Text text = labelGo.AddComponent<Text>();
            text.font = BuiltinFont;
            text.fontSize = 30;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.text = label;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            RectTransform labelRect = (RectTransform)labelGo.transform;
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = new Vector2(6f, 4f);
            labelRect.offsetMax = new Vector2(-6f, -4f);

            buttons.Add(button);
            return button;
        }

        /// <summary>ОБНОВИТЬ СТРОКУ СОСТОЯНИЯ (текст уже собран вызывающим — без логики здесь).</summary>
        public void SetStatus(string text)
        {
            if (statusText == null) return;
            statusText.text = string.IsNullOrEmpty(text) ? "AR" : text;
        }

        /// <summary>ОБНОВИТЬ ПОДСКАЗКУ (короткая строка внизу экрана).</summary>
        public void SetHint(string text)
        {
            if (hintText == null) return;
            hintText.text = text ?? string.Empty;
            if (hintsRow != null) hintsRow.SetActive(!string.IsNullOrEmpty(text));
        }

        /// <summary>СПРЯТАТЬ ПОДСКАЗКУ (после постановки робота).</summary>
        public void HideHint() { SetHint(string.Empty); }

        /// <summary>
        /// ПОКАЗАТЬ СООБЩЕНИЕ ОБ ОТКАЗЕ (Ж1/Ж2). Кнопка необязательна: если действие не задано,
        /// остаётся только текст — приложение обязано продолжать работать, а не падать.
        /// </summary>
        public void ShowMessage(string text, string buttonLabel = null, Action action = null)
        {
            EnsureBuilt();
            if (messageRoot == null) return;
            messageRoot.SetActive(true);
            if (messageText != null) messageText.text = text ?? string.Empty;
            messageAction = action;
            bool hasButton = !string.IsNullOrEmpty(buttonLabel) && action != null;
            if (messageButton != null) messageButton.gameObject.SetActive(hasButton);
            if (hasButton && messageButtonText != null) messageButtonText.text = buttonLabel;
            SetHint(string.Empty);
        }

        /// <summary>СКРЫТЬ СООБЩЕНИЕ (например, оператор разрешил камеру).</summary>
        public void HideMessage()
        {
            if (messageRoot == null) return;
            messageRoot.SetActive(false);
            messageAction = null;
        }

        private void OnMessageButtonClicked()
        {
            Action action = messageAction;
            if (action != null && messageRoot != null) messageRoot.SetActive(false);
            messageAction = null;
            if (action != null) action();
        }

        /// <summary>ВКЛЮЧИТЬ/ВЫКЛЮЧИТЬ КНОПКИ (в загрузочной сцене кнопок нет).</summary>
        public void SetButtonsVisible(bool visible)
        {
            if (canvas == null) return;
            Transform row = canvas.transform.Find("ArButtons");
            if (row != null) row.gameObject.SetActive(visible);
        }

        /// <summary>
        /// EVENT SYSTEM ДЛЯ ПАЛЬЦА. В XR-сцене он уже есть (`XrEventSystem`, модуль Input System),
        /// а в загрузочной сцене его нет — без него кнопки не нажимаются. Создаём только если
        /// действительно нет, чтобы не завести второй EventSystem (Unity ругается и путается).
        /// </summary>
        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            EventSystem[] all = UnityEngine.Object.FindObjectsByType<EventSystem>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (all != null && all.Length > 0) return;

            GameObject go = new GameObject("ArEventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            Debug.Log("[KvAr] ИНТЕРФЕЙС AR: создан EventSystem с модулем Input System");
        }

        /// <summary>
        /// СТРОКА СОСТОЯНИЯ ИЗ ФАКТОВ (чистая функция — проверяется тестом без сцены).
        /// </summary>
        public static string BuildStatusLine(bool arSessionReady, bool cameraGranted, int planesFound,
                                             bool worldPlaced, bool robotPlaced, float fps,
                                             float anchorHeightMeters)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("AR · ");
            sb.Append(cameraGranted ? "камера есть" : "камера НЕТ");
            sb.Append(" · сессия: ").Append(arSessionReady ? "готова" : "не готова");
            sb.Append(" · плоскостей: ").Append(planesFound);
            sb.Append(" · ");
            sb.Append(worldPlaced ? (robotPlaced ? "робот поставлен" : "якорь есть") : "наведите на стол");
            if (worldPlaced && anchorHeightMeters > -1000f)
                sb.Append(" · высота якоря ")
                  .Append(anchorHeightMeters.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(" м");
            if (fps > 0f)
                sb.Append(" · ").Append(fps.ToString("0", System.Globalization.CultureInfo.InvariantCulture))
                  .Append(" кадр/с");
            return sb.ToString();
        }
    }
}
