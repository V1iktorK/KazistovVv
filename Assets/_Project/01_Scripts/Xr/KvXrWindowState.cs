using System.Collections.Generic;

namespace KazistovVvXr
{
    /// <summary>Состояние окна в шлеме (решения владельца [D21] «закрывать и сворачивать» и [D22] «пояс»).</summary>
    public enum KvWindowState
    {
        /// <summary>РАЗВЁРНУТО — окно видно полностью.</summary>
        Expanded = 0,

        /// <summary>СВЁРНУТО (чёрточка) — окно остаётся на своём месте, содержимое скрыто.</summary>
        Collapsed = 1,

        /// <summary>НА ПОЯСЕ — окно убрано с панели и ждёт на плитке у бёдер ([D22]).</summary>
        OnBelt = 2,

        /// <summary>ЗАКРЫТО (квадратик) — окно снято с показа, но помнит, где было.</summary>
        Closed = 3
    }

    /// <summary>
    /// ЧТО ПРОИСХОДИТ С ОКНОМ ОТ НАЖАТИЯ КНОПОК ([D21]) И КАК ОНО ВОЗВРАЩАЕТСЯ С ПОЯСА ([D22]).
    ///
    /// ПОЧЕМУ ЧИСТАЯ ЛОГИКА, А НЕ ПОЛЯ В ОКНЕ. Правила переходов — это то, что обязано быть
    /// ОДИНАКОВЫМ для всех окон (и на компьютере, и в шлеме), и то, что нельзя проверить глазами
    /// в каждом окне по отдельности. Здесь только переходы и их свойства; сами кнопки рисует
    /// интерфейс, а «пояс» хранит список убранных окон (см. <see cref="KvXrBelt"/>).
    ///
    /// ПРАВИЛА (приняты по решениям владельца):
    ///   • «свернуть» и «развернуть» переключают Expanded ↔ Collapsed;
    ///   • «закрыть» убирает окно с показа (Closed), но НЕ забывает его: вернуть можно;
    ///   • «на пояс» — это отдельное состояние: окно уходит на плитку у бёдер;
    ///   • из состояния «на поясе» окно возвращается только РАЗВЁРНУТЫМ (взять плитку — значит
    ///     открыть окно, а не оставить его свёрнутым);
    ///   • повторное нажатие той же кнопки ничего не ломает (переходы идемпотентны).
    /// </summary>
    public static class KvWindowStates
    {
        /// <summary>Свернуть: развёрнутое становится свёрнутым, закрытое — возвращается свёрнутым.</summary>
        public static KvWindowState Collapse(KvWindowState current)
        {
            if (current == KvWindowState.Closed) return KvWindowState.Collapsed;
            if (current == KvWindowState.OnBelt) return KvWindowState.Collapsed;
            return KvWindowState.Collapsed;
        }

        /// <summary>Развернуть: любое «не развёрнутое» состояние становится развёрнутым.</summary>
        public static KvWindowState Expand(KvWindowState current)
        {
            return KvWindowState.Expanded;
        }

        /// <summary>Закрыть: окно уходит с показа (и с пояса — закрытое на поясе не висит).</summary>
        public static KvWindowState Close(KvWindowState current)
        {
            return KvWindowState.Closed;
        }

        /// <summary>Убрать на пояс ([D22]): скрытое окно уходит на плитку, а не исчезает.</summary>
        public static KvWindowState SendToBelt(KvWindowState current)
        {
            return KvWindowState.OnBelt;
        }

        /// <summary>Взять с пояса: окно возвращается РАЗВЁРНУТЫМ (взяли — значит открыли).</summary>
        public static KvWindowState TakeFromBelt(KvWindowState current)
        {
            return KvWindowState.Expanded;
        }

        /// <summary>Видно ли окно на панели (свёрнутое видно — оно остаётся на месте).</summary>
        public static bool IsVisibleOnPanel(KvWindowState state)
        {
            return state == KvWindowState.Expanded || state == KvWindowState.Collapsed;
        }

