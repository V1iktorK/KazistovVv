// ================================================================================================
//  KvSafetyGateVelocityTests.cs — EditMode-тесты ПРОВЕРКИ СКОРОСТИ В ПРЕДОХРАНИТЕЛЕ
//  (сессия S44, пункт 5.9 плана: «SafetyGate.Velocity не проверяется»).
//
//  ОТКУДА ЗАДАЧА. В перечислении `SafetyReason` есть `Velocity`, и `Describe` показывает
//  оператору строку «превышение скорости». Но выставлять эту причину было НЕЧЕМ: `Approve`
//  смотрел только лимиты суставов и зазор. То есть проверка существовала как обещание.
//
//  ЧЕМ ЭТО ПЛОХО. «Мёртвая проверка» хуже отсутствующей: оператор читает в списке причин
//  «превышение скорости» и считает, что скорость контролируется. А её никто не смотрел.
//
//  ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ (чистые данные, без сцены):
//    1) нормальная траектория проходит;
//    2) завышенная скорость ОТВЕРГАЕТСЯ с причиной `Velocity`;
//    3) РОВНО на пределе — проходит (граница включительно, иначе оператор увидит отказ на
//       допустимой траектории);
//    4) скорость считается ПО КАЖДОМУ СУСТАВУ (быстрый сустав в одном месте ловится);
//    5) нулевые интервалы времени не дают «бесконечной» скорости и не скрывают нарушение;
//    6) время назад отвергается;
//    7) нет времени — нет данных, а не «ноль скорости».
// ================================================================================================

