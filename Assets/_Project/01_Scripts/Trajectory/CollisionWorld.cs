using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>Капсула препятствия: отрезок A–B + радиус.</summary>
    public struct ObstacleCapsule
    {
        public Vector3 a;
        public Vector3 b;
        public float r;
        public string name;
        public bool support;   // опора робота (стол, на котором он стоит) — не считается столкновением
    }

    /// <summary>Осевой бокс препятствия (столешницы, плиты, ящики).</summary>
    public struct ObstacleBox
    {
        public Vector3 center;
        public Vector3 half;
        public string name;
        public bool support;
        /// <summary>
        /// ЭТАП 13 ТЗ: поворот бокса (низкополигональный прокси-меш). По умолчанию —
        /// единичный кватернион, то есть прежнее поведение «осевой бокс»: поле добавлено
        /// так, чтобы уже существующая геометрия сцены считалась ровно как раньше.
        /// </summary>
        public Quaternion rotation;
    }

    /// <summary>
    /// РЕЕСТР ПРОКСИ СТОЛКНОВЕНИЙ (ЭТАП 13 ТЗ: «low-poly collision mesh»).
    ///
    /// Оптимизатор (`KvCollisionOptimizer` в модуле функций) считает по мешам сцены
    /// упрощённые прокси — ориентированные боксы и капсулы — и кладёт их сюда. Мир
    /// столкновений спрашивает реестр при пересборке и, если прокси есть, использует его
    /// ВМЕСТО габарита меша: повёрнутые и вытянутые детали перестают «раздуваться» до
    /// осевого габарита, поэтому исчезают ложные столкновения и планировщик находит путь
    /// там, где раньше отказывался.
    ///
    /// ВАЖНО ПРО БЕЗОПАСНОСТЬ: прокси строится по ВСЕМ вершинам меша, поэтому он всегда
    /// накрывает исходную геометрию (никогда не «срезает» её). Отключается одним флагом:
    /// при `Enabled = false` мир собирается как раньше, по габаритам мешей.
    /// </summary>
    public static class CollisionProxies
    {
        /// <summary>Использовать прокси при пересборке мира (флаг ставит оптимизатор).</summary>
        public static bool Enabled;

        private static readonly Dictionary<Renderer, ObstacleBox> boxes =
            new Dictionary<Renderer, ObstacleBox>();
        private static readonly Dictionary<Renderer, ObstacleCapsule> capsules =
            new Dictionary<Renderer, ObstacleCapsule>();
        private static readonly HashSet<Renderer> skipped = new HashSet<Renderer>();

        public static int BoxCount { get { return boxes.Count; } }
        public static int CapsuleCount { get { return capsules.Count; } }
        public static int SkippedCount { get { return skipped.Count; } }
        public static int Count { get { return boxes.Count + capsules.Count + skipped.Count; } }

        public static void Clear()
        {
            boxes.Clear();
            capsules.Clear();
            skipped.Clear();
        }

        /// <summary>
        /// СБРОС СТАТИЧЕСКИХ СЛОВАРЕЙ МЕЖДУ ПРОГОНАМИ (P2-9, 11.11.2026).
        ///
        /// ЧТО БЫЛО. Словари ключуются `Renderer`-ами СЦЕНЫ, а `Clear()` звался только из
        /// `KvCollisionOptimizer`. Ни выход из PlayMode, ни смена сцены их не чистили.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Между прогонами (домен не перезагружается) в словарях оставались
        /// ссылки на уничтоженные рендереры: `KvCollisionOptimizer` считал объекты
        /// «уже размеченными», прокси-геометрия для НОВОЙ сцены не считалась — предохранитель
        /// зазоров молча работал по пустому набору, то есть «можно» там, где нельзя.
        ///
        /// ТЕПЕРЬ словари очищаются в самой ранней точке старта — до первого `Awake` сцены.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            boxes.Clear();
            capsules.Clear();
            skipped.Clear();
        }

        public static void SetBox(Renderer r, ObstacleBox box)
        {
            if (r == null) return;
            skipped.Remove(r);
            capsules.Remove(r);
            boxes[r] = box;
        }

        public static void SetCapsule(Renderer r, ObstacleCapsule capsule)
        {
            if (r == null) return;
            skipped.Remove(r);
            boxes.Remove(r);
            capsules[r] = capsule;
        }

        /// <summary>Пометить объект как «не препятствие» (мелкая деталь внутри другого объёма).</summary>
        public static void SetSkipped(Renderer r)
        {
            if (r == null) return;
            boxes.Remove(r);
            capsules.Remove(r);
            skipped.Add(r);
        }

        /// <summary>Что делать с этим рендерером: бокс / капсула / пропустить / нет прокси.</summary>
        public static bool TryGet(Renderer r, out ObstacleBox box, out ObstacleCapsule capsule,
            out bool isBox, out bool isSkipped)
        {
            box = default(ObstacleBox);
            capsule = default(ObstacleCapsule);
            isBox = false;
            isSkipped = false;
            if (r == null) return false;
            if (skipped.Contains(r)) { isSkipped = true; return true; }
            if (boxes.TryGetValue(r, out box)) { isBox = true; return true; }
            if (capsules.TryGetValue(r, out capsule)) { isBox = false; return true; }
            return false;
        }

        /// <summary>Сколько прокси сейчас в реестре (диагностика).</summary>
        public static string Status()
        {
            return "прокси: боксов " + boxes.Count + " · капсул " + capsules.Count +
                   " · пропущено " + skipped.Count + (Enabled ? " · включены" : " · выключены");
        }
    }

    /// <summary>
    /// Мир столкновений (E0/E1 плана): упрощённая капсульная модель окружения.
    /// * статика сцены (столы, детали) — боксы по габаритам мешей (вытянутые — капсулы);
    /// * пол — горизонтальная плоскость (по самой крупной плоской поверхности);
    /// * другие роботы — капсульные цепочки по их суставам;
    /// * очень крупные объекты (ангар/стены) игнорируются (далеко от рабочей зоны),
    ///   НО детали стендов (`Стенд_*`: столешницы 14.4 × 9.6 и ножки) — исключение:
    ///   они всегда остаются препятствиями и никогда не считаются «полом».
    /// Расстояния считаются по отрезкам (segment-segment), без физики Unity.
    /// </summary>
    public class CollisionWorld
    {
        public readonly List<ObstacleCapsule> Capsules = new List<ObstacleCapsule>();
        public readonly List<ObstacleBox> Boxes = new List<ObstacleBox>();
        public float FloorY = float.NegativeInfinity;
        public uint Version { get; private set; }

        private const float MaxObjectSize = 6f;   // крупнее — считаем фоном (ангар/стены)
        private const float MinObjectSize = 0.02f;
        private const string StandPrefix = "Стенд_";  // корни стендов (см. StandBuilder)

        /// <summary>
        /// Имя куба pick-and-place. Куб — ЦЕЛЬ захвата, а не препятствие: если считать его
        /// препятствием, подход к нему отбраковывается SafetyGate по зазору («малый зазор»)
        /// и демонстрация не выполняется вовсе.
        /// </summary>
        public const string PickAndPlaceCubeName = "Куб_PickAndPlace";

        /// <summary>Пересобрать мир. basePos — позиция базы робота (для определения опоры).</summary>
        public void Rebuild(RobotController skipRobot, float linkRadius = 0.06f)
        {
            Capsules.Clear();
            Boxes.Clear();
            FloorY = float.NegativeInfinity;
            Version++;

            Vector3 basePos = skipRobot != null ? skipRobot.transform.position : Vector3.zero;

            // 1. Статика сцены.
            Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude);
            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.Contains("Phantom") || n.Contains("Preview") || n.Contains("Laser") ||
                    n.Contains("IlyichLamp") || n.Contains("AimMarker") || n.Contains("TCP") ||
                    n.Contains("GhostRunner") || n.Contains("GhostWorstClearance") ||
                    n.Contains("GhostTrajectory"))
                    continue;
                // Цель захвата (куб pick-and-place) — НЕ препятствие: робот обязан к нему
                // подъехать, иначе SafetyGate отклоняет каждый шаг подхода по зазору
                // («малый зазор») и демонстрация не идёт вовсе. Пока куб в захвате, он и так
                // не попадает сюда — он ребёнок TCP робота.
                if (n.StartsWith(PickAndPlaceCubeName, System.StringComparison.Ordinal)) continue;
                // ЗОНЫ ЗАПРЕТА — тоже НЕ препятствия: они проверяются ОТДЕЛЬНО
                // (`KvZoneService.CheckPlan` по цепочке звеньев) и задуманы как «пометка/отбраковка»,
                // а не как геометрия. Но мир столкновений собирается по РЕНДЕРЕРАМ, а не по
                // коллайдерам, поэтому полупрозрачный объём зоны молча становился препятствием —
                // планировщик отказывался строить ЛЮБЫЕ траектории рядом с зоной.
                if (IsKeepOutZone(r.transform)) continue;
                // Служебная визуализация (фантомы, «колбаски» траекторий) скрыта из иерархии
                // и в мир столкновений не входит — иначе робот «упирается» в собственные фантомы.
                if (IsServiceObject(r.transform)) continue;
                if (r.GetComponentInParent<RobotController>() != null) continue; // роботы отдельно

                Bounds b = r.bounds;
                Vector3 s = b.size;
                if (s.magnitude < MinObjectSize) continue;

                // Детали СТЕНДОВ (столешницы и ножки) всегда учитываются как препятствия-боксы,
                // даже когда объект крупнее MaxObjectSize. Иначе столешница 14.4 × 9.6 (столы
                // увеличены в 3 раза, ТЗ 13.09.2026) попадала в правило «пол» и поднимала FloorY
                // до 0.98 — а тогда точка ровно на поверхности стола получала зазор
                // (0.98 − 0.98 − linkRadius 0.06) < 0 и планировщик отказывался строить путь.
                bool underStand = IsUnderStand(r.transform);

                if (!underStand && Mathf.Max(s.x, Mathf.Max(s.y, s.z)) > MaxObjectSize)
                {
                    // Пол: низкий и очень широкий — учитываем как плоскость.
                    if (s.y < 1f && s.x > 8f && s.z > 8f)
                        FloorY = Mathf.Max(FloorY, b.max.y);
                    continue; // стены/потолок ангара игнорируем
                }

                // Плиты/столешницы (одна сторона много меньше двух других) — бокс;
                // вытянутые тела — капсула.
                float min = Mathf.Min(s.x, Mathf.Min(s.y, s.z));
                float max = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
                bool slab = min < 0.35f * max;

                // ЭТАП 13 ТЗ: низкополигональный прокси (если оптимизатор его посчитал).
                // Прокси накрывает меш целиком, поэтому замена безопасна, а для повёрнутых
                // и вытянутых деталей она ещё и ТОЧНЕЕ габарита (нет «раздувания» до AABB).
                if (CollisionProxies.Enabled)
                {
                    ObstacleBox pb;
                    ObstacleCapsule pc;
                    bool isBox, isSkipped;
                    if (CollisionProxies.TryGet(r, out pb, out pc, out isBox, out isSkipped))
                    {
                        if (isSkipped) continue;
                        if (isBox)
                        {
                            bool supportProxy = b.max.y <= basePos.y + 0.03f &&
                                                b.max.y >= basePos.y - 0.4f &&
                                                Mathf.Abs(b.center.x - basePos.x) <= b.extents.x + 0.25f &&
                                                Mathf.Abs(b.center.z - basePos.z) <= b.extents.z + 0.25f;
                            pb.support = supportProxy;
                            Boxes.Add(pb);
                        }
                        else
                        {
                            pc.support = false;
                            Capsules.Add(pc);
                        }
                        continue;
                    }
                }

                if (slab)
                {
                    bool support = b.max.y <= basePos.y + 0.03f && b.max.y >= basePos.y - 0.4f &&
                                   Mathf.Abs(b.center.x - basePos.x) <= b.extents.x + 0.25f &&
                                   Mathf.Abs(b.center.z - basePos.z) <= b.extents.z + 0.25f;
                    Boxes.Add(new ObstacleBox
                    {
                        center = b.center,
                        half = b.extents,
                        name = n,
                        support = support
                    });
                }
                else
                {
                    ObstacleCapsule c = MakeCapsule(b, n);
                    c.support = false;
                    Capsules.Add(c);
                }
            }

            // 2. Другие роботы — цепочки по суставам.
            RobotController[] robots = Object.FindObjectsByType<RobotController>(FindObjectsInactive.Exclude);
            foreach (RobotController rc in robots)
            {
                if (rc == null || rc == skipRobot) continue;

                // РУКИ ОДНОГО ДВУРУКОГО РОБОТА — НЕ ПРЕПЯТСТВИЯ ДРУГ ДЛЯ ДРУГА (02.11.2026).
                //
                // Владелец решил разделить двурукого на ОТДЕЛЬНЫЕ модели рук на общем
                // кронштейне. Формально это два робота — и мир столкновений начал бы считать
                // левую руку препятствием для правой. Тогда ЛЮБАЯ траектория одной руки
                // отклонялась бы планировщиком из-за «малого зазора» с другой рукой, и двурукий
                // не смог бы работать вовсе.
                //
                // ПРАВИЛО: если у двух роботов ОБЩИЙ РОДИТЕЛЬ (кронштейн), они «свои» и друг
                // для друга препятствием не считаются. Для независимых роботов поведение
                // прежнее: они друг друга видят, и это правильно — два манипулятора на одном
                // столе действительно могут столкнуться.
                if (skipRobot != null && rc.transform.parent != null &&
                    rc.transform.parent == skipRobot.transform.parent)
                    continue;

                AddRobotChain(rc, linkRadius);
            }
        }

        /// <summary>
        /// Служебный объект визуализации: сам или любой его предок скрыт из иерархии
        /// (HideFlags.HideInHierarchy). Такие объекты не являются препятствиями.
        /// </summary>
        private static bool IsServiceObject(Transform t)
        {
            for (Transform c = t; c != null; c = c.parent)
                if ((c.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) return true;
            return false;
        }

        /// <summary>Деталь стенда: сам объект или любой его предок — корень `Стенд_*`.</summary>
        private static bool IsUnderStand(Transform t)
        {
            for (Transform c = t; c != null; c = c.parent)
                if (c.name.StartsWith(StandPrefix, System.StringComparison.Ordinal)) return true;
            return false;
        }

        /// <summary>Объект зоны запрета (`Зона_*` или её тело `ZoneBody`) — не геометрия мира.</summary>
        private static bool IsKeepOutZone(Transform t)
        {
            for (Transform c = t; c != null; c = c.parent)
                if (c.name.StartsWith("Зона_", System.StringComparison.Ordinal) ||
                    c.name.StartsWith("ZoneBody", System.StringComparison.Ordinal)) return true;
            return false;
        }

        private static ObstacleCapsule MakeCapsule(Bounds b, string name)        {
            Vector3 s = b.size;
            // Ось — самое длинное измерение; радиус — по двум другим (консервативно, по максимуму).
            int axis = 0;
            if (s.y >= s.x && s.y >= s.z) axis = 1;
            else if (s.z >= s.x && s.z >= s.y) axis = 2;

            Vector3 dir = axis == 0 ? Vector3.right : (axis == 1 ? Vector3.up : Vector3.forward);
            float half = s[axis] * 0.5f;
            float rOther = Mathf.Max(0.01f, 0.5f * Mathf.Max(
                axis == 0 ? s.y : s.x,
                axis == 2 ? s.y : s.z));

            return new ObstacleCapsule
            {
                a = b.center - dir * half,
                b = b.center + dir * half,
                r = rOther,
                name = name
            };
        }

        private void AddRobotChain(RobotController rc, float linkRadius)
        {
            Transform[] joints = null;
            if (rc is SixAxisController six) joints = six.jointTransforms;
            else if (rc is SCARAController sc) joints = new[] { sc.joint1, sc.joint2, sc.joint3 };

            if (joints == null) return;
            var nodes = new List<Vector3>();
            foreach (Transform j in joints)
            {
                if (j == null) continue;
                Vector3 p = j.position;
                if (nodes.Count > 0 && (p - nodes[nodes.Count - 1]).sqrMagnitude < 1e-6f) continue;
                nodes.Add(p);
            }
            for (int i = 0; i + 1 < nodes.Count; i++)
            {
                Capsules.Add(new ObstacleCapsule
                {
                    a = nodes[i],
                    b = nodes[i + 1],
                    r = linkRadius,
                    name = rc.robotName + "_link" + i
                });
            }
        }

        /// <summary>Минимальное расстояние от точки до мира (пол + капсулы + боксы).</summary>
        public float DistanceToPoint(Vector3 p)
        {
            float best = FloorY > float.NegativeInfinity ? Mathf.Max(0f, p.y - FloorY) : float.MaxValue;
            foreach (ObstacleCapsule c in Capsules)
            {
                if (c.support) continue;
                best = Mathf.Min(best, DistancePointSegment(p, c.a, c.b) - c.r);
            }
            foreach (ObstacleBox b in Boxes)
            {
                if (b.support) continue;
                best = Mathf.Min(best, PointBoxDistance(p, b));
            }
            return best;
        }

        /// <summary>
        /// Минимальный зазор между капсульной цепочкой робота и миром.
        ///
        /// ФИКС §27 — С КАКОГО ОТРЕЗКА НАЧИНАТЬ. Прежнее правило «первое звено (база) крепится
        /// к опоре, его столкновения считаются нормой» было ЖЁСТКО зашито как
        /// `nodes.Count > 2 ? 1 : 0` и применялось к ЛЮБОМУ роботу. У 6-осевого это верно:
        /// нулевой отрезок — колонна базы (Axis1→Axis2), стоящая на столе-опоре. Но у SCARA
        /// нулевой отрезок — это РЕАЛЬНОЕ ПЛЕШО (J1→J2, ≈0.33 м), и он не проверялся против
        /// препятствий ВООБЩЕ: ни в `PoseValidator.ClearanceAt`, ни в `ReachabilityOracle`.
        /// Отсюда «не на всех роботах работает грамотно коллизия» — плечо SCARA проходило
        /// сквозь столбы, стойки и второй стенд, а система видела «запас достаточный».
        ///
        /// Теперь отрезок задаёт ВЫЗЫВАЮЩИЙ (он знает тип робота):
        ///   * <paramref name="firstSegment"/> = −1 (по умолчанию) — прежнее поведение
        ///     (пропускать первый отрезок у цепочки длиннее двух узлов);
        ///   * 0 — проверять ВСЕ отрезки (так вызывается SCARA: оба её звена рабочие).
        /// </summary>
        public float MinDistanceChain(IList<Vector3> nodes, float linkRadius, int firstSegment = -1)
        {
            float best = float.MaxValue;

            // против пола
            if (FloorY > float.NegativeInfinity)
                for (int i = 0; i + 1 < nodes.Count; i++)
                    best = Mathf.Min(best, Mathf.Min(nodes[i].y, nodes[i + 1].y) - FloorY - linkRadius);

            // первое звено (база) крепится к опоре — его столкновения считаются нормой.
            // −1 = «решить здесь по прежнему правилу»; 0 = проверять всё (см. описание выше).
            if (firstSegment < 0) firstSegment = nodes.Count > 2 ? 1 : 0;
            firstSegment = Mathf.Clamp(firstSegment, 0, Mathf.Max(0, nodes.Count - 2));

            for (int i = firstSegment; i + 1 < nodes.Count; i++)
            {
                for (int k = 0; k < Capsules.Count; k++)
                {
                    ObstacleCapsule c = Capsules[k];
                    if (c.support) continue;
                    float d = SegmentSegmentDistance(nodes[i], nodes[i + 1], c.a, c.b) - linkRadius - c.r;
                    if (d < best) best = d;
                }
                for (int k = 0; k < Boxes.Count; k++)
                {
                    ObstacleBox bx = Boxes[k];
                    if (bx.support) continue;
                    float d = SegmentBoxDistance(nodes[i], nodes[i + 1], bx) - linkRadius;
                    if (d < best) best = d;
                }
            }
            return best;
        }

        /// <summary>Точное расстояние точка–бокс (с учётом поворота прокси-бокса, этап 13).</summary>
        public static float PointBoxDistance(Vector3 p, ObstacleBox b)
        {
            Vector3 d;
            if (b.rotation.x == 0f && b.rotation.y == 0f && b.rotation.z == 0f &&
                b.rotation.w == 1f)
            {
                d = new Vector3(
                    Mathf.Abs(p.x - b.center.x) - b.half.x,
                    Mathf.Abs(p.y - b.center.y) - b.half.y,
                    Mathf.Abs(p.z - b.center.z) - b.half.z);
            }
            else
            {
                // Переводим точку в систему бокса: там он снова осевой.
                Vector3 local = Quaternion.Inverse(b.rotation) * (p - b.center);
                d = new Vector3(
                    Mathf.Abs(local.x) - b.half.x,
                    Mathf.Abs(local.y) - b.half.y,
                    Mathf.Abs(local.z) - b.half.z);
            }
            Vector3 outside = new Vector3(Mathf.Max(d.x, 0f), Mathf.Max(d.y, 0f), Mathf.Max(d.z, 0f));
            float outsideDist = outside.magnitude;
            float insideDist = Mathf.Min(Mathf.Max(d.x, Mathf.Max(d.y, d.z)), 0f);
            return outsideDist + insideDist;
        }

        /// <summary>Расстояние отрезок–бокс (сэмплирование отрезка).</summary>
        public static float SegmentBoxDistance(Vector3 a, Vector3 b, ObstacleBox box)
        {
            const int samples = 7;
            float best = float.MaxValue;
            for (int i = 0; i <= samples; i++)
            {
                Vector3 p = Vector3.Lerp(a, b, (float)i / samples);
                best = Mathf.Min(best, PointBoxDistance(p, box));
            }
            return best;
        }

        // ------------------------------------------------------------- геометрия

        public static float DistancePointSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.sqrMagnitude;
            if (len2 < 1e-9f) return (p - a).magnitude;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / len2);
            return (p - (a + ab * t)).magnitude;
        }

        /// <summary>Минимальное расстояние между двумя отрезками (3D).</summary>
        public static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
        {
            Vector3 u = p2 - p1, v = q2 - q1, w = p1 - q1;
            float a = Vector3.Dot(u, u), b = Vector3.Dot(u, v), c = Vector3.Dot(v, v);
            float d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
            float denom = a * c - b * b;
            float sN, sD = denom, tN, tD = denom;

            if (denom < 1e-6f) { sN = 0f; sD = 1f; tN = e; tD = c; }
            else
            {
                sN = b * e - c * d;
                tN = a * e - b * d;
                if (sN < 0f) { sN = 0f; tN = e; tD = c; }
                else if (sN > sD) { sN = sD; tN = e + b; tD = c; }
            }

            if (tN < 0f)
            {
                tN = 0f;
                if (-d < 0f) sN = 0f;
                else if (-d > a) sN = sD;
                else { sN = -d; sD = a; }
            }
            else if (tN > tD)
            {
                tN = tD;
                if (-d + b < 0f) sN = 0f;
                else if (-d + b > a) sN = sD;
                else { sN = (-d + b); sD = a; }
            }

            float sc = Mathf.Abs(sN) < 1e-6f ? 0f : sN / sD;
            float tc = Mathf.Abs(tN) < 1e-6f ? 0f : tN / tD;
            return (w + sc * u - tc * v).magnitude;
        }
    }
}