        /// <summary>Лежит ли окно на поясе.</summary>
        public static bool IsStoredOnBelt(KvWindowState state)
        {
            return state == KvWindowState.OnBelt;
        }

        /// <summary>Скрыто ли окно с показа (закрыто или на поясе).</summary>
        public static bool IsHidden(KvWindowState state)
        {
            return state == KvWindowState.Closed || state == KvWindowState.OnBelt;
        }

        /// <summary>Название состояния для журнала и подсказок — на 7 языках через `KvLoc`.</summary>
        public static string Label(KvWindowState state)
        {
            switch (state)
            {
                case KvWindowState.Collapsed:
                    return KazistovVvUI.KvLoc.T("xr.window.collapse", "Свернуть");
                case KvWindowState.OnBelt:
                    return KazistovVvUI.KvLoc.T("xr.belt.title", "Пояс");
                case KvWindowState.Closed:
                    return KazistovVvUI.KvLoc.T("xr.window.close", "Закрыть");
                default:
                    return KazistovVvUI.KvLoc.T("xr.window.expanded", "Открыто");
            }
        }
    }

    /// <summary>
    /// ПОЯС: СПИСОК ОКОН, УБРАННЫХ НА ПОЯС ([D22]).
    ///
    /// Зачем отдельный класс: «пояс» — это место хранения, у него есть ВМЕСТИМОСТЬ (сколько плиток
    /// помещается у бёдер) и правило «одно окно — одна плитка». Если окно убирают дважды, плитка
    /// не удваивается; если мест нет — честный отказ, а не молчаливая потеря окна.
    /// </summary>
    public class KvXrBelt
    {
        /// <summary>Сколько окон помещается на пояс по умолчанию.</summary>
        public const int DefaultCapacity = 8;

        private readonly List<string> items = new List<string>();
        private readonly int capacity;

        public KvXrBelt() : this(DefaultCapacity) { }

        public KvXrBelt(int capacity)
        {
            this.capacity = capacity < 1 ? 1 : capacity;
        }

        /// <summary>Сколько плиток помещается.</summary>
        public int Capacity { get { return capacity; } }

        /// <summary>Сколько окон сейчас на поясе.</summary>
        public int Count { get { return items.Count; } }

        /// <summary>Есть ли свободное место.</summary>
        public bool HasRoom { get { return items.Count < capacity; } }

        /// <summary>Список окон на поясе (только чтение — порядок = порядок укладки).</summary>
        public IList<string> Items { get { return items.AsReadOnly(); } }

        /// <summary>Лежит ли это окно на поясе.</summary>
        public bool Contains(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return items.Contains(id);
        }

        /// <summary>
        /// Положить окно на пояс. Возвращает false, если места нет (окно НЕ теряется — вызывающий
        /// код обязан сказать оператору «на поясе нет места»).
        /// </summary>
        public bool Put(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            if (items.Contains(id)) return true;         // уже лежит — повтор не удваивает плитку
            if (!HasRoom) return false;
            items.Add(id);
            return true;
        }

        /// <summary>Взять окно с пояса. false — такого окна на поясе не было.</summary>
        public bool Take(string id)
        {
            if (string.IsNullOrEmpty(id)) return false;
            return items.Remove(id);
        }

        /// <summary>Очистить пояс (например, при перезагрузке сцены).</summary>
        public void Clear()
        {
            items.Clear();
        }

        /// <summary>Подпись плитки по номеру (для интерфейса и журнала).</summary>
        public string ItemAt(int index)
        {
            if (index < 0 || index >= items.Count) return "";
            return items[index];
        }

        /// <summary>Строка для журнала: сколько окон на поясе и сколько всего помещается.</summary>
        public string Describe()
        {
            if (items.Count == 0)
                return string.Format("пояс пуст (мест {0})", capacity);
            return string.Format("на поясе {0} из {1}: {2}",
                items.Count, capacity, string.Join(", ", items.ToArray()));
        }
    }
}
