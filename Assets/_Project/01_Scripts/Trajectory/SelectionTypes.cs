using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Состояния сценария (новый алгоритм подтверждения траектории) + режим перемещения точки.
    /// </summary>
    public enum FlowState
    {
        Idle,                // шаг 0: точка не подтверждена — ничего не считаем и не двигаем
        PointSelected,       // шаг 1: точка подтверждена красным лазером, варианты ещё считаются
        TrajectoriesShown,   // шаг 2: показаны ВСЕ траектории, и по КАЖДОЙ едет свой фантом
        PhantomsMoving,      // шаги 3–4: траектория выбрана (остался один фантом), ждём подтверждения
        RobotMoving,         // шаг 5: реальный робот едет по выбранной траектории (нажатия игнорируются)
        PointMoveMode        // РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ: QWEASD двигают точку в мире, Enter/Esc — выход
    }

    /// <summary>Кандидат-траектория: геометрия для «колбаски», метрики и стоимость.</summary>
    public class TrajectoryCandidate
    {
        public int id;
        public string label = "";
        public PlannedTrajectory plan;      // q(t) + времена
        public Vector3[] tube;              // полилиния TCP для визуала и выбора
        public Color color;                 // СВОЙ оттенок варианта (оранжево-жёлтая гамма, ТЗ 8 траекторий)
        public float lengthM;               // длина пути TCP, м
        public float timeS;                 // время по параметризации, с
        public float minClearance;          // минимальный зазор, м
        public float limitMarginDeg;        // запас до лимитов, град
        public double score;                // S — меньше лучше
        public bool safe;                   // прошла SafetyGate
        public string why = "";             // причина отказа/предупреждения
        public TrajectoryTube view;         // визуал (создаётся TrajectoryTube)
    }

    /// <summary>Вариант конфигурации робота (решение IK) для фантома.</summary>
    public struct PhantomConfig
    {
        public int index;
        public double[] q;
        public IkBranchTag tag;
        public double cost;        // стоимость позы (PostureSelector)
        public float sigmaMin;     // м/град
        public float fkError;      // ошибка прямой задачи, м
        public GameObject ghost;   // визуал
        public Color tint;         // оттенок бирюзового (различимость конфигураций)
        public string Label => "Фантом " + (index + 1) + " [" + tag + "]";
    }

    /// <summary>Текущее состояние выбора (единая точка правды для UI и логики).</summary>
    public class SelectionState
    {
        public FlowState phase = FlowState.Idle;
        public Vector3 point;                 // зафиксированная точка (цель TCP)
        public bool hasPoint;
        public Vector3 aimAtLock;             // прицел в момент фиксации точки
        public int hoveredTrajectory = -1;
        public int selectedTrajectory = -1;
        public int hoveredPhantom = -1;
        public int selectedPhantom = -1;
        public readonly List<TrajectoryCandidate> candidates = new List<TrajectoryCandidate>();
        public readonly List<PhantomConfig> phantoms = new List<PhantomConfig>();

        // --- режим перемещения точки (PointMoveMode) ---
        public Vector3 movePoint;             // текущая позиция перемещаемой точки (цель TCP)
        public Vector3 moveOrigin;            // позиция на входе в режим (для отмены по Esc)
        public ReachVerdict moveVerdict = ReachVerdict.Safe;   // онлайн-вердикт оракула
        public string moveReason = "";        // причина вердикта (для HUD/подсказки)
        public bool moveVerdictValid;         // был ли уже получен вердикт для текущей позиции

        public bool InMoveMode => phase == FlowState.PointMoveMode;

        public void ResetTrajectorySelection()
        {
            selectedTrajectory = -1;
            hoveredTrajectory = -1;
            selectedPhantom = -1;
            hoveredPhantom = -1;
            phantoms.Clear();
        }

        public void ClearAll()
        {
            ResetTrajectorySelection();
            candidates.Clear();
            hasPoint = false;
            phase = FlowState.Idle;
            movePoint = Vector3.zero;
            moveOrigin = Vector3.zero;
            moveVerdictValid = false;
            moveReason = "";
        }
    }

    /// <summary>Геометрия выбора: расстояние «луч ↔ трубка траектории» (без коллайдеров).</summary>
    public static class TubeMath
    {
        /// <summary>
        /// Минимальное расстояние от луча до полилинии; возвращает также параметр вдоль луча
        /// (для проверки перекрытия по глубине) и индекс ближайшего сегмента.
        /// </summary>
        public static float DistanceRayPolyline(Ray ray, Vector3[] poly, out float rayParam, out int segIndex)
        {
            rayParam = 0f;
            segIndex = -1;
            if (poly == null || poly.Length < 2) return float.MaxValue;

            float best = float.MaxValue;
            Vector3 far = ray.origin + ray.direction * 1000f;
            for (int i = 0; i + 1 < poly.Length; i++)
            {
                float d = SegmentSegmentDistance(ray.origin, far, poly[i], poly[i + 1]);
                if (d < best)
                {
                    best = d;
                    segIndex = i;
                    // проекция ближайшей точки сегмента на луч — грубая, но достаточная оценка
                    Vector3 mid = (poly[i] + poly[i + 1]) * 0.5f;
                    rayParam = Vector3.Dot(mid - ray.origin, ray.direction);
                }
            }
            return best;
        }

        /// <summary>
        /// РАССТОЯНИЕ МЕЖДУ ОТРЕЗКАМИ — ВЫЗОВ ЕДИНСТВЕННОЙ РЕАЛИЗАЦИИ (P2-14, 11.11.2026).
        ///
        /// ЧТО БЫЛО. Здесь лежала построчная КОПИЯ `CollisionWorld.SegmentSegmentDistance`.
        /// Две реализации одной геометрии — это две возможности разойтись: правку в одной
        /// (например, исправление вырожденного случая) никто не перенесёт во вторую, и тогда
        /// выбор траектории лучом и проверка зазоров будут считать расстояния по-разному.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. «Луч выбрал траекторию, которая по проверке зазоров пересекает стол» —
        /// и оба расчёта формально верны, потому что считают разное. Разбор такого случая стоит
        /// дня, а виноват дубль.
        ///
        /// ТЕПЕРЬ копия удалена как расчёт, а метод оставлен обёрткой: его зовут тесты и внешние
        /// места, и ломать их ради удаления имени незачем.
        /// </summary>
        public static float SegmentSegmentDistance(Vector3 p1, Vector3 p2, Vector3 q1, Vector3 q2)
        {
            return CollisionWorld.SegmentSegmentDistance(p1, p2, q1, q2);
        }

        /// <summary>Длина полилинии, м.</summary>
        public static float PolylineLength(Vector3[] poly)
        {
            if (poly == null || poly.Length < 2) return 0f;
            float len = 0f;
            for (int i = 0; i + 1 < poly.Length; i++) len += Vector3.Distance(poly[i], poly[i + 1]);
            return len;
        }
    }

    /// <summary>Немного геометрии для фантомов: выбор «максимально разных» ветвей.</summary>
    public static class PhantomMath
    {
        /// <summary>
        /// Жадный выбор МАКСИМАЛЬНО РАЗНЕСЁННЫХ по конфигурации ветвей (farthest-point sampling):
        /// первая — лучшая по стоимости, далее — та, что дальше всех от уже выбранных.
        /// Даёт визуально различимые фантомы (elbow up/down, shoulder left/right, wrist flip).
        /// </summary>
        public static List<int> PickDistinct(List<IkSolution> solutions, double[] ranges, int count)
        {
            var result = new List<int>();
            if (solutions == null || solutions.Count == 0) return result;

            int bestStart = 0;
            for (int i = 1; i < solutions.Count; i++)
                if (solutions[i].withinLimits && !solutions[bestStart].withinLimits) bestStart = i;
            result.Add(bestStart);

            while (result.Count < Mathf.Min(count, solutions.Count))
            {
                int next = -1;
                double bestDist = -1;
                for (int i = 0; i < solutions.Count; i++)
                {
                    if (result.Contains(i)) continue;
                    double minDist = double.MaxValue;
                    foreach (int r in result)
                        minDist = System.Math.Min(minDist, ConfigDistance(solutions[i].q, solutions[r].q, ranges));
                    // предпочитаем далёкие, но валидные
                    double score = minDist + (solutions[i].withinLimits ? 0.0 : -10.0);
                    if (score > bestDist) { bestDist = score; next = i; }
                }
                if (next < 0) break;
                result.Add(next);
            }
            return result;
        }

        public static double ConfigDistance(double[] a, double[] b, double[] ranges)
        {
            if (a == null || b == null) return 0;
            double s = 0;
            int n = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                double r = ranges != null && i < ranges.Length && ranges[i] > 1e-6 ? ranges[i] : 1.0;
                double d = (a[i] - b[i]) / r;
                s += d * d;
            }
            return System.Math.Sqrt(s);
        }
    }

    /// <summary>
    /// Детерминированный порядок объектов: путь в иерархии сцены (как в окне Hierarchy).
    /// Нужен, чтобы выбор робота/узлов дерева не «мигал» от кадра к кадру
    /// (FindObjectsByType не гарантирует порядок).
    /// </summary>
    public static class HierarchyOrder
    {
        public static string PathOf(Transform t)
        {
            if (t == null) return "";
            var stack = new List<int>();
            for (Transform c = t; c != null; c = c.parent) stack.Add(c.GetSiblingIndex());
            var sb = new System.Text.StringBuilder();
            for (int i = stack.Count - 1; i >= 0; i--) sb.Append(stack[i].ToString("D4")).Append('/');
            return sb.ToString();
        }

        public static int Compare(Transform a, Transform b)
        {
            return string.CompareOrdinal(PathOf(a), PathOf(b));
        }
    }

    /// <summary>Таймирование движения под заданную скорость инструмента (м/с).</summary>
    public static class MotionTiming
    {
        /// <summary>
        /// Пересчитывает времена траектории так, чтобы средняя скорость TCP была speedMps
        /// (например 0.05 м/с = 1 м за 20 с), сохраняя форму профиля.
        /// </summary>
        public static void RescaleToSpeed(PlannedTrajectory t, float lengthM, float speedMps)
        {
            if (t == null || t.Times == null || t.Times.Length == 0) return;
            float total = t.Times[t.Times.Length - 1];
            if (total <= 1e-4f || lengthM <= 1e-4f) return;
            float desired = lengthM / Mathf.Max(0.005f, speedMps);
            float k = desired / total;
            for (int i = 0; i < t.Times.Length; i++) t.Times[i] *= k;
            t.Time = t.Times[t.Times.Length - 1];
        }
    }
}
