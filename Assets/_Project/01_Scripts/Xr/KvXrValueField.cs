using System.Globalization;
using UnityEngine;
using KazistovVvUI;

namespace KazistovVvXr
{
    /// <summary>
    /// ПОЛЕ, КОТОРОЕ ВЛАДЕЛЕЦ ВВОДИТ РУКАМИ (сессия §41, задание [S1]: «любое числовое поле,
    /// доступное на компьютере, выбрать и ввести вручную — с теми же границами»).
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ РЕЕСТР, А НЕ СПИСОК В ЭКРАНЕ. Границы и запись обязаны быть в ОДНОМ месте:
    /// экран «Настройки» рисует строки, клавиатура и колесо дают строку-кандидат, а проверяет и
    /// записывает всё это одна функция (`KvXrValueInput.Commit`). Иначе появится второе место, где
    /// живут границы, и они разойдутся — ровно так уже было с целями луча (карточка Б50).
    ///
    /// ДВА ХРАНИЛИЩА, НО НИ ОДНОГО НОВОГО. Часть полей живёт в настройках шлема
    /// (`KvXrSettings`, ключи `KazistovVv.XR.*`), часть — в настройках интерфейса компьютера
    /// (`KvSettings`, ключи `KazistovVv.*`). Запись идёт ТОЛЬКО через существующие сеттеры: своего
    /// хранилища у ввода нет и быть не должно ([K4]).
    /// </summary>
    public enum KvXrValueFieldId
    {
        /// <summary>Радиус панели, м. Хранится в настройках шлема.</summary>
        PanelRadius = 0,

        /// <summary>Скорость плавного хода, м/с.</summary>
        SmoothSpeed = 1,

        /// <summary>Рост оператора, м.</summary>
        OperatorHeight = 2,

        /// <summary>Дальность телепорта, м.</summary>
        TeleportDistance = 3,

        /// <summary>Масштаб интерфейса, × (настройка компьютера).</summary>
        UiScale = 4,

        /// <summary>Плотность интерфейса (настройка компьютера).</summary>
        Density = 5,

        /// <summary>Рядов панели инструментов (настройка компьютера).</summary>
        ToolbarRows = 6,

        /// <summary>Раскладка панели инструментов (настройка компьютера).</summary>
        ToolbarLayout = 7,

        /// <summary>Размер шрифта (настройка компьютера).</summary>
        FontSize = 8,

        /// <summary>Режим цветовой коррекции (настройка компьютера).</summary>
        ColorBlind = 9
    }

    /// <summary>
    /// ОПИСАНИЕ ОДНОГО ЧИСЛОВОГО ПОЛЯ: подпись, границы, формат, чтение и запись.
    ///
    /// `Read`/`Write` — обёртки над СУЩЕСТВУЮЩИМИ сеттерами и геттерами проекта; ни одно значение
    /// здесь не хранится. Поэтому «границы в реестре» и «границы в сеттере» — одни и те же числа
    /// (проверка: тест `Numeric_EveryFieldBoundsAreOwnersConstants`).
    /// </summary>
    public struct KvXrValueFieldInfo
    {
        /// <summary>Идентификатор поля.</summary>
        public KvXrValueFieldId Id;

        /// <summary>Подпись для экрана: «Радиус панели».</summary>
        public string Title;

        /// <summary>Подпись для строки журнала — строчными, как в задании: «радиус панели».</summary>
        public string LogTitle;

        /// <summary>Единица измерения для экрана и журнала: «м», «м/с», «×», пусто.</summary>
        public string Unit;

        /// <summary>Нижняя граница — ровно та, что проверяет сеттер.</summary>
        public float Min;

        /// <summary>Верхняя граница — ровно та, что проверяет сеттер.</summary>
        public float Max;

        /// <summary>Сколько знаков после запятой печатать.</summary>
        public int Decimals;

        /// <summary>Целое ли значение (ряды, плотность, шрифт, цветокоррекция).</summary>
        public bool Whole;

        /// <summary>Прочитать текущее значение тем же API, что и компьютер.</summary>
        public float Read()
        {
            switch (Id)
            {
                case KvXrValueFieldId.PanelRadius: return KvXrSettings.GetGroupPanelRadius();
                case KvXrValueFieldId.SmoothSpeed: return KvXrSettings.GetSmoothSpeed();
                case KvXrValueFieldId.OperatorHeight: return KvXrSettings.GetOperatorHeight();
                case KvXrValueFieldId.TeleportDistance: return KvXrSettings.GetTeleportDistance();
                case KvXrValueFieldId.UiScale: return KvSettings.UiScale;
                case KvXrValueFieldId.Density: return KvSettings.Density;
                case KvXrValueFieldId.ToolbarRows: return KvSettings.ToolbarRows;
                case KvXrValueFieldId.ToolbarLayout: return KvSettings.ToolbarLayout;
                case KvXrValueFieldId.FontSize: return KvSettings.FontSize;
                default: return KvSettings.ColorBlind;
            }
        }

