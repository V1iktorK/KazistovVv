using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// СТРОКА МЕНЮ КАРКАСА ОКНА (раскладка Blender): Файл / Правка / Вид / Робот /
    /// Сценарии / Сервис / Справка — как просил владелец.
    ///
    /// Пункты НЕ прописаны вручную: они строятся из реестра <see cref="KvCommands"/> по
    /// <see cref="KvCommand.MenuPath"/> существующих команд («привязать к существующим
    /// меню-командам»). Особый случай — «Сценарии»: команды сценариев лежат в путях
    /// «Сервис/Сценарии…», а в новой раскладке у сценариев СВОЁ верхнее меню.
    /// Нажатие пункта → `KvCommands.Invoke(id)` — та же команда, что в классике.
    /// </summary>
    public class KvBlenderMenuStrip : MonoBehaviour
    {
        /// <summary>Высота строки меню (px).</summary>
        public const float Height = 22f;

        /// <summary>Верхние меню в порядке показа.</summary>
        public static readonly string[] Menus =
            { "Файл", "Правка", "Вид", "Робот", "Сценарии", "Сервис", "Справка" };

        private RectTransform root;
        private RectTransform canvasRect;
        private RectTransform dropdown;
        private string openMenu = "";
        private readonly List<GameObject> dropdownRows = new List<GameObject>();
        private readonly Dictionary<string, List<string>> byMenu =
            new Dictionary<string, List<string>>();

        /// <summary>Открыто ли хоть одно меню (диагностика/самопрогон).</summary>
        public bool AnyOpen
        {
            get { return !string.IsNullOrEmpty(openMenu); }
        }

        /// <summary>Заголовки меню.</summary>
        public string[] MenuTitles
        {
            get { return Menus; }
        }

        /// <summary>Id команд меню (порядок — как в реестре).</summary>
        public IReadOnlyList<string> CommandIdsFor(string menuTitle)
        {
            List<string> list;
            return byMenu.TryGetValue(menuTitle ?? "", out list) ? list : new List<string>();
        }

        /// <summary>Собрать строку меню (возвращает высоту).</summary>
        public float Build(RectTransform canvas, float topY)
        {
            canvasRect = canvas;

            GameObject go = new GameObject("KvBlenderMenu", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            root.sizeDelta = new Vector2(0f, Height);
            root.anchoredPosition = new Vector2(0f, -topY);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvGlassStyle.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = KvGlassStyle.FillColor;
            bg.raycastTarget = true;
            KvGlassStyle.Track(bg, KvGlassRole.Fill);

            // --- разложить команды реестра по меню
            foreach (string menu in Menus) byMenu[menu] = new List<string>();
            IReadOnlyList<KvCommand> all = KvCommands.All;
            for (int i = 0; i < all.Count; i++)
            {
                KvCommand c = all[i];
                if (c == null || string.IsNullOrEmpty(c.MenuPath)) continue;
                string bucket = BucketFor(c.MenuPath);
                byMenu[bucket].Add(c.Id);
            }

            // --- кнопки меню
            float x = 6f;
            foreach (string menu in Menus)
            {
                string captured = menu;
                GameObject bgo = new GameObject("Menu_" + menu, typeof(Image), typeof(Button));
                bgo.transform.SetParent(root, false);
                Image bimg = bgo.GetComponent<Image>();
                bimg.sprite = KvGlassStyle.Rounded;
                bimg.type = Image.Type.Sliced;
                bimg.color = new Color(0f, 0f, 0f, 0f);
                bimg.raycastTarget = true;
                RectTransform brt = (RectTransform)bgo.transform;
                brt.anchorMin = new Vector2(0f, 0f);
                brt.anchorMax = new Vector2(0f, 1f);
                brt.pivot = new Vector2(0f, 0.5f);
                float w = 12f + menu.Length * 7.4f;
                brt.sizeDelta = new Vector2(w, -2f);
                brt.anchoredPosition = new Vector2(x, 0f);
                x += w + 2f;

                Text t = KvTheme.CreateText(brt, "Label", KvLoc.Menu(menu), KvTheme.FontSizeSmall,
                    TextAnchor.MiddleCenter, KvTheme.TextMain);
                KvTheme.Stretch(t.rectTransform);

                Button btn = bgo.GetComponent<Button>();
                btn.targetGraphic = bimg;
                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelBg);
                cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelBg);
                cb.fadeDuration = 0.05f;
                btn.colors = cb;
                btn.onClick.AddListener(delegate { ToggleMenu(captured); });
            }

            BuildDropdown(canvas);
            return Height;
        }

        /// <summary>Открыть меню по заголовку (тесты/клавиши).</summary>
        public void ToggleMenu(string menuTitle)
        {
            if (openMenu == menuTitle)
            {
                CloseMenus();
                return;
            }
            OpenMenu(menuTitle);
        }

        public void OpenMenu(string menuTitle)
        {
            CloseMenus();
            if (dropdown == null || !byMenu.ContainsKey(menuTitle)) return;
            openMenu = menuTitle;
            RebuildRows(menuTitle);
            dropdown.gameObject.SetActive(true);
            dropdown.SetAsLastSibling();
        }

        public void CloseMenus()
        {
            openMenu = "";
            if (dropdown != null) dropdown.gameObject.SetActive(false);
        }

        /// <summary>
        /// Куда попадает команда: «Сценарии» собирают все команды сценариев (пути
        /// «Сервис/Сценарии…»), остальные — по первому сегменту `MenuPath`.
        /// </summary>
        public static string BucketFor(string menuPath)
        {
            if (string.IsNullOrEmpty(menuPath)) return "Сервис";
            if (menuPath.Contains("Сценарии")) return "Сценарии";
            string top = menuPath.Split('/')[0];
            foreach (string menu in Menus)
                if (menu == top) return top;
            return "Сервис";
        }

        private void BuildDropdown(RectTransform canvas)
        {
            GameObject go = new GameObject("KvBlenderMenuList", typeof(Image));
            go.transform.SetParent(canvas, false);
            dropdown = (RectTransform)go.transform;
            dropdown.anchorMin = dropdown.anchorMax = new Vector2(0f, 1f);
            dropdown.pivot = new Vector2(0f, 1f);
            dropdown.sizeDelta = new Vector2(340f, 0f);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvGlassStyle.Rounded;
            bg.type = Image.Type.Sliced;
            bg.color = KvGlassStyle.FillColor;
            bg.raycastTarget = true;
            KvGlassStyle.Track(bg, KvGlassRole.Fill);

            dropdown.gameObject.SetActive(false);
        }

        private void RebuildRows(string menuTitle)
        {
            foreach (GameObject go in dropdownRows)
                if (go != null) KvGlassStyle.SafeDestroy(go);
            dropdownRows.Clear();

            List<string> ids = byMenu[menuTitle];
            float y = 4f;
            float rowH = 22f;
            for (int i = 0; i < ids.Count; i++)
            {
                KvCommand command = KvCommands.Get(ids[i]);
                if (command == null) continue;

                GameObject row = new GameObject("MM_" + command.Id, typeof(Image), typeof(Button));
                row.transform.SetParent(dropdown, false);
                RectTransform rt = (RectTransform)row.transform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.sizeDelta = new Vector2(-8f, rowH);
                rt.anchoredPosition = new Vector2(4f, -y);
                y += rowH + 1f;

                Image bg = row.GetComponent<Image>();
                bg.sprite = KvGlassStyle.Rounded;
                bg.type = Image.Type.Sliced;
                bg.color = new Color(0f, 0f, 0f, 0f);
                bg.raycastTarget = true;

                Text label = KvTheme.CreateText(rt, "Label", command.LocalizedTitle,
                    KvTheme.FontSizeSmall, TextAnchor.MiddleLeft,
                    command.Enabled ? KvTheme.TextMain : KvTheme.TextDisabled);
                label.rectTransform.anchorMin = new Vector2(0f, 0f);
                label.rectTransform.anchorMax = new Vector2(1f, 1f);
                label.rectTransform.offsetMin = new Vector2(8f, 0f);
                label.rectTransform.offsetMax = new Vector2(-8f, 0f);

                Button btn = row.GetComponent<Button>();
                btn.targetGraphic = bg;
                btn.transition = Selectable.Transition.ColorTint;
                ColorBlock cb = btn.colors;
                cb.normalColor = Color.white;
                cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelBg);
                cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelBg);
                cb.fadeDuration = 0.05f;
                btn.colors = cb;

                string captured = command.Id;
                btn.onClick.AddListener(delegate
                {
                    CloseMenus();
                    KvCommands.Invoke(captured);
                });

                dropdownRows.Add(row);
            }

            dropdown.sizeDelta = new Vector2(340f, y + 4f);
        }
    }
}
