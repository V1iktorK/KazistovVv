using UnityEngine;

namespace TrajectoryCore
{
    public enum PrimKind { Sphere, Capsule, Box, Hull }

    /// <summary>Выпуклый примитив с опорной функцией h_K(d) = max_{x∈K} dᵀx.</summary>
    public struct Prim
    {
        public PrimKind kind;
        public Vector3 a, b;      // sphere: a=центр; capsule: a,b — ось; box: a=центр
        public float r;           // sphere/capsule
        public Vector3 half;      // box
        public Vector3[] hull;    // hull

        public static Prim Sphere(Vector3 c, float r)
        { return new Prim { kind = PrimKind.Sphere, a = c, r = r }; }

        public static Prim Capsule(Vector3 a, Vector3 b, float r)
        { return new Prim { kind = PrimKind.Capsule, a = a, b = b, r = r }; }

        public static Prim Box(Vector3 c, Vector3 half)
        { return new Prim { kind = PrimKind.Box, a = c, half = half }; }

        public static Prim Hull(Vector3[] verts)
        { return new Prim { kind = PrimKind.Hull, hull = verts }; }

        public Vector3 Support(Vector3 dir)
        {
            switch (kind)
            {
                case PrimKind.Sphere:
                    return a + dir.normalized * r;
                case PrimKind.Capsule:
                {
                    Vector3 ab = b - a;
                    Vector3 p = Vector3.Dot(dir, ab) >= 0f ? b : a;
                    return p + dir.normalized * r;
                }
                case PrimKind.Box:
                    return a + new Vector3(
                        Mathf.Sign(dir.x) * half.x,
                        Mathf.Sign(dir.y) * half.y,
                        Mathf.Sign(dir.z) * half.z);
                default:
                {
                    if (hull == null || hull.Length == 0) return a;
                    Vector3 best = hull[0];
                    float bestDot = Vector3.Dot(best, dir);
                    for (int i = 1; i < hull.Length; i++)
                    {
                        float d = Vector3.Dot(hull[i], dir);
                        if (d > bestDot) { bestDot = d; best = hull[i]; }
                    }
                    return best;
                }
            }
        }
    }

    /// <summary>
    /// Узкая фаза (Проблема 2): GJK (пересечение выпуклых тел через разность Минковского)
    /// и EPA (глубина проникновения и нормаль контакта).
    /// Работает с любыми выпуклыми примитивами (капсулы, боксы, оболочки звеньев).
    /// </summary>
    public static class NarrowPhase
    {
        private const int MaxIterations = 40;
        private const float Eps = 1e-5f;

        private static Vector3 Support(in Prim A, in Prim B, Vector3 dir)
        {
            return A.Support(dir) - B.Support(-dir);
        }

        /// <summary>Есть ли в векторе NaN или бесконечность (отсев нечисловых данных, урок E-21).</summary>
        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        /// <summary>Все вершины оболочки конечны (оболочка без вершин проверяется отдельно).</summary>
        private static bool IsFinite(Vector3[] hull)
        {
            if (hull == null) return true;              // отсутствие оболочки ловит `IsEmpty`
            for (int i = 0; i < hull.Length; i++)
                if (!IsFinite(hull[i])) return false;
            return true;
        }
        /// <summary>
        /// ПРИМИТИВ БЕЗ ГЕОМЕТРИИ — НЕ ПЕРЕСЕКАЕТ НИЧЕГО (исправлено 30.10.2026, S44).
        ///
        /// ЧТО БЫЛО. Оболочка без вершин (`Prim.Hull(new Vector3[0])`, а также `default`,
        /// который приходит из незаполненного поля) не имеет опорной функции: `Support` возвращает
        /// `a` = (0,0,0). Разность Минковского вырождается в точку в начале координат, GJK видит
        /// «начало координат внутри» и отвечает ПЕРЕСЕЧЕНИЕ.
        ///
        /// ЧЕМ ЭТО ПЛОХО. Оболочка без вершин — это ошибка ДАННЫХ (не загрузилась модель, пустой
        /// массив в инспекторе), а не тело. Отвечая «пересекается», код останавливал бы планирование
        /// с ложным столкновением на каждом шаге — и оператор искал бы причину в роботе.
        /// Обнаружено тестом `KvNarrowPhaseTests.DegeneratePrimitives_DoNotThrow`.
        /// </summary>
        private static bool IsEmpty(in Prim p)
        {
            if (p.kind == PrimKind.Hull) return p.hull == null || p.hull.Length == 0;
            // Сфера и капсула нулевого радиуса — тоже «ничего»: точка в пространстве не образует
            // объёма, и считать её пересекающейся с миром неверно.
            if (p.kind == PrimKind.Sphere || p.kind == PrimKind.Capsule) return p.r <= 0f;
            return false;
        }

