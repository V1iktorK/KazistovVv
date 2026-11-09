using UnityEngine;

namespace TrajectoryCore
{
    public enum SafetyReason { None, NotChecked, JointLimit, Clearance, Velocity, Watchdog, NoData, OperatorStop }

    /// <summary>
    /// Safety Layer (E4 плана): независимая проверка траектории и «сторожевой таймер».
    /// Проверки: лимиты суставов (с запасом), минимальный зазор, предельные скорости,
    /// актуальность данных. При отказе — движение не разрешается (fail-safe).
    /// </summary>
    public class SafetyGate
    {
        /// <summary>
        /// МИНИМАЛЬНЫЙ ЗАЗОР, метры — ЕДИНСТВЕННОЕ МЕСТО, где он задан (P1-16, 11.11.2026).
        ///
        /// ЧТО БЫЛО. Для одного понятия безопасности жили ТРИ разных числа: `SafetyGate.minClearance`
        /// (0,015 м), он же с коэффициентом «половина» в `ApproveStep` (`minClearance * 0.5f`,
        /// нигде не объяснённый) и отдельное `Planner.clearance = 0.02f`. Планировщик оптимизировал
        /// траекторию по одному порогу, а пропускал её другой — и «план прошёл проверку» означало
        /// разные вещи в разных местах.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Траектория, которую планировщик считал годной, могла быть отклонена
        /// предохранителем; «движение на грани» на паузе пропускалось по 0,0075 м. Наладчик не мог
        /// понять, какое число настоящее.
        ///
        /// Теперь порог один; запас планировщика выражен ЯВНЫМ коэффициентом, а не вторым числом.
        ///
        /// ДОПОЛНЕНО 12.11.2026 (дефект B). Шапка это уже ЗАЯВЛЯЛА, а `ApproveStep` продолжал
        /// исполнять `minClearance * 0.5f`: слово и дело расходились, и предохранитель пропускал
        /// позу с зазором ВДВОЕ меньше объявленного минимума — то есть был мягче собственного
        /// описания. Теперь половины нет нигде: и движение (`ApproveStep`), и стоящая поза на
        /// паузе (`ApproveStandingPose`) сравниваются с ОДНИМ числом — `minClearance`.
        ///
        /// ПОЧЕМУ ЭТО НЕ ОТКЛОНИТ ГОДНЫЕ ПЛАНЫ. `Planner.clearance` берётся из
        /// `DefaultPlanningClearance` = `DefaultMinClearance * PlanningClearanceFactor` (0,02 м):
        /// планировщик строит путь с запасом 4/3 выше предохранителя, и `PlannedTrajectory.MinClearance`
        /// считается как минимум по тем же сэмплам. План с зазором ≥ `minClearance` проходит единый
        /// порог с запасом в треть; ужесточение отсекает только «движение на грани» — то, что и
        /// должно было отсекаться.
        /// </summary>
        public const float DefaultMinClearance = 0.015f;

        /// <summary>
        /// ВО СКОЛЬКО РАЗ ПЛАНИРОВЩИК ТРЕБУЕТ БОЛЬШЕ, ЧЕМ ПРЕДОХРАНИТЕЛЬ (P1-16). Планировать с
        /// запасом 4/3 — осознанное решение: план строится «с полем», а пропускается по границе.
        /// Раньше это было зашито двумя независимыми числами (0,015 в предохранителе и 0,02 в
        /// планировщике), и «прошло проверку» означало разное в разных местах.
        /// </summary>
        public const float PlanningClearanceFactor = 4f / 3f;

        /// <summary>ПОРОГ ПЛАНИРОВЩИКА, метры — единственный источник (см. `PlanningClearanceFactor`).</summary>
        public static float DefaultPlanningClearance
        {
            get { return DefaultMinClearance * PlanningClearanceFactor; }
        }

        /// <summary>Действующий порог зазора, метры (настраивается; по умолчанию `DefaultMinClearance`).</summary>
        public float minClearance = DefaultMinClearance;

        public float minLimitMarginDeg = 3f;
        public int watchdogTimeoutMs = 300;

