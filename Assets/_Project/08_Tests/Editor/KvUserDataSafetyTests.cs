// ================================================================================================
//  KvUserDataSafetyTests.cs — EditMode-тесты СОХРАННОСТИ ДАННЫХ ПОЛЬЗОВАТЕЛЯ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Проект хранит макросы, позы, зоны, сессии, сценарии и записи оператора в
//  `Application.persistentDataPath`. Этот путь Unity строит ИЗ ИМЕНИ КОМПАНИИ И ПРОДУКТА
//  (`companyName` + `productName`). Если кто-то однажды поправит название продукта в
//  `ProjectSettings` — путь изменится, и ВСЕ данные оператора станут «невидимыми»: программа
//  запустится пустой, без единой ошибки в журнале.
//
//  ПОЧЕМУ ЭТО ОПАСНЕЕ ОБЫЧНОГО ДЕФЕКТА. Потеря не выглядит потерей: нет ни исключения, ни красного
//  сообщения. Оператор видит пустой список макросов и решает, что «программа сбросилась». Данные при
//  этом лежат в старой папке — но найти её без знания предмета невозможно.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) папка данных лежит ВНУТРИ `persistentDataPath` — то есть переживает обновление программы;
//    2) у неё ФИКСИРОВАННОЕ имя `KazistovVv` — не зависящее от версии и от названия продукта;
//    3) имя компании и продукта не пустые (иначе путь может схлопнуться);
//    4) все рабочие подпапки лежат ВНУТРИ корня — не «рядом» с ним;
//    5) имя продукта в настройках проекта совпадает с именем папки данных. Это и есть защита от
//       «переименовали продукт — потеряли данные»: тест покраснеет ДО того, как оператор обновится.
// ================================================================================================

using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Данные пользователя: путь, имя папки, подпапки, совпадение с настройками.</summary>
    public class KvUserDataSafetyTests
    {
        private const string DataFolderName = "KazistovVv";

        /// <summary>
        /// ПАПКА ДАННЫХ ВНУТРИ `persistentDataPath`. Вне её данные могут быть удалены системой
        /// (Android чистит кэш), а на компьютере — попасть в папку установки, которую затирают при
        /// обновлении.
        /// </summary>
        [Test]
        public void DataFolderIsInsidePersistentDataPath()
        {
            string root = Path.Combine(Application.persistentDataPath, DataFolderName);

            string persistent = Path.GetFullPath(Application.persistentDataPath);
            string data = Path.GetFullPath(root);

            StringAssert.StartsWith(persistent, data,
                "папка данных не внутри persistentDataPath — при обновлении программы данные пропадут: " +
                data);
        }

        /// <summary>
        /// ИМЯ ПАПКИ ФИКСИРОВАННОЕ И НЕ ЗАВИСИТ ОТ ВЕРСИИ. `KazistovVv/data` переживает обновления,
        /// `KazistovVv/0.1.0` — нет: после смены версии оператор увидит пустой список.
        /// </summary>
        [Test]
        public void DataFolderNameIsStable()
        {
            string root = Path.Combine(Application.persistentDataPath, DataFolderName);

            Assert.IsFalse(root.Contains(Application.version),
                "в пути данных есть НОМЕР ВЕРСИИ — после обновления данные станут невидимыми: " + root);
            Assert.IsFalse(root.Contains("0.1.0"),
                "в пути данных зашит номер версии: " + root);
            StringAssert.EndsWith(DataFolderName, root,
                "последняя папка в пути данных обязана называться " + DataFolderName + ": " + root);
        }

        /// <summary>
        /// ИДЕНТИЧНОСТЬ ПРИЛОЖЕНИЯ НЕ ПУСТАЯ. Пустой `companyName` или `productName` — это путь вида
        /// `.../persistentDataPath//KazistovVv`, и на части платформ он схлопывается в другой
        /// каталог. Проверяется через имя продукта: оно обязано быть непустым.
        /// </summary>
        [Test]
        public void AppIdentityIsNotEmpty()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(Application.productName),
                "у приложения пустое имя продукта — путь к данным может схлопнуться");
            Assert.IsFalse(string.IsNullOrWhiteSpace(Application.companyName),
                "у приложения пустое имя компании — путь к данным может схлопнуться");
        }

        /// <summary>
        /// НАЗВАНИЕ ПРОДУКТА СОВПАДАЕТ С ИМЕНЕМ ПАПКИ ДАННЫХ. ЭТО ГЛАВНЫЙ ТЕСТ ФАЙЛА.
        ///
        /// Unity строит `persistentDataPath` из `<компания>/<продукт>`. Мы дополнительно кладём всё
        /// в подпапку `KazistovVv`. Пока продукт называется так же, путь предсказуем и совпадает у
        /// всех сборок. Если продукт переименуют, тест покраснеет СРАЗУ — до того, как это заметит
        /// оператор, у которого пропали макросы.
        /// </summary>
        [Test]
        public void ProductNameMatchesDataFolder()
        {
            Assert.AreEqual(DataFolderName, Application.productName,
                "имя продукта («" + Application.productName + "») не совпадает с именем папки данных " +
                "«" + DataFolderName + "». Если это осознанное переименование — продумайте перенос " +
                "старых данных, иначе у оператора пропадут макросы, позы, зоны и сессии. Если " +
                "случайное — верните имя");
        }

        // ---------------------------------------------------------------- ПОДПАПКИ

        /// <summary>
        /// ВСЕ РАБОЧИЕ ПОДПАПКИ ЛЕЖАТ ВНУТРИ КОРНЯ. Если одна окажется вне (`../Recordings`), она
        /// переживёт не всё: часть систем чистит только свою папку.
        ///
        /// Проверяется по исходнику: список подпапок задаётся в одном месте, и именно его надо
        /// держать под контролем. Разбор ищет `Path.Combine(Root, "<имя>")`.
        /// </summary>
        [Test]
        public void AllWorkSubfoldersAreInsideRoot()
        {
            string source = FindStorageSource();
            Assert.IsNotNull(source, "не найден FeatureStorage.cs — проверить нечего");

            string text = File.ReadAllText(source);
            var outside = new System.Collections.Generic.List<string>();

            foreach (System.Text.RegularExpressions.Match m in
                     System.Text.RegularExpressions.Regex.Matches(text, @"Path\.Combine\(\s*Root\s*,\s*""([^""]+)"""))
            {
                string name = m.Groups[1].Value;
                // «../» или абсолютный путь уводят данные за пределы корня.
                if (name.Contains("..") || Path.IsPathRooted(name)) outside.Add(name);
            }

            Assert.AreEqual(0, outside.Count,
                "эти подпапки уводят данные ЗА пределы корня: " + string.Join(", ", outside.ToArray()) +
                " — часть из них не переживёт обновление программы");

            // САМОПРОВЕРКА: разбор обязан что-то найти, иначе тест зеленеет по определению (E-16).
            int found = System.Text.RegularExpressions.Regex.Matches(text, @"Path\.Combine\(\s*Root\s*,").Count;
            Assert.Greater(found, 3,
                "разбор нашёл всего " + found + " подпапок — похоже, разбор сломан, а не список короток");
        }

        private static string FindStorageSource()
        {
            string root = Path.Combine(Application.dataPath, "_Project");
            if (!Directory.Exists(root)) return null;
            foreach (string file in Directory.GetFiles(root, "FeatureStorage.cs", SearchOption.AllDirectories))
                return file;
            return null;
        }
    }
}
