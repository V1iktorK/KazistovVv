using System;
using System.Collections.Generic;
using KazistovVvKinematics;   // DHInverse (осевой CCD)
using UnityEngine;

namespace KazistovVv.Core
{
    /// <summary>
    /// ДВУРУКИЙ РОБОТ: ДВЕ НЕЗАВИСИМЫЕ ЦЕПОЧКИ (часть 4 задачи владельца 02.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА: «запрограммируй правильно наконец уже двурукого!»
    ///
    /// ПОЧЕМУ НУЖЕН ОТДЕЛЬНЫЙ КЛАСС. Вся система рассчитана на ОДИН манипулятор: у
    /// `RobotController` один `endEffector` и один `tcp`, у `SixAxisController` один массив
    /// `jointTransforms`. Двух рук в этой модели не бывает — вторая рука осталась бы мёртвой
    /// геометрией, что владелец и видел.
    ///
    /// ЧТО ЗДЕСЬ. Две цепочки — левая и правая — с общим основанием:
    ///   • у каждой свой массив суставов и свой инструмент (TCP);
    ///   • у каждой свой обратный решатель (осевой CCD — тот же, что у шестиосевого робота);
    ///   • цели задаются отдельно на каждую руку;
    ///   • есть режим СИНХРОННОГО движения, когда обе руки повторяют движение зеркально.
    ///
    /// ЧЕМ НЕ ЯВЛЯЕТСЯ. Это не «два робота рядом»: общее основание одно, и руки не должны
    /// сталкиваться друг с другом — проверка зазора между руками есть отдельным методом.
    ///
    /// МОДЕЛЬ. `openarm.prefab` — цепочки `openarm_left_link0…7` и `openarm_right_link0…7`
    /// (по 7 вращательных суставов в каждой).
    /// </summary>
    public class KvDualArmController : RobotController
    {
        [Header("Двурукий: левая рука")]
        public Transform[] leftJoints = new Transform[0];
        public Transform leftTool;

        [Header("Двурукий: правая рука")]
        public Transform[] rightJoints = new Transform[0];
        public Transform rightTool;

        [Header("Имена звеньев (если массивы не заданы вручную)")]
        public string[] leftJointNames =
        {
            "openarm_left_link0", "openarm_left_link1", "openarm_left_link2", "openarm_left_link3",
            "openarm_left_link4", "openarm_left_link5", "openarm_left_link6"
        };
        public string[] rightJointNames =
        {
            "openarm_right_link0", "openarm_right_link1", "openarm_right_link2", "openarm_right_link3",
            "openarm_right_link4", "openarm_right_link5", "openarm_right_link6"
        };
        public string leftToolName = "openarm_left_hand_tcp";
        public string rightToolName = "openarm_right_hand_tcp";

        [Header("Синхронное движение")]
        [Tooltip("Обе руки повторяют движение зеркально относительно оси симметрии основания.")]
        public bool mirrorMotion = false;

        /// <summary>Цель левой руки (мир).</summary>
        public Vector3 LeftTarget { get; private set; }
        /// <summary>Цель правой руки (мир).</summary>
        public Vector3 RightTarget { get; private set; }

        /// <summary>Достигла ли левая рука своей цели.</summary>
        public bool LeftReached { get; private set; }
        /// <summary>Достигла ли правая рука своей цели.</summary>
        public bool RightReached { get; private set; }

        private bool hasLeftTarget, hasRightTarget;
        private Vector3[] leftAxes = new Vector3[0];
        private Vector3[] rightAxes = new Vector3[0];
        private bool axesReady;

        /// <summary>Найдены ли обе цепочки — без этого двурукость не работает.</summary>
        public bool ChainsReady
        {
            get { return leftJoints != null && leftJoints.Length > 0 &&
                         rightJoints != null && rightJoints.Length > 0; }
        }

        protected override void Awake()
        {
            base.Awake();
            ResolveChains();
        }

        /// <summary>
        /// НАЙТИ ЦЕПОЧКИ ПО ИМЕНАМ, если они не заданы в инспекторе.
        ///
        /// Ищем в потомках ЭТОГО объекта: обе руки — дети общего основания, поэтому имя
        /// уникально внутри модели. Копии `*_LOD*` не трогаем — они не в кинематике.
        /// </summary>
        public void ResolveChains()
        {
            if (leftJoints == null || leftJoints.Length == 0)
                leftJoints = FindChain(leftJointNames);
            if (rightJoints == null || rightJoints.Length == 0)
                rightJoints = FindChain(rightJointNames);

            if (leftTool == null && !string.IsNullOrEmpty(leftToolName))
                leftTool = FindByName(leftToolName);
            if (rightTool == null && !string.IsNullOrEmpty(rightToolName))
                rightTool = FindByName(rightToolName);

            // Если TCP не нашёлся — берём кончик цепочки: он и есть фланец.
            if (leftTool == null && leftJoints != null && leftJoints.Length > 0)
                leftTool = leftJoints[leftJoints.Length - 1];
            if (rightTool == null && rightJoints != null && rightJoints.Length > 0)
                rightTool = rightJoints[rightJoints.Length - 1];

            CacheAxes();

            if (!ChainsReady)
            {
                Debug.LogWarning("[KvDualArm] цепочки не найдены: левая " +
                                 (leftJoints != null ? leftJoints.Length : 0) + ", правая " +
                                 (rightJoints != null ? rightJoints.Length : 0) +
                                 " — двурукий робот не будет двигаться");
            }
            else
            {
                Debug.Log("[KvDualArm] цепочки: левая " + leftJoints.Length +
                          " суставов, правая " + rightJoints.Length +
                          " · TCP: «" + (leftTool != null ? leftTool.name : "нет") + "» / «" +
                          (rightTool != null ? rightTool.name : "нет") + "»");
            }
        }