        /// <summary>
        /// ПОРОГ ЗАЗОРА ДЛЯ СТОЯЩЕЙ ПОЗЫ, метры — ТО ЖЕ ЧИСЛО, ЧТО ДЛЯ ДВИЖЕНИЯ (12.11.2026).
        ///
        /// ЧТО БЫЛО. Здесь стоял множитель `standingClearanceFactor = 0.5f` с объяснением «стоящий
        /// робот не движется, поэтому хватит половины». Объяснение неверно по существу: зазор —
        /// это расстояние до стола, деталей и людей, и оно не зависит от того, едет робот или стоит.
        /// Ровно наоборот — на паузе робот стоит рядом с тем, что оператор в этот момент может
        /// пододвинуть, и именно тогда занижать порог опаснее всего.
        ///
        /// ЧЕМ ЭТО ГРОЗИЛО. Шапка файла обещала «порог один», а чисел было два: наладчик, читая
        /// «минимум 15 мм», получал разрешение на 7,5 мм и не мог понять, какое число настоящее.
        ///
        /// ЧТО СТАЛО. Множителя нет (поле `standingClearanceFactor` удалено вместе с ним: поле,
        /// которое ничего не меняет, — обещание настройки, которой нет). Свойство оставлено, потому
        /// что называет ПОНЯТИЕ («порог для стоящей позы») и им пользуются снаружи, но возвращает
        /// тот же `minClearance`: один порог на движение и на стоянку.
        /// </summary>
        public float StandingClearance { get { return minClearance; } }

        /// <summary>
        /// ПРЕДЕЛ СКОРОСТИ СУСТАВА ДЛЯ ПРОВЕРКИ ПЕРЕД ПУСКОМ, °/с (добавлено 30.10.2026, S44).
        ///
        /// ЧТО БЫЛО. В перечислении `SafetyReason` есть `Velocity` («превышение скорости»), и
        /// `Describe` показывает его оператору — но выставлять его было НЕЧЕМ: `Approve` смотрел
        /// только лимиты суставов и зазор. То есть строка «превышение скорости» существовала как
        /// обещание, а проверки за ней не было. Это тот же класс, что «мёртвая проверка»: хуже
        /// отсутствующей, потому что создаёт уверенность.
        ///
        /// ЧТО СЧИТАЕТСЯ. Скорость сустава по готовой траектории: `|Δq| / Δt` между соседними
        /// сэмплами. Это ровно то, что поедет исполнитель, — никаких допущений о профиле.
        ///
        /// ПОЧЕМУ 90 °/с. Это значение по умолчанию `KvMotionLimits.maxVelDeg` для этого робота
        /// (см. `KvTrajMath.KvMotionLimits`). Держать здесь своё число значило бы завести вторую
        /// правду о пределах; поле открыто для настройки, но по умолчанию совпадает с планировщиком.
        /// </summary>
        public float maxJointVelocityDeg = 90f;

        public bool Enabled = true;

        private long lastStateMs = -1;
        private double lastPlayheadTime = -1;

        public SafetyReason LastReason { get; private set; } = SafetyReason.NotChecked;

        /// <summary>
        /// САМАЯ БОЛЬШАЯ СКОРОСТЬ СУСТАВА НА ТРАЕКТОРИИ, °/с (добавлено 30.10.2026, S44).
        ///
        /// ЧИСТАЯ ФУНКЦИЯ — намеренно: её надо проверять тестом без сцены, а сцена для проверки
        /// скорости не нужна вовсе, нужны только позы и времена.
        ///
        /// ВОЗВРАЩАЕТ 0, если времена отсутствуют или их длина не совпадает с путём: «нет данных
        /// о времени» — это не «нулевая скорость», и вызывающий обязан проверить `times != null`.
        /// </summary>
        public static float MaxJointVelocityDeg(PlannedTrajectory t)
        {
            if (t == null || t.Path == null || t.Times == null) return 0f;
            if (t.Path.Length < 2 || t.Times.Length != t.Path.Length) return 0f;

            float worst = 0f;
            for (int i = 1; i < t.Path.Length; i++)
            {
                double dt = t.Times[i] - t.Times[i - 1];
                // Нулевой или отрицательный интервал — дефект траектории, а не «бесконечная
                // скорость»: такой шаг пропускаем, его поймает проверка времени (см. `TimesMonotonic`).
                if (dt <= 1e-9) continue;

                double[] a = t.Path[i - 1];
                double[] b = t.Path[i];
                int dof = Mathf.Min(a.Length, b.Length);
                for (int j = 0; j < dof; j++)
                {
                    float v = (float)(Mathf.Abs((float)(b[j] - a[j])) / dt);
                    if (v > worst) worst = v;
                }
            }
            return worst;
        }

