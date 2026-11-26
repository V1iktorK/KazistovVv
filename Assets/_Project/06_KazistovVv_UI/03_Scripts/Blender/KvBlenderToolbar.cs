using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// ТУЛБАР РАСКЛАДКИ BLENDER: 8 групп с разделителями, иконки — НАСТОЯЩИЕ спрайты
    /// владельца из `Assets/Resources/UI/Icons/Toolbar/` (126 штук, имя = id команды с
    /// подчёркиваниями), нажатие = `KvCommands.Invoke(id)` — реальная команда реестра.
    ///
    /// ПОРЯДОК — из <see cref="KvToolbarGroups"/> (данные): THOR первым (robot.model.thor
    /// первый среди моделей), группы идут в порядке каталога и разделяются тонкими линиями.
    ///
    /// РЕЖИМ КРУПНЫХ ИКОНОК («как в оверлее, крупные цели»): переключатель `ui.toolbar.large`
    /// меняет размер цели нажатия 24 → 32 px; выбор переживает перезапуск (PlayerPrefs).
    /// </summary>
    public class KvBlenderToolbar : MonoBehaviour
    {
        /// <summary>Обычный размер иконки/кнопки (px).</summary>
        public const int RegularIconSize = 24;
        /// <summary>Крупный размер иконки/кнопки (px).</summary>
        public const int LargeIconSize = 32;
        /// <summary>Ключ PlayerPrefs режима крупных иконок.</summary>
        public const string LargeIconsPrefKey = "KazistovVv.Blender.LargeIcons";

        private RectTransform root;
        private readonly List<string> buttonIds = new List<string>();
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>();
        private int separatorCount;

        /// <summary>Id кнопок в порядке показа (по группам, как в <see cref="KvToolbarGroups"/>).</summary>
        public IReadOnlyList<string> ButtonIds
        {
            get { return buttonIds; }
        }

        /// <summary>Кнопка команды (null — нет такой).</summary>
        public Button ButtonFor(string id)
        {
            Button b;
            return buttons.TryGetValue(id ?? "", out b) ? b : null;
        }

        /// <summary>Сколько групп показано.</summary>
        public int GroupCount
        {
            get { return KvToolbarGroups.All.Length; }
        }

        /// <summary>Сколько разделителей между группами.</summary>
        public int SeparatorCount
        {
            get { return separatorCount; }
        }

        /// <summary>Режим крупных иконок (с сохранением выбора).</summary>
        public bool LargeIcons
        {
            get { return PlayerPrefs.GetInt(LargeIconsPrefKey, 0) != 0; }
            set
            {
                if (LargeIcons == value) return;
                PlayerPrefs.SetInt(LargeIconsPrefKey, value ? 1 : 0);
                PlayerPrefs.Save();
                Rebuild();
            }
        }

        /// <summary>Текущий размер иконки/кнопки.</summary>
        public int IconSize
        {
            get { return LargeIcons ? LargeIconSize : RegularIconSize; }
        }

        /// <summary>Собрать тулбар (возвращает высоту полосы).</summary>
        public float Build(RectTransform parent, float topY)
        {
            GameObject go = new GameObject("KvBlenderToolbar", typeof(Image));
            go.transform.SetParent(parent, false);
            root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.anchoredPosition = new Vector2(0f, -topY);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvGlassStyle.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = KvGlassStyle.HeaderColor;
            bg.raycastTarget = true;
            KvGlassStyle.Track(bg, KvGlassRole.Header);

            Rebuild();
            return root.sizeDelta.y;
        }

        /// <summary>Пересобрать кнопки (смена режима иконок, обновление списка).</summary>
        public void Rebuild()
        {
            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
                KvGlassStyle.SafeDestroy(root.GetChild(i).gameObject);
            buttonIds.Clear();
            buttons.Clear();
            separatorCount = 0;

            int size = IconSize;
            int pad = 4;
            int buttonSize = size + 6;
            root.sizeDelta = new Vector2(0f, buttonSize + pad * 2);

            float x = pad;
            KvToolbarGroup[] groups = KvToolbarGroups.All;
            for (int g = 0; g < groups.Length; g++)
            {
                if (g > 0)
                {
                    AddSeparator(x, buttonSize, pad);
                    x += 7f;
                }
                string[] ids = groups[g].Commands;
                for (int i = 0; i < ids.Length; i++)
                {
                    AddButton(ids[i], x, buttonSize, size, pad);
                    x += buttonSize + 2f;
                }
            }
        }

        private void AddSeparator(float x, int buttonSize, int pad)
        {
            separatorCount++;
            Image sep = KvTheme.CreatePanel(root, "Sep_" + separatorCount, KvTheme.Separator);
            RectTransform rt = sep.rectTransform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(1f, buttonSize - 4f);
            rt.anchoredPosition = new Vector2(x, 0f);
        }

        private void AddButton(string id, float x, int buttonSize, int iconSize, int pad)
        {
            GameObject go = new GameObject("Tb_" + id, typeof(Image), typeof(Button));
            go.transform.SetParent(root, false);
            RectTransform rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 0.5f);
            rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.sizeDelta = new Vector2(buttonSize, buttonSize);
            rt.anchoredPosition = new Vector2(x, 0f);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvGlassStyle.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            Button btn = go.GetComponent<Button>();
            btn.targetGraphic = bg;
            btn.transition = Selectable.Transition.ColorTint;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelHeader);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelHeader);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            // --- иконка: спрайт владельца из Resources; фолбэк — встроенная `KvIcons`.
            Image icon = KvTheme.CreatePanel(rt, "Icon", KvTheme.IconTint);
            icon.sprite = KvSpriteIcons.Load(id);
            KvCommand command = KvCommands.Get(id);
            if (icon.sprite == null && command != null)
                icon.sprite = KvIcons.Get(string.IsNullOrEmpty(command.Icon) ? "more" : command.Icon, iconSize);
            if (icon.sprite == null) icon.sprite = KvIcons.Get("more", iconSize);
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0.5f, 0.5f);
            ir.pivot = new Vector2(0.5f, 0.5f);
            ir.sizeDelta = new Vector2(iconSize, iconSize);
            ir.anchoredPosition = Vector2.zero;

            if (command != null)
            {
                KvTooltipTarget tip = go.AddComponent<KvTooltipTarget>();
                tip.Set(command.LocalizedTitle, command.TooltipBody, command.Hotkey);
                tip.stub = command.Stub;
            }

            string captured = id;
            btn.onClick.AddListener(delegate { KvCommands.Invoke(captured); });

            buttonIds.Add(id);
            buttons[id] = btn;
        }
    }
}
