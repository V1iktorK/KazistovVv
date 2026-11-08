// EditMode-тесты (Unity Test Framework, NUnit).
// Файл лежит в папке Editor, asmdef в проекте нет → компилируется в Assembly-CSharp-Editor.
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using KazistovVvFeatures;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>
    /// Тесты ЭНЕРГЕТИКИ (ЭТАП 6 ТЗ) для файла
    /// Assets/_Project/01_Scripts/Features/KvEnergyOptimal.cs: сервис
    /// <see cref="KvEnergyOptimal"/>, его вложенный DTO <see cref="KvEnergyOptimal.Draft"/>,
    /// модель энергии <see cref="KvEnergyModel"/> и ЯДРО расчёта <see cref="KvTrajMath.Energy"/> —
    /// ровно та функция, которую сервис вызывает в Compute() и CurrentEnergy().
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ (по ТЗ «расчёт энергии»):
    ///   • аналитический эталон формулы E = Σ |момент × угловая скорость| × dt, где момент
    ///     считается как инерция·α + вязкое трение·ω + удержание груза на плече;
    ///   • монотонности: масса груза ↑, путь ↑, ускорение ↑ → энергия ↑;
    ///   • нулевые и отрицательные входы: 0 Дж, отсутствие NaN и бесконечностей, отсутствие
    ///     деления на ноль при нулевых временах;
    ///   • экстремумы: 1000 кг, ход 1e5°, перемещение 1e-6°, ускорение 1e6 °/с²;
    ///   • аддитивность энергии по суставам, связь пиковой мощности со средней;
    ///   • контракт сервиса без сцены (Compute/ApplySelected/ResetSelected/CurrentEnergy/
    ///     MetricLine/PayloadKg/Draft) и семейство эко-профилей с лимитами движения.
    ///
    /// КАК ОБХОДИТСЯ ОТСУТСТВИЕ СЦЕНЫ (важно для чтения тестов):
    ///   GameObject, MonoBehaviour, корутины и UnityEditor в тестах запрещены, а
    ///   TrajectoryFlowController — MonoBehaviour. Поэтому Compute()/ApplySelected()/
    ///   ResetSelected() не могут выполнить «настоящий» расчёт: он требует
    ///   flow.Validator.Ready, а этот флаг выставляется только в PoseValidator.Init(RobotController),
    ///   то есть из сцены (приватный сеттер). Энергетика проверяется на ЧИСТОМ ядре
    ///   KvTrajMath.Energy с валидатором, которому флаг Ready выставлен отражением
    ///   (см. ReadyValidator): сама Energy() читает из валидатора ровно Ready и
    ///   IsPrismatic(j) и к геометрии (Transform) не обращается. Плечи удержания
    ///   подставляются массивом levers — без сцены геометрические плечи нулевые,
    ///   потому что TcpAt() возвращает ноль.
    ///
    /// НЕ ПОКРЫТО (с причиной):
    ///   1. Compute(candidate, quiet) целиком: кэш по Signature, перебор 20 профилей
    ///      (AccelScales × VelScales), выбор минимальной энергии, поля Draft.savings,
    ///      energyPerMeter, originalEnergy/PerMeter/PeakPower/Time, trials, applied —
    ///      всё это за гейтом flow.Validator.Ready, то есть нужно сцену с роботом.
    ///   2. ApplySelected()/ResetSelected() в УСПЕШНЫХ ветках (подмена плана варианта через
    ///      KvVariantKit.ApplyPlan, словарь originals): нужен тот же поток траекторий.
    ///      Покрыто только поведение «варианта нет → false».
    ///   3. CurrentEnergy()/MetricLine() с РЕАЛЬНЫМИ числами: при flow == null они возвращают
    ///      фиксированный ноль; проверен контракт вырожденного случая, а не расчёт.
    ///   4. KvEnergyTab (IKvWorkbenchTab) — вкладка UI-верстака (Build/Slider/Table): по ТЗ UI
    ///      не тестируется и требует KvTabKit из окна редактора.
    ///   5. Призматическая ось (SCARA z, IsPrismatic == true): ветка Energy() «сила × скорость»
    ///      недостижима — поле scara в PoseValidator приватно и заполняется только из сцены;
    ///      во всех тестах оси вращательные.
    ///   6. Связка «геометрия TCP → момент удержания» (LeverArms/TcpAt/KvVariantKit.RobotBase):
    ///      без сцены плечи нулевые, поэтому удержание задаётся массивом levers вручную.
    ///   7. KvTrajMath.Analyze / VerifyJerk / Smooth / Curvature — относятся к другим этапам ТЗ
    ///      (метрики, сглаживание, рывок), а не к расчёту энергии.
    ///   8. Лимиты МОМЕНТА и ТОКА: внутри Energy() их нет вообще — модель не знает ни
    ///      ограничения момента, ни КПД, ни тока (в ней только инерция, трение и удержание).
    ///      Ограничения скорости/ускорения/рывка живут в KvTrajMath.Retime, поэтому
    ///      «соблюдение лимитов» проверено на семействе эко-профилей (время и энергия),
    ///      а не как усечение момента в Energy().
    ///   9. Рекуперация: в модели НЕ вычитается (мощность берётся по модулю), генераторный
    ///      режим учитывается как расход — это зафиксировано тестом, а не исправлено.
    ///  10. PlayerPrefs: проверен только контракт ключа и клампа (SetUp/TearDown сохраняют и
    ///      возвращают прежнее значение); сама запись во внешнее хранилище — не логика энергии.
    ///
    /// ДЕТЕРМИНИРОВАННОСТЬ: нет ни времени, ни случайности, ни сцены; входы синтетические,
    /// утверждения аналитические (Within с запасом), состояние PlayerPrefs изолировано.
    /// </summary>
    [TestFixture]
        [Category(KvTestCategories.Fast)]
    public class KvEnergyOptimalTests
    {
        /// <summary>Градусы → радианы (в модели вращательные оси считаются в рад/с).</summary>
        private const float Deg2Rad = (float)(Math.PI / 180.0);

        /// <summary>Ускорение свободного падения в модели KvTrajMath.Energy.</summary>
        private const float Gravity = 9.81f;

        private bool hadPayloadPref;
        private float savedPayloadPref;

        // ============================================================== инфраструктура

        /// <summary>
        /// Масса груза хранится в PlayerPrefs под ключом KvEnergyOptimal.PayloadPrefsKey,
        /// поэтому тесты сохраняют прежнее значение и возвращают его обратно: прогон
        /// идемпотентен и не зависит от предыдущих запусков.
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            hadPayloadPref = PlayerPrefs.HasKey(KvEnergyOptimal.PayloadPrefsKey);
            savedPayloadPref = hadPayloadPref
                ? PlayerPrefs.GetFloat(KvEnergyOptimal.PayloadPrefsKey)
                : 0f;
            PlayerPrefs.DeleteKey(KvEnergyOptimal.PayloadPrefsKey);
        }

        /// <summary>Восстановление внешнего состояния (PlayerPrefs) после теста.</summary>
        [TearDown]
        public void TearDown()
        {
            if (hadPayloadPref) PlayerPrefs.SetFloat(KvEnergyOptimal.PayloadPrefsKey, savedPayloadPref);
            else PlayerPrefs.DeleteKey(KvEnergyOptimal.PayloadPrefsKey);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Валидатор позы в состоянии Ready — «пропуск» в KvTrajMath.Energy.
        ///
        /// ПОЧЕМУ ОТРАЖЕНИЕ: PoseValidator.Ready имеет ПРИВАТНЫЙ сеттер и выставляется только
        /// в Init(RobotController); RobotController — MonoBehaviour, ему нужны GameObject и
        /// Transform'ы сцены, которых в EditMode-тесте без GameObject взять негде. Отражение
        /// меняет флаг только у локального объекта теста: исходный код проекта не правится,
        /// файлы не создаются. Дальше Energy() — чистая арифметика: из валидатора читаются
        /// ровно Ready и IsPrismatic(j), геометрия не запрашивается.
        /// </summary>
        private static PoseValidator ReadyValidator()
        {
            PoseValidator validator = new PoseValidator();
            PropertyInfo ready = typeof(PoseValidator).GetProperty("Ready");
            Assert.That(ready, Is.Not.Null,
                "PoseValidator.Ready — публичное свойство с приватным сеттером");
            ready.SetValue(validator, true, null);
            Assert.That(validator.Ready, Is.True, "флаг готовности выставлен отражением");
            return validator;
        }

        /// <summary>План без метрик: только путь и времена (всё, что читает Energy()).</summary>
        private static PlannedTrajectory Plan(double[][] path, float[] times)
        {
            PlannedTrajectory plan = new PlannedTrajectory();
            plan.Path = path;
            plan.Times = times;
            plan.Label = "тест";
            return plan;
        }

        /// <summary>Равномерные времена: samples сэмплов, последний — на total секунде.</summary>
        private static float[] UniformTimes(int samples, float total)
        {
            float[] times = new float[samples];
            for (int i = 0; i < samples; i++)
                times[i] = samples > 1 ? total * i / (samples - 1) : 0f;
            return times;
        }

        /// <summary>Путь: линейно движется ТОЛЬКО сустав joint, остальные стоят в нуле.</summary>
        private static double[][] LinearPath(int dof, int samples, int joint, double from, double to)
        {
            double[][] path = new double[samples][];
            for (int i = 0; i < samples; i++)
            {
                path[i] = new double[dof];
                double u = samples > 1 ? (double)i / (samples - 1) : 0.0;
                path[i][joint] = from + (to - from) * u;
            }
            return path;
        }

        /// <summary>Путь с постоянным ускорением по суставу joint: q = a·t²/2 (градусы).</summary>
        private static double[][] AccelPath(int dof, int samples, int joint,
            double accelDegPerSec2, float[] times)
        {
            double[][] path = new double[samples][];
            for (int i = 0; i < samples; i++)
            {
                path[i] = new double[dof];
                double t = times[Math.Min(i, times.Length - 1)];
                path[i][joint] = 0.5 * accelDegPerSec2 * t * t;
            }
            return path;
        }

        /// <summary>Путь без движения: все сэмплы одинаковые (нулевые).</summary>
        private static double[][] StillPath(int dof, int samples)
        {
            return LinearPath(dof, samples, 0, 0.0, 0.0);
        }

        /// <summary>Модель робота с одинаковыми параметрами по всем осям.</summary>
        private static KvEnergyModel JointModel(int dof, float inertia, float friction,
            float distalMass, float payloadShare, float payloadKg)
        {
            KvEnergyModel model = new KvEnergyModel();
            model.payloadKg = payloadKg;
            model.distalMass = new float[dof];
            model.inertia = new float[dof];
            model.friction = new float[dof];
            model.payloadShare = new float[dof];
            for (int j = 0; j < dof; j++)
            {
                model.distalMass[j] = distalMass;
                model.inertia[j] = inertia;
                model.friction[j] = friction;
                model.payloadShare[j] = payloadShare;
            }
            return model;
        }

        /// <summary>Массив из n одинаковых значений (плечи или любая другая длина).</summary>
        private static float[] Filled(int n, float value)
        {
            float[] array = new float[n];
            for (int i = 0; i < n; i++) array[i] = value;
            return array;
        }

        /// <summary>Нулевые плечи: удержание в расчёт не входит (чистые инерция и трение).</summary>
        private static float[] ZeroLevers(int n)
        {
            return new float[n];
        }

        // ============================================================== KvEnergyOptimal: состояние, кэш

        /// <summary>Новый сервис: кэш пуст, поток траекторий не привязан, модель доступна.</summary>
        [Test]
        public void NewService_HasNoDraftsAndNoFlow()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            Assert.That(service.DraftCount, Is.EqualTo(0), "кэш черновиков пуст");
            Assert.That(service.Last, Is.Null, "последнего черновика нет");
            Assert.That(service.Flow(), Is.Null, "поток траекторий ещё не привязан");
            Assert.That(service.Model, Is.Not.Null, "модель энергии доступна сразу");
        }

        /// <summary>Инвалидация кэша на пустом сервисе идемпотентна и не бросает исключений.</summary>
        [Test]
        public void Invalidate_And_ResetCache_AreIdempotentOnEmptyService()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            Assert.DoesNotThrow(() => service.Invalidate());
            Assert.DoesNotThrow(() => service.ResetCache());
            Assert.DoesNotThrow(() => service.Invalidate());

            Assert.That(service.DraftCount, Is.EqualTo(0));
            Assert.That(service.Last, Is.Null);
        }

        /// <summary>
        /// Без сцены Compute() возвращает null и НЕ засоряет кэш: пустой кандидат, кандидат
        /// без плана и валидный кандидат без привязанного потока траекторий.
        /// </summary>
        [Test]
        public void Compute_WithoutScene_ReturnsNullAndCachesNothing()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();
            PlannedTrajectory plan = Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 1f));

            Assert.That(service.Compute(null, true), Is.Null, "null-кандидат");

            TrajectoryCandidate noPlan = new TrajectoryCandidate();
            Assert.That(service.Compute(noPlan, true), Is.Null, "кандидат без плана");

            TrajectoryCandidate candidate = new TrajectoryCandidate();
            candidate.plan = plan;
            Assert.That(service.Compute(candidate, true), Is.Null,
                "поток не привязан → расчёт невозможен (валидатор робота не готов)");
            Assert.That(service.Compute(candidate, true), Is.Null, "повторный вызов тоже null");

            Assert.That(service.DraftCount, Is.EqualTo(0), "в кэш ничего не попало");
            Assert.That(service.Last, Is.Null);
        }

        /// <summary>
        /// Причина отказа сообщается оператору через событие Message (ветка «валидатор робота
        /// не готов»). Это единственный тест, который намеренно идёт по «громкой» ветке
        /// (quiet = false), поэтому в консоль попадает одна строка Debug.Log.
        /// </summary>
        [Test]
        public void Compute_WithoutBoundFlow_ReportsReasonThroughMessageEvent()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();
            string reported = null;
            Action<string> handler = delegate (string text) { reported = text; };
            service.Message += handler;
            try
            {
                TrajectoryCandidate candidate = new TrajectoryCandidate();
                candidate.plan = Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 1f));

                KvEnergyOptimal.Draft draft = service.Compute(candidate, false);

                Assert.That(draft, Is.Null, "расчёт без валидатора невозможен");
                Assert.That(reported, Is.Not.Null, "событие Message должно сработать");
                Assert.That(reported, Is.Not.Empty, "сообщение не пустое");
                Assert.That(reported, Does.Contain("валидатор"), "в сообщении — причина отказа");
            }
            finally
            {
                service.Message -= handler;
            }
        }

        /// <summary>
        /// Без потока траекторий применять и возвращать эко-профиль нечего: сервис отвечает
        /// false и ничего не меняет в состоянии.
        /// </summary>
        [Test]
        public void ApplyAndResetSelected_WithoutBoundFlow_ReturnFalse()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            Assert.That(service.ApplySelected(), Is.False, "нет выбранного варианта");
            Assert.That(service.ResetSelected(), Is.False, "нечего возвращать");
            Assert.That(service.DraftCount, Is.EqualTo(0), "кэш не изменился");
        }

        /// <summary>Bind(null, ...) допустим: поток остаётся непривязанным, исключений нет.</summary>
        [Test]
        public void Bind_WithNullController_KeepsFlowNull()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            Assert.DoesNotThrow(() => service.Bind(null, null));
            Assert.DoesNotThrow(() => service.Bind(null, new KvMotionLimits()));
            Assert.That(service.Flow(), Is.Null);
        }

        /// <summary>
        /// CurrentEnergy без потока: 0 Дж и 0 Вт (гейт «валидатор робота не готов»),
        /// ни NaN, ни бесконечностей — метрика вырождается безопасно.
        /// </summary>
        [Test]
        public void CurrentEnergy_WithoutBoundFlow_ReturnsZero()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();
            TrajectoryCandidate candidate = new TrajectoryCandidate();
            candidate.plan = Plan(LinearPath(6, 5, 1, 0.0, 30.0), UniformTimes(5, 1f));

            float peak;
            float energy = service.CurrentEnergy(candidate, out peak);

            Assert.That(energy, Is.EqualTo(0f), "без сцены энергия неизвестна → 0");
            Assert.That(peak, Is.EqualTo(0f));
            Assert.That(float.IsNaN(energy), Is.False);
            Assert.That(float.IsInfinity(peak), Is.False);

            float peakNull;
            Assert.That(service.CurrentEnergy(null, out peakNull), Is.EqualTo(0f), "null-кандидат");
            Assert.That(peakNull, Is.EqualTo(0f));
        }

        /// <summary>
        /// MetricLine: «—» для пустого кандидата; без сцены — нулевая энергия, прочерк вместо
        /// удельной (длины нет) и единицы измерения в строке метрики.
        /// </summary>
        [Test]
        public void MetricLine_IsDashForEmptyCandidate_AndZeroWithoutFlow()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            Assert.That(service.MetricLine(null), Is.EqualTo("—"));

            TrajectoryCandidate noPlan = new TrajectoryCandidate();
            Assert.That(service.MetricLine(noPlan), Is.EqualTo("—"), "кандидат без плана");

            TrajectoryCandidate candidate = new TrajectoryCandidate();
            candidate.plan = Plan(LinearPath(6, 5, 1, 0.0, 30.0), UniformTimes(5, 1f));
            string line = service.MetricLine(candidate);

            Assert.That(line, Is.Not.Null);
            Assert.That(line, Is.Not.Empty);
            Assert.That(line, Does.StartWith("0"), "энергия без сцены нулевая");
            Assert.That(line, Does.Contain("Дж"), "единица энергии");
            Assert.That(line, Does.Contain("Дж/м"), "удельная энергия (без длины — прочерк)");
            Assert.That(line, Does.Contain("Вт"), "пиковая мощность");
            Assert.That(line, Does.Contain("кг"), "масса груза");
        }

        // ============================================================== масса груза (PayloadKg)

        /// <summary>Масса груза клампится в паспортный диапазон [0, 50] кг.</summary>
        [Test]
        public void PayloadKg_IsClampedToDocumentedRange()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            service.PayloadKg = -5f;
            Assert.That(service.PayloadKg, Is.EqualTo(0f), "отрицательная масса → 0 кг");

            service.PayloadKg = 100f;
            Assert.That(service.PayloadKg, Is.EqualTo(50f), "свыше 50 кг → 50 кг");

            service.PayloadKg = 12.5f;
            Assert.That(service.PayloadKg, Is.EqualTo(12.5f).Within(1e-4f));
        }

        /// <summary>
        /// Масса груза живёт в PlayerPrefs по ключу PayloadPrefsKey и подхватывается НОВЫМ
        /// экземпляром сервиса; кламп работает и при записи, и при чтении.
        /// </summary>
        [Test]
        public void PayloadKg_IsPersistedUnderDocumentedPrefsKey()
        {
            Assert.That(KvEnergyOptimal.PayloadPrefsKey, Is.EqualTo("KazistovVv.Post.PayloadKg"),
                "ключ настройки — часть контракта (его читают другие модули)");

            KvEnergyOptimal first = new KvEnergyOptimal();
            first.PayloadKg = 7.5f;

            KvEnergyOptimal second = new KvEnergyOptimal();
            Assert.That(second.PayloadKg, Is.EqualTo(7.5f).Within(1e-4f),
                "значение вернулось из внешнего хранилища");

            first.PayloadKg = 100f;
            Assert.That(new KvEnergyOptimal().PayloadKg, Is.EqualTo(50f), "кламп перед записью");

            PlayerPrefs.SetFloat(KvEnergyOptimal.PayloadPrefsKey, 80f);
            Assert.That(new KvEnergyOptimal().PayloadKg, Is.EqualTo(50f), "кламп при чтении (сверху)");

            PlayerPrefs.SetFloat(KvEnergyOptimal.PayloadPrefsKey, -3f);
            Assert.That(new KvEnergyOptimal().PayloadKg, Is.EqualTo(0f), "кламп при чтении (снизу)");
        }

        /// <summary>
        /// NaN-масса: исключений нет, значение не становится бесконечностью и не выходит за
        /// паспортный диапазон модели. ФАКТИЧЕСКОЕ ПОВЕДЕНИЕ: Mathf.Clamp сравнивает через
        /// «меньше/больше», поэтому NaN проходит кламп насквозь (и попадает в энергетику как
        /// NaN-масса) — утверждение сформулировано терпимо, чтобы не зависеть от версии Unity.
        /// </summary>
        [Test]
        public void PayloadKg_WithNaN_DoesNotThrowAndStaysBounded()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            Assert.DoesNotThrow(() => service.PayloadKg = float.NaN);

            float value = service.PayloadKg;
            Assert.That(float.IsInfinity(value), Is.False, "бесконечная масса недопустима");
            Assert.That(value, Is.NaN | Is.InRange(0f, 50f),
                "NaN (фактическое поведение клампа) либо значение в диапазоне [0, 50] кг");
        }

        /// <summary>
        /// Model — один и тот же кэшированный объект; физические параметры изменяемы напрямую,
        /// а PayloadKg читает то же поле (кламп стоит только на записи через свойство).
        /// </summary>
        [Test]
        public void Model_ReturnsSameInstanceAndKeepsPhysicalParameters()
        {
            KvEnergyOptimal service = new KvEnergyOptimal();

            KvEnergyModel model = service.Model;
            Assert.That(model, Is.Not.Null);
            Assert.That(service.Model, Is.SameAs(model), "модель кэшируется в сервисе");

            Assert.That(model.inertia.Length, Is.EqualTo(6), "шесть осей — как в спецификации");
            Assert.That(model.friction.Length, Is.EqualTo(6));
            Assert.That(model.distalMass.Length, Is.EqualTo(6));
            Assert.That(model.payloadShare.Length, Is.EqualTo(6));

            model.payloadKg = 3.5f;
            Assert.That(service.Model.payloadKg, Is.EqualTo(3.5f).Within(1e-4f),
                "параметры модели можно править напрямую");
            Assert.That(service.PayloadKg, Is.EqualTo(3.5f).Within(1e-4f),
                "PayloadKg читает то же поле: прямая запись в модель клампом не ограничена");
        }

        // ============================================================== черновик эко-профиля (Draft)

        /// <summary>
        /// Draft — обычный DTO: пустые значения по умолчанию, поля читаются и пишутся,
        /// экземпляры независимы (сервис хранит их по кандидату в словаре).
        /// </summary>
        [Test]
        public void Draft_DefaultsAreEmptyAndFieldsRoundTrip()
        {
            KvEnergyOptimal.Draft draft = new KvEnergyOptimal.Draft();

            Assert.That(draft.plan, Is.Null);
            Assert.That(draft.energy, Is.EqualTo(0f));
            Assert.That(draft.energyPerMeter, Is.EqualTo(0f));
            Assert.That(draft.peakPower, Is.EqualTo(0f));
            Assert.That(draft.time, Is.EqualTo(0f));
            Assert.That(draft.originalEnergy, Is.EqualTo(0f));
            Assert.That(draft.originalPerMeter, Is.EqualTo(0f));
            Assert.That(draft.originalPeakPower, Is.EqualTo(0f));
            Assert.That(draft.originalTime, Is.EqualTo(0f));
            Assert.That(draft.savings, Is.EqualTo(0f));
            Assert.That(draft.accelScale, Is.EqualTo(0f));
            Assert.That(draft.velScale, Is.EqualTo(0f));
            Assert.That(draft.key, Is.EqualTo(""), "key инициализируется пустой строкой, а не null");
            Assert.That(draft.applied, Is.False);
            Assert.That(draft.trials, Is.EqualTo(0));

            draft.energy = 12.5f;
            draft.savings = 30f;
            draft.accelScale = 0.45f;
            draft.velScale = 0.75f;
            draft.trials = 20;
            draft.applied = true;
            draft.key = "подпись";

            Assert.That(draft.energy, Is.EqualTo(12.5f).Within(1e-4f));
            Assert.That(draft.savings, Is.EqualTo(30f).Within(1e-4f));
            Assert.That(draft.accelScale, Is.EqualTo(0.45f).Within(1e-4f));
            Assert.That(draft.velScale, Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(draft.trials, Is.EqualTo(20));
            Assert.That(draft.applied, Is.True);
            Assert.That(draft.key, Is.EqualTo("подпись"));

            Assert.That(new KvEnergyOptimal.Draft().energy, Is.EqualTo(0f), "экземпляры независимы");
        }

        // ============================================================== модель энергии (KvEnergyModel)

        /// <summary>
        /// Паспортные параметры модели: шесть осей, положительные инерция, трение и масса;
        /// приводящаяся масса Distal зависит от груза только у осей, которые его держат.
        /// </summary>
        [Test]
        public void EnergyModel_DefaultsAndDistalMass_FollowDocumentedShares()
        {
            KvEnergyModel model = new KvEnergyModel();

            Assert.That(model.payloadKg, Is.EqualTo(1f).Within(1e-4f), "груз по умолчанию 1 кг");
            Assert.That(model.distalMass.Length, Is.EqualTo(6));
            Assert.That(model.inertia.Length, Is.EqualTo(6));
            Assert.That(model.friction.Length, Is.EqualTo(6));
            Assert.That(model.payloadShare.Length, Is.EqualTo(6));

            for (int j = 0; j < 6; j++)
            {
                Assert.That(model.inertia[j], Is.GreaterThan(0f), "инерция оси " + j);
                Assert.That(model.friction[j], Is.GreaterThan(0f), "трение оси " + j);
                Assert.That(model.distalMass[j], Is.GreaterThan(0f), "масса оси " + j);
            }

            Assert.That(model.payloadShare[0], Is.EqualTo(0f),
                "вертикальная ось базы груз не держит (share = 0)");
            Assert.That(model.Distal(0), Is.EqualTo(model.distalMass[0]).Within(1e-5f),
                "share = 0 → приводящаяся масса не зависит от груза");
            Assert.That(model.Distal(1),
                Is.EqualTo(model.distalMass[1] + 0.85f * model.payloadKg).Within(1e-4f),
                "share = 0.85 → груз добавляет 85 % своей массы");

            float baseMassOfZeroJoint = model.Distal(0);
            model.payloadKg = 10f;
            Assert.That(model.Distal(0), Is.EqualTo(baseMassOfZeroJoint).Within(1e-5f),
                "ось 0 груз не держит");
            Assert.That(model.Distal(1), Is.GreaterThan(baseMassOfZeroJoint), "ось 1 держит груз");
            Assert.That(model.Distal(1), Is.EqualTo(5f + 8.5f).Within(1e-3f),
                "5 кг звена + 0.85 × 10 кг груза");
        }

        /// <summary>
        /// Distal: отрицательный груз не «облегчает» звено (Mathf.Max(0, ...)), а индекс вне
        /// диапазона даёт безопасный фолбэк 1 кг вместо исключения.
        /// </summary>
        [Test]
        public void EnergyModel_Distal_IgnoresNegativePayloadAndFallsBackOutOfRange()
        {
            KvEnergyModel model = new KvEnergyModel();

            model.payloadKg = -25f;
            Assert.That(model.Distal(1), Is.EqualTo(model.distalMass[1]).Within(1e-4f),
                "Mathf.Max(0, payloadKg) → отрицательный груз эквивалентен нулевому");

            model.distalMass = null;
            model.payloadShare = null;
            Assert.That(model.Distal(3), Is.EqualTo(1f).Within(1e-4f), "фолбэк базовой массы");
            Assert.That(model.Distal(-1), Is.EqualTo(1f).Within(1e-4f), "индекс меньше нуля");
            Assert.That(model.Distal(99), Is.EqualTo(1f).Within(1e-4f), "индекс вне диапазона");
        }

        /// <summary>Clone — глубокая копия: правка копии не меняет оригинал.</summary>
        [Test]
        public void EnergyModel_CloneIsDeepCopy()
        {
            KvEnergyModel origin = new KvEnergyModel();
            origin.payloadKg = 4f;

            KvEnergyModel copy = origin.Clone();

            Assert.That(copy, Is.Not.SameAs(origin));
            Assert.That(copy.payloadKg, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(copy.inertia, Is.Not.SameAs(origin.inertia), "массивы скопированы");
            Assert.That(copy.inertia.Length, Is.EqualTo(origin.inertia.Length));
            for (int j = 0; j < origin.inertia.Length; j++)
            {
                Assert.That(copy.inertia[j], Is.EqualTo(origin.inertia[j]).Within(1e-6f));
                Assert.That(copy.friction[j], Is.EqualTo(origin.friction[j]).Within(1e-6f));
                Assert.That(copy.distalMass[j], Is.EqualTo(origin.distalMass[j]).Within(1e-6f));
                Assert.That(copy.payloadShare[j], Is.EqualTo(origin.payloadShare[j]).Within(1e-6f));
            }

            copy.inertia[0] = 99f;
            Assert.That(origin.inertia[0], Is.Not.EqualTo(99f).Within(1e-6f),
                "правка копии не влияет на оригинал");
        }

        // ============================================================== расчёт энергии (ядро Energy)

        /// <summary>
        /// АНАЛИТИЧЕСКИЙ ЭТАЛОН. Равномерное движение одного сустава 0 → 90° за 1 с при
        /// нулевой инерции и нулевом удержании: ускорение 0, момент равен трению × ω,
        /// поэтому E = b·ω²·T = 0.25 × (90 °/с в рад/с)² × 1 с ≈ 0.6169 Дж.
        /// </summary>
        [Test]
        public void Energy_LinearMotionWithFriction_MatchesAnalyticValue()
        {
            PoseValidator validator = ReadyValidator();
            PlannedTrajectory plan = Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 1f));
            KvEnergyModel model = JointModel(1, 0f, 0.25f, 0f, 0f, 0f);

            float peak;
            float energy = KvTrajMath.Energy(validator, plan, model, ZeroLevers(3), out peak);

            float omega = 90f * Deg2Rad;                    // рад/с
            float expected = 0.25f * omega * omega * 1f;    // Дж
            Assert.That(energy, Is.EqualTo(expected).Within(1e-3f), "E = b·ω²·T");
            Assert.That(energy, Is.EqualTo(0.6169f).Within(0.001f), "числовой эталон");
            Assert.That(peak, Is.EqualTo(expected).Within(1e-3f),
                "мощность постоянна → пик равен средней");
        }

        /// <summary>
        /// Нулевые входы: неподвижный путь при ненулевом моменте удержания даёт 0 Дж,
        /// «пустая» модель (без инерции, трения и массы) — тоже 0 Дж, слишком короткий путь
        /// вообще не считается. Ни NaN, ни бесконечностей, ни деления на ноль.
        /// </summary>
        [Test]
        public void Energy_ZeroInputs_ProduceZeroWithoutNaN()
        {
            PoseValidator validator = ReadyValidator();
            KvEnergyModel holding = JointModel(6, 1.4f, 0.9f, 7f, 0.85f, 12f);
            float peak;

            PlannedTrajectory still = Plan(StillPath(6, 4), UniformTimes(4, 1f));
            float stillEnergy = KvTrajMath.Energy(validator, still, holding, Filled(4, 0.5f), out peak);
            Assert.That(stillEnergy, Is.EqualTo(0f), "нет движения → нет работы");
            Assert.That(peak, Is.EqualTo(0f));
            Assert.That(float.IsNaN(stillEnergy), Is.False);

            PlannedTrajectory moving = Plan(LinearPath(6, 4, 1, 0.0, 90.0), UniformTimes(4, 1f));
            float emptyEnergy = KvTrajMath.Energy(validator, moving,
                JointModel(6, 0f, 0f, 0f, 0f, 0f), Filled(4, 0.5f), out peak);
            Assert.That(emptyEnergy, Is.EqualTo(0f), "нулевая модель ничего не потребляет");
            Assert.That(peak, Is.EqualTo(0f));
            Assert.That(float.IsInfinity(emptyEnergy), Is.False);

            PlannedTrajectory single = Plan(LinearPath(6, 1, 1, 0.0, 90.0), null);
            float shortEnergy = KvTrajMath.Energy(validator, single, holding, ZeroLevers(1), out peak);
            Assert.That(shortEnergy, Is.EqualTo(0f), "один сэмпл — это не траектория");
            Assert.That(peak, Is.EqualTo(0f));
        }

        /// <summary>
        /// Нулевое время: EnsureTimes() ЧИНИТ массив времён (i/(n−1), Time = 1 с), а не делит
        /// на ноль; результат совпадает с планом, у которого времён нет вовсе.
        /// </summary>
        [Test]
        public void Energy_ZeroTimes_AreRebuiltWithoutDivisionByZero()
        {
            PoseValidator validator = ReadyValidator();
            KvEnergyModel model = JointModel(1, 0f, 0.25f, 0f, 0f, 0f);

            PlannedTrajectory zeroTimes = Plan(LinearPath(1, 3, 0, 0.0, 90.0), new float[] { 0f, 0f, 0f });
            float peakZero;
            float zeroEnergy = KvTrajMath.Energy(validator, zeroTimes, model, ZeroLevers(3), out peakZero);

            Assert.That(zeroTimes.Times[0], Is.EqualTo(0f), "времена пересобраны заново");
            Assert.That(zeroTimes.Times[2], Is.EqualTo(1f));
            Assert.That(zeroTimes.Time, Is.EqualTo(1.0).Within(1e-6), "Time синхронизировано с Times");

            PlannedTrajectory noTimes = Plan(LinearPath(1, 3, 0, 0.0, 90.0), null);
            float peakNone;
            float noneEnergy = KvTrajMath.Energy(validator, noTimes, model, ZeroLevers(3), out peakNone);

            Assert.That(zeroEnergy, Is.EqualTo(noneEnergy).Within(1e-4f),
                "нулевые времена эквивалентны временам по умолчанию");
            Assert.That(zeroEnergy, Is.GreaterThan(0f), "движение за 1 с стоит энергии");
            Assert.That(float.IsNaN(zeroEnergy), Is.False);
            Assert.That(peakZero, Is.EqualTo(peakNone).Within(1e-4f));
        }

        /// <summary>
        /// Чистое вязкое трение: E = b·ω²·T, поэтому растяжение времени (мягкий профиль)
        /// уменьшает энергию ровно пропорционально — это и есть физическая основа эко-профиля.
        /// </summary>
        [Test]
        public void Energy_ScalesInverselyWithTime_ForPureFriction()
        {
            PoseValidator validator = ReadyValidator();
            KvEnergyModel model = JointModel(1, 0f, 0.25f, 0f, 0f, 0f);

            float peakFast, peakSlow;
            float fast = KvTrajMath.Energy(validator,
                Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 1f)),
                model, ZeroLevers(3), out peakFast);
            float slow = KvTrajMath.Energy(validator,
                Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 2f)),
                model, ZeroLevers(3), out peakSlow);

            Assert.That(fast, Is.GreaterThan(0f));
            Assert.That(slow, Is.EqualTo(fast * 0.5f).Within(fast * 0.01f),
                "вдвое медленнее → вдвое дешевле");
            Assert.That(slow, Is.LessThan(fast));
            Assert.That(peakSlow, Is.LessThan(peakFast), "и пиковая мощность тоже ниже");
        }

        /// <summary>
        /// Чистая инерция: E = I·α·ω·T, а ускорение и скорость пропорциональны заданному
        /// ускорению пути, поэтому удвоение ускорения учетверяет энергию (E ∝ A²).
        /// </summary>
        [Test]
        public void Energy_ScalesQuadraticallyWithAcceleration_ForPureInertia()
        {
            PoseValidator validator = ReadyValidator();
            KvEnergyModel model = JointModel(1, 0.5f, 0f, 0f, 0f, 0f);
            float[] times = UniformTimes(3, 1f);

            float peak;
            float slow = KvTrajMath.Energy(validator,
                Plan(AccelPath(1, 3, 0, 100.0, times), times), model, ZeroLevers(3), out peak);
            float fast = KvTrajMath.Energy(validator,
                Plan(AccelPath(1, 3, 0, 200.0, times), times), model, ZeroLevers(3), out peak);

            Assert.That(slow, Is.GreaterThan(0f));
            Assert.That(fast, Is.EqualTo(slow * 4f).Within(slow * 0.02f), "E ∝ A²");
            Assert.That(fast, Is.GreaterThan(slow * 3f), "запас: больше ускорение → больше энергия");
        }

        /// <summary>
        /// Больше путь → больше энергия: при чистом трении E ∝ (перемещение)², поэтому
        /// удвоение хода даёт четырёхкратную энергию (проверка с запасом).
        /// </summary>
        [Test]
        public void Energy_GrowsWithTravelDistance()
        {
            PoseValidator validator = ReadyValidator();
            KvEnergyModel model = JointModel(1, 0f, 0.25f, 0f, 0f, 0f);

            float peak;
            float shortMove = KvTrajMath.Energy(validator,
                Plan(LinearPath(1, 3, 0, 0.0, 45.0), UniformTimes(3, 1f)),
                model, ZeroLevers(3), out peak);
            float longMove = KvTrajMath.Energy(validator,
                Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 1f)),
                model, ZeroLevers(3), out peak);

            Assert.That(longMove, Is.GreaterThan(shortMove * 3f), "запас вместо строгого равенства");
            Assert.That(longMove, Is.EqualTo(shortMove * 4f).Within(shortMove * 0.02f), "E ∝ S²");
        }

        /// <summary>
        /// Больше масса груза → больше энергия: у оси, которая держит груз (share = 0.9),
        /// энергия линейна по массе, поэтому 0 → 20 кг даёт ровно (5 + 0.85·20)/(5 + 0) = 4.4 раза.
        /// </summary>
        [Test]
        public void Energy_GrowsWithPayloadMass_LinearlyInHoldingJoint()
        {
            PoseValidator validator = ReadyValidator();
            float[] levers = Filled(3, 0.5f);
            float[] times = UniformTimes(3, 1f);
            double[][] path = LinearPath(6, 3, 1, 0.0, 30.0);   // движется ось 1 (share = 0.9)

            float peak;
            float empty = KvTrajMath.Energy(validator, Plan(path, times),
                JointModel(6, 0f, 0f, 5f, 0.85f, 0f), levers, out peak);
            float loaded = KvTrajMath.Energy(validator, Plan(path, times),
                JointModel(6, 0f, 0f, 5f, 0.85f, 20f), levers, out peak);

            Assert.That(empty, Is.GreaterThan(0f), "удержание даёт энергию даже без груза");
            Assert.That(loaded, Is.GreaterThan(empty * 2f), "запас: 20 кг заметно дороже");
            Assert.That(loaded, Is.EqualTo(empty * 4.4f).Within(empty * 0.05f),
                "линейный закон по массе груза");
            Assert.That(float.IsInfinity(loaded), Is.False);
        }

        /// <summary>
        /// Отрицательное направление (движение «вниз» по моменту удержания): исключений нет,
        /// энергия НЕ отрицательная. ФАКТИЧЕСКОЕ ПОВЕДЕНИЕ МОДЕЛИ: берётся |момент × скорость|,
        /// поэтому генераторный режим (момент и скорость разных знаков) не вычитается из
        /// энергозатрат — рекуперация в этой модели не моделируется, а считается расходом.
        /// </summary>
        [Test]
        public void Energy_NegativeMotion_IsNonNegative_AndRegenerationIsNotSubtracted()
        {
            PoseValidator validator = ReadyValidator();
            float[] levers = Filled(3, 0.5f);
            float[] times = UniformTimes(3, 1f);
            KvEnergyModel model = JointModel(6, 0f, 0.7f, 5f, 0.85f, 0f);

            float peakDown, peakUp;
            float down = KvTrajMath.Energy(validator,
                Plan(LinearPath(6, 3, 1, 0.0, -30.0), times), model, levers, out peakDown);
            float up = KvTrajMath.Energy(validator,
                Plan(LinearPath(6, 3, 1, 0.0, 30.0), times), model, levers, out peakUp);

            float gravity = 5f * Gravity * 0.5f * 0.9f;   // Н·м: масса звена × плечо × share оси 1
            float omega = 30f * Deg2Rad;                  // рад/с (модуль)
            Assert.That(down, Is.GreaterThan(0f), "энергия не отрицательна (Math.Abs)");
            Assert.That(down, Is.EqualTo((gravity - 0.7f * omega) * omega).Within(0.02f),
                "E = |удержание − трение·ω|·ω·T: удержание частично «везёт» сустав вниз");
            Assert.That(down, Is.LessThan(up), "вниз дешевле, чем вверх");
            Assert.That(peakDown, Is.LessThan(peakUp));
            Assert.That(float.IsNaN(down), Is.False);
        }

        /// <summary>
        /// Быстрый спуск: момент удержания «везёт» привод, поэтому энергия спуска в разы
        /// меньше энергии подъёма — |g − b·|ω||·|ω| против (g + b·|ω|)·|ω| при том же ходе.
        /// </summary>
        [Test]
        public void Energy_GravityAssistedDownwardMotion_IsCheaperThanUpward()
        {
            PoseValidator validator = ReadyValidator();
            float[] levers = Filled(3, 0.5f);
            float[] times = UniformTimes(3, 1f);
            KvEnergyModel model = JointModel(6, 0f, 0.7f, 5f, 0.85f, 0f);

            float peakUp, peakDown;
            float up = KvTrajMath.Energy(validator,
                Plan(LinearPath(6, 3, 1, 0.0, 3000.0), times), model, levers, out peakUp);
            float down = KvTrajMath.Energy(validator,
                Plan(LinearPath(6, 3, 1, 0.0, -3000.0), times), model, levers, out peakDown);

            Assert.That(up, Is.GreaterThan(0f));
            Assert.That(down, Is.GreaterThan(0f), "«помощь» гравитации не делает энергию отрицательной");
            Assert.That(down, Is.LessThan(up * 0.5f), "запас: спуск дешевле более чем вдвое");
            Assert.That(peakDown, Is.LessThan(peakUp));
        }

        /// <summary>
        /// Аддитивность по суставам: E системы = Σ E_j, потому что скорость и ускорение каждого
        /// сустава считаются независимо, а мощности складываются по модулю; пик системы не ниже
        /// пика любой отдельной оси.
        /// </summary>
        [Test]
        public void Energy_IsAdditiveOverJoints()
        {
            PoseValidator validator = ReadyValidator();
            const int dof = 6;
            const int samples = 5;
            float[] times = UniformTimes(samples, 1f);
            float[] levers = Filled(samples, 0.35f);
            KvEnergyModel model = new KvEnergyModel();
            model.payloadKg = 3f;

            double[][] all = new double[samples][];
            for (int i = 0; i < samples; i++)
            {
                all[i] = new double[dof];
                for (int j = 0; j < dof; j++)
                    all[i][j] = 10.0 * (j + 1) * i / (samples - 1);
            }

            float peakAll;
            float total = KvTrajMath.Energy(validator, Plan(all, times), model, levers, out peakAll);

            float sum = 0f;
            float maxSingle = 0f;
            for (int j = 0; j < dof; j++)
            {
                float peakSingle;
                float part = KvTrajMath.Energy(validator,
                    Plan(LinearPath(dof, samples, j, 0.0, 10.0 * (j + 1)), times),
                    model, levers, out peakSingle);
                sum += part;
                if (part > maxSingle) maxSingle = part;
            }

            Assert.That(total, Is.GreaterThan(0f));
            Assert.That(total, Is.EqualTo(sum).Within(0.02f), "энергия системы = сумма по суставам");
            Assert.That(peakAll, Is.GreaterThanOrEqualTo(maxSingle), "пик системы ≥ пика каждой оси");
        }

        /// <summary>
        /// Пиковая мощность не ниже средней: пик — максимум мгновенной мощности, а E/T — её
        /// средневзвешенное значение (интервалы dt в сумме дают ровно T).
        /// </summary>
        [Test]
        public void Energy_PeakPower_IsNotBelowAveragePower()
        {
            PoseValidator validator = ReadyValidator();
            float[] times = UniformTimes(3, 1f);
            float total = times[times.Length - 1];
            KvEnergyModel model = JointModel(1, 1.4f, 0.9f, 0f, 0f, 0f);

            float peak;
            float energy = KvTrajMath.Energy(validator,
                Plan(AccelPath(1, 3, 0, 240.0, times), times), model, ZeroLevers(3), out peak);

            Assert.That(peak, Is.GreaterThan(0f));
            Assert.That(peak, Is.GreaterThanOrEqualTo(energy / total * 0.999f),
                "средняя мощность за время движения не выше пиковой");
        }

        /// <summary>
        /// Экстремальные, но мыслимые значения: масса 1000 кг (прямая запись в модель клампом
        /// не ограничена) и ход 1e5° дают большие, но КОНЕЧНЫЕ числа — без ∞ и NaN — и
        /// ожидаемый рост энергии относительно базового варианта.
        /// </summary>
        [Test]
        public void Energy_ExtremeMassAndLongPath_StayFiniteAndGrow()
        {
            PoseValidator validator = ReadyValidator();
            float[] levers = Filled(3, 0.5f);
            float[] times = UniformTimes(3, 1f);
            double[][] path = LinearPath(6, 3, 1, 0.0, 30.0);

            float peak;
            float baseEnergy = KvTrajMath.Energy(validator, Plan(path, times),
                JointModel(6, 0f, 0.7f, 5f, 0.85f, 0f), levers, out peak);
            float heavy = KvTrajMath.Energy(validator, Plan(path, times),
                JointModel(6, 0f, 0.7f, 5f, 0.85f, 1000f), levers, out peak);
            float longPath = KvTrajMath.Energy(validator,
                Plan(LinearPath(6, 3, 1, 0.0, 1e5), times),
                JointModel(6, 0f, 0.7f, 5f, 0.85f, 0f), levers, out peak);

            Assert.That(baseEnergy, Is.GreaterThan(0f));
            Assert.That(float.IsNaN(heavy), Is.False);
            Assert.That(float.IsInfinity(heavy), Is.False);
            Assert.That(heavy, Is.GreaterThan(baseEnergy * 50f), "855 кг на плече 0.5 м");

            Assert.That(float.IsNaN(longPath), Is.False);
            Assert.That(float.IsInfinity(longPath), Is.False);
            Assert.That(longPath, Is.GreaterThan(baseEnergy * 100f), "ход 1e5° за то же время");
        }

        /// <summary>
        /// Экстремально малые и большие входы: перемещение 1e-6° даёт микроскопическую, но
        /// конечную энергию, а ускорение 1e6 °/с² — большую и тоже конечную (переполнения нет).
        /// </summary>
        [Test]
        public void Energy_VerySmallAndVeryFastMotion_StayFinite()
        {
            PoseValidator validator = ReadyValidator();
            float[] times = UniformTimes(3, 1f);
            KvEnergyModel model = JointModel(1, 1.4f, 0.7f, 0f, 0f, 0f);

            float peak;
            float tiny = KvTrajMath.Energy(validator,
                Plan(LinearPath(1, 3, 0, 0.0, 1e-6), times), model, ZeroLevers(3), out peak);

            Assert.That(tiny, Is.GreaterThanOrEqualTo(0f));
            Assert.That(float.IsNaN(tiny), Is.False);
            Assert.That(tiny, Is.LessThan(1e-12f), "микроперемещение почти ничего не стоит");

            float fast = KvTrajMath.Energy(validator,
                Plan(AccelPath(1, 3, 0, 1e6, times), times), model, ZeroLevers(3), out peak);

            Assert.That(float.IsNaN(fast), Is.False);
            Assert.That(float.IsInfinity(fast), Is.False);
            Assert.That(fast, Is.GreaterThan(1e4f), "ускорение 1e6 °/с² — сотни мегаджоулей");
            Assert.That(peak, Is.GreaterThanOrEqualTo(fast / times[2] * 0.999f));
        }

        /// <summary>
        /// null-модель заменяется моделью по умолчанию (инерция 1.4, трение 0.9 для первой оси):
        /// результат обязан совпасть с явно созданной KvEnergyModel() и равен b·ω²·T ≈ 2.22 Дж.
        /// </summary>
        [Test]
        public void Energy_NullModel_FallsBackToDefaults()
        {
            PoseValidator validator = ReadyValidator();
            PlannedTrajectory plan = Plan(LinearPath(1, 3, 0, 0.0, 90.0), UniformTimes(3, 1f));

            float peakNull, peakExplicit;
            float withNull = KvTrajMath.Energy(validator, plan, null, ZeroLevers(3), out peakNull);
            float withModel = KvTrajMath.Energy(validator, plan, new KvEnergyModel(),
                ZeroLevers(3), out peakExplicit);

            Assert.That(withNull, Is.EqualTo(withModel).Within(1e-4f), "null → модель по умолчанию");
            Assert.That(withNull, Is.EqualTo(2.2207f).Within(0.001f),
                "трение 0.9 Н·м·с/рад и ω = 90 °/с дают E = 0.9·ω²·1 с");
            Assert.That(peakNull, Is.EqualTo(peakExplicit).Within(1e-4f));
        }

        /// <summary>
        /// Границы: без валидатора, без плана, без пути и с одним сэмплом расчёт даёт 0 Дж и
        /// 0 Вт без исключений. Здесь же зафиксировано, почему тестам нужен ReadyValidator:
        /// PoseValidator вне Init(RobotController) не готов (нужна сцена), а перегрузка с
        /// Vector3 без сцены даёт нулевые плечи — удержание в такой расчёт не входит.
        /// </summary>
        [Test]
        public void Energy_Guards_ReturnZeroWithoutReadyValidatorOrUsablePlan()
        {
            PlannedTrajectory plan = Plan(LinearPath(6, 5, 1, 0.0, 30.0), UniformTimes(5, 1f));
            KvEnergyModel model = new KvEnergyModel();
            float peak;

            Assert.That(KvTrajMath.Energy(null, plan, model, ZeroLevers(5), out peak), Is.EqualTo(0f),
                "нет валидатора");
            Assert.That(peak, Is.EqualTo(0f));

            PoseValidator cold = new PoseValidator();
            Assert.That(cold.Ready, Is.False,
                "вне Init(RobotController) валидатор не готов — ему нужна сцена");
            Assert.That(KvTrajMath.Energy(cold, plan, model, ZeroLevers(5), out peak), Is.EqualTo(0f),
                "валидатор не готов → 0 Дж");

            Assert.That(KvTrajMath.Energy(ReadyValidator(), null, model, ZeroLevers(5), out peak),
                Is.EqualTo(0f), "нет плана");
            Assert.That(KvTrajMath.Energy(ReadyValidator(), Plan(null, null), model, (float[])null, out peak),
                Is.EqualTo(0f), "нет пути и нет плеч");

            float peakGeometry;
            float viaBase = KvTrajMath.Energy(ReadyValidator(), plan, model, Vector3.zero, out peakGeometry);
            float viaZeroLevers = KvTrajMath.Energy(ReadyValidator(), plan, model, ZeroLevers(5), out peak);
            Assert.That(viaBase, Is.EqualTo(viaZeroLevers).Within(1e-4f),
                "без сцены геометрические плечи нулевые — удержание не учитывается");
        }

        // ============================================================== эко-профиль и лимиты движения

        /// <summary>
        /// Мягкий эко-профиль (меньшие доли лимитов ускорения и скорости) занимает заметно
        /// больше времени — именно этот перебор делает KvEnergyOptimal.Compute; времена
        /// профиля строго монотонны и начинаются с покоя.
        /// </summary>
        [Test]
        public void Retime_SofterScales_ProduceLongerTime()
        {
            PoseValidator validator = ReadyValidator();
            PlannedTrajectory source = Plan(LinearPath(6, 21, 1, 0.0, 45.0), UniformTimes(21, 1f));
            KvMotionLimits limits = new KvMotionLimits();

            PlannedTrajectory hard = KvTrajMath.Retime(validator, source, limits, 1.00f, 1.00f, "жёсткий");
            PlannedTrajectory soft = KvTrajMath.Retime(validator, source, limits, 0.30f, 0.55f, "мягкий");

            Assert.That(hard, Is.Not.Null, "планировщик дал план");
            Assert.That(soft, Is.Not.Null);
            Assert.That(hard.Time, Is.GreaterThan(0.0));
            Assert.That(soft.Time, Is.GreaterThan(hard.Time * 1.5), "мягче профиль → дольше движение");
            Assert.That(hard.Times[0], Is.EqualTo(0f), "профиль стартует из состояния покоя");
            Assert.That(hard.Times[hard.Times.Length - 1], Is.EqualTo((float)hard.Time).Within(1e-4f),
                "последний сэмпл = полное время движения");
        }

        /// <summary>
        /// Семейство эко-профилей (те же доли лимитов, что в приватных таблицах AccelScales и
        /// VelScales класса) действительно меняет энергию: для модели без инерции и удержания
        /// (E = b·Σω²·dt) самый мягкий профиль не дороже самого жёсткого. Сам ВЫБОР минимума
        /// живёт в приватном цикле Compute и без сцены недостижим — здесь проверена его физическая
        /// предпосылка, а не сам argmin.
        /// </summary>
        [Test]
        public void EcoProfileFamily_SofterProfileIsNotMoreExpensive()
        {
            PoseValidator validator = ReadyValidator();
            float[] accelScales = { 0.30f, 0.45f, 0.65f, 0.85f, 1.00f };
            float[] velScales = { 0.55f, 0.75f, 0.90f, 1.00f };
            PlannedTrajectory source = Plan(LinearPath(6, 21, 1, 0.0, 45.0), UniformTimes(21, 1f));
            KvMotionLimits limits = new KvMotionLimits();
            KvEnergyModel model = JointModel(6, 0f, 0.5f, 0f, 0f, 0f);
            float[] levers = ZeroLevers(21);

            float peak;
            float min = float.MaxValue;
            float max = 0f;
            float hardest = 0f;
            float softest = 0f;
            for (int a = 0; a < accelScales.Length; a++)
            {
                for (int s = 0; s < velScales.Length; s++)
                {
                    PlannedTrajectory trial = KvTrajMath.Retime(validator, source, limits,
                        accelScales[a], velScales[s], "проба");
                    Assert.That(trial, Is.Not.Null,
                        "профиль " + accelScales[a] + " / " + velScales[s] + " построен");

                    float energy = KvTrajMath.Energy(validator, trial, model, levers, out peak);
                    if (energy < min) min = energy;
                    if (energy > max) max = energy;
                    if (a == 0 && s == 0) softest = energy;
                    if (a == accelScales.Length - 1 && s == velScales.Length - 1) hardest = energy;
                }
            }

            Assert.That(min, Is.GreaterThan(0f), "движение есть → энергия положительна");
            Assert.That(max, Is.GreaterThan(min * 1.05f), "семейство реально меняет энергию");
            Assert.That(softest, Is.LessThan(hardest * 1.05f),
                "самый мягкий профиль не дороже самого жёсткого (запас 5 %)");
        }

        /// <summary>
        /// Retime терпим к null-лимитам (берёт KvMotionLimits по умолчанию, времена строго
        /// монотонны), но без готового валидатора и без исходного плана возвращает null —
        /// это причина, по которой эко-профиль нельзя посчитать в EditMode без сцены.
        /// </summary>
        [Test]
        public void Retime_NullLimitsUseDefaults_AndNotReadyValidatorGivesNull()
        {
            PoseValidator validator = ReadyValidator();
            PlannedTrajectory source = Plan(LinearPath(6, 21, 1, 0.0, 45.0), UniformTimes(21, 1f));

            PlannedTrajectory plan = KvTrajMath.Retime(validator, source, null);
            Assert.That(plan, Is.Not.Null, "null-лимиты = лимиты по умолчанию");
            Assert.That(plan.Times, Is.Not.Null);
            Assert.That(plan.Times.Length, Is.EqualTo(source.Path.Length), "число времён = число сэмплов");
            for (int i = 1; i < plan.Times.Length; i++)
            {
                Assert.That(plan.Times[i], Is.GreaterThan(plan.Times[i - 1]),
                    "время сэмплов строго растёт (сэмпл " + i + ")");
            }

            Assert.That(KvTrajMath.Retime(new PoseValidator(), source, null), Is.Null,
                "без готового валидатора эко-профиль не строится (нужна сцена)");
            Assert.That(KvTrajMath.Retime(validator, null, null), Is.Null, "нет исходного плана");
        }

        /// <summary>
        /// Лимиты движения — обычный DTO с паспортными значениями; Clone копирует их
        /// независимо (мягкие эко-профили строятся масштабированием именно этих лимитов).
        /// </summary>
        [Test]
        public void MotionLimits_DefaultsAndCloneAreIndependent()
        {
            KvMotionLimits limits = new KvMotionLimits();

            Assert.That(limits.maxVelDeg, Is.EqualTo(90f).Within(1e-4f));
            Assert.That(limits.maxAccDeg, Is.EqualTo(180f).Within(1e-4f));
            Assert.That(limits.maxJerkDeg, Is.EqualTo(1200f).Within(1e-4f));
            Assert.That(limits.maxVelMps, Is.GreaterThan(0f));
            Assert.That(limits.maxAccMps2, Is.GreaterThan(0f));
            Assert.That(limits.maxJerkMps3, Is.GreaterThan(0f));

            KvMotionLimits copy = limits.Clone();
            Assert.That(copy, Is.Not.SameAs(limits));
            Assert.That(copy.maxVelDeg, Is.EqualTo(limits.maxVelDeg).Within(1e-6f));

            copy.maxVelDeg = 12f;
            Assert.That(limits.maxVelDeg, Is.EqualTo(90f).Within(1e-4f), "копия независима");
        }
    }
}

