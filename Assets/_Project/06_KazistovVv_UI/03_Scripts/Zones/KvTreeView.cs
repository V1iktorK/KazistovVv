using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// Левая панель — ДЕРЕВО МОДЕЛЕЙ в стиле FreeCAD (combo view):
    /// иерархия с раскрывающимися узлами, «глазик» (скрыть/показать объект),
    /// переименование по двойному клику, выбор узла (подсветка объекта в сцене).
    ///
    /// Строки строятся по списку <see cref="ProjectNode"/>; пересборка — только
    /// когда список реально изменился (сигнатуру считает менеджер UI), поэтому
    /// на FPS дерево не влияет.
    /// </summary>
    public class KvTreeView : MonoBehaviour
    {
        /// <summary>Максимальная глубина отрисовки (защита от циклов).</summary>
        public int maxDepth = 6;

        private RectTransform root;
        private RectTransform content;
        private ScrollRect scroll;
        private Image bg;
        private Text emptyText;
        private Text emptyHint;
        private readonly List<GameObject> rows = new List<GameObject>();
        private readonly HashSet<string> expanded = new HashSet<string>();
        private readonly Dictionary<string, bool> visibilityMemory = new Dictionary<string, bool>();

        private Action<ProjectNode> onSelect;
        private Action<ProjectNode, bool> onVisibilityChanged;
        private Action<ProjectNode, string> onRenamed;
        private Action onExpansionChanged;

        private ProjectNode selected;
        private List<ProjectNode> roots;

        /// <summary>
        /// ЭТАП 6: запрос КОНТЕКСТНОГО МЕНЮ (ПКМ по узлу). Менеджер UI подписывается
        /// и сам строит меню под тип узла/мультивыбор — дерево остаётся «немым».
        /// Аргументы: набор выбранных узлов, позиция курсора на экране.
        /// </summary>
        public Action<List<ProjectNode>, Vector2> ContextRequested;

        /// <summary>ЭТАП 6: набор узлов мультивыбора (Ctrl+клик).</summary>
        private readonly List<ProjectNode> multiSelection = new List<ProjectNode>();

        // --- переименование «на месте»
        private ProjectNode editing;
        private Text editingLabel;
        private string editBuffer = "";
        private bool editJustStarted;
        private bool textInputSubscribed;

        /// <summary>Сколько строк сейчас в дереве (диагностика).</summary>
        public int RowCount { get { return rows.Count; } }
        /// <summary>Выбранный узел.</summary>
        public ProjectNode Selected { get { return selected; } }
        /// <summary>Счётчик пересборок дерева (по нему видно, пересобиралось ли дерево).</summary>
        public int RebuildVersion { get; private set; }
        /// <summary>Идёт ли переименование узла.</summary>
        public bool IsRenaming { get { return editing != null; } }

        public void Build(RectTransform canvas, Canvas owner, Action<ProjectNode> select,
            Action<ProjectNode, bool> visibility, Action<ProjectNode, string> renamed,
            Action expansionChanged)
        {
            onSelect = select;
            onVisibilityChanged = visibility;
            onRenamed = renamed;
            onExpansionChanged = expansionChanged;

            GameObject go = new GameObject("KvTreeView", typeof(Image));
            go.transform.SetParent(canvas, false);
            root = (RectTransform)go.transform;
            bg = go.GetComponent<Image>();
            bg.sprite = KvTheme.WhiteSprite;
            bg.color = KvTheme.PanelDark;
            bg.raycastTarget = true;

            // --- строка инструментов дерева (развернуть/свернуть всё)
            // ФИКС 8 (§23): ЭТО И БЫЛИ «НЕПОНЯТНЫЕ + И −» СО СКРИНШОТА ОПЕРАТОРА.
            // Здесь две кнопки-иконки: «expand» рисуется как «+» и раскрывает дерево целиком,
            // «collapse» — как «−» и сворачивает его до корневых узлов. Полезны, поэтому
            // оставлены (вариант Б ТЗ), но приведены к эталону: квадрат 24×24 (было —
            // квадрат 100×100, потому что `CreateRow` не управляет размерами детей,
            // а `LayoutElement` в таком режиме Unity игнорирует), глиф 0.78·24 ≈ 19,
            // подсказки у обеих кнопок уже были — теперь они попадают точно в кнопку.
            RectTransform bar = KvWidgets.CreateRow(root, "TreeBar", KvWidgets.IconButtonSize, 2f);
            bar.anchorMin = new Vector2(0f, 1f);
            bar.anchorMax = new Vector2(1f, 1f);
            bar.pivot = new Vector2(0.5f, 1f);
            bar.sizeDelta = new Vector2(0f, KvWidgets.IconButtonSize);
            bar.anchoredPosition = Vector2.zero;

            KvIconButton expandAll = KvWidgets.IconButton(bar, "ExpandAll", "expand",
                KvWidgets.IconButtonSize, ExpandAll);
            expandAll.Tooltip.Set("Раскрыть все узлы", "Развернуть дерево целиком");
            expandAll.SetChecked(false);
            KvIconButton collapseAll = KvWidgets.IconButton(bar, "CollapseAll", "collapse",
                KvWidgets.IconButtonSize, CollapseAll);
            collapseAll.Tooltip.Set("Свернуть все узлы", "Оставить только корневые узлы");
            collapseAll.SetChecked(false);

            // --- прокручиваемый список
            GameObject scrollGo = new GameObject("Scroll", typeof(ScrollRect), typeof(RectMask2D),
                typeof(Image));
            scrollGo.transform.SetParent(go.transform, false);
            Image scrollBg = scrollGo.GetComponent<Image>();
            scrollBg.sprite = KvTheme.WhiteSprite;
            scrollBg.color = KvTheme.PanelDark;
            RectTransform viewport = (RectTransform)scrollGo.transform;
            viewport.anchorMin = Vector2.zero;
            viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(2f, 2f);
            viewport.offsetMax = new Vector2(-2f, -(KvWidgets.IconButtonSize + 2f));

            scroll = scrollGo.GetComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 26f;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.viewport = viewport;

            GameObject contentGo = new GameObject("Content", typeof(RectTransform));
            contentGo.transform.SetParent(viewport, false);
            content = (RectTransform)contentGo.transform;
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            content.sizeDelta = Vector2.zero;

            VerticalLayoutGroup vlg = contentGo.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 0f;
            vlg.padding = new RectOffset(1, 1, 1, 1);
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            ContentSizeFitter csf = contentGo.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            scroll.content = content;

            // --- ЭТАП 8: EMPTY STATE дерева (когда в сцене нет ни одного объекта)
            emptyText = KvTheme.CreateText(root, "Empty", KvLoc.T("tree.empty", "Нет объектов"),
                KvTheme.FontSizeSmall, TextAnchor.UpperLeft, KvTheme.TextDisabled);
            emptyText.rectTransform.anchorMin = new Vector2(0f, 1f);
            emptyText.rectTransform.anchorMax = new Vector2(1f, 1f);
            emptyText.rectTransform.pivot = new Vector2(0.5f, 1f);
            emptyText.rectTransform.sizeDelta = new Vector2(-10f, 18f);
            emptyText.rectTransform.anchoredPosition = new Vector2(0f, -24f);
            emptyText.gameObject.SetActive(false);

            emptyHint = KvTheme.CreateText(root, "EmptyHint",
                KvLoc.T("tree.empty.hint", "Выберите точку красным лазером (Z) — объекты появятся здесь"),
                KvTheme.FontSizeSmall - 1, TextAnchor.UpperLeft, KvTheme.TextDisabled);
            emptyHint.rectTransform.anchorMin = new Vector2(0f, 1f);
            emptyHint.rectTransform.anchorMax = new Vector2(1f, 1f);
            emptyHint.rectTransform.pivot = new Vector2(0.5f, 1f);
            emptyHint.rectTransform.sizeDelta = new Vector2(-10f, 30f);
            emptyHint.rectTransform.anchoredPosition = new Vector2(0f, -42f);
            emptyHint.horizontalOverflow = HorizontalWrapMode.Wrap;
            emptyHint.gameObject.SetActive(false);
        }

        /// <summary>Внешняя геометрия панели (задаёт менеджер через KvDockPanel).</summary>
        public RectTransform Root { get { return root; } }

        /// <summary>Пересобрать дерево по списку корневых узлов.</summary>
        public void Rebuild(List<ProjectNode> newRoots, ProjectNode newSelected)
        {
            // ЭТАП 6: узлы дерева пересоздаются при каждой пересборке, поэтому мультивыбор
            // запоминается ПО КЛЮЧАМ и восстанавливается на новых объектах узлов.
            List<string> multiKeys = new List<string>();
            foreach (ProjectNode n in multiSelection)
                if (n != null) multiKeys.Add(n.Key);

            roots = newRoots;
            selected = newSelected;
            RebuildVersion++;
            ClearRows();
            if (roots == null || roots.Count == 0)
            {
                if (emptyText != null) emptyText.gameObject.SetActive(true);
                if (emptyHint != null) emptyHint.gameObject.SetActive(true);
                return;
            }
            if (emptyText != null) emptyText.gameObject.SetActive(false);
            if (emptyHint != null) emptyHint.gameObject.SetActive(false);

            foreach (ProjectNode node in roots)
                AddRow(node, 0);

            multiSelection.Clear();
            foreach (string key in multiKeys)
            {
                ProjectNode node = FindByKey(key, 0);
                if (node != null) multiSelection.Add(node);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        /// <summary>Найти узел текущего дерева по ключу (для восстановления мультивыбора).</summary>
        public ProjectNode FindByKey(string key, int depth)
        {
            if (string.IsNullOrEmpty(key) || roots == null) return null;
            foreach (ProjectNode root in roots)
            {
                ProjectNode found = FindByKeyRecursive(root, key, depth);
                if (found != null) return found;
            }
            return null;
        }

        private static ProjectNode FindByKeyRecursive(ProjectNode node, string key, int depth)
        {
            if (node == null || depth > 6) return null;
            if (string.Equals(node.Key, key, StringComparison.Ordinal)) return node;
            foreach (ProjectNode child in node.Children)
            {
                ProjectNode found = FindByKeyRecursive(child, key, depth + 1);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Только выделение (без полной пересборки).</summary>
        public void SetSelected(ProjectNode node)
        {
            selected = node;
            foreach (GameObject rowGo in rows)
            {
                KvTreeRow row = rowGo != null ? rowGo.GetComponent<KvTreeRow>() : null;
                if (row == null) continue;
                bool inMulti = multiSelection.Contains(row.Node);
                row.SetSelected(row.Node == selected, inMulti);
            }
        }

        /// <summary>ЭТАП 6: узлы мультивыбора (Ctrl+клик). Первый — основной выбор.</summary>
        public IReadOnlyList<ProjectNode> MultiSelection { get { return multiSelection; } }

        /// <summary>ЭТАП 6: выделенные узлы: основной + мультивыбор (без повторов).</summary>
        public List<ProjectNode> SelectionSet()
        {
            List<ProjectNode> set = new List<ProjectNode>();
            if (selected != null) set.Add(selected);
            foreach (ProjectNode n in multiSelection)
                if (n != null && n != selected) set.Add(n);
            return set;
        }

        /// <summary>ЭТАП 6: очистить мультивыбор.</summary>
        public void ClearMultiSelection()
        {
            multiSelection.Clear();
            SetSelected(selected);
        }

        /// <summary>
        /// ЭТАП 6: обработать клик по узлу с учётом модификаторов:
        /// Ctrl+клик — добавить/убрать узел из мультивыбора, обычный клик — единственный выбор.
        /// </summary>
        public void HandleRowClick(ProjectNode node, bool ctrl)
        {
            if (node == null) return;
            if (ctrl)
            {
                if (multiSelection.Contains(node)) multiSelection.Remove(node);
                else multiSelection.Add(node);
                if (selected == null) selected = node;
                SetSelected(selected);
                if (onSelect != null) onSelect(selected);
                return;
            }
            multiSelection.Clear();
            Select(node);
        }

        /// <summary>ЭТАП 6: правый клик — выбрать узел (если он не в наборе) и запросить меню.</summary>
        public void HandleRowRightClick(ProjectNode node, Vector2 screenPosition)
        {
            if (node == null) return;
            if (selected != node && !multiSelection.Contains(node))
            {
                multiSelection.Clear();
                Select(node);
            }
            else if (node != selected)
            {
                // Клик по узлу из мультивыбора: он становится основным (действия — по всем).
                ProjectNode previous = selected;
                selected = node;
                multiSelection.Remove(node);
                if (previous != null && !multiSelection.Contains(previous))
                    multiSelection.Add(previous);
                SetSelected(selected);
            }
            if (ContextRequested != null) ContextRequested(SelectionSet(), screenPosition);
        }

        /// <summary>Раскрыть узел по ключу.</summary>
        public void Expand(string key)
        {
            if (!string.IsNullOrEmpty(key)) expanded.Add(key);
        }

        public void ExpandAll()
        {
            if (roots == null) return;
            foreach (ProjectNode node in roots) ExpandRecursive(node);
            if (onExpansionChanged != null) onExpansionChanged();
        }

        public void CollapseAll()
        {
            expanded.Clear();
            if (onExpansionChanged != null) onExpansionChanged();
        }

        private void ExpandRecursive(ProjectNode node)
        {
            if (node == null) return;
            if (node.Children != null && node.Children.Count > 0) expanded.Add(node.Key);
            foreach (ProjectNode child in node.Children) ExpandRecursive(child);
        }

        public bool IsExpanded(string key)
        {
            return !string.IsNullOrEmpty(key) && expanded.Contains(key);
        }

        // ------------------------------------------------------------------ строки

        private void AddRow(ProjectNode node, int depth)
        {
            if (node == null || depth > maxDepth) return;

            bool hasChildren = node.Children != null && node.Children.Count > 0;
            bool open = hasChildren && expanded.Contains(node.Key);
            bool visible = IsObjectVisible(node);

            GameObject rowGo = new GameObject("Row_" + node.DisplayName, typeof(Image), typeof(Button),
                typeof(LayoutElement));
            rowGo.transform.SetParent(content, false);

            Image rowBg = rowGo.GetComponent<Image>();
            rowBg.sprite = KvTheme.WhiteSprite;
            rowBg.color = new Color(0f, 0f, 0f, 0f);
            rowBg.raycastTarget = true;

            float rowHeight = KvSettings.RowHeight;
            LayoutElement le = rowGo.GetComponent<LayoutElement>();
            le.minHeight = rowHeight;
            le.preferredHeight = rowHeight;

            KvTreeRow row = rowGo.AddComponent<KvTreeRow>();
            row.Bind(node, this, rowBg, visible);
            row.onSelect = onSelect;

            Button btn = rowGo.GetComponent<Button>();
            btn.transition = Selectable.Transition.None;
            // Выбор/мультивыбор и ПКМ обрабатывает сама строка (KvTreeRow.OnPointerClick):
            // кнопка здесь только завершает переименование, чтобы клики не дублировались.
            btn.onClick.AddListener(delegate
            {
                if (editing != null && editing != node) CommitRename();
            });

            float x = 2f + depth * 11f;

            // --- стрелка раскрытия
            if (hasChildren)
            {
                KvIconButton arrow = KvWidgets.IconButton((RectTransform)rowGo.transform, "Arrow",
                    open ? "chevron-down" : "chevron-right", 12f, delegate
                    {
                        if (expanded.Contains(node.Key)) expanded.Remove(node.Key);
                        else expanded.Add(node.Key);
                        if (onExpansionChanged != null) onExpansionChanged();
                    });
                PlaceBox((RectTransform)arrow.transform, x, 12f);
                arrow.Background.color = new Color(0f, 0f, 0f, 0f);
                arrow.Icon.color = KvTheme.TextDim;
                arrow.Tooltip.Set(open ? "Свернуть узел" : "Раскрыть узел", node.DisplayName, "");
                x += 12f;
            }
            else
            {
                x += 12f;
            }

            // --- иконка типа
            GameObject iconGo = new GameObject("Icon", typeof(Image));
            iconGo.transform.SetParent(rowGo.transform, false);
            Image icon = iconGo.GetComponent<Image>();
            icon.sprite = KvIcons.Get(node.IconId, 12);
            icon.color = IconColorFor(node);
            icon.raycastTarget = false;
            PlaceBox(icon.rectTransform, x, 12f);
            if (icon.sprite == null) iconGo.SetActive(false);
            x += 14f;

            // --- подпись (или поле переименования)
            Text label = KvTheme.CreateText((RectTransform)rowGo.transform, "Label",
                node.DisplayName, KvTheme.FontSizeSmall, TextAnchor.MiddleLeft,
                node == selected ? KvTheme.TextFor(true, true) : KvTheme.TextMain);
            RectTransform lr = label.rectTransform;
            lr.anchorMin = new Vector2(0f, 0f);
            lr.anchorMax = new Vector2(1f, 1f);
            lr.offsetMin = new Vector2(x, 0f);
            lr.offsetMax = new Vector2(-40f, 0f);
            label.raycastTarget = false;
            row.Label = label;

            // --- детали (метрики) справа
            if (!string.IsNullOrEmpty(node.Details))
            {
                Text details = KvTheme.CreateText((RectTransform)rowGo.transform, "Details",
                    node.Details, KvTheme.FontSizeSmall - 1, TextAnchor.MiddleRight, KvTheme.TextDim);
                RectTransform dr = details.rectTransform;
                dr.anchorMin = new Vector2(1f, 0f);
                dr.anchorMax = new Vector2(1f, 1f);
                dr.pivot = new Vector2(1f, 0.5f);
                dr.sizeDelta = new Vector2(150f, 0f);
                dr.anchoredPosition = new Vector2(-20f, 0f);
                details.raycastTarget = false;
            }

            // --- «глазик» видимости объекта
            if (node.CanHide)
            {
                ProjectNode captured = node;
                KvIconButton eye = KvWidgets.IconButton((RectTransform)rowGo.transform, "Eye",
                    visible ? "eye" : "eye-off", 13f, delegate
                    {
                        bool nowVisible = !IsObjectVisible(captured);
                        SetObjectVisible(captured, nowVisible);
                        if (onVisibilityChanged != null) onVisibilityChanged(captured, nowVisible);
                        if (onExpansionChanged != null) onExpansionChanged();
                    });
                PlaceBox((RectTransform)eye.transform, 0f, 13f, right: true);
                eye.Background.color = new Color(0f, 0f, 0f, 0f);
                eye.Icon.color = visible ? KvTheme.TextDim : KvTheme.TextDisabled;
                eye.Tooltip.Set(visible ? "Скрыть объект" : "Показать объект",
                    "Видимость только визуальная: логика, коллайдеры и расчёты не меняются", "");
            }

            // --- подсказка строки
            KvTooltipTarget tip = rowGo.AddComponent<KvTooltipTarget>();
            tip.Set(node.DisplayName, BuildTooltip(node), "");

            rows.Add(rowGo);

            if (hasChildren && open)
            {
                foreach (ProjectNode child in node.Children) AddRow(child, depth + 1);
            }
        }

        private string BuildTooltip(ProjectNode node)
        {
            if (!string.IsNullOrEmpty(node.Tooltip)) return node.Tooltip;
            if (node.WorldTransform != null)
                return "Объект сцены: " + node.WorldTransform.name + "\nДвойной клик — переименовать узел";
            return "Двойной клик — переименовать узел";
        }

        private void PlaceBox(RectTransform rt, float x, float size, bool right = false)
        {
            rt.anchorMin = new Vector2(right ? 1f : 0f, 0.5f);
            rt.anchorMax = new Vector2(right ? 1f : 0f, 0.5f);
            rt.pivot = new Vector2(right ? 1f : 0f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(right ? -4f : x, 0f);
            LayoutElement le = rt.GetComponent<LayoutElement>();
            if (le != null) Destroy(le);
        }

        private static Color IconColorFor(ProjectNode node)
        {
            switch (node.Kind)
            {
                case ProjectNodeKind.Robot: return KvTheme.Accent;
                case ProjectNodeKind.Point: return KvTheme.LaserRed;
                case ProjectNodeKind.Trajectory: return KvTheme.Warn;
                case ProjectNodeKind.Phantom: return KvTheme.Ok;
                default: return KvTheme.TextDim;
            }
        }

        // ------------------------------------------------------------------ видимость

        /// <summary>Виден ли объект узла (по рендерерам, без смены hideFlags).</summary>
        public static bool IsObjectVisible(ProjectNode node)
        {
            if (node == null || node.WorldTransform == null) return true;
            Renderer[] renderers = node.WorldTransform.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return true;
            return renderers[0].enabled;
        }

        /// <summary>
        /// Скрыть/показать объект узла: выключаются ТОЛЬКО рендереры (визуальная
        /// видимость, как «глазик» во FreeCAD). Коллайдеры, скрипты, State Machine,
        /// планировщик и `CollisionWorld` не затрагиваются — объект остаётся препятствием.
        /// </summary>
        public void SetObjectVisible(ProjectNode node, bool visible)
        {
            if (node == null || node.WorldTransform == null) return;
            Renderer[] renderers = node.WorldTransform.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                r.enabled = visible;
            }
            if (visibilityMemory != null) visibilityMemory[node.Key] = visible;
        }

        // ------------------------------------------------------------------ выбор/переименование

        /// <summary>Выбрать узел (подсветить объект в сцене).</summary>
        public void Select(ProjectNode node)
        {
            selected = node;
            SetSelected(node);
            if (onSelect != null) onSelect(node);
        }

        /// <summary>
        /// ФИКС 9 (§22): узлы РОВНО в том порядке, в каком они видны в дереве (сверху вниз).
        /// Нужно навигации с клавиатуры: ↑/↓ ходят по этому списку. Только чтение —
        /// ни выделение, ни раскрытие узлов не меняются.
        /// </summary>
        public List<ProjectNode> VisibleNodes()
        {
            List<ProjectNode> result = new List<ProjectNode>();
            for (int i = 0; i < rows.Count; i++)
            {
                GameObject rowGo = rows[i];
                if (rowGo == null) continue;
                KvTreeRow row = rowGo.GetComponent<KvTreeRow>();
                if (row == null || row.Node == null) continue;
                result.Add(row.Node);
            }
            return result;
        }

        /// <summary>Раскрыт ли узел (по ключу) — используется навигацией стрелками.</summary>
        public bool IsExpandedNode(ProjectNode node)
        {
            return node != null && IsExpanded(node.Key);
        }

        /// <summary>Свернуть узел (навигация стрелками).</summary>
        public void Collapse(ProjectNode node)
        {
            if (node == null || node.Children == null || node.Children.Count == 0) return;
            if (!expanded.Contains(node.Key)) return;
            expanded.Remove(node.Key);
            if (onExpansionChanged != null) onExpansionChanged();
        }

        /// <summary>Начать переименование узла (двойной клик).</summary>
        public void BeginRename(ProjectNode node, Text label)
        {
            if (node == null || label == null) return;
            editing = node;
            editingLabel = label;
            editBuffer = node.DisplayName ?? "";
            editJustStarted = true;
            if (!textInputSubscribed && Keyboard.current != null)
            {
                Keyboard.current.onTextInput += OnTextInput;
                textInputSubscribed = true;
            }
            PaintEditing();
        }

        /// <summary>Начать переименование ВЫБРАННОГО узла (внешний вызов/диагностика).</summary>
        public bool BeginRenameSelected()
        {
            if (selected == null) return false;
            foreach (GameObject rowGo in rows)
            {
                if (rowGo == null) continue;
                KvTreeRow row = rowGo.GetComponent<KvTreeRow>();
                if (row != null && row.Node == selected)
                {
                    BeginRename(selected, row.Label);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Текст, который сейчас набран при переименовании (диагностика).</summary>
        public string EditBuffer { get { return editBuffer; } }

        /// <summary>Дописать текст в поле переименования (внешний источник: геймпад, IME, тест).</summary>
        public void AppendToEdit(string text)
        {
            if (editing == null || string.IsNullOrEmpty(text)) return;
            editBuffer += text;
            PaintEditing();
        }

        /// <summary>
        /// ЗАДАТЬ ТЕКСТ ПОЛЯ ЦЕЛИКОМ (сессия §41).
        ///
        /// Зачем: в шлеме имя набирается на виртуальной клавиатуре и приходит ГОТОВОЙ строкой, а
        /// `BeginRename` предзаполняет буфер текущим именем. Дописывать к нему было бы неверно —
        /// получилось бы «Стенд 1Стенд 1». Своё переименование в шлеме не заводится: и проверки
        /// (Trim, 48 символов), и запись ярлыка остаются здесь, в контракте компьютера.
        /// </summary>
        public void SetEditText(string text)
        {
            if (editing == null) return;
            editBuffer = text ?? "";
            PaintEditing();
        }

        /// <summary>Подтвердить переименование (внешний вызов).</summary>
        public void CommitRenameNow()
        {
            CommitRename();
        }

        /// <summary>Отменить переименование (внешний вызов).</summary>
        public void CancelRenameNow()
        {
            CancelRename();
        }

        private void CommitRename()
        {
            if (editing == null) return;
            string value = (editBuffer ?? "").Trim();
            ProjectNode node = editing;
            StopEditing();
            if (value.Length > 0 && value != node.DisplayName)
            {
                node.DisplayName = value;
                if (onRenamed != null) onRenamed(node, value);
                if (onExpansionChanged != null) onExpansionChanged();
            }
        }

        private void CancelRename()
        {
            if (editing == null) return;
            StopEditing();
            if (onExpansionChanged != null) onExpansionChanged();
        }

        private void StopEditing()
        {
            editing = null;
            editingLabel = null;
            editBuffer = "";
            if (textInputSubscribed && Keyboard.current != null)
            {
                Keyboard.current.onTextInput -= OnTextInput;
                textInputSubscribed = false;
            }
        }

        private void PaintEditing()
        {
            if (editingLabel == null) return;
            editingLabel.text = editBuffer + "|";
            editingLabel.color = KvTheme.Accent;
        }

        private void OnTextInput(char c)
        {
            if (editing == null) return;
            if (c == '\n' || c == '\r') { CommitRename(); return; }
            if (c == '\b')
            {
                if (editBuffer.Length > 0) editBuffer = editBuffer.Substring(0, editBuffer.Length - 1);
                PaintEditing();
                return;
            }
            if (c < ' ') return;
            if (editBuffer.Length >= 48) return;
            editBuffer += c;
            PaintEditing();
        }

        private void Update()
        {
            if (editing == null) return;

            // Enter/Esc/Backspace — через состояние клавиш (работает и без Input System).
            bool enter = false, escape = false, backspace = false;
            if (Keyboard.current != null)
            {
                if (editJustStarted)
                {
                    editJustStarted = false;
                }
                else
                {
                    enter = Keyboard.current.enterKey.wasPressedThisFrame ||
                            Keyboard.current.numpadEnterKey.wasPressedThisFrame;
                    escape = Keyboard.current.escapeKey.wasPressedThisFrame;
                    backspace = Keyboard.current.backspaceKey.wasPressedThisFrame;
                }
            }
            else
            {
                try
                {
                    enter = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
                    escape = Input.GetKeyDown(KeyCode.Escape);
                    backspace = Input.GetKeyDown(KeyCode.Backspace);
                }
                catch { }
            }

            if (escape) { CancelRename(); return; }
            if (enter) { CommitRename(); return; }
            if (backspace && editBuffer.Length > 0)
            {
                editBuffer = editBuffer.Substring(0, editBuffer.Length - 1);
                PaintEditing();
            }
        }

        private void ClearRows()
        {
            foreach (GameObject go in rows)
                if (go != null) Destroy(go);
            rows.Clear();
        }

        private void OnDisable()
        {
            StopEditing();
        }

        /// <summary>Перекрасить дерево под текущую тему (без пересборки строк).</summary>
        public void Repaint()
        {
            if (bg != null) bg.color = KvTheme.PanelDark;
            if (emptyText != null) emptyText.color = KvTheme.TextDisabled;
        }
    }

    /// <summary>Строка дерева: выбор, мультивыбор (Ctrl), ПКМ (контекстное меню), двойной клик.</summary>
    internal class KvTreeRow : MonoBehaviour, IPointerClickHandler
    {
        public ProjectNode Node { get; private set; }
        public Text Label;
        public Action<ProjectNode> onSelect;

        private KvTreeView owner;
        private Image background;
        private bool visible = true;

        public void Bind(ProjectNode node, KvTreeView view, Image bg, bool isVisible)
        {
            Node = node;
            owner = view;
            background = bg;
            visible = isVisible;
            SetSelected(false);
        }

        public void SetSelected(bool value) { SetSelected(value, false); }

        public void SetSelected(bool primary, bool inMulti)
        {
            if (background == null) return;
            Color color = new Color(0f, 0f, 0f, 0f);
            if (primary) color = KvTheme.SelectionBg;
            else if (inMulti)
            {
                Color accent = KvTheme.Accent;
                color = new Color(accent.r, accent.g, accent.b, KvTheme.IsLight ? 0.45f : 0.30f);
            }
            background.color = color;
            if (Label != null)
                Label.color = visible ? KvTheme.TextFor(primary, true) : KvTheme.TextDisabled;
        }

        public void OnPointerClick(PointerEventData e)
        {
            // ПКМ — контекстное меню (ЭТАП 6).
            if (e.button == PointerEventData.InputButton.Right)
            {
                if (owner != null) owner.HandleRowRightClick(Node, e.position);
                return;
            }
            if (e.button != PointerEventData.InputButton.Left) return;

            if (e.clickCount >= 2)
            {
                if (owner != null) owner.BeginRename(Node, Label);
                return;
            }

            if (owner != null) owner.HandleRowClick(Node, CtrlPressed());
            else if (onSelect != null) onSelect(Node);
        }

        /// <summary>Ctrl (или Cmd) нажат — мультивыбор.</summary>
        private static bool CtrlPressed()
        {
            try
            {
                if (Keyboard.current != null) return Keyboard.current.ctrlKey.isPressed;
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            }
            catch
            {
                return false;
            }
        }
    }
}
