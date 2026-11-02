using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// ЛОГИЧЕСКАЯ ГРУППА тулбара (ЭТАП 1): «Робот», «Точка и траектория»,
    /// «Постобработка», «Визуализация», «Запись и экспорт», «Сеть и автоматизация»,
    /// «Настройки». Кнопки внутри группы идут ПОДРЯД и отделяются от соседней группы
    /// тонкой вертикальной линией; у каждой группы есть кнопка-иконка группы, по клику
    /// открывающая выпадающий список её команд (с горячими клавишами) и позволяющая
    /// свернуть группу в одну иконку.
    /// </summary>
    public class KvToolbarGroup
    {
        /// <summary>Id группы («robot», «path», …) — он же ключ в PlayerPrefs.</summary>
        public string Id;
        /// <summary>Название группы для подсказки и меню.</summary>
        public string Title;
        /// <summary>Иконка группы (id из <see cref="KvIcons"/>).</summary>
        public string Icon;
        /// <summary>Id команд группы в порядке показа.</summary>
        public string[] Commands;

        public KvToolbarGroup(string id, string title, string icon, params string[] commands)
        {
            Id = id;
            Title = title;
            Icon = icon;
            Commands = commands ?? new string[0];
        }
    }

    /// <summary>Каталог групп тулбара (ЭТАП 1). Группы — ДАННЫЕ: правка панели не нужна.</summary>
    public static class KvToolbarGroups
    {
        private static KvToolbarGroup[] all;

        /// <summary>Все группы в порядке показа.</summary>
        public static KvToolbarGroup[] All
        {
            get
            {
                if (all == null) all = Build();
                return all;
            }
        }

        /// <summary>
        /// РАСКЛАДКА ВСЕХ КОМАНД ПО ГРУППАМ (переписано 02.11.2026, задача части 1).
        ///
        /// СЛОВА ВЛАДЕЛЬЦА: «добавить весь функционал к кнопкам на меню левой руки».
        ///
        /// ЧТО БЫЛО. В 8 группах лежало 78 идентификаторов, из них 15 ОТСУТСТВОВАЛИ в реестре
        /// команд (мёртвые: valid.run, forces.tab, lab.tree, lab.run, collision.toggle,
        /// collision.tab, report.make, report.pdf, net.tab, web.toggle, mobile.toggle,
        /// script.tab, bt.tab, cameras.tab, show.trajectories, fail.tab), а 48 РЕАЛЬНЫХ команд
        /// компьютера не попадали в шлем вообще. Итог: из 110 команд шлем видел 63.
        ///
        /// ЧТО СТАЛО. Все 110 зарегистрированных команд разложены по 8 группам. Проверяется
        /// тестом KvXrMenuCoverageTests: каждая команда реестра обязана быть в меню.
        ///
        /// КАК РАСКЛАДЫВАЛИ. Группы шлема — это не копия меню компьютера: у владельца в шлеме
        /// своя логика («Постобработка», «Визуализация»), и она сохранена. Команды, которых
        /// не хватало, добавлены в конец «своей» группы по смыслу: постобработка → «Постобработка»,
        /// зоны и измерение → «Визуализация», файлы → «Запись и экспорт», пульт и геймпад → «Сеть».
        /// </summary>
        private static KvToolbarGroup[] Build()
        {
            return new[]
            {
                new KvToolbarGroup("robot", "Робот", "robot",
                    "robot.switch", "robot.playpause", "robot.stop", "estop",
                    "edit.reset", "joints.panel", "pose.save", "pose.goto",
                    "gripper.toggle",

                    // ЧАСТЬ 3 (02.11.2026): МОДЕЛИ РОБОТОВ в конце группы. Владелец:
                    // «роботов всех перенеси», вариант «выбор из списка — один активный».
                    // Эти команды регистрируются ДИНАМИЧЕСКИ в KvRobotCommands (по одной
                    // на модель каталога), поэтому статический парсинг реестра их не видит —
                    // тест-сторож знает про префикс robot.model. и не считает их «мёртвыми».
                    "robot.model.stand_6axis", "robot.model.scara_ls10b702s",
                    "robot.model.ur5", "robot.model.kuka_iiwa7",
                    "robot.model.kuka_iiwa14", "robot.model.abb_irb6640",
                    "robot.model.fanuc_m20ib", "robot.model.franka_fr3",
                    "robot.model.franka_fr3_v2", "robot.model.kinova_gen3",
                    "robot.model.comau_edo", "robot.model.openarm"),

                new KvToolbarGroup("path", "Точка и траектория", "point",
                    "point.select", "path.select", "waypoint.add", "waypoint.move",
                    "waypoint.delete", "waypoint.build", "waypoint.play", "compare.mark",
                    "compare.open", "graph.open", "waypoint.earlier", "waypoint.later",
                    "waypoint.clear"),

                new KvToolbarGroup("post", "Постобработка", "graph",
                    "post.smooth", "post.timeoptimal", "post.energy", "post.auto",
                    "workbench.toggle", "constr.tab", "constr.toggle", "wp.tab",
                    "post.apply.smooth", "post.apply.topt", "post.apply.energy", "post.auto.selected",
                    "wp.orient", "wp.speed", "wp.detour", "wp.pause",
                    "wp.clear"),

                new KvToolbarGroup("view", "Визуализация", "workspace",
                    "view.workspace", "view.limits", "view.metrics", "view.singularity",
                    "view.heatmap", "view.clearance", "health.toggle", "obstacle.toggle",
                    "zone.box", "zone.sphere", "zone.cylinder", "zone.discard",
                    "tool.measure", "tool.measure.reset", "tool.grid", "tool.grid.clear",
                    "obstacle.discard", "tool.flashlight", "view.panel.tree", "view.panel.properties",
                    "view.panel.settings", "ui.toggle"),

                new KvToolbarGroup("export", "Запись и экспорт", "record",
                    "record.toggle", "record.play", "shot.screenshot", "video.toggle",
                    "export.robot", "import.tab", "session.save", "session.load",
                    "record.speed", "log.export", "session.panel", "file.export",
                    "shot.annotate", "export.lang", "export.tab", "import.scan",
                    "file.import"),

                new KvToolbarGroup("net", "Сеть и автоматизация", "lan",
                    "scenarios.open", "scenarios.run", "pendant.toggle", "pendant.variant",
                    "pendant.attach", "pendant.jogmode", "gamepad.hud"),

                new KvToolbarGroup("ui", "Настройки", "settings",
                    "view.theme", "lang.cycle", "ui.settings", "ui.palette",
                    "help.hotkeys", "ui.resetlayout", "features.open", "startmenu.show",
                    "tut.toggle", "help.open", "tut.restart", "help.about",
                    "ui.access"),

                new KvToolbarGroup("misc", "Прочее", "more",
                    "eta.toggle", "presentation.toggle", "log.open", "demo.quick",
                    "obstacle.evaluate", "zone.panel", "perf.open", "demo.pickplace",
                    "edit.undo", "edit.redo", "calib.tab", "payload.tab"),

            };
        }

        /// <summary>Группа по id (null — нет такой).</summary>
        public static KvToolbarGroup Find(string id)
        {
            foreach (KvToolbarGroup g in All)
                if (string.Equals(g.Id, id, StringComparison.Ordinal)) return g;
            return null;
        }

        /// <summary>Индекс группы, которой принадлежит команда (-1 — не разложена).</summary>
        public static int GroupOf(string commandId)
        {
            if (string.IsNullOrEmpty(commandId)) return -1;
            KvToolbarGroup[] groups = All;
            for (int i = 0; i < groups.Length; i++)
            {
                if (string.Equals(groups[i].Id, "misc", StringComparison.Ordinal)) continue;
                foreach (string id in groups[i].Commands)
                    if (string.Equals(id, commandId, StringComparison.Ordinal)) return i;
            }
            // Не разложена — уходит в «Прочее» (последняя группа): ни одна команда не теряется.
            return groups.Length - 1;
        }

        /// <summary>Название группы, которой принадлежит команда (для палитры/подсказок).</summary>
        public static string GroupTitleOf(string commandId)
        {
            int index = GroupOf(commandId);
            return index >= 0 ? All[index].Title : "";
        }

        /// <summary>
        /// Разложить «плоский» список id по группам, СОХРАНЯЯ порядок групп и отбрасывая
        /// неизвестные/несуществующие команды (это защищает тулбар от опечатки в списке).
        /// </summary>
        public static List<KeyValuePair<KvToolbarGroup, List<string>>> Arrange(
            IReadOnlyList<string> commandIds)
        {
            List<KeyValuePair<KvToolbarGroup, List<string>>> result =
                new List<KeyValuePair<KvToolbarGroup, List<string>>>();
            KvToolbarGroup[] groups = All;
            for (int i = 0; i < groups.Length; i++)
                result.Add(new KeyValuePair<KvToolbarGroup, List<string>>(groups[i],
                    new List<string>()));

            if (commandIds != null)
            {
                HashSet<string> used = new HashSet<string>();
                foreach (string id in commandIds)
                {
                    if (string.IsNullOrEmpty(id) || used.Contains(id)) continue;
                    if (KvCommands.Get(id) == null) continue;      // команды нет — кнопки тоже
                    used.Add(id);
                    int index = GroupOf(id);
                    if (index < 0 || index >= result.Count) continue;
                    result[index].Value.Add(id);
                }
            }

            // Пустые группы не показываем (например, «Прочее» на «чистой» раскладке).
            List<KeyValuePair<KvToolbarGroup, List<string>>> nonEmpty =
                new List<KeyValuePair<KvToolbarGroup, List<string>>>();
            foreach (KeyValuePair<KvToolbarGroup, List<string>> pair in result)
                if (pair.Value.Count > 0) nonEmpty.Add(pair);
            return nonEmpty;
        }
    }

    /// <summary>
    /// Выпадающий список КОМАНД ГРУППЫ (ЭТАП 1): открывается кнопкой-иконкой группы.
    /// Показывает все команды группы с названием и горячей клавишей, а также действие
    /// «свернуть/развернуть группу» (группа в одну иконку). Сделан локально по образцу
    /// `KvMenuBar`/`KvToolbarMoreMenu`: нет готового выпадающего списка в проекте.
    /// </summary>
    internal class KvToolbarGroupMenu : MonoBehaviour
    {
        private const float RowHeight = 22f;
        private const float PanelWidth = 320f;
        private const int RowIconSize = 16;

        private RectTransform canvasRect;
        private KvToolbar owner;
        private RectTransform panel;
        private RectTransform blocker;
        private readonly List<GameObject> rows = new List<GameObject>();

        /// <summary>Меню открыто (диагностика).</summary>
        public bool IsOpen { get { return panel != null && panel.gameObject.activeSelf; } }
        /// <summary>Сколько строк построено (диагностика).</summary>
        public int RowCount { get { return rows.Count; } }

        public void Ensure(RectTransform canvas, KvToolbar toolbar)
        {
            owner = toolbar;
            if (panel != null && canvasRect == canvas) return;
            canvasRect = canvas;
            BuildBlocker();
            BuildPanel();
        }

        /// <summary>Открыть/закрыть список группы под её кнопкой.</summary>
        public void Toggle(RectTransform anchor, KvToolbarGroup group, IReadOnlyList<string> ids)
        {
            if (IsOpen)
            {
                Close();
                return;
            }
            Open(anchor, group, ids);
        }

        public void Close()
        {
            if (panel != null) panel.gameObject.SetActive(false);
            if (blocker != null) blocker.gameObject.SetActive(false);
        }

        private void Open(RectTransform anchor, KvToolbarGroup group, IReadOnlyList<string> ids)
        {
            if (panel == null || anchor == null || group == null) return;
            RebuildRows(group, ids);
            Position(anchor);
            if (blocker != null)
            {
                blocker.gameObject.SetActive(true);
                blocker.SetAsLastSibling();
            }
            panel.gameObject.SetActive(true);
            panel.SetAsLastSibling();
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void BuildBlocker()
        {
            GameObject go = new GameObject("KvGroupMenuBlocker", typeof(Image), typeof(Button));
            go.transform.SetParent(transform, false);
            blocker = (RectTransform)go.transform;
            KvTheme.Stretch(blocker);
            Image img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            img.raycastTarget = true;
            Button btn = go.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);
            blocker.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            GameObject go = new GameObject("KvGroupMenu", typeof(Image));
            go.transform.SetParent(transform, false);
            panel = (RectTransform)go.transform;
            panel.anchorMin = panel.anchorMax = new Vector2(0f, 1f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(PanelWidth, 0f);

            Image bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelBg;
            bg.raycastTarget = true;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Border;
            o.effectDistance = new Vector2(1f, -1f);
            o.useGraphicAlpha = false;

            panel.gameObject.SetActive(false);
        }

        private void RebuildRows(KvToolbarGroup group, IReadOnlyList<string> ids)
        {
            foreach (GameObject go in rows)
                if (go != null) Destroy(go);
            rows.Clear();

            float y = 3f;

            // --- заголовок группы
            Text title = KvTheme.CreateText(panel, "GroupTitle", group.Title.ToUpperInvariant(),
                KvTheme.FontSizeSmall, TextAnchor.MiddleLeft, KvTheme.Accent);
            RectTransform tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0f, 1f);
            tr.sizeDelta = new Vector2(-8f, 18f);
            tr.anchoredPosition = new Vector2(6f, -y);
            rows.Add(title.gameObject);
            y += 19f;

            if (ids != null)
                for (int i = 0; i < ids.Count; i++) y = AddRow(ids[i], y);

            // --- действие «свернуть/развернуть группу»
            Image sep = KvTheme.CreatePanel(panel, "Sep", KvTheme.Separator);
            sep.rectTransform.anchorMin = new Vector2(0f, 1f);
            sep.rectTransform.anchorMax = new Vector2(1f, 1f);
            sep.rectTransform.pivot = new Vector2(0f, 1f);
            sep.rectTransform.sizeDelta = new Vector2(-10f, 1f);
            sep.rectTransform.anchoredPosition = new Vector2(5f, -(y + 2f));
            rows.Add(sep.gameObject);
            y += 6f;

            bool collapsed = owner != null && owner.IsGroupCollapsed(group.Id);
            y = AddActionRow(collapsed ? "Развернуть группу" : "Свернуть группу в иконку",
                collapsed ? "expand" : "collapse", y, delegate
                {
                    Close();
                    if (owner != null) owner.SetGroupCollapsed(group.Id, !collapsed);
                });

            panel.sizeDelta = new Vector2(PanelWidth, y + 3f);
        }

        private float AddRow(string id, float y)
        {
            KvCommand command = KvCommands.Get(id);
            if (command == null) return y;

            string label = command.LocalizedTitle;
            string iconId = string.IsNullOrEmpty(command.Icon) ? "info" : command.Icon;
            bool enabled = command.Enabled;
            Color textColor = enabled ? KvTheme.TextMain : KvTheme.TextDisabled;

            GameObject row = new GameObject("GM_" + id, typeof(Image), typeof(Button));
            row.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-6f, RowHeight);
            rt.anchoredPosition = new Vector2(3f, -y);

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

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get(iconId, RowIconSize);
            icon.color = enabled ? KvTheme.IconTint : KvTheme.TextDisabled;
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(RowIconSize, RowIconSize);
            ir.anchoredPosition = new Vector2(6f, 0f);
            if (icon.sprite == null) iconGo.SetActive(false);

            Text text = KvTheme.CreateText(rt, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, textColor);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(28f, 0f);
            text.rectTransform.offsetMax = new Vector2(-64f, 0f);

            if (!string.IsNullOrEmpty(command.Hotkey))
            {
                Text hk = KvTheme.CreateText(rt, "Hotkey", command.Hotkey, KvTheme.FontSizeSmall - 1,
                    TextAnchor.MiddleRight, KvTheme.TextDim);
                hk.rectTransform.anchorMin = new Vector2(1f, 0f);
                hk.rectTransform.anchorMax = new Vector2(1f, 1f);
                hk.rectTransform.pivot = new Vector2(1f, 0.5f);
                hk.rectTransform.sizeDelta = new Vector2(58f, 0f);
                hk.rectTransform.anchoredPosition = new Vector2(-5f, 0f);
            }

            if (command.Stub)
            {
                KvTooltipTarget tip = row.AddComponent<KvTooltipTarget>();
                tip.Set(command.LocalizedTitle, command.TooltipBody, command.Hotkey);
                tip.stub = true;
            }
            else
            {
                KvTooltipTarget tip = row.AddComponent<KvTooltipTarget>();
                tip.Set(command.LocalizedTitle, command.TooltipBody, command.Hotkey);
            }

            btn.onClick.AddListener(delegate
            {
                Close();
                KvCommands.Invoke(id);
                if (owner != null) owner.Refresh();
            });

            rows.Add(row);
            return y + RowHeight + 1f;
        }

        private float AddActionRow(string label, string iconId, float y, Action action)
        {
            GameObject row = new GameObject("GM_Action", typeof(Image), typeof(Button));
            row.transform.SetParent(panel, false);
            RectTransform rt = (RectTransform)row.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(-6f, RowHeight);
            rt.anchoredPosition = new Vector2(3f, -y);

            Image bg = row.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = new Color(0f, 0f, 0f, 0f);
            bg.raycastTarget = true;

            Button btn = row.GetComponent<Button>();
            btn.targetGraphic = bg;
            ColorBlock cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = KvTheme.Ratio(KvTheme.ButtonHover, KvTheme.PanelBg);
            cb.pressedColor = KvTheme.Ratio(KvTheme.ButtonPressed, KvTheme.PanelBg);
            cb.fadeDuration = 0.05f;
            btn.colors = cb;

            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(row.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get(iconId, RowIconSize);
            icon.color = KvTheme.TextDim;
            icon.raycastTarget = false;
            RectTransform ir = icon.rectTransform;
            ir.anchorMin = ir.anchorMax = new Vector2(0f, 0.5f);
            ir.pivot = new Vector2(0f, 0.5f);
            ir.sizeDelta = new Vector2(RowIconSize, RowIconSize);
            ir.anchoredPosition = new Vector2(6f, 0f);
            if (icon.sprite == null) iconGo.SetActive(false);

            Text text = KvTheme.CreateText(rt, "Label", label, KvTheme.FontSizeSmall,
                TextAnchor.MiddleLeft, KvTheme.Accent);
            text.rectTransform.anchorMin = new Vector2(0f, 0f);
            text.rectTransform.anchorMax = new Vector2(1f, 1f);
            text.rectTransform.offsetMin = new Vector2(28f, 0f);
            text.rectTransform.offsetMax = new Vector2(-6f, 0f);

            if (action != null) btn.onClick.AddListener(delegate { action(); });
            rows.Add(row);
            return y + RowHeight + 1f;
        }

        private void Position(RectTransform anchor)
        {
            Vector3 world = anchor.TransformPoint(new Vector3(anchor.rect.xMin, anchor.rect.yMin, 0f));
            Vector3 local = canvasRect.InverseTransformPoint(world);
            float x = local.x - canvasRect.rect.xMin + 2f;
            float y = local.y - canvasRect.rect.yMax - 4f;

            // Не выпускаем список за правый/левый край канваса.
            float maxX = canvasRect.rect.width - PanelWidth - 4f;
            if (x > maxX) x = Mathf.Max(4f, maxX);
            if (x < 4f) x = 4f;
            panel.anchoredPosition = new Vector2(x, y);
        }
    }
}
