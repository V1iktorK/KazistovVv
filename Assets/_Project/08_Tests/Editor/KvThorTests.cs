// ================================================================================================
//  KvThorTests.cs — EditMode-тесты РОБОТА THOR: ОСНОВНОГО РОБОТА ПРОГРАММЫ (17.11.2026).
//
//  ЗАДАЧА ВЛАДЕЛЬЦА. «KazistovVv: робот Thor — основной в программе (модель из примитивов с
//  кинематикой Thor, по умолчанию в меню/верстаке), каталог 12+ моделей доступен везде
//  (ПК/шлем/планшет)». Отсюда четыре группы проверок:
//    1. Thor В КАТАЛОГЕ — есть, первый, геометрия разрешается, суставы находятся, команда
//       зарегистрирована, `IsEnabled` следует факту стола, `ApplyModel` ставит ЕГО;
//    2. Thor ПО УМОЛЧАНИЮ — `KvRobotCatalog.Default`, строка стартового меню шлема, правило
//       верстака (`Seg_Workbench`) и первый пункт выбора робота (`ShowRobotChooser`);
//    3. КИНЕМАТИКА THOR — прямая задача по иерархии суставов против ЭТАЛОННЫХ значений,
//       выведенных из официального чертежа (202/160/195/67,15 мм);
//    4. ПАРНОСТЬ КАТАЛОГА — 12 прежних моделей + Thor на месте, все видны в группе тулбара
//       (общей для ПК и шлема) и все имеют команду выбора.
//
//  ГДЕ ДАННЫЕ. Длины звеньев, оси суставов и таблица мешей — `KvThorModel`; меш — официальная
//  сборка FreeCAD проекта AngelLM/Thor (`Assets/_Project/99_ModelSources/Thor/meshes/*.obj`),
//  исходники STEP лежат рядом. Префаб собирается из описателя `KvThorPrefabBuilder`.
// ================================================================================================

