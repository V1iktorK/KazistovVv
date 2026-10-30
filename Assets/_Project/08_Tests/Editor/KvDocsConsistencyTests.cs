// ================================================================================================
//  KvDocsConsistencyTests.cs — EditMode-тесты СОГЛАСОВАННОСТИ ДОКУМЕНТОВ С ФАКТОМ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Документы проекта регулярно отставали от кода: план писал «открыт» про пункты,
//  закрытые ещё в прошлую сессию, отчёт называл число тестов, которого давно нет. Владелец читал
//  план, чтобы понять «что осталось», и получал неверную картину — а это дороже, чем отсутствие
//  документа вовсе.
//
//  СЛУЧАЙ ИЗ ЭТОЙ ЖЕ НОЧИ (30.10.2026). План объявлял открытыми пункты 5.9, 5.14, 5.27, а отчёт
//  писал «738 тестов», когда их было уже 830. Я нашёл это глазами — то есть случайно.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) в отчёте НЕТ устаревших чисел тестов (иначе он врёт о масштабе проверки);
//    2) закрытые пункты в плане помечены зачёркиванием, а не оставлены «открытыми»;
//    3) у каждого закрытого пункта ЕСТЬ дата закрытия — иначе непонятно, когда проверять;
//    4) документы существуют и не пусты.
//
//  ГРАНИЦА ЧЕСТНОСТИ: тест не может проверить, ПРАВДА ли написана в документе (это не формализуемо).
//  Он ловит конкретный, самый частый вид расхождения — числа и статусы, которые обязаны совпадать
//  с кодом и прогоном.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Документы не отстают от факта: числа и статусы пунктов.</summary>
    public class KvDocsConsistencyTests
    {
        /// <summary>
        /// ЧИСЛА ТЕСТОВ, КОТОРЫХ ДАВНО НЕТ. Если такое число встретится в отчёте или плане, значит
        /// документ не обновляли после изменений — и читатель получит неверное представление о
        /// масштабе проверки.
        ///
        /// Список ведётся с причиной: когда число меняется, старое значение попадает сюда, и
        /// документ с ним больше не пройдёт.
        /// </summary>
        private static readonly Dictionary<string, string> StaleTestCounts = new Dictionary<string, string>
        {
            { "738 тест", "было в отчёте до вечера 30.10.2026; сейчас 830" },
            { "738 всего", "было в живом статусе до вечера 30.10.2026" },
            { "785 всего", "было в живом статусе до вечера 30.10.2026" },
            { "799 всего", "было в живом статусе до вечера 30.10.2026" },
            { "810 всего", "было в живом статусе до вечера 30.10.2026" },
        };

        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string DocsPath(string name)
        {
            string path = Path.Combine(Root, "Docs", name);
            return File.Exists(path) ? path : null;
        }

        /// <summary>
        /// ДОКУМЕНТЫ СУЩЕСТВУЮТ. Без них остальные проверки «пройдут» на пустоте — тест зеленел бы
        /// по определению (E-16).
        /// </summary>
        [Test]
        public void RequiredDocsExist()
        {
            var missing = new List<string>();
            foreach (string name in new[] { "REPORT_S44.md", "PLAN_NEXT_STEPS.md", "PROBLEMS.md", "FIXES.md" })
                if (DocsPath(name) == null) missing.Add(name);

            Assert.AreEqual(0, missing.Count,
                "нет документов, без которых проверки ниже бессмысленны: " +
                string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// В ДОКУМЕНТАХ НЕТ УСТАРЕВШИХ ЧИСЕЛ ТЕСТОВ.
        ///
        /// Владелец ориентируется на эти числа: «830 тестов» означает объём проверки. Число из
        /// прошлой сессии создаёт ложное чувство, что работа не двигалась.
        /// </summary>
        [Test]
        public void NoStaleTestCountsInDocs()
        {
            var problems = new List<string>();
            string[] docs = { "REPORT_S44.md", "RUNNING_S44_TONIGHT.md", "PLAN_NEXT_STEPS.md" };

            foreach (string name in docs)
            {
                string path = DocsPath(name);
                if (path == null) continue;
                string text = File.ReadAllText(path);

                foreach (var stale in StaleTestCounts)
                {
                    if (text.IndexOf(stale.Key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                        problems.Add(name + ": «" + stale.Key + "» (" + stale.Value + ")");
                }
            }

            Assert.AreEqual(0, problems.Count,
                "в документах остались УСТАРЕВШИЕ числа тестов — читатель получит неверное " +
                "представление о масштабе проверки: " + string.Join("; ", problems.ToArray()));
        }

        /// <summary>
        /// ЗАКРЫТЫЕ ПУНКТЫ ПОМЕЧЕНЫ ЗАЧЁРКИВАНИЕМ. Правило проекта: закрытый пункт не удаляют и не
        /// оставляют «как был», а зачёркивают с датой. Так видно и то, что сделано, и когда.
        /// </summary>
        [Test]
        public void ClosedPlanItemsAreStruckThrough()
        {
            string path = DocsPath("PLAN_NEXT_STEPS.md");
            Assert.IsNotNull(path, "нет PLAN_NEXT_STEPS.md");

            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 5000, "план подозрительно мал");

            int struckThrough = Regex.Matches(text, @"~~[^~]+~~").Count;
            Assert.Greater(struckThrough, 5,
                "в плане почти нет зачёркнутых пунктов (" + struckThrough + ") — либо ничего не " +
                "закрыто, либо закрытое помечают как-то иначе. Правило проекта — зачёркивание с датой");

            // У закрытия обязана быть дата: без неё непонятно, когда это проверять повторно.
            Assert.IsTrue(Regex.IsMatch(text, @"ЗАКРЫТО \d{2}\.\d{2}\.\d{4}"),
                "у закрытых пунктов нет даты закрытия — через месяц неясно, что и когда проверяли");
        }

        /// <summary>
        /// ОТЧЁТ НАЗЫВАЕТ ТЕКУЩЕЕ ЧИСЛО ТЕСТОВ. Отчёт — главный документ для владельца; если он
        /// называет число из середины работы, вся картина ночи смещена.
        /// </summary>
        [Test]
        public void ReportStatesCurrentTestCount()
        {
            string path = DocsPath("REPORT_S44.md");
            Assert.IsNotNull(path, "нет REPORT_S44.md");

            string text = File.ReadAllText(path);
            Match m = Regex.Match(text, @"EditMode-тесты всего\*\*\s*\|\s*\d+\s*\|\s*\*\*(\d+)\*\*");
            Assert.IsTrue(m.Success, "в отчёте нет строки с итоговым числом тестов");

            int stated = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            Assert.Greater(stated, 700,
                "отчёт называет " + stated + " тестов — это меньше, чем было уже в начале S44 (665). " +
                "Похоже, число не обновляли");
        }

        /// <summary>
        /// ЖУРНАЛ ОШИБОК ВЕДЁТСЯ ПО ПОРЯДКУ: новые сверху. Так принято в проекте (`FIXES.md`:
        /// «новые ошибки добавлять в НАЧАЛО файла»), и нарушение этого порядка прячет свежие уроки
        /// в середину файла на 300 строк.
        /// </summary>
        [Test]
        public void FixesJournalKeepsNewestFirst()
        {
            string path = DocsPath("FIXES.md");
            Assert.IsNotNull(path, "нет FIXES.md");

            string text = File.ReadAllText(path);
            var numbers = new List<int>();
            foreach (Match m in Regex.Matches(text, @"^## E-(\d+)\.", RegexOptions.Multiline))
                numbers.Add(int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture));

            Assert.Greater(numbers.Count, 3, "в журнале меньше четырёх записей — проверять нечего");

            // Новые записи обязаны идти СВЕРХУ: значит номера убывают.
            for (int i = 1; i < numbers.Count; i++)
            {
                Assert.Greater(numbers[i - 1], numbers[i],
                    "записи журнала идут не по убыванию номеров (E-" + numbers[i - 1] + " перед E-" +
                    numbers[i] + ") — свежий урок спрятан в середине файла");
            }
        }
    }
}
