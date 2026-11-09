// ================================================================================================
//  KvPauseSafetyTests.cs — EditMode-тесты БЕЗОПАСНОСТИ НА ПАУЗЕ (сессия S44; переведены на поведение
//  в рамках P4-3, 12.11.2026).
//
//  ОТКУДА ЗАДАЧА. В проекте аварийный останов СТАВИТ ПАУЗУ: `KvFailureSimulator.StopMotion` вызывает
//  `features.EmergencyStop()` и `Motion.SetPaused(true)`.
//
//  ЧТО НАЙДЕНО 30.10.2026. На паузе `TrajectoryExecutor.Update` выходил СРАЗУ после `NotifyState()` —
//  то есть ДО расчёта зазора и ДО `ApproveStep`. Робот стоит, но никто не следит, что он стоит в
//  безопасном месте.
//
//  ЧЕМ ЭТО ПЛОХО. После аварии робот оказывается в состоянии, где контроль зазора и лимитов
//  ВЫКЛЮЧЕН. Если в это время оператор пододвинет стол, поставит манекен или включит ограничения —
//  система об этом не узнает. Первое, что произойдёт после снятия паузы, — движение в ставшую
//  опасной зону.
//
//  ЧТО ИЗМЕНИЛОСЬ В ЭТОМ ФАЙЛЕ (P4-3). Раньше здесь ЧИТАЛСЯ ИСХОДНИК: бралась подстрока 1500
//  символов от `private void CheckStandingPose`, и в ней искались слова `ApproveStep`, `ClearanceAt`,
//  `Stop(`. Такой тест ломается от переформулировки комментария и — что хуже — ПРОХОДИТ на сломанном
//  коде: строка `ApproveStep` могла остаться в недостижимой ветке. Теперь правила вынесены в ЧИСТЫЕ
//  функции рантайма, и проверяется РЕЗУЛЬТАТ:
//    • что вообще делается в кадре на паузе      → `TrajectoryExecutor.PausedFrameActionFor`
//    • какой сэмпл проверяется                  → `TrajectoryExecutor.StandingSampleIndex`
//    • останавливает ли отказ предохранителя     → `TrajectoryExecutor.StandingPoseNeedsStop`
//    • идёт ли время на паузе                    → `TrajectoryExecutor.PlayheadAfterFrame`
//    • ставит ли аварийный останов паузу         → `KvFailureSimulator.PlanEmergencyStop`
//
//  ДОБАВЛЕНО 12.11.2026 (дефекты A и B, тесты §4-§5 ниже). Проверка стоящей позы шла через
//  `SafetyGate.ApproveStep` и передавала туда ТЕКУЩИЙ `playhead` — а он на паузе заморожен
//  (`PlayheadAfterFrame`). Первое же правило `ApproveStep` («время должно идти вперёд») отказывало
//  с причиной `Watchdog`, и `Stop(reason)` превращал ПАУЗУ В АВАРИЙНЫЙ ОСТАНОВ с ложной причиной.
//  Второй дефект: шапка `SafetyGate` заявляла «порог зазора один», а `ApproveStep` исполнял
//  `minClearance * 0.5f` — предохранитель был мягче собственного описания.
//
//  ЧТО ПРОВЕРЯЕТСЯ ТЕПЕРЬ (по результату, а не по тексту):
//    §4 — пауза в движении НЕ даёт останова; после снятия паузы движение продолжается без ложного
//         `Watchdog`; настоящая опасность на паузе останавливает с ВЕРНОЙ причиной.
//    §5 — порог зазора ЕДИН: чуть выше порога проходит, чуть ниже — отклоняется, половина порога
//         отклоняется; мёртвый watchdog останавливает и на паузе.
// ================================================================================================

