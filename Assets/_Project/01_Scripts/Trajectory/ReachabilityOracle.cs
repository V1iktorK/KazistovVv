using System.Collections.Generic;
using KazistovVvKinematics;
using UnityEngine;

namespace TrajectoryCore
{
    public enum ReachVerdict
    {
        Safe,          // зелёный: достижимо и есть запас
        Marginal,      // жёлтый: достижимо, но малый запас / лимит / близко к сингулярности
        Collision,     // красный: столкновение по пути/в позе
        Unreachable    // красный: вне рабочей зоны / IK не сходится
    }

    public struct ReachResult
    {
        public ReachVerdict verdict;
        public float clearance;   // минимальный зазор, м (минус = пересечение)
        public Vector3 tcp;       // где окажется инструмент
        public string reason;     // человекочитаемая причина
        public long stampMs;
    }

    /// <summary>
    /// Reachability Oracle (E1 плана): онлайн-вердикт по точке прицела.
    /// * SCARA — аналитически: кольцо рабочей зоны + ход z + капсулы корпуса (2 ветви локтя);
    /// * 6-осевой — численно: seeded CCD в текущей позе, затем проверка капсульной цепочки.
    /// Результат кэшируется по вокселю 1 см и сбрасывается при смене версии мира/позы.
    /// </summary>
    public class ReachabilityOracle
    {
        public float clearance = 0.02f;    // требуемый запас, м
        public float linkRadius = 0.06f;   // радиус звеньев, м
        public int ccdIterations = 24;
        public float ccdTolerance = 0.01f;
        [Tooltip("Радиус РАБОЧЕЙ ЗОНЫ от базы робота, м (быстрый отсев «вне рабочей зоны (далеко)»). " +
                 "Публичное поле: визуализация зоны достижимости рисует по нему границу " +
                 "(WorkspaceVisualizer.oracleZoneRadius), поэтому значение — один источник правды.")]
        public float workZoneRadius = 1.6f;
        [Tooltip("Радиус «внутри корпуса робота», м — точка ближе считается недостижимой.")]
        public float bodyZoneRadius = 0.15f;

        private CollisionWorld world;
        private SixAxisController six;
        private SCARAController scara;

        // SCARA-геометрия (кэш)
        private float a1, a2, baseY, initialHeight, zMin, zMax;
        private Vector3 basePos;
        private Vector3 baseUp = Vector3.up;

        private readonly Dictionary<long, ReachResult> cache = new Dictionary<long, ReachResult>();
        private uint cachedWorldVersion;
        private float poseSignature = -1f;
        private float cacheTime;

        public bool Ready { get; private set; }
        public string RobotName { get; private set; }

        public void Init(RobotController robot, CollisionWorld collisionWorld)
        {
            world = collisionWorld;
            six = robot as SixAxisController;
            scara = robot as SCARAController;
            Ready = six != null || scara != null;
            RobotName = robot != null ? robot.robotName : "-";
            cache.Clear();
            cachedWorldVersion = 0;
            poseSignature = -1f;

            if (scara != null) CacheScaraGeometry();
        }

        private void CacheScaraGeometry()
        {
            Transform j1 = scara.joint1, j2 = scara.joint2, j3 = scara.joint3;
            if (j1 == null || j2 == null || j3 == null) return;
            if (scara.baseTransform == null)
            {
                foreach (Transform t in scara.GetComponentsInChildren<Transform>(true))
                    if (t.name.StartsWith("LS10-B702S_base")) { scara.baseTransform = t; break; }
            }
            basePos = scara.baseTransform != null ? scara.baseTransform.position : j1.position;
            baseUp = scara.baseTransform != null ? scara.baseTransform.up : Vector3.up;
            // Плоское звено считаем от J1 (плечо), длины: L1 = J1→J2, L2 = J2→z_5.
            Vector3 p1 = Vector3.ProjectOnPlane(j1.position - basePos, baseUp);
            Vector3 p2 = Vector3.ProjectOnPlane(j2.position - basePos, baseUp);
            Vector3 p3 = Vector3.ProjectOnPlane(j3.position - basePos, baseUp);
            a1 = Vector3.Distance(p1, p2);
            a2 = Vector3.Distance(p2, p3);
            sRef1 = (p2 - p1).normalized;
            sRef2 = (p3 - p2).normalized;
            initialHeight = Vector3.Dot(j3.position - basePos, baseUp);
            zMin = scara.ZMin;
            zMax = scara.ZMax;
        }

