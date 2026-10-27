using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace KazistovVvXr
{
    /// <summary>
    /// РАЗБОР ПРОИЗНЕСЁННОГО ЧИСЛА В ЦИФРЫ (задание [A3]: «голос … приведение к числу с проверкой»).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КЛАСС. Распознавание речи отдаёт текст — и это единственное, что от него
    /// зависит. Всё остальное (понимает ли программа «ноль целых сто двадцать пять тысячных») —
    /// чистая функция, которую можно проверить тестом без микрофона. Если положить разбор внутрь
    /// компонента с микрофоном, он останется непроверенным до самого шлема.
    ///
    /// ЧТО ОБЯЗАНО РАЗБИРАТЬСЯ: «сто двадцать пять» → 125; «ноль целых сто двадцать пять тысячных»
    /// → 0,125; «минус пять» → −5; «девять и девять десятых» → 9,9. Готовые цифры («0.125») тоже
    /// принимаются: распознаватель может вернуть их сам.
    ///
    /// ЧЕГО НЕ ДЕЛАЕТСЯ: догадок. «привет» — это отказ с причиной, а не ноль. Молчаливый ноль в
    /// настройке радиуса панели — худшее, что может случиться ([K4]: проверка правдоподобия).
    /// </summary>
    public static class KvXrNumberWords
    {
        /// <summary>Слова-разделители целой и дробной части.</summary>
        private static readonly string[] Separators = { "целых", "целая", "целое", "запятая", "точка", "и" };

        /// <summary>Знаменатели дробной части: слово → сколько знаков после запятой.</summary>
        private static readonly Dictionary<string, int> Denominators = new Dictionary<string, int>
        {
            { "десятых", 1 }, { "десятая", 1 }, { "десятые", 1 },
            { "сотых", 2 }, { "сотая", 2 }, { "сотые", 2 },
            { "тысячных", 3 }, { "тысячная", 3 }, { "тысячные", 3 },
            { "десятитысячных", 4 }, { "десятитысячная", 4 }
        };

        /// <summary>
        /// РАЗОБРАТЬ ФРАЗУ. `true` — число получено и `reason` пуст; `false` — число не распознано и
        /// `reason` называет причину («в речи нет числа», «две целых части» и т. п.).
        /// </summary>
        public static bool TryParse(string phrase, out float value, out string reason)
        {
            value = 0f;
            string normalized = Normalize(phrase);

            if (normalized.Length == 0)
            {
                reason = "пустая фраза";
                return false;
            }

            // ГОТОВЫЕ ЦИФРЫ ПРИНИМАЮТСЯ КАК ЕСТЬ: распознаватель может вернуть «0.125» сам.
            float direct = 0f;
            if (float.TryParse(normalized.Replace(' ', '\0').Replace("\0", "").Replace(',', '.'),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out direct) &&
                !float.IsNaN(direct) && !float.IsInfinity(direct))
            {
                value = direct;
                reason = "";
                return true;
            }

            string[] words = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

            bool negative = false;
            int start = 0;
            if (words.Length > 0 && (words[0] == "минус" || words[0] == "отрицательный"))
            {
                negative = true;
                start = 1;
            }

            // ИЩЕМ РАЗДЕЛИТЕЛЬ ЦЕЛОЙ И ДРОБНОЙ ЧАСТИ.
            int split = -1;
            for (int i = start; i < words.Length; i++)
                if (IsSeparator(words[i])) { split = i; break; }

            long integerPart = 0;
            if (split < 0)
            {
                if (!TryReadInteger(words, start, words.Length, out integerPart))
                {
                    reason = "в речи нет числа";
                    return false;
                }

                value = negative ? -integerPart : integerPart;
                reason = "";
                return true;
            }

            if (!TryReadInteger(words, start, split, out integerPart))
            {
                reason = "целая часть не распознана";
                return false;
            }

            // ДРОБНАЯ ЧАСТЬ: «… сотых/тысячных» — знаменателем; «запятая 125» — по количеству цифр.
            int fractionEnd = words.Length;
            int digits = 0;
            for (int i = fractionEnd - 1; i > split; i--)
            {
                int found;
                if (Denominators.TryGetValue(words[i], out found))
                {
                    digits = found;
                    fractionEnd = i;
                    break;
                }
            }

            long fractionPart;
            if (!TryReadInteger(words, split + 1, fractionEnd, out fractionPart))
            {
                reason = "дробная часть не распознана";
                return false;
            }

            if (digits == 0)
            {
                // «запятая сто двадцать пять» — три знака, как в самом числе.
                digits = fractionPart == 0 ? 1 : fractionPart.ToString(CultureInfo.InvariantCulture).Length;
            }

            double scale = Math.Pow(10.0, digits);
            double result = integerPart + fractionPart / scale;

            value = (float)(negative ? -result : result);
            reason = "";
            return true;

        }

        /// <summary>
        /// ПРИВЕСТИ ФРАЗУ К ЕДИНОМУ ВИДУ перед разбором: строчные буквы, «ё» → «е», лишние пробелы
        /// убраны, «−» и «—» → «-», запятая оставлена как есть. Отдельной функцией — потому что
        /// именно здесь чаще всего и ошибаются, и это проверяется тестом.
        /// </summary>
        public static string Normalize(string phrase)
        {
            if (string.IsNullOrEmpty(phrase)) return "";

            var sb = new StringBuilder(phrase.Length);
            bool space = false;

            for (int i = 0; i < phrase.Length; i++)
            {
                char c = char.ToLowerInvariant(phrase[i]);
                if (c == 'ё') c = 'е';
                if (c == '−' || c == '–' || c == '—') c = '-';

                if (char.IsWhiteSpace(c))
                {
                    space = sb.Length > 0;
                    continue;
                }

                if (space) { sb.Append(' '); space = false; }
                sb.Append(c);
            }

            return sb.ToString();
        }

        /// <summary>Это слово-разделитель целой и дробной части?</summary>
        private static bool IsSeparator(string word)
        {
            for (int i = 0; i < Separators.Length; i++)
                if (Separators[i] == word) return true;
            return false;
        }

        /// <summary>
        /// ПРОЧИТАТЬ ЦЕЛОЕ ЧИСЛО ИЗ СЛОВ. Сложение для сотен/десятков/единиц и умножение на тысячу:
        /// «сто двадцать пять» = 100 + 20 + 5; «две тысячи пятьсот» = 2 × 1000 + 500.
        /// </summary>
        private static bool TryReadInteger(string[] words, int from, int to, out long value)
        {
            value = 0;
            long current = 0;
            bool any = false;

            for (int i = from; i < to; i++)
            {
                string word = words[i];
                if (word.Length == 0) continue;

                if (word == "тысяча" || word == "тысячи" || word == "тысяч")
                {
                    value += (current == 0 ? 1 : current) * 1000;
                    current = 0;
                    any = true;
                    continue;
                }

                long part;
                if (NumberWord(word, out part))
                {
                    current += part;
                    any = true;
                }
            }

            value += current;
            return any;
        }

        /// <summary>Значение одного слова-числительного. `false` — слово не числительное.</summary>
        private static bool NumberWord(string word, out long value)
        {
            switch (word)
            {
                case "ноль": case "нуль": value = 0; return true;
                case "один": case "одна": case "одно": value = 1; return true;
                case "два": case "две": value = 2; return true;
                case "три": value = 3; return true;
                case "четыре": value = 4; return true;
                case "пять": value = 5; return true;
                case "шесть": value = 6; return true;
                case "семь": value = 7; return true;
                case "восемь": value = 8; return true;
                case "девять": value = 9; return true;
                case "десять": value = 10; return true;
                case "одиннадцать": value = 11; return true;
                case "двенадцать": value = 12; return true;
                case "тринадцать": value = 13; return true;
                case "четырнадцать": value = 14; return true;
                case "пятнадцать": value = 15; return true;
                case "шестнадцать": value = 16; return true;
                case "семнадцать": value = 17; return true;
                case "восемнадцать": value = 18; return true;
                case "девятнадцать": value = 19; return true;
                case "двадцать": value = 20; return true;
                case "тридцать": value = 30; return true;
                case "сорок": value = 40; return true;
                case "пятьдесят": value = 50; return true;
                case "шестьдесят": value = 60; return true;
                case "семьдесят": value = 70; return true;
                case "восемьдесят": value = 80; return true;
                case "девяносто": value = 90; return true;
                case "сто": value = 100; return true;
                case "двести": value = 200; return true;
                case "триста": value = 300; return true;
                case "четыреста": value = 400; return true;
                case "пятьсот": value = 500; return true;
                case "шестьсот": value = 600; return true;
                case "семьсот": value = 700; return true;
                case "восемьсот": value = 800; return true;
                case "девятьсот": value = 900; return true;
                default: value = 0; return false;
            }
        }
    }
}