using KazistovVvFeatures;
using NUnit.Framework;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>Пауза не отключает контроль зазора и лимитов.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvPauseSafetyTests
    {
        private static readonly double[] Pose = { 0, 0, 0, 0, 0, 0 };

        /// <summary>Живой предохранитель: сторожевой таймер «дышит», чтобы дело было только в зазоре.</summary>
        private static SafetyGate LiveGate()
        {
            var gate = new SafetyGate { Enabled = true, minClearance = SafetyGate.DefaultMinClearance };
            gate.NotifyState();
            return gate;
        }

        /// <summary>
        /// ВАЛИДАТОР С ЗАДАННЫМИ ЛИМИТАМИ СУСТАВОВ — БЕЗ СЦЕНЫ.
        ///
        /// ЗАЧЕМ ЗЕРКАЛЬНО, А НЕ «ПРОСТО new PoseValidator()». Пороги (`Dof`, `Lower`, `Upper`)
        /// выставляются только в `PoseValidator.Init(RobotController)`, а `RobotController` —
        /// `MonoBehaviour`: без сцены валидатор остаётся «не готовым», и `WithinLimits` у него
        /// возвращает `true` для ЛЮБОЙ позы. Тест «поза вне лимитов останавливает» на таком
        /// валидаторе зеленел бы всегда — то есть не проверял бы ничего. Поэтому пороги
        /// прописываются напрямую в те же поля, которыми пользуется `WithinLimits`; приём уже
        /// применяется в проекте (`KvEnergyOptimalTests`, `KvTimeOptimalTests`).
        /// </summary>
        private static PoseValidator ValidatorWithLimits(float lowerDeg, float upperDeg)
        {
            const System.Reflection.BindingFlags anyInstance =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var v = new PoseValidator();
            System.Type t = typeof(PoseValidator);
            const int dof = 6;
            var lower = new float[dof];
            var upper = new float[dof];
            for (int i = 0; i < dof; i++) { lower[i] = lowerDeg; upper[i] = upperDeg; }

            t.GetProperty("Dof", anyInstance).GetSetMethod(true).Invoke(v, new object[] { dof });
            t.GetProperty("Lower", anyInstance).GetSetMethod(true).Invoke(v, new object[] { lower });
            t.GetProperty("Upper", anyInstance).GetSetMethod(true).Invoke(v, new object[] { upper });
            return v;
        }

        // ---------------------------------------------------------------- 1. ПРОВЕРКА НА ПАУЗЕ

        /// <summary>
        /// НА ПАУЗЕ ВЫПОЛНЯЕТСЯ ПРОВЕРКА. Главный тест файла: до исправления был просто `return`.
        ///
        /// ПРОВЕРЯЕТСЯ ПРАВИЛО, а не наличие слова в исходнике: в кадре, где движение стоит на паузе
        /// при живой траектории, действие обязано быть «проверить стоящую позу». Во всех остальных
        /// случаях (движение не запущено, траектории нет, паузы нет) проверять нечего — и это тоже
        /// закреплено, иначе «проверка» превратилась бы в безусловный вызов каждый кадр.
        /// </summary>
        [Test]
        public void PausedFrameStillChecksSafety()
        {
            Assert.AreEqual(TrajectoryExecutor.PausedFrameAction.VerifyStandingPose,
                TrajectoryExecutor.PausedFrameActionFor(true, true, true),
                "на паузе проверка безопасности НЕ выполняется — робот стоит без контроля зазора и " +
                "лимитов. Это особенно опасно после аварийного останова, который сам ставит паузу: " +
                "оператор поправит обстановку, а система не заметит");

            Assert.AreEqual(TrajectoryExecutor.PausedFrameAction.Nothing,
                TrajectoryExecutor.PausedFrameActionFor(false, true, true),
                "в движении этот путь не нужен: там проверка идёт на каждом шаге траектории");
            Assert.AreEqual(TrajectoryExecutor.PausedFrameAction.Nothing,
                TrajectoryExecutor.PausedFrameActionFor(true, false, true),
                "движение не запущено — проверять стоящую позу нечего");
            Assert.AreEqual(TrajectoryExecutor.PausedFrameAction.Nothing,
                TrajectoryExecutor.PausedFrameActionFor(true, true, false),
                "траектории нет — проверять нечего (иначе обращение к пустому пути)");
        }

        /// <summary>
        /// ПРОВЕРКА СТОЯЩЕЙ ПОЗЫ ДЕЙСТВИТЕЛЬНО ВЫЗЫВАЕТ ПРЕДОХРАНИТЕЛЬ, И ЕГО ОТКАЗ ВЕДЁТ К ОСТАНОВУ.
        ///
        /// ТРИ УТВЕРЖДЕНИЯ В ОДНОМ ТЕСТЕ, потому что по отдельности любое из них бессмысленно:
        ///   1) при зазоре выше порога стоящая поза признаётся безопасной (проверка не «стопит всё»);
        ///   2) при зазоре ниже порога безопасности требуется остановка — то есть проверка не просто
        ///      считает зазор, а передаёт отказ в останов (`Stop(reason)`);
        ///   3) причина отказа названа — `Clearance`. Без причины оператор увидит «СТОП» без повода,
        ///      и это ровно то, на что жаловались: «остановился, а почему — непонятно».
        ///
        /// 12.11.2026: время проигрывания из вызова УБРАНО — на паузе оно заморожено, и его
        /// проверка превращала паузу в аварийный останов (см. `SafetyGate.ApproveStandingPose`).
        /// </summary>
        [Test]
        public void StandingPoseCheckCallsTheGate()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;

            Assert.IsFalse(TrajectoryExecutor.StandingPoseNeedsStop(gate, Pose, 1f, null, out reason),
                "зазор 1 м — движение стоя признано безопасным; причина: " + SafetyGate.Describe(reason));

            SafetyGate second = LiveGate();
            Assert.IsTrue(TrajectoryExecutor.StandingPoseNeedsStop(second, Pose, 0.001f, null, out reason),
                "зазор 1 мм при пороге 15 мм — проверка обязана требовать остановки, иначе она " +
                "обнаружит опасность и промолчит");
            Assert.AreEqual(SafetyReason.Clearance, reason,
                "причина отказа не названа как «малый зазор»: " + SafetyGate.Describe(reason));
        }

        /// <summary>
        /// СЭМПЛ ДЛЯ ПРОВЕРКИ ВСЕГДА ВНУТРИ ПУТИ (−1 — проверять нечего).
        ///
        /// ЗАЧЕМ. `index` ведёт движение и может оказаться за последним сэмплом (или отрицательным на
        /// первом кадре). Обращение `active.Path[index]` за границей пути — это
        /// `IndexOutOfRangeException` посреди исполнения, то есть остановка БЕЗ причины в журнале:
        /// оператор видит «робот встал», а в консоли — исключение, которого он не читает.
        /// </summary>
        [Test]
        public void StandingSampleIndexStaysInsidePath()
        {
            Assert.AreEqual(-1, TrajectoryExecutor.StandingSampleIndex(3, 0),
                "пустого пути не соответствует ни один сэмпл");
            Assert.AreEqual(-1, TrajectoryExecutor.StandingSampleIndex(0, -5),
                "отрицательная длина пути — тоже «проверять нечего», а не исключение");
            Assert.AreEqual(0, TrajectoryExecutor.StandingSampleIndex(-3, 10),
                "отрицательный индекс прижимается к первому сэмплу");
            Assert.AreEqual(9, TrajectoryExecutor.StandingSampleIndex(50, 10),
                "индекс за путём прижимается к последнему сэмплу, а не выходит за массив");
            Assert.AreEqual(4, TrajectoryExecutor.StandingSampleIndex(4, 10),
                "индекс внутри пути не смещается — проверяется именно текущая поза");
        }

        // ---------------------------------------------------------------- 2. АВАРИЯ → ПАУЗА

        /// <summary>
        /// АВАРИЙНЫЙ ОСТАНОВ В ПРОЕКТЕ ДЕЙСТВИТЕЛЬНО СТАВИТ ПАУЗУ. Это ПРИЧИНА всего дефекта: если бы
        /// останов просто останавливал выполнение, пауза не была бы «опасным состоянием».
        ///
        /// РАНЬШЕ здесь искалась строка `SetPaused(true)` в теле `private void StopMotion`. Проверка
        /// была обманчива: вызов мог остаться в недостижимой ветке, и тест всё равно зеленел. Теперь
        /// проверяется НАМЕРЕНИЕ, которое исполняет `StopMotion`: останов обязан и погасить движение,
        /// и поставить паузу.
        /// </summary>
        [Test]
        public void EmergencyStopUsesPause()
        {
            KvFailureSimulator.KvEmergencyStopPlan plan = KvFailureSimulator.PlanEmergencyStop();

            Assert.IsTrue(plan.CallEmergencyStop,
                "аварийный останов больше не гасит движение — тогда этот тест надо пересмотреть " +
                "(останов обязан останавливать)");
            Assert.IsTrue(plan.PauseMotion,
                "аварийный останов больше не ставит паузу — а на паузе работают проверки зазора и " +
                "лимитов. Без неё после аварии контроль безопасности прекращается, и это был " +
                "исходный дефект S44");
        }

        // ---------------------------------------------------------------- 3. ВРЕМЯ НА ПАУЗЕ

        /// <summary>
        /// ПРОВЕРКА НА ПАУЗЕ НЕ ДВИГАЕТ ВРЕМЯ. Если бы `playhead` увеличивался, сработала бы проверка
        /// «время идёт вперёд» — и пауза сама себя останавливала бы «сторожевым таймером».
        ///
        /// РАНЬШЕ здесь искалась регулярка «нет `playhead +=`» в теле проверки. Теперь проверяется
        /// сама функция времени кадра: на паузе она обязана вернуть то же значение, что получила.
        /// </summary>
        [Test]
        public void StandingCheckDoesNotAdvanceTime()
        {
            Assert.AreEqual(0.5, TrajectoryExecutor.PlayheadAfterFrame(0.5, 0.1f, 1f, true), 1e-9,
                "на паузе время сдвинулось — тогда сработает проверка «время не идёт» и пауза сама " +
                "себя остановит");
            Assert.AreEqual(0.0, TrajectoryExecutor.PlayheadAfterFrame(0.0, 5f, 1f, true), 1e-9,
                "на паузе время не идёт даже при огромной длительности кадра");

            // Обратная сторона: вне паузы время ОБЯЗАНО идти, иначе робот не поедет вовсе.
            Assert.AreEqual(0.6, TrajectoryExecutor.PlayheadAfterFrame(0.5, 0.1f, 1f, false), 1e-6,
                "в движении время обязано идти вперёд, иначе траектория не проигрывается");
            Assert.AreEqual(0.005, TrajectoryExecutor.PlayheadAfterFrame(0.0, 0.1f, 0f, false), 1e-6,
                "нижняя граница множителя скорости (0,05) потеряна — при timeScale 0 движение встало бы");
        }

        // ---------------------------------------------------------------- 4. ПАУЗА ≠ АВАРИЙНЫЙ СТОП

        /// <summary>
        /// ПАУЗА В ДВИЖЕНИИ НЕ ДАЁТ ЛОЖНЫЙ АВАРИЙНЫЙ СТОП (дефект A, 12.11.2026).
        ///
        /// ЧТО БЫЛО. `CheckStandingPose` звал `ApproveStep` и передавал туда ТЕКУЩИЙ `playhead`, а
        /// на паузе он заморожен (`PlayheadAfterFrame`). Первое же правило `ApproveStep` — «время
        /// должно идти вперёд» — отказывало с причиной `Watchdog`, и `Stop(reason)` превращал паузу
        /// в АВАРИЙНЫЙ ОСТАНОВ. Оператор терял движение, которое всего лишь хотел приостановить, и
        /// видел ЛОЖНУЮ причину: watchdog жив, данные есть.
        ///
        /// ПОСЛЕДОВАТЕЛЬНОСТЬ КАК В ПРОДЕ: несколько кадров движения с РАСТУЩИМ временем (все
        /// одобрены) → кадр паузы с тем же замороженным временем, проверенный ТЕМ ЖЕ путём, которым
        /// ходит `CheckStandingPose` (`StandingPoseNeedsStop` → `SafetyGate.ApproveStandingPose`).
        /// </summary>
        [Test]
        public void PauseDuringMotion_IsNotAnEmergencyStop()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;
            double playhead = 0.0;

            for (int frame = 1; frame <= 4; frame++)
            {
                gate.NotifyState();                     // как в `Update`: данные живы каждый кадр
                playhead = TrajectoryExecutor.PlayheadAfterFrame(playhead, 0.02f, 1f, false);
                Assert.IsTrue(gate.ApproveStep(Pose, 1f, playhead, null, out reason),
                    "подготовка сорвана: кадр движения " + frame + " не одобрен (" +
                    SafetyGate.Describe(reason) + ") — тогда проверять паузу не на чем");
            }

            // Кадр паузы: время стоит на месте осознанно.
            double frozen = TrajectoryExecutor.PlayheadAfterFrame(playhead, 0.02f, 1f, true);
            Assert.AreEqual(playhead, frozen, 1e-12, "подготовка сорвана: на паузе время обязано стоять");

            gate.NotifyState();
            bool mustStop = TrajectoryExecutor.StandingPoseNeedsStop(gate, Pose, 1f, null, out reason);

            Assert.IsFalse(mustStop,
                "ПАУЗА ПРЕВРАТИЛАСЬ В АВАРИЙНЫЙ ОСТАНОВ с причиной «" + SafetyGate.Describe(reason) +
                "»: оператор нажал «Пауза» и потерял движение, а робот и журнал говорят об аварии");
            Assert.AreNotEqual(SafetyReason.Watchdog, reason,
                "причина отказа — «watchdog: нет данных», хотя данные есть: время на паузе " +
                "заморожено осознанно, и это не застревание плеера");
        }

        /// <summary>
        /// ПОСЛЕ СНЯТИЯ ПАУЗЫ ДВИЖЕНИЕ ПРОДОЛЖАЕТСЯ БЕЗ ЛОЖНОГО `Watchdog` (дефект A).
        ///
        /// ЧЕГО НЕ ДОЛЖНО БЫТЬ. Проверка на паузе не имеет права «съесть» учёт времени: если бы она
        /// записывала замороженное время как последнее, следующий кадр движения сравнивался бы с ним
        /// и получил бы отказ `Watchdog` — то есть дефект вернулся бы с другой стороны. Поэтому здесь
        /// проверяется не только отсутствие останова на паузе, но и ОДОБРЕНИЕ первого кадра после неё.
        /// </summary>
        [Test]
        public void AfterPauseResume_MotionContinuesWithoutFalseWatchdog()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;
            double playhead = 0.0;

            for (int frame = 1; frame <= 3; frame++)
            {
                gate.NotifyState();
                playhead = TrajectoryExecutor.PlayheadAfterFrame(playhead, 0.02f, 1f, false);
                Assert.IsTrue(gate.ApproveStep(Pose, 1f, playhead, null, out reason),
                    "подготовка сорвана: кадр движения " + frame + " не одобрен (" +
                    SafetyGate.Describe(reason) + ")");
            }

            // Несколько кадров паузы: время не двигается, проверка стоящей позы идёт каждый кадр.
            for (int frame = 1; frame <= 3; frame++)
            {
                gate.NotifyState();
                playhead = TrajectoryExecutor.PlayheadAfterFrame(playhead, 0.02f, 1f, true);
                Assert.IsFalse(TrajectoryExecutor.StandingPoseNeedsStop(gate, Pose, 1f, null, out reason),
                    "кадр паузы " + frame + " отказал с причиной «" + SafetyGate.Describe(reason) + "»");
            }

            // Пауза снята — время снова идёт. Первый же кадр обязан быть одобрен.
            gate.NotifyState();
            playhead = TrajectoryExecutor.PlayheadAfterFrame(playhead, 0.02f, 1f, false);
            Assert.IsTrue(gate.ApproveStep(Pose, 1f, playhead, null, out reason),
                "первый кадр после снятия паузы отклонён («" + SafetyGate.Describe(reason) + "»): " +
                "проверка на паузе испортила учёт времени, и движение не продолжится");
            Assert.AreEqual(SafetyReason.None, reason,
                "после снятия паузы причина отказа обязана быть «ок», получено: " +
                SafetyGate.Describe(reason));
        }

        /// <summary>
        /// НАСТОЯЩАЯ ОПАСНОСТЬ НА ПАУЗЕ ОСТАНАВЛИВАЕТ С ВЕРНОЙ ПРИЧИНОЙ.
        ///
        /// Убрав ложный отказ, легко убрать и настоящий: если бы вместе с правилом времени из
        /// проверки ушли ещё и лимиты/зазор, на паузе перестало бы ловиться именно то, ради чего
        /// она и нужна — «робот стоит, а обстановку могли изменить». Поэтому проверяются обе
        /// опасности и обе причины, а третье утверждение — обратное: безопасная поза не стопит.
        /// </summary>
        [Test]
        public void RealDangerOnPause_StopsWithCorrectReason()
        {
            SafetyReason reason;

            // (1) ЗАЗОР НИЖЕ ПОРОГА → `Clearance`.
            SafetyGate lowClearance = LiveGate();
            Assert.IsTrue(TrajectoryExecutor.StandingPoseNeedsStop(lowClearance, Pose, 0.001f, null, out reason),
                "зазор 1 мм на паузе не остановил — проверка обнаружит опасность и промолчит");
            Assert.AreEqual(SafetyReason.Clearance, reason,
                "причина обязана быть «малый зазор», получено: " + SafetyGate.Describe(reason));

            // (2) ПОЗА ВНЕ ЛИМИТОВ → `JointLimit`. Зазор здесь заведомо большой, поэтому причина
            // может быть только одна — иначе тест не различит две проверки.
            PoseValidator narrow = ValidatorWithLimits(-10f, 10f);
            SafetyGate limits = LiveGate();
            double[] beyond = { 45, 0, 0, 0, 0, 0 };
            Assert.IsTrue(TrajectoryExecutor.StandingPoseNeedsStop(limits, beyond, 1f, narrow, out reason),
                "поза вне лимитов суставов на паузе не остановила — лимиты проверяются только в движении");
            Assert.AreEqual(SafetyReason.JointLimit, reason,
                "причина обязана быть «лимит сустава», получено: " + SafetyGate.Describe(reason));

            // (3) ОБРАТНАЯ СТОРОНА: поза в пределах лимитов и с большим зазором — останова НЕТ.
            SafetyGate fine = LiveGate();
            Assert.IsFalse(TrajectoryExecutor.StandingPoseNeedsStop(fine, Pose, 1f, narrow, out reason),
                "безопасная поза на паузе признана опасной («" + SafetyGate.Describe(reason) +
                "») — тогда проверка останавливает всё подряд и оператор теряет движение без причины");
        }

        // ---------------------------------------------------------------- 5. ПОРОГ ЗАЗОРА

        /// <summary>
        /// ПОРОГ ЗАЗОРА ЕДИН: ЧУТЬ ВЫШЕ — ПРОХОДИТ, ЧУТЬ НИЖЕ — ОТКАЗ (дефект B, 12.11.2026).
        ///
        /// ЧТО БЫЛО. Шапка `SafetyGate` заявляла «теперь порог один», а `ApproveStep` исполнял
        /// `clearance < minClearance * 0.5f`: предохранитель пропускал позу с зазором ВДВОЕ меньше
        /// объявленного минимума. Наладчик, читая «минимум 15 мм», получал разрешение на 7,5 мм.
        ///
        /// ТЕСТ ЛОВИТ ВОЗВРАТ ПОЛОВИНЫ: зазор `minClearance * 0.5f` обязан ОТКЛОНЯТЬСЯ, а зазор
        /// ровно на пороге и выше — проходить (для движения и для стоящей позы одинаково).
        /// </summary>
        [Test]
        public void ClearanceThresholdIsSingleForMotionAndStanding()
        {
            SafetyGate gate = LiveGate();
            SafetyReason reason;
            float min = gate.minClearance;

            Assert.AreEqual(gate.minClearance, gate.StandingClearance, 1e-9f,
                "порог стоящей позы снова отличается от порога движения — «порог один» опять только на словах");

            // СТОЯЩАЯ ПОЗА: граница включительная — чуть выше проходит, чуть ниже отклоняется.
            Assert.IsTrue(gate.ApproveStandingPose(Pose, min + 0.0001f, null, out reason),
                "зазор чуть выше порога отклонён на паузе: " + SafetyGate.Describe(reason));
            Assert.IsFalse(gate.ApproveStandingPose(Pose, min - 0.0001f, null, out reason),
                "зазор чуть ниже порога пропущен на паузе — предохранитель мягче собственного описания");
            Assert.AreEqual(SafetyReason.Clearance, reason,
                "причина отказа обязана быть «малый зазор», получено: " + SafetyGate.Describe(reason));

            // ДВИЖЕНИЕ: тот же порог. Половина порога — ОТКАЗ (ровно это и было дефектом B).
            SafetyGate movingHalf = LiveGate();
            float half = min * 0.5f;
            Assert.IsFalse(movingHalf.ApproveStep(Pose, half, 0.0, null, out reason),
                "предохранитель снова пропускает зазор вдвое меньше минимума (" + (half * 1000f) +
                " мм при пороге " + (min * 1000f) + " мм) — половина порога вернулась в код");
            Assert.AreEqual(SafetyReason.Clearance, reason,
                "причина отказа обязана быть «малый зазор», получено: " + SafetyGate.Describe(reason));

            SafetyGate movingLow = LiveGate();
            Assert.IsFalse(movingLow.ApproveStep(Pose, min - 0.0001f, 0.02, null, out reason),
                "зазор чуть ниже порога пропущен в движении: " + SafetyGate.Describe(reason));

            SafetyGate movingOk = LiveGate();
            Assert.IsTrue(movingOk.ApproveStep(Pose, min + 0.0001f, 0.04, null, out reason),
                "зазор чуть выше порога отклонён в движении: " + SafetyGate.Describe(reason));

            // ЗАПАС ПЛАНИРОВЩИКА (4/3 от порога) обязан проходить: ужесточение не должно отсекать
            // корректно построенные планы — ради этого планировщик и держит запас.
            SafetyGate planned = LiveGate();
            Assert.IsTrue(planned.ApproveStep(Pose, SafetyGate.DefaultPlanningClearance, 0.06, null, out reason),
                "зазор планировщика (4/3 от минимума) не проходит единый порог («" +
                SafetyGate.Describe(reason) + "») — тогда ужесточение ломает корректные планы");
        }

        /// <summary>
        /// МЁРТВЫЙ WATCHDOG ОСТАНАВЛИВАЕТ И НА ПАУЗЕ.
        ///
        /// Убрав правило времени, нельзя потерять правило живости данных: на паузе оно как раз и
        /// остаётся главным («система ещё слышит приводы»). Без него стоящий робот оказывается без
        /// надзора — тот же класс дефекта, что был у S44, только тише.
        /// </summary>
        [Test]
        public void DeadWatchdog_StopsEvenOnPause()
        {
            // `NotifyState` не вызывался ни разу: данных о состоянии нет вовсе.
            var gate = new SafetyGate { Enabled = true, minClearance = SafetyGate.DefaultMinClearance };
            SafetyReason reason;

            Assert.IsTrue(TrajectoryExecutor.StandingPoseNeedsStop(gate, Pose, 1f, null, out reason),
                "мёртвый watchdog на паузе не остановил — стоящий робот остался без контроля данных");
            Assert.AreEqual(SafetyReason.Watchdog, reason,
                "причина обязана быть «watchdog: нет данных», получено: " + SafetyGate.Describe(reason));

            // И в движении — то же самое: правило не должно разойтись между двумя путями.
            Assert.IsFalse(gate.ApproveStep(Pose, 1f, 0.02, null, out reason),
                "мёртвый watchdog в движении больше не останавливает");
            Assert.AreEqual(SafetyReason.Watchdog, reason,
                "причина обязана быть «watchdog: нет данных», получено: " + SafetyGate.Describe(reason));
        }
    }
}
