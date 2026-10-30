// ================================================================================================
//  KvSafetyGateStepTests.cs — EditMode-тесты ПРОВЕРКИ НА ШАГЕ ИСПОЛНЕНИЯ (сессия S44).
//
//  ОТКУДА ЗАДАЧА. `SafetyGate.ApproveStep` — это предохранитель, который работает ВО ВРЕМЯ
//  движения: если время плеера не идёт вперёд, робот должен остановиться. Ровно от этого он и
//  нужен, потому что застрявший плеер — самая частая причина «робот встал и молчит».
//
//  ЧТО НАЙДЕНО ТЕСТАМИ 30.10.2026:
//    1) ЗАСТРЕВАНИЕ НЕ ЛОВИЛОСЬ. Условие `playheadTime <= last - 1e-6` срабатывало только на
//       движение НАЗАД. Если время СТОЯЛО на месте, проверка молчала — а именно это и есть
//       застревание.
//    2) `validator.WithinLimits(q)` падал `NullReferenceException` без валидатора — уже ПОСЛЕ
//       проверки живости данных, то есть терял улики и оставлял ЛОЖНУЮ причину от прошлого вызова.
//
//  ЧТО ЗАКРЕПЛЕНО:
//    1) время вперёд — проходит;
//    2) время СТОИТ — отказ `Watchdog` (главное исправление);
//    3) время НАЗАД — отказ;
//    4) после отказа состояние не сдвигается (иначе следующая проверка сравнивала бы с ним);
//    5) малый зазор — отказ `Clearance`;
//    6) без валидатора не бросает;
//    7) после `ResetPlayhead` можно начинать заново.
// ================================================================================================

