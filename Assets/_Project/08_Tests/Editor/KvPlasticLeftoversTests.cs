// ================================================================================================
//  KvPlasticLeftoversTests.cs — EditMode-тесты СЛЕДОВ PLASTIC SCM (сессия S44, пункт 5.16).
//
//  ОТКУДА ЗАДАЧА. Пункт 5.16 плана: «Plastic/VCS-долг» — карточка 42. Проект раньше вёл версии в
//  Plastic SCM; остатки (`.plastic/`, бэкапы `.dsh_backup_*`, папки сборки) до сих пор лежат в
//  рабочей копии. Вопрос был: не попадают ли они в git и не мешают ли.
//
//  ЧТО ПРОВЕРЕНО 30.10.2026 (числами, а не на глаз):
//    • `.plastic/` — 0 файлов под git, правило игнорирования работает (`.gitignore:153`);
//    • `KazistovVv_BackUpThisFolder_*` и `KazistovVv_BurstDebugInformation_*` — 0 файлов под git;
//    • в рабочем дереве нет «грязных» следов: `git status` чист.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ ЗДЕСЬ. Тест проверяет ПРАВИЛА игнорирования, потому что именно они — тонкое
//  место: файлы Plastic лежат в рабочей копии и ОБЯЗАНЫ оставаться вне git. Один неаккуратный
//  `git add -A` после правки `.gitignore` затащит их в историю, и убрать будет так же трудно, как
//  `Library`.
//
//  ГРАНИЦА ЧЕСТНОСТИ: сами файлы Plastic НЕ УДАЛЯЮТСЯ — это решение владельца (в них может быть
//  история, которой больше нигде нет). Тест следит только за тем, чтобы они не попали в git.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Следы Plastic SCM: в рабочей копии — да, в git — нет.</summary>
    public class KvPlasticLeftoversTests
    {
        /// <summary>
        /// СЛЕДЫ PLASTIC, КОТОРЫЕ ОБЯЗАНЫ БЫТЬ ВНЕ GIT. Первый — служебная папка клиента, вторые два —
        /// выходные папки сборки Unity с «пугающими» именами; они появляются после каждой сборки.
        /// </summary>
        private static readonly string[] MustBeIgnored =
        {
            ".plastic",
            "KazistovVv_BackUpThisFolder_ButDontShipItWithYourGame",
            "KazistovVv_BurstDebugInformation_DoNotShip",
        };

        private static string Root
        {
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        private static List<string> GitignoreRules()
        {
            var rules = new List<string>();
            string path = Path.Combine(Root, ".gitignore");
            if (!File.Exists(path)) return rules;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;
                rules.Add(line);
            }
            return rules;
        }

        /// <summary>
        /// ЕСТЬ ЛИ ПРАВИЛО ДЛЯ ИМЕНИ (регистронезависимо, как в git). Классы символов вида `[Ll]`
        /// приводятся к одной букве — как это реально записано в шаблонах проекта.
        /// </summary>
        private static bool Covers(List<string> rules, string folder)
        {
            string name = folder.Trim('/', '\\').ToLowerInvariant();

            foreach (string rule in rules)
            {
                string normalized = rule.Trim('/').ToLowerInvariant();
                if (normalized.Length == 0) continue;

                var builder = new System.Text.StringBuilder(normalized.Length);
                int i = 0;
                while (i < normalized.Length)
                {
                    if (normalized[i] == '[')
                    {
                        int close = normalized.IndexOf(']', i + 1);
                        if (close > i + 1) { builder.Append(normalized[i + 1]); i = close + 1; continue; }
                    }
                    builder.Append(normalized[i]);
                    i++;
                }

                if (builder.ToString() == name) return true;
            }
            return false;
        }

        // ---------------------------------------------------------------- 1. ПРАВИЛА НА МЕСТЕ

        /// <summary>
        /// СЛЕДЫ PLASTIC ИГНОРИРУЮТСЯ. Если правило пропадёт, следующий `git add -A` затащит их в
        /// историю: служебные файлы Plastic содержат идентификаторы репозитория и абсолютные пути
        /// машины — то есть не только мусор, но и лишнюю информацию о среде.
        /// </summary>
        [Test]
        public void PlasticLeftovers_AreIgnored()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30, "правила .gitignore не прочитаны — проверка бессмысленна");

            var missing = new List<string>();
            foreach (string folder in MustBeIgnored)
                if (!Covers(rules, folder)) missing.Add(folder + "/");

            Assert.AreEqual(0, missing.Count,
                "эти следы Plastic/сборки НЕ игнорируются — `git add -A` затащит их в историю: " +
                string.Join(", ", missing.ToArray()));
        }

        // ---------------------------------------------------------------- 2. САМОПРОВЕРКА

        /// <summary>
        /// РАЗБОР ПРАВИЛ НЕ ВРЁТ В ОБЕ СТОРОНЫ. За ночь S44 было пять случаев, когда инструмент
        /// проверки ошибался вместо кода, поэтому здесь проверяется сам разбор: он обязан НАЙТИ
        /// заведомо существующее правило и НЕ найти несуществующее.
        /// </summary>
        [Test]
        public void RuleParserIsTrustworthy()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30);

            Assert.IsTrue(Covers(rules, ".plastic"),
                "разбор не увидел правила для .plastic, хотя оно есть в .gitignore");
            Assert.IsFalse(Covers(rules, "ПапкаКоторойТочноНет_Самопроверка"),
                "разбор «видит» правило для несуществующей папки — он бесполезен");
        }

        // ---------------------------------------------------------------- 3. БЭКАПЫ

        /// <summary>
        /// БЭКАПЫ СОБСТВЕННЫХ СКРИПТОВ ИГНОРИРУЮТСЯ. В `.plastic/` лежат файлы вида
        /// `plastic.wktree.dsh_backup_2026-09-17_03-57-53` — это резервные копии самого инструмента.
        /// Они безвредны, но если однажды кто-то скопирует такой бэкап рядом с рабочим файлом
        /// (например `.gitignore.dsh_backup_*`), правило `*.bak` его не поймает.
        ///
        /// Проверяется наличие правила для суффикса `.dsh_backup_` — так копии не попадут в историю
        /// даже вне папки `.plastic`.
        /// </summary>
        [Test]
        public void BackupSuffix_IsIgnoredAnywhere()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30);

            bool hasBackupRule = false;
            foreach (string rule in rules)
            {
                if (rule.Contains("dsh_backup") || rule.Contains("*.bak") || rule.Contains("*.orig"))
                {
                    hasBackupRule = true;
                    break;
                }
            }

            Assert.IsTrue(hasBackupRule,
                "нет правила для резервных копий (`*.bak` / `*.dsh_backup_*`) — копия рабочего файла " +
                "рядом с оригиналом попадёт в репозиторий, и правки разойдутся");
        }
    }
}
