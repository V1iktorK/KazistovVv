using System;
using System.Collections.Generic;

namespace KazistovVvXr
{
    /// <summary>
    /// РЕЕСТР ОКОН И ПОЯС (решения владельца [D21] «свернуть/закрыть» и [D22] «скрытые окна — на пояс»).
    ///
    /// ЧТО ЭТО. Единое место, где известно про ВСЕ окна интерфейса: как их показать, скрыть
    /// и свернуть, в каком они состоянии и какие окна сейчас лежат на поясе. Без такого реестра
    /// «пояс» пришлось бы собирать по сцене поиском компонентов (а окна проекта живут на разных
    /// канвасах и создаются кодом), и любое новое окно пришлось бы подключать в трёх местах.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: ни GameObject, ни Canvas, ни кадра. Только идентификаторы, состояния
    /// и ДЕЙСТВИЯ, которые передаёт само окно при регистрации. Поэтому правила пояса проверяются
    /// тестами без сцены (`KvWindowBeltTests`), а построение кнопок остаётся в интерфейсе.
    ///
    /// ПРАВИЛА:
    ///   • «закрыть» (✕) — окно скрывается и УХОДИТ НА ПОЯС: по [D22] скрытое окно не исчезает
    ///     бесследно, его можно достать;
    ///   • «взять с пояса» — окно показывается РАЗВЁРНУТЫМ и снимается с пояса;
    ///   • если на поясе нет места — окно НЕ прячется (иначе оператор решит, что закрыл его,
    ///     а найти не сможет) и вызывающий код обязан сказать «на поясе нет места»;
    ///   • повторные вызовы ничего не ломают: плитка не удваивается, состояние не «прыгает».
    /// </summary>
    public static class KvWindowBelt
    {
        /// <summary>Действие окна без параметров: показать, скрыть, свернуть.</summary>
        public delegate void WindowAction();

        /// <summary>Запись об окне: кто оно, что умеет и в каком состоянии.</summary>
        public class Entry
        {
            public string Id;
            public object Window;
            public WindowAction Show;
            public WindowAction Hide;
            public WindowAction Collapse;
            public KvWindowState State = KvWindowState.Expanded;

            /// <summary>Человеческая подпись окна (для плитки пояса и журнала).</summary>
            public string Title;

            public override string ToString()
            {
                return Id + " (" + State + (string.IsNullOrEmpty(Title) ? "" : ", " + Title) + ")";
            }
        }

        private static readonly List<Entry> entries = new List<Entry>();
        private static readonly KvXrBelt belt = new KvXrBelt();

        /// <summary>Пояс: сколько окон убрано и сколько помещается.</summary>
        public static KvXrBelt Belt { get { return belt; } }

        /// <summary>Сколько окон зарегистрировано.</summary>
        public static int Count { get { return entries.Count; } }

        /// <summary>Записи (только чтение) — для интерфейса пояса и отчётов.</summary>
        public static IList<Entry> Entries { get { return entries.AsReadOnly(); } }

        /// <summary>Зарегистрировать окно. Повторная регистрация обновляет действия, а не плодит записи.</summary>
        public static bool Register(string id, object window, WindowAction show, WindowAction hide,
                                    WindowAction collapse, string title = null)
        {
            if (string.IsNullOrEmpty(id)) return false;

            Entry existing = Find(id);
            if (existing != null)
            {
                existing.Window = window;
                existing.Show = show;
                existing.Hide = hide;
                existing.Collapse = collapse;
                if (!string.IsNullOrEmpty(title)) existing.Title = title;
                return true;
            }

            entries.Add(new Entry
            {
                Id = id,
                Window = window,
                Show = show,
                Hide = hide,
                Collapse = collapse,
                Title = title,
                State = KvWindowState.Expanded
            });
            return true;
        }

        /// <summary>Снять окно с учёта (окно уничтожено). Заодно убирает его с пояса.</summary>
        public static bool Unregister(string id)
        {
            Entry entry = Find(id);
            if (entry == null) return false;
            belt.Take(id);
            entries.Remove(entry);
            return true;
        }

        /// <summary>Найти запись по идентификатору.</summary>
        public static Entry Find(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].Id == id) return entries[i];
            return null;
        }

        /// <summary>Состояние окна. Незарегистрированное окно считается развёрнутым.</summary>
        public static KvWindowState StateOf(string id)
        {
            Entry entry = Find(id);
            return entry != null ? entry.State : KvWindowState.Expanded;
        }

        /// <summary>
        /// СКРЫТЬ ОКНО И ПОЛОЖИТЬ ЕГО НА ПОЯС ([D21] + [D22]).
        /// Возвращает false, если места на поясе нет: в этом случае окно ОСТАЁТСЯ видимым,
        /// а вызывающий код обязан сказать оператору «на поясе нет места».
        /// </summary>
        public static bool SendToBelt(string id)
        {
            Entry entry = Find(id);
            if (entry == null) return false;
            if (!belt.HasRoom && !belt.Contains(id)) return false;   // окно не теряем

            entry.State = KvWindowStates.SendToBelt(entry.State);
            belt.Put(id);
            if (entry.Hide != null) entry.Hide();
            return true;
        }

        /// <summary>ДОСТАТЬ ОКНО С ПОЯСА: показать развёрнутым и снять плитку.</summary>
        public static bool TakeFromBelt(string id)
        {
            Entry entry = Find(id);
            if (entry == null) return false;

            entry.State = KvWindowStates.TakeFromBelt(entry.State);
            belt.Take(id);
            if (entry.Show != null) entry.Show();
            return true;
        }

        /// <summary>Окно показали другим путём (тулбар, горячая клавиша) — снять с пояса.</summary>
        public static void NotifyShown(string id)
        {
            Entry entry = Find(id);
            if (entry == null) return;
            entry.State = KvWindowStates.Expand(entry.State);
            belt.Take(id);
        }

        /// <summary>Окно свернули или развернули (чёрточка) — обновить состояние.</summary>
        public static void NotifyCollapsed(string id, bool collapsed)
        {
            Entry entry = Find(id);
            if (entry == null) return;
            entry.State = collapsed ? KvWindowStates.Collapse(entry.State) : KvWindowStates.Expand(entry.State);
        }

        /// <summary>Список идентификаторов, лежащих на поясе (в порядке укладки).</summary>
        public static string[] OnBelt()
        {
            var result = new List<string>();
            IList<string> items = belt.Items;
            for (int i = 0; i < items.Count; i++) result.Add(items[i]);
            return result.ToArray();
        }

        /// <summary>Строка для журнала: сколько окон зарегистрировано и что лежит на поясе.</summary>
        public static string Describe()
        {
            return "окон зарегистрировано " + entries.Count + " · " + belt.Describe();
        }

        /// <summary>Очистить реестр (перезагрузка сцены, тесты).</summary>
        public static void Clear()
        {
            entries.Clear();
            belt.Clear();
        }
    }
}
