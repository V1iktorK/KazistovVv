using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// ВИРТУАЛЬНЫЙ ГЕЙМПАД (ЭТАП 9): мини-индикатор на экране, который показывает
    /// ТЕКУЩИЕ НАЖАТИЯ — какой стик отклонён, какие кнопки нажаты, насколько выжаты
    /// триггеры. Нужен потому, что оператор взял геймпад впервые: видно, что система
    /// действительно видит нажатие, и не нужно угадывать раскладку.
    ///
    /// Панель живёт в левом нижнем углу (над статус-баром), скрывается настройкой
    /// «Виртуальный геймпад» (вкладка «Интерфейс»). Когда геймпад не подключён,
    /// показывается одна строка «Нет геймпада» — управление при этом не активируется.
    /// </summary>
    public class KvGamepadHud : MonoBehaviour
    {
        private const float PanelWidth = 316f;
        private const float PanelHeight = 104f;

        private RectTransform root;
        private Image bg;
        private Text title;
        private Text lastAction;
        private Image leftDot;
        private Image rightDot;
        private Image leftTriggerFill;
        private Image rightTriggerFill;
        private readonly Dictionary<string, Image> pills = new Dictionary<string, Image>();

        /// <summary>Панель видна (диагностика).</summary>
        public bool Visible { get { return root != null && root.gameObject.activeSelf; } }
        /// <summary>Сколько «кнопок-таблеток» на индикаторе (диагностика).</summary>
        public int PillCount { get { return pills.Count; } }
        /// <summary>Подпись состояния геймпада (диагностика).</summary>
        public string StatusText { get { return title != null ? title.text : ""; } }

        /// <summary>
        /// ИНТЕРВАЛ ОБНОВЛЕНИЯ ИНДИКАТОРА, СЕКУНД (оптимизация MR, 17.11.2026).
        /// Общая функция <see cref="KazistovVvFeatures.KvMrPerformance"/>: в MR обновления идут
        /// реже, чем в VR, — и в том, и в другом случае по таймеру, а не каждый кадр.
        /// </summary>
        public float UpdateInterval
        {
            get { return KazistovVvFeatures.KvMrPerformance.UiUpdateIntervalSeconds(); }
        }

        /// <summary>Когда последний раз обновлялись строки (таймер <see cref="UpdateInterval"/>).</summary>
        private float lastTick = -1000f;

        /// <summary>Собрать индикатор на канвасе.</summary>
        public static KvGamepadHud Create(RectTransform canvas)
        {
            GameObject go = new GameObject("KvGamepadHud", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rt = (RectTransform)go.transform;
            KvTheme.Stretch(rt);
            KvGamepadHud hud = go.AddComponent<KvGamepadHud>();
            hud.canvasRect = canvas;
            hud.Build();
            return hud;
        }

        private RectTransform canvasRect;

        private void Build()
        {
            GameObject panelGo = new GameObject("Panel", typeof(Image));
            panelGo.transform.SetParent(transform, false);
            root = (RectTransform)panelGo.transform;
            root.anchorMin = new Vector2(0f, 0f);
            root.anchorMax = new Vector2(0f, 0f);
            root.pivot = new Vector2(0f, 0f);
            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            root.anchoredPosition = new Vector2(10f, KvStatusBar.Height + 24f);
            bg = panelGo.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(KvTheme.PanelBg.r, KvTheme.PanelBg.g, KvTheme.PanelBg.b, 0.88f);
            bg.raycastTarget = false;
            Outline o = panelGo.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            title = KvTheme.CreateText(root, "Title", "", KvTheme.FontSizeSmall - 1,
                TextAnchor.MiddleLeft, KvTheme.TextDim);
            title.rectTransform.anchorMin = new Vector2(0f, 1f);
            title.rectTransform.anchorMax = new Vector2(1f, 1f);
            title.rectTransform.pivot = new Vector2(0f, 1f);
            title.rectTransform.sizeDelta = new Vector2(-10f, 14f);
            title.rectTransform.anchoredPosition = new Vector2(5f, -3f);

            // --- два стика
            leftDot = Stick("Left", 5f, -20f);
            rightDot = Stick("Right", 40f, -20f);

            // --- триггеры (полоски, заполняются по выжатию)
            leftTriggerFill = Trigger("LT", 5f, -52f);
            rightTriggerFill = Trigger("RT", 5f, -64f);

            // --- кнопки-таблетки
            float x = 76f;
            float y = -19f;
            string[,] layout =
            {
                { "Y", "верхняя" }, { "X", "левая" }, { "B", "правая" }, { "A", "нижняя" },
                { "LB", "стоп" }, { "RB", "домой" }, { "Start", "палитра" }, { "Select", "клавиши" },
                { "DUp", "глубже" }, { "DDown", "ближе" }, { "DLeft", "робот" }, { "DRight", "робот" }
            };
            for (int i = 0; i < layout.GetLength(0); i++)
            {
                string key = layout[i, 0];
                string caption = layout[i, 1];
                AddPill(key, key + " · " + caption, x, y);
                x += 78f;
                if (x > PanelWidth - 80f)
                {
                    x = 76f;
                    y -= 16f;
                }
            }

            lastAction = KvTheme.CreateText(root, "Action", "", KvTheme.FontSizeSmall - 1,
                TextAnchor.MiddleLeft, KvTheme.Accent);
            lastAction.rectTransform.anchorMin = new Vector2(0f, 0f);
            lastAction.rectTransform.anchorMax = new Vector2(1f, 0f);
            lastAction.rectTransform.pivot = new Vector2(0f, 0f);
            lastAction.rectTransform.sizeDelta = new Vector2(-10f, 13f);
            lastAction.rectTransform.anchoredPosition = new Vector2(5f, 3f);

            root.sizeDelta = new Vector2(PanelWidth, PanelHeight);
            root.gameObject.SetActive(KvSettings.GamepadHud);

            // Подпись состояния заполняется СРАЗУ (а не только в Update): индикатор не должен
            // показывать пустую строку ни одного кадра — в том числе в первом кадре после сборки.
            RefreshStatus();
        }

        /// <summary>Обновить строку состояния («ГЕЙМПАД: …» / «НЕТ ГЕЙМПАДА»).</summary>
        private void RefreshStatus()
        {
            if (title == null) return;
            KvGamepadRouter router = KvGamepadRouter.Instance;
            bool present = router != null && router.Present;
            title.text = present
                ? KvLoc.T("gamepad.connected", "ГЕЙМПАД") + ": " + router.DeviceName
                : KvLoc.T("gamepad.absent", "НЕТ ГЕЙМПАДА — управление геймпадом не активно");
        }

        private Image Stick(string name, float x, float y)
        {
            GameObject circleGo = new GameObject("Stick_" + name, typeof(Image));
            circleGo.transform.SetParent(root, false);
            Image circle = circleGo.GetComponent<Image>();
            circle.sprite = KvIcons.Get("joint", 26);
            circle.color = KvTheme.TextDisabled;
            circle.raycastTarget = false;
            RectTransform cr = circle.rectTransform;
            cr.anchorMin = cr.anchorMax = new Vector2(0f, 1f);
            cr.pivot = new Vector2(0f, 1f);
            cr.sizeDelta = new Vector2(30f, 30f);
            cr.anchoredPosition = new Vector2(x, y);

            GameObject dotGo = new GameObject("Dot", typeof(Image));
            dotGo.transform.SetParent(circleGo.transform, false);
            Image dot = dotGo.GetComponent<Image>();
            dot.sprite = KvTheme.WhiteSprite;
            dot.color = KvTheme.Accent;
            dot.raycastTarget = false;
            RectTransform dr = dot.rectTransform;
            dr.anchorMin = dr.anchorMax = new Vector2(0.5f, 0.5f);
            dr.pivot = new Vector2(0.5f, 0.5f);
            dr.sizeDelta = new Vector2(8f, 8f);
            dr.anchoredPosition = Vector2.zero;
            return dot;
        }

        private Image Trigger(string label, float x, float y)
        {
            GameObject barGo = new GameObject("Trig_" + label, typeof(Image));
            barGo.transform.SetParent(root, false);
            Image bar = barGo.GetComponent<Image>();
            bar.sprite = KvTheme.WhiteSprite;
            bar.color = KvTheme.ButtonBg;
            bar.raycastTarget = false;
            RectTransform br = bar.rectTransform;
            br.anchorMin = br.anchorMax = new Vector2(0f, 1f);
            br.pivot = new Vector2(0f, 1f);
            br.sizeDelta = new Vector2(66f, 8f);
            br.anchoredPosition = new Vector2(x, y);

            Text t = KvTheme.CreateText(bar.rectTransform, "L", label, KvTheme.FontSizeSmall - 3,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(t.rectTransform, 2f, 2f, 0f, 0f);

            GameObject fillGo = new GameObject("Fill", typeof(Image));
            fillGo.transform.SetParent(barGo.transform, false);
            Image fill = fillGo.GetComponent<Image>();
            fill.sprite = KvTheme.WhiteSprite;
            fill.color = KvTheme.Accent;
            fill.raycastTarget = false;
            RectTransform fr = fill.rectTransform;
            fr.anchorMin = new Vector2(0f, 0f);
            fr.anchorMax = new Vector2(0f, 1f);
            fr.pivot = new Vector2(0f, 0.5f);
            fr.sizeDelta = new Vector2(0f, -2f);
            fr.anchoredPosition = new Vector2(1f, 0f);
            return fill;
        }

        private void AddPill(string key, string caption, float x, float y)
        {
            GameObject go = new GameObject("Pill_" + key, typeof(Image));
            go.transform.SetParent(root, false);
            Image img = go.GetComponent<Image>();
            img.sprite = KvTheme.WhiteSprite;
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = false;
            RectTransform rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(76f, 14f);
            rt.anchoredPosition = new Vector2(x, y);

            Text t = KvTheme.CreateText(rt, "Text", caption, KvTheme.FontSizeSmall - 3,
                TextAnchor.MiddleLeft, KvTheme.TextDisabled);
            KvTheme.Stretch(t.rectTransform, 3f, 1f, 0f, 0f);
            pills[key] = img;
        }

        private void Update()
        {
            if (root == null) return;
            bool enabled = KvSettings.GamepadHud;
            if (root.gameObject.activeSelf != enabled) root.gameObject.SetActive(enabled);
            if (!enabled) return;

            // ТРОТТЛИНГ (оптимизация MR, 17.11.2026). Ниже — сборка строк состояния и цветов:
            // в `Update` она аллоцирует строки (`+ ": "` в каждой подписи) и гонит их через
            // сборщик мусора каждый кадр (Docs/PERFORMANCE_AUDIT.md, §3.1 «KvGamepadHud.Update —
            // 7 аллокаций»). По таймеру те же строки собираются 20 раз в секунду в VR и ещё
            // реже в MR; индикатор от этого не «замерзает» — показываемое состояние меняется
            // медленнее кадра, а не медленнее действия оператора.
            if (!KazistovVvFeatures.KvMrPerformance.Due(Time.unscaledTime, UpdateInterval, ref lastTick))
                return;

            KvGamepadRouter router = KvGamepadRouter.Instance;
            bool present = router != null && router.Present;

            RefreshStatus();

            if (!present)
            {
                if (leftDot != null) leftDot.color = KvTheme.TextDisabled;
                if (rightDot != null) rightDot.color = KvTheme.TextDisabled;
                if (leftTriggerFill != null) leftTriggerFill.rectTransform.sizeDelta = new Vector2(0f, -2f);
                if (rightTriggerFill != null) rightTriggerFill.rectTransform.sizeDelta = new Vector2(0f, -2f);
                foreach (KeyValuePair<string, Image> pair in pills)
                    pair.Value.color = new Color(0f, 0f, 0f, 0f);
                if (lastAction != null) lastAction.text = "";
                return;
            }

            // --- стики: положение точки внутри круга
            if (leftDot != null)
            {
                leftDot.rectTransform.anchoredPosition = router.LeftStick * 10f;
                leftDot.color = router.LeftStick.sqrMagnitude > 0.001f ? KvTheme.Accent : KvTheme.TextDisabled;
            }
            if (rightDot != null)
            {
                rightDot.rectTransform.anchoredPosition = router.RightStick * 10f;
                rightDot.color = router.RightStick.sqrMagnitude > 0.001f ? KvTheme.Accent : KvTheme.TextDisabled;
            }

            // --- триггеры
            if (leftTriggerFill != null)
                leftTriggerFill.rectTransform.sizeDelta = new Vector2(64f * router.LeftTrigger, -2f);
            if (rightTriggerFill != null)
                rightTriggerFill.rectTransform.sizeDelta = new Vector2(64f * router.RightTrigger, -2f);

            // --- кнопки
            foreach (KeyValuePair<string, Image> pair in pills)
            {
                bool on = router.Pressed.Contains(pair.Key);
                pair.Value.color = on
                    ? new Color(KvTheme.Accent.r, KvTheme.Accent.g, KvTheme.Accent.b, 0.55f)
                    : new Color(0f, 0f, 0f, 0f);
            }

            if (lastAction != null)
                lastAction.text = string.IsNullOrEmpty(router.LastAction)
                    ? "" : KvLoc.T("gamepad.last", "последнее") + ": " + router.LastAction;
        }

        /// <summary>Перекрасить под текущую тему.</summary>
        public void Repaint()
        {
            if (bg != null)
                bg.color = new Color(KvTheme.PanelBg.r, KvTheme.PanelBg.g, KvTheme.PanelBg.b, 0.88f);
            if (title != null) title.color = KvTheme.TextDim;
            if (lastAction != null) lastAction.color = KvTheme.Accent;
        }
    }
}
