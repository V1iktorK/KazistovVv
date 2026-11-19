using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 3 ТЗ: QUICK START («Показать демо» в главном меню).
    ///
    /// Автоматический сценарий показывает полный цикл работы платформы, пока оператор
    /// просто смотрит:
    ///   точка над столешницей → планировщик строит 8 вариантов → по каждой траектории
    ///   идут фантомы → выбирается лучший вариант → робот едет по траектории → итог.
    ///
    /// Каждый шаг подписан на экране (карточка этапа 2), у шага есть минимальное время
    /// показа и необязательное условие завершения (состояние State Machine) — сценарий
    /// НЕ ломается, если планирование заняло больше времени: он просто ждёт.
    ///
    /// По завершении предлагается пройти туториал (кнопка ведёт в этап 2).
    /// Прервать можно в любой момент — кнопкой «Остановить демонстрацию» или Esc.
    ///
    /// Ничего в существующей логике не переписывается: демонстрация пользуется теми же
    /// публичными методами потока (`LockPointFromUi`, `SelectCandidateByIndex`),
    /// что и ручная работа оператора.
    /// </summary>
    public class KvQuickStart : MonoBehaviour
    {
        public float pointHeight = 0.10f;       // точка над столешницей, м
        public float waitForRobot = 25f;        // сколько ждать привязки робота, с
        public float waitForPlanning = 75f;     // сколько ждать 8 вариантов, с
        public float waitForMotion = 240f;      // сколько ждать проезда робота, с
        public float finalPause = 4f;           // пауза на «готово», с

        public event Action<string> Message;

        private class DemoStep
        {
            public string TextKey;
            public float MinTime;
            public float MaxTime;
            public Action Enter;
            public Func<bool> Done;
        }

        private readonly List<DemoStep> steps = new List<DemoStep>();
        private TrajectoryFlowController flow;
        private KvTutorial overlay;
        private FeatureHub features;

        private bool running;
        private int index;
        private float stepTime;
        private float totalTime;
        private string abortReason = "";
        private int chosenVariant = -1;
        private bool motionSeen;

        public bool Running { get { return running; } }
        public int StepIndex { get { return index; } }
        public int StepCount { get { return steps.Count; } }
        public float TotalTime { get { return totalTime; } }

        public void Bind(TrajectoryFlowController controller, KvTutorial tutorialOverlay)
        {
            flow = controller;
            overlay = tutorialOverlay;
            BuildSteps();
        }

        // ================================================================== сценарий

        private void BuildSteps()
        {
            steps.Clear();

            // --- 1. робот должен быть определён (прицел уже навёл поток)
            steps.Add(new DemoStep
            {
                TextKey = "demo.line1",
                MinTime = 1.2f,
                MaxTime = waitForRobot,
                Enter = delegate { EnsureMetricsPanel(); },
                Done = delegate
                {
                    if (flow == null) return false;
                    if (flow.Robot == null)
                    {
                        abortReason = KvLocExtra.T("demo.needrobot",
                            "Робот не определён: наведите красный лазер (Z) на стенд с роботом");
                        return false;
                    }
                    return true;
                }
            });

            // --- 2. точка над столешницей + расчёт вариантов
            steps.Add(new DemoStep
            {
                TextKey = "demo.line2",
                MinTime = 1.5f,
                MaxTime = waitForPlanning,
                Enter = delegate { PlaceDemoPoint(); },
                Done = delegate
                {
                    if (flow == null || flow.State == null) return false;
                    return flow.State.phase == FlowState.TrajectoriesShown ||
                           flow.State.phase == FlowState.PhantomsMoving ||
                           flow.State.phase == FlowState.RobotMoving;
                }
            });

            // --- 3. фантомы идут по всем вариантам (в проекте едут все 8 сразу)
            steps.Add(new DemoStep
            {
                TextKey = "demo.line3",
                MinTime = 5f,
                MaxTime = 12f
            });

            // --- 4. метрики вариантов
            steps.Add(new DemoStep
            {
                TextKey = "demo.line4",
                MinTime = 4f,
                MaxTime = 8f
            });

            // --- 5. выбор лучшего варианта и движение робота
            steps.Add(new DemoStep
            {
                TextKey = "demo.line5",
                MinTime = 2f,
                MaxTime = waitForMotion,
                Enter = delegate { SelectBestVariant(); },
                Done = delegate
                {
                    if (flow == null || flow.State == null) return false;

                    // Сначала ждём фактического старта движения: до него поток стоит
                    // в состоянии «показаны траектории», и без этого флага шаг
                    // завершился бы мгновенно.
                    bool moving = flow.State.phase == FlowState.RobotMoving ||
                                  (flow.Motion != null && flow.Motion.IsRunning) ||
                                  flow.ExternalMotionRunning;
                    if (moving)
                    {
                        motionSeen = true;
                        return false;
                    }
                    if (!motionSeen) return false;
                    return flow.State.phase == FlowState.Idle ||
                           flow.State.phase == FlowState.TrajectoriesShown;
                }
            });

            // --- 6. итог и предложение пройти туториал
            steps.Add(new DemoStep
            {
                TextKey = "demo.line6",
                MinTime = finalPause,
                MaxTime = 0f,
                Enter = delegate { OfferTutorial(); }
            });
        }

        /// <summary>
        /// Запустить демонстрацию (кнопка «Показать демо»).
        /// ВАЖНО: метод назван `Begin`, а НЕ `Start`: у MonoBehaviour `Start` — служебное
        /// сообщение Unity, и движок вызвал бы его сам при первом кадре (без привязки
        /// к потоку этапов, то есть на пустом сценарии).
        /// </summary>
        public bool Begin()
        {
            if (running) return false;

            // ДЕФЕКТ ВОЛНЫ 5 («Показать демо», лист «Сервис/Показать демо»): шаги строит только
            // `Bind`, а его зовёт `KvStageHub2.BindServices`, который рано выходит, пока
            // планировщик не готов (нет робота/стола). Кнопка демо при этом ДОСТУПНА ВСЕГДА, и
            // нажатие до готовности падало `IndexOutOfRangeException` на `steps[0]` — вместо
            // designed-поведения «демонстрация начнётся с шага „наведите лазер“» (см. комментарий
            // ниже про ожидание робота). Теперь шаги достраиваются на месте.
            if (steps.Count == 0) BuildSteps();

            if (flow == null)
            {
                KazistovVvUIManager ui = KazistovVvUIManager.Instance;
                if (ui != null) flow = ui.Flow;
            }
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                // Робот ещё не определён — демонстрация начнётся с шага «наведите лазер».
                Report("демонстрация: ожидание робота");
            }

            features = FeatureHub.Current;
            running = true;
            index = 0;
            totalTime = 0f;
            stepTime = 0f;
            abortReason = "";
            chosenVariant = -1;
            motionSeen = false;

            // Сценарий всегда начинается с чистой сцены: точка и траектории прежней
            // работы оператора не мешают показу (поток сбрасывается штатным Esc-путём).
            try
            {
                if (flow != null && (flow.State.hasPoint || flow.State.candidates.Count > 0))
                    flow.ResetFlow("демонстрация: старт с чистой сцены");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[QuickStart] сброс перед демонстрацией: " + e.Message);
            }

            EnterStep(0);
            Report("демонстрация запущена (" + steps.Count + " шагов)");
            return true;
        }

        /// <summary>Остановить демонстрацию (кнопка или Esc).</summary>
        public void StopDemo(string why)
        {
            if (!running) return;
            running = false;
            if (overlay != null) overlay.HideCaption();
            KazistovVvUIManager.SetPlanStatus("Демонстрация остановлена: " + why, KvTheme.Warn);
            Report("демонстрация остановлена (" + why + ") · время показа " +
                   totalTime.ToString("0.0") + " с");
        }

        public void Toggle()
        {
            if (running) StopDemo("кнопка интерфейса");
            else Begin();
        }

        // ================================================================== шаги

        private void EnterStep(int i)
        {
            index = i;
            stepTime = 0f;
            DemoStep step = steps[i];
            if (step.Enter != null)
            {
                try { step.Enter(); }
                catch (Exception e) { Debug.LogWarning("[QuickStart] шаг " + i + ": " + e.Message); }
            }
            ShowCard(step);
        }

        private void ShowCard(DemoStep step)
        {
            if (overlay == null) return;
            string body = KvLocExtra.T(step.TextKey, "");
            overlay.ShowCaption(
                KvLocExtra.T("demo.title", "Демонстрация работы") + "   ·   " +
                KvLocExtra.T("demo.running", "Демонстрация идёт — наблюдать можно, ничего нажимать не нужно"),
                body,
                "Esc — " + KvLocExtra.T("demo.stop", "Остановить демонстрацию"),
                new[] { KvLocExtra.T("demo.stop", "Остановить демонстрацию") },
                new Action[] { delegate { StopDemo("кнопка «Остановить демонстрацию»"); } },
                index, steps.Count);
        }

        /// <summary>Кадровое обслуживание сценария.</summary>
        public void Tick(float deltaTime)
        {
            if (!running) return;
            if (flow == null)
            {
                KazistovVvUIManager ui = KazistovVvUIManager.Instance;
                if (ui != null) flow = ui.Flow;
            }

            totalTime += deltaTime;
            stepTime += deltaTime;
            DemoStep step = steps[index];

            bool waited = stepTime >= Mathf.Max(0f, step.MinTime);
            bool done = true;
            if (step.Done != null)
            {
                try { done = step.Done(); }
                catch (Exception) { done = false; }
            }
            bool timeout = step.MaxTime > 0f && stepTime >= step.MaxTime;

            if (waited && done)
            {
                if (index >= steps.Count - 1)
                {
                    running = false;
                    KazistovVvUIManager.SetPlanStatus(
                        "Демонстрация завершена · " + totalTime.ToString("0.0") + " с", KvTheme.Ok);
                    Report("демонстрация завершена за " + totalTime.ToString("0.0") + " с");
                    return;
                }
                EnterStep(index + 1);
                return;
            }

            if (timeout)
            {
                if (!string.IsNullOrEmpty(abortReason))
                {
                    StopDemo(abortReason);
                    return;
                }
                // Условие не наступило за отведённое время — идём дальше, но честно пишем об этом.
                Report("шаг " + (index + 1) + " не подтвердился за " + step.MaxTime.ToString("0") +
                       " с — продолжаю демонстрацию");
                if (index >= steps.Count - 1)
                {
                    running = false;
                    return;
                }
                EnterStep(index + 1);
            }
        }

        // ================================================================== действия шагов

        private void EnsureMetricsPanel()
        {
            if (flow == null) return;
            flow.SetMetricsPanelVisible(true);
            KazistovVvUIManager.SetPlanStatus(
                "Демонстрация: панель метрик включена — видно время, длину, кривизну и зазор",
                KvTheme.Accent);
        }

        /// <summary>Демонстрационная точка над столешницей (для SCARA — ближе к базе).</summary>
        private void PlaceDemoPoint()
        {
            if (flow == null || flow.Robot == null) return;

            bool scara = flow.Validator != null && flow.Validator.Dof <= 3;
            Vector3 basePos = flow.Robot.transform.position;
            Vector3 point = scara
                ? new Vector3(basePos.x + 0.34f, TrajectoryCore.StandBuilder.TopHeight + pointHeight,
                    basePos.z + 0.16f)
                : new Vector3(basePos.x + 0.48f, TrajectoryCore.StandBuilder.TopHeight + pointHeight,
                    basePos.z + 0.34f);

            bool ok = flow.LockPointFromUi(point, point, Vector3.up, false);
            if (!ok)
            {
                KazistovVvUIManager.SetPlanStatus("Демонстрация: точка не поставлена (робот занят)",
                    KvTheme.Warn);
                return;
            }
            Report("демонстрация: точка " + point.x.ToString("0.00") + ", " + point.y.ToString("0.00") +
                   ", " + point.z.ToString("0.00") + (scara ? " (SCARA)" : " (робот)"));
        }

        /// <summary>Выбрать лучший вариант: минимальная стоимость среди прошедших SafetyGate.</summary>
        private void SelectBestVariant()
        {
            if (flow == null || flow.State == null || flow.State.candidates.Count == 0) return;

            List<TrajectoryCandidate> candidates = flow.State.candidates;
            int best = -1;
            double bestScore = double.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                TrajectoryCandidate c = candidates[i];
                if (c == null || c.plan == null) continue;
                double score = c.safe ? c.score : c.score + 1000.0;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }
            if (best < 0) best = 0;

            chosenVariant = best;
            bool ok = flow.SelectCandidateByIndex(best);
            Report(ok
                ? "демонстрация: выбран вариант №" + (best + 1) + " («" +
                  (candidates[best].label ?? "") + "») — робот пошёл по траектории"
                : "демонстрация: выбрать вариант не удалось");
        }

        /// <summary>Финал: предложение пройти туториал (этап 2).</summary>
        private void OfferTutorial()
        {
            if (overlay == null) return;
            string body = KvLocExtra.T("demo.offer",
                "Демонстрация завершена. Пройти туториал, чтобы делать это самому?");
            if (chosenVariant >= 0) body += "\n\n" +
                KvLocExtra.T("demo.line5", "Выбран лучший вариант") + ": №" + (chosenVariant + 1) +
                " · " + totalTime.ToString("0.0") + " с";

            overlay.ShowCaption(
                KvLocExtra.T("demo.title", "Демонстрация работы") + "   ·   " +
                KvLocExtra.T("common.ok", "Готово"),
                body,
                KvLocExtra.T("tut.progress",
                    "Прогресс сохраняется автоматически — обучение можно продолжить позже"),
                new[]
                {
                    KvLocExtra.T("demo.later", "Позже"),
                    KvLocExtra.T("demo.yes", "Пройти туториал")
                },
                new Action[]
                {
                    delegate
                    {
                        if (overlay != null) overlay.HideCaption();
                        KazistovVvUIManager.SetPlanStatus("Демонстрация завершена", KvTheme.Ok);
                    },
                    delegate
                    {
                        if (overlay == null) return;
                        overlay.HideCaption();
                        overlay.StartTutorial(false);
                    }
                },
                0, 1);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[QuickStart] " + text);
            if (Message != null) Message(text);
        }
    }
}
