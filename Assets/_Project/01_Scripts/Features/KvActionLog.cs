using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>Тип события журнала действий (ЭТАП 13 ТЗ) — по нему работает фильтр.</summary>
    public enum KvLogKind
    {
        System,
        Point,
        Trajectory,
        Motion,
        Stop,
        Zone,
        Pose,
        Record,
        Session,
        Scenario,
        Error,
        Ui,
        /// <summary>
        /// Предупреждение (добавлено на этапе 36 ТЗ: уровни «информация / предупреждение / ошибка»).
        /// Значение добавлено В КОНЕЦ списка, поэтому уже сохранённые журналы читаются как раньше.
        /// </summary>
        Warning
    }

    /// <summary>Одна запись журнала.</summary>
    [Serializable]
    public class KvLogEntry
    {
        /// <summary>Секунды от старта приложения (для сортировки).</summary>
        public float time;
        /// <summary>Время суток чч:мм:сс.</summary>
        public string stamp = "";
        /// <summary>Тип события (для фильтра).</summary>
        public KvLogKind kind = KvLogKind.System;
        public string text = "";

        public string Line
        {
            get { return stamp + " [" + KvActionLog.KindLabel(kind) + "] " + text; }
        }
    }

    /// <summary>
    /// ЖУРНАЛ ДЕЙСТВИЙ (ЭТАП 13 ТЗ).
    ///
    /// Прокручиваемый список событий: «точка выбрана», «траектория №3 подтверждена»,
    /// «робот начал движение», «аварийная остановка» и т.п. Хранится в кольцевом буфере
    /// (последние `capacity` записей) И на диске — каждая строка дописывается в файл
    /// сразу (JSON Lines + читаемый текст), поэтому журнал переживает падение/выход.
    ///
    /// Фильтр по типу — свойство <see cref="Filter"/> (null = все типы).
    /// Подписка на новые записи — событие <see cref="Changed"/>.
    ///
    /// ==================== ПОЧЕМУ ЗДЕСЬ ПОЯВИЛИСЬ ПОВТОРЫ, РОТАЦИЯ И ПОЗА (12.11.2026) ====================
    ///
    /// ЧТО БЫЛО. Разбор реальной записи с устройства (`actions_20261110.log`, журналы 12.11.2026)
    /// показал две вещи сразу. Первое: на весь сеанс 14 строк, из них 11 — ОДНО И ТО ЖЕ
    /// («тепловая карта достижимости: выкл», «тепловая карта зазоров: выкл», «Сингулярности:
    /// выключено»), повторяющееся каждые 1–2 секунды. Сервисы сообщают о своём состоянии из
    /// кадрового `Tick`, и каждое такое сообщение ложилось отдельной строкой. Второе: за те же
    /// 18 секунд самопрогон интерфейса сделал 140 нажатий, и о них в записи НЕ БЫЛО НИ СЛОВА.
    /// То есть файл был одновременно полон мусора и пуст там, где нужно доказательство работы.
    ///
    /// ЧЕМ ПЛОХО. За одну сессию накопилось 1,1 ГБ журналов, и разобрать в них что-либо стало
    /// невозможно: полезные строки тонут в повторах, а нужных (кто что нажал) нет вовсе.
    ///
    /// ЧТО СТАЛО (три правила, все — в этом файле):
    ///   1) ОТСЕЧЕНИЕ ПОВТОРОВ: одинаковое событие подряд (тот же модуль + то же действие +
    ///      то же значение) не пишется чаще раза в <see cref="RepeatWindowSeconds"/> секунд;
    ///      повторы считаются, и число печатается хвостом «… ×N»;
    ///   2) РОТАЦИЯ ПО РАЗМЕРУ: файл не растёт без предела — после <see cref="RotateAfterBytes"/>
    ///      создаётся `actions_&lt;дата&gt;_2.log`, старых файлов держится не больше
    ///      <see cref="MaxLogFiles"/>;
    ///   3) РЕАЛЬНЫЕ СОБЫТИЯ: нажатия команд (`KvCommands.Invoke`), выбор модели робота, попытка
    ///      постановки с результатом и смена режима VR/MR пишутся ОТДЕЛЬНЫМ событием
    ///      с идентификатором, а не только строкой в консоли Unity.
    ///
    /// ВАЖНОЕ СЛЕДСТВИЕ ДЛЯ ТЕСТОВ. Правила отсечения, форматирования и ротации вынесены в
    /// ЧИСТЫЕ функции (`ComposeKey`, `ComposeText`, `ShouldWrite`, `PoseDue`, `FormatPoseLine`,
    /// `FormatModeLine`, `FormatCommandLine`, `RotatedPath`, `FilesToDelete`): их можно проверить
    /// без сцены, без шлема и без файловой системы, и именно они проверяются тестами.
    /// ================================================================================================
    /// </summary>
    public class KvActionLog
    {
        /// <summary>Сколько записей держать в памяти.</summary>
        public int capacity = 600;
        /// <summary>Дублировать ли записи в консоль Unity (по умолчанию — только важные).</summary>
        public bool echoToConsole;

        public event Action Changed;

        // ================================================================== ОТСЕЧЕНИЕ ПОВТОРОВ
        //
        // ПОЧЕМУ 30 СЕКУНД. Замер по записи с устройства: один и тот же переключатель сообщал о
        // себе каждые 1–2 секунды (кадровый `Tick` сервиса). 30 секунд — заметно больше любого
        // дребезга и заметно меньше, чем «оператор уже забыл, что делал». Если событие повторилось
        // ПОСЛЕ окна, оно пишется снова — уже с числом повторов, накопленных за окно.

        /// <summary>Не писать одно и то же событие чаще раза в эти секунды.</summary>
        public const float RepeatWindowSeconds = 30f;

        /// <summary>Сколько разных событий помнить для отсечения повторов (кольцевой предел).</summary>
        public const int RepeatMemory = 512;

        // ================================================================== РОТАЦИЯ ПО РАЗМЕРУ
        //
        // ПОЧЕМУ 5 МБ. За сессию накоплено 1,1 ГБ — на порядки больше, чем нужно для разбора
        // (14 полезных строк занимают байты). 5 МБ хватает на сотни тысяч строк: файл остаётся
        // открываемым на телефоне и пересылаемым через adb, а старые части удаляются сами.

        /// <summary>После какого размера файл журнала закрывается и начинается следующий.</summary>
        public const long RotateAfterBytes = 5L * 1024L * 1024L;

        /// <summary>Сколько файлов журнала держать на диске (включая текущий).</summary>
        public const int MaxLogFiles = 3;

        /// <summary>Отсекать ли повторы (по умолчанию — да; причина в шапке класса).</summary>
        public bool suppressRepeats = true;

        /// <summary>Одно запомненное событие: когда писали и сколько повторов подавили.</summary>
        private class KvRepeatState
        {
            public double lastWriteAt;
            public int suppressed;
        }

        private readonly List<KvLogEntry> entries = new List<KvLogEntry>();
        private readonly Dictionary<string, KvRepeatState> repeats =
            new Dictionary<string, KvRepeatState>();
        private KvLogKind? filter;

        private static KvActionLog instance;

        /// <summary>Общий журнал приложения (создаётся при первом обращении).</summary>
        public static KvActionLog Instance
        {
            get
            {
                if (instance == null) instance = new KvActionLog();
                return instance;
            }
        }

        /// <summary>Все записи (новые в конце).</summary>
        public IReadOnlyList<KvLogEntry> Entries { get { return entries; } }

        public int Count { get { return entries.Count; } }

        /// <summary>Активный фильтр (null — показывать все типы).</summary>
        public KvLogKind? Filter
        {
            get { return filter; }
            set { filter = value; Raise(); }
        }

        /// <summary>Файл журнала (текстовый, дописывается построчно).</summary>
        public string FilePath
        {
            get { return CurrentFilePath; }
        }

        /// <summary>
        /// ТЕКУЩИЙ файл записи с учётом ротации: `actions_&lt;дата&gt;.log`, а если он уже дорос до
        /// <see cref="RotateAfterBytes"/> — `actions_&lt;дата&gt;_2.log` и далее.
        ///
        /// ЧТО БЫЛО. Путь всегда был один и тот же (`actions_&lt;дата&gt;.log`), файл только
        /// дописывался. За одну сессию так накопилось 1,1 ГБ — файл нельзя ни открыть на телефоне,
        /// ни переслать, ни разобрать.
        /// ЧЕМ ПЛОХО. Журнал, который невозможно прочитать, не выполняет свою единственную задачу —
        /// быть уликой. Плюс забивается общее хранилище телефона.
        /// ЧТО СТАЛО. Части нумеруются по порядку (`…_2.log`, `…_3.log`), лишние удаляются
        /// (<see cref="FilesToDelete"/>), а выбор номера — чистая функция
        /// <see cref="NextPart"/>: её проверяет тест без единой записи на диск.
        /// </summary>
        public string CurrentFilePath
        {
            get
            {
                string dir = FeatureStorage.LogsDir;
                string stem = "actions_" + DateTime.Now.ToString("yyyyMMdd");
                int part = NextPartOnDisk(dir, stem, RotateAfterBytes, PartScanLimit);
                return Path.Combine(dir, PartFileName(stem, part));
            }
        }

        // ================================================================== ЧИСТЫЕ ПРАВИЛА
        //
        // Всё, что ниже, НЕ трогает ни сцену, ни диск: это формулы, по которым принимаются решения.
        // Именно поэтому их можно проверить тестом — и именно поэтому они здесь, а не размазаны
        // по вызывающему коду (иначе проверялся бы «грепом» текст исходника, а не поведение).

        /// <summary>
        /// КЛЮЧ СОБЫТИЯ ДЛЯ ОТСЕЧЕНИЯ ПОВТОРОВ: «модуль + действие + значение».
        ///
        /// ПОЧЕМУ НЕ ВЕСЬ ТЕКСТ. Строка вида «операция завершена за 1,37 с» меняется в каждом
        /// повторе одной цифрой, и по полному тексту повторы не отсеклись бы НИКОГДА — то есть
        /// дефект «1,1 ГБ повторов» остался бы на месте, а правило выглядело бы работающим.
        /// Поэтому модуль и действие берутся явно, а значение — из текста, но числа из него
        /// вырезаются: «за 1,37 с» и «за 2,04 с» — это одно и то же событие.
        /// </summary>
        public static string ComposeKey(string module, string action, string value)
        {
            return (module ?? "") + "|" + (action ?? "") + "|" + NormalizeValue(value);
        }

        /// <summary>
        /// ЗНАЧЕНИЕ БЕЗ ЧИСЕЛ. Числа внутри значения — это измерение, а не смысл события: две
        /// строки «зазор 0,31 м» и «зазор 0,29 м» описывают одно и то же («зазор мал») и не
        /// должны удваивать журнал. Цифры заменяются на «#», прочие символы остаются.
        /// </summary>
        public static string NormalizeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder sb = new StringBuilder(value.Length);
            bool lastWasDigit = false;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                bool digit = c >= '0' && c <= '9';
                if (digit)
                {
                    if (!lastWasDigit) sb.Append('#');
                }
                else
                {
                    sb.Append(c);
                }
                lastWasDigit = digit;
            }
            return sb.ToString();
        }

        /// <summary>
        /// ПИСАТЬ ЛИ СОБЫТИЕ СЕЙЧАС. ЧИСТАЯ ФУНКЦИЯ — вся логика отсечения в одной строке, поэтому
        /// её видно целиком и можно проверить без файла и без сцены.
        ///
        /// РЕШЕНИЕ:
        ///   • событие другое (ключ не тот) — писать всегда (анти-вакуум: правило «не повторять»
        ///     не имеет права глотать РАЗНЫЕ строки);
        ///   • то же событие, но прошло не меньше <see cref="RepeatWindowSeconds"/> — писать снова;
        ///   • то же событие внутри окна — не писать, только копить счётчик.
        /// </summary>
        /// <param name="lastWriteAt">когда это событие писали в прошлый раз (double.NaN — никогда)</param>
        /// <param name="seconds">текущие часы, секунды</param>
        /// <param name="window">окно отсечения, секунды</param>
        /// <param name="enabled">отсечение включено</param>
        public static bool ShouldWrite(double lastWriteAt, double seconds, float window, bool enabled)
        {
            if (!enabled) return true;
            if (double.IsNaN(lastWriteAt)) return true;
            if (double.IsNaN(seconds)) return true;        // часы «неизвестны» — не глотаем молча
            return seconds - lastWriteAt >= window;
        }

        /// <summary>
        /// ТЕКСТ ЗАПИСИ С УЧЁТОМ ПОДАВЛЕННЫХ ПОВТОРОВ: «… ×N».
        ///
        /// ЧТО БЫЛО. Десять одинаковых строк подряд — это десять одинаковых строк. Ни числа
        /// повторов, ни факта, что их было десять, из журнала не следовало.
        /// ЧЕМ ПЛОХО. По журналу нельзя было отличить «случилось один раз» от «долбит каждую
        /// секунду» — а это разные диагнозы.
        /// ЧТО СТАЛО. Счётчик пишется ХВОСТОМ при следующей записи события и обнуляется: «… ×10».
        /// При нуле хвоста нет — запись выглядит как раньше и не ломает разбор старых строк.
        /// </summary>
        public static string ComposeText(string text, int suppressed)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (suppressed <= 0) return text;
            return text + " … ×" + (suppressed + 1);   // +1 — первый, честно написанный повтор
        }

        /// <summary>Имя файла части журнала: `actions_20261112.log`, `actions_20261112_2.log`.</summary>
        public static string PartFileName(string stem, int part)
        {
            if (part <= 1) return stem + ".log";
            return stem + "_" + part + ".log";
        }

        /// <summary>
        /// НОМЕР ЧАСТИ, В КОТОРУЮ ПИСАТЬ СЕЙЧАС — ПО ГОТОВЫМ РАЗМЕРАМ. ЧИСТАЯ ФУНКЦИЯ.
        ///
        /// ЧТО БЫЛО (исправлено 12.11.2026 после прогона в Unity). Правило выбора части было
        /// записано через функцию «номер → путь», а размер спрашивался у файловой системы внутри.
        /// Проверить это тестом можно было ТОЛЬКО подсунув настоящие файлы: тест подставлял строку
        /// «занято» вместо пути, размер получался 0 байт, и падало с «полная первая часть не
        /// переключила запись на вторую». То есть проверялась не логика, а то, умеет ли тест
        /// обмануть `FileInfo`. Это ловушка P4-3 в чистом виде — тест, который измеряет не то.
        ///
        /// ЧТО СТАЛО. Решение принимается по СПИСКУ РАЗМЕРОВ (`sizeOfPart`), а где их взять —
        /// забота вызывающего. Правило стало по-настоящему чистым: ни `File`, ни `Directory`
        /// внутри нет, и тест передаёт любые числа, не трогая диск.
        /// </summary>
        /// <param name="sizeOfPart">функция «номер части → размер в байтах» (0 — файла нет)</param>
        /// <param name="maxBytes">порог размера; 0 или меньше — ротация выключена</param>
        /// <param name="partLimit">сколько частей максимум (защита от бесконечного цикла)</param>
        public static int NextPart(Func<int, long> sizeOfPart, long maxBytes,
            int partLimit = int.MaxValue)
        {
            if (maxBytes <= 0) return 1;
            if (sizeOfPart == null) return 1;

            int part = 1;
            while (part < partLimit)
            {
                if (sizeOfPart(part) < maxBytes) return part;
                part++;
            }
            return part;
        }

        /// <summary>
        /// НОМЕР ЧАСТИ ПО ФАЙЛАМ НА ДИСКЕ — тонкая обёртка над <see cref="NextPart"/>.
        /// Вся логика решения остаётся в чистой функции; здесь только «спросить размер у файла».
        /// </summary>
        public static int NextPartOnDisk(string dir, string stem, long maxBytes,
            int partLimit = int.MaxValue)
        {
            return NextPart(
                p => FileSizeOrZero(Path.Combine(dir, PartFileName(stem, p))),
                maxBytes, partLimit);
        }

        /// <summary>Размер файла в байтах, 0 — файла нет (или он недоступен).</summary>
        private static long FileSizeOrZero(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path)) return 0L;
                FileInfo info = new FileInfo(path);
                return info.Exists ? info.Length : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        /// <summary>
        /// КАКИЕ ФАЙЛЫ ЖУРНАЛА УДАЛИТЬ, ЧТОБЫ ИХ ОСТАЛОСЬ НЕ БОЛЬШЕ <paramref name="keep"/>.
        /// ЧИСТАЯ ФУНКЦИЯ: принимает готовый список «имя части → размер», ничего не удаляет.
        ///
        /// ПРАВИЛО: части нумеруются по возрастанию; удаляются САМЫЕ СТАРЫЕ (с наименьшим номером),
        /// пока число файлов не станет равно `keep`. Текущий файл (последний) не удаляется никогда:
        /// запись идёт в него прямо сейчас.
        /// </summary>
        public static List<int> FilesToDelete(List<int> existingParts, int keep)
        {
            List<int> result = new List<int>();
            if (existingParts == null || existingParts.Count == 0) return result;
            if (keep < 1) keep = 1;

            List<int> sorted = new List<int>(existingParts);
            sorted.Sort();
            int extra = sorted.Count - keep;
            for (int i = 0; i < extra; i++) result.Add(sorted[i]);
            return result;
        }

        /// <summary>
        /// ФАЙЛ ЗАПИСИ ПОЛОН? ВЫНЕСЕНО ОТДЕЛЬНО, чтобы порог проверялся тестом, а не чтением кода.
        /// </summary>
        public static bool IsFull(long sizeBytes, long maxBytes)
        {
            if (maxBytes <= 0) return false;
            return sizeBytes >= maxBytes;
        }

        // ------------------------------------------------------------------ ПОЗА И ВЗГЛЯД

        /// <summary>
        /// САМАЯ ЧАСТАЯ ЗАПИСЬ ВЗГЛЯДА, Гц. 10 Гц — это не «каждый кадр»: при 72 Гц шлема запись
        /// позы была бы в семь раз чаще нужного и утроила бы объём журнала сама по себе.
        /// </summary>
        public const float PoseMaxHz = 10f;

        /// <summary>Минимальный интервал между записями позы, секунды (следствие <see cref="PoseMaxHz"/>).</summary>
        public const float PoseMinIntervalSeconds = 1f / PoseMaxHz;

        /// <summary>Порог «поза заметно изменилась» по положению головы, метры.</summary>
        public const float PosePositionThresholdMeters = 0.05f;

        /// <summary>Порог «взгляд заметно изменился» по углу, градусы.</summary>
        public const float PoseAngleThresholdDegrees = 5f;

        /// <summary>
        /// ЗАПИСЫВАТЬ ЛИ ПОЗУ В ЭТОМ КАДРЕ. ЧИСТАЯ ФУНКЦИЯ — проверяется тестом без шлема.
        ///
        /// ЧТО БЫЛО. Позы в записи не было вовсе. Наладчику нечем было ответить на вопрос «куда
        /// смотрел оператор, когда робот поехал не туда».
        /// ЧЕМ ПЛОХО. Любая запись позы «каждый кадр» — это 72 строки в секунду: журнал,
        /// который только что починили от повторов, снова превратился бы в 1,1 ГБ.
        /// ЧТО СТАЛО. Пишем не чаще 10 Гц И только при заметном изменении: положение головы
        /// сдвинулось больше <see cref="PosePositionThresholdMeters"/> ЛИБО взгляд повернулся
        /// больше <see cref="PoseAngleThresholdDegrees"/>. Два одинаковых кадра дают ОДНУ строку.
        /// </summary>
        /// <param name="sinceLastSeconds">сколько секунд прошло с прошлой записи позы</param>
        /// <param name="movedMeters">насколько сдвинулась голова с прошлой записи</param>
        /// <param name="turnedDegrees">насколько повернулся взгляд с прошлой записи</param>
        /// <param name="hasPrevious">была ли запись раньше (первая пишется всегда)</param>
        public static bool PoseDue(float sinceLastSeconds, float movedMeters, float turnedDegrees,
            bool hasPrevious)
        {
            if (!hasPrevious) return true;
            // НЕЧИСЛО — это потеря трекинга, а не «изменение»: такую позу не пишем (правило E-21).
            if (float.IsNaN(sinceLastSeconds) || float.IsNaN(movedMeters) ||
                float.IsNaN(turnedDegrees)) return false;
            if (sinceLastSeconds < PoseMinIntervalSeconds) return false;
            return movedMeters >= PosePositionThresholdMeters ||
                   turnedDegrees >= PoseAngleThresholdDegrees;
        }

        /// <summary>
        /// СТРОКА ЗАПИСИ ПОЗЫ — ровно в том виде, который читает наладчик:
        /// `t=12.4 с · позиция (0.12; 1.60; -0.30) · взгляд (0.00; -0.20; 0.98) · FPS 30`.
        ///
        /// ЧИСЛА — ЧЕРЕЗ ТОЧКУ И ВСЕГДА С ДВУМЯ ЗНАКАМИ (`InvariantCulture`): в русской локали
        /// «0,12» в журнале спутается с разделителем списка при разборе скриптами, а «0,1» и «0,10»
        /// в одной колонке читаются как разные величины.
        /// </summary>
        public static string FormatPoseLine(float seconds, Vector3 position, Vector3 gaze, float fps)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return "t=" + seconds.ToString("0.0", c) + " с · позиция (" +
                   Num(position.x, c) + "; " + Num(position.y, c) + "; " + Num(position.z, c) + ") · взгляд (" +
                   Num(gaze.x, c) + "; " + Num(gaze.y, c) + "; " + Num(gaze.z, c) + ") · FPS " +
                   fps.ToString("0", c);
        }

        /// <summary>Число с двумя знаками и точкой — единственный вид числа в строке позы.</summary>
        private static string Num(float value, System.IFormatProvider c)
        {
            // Нечисло приходит при потере трекинга. Печатать «NaN» в журнале бессмысленно: строка
            // должна оставаться разбираемой, поэтому нечисло заменяется нулём и об этом говорит «0.00».
            if (float.IsNaN(value) || float.IsInfinity(value)) return "0.00";
            return value.ToString("0.00", c);
        }

        /// <summary>
        /// СТРОКА СМЕНЫ РЕЖИМА VR/MR — с числами (их требует разбор записи):
        /// `VR → MR · пропускание есть · якоря есть · камера есть · масштаб мира 1.000`.
        /// </summary>
        public static string FormatModeLine(string from, string to, bool passthrough,
            bool anchors, bool camera, float worldScale)
        {
            var c = System.Globalization.CultureInfo.InvariantCulture;
            float scale = worldScale > 0.0001f ? worldScale : 1f;
            return (from ?? "—") + " → " + (to ?? "—") +
                   " · пропускание " + (passthrough ? "есть" : "НЕТ") +
                   " · якоря " + (anchors ? "есть" : "НЕТ") +
                   " · камера " + (camera ? "есть" : "НЕТ") +
                   " · масштаб мира " + scale.ToString("0.000", c);
        }

        /// <summary>
        /// СТРОКА НАЖАТИЯ КОМАНДЫ ИНТЕРФЕЙСА: модуль, идентификатор, заголовок.
        /// `нажатие: view.heatmap «Тепловая карта достижимости» · результат: выполнена`.
        /// </summary>
        public static string FormatCommandLine(string id, string title, string result)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("нажатие: ").Append(id ?? "?");
            if (!string.IsNullOrEmpty(title)) sb.Append(" «").Append(title).Append("»");
            sb.Append(" · результат: ").Append(string.IsNullOrEmpty(result) ? "выполнена" : result);
            return sb.ToString();
        }

        /// <summary>
        /// СТРОКА ПОПЫТКИ ПОСТАНОВКИ: что пытались, чем кончилось и почему.
        /// `постановка: модель «Стол 6-осевой» · ОТКАЗ · причина: модель недоступна …`
        /// </summary>
        public static string FormatPlacementLine(string what, bool ok, string reason)
        {
            string head = "постановка: " + (string.IsNullOrEmpty(what) ? "робот" : what) + " · " +
                          (ok ? "ВЫПОЛНЕНА" : "ОТКАЗ");
            if (ok) return head + (string.IsNullOrEmpty(reason) ? "" : " · " + reason);
            return head + " · причина: " + (string.IsNullOrEmpty(reason) ? "не названа" : reason);
        }

        /// <summary>Добавить событие.</summary>
        public void Add(KvLogKind kind, string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            double seconds = Time.realtimeSinceStartup;

            // ОТСЕЧЕНИЕ ПОВТОРОВ. Ключ по умолчанию — тип + текст без чисел: сервисы сообщают о
            // состоянии ОДНОЙ строкой, и различать «модуль/действие/значение» без разбора текста
            // нечем. Числа из ключа вырезаны (см. `NormalizeValue`), иначе «за 1,37 с» и «за 2,04 с»
            // считались бы разными событиями и не отсеклись бы никогда.
            string key = ComposeKey(KindLabel(kind), "", text);
            KvRepeatState state;
            if (!repeats.TryGetValue(key, out state))
            {
                if (repeats.Count >= RepeatMemory) repeats.Clear();   // кольцевой предел памяти
                state = new KvRepeatState { lastWriteAt = double.NaN, suppressed = 0 };
                repeats[key] = state;
            }

            if (!ShouldWrite(state.lastWriteAt, seconds, RepeatWindowSeconds, suppressRepeats))
            {
                // Повтор внутри окна: НЕ пишем, но считаем — число уйдёт в журнал при смене состояния.
                state.suppressed++;
                return;
            }

            // Пишем. Если до этого были подавленные повторы — приписываем их число хвостом «×N»
            // и обнуляем счётчик: следующая пачка считается заново.
            string line = ComposeText(text, state.suppressed);
            state.suppressed = 0;
            state.lastWriteAt = seconds;

            KvLogEntry e = new KvLogEntry
            {
                time = Time.realtimeSinceStartup,
                stamp = DateTime.Now.ToString("HH:mm:ss"),
                kind = kind,
                text = line
            };
            entries.Add(e);
            if (entries.Count > Mathf.Max(20, capacity)) entries.RemoveRange(0, entries.Count - capacity);

            // РОТАЦИЯ: файл, доросший до порога, закрывается — `CurrentFilePath` сам отдаст
            // следующую часть, а лишние части удаляются здесь же (иначе «не больше 3» не выполнится).
            string path = CurrentFilePath;
            FeatureStorage.AppendLine(path, e.Line);
            EnforceRotation(path);

            if (echoToConsole || kind == KvLogKind.Error)
            {
                if (kind == KvLogKind.Error) Debug.LogWarning("[Log] " + e.text);
                else Debug.Log("[Log] " + e.text);
            }

            // Запись, не проходящую фильтр, всё равно сохранили — панель просто не покажет её.
            Raise();
        }

        /// <summary>
        /// УДАЛИТЬ ЛИШНИЕ ЧАСТИ ЖУРНАЛА ЗА ТЕКУЩИЕ СУТКИ (хранение — не больше
        /// <see cref="MaxLogFiles"/> файлов). Решение принимает чистая функция
        /// <see cref="FilesToDelete"/>, здесь — только исполнение.
        /// </summary>
        private void EnforceRotation(string currentPath)
        {
            try
            {
                string dir = Path.GetDirectoryName(currentPath);
                string name = Path.GetFileNameWithoutExtension(currentPath);
                if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(name)) return;

                string stem = StemOfPart(name);

                var parts = new List<int>();
                for (int part = 1; part <= PartScanLimit; part++)
                {
                    string path = Path.Combine(dir, PartFileName(stem, part));
                    if (!File.Exists(path)) break;
                    parts.Add(part);
                }

                List<int> remove = FilesToDelete(parts, MaxLogFiles);
                for (int i = 0; i < remove.Count; i++)
                    FeatureStorage.DeleteFile(Path.Combine(dir, PartFileName(stem, remove[i])));
            }
            catch (Exception e)
            {
                // Ротация не имеет права ломать запись: не смогли убрать старый файл — пишем дальше.
                Debug.LogWarning("[Log] ротация журнала: " + e.Message);
            }
        }

        /// <summary>
        /// СКОЛЬКО ЧАСТЕЙ ВООБЩЕ ПРОСМАТРИВАТЬ. Предохранитель от бесконечного цикла и от
        /// «каталог забит тысячей файлов»: при <see cref="MaxLogFiles"/> = 3 реально живых частей
        /// не больше четырёх (три хранимые плюс только что созданная до удаления).
        /// </summary>
        public const int PartScanLimit = 64;

        /// <summary>
        /// ОСНОВА ИМЕНИ ЧАСТИ: `actions_20261112_2` → `actions_20261112`, `actions_20261112` →
        /// без изменений. ЧИСТАЯ ФУНКЦИЯ — проверяется тестом.
        ///
        /// ЗДЕСЬ БЫЛА ОШИБКА, И ОНА ПРИМЕР ТОГО, ЗАЧЕМ ЧИСТЫЕ ФУНКЦИИ. Первая версия резала имя по
        /// последнему подчёркиванию, если хвост состоял из цифр. Проверка на настоящих именах дала:
        /// `StemOfPart("actions_20261112")` → `actions`, потому что ДАТА `20261112` — это тоже
        /// сплошные цифры! Основа теряла день, и ротация начинала искать файлы `actions.log`,
        /// `actions_2.log` — то есть не находила свои же части НИКОГДА. Снаружи это выглядело бы
        /// как «правило «не больше 3 файлов» написано, но не работает».
        ///
        /// ЧТО СТАЛО. Хвост считается номером части, только если он цифровой И НЕ ЯВЛЯЕТСЯ ДАТОЙ:
        /// дата — это ровно <see cref="DateFormatLength"/> цифр (`yyyyMMdd`). Остальное остаётся
        /// частью основы. Проверены обе стороны: и «номер — режем», и «дата/слово — НЕ режем».
        /// </summary>
        public static string StemOfPart(string nameWithoutExtension)
        {
            if (string.IsNullOrEmpty(nameWithoutExtension)) return "";
            int cut = nameWithoutExtension.LastIndexOf('_');
            if (cut <= 0) return nameWithoutExtension;

            string tail = nameWithoutExtension.Substring(cut + 1);
            if (tail.Length == 0) return nameWithoutExtension;

            // Только цифры — иначе это не номер части (например, `actions_20261112_ночь`).
            for (int i = 0; i < tail.Length; i++)
                if (tail[i] < '0' || tail[i] > '9') return nameWithoutExtension;

            // ДАТА — НЕ НОМЕР ЧАСТИ. Восемь цифр после подчёркивания — это `yyyyMMdd`, то есть
            // сам день сессии, а не номер файла. Отрезать его значило бы потерять дату из имени.
            if (tail.Length == DateFormatLength) return nameWithoutExtension;

            return nameWithoutExtension.Substring(0, cut);
        }

        /// <summary>Длина даты в имени файла записи: `yyyyMMdd` — восемь цифр.</summary>
        public const int DateFormatLength = 8;

        /// <summary>
        /// СБРОСИТЬ ПАМЯТЬ О ПОВТОРАХ (для теста и для смены сеанса). После сброса то же событие
        /// снова считается новым — это и есть «смена состояния» в смысле правила отсечения.
        /// </summary>
        public void ResetRepeatMemory()
        {
            repeats.Clear();
        }

        /// <summary>
        /// НАЖАТИЕ КОМАНДЫ ИНТЕРФЕЙСА — отдельное событие с идентификатором (`KvLogKind.Ui`).
        ///
        /// ЧТО БЫЛО. Нажатие жило только в консоли Unity. В файле записи его не было вовсе: за
        /// сеанс самопрогон сделал 140 нажатий, а в файле ноль строк о них. То есть «доказательство
        /// работы кнопки» существовало ровно в том месте, которое на устройстве не читается.
        /// ЧЕМ ПЛОХО. Тестировщик видел файл из 14 строк, 11 из которых — один и тот же повтор, и
        /// не мог ни подтвердить, ни опровергнуть, что оператор вообще что-то нажимал.
        /// ЧТО СТАЛО. Каждое выполненное нажатие даёт строку с ИДЕНТИФИКАТОРОМ команды и её
        /// заголовком. Повторы отсекаются тем же правилом, что и всё остальное (30 с), — иначе
        /// самопрогон из 140 нажатий снова залил бы журнал.
        /// </summary>
        public void Command(string id, string title, string result)
        {
            Add(KvLogKind.Ui, FormatCommandLine(id, title, result));
        }

        /// <summary>СМЕНА РЕЖИМА VR/MR/AR — с числами (что доступно и какой масштаб мира).</summary>
        public void ModeChanged(string from, string to, bool passthrough, bool anchors, bool camera,
            float worldScale)
        {
            Add(KvLogKind.System, FormatModeLine(from, to, passthrough, anchors, camera, worldScale));
        }

        /// <summary>ПОПЫТКА ПОСТАНОВКИ И ЕЁ РЕЗУЛЬТАТ (успех — с местом, отказ — с причиной).</summary>
        public void Placement(string what, bool ok, string reason)
        {
            Add(KvLogKind.Point, FormatPlacementLine(what, ok, reason));
        }

        /// <summary>
        /// ЗАПИСЬ ПОЗЫ И ВЗГЛЯДА С ОГРАНИЧЕНИЕМ ЧАСТОТЫ И ПОРОГОМ ИЗМЕНЕНИЯ.
        ///
        /// Возвращает true, если строка действительно написана (это же проверяет тест). Внутри
        /// решение принимает чистая функция <see cref="PoseDue"/>: не чаще 10 Гц и только при
        /// заметном изменении — иначе журнал снова превратится в поток кадров.
        /// </summary>
        public bool Pose(float seconds, Vector3 position, Vector3 gaze, float fps)
        {
            float since = lastPoseAt < 0f ? float.PositiveInfinity : seconds - lastPoseAt;
            float moved = lastPoseAt < 0f ? float.PositiveInfinity
                : (position - lastPosePosition).magnitude;
            float turned = lastPoseAt < 0f ? float.PositiveInfinity
                : Vector3.Angle(lastPoseGaze, gaze);

            if (!PoseDue(since, moved, turned, lastPoseAt >= 0f)) return false;

            lastPoseAt = seconds;
            lastPosePosition = position;
            lastPoseGaze = gaze;
            Add(KvLogKind.Pose, FormatPoseLine(seconds, position, gaze, fps));
            return true;
        }

        private float lastPoseAt = -1f;
        private Vector3 lastPosePosition;
        private Vector3 lastPoseGaze = Vector3.forward;

        /// <summary>
        /// СБРОСИТЬ ПАМЯТЬ ПОЗЫ: следующая запись позы снова считается первой (пишется всегда).
        /// Нужно при смене режима и после восстановления трекинга — иначе «прыжок» головы после
        /// сна шлема остался бы незамеченным порогом.
        /// </summary>
        public void ResetPoseMemory()
        {
            lastPoseAt = -1f;
            lastPosePosition = Vector3.zero;
            lastPoseGaze = Vector3.forward;
        }

        public void Info(string text) { Add(KvLogKind.System, text); }
        public void Error(string text) { Add(KvLogKind.Error, text); }

        /// <summary>Предупреждение (этап 36 ТЗ): не ошибка, но требует внимания оператора.</summary>
        public void Warning(string text) { Add(KvLogKind.Warning, text); }

        /// <summary>Записи с учётом фильтра (для панели журнала).</summary>
        public List<KvLogEntry> Filtered()
        {
            if (filter == null) return new List<KvLogEntry>(entries);
            List<KvLogEntry> result = new List<KvLogEntry>();
            for (int i = 0; i < entries.Count; i++)
                if (entries[i].kind == filter.Value) result.Add(entries[i]);
            return result;
        }

        /// <summary>Последние N записей с учётом фильтра (для прокрутки «в конец»).</summary>
        public List<KvLogEntry> Tail(int count)
        {
            List<KvLogEntry> all = Filtered();
            if (all.Count <= count) return all;
            return all.GetRange(all.Count - count, count);
        }

        public void Clear()
        {
            entries.Clear();
            // Память о повторах и позе чистится вместе с буфером: после очистки журнал начинает
            // «с чистого листа», и подавленное до очистки не приписывается первой же новой строке.
            repeats.Clear();
            ResetPoseMemory();
            Raise();
        }

        /// <summary>Тип события по строке настроек (для внешней схемы).</summary>
        public static bool TryParseKind(string name, out KvLogKind kind)
        {
            kind = KvLogKind.System;
            if (string.IsNullOrEmpty(name)) return false;
            try
            {
                kind = (KvLogKind)Enum.Parse(typeof(KvLogKind), name, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string KindLabel(KvLogKind kind)
        {
            switch (kind)
            {
                case KvLogKind.Point: return "точка";
                case KvLogKind.Trajectory: return "траектория";
                case KvLogKind.Motion: return "движение";
                case KvLogKind.Stop: return "стоп";
                case KvLogKind.Zone: return "зоны";
                case KvLogKind.Pose: return "позы";
                case KvLogKind.Record: return "запись";
                case KvLogKind.Session: return "сессия";
                case KvLogKind.Scenario: return "сценарий";
                case KvLogKind.Error: return "ошибка";
                case KvLogKind.Warning: return "предупреждение";
                case KvLogKind.Ui: return "интерфейс";
                default: return "система";
            }
        }

        /// <summary>Короткое имя типа для колонки фильтра.</summary>
        public static string ShortKind(KvLogKind kind)
        {
            switch (kind)
            {
                case KvLogKind.Point: return "ТЧК";
                case KvLogKind.Trajectory: return "ТРЖ";
                case KvLogKind.Motion: return "ДВЖ";
                case KvLogKind.Stop: return "СТП";
                case KvLogKind.Zone: return "ЗОН";
                case KvLogKind.Pose: return "ПОЗ";
                case KvLogKind.Record: return "ЗПС";
                case KvLogKind.Session: return "СЕС";
                case KvLogKind.Scenario: return "СЦН";
                case KvLogKind.Error: return "ОШБ";
                case KvLogKind.Warning: return "ПРД";
                case KvLogKind.Ui: return "ИНТ";
                default: return "СИС";
            }
        }

        public static Color KindColor(KvLogKind kind)
        {
            switch (kind)
            {
                case KvLogKind.Error: return new Color(1f, 0.28f, 0.32f);
                case KvLogKind.Warning: return new Color(1f, 0.75f, 0.2f);
                case KvLogKind.Stop: return new Color(1f, 0.55f, 0.15f);
                case KvLogKind.Motion: return new Color(0.45f, 0.85f, 1f);
                case KvLogKind.Point: return new Color(0.95f, 0.45f, 0.45f);
                case KvLogKind.Trajectory: return new Color(0.95f, 0.8f, 0.3f);
                case KvLogKind.Zone: return new Color(1f, 0.5f, 0.5f);
                case KvLogKind.Pose: return new Color(0.6f, 1f, 0.6f);
                case KvLogKind.Record: return new Color(0.75f, 0.7f, 1f);
                case KvLogKind.Session: return new Color(0.7f, 0.9f, 0.95f);
                case KvLogKind.Scenario: return new Color(0.95f, 0.75f, 0.95f);
                default: return new Color(0.75f, 0.78f, 0.82f);
            }
        }

        /// <summary>Выгрузить весь буфер в отдельный файл (кнопка «Экспорт журнала»).</summary>
        public string Export()
        {
            string path = Path.Combine(FeatureStorage.LogsDir, "actions_export_" + FeatureStorage.TimeStamp() + ".log");
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < entries.Count; i++) sb.AppendLine(entries[i].Line);
            try
            {
                File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
                Add(KvLogKind.System, "журнал выгружен: " + path);
                return path;
            }
            catch (Exception e)
            {
                Error("не удалось выгрузить журнал: " + e.Message);
                return null;
            }
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }
    }
}