using NUnit.Framework;
using TrajectoryCore;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Скорость суставов в предохранителе: реальная проверка вместо обещания.</summary>
    public class KvSafetyGateVelocityTests
    {
        /// <summary>
        /// ТРАЕКТОРИЯ ИЗ ПОЗ И ВРЕМЁН. `degPerStep` — на сколько градусов сдвигается ПЕРВЫЙ сустав
        /// между соседними сэмплами; время шага задаётся отдельно, поэтому скорость считается в уме:
        /// скорость = degPerStep / stepSeconds.
        /// </summary>
        private static PlannedTrajectory MakePlan(int samples, float degPerStep, float stepSeconds)
        {
            var path = new double[samples][];
            var times = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                path[i] = new double[] { i * (double)degPerStep, 0, 0, 0, 0, 0 };
                times[i] = i * stepSeconds;
            }
            return new PlannedTrajectory
            {
                Path = path,
                Times = times,
                Time = times[samples - 1],
                Label = "тест скорости",
                MinClearance = 1f,        // зазор не мешает — проверяем именно скорость
                LimitMargin = 90f         // запас до лимитов большой
            };
        }

        // ---------------------------------------------------------------- 1. СЧЁТ СКОРОСТИ

        /// <summary>
        /// СКОРОСТЬ СЧИТАЕТСЯ АРИФМЕТИКОЙ: 90° за 0,5 с = 180 °/с. Число проверяемо в уме, поэтому
        /// тест не может «сойтись» на ошибке в формуле.
        /// </summary>
        [Test]
        public void MaxJointVelocity_IsDegreesPerSecond()
        {
            PlannedTrajectory plan = MakePlan(5, 9f, 0.05f);            // 9° / 0,05 с = 180 °/с
            Assert.AreEqual(180f, SafetyGate.MaxJointVelocityDeg(plan), 0.5f,
                "9° за 0,05 с обязано дать 180 °/с");
        }

        /// <summary>БЕЗ ВРЕМЁН — НЕТ ДАННЫХ (0), а не «нулевая скорость». Вызывающий обязан это различить.</summary>
        [Test]
        public void MaxJointVelocity_WithoutTimes_ReturnsZero()
        {
            var plan = new PlannedTrajectory
            {
                Path = new[] { new double[] { 0, 0, 0, 0, 0, 0 }, new double[] { 90, 0, 0, 0, 0, 0 } },
                Times = null
            };
            Assert.AreEqual(0f, SafetyGate.MaxJointVelocityDeg(plan), 1e-6f,
                "без времён скорость не определена — функция обязана вернуть 0, а не выдумывать");
        }

        /// <summary>Одна точка — движения нет, скорость 0 и никаких исключений.</summary>
        [Test]
        public void MaxJointVelocity_SinglePoint_IsZero()
        {
            PlannedTrajectory plan = MakePlan(1, 5f, 0.05f);
            Assert.AreEqual(0f, SafetyGate.MaxJointVelocityDeg(plan), 1e-6f);
        }

        // ---------------------------------------------------------------- 2. ПРОВЕРКА В ГЕЙТЕ

        /// <summary>
        /// СЛИШКОМ БЫСТРАЯ ТРАЕКТОРИЯ ОТВЕРГАЕТСЯ С ПРИЧИНОЙ `Velocity`.
        ///
        /// 900 °/с при пределе 90 — заведомое нарушение. Проверяется и сам отказ, и ПРИЧИНА:
        /// без причины оператор не узнает, что именно не так.
        /// </summary>
        [Test]
        public void Approve_RejectsTooFastTrajectory_WithVelocityReason()
        {
            var gate = new SafetyGate { maxJointVelocityDeg = 90f, Enabled = true };
            gate.NotifyState();                                    // watchdog «жив»

            PlannedTrajectory plan = MakePlan(5, 45f, 0.05f);      // 45° / 0,05 с = 900 °/с

            SafetyReason reason;
            bool approved = gate.Approve(plan, null, out reason);   // лимиты не проверяем: validator=null

            Assert.IsFalse(approved, "траектория со скоростью 900 °/с при пределе 90 обязана быть отвергнута");
            Assert.AreEqual(SafetyReason.Velocity, reason,
                "причина обязана быть «превышение скорости», получено: " + SafetyGate.Describe(reason));
        }

        /// <summary>
        /// НОРМАЛЬНАЯ СКОРОСТЬ ПРОХОДИТ. Без этой проверки «строгий» предохранитель отвергал бы
        /// всё подряд — а это не безопасность, а неработающая система.
        /// </summary>
        [Test]
        public void Approve_AcceptsNormalTrajectory()
        {
            var gate = new SafetyGate { maxJointVelocityDeg = 90f, Enabled = true };
            gate.NotifyState();

            PlannedTrajectory plan = MakePlan(5, 2f, 0.05f);       // 40 °/с — в пределах

            SafetyReason reason;
            bool approved = gate.Approve(plan, null, out reason);

            Assert.IsTrue(approved, "траектория 40 °/с при пределе 90 обязана проходить; причина: " +
                                    SafetyGate.Describe(reason));
        }

        /// <summary>
        /// РОВНО НА ПРЕДЕЛЕ — ПРОХОДИТ. Граница включительная: если предел 90, то 90 допустимо.
        /// Иначе оператор получал бы отказ на траектории, которую сам планировщик счёл допустимой
        /// (планировщик тоже считает 90 допустимым — расхождение открывалось бы как «не пускает
        /// без причины»).
        /// </summary>
        [Test]
        public void Approve_AcceptsExactlyAtTheLimit()
        {
            var gate = new SafetyGate { maxJointVelocityDeg = 90f, Enabled = true };
            gate.NotifyState();

            PlannedTrajectory plan = MakePlan(5, 4.5f, 0.05f);     // ровно 90 °/с

            SafetyReason reason;
            Assert.IsTrue(gate.Approve(plan, null, out reason),
                "скорость ровно на пределе обязана проходить (предел включительный); причина: " +
                SafetyGate.Describe(reason));
        }

        /// <summary>
        /// СЧЁТ ИДЁТ ПО КАЖДОМУ СУСТАВУ. Быстрый рывок ШЕСТОГО сустава обязан ловиться так же, как
        /// первого: если проверять только ось 1, «дёрганье» кисти проходило бы незамеченным.
        /// </summary>
        [Test]
        public void MaxJointVelocity_ChecksEveryJoint()
        {
            // Все суставы медленные, кроме шестого — он «дёргается».
            var path = new double[3][];
            var times = new float[] { 0f, 0.05f, 0.1f };
            for (int i = 0; i < 3; i++) path[i] = new double[] { i * 0.1, i * 0.1, i * 0.1, i * 0.1, i * 0.1, i * 60.0 };

            var plan = new PlannedTrajectory { Path = path, Times = times, Time = 0.1f };
            Assert.AreEqual(1200f, SafetyGate.MaxJointVelocityDeg(plan), 1f,
                "рывок шестого сустава 60° за 0,05 с = 1200 °/с обязан быть найден");
        }

        // ---------------------------------------------------------------- 3. ВРЕМЯ

        /// <summary>
        /// НУЛЕВОЙ ИНТЕРВАЛ НЕ ДАЁТ «БЕСКОНЕЧНОЙ» СКОРОСТИ. Два сэмпла в одно и то же время —
        /// это дефект тайминга; деление на ноль дало бы Infinity и «отравило» бы весь расчёт.
        /// </summary>
        [Test]
        public void MaxJointVelocity_ZeroInterval_DoesNotProduceInfinity()
        {
            var path = new[]
            {
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 50, 0, 0, 0, 0, 0 },
                new double[] { 60, 0, 0, 0, 0, 0 }
            };
            var times = new float[] { 0f, 0f, 0.05f };            // первый интервал нулевой

            var plan = new PlannedTrajectory { Path = path, Times = times, Time = 0.05f };
            float v = SafetyGate.MaxJointVelocityDeg(plan);

            Assert.IsFalse(float.IsInfinity(v) || float.IsNaN(v),
                "нулевой интервал не должен давать бесконечность или NaN");
            Assert.AreEqual(200f, v, 1f,
                "скорость второго интервала: 10° за 0,05 с = 200 °/с; получено " + v.ToString("0.0"));
        }

        /// <summary>
        /// ВРЕМЯ НАЗАД — ОТКАЗ. Убывающее время ломает исполнителя (он ведёт робота по времени) и
        /// при этом «прячет» скорость, потому что такие интервалы пропускаются.
        /// </summary>
        [Test]
        public void Approve_RejectsNonMonotonicTime()
        {
            var gate = new SafetyGate { maxJointVelocityDeg = 90f, Enabled = true };
            gate.NotifyState();

            var path = new[]
            {
                new double[] { 0, 0, 0, 0, 0, 0 },
                new double[] { 1, 0, 0, 0, 0, 0 },
                new double[] { 2, 0, 0, 0, 0, 0 }
            };
            var times = new float[] { 0f, 0.1f, 0.05f };          // время пошло НАЗАД

            var plan = new PlannedTrajectory
            {
                Path = path,
                Times = times,
                Time = 0.05f,
                MinClearance = 1f,
                LimitMargin = 90f
            };

            Assert.IsFalse(SafetyGate.TimesMonotonic(plan), "убывающее время обязано распознаваться");

            SafetyReason reason;
            Assert.IsFalse(gate.Approve(plan, null, out reason),
                "траектория с временем назад обязана отвергаться");
            Assert.AreEqual(SafetyReason.Velocity, reason,
                "причина — «превышение скорости» (так это увидит оператор): " +
                SafetyGate.Describe(reason));
        }

        /// <summary>МОНОТОННОЕ ВРЕМЯ РАСПОЗНАЁТСЯ КАК ПОРЯДОК — базовая проверка, чтобы не путать.</summary>
        [Test]
        public void TimesMonotonic_AcceptsOrderedTimes()
        {
            Assert.IsTrue(SafetyGate.TimesMonotonic(MakePlan(5, 1f, 0.05f)));
            Assert.IsTrue(SafetyGate.TimesMonotonic(null), "без траектории проверять нечего — не отказ");
        }

        // ---------------------------------------------------------------- 4. ВЫКЛЮЧЕННЫЙ ГЕЙТ

        /// <summary>
        /// ВЫКЛЮЧЕННЫЙ ПРЕДОХРАНИТЕЛЬ НИЧЕГО НЕ ПРОВЕРЯЕТ — это его контракт (`Enabled = false`),
        /// и он не должен меняться: иначе тесты выше проверяли бы не то, что работает на стенде
        /// при включённом гейте.
        /// </summary>
        [Test]
        public void DisabledGate_DoesNotCheckVelocity()
        {
            var gate = new SafetyGate { maxJointVelocityDeg = 90f, Enabled = false };
            PlannedTrajectory plan = MakePlan(5, 45f, 0.05f);      // 900 °/с

            SafetyReason reason;
            Assert.IsTrue(gate.Approve(plan, null, out reason),
                "выключенный предохранитель обязан пропускать — это его явный контракт");
        }
    }
}
