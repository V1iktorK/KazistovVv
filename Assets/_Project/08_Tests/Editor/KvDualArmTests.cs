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
    /// ДВУРУКИЙ РОБОТ: ПРОВЕРКА МОДЕЛИ ДАННЫХ (часть 4 задачи владельца 02.11.2026:
    /// «запрограммируй правильно наконец уже двурукого»).
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ БЕЗ ЗАПУСКА СЦЕНЫ. Сам контроллер `KvDualArmController` — `MonoBehaviour`,
    /// его поведение в EditMode не проверить без сцены. Зато можно и НУЖНО проверить то, из-за
    /// чего двурукость обычно и не работает: имена звеньев в модели и структура цепочек.
    /// Если имена разошлись — руки не найдутся, и робот останется мёртвой геометрией.
    ///
    /// ОТДЕЛЬНО ПРОВЕРЯЕТСЯ СИММЕТРИЯ: у настоящего двурукого левая и правая цепочки различаются
    /// ТОЛЬКО префиксом. Если в модели правая рука названа иначе — это дефект модели, и его
    /// лучше увидеть здесь, а не в шлеме.
    /// </summary>
    public class KvDualArmTests
    {
        private static string ProjectRoot
        {
            get { return Directory.GetParent(Application.dataPath).FullName; }
        }

        private static HashSet<string> OpenArmNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            string file = Path.Combine(ProjectRoot, "Assets", "Resources", "Models", "Robots",
                "openarm.prefab");
            if (!File.Exists(file)) return names;

            string text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"m_Name: ([^\n]+)"))
            {
                string n = m.Groups[1].Value.Trim().Trim('"');
                if (string.IsNullOrEmpty(n) || n.StartsWith("!", StringComparison.Ordinal)) continue;
                if (n.Contains("_LOD")) continue;
                names.Add(n);
            }
            return names;
        }

        /// <summary>
        /// В МОДЕЛИ ЕСТЬ ОБЕ РУКИ, ПО 7 ЗВЕНЬЕВ КАЖДАЯ.
        ///
        /// 7, а не 6: у OpenArm в каждой руке по семь вращательных суставов (link0…link6),
        /// а `link7` — фланец инструмента.
        /// </summary>
        [Test]
        public void OpenArm_HasBothArmsWithSevenLinks()
        {
            HashSet<string> names = OpenArmNames();
            Assert.Greater(names.Count, 0, "не найден префаб openarm или в нём нет объектов");

            var missing = new List<string>();
            for (int i = 0; i <= 6; i++)
            {
                if (!names.Contains("openarm_left_link" + i)) missing.Add("openarm_left_link" + i);
                if (!names.Contains("openarm_right_link" + i)) missing.Add("openarm_right_link" + i);
            }

            Assert.AreEqual(0, missing.Count,
                "у двурукого не хватает звеньев: " + string.Join(", ", missing.ToArray()));
        }

        /// <summary>
        /// ЦЕПОЧКИ СИММЕТРИЧНЫ: каждому левому звену соответствует правое с тем же номером.
        ///
        /// Если это не так, модели данных «две одинаковые руки» не получится: придётся держать
        /// два разных списка имён, и любая правка модели будет ломать одну руку незаметно.
        /// </summary>
        [Test]
        public void OpenArm_ChainsAreSymmetric()
        {
            HashSet<string> names = OpenArmNames();
            var asymmetric = new List<string>();

            for (int i = 0; i <= 7; i++)
            {
                bool hasLeft = names.Contains("openarm_left_link" + i);
                bool hasRight = names.Contains("openarm_right_link" + i);
                if (hasLeft != hasRight)
                    asymmetric.Add("link" + i + ": левая " + (hasLeft ? "есть" : "нет") +
                                   ", правая " + (hasRight ? "есть" : "нет"));
            }

            Assert.AreEqual(0, asymmetric.Count,
                "цепочки рук несимметричны: " + string.Join("; ", asymmetric.ToArray()));
        }

        /// <summary>
        /// У КАЖДОЙ РУКИ ЕСТЬ ИНСТРУМЕНТ (TCP).
        ///
        /// Без TCP обратная задача не знает, куда стремиться: CCD двигает «кончик», и если
        /// кончик не задан, рука будет дёргаться в случайную сторону.
        /// </summary>
        [Test]
        public void OpenArm_HasHandTcpForEachArm()
        {
            HashSet<string> names = OpenArmNames();
            Assert.IsTrue(names.Contains("openarm_left_hand_tcp"),
                "у левой руки нет TCP (openarm_left_hand_tcp)");
            Assert.IsTrue(names.Contains("openarm_right_hand_tcp"),
                "у правой руки нет TCP (openarm_right_hand_tcp)");
        }

        /// <summary>
        /// КАТАЛОГ ЗНАЕТ, ЧТО ЭТО ДВУРУКИЙ.
        ///
        /// Если модель помечена как обычный 6-осевой, `KvRobotSpawner` создаст ей один
        /// `SixAxisController` — и вторая рука останется мёртвой. Именно эту ошибку и надо
        /// не допустить: тип модели определяет, какой контроллер ставится.
        /// </summary>
        [Test]
        public void Catalog_MarksOpenArmAsDualArm()
        {
            KvRobotModel openarm = KvRobotCatalog.Find("openarm");
            Assert.IsNotNull(openarm, "в каталоге нет модели «openarm»");
            Assert.IsTrue(openarm.IsDualArm,
                "модель «openarm» обязана быть типа «двурукий», иначе вторая рука не будет двигаться");
            Assert.AreEqual(14, openarm.Axes, "у двурукого две руки по 7 осей = 14");
        }

        /// <summary>
        /// ДВУРУКИЙ НЕ ДОЛЖЕН ИМЕТЬ ОДНОГО СПИСКА СУСТАВОВ.
        ///
        /// `KvRobotCatalog.JointNames` — это ОДНА цепочка. Для двурукого она обязана быть пустой:
        /// иначе `KvRobotSpawner` создаст контроллер с одной цепочкой и «двурукость» пропадёт.
        /// Цепочки задаются в `KvDualArmController` отдельно.
        /// </summary>
        [Test]
        public void DualArmCatalogEntry_HasNoSharedJointList()
        {
            KvRobotModel openarm = KvRobotCatalog.Find("openarm");
            Assert.IsNotNull(openarm, "в каталоге нет модели «openarm»");
            Assert.AreEqual(0, openarm.JointNames.Length,
                "у двурукого общий список суставов должен быть пустым: цепочки задаются в " +
                "KvDualArmController, иначе обе руки будут управляться как одна");
        }

        /// <summary>
        /// ОСИ ВРАЩЕНИЯ ПО УМОЛЧАНИЮ — Y.
        ///
        /// В URDF вращательные суставы крутятся вокруг Z, а в Unity-моделях после импорта —
        /// вокруг Y (ось вверх). Проверяем, что контроллер не перепутал: он берёт
        /// `localRotation * Vector3.up`, то есть локальную Y.
        /// </summary>
        [Test]
        public void DualArm_UsesLocalUpAsRotationAxis()
        {
            string file = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Core",
                "KvDualArmController.cs");
            Assert.IsTrue(File.Exists(file), "не найден KvDualArmController.cs: " + file);

            string text = File.ReadAllText(file);
            StringAssert.Contains("localRotation * Vector3.up", text,
                "ось вращения сустава двурукого берётся как локальная Y; если это изменили — " +
                "проверьте, что CCD всё ещё сходится");
        }
    }
}
