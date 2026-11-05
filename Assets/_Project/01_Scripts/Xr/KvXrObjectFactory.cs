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

        /// <summary>
        /// Вылет руки робота, метры (по пивотам до TCP — та же формула, что у зон).
        ///
        /// ВАЖНО ПРО ВЫКЛЮЧЕННЫХ РОБОТОВ. В шлеме при запуске стенды погашены, и `Awake` у их роботов
        /// мог не выполниться — тогда `jointTransforms` пусты, и вылет «получался» 0,05 м. Поэтому
        /// при пустых полях суставы добираются тем же `PoseValidator`, что и зона достижимости
        /// (одна логика поиска по именам Axis1…Axis6): вылет не зависит от того, включён робот или нет.
        /// </summary>
        public static float ReachOf(RobotController robot)
        {
            if (robot == null) return KvReachMath.MinChainMeters;

            var six = robot as SixAxisController;
            if (six != null)
            {
                if (six.jointTransforms == null || six.jointTransforms.Length == 0)
                {
                    // Тот же поиск суставов/TCP, что у зон: второго способа в проекте не заводим.
                    var validator = new PoseValidator();
                    validator.Init(robot);
                }

                if (six.jointTransforms != null)
                {
                    var pivots = new System.Collections.Generic.List<Vector3>();
                    for (int i = 0; i < six.jointTransforms.Length; i++)
                        if (six.jointTransforms[i] != null) pivots.Add(six.jointTransforms[i].position);
                    Vector3 tcp = six.tcp != null ? six.tcp.position
                                : (six.endEffector != null ? six.endEffector.position : robot.transform.position);
                    return KvReachMath.ChainLength(pivots.ToArray(), tcp);
                }
            }

            var scara = robot as SCARAController;
            if (scara != null)
            {
                if (scara.joint1 == null || scara.joint2 == null || scara.joint3 == null)
                {
                    var validator = new PoseValidator();
                    validator.Init(robot);
                }

                if (scara.joint1 != null && scara.joint2 != null && scara.joint3 != null)
                {
                    var pivots = new[] { scara.joint1.position, scara.joint2.position, scara.joint3.position };
                    return KvReachMath.ChainLength(pivots, scara.joint3.position);
                }
            }

            return KvReachMath.MinChainMeters;
        }

        /// <summary>Первый активный робот сцены (для имён в экранах и команд).</summary>
        public static RobotController ActiveRobot()
        {
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
            for (int i = 0; i < robots.Length; i++)
                if (robots[i] != null && robots[i].isActiveAndEnabled) return robots[i];
            return robots.Length > 0 ? robots[0] : null;
        }

        /// <summary>
        /// РОБОТ, ПО КОТОРОМУ СТРОИТСЯ АВТОСТОЛ (пункт 8 владельца, 02.11.2026).
        /// Порядок выбора:
        ///   1) робот, явно выбранный оператором в меню (если передан);
        ///   2) активный робот сцены;
        ///   3) ЛЮБОЙ робот сцены, включая выключенного: в шлеме при запуске пусто, сценные
        ///      роботы погашены, но их вылет известен — берём САМЫЙ БОЛЬШОЙ.
        /// Именно третий пункт не даёт автостолу стать размером с основание робота: раньше при пустой
        /// сцене `ActiveRobot()` возвращал null, вылет падал до 0,05 м, и стол получался 0,15 м.
        /// </summary>
        public static RobotController RobotForAutoTable(Transform selected)
        {
            if (selected != null)
            {
                RobotController picked = selected.GetComponentInParent<RobotController>();
                if (picked != null) return picked;
            }

            RobotController active = ActiveRobot();
            if (active != null) return active;

            RobotController[] all = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include);
            float[] reaches = new float[all.Length];
            for (int i = 0; i < all.Length; i++)
                reaches[i] = all[i] != null ? ReachOf(all[i]) : 0f;

            // Политика «берём самый большой вылет» живёт в KvReachMath и покрыта тестом.
            float bestReach = KvReachMath.MaxReach(reaches);
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && Mathf.Abs(reaches[i] - bestReach) < 0.0001f) return all[i];
            return null;
        }

        /// <summary>Вылет руки, от которого строится автостол (по правилу `RobotForAutoTable`).</summary>
        public static float ReachForAutoTable(Transform selected)
        {
            return ReachOf(RobotForAutoTable(selected));
        }

        // ==================================================== ХУКИ AR (по умолчанию выключены)
        //
        // ЗАЧЕМ. В AR «пол» — это найденная камерой плоскость, а не мировой y = 0: на планшете
        // y = 0 находится там, где устройство запустили (обычно на высоте груди). Меню шлема
        // создаёт столы и роботов в мировых координатах, поэтому в AR ему нужно подставить
        // точку на найденной плоскости и высоту якоря.
        //
        // ПОЧЕМУ ХУКИ, А НЕ ПРАВКА ВЫЗЫВАЮЩЕГО КОДА. Вызывающий код — меню шлема
        // (`KvXrGroupPanel`), трогать его нельзя: VR обязан остаться прежним. Здесь три
        // статических поля, по умолчанию `null`: в шлеме и на компьютере ветки не исполняются
        // ни разу, поведение совпадает с прежним. Хуки снимаются при выходе из AR-сцены
        // (`KvArRuntime.ClearObjectFactoryHooks`).
        //
        // Что подменяет каждый хук:
        //   SpotOverride        — «точка перед оператором на полу» → точка на найденной плоскости;
        //   TableFloorFixup     — высота стола (низ) → высота плоскости якоря;
        //   RobotPositionFixup  — основание робота на высоте стола (0,98 м) → + высота якоря.

        /// <summary>AR: замена точки «перед оператором». null — обычное поведение VR/ПК.</summary>
        public static System.Func<Camera, float, Vector3?> SpotOverride;

        /// <summary>AR: поправка высоты пола создаваемого стола. null — без поправки.</summary>
        public static System.Func<Vector3, Vector3> TableFloorFixup;

        /// <summary>AR: поправка позиции создаваемого робота. null — без поправки.</summary>
        public static System.Func<Vector3, Vector3> RobotPositionFixup;

        /// <summary>
        /// AR: МИРОВЫЕ ВЫСОТЫ — АБСОЛЮТНЫЕ. На планшете «пол» — найденная плоскость на реальной
        /// высоте, а не мировой y=0: меню не должно переписывать высоту стола/робота как  ,98,
        /// иначе робот оказывается на 0,98 м выше стола (двойная высота, аудит 05.11.2026).
        /// В VR/ПК флаг выключен — поведение прежнее.
        /// </summary>
        public static bool AbsoluteWorldHeights;

        /// <summary>ТОЧКА ПЕРЕД ОПЕРАТОРОМ на полу (для создаваемого стола).</summary>
        public static Vector3 SpotInFrontOf(Camera camera, float distanceMeters)
        {
            // AR-хук: если он задан и вернул точку — используем её (VR/ПК этой ветки не видят).
            if (SpotOverride != null)
            {
                Vector3? arPoint = SpotOverride(camera, distanceMeters);
                if (arPoint.HasValue) return arPoint.Value;
            }

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
            // AR-хук: высота пола — найденная плоскость, а не мировой y = 0 (VR/ПК не видят).
            if (TableFloorFixup != null) floorPoint = TableFloorFixup(floorPoint);

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

            // AR-хук: основание робота считается от найденной плоскости, а не от мирового нуля.
            if (RobotPositionFixup != null) position = RobotPositionFixup(position);

            RegisteredObject copy = spawner.SpawnRobot(position, yawDegrees, template);
            if (copy == null) return null;

            copy.gameObject.SetActive(true);        // эталон мог быть выключен (пустая сцена при старте)
            RobotController robot = copy.GetComponent<RobotController>();
            if (robot != null) robot.SetActive(true);
            return robot;
        }
    }
}