        /// <summary>Пересекаются ли тела (GJK).</summary>
        public static bool Intersect(in Prim A, in Prim B)
        {
            // Пустой примитив не пересекается ни с чем — см. `IsEmpty`.
            if (IsEmpty(A) || IsEmpty(B)) return false;

            // ОТСЕВ НЕЧИСЕЛ (добавлено 30.10.2026, S44 — урок E-21).
            //
            // ЧЕМ ЭТО ОПАСНО ЗДЕСЬ. `Support` вызывает `dir.normalized`: для направления с NaN
            // результат тоже NaN, и дальше ВСЕ сравнения в GJK дают `false`:
            //   • `dir.sqrMagnitude < Eps` — не срабатывает (NaN не меньше);
            //   • `Vector3.Dot(p, dir) < 0f` — не срабатывает, то есть «разделяющая ось найдена»
            //     НЕ ОБНАРУЖИВАЕТСЯ;
            //   • цикл доходит до `return true` — «считаем контактом (консервативно)».
            //
            // Итог: тела, которые нигде не соприкасаются, объявляются столкнувшимися, и
            // планировщик отказывает с ложным препятствием. Оператор ищет причину в роботе.
            //
            // ПОЧЕМУ НЕ «ПУСТИТЬ КАК ЕСТЬ»: если геометрия содержит NaN, достоверного ответа нет
            // вообще. Возврат «пересекаются» — это ЛОЖНОЕ препятствие; возврат «не пересекаются» —
            // пропуск настоящего. Выбираем отказ от контакта, потому что ложное препятствие
            // останавливает работу на ровном месте, а исходная причина (NaN в данных) всё равно
            // требует вмешательства.
            if (!IsFinite(A.a) || !IsFinite(B.a)) return false;
            if (A.kind == PrimKind.Capsule && (!IsFinite(A.b) || !IsFinite(B.b))) return false;
            if (A.kind == PrimKind.Box && (!IsFinite(A.half) || !IsFinite(B.half))) return false;
            if (A.kind == PrimKind.Hull && (!IsFinite(A.hull) || !IsFinite(B.hull))) return false;
            if (float.IsNaN(A.r) || float.IsNaN(B.r) || float.IsInfinity(A.r) || float.IsInfinity(B.r))
                return false;

            var simplex = new System.Collections.Generic.List<Vector3>(4);
            Vector3 dir = A.kind == PrimKind.Box ? -B.a : -A.a;
            if (dir.sqrMagnitude < Eps) dir = Vector3.right;

            simplex.Add(Support(A, B, dir));
            dir = -simplex[0];

            for (int it = 0; it < MaxIterations; it++)
            {
                if (dir.sqrMagnitude < Eps) return true; // начало координат внутри
                Vector3 p = Support(A, B, dir);
                if (Vector3.Dot(p, dir) < 0f) return false; // разделяющая ось найдена
                simplex.Add(p);
                if (DoSimplex(simplex, ref dir)) return true;
            }
            return true; // зациклились рядом — считаем контактом (консервативно)
        }

        private static bool DoSimplex(System.Collections.Generic.List<Vector3> s, ref Vector3 dir)
        {
            if (s.Count == 2) return Line(s, ref dir);
            if (s.Count == 3) return Triangle(s, ref dir);
            return Tetrahedron(s, ref dir);
        }

        private static bool Line(System.Collections.Generic.List<Vector3> s, ref Vector3 dir)
        {
            Vector3 a = s[1], b = s[0];           // a — последняя точка
            Vector3 ab = b - a, ao = -a;
            if (Vector3.Dot(ab, ao) > 0f)
                dir = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            else { s.Clear(); s.Add(a); dir = ao; }
            if (dir.sqrMagnitude < Eps) dir = Perp(ab);
            return false;
        }

        private static bool Triangle(System.Collections.Generic.List<Vector3> s, ref Vector3 dir)
        {
            Vector3 a = s[2], b = s[1], c = s[0];
            Vector3 ab = b - a, ac = c - a, ao = -a;
            Vector3 abc = Vector3.Cross(ab, ac);

            if (Vector3.Dot(Vector3.Cross(abc, ac), ao) > 0f)
            {
                if (Vector3.Dot(ac, ao) > 0f) { s.Clear(); s.Add(c); s.Add(a); dir = Vector3.Cross(Vector3.Cross(ac, ao), ac); }
                else return Line2(s, a, b, ref dir);
            }
            else
            {
                if (Vector3.Dot(Vector3.Cross(ab, abc), ao) > 0f)
                    return Line2(s, a, b, ref dir);
                if (Vector3.Dot(abc, ao) > 0f) dir = abc;
                else { s.Clear(); s.Add(b); s.Add(a); s.Add(c); dir = -abc; }
            }
            if (dir.sqrMagnitude < Eps) dir = Perp(ab);
            return false;
        }

