using System.Text;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// СПОСОБ ВВОДА (задание [S1], решение владельца R5: «все три способа, переключение в шлеме»).
    ///
    /// ПОЧЕМУ ТРИ, А НЕ ОДИН. Владелец прямо сказал: «есть один способ» не принимается. Способ
    /// выбирается на экране «Настройки» самим владельцем в шлеме — чтобы он сравнил их руками, а не
    /// читал доводы в отчёте.
    /// </summary>
    public enum KvXrInputMethod
    {
        /// <summary>Клавиатура на панели: символы нажимаются лучом.</summary>
        Keyboard = 0,

        /// <summary>Колесо символов: выбор стиком, подтверждение курком или щипком.</summary>
        Wheel = 1,

        /// <summary>Голос: микрофон шлема, распознавание в текст, затем проверка числа.</summary>
        Voice = 2
    }

    /// <summary>
    /// ПРАВИЛА ТЕКСТА — ОДНИ И ТЕ ЖЕ ДЛЯ ВСЕХ ТРЁХ СПОСОБОВ И ДЛЯ ВСЕХ ЭКРАНОВ.
    ///
    /// ОГРАНИЧЕНИЯ ВЗЯТЫ У КОМПЬЮТЕРА, А НЕ ПРИДУМАНЫ: переименование в `KvTreeView.CommitRename`
    /// отбрасывает управляющие символы и молча обрезает строку на 48 символах
    /// (`KvTreeView.cs:678-679`). Шлем обязан вести себя так же, иначе одно и то же имя будет
    /// принято на компьютере и отвергнуто в шлеме.
    /// </summary>
    public static class KvXrTextRules
    {
        /// <summary>Предел имени — как на компьютере (`KvTreeView.cs:679`).</summary>
        public const int MaxNameLength = 48;

        /// <summary>Предел строки поиска.</summary>
        public const int MaxQueryLength = 32;

        /// <summary>Символ, которым показан курсор в строке-кандидате.</summary>
        public const char Cursor = '_';

        /// <summary>Годится ли строка в имя объекта: непустая после Trim, ≤ 48, без управляющих символов.</summary>
        public static bool IsAcceptableName(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            if (text.Trim().Length == 0) return false;
            if (text.Length > MaxNameLength) return false;

            for (int i = 0; i < text.Length; i++)
                if (char.IsControl(text[i])) return false;

            return true;
        }

        /// <summary>Можно ли набрать символ в число: только цифры, точка и минус.</summary>
        public static bool AcceptsNumberCharacter(char symbol)
        {
            return (symbol >= '0' && symbol <= '9') || symbol == '.' || symbol == '-';
        }

        /// <summary>Можно ли набрать символ в имя или строку поиска: буквы, цифры, пробел, знаки.</summary>
        public static bool AcceptsTextCharacter(char symbol)
        {
            return !char.IsControl(symbol);
        }
    }

    /// <summary>
    /// СОСТОЯНИЕ ВВОДА: строка-кандидат, её предел и то, к какому полю она относится.
    ///
    /// ЭТО НЕ ЭКРАН И НЕ UNITY-ОБЪЕКТ: класс только копит символы и умеет отбой/сброс, поэтому
    /// проверяется тестами без шлема. Рисует строку панель, сохраняет — `KvXrValueInput.Commit`.
    /// </summary>
    public class KvXrTextInput
    {
        private readonly StringBuilder builder = new StringBuilder(64);
        private bool active;
        private string title = "";
        private int fieldIndex = -1;
        private int maxLength = KvXrTextRules.MaxQueryLength;

        /// <summary>Активен ли ввод (открыт экран ввода).</summary>
        public bool IsActive { get { return active; } }

        /// <summary>Подпись поля или экрана: «Радиус панели», «Имя объекта», «Поиск».</summary>
        public string Title { get { return title; } }

        /// <summary>Набранная строка-кандидат (без курсора).</summary>
        public string Text { get { return builder.ToString(); } }

        /// <summary>Строка для показа на панели: текст и курсор в конце.</summary>
        public string Display { get { return builder.ToString() + KvXrTextRules.Cursor; } }

        /// <summary>Номер поля (0 … 9), −1 — вводится не число (имя, поиск).</summary>
        public int FieldIndex { get { return fieldIndex; } }

        /// <summary>Текущий предел длины.</summary>
        public int MaxLength { get { return maxLength; } }

        /// <summary>Вводится число (тогда допустимы только цифры, точка и минус).</summary>
        public bool Numeric { get { return fieldIndex >= 0; } }

        /// <summary>Начать ввод числа в указанное поле.</summary>
        public void BeginField(KvXrValueFieldId id)
        {
            fieldIndex = KvXrValueFields.IndexOf(id);
            title = KvXrValueFields.Get(fieldIndex).Title;
            maxLength = KvXrValueInput.MaxTextLength;
            builder.Length = 0;
            active = true;
        }

        /// <summary>Начать ввод текста (имя объекта, строка поиска).</summary>
        public void BeginText(string title, int maxLength)
        {
            fieldIndex = -1;
            this.title = title == null ? "" : title;
            this.maxLength = Mathf.Clamp(maxLength, 1, 256);
            builder.Length = 0;
            active = true;
        }

        /// <summary>Дописать символ. `false` — символ недопустим или предел длины достигнут.</summary>
        public bool Append(char symbol)
        {
            if (!active) return false;
            bool allowed = Numeric
                ? KvXrTextRules.AcceptsNumberCharacter(symbol)
                : KvXrTextRules.AcceptsTextCharacter(symbol);
            if (!allowed) return false;
            if (builder.Length >= maxLength) return false;

            builder.Append(symbol);
            return true;
        }

        /// <summary>Отбой: убрать последний символ.</summary>
        public void Backspace()
        {
            if (builder.Length > 0) builder.Length--;
        }

        /// <summary>Сброс набранного (строка-кандидат пуста, ввод остаётся открытым).</summary>
        public void Clear()
        {
            builder.Length = 0;
        }

        /// <summary>Закрыть ввод: строка-кандидат больше не показывается.</summary>
        public void End()
        {
            active = false;
            builder.Length = 0;
            fieldIndex = -1;
            title = "";
        }
    }

    /// <summary>
    /// ВЫБРАННЫЙ СПОСОБ ВВОДА — НАСТРОЙКА, А НЕ КОНСТАНТА (решение владельца R5).
    /// Живёт рядом с остальными настройками шлема и переживает перезапуск.
    /// </summary>
    public static class KvXrInputMethodSetting
    {
        /// <summary>Ключ настройки. Новый ключ, существующие не трогаются.</summary>
        public const string Key = "KazistovVv.XR.Input.Method";

        /// <summary>Сколько способов предлагается.</summary>
        public const int Count = 3;

        /// <summary>Способ по умолчанию: клавиатура (владелец видит её первой).</summary>
        public const KvXrInputMethod Default = KvXrInputMethod.Keyboard;

        /// <summary>Прочитать выбранный способ.</summary>
        public static KvXrInputMethod Get()
        {
            int stored = PlayerPrefs.GetInt(Key, (int)Default);
            return FromIndex(stored);
        }

        /// <summary>Записать выбранный способ.</summary>
        public static void Set(KvXrInputMethod method)
        {
            PlayerPrefs.SetInt(Key, (int)FromIndex((int)method));
            PlayerPrefs.Save();
        }

        /// <summary>Способ по номеру (с зажимом в допустимый диапазон).</summary>
        public static KvXrInputMethod FromIndex(int index)
        {
            return (KvXrInputMethod)Mathf.Clamp(index, 0, Count - 1);
        }

        /// <summary>Название способа строчными: «клавиатура», «колесо», «голос».</summary>
        public static string Label(KvXrInputMethod method)
        {
            switch (FromIndex((int)method))
            {
                case KvXrInputMethod.Wheel: return "колесо";
                case KvXrInputMethod.Voice: return "голос";
                default: return "клавиатура";
            }
        }

        /// <summary>
        /// Следующий способ по кругу (переключатель на экране «Настройки»).
        ///
        /// ПОРЯДОК — ИЗ ЗАДАНИЯ [S5]: «числа (клавиатура → голос → колесо)». Поэтому цикл
        /// клавиатура → голос → колесо → клавиатура, а не по номерам перечисления: владелец
        /// переключает и сравнивает способы именно в этом порядке, и порядок номеров к нему
        /// отношения не имеет.
        /// </summary>
        public static KvXrInputMethod Next(KvXrInputMethod method)
        {
            switch (FromIndex((int)method))
            {
                case KvXrInputMethod.Keyboard: return KvXrInputMethod.Voice;
                case KvXrInputMethod.Voice: return KvXrInputMethod.Wheel;
                default: return KvXrInputMethod.Keyboard;
            }
        }

        /// <summary>
        /// Строка журнала переключения — из задания [S2] дословно:
        ///   [KvXr] НАСТРОЙКА: способ ввода «голос» · было «клавиатура»
        /// </summary>
        public static string SwitchLogLine(KvXrInputMethod now, KvXrInputMethod was)
        {
            return "[KvXr] НАСТРОЙКА: способ ввода «" + Label(now) + "» · было «" + Label(was) + "»";
        }
    }
}
