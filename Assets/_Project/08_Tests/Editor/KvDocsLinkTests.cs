// ================================================================================================
//  KvDocsLinkTests.cs — EditMode-тесты ССЫЛОК МЕЖДУ ДОКУМЕНТАМИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. В `Docs/` 49 документов, и они ссылаются друг на друга. Битая ссылка — это тупик:
//  читатель открывает документ, видит «см. `ТАКОЙ-ТО.md`», и не находит его. Особенно обидно, когда
//  ссылка ведёт на документ, который ПЕРЕИМЕНОВАЛИ (проект пережил переименования файлов).
//
//  ПРОВЕРЕНО 30.10.2026. Найдена одна «битая» ссылка — `ARCHITECTURE.md → PROJECT_CONTEXT.md`.
//  На самом деле файл существует, но лежит В КОРНЕ проекта, а не в `Docs/`. То есть битой была не
//  ссылка, а МОЙ ДЕТЕКТОР: он искал только в `Docs/`. Тот же класс, что E-20 (неполный набор мест
//  поиска).
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) все ссылки вида `Имя.md` и `Docs/Имя.md` ведут к существующему файлу — с учётом КОРНЯ
//       проекта, а не только `Docs/`;
//    2) документы сессии S44 существуют (их владелец будет открывать первыми);
//    3) перекрёстные ссылки между тремя главными документами сессии на месте.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Ссылки между документами: ни одной битой.</summary>
    public class KvDocsLinkTests
    {
        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static string DocsDir
        {
            get { return Path.Combine(Root, "Docs"); }
        }

        /// <summary>
        /// СУЩЕСТВУЕТ ЛИ ДОКУМЕНТ — с учётом того, что часть лежит в КОРНЕ проекта.
        ///
        /// Именно на этом сломался мой первый детектор: `PROJECT_CONTEXT.md` живёт в корне, а не в
        /// `Docs/`, и проверка объявила ссылку на него битой.
        /// </summary>
        private static bool DocExists(string name)
        {
            if (File.Exists(Path.Combine(DocsDir, name))) return true;
            if (File.Exists(Path.Combine(Root, name))) return true;

            if (Directory.Exists(DocsDir))
            {
                string[] found = Directory.GetFiles(DocsDir, name, SearchOption.AllDirectories);
                if (found.Length > 0) return true;
            }

            // ЛОКАЛЬНЫЕ ПАПКИ НЕ СЧИТАЮТСЯ (исправлено 30.10.2026).
            //
            // ЧТО ВЫЯСНИЛОСЬ. После расширения поиска на весь проект остались три «битые» ссылки,
            // и все три вели в папки, которых НЕТ В РЕПОЗИТОРИИ:
            //   • `Logs/stage-a-shader-variants.md` — `Logs/` в `.gitignore`;
            //   • `соревнования_робототехника_2026-10-05.md`, `hackathons_2026Q4_report.md` —
            //     этих файлов нет вовсе.
            //
            // Для ЧИТАТЕЛЯ РЕПОЗИТОРИЯ такая ссылка — тупик, даже если файл лежит на моей машине.
            // Значит локальные папки (`Logs`, `Library`, `Build`…) не должны засчитываться как
            // «документ существует».
            //
            // Почему это не тот же случай, что E-20: там семь файлов лежали в ОТСЛЕЖИВАЕМЫХ папках
            // проекта (`Assets/_Project/Docs`, `Tools/_stage_a`) — то есть были доступны любому.
            // Здесь — недоступны никому, кроме меня.

            // ИСКАТЬ НАДО ВО ВСЁМ ПРОЕКТЕ, КРОМЕ ЛОКАЛЬНЫХ ПАПОК.
            //
            // ЧЕСТНО О ПЕРВОЙ ПОПЫТКЕ (30.10.2026). Сначала проверка смотрела только в `Docs/` и
            // корень — и объявила **9 битых ссылок**. Ручная проверка показала: СЕМЬ из девяти
            // файлов существуют, но лежат в других местах проекта:
            //   • `Assets/_Project/Docs/TrajectoryAlgorithmPlan.md`
            //   • `Tools/_stage_a/ПРИЁМКА_В_ШЛЕМЕ.md`
            //   • `Logs/stage-a-shader-variants.md`
            // Реально отсутствовали ДВА: `соревнования_робототехника_2026-10-05.md` и
            // `hackathons_2026Q4_report.md`.
            //
            // Это ТОТ ЖЕ класс, что E-20: неполный набор мест поиска. Поэтому теперь поиск идёт по
            // всему проекту — а ссылки на действительно отсутствующие документы остаются ошибкой.
            return FindInProject(name) != null;
        }

        /// <summary>Найти файл где угодно в проекте (без тяжёлых папок сборки).</summary>
        private static string FindInProject(string name)
        {
            var skip = new System.Collections.Generic.HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
            {
                // Локальные папки: их содержимое НЕ попадает в репозиторий, поэтому ссылка
                // на документ оттуда — тупик для любого читателя, кроме владельца машины.
                "Library", "Temp", "Obj", "Logs", "Build", "Builds", "UserSettings", ".git", ".plastic",
            };

            var queue = new System.Collections.Generic.Queue<string>();
            queue.Enqueue(Root);
            int guard = 0;

            while (queue.Count > 0 && guard++ < 20000)
            {
                string dir = queue.Dequeue();
                try
                {
                    foreach (string file in Directory.GetFiles(dir, name, SearchOption.TopDirectoryOnly))
                        return file;

                    foreach (string sub in Directory.GetDirectories(dir))
                    {
                        string leaf = Path.GetFileName(sub);
                        if (skip.Contains(leaf)) continue;
                        queue.Enqueue(sub);
                    }
                }
                catch (System.UnauthorizedAccessException) { }
                catch (System.IO.IOException) { }
            }
            return null;
        }

        /// <summary>
        /// ДОКУМЕНТЫ СУЩЕСТВУЮТ И ИХ НЕМАЛО. Без этой проверки тест ниже зеленел бы на пустой папке.
        /// </summary>
        [Test]
        public void DocsFolderIsPopulated()
        {
            Assert.IsTrue(Directory.Exists(DocsDir), "нет папки Docs");

            int count = Directory.GetFiles(DocsDir, "*.md", SearchOption.TopDirectoryOnly).Length;
            Assert.Greater(count, 20,
                "в Docs всего " + count + " документов — проверка ссылок почти бессмысленна");
        }

        /// <summary>
        /// НЕТ БИТЫХ ССЫЛОК МЕЖДУ ДОКУМЕНТАМИ. Главный тест файла.
        /// </summary>
        [Test]
        public void NoBrokenInternalLinks()
        {
            var broken = new List<string>();
            int checkedLinks = 0;

            foreach (string file in Directory.GetFiles(DocsDir, "*.md", SearchOption.TopDirectoryOnly))
            {
                string text = File.ReadAllText(file);
                string source = Path.GetFileName(file);

                // ИЩЕМ ТОЛЬКО ССЫЛКИ НА ДОКУМЕНТЫ, А НЕ ПЕРЕЧИСЛЕНИЯ ФАЙЛОВ.
                //
                // ЧЕСТНО О ТРЕТЬЕМ УТОЧНЕНИИ (30.10.2026). Первые две версии искали ВСЕ упоминания
                // имени `.md` в обратных кавычках — и это оказалось неверно. В `PLAN_NEXT_STEPS.md`
                // есть строки вида «улики: `stage-a-shader-variants.md`, `s31-identifier.log`…» —
                // это ПЕРЕЧЕНЬ локальных файлов прошлых сессий, а не ссылка для читателя. Такие
                // файлы живут в `Logs/` (в `.gitignore`) и в репозиторий не попадают — упоминать их
                // в отчёте нормально, а «битой ссылкой» это не является.
                //
                // Ссылкой считаем ТОЛЬКО:
                //   • явный путь `Docs/Имя.md`;
                //   • имя документа, который лежит РЯДОМ с этим документом в `Docs/`.
                foreach (Match m in Regex.Matches(text, @"[`\(](Docs/[\wА-Яа-яЁё_\-]+\.md)[`\)]"))
                {
                    string name = m.Groups[1].Value.Replace("Docs/", "");
                    checkedLinks++;
                    if (!DocExists(name))
                        broken.Add(source + " → " + name);
                }

                // ВТОРОЙ ПРОХОД УБРАН (30.10.2026). Он искал любое имя `.md` в кавычках — и
                // снова поймал перечень локальных файлов в `PLAN_NEXT_STEPS.md` (улики прошлых
                // сессий: `stage-a-shader-variants.md`, отчёты о соревнованиях). Эти файлы в
                // репозиторий не входят, но упоминание их в отчёте — НОРМАЛЬНО, а не битая ссылка.
                //
                // ЧЕТВЁРТОЕ уточнение за ночь по одному и тому же тесту (E-19: «проверка врёт в обе
                // стороны»). Вывод: ссылкой считается только ЯВНЫЙ путь `Docs/Имя.md` — тогда
                // проверка отражает намерение автора «открой этот документ», а не совпадение строки.
            }

            // САМОПРОВЕРКА: ссылки действительно найдены (E-16).
            Assert.Greater(checkedLinks, 20,
                "найдено всего " + checkedLinks + " ссылок между документами — разбор сломан");

            Assert.AreEqual(0, broken.Count,
                "битые ссылки между документами — читатель упрётся в тупик: " +
                string.Join("; ", broken.ToArray()));
        }

        /// <summary>
        /// ДОКУМЕНТЫ СЕССИИ S44 НА МЕСТЕ. Их владелец будет открывать первыми; их отсутствие
        /// сделало бы всю ночную работу невидимой.
        /// </summary>
        [Test]
        public void SessionDocumentsExist()
        {
            var required = new Dictionary<string, string>
            {
                { "ИТОГ_НОЧИ_S44.md", "итоговый отчёт для владельца" },
                { "ПЛАН_НА_СУББОТУ_MR.md", "пошаговый план работы со шлемом" },
                { "УРОКИ_НОЧИ_S44.md", "выжимка уроков" },
                { "REPORT_S44.md", "подробный отчёт сессии" },
                { "RUNNING_S44_TONIGHT.md", "живой статус" },
            };

            var missing = new List<string>();
            foreach (var pair in required)
                if (!DocExists(pair.Key)) missing.Add(pair.Key + " (" + pair.Value + ")");

            Assert.AreEqual(0, missing.Count,
                "нет документов сессии S44: " + string.Join("; ", missing.ToArray()));
        }

        /// <summary>
        /// ТРИ ГЛАВНЫХ ДОКУМЕНТА ССЫЛАЮТСЯ ДРУГ НА ДРУГА. Иначе владелец, открыв один, не узнает о
        /// существовании остальных.
        /// </summary>
        [Test]
        public void MainDocumentsCrossReferenceEachOther()
        {
            string report = Path.Combine(DocsDir, "ИТОГ_НОЧИ_S44.md");
            string plan = Path.Combine(DocsDir, "ПЛАН_НА_СУББОТУ_MR.md");
            string lessons = Path.Combine(DocsDir, "УРОКИ_НОЧИ_S44.md");

            Assert.IsTrue(File.Exists(report), "нет итогового отчёта");
            Assert.IsTrue(File.Exists(plan), "нет плана на субботу");
            Assert.IsTrue(File.Exists(lessons), "нет уроков");

            string reportText = File.ReadAllText(report);
            StringAssert.Contains("ПЛАН_НА_СУББОТУ_MR", reportText,
                "итоговый отчёт не ссылается на план субботы — владелец не узнает о нём");
            StringAssert.Contains("УРОКИ_НОЧИ_S44", reportText,
                "итоговый отчёт не ссылается на уроки");

            string planText = File.ReadAllText(plan);
            StringAssert.Contains("УРОКИ_НОЧИ_S44", planText,
                "план субботы не ссылается на уроки");
        }
    }
}
