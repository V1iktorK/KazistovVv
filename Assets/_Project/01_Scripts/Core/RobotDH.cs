using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvKinematics
{
    /// <summary>
    /// Кинематическая модель робота по Денавиту–Хартенбергу (DH).
    ///
    /// Для каждого сустава хранится:
    ///   * axisLocal — ось вращения сустава в его ЛОКАЛЬНОЙ системе (для сцены);
    ///   * DH-параметры a/d/alpha/theta0 (измеряются автоматически из иерархии);
    ///   * theta — текущий угол.
    ///
    /// Прямая кинематика: DHForward (произведение матриц).
    /// Обратная кинематика: CCD с вращением строго вокруг оси каждого сустава —
    /// честная кинематика шарнирного манипулятора (без «свободных» вращений).
    /// </summary>
    [System.Serializable]
    public class DHLink
    {
        public float a;        // длина звена (смещение по X в системе i-1)
        public float d;        // офсет по оси Z
        public float alphaRad; // скрутка между осями Z
        public float theta0Rad;// начальный угол (нулевая поза модели)
        public float thetaRad; // текущий угол
        public Vector3 axisLocal = Vector3.up; // ось вращения в локальных осях сустава

        public Matrix4x4 Matrix(float theta)
        {
            float th = theta0Rad + theta;
            float ct = Mathf.Cos(th), st = Mathf.Sin(th);
            float ca = Mathf.Cos(alphaRad), sa = Mathf.Sin(alphaRad);
            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = ct;  m.m01 = -st * ca; m.m02 = st * sa;  m.m03 = a * ct;
            m.m10 = st;  m.m11 = ct * ca;  m.m12 = -ct * sa; m.m13 = a * st;
            m.m20 = 0f;  m.m21 = sa;       m.m22 = ca;       m.m23 = d;
            m.m30 = 0f;  m.m31 = 0f;       m.m32 = 0f;       m.m33 = 1f;
            return m;
        }
    }

    /// <summary>Прямая кинематика.</summary>
    public static class DHForward
    {
        public static Matrix4x4 Solve(IList<DHLink> links, IList<float> thetas)
        {
            Matrix4x4 acc = Matrix4x4.identity;
            for (int i = 0; i < links.Count; i++)
            {
                float th = thetas != null && i < thetas.Count ? thetas[i] : links[i].thetaRad;
                acc *= links[i].Matrix(th);
            }
            return acc;
        }

        public static Vector3 Position(IList<DHLink> links, IList<float> thetas)
        {
            return Solve(links, thetas).GetColumn(3);
        }
    }

    /// <summary>
    /// Обратная кинематика — осевой CCD (Cyclic Coordinate Descent):
    /// каждый сустав поворачивается ТОЛЬКО вокруг своей оси вращения,
    /// угол считается из проекций векторов «сустав→TCP» и «сустав→цель».
    /// </summary>
    public static class DHInverse
    {
        /// <summary>
        /// Пытается свести TCP (endEffector, потомок последнего сустава) к target.
        /// </summary>
        /// <param name="joints">Суставы от базы к фланцу.</param>
        /// <param name="axes">Оси вращения в локальных координатах соответствующих суставов.</param>
        /// <param name="endEffector">Точка TCP (может совпадать с последним суставом).</param>
        /// <param name="target">Целевая мировая точка.</param>
        /// <param name="iterations">Число итераций.</param>
        /// <param name="tolerance">Допуск, м.</param>
        /// <summary>Есть ли в векторе NaN или бесконечность (отсев нечисловых данных, урок E-21).</summary>
        private static bool IsNotFinite(Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                   float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
        }
        public static bool SolveCCD(Transform[] joints, Vector3[] axes,
            Transform endEffector, Vector3 target,
            int iterations = 24, float tolerance = 0.01f)
        {
            if (joints == null || joints.Length == 0 || endEffector == null) return false;

            // ОТСЕВ НЕЧИСЕЛ (добавлено 30.10.2026, S44 — урок E-21).
            //
            // ЧЕМ ЭТО ОПАСНО ЗДЕСЬ. Условие выхода `(pos - target).magnitude <= tolerance` для NaN
            // даёт `false` — то есть НИКОГДА не выполняется. Решатель отрабатывает все 24 итерации
            // вхолостую, поворачивая суставы по мусорным углам, и возвращает `false` «не сошлось».
            //
            // ПОЧЕМУ ЭТО ХУЖЕ ПРОСТО ОТКАЗА: за время этих итераций суставы УЖЕ ПОВЁРНУТЫ по
            // нечисловым осям. Робот окажется в позе, которой никто не просил, а сообщение скажет
            // лишь «решатель не сошёлся» — и искать причину будут в планировщике.
            //
            // Теперь при нечисловой цели решатель выходит СРАЗУ, не трогая суставы.
            if (IsNotFinite(target)) return false;

            for (int it = 0; it < iterations; it++)
            {
                if ((endEffector.position - target).magnitude <= tolerance) return true;

                for (int i = joints.Length - 1; i >= 0; i--)
                {
                    Transform j = joints[i];
                    if (j == null) continue;
                    if ((endEffector.position - target).magnitude <= tolerance) return true;

                    Vector3 axis = j.TransformDirection(axes != null && i < axes.Length
                        ? axes[i]
                        : Vector3.up);

                    Vector3 toEnd = endEffector.position - j.position;
                    Vector3 toTarget = target - j.position;

                    // Проецируем на плоскость, перпендикулярную оси вращения.
                    Vector3 eP = Vector3.ProjectOnPlane(toEnd, axis);
                    Vector3 tP = Vector3.ProjectOnPlane(toTarget, axis);
                    if (eP.sqrMagnitude < 1e-8f || tP.sqrMagnitude < 1e-8f) continue;

                    float angle = Vector3.SignedAngle(eP, tP, axis);
                    // Ограничиваем шаг, чтобы не «дёргать»
                    angle = Mathf.Clamp(angle, -25f, 25f);

                    j.Rotate(axis, angle, Space.World);
                }
            }

            return (endEffector.position - target).magnitude <= tolerance * 4f;
        }
    }
}
