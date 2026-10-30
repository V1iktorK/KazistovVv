using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ХАБ ЭТАПОВ 1–8 ТЕКУЩЕЙ СЕССИИ (фантомы, мультиязычность, экспорт демонстраций,
    /// сингулярности, waypoint-редактор, health monitor, динамические препятствия, пульт).
    ///
    /// Роль та же, что у <see cref="FeatureHub"/> для этапов 1–20: единая точка создания
    /// сервисов, кадрового обслуживания, горячих клавиш, дерева моделей, свойств и команд.
    /// Он НЕ переписывает существующий код: сервисы живут рядом и вызывают только публичные
    /// методы потока этапов (`PlayExternalPlan`, `Planner`, `PoseValidator`, `Motion`, …).
    ///
    /// ГОРЯЧИЕ КЛАВИШИ (проверено по проекту — свободны):
    ///   F8  — скриншот, F10 — старт/стоп записи видео, F11 — пульт,
    ///   F12 — мониторинг состояния, Ctrl+F8 — сингулярности, Ctrl+F10 — динамические
    ///   препятствия. Существующие бинды (W A S D Q E Z X G F TAB Esc Enter Shift R P V H J
    ///   F5 F6 F7 Ctrl+Z/Ctrl+Y, ЛКМ/колесо/СКМ) не тронуты и не добавлены.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class KvStageHub : MonoBehaviour
    {
        public static KvStageHub Instance { get; private set; }

        // ------------------------------------------------------------------ сервисы
        public KvCaptureService Capture { get; private set; }
        public KvSingularityVisualizer Singularities { get; private set; }
        public KvWaypointManager Waypoints { get; private set; }
        public KvHealthMonitor Health { get; private set; }
        public KvHealthPanel HealthPanel { get; private set; }
        public KvDynamicObstacleService Obstacles { get; private set; }
        public KvTeachPendant Pendant { get; private set; }

        // ------------------------------------------------------------------ параметры (инспектор)
        [Header("Этап 3: экспорт демонстраций")]
        public bool captureOnHotkeys = true;

        [Header("Этап 4: сингулярности")]
        public bool singularitiesOnByDefault = true;

        [Header("Этап 6: мониторинг состояния")]
        public bool healthOnByDefault = false;

        [Header("Этап 7: динамические препятствия")]
        public bool obstaclesOnByDefault = false;

        [Header("Этап 8: виртуальный пульт")]
        public bool pendantOnByDefault = false;

        [Header("Диагностика")]
        public bool logEvents = true;

        // ------------------------------------------------------------------ состояние
        private KazistovVvUIManager ui;
        private TrajectoryFlowController flow;
        private FreeFlyCameraController rig;
        private FeatureHub features;
        private bool servicesBound;
        private RobotController boundRobot;

        // ------------------------------------------------------------------ создание/жизненный цикл

        /// <summary>Создать (или найти) хаб этапов. Вызывается UI-менеджером при регистрации команд.</summary>
        public static KvStageHub Install(KazistovVvUIManager manager)
        {
            if (Instance != null) return Instance;
            if (manager == null) return null;
            KvStageHub hub = manager.gameObject.GetComponent<KvStageHub>();
            if (hub == null) hub = manager.gameObject.AddComponent<KvStageHub>();
            return hub;
        }

        public static KvStageHub Current { get { return Instance; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // --- сервисы (все — обычные классы; MonoBehaviour создаётся только для снимков экрана)
            Capture = KvCaptureService.Create(transform);
            Capture.screenshotKey = KeyCode.F8;
            Capture.videoKey = KeyCode.F10;

            Singularities = new KvSingularityVisualizer();
            Waypoints = new KvWaypointManager();
            Health = new KvHealthMonitor();
            Obstacles = new KvDynamicObstacleService();
            Pendant = gameObject.AddComponent<KvTeachPendant>();

            HealthPanel = gameObject.AddComponent<KvHealthPanel>();
            HealthPanel.Build(Health);

            // --- сообщения сервисов → журнал и консоль
            Capture.Message += OnServiceMessage;
            Singularities.Message += OnServiceMessage;
            Waypoints.Message += OnServiceMessage;
            Health.Message += OnServiceMessage;
            Obstacles.Message += OnServiceMessage;
            Pendant.Message += OnServiceMessage;

            Obstacles.stopRequest = delegate
            {
                if (features != null) features.EmergencyStop();
                else if (flow != null) flow.ResetFlow("аварийная остановка: динамическое препятствие");
                return true;
            };
            Pendant.stopRequest = delegate
            {
                if (features != null) features.EmergencyStop();
                else if (flow != null) flow.ResetFlow("аварийная остановка (пульт)");
            };
            Pendant.homeRequest = GoHome;
            Pendant.recordPoseRequest = RecordPose;
            Pendant.startRequest = StartMotion;

            Debug.Log("[Stages] хаб этапов 1–8 поднят (экспорт, сингулярности, waypoints, состояние, " +
                      "препятствия, пульт) · папка экспорта: " + Capture.Folder);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Отписка от сообщений сервисов (утечка при повторном входе в PlayMode).
            if (Capture != null) Capture.Message -= OnServiceMessage;
            if (Singularities != null) Singularities.Message -= OnServiceMessage;
            if (Waypoints != null) Waypoints.Message -= OnServiceMessage;
            if (Health != null) Health.Message -= OnServiceMessage;
            if (Obstacles != null) Obstacles.Message -= OnServiceMessage;
            if (Pendant != null) Pendant.Message -= OnServiceMessage;
            if (Singularities != null) Singularities.Dispose();
            if (Waypoints != null) Waypoints.Dispose();
            if (Health != null) Health.Dispose();
            if (Obstacles != null) Obstacles.Dispose();
        }

        private void OnServiceMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (KvActionLog.Instance != null) KvActionLog.Instance.Add(KvLogKind.Ui, message);
        }

        // ------------------------------------------------------------------ привязка и тики

        private void Update()
        {
            if (ui == null) ui = KazistovVvUIManager.Instance;
            if (ui == null) return;
            if (flow == null) flow = ui.Flow;
            if (rig == null) rig = ui.CameraRig;
            if (flow == null) return;

            float dt = Time.unscaledDeltaTime;

            BindServices();

            Capture.Tick();
            Singularities.Tick(dt);
            Waypoints.Tick(dt);
            Health.Tick(dt);
            HealthPanel.Refresh();
            Obstacles.Tick(dt);

            Pendant.Tick(dt);
            HandleHotkeys();
        }

        private void BindServices()
        {
            if (flow.Validator == null || !flow.Validator.Ready) return;

            if (!servicesBound)
            {
                servicesBound = true;
                features = FeatureHub.Current;

                Capture.videoWidth = 1920;
                Capture.videoHeight = 1080;

                Singularities.Bind(flow);
                Waypoints.Bind(flow, features != null ? features.World : null, rig);
                Health.Bind(flow);
                Obstacles.Bind(flow, features != null ? features.World : null);
                Pendant.Build(flow);

                Singularities.SetEnabled(singularitiesOnByDefault);
                Health.SetEnabled(healthOnByDefault);
                if (healthOnByDefault) HealthPanel.SetVisible(true);
                Obstacles.SetEnabled(obstaclesOnByDefault);
                Pendant.SetVisible(pendantOnByDefault);

                Debug.Log("[Stages] сервисы привязаны к потоку этапов (робот: " +
                          flow.Validator.RobotName + ")");
            }

            if (flow.Robot != null && flow.Robot != boundRobot)
            {
                boundRobot = flow.Robot;
                Singularities.Rebind(flow.Robot);
                Health.Rebind(flow.Robot);
                Waypoints.EvaluateAll();
                Waypoints.RebuildRoute();
            }
        }

        // ------------------------------------------------------------------ горячие клавиши

        private void HandleHotkeys()
        {
            if (!captureOnHotkeys) return;

            bool ctrl = IsHeld(KeyCode.LeftControl) || IsHeld(KeyCode.RightControl);

            if (Down(KeyCode.F8))
            {
                if (ctrl) ToggleSingularities();
                else TakeScreenshot();
            }
            if (Down(KeyCode.F10))
            {
                if (ctrl) ToggleObstacles();
                else ToggleVideo();
            }
            if (Down(KeyCode.F11)) TogglePendant();
            if (Down(KeyCode.F12)) ToggleHealth();
        }

        /// <summary>
        /// ЗАПАСНОЙ ПУТЬ ПО КЛАВИАТУРЕ СЛОМАЛСЯ — СКАЗАТЬ ОДИН РАЗ (30.10.2026, аудит S43).
        ///
        /// ПОЧЕМУ НЕ МОЛЧА. Оба метода ниже (`IsHeld`, `Down`) сначала пробуют новое Input System,
        /// а при исключении откатываются на старый Input Manager. Это правильно. Но пустой
        /// `catch {}` означал, что если новое API начнёт падать (например, после обновления
        /// пакета), оператор узнает об этом только по «клавиши не работают» — и будет искать
        /// причину в железе.
        ///
        /// ПОЧЕМУ ОДИН РАЗ. Методы вызываются из кадрового `Update`: строка на исключение
        /// затопила бы журнал и утопила остальные улики.
        /// </summary>
        private static bool inputFallbackLogged;

        private static void LogInputFallback(Exception e)
        {
            if (inputFallbackLogged) return;
            inputFallbackLogged = true;
            Debug.LogWarning("[Input] новое Input System недоступно (" + e.GetType().Name + ": " +
                             e.Message + ") — работаю через старый Input Manager. " +
                             "Если клавиши перестали отвечать, ищите причину здесь");
        }

        private static bool IsHeld(KeyCode code)
        {
            try
            {
                Keyboard k = Keyboard.current;
                if (k != null) return KeyOf(k, code) != null && KeyOf(k, code).isPressed;
            }
            catch (Exception e)
            {
                // ПУСТОЙ CATCH ЗАМЕНЁН (30.10.2026, аудит S43, пункт 5.7).
                //
                // Запасной путь ниже правильный — новое Input System не всегда доступно, и тогда
                // работает старый Input Manager. Но МОЛЧА глотать исключение нельзя: если новое
                // API начало падать (например, после обновления пакета), оператор узнает об этом
                // только по «клавиши не работают». Печатаем ОДИН раз за сеанс, чтобы не залить
                // журнал строкой на каждый кадр.
                LogInputFallback(e);
            }
            return Input.GetKey(code);
        }

        private static bool Down(KeyCode code)
        {
            try
            {
                Keyboard k = Keyboard.current;
                if (k != null)
                {
                    var key = KeyOf(k, code);
                    if (key != null) return key.wasPressedThisFrame;
                }
            }
            catch (Exception e)
            {
                LogInputFallback(e);   // см. пояснение в IsHeld: печатается один раз за сеанс
            }
            return Input.GetKeyDown(code);
        }

        private static UnityEngine.InputSystem.Controls.KeyControl KeyOf(Keyboard keyboard, KeyCode code)
        {
            switch (code)
            {
                case KeyCode.F5: return keyboard.f5Key;
                case KeyCode.F6: return keyboard.f6Key;
                case KeyCode.F7: return keyboard.f7Key;
                case KeyCode.F8: return keyboard.f8Key;
                case KeyCode.F9: return keyboard.f9Key;
                case KeyCode.F10: return keyboard.f10Key;
                case KeyCode.F11: return keyboard.f11Key;
                case KeyCode.F12: return keyboard.f12Key;
                case KeyCode.LeftControl: return keyboard.leftCtrlKey;
                case KeyCode.RightControl: return keyboard.rightCtrlKey;
                default: return null;
            }
        }

        // ------------------------------------------------------------------ действия

        /// <summary>Скриншот: подпись берётся из фактического состояния (ТЗ этап 3.1).</summary>
        public bool TakeScreenshot()
        {
            string state = flow != null ? flow.State.phase.ToString() : "—";
            string robot = flow != null && flow.Robot != null ? flow.Robot.robotName : "";
            return Capture.TakeScreenshot(state, robot);
        }

        public void ToggleVideo()
        {
            Capture.ToggleRecording();
        }

        public void ToggleSingularities() { Singularities.Toggle(); }
        public void ToggleHealth()
        {
            bool on = Health.Toggle();
            HealthPanel.SetVisible(on);
        }
        public void ToggleObstacles() { Obstacles.Toggle(); }
        public void TogglePendant() { Pendant.Toggle(); }

        /// <summary>Переезд в «домашнюю» позу (кнопка ДОМОЙ на пульте) штатным планировщиком.</summary>
        private void GoHome()
        {
            if (flow == null || !flow.Validator.Ready)
            {
                Debug.LogWarning("[Stages] ДОМОЙ: робот не определён");
                return;
            }
            if (flow.ExternalMotionRunning || flow.State.phase == FlowState.RobotMoving)
            {
                Debug.LogWarning("[Stages] ДОМОЙ: робот занят — сначала остановите движение");
                return;
            }

            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            double[] home = new double[v.Dof];
            for (int i = 0; i < home.Length; i++)
                home[i] = v.IsPrismatic(i) ? v.Lower[i] + (v.Upper[i] - v.Lower[i]) * 0.5 : 0.0;
            home = v.ContinueFrom(start, home);

            PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v, features != null ? features.World : null,
                start, home, "ДОМОЙ (нулевая поза)", 0.08f, 60);
            if (plan == null)
            {
                Debug.LogWarning("[Stages] ДОМОЙ: план не построен");
                return;
            }
            bool ok = flow.PlayExternalPlan(plan, plan.GoalQ, "переезд в домашнюю позу (пульт)");
            OnServiceMessage(ok ? "пульт: ПУСК «ДОМОЙ» — робот едет в нулевую позу"
                                : "пульт: «ДОМОЙ» отклонён (робот занят)");
        }

        /// <summary>Записать текущую позу (кнопка на пульте) — та же библиотека поз.</summary>
        private void RecordPose()
        {
            if (features == null || features.Poses == null)
            {
                Debug.LogWarning("[Stages] ЗАПИСАТЬ ПОЗУ: библиотека поз недоступна");
                return;
            }
            features.Poses.SaveCurrent("Поза " + FeatureStorage.TimeStamp());
            OnServiceMessage("пульт: поза записана (см. ветку «Позы» в дереве моделей)");
            if (ui != null) ui.RebuildTree(true);
        }

        /// <summary>ПУСК с пульта: продолжить паузу или отправить робота по выбранной траектории.</summary>
        private bool StartMotion()
        {
            if (flow == null) return false;

            if (flow.ExternalMotionRunning || (flow.Motion != null && flow.Motion.IsRunning))
            {
                if (flow.Motion != null && flow.Motion.Paused)
                {
                    flow.Motion.SetPaused(false);
                    OnServiceMessage("пульт: ПУСК — движение продолжено");
                    return true;
                }
                OnServiceMessage("пульт: робот уже движется");
                return true;
            }

            if (flow.State.phase == FlowState.PhantomsMoving)
            {
                bool ok = flow.SelectCandidateByIndex(flow.State.selectedTrajectory);
                OnServiceMessage(ok ? "пульт: ПУСК — робот пошёл по выбранной траектории"
                                    : "пульт: ПУСК отклонён");
                return ok;
            }

            OnServiceMessage("пульт: ПУСК — нечего запускать (нет выбранной траектории)");
            return false;
        }

        // ================================================================== ДЕРЕВО МОДЕЛЕЙ

        /// <summary>
        /// Ветки дерева моделей от этапов 1–8: «Промежуточные точки» (waypoints) и
        /// «Динамические препятствия» + «Сингулярности» (как узлы-состояния).
        /// </summary>
        public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,
            RobotController activeRobot)
        {
            KvStageHub hub = Instance;
            if (hub == null || roots == null) return;

            // --- ПРОМЕЖУТОЧНЫЕ ТОЧКИ (ЭТАП 5)
            if (hub.Waypoints != null && hub.Waypoints.Count > 0)
            {
                ProjectNode group = new ProjectNode("group:waypoints",
                    KvLoc.T("group.waypoints", "Промежуточные точки"), ProjectNodeKind.Group,
                    null, activeRobot);
                group.Key = "group:waypoints";
                group.Details = hub.Waypoints.Count.ToString();
                group.Tooltip = KvLoc.T("waypoint.title", "Промежуточные точки") +
                                ": маршрут строится штатным планировщиком через эти точки. " +
                                "Выберите узел и используйте кнопки «Добавить waypoint», " +
                                "«Переместить в точку прицела», «Раньше/Позже», «Удалить».";

                for (int i = 0; i < hub.Waypoints.Count; i++)
                {
                    KvWaypoint wp = hub.Waypoints.Items[i];
                    ProjectNode node = new ProjectNode("waypoint:" + i,
                        "#" + (i + 1) + "  " + wp.Position.x.ToString("0.00") + ", " +
                        wp.Position.y.ToString("0.00") + ", " + wp.Position.z.ToString("0.00"),
                        ProjectNodeKind.Point, wp.Marker != null ? wp.Marker.transform : null,
                        activeRobot);
                    node.Key = "waypoint:" + i;
                    node.Tag = wp;
                    node.Details = wp.Reachable
                        ? "✓ " + wp.Note
                        : "✗ " + KvLoc.T("waypoint.unreachable", "недостижима") + " · " + wp.Note;
                    node.Tooltip = "Промежуточная точка " + wp.Short + "\n" + wp.Note +
                                   "\n\nНедостижимая точка помечена КРАСНЫМ и в маршрут не берётся.";
                    group.Children.Add(node);
                }
                roots.Add(group);
            }

            // --- ДИНАМИЧЕСКИЕ ПРЕПЯТСТВИЯ (ЭТАП 7)
            if (hub.Obstacles != null && hub.Obstacles.Enabled)
            {
                ProjectNode node = new ProjectNode("group:obstacles",
                    KvLoc.T("group.obstacles", "Динамические препятствия"), ProjectNodeKind.Object,
                    null, activeRobot);
                node.Key = "group:obstacles";
                node.Details = hub.Obstacles.Status.Replace("\n", " ");
                node.Tooltip = hub.Obstacles.Status +
                               "\n\nПрепятствие учитывается планировщиком как геометрия сцены.";
                roots.Add(node);
            }

            // --- СИНГУЛЯРНОСТИ (ЭТАП 4)
            if (hub.Singularities != null && hub.Singularities.Enabled)
            {
                ProjectNode node = new ProjectNode("group:singularities",
                    KvLoc.T("group.singularities", "Сингулярности"), ProjectNodeKind.Group,
                    null, activeRobot);
                node.Key = "group:singularities";
                node.Details = hub.Singularities.Status;
                node.Tooltip = hub.Singularities.Status +
                               "\n\nЗоны вокруг суставов: жёлтый — близко, красный — сингулярность.";
                roots.Add(node);
            }
        }

        // ================================================================== СВОЙСТВА

        /// <summary>Строки свойств для узлов этапов 1–8 (waypoints, препятствие, сингулярности).</summary>
        public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)
        {
            KvStageHub hub = Instance;
            if (hub == null || node == null || list == null) return;

            if (node.Key != null && node.Key.StartsWith("waypoint:", StringComparison.Ordinal))
            {
                KvWaypoint wp = node.Tag as KvWaypoint;
                if (wp == null) return;
                list.Add(KvProp.Section(KvLoc.T("prop.section.waypoint", "Промежуточная точка")));
                list.Add(KvProp.Row("Порядок", "#" + (wp.Index + 1)));
                list.Add(KvProp.Row("X, м", wp.Position.x.ToString("0.000")));
                list.Add(KvProp.Row("Y, м", wp.Position.y.ToString("0.000")));
                list.Add(KvProp.Row("Z, м", wp.Position.z.ToString("0.000")));
                list.Add(KvProp.Row("Достижимость", wp.Reachable ? "✓ достижима" : "✗ недостижима"));
                list.Add(KvProp.Row("Запас", wp.Note));
                list.Add(KvProp.Section("Примечание"));
                list.Add(KvProp.Row("Маршрут", hub.Waypoints.HasRoute
                    ? hub.Waypoints.RouteNote : "не построен"));
                return;
            }

            if (node.Key == "group:obstacles")
            {
                list.Add(KvProp.Section(KvLoc.T("prop.section.obstacle", "Препятствие")));
                list.Add(KvProp.Row("Состояние", hub.Obstacles.Enabled ? "движется" : "выключено"));
                list.Add(KvProp.Row(KvLoc.T("obstacle.speed", "Скорость"),
                    hub.Obstacles.speed.ToString("0.00") + " м/с"));
                list.Add(KvProp.Row(KvLoc.T("obstacle.route", "Маршрут"),
                    hub.Obstacles.pathA.ToString("0.0") + " → " + hub.Obstacles.pathB.ToString("0.0")));
                list.Add(KvProp.Row("Пройдено", hub.Obstacles.Traveled.ToString("0.0") + " м"));
                list.Add(KvProp.Row("Риск", string.IsNullOrEmpty(hub.Obstacles.LastRisk)
                    ? "нет" : hub.Obstacles.LastRisk));
                return;
            }

            if (node.Key == "group:singularities")
            {
                list.Add(KvProp.Section(KvLoc.T("singularity.title", "Сингулярности")));
                list.Add(KvProp.Row("Состояние", hub.Singularities.Status));
                list.Add(KvProp.Row("Тип", hub.Singularities.Kind.ToString()));
                list.Add(KvProp.Row("Манёвренность",
                    (hub.Singularities.Mobility * 100f).ToString("0") + " %"));
                list.Add(KvProp.Row("Зон на экране", hub.Singularities.VisibleZones.ToString()));
                return;
            }
        }

        /// <summary>Выбор узла дерева: узел waypoint становится «выбранным» в редакторе.</summary>
        public static void OnNodeSelected(ProjectNode node)
        {
            KvStageHub hub = Instance;
            if (hub == null || hub.Waypoints == null) return;
            if (node == null || node.Key == null || !node.Key.StartsWith("waypoint:", StringComparison.Ordinal))
                return;
            int index;
            if (int.TryParse(node.Key.Substring("waypoint:".Length), out index))
                hub.Waypoints.Select(index);
        }

        // ================================================================== ПЕРЕКЛЮЧАТЕЛИ ФУНКЦИЙ

        /// <summary>Какие id функций обслуживает хаб этапов 1–8 (для панели настроек).</summary>
        public static bool HasFeature(string id)
        {
            switch (id)
            {
                case "scene.singularities":
                case "scene.obstacles":
                case "tool.health":
                case "tool.pendant":
                case "tool.waypoints":
                case "capture.annotate":
                    return Instance != null;
                default:
                    return false;
            }
        }

        /// <summary>Текущее значение переключателя (handled = false — id не наш).</summary>
        public static bool GetFeature(string id, out bool handled)
        {
            handled = true;
            KvStageHub hub = Instance;
            if (hub == null) { handled = false; return false; }

            switch (id)
            {
                case "scene.singularities": return hub.Singularities.Enabled;
                case "scene.obstacles": return hub.Obstacles.Enabled;
                case "tool.health": return hub.Health.Enabled;
                case "tool.pendant": return hub.Pendant.Visible;
                case "tool.waypoints": return hub.Waypoints.Count > 0;
                case "capture.annotate": return hub.Capture.annotate;
                default:
                    handled = false;
                    return false;
            }
        }

        /// <summary>Применить переключатель. true — id обработан хабом.</summary>
        public static bool SetFeature(string id, bool value)
        {
            KvStageHub hub = Instance;
            if (hub == null) return false;

            switch (id)
            {
                case "scene.singularities": hub.Singularities.SetEnabled(value); return true;
                case "scene.obstacles": hub.Obstacles.SetEnabled(value); return true;
                case "tool.health":
                    hub.Health.SetEnabled(value);
                    hub.HealthPanel.SetVisible(value);
                    return true;
                case "tool.pendant": hub.Pendant.SetVisible(value); return true;
                case "capture.annotate": hub.Capture.annotate = value; return true;
                default: return false;
            }
        }

        // ================================================================== КОМАНДЫ

        /// <summary>Регистрация команд этапов 1–8 (кнопки тулбара и пункты меню).</summary>
        public static void RegisterCommands(KazistovVvUIManager manager)
        {
            KvStageHub hub = Install(manager);
            if (hub == null) return;

            // --- ЭТАП 3.1: скриншот
            KvCommands.Register(new KvCommand
            {
                Id = "shot.screenshot",
                Title = "Сделать скриншот",
                Description = "PNG с подписью (дата, состояние, робот) в системную папку «Видео»",
                Hotkey = "F8",
                Icon = "screenshot",
                MenuPath = "Файл/Экспорт демонстрации/Скриншот",
                Execute = delegate { hub.TakeScreenshot(); }
            });

            // --- ЭТАП 3.2: видео
            KvCommands.Register(new KvCommand
            {
                Id = "video.toggle",
                Title = "Начать / остановить запись",
                Description = "Запись вида оператора (MP4 через Unity Recorder) в папку «Видео»",
                Hotkey = "F10",
                Icon = "video",
                MenuPath = "Файл/Экспорт демонстрации/Запись видео",
                Execute = delegate { hub.ToggleVideo(); },
                IsChecked = delegate { return hub.Capture != null && hub.Capture.IsRecording; },
                CheckedTint = delegate { return (Color?)new Color(1f, 0.25f, 0.25f); }
            });

            KvCommands.Register(new KvCommand
            {
                Id = "shot.annotate",
                Title = "Подпись на скриншоте",
                Description = "Рисовать дату, состояние State Machine и выбранного робота поверх снимка",
                Icon = "screenshot",
                MenuPath = "Файл/Экспорт демонстрации/Подпись на скриншоте",
                Execute = delegate
                {
                    hub.Capture.annotate = !hub.Capture.annotate;
                    Debug.Log("[Capture] подпись на скриншоте: " +
                              (hub.Capture.annotate ? "включена" : "выключена"));
                },
                IsChecked = delegate { return hub.Capture != null && hub.Capture.annotate; }
            });

            // --- ЭТАП 4: сингулярности
            KvCommands.Register(new KvCommand
            {
                Id = "view.singularity",
                Title = "Сингулярности",
                Description = "Зоны потери манёвренности вокруг суставов (жёлтый — близко, красный — опасно)",
                Hotkey = "Ctrl+F8",
                Icon = "singularity",
                MenuPath = "Вид/Сингулярности",
                Execute = delegate { hub.ToggleSingularities(); },
                IsChecked = delegate { return hub.Singularities != null && hub.Singularities.Enabled; }
            });

            // --- ЭТАП 5: waypoints
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.add",
                Title = "Добавить waypoint",
                Description = "Добавить промежуточную точку (в зафиксированной точке или в точке прицела)",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Добавить waypoint",
                Execute = delegate { hub.Waypoints.AddFromAim(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.move",
                Title = "Переместить waypoint",
                Description = "Перенести выбранную промежуточную точку в текущую точку прицела",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Переместить в точку прицела",
                Execute = delegate { hub.Waypoints.MoveSelectedToAim(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.earlier",
                Title = "Waypoint раньше",
                Description = "Поднять выбранную точку в порядке обхода",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Раньше",
                Execute = delegate { hub.Waypoints.MoveUp(hub.Waypoints.SelectedIndex); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.later",
                Title = "Waypoint позже",
                Description = "Опустить выбранную точку в порядке обхода",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Позже",
                Execute = delegate { hub.Waypoints.MoveDown(hub.Waypoints.SelectedIndex); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.delete",
                Title = "Удалить waypoint",
                Description = "Убрать выбранную промежуточную точку (траектория вернётся к прежней форме)",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Удалить",
                Execute = delegate { hub.Waypoints.RemoveSelected(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.clear",
                Title = "Очистить waypoints",
                Description = "Удалить все промежуточные точки и снять маршрут",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Очистить все",
                Execute = delegate { hub.Waypoints.Clear("кнопка интерфейса"); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.build",
                Title = "Перестроить маршрут",
                Description = "Построить траекторию через промежуточные точки штатным планировщиком (BiRRT)",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Перестроить маршрут",
                Execute = delegate { hub.Waypoints.RebuildRoute(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "waypoint.play",
                Title = "Выполнить маршрут",
                Description = "Отправить робота по маршруту через промежуточные точки",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Выполнить маршрут",
                Execute = delegate { hub.Waypoints.PlayRoute(); }
            });

            // --- ЭТАП 6: мониторинг состояния
            KvCommands.Register(new KvCommand
            {
                Id = "health.toggle",
                Title = "Мониторинг состояния",
                Description = "Графики в реальном времени: температура, ток, скорость, износ суставов",
                Hotkey = "F12",
                Icon = "health",
                MenuPath = "Вид/Мониторинг состояния",
                Execute = delegate { hub.ToggleHealth(); },
                IsChecked = delegate { return hub.Health != null && hub.Health.Enabled; }
            });

            // --- ЭТАП 7: динамические препятствия
            KvCommands.Register(new KvCommand
            {
                Id = "obstacle.toggle",
                Title = "Динамические препятствия",
                Description = "Движущаяся тележка: планировщик учитывает её, траектории помечаются как рискованные",
                Hotkey = "Ctrl+F10",
                Icon = "obstacle",
                MenuPath = "Вид/Динамические препятствия",
                Execute = delegate { hub.ToggleObstacles(); },
                IsChecked = delegate { return hub.Obstacles != null && hub.Obstacles.Enabled; }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "obstacle.evaluate",
                Title = "Проверить траектории на риск",
                Description = "Сверить построенные варианты с прогнозом движения препятствия",
                Icon = "obstacle",
                MenuPath = "Вид/Динамические препятствия/Проверить варианты",
                Execute = delegate { hub.Obstacles.EvaluateVariants(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "obstacle.discard",
                Title = "Отбрасывать рискованные траектории",
                Description = "Вкл: траектории, пересекающие путь препятствия, отбрасываются (выкл: помечаются)",
                Icon = "obstacle",
                MenuPath = "Вид/Динамические препятствия/Отбрасывать рискованные",
                Execute = delegate
                {
                    hub.Obstacles.discardRisky = !hub.Obstacles.discardRisky;
                    Debug.Log("[Obstacle] рискованные траектории " +
                              (hub.Obstacles.discardRisky ? "отбрасываются" : "только помечаются"));
                },
                IsChecked = delegate { return hub.Obstacles != null && hub.Obstacles.discardRisky; }
            });

            // --- ЭТАП 8: виртуальный пульт
            KvCommands.Register(new KvCommand
            {
                Id = "pendant.toggle",
                Title = "Виртуальный пульт",
                Description = "Teach pendant: экран TCP, джойстик, кнопки ПУСК/СТОП/ДОМОЙ/ЗАПИСАТЬ ПОЗУ, LED",
                Hotkey = "F11",
                Icon = "pendant",
                MenuPath = "Сервис/Виртуальный пульт",
                Execute = delegate { hub.TogglePendant(); },
                IsChecked = delegate { return hub.Pendant != null && hub.Pendant.Visible; }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "pendant.variant",
                Title = "Вариант пульта (A / B)",
                Description = "Переключить оформление: A — промышленный, B — стиль проекта АДДОН",
                Icon = "pendant",
                MenuPath = "Сервис/Виртуальный пульт/Вариант A / B",
                Execute = delegate { hub.Pendant.ToggleVariant(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "pendant.attach",
                Title = "Закрепление пульта",
                Description = "Пульт парит в сцене → в руке оператора → над роботом",
                Icon = "pendant",
                MenuPath = "Сервис/Виртуальный пульт/Закрепление",
                Execute = delegate
                {
                    KvPendantAttach next = hub.Pendant.Attach == KvPendantAttach.Floating
                        ? KvPendantAttach.FollowCamera
                        : hub.Pendant.Attach == KvPendantAttach.FollowCamera
                            ? KvPendantAttach.AboveRobot
                            : KvPendantAttach.Floating;
                    hub.Pendant.SetAttach(next);
                }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "pendant.jogmode",
                Title = "Режим джойстика (JOINT / TCP)",
                Description = "Джойстик покачивает выбранный сустав либо сдвигает TCP в плоскости",
                Icon = "pendant",
                MenuPath = "Сервис/Виртуальный пульт/Режим джойстика",
                Execute = delegate { hub.Pendant.ToggleJogMode(); }
            });

            Debug.Log("[Stages] команды этапов 1–8 зарегистрированы · всего команд: " +
                      KvCommands.All.Count);
        }
    }
}
