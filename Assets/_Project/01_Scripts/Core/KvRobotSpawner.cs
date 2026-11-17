using System;
using System.Collections.Generic;
using KazistovVvUI;   // RegisteredObject живёт в этом namespace
using UnityEngine;

namespace KazistovVv.Core
{
    /// <summary>
    /// СОЗДАНИЕ РОБОТА ИЗ МОДЕЛИ КАТАЛОГА (часть 3 задачи владельца 02.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА: «роботов всех перенеси». Выбранный вариант: «Выбор из списка — один
    /// активный», то есть на столе стоит один робот, а модель выбирается в меню.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КЛАСС, А НЕ МЕТОД В ПАНЕЛИ. Создание робота — не UI: та же операция нужна
    /// тестам, отчётам и загрузке сессии. В панели остаётся только «нажали — вызвали».
    ///
    /// ГЛАВНОЕ, ЧТО ЗДЕСЬ ПРОИСХОДИТ:
    ///   1. префаб модели грузится из `Resources`;
    ///   2. в нём находятся суставы ПО ИМЕНАМ из каталога (копии `*_LOD*` отбрасываются —
    ///      это упрощённые меши для рендера, а не кинематика);
    ///   3. на объект ставится `RobotController` нужного типа (шестиосевой или SCARA);
    ///   4. заполняются `jointTransforms` и `endEffector`;
    ///   5. объект регистрируется (`RegisteredObject`), чтобы дерево проекта и меню его видели.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ И ПОЧЕМУ. Нет «умной» подгонки DH: в проекте обратная задача решается
    /// осевым CCD, а оси берутся из трансформов. Поэтому НЕПРАВИЛЬНЫЕ имена звеньев — единственное,
    /// что может сломать модель, и именно это проверяет `KvRobotCatalogTests`.
    /// </summary>
    public static class KvRobotSpawner
    {
        /// <summary>Приставка к имени объекта, чтобы отличить созданного робота в иерархии.</summary>
        public const string SpawnedPrefix = "Робот_";

