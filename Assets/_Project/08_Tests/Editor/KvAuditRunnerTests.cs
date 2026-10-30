// ================================================================================================
//  KvAuditRunnerTests.cs — EditMode-тесты КОРНЕВОГО СКРИПТА ПРОВЕРКИ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. `Tools/audit-all.ps1` — единственный вход для всех проверок: тесты, «чистая
//  консоль», вердикт. Если ОН соврёт (скажет «всё прошло», когда что-то упало), то вся ночная
//  работа ничего не стоит: зелёный вердикт перестаёт что-либо значить.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ, БЕЗ ЗАПУСКА UNITY (это важно: прогон занимает минуты, а проверить надо
//  ЛОГИКУ вердикта):
//    1) скрипт ВАЛИТСЯ на пропущенном тесте — «пропуск» больше не считается нормой (E-16);
//    2) скрипт ВАЛИТСЯ на упавшем тесте;
//    3) скрипт ВАЛИТСЯ, если отчёт тестов не создан (прогон не дошёл до отчёта);
//    4) скрипт ВАЛИТСЯ, если гейт консоли вернул не ноль;
//    5) НОЛЬ возвращается ТОЛЬКО при отсутствии всех отказов — проверяется по структуре кода.
//
//  ПОЧЕМУ ПО ИСХОДНИКУ, А НЕ ЗАПУСКОМ. Запуск `audit-all.ps1` внутри EditMode-теста означал бы
//  «Unity запускает Unity»: рекурсивный batchmode, который висит и мешает сам себе. Поэтому
//  тест проверяет ПРАВИЛА, записанные в скрипте, а реальный прогон делает человек одной командой.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Корневой скрипт проверки: отказы и вердикт.</summary>
    public class KvAuditRunnerTests
    {
        private static string ScriptPath
        {
            get
            {
                string root = Path.GetDirectoryName(Application.dataPath);
                string path = Path.Combine(root, "Tools", "audit-all.ps1");
                return File.Exists(path) ? path : null;
            }
        }

        /// <summary>
        /// СКРИПТ СУЩЕСТВУЕТ И НЕ ПУСТ. Без него следующие проверки «пройдут» на пустой строке —
        /// то есть зеленели бы по определению (E-16).
        /// </summary>
        [Test]
        public void AuditScriptExists()
        {
            string path = ScriptPath;
            Assert.IsNotNull(path,
                "нет Tools/audit-all.ps1 — единого входа для проверок, значит и вердикта нет");

            string text = File.ReadAllText(path);
            Assert.Greater(text.Length, 2000,
                "скрипт подозрительно мал (" + text.Length + " символов) — возможно, обрезан");
        }

        /// <summary>
        /// ПРОПУЩЕННЫЙ ТЕСТ — ЭТО ПРОВАЛ. С 30.10.2026 самовыключающихся тестов быть не должно;
        /// если пропуск появился, вердикт не пройден. Без этого правила 13 замаскированных
        /// `Assert.Ignore` выглядели бы «зелёным прогоном» — ровно то, что закрывали в S44.
        /// </summary>
        [Test]
        public void SkippedTest_FailsTheRun()
        {
            string text = File.ReadAllText(ScriptPath);

            Assert.IsTrue(Regex.IsMatch(text, @"\$run\.skipped"),
                "скрипт не смотрит на число пропущенных тестов");
            Assert.IsTrue(Regex.IsMatch(text, @"result\s*=\s*[""']Skipped[""']"),
                "скрипт не перечисляет пропущенные тест-кейсы поимённо");
            Assert.IsTrue(Regex.IsMatch(text, @"Fail-Gate\s*\(\s*[""']ПРОПУЩЕН"),
                "пропуск не приводит к отказу — «пропущено» снова станет нормой");
        }

        /// <summary>
        /// УПАВШИЙ ТЕСТ — ЭТО ПРОВАЛ, причём с ИМЕНЕМ теста. «Упало 3» без имён заставляет искать
        /// причину вручную по всему XML.
        /// </summary>
        [Test]
        public void FailedTest_FailsTheRunWithName()
        {
            string text = File.ReadAllText(ScriptPath);

            Assert.IsTrue(Regex.IsMatch(text, @"\$run\.failed"),
                "скрипт не смотрит на число упавших тестов");
            Assert.IsTrue(Regex.IsMatch(text, @"result\s*=\s*[""']Failed[""']"),
                "скрипт не перечисляет упавшие тест-кейсы поимённо");
        }

        /// <summary>
        /// ОТСУТСТВИЕ ОТЧЁТА — ПРОВАЛ. Если Unity упал до создания XML, «тестов не было» нельзя
        /// считать успехом: это самый частый вид молчаливого провала (компиляция не прошла).
        /// </summary>
        [Test]
        public void MissingXmlReport_FailsTheRun()
        {
            string text = File.ReadAllText(ScriptPath);

            Assert.IsTrue(Regex.IsMatch(text, @"Test-Path\s+-LiteralPath\s+\$testXml"),
                "скрипт не проверяет наличие XML-отчёта тестов");
            Assert.IsTrue(Regex.IsMatch(text, @"не создан|не дошёл до отчёта"),
                "отсутствие отчёта не названо отказом — молчаливый провал пройдёт как успех");
        }

        /// <summary>
        /// НЕНУЛЕВОЙ КОД ГЕЙТА — ПРОВАЛ. Гейт «чистой консоли» ловит ошибки компиляции и исключения;
        /// если его код проигнорировать, красный гейт ничего не остановит.
        /// </summary>
        [Test]
        public void GateFailure_FailsTheRun()
        {
            string text = File.ReadAllText(ScriptPath);

            Assert.IsTrue(Regex.IsMatch(text, @"\$LASTEXITCODE"),
                "скрипт не читает код возврата гейта консоли");
            Assert.IsTrue(Regex.IsMatch(text, @"gateExit\s*-ne\s*0"),
                "ненулевой код гейта не проверяется — красный гейт никого не остановит");
        }

        /// <summary>
        /// НОЛЬ ВОЗВРАЩАЕТСЯ ТОЛЬКО ПРИ ОТСУТСТВИИ ОТКАЗОВ. Проверяется структура: перед `exit 0`
        /// обязан стоять флаг `$script:failed`, и `exit 1` должен быть ДО него. Иначе скрипт
        /// вернёт «всё прошло» при любом провале.
        /// </summary>
        [Test]
        public void ZeroExit_ComesOnlyAfterTheFailureFlag()
        {
            string text = File.ReadAllText(ScriptPath);

            int failedFlag = text.LastIndexOf("$script:failed", System.StringComparison.Ordinal);
            int exitZero = text.LastIndexOf("exit 0", System.StringComparison.Ordinal);
            int exitOne = text.LastIndexOf("exit 1", System.StringComparison.Ordinal);

            Assert.Greater(failedFlag, 0, "в скрипте нет флага провала $script:failed");
            Assert.Greater(exitZero, 0, "в скрипте нет успешного завершения");
            Assert.Greater(exitOne, 0, "в скрипте нет отказа");

            Assert.Less(failedFlag, exitZero,
                "успешный выход идёт РАНЬШЕ проверки флага провала — скрипт соврёт о результате");
            Assert.Less(exitOne, exitZero,
                "отказ должен быть ДО успешного выхода, иначе он недостижим и скрипт всегда «одобряет»");
        }

        /// <summary>
        /// ТАЙМАУТ ЕСТЬ. Прогон Unity в пакетном режиме может зависнуть; без таймаута ночная
        /// проверка висела бы до утра, и «нет результата» выглядело бы как «не запускали».
        /// </summary>
        [Test]
        public void TestRunHasTimeout()
        {
            string text = File.ReadAllText(ScriptPath);

            Assert.IsTrue(Regex.IsMatch(text, @"WaitForExit\s*\("),
                "скрипт ждёт Unity без ограничения времени — зависший прогон повесит всю проверку");
            Assert.IsTrue(Regex.IsMatch(text, @"Stop-Process"),
                "нет снятия зависшего процесса — Unity останется висеть в памяти");
        }
    }
}
