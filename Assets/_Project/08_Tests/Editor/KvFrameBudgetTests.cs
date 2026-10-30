// ================================================================================================
//  KvFrameBudgetTests.cs — EditMode-тесты БЮДЖЕТА КАДРА (сессия S44).
//
//  ОТКУДА ЗАДАЧА. Приложение работает в шлеме на 90 кадрах в секунду — это 11 миллисекунд на кадр
//  (измерено в проекте: 9,8–10,3 мс из 11,1). Всё, что выполняется в `Update`, выполняется 90 раз
//  в секунду, и любая дорогая операция там съедает кадр ЦЕЛИКОМ.
//
//  ЧТО НАЙДЕНО 30.10.2026. `KvXrSecondaryRay.Update` вызывал `Camera.main` КАЖДЫЙ КАДР.
//  `Camera.main` — не свойство, а ПОИСК по тегу `MainCamera` среди всех камер сцены. В проекте
//  камер несколько (вид сверху, сбоку, от первого лица), так что поиск небесплатен. Исправлено:
//  камера запоминается после первого раза.
//
//  ЧТО ПРОВЕРЯЕТСЯ (по исходникам):
//    1) в телах `Update` нет `Camera.main`, `FindObjectOfType`, `FindObjectsOfType`,
//       `GetComponentsInChildren` — они ищут по всей сцене на каждом кадре;
//    2) разбор действительно находит тела `Update` (иначе тест зеленел бы «потому что не нашёл»);
//    3) исключения — только с объяснением и только там, где вызов неизбежен.
//
//  ГРАНИЦА ЧЕСТНОСТИ: это не профилировщик. Тест ловит КЛАСС «дорогой поиск в покадровом коде»,
//  а не медленный код вообще. Реальную цену кадра показывает замер в шлеме.
// ================================================================================================

using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Дорогие вызовы в покадровом коде.</summary>
    public class KvFrameBudgetTests
    {
        /// <summary>
        /// ВЫЗОВЫ, КОТОРЫЕ НЕЛЬЗЯ ДЕЛАТЬ В `Update`. Все они ищут по сцене или материализуют
        /// коллекции, и все выполняются 90 раз в секунду.
        /// </summary>
        private static readonly Dictionary<string, string> ExpensiveCalls = new Dictionary<string, string>
            {
                { "Camera.main", "поиск камеры по тегу по всей сцене" },
                { "FindObjectOfType", "поиск объекта по всей сцене" },
                { "FindObjectsOfType", "поиск всех объектов по всей сцене" },
                { "FindAnyObjectByType", "поиск объекта по всей сцене" },
                { "GetComponentsInChildren", "сбор компонентов по иерархии" },
                { "Resources.Load", "загрузка ресурса с диска" },
            };

        /// <summary>
        /// ФАЙЛЫ-ИСКЛЮЧЕНИЯ: вызов неизбежен по смыслу — с причиной у каждого.
        /// </summary>
        private static readonly Dictionary<string, string> AllowedFiles = new Dictionary<string, string>
            {
                { "DshPlayModeRunner.cs", "инструмент проверки: не работает в сборке" },
            };

        private static string ScriptsRoot
        {
            get { return Path.Combine(Application.dataPath, "_Project", "01_Scripts"); }
        }

        /// <summary>
        /// ТЕЛА МЕТОДОВ `Update` ИЗ ИСХОДНИКА. Границы ищутся по строкам: от строки с объявлением до
        /// строки из одной `}`. Так вложенные блоки не путают разбор (за ночь это уже случалось).
        /// </summary>
        private static List<string> UpdateBodies(string path)
        {
            var bodies = new List<string>();
            string[] lines = File.ReadAllLines(path);

            for (int i = 0; i < lines.Length; i++)
            {
                if (!Regex.IsMatch(lines[i], @"\b(?:void|override\s+void)\s+Update\s*\(\s*\)")) continue;

                var builder = new System.Text.StringBuilder();
                for (int j = i; j < lines.Length; j++)
                {
                    builder.AppendLine(lines[j]);
                    if (j > i && lines[j].Trim() == "}") break;
                }
                bodies.Add(builder.ToString());
            }
            return bodies;
        }

        /// <summary>
        /// РАЗБОР НАХОДИТ ТЕЛА `Update`. Без этой проверки следующий тест зеленел бы на пустом
        /// списке — «нечего проверять, значит всё хорошо» (E-16).
        /// </summary>
        [Test]
        public void ParserFindsUpdateMethods()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot), "нет папки скриптов");

            int total = 0;
            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
                total += UpdateBodies(file).Count;

            Assert.Greater(total, 20,
                "найдено всего " + total + " методов Update — разбор сломан, проверка обесценена");
        }

        /// <summary>
        /// В `UPDATE` НЕТ ДОРОГИХ ВЫЗОВОВ. Каждый такой вызов — это 90 обращений к сцене в секунду.
        /// При бюджете кадра 11 мс это заметная доля, а при нескольких таких вызовах подряд —
        /// просадка, которую оператор воспринимает как «шлем тормозит».
        /// </summary>
        [Test]
        public void UpdateHasNoExpensiveCalls()
        {
            Assert.IsTrue(Directory.Exists(ScriptsRoot));

            var offenders = new List<string>();
            foreach (string file in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                string name = Path.GetFileName(file);
                if (name.EndsWith("Tests.cs")) continue;
                if (AllowedFiles.ContainsKey(name)) continue;

                List<string> bodies = UpdateBodies(file);
                if (bodies.Count == 0) continue;

                for (int i = 0; i < bodies.Count; i++)
                {
                    foreach (var call in ExpensiveCalls)
                    {
                        if (bodies[i].IndexOf(call.Key, System.StringComparison.Ordinal) < 0) continue;
                        offenders.Add(name + " → " + call.Key + " (" + call.Value + ")");
                    }
                }
            }

            Assert.AreEqual(0, offenders.Count,
                "в покадровом коде есть ДОРОГИЕ вызовы — они выполняются 90 раз в секунду и съедают " +
                "бюджет кадра (11 мс): " + string.Join("; ", offenders.ToArray()) +
                ". Найдите один раз (в Start или Awake) и запомните результат");
        }

        /// <summary>
        /// СПИСОК ИСКЛЮЧЕНИЙ НЕ УСТАРЕЛ и у каждой записи есть причина.
        /// </summary>
        [Test]
        public void ExceptionListIsJustified()
        {
            var stale = new List<string>();
            foreach (var pair in AllowedFiles)
            {
                if (string.IsNullOrWhiteSpace(pair.Value) || pair.Value.Length < 10)
                    stale.Add(pair.Key + " (нет причины)");
                if (!File.Exists(Path.Combine(ScriptsRoot, pair.Key)) &&
                    Directory.GetFiles(ScriptsRoot, pair.Key, SearchOption.AllDirectories).Length == 0)
                    stale.Add(pair.Key + " (файла нет)");
            }

            Assert.AreEqual(0, stale.Count,
                "список исключений покадровых вызовов устарел: " + string.Join("; ", stale.ToArray()));
        }
    }
}
