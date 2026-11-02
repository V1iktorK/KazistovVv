using System;
using System.Collections.Generic;
using KazistovVvUI;   // RegisteredObject
using UnityEngine;

namespace KazistovVv.Core
{
    /// <summary>
    /// ДВУРУКИЙ РОБОТ КАК ПАРА РОБОТОВ (решение владельца 02.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА: «меняем его структуру через блендер. Кранштейн и "руки" сохраняем
    /// отдельно. а при добавлении робота на сцену формально получится что добавлены стали два
    /// робота и управление будет как-будто двумя, а не одним».
    ///
    /// ПОЧЕМУ ЭТО ПРАВИЛЬНО. Вся система проекта — IK, фантомы, траектории, коллизии, экспорт —
    /// рассчитана на ОДНУ кинематическую цепочку. Двурукий «одним роботом» потребовал бы
    /// переписать всё это. Два робота на общем основании работают БЕЗ переделки: каждый со своей
    /// цепочкой, своим фантомом и своей траекторией.
    ///
    /// ЧТО ДЕЛАЕТ ЭТОТ КЛАСС. Ему НЕ НУЖНА переделанная в Blender модель: он берёт один префаб
    /// `openarm`, создаёт ДВЕ копии и в каждой оставляет только свою руку (вторую скрывает,
    /// а не удаляет — чтобы не ломать иерархию трансформов, от которой зависят оси суставов).
    ///
    /// Когда владелец принесёт разделённые префабы (кронштейн + две руки), здесь достаточно
    /// заменить источник: `KvDualArm.SpawnFromSeparatePrefabs`. Механика пары роботов останется
    /// той же — её и проверяет `KvDualArmTests`.
    /// </summary>
    public static class KvDualArm
    {
        /// <summary>Префикс имени левой руки (звенья `openarm_left_link0…6`).</summary>
        public const string LeftPrefix = "openarm_left_";

        /// <summary>Префикс имени правой руки.</summary>
        public const string RightPrefix = "openarm_right_";

        /// <summary>Корень пары: кронштейн, к которому крепятся обе руки.</summary>
        public const string BracketName = "Двурукий_кронштейн";

        /// <summary>
        /// СОЗДАТЬ ПАРУ РУК НА ОБЩЕМ КРОНШТЕЙНЕ.
        ///
        /// Возвращает массив из двух `RobotController` (левая, правая) или пустой массив, если
        /// префаб не найден. Пустоту не маскируем: оператор должен увидеть в журнале причину,
        /// а не пустой стол без объяснений.
        /// </summary>
        public static RobotController[] SpawnFromOnePrefab(KvRobotModel model, Vector3 position,
                                                           float yawDegrees)
        {
            var result = new List<RobotController>();
            if (model == null)
            {
                Debug.LogWarning("[KvDualArm] модель не задана — пара не создана");
                return result.ToArray();
            }

            GameObject prefab = Resources.Load<GameObject>(model.PrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[KvDualArm] не найден префаб «" + model.PrefabPath +
                                 "» — пара не создана");
                return result.ToArray();
            }

            // --- кронштейн: общий родитель обеих рук
            var bracket = new GameObject(BracketName);
            bracket.transform.position = position;
            bracket.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);

            RobotController left = SpawnArm(prefab, bracket.transform, "Левая рука",
                LeftPrefix, model.Title + " (левая)", true);
            RobotController right = SpawnArm(prefab, bracket.transform, "Правая рука",
                RightPrefix, model.Title + " (правая)", false);

            if (left != null) result.Add(left);
            if (right != null) result.Add(right);

            if (result.Count == 2)
            {
                Debug.Log("[KvDualArm] пара создана: левая «" + left.robotName + "» и правая «" +
                          right.robotName + "» на кронштейне «" + BracketName + "»");
            }
            else
            {
                Debug.LogWarning("[KvDualArm] пара НЕПОЛНАЯ: создано " + result.Count +
                                 " из 2 рук — проверьте имена звеньев в префабе");
            }
            return result.ToArray();
        }

        /// <summary>
        /// СОЗДАТЬ ОДНУ РУКУ: копия префаба, у которой ВИДИМА только своя цепочка.
        ///
        /// ПОЧЕМУ СКРЫВАЕМ, А НЕ УДАЛЯЕМ. Оси вращения суставов берутся из локальных
        /// трансформов (`localRotation`). Если удалить чужие звенья, иерархия изменится и оси
        /// могут поехать. Скрытие (`SetActive(false)`) сохраняет структуру нетронутой.
        /// </summary>
        private static RobotController SpawnArm(GameObject prefab, Transform bracket,
                                                string armName, string ownPrefix, string title,
                                                bool isLeft)
        {
            GameObject go = UnityEngine.Object.Instantiate(prefab, bracket, false);
            go.name = armName;
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;

            // Скрываем ЧУЖУЮ цепочку: левой руке не нужна правая, и наоборот.
            string foreignPrefix = isLeft ? RightPrefix : LeftPrefix;
            int hidden = 0;
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true))
            {
                if (t == null || t == go.transform) continue;
                if (!t.name.StartsWith(foreignPrefix, StringComparison.Ordinal)) continue;
                t.gameObject.SetActive(false);
                hidden++;
            }

            // --- шесть суставов СВОЕЙ руки (у модели по семь звеньев: 0…6, где 6 — фланец)
            var joints = new List<Transform>();
            for (int i = 0; i < 7; i++)
            {
                Transform found = FindDeep(go.transform, ownPrefix + "link" + i);
                if (found != null) joints.Add(found);
            }

            if (joints.Count < 3)
            {
                Debug.LogWarning("[KvDualArm] «" + armName + "»: найдено " + joints.Count +
                                 " звеньев из 7 — рука не будет двигаться");
                return null;
            }

            SixAxisController arm = go.GetComponent<SixAxisController>();
            if (arm == null) arm = go.AddComponent<SixAxisController>();

            arm.robotName = title;
            arm.jointTransforms = joints.ToArray();

            // TCP — кончик руки (кисть), а не последний сустав: у модели есть *_hand_tcp.
            Transform tool = FindDeep(go.transform, ownPrefix + "hand_tcp");
            if (tool != null) arm.endEffector = tool;
            else arm.endEffector = joints[joints.Count - 1];

            if (go.GetComponent<RegisteredObject>() == null)
                go.AddComponent<RegisteredObject>();

            Debug.Log("[KvDualArm] рука «" + armName + "»: суставов " + joints.Count +
                      " · скрыто чужих объектов " + hidden +
                      " · TCP «" + (tool != null ? tool.name : "последний сустав") + "»");
            return arm;
        }

        /// <summary>Поиск объекта по ТОЧНОМУ имени среди потомков (LOD-копии не совпадут).</summary>
        private static Transform FindDeep(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != null && string.Equals(t.name, name, StringComparison.Ordinal)) return t;
            return null;
        }

        /// <summary>
        /// ПАРА ГОТОВА К РАБОТЕ: обе руки нашлись и у каждой есть суставы.
        /// Нужна тестам и журналу — «создано два объекта» ещё не значит «две руки работают».
        /// </summary>
        public static bool IsPairReady(RobotController[] pair)
        {
            if (pair == null || pair.Length != 2) return false;
            for (int i = 0; i < pair.Length; i++)
            {
                if (pair[i] == null) return false;
                SixAxisController arm = pair[i] as SixAxisController;
                if (arm == null || arm.jointTransforms == null || arm.jointTransforms.Length < 3)
                    return false;
            }
            return true;
        }
    }
}
