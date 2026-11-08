using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Верхняя панель инструментов в стиле FreeCAD: ПЛОТНЫЙ блок кнопок-иконок БЕЗ подписей,
    /// разложенный ПО ЛОГИЧЕСКИМ ГРУППАМ (ЭТАП 1).
    ///
    /// Что изменилось относительно «плоской сетки»:
    ///   • кнопки идут не одним потоком, а группами («Робот», «Точка и траектория»,
    ///     «Постобработка», «Визуализация», «Запись и экспорт», «Сеть и автоматизация»,
    ///     «Настройки», «Прочее») — между группами тонкая вертикальная линия;
    ///   • у каждой группы слева есть КНОПКА-ИКОНКА группы: клик открывает выпадающий
    ///     список её команд (название + горячая клавиша) и позволяет СВЕРНУТЬ группу
    ///     в одну иконку (состояние хранится в PlayerPrefs);
    ///   • раскладка считается ВРУЧНУЮ: группы не разрываются между строками, а при
    ///     нехватке ширины переносятся целиком на следующую полосу;
    ///   • у каждой кнопки подсказка = название + описание + горячая клавиша.
    ///
    /// Кнопки берутся из реестра команд (<see cref="KvCommands"/>) по id, поэтому новая
    /// кнопка добавляется регистрацией команды и её id в группе (<see cref="KvToolbarGroups"/>).
    /// Высота панели (<see cref="Height"/>) считается здесь и используется менеджером UI
    /// в `LayoutDock`, поэтому раскладка dock-панелей подстраивается под тулбар автоматически.
    /// </summary>
    public class KvToolbar : MonoBehaviour
    {
        /// <summary>Минимум колонок в одной группе (историческое «5 в ряд» — как минимум для панели).</summary>
        public const int Columns = 5;

        /// <summary>Сторона кнопки-иконки в компактном режиме (ФИКС 9: 20 px вместо 24/28).</summary>
        public const float CompactIconSize = 20f;

        /// <summary>Ширина правого блока «Верстак» — по ней считается место под кнопки.</summary>
        public const float RightBlockWidth = 336f;

        /// <summary>Зазор между сеткой кнопок и правым блоком (px канваса).</summary>
        public const float RightGap = 16f;

        /// <summary>Ширина «ручки» группы (кнопка-иконка группы) — ЭТАП 1.</summary>
        public const float GroupHandleWidth = 15f;

        /// <summary>Расстояние между группами; линия-разделитель стоит по его центру.</summary>
        public const float GroupGap = 8f;

        /// <summary>Зазор между полосами групп при переносе (px).</summary>
        public const float LineGap = 3f;

        /// <summary>
        /// ТЕКСТЫ ПОДСКАЗОК, ПОСЧИТАННЫЕ ОДИН РАЗ ПРИ СБОРКЕ КНОПОК (P2-10). Стоят в том же
        /// порядке, что `buttons`/`ids`. Раньше текст склеивался заново десять раз в секунду
        /// в `Refresh()` — см. шапку у `Refresh`.
        /// </summary>
        private readonly List<string> tooltipBodies = new List<string>();

        /// <summary>
        /// ФИКС 9: редко используемые команды. Они ДУБЛИРУЮТСЯ пунктами меню, поэтому в
        /// компактном режиме не занимают место (в KvCommands/меню ничего не удаляется).
        /// </summary>
        public static readonly string[] RareCommandIds =
        {
            "help.open", "edit.undo", "edit.redo", "view.theme", "ui.settings",
            "startmenu.show", "tut.toggle", "demo.quick", "report.make", "cameras.tab"
        };

        // --- режимы раскладки (значения совпадают с KvSettings.ToolbarLayout)
        private const int LayoutAuto = 0;      // группы считаются от ширины канваса
        private const int LayoutWide = 1;      // занять всю доступную ширину
        private const int LayoutFive = 2;      // «5 в ряд» — исторический вид (без групп)

        /// <summary>Высота панели (px канваса) — считается от числа полос групп.</summary>
        public float Height { get; private set; }

        private RectTransform root;
        private RectTransform canvasRect;
        private RectTransform gridRect;
        private RectTransform rightBlock;
        private readonly List<KvIconButton> buttons = new List<KvIconButton>();
        private readonly List<string> ids = new List<string>();
        private readonly List<KvIconButton> groupHandles = new List<KvIconButton>();
        private readonly List<string> groupHandleIds = new List<string>();
        private KvSegmented workbench;
        private Text workbenchLabel;
        private Image bg;
        private Image rightBg;
        private KvToolbarGroupMenu groupMenu;
        private string[] sourceIds = new string[0];
        private int layoutMode = LayoutAuto;
        private int maxRows = 4;
        private bool compact;
        private int columns = Columns;
        private float cell = 24f;
        private float spacing = 2f;
        private float pad = 4f;

        /// <summary>
        /// Собрать тулбар. Возвращает его высоту.
        /// <paramref name="layout"/> — режим раскладки (0 авто, 1 широко, 2 «5 в ряд»),
        /// <paramref name="maxRows"/> — максимум рядов ВНУТРИ одной группы (по умолчанию 4),
        /// <paramref name="compact"/> — компактный режим (мелкие иконки, без «редких» кнопок).
        /// </summary>
        public float Build(RectTransform canvas, Canvas owner, string[] commandIds,
            Action<int> onWorkbenchChanged, string[] workbenchNames,
            int layout = LayoutAuto, int maxRows = 4, bool compact = false)
        {
            canvasRect = canvas;
            sourceIds = commandIds ?? new string[0];
            layoutMode = layout;
            this.maxRows = Mathf.Max(1, maxRows);
            this.compact = compact;
            cell = compact ? CompactIconSize : KvSettings.IconButtonSize;
            pad = 4f;
            spacing = 2f;

            GameObject go = new GameObject("KvToolbar", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            root.anchorMin = new Vector2(0f, 1f);
            root.anchorMax = new Vector2(1f, 1f);
            root.pivot = new Vector2(0.5f, 1f);
            bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelBg;
            bg.raycastTarget = true;

            // --- блок кнопок (ручная раскладка по группам, выравнивание влево)
            GameObject gridGo = new GameObject("Groups", typeof(RectTransform));
            gridGo.transform.SetParent(go.transform, false);
            gridRect = (RectTransform)gridGo.transform;
            gridRect.anchorMin = new Vector2(0f, 0.5f);
            gridRect.anchorMax = new Vector2(0f, 0.5f);
            gridRect.pivot = new Vector2(0f, 0.5f);
            gridRect.anchoredPosition = new Vector2(pad, 0f);

            // --- «верстак» справа (переключатель робота)
            GameObject rightGo = new GameObject("Workbench", typeof(Image));
            rightGo.transform.SetParent(go.transform, false);
            rightBlock = (RectTransform)rightGo.transform;
            rightBlock.anchorMin = new Vector2(1f, 0.5f);
            rightBlock.anchorMax = new Vector2(1f, 0.5f);
            rightBlock.pivot = new Vector2(1f, 0.5f);
            rightBlock.sizeDelta = new Vector2(RightBlockWidth, cell + 4f);
            rightBlock.anchoredPosition = new Vector2(-pad, 0f);
            rightBg = rightGo.GetComponent<Image>();
            rightBg.sprite = KvTheme.WhiteSprite;
            rightBg.color = new Color(0f, 0f, 0f, 0f);
            rightBg.raycastTarget = false;

            RectTransform row = KvWidgets.CreateRow(rightBlock, "WbRow", cell + 4f, 6f,
                TextAnchor.MiddleRight);
            KvTheme.Stretch(row, 0f, 0f, 2f, 2f);

            workbenchLabel = KvTheme.CreateText(row, "WbLabel", KvLoc.T("toolbar.workbench", "Верстак:"),
                KvTheme.FontSizeSmall, TextAnchor.MiddleRight, KvTheme.TextDim);
            KvWidgets.Fit(workbenchLabel.gameObject, 62f, cell);

            string[] names = workbenchNames != null && workbenchNames.Length >= 2
                ? workbenchNames : new[] { "Робот", "SCARA" };
            KvSegmented seg = KvWidgets.Segmented(row, "Workbench", names, 0,
                delegate (int i) { onWorkbenchChanged?.Invoke(i); }, cell);
            LayoutElement le = seg.GetComponent<LayoutElement>();
            if (le == null) le = seg.gameObject.AddComponent<LayoutElement>();
            le.minWidth = 236f;
            le.preferredWidth = 236f;
            le.minHeight = cell;
            workbench = seg;

            RebuildGrid();
            return Height;
        }

        // ------------------------------------------------------------------ раскладка групп

        /// <summary>
        /// Пересобрать панель: разложить команды по группам, посчитать геометрию,
        /// создать кнопки и «ручки» групп. Вызывается при сборке, смене раскладки,
        /// сворачивании группы и пересборке оболочки.
        ///
        /// Раскладка — ДВА ПРОХОДА: сначала считаются позиции всех групп (и переносы
        /// полос), затем рисуются кнопки и разделители — так вертикальная линия между
        /// группами знает ВЫСОТУ ВСЕЙ полосы, а не только своей группы.
        /// </summary>
        private void RebuildGrid()
        {
            ClearButtons();

            // Компактный режим: «редкие» команды (дублируются меню) в панель не попадают.
            List<string> visible = new List<string>();
            foreach (string id in sourceIds)
            {
                if (string.IsNullOrEmpty(id)) continue;
                if (compact && IsRare(id)) continue;
                visible.Add(id);
            }

            // Режим «5 в ряд» — исторический вид БЕЗ группировки (одна группа «Тулбар»):
            // раскладка идёт обычной сеткой, как раньше. Так сохраняется прежнее поведение
            // для тех, кто выбрал этот режим в настройках.
            List<KeyValuePair<KvToolbarGroup, List<string>>> groups =
                layoutMode == LayoutFive
                    ? FlatGroup(visible)
                    : KvToolbarGroups.Arrange(visible);

            float avail = Mathf.Max(cell * 5f, CanvasWidth() - RightBlockWidth - RightGap - pad * 2f);
            columns = Columns;

            // ---------- проход 1: позиции групп
            List<GroupPlacement> placements = new List<GroupPlacement>();
            float cursorX = 0f, cursorY = 0f, lineHeight = 0f, totalW = 0f;
            int line = 0;
            float lineStartX = 0f;

            for (int g = 0; g < groups.Count; g++)
            {
                KvToolbarGroup group = groups[g].Key;
                List<string> groupIds = groups[g].Value;
                if (groupIds.Count == 0) continue;

                bool collapsed = IsGroupCollapsed(group.Id);
                int rows = collapsed ? 1 : Mathf.Min(maxRows, groupIds.Count);
                int cols = collapsed ? 1 : Mathf.CeilToInt(groupIds.Count / (float)rows);
                // Режим «5 в ряд» — историческая сетка ровно по 5 кнопок в строке
                // (ряды при этом не ограничиваются, как и было до ЭТАПА 1).
                if (layoutMode == LayoutFive && !collapsed)
                {
                    cols = Columns;
                    rows = Mathf.CeilToInt(groupIds.Count / (float)Columns);
                }
                if (cols > columns) columns = cols;

                float innerW = cols * cell + (cols - 1) * spacing;
                float blockW = GroupHandleWidth + 2f + innerW;
                float blockH = rows * cell + (rows - 1) * spacing;

                // Перенос полосы: группа целиком уходит на следующую строку.
                if (cursorX > 0f && cursorX + GroupGap + blockW > avail)
                {
                    FinishLine(placements, line, lineStartX, cursorX, lineHeight);
                    cursorX = 0f;
                    cursorY += lineHeight + LineGap;
                    lineHeight = 0f;
                    line++;
                    lineStartX = 0f;
                }

                float x = cursorX;
                if (cursorX > 0f) x += GroupGap;
                placements.Add(new GroupPlacement(group, groupIds, x, cursorY, blockW, blockH,
                    rows, line, cursorX > 0f));

                cursorX = x + blockW;
                if (blockH > lineHeight) lineHeight = blockH;
                if (cursorX > totalW) totalW = cursorX;
            }
            FinishLine(placements, line, lineStartX, cursorX, lineHeight);

            float totalH = Mathf.Max(lineHeight, cell);

            // ---------- проход 2: объекты
            foreach (GroupPlacement p in placements)
            {
                if (p.Separator) continue;
                float x = p.X;
                if (p.HasSeparator)
                {
                    AddSeparator(p.X - GroupGap * 0.5f - 0.5f, p.Y, Mathf.Max(p.LineHeight, cell));
                }
                AddGroupHandle(p.Group, p.Ids, x, p.Y, p.BlockH);
                if (!IsGroupCollapsed(p.Group.Id))
                {
                    for (int i = 0; i < p.Ids.Count; i++)
                    {
                        int col = i / p.Rows;
                        int row = i % p.Rows;
                        AddButton(p.Ids[i],
                            x + GroupHandleWidth + 2f + col * (cell + spacing),
                            p.Y + row * (cell + spacing));
                    }
                }
            }

            Height = pad * 2f + cursorY + totalH + 1f;
            if (root != null) root.sizeDelta = new Vector2(0f, Height);
            if (gridRect != null)
                gridRect.sizeDelta = new Vector2(Mathf.Max(totalW, cell), totalH);

            Refresh();
        }

        /// <summary>Описание размещения одной группы на панели (проход 1 раскладки).</summary>
        private class GroupPlacement
        {
            public KvToolbarGroup Group;
            public List<string> Ids;
            public float X;
            public float Y;
            public float BlockW;
            public float BlockH;
            public int Rows;
            public int Line;
            public bool HasSeparator;
            public float LineHeight;
            public bool Separator;

            public GroupPlacement(KvToolbarGroup group, List<string> ids, float x, float y,
                float blockW, float blockH, int rows, int line, bool hasSeparator)
            {
                Group = group;
                Ids = ids;
                X = x;
                Y = y;
                BlockW = blockW;
                BlockH = blockH;
                Rows = rows;
                Line = line;
                HasSeparator = hasSeparator;
            }
        }

        /// <summary>Дописать высоту полосы всем её группам (для разделителей и отступов).</summary>
        private static void FinishLine(List<GroupPlacement> placements, int line, float startX,
            float endX, float height)
        {
            if (placements == null) return;
            for (int i = 0; i < placements.Count; i++)
                if (placements[i] != null && placements[i].Line == line)
                    placements[i].LineHeight = height;
        }

        /// <summary>Одна плоская группа «Тулбар» — режим «5 в ряд» (исторический вид).</summary>
        private static List<KeyValuePair<KvToolbarGroup, List<string>>> FlatGroup(List<string> visible)
        {
            List<KeyValuePair<KvToolbarGroup, List<string>>> result =
                new List<KeyValuePair<KvToolbarGroup, List<string>>>();
            KvToolbarGroup flat = new KvToolbarGroup("flat",
                KvLoc.T("toolbar.group.all", "Все команды"), "more");
            result.Add(new KeyValuePair<KvToolbarGroup, List<string>>(flat, visible));
            return result;
        }

        /// <summary>Создать кнопку команды в абсолютной позиции (от левого верхнего угла блока).</summary>
        private void AddButton(string id, float x, float y)
        {
            KvCommand command = KvCommands.Get(id);
            string icon = command != null && !string.IsNullOrEmpty(command.Icon) ? command.Icon : "info";

            KvIconButton button = KvWidgets.IconButton(gridRect, "Cmd_" + id, icon, cell,
                delegate { KvCommands.Invoke(id); });
            PlaceBox((RectTransform)button.transform, x, y, cell, cell);
            string tooltipBody = TooltipBodyFor(command);      // считается ОДИН раз (P2-10)
            button.Tooltip.Set(command != null ? command.LocalizedTitle : id,
                tooltipBody, command != null ? command.Hotkey : "");
            if (command != null) button.SetStub(command.Stub);
            buttons.Add(button);
            ids.Add(id);
            tooltipBodies.Add(tooltipBody);
        }

        /// <summary>
        /// Текст подсказки кнопки: ОБЛАСТЬ ПРИМЕНЕНИЯ (группа, ЭТАП 1) + описание команды.
        /// Именно группа отвечает на вопрос «что это за кнопка и где её искать».
        /// </summary>
        private static string TooltipBodyFor(KvCommand command)
        {
            if (command == null) return "";
            string group = KvToolbarGroups.GroupTitleOf(command.Id);
            string body = command.TooltipBody;
            if (string.IsNullOrEmpty(group)) return body;
            string prefix = KvLoc.T("toolbar.group", "Группа") + " «" + group + "»";
            return string.IsNullOrEmpty(body) ? prefix : prefix + " · " + body;
        }

        /// <summary>
        /// «Ручка» группы: узкая кнопка с иконкой группы и её названием в подсказке.
        /// Клик открывает выпадающий список команд группы (и пункт «свернуть»).
        /// </summary>
        private void AddGroupHandle(KvToolbarGroup group, List<string> groupIds,
            float x, float y, float height)
        {
            KvIconButton handle = KvWidgets.IconButton(gridRect, "Group_" + group.Id, group.Icon,
                GroupHandleWidth, delegate { ShowGroupMenu(group, groupIds, null); });
            handle.SetIcon(group.Icon);
            PlaceBox((RectTransform)handle.transform, x, y, GroupHandleWidth, Mathf.Max(cell, height));
            handle.Background.color = KvTheme.PanelHeader;
            handle.Icon.color = KvTheme.TextDim;
            handle.Tooltip.Set(KvLoc.T("toolbar.group", "Группа") + " «" + group.Title + "»",
                KvLoc.T("toolbar.group.hint",
                    "Клик — список команд группы; можно свернуть группу в одну иконку") +
                " · " + KvLoc.T("toolbar.group.count", "команд") + ": " + groupIds.Count, "");
            groupHandles.Add(handle);
            groupHandleIds.Add(group.Id);
        }

        /// <summary>Тонкая вертикальная линия-разделитель между группами.</summary>
        private void AddSeparator(float x, float y, float height)
        {
            if (height <= 0f) height = cell;
            Image sep = KvTheme.CreatePanel(gridRect, "GroupSep", KvTheme.Separator);
            PlaceBox(sep.rectTransform, x, y + 1f, 1f, Mathf.Max(4f, height - 2f));
        }

        private static void PlaceBox(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        private void ClearButtons()
        {
            foreach (KvIconButton b in buttons)
                if (b != null) Destroy(b.gameObject);
            buttons.Clear();
            ids.Clear();
            tooltipBodies.Clear();     // тексты подсказок живут ровно столько же, сколько кнопки
            foreach (KvIconButton b in groupHandles)
                if (b != null) Destroy(b.gameObject);
            groupHandles.Clear();
            groupHandleIds.Clear();
            if (gridRect == null) return;
            for (int i = gridRect.childCount - 1; i >= 0; i--)
            {
                Transform child = gridRect.GetChild(i);
                if (child != null) Destroy(child.gameObject);   // разделители-линии
            }
            if (groupMenu != null) groupMenu.Close();
        }

        /// <summary>Открыть список команд группы (ЭТАП 1).</summary>
        private void ShowGroupMenu(KvToolbarGroup group, List<string> groupIds, RectTransform anchor)
        {
            if (group == null) return;
            if (groupMenu == null)
            {
                // Слой списка — отдельный объект ПОД КАНВАСОМ (как KvTooltipLayer): живёт
                // ровно столько же, сколько оболочка, и не копится при пересборках.
                GameObject layer = new GameObject("KvToolbarGroupLayer", typeof(RectTransform));
                layer.transform.SetParent(canvasRect, false);
                RectTransform layerRect = (RectTransform)layer.transform;
                KvTheme.Stretch(layerRect);
                layerRect.SetAsLastSibling();
                groupMenu = layer.AddComponent<KvToolbarGroupMenu>();
            }
            groupMenu.Ensure(canvasRect, this);

            RectTransform target = anchor;
            if (target == null)
            {
                int index = groupHandleIds.IndexOf(group.Id);
                if (index >= 0 && index < groupHandles.Count && groupHandles[index] != null)
                    target = (RectTransform)groupHandles[index].transform;
            }
            if (target == null) return;
            groupMenu.Toggle(target, group, groupIds);
        }

        /// <summary>
        /// Ширина канваса в его собственных координатах: именно она определяет, сколько
        /// кнопок помещается в строку (CanvasScaler с «Scale With Screen Size» пересчитывает
        /// её сам, поэтому на 1366×768 и на 1920×1080 ширина в координатах канваса одна и та же).
        /// </summary>
        private float CanvasWidth()
        {
            if (canvasRect == null) return 0f;
            float width = canvasRect.rect.width;
            if (width > 1f) return width;
            float scale = Mathf.Abs(canvasRect.lossyScale.x);
            return scale > 0.0001f ? Screen.width / scale : 0f;
        }

        /// <summary>«Редкая» команда (ФИКС 9) — в компактном режиме скрывается из панели.</summary>
        private static bool IsRare(string id)
        {
            for (int i = 0; i < RareCommandIds.Length; i++)
                if (string.Equals(RareCommandIds[i], id, StringComparison.Ordinal)) return true;
            return false;
        }

        // ------------------------------------------------------------------ группы: состояние

        /// <summary>Свёрнута ли группа в одну иконку (состояние — в PlayerPrefs).</summary>
        public bool IsGroupCollapsed(string groupId)
        {
            return KvSettings.IsToolbarGroupCollapsed(groupId);
        }

        /// <summary>Свернуть/развернуть группу и перестроить панель.</summary>
        public void SetGroupCollapsed(string groupId, bool collapsed)
        {
            KvSettings.SetToolbarGroupCollapsed(groupId, collapsed);
            RebuildGrid();
        }

        /// <summary>Развернуть все группы (кнопка «Сбросить раскладку»).</summary>
        public void ExpandAllGroups()
        {
            foreach (KvToolbarGroup g in KvToolbarGroups.All)
                KvSettings.SetToolbarGroupCollapsed(g.Id, false);
            RebuildGrid();
        }

        /// <summary>Сколько групп свёрнуто (диагностика).</summary>
        public int CollapsedGroupCount
        {
            get
            {
                int count = 0;
                foreach (KvToolbarGroup g in KvToolbarGroups.All)
                    if (IsGroupCollapsed(g.Id)) count++;
                return count;
            }
        }

        /// <summary>Сколько «ручек» групп на панели (диагностика).</summary>
        public int GroupCount { get { return groupHandles.Count; } }

        /// <summary>Открыть список группы по её id (диагностика/тесты).</summary>
        public bool OpenGroupMenu(string groupId)
        {
            foreach (KeyValuePair<KvToolbarGroup, List<string>> pair in
                KvToolbarGroups.Arrange(sourceIds))
            {
                if (pair.Key.Id != groupId) continue;
                ShowGroupMenu(pair.Key, pair.Value, null);
                return groupMenu != null && groupMenu.IsOpen;
            }
            return false;
        }

        /// <summary>Список группы открыт (диагностика).</summary>
        public bool GroupMenuOpen { get { return groupMenu != null && groupMenu.IsOpen; } }
        /// <summary>Сколько строк в открытом списке группы (диагностика).</summary>
        public int GroupMenuRows { get { return groupMenu != null ? groupMenu.RowCount : 0; } }
        /// <summary>Закрыть список группы.</summary>
        public void CloseGroupMenu()
        {
            if (groupMenu != null) groupMenu.Close();
        }

        /// <summary>Заменить раскладку кнопок (для расширения/профилей).</summary>
        public void SetLayout(string[] commandIds)
        {
            sourceIds = commandIds ?? new string[0];
            RebuildGrid();
        }

        /// <summary>Активный «верстак» (0 — робот, 1 — SCARA).</summary>
        public void SetWorkbench(int index)
        {
            if (workbench != null) workbench.Set(index);
        }

        /// <summary>
        /// ОБНОВИТЬ СОСТОЯНИЕ КНОПОК (галки/доступность/иконки/подсказки) — БЕЗ ПОВТОРНОЙ СБОРКИ
        /// ТЕКСТА ПОДСКАЗОК (P2-10, 11.11.2026).
        ///
        /// ЧТО БЫЛО. `Refresh()` зовётся оболочкой десять раз в секунду и для КАЖДОЙ кнопки
        /// пересобирал текст подсказки (`TooltipBodyFor`: поиск группы + склейка строк +
        /// `KvLoc.T`). На тридцать кнопок это тридцать склеек строк десять раз в секунду —
        /// мусор в куче и работа в главном потоке ради текста, который НЕ МЕНЯЕТСЯ.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Подсказка зависит только от команды и её группы, то есть от
        /// константы: пересчёт был чистой работой «в никуда», отбиравшей кадр у рендера 72 Гц.
        ///
        /// ТЕПЕРЬ текст подсказки считается ОДИН РАЗ при сборке кнопки (`AddButton`) и лежит в
        /// `tooltipBodies`; если кнопок больше, чем текстов (пересборка ещё не закончилась),
        /// берётся живой расчёт — но это уже не кадровый путь.
        /// </summary>
        public void Refresh()
        {
            for (int i = 0; i < buttons.Count; i++)
            {
                KvIconButton button = buttons[i];
                if (button == null) continue;
                KvCommand c = KvCommands.Get(ids[i]);
                if (c == null) continue;

                bool enabled = c.Enabled;
                bool isToggle = c.IsChecked != null;
                bool on = isToggle && c.Checked;

                if (!string.IsNullOrEmpty(c.IconChecked) && isToggle)
                    button.SetIcon(on ? c.IconChecked : c.Icon);

                button.SetEnabled(enabled);
                if (enabled)
                {
                    Color? tint = null;
                    if (on && c.CheckedTint != null) tint = c.CheckedTint();
                    button.SetChecked(on, tint);
                }

                string body = i < tooltipBodies.Count ? tooltipBodies[i] : TooltipBodyFor(c);
                if (button.Tooltip != null) button.Tooltip.Set(c.LocalizedTitle, body, c.Hotkey);
            }
        }

        /// <summary>Перекрасить под текущую тему.</summary>
        public void Repaint()
        {
            if (root == null) return;
            bg.color = KvTheme.PanelBg;
            if (workbenchLabel != null) workbenchLabel.color = KvTheme.TextDim;
            if (rightBg != null) rightBg.color = new Color(0f, 0f, 0f, 0f);
            for (int i = 0; i < groupHandles.Count; i++)
            {
                if (groupHandles[i] == null) continue;
                groupHandles[i].Background.color = KvTheme.PanelHeader;
                groupHandles[i].Icon.color = KvTheme.TextDim;
            }
            Refresh();
        }

        /// <summary>Сколько кнопок в тулбаре (диагностика).</summary>
        public int ButtonCount { get { return buttons.Count; } }

        /// <summary>Сколько команд не попало в панель (в групповой раскладке — 0).</summary>
        public int MoreCount { get { return 0; } }

        /// <summary>Список id кнопок (диагностика).</summary>
        public IReadOnlyList<string> ButtonIds { get { return ids; } }

        /// <summary>Кнопка по индексу (диагностика/расширение).</summary>
        public KvIconButton ButtonAt(int index)
        {
            return index >= 0 && index < buttons.Count ? buttons[index] : null;
        }

        /// <summary>Сколько кнопок в самой широкой группе (диагностика раскладки).</summary>
        public int ColumnsCount { get { return columns; } }

        /// <summary>Группа, которой принадлежит кнопка с индексом (диагностика/тесты).</summary>
        public string GroupOfButton(int index)
        {
            if (index < 0 || index >= ids.Count) return "";
            return KvToolbarGroups.GroupTitleOf(ids[index]);
        }

        /// <summary>Прямоугольник панели (для раскладки).</summary>
        public RectTransform Root { get { return root; } }
    }

    /// <summary>
    /// Выпадающий список «Ещё» (ФИКС 8) — оставлен для совместимости: в групповой
    /// раскладке (ЭТАП 1) ничего не скрывается, поэтому список не используется.
    /// </summary>
    internal class KvToolbarMoreMenu : MonoBehaviour
    {
        /// <summary>Список сейчас открыт.</summary>
        public bool IsOpen { get { return false; } }
        /// <summary>Сколько команд в списке (диагностика).</summary>
        public int RowCount { get { return 0; } }

        public void Ensure(RectTransform canvas, KvToolbar toolbar) { }
        public void Toggle(RectTransform anchor, IReadOnlyList<string> commandIds) { }
        public void Close() { }
    }
}