        /// <summary>
        /// ЗАПИСАТЬ значение существующим сеттером. `true` — записалось ровно то, что просили;
        /// `false` — сеттер зажал значение в свои границы (значит границы разошлись, и это дефект).
        /// </summary>
        public bool Write(float value)
        {
            float clamped = Mathf.Clamp(value, Min, Max);
            int whole = Mathf.RoundToInt(clamped);

            switch (Id)
            {
                case KvXrValueFieldId.PanelRadius: KvXrSettings.SetGroupPanelRadius(clamped); break;
                case KvXrValueFieldId.SmoothSpeed: KvXrSettings.SetSmoothSpeed(clamped); break;
                case KvXrValueFieldId.OperatorHeight: KvXrSettings.SetOperatorHeight(clamped); break;
                case KvXrValueFieldId.TeleportDistance: KvXrSettings.SetTeleportDistance(clamped); break;
                case KvXrValueFieldId.UiScale: KvSettings.UiScale = clamped; break;
                case KvXrValueFieldId.Density: KvSettings.Density = whole; break;
                case KvXrValueFieldId.ToolbarRows: KvSettings.ToolbarRows = whole; break;
                case KvXrValueFieldId.ToolbarLayout: KvSettings.ToolbarLayout = whole; break;
                case KvXrValueFieldId.FontSize: KvSettings.FontSize = whole; break;
                default: KvSettings.ColorBlind = whole; break;
            }

            float stored = Read();
            return Mathf.Abs(stored - value) <= 0.0005f;
        }

        /// <summary>Границы одной строкой для сообщений: «0.080…0.420».</summary>
        public string RangeText()
        {
            return Format(Min, Decimals) + "…" + Format(Max, Decimals);
        }

        /// <summary>Число с нужным количеством знаков, всегда с точкой (как в журнале и в задании).</summary>
        public static string Format(float value, int decimals)
        {
            return value.ToString("F" + Mathf.Clamp(decimals, 0, 4), CultureInfo.InvariantCulture);
        }
    }

    /// <summary>ЧЕМ ЗАКОНЧИЛСЯ ВВОД. Отказ — это нормальный исход, а не ошибка ([A3]).</summary>
    public enum KvXrCommitStatus
    {
        /// <summary>Значение принято и записано.</summary>
        Saved = 0,

        /// <summary>Пустая строка.</summary>
        Empty = 1,

        /// <summary>Не число: буквы, две точки, точка без цифр и т. п.</summary>
        BadFormat = 2,

        /// <summary>Число вне границ поля.</summary>
        OutOfBounds = 3,

        /// <summary>Слишком длинная строка.</summary>
        TooLong = 4
    }

    /// <summary>РЕЗУЛЬТАТ ВВОДА: что решили, какое число, почему и какой строкой это уйдёт в журнал.</summary>
    public struct KvXrCommitOutcome
    {
        /// <summary>Исход.</summary>
        public KvXrCommitStatus Status;

        /// <summary>Принятое число (при отказе — прежнее значение поля, не изменённое).</summary>
        public float Value;

        /// <summary>Причина отказа словами: «вне границ 0.080…0.420».</summary>
        public string Reason;

        /// <summary>Готовая строка журнала (печатается вызывающим, а не здесь: журнал не должен врать).</summary>
        public string LogLine;

        /// <summary>Значение записано.</summary>
        public bool Saved { get { return Status == KvXrCommitStatus.Saved; } }
    }

    /// <summary>
    /// РЕЕСТР ВВОДИМЫХ ПОЛЕЙ: десять числовых, ровно те, что есть на компьютере и в шлеме.
    /// Порядок = порядок на экране «Настройки»: сначала параметры шлема, затем интерфейса компьютера.
    /// </summary>
    public static class KvXrValueFields
    {
        /// <summary>Сколько полей доступно для ручного ввода.</summary>
        public const int Count = 10;

        private static readonly KvXrValueFieldInfo[] fields = Build();