        /// <summary>
        /// СОЗДАТЬ РОБОТА ИЗ МОДЕЛИ КАТАЛОГА.
        ///
        /// Возвращает `null` и пишет причину в журнал, если модель не найдена или у неё не
        /// нашлись суставы. Молча создавать «пустышку» нельзя: оператор увидит на столе
        /// неподвижную геометрию и решит, что программа сломалась.
        /// </summary>
        public static RobotController Create(KvRobotModel model, Vector3 position, float yawDegrees,
                                             Transform parent = null)
        {
            if (model == null)
            {
                Debug.LogWarning("[KvRobot] модель не задана — робот не создан");
                return null;
            }

            // ПОСЛЕДНЯЯ ЛИНИЯ ОБОРОНЫ: МОДЕЛЬ БЕЗ ГЕОМЕТРИИ НЕ СОЗДАЁТСЯ (12.11.2026).
            //
            // ЧТО БЫЛО. Создание проверяло только наличие префаба и суставов. У 11 моделей каталога
            // префаб есть, суставы есть — и создавался робот БЕЗ ЕДИНОГО МЕША: на столе и в шлеме
            // ничего не появлялось, хотя журнал бодро писал «создан робот «UR5» · суставов 6».
            //
            // ЧЕМ ПЛОХО. Оператор нажимает «Робот: UR5» и не видит ничего: «нажал — не работает».
            // Хуже того, `KvRobotCommands.ApplyModel` и `KvArContent.SpawnModel` удаляют текущего
            // робота ДО создания нового, поэтому выбор недоступной модели уносил рабочего робота.
            //
            // ЧТО СТАЛО. Отказ с названной причиной (а не молчаливая пустышка) — здесь, в одной
            // точке, чтобы ни один путь создания не мог обойти проверку. Вызывающие, которые
            // удаляют робота перед созданием, спрашивают `KvRobotCatalog.HasGeometry` ЗАРАНЕЕ
            // (`UnavailableReason`), иначе отказ случился бы слишком поздно.
            if (!KvRobotCatalog.HasGeometry(model))
            {
                Debug.LogWarning("[KvRobot] модель «" + model.Title + "» НЕ создана: у её префаба нет " +
                                 "разрешаемой геометрии (меши отсутствуют в проекте) — в сцене был бы " +
                                 "робот без единого меша. Недоступные модели: " +
                                 KvRobotCatalog.DescribeGeometry());
                return null;
            }

            GameObject prefab = LoadPrefab(model);
            if (prefab == null)
            {
                Debug.LogWarning("[KvRobot] не найден префаб модели «" + model.Title + "» по пути " +
                                 model.PrefabPath + " — робот не создан");
                return null;
            }

            GameObject go = UnityEngine.Object.Instantiate(prefab, position,
                Quaternion.Euler(0f, yawDegrees, 0f));
            go.name = SpawnedPrefix + model.Title + "_" + DateTime.Now.ToString("HHmmss");
            if (parent != null) go.transform.SetParent(parent, true);

            // --- контроллер нужного типа
            //
            // ТРИ РАЗНЫХ КОНТРОЛЛЕРА, А НЕ ДВА. Двурукий робот НЕ МОЖЕТ использовать
            // SixAxisController: у того один массив суставов и один TCP, поэтому вторая рука
            // осталась бы мёртвой геометрией — ровно та проблема, на которую владелец и жаловался
            // («запрограммируй правильно наконец уже двурукого»). Поэтому для двурукого ставится
            // KvDualArmController с двумя независимыми цепочками.
            RobotController robot = go.GetComponent<RobotController>();
            if (robot == null)
            {
                if (model.IsDualArm)
                    robot = (RobotController)go.AddComponent<KvDualArmController>();
                else if (model.IsScara)
                    robot = (RobotController)go.AddComponent<SCARAController>();
                else
                    robot = (RobotController)go.AddComponent<SixAxisController>();
            }

            // ДВУРУКИЙ: ДВЕ РУКИ КАК ДВА РОБОТА (решение владельца 02.11.2026).
            //
            // Владелец: «при добавлении робота на сцену формально получится что добавлены стали
            // два робота и управление будет как-будто двумя, а не одним». Это лучше отдельного
            // двурукого контроллера: вся система (IK, фантомы, траектории, коллизии) уже умеет
            // работать с одной цепочкой, и ничего переписывать не надо.
            //
            // Здесь возвращаем ПЕРВУЮ руку (левую): вызывающий код ждёт один контроллер.
            // Обе руки лежат под общим кронштейном и находятся поиском по сцене.
            if (model.IsDualArm)
            {
                // Убираем одиночную копию: пару создаём отдельно, чтобы не было трёх рук.
                UnityEngine.Object.Destroy(go);

                RobotController[] pair = KvDualArm.SpawnFromOnePrefab(model, position, yawDegrees);
                if (pair.Length == 0)
                {
                    Debug.LogWarning("[KvRobot] двурукий «" + model.Title + "» не создан — " +
                                     "смотрите причину строкой выше");
                    return null;
                }

                if (!KvDualArm.IsPairReady(pair))
                {
                    Debug.LogWarning("[KvRobot] пара рук создана, но НЕ готова к движению: " +
                                     "у одной из рук меньше трёх суставов");
                }
                return pair[0];
            }

            // --- суставы по именам
            Transform[] joints = FindJoints(go.transform, model.JointNames);
            if (joints == null || joints.Length == 0)
            {
                Debug.LogWarning("[KvRobot] у модели «" + model.Title + "» не найдены суставы (" +
                                 model.JointNames.Length + " имён: " +
                                 string.Join(", ", model.JointNames) + ") — робот не создан. " +
                                 "Проверьте имена звеньев в префабе.");
                UnityEngine.Object.Destroy(go);
                return null;
            }

            ApplyJoints(robot, joints, model);
            ApplyJointAxes(robot, model);

            // --- TCP
            Transform tcp = FindEndEffector(go.transform, model.EndEffectorName, joints);
            if (tcp != null) ApplyEndEffector(robot, tcp);

            // --- регистрация и материалы (как у штатного робота)
            if (go.GetComponent<RegisteredObject>() == null)
                go.AddComponent<RegisteredObject>();

            Debug.Log("[KvRobot] создан робот «" + model.Title + "» · суставов " + joints.Length +
                      (tcp != null ? " · TCP «" + tcp.name + "»" : " · TCP по последнему суставу") +
                      " · тип " + model.Kind);
            return robot;
        }

