using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Постур-контроль (Проблема 1, п.1.4): движение к цели с проекцией вторичной цели
    /// в нулевое пространство Якобиана — робот доезжает до точки, одновременно
    /// «уходя» от лимитов и сингулярностей, не сбиваясь с траектории.
    ///
    ///   J#  = Jᵀ (J Jᵀ + λ²I)⁻¹                     (демпфированный псевдообратный)
    ///   λ²  = λ0²(1 − σ_min/σ_th)²  при σ_min < σ_th, иначе 0
    ///   Δq  = J# e + α (I − J# J) z ,  z = −k ∇H(q)
    ///
    /// H(q) — вторичная цель (лимиты + home + манипулируемость), ∇H — конечными разностями.
    /// Все операции детерминированы, шаг по суставам ограничен скоростями.
    /// </summary>
    public class PostureControl
    {
        public float lambda0 = 0.05f;        // базовая демпфирующая добавка
        public float sigmaThreshold = 0.05f; // порог σ_min (м/град)
        public float alpha = 0.6f;           // вклад нулевого пространства
        public float gain = 0.5f;            // k для градиента вторичной цели
        public float maxStepDeg = 3f;        // ограничение шага на кадр (град)

        private PoseValidator v;
        private PostureSelector posture;
        private double[][] jac;

        public bool Ready { get; private set; }
        public float LastSigmaMin { get; private set; }
        public float LastNullSpaceGain { get; private set; }

        public void Init(PoseValidator validator, PostureSelector postureSelector)
        {
            v = validator;
            posture = postureSelector;
            Ready = v != null && v.Ready;
            if (!Ready) return;
            jac = KinematicsJacobian.Allocate(v.Dof);
        }

        /// <summary>
        /// Один шаг: сместить TCP к targetPos (мировые координаты), сохранив позу удобной.
        /// Возвращает новую конфигурацию (не меняя сцену).
        /// </summary>
        public double[] Step(double[] q, Vector3 targetPos, double dt)
        {
            if (!Ready) return q;
            double[] qNew = (double[])q.Clone();

            v.Jacobian(q, jac, out Vector3 tcp);
            Vector3 e = targetPos - tcp;                       // ошибка задачи (3D)
            if (e.magnitude < 0.0005f) return qNew;

            double sigmaMin = KinematicsJacobian.SigmaMin(jac, v.Dof) * 57.29578;
            LastSigmaMin = (float)sigmaMin;
            double lambda2 = 0;
            if (sigmaMin < sigmaThreshold)
            {
                double t = 1.0 - sigmaMin / Mathf.Max(1e-6f, sigmaThreshold);
                lambda2 = lambda0 * lambda0 * t * t;
            }

            int n = v.Dof;
            // A = J Jᵀ + λ²I (3×3), решаем A y = e
            double[,] A = new double[3, 3];
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                {
                    double s = 0;
                    for (int k = 0; k < n; k++) s += jac[r][k] * jac[c][k];
                    A[r, c] = s + (r == c ? lambda2 : 0.0);
                }
            double[] rhs = { e.x, e.y, e.z };
            if (!Solve3(A, rhs, out double[] y)) return qNew;

            // Δq_task = Jᵀ y
            double[] dqTask = new double[n];
            for (int k = 0; k < n; k++)
                dqTask[k] = jac[0][k] * y[0] + jac[1][k] * y[1] + jac[2][k] * y[2];

            // Вторичная цель: градиент стоимости позы (конечные разности, детерминированно)
            double[] grad = new double[n];
            double h = 0.5 * Mathf.Deg2Rad;
            double baseCost = posture != null && posture.Ready ? posture.Cost(q, null) : 0;
            for (int k = 0; k < n; k++)
            {
                var probe = (double[])q.Clone();
                probe[k] += h;
                grad[k] = ((posture != null && posture.Ready ? posture.Cost(probe, null) : 0) - baseCost) / h;
            }

            // Проектор N = I − J# J через уже решённую систему: J# J v = Jᵀ A⁻¹ (J v)
            double[] z = new double[n];
            for (int k = 0; k < n; k++) z[k] = -gain * grad[k];
            double[] jz = new double[3];
            for (int r = 0; r < 3; r++)
            {
                double s = 0;
                for (int k = 0; k < n; k++) s += jac[r][k] * z[k];
                jz[r] = s;
            }
            double[] yz = { 0, 0, 0 };
            if (Solve3(A, jz, out double[] solved)) yz = solved;
            double[] jSharpJz = new double[n];
            for (int k = 0; k < n; k++)
                jSharpJz[k] = jac[0][k] * yz[0] + jac[1][k] * yz[1] + jac[2][k] * yz[2];
            double[] dqNull = new double[n];
            for (int k = 0; k < n; k++) dqNull[k] = z[k] - jSharpJz[k];

            LastNullSpaceGain = 0f;
            double maxTask = 0;
            for (int k = 0; k < n; k++) maxTask = System.Math.Max(maxTask, System.Math.Abs(dqTask[k]));
            double scale = maxTask > 1e-9
                ? System.Math.Min(1.0, (maxStepDeg * Mathf.Deg2Rad) / maxTask * System.Math.Max(dt, 0.001) * 60.0)
                : 1.0;

            for (int k = 0; k < n; k++)
            {
                double dq = dqTask[k] * scale + alpha * dqNull[k] * System.Math.Min(1.0, scale);
                qNew[k] += dq;
                LastNullSpaceGain += (float)System.Math.Abs(alpha * dqNull[k] * System.Math.Min(1.0, scale));
            }
            return qNew;
        }

        /// <summary>
        /// ПОРОГ ОТКАЗА ПО ОПРЕДЕЛИТЕЛЮ (найдено тестом 30.10.2026, S44).
        ///
        /// ЧТО БЫЛО. Стояло `Math.Abs(det) < 1e-12` — порог на САМОМ пределе точности double.
        /// Матрица почти вырожденная (определитель 1e-9 — это уже практически сингулярная поза)
        /// проходила проверку, и решение давало шаги порядка 1e+9: робота не «дёргало», а
        /// телепортировало, и виноватой выглядела траектория.
        ///
        /// ПОЧЕМУ 1e-8. `A = J Jᵀ` имеет масштаб метров и радианов (у этого робота — до сотен).
        /// Определитель меньше 1e-8 означает число обусловленности свыше ~1e8: решение уже не
        /// имеет физического смысла. Порог взят с запасом в 4 порядка от прежнего и всё ещё на
        /// 4 порядка НИЖЕ нормально обусловленных матриц (тест `Solve3_WellConditionedMatrix_Solves`
        /// проверяет, что обычные конфигурации по-прежнему решаются).
        /// </summary>
        private const double SingularDeterminant = 1e-8;

        private static bool Solve3(double[,] A, double[] b, out double[] x)
        {
            x = new double[3];
            double c00 = A[1, 1] * A[2, 2] - A[1, 2] * A[2, 1];
            double c01 = -(A[1, 0] * A[2, 2] - A[1, 2] * A[2, 0]);
            double c02 = A[1, 0] * A[2, 1] - A[1, 1] * A[2, 0];
            double c10 = -(A[0, 1] * A[2, 2] - A[0, 2] * A[2, 1]);
            double c11 = A[0, 0] * A[2, 2] - A[0, 2] * A[2, 0];
            double c12 = -(A[0, 0] * A[2, 1] - A[0, 1] * A[2, 0]);
            double c20 = A[0, 1] * A[1, 2] - A[0, 2] * A[1, 1];
            double c21 = -(A[0, 0] * A[1, 2] - A[0, 2] * A[1, 0]);
            double c22 = A[0, 0] * A[1, 1] - A[0, 1] * A[1, 0];
            double det = A[0, 0] * c00 + A[0, 1] * c01 + A[0, 2] * c02;
            if (System.Math.Abs(det) < SingularDeterminant) return false;

            // x = A⁻¹ b, где A⁻¹[i][j] = cof[j][i]/det
            x[0] = (c00 * b[0] + c10 * b[1] + c20 * b[2]) / det;
            x[1] = (c01 * b[0] + c11 * b[1] + c21 * b[2]) / det;
            x[2] = (c02 * b[0] + c12 * b[1] + c22 * b[2]) / det;
            return true;
        }
    }
}
