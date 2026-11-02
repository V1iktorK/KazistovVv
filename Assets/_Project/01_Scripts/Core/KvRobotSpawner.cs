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

            GameObject prefab = Resources.Load<GameObject>(model.PrefabPath);
            if (prefab == null)
            {
                // Пробуем без приставки «Resources/» — путь мог быть записан по-разному.
                string alt = model.PrefabPath.StartsWith("Resources/", StringComparison.Ordinal)
                    ? model.PrefabPath.Substring("Resources/".Length)
                    : "Resources/" + model.PrefabPath;
                prefab = Resources.Load<GameObject>(alt);
            }
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
            RobotController robot = go.GetComponent<RobotController>();
            if (robot == null)
            {
                robot = model.IsScara
                    ? (RobotController)go.AddComponent<SCARAController>()
                    : (RobotController)go.AddComponent<SixAxisController>();
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

        /// <summary>
        /// Строка для журнала: какие модели каталога РЕАЛЬНО можно создать.
        ///
        /// Нужна, чтобы отчёт не врал: «в каталоге 12 моделей» и «создаются 9» — разные вещи.
        /// Здесь проверяется наличие префаба, а не суставов (суставы требуют загрузки сцены).
        /// </summary>
        public static string DescribeAvailability()
        {
            var sb = new System.Text.StringBuilder();
            int ok = 0, noPrefab = 0;
            foreach (KvRobotModel m in KvRobotCatalog.All)
            {
                GameObject prefab = Resources.Load<GameObject>(m.PrefabPath);
                if (prefab == null)
                {
                    noPrefab++;
                    sb.Append(m.Id).Append(": НЕТ ПРЕФАБА; ");
                }
                else ok++;
            }
            return "моделей " + KvRobotCatalog.All.Length + " · префаб есть у " + ok +
                   (noPrefab > 0 ? " · без префаба " + noPrefab + " (" + sb + ")" : "");
        }
    }
}