        /// <summary>
        /// НАЙТИ СУСТАВЫ ПО ИМЕНАМ. Копии `*_LOD*` и `*_LOD0..3` отбрасываются.
        ///
        /// ПОЧЕМУ ИМЕННО ТАК. Импортированные модели содержат по 4 упрощённые копии каждого
        /// меша (`link_1.001_LOD2` и т.п.). Если взять первую попавшуюся по имени, контроллер
        /// получит LOD-двойника — он не в кинематической цепочке, и робот будет дёргаться.
        /// Поэтому сравнение ТОЧНОЕ по имени, а LOD-варианты просто не совпадут.
        /// </summary>
        public static Transform[] FindJoints(Transform root, string[] names)
        {
            var found = new List<Transform>();
            if (root == null || names == null) return found.ToArray();

            var index = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                if (!index.ContainsKey(t.name)) index[t.name] = t;
            }

            foreach (string name in names)
            {
                if (string.IsNullOrEmpty(name)) continue;
                Transform t;
                if (index.TryGetValue(name, out t)) found.Add(t);
                else Debug.LogWarning("[KvRobot] сустав «" + name + "» не найден в модели");
            }
            return found.ToArray();
        }

        /// <summary>Инструмент: по имени, иначе последний сустав (фланец).</summary>
        public static Transform FindEndEffector(Transform root, string name, Transform[] joints)
        {
            if (!string.IsNullOrEmpty(name))
            {
                foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                    if (t != null && string.Equals(t.name, name, StringComparison.Ordinal)) return t;
            }
            return joints != null && joints.Length > 0 ? joints[joints.Length - 1] : null;
        }