using NUnit.Framework;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>Проверка на шаге исполнения: застревание, зазор, устойчивость.</summary>
    public class KvSafetyGateStepTests
    {
        private static readonly double[] Pose = { 0, 0, 0, 0, 0, 0 };

        /// <summary>Живой предохранитель с большим зазором — чтобы проверять только время.</summary>
        private static SafetyGate LiveGate()
        {
            var gate = new SafetyGate { Enabled = true, minClearance = 0.015f };
            gate.NotifyState();                       // watchdog «жив»
            return gate;
        }

        // ---------------------------------------------------------------- 1. НОРМАЛЬНЫЙ ХОД

        /// <summary>
        /// ВРЕМЯ ИДЁТ ВПЕРЁД — ДВИЖЕНИЕ РАЗРЕШЕНО. Без этой проверки «строгий» предохранитель
        /// останавливал бы всё подряд, и это была бы не безопасность, а неработающая система.
        /// </summary>
        [Test]
        public void AdvancingPlayhead_IsApproved()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.0, null, out reason),
                "первый шаг обязан проходить; причина: " + SafetyGate.Describe(reason));
            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.02, null, out reason),
                "время пошло вперёд — движение разрешено; причина: " + SafetyGate.Describe(reason));
            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.04, null, out reason),
                "третий шаг тоже; причина: " + SafetyGate.Describe(reason));
            Assert.AreEqual(SafetyReason.None, reason);
        }

        // ---------------------------------------------------------------- 2. ЗАСТРЕВАНИЕ

        /// <summary>
        /// ВРЕМЯ СТОИТ НА МЕСТЕ — ОТКАЗ `Watchdog`. ЭТО ГЛАВНЫЙ ТЕСТ ФАЙЛА.
        ///
        /// До исправления условие требовало «меньше предыдущего минус 1e-6», поэтому РАВНОЕ время
        /// проходило. Следствие: застрявший плеер (время не двигается — самый частый отказ
        /// исполнителя) не замечался, робот висел, и в журнале не было ни одной причины.
        /// </summary>
        [Test]
        public void FrozenPlayhead_IsRejectedAsWatchdog()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.5, null, out reason), "подготовка: шаг на 0,5 с");

            // ТО ЖЕ САМОЕ ВРЕМЯ ПОВТОРНО — это застревание.
            Assert.IsFalse(gate.ApproveStep(Pose, 1f, 0.5, null, out reason),
                "повтор того же времени — застревание, движение обязано остановиться");
            Assert.AreEqual(SafetyReason.Watchdog, reason,
                "причина — сторожевой таймер (данные есть, прогресса нет): " + SafetyGate.Describe(reason));
        }

        /// <summary>
        /// ВРЕМЯ НАЗАД — ТОЖЕ ОТКАЗ. Проверялось и раньше, но закрепляем: исправление не должно
        /// потерять этот случай, заменяя условие.
        /// </summary>
        [Test]
        public void BackwardPlayhead_IsRejected()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.5, null, out reason));
            Assert.IsFalse(gate.ApproveStep(Pose, 1f, 0.3, null, out reason),
                "время пошло назад — движение обязано остановиться");
            Assert.AreEqual(SafetyReason.Watchdog, reason);
        }

        /// <summary>
        /// МИКРОСКОПИЧЕСКИЙ СДВИГ ВРЕМЕНИ — НЕ ЗАСТРЕВАНИЕ. Допуск 1e-6 существует именно для
        /// этого: при частоте 90 Гц шаг 0,0111 с, но у плавающей точки возможны дрожания. Слишком
        /// строгое условие (без допуска) останавливало бы нормальное движение.
        /// </summary>
        [Test]
        public void TinyAdvance_IsNotTreatedAsStuck()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.5, null, out reason));
            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.5 + 5e-6, null, out reason),
                "сдвиг 5e-6 с больше допуска — это движение, а не застревание; причина: " +
                SafetyGate.Describe(reason));
        }

        // ---------------------------------------------------------------- 3. СОСТОЯНИЕ

        /// <summary>
        /// ПОСЛЕ ОТКАЗА СОСТОЯНИЕ НЕ СДВИГАЕТСЯ. Если бы отказ записывал время, следующая проверка
        /// сравнивала бы с ним и могла бы «простить» застревание — предохранитель перестал бы
        /// замечать остановку после первого же отказа.
        /// </summary>
        [Test]
        public void AfterRejection_StateIsNotAdvanced()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.5, null, out reason));
            Assert.IsFalse(gate.ApproveStep(Pose, 1f, 0.3, null, out reason), "время назад — отказ");

            // Повторяем то же застрявшее время — отказ обязан повториться.
            Assert.IsFalse(gate.ApproveStep(Pose, 1f, 0.3, null, out reason),
                "отказ не должен «прощаться» на следующем вызове");
        }

        /// <summary>
        /// `ResetPlayhead` ВОЗВРАЩАЕТ К НАЧАЛУ. Нужно при перезапуске траектории: иначе новый
        /// запуск с нуля выглядел бы как «время пошло назад» и не стартовал бы.
        /// </summary>
        [Test]
        public void ResetPlayhead_AllowsStartingFromTheBeginning()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 5.0, null, out reason), "подготовка: время 5 с");
            gate.ResetPlayhead();

            Assert.IsTrue(gate.ApproveStep(Pose, 1f, 0.0, null, out reason),
                "после сброса можно начать с нуля; причина: " + SafetyGate.Describe(reason));
        }

        // ---------------------------------------------------------------- 4. ЗАЗОР

        /// <summary>
        /// МАЛЫЙ ЗАЗОР — ОТКАЗ `Clearance`. На шаге исполнения порог мягче (`minClearance * 0.5`),
        /// чем перед пуском: робот уже в движении, и останавливать его из-за каждого миллиметра
        /// значило бы дергать. Но половинный порог обязан ловить реальную опасность.
        /// </summary>
        [Test]
        public void SmallClearance_IsRejected()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            // Порог 0,0075 м (половина от 0,015). Даём 0,001 м — заведомо мало.
            Assert.IsFalse(gate.ApproveStep(Pose, 0.001f, 0.0, null, out reason),
                "зазор 1 мм при пороге 7,5 мм — движение обязано остановиться");
            Assert.AreEqual(SafetyReason.Clearance, reason,
                "причина — малый зазор: " + SafetyGate.Describe(reason));
        }

        /// <summary>
        /// ЗАЗОР ВЫШЕ ПОРОГА — ПРОХОДИТ. Граница «половина от пускового порога» включительная по
        /// смыслу: значение ровно на пороге не должно давать отказ, иначе получим дрожание
        /// «стоп-поехали» на ровном месте.
        /// </summary>
        [Test]
        public void ClearanceAboveThreshold_Passes()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 0.02f, 0.0, null, out reason),
                "зазор 20 мм при пороге 7,5 мм — движение разрешено; причина: " +
                SafetyGate.Describe(reason));
        }

        // ---------------------------------------------------------------- 5. УСТОЙЧИВОСТЬ

        /// <summary>
        /// БЕЗ ВАЛИДАТОРА НЕ БРОСАЕТ. До исправления здесь был `NullReferenceException` — уже
        /// после того, как предохранитель проверил живость данных. Исключение теряло улики и
        /// оставляло `LastReason` от предыдущего вызова, то есть оператор видел ЛОЖНУЮ причину.
        /// </summary>
        [Test]
        public void WithoutValidator_DoesNotThrow()
        {
            SafetyGate gate = LiveGate();
            // ИНИЦИАЛИЗАЦИЯ ОБЯЗАТЕЛЬНА: присваивание идёт внутри лямбды, и компилятор не может
            // доказать, что она выполнится (CS0165). Это не придирка — если лямбда бросит раньше,
            // проверка ниже читала бы неинициализированное значение.
            SafetyReason reason = SafetyReason.NotChecked;

            Assert.DoesNotThrow(() => gate.ApproveStep(Pose, 1f, 0.0, null, out reason),
                "предохранитель обязан пережить отсутствие валидатора, а не падать");
            Assert.AreEqual(SafetyReason.None, reason);
        }

        /// <summary>
        /// ВЫКЛЮЧЕННЫЙ ПРЕДОХРАНИТЕЛЬ ПРОПУСКАЕТ ВСЁ — это его явный контракт, и он не должен
        /// меняться от исправлений выше.
        /// </summary>
        [Test]
        public void DisabledGate_ApprovesEverything()
        {
            var gate = new SafetyGate { Enabled = false };
            SafetyReason reason;

            Assert.IsTrue(gate.ApproveStep(Pose, 0f, 0.0, null, out reason),
                "выключенный предохранитель пропускает даже нулевой зазор");
        }
    }
}
