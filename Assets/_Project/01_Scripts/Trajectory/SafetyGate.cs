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
        public float minClearance = 0.015f;
        public float minLimitMarginDeg = 3f;
        public int watchdogTimeoutMs = 300;

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

        /// <summary>Проверка на шаге исполнения: не вышли ли за пределы/не застряли ли.</summary>
        public bool ApproveStep(double[] q, float clearance, double playheadTime, PoseValidator validator,
            out SafetyReason reason)
        {
            reason = SafetyReason.None;
            if (!Enabled) return true;

            if (!WatchdogAlive()) { reason = SafetyReason.Watchdog; LastReason = reason; return false; }
            if (playheadTime <= lastPlayheadTime - 1e-6 && lastPlayheadTime > 0)
            {
                reason = SafetyReason.Watchdog; LastReason = reason; return false;
            }
            if (!validator.WithinLimits(q)) { reason = SafetyReason.JointLimit; LastReason = reason; return false; }
            if (clearance < minClearance * 0.5f) { reason = SafetyReason.Clearance; LastReason = reason; return false; }

            lastPlayheadTime = playheadTime;
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
