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
        [Category(KazistovVvTests.KvTestCategories.Fast)]
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
        /// ОДНА ЦЕЛЬ РАЗВОДИТСЯ НА ДВЕ РУКИ (исправление 02.11.2026).
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ: первая версия SetTarget(position) отправляла ОБЕ руки
        /// в одну точку. Инструменты сходились, и двурукий выглядел как однорукий. Владелец
        /// просил «правильно», поэтому разводка должна быть в коде и должна иметь ненулевое
        /// значение по умолчанию.
        /// </summary>
        [Test]
        public void DualArm_OneTargetSpreadsToBothArms()
        {
            string file = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Core",
                "KvDualArmController.cs");
            Assert.IsTrue(File.Exists(file), "не найден KvDualArmController.cs: " + file);
            string text = File.ReadAllText(file);

            StringAssert.Contains("armSpreadMeters", text,
                "должен быть параметр armSpreadMeters — разводка целей двух рук");
            StringAssert.DoesNotContain("SetTargetBoth(position, position);\n        }\n\n        public override void ClearTarget",
                text,
                "нельзя отправлять обе руки в одну точку без разводки: инструменты столкнутся");
        }

        /// <summary>
        /// TCP РУК НЕ СОВПАДАЮТ В ПОКОЕ.
        ///
        /// Если в модели обе цепочки начинаются из одной точки, проверка «между инструментами»
        /// не имеет смысла. У openarm руки разнесены — это и проверяем по именам звеньев
        /// (косвенно: обе цепочки существуют независимо).
        /// </summary>
        [Test]
        public void DualArm_HasIndependentToolNames()
        {
            string file = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Core",
                "KvDualArmController.cs");
            string text = File.ReadAllText(file);

            StringAssert.Contains("openarm_left_hand_tcp", text, "не задан TCP левой руки");
            StringAssert.Contains("openarm_right_hand_tcp", text, "не задан TCP правой руки");
        }


        /// <summary>
        /// КРОНШТЕЙН НЕ ДУБЛИРУЕТСЯ МЕЖДУ КОПИЯМИ РУК (добавлено 02.11.2026).
        ///
        /// ДЕФЕКТ, КОТОРЫЙ ЭТО ЛОВИТ: KvDualArm создаёт ДВЕ копии одного префаба, и каждая
        /// содержит ВСЮ модель, включая тело-кронштейн. Если оставить кронштейн в обеих копиях,
        /// две одинаковые детали окажутся ровно в одной точке — Z-fighting: грани мерцают,
        /// на корпусе появляется «мясо». Тело одно, руки две.
        /// </summary>
        [Test]
        public void DualArm_HidesSharedBodyInSecondCopy()
        {
            string file = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Core",
                "KvDualArm.cs");
            Assert.IsTrue(File.Exists(file), "не найден KvDualArm.cs: " + file);
            string text = File.ReadAllText(file);

            StringAssert.Contains("isRightArm", text,
                "у правой копии должна скрываться общая часть модели (кронштейн): иначе " +
                "две одинаковые детали наложатся и грани будут мерцать");
            StringAssert.Contains("Z-fighting", text,
                "причина скрытия кронштейна в правой копии должна быть записана — иначе через " +
                "месяц правку уберут как «непонятную»");
        }

        /// <summary>
        /// ПАРА СОЗДАЁТСЯ ИЗ ОДНОГО ПРЕФАБА (не требует переделанной модели).
        ///
        /// Владелец планирует разделить модель в Blender, но система обязана работать УЖЕ СЕЙЧАС,
        /// с тем префабом, что есть. Иначе «двурукий» будет ждать файла, которого может не быть.
        /// </summary>
        [Test]
        public void DualArm_CanSpawnFromSinglePrefab()
        {
            string file = Path.Combine(ProjectRoot, "Assets", "_Project", "01_Scripts", "Core",
                "KvDualArm.cs");
            string text = File.ReadAllText(file);

            StringAssert.Contains("SpawnFromOnePrefab", text,
                "должен быть режим создания пары из ОДНОГО префаба");
            StringAssert.Contains("LeftPrefix", text, "должен быть префикс левой руки");
            StringAssert.Contains("RightPrefix", text, "должен быть префикс правой руки");
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

