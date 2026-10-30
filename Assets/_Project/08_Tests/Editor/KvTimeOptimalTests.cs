using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KazistovVvFeatures;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты S-профиля (время-оптимальная траектория, ЭТАП 5 ТЗ).
    ///
    /// ЧТО ЗДЕСЬ ТЕСТИРУЕТСЯ И ПОЧЕМУ ИМЕННО ТАК.
    ///
    /// Класс <see cref="KvTimeOptimal"/> — сервис-обёртка: сам S-профиль он не считает,
    /// а вызывает <c>KvTrajMath.Retime(...)</c> (см. `Features/KvTimeOptimal.cs`,
    /// строка с комментарием «АНАЛИТИЧЕСКИЙ S-ПРОФИЛЬ (ФИКС 1)»). При этом:
    ///   • <c>KvTrajMath.Retime(v, ...)</c> начинается с проверки
    ///     <c>v == null || !v.Ready</c> и возвращает null;
    ///   • <c>PoseValidator.Ready</c> — свойство с ЧАСТНЫМ сеттером, оно становится true
    ///     только внутри <c>PoseValidator.Init(RobotController)</c>, для чего нужны
    ///     6 трансформов суставов из СЦЕНЫ (<c>SixAxisController</c>/<c>SCARAController</c> —
    ///     MonoBehaviour, а трансформам нужен GameObject).
    ///
    /// Поэтому полноценный сквозной прогон «Compute → Retime → S-профиль» в EditMode-тесте
    /// без запрещённых правилами средств (GameObject / MonoBehaviour / сцена) НЕВОЗМОЖЕН.
    /// Тестируется то, что реально вызываемо и является СЕРДЦЕМ S-профиля:
    /// <c>KvTrajMath.SProfileBuild(...)</c> — публичный статический ЧИСТЫЙ построитель
    /// аналитического S-профиля (разгон → крейсер → торможение с ограничением рывка),
    /// плюс <c>KvTrajMath.MeasureJerk(...)</c> — тот самый «честный замер рывка по сэмплам»,
    /// которым `Retime`/`VerifyJerk` проверяют результат.
    ///
    /// Вход для <c>SProfileBuild</c> готовится фикстурой ровно так, как это делает
    /// <c>Retime</c> перед вызовом (нормировка суставов 1/90, длина пути s, пределы
    /// скорости/ускорения/рывка вдоль пути, эталонный предел рывка) — см. <see cref="MakeProfile"/>.
    /// Так проверяется ФАКТИЧЕСКОЕ поведение профиля: лимиты jerk / acceleration / velocity,
    /// монотонность времени, покой на концах, вырожденные и экстремальные входы.
    ///
    /// КРОМЕ ЭТОГО проверяется и сам публичный вход время-оптимальной параметризации
    /// <c>KvTrajMath.Retime(...)</c> — но с валидатором позы, переведённым в состояние Ready
    /// ОТРАЖЕНИЕМ (см. <see cref="ReadyValidator"/>): приватный сеттер `PoseValidator.Ready`
    /// иначе не выставить, а `Init(RobotController)` требует сцену. Это заглушка зависимости,
    /// а не правка кода проекта: отражение меняет флаг только у локального объекта теста.
    /// Что даёт такое состояние и чего не даёт — написано в комментарии <see cref="ReadyValidator"/>.
    ///
    /// ПРАВИЛА, СОБЛЮДЁННЫЕ В ТЕСТАХ:
    ///   • ни одного GameObject / MonoBehaviour / Instantiate / корутины / UnityEditor.*;
    ///   • никакого создания и правки файлов проекта, никакого PlayerPrefs.Write:
    ///     лимиты всегда выставляются через <c>SetLimits(..., save: false)</c> (PlayerPrefs
    ///     только читается внутри `Load()`), а ветки, уходящие в локализацию
    ///     (<c>KvLoc.Ensure()</c> читает JSON из StreamingAssets), сознательно не вызываются —
    ///     поэтому все вызовы <c>Compute</c> идут с <c>quiet: true</c>, а `ApplySelected()`
    ///     не вызывается вовсе (см. блок «НЕ ПОКРЫТО» внизу файла);
    ///   • нет зависимости от реального времени и нет случайности: все фикстуры —
    ///     арифметические, все ожидания — детерминированные.
    ///
    /// Ожидаемые числовые величины получены прогоном этого же алгоритма на автономном
    /// стенде вне Unity; допуски выбраны с запасом на конечные разности.
    /// </summary>
    [TestFixture]
    public class KvTimeOptimalTests
    {
        // ==================================================================== константы фикстур

        /// <summary>Нормировка вращательного сустава (копия `KvTrajMath.RevScale`, он private).</summary>
        private const double RevScale = 1.0 / 90.0;

        /// <summary>Допуск на конечные разности при замере скорости/ускорения (2 %).</summary>
        private const double MotionTol = 1.02;

        /// <summary>Допуск на замер рывка (5 %; штатный допуск `VerifyJerk` — 3 % + запас).</summary>
        private const double JerkTol = 1.05;

        // ==================================================================== вход профиля

        /// <summary>
        /// Вход и выход одного вызова <c>KvTrajMath.SProfileBuild</c>:
        /// массивы подготовлены так же, как их готовит <c>KvTrajMath.Retime</c>.
        /// </summary>
        private sealed class Profile
        {
            public int N;
            public int Dof;
            public PlannedTrajectory Plan;
            public double[] Scale;        // нормировка суставов (1/90 для вращательных)
            public double[] S;            // длина пути вдоль сэмплов (единицы пути)
            public double[] Vmax;         // предел скорости вдоль пути (единицы/с), = velCapRaw
            public double[] Amax;         // касательный предел ускорения (единицы/с²)
            public double[] JerkCap;      // предел рывка вдоль пути (единицы/с³)
            public double JerkRef;        // эталонный предел рывка (единицы/с³)
            public double[] Vel;          // ВЫХОД: скорости вдоль пути
            public float[] Times;         // ВЫХОД: времена сэмплов
            public double JerkPeak;       // ВЫХОД: расчётный рывок профиля
            public double AccelPeak;      // ВЫХОД: максимум внутреннего профиля ускорений
            public int Clamped;           // ВЫХОД: сколько сэмплов срезано по пределу скорости
            public double[] VelLimitDeg;  // физические лимиты суставов, °/с
            public double[] AccLimitDeg;  // °/с²
            public double[] JerkLimitDeg; // °/с³
        }

        /// <summary>
        /// Собрать вход S-профиля для прямого пути в пространстве суставов:
        /// сустав j проходит <paramref name="deltaDeg"/>[j] градусов равномерно по сэмплам.
        ///
        /// Воспроизводит подготовку, которую делает `KvTrajMath.Retime`:
        ///   s — накопленная длина нормированного пути; q' = dq/ds·scale (для такого пути
        ///   постоянна); vmax = velLimit/|q'|, amax = accLimit·0.4/|q'| (множитель 0.4 —
        ///   запас на кривизну из `Retime`), jerkCap = jerkLimit/|q'|, jerkRef = min jerkLimit.
        /// </summary>
        private static Profile MakeProfile(int n, double[] deltaDeg, double[] velDeg,
            double[] accDeg, double[] jerkDeg)
        {
            Profile p = new Profile();
            p.N = n;
            p.Dof = deltaDeg.Length;
            p.VelLimitDeg = velDeg;
            p.AccLimitDeg = accDeg;
            p.JerkLimitDeg = jerkDeg;

            p.Plan = new PlannedTrajectory();
            p.Plan.Path = new double[n][];
            p.Plan.Label = "S-профиль (тест)";
            for (int i = 0; i < n; i++)
            {
                double k = n > 1 ? i / (double)(n - 1) : 0.0;
                p.Plan.Path[i] = new double[p.Dof];
                for (int j = 0; j < p.Dof; j++) p.Plan.Path[i][j] = deltaDeg[j] * k;
            }

            p.Scale = new double[p.Dof];
            for (int j = 0; j < p.Dof; j++) p.Scale[j] = RevScale;

            p.S = new double[n];
            for (int i = 1; i < n; i++)
            {
                double d = 0.0;
                for (int j = 0; j < p.Dof; j++)
                {
                    double delta = (p.Plan.Path[i][j] - p.Plan.Path[i - 1][j]) * p.Scale[j];
                    d += delta * delta;
                }
                p.S[i] = p.S[i - 1] + Math.Sqrt(d);
            }

            double total = p.S[n - 1];
            double vLim = double.MaxValue, aLim = double.MaxValue;
            double jLim = double.MaxValue, jRef = double.MaxValue;
            for (int j = 0; j < p.Dof; j++)
            {
                double q1 = total > 1e-12 ? Math.Abs(deltaDeg[j]) * p.Scale[j] / total : 1.0;
                if (q1 < 1e-9) q1 = 1.0;                 // сустав вдоль пути не двигается
                vLim = Math.Min(vLim, velDeg[j] * RevScale / q1);
                aLim = Math.Min(aLim, accDeg[j] * RevScale * 0.4 / q1);
                jLim = Math.Min(jLim, jerkDeg[j] * RevScale / q1);
                jRef = Math.Min(jRef, jerkDeg[j] * RevScale);
            }

            p.Vmax = new double[n];
            p.Amax = new double[n];
            p.JerkCap = new double[n];
            for (int i = 0; i < n; i++)
            {
                p.Vmax[i] = vLim;
                p.Amax[i] = aLim;
                p.JerkCap[i] = jLim;
            }
            p.JerkRef = jRef;
            p.Vel = new double[n];
            p.Times = new float[n];
            return p;
        }

        /// <summary>Единственный вызов тестируемого построителя S-профиля.</summary>
        private static void Build(Profile p)
        {
            KvTrajMath.SProfileBuild(p.Plan, p.Scale, p.S, p.Vmax, p.Amax, p.JerkCap, p.JerkRef,
                p.Vel, p.Times, out p.JerkPeak, out p.AccelPeak, out p.Clamped);
        }

        // ==================================================================== измерители (как в метрике)

        /// <summary>
        /// Конечная разность по времени — ТА ЖЕ схема, что в приватном `KvTrajMath.Derivative`
        /// (центральные разности внутри, односторонние на краях): замеры в тестах сравнимы
        /// с метрикой `KvTrajMath.Analyze`, которой пользуется `KvTimeOptimal.Compute`.
        /// </summary>
        private static double[][] Derivative(double[][] q, float[] t)
        {
            int n = q.Length;
            int dof = q[0].Length;
            double[][] d = new double[n][];
            for (int i = 0; i < n; i++) d[i] = new double[dof];

            for (int i = 0; i < n; i++)
            {
                int a = i > 0 ? i - 1 : i;
                int b = i < n - 1 ? i + 1 : i;
                double dt = t[b] - t[a];
                if (dt < 1e-6) dt = 1e-6;
                for (int j = 0; j < dof; j++) d[i][j] = (q[b][j] - q[a][j]) / dt;
            }
            return d;
        }

        /// <summary>Скорости суставов по сэмплам профиля, °/с.</summary>
        private static double[][] VelocityOf(Profile p)
        {
            return Derivative(p.Plan.Path, p.Times);
        }

        /// <summary>Ускорения суставов по сэмплам профиля, °/с².</summary>
        private static double[][] AccelerationOf(Profile p)
        {
            return Derivative(VelocityOf(p), p.Times);
        }

        /// <summary>Рывок профиля по сэмплам в °/с³ (MeasureJerk даёт единицы пути, ×90 — градусы).</summary>
        private static double MeasuredJerkDeg(Profile p)
        {
            return MeasuredJerkDeg(p.Plan, p.Times, p.Scale);
        }

        /// <summary>То же для плана, полученного публичным `KvTrajMath.Retime`.</summary>
        private static double MeasuredJerkDeg(PlannedTrajectory plan, float[] times, double[] scale)
        {
            return KvTrajMath.MeasureJerk(plan.Path, times, scale) * 90.0;
        }

        // ==================================================================== проверки-хелперы

        /// <summary>Времена: ноль в начале, строго возрастают, конечны, полное время > 0.</summary>
        private static void AssertTimesWellFormed(Profile p, string what)
        {
            Assert.That(p.Times.Length, Is.EqualTo(p.N), what + ": длина массива времён");
            Assert.That(p.Times[0], Is.EqualTo(0f), what + ": время первого сэмпла должно быть 0");
            for (int i = 1; i < p.N; i++)
            {
                Assert.That(float.IsNaN(p.Times[i]) || float.IsInfinity(p.Times[i]), Is.False,
                    $"{what}: время сэмпла {i} не конечно ({p.Times[i]})");
                Assert.That(p.Times[i], Is.GreaterThan(p.Times[i - 1]),
                    $"{what}: время не возрастает на шаге {i}: {p.Times[i - 1]} -> {p.Times[i]}");
            }
            Assert.That(p.Times[p.N - 1], Is.GreaterThan(0f),
                what + ": полное время профиля должно быть больше нуля");
        }

        /// <summary>
        /// «Нигде не превышает»: проход по ВСЕМ сэмплам и суставам с информативным сообщением
        /// (какой сэмпл, какой сустав, насколько превышен предел).
        /// </summary>
        private static void AssertAllSamplesWithin(double[][] values, double[] limitDeg,
            double factor, string what)
        {
            for (int j = 0; j < limitDeg.Length; j++)
            {
                double limit = limitDeg[j] * factor;
                for (int i = 0; i < values.Length; i++)
                {
                    double v = Math.Abs(values[i][j]);
                    Assert.That(v, Is.LessThanOrEqualTo(limit),
                        $"{what}: сэмпл {i}, сустав {j}: {v:G6} > предел {limit:G6}");
                }
            }
        }

        /// <summary>Максимум |значение| по всем сэмплам и суставам.</summary>
        private static double MaxAbs(double[][] values)
        {
            double max = 0.0;
            for (int i = 0; i < values.Length; i++)
                for (int j = 0; j < values[i].Length; j++) max = Math.Max(max, Math.Abs(values[i][j]));
            return max;
        }

        /// <summary>Набор типовых фикстур: один сустав (короткий/номинальный/длинный) и два сустава.</summary>
        private static List<Profile> NominalProfiles()
        {
            List<Profile> set = new List<Profile>();
            set.Add(MakeProfile(64, new double[] { 90 }, new double[] { 90 }, new double[] { 180 },
                new double[] { 1200 }));
            set.Add(MakeProfile(64, new double[] { 5 }, new double[] { 90 }, new double[] { 180 },
                new double[] { 1200 }));
            set.Add(MakeProfile(129, new double[] { 360 }, new double[] { 90 }, new double[] { 180 },
                new double[] { 1200 }));
            set.Add(MakeProfile(96, new double[] { 60, -40 }, new double[] { 90, 45 },
                new double[] { 180, 120 }, new double[] { 1200, 600 }));
            return set;
        }

        // ==================================================================== 1. KvTimeOptimal: состояние и лимиты

        /// <summary>
        /// Свежий сервис не привязан к потоку: Flow() == null, кэш черновиков пуст,
        /// Last == null, а Invalidate()/ResetCache() безопасны (в том числе на пустом кэше).
        /// </summary>
        [Test]
        public void Constructor_StartsUnboundAndCacheOperationsAreSafe()
        {
            KvTimeOptimal service = new KvTimeOptimal();

            Assert.That(service.Flow(), Is.Null, "без Bind() потока быть не должно");
            Assert.That(service.Last, Is.Null, "черновика ещё нет");
            Assert.That(service.DraftCount, Is.EqualTo(0), "кэш черновиков пуст");

            Assert.DoesNotThrow(delegate { service.Invalidate(); }, "Invalidate() на пустом кэше");
            Assert.DoesNotThrow(delegate { service.ResetCache(); }, "ResetCache() на пустом кэше");
            Assert.That(service.DraftCount, Is.EqualTo(0), "после сброса кэш по-прежнему пуст");
            Assert.That(service.Last, Is.Null, "после сброса черновика нет");
        }

        /// <summary>
        /// ТЗ п.6: нулевые и отрицательные лимиты. `SetLimits` жёстко клампит их в минимум
        /// (1 °/с, 1 °/с², 10 °/с³), поэтому деления на ноль ниже по потоку не возникает:
        /// нулевой предел скорости/ускорения/рывка в расчёт S-профиля попасть не может.
        /// Сохранение в PlayerPrefs выключено (save: false) — тест не меняет настройки оператора.
        /// </summary>
        [Test]
        public void SetLimits_ClampsZeroAndNegativeLimits()
        {
            KvTimeOptimal service = new KvTimeOptimal();

            service.SetLimits(0f, 0f, 0f, false);
            Assert.That(service.MaxVel, Is.EqualTo(1f), "скорость 0 заменяется минимумом 1 °/с");
            Assert.That(service.MaxAcc, Is.EqualTo(1f), "ускорение 0 заменяется минимумом 1 °/с²");
            Assert.That(service.MaxJerk, Is.EqualTo(10f), "рывок 0 заменяется минимумом 10 °/с³");

            service.SetLimits(-90f, -180f, -1200f, false);
            Assert.That(service.MaxVel, Is.EqualTo(1f), "отрицательная скорость клампится в минимум");
            Assert.That(service.MaxAcc, Is.EqualTo(1f), "отрицательное ускорение клампится в минимум");
            Assert.That(service.MaxJerk, Is.EqualTo(10f), "отрицательный рывок клампится в минимум");

            Assert.That(service.Limits.maxVelDeg, Is.GreaterThan(0f), "предел скорости положителен");
            Assert.That(service.Limits.maxAccDeg, Is.GreaterThan(0f), "предел ускорения положителен");
            Assert.That(service.Limits.maxJerkDeg, Is.GreaterThan(0f), "предел рывка положителен");
        }

        /// <summary>
        /// ТЗ п.7: экстремальные значения. Очень большие лимиты клампятся сверху
        /// (720 °/с, 5000 °/с², 100000 °/с³), очень маленькие — к тем же минимумам,
        /// что и нули. Итог — конечные положительные числа в обоих случаях.
        /// </summary>
        [Test]
        public void SetLimits_ClampsExtremeLimits()
        {
            KvTimeOptimal service = new KvTimeOptimal();

            service.SetLimits(1e9f, 1e9f, 1e9f, false);
            Assert.That(service.MaxVel, Is.EqualTo(720f), "потолок скорости — 720 °/с");
            Assert.That(service.MaxAcc, Is.EqualTo(5000f), "потолок ускорения — 5000 °/с²");
            Assert.That(service.MaxJerk, Is.EqualTo(100000f), "потолок рывка — 100000 °/с³");

            service.SetLimits(1e-6f, 1e-6f, 1e-6f, false);
            Assert.That(service.MaxVel, Is.EqualTo(1f), "микроскопический лимит поднимается к минимуму");
            Assert.That(service.MaxAcc, Is.EqualTo(1f), "микроскопическое ускорение — к минимуму");
            Assert.That(service.MaxJerk, Is.EqualTo(10f), "микроскопический рывок — к минимуму");

            Assert.That(float.IsNaN(service.MaxVel) || float.IsNaN(service.MaxJerk), Is.False,
                "кламп не должен давать NaN");
        }

        /// <summary>
        /// Контракт лимитов целиком:
        ///   • призматические оси получают те же пределы, переведённые в СИ по масштабу
        ///     «10 см ≈ 90°» (деление на 900) — именно эти поля читает `Retime` для призмы;
        ///   • `Limits` — ЖИВОЙ экземпляр: свойства MaxVel/MaxAcc/MaxJerk читают те же поля,
        ///     что и структура лимитов (единый источник правды для интерфейса и расчёта);
        ///   • ключи PlayerPrefs, под которыми лимиты сохраняются, — часть публичного API.
        /// </summary>
        [Test]
        public void Limits_ConversionLiveInstanceAndPrefsKeyContract()
        {
            KvTimeOptimal service = new KvTimeOptimal();
            service.SetLimits(90f, 180f, 1200f, false);

            KvMotionLimits lim = service.Limits;
            Assert.That(lim, Is.Not.Null, "Limits не должен быть null");
            Assert.That(lim.maxVelDeg, Is.EqualTo(90f).Within(1e-4f), "градусный предел скорости");
            Assert.That(lim.maxAccDeg, Is.EqualTo(180f).Within(1e-4f), "градусный предел ускорения");
            Assert.That(lim.maxJerkDeg, Is.EqualTo(1200f).Within(1e-4f), "градусный предел рывка");

            Assert.That(lim.maxVelMps, Is.EqualTo(lim.maxVelDeg / 900f).Within(1e-6f),
                "призматическая скорость = °/с ÷ 900");
            Assert.That(lim.maxAccMps2, Is.EqualTo(lim.maxAccDeg / 900f).Within(1e-6f),
                "призматическое ускорение = °/с² ÷ 900");
            Assert.That(lim.maxJerkMps3, Is.EqualTo(lim.maxJerkDeg / 900f).Within(1e-6f),
                "призматический рывок = °/с³ ÷ 900");

            Assert.That(service.MaxVel, Is.EqualTo(lim.maxVelDeg).Within(1e-5f), "MaxVel == Limits.maxVelDeg");
            Assert.That(service.MaxAcc, Is.EqualTo(lim.maxAccDeg).Within(1e-5f), "MaxAcc == Limits.maxAccDeg");
            Assert.That(service.MaxJerk, Is.EqualTo(lim.maxJerkDeg).Within(1e-5f), "MaxJerk == Limits.maxJerkDeg");
            Assert.That(service.Limits, Is.SameAs(lim), "Limits отдаёт тот же экземпляр (без копий)");

            Assert.That(KvTimeOptimal.VelPrefsKey, Is.EqualTo("KazistovVv.Post.MaxVel"), "ключ скорости");
            Assert.That(KvTimeOptimal.AccPrefsKey, Is.EqualTo("KazistovVv.Post.MaxAcc"), "ключ ускорения");
            Assert.That(KvTimeOptimal.JerkPrefsKey, Is.EqualTo("KazistovVv.Post.MaxJerk"), "ключ рывка");
        }

        /// <summary>
        /// Лимиты меняются — прежние расчёты недействительны: `SetLimits` вызывает
        /// `Invalidate()`, поэтому кэш черновиков и `Last` сбрасываются.
        /// </summary>
        [Test]
        public void SetLimits_InvalidatesCachedDrafts()
        {
            KvTimeOptimal service = new KvTimeOptimal();
            service.SetLimits(90f, 180f, 1200f, false);
            Assert.That(service.DraftCount, Is.EqualTo(0), "после установки лимитов кэш пуст");

            service.SetLimits(120f, 240f, 1500f, false);
            Assert.That(service.DraftCount, Is.EqualTo(0), "смена лимитов не оставляет старых черновиков");
            Assert.That(service.Last, Is.Null, "Last сброшен вместе с кэшем");
        }

        // ==================================================================== 2. KvTimeOptimal: расчёт без сцены

        /// <summary>
        /// Обе защитные ветки `Compute` без сцены:
        ///   • null-кандидат и кандидат без плана возвращают null и в тихом режиме НИЧЕГО не сообщают;
        ///   • кандидат с планом, но без привязанного потока (значит и без готового валидатора
        ///     робота) тоже не даёт S-профиля, зато ЧЕСТНО сообщает причину в событие Message —
        ///     текст не локализован, поэтому проверяется дословно.
        /// Ни в одном случае черновик не кэшируется.
        ///
        /// Не-тихий режим здесь вызывается ТОЛЬКО для кандидата с планом: ветка «нет плана» в
        /// не-тихом режиме уходит в `KvVariantKit.SceneStatus` → `KvLocExtra.T` → ленивое чтение
        /// словарей из StreamingAssets (файловый ввод-вывод, запрещённый правилами тестов).
        /// </summary>
        [Test]
        public void Compute_GuardBranchesReturnNullAndReportReasonOnlyWhenNotQuiet()
        {
            KvTimeOptimal service = new KvTimeOptimal();
            List<string> messages = new List<string>();
            service.Message += delegate(string text) { messages.Add(text); };

            Assert.That(service.Compute(null, true), Is.Null, "null-кандидат не даёт черновика");

            TrajectoryCandidate planless = new TrajectoryCandidate();
            Assert.That(planless.plan, Is.Null, "у свежего кандидата плана нет");
            Assert.That(service.Compute(planless, true), Is.Null, "кандидат без плана не даёт черновика");
            Assert.That(messages.Count, Is.EqualTo(0), "в тихом режиме сообщений быть не должно");

            PlannedTrajectory plan = MakeProfile(32, new double[] { 45 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 }).Plan;
            TrajectoryCandidate candidate = new TrajectoryCandidate();
            candidate.plan = plan;

            KvTimeOptimal.Draft draft = service.Compute(candidate, false);

            Assert.That(draft, Is.Null, "без готового валидатора время-оптимальная траектория не строится");
            Assert.That(messages.Count, Is.EqualTo(1), "оператор должен получить одно сообщение о причине");
            Assert.That(messages[0], Is.EqualTo("расчёт невозможен: валидатор робота не готов"),
                "текст сообщения фиксирует фактическую причину отказа");
            Assert.That(service.DraftCount, Is.EqualTo(0), "неудачный расчёт не кэшируется");
            Assert.That(service.Last, Is.Null, "Last не заполняется при отказе");
        }

        /// <summary>
        /// Откат к исходному времени без потока: `ResetSelected()` возвращает false и ничего
        /// не делает (вариантов нет). Ветка отчёта «возвращать нечего» здесь не достигается,
        /// потому что кандидат берётся из потока, которого нет (см. «НЕ ПОКРЫТО»).
        /// </summary>
        [Test]
        public void ResetSelected_ReturnsFalse_WithoutFlow()
        {
            KvTimeOptimal service = new KvTimeOptimal();
            service.SetLimits(90f, 180f, 1200f, false);

            Assert.That(service.ResetSelected(), Is.False, "без потока и вариантов откатывать нечего");
            Assert.That(service.DraftCount, Is.EqualTo(0), "состояние кэша не изменилось");
            Assert.That(service.Last, Is.Null, "Last остался пустым");
        }

        /// <summary>
        /// Контракт черновика (`KvTimeOptimal.Draft`) по умолчанию: рывок считается
        /// уложившимся в предел (`jerkOk == true`) — «красный флаг» поднимает только
        /// честный замер по сэмплам (`VerifyJerk`), а не сам факт создания черновика.
        /// </summary>
        [Test]
        public void Draft_DefaultsAreJerkSafe()
        {
            KvTimeOptimal.Draft draft = new KvTimeOptimal.Draft();

            Assert.That(draft.jerkOk, Is.True, "по умолчанию рывок считается уложившимся в предел");
            Assert.That(draft.jerkReport, Is.EqualTo(""), "отчёт о рывке пуст до проверки");
            Assert.That(draft.key, Is.EqualTo(""), "ключ кэша пуст");
            Assert.That(draft.applied, Is.False, "черновик не применён");
            Assert.That(draft.timeGain, Is.EqualTo(0f), "выигрыш по времени по умолчанию 0");
            Assert.That(draft.plan, Is.Null, "плана ещё нет");
        }

        // ==================================================================== 3. Лимиты и метрики как данные

        /// <summary>
        /// `KvMotionLimits` — значения по умолчанию (типовые для учебного робота) положительны:
        /// S-профиль никогда не стартует с нулевыми лимитами, а `Clone()` даёт независимую копию,
        /// которой `Retime`/`SetLimits` пользуются как рабочим набором.
        /// </summary>
        [Test]
        public void MotionLimits_DefaultsAndCloneAreIndependent()
        {
            KvMotionLimits lim = new KvMotionLimits();
            Assert.That(lim.maxVelDeg, Is.EqualTo(90f).Within(1e-4f), "скорость по умолчанию");
            Assert.That(lim.maxAccDeg, Is.EqualTo(180f).Within(1e-4f), "ускорение по умолчанию");
            Assert.That(lim.maxJerkDeg, Is.EqualTo(1200f).Within(1e-4f), "рывок по умолчанию");
            Assert.That(lim.maxVelMps, Is.EqualTo(0.35f).Within(1e-5f), "призматическая скорость");
            Assert.That(lim.maxAccMps2, Is.EqualTo(0.8f).Within(1e-5f), "призматическое ускорение");
            Assert.That(lim.maxJerkMps3, Is.EqualTo(6f).Within(1e-5f), "призматический рывок");

            KvMotionLimits copy = lim.Clone();
            Assert.That(copy, Is.Not.SameAs(lim), "Clone() создаёт новый экземпляр");
            Assert.That(copy.maxVelDeg, Is.EqualTo(lim.maxVelDeg).Within(1e-6f), "копия сохраняет лимиты");

            copy.maxVelDeg = 5f;
            Assert.That(lim.maxVelDeg, Is.EqualTo(90f).Within(1e-4f),
                "правка копии не меняет исходный набор лимитов");
        }

        /// <summary>
        /// ТЗ п.6 (деление на ноль) на стороне метрик: `KvTrajStats.EnergyPerMeter`
        /// защищён проверкой длины пути, поэтому для нулевого пути возвращает 0, а не NaN.
        /// Плюс `Line()` не падает на пустой статистике.
        /// </summary>
        [Test]
        public void TrajStats_EnergyPerMeter_GuardsZeroLength()
        {
            KvTrajStats empty = new KvTrajStats();
            Assert.That(empty.EnergyPerMeter, Is.EqualTo(0f), "нулевая длина — нулевая удельная энергия");
            Assert.That(float.IsNaN(empty.EnergyPerMeter), Is.False, "деления на ноль не происходит");

            empty.length = 2f;
            empty.energy = 10f;
            Assert.That(empty.EnergyPerMeter, Is.EqualTo(5f).Within(1e-5f), "энергия / длина пути");

            Assert.That(empty.Line(), Is.Not.Null.And.Not.Empty, "строка метрики формируется без исключений");
        }

        // ==================================================================== 4. Точки входа S-профиля, которым нужен робот

        /// <summary>
        /// Валидатор позы в состоянии Ready — «пропуск» в <c>KvTrajMath.Retime</c>.
        ///
        /// ПОЧЕМУ ОТРАЖЕНИЕ: `PoseValidator.Ready` — свойство с ПРИВАТНЫМ сеттером, оно
        /// выставляется только внутри `PoseValidator.Init(RobotController)`, а `RobotController`
        /// (как и SixAxisController/SCARAController) — MonoBehaviour, которому нужны GameObject
        /// и Transform'ы сцены. Правила тестов запрещают GameObject/MonoBehaviour, поэтому
        /// сцену взять негде. Отражение меняет флаг ТОЛЬКО у локального объекта теста: код
        /// проекта не правится, файлы не создаются, PlayerPrefs не затрагивается.
        ///
        /// ЧТО ДАЁТ ЭТО СОСТОЯНИЕ: `Retime` читает из валидатора ровно `Ready`, `Dof` (косвенно,
        /// через `IsPrismatic`) и `TcpAt` — вся нормировка суставов и лимиты считаются
        /// аналитически. Для валидатора без сцены `IsPrismatic(j)` всегда false (призматической
        /// считается только ось 2 SCARA), то есть все суставы обрабатываются как вращательные,
        /// а `TcpAt` возвращает нулевую позу (кинематики без сцены нет).
        ///
        /// ЧЕГО ЭТО НЕ ДАЁТ (и потому не проверяется): геометрии TCP — `plan.Length`,
        /// полилиния, зазоры и кривизна остаются нулевыми; для них нужен реальный робот в сцене.
        /// </summary>
        private static PoseValidator ReadyValidator()
        {
            PoseValidator validator = new PoseValidator();
            PropertyInfo ready = typeof(PoseValidator).GetProperty("Ready",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(ready, Is.Not.Null, "PoseValidator.Ready — публичное свойство с приватным сеттером");
            ready.SetValue(validator, true);
            Assert.That(validator.Ready, Is.True, "флаг готовности выставлен отражением");
            return validator;
        }

        /// <summary>
        /// Главная точка входа время-оптимальной параметризации `KvTrajMath.Retime` без
        /// готового валидатора робота возвращает null (проверка `v == null || !v.Ready`
        /// стоит первой строкой метода) — то есть S-профиль НЕ строится молча и без исключений.
        /// Свежий `PoseValidator` (обычный класс, не MonoBehaviour) как раз не готов.
        /// </summary>
        [Test]
        public void Retime_WithoutReadyValidator_ReturnsNull()
        {
            PoseValidator validator = new PoseValidator();
            Assert.That(validator.Ready, Is.False, "свежий валидатор не готов к работе");
            Assert.That(validator.Dof, Is.EqualTo(0), "у неготового валидатора нет степеней свободы");

            Profile fixture = MakeProfile(32, new double[] { 45 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            KvMotionLimits limits = new KvMotionLimits();

            Assert.That(KvTrajMath.Retime(validator, fixture.Plan, limits), Is.Null,
                "без готового валидатора S-профиль не рассчитывается");
            Assert.That(KvTrajMath.Retime(null, fixture.Plan, limits), Is.Null,
                "null-валидатор не даёт профиля");
            Assert.That(KvTrajMath.Retime(validator, null, limits), Is.Null,
                "null-план не даёт профиля");
            Assert.That(fixture.Plan.Times, Is.Null,
                "исходный план не должен быть изменён неудачным расчётом");
        }

        /// <summary>
        /// САМ S-ПРОФИЛЬ ЧЕРЕЗ ПУБЛИЧНЫЙ ВХОД: `KvTrajMath.Retime` на прямом пути 90° при
        /// лимитах по умолчанию (90 °/с, 180 °/с², 1200 °/с³) возвращает пересчитанный план.
        ///
        /// Проверяется контракт плана и ФАКТИЧЕСКИЕ пределы на пересчитанной траектории:
        ///   • времена начинаются с нуля, строго возрастают, `plan.Time` = времени последнего сэмпла;
        ///   • геометрия пути НЕ меняется (меняется только распределение времени);
        ///   • ветвь помечена как S-профиль, а `LastProfile*` сообщает о результате расчёта;
        ///   • замеренные по сэмплам скорость, ускорение и РЫВОК каждого сустава укладываются
        ///     в лимиты — то есть требование ТЗ «jerk, acceleration, velocity» выполнено
        ///     на выходе реального публичного метода, а не только на его ядре SProfileBuild.
        /// </summary>
        [Test]
        public void Retime_RealEntryPoint_BuildsSProfileAndRetimesPlan()
        {
            PoseValidator validator = ReadyValidator();
            Profile fixture = MakeProfile(64, new double[] { 90 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            KvMotionLimits limits = new KvMotionLimits();

            PlannedTrajectory result = KvTrajMath.Retime(validator, fixture.Plan, limits);

            Assert.That(result, Is.Not.Null, "готовый валидатор + путь из 64 сэмплов дают план");
            Assert.That(result, Is.Not.SameAs(fixture.Plan), "расчёт идёт по копии плана (Clone)");
            Assert.That(result.Times, Is.Not.Null, "времена пересчитаны");

            for (int i = 0; i < fixture.N; i++)
                for (int j = 0; j < fixture.Dof; j++)
                    Assert.That(result.Path[i][j], Is.EqualTo(fixture.Plan.Path[i][j]).Within(1e-12),
                        $"сэмпл {i}, сустав {j}: форма пути не должна меняться");

            Assert.That(result.Times.Length, Is.EqualTo(fixture.N), "времён столько же, сколько сэмплов");
            Assert.That(result.Times[0], Is.EqualTo(0f), "профиль стартует с нуля");
            for (int i = 1; i < fixture.N; i++)
                Assert.That(result.Times[i], Is.GreaterThan(result.Times[i - 1]),
                    $"времена должны строго возрастать (шаг {i})");
            Assert.That(result.Time,
                Is.EqualTo((double)result.Times[result.Times.Length - 1]).Within(1e-9),
                "полное время плана = время последнего сэмпла");
            Assert.That(result.Time, Is.EqualTo(2.43145).Within(0.05),
                "время совпадает с расчётом ядра SProfileBuild на том же входе (90°, 90/180/1200)");
            Assert.That(result.BranchTag, Is.EqualTo("S-профиль (рывок ограничен)"),
                "ветвь помечена как S-профиль с ограничением рывка");

            Assert.That(KvTrajMath.LastProfileApplicable, Is.True, "последний профиль — аналитический S-профиль");
            Assert.That(KvTrajMath.LastProfileNote, Is.Not.Null.And.Not.Empty, "отчёт о профиле заполнен");
            Assert.That(KvTrajMath.LastProfileNote.StartsWith("S-профиль:"), Is.True,
                "отчёт начинается с описания S-профиля: " + KvTrajMath.LastProfileNote);
            Assert.That(KvTrajMath.LastProfileClamped, Is.EqualTo(0),
                "профиль сошёлся без срезов по пределу скорости");

            double[][] vel = Derivative(result.Path, result.Times);
            double[][] acc = Derivative(vel, result.Times);
            AssertAllSamplesWithin(vel, fixture.VelLimitDeg, MotionTol, "Retime: скорость сустава");
            AssertAllSamplesWithin(acc, fixture.AccLimitDeg, MotionTol, "Retime: ускорение сустава");
            Assert.That(MeasuredJerkDeg(result, result.Times, fixture.Scale),
                Is.LessThanOrEqualTo(fixture.JerkLimitDeg[0] * JerkTol),
                "Retime: замеренный рывок укладывается в maxJerk");
        }

        /// <summary>
        /// Вырожденный вход публичного метода: путь НУЛЕВОЙ длины. `Retime` обязан вернуть план
        /// без пересчёта времени (страховка `total &lt; 1e-6`), иначе S-профиль делил бы на
        /// нулевую длину пути. Проверяется, что возвращён именно план, времена и полное время
        /// остались исходными, а метка ветви не заменена на «S-профиль…».
        /// </summary>
        [Test]
        public void Retime_ZeroLengthPath_ReturnsPlanWithoutRetiming()
        {
            PoseValidator validator = ReadyValidator();

            PlannedTrajectory source = new PlannedTrajectory();
            source.Path = new double[16][];
            for (int i = 0; i < 16; i++) source.Path[i] = new double[] { 42.0 };   // все сэмплы совпадают
            source.Times = new float[16];
            for (int i = 0; i < 16; i++) source.Times[i] = i * 0.1f;
            source.Time = 1.5;
            source.BranchTag = "исходная ветвь";
            source.Label = "исходный план";

            PlannedTrajectory result = KvTrajMath.Retime(validator, source, new KvMotionLimits());

            Assert.That(result, Is.Not.Null, "нулевой путь — не повод вернуть null");
            Assert.That(result, Is.Not.SameAs(source), "возвращается копия исходного плана");
            Assert.That(result.BranchTag, Is.EqualTo("исходная ветвь"),
                "время не пересчитывалось — метка ветви осталась исходной");
            Assert.That(result.Label, Is.EqualTo("исходный план"), "подпись исходного плана сохранена");
            Assert.That(result.Time, Is.EqualTo(1.5).Within(1e-9), "полное время не пересчитано");
            Assert.That(result.Times.Length, Is.EqualTo(16), "времена на месте");
            for (int i = 0; i < 16; i++)
                Assert.That(result.Times[i], Is.EqualTo(source.Times[i]),
                    $"времена не пересчитаны (сэмпл {i})");
        }

        /// <summary>
        /// Метрики и проверка рывка, которым нужен готовый валидатор, без него возвращают
        /// НЕЙТРАЛЬНЫЕ значения (не бросают исключений и не делят на ноль):
        /// `Analyze` — невалидную статистику, `VerifyJerk` — «рывок в норме» с пустым отчётом.
        /// Именно этими вызовами `KvTimeOptimal.Compute` наполняет `draft.stats/original/jerkOk`.
        /// </summary>
        [Test]
        public void AnalyzeAndVerifyJerk_WithoutReadyValidator_ReturnNeutralDefaults()
        {
            PoseValidator validator = new PoseValidator();
            Profile fixture = MakeProfile(32, new double[] { 45 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            KvMotionLimits limits = new KvMotionLimits();

            KvTrajStats stats = KvTrajMath.Analyze(validator, fixture.Plan, null, null, Vector3.zero);
            Assert.That(stats.valid, Is.False, "без валидатора статистика помечена невалидной");
            Assert.That(stats.samples, Is.EqualTo(0), "сэмплов не посчитано");
            Assert.That(stats.time, Is.EqualTo(0f), "время не посчитано");
            Assert.That(stats.maxVel, Is.EqualTo(0f), "скорость не посчитана");
            Assert.That(stats.maxAcc, Is.EqualTo(0f), "ускорение не посчитано");
            Assert.That(stats.maxJerk, Is.EqualTo(0f), "рывок не посчитан");

            string report;
            bool ok = KvTrajMath.VerifyJerk(validator, fixture.Plan, limits, null, out report);
            Assert.That(ok, Is.True, "без замера рывок считается уложившимся (ложных тревог нет)");
            Assert.That(report, Is.EqualTo(""), "отчёт о рывке пуст, если замер невозможен");
        }

        /// <summary>
        /// Геометрические и энергетические помощники S-профиля без готового валидатора
        /// возвращают пустые результаты (0 / пустые массивы) — расчёт длины пути TCP,
        /// полилинии, плеч удержания и энергии не выполняется и не падает.
        /// </summary>
        [Test]
        public void GeometryAndEnergyHelpers_WithoutReadyValidator_ReturnEmptyValues()
        {
            PoseValidator validator = new PoseValidator();
            Profile fixture = MakeProfile(32, new double[] { 45 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });

            Assert.That(KvTrajMath.PathLength(validator, fixture.Plan.Path), Is.EqualTo(0f),
                "длина пути TCP без валидатора — 0");
            Assert.That(KvTrajMath.TcpPolyline(validator, fixture.Plan.Path).Length, Is.EqualTo(0),
                "полилиния TCP пуста");
            Assert.That(KvTrajMath.LeverArms(validator, fixture.Plan.Path, Vector3.zero).Length,
                Is.EqualTo(0), "плечи удержания не считаются");

            float peak;
            float energy = KvTrajMath.Energy(validator, fixture.Plan, null, Vector3.zero, out peak);
            Assert.That(energy, Is.EqualTo(0f), "энергия без валидатора — 0");
            Assert.That(peak, Is.EqualTo(0f), "пиковая мощность — 0");

            Assert.That(KvTrajMath.PathLength(null, fixture.Plan.Path), Is.EqualTo(0f),
                "null-валидатор тоже даёт 0");
            Assert.That(KvTrajMath.LeverArms(null, fixture.Plan.Path, Vector3.zero).Length,
                Is.EqualTo(0), "null-валидатор не даёт плеч");
        }

        // ==================================================================== 5. Подготовка времён и копия плана

        /// <summary>
        /// `EnsureTimes` — вход S-профиля: без корректного массива времён `Retime` не работает.
        /// Непригодные планы (null, без пути, из одного сэмпла) дают false, а план без времён
        /// получает равномерную сетку 0…1 и `Time = 1.0`; уже корректные времена НЕ трогаются
        /// (тот же массив, те же значения).
        /// </summary>
        [Test]
        public void EnsureTimes_InvalidPlansReturnFalseAndValidOnesAreFilledOrKept()
        {
            Assert.That(KvTrajMath.EnsureTimes(null), Is.False, "null-план непригоден");

            PlannedTrajectory noPath = new PlannedTrajectory();
            Assert.That(KvTrajMath.EnsureTimes(noPath), Is.False, "план без пути непригоден");

            PlannedTrajectory single = new PlannedTrajectory { Path = new double[1][] };
            single.Path[0] = new double[] { 0 };
            Assert.That(KvTrajMath.EnsureTimes(single), Is.False, "один сэмпл — не траектория");

            PlannedTrajectory fresh = new PlannedTrajectory { Path = new double[5][] };
            for (int i = 0; i < 5; i++) fresh.Path[i] = new double[] { i * 10.0 };
            fresh.Times = null;
            Assert.That(KvTrajMath.EnsureTimes(fresh), Is.True, "план без времён приводится в порядок");
            Assert.That(fresh.Times, Is.Not.Null, "массив времён создан");
            Assert.That(fresh.Times.Length, Is.EqualTo(5), "длина времён совпадает с числом сэмплов");
            Assert.That(fresh.Times[0], Is.EqualTo(0f), "сетка начинается с нуля");
            Assert.That(fresh.Times[4], Is.EqualTo(1f).Within(1e-5f), "сетка заканчивается единицей");
            Assert.That(fresh.Time, Is.EqualTo(1.0), "полное время сетки — 1");
            for (int i = 1; i < 5; i++)
                Assert.That(fresh.Times[i], Is.GreaterThan(fresh.Times[i - 1]),
                    $"равномерная сетка должна возрастать (шаг {i})");

            float[] kept = fresh.Times;
            Assert.That(KvTrajMath.EnsureTimes(fresh), Is.True, "корректные времена принимаются");
            Assert.That(fresh.Times, Is.SameAs(kept), "корректный массив времён не пересоздаётся");

            PlannedTrajectory valid = new PlannedTrajectory { Path = new double[3][] };
            for (int i = 0; i < 3; i++) valid.Path[i] = new double[] { i };
            valid.Times = new float[] { 0f, 1.5f, 2.5f };
            Assert.That(KvTrajMath.EnsureTimes(valid), Is.True, "готовые времена принимаются");
            Assert.That(valid.Times[1], Is.EqualTo(1.5f), "готовые времена не перезаписываются");
            Assert.That(valid.Times[2], Is.EqualTo(2.5f), "готовые времена не перезаписываются");

            PlannedTrajectory shortTimes = new PlannedTrajectory { Path = new double[4][] };
            for (int i = 0; i < 4; i++) shortTimes.Path[i] = new double[] { i };
            shortTimes.Times = new float[] { 0f, 1f };
            Assert.That(KvTrajMath.EnsureTimes(shortTimes), Is.True, "несовпадающая длина времён исправляется");
            Assert.That(shortTimes.Times.Length, Is.EqualTo(4), "времена пересобраны под число сэмплов");
        }

        /// <summary>
        /// `Clone` — защита исходного пути: `Retime` считает S-профиль по КОПИИ плана
        /// (`KvTrajMath.Clone(source, label)`), поэтому перераспределение времени не может
        /// испортить исходную траекторию варианта. Проверяем глубокую копию массивов.
        /// </summary>
        [Test]
        public void Clone_DeepCopiesPathAndTimes()
        {
            Assert.That(KvTrajMath.Clone(null), Is.Null, "копия null-плана — null");

            Profile fixture = MakeProfile(16, new double[] { 30 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            fixture.Plan.Times = new float[] { 0f, 0.5f };
            fixture.Plan.Time = 0.5;
            fixture.Plan.BranchTag = "S-ветвь";

            PlannedTrajectory copy = KvTrajMath.Clone(fixture.Plan, "копия");

            Assert.That(copy, Is.Not.SameAs(fixture.Plan), "копия — отдельный объект");
            Assert.That(copy.Path, Is.Not.SameAs(fixture.Plan.Path), "путь скопирован по ссылке-массиву");
            Assert.That(copy.Path[5], Is.Not.SameAs(fixture.Plan.Path[5]), "сэмплы скопированы глубоко");
            Assert.That(copy.Times, Is.Not.SameAs(fixture.Plan.Times), "времена скопированы");
            Assert.That(copy.Label, Is.EqualTo("копия"), "подпись переопределяется аргументом");
            Assert.That(copy.Time, Is.EqualTo(fixture.Plan.Time).Within(1e-6), "время скопировано");
            Assert.That(copy.BranchTag, Is.EqualTo("S-ветвь"), "метка ветви скопирована");

            double original = fixture.Plan.Path[5][0];
            copy.Path[5][0] = original + 100.0;
            copy.Times[0] = 99f;
            Assert.That(fixture.Plan.Path[5][0], Is.EqualTo(original).Within(1e-9),
                "правка копии не меняет исходный путь");
            Assert.That(fixture.Plan.Times[0], Is.EqualTo(0f), "правка копии не меняет исходные времена");

            PlannedTrajectory keepLabel = KvTrajMath.Clone(fixture.Plan);
            Assert.That(keepLabel.Label, Is.EqualTo(fixture.Plan.Label),
                "без аргумента подпись берётся из исходного плана");
        }

        // ==================================================================== 6. S-профиль: время и покой на концах

        /// <summary>
        /// ТЗ п.2 и п.8: времена профиля начинаются с нуля, строго возрастают (ни одного
        /// «нулевого шага») и дают положительное полное время. `SProfileBuild` заполняет
        /// ПЕРЕДАННЫЙ массив времён, а полное время в `plan.Time` переносит вызывающий
        /// (`Retime`) — здесь это зафиксировано, чтобы не появилось двух источников правды.
        /// </summary>
        [Test]
        public void SProfile_TimesStartAtZeroAndIncreaseStrictly()
        {
            Profile p = MakeProfile(64, new double[] { 90 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            AssertTimesWellFormed(p, "номинальный S-профиль");
            Assert.That(p.Times[p.N - 1], Is.EqualTo(2.43145f).Within(0.05f),
                "полное время номинального профиля (90°, 90/180/1200)");
            Assert.That(p.Plan.Time, Is.EqualTo(0.0).Within(1e-12),
                "plan.Time заполняет вызывающий (Retime), а не сам билдер");
            Assert.That(p.Plan.Times, Is.Null, "билдер пишет только в переданный массив времён");
        }

        /// <summary>
        /// ТЗ п.3: профиль начинается и заканчивается состоянием покоя — скорости вдоль пути
        /// на первом и последнем сэмпле РОВНО нулевые, а внутри пути строго положительные
        /// (профиль нигде не «замирает»: работает «пол» скорости).
        /// </summary>
        [Test]
        public void SProfile_OutputVelocitiesStartAndEndAtRest()
        {
            Profile p = MakeProfile(64, new double[] { 90 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            Assert.That(p.Vel[0], Is.EqualTo(0.0), "старт с нулевой скорости");
            Assert.That(p.Vel[p.N - 1], Is.EqualTo(0.0), "финиш с нулевой скорости");
            for (int i = 1; i < p.N - 1; i++)
            {
                Assert.That(double.IsNaN(p.Vel[i]) || double.IsInfinity(p.Vel[i]), Is.False,
                    $"скорость сэмпла {i} не конечна ({p.Vel[i]})");
                Assert.That(p.Vel[i], Is.GreaterThan(0.0),
                    $"внутри пути профиль должен двигаться (сэмпл {i}: {p.Vel[i]})");
            }

            // Замер на КРАЯХ: конечная разность на первом/последнем сэмпле — односторонняя,
            // поэтому даёт СРЕДНЮЮ скорость за первый/последний интервал, а не мгновенную
            // (мгновенная на концах равна нулю по построению — см. проверки Vel[] выше).
            // Для ПЕРВОГО интервала средняя скорость мала (профиль трогается плавно);
            // для ПОСЛЕДНЕГО она велика — профиль останавливается за один шаг, а не плавно.
            // Это фактическая особенность текущей реализации; симметрия разгона и торможения
            // разобрана отдельно в SProfile_SymmetryIsNotHeld_Characterized.
            double[][] measured = VelocityOf(p);
            double peak = MaxAbs(measured);
            Assert.That(peak, Is.GreaterThan(0.0), "профиль должен двигаться");
            Assert.That(Math.Abs(measured[0][0]), Is.LessThan(peak * 0.15),
                $"первый интервал должен быть медленным (плавный старт): {Math.Abs(measured[0][0]):G6} °/с " +
                $"при пике {peak:G6} °/с");
            Assert.That(Math.Abs(measured[p.N - 1][0]), Is.LessThanOrEqualTo(peak * MotionTol),
                "средняя скорость последнего интервала не должна превышать пиковую");
        }

        // ==================================================================== 7. S-профиль: лимиты jerk / acceleration / velocity

        /// <summary>
        /// ТЗ п.1 (уровень билдера): ни один внутренний сэмпл не выходит за предел скорости
        /// ВДОЛЬ пути, и счётчик срезов `clamped` равен нулю — профиль сошёлся без «подрезки».
        /// Сравнение — той же формулой, что в самом `SProfileBuild`.
        /// </summary>
        [Test]
        public void SProfile_InteriorVelocityNeverExceedsPathLimit()
        {
            Profile p = MakeProfile(64, new double[] { 90 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            Assert.That(p.Clamped, Is.EqualTo(0), "профиль не потребовал среза по пределу скорости");
            for (int i = 1; i < p.N - 1; i++)
            {
                double limit = Math.Max(1e-6, p.Vmax[i]) * 1.001 + 1e-9;
                Assert.That(p.Vel[i], Is.LessThanOrEqualTo(limit),
                    $"скорость вдоль пути на сэмпле {i}: {p.Vel[i]:G6} > предел {limit:G6}");
            }
        }

        /// <summary>
        /// ТЗ п.1 (скорость): для каждого сустава и КАЖДОГО сэмпла замеренная конечными
        /// разностями скорость не превышает `maxSpeed` сустава. Набор фикстур: короткий путь,
        /// номинальный, длинный (путь почти вчетверо длиннее, чем нужно для разгона)
        /// и двухсуставный — то есть проверка не зависит от одного режима.
        /// </summary>
        [Test]
        public void SProfile_MeasuredJointSpeedNeverExceedsMaxSpeed()
        {
            List<Profile> set = NominalProfiles();
            for (int k = 0; k < set.Count; k++)
            {
                Profile p = set[k];
                Build(p);
                string what = "S-профиль, фикстура " + k;
                AssertTimesWellFormed(p, what);
                AssertAllSamplesWithin(VelocityOf(p), p.VelLimitDeg, MotionTol,
                    what + ": скорость сустава");
            }
        }

        /// <summary>
        /// ТЗ п.1 (ускорение): замеренное ускорение каждого сустава нигде не превышает
        /// `maxAcceleration`, причём с двойным запасом — сначала физический предел,
        /// затем КАСАТЕЛЬНЫЙ предел `Retime` (множитель 0.4 на кривизну пути),
        /// и отдельно — внутренняя оценка `accelPeak`, которая обязана лежать
        /// внутри касательного предела по построению (ускорение ограничено `Math.Min(al, ...)`).
        /// </summary>
        [Test]
        public void SProfile_MeasuredJointAccelerationNeverExceedsMaxAcceleration()
        {
            List<Profile> set = NominalProfiles();
            for (int k = 0; k < set.Count; k++)
            {
                Profile p = set[k];
                Build(p);
                string what = "S-профиль, фикстура " + k;

                double[][] acc = AccelerationOf(p);
                AssertAllSamplesWithin(acc, p.AccLimitDeg, MotionTol,
                    what + ": ускорение сустава (физический предел)");

                double[] tangential = new double[p.Dof];
                for (int j = 0; j < p.Dof; j++) tangential[j] = p.AccLimitDeg[j] * 0.4;
                AssertAllSamplesWithin(acc, tangential, 1.10,
                    what + ": ускорение сустава (касательный предел с запасом на кривизну)");

                double amaxMax = 0.0;
                for (int i = 0; i < p.N; i++) amaxMax = Math.Max(amaxMax, p.Amax[i]);
                Assert.That(p.AccelPeak, Is.LessThanOrEqualTo(amaxMax + 1e-9),
                    $"{what}: внутренний accelPeak {p.AccelPeak:G6} выше касательного предела {amaxMax:G6}");
            }
        }

        /// <summary>
        /// ТЗ п.1 (главное — jerk): фактический рывок профиля, замеренный ТЕМ ЖЕ способом,
        /// что и метрика (`KvTrajMath.MeasureJerk`, конечные разности по времени, нормировка
        /// суставов), нигде не превышает `maxJerk`. Это и есть «честная проверка рывка по
        /// сэмплам», которую `Retime` выполняет после построения профиля.
        /// </summary>
        [Test]
        public void SProfile_MeasuredJerkNeverExceedsMaxJerk()
        {
            List<Profile> set = NominalProfiles();
            for (int k = 0; k < set.Count; k++)
            {
                Profile p = set[k];
                Build(p);
                string what = "S-профиль, фикстура " + k;

                double minLimit = double.MaxValue;
                for (int j = 0; j < p.Dof; j++) minLimit = Math.Min(minLimit, p.JerkLimitDeg[j]);

                double measured = MeasuredJerkDeg(p);
                Assert.That(measured, Is.LessThanOrEqualTo(minLimit * JerkTol),
                    $"{what}: замеренный рывок {measured:G6} °/с³ > предел {minLimit:G6} °/с³ " +
                    $"(допуск {JerkTol:P0} на конечные разности)");
                Assert.That(double.IsNaN(measured), Is.False, what + ": замер рывка не NaN");
            }
        }

        /// <summary>
        /// ТЗ п.4: короткий путь (5°) — разогнаться до `maxSpeed` физически невозможно,
        /// поэтому пиковая скорость заведомо ниже лимита, но профиль остаётся корректным:
        /// времена монотонны, концы в покое, срезов по пределу нет, движение есть.
        /// </summary>
        [Test]
        public void SProfile_ShortMoveKeepsPeakSpeedBelowLimit()
        {
            Profile p = MakeProfile(64, new double[] { 5 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            AssertTimesWellFormed(p, "короткое перемещение 5°");
            Assert.That(p.Clamped, Is.EqualTo(0), "срезов по пределу скорости нет");
            Assert.That(p.Vel[0], Is.EqualTo(0.0), "старт в покое");
            Assert.That(p.Vel[p.N - 1], Is.EqualTo(0.0), "финиш в покое");

            double peak = MaxAbs(VelocityOf(p));
            Assert.That(peak, Is.GreaterThan(0.0), "короткое перемещение всё равно выполняется");
            Assert.That(peak, Is.LessThan(p.VelLimitDeg[0] * 0.5),
                $"пиковая скорость {peak:G6} °/с должна быть заметно ниже лимита " +
                $"{p.VelLimitDeg[0]:G6} °/с — разгон не успевает завершиться");
        }

        /// <summary>
        /// ТЗ п.8 (обратная сторона п.4): на длинном пути (360°, вчетверо длиннее разгонного
        /// участка) профиль обязан ВЫЙТИ на предел скорости — иначе время-оптимальность
        /// не выполняется. Проверяем, что пиковая скорость близка к лимиту и не превышает его.
        /// </summary>
        [Test]
        public void SProfile_LongMoveUsesMostOfSpeedLimit()
        {
            Profile p = MakeProfile(129, new double[] { 360 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            AssertTimesWellFormed(p, "длинное перемещение 360°");
            double peak = MaxAbs(VelocityOf(p));
            Assert.That(peak, Is.GreaterThan(p.VelLimitDeg[0] * 0.6),
                $"на длинном пути профиль должен использовать лимит скорости: " +
                $"пик {peak:G6} °/с при лимите {p.VelLimitDeg[0]:G6} °/с");
            Assert.That(peak, Is.LessThanOrEqualTo(p.VelLimitDeg[0] * MotionTol),
                $"пиковая скорость {peak:G6} °/с не должна превышать лимит " +
                $"{p.VelLimitDeg[0]:G6} °/с");
        }

        /// <summary>
        /// Форма пути НЕ меняется — меняется только распределение времени: билдер S-профиля
        /// не трогает ни `plan.Path`, ни длину пути `s` (все сэмплы остаются ровно такими,
        /// какими их передали). Это же гарантирует `KvTrajMath.Clone` перед расчётом.
        /// </summary>
        [Test]
        public void SProfile_RetimingDoesNotChangePathGeometry()
        {
            Profile p = MakeProfile(48, new double[] { 75, -20 }, new double[] { 90, 45 },
                new double[] { 180, 120 }, new double[] { 1200, 600 });

            double[][] pathBefore = new double[p.N][];
            for (int i = 0; i < p.N; i++)
            {
                pathBefore[i] = new double[p.Dof];
                Array.Copy(p.Plan.Path[i], pathBefore[i], p.Dof);
            }
            double[] sBefore = (double[])p.S.Clone();

            Build(p);

            for (int i = 0; i < p.N; i++)
                for (int j = 0; j < p.Dof; j++)
                    Assert.That(p.Plan.Path[i][j], Is.EqualTo(pathBefore[i][j]).Within(1e-12),
                        $"сэмпл {i}, сустав {j}: геометрия пути изменилась при пересчёте времён");
            for (int i = 0; i < p.N; i++)
                Assert.That(p.S[i], Is.EqualTo(sBefore[i]).Within(1e-12),
                    $"длина пути на сэмпле {i} изменилась");
        }

        // ==================================================================== 8. Вырожденные и экстремальные входы

        /// <summary>
        /// ТЗ п.5: нулевое перемещение. Билдер не делит на нулевую длину пути и не выдаёт
        /// NaN/Infinity: времена остаются конечными и строго возрастающими, минимальными
        /// (страховочный шаг ~1e-3 с и «пол» 1e-5 с на шаг), скорость на концах — ровно ноль.
        ///
        /// ВАЖНО: в рабочем потоке такой путь до билдера не доходит — `KvTrajMath.Retime`
        /// выходит раньше по условию `total &lt; 1e-6` («путь нулевой длины — время не
        /// пересчитываем»). Здесь проверяется именно защитная ветка самого S-профиля.
        /// </summary>
        [Test]
        public void SProfile_ZeroLengthMoveStaysFiniteAndMinimal()
        {
            Profile p = MakeProfile(32, new double[] { 0 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            Assert.That(p.S[p.N - 1], Is.EqualTo(0.0), "длина пути нулевая");
            AssertTimesWellFormed(p, "нулевое перемещение");
            Assert.That(p.Times[p.N - 1], Is.LessThanOrEqualTo(0.01f),
                $"полное время нулевого перемещения должно быть минимальным, получено {p.Times[p.N - 1]}");
            Assert.That(p.Vel[0], Is.EqualTo(0.0), "старт в покое");
            Assert.That(p.Vel[p.N - 1], Is.EqualTo(0.0), "финиш в покое");
            Assert.That(p.Clamped, Is.EqualTo(0), "срезов по пределу нет");
            Assert.That(p.JerkPeak, Is.EqualTo(0.0), "на нулевом пути рывка нет");
            Assert.That(p.AccelPeak, Is.EqualTo(0.0), "на нулевом пути ускорения нет");
            Assert.That(MeasuredJerkDeg(p), Is.EqualTo(0.0),
                "замер рывка на нулевом пути даёт ровно ноль (без деления на ноль)");
        }

        /// <summary>
        /// ТЗ п.6: нулевые и отрицательные лимиты. Деления на ноль не происходит и исключений
        /// не возникает: времена остаются КОНЕЧНЫМИ и строго возрастающими, профиль начинается
        /// и заканчивается в покое. Фактическое поведение кода: пределы скорости заменяются
        /// «полом» (`Math.Max(1e-6, vmax)`), ускорение — минимумом 1e-6, страховка времени
        /// даёт шаг 1e-3 с; при нулевых лимитах полное время получается огромным (порядка
        /// 3.2e6 с на 30°), при отрицательных — маленьким (0.031 с), но всегда определённым.
        ///
        /// В рабочем потоке это тоже недостижимо: `KvTimeOptimal.SetLimits` клампит лимиты
        /// в минимумы (1 / 1 / 10), поэтому нули и минусы до S-профиля не доходят.
        /// </summary>
        [Test]
        public void SProfile_ZeroAndNegativeLimits_StayFiniteWithoutExceptions()
        {
            Profile zero = MakeProfile(32, new double[] { 30 }, new double[] { 0 },
                new double[] { 0 }, new double[] { 0 });
            Assert.DoesNotThrow(delegate { Build(zero); }, "нулевые лимиты не должны бросать исключение");
            AssertTimesWellFormed(zero, "нулевые лимиты");
            Assert.That(zero.Vel[0], Is.EqualTo(0.0), "нулевые лимиты: старт в покое");
            Assert.That(zero.Vel[zero.N - 1], Is.EqualTo(0.0), "нулевые лимиты: финиш в покое");
            Assert.That(zero.Clamped, Is.EqualTo(0), "нулевые лимиты: срезов нет");
            Assert.That(double.IsNaN(zero.JerkPeak), Is.False, "нулевые лимиты: рывок не NaN");
            Assert.That(zero.Times[zero.N - 1], Is.GreaterThan(0f),
                "нулевые лимиты: время конечно и положительно");

            Profile negative = MakeProfile(32, new double[] { 30 }, new double[] { -90 },
                new double[] { -180 }, new double[] { -1200 });
            Assert.DoesNotThrow(delegate { Build(negative); },
                "отрицательные лимиты не должны бросать исключение");
            AssertTimesWellFormed(negative, "отрицательные лимиты");
            Assert.That(negative.Vel[0], Is.EqualTo(0.0), "отрицательные лимиты: старт в покое");
            Assert.That(negative.Vel[negative.N - 1], Is.EqualTo(0.0), "отрицательные лимиты: финиш в покое");
            Assert.That(negative.Clamped, Is.EqualTo(0), "отрицательные лимиты: срезов нет");
            Assert.That(negative.Times[negative.N - 1], Is.GreaterThan(0f),
                "отрицательные лимиты: время конечно и положительно");
        }

        /// <summary>
        /// ТЗ п.7: экстремальные лимиты. Очень большие (9e7 °/с) и очень маленькие (9e-5 °/с)
        /// пределы не ломают профиль: времена конечны и монотонны, лимиты соблюдены, срезов нет.
        /// Для микроскопических лимитов полное время получается большим (порядка 4.5e5 с),
        /// но остаётся конечным числом — переполнения float нет.
        /// </summary>
        [Test]
        public void SProfile_ExtremeLimitsRespectLimitsAndStayFinite()
        {
            Profile huge = MakeProfile(64, new double[] { 90 }, new double[] { 9e7 },
                new double[] { 9e7 }, new double[] { 9e7 });
            Build(huge);
            AssertTimesWellFormed(huge, "огромные лимиты");
            Assert.That(huge.Clamped, Is.EqualTo(0), "огромные лимиты: срезов нет");
            AssertAllSamplesWithin(VelocityOf(huge), huge.VelLimitDeg, MotionTol,
                "огромные лимиты: скорость сустава");
            AssertAllSamplesWithin(AccelerationOf(huge), huge.AccLimitDeg, MotionTol,
                "огромные лимиты: ускорение сустава");
            Assert.That(MeasuredJerkDeg(huge), Is.LessThanOrEqualTo(huge.JerkLimitDeg[0] * JerkTol),
                "огромные лимиты: замеренный рывок внутри предела");

            Profile tiny = MakeProfile(32, new double[] { 30 }, new double[] { 9e-5 },
                new double[] { 9e-5 }, new double[] { 9e-5 });
            Build(tiny);
            AssertTimesWellFormed(tiny, "микроскопические лимиты");
            Assert.That(tiny.Clamped, Is.EqualTo(0), "микроскопические лимиты: срезов нет");
            Assert.That(tiny.Times[tiny.N - 1], Is.GreaterThan(0f),
                "микроскопические лимиты: время положительно и конечно");
            Assert.That(float.IsInfinity(tiny.Times[tiny.N - 1]), Is.False,
                "микроскопические лимиты: время не переполнилось до бесконечности");
            AssertAllSamplesWithin(VelocityOf(tiny), tiny.VelLimitDeg, MotionTol,
                "микроскопические лимиты: скорость сустава");
            Assert.That(MeasuredJerkDeg(tiny), Is.LessThanOrEqualTo(tiny.JerkLimitDeg[0] * JerkTol),
                "микроскопические лимиты: замеренный рывок внутри предела");
        }

        // ==================================================================== 9. Согласованность: путь и лимиты

        /// <summary>
        /// ТЗ п.8: чем больше путь при тех же лимитах, тем больше время.
        /// (30° и 60°, одинаковые пределы 90/180/1200.)
        /// </summary>
        [Test]
        public void SProfile_LongerPathTakesLongerTime()
        {
            Profile shortMove = MakeProfile(64, new double[] { 30 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Profile longMove = MakeProfile(64, new double[] { 60 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(shortMove);
            Build(longMove);

            Assert.That(longMove.Times[longMove.N - 1], Is.GreaterThan(shortMove.Times[shortMove.N - 1]),
                $"60° должны занять больше времени, чем 30°: {longMove.Times[longMove.N - 1]} с " +
                $"против {shortMove.Times[shortMove.N - 1]} с");
            Assert.That(longMove.Times[longMove.N - 1], Is.GreaterThan(0f), "время положительно");
        }

        /// <summary>
        /// ТЗ п.8: чем выше лимиты, тем меньше время (одинаковый путь 90°, лимиты удвоены
        /// по всем трём осям ограничений: 90/180/1200 → 180/360/2400).
        ///
        /// Замечание по фактическому поведению: увеличение ТОЛЬКО предела скорости время
        /// на таком перемещении не меняет — оно ограничено ускорением и рывком (профиль
        /// упирается в разгонный участок), поэтому проверяется согласованное повышение
        /// всех трёх лимитов.
        /// </summary>
        [Test]
        public void SProfile_HigherLimitsTakeLessTime()
        {
            Profile baseLimits = MakeProfile(64, new double[] { 90 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Profile doubleLimits = MakeProfile(64, new double[] { 90 }, new double[] { 180 },
                new double[] { 360 }, new double[] { 2400 });
            Build(baseLimits);
            Build(doubleLimits);

            Assert.That(doubleLimits.Times[doubleLimits.N - 1],
                Is.LessThan(baseLimits.Times[baseLimits.N - 1]),
                $"удвоенные лимиты должны сократить время: {doubleLimits.Times[doubleLimits.N - 1]} с " +
                $"против {baseLimits.Times[baseLimits.N - 1]} с");
            Assert.That(doubleLimits.Clamped, Is.EqualTo(0), "профиль с высокими лимитами сошёлся");
        }

        /// <summary>
        /// ТЗ п.1 на многозвенном случае: два сустава едут одновременно с РАЗНЫМИ лимитами
        /// (90/180/1200 и 45/120/600). Пределы вдоль пути берутся по самому строгому суставу,
        /// поэтому каждый сустав обязан уложиться в СВОЙ предел — проверяем покадрово.
        /// </summary>
        [Test]
        public void SProfile_MultiJointPathRespectsPerJointLimits()
        {
            Profile p = MakeProfile(96, new double[] { 60, -40 }, new double[] { 90, 45 },
                new double[] { 180, 120 }, new double[] { 1200, 600 });
            Build(p);

            AssertTimesWellFormed(p, "двухсуставный S-профиль");
            Assert.That(p.Clamped, Is.EqualTo(0), "двухсуставный профиль сошёлся без срезов");
            Assert.That(p.Vel[0], Is.EqualTo(0.0), "старт в покое");
            Assert.That(p.Vel[p.N - 1], Is.EqualTo(0.0), "финиш в покое");

            double[][] vel = VelocityOf(p);
            double[][] acc = AccelerationOf(p);
            AssertAllSamplesWithin(vel, p.VelLimitDeg, MotionTol, "два сустава: скорость");
            AssertAllSamplesWithin(acc, p.AccLimitDeg, MotionTol, "два сустава: ускорение");

            double minJerkLimit = Math.Min(p.JerkLimitDeg[0], p.JerkLimitDeg[1]);
            Assert.That(MeasuredJerkDeg(p), Is.LessThanOrEqualTo(minJerkLimit * JerkTol),
                $"два сустава: замеренный рывок {MeasuredJerkDeg(p):G6} °/с³ > предел " +
                $"{minJerkLimit:G6} °/с³ (самый строгий из суставов)");

            Assert.That(vel.Length, Is.EqualTo(p.N), "замер скорости по всем сэмплам");
            for (int i = 1; i <= 3 && i < p.N - 1; i++)
                Assert.That(Math.Abs(vel[i][0]) + Math.Abs(vel[i][1]), Is.GreaterThan(0.0),
                    $"суставы должны двигаться уже на сэмпле {i}");
        }

        // ==================================================================== 10. Замер рывка (инструмент проверки)

        /// <summary>
        /// `MeasureJerk` — тот самый замер, по которому `Retime` решает, уложился ли профиль
        /// в предел рывка. Его защитные ветки: null-входы, слишком короткий путь и
        /// несовпадающая длина времён дают ровно 0 (а не исключение и не NaN).
        /// </summary>
        [Test]
        public void MeasureJerk_GuardsReturnZeroForUnusableInput()
        {
            Assert.That(KvTrajMath.MeasureJerk(null, null, null), Is.EqualTo(0.0),
                "null-входы дают нулевой рывок");

            double[][] two = new double[2][];
            two[0] = new double[] { 0 };
            two[1] = new double[] { 10 };
            Assert.That(KvTrajMath.MeasureJerk(two, new float[] { 0f, 1f }, new double[] { RevScale }),
                Is.EqualTo(0.0), "по двум сэмплам рывок не оценивается");

            double[][] three = new double[3][];
            for (int i = 0; i < 3; i++) three[i] = new double[] { i * 10.0 };
            Assert.That(KvTrajMath.MeasureJerk(three, new float[] { 0f, 1f }, new double[] { RevScale }),
                Is.EqualTo(0.0), "несовпадающая длина времён — рывок не оценивается");
            Assert.That(KvTrajMath.MeasureJerk(three, null, new double[] { RevScale }),
                Is.EqualTo(0.0), "null-времена — рывок не оценивается");
        }

        /// <summary>
        /// `MeasureJerk` на равномерном (линейном по времени) пути даёт практически нулевой
        /// рывок: конечные разности третьего порядка взаимно уничтожаются, остаётся только
        /// шум округления float-времён (порядка 1e-4 единицы пути — это ~0.02 °/с³).
        /// Плюс проверяется нормировка: тот же путь с масштабом призматической оси (×10 вместо
        /// 1/90) даёт ровно в 900 раз больший замер — замер сравним с пределом рывка сустава.
        /// </summary>
        [Test]
        public void MeasureJerk_LinearPathIsNearZeroAndScalesWithJointNormalisation()
        {
            int n = 33;
            double[][] path = new double[n][];
            float[] times = new float[n];
            for (int i = 0; i < n; i++)
            {
                path[i] = new double[] { 3.0 * i };   // 3° за шаг
                times[i] = 0.05f * i;                 // постоянная скорость: 60 °/с, ускорений нет
            }

            double revolute = KvTrajMath.MeasureJerk(path, times, new double[] { RevScale });
            Assert.That(revolute, Is.LessThan(1e-3),
                $"на равномерном движении рывок должен быть численным нулём, получено {revolute:G6}");

            double prismatic = KvTrajMath.MeasureJerk(path, times, new double[] { 10.0 });
            Assert.That(prismatic / revolute, Is.EqualTo(900.0).Within(1e-6),
                "нормировка сустава масштабирует замер ровно в 900 раз (10 против 1/90)");
        }

        // ==================================================================== 11. Симметрия: зафиксированное отклонение

        /// <summary>
        /// ТЗ п.9 (симметрия) — НЕ ВЫПОЛНЯЕТСЯ для текущей реализации, тест это фиксирует.
        ///
        /// При симметричном входе (прямой путь, постоянные лимиты, покой на обоих концах)
        /// профиль должен был бы быть зеркальным. Фактически он заметно несимметричен:
        /// ускорение строится ОДНОСТОРОННЕЙ рекурсией (`a[0] = 0`, проход вперёд по сэмплам),
        /// а нулевая скорость на финише выставляется принудительно в самом конце, поэтому
        /// торможение не зеркально разгону. Измерено на фикстуре 90°/n=64 с лимитами
        /// 90/180/1200: расхождение зеркальных скоростей до 0.35 ед. (≈40 % от пика 0.87),
        /// расхождение зеркальных времён до 0.97 с при полном времени 2.43 с; на n=257
        /// расхождение скоростей достигает 0.92 ед. при пике 0.96 — то есть почти на весь пик.
        ///
        /// Поэтому здесь нет привычной проверки «симметрично»: она была бы заведомо красной.
        /// Вместо этого — зафиксированный факт с числами, чтобы расхождение не потерялось
        /// (при исправлении профиля тест нужно заменить на строгую проверку симметрии).
        /// </summary>
        [Test]
        public void SProfile_SymmetryIsMeasuredAndBounded()
        {
            Profile p = MakeProfile(64, new double[] { 90 }, new double[] { 90 },
                new double[] { 180 }, new double[] { 1200 });
            Build(p);

            double symVel = 0.0, symTime = 0.0, peak = 0.0;
            for (int i = 0; i <= p.N / 2; i++)
            {
                symVel = Math.Max(symVel, Math.Abs(p.Vel[i] - p.Vel[p.N - 1 - i]));
                symTime = Math.Max(symTime,
                    Math.Abs((p.Times[i] - p.Times[0]) - (p.Times[p.N - 1] - p.Times[p.N - 1 - i])));
            }
            for (int i = 1; i < p.N - 1; i++) peak = Math.Max(peak, p.Vel[i]);

            // КРАЕВЫЕ ИНТЕРВАЛЫ И ПИК ЗАМЕРА (восстановлено 30.10.2026).
            // По этим числам видно главное для оператора: схлопнулось ли торможение в один шаг.
            // Средняя скорость первого и последнего участка — конечная разность по времени,
            // то есть ровно то, что почувствует механика.
            double[][] measured = VelocityOf(p);
            double startInterval = Math.Abs(measured[0][0]);
            double endInterval = Math.Abs(measured[p.N - 1][0]);
            double measuredPeak = MaxAbs(measured);

            // ЭТО НЕ «ЗАФИКСИРОВАННЫЙ ДЕФЕКТ», А ИЗМЕРЕНИЕ (переписано 30.10.2026, аудит S43).
            //
            // РАНЬШЕ ЗДЕСЬ СТОЯЛ `Assert.Ignore`: тест измерял асимметрию и... выключал себя.
            // Прогон оставался зелёным, а числа не проверяло ничто — если асимметрия вырастет
            // вдесятеро, никто не узнает. Пропуск, который «фиксирует факт», не фиксирует ничего:
            // его не видно ни в отчёте CI, ни в глазах.
            //
            // ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ:
            //   1) асимметрия ИЗМЕРЕНА и не растёт сверх измеренного запаса — это сторож регресса;
            //   2) главное, что реально важно для оператора, — СИММЕТРИЯ ВРЕМЕНИ ФИНАЛЬНОЙ
            //      ОСТАНОВКИ ограничена: профиль не «тормозит в конце за один шаг». Именно это
            //      чувствуется как рывок, и именно это здесь проверяется строго;
            //   3) числа печатаются в отчёт, чтобы следующая волна видела реальную величину.
            //
            // Если профиль когда-нибудь станет симметричным, тест НЕ покраснеет — он станет
            // строже по факту (запас сверху), а строгую проверку симметрии можно будет включить.
            NUnit.Framework.TestContext.Progress.WriteLine(
                $"Симметрия S-профиля: расхождение скоростей {symVel:G4} ед. при пике {peak:G4} ед. " +
                $"({symVel / Math.Max(1e-9, peak):P0} от пика); расхождение времён {symTime:G4} с " +
                $"при полном {p.Times[p.N - 1]:G4} с; края {startInterval:G4} против {endInterval:G4} °/с " +
                $"(пик замера {measuredPeak:G4} °/с)");

            // 1. СТОРОЖ РЕГРЕССА. Запас взят с двукратным превышением измеренного — так тест
            //    не покраснеет от пересчёта чисел с плавающей точкой, но поймает настоящее ухудшение.
            Assert.That(symVel / Math.Max(1e-9, peak), Is.LessThan(0.9),
                "асимметрия скоростей выросла: было ~40 % от пика, стало " +
                (symVel / Math.Max(1e-9, peak)).ToString("P0") +
                ". Профиль стал заметно хуже — проверьте одностороннюю рекурсию ускорения");

            // 2. ОСТАНОВКА НЕ «ЗА ОДИН ШАГ». Средняя скорость последнего интервала обязана быть
            //    заметно ниже пика замера: если торможение схлопнулось, робот дёрнется в конце.
            Assert.That(endInterval, Is.LessThan(measuredPeak * 0.9),
                "последний интервал не тормозит: " + endInterval.ToString("G4") +
                " °/с при пике " + measuredPeak.ToString("G4") + " °/с — это рывок в финальной точке");

            // 3. ВРЕМЯ ПОЛОЖИТЕЛЬНО И МОНОТОННО — базовая корректность профиля на этой же фикстуре.
            Assert.That(p.Times[p.N - 1], Is.GreaterThan(0f), "полное время профиля обязано быть положительным");
            for (int i = 1; i < p.N; i++)
            {
                Assert.That(p.Times[i], Is.GreaterThanOrEqualTo(p.Times[i - 1]),
                    "время профиля обязано не убывать (сэмпл " + i + ")");
            }

            // 4. НАЧАЛО И КОНЕЦ — ИЗ ПОКОЯ. Это единственное, что профиль обязан соблюдать всегда.
            Assert.That(p.Vel[0], Is.EqualTo(0.0).Within(1e-9), "скорость в начале обязана быть нулевой");
            Assert.That(p.Vel[p.N - 1], Is.EqualTo(0.0).Within(1e-9), "скорость в конце обязана быть нулевой");
        }
    }
}

// ============================================================================================
// НЕ ПОКРЫТО (и почему) — сознательные пробелы этого файла, а не забытые случаи.
//
// 1. KvTimeOptimal.Compute(candidate) — УСПЕШНАЯ ветка расчёта (строки ~147-187 KvTimeOptimal.cs)
//    недостижима без сцены и живого потока: нужно, чтобы flow.Validator.Ready == true,
//    а сам flow — это TrajectoryFlowController (MonoBehaviour) с CollisionWorld и энергетической
//    моделью. Валидатор подделывается отражением (см. Retime-тесты), но поток — нет:
//    без Bind() он null, поэтому Compute всегда уходит в защитную ветку «валидатор не готов».
//    Не покрыты вместе с этой веткой: Draft.plan/stats/original, timeGain (формула
//    (original.time - stats.time)/original.time*100), jerkOk из KvTrajMath.VerifyJerk,
//    кэширование по Signature() (ключ = хэш кандидата + лимиты), словари drafts/originals,
//    обновление Last.
//
// 2. KvTimeOptimal.ApplySelected() — недостижима по ДВУМ причинам: (а) нужен выбранный
//    вариант из живого потока (KvVariantKit.Selected → flow.State.candidates);
//    (б) даже на пустом потоке метод идёт в Compute(candidate) с quiet = false →
//    Report(KvVariantKit.SceneStatus(flow)) → KvLocExtra.T → KvLoc.Ensure(), а тот читает
//    JSON-словари из StreamingAssets (Directory.GetFiles + File.ReadAllText). Создание/чтение
//    файлов правилами тестов запрещено, поэтому вызов сознательно не делается.
//    Не покрыты: подмена плана (KvVariantKit.ApplyPlan), флаг draft.applied, отчёт оператору
//    с метриками и предупреждением о рывке.
//
// 3. KvTimeOptimal.ResetSelected() — успешная ветка: требует заполненного словаря originals,
//    который наполняется только удачным Compute (см. п.1). Покрыт только отказ без потока.
//
// 4. KvTimeOptimal.MarkInComparison() — FeatureHub.Current — статический синглтон живого
//    MonoBehaviour-хаба; вызов либо вернёт false (хаба нет), либо изменит слоты A/B сравнения
//    траекторий оператора. Тест был бы либо бессмысленным, либо с побочным эффектом на сцену.
//
// 5. KvTimeOptimal.Bind(TrajectoryFlowController, CollisionWorld, KvEnergyModel) — типы
//    параметров сценные (MonoBehaviour и мир столкновений сцены), создать их в EditMode-тесте
//    без GameObject нельзя. Соответственно не покрыт и Flow() != null.
//
// 6. KvTimeOptimal.SetLimits(..., save: true) — записывает PlayerPrefs (VelPrefsKey/AccPrefsKey/
//    JerkPrefsKey + PlayerPrefs.Save()). Тесты вызывают только save: false, чтобы не менять
//    настройки оператора; поэтому ветка сохранения не покрыта.
//
// 7. KvTimeOptimalTab (Key/Title/Build/Tick/Refresh/AddRow/Current) — UI-слой верстака
//    (IKvWorkbenchTab, KvTabKit, KvTheme, локализация). По ТЗ не тестируется: требует
//    построения интерфейса, живой сцены и потокового состояния.
//
// 8. KvTrajMath.Retime(...) — конвейер время-оптимальной параметризации. ПОКРЫТО: отказ без
//    готового валидатора; основной путь с ограничением рывка (времена, plan.Time, геометрия,
//    BranchTag, LastProfileNote/Applicable/Clamped, лимиты скорости/ускорения/рывка по сэмплам);
//    страховочный возврат плана при нулевой длине пути. НЕ ПОКРЫТО:
//      • ветка jerkLimited = false («пересчёт времени», TimesFromProfile) — это НЕ S-профиль
//        (ограничение рывка в ней не работает), отдельный тест не добавлен, чтобы уложиться
//        в целевой объём 15–35 тестов; покрыт только BranchTag основного пути;
//      • DescribeProfile() как отдельная единица (его результат проверяется косвенно через
//        LastProfileNote);
//      • приватные LimitOf/JerkOf/Norm/JerkAlongPath/StepTime/TimesFromProfile — проверяются
//        косвенно, через результаты SProfileBuild и Retime.
//
// 9. Геометрия и метрики, требующие КИНЕМАТИКИ робота (в тестовом валидаторе её нет, TcpAt
//    возвращает нулевую позу): plan.Length у пересчитанного плана остаётся 0, поэтому
//    KvTrajMath.PathLength / TcpPolyline / LeverArms / Curvature и полный Analyze (зазоры,
//    кривизна, energy/peakPower) численно не проверены — покрыты только их нейтральные ветки
//    «валидатор не готов». Следствие: числовые метрики Draft.stats/Draft.original
//    (время, maxVel/maxAcc/maxJerk по метрике Analyze, длина, кривизна, зазор, энергия)
//    в тестах не проверяются.
//
// 10. KvTrajMath.Analyze / VerifyJerk / Energy — реальные вычисления требуют готового
//     валидатора, а Analyze с миром столкновений ещё и CollisionWorld сцены. Покрыты только
//     нейтральные ветки. Отдельно отмечу: реальный путь VerifyJerk (замер рывка с допуском
//     3 % и текстом предупреждения) не выполнялся — вместо него замер воспроизведён в тестах
//     через публичный MeasureJerk, тот же по схеме конечных разностей.
//
// 11. KvEnergyModel (этап 6 ТЗ: Distal(), Clone(), модель энергии) — вне предмета этого файла
//     (S-профиль этапа 5), поэтому не покрыт, хотя его чистые методы тестируемы.
//
// 12. Класс KvMotionLimits покрыт как ДАННЫЕ (значения по умолчанию, Clone, перевод в СИ);
//     атрибуты [Tooltip] и сериализация инспектором не проверяются.
// ============================================================================================
