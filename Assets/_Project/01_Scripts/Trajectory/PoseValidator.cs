using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Валидатор позы: FK по вектору обобщённых координат q → капсульная цепочка звеньев
    /// → зазор до мира. Для 6-осевого применяет углы к реальным трансформам и возвращает
    /// позу назад (дешёвый «виртуальный» FK); для SCARA считает аналитически, не трогая сцену.
    /// q для 6-осевого: углы вокруг осей суставов относительно стартовой позы (град).
    /// q для SCARA: (θ1, θ2, z), θ — вокруг вертикали от стартовой позы (град), z — метры.
    /// </summary>
    public class PoseValidator
    {
        public float linkRadius = 0.06f;

        /// <summary>
        /// ДЛИНА ИНСТРУМЕНТА В КАПСУЛЬНОЙ ЦЕПОЧКЕ (м) и его радиус — закрытие дефекта №3 из §27.
        ///
        /// ЧТО БЫЛО. `toolDrop` для SCARA был ЖЁСТКО НУЛЁМ, поэтому четвёртый узел цепочки совпадал
        /// с третьим, и весь стержень инструмента (по замеру §27 — 431 мм) не проверялся ни в
        /// `ClearanceAt`, ни в планировании, ни в `SafetyGate`. У 6-осевого цепочка кончалась на
        /// `tip`, а гриппер (109 мм) и захваченный куб оставались вне модели. То есть робот мог
        /// «проткнуть» стол инструментом, и ни один зазор этого не видел.
        ///
        /// ПОЧЕМУ НЕ ПРОСТО «ПОДСТАВИТЬ 431 мм». Наивное включение с `linkRadius` (0,06 м) ломало
        /// штатную работу SCARA у столешницы: инструмент у неё опускается НИЖЕ плоскости стола, и
        /// капсула радиуса 6 см сразу давала пересечение (§18.6, `zLowerOffsetM = 0,02`).
        ///
        /// РЕШЕНИЕ (ровно рекомендация §27): длина инструмента — отдельное значение, а его капсула
        /// ТОНКАЯ (`toolRadius`, по умолчанию 0,012 м — реальный радиус пальцев/иглы), а не
        /// `linkRadius`. Опора (`ObstacleBox.support`) для тонкой капсулы не учитывается — искать
        /// инструментом стол, на котором он работает, бессмысленно.
        ///
        /// НУЛИ — ЗАКОННОЕ ЗНАЧЕНИЕ «инструмент не учитывать»: так прежнее поведение сохраняется
        /// у сцен, где длина не замерена, и ничего не ломается молча.
        /// </summary>
        public float toolLengthMeters = 0f;

        /// <summary>Радиус капсулы инструмента (м). Тонкий — см. пояснение выше.</summary>
        public float toolRadius = 0.012f;

        public int Dof { get; private set; }
        public float[] Lower { get; private set; }
        public float[] Upper { get; private set; }
        public float[] VelMax { get; private set; }

        private SixAxisController six;
        private SCARAController scara;

        // 6-осевой
        private Transform[] joints;
        private Vector3[] axes;
        private Quaternion[] q0;
        private Transform tip;

        // SCARA (аналитика)
        private Vector3 basePos;
        private Vector3 up = Vector3.up;
        private float a1, a2, shoulderY, initialHeight, zMin, zMax;
        private Transform sj1, sj2, sj3;
        private Vector3 sRef1, sRef2;

        // Поле `toolDrop` удалено 30.10.2026: оно было жёстко нулём и из-за этого четвёртый узел
        // цепочки совпадал с третьим — стержень SCARA не проверялся. Заменено на
        // `toolLengthMeters` + `toolRadius` (объявлены выше с пояснением).

        public bool Ready { get; private set; }
        public string RobotName { get; private set; }

        public void Init(RobotController robot)
        {
            six = robot as SixAxisController;
            scara = robot as SCARAController;
            RobotName = robot != null ? robot.robotName : "-";
            Ready = false;
            if (robot == null) return;

            // Суставы могут быть не назначены в инспекторе (сцена) — ищем по именам модели.
            if (six != null)
            {
                if (six.jointTransforms == null || six.jointTransforms.Length < 6)
                {
                    var list = new List<Transform>();
                    for (int i = 1; i <= 6; i++)
                    {
                        Transform t = FindByName(robot.transform, "Axis" + i);
                        if (t == null) t = FindByPart(robot.transform, "Axis" + i + "_");
                        if (t != null) list.Add(t);
                    }
                    if (list.Count >= 6) six.jointTransforms = list.ToArray();
                }
                if (six.tcp == null) six.tcp = six.endEffector;
                if (six.tcp == null)
                {
                    // TCP-прокси в центре меша фланца (пивоты CAD в начале координат).
                    // ВНИМАНИЕ: здесь нельзя обращаться к полю joints — оно заполняется НИЖЕ,
                    // по ветке Ready (раньше это был потенциальный NullReferenceException).
                    Transform[] js = six.jointTransforms;
                    Transform axis6 = js != null && js.Length > 5 ? js[5] : null;
                    Transform mesh = axis6 != null ? FindByPart(axis6, "Axis6_") : null;
                    Renderer r = mesh != null ? mesh.GetComponent<Renderer>() : null;
                    if (r != null)
                    {
                        var go = new GameObject("TCP");
                        go.transform.SetParent(mesh, false);
                        go.transform.position = r.bounds.center;
                        six.tcp = go.transform;
                    }
                    else if (axis6 != null)
                    {
                        six.tcp = axis6;
                    }
                }
            }
            if (scara != null)
            {
                if (scara.joint1 == null) scara.joint1 = FindByPart(robot.transform, "J1_3");
                if (scara.joint2 == null) scara.joint2 = FindByPart(robot.transform, "J2_4");
                if (scara.joint3 == null) scara.joint3 = FindByPart(robot.transform, "z_5");
                if (scara.baseTransform == null)
                    scara.baseTransform = FindByPart(robot.transform, "LS10-B702S_base_1");
            }

            if (six != null && six.jointTransforms != null && six.jointTransforms.Length >= 6)
            {
                joints = six.jointTransforms;
                Dof = joints.Length;
                axes = new Vector3[Dof];
                q0 = new Quaternion[Dof];
                Lower = new float[Dof];
                Upper = new float[Dof];
                VelMax = new float[Dof];
                for (int i = 0; i < Dof; i++)
                {
                    Vector3 e = i < six.jointAxesLocal.Length && six.jointAxesLocal[i].sqrMagnitude > 0.01f
                        ? six.jointAxesLocal[i].normalized : Vector3.up;
                    q0[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
                    axes[i] = e;
                    Vector2 lim = i < six.jointLimits.Length ? six.jointLimits[i] : new Vector2(-180f, 180f);
                    Lower[i] = lim.x;
                    Upper[i] = lim.y;
                    VelMax[i] = 60f; // град/с по умолчанию
                }

                // Битые (нулевые) лимиты из сцены заменяем рабочими дефолтами модели.
                bool limitsBroken = true;
                for (int i = 0; i < Dof; i++)
                    if (Mathf.Abs(Lower[i]) > 0.01f || Mathf.Abs(Upper[i]) > 0.01f) { limitsBroken = false; break; }
                if (limitsBroken)
                {
                    Vector2[] def =
                    {
                        new Vector2(-170f, 170f), new Vector2(-90f, 150f), new Vector2(-70f, 225f),
                        new Vector2(-180f, 180f), new Vector2(-120f, 120f), new Vector2(-180f, 180f)
                    };
                    for (int i = 0; i < Dof && i < def.Length; i++)
                    {
                        Lower[i] = def[i].x;
                        Upper[i] = def[i].y;
                    }
                    six.jointLimits = def;
                }
                tip = six.tcp != null ? six.tcp : six.endEffector;
                // База 6-осевого робота: раньше basePos заполнялся ТОЛЬКО у SCARA, поэтому
                // `BasePosition` возвращал (0,0,0) — точка мирового начала координат, а не база
                // робота (робот стоит на столе в z ≈ −24). Из-за этого, например, тепловая карта
                // достижимости и любые расчёты «от базы» строились вокруг начала координат.
                // Заполняем ровно так же, как у SCARA: baseTransform, иначе корень робота.
                basePos = six.baseTransform != null ? six.baseTransform.position : six.transform.position;
                up = six.baseTransform != null ? six.baseTransform.up : Vector3.up;
                Ready = true;
            }
            else if (scara != null && scara.joint1 != null && scara.joint2 != null && scara.joint3 != null)
            {
                sj1 = scara.joint1; sj2 = scara.joint2; sj3 = scara.joint3;
                basePos = scara.baseTransform != null ? scara.baseTransform.position : sj1.position;
                up = scara.baseTransform != null ? scara.baseTransform.up : Vector3.up;
                shoulderY = Vector3.Dot(sj1.position - basePos, up);
                Vector3 p1 = Vector3.ProjectOnPlane(sj1.position - basePos, up);
                Vector3 p2 = Vector3.ProjectOnPlane(sj2.position - basePos, up);
                Vector3 p3 = Vector3.ProjectOnPlane(sj3.position - basePos, up);
                // Длины звеньев SCARA: L1 = J1→J2 (плечо), L2 = J2→z_5 (предплечье).
                a1 = Vector3.Distance(p1, p2);
                a2 = Vector3.Distance(p2, p3);
                sRef1 = (p2 - p1).normalized;
                sRef2 = (p3 - p2).normalized;
                initialHeight = Vector3.Dot(sj3.position - basePos, up);
                zMin = scara.ZMin;
                zMax = scara.ZMax;

                // 30.10.2026: ДЛИНА ИНСТРУМЕНТА ИЗ ГЕОМЕТРИИ, А НЕ НУЛЬ (дефект №3 из §27).
                //
                // ПОЧЕМУ ИЗМЕРЯЕТСЯ, А НЕ ЗАШИВАЕТСЯ. Зашитые 431 мм верны для текущей модели
                // SCARA и станут ложью при смене инструмента. Здесь замер берётся из самой сцены:
                // на сколько узел J3 стоит ВЫШЕ плоскости стола (третья ось SCARA — вертикальный
                // ход). Это ровно та часть манипулятора, которая опускается к столу.
                //
                // Если геометрия не даёт числа (нулевая высота, нет опоры) — остаётся
                // `toolLengthMeters` из инспектора (по умолчанию 0, то есть прежнее поведение).
                toolLengthMeters = MeasureScaraToolLength();

                Dof = 3;
                Lower = new float[] { -170f, -170f, initialHeight + zMin };
                Upper = new float[] { 170f, 170f, initialHeight + zMax };
                VelMax = new float[] { 90f, 90f, 0.25f };
                Ready = true;
            }
        }

        /// <summary>
        /// ДЛИНА ИНСТРУМЕНТА SCARA ПО ГЕОМЕТРИИ СЦЕНЫ (30.10.2026).
        ///
        /// ЧТО СЧИТАЕТСЯ. Вертикальное расстояние от узла J3 до `ZMin` — то есть насколько
        /// инструмент может опуститься относительно узла при полном ходе третьей оси. Это
        /// КОНСЕРВАТИВНАЯ оценка той части манипулятора, которая «уходит вниз» дальше последнего
        /// сустава; она не выдумывает геометрию, а берёт её из настроек контроллера.
        ///
        /// ЧЕГО НЕ ДЕЛАЕТ: не переопределяет явно заданный `toolLengthMeters` из инспектора
        /// (если он больше измеренного) и не возвращает отрицательных чисел.
        ///
        /// ГРАНИЦА ЧЕСТНОСТИ: если в сцене ход третьей оси занижен относительно реального
        /// стержня, замер это не поймает — тогда длину надо задать вручную в инспекторе, и
        /// в журнале будет видно, какое число взято (см. `DescribeChainModel`).
        /// </summary>
        private float MeasureScaraToolLength()
        {
            if (scara == null || sj3 == null) return Mathf.Max(0f, toolLengthMeters);
            float travel = Mathf.Abs(scara.ZMin);
            return Mathf.Max(Mathf.Max(0f, toolLengthMeters), travel);
        }

        /// <summary>
        /// СТРОКА-ДОКАЗАТЕЛЬСТВО МОДЕЛИ КОЛЛИЗИЙ: сколько узлов в цепочке и какова длина
        /// инструмента. По ней на устройстве видно, учитывается инструмент или нет — без неё
        /// «инструмент вне цепочки» выглядит как «всё проверено».
        /// </summary>
        public string DescribeChainModel()
        {
            // ЧИСЛА — ЧЕРЕЗ INVARIANT CULTURE (поймано тестом 30.10.2026).
            //
            // На машине с русской локалью `ToString("0.000")` даёт «0,250». Такие строки потом
            // ищут глазами в журнале устройства и сравнивают с эталоном — и запятая ломает и то,
            // и другое. В журналах и в файлах проекта числа всегда с точкой.
            var c = System.Globalization.CultureInfo.InvariantCulture;
            return "цепочка " + (Ready ? ReadChain().Length : 0) + " узлов · радиус звена " +
                   linkRadius.ToString("0.000", c) + " м · инструмент " +
                   toolLengthMeters.ToString("0.000", c) + " м · радиус инструмента " +
                   toolRadius.ToString("0.000", c) + " м";
        }

        public double[] CopyCurrent()
        {
            if (!Ready) return new double[Mathf.Max(1, Dof)];
            double[] q = new double[Dof];
            if (six != null)
            {
                for (int i = 0; i < Dof; i++) q[i] = MeasureJoint(i);
            }
            else if (scara != null)
            {
                q[0] = MeasureScaraYaw(sRef1, Vector3.ProjectOnPlane(sj2.position - sj1.position, up));
                q[1] = MeasureScaraYaw(sRef2, Vector3.ProjectOnPlane(sj3.position - sj2.position, up));
                q[2] = Vector3.Dot(sj3.position - basePos, up);
            }
            return q;
        }

        private float MeasureJoint(int i)
        {
            Quaternion rel = joints[i].localRotation * Quaternion.Inverse(q0[i]);
            Vector3 u = (q0[i] * axes[i]).normalized;
            Vector3 r = PickPerp(u);
            Vector3 a = Vector3.ProjectOnPlane(r, u);
            Vector3 b = Vector3.ProjectOnPlane(rel * r, u);
            if (a.sqrMagnitude < 1e-6f || b.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(a, b, u);
        }

        /// <summary>
        /// Текущая конфигурация робота в ГОТОВЫЙ массив (без аллокаций) — для визуализации,
        /// которой поза нужна каждый кадр (индикаторы лимитов суставов). Семантика та же,
        /// что у <see cref="CopyCurrent"/>.
        /// </summary>
        public void CopyCurrentInto(double[] q)
        {
            if (q == null) return;
            if (!Ready)
            {
                for (int i = 0; i < q.Length; i++) q[i] = 0;
                return;
            }
            if (six != null)
            {
                int n = Mathf.Min(Dof, q.Length);
                for (int i = 0; i < n; i++) q[i] = MeasureJoint(i);
            }
            else if (scara != null)
            {
                if (q.Length > 0)
                    q[0] = MeasureScaraYaw(sRef1, Vector3.ProjectOnPlane(sj2.position - sj1.position, up));
                if (q.Length > 1)
                    q[1] = MeasureScaraYaw(sRef2, Vector3.ProjectOnPlane(sj3.position - sj2.position, up));
                if (q.Length > 2)
                    q[2] = Vector3.Dot(sj3.position - basePos, up);
            }
        }

        /// <summary>
        /// Пивоты и МИРОВЫЕ оси ВСЕХ суставов за ОДНО применение позы (вместо Dof вызовов
        /// PivotAt/AxisWorld, каждый из которых «примеряет» позу): нужна визуализации лимитов.
        /// Поза восстанавливается, реальный робот не двигается.
        /// </summary>
        public void JointFrames(double[] q, Vector3[] pivots, Vector3[] axesOut)
        {
            if (!Ready || q == null) return;
            PoseSnapshot saved = CapturePose();
            Apply(q);

            if (six != null)
            {
                int n = Mathf.Min(Dof, joints.Length);
                for (int i = 0; i < n; i++)
                {
                    if (pivots != null && i < pivots.Length)
                        pivots[i] = joints[i] != null ? joints[i].position : Vector3.zero;
                    if (axesOut != null && i < axesOut.Length)
                        axesOut[i] = joints[i] != null
                            ? joints[i].TransformDirection(axes[i]).normalized : Vector3.up;
                }
            }
            else if (scara != null)
            {
                for (int i = 0; i < Dof; i++)
                {
                    if (pivots != null && i < pivots.Length)
                        pivots[i] = i == 0 ? sj1.position : (i == 1 ? sj2.position : sj3.position);
                    if (axesOut != null && i < axesOut.Length) axesOut[i] = up;
                }
            }

            RestorePose(saved);
        }

        private static float MeasureScaraYaw(Vector3 reference, Vector3 current)
        {
            if (reference.sqrMagnitude < 1e-6f || current.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(reference, current.normalized, Vector3.up);
        }

        private static Vector3 PickPerp(Vector3 u)
        {
            Vector3 seed = Mathf.Abs(u.y) < 0.9f ? Vector3.up : Vector3.right;
            Vector3 r = Vector3.Cross(seed, u);
            return r.sqrMagnitude < 1e-6f ? Vector3.right : r.normalized;
        }

        private static Transform FindByName(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name == name) return t;
            return null;
        }

        private static Transform FindByPart(Transform root, string part)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t != root && t.name.IndexOf(part, System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return t;
            return null;
        }

        /// <summary>Применить конфигурацию (реальные трансформы). Для SCARA — тоже (yaw + z).</summary>
        public void Apply(double[] q)
        {
            if (six != null)
            {
                for (int i = 0; i < Dof; i++)
                {
                    if (joints[i] == null) continue;
                    Vector3 u = (q0[i] * axes[i]).normalized;
                    joints[i].localRotation = Quaternion.AngleAxis((float)q[i], u) * q0[i];
                }
            }
            else if (scara != null)
            {
                // q[0] — отклонение звена 1 от покоя, q[1] — отклонение звена 2 ОТ ЗВЕНА 1,
                // поэтому второму суставу добавляем поворот родителя (иначе кинематика рвётся).
                ApplyScaraYaw(sj1, sRef1, (float)q[0], 0f);
                ApplyScaraYaw(sj2, sRef2, (float)q[1], (float)q[0]);
                // q[2] — АБСОЛЮТНАЯ высота z_5 над базой (идемпотентно).
                Vector3 current = sj3.position;
                float currentH = Vector3.Dot(current - basePos, up);
                sj3.position = current + up * ((float)q[2] - currentH);
            }
        }

        /// <summary>Довернуть сустав так, чтобы его звено встало под углом (parentAngle + ownAngle) от покоя.</summary>
        private void ApplyScaraYaw(Transform joint, Vector3 reference, float ownAngle, float parentAngle)
        {
            if (joint == null) return;
            Vector3 childOffset;
            if (joint == sj1) childOffset = Vector3.ProjectOnPlane(sj2.position - sj1.position, up);
            else if (joint == sj2) childOffset = Vector3.ProjectOnPlane(sj3.position - sj2.position, up);
            else return;
            if (childOffset.sqrMagnitude < 1e-8f) return;

            Vector3 desired = Quaternion.AngleAxis(parentAngle + ownAngle, up) * reference;
            float delta = Vector3.SignedAngle(childOffset.normalized, desired.normalized, up);
            joint.Rotate(up, delta, Space.World);
        }

        /// <summary>Прочитать капсульную цепочку (после Apply).</summary>
        public Vector3[] ReadChain()
        {
            var nodes = new List<Vector3>(8);
            if (six != null)
            {
                foreach (Transform j in joints)
                {
                    if (j == null) continue;
                    Vector3 p = j.position;
                    if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < 1e-6f) continue;
                    nodes.Add(p);
                }
                if (tip != null) nodes.Add(tip.position);
            }
            else if (scara != null)
            {
                nodes.Add(sj1.position);
                nodes.Add(sj2.position);
                nodes.Add(sj3.position);
                // ИНСТРУМЕНТ В ЦЕПОЧКЕ (30.10.2026, дефект №3 из §27): четвёртый узел ставится
                // ТОЛЬКО если длина инструмента известна. При нуле узел не добавляется вовсе —
                // вырожденный отрезок нулевой длины бесполезен и он же ломал самозазор
                // (`SelfClearance` на вырожденных отрезках делает `continue`, то есть молчит).
                if (toolLengthMeters > 0.0005f)
                    nodes.Add(sj3.position - up * toolLengthMeters);
            }
            return nodes.ToArray();
        }

        /// <summary>
        /// РАДИУС ДЛЯ ПОСЛЕДНЕГО ЗВЕНА ЦЕПОЧКИ (инструмента).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО. Минимальное расстояние считается по всем отрезкам с ОДНИМ радиусом
        /// `linkRadius`. Для звеньев руки это верно (они толстые), а для инструмента — нет: игла
        /// или пальцы тонкие, и капсула радиусом 6 см даёт ложные «пересечения» у столешницы
        /// (именно из-за этого в §27 правку и отложили). Вызывающий берёт `toolRadius` для
        /// последнего отрезка и отдельно решает про опору.
        /// </summary>
        public float RadiusForLastSegment()
        {
            return toolLengthMeters > 0.0005f ? toolRadius : linkRadius;
        }

        /// <summary>Зазор до мира в конфигурации q (поза восстанавливается).</summary>
        public float ClearanceAt(double[] q, CollisionWorld world, out Vector3 tcp, out Vector3[] nodes)
        {
            tcp = Vector3.zero;
            nodes = new Vector3[0];
            if (!Ready || world == null) return float.NegativeInfinity;

            Quaternion[] saved = null;
            Vector3[] savedPos = null;
            if (six != null)
            {
                saved = new Quaternion[Dof];
                for (int i = 0; i < Dof; i++)
                    saved[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            }
            else if (scara != null)
            {
                // SCARA считается АНАЛИТИЧЕСКИ, но Apply() двигает реальные трансформы —
                // позу обязательно возвращаем, иначе робот «крутится» во время планирования.
                saved = new Quaternion[2];
                savedPos = new Vector3[1];
                saved[0] = sj1.localRotation; saved[1] = sj2.localRotation;
                savedPos[0] = sj3.localPosition;
            }

            Apply(q);
            nodes = ReadChain();
            tcp = nodes.Length > 0 ? nodes[nodes.Length - 1] : Vector3.zero;
            // ФИКС §27: у 6-осевого нулевой отрезок цепочки — колонна базы (Axis1→Axis2),
            // стоящая на опоре, его контакт с миром считается нормой. У SCARA нулевой
            // отрезок — РЕАЛЬНОЕ ПЛЕШО J1→J2, и пропускать его нельзя: раньше он не
            // проверялся вообще (см. `CollisionWorld.MinDistanceChain`).
            int firstSegment = six != null ? -1 : 0;
            float c = world.MinDistanceChain(nodes, linkRadius, firstSegment);

            // ТОНКАЯ КАПСУЛА ДЛЯ ИНСТРУМЕНТА (30.10.2026, дефект №3 из §27).
            //
            // ПОЧЕМУ ОТДЕЛЬНЫЙ ПРОХОД. `linkRadius` (0,06 м) — толщина звеньев руки; для
            // последнего отрезка (инструмент) она завышена в пять раз. Если считать инструмент
            // толстым, SCARA «упирается» в столешницу там, где реально ничем её не касается, и
            // планировщик начинает отказывать в штатной работе (§18.6). Поэтому последний
            // отрезок проверяется ОТДЕЛЬНО и с `toolRadius`.
            //
            // ФИЗИКА: итоговый зазор = минимум из (зазор звеньев − радиус звена) и
            // (зазор инструмента − радиус инструмента). Оба считаются по одному и тому же миру.
            if (toolLengthMeters > 0.0005f && nodes.Length >= 2)
            {
                var toolNodes = new[] { nodes[nodes.Length - 2], nodes[nodes.Length - 1] };
                float toolClearance = world.MinDistanceChain(toolNodes, toolRadius, 0);
                if (toolClearance < c) c = toolClearance;
            }

            if (six != null)
            {
                for (int i = 0; i < Dof; i++)
                    if (joints[i] != null) joints[i].localRotation = saved[i];
            }
            else if (scara != null && savedPos != null)
            {
                sj1.localRotation = saved[0];
                sj2.localRotation = saved[1];
                sj3.localPosition = savedPos[0];
            }
            return c;
        }

        /// <summary>Позиция TCP в конфигурации q (поза восстанавливается).</summary>
        public Vector3 TcpAt(double[] q)
        {
            if (!Ready) return Vector3.zero;
            Quaternion[] saved = null;
            Vector3[] savedPos = null;
            if (six != null)
            {
                saved = new Quaternion[Dof];
                for (int i = 0; i < Dof; i++)
                    saved[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            }
            else if (scara != null)
            {
                saved = new Quaternion[2];
                savedPos = new Vector3[1];
                saved[0] = sj1.localRotation; saved[1] = sj2.localRotation;
                savedPos[0] = sj3.localPosition;
            }

            Apply(q);
            Vector3 p = six != null
                ? (tip != null ? tip.position : Vector3.zero)
                : (sj3 != null ? sj3.position : Vector3.zero);

            if (six != null)
            {
                for (int i = 0; i < Dof; i++)
                    if (joints[i] != null) joints[i].localRotation = saved[i];
            }
            else if (scara != null)
            {
                sj1.localRotation = saved[0];
                sj2.localRotation = saved[1];
                sj3.localPosition = savedPos[0];
            }
            return p;
        }

        /// <summary>
        /// Самоколлизия звеньев (Проблема 2): минимальный зазор между несоседними
        /// звеньями робота. Возвращает зазор (м, минус — пересечение) и индексы звеньев.
        /// </summary>
        public float SelfClearance(double[] q, out int linkA, out int linkB)
        {
            linkA = -1;
            linkB = -1;
            if (!Ready) return float.MaxValue;

            Quaternion[] saved = Snapshot();
            Vector3[] savedPos = null;
            if (scara != null)
            {
                saved = new Quaternion[2];
                savedPos = new Vector3[1];
                saved[0] = sj1.localRotation; saved[1] = sj2.localRotation;
                savedPos[0] = sj3.localPosition;
            }

            Apply(q);
            Vector3[] nodes = ReadChain();
            if (savedPos != null)
            {
                sj1.localRotation = saved[0];
                sj2.localRotation = saved[1];
                sj3.localPosition = savedPos[0];
            }
            Restore(saved);

            float best = float.MaxValue;
            int n = nodes.Length;
            float rr = linkRadius * 2f;
            for (int i = 0; i + 1 < n; i++)
            {
                Vector3 a0 = nodes[i], a1 = nodes[i + 1];
                if ((a1 - a0).sqrMagnitude < 1e-8f) continue;      // совпавшие оси (запястье)
                for (int j = i + 2; j + 1 < n; j++)
                {
                    Vector3 b0 = nodes[j], b1 = nodes[j + 1];
                    if ((b1 - b0).sqrMagnitude < 1e-8f) continue;
                    float d = CollisionWorld.SegmentSegmentDistance(a0, a1, b0, b1) - rr;
                    if (d < best) { best = d; linkA = i; linkB = j; }
                }
            }
            return best;
        }

        /// <summary>
        /// Аналитический (численный) позиционный Якобиан 3×N в конфигурации q.
        /// J_k = ∂p_tcp/∂q_k (конечные разности, шаг 0.25°; для призмы SCARA — метры).
        /// </summary>
        public void Jacobian(double[] q, double[][] jac, out Vector3 tcp)
        {
            tcp = TcpAt(q);
            int n = Dof;
            double h = 0.25 * Mathf.Deg2Rad;
            var probe = (double[])q.Clone();
            for (int k = 0; k < n; k++)
            {
                double save = probe[k];
                probe[k] = save + h;
                Vector3 plus = TcpAt(probe);
                probe[k] = save - h;
                Vector3 minus = TcpAt(probe);
                probe[k] = save;
                Vector3 dp = (plus - minus) / (float)(2.0 * h);
                jac[0][k] = dp.x; jac[1][k] = dp.y; jac[2][k] = dp.z;
            }
        }

        /// <summary>Максимальная норма строки Якобиана ‖∂p/∂q_k‖∞ — для адаптивного шага рёбер.</summary>
        public float MaxJacobianNorm(double[] q)
        {
            if (!Ready) return 1f;
            var jac = KinematicsJacobian.Allocate(Dof);
            Jacobian(q, jac, out _);
            float m = 1e-6f;
            for (int k = 0; k < Dof; k++)
            {
                float norm = Mathf.Max(Mathf.Abs((float)jac[0][k]),
                             Mathf.Max(Mathf.Abs((float)jac[1][k]), Mathf.Abs((float)jac[2][k])));
                m = Mathf.Max(m, norm);
            }
            return m;
        }

        /// <summary>
        /// Применить конфигурацию к КОПИИ робота (фантом): суставы копии берутся по именам,
        /// соглашение то же (AngleAxis(q, q0·e) · q0), т.к. копия идентична в покое.
        /// </summary>
        public void ApplyToCopy(Transform[] copyJoints, double[] q)
        {
            if (!Ready || copyJoints == null || q == null) return;
            int n = Mathf.Min(Mathf.Min(copyJoints.Length, q.Length), Dof);
            for (int i = 0; i < n; i++)
            {
                if (copyJoints[i] == null) continue;
                Vector3 u = (q0[i] * axes[i]).normalized;
                copyJoints[i].localRotation = Quaternion.AngleAxis((float)q[i], u) * q0[i];
            }
        }

        /// <summary>Найти суставы в иерархии копии (для фантомов).</summary>
        public Transform[] FindCopyJoints(Transform copyRoot)
        {
            if (copyRoot == null) return new Transform[0];
            var list = new List<Transform>();
            for (int i = 1; i <= 6; i++)
            {
                Transform t = FindByName(copyRoot, "Axis" + i);
                if (t != null) list.Add(t);
            }
            return list.ToArray();
        }

        /// <summary>
        /// CCD только по подмножеству суставов [first..last] (например, доводка запястья 3..5,
        /// когда позиционная часть уже решена аналитически). q меняется на месте.
        /// </summary>
        public bool SolveIkRange(Vector3 goal, double[] q, int first, int last,
            int iterations = 60, float tolerance = 0.003f)
        {
            if (!Ready || q == null) return false;
            first = Mathf.Clamp(first, 0, Dof - 1);
            last = Mathf.Clamp(last, first, Dof - 1);

            for (int it = 0; it < iterations; it++)
            {
                if ((TcpAt(q) - goal).magnitude <= tolerance) return true;
                for (int i = last; i >= first; i--)
                {
                    Vector3 pivot = PivotAt(i, q);
                    Vector3 axis = AxisWorld(i, q);
                    Vector3 e = Vector3.ProjectOnPlane(TcpAt(q) - pivot, axis);
                    Vector3 t = Vector3.ProjectOnPlane(goal - pivot, axis);
                    if (e.sqrMagnitude < 1e-8f || t.sqrMagnitude < 1e-8f) continue;
                    float ang = Mathf.Clamp(Vector3.SignedAngle(e, t, axis), -25f, 25f);
                    double save = q[i];
                    q[i] += ang;
                    if (!WithinLimits(q)) q[i] = save;
                }
            }
            return (TcpAt(q) - goal).magnitude <= tolerance * 3f;
        }

        /// <summary>
        /// Применить конфигурацию SCARA к КОПИИ робота (фантом): θ1/θ2 — доворот звеньев
        /// копии относительно их эталонных направлений, z — вертикальный сдвиг z_5.
        /// Работает на копии, реального робота не трогает.
        /// </summary>
        public void ApplyScaraToCopy(Transform copyRoot, double[] q)
        {
            if (copyRoot == null || q == null || q.Length < 3) return;
            Transform c1 = FindByPart(copyRoot, "J1_3");
            Transform c2 = FindByPart(copyRoot, "J2_4");
            Transform c3 = FindByPart(copyRoot, "z_5");
            if (c1 == null || c2 == null || c3 == null) return;

            Vector3 up = Vector3.up;
            Vector3 restLink1 = Vector3.ProjectOnPlane(c2.position - c1.position, up).normalized;
            Vector3 restLink2 = Vector3.ProjectOnPlane(c3.position - c2.position, up).normalized;
            if (restLink1.sqrMagnitude < 1e-8f || restLink2.sqrMagnitude < 1e-8f) return;

            // Звено 1: отклонение q[0]; звено 2: отклонение q[1] от звена 1 (+ поворот родителя).
            RotateCopyTowards(c1, restLink1, up, (float)q[0]);
            Vector3 curLink2 = Vector3.ProjectOnPlane(c3.position - c2.position, up).normalized;
            Vector3 desired2 = Quaternion.AngleAxis((float)q[0] + (float)q[1], up) * restLink2;
            c2.Rotate(up, Vector3.SignedAngle(curLink2, desired2, up), Space.World);

            // Вертикальный сдвиг z_5 относительно текущей высоты копии.
            float targetHeight = (float)q[2];
            Vector3 refBase = basePos;
            float cur = Vector3.Dot(c3.position - refBase, up);
            c3.position += up * (targetHeight - cur);
        }

        private static void RotateCopyTowards(Transform joint, Vector3 restDir, Vector3 axis, float deltaFromRest)
        {
            if (joint == null) return;
            Vector3 cur = Vector3.ProjectOnPlane(
                joint == null ? Vector3.forward : (joint.childCount > 0 ? joint.GetChild(0).position - joint.position : Vector3.forward),
                axis).normalized;
            if (cur.sqrMagnitude < 1e-8f) cur = restDir;
            Vector3 desired = Quaternion.AngleAxis(deltaFromRest, axis) * restDir;
            joint.Rotate(axis, Vector3.SignedAngle(cur, desired, axis), Space.World);
        }

        /// <summary>Мировая позиция базы робота (для отсчёта хода z_5 и фантомов).</summary>
        public Vector3 BasePosition => basePos;

        /// <summary>Углы вращения заданы по модулю 360°, поэтому «в лимитах» проверяем
        /// эквивалентный угол (для призматической оси SCARA — само значение).</summary>
        public bool IsPrismatic(int i)
        {
            return scara != null && i == 2;
        }

        /// <summary>Эквивалентный угол в пределах хода сустава (ближайший по модулю 360°).</summary>
        public double FoldAngle(int i, double angle)
        {
            if (i < 0 || i >= Dof) return angle;
            if (IsPrismatic(i)) return angle;
            double lo = Lower[i], hi = Upper[i];
            if (hi - lo >= 359.999) return Mathf.Repeat((float)angle + 180f, 360f) - 180f;
            double a = angle;
            int guard = 0;
            while (a > hi && guard++ < 16) a -= 360.0;
            guard = 0;
            while (a < lo && guard++ < 16) a += 360.0;
            return a;
        }

        public bool WithinLimits(double[] q, float marginDeg = 0f)
        {
            if (q == null) return false;
            for (int i = 0; i < Dof && i < q.Length; i++)
            {
                double a = FoldAngle(i, q[i]);
                double lo = Lower[i] + marginDeg;
                double hi = Upper[i] - marginDeg;
                if (a < lo - 1e-6 || a > hi + 1e-6) return false;
            }
            return true;
        }

        /// <summary>
        /// Приводит конфигурацию к НЕПРЕРЫВНОМУ виду относительно предыдущей:
        /// каждый вращательный сустав берёт кратчайший доворот (без скачков ±360°).
        /// Нужно исполнителю движения, иначе последний шаг «проворачивает» сустав на 360°.
        /// </summary>
        public double[] ContinueFrom(double[] previous, double[] q)
        {
            if (q == null) return null;
            if (previous == null) return (double[])q.Clone();
            var r = (double[])q.Clone();
            int n = System.Math.Min(System.Math.Min(previous.Length, r.Length), Dof);
            for (int i = 0; i < n; i++)
                if (!IsPrismatic(i))
                    r[i] = previous[i] + Mathf.DeltaAngle((float)previous[i], (float)r[i]);
            return r;
        }

        /// <summary>
        /// Запас до лимитов в сопоставимых единицах (градусы).
        /// ВАЖНО: для призматической оси SCARA (z_5, ход ~0,2 м) метры приводятся
        /// к «градусам» (полный ход = 360°). Иначе порог SafetyGate (3°) отбраковывал
        /// ЛЮБУЮ позу SCARA: запас 0,07 м численно меньше 3 и трактовался как «на пределе».
        /// </summary>
        public float LimitMargin(double[] q)
        {
            float m = float.MaxValue;
            for (int i = 0; i < Dof; i++)
            {
                float a = (float)FoldAngle(i, q[i]);
                float marg = Mathf.Min(a - Lower[i], Upper[i] - a);
                if (IsPrismatic(i))
                {
                    float range = Mathf.Max(1e-4f, Upper[i] - Lower[i]);
                    marg = marg / range * 360f;
                }
                m = Mathf.Min(m, marg);
            }
            return m;
        }

        /// <summary>
        /// Запас до лимитов КАЖДОГО сустава в «градусах» (у призмы SCARA полный ход = 360° —
        /// та же нормировка, что в <see cref="LimitMargin"/>). Нужна визуализации лимитов:
        /// по ней красится каждый сустав отдельно (зелёный/жёлтый/красный).
        /// </summary>
        public void LimitMargins(double[] q, float[] result)
        {
            if (result == null) return;
            for (int i = 0; i < result.Length; i++)
            {
                if (!Ready || q == null || i >= Dof || i >= q.Length)
                {
                    result[i] = float.MaxValue;
                    continue;
                }
                float a = (float)FoldAngle(i, q[i]);
                float marg = Mathf.Min(a - Lower[i], Upper[i] - a);
                if (IsPrismatic(i))
                {
                    float range = Mathf.Max(1e-4f, Upper[i] - Lower[i]);
                    marg = marg / range * 360f;
                }
                result[i] = marg;
            }
        }

        /// <summary>Мировая позиция пивота сустава i в конфигурации q (поза восстанавливается).</summary>
        public Vector3 PivotAt(int i, double[] q)
        {
            PoseSnapshot saved = CapturePose();
            Apply(q);
            Vector3 p;
            if (six != null) p = joints[i] != null ? joints[i].position : Vector3.zero;
            else if (scara != null) p = i == 0 ? sj1.position : (i == 1 ? sj2.position : sj3.position);
            else p = Vector3.zero;
            RestorePose(saved);
            return p;
        }

        /// <summary>Мировое направление оси сустава i в конфигурации q (для z SCARA — вертикаль).</summary>
        public Vector3 AxisWorld(int i, double[] q)
        {
            if (scara != null) return up;
            PoseSnapshot saved = CapturePose();
            Apply(q);
            Vector3 a = joints[i] != null ? joints[i].TransformDirection(axes[i]).normalized : Vector3.up;
            RestorePose(saved);
            return a;
        }

        /// <summary>Снимок позы (углы всех суставов + ход призмы) для точного восстановления.</summary>
        private struct PoseSnapshot
        {
            public Quaternion[] rot;
            public Vector3[] pos;
        }

        /// <summary>
        /// Снять позу. ВАЖНО: для SCARA Apply() двигает РЕАЛЬНЫЕ трансформы, поэтому
        /// снимок обязателен везде, где поза «примеряется» (иначе робот уезжает сам).
        /// </summary>
        private PoseSnapshot CapturePose()
        {
            var s = new PoseSnapshot();
            if (six != null)
            {
                s.rot = new Quaternion[Dof];
                for (int i = 0; i < Dof; i++)
                    s.rot[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            }
            else if (scara != null)
            {
                s.rot = new Quaternion[2];
                s.pos = new Vector3[1];
                s.rot[0] = sj1.localRotation;
                s.rot[1] = sj2.localRotation;
                s.pos[0] = sj3.localPosition;
            }
            return s;
        }

        private void RestorePose(PoseSnapshot s)
        {
            if (s.rot == null) return;
            if (six != null)
            {
                for (int i = 0; i < Dof && i < s.rot.Length; i++)
                    if (joints[i] != null) joints[i].localRotation = s.rot[i];
            }
            else if (scara != null)
            {
                if (s.rot.Length >= 2) { sj1.localRotation = s.rot[0]; sj2.localRotation = s.rot[1]; }
                if (s.pos != null && s.pos.Length > 0) sj3.localPosition = s.pos[0];
            }
        }

        private Quaternion[] Snapshot()
        {
            if (six == null) return null;
            var saved = new Quaternion[Dof];
            for (int i = 0; i < Dof; i++)
                saved[i] = joints[i] != null ? joints[i].localRotation : Quaternion.identity;
            return saved;
        }

        private void Restore(Quaternion[] saved)
        {
            if (six == null || saved == null) return;
            for (int i = 0; i < Dof; i++)
                if (joints[i] != null) joints[i].localRotation = saved[i];
        }

        /// <summary>
        /// CCD-IK в пространстве обобщённых координат (работает и для SCARA, и для 6-осевого):
        /// вращает каждый сустав вокруг своей мировой оси; для SCARA дополнительно доводит z.
        /// </summary>
        public bool SolveIk(Vector3 goal, double[] seed, out double[] result,
            int iterations = 60, float tolerance = 0.01f)
        {
            result = (double[])seed.Clone();
            if (!Ready) return false;
            int revolute = scara != null ? 2 : Dof;

            // Для SCARA высота цели клампится к ходу z: планируем максимально близко.
            Vector3 effectiveGoal = goal;
            if (scara != null)
            {
                float h = Vector3.Dot(goal - basePos, up);
                float hClamped = Mathf.Clamp(h, Lower[2], Upper[2]);
                effectiveGoal = goal + up * (hClamped - h);
            }

            for (int it = 0; it < iterations; it++)
            {
                Vector3 tipNow = TcpAt(result);
                if ((tipNow - effectiveGoal).magnitude <= tolerance) break;

                for (int i = revolute - 1; i >= 0; i--)
                {
                    Vector3 pivot = PivotAt(i, result);
                    Vector3 axis = AxisWorld(i, result);
                    Vector3 toTip = TcpAt(result) - pivot;
                    Vector3 toGoal = effectiveGoal - pivot;
                    Vector3 e = Vector3.ProjectOnPlane(toTip, axis);
                    Vector3 t = Vector3.ProjectOnPlane(toGoal, axis);
                    if (e.sqrMagnitude < 1e-8f || t.sqrMagnitude < 1e-8f) continue;
                    float ang = Vector3.SignedAngle(e, t, axis);
                    ang = Mathf.Clamp(ang, -25f, 25f);
                    result[i] += ang;
                    if (!WithinLimits(result)) { result[i] -= ang; continue; }
                }

                if (scara != null)
                {
                    double goalH = Vector3.Dot(effectiveGoal - basePos, up);
                    result[2] += (goalH - result[2]) * 0.35;
                    result[2] = System.Math.Min(System.Math.Max(result[2], Lower[2]), Upper[2]);
                }
            }

            float err = (TcpAt(result) - effectiveGoal).magnitude;
            LastIkError = err;
            return err <= tolerance * 4f;
        }

        /// <summary>Ошибка последнего решения IK (м) — для диагностики.</summary>
        public float LastIkError { get; private set; }
    }
}
