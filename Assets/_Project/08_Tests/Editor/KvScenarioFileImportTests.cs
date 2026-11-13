// ================================================================================================
//  KvScenarioFileImportTests.cs — EditMode-тесты ИМПОРТА СЦЕНАРИЯ ИЗ ФАЙЛА (13.11.2026).
//
//  ОТКУДА ЗАДАЧА. Красный список тестировщика, §3: пункт меню «Файл → Импорт сценария»
//  (`file.import`) был виден в интерфейсе и НЕ делал ничего (команда-заглушка `Stub = true`
//  без обработчика). Требование владельца дословно: «чтобы все кнопочки были заполнены
//  функционалом». Выбран вариант 1 из §3: парная команда `file.export` уже реальная (она
//  показывает папку данных, где лежат файлы сценариев), поэтому импорт делает ОБРАТНЫЙ РАЗБОР
//  того же формата — JSON сценария, который пишет `session.save` в папку данных.
//
//  ЧЕМ ЭТО ОТЛИЧАЕТСЯ ОТ `session.load`. Штатная загрузка возвращает `null` при ЛЮБОЙ проблеме:
//  нет файла, файл не читается, это не JSON, в файле пусто — оператор получает одно и то же
//  «сессий нет» и не понимает, что делать. Здесь КАЖДАЯ проблема получает своё честное
//  сообщение с именем файла, а само действие НИКОГДА не бросает исключение: кнопка меню не
//  имеет права ронять интерфейс.
//
//  ЧТО ПРОВЕРЯЕТСЯ (по поведению, а не по тексту исходника):
//    1) отсутствующий файл, мусор вместо JSON и JSON без данных сценария → `Ok = false` и
//       сообщение с причиной, исключений нет;
//    2) настоящий файл формата экспорта разбирается обратно: имя, роботы, точки доезжают
//       до `KvSession` (анти-вакуум: без этого «не падает» зеленело бы на пустом результате);
//    3) в пустой папке и в несуществующей папке — честное сообщение, а не тишина;
//    4) `ImportNewest` берёт САМЫЙ СВЕЖИЙ файл, а не первый попавшийся.
//
//  ГРАНИЦА ЧЕСТНОСТИ. Тест не открывает меню и не нажимает кнопку `file.import`: состав
//  реестра команд в EditMode пуст (команды регистрирует менеджер интерфейса в рантайме).
//  Факт «у команды есть настоящий обработчик» закреплён Е2Е-проверкой 6.6
//  (`KvE2eSettingsDiag`), которая смотрит на РЕЕСТР после всех регистраций.
// ================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using KazistovVvFeatures;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    [Category(KvTestCategories.Fast)]
    public class KvScenarioFileImportTests
    {
        /// <summary>Каталог одного теста: создаётся в SetUp, целиком удаляется в TearDown.</summary>
        private string dir;

        [SetUp]
        public void MakeWorkDir()
        {
            dir = Path.Combine(Application.temporaryCachePath,
                "kv_import_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            Assert.IsTrue(Directory.Exists(dir), "подготовка сорвана: рабочий каталог не создан");
        }

        [TearDown]
        public void RemoveWorkDir()
        {
            // Пустая проверка перед удалением — правило проекта: никогда не удалять по пути
            // из переменной без проверки непустоты.
            if (string.IsNullOrEmpty(dir)) return;
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
            dir = null;
        }

        // ------------------------------------------------------------------ вспомогательное

        /// <summary>Записать файл сценария и вернуть его путь.</summary>
        private string WriteScenario(string name, KvSession session)
        {
            string path = Path.Combine(dir, name);
            Assert.IsTrue(FeatureStorage.SaveJson(path, session),
                "подготовка сорвана: не удалось записать файл «" + path + "»");
            return path;
        }

        /// <summary>Настоящий сценарий в формате экспорта: один робот, две точки.</summary>
        private static KvSession RealSession()
        {
            var session = new KvSession();
            session.id = "scenario-001";
            session.name = "Проверка импорта";
            session.created = "2026-11-13T00:00:00Z";
            session.robots.Add(new KvSessionRobot { name = "stand_6axis", active = true });
            session.points.Add(new KvVec3(new Vector3(1f, 2f, 3f)));
            session.points.Add(new KvVec3(new Vector3(4f, 5f, 6f)));
            return session;
        }

        // ------------------------------------------------------------------ 1. ЧЕСТНЫЕ ОШИБКИ

        /// <summary>
        /// ОТСУТСТВУЮЩИЙ ФАЙЛ — ЧЕСТНОЕ СООБЩЕНИЕ, А НЕ ИСКЛЮЧЕНИЕ.
        ///
        /// ЧТО БЫЛО. `file.import` вообще ничего не делал (заглушка), а ближайший путь
        /// (`KvSessionStore.Load`) молча возвращает `null` на любой проблеме — оператор видит
        /// «сессий нет» даже тогда, когда файл есть, но лежит не там.
        ///
        /// ЧТО СТАЛО. Результат называет причину и путь: по сообщению видно, что искать и где.
        /// </summary>
        [Test]
        public void ImportFile_MissingFile_ReportsHonestErrorWithoutThrowing()
        {
            string path = Path.Combine(dir, "нет_такого.session.json");

            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportFile(path),
                "импорт отсутствующего файла бросил исключение — пункт меню не имеет права " +
                "ронять интерфейс оператора");

            Assert.IsNotNull(result, "импорт не вернул результата вообще");
            Assert.IsFalse(result.Ok, "отсутствующий файл объявлен успешно импортированным");
            Assert.IsFalse(string.IsNullOrEmpty(result.Error),
                "причина отказа не названа — оператор увидит просто «не работает»");
            StringAssert.Contains("нет_такого", result.Error,
                "сообщение не называет файл: " + result.Error);
        }

        /// <summary>
        /// МУСОР ВМЕСТО JSON — ЧЕСТНОЕ СООБЩЕНИЕ, А НЕ ИСКЛЮЧЕНИЕ.
        ///
        /// Файл мог быть повреждён при копировании или создан другим приложением. Кнопка меню
        /// обязана сказать «файл не разбирается», а не упасть с `ArgumentException` в консоль.
        /// </summary>
        [Test]
        public void ImportFile_NotJson_ReportsHonestErrorWithoutThrowing()
        {
            string path = Path.Combine(dir, "битый.session.json");
            File.WriteAllText(path, "это не сценарий, а обычный текст");

            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportFile(path),
                "импорт файла без JSON бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsFalse(result.Ok, "файл без JSON объявлен успешно импортированным");
            Assert.IsFalse(string.IsNullOrEmpty(result.Error),
                "причина отказа не названа");
            StringAssert.Contains("битый", result.Error,
                "сообщение не называет файл: " + result.Error);
        }

        /// <summary>
        /// JSON БЕЗ ДАННЫХ СЦЕНАРИЯ — ЧЕСТНОЕ СООБЩЕНИЕ, А НЕ ИСКЛЮЧЕНИЕ.
        ///
        /// `JsonUtility` не считает посторонний объект ошибкой: он вернёт `KvSession` со
        /// значениями по умолчанию, и наивный импорт «применил» бы пустую сцену, сделав вид,
        /// что импорт прошёл. Это ХУЖЕ отказа: оператор не узнает, что файл не тот.
        /// </summary>
        [Test]
        public void ImportFile_JsonWithoutScenarioData_ReportsHonestError()
        {
            string path = Path.Combine(dir, "чужой.session.json");
            File.WriteAllText(path, "{\"foo\":1,\"bar\":\"не сценарий\"}");

            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportFile(path),
                "импорт постороннего JSON бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsFalse(result.Ok,
                "посторонний JSON принят как сценарий — оператор получит пустую сцену вместо " +
                "своего файла и не поймёт причину");
            Assert.IsFalse(string.IsNullOrEmpty(result.Error),
                "причина отказа не названа — сообщение обязано объяснить, чего в файле не хватает");
            Assert.IsNull(result.Session, "отказ сопровождается объектом сценария — его нельзя применять");
        }

        // ------------------------------------------------------------------ 2. ОБРАТНЫЙ РАЗБОР

        /// <summary>
        /// НАСТОЯЩИЙ ФАЙЛ ФОРМАТА ЭКСПОРТА РАЗБИРАЕТСЯ ОБРАТНО.
        ///
        /// Анти-вакуум главного утверждения: «импорт работает» — это не «не упало», а то, что
        /// имя, роботы и точки из файла доехали до `KvSession` в том же виде, в каком их
        /// записал экспорт. Если импорт начнёт возвращать заглушку или пустой объект, тест
        /// покраснеет по СОДЕРЖИМОМУ, а не по факту вызова.
        /// </summary>
        [Test]
        public void ImportFile_RoundTripsTheExportedScenarioFormat()
        {
            KvSession exported = RealSession();
            string path = WriteScenario("сценарий.session.json", exported);

            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportFile(path),
                "разбор корректного файла бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsTrue(result.Ok,
                "корректный файл отклонён: " + result.Error);
            Assert.IsTrue(string.IsNullOrEmpty(result.Error),
                "успешный импорт сопровождается сообщением об ошибке: " + result.Error);
            Assert.IsNotNull(result.Session, "успешный импорт без разобранной сценарии");

            KvSession parsed = result.Session;
            Assert.AreEqual(exported.id, parsed.id, "идентификатор сценария не доехал");
            Assert.AreEqual(exported.name, parsed.name, "имя сценария не доехало");
            Assert.AreEqual(1, parsed.robots.Count, "роботы не доехали");
            Assert.AreEqual("stand_6axis", parsed.robots[0].name, "имя робота исказилось");
            Assert.AreEqual(2, parsed.points.Count, "точки не доехали");
            Assert.AreEqual(new Vector3(1f, 2f, 3f), parsed.points[0].Vector,
                "координаты первой точки исказились");
            Assert.AreEqual(path, result.FilePath, "результат не называет разобранный файл");
        }

        // ------------------------------------------------------------------ 3. ПОИСК ФАЙЛА

        /// <summary>
        /// В ПУСТОЙ ПАПКЕ — ЧЕСТНОЕ СООБЩЕНИЕ («файлов нет»), А НЕ ТИШИНА И НЕ ИСКЛЮЧЕНИЕ.
        ///
        /// Оператор жмёт «Импорт сценария» на чистой машине: ему нужно понять, что сначала
        /// надо что-нибудь сохранить, а не догадываться, сработало ли нажатие.
        /// </summary>
        [Test]
        public void ImportNewest_NoFiles_ReportsHonestError()
        {
            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportNewest(dir),
                "импорт в пустой папке бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsFalse(result.Ok, "в пустой папке найден сценарий");
            Assert.IsFalse(string.IsNullOrEmpty(result.Error),
                "оператор не получил никакого объяснения — нажатие выглядит как «ничего не произошло»");
            StringAssert.Contains("нет", result.Error.ToLowerInvariant(),
                "сообщение не говорит о том, что файлов нет: " + result.Error);
        }

        /// <summary>Несуществующая папка — тоже честное сообщение, а не исключение.</summary>
        [Test]
        public void ImportNewest_MissingDirectory_ReportsHonestError()
        {
            string missing = Path.Combine(dir, "нет_папки");

            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportNewest(missing),
                "импорт в несуществующей папке бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsFalse(result.Ok, "несуществующая папка объявлена источником сценария");
            Assert.IsFalse(string.IsNullOrEmpty(result.Error), "причина отказа не названа");
        }

        /// <summary>
        /// `ImportNewest` БЕРЁТ САМЫЙ СВЕЖИЙ ФАЙЛ, А НЕ ПЕРВЫЙ ПОПАВШИЙСЯ.
        ///
        /// ЗАЧЕМ. Оператор жмёт «Импорт» и ждёт ПОСЛЕДНЕЕ сохранённое состояние. Взять файл по
        /// алфавиту или по порядку обхода — значит молча откатить работу на старую версию, и
        /// это незаметно: сценарий загрузится, просто не тот.
        /// </summary>
        [Test]
        public void ImportNewest_PicksTheMostRecentlyWrittenFile()
        {
            KvSession old = RealSession();
            old.id = "старый";
            old.name = "Старый сценарий";
            string oldPath = WriteScenario("а_старый.session.json", old);
            File.SetLastWriteTimeUtc(oldPath, DateTime.UtcNow.AddHours(-3));

            KvSession recent = RealSession();
            recent.id = "свежий";
            recent.name = "Свежий сценарий";
            string recentPath = WriteScenario("б_свежий.session.json", recent);
            File.SetLastWriteTimeUtc(recentPath, DateTime.UtcNow);

            // Порядок имён противоречит порядку времени: по имени первый — «а_старый».
            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportNewest(dir),
                "импорт двух файлов бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsTrue(result.Ok, "корректный файл отклонён: " + result.Error);
            Assert.AreEqual(recentPath, result.FilePath,
                "взят не самый свежий файл — оператор получит старое состояние вместо последнего");
            Assert.IsNotNull(result.Session, "сценарий не разобран");
            Assert.AreEqual("свежий", result.Session.id,
                "разобран не тот файл: " + result.Session.id);
        }

        /// <summary>
        /// СПИСОК КАНДИДАТОВ НЕ ВКЛЮЧАЕТ ВРЕМЕННЫЕ ФАЙЛЫ АТОМАРНОЙ ЗАПИСИ.
        ///
        /// `FeatureStorage.SaveJson` пишет через `*.tmp` и подменяет файл: если импорт увидит
        /// полузаписанный `.tmp`, он либо откажется, либо прочитает обрывок. Проверяется на
        /// поведении: лежащий рядом `.tmp` не мешает найти настоящий файл.
        /// </summary>
        [Test]
        public void ImportNewest_IgnoresTemporaryWriteFiles()
        {
            KvSession session = RealSession();
            string path = WriteScenario("сценарий.session.json", session);
            File.WriteAllText(path + ".tmp", "{\"обрывок\": ");

            KvScenarioImportResult result = null;
            Assert.DoesNotThrow(() => result = KvScenarioFileImport.ImportNewest(dir),
                "импорт рядом с временным файлом бросил исключение");

            Assert.IsNotNull(result, "импорт не вернул результата");
            Assert.IsTrue(result.Ok,
                "импорт сорвался на временном файле рядом с настоящим: " + result.Error);
            Assert.AreEqual(path, result.FilePath, "взят не настоящий файл");
            Assert.IsNotNull(result.Session, "сценарий не разобран");
        }

        /// <summary>
        /// АНТИ-ВАКУУМ: КАНДИДАТЫ ДЕЙСТВИТЕЛЬНО НАХОДЯТСЯ.
        ///
        /// Проверки «пустая папка → отказ» и «нет кандидатов → отказ» зеленели бы и на методе,
        /// который не умеет искать файлы вовсе. Здесь рядом лежат два настоящих файла, и оба
        /// обязаны быть найдены.
        /// </summary>
        [Test]
        public void CandidateFiles_FindsRealScenarioFiles()
        {
            WriteScenario("первый.session.json", RealSession());
            WriteScenario("второй.session.json", RealSession());
            File.WriteAllText(Path.Combine(dir, "заметки.txt"), "не сценарий");

            List<string> found = KvScenarioFileImport.CandidateFiles(dir);

            Assert.IsNotNull(found, "поиск кандидатов не вернул списка");
            Assert.AreEqual(2, found.Count,
                "найдено файлов: " + found.Count + " — ожидались два «*.session.json» рядом с " +
                "посторонним «заметки.txt»");
        }
    }
}