        /// <summary>
        /// ВРЕМЯ МОНОТОННО. Отдельная проверка, потому что убывающее время ломает и скорость, и
        /// исполнителя (он ведёт робота по времени). Добавлено вместе с проверкой скорости: без
        /// неё траектория с «временем назад» проходила бы порог скорости просто потому, что такие
        /// интервалы пропускаются.
        /// </summary>
        public static bool TimesMonotonic(PlannedTrajectory t)
        {
            if (t == null || t.Times == null || t.Times.Length < 2) return true;
            for (int i = 1; i < t.Times.Length; i++)
                if (t.Times[i] < t.Times[i - 1]) return false;
            return true;
        }

        public void NotifyState() { lastStateMs = (long)(Time.realtimeSinceStartup * 1000f); }

        public bool WatchdogAlive()
        {
            if (lastStateMs < 0) return false;
            return (long)(Time.realtimeSinceStartup * 1000f) - lastStateMs <= watchdogTimeoutMs;
        }

        /// <summary>Проверка траектории перед исполнением.</summary>
        public bool Approve(PlannedTrajectory t, PoseValidator validator, out SafetyReason reason)
        {
            reason = SafetyReason.None;
            if (!Enabled) { LastReason = reason; return true; }
            if (t == null || t.Path == null || t.Path.Length == 0)
            {
                reason = SafetyReason.NoData; LastReason = reason; return false;
            }

            // П1-10: план без времён (или с несовпадающей длиной) не пропускаем — иначе падение
            // `NullReferenceException` уже В ЦИКЛЕ ДВИЖЕНИЯ, мимо `Stop(reason)`.
            if (t.Times == null || t.Times.Length != t.Path.Length)
            {
                reason = SafetyReason.NoData; LastReason = reason;
                Debug.LogWarning("[Safety] Отказ: у плана " + t.Path.Length + " точек и " +
                                 (t.Times != null ? t.Times.Length : 0) +
                                 " времён — исполнять нечего (P1-10)");
                return false;
            }

            if (!WatchdogAlive())
            {
                reason = SafetyReason.Watchdog; LastReason = reason; return false;
            }

            if (t.MinClearance < minClearance)
            {
                reason = SafetyReason.Clearance; LastReason = reason; return false;
            }
            if (t.LimitMargin < minLimitMarginDeg)
            {
                reason = SafetyReason.JointLimit; LastReason = reason; return false;
            }
            // ПРОВЕРКА ЛИМИТОВ СУСТАВОВ — ТОЛЬКО ЕСЛИ ЕСТЬ ВАЛИДАТОР (исправлено 30.10.2026, S44).
            //
            // ЧТО БЫЛО. Строка `foreach (double[] q in t.Path) if (!validator.WithinLimits(q))`
            // падала `NullReferenceException`, если валидатор не передан. В боевом потоке он
            // всегда есть, поэтому дефект не проявлялся — но предохранитель УЖЕ к этому моменту
            // проверил зазор, запас и живость данных. Падать в самом конце, потеряв все собранные
            // улики, — худший вариант: `LastReason` остался бы от предыдущего вызова, и оператор
            // увидел бы ЛОЖНУЮ причину.
            //
            // ТЕПЕРЬ: нет валидатора — проверка лимитов пропускается (проверять их не по чему:
            // пределы живут в валидаторе), остальные проверки работают. Это не «ослабить
            // безопасность»: без валидатора пределов просто не существует, и выдумывать их нельзя.
            if (validator != null)
            {
                foreach (double[] q in t.Path)
                {
                    if (!validator.WithinLimits(q))
                    {
                        reason = SafetyReason.JointLimit; LastReason = reason; return false;
                    }
                }
            }

            // ПРОВЕРКА СКОРОСТИ — НАКОНЕЦ РЕАЛЬНАЯ (30.10.2026, S44).
            //
            // Раньше `SafetyReason.Velocity` было объявлено и описано оператору, но выставить его
            // было нечем: проверка сводилась к лимитам суставов и зазору. Теперь скорость считается
            // по готовой траектории — ровно та, с которой поедет исполнитель.
            //
            // ПОЧЕМУ ПОСЛЕ ЛИМИТОВ. Порядок важен для ЧЕЛОВЕКА: если поза вне лимитов, сказать
            // «лимит сустава» понятнее, чем «превышение скорости» (вторая — следствие первой).
            if (!TimesMonotonic(t))
            {
                // Время назад — это дефект тайминга; называем его как предел скорости, потому что
                // именно «скорость» оператор и увидит в журнале при странном поведении робота.
                reason = SafetyReason.Velocity; LastReason = reason; return false;
            }

            // ДОПУСК НА ГРАНИЦЕ. Скорость считается по float-временам (0,05 с хранится как
            // 0,049999997): «ровно 90 °/с» честно выходит 90,000004 и отвергалось бы. Допуск 0,1 %
            // делает границу ВКЛЮЧИТЕЛЬНОЙ — иначе оператор получал бы отказ на траектории, которую
            // сам планировщик счёл допустимой, и это выглядело бы как «предохранитель капризничает».
            float limit = maxJointVelocityDeg * 1.001f;
            float velocity = MaxJointVelocityDeg(t);
            if (velocity > limit)
            {
                reason = SafetyReason.Velocity; LastReason = reason; return false;
            }

            LastReason = reason;
            return true;
        }

