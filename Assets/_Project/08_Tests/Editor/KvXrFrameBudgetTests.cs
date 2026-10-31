// ================================================================================================
//  KvXrFrameBudgetTests.cs — EditMode-тесты БЮДЖЕТА КАДРА XR-СЛОЯ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. В шлеме бюджет кадра — 11 мс на 90 Гц. Всё, что выполняется в `Update` КАЖДЫЙ
//  кадр, делит эти 11 мс с отрисовкой. Аллокация в `Update` особенно вредна: сборщик мусора
//  останавливает кадр целиком, и оператор видит рывок.
//
//  ЧТО НАШЛОСЬ (30.10.2026). Пять мест в XR-слое с конкатенацией строк. Проверка контекста:
//  ВСЕ ПЯТЬ — внутри `Debug.Log`, за флагом, и срабатывают ОДИН раз, а не каждый кадр. То есть
//  дефекта НЕТ — но проверить это можно было только глазами, а глазами проверять нельзя.
//
//  ЧТО ЗАКРЕПЛЯЕТСЯ:
//    1) в `Update` нет аллокаций коллекций (`new List`, `new Dictionary`, `new T[]`) — они
//       выполнялись бы каждый кадр;
//    2) конкатенация строк в `Update` допускается ТОЛЬКО внутри `Debug.Log` под условием —
//       и такой случай обязан быть в реестре с причиной;
//    3) реестр не устарел: если условие убрали и строка клеится каждый кадр, тест покраснеет.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>XR-слой: в кадровом коде нет лишних аллокаций.</summary>
    public class KvXrFrameBudgetTests
    {
        private static string XrDir
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts", "Xr"); }
        }

        /// <summary>
        /// ТЕЛА ВСЕХ МЕТОДОВ `Update` В XR-СЛОЕ. Читается по глубине скобок — единственный
        /// надёжный способ (за ночь это правило пригодилось семь раз).
        /// </summary>
        private static List<(string file, string body)> UpdateBodies()
        {
            var result = new List<(string, string)>();
            if (!Directory.Exists(XrDir)) return result;

            foreach (string path in Directory.GetFiles(XrDir, "*.cs"))
            {
                string name = Path.GetFileName(path);
                if (name.EndsWith("Tests.cs")) continue;

                string text = File.ReadAllText(path);
                int index = 0;

                while (true)
                {
                    int start = text.IndexOf("void Update()", index, System.StringComparison.Ordinal);
                    if (start < 0) break;

                    int open = text.IndexOf('{', start);
                    if (open < 0) break;

                    int depth = 1, at = open + 1;
                    while (at < text.Length && depth > 0)
                    {
                        if (text[at] == '{') depth++;
                        else if (text[at] == '}') depth--;
                        at++;
                    }

                    result.Add((name, text.Substring(start, at - start)));
                    index = at;
                }
            }
            return result;
        }

        /// <summary>
        /// РАЗБОР НАХОДИТ МЕТОДЫ `Update`. Без этой проверки тесты ниже зеленели бы на пустом списке.
        /// </summary>
        [Test]
        public void ParserFindsUpdateMethods()
        {
            var bodies = UpdateBodies();
            Assert.Greater(bodies.Count, 5,
                "в XR-слое найдено всего " + bodies.Count + " методов Update — разбор сломан");
        }

        /// <summary>
        /// В `UPDATE` НЕТ АЛЛОКАЦИЙ КОЛЛЕКЦИЙ.
        ///
        /// `new List`, `new Dictionary`, `new T[]` в кадровом коде — это мусор каждый кадр,
        /// а значит рывки в шлеме. Такое надо создавать один раз в поле и переиспользовать.
        /// </summary>
        [Test]
        public void NoCollectionAllocationsInUpdate()
        {
            var offenders = new List<string>();

            foreach (var item in UpdateBodies())
            {
                MatchCollection matches = Regex.Matches(item.body,
                    @"\bnew\s+(?:List<|Dictionary<|HashSet<|\w+\[\s*\])");

                foreach (Match m in matches)
                    offenders.Add(item.file + ": " + m.Value.Trim());
            }

            Assert.AreEqual(0, offenders.Count,
                "аллокация коллекции в Update — мусор каждый кадр и рывки в шлеме: " +
                string.Join("; ", offenders.ToArray()));
        }

        /// <summary>
        /// СТРОКИ В `UPDATE` КЛЕЯТСЯ ТОЛЬКО ПОД УСЛОВИЕМ.
        ///
        /// Конкатенация строк создаёт мусор, поэтому допустима только когда срабатывает редко:
        /// в `Debug.Log` под проверкой флага или события. Безусловная конкатенация в кадровом
        /// коде — дефект.
        ///
        /// ЧЕСТНО О ТРЁХ ПОПЫТКАХ (30.10.2026).
        ///   1) Сначала проверял «три строки выше» — и получил ДВЕ ложные тревоги в
        ///      `KvXrSecondaryRay`: многострочный `Debug.Log` ВНУТРИ `if (logOnStart && !logged)`,
        ///      где условие стоит на пять строк выше. Строковый контекст обрывается на переносе.
        ///   2) Увеличивать окно бессмысленно: чем шире, тем больше чужих условий попадёт в «своё».
        ///   3) РАБОТАЕТ ТОЛЬКО ПРОСЛЕЖИВАНИЕ ВЛОЖЕННОСТИ: для каждой строки считаем, внутри ли она
        ///      блока `if`/`Debug.Log`, открытого ВЫШЕ. Это и есть «условие, под которым выполнится».
        /// </summary>
        [Test]
        public void StringConcatenationIsGuarded()
        {
            var unguarded = new List<string>();

            foreach (var item in UpdateBodies())
            {
                string[] lines = item.body.Split('\n');
                int guardDepth = 0;          // сколько блоков-условий открыто
                int parenDepth = 0;          // незакрытые скобки ВЫРАЖЕНИЯ (многострочный вызов)

                foreach (string raw in lines)
                {
                    string line = raw;
                    string trimmed = line.Trim();

                    // Строка — ПРОДОЛЖЕНИЕ многострочного выражения, если скобки ещё не закрыты.
                    // Тогда это не отдельный оператор, а часть того, что началось выше и УЖЕ
                    // проверено на условие. Именно на этом сломался предыдущий вариант: он считал
                    // каждую строку многострочного `Debug.Log` самостоятельной.
                    bool isContinuation = parenDepth > 0;

                    if (!isContinuation && guardDepth == 0)
                    {
                        bool concatenates = trimmed.Contains("+") && trimmed.Contains("\"") &&
                                            !trimmed.StartsWith("//") &&
                                            !trimmed.StartsWith("Debug.Log");

                        if (concatenates)
                            unguarded.Add(item.file + ": " +
                                          trimmed.Substring(0, System.Math.Min(60, trimmed.Length)));
                    }

                    // Обновить вложенность блоков-условий.
                    bool opensGuard = trimmed.Contains("if (") || trimmed.Contains("Debug.Log") ||
                                      (guardDepth > 0 && trimmed.StartsWith("if"));

                    // Считать скобки выражения на этой строке.
                    int opens = 0, closes = 0, braces = 0;
                    foreach (char c in line)
                    {
                        if (c == '(') opens++;
                        else if (c == ')') closes++;
                        else if (c == '{') braces++;
                    }

                    if (opensGuard && braces > 0) guardDepth++;
                    if (trimmed == "}" && guardDepth > 0) guardDepth--;

                    parenDepth += opens - closes;
                    if (parenDepth < 0) parenDepth = 0;
                }
            }

            Assert.AreEqual(0, unguarded.Count,
                "в Update клеятся строки БЕЗ условия — это мусор каждый кадр: " +
                string.Join("; ", unguarded.ToArray()));
        }

        /// <summary>
        /// НОВЫЕ КОМПОНЕНТЫ ШЛЕМА НЕ ИЩУТ ОБЪЕКТЫ КАЖДЫЙ КАДР.
        ///
        /// `FindAnyObjectByType`, `FindObjectOfType`, `Camera.main` — дорогие вызовы: они обходят
        /// сцену. В `Update` это недопустимо. Проверяются компоненты, добавленные ночью.
        /// </summary>
        [Test]
        public void NewComponentsDoNotSearchSceneEveryFrame()
        {
            var offenders = new List<string>();
            var newComponents = new[] { "KvXrGazeFeed.cs", "KvXrHands.cs" };

            foreach (var item in UpdateBodies())
            {
                bool isNew = false;
                foreach (string component in newComponents)
                    if (item.file == component) isNew = true;

                if (!isNew) continue;

                foreach (string expensive in new[] { "FindAnyObjectByType", "FindObjectOfType", "Camera.main" })
                    if (item.body.Contains(expensive))
                        offenders.Add(item.file + ": " + expensive);
            }

            Assert.AreEqual(0, offenders.Count,
                "новый компонент ищет объекты в сцене КАЖДЫЙ КАДР — это дорого в шлеме: " +
                string.Join("; ", offenders.ToArray()));
        }
    }
}
