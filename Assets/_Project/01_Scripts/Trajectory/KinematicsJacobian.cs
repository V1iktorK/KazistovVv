using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Численный анализ якобиана (E2 плана): позиционный якобиан 3×N конечными разностями,
    /// манипулируемость sqrt(det(J·Jᵀ)) и оценка σ_min (степенной метод по JᵀJ).
    /// Нужен для критерия «близко к сингулярности» и для скоринга траекторий.
    /// </summary>
    public static class KinematicsJacobian
    {
        /// <summary>
        /// ШАГ КОНЕЧНЫХ РАЗНОСТЕЙ ДЛЯ ЯКОБИАНА — ЕДИНСТВЕННОЕ МЕСТО, ГДЕ ОН ЗАДАН (P2-14,
        /// 11.11.2026).
        ///
        /// ЧТО БЫЛО. Здесь стояло 0,5°, а в `PoseValidator.Jacobian` — 0,25°: два расчёта одного
        /// якобиана давали разные σ_min и разную манипулируемость для ОДНОЙ позы.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. «Близко к сингулярности» решает, можно ли двигать робота; два разных
        /// ответа на один вопрос — это прицел, который зелёный по одной оценке и красный по
        /// другой, и никакой возможности понять, какая верна.
        ///
        /// ВЫБРАНО 0,5°: шаг вдвое крупнее даёт вдвое меньший шум округления `float`-трансформа
        /// и остаётся малым против хода сустава (0,5° из 180°), то есть производная ещё честная.
        /// </summary>
        public const double StepDegrees = 0.5;

        /// <summary>Шаг конечных разностей в радианах (для призмы SCARA — те же единицы счёта).</summary>
        public static double StepRadians { get { return StepDegrees * Mathf.Deg2Rad; } }

        public static void Compute(PoseValidator v, double[] q, double[][] jac, out Vector3 tcp)
        {
            int n = v.Dof;
            tcp = v.TcpAt(q);
            double h = StepRadians;   // шаг — из одного места (P2-14)
            double[] probe = (double[])q.Clone();
            for (int i = 0; i < n; i++)
            {
                double save = probe[i];
                probe[i] = save + h;
                Vector3 plus = v.TcpAt(probe);
                probe[i] = save - h;
                Vector3 minus = v.TcpAt(probe);
                probe[i] = save;
                Vector3 d = (plus - minus) / (float)(2.0 * h);
                jac[0][i] = d.x; jac[1][i] = d.y; jac[2][i] = d.z;
            }
        }

        public static double[][] Allocate(int dof)
        {
            var j = new double[3][];
            for (int i = 0; i < 3; i++) j[i] = new double[dof];
            return j;
        }

        /// <summary>Манипулируемость: sqrt(det(J·Jᵀ)) — мера близости к сингулярности.</summary>
        public static double Manipulability(double[][] j, int dof)
        {
            // 3×3 матрица A = J·Jᵀ
            double[,] a = new double[3, 3];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    double s = 0;
                    for (int k = 0; k < dof; k++) s += j[r][k] * j[c][k];
                    a[r, c] = s;
                }
            double det = a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1])
                       - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0])
                       + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);
            return det <= 0 ? 0.0 : System.Math.Sqrt(det);
        }

        /// <summary>Оценка σ_min через степенной метод по JᵀJ (10 итераций, детерминированно).</summary>
        public static double SigmaMin(double[][] j, int dof)
        {
            double[,] m = new double[dof, dof];
            for (int r = 0; r < dof; r++)
                for (int c = 0; c < dof; c++)
                {
                    double s = 0;
                    for (int k = 0; k < 3; k++) s += j[k][r] * j[k][c];
                    m[r, c] = s;
                }
            // сдвиг: (||M||·I − M) → наибольшее собственное значение = ||M|| − σ_min²
            double norm = 0;
            for (int r = 0; r < dof; r++)
            {
                double s = 0;
                for (int c = 0; c < dof; c++) s += System.Math.Abs(m[r, c]);
                norm = System.Math.Max(norm, s);
            }
            for (int r = 0; r < dof; r++) m[r, r] = norm - m[r, r];
            for (int r = 0; r < dof; r++)
                for (int c = 0; c < dof; c++)
                    if (r != c) m[r, c] = -m[r, c];

            double[] v = new double[dof];
            for (int i = 0; i < dof; i++) v[i] = 1.0 / System.Math.Sqrt(dof);
            double lambda = 0;
            for (int it = 0; it < 10; it++)
            {
                double[] nv = new double[dof];
                for (int r = 0; r < dof; r++)
                {
                    double s = 0;
                    for (int c = 0; c < dof; c++) s += m[r, c] * v[c];
                    nv[r] = s;
                }
                double len = 0;
                for (int i = 0; i < dof; i++) len += nv[i] * nv[i];
                len = System.Math.Sqrt(len);
                if (len < 1e-12) break;
                lambda = len;
                for (int i = 0; i < dof; i++) v[i] = nv[i] / len;
            }
            double smin2 = System.Math.Max(0.0, norm - lambda);
            return System.Math.Sqrt(smin2);
        }
    }
}