        private Vector3 sRef1 = Vector3.forward;
        private Vector3 sRef2 = Vector3.forward;

        /// <summary>Вердикт по точке прицела (мир).</summary>
        public ReachResult Query(Vector3 point)
        {
            ReachResult r = new ReachResult { verdict = ReachVerdict.Unreachable, reason = "нет робота" };
            if (!Ready || world == null) return r;

            float sig = PoseSignature();
            if (cachedWorldVersion != world.Version || Mathf.Abs(sig - poseSignature) > 0.0001f)
            {
                cache.Clear();
                cachedWorldVersion = world.Version;
                poseSignature = sig;
                if (scara != null) CacheScaraGeometry();
            }

            long key = VoxelKey(point);
            ReachResult cachedResult;
            if (cache.TryGetValue(key, out cachedResult) &&
                Time.realtimeSinceStartup - cachedResult.stampMs / 1000f < CacheTtlSeconds)
                return cachedResult;

            r = scara != null ? QueryScara(point) : QuerySixAxis(point);
            r.stampMs = (long)(Time.realtimeSinceStartup * 1000f);
            cache[key] = r;
            return r;
        }

        /// <summary>
        /// СКОЛЬКО ЖИВЁТ ЗАПИСЬ КЭША ДОСТИЖИМОСТИ, секунды (P1-11).
        ///
        /// ЧТО БЫЛО. Свежесть проверялась ОДНИМ полем `cacheTime` на весь словарь, и оно
        /// перезаписывалось при КАЖДОМ попадании в новую точку. Поэтому запись, положенная
        /// десять минут назад, считалась свежей, если рядом только что спросили другую точку.
        /// По этому вердикту красится прицел и оператор решает, тянуться ли туда.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Оператор убрал со стола препятствие (или подвинул стол) — прицел
        /// продолжает показывать «недостижимо/пересечение» по старому вердикту, и это выглядит как
        /// «робот разучился». Обратный случай хуже: зона освободилась, а вердикт «можно» устарел.
        ///
        /// ТЕПЕРЬ: время хранится ВМЕСТЕ с записью (`ReachResult.stampMs`), у каждой точки свой срок.
        /// </summary>
        public const float CacheTtlSeconds = 1.5f;

        /// <summary>Сколько записей в кэше сейчас (для теста P1-11 и журнала).</summary>
        public int CacheCount { get { return cache.Count; } }

        private float PoseSignature()
        {
            if (scara != null && scara.joint1 != null)
                return scara.joint1.localEulerAngles.y + (scara.joint3 != null ? scara.joint3.localPosition.y * 10f : 0f);
            if (six != null && six.jointTransforms != null)
            {
                float s = 0f;
                foreach (Transform j in six.jointTransforms)
                    if (j != null) s += j.localEulerAngles.y + j.localEulerAngles.x * 0.01f;
                return s;
            }
            return 0f;
        }

        private static long VoxelKey(Vector3 p)
        {
            long x = (long)Mathf.Round(p.x * 100f);
            long y = (long)Mathf.Round(p.y * 100f);
            long z = (long)Mathf.Round(p.z * 100f);
            return (x * 73856093L) ^ (y * 19349663L) ^ (z * 83492791L);
        }

        // ------------------------------------------------------------- SCARA

        private ReachResult QueryScara(Vector3 point)
        {
            var res = new ReachResult();
            Transform j1 = scara.joint1, j2 = scara.joint2, j3 = scara.joint3;
            if (j1 == null || j2 == null || j3 == null)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "SCARA: нет суставов";
                return res;
            }

