// ================================================================================================
//  KvSafeNameTests.cs — EditMode-тесты БЕЗОПАСНЫХ ИМЁН ФАЙЛОВ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Имена макросов, зон, сессий и отчётов приходят ОТ ОПЕРАТОРА и становятся именами
//  файлов. `FeatureStorage.SafeName` вычищает недопустимые символы — от этого зависит, сохранится
//  ли работа вообще.
//
//  ЧТО ЛОМАЕТСЯ БЕЗ ТАКОЙ ОЧИСТКИ (и это не гипотезы):
//    • «Отчёт 01.11» — точка разрешена в имени, но имя из одних точек `.` или `..` означает
//      СОСЕДНЮЮ папку: файл уйдёт не туда или сохранение провалится;
//    • `a/b` — косая черта создаёт ПОДПАПКУ, и файл окажется не в папке данных;
//    • `:` на Windows — разделитель диска;
//    • имена устройств (`CON`, `PRN`, `NUL`, `COM1`) на Windows НЕВОЗМОЖНЫ как имена файлов вообще,
//      даже с расширением;
//    • пробелы и точки в конце имени Windows молча отбрасывает — а потом не находит файл.
//
//  ЧТО ПРОВЕРЯЕТСЯ:
//    1) недопустимые символы заменяются, имя остаётся НЕПУСТЫМ;
//    2) косая черта в любом направлении заменяется (файл не уйдёт в подпапку);
//    3) имя из точек не превращается в «соседнюю папку»;
//    4) пустое имя и null дают запасное;
//    5) результат СТАБИЛЕН: одно и то же имя даёт одно и то же имя файла.
// ================================================================================================

