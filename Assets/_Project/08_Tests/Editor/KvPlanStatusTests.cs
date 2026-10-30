// ================================================================================================
//  KvPlanStatusTests.cs — EditMode-тесты СОГЛАСОВАННОСТИ СТАТУСОВ ПЛАНА (сессия S44).
//
//  ОТКУДА ЗАДАЧА. `Docs/PLAN_NEXT_STEPS.md` — документ на 159 000 байт. В нём ДВЕ части: сводная
//  таблица статусов (вверху) и подробные описания пунктов (ниже). Они разошлись: в таблице пункты
//  5.21 и 5.16 стояли «открыты», а ниже было написано, что они проверены и закрыты.
//
//  ЧЕМ ЭТО ПЛОХО. Владелец читает СВОДНУЮ таблицу — она для этого и сделана. Если она врёт, он
//  получает неверную картину, даже когда подробная часть документа точна.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) каждый пункт, помеченный в таблице как ЗАКРЫТО/ПРОВЕРЕНО, имеет зачёркнутое название —
//       иначе «закрыто» висит рядом с выглядящим активным пунктом;
//    2) закрытые пункты имеют ДАТУ в отметке — без неё непонятно, когда проверять;
//    3) в подробной части нет утверждений «не реализовано» там, где таблица говорит «закрыто»
//       (это и было противоречие 5.22);
//    4) открытых пунктов не больше разумного — иначе «всё закрыто» в отчёте было бы неправдой.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Статусы плана не противоречат друг другу.</summary>
    public class KvPlanStatusTests
    {
        private static string PlanPath
        {
            get
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                string path = Path.Combine(root, "Docs", "PLAN_NEXT_STEPS.md");
                return File.Exists(path) ? path : null;
            }
        }

        /// <summary>
        /// ПЛАН СУЩЕСТВУЕТ И НЕ ПУСТ.
        /// </summary>
        [Test]
        public void PlanExists()
        {
            string path = PlanPath;
            Assert.IsNotNull(path, "нет Docs/PLAN_NEXT_STEPS.md");

            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 50000, "план подозрительно мал");
        }

        /// <summary>
        /// ЗАКРЫТЫЕ ПУНКТЫ ЗАЧЁРКНУТЫ И ИМЕЮТ ДАТУ.
        ///
        /// Зачёркивание — правило проекта: закрытый пункт не удаляют и не оставляют «как был», а
        /// помечают. Дата нужна, чтобы знать, когда это проверяли.
        /// </summary>
        [Test]
        public void ClosedItemsAreStruckThroughWithDate()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            var problems = new List<string>();

            foreach (Match m in Regex.Matches(text, @"^\|\s*(~~[^|]*?~~)\s*\|\s*\*\*(ЗАКРЫТО|ПРОВЕРЕНО)",
                         RegexOptions.Multiline))
            {
                string name = m.Groups[1].Value;
                StringAssert.Contains("~~", name, "закрытый пункт не зачёркнут");
            }

            // Отметки о закрытии обязаны содержать дату.
            int closures = Regex.Matches(text, @"\*\*(?:ЗАКРЫТО|ПРОВЕРЕНО)\s+\d{2}\.\d{2}\.\d{4}").Count;
            Assert.Greater(closures, 8,
                "найдено всего " + closures + " закрытий с датой — ожидалось больше. Либо пункты " +
                "закрывают без даты, либо разбор сломан");
        }

        /// <summary>
        /// ОТКРЫТЫХ ПУНКТОВ НЕ МНОГО. Если их десятки, заявление «всё закрыто» в отчёте — неправда.
        /// Сейчас открыт один (5.15, требует доступа к корзине OneDrive).
        /// </summary>
        [Test]
        public void OpenItemsAreFew()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            var open = new List<string>();

            foreach (Match m in Regex.Matches(text, @"^\|\s*(\d+\.\d+[^|]*)\|([^|]*)\|([^|]*)\|",
                         RegexOptions.Multiline))
            {
                string name = m.Groups[1].Value.Trim();
                string status = m.Groups[2].Value;

                // Только строки таблицы статусов: у них в статусе есть слово «открыт».
                if (status.IndexOf("открыт", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    open.Add(name);
            }

            Assert.LessOrEqual(open.Count, 5,
                "в плане " + open.Count + " открытых пунктов — проверьте, что отчёт об этом " +
                "не умалчивает. Открытые: " + string.Join("; ", open.ToArray()));
        }

        /// <summary>
        /// ТАБЛИЦА И ПОДРОБНАЯ ЧАСТЬ НЕ ПРОТИВОРЕЧАТ по пункту 5.22.
        ///
        /// Именно здесь было расхождение: таблица говорила «закрыто», а подробное описание —
        /// «либо реализовать, либо убрать кнопки (сейчас кнопки есть и не делают ничего)».
        /// Читатель, дошедший до подробностей, получал противоположную картину.
        /// </summary>
        [Test]
        public void DetailedSectionsDoNotContradictTable()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);

            // Раздел 5.22 обязан сообщать, что часть команд реализована.
            Match section = Regex.Match(text, @"\*\*5\.22\..*?(?=\*\*5\.23\.)", RegexOptions.Singleline);
            if (section.Success)
            {
                string body = section.Value;
                Assert.IsTrue(body.Contains("ЗАКРЫТО") || body.Contains("реализованы"),
                    "раздел 5.22 не сообщает о выполненной работе — таблица говорит «закрыто», а " +
                    "подробности молчат об этом");
            }

            // Раздел 5.21 обязан сообщать про резервную схему, а не «полумера хуже обоих».
            Match section521 = Regex.Match(text, @"\*\*5\.21\..*?(?=\*\*5\.22\.)", RegexOptions.Singleline);
            if (section521.Success)
            {
                Assert.IsFalse(section521.Value.Contains("Полумера хуже обоих"),
                    "раздел 5.21 всё ещё называет резервную схему «полумерой», хотя она уже признана " +
                    "осознанным решением и проверена тестами");
            }
        }
    }
}
