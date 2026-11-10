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
                // ОТКАЗ ТОЖЕ ЕСТЬ СОБЫТИЕ: «нажал, ничего не произошло» — самый частый вопрос
                // разбора записи. Причина (`IsEnabled` = ложь или заглушка) пишется в журнал,
                // чтобы её не пришлось угадывать по отсутствию строки.
                KvUiJournal.Command(id, c.Title, c.Stub ? "недоступна: в разработке"
                                                       : "недоступна: сейчас нельзя");
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

                // ЗАПИСЬ НАЖАТИЯ (12.11.2026). ЧТО БЫЛО: выполненная команда оставляла след ТОЛЬКО
                // в консоли Unity. Разбор записи с устройства показал обратное: за 18 секунд
                // самопрогон сделал 140 нажатий, а в файле `actions_<дата>.log` о них не было ни
                // строки. ЧЕМ ПЛОХО: нельзя ни подтвердить, ни опровергнуть, что кнопка нажата и
                // что она сработала. ЧТО СТАЛО: каждое выполнение даёт строку с ИДЕНТИФИКАТОРОМ
                // команды и заголовком. Пишем ПОСЛЕ успешного вызова, поэтому строка означает
                // «дошло до обработчика», а не «было нажатие».
                KvUiJournal.Command(id, c.Title, "выполнена");
            }
            else
            {
                // ДОСТУПНАЯ КОМАНДА БЕЗ ОБРАБОТЧИКА — не успех (см. пояснение к счётчику выше),
                // но и не тишина: в журнале она обязана быть видна как «нечего выполнять».
                KvUiJournal.Command(id, c.Title, "без обработчика: работа не выполнена");
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

    /// <summary>
    /// МОСТ «ИНТЕРФЕЙС → ЖУРНАЛ ДЕЙСТВИЙ» (создано 12.11.2026, разбор записи с устройства).
    ///
    /// ЧТО БЫЛО. `KvCommands.Invoke` выполнял команду и оставлял след ТОЛЬКО в консоли Unity
    /// (`Debug.Log`). Консоль на шлеме не читается: единственная запись, которую можно забрать
    /// через adb, — файл `Logs/actions_&lt;дата&gt;.log`, и в нём нажатий не было ВООБЩЕ.
    /// Разбор подтвердил это числами: за 18 секунд самопрогон интерфейса сделал 140 нажатий,
    /// а в файле за весь сеанс оказалось 14 строк, из них 11 — один и тот же повтор состояния.
    ///
    /// ЧЕМ ПЛОХО. Запись перестала отвечать на главный вопрос разбора: «оператор нажал — и что
    /// произошло». Есть повторяющийся шум и нет событий.
    ///
    /// ЧТО СТАЛО. Одно место, через которое интерфейс пишет события в журнал. Зачем отдельный
    /// класс, а не прямой вызов `KvActionLog`:
    ///   • у реестра команд НЕТ права падать из-за журнала. Если журнал недоступен (диск занят,
    ///     папка не создана, идёт выход), нажатие всё равно обязано выполниться — поэтому здесь
    ///     стоит защита от исключения, и она одна на всех;
    ///   • точка входа одна, значит «нажатие не пишется» больше нельзя забыть в одном месте из
    ///     пяти (тулбар, меню, палитра, геймпад, панель шлема — все они идут через `Invoke`).
    ///
    /// ЧЕГО ЭТОТ КЛАСС НЕ ДЕЛАЕТ: не решает, что писать. Формат строки, отсечение повторов и
    /// ротацию файла решает `KvActionLog` — второй копии этих правил здесь нет.
    ///
    /// ПОЧЕМУ В ЭТОМ ЖЕ ФАЙЛЕ. Отдельный файл — это ещё и `.meta`, и строка в сгенерированном
    /// `.csproj`; класс из пяти строк такого хозяйства не стоит. Сборка одна
    /// (`KazistovVv.Runtime`), поэтому ограничений на расположение нет.
    /// </summary>
    public static class KvUiJournal
    {
        /// <summary>
        /// НАЖАТИЕ/ПОПЫТКА КОМАНДЫ ИНТЕРФЕЙСА: идентификатор + заголовок + результат.
        ///
        /// Пишется ВСЕГДА (кроме случая, когда журнала ещё нет) — отсечение повторов делает сам
        /// журнал: событие «одно и то же» он узнаёт по модулю, действию и значению и не пускает
        /// его чаще раза в 30 секунд, копя счётчик повторов.
        /// </summary>
        public static void Command(string id, string title, string result)
        {
            KazistovVvFeatures.KvActionLog log = SafeInstance();
            if (log == null) return;
            try
            {
                log.Command(id, title, result);
            }
            catch (Exception e)
            {
                // Журнал не имеет права уронить нажатие кнопки. Говорим один раз — и работаем дальше.
                Debug.LogWarning("[KvUi] запись нажатия не удалась (" + id + "): " + e.Message);
            }
        }

        /// <summary>
        /// ЖУРНАЛ, ЕСЛИ ОН ЕСТЬ. Отдельная защита: `KvActionLog.Instance` создаёт журнал при первом
        /// обращении, а создание трогает диск (`FeatureStorage.LogsDir`). На выходе из приложения
        /// диск уже может быть недоступен — и тогда исключение вышло бы из обработчика нажатия.
        /// </summary>
        private static KazistovVvFeatures.KvActionLog SafeInstance()
        {
            try
            {
                return KazistovVvFeatures.KvActionLog.Instance;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvUi] журнал действий недоступен: " + e.Message);
                return null;
            }
        }
    }
}