        private static KvXrValueFieldInfo[] Build()
        {
            return new[]
            {
                Make(KvXrValueFieldId.PanelRadius, "Радиус панели", "радиус панели", "м",
                    KvXrGroupPanelLayout.MinPanelRadiusMeters, KvXrGroupPanelLayout.MaxPanelRadiusMeters, 3, false),
                Make(KvXrValueFieldId.SmoothSpeed, "Скорость плавного хода", "скорость плавного хода", "м/с",
                    KvXrSettings.MinSmoothSpeed, KvXrSettings.MaxSmoothSpeed, 1, false),
                Make(KvXrValueFieldId.OperatorHeight, "Рост оператора", "рост оператора", "м",
                    KvXrSettings.MinOperatorHeight, KvXrSettings.MaxOperatorHeight, 2, false),
                Make(KvXrValueFieldId.TeleportDistance, "Дальность телепорта", "дальность телепорта", "м",
                    KvXrSettings.MinTeleportDistance, KvXrSettings.MaxTeleportDistance, 1, false),
                // Десктопные поля: границы взяты ИЗ СЕТТЕРОВ `KvSettings` (Mathf.Clamp прямо в них).
                Make(KvXrValueFieldId.UiScale, "Масштаб интерфейса", "масштаб интерфейса", "×", 0.6f, 1.6f, 3, false),
                Make(KvXrValueFieldId.Density, "Плотность", "плотность", "", 0f, 1f, 0, true),
                Make(KvXrValueFieldId.ToolbarRows, "Рядов панели инструментов", "рядов панели", "", 1f, 6f, 0, true),
                Make(KvXrValueFieldId.ToolbarLayout, "Раскладка панели инструментов", "раскладка панели", "", 0f, 2f, 0, true),
                Make(KvXrValueFieldId.FontSize, "Размер шрифта", "размер шрифта", "", 0f, 2f, 0, true),
                Make(KvXrValueFieldId.ColorBlind, "Режим цветовой коррекции", "цветовая коррекция", "", 0f, 3f, 0, true)
            };
        }

        private static KvXrValueFieldInfo Make(KvXrValueFieldId id, string title, string logTitle, string unit,
                                               float min, float max, int decimals, bool whole)
        {
            return new KvXrValueFieldInfo
            {
                Id = id, Title = title, LogTitle = logTitle, Unit = unit,
                Min = min, Max = max, Decimals = decimals, Whole = whole
            };
        }

        /// <summary>Поле по номеру строки экрана (0 … Count−1).</summary>
        public static KvXrValueFieldInfo Get(int index)
        {
            return fields[Mathf.Clamp(index, 0, Count - 1)];
        }

        /// <summary>Поле по идентификатору.</summary>
        public static KvXrValueFieldInfo Get(KvXrValueFieldId id)
        {
            return fields[Mathf.Clamp((int)id, 0, Count - 1)];
        }

        /// <summary>Номер строки по идентификатору.</summary>
        public static int IndexOf(KvXrValueFieldId id)
        {
            return Mathf.Clamp((int)id, 0, Count - 1);
        }

        /// <summary>Подпись поля с границами и единицей: «Радиус панели · 0.080…0.420 м».</summary>
        public static string Describe(KvXrValueFieldInfo field)
        {
            return field.Title + " · " + field.RangeText() + UnitPart(field);
        }

        /// <summary>Единица с пробелом перед ней или пустая строка.</summary>
        public static string UnitPart(KvXrValueFieldInfo field)
        {
            return string.IsNullOrEmpty(field.Unit) ? "" : " " + field.Unit;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА ОБ УСПЕШНОМ ВВОДЕ. Формат — из задания [S2], дословно:
        ///   клавиатура: [KvXr] ВВОД: клавиатура «0.125» → радиус панели 0.125 м (границы 0.080…0.420) · сохранено
        ///   колесо:     [KvXr] ВВОД: колесо «0.125» → радиус панели 0.125 м · подтверждено
        ///   голос:      [KvXr] ВВОД: голос «сто двадцать пять» → 0.125 · подтверждено
        /// </summary>
        public static string InputLogLine(KvXrInputMethod method, string text,
                                          KvXrValueFieldInfo field, float value)
        {
            string number = KvXrValueFieldInfo.Format(value, field.Decimals);

            switch (method)
            {
                case KvXrInputMethod.Keyboard:
                    return "[KvXr] ВВОД: клавиатура «" + text + "» → " + field.LogTitle + " " + number +
                           UnitPart(field) + " (границы " + field.RangeText() + ") · сохранено";
                case KvXrInputMethod.Wheel:
                    return "[KvXr] ВВОД: колесо «" + text + "» → " + field.LogTitle + " " + number +
                           UnitPart(field) + " · подтверждено";
                default:
                    return "[KvXr] ВВОД: голос «" + text + "» → " + number + " · подтверждено";
            }
        }
    }

    /// <summary>
    /// ОДНА ТОЧКА СОХРАНЕНИЯ ([A3], главное правило волны): любой способ ввода — клавиатура, колесо,
    /// голос — передаёт СТРОКУ сюда. Здесь проверяются формат и границы и здесь же идёт запись.
    /// Ввод никогда не пишет настройку сам: иначе три способа разойдутся по правилам.
    /// </summary>
    public static class KvXrValueInput
    {
        /// <summary>Предел длины строки-кандидата: больше — не число, а мусор.</summary>
        public const int MaxTextLength = 12;

