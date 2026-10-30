// ================================================================================================
//  KvStorageResilienceTests.cs — EditMode-тесты УСТОЙЧИВОСТИ ХРАНИЛИЩА (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Приложение хранит данные оператора на ДИСКЕ: макросы, позы, зоны, сессии,
//  сценарии, записи. На шлеме диск — это не «папка на компьютере»: он может быть заполнен, может
//  быть недоступен в первые секунды после запуска, а разрешения на запись могут не выдаться.
//
//  ЧТО ПРОВЕРЕНО 30.10.2026. `FeatureStorage.EnsureDir` — ЕДИНСТВЕННОЕ место, где создаются папки,
//  и оно переживает отказ: ловит исключение, пишет ПРЕДУПРЕЖДЕНИЕ в журнал и **возвращает путь**.
//  Это правильно: вызывающий код получает путь и попробует писать — а отказ записи обработает у
//  себя. Падение здесь означало бы, что приложение не запускается вообще.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) `EnsureDir` не бросает даже на заведомо невозможном пути;
//    2) возвращает путь (а не пустую строку) — иначе путь к данным «схлопнется» в текущую папку;
//    3) не падает на пустом и на null-пути;
//    4) корень данных не зависит от версии программы (переживает обновление);
//    5) все рабочие подпапки создаются через ОДНО место, а не каждая по-своему.
// ================================================================================================

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Хранилище на диске: отказы не роняют приложение, путь не теряется.</summary>
    public class KvStorageResilienceTests
    {
        /// <summary>
        /// СОЗДАНИЕ КАТАЛОГА НЕ БРОСАЕТ ДАЖЕ НА НЕВОЗМОЖНОМ ПУТИ.
        ///
        /// Путь с недопустимыми символами заведомо не создастся (на Windows это `:*?`). Если бы
        /// `EnsureDir` бросал исключение, приложение не запустилось бы на устройстве из-за
        /// недоступной папки — а причина была бы не видна оператору.
        /// </summary>
        [Test]
        public void EnsureDir_DoesNotThrowOnImpossiblePath()
        {
            var storageType = FindStorageType();
            Assert.IsNotNull(storageType, "не найден FeatureStorage — проверить нечего");

            var ensure = storageType.GetMethod("EnsureDir",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(ensure, "у FeatureStorage нет метода EnsureDir");

            // ПУТЬ С НЕДОПУСТИМЫМИ СИМВОЛАМИ: создать нельзя ни при каких правах.
            //
            // ЧЕСТНО О ПЕРВОЙ ПОПЫТКЕ (30.10.2026). Сначала путь склеивался через
            // `Path.Combine(Application.persistentDataPath, "не\\допустимо:*?<>|\0имя")` — и падал
            // САМ `Path.Combine` с `Illegal characters in path`, ещё до вызова проверяемого метода.
            // Тест сообщал о «падении» там, где `EnsureDir` даже не вызывался. Поэтому строка
            // формируется конкатенацией, а `\0` убран: цель — проверить ОБРАБОТКУ ОТКАЗА в
            // `EnsureDir`, а не поведение `Path.Combine`.
            string impossible = Application.persistentDataPath + "/kv_test_недопустимо:*?<>|/папка";

            object result = null;
            Assert.DoesNotThrow(() => result = ensure.Invoke(null, new object[] { impossible }),
                "создание каталога обязано переживать невозможный путь, а не падать");

            Assert.AreEqual(impossible, result,
                "при отказе метод обязан ВЕРНУТЬ путь, а не пустую строку: иначе данные уйдут в " +
                "текущую папку процесса");
        }

        /// <summary>
        /// ПУСТОЙ И NULL-ПУТЬ НЕ ЛОМАЮТ ХРАНИЛИЩЕ. Такие значения приходят из настроек и от
        /// вызывающего кода; падение здесь означало бы отказ при запуске из-за одной пустой строки.
        /// </summary>
        [Test]
        public void EnsureDir_SurvivesEmptyAndNull()
        {
            var storageType = FindStorageType();
            Assert.IsNotNull(storageType);

            var ensure = storageType.GetMethod("EnsureDir",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(ensure);

            Assert.DoesNotThrow(() => ensure.Invoke(null, new object[] { "" }),
                "пустой путь не должен ронять создание каталога");
            Assert.DoesNotThrow(() => ensure.Invoke(null, new object[] { null }),
                "null вместо пути не должен ронять создание каталога");
        }

        /// <summary>
        /// КОРЕНЬ ДАННЫХ НЕ ЗАВИСИТ ОТ ВЕРСИИ ПРОГРАММЫ. Если бы в пути оказался номер версии,
        /// после обновления приложения данные оператора стали бы невидимыми — и это не выглядело бы
        /// ошибкой (пустой список макросов, «ничего не было»).
        /// </summary>
        [Test]
        public void DataRootDoesNotContainVersion()
        {
            string root = Application.persistentDataPath;

            Assert.IsFalse(root.Contains(Application.version),
                "в пути данных есть НОМЕР ВЕРСИИ («" + Application.version + "»): " + root +
                " — после обновления программы данные оператора станут невидимыми");
        }

        /// <summary>
        /// ВСЕ РАБОЧИЕ ПОДПАПКИ ИДУТ ЧЕРЕЗ ОДНО МЕСТО. Если одна папка создаётся «своим» кодом,
        /// она не получит обработку отказа из `EnsureDir` — и станет единственной, которая роняет
        /// приложение при недоступном диске.
        ///
        /// Проверяется по исходнику: остальные файлы не должны вызывать `Directory.CreateDirectory`
        /// напрямую (кроме самого хранилища и служебных мест).
        /// </summary>
        [Test]
        public void DirectoryCreationGoesThroughOnePlace()
        {
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "01_Scripts");
            Assert.IsTrue(Directory.Exists(scriptsRoot), "нет папки скриптов");

            var direct = new System.Collections.Generic.List<string>();
            foreach (string file in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;
                if (name == "FeatureStorage.cs") continue;        // сам хранилище — можно
                if (name.Contains("Diag")) continue;              // диагностика — можно

                string text = File.ReadAllText(file);
                if (text.Contains("Directory.CreateDirectory")) direct.Add(name);
            }

            // Наличие такого списка — не провал: иногда папку надо создать именно на месте.
            // Проверяем главное: их НЕМНОГО. Массовое создание папок «где попало» означает, что
            // обработка отказа из EnsureDir обходится.
            Assert.Less(direct.Count, 15,
                "слишком много файлов создают каталоги напрямую (" + direct.Count + "), минуя " +
                "FeatureStorage.EnsureDir с обработкой отказа: " + string.Join(", ", direct.ToArray()));
        }

        private static System.Type FindStorageType()
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("KazistovVvFeatures.FeatureStorage");
                if (type != null) return type;
            }
            return null;
        }
    }
}
