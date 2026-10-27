using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ХАБ ЭТАПОВ 1–6 ЭТОЙ СЕССИИ (главное меню, туториал, демонстрация,
    /// сглаживание траекторий, время-оптимальная траектория, эко-профиль).
    ///
    /// Роль та же, что у <see cref="FeatureHub"/> и <see cref="KvStageHub"/>: единая точка
    /// создания сервисов, кадрового обслуживания, горячих клавиш, дерева моделей, свойств,
    /// переключателей функций и команд интерфейса. Существующая логика НЕ переписывается:
    /// хаб пользуется только публичными методами потока этапов, планировщика и оболочки.
    ///
    /// ГОРЯЧИЕ КЛАВИШИ (проверено по проекту — эти клавиши свободны: F5–F8, F10–F12 заняты
    /// другими модулями, F9 не используется):
    ///   F1 — главное меню (стартовый экран), F2 — верстак постобработки,
    ///   F3 — туториал, F4 — демонстрация.
    /// Существующие бинды (W A S D Q E Z X G F TAB Esc Enter Shift R P V H J ЛКМ/колесо/СКМ,
    /// Ctrl+Z / Ctrl+Y) не тронуты.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class KvStageHub2 : MonoBehaviour
    {
        public static KvStageHub2 Instance { get; private set; }

        // ------------------------------------------------------------------ сервисы
        public KvStartMenu StartMenu { get; private set; }
        public KvTutorial Tutorial { get; private set; }
        public KvQuickStart QuickStart { get; private set; }
        public KvPathSmoothing Smoothing { get; private set; }
        public KvTimeOptimal TimeOptimal { get; private set; }
        public KvEnergyOptimal Energy { get; private set; }
        public KvWorkbenchWindow Workbench { get; private set; }

        /// <summary>Общие ограничения движения (этапы 4–6: сглаживание, время, энергия).</summary>
        public KvMotionLimits Limits { get; private set; }

        // ------------------------------------------------------------------ параметры (инспектор)
        [Header("Этап 1: главное меню")]
        public bool showStartMenuOnLaunch = true;
        [Tooltip("Радиус кинематографического облёта, м")]
        public float menuOrbitRadius = 3.6f;

        [Header("Этап 2: туториал")]
        public bool tutorialOnLaunch = false;

        [Header("Диагностика")]
        public bool logEvents = true;

        // ------------------------------------------------------------------ состояние
        private KazistovVvUIManager ui;
        private TrajectoryFlowController flow;
        private FeatureHub features;
        private bool servicesBound;
        private RobotController boundRobot;
        private bool startMenuShown;
        private float idle;

        // ================================================================== создание

        /// <summary>Установить хаб (вызывается UI-менеджером при регистрации команд).</summary>
        public static KvStageHub2 Install(KazistovVvUIManager manager)
        {
            if (Instance != null) return Instance;
            if (manager == null) return null;
            KvStageHub2 hub = manager.gameObject.GetComponent<KvStageHub2>();
            if (hub == null) hub = manager.gameObject.AddComponent<KvStageHub2>();
            return hub;
        }

        public static KvStageHub2 Current { get { return Instance; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Строки новых модулей — в словари интерфейса (7 языков), см. KvLocExtra.
            KvLocExtra.Install();

            Limits = new KvMotionLimits();

            StartMenu = gameObject.AddComponent<KvStartMenu>();
            Tutorial = gameObject.AddComponent<KvTutorial>();
            QuickStart = gameObject.AddComponent<KvQuickStart>();

            Smoothing = new KvPathSmoothing();
            TimeOptimal = new KvTimeOptimal();
            Energy = new KvEnergyOptimal();

            Smoothing.Limits = Limits;
            Smoothing.Message += OnServiceMessage;
            TimeOptimal.Message += OnServiceMessage;
            Energy.Message += OnServiceMessage;
            StartMenu.Message += OnServiceMessage;
            Tutorial.Message += OnServiceMessage;
            QuickStart.Message += OnServiceMessage;

            // Кнопки стартового меню ведут в модули этапов 2 и 3.
            StartMenu.demoAction = PlayDemo;
            StartMenu.tutorialAction = StartTutorial;
            StartMenu.newProjectAction = ClearWorkspace;

            // Окно-верстак и вкладки постобработки.
            Workbench = KvWorkbenchWindow.Create(transform);
            KvWorkbenchWindow.RegisterTab(new KvSmoothTab(Smoothing));
            KvWorkbenchWindow.RegisterTab(new KvTimeOptimalTab(TimeOptimal));
            KvWorkbenchWindow.RegisterTab(new KvEnergyTab(Energy));

            KvLoc.Changed += OnLanguageChanged;

            Debug.Log("[Stages2] хаб этапов 1–6 поднят (меню, туториал, демонстрация, " +
                      "сглаживание, время-оптимальная, энергия) · строк локализации: " +
                      KvLocExtra.RegisteredCount);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Отписка от сообщений сервисов (утечка при повторном входе в PlayMode).
            if (Smoothing != null) Smoothing.Message -= OnServiceMessage;
            if (TimeOptimal != null) TimeOptimal.Message -= OnServiceMessage;
            if (Energy != null) Energy.Message -= OnServiceMessage;
            if (StartMenu != null) StartMenu.Message -= OnServiceMessage;
            if (Tutorial != null) Tutorial.Message -= OnServiceMessage;
            if (QuickStart != null) QuickStart.Message -= OnServiceMessage;
            KvLoc.Changed -= OnLanguageChanged;
        }

        private void OnServiceMessage(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            if (KvActionLog.Instance != null) KvActionLog.Instance.Add(KvLogKind.Ui, message);
        }

        private void OnLanguageChanged()
        {
            if (StartMenu != null) StartMenu.RefreshLanguage();
            if (Workbench != null)
            {
                Workbench.RebuildTabs();
                if (Workbench.Visible) Workbench.RebuildContent();
            }
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

            Smoothing.Tick(dt);
            Tutorial.Tick(dt);
            QuickStart.Tick(dt);
            HandleHotkeys();

            // Главное меню при запуске: показывается один раз, когда поток готов.
            if (!startMenuShown)
            {
                idle += dt;
                if (idle > 0.35f)
                {
                    startMenuShown = true;
                    if (showStartMenuOnLaunch) StartMenu.Show();
                }
            }
        }

        private void LateUpdate()
        {
            // Кинематографический облёт — после всей камерной логики кадра.
            if (StartMenu != null && StartMenu.Visible)
                StartMenu.Tick(Time.unscaledDeltaTime);
        }

        private void BindServices()
        {
            if (flow.Validator == null || !flow.Validator.Ready) return;

            if (!servicesBound)
            {
                servicesBound = true;
                features = FeatureHub.Current;

                StartMenu.Build(flow, ui != null ? ui.CameraRig : null);
                StartMenu.orbitRadius = Mathf.Max(1.5f, menuOrbitRadius);
                Tutorial.Build(flow);
                QuickStart.Bind(flow, Tutorial);

                Smoothing.Bind(flow, features != null ? features.World : null, Limits,
                    Energy != null ? Energy.Model : null);
                TimeOptimal.Bind(flow, features != null ? features.World : null,
                    Energy != null ? Energy.Model : null);
                Energy.Bind(flow, Limits);

                // ФИКС 2 (§20): планировщик параметризует время ТЕМ ЖЕ набором лимитов, что
                // и постобработка (сглаживание / время-оптимальная / эко-профиль). Раньше он
                // брал собственные `PoseValidator.VelMax` и не знал про ускорение и рывок —
                // отсюда и расхождение времён «планировщик против постобработки».
                if (flow.Planner != null) flow.Planner.MotionLimits = Limits;

                if (tutorialOnLaunch && !KvTutorial.Completed) StartTutorial();
                else if (KvTutorial.StartedNotFinished)
                    Debug.Log("[Stages2] туториал был начат и не закончен: шаг " +
                              (KvTutorial.SavedStep + 1) + " — откройте F3 или «Обучение» " +
                              "в главном меню, чтобы продолжить");

                Debug.Log("[Stages2] сервисы привязаны к потоку этапов (робот: " +
                          flow.Validator.RobotName + ") · F1 меню · F2 верстак · F3 обучение · F4 демо");
            }

            if (flow.Robot != null && flow.Robot != boundRobot)
            {
                boundRobot = flow.Robot;
                Smoothing.ResetCache();
                TimeOptimal.ResetCache();
                Energy.ResetCache();
                Debug.Log("[Stages2] постобработка перенастроена на робота «" +
                          boundRobot.robotName + "» (" + flow.Validator.Dof + " осей)");
            }
        }

        // ------------------------------------------------------------------ горячие клавиши

        private void HandleHotkeys()
        {
            if (Down(KeyCode.F1)) ToggleStartMenu();
            if (Down(KeyCode.F2)) ToggleWorkbench();
            if (Down(KeyCode.F3)) ToggleTutorial();
            if (Down(KeyCode.F4)) ToggleDemo();

            // Esc во время демонстрации — остановить показ (сброс потока делает камера).
            if (QuickStart != null && QuickStart.Running && Down(KeyCode.Escape))
                QuickStart.StopDemo("Esc");
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
                case KeyCode.F1: return keyboard.f1Key;
                case KeyCode.F2: return keyboard.f2Key;
                case KeyCode.F3: return keyboard.f3Key;
                case KeyCode.F4: return keyboard.f4Key;
                case KeyCode.Escape: return keyboard.escapeKey;
                default: return null;
            }
        }

        // ================================================================== действия

        public void ToggleStartMenu()
        {
            if (StartMenu == null) return;
            if (StartMenu.Visible) StartMenu.Hide();
            else StartMenu.Show();
        }

        public void ToggleWorkbench()
        {
            if (Workbench == null) return;
            Workbench.Toggle();
        }

        public void OpenWorkbench(string tabKey)
        {
            if (Workbench == null) return;
            Workbench.Show(tabKey);
        }

        /// <summary>Показать обучение (продолжить с сохранённого шага, если оно не закончено).</summary>
        public void StartTutorial()
        {
            if (Tutorial == null) return;
            if (Tutorial.Active) Tutorial.Skip("повторное нажатие");
            else Tutorial.StartTutorial(KvTutorial.StartedNotFinished);
        }

        public void ToggleTutorial()
        {
            if (Tutorial == null) return;
            if (Tutorial.Active) Tutorial.Skip("кнопка «Обучение»");
            else Tutorial.StartTutorial(KvTutorial.StartedNotFinished);
        }

        /// <summary>Показать демонстрацию (этап 3).</summary>
        public void PlayDemo()
        {
            if (QuickStart == null) return;
            if (QuickStart.Running) QuickStart.StopDemo("повторное нажатие");
            else QuickStart.Begin();
        }

        public void ToggleDemo()
        {
            PlayDemo();
        }

        /// <summary>Постобработка выбранного варианта: сглаживание (этап 4).</summary>
        public void SmoothSelected()
        {
            if (Smoothing == null) return;
            Smoothing.ApplySelected(false);
        }

        /// <summary>Переключиться на время-оптимальную траекторию (этап 5).</summary>
        public void ApplyTimeOptimal()
        {
            if (TimeOptimal == null) return;
            TimeOptimal.ApplySelected();
        }

        /// <summary>Переключиться на эко-профиль (этап 6).</summary>
        public void ApplyEcoProfile()
        {
            if (Energy == null) return;
            Energy.ApplySelected();
        }

        /// <summary>Дополнительная очистка рабочей области для «Нового проекта» (этап 1).</summary>
        public void ClearWorkspace()
        {
            try
            {
                if (features == null) features = FeatureHub.Current;
                if (features != null)
                {
                    if (features.Zones != null) features.Zones.Clear();
                    if (features.Comparison != null) features.Comparison.Reset();
                }
                if (KvStageHub.Current != null && KvStageHub.Current.Waypoints != null)
                    KvStageHub.Current.Waypoints.Clear("новый проект");
                if (Smoothing != null) Smoothing.ResetCache();
                if (TimeOptimal != null) TimeOptimal.ResetCache();
                if (Energy != null) Energy.ResetCache();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Stages2] очистка рабочей области: " + e.Message);
            }
        }

        // ================================================================== ДЕРЕВО МОДЕЛЕЙ

        /// <summary>Подпись состояния постобработки для пересборки дерева UI-менеджером.</summary>
        public static string TreeSignaturePart()
        {
            KvStageHub2 hub = Instance;
            if (hub == null) return "нет";
            return (hub.Smoothing != null ? hub.Smoothing.AppliedCount : 0) + ":" +
                   (hub.Smoothing != null ? hub.Smoothing.Level.ToString("0") : "0") + ":" +
                   (hub.TimeOptimal != null ? hub.TimeOptimal.DraftCount : 0) + ":" +
                   (hub.Energy != null ? hub.Energy.DraftCount : 0) + ":" +
                   (hub.Energy != null ? hub.Energy.PayloadKg.ToString("0.0") : "0") + ":" +
                   (hub.QuickStart != null && hub.QuickStart.Running ? "demo" : "-") + ":" +
                   (hub.Tutorial != null && hub.Tutorial.Active
                       ? "tut" + hub.Tutorial.StepIndex : "-") + ":" +
                   (hub.Limits != null ? hub.Limits.maxVelDeg.ToString("0") : "0");
        }

        /// <summary>Ветки дерева моделей этапов 1–6: группа «Постобработка траекторий».</summary>
        public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,
            RobotController activeRobot)
        {
            KvStageHub2 hub = Instance;
            if (hub == null || roots == null) return;

            ProjectNode group = new ProjectNode("group:post",
                KvLocExtra.T("wb.title", "Верстак: постобработка траекторий"),
                ProjectNodeKind.Group, null, activeRobot);
            group.Key = "group:post";
            group.Tooltip = KvLocExtra.T("smooth.info",
                "Постобработка после планирования: путь сглаживается в пространстве суставов, " +
                "затем время пересчитывается по лимитам скорости и ускорения.");
            group.Details = (hub.Smoothing != null ? hub.Smoothing.AppliedCount : 0) + " обр.";

            // --- сглаживание (этап 4)
            ProjectNode smooth = new ProjectNode("post:smooth",
                KvLocExtra.T("smooth.title", "Сглаживание траектории"),
                ProjectNodeKind.Object, null, activeRobot);
            smooth.Key = "post:smooth";
            smooth.Details = hub.Smoothing != null
                ? hub.Smoothing.Level.ToString("0") + " % · " + hub.Smoothing.MethodLabel +
                  (hub.Smoothing.Auto ? " · авто" : "")
                : "—";
            smooth.Tooltip = KvLocExtra.T("smooth.info", "Сглаживание траектории");
            group.Children.Add(smooth);

            // --- время-оптимальная (этап 5)
            ProjectNode topt = new ProjectNode("post:topt",
                KvLocExtra.T("topt.title", "Время-оптимальная траектория"),
                ProjectNodeKind.Object, null, activeRobot);
            topt.Key = "post:topt";
            topt.Details = hub.TimeOptimal != null
                ? hub.TimeOptimal.MaxVel.ToString("0") + " °/с · " + hub.TimeOptimal.MaxAcc.ToString("0") +
                  " °/с² · " + hub.TimeOptimal.MaxJerk.ToString("0") + " °/с³"
                : "—";
            topt.Tooltip = KvLocExtra.T("topt.info", "Время-оптимальная траектория");
            group.Children.Add(topt);

            // --- энергия (этап 6)
            ProjectNode energy = new ProjectNode("post:energy",
                KvLocExtra.T("energy.title", "Оптимизация по энергии"),
                ProjectNodeKind.Object, null, activeRobot);
            energy.Key = "post:energy";
            energy.Details = hub.Energy != null
                ? hub.Energy.PayloadKg.ToString("0.0") + " кг" +
                  (hub.Energy.Last != null
                      ? " · " + hub.Energy.Last.savings.ToString("0.0") + " % экономии"
                      : "")
                : "—";
            energy.Tooltip = KvLocExtra.T("energy.info", "Оптимизация по энергии");
            group.Children.Add(energy);

            roots.Add(group);
        }

        /// <summary>
        /// Строки свойств: постобработка выбранного варианта + состояние узлов группы
        /// «Постобработка траекторий».
        /// </summary>
        public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)
        {
            KvStageHub2 hub = Instance;
            if (hub == null || node == null || list == null) return;

            // --- узлы группы постобработки
            if (node.Key == "post:smooth" && hub.Smoothing != null)
            {
                list.Add(KvProp.Section(KvLocExtra.T("smooth.title", "Сглаживание траектории")));
                list.Add(KvProp.Row(KvLocExtra.T("smooth.level", "Уровень сглаживания, %"),
                    hub.Smoothing.Level.ToString("0") + " %"));
                list.Add(KvProp.Row(KvLocExtra.T("smooth.method", "Метод"), hub.Smoothing.MethodLabel));
                list.Add(KvProp.Row(KvLocExtra.T("smooth.auto",
                    "Применять автоматически после планирования"),
                    hub.Smoothing.Auto ? "да" : "нет"));
                list.Add(KvProp.Row("Обработано вариантов", hub.Smoothing.AppliedCount.ToString()));
                return;
            }

            if (node.Key == "post:topt" && hub.TimeOptimal != null)
            {
                list.Add(KvProp.Section(KvLocExtra.T("topt.title", "Время-оптимальная траектория")));
                list.Add(KvProp.Row(KvLocExtra.T("topt.vel", "Макс. скорость суставов, °/с"),
                    hub.TimeOptimal.MaxVel.ToString("0")));
                list.Add(KvProp.Row(KvLocExtra.T("topt.acc", "Макс. ускорение, °/с²"),
                    hub.TimeOptimal.MaxAcc.ToString("0")));
                list.Add(KvProp.Row(KvLocExtra.T("topt.jerk", "Макс. jerk, °/с³"),
                    hub.TimeOptimal.MaxJerk.ToString("0")));
                if (hub.TimeOptimal.Last != null)
                {
                    list.Add(KvProp.Row(KvLocExtra.T("smooth.time", "Время, с"),
                        hub.TimeOptimal.Last.stats.time.ToString("0.000")));
                    list.Add(KvProp.Row(KvLocExtra.T("topt.gain", "Выигрыш по времени"),
                        hub.TimeOptimal.Last.timeGain.ToString("+0.0;-0.0") + " %"));
                }
                return;
            }

            if (node.Key == "post:energy" && hub.Energy != null)
            {
                list.Add(KvProp.Section(KvLocExtra.T("energy.title", "Оптимизация по энергии")));
                list.Add(KvProp.Row(KvLocExtra.T("energy.payload", "Масса груза, кг"),
                    hub.Energy.PayloadKg.ToString("0.0")));
                if (hub.Energy.Last != null)
                {
                    list.Add(KvProp.Row(KvLocExtra.T("energy.value", "Энергия, Дж"),
                        hub.Energy.Last.energy.ToString("0.00")));
                    list.Add(KvProp.Row(KvLocExtra.T("energy.per.meter", "Удельная энергия, Дж/м"),
                        hub.Energy.Last.energyPerMeter.ToString("0.00")));
                    list.Add(KvProp.Row(KvLocExtra.T("energy.peak", "Пиковая мощность, Вт"),
                        hub.Energy.Last.peakPower.ToString("0.0")));
                    list.Add(KvProp.Row(KvLocExtra.T("energy.savings", "Экономия"),
                        hub.Energy.Last.savings.ToString("+0.0;-0.0") + " %"));
                }
                return;
            }

            // --- свойства КОНКРЕТНОГО варианта траектории (этапы 4–6)
            TrajectoryCandidate candidate = node.Tag as TrajectoryCandidate;
            if (candidate == null || candidate.plan == null) return;

            list.Add(KvProp.Section(KvLocExtra.T("wb.title", "Постобработка траектории")));

            if (hub.Smoothing != null)
            {
                KvSmoothEntry entry = hub.Smoothing.EntryOf(candidate);
                hub.Smoothing.EnsureBaseline(candidate, entry);
                list.Add(KvProp.Row(KvLocExtra.T("smooth.title", "Сглаживание"),
                    entry != null && entry.applied
                        ? entry.level.ToString("0") + " % · " + hub.Smoothing.MethodLabel
                        : KvLocExtra.T("common.off", "выключено")));
                if (entry != null)
                {
                    list.Add(KvProp.Row(KvLocExtra.T("smooth.jerk", "Jerk, °/с³") + " · " +
                                        KvLocExtra.T("smooth.before", "до") + " → " +
                                        KvLocExtra.T("smooth.after", "после"),
                        entry.Reference.maxJerk.ToString("0") + " → " +
                        (entry.applied ? entry.after.maxJerk.ToString("0") : "—") +
                        " (" + entry.JerkGain.ToString("+0.0;-0.0") + " %)"));
                    list.Add(KvProp.Row(KvLocExtra.T("smooth.curvature", "Кривизна, 1/м") + " · " +
                                        KvLocExtra.T("smooth.before", "до") + " → " +
                                        KvLocExtra.T("smooth.after", "после"),
                        entry.Reference.curvature.ToString("0.000") + " → " +
                        (entry.applied ? entry.after.curvature.ToString("0.000") : "—")));
                    list.Add(KvProp.Row(
                        KvLocExtra.T("smooth.planner", "План планировщика (как построен)"),
                        entry.before.Line()));
                }
            }

            if (hub.TimeOptimal != null)
            {
                KvTimeOptimal.Draft draft = hub.TimeOptimal.Compute(candidate, true);
                if (draft != null)
                {
                    list.Add(KvProp.Row(KvLocExtra.T("topt.metric", "Метрика «время-оптимальная»"),
                        draft.stats.time.ToString("0.000") + " с · " +
                        KvLocExtra.F("topt.faster", "быстрее на {0} %",
                            draft.timeGain.ToString("0.0"))));
                }
            }

            if (hub.Energy != null)
            {
                float peak;
                float energy = hub.Energy.CurrentEnergy(candidate, out peak);
                list.Add(KvProp.Row(KvLocExtra.T("energy.value", "Энергия, Дж"),
                    energy.ToString("0.00") + " · " +
                    KvLocExtra.T("energy.peak", "Пиковая мощность, Вт") + " " + peak.ToString("0.0")));
            }
        }

        /// <summary>Выбор узла дерева: узлы постобработки открывают свою вкладку верстака.</summary>
        public static void OnNodeSelected(ProjectNode node)
        {
            KvStageHub2 hub = Instance;
            if (hub == null || node == null || node.Key == null) return;
            if (node.Key == "post:smooth") hub.OpenWorkbench("smooth");
            else if (node.Key == "post:topt") hub.OpenWorkbench("topt");
            else if (node.Key == "post:energy") hub.OpenWorkbench("energy");
        }

        // ================================================================== ПЕРЕКЛЮЧАТЕЛИ ФУНКЦИЙ

        /// <summary>Какие id функций обслуживает этот хаб (для панели настроек).</summary>
        public static bool HasFeature(string id)
        {
            switch (id)
            {
                case "ui.startmenu":
                case "post.auto":
                case "tool.workbench":
                case "tool.tutorial":
                    return Instance != null;
                default:
                    return false;
            }
        }

        public static bool GetFeature(string id, out bool handled)
        {
            handled = true;
            KvStageHub2 hub = Instance;
            if (hub == null) { handled = false; return false; }

            switch (id)
            {
                case "ui.startmenu": return KvStartMenu.ShowOnStart;
                case "post.auto": return hub.Smoothing != null && hub.Smoothing.Auto;
                case "tool.workbench": return hub.Workbench != null && hub.Workbench.Visible;
                case "tool.tutorial": return hub.Tutorial != null && hub.Tutorial.Active;
                default:
                    handled = false;
                    return false;
            }
        }

        public static bool SetFeature(string id, bool value)
        {
            KvStageHub2 hub = Instance;
            if (hub == null) return false;

            switch (id)
            {
                case "ui.startmenu":
                    KvStartMenu.ShowOnStart = value;
                    return true;
                case "post.auto":
                    if (hub.Smoothing != null) hub.Smoothing.Auto = value;
                    return true;
                case "tool.workbench":
                    if (hub.Workbench != null)
                    {
                        if (value) hub.Workbench.Show(-1);
                        else hub.Workbench.Hide();
                    }
                    return true;
                case "tool.tutorial":
                    if (value) hub.StartTutorial();
                    else if (hub.Tutorial != null) hub.Tutorial.Skip("переключатель настроек");
                    return true;
                default:
                    return false;
            }
        }

        // ================================================================== КОМАНДЫ

        /// <summary>Регистрация команд этапов 1–6 (кнопки тулбара и пункты меню).</summary>
        public static void RegisterCommands(KazistovVvUIManager manager)
        {
            KvStageHub2 hub = Install(manager);
            if (hub == null) return;

            // --- ЭТАП 1: главное меню
            KvCommands.Register(new KvCommand
            {
                Id = "startmenu.show",
                Title = "Главное меню",
                Description = "Экран запуска: новый проект, открытие сессии, демонстрация, настройки, выход",
                Hotkey = "F1",
                Icon = "presentation",
                MenuPath = "Файл/Главное меню",
                Execute = delegate { hub.ToggleStartMenu(); },
                IsChecked = delegate { return hub.StartMenu != null && hub.StartMenu.Visible; }
            });

            // --- ЭТАП 3: демонстрация
            KvCommands.Register(new KvCommand
            {
                Id = "demo.quick",
                Title = "Показать демо",
                Description = "Автоматическая демонстрация: точка → 8 траекторий → фантомы → движение робота",
                Hotkey = "F4",
                Icon = "scenario",
                MenuPath = "Сервис/Показать демо",
                Execute = delegate { hub.PlayDemo(); },
                IsChecked = delegate { return hub.QuickStart != null && hub.QuickStart.Running; },
                CheckedTint = delegate { return (Color?)new Color(0.4f, 0.9f, 0.4f); }
            });

            // --- ЭТАП 2: обучение
            KvCommands.Register(new KvCommand
            {
                Id = "tut.toggle",
                Title = "Обучение",
                Description = "Пошаговое обучение с подсветкой элементов; прогресс сохраняется",
                Hotkey = "F3",
                Icon = "help",
                MenuPath = "Справка/Обучение",
                Execute = delegate { hub.ToggleTutorial(); },
                IsChecked = delegate { return hub.Tutorial != null && hub.Tutorial.Active; }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "tut.restart",
                Title = "Обучение заново",
                Description = "Сбросить прогресс обучения и начать с первого шага",
                Icon = "reset",
                MenuPath = "Справка/Обучение заново",
                Execute = delegate { if (hub.Tutorial != null) hub.Tutorial.RestartTutorial(); }
            });

            // --- ЭТАПЫ 4–6: верстак постобработки
            KvCommands.Register(new KvCommand
            {
                Id = "workbench.toggle",
                Title = "Верстак постобработки",
                Description = "Вкладки: сглаживание, время-оптимальная траектория, энергия",
                Hotkey = "F2",
                Icon = "features",
                MenuPath = "Вид/Верстак постобработки",
                Execute = delegate { hub.ToggleWorkbench(); },
                IsChecked = delegate { return hub.Workbench != null && hub.Workbench.Visible; }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.smooth",
                Title = "Сглаживание траектории",
                Description = "Открыть вкладку сглаживания: уровень 0–100 %, метод, метрики «до / после»",
                Icon = "curve",
                MenuPath = "Робот/Постобработка/Сглаживание траектории",
                Execute = delegate { hub.OpenWorkbench("smooth"); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.timeoptimal",
                Title = "Время-оптимальная",
                Description = "Самая быстрая траектория при заданных лимитах скорости, ускорения и jerk",
                Icon = "eta",
                MenuPath = "Робот/Постобработка/Время-оптимальная",
                Execute = delegate { hub.OpenWorkbench("topt"); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.energy",
                Title = "Эко-профиль",
                Description = "Профиль движения с минимальным энергопотреблением (метрика «энергоэффективность»)",
                Icon = "health",
                MenuPath = "Робот/Постобработка/Эко-профиль",
                Execute = delegate { hub.OpenWorkbench("energy"); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.apply.smooth",
                Title = "Сгладить выбранную траекторию",
                Description = "Применить выбранный уровень сглаживания к текущему варианту",
                Icon = "curve",
                MenuPath = "Робот/Постобработка/Сгладить выбранную",
                Execute = delegate { hub.SmoothSelected(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.apply.topt",
                Title = "Переключиться на время-оптимальную",
                Description = "Заменить время выбранного варианта на время-оптимальное при текущих лимитах",
                Icon = "eta",
                MenuPath = "Робот/Постобработка/Переключиться на время-оптимальную",
                Execute = delegate { hub.ApplyTimeOptimal(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.apply.energy",
                Title = "Переключиться на эко-профиль",
                Description = "Заменить профиль времени выбранного варианта на энергоэффективный",
                Icon = "health",
                MenuPath = "Робот/Постобработка/Переключиться на эко-профиль",
                Execute = delegate { hub.ApplyEcoProfile(); }
            });
            KvCommands.Register(new KvCommand
            {
                Id = "post.auto",
                Title = "Автосглаживание после планирования",
                Description = "Вкл: каждая новая точка сразу даёт сглаженные варианты траектории",
                Icon = "curve",
                MenuPath = "Робот/Постобработка/Автосглаживание",
                Execute = delegate
                {
                    if (hub.Smoothing == null) return;
                    hub.Smoothing.Auto = !hub.Smoothing.Auto;
                    Debug.Log("[Stages2] автосглаживание " +
                              (hub.Smoothing.Auto ? "включено" : "выключено"));
                },
                IsChecked = delegate { return hub.Smoothing != null && hub.Smoothing.Auto; }
            });

            // ФИКС 7. Стоимость автосглаживания: обрабатывать ОДИН выбранный вариант, а не все 8.
            // По умолчанию выключено — прежнее поведение (все 8) сохраняется; выбор хранится
            // в PlayerPrefs и виден галочкой в меню и тумблером на вкладке «Сглаживание».
            KvCommands.Register(new KvCommand
            {
                Id = "post.auto.selected",
                Title = "Сглаживать только выбранный вариант",
                Description = "Вкл: автосглаживание обрабатывает один выбранный вариант вместо всех 8 " +
                              "(появление траекторий быстрее). Выкл: прежнее поведение — все варианты",
                Icon = "curve",
                MenuPath = "Робот/Постобработка/Сглаживать только выбранный",
                Execute = delegate
                {
                    if (hub.Smoothing == null) return;
                    hub.Smoothing.AutoSelectedOnly = !hub.Smoothing.AutoSelectedOnly;
                    Debug.Log("[Stages2] автосглаживание " +
                              (hub.Smoothing.AutoSelectedOnly
                                  ? "только выбранного варианта (быстрее)"
                                  : "всех вариантов (как было)"));
                },
                IsChecked = delegate
                {
                    return hub.Smoothing != null && hub.Smoothing.AutoSelectedOnly;
                }
            });

            Debug.Log("[Stages2] команды этапов 1–6 зарегистрированы · всего команд: " +
                      KvCommands.All.Count);
        }
    }
}
