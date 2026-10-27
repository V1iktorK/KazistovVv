using System;
using System.Collections.Generic;

namespace KazistovVvUI
{
    /// <summary>
    /// ОДИН ПОИСК ПО КОМАНДАМ ДЛЯ КОМПЬЮТЕРА И ШЛЕМА (сессия §41, задание [S1]: «нужен экран "Поиск"
    /// с вводом строки и выполнением найденной команды»).
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ КЛАСС, А НЕ ВТОРОЙ ПОИСК В ШЛЕМЕ. Владелец не примет положения, когда в шлеме
    /// находится одно, а на компьютере другое ([K4]: один источник правды). Палитра Ctrl+P
    /// (`KvCommandPalette`) и экран «Поиск» в шлеме зовут ОДНУ И ТУ ЖЕ функцию — разойтись им негде.
    /// Поведение при этом сохранено прежнее, до последней запятой:
    ///
    ///   1. пустой запрос — все команды в порядке регистрации (недавние подставляет палитра сама,
    ///      потому что они живут в её настройках, а не в поиске);
    ///   2. запрос приводится к нижнему регистру и обрезается по краям;
    ///   3. совпадение — подстрока в ЛЮБОМ из семи полей: Id, LocalizedTitle, Title,
    ///      LocalizedDescription, Hotkey, переводы всех языков («cmd.&lt;id&gt;»), название группы;
    ///   4. команды, чей LocalizedTitle НАЧИНАЕТСЯ с запроса, идут первыми и отсортированы по
    ///      заголовку; остальные — в порядке регистрации;
    ///   5. на экран показывается не больше `MaxRows` строк.
    ///
    /// ПОИСК НИЧЕГО НЕ ВЫПОЛНЯЕТ. Выполнение — единственный вызов `KvCommands.Invoke(id)` в месте
    /// нажатия, как и на компьютере.
    /// </summary>
    public static class KvCommandSearch
    {
        /// <summary>Сколько строк помещается на экран (как в палитре компьютера).</summary>
        public const int MaxRows = 12;

        /// <summary>
        /// СОВПАДЕНИЕ ПО ЗАПРОСУ. Ровно те семь полей, что ищет палитра компьютера: пропустишь
        /// одно — в шлеме «не находится» команда, которая на компьютере находится.
        /// </summary>
        public static bool Matches(KvCommand command, string query)
        {
            if (command == null) return false;

            string q = Query(query);
            if (q.Length == 0) return true;

            if (command.Id != null && command.Id.ToLowerInvariant().Contains(q)) return true;
            if (command.LocalizedTitle != null && command.LocalizedTitle.ToLowerInvariant().Contains(q)) return true;
            if (command.Title != null && command.Title.ToLowerInvariant().Contains(q)) return true;
            if (command.LocalizedDescription != null &&
                command.LocalizedDescription.ToLowerInvariant().Contains(q)) return true;
            if (command.Hotkey != null && command.Hotkey.ToLowerInvariant().Contains(q)) return true;

            // Поиск и на других языках (включая английский) — как в палитре ЭТАПА 5.
            string all = KvLoc.AllLanguages("cmd." + command.Id);
            if (!string.IsNullOrEmpty(all) && all.ToLowerInvariant().Contains(q)) return true;

            string group = KvToolbarGroups.GroupTitleOf(command.Id);
            if (!string.IsNullOrEmpty(group) && group.ToLowerInvariant().Contains(q)) return true;

            return false;
        }

        /// <summary>Заголовок команды начинается с запроса — такие показываются первыми.</summary>
        public static bool StartsTitle(KvCommand command, string query)
        {
            if (command == null) return false;
            string q = Query(query);
            return command.LocalizedTitle != null &&
                   command.LocalizedTitle.ToLowerInvariant().StartsWith(q);
        }

        /// <summary>
        /// ОТФИЛЬТРОВАТЬ: совпадения, «начинается с» — первыми. Порядок и состав совпадают с палитрой
        /// компьютера, потому что это один и тот же код.
        ///
        /// Принимает `IEnumerable`, а не `IList`: реестр команд отдаёт `IReadOnlyList` (`KvCommands.All`),
        /// и приведение к списку здесь стоило бы копии на каждый символ запроса.
        /// </summary>
        public static List<KvCommand> Filter(IEnumerable<KvCommand> commands, string query)
        {
            var result = new List<KvCommand>();
            if (commands == null) return result;

            string q = Query(query);

            if (q.Length == 0)
            {
                foreach (KvCommand command in commands)
                    if (command != null) result.Add(command);
                return result;
            }

            var starts = new List<KvCommand>();
            var rest = new List<KvCommand>();

            foreach (KvCommand command in commands)
            {
                if (command == null) continue;
                if (!Matches(command, q)) continue;

                if (StartsTitle(command, q)) starts.Add(command);
                else rest.Add(command);
            }

            starts.Sort(delegate (KvCommand a, KvCommand b)
            {
                return string.Compare(a.LocalizedTitle, b.LocalizedTitle,
                    StringComparison.CurrentCultureIgnoreCase);
            });

            result.AddRange(starts);
            result.AddRange(rest);
            return result;
        }

        /// <summary>
        /// МОЖНО ЛИ ВЫПОЛНЯТЬ. Недоступная команда (`Stub` или `IsEnabled` вернул false) не
        /// выполняется, и это называется вслух — как на компьютере («Команда недоступна»).
        /// </summary>
        public static bool CanExecute(KvCommand command)
        {
            return command != null && command.Enabled;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА ПОИСКА — из задания [S2] дословно:
        ///   [KvXr] ПОИСК: «пауза» → найдено 3
        /// </summary>
        public static string SearchLogLine(string query, int found)
        {
            return "[KvXr] ПОИСК: «" + (query == null ? "" : query.Trim()) + "» → найдено " + found;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА ВЫПОЛНЕНИЯ — из задания [S2] дословно:
        ///   [KvXr] ПОИСК: «пауза» → найдено 3 · выполнена «Запуск / пауза»
        /// </summary>
        public static string ExecuteLogLine(string query, int found, string title)
        {
            return SearchLogLine(query, found) + " · выполнена «" + (title == null ? "" : title) + "»";
        }

        /// <summary>Запрос в том виде, в каком его сравнивают: обрезан по краям и в нижнем регистре.</summary>
        public static string Query(string query)
        {
            return string.IsNullOrEmpty(query) ? "" : query.Trim().ToLowerInvariant();
        }
    }
}
