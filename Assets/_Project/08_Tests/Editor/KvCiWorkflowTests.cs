// ================================================================================================
//  KvCiWorkflowTests.cs — EditMode-тесты КОНФИГУРАЦИИ CI (сессия S44).
//
//  ОТКУДА ЗАДАЧА. CI написан, но НЕ ЗАПУСКАЛСЯ НИ РАЗУ: у владельца нет секретов лицензии Unity, а
//  прогон на GitHub требует публикации ветки. Значит проверить его «на живом» сейчас нельзя — и
//  остаётся проверить ПРАВИЛА, зашитые в конфигурацию.
//
//  ЧТО НАЙДЕНО 30.10.2026 ПРИ РАЗБОРЕ. Итоговый гейт оставался ЗЕЛЁНЫМ, когда тесты
//  ПРОПУСКАЛИСЬ из-за отсутствия лицензии. То есть «зелёная галочка» при отсутствии тестов —
//  ровно тот класс ошибки, который всю ночь вычищался в самих тестах (E-16: «пропущено» ≠ «хорошо»).
//
//  ЧТО ЗАКРЕПЛЕНО:
//    1) пропущенные тесты ВАЛЯТ итоговый гейт, и в логе сказано, ЧТО настроить;
//    2) отменённый прогон тоже не считается успехом;
//    3) шаг тестов действительно запускает EditMode-тесты в batchmode;
//    4) кэш `Library` есть (иначе каждый прогон — с нуля, и CI станет бесполезно медленным);
//    5) файл в `.github/workflows` — иначе GitHub его вообще не увидит;
//    6) версия Unity в CI совпадает с `ProjectVersion.txt` — иначе тесты пойдут не тем движком.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Конфигурация CI: пропуск не равен успеху, версия и кэш на месте.</summary>
    public class KvCiWorkflowTests
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

        /// <summary>
        /// ФАЙЛ КОНФИГУРАЦИИ НА МЕСТЕ. GitHub ищет его только в `.github/workflows`; файл, лежащий
        /// рядом (например в корне), не запустит НИЧЕГО, и это не заметно до первого пуша.
        /// </summary>
        [Test]
        public void WorkflowFileExistsInTheRightFolder()
        {
            string path = WorkflowPath;
            Assert.IsNotNull(path,
                "нет .github/workflows/unity-tests.yml — GitHub не увидит конфигурацию, CI не запустится");

            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 1500, "файл подозрительно мал (" + text.Length + " символов)");
            StringAssert.Contains("jobs:", text, "в конфигурации нет ни одной задачи");
        }

        /// <summary>
        /// ПРОПУЩЕННЫЕ ТЕСТЫ ВАЛЯТ ГЕЙТ — ГЛАВНЫЙ ТЕСТ ФАЙЛА.
        ///
        /// До исправления гейт оставался зелёным при `skipped`. Это значит: нет лицензии → тесты не
        /// запускались → «всё хорошо». Человек смотрел на зелёную галочку и считал ветку проверенной.
        /// </summary>
        [Test]
        public void SkippedTests_FailTheGate()
        {
            string text = File.ReadAllText(WorkflowPath);

            // В гейте обязан быть явный отказ на skipped.
            Match skippedCheck = Regex.Match(text,
                @"editmode-tests\.result\s*\}\}\s*[""']\s*=\s*[""']skipped[""']");
            Assert.IsTrue(skippedCheck.Success,
                "итоговый гейт НЕ проверяет пропуск тестов — зелёная галочка возможна без единого " +
                "запущенного теста (та же ошибка, что E-16)");

            // И объяснение, что надо настроить: без него красный крестик бесполезен.
            StringAssert.Contains("UNITY_LICENSE", text,
                "в конфигурации не сказано, что настроить при пропуске — искать придётся наугад");
        }

        /// <summary>
        /// ОТМЕНЁННЫЙ ПРОГОН — НЕ УСПЕХ. `cancelled` тоже не проверяет ничего, и пропускать его как
        /// «не failure» значит повторить ту же ошибку с другой стороны.
        /// </summary>
        [Test]
        public void CancelledRun_IsNotSuccess()
        {
            string text = File.ReadAllText(WorkflowPath);
            Assert.IsTrue(Regex.IsMatch(text, @"=\s*[""']cancelled[""']"),
                "отменённый прогон не проверяется — он пройдёт как успех");
        }

        /// <summary>
        /// ШАГ ТЕСТОВ ДЕЙСТВИТЕЛЬНО ЗАПУСКАЕТ EDITMODE-ТЕСТЫ. Конфигурация без `testMode: EditMode`
        /// запустит что-то другое (или ничего) — и гейт будет зелёным при непроверенном коде.
        /// </summary>
        [Test]
        public void EditModeTestsAreActuallyRun()
        {
            string text = File.ReadAllText(WorkflowPath);

            StringAssert.Contains("game-ci/unity-test-runner", text,
                "не используется штатный запуск тестов Unity");
            StringAssert.Contains("EditMode", text, "не указан режим EditMode");

            // Запуск в пакетном режиме обязателен на раннере без графики: без -nographics и
            // -batchmode Unity на сервере не стартует или висит.
            StringAssert.Contains("nographics", text,
                "нет -nographics — на раннере без GPU Unity может зависнуть (это уже измерялось)");
        }

        /// <summary>
        /// КЭШ `LIBRARY` ЕСТЬ. Без него каждый прогон импортирует проект с нуля (десятки минут), и CI
        /// станет настолько медленным, что его выключат — а выключенный CI не проверяет ничего.
        /// </summary>
        [Test]
        public void LibraryCacheIsConfigured()
        {
            string text = File.ReadAllText(WorkflowPath);

            StringAssert.Contains("actions/cache", text, "кэш не настроен — каждый прогон с нуля");
            StringAssert.Contains("Library", text, "в кэш не включена папка Library");

            // Ключ обязан зависеть от версии Unity и lock-файла пакетов: иначе после обновления
            // движка останется старый кэш, и тесты пойдут на несовместимых бинарниках.
            StringAssert.Contains("packages-lock.json", text,
                "ключ кэша не учитывает пакеты — после их обновления останется устаревший кэш");
            StringAssert.Contains("ProjectVersion.txt", text,
                "ключ кэша не учитывает версию Unity");
        }

        /// <summary>
        /// ВЕРСИЯ UNITY В CI СОВПАДАЕТ С ПРОЕКТОМ. Разные версии — это тесты на другом движке,
        /// и «у меня работает, а в CI нет» (или наоборот) будет необъяснимым.
        /// </summary>
        [Test]
        public void UnityVersionMatchesTheProject()
        {
            string versionFile = Path.Combine(Root, "ProjectSettings", "ProjectVersion.txt");
            Assert.IsTrue(File.Exists(versionFile), "нет ProjectSettings/ProjectVersion.txt");

            Match m = Regex.Match(File.ReadAllText(versionFile), @"m_EditorVersion:\s*(\S+)");
            Assert.IsTrue(m.Success, "не удалось прочитать версию Unity из ProjectVersion.txt");
            string projectVersion = m.Groups[1].Value;

            string workflow = File.ReadAllText(WorkflowPath);
            StringAssert.Contains(projectVersion, workflow,
                "в CI указана не та версия Unity, что в проекте (" + projectVersion +
                ") — тесты пойдут на другом движке");
        }

        /// <summary>
        /// ПРОВЕРКИ БЕЗ UNITY НЕ ТРЕБУЮТ ЛИЦЕНЗИИ. Если бы требовали, CI молчал бы целиком, пока
        /// владелец не настроит секреты, — а дешёвые проверки (BOM, синтаксис .ps1) должны работать
        /// всегда.
        /// </summary>
        [Test]
        public void StaticChecksDoNotDependOnUnityLicense()
        {
            string text = File.ReadAllText(WorkflowPath);

            // Найти задачу static-checks и убедиться, что в её теле нет условий по лицензии.
            Match job = Regex.Match(text, @"static-checks:(.*?)(?=\n  \w[\w-]*:|\z)", RegexOptions.Singleline);
            Assert.IsTrue(job.Success, "в конфигурации нет задачи static-checks");

            string body = job.Groups[1].Value;
            Assert.IsFalse(body.Contains("UNITY_LICENSE") && body.Contains("if:"),
                "проверки без Unity зависят от лицензии — они не будут работать, пока секреты не настроены");
        }
    }
}
