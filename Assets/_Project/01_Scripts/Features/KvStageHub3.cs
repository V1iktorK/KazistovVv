using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ХАБ ЭТАПОВ 7–12 ЭТОЙ СЕССИИ:
    ///   7 — ограничения на промежуточные точки (ориентация TCP, скорость, обход, пауза);
    ///   8 — планирование с ограничениями (вертикальный инструмент, предел наклона, взгляд на объект);
    ///   9 — калибровочный мастер (TCP по 4 точкам, база робота, камера — заглушка);
    ///  10 — калькулятор нагрузки (максимум в текущей позе + график от расстояния до базы);
    ///  11 — экспорт траектории в языки роботов KUKA KRL / FANUC KAREL / ABB RAPID;
    ///  12 — импорт моделей роботов URDF / STEP с проверкой кинематики.
    ///
    /// Роль та же, что у <see cref="FeatureHub"/>, <see cref="KvStageHub"/> и
    /// <see cref="KvStageHub2"/>: единая точка создания сервисов, кадрового обслуживания,
    /// горячих клавиш, дерева моделей, свойств, переключателей и команд. Существующая
    /// логика не переписывается — используются только публичные методы потока этапов,
    /// планировщика, валидатора и оболочки.
    ///
    /// ГОРЯЧИЕ КЛАВИШИ (проверено — свободны: заняты F1–F4, F5–F8, F10–F12):
    ///   F9 — верстак на вкладке ограничений планирования.
    /// Штатные бинды не тронуты.
    /// </summary>
    [DefaultExecutionOrder(70)]
    public class KvStageHub3 : MonoBehaviour
    {
        public static KvStageHub3 Instance { get; private set; }

        // ------------------------------------------------------------------ сервисы
        public KvConstrainedPlanner Constrained { get; private set; }
        public KvCalibrationService Calibration { get; private set; }
        public KvPayloadCalculator Payload { get; private set; }
        public KvRobotExporter Exporter { get; private set; }
        public KvRobotImportService Import { get; private set; }

        /// <summary>Единые лимиты движения (те же, что у этапов 4–6).</summary>
        public KvMotionLimits Limits { get; private set; }

        [Header("Этап 12: импорт моделей")]
        public bool scanModelsOnStart = true;

        [Header("Этап 7: ограничения промежуточных точек")]
        public bool waypointConstraints = true;

        [Header("Диагностика")]
        public bool logEvents = true;

        // ------------------------------------------------------------------ состояние
        private KazistovVvUIManager ui;
        private TrajectoryFlowController flow;
        private FeatureHub features;
        private bool servicesBound;
        private RobotController boundRobot;
        private KvWaypointTab waypointTab;

        // ================================================================== создание

        /// <summary>Установить хаб (вызывается UI-менеджером при регистрации команд).</summary>
        public static KvStageHub3 Install(KazistovVvUIManager manager)
        {
            if (Instance != null) return Instance;
            if (manager == null) return null;
            KvStageHub3 hub = manager.gameObject.GetComponent<KvStageHub3>();
            if (hub == null) hub = manager.gameObject.AddComponent<KvStageHub3>();
            return hub;
        }

        public static KvStageHub3 Current { get { return Instance; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Строки этапов 7–12 (7 языков) — в словари интерфейса.
            KvLocExtra2.Install();

            Limits = KvStageHub2.Current != null && KvStageHub2.Current.Limits != null
                ? KvStageHub2.Current.Limits
                : new KvMotionLimits();

            Constrained = new KvConstrainedPlanner();
            Calibration = new KvCalibrationService();
            Payload = new KvPayloadCalculator();
            Exporter = new KvRobotExporter();
            Import = new KvRobotImportService();

            Constrained.Message += OnServiceMessage;
            Calibration.Message += OnServiceMessage;
            Payload.Message += OnServiceMessage;
            Exporter.Message += OnServiceMessage;
            Import.Message += OnServiceMessage;

            Debug.Log("[Stages3] хаб этапов 7–12 поднят (ограничения точек, ограниченное планирование, " +
                      "калибровка, нагрузка, экспорт в языки роботов, импорт моделей) · " +
                      "строк локализации: " + KvLocExtra2.RegisteredCount);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Отписка от сообщений сервисов (утечка при повторном входе в PlayMode).
            if (Constrained != null) Constrained.Message -= OnServiceMessage;
            if (Calibration != null) Calibration.Message -= OnServiceMessage;
            if (Payload != null) Payload.Message -= OnServiceMessage;
            if (Exporter != null) Exporter.Message -= OnServiceMessage;
            if (Import != null) Import.Message -= OnServiceMessage;
        }

        private void OnServiceMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (KvActionLog.Instance != null) KvActionLog.Instance.Add(KvLogKind.Ui, message);
        }

        // ================================================================== кадровое обслуживание

        private void Update()
        {
            if (ui == null) ui = KazistovVvUIManager.Instance;
            if (ui == null) return;
            if (flow == null) flow = ui.Flow;
            if (flow == null) return;

            float dt = Time.unscaledDeltaTime;
            BindServices();

            Constrained.Tick(dt);
            Payload.Tick(dt);
            HandleHotkeys();
        }

        private void BindServices()
        {
            if (flow.Validator == null || !flow.Validator.Ready) return;

            if (!servicesBound)
            {
                servicesBound = true;
                features = FeatureHub.Current;

                Constrained.Bind(flow, features != null ? features.World : null, Limits);
                Calibration.Bind(flow, features != null ? features.World : null);
                Payload.Bind(flow, features != null ? features.World : null);
                Exporter.Bind(flow);
                Import.Bind(flow, features != null ? features.World : null);

                // Ограничения скорости в точках маршрута считаются по тем же лимитам, что у этапов 4–6.
                if (KvStageHub.Current != null && KvStageHub.Current.Waypoints != null)
                {
                    KvStageHub.Current.Waypoints.SetMotionLimits(Limits);
                    KvStageHub.Current.Waypoints.useWaypointLimits = waypointConstraints;
                }

                RegisterTabs();
                Calibration.Load();
                if (scanModelsOnStart) Import.RefreshFiles();

                Debug.Log("[Stages3] сервисы этапов 7–12 привязаны к потоку этапов (робот: " +
                          flow.Validator.RobotName + ") · вкладок верстака: " +
                          KvWorkbenchWindow.TabCount);
            }

            if (flow.Robot != null && flow.Robot != boundRobot)
            {
                boundRobot = flow.Robot;
                Constrained.Invalidate();
                Payload.Evaluate(flow.Validator.CopyCurrent());
                Debug.Log("[Stages3] ограничения и нагрузка перенастроены на робота «" +
                          boundRobot.robotName + "» (" + flow.Validator.Dof + " осей)");
            }
        }

        private void RegisterTabs()
        {
            KvWorkbenchWindow window = KvWorkbenchWindow.Instance;
            if (window == null) return;

            if (KvStageHub.Current != null && KvStageHub.Current.Waypoints != null && waypointTab == null)
            {
                waypointTab = new KvWaypointTab(KvStageHub.Current.Waypoints,
                    delegate { return flow; },
                    delegate
                    {
                        FreeFlyCameraController rig = ui != null ? ui.CameraRig : null;
                        return rig != null ? rig.AimPointPublic : Vector3.zero;
                    },
                    delegate (int index) { if (ui != null) ui.RebuildTree(true); });
                KvWorkbenchWindow.RegisterTab(waypointTab);
            }

            KvWorkbenchWindow.RegisterTab(new KvConstrainedTab(Constrained, AimPoint));
            KvWorkbenchWindow.RegisterTab(new KvCalibrationTab(Calibration));
            KvWorkbenchWindow.RegisterTab(new KvPayloadTab(Payload));
            KvWorkbenchWindow.RegisterTab(new KvExportTab(Exporter));
            KvWorkbenchWindow.RegisterTab(new KvImportTab(Import));
        }

        private Vector3 AimPoint()
        {
            FreeFlyCameraController rig = ui != null ? ui.CameraRig : null;
            if (rig != null && rig.AimPointPublic.sqrMagnitude > 1e-6f) return rig.AimPointPublic;
            if (flow != null && flow.State != null && flow.State.hasPoint) return flow.State.point;
            return Vector3.zero;
        }

        // ------------------------------------------------------------------ горячие клавиши

        private void HandleHotkeys()
        {
            if (Down(KeyCode.F9)) OpenTab("constraints");
        }

        private static bool Down(KeyCode code)
        {
            try
            {
                Keyboard k = Keyboard.current;
                if (k != null)
                {
                    UnityEngine.InputSystem.Controls.KeyControl key = KeyOf(k, code);
                    if (key != null) return key.wasPressedThisFrame;
                }
            }
            catch (Exception) { }
            return Input.GetKeyDown(code);
        }

        private static UnityEngine.InputSystem.Controls.KeyControl KeyOf(Keyboard keyboard, KeyCode code)
        {
            switch (code)
            {
                case KeyCode.F9: return keyboard.f9Key;
                default: return null;
            }
        }

        // ================================================================== действия

        public void OpenTab(string tabKey)
        {
            if (KvWorkbenchWindow.Instance == null) return;
            KvWorkbenchWindow.Instance.Show(tabKey);
        }

        /// <summary>Ограничение ориентации выбранной промежуточной точки «как сейчас» (этап 7).</summary>
        public void WaypointOrientationFromCurrent()
        {
            KvWaypoint wp = SelectedWaypoint();
            if (wp == null) return;
            wp.Limits.Orientation.mode = KvOrientMode.ToolDirection;
            wp.Limits.Orientation.direction = KvToolKinematics.ToolAxis(flow.Validator,
                flow.Validator.CopyCurrent());
            RebuildWaypointRoute("ориентация из текущей позы инструмента");
        }

        /// <summary>Ограничить скорость в выбранной точке (этап 7).</summary>
        public void WaypointSpeedLimit()
        {
            KvWaypoint wp = SelectedWaypoint();
            if (wp == null) return;
            wp.Limits.LimitSpeed = !wp.Limits.LimitSpeed;
            RebuildWaypointRoute("ограничение скорости в точке " +
                                 (wp.Limits.LimitSpeed ? "включено" : "выключено"));
        }

        /// <summary>Обязательный обход препятствия у выбранной точки (этап 7).</summary>
        public void WaypointDetour()
        {
            KvWaypoint wp = SelectedWaypoint();
            if (wp == null) return;
            wp.Limits.Detour = !wp.Limits.Detour;
            if (wp.Limits.Detour) wp.Limits.DetourCenter = AimPoint();
            RebuildWaypointRoute("обход препятствия " + (wp.Limits.Detour ? "включён" : "выключен"));
        }

        /// <summary>Пауза в выбранной точке (этап 7).</summary>
        public void WaypointPause()
        {
            KvWaypoint wp = SelectedWaypoint();
            if (wp == null) return;
            wp.Limits.Pause = !wp.Limits.Pause;
            RebuildWaypointRoute("пауза " + (wp.Limits.Pause
                ? "включена (" + wp.Limits.PauseSeconds.ToString("0.0") + " с)"
                : "выключена"));
        }

        /// <summary>Снять все ограничения выбранной точки (этап 7).</summary>
        public void WaypointClearLimits()
        {
            KvWaypoint wp = SelectedWaypoint();
            if (wp == null) return;
            wp.Limits.Clear();
            RebuildWaypointRoute("ограничения точки сняты");
        }

        private KvWaypoint SelectedWaypoint()
        {
            if (KvStageHub.Current == null || KvStageHub.Current.Waypoints == null)
            {
                Debug.LogWarning("[Stages3] waypoint-редактор недоступен");
                return null;
            }
            KvWaypointManager manager = KvStageHub.Current.Waypoints;
            int index = manager.SelectedIndex;
            if (index < 0 || index >= manager.Count)
            {
                Debug.Log("[Stages3] waypoint не выбран — выберите узел в ветке " +
                          "«Промежуточные точки» или добавьте точку");
                return null;
            }
            return manager.Items[index];
        }

        private void RebuildWaypointRoute(string what)
        {
            KvWaypointManager manager = KvStageHub.Current != null
                ? KvStageHub.Current.Waypoints : null;
            if (manager == null) return;
            manager.EvaluateAll();
            manager.RebuildRoute();
            Debug.Log("[Stages3] ограничения waypoint: " + what + " · " + manager.RouteNote);
            if (ui != null) ui.RebuildTree(true);
        }

        /// <summary>Включить/выключить планирование с ограничениями (этап 8).</summary>
        public void ToggleConstrained()
        {
            Constrained.Enabled = !Constrained.Enabled;
            Debug.Log("[Stages3] планирование с ограничениями " +
                      (Constrained.Enabled ? "включено" : "выключено") + " · " +
                      Constrained.Profile.Describe());
        }

        /// <summary>Экспорт выбранной траектории в язык робота (этап 11).</summary>
        public void ExportSelected()
        {
            Exporter.ExportSelected();
        }

        /// <summary>Переключить язык робота для экспорта (этап 11).</summary>
        public void CycleExportLanguage()
        {
            Exporter.CycleLanguage();
        }

        // ================================================================== ДЕРЕВО МОДЕЛЕЙ

        /// <summary>Подпись состояния этапов 7–12 для пересборки дерева.</summary>
        public static string TreeSignaturePart()
        {
            KvStageHub3 hub = Instance;
            if (hub == null) return "нет";
            return (hub.Constrained != null && hub.Constrained.Enabled ? "1" : "0") + ":" +
                   (hub.Constrained != null ? hub.Constrained.ViolatingCount : 0) + ":" +
                   (hub.Payload != null && hub.Payload.Last != null && hub.Payload.Last.valid
                       ? hub.Payload.Last.maxKg.ToString("0.00") : "-") + ":" +
                   (hub.Exporter != null ? hub.Exporter.Language.ToString() : "-") + ":" +
                   (hub.Import != null ? hub.Import.Files.Count : 0) + ":" +
                   (hub.Calibration != null && hub.Calibration.TcpSolved ? "tcp" : "-");
        }

        /// <summary>Ветки дерева моделей этапов 7–12.</summary>
        public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,
            RobotController activeRobot)
        {
            KvStageHub3 hub = Instance;
            if (hub == null || roots == null) return;

            ProjectNode group = new ProjectNode("group:stages3",
                "Ограничения, калибровка и экспорт", ProjectNodeKind.Group, null, activeRobot);
            group.Key = "group:stages3";
            group.Tooltip = "Этапы 7–12: ограничения промежуточных точек, планирование с ограничениями, " +
                            "калибровка, нагрузка, экспорт в языки роботов, импорт моделей.";

            int limited = CountLimitedWaypoints();
            group.Details = limited > 0 ? limited + " точек с ограничениями" : "";

            // --- этап 7: ограничения точек
            ProjectNode wp = new ProjectNode("stage3:waypoints",
                KvLocExtra2.T("wp.limits", "Ограничения точки"), ProjectNodeKind.Object,
                null, activeRobot);
            wp.Key = "stage3:waypoints";
            wp.Details = limited + " из " + (KvStageHub.Current != null && KvStageHub.Current.Waypoints != null
                ? KvStageHub.Current.Waypoints.Count : 0) + " точек";
            wp.Tooltip = "Ориентация TCP, скорость в точке, обязательный обход препятствия и пауза " +
                         "задаются на вкладке «Ограничения точки» (F2).";
            group.Children.Add(wp);

            // --- этап 8: ограничения планирования
            ProjectNode constr = new ProjectNode("stage3:constraints",
                KvLocExtra2.T("constr.title", "Планирование с ограничениями"), ProjectNodeKind.Object,
                null, activeRobot);
            constr.Key = "stage3:constraints";
            constr.Details = hub.Constrained.Enabled ? hub.Constrained.Profile.Describe() : "выключено";
            constr.Tooltip = KvLocExtra2.T("constr.info", "Ограничения планирования");
            group.Children.Add(constr);

            // --- этап 9: калибровка
            ProjectNode calib = new ProjectNode("stage3:calibration",
                KvLocExtra2.T("calib.title", "Калибровочный мастер"), ProjectNodeKind.Object,
                null, activeRobot);
            calib.Key = "stage3:calibration";
            calib.Details = hub.Calibration.TcpSolved
                ? "TCP " + (hub.Calibration.Data.tcpLength * 1000f).ToString("0.0") + " мм · точек " +
                  hub.Calibration.Data.tcpPoints
                : KvLocExtra2.T("calib.none", "калибровка не выполнялась");
            calib.Tooltip = "Калибровка TCP по 4 точкам, базы робота по 3 точкам, камеры (заглушка).";
            group.Children.Add(calib);

            // --- этап 10: нагрузка
            ProjectNode payload = new ProjectNode("stage3:payload",
                KvLocExtra2.T("payload.title", "Калькулятор нагрузки"), ProjectNodeKind.Object,
                null, activeRobot);
            payload.Key = "stage3:payload";
            payload.Details = hub.Payload.Last != null && hub.Payload.Last.valid
                ? hub.Payload.Last.maxKg.ToString("0.00") + " кг"
                : "—";
            payload.Tooltip = KvLocExtra2.T("payload.info", "Калькулятор нагрузки");
            group.Children.Add(payload);

            // --- этап 11: экспорт
            ProjectNode export = new ProjectNode("stage3:export",
                KvLocExtra2.T("export.title", "Экспорт траектории"), ProjectNodeKind.Object,
                null, activeRobot);
            export.Key = "stage3:export";
            export.Details = hub.Exporter.LanguageLabel + " · trajectory" + hub.Exporter.Extension;
            export.Tooltip = KvLocExtra2.T("export.info", "Экспорт траектории в язык робота");
            group.Children.Add(export);

            // --- этап 12: импорт
            ProjectNode import = new ProjectNode("stage3:import",
                KvLocExtra2.T("import.title", "Импорт модели робота"), ProjectNodeKind.Object,
                null, activeRobot);
            import.Key = "stage3:import";
            import.Details = hub.Import.Imported != null
                ? hub.Import.Imported.name
                : hub.Import.Files.Count + " файл(ов)";
            import.Tooltip = KvLocExtra2.T("import.info", "Импорт моделей URDF / STEP");
            group.Children.Add(import);

            roots.Add(group);

            // --- пометки вариантов траекторий, нарушающих ограничение (этап 8)
            if (hub.Constrained.Active != null)
            {
                for (int r = 0; r < roots.Count; r++)
                {
                    ProjectNode trajectories = roots[r];
                    if (trajectories == null || trajectories.Key != "group:trajectories") continue;
                    for (int i = 0; i < trajectories.Children.Count; i++)
                    {
                        ProjectNode node = trajectories.Children[i];
                        TrajectoryCandidate candidate = node.Tag as TrajectoryCandidate;
                        if (candidate == null) continue;
                        KvToolKinematics.PlanCheck check = hub.Constrained.Check(candidate);
                        if (check.samples == 0 || check.violations == 0) continue;
                        node.Details = "◈ " + node.Details;
                        node.Tooltip += "\n\n◈ " + KvLocExtra2.T("constr.status",
                            "Состояние ограничения") + ": " +
                            KvLocExtra2.F("constr.violated",
                                "нарушений: {0} из {1} сэмплов · худший угол {2}°",
                                check.violations, check.samples, check.worstAngle.ToString("0.0"));
                    }
                }
            }
        }

        private static int CountLimitedWaypoints()
        {
            KvStageHub hub = KvStageHub.Current;
            if (hub == null || hub.Waypoints == null) return 0;
            int count = 0;
            for (int i = 0; i < hub.Waypoints.Count; i++)
                if (hub.Waypoints.Items[i] != null && hub.Waypoints.Items[i].HasLimits) count++;
            return count;
        }

        // ================================================================== СВОЙСТВА

        /// <summary>Строки свойств этапов 7–12 (узлы группы, робот — нагрузка, варианты — ограничение).</summary>
        public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)
        {
            KvStageHub3 hub = Instance;
            if (hub == null || node == null || list == null) return;

            // --- нагрузка в панели свойств РОБОТА (требование ТЗ этапа 10)
            if (node.Kind == ProjectNodeKind.Robot && hub.Payload != null)
            {
                KvPayloadResult payload = hub.Payload.Last;
                list.Add(KvProp.Section(KvLocExtra2.T("payload.title", "Калькулятор нагрузки")));
                list.Add(KvProp.Row(KvLocExtra2.T("payload.now",
                        "Максимальная нагрузка в текущей позе, кг"),
                    payload != null && payload.valid ? payload.maxKg.ToString("0.00") : "—"));
                if (payload != null && payload.valid)
                {
                    list.Add(KvProp.Row(KvLocExtra2.T("payload.limit", "Ограничивающий сустав"),
                        "J" + (payload.limitingJoint + 1) + " · " +
                        payload.limitingValue.ToString("0.0") + "/" +
                        payload.limitingRating.ToString("0.0") + " Н·м"));
                    list.Add(KvProp.Row(KvLocExtra2.T("payload.distance", "Расстояние от базы, м"),
                        payload.tcpDistance.ToString("0.000")));
                    list.Add(KvProp.Row(KvLocExtra2.T("payload.safety", "Коэффициент запаса"),
                        hub.Payload.Model.safety.ToString("0.00")));
                }
                return;
            }

            // --- проверка ограничения у варианта траектории (этап 8)
            TrajectoryCandidate candidate = node.Tag as TrajectoryCandidate;
            if (candidate != null && hub.Constrained != null && hub.Constrained.Active != null)
            {
                KvToolKinematics.PlanCheck check = hub.Constrained.Check(candidate);
                list.Add(KvProp.Section(KvLocExtra2.T("constr.title", "Планирование с ограничениями")));
                list.Add(KvProp.Row(KvLocExtra2.T("constr.mode", "Тип ограничения"),
                    hub.Constrained.Profile.Describe()));
                list.Add(KvProp.Row(KvLocExtra2.T("constr.status", "Состояние ограничения"),
                    check.samples == 0
                        ? "—"
                        : check.violations == 0
                            ? "✓ соблюдено (" + check.compliance.ToString("P0") + ")"
                            : "⚠ " + KvLocExtra2.F("constr.violated",
                                "нарушений: {0} из {1} сэмплов · худший угол {2}°",
                                check.violations, check.samples, check.worstAngle.ToString("0.0"))));
                return;
            }

            // --- узлы группы этапов 7–12
            if (node.Key == "stage3:constraints")
            {
                list.Add(KvProp.Section(KvLocExtra2.T("constr.title", "Планирование с ограничениями")));
                list.Add(KvProp.Row(KvLocExtra2.T("constr.enable", "Включить ограничения планирования"),
                    hub.Constrained.Enabled ? "да" : "нет"));
                list.Add(KvProp.Row(KvLocExtra2.T("constr.mode", "Тип ограничения"),
                    hub.Constrained.Profile.Describe()));
                list.Add(KvProp.Row(KvLocExtra2.T("constr.tolerance", "Допуск, °"),
                    hub.Constrained.Profile.toleranceDeg.ToString("0.0")));
                list.Add(KvProp.Row(KvLocExtra2.T("constr.filter",
                        "Отбрасывать варианты, нарушающие ограничение"),
                    hub.Constrained.DiscardViolating ? "да" : "нет"));
                list.Add(KvProp.Row("Сэмплов проверено", hub.Constrained.EvaluatedCount.ToString()));
                return;
            }

            if (node.Key == "stage3:calibration")
            {
                KvCalibrationData data = hub.Calibration.Data;
                list.Add(KvProp.Section(KvLocExtra2.T("calib.title", "Калибровочный мастер")));
                list.Add(KvProp.Row(KvLocExtra2.T("calib.points", "Записано точек") + " (TCP)",
                    hub.Calibration.TcpPointCount + " / " + KvCalibrationService.TcpPointsNeeded));
                if (data.tcpSolved)
                {
                    list.Add(KvProp.Row(KvLocExtra2.T("calib.result",
                            "Смещение инструмента (в системе фланца), мм"),
                        (data.tcpOffsetFlange[0] * 1000f).ToString("0.0") + ", " +
                        (data.tcpOffsetFlange[1] * 1000f).ToString("0.0") + ", " +
                        (data.tcpOffsetFlange[2] * 1000f).ToString("0.0")));
                    list.Add(KvProp.Row(KvLocExtra2.T("calib.length", "Длина инструмента, мм"),
                        (data.tcpLength * 1000f).ToString("0.0")));
                    list.Add(KvProp.Row(KvLocExtra2.T("calib.residual", "Остаточная ошибка, мм"),
                        data.tcpResidualMm.ToString("0.00") + " (худшая " +
                        data.tcpMaxResidualMm.ToString("0.00") + ")"));
                }
                if (data.baseSolved)
                {
                    list.Add(KvProp.Row(KvLocExtra2.T("calib.base", "Калибровка базы робота"),
                        data.baseHeightMm.ToString("0.0") + " мм · " +
                        data.baseTiltDeg.ToString("0.00") + "°"));
                }
                list.Add(KvProp.Row(KvLocExtra2.T("calib.camera", "Калибровка камеры"),
                    data.cameraNote));
                list.Add(KvProp.Row(KvLocExtra2.T("calib.file", "Файл калибровки"),
                    hub.Calibration.FilePath));
                return;
            }

            if (node.Key == "stage3:payload")
            {
                list.Add(KvProp.Section(KvLocExtra2.T("payload.title", "Калькулятор нагрузки")));
                KvPayloadResult payload = hub.Payload.Last;
                list.Add(KvProp.Row(KvLocExtra2.T("payload.now",
                        "Максимальная нагрузка в текущей позе, кг"),
                    payload != null && payload.valid ? payload.maxKg.ToString("0.00") : "—"));
                if (payload != null && payload.valid)
                {
                    list.Add(KvProp.Row(KvLocExtra2.T("payload.limit", "Ограничивающий сустав"),
                        "J" + (payload.limitingJoint + 1)));
                    list.Add(KvProp.Row(KvLocExtra2.T("payload.distance", "Расстояние от базы, м"),
                        payload.tcpDistance.ToString("0.000")));
                    if (payload.distances != null)
                        list.Add(KvProp.Row("Точек на графике", payload.distances.Length.ToString()));
                }
                return;
            }

            if (node.Key == "stage3:export")
            {
                list.Add(KvProp.Section(KvLocExtra2.T("export.title", "Экспорт траектории")));
                list.Add(KvProp.Row(KvLocExtra2.T("export.language", "Язык робота"),
                    hub.Exporter.LanguageLabel));
                list.Add(KvProp.Row("Файл", "trajectory" + hub.Exporter.Extension));
                list.Add(KvProp.Row(KvLocExtra2.T("export.folder", "Папка экспорта"),
                    hub.Exporter.FolderPath));
                if (!string.IsNullOrEmpty(hub.Exporter.LastFile))
                    list.Add(KvProp.Row("Последний экспорт",
                        hub.Exporter.LastFile + " · строк " + hub.Exporter.LastLines));
                return;
            }

            if (node.Key == "stage3:import")
            {
                list.Add(KvProp.Section(KvLocExtra2.T("import.title", "Импорт модели робота")));
                list.Add(KvProp.Row("Файлов найдено", hub.Import.Files.Count.ToString()));
                list.Add(KvProp.Row(KvLocExtra2.T("import.folder", "Папки поиска"),
                    string.Join("; ", hub.Import.SearchFolders())));
                if (!string.IsNullOrEmpty(hub.Import.LastReport))
                    list.Add(KvProp.Row("Импорт", hub.Import.LastReport));
                if (!string.IsNullOrEmpty(hub.Import.LastCheck))
                    list.Add(KvProp.Row(KvLocExtra2.T("import.check", "Проверить кинематику"),
                        hub.Import.LastCheck));
                return;
            }

            if (node.Key == "stage3:waypoints")
            {
                list.Add(KvProp.Section(KvLocExtra2.T("wp.limits", "Ограничения точки")));
                KvStageHub hub2 = KvStageHub.Current;
                if (hub2 != null && hub2.Waypoints != null)
                {
                    list.Add(KvProp.Row("Точек в маршруте", hub2.Waypoints.Count.ToString()));
                    for (int i = 0; i < hub2.Waypoints.Count; i++)
                    {
                        KvWaypoint wp = hub2.Waypoints.Items[i];
                        if (wp == null) continue;
                        list.Add(KvProp.Row("#" + (i + 1) + " " + (wp.HasLimits ? "◈" : "·"),
                            wp.HasLimits ? wp.Limits.Describe() : "без ограничений"));
                    }
                    list.Add(KvProp.Row("Маршрут", hub2.Waypoints.HasRoute
                        ? hub2.Waypoints.RouteNote : "не построен"));
                }
                return;
            }
        }

        /// <summary>Выбор узла дерева: узлы этапов 7–12 открывают свою вкладку верстака.</summary>
        public static void OnNodeSelected(ProjectNode node)
        {
            KvStageHub3 hub = Instance;
            if (hub == null || node == null || node.Key == null) return;
            if (node.Key == "stage3:waypoints") hub.OpenTab("waypoints");
            else if (node.Key == "stage3:constraints") hub.OpenTab("constraints");
            else if (node.Key == "stage3:calibration") hub.OpenTab("calibration");
            else if (node.Key == "stage3:payload") hub.OpenTab("payload");
            else if (node.Key == "stage3:export") hub.OpenTab("export");
            else if (node.Key == "stage3:import") hub.OpenTab("import");
        }

        // ================================================================== ПЕРЕКЛЮЧАТЕЛИ

        public static bool HasFeature(string id)
        {
            switch (id)
            {
                case "plan.constrained":
                case "plan.filter":
                case "wp.constraints":
                case "import.scan":
                    return Instance != null;
                default:
                    return false;
            }
        }

        public static bool GetFeature(string id, out bool handled)
        {
            handled = true;
            KvStageHub3 hub = Instance;
            if (hub == null) { handled = false; return false; }

            switch (id)
            {
                case "plan.constrained": return hub.Constrained.Enabled;
                case "plan.filter": return hub.Constrained.DiscardViolating;
                case "wp.constraints": return hub.waypointConstraints;
                case "import.scan": return hub.scanModelsOnStart;
                default:
                    handled = false;
                    return false;
            }
        }

        public static bool SetFeature(string id, bool value)
        {
            KvStageHub3 hub = Instance;
            if (hub == null) return false;

            switch (id)
            {
                case "plan.constrained":
                    hub.Constrained.Enabled = value;
                    if (value && hub.Constrained.Profile.mode == KvOrientMode.None)
                    {
                        hub.Constrained.Profile.mode = KvOrientMode.ToolVertical;
                        hub.Constrained.Save();
                    }
                    return true;
                case "plan.filter":
                    hub.Constrained.DiscardViolating = value;
                    return true;
                case "wp.constraints":
                    hub.waypointConstraints = value;
                    if (KvStageHub.Current != null && KvStageHub.Current.Waypoints != null)
                    {
                        KvStageHub.Current.Waypoints.useWaypointLimits = value;
                        KvStageHub.Current.Waypoints.RebuildRoute();
                    }
                    return true;
                case "import.scan":
                    hub.scanModelsOnStart = value;
                    if (value) hub.Import.RefreshFiles();
                    return true;
                default:
                    return false;
            }
        }

        // ================================================================== КОМАНДЫ

        /// <summary>Регистрация команд этапов 7–12.</summary>
        public static void RegisterCommands(KazistovVvUIManager manager)
        {
            KvStageHub3 hub = Install(manager);
            if (hub == null) return;

            // --- ЭТАП 7: ограничения промежуточных точек
            KvCommands.Register(new KvCommand
            {
                Id = "wp.tab",
                Title = "Ограничения точки",
                Description = "Вкладка ограничений промежуточных точек: ориентация, скорость, обход, пауза",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Ограничения точки",
                Execute = delegate { hub.OpenTab("waypoints"); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "wp.orient",
                Title = "Ориентация waypoint",
                Description = "Задать требуемое направление оси инструмента в выбранной точке (как сейчас)",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Ограничения/Ориентация (как сейчас)",
                Execute = delegate { hub.WaypointOrientationFromCurrent(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "wp.speed",
                Title = "Скорость waypoint",
                Description = "Включить/выключить ограничение скорости TCP в выбранной точке",
                Icon = "waypoint",
                MenuPath = "Робот/Промежуточные точки/Ограничения/Скорость в точке",
                Execute = delegate { hub.WaypointSpeedLimit(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "wp.detour",
                Title = "Обход препятствия (waypoint)",
                Description = "Обязать маршрут обходить окрестность препятствия у выбранной точки",
                Icon = "obstacle",
                MenuPath = "Робот/Промежуточные точки/Ограничения/Обход препятствия",
                Execute = delegate { hub.WaypointDetour(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "wp.pause",
                Title = "Пауза waypoint",
                Description = "Остановиться и подождать в выбранной точке",
                Icon = "eta",
                MenuPath = "Робот/Промежуточные точки/Ограничения/Пауза в точке",
                Execute = delegate { hub.WaypointPause(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "wp.clear",
                Title = "Снять ограничения waypoint",
                Description = "Вернуть выбранной точке обычное поведение",
                Icon = "reset",
                MenuPath = "Робот/Промежуточные точки/Ограничения/Снять все ограничения",
                Execute = delegate { hub.WaypointClearLimits(); }
            });

            // --- ЭТАП 8: планирование с ограничениями
            KvCommands.Register(new KvCommand
            {
                Id = "constr.toggle",
                Title = "Ограничения планирования",
                Description = "Проверка ограничений: вертикальный инструмент, предел наклона, взгляд на объект",
                Hotkey = "F9",
                Icon = "singularity",
                MenuPath = "Робот/Ограничения планирования",
                Execute = delegate { hub.ToggleConstrained(); },
                IsChecked = delegate { return hub.Constrained != null && hub.Constrained.Enabled; }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "constr.tab",
                Title = "Вкладка ограничений",
                Description = "Тип ограничения, предел наклона, допуск, проверка и приведение траектории",
                Icon = "singularity",
                MenuPath = "Робот/Ограничения планирования (вкладка)",
                Execute = delegate { hub.OpenTab("constraints"); }
            });

            // --- ЭТАП 9: калибровка
            KvCommands.Register(new KvCommand
            {
                Id = "calib.tab",
                Title = "Калибровочный мастер",
                Description = "TCP по 4 точкам, база робота по 3 точкам, камера (заглушка), файл калибровки",
                Icon = "tcp",
                MenuPath = "Сервис/Калибровочный мастер",
                Execute = delegate { hub.OpenTab("calibration"); }
            });

            // --- ЭТАП 10: нагрузка
            KvCommands.Register(new KvCommand
            {
                Id = "payload.tab",
                Title = "Калькулятор нагрузки",
                Description = "Максимальная нагрузка в текущей позе и график от расстояния до базы",
                Icon = "metrics",
                MenuPath = "Сервис/Калькулятор нагрузки",
                Execute = delegate { hub.OpenTab("payload"); }
            });

            // --- ЭТАП 11: экспорт в языки роботов
            KvCommands.Register(new KvCommand
            {
                Id = "export.robot",
                Title = "Экспорт в язык робота",
                Description = "Код движения по выбранной траектории: KUKA KRL (.src), FANUC KAREL (.kl), ABB RAPID (.mod)",
                Icon = "video",
                MenuPath = "Файл/Экспорт в язык робота",
                Execute = delegate { hub.ExportSelected(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "export.lang",
                Title = "Язык экспорта (KRL / KAREL / RAPID)",
                Description = "Переключить язык робота для экспорта траекторий",
                Icon = "lang",
                MenuPath = "Файл/Экспорт демонстрации/Язык робота",
                Execute = delegate { hub.CycleExportLanguage(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "export.tab",
                Title = "Вкладка экспорта траектории",
                Description = "Выбор языка робота, папка экспорта, предпросмотр файла",
                Icon = "video",
                MenuPath = "Файл/Экспорт в язык робота (вкладка)",
                Execute = delegate { hub.OpenTab("export"); }
            });

            // --- ЭТАП 12: импорт моделей
            KvCommands.Register(new KvCommand
            {
                Id = "import.tab",
                Title = "Импорт модели робота",
                Description = "URDF (рабочая кинематика) и STEP (базовая поддержка) + проверка кинематики",
                Icon = "robot",
                MenuPath = "Файл/Импорт модели робота",
                Execute = delegate { hub.OpenTab("import"); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "import.scan",
                Title = "Обновить список моделей",
                Description = "Перечитать папки поиска моделей (URDF / STEP)",
                Icon = "robot",
                MenuPath = "Файл/Импорт модели робота/Обновить список",
                Execute = delegate { hub.Import.RefreshFiles(); }
            });

            Debug.Log("[Stages3] команды этапов 7–12 зарегистрированы · всего команд: " +
                      KvCommands.All.Count);
        }
    }
}
