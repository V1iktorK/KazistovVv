using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// COMMAND PALETTE (ЭТАП 5): быстрый доступ ко ВСЕМ командам через поиск.
    /// Открывается Ctrl+P / Ctrl+Shift+P (и кнопкой Start геймпада, ЭТАП 9),
    /// закрывается Esc; Enter — выполнить, стрелки — навигация.
    ///
    /// Поиск идёт по:
    ///   • подписи на ТЕКУЩЕМ языке и на ВСЕХ остальных (включая английский) —
    ///     через <see cref="KvLoc.AllLanguages"/>;
    ///   • id команды («robot.stop» — удобно для тех, кто знает код);
    ///   • описанию и горячей клавише;
    ///   • названию группы тулбара (ЭТАП 1).
    /// НЕДАВНИЕ команды показываются первыми (пока список пуст), история — в PlayerPrefs.
    /// </summary>
    public class KvCommandPalette : MonoBehaviour
    {
        private const string PrefsRecent = "KazistovVv.UI.Palette.Recent";
        private const float RowHeight = 22f;
        private const float PanelWidth = 560f;
        private const int MaxRows = 12;
        private const int MaxRecent = 8;

        private RectTransform root;
        private RectTransform canvasRect;
        private InputField field;
        private RectTransform list;
        private Image panelBg;
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly List<KvCommand> filtered = new List<KvCommand>();
        private readonly List<GameObject> recentRows = new List<GameObject>();

        private int highlight;
        private bool justOpened;
        private static readonly List<string> recent = new List<string>();

        /// <summary>Палитра открыта (диагностика).</summary>
        public bool IsOpen { get { return root != null && root.gameObject.activeSelf; } }
        /// <summary>Сколько команд в отфильтрованном списке (диагностика).</summary>
        public int ResultCount { get { return filtered.Count; } }
        /// <summary>Строка поиска (диагностика).</summary>
        public string Query { get { return field != null ? field.text : ""; } }
        /// <summary>Индекс подсвеченной строки (диагностика).</summary>
        public int Highlight { get { return highlight; } }
        /// <summary>Подпись подсвеченной команды (диагностика).</summary>
        public string HighlightLabel
        {
            get
            {
                if (highlight < 0 || highlight >= filtered.Count) return "";
                return filtered[highlight].LocalizedTitle;
            }
        }
        /// <summary>Недавние команды (id), самые свежие — первыми.</summary>
        public IReadOnlyList<string> Recent { get { return recent; } }

        /// <summary>Создать палитру на канвасе (один раз на оболочку).</summary>
        public static KvCommandPalette Create(RectTransform canvas)
        {
            GameObject go = new GameObject("KvCommandPalette", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rt = (RectTransform)go.transform;
            KvTheme.Stretch(rt);
            KvCommandPalette palette = go.AddComponent<KvCommandPalette>();
            palette.canvasRect = canvas;
            palette.Build();
            return palette;
        }

        private void Build()
        {
            LoadRecent();

            // --- «блокер»: ловит клик вне палитры и закрывает её
            GameObject blockGo = new GameObject("Blocker", typeof(Image), typeof(Button));
            blockGo.transform.SetParent(transform, false);
            RectTransform blocker = (RectTransform)blockGo.transform;
            KvTheme.Stretch(blocker);
            Image bi = blockGo.GetComponent<Image>();
            bi.sprite = KvTheme.WhiteSprite;
            bi.color = new Color(0f, 0f, 0f, 0.35f);
            bi.raycastTarget = true;
            Button bb = blockGo.GetComponent<Button>();
            bb.transition = Selectable.Transition.None;
            bb.onClick.AddListener(Close);

            // --- сама палитра (сверху по центру, как в редакторах кода)
            GameObject panelGo = new GameObject("Panel", typeof(Image));
            panelGo.transform.SetParent(transform, false);
            root = (RectTransform)panelGo.transform;
            root.anchorMin = new Vector2(0.5f, 1f);
            root.anchorMax = new Vector2(0.5f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(PanelWidth, 120f);
            root.anchoredPosition = new Vector2(0f, -64f);
            panelBg = panelGo.GetComponent<Image>();
            panelBg.sprite = KvTheme.WhiteSprite;
            panelBg.color = KvTheme.PanelBg;
            panelBg.raycastTarget = true;
            Outline o = panelGo.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            // --- строка ввода
            GameObject inputGo = new GameObject("Input", typeof(Image), typeof(InputField));
            inputGo.transform.SetParent(root, false);
            RectTransform ir = (RectTransform)inputGo.transform;
            ir.anchorMin = new Vector2(0f, 1f);
            ir.anchorMax = new Vector2(1f, 1f);
            ir.pivot = new Vector2(0.5f, 1f);
            ir.sizeDelta = new Vector2(-8f, 24f);
            ir.anchoredPosition = new Vector2(0f, -4f);
            Image inputBg = inputGo.GetComponent<Image>();
            inputBg.sprite = KvTheme.WhiteSprite;
            inputBg.color = KvTheme.InputBg;
            inputBg.raycastTarget = true;

            Text placeholder = KvTheme.CreateText(ir, "Placeholder",
                KvLoc.T("palette.hint", "Введите команду… (Enter — выполнить, Esc — закрыть)"),
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextDisabled);
            KvTheme.Stretch(placeholder.rectTransform, 6f, 6f, 2f, 2f);

            Text text = KvTheme.CreateText(ir, "Text", "", KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.TextMain);
            KvTheme.Stretch(text.rectTransform, 6f, 6f, 2f, 2f);
            text.supportRichText = false;

            field = inputGo.GetComponent<InputField>();
            field.targetGraphic = inputBg;
            field.textComponent = text;
            field.placeholder = placeholder;
            field.lineType = InputField.LineType.SingleLine;
            field.caretColor = KvTheme.TextMain;
            field.selectionColor = KvTheme.SelectionBg;
            field.onValueChanged.AddListener(delegate { Refresh(); });
            field.onEndEdit.AddListener(delegate { });

            // --- список результатов
            GameObject listGo = new GameObject("List", typeof(RectTransform));
            listGo.transform.SetParent(root, false);
            list = (RectTransform)listGo.transform;
            list.anchorMin = new Vector2(0f, 0f);
            list.anchorMax = new Vector2(1f, 1f);
            list.offsetMin = new Vector2(4f, 4f);
            list.offsetMax = new Vector2(-4f, -30f);

            root.gameObject.SetActive(false);
            Refresh();
        }

        /// <summary>Открыть палитру с пустой строкой поиска.</summary>
        public void Open()
        {
            if (root == null) return;
            root.gameObject.SetActive(true);
            justOpened = true;
            highlight = 0;
            if (field != null)
            {
                field.text = "";
                field.ActivateInputField();
                field.Select();
            }
            Refresh();
            transform.SetAsLastSibling();
        }

        /// <summary>Закрыть палитру.</summary>
        public void Close()
        {
            if (root == null) return;
            root.gameObject.SetActive(false);
            if (field != null) field.DeactivateInputField();
        }

        /// <summary>Открыть/закрыть.</summary>
        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private void Update()
        {
            if (!IsOpen) return;

            // Пока окно только открылось, первый кадр ввода пропускаем:
            // иначе та же клавиша, которой открыли палитру (P), попадала бы в строку.
            if (justOpened)
            {
                justOpened = false;
                if (field != null) field.text = "";
                return;
            }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }
            if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
            {
                Move(Input.GetKeyDown(KeyCode.DownArrow) ? 1 : -1);
                return;
            }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Execute();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Move(1);
            }
        }

        /// <summary>Передвинуть подсветку по списку.</summary>
        public void Move(int delta)
        {
            if (filtered.Count == 0) return;
            highlight = (highlight + delta) % filtered.Count;
            if (highlight < 0) highlight += filtered.Count;
            PaintHighlight();
        }

        /// <summary>Выполнить подсвеченную команду.</summary>
        public bool Execute()
        {
            if (highlight < 0 || highlight >= filtered.Count) return false;
            KvCommand command = filtered[highlight];
            if (command == null) return false;
            if (!command.Enabled)
            {
                KazistovVvUIManager.SetPlanStatus(
                    KvLoc.T("palette.disabled", "Команда недоступна") + ": " + command.LocalizedTitle,
                    KvTheme.Warn);
                return false;
            }
            PushRecent(command.Id);
            Close();
            KvCommands.Invoke(command.Id);
            return true;
        }

        /// <summary>Выполнить команду по id (диагностика/тесты).</summary>
        public bool ExecuteId(string id)
        {
            for (int i = 0; i < filtered.Count; i++)
            {
                if (filtered[i] == null || filtered[i].Id != id) continue;
                highlight = i;
                return Execute();
            }
            return false;
        }

        /// <summary>Ввести строку поиска программно (диагностика/тесты).</summary>
        public void SetQuery(string query)
        {
            if (field != null) field.text = query ?? "";
            Refresh();
        }

        // ------------------------------------------------------------------ поиск

        private void Refresh()
        {
            if (list == null) return;
            ClearRows();

            string query = field != null ? field.text : "";
            filtered.Clear();

            if (string.IsNullOrEmpty(query))
            {
                // Пустая строка — НЕДАВНИЕ команды первыми (ТЗ ЭТАПА 5), затем остальные.
                HashSet<string> added = new HashSet<string>();
                foreach (string id in recent)
                {
                    KvCommand c = KvCommands.Get(id);
                    if (c == null || !added.Add(id)) continue;
                    filtered.Add(c);
                    if (filtered.Count >= MaxRecent) break;
                }
                foreach (KvCommand c in KvCommands.All)
                {
                    if (c == null || added.Contains(c.Id)) continue;
                    filtered.Add(c);
                }
            }
            else
            {
                // ФИЛЬТРАЦИЯ — ОДНИМ КОДОМ С ШЛЕМОМ (§41). Раньше правило поиска жило здесь, и его
                // пришлось бы повторять в VR: два поиска разошлись бы по составу и порядку выдачи.
                // Теперь правило одно — `KvCommandSearch.Filter`; поведение палитры не изменилось:
                // «начинается с» первыми и по алфавиту, остальные — в порядке реестра.
                filtered.AddRange(KvCommandSearch.Filter(KvCommands.All, query));
            }

            int shown = Mathf.Min(filtered.Count, MaxRows);
            for (int i = 0; i < shown; i++) AddRow(filtered[i], i);

            if (filtered.Count == 0)
            {
                Text empty = KvTheme.CreateText(list, "Empty",
                    KvLoc.T("palette.empty", "Ничего не найдено"),
                    KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.TextDisabled);
                RectTransform er = empty.rectTransform;
                er.anchorMin = new Vector2(0f, 1f);
                er.anchorMax = new Vector2(1f, 1f);
                er.pivot = new Vector2(0f, 1f);
                er.sizeDelta = new Vector2(-8f, RowHeight);
                er.anchoredPosition = new Vector2(6f, -4f);
                rows.Add(empty.gameObject);
            }

            float height = 36f + Mathf.Max(1, Mathf.Min(filtered.Count, MaxRows)) * (RowHeight + 1f) + 8f;
            root.sizeDelta = new Vector2(PanelWidth, height);
            if (highlight >= filtered.Count) highlight = Mathf.Max(0, filtered.Count - 1);
            PaintHighlight();
        }

        // ПРАВИЛО ПОИСКА ЖИВЁТ В `KvCommandSearch` (§41): одна функция на палитру компьютера и на
        // экран «Поиск» в шлеме. Прежние `Matches`/`Starts` отсюда убраны — иначе два поиска
        // разошлись бы, и владелец увидел бы разные результаты в двух местах.

        // ------------------------------------------------------------------ строки

        private void ClearRows()
        {
            foreach (GameObject go in rows)
                if (go != null) Destroy(go);
            rows.Clear();
            recentRows.Clear();
        }

        private void AddRow(KvCommand command, int index)
        {
            bool enabled = command.Enabled;
            GameObject row = new GameObject("Pal_" + command.Id, typeof(Image), typeof(Button));
            row.transform.SetParent(list, false);
            RectTransform rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-4f, RowHeight);
            rt.anchoredPosition = new Vector2(2f, -(index * (RowHeight + 1f) + 2f));

            Image bg = row.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            Button btn = row.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.interactable = enabled;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelBg);
            cb.disabledColor = Color.white;
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            Color textColor = enabled ? KvTheme.TextMain : KvTheme.TextDisabled;

            string iconId = string.IsNullOrEmpty(command.Icon) ? "info" : command.Icon;
            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get(iconId, 14);
            icon.color = enabled ? KvTheme.IconTint : KvTheme.TextDisabled;
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(14f, 14f);
            ir.anchoredPosition = new Vector2(6f, 0f);
            if (icon.sprite == null) iconGo.SetActive(false);

            Text label = KvTheme.CreateText(rt, "Label", command.LocalizedTitle,
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, textColor);
            label.rectTransform.anchorMin = new Vector2(0f, 0f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(26f, 0f);
            label.rectTransform.offsetMax = new Vector2(-176f, 0f);
            label.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Группа тулбара (ЭТАП 1) — маленькая серая подпись справа от названия.
            string group = KvToolbarGroups.GroupTitleOf(command.Id);
            if (!string.IsNullOrEmpty(group))
            {
                Text g = KvTheme.CreateText(rt, "Group", group, KvTheme.FontSizeSmall - 1,
                    TextAnchor.MiddleRight, KvTheme.TextDisabled);
                g.rectTransform.anchorMin = new Vector2(1f, 0f);
                g.rectTransform.anchorMax = new Vector2(1f, 1f);
                g.rectTransform.pivot = new Vector2(1f, 0.5f);
                g.rectTransform.sizeDelta = new Vector2(118f, 0f);
                g.rectTransform.anchoredPosition = new Vector2(-56f, 0f);
            }

            // Горячая клавиша рядом с командой (ТЗ ЭТАПА 5).
            if (!string.IsNullOrEmpty(command.Hotkey))
            {
                Text hk = KvTheme.CreateText(rt, "Hotkey", command.Hotkey, KvTheme.FontSizeSmall - 1,
                    TextAnchor.MiddleRight, KvTheme.Accent);
                hk.rectTransform.anchorMin = new Vector2(1f, 0f);
                hk.rectTransform.anchorMax = new Vector2(1f, 1f);
                hk.rectTransform.pivot = new Vector2(1f, 0.5f);
                hk.rectTransform.sizeDelta = new Vector2(52f, 0f);
                hk.rectTransform.anchoredPosition = new Vector2(-4f, 0f);
            }

            KvCommand captured = command;
            int capturedIndex = index;
            btn.onClick.AddListener(delegate
            {
                highlight = capturedIndex;
                Execute();
            });

            rows.Add(row);
        }

        private void PaintHighlight()
        {
            int i = 0;
            foreach (GameObject go in rows)
            {
                if (go == null) continue;
                Image img = go.GetComponent<Image>();
                if (img == null) { i++; continue; }
                bool on = i == highlight;
                img.color = on
                    ? (KvTheme.IsLight ? KvTheme.SelectionBg : KvTheme.ButtonChecked)
                    : new Color(0f, 0f, 0f, 0f);
                Text label = go.GetComponentInChildren<Text>();
                if (label != null && !on) label.color = KvTheme.TextMain;
                i++;
            }
        }

        // ------------------------------------------------------------------ недавние

        private static void LoadRecent()
        {
            recent.Clear();
            string raw = PlayerPrefs.GetString(PrefsRecent, "");
            if (string.IsNullOrEmpty(raw)) return;
            string[] parts = raw.Split('|');
            foreach (string part in parts)
            {
                if (string.IsNullOrEmpty(part)) continue;
                if (!recent.Contains(part)) recent.Add(part);
                if (recent.Count >= MaxRecent) break;
            }
        }

        private static void PushRecent(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            recent.Remove(id);
            recent.Insert(0, id);
            while (recent.Count > MaxRecent) recent.RemoveAt(recent.Count - 1);
            PlayerPrefs.SetString(PrefsRecent, string.Join("|", recent.ToArray()));
        }
    }
}
