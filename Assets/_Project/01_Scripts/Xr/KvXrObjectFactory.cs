using UnityEngine;
using TrajectoryCore;
using KazistovVvUI;

namespace KazistovVvXr
{
    /// <summary>
    /// СОЗДАНИЕ СТОЛОВ И РОБОТОВ В ШЛЕМЕ (§42, пункты 2 и 3 задания).
    ///
    /// ЧТО БЫЛО. При запуске в шлеме стояли два стола по 14,4 × 9,6 м и два робота, а добавить новые
    /// было нечем: строка меню «Запустить стенд» вызывала команду `stand.run`, которой НЕТ в реестре
    /// (проверено 25.10.2026) — то есть гарантированное «команда не найдена».
    ///
    /// ПРАВИЛО «ОДИН МЕХАНИЗМ». Столы строит ТОТ ЖЕ `StandBuilder`, что и в ПК-версии (добавлены два
    /// публичных построителя с размерами), роботов клонирует ТОТ ЖЕ `ObjectSpawner.SpawnRobot`, что и
    /// на компьютере. Второй фабрики объектов не заводится.
    ///
    /// ГДЕ СТАВИТСЯ. Центр стола — 1,20 м прямо перед оператором на полу, разворот на оператора;
    /// робот — на столешнице этого стола (верх 0,98 м). Числа объявлены заранее (вопрос В3 плана).
    /// </summary>
    public static class KvXrObjectFactory
    {
        /// <summary>Расстояние от оператора до центра создаваемого стола, метры.</summary>
        public const float SpawnDistanceMeters = 1.20f;

        /// <summary>Вылет руки робота, метры (по пивотам до TCP — та же формула, что у зон).</summary>
        public static float ReachOf(RobotController robot)
        {
            if (robot == null) return KvReachMath.MinChainMeters;

            var six = robot as SixAxisController;
            if (six != null && six.jointTransforms != null)
            {
                var pivots = new System.Collections.Generic.List<Vector3>();
                for (int i = 0; i < six.jointTransforms.Length; i++)
                    if (six.jointTransforms[i] != null) pivots.Add(six.jointTransforms[i].position);
                Vector3 tcp = six.tcp != null ? six.tcp.position
                            : (six.endEffector != null ? six.endEffector.position : robot.transform.position);
                return KvReachMath.ChainLength(pivots.ToArray(), tcp);
            }

            var scara = robot as SCARAController;
            if (scara != null && scara.joint1 != null && scara.joint2 != null && scara.joint3 != null)
            {
                var pivots = new[] { scara.joint1.position, scara.joint2.position, scara.joint3.position };
                return KvReachMath.ChainLength(pivots, scara.joint3.position);
            }

            return KvReachMath.MinChainMeters;
        }

        /// <summary>Первый активный робот сцены (тот, по вылету которого считается автостол).</summary>
        public static RobotController ActiveRobot()
        {
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < robots.Length; i++)
                if (robots[i] != null && robots[i].isActiveAndEnabled) return robots[i];
            return robots.Length > 0 ? robots[0] : null;
        }

        /// <summary>ТОЧКА ПЕРЕД ОПЕРАТОРОМ на полу (для создаваемого стола).</summary>
        public static Vector3 SpotInFrontOf(Camera camera, float distanceMeters)
        {
            Transform source = camera != null ? camera.transform : null;
            Vector3 position = source != null ? source.position : Vector3.zero;
            Vector3 forward = source != null ? source.forward : Vector3.forward;

            Vector3 flat = Vector3.ProjectOnPlane(forward, Vector3.up);
            if (flat.sqrMagnitude < 0.0001f) flat = Vector3.forward;
            flat.Normalize();

            float distance = distanceMeters > 0f ? distanceMeters : SpawnDistanceMeters;
            return new Vector3(position.x + flat.x * distance, 0f, position.z + flat.z * distance);
        }

        /// <summary>Разворот стола НА оператора (чтобы он видел переднюю сторону, а не спину).</summary>
        public static float YawTowards(Vector3 from, Vector3 target)
        {
            Vector3 direction = target - from;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            return Quaternion.LookRotation(direction, Vector3.up).eulerAngles.y;
        }

        /// <summary>СОЗДАТЬ СТОЛ нужного вида (числа — из `KvXrTableKinds`).</summary>
        public static GameObject CreateTable(KvXrTableKind kind, Vector3 floorPoint, float yawDegrees,
                                             float robotReachMeters)
        {
            string name = kind == KvXrTableKind.Square ? "Стол_квадрат" : "Стол_авто";
            name = name + "_" + System.DateTime.Now.ToString("HHmmss");

            GameObject table;
            if (kind == KvXrTableKind.Square)
            {
                table = StandBuilder.BuildTable(name, floorPoint, KvXrTableKinds.TopHeight,
                    KvXrTableKinds.SquareWidth, KvXrTableKinds.SquareDepth);
            }
            else
            {
                table = StandBuilder.BuildRoundTable(name, floorPoint, KvXrTableKinds.TopHeight,
                    KvReachMath.AutoRadius(robotReachMeters));
            }

            if (table != null) table.transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f);
            return table;
        }

        /// <summary>СОЗДАТЬ РОБОТА копией сценного эталона (тем же путём, что на компьютере).</summary>
        public static RobotController CreateRobot(bool scara, Vector3 position, float yawDegrees)
        {
            ObjectSpawner spawner = null;
            if (KazistovVvUIManager.Instance != null) spawner = KazistovVvUIManager.Instance.Objects;
            if (spawner == null)
            {
                Debug.LogWarning("[KvXr] МЕНЮ: спавнер объектов недоступен — робот не создан");
                return null;
            }

            RobotController[] templates = spawner.RefreshRobotTemplates();
            RobotController template = null;
            for (int i = 0; templates != null && i < templates.Length; i++)
            {
                if (templates[i] == null) continue;
                bool isScara = templates[i] is SCARAController;
                if (isScara == scara) { template = templates[i]; break; }
            }
            if (template == null)
            {
                Debug.LogWarning("[KvXr] МЕНЮ: нет шаблона робота (" + (scara ? "SCARA" : "6-осевой") +
                                 ") — копировать нечего");
                return null;
            }

            RegisteredObject copy = spawner.SpawnRobot(position, yawDegrees, template);
            if (copy == null) return null;

            copy.gameObject.SetActive(true);        // эталон мог быть выключен (пустая сцена при старте)
            RobotController robot = copy.GetComponent<RobotController>();
            if (robot != null) robot.SetActive(true);
            return robot;
        }
    }
}
