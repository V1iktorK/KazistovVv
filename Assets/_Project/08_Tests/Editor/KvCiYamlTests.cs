// ================================================================================================
//  KvCiYamlTests.cs — EditMode-тесты СИНТАКСИСА КОНФИГУРАЦИИ CI (сессия S44).
//
//  ОТКУДА ЗАДАЧА. CI нельзя проверить «на живом»: у владельца нет секретов лицензии Unity, а прогон
//  требует публикации. Значит ошибка синтаксиса в YAML обнаружится ЛИБО при первом пуше, ЛИБО
//  никогда — а «никогда» здесь означает «CI не работает, и об этом не знают».
//
//  ЧТО ПРОВЕРЯЕТСЯ БЕЗ ЗАПУСКА. Синтаксис YAML проверяется парсером Unity (`YamlDotNet` есть в
//  проекте как зависимость? — нет, поэтому проверяем СТРУКТУРУ сами, по правилам, которые в этом
//  файле реально нужны):
//    1) табы в отступах — САМАЯ частая ошибка YAML: GitHub отклоняет файл целиком;
//    2) задачи объявлены на одном уровне и названы правильно;
//    3) у итоговой задачи `needs` включает все проверочные задачи (иначе она пройдёт, пока
//       проверки ещё идут, и вердикт будет ложным);
//    4) `if: always()` у итоговой задачи — без него она не запустится после провала проверок;
//    5) у каждой задачи есть `runs-on` — без него GitHub не знает, где её запускать.
//
//  ГРАНИЦА ЧЕСТНОСТИ: это не полноценный YAML-парсер. Полный парсер проверен отдельным запуском
//  при разработке (PyYAML: файл корректен). Здесь — защита от типовых правок, которые ломают CI.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Синтаксис и структура конфигурации CI.</summary>
    public class KvCiYamlTests
    {
        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string WorkflowPath
        {
            get
            {
                string path = Path.Combine(Root, ".github", "workflows", "unity-tests.yml");
                return File.Exists(path) ? path : null;
            }
        }

        private static string[] Lines()
        {
            string path = WorkflowPath;
            if (path == null) return new string[0];
            return File.ReadAllLines(path);
        }

        /// <summary>
        /// В ОТСТУПАХ НЕТ ТАБОВ. YAML запрещает табы для отступов; GitHub в этом случае отклоняет
        /// файл ЦЕЛИКОМ и не сообщает, что именно не так. Ошибка появляется от копирования из
        /// редактора, где таб выглядит как пробелы, — глазами её не увидеть.
        /// </summary>
        [Test]
        public void NoTabsInIndentation()
        {
            Assert.IsNotNull(WorkflowPath, "нет .github/workflows/unity-tests.yml");

            var bad = new List<string>();
            string[] lines = Lines();
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0) continue;
                if (line.StartsWith("\t") || Regex.IsMatch(line, @"^ +\t"))
                    bad.Add("строка " + (i + 1) + ": " + line.Trim());
            }

            Assert.AreEqual(0, bad.Count,
                "в отступах YAML есть ТАБЫ — GitHub отклонит файл целиком: " +
                string.Join("; ", bad.ToArray()));
        }

        /// <summary>
        /// ТРИ ЗАДАЧИ НА ОДНОМ УРОВНЕ. Если уровень съедет, GitHub прочитает задачу как свойство
        /// предыдущей, и она молча не запустится.
        /// </summary>
        [Test]
        public void AllJobsAreDeclaredAtTheSameLevel()
        {
            string[] lines = Lines();
            var jobs = new List<string>();
            bool inJobs = false;

            foreach (string line in lines)
            {
                if (Regex.IsMatch(line, @"^jobs:\s*$")) { inJobs = true; continue; }
                if (!inJobs) continue;
                // Задача — ровно два пробела отступа, имя, двоеточие.
                Match m = Regex.Match(line, @"^  ([a-z][\w-]*):\s*$");
                if (m.Success) jobs.Add(m.Groups[1].Value);
            }

            Assert.GreaterOrEqual(jobs.Count, 3,
                "в конфигурации меньше трёх задач — ожидались проверки без Unity, тесты и итог. " +
                "Найдено: " + string.Join(", ", jobs.ToArray()));

            StringAssert.Contains("static-checks", string.Join(",", jobs.ToArray()));
            StringAssert.Contains("gate", string.Join(",", jobs.ToArray()));
        }

        /// <summary>
        /// У КАЖДОЙ ЗАДАЧИ ЕСТЬ `runs-on`. Без него GitHub не знает, на какой системе запускать, и
        /// задача не выполнится — при этом файл останется синтаксически верным.
        ///
        /// ЧЕСТНО О ПЕРВОЙ ПОПЫТКЕ (30.10.2026). Сначала задачи считались регуляркой «два пробела +
        /// имя + двоеточие», и она поймала `env:` и `steps:` — обычные свойства, а не задачи. Тест
        /// объявил «у некоторых задач нет runs-on», хотя их было ровно три и все с `runs-on`.
        /// Поэтому список задач берётся ЯВНО: так проверка не зависит от формы записи файла.
        /// </summary>
        [Test]
        public void EveryJobHasRunsOn()
        {
            string text = File.ReadAllText(WorkflowPath);
            string[] knownJobs = { "static-checks", "editmode-tests", "gate" };

            var missing = new List<string>();
            foreach (string job in knownJobs)
            {
                // Тело задачи: от «  <имя>:» до следующей задачи того же уровня.
                Match m = Regex.Match(text,
                    @"^  " + Regex.Escape(job) + @":(.*?)(?=^  [a-z][\w-]*:\s*$|\z)",
                    RegexOptions.Multiline | RegexOptions.Singleline);
                if (!m.Success) { missing.Add(job + " (задачи нет в файле)"); continue; }
                if (!Regex.IsMatch(m.Groups[1].Value, @"runs-on:"))
                    missing.Add(job + " (нет runs-on)");
            }

            Assert.AreEqual(0, missing.Count,
                "у этих задач нет `runs-on` — GitHub не запустит их и не скажет почему: " +
                string.Join("; ", missing.ToArray()));
        }

        /// <summary>
        /// ИТОГОВАЯ ЗАДАЧА ЖДЁТ ВСЕ ПРОВЕРКИ. Если `needs` неполный, итог подводится, пока тесты ещё
        /// идут, и вердикт окажется ложным: «успех» при непроверенном коде.
        /// </summary>
        [Test]
        public void GateDependsOnAllCheckJobs()
        {
            string text = File.ReadAllText(WorkflowPath);

            Match gate = Regex.Match(text, @"^  gate:(.*?)(?=^  [a-z][\w-]*:\s*$|\z)",
                RegexOptions.Multiline | RegexOptions.Singleline);
            Assert.IsTrue(gate.Success, "в конфигурации нет итоговой задачи gate");

            string body = gate.Groups[1].Value;
            Assert.IsTrue(Regex.IsMatch(body, @"needs:\s*\[[^\]]*static-checks"),
                "итоговая задача не ждёт проверок без Unity — вердикт будет ложным");
            Assert.IsTrue(Regex.IsMatch(body, @"needs:\s*\[[^\]]*editmode-tests"),
                "итоговая задача не ждёт тестов — вердикт будет ложным");
        }

        /// <summary>
        /// ИТОГОВАЯ ЗАДАЧА ЗАПУСКАЕТСЯ ВСЕГДА. Без `if: always()` она не выполнится после провала
        /// проверок — то есть самый важный случай (что-то сломалось) останется БЕЗ вердикта.
        /// </summary>
        [Test]
        public void GateRunsEvenWhenChecksFail()
        {
            string text = File.ReadAllText(WorkflowPath);

            Match gate = Regex.Match(text, @"^  gate:(.*?)(?=^  [a-z][\w-]*:\s*$|\z)",
                RegexOptions.Multiline | RegexOptions.Singleline);
            Assert.IsTrue(gate.Success);

            StringAssert.Contains("always()", gate.Groups[1].Value,
                "итоговая задача не запускается при провале проверок — худший случай останется без вердикта");
        }

        /// <summary>
        /// ЕСТЬ РУЧНОЙ ЗАПУСК. `workflow_dispatch` позволяет прогнать проверки кнопкой — это
        /// единственный способ проверить CI до того, как владелец настроит секреты лицензии.
        /// </summary>
        [Test]
        public void ManualRunIsAvailable()
        {
            string text = File.ReadAllText(WorkflowPath);
            StringAssert.Contains("workflow_dispatch", text,
                "нет ручного запуска — проверить CI без нового коммита нельзя");
        }
    }
}