            Vector3 shoulder = j1.position;
            Vector3 toTarget = Vector3.ProjectOnPlane(point - shoulder, baseUp);
            float r = toTarget.magnitude;
            float reachMax = a1 + a2 - 0.002f;
            float reachMin = Mathf.Abs(a1 - a2) + 0.002f;
            if (r > reachMax || r < reachMin)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = r > reachMax ? "вне вылета руки" : "ближе мертвой зоны";
                res.tcp = shoulder + toTarget.normalized * Mathf.Clamp(r, reachMin, reachMax);
                return res;
            }

            // Ход Z (стержень z_5 относительно базы).
            float targetHeight = Vector3.Dot(point - basePos, baseUp);
            float desired = Mathf.Clamp(targetHeight, initialHeight + zMin, initialHeight + zMax);
            float zSlide = desired - initialHeight;
            float zError = targetHeight - desired;

            float cosQ2 = Mathf.Clamp((r * r - a1 * a1 - a2 * a2) / (2f * a1 * a2), -1f, 1f);
            float q2Abs = Mathf.Acos(cosQ2) * Mathf.Rad2Deg;
            float azimuth = Vector3.SignedAngle(sRef1, toTarget, baseUp);

            float bestClearance = float.NegativeInfinity;
            Vector3 bestTcp = point;
            for (int s = 0; s < 2; s++)
            {
                float q2 = s == 0 ? q2Abs : -q2Abs;
                float phi2 = Mathf.Atan2(a2 * Mathf.Sin(q2 * Mathf.Deg2Rad),
                                          a1 + a2 * Mathf.Cos(q2 * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                float q1 = azimuth - phi2;

                Vector3 elbowDir = Quaternion.AngleAxis(q1, baseUp) * sRef1;
                Vector3 wristDir = Quaternion.AngleAxis(q1 + phi2, baseUp) * sRef1;
                Vector3 elbow = shoulder + elbowDir * a1;
                Vector3 wrist = shoulder + wristDir * r + baseUp * zSlide;

                var nodes = new List<Vector3> { shoulder, elbow, wrist };
                // ФИКС §27: у SCARA оба звена рабочие, поэтому проверяются ВСЕ отрезки цепочки
                // (раньше `MinDistanceChain` пропускал первый — плечо J1→J2 — и оракул
                // показывал «запас достаточный» там, где плечо входило в препятствие).
                float clearanceNow = world.MinDistanceChain(nodes, linkRadius, 0);
                if (clearanceNow > bestClearance)
                {
                    bestClearance = clearanceNow;
                    bestTcp = wrist;
                }
            }

            res.clearance = bestClearance;
            res.tcp = bestTcp;

            if (bestClearance < 0f)
            {
                res.verdict = ReachVerdict.Collision;
                res.reason = "пересечение со сценой";
            }
            else if (bestClearance < clearance)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "малый запас " + (bestClearance * 1000f).ToString("0") + " мм";
            }
            else if (Mathf.Abs(zError) > 0.005f)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "Z-ход ограничен (" + (zError * 1000f).ToString("0") + " мм)";
            }
            else
            {
                res.verdict = ReachVerdict.Safe;
                res.reason = "запас " + (bestClearance * 1000f).ToString("0") + " мм";
            }
            return res;
        }

        // ------------------------------------------------------------- 6-осевой

        private ReachResult QuerySixAxis(Vector3 point)
        {
            var res = new ReachResult();
            if (six == null || six.jointTransforms == null || six.jointTransforms.Length < 6)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "6-осевой: нет суставов";
                return res;
            }

            // Быстрый отсев по габариту рабочей зоны (относительно базы).
            Transform b = six.baseTransform != null ? six.baseTransform : six.transform;
            float dist = Vector3.Distance(point, b.position);
            if (dist > workZoneRadius)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "вне рабочей зоны (далеко)";
                return res;
            }
            if (dist < bodyZoneRadius)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "внутри корпуса робота";
                return res;
            }

            Transform tip = six.tcp != null ? six.tcp : six.endEffector;
            if (tip == null)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "нет TCP";
                return res;
            }

            // Сохраняем позу, решаем IK в реальных трансформах, затем ВСЕГДА возвращаем.
            //
            // P1-12 (11.11.2026). ЧТО БЫЛО: восстановление позы стояло ПРОСТО ПОСЛЕ вызова
            // `SolveCCD`, без `try/finally`. Любое исключение внутри расчёта (а он ходит по
            // настоящим трансформам и мировым матрицам) оставляло робота В ПОЗЕ, КОТОРУЮ ПОСЧИТАЛ
            // ОРАКУЛ. ЧЕМ ЭТО ГРОЗИЛО: «проверка достижимости» двигала настоящего робота и не
            // возвращала его — оператор видел рывок без команды, а предохранители считали зазоры
            // по этой случайной позе.
            //
            // ЗАОДНО: условие `err &gt; 0.03f || !ok &amp;&amp; err &gt; 0.03f` было тавтологией —
            // признак сходимости `ok` не влиял ни на что. Теперь `!ok` сам по себе означает отказ.
            Quaternion[] saved = new Quaternion[six.jointTransforms.Length];
            for (int i = 0; i < saved.Length; i++)
                saved[i] = six.jointTransforms[i] != null ? six.jointTransforms[i].localRotation : Quaternion.identity;

            bool ok = false;
            float err = float.MaxValue;
            float clr = -1f;
            bool nearLimit = false;
            bool wristSingular = false;
            try
            {
                ok = DHInverse.SolveCCD(six.jointTransforms, six.jointAxesLocal, tip, point,
                    ccdIterations, ccdTolerance);
                err = Vector3.Distance(tip.position, point);
                res.tcp = tip.position;

                // Капсульная цепочка по суставам + инструмент.
                var nodes = new List<Vector3>();
                foreach (Transform j in six.jointTransforms)
                {
                    if (j == null) continue;
                    Vector3 p = j.position;
                    if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < 1e-6f) continue;
                    nodes.Add(p);
                }
                nodes.Add(tip.position);
                clr = world.MinDistanceChain(nodes, linkRadius);

                // Лимиты и сингулярности по фактическим углам.
                float[] angles = six.GetJointAngles();
                if (angles != null && six.jointLimits != null)
                {
                    for (int i = 0; i < angles.Length && i < six.jointLimits.Length; i++)
                    {
                        if (angles[i] < six.jointLimits[i].x + 5f || angles[i] > six.jointLimits[i].y - 5f)
                        { nearLimit = true; break; }
                    }
                }
                wristSingular = angles != null && angles.Length > 4 && Mathf.Abs(angles[4]) < 8f;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Reachability] проверка достижимости прервана (" +
                                 e.GetType().Name + ": " + e.Message + ") — поза робота возвращена");
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "проверка прервана";
                res.clearance = -1f;
                return res;
            }
            finally
            {
                // ВОЗВРАТ ПОЗЫ ВСЕГДА — даже при исключении (ради этого и заведён try/finally).
                for (int i = 0; i < saved.Length; i++)
                    if (six.jointTransforms[i] != null) six.jointTransforms[i].localRotation = saved[i];
            }

            res.clearance = clr;
            if (!ok || err > 0.03f)
            {
                res.verdict = ReachVerdict.Unreachable;
                res.reason = "IK не сходится (ошибка " + (err * 1000f).ToString("0") + " мм)";
            }
            else if (clr < 0f)
            {
                res.verdict = ReachVerdict.Collision;
                res.reason = "пересечение со сценой";
            }
            else if (clr < clearance)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "малый запас " + (clr * 1000f).ToString("0") + " мм";
            }
            else if (nearLimit)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "близко к лимиту сустава";
            }
            else if (wristSingular)
            {
                res.verdict = ReachVerdict.Marginal;
                res.reason = "близко к сингулярности запястья";
            }
            else
            {
                res.verdict = ReachVerdict.Safe;
                res.reason = "запас " + (clr * 1000f).ToString("0") + " мм";
            }
            return res;
        }
    }
}
