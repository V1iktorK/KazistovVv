using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using KazistovVvFeatures;
using TrajectoryCore;

namespace KazistovVvTests
{
    /// <summary>
    /// ФИКС 4 (§20). PLAY-MODE ТЕСТЫ ПОЛНОГО СЦЕНАРИЯ.
    ///
    /// ЗАЧЕМ. В §13.11 честно записано: `KvCalibrationService.SolveTcp`, `KvTimeOptimal.Compute`,
    /// `KvEnergyOptimal.Compute`, `KvPayloadCalculator.Evaluate/JointTorques`, `KvRobotExport.Export`
    /// и `KvTrajMath.Analyze/Retime/Energy/PathLength` покрыты только защитными ветвями
    /// (EditMode-тесты вызывают их без робота) — их «живые» ветви требуют загруженной сцены
    /// с роботом и готового `flow.Validator`. Этот тест закрывает ровно эту дыру.
    ///
    /// ЧТО ДЕЛАЕТ (один сценарий, как просило ТЗ):
    ///   MainScene → ожидание `flow.Validator.Ready` → точка → варианты траекторий →
    ///   сглаживание → время-оптимальная → эко-профиль → экспорт в язык робота → расчёт нагрузки.
    ///   Каждый шаг обязан выполниться БЕЗ исключений; проверяются и возвращаемые значения.
    ///
    /// ПОЧЕМУ `[UnityPlatform(RuntimePlatform.WindowsEditor)]`. В `-nographics`/batch-PlayMode
    /// на этой машине вход в PlayMode не завершается (измерено 15.09.2026: процесс ушёл
    /// в бесконечный цикл плеера и был снят через ~40 минут — §0.7.7, §DshScreenshotsDiag).
    /// Поэтому тест помечен платформой и запускается ТОЛЬКО в живом редакторе:
    ///   Window → General → Test Runner → PlayMode → Run All.
    ///
    /// ЧТО НЕ ДЕЛАЕТ: не сохраняет сцену и не меняет ассеты — только читает их (сцена
    /// загружается в PlayMode, файл `MainScene.unity` не перезаписывается). Единственная
    /// запись на диск — файл экспорта робота (штатное поведение экспорта) в
    /// `Документы\KazistovVv\robot_export`.
    ///
    /// ТРЕБОВАНИЕ К РЕЖИМУ: `Project Settings → Editor → «Enable playmode tests for all
    /// assemblies»` (ProjectSettings.asset: `playModeTestRunnerEnabled: 1`) — без этого
    /// PlayMode-тесты в предопределённой сборке `Assembly-CSharp` не находятся раннером.
    /// </summary>
    [UnityPlatform(RuntimePlatform.WindowsEditor)]
        [Category("Slow")]   // Slow: см. KvTestCategories в EditMode-сборке
    public class KvFlowPlayModeTests
    {
        /// <summary>Сцена проекта (зарегистрирована в EditorBuildSettings).</summary>
        private const string SceneName = "MainScene";

        /// <summary>Ожидание готовности робота (валидатор + хабы этапов).</summary>
        private const float ReadyTimeoutSec = 60f;

        /// <summary>Ожидание генерации вариантов после подтверждения точки.</summary>
        private const float PlanTimeoutSec = 45f;

        /// <summary>Требование ТЗ: постобработка и сервисы обязаны укладываться в 30 с.</summary>
        private const float FastChainSec = 30f;

        private TrajectoryFlowController flow;
        private FeatureHub hub;
        private FreeFlyCameraController cam;
        private PoseValidator v;

        // ================================================================== подготовка