        /// <summary>
        /// ЗАПОЛНИТЬ СУСТАВЫ У КОНТРОЛЛЕРА.
        ///
        /// Работает через рефлексию по имени поля: у `SixAxisController` и `SCARAController`
        /// разные типы, а общий предок `RobotController` массив суставов не объявляет.
        /// Рефлексия здесь — не «хак ради хака»: альтернатива — добавить поле в базовый класс
        /// и переписать оба контроллера, что рискует сломать проверенные движения.
        /// </summary>
        private static void ApplyJoints(RobotController robot, Transform[] joints, KvRobotModel model)
        {
            if (robot == null || joints == null) return;

            System.Reflection.FieldInfo field = robot.GetType().GetField("jointTransforms",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

            if (field != null && field.FieldType == typeof(Transform[]))
            {
                field.SetValue(robot, joints);
                return;
            }

            Debug.LogWarning("[KvRobot] у контроллера «" + robot.GetType().Name +
                             "» нет поля jointTransforms — суставы не привязаны");
        }

        /// <summary>
        /// ПРИМЕНИТЬ ОСИ СУСТАВОВ ИЗ ДАННЫХ МОДЕЛИ (задача THOR, 17.11.2026).
        ///
        /// Зачем. `SixAxisController` вращает каждый сустав строго вокруг СВОЕЙ оси, заданной в
        /// локальных осях сустава (`jointAxesLocal`). Для импортированных URDF-моделей оси
        /// подобраны под чужую геометрию, а модель, собранная кодом (THOR), приходит с осями
        /// из описателя `KvThorModel.JointAxesLocal` — они выведены из официальной конфигурации
        /// робота (yaw-roll-roll-yaw-roll-yaw) и из нулевой позы, в которой стоят суставы.
        ///
        /// Пустой массив у модели — обычное состояние (все прежние модели): поведение не
        /// меняется, контроллер оставляет свои оси.
        /// </summary>
        private static void ApplyJointAxes(RobotController robot, KvRobotModel model)
        {
            if (robot == null || model == null) return;
            Vector3[] axes = model.JointAxesLocal;
            if (axes == null || axes.Length == 0) return;

            System.Reflection.FieldInfo field = robot.GetType().GetField("jointAxesLocal",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            if (field != null && field.FieldType == typeof(Vector3[]))
            {
                var copy = new Vector3[axes.Length];
                Array.Copy(axes, copy, axes.Length);
                field.SetValue(robot, copy);
            }
        }

        /// <summary>Заполнить TCP у контроллера (поле называется по-разному у разных типов).</summary>
        private static void ApplyEndEffector(RobotController robot, Transform tcp)
        {
            if (robot == null || tcp == null) return;
            string[] candidates = { "endEffector", "endEffectorTransform", "tool" };
            foreach (string name in candidates)
            {
                System.Reflection.FieldInfo f = robot.GetType().GetField(name,
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.NonPublic);
                if (f != null && f.FieldType == typeof(Transform))
                {
                    f.SetValue(robot, tcp);
                    return;
                }
            }
        }

        // ============================================================================================
        // РАЗРЕШЕНИЕ ПРЕФАБА МОДЕЛИ — ОДНА ФУНКЦИЯ НА ВСЕХ (найдено тестом сессии S46).
        //
        // ЧТО БЫЛО. Путь в каталоге записан с приставкой: `Resources/Models/Robots/ur5`.
        // `Resources.Load` ждёт путь ОТ папки `Resources` (`Models/Robots/ur5`), поэтому сырой путь
        // НЕ разрешается никогда. `Create` это знал и после неудачи пробовал путь без приставки —
        // а `DescribeAvailability` (диагностика «какие модели реально можно создать») не пробовал
        // и складывал из этого строку «префаб есть у 0 · без префаба 12» при полностью рабочих
        // префабах. То есть ровно та строка, которой отчёт владельцу доказывает «роботы перенесены»,
        // всегда говорила обратное.
        //
        // ЧЕМ ЭТО ГРОЗИЛО. Два разных правила для одного пути — это класс дефекта, который уже был
        // в проекте (`SetPassthrough` звали из одного места из двух, и вся ветка MR оказалась
        // мёртвой). Здесь третий вариант пути появился бы при первой же правке любого из двух мест.
        //
        // ЧТО СТАЛО. Разрешение живёт в ОДНОМ месте (`LoadPrefab`): сначала путь как записан,
        // затем — без приставки `Resources/`. Им пользуются и создание робота, и диагностика,
        // и тесты. `LoadPrefab` возвращает null, если префаба нет; исключений не бросает.
        // ============================================================================================

        /// <summary>Приставка, которую `Resources.Load` НЕ принимает (путь считается от папки `Resources`).</summary>
        public const string ResourcesPrefix = "Resources/";

        /// <summary>
        /// ПУТЬ МОДЕЛИ В ВИДЕ, КОТОРЫЙ ПРИНИМАЕТ `Resources.Load`: без приставки `Resources/`.
        /// Нужен там, где путь печатается в журнал или сверяется с чем-то ещё (диагностика, тесты).
        /// </summary>
        public static string ResourcePathOf(KvRobotModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.PrefabPath)) return "";
            string path = model.PrefabPath;
            return path.StartsWith(ResourcesPrefix, StringComparison.Ordinal)
                ? path.Substring(ResourcesPrefix.Length)
                : path;
        }