using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Очистка имён файлов из пользовательского ввода.</summary>
    public class KvSafeNameTests
    {
        /// <summary>Вызов очистки через отражение: тип живёт в сборке фич.</summary>
        private static string SafeName(string name)
        {
            var type = FindType("KazistovVvFeatures.FeatureStorage");
            Assert.IsNotNull(type, "не найден FeatureStorage — проверить нечего");

            var method = type.GetMethod("SafeName",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            Assert.IsNotNull(method, "у FeatureStorage нет метода SafeName");

            // Второй параметр — запасное имя; берём значение по умолчанию.
            var parameters = method.GetParameters();
            object[] args = parameters.Length > 1
                ? new object[] { name, "item" }
                : new object[] { name };

            return (string)method.Invoke(null, args);
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var assembly in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType(fullName);
                if (type != null) return type;
            }
            return null;
        }

        // ---------------------------------------------------------------- 1. НЕДОПУСТИМОЕ

        /// <summary>
        /// НЕДОПУСТИМЫЕ СИМВОЛЫ ЗАМЕНЯЮТСЯ, ИМЯ ОСТАЁТСЯ НЕПУСТЫМ.
        ///
        /// Проверяется не «стало красиво», а главное: результат пригоден как имя файла. Пустое имя
        /// означало бы, что сохранение провалится молча (файл не создастся).
        /// </summary>
        [Test]
        public void InvalidCharactersAreReplaced()
        {
            string[] inputs = { "Отчёт: 01.11", "a<b>c", "test|name", "quote\"name", "star*name", "question?name" };

            foreach (string input in inputs)
            {
                string result = SafeName(input);

                Assert.IsFalse(string.IsNullOrWhiteSpace(result),
                    "после очистки имени «" + input + "» получилась пустая строка — файл не сохранится");

                foreach (char bad in System.IO.Path.GetInvalidFileNameChars())
                    Assert.IsFalse(result.IndexOf(bad) >= 0,
                        "в результате «" + result + "» остался недопустимый символ «" + bad + "»");
            }
        }

        // ---------------------------------------------------------------- 2. КОСАЯ ЧЕРТА

        /// <summary>
        /// КОСАЯ ЧЕРТА В ЛЮБОМ НАПРАВЛЕНИИ ЗАМЕНЯЕТСЯ. На Linux (а шлем — это Android) `\` НЕ
        /// является недопустимым символом, но в имени файла означает разделитель папок. Если её не
        /// заменить, данные уйдут в подпапку, которой нет, и сохранение провалится.
        ///
        /// `Path.GetInvalidFileNameChars()` на Linux возвращает только `/` и `\0` — поэтому `\`
        /// приходится обрабатывать отдельно. В коде это сделано; тест это закрепляет.
        /// </summary>
        [Test]
        public void SlashesAreReplaced()
        {
            Assert.IsFalse(SafeName("a/b").Contains("/"),
                "косая черта осталась — файл уйдёт в подпапку");
            Assert.IsFalse(SafeName("a\\b").Contains("\\"),
                "обратная косая черта осталась — на Linux это разделитель папок, файл уйдёт не туда");
        }

        // ---------------------------------------------------------------- 3. ТОЧКИ

        /// <summary>
        /// ИМЯ ИЗ ТОЧЕК НЕ СТАНОВИТСЯ «СОСЕДНЕЙ ПАПКОЙ». `.` — текущая папка, `..` — родительская.
        /// Имя `..` в качестве имени файла означает выход ИЗ папки данных: сохранение либо
        /// провалится, либо запишет файл в неожиданное место.
        ///
        /// ПОЧЕМУ ЭТО ВАЖНО ИМЕННО ЗДЕСЬ: имя приходит от оператора, и «.» или «..» — правдоподобный
        /// ввод (человек мог нажать точку, чтобы проверить поле).
        /// </summary>
        [Test]
        public void DotOnlyNamesDoNotEscapeTheFolder()
        {
            string[] dangerous = { ".", "..", "...", " .. " };

            foreach (string input in dangerous)
            {
                string result = SafeName(input);

                // Если очистка оставила имя из точек — файл уйдёт в родительскую папку.
                bool onlyDots = result.Trim('.').Length == 0;
                Assert.IsFalse(onlyDots,
                    "имя «" + input + "» осталось точками («" + result + "») — это соседняя папка, " +
                    "а не файл: данные могут уйти из папки приложения");
            }
        }

        // ---------------------------------------------------------------- 4. ПУСТОЕ

        /// <summary>
        /// ПУСТОЕ ИМЯ И NULL ДАЮТ ЗАПАСНОЕ. Иначе сохранение получит пустое имя файла — и это
        /// либо исключение, либо файл с именем «.json» без названия.
        /// </summary>
        [Test]
        public void EmptyNameGivesFallback()
        {
            Assert.IsFalse(string.IsNullOrWhiteSpace(SafeName("")),
                "пустое имя обязано дать запасное");
            Assert.IsFalse(string.IsNullOrWhiteSpace(SafeName(null)),
                "null обязан дать запасное");
            Assert.IsFalse(string.IsNullOrWhiteSpace(SafeName("   ")),
                "имя из одних пробелов обязано дать запасное");
        }

        // ---------------------------------------------------------------- 5. СТАБИЛЬНОСТЬ

        /// <summary>
        /// ОДНО И ТО ЖЕ ИМЯ ДАЁТ ОДИН И ТОТ ЖЕ РЕЗУЛЬТАТ. Если бы очистка добавляла случайный
        /// суффикс или время, повторное сохранение создавало бы НОВЫЙ файл вместо замены старого —
        /// и папка данных заполнялась бы копиями.
        /// </summary>
        [Test]
        public void ResultIsStableAcrossCalls()
        {
            const string input = "Зона: опасная / 1";

            string first = SafeName(input);
            string second = SafeName(input);

            Assert.AreEqual(first, second,
                "очистка непостоянна: одно и то же имя дало «" + first + "» и «" + second + "». " +
                "Повторное сохранение создаст новый файл вместо замены");
        }
    }
}