        /// <summary>Допуск сравнения с границами: 0.420, набранное как 0.4200001, — то же число.</summary>
        private const float BoundTolerance = 0.00005f;

        /// <summary>Проверить строку без записи (формат и границы). Нужно для показа «что будет введено».</summary>
        public static KvXrCommitOutcome Parse(KvXrValueFieldInfo field, string text)
        {
            var outcome = new KvXrCommitOutcome
            {
                Status = KvXrCommitStatus.Saved,
                Value = field.Read(),
                Reason = "",
                LogLine = ""
            };

            string trimmed = Clean(text);

            if (trimmed.Length == 0)
            {
                outcome.Status = KvXrCommitStatus.Empty;
                outcome.Reason = "пустая строка";
                return outcome;
            }

            if (trimmed.Length > MaxTextLength)
            {
                outcome.Status = KvXrCommitStatus.TooLong;
                outcome.Reason = "строка длиннее " + MaxTextLength + " символов";
                return outcome;
            }

            float parsed;
            if (!float.TryParse(Normalize(trimmed), NumberStyles.Float, CultureInfo.InvariantCulture, out parsed) ||
                float.IsNaN(parsed) || float.IsInfinity(parsed))
            {
                outcome.Status = KvXrCommitStatus.BadFormat;
                outcome.Reason = "не число";
                return outcome;
            }

            if (field.Whole) parsed = Mathf.Round(parsed);

            if (parsed < field.Min - BoundTolerance || parsed > field.Max + BoundTolerance)
            {
                outcome.Status = KvXrCommitStatus.OutOfBounds;
                outcome.Reason = "вне границ " + field.RangeText();
                return outcome;
            }

            outcome.Value = Mathf.Clamp(parsed, field.Min, field.Max);
            return outcome;
        }

        /// <summary>Проверить И ЗАПИСАТЬ через существующий сеттер. Отказ не меняет значение.</summary>
        public static KvXrCommitOutcome Commit(KvXrValueFieldId id, string text, KvXrInputMethod method)
        {
            return Commit(KvXrValueFields.IndexOf(id), text, method);
        }

        /// <summary>То же по номеру строки экрана.</summary>
        public static KvXrCommitOutcome Commit(int fieldIndex, string text, KvXrInputMethod method)
        {
            KvXrValueFieldInfo field = KvXrValueFields.Get(fieldIndex);
            KvXrCommitOutcome outcome = Parse(field, text);
            string trimmed = Clean(text);

            if (!outcome.Saved)
            {
                outcome.LogLine = RefusalLine(field, trimmed, outcome.Status);
                return outcome;
            }

            field.Write(outcome.Value);

            // В ЖУРНАЛ ИДЁТ ПРОЧИТАННОЕ, А НЕ ЖЕЛАЕМОЕ: если сеттер зажал значение, строка назовёт
            // то, что действительно записалось.
            outcome.Value = field.Read();
            outcome.LogLine = KvXrValueFields.InputLogLine(method, trimmed, field, outcome.Value);
            return outcome;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА ОБ ОТКАЗЕ. Формулировки — из задания [S2]:
        ///   [KvXr] ВВОД: «abc» отклонён — значение не изменено
        ///   [KvXr] ВВОД: 9.9 отклонён (вне границ 0.080…0.420)
        /// </summary>
        public static string RefusalLine(KvXrValueFieldInfo field, string text, KvXrCommitStatus status)
        {
            switch (status)
            {
                case KvXrCommitStatus.Empty:
                    return "[KvXr] ВВОД: пустая строка отклонена — значение не изменено";
                case KvXrCommitStatus.TooLong:
                    return "[KvXr] ВВОД: «" + text + "» отклонён — строка длиннее " + MaxTextLength +
                           " символов, значение не изменено";
                case KvXrCommitStatus.OutOfBounds:
                    return "[KvXr] ВВОД: " + text + " отклонён (вне границ " + field.RangeText() + ")";
                default:
                    return "[KvXr] ВВОД: «" + text + "» отклонён — значение не изменено";
            }
        }

        /// <summary>Обрезать края и привести «типографские» минусы к обычному: их даёт распознавание речи.</summary>
        private static string Clean(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Trim().Replace('−', '-').Replace('–', '-').Replace('—', '-');
        }

        /// <summary>Запятая как разделитель дробной части — так пишут и так говорят.</summary>
        private static string Normalize(string text)
        {
            return text.Replace(',', '.').Replace(' ', '\u0000').Replace("\u0000", "");
        }
    }
}