        private Transform[] FindChain(string[] names)
        {
            if (names == null) return new Transform[0];
            var list = new List<Transform>();
            for (int i = 0; i < names.Length; i++)
            {
                Transform t = FindByName(names[i]);
                if (t != null) list.Add(t);
                else Debug.LogWarning("[KvDualArm] звено «" + names[i] + "» не найдено");
            }
            return list.ToArray();
        }

        private Transform FindByName(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (Transform t in GetComponentsInChildren<Transform>(true))
                if (t != null && string.Equals(t.name, name, StringComparison.Ordinal)) return t;
            return null;
        }

        /// <summary>
        /// ОСИ ВРАЩЕНИЯ В ЛОКАЛЬНЫХ КООРДИНАТАХ. Берутся из трансформов один раз при старте:
        /// за кадр оси не меняются, а пересчёт каждый кадр — трата кадрового бюджета
        /// (правило §44: дорогие вычисления не в кадре).
        /// </summary>
        private void CacheAxes()
        {
            leftAxes = AxesOf(leftJoints);
            rightAxes = AxesOf(rightJoints);
            axesReady = true;
        }

        private static Vector3[] AxesOf(Transform[] joints)
        {
            if (joints == null) return new Vector3[0];
            var axes = new Vector3[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                // Ось вращения по умолчанию — Y (так в URDF для вращательных суставов).
                // Если в модели ось другая, CCD подстроится: он вращает сустав ВОКРУГ
                // локальной оси и смотрит на результат, поэтому ошибка оси даёт лишь
                // меньшую скорость сходимости, а не поломку.
                axes[i] = joints[i] != null ? joints[i].localRotation * Vector3.up : Vector3.up;
            }
            return axes;
        }

        // ================================================================ ЦЕЛИ

        /// <summary>Задать цели обеим рукам.</summary>
        public void SetTargetBoth(Vector3 left, Vector3 right)
        {
            LeftTarget = left;
            RightTarget = right;
            hasLeftTarget = true;
            hasRightTarget = true;

            if (mirrorMotion)
            {
                // Зеркальное движение: правая рука повторяет левую относительно оси симметрии.
                // Нужно для «обе руки делают одно и то же» — владелец просил синхронность.
                Vector3 pivot = transform.position;
                RightTarget = new Vector3(2f * pivot.x - left.x, left.y, left.z);
            }
        }

        /// <summary>Задать цель одной руке (левой или правой).</summary>
        public void SetTargetOne(bool left, Vector3 target)
        {
            if (left)
            {
                LeftTarget = target;
                hasLeftTarget = true;
            }
            else
            {
                RightTarget = target;
                hasRightTarget = true;
            }
        }

        public override void SetTarget(Vector3 position)
        {
            // Базовая система знает ОДНУ цель. Для двурукого это означает «обе руки в эту точку»
            // — так ведут себя вызовы, которые ещё не знают про двурукость.
            SetTargetBoth(position, position);
        }

        public override void ClearTarget()
        {
            hasLeftTarget = false;
            hasRightTarget = false;
            LeftReached = false;
            RightReached = false;
        }

        // ================================================================ ДВИЖЕНИЕ

        public override void MoveToTarget(float deltaTime)
        {
            if (!ChainsReady) return;
            if (!axesReady) CacheAxes();

            int iterations = Mathf.Clamp(ikIterations, 1, 40);

            if (hasLeftTarget)
            {
                LeftReached = DHInverse.SolveCCD(leftJoints, leftAxes, leftTool,
                    LeftTarget, iterations, ikTolerance);
            }

            if (hasRightTarget)
            {
                RightReached = DHInverse.SolveCCD(rightJoints, rightAxes, rightTool,
                    RightTarget, iterations, ikTolerance);
            }
        }

        /// <summary>
        /// УГЛЫ ВСЕХ ОСЕЙ: первые — левая рука, дальше правая.
        ///
        /// Порядок важен для телеметрии и экспорта: 14 значений в предсказуемом порядке.
        /// </summary>
        public override float[] GetJointAngles()
        {
            var list = new List<float>();
            for (int i = 0; i < leftJoints.Length; i++)
                if (leftJoints[i] != null) list.Add(leftJoints[i].localEulerAngles.y);
            for (int i = 0; i < rightJoints.Length; i++)
                if (rightJoints[i] != null) list.Add(rightJoints[i].localEulerAngles.y);
            return list.ToArray();
        }

        public override void SetActive(bool active)
        {
            base.SetActive(active);
            isActive = active;
        }

        // ================================================================ ПРОВЕРКИ

        /// <summary>
        /// РАССТОЯНИЕ МЕЖДУ ИНСТРУМЕНТАМИ РУК, метры.
        ///
        /// Нужно для проверки «руки не сошлись в одну точку»: у двурукого это реальный дефект —
        /// две руки в одной точке выглядят как одна, и оператор не понимает, что произошло.
        /// </summary>
        public float ToolSeparation
        {
            get
            {
                if (leftTool == null || rightTool == null) return 0f;
                return Vector3.Distance(leftTool.position, rightTool.position);
            }
        }

        /// <summary>Строка для журнала: состояние двурукого.</summary>
        public string DescribeDual()
        {
            return "двурукий «" + robotName + "» · левая " + leftJoints.Length +
                   " осей" + (LeftReached ? " (дошла)" : "") +
                   " · правая " + rightJoints.Length + " осей" + (RightReached ? " (дошла)" : "") +
                   " · между инструментами " +
                   ToolSeparation.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " м";
        }
    }
}
