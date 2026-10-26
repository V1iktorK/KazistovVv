using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using KazistovVvXrEditor;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПАРКОВКИ ТЕСТОВ НА ВРЕМЯ СБОРКИ APK (Этап А, дефект A2).
    ///
    /// ЗАЧЕМ ЭТИ ТЕСТЫ. Папка `Assets/_Project/08_Tests` убирается из `Assets` на время сборки
    /// плеера (в плеере нет библиотеки Unity Test Framework, и `KvFlowPlayModeTests.cs` рвёт
    /// сборку ошибкой CS0246 — так это и было найдено в §28). Но возврат из `Temp` был написан
    /// так: «перенести обратно, ЕСЛИ папки в `Assets` НЕТ». Unity пересоздаёт папку (остаётся
    /// её `.meta`), поэтому на живой сборке §30 условие не выполнилось и 23 файла тестов
    /// остались бы в `Temp` — МОЛЧА, без единой строки в журнале.
    ///
    /// ПОЧЕМУ ПРОВЕРЯЕТСЯ МЕТОД, А НЕ ВЕСЬ `BuildAndroidApk`. Запустить сборку APK из теста
    /// нельзя (это часы и гигабайты), а проверить правило возврата — можно и нужно. Поэтому
    /// возврат и парковка вынесены в отдельные методы, работающие только с путями
    /// (`System.IO`, ни одного обращения к Unity API), — их и вызывает сборка. Тест проверяет
    /// РОВНО тот код, который исполняется на сборке, а не его копию.
    ///
    /// ЧТО ИМЕННО ЛОВИТСЯ (каждый случай — отдельный тест):
    ///   1) обычный возврат, когда папки в `Assets` нет;
    ///   2) **папка пересоздана Unity и ПУСТА** — тот самый дефект;
    ///   3) в `Assets` уже есть ПОСТОРОННИЕ файлы — ничего не затирается, конфликты называются;
    ///   4) возвращать нечего — это не ошибка, и об этом честно сказано;
    ///   5) в отчёте есть числа и остаток в `Temp`, а не «вроде вернули»;
    ///   6) **в `Temp` лежит парковка от убитой сборки** — второй дефект того же места:
    ///      прежний код сначала УДАЛЯЛ папку в `Temp`, то есть уничтожал тесты навсегда.
    ///
    /// Тесты написаны ДО правки и на прежнем коде обязаны падать (правило [K5]).
    /// Ни одного GameObject, ни одной записи в настройки, ни зависимости от времени.
    /// </summary>
    public class KvBuildTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "kv_build_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                if (Directory.Exists(_root)) Directory.Delete(_root, true);
            }
            catch (IOException)
            {
                // Временная папка теста — не предмет проверки: не убралась, и ладно.
            }
        }

        /// <summary>Путь папки тестов в проекте (в тесте — внутри временной папки).</summary>
        private string TestsPath
        {
            get { return Path.Combine(_root, "Assets", "_Project", "08_Tests"); }
        }

        /// <summary>Путь парковки — так же, как в `KvXrTools.BuildAndroidApk` (`Temp` проекта).</summary>
        private string ParkedPath
        {
            get { return Path.Combine(_root, "Temp", "_tests_parked_for_build"); }
        }

        /// <summary>Разложить «тесты» в `Temp`: структура повторяет настоящую (Editor + PlayMode).</summary>
        private void SeedParked(params string[] relativeFiles)
        {
            foreach (string rel in relativeFiles)
            {
                string full = Path.Combine(ParkedPath, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "// тест " + rel);
            }
        }

        /// <summary>Разложить файлы в `Assets` — то, что Unity может пересоздать или дописать.</summary>
        private void SeedInAssets(params string[] relativeFiles)
        {
            foreach (string rel in relativeFiles)
            {
                string full = Path.Combine(TestsPath, rel.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, "// посторонний файл " + rel);
            }
        }

        private static int CountFiles(string path)
        {
            if (!Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
        }

        // ==================================================== 1. ОБЫЧНЫЙ ВОЗВРАТ

        /// <summary>
        /// ПАПКИ В `Assets` НЕТ — переносится целиком. Этот случай работал и до правки;
        /// он оставлен, чтобы правка не сломала работающее.
        /// </summary>
        [Test]
        public void Restore_TargetMissing_MovesWholeFolderBack()
        {
            SeedParked("Editor.meta", "Editor/KvXrTests.cs", "Editor/KvXrTests.cs.meta", "PlayMode.meta");
            Assert.IsFalse(Directory.Exists(TestsPath), "подготовка: папки в Assets быть не должно");

            KvXrTools.TestParkingReport report = KvXrTools.RestoreParkedTests(TestsPath, ParkedPath);

            Assert.IsTrue(report.Success, "возврат обязан удаться: " + report.Describe());
            Assert.AreEqual(4, report.FilesMoved, "перенесено должно быть 4 файла: " + report.Describe());
            Assert.IsTrue(File.Exists(Path.Combine(TestsPath, "Editor", "KvXrTests.cs")),
                "файл теста не вернулся в Assets");
            Assert.IsFalse(Directory.Exists(ParkedPath), "парковка в Temp должна опустеть и исчезнуть");
        }

        // ==================================================== 2. ДЕФЕКТ A2

        /// <summary>
        /// ПАПКА ПЕРЕСОЗДАНА UNITY И ПУСТА — ЭТО И ЕСТЬ ДЕФЕКТ A2.
        ///
        /// Именно так выглядел проект после сборки §30: `Assets/_Project/08_Tests` существует
        /// и пуста, а 23 файла лежат в `Temp`. Прежнее условие «возвращать, только если папки
        /// нет» на этом состоянии не выполнялось — и тесты оставались в `Temp` навсегда.
        /// Пустая папка — не данные: её удаляют и переносят содержимое на место.
        /// </summary>
        [Test]
        public void Restore_TargetRecreatedEmptyByUnity_ContentsComeBack()
        {
            SeedParked("Editor.meta", "Editor/KvXrTests.cs", "Editor/KvXrTests.cs.meta",
                       "PlayMode.meta", "PlayMode/KvFlowPlayModeTests.cs");
            Directory.CreateDirectory(TestsPath);   // ← Unity пересоздала пустую папку

            KvXrTools.TestParkingReport report = KvXrTools.RestoreParkedTests(TestsPath, ParkedPath);

            Assert.IsTrue(report.TargetRecreated,
                "отчёт обязан назвать причину: папка была пересоздана Unity и пуста — " + report.Describe());
            Assert.IsTrue(report.Success, "возврат обязан удаться: " + report.Describe());
            Assert.AreEqual(5, report.FilesMoved, "перенесено должно быть 5 файлов: " + report.Describe());
            Assert.IsTrue(File.Exists(Path.Combine(TestsPath, "Editor", "KvXrTests.cs")),
                "ГЛАВНОЕ: тест обязан оказаться в Assets, а не остаться в Temp");
            Assert.IsTrue(File.Exists(Path.Combine(TestsPath, "PlayMode", "KvFlowPlayModeTests.cs")),
                "вернуться обязаны оба подкаталога: Editor и PlayMode");
            Assert.AreEqual(0, CountFiles(ParkedPath), "в Temp не должно остаться ни одного файла");
        }

        // ==================================================== 3. НЕ ЗАТИРАТЬ ЧУЖОЕ

        /// <summary>
        /// В `Assets` УЖЕ ЛЕЖАТ ФАЙЛЫ С ТЕМИ ЖЕ ИМЕНАМИ. Возврат обязан быть НЕразрушающим:
        /// существующее не перезаписывается, конфликт называется числом, а неразобранное
        /// остаётся в `Temp` (потерять файл теста хуже, чем доложить о конфликте).
        /// </summary>
        [Test]
        public void Restore_TargetHasForeignFiles_NothingIsOverwritten()
        {
            SeedParked("Editor/KvXrTests.cs", "Editor/KvNewTests.cs");
            SeedInAssets("Editor/KvXrTests.cs");   // тот же путь, другое содержимое

            string before = File.ReadAllText(Path.Combine(TestsPath, "Editor", "KvXrTests.cs"));

            KvXrTools.TestParkingReport report = KvXrTools.RestoreParkedTests(TestsPath, ParkedPath);

            Assert.AreEqual(1, report.Conflicts, "конфликт обязан быть посчитан: " + report.Describe());
            Assert.AreEqual(1, report.FilesMoved, "неконфликтный файл обязан вернуться: " + report.Describe());
            Assert.IsFalse(report.Success,
                "частичный возврат — не успех: в Temp остались неразобранные файлы — " + report.Describe());
            Assert.AreEqual(1, report.FilesLeftInTemp, "остаток в Temp обязан быть назван числом");
            Assert.AreEqual(before, File.ReadAllText(Path.Combine(TestsPath, "Editor", "KvXrTests.cs")),
                "существующий файл перезаписывать НЕЛЬЗЯ");
            Assert.IsTrue(File.Exists(Path.Combine(TestsPath, "Editor", "KvNewTests.cs")),
                "файл без конфликта обязан вернуться в Assets");
            Assert.IsTrue(File.Exists(Path.Combine(ParkedPath, "Editor", "KvXrTests.cs")),
                "конфликтный файл обязан ОСТАТЬСЯ в Temp — потерять его нельзя");
        }

        // ==================================================== 4. ВОЗВРАЩАТЬ НЕЧЕГО

        /// <summary>
        /// ПАРКОВКИ НЕТ — это НЕ ошибка: сборка без правки тестов идёт именно так.
        /// Прежний код в этом случае молча ничего не делал; отличие в том, что теперь об этом
        /// сказано в журнале — «тишина вместо ответа» и была причиной, по которой дефект
        /// прожил с §28 по §30 незамеченным.
        /// </summary>
        [Test]
        public void Restore_NothingParked_ReportsHonestlyWithoutError()
        {
            KvXrTools.TestParkingReport report = KvXrTools.RestoreParkedTests(TestsPath, ParkedPath);

            Assert.IsTrue(report.Success, "отсутствие парковки — не ошибка: " + report.Describe());
            Assert.IsTrue(report.NothingToDo, "отчёт обязан сказать, что работы не было: " + report.Describe());
            Assert.AreEqual(0, report.FilesMoved, "переносить было нечего");
            StringAssert.Contains("нечего", report.Describe(), "в журнале должно быть сказано «нечего возвращать»");
        }

        // ==================================================== 5. ЧИСЛА, А НЕ «ВРОДЕ»

        /// <summary>
        /// ОТЧЁТ ОБЯЗАН НАЗЫВАТЬ ЧИСЛА. Строка в журнале сборки — единственное доказательство
        /// для владельца; «тесты возвращены» без числа файлов неотличимо от «возврат не сработал».
        /// </summary>
        [Test]
        public void Restore_ReportNamesCountsAndLeavesNothingBehind()
        {
            SeedParked("Editor/KvA.cs", "Editor/KvA.cs.meta", "Editor/KvB.cs", "Editor/KvB.cs.meta");

            KvXrTools.TestParkingReport report = KvXrTools.RestoreParkedTests(TestsPath, ParkedPath);
            string text = report.Describe();

            Assert.AreEqual(4, report.FilesMoved, "перенесено 4 файла: " + text);
            Assert.AreEqual(0, report.FilesLeftInTemp, "в Temp не осталось ничего: " + text);
            StringAssert.Contains("4", text, "число перенесённых файлов обязано быть в отчёте");
            StringAssert.Contains("2", text, "число файлов тестов (без .meta) обязано быть в отчёте");
        }

        // ==================================================== 6. ВТОРОЙ ДЕФЕКТ ТОГО ЖЕ МЕСТА

        /// <summary>
        /// ПАРКОВКА ОТ УБИТОЙ СБОРКИ НЕ УНИЧТОЖАЕТСЯ.
        ///
        /// Второй дефект того же места, найденный при написании этих тестов: прежний код
        /// парковки начинался с `Directory.Delete(testsParked, true)` — то есть если прошлая
        /// сборка была убита (таймаут, снятие процесса) и тесты остались в `Temp`, следующая
        /// сборка **удаляла их навсегда** и парковала на их место пустую папку.
        /// Правильное поведение: сначала вернуть остаток на место (самолечение), и только
        /// потом парковать снова. Тест ловит именно потерю файла.
        /// </summary>
        [Test]
        public void Parking_LeftoverFromKilledBuild_IsNotDestroyed()
        {
            SeedParked("Editor.meta", "Editor/KvXrTests.cs", "Editor/KvXrTests.cs.meta");
            Directory.CreateDirectory(TestsPath);   // папку пересоздала Unity — как после убитой сборки

            KvXrTools.TestParkingReport report = KvXrTools.ParkTestsForBuild(TestsPath, ParkedPath);

            Assert.IsTrue(report.Success, "парковка обязана пройти: " + report.Describe());
            Assert.IsTrue(File.Exists(Path.Combine(ParkedPath, "Editor", "KvXrTests.cs")),
                "ФАЙЛ ТЕСТА УНИЧТОЖЕН: остаток прошлой парковки нельзя удалять — " + report.Describe());
            Assert.AreEqual(3, CountFiles(ParkedPath), "в новой парковке обязаны быть все 3 файла");
        }
    }
}
