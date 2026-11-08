using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>Метки ветви решения IK (для «posture locking» и гистерезиса).</summary>
    public struct IkBranchTag
    {
        public bool shoulderFar;   // база повёрнута на 180°
        public bool elbowDown;     // локоть «вниз» (вторая ветвь)
        public bool wristFlip;     // запястье перевёрнуто
        public override string ToString()
        {
            return (shoulderFar ? "S+" : "S-") + "," + (elbowDown ? "E+" : "E-") +
                   "," + (wristFlip ? "W+" : "W-");
        }
    }

    public struct IkSolution
    {
        public double[] q;
        public IkBranchTag tag;
        public double fkError;      // ошибка прямой задачи по TCP, м
        public bool withinLimits;
        public bool nearSingularity; // близко к вырождению (вытянута/сложена — SCARA; wrist-singular — 6R)
    }

    /// <summary>
    /// Аналитическая IK (MVP, Проблема 1): для 6-осевого робота с «сферическим»
    /// запястьем (оси 4-5-6 пересекаются) даём ВСЕ ветви: 2 (плечо) × 2 (локоть) ×
    /// 2 (переворот запястья) = до 8 решений, плюс резервный многостартовый CCD.
    ///
    /// Вывод формул: позиционная часть — 2R в плоскости руки (теорема косинусов),
    /// база — доворот плоскости на цель (q1 = atan2 в плоскости ⊥ вертикали),
    /// запястье — roll-pitch-roll, переворот: (q4,q5,q6) → (q4+π, −q5, q6+π).
    /// Нулевые углы модели учитываются через смещения q2_off/q3_off, замеренные
    /// в стартовой позе.
    /// </summary>
    public class IkSolver
    {
        public float positionTolerance = 0.004f;  // 4 мм

        /// <summary>
        /// ДОПУСК IK ДЛЯ SCARA, метры (P1-17, 11.11.2026).
        ///
        /// ЧТО БЫЛО. В `SolveScara` стояло «магическое» `sol.fkError &lt;= 0.05f` — 5 см, — тогда
        /// как `positionTolerance` равен 4 мм и 6-осевой отбраковывает решения по
        /// `positionTolerance * 2f`. Для одного проекта это два разных представления о «попал»:
        /// SCARA принимала решение с ошибкой 4,9 см как годное, и оператор видел, что кончик
        /// останавливается в 5 см от цели, — а планировщик считал позу достигнутой.
        ///
        /// ПОЧЕМУ ДОПУСК ВСЁ ЖЕ ШИРЕ. У SCARA схват/«палец» (`z_5`) вынесен вдоль оси инструмента:
        /// FTC-точка модели и точка, которой оператор целится, расходятся на конструктивную длину
        /// пальца, поэтому честная ошибка по TCP на этой модели больше. Число названо, объяснено и
        /// живёт рядом с `positionTolerance`, а не внутри метода.
        /// </summary>
        public float scaraFkTolerance = 0.05f;

        public int ccdIterations = 120;
        public int ccdSeeds = 6;

        private PoseValidator v;
        public bool Ready { get; private set; }
        public string LastScaraDebug { get; private set; } = "";

        /// <summary>
        /// Сколько решений последнего `SolveAllSeeded` дала АНАЛИТИКА (всё, что дальше, —
        /// добор CCD-сидами). Нужно вызывающему, чтобы честно подписать конфигурацию цели
        /// («IK S-,E+» или «CCD»), а не выдавать CCD-доводку за ветвь аналитики.
        /// </summary>
        public int LastAnalyticCount { get; private set; }

        // Замеренная геометрия 6-осевого
        private Vector3 baseOrigin, up, rRef, nRef;
        private float a2, a3;
        private float q2OffsetDeg, q3OffsetDeg;

        public void Init(PoseValidator validator)
        {
            v = validator;
            Ready = false;
            if (v == null || !v.Ready || v.Dof < 6) return;

            double[] q0 = new double[v.Dof]; // стартовая (нулевая) поза
            Vector3 baseP = v.PivotAt(0, q0);
            Vector3 shoulder = v.PivotAt(1, q0);
            Vector3 elbow = v.PivotAt(2, q0);
            Vector3 wrist = v.PivotAt(3, q0);

            baseOrigin = baseP;
            up = v.AxisWorld(0, q0);                       // ось J1 — вертикаль
            Vector3 armDir = Vector3.ProjectOnPlane(wrist - elbow, up);
            if (armDir.sqrMagnitude < 1e-6f) return;
            rRef = armDir.normalized;                      // радиальное направление покоя
            nRef = Vector3.Cross(up, rRef).normalized;     // нормаль плоскости руки

            a2 = Vector3.Distance(shoulder, elbow);        // плечо
            a3 = Vector3.Distance(elbow, wrist);           // предплечье

            // Смещения нулей: считаем формулы в стартовой позе и запоминаем разницу.
            Vector3 d = elbow - shoulder;
            float rho = new Vector2(Vector3.Dot(d, rRef), Vector3.Dot(d, nRef)).magnitude;
            float sigma = Vector3.Dot(d, up);
            float phi1Rest = Mathf.Atan2(sigma, rho) * Mathf.Rad2Deg;
            Vector3 f = wrist - elbow;
            float phi2Rest = Mathf.Atan2(Vector3.Dot(f, up),
                new Vector2(Vector3.Dot(f, rRef), Vector3.Dot(f, nRef)).magnitude) * Mathf.Rad2Deg;
            q2OffsetDeg = 0f - (phi1Rest - 90f);
            q3OffsetDeg = 0f - (phi2Rest - phi1Rest);
            Ready = true;
        }

        /// <summary>
        /// Расширенный набор решений для «фантомов» (MVP-3): аналитические ветви +
        /// детерминированный мультисид-CCD (текущая поза, «дом», зеркала локтя/плеча,
        /// переворот запястья). Дубликаты по конфигурации отбрасываются.
        /// </summary>
        public List<IkSolution> SolveAllSeeded(Vector3 target, double[] seed, int maxSolutions = 6)
        {
            var list = SolveAll(target, seed);
            LastAnalyticCount = list.Count;
            if (v == null || seed == null) return list;

            // P1-18: ДЛИНА СИДА ПРОВЕРЯЕТСЯ. Ниже идут `s[1]…s[5]`; для робота с 5 осями
            // (`seed.Length == 5`) обращение `s[5] += 180` бросало `IndexOutOfRangeException`
            // прямо в генерации вариантов, и вызывающий видел пустой список вместо ветвей.
            // `SolveAll` такую проверку (`seed.Length < 6`) делал, а этот метод — нет.
            if (seed.Length < 6) return list;

            if (v.Dof == 3) return list;   // SCARA: аналитических ветвей (≤4) достаточно
            if (list.Count >= maxSolutions) return list;

            const int seedCount = 6;
            for (int k = 0; k < seedCount && list.Count < maxSolutions; k++)
            {
                var s = (double[])seed.Clone();
                switch (k)
                {
                    case 0: break;                                   // текущая поза
                    case 1: s[1] -= 45; s[2] += 45; break;            // локоть «вверх»
                    case 2: s[1] += 45; s[2] -= 45; break;            // локоть «вниз»
                    case 3: s[0] += 180; break;                       // плечо развёрнуто
                    case 4: s[3] += 180; s[4] = -s[4]; s[5] += 180; break; // переворот запястья
                    case 5: s[1] -= 70; s[2] += 70; s[4] = -25; break;     // «сложенная» рука
                }
                if (!v.WithinLimits(s)) continue;
                if (!v.SolveIk(target, s, out double[] q, ccdIterations, positionTolerance)) continue;
                if (!v.WithinLimits(q)) continue;

                double err = (v.TcpAt(q) - target).magnitude;
                if (err > positionTolerance * 3f) continue;

                bool dup = false;
                foreach (IkSolution ex in list)
                    if (ConfigDistance(ex.q, q) < 0.05) { dup = true; break; }
                if (dup) continue;

                list.Add(new IkSolution
                {
                    q = q,
                    tag = new IkBranchTag
                    {
                        shoulderFar = k == 3,
                        elbowDown = k == 2 || k == 5,
                        wristFlip = k == 4
                    },
                    withinLimits = true,
                    fkError = err
                });
            }
            return list;
        }

        private double ConfigDistance(double[] a, double[] b)
        {
            if (a == null || b == null) return 0;
            double s = 0;
            int n = System.Math.Min(a.Length, b.Length);
            for (int i = 0; i < n; i++)
            {
                double r = System.Math.Abs(v.Upper[i] - v.Lower[i]);
                if (r < 1e-6) r = 1;
                // Вращательные оси сравниваем по кратчайшему довороту (360° ≡ 0°).
                double d = v.IsPrismatic(i) ? (a[i] - b[i]) : Mathf.DeltaAngle((float)a[i], (float)b[i]);
                d /= r;
                s += d * d;
            }
            return System.Math.Sqrt(s);
        }

        /// <summary>Все ветви аналитической IK; при неудаче — многостартовый CCD.</summary>
        public List<IkSolution> SolveAll(Vector3 target, double[] seed)
        {
            var result = new List<IkSolution>();
            if (v == null || seed == null) return result;
            if (v.Dof == 3) return SolveAllScara(target, seed);   // SCARA: 2R + призма
            if (!Ready || seed.Length < 6) return result;

            Vector3 shoulder = v.PivotAt(1, new double[v.Dof]);
            Vector3 d = target - shoulder;
            Vector3 planar = Vector3.ProjectOnPlane(d, up);
            float sigma = Vector3.Dot(d, up);

            // Азимут цели относительно плоскости покоя.
            float az = Mathf.Atan2(Vector3.Dot(planar, nRef), Vector3.Dot(planar, rRef)) * Mathf.Rad2Deg;

            for (int s = 0; s < 2; s++)               // плечо: 0 — «ближняя», 1 — разворот на 180°
            {
                bool shoulderFar = s == 1;
                float q1 = az + (shoulderFar ? 180f : 0f);
                float rho = planar.magnitude * (shoulderFar ? -1f : 1f);

                float r = Mathf.Sqrt(rho * rho + sigma * sigma);
                float cosRel = (r * r - a2 * a2 - a3 * a3) / (2f * a2 * a3);
                if (Mathf.Abs(cosRel) > 1f) continue;  // вне рабочей зоны

                float relAbs = Mathf.Acos(Mathf.Clamp(cosRel, -1f, 1f)) * Mathf.Rad2Deg;
                for (int e = 0; e < 2; e++)
                {
                    bool elbowDown = e == 1;
                    float rel = elbowDown ? -relAbs : relAbs;
                    float psi = Mathf.Atan2(sigma, rho) * Mathf.Rad2Deg;
                    float phi1 = psi - Mathf.Atan2(a3 * Mathf.Sin(rel * Mathf.Deg2Rad),
                                                   a2 + a3 * Mathf.Cos(rel * Mathf.Deg2Rad)) * Mathf.Rad2Deg;

                    double q2 = phi1 - 90f + q2OffsetDeg;
                    double q3 = rel + q3OffsetDeg;

                    for (int w = 0; w < 2; w++)         // запястье: базовая / перевёрнутая
                    {
                        bool wristFlip = w == 1;
                        var q = (double[])seed.Clone();
                        q[0] = q1; q[1] = q2; q[2] = q3;
                        if (wristFlip)
                        {
                            q[3] += 180.0;
                            q[4] = -q[4];
                            q[5] += 180.0;
                        }

                        // Доводим запястье (оси 4..6) на кончик инструмента: позиционная часть
                        // уже решена, остаётся 3-DOF CCD — быстро и без потери ветви.
                        v.SolveIkRange(target, q, 3, v.Dof - 1, 50, positionTolerance);

                        var sol = new IkSolution
                        {
                            q = q,
                            tag = new IkBranchTag { shoulderFar = shoulderFar, elbowDown = elbowDown, wristFlip = wristFlip }
                        };
                        sol.withinLimits = v.WithinLimits(q, 0f);
                        sol.fkError = (v.TcpAt(q) - target).magnitude;
                        if (sol.fkError <= positionTolerance * 2f) result.Add(sol);
                    }
                }
            }

            if (result.Count == 0) result.AddRange(CcdFallback(target, seed));
            return result;
        }

        /// <summary>
        /// SCARA (q = (θ1, θ2, z)): аналитика 2R + призматическая ось.
        /// Ветви: elbow up/down × передний/задний вылет (q1 и q1+180°) = до 4 конфигураций.
        /// Рабочая зона — КОЛЬЦО r ∈ [|a1−a2|, a1+a2]; сингулярности — полное вытягивание
        /// (q2 ≈ 0) и полное складывание (q2 ≈ 180°); ориентация фланца без wrist flip.
        /// </summary>
        private List<IkSolution> SolveAllScara(Vector3 target, double[] seed)
        {
            var list = new List<IkSolution>();
            if (v.Dof != 3) return list;

            double[] zero = new double[3];
            Vector3 shoulder = v.PivotAt(0, zero);
            Vector3 elbow0 = v.PivotAt(1, zero);
            Vector3 wrist0 = v.PivotAt(2, zero);
            Vector3 up = v.AxisWorld(0, zero);
            Vector3 ref1 = Vector3.ProjectOnPlane(elbow0 - shoulder, up);
            Vector3 ref2 = Vector3.ProjectOnPlane(wrist0 - elbow0, up);
            float a1 = ref1.magnitude, a2 = ref2.magnitude;
            if (a1 < 1e-4f || a2 < 1e-4f) return list;
            ref1.Normalize(); ref2.Normalize();
            float restAngle2 = Vector3.SignedAngle(ref1, ref2, up);

            Vector3 d = Vector3.ProjectOnPlane(target - shoulder, up);
            float r = d.magnitude;
            float reachMax = a1 + a2 - 0.002f;
            float reachMin = Mathf.Abs(a1 - a2) + 0.002f;
            LastScaraDebug = string.Format("a1={0:F3} a2={1:F3} shoulder={2} r={3:F3} (кольцо {4:F3}..{5:F3})",
                a1, a2, shoulder.ToString("0.00"), r, reachMin, reachMax);            if (r > reachMax || r < reachMin) return list;      // вне кольца — решения нет

            float cosQ2 = Mathf.Clamp((r * r - a1 * a1 - a2 * a2) / (2f * a1 * a2), -1f, 1f);
            float q2Abs = Mathf.Acos(cosQ2) * Mathf.Rad2Deg;
            float az = Vector3.SignedAngle(ref1, d, up);
            // Высота z_5 отсчитывается ОТ БАЗЫ (не от плеча!) — иначе системная ошибка ~0.2 м.
            double zTarget = System.Math.Min(System.Math.Max(
                Vector3.Dot(target - v.BasePosition, up), v.Lower[2]), v.Upper[2]);

            float bestErr = float.MaxValue;

            for (int s = 0; s < 2; s++)                 // передний / задний вылет
            {
                bool backReach = s == 1;
                float azimuth = az + (backReach ? 180f : 0f);
                float radial = r * (backReach ? -1f : 1f);

                for (int e = 0; e < 2; e++)             // локоть вверх / вниз
                {
                    bool elbowDown = e == 1;
                    float q2rel = elbowDown ? -q2Abs : q2Abs;
                    float phi = Mathf.Atan2(a2 * Mathf.Sin(q2rel * Mathf.Deg2Rad),
                                            a1 + a2 * Mathf.Cos(q2rel * Mathf.Deg2Rad)) * Mathf.Rad2Deg;
                    double q1 = azimuth - phi;
                    double q2 = q2rel - restAngle2;

                    var q = (double[])seed.Clone();
                    q[0] = q1; q[1] = q2; q[2] = zTarget;

                    bool singular = Mathf.Abs(cosQ2) > 0.985f;   // вытянута или сложена
                    var sol = new IkSolution
                    {
                        q = q,
                        tag = new IkBranchTag { shoulderFar = backReach, elbowDown = elbowDown, wristFlip = false },
                        withinLimits = v.WithinLimits(q, 0f),
                        fkError = (v.TcpAt(q) - target).magnitude,
                        nearSingularity = singular
                    };
                    if (sol.fkError <= scaraFkTolerance) list.Add(sol);   // допуск SCARA — см. `scaraFkTolerance`
                    bestErr = System.Math.Min(bestErr, (float)sol.fkError);
                    // `radial` больше не считается: задний вылет уже выражен поворотом азимута на
                    // 180° (P1-18). Прежняя переменная вычислялась и «гасилась» `_ = radial;`, то
                    // есть шапка метода обещала различие вылетов, которого в коде не было.
                }
            }
            LastScaraDebug += " | лучший err=" + (bestErr == float.MaxValue ? "n/a" : bestErr.ToString("0.000"));
            return list;
        }

        /// <summary>Резерв: CCD из нескольких сидов (текущая поза, «дом», зеркала по знаку).</summary>
        private List<IkSolution> CcdFallback(Vector3 target, double[] seed)
        {
            var list = new List<IkSolution>();
            for (int k = 0; k < ccdSeeds; k++)
            {
                var s = (double[])seed.Clone();
                if (k == 1) { s[1] -= 40; s[2] += 40; }
                if (k == 2) { s[1] += 40; s[2] -= 40; }
                if (k == 3) { s[0] += 180; }
                if (k == 4) { s[4] = -s[4]; s[3] += 180; s[5] += 180; }
                if (k == 5) { s[1] -= 70; s[2] += 70; s[4] = -20; }

                if (!v.SolveIk(target, s, out double[] q, ccdIterations, positionTolerance))
                    continue;
                list.Add(new IkSolution
                {
                    q = q,
                    tag = new IkBranchTag { shoulderFar = k == 3, elbowDown = k == 5, wristFlip = k == 4 },
                    withinLimits = v.WithinLimits(q, 0f),
                    fkError = (v.TcpAt(q) - target).magnitude
                });
                break; // первого успешного достаточно как запасной вариант
            }
            return list;
        }
    }
}