        /// <summary>
        /// Загрузить MainScene и дождаться `flow.Validator.Ready`. Сцена уже может быть
        /// загружена (оператор держит её открытой) — тогда повторно не грузим.
        ///
        /// ФИКС (§21, найдено первым прогоном). Test Framework считает ПРОВАЛОМ любую красную
        /// строку в консоли. При загрузке MainScene HDRP пишет `[Error] No more space in
        /// Reflection Probe Atlas…` — это НЕ ошибка проверяемого кода и НЕ предмет этого теста
        /// (атлас отражений в настройках HDRP; в проекте он мал для сцены). Поэтому проверка
        /// логов гасится РОВНО на время загрузки сцены и сразу включается обратно: дальше
        /// любая ошибка или исключение от самой цепочки снова валит тест.
        ///
        /// Первый прогон (17.09.2026, `TestResults.xml`) упал именно на этой строке:
        /// `SetUp : Unhandled log message: '[Error] No more space in Reflection Probe Atlas…'`.
        /// </summary>
        [UnitySetUp]
        public IEnumerator LoadMainSceneAndWaitForValidator()
        {
            // P4-9 (12.11.2026). ЧТО БЫЛО: на время загрузки сцены СТАВИЛСЯ
            // `LogAssert.ignoreFailingMessages = true` — то есть глушились ВСЕ ошибки лога
            // целиком. Ошибка самой загрузки сцены (сломанный префаб, отсутствующий ассет,
            // исключение в Awake) при этом молча проглатывалась, и тест «проходил».
            // ЧТО СТАЛО: ожидаемый шум HDRP отсекается ПО ПОДСТРОКЕ в собственном приёмнике,
            // а любая ДРУГАЯ ошибка за время загрузки собирается и валит тест с текстом.
            StartLogCapture();
            LogAssert.ignoreFailingMessages = true;

            if (SceneManager.GetActiveScene().name != SceneName)
            {
                SceneManager.LoadScene(SceneName, LoadSceneMode.Single);
                yield return null;
                yield return null;      // даём Awake/Start пройти у всех компонентов сцены
            }

            float deadline = Time.realtimeSinceStartup + ReadyTimeoutSec;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (flow == null) flow = UnityEngine.Object.FindAnyObjectByType<TrajectoryFlowController>();
                if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<FreeFlyCameraController>();
                hub = FeatureHub.Current;
                if (flow != null && flow.Validator != null && flow.Validator.Ready &&
                    KvStageHub2.Current != null && KvStageHub3.Current != null)
                    break;
                yield return null;
            }

            // Сцена поднялась — дальше консоль проверяется СТРОГО.
            StopLogCapture();
            LogAssert.ignoreFailingMessages = false;
            AssertNoUnexpectedLoadErrors();

            Assert.IsNotNull(flow, "в сцене «" + SceneName + "» нет TrajectoryFlowController");
            Assert.IsNotNull(flow.Validator, "поток не привязал робота (flow.Validator = null)");
            Assert.IsTrue(flow.Validator.Ready, "flow.Validator.Ready не стал true за " +
                                                ReadyTimeoutSec.ToString("0") + " с");
            v = flow.Validator;
        }

        // ================================================================== отсев известного шума

        /// <summary>
        /// ИЗВЕСТНЫЙ ШУМ ЗАГРУЗКИ СЦЕНЫ (P4-9). Единственная строка — предупреждение HDRP об
        /// атласе отражений: атлас в настройках HDRP мал для этой сцены. Это НЕ ошибка
        /// проверяемого кода и НЕ предмет теста. Всё остальное ошибкой считается.
        /// </summary>
        private static readonly string[] KnownSceneLoadNoise =
        {
            "No more space in Reflection Probe Atlas"
        };

        private readonly List<string> unexpectedLogErrors = new List<string>();
        private bool capturingLog;

