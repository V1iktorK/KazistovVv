// ================================================================================================
//  KvLocalizationParityTests.cs — EditMode-тесты ПОЛНОТЫ ПЕРЕВОДОВ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Проект переводится на семь языков. Общий тест уже проверяет, что код не
//  запрашивает несуществующих ключей. Но он НЕ проверяет обратную сторону: что во всех языках
//  ОДИН И ТОТ ЖЕ набор ключей. Если ключ есть в русском и пропал в немецком, оператор увидит
//  русский текст — и не поймёт, почему.
//
//  ПРОВЕРЕНО 30.10.2026: во всех семи словарях ровно 535 ключей, паритет полный, пустых значений
//  и кириллицы в нерусских языках нет. Это надо закрепить: добавить ключ в один словарь и забыть
//  в остальных — самая частая ошибка при переводе.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) наборы ключей СОВПАДАЮТ во всех семи языках;
//    2) НЕТ ПУСТЫХ значений (пустая строка = «перевода нет», но ключ «есть»);
//    3) в нерусских языках НЕТ КИРИЛЛИЦЫ (признак скопированного русского текста);
//    4) одинаковых текстов на разных языках не больше разумной доли — иначе часть «перевода» это
//       копия английского.
//
//  ГРАНИЦА ЧЕСТНОСТИ: совпадение текстов само по себе НЕ ошибка. В проекте есть настоящие
//  интернационализмы (`Robot`, `Export`, `START`, название продукта) — поэтому проверка стоит
//  ПОРОГОМ, а не запретом. Иначе тест требовал бы «перевести» слово `Robot` на немецкий любой ценой.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Полнота и согласованность семи словарей.</summary>
    public class KvLocalizationParityTests
    {
        private static readonly string[] Languages = { "ru", "en", "zh", "es", "de", "fr", "ja" };

        /// <summary>
        /// ДОЛЯ ТЕКСТОВ, КОТОРЫЕ МОГУТ СОВПАДАТЬ С АНГЛИЙСКИМИ. Сейчас максимум — 22 из 535 (4 %,
        /// французский, где `Robot`, `Service`, `Tables` пишутся так же). Порог 10 % оставляет запас
        /// на новые интернационализмы, но ловит «скопировали английский во все языки».
        /// </summary>
        private const double MaxEnglishCopyShare = 0.10;

        private static string Folder
        {
            get { return Path.Combine(Application.dataPath, "StreamingAssets", "kazistovvv_i18n"); }
        }

        /// <summary>Читает словарь: ключ → текст. Формат — JSON-массив объектов `{key, text}`.</summary>
        private static Dictionary<string, string> Load(string language)
        {
            var map = new Dictionary<string, string>();
            string path = Path.Combine(Folder, language + ".json");
            if (!File.Exists(path)) return map;

            string text = File.ReadAllText(path);
            // Разбор парами «ключ: значение» — без внешнего JSON-парсера, чтобы тест не зависел
            // от версии библиотеки в проекте.
            foreach (Match m in Regex.Matches(text,
                @"""key""\s*:\s*""([^""]+)""\s*,\s*""text""\s*:\s*""((?:[^""\\]|\\.)*)"""))
            {
                map[m.Groups[1].Value] = Regex.Unescape(m.Groups[2].Value);
            }
            return map;
        }

        /// <summary>
        /// ВСЕ СЕМЬ СЛОВАРЕЙ ЧИТАЮТСЯ. Без этой проверки следующие тесты «пройдут» на пустых
        /// словарях — зелёные по определению (E-16).
        /// </summary>
        [Test]
        public void AllDictionariesAreReadable()
        {
            var problems = new List<string>();
            foreach (string language in Languages)
            {
                Dictionary<string, string> map = Load(language);
                if (map.Count < 100) problems.Add(language + " (" + map.Count + " ключей)");
            }

            Assert.AreEqual(0, problems.Count,
                "словари не прочитаны или подозрительно малы: " + string.Join(", ", problems.ToArray()));
        }

        // ---------------------------------------------------------------- 1. ПАРИТЕТ

        /// <summary>
        /// НАБОРЫ КЛЮЧЕЙ СОВПАДАЮТ. Эталон — русский (он основной). Ключ, добавленный только в него,
        /// в остальных шести языках покажет русский текст.
        /// </summary>
        [Test]
        public void AllLanguagesHaveTheSameKeySet()
        {
            var baseMap = Load("ru");
            Assert.Greater(baseMap.Count, 100, "русский словарь не прочитан");

            var problems = new List<string>();
            foreach (string language in Languages)
            {
                if (language == "ru") continue;
                Dictionary<string, string> map = Load(language);

                var missing = new List<string>();
                foreach (string key in baseMap.Keys)
                    if (!map.ContainsKey(key)) missing.Add(key);

                var extra = new List<string>();
                foreach (string key in map.Keys)
                    if (!baseMap.ContainsKey(key)) extra.Add(key);

                if (missing.Count > 0) problems.Add(language + ": нет " + missing.Count + " (" +
                                                    string.Join(", ", missing.GetRange(0,
                                                        System.Math.Min(3, missing.Count)).ToArray()) + ")");
                if (extra.Count > 0) problems.Add(language + ": лишних " + extra.Count);
            }

            Assert.AreEqual(0, problems.Count,
                "словари разошлись по набору ключей — часть текста будет на другом языке: " +
                string.Join("; ", problems.ToArray()));
        }

        // ---------------------------------------------------------------- 2. ПУСТЫЕ

        /// <summary>
        /// НЕТ ПУСТЫХ ЗНАЧЕНИЙ. Пустая строка — это «ключ есть, перевода нет»: интерфейс покажет
        /// пустое место вместо подписи, и понять, почему кнопка безымянная, будет трудно.
        /// </summary>
        [Test]
        public void NoEmptyTranslations()
        {
            var problems = new List<string>();
            foreach (string language in Languages)
            {
                foreach (var pair in Load(language))
                {
                    if (pair.Value == null || pair.Value.Trim().Length == 0)
                        problems.Add(language + ":" + pair.Key);
                }
            }

            Assert.AreEqual(0, problems.Count,
                "пустые переводы (в интерфейсе будет пустое место): " +
                string.Join(", ", problems.ToArray()));
        }

        // ---------------------------------------------------------------- 3. КИРИЛЛИЦА

        /// <summary>
        /// В НЕРУССКИХ ЯЗЫКАХ НЕТ КИРИЛЛИЦЫ. Это прямой признак скопированного русского текста —
        /// самая частая ошибка при добавлении перевода «на скорость».
        ///
        /// Проверено 30.10.2026: ни одной такой записи. Закрепляем, чтобы не появилось.
        /// </summary>
        [Test]
        public void NonRussianLanguagesHaveNoCyrillic()
        {
            var problems = new List<string>();
            foreach (string language in Languages)
            {
                if (language == "ru") continue;
                foreach (var pair in Load(language))
                {
                    if (Regex.IsMatch(pair.Value ?? "", @"[\u0400-\u04FF]"))
                        problems.Add(language + ":" + pair.Key + " = «" + pair.Value + "»");
                }
            }

            Assert.AreEqual(0, problems.Count,
                "в нерусских языках остался русский текст — это пропущенный перевод: " +
                string.Join("; ", problems.ToArray()));
        }

        // ---------------------------------------------------------------- 4. КОПИИ АНГЛИЙСКОГО

        /// <summary>
        /// НЕ СЛИШКОМ МНОГО ТЕКСТОВ, СОВПАДАЮЩИХ С АНГЛИЙСКИМИ.
        ///
        /// Совпадение само по себе НЕ ошибка: `Robot`, `Export`, `START`, название продукта пишутся
        /// одинаково почти на всех языках. Ошибка — когда СОВПАДАЕТ ВСЁ (скопировали английский и
        /// назвали это переводом). Граница 10 % отделяет одно от другого.
        /// </summary>
        [Test]
        public void NotTooManyTextsAreCopiedFromEnglish()
        {
            var english = Load("en");
            Assert.Greater(english.Count, 100, "английский словарь не прочитан");

            var problems = new List<string>();
            foreach (string language in Languages)
            {
                if (language == "en") continue;
                Dictionary<string, string> map = Load(language);

                int same = 0;
                foreach (var pair in english)
                {
                    string other;
                    if (map.TryGetValue(pair.Key, out other) && other == pair.Value &&
                        !string.IsNullOrWhiteSpace(pair.Value))
                        same++;
                }

                double share = (double)same / english.Count;
                if (share > MaxEnglishCopyShare)
                    problems.Add(language + ": " + same + " из " + english.Count +
                                 " (" + (share * 100).ToString("0") + " %)");
            }

            Assert.AreEqual(0, problems.Count,
                "слишком много текстов совпадает с английскими — похоже, часть «перевода» это " +
                "копия (допустимо до " + (MaxEnglishCopyShare * 100).ToString("0") + " %): " +
                string.Join("; ", problems.ToArray()));
        }

        /// <summary>
        /// САМОПРОВЕРКА: разбор действительно видит различия. Если бы он всегда возвращал «всё
        /// совпадает», проверки выше стали бы бессмысленными.
        /// </summary>
        [Test]
        public void ParserSeesRealDifferences()
        {
            var russian = Load("ru");
            var english = Load("en");
            Assert.Greater(russian.Count, 100);
            Assert.Greater(english.Count, 100);

            // Хотя бы половина текстов должна РАЗЛИЧАТЬСЯ: это разные языки.
            int different = 0;
            foreach (var pair in russian)
            {
                string other;
                if (english.TryGetValue(pair.Key, out other) && other != pair.Value) different++;
            }

            Assert.Greater(different, russian.Count / 2,
                "русский и английский словари почти не различаются (" + different + " различий из " +
                russian.Count + ") — вероятно, разбор сравнивает не то");
        }
    }
}
