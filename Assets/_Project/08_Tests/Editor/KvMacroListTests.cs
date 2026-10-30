// ================================================================================================
//  KvMacroListTests.cs — EditMode-тесты ОБНОВЛЕНИЯ СПИСКА МАКРОСОВ (сессия S44, пункт 5.25).
//
//  ОТКУДА ЗАДАЧА. `KvAutomation.RefreshMacroList()` был ПУСТЫМ методом, и его вызывали после
//  сохранения и удаления макроса. Наружу это выглядело так: оператор сохранил макрос — в списке
//  его нет; удалил — он остался. Чтобы увидеть изменение, приходилось перезапускать окно.
//
//  ЧТО СДЕЛАНО. Добавлено событие `MacroListChanged`, которое `RefreshMacroList()` рассылает
//  подписчикам. Движок макросов теперь не знает, КТО показывает список, — это и правильно:
//  иначе движок зависел бы от интерфейса.
//
//  ЧТО ПРОВЕРЯЕТСЯ (без сцены, без записи пользовательских файлов):
//    1) после сохранения подписчик получает ОДНО уведомление;
//    2) после удаления — тоже, и ровно одно;
//    3) если подписчик бросил исключение, сохранение НЕ ломается (макрос на диске в порядке);
//    4) без подписчиков ничего не падает;
//    5) папка макросов живёт внутри папки данных (а не в корне диска).
//
//  ПРО ФАЙЛЫ. Тесты пишут макросы в папку данных проекта (`FeatureStorage.Root/Macros`) и
//  подчищают за собой в `[TearDown]`. Имена начинаются с `__S44_`, чтобы не задеть настоящие
//  макросы оператора.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using KazistovVvFeatures;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Уведомление об изменении списка макросов.</summary>
    public class KvMacroListTests
    {
        private const string TestPrefix = "__S44_";

        private KvScriptEngine engine;
        private readonly List<string> created = new List<string>();
        private int notifications;

        [SetUp]
        public void SetUp()
        {
            engine = new KvScriptEngine();
            notifications = 0;
            engine.MacroListChanged += OnChanged;
        }

        [TearDown]
        public void TearDown()
        {
            if (engine != null) engine.MacroListChanged -= OnChanged;
            // Убрать только СВОИ макросы: настоящие файлы оператора трогать нельзя.
            foreach (string name in created)
            {
                try { engine.DeleteMacro(name); } catch { /* уборка не должна ломать тест */ }
            }
            created.Clear();
        }

        private void OnChanged()
        {
            notifications++;
        }

        // ---------------------------------------------------------------- 1. СОХРАНЕНИЕ

        /// <summary>
        /// ПОСЛЕ СОХРАНЕНИЯ — ОДНО УВЕДОМЛЕНИЕ. Ровно одно важно: два означали бы двойную
        /// перестройку списка, ноль — ровно тот дефект, что был (список не обновлялся).
        /// </summary>
        [Test]
        public void SaveMacro_NotifiesExactlyOnce()
        {
            string name = TestPrefix + "save";
            created.Add(name);

            engine.SaveMacro(name, "// тестовый макрос\n");

            Assert.AreEqual(1, notifications,
                "после сохранения макроса обязан быть ОДИН сигнал обновления списка; было " +
                notifications);
        }

        /// <summary>После сохранения макрос действительно виден в списке движка.</summary>
        [Test]
        public void SaveMacro_AppearsInList()
        {
            string name = TestPrefix + "visible";
            created.Add(name);

            engine.SaveMacro(name, "// видимый макрос\n");

            bool found = false;
            foreach (KvScriptMacro macro in engine.Macros)
            {
                if (macro != null && macro.name == name) { found = true; break; }
            }
            Assert.IsTrue(found, "сохранённый макрос обязан появиться в списке (был пустой RefreshMacroList)");
        }

        // ---------------------------------------------------------------- 2. УДАЛЕНИЕ

        /// <summary>
        /// ПОСЛЕ УДАЛЕНИЯ — ТОЖЕ ОДНО УВЕДОМЛЕНИЕ, и макроса в списке больше нет. Раньше он
        /// оставался видимым до перезапуска.
        /// </summary>
        [Test]
        public void DeleteMacro_NotifiesOnceAndRemovesFromList()
        {
            string name = TestPrefix + "delete";
            created.Add(name);
            engine.SaveMacro(name, "// удаляемый\n");
            notifications = 0;                              // считаем только удаление

            Assert.IsTrue(engine.DeleteMacro(name), "удаление обязано вернуть успех");
            Assert.AreEqual(1, notifications, "после удаления обязан быть один сигнал обновления");

            foreach (KvScriptMacro macro in engine.Macros)
            {
                Assert.AreNotEqual(name, macro != null ? macro.name : null,
                    "удалённый макрос остался в списке");
            }
        }

        // ---------------------------------------------------------------- 3. УСТОЙЧИВОСТЬ

        /// <summary>
        /// ПАДЕНИЕ ПОДПИСЧИКА НЕ ЛОМАЕТ СОХРАНЕНИЕ.
        ///
        /// ПОЧЕМУ ЭТО ВАЖНО. Окно интерфейса может быть в любом состоянии. Если бы исключение
        /// подписчика вылетало наружу, `SaveMacro` упал бы ПОСЛЕ записи файла — и оператор увидел
        /// бы «макрос не сохранён», хотя файл на диске есть. Хуже: список остался бы рассинхронён.
        /// </summary>
        [Test]
        public void SubscriberThrows_SavingStillSucceeds()
        {
            string name = TestPrefix + "resilient";
            created.Add(name);

            engine.MacroListChanged += delegate { throw new InvalidDataException("подписчик сломался"); };

            Assert.DoesNotThrow(() => engine.SaveMacro(name, "// устойчивый\n"),
                "исключение подписчика не должно вылетать из сохранения макроса");

            string path = Path.Combine(engine.MacrosFolder, name + ".kvs");
            Assert.IsTrue(File.Exists(path),
                "файл макроса обязан быть записан, несмотря на падение подписчика: " + path);
        }

        /// <summary>БЕЗ ПОДПИСЧИКОВ всё работает молча — событие не обязательно кому-то слушать.</summary>
        [Test]
        public void WithoutSubscribers_NothingThrows()
        {
            string name = TestPrefix + "nosubs";
            created.Add(name);
            engine.MacroListChanged -= OnChanged;          // снять единственного подписчика

            Assert.DoesNotThrow(() => engine.SaveMacro(name, "// без подписчиков\n"));
            Assert.IsFalse(engine.HasMacroListSubscribers, "подписчиков быть не должно");
        }

        // ---------------------------------------------------------------- 4. ПАПКА

        /// <summary>
        /// ПАПКА МАКРОСОВ — ВНУТРИ ПАПКИ ДАННЫХ. Если она окажется в корне диска или рядом с
        /// проектом, макросы потеряются при переносе проекта и будут мешать в репозитории.
        /// </summary>
        [Test]
        public void MacrosFolder_IsInsideDataRoot()
        {
            string folder = engine.MacrosFolder;
            Assert.IsFalse(string.IsNullOrEmpty(folder), "папка макросов не определена");
            StringAssert.StartsWith(Path.GetFullPath(FeatureStorage.Root), Path.GetFullPath(folder),
                "папка макросов обязана лежать внутри папки данных: " + folder);
            StringAssert.EndsWith("Macros", folder, "папка макросов обязана называться Macros: " + folder);
        }
    }
}