        private static bool Line2(System.Collections.Generic.List<Vector3> s, Vector3 a, Vector3 b, ref Vector3 dir)
        {
            Vector3 ab = b - a, ao = -a;
            if (Vector3.Dot(ab, ao) > 0f)
                dir = Vector3.Cross(Vector3.Cross(ab, ao), ab);
            else { s.Clear(); s.Add(a); dir = ao; }
            if (dir.sqrMagnitude < Eps) dir = Perp(ab);
            return false;
        }

        private static bool Tetrahedron(System.Collections.Generic.List<Vector3> s, ref Vector3 dir)
        {
            Vector3 a = s[3], b = s[2], c = s[1], d = s[0];
            Vector3 ao = -a;
            Vector3 abc = Vector3.Cross(b - a, c - a);
            Vector3 acd = Vector3.Cross(c - a, d - a);
            Vector3 adb = Vector3.Cross(d - a, b - a);

            if (Vector3.Dot(abc, ao) > 0f) { s.Clear(); s.Add(c); s.Add(b); s.Add(a); return Triangle(s, ref dir); }
            if (Vector3.Dot(acd, ao) > 0f) { s.Clear(); s.Add(d); s.Add(c); s.Add(a); return Triangle(s, ref dir); }
            if (Vector3.Dot(adb, ao) > 0f) { s.Clear(); s.Add(b); s.Add(d); s.Add(a); return Triangle(s, ref dir); }
            return true; // начало координат внутри тетраэдра
        }

        private static Vector3 Perp(Vector3 v)
        {
            Vector3 t = Mathf.Abs(v.x) < 0.9f ? Vector3.right : Vector3.up;
            Vector3 p = Vector3.Cross(v, t);
            return p.sqrMagnitude < Eps ? Vector3.forward : p.normalized;
        }

        /// <summary>
        /// Глубина проникновения и нормаль. Реализация: минимизация функции перекрытия
        /// f(n) = h_A(n) + h_B(−n) по направлениям (грубая сетка + уточнение спуском),
        /// где h — опорные функции. Для выпуклых тел min f(n) = глубина проникновения.
        /// </summary>
        public static bool Penetration(in Prim A, in Prim B, out float depth, out Vector3 normal)
        {
            depth = 0f;
            normal = Vector3.up;
            if (!Intersect(A, B)) return false;

            float best = float.MaxValue;
            Vector3 bestN = Vector3.up;

            // 1. Грубая сетка направлений (26 направлений куба + оси).
            for (int x = -1; x <= 1; x++)
                for (int y = -1; y <= 1; y++)
                    for (int z = -1; z <= 1; z++)
                    {
                        if (x == 0 && y == 0 && z == 0) continue;
                        Vector3 n = new Vector3(x, y, z).normalized;
                        float f = Support(A, B, n).x * n.x + Support(A, B, n).y * n.y + Support(A, B, n).z * n.z;
                        if (f < best) { best = f; bestN = n; }
                    }

            // 2. Уточнение: локальный спуск по сфере (детерминированный, фиксированные шаги).
            float step = 0.35f;
            for (int it = 0; it < 24; it++)
            {
                bool improved = false;
                Vector3 t1 = Vector3.Cross(bestN, Vector3.up);
                if (t1.sqrMagnitude < 1e-6f) t1 = Vector3.right;
                t1.Normalize();
                Vector3 t2 = Vector3.Cross(bestN, t1).normalized;

                for (int k = 0; k < 4; k++)
                {
                    Vector3 cand = (bestN + (k == 0 ? t1 : k == 1 ? -t1 : k == 2 ? t2 : -t2) * step).normalized;
                    Vector3 sp = Support(A, B, cand);
                    float f = Vector3.Dot(sp, cand);
                    if (f < best) { best = f; bestN = cand; improved = true; }
                }
                if (!improved) step *= 0.5f;
                if (step < 1e-3f) break;
            }

            depth = Mathf.Max(0f, best);
            normal = bestN;
            return true;
        }

        /// <summary>Быстрая проверка капсула-капсула (замкнутая формула) — для ссылок робота.</summary>
        public static float CapsuleCapsuleDistance(in Prim A, in Prim B)
        {
            return CollisionWorld.SegmentSegmentDistance(A.a, A.b, B.a, B.b) - A.r - B.r;
        }
    }
}
