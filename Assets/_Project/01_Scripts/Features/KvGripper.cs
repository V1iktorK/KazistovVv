using System;
using System.Collections.Generic;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ПРОСТОЙ ГРИППЕР (ЭТАП 10 ТЗ).
    ///
    /// Двухпалый захват на конце робота: два «пальца», которые раздвигаются/сдвигаются
    /// по кнопке. Собирается КОДОМ (никаких префабов и ассетов) и вешается на `tcp` робота —
    /// поэтому работает и у робота, и у SCARA (см. отчёт: у SCARA инструмент вертикальный,
    /// пальцы идут вдоль её оси Z и захват работает так же; ориентация пальцев настраивается
    /// полями `openAxisLocal`/`extendAxisLocal`).
    ///
    /// Коллайдеры пальцев УДАЛЯЮТСЯ: `CollisionWorld.Rebuild` собирает препятствия по
    /// рендерерам сцены, и живой коллайдер/рендерер захвата молча изменил бы планирование
    /// всего проекта (то же решение, что у зон запрета).
    /// </summary>
    public class KvGripper
    {
        /// <summary>Раскрытие пальцев, м (расстояние между внутренними плоскостями).</summary>
        public float openWidth = 0.075f;
        /// <summary>Раскрытие в закрытом состоянии, м.</summary>
        public float closedWidth = 0.012f;
        /// <summary>Длина пальца, м.</summary>
        public float fingerLength = 0.055f;
        /// <summary>Толщина/ширина пальца, м.</summary>
        public float fingerThickness = 0.010f;
        /// <summary>Смещение основания пальцев вдоль инструмента, м.</summary>
        public float mountOffset = 0.010f;
        /// <summary>Скорость смыкания/размыкания, 1/с (доля хода в секунду).</summary>
        public float speed = 3.5f;

        /// <summary>Ось раскрытия пальцев в локальных координатах TCP.</summary>
        public Vector3 openAxisLocal = Vector3.right;
        /// <summary>
        /// Ось «вдоль инструмента» в локальных координатах TCP. По соглашению проекта
        /// (§5.8: нормаль концевой плоскости — локальная Y фланца, а инструмент вытянут
        /// вдоль локальной −Y) пальцы идут ВНИЗ по локальной −Y, поэтому значение по умолчанию
        /// `Vector3.down`. Если в вашей сборке инструмент смотрит иначе — поменяйте здесь.
        /// </summary>
        public Vector3 extendAxisLocal = Vector3.down;

        /// <summary>Раскрытие 0 (закрыт) … 1 (открыт).</summary>
        public float Open01 { get; private set; }
        public bool IsOpen { get { return Open01 > 0.5f; } }
        public bool Attached { get { return root != null; } }
        /// <summary>
        /// Робот, НА КОТОРОМ собран захват (ФИКС 3). Раньше это число нигде не хранилось:
        /// поток мог перепривязаться к другому роботу, а захват оставался на прежнем — и
        /// «пальцы не двигаются», потому что двигаются они на другом стенде.
        /// </summary>
        public RobotController Robot { get; private set; }
        /// <summary>Захват собран ровно на этом роботе (ФИКС 3).</summary>
        public bool AttachedTo(RobotController robot)
        {
            return root != null && robot != null && Robot == robot;
        }
        /// <summary>Захваченный объект (null — пусто).</summary>
        public Transform Held { get; private set; }

        public event Action<string> Message;

        private Transform root;
        private Transform fingerA;
        private Transform fingerB;
        private Transform holder;          // «ладонь» — к ней крепится захваченный объект
        private Material material;
        private float target01;

        /// <summary>
        /// Собрать/пересобрать захват на роботе (вызывается при инициализации и смене робота).
        ///
        /// ФИКС 3. Робот берётся У ПОТОКА (`flow.Robot` → сюда), сам захват робота не ищет:
        /// ни `FindFirstObjectByType`, ни обхода сцены здесь нет и не было.
        /// Две защиты от «пальцы не двигаются»:
        ///   1) если захват УЖЕ собран на этом же роботе — пересборки нет (иначе каждое
        ///      обращение потока сбрасывало бы анимацию пальцев в открытое состояние);
        ///   2) при переезде на ДРУГОГО робота раскрытие СОХРАНЯЕТСЯ: оператор нажал «сжать» —
        ///      пальцы остаются сжатыми и на новом роботе, а не «сами разжимаются».
        /// </summary>
        public bool Attach(RobotController robot)
        {
            if (robot == null) return false;
            if (AttachedTo(robot)) return true;          // уже на этом роботе — ничего не трогаем

            float keepOpen01 = root != null ? Open01 : 1f;
            float keepTarget01 = root != null ? target01 : 1f;

            Detach();
            Transform tcp = robot.tcp;
            if (tcp == null) return false;

            GameObject go = new GameObject("Захват");
            go.transform.SetParent(tcp, false);
            root = go.transform;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;
            // Захват — ЧАСТЬ РОБОТА, поэтому в иерархии виден (оператору это полезно),
            // а в CollisionWorld он не попадёт: объекты под RobotController там пропускаются.
            go.hideFlags = HideFlags.None;

            // Материал — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP для шлема, HDRP для
            // компьютера): жёсткий `Shader.Find("HDRP/Lit")` под URP давал бы розовый захват.
            material = KvRenderPipelineShaders.CreateLit("Материал_захвата", new Color(0.35f, 0.36f, 0.40f));
            if (material != null && material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.75f);
            if (material != null && material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.45f);

            fingerA = MakeFinger("Палец_A", 1f);
            fingerB = MakeFinger("Палец_B", -1f);

            GameObject holderGo = new GameObject("ТочкаЗахвата");
            holderGo.transform.SetParent(root, false);
            holder = holderGo.transform;
            holder.localPosition = Extend * GraspDrop;

            Robot = robot;
            Open01 = Mathf.Clamp01(keepOpen01);          // состояние пальцев переносится
            target01 = Mathf.Clamp01(keepTarget01);
            Apply();
            Debug.Log("[Gripper] захват собран на роботе «" + robot.robotName + "» (TCP: " +
                      tcp.name + ") · раскрытие " +
                      (Width * 1000f).ToString("0") + " мм (из " +
                      (openWidth * 1000f).ToString("0") + " мм), состояние " +
                      (IsOpen ? "разжат" : "сжат") +
                      " · робот взят у потока (flow.Robot)");
            return true;
        }

        private Vector3 Open { get { return openAxisLocal.normalized; } }
        private Vector3 Extend { get { return extendAxisLocal.normalized; } }

        private Transform MakeFinger(string name, float side)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(root, false);
            Collider col = go.GetComponent<Collider>();
            if (col != null) DestroySafe(col);              // захват не должен менять мир столкновений
            Renderer r = go.GetComponent<Renderer>();
            if (r != null && material != null) r.sharedMaterial = material;
            go.transform.localScale = new Vector3(fingerThickness, fingerLength, fingerThickness * 1.8f);
            return go.transform;
        }

        /// <summary>Открыть/закрыть захват (плавно).</summary>
        public void SetOpen(bool open)
        {
            target01 = open ? 1f : 0f;
            if (Message != null) Message("захват: " + (open ? "размыкание" : "смыкание"));
        }

        public void Toggle()
        {
            SetOpen(!IsOpen);
        }

        /// <summary>Анимация пальцев (вызывается из кадрового обновления хаба).</summary>
        public void Tick(float deltaTime)
        {
            if (root == null) return;
            if (Mathf.Abs(Open01 - target01) < 0.001f) return;
            Open01 = Mathf.MoveTowards(Open01, target01, Mathf.Max(0.05f, speed) * deltaTime);
            Apply();
        }

        private void Apply()
        {
            if (fingerA == null || fingerB == null) return;
            float half = Mathf.Lerp(closedWidth, openWidth, Open01) * 0.5f;
            Vector3 basePos = Extend * mountOffset;
            // Кубы Unity имеют размер 1: масштаб уже задан, поэтому позиция = середина пальца.
            fingerA.localPosition = basePos + Open * half + Extend * (fingerLength * 0.5f);
            fingerB.localPosition = basePos - Open * half + Extend * (fingerLength * 0.5f);
            fingerA.localRotation = Quaternion.identity;
            fingerB.localRotation = Quaternion.identity;
        }

        /// <summary>Взять объект (родителем становится точка захвата, физика выключается).</summary>
        public bool Grab(Transform target)
        {
            if (root == null || target == null || holder == null) return false;
            if (Held != null) Release();

            Rigidbody body = target.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            target.SetParent(holder, true);
            Held = target;
            Debug.Log("[Gripper] объект «" + target.name + "» захвачен");
            if (Message != null) Message("объект «" + target.name + "» захвачен");
            return true;
        }

        /// <summary>Отпустить объект (снова свободен и с физикой).</summary>
        public bool Release()
        {
            if (Held == null) return false;
            Transform released = Held;
            Held = null;

            released.SetParent(null, true);
            Rigidbody body = released.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }
            Debug.Log("[Gripper] объект «" + released.name + "» отпущен");
            if (Message != null) Message("объект «" + released.name + "» отпущен");
            return true;
        }

        /// <summary>Расстояние между кончиками пальцев (для проверки «можно ли взять»).</summary>
        public float Width { get { return Mathf.Lerp(closedWidth, openWidth, Open01); } }

        /// <summary>Насколько точка захвата отстоит от TCP вдоль инструмента, м.</summary>
        public float GraspDrop { get { return mountOffset + fingerLength; } }

        /// <summary>Направление «вдоль инструмента» в МИРОВЫХ координатах.</summary>
        public Vector3 ExtendWorld
        {
            get { return root != null ? root.TransformDirection(Extend) : Vector3.down; }
        }

        /// <summary>Точка между пальцами в мире (куда «смотрит» захват).</summary>
        public Vector3 GraspPoint
        {
            get { return holder != null ? holder.position : (root != null ? root.position : Vector3.zero); }
        }

        public void Detach()
        {
            if (Held != null) Release();
            if (root != null) DestroySafe(root.gameObject);
            if (material != null) DestroySafe(material);
            root = null;
            fingerA = null;
            fingerB = null;
            holder = null;
            material = null;
            Robot = null;          // ФИКС 3: захват больше ни на ком не собран
        }

        private static void DestroySafe(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }
    }

    /// <summary>
    /// PICK-AND-PLACE ДЕМО (ЭТАП 11 ТЗ).
    ///
    /// На столешнице появляется куб; робот берёт его и перекладывает в другую точку.
    /// Последовательность: подъезд → опускание → захват → подъём → перенос → опускание →
    /// отпускание → отход. Каждый шаг — обычная траектория, построенная общими средствами
    /// (`KvPlanKit` + штатный исполнитель потока), поэтому SafetyGate, лимиты и зазоры
    /// проверяются ровно так же, как на этапе 4.
    ///
    /// Пока объект удерживается, он — ДОЧЕРНИЙ объект гриппера (ТЗ), отпущенный снова
    /// свободен и с физикой (Rigidbody снова не кинематический).
    /// </summary>
    public class KvPickAndPlace
    {
        public enum Step
        {
            Idle, Approach, Descend, Close, Lift, Transfer, Lower, Open, Retreat, Done, Failed
        }

        /// <summary>Размер куба, м.</summary>
        public float cubeSize = 0.07f;
        /// <summary>Высота подхода над кубом, м.</summary>
        public float approachHeight = 0.16f;
        /// <summary>Высота подъёма после захвата, м.</summary>
        public float liftHeight = 0.20f;
        /// <summary>Смещение точки переноса от исходной позиции куба, м.</summary>
        public Vector3 transferOffset = new Vector3(0.35f, 0f, 0.25f);
        [Tooltip("Смещение точки переноса для 3-осевого SCARA (вылет руки ≈0.70 м). " +
                 "Общий transferOffset уводит куб за пределы кольца и «перенос» не планируется.")]
        public Vector3 transferOffsetScara = new Vector3(0.15f, 0f, 0.10f);
        [Tooltip("Сколько раз подряд разрешено пытаться запустить шаг, прежде чем признать его неудачным. " +
                 "Без предела шаг повторялся КАЖДЫЙ кадр (полное перепланирование + отказ Safety) — " +
                 "это забивало консоль и грузило процессор.")]
        public int maxStepAttempts = 3;
        /// <summary>Действующее смещение переноса под текущего робота.</summary>
        private Vector3 TransferOffsetNow
        {
            get
            {
                return (flow != null && flow.Validator != null && flow.Validator.Dof <= 3)
                    ? transferOffsetScara : transferOffset;
            }
        }
        /// <summary>Высота стола, если не удалось определить (мир Y).</summary>
        public float tableHeight = TrajectoryCore.StandBuilder.TopHeight;

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private KvGripper gripper;

        private GameObject cube;
        private Rigidbody cubeBody;
        private Vector3 placePoint;

        public Step Current { get; private set; }
        public bool Running { get { return Current != Step.Idle && Current != Step.Done && Current != Step.Failed; } }
        public GameObject Cube { get { return cube; } }

        private float waitTimer;
        private bool cubeSpawned;
        private int stepAttempts;

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld, KvGripper grip)
        {
            flow = controller;
            world = collisionWorld;
            gripper = grip;
        }

        /// <summary>Положить куб на столешницу рядом с роботом.</summary>
        public bool SpawnCube(bool replace = true)
        {
            if (flow == null || flow.Robot == null) return false;
            if (cube != null && !replace) return true;
            if (cube != null) DestroyCube();

            Vector3 basePos = flow.Robot.transform.position;
            // Вынос точки захвата от базы. У 6-осевого робота вылет руки ≈1 м и смещение
            // 0.62 м — комфортное. У SCARA вылет всего ≈0.70 м (a1 0.325 + a2 0.375), и куб
            // на 0.62 м оказывался у самой границы кольца: подход отбраковывался по лимитам
            // и pick-and-place на SCARA не выполнялся вовсе. Поэтому для 3-осевого — ближе.
            float lateral = flow.Validator.Dof <= 3 ? 0.38f : 0.62f;
            float forward = flow.Validator.Dof <= 3 ? 0.20f : 0.30f;
            Vector3 spawn = new Vector3(basePos.x + lateral, tableHeight + cubeSize * 0.5f + 0.002f,
                                        basePos.z + forward);

            // Если в сцене есть стол под роботом — ставим куб на его верхнюю плоскость.
            float surface = FindSurfaceY(spawn);
            if (!float.IsNaN(surface)) spawn.y = surface + cubeSize * 0.5f + 0.002f;

            cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = TrajectoryCore.CollisionWorld.PickAndPlaceCubeName;
            cube.transform.position = spawn;
            cube.transform.localScale = Vector3.one * cubeSize;

            Renderer r = cube.GetComponent<Renderer>();
            if (r != null)
            {
                // Материал кубика — тоже под текущий конвейер (см. пояснение выше).
                Material m = KvRenderPipelineShaders.CreateLit("Материал_кубика", new Color(0.95f, 0.62f, 0.15f));
                if (m != null)
                {
                    if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.1f);
                    if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.35f);
                    r.sharedMaterial = m;
                }
            }

            cubeBody = cube.AddComponent<Rigidbody>();
            cubeBody.mass = 0.35f;
            cubeBody.useGravity = true;
            cubeBody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            placePoint = spawn + TransferOffsetNow;
            placePoint.y = spawn.y;
            cubeSpawned = true;

            Debug.Log("[PickPlace] куб создан в " + spawn + " · точка переноса " + placePoint);
            if (Message != null) Message("куб на столе: " + Vec(spawn) + " → перенос " + Vec(placePoint));
            return true;
        }

        /// <summary>Запустить демонстрацию (робот должен стоять, гриппер обязателен).</summary>
        public bool Run()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Fail("робот не готов");
                return false;
            }
            if (gripper == null || !gripper.Attached)
            {
                Fail("захват не собран");
                return false;
            }
            if (flow.Motion != null && flow.Motion.IsRunning)
            {
                Fail("робот уже едет — дождитесь остановки");
                return false;
            }
            if (!cubeSpawned || cube == null) SpawnCube();

            gripper.SetOpen(true);
            stepAttempts = 0;
            Current = Step.Approach;
            waitTimer = 0.35f;              // даём пальцам разжаться
            Debug.Log("[PickPlace] старт демонстрации: куб " + Vec(cube.transform.position) +
                      " → " + Vec(placePoint));
            if (Message != null) Message("pick-and-place: подъезд к кубу");
            return true;
        }

        public void Cancel(string why = "отменено")
        {
            if (gripper != null) gripper.SetOpen(true);
            stepAttempts = 0;
            if (Current != Step.Idle)
            {
                Current = Step.Idle;
                if (flow != null) flow.StopExternalMotion("pick-and-place " + why);
                if (Message != null) Message("pick-and-place " + why);
            }
        }

        /// <summary>Кадровое ведение последовательности (вызывает хаб).</summary>
        public void Tick(float deltaTime)
        {
            if (Current == Step.Idle || Current == Step.Done || Current == Step.Failed) return;
            if (flow == null || cube == null) { Current = Step.Failed; return; }

            if (waitTimer > 0f)
            {
                waitTimer -= deltaTime;
                return;
            }

            // Пока идёт предыдущий переход — ждём его конца (следующий шаг не запускаем).
            if (flow.ExternalMotionRunning) return;

            switch (Current)
            {
                case Step.Approach:
                    if (MoveTo(GraspTarget(approachHeight), "подъезд")) Next(Step.Descend, 0.2f);
                    break;

                case Step.Descend:
                    if (MoveTo(GraspTarget(0f), "опускание")) Next(Step.Close, 0.4f);
                    break;

                case Step.Close:
                    // Захват — ПОСЛЕ того, как робот действительно опустился к кубу
                    // (движение уже завершено: Tick ждёт `ExternalMotionRunning`).
                    if (gripper != null && gripper.Held == null && cube != null)
                    {
                        float distance = Vector3.Distance(gripper.GraspPoint, cube.transform.position);
                        if (distance < 0.12f) gripper.Grab(cube.transform);
                        else Debug.LogWarning("[PickPlace] куб не рядом с захватом (" +
                                              distance.ToString("0.000") + " м) — захват не выполнен" +
                                              " · направление инструмента к вертикали " +
                                              Vector3.Dot(gripper.ExtendWorld, Vector3.up).ToString("0.00") +
                                              " (ожидается ≈−1: пальцы вниз; при +1 поменяйте" +
                                              " `toolExtendsAlongNegativeY`)");
                    }
                    gripper.SetOpen(false);
                    Next(Step.Lift, 0.6f);
                    break;

                case Step.Lift:
                    if (MoveTo(GraspTarget(liftHeight), "подъём")) Next(Step.Transfer, 0.2f);
                    break;

                case Step.Transfer:
                    if (MoveTo(WithDrop(placePoint) + Vector3.up * liftHeight, "перенос")) Next(Step.Lower, 0.2f);
                    break;

                case Step.Lower:
                    if (MoveTo(WithDrop(placePoint), "опускание к месту")) Next(Step.Open, 0.4f);
                    break;

                case Step.Open:
                    // Отпускание — тоже после завершения движения «опускание к месту».
                    if (gripper != null && gripper.Held != null) gripper.Release();
                    gripper.SetOpen(true);
                    Next(Step.Retreat, 0.5f);
                    break;

                case Step.Retreat:
                    if (MoveTo(WithDrop(placePoint) + Vector3.up * liftHeight, "отход")) Next(Step.Done, 0.2f);
                    break;
            }
        }

        /// <summary>Точка TCP над точкой стола с учётом выноса точки захвата.</summary>
        private Vector3 WithDrop(Vector3 point)
        {
            return point + Vector3.up * (gripper != null ? gripper.GraspDrop : 0.05f);
        }

        private void Next(Step step, float wait)
        {
            Current = step;
            waitTimer = wait > 0f ? wait : (step == Step.Done || step == Step.Failed ? 0f : -1f);
            if (step == Step.Done)
            {
                Debug.Log("[PickPlace] демонстрация завершена");
                if (Message != null) Message("pick-and-place завершён");
            }
        }

        /// <summary>
        /// Точка TCP для захвата: центр куба + высота + вынос точки захвата от TCP.
        /// (Точка захвата у гриппера смещена вдоль инструмента на `GraspDrop`, поэтому TCP
        /// должен встать ВЫШЕ куба ровно на эту величину.)
        /// </summary>
        private Vector3 GraspTarget(float height)
        {
            Vector3 c = cube != null ? cube.transform.position : placePoint;
            return c + Vector3.up * (height + (gripper != null ? gripper.GraspDrop : 0.05f));
        }

        /// <summary>
        /// Какое направление инструмента просить у `ToolAlign`. По соглашению проекта инструмент
        /// вытянут вдоль локальной −Y, поэтому «пальцы вниз» = нормаль −up (`true`).
        /// ИЗМЕРЕНО прогонами 14.09.2026: при `true` фактическое `ExtendWorld · up` = **+0.90**,
        /// при `false` = **+1.00** — то есть выравнивание на `+up` удаётся, а на `−up` НЕТ:
        /// пальцы остаются вверху и захват «промахивается» на 2 × `GraspDrop`. Причина — доворот
        /// запястья почти на 180° (см. проектную заметку §0.5 «требует доработки»); в
        /// `KvPlanKit.SolvePoseForPoint` добавлена попытка зеркальной конфигурации запястья.
        /// Если пальцы всё равно смотрят вверх — проверьте строку «направление инструмента
        /// к вертикали» в предупреждении шага захвата.
        /// </summary>
        public bool toolExtendsAlongNegativeY = true;

        private Vector3 AlignNormal
        {
            get { return toolExtendsAlongNegativeY ? -Vector3.up : Vector3.up; }
        }

        /// <summary>Перейти в точку TCP обычной траекторией (IK + выравнивание инструмента вниз).</summary>
        private bool MoveTo(Vector3 point, string what)
        {
            if (flow == null || !flow.Validator.Ready) return false;

            double[] q;
            string note;
            bool ok = KvPlanKit.SolvePoseForPoint(flow.Validator, world, point, AlignNormal,
                flow.Validator.CopyCurrent(), out q, out note);
            if (!ok)
            {
                Fail("шаг «" + what + "»: " + note);
                return false;
            }

            double[] start = flow.Validator.CopyCurrent();
            start = flow.Validator.ContinueFrom(start, q);
            TrajectoryCore.PlannedTrajectory plan = KvPlanKit.MakeJointPlan(
                flow.Validator, world, start, q, "Pick&Place: " + what, 0.12f, 32);
            if (plan == null)
            {
                Fail("шаг «" + what + "»: не удалось построить путь");
                return false;
            }

            if (flow.PlayExternalPlan(plan, q, "Pick&Place: " + what))
            {
                stepAttempts = 0;
                if (Message != null) Message("pick-and-place: " + what + " (" + note + ")");
                // Захват и отпускание выполняются НЕ здесь: `PlayExternalPlan` только ЗАПУСКАЕТ
                // движение, робот в этот момент ещё стоит в начале пути (в прогоне из-за этого
                // захват срабатывал за 0.28 м до куба и не выполнялся вовсе). Захват делает шаг
                // `Close`, отпускание — шаг `Open`: они выполняются после того, как движение
                // реально закончилось (Tick ждёт `flow.ExternalMotionRunning`).
                return true;
            }

            // Внешнее движение отклонено (обычно SafetyGate: «малый зазор»). Повторять вечно
            // нельзя — шаг пробуется ограниченное число раз, затем демонстрация честно падает.
            stepAttempts++;
            if (stepAttempts >= Mathf.Max(1, maxStepAttempts))
                Fail("шаг «" + what + "»: внешнее движение отклонено " + stepAttempts +
                     " раза подряд (проверьте зазор до препятствий)");
            return false;
        }

        private void Fail(string why)
        {
            Current = Step.Failed;
            Debug.LogWarning("[PickPlace] " + why);
            if (gripper != null) gripper.SetOpen(true);
            if (Message != null) Message("pick-and-place: " + why);
        }

        public void DestroyCube()
        {
            if (cube == null) return;
            if (gripper != null && gripper.Held == cube.transform) gripper.Release();
            UnityEngine.Object.Destroy(cube);
            cube = null;
            cubeBody = null;
            cubeSpawned = false;
            Current = Step.Idle;
        }

        /// <summary>Верхняя плоскость геометрии под точкой (столешница) или NaN.</summary>
        private static float FindSurfaceY(Vector3 point)
        {
            RaycastHit hit;
            Ray ray = new Ray(point + Vector3.up * 1.5f, Vector3.down);
            if (Physics.Raycast(ray, out hit, 4f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore))
                return hit.point.y;
            return float.NaN;
        }

        private static string Vec(Vector3 v)
        {
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ", " +
                   v.z.ToString("0.000") + ")";
        }
    }
}
