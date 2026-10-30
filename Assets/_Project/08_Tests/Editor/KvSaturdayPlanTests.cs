// ================================================================================================
//  KvSaturdayPlanTests.cs — EditMode-тесты СООТВЕТСТВИЯ ПЛАНА НА СУББОТУ КОДУ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. `Docs/ПЛАН_НА_СУББОТУ_MR.md` — пошаговая инструкция для работы со шлемом. В ней
//  сказано: «собери такой командой», «ищи в журнале такую строку», «проверь такой признак».
//
//  ЧЕМ ЭТО ОПАСНО, ЕСЛИ РАЗОЙДЁТСЯ. Суббота — единственное окно с устройством. Если инструкция
//  велит искать строку, которой в журнале нет, или запускать скрипт с другим именем — время уйдёт
//  на выяснение, а не на проверку. Это хуже отсутствия инструкции: человек верит документу.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) все инструменты, названные в плане, СУЩЕСТВУЮТ в `Tools/`;
//    2) все строки журнала, которые план велит искать, РЕАЛЬНО печатаются кодом;
//    3) в плане есть четыре исхода пробы и что делать в каждом (иначе он не готовит к отказу);
//    4) план честно называет, чего ждать не стоит.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>План на субботу соответствует реальному коду и инструментам.</summary>
    public class KvSaturdayPlanTests
    {
        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string PlanPath
        {
            get
            {
                string path = Path.Combine(Root, "Docs", "ПЛАН_НА_СУББОТУ_MR.md");
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
            Assert.IsNotNull(path, "нет Docs/ПЛАН_НА_СУББОТУ_MR.md — инструкции для работы со шлемом");

            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 2000, "план подозрительно мал");
        }

        /// <summary>
        /// ВСЕ ИНСТРУМЕНТЫ ИЗ ПЛАНА СУЩЕСТВУЮТ. План велит запускать скрипты — если имени нет,
        /// человек потратит время окна с устройством на поиск несуществующего файла.
        /// </summary>
        [Test]
        public void AllMentionedToolsExist()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string text = File.ReadAllText(path);
            var missing = new List<string>();

            foreach (Match m in Regex.Matches(text, @"Tools\\([\w\-]+\.ps1)"))
            {
                string tool = m.Groups[1].Value;
                if (!File.Exists(Path.Combine(Root, "Tools", tool))) missing.Add(tool);
            }

            Assert.Greater(Regex.Matches(text, @"Tools\\").Count, 0,
                "в плане нет ни одной ссылки на инструмент — проверка бессмысленна");

            Assert.AreEqual(0, missing.Count,
                "план велит запускать несуществующие инструменты: " + string.Join(", ", missing.ToArray()) +
                ". В окне с устройством это стоит времени");
        }

        /// <summary>
        /// СТРОКИ ЖУРНАЛА, КОТОРЫЕ ПЛАН ВЕЛИТ ИСКАТЬ, ДЕЙСТВИТЕЛЬНО ПЕЧАТАЮТСЯ.
        ///
        /// Проверяются ключевые фрагменты: «возможности MR/AR» (проба) и «РЕЖИМ:» (выбор режима).
        /// Если их переименуют в коде, план станет бесполезным — и это надо узнать ДО субботы.
        /// </summary>
        [Test]
        public void LogLinesFromPlanAreActuallyPrinted()
        {
            string planPath = PlanPath;
            Assert.IsNotNull(planPath);

            string plan = File.ReadAllText(planPath);

            // План обязан ссылаться на обе ключевые строки.
            StringAssert.Contains("возможности MR/AR", plan,
                "план не велит искать строку о возможностях устройства — это главная информация субботы");
            StringAssert.Contains("РЕЖИМ:", plan,
                "план не велит искать строку о выбранном режиме");

            // И эти строки обязаны быть в коде.
            string scripts = Path.Combine(Application.dataPath, "_Project");
            Assert.IsTrue(Directory.Exists(scripts), "нет папки скриптов");

            bool foundCapabilities = false;
            bool foundMode = false;

            foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;

                string text = File.ReadAllText(file);
                if (text.Contains("возможности MR/AR")) foundCapabilities = true;
                if (text.Contains("[KvXr] РЕЖИМ:") || text.Contains("\"[KvXr] РЕЖИМ: \"")) foundMode = true;
            }

            Assert.IsTrue(foundCapabilities,
                "код НЕ печатает строку «возможности MR/AR» — план велит искать то, чего нет в журнале");
            Assert.IsTrue(foundMode,
                "код НЕ печатает строку «[KvXr] РЕЖИМ:» — план велит искать то, чего нет в журнале");
        }

        /// <summary>
        /// ПЛАН ГОТОВИТ К ОТКАЗУ, А НЕ ТОЛЬКО К УСПЕХУ.
        ///
        /// Главная польза инструкции — не «как должно работать», а «что делать, если не работает».
        /// Если в плане только счастливый путь, при первом же отказе он бесполезен.
        /// </summary>
        [Test]
        public void PlanCoversFailureCases()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string plan = File.ReadAllText(path);

            StringAssert.Contains("неизвестно", plan,
                "план не рассматривает исход «не удалось спросить» — а это самый вероятный случай " +
                "при первой пробе на новом устройстве");
            StringAssert.Contains("НЕТ", plan,
                "план не рассматривает случай «возможности нет»");

            Assert.IsTrue(Regex.IsMatch(plan, @"Если не так|если.*не работает|пошло не так",
                    RegexOptions.IgnoreCase),
                "в плане нет раздела «если что-то пошло не так» — при отказе инструкция бесполезна");
        }

        /// <summary>
        /// ПЛАН ЧЕСТНО НАЗЫВАЕТ, ЧЕГО ЖДАТЬ НЕ СТОИТ. Иначе владелец будет ждать от субботы того,
        /// чего в проекте нет (якоря, AR на телефоне, китайский текст в PDF).
        /// </summary>
        [Test]
        public void PlanNamesWhatIsNotAvailable()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string plan = File.ReadAllText(path);

            StringAssert.Contains("ЧЕГО ЖДАТЬ НЕ СТОИТ", plan,
                "в плане нет раздела о том, чего ждать не стоит — владелец будет ждать недоступного");
            StringAssert.Contains("якор", plan,
                "план не упоминает отсутствие пространственных якорей — это ограничение MR");
        }

        /// <summary>
        /// САМОПРОВЕРКА: ссылок на инструменты в плане действительно несколько. Если разбор сломается,
        /// тест выше зеленел бы на пустом списке (E-16).
        /// </summary>
        [Test]
        public void PlanActuallyReferencesTools()
        {
            string path = PlanPath;
            Assert.IsNotNull(path);

            string plan = File.ReadAllText(path);
            int count = Regex.Matches(plan, @"Tools\\[\w\-]+\.ps1").Count;

            Assert.GreaterOrEqual(count, 3,
                "план ссылается всего на " + count + " инструментов — пошаговая инструкция без команд " +
                "полезна только тому, кто уже знает, что делать");
        }
    }
}
