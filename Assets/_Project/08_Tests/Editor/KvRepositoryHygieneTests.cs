// ================================================================================================
//  KvRepositoryHygieneTests.cs — EditMode-тесты ГИГИЕНЫ РЕПОЗИТОРИЯ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Пункт 5.14 плана: «ревизия репозитория». Вопрос был не «красиво ли», а
//  конкретный: не попадают ли в git артефакты, которые там быть не должны. Проверено 30.10.2026:
//  `Library` (93 632 файла!), `Logs`, `UserSettings`, `Build`, `Builds` — все вне git. Правила на
//  месте. Это надо ЗАКРЕПИТЬ, чтобы однажды случайный `git add -f` или правка `.gitignore` не
//  затащили в историю гигабайты.
//
//  ЧЕМ ЭТО ОПАСНО НА ПРАКТИКЕ. Один коммит с `Library/` — это ~10 ГБ в истории. Убрать их потом
//  можно только переписыванием истории (`git filter-repo`), которое ломает клоны у всех. Дешевле
//  — тест.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) `.gitignore` содержит правила на КАЖДЫЙ артефакт Unity (регистронезависимо, как git);
//    2) `keystore`, `gradle`, `local.properties` игнорируются (секреты подписи);
//    3) наш собственный мусор (`_ui_audit`, `Logs/ci-*`) — тоже;
//    4) `.gitattributes` подключает LFS для крупных бинарников (иначе история распухнет).
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Гигиена репозитория: артефакты и секреты не должны попадать в git.</summary>
    public class KvRepositoryHygieneTests
    {
        /// <summary>
        /// ПАПКИ-АРТЕФАКТЫ UNITY, КОТОРЫЕ ОБЯЗАНЫ БЫТЬ В `.gitignore`.
        ///
        /// `Library` — самая дорогая: у этого проекта она 93 632 файла. Попадёт в историю — её
        /// оттуда не вынуть без переписывания, которое ломает все клоны.
        /// </summary>
        private static readonly string[] ArtifactFolders =
        {
            "Library", "Temp", "Obj", "Logs", "UserSettings", "Build", "Builds",
            "MemoryCaptures", ".vs", ".gradle",
        };

        /// <summary>СЕКРЕТЫ ПОДПИСИ — не должны попадать в git ни при каких обстоятельствах.</summary>
        private static readonly string[] SecretPatterns =
        {
            "keystore", "*.keystore", "*.jks", "gradle.properties", "local.properties",
        };

        private static string Root
        {
            // Application.dataPath = <корень проекта>/Assets, поэтому корень — на уровень выше.
            get { return Path.GetDirectoryName(Application.dataPath); }
        }

        /// <summary>Строки `.gitignore` без пустых и комментариев.</summary>
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
        /// ЕСТЬ ЛИ ПРАВИЛО, ПОКРЫВАЮЩЕЕ ИМЯ. Git сравнивает ПУТИ РЕГИСТРОНЕЗАВИСИМО, а в шаблонах
        /// проекта это ещё и записано диапазонами: `/[Ll]ibrary/`, `/[Uu]ser[Ss]ettings/`.
        ///
        /// КАК ПРИВОДИМ К СРАВНЕНИЮ. Каждый класс `[Ll]` означает «любая из этих букв», поэтому
        /// заменяем его на ПЕРВУЮ букву класса: `[Ll]ibrary` → `library`. Так шаблон становится
        /// обычным именем в нижнем регистре, и сравнение сводится к равенству.
        ///
        /// ЧЕСТНО О ПЕРВОЙ ПОПЫТКЕ (30.10.2026). Сначала здесь был разбор через `Regex.Escape` и
        /// обратную замену — он не совпал ни с одним правилом, и тест объявил «всё не
        /// игнорируется», хотя `git ls-files` показывал ОБРАТНОЕ: 93 632 файла `Library` вне git.
        /// Вывод, который стоит держать в голове: проверка может врать в обе стороны — и
        /// «зелёной по определению», и «красной по ошибке разбора». Поэтому она проверяется
        /// самопроверкой ниже.
        /// </summary>
        private static bool Covers(List<string> rules, string folder)
        {
            string name = folder.Trim('/', '\\').ToLowerInvariant();

            foreach (string rule in rules)
            {
                string normalized = rule.Trim('/').ToLowerInvariant();
                if (normalized.Length == 0) continue;

                // Каждый класс символов `[...]` → его первая буква.
                var builder = new System.Text.StringBuilder(normalized.Length);
                int i = 0;
                while (i < normalized.Length)
                {
                    if (normalized[i] == '[')
                    {
                        int close = normalized.IndexOf(']', i + 1);
                        if (close > i + 1)
                        {
                            builder.Append(normalized[i + 1]);   // [Ll] → l
                            i = close + 1;
                            continue;
                        }
                    }
                    builder.Append(normalized[i]);
                    i++;
                }

                if (builder.ToString() == name) return true;
            }
            return false;
        }

        /// <summary>
        /// САМОПРОВЕРКА РАЗБОРА ПРАВИЛ. Если разбор сломается, все проверки выше станут «зелёными
        /// по определению» (правил «не нашлось» → всё игнорируется?) — или, как уже было, «красными
        /// по ошибке» (правил не нашлось → ничего не игнорируется).
        ///
        /// Здесь проверяется разбор на ЗАВЕДОМО ИЗВЕСТНЫХ строках формата этого проекта.
        /// </summary>
        [Test]
        public void RuleParser_UnderstandsProjectFormat()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30, "правила не прочитаны");

            // Формат проекта: `/[Ll]ibrary/` и `/[Uu]ser[Ss]ettings/`.
            Assert.IsTrue(Covers(rules, "Library"), "разбор не увидел правила для Library");
            Assert.IsTrue(Covers(rules, "UserSettings"), "разбор не увидел правила для UserSettings");
            Assert.IsFalse(Covers(rules, "НесуществующаяПапкаДляСамопроверки"),
                "разбор «видит» правила для несуществующей папки — он бесполезен");
        }

        // ---------------------------------------------------------------- 1. АРТЕФАКТЫ

        /// <summary>
        /// `.gitignore` ЕСТЬ И НЕ ПУСТ. Без него следующие проверки «пройдут» на пустом списке
        /// правил — то есть тест зеленел бы по определению (урок E-16).
        /// </summary>
        [Test]
        public void GitignoreExistsAndHasRules()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30,
                "в .gitignore меньше 30 правил — файл потерян или повреждён");
        }

        /// <summary>
        /// КАЖДАЯ ПАПКА-АРТЕФАКТ УНИТИ ИГНОРИРУЕТСЯ. Если правило пропадёт, следующий `git add -A`
        /// затащит в историю гигабайты, и убрать их можно будет только переписыванием истории.
        /// </summary>
        [Test]
        public void ArtifactFolders_AreIgnored()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30, "правила не прочитаны — проверка бессмысленна");

            var missing = new List<string>();
            foreach (string folder in ArtifactFolders)
                if (!Covers(rules, folder)) missing.Add(folder + "/");

            Assert.AreEqual(0, missing.Count,
                "эти папки-артефакты Unity НЕ игнорируются — `git add -A` затащит их в историю: " +
                string.Join(", ", missing.ToArray()));
        }

        // ---------------------------------------------------------------- 2. СЕКРЕТЫ

        /// <summary>
        /// СЕКРЕТЫ ПОДПИСИ APK НЕ ПОПАДАЮТ В GIT. Keystore + пароль = возможность подписать
        /// приложение от имени владельца. Это не «неудобство», а компрометация.
        ///
        /// Правила добавлены в S43; тест не даёт им потеряться при чистке `.gitignore`.
        /// </summary>
        [Test]
        public void SigningSecrets_AreIgnored()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30);

            var missing = new List<string>();
            foreach (string pattern in SecretPatterns)
                if (!Covers(rules, pattern)) missing.Add(pattern);

            Assert.AreEqual(0, missing.Count,
                "эти секреты подписи НЕ игнорируются — их можно случайно закоммитить: " +
                string.Join(", ", missing.ToArray()));
        }

        // ---------------------------------------------------------------- 3. НАШ МУСОР

        /// <summary>
        /// РАБОЧИЕ ПАПКИ ПРОГОНОВ НЕ ПОПАДАЮТ В GIT. `_ui_audit` (логи и отчёты аудита) и
        /// `Logs/ci-*` (выгрузки консольных проверок) — это мегабайты текста, который нужен только
        /// на этой машине.
        /// </summary>
        [Test]
        public void AuditScratchFolders_AreIgnored()
        {
            List<string> rules = GitignoreRules();
            Assert.Greater(rules.Count, 30);

            bool hasAudit = false, hasCiLogs = false;
            foreach (string rule in rules)
            {
                if (rule.IndexOf("_ui_audit", System.StringComparison.OrdinalIgnoreCase) >= 0) hasAudit = true;
                if (rule.IndexOf("ci-", System.StringComparison.OrdinalIgnoreCase) >= 0) hasCiLogs = true;
            }

            Assert.IsTrue(hasAudit, "папка _ui_audit (логи аудита) не игнорируется");
            Assert.IsTrue(hasCiLogs, "выгрузки Logs/ci-* не игнорируются");
        }

        // ---------------------------------------------------------------- 4. LFS

        /// <summary>
        /// КРУПНЫЕ БИНАРНИКИ ИДУТ ЧЕРЕЗ LFS. Без этого каждая версия модели `.fbx`/`.glb`/`.png`
        /// навсегда остаётся в истории, и клон растёт без предела.
        ///
        /// Проверяется, что `.gitattributes` объявляет LFS хотя бы для ШЕСТИ типов, которые в
        /// проекте реально есть (модели, изображения, шрифты, звук, видео, архивы).
        /// </summary>
        [Test]
        public void GitAttributes_EnableLfsForBinaryAssets()
        {
            string path = Path.Combine(Root, ".gitattributes");
            Assert.IsTrue(File.Exists(path), "нет .gitattributes — LFS не подключён, история будет расти");

            string text = File.ReadAllText(path);
            var extensions = new[] { "*.fbx", "*.png", "*.ttf", "*.wav", "*.mp4", "*.zip" };

            var missing = new List<string>();
            foreach (string ext in extensions)
                if (!Regex.IsMatch(text, Regex.Escape(ext) + @"\s+lfs"))
                    missing.Add(ext);

            Assert.AreEqual(0, missing.Count,
                "эти расширения не объявлены в LFS — каждая версия файла останется в истории: " +
                string.Join(", ", missing.ToArray()));
        }
    }
}
