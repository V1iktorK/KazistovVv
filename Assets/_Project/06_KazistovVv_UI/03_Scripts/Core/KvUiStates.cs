using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// ЭЛЕМЕНТЫ СОСТОЯНИЙ (ЭТАП 8): интерфейс реагирует на события, а не молчит.
    ///
    ///   • ERROR STATE — красная плашка в углу экрана с текстом ошибки/исключения и
    ///     кнопкой «Подробнее» (разворачивает стек). Ошибки приходят из
    ///     `Application.logMessageReceived`, то есть НЕ только в консоль.
    ///   • LOADING STATE — полоса прогресса (детерминированная и «неопределённая»)
    ///     во время планирования, записи и экспорта.
    ///   • EMPTY STATE — подсказка-строка в статус-баре («Выберите точку красным лазером»).
    ///
    /// Модуль ничего не знает о роботах: он только показывает состояние, которое
    /// сообщают менеджер UI и модули функций.
    /// </summary>
    public class KvUiStates : MonoBehaviour
    {
        /// <summary>Сколько ошибок перехвачено за сессию (диагностика).</summary>
        public static int ErrorCount { get; private set; }
        /// <summary>Последняя ошибка (диагностика).</summary>
        public static string LastError { get; private set; }

        private static KvUiStates instance;

        private RectTransform canvasRect;
        private RectTransform errorPlate;
        private RectTransform errorDetails;
        private Text errorTitle;
        private Text errorDetailsText;
        private Image errorBg;
        private bool detailsOpen;

        private RectTransform loadingBar;
        private RectTransform loadingFill;
        private Image loadingBg;
        private Text loadingLabel;
        private bool loading;
        private float loadingStart;
        private float loadingProgress = -1f;      // < 0 — «неопределённый» прогресс
        private string loadingText = "";

        private Text emptyHint;
        private string emptyHintText = "";
        private float emptySince = -1f;

        /// <summary>Активный модуль состояний.</summary>
        public static KvUiStates Current { get { return instance; } }

        /// <summary>Идёт загрузка/выполнение длительной операции.</summary>
        public bool Loading { get { return loading; } }
        /// <summary>Плашка ошибки видна.</summary>
        public bool ErrorVisible { get { return errorPlate != null && errorPlate.gameObject.activeSelf; } }
        /// <summary>Подробности ошибки развёрнуты.</summary>
        public bool DetailsOpen { get { return detailsOpen; } }
        /// <summary>Подпись текущей загрузки (диагностика).</summary>
        public string LoadingText { get { return loadingText; } }
        /// <summary>Текущий прогресс (0..1; −1 — неопределённый).</summary>
        public float Progress { get { return loadingProgress; } }
        /// <summary>Текст подсказки пустого состояния (диагностика).</summary>
        public string EmptyHint { get { return emptyHintText; } }

        /// <summary>Создать слой состояний на канвасе интерфейса.</summary>
        public static KvUiStates Create(RectTransform canvas)
        {
            GameObject go = new GameObject("KvUiStates", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rt = (RectTransform)go.transform;
            KvTheme.Stretch(rt);
            instance = go.AddComponent<KvUiStates>();
            instance.canvasRect = canvas;
            instance.Build();
            return instance;
        }

        private void Build()
        {
            BuildLoading();
            BuildErrorPlate();
            BuildEmptyHint();

            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------ ERROR STATE

        private void BuildErrorPlate()
        {
            GameObject go = new GameObject("ErrorPlate", typeof(Image));
            go.transform.SetParent(transform, false);
            errorPlate = (RectTransform)go.transform;
            errorPlate.anchorMin = new Vector2(1f, 0f);
            errorPlate.anchorMax = new Vector2(1f, 0f);
            errorPlate.pivot = new Vector2(1f, 0f);
            errorPlate.sizeDelta = new Vector2(360f, 54f);
            errorPlate.anchoredPosition = new Vector2(-10f, KvStatusBar.Height + 8f);
            errorBg = go.GetComponent<Image>();
            errorBg.sprite = KvTheme.WhiteSprite;
            errorBg.color = new Color(KvTheme.Error.r, KvTheme.Error.g, KvTheme.Error.b, 0.92f);
            errorBg.raycastTarget = true;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Error;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(errorPlate, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get("error", 16);
            icon.color = Color.white;
            icon.raycastTarget = false;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(0f, 1f);
            icon.rectTransform.pivot = new Vector2(0f, 1f);
            icon.rectTransform.sizeDelta = new Vector2(16f, 16f);
            icon.rectTransform.anchoredPosition = new Vector2(6f, -6f);

            errorTitle = KvTheme.CreateText(errorPlate, "Title", "", KvTheme.FontSizeSmall,
                TextAnchor.UpperLeft, Color.white);
            errorTitle.horizontalOverflow = HorizontalWrapMode.Wrap;
            errorTitle.rectTransform.anchorMin = new Vector2(0f, 1f);
            errorTitle.rectTransform.anchorMax = new Vector2(1f, 1f);
            errorTitle.rectTransform.pivot = new Vector2(0f, 1f);
            errorTitle.rectTransform.offsetMin = new Vector2(28f, 0f);
            errorTitle.rectTransform.offsetMax = new Vector2(-8f, 0f);
            errorTitle.rectTransform.sizeDelta = new Vector2(-36f, 34f);
            errorTitle.rectTransform.anchoredPosition = new Vector2(0f, -5f);

            Button more = KvTheme.CreateSmallButton(errorPlate, "More",
                KvLoc.T("error.more", "Подробнее"), ToggleDetails);
            RectTransform mr = (RectTransform)more.transform;
            mr.anchorMin = mr.anchorMax = new Vector2(0f, 0f);
            mr.pivot = new Vector2(0f, 0f);
            mr.sizeDelta = new Vector2(88f, 16f);
            mr.anchoredPosition = new Vector2(28f, 4f);

            Button close = KvTheme.CreateSmallButton(errorPlate, "Close",
                KvLoc.T("error.hide", "Скрыть"), HideError);
            RectTransform cr = (RectTransform)close.transform;
            cr.anchorMin = cr.anchorMax = new Vector2(1f, 0f);
            cr.pivot = new Vector2(1f, 0f);
            cr.sizeDelta = new Vector2(64f, 16f);
            cr.anchoredPosition = new Vector2(-6f, 4f);

            GameObject detGo = new GameObject("Details", typeof(Image));
            detGo.transform.SetParent(errorPlate, false);
            errorDetails = (RectTransform)detGo.transform;
            errorDetails.anchorMin = new Vector2(0f, 0f);
            errorDetails.anchorMax = new Vector2(1f, 0f);
            errorDetails.pivot = new Vector2(0.5f, 1f);
            errorDetails.sizeDelta = new Vector2(-12f, 96f);
            errorDetails.anchoredPosition = new Vector2(0f, -2f);
            Image detBg = detGo.GetComponent<Image>();
            detBg.sprite = KvTheme.WhiteSprite;
            detBg.color = new Color(0f, 0f, 0f, 0.55f);
            detBg.raycastTarget = true;
            errorDetailsText = KvTheme.CreateText(errorDetails, "Text", "", KvTheme.FontSizeSmall - 1,
                TextAnchor.UpperLeft, Color.white);
            errorDetailsText.horizontalOverflow = HorizontalWrapMode.Wrap;
            errorDetailsText.verticalOverflow = VerticalWrapMode.Truncate;
            KvTheme.Stretch(errorDetailsText.rectTransform, 6f, 6f, 4f, 4f);
            errorDetails.gameObject.SetActive(false);

            errorPlate.gameObject.SetActive(false);
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            // Ошибка среды batch-прогона (HDRP без графического устройства) — не наша:
            if (!string.IsNullOrEmpty(condition) && condition.Contains("No graphic device")) return;
            ReportError(condition, stackTrace);
        }

        /// <summary>Показать ошибку (из лога Unity или из кода).</summary>
        public static void ReportError(string message, string details)
        {
            ErrorCount++;
            LastError = message;
            if (instance == null) return;
            if (!KvSettings.ErrorPlate) return;
            instance.ShowError(message, details);
        }

        private void ShowError(string message, string details)
        {
            if (errorPlate == null) return;
            string text = string.IsNullOrEmpty(message) ? KvLoc.T("error.unknown", "Неизвестная ошибка")
                : message;
            if (text.Length > 220) text = text.Substring(0, 217) + "…";
            errorTitle.text = text;
            errorDetailsText.text = string.IsNullOrEmpty(details) ? text : details;
            detailsOpen = false;
            errorDetails.gameObject.SetActive(false);
            errorPlate.sizeDelta = new Vector2(360f, 54f);
            errorPlate.gameObject.SetActive(true);
            errorPlate.SetAsLastSibling();
        }

        private void ToggleDetails()
        {
            detailsOpen = !detailsOpen;
            if (errorDetails != null) errorDetails.gameObject.SetActive(detailsOpen);
            if (errorPlate != null)
                errorPlate.sizeDelta = new Vector2(360f, detailsOpen ? 156f : 54f);
        }

        /// <summary>Скрыть плашку ошибки.</summary>
        public void HideError()
        {
            detailsOpen = false;
            if (errorDetails != null) errorDetails.gameObject.SetActive(false);
            if (errorPlate != null) errorPlate.gameObject.SetActive(false);
        }

        // ------------------------------------------------------------------ LOADING STATE

        private void BuildLoading()
        {
            GameObject go = new GameObject("LoadingBar", typeof(Image));
            go.transform.SetParent(transform, false);
            loadingBar = (RectTransform)go.transform;
            loadingBar.anchorMin = new Vector2(0f, 0f);
            loadingBar.anchorMax = new Vector2(1f, 0f);
            loadingBar.pivot = new Vector2(0.5f, 0f);
            loadingBar.sizeDelta = new Vector2(0f, 16f);
            loadingBar.anchoredPosition = new Vector2(0f, KvStatusBar.Height + 2f);
            loadingBg = go.GetComponent<Image>();
            loadingBg.sprite = KvTheme.WhiteSprite;
            loadingBg.color = new Color(0f, 0f, 0f, 0.55f);
            loadingBg.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(loadingBar, false);
            loadingFill = (RectTransform)fillGo.transform;
            loadingFill.anchorMin = new Vector2(0f, 0f);
            loadingFill.anchorMax = new Vector2(0f, 1f);
            loadingFill.pivot = new Vector2(0f, 0.5f);
            loadingFill.sizeDelta = new Vector2(0f, -2f);
            loadingFill.anchoredPosition = new Vector2(1f, 0f);
            Image fill = loadingFill.GetComponent<Image>();
            fill.sprite = KvTheme.WhiteSprite;
            fill.color = KvTheme.Accent;
            fill.raycastTarget = false;

            loadingLabel = KvTheme.CreateText(loadingBar, "Label", "", KvTheme.FontSizeSmall - 1,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            loadingLabel.rectTransform.anchorMin = new Vector2(0f, 0f);
            loadingLabel.rectTransform.anchorMax = new Vector2(1f, 1f);
            loadingLabel.rectTransform.offsetMin = new Vector2(8f, 0f);
            loadingLabel.rectTransform.offsetMax = new Vector2(-8f, 0f);

            loadingBar.gameObject.SetActive(false);
        }

        /// <summary>
        /// Начать длительную операцию (планирование, запись, экспорт).
        ///
        /// P2-10 (11.11.2026). ЧТО БЫЛО: `KazistovVvUIManager.UpdateUiStates()` зовёт `Begin`
        /// КАЖДЫЙ кадр, пока идёт планирование, а `Begin` каждый раз выставлял
        /// `SetAsLastSibling()` (перестановка в иерархии) и заново присваивал строку статуса.
        /// Перестановка потомка помечает канвас грязным — весь интерфейс перестраивался 72 раза
        /// в секунду. ЧЕМ ЭТО ГРОЗИЛО: перестройка канваса съедала кадр 13,9 мс именно во время
        /// планирования, когда оператор ждёт отклика.
        /// ТЕПЕРЬ повторный вызов с тем же текстом и прогрессом ничего не делает.
        /// </summary>
        public static void Begin(string label, float progress = -1f)
        {
            if (instance == null) return;
            string text = label ?? "";

            if (instance.loading &&
                instance.loadingProgress == progress &&
                string.Equals(instance.loadingText, text, System.StringComparison.Ordinal) &&
                (instance.loadingBar == null || instance.loadingBar.gameObject.activeSelf))
            {
                return;   // состояние не изменилось — ни перестановки, ни перестройки канваса
            }

            instance.loading = true;
            instance.loadingText = text;
            instance.loadingProgress = progress;
            instance.loadingStart = Time.realtimeSinceStartup;
            if (instance.loadingBar != null)
            {
                if (!instance.loadingBar.gameObject.activeSelf)
                    instance.loadingBar.gameObject.SetActive(true);
                instance.loadingBar.SetAsLastSibling();   // вызывается только при начале операции
            }
        }

        /// <summary>Обновить прогресс (0..1; −1 — «неопределённый», полоса бежит).</summary>
        public static void SetProgress(float progress, string label = null)
        {
            if (instance == null) return;
            instance.loadingProgress = progress;
            if (!string.IsNullOrEmpty(label)) instance.loadingText = label;
        }

        /// <summary>Завершить длительную операцию.</summary>
        public static void End()
        {
            if (instance == null) return;
            instance.loading = false;
            instance.loadingProgress = -1f;
            if (instance.loadingBar != null) instance.loadingBar.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!loading || loadingBar == null) return;

            float width = canvasRect != null ? canvasRect.rect.width : 1920f;
            float fraction;
            if (loadingProgress >= 0f)
            {
                fraction = Mathf.Clamp01(loadingProgress);
            }
            else
            {
                // «Неопределённый» прогресс: полоса бежит по кругу — видно, что процесс идёт.
                float t = (Time.unscaledTime * 0.55f) % 1f;
                fraction = 0.18f;
                float x = Mathf.Lerp(0f, width * (1f - fraction), t);
                loadingFill.sizeDelta = new Vector2(width * fraction, -2f);
                loadingFill.anchoredPosition = new Vector2(x, 0f);
                if (loadingLabel != null && !string.IsNullOrEmpty(loadingText))
                    loadingLabel.text = loadingText + "…";
                return;
            }

            loadingFill.sizeDelta = new Vector2(Mathf.Max(0f, width * fraction), -2f);
            loadingFill.anchoredPosition = new Vector2(1f, 0f);
            if (loadingLabel != null)
                loadingLabel.text = (string.IsNullOrEmpty(loadingText) ? "" : loadingText + " — ") +
                    Mathf.RoundToInt(fraction * 100f) + " %";
        }

        // ------------------------------------------------------------------ EMPTY STATE

        private void BuildEmptyHint()
        {
            emptyHint = KvTheme.CreateText((RectTransform)transform, "EmptyHint", "",
                KvTheme.FontSizeSmall, TextAnchor.MiddleCenter, KvTheme.TextDim);
            emptyHint.rectTransform.anchorMin = new Vector2(0.5f, 0f);
            emptyHint.rectTransform.anchorMax = new Vector2(0.5f, 0f);
            emptyHint.rectTransform.pivot = new Vector2(0.5f, 0f);
            emptyHint.rectTransform.sizeDelta = new Vector2(560f, 20f);
            emptyHint.rectTransform.anchoredPosition = new Vector2(0f, KvStatusBar.Height + 26f);
            emptyHint.gameObject.SetActive(false);
        }

        /// <summary>
        /// ЭТАП 8: подсказка пустого состояния («Выберите точку красным лазером»).
        /// Пустая строка скрывает подсказку.
        /// </summary>
        public static void SetEmptyHint(string text)
        {
            if (instance == null) return;
            if (string.Equals(instance.emptyHintText, text ?? "")) return;
            instance.emptyHintText = text ?? "";
            if (instance.emptyHint == null) return;
            bool show = !string.IsNullOrEmpty(text);
            instance.emptyHint.gameObject.SetActive(show);
            if (show) instance.emptyHint.text = text;
            instance.emptySince = show ? Time.realtimeSinceStartup : -1f;
        }

        /// <summary>Секунд показана подсказка пустого состояния (диагностика).</summary>
        public float EmptyHintAge
        {
            get { return emptySince < 0f ? 0f : Time.realtimeSinceStartup - emptySince; }
        }

        /// <summary>Перекрасить под текущую тему.</summary>
        public void Repaint()
        {
            if (loadingBg != null) loadingBg.color = new Color(0f, 0f, 0f, 0.55f);
            if (loadingFill != null) loadingFill.GetComponent<Image>().color = KvTheme.Accent;
            if (errorBg != null)
                errorBg.color = new Color(KvTheme.Error.r, KvTheme.Error.g, KvTheme.Error.b, 0.92f);
            if (emptyHint != null) emptyHint.color = KvTheme.TextDim;
        }
    }
}