        /// <summary>
        /// ПРЕФАБ МОДЕЛИ ИЗ `Resources` — ЕДИНСТВЕННЫЙ ПУТЬ ЗАГРУЗКИ.
        ///
        /// Пробует путь как записан в каталоге, затем — без приставки `Resources/` (каталог хранит
        /// путь вместе с приставкой, а `Resources.Load` её не понимает). Возвращает null, если
        /// префаба нет: это законное состояние, вызывающий обязан его назвать, а не падать.
        /// </summary>
        public static GameObject LoadPrefab(KvRobotModel model)
        {
            if (model == null || string.IsNullOrEmpty(model.PrefabPath)) return null;

            // 1) ПУТЬ КАК ЗАПИСАН В КАТАЛОГЕ.
            GameObject prefab = Resources.Load<GameObject>(model.PrefabPath);
            if (prefab != null) return prefab;

            // 2) ДРУГОЙ ВАРИАНТ ЗАПИСИ ТОГО ЖЕ ПУТИ — ровно один (без приставки, а если её не
            //    было — с ней). Второй запасной вариант завёл бы третье правило для одного пути.
            string alternative = model.PrefabPath.StartsWith(ResourcesPrefix, StringComparison.Ordinal)
                ? model.PrefabPath.Substring(ResourcesPrefix.Length)
                : ResourcesPrefix + model.PrefabPath;
            if (string.IsNullOrEmpty(alternative) || alternative == model.PrefabPath) return null;
            return Resources.Load<GameObject>(alternative);
        }

        /// <summary>Есть ли у модели префаб, из которого её МОЖНО создать (без загрузки суставов).</summary>
        public static bool HasPrefab(KvRobotModel model)
        {
            return LoadPrefab(model) != null;
        }

        /// <summary>
        /// ЧЕСТНАЯ СТРОКА ПРО ОДНУ МОДЕЛЬ: есть префаб или нет, и по какому пути искали.
        /// Отдельно нужна для случая «модели нет в каталоге» — тогда ответ обязан быть «нет
        /// префаба», а не исключение или пустая строка.
        /// </summary>
        public static string DescribeModelAvailability(KvRobotModel model)
        {
            if (model == null) return "модель не задана — префаба нет";
            if (string.IsNullOrEmpty(model.Id)) return "у модели нет id — префаба нет";

            if (LoadPrefab(model) != null)
                return model.Id + ": префаб есть (" + ResourcePathOf(model) + ")";

            return model.Id + ": НЕТ ПРЕФАБА (искали «" + model.PrefabPath + "» и «" +
                   ResourcePathOf(model) + "»)";
        }

        /// <summary>
        /// Строка для журнала: какие модели каталога РЕАЛЬНО можно создать.
        ///
        /// Нужна, чтобы отчёт не врал: «в каталоге 12 моделей» и «создаются 9» — разные вещи.
        /// Здесь проверяется наличие префаба, а не суставов (суставы требуют загрузки сцены).
        ///
        /// ГРУЗИТ ТЕМ ЖЕ ПУТЁМ, ЧТО И СОЗДАНИЕ (`LoadPrefab`). До этого здесь стоял сырой путь
        /// каталога, который `Resources.Load` не разрешает, — и диагностика утверждала «префаб есть
        /// у 0» при полностью доступных префабах.
        /// </summary>
        public static string DescribeAvailability()
        {
            var sb = new System.Text.StringBuilder();
            int ok = 0, noPrefab = 0;
            foreach (KvRobotModel m in KvRobotCatalog.All)
            {
                if (HasPrefab(m)) { ok++; continue; }
                noPrefab++;
                // Причина называется по модели: «НЕТ ПРЕФАБА» + оба пути, которые пробовали.
                sb.Append(DescribeModelAvailability(m)).Append("; ");
            }
            return "моделей " + KvRobotCatalog.All.Length + " · префаб есть у " + ok +
                   (noPrefab > 0 ? " · без префаба " + noPrefab + " (" + sb + ")" : "");
        }
    }
}
