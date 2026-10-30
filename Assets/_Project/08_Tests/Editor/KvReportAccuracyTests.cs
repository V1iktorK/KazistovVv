// ================================================================================================
//  KvReportAccuracyTests.cs — EditMode-тесты ТОЧНОСТИ ОТЧЁТА (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Отчёт для владельца — главный документ ночи. Если в нём неверные числа, владелец
//  получает ложную картину, и вся работа оценивается неправильно.
//
//  СЛУЧАЙ 30.10.2026. Отчёт утверждал «51 коммит» и «67 файлов тестов», когда их было 90 и 85.
//  Расхождение почти вдвое — и нашёл я его СЛУЧАЙНО, сверяя вручную. При том, что тест
//  `KvDocsConsistencyTests` уже существовал, но проверял только количество тестов.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ — то, что можно посчитать без git и без прогона Unity:
//    1) число файлов тестов, названное в отчёте, совпадает с фактическим;
//    2) в отчёте нет отрицаний, которые уже неправда (например «Assert.Ignore = 13», когда их 0);
//    3) отчёт называет ВСЕ пункты, которые реально закрыты по плану (перечень разделов);
//    4) нет ссылок на несуществующие документы.
//
//  ГРАНИЦА ЧЕСТНОСТИ: число КОММИТОВ проверить из теста нельзя (git недоступен из Unity). Это
//  остаётся на совести того, кто пишет отчёт, — и я сверяю его вручную.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Отчёт для владельца: числа и утверждения совпадают с фактом.</summary>
    public class KvReportAccuracyTests
    {
        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string ReportPath
        {
            get
            {
                string path = Path.Combine(Root, "Docs", "ИТОГ_НОЧИ_S44.md");
                return File.Exists(path) ? path : null;
            }
        }

        /// <summary>Сколько файлов тестов реально лежит в проекте.</summary>
        private static int ActualTestFileCount()
        {
            string tests = Path.Combine(Application.dataPath, "_Project", "08_Tests");
            if (!Directory.Exists(tests)) return 0;
            return Directory.GetFiles(tests, "*.cs", SearchOption.AllDirectories).Length;
        }

        /// <summary>
        /// ОТЧЁТ СУЩЕСТВУЕТ И НЕ ПУСТ. Без этого остальные проверки «пройдут» на пустом файле.
        /// </summary>
        [Test]
        public void ReportExistsAndHasContent()
        {
            string path = ReportPath;
            Assert.IsNotNull(path, "нет Docs/ИТОГ_НОЧИ_S44.md — владельцу нечего читать");

            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 3000,
                "отчёт подозрительно мал (" + text.Length + " символов)");
        }

        /// <summary>
        /// ЧИСЛО ФАЙЛОВ ТЕСТОВ В ОТЧЁТЕ СОВПАДАЕТ С ФАКТОМ.
        ///
        /// Названо «67» при фактических 85 — расхождение, которое владелец не заметит, но которое
        /// делает отчёт ненадёжным целиком: если одно число неверно, каким ещё верить?
        /// </summary>
        [Test]
        public void TestFileCountIsAccurate()
        {
            string path = ReportPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            Match m = Regex.Match(text, @"Файлов тестов\*\*\s*\|\s*[^|]+\|\s*\*\*(\d+)\*\*");
            Assert.IsTrue(m.Success, "в отчёте нет строки с числом файлов тестов");

            int stated = int.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
            int actual = ActualTestFileCount();

            Assert.AreEqual(actual, stated,
                "отчёт называет " + stated + " файлов тестов, а их " + actual +
                ". Если одно число неверно, непонятно, каким ещё верить");
        }

        /// <summary>
        /// В ОТЧЁТЕ НЕТ УСТАРЕВШИХ ОТРИЦАНИЙ. Например «13 самовыключающихся тестов» было правдой
        /// ДО ночи; сейчас их ноль. Такое утверждение читается как текущее состояние.
        /// </summary>
        [Test]
        public void NoStaleNegativeClaims()
        {
            string path = ReportPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);

            // Проверяем: если отчёт утверждает «13 самовыключающихся» как ТЕКУЩЕЕ состояние — это
            // неправда. Упоминание в контексте «было 13, стало 0» — законно, поэтому ищем именно
            // утверждение без противопоставления.
            var stale = new List<string>();

            if (Regex.IsMatch(text, @"\*\*13\*\*\s*\|") && !text.Contains("было 13"))
                stale.Add("«13 самовыключающихся тестов» как текущее состояние");

            Assert.AreEqual(0, stale.Count,
                "в отчёте есть устаревшие утверждения: " + string.Join("; ", stale.ToArray()));
        }

        /// <summary>
        /// ССЫЛКИ НА ДОКУМЕНТЫ СУЩЕСТВУЮТ. Отчёт ссылается на `FIXES.md`, `PROBLEMS.md`, план и
        /// прочее; ссылка в никуда сбивает читателя с толку.
        /// </summary>
        [Test]
        public void ReferencedDocumentsExist()
        {
            string path = ReportPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);

            var missing = new List<string>();
            foreach (Match m in Regex.Matches(text, @"`?(Docs/[\wА-Яа-яЁё_\-.]+\.md)`?"))
            {
                string name = m.Groups[1].Value;
                string full = Path.Combine(Root, name.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) missing.Add(name);
            }

            // Ссылки без префикса Docs/ — тоже проверяем по короткому имени.
            foreach (Match m in Regex.Matches(text, @"\b(FIXES|PROBLEMS|PLAN_NEXT_STEPS|REPORT_S44)\.md\b"))
            {
                string full = Path.Combine(Root, "Docs", m.Groups[1].Value + ".md");
                if (!File.Exists(full) && !missing.Contains("Docs/" + m.Groups[1].Value + ".md"))
                    missing.Add(m.Groups[1].Value + ".md");
            }

            Assert.AreEqual(0, missing.Count,
                "отчёт ссылается на несуществующие документы: " + string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// ОТЧЁТ НАЗЫВАЕТ ГЛАВНЫЕ РАЗДЕЛЫ. Если владелец откроет документ и не найдёт «что решать
        /// вам» или «чего не сделано», отчёт не выполняет свою работу.
        /// </summary>
        [Test]
        public void ReportHasRequiredSections()
        {
            string path = ReportPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);

            var required = new Dictionary<string, string>
            {
                { "ИТОГ В ЦИФРАХ", "сводка результатов" },
                { "ДЕФЕКТ", "перечень найденных дефектов" },
                { "РЕШАТЬ ВАМ", "список решений за владельцем" },
                { "НЕ СДЕЛАНО", "честный перечень того, что не сделано" },
                { "ОШИБК", "признание собственных ошибок" },
            };

            var missing = new List<string>();
            foreach (var pair in required)
                if (text.IndexOf(pair.Key, System.StringComparison.OrdinalIgnoreCase) < 0)
                    missing.Add(pair.Key + " (" + pair.Value + ")");

            Assert.AreEqual(0, missing.Count,
                "в отчёте нет обязательных разделов: " + string.Join("; ", missing.ToArray()) +
                ". Без них документ не отвечает на главные вопросы владельца");
        }
    }
}