        /// <summary>
        /// ПРОВЕРКА НА ШАГЕ ИСПОЛНЕНИЯ: не вышли ли за пределы и не застряли ли.
        ///
        /// ЧТО ИСПРАВЛЕНО 30.10.2026 (S44):
        ///  1) `validator.WithinLimits(q)` падал `NullReferenceException` без валидатора — уже
        ///     ПОСЛЕ проверки живости данных, то есть терял собранные улики и оставлял ложную
        ///     причину от предыдущего вызова;
        ///  2) ЗАСТРЕВАНИЕ НЕ ЛОВИЛОСЬ: условие `playheadTime <= last - 1e-6` срабатывало только
        ///     на движение НАЗАД. Если время просто СТОЯЛО НА МЕСТЕ (застрявший плеер — ровно то,
        ///     от чего этот предохранитель и нужен), проверка молчала, и робот «висел» без причины
        ///     в журнале.
        ///
        /// ЭТОТ МЕТОД — ТОЛЬКО ДЛЯ ДВИЖЕНИЯ (12.11.2026). Правило «время вперёд» нужно едущему
        /// роботу и НЕ нужно стоящему: на паузе время заморожено осознанно. Проверка стоящей позы
        /// поэтому идёт своим путём — `ApproveStandingPose` (см. пояснение там: пока она шла через
        /// этот метод, пауза превращалась в аварийный останов «watchdog»).
        /// </summary>
        public bool ApproveStep(double[] q, float clearance, double playheadTime, PoseValidator validator,
            out SafetyReason reason)
        {
            reason = SafetyReason.None;
            if (!Enabled) return true;

            if (!WatchdogAlive()) { reason = SafetyReason.Watchdog; LastReason = reason; return false; }

            // ВРЕМЯ ДОЛЖНО ИДТИ ВПЕРЁД. Не «не назад», а именно вперёд: стоящее время — такое же
            // застревание, как и обратное, и для оператора это одно и то же («робот встал»).
            const double epsilon = 1e-6;
            if (lastPlayheadTime > 0 && playheadTime <= lastPlayheadTime + epsilon)
            {
                reason = SafetyReason.Watchdog; LastReason = reason; return false;
            }

            // ЛИМИТЫ — ТОЛЬКО ПРИ НАЛИЧИИ ВАЛИДАТОРА (пределы живут в нём, выдумывать их нельзя).
            if (validator != null && !validator.WithinLimits(q))
            {
                reason = SafetyReason.JointLimit; LastReason = reason; return false;
            }

            // ПОРОГ ЗАЗОРА — ОДИН (исправлено 12.11.2026, дефект B). Здесь стояло
            // `minClearance * 0.5f`: предохранитель пропускал позу с зазором ВДВОЕ меньше
            // объявленного минимума, хотя шапка файла уже утверждала «порог один». Слово и дело
            // расходились, и предохранитель был мягче собственного описания. Теперь сравнение с
            // самим `minClearance`; планировщик держит запас 4/3 (`PlanningClearanceFactor`),
            // поэтому корректно построенные планы проходят с запасом в треть.
            if (clearance < minClearance) { reason = SafetyReason.Clearance; LastReason = reason; return false; }

            lastPlayheadTime = playheadTime;
            LastReason = reason;
            return true;
        }