using System.Collections.Generic;
using KazistovVv.Core;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KazistovVvTests
{
        [Category(KvTestCategories.Fast)]
    public class KvThorTests
    {
        /// <summary>Корни сцены ДО теста: созданные роботы обязаны исчезнуть в TearDown.</summary>
        private readonly List<GameObject> rootsBefore = new List<GameObject>();

        /// <summary>Снимок реестра команд до штатной регистрации (возвращается в TearDown).</summary>
        private static List<KvCommand> registrySnapshot;

        [SetUp]
        public void SetUp()
        {
            rootsBefore.Clear();
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
                rootsBefore.Add(root);

            if (registrySnapshot == null)
            {
                registrySnapshot = new List<KvCommand>();
                foreach (KvCommand command in KvCommands.All) registrySnapshot.Add(command);
            }
        }

        /// <summary>ПУСТАЯ СЦЕНА: всё созданное тестом уничтожается, реестр команд возвращается.</summary>
        [TearDown]
        public void TearDown()
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (rootsBefore.Contains(root)) continue;
                if (root == null) continue;
                if (root.name.StartsWith("[", System.StringComparison.Ordinal)) continue;
                UnityEngine.Object.DestroyImmediate(root);
            }

            if (registrySnapshot != null)
            {
                KvCommands.Clear();
                foreach (KvCommand command in registrySnapshot) KvCommands.Register(command);
                registrySnapshot = null;
            }

            KvRobotCatalog.InvalidateAvailability();
            KvRobotCommands.InvalidateTableCache();
        }

        // ========================================================================================
        // 1. THOR В КАТАЛОГЕ
        // ========================================================================================

        /// <summary>
        /// THOR — ПЕРВАЯ МОДЕЛЬ КАТАЛОГА, С ГЕОМЕТРИЕЙ И СУСТАВАМИ, ЕГО КОМАНДА ДОСТУПНА.
        ///
        /// Это ядро обещания владельца: Thor не «ещё одна строка», а модель, которую МОЖНО
        /// выбрать. Проверяется целиком: запись → меш → суставы → команда → условие стола →
        /// факт постановки (`ApplyModel`), потому что именно так устроен путь оператора.
        /// </summary>
        [Test]
        public void Thor_IsFirstInCatalog_WithGeometryJointsAndCommand()
        {
            KvRobotModel thor = KvRobotCatalog.Find(KvThorModel.Id);
            Assert.IsNotNull(thor, "THOR отсутствует в каталоге — основной робот не заявлен");
            Assert.AreSame(thor, KvRobotCatalog.All[0],
                "THOR обязан быть ПЕРВЫМ в каталоге: порядок списка — это порядок показа в меню");

            // --- геометрия: меш официальной сборки, а не пустышка
            Assert.IsTrue(KvRobotCatalog.HasGeometry(thor),
                "у THOR нет разрешаемой геометрии — «выбор робота» не даст ничего видимого");
            Assert.Greater(KvRobotCatalog.GeometryMeshCount(thor), 0,
                "счётчик мешей THOR равен нулю");
            Assert.AreEqual("", KvRobotCommands.UnavailableReason(thor),
                "интерфейс считает THOR недоступным: " + KvRobotCommands.UnavailableReason(thor));

            // --- суставы: те же имена, что ищет спавнер
            GameObject prefab = KvRobotSpawner.LoadPrefab(thor);
            Assert.IsNotNull(prefab, "префаб THOR не грузится: " + thor.PrefabPath);
            Transform[] joints = KvRobotSpawner.FindJoints(prefab.transform, thor.JointNames);
            Assert.AreEqual(6, joints.Length,
                "в префабе THOR нашлось " + joints.Length + " суставов из шести (" +
                string.Join(", ", thor.JointNames) + ")");
            foreach (Transform joint in joints)
                Assert.IsNotNull(joint, "сустав THOR не найден в иерархии префаба");

            // --- TCP на месте
            Assert.IsNotNull(KvRobotSpawner.FindEndEffector(prefab.transform,
                    KvThorModel.EndEffectorName, joints),
                "в префабе THOR нет объекта инструмента «" + KvThorModel.EndEffectorName + "»");

            // --- команда выбора: зарегистрирована, условие следует факту стола
            //
            // РЕЕСТР ОБЩИЙ НА ВЕСЬ ПРОГОН: чужой TearDown (KvRobotCatalogCoverageTests) оставляет
            // в нём «снятую» заглушку БЕЗ условия, а `RegisterModels` не перезаписывает уже
            // существующий id. Без очистки проверялось бы состояние, оставленное другим тестом.
            // Очищаем реестр и регистрируем модели штатно; снимок в TearDown вернёт всё как было.
            KvCommands.Clear();
            KvRobotCommands.RegisterModels(null);
            KvCommand command = KvCommands.Get(KvRobotCommands.ModelCommandPrefix + KvThorModel.Id);
            Assert.IsNotNull(command, "команда выбора THOR не зарегистрирована");
            Assert.IsNotNull(command.IsEnabled, "у команды THOR нет условия IsEnabled");

            KvRobotCommands.InvalidateTableCache();
            Assert.IsFalse(command.Enabled,
                "команда THOR доступна, хотя стола в сцене нет — условие не читает факт");

            var table = new GameObject("KvThorTests_Стол тестовый");
            table.AddComponent<RegisteredObject>().DisplayName = "Стол тестовый";
            try
            {
                KvRobotCommands.InvalidateTableCache();
                Assert.IsTrue(command.Enabled,
                    "команда THOR недоступна, когда стол в сцене есть — оператор не сможет " +
                    "поставить основного робота");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(table);
                KvRobotCommands.InvalidateTableCache();
            }

            // --- ApplyModel СТАВИТ ИМЕННО THOR (суставы, контроллер, имя в иерархии).
            // Считаем РАЗНИЦУ, а не абсолют: EditMode-прогон общий, и чужой робот,
            // оставшийся в сцене, не должен ломать проверку «добавился именно THOR».
            int before = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None).Length;
            KvRobotCommands.ApplyModel(thor);
            RobotController[] placed = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.LessOrEqual(placed.Length, before + 1,
                "выбор модели создал больше одного робота (было " + before + ", стало " +
                placed.Length + ")");

            RobotController robot = null;
            foreach (RobotController candidate in placed)
            {
                if (candidate != null && candidate.gameObject.name.Contains("THOR"))
                {
                    robot = candidate;
                    break;
                }
            }
            Assert.IsNotNull(robot, "поставлен не THOR: " + Names(placed));

            SixAxisController six = robot as SixAxisController;
            Assert.IsNotNull(six, "THOR должен работать на шестиосевом контроллере");
            Assert.IsNotNull(six.jointTransforms, "у THOR не заполнены суставы");
            Assert.AreEqual(6, six.jointTransforms.Length, "у THOR не шесть суставов");
            Assert.IsNotNull(six.endEffector, "у THOR не назначен инструмент (TCP)");

            // ОСИ ИЗ ОПИСАТЕЛЯ, а не дефолтные под чужую модель (J4 — вдоль предплечья).
            Assert.AreEqual(KvThorModel.JointAxesLocal.Length, six.jointAxesLocal.Length,
                "спавнер не применил оси THOR из описателя");
            Assert.AreEqual(Vector3.up, six.jointAxesLocal[3],
                "ось J4 THOR не вдоль предплечья — yaw запястья поедет не туда");
            Assert.IsFalse(six.fixLegacyMeshParents,
                "у THOR включена компенсация пивота FBX — она перенесла бы звено запястья на локоть");
        }

        // ========================================================================================
        // 2. THOR ПО УМОЛЧАНИЮ: СТАРТОВОЕ МЕНЮ, ВЕРСТАК, ВЫБОР РОБОТА
        // ========================================================================================

        /// <summary>
        /// THOR — МОДЕЛЬ ПО УМОЛЧАНИЮ И ПЕРВЫЙ ВО ВСЕХ ТРЁХ ТОЧКАХ ВЫБОРА.
        ///
        /// Точки по требованиям владельца: `KvRobotCatalog.Default` (от него считается старт),
        /// строка стартового меню шлема «Добавить робота THOR» (действие — создать шестиосевого),
        /// правило верстака `Seg_Workbench` (активирует THOR, если он в сцене) и первый пункт
        /// `ShowRobotChooser` (ставит модель каталога, а не сценный шаблон).
        /// </summary>
        [Test]
        public void Thor_IsDefault_InCatalogStartMenuWorkbenchAndChooser()
        {
            // --- каталог
            KvRobotModel def = KvRobotCatalog.Default;
            Assert.IsNotNull(def, "у каталога нет модели по умолчанию");
            Assert.AreEqual(KvThorModel.Id, def.Id,
                "моделью по умолчанию перестал быть THOR — основной робот программы");

            // --- стартовое меню шлема: строка называет THOR и создаёт шестиосевого
            List<string> rows = KvXrStartMenuRows.Robots(0);
            Assert.Contains(KvXrStartMenuRows.AddSixAxisRowLabel, rows,
                "строки меню шлема не содержат строку создания основного робота");
            StringAssert.Contains("THOR", KvXrStartMenuRows.AddSixAxisRowLabel,
                "строка создания основного робота не называет THOR — владелец не узнает свою кнопку");
            Assert.AreEqual(KvXrStartRowAction.CreateSixAxisRobot,
                KvXrStartMenuRows.ActionFor(KvXrStartSection.Robots, 0, 0, 0),
                "первая строка создания робота ведёт не на шестиосевого");
            // Подпись и действие обязаны ссылаться на ОДНУ строку (правило §42): индекс подписи
            // в списке и есть та строка, которую нажатие разбирает в CreateSixAxisRobot.
            int labelIndex = rows.IndexOf(KvXrStartMenuRows.AddSixAxisRowLabel);
            Assert.AreEqual(KvXrStartRowAction.CreateSixAxisRobot,
                KvXrStartMenuRows.ActionFor(KvXrStartSection.Robots, labelIndex, 0, 0),
                "строка с подписью основного робота не ведёт к созданию шестиосевого");

            // --- верстак: сегмент «робот» активирует THOR, даже если он НЕ первый в списке
            var other = new GameObject("KvThorTests_простой робот");
            var thorGo = new GameObject("KvThorTests_THOR");
            try
            {
                RobotController plain = other.AddComponent<SixAxisController>();
                plain.robotName = "Robot";
                RobotController thor = thorGo.AddComponent<SixAxisController>();
                thor.robotName = KvRobotCatalog.Default.Title;

                var both = new[] { plain, thor };
                Assert.AreEqual(1, KazistovVvUIManager.PickWorkbenchRobot(both, 0),
                    "верстак не выбрал THOR, хотя тот есть в списке (сегмент «робот»)");
                Assert.AreEqual(0, KazistovVvUIManager.PickWorkbenchRobot(new[] { plain }, 0),
                    "без THOR верстак обязан выбирать первого шестиосевого, как раньше");
                Assert.AreEqual(-1, KazistovVvUIManager.PickWorkbenchRobot(
                        new RobotController[0], 0),
                    "без роботов верстак не выбирает ничего");
                Assert.IsFalse(KazistovVvUIManager.IsDefaultRobot(plain),
                    "простой робот ошибочно опознан как основной");
                Assert.IsTrue(KazistovVvUIManager.IsDefaultRobot(thor),
                    "робот с именем модели по умолчанию не опознан как основной");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(other);
                UnityEngine.Object.DestroyImmediate(thorGo);
            }

            // --- выбор робота (ShowRobotChooser): первый пункт ставит модель каталога
            KvRobotModel chosen = KazistovVvUIManager.CatalogModelForChooser(0);
            Assert.IsNotNull(chosen, "первый пункт выбора робота не вернул модель каталога");
            Assert.AreEqual(KvThorModel.Id, chosen.Id,
                "первый пункт выбора робота ставит не THOR");
            Assert.IsNull(KazistovVvUIManager.CatalogModelForChooser(1),
                "пункт SCARA неожиданно вернул модель каталога");
            StringAssert.StartsWith("THOR", CenterWindow.SixAxisChooserLabel,
                "подпись первого пункта выбора робота не называет THOR: " +
                CenterWindow.SixAxisChooserLabel);
        }

        // ========================================================================================
        // 3. КИНЕМАТИКА THOR: ПРЯМАЯ ЗАДАЧА ПРОТИВ ЭТАЛОННЫХ ЗНАЧЕНИЙ
        // ========================================================================================

        /// <summary>
        /// FK THOR В ТРЁХ ПОЗАХ ПРОТИВ ЭТАЛОНА С ЧЕРТЕЖА.
        ///
        /// Эталон посчитан ВРУЧНУЮ из официальных размеров Thor (doc/ThorDimensions.png:
        /// 202 / 160 / 195 / 67,15 мм) для нулевой позы «рука вытянута вверх»:
        ///   • все нули: TCP = (0; 0,202 + 0,160 + 0,195 + 0,06715; 0) = (0; 0,62415; 0) —
        ///     и это ровно «625 мм вытянутый» из README проекта Thor (разница 0,85 мм);
        ///   • J2 = 90°: плечо ложится горизонтально → TCP = (−0,42215; 0,202; 0);
        ///   • J2 = 90°, J3 = −90°: предплечье возвращается вверх → TCP = (−0,160; 0,46415; 0).
        ///
        /// Знак X проверяется через `Quaternion.AngleAxis` — ту же операцию, что делает
        /// `SixAxisController.SetJointAngle`, поэтому тест меряет ФАКТИЧЕСКУЮ иерархию, а не
        /// собственную копию формулы.
        /// </summary>
        [Test]
        public void Thor_ForwardKinematics_MatchesReferencePoses()
        {
            KvRobotModel thor = KvRobotCatalog.Find(KvThorModel.Id);
            Assert.IsNotNull(thor);

            RobotController created = KvRobotSpawner.Create(thor, Vector3.zero, 0f);
            Assert.IsNotNull(created, "THOR не создался — FK проверять нечего");

            SixAxisController six = created as SixAxisController;
            Assert.IsNotNull(six, "THOR должен получить SixAxisController");
            Assert.AreEqual(6, six.jointTransforms.Length, "у THOR не шесть суставов");

            Transform tcp = six.endEffector;
            Assert.IsNotNull(tcp, "у THOR нет TCP — прямую задачу не измерить");

            // Кэш осей контроллера считает нулевой позой ТЕКУЩУЮ: прогреваем в нулях.
            float[] zeroReadback = six.GetJointAngles();
            Assert.AreEqual(6, zeroReadback.Length, "отчёт углов THOR не на шесть осей");

            Vector3[] axes = KvThorModel.JointAxesLocal;

            // --- ПОЗА A: все нули — робот вытянут вверх.
            SetPose(six, new float[6]);
            AssertVec(new Vector3(0f, KvThorModel.StretchedHeightM, 0f), tcp.position,
                "поза A (все нули)");

            // --- ПОЗА B: J2 = 90° — плечо горизонтально.
            SetPose(six, new[] { 0f, 90f, 0f, 0f, 0f, 0f });
            AssertVec(new Vector3(-KvThorModel.UpperArmM - KvThorModel.ForearmM -
                                   KvThorModel.WristToTcpM, KvThorModel.BaseToShoulderM, 0f),
                tcp.position, "поза B (J2 = 90°)");

            // --- ПОЗА C: J2 = 90°, J3 = −90° — предплечье снова вверх.
            SetPose(six, new[] { 0f, 90f, -90f, 0f, 0f, 0f });
            AssertVec(new Vector3(-KvThorModel.UpperArmM,
                KvThorModel.BaseToShoulderM + KvThorModel.ForearmM + KvThorModel.WristToTcpM, 0f),
                tcp.position, "поза C (J2 = 90°, J3 = −90°)");

            // --- ОТЧЁТ УГЛОВ = ТО, ЧТО ЗАДАЛИ: контроллер меряет по своей оси, а не по своей формуле.
            float[] readback = six.GetJointAngles();
            Assert.AreEqual(90f, readback[1], 0.5f,
                "контроллер не измерил заданный угол J2 (получил " + readback[1] + ")");
            Assert.AreEqual(-90f, readback[2], 0.5f,
                "контроллер не измерил заданный угол J3 (получил " + readback[2] + ")");

            // --- возврат в ноль: иерархия не «запоминает» позу
            SetPose(six, new float[6]);
            AssertVec(new Vector3(0f, KvThorModel.StretchedHeightM, 0f), tcp.position,
                "возврат в нулевую позу");
        }

        /// <summary>Имена роботов через запятую — для сообщений об ошибке.</summary>
        private static string Names(RobotController[] robots)
        {
            var sb = new System.Text.StringBuilder();
            foreach (RobotController rc in robots)
            {
                if (rc == null) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(rc.gameObject.name);
            }
            return sb.Length == 0 ? "(роботов нет)" : sb.ToString();
        }

        /// <summary>Поставить суставы так же, как это делает `SixAxisController` (q0 = identity).</summary>
        private static void SetPose(SixAxisController six, float[] degrees)
        {
            Vector3[] axes = KvThorModel.JointAxesLocal;
            for (int i = 0; i < six.jointTransforms.Length && i < degrees.Length; i++)
                six.jointTransforms[i].localRotation =
                    Quaternion.AngleAxis(degrees[i], axes[i]);
        }

        private static void AssertVec(Vector3 expected, Vector3 actual, string pose)
        {
            Assert.AreEqual(expected.x, actual.x, 0.002f, pose + ": X");
            Assert.AreEqual(expected.y, actual.y, 0.002f, pose + ": Y");
            Assert.AreEqual(expected.z, actual.z, 0.002f, pose + ": Z");
        }

        // ========================================================================================
        // 4. ПАРНОСТЬ КАТАЛОГА: 12 ПРЕЖНИХ МОДЕЛЕЙ + THOR, ВСЕ ВИДНЫ ВЕЗДЕ
        // ========================================================================================

        /// <summary>
        /// В КАТАЛОГЕ 13 МОДЕЛЕЙ (12 ПРЕЖНИХ + THOR), И КАЖДАЯ ЕСТЬ В ГРУППЕ ТУЛБАРА И В РЕЕСТРЕ.
        ///
        /// Группа «Робот» (`KvToolbarGroups`) — общий источник кнопок для ПК и шлема, а реестр
        /// команд — источник для панели планшета и палитры. Если модель попала в каталог, но не
        /// попала в эти два списка, на какой-то поверхности её просто нет — ровно то, что
        /// требование «каталог доступен везде» и запрещает.
        /// </summary>
        [Test]
        public void Catalog_HasTwelveLegacyModelsPlusThor_Everywhere()
        {
            string[] legacy =
            {
                "stand_6axis", "scara_ls10b702s", "ur5", "kuka_iiwa7", "kuka_iiwa14",
                "abb_irb6640", "fanuc_m20ib", "franka_fr3", "franka_fr3_v2", "kinova_gen3",
                "comau_edo", "openarm"
            };

            KvRobotModel[] all = KvRobotCatalog.All;
            Assert.AreEqual(legacy.Length + 1, all.Length,
                "в каталоге " + all.Length + " моделей, ожидалось " + (legacy.Length + 1) +
                " (12 прежних + THOR)");

            foreach (string id in legacy)
                Assert.IsNotNull(KvRobotCatalog.Find(id),
                    "прежняя модель «" + id + "» пропала из каталога — парность нарушена");
            Assert.IsNotNull(KvRobotCatalog.Find(KvThorModel.Id), "THOR пропал из каталога");

            // --- каждая модель имеет команду выбора (реестр — общий для ПК, планшета и шлема)
            KvRobotCommands.RegisterModels(null);
            KvToolbarGroup robotGroup = KvToolbarGroups.Find("robot");
            Assert.IsNotNull(robotGroup, "группы «robot» в тулбаре нет");

            var problems = new List<string>();
            foreach (KvRobotModel model in all)
            {
                string id = KvRobotCommands.ModelCommandPrefix + model.Id;
                if (KvCommands.Get(id) == null) problems.Add(model.Id + ": нет команды");
                if (System.Array.IndexOf(robotGroup.Commands, id) < 0)
                    problems.Add(model.Id + ": нет кнопки в группе «Робот» (ПК и шлем)");
            }

            Assert.AreEqual(0, problems.Count,
                "модели каталога не доехали до поверхностей: " + string.Join("; ", problems.ToArray()));

            // THOR — ПЕРВЫЙ и в каталоге, и в группе кнопок (основной робот идёт раньше
            // «6-осевого (стандартного)» на всех поверхностях).
            int thorIndex = System.Array.IndexOf(robotGroup.Commands,
                KvRobotCommands.ModelCommandPrefix + KvThorModel.Id);
            int standIndex = System.Array.IndexOf(robotGroup.Commands,
                KvRobotCommands.ModelCommandPrefix + "stand_6axis");
            Assert.GreaterOrEqual(thorIndex, 0, "команды THOR нет в группе кнопок");
            Assert.GreaterOrEqual(standIndex, 0, "команды «6-осевой (стандартный)» нет в группе");
            Assert.Less(thorIndex, standIndex,
                "THOR обязан идти раньше «6-осевого (стандартного)» — он основной робот");
        }
    }
}
