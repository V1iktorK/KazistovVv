using System;
using NUnit.Framework;
using KazistovVvFeatures;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты чистой математики траекторий (класс <see cref="KvTrajMath"/>, namespace KazistovVvFeatures).
    ///
    /// Покрыты только ЧИСТЫЕ вычисления, не требующие сцены: кривизна полилинии TCP, сглаживание пути
    /// (<see cref="KvSmoothMethod"/>), аналитический S-профиль по длине пути, измерение рывка конечными
    /// разностями, статистика (<see cref="KvTrajStats"/>), модель энергии (<see cref="KvEnergyModel"/>)
    /// и лимиты движения (<see cref="KvMotionLimits"/>), включая копирование и защитные ветки.
    ///
    /// Тесты не создают GameObject/MonoBehaviour, не используют UnityEditor, сцену, время, файлы и сеть,
    /// не зависят от порядка выполнения и не используют случайность, поэтому детерминированы.
    /// Список непокрытого (требующего рефакторинга) — в конце файла.
    /// </summary>
    [TestFixture]
        [Category(KvTestCategories.Fast)]
    public class KvTrajMathTests
    {
        // ==================================================================== вспомогательное

        /// <summary>Все три метода сглаживания — для параметризованных проверок.</summary>
        private static KvSmoothMethod[] AllMethods()
        {
            return new[] { KvSmoothMethod.BSpline, KvSmoothMethod.Bezier, KvSmoothMethod.Gauss };
        }

        /// <summary>Прямой путь в одном суставе: q[i] = i * step.</summary>
        private static double[][] StraightPath(int count, double step)
        {
            double[][] path = new double[count][];
            for (int i = 0; i < count; i++) path[i] = new[] { i * step };
            return path;
        }

        /// <summary>«Изломанный» путь в одном суставе: 0, 1, 0, 1, … — максимальная шероховатость.</summary>
        private static double[][] ZigzagPath(int count)
        {
            double[][] path = new double[count][];
            for (int i = 0; i < count; i++) path[i] = new[] { i % 2 == 0 ? 0.0 : 1.0 };
            return path;
        }

        /// <summary>Кумулятивная длина пути вдоль равномерных шагов (аргумент S-профиля).</summary>
        private static double[] CumulativeS(int count, double step)
        {
            double[] s = new double[count];
            for (int i = 0; i < count; i++) s[i] = i * step;
            return s;
        }

        /// <summary>«Шероховатость» пути — сумма модулей вторых разностей по всем суставам.</summary>
        private static double Roughness(double[][] path)
        {
            double sum = 0.0;
            for (int i = 1; i + 1 < path.Length; i++)
                for (int j = 0; j < path[i].Length; j++)
                    sum += Math.Abs(path[i + 1][j] - 2.0 * path[i][j] + path[i - 1][j]);
            return sum;
        }

        /// <summary>План из пути без времён (времена проставит EnsureTimes или SProfileBuild).</summary>
        private static PlannedTrajectory Plan(double[][] path)
        {
            return new PlannedTrajectory { Path = path };
        }

        /// <summary>Вызов SProfileBuild с масштабом сустава 1.0 и эталоном рывка 0 (без внешней петли подгонки).</summary>
        private static void BuildProfile(PlannedTrajectory plan, double[] s, double[] vmax, double[] amax,
            double[] jerkCap, double[] vel, float[] times, out double jerkPeak, out double accelPeak, out int clamped)
        {
            KvTrajMath.SProfileBuild(plan, new[] { 1.0 }, s, vmax, amax, jerkCap, 0.0, vel, times,
                out jerkPeak, out accelPeak, out clamped);
        }

        /// <summary>Полное время прохода равномерного пути при заданном шаге и пределе скорости.</summary>
        private static double ProfileTime(int count, double step, double velocityCap)
        {
            PlannedTrajectory plan = Plan(StraightPath(count, step));
            double[] s = CumulativeS(count, step);
            double[] vmax = new double[count];
            double[] amax = new double[count];
            double[] jerkCap = new double[count];
            for (int i = 0; i < count; i++)
            {
                vmax[i] = (i == 0 || i == count - 1) ? 0.0 : velocityCap;
                amax[i] = 1.0;
                jerkCap[i] = 1e6;
            }
            double[] vel = new double[count];
            float[] times = new float[count];
            double jerkPeak, accelPeak;
            int clamped;
            BuildProfile(plan, s, vmax, amax, jerkCap, vel, times, out jerkPeak, out accelPeak, out clamped);
            return times[count - 1];
        }

        /// <summary>Путь не содержит NaN и ±∞ ни в одном сэмпле.</summary>
        private static void AssertPathFinite(double[][] path)
        {
            for (int i = 0; i < path.Length; i++)
            {
                Assert.That(path[i], Is.Not.Null, "строка пути не должна быть null");
                for (int j = 0; j < path[i].Length; j++)
                    Assert.That(double.IsNaN(path[i][j]) || double.IsInfinity(path[i][j]), Is.False,
                        "нечисловое значение: сэмпл " + i + ", сустав " + j);
            }
        }

        /// <summary>Одномерный массив не содержит NaN и ±∞.</summary>
        private static void AssertFinite(double[] values)
        {
            for (int i = 0; i < values.Length; i++)
                Assert.That(double.IsNaN(values[i]) || double.IsInfinity(values[i]), Is.False, "индекс " + i);
        }

        /// <summary>Одномерный массив не содержит NaN и ±∞.</summary>
        private static void AssertFinite(float[] values)
        {
            for (int i = 0; i < values.Length; i++)
                Assert.That(float.IsNaN(values[i]) || float.IsInfinity(values[i]), Is.False, "индекс " + i);
        }

        #region 1. Длина пути и TCP-полилиния (контракт без сцены)

        /// <summary>
        /// Проверяется защитный контракт расчёта длины: без готового валидатора позы (Ready == false)
        /// и без пути метод обязан вернуть ровно 0 — без исключений, NaN и бесконечностей.
        /// </summary>
        [Test]
        public void PathLength_UnavailableValidatorOrPath_ReturnsZero()
        {
            double[][] path = StraightPath(5, 1.0);

            Assert.That(KvTrajMath.PathLength(null, path), Is.EqualTo(0f).Within(1e-6f), "null-валидатор");
            Assert.That(KvTrajMath.PathLength(new PoseValidator(), path), Is.EqualTo(0f).Within(1e-6f), "валидатор без Init");
            Assert.That(KvTrajMath.PathLength(null, null), Is.EqualTo(0f).Within(1e-6f), "null-путь");
            Assert.That(KvTrajMath.PathLength(new PoseValidator(), new double[0][]), Is.EqualTo(0f).Within(1e-6f), "пустой путь");
            Assert.That(KvTrajMath.PathLength(new PoseValidator(), new[] { new[] { 0.0 } }), Is.EqualTo(0f).Within(1e-6f), "одна точка");

            float length = KvTrajMath.PathLength(null, path);
            Assert.That(float.IsNaN(length) || float.IsInfinity(length), Is.False, "результат обязан быть числом");
        }

        /// <summary>
        /// TCP-полилиния и плечи удержания без готового валидатора позы обязаны вернуть пустой массив
        /// (а не null), чтобы вызывающий код мог безопасно его обходить.
        /// </summary>
        [Test]
        public void TcpPolylineAndLeverArms_UnavailableValidator_ReturnEmptyArrays()
        {
            double[][] path = StraightPath(4, 0.5);

            Assert.That(KvTrajMath.TcpPolyline(null, path), Is.Not.Null);
            Assert.That(KvTrajMath.TcpPolyline(null, path).Length, Is.EqualTo(0));
            Assert.That(KvTrajMath.TcpPolyline(new PoseValidator(), path).Length, Is.EqualTo(0));
            Assert.That(KvTrajMath.TcpPolyline(new PoseValidator(), null).Length, Is.EqualTo(0));

            Assert.That(KvTrajMath.LeverArms(null, path, Vector3.zero), Is.Not.Null);
            Assert.That(KvTrajMath.LeverArms(null, path, Vector3.zero).Length, Is.EqualTo(0));
            Assert.That(KvTrajMath.LeverArms(new PoseValidator(), path, Vector3.zero).Length, Is.EqualTo(0));
        }

        #endregion

        #region 2. Кривизна полилинии TCP

        /// <summary>
        /// Прямая полилиния: направление не меняется, поэтому и максимум, и среднее кривизны равны нулю.
        /// </summary>
        [Test]
        public void Curvature_StraightPolyline_ReturnsZeroMaxAndAverage()
        {
            Vector3[] poly =
            {
                new Vector3(0f, 0f, 0f), new Vector3(0.25f, 0f, 0f), new Vector3(0.5f, 0f, 0f),
                new Vector3(0.75f, 0f, 0f), new Vector3(1f, 0f, 0f)
            };
            float max, average;
            KvTrajMath.Curvature(poly, out max, out average);

            Assert.That(max, Is.EqualTo(0f).Within(1e-4f), "у прямой кривизна нулевая");
            Assert.That(average, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(float.IsNaN(max) || float.IsInfinity(max), Is.False);
            Assert.That(float.IsNaN(average) || float.IsInfinity(average), Is.False);
        }

        /// <summary>
        /// Известная дуга: три точки на окружности радиуса R дают кривизну Менгера ровно 1/R,
        /// поэтому для R = 2 ожидается 0.5 (и максимум, и среднее — точек перелома всего одна).
        /// </summary>
        [Test]
        public void Curvature_ThreePointsOnCircleRadiusTwo_ReturnsHalf()
        {
            const float radius = 2f;
            float c = Mathf.Cos(Mathf.PI / 6f);
            float s = Mathf.Sin(Mathf.PI / 6f);
            Vector3[] poly =
            {
                new Vector3(radius * c, -radius * s, 0f),
                new Vector3(radius, 0f, 0f),
                new Vector3(radius * c, radius * s, 0f)
            };
            float max, average;
            KvTrajMath.Curvature(poly, out max, out average);

            Assert.That(max, Is.EqualTo(1f / radius).Within(1e-3f), "k = 1/R = 0.5");
            Assert.That(average, Is.EqualTo(1f / radius).Within(1e-3f));
            Assert.That(max, Is.GreaterThan(0f));
        }

        /// <summary>
        /// Уменьшение радиуса дуги вдвое удваивает кривизну: для R = 0.5 ожидается k = 2.
        /// </summary>
        [Test]
        public void Curvature_SmallerCircleRadius_ReturnsProportionallyLargerCurvature()
        {
            const float radius = 0.5f;
            float c = Mathf.Cos(Mathf.PI / 6f);
            float s = Mathf.Sin(Mathf.PI / 6f);
            Vector3[] poly =
            {
                new Vector3(radius * c, -radius * s, 0f),
                new Vector3(radius, 0f, 0f),
                new Vector3(radius * c, radius * s, 0f)
            };
            float max, average;
            KvTrajMath.Curvature(poly, out max, out average);

            Assert.That(max, Is.EqualTo(1f / radius).Within(1e-3f), "R = 0.5 → k = 2");
            Assert.That(average, Is.EqualTo(1f / radius).Within(1e-3f));
            Assert.That(max, Is.GreaterThan(1f), "кривизна малой дуги больше, чем у R = 2 (k = 0.5)");
        }

        /// <summary>
        /// Прямой участок + поворот на 90°: в точке поворота k = 2|a×b| / (|a||b||a+b|) = √2,
        /// среднее по двум переломам (нулевому и √2) равно √2/2.
        /// </summary>
        [Test]
        public void Curvature_RightAngleTurn_ReturnsRootTwoMaxAndHalfOfItAsAverage()
        {
            Vector3[] poly =
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(2f, 0f, 0f), new Vector3(2f, 1f, 0f)
            };
            float max, average;
            KvTrajMath.Curvature(poly, out max, out average);

            Assert.That(max, Is.EqualTo(Mathf.Sqrt(2f)).Within(1e-3f), "поворот на 90° → √2");
            Assert.That(average, Is.EqualTo(Mathf.Sqrt(2f) * 0.5f).Within(1e-3f), "среднее по прямому и поворотному переломам");
            Assert.That(max, Is.GreaterThanOrEqualTo(average));
        }

        /// <summary>
        /// Вырожденные входы кривизны: null, пустая полилиния, одна и две точки — нули без исключений.
        /// </summary>
        [Test]
        public void Curvature_NullOrTooShortPolyline_ReturnsZero()
        {
            float max, average;

            KvTrajMath.Curvature(null, out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(average, Is.EqualTo(0f).Within(1e-6f));

            KvTrajMath.Curvature(new Vector3[0], out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "пустая полилиния");

            KvTrajMath.Curvature(new[] { new Vector3(1f, 2f, 3f) }, out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "одна точка");

            KvTrajMath.Curvature(new[] { new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f) }, out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "две точки — переломов нет");
            Assert.That(average, Is.EqualTo(0f).Within(1e-6f));

            for (int i = 0; i < 4; i++)
                Assert.That(float.IsNaN(max), Is.False, "кривизна обязана быть числом");
        }

        /// <summary>
        /// Совпадающие точки, нулевые сегменты и сегменты короче 1e-6 м пропускаются: деления на ноль,
        /// NaN и бесконечностей быть не должно.
        /// </summary>
        [Test]
        public void Curvature_DuplicateZeroLengthAndSubMicrometerSegments_AreSkippedWithoutNaN()
        {
            float max, average;

            KvTrajMath.Curvature(new[] { new Vector3(0f, 0f, 0f), new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f) },
                out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "две совпадающие точки");
            Assert.That(average, Is.EqualTo(0f).Within(1e-6f));

            KvTrajMath.Curvature(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1f, 0f, 0f),
                new Vector3(1f, 0f, 0f), new Vector3(2f, 0f, 0f)
            }, out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "нулевой сегмент в середине пути");

            Vector3 same = new Vector3(0.5f, 0.5f, 0.5f);
            KvTrajMath.Curvature(new[] { same, same, same, same }, out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "все точки совпадают");

            KvTrajMath.Curvature(new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(1e-7f, 0f, 0f), new Vector3(2e-7f, 1e-7f, 0f)
            }, out max, out average);
            Assert.That(max, Is.EqualTo(0f).Within(1e-6f), "микрошаги короче порога 1e-6 м пропускаются");

            Assert.That(float.IsNaN(max) || float.IsInfinity(max), Is.False);
            Assert.That(float.IsNaN(average) || float.IsInfinity(average), Is.False);
        }

        #endregion

        #region 3. Время, параметризация и S-профиль

        /// <summary>
        /// Без корректного пути (null-план, план без пути, пустой путь, одна точка) массив времён
        /// построить нельзя — метод обязан вернуть false и ничего не менять.
        /// </summary>
        [Test]
        public void EnsureTimes_NullOrTooShortPlan_ReturnsFalse()
        {
            Assert.That(KvTrajMath.EnsureTimes(null), Is.False, "null-план");
            Assert.That(KvTrajMath.EnsureTimes(new PlannedTrajectory()), Is.False, "план без пути");
            Assert.That(KvTrajMath.EnsureTimes(Plan(new double[0][])), Is.False, "пустой путь");
            Assert.That(KvTrajMath.EnsureTimes(Plan(new[] { new[] { 0.0 } })), Is.False, "одна точка");
            Assert.That(KvTrajMath.EnsureTimes(Plan(new[] { new[] { 0.0 }, new[] { 1.0 } })), Is.True,
                "две точки — минимально допустимый путь");
        }

        /// <summary>
        /// Отсутствующие или вырожденные (нулевые) времена заменяются равномерной рампой 0…1:
        /// иначе дискретные производные делили бы на ноль.
        /// </summary>
        [Test]
        public void EnsureTimes_MissingOrDegenerateTimes_AreReplacedByUniformRamp()
        {
            PlannedTrajectory plan = Plan(new[] { new[] { 0.0 }, new[] { 1.0 }, new[] { 2.0 } });
            Assert.That(KvTrajMath.EnsureTimes(plan), Is.True);
            Assert.That(plan.Times, Is.Not.Null);
            Assert.That(plan.Times.Length, Is.EqualTo(3), "времён столько же, сколько сэмплов");
            Assert.That(plan.Times[0], Is.EqualTo(0f).Within(1e-6f));
            Assert.That(plan.Times[1], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(plan.Times[2], Is.EqualTo(1f).Within(1e-6f));
            Assert.That((float)plan.Time, Is.EqualTo(1f).Within(1e-6f), "полное время рампы равно 1 с");
            AssertFinite(plan.Times);

            PlannedTrajectory zeroed = Plan(new[] { new[] { 0.0 }, new[] { 1.0 }, new[] { 2.0 } });
            zeroed.Times = new[] { 0f, 0f, 0f };
            Assert.That(KvTrajMath.EnsureTimes(zeroed), Is.True, "нулевая длительность недопустима");
            Assert.That(zeroed.Times[1], Is.EqualTo(0.5f).Within(1e-6f), "нулевые времена заменяются рампой");
            Assert.That(zeroed.Times[2], Is.EqualTo(1f).Within(1e-6f));
            AssertFinite(zeroed.Times);
        }

        /// <summary>
        /// Корректный монотонный массив времён не пересоздаётся и не портится (важно: алгоритмы
        /// этапа 5 опираются на уже построенную параметризацию).
        /// </summary>
        [Test]
        public void EnsureTimes_ValidMonotonicTimes_AreKeptUntouched()
        {
            PlannedTrajectory plan = Plan(new[] { new[] { 0.0 }, new[] { 1.0 }, new[] { 2.0 } });
            plan.Times = new[] { 0.25f, 0.6f, 1.4f };
            plan.Time = 1.4;
            float[] before = plan.Times;

            Assert.That(KvTrajMath.EnsureTimes(plan), Is.True);
            Assert.That(ReferenceEquals(before, plan.Times), Is.True, "готовый массив не пересоздаётся");
            Assert.That(plan.Times[0], Is.EqualTo(0.25f).Within(1e-6f));
            Assert.That(plan.Times[1], Is.EqualTo(0.6f).Within(1e-6f));
            Assert.That(plan.Times[2], Is.EqualTo(1.4f).Within(1e-6f));
            Assert.That(plan.Time, Is.EqualTo(1.4).Within(1e-9), "полное время плана не меняется");
        }

        /// <summary>
        /// S-профиль равномерного пути: времена начинаются с нуля, строго возрастают, а скорости
        /// на концах равны нулю (исполнитель стартует и финиширует из состояния покоя).
        /// </summary>
        [Test]
        public void SProfileBuild_StraightPath_ProducesIncreasingTimesFromZeroAndRestAtEnds()
        {
            const int n = 5;
            PlannedTrajectory plan = Plan(StraightPath(n, 1.0));
            double[] s = CumulativeS(n, 1.0);
            double[] vmax = { 0.0, 1.0, 1.0, 1.0, 0.0 };
            double[] amax = { 1.0, 1.0, 1.0, 1.0, 1.0 };
            double[] jerkCap = { 1e6, 1e6, 1e6, 1e6, 1e6 };
            double[] vel = new double[n];
            float[] times = new float[n];
            double jerkPeak, accelPeak;
            int clamped;

            BuildProfile(plan, s, vmax, amax, jerkCap, vel, times, out jerkPeak, out accelPeak, out clamped);

            AssertFinite(times);
            AssertFinite(vel);
            Assert.That(times[0], Is.EqualTo(0f).Within(1e-6f), "профиль стартует в момент 0");
            for (int i = 1; i < n; i++)
                Assert.That(times[i], Is.GreaterThan(times[i - 1]), "времена обязаны строго возрастать (сэмпл " + i + ")");
            Assert.That(times[n - 1], Is.GreaterThan(0f), "полное время положительно");
            Assert.That(vel[0], Is.EqualTo(0.0).Within(1e-9), "старт из покоя");
            Assert.That(vel[n - 1], Is.EqualTo(0.0).Within(1e-9), "финиш в покое");
            Assert.That(double.IsNaN(jerkPeak) || double.IsInfinity(jerkPeak), Is.False);
            Assert.That(double.IsNaN(accelPeak) || double.IsInfinity(accelPeak), Is.False);
            Assert.That(jerkPeak, Is.GreaterThanOrEqualTo(0.0));
            Assert.That(accelPeak, Is.GreaterThanOrEqualTo(0.0));
        }

        /// <summary>
        /// Предел скорости соблюдается: без срезов (clamped == 0) скорость внутри пути не превышает
        /// заданный предел ни на одном сэмпле.
        /// </summary>
        [Test]
        public void SProfileBuild_StraightPath_RespectsVelocityCapWithoutClamping()
        {
            const int n = 9;
            PlannedTrajectory plan = Plan(StraightPath(n, 1.0));
            double[] s = CumulativeS(n, 1.0);
            double[] vmax = new double[n];
            double[] amax = new double[n];
            double[] jerkCap = new double[n];
            for (int i = 0; i < n; i++)
            {
                vmax[i] = (i == 0 || i == n - 1) ? 0.0 : 1.0;
                amax[i] = 2.0;
                jerkCap[i] = 500.0;
            }
            double[] vel = new double[n];
            float[] times = new float[n];
            double jerkPeak, accelPeak;
            int clamped;

            BuildProfile(plan, s, vmax, amax, jerkCap, vel, times, out jerkPeak, out accelPeak, out clamped);

            Assert.That(clamped, Is.EqualTo(0), "профиль обязан уложиться в предел скорости без срезов");
            for (int i = 1; i + 1 < n; i++)
            {
                Assert.That(vel[i], Is.GreaterThanOrEqualTo(0.0), "скорость неотрицательна (сэмпл " + i + ")");
                Assert.That(vel[i], Is.LessThanOrEqualTo(vmax[i] * 1.001 + 1e-6), "скорость внутри предела (сэмпл " + i + ")");
            }
        }

        /// <summary>
        /// Детерминизм: тот же вход даёт бит-в-бит тот же профиль (нет случайности, времени и состояния).
        /// </summary>
        [Test]
        public void SProfileBuild_SameInputTwice_ProducesIdenticalProfile()
        {
            const int n = 9;
            PlannedTrajectory planA = Plan(StraightPath(n, 0.7));
            PlannedTrajectory planB = Plan(StraightPath(n, 0.7));
            double[] sA = CumulativeS(n, 0.7);
            double[] sB = CumulativeS(n, 0.7);
            double[] vmaxA = new double[n], vmaxB = new double[n];
            double[] amaxA = new double[n], amaxB = new double[n];
            double[] jerkA = new double[n], jerkB = new double[n];
            for (int i = 0; i < n; i++)
            {
                vmaxA[i] = vmaxB[i] = (i == 0 || i == n - 1) ? 0.0 : 0.8;
                amaxA[i] = amaxB[i] = 1.5;
                jerkA[i] = jerkB[i] = 250.0;
            }
            double[] velA = new double[n], velB = new double[n];
            float[] timesA = new float[n], timesB = new float[n];
            double jerkPeakA, accelPeakA, jerkPeakB, accelPeakB;
            int clampedA, clampedB;

            BuildProfile(planA, sA, vmaxA, amaxA, jerkA, velA, timesA, out jerkPeakA, out accelPeakA, out clampedA);
            BuildProfile(planB, sB, vmaxB, amaxB, jerkB, velB, timesB, out jerkPeakB, out accelPeakB, out clampedB);

            for (int i = 0; i < n; i++)
            {
                Assert.That(timesB[i], Is.EqualTo(timesA[i]), "время сэмпла " + i + " должно совпадать точно");
                Assert.That(velB[i], Is.EqualTo(velA[i]), "скорость сэмпла " + i + " должна совпадать точно");
            }
            Assert.That(jerkPeakB, Is.EqualTo(jerkPeakA));
            Assert.That(accelPeakB, Is.EqualTo(accelPeakA));
            Assert.That(clampedB, Is.EqualTo(clampedA));
        }

        /// <summary>
        /// Вырожденные входы S-профиля (меньше трёх сэмплов, null-план, план без пути) дают нулевые
        /// метрики и не портят выходные массивы — без исключений и делений на ноль.
        /// </summary>
        [Test]
        public void SProfileBuild_DegenerateInputs_ReturnZeroMetrics()
        {
            double[] vel = new double[4];
            float[] times = new float[4];
            double jerkPeak, accelPeak;
            int clamped;

            double[] shortS = { 0.0, 1.0 };
            double[] vmax = { 0.0, 1.0, 1.0, 0.0 };
            double[] amax = { 1.0, 1.0, 1.0, 1.0 };
            double[] jerkCap = { 1e6, 1e6, 1e6, 1e6 };

            BuildProfile(Plan(StraightPath(4, 1.0)), shortS, vmax, amax, jerkCap, vel, times,
                out jerkPeak, out accelPeak, out clamped);
            Assert.That(jerkPeak, Is.EqualTo(0.0), "n < 3 — расчёт не выполняется");
            Assert.That(accelPeak, Is.EqualTo(0.0));
            Assert.That(clamped, Is.EqualTo(0));
            Assert.That(times[0], Is.EqualTo(0f), "выходные массивы не должны портиться");
            Assert.That(vel[0], Is.EqualTo(0.0));
            AssertFinite(times);
            AssertFinite(vel);

            KvTrajMath.SProfileBuild(null, new[] { 1.0 }, shortS, vmax, amax, jerkCap, 0.0, vel, times,
                out jerkPeak, out accelPeak, out clamped);
            Assert.That(jerkPeak, Is.EqualTo(0.0), "null-план");
            Assert.That(accelPeak, Is.EqualTo(0.0));

            KvTrajMath.SProfileBuild(new PlannedTrajectory(), new[] { 1.0 }, shortS, vmax, amax, jerkCap, 0.0,
                vel, times, out jerkPeak, out accelPeak, out clamped);
            Assert.That(clamped, Is.EqualTo(0), "план без пути");
            AssertFinite(times);
        }

        /// <summary>
        /// Монотонность по длине пути: вчетверо более длинный путь требует строго большего времени.
        /// </summary>
        [Test]
        public void SProfileBuild_LongerPath_TakesMoreTime()
        {
            double shortTime = ProfileTime(9, 1.0, 1.0);
            double longTime = ProfileTime(9, 4.0, 1.0);

            Assert.That(shortTime, Is.GreaterThan(0.0), "время прохода положительно");
            Assert.That(longTime, Is.GreaterThan(shortTime), "больше путь → больше время");
            Assert.That(double.IsNaN(longTime) || double.IsInfinity(longTime), Is.False);
        }

        /// <summary>
        /// Монотонность по пределу скорости: чем выше допустимая скорость, тем меньше время прохода.
        /// </summary>
        [Test]
        public void SProfileBuild_HigherVelocityCap_TakesLessTime()
        {
            double slow = ProfileTime(9, 1.0, 0.5);
            double middle = ProfileTime(9, 1.0, 1.0);
            double fast = ProfileTime(9, 1.0, 2.0);

            Assert.That(middle, Is.LessThan(slow), "выше предел → меньше время (0.5 → 1.0)");
            Assert.That(fast, Is.LessThan(middle), "выше предел → меньше время (1.0 → 2.0)");
            Assert.That(fast, Is.GreaterThan(0.0), "время остаётся положительным");
        }

        /// <summary>
        /// Длинный путь (200 сэмплов, 200 м): профиль обязан остаться численным, времена —
        /// неубывающими, скорости — внутри предела, метрики — без NaN.
        /// </summary>
        [Test]
        public void SProfileBuild_LongPath_ProducesFiniteMonotonicProfile()
        {
            const int n = 201;
            PlannedTrajectory plan = Plan(StraightPath(n, 1.0));
            double[] s = CumulativeS(n, 1.0);
            double[] vmax = new double[n];
            double[] amax = new double[n];
            double[] jerkCap = new double[n];
            for (int i = 0; i < n; i++)
            {
                vmax[i] = (i == 0 || i == n - 1) ? 0.0 : 0.5;
                amax[i] = 2.0;
                jerkCap[i] = 100.0;
            }
            double[] vel = new double[n];
            float[] times = new float[n];
            double jerkPeak, accelPeak;
            int clamped;

            BuildProfile(plan, s, vmax, amax, jerkCap, vel, times, out jerkPeak, out accelPeak, out clamped);

            AssertFinite(times);
            AssertFinite(vel);
            Assert.That(times[0], Is.EqualTo(0f).Within(1e-6f));
            for (int i = 1; i < n; i++)
                Assert.That(times[i], Is.GreaterThanOrEqualTo(times[i - 1]), "времена не должны убывать (сэмпл " + i + ")");
            Assert.That(times[n - 1], Is.GreaterThan(0f), "полное время положительно");
            for (int i = 0; i < n; i++)
                Assert.That(vel[i], Is.LessThanOrEqualTo(vmax[i] + 1e-3), "скорость внутри предела (сэмпл " + i + ")");
            Assert.That(double.IsNaN(jerkPeak) || double.IsInfinity(jerkPeak), Is.False);
            Assert.That(double.IsNaN(accelPeak) || double.IsInfinity(accelPeak), Is.False);
        }

        #endregion

        #region 4. Сглаживание пути (KvSmoothMethod)

        /// <summary>
        /// null-путь сглаживать нечего: метод обязан вернуть null для всех трёх методов.
        /// </summary>
        [Test]
        public void Smooth_NullPath_ReturnsNull()
        {
            foreach (KvSmoothMethod method in AllMethods())
                Assert.That(KvTrajMath.Smooth(null, method, 1f), Is.Null, method.ToString());
        }

        /// <summary>
        /// Уровень сглаживания обрезается в [0, 1]: нулевой и отрицательный уровень возвращают
        /// независимую копию пути без изменений, а уровень больше 1 совпадает с полным сглаживанием.
        /// </summary>
        [Test]
        public void Smooth_LevelIsClampedToUnitRange()
        {
            double[][] path = ZigzagPath(9);
            foreach (KvSmoothMethod method in AllMethods())
            {
                double[][] zero = KvTrajMath.Smooth(path, method, 0f);
                double[][] negative = KvTrajMath.Smooth(path, method, -5f);
                double[][] full = KvTrajMath.Smooth(path, method, 1f);
                double[][] above = KvTrajMath.Smooth(path, method, 5f);

                Assert.That(zero.Length, Is.EqualTo(path.Length));
                Assert.That(ReferenceEquals(zero, path), Is.False, method + ": результат — новая структура");
                for (int i = 0; i < path.Length; i++)
                {
                    Assert.That(zero[i][0], Is.EqualTo(path[i][0]), method + ": уровень 0 не меняет путь");
                    Assert.That(negative[i][0], Is.EqualTo(path[i][0]), method + ": отрицательный уровень = 0");
                    Assert.That(above[i][0], Is.EqualTo(full[i][0]).Within(1e-12), method + ": уровень > 1 обрезается до 1");
                }
            }
        }

        /// <summary>
        /// Меньше четырёх сэмплов сглаживать нечем: путь возвращается копией без изменений
        /// (проверяются массивы из одной, двух и трёх точек).
        /// </summary>
        [Test]
        public void Smooth_LessThanFourPoints_ReturnsUnchangedCopy()
        {
            double[][] one = KvTrajMath.Smooth(new[] { new[] { 2.5 } }, KvSmoothMethod.Gauss, 1f);
            Assert.That(one.Length, Is.EqualTo(1));
            Assert.That(one[0][0], Is.EqualTo(2.5));

            double[][] two = KvTrajMath.Smooth(new[] { new[] { 0.0 }, new[] { 1.0 } }, KvSmoothMethod.BSpline, 1f);
            Assert.That(two.Length, Is.EqualTo(2));
            Assert.That(two[0][0], Is.EqualTo(0.0));
            Assert.That(two[1][0], Is.EqualTo(1.0), "конец пути не смещается");

            double[][] three = KvTrajMath.Smooth(new[] { new[] { 0.0 }, new[] { 1.0 }, new[] { 0.0 } },
                KvSmoothMethod.Bezier, 1f);
            Assert.That(three.Length, Is.EqualTo(3));
            Assert.That(three[0][0], Is.EqualTo(0.0));
            Assert.That(three[1][0], Is.EqualTo(1.0));
            Assert.That(three[2][0], Is.EqualTo(0.0));
            AssertPathFinite(three);
        }

        /// <summary>
        /// Ключевое требование ТЗ: траектория обязана начинаться и заканчиваться там, где её построил
        /// планировщик, поэтому первая и последняя точки не смещаются ни одним методом; результат —
        /// конечные числа и та же размерность.
        /// </summary>
        [Test]
        public void Smooth_AllMethods_PreserveEndsAndProduceFiniteValues()
        {
            double[][] path = ZigzagPath(9);
            foreach (KvSmoothMethod method in AllMethods())
            {
                double[][] smooth = KvTrajMath.Smooth(path, method, 1f);

                Assert.That(smooth.Length, Is.EqualTo(path.Length), method + ": число сэмплов сохраняется");
                Assert.That(smooth[0].Length, Is.EqualTo(path[0].Length), method + ": число суставов сохраняется");
                Assert.That(smooth[0][0], Is.EqualTo(path[0][0]), method + ": первая точка фиксирована");
                Assert.That(smooth[path.Length - 1][0], Is.EqualTo(path[path.Length - 1][0]),
                    method + ": последняя точка фиксирована");
                AssertPathFinite(smooth);
            }
        }

        /// <summary>
        /// Сглаживание действительно убирает изломы: шероховатость зигзага (сумма модулей вторых
        /// разностей) падает более чем вдвое при уровне 1 и уменьшается при уровне 0.5.
        /// </summary>
        [Test]
        public void Smooth_BSplineAndGauss_ReduceRoughnessOfZigzag()
        {
            double[][] path = ZigzagPath(9);
            double before = Roughness(path);
            Assert.That(before, Is.GreaterThan(0.0), "исходный зигзаг обязан быть шероховатым");

            Assert.That(Roughness(KvTrajMath.Smooth(path, KvSmoothMethod.BSpline, 1f)), Is.LessThan(before * 0.5f),
                "B-сплайн при уровне 1 убирает большую часть изломов");
            Assert.That(Roughness(KvTrajMath.Smooth(path, KvSmoothMethod.Gauss, 1f)), Is.LessThan(before * 0.5f),
                "фильтр Гаусса при уровне 1 убирает большую часть изломов");
            Assert.That(Roughness(KvTrajMath.Smooth(path, KvSmoothMethod.BSpline, 0.5f)), Is.LessThan(before),
                "уровень 0.5 уменьшает шероховатость");
            Assert.That(Roughness(KvTrajMath.Smooth(path, KvSmoothMethod.Gauss, 0.5f)), Is.LessThan(before),
                "уровень 0.5 уменьшает шероховатость");
        }

        /// <summary>
        /// Сглаживание B-сплайном и Гауссом — выпуклая комбинация сэмплов, поэтому значения не выходят
        /// за диапазон входа и не «разъезжаются» по суставам (проверка на двухсуставном пути).
        /// </summary>
        [Test]
        public void Smooth_BSplineAndGauss_KeepValuesWithinInputRangeAndDof()
        {
            double[][] path = new double[12][];
            for (int i = 0; i < path.Length; i++) path[i] = new[] { i * 0.5, i % 2 == 0 ? 0.0 : 2.0 };

            double low = double.MaxValue, high = double.MinValue;
            for (int i = 0; i < path.Length; i++)
                for (int j = 0; j < path[i].Length; j++)
                {
                    if (path[i][j] < low) low = path[i][j];
                    if (path[i][j] > high) high = path[i][j];
                }

            foreach (KvSmoothMethod method in new[] { KvSmoothMethod.BSpline, KvSmoothMethod.Gauss })
            {
                double[][] smooth = KvTrajMath.Smooth(path, method, 1f);
                Assert.That(smooth.Length, Is.EqualTo(path.Length), method + ": число сэмплов");
                for (int i = 0; i < smooth.Length; i++)
                {
                    Assert.That(smooth[i].Length, Is.EqualTo(2), method + ": число суставов");
                    for (int j = 0; j < smooth[i].Length; j++)
                        Assert.That(smooth[i][j], Is.InRange(low - 1e-9, high + 1e-9),
                            method + ": значение вне диапазона входа (сэмпл " + i + ", сустав " + j + ")");
                }
            }
        }

        /// <summary>
        /// Постоянный путь (робот стоит на месте) обязан остаться постоянным после любого метода
        /// сглаживания — иначе появился бы паразитный «дрейф» при нулевом движении.
        /// </summary>
        [Test]
        public void Smooth_ConstantPath_StaysConstantForAllMethods()
        {
            double[][] path = new double[6][];
            for (int i = 0; i < path.Length; i++) path[i] = new[] { 3.0, -2.0 };

            foreach (KvSmoothMethod method in AllMethods())
            {
                double[][] smooth = KvTrajMath.Smooth(path, method, 1f);
                for (int i = 0; i < smooth.Length; i++)
                {
                    Assert.That(smooth[i][0], Is.EqualTo(3.0).Within(1e-4), method + ": сустав 0 остался постоянным");
                    Assert.That(smooth[i][1], Is.EqualTo(-2.0).Within(1e-4), method + ": сустав 1 остался постоянным");
                }
            }
        }

        /// <summary>
        /// Монотонный (равномерный) путь при сглаживании B-сплайном не должен разворачиваться:
        /// сглаживание не имеет права создавать движение назад.
        /// </summary>
        [Test]
        public void Smooth_BSpline_KeepsMonotonicRampMonotonic()
        {
            double[][] smooth = KvTrajMath.Smooth(StraightPath(11, 1.0), KvSmoothMethod.BSpline, 1f);

            for (int i = 1; i < smooth.Length; i++)
                Assert.That(smooth[i][0], Is.GreaterThan(smooth[i - 1][0]),
                    "монотонность нарушена на сэмпле " + i);
        }

        /// <summary>
        /// Сглаживание возвращает независимую структуру: изменение результата не должно менять
        /// исходный путь (иначе «исходная» и «сглаженная» траектории в интерфейсе были бы одной).
        /// </summary>
        [Test]
        public void Smooth_OutputIsNotAliasedToInput()
        {
            double[][] path = ZigzagPath(9);
            double[][] smooth = KvTrajMath.Smooth(path, KvSmoothMethod.Gauss, 1f);

            Assert.That(ReferenceEquals(smooth, path), Is.False);
            for (int i = 0; i < smooth.Length; i++)
                Assert.That(ReferenceEquals(smooth[i], path[i]), Is.False, "строка " + i + " должна быть новым массивом");

            smooth[4][0] = 12345.0;
            Assert.That(path[4][0], Is.EqualTo(0.0), "изменение результата не меняет вход");
        }

        #endregion

        #region 5. Измерение рывка (MeasureJerk)

        /// <summary>
        /// Ручной эталон: для q = [0, 0, 0, 1] и t = [0, 1, 2, 3] третья конечная разность достигает
        /// 0.25; масштаб сустава множит результат, null или короткий масштаб трактуется как 1.
        /// </summary>
        [Test]
        public void MeasureJerk_KnownSamples_ReturnsExactValueAndAppliesScale()
        {
            double[][] path = { new[] { 0.0 }, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 } };
            float[] times = { 0f, 1f, 2f, 3f };

            Assert.That(KvTrajMath.MeasureJerk(path, times, new[] { 1.0 }), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(KvTrajMath.MeasureJerk(path, times, new[] { 2.0 }), Is.EqualTo(0.5).Within(1e-9),
                "масштаб сустава умножает рывок");
            Assert.That(KvTrajMath.MeasureJerk(path, times, null), Is.EqualTo(0.25).Within(1e-9), "null-масштаб = 1");
            Assert.That(KvTrajMath.MeasureJerk(path, times, new double[0]), Is.EqualTo(0.25).Within(1e-9),
                "масштаб короче числа суставов = 1");
        }

        /// <summary>
        /// Равномерное движение: третья производная строго нулевая, поэтому фактический рывок равен нулю
        /// (в том числе на двухсуставном пути).
        /// </summary>
        [Test]
        public void MeasureJerk_ConstantVelocity_ReturnsZero()
        {
            const int n = 6;
            double[][] path = new double[n][];
            float[] times = new float[n];
            for (int i = 0; i < n; i++)
            {
                path[i] = new[] { 2.0 * i, -1.5 * i };
                times[i] = i;
            }

            Assert.That(KvTrajMath.MeasureJerk(path, times, new[] { 1.0, 1.0 }), Is.EqualTo(0.0).Within(1e-9),
                "у равномерного движения рывок нулевой");
        }

        /// <summary>
        /// Некорректные и вырожденные входы: null-путь, null-времена, несовпадение длин и меньше трёх
        /// сэмплов дают 0; полностью нулевые времена (защита от деления на ноль) дают конечное число,
        /// а не NaN/∞.
        /// </summary>
        [Test]
        public void MeasureJerk_InvalidAndDegenerateInputs_AreFinite()
        {
            double[][] path = { new[] { 0.0 }, new[] { 0.0 }, new[] { 0.0 }, new[] { 1.0 } };
            float[] times = { 0f, 1f, 2f, 3f };

            Assert.That(KvTrajMath.MeasureJerk(null, times, new[] { 1.0 }), Is.EqualTo(0.0), "null-путь");
            Assert.That(KvTrajMath.MeasureJerk(path, null, new[] { 1.0 }), Is.EqualTo(0.0), "null-времена");
            Assert.That(KvTrajMath.MeasureJerk(path, new[] { 0f, 1f, 2f }, new[] { 1.0 }), Is.EqualTo(0.0),
                "длины массивов не совпадают");
            Assert.That(KvTrajMath.MeasureJerk(new[] { new[] { 0.0 }, new[] { 1.0 } }, new[] { 0f, 1f }, new[] { 1.0 }),
                Is.EqualTo(0.0), "меньше трёх сэмплов");

            double zeroTime = KvTrajMath.MeasureJerk(path, new[] { 0f, 0f, 0f, 0f }, new[] { 1.0 });
            Assert.That(double.IsNaN(zeroTime), Is.False, "нулевые времена не должны давать NaN");
            Assert.That(double.IsInfinity(zeroTime), Is.False, "нулевые времена не должны давать бесконечность");
        }

        #endregion

        #region 6. Вырожденные случаи и копирование

        /// <summary>
        /// Разбор траектории без готового валидатора позы невозможен: структура метрик возвращается
        /// невалидной и обнулённой, без исключений (важно для вызовов из UI до инициализации сцены).
        /// </summary>
        [Test]
        public void Analyze_UnavailableValidator_ReturnsDefaultStats()
        {
            PlannedTrajectory plan = Plan(StraightPath(5, 1.0));

            KvTrajStats byNull = KvTrajMath.Analyze(null, plan, null, null, Vector3.zero);
            Assert.That(byNull.valid, Is.False, "null-валидатор → метрики невалидны");
            Assert.That(byNull.samples, Is.EqualTo(0));
            Assert.That(byNull.length, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(byNull.time, Is.EqualTo(0f).Within(1e-6f));

            KvTrajStats byNotReady = KvTrajMath.Analyze(new PoseValidator(), plan, null, new KvEnergyModel(), Vector3.zero);
            Assert.That(byNotReady.valid, Is.False, "валидатор без Init → метрики невалидны");
            Assert.That(byNotReady.samples, Is.EqualTo(0));
            Assert.That(float.IsNaN(byNotReady.energy), Is.False);

            KvTrajStats byNullPlan = KvTrajMath.Analyze(null, null, null, null, Vector3.zero);
            Assert.That(byNullPlan.valid, Is.False);
            Assert.That(float.IsNaN(byNullPlan.length), Is.False);
            Assert.That(float.IsNaN(byNullPlan.curvature), Is.False);
        }

        /// <summary>
        /// Пересчёт времени и проверка рывка без готового валидатора позы: Retime возвращает null,
        /// VerifyJerk сообщает «проверка не выполнялась» (true) с пустым отчётом.
        /// </summary>
        [Test]
        public void RetimeAndVerifyJerk_UnavailableValidator_ReturnSafeDefaults()
        {
            PlannedTrajectory plan = Plan(StraightPath(5, 1.0));

            Assert.That(KvTrajMath.Retime(null, plan, new KvMotionLimits()), Is.Null, "null-валидатор");
            Assert.That(KvTrajMath.Retime(new PoseValidator(), plan, new KvMotionLimits()), Is.Null,
                "без геометрии робота пересчёт времени невозможен");
            Assert.That(KvTrajMath.Retime(new PoseValidator(), null, new KvMotionLimits()), Is.Null, "null-план");

            string report;
            Assert.That(KvTrajMath.VerifyJerk(null, plan, new KvMotionLimits(), "тест", out report), Is.True);
            Assert.That(report, Is.EqualTo(""), "без валидатора отчёт пуст");

            Assert.That(KvTrajMath.VerifyJerk(new PoseValidator(), plan, new KvMotionLimits(), "тест", out report), Is.True);
            Assert.That(report, Is.EqualTo(""));
        }

        /// <summary>
        /// Энергия без готового валидатора позы не считается: 0 Дж и 0 Вт пиковой мощности,
        /// без NaN (нулевые значения обязан обрабатывать вызывающий код).
        /// </summary>
        [Test]
        public void Energy_UnavailableValidator_ReturnsZeroEnergyAndPeak()
        {
            PlannedTrajectory plan = Plan(StraightPath(5, 1.0));
            KvEnergyModel model = new KvEnergyModel();
            float peak;

            Assert.That(KvTrajMath.Energy(null, plan, model, Vector3.zero, out peak), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(peak, Is.EqualTo(0f).Within(1e-6f));

            Assert.That(KvTrajMath.Energy(new PoseValidator(), plan, model, Vector3.zero, out peak),
                Is.EqualTo(0f).Within(1e-6f), "валидатор без Init");
            Assert.That(peak, Is.EqualTo(0f).Within(1e-6f));

            Assert.That(KvTrajMath.Energy(null, null, null, Vector3.zero, out peak), Is.EqualTo(0f).Within(1e-6f));
            Assert.That(float.IsNaN(peak), Is.False);
        }

        /// <summary>
        /// Копирование: Copy / CopyPath / Clone обязаны быть глубокими (правка копии не задевает
        /// оригинал), корректно обрабатывать null и сохранять метрики плана вместе с меткой.
        /// </summary>
        [Test]
        public void CopyCopyPathAndClone_HandleNullAndCopyDeeply()
        {
            Assert.That(KvTrajMath.Copy(null), Is.Null, "Copy(null)");
            double[] q = { 1.0, 2.0, 3.0 };
            double[] qCopy = KvTrajMath.Copy(q);
            Assert.That(qCopy, Is.Not.Null);
            Assert.That(ReferenceEquals(q, qCopy), Is.False);
            qCopy[0] = 42.0;
            Assert.That(q[0], Is.EqualTo(1.0), "Copy — глубокая копия");

            Assert.That(KvTrajMath.CopyPath(null), Is.Null, "CopyPath(null)");
            double[][] path = { new[] { 1.0 }, new[] { 2.0 } };
            double[][] pathCopy = KvTrajMath.CopyPath(path);
            Assert.That(ReferenceEquals(path, pathCopy), Is.False);
            Assert.That(ReferenceEquals(path[0], pathCopy[0]), Is.False);
            pathCopy[0][0] = 42.0;
            Assert.That(path[0][0], Is.EqualTo(1.0), "CopyPath — глубокая копия");

            Assert.That(KvTrajMath.Clone(null), Is.Null, "Clone(null)");
            PlannedTrajectory source = new PlannedTrajectory
            {
                Path = new[] { new[] { 0.0 }, new[] { 1.0 } },
                Times = new[] { 0f, 1f },
                Label = "исходная",
                Time = 1.0,
                Length = 2.0,
                MinClearance = 0.03f,
                LimitMargin = 7f,
                SigmaMin = 0.01,
                Score = 3.5,
                BranchTag = "S-",
                Curvature = 0.25,
                CurvatureTotal = 1.5,
                CurvatureMax = 0.5,
                CurvatureSamples = 2
            };
            PlannedTrajectory clone = KvTrajMath.Clone(source, "копия");
            Assert.That(clone, Is.Not.Null);
            Assert.That(clone.Label, Is.EqualTo("копия"), "метка применяется");
            Assert.That(ReferenceEquals(clone.Path, source.Path), Is.False);
            Assert.That(ReferenceEquals(clone.Path[0], source.Path[0]), Is.False);
            Assert.That(ReferenceEquals(clone.Times, source.Times), Is.False);

            clone.Path[0][0] = 99.0;
            clone.Times[1] = 99f;
            Assert.That(source.Path[0][0], Is.EqualTo(0.0), "правка копии не меняет план-источник");
            Assert.That(source.Times[1], Is.EqualTo(1f));

            Assert.That(clone.Time, Is.EqualTo(source.Time).Within(1e-9), "время перенесено");
            Assert.That(clone.Length, Is.EqualTo(source.Length).Within(1e-9), "длина перенесена");
            Assert.That(clone.Curvature, Is.EqualTo(source.Curvature).Within(1e-9));
            Assert.That(clone.CurvatureMax, Is.EqualTo(source.CurvatureMax).Within(1e-9));
            Assert.That(clone.BranchTag, Is.EqualTo("S-"));
            Assert.That(clone.CurvatureSamples, Is.EqualTo(2));
            Assert.That(KvTrajMath.Clone(source).Label, Is.EqualTo("исходная"), "без метки сохраняется исходная");
        }

        #endregion

        #region 7. Статистика, модель энергии, лимиты

        /// <summary>
        /// Статистика по умолчанию: структура невалидна, все числовые поля нулевые,
        /// производная величина «энергия на метр» не даёт деления на ноль.
        /// </summary>
        [Test]
        public void TrajStats_DefaultInstance_IsInvalidAndZeroed()
        {
            KvTrajStats stats = new KvTrajStats();

            Assert.That(stats.valid, Is.False, "метрика по умолчанию невалидна");
            Assert.That(stats.samples, Is.EqualTo(0));
            Assert.That(stats.time, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.length, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.maxVel, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.maxAcc, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.maxJerk, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.curvature, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.curvatureAvg, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.clearance, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.limitMargin, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.energy, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.peakPower, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(stats.EnergyPerMeter, Is.EqualTo(0f).Within(1e-6f));
            Assert.That(float.IsNaN(stats.EnergyPerMeter), Is.False);
        }

        /// <summary>
        /// Удельная энергия = энергия / длина, но при нулевой и «микроскопической» длине возвращается 0:
        /// деления на ноль и бесконечностей быть не должно.
        /// </summary>
        [Test]
        public void TrajStats_EnergyPerMeter_ComputesRatioAndGuardsZeroLength()
        {
            KvTrajStats stats = new KvTrajStats { length = 4f, energy = 8f };
            Assert.That(stats.EnergyPerMeter, Is.EqualTo(2f).Within(1e-4f), "8 Дж / 4 м = 2 Дж/м");

            KvTrajStats zero = new KvTrajStats { length = 0f, energy = 5f };
            Assert.That(zero.EnergyPerMeter, Is.EqualTo(0f).Within(1e-6f), "нулевая длина → 0");
            Assert.That(float.IsNaN(zero.EnergyPerMeter), Is.False);
            Assert.That(float.IsInfinity(zero.EnergyPerMeter), Is.False);

            KvTrajStats tiny = new KvTrajStats { length = 1e-5f, energy = 5f };
            Assert.That(tiny.EnergyPerMeter, Is.EqualTo(0f).Within(1e-6f), "длина ниже порога 1e-4 м → 0");

            KvTrajStats negative = new KvTrajStats { length = -1f, energy = 5f };
            Assert.That(negative.EnergyPerMeter, Is.EqualTo(0f).Within(1e-6f), "отрицательная длина → 0");
            Assert.That(float.IsNaN(negative.EnergyPerMeter), Is.False);
        }

        /// <summary>
        /// Строка метрик содержит все единицы измерения ТЗ (время, длина, скорость, ускорение, рывок,
        /// кривизна, энергия) и не содержит NaN — она показывается оператору в интерфейсе.
        /// </summary>
        [Test]
        public void TrajStats_Line_ContainsAllMetricUnitsAndNoNaN()
        {
            KvTrajStats stats = new KvTrajStats
            {
                samples = 101,
                time = 1.5f,
                length = 2.5f,
                maxVel = 10f,
                maxAcc = 20f,
                maxJerk = 30f,
                curvature = 0.5f,
                curvatureAvg = 0.25f,
                clearance = 0.03f,
                limitMargin = 12f,
                energy = 12.5f,
                peakPower = 300f,
                valid = true
            };

            string line = stats.Line();
            Assert.That(line, Is.Not.Null);
            Assert.That(line.Length, Is.GreaterThan(0));
            Assert.That(line, Does.Contain("с"), "время");
            Assert.That(line, Does.Contain("м"), "длина");
            Assert.That(line, Does.Contain("°/с"), "скорость");
            Assert.That(line, Does.Contain("°/с²"), "ускорение");
            Assert.That(line, Does.Contain("°/с³"), "рывок");
            Assert.That(line, Does.Contain("1/м"), "кривизна");
            Assert.That(line, Does.Contain("Дж"), "энергия");
            Assert.That(line, Does.Not.Contain("NaN"), "в подписи не должно быть NaN");
        }

        /// <summary>
        /// Модель энергии: Distal складывает массу звена с долей груза, отрицательный груз обрезается
        /// до нуля, а индексы вне массива и null-массивы дают запасные значения без NaN.
        /// </summary>
        [Test]
        public void EnergyModel_Distal_AddsPayloadShareClampsNegativeAndFallsBack()
        {
            KvEnergyModel model = new KvEnergyModel();
            Assert.That(model.Distal(0), Is.EqualTo(7f).Within(1e-4f), "7 кг + 1 кг × доля 0");
            Assert.That(model.Distal(1), Is.EqualTo(5.85f).Within(1e-4f), "5 кг + 1 кг × 0.85");
            Assert.That(model.Distal(5), Is.EqualTo(0.8f).Within(1e-4f), "0.4 кг + 1 кг × 0.4");

            Assert.That(model.Distal(-1), Is.EqualTo(1f).Within(1e-4f), "индекс вне массива → запасная масса");
            Assert.That(model.Distal(99), Is.EqualTo(1f).Within(1e-4f));

            KvEnergyModel negative = new KvEnergyModel { payloadKg = -5f };
            Assert.That(negative.Distal(2), Is.EqualTo(3f).Within(1e-4f), "отрицательный груз обрезается до нуля");
            Assert.That(float.IsNaN(negative.Distal(2)), Is.False);

            KvEnergyModel zero = new KvEnergyModel { payloadKg = 0f };
            Assert.That(zero.Distal(2), Is.EqualTo(3f).Within(1e-4f), "нулевой груз не добавляет массы");

            KvEnergyModel noArrays = new KvEnergyModel { distalMass = null, payloadShare = null, payloadKg = 10f };
            Assert.That(noArrays.Distal(0), Is.EqualTo(1f).Within(1e-4f), "null-массивы → запасное значение");
            Assert.That(noArrays.Distal(3), Is.EqualTo(1f).Within(1e-4f));
            Assert.That(float.IsNaN(noArrays.Distal(3)), Is.False);
        }

        /// <summary>
        /// Клон модели энергии независим: правки клона (включая элементы массивов) не меняют оригинал,
        /// иначе эко-профиль случайно портил бы базовую модель.
        /// </summary>
        [Test]
        public void EnergyModel_Clone_IsIndependent()
        {
            KvEnergyModel original = new KvEnergyModel();
            KvEnergyModel clone = original.Clone();

            Assert.That(clone, Is.Not.Null);
            Assert.That(ReferenceEquals(original, clone), Is.False);
            Assert.That(clone.payloadKg, Is.EqualTo(original.payloadKg).Within(1e-6f));
            Assert.That(clone.distalMass.Length, Is.EqualTo(original.distalMass.Length));
            Assert.That(clone.inertia.Length, Is.EqualTo(original.inertia.Length));
            Assert.That(clone.friction.Length, Is.EqualTo(original.friction.Length));
            Assert.That(clone.payloadShare.Length, Is.EqualTo(original.payloadShare.Length));

            clone.payloadKg = 99f;
            clone.distalMass[0] = 99f;
            clone.inertia[1] = 99f;
            clone.friction[2] = 99f;
            clone.payloadShare[3] = 99f;

            Assert.That(original.payloadKg, Is.EqualTo(1f).Within(1e-6f), "масса груза оригинала не изменилась");
            Assert.That(original.distalMass[0], Is.EqualTo(7f).Within(1e-6f));
            Assert.That(original.inertia[1], Is.EqualTo(1.05f).Within(1e-6f));
            Assert.That(original.friction[2], Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(original.payloadShare[3], Is.EqualTo(0.25f).Within(1e-6f));
        }

        /// <summary>
        /// Лимиты движения: значения по умолчанию соответствуют модели учебного робота (90 °/с, 180 °/с²,
        /// 1200 °/с³; 0.35 м/с, 0.8 м/с², 6 м/с³), а Clone даёт независимую копию.
        /// </summary>
        [Test]
        public void MotionLimits_DefaultsAndClone_AreAsSpecified()
        {
            KvMotionLimits limits = new KvMotionLimits();
            Assert.That(limits.maxVelDeg, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(limits.maxAccDeg, Is.EqualTo(180f).Within(1e-4f));
            Assert.That(limits.maxJerkDeg, Is.EqualTo(1200f).Within(1e-4f));
            Assert.That(limits.maxVelMps, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(limits.maxAccMps2, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(limits.maxJerkMps3, Is.EqualTo(6f).Within(1e-4f));

            KvMotionLimits clone = limits.Clone();
            Assert.That(clone, Is.Not.Null);
            Assert.That(ReferenceEquals(limits, clone), Is.False);
            Assert.That(clone.maxVelDeg, Is.EqualTo(90f).Within(1e-4f), "значения перенесены");

            clone.maxVelDeg = 10f;
            clone.maxAccDeg = 20f;
            clone.maxJerkDeg = 30f;
            clone.maxVelMps = 0.1f;
            clone.maxAccMps2 = 0.2f;
            clone.maxJerkMps3 = 0.3f;

            Assert.That(limits.maxVelDeg, Is.EqualTo(90f).Within(1e-4f), "оригинал не изменился");
            Assert.That(limits.maxAccDeg, Is.EqualTo(180f).Within(1e-4f));
            Assert.That(limits.maxJerkDeg, Is.EqualTo(1200f).Within(1e-4f));
            Assert.That(limits.maxVelMps, Is.EqualTo(0.35f).Within(1e-4f));
            Assert.That(limits.maxAccMps2, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(limits.maxJerkMps3, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(clone.maxVelDeg, Is.EqualTo(10f).Within(1e-4f), "клон получил новые значения");
        }

        #endregion

        #region 8. Экстремальные значения

        /// <summary>
        /// Очень длинная прямая полилиния (10 000 точек): кривизна обязана остаться нулевой,
        /// без накопления погрешности и без NaN.
        /// </summary>
        [Test]
        public void Extreme_TenThousandPointStraightPolyline_CurvatureIsZero()
        {
            Vector3[] poly = new Vector3[10000];
            for (int i = 0; i < poly.Length; i++) poly[i] = new Vector3(i * 0.001f, 0f, 0f);

            float max, average;
            KvTrajMath.Curvature(poly, out max, out average);

            Assert.That(max, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(average, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(float.IsNaN(max) || float.IsInfinity(max), Is.False);
            Assert.That(float.IsNaN(average) || float.IsInfinity(average), Is.False);
        }

        /// <summary>
        /// Гигантские координаты (1e6 м): из-за точности float кривизна прямой не ровно нулевая,
        /// но обязана быть пренебрежимо малой, а сглаживание таких путей — конечным и с фиксированными концами.
        /// </summary>
        [Test]
        public void Extreme_HugeCoordinates_StayFiniteAndNegligible()
        {
            Vector3[] poly =
            {
                new Vector3(0f, 0f, 0f), new Vector3(1e6f, 0f, 0f), new Vector3(2e6f, 0f, 0f)
            };
            float max, average;
            KvTrajMath.Curvature(poly, out max, out average);
            Assert.That(max, Is.LessThan(1e-4f), "кривизна прямой длиной 2e6 м пренебрежимо мала");
            Assert.That(float.IsNaN(max) || float.IsInfinity(max), Is.False);
            Assert.That(float.IsNaN(average) || float.IsInfinity(average), Is.False);

            double[][] path = new double[5][];
            for (int i = 0; i < path.Length; i++) path[i] = new[] { i * 1e6, -i * 1e6 };

            foreach (KvSmoothMethod method in AllMethods())
            {
                double[][] smooth = KvTrajMath.Smooth(path, method, 1f);
                AssertPathFinite(smooth);
                Assert.That(smooth[0][0], Is.EqualTo(path[0][0]), method + ": первая точка фиксирована");
                Assert.That(smooth[path.Length - 1][1], Is.EqualTo(path[path.Length - 1][1]),
                    method + ": последняя точка фиксирована");
            }
        }

        /// <summary>
        /// Очень длинный путь (2001 сэмпл) с микронными шагами 1e-6 м: сглаживание всеми методами
        /// остаётся численным, сохраняет число сэмплов и не смещает концы.
        /// </summary>
        [Test]
        public void Extreme_LongPathWithMicroSteps_SmoothingKeepsEndsAndStaysFinite()
        {
            const int n = 2001;
            double[][] path = new double[n][];
            for (int i = 0; i < n; i++) path[i] = new[] { i * 1e-6, Math.Sin(i * 0.01) };

            foreach (KvSmoothMethod method in AllMethods())
            {
                double[][] smooth = KvTrajMath.Smooth(path, method, 1f);
                Assert.That(smooth.Length, Is.EqualTo(n), method + ": число сэмплов сохраняется");
                AssertPathFinite(smooth);
                Assert.That(smooth[0][0], Is.EqualTo(path[0][0]), method + ": первая точка фиксирована");
                Assert.That(smooth[n - 1][1], Is.EqualTo(path[n - 1][1]), method + ": последняя точка фиксирована");
            }
        }

        /// <summary>
        /// Экстремальные лимиты S-профиля: полностью нулевые пределы (защита от деления на ноль)
        /// и гигантские пределы (1e6 ед/с, 1e12 ед/с³) обязаны давать конечный профиль без NaN/∞.
        /// </summary>
        [Test]
        public void Extreme_SProfileBuild_ZeroAndHugeLimits_StayFinite()
        {
            const int n = 5;
            PlannedTrajectory plan = Plan(StraightPath(n, 1.0));
            double[] s = CumulativeS(n, 1.0);
            double[] vel = new double[n];
            float[] times = new float[n];
            double jerkPeak, accelPeak;
            int clamped;

            double[] zeros = new double[n];
            BuildProfile(plan, s, zeros, zeros, zeros, vel, times, out jerkPeak, out accelPeak, out clamped);
            AssertFinite(times);
            AssertFinite(vel);
            Assert.That(times[0], Is.EqualTo(0f).Within(1e-6f));
            for (int i = 1; i < n; i++)
                Assert.That(times[i], Is.GreaterThanOrEqualTo(times[i - 1]), "времена не должны убывать (сэмпл " + i + ")");
            Assert.That(double.IsNaN(jerkPeak) || double.IsInfinity(jerkPeak), Is.False);
            Assert.That(double.IsNaN(accelPeak) || double.IsInfinity(accelPeak), Is.False);

            double[] vmax = { 0.0, 1e6, 1e6, 1e6, 0.0 };
            double[] amax = { 1e6, 1e6, 1e6, 1e6, 1e6 };
            double[] jerkCap = { 1e12, 1e12, 1e12, 1e12, 1e12 };
            BuildProfile(plan, s, vmax, amax, jerkCap, vel, times, out jerkPeak, out accelPeak, out clamped);
            AssertFinite(times);
            AssertFinite(vel);
            Assert.That(times[n - 1], Is.GreaterThanOrEqualTo(0f));
            Assert.That(vel[0], Is.EqualTo(0.0).Within(1e-9), "старт из покоя сохраняется");
            Assert.That(vel[n - 1], Is.EqualTo(0.0).Within(1e-9), "финиш в покое сохраняется");
        }

        #endregion
    }
}

// ============================================================================================
// НЕ ПОКРЫТО (требует рефакторинга):
//
// 1. KvTrajMath.PathLength(PoseValidator, double[][]) — фактическая длина пути TCP. Внутри нужен
//    валидатор позы с Ready == true (прямая кинематика и TcpAt), а Ready выставляется только
//    в PoseValidator.Init(RobotController), где требуется MonoBehaviour из сцены. Покрыт лишь
//    защитный контракт (null-валидатор / валидатор без Init → 0). Чтобы закрыть расчёт длины
//    полностью, нужен Init, принимающий чистую геометрию (длины звеньев) без RobotController.
//
// 2. KvTrajMath.Retime(PoseValidator, PlannedTrajectory, KvMotionLimits, float, float, string, bool) —
//    время-оптимальная параметризация целиком: на входе требует Ready-валидатор (нормировка суставов
//    через IsPrismatic и лимиты по геометрии). Покрыт только возврат null без валидатора; сам расчёт
//    недоступен, хотя публичный SProfileBuild (его ядро) протестирован напрямую.
//
// 3. KvTrajMath.Analyze(PoseValidator, PlannedTrajectory, CollisionWorld, KvEnergyModel, Vector3) —
//    полный разбор траектории: нужен Ready-валидатор, а для зазора ещё и CollisionWorld (объекты сцены).
//    Покрыт только невалидный результат по умолчанию.
//
// 4. KvTrajMath.VerifyJerk(PoseValidator, PlannedTrajectory, KvMotionLimits, string, out string) —
//    фактическая проверка рывка по сэмплам: без Ready-валидатора метод сразу выходит. Покрыт только
//    ранний выход (true, пустой отчёт).
//
// 5. KvTrajMath.Energy(...) (обе перегрузки: с Vector3 basePosition и с float[] levers) — расчёт
//    энергии требует Ready-валидатора (TcpAt для плеч и IsPrismatic для типа сустава). Покрыт только
//    нулевой результат (0 Дж, 0 Вт) на null-валидаторе и валидаторе без Init.
//
// 6. KvTrajMath.LeverArms(PoseValidator, double[][], Vector3) и KvTrajMath.TcpPolyline(PoseValidator,
//    double[][]) — фактический расчёт плеч и полилинии TCP невозможен без FK из сцены; покрыто только
//    возвращение пустого массива.
//
// 7. KvTrajMath.LastProfileNote / LastProfileJerk / LastProfileClamped / LastProfileApplicable —
//    статические свойства заполняются ТОЛЬКО внутри Retime. Проверять их значения в EditMode-тестах
//    нельзя: без Retime они всегда пусты, а любой тест, дёргающий Retime, сделал бы результат
//    зависимым от порядка выполнения (нарушение требования детерминизма).
//
// 8. Приватная реализация: Derivative, MaxAbs, BSplinePass, BezierPass, GaussianPass, Norm, LimitOf,
//    JerkOf, JerkAlongPath, StepTime, TimesFromProfile, DescribeProfile, GravityShare. Прямых тестов нет
//    (недоступны извне), но ключевые из них проверяются косвенно: MeasureJerk — те же конечные разности,
//    Smooth — все три прохода сглаживания, SProfileBuild — StepTime/JerkAlongPath/TimesFromProfile.
//
// 9. Полноценные сценарии «длина / кривизна / время» на РЕАЛЬНОЙ геометрии робота (SCARA 3 DOF и
//    6-осевой) не покрыты: они требуют сцены, RobotController и CollisionWorld, то есть являются
//    PlayMode/интеграционными тестами, а не EditMode-юнит-тестами чистой математики.
// ============================================================================================

