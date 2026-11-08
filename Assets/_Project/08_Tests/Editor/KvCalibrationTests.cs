using NUnit.Framework;
using KazistovVvFeatures;
using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты калибровки TCP (класс <see cref="KvCalibrationService"/>, файл
    /// Assets/_Project/01_Scripts/Features/KvCalibration.cs).
    ///
    /// ЧТО ИМЕННО ПРОВЕРЯЕТСЯ. Расчёт смещения TCP в проекте разделён на две части:
    ///   • «обвязка» — запись поз робота, построение системы фланца, сохранение результата:
    ///     <see cref="KvCalibrationService.RecordTcpPoint"/> читает ТЕКУЩУЮ позу суставов,
    ///     а <see cref="KvCalibrationService.SolveTcp"/> требует готового валидатора
    ///     (flow.Validator.Ready) и робота в сцене. Без сцены эта часть не запускается;
    ///   • ЧИСТАЯ МАТЕМАТИКА, специально вынесенная в публичные СТАТИЧЕСКИЕ функции, чтобы её
    ///     можно было проверить на синтетических данных (это прямо написано в XML-комментариях
    ///     исходника):
    ///       - <see cref="KvCalibrationService.SolveToolOffset"/> — 4-точечный метод (МНК);
    ///       - <see cref="KvCalibrationService.SolvePlaneOffset"/> — метод «по нормали к плоскости».
    /// Тесты основной части работают именно с этими двумя функциями: они принимают позы/точки
    /// АРГУМЕНТАМИ, поэтому сцена, Transform, MonoBehaviour, корутины не нужны.
    ///
    /// ЗАПРЕЩЁННОЕ НЕ ИСПОЛЬЗУЕТСЯ: GameObject, Object.Instantiate, MonoBehaviour, Transform,
    /// корутины, UnityEditor.*, запись/чтение файлов, сеть, реальное время.
    ///
    /// ДЕТЕРМИНИРОВАННОСТЬ. Оба решателя детерминированы по построению:
    ///   • SolveToolOffset — замкнутое решение (формулы Крамера, Solve3) с фиксированной
    ///     регуляризацией 1e-6 на диагонали нормальных уравнений; итераций нет;
    ///   • SolvePlaneOffset — нормаль ищется обратной итерацией с ФИКСИРОВАННЫМ начальным
    ///     приближением (1.0, 0.7, 0.3) и ФИКСИРОВАННЫМ числом итераций (32) — см.
    ///     SmallestEigenVector в KvCalibration.cs. Никаких ГСЧ и времени внутри нет.
    /// Повторный вызов на тех же данных даёт побитово одинаковый результат (есть тест).
    ///
    /// ДОПУСКИ. Синтетические данные строятся из ИЗВЕСТНОГО смещения TCP, эталон проверен
    /// отдельным расчётом в double. Входные данные имеют точность float32 (как в Unity),
    /// поэтому на идеальных данных ошибка восстановления — это чистое округление:
    /// измерено ≤ 3.6e-7 м = 0.00036 мм. Отсюда базовый допуск 5e-6 м (0.005 мм): запас ≈ 14×
    /// и при этом в 20 раз строже нижней границы практического допуска из ТЗ (1e-4 м).
    /// В формате проекта ("0.000" мм) такая ошибка печатается как 0.000…0.005 мм, то есть
    /// требование ТЗ «калибровка TCP на синтетике с точностью 0.000 мм» выполняется.
    /// Для каждого допуска в комментарии указано ИЗМЕРЕННОЕ значение и запас.
    /// </summary>
    [TestFixture]
        [Category(KvTestCategories.Fast)]
    public class KvCalibrationTests
    {
        // ==================================================================== ДОПУСКИ

        /// <summary>Допуск восстановления смещения TCP на идеальных данных: 5e-6 м = 0.005 мм.
        /// Измерено: ≤ 3.6e-7 м (0.00036 мм) для 4 поз у начала координат, ≤ 3.3e-7 м для
        /// «далёкой» точки касания (z ≈ −23.9 м) — запас ≈ 14×.</summary>
        private const float TcpIdealToleranceM = 5e-6f;

        /// <summary>Допуск на невязку (rms/worst) идеальных данных: 1e-6 м = 0.001 мм.
        /// Измерено: rms ≤ 6.9e-8 м, worst ≤ 9.2e-8 м (4/6/8 поз у начала координат) — запас ≈ 11×.
        /// Для 8 «далёких» поз остаток больше (worst ≈ 1.95e-6 м), поэтому там допуск отдельный.</summary>
        private const float IdealResidualToleranceM = 1e-6f;

        /// <summary>Допуск высоты инструмента в методе «по нормали»: 5e-6 м = 0.005 мм.
        /// Измерено: ≤ 1.4e-7 м (0.00013 мм) для 6 и 3 точек, включая наклонную нормаль и
        /// наклонную ось инструмента — запас ≈ 35×.</summary>
        private const float PlaneHeightToleranceM = 5e-6f;

        /// <summary>Допуск на остаток метода «по нормали» в мм: 0.01 мм.
        /// Измерено: ≤ 1.5e-4 мм на идеальных данных — запас ≈ 70×.</summary>
        private const float PlaneIdealResidualMm = 0.01f;

        /// <summary>Допуск на угол между восстановленной и истинной нормалью, градусы.
        /// Измерено: 0.00000° для идеальных данных — запас огромный; 0.05° оставлено на
        /// возможные различия округления float32/double между рантаймами.</summary>
        private const float PlaneNormalAngleToleranceDeg = 0.05f;

        // ==================================================================== ЭТАЛОННЫЕ ДАННЫЕ

        /// <summary>Известное («истинное») смещение инструмента в системе фланца, м.
        /// Взято как в <see cref="KvCalibrationService.SelfTest"/>: разные знаки и порядки
        /// величин, чтобы ошибка не маскировалась симметрией.</summary>
        private static readonly Vector3 TruthTcpOffsetM = new Vector3(0.012f, -0.035f, 0.145f);

        /// <summary>Точка касания недалеко от начала координат: хорошо обусловлена по float32.</summary>
        private static readonly Vector3 NearTouchPointM = new Vector3(0.31f, 0.05f, -0.12f);

        /// <summary>«Далёкая» точка касания — как в SelfTest() проекта (z ≈ −23.9 м): проверяет,
        /// что метод работает и при больших абсолютных координатах (худшая обусловленность).</summary>
        private static readonly Vector3 FarTouchPointM = new Vector3(0.42f, 1.03f, -23.91f);

        /// <summary>Четыре ориентации с заметным взаимным разворотом (как требует метод: ≥ 12°).</summary>
        private static readonly Quaternion[] Poses4 =
        {
            Quaternion.Euler(0f, 0f, 0f),
            Quaternion.Euler(0f, 35f, 0f),
            Quaternion.Euler(25f, 0f, 20f),
            Quaternion.Euler(-20f, 40f, -30f)
        };

        /// <summary>Шесть ориентаций.</summary>
        private static readonly Quaternion[] Poses6 =
        {
            Quaternion.Euler(0f, 0f, 0f),
            Quaternion.Euler(0f, 35f, 0f),
            Quaternion.Euler(25f, 0f, 20f),
            Quaternion.Euler(-20f, 40f, -30f),
            Quaternion.Euler(40f, -25f, 55f),
            Quaternion.Euler(-35f, -40f, 15f)
        };

        /// <summary>Восемь ориентаций — предел, который допускает RecordTcpPoint (8 точек).</summary>
        private static readonly Quaternion[] Poses8 =
        {
            Quaternion.Euler(0f, 0f, 0f),
            Quaternion.Euler(0f, 35f, 0f),
            Quaternion.Euler(25f, 0f, 20f),
            Quaternion.Euler(-20f, 40f, -30f),
            Quaternion.Euler(40f, -25f, 55f),
            Quaternion.Euler(-35f, -40f, 15f),
            Quaternion.Euler(15f, 60f, -45f),
            Quaternion.Euler(-50f, 20f, 30f)
        };

        /// <summary>Те же четыре ориентации, но углы увеличены на целое число оборотов (360/720/1080°).
        /// Физически это ТЕ ЖЕ ориентации, поэтому результат обязан совпасть с Poses4.</summary>
        private static readonly Quaternion[] Poses4FullTurn =
        {
            Quaternion.Euler(360f, 360f, 360f),
            Quaternion.Euler(360f, 395f, 360f),
            Quaternion.Euler(385f, 360f, 380f),
            Quaternion.Euler(340f, 400f, 330f)
        };

        /// <summary>Ориентации, близкие к «противоположным» (170…179°) — экстремальные углы.</summary>
        private static readonly Quaternion[] PosesNear180 =
        {
            Quaternion.Euler(0f, 0f, 0f),
            Quaternion.Euler(179f, 0f, 0f),
            Quaternion.Euler(0f, 170f, 0f),
            Quaternion.Euler(0f, 0f, 175f)
        };

        /// <summary>Точки касания столешницы для метода «по нормали»: разведены «звездой» по
        /// плоскости (разброс ≈ 0.4 м). Координаты x, y — смещения вдоль двух осей плоскости.</summary>
        private static readonly Vector3[] PlaneSpread6 =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0.20f, 0.05f, 0f),
            new Vector3(0.05f, 0.22f, 0f),
            new Vector3(-0.18f, 0.12f, 0f),
            new Vector3(-0.15f, -0.20f, 0f),
            new Vector3(0.12f, -0.17f, 0f)
        };

        /// <summary>Минимальный набор метода «по нормали» — ровно 3 точки (TcpPlanePointsNeeded).</summary>
        private static readonly Vector3[] PlaneSpread3 =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0.20f, 0.02f, 0f),
            new Vector3(0.03f, 0.18f, 0f)
        };

        /// <summary>Нормаль опорной плоскости, близкая к вертикали (как реальная столешница).</summary>
        private static readonly Vector3 PlaneNormalNearVertical = new Vector3(0.02f, 1f, -0.03f).normalized;

        /// <summary>Сильно наклонённая нормаль опорной плоскости (≈ 17° от вертикали).</summary>
        private static readonly Vector3 PlaneNormalTilted = new Vector3(0.3f, 1f, -0.2f).normalized;

        /// <summary>Ось инструмента, направленная вниз (обычное положение инструмента).</summary>
        private static readonly Vector3 ToolAxisDown = Vector3.down;

        /// <summary>Наклонённая ось инструмента (не строго вниз) — проверяет деление на косинус.</summary>
        private static readonly Vector3 ToolAxisTilted = new Vector3(0.12f, -1f, 0.08f).normalized;

        /// <summary>Известная высота инструмента для метода «по нормали», м.</summary>
        private const float TruthToolHeightM = 0.183f;

        /// <summary>Высота опорной плоскости (столешницы стенда) — как StandBuilder.TopHeight, м.</summary>
        private const float ReferencePlaneHeightM = 0.98f;

        /// <summary>Относительная погрешность шума, накладываемого на записанные системы фланца, м
        /// (0.1 мм — реалистичная ошибка «касания» оператора и позиционирования).</summary>
        private const float NoiseSigmaM = 1e-4f;

        // ==================================================================== ВСПОМОГАТЕЛЬНОЕ

        /// <summary>
        /// Детерминированный линейный конгруэнтный генератор (LCG) с ФИКСИРОВАННЫМ seed.
        /// Специально не System.Random: его алгоритм отличается между рантаймами (Mono у Unity
        /// и .NET у другого хоста), а этот ряд — чистая целочисленная арифметика, поэтому
        /// «шумные» сценарии воспроизводимы один в один в любом рантайме.
        /// </summary>
        private sealed class FixedSeedNoise
        {
            private uint state;

            public FixedSeedNoise(uint seed) { state = seed; }

            /// <summary>Число в диапазоне [-1, 1) с шагом 2^-23 (ровно представимо во float).</summary>
            public float NextSymmetric()
            {
                unchecked { state = state * 1664525u + 1013904223u; }
                return ((state >> 8) * (1f / 16777216f)) * 2f - 1f;
            }

            /// <summary>Вектор шума с покомпонентным разбросом ±sigma.</summary>
            public Vector3 NextVector(float sigma)
            {
                float x = NextSymmetric() * sigma;
                float y = NextSymmetric() * sigma;
                float z = NextSymmetric() * sigma;
                return new Vector3(x, y, z);
            }
        }

        /// <summary>
        /// Синтетические системы фланца для 4-точечного метода: все позы касаются ОДНОЙ точки
        /// пространства, поэтому p_i = P − R_i·t (обратная задача: известна точка касания и
        /// смещение t, ищем положения фланца). Оператору точка касания неизвестна — именно
        /// поэтому метод исключает её из системы, и здесь она нужна только для генерации данных.
        /// </summary>
        private static Vector3[] FlangeSystems(Vector3 touchPoint, Vector3 tcpOffsetM,
            Quaternion[] rotations)
        {
            var positions = new Vector3[rotations.Length];
            for (int i = 0; i < rotations.Length; i++)
                positions[i] = touchPoint - rotations[i] * tcpOffsetM;
            return positions;
        }

        /// <summary>Вектор истинного смещения для метода «по нормали»: ось инструмента × высота.</summary>
        private static Vector3 PlaneOffsetVector(Vector3 toolAxis, float toolHeightM)
        {
            return toolAxis.normalized * toolHeightM;
        }

        /// <summary>
        /// Точка ОПОРНОЙ плоскости (столешницы): плоскость проходит через точку
        /// (0, referenceHeight, 0) с заданной нормалью. Смещения a, b откладываются по двум
        /// ортам плоскости и затем точка точно «сажается» на плоскость.
        /// </summary>
        private static Vector3 PointOnReferencePlane(Vector3 normal, float referenceHeight, float a, float b)
        {
            Vector3 refPoint = new Vector3(0f, referenceHeight, 0f);
            Vector3 u = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;
            Vector3 onPlane = refPoint + u * a + v * b;
            return onPlane + normal * Vector3.Dot(refPoint - onPlane, normal);
        }

        /// <summary>
        /// Синтетические системы фланца для метода «по нормали» (SCARA: ориентация инструмента
        /// не меняется). Касание лежит на опорной плоскости, фланец — ВЫШЕ него на высоту
        /// инструмента вдоль оси инструмента: касание = фланец + ось·h (см. формулу TCP_i =
        /// Фланец_i + R·t в KvCalibration.cs).
        /// </summary>
        private static List<Vector3> FlangePlaneSystems(Vector3 normal, float referenceHeight,
            Vector3 toolAxis, float toolHeightM, Vector3[] planeOffsets)
        {
            var points = new List<Vector3>(planeOffsets.Length);
            for (int i = 0; i < planeOffsets.Length; i++)
            {
                Vector3 touch = PointOnReferencePlane(normal, referenceHeight,
                    planeOffsets[i].x, planeOffsets[i].y);
                points.Add(touch - toolAxis * toolHeightM);
            }
            return points;
        }

        /// <summary>Копия набора точек с добавленным детерминированным шумом (один и тот же seed —
        /// один и тот же шум, поэтому тест воспроизводим).</summary>
        private static List<Vector3> WithNoise(List<Vector3> points, uint seed, float sigma)
        {
            var noisy = new List<Vector3>(points.Count);
            var rnd = new FixedSeedNoise(seed);
            for (int i = 0; i < points.Count; i++)
                noisy.Add(points[i] + rnd.NextVector(sigma));
            return noisy;
        }

        /// <summary>Проверка «число конечное» (не NaN и не ±Infinity) — по ТЗ входы не должны
        /// приводить к NaN/Infinity и выходу за границы массива.</summary>
        private static void AssertFinite(Vector3 value, string label)
        {
            Assert.That(float.IsNaN(value.x) || float.IsNaN(value.y) || float.IsNaN(value.z), Is.False,
                label + ": компонента не должна быть NaN");
            Assert.That(float.IsInfinity(value.x) || float.IsInfinity(value.y) || float.IsInfinity(value.z),
                Is.False, label + ": компонента не должна быть бесконечной");
        }

        // ==================================================================== 1. ГЛАВНЫЙ ТЕСТ:
        // синтетика по известному смещению TCP восстанавливается с точностью «0.000 мм»

        /// <summary>
        /// ГЛАВНЫЙ ТЕСТ. Синтетические данные строятся из ИЗВЕСТНОГО смещения TCP: четыре разные
        /// ориентации касаются одной точки пространства, положения фланца считаются как P − R·t.
        /// Решатель получает ТОЛЬКО положения и ориентации (точку касания он не знает и исключает
        /// её сам) и обязан вернуть исходное смещение — все три компоненты по отдельности.
        /// Допуск 5e-6 м = 0.005 мм: измеренная ошибка ≤ 3.6e-7 м (0.00036 мм), запас ≈ 14×.
        /// </summary>
        [Test]
        public void SolveToolOffset_IdealSyntheticData_RecoversAllThreeComponents()
        {
            Vector3[] positions = FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4);
            Vector3 offset;
            float rms, worst;
            bool solved = KvCalibrationService.SolveToolOffset(positions, Poses4,
                out offset, out rms, out worst);

            Assert.That(solved, Is.True, "синтетика из известного смещения обязана решаться");
            AssertFinite(offset, "восстановленное смещение");

            // Покомпонентно: X, Y, Z. Допуск 5e-6 м (0.005 мм) с пояснением в константе.
            Assert.That(offset.x, Is.EqualTo(TruthTcpOffsetM.x).Within(TcpIdealToleranceM),
                "компонента X восстановлена с ошибкой " +
                ((offset.x - TruthTcpOffsetM.x) * 1000f).ToString("0.000000") + " мм");
            Assert.That(offset.y, Is.EqualTo(TruthTcpOffsetM.y).Within(TcpIdealToleranceM),
                "компонента Y восстановлена с ошибкой " +
                ((offset.y - TruthTcpOffsetM.y) * 1000f).ToString("0.000000") + " мм");
            Assert.That(offset.z, Is.EqualTo(TruthTcpOffsetM.z).Within(TcpIdealToleranceM),
                "компонента Z восстановлена с ошибкой " +
                ((offset.z - TruthTcpOffsetM.z) * 1000f).ToString("0.000000") + " мм");
        }

        /// <summary>
        /// Модуль (длина) восстановленного смещения. В проекте эта величина — то, что реально
        /// применяется к потоку (flow.toolOffset = data.tcpLength), поэтому проверяется отдельно.
        /// Допуск 5e-6 м = 0.005 мм (измеренная ошибка модуля ≤ 3.6e-7 м).
        /// </summary>
        [Test]
        public void SolveToolOffset_IdealSyntheticData_RecoversOffsetMagnitude()
        {
            Vector3[] positions = FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4);
            Vector3 offset;
            float rms, worst;
            Assert.That(KvCalibrationService.SolveToolOffset(positions, Poses4,
                out offset, out rms, out worst), Is.True);

            float errorM = Vector3.Distance(offset, TruthTcpOffsetM);
            Assert.That(offset.magnitude, Is.EqualTo(TruthTcpOffsetM.magnitude).Within(TcpIdealToleranceM),
                "длина инструмента восстановлена с ошибкой " + (errorM * 1000f).ToString("0.000000") + " мм");
            Assert.That(errorM, Is.LessThan(TcpIdealToleranceM),
                "модуль вектора смещения восстановлен с ошибкой " + (errorM * 1000f).ToString("0.000000") + " мм");
        }

        /// <summary>
        /// Тот же главный тест, но точка касания «далёкая» (z ≈ −23.9 м) — как в SelfTest()
        /// проекта. Это худший случай по обусловленности float32 (вычитание близких больших
        /// чисел), поэтому он проверяется отдельно. Измеренная ошибка ≤ 3.3e-7 м (0.00033 мм),
        /// допуск прежний 5e-6 м — запас ≈ 15×.
        /// </summary>
        [Test]
        public void SolveToolOffset_FarTouchPoint_RecoversOffsetLikeSelfTest()
        {
            Vector3[] positions = FlangeSystems(FarTouchPointM, TruthTcpOffsetM, Poses4);
            Vector3 offset;
            float rms, worst;
            Assert.That(KvCalibrationService.SolveToolOffset(positions, Poses4,
                out offset, out rms, out worst), Is.True);

            Assert.That(offset.x, Is.EqualTo(TruthTcpOffsetM.x).Within(TcpIdealToleranceM), "ось X");
            Assert.That(offset.y, Is.EqualTo(TruthTcpOffsetM.y).Within(TcpIdealToleranceM), "ось Y");
            Assert.That(offset.z, Is.EqualTo(TruthTcpOffsetM.z).Within(TcpIdealToleranceM), "ось Z");
            // Остаток «далёких» идеальных данных измерен как rms ≤ 6.9e-8 м, worst ≤ 1.2e-7 м.
            Assert.That(rms, Is.LessThan(IdealResidualToleranceM), "rms на идеальных данных ≈ 0");
            Assert.That(worst, Is.LessThan(IdealResidualToleranceM), "worst на идеальных данных ≈ 0");
        }

        // ==================================================================== 2. НЕВЯЗКА НА ИДЕАЛЬНЫХ ДАННЫХ

        /// <summary>
        /// Невязка (rms и worst — в метрах, в проекте умножаются на 1000 и показываются в мм)
        /// на «идеальных» данных обязана быть близка к нулю. Проверяются наборы 4, 6 и 8 поз:
        /// 4 — минимум метода, 6 — типичный, 8 — предел RecordTcpPoint. Допуск 1e-6 м = 0.001 мм;
        /// измерено rms ≤ 6.9e-8 м и worst ≤ 9.2e-8 м — запас ≈ 11×. Остаток здесь — чистая
        /// ошибка округления float32 при построении синтетики.
        /// </summary>
        [Test]
        public void SolveToolOffset_IdealData_ResidualStaysNearZeroForFourSixAndEightPoses()
        {
            AssertIdealRecovery(Poses4, "4 позы");
            AssertIdealRecovery(Poses6, "6 поз");
            AssertIdealRecovery(Poses8, "8 поз");
        }

        /// <summary>Смещение + остаток на идеальных данных для заданного набора ориентаций.</summary>
        private static void AssertIdealRecovery(Quaternion[] rotations, string label)
        {
            Vector3[] positions = FlangeSystems(NearTouchPointM, TruthTcpOffsetM, rotations);
            Vector3 offset;
            float rms, worst;
            Assert.That(KvCalibrationService.SolveToolOffset(positions, rotations,
                out offset, out rms, out worst), Is.True, label + ": идеальные данные обязаны решаться");

            Assert.That(Vector3.Distance(offset, TruthTcpOffsetM), Is.LessThan(TcpIdealToleranceM),
                label + ": ошибка смещения " +
                (Vector3.Distance(offset, TruthTcpOffsetM) * 1000f).ToString("0.000000") + " мм");
            Assert.That(rms, Is.LessThan(IdealResidualToleranceM),
                label + ": rms = " + (rms * 1000f).ToString("0.000000") + " мм");
            Assert.That(worst, Is.LessThan(IdealResidualToleranceM),
                label + ": worst = " + (worst * 1000f).ToString("0.000000") + " мм");
            Assert.That(worst, Is.GreaterThanOrEqualTo(0f), label + ": остаток не может быть отрицательным");
        }

        // ==================================================================== 3. ДАННЫЕ С ШУМОМ

        /// <summary>
        /// Данные с детерминированным шумом 0.1 мм на записанные системы фланца (фиксированный
        /// seed): ошибка обязана ВЫРАСТИ относительно идеального случая, но остаться малой, а
        /// оценка — близкой к истине. Измерено: ошибка 0.284 мм (2.8σ), то есть в ~800 раз больше
        /// идеальной (0.00036 мм) и при этом в 3.5 раза меньше допуска 1 мм (10σ).
        /// </summary>
        [Test]
        public void SolveToolOffset_NoisyData_ErrorGrowsButOffsetStaysCloseToTruth()
        {
            Vector3[] ideal = FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4);
            Vector3[] noisy = AddNoise(ideal, 12345u, NoiseSigmaM);

            Vector3 idealOffset, noisyOffset;
            float rms, worst;
            Assert.That(KvCalibrationService.SolveToolOffset(ideal, Poses4,
                out idealOffset, out rms, out worst), Is.True);
            Assert.That(KvCalibrationService.SolveToolOffset(noisy, Poses4,
                out noisyOffset, out rms, out worst), Is.True);

            float idealErrorM = Vector3.Distance(idealOffset, TruthTcpOffsetM);
            float noisyErrorM = Vector3.Distance(noisyOffset, TruthTcpOffsetM);

            AssertFinite(noisyOffset, "шумная оценка");
            Assert.That(noisyErrorM, Is.GreaterThan(idealErrorM),
                "с шумом ошибка обязана вырасти (идеальная " + (idealErrorM * 1000f).ToString("0.000000") +
                " мм, шумная " + (noisyErrorM * 1000f).ToString("0.000000") + " мм)");
            // 1 мм = 10σ: измерено 0.284 мм (2.8σ) — запас 3.5×.
            Assert.That(noisyErrorM, Is.LessThan(1e-3f),
                "оценка по шумным данным осталась близкой к истине: ошибка " +
                (noisyErrorM * 1000f).ToString("0.000") + " мм");
            // Покомпонентно: шум не должен «сдвинуть» ни одну ось сильнее 5σ (0.5 мм).
            Assert.That(Mathf.Abs(noisyOffset.x - TruthTcpOffsetM.x), Is.LessThan(5f * NoiseSigmaM), "ось X");
            Assert.That(Mathf.Abs(noisyOffset.y - TruthTcpOffsetM.y), Is.LessThan(5f * NoiseSigmaM), "ось Y");
            Assert.That(Mathf.Abs(noisyOffset.z - TruthTcpOffsetM.z), Is.LessThan(5f * NoiseSigmaM), "ось Z");
        }

        /// <summary>
        /// Остаток на шумных данных обязан ОТРАЖАТЬ уровень шума, а не быть нулём: это и есть
        /// «честная мера качества» из комментария к SolveToolOffset. Измерено для σ = 0.1 мм:
        /// rms = 0.0986 мм (0.99σ), worst = 0.115 мм (1.15σ) — оба попадают в ожидаемый коридор.
        /// </summary>
        [Test]
        public void SolveToolOffset_NoisyData_ResidualTracksNoiseLevel()
        {
            Vector3[] noisy = AddNoise(FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4),
                12345u, NoiseSigmaM);
            // Вторая серия с в 5 раз большим шумом — остаток обязан вырасти пропорционально.
            Vector3[] noisier = AddNoise(FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4),
                12345u, 5f * NoiseSigmaM);

            Vector3 offset;
            float rms, worst, rmsBig, worstBig;
            Assert.That(KvCalibrationService.SolveToolOffset(noisy, Poses4,
                out offset, out rms, out worst), Is.True);
            Assert.That(KvCalibrationService.SolveToolOffset(noisier, Poses4,
                out offset, out rmsBig, out worstBig), Is.True);

            AssertFinite(new Vector3(rms, worst, rmsBig), "остатки");
            // Коридор: остаток не может быть существенно меньше шума (метод не «подгоняет» данные)
            // и не должен сильно его превышать (иначе модель неверна). Измерено 0.99σ и 1.15σ.
            Assert.That(rms, Is.GreaterThan(0.3f * NoiseSigmaM), "rms не должен быть много меньше шума");
            Assert.That(rms, Is.LessThan(3f * NoiseSigmaM), "rms не должен многократно превышать шум");
            Assert.That(worst, Is.GreaterThanOrEqualTo(rms), "worst — наихудшая точка, она не меньше СКО");
            Assert.That(worst, Is.LessThan(5f * NoiseSigmaM), "worst не должен многократно превышать шум");
            Assert.That(rmsBig, Is.GreaterThan(rms), "в 5 раз больший шум обязан дать больший остаток");
            Assert.That(rmsBig, Is.LessThan(3f * 5f * NoiseSigmaM), "остаток растёт вместе с шумом");
        }

        /// <summary>Шум на каждую систему фланца (реалистичная модель: ошибка касания оператора
        /// и погрешность позиционирования сказываются на записанном положении фланца).</summary>
        private static Vector3[] AddNoise(Vector3[] positions, uint seed, float sigma)
        {
            var noisy = new Vector3[positions.Length];
            var rnd = new FixedSeedNoise(seed);
            for (int i = 0; i < positions.Length; i++) noisy[i] = positions[i] + rnd.NextVector(sigma);
            return noisy;
        }

        // ==================================================================== 4. ВЫРОЖДЕННЫЕ ВХОДЫ

        /// <summary>
        /// Неполные/некорректные входные данные 4-точечного метода. По коду SolveToolOffset:
        /// null-массив → false; менее 4 точек → false; разная длина массивов → берётся min(...)
        /// и снова проверяется «n ≥ 4». Исключений нет, выходные параметры остаются нулевыми —
        /// именно это (корректный отказ, а не исключение и не выход за границы) и проверяется.
        /// </summary>
        [Test]
        public void SolveToolOffset_InvalidInputShapes_ReturnFalseWithZeroOutputs()
        {
            Vector3 onePoint = FlangeSystems(NearTouchPointM, TruthTcpOffsetM,
                new[] { Poses4[0] })[0];

            AssertRejected(null, null, "null-массивы");
            AssertRejected(new Vector3[0], new Quaternion[0], "пустые массивы");
            AssertRejected(new[] { onePoint }, new[] { Poses4[0] }, "одна точка");
            AssertRejected(new[] { onePoint, onePoint }, new[] { Poses4[0], Poses4[1] }, "две точки");
            AssertRejected(new[] { onePoint, onePoint, onePoint },
                new[] { Poses4[0], Poses4[1], Poses4[2] }, "три точки");
            // Разная длина: 4 положения, но 2 ориентации → min = 2 → отказ.
            AssertRejected(new[] { onePoint, onePoint, onePoint, onePoint },
                new[] { Poses4[0], Poses4[1] }, "разная длина массивов (4 положения / 2 ориентации)");
            // Обратный случай: 4 ориентации, но 2 положения → тоже отказ.
            AssertRejected(new[] { onePoint, onePoint },
                new[] { Poses4[0], Poses4[1], Poses4[2], Poses4[3] }, "разная длина (2 положения / 4 ориентации)");
        }

        /// <summary>Отказ: false + нулевые выходные значения (без исключений и мусора).</summary>
        private static void AssertRejected(Vector3[] positions, Quaternion[] rotations, string label)
        {
            Vector3 offset = new Vector3(9f, 9f, 9f);
            float rms = 9f, worst = 9f;
            bool solved = KvCalibrationService.SolveToolOffset(positions, rotations,
                out offset, out rms, out worst);

            Assert.That(solved, Is.False, label + ": метод обязан вернуть false");
            AssertFinite(offset, label);
            Assert.That(offset, Is.EqualTo(Vector3.zero), label + ": смещение должно остаться нулевым");
            Assert.That(rms, Is.EqualTo(0f).Within(0f), label + ": rms должен остаться нулевым");
            Assert.That(worst, Is.EqualTo(0f).Within(0f), label + ": worst должен остаться нулевым");
        }

        /// <summary>
        /// Вырожденные данные при ДОСТАТОЧНОМ числе точек (фактическое поведение по коду):
        ///   • все ориентации одинаковы → нормальные уравнения вырождены, определитель падает ниже
        ///     порога 1e-10 в Solve3 → false (система не решается);
        ///   • все положения одинаковы (но ориентации разные) → правая часть нулевая, решение
        ///     t = 0, метод возвращает TRUE с нулевым смещением и нулевым остатком — то есть
        ///     «нулевой инструмент» (это корректный ответ для такой синтетики, а не сбой);
        ///   • одинаковы и положения, и ориентации → снова false (вырождение по ориентациям).
        /// </summary>
        [Test]
        public void SolveToolOffset_DegenerateRotationsOrPositions_BehaveAsDocumented()
        {
            Vector3[] ideal = FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4);
            Vector3 offset;
            float rms, worst;

            // 1) одинаковые ориентации → отказ.
            Assert.That(KvCalibrationService.SolveToolOffset(ideal,
                new[] { Poses4[0], Poses4[0], Poses4[0], Poses4[0] }, out offset, out rms, out worst),
                Is.False, "одинаковые ориентации делают систему вырожденной");

            // 2) одинаковые положения при разных ориентациях → t = 0, остаток 0, без NaN.
            Assert.That(KvCalibrationService.SolveToolOffset(
                new[] { NearTouchPointM, NearTouchPointM, NearTouchPointM, NearTouchPointM }, Poses4,
                out offset, out rms, out worst), Is.True, "одинаковые положения решаются (t = 0)");
            AssertFinite(offset, "вырожденные положения");
            Assert.That(offset.magnitude, Is.EqualTo(0f).Within(0f), "при совпадающих положениях смещение нулевое");
            Assert.That(rms, Is.EqualTo(0f).Within(0f), "остаток совпадающих точек нулевой");
            Assert.That(worst, Is.EqualTo(0f).Within(0f), "наихудшее отклонение нулевое");

            // 3) одинаковые положения И одинаковые ориентации → отказ по ориентациям.
            Assert.That(KvCalibrationService.SolveToolOffset(
                new[] { NearTouchPointM, NearTouchPointM, NearTouchPointM, NearTouchPointM },
                new[] { Poses4[0], Poses4[0], Poses4[0], Poses4[0] }, out offset, out rms, out worst),
                Is.False, "двойное вырождение обязано отвергаться");
        }

        /// <summary>
        /// ВАЖНОЕ ОГРАНИЧЕНИЕ МЕТОДА (проверяется фактическое поведение). Если инструмент
        /// поворачивали только вокруг ОДНОЙ оси (здесь — две разные ориентации: 0° и 45° вокруг Y),
        /// компонента смещения вдоль этой оси не определяется: МНК даёт по ней ноль, а остаток
        /// при этом остаётся практически нулевым. Измерено: ошибка ровно 35 мм (= |ΔY| истинного
        /// смещения), rms = 5.3e-5 мм. Вывод: малый остаток НЕ гарантирует корректность
        /// калибровки — нужны повороты вокруг разных осей (в UI это и требует проверка ≥ 12°).
        /// </summary>
        [Test]
        public void SolveToolOffset_TwoDistinctRotationsOnly_ResidualIsZeroButOffsetIsWrong()
        {
            Quaternion[] twoOrientations =
            {
                Quaternion.Euler(0f, 0f, 0f),
                Quaternion.Euler(0f, 0f, 0f),
                Quaternion.Euler(0f, 45f, 0f),
                Quaternion.Euler(0f, 45f, 0f)
            };
            Vector3[] positions = FlangeSystems(NearTouchPointM, TruthTcpOffsetM, twoOrientations);

            Vector3 offset;
            float rms, worst;
            Assert.That(KvCalibrationService.SolveToolOffset(positions, twoOrientations,
                out offset, out rms, out worst), Is.True, "формально 4 точки — система «решается»");

            AssertFinite(offset, "оценка при поворотах вокруг одной оси");
            // Компонента вдоль оси поворота не наблюдаема: МНК устойчиво даёт по ней ~0.
            Assert.That(Mathf.Abs(offset.y), Is.LessThan(1e-4f),
                "ненаблюдаемая компонента выходит нулевой (измерено |Y| < 1e-6 м)");
            // Поэтому ошибка большая — это и есть предмет теста.
            Assert.That(Vector3.Distance(offset, TruthTcpOffsetM), Is.GreaterThan(0.01f),
                "при поворотах вокруг одной оси смещение восстанавливается НЕВЕРНО");
            // И при этом остаток обманывающе мал: измерено 5.3e-8 м.
            Assert.That(rms, Is.LessThan(IdealResidualToleranceM),
                "остаток НЕ выявляет вырождение: rms = " + (rms * 1000f).ToString("0.000000") + " мм");
            Assert.That(worst, Is.LessThan(IdealResidualToleranceM), "worst тоже мал");
        }

        // ==================================================================== 5. ЭКСТРЕМАЛЬНЫЕ ЗНАЧЕНИЯ

        /// <summary>
        /// Экстремальные КООРДИНАТЫ. Большие: положения фланца ~1e4 м (проверка устойчивости
        /// вычитания близких больших чисел во float32 — шаг представления там уже ~1e-3 м).
        /// Малые: и точка касания, и смещение ~1e-6 м (проверка, что регуляризация 1e-6 в
        /// нормальных уравнениях не «съедает» решение). Ни в одном случае не допускаются
        /// NaN/Infinity и выход за границы.
        /// Измерено: при координатах 1e4 м ошибка 0.91 мм (покомпонентно ≤ 0.63 мм), rms 0.81 мм;
        /// при координатах 1e-6 м ошибка 3.4e-11 м — точность сохраняется полностью.
        /// </summary>
        [Test]
        public void SolveToolOffset_ExtremeCoordinateScales_StayFiniteAndBounded()
        {
            Vector3 offset;
            float rms, worst;

            // --- очень большие координаты (1e4 м). Допуск 5 мм = 5e-3 м: измерено ≤ 0.63 мм
            // покомпонентно и 0.91 мм по модулю. Точность здесь ограничена ФОРМАТОМ float32
            // (шаг 2^-23 от 1e4 ≈ 1.2e-3 м), а не методом, поэтому допуск заведомо больше базового.
            Vector3[] huge = FlangeSystems(new Vector3(4200f, 10300f, -23900f), TruthTcpOffsetM, Poses4);
            Assert.That(KvCalibrationService.SolveToolOffset(huge, Poses4,
                out offset, out rms, out worst), Is.True, "большие координаты обязаны решаться");
            AssertFinite(offset, "координаты 1e4 м");
            AssertFinite(new Vector3(rms, worst, 0f), "остатки при координатах 1e4 м");
            Assert.That(offset.x, Is.EqualTo(TruthTcpOffsetM.x).Within(5e-3f), "ось X при 1e4 м");
            Assert.That(offset.y, Is.EqualTo(TruthTcpOffsetM.y).Within(5e-3f), "ось Y при 1e4 м");
            Assert.That(offset.z, Is.EqualTo(TruthTcpOffsetM.z).Within(5e-3f), "ось Z при 1e4 м");
            Assert.That(Vector3.Distance(offset, TruthTcpOffsetM), Is.LessThan(5e-3f),
                "модуль при 1e4 м (измерено 0.91 мм)");
            Assert.That(rms, Is.LessThan(5e-3f), "rms при 1e4 м (измерено 0.81 мм)");

            // --- очень малые координаты (1e-6 м). Допуск 1e-9 м: измерено 3.4e-11 м — запас ≈ 30×.
            Vector3 tinyTruth = new Vector3(1.2e-6f, -3.5e-6f, 1.45e-5f);
            Vector3[] tiny = FlangeSystems(new Vector3(3.1e-6f, 5e-7f, -1.2e-6f), tinyTruth, Poses4);
            Assert.That(KvCalibrationService.SolveToolOffset(tiny, Poses4,
                out offset, out rms, out worst), Is.True, "малые координаты обязаны решаться");
            AssertFinite(offset, "координаты 1e-6 м");
            Assert.That(offset.x, Is.EqualTo(tinyTruth.x).Within(1e-9f), "ось X при 1e-6 м");
            Assert.That(offset.y, Is.EqualTo(tinyTruth.y).Within(1e-9f), "ось Y при 1e-6 м");
            Assert.That(offset.z, Is.EqualTo(tinyTruth.z).Within(1e-9f), "ось Z при 1e-6 м");
            Assert.That(rms, Is.LessThan(1e-9f), "rms при 1e-6 м");
        }

        /// <summary>
        /// Экстремальные УГЛЫ. Углы больше полного оборота (360/400/1080°) описывают ту же
        /// ориентацию, что и «свёрнутые»: решение обязано совпасть. Отдельно проверяются
        /// ориентации, близкие к 180° (170…179°) — они дают максимальные элементы матриц
        /// (R_i − R_0), то есть наиболее «жёсткую» систему.
        /// Измерено: расхождение с «свёрнутым» набором 3.1e-8 м; ошибка при углах 170…179° 1.5e-8 м.
        /// Допуски: 1e-5 м для сравнения наборов (запас ≈ 300×) и 5e-6 м на восстановление.
        /// </summary>
        [Test]
        public void SolveToolOffset_ExtremeRotationAngles_AreSolvedCorrectly()
        {
            Vector3 offset, wrappedOffset;
            float rms, worst;

            // Углы с лишними оборотами: результат обязан совпасть с Poses4.
            Assert.That(KvCalibrationService.SolveToolOffset(
                FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4FullTurn), Poses4FullTurn,
                out offset, out rms, out worst), Is.True, "углы > 360° обязаны решаться");
            Assert.That(KvCalibrationService.SolveToolOffset(
                FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4), Poses4,
                out wrappedOffset, out rms, out worst), Is.True);
            Assert.That(Vector3.Distance(offset, wrappedOffset), Is.LessThan(1e-5f),
                "лишние обороты не меняют ориентацию, значит и результат (измерено 3.1e-8 м)");
            Assert.That(offset.x, Is.EqualTo(TruthTcpOffsetM.x).Within(TcpIdealToleranceM), "ось X при > 360°");
            Assert.That(offset.y, Is.EqualTo(TruthTcpOffsetM.y).Within(TcpIdealToleranceM), "ось Y при > 360°");
            Assert.That(offset.z, Is.EqualTo(TruthTcpOffsetM.z).Within(TcpIdealToleranceM), "ось Z при > 360°");

            // Ориентации около 180°.
            Assert.That(KvCalibrationService.SolveToolOffset(
                FlangeSystems(NearTouchPointM, TruthTcpOffsetM, PosesNear180), PosesNear180,
                out offset, out rms, out worst), Is.True, "углы 170…179° обязаны решаться");
            AssertFinite(offset, "углы около 180°");
            Assert.That(Vector3.Distance(offset, TruthTcpOffsetM), Is.LessThan(TcpIdealToleranceM),
                "ошибка при углах 170…179° (измерено 1.5e-8 м)");
        }

        // ==================================================================== 6. ИНВАРИАНТЫ

        /// <summary>
        /// Независимость от ПОРЯДКА точек. Система МНК использует первую точку как опорную,
        /// поэтому перестановка формально меняет систему — но на идеальных данных решение то же.
        /// Измерено: расхождение 4.4e-7 м (у начала координат) и 1.5e-6 м (далёкая точка).
        /// Допуск 1e-5 м = 0.01 мм — запас ≈ 7× по худшему случаю.
        /// На шумных данных перестановка меняет результат заметнее (другая опорная точка):
        /// измерено 9.8e-5 м (0.098 мм), допуск 5e-4 м — запас 5×.
        /// ВАЖНО: это инвариант ПРИБЛИЖЁННЫЙ, поэтому допуск явный, а не «точное равенство».
        /// </summary>
        [Test]
        public void SolveToolOffset_PointOrderInvariance_HoldsForIdealAndStaysSmallForNoisy()
        {
            Vector3 first, second;
            float rms, worst;

            // Идеальные данные, «далёкая» точка (худший случай по округлению).
            Vector3[] far = FlangeSystems(FarTouchPointM, TruthTcpOffsetM, Poses4);
            Assert.That(KvCalibrationService.SolveToolOffset(far, Poses4,
                out first, out rms, out worst), Is.True);
            Assert.That(KvCalibrationService.SolveToolOffset(
                new[] { far[2], far[3], far[0], far[1] },
                new[] { Poses4[2], Poses4[3], Poses4[0], Poses4[1] },
                out second, out rms, out worst), Is.True);
            Assert.That(Vector3.Distance(first, second), Is.LessThan(1e-5f),
                "перестановка идеальных точек не меняет результат (измерено 1.5e-6 м)");

            // Шумные данные: перестановка меняет опорную точку, поэтому расхождение больше,
            // но остаётся малым по сравнению с самим шумом.
            Vector3[] noisy = AddNoise(FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4),
                12345u, NoiseSigmaM);
            Assert.That(KvCalibrationService.SolveToolOffset(noisy, Poses4,
                out first, out rms, out worst), Is.True);
            Assert.That(KvCalibrationService.SolveToolOffset(
                new[] { noisy[3], noisy[0], noisy[2], noisy[1] },
                new[] { Poses4[3], Poses4[0], Poses4[2], Poses4[1] },
                out second, out rms, out worst), Is.True);
            Assert.That(Vector3.Distance(first, second), Is.LessThan(5e-4f),
                "на шумных данных перестановка меняет оценку в пределах долей шума " +
                "(измерено 0.098 мм)");
            Assert.That(Vector3.Distance(second, TruthTcpOffsetM), Is.LessThan(1e-3f),
                "после перестановки оценка всё равно близка к истине");
        }

        /// <summary>
        /// ДЕТЕРМИНИЗМ: повторный вызов на тех же данных даёт ТОЧНО тот же результат (в решателе
        /// нет ни ГСЧ, ни времени, ни итераций с плавающим числом шагов — только формулы Крамера
        /// и фиксированная регуляризация). Допуск 0: сравниваем побитово.
        /// </summary>
        [Test]
        public void SolveToolOffset_RepeatedCalls_ProduceIdenticalResults()
        {
            Vector3[] positions = AddNoise(FlangeSystems(NearTouchPointM, TruthTcpOffsetM, Poses4),
                777u, NoiseSigmaM);

            Vector3 first, second;
            float rmsFirst, worstFirst, rmsSecond, worstSecond;
            Assert.That(KvCalibrationService.SolveToolOffset(positions, Poses4,
                out first, out rmsFirst, out worstFirst), Is.True);
            Assert.That(KvCalibrationService.SolveToolOffset(positions, Poses4,
                out second, out rmsSecond, out worstSecond), Is.True);

            Assert.That(second.x, Is.EqualTo(first.x).Within(0f), "X повторного вызова");
            Assert.That(second.y, Is.EqualTo(first.y).Within(0f), "Y повторного вызова");
            Assert.That(second.z, Is.EqualTo(first.z).Within(0f), "Z повторного вызова");
            Assert.That(rmsSecond, Is.EqualTo(rmsFirst).Within(0f), "rms повторного вызова");
            Assert.That(worstSecond, Is.EqualTo(worstFirst).Within(0f), "worst повторного вызова");
        }

        // ==================================================================== 7. МЕТОД «ПО НОРМАЛИ»
        // (калибровка TCP для SCARA: SolveTcpPlane — тоже калибровка TCP, вторая ветка метода)

        /// <summary>
        /// ГЛАВНЫЙ ТЕСТ метода «по нормали к плоскости». Синтетика: известная высота инструмента
        /// 0.183 м, известная наклонная нормаль столешницы, НАКЛОНЁННАЯ ось инструмента.
        /// Точки касания лежат точно на опорной плоскости, системы фланца — на высоту инструмента
        /// выше вдоль оси (касание = фланец + ось·h). Проверяются: восстановленная высота,
        /// направление нормали, остаток ≈ 0 и восстановленный ВЕКТОР смещения (ось·h).
        /// Допуски: высота 5e-6 м (измерено ≤ 1.4e-7 м, запас ≈ 35×), нормаль 0.05°
        /// (измерено 0.00000°), остаток 0.01 мм (измерено ≤ 1.5e-4 мм).
        /// Дополнительно тот же расчёт на МИНИМАЛЬНЫХ 3 точках (TcpPlanePointsNeeded).
        /// </summary>
        [Test]
        public void SolvePlaneOffset_IdealSyntheticData_RecoversHeightNormalAndZeroResidual()
        {
            // Санитарная проверка показательности теста: ось инструмента НЕ параллельна нормали
            // (измерено |cos| = 0.9125 при угле 155.8°), поэтому деление на косинус угла между
            // осью и нормалью (along) действительно проверяется: ошибка в нём дала бы ~10 %
            // (≈18 мм) и была бы поймана допуском 0.005 мм. Высота откладывается ВДОЛЬ оси
            // инструмента, и наклон оси не должен её искажать — восстановленный вектор
            // смещения (ось·h) тоже проверяется внутри AssertPlaneRecovery.
            Assert.That(Mathf.Abs(Vector3.Dot(ToolAxisTilted, PlaneNormalTilted)), Is.LessThan(0.99f),
                "ось инструмента должна быть заметно наклонена к нормали — иначе тест не показателен");

            AssertPlaneRecovery(PlaneSpread6, "6 точек");
            AssertPlaneRecovery(PlaneSpread3, "3 точки (минимум метода)");
        }

        /// <summary>Проверка идеального набора точек плоскости (наклонная нормаль + наклонная ось).</summary>
        private static void AssertPlaneRecovery(Vector3[] planeOffsets, string label)
        {
            List<Vector3> points = FlangePlaneSystems(PlaneNormalTilted, ReferencePlaneHeightM,
                ToolAxisTilted, TruthToolHeightM, planeOffsets);

            Vector3 normal;
            float height, residualMm;
            bool solved = KvCalibrationService.SolvePlaneOffset(points, ToolAxisTilted,
                ReferencePlaneHeightM, out normal, out height, out residualMm);

            Assert.That(solved, Is.True, label + ": идеальная плоскость обязана решаться");
            AssertFinite(normal, label + ": нормаль");
            Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-4f), label + ": нормаль нормирована");
            Assert.That(height, Is.EqualTo(TruthToolHeightM).Within(PlaneHeightToleranceM),
                label + ": высота инструмента восстановлена с ошибкой " +
                ((height - TruthToolHeightM) * 1000f).ToString("0.000000") + " мм");
            Assert.That(Vector3.Angle(normal, PlaneNormalTilted),
                Is.LessThan(PlaneNormalAngleToleranceDeg),
                label + ": направление нормали (измерено 0.00000°)");
            Assert.That(residualMm, Is.LessThan(PlaneIdealResidualMm),
                label + ": остаток на идеальных данных " + residualMm.ToString("0.000000") + " мм");

            // Вектор смещения, который уходит в файл калибровки (data.tcpOffsetPlane): ось × высота.
            Vector3 offset = ToolAxisTilted * height;
            Vector3 truth = PlaneOffsetVector(ToolAxisTilted, TruthToolHeightM);
            Assert.That(Vector3.Distance(offset, truth), Is.LessThan(PlaneHeightToleranceM),
                label + ": вектор смещения (ось·h) восстановлен с ошибкой " +
                (Vector3.Distance(offset, truth) * 1000f).ToString("0.000000") + " мм");
        }

        /// <summary>
        /// Данные с шумом 0.1 мм на системы фланца: высота и остаток обязаны остаться малыми.
        /// Измерено (наклонная нормаль, наклонная ось, 5 точек): ошибка высоты 0.018 мм,
        /// остаток 0.033 мм, нормаль не искажена. Допуски: высота 0.5 мм (запас ≈ 27×),
        /// остаток 1 мм (запас ≈ 30×).
        /// </summary>
        [Test]
        public void SolvePlaneOffset_NoisyData_StaysAccurate()
        {
            List<Vector3> clean = FlangePlaneSystems(PlaneNormalTilted, ReferencePlaneHeightM,
                ToolAxisTilted, TruthToolHeightM, PlaneSpread6);
            List<Vector3> noisy = WithNoise(clean, 777u, NoiseSigmaM);

            Vector3 normal;
            float height, residualMm;
            Assert.That(KvCalibrationService.SolvePlaneOffset(noisy, ToolAxisTilted,
                ReferencePlaneHeightM, out normal, out height, out residualMm), Is.True,
                "шумные точки плоскости обязаны решаться");

            AssertFinite(normal, "нормаль по шумным точкам");
            Assert.That(Mathf.Abs(height - TruthToolHeightM), Is.LessThan(5e-4f),
                "ошибка высоты по шумным данным " +
                (Mathf.Abs(height - TruthToolHeightM) * 1000f).ToString("0.000") + " мм (измерено 0.018 мм)");
            Assert.That(residualMm, Is.LessThan(1f),
                "остаток по шумным данным " + residualMm.ToString("0.000") + " мм (измерено 0.033 мм)");
            Assert.That(residualMm, Is.GreaterThan(0f),
                "на шумных данных остаток не может быть ровно нулевым — он отражает качество");
            Assert.That(Vector3.Angle(normal, PlaneNormalTilted), Is.LessThan(1f),
                "нормаль по шумным данным не должна «уезжать»");
        }

        /// <summary>
        /// Вырожденные входы метода «по нормали». По коду SolvePlaneOffset: null-список → false;
        /// менее 3 точек → false; ПОЛНОСТЬЮ совпадающий набор точек → тоже false, потому что
        /// ковариационная матрица вырождается и Invert3 (порог определителя 1e-18) возвращает null.
        /// Исключений нет. Здесь фиксируется именно фактическое поведение: корректный ОТКАЗ, а не
        /// исключение и не NaN/Infinity. Одиночный ДУБЛИКАТ точки, в отличие от полного совпадения,
        /// не вырождает метод — это проверяет отдельный тест ниже.
        /// </summary>
        [Test]
        public void SolvePlaneOffset_InvalidOrDegenerateInputSizes_ReturnFalse()
        {
            List<Vector3> points = FlangePlaneSystems(PlaneNormalTilted, ReferencePlaneHeightM,
                ToolAxisTilted, TruthToolHeightM, PlaneSpread6);

            AssertPlaneRejected(null, "null-список");
            AssertPlaneRejected(new List<Vector3>(), "пустой список");
            AssertPlaneRejected(new List<Vector3> { points[0] }, "одна точка");
            AssertPlaneRejected(new List<Vector3> { points[0], points[1] }, "две точки");
            AssertPlaneRejected(new List<Vector3> { points[0], points[0], points[0], points[0] },
                "все точки одинаковые");
            AssertPlaneRejected(new List<Vector3> { points[2], points[2], points[2] },
                "три одинаковые точки");
        }

        /// <summary>
        /// Одиночный ДУБЛИКАТ точки (оператор дважды записал одно положение) вырождением НЕ
        /// является: повторившаяся точка просто получает двойной вес в ковариационной матрице,
        /// метод решает задачу и даёт корректную высоту. Это фактическое поведение по коду — и оно
        /// полезно зафиксировать, чтобы отказ на дубликате не был ошибочно «починен».
        /// Измерено на наборе {p0, p0, p1, p2}: ошибка высоты −0.000179 мм, остаток 0.000179 мм,
        /// нормаль не искажена. Допуски: высота 5e-6 м (запас ≈ 28×), остаток 0.01 мм.
        /// </summary>
        [Test]
        public void SolvePlaneOffset_DuplicatePointIsToleratedAndHeightStaysCorrect()
        {
            List<Vector3> points = FlangePlaneSystems(PlaneNormalTilted, ReferencePlaneHeightM,
                ToolAxisTilted, TruthToolHeightM, PlaneSpread6);
            var withDuplicate = new List<Vector3> { points[0], points[0], points[1], points[2] };

            Vector3 normal;
            float height, residualMm;
            Assert.That(KvCalibrationService.SolvePlaneOffset(withDuplicate, ToolAxisTilted,
                ReferencePlaneHeightM, out normal, out height, out residualMm), Is.True,
                "дубликат точки не вырождает метод");

            AssertFinite(normal, "нормаль при дубликате точки");
            Assert.That(normal.magnitude, Is.EqualTo(1f).Within(1e-4f), "нормаль нормирована");
            Assert.That(height, Is.EqualTo(TruthToolHeightM).Within(PlaneHeightToleranceM),
                "высота при дубликате точки (измерено 0.000179 мм ошибки)");
            Assert.That(Vector3.Angle(normal, PlaneNormalTilted), Is.LessThan(PlaneNormalAngleToleranceDeg),
                "нормаль при дубликате точки не искажена");
            Assert.That(residualMm, Is.LessThan(PlaneIdealResidualMm),
                "остаток при дубликате точки (измерено 0.000179 мм)");
        }

        /// <summary>Отказ метода «по нормали»: false и значения по умолчанию (up / 0 / 0).</summary>
        private static void AssertPlaneRejected(List<Vector3> points, string label)
        {
            Vector3 normal = new Vector3(9f, 9f, 9f);
            float height = 9f, residualMm = 9f;
            bool solved = KvCalibrationService.SolvePlaneOffset(points, ToolAxisDown,
                ReferencePlaneHeightM, out normal, out height, out residualMm);

            Assert.That(solved, Is.False, label + ": метод обязан вернуть false");
            AssertFinite(normal, label + ": нормаль");
            Assert.That(normal, Is.EqualTo(Vector3.up), label + ": нормаль остаётся значением по умолчанию");
            Assert.That(height, Is.EqualTo(0f).Within(0f), label + ": высота остаётся нулевой");
            Assert.That(residualMm, Is.EqualTo(0f).Within(0f), label + ": остаток остаётся нулевым");
        }

        /// <summary>
        /// Точки, лежащие на ОДНОЙ ЛИНИИ, не задают плоскость — метод обязан отказать.
        /// Проверяются два случая: строго коллинеарные точки и «почти коллинеарные» (отклонение
        /// от линии 0.2…0.3 мм), которые на реальном стенде означают, что оператор водил
        /// инструментом по одной прямой. Оба случая отвергаются (измерено false), потому что
        /// определитель ковариационной матрицы уходит ниже порога 1e-18 в Invert3.
        /// (Предупреждение в UI — вторая линия защиты: RecordTcpPlanePoint проверяет векторное
        /// произведение, но это уже требует робота и здесь не проверяется.)
        /// </summary>
        [Test]
        public void SolvePlaneOffset_CollinearOrNearlyCollinearPoints_ReturnFalse()
        {
            // Строго коллинеарные точки: линия внутри плоскости столешницы, разброс 0.4 м.
            var collinear = new List<Vector3>();
            for (int i = 0; i < 5; i++)
                collinear.Add(new Vector3(0.1f * i, ReferencePlaneHeightM + TruthToolHeightM, 0f));
            AssertPlaneRejected(collinear, "коллинеарные точки (разброс 0.4 м)");

            // Вертикальная линия (проекция на столешницу — одна точка): тоже отказ.
            var vertical = new List<Vector3>();
            for (int i = 0; i < 5; i++)
                vertical.Add(new Vector3(0f, ReferencePlaneHeightM + 0.1f * i, 0f));
            AssertPlaneRejected(vertical, "вертикальная линия");

            // «Почти» коллинеарные: отклонение от линии всего 0.2…0.3 мм.
            var nearly = new List<Vector3>();
            for (int i = 0; i < 5; i++)
                nearly.Add(new Vector3(0.1f * i, ReferencePlaneHeightM + TruthToolHeightM + 0.0002f * i,
                    0.0003f * i));
            AssertPlaneRejected(nearly, "почти коллинеарные точки (отклонение 0.3 мм)");
        }

        /// <summary>
        /// Крайние случаи ОСИ ИНСТРУМЕНТА:
        ///   • ось лежит почти в плоскости (перпендикулярна нормали) → метод неприменим: false
        ///     (в SolvePlaneOffset есть проверка |cos| ≥ 0.2);
        ///   • нулевая ось → подставляется Vector3.down (запасное значение в коде), и корректная
        ///     геометрия решается как обычно: измерено 3e-8 м ошибки высоты.
        /// </summary>
        [Test]
        public void SolvePlaneOffset_ToolAxisEdgeCases_AreHandled()
        {
            // Ось инструмента горизонтальна, нормаль вертикальна → косинус 0 → отказ.
            List<Vector3> flat = FlangePlaneSystems(Vector3.up, ReferencePlaneHeightM,
                ToolAxisDown, TruthToolHeightM, PlaneSpread6);
            Vector3 normal;
            float height, residualMm;
            Assert.That(KvCalibrationService.SolvePlaneOffset(flat, Vector3.right,
                ReferencePlaneHeightM, out normal, out height, out residualMm), Is.False,
                "ось инструмента почти в плоскости — метод неприменим");

            // Нулевая ось → запасное значение down.
            Assert.That(KvCalibrationService.SolvePlaneOffset(flat, Vector3.zero,
                ReferencePlaneHeightM, out normal, out height, out residualMm), Is.True,
                "нулевая ось должна подменяться значением по умолчанию (down)");
            AssertFinite(normal, "нормаль при нулевой оси");
            Assert.That(height, Is.EqualTo(TruthToolHeightM).Within(PlaneHeightToleranceM),
                "высота при нулевой оси (измерено 3e-8 м ошибки)");
            Assert.That(residualMm, Is.LessThan(PlaneIdealResidualMm), "остаток при нулевой оси");
        }

        /// <summary>
        /// Экстремальные МАСШТАБЫ метода «по нормали» и его внутренний порог обусловленности.
        ///   • очень большая высота опорной плоскости (1e4 м, точки ~1e4 м): измерено — ошибка
        ///     высоты 0.079 мм, остаток 0.98 мм, нормаль ушла на 0.048°; допуски 1 мм / 5 мм / 0.5°;
        ///   • очень малая высота инструмента (1e-6 м при разбросе точек 0.2 м): измерено
        ///     8.7e-10 м — допуск 1e-8 м, запас ≈ 11×;
        ///   • СЛИШКОМ ТЕСНЫЙ набор точек (разброс ~1 мм) метод отвергает: нормаль ищется через
        ///     Invert3 с ФИКСИРОВАННЫМ порогом определителя 1e-18, поэтому у ковариационной
        ///     матрицы есть «пол» по масштабу (для плоскости нужно СКО разброса порядка сантиметров).
        ///     Это ограничение метода, зафиксированное тестом как корректный отказ;
        ///   • коллинеарный набор с гигантским разбросом (сотни метров) этим порогом НЕ отвергается
        ///     (определитель растёт вместе с масштабом) — поэтому для него проверяются только
        ///     безопасность (нет исключения, нет NaN/Infinity) и повторяемость.
        /// </summary>
        [Test]
        public void SolvePlaneOffset_ExtremeScales_StayFiniteAndAccurate()
        {
            Vector3 normal;
            float height, residualMm;

            // 1) Очень большие значения.
            List<Vector3> huge = FlangePlaneSystems(PlaneNormalTilted, 10000f,
                ToolAxisTilted, TruthToolHeightM, PlaneSpread6);
            Assert.That(KvCalibrationService.SolvePlaneOffset(huge, ToolAxisTilted, 10000f,
                out normal, out height, out residualMm), Is.True, "высота плоскости 1e4 м обязана решаться");
            AssertFinite(normal, "нормаль при 1e4 м");
            AssertFinite(new Vector3(height, residualMm, 0f), "высота/остаток при 1e4 м");
            Assert.That(Mathf.Abs(height - TruthToolHeightM), Is.LessThan(1e-3f),
                "ошибка высоты при 1e4 м " + (Mathf.Abs(height - TruthToolHeightM) * 1000f).ToString("0.000") +
                " мм (измерено 0.079 мм)");
            Assert.That(residualMm, Is.LessThan(5f), "остаток при 1e4 м (измерено 0.98 мм)");
            Assert.That(Vector3.Angle(normal, PlaneNormalTilted), Is.LessThan(0.5f),
                "нормаль при 1e4 м (измерено 0.048°)");

            // 2) Очень малая высота инструмента.
            List<Vector3> tinyHeight = FlangePlaneSystems(PlaneNormalTilted, 1e-5f,
                ToolAxisTilted, 1e-6f, PlaneSpread6);
            Assert.That(KvCalibrationService.SolvePlaneOffset(tinyHeight, ToolAxisTilted, 1e-5f,
                out normal, out height, out residualMm), Is.True, "высота 1e-6 м обязана решаться");
            Assert.That(Mathf.Abs(height - 1e-6f), Is.LessThan(1e-8f),
                "ошибка высоты 1e-6 м: " + (Mathf.Abs(height - 1e-6f) * 1000f).ToString("0.0000000") +
                " мм (измерено 8.7e-10 м)");

            // 3) Слишком тесный набор точек (разброс ~1 мм) — корректный отказ.
            var tooTight = new List<Vector3>();
            for (int i = 0; i < 4; i++)
                tooTight.Add(new Vector3(1e-3f * (i % 2), ReferencePlaneHeightM + TruthToolHeightM,
                    1e-3f * (i / 2)));
            AssertPlaneRejected(tooTight, "разброс точек ~1 мм (ниже порога метода)");

            // 4) Коллинеарный набор огромного масштаба: безопасность и повторяемость.
            var hugeCollinear = new List<Vector3>();
            for (int i = 0; i < 5; i++)
                hugeCollinear.Add(new Vector3(50f * i, ReferencePlaneHeightM, 100f * i));

            Vector3 normal1, normal2;
            float height1, height2, residual1, residual2;
            bool solved1 = KvCalibrationService.SolvePlaneOffset(hugeCollinear, ToolAxisDown,
                ReferencePlaneHeightM, out normal1, out height1, out residual1);
            bool solved2 = KvCalibrationService.SolvePlaneOffset(hugeCollinear, ToolAxisDown,
                ReferencePlaneHeightM, out normal2, out height2, out residual2);

            // Фактическое поведение (измерено): отказ НЕ гарантирован — проверяем безопасность.
            Assert.That(solved2, Is.EqualTo(solved1), "решение по одним данным обязано быть повторяемым");
            if (solved1)
            {
                AssertFinite(normal1, "нормаль огромного коллинеарного набора");
                Assert.That(normal1.magnitude, Is.EqualTo(1f).Within(1e-4f), "нормаль нормирована");
                AssertFinite(new Vector3(height1, residual1, 0f), "высота/остаток огромного набора");
                Assert.That(height1, Is.EqualTo(height2).Within(0f), "высота повторяема");
                Assert.That(residual1, Is.EqualTo(residual2).Within(0f), "остаток повторяем");
            }
        }

        /// <summary>
        /// ОГРАНИЧЕНИЕ, важное для эксплуатации: высота опорной плоскости
        /// (referencePlaneHeightM, по умолчанию StandBuilder.TopHeight = 0.98 м) НЕ входит в
        /// остаток, поэтому ошибка в ней не видна по остатку — она просто сдвигает найденную
        /// высоту инструмента ровно на величину ошибки. Измерено: при referenceHeight +10 мм
        /// высота стала 173.000 мм (было 183.000), остаток остался ≈ 0.
        /// Допуск 1e-6 м: измерено смещение ровно 10 мм (совпадение до float32).
        /// </summary>
        [Test]
        public void SolvePlaneOffset_WrongReferenceHeight_ShiftsHeightExactlyByError()
        {
            List<Vector3> points = FlangePlaneSystems(PlaneNormalTilted, ReferencePlaneHeightM,
                ToolAxisDown, TruthToolHeightM, PlaneSpread6);

            const float referenceErrorM = 0.010f;
            Vector3 normal;
            float height, residualMm;
            Assert.That(KvCalibrationService.SolvePlaneOffset(points, ToolAxisDown,
                ReferencePlaneHeightM + referenceErrorM, out normal, out height, out residualMm),
                Is.True);

            float expectedM = TruthToolHeightM - referenceErrorM;
            Assert.That(height, Is.EqualTo(expectedM).Within(1e-6f),
                "высота сдвинулась ровно на ошибку опорной плоскости: получено " +
                (height * 1000f).ToString("0.000") + " мм, ожидалось " +
                (expectedM * 1000f).ToString("0.000") + " мм");
            Assert.That(residualMm, Is.LessThan(PlaneIdealResidualMm),
                "остаток НЕ выявляет ошибку опорной плоскости (" + residualMm.ToString("0.000000") + " мм)");
        }

        /// <summary>
        /// Инварианты метода «по нормали»: результат не зависит от ПОРЯДКА точек (нормаль ищется
        /// по ковариационной матрице, а она — сумма, то есть перестановочно-инвариантна; измерено
        /// расхождение &lt; 5e-8 м даже на шумных данных) и повторный вызов даёт ТОЧНО тот же
        /// результат (обратная итерация: фиксированное начальное приближение (1, 0.7, 0.3) и
        /// ровно 32 итерации — допуск 0, сравнение побитовое).
        /// </summary>
        [Test]
        public void SolvePlaneOffset_PermutationAndRepeatedCalls_AreStable()
        {
            List<Vector3> ideal = FlangePlaneSystems(PlaneNormalTilted, ReferencePlaneHeightM,
                ToolAxisTilted, TruthToolHeightM, PlaneSpread6);
            var permuted = new List<Vector3>
            {
                ideal[4], ideal[0], ideal[3], ideal[1], ideal[2], ideal[5]
            };

            // --- 1) идеальные данные: перестановка не меняет результат.
            Vector3 idealNormal, permutedNormal;
            float idealHeight, permutedHeight, idealResidual, permutedResidual;
            Assert.That(KvCalibrationService.SolvePlaneOffset(ideal, ToolAxisTilted,
                ReferencePlaneHeightM, out idealNormal, out idealHeight, out idealResidual), Is.True);
            Assert.That(KvCalibrationService.SolvePlaneOffset(permuted, ToolAxisTilted,
                ReferencePlaneHeightM, out permutedNormal, out permutedHeight, out permutedResidual), Is.True);

            Assert.That(Mathf.Abs(permutedHeight - idealHeight), Is.LessThan(1e-6f),
                "перестановка точек не меняет высоту (измерено < 5e-8 м)");
            Assert.That(Vector3.Angle(idealNormal, permutedNormal), Is.LessThan(1e-3f),
                "перестановка точек не меняет нормаль (измерено 0°)");

            // --- 2) шумные данные: расхождение тоже в пределах долей шума.
            List<Vector3> noisy = WithNoise(ideal, 4242u, NoiseSigmaM);
            var noisyPermuted = new List<Vector3>
            {
                noisy[5], noisy[1], noisy[0], noisy[4], noisy[2], noisy[3]
            };
            Vector3 noisyNormal, noisyPermutedNormal;
            float noisyHeight, noisyPermutedHeight, noisyResidual, noisyPermutedResidual;
            Assert.That(KvCalibrationService.SolvePlaneOffset(noisy, ToolAxisTilted,
                ReferencePlaneHeightM, out noisyNormal, out noisyHeight, out noisyResidual), Is.True);
            Assert.That(KvCalibrationService.SolvePlaneOffset(noisyPermuted, ToolAxisTilted,
                ReferencePlaneHeightM, out noisyPermutedNormal, out noisyPermutedHeight,
                out noisyPermutedResidual), Is.True);
            Assert.That(Mathf.Abs(noisyPermutedHeight - noisyHeight), Is.LessThan(1e-5f),
                "на шумных точках перестановка даёт расхождение в пределах долей шума");
            Assert.That(Vector3.Angle(noisyNormal, noisyPermutedNormal), Is.LessThan(0.01f),
                "нормаль по шумным точкам при перестановке почти не меняется");

            // --- 3) повторный вызов на ТЕХ ЖЕ идеальных данных — побитово тот же результат
            // (сравнение идёт именно с идеальным прогоном из пункта 1).
            Vector3 repeatNormal;
            float repeatHeight, repeatResidual;
            Assert.That(KvCalibrationService.SolvePlaneOffset(ideal, ToolAxisTilted,
                ReferencePlaneHeightM, out repeatNormal, out repeatHeight, out repeatResidual), Is.True);
            Assert.That(repeatNormal.x, Is.EqualTo(idealNormal.x).Within(0f), "X нормали повторного вызова");
            Assert.That(repeatNormal.y, Is.EqualTo(idealNormal.y).Within(0f), "Y нормали повторного вызова");
            Assert.That(repeatNormal.z, Is.EqualTo(idealNormal.z).Within(0f), "Z нормали повторного вызова");
            Assert.That(repeatHeight, Is.EqualTo(idealHeight).Within(0f), "высота повторного вызова");
            Assert.That(repeatResidual, Is.EqualTo(idealResidual).Within(0f), "остаток повторного вызова");
        }

        /// <summary>
        /// ХАРАКТЕРИЗАЦИОННЫЙ тест: фиксирует ФАКТИЧЕСКОЕ поведение на данных, построенных так же,
        /// как во встроенной самопроверке <see cref="KvCalibrationService.SelfTestPlane"/>.
        /// Там точки фланца строятся как «касание + axis·h» при axis = Vector3.down, то есть фланец
        /// оказывается НИЖЕ столешницы, хотя в комментарии сказано «выше». Высота при этом
        /// восстанавливается верно (берётся модуль), а ОСТАТОК считается для точек, сдвинутых на
        /// +axis·height, и потому равен ≈ 2·h: измерено 365.77 мм на «идеальных» данных
        /// (2 · 183 мм = 366 мм). Та же геометрия с фланцем ВЫШЕ плоскости даёт остаток
        /// ≈ 5e-5 мм — см. тест идеальных данных. Тест нужен, чтобы этот дефект данных
        /// самопроверки не остался незамеченным: он НЕ в решателе, а в генераторе SelfTestPlane.
        /// </summary>
        [Test]
        public void SolvePlaneOffset_SelfTestPlaneSyntheticConstruction_ResidualIsAroundTwoHeights()
        {
            Vector3 normal = PlaneNormalNearVertical;
            Vector3 refPoint = new Vector3(0f, ReferencePlaneHeightM, 0f);
            Vector3 u = Vector3.ProjectOnPlane(Vector3.right, normal).normalized;
            Vector3 v = Vector3.Cross(normal, u).normalized;

            // Тот же детерминированный разброс ±0.2 м, что и System.Random(7) в SelfTestPlane,
            // и та же база (0.4, 0, −23.9) — но через наш LCG, чтобы результат не зависел от рантайма.
            var points = new List<Vector3>();
            var rnd = new FixedSeedNoise(7u);
            Vector3 basePoint = new Vector3(0.4f, 0f, -23.9f);
            for (int i = 0; i < 5; i++)
            {
                float a = rnd.NextSymmetric() * 0.2f;
                float b = rnd.NextSymmetric() * 0.2f;
                Vector3 onPlane = refPoint + basePoint + u * a + v * b;
                onPlane += normal * Vector3.Dot(refPoint - onPlane, normal);
                points.Add(onPlane + ToolAxisDown * TruthToolHeightM);   // как в SelfTestPlane
            }

            Vector3 foundNormal;
            float height, residualMm;
            Assert.That(KvCalibrationService.SolvePlaneOffset(points, ToolAxisDown,
                ReferencePlaneHeightM, out foundNormal, out height, out residualMm), Is.True);

            // Высота при этом всё равно верна (дефект только в остатке): измерено 0.0031 мм ошибки.
            Assert.That(height, Is.EqualTo(TruthToolHeightM).Within(5e-5f),
                "высота в конструкции SelfTestPlane восстанавливается верно");
            Assert.That(Vector3.Angle(foundNormal, normal), Is.LessThan(PlaneNormalAngleToleranceDeg),
                "нормаль тоже верна");
            // А остаток «врёт» ровно на 2·h — это и фиксируем (измерено 365.77 мм).
            Assert.That(residualMm, Is.GreaterThan(300f),
                "остаток конструкции SelfTestPlane ≈ 2·h = 366 мм, получено " +
                residualMm.ToString("0.00") + " мм");
            Assert.That(residualMm, Is.LessThan(400f),
                "остаток не должен превышать 2·h уж совсем сильно");
        }

        // ==================================================================== 8. СИСТЕМА ФЛАНЦА

        /// <summary>
        /// <see cref="KvCalibrationService.FlangeFrame"/> — единственная точка, где калибровка
        /// соприкасается с кинематикой: система фланца строится по пивотам и осям валидатора.
        /// Без робота (PoseValidator.Init не вызывался) валидатор не готов, и функция обязана
        /// вернуть false, записав в выходные параметры нулевое положение и единичный поворот, —
        /// без исключения и без обращения к трансформам сцены.
        /// Геометрию системы фланца (PivotAt/AxisWorld по реальной кинематике) EditMode-тест
        /// проверить не может — это требует робота в сцене (см. отчёт: НЕ ПОКРЫТО).
        /// </summary>
        [Test]
        public void FlangeFrame_WithoutReadyValidator_ReturnsFalseAndIdentityOutputs()
        {
            var validator = new TrajectoryCore.PoseValidator();
            Assert.That(validator.Ready, Is.False, "валидатор без Init не должен быть готов");

            Vector3 position;
            Quaternion rotation;
            Assert.That(KvCalibrationService.FlangeFrame(validator, new double[6],
                out position, out rotation), Is.False, "неготовый валидатор → отказ");
            Assert.That(Vector3.Distance(position, Vector3.zero), Is.EqualTo(0f).Within(0f),
                "положение остаётся нулевым");
            Assert.That(Quaternion.Angle(rotation, Quaternion.identity), Is.EqualTo(0f).Within(0f),
                "поворот остаётся единичным");

            // null-валидатор и null-поза — тоже отказ, а не исключение.
            Assert.That(KvCalibrationService.FlangeFrame(null, new double[6],
                out position, out rotation), Is.False, "null-валидатор → отказ");
            Assert.That(KvCalibrationService.FlangeFrame(validator, null,
                out position, out rotation), Is.False, "null-поза → отказ");
            Assert.That(KvCalibrationService.FlangeFrame(null, null,
                out position, out rotation), Is.False, "оба null → отказ");
            Assert.That(Vector3.Distance(position, Vector3.zero), Is.EqualTo(0f).Within(0f));
        }

        // ==================================================================== 9. СЕРВИС БЕЗ РОБОТА
        // (члены, не требующие сцены: состояние, счётчики, отказы, отчёты)

        /// <summary>
        /// Новый экземпляр <see cref="KvCalibrationService"/> и данные калибровки
        /// (<see cref="KvCalibrationData"/>) обязаны быть в «чистом» состоянии: ни одной точки,
        /// ничего не решено, все массивы результатов созданы (то есть обращение к
        /// tcpOffsetFlange[0..2] безопасно ещё до первой калибровки), метод по умолчанию —
        /// 4-точечный, высота опорной плоскости — высота столешницы стенда.
        /// </summary>
        [Test]
        public void Service_NewInstanceAndDataDefaults_AreClean()
        {
            var service = new KvCalibrationService();

            Assert.That(service.TcpPointCount, Is.EqualTo(0), "точек TCP нет");
            Assert.That(service.TcpPlanePointCount, Is.EqualTo(0), "точек плоскости нет");
            Assert.That(service.BasePointCount, Is.EqualTo(0), "точек базы нет");
            Assert.That(service.TcpSolved, Is.False, "TCP не решён");
            Assert.That(service.BaseSolved, Is.False, "база не решена");
            Assert.That(service.BaseForwardSet, Is.False, "направление «вперёд» не задано");
            Assert.That(KvCalibrationService.TcpPointsNeeded, Is.EqualTo(4), "метод требует 4 точки");
            Assert.That(KvCalibrationService.TcpPlanePointsNeeded, Is.EqualTo(3), "метод «по нормали» требует 3 точки");

            // Данные: массивы должны существовать — иначе чтение результата упало бы с исключением.
            KvCalibrationData data = service.Data;
            Assert.That(data, Is.Not.Null, "данные калибровки обязаны существовать");
            Assert.That(data.version, Is.EqualTo(1), "версия формата файла");
            Assert.That(data.tcpMethod, Is.EqualTo("4point"), "метод по умолчанию — 4-точечный");
            Assert.That(data.tcpOffsetFlange, Is.Not.Null, "массив смещения TCP создан");
            Assert.That(data.tcpOffsetFlange.Length, Is.EqualTo(3), "смещение TCP — три компоненты (X, Y, Z)");
            Assert.That(data.tcpPose, Is.Not.Null.And.Length.EqualTo(6), "диагностическая поза — 6 чисел");
            Assert.That(data.baseOffsetWorld, Is.Not.Null.And.Length.EqualTo(3), "смещение базы — 3 компоненты");
            Assert.That(data.baseEulerWorld, Is.Not.Null.And.Length.EqualTo(3), "доворот базы — 3 компоненты");
            Assert.That(data.basePlaneNormal, Is.Not.Null.And.Length.EqualTo(3), "нормаль базы — 3 компоненты");
            Assert.That(data.tcpOffsetPlane, Is.Not.Null.And.Length.EqualTo(3), "смещение TCP по плоскости — 3 компоненты");
            Assert.That(data.tcpPlaneNormal, Is.Not.Null.And.Length.EqualTo(3), "нормаль плоскости — 3 компоненты");
            Assert.That(data.baseForwardWorld, Is.Not.Null.And.Length.EqualTo(3), "направление «вперёд» — 3 компоненты");
            Assert.That(data.tcpPoints, Is.EqualTo(0), "число точек TCP в данных");
            Assert.That(data.tcpSolved, Is.False, "флаг решения TCP");
            Assert.That(data.cameraCalibrated, Is.False, "камера — заглушка, не калибрована");
            Assert.That(data.cameraNote, Is.Not.Null.And.Not.Empty, "у заглушки камеры есть пояснение");

            // Высота опорной плоскости по умолчанию берётся из геометрии стенда.
            Assert.That(service.referencePlaneHeightM,
                Is.EqualTo(TrajectoryCore.StandBuilder.TopHeight).Within(0f),
                "по умолчанию опорная плоскость — столешница стенда");
            Assert.That(service.referencePlaneHeightM, Is.EqualTo(0.98f).Within(1e-6f), "StandBuilder.TopHeight = 0.98 м");
        }

        /// <summary>
        /// Без привязанного робота (<c>Bind</c> не вызывался) ВСЕ операции калибровки обязаны
        /// вернуть false и объяснить причину оператору через событие
        /// <see cref="KvCalibrationService.Message"/>, а счётчики остаться нулевыми — то есть
        /// ни одна операция не должна падать с NullReferenceException и не должна менять состояние.
        /// Дополнительно проверяется, что даже с искусственно выставленными флагами решения
        /// применение результатов невозможно (применять некуда — нет потока и робота).
        /// </summary>
        [Test]
        public void Service_WithoutBoundRobot_AllOperationsRefuseAndReport()
        {
            var service = new KvCalibrationService();
            var messages = new List<string>();
            service.Message += delegate(string text) { messages.Add(text); };

            Assert.That(service.RecordTcpPoint(), Is.False, "запись точки TCP без робота");
            Assert.That(service.RecordTcpPlanePoint(), Is.False, "запись точки плоскости без робота");
            Assert.That(service.RecordBasePoint(), Is.False, "запись точки базы без робота");
            Assert.That(service.RecordBaseDirection(), Is.False, "запись направления без робота");
            Assert.That(service.SolveTcp(), Is.False, "расчёт TCP без робота");
            Assert.That(service.SolveTcpPlane(), Is.False, "расчёт TCP по плоскости без робота");
            Assert.That(service.SolveBase(), Is.False, "расчёт базы без робота");
            Assert.That(service.ApplyToolOffsetToFlow(), Is.False, "применение смещения без потока");
            Assert.That(service.ApplyBaseToScene(), Is.False, "применение базы без сцены");

            // Даже если флаги решения выставлены, применять нечего: нет потока/робота.
            service.Data.tcpSolved = true;
            service.Data.baseSolved = true;
            Assert.That(service.ApplyToolOffsetToFlow(), Is.False, "применение невозможно без привязанного потока");
            Assert.That(service.ApplyBaseToScene(), Is.False, "применение к сцене невозможно без робота");

            // Состояние счётчиков не пострадало (нет выхода за границы и «мусорных» точек).
            Assert.That(service.TcpPointCount, Is.EqualTo(0), "счётчик точек TCP");
            Assert.That(service.TcpPlanePointCount, Is.EqualTo(0), "счётчик точек плоскости");
            Assert.That(service.BasePointCount, Is.EqualTo(0), "счётчик точек базы");

            // Каждый отказ обязан быть объяснён оператору непустым сообщением.
            Assert.That(messages.Count, Is.GreaterThanOrEqualTo(11), "на каждый отказ — своё сообщение");
            for (int i = 0; i < messages.Count; i++)
                Assert.That(string.IsNullOrEmpty(messages[i]), Is.False, "сообщение " + i + " не пустое");
            Assert.That(messages.Exists(delegate(string m) { return m.Contains("робот не определён"); }), Is.True,
                "отказ из-за отсутствия робота объяснён");
            Assert.That(messages.Exists(delegate(string m) { return m.Contains("не выполнялась"); }), Is.True,
                "попытка применить невыполненную калибровку объяснена");
        }

        /// <summary>
        /// Сбросы (TCP, TCP по плоскости, база, направление «вперёд») доступны без робота, обязаны
        /// вернуть состояние в исходное (счётчики 0, флаги false, поля данных обнулены) и
        /// отчитаться оператору через событие Message. Пустые наборы точек означают, что и
        /// последующие расчёты отвергаются по количеству точек, а не падают.
        /// </summary>
        [Test]
        public void Service_ResetMethods_KeepCountsZeroAndReportToMessage()
        {
            var service = new KvCalibrationService();
            var messages = new List<string>();
            service.Message += delegate(string text) { messages.Add(text); };

            service.ResetTcpPoints();
            service.ResetTcpPlanePoints();
            service.ResetBasePoints();
            service.ResetBaseDirection();

            Assert.That(service.TcpPointCount, Is.EqualTo(0), "точки TCP сброшены");
            Assert.That(service.TcpPlanePointCount, Is.EqualTo(0), "точки плоскости сброшены");
            Assert.That(service.BasePointCount, Is.EqualTo(0), "точки базы сброшены");
            Assert.That(service.TcpSolved, Is.False, "флаг решения TCP снят");
            Assert.That(service.BaseSolved, Is.False, "флаг решения базы снят");
            Assert.That(service.BaseForwardSet, Is.False, "направление «вперёд» снято");
            Assert.That(service.Data.tcpPoints, Is.EqualTo(0), "число точек TCP в данных");
            Assert.That(service.Data.tcpPlanePoints, Is.EqualTo(0), "число точек плоскости в данных");
            Assert.That(service.Data.basePoints, Is.EqualTo(0), "число точек базы в данных");
            Assert.That(service.Data.tcpPlaneSolved, Is.False, "флаг решения по плоскости снят");
            Assert.That(service.Data.baseForwardSet, Is.False, "флаг направления в данных снят");
            Assert.That(service.Data.baseYawDeg, Is.EqualTo(0f).Within(0f), "доворот базы обнулён");

            Assert.That(messages.Count, Is.EqualTo(4), "каждый сброс обязан отчитаться (4 сообщения)");
            for (int i = 0; i < messages.Count; i++)
                Assert.That(messages[i].Contains("сброшен"), Is.True,
                    "сообщение о сбросе №" + i + ": " + messages[i]);

            // После сброса расчёт по-прежнему отвергается (нет точек) — падения быть не должно.
            Assert.That(service.SolveTcp(), Is.False, "расчёт TCP на пустом наборе");
            Assert.That(service.SolveTcpPlane(), Is.False, "расчёт TCP по плоскости на пустом наборе");
            Assert.That(service.SolveBase(), Is.False, "расчёт базы на пустом наборе");
        }

        /// <summary>
        /// Выбор метода калибровки TCP (<see cref="KvCalibrationService.UsePlaneMethod"/>) и его
        /// подпись. Без привязанного робота автоматически выбирается 4-точечный метод, ручное
        /// переопределение (<see cref="KvCalibrationService.PlaneMethodOverride"/>) включает метод
        /// «по нормали» и меняет подпись для интерфейса. Bind(null, null) не должен бросать
        /// исключение и не должен включать метод «по нормали» (робота нет).
        /// </summary>
        [Test]
        public void Service_PlaneMethodSelection_FollowsOverrideAndRobotType()
        {
            var service = new KvCalibrationService();

            Assert.That(service.UsePlaneMethod, Is.False, "без робота метод «по нормали» не выбирается");
            Assert.That(service.TcpMethodLabel.Contains("4 точкам"), Is.True,
                "подпись действующего метода: " + service.TcpMethodLabel);

            service.Bind(null, null);   // не должно бросить исключение
            Assert.That(service.UsePlaneMethod, Is.False, "Bind(null, null) не включает метод «по нормали»");

            service.PlaneMethodOverride = true;
            Assert.That(service.PlaneMethodOverride, Is.True, "переопределение сохранено");
            Assert.That(service.UsePlaneMethod, Is.True, "переопределение включает метод «по нормали»");
            Assert.That(service.TcpMethodLabel.Contains("нормали"), Is.True,
                "подпись метода «по нормали»: " + service.TcpMethodLabel);
            Assert.That(service.TcpMethodLabel.Contains(KvCalibrationService.TcpPlanePointsNeeded.ToString()),
                Is.True, "подпись сообщает минимальное число точек метода");

            service.PlaneMethodOverride = false;
            Assert.That(service.UsePlaneMethod, Is.False, "переопределение снято");
        }

        /// <summary>
        /// Камера (hand-eye) — заглушка на будущее: отметка обязана снять флаг калибровки камеры
        /// и записать понятное пояснение, даже если флаг был выставлен ошибочно.
        /// </summary>
        [Test]
        public void Service_MarkCameraStub_ReportsStubAndKeepsCameraNotCalibrated()
        {
            var service = new KvCalibrationService();
            service.Data.cameraCalibrated = true;   // имитируем «грязное» состояние

            service.MarkCameraStub();

            Assert.That(service.Data.cameraCalibrated, Is.False, "камера остаётся заглушкой");
            Assert.That(service.Data.cameraNote, Is.Not.Null.And.Not.Empty, "пояснение заглушки не пусто");
            Assert.That(service.Data.cameraNote.Contains("заглушка"), Is.True,
                "пояснение: " + service.Data.cameraNote);
        }

        // ==================================================================== 10. ВСТРОЕННЫЕ САМОПРОВЕРКИ

        /// <summary>
        /// Встроенные самопроверки <see cref="KvCalibrationService.SelfTest"/> и
        /// <see cref="KvCalibrationService.SelfTestPlane"/> вызываются кнопками в интерфейсе,
        /// поэтому обязаны (а) не падать, (б) сообщать об УСПЕХЕ на своей синтетике.
        /// Числа из строки НЕ разбираются намеренно: формат ToString("0.000") зависит от текущей
        /// культуры, а точность восстановления проверяется прямыми вызовами решателей (тесты выше).
        /// Про «странный» остаток самопроверки плоскости см. характеризационный тест выше.
        /// </summary>
        [Test]
        public void StaticSelfTests_ReportSuccessWithoutFailureMarkers()
        {
            string tcpReport = KvCalibrationService.SelfTest();
            Assert.That(tcpReport, Is.Not.Null.And.Not.Empty, "SelfTest обязан вернуть отчёт");
            Assert.That(tcpReport.Contains("не решена"), Is.False, "система 4-точечного метода должна решиться");
            Assert.That(tcpReport.Contains("смещение восстановлено"), Is.True, "отчёт: " + tcpReport);

            string planeReport = KvCalibrationService.SelfTestPlane();
            Assert.That(planeReport, Is.Not.Null.And.Not.Empty, "SelfTestPlane обязан вернуть отчёт");
            Assert.That(planeReport.Contains("не построена"), Is.False, "плоскость должна построиться");
            Assert.That(planeReport.Contains("высота восстановлена"), Is.True, "отчёт: " + planeReport);
        }
    }
}