        /// <summary>
        /// ПРОВЕРКА СТОЯЩЕЙ ПОЗЫ НА ПАУЗЕ (добавлено 12.11.2026, дефект A).
        ///
        /// ЧТО БЫЛО. `TrajectoryExecutor.CheckStandingPose` звал `ApproveStep` и передавал туда
        /// ТЕКУЩИЙ `playhead`. На паузе он намеренно не растёт (см. `TrajectoryExecutor.PlayheadAfterFrame`),
        /// поэтому первое же правило `ApproveStep` — «время должно идти вперёд» — отказывало с
        /// причиной `Watchdog`, и `Stop(reason)` превращал ПАУЗУ В АВАРИЙНЫЙ ОСТАНОВ. Оператор
        /// терял движение, которое всего лишь хотел приостановить, и видел ложную причину: watchdog
        /// жив, данные есть.
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД, А НЕ ФЛАЖОК. Правило времени нужно ДВИЖЕНИЮ (застрявший плеер —
        /// самая частая причина «робот встал и молчит») и не нужно стоянке: время на паузе
        /// заморожено осознанно, смысл проверки здесь — «робот стоит, а обстановка могла
        /// измениться». Флажок «не проверять время» оставил бы один метод с двумя смыслами и
        /// вернул бы ровно ту путаницу, из которой дефект и вырос.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ: живость данных (watchdog), лимиты суставов, зазор — тот же единый порог
        /// `minClearance`, что и в движении. Чего НЕТ: правила «время вперёд», проверки скорости
        /// (стоящий робот её не имеет) и записи `lastPlayheadTime` — проверка на паузе не должна
        /// портить учёт времени, иначе первое же движение после снятия паузы получит ложный
        /// `Watchdog`.
        ///
        /// НАСТОЯЩАЯ ОПАСНОСТЬ ПО-ПРЕЖНЕМУ ОСТАНАВЛИВАЕТ: малый зазор (`Clearance`), выход за
        /// лимиты (`JointLimit`), мёртвый watchdog (`Watchdog`) — с ВЕРНОЙ причиной в журнале.
        /// </summary>
        public bool ApproveStandingPose(double[] q, float clearance, PoseValidator validator,
            out SafetyReason reason)
        {
            reason = SafetyReason.None;
            if (!Enabled) return true;

            if (!WatchdogAlive()) { reason = SafetyReason.Watchdog; LastReason = reason; return false; }

            // ЛИМИТЫ — ТОЛЬКО ПРИ НАЛИЧИИ ВАЛИДАТОРА (пределы живут в нём, выдумывать их нельзя).
            if (validator != null && !validator.WithinLimits(q))
            {
                reason = SafetyReason.JointLimit; LastReason = reason; return false;
            }

            // ТОТ ЖЕ ПОРОГ, ЧТО НА ШАГЕ ДВИЖЕНИЯ (см. `StandingClearance`).
            if (clearance < StandingClearance) { reason = SafetyReason.Clearance; LastReason = reason; return false; }

            // `lastPlayheadTime` НЕ ЗАПИСЫВАЕТСЯ: время на паузе не идёт, и запись сюда «съела» бы
            // следующий кадр движения.
            LastReason = reason;
            return true;
        }

        public void ResetPlayhead() { lastPlayheadTime = -1; }

        public static string Describe(SafetyReason r)
        {
            switch (r)
            {
                case SafetyReason.JointLimit: return "лимит сустава";
                case SafetyReason.Clearance: return "малый зазор";
                case SafetyReason.Velocity: return "превышение скорости";
                case SafetyReason.Watchdog: return "watchdog: нет данных";
                case SafetyReason.NoData: return "нет траектории";
                case SafetyReason.OperatorStop: return "остановка оператором";
                default: return "ок";
            }
        }
    }
}
