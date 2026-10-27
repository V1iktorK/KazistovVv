using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>Одна строка реестра биндов (клавиатура, мышь, геймпад).</summary>
    public class KvBindEntry
    {
        /// <summary>Раздел: «Интерфейс», «Робот», «Траектории», «Экспорт», «Геймпад» …</summary>
        public string Group = "";
        /// <summary>Что делает.</summary>
        public string Action = "";
        /// <summary>Клавиша/кнопка («Z», «Ctrl+P», «LT»).</summary>
        public string Keys = "";
        /// <summary>Пояснение (необязательно).</summary>
        public string Note = "";
        /// <summary>Устройство: «Клавиатура», «Геймпад» …</summary>
        public string Device = "Клавиатура";
        /// <summary>
        /// Контекстный бинд: одна и та же клавиша в РАЗНЫХ состояниях (Esc — отмена/сброс).
        /// Такие строки не считаются конфликтом (иначе окно горячих клавиш «краснело» бы зря).
        /// </summary>
        public bool Contextual;
        /// <summary>Id команды, если бинд можно выполнить из палитры команд.</summary>
        public string CommandId = "";

        public KvBindEntry() { }

        public KvBindEntry(string group, string action, string keys, string note = "",
            string device = "Клавиатура", bool contextual = false, string commandId = "")
        {
            Group = group;
            Action = action;
            Keys = keys;
            Note = note;
            Device = device;
            Contextual = contextual;
            CommandId = commandId;
        }
    }

    /// <summary>
    /// ЕДИНЫЙ РЕЕСТР БИНДОВ (ЭТАП 7): окно «Горячие клавиши» (F12), палитра команд и
    /// проверка конфликтов читают ЭТОТ список. Значения соответствуют фактическому коду:
    /// клавиатура/мышь — как было (бинды не менялись), геймпад — ЭТАП 9 этой сессии.
    /// </summary>
    public static class KvBindings
    {
        private static List<KvBindEntry> entries;

        /// <summary>Все бинды (сначала клавиатура, затем геймпад, затем VR-заглушки).</summary>
        public static List<KvBindEntry> All()
        {
            if (entries != null) return entries;
            entries = new List<KvBindEntry>();

            // ---------------------------------------------------------- траектории (шаги 0–5)
            Add("Траектории", "Выбор точки (красный лазер)", "Z",
                "включает/выключает левую указку", false, "point.select");
            Add("Траектории", "Выбор траектории (зелёный лазер)", "X",
                "включает/выключает правую указку", false, "path.select");
            Add("Траектории", "Подтверждение действия", "ЛКМ",
                "смысл зависит от включённого лазера (шаги 1–5)", false, "flow.confirm");
            Add("Траектории", "Режим перемещения точки", "Enter",
                "вход и подтверждение (только на шагах 1–2). Бинд КОНТЕКСТНЫЙ (§27): пока фокус " +
                "клавиатурной навигации стоит на кнопке тулбара/меню, Enter нажимает ЭТУ кнопку " +
                "и в поток НЕ идёт — иначе одно нажатие делало бы два действия " +
                "(см. FreeFlyCameraController, арбитраж Enter)", true, "flow.enter");
            Add("Траектории", "Движение точки в режиме", "Q/E, W/S, A/D",
                "мировые оси; Shift — втрое быстрее (работает ТОЛЬКО в режиме перемещения точки)",
                true, "flow.movepoint");
            Add("Траектории", "Глубина шарика прицела", "колесо мыши",
                "вперёд — от оператора, с прилипанием к поверхности", false, "aim.depth");
            Add("Траектории", "Шарик к ближайшей поверхности", "средняя кнопка", "", false, "aim.snap");
            Add("Траектории", "Отмена / сброс", "Esc",
                "в режиме перемещения — отмена, вне его — сброс сценария", true, "edit.reset");

            // ---------------------------------------------------------- робот
            Add("Робот", "Выбор робота по прицелу", "F", "", false, "robot.switch");
            Add("Робот", "Фонарик", "G", "вкл/выкл с первого нажатия", false, "tool.flashlight");
            Add("Робот", "Аварийная остановка", "—",
                "кнопка тулбара / LB на геймпаде", false, "estop");
            Add("Робот", "Слайдеры суставов", "—", "панель тулбара", false, "joints.panel");
            Add("Робот", "Захват: открыть/закрыть", "V", "", false, "gripper.toggle");

            // ---------------------------------------------------------- интерфейс
            // §26: РЕЖИМ КУРСОРА переключает TAB (режим камеры ⟷ режим интерфейса).
            // CAPS LOCK НЕ ДЕЛАЕТ НИЧЕГО — удержание убрано полностью (§23.3 отменён §26).
            // Области навигации переключает F6 (ФИКС 2 §23), из режима камеры в режим
            // интерфейса приводит и Esc (он же дальше работает контекстно).
            Add("Интерфейс", "Режим курсора: Камера ⟷ UI", "TAB",
                "TAB показывает СИСТЕМНЫЙ курсор Unity (Cursor.visible = true) и панели: мышь " +
                "работает только с интерфейсом, камера НЕ реагирует на мышь и НЕ двигается клавишами " +
                "(WASD/QE отключены). Повторный TAB возвращает режим камеры (курсор скрыт и захвачен, " +
                "мышь вращает камеру, WASD двигает камеру). То же самое делает КЛИК по индикатору " +
                "режима в статус-баре (иконка + «Режим: Камера / UI»). CAPS LOCK не делает ничего",
                false, "ui.capture");
            Add("Интерфейс", "Системный курсор Unity / контекст", "ESC",
                "ВАРИАНТ А (Esc = Tab с приоритетом контекста). В режиме камеры Esc показывает " +
                "системный курсор Unity и панели — то же, что TAB. В режиме интерфейса Esc СНАЧАЛА " +
                "отдаётся контексту: открытое меню, палитра Ctrl+P, контекстное меню узла, группы " +
                "тулбара, размещение робота, незавершённый сценарий, фокус навигации — их Esc " +
                "закрывает/отменяет, режим НЕ меняется. Если контекста нет — Esc возвращает режим " +
                "камеры (как повторный TAB). Геймпад B — только «показать курсор», режим не меняет",
                true, "ui.cursorfree");
            Add("Интерфейс", "Показать/скрыть панели (без смены режима курсора)", "—",
                "кнопка тулбара и меню «Вид → Показать-скрыть интерфейс»", false, "ui.toggle");
            Add("Интерфейс", "Навигация с клавиатуры: фокус по кнопкам тулбара", "стрелки, ENTER",
                "первая стрелка ставит фокус и ходит по соседним кнопкам тулбара, ENTER — нажать, " +
                "ESC — снять фокус (работает при включённой настройке). TAB в навигации " +
                "НЕ участвует: он переключает режим курсора (§26)",
                false, "ui.kbnav");
            Add("Интерфейс", "Область навигации: тулбар → меню → дерево", "F6",
                "ФИКС 2: CTRL+TAB УБРАН. В меню ←/→ — группы, ↓ — открыть, ↑/↓ — пункты, " +
                "ENTER — нажать, ESC — вернуться в тулбар; в дереве ↑/↓ — узлы, " +
                "←/→ — свернуть/развернуть, ESC — вернуться в тулбар", false, "ui.navscope");
            Add("Интерфейс", "Палитра команд", "Ctrl+P",
                "поиск по всем командам; Ctrl+Shift+P — тоже", false, "ui.palette");
            Add("Интерфейс", "Горячие клавиши", "F12",
                "это окно со списком и поиском", false, "help.hotkeys");
            Add("Интерфейс", "Движение камеры", "W/A/S/D, Q/E",
                "Shift — быстрее; в режиме перемещения клавиши отданы точке", false, "cam.move");
            Add("Интерфейс", "Обзор камеры", "мышь или правый стик геймпада", "", false, "cam.look");
            Add("Интерфейс", "Размещение: тип робота", "1 / 2",
                "только в режиме «Добавить робота»", true);
            Add("Интерфейс", "Размещение: поворот", "← / →, A / D", "", true);
            Add("Интерфейс", "Размещение: подтвердить", "ЛКМ / Enter", "", true);

            // ---------------------------------------------------------- контекстное меню (ЭТАП 6)
            Add("Дерево моделей", "Контекстное меню узла", "ПКМ",
                "переименовать, удалить, скрыть, фокус, свойства, копировать имя, дублировать",
                false, "tree.context");
            Add("Дерево моделей", "Мультивыбор узлов", "Ctrl + ЛКМ",
                "действие меню применяется ко всем выбранным узлам", false, "tree.multiselect");
            Add("Дерево моделей", "Переименование узла", "двойной ЛКМ", "", false, "tree.rename");

            // ---------------------------------------------------------- постобработка / экспорт
            Add("Постобработка и экспорт", "Тепловая карта достижимости", "H", "", false, "view.heatmap");
            Add("Постобработка и экспорт", "Тепловая карта зазоров", "J", "", false, "view.clearance");
            Add("Постобработка и экспорт", "Графики углов суставов", "F9*", "открывается панелью", true);
            Add("Постобработка и экспорт", "Скриншот / видео / PDF", "—",
                "кнопки тулбара (запись и экспорт)", false, "shot.screenshot");

            // ---------------------------------------------------------- ГЕЙМПАД (ЭТАП 9)
            AddPad("Робот", "Переключение робота (робот / SCARA)", "D-Pad ← / →", "",
                "robot.switch");
            AddPad("Траектории", "Глубина шарика прицела (глубже / ближе)", "D-Pad ↑ / ↓", "",
                "aim.depth");
            AddPad("Траектории", "Подтверждение (аналог ЛКМ): точка / траектория", "A (нижняя кнопка)", "",
                "flow.confirm");
            AddPad("Траектории", "Отмена / Esc", "B (правая кнопка)", "", "flow.cancel");
            AddPad("Траектории", "Красный лазер (выбор точки)", "X (левая кнопка)", "", "point.select");
            AddPad("Траектории", "Зелёный лазер (выбор траектории)", "Y (верхняя кнопка)", "",
                "path.select");
            AddPad("Траектории", "Вход/подтверждение режима перемещения точки", "LT", "", "flow.enter");
            AddPad("Траектории", "Запуск траектории (то же, что ЛКМ зелёным)", "RT", "", "flow.confirm");
            AddPad("Робот", "Аварийная остановка", "LB", "", "estop");
            AddPad("Робот", "Домой (preset-поза)", "RB", "", "pose.goto");
            AddPad("Интерфейс", "Палитра команд", "Start", "", "ui.palette");
            AddPad("Интерфейс", "Список горячих клавиш", "Select (Back)", "", "help.hotkeys");
            AddPad("Интерфейс", "Движение камеры (как ходьба)", "левый стик", "");
            AddPad("Интерфейс", "Поворот камеры (обзор)", "правый стик", "");
            AddPad("Интерфейс", "Ручной режим стиков (устаревший тумблер)", "R3",
                "оставлен для совместимости; стики работают и без него");

            // ---------------------------------------------------------- VR/MR — стандарт Quest/PICO (27.10.2026)
            AddXr("VR / MR", "Подтвердить / выбрать", "A", "то же, что курок вне интерфейса");
            AddXr("VR / MR", "Назад / отмена / закрыть окно", "B", "эквивалент Esc");
            AddXr("VR / MR", "Стартовое меню (открыть/закрыть)", "X", "");
            AddXr("VR / MR", "Панель у руки (показать/скрыть)", "Y", "вторичное меню");
            AddXr("VR / MR", "Движение / телепорт", "левый стик", "направление — ход, удержание вверх — телепорт");
            AddXr("VR / MR", "Поворот шагами 30°", "правый стик", "без укачивания (стандарт Quest)");
            AddXr("VR / MR", "Выбор иконки / подтверждение точки", "курок", "арбитраж: над интерфейсом — иконка, вне — точка");
            AddXr("VR / MR", "Захват и перенос окна", "грип", "перенос обеими руками");
            AddXr("VR / MR", "Стартовое меню (запасное)", "нажатие левого стика", "то же, что X");
            return entries;
        }

        /// <summary>Сбросить кэш (например, после переназначения бинда — задел).</summary>
        public static void Invalidate()
        {
            entries = null;
        }

        private static void Add(string group, string action, string keys, string note,
            bool contextual, string commandId = "")
        {
            entries.Add(new KvBindEntry(group, action, keys, note, "Клавиатура", contextual, commandId));
        }

        private static void AddPad(string group, string action, string keys, string note,
            string commandId = "", bool contextual = false)
        {
            entries.Add(new KvBindEntry(group, action, keys, note, "Геймпад", contextual, commandId));
        }

        private static void AddXr(string group, string action, string keys, string note)
        {
            entries.Add(new KvBindEntry(group, action, keys, note, "VR-контроллер", false, ""));
        }

        // ------------------------------------------------------------------ поиск/конфликты

        /// <summary>Бинды, подходящие под строку поиска (по действию, клавише, пояснению).</summary>
        public static List<KvBindEntry> Filter(string query)
        {
            List<KvBindEntry> all = All();
            if (string.IsNullOrEmpty(query)) return new List<KvBindEntry>(all);

            string q = query.Trim().ToLowerInvariant();
            List<KvBindEntry> result = new List<KvBindEntry>();
            foreach (KvBindEntry e in all)
            {
                if (e == null) continue;
                if (Contains(e.Action, q) || Contains(e.Keys, q) || Contains(e.Group, q) ||
                    Contains(e.Note, q) || Contains(e.Device, q))
                    result.Add(e);
            }
            return result;
        }

        private static bool Contains(string text, string lower)
        {
            return !string.IsNullOrEmpty(text) && text.ToLowerInvariant().Contains(lower);
        }

        /// <summary>
        /// КОНФЛИКТЫ БИНДОВ (ЭТАП 7): одна и та же клавиша у двух РАЗНЫХ действий внутри
        /// одного устройства. Контекстные бинды (Esc в разных режимах) конфликтом не считаются.
        /// Возвращает множество «устройство|клавиша», по которым есть конфликт.
        /// </summary>
        public static HashSet<string> Conflicts()
        {
            Dictionary<string, List<KvBindEntry>> map = new Dictionary<string, List<KvBindEntry>>();
            foreach (KvBindEntry e in All())
            {
                if (e == null || e.Contextual || string.IsNullOrEmpty(e.Keys) || e.Keys == "—") continue;
                foreach (string key in Split(e.Keys))
                {
                    string id = e.Device + "|" + key;
                    List<KvBindEntry> list;
                    if (!map.TryGetValue(id, out list))
                    {
                        list = new List<KvBindEntry>();
                        map[id] = list;
                    }
                    list.Add(e);
                }
            }

            HashSet<string> conflicts = new HashSet<string>();
            foreach (KeyValuePair<string, List<KvBindEntry>> pair in map)
            {
                HashSet<string> actions = new HashSet<string>();
                foreach (KvBindEntry e in pair.Value) actions.Add(e.Action);
                if (actions.Count > 1) conflicts.Add(pair.Key);
            }
            return conflicts;
        }

        /// <summary>Конфликтует ли конкретный бинд (по ключу «устройство|клавиша»).</summary>
        public static bool IsConflict(KvBindEntry entry, HashSet<string> conflicts)
        {
            if (entry == null || conflicts == null || entry.Contextual) return false;
            if (string.IsNullOrEmpty(entry.Keys) || entry.Keys == "—") return false;
            foreach (string key in Split(entry.Keys))
                if (conflicts.Contains(entry.Device + "|" + key)) return true;
            return false;
        }

        /// <summary>Клавиши строки («Q/E, W/S, A/D» → q/e, w/s, a/d).</summary>
        public static List<string> Split(string keys)
        {
            List<string> result = new List<string>();
            if (string.IsNullOrEmpty(keys)) return result;
            string[] parts = keys.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                string key = part.Trim().ToLowerInvariant();
                // «A / D», «← / →» — это одно действие на двух клавишах, берём обе.
                string[] halves = key.Split('/');
                foreach (string half in halves)
                {
                    string k = half.Trim();
                    if (k.Length > 0) result.Add(k);
                }
            }
            return result;
        }

        /// <summary>Разделы в порядке появления (для группировки в окне).</summary>
        public static List<string> Groups()
        {
            List<string> groups = new List<string>();
            foreach (KvBindEntry e in All())
            {
                if (e == null || string.IsNullOrEmpty(e.Group)) continue;
                if (!groups.Contains(e.Group)) groups.Add(e.Group);
            }
            return groups;
        }

        /// <summary>Текстовый дамп (диагностика).</summary>
        public static string Dump()
        {
            StringBuilder sb = new StringBuilder();
            HashSet<string> conflicts = Conflicts();
            sb.Append("Биндов: ").Append(All().Count).Append(" · конфликтов: ").Append(conflicts.Count);
            foreach (KvBindEntry e in All())
                sb.Append("\n  [").Append(e.Device).Append("] ").Append(e.Keys).Append(" — ")
                  .Append(e.Action).Append(IsConflict(e, conflicts) ? " [КОНФЛИКТ]" : "");
            return sb.ToString();
        }
    }
}
