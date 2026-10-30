using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Файл калибровки робота (ЭТАП 9 ТЗ): TCP, база, камера (заглушка).</summary>
    [Serializable]
    public class KvCalibrationData
    {
        public int version = 1;
        public string robot = "";
        public string created = "";
        public string notes = "";

        // --- TCP (4 точки)
        public int tcpPoints;
        public bool tcpSolved;
        public float[] tcpOffsetFlange = new float[3];   // смещение инструмента в системе фланца, м
        public float tcpLength;                          // |смещение|, м
        public float tcpResidualMm;                      // СКО остатка, мм
        public float tcpMaxResidualMm;                   // худшая точка, мм
        public float[] tcpPose = new float[6];           // поза при последней записи (диагностика)

        // --- база робота
        public int basePoints;
        public bool baseSolved;
        public float baseHeightMm;                       // высота базы над опорной плоскостью
        public float baseTiltDeg;                        // наклон оси базы к нормали плоскости
        public float[] basePlaneNormal = new float[3];
        public float[] baseOffsetWorld = new float[3];   // предлагаемое смещение базы в мире, м
        public float[] baseEulerWorld = new float[3];    // предлагаемый доворот базы, °

        // --- TCP для SCARA (ФИКС 3): калибровка ПО НОРМАЛИ К ПЛОСКОСТИ
        // У SCARA кисти нет — ориентация инструмента не меняется, поэтому 4-точечный метод
        // неприменим. Оператор ставит инструмент в N ≥ 3 точки одной плоскости (столешницы),
        // система строит плоскость по положениям ФЛАНЦА и находит смещение TCP вдоль оси
        // инструмента (высоту инструмента).
        public string tcpMethod = "4point";              // "4point" (робот) | "plane" (SCARA)
        public int tcpPlanePoints;
        public bool tcpPlaneSolved;
        public float[] tcpOffsetPlane = new float[3];    // смещение TCP в мире сцены, м (для отчёта)
        public float[] tcpPlaneNormal = new float[3];    // нормаль опорной плоскости
        public float tcpPlaneHeight;                     // высота инструмента над плоскостью, м
        public float tcpPlaneResidualMm;                 // остаток приведения к плоскости, мм

        // --- поворот базы (ФИКС 4): вторая точка отсчёта — направление «вперёд» робота
        public bool baseForwardSet;
        public float[] baseForwardWorld = new float[3];
        public float[] baseForwardPose = new float[6];
        public float baseYawDeg;                         // доворот базы вокруг вертикали, °

        // --- камера (hand-eye) — заглушка на будущее
        public bool cameraCalibrated;
        public string cameraNote = "заглушка: ждём модель крепления камеры";
    }

    /// <summary>
    /// ЭТАП 9 ТЗ: КАЛИБРОВОЧНЫЙ МАСТЕР.
    ///
    ///   1. КАЛИБРОВКА TCP ПО 4 ТОЧКАМ. Оператор наводит инструмент в ОДНУ И ТУ ЖЕ точку
    ///      пространства четырьмя разными ориентациями. Для каждой записи берётся поза
    ///      суставов, из неё считается СИСТЕМА ФЛАНЦА (положение — пивот последнего сустава,
    ///      оси — ось последнего сустава и перпендикуляр к ней), и решается линейная система
    ///      «(R_i − R_1)·t = p_1 − p_i» методом наименьших квадратов. Неизвестная точка
    ///      касания из системы исключается, поэтому знать её координаты оператору не нужно.
    ///      Результат: смещение инструмента в системе фланца, его длина и ОСТАТОЧНАЯ ОШИБКА
    ///      по точкам (по ней видно качество калибровки).
    ///   2. КАЛИБРОВКА БАЗЫ: оператор касается инструментом ТРЁХ и более точек одной
    ///      плоскости (например, столешницы) — считается высота базы над плоскостью и
    ///      наклон оси базы к нормали. Предлагаемое смещение/доворот можно применить
    ///      к роботу в сцене (runtime) отдельной кнопкой.
    ///   3. КАМЕРА (hand-eye) — ЗАГЛУШКА на будущее, как и требует ТЗ: сохраняется только
    ///      намерение калибровки.
    ///
    /// Всё сохраняется в файл JSON (`FeatureStorage.ConfigDir`), поэтому калибровка
    /// переживает перезапуск и может лежать рядом с проектом.
    /// </summary>
    public class KvCalibrationService
    {
        public event Action<string> Message;

        public const int TcpPointsNeeded = 4;

        private TrajectoryFlowController flow;
        private CollisionWorld world;

        private readonly List<double[]> tcpPoses = new List<double[]>();
        private readonly List<Vector3> basePoints = new List<Vector3>();
        private readonly List<double[]> basePoses = new List<double[]>();

        // --- ФИКС 3: калибровка TCP для SCARA по нормали к плоскости
        private readonly List<Vector3> planeFlange = new List<Vector3>();
        private readonly List<double[]> planePoses = new List<double[]>();
        private bool planeMethodOverride;

        // --- ФИКС 4: направление «вперёд» базы (вторая точка отсчёта)
        private Vector3 baseForward = Vector3.zero;
        private bool baseForwardSet;

        private KvCalibrationData data = new KvCalibrationData();
        private bool loadedFromFile;

        public int TcpPointCount { get { return tcpPoses.Count; } }
        public int BasePointCount { get { return basePoints.Count; } }
        /// <summary>Сколько точек плоскости записано для метода «по нормали» (ФИКС 3).</summary>
        public int TcpPlanePointCount { get { return planeFlange.Count; } }
        /// <summary>Сколько точек нужно методу «по нормали» (минимум 3 точки плоскости).</summary>
        public const int TcpPlanePointsNeeded = 3;

        /// <summary>
        /// ВЫБОР МЕТОДА КАЛИБРОВКИ TCP АВТОМАТИЧЕСКИ ПО ТИПУ РОБОТА (ФИКС 3):
        /// у SCARA кисти нет (ориентация инструмента не меняется), поэтому 4-точечный метод
        /// для неё вырожден — применяется метод «по нормали к плоскости». Оператор может
        /// переопределить выбор вручную (`PlaneMethodOverride`).
        /// </summary>
        public bool UsePlaneMethod
        {
            get
            {
                if (planeMethodOverride) return true;
                return flow != null && flow.Robot is SCARAController;
            }
        }

        /// <summary>Ручное переопределение метода (true — всегда «по нормали»).</summary>
        public bool PlaneMethodOverride
        {
            get { return planeMethodOverride; }
            set { planeMethodOverride = value; }
        }

        /// <summary>Подпись действующего метода для интерфейса и журнала.</summary>
        public string TcpMethodLabel
        {
            get
            {
                return UsePlaneMethod
                    ? "по нормали к плоскости (N ≥ " + TcpPlanePointsNeeded + " точек) — для SCARA"
                    : "по 4 точкам с разными ориентациями — для робота с кистью";
            }
        }
        public KvCalibrationData Data { get { return data; } }
        public bool TcpSolved { get { return data.tcpSolved; } }
        public bool BaseSolved { get { return data.baseSolved; } }

        public string FilePath
        {
            get
            {
                string robot = flow != null && flow.Robot != null ? flow.Robot.robotName : "robot";
                return Path.Combine(FeatureStorage.ConfigDir,
                    "calibration_" + FeatureStorage.SafeName(robot, "robot") + ".json");
            }
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)
        {
            flow = controller;
            world = collisionWorld;
        }

        // ================================================================== система фланца

        /// <summary>
        /// Система фланца для конфигурации q: положение — пивот последнего сустава,
        /// ориентация — ось последнего сустава (z) и перпендикуляр к оси предыдущего (x).
        /// Считается по ФАКТИЧЕСКОЙ кинематике валидатора, без изменений в ядре.
        /// </summary>
        public static bool FlangeFrame(PoseValidator v, double[] q, out Vector3 position,
            out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            if (v == null || !v.Ready || q == null) return false;

            int last = v.Dof - 1;
            position = v.PivotAt(last, q);

            Vector3 z = v.AxisWorld(last, q);
            if (z.sqrMagnitude < 1e-8f) z = Vector3.up;
            z.Normalize();

            Vector3 refAxis = v.Dof >= 2 ? v.AxisWorld(last - 1, q) : Vector3.right;
            Vector3 x = Vector3.Cross(refAxis, z);
            if (x.sqrMagnitude < 1e-6f) x = Vector3.Cross(Vector3.up, z);
            if (x.sqrMagnitude < 1e-6f) x = Vector3.right;
            x.Normalize();
            Vector3 y = Vector3.Cross(z, x).normalized;

            rotation = Quaternion.LookRotation(z, y);
            return true;
        }

        // ================================================================== TCP: запись и решение

        /// <summary>
        /// Записать точку касания: берётся ТЕКУЩАЯ поза суставов (оператор только что
        /// приложил инструмент к эталонной точке новой ориентацией).
        /// </summary>
        public bool RecordTcpPoint()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка TCP: робот не определён");
                return false;
            }
            if (tcpPoses.Count >= 8)
            {
                Report("калибровка TCP: уже записано 8 точек — нажмите «Сбросить точки»");
                return false;
            }

            double[] q = flow.Validator.CopyCurrent();

            // Проверяем, что ориентация заметно отличается от уже записанных: без этого
            // система вырождается (об этом честно пишем оператору).
            foreach (double[] prev in tcpPoses)
            {
                float angle = KvToolKinematics.ToolAxis(flow.Validator, prev) == Vector3.zero
                    ? 0f
                    : Vector3.Angle(KvToolKinematics.ToolAxis(flow.Validator, prev),
                        KvToolKinematics.ToolAxis(flow.Validator, q));
                if (angle < 12f)
                {
                    Report("калибровка TCP: ориентация почти такая же, как у уже записанной точки (" +
                           angle.ToString("0.0") + "°) — поверните инструмент заметно (нужно ≥ 12°)");
                    return false;
                }
            }

            tcpPoses.Add(q);
            Vector3 pos;
            Quaternion rot;
            FlangeFrame(flow.Validator, q, out pos, out rot);
            Report("калибровка TCP: точка " + tcpPoses.Count + " записана · фланец " +
                   pos.x.ToString("0.000") + ", " + pos.y.ToString("0.000") + ", " +
                   pos.z.ToString("0.000") + " · ось " +
                   KvToolKinematics.ToolAxis(flow.Validator, q).ToString("F2") +
                   (tcpPoses.Count >= TcpPointsNeeded
                       ? " · точек достаточно, нажмите «Рассчитать смещение TCP»"
                       : " · нужно " + (TcpPointsNeeded - tcpPoses.Count) + " из " +
                         TcpPointsNeeded));
            return true;
        }

        public void ResetTcpPoints()
        {
            tcpPoses.Clear();
            data.tcpSolved = false;
            data.tcpPoints = 0;
            Report("калибровка TCP: точки сброшены (0 из " + TcpPointsNeeded + ")");
        }

        /// <summary>
        /// РЕШЕНИЕ СМЕЩЕНИЯ TCP по записанным точкам: метод наименьших квадратов для системы
        /// (R_i − R_1)·t = p_1 − p_i. Возвращает false с понятной причиной, если точек мало.
        /// </summary>
        public bool SolveTcp()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка TCP: робот не определён");
                return false;
            }
            if (tcpPoses.Count < TcpPointsNeeded)
            {
                Report(KvLocExtra2.T("calib.need4", "Нужно 4 точки с разными ориентациями") +
                       " (записано " + tcpPoses.Count + ")");
                return false;
            }

            PoseValidator v = flow.Validator;
            int n = tcpPoses.Count;
            var rot = new Quaternion[n];
            var pos = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                if (!FlangeFrame(v, tcpPoses[i], out pos[i], out rot[i]))
                {
                    Report("калибровка TCP: не удалось построить систему фланца для точки " + (i + 1));
                    return false;
                }
            }

            Vector3 offset;
            float rms, worst;
            if (!SolveToolOffset(pos, rot, out offset, out rms, out worst))
            {
                Report("калибровка TCP: система вырождена — ориентации точек почти совпадают, " +
                       "поверните инструмент сильнее и запишите точки заново");
                return false;
            }

            data.tcpSolved = true;
            data.tcpPoints = n;
            data.tcpOffsetFlange = new[] { offset.x, offset.y, offset.z };
            data.tcpLength = offset.magnitude;
            data.tcpResidualMm = rms * 1000f;
            data.tcpMaxResidualMm = worst * 1000f;
            data.robot = flow.Robot != null ? flow.Robot.robotName : "";
            data.created = FeatureStorage.IsoNow();

            Report(KvLocExtra2.T("calib.result", "Смещение инструмента (в системе фланца), мм") + ": (" +
                   (offset.x * 1000f).ToString("0.0") + ", " + (offset.y * 1000f).ToString("0.0") +
                   ", " + (offset.z * 1000f).ToString("0.0") + ") · " +
                   KvLocExtra2.T("calib.length", "Длина инструмента, мм") + " " +
                   (offset.magnitude * 1000f).ToString("0.0") + " · " +
                   KvLocExtra2.T("calib.residual", "Остаточная ошибка, мм") + " " +
                   (rms * 1000f).ToString("0.00") + " (худшая точка " +
                   (worst * 1000f).ToString("0.00") + ")");
            return true;
        }

        /// <summary>
        /// РЕШЕНИЕ 4-ТОЧЕЧНОЙ КАЛИБРОВКИ по системам фланца: метод наименьших квадратов для
        /// системы «(R_i − R_1)·t = p_1 − p_i» (неизвестная точка касания исключается).
        /// Вынесено отдельной функцией, чтобы метод можно было проверить на синтетических
        /// данных (см. <see cref="SelfTest"/>).
        /// </summary>
        public static bool SolveToolOffset(Vector3[] positions, Quaternion[] rotations,
            out Vector3 offset, out float rms, out float worst)
        {
            offset = Vector3.zero;
            rms = 0f;
            worst = 0f;
            if (positions == null || rotations == null) return false;
            int n = Mathf.Min(positions.Length, rotations.Length);
            if (n < 4) return false;

            // Нормальные уравнения метода наименьших квадратов.
            // Для каждой пары точек i ≥ 1: (R_i − R_0)·t = p_0 − p_i (три уравнения).
            double[,] ata = new double[3, 3];
            double[] atb = new double[3];
            double[,] r0 = MatrixOf(rotations[0]);

            for (int i = 1; i < n; i++)
            {
                double[,] ri = MatrixOf(rotations[i]);
                var m = new double[3, 3];
                for (int a = 0; a < 3; a++)
                    for (int b = 0; b < 3; b++)
                        m[a, b] = ri[a, b] - r0[a, b];

                double[] rhs =
                {
                    positions[0].x - positions[i].x,
                    positions[0].y - positions[i].y,
                    positions[0].z - positions[i].z
                };

                for (int r = 0; r < 3; r++)
                {
                    for (int a = 0; a < 3; a++)
                    {
                        for (int b = 0; b < 3; b++)
                            ata[a, b] += m[r, a] * m[r, b];
                        atb[a] += m[r, a] * rhs[r];
                    }
                }
            }
            // Небольшая регуляризация — устойчивость при почти одинаковых ориентациях.
            for (int r = 0; r < 3; r++) ata[r, r] += 1e-6;

            double[] t = Solve3(ata, atb);
            if (t == null) return false;
            offset = new Vector3((float)t[0], (float)t[1], (float)t[2]);

            // Остаток: насколько разошлись бы точки касания при найденном смещении.
            var touch = new Vector3[n];
            for (int i = 0; i < n; i++) touch[i] = positions[i] + rotations[i] * offset;
            Vector3 mean = Vector3.zero;
            for (int i = 0; i < n; i++) mean += touch[i];
            mean /= n;
            float sum = 0f;
            worst = 0f;
            for (int i = 0; i < n; i++)
            {
                float d = Vector3.Distance(touch[i], mean);
                sum += d * d;
                if (d > worst) worst = d;
            }
            rms = Mathf.Sqrt(sum / n);
            return true;
        }

        /// <summary>
        /// САМОПРОВЕРКА МЕТОДА на синтетических данных: по известному смещению инструмента
        /// строятся четыре системы фланца, касающиеся ОДНОЙ точки, и метод обязан вернуть
        /// исходное смещение. Возвращает текст отчёта для диагностики.
        /// </summary>
        public static string SelfTest()
        {
            Vector3 truth = new Vector3(0.012f, -0.035f, 0.145f);
            Vector3 point = new Vector3(0.42f, 1.03f, -23.91f);

            Quaternion[] rotations =
            {
                Quaternion.Euler(0f, 0f, 0f),
                Quaternion.Euler(0f, 35f, 0f),
                Quaternion.Euler(25f, 0f, 20f),
                Quaternion.Euler(-20f, 40f, -30f)
            };
            var positions = new Vector3[rotations.Length];
            for (int i = 0; i < rotations.Length; i++)
                positions[i] = point - rotations[i] * truth;

            Vector3 offset;
            float rms, worst;
            if (!SolveToolOffset(positions, rotations, out offset, out rms, out worst))
                return "самопроверка 4-точечной калибровки: система не решена";

            float error = Vector3.Distance(offset, truth);
            return "самопроверка 4-точечной калибровки: смещение восстановлено с ошибкой " +
                   (error * 1000f).ToString("0.000") + " мм (задано " +
                   (truth.magnitude * 1000f).ToString("0.0") + " мм, остаток " +
                   (rms * 1000f).ToString("0.000") + " мм)";
        }

        /// <summary>Матрица поворота кватерниона (столбцы — образы базисных векторов).</summary>
        private static double[,] MatrixOf(Quaternion q)
        {
            Vector3 x = q * Vector3.right;
            Vector3 y = q * Vector3.up;
            Vector3 z = q * Vector3.forward;
            return new double[3, 3]
            {
                { x.x, y.x, z.x },
                { x.y, y.y, z.y },
                { x.z, y.z, z.z }
            };
        }

        /// <summary>
        /// ПОРОГ «МАТРИЦА ВЫРОЖДЕНА» ДЛЯ МЕТОДА НАИМЕНЬШИХ КВАДРАТОВ (назван 30.10.2026, S44).
        ///
        /// ЧТО ЭТО ЧИСЛО ЗНАЧИТ. `A = Σ aᵀa` — матрица нормальных уравнений калибровки, её элементы
        /// имеют порядок метров. Определитель меньше 1e-10 означает число обусловленности порядка
        /// 1e10 и выше: точки замера лежат почти на одной прямой, и решение НЕ ОПРЕДЕЛЕНО —
        /// любая погрешность замера даёт произвольный ответ.
        ///
        /// ГРАНИЦА ЧЕСТНОСТИ: это порог ОТКАЗА, а не «точность». Он подобран по масштабу задачи
        /// (метры) и не заменяет проверку остатка — калибровка с плохим остатком обязана
        /// отвергаться отдельно (см. `residualMm`).
        /// </summary>
        private const double DegenerateNormalMatrix = 1e-10;

        /// <summary>
        /// ПОРОГ ОБРАТИМОСТИ МАТРИЦЫ 3×3 ДЛЯ СОБСТВЕННОГО ВЕКТОРА (назван 30.10.2026, S44).
        ///
        /// ПОЧЕМУ 1e-18, А НЕ «НАДЁЖНЫЕ» 1e-12 — ПРОВЕРЕНО ПРОГОНОМ, А НЕ НА ГЛАЗ.
        /// Сначала было взято 1e-12 «с запасом от численного шума». Прогон тестов показал, что
        /// так ломается РАБОЧИЙ СЛУЧАЙ: калибровка плоскости по МИНИМУМУ точек (три точки) даёт
        /// ковариационную матрицу, которая ЗАКОННО почти вырождена — три точки задают плоскость
        /// ровно, и одно из собственных значений около нуля. Порог 1e-12 отвергал валидный замер
        /// («дубликат точки не вырождает метод» и «3 точки (минимум метода)» падали).
        ///
        /// ПОЧЕМУ ЭТО НЕ ОПАСНО. Матрица перед обращением РЕГУЛЯРИЗУЕТСЯ строкой выше
        /// (`a[r,r] += trace·1e-9 + 1e-12`), поэтому деления на чистый ноль нет. Порог здесь —
        /// только защита от буквально нулевого определителя (вырожденный вход: все точки совпали),
        /// и в этом смысле 1e-18 достаточно.
        ///
        /// УРОК (записан в FIXES как E-17): «улучшить порог по интуиции» без прогона тестов —
        /// это способ сломать работающее. Порог подбирается по данным, а не по ощущению запаса.
        /// </summary>
        private const double InvertibleDeterminant = 1e-18;

        private static double[] Solve3(double[,] a, double[] b)
        {
            double det = a[0, 0] * (a[1, 1] * a[2, 2] - a[1, 2] * a[2, 1])
                       - a[0, 1] * (a[1, 0] * a[2, 2] - a[1, 2] * a[2, 0])
                       + a[0, 2] * (a[1, 0] * a[2, 1] - a[1, 1] * a[2, 0]);
            if (Math.Abs(det) < DegenerateNormalMatrix) return null;

            double[] x = new double[3];
            for (int i = 0; i < 3; i++)
            {
                double[,] m = (double[,])a.Clone();
                for (int r = 0; r < 3; r++) m[r, i] = b[r];
                double d = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                         - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                         + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
                x[i] = d / det;
            }
            return x;
        }

        // ================================================================== TCP для SCARA: по нормали к плоскости

        /// <summary>
        /// ЗАПИСАТЬ ТОЧКУ ПЛОСКОСТИ (ФИКС 3): берётся текущая поза, из неё — положение ФЛАНЦА
        /// (пивот последней оси). Оператор ставит инструмент в N ≥ 3 точки одной плоскости
        /// (например, столешницы) в разных местах рабочей зоны.
        /// </summary>
        public bool RecordTcpPlanePoint()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка TCP (по нормали): робот не определён");
                return false;
            }
            if (planeFlange.Count >= 8)
            {
                Report("калибровка TCP (по нормали): уже записано 8 точек — нажмите «Сбросить точки»");
                return false;
            }

            double[] q = flow.Validator.CopyCurrent();
            Vector3 pos;
            Quaternion rot;
            if (!FlangeFrame(flow.Validator, q, out pos, out rot))
            {
                Report("калибровка TCP (по нормали): не удалось построить систему фланца");
                return false;
            }

            // Точки не должны идти по одной линии — иначе плоскость не построить.
            string warn = "";
            if (planeFlange.Count >= 2)
            {
                Vector3 a = planeFlange[1] - planeFlange[0];
                Vector3 b = pos - planeFlange[0];
                if (Vector3.Cross(a, b).magnitude < 0.0009f)
                    warn = " · точки почти на одной линии — разведите их по плоскости";
            }

            planeFlange.Add(pos);
            planePoses.Add(q);

            Report("калибровка TCP (по нормали): точка " + planeFlange.Count + " записана · фланец " +
                   pos.x.ToString("0.000") + ", " + pos.y.ToString("0.000") + ", " +
                   pos.z.ToString("0.000") +
                   (planeFlange.Count >= TcpPlanePointsNeeded
                       ? " · точек достаточно, нажмите «Рассчитать смещение TCP по плоскости»"
                       : " · нужно ещё " + (TcpPlanePointsNeeded - planeFlange.Count)) + warn);
            return true;
        }

        /// <summary>Сбросить точки метода «по нормали».</summary>
        public void ResetTcpPlanePoints()
        {
            planeFlange.Clear();
            planePoses.Clear();
            data.tcpPlaneSolved = false;
            data.tcpPlanePoints = 0;
            Report("калибровка TCP (по нормали): точки сброшены (0 из " + TcpPlanePointsNeeded + ")");
        }

        /// <summary>
        /// РЕШЕНИЕ СМЕЩЕНИЯ TCP ПО НОРМАЛИ К ПЛОСКОСТИ (ФИКС 3).
        ///
        /// У SCARA ориентация инструмента при движении не меняется (кисти нет), поэтому
        /// `TCP_i = Фланец_i + R·t`. Все точки касания лежат в ОДНОЙ плоскости, значит фланцы
        /// тоже лежат в плоскости, параллельной опорной, сдвинутой на вектор инструмента.
        /// Отсюда:
        ///   1. по положениям фланцев строится плоскость (метод наименьших квадратов:
        ///      центроид + нормаль как собственный вектор наименьшей дисперсии);
        ///   2. смещение инструмента — вдоль ОСИ ИНСТРУМЕНТА на расстояние от фланца до этой
        ///      плоскости (знак выбирается так, чтобы TCP оказался на плоскости);
        ///   3. остаток (СКО расстояний приведённых точек до плоскости) показывается честно.
        ///
        /// Метод выделен отдельной функцией, чтобы его можно было проверить на синтетических
        /// данных (см. <see cref="SelfTestPlane"/>).
        /// </summary>
        public bool SolveTcpPlane()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка TCP (по нормали): робот не определён");
                return false;
            }
            if (planeFlange.Count < TcpPlanePointsNeeded)
            {
                Report("калибровка TCP (по нормали): нужно минимум " + TcpPlanePointsNeeded +
                       " точек плоскости (записано " + planeFlange.Count + ")");
                return false;
            }

            PoseValidator v = flow.Validator;
            Vector3 axis = KvToolKinematics.ToolAxis(v, planePoses[planePoses.Count - 1]);
            if (axis.sqrMagnitude < 1e-8f) axis = Vector3.down;
            axis.Normalize();

            Vector3 normal;
            float height;
            float residualMm;
            if (!SolvePlaneOffset(planeFlange, axis, referencePlaneHeightM, out normal, out height, out residualMm))
            {
                Report("калибровка TCP (по нормали): точки лежат на одной линии — разведите их " +
                       "по плоскости и запишите заново");
                return false;
            }

            Vector3 offset = axis * height;
            data.tcpSolved = true;
            data.tcpMethod = "plane";
            data.tcpPlaneSolved = true;
            data.tcpPlanePoints = planeFlange.Count;
            data.tcpOffsetPlane = new[] { offset.x, offset.y, offset.z };
            data.tcpPlaneNormal = new[] { normal.x, normal.y, normal.z };
            data.tcpPlaneHeight = height;
            data.tcpPlaneResidualMm = residualMm;
            data.tcpLength = Mathf.Abs(height);
            data.tcpResidualMm = residualMm;
            data.tcpMaxResidualMm = residualMm;
            data.robot = flow.Robot != null ? flow.Robot.robotName : "";
            data.created = FeatureStorage.IsoNow();

            Report("калибровка TCP по нормали к плоскости: " +
                   KvLocExtra2.T("calib.length", "Длина инструмента, мм") + " " +
                   (height * 1000f).ToString("0.0") + " · нормаль плоскости (" +
                   normal.x.ToString("0.00") + ", " + normal.y.ToString("0.00") + ", " +
                   normal.z.ToString("0.00") + ") · " +
                   KvLocExtra2.T("calib.residual", "Остаточная ошибка, мм") + " " +
                   residualMm.ToString("0.00") + " · точек " + planeFlange.Count);
            return true;
        }

        /// <summary>
        /// ВЫСОТА ОПОРНОЙ ПЛОСКОСТИ НАД ПОЛОМ, м (ФИКС 3): по умолчанию — столешница стенда
        /// (`StandBuilder.TopHeight`). Оператор касается ИМЕННО этой плоскости, поэтому её
        /// положение нужно знать: из одних только касаний высота инструмента НЕ определяется —
        /// при любой длине инструмента точки касания всё равно лежат в одной плоскости
        /// (меняется лишь положение плоскости фланцев). Это честно написано в подсказке вкладки.
        /// </summary>
        public float referencePlaneHeightM = TrajectoryCore.StandBuilder.TopHeight;

        /// <summary>
        /// ЯДРО МЕТОДА «ПО НОРМАЛИ» (ФИКС 3): по точкам ФЛАНЦЕВ и оси инструмента считается
        /// нормаль опорной плоскости, высота инструмента (смещение вдоль оси) и остаток в мм.
        ///
        ///   • нормаль — собственный вектор наименьшей дисперсии ковариационной матрицы точек
        ///     (устойчивее «векторных произведений соседних троек»: нет зависимости от того,
        ///     какой треугольник оказался «удачным»);
        ///   • высота — расстояние между плоскостью фланцев и ОПОРНОЙ плоскостью, делённое
        ///     на косинус угла между осью инструмента и нормалью (то есть отложенное вдоль оси);
        ///   • остаток — СКО расстояний приведённых точек касания до опорной плоскости:
        ///     честная мера качества калибровки.
        /// </summary>
        public static bool SolvePlaneOffset(List<Vector3> points, Vector3 toolAxis,
            float referenceHeight, out Vector3 normal, out float height, out float residualMm)
        {
            normal = Vector3.up;
            height = 0f;
            residualMm = 0f;
            if (points == null || points.Count < 3) return false;

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < points.Count; i++) centroid += points[i];
            centroid /= points.Count;

            // Нормаль — собственный вектор наименьшей дисперсии ковариационной матрицы точек.
            double[,] cov = new double[3, 3];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 d = points[i] - centroid;
                double[] a = { d.x, d.y, d.z };
                for (int r = 0; r < 3; r++)
                    for (int c = 0; c < 3; c++)
                        cov[r, c] += a[r] * a[c];
            }
            Vector3 n = SmallestEigenVector(cov);
            if (n.sqrMagnitude < 1e-10f) return false;
            n.Normalize();
            if (n.y < 0f) n = -n;             // нормаль опорной плоскости — «вверх»

            Vector3 axis = toolAxis.sqrMagnitude > 1e-8f ? toolAxis.normalized : Vector3.down;
            float along = Mathf.Abs(Vector3.Dot(axis, n));
            if (along < 0.2f) return false;   // ось инструмента почти в плоскости — метод неприменим

            // Плоскость фланцев: Dot(p, n) = flangePlane. Опорная плоскость проходит через
            // точку (0, referenceHeight, 0) с той же нормалью: Dot(p, n) = referenceHeight·n.y.
            float flangePlane = Vector3.Dot(centroid, n);
            float refPlane = referenceHeight * n.y;
            height = Mathf.Abs(flangePlane - refPlane) / along;

            // Остаток: насколько точно приведённые точки касания ложатся на опорную плоскость.
            double sum = 0.0;
            for (int i = 0; i < points.Count; i++)
            {
                float dist = Vector3.Dot(points[i] + axis * height, n) - refPlane;
                sum += dist * dist;
            }
            residualMm = Mathf.Sqrt((float)(sum / points.Count)) * 1000f;
            normal = n;
            return true;
        }

        /// <summary>Собственный вектор матрицы 3×3 для наименьшего собственного значения
        /// (обратная итерация: устойчиво для почти вырожденных матриц).</summary>
        private static Vector3 SmallestEigenVector(double[,] a)
        {
            // Обратная итерация со сдвигом: решаем (A − σI)x = b, σ чуть меньше минимального
            // собственного значения. Проще и надёжнее для нашей задачи — степенной метод
            // по обратной матрице с регуляризацией.
            double trace = a[0, 0] + a[1, 1] + a[2, 2];
            for (int r = 0; r < 3; r++) a[r, r] += trace * 1e-9 + 1e-12;

            double[,] inv = Invert3(a);
            if (inv == null) return Vector3.zero;

            var x = new double[] { 1.0, 0.7, 0.3 };
            for (int iter = 0; iter < 32; iter++)
            {
                var y = new double[3];
                for (int r = 0; r < 3; r++)
                {
                    y[r] = 0.0;
                    for (int c = 0; c < 3; c++) y[r] += inv[r, c] * x[c];
                }
                double norm = Math.Sqrt(y[0] * y[0] + y[1] * y[1] + y[2] * y[2]);
                if (norm < 1e-14) break;
                for (int r = 0; r < 3; r++) x[r] = y[r] / norm;
            }
            return new Vector3((float)x[0], (float)x[1], (float)x[2]);
        }

        private static double[,] Invert3(double[,] m)
        {
            double det = m[0, 0] * (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1])
                       - m[0, 1] * (m[1, 0] * m[2, 2] - m[1, 2] * m[2, 0])
                       + m[0, 2] * (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]);
            if (Math.Abs(det) < InvertibleDeterminant) return null;   // см. пояснение у константы
            var r = new double[3, 3];
            r[0, 0] = (m[1, 1] * m[2, 2] - m[1, 2] * m[2, 1]) / det;
            r[0, 1] = (m[0, 2] * m[2, 1] - m[0, 1] * m[2, 2]) / det;
            r[0, 2] = (m[0, 1] * m[1, 2] - m[0, 2] * m[1, 1]) / det;
            r[1, 0] = (m[1, 2] * m[2, 0] - m[1, 0] * m[2, 2]) / det;
            r[1, 1] = (m[0, 0] * m[2, 2] - m[0, 2] * m[2, 0]) / det;
            r[1, 2] = (m[0, 2] * m[1, 0] - m[0, 0] * m[1, 2]) / det;
            r[2, 0] = (m[1, 0] * m[2, 1] - m[1, 1] * m[2, 0]) / det;
            r[2, 1] = (m[0, 1] * m[2, 0] - m[0, 0] * m[2, 1]) / det;
            r[2, 2] = (m[0, 0] * m[1, 1] - m[0, 1] * m[1, 0]) / det;
            return r;
        }

        /// <summary>
        /// САМОПРОВЕРКА МЕТОДА «ПО НОРМАЛИ» на синтетических данных: задаётся плоскость и
        /// известная высота инструмента, точки фланцев строятся как «касание + ось·h».
        /// </summary>
        public static string SelfTestPlane()
        {
            float truth = 0.183f;
            float truthPlaneH = 0.98f;
            Vector3 axis = Vector3.down;
            Vector3 normal = new Vector3(0.02f, 1f, -0.03f).normalized;
            // Опорная плоскость синтетического теста проходит через точку (0, truthPlaneH, 0)
            // с заданной нормалью; точки касания строятся В этой плоскости, а точки фланца —
            // на высоту инструмента ВЫШЕ вдоль оси инструмента.
            Vector3 refPoint = new Vector3(0f, truthPlaneH, 0f);
            Vector3 u = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;
            var pts = new List<Vector3>();
            var rnd = new System.Random(7);
            Vector3 basePoint = new Vector3(0.4f, 0f, -23.9f);
            for (int i = 0; i < 5; i++)
            {
                float a = (float)(rnd.NextDouble() - 0.5) * 0.4f;
                float b = (float)(rnd.NextDouble() - 0.5) * 0.4f;
                Vector3 onPlane = refPoint + basePoint + u * a + v * b;
                onPlane += normal * Vector3.Dot(refPoint - onPlane, normal);   // точно в плоскости
                pts.Add(onPlane + axis * truth);
            }

            Vector3 n;
            float h, res;
            if (!SolvePlaneOffset(pts, axis, truthPlaneH, out n, out h, out res))
                return "самопроверка метода «по нормали»: плоскость не построена";

            return "самопроверка метода «по нормали»: высота восстановлена " +
                   (h * 1000f).ToString("0.000") + " мм (задано " + (truth * 1000f).ToString("0.0") +
                   " мм), наклон нормали " + Vector3.Angle(n, normal).ToString("0.000") +
                   "°, остаток " + res.ToString("0.000") + " мм";
        }

        // ================================================================== база робота

        /// <summary>Записать точку касания опорной плоскости (столешницы) текущим TCP.</summary>
        public bool RecordBasePoint()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка базы: робот не определён");
                return false;
            }
            if (basePoints.Count >= 8)
            {
                Report("калибровка базы: уже записано 8 точек — нажмите «Сбросить точки»");
                return false;
            }

            double[] q = flow.Validator.CopyCurrent();
            Vector3 p = flow.Validator.TcpAt(q);
            basePoints.Add(p);
            basePoses.Add(q);

            // Предупреждаем, если точки идут почти по одной линии — плоскость по ним не построить.
            string warn = "";
            if (basePoints.Count >= 3)
            {
                Vector3 a = basePoints[1] - basePoints[0];
                Vector3 b = basePoints[2] - basePoints[0];
                float area = Vector3.Cross(a, b).magnitude;
                if (area < 0.0009f)
                    warn = " · точки почти на одной линии — разведите их по плоскости";
            }

            Report("калибровка базы: точка " + basePoints.Count + " записана · TCP " +
                   p.x.ToString("0.000") + ", " + p.y.ToString("0.000") + ", " + p.z.ToString("0.000") +
                   (basePoints.Count >= 3 ? " · можно рассчитать базу" : " · нужно ещё " +
                       (3 - basePoints.Count)) + warn);
            return true;
        }

        public void ResetBasePoints()
        {
            basePoints.Clear();
            basePoses.Clear();
            data.baseSolved = false;
            data.basePoints = 0;
            Report("калибровка базы: точки сброшены");
        }

        /// <summary>
        /// РЕШЕНИЕ КАЛИБРОВКИ БАЗЫ: по точкам касания плоскости считается её нормаль,
        /// высота базы над плоскостью и наклон оси базы к нормали, а также предлагаемое
        /// смещение/доворот базы в мире.
        /// </summary>
        public bool SolveBase()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка базы: робот не определён");
                return false;
            }
            if (basePoints.Count < 3)
            {
                Report("калибровка базы: нужно минимум 3 точки на плоскости (записано " +
                       basePoints.Count + ")");
                return false;
            }

            // Нормаль плоскости: усредняем векторные произведения соседних троек точек —
            // так результат не зависит от того, какой именно треугольник «удачный».
            Vector3 normal = Vector3.zero;
            int used = 0;
            for (int i = 0; i + 2 < basePoints.Count; i++)
            {
                Vector3 a = basePoints[i + 1] - basePoints[i];
                Vector3 b = basePoints[i + 2] - basePoints[i];
                Vector3 c = Vector3.Cross(a, b);
                if (c.magnitude < 1e-5f) continue;
                normal += c.normalized;
                used++;
            }
            if (used == 0 || normal.magnitude < 1e-4f)
            {
                Report("калибровка базы: точки лежат на одной линии — разведите их по плоскости " +
                       "и запишите заново");
                return false;
            }
            normal.Normalize();

            Vector3 centroid = Vector3.zero;
            for (int i = 0; i < basePoints.Count; i++) centroid += basePoints[i];
            centroid /= basePoints.Count;

            Vector3 baseOrigin = flow.Robot != null
                ? flow.Robot.transform.position
                : flow.Validator.BasePosition;
            Vector3 baseUp = flow.Robot != null ? flow.Robot.transform.up : Vector3.up;

            float height = Vector3.Dot(baseOrigin - centroid, normal);
            float tilt = Vector3.Angle(baseUp, normal);

            // Знак: нормаль ориентируем «в сторону базы», чтобы высота была положительной.
            if (height < 0f)
            {
                normal = -normal;
                height = -height;
                tilt = Vector3.Angle(baseUp, normal);
            }

            // Предлагаемый доворот базы: убрать наклон оси базы к нормали плоскости.
            Quaternion fix = Quaternion.FromToRotation(baseUp, normal);
            Vector3 offset = normal * height;

            // --- ФИКС 4: ПОВОРОТ БАЗЫ ВОКРУГ ВЕРТИКАЛИ.
            // Плоскость поворот не задаёт (она симметрична относительно нормали), поэтому
            // нужна ВТОРАЯ точка отсчёта — направление «вперёд» робота в реальной сцене.
            // По двум направлениям (нормаль + «вперёд») строится ПОЛНАЯ ориентация базы,
            // и доворот считается как разность с текущей ориентацией робота.
            float yaw = 0f;
            if (baseForwardSet)
            {
                Vector3 fwdOnPlane = Vector3.ProjectOnPlane(baseForward, normal);
                if (fwdOnPlane.sqrMagnitude > 1e-8f)
                {
                    fwdOnPlane.Normalize();
                    Vector3 curFwd = Vector3.ProjectOnPlane(flow.Robot != null
                        ? flow.Robot.transform.forward : Vector3.forward, normal);
                    if (curFwd.sqrMagnitude > 1e-8f)
                    {
                        curFwd.Normalize();
                        yaw = Vector3.SignedAngle(curFwd, fwdOnPlane, normal);
                    }
                }
            }

            // Итоговый доворот: сначала убрать наклон, затем повернуть вокруг нормали на yaw.
            Quaternion yawFix = Quaternion.AngleAxis(yaw, normal);
            Quaternion total = yawFix * fix;
            Vector3 euler = total.eulerAngles;

            data.baseSolved = true;
            data.basePoints = basePoints.Count;
            data.baseHeightMm = height * 1000f;
            data.baseTiltDeg = tilt;
            data.basePlaneNormal = new[] { normal.x, normal.y, normal.z };
            data.baseOffsetWorld = new[] { offset.x, offset.y, offset.z };
            data.baseEulerWorld = new[] { euler.x, euler.y, euler.z };
            data.baseYawDeg = yaw;
            data.baseForwardSet = baseForwardSet;
            if (baseForwardSet)
                data.baseForwardWorld = new[] { baseForward.x, baseForward.y, baseForward.z };
            data.robot = flow.Robot != null ? flow.Robot.robotName : "";
            data.created = FeatureStorage.IsoNow();

            Report(KvLocExtra2.F("calib.base.result", "База: высота {0} мм · наклон {1}° · поворот {2}°",
                       (height * 1000f).ToString("0.0"), tilt.ToString("0.00"),
                       yaw.ToString("0.0")) +
                   " · точек " + basePoints.Count +
                   (baseForwardSet
                       ? " · направление «вперёд» задано (поворот определён по двум точкам отсчёта)"
                       : " · направление «вперёд» НЕ задано: поворот вокруг вертикали не определён " +
                         "(нужна вторая точка отсчёта)"));
            return true;
        }

        /// <summary>
        /// ЗАПИСАТЬ НАПРАВЛЕНИЕ «ВПЕРЁД» БАЗЫ (ФИКС 4): оператор ставит инструмент в точку на
        /// оси X базы робота (направление «вперёд» робота) и нажимает кнопку. Без этой точки
        /// поворот базы вокруг вертикали не определяется — плоскость его не задаёт.
        /// </summary>
        public bool RecordBaseDirection()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("калибровка базы: робот не определён");
                return false;
            }

            Vector3 baseOrigin = flow.Robot != null
                ? flow.Robot.transform.position
                : flow.Validator.BasePosition;
            double[] q = flow.Validator.CopyCurrent();
            Vector3 point = flow.Validator.TcpAt(q);

            Vector3 dir = point - baseOrigin;
            dir.y = 0f;
            if (dir.sqrMagnitude < 1e-6f)
            {
                Report("калибровка базы: точка «вперёд» совпала с осью базы — отведите инструмент " +
                       "в сторону (нужно направление, а не положение)");
                return false;
            }

            baseForward = dir.normalized;
            baseForwardSet = true;
            data.baseForwardSet = true;
            data.baseForwardWorld = new[] { baseForward.x, baseForward.y, baseForward.z };
            data.baseForwardPose = new[]
            {
                (float)q[0], q.Length > 1 ? (float)q[1] : 0f, q.Length > 2 ? (float)q[2] : 0f,
                q.Length > 3 ? (float)q[3] : 0f, q.Length > 4 ? (float)q[4] : 0f,
                q.Length > 5 ? (float)q[5] : 0f
            };

            Report("калибровка базы: направление «вперёд» записано (" +
                   baseForward.x.ToString("0.000") + ", " + baseForward.y.ToString("0.000") + ", " +
                   baseForward.z.ToString("0.000") + ") · теперь «Рассчитать базу» определит и " +
                   "поворот вокруг вертикали");
            return true;
        }

        /// <summary>Сбросить направление «вперёд» (вторая точка отсчёта).</summary>
        public void ResetBaseDirection()
        {
            baseForward = Vector3.zero;
            baseForwardSet = false;
            data.baseForwardSet = false;
            data.baseYawDeg = 0f;
            Report("калибровка базы: направление «вперёд» сброшено (поворот снова не определён)");
        }

        /// <summary>Задано ли направление «вперёд» (для интерфейса).</summary>
        public bool BaseForwardSet { get { return baseForwardSet; } }

        // ================================================================== применение и файл

        /// <summary>
        /// Применить найденное смещение инструмента к потоку: проект ведёт TCP смещением
        /// вдоль направления (`toolOffset`), поэтому применяется ДЛИНА инструмента —
        /// направление задано моделью робота. Полный вектор остаётся в файле калибровки.
        /// </summary>
        public bool ApplyToolOffsetToFlow()
        {
            if (!data.tcpSolved || flow == null)
            {
                Report(KvLocExtra2.T("calib.none", "калибровка не выполнялась"));
                return false;
            }
            flow.toolOffset = data.tcpLength;
            if (data.tcpMethod == "plane")
                Report("калибровка TCP (метод «по нормали к плоскости»): высота инструмента " +
                       (data.tcpPlaneHeight * 1000f).ToString("0.0") + " мм по " +
                       data.tcpPlanePoints + " точкам, остаток " +
                       data.tcpPlaneResidualMm.ToString("0.00") + " мм");
            Report("калибровка: toolOffset потока = " + (data.tcpLength * 1000f).ToString("0.0") +
                   " мм (полный вектор смещения — в файле калибровки: " +
                   (data.tcpOffsetFlange[0] * 1000f).ToString("0.0") + ", " +
                   (data.tcpOffsetFlange[1] * 1000f).ToString("0.0") + ", " +
                   (data.tcpOffsetFlange[2] * 1000f).ToString("0.0") + " мм)");
            return true;
        }

        /// <summary>Применить найденную коррекцию базы к роботу в сцене (runtime, не в файл сцены).</summary>
        public bool ApplyBaseToScene()
        {
            if (!data.baseSolved || flow == null || flow.Robot == null)
            {
                Report(KvLocExtra2.T("calib.none", "калибровка не выполнялась"));
                return false;
            }
            Transform t = flow.Robot.transform;
            Vector3 offset = new Vector3(data.baseOffsetWorld[0], data.baseOffsetWorld[1],
                data.baseOffsetWorld[2]);
            t.position += offset;
            t.rotation = Quaternion.Euler(data.baseEulerWorld[0], data.baseEulerWorld[1],
                data.baseEulerWorld[2]) * t.rotation;
            Report("калибровка базы применена к сцене: смещение " +
                   (offset.magnitude * 1000f).ToString("0.0") + " мм, доворот " +
                   data.baseTiltDeg.ToString("0.00") + "° (это изменение рантайма — " +
                   "сцену не сохраняем)");
            return true;
        }

        /// <summary>Сохранить калибровку в файл JSON (ТЗ: «Сохранение калибровки в файл»).</summary>
        public bool Save()
        {
            data.robot = flow != null && flow.Robot != null ? flow.Robot.robotName : data.robot;
            data.created = FeatureStorage.IsoNow();
            bool ok = FeatureStorage.SaveJson(FilePath, data);
            Report(ok
                ? KvLocExtra2.T("calib.saved", "калибровка сохранена") + ": " + FilePath
                : "калибровку сохранить не удалось: " + FilePath);
            return ok;
        }

        /// <summary>Загрузить калибровку из файла (если он есть).</summary>
        public bool Load()
        {
            if (loadedFromFile) return data.tcpSolved || data.baseSolved;
            loadedFromFile = true;
            KvCalibrationData fromDisk = FeatureStorage.LoadJson<KvCalibrationData>(FilePath);
            if (fromDisk == null) return false;
            data = fromDisk;
            if (data.tcpOffsetFlange == null) data.tcpOffsetFlange = new float[3];
            if (data.baseOffsetWorld == null) data.baseOffsetWorld = new float[3];
            if (data.baseEulerWorld == null) data.baseEulerWorld = new float[3];
            if (data.basePlaneNormal == null) data.basePlaneNormal = new float[3];
            if (data.tcpOffsetPlane == null) data.tcpOffsetPlane = new float[3];
            if (data.tcpPlaneNormal == null) data.tcpPlaneNormal = new float[3];
            if (data.baseForwardWorld == null) data.baseForwardWorld = new float[3];
            if (data.baseForwardPose == null) data.baseForwardPose = new float[6];
            if (string.IsNullOrEmpty(data.tcpMethod)) data.tcpMethod = "4point";
            baseForwardSet = data.baseForwardSet;
            if (baseForwardSet)
                baseForward = new Vector3(data.baseForwardWorld[0], data.baseForwardWorld[1],
                    data.baseForwardWorld[2]);
            Report("калибровка прочитана из файла: TCP " +
                   (data.tcpSolved
                       ? (data.tcpLength * 1000f).ToString("0.0") + " мм (остаток " +
                         data.tcpResidualMm.ToString("0.00") + " мм, точек " + data.tcpPoints + ")"
                       : "не выполнялась") +
                   " · база " + (data.baseSolved
                       ? data.baseHeightMm.ToString("0.0") + " мм / " +
                         data.baseTiltDeg.ToString("0.00") + "°"
                       : "не выполнялась"));
            return true;
        }

        /// <summary>Отметить калибровку камеры как «заглушку» (ТЗ: заглушка на будущее).</summary>
        public void MarkCameraStub()
        {
            data.cameraCalibrated = false;
            data.cameraNote = "заглушка: ждём модель крепления камеры (hand-eye)";
            Report("калибровка камеры: " + data.cameraNote);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Calibration] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «КАЛИБРОВОЧНЫЙ МАСТЕР» (ЭТАП 9 ТЗ): пошаговые шаги TCP (4 точки), базы
    /// (3+ точки) и камеры (заглушка) с сохранением результата в файл.
    /// </summary>
    public class KvCalibrationTab : IKvWorkbenchTab
    {
        private readonly KvCalibrationService service;

        public KvCalibrationTab(KvCalibrationService calibration)
        {
            service = calibration;
        }

        public string Key { get { return "calibration"; } }
        public string Title { get { return KvLocExtra2.T("calib.title", "Калибровочный мастер"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra2.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;

            // ---------------------------------------------------------- шаг 1: TCP
            kit.Section(T("calib.tcp", "Калибровка TCP (4 точки)"));
            kit.Info(delegate
            {
                return T("calib.points", "Записано точек") + ": " + service.TcpPointCount + " / " +
                       KvCalibrationService.TcpPointsNeeded +
                       (service.TcpPointCount >= KvCalibrationService.TcpPointsNeeded
                           ? " · ✓" : "");
            }, KvTheme.Accent);
            kit.Note(T("calib.tcp.text",
                "Наведите TCP инструмента в одну и ту же точку пространства ЧЕТЫРЬМЯ разными " +
                "ориентациями и нажмите «Записать точку» после каждого наведения."), KvTheme.TextDim);
            kit.Buttons(new[]
            {
                T("calib.record", "Записать точку"),
                T("calib.reset", "Сбросить точки")
            }, new Action[]
            {
                delegate { service.RecordTcpPoint(); },
                delegate { service.ResetTcpPoints(); }
            });
            kit.Buttons(new[]
            {
                T("calib.solve", "Рассчитать смещение TCP"),
                T("calib.apply", "Применить смещение к потоку (toolOffset)")
            }, new Action[]
            {
                delegate { service.SolveTcp(); },
                delegate { service.ApplyToolOffsetToFlow(); }
            });
            kit.Table(T("calib.result", "Смещение инструмента (в системе фланца), мм"),
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.tcpSolved
                        ? (d.tcpOffsetFlange[0] * 1000f).ToString("0.0") + ", " +
                          (d.tcpOffsetFlange[1] * 1000f).ToString("0.0") + ", " +
                          (d.tcpOffsetFlange[2] * 1000f).ToString("0.0")
                        : "—";
                },
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.tcpSolved
                        ? T("calib.length", "Длина инструмента, мм") + " " +
                          (d.tcpLength * 1000f).ToString("0.0")
                        : T("calib.none", "калибровка не выполнялась");
                });
            kit.Table(T("calib.residual", "Остаточная ошибка, мм"),
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.tcpSolved ? d.tcpResidualMm.ToString("0.00") : "—";
                },
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.tcpSolved ? "худшая " + d.tcpMaxResidualMm.ToString("0.00") : "—";
                });

            // ---------------------------------------------------------- шаг 1б: TCP для SCARA
            // ФИКС 3: у SCARA кисти нет, 4-точечный метод вырожден — применяется метод
            // «по нормали к плоскости». Метод выбирается АВТОМАТИЧЕСКИ по типу робота.
            kit.Divider();
            kit.Section(T("calib.tcp.plane", "Калибровка TCP по нормали к плоскости (SCARA)"));
            kit.Info(delegate
            {
                return T("calib.method", "Действующий метод") + ": " + service.TcpMethodLabel +
                       " · " + T("calib.points", "Записано точек") + ": " +
                       service.TcpPlanePointCount + " / " +
                       KvCalibrationService.TcpPlanePointsNeeded +
                       (service.BaseForwardSet ? "" : "");
            }, service.UsePlaneMethod ? KvTheme.Accent : KvTheme.TextDim);
            kit.Note(T("calib.tcp.plane.text",
                "У SCARA нет кисти, поэтому ориентация инструмента не меняется и 4-точечный метод " +
                "неприменим. Поставьте инструмент в 3+ точки ОДНОЙ плоскости (например, " +
                "столешницы) в разных местах рабочей зоны и запишите их: система построит " +
                "плоскость и найдёт высоту инструмента (смещение TCP вдоль оси)."),
                KvTheme.TextDim);
            kit.Buttons(new[]
            {
                T("calib.record", "Записать точку"),
                T("calib.reset", "Сбросить точки"),
                T("calib.solve.plane", "Рассчитать смещение TCP по плоскости")
            }, new Action[]
            {
                delegate { service.RecordTcpPlanePoint(); },
                delegate { service.ResetTcpPlanePoints(); },
                delegate { service.SolveTcpPlane(); }
            });
            kit.Table(T("calib.tcp.plane.result", "Высота инструмента (метод «по нормали»), мм"),
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.tcpPlaneSolved ? (d.tcpPlaneHeight * 1000f).ToString("0.0") : "—";
                },
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.tcpPlaneSolved
                        ? T("calib.residual", "Остаточная ошибка, мм") + " " +
                          d.tcpPlaneResidualMm.ToString("0.00")
                        : T("calib.none", "калибровка не выполнялась");
                });
            kit.Buttons(new[] { T("calib.selftest.plane", "Самопроверка метода «по нормали»") },
                new Action[] { delegate { Debug.Log("[Calibration] " + KvCalibrationService.SelfTestPlane()); } });
            kit.Info(delegate
            {
                return service.UsePlaneMethod
                    ? T("calib.method.auto.scara",
                        "Метод «по нормали» выбран автоматически: это SCARA (кисти нет).")
                    : T("calib.method.auto.robot",
                        "Сейчас выбран робот с кистью — используется 4-точечный метод; " +
                        "метод «по нормали» доступен переключателем ниже.");
            }, KvTheme.TextDim);
            kit.Toggle(T("calib.method.force.plane", "Всегда применять метод «по нормали»"),
                service.PlaneMethodOverride, delegate (bool v) { service.PlaneMethodOverride = v; });
            // ---------------------------------------------------------- шаг 2: база
            kit.Divider();
            kit.Section(T("calib.base", "Калибровка базы робота"));
            kit.Info(delegate
            {
                return T("calib.points", "Записано точек") + ": " + service.BasePointCount + " / 3+";
            }, KvTheme.Accent);
            kit.Note(T("calib.base.text",
                "Коснитесь инструментом ТРЁХ точек на одной плоскости (например, столешницы) и " +
                "запишите их: система определит высоту базы над плоскостью и её наклон."),
                KvTheme.TextDim);
            kit.Buttons(new[]
            {
                T("calib.record", "Записать точку"),
                T("calib.reset", "Сбросить точки"),
                T("calib.solve", "Рассчитать базу")
            }, new Action[]
            {
                delegate { service.RecordBasePoint(); },
                delegate { service.ResetBasePoints(); },
                delegate { service.SolveBase(); }
            });
            kit.Table(T("calib.base.result", "База"),
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.baseSolved ? d.baseHeightMm.ToString("0.0") + " мм" : "—";
                },
                delegate
                {
                    KvCalibrationData d = service.Data;
                    return d.baseSolved ? d.baseTiltDeg.ToString("0.00") + "°" : "—";
                });
            // ФИКС 4: вторая точка отсчёта — направление «вперёд» базы. Без неё плоскость
            // не задаёт поворот вокруг вертикали, и это честно показывается оператору.
            kit.Note(T("calib.base.dir.text",
                "Поворот базы вокруг вертикали плоскость не задаёт. Поставьте инструмент в точку " +
                "на оси X базы (направление «вперёд» робота) и нажмите «Записать направление» — " +
                "по двум точкам отсчёта (нормаль + «вперёд») система построит ПОЛНУЮ ориентацию " +
                "базы и определит доворот."), KvTheme.TextDim);
            kit.Buttons(new[]
            {
                T("calib.base.dir.record", "Записать направление «вперёд»"),
                T("calib.base.dir.reset", "Сбросить направление")
            }, new Action[]
            {
                delegate { service.RecordBaseDirection(); },
                delegate { service.ResetBaseDirection(); }
            });
            kit.Info(delegate
            {
                KvCalibrationData d = service.Data;
                return service.BaseForwardSet
                    ? T("calib.base.yaw", "Доворот базы вокруг вертикали, °") + ": " +
                      d.baseYawDeg.ToString("0.00")
                    : "⚠ " + T("calib.base.yaw.none",
                        "направление «вперёд» не задано — поворот вокруг вертикали не определён");
            }, service.BaseForwardSet ? KvTheme.Ok : KvTheme.Warn);            kit.Buttons(new[] { T("calib.apply", "Применить базу к сцене (runtime)") },
                new Action[] { delegate { service.ApplyBaseToScene(); } });

            // ---------------------------------------------------------- шаг 3: камера
            kit.Divider();
            kit.Section(T("calib.camera", "Калибровка камеры (hand-eye)"));
            kit.Note(T("calib.camera.text",
                "Заглушка на будущее: потребуется модель крепления камеры. Сейчас сохраняется " +
                "намерение калибровки и занятое место в файле."), KvTheme.Warn);
            kit.Buttons(new[] { T("calib.camera", "Отметить как заглушку") },
                new Action[] { delegate { service.MarkCameraStub(); } });

            // ---------------------------------------------------------- файл
            kit.Divider();
            kit.Buttons(new[] { T("calib.save", "Сохранить калибровку в файл") },
                new Action[] { delegate { service.Save(); } });
            kit.Info(delegate { return T("calib.file", "Файл калибровки") + ": " + service.FilePath; },
                KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