        /// <summary>
        /// ПРИЁМНИК ЛОГА: копит ТОЛЬКО ошибки и исключения, кроме известного шума. Висит на
        /// `Application.logMessageReceived` — то есть видит те же сообщения, что и Test Framework,
        /// но не мешает ему (флаг `ignoreFailingMessages` на время загрузки всё равно включён).
        /// </summary>
        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (!capturingLog) return;
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            if (!string.IsNullOrEmpty(condition))
            {
                for (int i = 0; i < KnownSceneLoadNoise.Length; i++)
                    if (condition.Contains(KnownSceneLoadNoise[i])) return;
            }
            unexpectedLogErrors.Add(type + ": " + condition);
        }

        private void StartLogCapture()
        {
            unexpectedLogErrors.Clear();
            capturingLog = true;
            Application.logMessageReceived += OnLogMessage;
        }

        private void StopLogCapture()
        {
            capturingLog = false;
            Application.logMessageReceived -= OnLogMessage;
        }

        /// <summary>Загрузка сцены не имеет права молча проглотить ни одной ошибки.</summary>
        private void AssertNoUnexpectedLoadErrors()
        {
            Assert.AreEqual(0, unexpectedLogErrors.Count,
                "за время загрузки сцены были ошибки, кроме известного шума HDRP:\n" +
                string.Join("\n", unexpectedLogErrors.ToArray()));
        }

        /// <summary>
        /// Страховка: проверка консоли обязана вернуться в строгий режим, даже если сценарий
        /// упал (иначе следующее падение «промолчит» из-за погашенного флага).
        /// </summary>
        [UnityTearDown]
        public IEnumerator RestoreStrictLogChecks()
        {
            StopLogCapture();
            LogAssert.ignoreFailingMessages = false;
            yield return null;
        }

        // ================================================================== сценарий целиком

        /// <summary>
        /// ПОЛНЫЙ СЦЕНАРИЙ ТЗ: точка → варианты траекторий → сглаживание → время-оптимальная →
        /// эко-профиль → экспорт → нагрузка. Все шаги — «живые» ветви модулей из §13.11.
        /// </summary>
        [UnityTest]
        [Timeout(300000)]
        public IEnumerator FullChain_PointTrajectories_SmoothTimeOptimalEcoExportPayload()
        {
            // ---------------------------------------------------------- 1. точка и траектории
            Vector3 point = PickReachablePoint();
            Click(point);
            yield return WaitForTrajectories();

            int count = flow.State.candidates.Count;
            Debug.Log("[PlayMode] вариантов траекторий: " + count + " из " + flow.PlanTargetCount +
                      " · попыток " + flow.PlanAttempts + " · дубликатов " + flow.DuplicatesFiltered);
            Assert.Greater(count, 0, "планировщик не дал ни одного варианта для точки " + point);
            Assert.LessOrEqual(count, Mathf.Max(1, flow.PlanTargetCount),
                "вариантов больше, чем запрошено у планировщика");

            TrajectoryCandidate candidate = flow.State.candidates[0];
            Assert.IsNotNull(candidate, "первый вариант пуст");
            Assert.IsNotNull(candidate.plan, "у первого варианта нет плана");
            Assert.IsNotNull(candidate.plan.Path, "у плана нет пути");
            Assert.Greater(candidate.plan.Path.Length, 2, "в плане меньше трёх сэмплов");
            Assert.IsNotNull(candidate.plan.Times, "у плана нет времён");
            Assert.AreEqual(candidate.plan.Path.Length, candidate.plan.Times.Length,
                "число сэмплов и времён плана расходится");

            float t0 = Time.realtimeSinceStartup;

            // ---------------------------------------------------------- 2. ядро метрик пути
            KvTrajStats before = KvTrajMath.Analyze(v, candidate.plan, null, null, v.BasePosition);
            Assert.IsTrue(before.valid, "KvTrajMath.Analyze не разобрал план планировщика");
            Assert.Greater(before.time, 0f, "Analyze вернул нулевое время");

            float length = KvTrajMath.PathLength(v, candidate.plan.Path);
            Assert.Greater(length, 0f, "длина пути TCP нулевая");

            PlannedTrajectory retimed = KvTrajMath.Retime(v, candidate.plan, new KvMotionLimits(),
                1f, 1f, "игра: время-оптимальная");
            Assert.IsNotNull(retimed, "KvTrajMath.Retime не построил профиль");
            Assert.IsNotNull(retimed.Times, "Retime не заполнил времена");
            Assert.Greater(retimed.Time, 0.0, "Retime вернул нулевое время");

            KvTrajStats retimedStats = KvTrajMath.Analyze(v, retimed, null, null, v.BasePosition);
            Assert.IsTrue(retimedStats.valid, "Analyze не разобрал перепараметризованный план");

            float peakPower;
            float energy = KvTrajMath.Energy(v, retimed, new KvEnergyModel(), v.BasePosition, out peakPower);
            Assert.GreaterOrEqual(energy, 0f, "энергия отрицательна");
            Assert.GreaterOrEqual(peakPower, 0f, "пиковая мощность отрицательна");

            // ---------------------------------------------------------- 3. сглаживание
            KvStageHub2 stages2 = KvStageHub2.Current;
            Assert.IsNotNull(stages2, "KvStageHub2 не поднят в сцене");
            Assert.IsNotNull(stages2.Smoothing, "служба сглаживания недоступна");
            bool smoothed = stages2.Smoothing.ApplySelected(true);
            Assert.IsTrue(smoothed, "сглаживание выбранного варианта не применилось");

            // ---------------------------------------------------------- 4. время-оптимальная
            Assert.IsNotNull(stages2.TimeOptimal, "служба время-оптимальной траектории недоступна");
            KvTimeOptimal.Draft fast = stages2.TimeOptimal.Compute(candidate, true);
            Assert.IsNotNull(fast, "KvTimeOptimal.Compute не вернул черновик");
            Assert.IsNotNull(fast.plan, "у время-оптимального черновика нет плана");
            Assert.IsTrue(fast.stats.valid, "метрики время-оптимальной траектории не посчитаны");
            Debug.Log("[PlayMode] время-оптимальная: " + fast.original.time.ToString("0.000") +
                      " с → " + fast.stats.time.ToString("0.000") + " с (выигрыш " +
                      fast.timeGain.ToString("0.0") + " %) · " + fast.jerkReport);
            Assert.IsTrue(stages2.TimeOptimal.ApplySelected(), "время-оптимальный профиль не применён");

            // ---------------------------------------------------------- 5. эко-профиль
            Assert.IsNotNull(stages2.Energy, "служба эко-профиля недоступна");
            KvEnergyOptimal.Draft eco = stages2.Energy.Compute(candidate, true);
            Assert.IsNotNull(eco, "KvEnergyOptimal.Compute не вернул черновик");
            Assert.IsNotNull(eco.plan, "у эко-профиля нет плана");
            Assert.Greater(eco.trials, 0, "эко-профиль не сделал ни одной попытки подбора");
            Debug.Log("[PlayMode] эко-профиль: " + eco.originalEnergy.ToString("0.00") + " Дж → " +
                      eco.energy.ToString("0.00") + " Дж (экономия " + eco.savings.ToString("0.0") +
                      " %, попыток " + eco.trials + ")");
            Assert.IsTrue(stages2.Energy.ApplySelected(), "эко-профиль не применён");

            // ---------------------------------------------------------- 6. экспорт и нагрузка
            KvStageHub3 stages3 = KvStageHub3.Current;
            Assert.IsNotNull(stages3, "KvStageHub3 не поднят в сцене");

            Assert.IsNotNull(stages3.Exporter, "экспортёр недоступен");
            string file = stages3.Exporter.ExportSelected();
            Assert.IsFalse(string.IsNullOrEmpty(file), "экспорт не создал файл");
            Assert.IsTrue(System.IO.File.Exists(file), "файл экспорта не найден на диске: " + file);
            Debug.Log("[PlayMode] экспорт (" + stages3.Exporter.LanguageLabel + "): " + file +
                      " · строк " + stages3.Exporter.LastLines);

            Assert.IsNotNull(stages3.Payload, "калькулятор нагрузки недоступен");
            double[] q = v.CopyCurrent();
            KvPayloadResult payload = stages3.Payload.Evaluate(q);
            Assert.IsNotNull(payload, "KvPayloadCalculator.Evaluate не вернул результат");
            Assert.IsTrue(payload.valid, "расчёт нагрузки невалиден: " + payload.why);
            Assert.Greater(payload.maxKg, 0f, "предельная масса груза нулевая");

            float[] torque;
            float[] load01;
            Vector3 toolForce;
            bool torques = stages3.Payload.JointTorques(q, Mathf.Max(0.1f, payload.maxKg * 0.25f),
                out torque, out load01, out toolForce);
            Assert.IsTrue(torques, "KvPayloadCalculator.JointTorques не посчитал моменты");
            Assert.IsNotNull(torque, "массив моментов пуст");
            Assert.AreEqual(v.Dof, torque.Length, "моментов не столько, сколько осей");
            Assert.AreEqual(v.Dof, load01.Length, "долей загрузки не столько, сколько осей");
            Assert.Greater(toolForce.magnitude, 0f, "сила на инструменте нулевая");

            // ---------------------------------------------------------- 7. калибровка TCP
            Assert.IsNotNull(stages3.Calibration, "калибровочная служба недоступна");
            bool tcpSolved = stages3.Calibration.SolveTcp();
            if (!tcpSolved)
            {
                // Штатный исход: точек калибровки в сцене нет. Важно, что вызов не бросил
                // исключение и служба честно отчиталась, а не сделала вид, что решила.
                Assert.IsFalse(stages3.Calibration.TcpSolved,
                    "SolveTcp вернул false, но служба считает TCP решённым");
                Debug.Log("[PlayMode] калибровка TCP: точек нет — метод вернул false без исключения");
            }

            // ---------------------------------------------------------- 8. «быстро»
            float elapsed = Time.realtimeSinceStartup - t0;
            Debug.Log("[PlayMode] постобработка и сервисы: " + elapsed.ToString("0.0") + " с");
            Assert.Less(elapsed, FastChainSec,
                "сценарий постобработки занял больше " + FastChainSec.ToString("0") + " с");

            // Возвращаем поток в исходное состояние (сглаживание/время/эко отменяются),
            // чтобы тест не оставлял сцену изменённой для следующего прогона.
            stages2.Energy.ResetSelected();
            stages2.TimeOptimal.ResetSelected();
            stages2.Smoothing.ResetSelected();
        }

        // ================================================================== вспомогательное

        /// <summary>
        /// Точка в рабочей зоне робота: пробуем несколько смещений вокруг базы и берём первую,
        /// для которой IK сходится и поза проходит лимиты (та же схема, что у стендов проекта).
        /// </summary>
        private Vector3 PickReachablePoint()
        {
            Vector3 b = v.BasePosition;
            RobotController robot = flow.Robot;
            Vector3 origin = robot != null ? robot.transform.position : b;

            Vector3[] probes =
            {
                origin + new Vector3(0.28f, 0.30f, -0.30f),
                origin + new Vector3(-0.28f, 0.30f, -0.30f),
                origin + new Vector3(0.34f, 0.42f, -0.20f),
                origin + new Vector3(0.00f, 0.34f, -0.40f),
                b + new Vector3(0.30f, 0.28f, -0.24f),
                v.TcpAt(v.CopyCurrent())
            };

            double[] seed = v.CopyCurrent();
            for (int i = 0; i < probes.Length; i++)
            {
                double[] q;
                string why;
                if (KvPlanKit.SolvePoseForPoint(v, hub != null ? hub.World : null, probes[i],
                        Vector3.down, seed, out q, out why))
                    return probes[i];
            }

            // Ни одна проба не прошла IK — сообщаем прямо, с какой точки начинали.
            Assert.Fail("в рабочей зоне робота не найдена достижимая точка (проб: " +
                        probes.Length + ", старт " + probes[probes.Length - 1] + ")");
            return probes[0];
        }

        /// <summary>Кадр прицеливания (как в стендах проекта): луч смотрит на робота.</summary>
        private void AimCameraAtRobot()
        {
            if (cam == null || flow.Robot == null) return;
            Vector3 center = flow.Robot.transform.position;
            Vector3 from = center + new Vector3(0f, 1.15f, -1.5f);
            cam.transform.position = from;
            cam.transform.rotation = Quaternion.LookRotation(
                (center + Vector3.up * 0.15f - from).normalized, Vector3.up);
        }

        /// <summary>Один кадр потока: прицел + подтверждение (ЛКМ) по точке.</summary>
        private void Click(Vector3 point)
        {
            AimCameraAtRobot();
            // Esc — чистое состояние, затем подтверждение точки.
            flow.UpdateAim(Vector3.zero, false, false, true, false, false, Vector3.up, false,
                false, Vector3.zero, false);
            flow.UpdateAim(point, true, true, false, true, false, Vector3.up, true,
                false, Vector3.zero, false);
        }

        /// <summary>Ждать, пока поток закончит генерацию вариантов (держим прицел на точке).</summary>
        private IEnumerator WaitForTrajectories()
        {
            float deadline = Time.realtimeSinceStartup + PlanTimeoutSec;
            bool sawGenerating = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (flow.Generating) sawGenerating = true;
                if (!flow.Generating && sawGenerating && flow.State.candidates.Count > 0) yield break;
                // Прицел держим, пока идёт просчёт: поток считает точку по текущему лучу.
                flow.UpdateAim(flow.State.point, true, false, false, true, false, Vector3.up, true,
                    false, Vector3.zero, false);
                yield return null;
            }
            Assert.Fail("генерация вариантов не завершилась за " + PlanTimeoutSec.ToString("0") +
                        " с (вариантов: " + flow.State.candidates.Count +
                        ", Generating: " + flow.Generating + ")");
        }
    }
}

