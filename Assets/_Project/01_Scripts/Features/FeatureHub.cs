using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ХАБ НОВЫХ ФУНКЦИЙ (этапы 1–20 ТЗ): единая точка входа для интерфейса, дерева,
    /// горячих клавиш и кадрового обслуживания всех новых сервисов.
    ///
    /// Создаётся кодом (`KazistovVvUIManager` вызывает <see cref="Ensure"/> при регистрации
    /// команд), поэтому сцена НЕ меняется. Ничего из существующей логики не переписывается:
    /// хаб только вызывает уже существующие публичные методы потока этапов и добавляет
    /// свои сервисы рядом.
    ///
    /// ГОРЯЧИЕ КЛАВИШИ НОВЫХ ФУНКЦИЙ (проверено поиском по проекту — эти клавиши свободны,
    /// существующие бинды W A S D Q E Z X G F TAB Esc Enter Shift не тронуты):
    ///   R — запись вкл/выкл, P — воспроизведение/пауза, V — захват, H — тепловая карта
    ///   достижимости, J — тепловая карта зазоров, F5 — панель функций,
    ///   F6 — журнал, F7 — сравнение траекторий, Ctrl+Z / Ctrl+Y — отмена/повтор.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class FeatureHub : MonoBehaviour
    {
        public static FeatureHub Instance { get; private set; }

        // ------------------------------------------------------------------ настройки (инспектор)
        [Header("Запись траекторий (этап 1)")]
        public float recordRate = 20f;
        public float playbackSpeed = 1f;

        [Header("Тепловые карты (этапы 8–9)")]
        public bool heatmapOnByDefault = true;
        public int heatmapSamplesPerFrame = 64;

        [Header("Зоны запрета (этап 5)")]
        public bool discardDangerousTrajectories = false;

        [Header("Горячие клавиши новых функций")]
        public bool hotkeysEnabled = true;

        [Header("Диагностика")]
        public bool logEvents = true;

        // ------------------------------------------------------------------ сервисы
        public KvRecordingService Recording { get; private set; }
        public KvPoseLibrary Poses { get; private set; }
        public KvZoneService Zones { get; private set; }
        public KvComparison Comparison { get; private set; }
        public KvReachabilityHeatmap Heatmap { get; private set; }
        public KvClearanceOverlay Clearance { get; private set; }
        public KvGripper Gripper { get; private set; }
        public KvPickAndPlace PickAndPlace { get; private set; }
        public KvSpatialAudio Audio { get; private set; }
        public KvHaptics Haptics { get; private set; }
        public KvUndoStack Undo { get; private set; }
        public KvScenarioManager Scenarios { get; private set; }
        public KvPresentationMode Presentation { get; private set; }
        public KvSessionManager Sessions { get; private set; }
        public KvPlannerPerformance Performance { get; private set; }
        public KvActionLog Log { get { return KvActionLog.Instance; } }

        public KvFeatureWindow Window { get; private set; }

        /// <summary>Свой мир столкновений для новых функций (поток свой не отдаёт, а трогать его нельзя).</summary>
        public CollisionWorld World { get; private set; }

        // ------------------------------------------------------------------ состояние
        private KazistovVvUIManager ui;
        private TrajectoryFlowController flow;
        private FreeFlyCameraController rig;
        private RobotController boundRobot;

        private float worldTimer;
        private int zoneCheckedFor = -1;
        /// <summary>Кадры, в которых наблюдение за потоком не пишет Undo (идёт отмена/повтор).</summary>
        private int suppressUndoFrames;

        // наблюдение за потоком (журнал, звук, вибрация, Undo)
        private FlowState lastPhase = FlowState.Idle;
        private bool lastHadPoint;
        private Vector3 lastPoint;
        private int lastSelectedTrajectory = -1;
        private bool motionWasRunning;

        // Ctrl+Z / Ctrl+Y
        private bool ctrlZWasDown, ctrlYWasDown;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            World = new CollisionWorld();
            Recording = new KvRecordingService { rate = recordRate };
            Poses = new KvPoseLibrary();
            Zones = new KvZoneService { discardDangerous = discardDangerousTrajectories };
            Comparison = new KvComparison();
            Heatmap = new KvReachabilityHeatmap
            {
                samplesPerFrame = heatmapSamplesPerFrame
            };
            Clearance = new KvClearanceOverlay();
            Gripper = new KvGripper();
            PickAndPlace = new KvPickAndPlace();
            Audio = new KvSpatialAudio();
            Haptics = new KvHaptics();
            Undo = new KvUndoStack();
            Scenarios = new KvScenarioManager();
            Presentation = new KvPresentationMode();
            Sessions = new KvSessionManager();
            Performance = new KvPlannerPerformance();

            // --- журнал: всё важное пишется в файл и в панель
            // Именованные методы вместо анонимных делегатов: их можно отписать в OnDestroy
            // (17 анонимных подписок без отписки = утечка при повторном входе в PlayMode).
            Zones.Message += OnZoneMessage;
            Poses.Changed += OnPosesChanged;
            Poses.Failed += OnPosesFailed;
            Recording.Failed += OnRecordingFailed;
            Recording.RecordingFinished += OnRecordingFinished;
            Recording.PlaybackStarted += OnPlaybackStarted;
            Recording.PlaybackFinished += OnPlaybackFinished;
            Comparison.Changed += OnComparisonChanged;
            Heatmap.Message += OnUiMessage;
            Clearance.Message += OnUiMessage;
            Gripper.Message += OnUiMessage;
            PickAndPlace.Message += OnScenarioMessage;
            Scenarios.Message += OnScenarioMessage;
            Presentation.Message += OnUiMessage;
            Sessions.Message += OnSessionMessage;
            Undo.Performed += OnUiMessage;
            Performance.RunFinished += OnRunFinished;

            BuildScenarios(Scenarios);
            Log.Add(KvLogKind.System, "модуль новых функций запущен (этапы 1–20) · данные: " +
                                      FeatureStorage.Root);
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Отписка от событий сервисов (утечка при повторном входе в PlayMode).
            if (Zones != null) Zones.Message -= OnZoneMessage;
            if (Poses != null)
            {
                Poses.Changed -= OnPosesChanged;
                Poses.Failed -= OnPosesFailed;
            }
            if (Recording != null)
            {
                Recording.Failed -= OnRecordingFailed;
                Recording.RecordingFinished -= OnRecordingFinished;
                Recording.PlaybackStarted -= OnPlaybackStarted;
                Recording.PlaybackFinished -= OnPlaybackFinished;
            }
            if (Comparison != null) Comparison.Changed -= OnComparisonChanged;
            if (Heatmap != null) Heatmap.Message -= OnUiMessage;
            if (Clearance != null) Clearance.Message -= OnUiMessage;
            if (Gripper != null) Gripper.Message -= OnUiMessage;
            if (PickAndPlace != null) PickAndPlace.Message -= OnScenarioMessage;
            if (Scenarios != null) Scenarios.Message -= OnScenarioMessage;
            if (Presentation != null) Presentation.Message -= OnUiMessage;
            if (Sessions != null) Sessions.Message -= OnSessionMessage;
            if (Undo != null) Undo.Performed -= OnUiMessage;
            if (Performance != null) Performance.RunFinished -= OnRunFinished;
            if (Audio != null) Audio.Dispose();
            if (Heatmap != null) Heatmap.Dispose();
            if (Clearance != null) Clearance.Dispose();
            if (Gripper != null) Gripper.Detach();
            if (Recording != null) Recording.StopAll("модуль выключен");
        }

        // --- именованные обработчики событий сервисов (отписываются в OnDestroy)

        private void OnZoneMessage(string m) { Log.Add(KvLogKind.Zone, m); }
        private void OnPosesChanged() { if (ui != null) ui.RebuildTree(true); }
        private void OnPosesFailed(string m) { Log.Add(KvLogKind.Error, "позы: " + m); }
        private void OnRecordingFailed(string m) { Log.Add(KvLogKind.Error, "запись: " + m); }
        private void OnUiMessage(string m) { Log.Add(KvLogKind.Ui, m); }
        private void OnScenarioMessage(string m) { Log.Add(KvLogKind.Scenario, m); }
        private void OnSessionMessage(string m) { Log.Add(KvLogKind.Session, m); }

        private void OnRecordingFinished(KvTrajectoryRecord r)
        {
            if (r != null) Log.Add(KvLogKind.Record, "запись «" + r.name + "» сохранена (" +
                                                   r.SampleCount + " сэмплов, " +
                                                   r.duration.ToString("0.0") + " с)");
        }

        private void OnPlaybackStarted(KvTrajectoryRecord r)
        {
            Log.Add(KvLogKind.Record, "воспроизведение «" + r.name + "» · скорость ×" +
                                      Recording.SpeedMultiplier.ToString("0.00"));
        }

        private void OnPlaybackFinished(KvTrajectoryRecord r)
        {
            Log.Add(KvLogKind.Record, "воспроизведение «" + r.name + "» завершено");
        }

        private void OnComparisonChanged() { if (ui != null) ui.RebuildTree(true); }

        private void OnRunFinished(KvPlanRun run)
        {
            Log.Add(KvLogKind.Trajectory, "планирование: " + KvPlannerPerformance.Summary(run));
        }

        void OnDisable()
        {
            if (Zones != null) Zones.SaveAll();
        }

        // ------------------------------------------------------------------ создание из UI-менеджера

        /// <summary>Создать (или найти) хаб и панель функций. Вызывается UI-менеджером.</summary>
        public static FeatureHub Ensure(KazistovVvUIManager manager)
        {
            if (Instance != null) return Instance;
            if (manager == null) return null;
            FeatureHub hub = manager.gameObject.GetComponent<FeatureHub>();
            if (hub == null) hub = manager.gameObject.AddComponent<FeatureHub>();
            return hub;
        }

        /// <summary>Хаб, если он уже создан (для команд/дерева).</summary>
        public static FeatureHub Current { get { return Instance; } }

        /// <summary>
        /// ЭТАП 2 (мультиязычность): после смены языка обновить подписи панели функций.
        /// Мгновенно, без перезагрузки и без пересоздания окна.
        /// </summary>
        public static void LocalizeRefresh()
        {
            if (Instance != null && Instance.Window != null)
                Instance.Window.RefreshLanguageLabels();
        }

        /// <summary>Поток этапов (для панели функций; null — ещё не найден).</summary>
        public TrajectoryFlowController Flow() { return flow; }

        /// <summary>Контроллер оператора (камера).</summary>
        public FreeFlyCameraController CameraRig { get { return rig; } }

        // ------------------------------------------------------------------ привязка к сцене

        private void Update()
        {
            if (ui == null) ui = KazistovVvUIManager.Instance;
            if (ui == null) return;

            if (flow == null) flow = ui.Flow;
            if (rig == null) rig = ui.CameraRig;
            if (flow == null) return;

            float dt = Time.unscaledDeltaTime;

            BindServices();
            TickWorld(dt);
            TickServices(dt);
            WatchFlow();
            HandleHotkeys();
            UpdateEta();
        }

        private void LateUpdate()
        {
            // Облёт камеры в презентационном режиме — после всей камерной логики кадра.
            if (Presentation != null && Presentation.Active)
                Presentation.Tick(Time.unscaledDeltaTime, NextPressed(), PrevPressed());
        }

        private bool servicesBound;

        private void BindServices()
        {
            if (flow.Validator == null || !flow.Validator.Ready) return;

            if (!servicesBound)
            {
                servicesBound = true;
                Recording.Bind(flow);
                Recording.BindWorld(World);
                Poses.Bind(flow);
                Poses.BindWorld(World);
                Comparison.Bind(flow);
                Heatmap.Bind(flow, World);
                Clearance.Bind(flow, World);
                PickAndPlace.Bind(flow, World, Gripper);
                Performance.Bind(flow);
                Sessions.Bind(flow, Zones, Recording, Poses, CaptureFlags, ApplyFlags);
                Presentation.Bind(Camera.main, flow, rig);
                Presentation.SetNarration(KvPresentationMode.DefaultNarration());
                Audio.Bind(transform, null);

                // Зоны запрета переживают перезапуск: поднимаем сохранённые с диска.
                int restored = Zones.LoadAll();
                if (restored > 0)
                    Log.Add(KvLogKind.Zone, "загружено зон запрета с диска: " + restored);

                Debug.Log("[Features] сервисы привязаны к потоку этапов (робот: " +
                          flow.Validator.RobotName + ")");
            }

            if (boundRobot != flow.Robot)
            {
                boundRobot = flow.Robot;
                if (boundRobot != null)
                {
                    // ФИКС 3. Захват собирается ТОЛЬКО если он сейчас НЕ на этом роботе.
                    // Раньше `Attach` вызывался на каждое изменение привязки потока: пока
                    // оператор вёл камеру, поток мог перекинуться на второй стенд и обратно,
                    // захват пересобирался, а его раскрытие сбрасывалось в «разжат» — в прогоне
                    // это выглядело как «пальцы не сдвинулись (75 → 75 мм)». Теперь состояние
                    // пальцев переносится, а лишних пересборок нет.
                    if (!Gripper.AttachedTo(boundRobot))
                        Gripper.Attach(boundRobot);
                    Heatmap.RequestRebuild(true);
                    if (Clearance.Visible) Clearance.Rebuild(true);
                    Log.Add(KvLogKind.System, "новые функции привязаны к роботу «" +
                                              boundRobot.robotName + "»" +
                                              (Gripper.Attached
                                                  ? " · захват: " + Gripper.Width.ToString("0.000") +
                                                    " м (" + (Gripper.IsOpen ? "разжат" : "сжат") + ")"
                                                  : " · захват не собран"));
                }
            }
        }

        private void TickWorld(float dt)
        {
            // Свой мир столкновений: поток свой не отдаёт, а менять его нельзя.
            // Пересборка — как у потока, раз в 0.5 с (цена та же).
            worldTimer -= dt;
            if (worldTimer > 0f) return;
            worldTimer = 0.5f;
            if (flow.Robot != null && flow.Validator.Ready)
                World.Rebuild(flow.Robot, flow.Validator.linkRadius);
        }

        private void TickServices(float dt)
        {
            Recording.SpeedMultiplier = playbackSpeed;
            Recording.Tick(dt);
            Gripper.Tick(dt);
            PickAndPlace.Tick(dt);
            Scenarios.Tick(dt);
            Performance.Tick();
            Heatmap.Tick();
            if (Heatmap.Visible && !Heatmap.Building) Heatmap.RequestRebuild(false);
            Clearance.RebuildIfNeeded();
            if (Zones.Count > 0) CheckZones();
            if (ui != null && Window != null) Window.Refresh();
        }

        // ------------------------------------------------------------------ наблюдение за потоком

        private void WatchFlow()
        {
            SelectionState state = flow.State;
            if (suppressUndoFrames > 0) suppressUndoFrames--;

            // 1) новая точка
            if (state.hasPoint && (!lastHadPoint || Vector3.Distance(state.point, lastPoint) > 0.001f))
            {
                Vector3 point = state.point;
                Vector3 surface = state.aimAtLock;
                bool onSurface = state.hasPoint;
                Vector3 before = lastPoint;
                bool hadBefore = lastHadPoint;

                Log.Add(KvLogKind.Point, "точка выбрана: " + Vec(point) +
                                         (onSurface ? " · цель TCP" : ""));
                PlayAndVibrate(KvSound.PointSelected, true);

                // Undo: возврат к прежней точке (или к отсутствию точки)
                if (suppressUndoFrames == 0)
                {
                    Undo.Record("смена точки " + Vec(point),
                        delegate
                        {
                            suppressUndoFrames = 3;
                            if (hadBefore) flow.LockPointFromUi(before, before, Vector3.up, false);
                            else flow.ResetFlow("Отмена выбора точки");
                        },
                        delegate
                        {
                            suppressUndoFrames = 3;
                            flow.LockPointFromUi(point, surface, Vector3.up, onSurface);
                        });
                }

                lastHadPoint = true;
                lastPoint = point;
            }
            else if (!state.hasPoint && lastHadPoint)
            {
                lastHadPoint = false;
            }

            // 2) выбор траектории
            if (state.selectedTrajectory != lastSelectedTrajectory)
            {
                int now = state.selectedTrajectory;
                int before = lastSelectedTrajectory;
                if (now >= 0)
                {
                    string label = now < state.candidates.Count && state.candidates[now] != null
                        ? state.candidates[now].label : ("№" + (now + 1));
                    Log.Add(KvLogKind.Trajectory, "траектория подтверждена: " + label);
                    PlayAndVibrate(KvSound.TrajectoryConfirmed, false);

                    if (suppressUndoFrames == 0)
                    {
                        Undo.Record("выбор «" + label + "»",
                            delegate
                            {
                                suppressUndoFrames = 3;
                                if (before >= 0) flow.SelectCandidateByIndex(before);
                            },
                            delegate
                            {
                                suppressUndoFrames = 3;
                                flow.SelectCandidateByIndex(now);
                            });
                    }
                }
                lastSelectedTrajectory = now;
            }

            // 3) старт движения робота (этап 4) + внешнее движение (позы/записи/сценарии)
            bool running = flow.Motion != null && flow.Motion.IsRunning;
            if (state.phase == FlowState.RobotMoving && lastPhase != FlowState.RobotMoving)
            {
                Log.Add(KvLogKind.Motion, "робот начал движение по траектории этапа 4");
                PlayAndVibrate(KvSound.MotionStarted, false);
                int index = state.selectedTrajectory;
                if (suppressUndoFrames == 0)
                {
                    Undo.Record("запуск движения",
                        delegate
                        {
                            suppressUndoFrames = 3;
                            flow.Motion.Stop();
                            if (index >= 0) flow.SelectCandidateByIndex(index);
                        },
                        delegate
                        {
                            // Повтор НЕ запускает робота сам (безопасность): возвращает выбор траектории.
                            suppressUndoFrames = 3;
                            if (index >= 0) flow.SelectCandidateByIndex(index);
                        });
                }
            }
            else if (running && !motionWasRunning && state.phase != FlowState.RobotMoving)
            {
                Log.Add(KvLogKind.Motion, "внешнее движение робота (поза/запись/сценарий)");
                PlayAndVibrate(KvSound.MotionStarted, false);
            }
            else if (!running && motionWasRunning && flow.ExternalMotionRunning)
            {
                flow.EndExternalMotion();          // внешний план доехал — робот снова «живой»
                Log.Add(KvLogKind.Motion, "движение робота завершено");
            }
            motionWasRunning = running;

            // 4) смена фазы — в журнал (кроме рутинных)
            if (state.phase != lastPhase)
            {
                if (state.phase == FlowState.Idle && lastPhase == FlowState.TrajectoriesShown)
                    Log.Add(KvLogKind.Trajectory, "траектория не найдена — поток вернулся в Idle");
                if (state.phase == FlowState.Idle && lastPhase == FlowState.RobotMoving)
                    PlayAndVibrate(KvSound.TrajectoryConfirmed, false);
                lastPhase = state.phase;
                if (ui != null) ui.RebuildTree(true);
            }
        }

        private void PlayAndVibrate(KvSound sound, bool light)
        {
            Audio.PlayAtRobot(sound, flow);
            if (light) Haptics.OnPointSelected();
            else Haptics.OnTrajectoryConfirmed();
        }

        // ------------------------------------------------------------------ зоны запрета: проверка траекторий

        private void CheckZones()
        {
            List<TrajectoryCandidate> candidates = flow.State.candidates;
            if (candidates.Count == 0)
            {
                if (zoneCheckedFor != 0) { KvZoneMarks.Prune(candidates); zoneCheckedFor = 0; }
                return;
            }
            if (flow.State.phase != FlowState.TrajectoriesShown &&
                flow.State.phase != FlowState.PhantomsMoving) return;
            if (zoneCheckedFor == candidates.Count) return;

            zoneCheckedFor = candidates.Count;
            int dangerous = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                TrajectoryCandidate c = candidates[i];
                if (c == null || c.plan == null) continue;
                KvZoneHit hit = Zones.CheckPlan(c.plan, flow.Validator, flow.Validator.Dof);
                KvZoneMarks.Set(c, hit.hit, hit.Describe(), hit.penetration);
                if (hit.hit) dangerous++;
            }
            KvZoneMarks.Prune(candidates);

            if (dangerous > 0)
            {
                string message = "зон запрета: " + dangerous + " из " + candidates.Count +
                                 " траекторий помечены как опасные";
                Log.Add(KvLogKind.Zone, message);
                KazistovVvUIManager.SetPlanStatus(message, KvZoneWarningColor);
                Haptics.OnError();
                Audio.PlayAtRobot(KvSound.Error, flow, 0.7f);
            }
        }

        private static readonly Color KvZoneWarningColor = new Color(1f, 0.35f, 0.25f);

        // ------------------------------------------------------------------ горячие клавиши

        private void HandleHotkeys()
        {
            if (!hotkeysEnabled) return;

            // Ctrl+Z / Ctrl+Y (в проекте эти сочетания не заняты)
            bool ctrl = IsKeyHeld(KeyCode.LeftControl) || IsKeyHeld(KeyCode.RightControl);
            bool z = IsKeyHeld(KeyCode.Z);
            bool y = IsKeyHeld(KeyCode.Y);
            if (ctrl && z && !ctrlZWasDown) UndoLast();
            if (ctrl && y && !ctrlYWasDown) RedoLast();
            ctrlZWasDown = ctrl && z;
            ctrlYWasDown = ctrl && y;

            if (Presentation != null && Presentation.Active)
            {
                if (IsKeyDown(KeyCode.Escape))
                {
                    Presentation.Exit();
                    return;
                }
                return;   // в презентационном режиме остальные клавиши не мешают показу
            }

            if (IsKeyDown(KeyCode.R)) ToggleRecord();
            if (IsKeyDown(KeyCode.P)) TogglePlayback();
            if (IsKeyDown(KeyCode.V)) Gripper.Toggle();
            if (IsKeyDown(KeyCode.H)) Heatmap.Toggle();
            if (IsKeyDown(KeyCode.J)) Clearance.Toggle();
            if (IsKeyDown(KeyCode.F5)) Window.Toggle(0);
            if (IsKeyDown(KeyCode.F6)) Window.Toggle(7);
            if (IsKeyDown(KeyCode.F7)) Window.Toggle(4);
        }

        private static bool IsKeyHeld(KeyCode code)
        {
            try
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    UnityEngine.InputSystem.Controls.KeyControl control = KeyOf(keyboard, code);
                    if (control != null) return control.isPressed;
                }
            }
            catch (Exception) { }
            return Input.GetKey(code);
        }

        private static bool IsKeyDown(KeyCode code)
        {
            try
            {
                Keyboard keyboard = Keyboard.current;
                if (keyboard != null)
                {
                    UnityEngine.InputSystem.Controls.KeyControl control = KeyOf(keyboard, code);
                    if (control != null) return control.wasPressedThisFrame;
                }
            }
            catch (Exception) { }
            return Input.GetKeyDown(code);
        }

        private static UnityEngine.InputSystem.Controls.KeyControl KeyOf(Keyboard keyboard, KeyCode code)
        {
            switch (code)
            {
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
                case KeyCode.RightControl: return keyboard.rightCtrlKey;
                case KeyCode.Z: return keyboard.zKey;
                case KeyCode.Y: return keyboard.yKey;
                case KeyCode.R: return keyboard.rKey;
                case KeyCode.P: return keyboard.pKey;
                case KeyCode.V: return keyboard.vKey;
                case KeyCode.H: return keyboard.hKey;
                case KeyCode.J: return keyboard.jKey;
                case KeyCode.Escape: return keyboard.escapeKey;
                case KeyCode.F5: return keyboard.f5Key;
                case KeyCode.F6: return keyboard.f6Key;
                case KeyCode.F7: return keyboard.f7Key;
                case KeyCode.Space: return keyboard.spaceKey;
                case KeyCode.LeftArrow: return keyboard.leftArrowKey;
                case KeyCode.RightArrow: return keyboard.rightArrowKey;
                default: return null;
            }
        }

        private static bool NextPressed()
        {
            return IsKeyDown(KeyCode.Space) || IsKeyDown(KeyCode.RightArrow);
        }

        private static bool PrevPressed()
        {
            return IsKeyDown(KeyCode.LeftArrow);
        }

        // ------------------------------------------------------------------ действия (команды интерфейса)

        /// <summary>АВАРИЙНАЯ ОСТАНОВКА (этап 4): стоп всего, сброс траектории, Idle, журнал.</summary>
        public void EmergencyStop()
        {
            if (flow == null) flow = ui != null ? ui.Flow : null;
            if (flow == null) return;

            try
            {
                if (flow.Motion != null) flow.Motion.Stop();
                if (flow.Executor != null) flow.Executor.Stop(SafetyReason.OperatorStop);
                flow.EndExternalMotion();
                Recording.StopAll("аварийная остановка");
                PickAndPlace.Cancel("аварийная остановка");
                Scenarios.Stop("аварийная остановка");
                flow.ResetFlow("АВАРИЙНАЯ ОСТАНОВКА");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Estop] " + e.Message);
            }

            Audio.PlayAtRobot(KvSound.EmergencyStop, flow, 1f);
            Haptics.OnEmergencyStop();
            Log.Add(KvLogKind.Stop, "АВАРИЙНАЯ ОСТАНОВКА — движение остановлено, траектория сброшена, " +
                                    "поток возвращён в Idle");
            KazistovVvUIManager.SetPlanStatus("АВАРИЙНАЯ ОСТАНОВКА · сценарий сброшен", Color.red);
            if (ui != null) ui.RebuildTree(true);
        }

        /// <summary>Запись вкл/выкл (этап 1).</summary>
        public void ToggleRecord()
        {
            if (Recording.IsRecording)
            {
                KvTrajectoryRecord record = Recording.StopRecording(true);
                if (record != null)
                {
                    KazistovVvUIManager.SetPlanStatus("Запись сохранена: «" + record.name + "» · " +
                        record.SampleCount + " сэмплов · " + record.duration.ToString("0.0") + " с",
                        KvTheme.Ok);
                    if (ui != null) ui.RebuildTree(true);
                }
            }
            else
            {
                if (Recording.StartRecording("Запись " + FeatureStorage.TimeStamp(), "live"))
                {
                    KazistovVvUIManager.SetPlanStatus("Запись начата · робот «" +
                        (flow.Validator != null ? flow.Validator.RobotName : "?") + "» · " +
                        ("источник: " + (Window != null && Window.RecordSourcePhantom ? "фантом" : "робот")),
                        KvTheme.Warn);
                    if (ui != null) ui.RebuildTree(true);
                }
            }
        }

        /// <summary>Воспроизведение последней/активной записи (этап 1).</summary>
        public void TogglePlayback()
        {
            if (Recording.IsPlaying)
            {
                if (!Recording.TogglePause()) Recording.StopPlayback("остановлено кнопкой");
                return;
            }
            KvTrajectoryRecord record = Window != null ? Window.SelectedRecord : null;
            if (record == null)
            {
                List<KvTrajectoryRecord> all = KvRecordStore.LoadAll();
                record = all.Count > 0 ? all[0] : null;
            }
            if (record == null)
            {
                Log.Add(KvLogKind.Error, "воспроизведение: записей нет (R — начать запись)");
                return;
            }
            Recording.Play(record, playbackSpeed);
        }

        public void UndoLast()
        {
            if (!Undo.Undo()) Log.Add(KvLogKind.Ui, "отменять нечего");
            if (ui != null) ui.RebuildTree(true);
        }

        public void RedoLast()
        {
            if (!Undo.Redo()) Log.Add(KvLogKind.Ui, "повторять нечего");
            if (ui != null) ui.RebuildTree(true);
        }

        /// <summary>Запустить pick-and-place демо (этап 11).</summary>
        public void RunPickAndPlace()
        {
            if (!PickAndPlace.SpawnCube()) Log.Add(KvLogKind.Error, "не удалось поставить куб на стол");
            PickAndPlace.Run();
        }

        // ------------------------------------------------------------------ ETA (этап 12)

        /// <summary>Показывать ли панель ETA (идёт движение).</summary>
        public bool EtaVisible { get; private set; }
        public float EtaRemaining { get; private set; }
        public float EtaProgress { get; private set; }
        public float EtaTotal { get; private set; }
        public string EtaLabel { get; private set; }

        private void UpdateEta()
        {
            EtaLabel = "";

            if (Recording.IsPlaying)
            {
                EtaVisible = true;
                EtaRemaining = Recording.RemainingSeconds;
                EtaProgress = Recording.Progress01;
                EtaTotal = Recording.Playing != null ? Recording.Playing.duration : 0f;
                EtaLabel = "ВОСПРОИЗВЕДЕНИЕ · " +
                           (Recording.Paused ? "пауза" : "×" + Recording.SpeedMultiplier.ToString("0.00"));
                return;
            }

            TrajectoryExecutor executor = flow.Executor;
            if (executor != null && executor.IsRunning)
            {
                EtaVisible = true;
                EtaRemaining = executor.RemainingSeconds;
                EtaProgress = executor.Progress01;
                EtaTotal = executor.TotalSeconds;
                EtaLabel = (executor.Paused ? "ПАУЗА · " : "") +
                           (flow.ExternalMotionRunning ? "ВНЕШНИЙ ПЛАН" : "ТРАЕКТОРИЯ ЭТАПА 4");
                return;
            }

            EtaVisible = false;
            EtaRemaining = 0f;
            EtaProgress = 0f;
            EtaTotal = 0f;
        }

        // ------------------------------------------------------------------ сценарии (этап 18)

        private void BuildScenarios(KvScenarioManager manager)
        {
            // 1) «Показать workspace»
            KvScenario workspace = new KvScenario
            {
                Id = "show.workspace",
                Title = "Показать workspace",
                Description = "Включает зону достижимости и лимиты суставов, показывает робота со всех сторон"
            };
            workspace.Steps.Add(new KvScenarioStep
            {
                Text = "включить зону достижимости и лимиты",
                MinTime = 3f,
                Enter = delegate
                {
                    flow.SetWorkspaceVisible(true);
                    flow.SetJointLimitsVisible(true);
                    KvSettings.ShowWorkspace = true;
                    KvSettings.ShowJointLimits = true;
                    KazistovVvUIManager.SetPlanStatus("Сценарий: рабочая зона и лимиты включены", KvTheme.Accent);
                }
            });
            workspace.Steps.Add(new KvScenarioStep
            {
                Text = "показать метрики и границы рабочей зоны",
                MinTime = 4f,
                Enter = delegate { flow.SetMetricsPanelVisible(true); }
            });
            workspace.Steps.Add(new KvScenarioStep
            {
                Text = "готово — зона достижимости показана",
                MinTime = 2f
            });
            manager.Add(workspace);

            // 2) «Показать лимиты»
            KvScenario limits = new KvScenario
            {
                Id = "show.limits",
                Title = "Показать лимиты",
                Description = "Кольца лимитов на суставах + панель «угол / лимит / запас»"
            };
            limits.Steps.Add(new KvScenarioStep
            {
                Text = "включить индикаторы лимитов",
                MinTime = 4f,
                Enter = delegate
                {
                    flow.SetJointLimitsVisible(true);
                    KvSettings.ShowJointLimits = true;
                    KazistovVvUIManager.SetPlanStatus("Сценарий: лимиты суставов показаны", KvTheme.Accent);
                }
            });
            limits.Steps.Add(new KvScenarioStep { Text = "пауза для осмотра", MinTime = 4f });
            manager.Add(limits);

            // 3) «Демонстрация 8 траекторий»
            KvScenario demo = new KvScenario
            {
                Id = "show.trajectories",
                Title = "Демонстрация 8 траекторий",
                Description = "Ставит точку над столом, ждёт генерацию, выбирает лучшую траекторию и пускает фантом"
            };
            demo.Steps.Add(new KvScenarioStep
            {
                Text = "выбрать демонстрационную точку над столом",
                MinTime = 0.5f,
                Enter = delegate
                {
                    if (flow.Robot == null) return;
                    Vector3 basePos = flow.Robot.transform.position;
                    Vector3 point = new Vector3(basePos.x + 0.45f, TrajectoryCore.StandBuilder.TopHeight,
                        basePos.z + 0.35f);
                    flow.SetMetricsPanelVisible(true);
                    KvSettings.ShowMetrics = true;
                    if (flow.LockPointFromUi(point, point, Vector3.up, false))
                        KazistovVvUIManager.SetPlanStatus("Сценарий: точка " + point + " — считаю варианты…",
                            KvTheme.Accent);
                }
            });
            demo.Steps.Add(new KvScenarioStep
            {
                Text = "дождаться генерации вариантов",
                MinTime = 1f,
                MaxTime = 60f,
                Done = delegate
                {
                    return flow.State.phase == FlowState.TrajectoriesShown ||
                           flow.State.phase == FlowState.PhantomsMoving;
                }
            });
            demo.Steps.Add(new KvScenarioStep
            {
                Text = "выбрать лучшую траекторию и показать фантом",
                MinTime = 6f,
                Enter = delegate { flow.SelectCandidateByIndex(0); }
            });
            demo.Steps.Add(new KvScenarioStep { Text = "фантом прошёл траекторию", MinTime = 3f });
            manager.Add(demo);

            // 4) «Продемонстрировать pick-and-place»
            KvScenario pickPlace = new KvScenario
            {
                Id = "show.pickplace",
                Title = "Продемонстрировать pick-and-place",
                Description = "Ставит куб, берёт его гриппером и перекладывает в другую точку"
            };
            pickPlace.Steps.Add(new KvScenarioStep
            {
                Text = "поставить куб и разжать захват",
                MinTime = 1f,
                Enter = delegate
                {
                    PickAndPlace.SpawnCube();
                    Gripper.SetOpen(true);
                }
            });
            pickPlace.Steps.Add(new KvScenarioStep
            {
                Text = "pick-and-place: подъезд → захват → перенос → отпускание",
                MinTime = 1f,
                MaxTime = 180f,
                Enter = delegate { PickAndPlace.Run(); },
                Done = delegate
                {
                    return !PickAndPlace.Running && flow != null && !flow.ExternalMotionRunning;
                }
            });
            pickPlace.Steps.Add(new KvScenarioStep { Text = "готово", MinTime = 3f });
            manager.Add(pickPlace);
        }

        // ------------------------------------------------------------------ флаги сессии (этап 20)

        private KvSessionFlags CaptureFlags()
        {
            return new KvSessionFlags
            {
                heatmapReachability = Heatmap != null && Heatmap.IsVisible,
                heatmapClearance = Clearance != null && Clearance.Visible,
                zonesVisible = Zones != null && Zones.Count > 0,
                jointsPanel = Window != null && Window.VisibleTab == 2,
                logPanel = Window != null && Window.VisibleTab == 7,
                scenariosPanel = Window != null && Window.VisibleTab == 8,
                autoRecordPose = Recording != null && Recording.IsRecording
            };
        }

        private void ApplyFlags(KvSessionFlags flags)
        {
            if (flags == null) return;
            Heatmap.SetVisible(flags.heatmapReachability);
            Clearance.SetVisible(flags.heatmapClearance);
            if (Zones != null) Zones.SetAllVisible(flags.zonesVisible);
            if (Window != null && flags.logPanel) Window.Show(7);
        }

        // ------------------------------------------------------------------ дерево моделей (этапы 1, 2, 5, 6)

        /// <summary>Добавить ветки новых функций в дерево моделей (вызывает UI-менеджер).</summary>
        public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,
            RobotController activeRobot)
        {
            FeatureHub hub = Instance;
            if (hub == null || roots == null) return;

            // --- ЗАПИСИ ТРАЕКТОРИЙ (этап 1)
            List<KvTrajectoryRecord> records = KvRecordStore.LoadAll();
            if (hub.Recording != null && hub.Recording.IsRecording)
            {
                ProjectNode live = new ProjectNode("recording:live",
                    "● ИДЁТ ЗАПИСЬ · " + hub.Recording.Recording.SampleCount + " т.",
                    ProjectNodeKind.Group, null, activeRobot);
                live.Key = "recording:live";
                live.Tooltip = "Запись идёт прямо сейчас. Нажмите «Записать» ещё раз, чтобы сохранить.";
                roots.Add(live);
            }
            if (records.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:records", "Записи траекторий",
                    ProjectNodeKind.Group, null, activeRobot);
                group.Key = "group:records";
                group.Details = records.Count.ToString();
                group.Tooltip = "Записанные траектории (JSON, папка " + FeatureStorage.RecordingsDir + ")";
                for (int i = 0; i < records.Count; i++)
                {
                    KvTrajectoryRecord record = records[i];
                    ProjectNode node = new ProjectNode("record:" + record.id, record.name,
                        ProjectNodeKind.Trajectory, null, activeRobot);
                    node.Key = "record:" + record.id;
                    node.Tag = record;
                    node.Details = record.duration.ToString("0.0") + " с · " + record.SampleCount + " т.";
                    node.Tooltip = record.Tooltip +
                                   "\n\nДвойной клик по узлу — переименовать; воспроизведение — кнопка «Проиграть».";
                    group.Children.Add(node);
                }
                roots.Add(group);
            }

            // --- ПОЗЫ (этап 2)
            List<KvPosePreset> poses = hub.Poses != null ? hub.Poses.ForCurrentRobot() : null;
            if (poses != null && poses.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:poses", "Позы", ProjectNodeKind.Group,
                    null, activeRobot);
                group.Key = "group:poses";
                group.Details = poses.Count.ToString();
                group.Tooltip = "Сохранённые позы робота. «Перейти в позу» — кнопка в панели «Позы».";
                for (int i = 0; i < poses.Count; i++)
                {
                    KvPosePreset pose = poses[i];
                    ProjectNode node = new ProjectNode("pose:" + pose.id, pose.name,
                        ProjectNodeKind.Point, null, activeRobot);
                    node.Key = "pose:" + pose.id;
                    node.Tag = pose;
                    node.Details = "TCP " + pose.TcpVector.x.ToString("0.00") + ", " +
                                   pose.TcpVector.y.ToString("0.00") + ", " +
                                   pose.TcpVector.z.ToString("0.00");
                    node.Tooltip = pose.Tooltip;
                    group.Children.Add(node);
                }
                roots.Add(group);
            }

            // --- ЗОНЫ ЗАПРЕТА (этап 5)
            if (hub.Zones != null && hub.Zones.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:zones", "Зоны запрета", ProjectNodeKind.Group,
                    null, activeRobot);
                group.Key = "group:zones";
                group.Details = hub.Zones.Count.ToString();
                group.Tooltip = "Зоны запрета: куб / сфера / цилиндр. Траектории, пересекающие зону, " +
                                "помечаются как опасные.";
                for (int i = 0; i < hub.Zones.Zones.Count; i++)
                {
                    KvZone zone = hub.Zones.Zones[i];
                    if (zone == null) continue;
                    ProjectNode node = new ProjectNode("zone:" + zone.Data.id, zone.Data.name,
                        ProjectNodeKind.Object, zone.transform, activeRobot);
                    node.Key = "zone:" + zone.Data.id;
                    node.Tag = zone;
                    node.Details = zone.Data.ShapeLabel + " · " + zone.Data.SizeText;
                    node.Tooltip = zone.Data.Tooltip;
                    group.Children.Add(node);
                }
                roots.Add(group);
            }

            // --- метки сравнения и зон у ветки «Траектории» (этапы 5–6)
            for (int r = 0; r < roots.Count; r++)
            {
                ProjectNode group = roots[r];
                if (group == null || group.Key != "group:trajectories") continue;
                for (int i = 0; i < group.Children.Count; i++)
                {
                    ProjectNode node = group.Children[i];
                    TrajectoryCandidate candidate = node.Tag as TrajectoryCandidate;
                    if (candidate == null) continue;

                    string mark = hub.Comparison != null ? hub.Comparison.MarkOf(i) : "";
                    if (!string.IsNullOrEmpty(mark)) node.Details = "[" + mark + "] " + node.Details;
                    if (KvZoneMarks.IsMarked(candidate))
                    {
                        node.Details = "⚠ " + node.Details;
                        node.Tooltip += "\n\n⚠ " + KvZoneMarks.ReasonOf(candidate);
                    }
                }
            }

            // --- ветки ЭТАПОВ 1–8 этой сессии: промежуточные точки, динамические
            //     препятствия, сингулярности (отдельным вызовом, чтобы не трогать логику выше)
            KvStageHub.BuildTreeNodes(manager, roots, activeRobot);
        }

        /// <summary>Дополнительные строки свойств для узлов новых функций (вызывает UI-менеджер).</summary>
        public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)        {
            FeatureHub hub = Instance;
            if (hub == null || node == null || list == null) return;

            KvTrajectoryRecord record = node.Tag as KvTrajectoryRecord;
            if (record != null)
            {
                list.Add(KvProp.Section("Запись траектории"));
                list.Add(KvProp.Row("Робот", string.IsNullOrEmpty(record.robot) ? "—" : record.robot, KvTheme.TextMain));
                list.Add(KvProp.Row("Источник", record.SourceLabel, KvTheme.TextMain));
                list.Add(KvProp.Row("Создана", record.created, KvTheme.TextDim));
                list.Add(KvProp.Row("Сэмплов", record.SampleCount.ToString(), KvTheme.TextMain));
                list.Add(KvProp.Row("Частота", record.rate.ToString("0") + " Гц", KvTheme.TextMain));
                list.Add(KvProp.Row("Длительность", record.duration.ToString("0.00") + " с", KvTheme.TextMain));
                list.Add(KvProp.Row("Путь TCP", record.length.ToString("0.000") + " м", KvTheme.TextMain));
                list.Add(KvProp.Row("Множитель скорости", "×" + record.speed.ToString("0.00"), KvTheme.TextMain));
                if (!string.IsNullOrEmpty(record.notes))
                    list.Add(KvProp.Row("Заметка", record.notes, KvTheme.TextDim));
                list.Add(KvProp.Row("Файл", record.filePath, KvTheme.TextDim));
                return;
            }

            KvPosePreset pose = node.Tag as KvPosePreset;
            if (pose != null)
            {
                list.Add(KvProp.Section("Поза робота"));
                list.Add(KvProp.Row("Робот", string.IsNullOrEmpty(pose.robot) ? "—" : pose.robot, KvTheme.TextMain));
                list.Add(KvProp.Row("Создана", pose.created, KvTheme.TextDim));
                list.Add(KvProp.Row("TCP", pose.TcpVector.x.ToString("0.000") + ", " +
                    pose.TcpVector.y.ToString("0.000") + ", " + pose.TcpVector.z.ToString("0.000"),
                    KvTheme.TextMain));
                for (int i = 0; i < pose.q.Length; i++)
                    list.Add(KvProp.Row("Ось " + (i + 1), pose.q[i].ToString("0.00") + "°", KvTheme.TextMain));
                if (!string.IsNullOrEmpty(pose.notes)) list.Add(KvProp.Row("Заметка", pose.notes, KvTheme.TextDim));
                list.Add(KvProp.Row("Файл", pose.filePath, KvTheme.TextDim));
                return;
            }

            KvZone zone = node.Tag as KvZone;
            if (zone != null && zone.Data != null)
            {
                KvZoneData data = zone.Data;
                list.Add(KvProp.Section("Зона запрета"));
                list.Add(KvProp.Row("Форма", data.ShapeLabel, KvTheme.TextMain));
                list.Add(KvProp.Row("Габарит", data.SizeText, KvTheme.TextMain));
                list.Add(KvProp.Row("Центр", data.Center.x.ToString("0.000") + ", " +
                    data.Center.y.ToString("0.000") + ", " + data.Center.z.ToString("0.000"), KvTheme.TextMain));
                list.Add(KvProp.Row("Поворот", data.Euler.x.ToString("0.0") + "°, " +
                    data.Euler.y.ToString("0.0") + "°, " + data.Euler.z.ToString("0.0") + "°", KvTheme.TextDim));
                list.Add(KvProp.Row("Видна", data.visible ? "да" : "нет", KvTheme.TextMain));
                list.Add(KvProp.Row("Создана", data.created, KvTheme.TextDim));
                list.Add(KvProp.Row("Пересечений", CountZoneHits(zone) + " траекторий", KvTheme.TextMain));
                return;
            }

            TrajectoryCandidate candidate = node.Tag as TrajectoryCandidate;
            if (candidate != null)
            {
                list.Add(KvProp.Section("Сравнение и зоны"));
                string mark = hub.Comparison != null ? hub.Comparison.MarkOf(IndexIn(candidate)) : "";
                list.Add(KvProp.Row("Метка сравнения", string.IsNullOrEmpty(mark) ? "—" : mark, KvTheme.TextMain));
                list.Add(KvProp.Row("Зона запрета", KvZoneMarks.IsMarked(candidate)
                    ? "ПЕРЕСЕКАЕТ: " + KvZoneMarks.ReasonOf(candidate) : "нет",
                    KvZoneMarks.IsMarked(candidate) ? new Color(1f, 0.35f, 0.25f) : KvTheme.Ok));
            }

            // --- свойства узлов ЭТАПОВ 1–8 этой сессии (waypoints, препятствия, сингулярности)
            KvStageHub.BuildExtraProperties(node, list);
        }

        private static int CountZoneHits(KvZone zone)
        {
            FeatureHub hub = Instance;
            if (hub == null || hub.flow == null || zone == null) return 0;
            int count = 0;
            List<TrajectoryCandidate> candidates = hub.flow.State.candidates;
            for (int i = 0; i < candidates.Count; i++)
            {
                TrajectoryCandidate c = candidates[i];
                if (c != null && KvZoneMarks.IsMarked(c) && KvZoneMarks.ReasonOf(c).Contains(zone.Data.name))
                    count++;
            }
            return count;
        }

        private static int IndexIn(TrajectoryCandidate candidate)
        {
            FeatureHub hub = Instance;
            if (hub == null || hub.flow == null) return -1;
            return hub.flow.State.candidates.IndexOf(candidate);
        }

        // ------------------------------------------------------------------ регистрация команд (кнопки/меню)

        /// <summary>Зарегистрировать команды новых функций. Вызывает UI-менеджер.</summary>
        public static void RegisterCommands(KazistovVvUIManager manager)
        {
            FeatureHub hub = Ensure(manager);
            if (hub == null) return;

            hub.Window = manager.gameObject.GetComponent<KvFeatureWindow>();
            if (hub.Window == null) hub.Window = manager.gameObject.AddComponent<KvFeatureWindow>();
            hub.Window.Build(hub);

            // --- ЭТАП 1: запись и воспроизведение
            KvCommands.Register(new KvCommand
            {
                Id = "record.toggle",
                Title = "Записать движение",
                Description = "Начать/остановить запись движения робота в файл (JSON: углы, время, TCP)",
                Hotkey = "R",
                Icon = "record",
                MenuPath = "Сервис/Запись/Записать движение",
                Execute = delegate { hub.ToggleRecord(); },
                IsChecked = delegate { return hub.Recording != null && hub.Recording.IsRecording; },
                CheckedTint = delegate { return (Color?)new Color(1f, 0.25f, 0.25f); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "record.play",
                Title = "Проиграть запись",
                Description = "Воспроизвести выбранную запись (пауза — повторное нажатие, множитель скорости — в панели)",
                Hotkey = "P",
                Icon = "play",
                IconChecked = "pause",
                MenuPath = "Сервис/Запись/Проиграть запись",
                Execute = delegate { hub.TogglePlayback(); },
                IsChecked = delegate { return hub.Recording != null && hub.Recording.IsPlaying; }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "record.speed",
                Title = "Скорость воспроизведения",
                Description = "Переключить множитель скорости воспроизведения: 0.25 → 0.5 → 1 → 2 → 4",
                Icon = "metrics",
                MenuPath = "Сервис/Запись/Скорость воспроизведения",
                Execute = delegate { hub.CyclePlaybackSpeed(); }
            });

            // --- ЭТАП 2: позы
            KvCommands.Register(new KvCommand
            {
                Id = "pose.save",
                Title = "Сохранить текущую позу как…",
                Description = "Снимок углов суставов активного робота в файл (панель «Позы» — быстрый выбор имени)",
                Icon = "node-point",
                MenuPath = "Робот/Позы/Сохранить текущую позу как…",
                Execute = delegate
                {
                    hub.Window.Show(1);
                    hub.Poses.SaveCurrent("Поза " + FeatureStorage.TimeStamp());
                },
                IsEnabled = delegate { return hub.Poses != null; }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "pose.goto",
                Title = "Перейти в позу",
                Description = "Плавный переезд робота в выбранную позу (панель «Позы»)",
                Icon = "robot",
                MenuPath = "Робот/Позы/Перейти в позу",
                Execute = delegate { hub.Window.Show(1); }
            });

            // --- ЭТАП 3: ручное управление суставами
            KvCommands.Register(new KvCommand
            {
                Id = "joints.panel",
                Title = "Панель суставов",
                Description = "Слайдеры по каждому суставу с текущим углом, лимитом и запасом",
                Icon = "joint",
                MenuPath = "Робот/Ручное управление суставами",
                Execute = delegate { hub.Window.Toggle(2); },
                IsChecked = delegate { return hub.Window != null && hub.Window.Visible && hub.Window.VisibleTab == 2; }
            });

            // --- ЭТАП 4: аварийная остановка
            KvCommands.Register(new KvCommand
            {
                Id = "estop",
                Title = "АВАРИЙНАЯ ОСТАНОВКА",
                Description = "Мгновенно останавливает всё движение, сбрасывает траекторию и возвращает Idle",
                Icon = "estop",
                MenuPath = "Робот/АВАРИЙНАЯ ОСТАНОВКА",
                Execute = delegate { hub.EmergencyStop(); }
            });

            // --- ЭТАП 5: зоны запрета
            KvCommands.Register(new KvCommand
            {
                Id = "zone.box",
                Title = "Зона запрета: куб",
                Description = "Создать зону запрета-куб в точке прицела (полупрозрачный красный объём)",
                Icon = "zone-box",
                MenuPath = "Вид/Зоны запрета/Куб",
                Execute = delegate { hub.CreateZone(KvZoneShape.Box); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "zone.sphere",
                Title = "Зона запрета: сфера",
                Description = "Создать зону запрета-сферу в точке прицела",
                Icon = "zone-sphere",
                MenuPath = "Вид/Зоны запрета/Сфера",
                Execute = delegate { hub.CreateZone(KvZoneShape.Sphere); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "zone.cylinder",
                Title = "Зона запрета: цилиндр",
                Description = "Создать зону запрета-цилиндр в точке прицела",
                Icon = "zone-cylinder",
                MenuPath = "Вид/Зоны запрета/Цилиндр",
                Execute = delegate { hub.CreateZone(KvZoneShape.Cylinder); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "zone.panel",
                Title = "Зоны запрета: список",
                Description = "Список зон: размер, перемещение, удаление, показать/скрыть",
                Icon = "zone",
                MenuPath = "Вид/Зоны запрета/Список зон",
                Execute = delegate { hub.Window.Toggle(3); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "zone.discard",
                Title = "Отбрасывать опасные траектории",
                Description = "Вкл: траектории, пересекающие зону запрета, отбрасываются (выкл: только помечаются)",
                Icon = "zone",
                MenuPath = "Вид/Зоны запрета/Отбрасывать опасные",
                Execute = delegate
                {
                    hub.Zones.discardDangerous = !hub.Zones.discardDangerous;
                    hub.Log.Add(KvLogKind.Zone, "опасные траектории " +
                        (hub.Zones.discardDangerous ? "отбрасываются" : "только помечаются"));
                },
                IsChecked = delegate { return hub.Zones != null && hub.Zones.discardDangerous; }
            });

            // --- ЭТАП 6: сравнение траекторий
            KvCommands.Register(new KvCommand
            {
                Id = "compare.mark",
                Title = "Отметить траекторию для сравнения",
                Description = "Отметить выбранную в дереве траекторию как A, затем как B (метки видны в дереве)",
                Icon = "compare",
                MenuPath = "Вид/Сравнение траекторий/Отметить",
                Execute = delegate { hub.MarkForComparison(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "compare.open",
                Title = "Сравнение траекторий",
                Description = "Панель сравнения: метрики двух траекторий бок о бок и переключение на одну из них",
                Hotkey = "F7",
                Icon = "compare",
                MenuPath = "Вид/Сравнение траекторий/Открыть панель",
                Execute = delegate { hub.Window.Toggle(4); }
            });

            // --- ЭТАП 7: графики углов
            KvCommands.Register(new KvCommand
            {
                Id = "graph.open",
                Title = "Графики углов суставов",
                Description = "График изменения каждого угла во времени по выбранной траектории (можно наложить две)",
                Icon = "graph",
                MenuPath = "Вид/Графики углов суставов",
                Execute = delegate { hub.Window.Toggle(5); },
                IsChecked = delegate { return hub.Window != null && hub.Window.Visible && hub.Window.VisibleTab == 5; }
            });

            // --- ЭТАП 8: тепловая карта достижимости
            KvCommands.Register(new KvCommand
            {
                Id = "view.heatmap",
                Title = "Тепловая карта достижимости",
                Description = "Сфера вокруг робота (кольцо у SCARA), окрашенная по стоимости достижения точки",
                Hotkey = "H",
                Icon = "heatmap",
                MenuPath = "Вид/Тепловые карты/Достижимость",
                Execute = delegate { hub.Heatmap.Toggle(); },
                IsChecked = delegate { return hub.Heatmap != null && hub.Heatmap.IsVisible; }
            });

            // --- ЭТАП 9: тепловая карта зазоров
            KvCommands.Register(new KvCommand
            {
                Id = "view.clearance",
                Title = "Тепловая карта зазоров",
                Description = "Раскрашивает траекторию по зазору до препятствий: красное — опасно близко",
                Hotkey = "J",
                Icon = "clearance",
                MenuPath = "Вид/Тепловые карты/Зазоры",
                Execute = delegate { hub.Clearance.Toggle(); },
                IsChecked = delegate { return hub.Clearance != null && hub.Clearance.Visible; }
            });

            // --- ЭТАП 10: гриппер
            KvCommands.Register(new KvCommand
            {
                Id = "gripper.toggle",
                Title = "Захват: открыть / закрыть",
                Description = "Двухпалый захват на конце робота (для SCARA работает так же)",
                Hotkey = "V",
                Icon = "gripper",
                MenuPath = "Робот/Захват/Открыть-закрыть",
                Execute = delegate { hub.Gripper.Toggle(); },
                IsChecked = delegate { return hub.Gripper != null && hub.Gripper.IsOpen; }
            });

            // --- ЭТАП 11: pick-and-place
            KvCommands.Register(new KvCommand
            {
                Id = "demo.pickplace",
                Title = "Pick-and-place демо",
                Description = "Куб на стол → подъезд → захват → перенос → отпускание (автоматическая последовательность)",
                Icon = "pickplace",
                MenuPath = "Сервис/Демонстрации/Pick-and-place",
                Execute = delegate { hub.RunPickAndPlace(); }
            });

            // --- ЭТАП 12: панель ETA (отдельная панель, команда — её показ)
            KvCommands.Register(new KvCommand
            {
                Id = "eta.toggle",
                Title = "Панель ETA",
                Description = "Показывает остаток времени и прогресс текущей траектории",
                Icon = "eta",
                MenuPath = "Вид/Панели/Панель ETA",
                Execute = delegate { hub.Window.ToggleEta(); },
                IsChecked = delegate { return hub.Window != null && hub.Window.EtaPanelEnabled; }
            });

            // --- ЭТАП 13: журнал действий
            KvCommands.Register(new KvCommand
            {
                Id = "log.open",
                Title = "Журнал действий",
                Description = "Прокручиваемый журнал событий с фильтром по типу (пишется в файл)",
                Hotkey = "F6",
                Icon = "log",
                MenuPath = "Сервис/Журнал действий",
                Execute = delegate { hub.Window.Toggle(7); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "log.export",
                Title = "Выгрузить журнал",
                Description = "Сохранить весь журнал в отдельный файл",
                Icon = "layers",
                MenuPath = "Файл/Экспорт журнала",
                Execute = delegate { hub.Log.Export(); }
            });

            // --- ЭТАП 14: производительность планировщика
            KvCommands.Register(new KvCommand
            {
                Id = "perf.open",
                Title = "Замер производительности планировщика",
                Description = "Время генерации, попытки планирования, итерации, отброшенные дубликаты",
                Icon = "metrics",
                MenuPath = "Сервис/Производительность планировщика",
                Execute = delegate { hub.Window.Toggle(6); }
            });

            // --- ЭТАП 15: Undo / Redo (активируем штатные кнопки-заглушки)
            KvCommands.Register(new KvCommand
            {
                Id = "edit.undo",
                Title = "Отменить",
                Description = "Отмена последнего действия (точка, выбор траектории, запуск, зоны, позы)",
                Hotkey = "Ctrl+Z",
                Icon = "undo",
                MenuPath = "Правка/Отменить",
                Execute = delegate { hub.UndoLast(); },
                IsEnabled = delegate { return hub.Undo != null && hub.Undo.CanUndo; }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "edit.redo",
                Title = "Вернуть",
                Description = "Повтор отменённого действия",
                Hotkey = "Ctrl+Y",
                Icon = "redo",
                MenuPath = "Правка/Вернуть",
                Execute = delegate { hub.RedoLast(); },
                IsEnabled = delegate { return hub.Undo != null && hub.Undo.CanRedo; }
            });

            // --- ЭТАП 18: сценарии
            KvCommands.Register(new KvCommand
            {
                Id = "scenarios.open",
                Title = "Сценарии",
                Description = "Готовые сценарии: workspace, pick-and-place, лимиты, 8 траекторий",
                Icon = "scenario",
                MenuPath = "Сервис/Сценарии",
                Execute = delegate { hub.Window.Toggle(8); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "scenarios.run",
                Title = "Запустить сценарий…",
                Description = "Запустить выбранный в панели сценарий (пауза/отмена — там же)",
                Icon = "play",
                MenuPath = "Сервис/Сценарии/Запустить",
                Execute = delegate { hub.Window.RunSelectedScenario(); }
            });

            // --- ЭТАП 19: презентационный режим
            KvCommands.Register(new KvCommand
            {
                Id = "presentation.toggle",
                Title = "Презентационный режим",
                Description = "Камера сама облетает робота, текст на экране, интерфейс скрыт; выход — Esc",
                Icon = "presentation",
                MenuPath = "Вид/Презентационный режим",
                Execute = delegate { hub.Presentation.Toggle(); },
                IsChecked = delegate { return hub.Presentation != null && hub.Presentation.Active; }
            });

            // --- ЭТАП 20: сессии
            KvCommands.Register(new KvCommand
            {
                Id = "session.save",
                Title = "Сохранить сессию",
                Description = "Позы роботов, точки, зоны и флаги — в JSON-файл сессии",
                Icon = "session",
                MenuPath = "Файл/Сессия/Сохранить сессию",
                Execute = delegate { hub.Sessions.Save("Сессия " + FeatureStorage.TimeStamp()); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "session.load",
                Title = "Загрузить сессию",
                Description = "Восстановить последнюю сохранённую сессию",
                Icon = "session",
                MenuPath = "Файл/Сессия/Загрузить последнюю сессию",
                Execute = delegate
                {
                    KvSession newest = hub.Sessions.Newest;
                    if (newest == null) hub.Log.Add(KvLogKind.Error, "сессий нет — сначала сохраните");
                    else hub.Sessions.Load(newest);
                }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "session.panel",
                Title = "Список сессий",
                Description = "Сохранённые сессии: загрузить, удалить, посмотреть содержимое",
                Icon = "session",
                MenuPath = "Файл/Сессия/Список сессий",
                Execute = delegate { hub.Window.Toggle(9); }
            });

            // --- Панель функций целиком
            KvCommands.Register(new KvCommand
            {
                Id = "features.open",
                Title = "Панель функций (этапы 1–20)",
                Description = "Запись, позы, суставы, зоны, сравнение, графики, замер, журнал, сценарии, сессии",
                Hotkey = "F5",
                Icon = "features",
                MenuPath = "Вид/Панель функций",
                Execute = delegate { hub.Window.Toggle(0); }
            });

            // --- Файл: сохранение/загрузка записей и поз вручную
            KvCommands.Register(new KvCommand
            {
                Id = "file.export",
                Title = "Экспорт: папка данных",
                Description = "Показать в консоли путь к папке записей/поз/зон/сессий (файлы можно копировать)",
                Icon = "layers",
                MenuPath = "Файл/Папка данных",
                Execute = delegate
                {
                    hub.Log.Add(KvLogKind.System, "папка данных: " + FeatureStorage.Root);
                    Debug.Log("[Features] папка данных: " + FeatureStorage.Root +
                              "\n  записи: " + FeatureStorage.RecordingsDir +
                              "\n  позы: " + FeatureStorage.PosesDir +
                              "\n  зоны: " + FeatureStorage.ZonesDir +
                              "\n  сессии: " + FeatureStorage.SessionsDir +
                              "\n  журнал: " + FeatureStorage.LogsDir);
                }
            });

            // --- ИЗМЕРЕНИЕ И СЕТКА: БЫЛИ ЗАГЛУШКАМИ, СТАЛИ ДЕЙСТВИЯМИ (30.10.2026, S44)
            //
            // ПОЧЕМУ ЗДЕСЬ, А НЕ В ШТАТНЫХ КОМАНДАХ. Менеджер интерфейса регистрирует заглушки
            // `tool.measure` и `tool.grid` ПЕРВЫМ, а этот хаб — ПОСЛЕ, и реестр заменяет команду
            // с тем же id. Так и задумано: реальная реализация перекрывает заглушку, а не
            // сосуществует с ней (см. комментарий у `RegisterCommands` выше).
            KvCommands.Register(new KvCommand
            {
                Id = "tool.measure",
                Title = "Измерение",
                Description = "Линейка: первое нажатие — первая точка, второе — расстояние с проекциями по осям",
                Icon = "curve",
                MenuPath = "Сервис/Инструменты/Измерение",
                Execute = delegate
                {
                    // Точка прицела: если луч никуда не попал, честно говорим это, а не «ноль метров».
                    Vector3 point;
                    if (!hub.TryAim(out point))
                    {
                        hub.Log.Add(KvLogKind.System, "линейка: луч не попал в сцену — наведите на объект");
                        return;
                    }
                    hub.MeasureAt(point);
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "tool.measure.reset",
                Title = "Измерение: сбросить",
                Description = "Забыть первую точку замера",
                Icon = "curve",
                MenuPath = "Сервис/Инструменты/Измерение: сбросить",
                Execute = delegate
                {
                    hub.ResetMeasure();
                    hub.Log.Add(KvLogKind.System, "линейка: сброшена");
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "tool.grid",
                Title = "Сетка рабочей зоны",
                Description = "Плоская сетка на столешнице: шаг подбирается по размеру области",
                Icon = "workspace",
                MenuPath = "Сервис/Инструменты/Сетка рабочей зоны",
                Execute = delegate
                {
                    // Размер области берётся с текущей видимой рабочей зоны; если её нет —
                    // используется типовой стол оператора (1,0 × 0,7 м, решение [D7]).
                    // Размер области — типовой стол оператора (1,0 × 0,7 м, решение [D7]);
                    // центр — точка прицела, а если луч никуда не попал, то место перед оператором.
                    Vector3 origin;
                    if (!hub.TryAim(out origin)) origin = new Vector3(0f, 0.78f, 0f);
                    hub.ShowWorkspaceGrid(1.0f, 0.7f, origin);
                }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "tool.grid.clear",
                Title = "Сетка: убрать",
                Description = "Снять построенную сетку рабочей зоны",
                Icon = "workspace",
                MenuPath = "Сервис/Инструменты/Сетка: убрать",
                Execute = delegate { hub.ClearWorkspaceGrid(); }
            });


            if (hub.logEvents)
                Debug.Log("[Features] команды этапов 1–20 зарегистрированы · всего команд: " +
                          KvCommands.All.Count);
        }

        // ------------------------------------------------------------------ измерение и сетка

        /// <summary>
        /// ИЗМЕРЕНИЕ РАССТОЯНИЯ МЕЖДУ ДВУМЯ ТОЧКАМИ ПРИЦЕЛА (закрыто 30.10.2026, S44).
        ///
        /// ЧТО БЫЛО. Команда `tool.measure` стояла ЗАГЛУШКОЙ с подписью «пункт задел»: кнопка
        /// есть, нажатие отвечает «в разработке». Оператору это не даёт ничего.
        ///
        /// КАК РАБОТАЕТ. Первое нажатие запоминает точку прицела, второе — считает расстояние до
        /// новой и печатает показание с проекциями по осям. Следующее нажатие начинает новый
        /// замер. Так линейка ведёт себя ожидаемо и не требует отдельной кнопки «сброс».
        /// </summary>
        private Vector3 measureFrom;
        private bool measureHasFirst;

        public void MeasureAt(Vector3 point)
        {
            if (!measureHasFirst)
            {
                measureFrom = point;
                measureHasFirst = true;
                Log.Add(KvLogKind.System, "линейка: первая точка " + point.ToString("0.000") +
                                          " — укажите вторую");
                return;
            }

            if ((point - measureFrom).sqrMagnitude < 1e-8f)
            {
                // Двойной удар в ту же точку — это промах, а не «ноль метров»: говорим прямо.
                measureHasFirst = false;
                Log.Add(KvLogKind.System, "линейка: вторая точка совпала с первой — замер отменён");
                return;
            }

            string text = KvMeasureKit.Describe(measureFrom, point);
            Log.Add(KvLogKind.System, "линейка: " + text);
            Debug.Log("[Measure] расстояние " + text);
            measureHasFirst = false;
        }

        /// <summary>
        /// ТОЧКА ПРИЦЕЛА, ЕСЛИ ЛУЧ ВО ЧТО-ТО ПОПАЛ. Единственное место, где инструменты СЕРВИСА
        /// узнают, куда смотрит оператор: `CreateZone` делал это вручную, и вторая копия того же
        /// разбора неизбежно разошлась бы с первой.
        ///
        /// Возвращает false, когда риг не найден ИЛИ луч ушёл в пустоту — и тогда вызывающий
        /// обязан сказать это словами, а не строить объект в начале координат.
        /// </summary>
        public bool TryAim(out Vector3 point)
        {
            point = Vector3.zero;
            if (rig == null) rig = ui != null ? ui.CameraRig : null;
            if (rig == null || !rig.AimHitPublic) return false;
            point = rig.AimPointPublic;
            return true;
        }

        /// <summary>Сбросить начатую линейку (после смены робота или сцены).</summary>
        public void ResetMeasure()
        {
            measureHasFirst = false;
        }

        /// <summary>
        /// СЕТКА РАБОЧЕЙ ЗОНЫ (закрыто 30.10.2026, S44). Команда `tool.grid` тоже была заглушкой.
        ///
        /// ЧТО ДЕЛАЕТ. Строит плоскую сетку на плоскости стола по заданной области и сообщает
        /// числами: размер, шаг, сколько линий. Расчёт сетки — в `KvMeasureKit` (он проверяется
        /// тестами), здесь только рисование и журнал.
        /// </summary>
        public void ShowWorkspaceGrid(float width, float height, Vector3 origin)
        {
            if (width <= 0.01f || height <= 0.01f)
            {
                Log.Add(KvLogKind.System, "сетка: размер области не задан (" +
                                          width.ToString("0.00") + " × " + height.ToString("0.00") +
                                          " м) — сетка не построена");
                return;
            }

            string line = KvMeasureKit.DescribeGrid(width, height);
            float step = KvMeasureKit.GridStep(Mathf.Max(width, height));
            int linesX = KvMeasureKit.GridLines(width, step);
            int linesZ = KvMeasureKit.GridLines(height, step);

            // Число проведённых линий — ДОКАЗАТЕЛЬСТВО, что сетка построена: по нему видно, что
            // шаг подобран под размер, а не взят наугад.
            int drawn = 0;
            drawn += DrawGridLines(origin, Vector3.right, Vector3.forward, width, step, linesX);
            drawn += DrawGridLines(origin, Vector3.forward, Vector3.right, height, step, linesZ);

            Log.Add(KvLogKind.System, line + " · проведено линий " + drawn);
            Debug.Log("[Grid] " + line + " · проведено линий " + drawn);
        }

        /// <summary>
        /// Провести линии сетки вдоль одного направления. Отдельным методом — чтобы «сколько
        /// линий провели» считалось в одном месте, а не в двух копиях цикла.
        /// </summary>
        private int DrawGridLines(Vector3 origin, Vector3 along, Vector3 across, float length,
            float step, int count)
        {
            if (count <= 0 || step <= 0.0001f) return 0;
            int drawn = 0;
            for (int i = 0; i < count; i++)
            {
                Vector3 start = origin + across * (i * step);
                Vector3 end = start + along * length;
                if (DrawLine(start, end) != null) drawn++;
            }
            return drawn;
        }

        /// <summary>
        /// НАРИСОВАТЬ ОДНУ ЛИНИЮ СЕТКИ. Возвращает объект линии или null, если рисование
        /// недоступно. Единственное место, где сетка касается графики: заменить рисование
        /// (например, на GPU-инстансинг в MR) можно здесь, не трогая расчёт сетки.
        ///
        /// Материал берётся ЧЕРЕЗ ФАБРИКУ `KvRenderPipelineShaders` — прямые обращения к шейдерам
        /// HDRP под URP давали розовые линии (дефект A8).
        /// </summary>
        private LineRenderer DrawLine(Vector3 from, Vector3 to)
        {
            try
            {
                var go = new GameObject("KvGridLine");
                go.transform.SetParent(gridRoot != null ? gridRoot : null, false);
                var line = go.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.SetPosition(0, from);
                line.SetPosition(1, to);
                line.startWidth = 0.004f;
                line.endWidth = 0.004f;
                Material material = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                    "Материал_сетки", new Color(0.5f, 0.75f, 1f, 0.55f));
                if (material != null) line.material = material;
                if (gridLines.Count < 4000) gridLines.Add(go);
                return line;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[Grid] линия сетки не нарисована: " + e.GetType().Name + ": " + e.Message);
                return null;
            }
        }

        private Transform gridRoot;
        private readonly System.Collections.Generic.List<GameObject> gridLines =
            new System.Collections.Generic.List<GameObject>();

        /// <summary>Убрать нарисованную сетку (смена области, выход из режима).</summary>
        public void ClearWorkspaceGrid()
        {
            for (int i = 0; i < gridLines.Count; i++)
            {
                if (gridLines[i] != null) UnityEngine.Object.Destroy(gridLines[i]);
            }
            int removed = gridLines.Count;
            gridLines.Clear();
            if (removed > 0) Log.Add(KvLogKind.System, "сетка убрана · линий " + removed);
        }

        // ------------------------------------------------------------------ вспомогательные действия

        /// <summary>Циклический перебор множителя скорости воспроизведения.</summary>
        public void CyclePlaybackSpeed()
        {
            float[] steps = { 0.25f, 0.5f, 1f, 2f, 4f };
            int index = 0;
            for (int i = 0; i < steps.Length; i++)
                if (Mathf.Abs(steps[i] - playbackSpeed) < 0.001f) { index = i; break; }
            playbackSpeed = steps[(index + 1) % steps.Length];
            Recording.SpeedMultiplier = playbackSpeed;
            Log.Add(KvLogKind.Record, "множитель скорости воспроизведения: ×" +
                                      playbackSpeed.ToString("0.00"));
        }

        /// <summary>Создать зону запрета в точке прицела.</summary>
        public void CreateZone(KvZoneShape shape)
        {
            if (rig == null) rig = ui != null ? ui.CameraRig : null;
            Vector3 point = rig != null ? rig.AimPointPublic : Vector3.zero;
            if (rig == null || !rig.AimHitPublic)
            {
                Log.Add(KvLogKind.Error, "зона запрета: наведите прицел на поверхность");
                return;
            }
            KvZone zone = Zones.Create(shape, point);
            if (zone != null && ui != null) ui.RebuildTree(true);
        }

        /// <summary>Отметить выбранную в дереве траекторию для сравнения (этап 6).</summary>
        public void MarkForComparison()
        {
            if (ui == null || flow == null) return;
            ProjectNode node = ui.SelectedNode;
            TrajectoryCandidate candidate = node != null ? node.Tag as TrajectoryCandidate : null;
            if (candidate == null)
            {
                Log.Add(KvLogKind.Error, "отметить можно траекторию: выберите узел «Траектория N» в дереве");
                return;
            }
            int index = flow.State.candidates.IndexOf(candidate);
            string result = Comparison.Mark(index);
            Log.Add(KvLogKind.Trajectory, result);
            if (Comparison.Ready) Log.Add(KvLogKind.Trajectory, Comparison.Verdict());
            ui.RebuildTree(true);
        }

        /// <summary>Текущий множитель скорости (для панели).</summary>
        public float PlaybackSpeed { get { return playbackSpeed; } }

        /// <summary>
        /// Компактная «подпись» состояния новых функций для подписи дерева моделей
        /// (менеджер пересобирает дерево только при изменении подписи — экономия кадров).
        /// </summary>
        public static string TreeSignaturePart()
        {
            FeatureHub hub = Instance;
            if (hub == null) return "-";
            int records = 0;
            try
            {
                records = FeatureStorage.ListFiles(FeatureStorage.RecordingsDir, "*" + KvRecordStore.Extension).Count;
            }
            catch (Exception) { }
            return records + ":" + (hub.Poses != null ? hub.Poses.Count : 0) +
                   ":" + (hub.Zones != null ? hub.Zones.Count : 0) +
                   ":" + (hub.Recording != null && hub.Recording.IsRecording ? "rec" : "-") +
                   ":" + (hub.Comparison != null ? hub.Comparison.SlotA + "/" + hub.Comparison.SlotB : "-") +
                   ":" + (hub.Scenarios != null && hub.Scenarios.Running ? "run" : "-");
        }

        /// <summary>Данные для свойств/диагностики: список сервисов одной строкой.</summary>
        public string Dump()
        {
            return "FeatureHub: записей " + KvRecordStore.LoadAll().Count +
                   " · поз " + Poses.Count +
                   " · зон " + Zones.Count +
                   " · сессий " + Sessions.Count +
                   " · журнал " + Log.Count +
                   " · undo " + Undo.UndoCount + "/" + Undo.RedoCount +
                   " · heatmap " + (Heatmap.IsVisible ? "вкл" : "выкл") +
                   " · зазоры " + (Clearance.Visible ? "вкл" : "выкл") +
                   " · захват " + (Gripper.Attached ? "есть" : "нет");
        }

        private static string Vec(Vector3 v)
        {
            return "(" + v.x.ToString("0.000") + ", " + v.y.ToString("0.000") + ", " + v.z.ToString("0.000") + ")";
        }
    }
}
