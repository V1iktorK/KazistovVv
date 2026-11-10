using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Одна команда интерфейса: иконка, подсказка, горячая клавиша, действие
    /// и функции состояния. Тулбар, меню и «горячие» кнопки рисуются ИЗ РЕЕСТРА
    /// команд, поэтому новая кнопка добавляется регистрацией команды (плюс её id
    /// в раскладке тулбара) — без правок самих панелей.
    /// </summary>
    public class KvCommand
    {
        /// <summary>Уникальный id («point.select», «view.workspace»).</summary>
        public string Id;
        /// <summary>Заголовок для подсказки и меню.</summary>
        public string Title;
        /// <summary>Пояснение (вторая строка подсказки).</summary>
        public string Description = "";
        /// <summary>Горячая клавиша/кнопка («Z», «ЛКМ», «TAB»).</summary>
        public string Hotkey = "";
        /// <summary>Id иконки из <see cref="KvIcons"/>.</summary>
        public string Icon = "";
        /// <summary>Идентификатор иконки в состоянии «включено» (тумблеры).</summary>
        public string IconChecked = "";
        /// <summary>Действие команды (null — заглушка).</summary>
        public Action Execute;
        /// <summary>Состояние тумблера (null — обычная кнопка).</summary>
        public Func<bool> IsChecked;
        /// <summary>Доступность (null — всегда доступна).</summary>
        public Func<bool> IsEnabled;
        /// <summary>Оттенок иконки в состоянии «включено» (красный/зелёный лазер и т.п.).</summary>
        public Func<Color?> CheckedTint;
        /// <summary>Путь в строке меню («Вид/Зона достижимости»), пусто — только тулбар.</summary>
        public string MenuPath = "";
        /// <summary>Функционала нет — кнопка показывается недоступной, в подсказке «в разработке».</summary>
        public bool Stub;

        public bool Enabled { get { return !Stub && (IsEnabled == null || IsEnabled()); } }
        public bool Checked { get { return IsChecked != null && IsChecked(); } }

        /// <summary>
        /// Заголовок с учётом языка интерфейса (ЭТАП 2): ключ словаря «cmd.&lt;id&gt;»,
        /// а если перевода нет — исходный русский текст этой команды.
        /// </summary>
        public string LocalizedTitle { get { return KvLoc.Cmd(Id, Title); } }

        /// <summary>Пояснение с учётом языка интерфейса (ключ «cmd.&lt;id&gt;.desc»).</summary>
        public string LocalizedDescription { get { return KvLoc.CmdDesc(Id, Description); } }

        public string TooltipBody
        {
            get
            {
                string stub = KvLoc.T("common.stub", "в разработке");
                if (Stub) return string.IsNullOrEmpty(LocalizedDescription)
                    ? stub : LocalizedDescription + " · " + stub;
                return LocalizedDescription;
            }
        }
    }

    /// <summary>Реестр команд интерфейса (расширяемость без правок панелей).</summary>
    public static class KvCommands
    {
        private static readonly List<KvCommand> ordered = new List<KvCommand>();
        private static readonly Dictionary<string, KvCommand> byId =
            new Dictionary<string, KvCommand>();

        /// <summary>Все команды в порядке регистрации.</summary>
        public static IReadOnlyList<KvCommand> All { get { return ordered; } }

        /// <summary>
        /// СКОЛЬКО КОМАНД ФАКТИЧЕСКИ ВЫПОЛНЕНО за всё время работы (счётчик самопрогона UI).
        ///
        /// ЧТО БЫЛО. Самопрогон интерфейса (`KvUiSweepRuntime`) должен доказать, что нажатие
        /// кнопки выполнило РАБОТУ, а не только перерисовало её. Проверять «по журналу» нельзя:
        /// строки пишут и заглушки. Нужен ЧИСЛОВОЙ признак, который меняется только тогда,
        /// когда команда действительно дошла до обработчика.
        /// ЧЕМ ПЛОХО БЫЛО БЕЗ НЕГО. Признак «сработало» приходилось собирать из частных полей
        /// (видимость панели, режим, язык) — и кнопка, которая реально что-то делает, но не
        /// меняет ни одно из них (например «включить запись»), выглядела как «без эффекта».
        /// ЧТО СТАЛО. Счётчик живёт в ОДНОМ месте — там, где команда выполняется, — поэтому
        /// забыть его нельзя. Он ТОЛЬКО ЧИТАЕТСЯ снаружи и не влияет на поведение интерфейса.
        /// </summary>
        public static int ExecutedCount { get; private set; }

        /// <summary>Зарегистрировать команду (повторная регистрация с тем же id заменяет её).</summary>
        public static KvCommand Register(KvCommand command)
        {
            if (command == null || string.IsNullOrEmpty(command.Id)) return null;
            KvCommand existing;
            if (byId.TryGetValue(command.Id, out existing))
            {
                int index = ordered.IndexOf(existing);
                if (index >= 0) ordered[index] = command;
                byId[command.Id] = command;
                return command;
            }
            ordered.Add(command);
            byId[command.Id] = command;
            return command;
        }

        /// <summary>Зарегистрировать команду по короткому набору полей.</summary>
        public static KvCommand Register(string id, string title, string description, string hotkey,
            string icon, Action execute, string menuPath = "", bool stub = false)
        {
            return Register(new KvCommand
            {
                Id = id,
                Title = title,
                Description = description,
                Hotkey = hotkey,
                Icon = icon,
                Execute = execute,
                MenuPath = menuPath,
                Stub = stub
            });
        }

        public static KvCommand Get(string id)
        {
            KvCommand c;
            return byId.TryGetValue(id, out c) ? c : null;
        }

        /// <summary>Выполнить команду (недоступные и заглушки не выполняются).</summary>
        public static bool Invoke(string id)
        {
            KvCommand c = Get(id);
            if (c == null) return false;
            if (!c.Enabled)
            {
                Debug.Log("[KazistovVv] Команда «" + c.Title + "» недоступна" +
                          (c.Stub ? " (в разработке)." : "."));
                return false;
            }
            if (c.Execute != null)
            {
                c.Execute();
                // СЧЁТЧИК ВЫПОЛНЕННЫХ — признак «кнопка сделала работу» для самопрогона UI.
                // Считаем ПОСЛЕ вызова и только при НЕПУСТОМ обработчике: команда без обработчика
                // работу не выполнила, даже если формально «доступна», и засчитывать её успехом
                // значило бы greenить ровно тот дефект, который самопрогон ищет. Упавшая команда
                // тоже не считается: до счётчика дело не доходит.
                ExecutedCount++;
            }
            return true;
        }

        public static bool IsChecked(string id)
        {
            KvCommand c = Get(id);
            return c != null && c.Checked;
        }

        public static bool IsEnabled(string id)
        {
            KvCommand c = Get(id);
            return c != null && c.Enabled;
        }

        /// <summary>Очистить реестр (например, перед повторной регистрацией на смене сцены).</summary>
        public static void Clear()
        {
            ordered.Clear();
            byId.Clear();
        }

        /// <summary>Текстовый дамп реестра — для диагностики и отчёта.</summary>
        public static string Dump()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("Команд: ").Append(ordered.Count);
            foreach (KvCommand c in ordered)
            {
                sb.Append("\n  ").Append(c.Id)
                  .Append(" · ").Append(c.Title)
                  .Append(c.Stub ? " [заглушка]" : "")
                  .Append(c.Enabled ? "" : " [недоступна]")
                  .Append(c.Checked ? " [вкл]" : "");
            }
            return sb.ToString();
        }
    }
}
