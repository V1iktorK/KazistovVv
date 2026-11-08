using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVv.Core;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVv.Tests
{
    /// <summary>
    /// СТОРОЖ КАТАЛОГА РОБОТОВ (часть 3 задачи владельца 02.11.2026: «роботов всех перенеси»).
    ///
    /// ЗАЧЕМ. Каталог `KvRobotCatalog` описывает модели ИМЕНАМИ звеньев. Если имя в каталоге
    /// разойдётся с префабом, `KvRobotSpawner` не найдёт сустав — робот не создастся, и владелец
    /// увидит кнопку, которая ничего не делает. Лучше упасть здесь, в тесте.
    ///
    /// КАК ПРОВЕРЯЕТСЯ. Читаем префабы прямо из `Assets/Resources/Models/Robots/*.prefab`
    /// (это YAML Unity), достаём `m_Name` — и сверяем с именами из каталога. Копии `*_LOD*`
    /// не учитываем: в рантайме они тоже отбрасываются, проверка обязана видеть то же.
    /// </summary>
        [Category(KazistovVvTests.KvTestCategories.Fast)]
    public class KvRobotCatalogTests
    {
        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static string RobotsFolder
        {
            get
            {
                return Path.Combine(ProjectRoot, "Assets", "Resources", "Models", "Robots");
            }
        }

        /// <summary>Имена объектов внутри файла префаба (YAML Unity), без LOD-копий.</summary>
        private static HashSet<string> NamesInPrefab(string prefabFile)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            if (!File.Exists(prefabFile)) return names;

            string text = File.ReadAllText(prefabFile);
            foreach (Match m in Regex.Matches(text, @"m_Name: ([^\n]+)"))
            {
                string n = m.Groups[1].Value.Trim().Trim('"');
                if (string.IsNullOrEmpty(n) || n.StartsWith("!", StringComparison.Ordinal)) continue;
                if (n.Contains("_LOD")) continue;       // упрощённые меши, не кинематика
                names.Add(n);
            }
            return names;
        }

        /// <summary>
        /// У КАЖДОЙ МОДЕЛИ КАТАЛОГА ЕСТЬ ПРЕФАБ.
        ///
        /// Путь пишется как `Resources/Models/...`, потому что `Resources.Load` принимает путь
        /// БЕЗ приставки `Resources/`. Здесь проверяем файл на диске — это надёжнее, чем
        /// `Resources.Load` в EditMode без запущенной сцены.
        /// </summary>
        [Test]
        public void EveryModel_HasPrefabFile()
        {
            var missing = new List<string>();

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                string path = model.PrefabPath;
                if (path.StartsWith("Resources/", StringComparison.Ordinal))
                    path = path.Substring("Resources/".Length);
                string file = Path.Combine(ProjectRoot, "Assets", "Resources",
                    path.Replace('/', Path.DirectorySeparatorChar) + ".prefab");

                if (!File.Exists(file)) missing.Add(model.Id + " → " + file);
            }

            Assert.AreEqual(0, missing.Count,
                "У этих моделей каталога НЕТ файла префаба:\n" + string.Join("\n", missing.ToArray()));
        }

        /// <summary>
        /// ВСЕ ИМЕНА СУСТАВОВ МОДЕЛИ ЕСТЬ В ЕЁ ПРЕФАБЕ.
        ///
        /// Это главная проверка части 3: ошибка в имени = неработающая кнопка выбора робота.
        /// Для двурукого (`openarm`) проверка особенная — у него две цепочки, и они лежат
        /// в `KvDualArmController`, а не в общем списке суставов.
        /// </summary>
        [Test]
        public void EveryJointName_ExistsInPrefab()
        {
            var problems = new List<string>();

            foreach (KvRobotModel model in KvRobotCatalog.All)
            {
                if (model.IsDualArm) continue;   // проверяется отдельным тестом ниже

                string path = model.PrefabPath;
                if (path.StartsWith("Resources/", StringComparison.Ordinal))
                    path = path.Substring("Resources/".Length);
                string file = Path.Combine(ProjectRoot, "Assets", "Resources",
                    path.Replace('/', Path.DirectorySeparatorChar) + ".prefab");

                if (!File.Exists(file))
                {
                    problems.Add(model.Id + ": нет префаба (" + file + ")");
                    continue;
                }

                HashSet<string> prefabNames = NamesInPrefab(file);
                foreach (string joint in model.JointNames)
                {
                    if (!prefabNames.Contains(joint))
                        problems.Add(model.Id + ": нет звена «" + joint + "»");
                }

                if (model.JointNames.Length < 3)
                    problems.Add(model.Id + ": в каталоге меньше трёх звеньев — это не манипулятор");
            }

            Assert.AreEqual(0, problems.Count,
                "РАСХОЖДЕНИЯ КАТАЛОГА С ПРЕФАБАМИ:\n" + string.Join("\n", problems.ToArray()));
        }

        /// <summary>
        /// ДВУРУКИЙ РОБОТ: ДВЕ ПОЛНОЦЕННЫЕ ЦЕПОЧКИ.
        ///
        /// Часть 4 задачи владельца: «запрограммируй правильно наконец уже двурукого».
        /// У `openarm` в модели есть `openarm_left_link0…7` и `openarm_right_link0…7` — обе руки
        /// обязаны быть в префабе, иначе двурукость будет только на словах.
        /// </summary>
        [Test]
        public void DualArmModel_HasBothChains()
        {
            KvRobotModel dual = null;
            foreach (KvRobotModel m in KvRobotCatalog.All)
                if (m.IsDualArm) { dual = m; break; }

            Assert.IsNotNull(dual, "в каталоге нет модели с типом «двурукий»");

            string path = dual.PrefabPath;
            if (path.StartsWith("Resources/", StringComparison.Ordinal))
                path = path.Substring("Resources/".Length);
            string file = Path.Combine(ProjectRoot, "Assets", "Resources",
                path.Replace('/', Path.DirectorySeparatorChar) + ".prefab");

            Assert.IsTrue(File.Exists(file), "нет префаба двурукого: " + file);
            HashSet<string> names = NamesInPrefab(file);

            var missing = new List<string>();
            for (int i = 0; i <= 7; i++)
            {
                if (!names.Contains("openarm_left_link" + i)) missing.Add("openarm_left_link" + i);
                if (!names.Contains("openarm_right_link" + i)) missing.Add("openarm_right_link" + i);
            }

            Assert.AreEqual(0, missing.Count,
                "у двурукого не хватает звеньев: " + string.Join(", ", missing.ToArray()) +
                " — обе руки обязаны быть в модели");
        }

        /// <summary>
        /// КАТАЛОГ НЕ ДОЛЖЕН СОДЕРЖАТЬ ДУБЛИКАТОВ ID.
        ///
        /// Дубликат id означает, что `Find("ur5")` вернёт не ту модель, а меню покажет две
        /// одинаковые строки с разными роботами — и понять, какая сработает, невозможно.
        /// </summary>
        [Test]
        public void Catalog_HasNoDuplicateIds()
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var dupes = new List<string>();

            foreach (KvRobotModel m in KvRobotCatalog.All)
            {
                if (string.IsNullOrEmpty(m.Id)) { dupes.Add("(пустой id: " + m.Title + ")"); continue; }
                if (!seen.Add(m.Id)) dupes.Add(m.Id);
            }

            Assert.AreEqual(0, dupes.Count,
                "в каталоге роботов повторяются id: " + string.Join(", ", dupes.ToArray()));
        }

        /// <summary>У каждой модели обязательны название, описание и число осей.</summary>
        [Test]
        public void EveryModel_HasTitleAndAxes()
        {
            foreach (KvRobotModel m in KvRobotCatalog.All)
            {
                Assert.IsFalse(string.IsNullOrEmpty(m.Title), "у модели «" + m.Id + "» нет названия");
                Assert.IsFalse(string.IsNullOrEmpty(m.Kind), "у модели «" + m.Id + "» нет типа");
                Assert.Greater(m.Axes, 0, "у модели «" + m.Id + "» не указано число осей");
            }
        }
    }
}

