using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace KazistovVvUI
{
    /// <summary>
    /// НАВИГАЦИЯ ПО ИНТЕРФЕЙСУ С КЛАВИАТУРЫ (ЭТАП 11): стрелки — фокус и переход
    /// по кнопкам панели тулбара (геометрически, как в панелях инструментов), Enter — нажать,
    /// Esc — снять фокус. Фокус обводится рамкой.
    ///
    /// ФИКС 5 (§22): пробел СНЯТ с активации (ТЗ: «Пробел в проекте должен быть свободен»).
    /// Раньше он дублировал Enter, и нажатие пробела при фокусе на кнопке «Деревья поведения»
    /// открывало это окно. Активация — только Enter / цифровой Enter.
    ///
    /// ФИКС 2 (§23) — TAB НЕ ПЕРЕКЛЮЧАЕТ ОБЛАСТИ: `Ctrl+Tab` (переключатель «Тулбар → Меню →
    /// Дерево» из §22.9) УБРАН ПОЛНОСТЬЮ, области переключает отдельная клавиша **F6**;
    /// обратный переход «на уровень выше» — контекстный `Esc`.
    ///
    /// §26 — TAB ОТДАН РЕЖИМУ КУРСОРА и в навигации больше НЕ участвует: он переключает
    /// «режим камеры ⟷ режим интерфейса» (<see cref="KvMouseCursor.ModeToggleKey"/>).
    /// Фокус по кнопкам тулбара ставится ПЕРВОЙ ЖЕ СТРЕЛКОЙ (или возвратом в область «Тулбар»
    /// по F6) — раньше это делал Tab, и без него стрелки были бы недоступны.
    ///
    /// Работает ТОЛЬКО в режиме интерфейса (курсор свободен, панели видны) и только когда
    /// настройка «Навигация с клавиатуры» включена — в режиме камеры клавиши принадлежат
    /// роботу, как и раньше.
    ///
    /// ВАЖНО: режим КУРСОРА переключает Tab (KvMouseCursor, §26), интерфейс показывает/скрывает
    /// отдельная команда «Показать / скрыть интерфейс» (тулбар/меню), Esc из режима камеры тоже
    /// приводит в режим интерфейса. Ни одна из них не занята навигацией.
    /// </summary>
    public class KvKeyboardNav : MonoBehaviour
    {
        private static KvKeyboardNav instance;

        private KazistovVvUIManager ui;
        private KvToolbar toolbar;
        private RectTransform layer;
        private Image frame;
        private readonly List<RectTransform> focusables = new List<RectTransform>();
        private readonly List<KvIconButton> focusButtons = new List<KvIconButton>();
        private int index = -1;
        private float rebuildTimer;

        /// <summary>Индекс кнопки в фокусе (−1 — фокуса нет).</summary>
        public int FocusIndex { get { return index; } }
        /// <summary>Сколько элементов доступно навигации (диагностика).</summary>
        public int FocusableCount { get { return focusables.Count; } }
        /// <summary>Фокус есть.</summary>
        public bool HasFocus { get { return index >= 0 && index < focusables.Count; } }
        /// <summary>Подпись кнопки в фокусе (диагностика).</summary>
        public string FocusLabel
        {
            get
            {
                if (!HasFocus) return "";
                KvIconButton b = focusButtons[index];
                if (b == null || b.Tooltip == null) return "";
                return b.Tooltip.title;
            }
        }

        /// <summary>Создать навигацию (один раз на оболочку).</summary>
        public static KvKeyboardNav Create(RectTransform canvas, KazistovVvUIManager manager,
            KvToolbar bar)
        {
            GameObject go = new GameObject("KvKeyboardNav", typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            RectTransform rt = (RectTransform)go.transform;
            KvTheme.Stretch(rt);
            instance = go.AddComponent<KvKeyboardNav>();
            instance.ui = manager;
            instance.toolbar = bar;
            instance.layer = rt;
            instance.Build();
            instance.enabled = KvSettings.KeyboardNav;
            return instance;
        }

        /// <summary>Включить/выключить навигацию (настройка «Навигация с клавиатуры»).</summary>
        public void SetEnabled(bool value)
        {
            enabled = value;
            if (value) RebuildFocusables();
            else ClearFocus();
        }

        private void Build()
        {
            GameObject go = new GameObject("FocusFrame", typeof(Image));
            go.transform.SetParent(transform, false);
            frame = go.GetComponent<Image>();
            frame.sprite = KvTheme.WhiteSprite;
            frame.color = new Color(KvTheme.Accent.r, KvTheme.Accent.g, KvTheme.Accent.b, 0.22f);
            frame.raycastTarget = false;
            Outline o = go.AddComponent<Outline>();
            o.effectColor = KvTheme.Accent;
            o.effectDistance = new Vector2(2f, -2f);
            o.useGraphicAlpha = false;
            go.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Update()
        {
            if (ui == null || toolbar == null) return;

            // Навигация нужна только в режиме интерфейса: в телеоперации клавиши — роботу.
            // ФИКС 7/8 (§22): источник правды о курсоре — KvMouseCursor.
            bool uiMode = !KvMouseCursor.Captured && ui.uiVisible;
            if (!uiMode)
            {
                if (HasFocus) ClearFocus();
                ResetScopeMarks();
                return;
            }
            // Пока текст вводится (палитра, поиск биндов, переименование) — не мешаем.
            if (IsTypingInField()) return;

            rebuildTimer -= Time.unscaledDeltaTime;
            if (rebuildTimer <= 0f)
            {
                rebuildTimer = 0.5f;
                RebuildFocusables();
            }

            // ФИКС 2 (§23): ПЕРЕКЛЮЧЕНИЕ ОБЛАСТЕЙ — ОТДЕЛЬНАЯ КЛАВИША F6.
            // `Ctrl+Tab` УДАЛЁН ПОЛНОСТЬЮ (по ТЗ: «убрать Tab из переключения меню/областей»),
            // `Tab` остался только для фокуса внутри панели тулбара (ниже, в UpdateToolbarScope).
            // Одно нажатие — одна область; внутри области работают стрелки, Esc возвращает
            // на уровень выше (меню/дерево → тулбар).
            if (KeyDown(KeyCode.F6))
            {
                NextScope();
                return;
            }

            switch (scope)
            {
                case NavScope.Menu:
                    UpdateMenuScope();
                    return;
                case NavScope.Tree:
                    UpdateTreeScope();
                    return;
                default:
                    UpdateToolbarScope();
                    return;
            }
        }

        // ================================================================== ФИКС 9 (§22) + ФИКС 2 (§23)
        // ОБЛАСТИ НАВИГАЦИИ: стрелки не могут одновременно ходить и по тулбару, и по меню,
        // и по дереву, поэтому активная область выбирается ОТДЕЛЬНОЙ КЛАВИШЕЙ **F6**
        // (в §22 это был `Ctrl+Tab` — он убран по ТЗ ФИКСА 2), а внутри неё работают
        // стрелки (плюс Enter). Обратно «на уровень выше» возвращает контекстный Esc.
        // Подсветка: в тулбаре — рамка (как было), в меню и дереве —
        // их собственная подсветка выбора, ничего не дублируется.

        /// <summary>Активная область навигации с клавиатуры.</summary>
        public enum NavScope
        {
            /// <summary>Кнопки тулбара (по умолчанию).</summary>
            Toolbar = 0,
            /// <summary>Строка меню: ←/→ — группы, ↓ — открыть, ↑/↓ — пункты, Enter — нажать.</summary>
            Menu = 1,
            /// <summary>Дерево моделей: ↑/↓ — узлы, ←/→ — свернуть/развернуть.</summary>
            Tree = 2
        }

        private NavScope scope = NavScope.Toolbar;
        private int menuIndex = -1;      // подсвеченная группа верхнего меню
        private int rowIndex = -1;       // подсвеченный пункт открытого меню

        /// <summary>Активная область (диагностика и подсказка состояния).</summary>
        public NavScope Scope { get { return scope; } }
        /// <summary>Имя активной области по-русски (для строки состояния/подсказки).</summary>
        public string ScopeLabel
        {
            get
            {
                switch (scope)
                {
                    case NavScope.Menu: return "Меню";
                    case NavScope.Tree: return "Дерево";
                    default: return "Тулбар";
                }
            }
        }

        private void NextScope()
        {
            scope = NextScopeOf(scope);
            ResetScopeMarks();
            if (scope == NavScope.Toolbar && focusables.Count > 0) Move(1);
            Debug.Log("[KvNav] Область навигации: " + ScopeLabel +
                      " (F6 — следующая, стрелки — внутри области, Esc — уровень выше)");
        }

        /// <summary>
        /// СЛЕДУЮЩАЯ ОБЛАСТЬ НАВИГАЦИИ ПО КРУГУ (чистая функция — проверяется тестом).
        ///
        /// Зачем отдельно: областей ровно три, и «по кругу» — единственное правило переключения по F6.
        /// Если оно записано по месту, проверить его можно только нажатием клавиши в шлеме; ошибка
        /// (например, потерянная область «Дерево») видна оператору как «F6 ходит по двум областям».
        /// </summary>
        public static NavScope NextScopeOf(NavScope current)
        {
            return (NavScope)(((int)current + 1) % 3);
        }

        /// <summary>
        /// ИНДЕКС ПОСЛЕ ШАГА ПО КРУГУ (чистая функция): из конца списка шаг вперёд попадает в начало
        /// и наоборот. Так ходят стрелки по кнопкам тулбара; отрицательный результат невозможен,
        /// иначе `focusButtons[index]` дал бы исключение прямо в кадре.
        /// </summary>
        public static int WrapIndex(int index, int delta, int count)
        {
            if (count <= 0) return -1;
            int next = (index + delta) % count;
            if (next < 0) next += count;
            return next;
        }

        /// <summary>
        /// ИНДЕКС ПОСЛЕ ШАГА ПО СПИСКУ С ПРИЖАТИЕМ К ГРАНИЦАМ (чистая функция): так ходят ↑/↓ по
        /// пунктам меню и узлам дерева. В отличие от тулбара здесь НЕТ перехода через край: на
        /// последнем пункте шаг вниз остаётся на последнем, иначе выделение «прыгало» бы в начало.
        /// </summary>
        public static int StepInList(int index, int delta, int count)
        {
            if (count <= 0) return -1;
            if (index < 0) return 0;
            int next = index + delta;
            if (next < 0) return 0;
            if (next >= count) return count - 1;
            return next;
        }

        private void ResetScopeMarks()
        {
            rowIndex = -1;
            KvMenuBar bar = ui != null ? ui.Menu : null;
            if (bar != null) bar.HighlightRow(-1);
            menuIndex = bar != null ? bar.OpenMenuIndex : -1;
        }

        /// <summary>Стрелки и Enter в области «Тулбар» (поведение ЭТАПА 11 не менялось).</summary>
        private void UpdateToolbarScope()
        {
            // §26: Tab здесь БОЛЬШЕ НЕ ЧИТАЕТСЯ — он переключает режим курсора
            // (KvMouseCursor.ModeToggleKey). Фокус ставится первой стрелкой либо возвратом
            // в область «Тулбар» по F6 (<see cref="NextScope"/>), поэтому стрелки доступны.
            if (!HasFocus)
            {
                if (KeyDown(KeyCode.RightArrow) || KeyDown(KeyCode.LeftArrow) ||
                    KeyDown(KeyCode.UpArrow) || KeyDown(KeyCode.DownArrow))
                    Move(1);
                return;
            }

            if (KeyDown(KeyCode.RightArrow)) MoveGeometric(new Vector2(1f, 0f));
            else if (KeyDown(KeyCode.LeftArrow)) MoveGeometric(new Vector2(-1f, 0f));
            else if (KeyDown(KeyCode.UpArrow)) MoveGeometric(new Vector2(0f, 1f));
            else if (KeyDown(KeyCode.DownArrow)) MoveGeometric(new Vector2(0f, -1f));
            // ФИКС 5 (§22): ПРОБЕЛ БОЛЬШЕ НЕ НАЖИМАЕТ кнопку в фокусе. Раньше он был вторым
            // (после Enter) способом активации, и если фокус стоял на кнопке «Деревья поведения»
            // (`bt.tab`), нажатие пробела открывало это окно — оператор видел «на пробел
            // открывается меню «Деревья поведения»». По ТЗ пробел в проекте должен быть свободен:
            // активация — только Enter / Enter на цифровой клавиатуре.
            else if (KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter))
                Activate();
            else if (KeyDown(KeyCode.Escape)) ClearFocus();
        }

        /// <summary>
        /// Стрелки в области «Меню»: ←/→ — группы верхнего меню (открытая группа переезжает),
        /// ↓ — открыть группу / ходить по пунктам вниз, ↑ — по пунктам вверх, Enter — нажать
        /// подсвеченный пункт, Esc — закрыть меню и вернуться в тулбар.
        /// </summary>
        private void UpdateMenuScope()
        {
            KvMenuBar bar = ui != null ? ui.Menu : null;
            if (bar == null || bar.MenuCount == 0) return;

            bool left = KeyDown(KeyCode.LeftArrow);
            bool right = KeyDown(KeyCode.RightArrow);
            bool up = KeyDown(KeyCode.UpArrow);
            bool down = KeyDown(KeyCode.DownArrow);

            if (left || right)
            {
                if (menuIndex < 0) menuIndex = bar.OpenMenuIndex >= 0 ? bar.OpenMenuIndex : 0;
                menuIndex += right ? 1 : -1;
                if (menuIndex < 0) menuIndex = bar.MenuCount - 1;
                if (menuIndex >= bar.MenuCount) menuIndex = 0;
                bar.SetKeyboardHighlight(menuIndex);
                // Если меню уже открыто — переезжаем на соседнюю группу сразу (как в FreeCAD).
                if (bar.OpenMenuIndex >= 0) bar.ToggleMenu(bar.MenuNameAt(menuIndex));
                rowIndex = -1;
                return;
            }

            if (down)
            {
                if (bar.OpenMenuIndex < 0)
                {
                    if (menuIndex < 0) menuIndex = 0;
                    bar.ToggleMenu(bar.MenuNameAt(menuIndex));
                    rowIndex = -1;
                    return;
                }
                rowIndex = StepInList(rowIndex, 1, bar.NavigableRowCount);
                bar.HighlightRow(rowIndex);
                return;
            }

            if (up)
            {
                if (bar.OpenMenuIndex < 0)
                {
                    if (menuIndex < 0) menuIndex = 0;
                    bar.ToggleMenu(bar.MenuNameAt(menuIndex));
                    rowIndex = bar.NavigableRowCount - 1;
                    bar.HighlightRow(rowIndex);
                    return;
                }
                rowIndex = StepInList(rowIndex, -1, bar.NavigableRowCount);
                bar.HighlightRow(rowIndex);
                return;
            }

            if (KeyDown(KeyCode.Return) || KeyDown(KeyCode.KeypadEnter))
            {
                if (bar.OpenMenuIndex >= 0 && rowIndex >= 0)
                {
                    bar.ActivateRow(rowIndex);       // Command.Invoke внутри — как при клике мышью
                    rowIndex = -1;
                }
                else
                {
                    if (menuIndex < 0) menuIndex = 0;
                    bar.ToggleMenu(bar.MenuNameAt(menuIndex));
                    rowIndex = -1;
                }
                return;
            }

            // Esc: закрыть меню; если оно уже закрыто — вернуться в тулбар.
            if (KeyDown(KeyCode.Escape))
            {
                if (bar.OpenMenuIndex >= 0)
                {
                    bar.CloseMenu();
                    bar.SetKeyboardHighlight(-1);
                    rowIndex = -1;
                }
                else
                {
                    scope = NavScope.Toolbar;
                    bar.SetKeyboardHighlight(-1);
                }
            }
        }

        /// <summary>
        /// Стрелки в области «Дерево»: ↑/↓ — по видимым узлам, → — развернуть узел
        /// (или перейти к первому потомку), ← — свернуть узел, Esc — вернуться в тулбар.
        /// Выделение узла идёт тем же <see cref="KvTreeView.Select"/>, что и клик мышью.
        /// </summary>
        private void UpdateTreeScope()
        {
            KvTreeView tree = ui != null ? ui.Tree : null;
            if (tree == null) return;

            List<ProjectNode> nodes = tree.VisibleNodes();
            if (nodes.Count == 0) return;

            if (KeyDown(KeyCode.Escape)) { scope = NavScope.Toolbar; return; }

            int current = nodes.IndexOf(tree.Selected);

            if (KeyDown(KeyCode.DownArrow))
            {
                int next = StepInList(current, 1, nodes.Count);
                if (next < 0) return;
                tree.Select(nodes[next]);
                return;
            }
            if (KeyDown(KeyCode.UpArrow))
            {
                int prev = StepInList(current, -1, nodes.Count);
                if (prev < 0) return;
                tree.Select(nodes[prev]);
                return;
            }
            if (KeyDown(KeyCode.RightArrow))
            {
                if (current < 0) { tree.Select(nodes[0]); return; }
                ProjectNode node = nodes[current];
                if (!tree.IsExpandedNode(node) && node.Children != null && node.Children.Count > 0)
                    tree.Expand(node.Key);
                else if (node.Children != null && node.Children.Count > 0)
                    tree.Select(node.Children[0]);
                return;
            }
            if (KeyDown(KeyCode.LeftArrow))
            {
                if (current < 0) return;
                tree.Collapse(nodes[current]);
            }
        }

        /// <summary>Удерживается ли Ctrl. ФИКС 2 (§23): навигацией больше НЕ используется.</summary>
        private static bool CtrlHeld()
        {
            try
            {
                if (Keyboard.current != null)
                    return Keyboard.current.leftCtrlKey.isPressed || Keyboard.current.rightCtrlKey.isPressed;
            }
            catch { }
            try { return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl); }
            catch { return false; }
        }

        private static bool IsTypingInField()
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            GameObject selected = es.currentSelectedGameObject;
            if (selected == null) return false;
            InputField field = selected.GetComponent<InputField>();
            return field != null && field.isFocused;
        }

        private bool KeyDown(KeyCode code)
        {
            try
            {
                if (Keyboard.current != null)
                {
                    var control = Keyboard.current[KeyCodeToKey(code)];
                    if (control != null) return control.wasPressedThisFrame;
                }
                return Input.GetKeyDown(code);
            }
            catch
            {
                return false;
            }
        }

        private bool KeyHeld(KeyCode code)
        {
            try
            {
                if (Keyboard.current != null)
                {
                    var control = Keyboard.current[KeyCodeToKey(code)];
                    if (control != null) return control.isPressed;
                }
                return Input.GetKey(code);
            }
            catch
            {
                return false;
            }
        }

        private static UnityEngine.InputSystem.Key KeyCodeToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.Tab: return UnityEngine.InputSystem.Key.Tab;
                case KeyCode.F6: return UnityEngine.InputSystem.Key.F6;
                case KeyCode.Return: return UnityEngine.InputSystem.Key.Enter;
                case KeyCode.KeypadEnter: return UnityEngine.InputSystem.Key.NumpadEnter;
                case KeyCode.Space: return UnityEngine.InputSystem.Key.Space;
                case KeyCode.Escape: return UnityEngine.InputSystem.Key.Escape;
                case KeyCode.LeftArrow: return UnityEngine.InputSystem.Key.LeftArrow;
                case KeyCode.RightArrow: return UnityEngine.InputSystem.Key.RightArrow;
                case KeyCode.UpArrow: return UnityEngine.InputSystem.Key.UpArrow;
                case KeyCode.DownArrow: return UnityEngine.InputSystem.Key.DownArrow;
                case KeyCode.LeftShift: return UnityEngine.InputSystem.Key.LeftShift;
                case KeyCode.RightShift: return UnityEngine.InputSystem.Key.RightShift;
                default: return UnityEngine.InputSystem.Key.None;
            }
        }

        /// <summary>Собрать список элементов, доступных навигации (кнопки тулбара).</summary>
        private void RebuildFocusables()
        {
            focusables.Clear();
            focusButtons.Clear();
            if (toolbar == null) return;
            for (int i = 0; i < toolbar.ButtonCount; i++)
            {
                KvIconButton button = toolbar.ButtonAt(i);
                if (button == null || !button.IsEnabled) continue;
                RectTransform rt = button.transform as RectTransform;
                if (rt == null || !rt.gameObject.activeInHierarchy) continue;
                focusables.Add(rt);
                focusButtons.Add(button);
            }
        }

        private void Move(int delta)
        {
            if (focusables.Count == 0) return;
            index = WrapIndex(index, delta, focusables.Count);
            if (index < 0) return;
            Paint();
        }

        /// <summary>Перейти к ближайшей кнопке в заданном направлении (по геометрии).</summary>
        private void MoveGeometric(Vector2 direction)
        {
            if (!HasFocus) { Move(1); return; }
            Vector2 origin = focusables[index].TransformPoint(focusables[index].rect.center);
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < focusables.Count; i++)
            {
                if (i == index) continue;
                Vector2 point = focusables[i].TransformPoint(focusables[i].rect.center);
                Vector2 delta = point - origin;
                float forward = Vector2.Dot(delta, direction);
                if (forward <= 4f) continue;
                float lateral = Mathf.Abs(Vector2.Dot(delta, new Vector2(-direction.y, direction.x)));
                float score = forward + lateral * 2f;
                if (score >= bestDistance) continue;
                bestDistance = score;
                best = i;
            }
            if (best < 0) return;
            index = best;
            Paint();
        }

        /// <summary>Нажать кнопку в фокусе.</summary>
        public bool Activate()
        {
            if (!HasFocus) return false;
            KvIconButton button = focusButtons[index];
            if (button == null || button.Button == null) return false;
            button.Button.onClick.Invoke();
            return true;
        }

        /// <summary>Снять фокус.</summary>
        public void ClearFocus()
        {
            index = -1;
            if (frame != null) frame.gameObject.SetActive(false);
        }

        /// <summary>Поставить фокус на кнопку по id команды (диагностика/тесты).</summary>
        public bool FocusCommand(string commandId)
        {
            if (toolbar == null) return false;
            RebuildFocusables();      // список мог устареть (пересборка панели/смена раскладки)
            IReadOnlyList<string> ids = toolbar.ButtonIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] != commandId) continue;
                for (int f = 0; f < focusButtons.Count; f++)
                {
                    if (focusButtons[f] != toolbar.ButtonAt(i)) continue;
                    index = f;
                    Paint();
                    return true;
                }
            }
            return false;
        }

        private void Paint()
        {
            if (!HasFocus || frame == null) return;
            RectTransform target = focusables[index];
            RectTransform parent = (RectTransform)frame.transform.parent;
            Vector3 world = target.TransformPoint(target.rect.min);
            Vector3 worldMax = target.TransformPoint(target.rect.max);
            Vector2 localMin = parent.InverseTransformPoint(world);
            Vector2 localMax = parent.InverseTransformPoint(worldMax);

            frame.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            frame.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            frame.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            frame.rectTransform.sizeDelta = new Vector2(Mathf.Abs(localMax.x - localMin.x) + 2f,
                Mathf.Abs(localMax.y - localMin.y) + 2f);
            frame.rectTransform.anchoredPosition = (localMin + localMax) * 0.5f;
            frame.gameObject.SetActive(true);
            frame.transform.SetAsLastSibling();
        }

        /// <summary>Перекрасить рамку под текущую тему.</summary>
        public void Repaint()
        {
            if (frame == null) return;
            frame.color = new Color(KvTheme.Accent.r, KvTheme.Accent.g, KvTheme.Accent.b, 0.22f);
            Outline o = frame.GetComponent<Outline>();
            if (o != null) o.effectColor = KvTheme.Accent;
        }
    }
}
