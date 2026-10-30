using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ХАБ ЭТАПОВ 13–36 (эта сессия):
    ///  13 — редактор коллизионных прокси (ускорение планирования);
    ///  14 — стенд сравнения планировщиков (100+ задач, метрики, таблица, CSV);
    ///  15 — живая визуализация дерева RRT;
    ///  16 — несколько камер и «картинка в картинке»;
    ///  17 — векторы сил и моментов;
    ///  18 — тепловая карта времени достижимости (секунды);
    ///  19 — генератор PDF-отчёта;
    ///  20 — совместная работа (мультиплеер по UDP);
    ///  21 — веб-дашборд (HTTP + страница мониторинга);
    ///  22 — мобильный пульт-компаньон (UDP-команды);
    ///  23 — голосовые команды;
    ///  24 — отслеживание рук (жесты);
    ///  25 — отслеживание взгляда и фовеальное рендерирование;
    ///  26 — интерфейс скриптов (макросы) в песочнице;
    ///  27 — визуальный редактор дерева поведения;
    ///  28 — пресеты окружения (ангар, лаборатория, цех, чистое помещение);
    ///  29 — пресеты освещения (день, ночь, студия, драматичный);
    ///  30 — редактор материалов в реальном времени;
    ///  31 — кинематографический режим;
    ///  32 — титры, подписи и пояснения (+ редактор субтитров SRT);
    ///  33 — запись голоса диктора с синхронизацией к видео;
    ///  34 — имитация отказов и поведение безопасности;
    ///  35 — проверка перед пуском с подтверждением оператором;
    ///  36 — уровни журнала, фильтрация, поиск и цвета.
    ///
    /// Роль та же, что у <see cref="FeatureHub"/>, <see cref="KvStageHub"/>,
    /// <see cref="KvStageHub2"/> и <see cref="KvStageHub3"/>: единая точка создания сервисов,
    /// кадрового обслуживания, вкладок верстака, дерева моделей, свойств, переключателей и команд.
    /// Существующая логика НЕ переписывается — используются только публичные методы потока,
    /// планировщика, валидатора и оболочки.
    ///
    /// ГОРЯЧИЕ КЛАВИШИ: Alt+1…Alt+0, Alt+−, Alt+= — открытие вкладок новых этапов.
    /// Занимать Alt-сочетания безопасно: проверено, что ни один штатный бинд проекта их не
    /// использует (F1–F12 заняты ранее и не тронуты).
    /// </summary>
    [DefaultExecutionOrder(80)]
    public class KvStageHub4 : MonoBehaviour
    {
        public static KvStageHub4 Instance { get; private set; }

        // ------------------------------------------------------------------ сервисы
        public KvCollisionOptimizer Proxies { get; private set; }
        public KvPlannerLab Lab { get; private set; }
        public KvCameraService Cameras { get; private set; }
        public KvForceVisualizer Forces { get; private set; }
        public KvTimeHeatmap Heatmap { get; private set; }
        public KvReportGenerator Report { get; private set; }
        public KvCollaborationService Collab { get; private set; }
        public KvWebDashboard Web { get; private set; }
        public KvCompanionServer Companion { get; private set; }
        public KvVoiceService Voice { get; private set; }
        public KvHandTrackingService Hands { get; private set; }
        public KvEyeTrackingService Eyes { get; private set; }
        public KvFoveatedRendering Foveated { get; private set; }
        public KvScriptEngine Script { get; private set; }
        public KvBehaviorTree Behavior { get; private set; }
        public KvBtRunner BehaviorRunner { get; private set; }
        public KvEnvironmentStudio Environment { get; private set; }
        public KvLightingStudio Lighting { get; private set; }
        public KvMaterialStudio Materials { get; private set; }
        public KvCinematicService Cinema { get; private set; }
        public KvTitlesService Titles { get; private set; }
        public KvVoiceOverService VoiceOver { get; private set; }
        public KvFailureSimulator Failures { get; private set; }
        public KvPreRunValidator PreRun { get; private set; }
        /// <summary>ФИКС 9: два манекена рядом с рабочей зоной (учитываются в проверке перед пуском).</summary>
        public KvMannequins Mannequins { get; private set; }
        public KvLogTools LogTools { get; private set; }
        /// <summary>
        /// СЛУЖБА ГРАФИКИ (раздел «Графика», сессия 18.09.2026): автоопределение железа,
        /// пресеты, применение настроек HDRP. Живёт отдельно от хаба (собственный носитель
        /// кадрового обслуживания), хаб только запускает её при инициализации — как требует ТЗ.
        /// </summary>
        public KvGraphicsService Graphics { get; private set; }

        private KazistovVvUIManager ui;
        private TrajectoryFlowController flow;
        private FeatureHub features;
        private KvStageHub3 stage3;
        private bool bound;
        private RobotController boundRobot;
        private bool tabsRegistered;

        // ================================================================== создание

        public static KvStageHub4 Install(KazistovVvUIManager manager)
        {
            if (Instance != null) return Instance;
            if (manager == null) return null;
            KvStageHub4 hub = manager.gameObject.GetComponent<KvStageHub4>();
            if (hub == null) hub = manager.gameObject.AddComponent<KvStageHub4>();
            return hub;
        }

        public static KvStageHub4 Current { get { return Instance; } }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            // Строки этапов 13–36 (7 языков) — в словари интерфейса.
            KvLocExtra3.Install();
            // Строки раздела «Графика» (7 языков) + сам раздел: служба читает PlayerPrefs,
            // выполняет автоопределение при первом запуске и применяет настройки (ТЗ, этапы 1–5).
            KvLocExtra4.Install();
            // Строки XR-оверлея: панель у руки, окна (свернуть/закрыть), пояс (7 языков) —
            // решения владельца [D20]–[D22], сессия §32.
            KvLocExtra5.Install();
            // 30.10.2026: ключи, которые код запрашивал, а в словарях их не было (аудит нашёл 174).
            // Ставятся ПОСЛЕ остальных: внешние словари и прежние таблицы главнее — здесь только
            // то, чего не было нигде, иначе на всех семи языках показывался русский fallback из кода.
            KvLocExtra6.Install();
            Graphics = KvGraphicsService.EnsureStarted();

            Proxies = new KvCollisionOptimizer();
            Lab = new KvPlannerLab();
            Cameras = new KvCameraService();
            Forces = new KvForceVisualizer();
            Heatmap = new KvTimeHeatmap();
            Report = new KvReportGenerator();
            Collab = new KvCollaborationService();
            Web = new KvWebDashboard();
            Companion = new KvCompanionServer();
            Voice = new KvVoiceService();
            Hands = new KvHandTrackingService();

            // ПОДКЛЮЧИТЬ КИСТИ ИЗ OpenXR К СЕРВИСУ ЖЕСТОВ (30.10.2026, S44, пункт 5.23).
            //
            // ДО ЭТОГО позы суставов приходили в `Hands` ТОЛЬКО из диагностики (`DshStage4Diag`),
            // то есть в собранном приложении распознавание жестов работало вхолостую: логика готова,
            // данных нет. Владелец видел кисти, но жесты не срабатывали — и причину найти было
            // нельзя, потому что ни ошибки, ни предупреждения не было.
            //
            // Теперь `KvXrHands` каждый кадр передаёт точки сюда через этот мост.
            KazistovVvXr.KvXrHands.GestureSink = Hands;
            Eyes = new KvEyeTrackingService();

            // ПОДКЛЮЧИТЬ ВЗГЛЯД К СЕРВИСУ ОТСЛЕЖИВАНИЯ (30.10.2026, S44, пункт 5.23).
            //
            // Та же история, что с кистями: `KvEyeTrackingService` получал данные ТОЛЬКО из
            // диагностики, поэтому фиксации, выбор взглядом и фовеальное зрение в собранном
            // приложении молчали. Теперь компонент `KvXrGazeFeed` подаёт направление взгляда
            // каждый кадр (по позе головы — честное упрощение, см. пояснение в его шапке).
            KazistovVvXr.KvXrGazeFeed.Sink = Eyes;
            Foveated = new KvFoveatedRendering();
            Script = new KvScriptEngine();
            Behavior = new KvBehaviorTree();
            BehaviorRunner = new KvBtRunner(Script);
            Environment = new KvEnvironmentStudio();
            Lighting = new KvLightingStudio();
            Materials = new KvMaterialStudio();
            Cinema = new KvCinematicService();
            Titles = new KvTitlesService();
            VoiceOver = new KvVoiceOverService();
            Failures = new KvFailureSimulator();
            PreRun = new KvPreRunValidator();
            Mannequins = new KvMannequins();
            LogTools = new KvLogTools();

            Proxies.Message += OnServiceMessage;
            Lab.Message += OnServiceMessage;
            Cameras.Message += OnServiceMessage;
            Forces.Message += OnServiceMessage;
            Heatmap.Message += OnServiceMessage;
            Report.Message += OnServiceMessage;
            Collab.Message += OnServiceMessage;
            Web.Message += OnServiceMessage;
            Companion.Message += OnServiceMessage;
            Voice.Message += OnServiceMessage;
            Hands.Message += OnServiceMessage;
            Eyes.Message += OnServiceMessage;
            Script.Message += OnServiceMessage;
            BehaviorRunner.Message += OnServiceMessage;
            Environment.Message += OnServiceMessage;
            Lighting.Message += OnServiceMessage;
            Materials.Message += OnServiceMessage;
            Cinema.Message += OnServiceMessage;
            Titles.Message += OnServiceMessage;
            VoiceOver.Message += OnServiceMessage;
            Failures.Message += OnServiceMessage;
            PreRun.Message += OnServiceMessage;
            Mannequins.Message += OnServiceMessage;
            LogTools.Message += OnServiceMessage;
            BehaviorRunner.SetTree(Behavior);

            Debug.Log("[Stages4] хаб этапов 13–36 поднят: коллизионные прокси, стенд планировщиков, " +
                      "камеры, силы и тепло, PDF-отчёт, сеть, XR-ввод, макросы и дерево поведения, " +
                      "окружение и свет, кинорежим, отказы и проверка перед пуском, уровни журнала · " +
                      "строк локализации: " + KvLocExtra3.RegisteredCount);
            if (Graphics != null)
                Debug.Log("[Graphics] раздел «Графика» поднят: режим «" +
                          KvGraphicsModel.Label(Graphics.Preset) + "» → " +
                          KvGraphicsModel.Label(Graphics.EffectivePreset()) +
                          " · графика " + (Graphics.Available ? "доступна" : "НЕдоступна (пакетный режим)") +
                          " · строк локализации: " + KvLocExtra4.RegisteredCount);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Отписка от сообщений сервисов (43 подписки без отписки → утечка при
            // повторном входе в PlayMode: обработчик держит ссылку на уничтоженный хаб).
            if (Proxies != null) Proxies.Message -= OnServiceMessage;
            if (Lab != null) Lab.Message -= OnServiceMessage;
            if (Cameras != null) Cameras.Message -= OnServiceMessage;
            if (Forces != null) Forces.Message -= OnServiceMessage;
            if (Heatmap != null) Heatmap.Message -= OnServiceMessage;
            if (Report != null) Report.Message -= OnServiceMessage;
            if (Collab != null) Collab.Message -= OnServiceMessage;
            if (Web != null) Web.Message -= OnServiceMessage;
            if (Companion != null) Companion.Message -= OnServiceMessage;
            if (Voice != null) Voice.Message -= OnServiceMessage;
            if (Hands != null) Hands.Message -= OnServiceMessage;
            if (Eyes != null) Eyes.Message -= OnServiceMessage;
            if (Script != null) Script.Message -= OnServiceMessage;
            if (BehaviorRunner != null) BehaviorRunner.Message -= OnServiceMessage;
            if (Environment != null) Environment.Message -= OnServiceMessage;
            if (Lighting != null) Lighting.Message -= OnServiceMessage;
            if (Materials != null) Materials.Message -= OnServiceMessage;
            if (Cinema != null) Cinema.Message -= OnServiceMessage;
            if (Titles != null) Titles.Message -= OnServiceMessage;
            if (VoiceOver != null) VoiceOver.Message -= OnServiceMessage;
            if (Failures != null) Failures.Message -= OnServiceMessage;
            if (PreRun != null) PreRun.Message -= OnServiceMessage;
            if (Mannequins != null) Mannequins.Message -= OnServiceMessage;
            if (LogTools != null) LogTools.Message -= OnServiceMessage;
        }

        /// <summary>
        /// ФИКС 11.Б: автосохранение правок материалов при выходе из PlayMode. Пишем только если
        /// правки действительно менялись и есть что писать, — тогда в редакторе не появляется
        /// лишних файлов, а сам редактор материалов работает как раньше (правки идут по копиям).
        /// </summary>
        private void OnApplicationQuit()
        {
            if (!Application.isPlaying) return;
            if (Materials == null || !Materials.Dirty) return;
            if (Materials.SavedCount == 0 && !Materials.HasSavedFile) return;
            Materials.SaveEdits(false);
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

            // Прокси столкновений пересобираются ПО ЗАПРОСУ (кнопкой вкладки или при смене
            // робота): покадровый пересчёт не нужен, поэтому у сервиса нет метода Tick.
            Lab.Tick(dt);
            Cameras.Tick(dt);
            Forces.Tick(dt);
            Heatmap.Tick(dt);
            Collab.Tick(dt);
            Web.Tick(dt);
            Companion.Tick(dt);
            Voice.Tick(dt);
            Hands.Tick(dt);
            Eyes.Tick(dt);
            Script.Tick(dt);
            BehaviorRunner.Tick(dt);
            Lighting.Tick(dt);
            Failures.Tick(dt);
            PreRun.Tick(dt);

            HandleHotkeys();
        }

        private void LateUpdate()
        {
            if (!bound) return;
            Cinema.Tick(Time.unscaledDeltaTime);
            Titles.Tick(Time.unscaledDeltaTime);
        }

        private void BindServices()
        {
            if (flow.Validator == null || !flow.Validator.Ready) return;
            features = FeatureHub.Current;
            stage3 = KvStageHub3.Current;

            Camera camera = Camera.main;
            KvWaypointManager route = KvStageHub.Current != null ? KvStageHub.Current.Waypoints : null;
            CollisionWorld world = features != null ? features.World : null;

            if (!bound)
            {
                bound = true;

                Proxies.Bind(flow, world, flow.Robot);
                Lab.Bind(flow, world, features);
                Cameras.Bind(flow, camera);
                Cameras.Build(transform);
                Forces.Bind(flow, features);
                Heatmap.Bind(flow, features);
                Report.Bind(flow, features, stage3);
                Collab.Bind(flow);
                Web.Bind(flow, features, stage3);
                Companion.Bind(flow, features, stage3);
                Voice.Bind(flow, features, ui);
                Hands.Bind(flow, features);
                Eyes.Bind(flow, features, route);
                Script.Bind(flow, features, route, stage3);
                Environment.Bind(flow);
                Materials.Refresh(flow.Robot != null ? flow.Robot.transform : null);
                // ФИКС 11.Б: возвращаем сохранённые правки материалов (файл materials_edits.json
                // в каталоге Config) — список объектов уже собран, значит цели правок найдены.
                Materials.LoadEdits();
                Cinema.Bind(flow, camera, transform);
                Titles.Bind(camera, transform);
                VoiceOver.Bind(transform);
                Failures.Bind(flow, features, stage3);
                PreRun.Bind(flow, features, stage3, Failures, People, transform);
                // ФИКС 9: два манекена рядом с рабочим местом робота — проверка перед пуском
                // видит не только оператора. Создаются кодом (префабов и правки сцены нет),
                // корневыми объектами сцены (см. KvMannequins.Ensure).
                Mannequins.Ensure(flow.Robot);
                Mannequins.ApplyLayout();                      // применить сохранённый вынос
                PreRun.PersonSourceNote = Mannequins.SourceNote();

                RegisterTabs();
                if (flow.Robot != null) boundRobot = flow.Robot;

                Debug.Log("[Stages4] сервисы этапов 13–36 привязаны (робот: " +
                          flow.Validator.RobotName + ", осей: " + flow.Validator.Dof +
                          ") · вкладок верстака: " + KvWorkbenchWindow.TabCount);
            }

            if (flow.Robot != null && flow.Robot != boundRobot)
            {
                boundRobot = flow.Robot;
                Proxies.ResetCache();
                Heatmap.RequestRebuild(true);
                Materials.Refresh(flow.Robot.transform);
                // ФИКС 9: манекены переставляются к рабочему месту нового робота.
                if (Mannequins != null) Mannequins.Ensure(boundRobot);
                if (PreRun != null && Mannequins != null) PreRun.PersonSourceNote = Mannequins.SourceNote();
                Debug.Log("[Stages4] сервисы перенастроены на робота «" + boundRobot.robotName +
                          "» (" + flow.Validator.Dof + " осей)");
            }
        }

        /// <summary>
        /// Люди в сцене: оператор у камеры/шлема И манекены (ФИКС 9). Манекены добавляются
        /// только если оператор не снял галочку «Учитывать манекены в проверке»
        /// (по умолчанию — учитывать).
        /// </summary>
        private Vector3[] People()
        {
            List<Vector3> points = new List<Vector3>();
            if (ui != null && ui.CameraRig != null)
                points.Add(ui.CameraRig.transform.position);
            if (Camera.main != null) points.Add(Camera.main.transform.position);
            if (Mannequins != null)
            {
                Vector3[] mannequins = Mannequins.Positions();
                if (mannequins.Length > 0) points.AddRange(mannequins);
            }
            return points.ToArray();
        }

        private void RegisterTabs()
        {
            if (tabsRegistered) return;
            if (KvWorkbenchWindow.Instance == null && KvWorkbenchWindow.TabCount == 0)
            {
                // Верстак ещё не создан — зарегистрируемся на следующем кадре.
                Invoke("RegisterTabs", 0.5f);
                return;
            }
            tabsRegistered = true;

            KvWorkbenchWindow.RegisterTab(new KvCollisionTab(Proxies));
            KvWorkbenchWindow.RegisterTab(new KvPlannerLabTab(Lab));
            KvWorkbenchWindow.RegisterTab(new KvCameraTab(Cameras));
            KvWorkbenchWindow.RegisterTab(new KvForceHeatTab(Forces, Heatmap));
            KvWorkbenchWindow.RegisterTab(new KvReportTab(Report));

            KvWorkbenchWindow.RegisterTab(new KvNetTab(Collab, Web, Companion));
            KvWorkbenchWindow.RegisterTab(new KvVoiceTab(Voice));
            KvWorkbenchWindow.RegisterTab(new KvHandsTab(Hands, Foveated));
            KvWorkbenchWindow.RegisterTab(new KvEyesTab(Eyes, Foveated));

            KvBehaviorTreeWindow window = KvBehaviorTreeWindow.Install(transform, Behavior, BehaviorRunner);
            KvWorkbenchWindow.RegisterTab(new KvScriptTab(Script));
            KvWorkbenchWindow.RegisterTab(new KvBehaviorTab(Behavior, BehaviorRunner, window));

            KvWorkbenchWindow.RegisterTab(new KvEnvironmentTab(Environment));
            KvWorkbenchWindow.RegisterTab(new KvLightingTab(Lighting));
            KvWorkbenchWindow.RegisterTab(new KvMaterialTab(Materials,
                delegate { return flow != null && flow.Robot != null ? flow.Robot.transform : null; },
                delegate { return Camera.main; }));

            KvWorkbenchWindow.RegisterTab(new KvCinemaTab(Cinema));
            KvWorkbenchWindow.RegisterTab(new KvTitlesTab(Titles, AimPoint));
            KvWorkbenchWindow.RegisterTab(new KvVoiceOverTab(VoiceOver));

            KvWorkbenchWindow.RegisterTab(new KvFailureTab(Failures));
            KvWorkbenchWindow.RegisterTab(new KvValidateTab(PreRun, StartSelected, Mannequins));
            KvWorkbenchWindow.RegisterTab(new KvLogToolsTab(LogTools, KvActionLog.Instance));

            Debug.Log("[Stages4] вкладки этапов 13–36 зарегистрированы · всего вкладок: " +
                      KvWorkbenchWindow.TabCount);
        }

        private Vector3 AimPoint()
        {
            FreeFlyCameraController rig = ui != null ? ui.CameraRig : null;
            if (rig != null && rig.AimPointPublic.sqrMagnitude > 1e-6f) return rig.AimPointPublic;
            if (flow != null && flow.State != null && flow.State.hasPoint) return flow.State.point;
            return Vector3.zero;
        }

        // ------------------------------------------------------------------ горячие клавиши (Alt + …)

        private static readonly KeyCode[] AltKeys =
        {
            KeyCode.Alpha1, KeyCode.Alpha2, KeyCode.Alpha3, KeyCode.Alpha4, KeyCode.Alpha5,
            KeyCode.Alpha6, KeyCode.Alpha7, KeyCode.Alpha8, KeyCode.Alpha9, KeyCode.Alpha0,
            KeyCode.Minus, KeyCode.Equals
        };

        private static readonly string[] AltTabs =
        {
            "collision", "planner", "cameras", "forces", "report",
            "net", "voice", "hands", "eyes", "script",
            "behavior", "cine"
        };

        private void HandleHotkeys()
        {
            bool alt;
            try
            {
                Keyboard k = Keyboard.current;
                alt = k != null ? k.altKey.isPressed : Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            }
            catch (Exception)
            {
                alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            }
            if (!alt) return;

            for (int i = 0; i < AltKeys.Length; i++)
            {
                if (!Down(AltKeys[i])) continue;
                OpenTab(AltTabs[i]);
                return;
            }
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
                case KeyCode.Alpha1: return keyboard.digit1Key;
                case KeyCode.Alpha2: return keyboard.digit2Key;
                case KeyCode.Alpha3: return keyboard.digit3Key;
                case KeyCode.Alpha4: return keyboard.digit4Key;
                case KeyCode.Alpha5: return keyboard.digit5Key;
                case KeyCode.Alpha6: return keyboard.digit6Key;
                case KeyCode.Alpha7: return keyboard.digit7Key;
                case KeyCode.Alpha8: return keyboard.digit8Key;
                case KeyCode.Alpha9: return keyboard.digit9Key;
                case KeyCode.Alpha0: return keyboard.digit0Key;
                case KeyCode.Minus: return keyboard.minusKey;
                case KeyCode.Equals: return keyboard.equalsKey;
                default: return null;
            }
        }

        // ================================================================== действия

        public void OpenTab(string tabKey)
        {
            if (KvWorkbenchWindow.Instance == null) return;
            KvWorkbenchWindow.Instance.Show(tabKey);
        }

        /// <summary>
        /// ПУСК выбранной траектории (используется проверкой перед пуском, кнопкой «ПУСК с проверкой»,
        /// голосом, мобильным пультом, макросами). Подтверждение делается тем же путём, что нажатие
        /// ЛКМ по фантому — через штатный `ConfirmSelectedTrajectory` потока, без дублирования логики.
        /// </summary>
        public bool StartSelected()
        {
            if (flow == null) return false;
            if (flow.State.phase != FlowState.PhantomsMoving)
            {
                Debug.LogWarning("[Stages4] ПУСК: нет подтверждаемой траектории (состояние " +
                                 flow.State.phase + ")");
                return false;
            }
            bool ok = flow.ConfirmSelectedTrajectory();
            if (ok) OnServiceMessage("пуск: робот пошёл по выбранной траектории");
            else OnServiceMessage("пуск отклонён (Safety или нет фантома)");
            return ok;
        }

        /// <summary>Пуск с проверкой перед пуском (этап 35 ТЗ). Возвращает false, если нужно подтверждение.</summary>
        public bool RequestRun()
        {
            return PreRun.RequestRun(delegate { StartSelected(); });
        }

        public void ToggleProxies() { Proxies.Enabled = !Proxies.Enabled; }

        public void ToggleTree() { Lab.ToggleTree(); }

        public void StartBenchmark() { Lab.Start(); }

        public void ToggleCinema() { Cinema.Toggle(); }

        public void ToggleWeb() { Web.Toggle(); }

        public void ToggleCompanion() { Companion.Toggle(); }

        public void CycleEnvironment()
        {
            Environment.SetVisible(true);
            Environment.Next();
            if (ui != null) ui.RebuildTree(true);
        }

        public void CycleLighting() { Lighting.SetPreset((Lighting.Index + 1) % Lighting.Presets.Count); }

        public void ToggleVoice() { Voice.SetEnabled(!Voice.Enabled); }

        public void ToggleHands() { Hands.SetEnabled(!Hands.Enabled); Hands.SetVisible(Hands.Enabled); }

        public void ToggleEyes() { Eyes.SetEnabled(!Eyes.Enabled); }

        public void ClearFault() { Failures.Clear(); }

        public void SimulateCommsLoss() { Failures.Start(KvFailureKind.CommsLoss); }

        public void SimulateJointLoss() { Failures.Start(KvFailureKind.JointLoss, 2); }

        public void ExportLogView() { LogTools.ExportVisible(); }

        public void MakeReport() { Report.Generate(); }

        // ================================================================== ДЕРЕВО МОДЕЛЕЙ

        /// <summary>Подпись состояния этапов 13–36 для пересборки дерева.</summary>
        public static string TreeSignaturePart()
        {
            KvStageHub4 hub = Instance;
            if (hub == null) return "нет";
            return (hub.Proxies != null && hub.Proxies.Enabled ? "1" : "0") + ":" +
                   (hub.Lab != null ? hub.Lab.Results.Count : 0) + ":" +
                   (hub.Cameras != null ? hub.Cameras.Windows.Count : 0) + ":" +
                   (hub.Heatmap != null ? hub.Heatmap.PointCount : 0) + ":" +
                   (hub.Report != null && !string.IsNullOrEmpty(hub.Report.LastFile) ? "r" : "-") + ":" +
                   (hub.Collab != null ? hub.Collab.Role.ToString() : "-") + ":" +
                   (hub.Web != null && hub.Web.Running ? "web" : "-") + ":" +
                   (hub.Voice != null && hub.Voice.Enabled ? "v" : "-") + ":" +
                   (hub.Hands != null && hub.Hands.Enabled ? "h" : "-") + ":" +
                   (hub.Eyes != null && hub.Eyes.Enabled ? "e" : "-") + ":" +
                   (hub.Script != null ? hub.Script.Macros.Count : 0) + ":" +
                   (hub.Behavior != null ? hub.Behavior.nodes.Count : 0) + ":" +
                   (hub.Environment != null ? hub.Environment.CurrentTitle : "-") + ":" +
                   (hub.Lighting != null ? hub.Lighting.CurrentTitle : "-") + ":" +
                   (hub.Cinema != null && hub.Cinema.Enabled ? "k" : "-") + ":" +
                   (hub.Titles != null ? hub.Titles.Cues.Count : 0) + ":" +
                   (hub.VoiceOver != null && hub.VoiceOver.HasRecording ? "vo" : "-") + ":" +
                   (hub.Failures != null ? hub.Failures.Kind.ToString() : "-") + ":" +
                   (hub.LogTools != null ? (int)hub.LogTools.MinLevel : 0);
        }

        /// <summary>Ветки дерева моделей этапов 13–36.</summary>
        public static void BuildTreeNodes(KazistovVvUIManager manager, List<ProjectNode> roots,
            RobotController activeRobot)
        {
            KvStageHub4 hub = Instance;
            if (hub == null || roots == null) return;

            ProjectNode group = new ProjectNode("group:stages4",
                "Этапы 13–36: производительность, показ и безопасность",
                ProjectNodeKind.Group, null, activeRobot);
            group.Key = "group:stages4";
            group.Tooltip = "Коллизионные прокси, стенд планировщиков, камеры, силы и тепло, отчёт, " +
                            "сеть и дашборд, XR-ввод, макросы и деревья поведения, окружение, свет, " +
                            "материалы, кинорежим, титры, голос диктора, отказы, проверка перед пуском, " +
                            "уровни журнала.";

            // --- производительность и планирование (13–15, 18)
            ProjectNode speed = Entry("stage4:speed", "Производительность планирования", activeRobot,
                hub.Proxies.Status());
            speed.Tooltip = "Этапы 13–15, 18: коллизионные прокси, стенд сравнения планировщиков, " +
                            "дерево RRT, карта времени достижимости.";
            group.Children.Add(speed);

            // --- показ и отчётность (16, 17, 19, 28–33)
            ProjectNode show = Entry("stage4:show", "Показ, отчёт и оформление", activeRobot,
                hub.Cameras.Status() + " · " + hub.Report.Status());
            show.Tooltip = "Этапы 16, 17, 19, 28–33: камеры и PiP, векторы сил, PDF-отчёт, окружение, " +
                           "освещение, материалы, кинорежим, титры, голос диктора.";
            group.Children.Add(show);

            // --- сеть и совместная работа (20–22)
            ProjectNode net = Entry("stage4:net", "Сеть и совместная работа", activeRobot,
                hub.Collab.Status() + " · " + (hub.Web.Running ? "дашборд включён" : "дашборд выключен") +
                " · " + (hub.Companion.Running ? "пульт включён" : "пульт выключен"));
            net.Tooltip = "Этапы 20–22: совместная работа по UDP, веб-дашборд, мобильный пульт.";
            group.Children.Add(net);

            // --- ввод (23–27)
            ProjectNode input = Entry("stage4:input", "Управление без клавиатуры и макросы", activeRobot,
                "голос " + (hub.Voice.Enabled ? "вкл" : "выкл") +
                ", руки " + (hub.Hands.Enabled ? "вкл" : "выкл") +
                ", взгляд " + (hub.Eyes.Enabled ? "вкл" : "выкл") +
                ", макросов " + hub.Script.Macros.Count +
                ", узлов дерева " + hub.Behavior.nodes.Count);
            input.Tooltip = "Этапы 23–27: голосовые команды, жесты рук, взгляд и фовеальное " +
                            "рендерирование, макросы, визуальный редактор дерева поведения.";
            group.Children.Add(input);

            // --- безопасность и журнал (34–36)
            ProjectNode safety = Entry("stage4:safety", "Безопасность и журнал", activeRobot,
                hub.Failures.Status() + " · " + hub.PreRun.LastSummary);
            safety.Tooltip = "Этапы 34–36: имитация отказов, проверка перед пуском с подтверждением, " +
                             "уровни журнала, фильтр и поиск.";
            group.Children.Add(safety);

            roots.Add(group);
        }

        private static ProjectNode Entry(string key, string title, RobotController robot, string details)
        {
            ProjectNode node = new ProjectNode(key, title, ProjectNodeKind.Object, null, robot);
            node.Key = key;
            node.Details = details;
            return node;
        }

        // ================================================================== СВОЙСТВА

        /// <summary>Строки свойств этапов 13–36 (робот: прокси и безопасность; варианты: время достижимости).</summary>
        public static void BuildExtraProperties(ProjectNode node, List<KvProp> list)
        {
            KvStageHub4 hub = Instance;
            if (hub == null || node == null || list == null) return;

            if (node.Kind == ProjectNodeKind.Robot)
            {
                list.Add(KvProp.Section("Производительность и безопасность (этапы 13–36)"));
                list.Add(KvProp.Row("Коллизионные прокси", hub.Proxies.Status()));
                list.Add(KvProp.Row("Стенд планировщиков", hub.Lab.Status()));
                list.Add(KvProp.Row("Проверка перед пуском", hub.PreRun.LastSummary));
                list.Add(KvProp.Row("Отказы", hub.Failures.Status()));
                list.Add(KvProp.Row("Уровень журнала", KvLogTools.LevelLabel(hub.LogTools.MinLevel)));
                return;
            }

            if (node.Kind == ProjectNodeKind.Trajectory)
            {
                TrajectoryCandidate candidate = node.Tag as TrajectoryCandidate;
                if (candidate == null) return;
                list.Add(KvProp.Section("Проверка и оценка (этапы 14, 18, 35)"));
                list.Add(KvProp.Row("Стратегия стенда", hub.Lab.LastStrategyLabel));
                list.Add(KvProp.Row("Время достижимости (карта)",
                    hub.Heatmap.TimeAt(candidate) >= 0f
                        ? hub.Heatmap.TimeAt(candidate).ToString("0.00") + " с"
                        : "не рассчитано"));
                list.Add(KvProp.Row("Замечания проверки", hub.PreRun.LastSummary));
                return;
            }
        }

        /// <summary>Выбор узла дерева: узлы этапов 13–36 открывают свою вкладку верстака.</summary>
        public static void OnNodeSelected(ProjectNode node)
        {
            KvStageHub4 hub = Instance;
            if (hub == null || node == null || node.Key == null) return;
            switch (node.Key)
            {
                case "stage4:speed": hub.OpenTab("planner"); break;
                case "stage4:show": hub.OpenTab("cameras"); break;
                case "stage4:net": hub.OpenTab("net"); break;
                case "stage4:input": hub.OpenTab("voice"); break;
                case "stage4:safety": hub.OpenTab("validate"); break;
            }
        }

        // ================================================================== ПЕРЕКЛЮЧАТЕЛИ

        public static bool HasFeature(string id)
        {
            switch (id)
            {
                case "collision.proxies":
                case "lab.tree":
                case "web.server":
                case "voice.input":
                case "hands.input":
                case "eyes.input":
                case "cinema.mode":
                case "env.show":
                case "safety.confirm":
                    return Instance != null;
                default:
                    return false;
            }
        }

        public static bool GetFeature(string id, out bool handled)
        {
            handled = true;
            KvStageHub4 hub = Instance;
            if (hub == null) { handled = false; return false; }
            switch (id)
            {
                case "collision.proxies": return hub.Proxies.Enabled;
                case "lab.tree": return hub.Lab.TreeVisible;
                case "web.server": return hub.Web.Running;
                case "voice.input": return hub.Voice.Enabled;
                case "hands.input": return hub.Hands.Enabled;
                case "eyes.input": return hub.Eyes.Enabled;
                case "cinema.mode": return hub.Cinema.Enabled;
                case "env.show": return hub.Environment.Visible;
                case "safety.confirm": return hub.PreRun.Findings.Count > 0;
                default:
                    handled = false;
                    return false;
            }
        }

        public static bool SetFeature(string id, bool value)
        {
            KvStageHub4 hub = Instance;
            if (hub == null) return false;
            switch (id)
            {
                case "collision.proxies": hub.Proxies.Enabled = value; return true;
                case "lab.tree":
                    if (hub.Lab.TreeVisible != value) hub.Lab.ToggleTree();
                    return true;
                case "web.server":
                    if (value) hub.Web.Start(); else hub.Web.Stop();
                    return true;
                case "voice.input": hub.Voice.SetEnabled(value); return true;
                case "hands.input": hub.Hands.SetEnabled(value); hub.Hands.SetVisible(value); return true;
                case "eyes.input": hub.Eyes.SetEnabled(value); return true;
                case "cinema.mode": hub.Cinema.SetEnabled(value); return true;
                case "env.show": hub.Environment.SetVisible(value); return true;
                case "safety.confirm":
                    if (value) hub.PreRun.Validate();
                    return true;
                default:
                    return false;
            }
        }

        // ================================================================== КОМАНДЫ

        /// <summary>Регистрация команд этапов 13–36.</summary>
        public static void RegisterCommands(KazistovVvUIManager manager)
        {
            KvStageHub4 hub = Install(manager);
            if (hub == null) return;

            // --- ЭТАП 13: коллизионные прокси
            Command("collision.tab", "Коллизионные прокси", "Автоматические упрощённые оболочки " +
                    "столкновений: ускоряют планирование", "obstacle",
                "Робот/Производительность/Коллизионные прокси", delegate { hub.OpenTab("collision"); });
            Command("collision.toggle", "Прокси вкл/выкл", "Включить или выключить упрощённые оболочки " +
                    "при планировании", "obstacle",
                "Робот/Производительность/Прокси (переключатель)",
                delegate { hub.ToggleProxies(); },
                delegate { return hub.Proxies.Enabled; });

            // --- ЭТАП 14: стенд сравнения планировщиков
            Command("lab.tab", "Стенд планировщиков", "Сравнение RRT*, BiRRT и оптимизации: 100+ задач, " +
                    "среднее время, успех, длина", "metrics",
                "Робот/Производительность/Стенд планировщиков", delegate { hub.OpenTab("planner"); });
            Command("lab.run", "Запустить стенд", "Прогнать набор задач и получить таблицу метрик", "metrics",
                "Робот/Производительность/Стенд планировщиков/Запустить",
                delegate { hub.StartBenchmark(); });

            // --- ЭТАП 15: дерево RRT
            Command("lab.tree", "Дерево RRT на экране", "Показывать построение дерева планировщика", "metrics",
                "Робот/Производительность/Дерево RRT",
                delegate { hub.ToggleTree(); },
                delegate { return hub.Lab.TreeVisible; });

            // --- ЭТАП 16: камеры
            Command("cameras.tab", "Камеры и PiP", "Вид сверху, сбоку и от первого лица в отдельных окнах",
                "video", "Вид/Камеры и картинка в картинке", delegate { hub.OpenTab("cameras"); });

            // --- ЭТАП 17: силы
            Command("forces.tab", "Силы и моменты", "Векторы моментов по суставам и сила на инструменте",
                "metrics", "Вид/Силы и моменты", delegate { hub.OpenTab("forces"); });

            // --- ЭТАП 18: карта времени
            Command("heat.time", "Карта времени достижимости", "Тепловая карта времени в секундах", "heatmap",
                "Вид/Тепловая карта времени", delegate { hub.OpenTab("forces"); });

            // --- ЭТАП 19: PDF-отчёт
            Command("report.pdf", "PDF-отчёт", "Отчёт с траекторией, метриками, снимками и кинематикой",
                "log", "Файл/Отчёт PDF", delegate { hub.OpenTab("report"); });
            Command("report.make", "Сделать отчёт", "Сформировать PDF-отчёт прямо сейчас", "log",
                "Файл/Отчёт PDF/Сформировать", delegate { hub.MakeReport(); });

            // --- ЭТАП 20: совместная работа
            Command("net.tab", "Сеть и мониторинг", "Совместная работа, веб-дашборд, мобильный пульт",
                "session", "Сервис/Сеть и мониторинг", delegate { hub.OpenTab("net"); });

            // --- ЭТАП 21: веб-дашборд
            Command("web.toggle", "Веб-дашборд", "Страница мониторинга состояния робота в браузере", "session",
                "Сервис/Веб-дашборд", delegate { hub.ToggleWeb(); },
                delegate { return hub.Web.Running; });

            // --- ЭТАП 22: мобильный пульт
            Command("mobile.toggle", "Мобильный пульт", "Приём команд с планшета по UDP", "session",
                "Сервис/Мобильный пульт", delegate { hub.ToggleCompanion(); },
                delegate { return hub.Companion.Running; });

            // --- ЭТАП 23: голос
            Command("voice.toggle", "Голосовые команды", "Слушать микрофон и выполнять команды", "record",
                "Сервис/Голосовые команды", delegate { hub.ToggleVoice(); },
                delegate { return hub.Voice.Enabled; });

            // --- ЭТАП 24: руки
            Command("hands.toggle", "Отслеживание рук", "Жесты: касание, свайп, горсть", "tcp",
                "Сервис/Отслеживание рук", delegate { hub.ToggleHands(); },
                delegate { return hub.Hands.Enabled; });

            // --- ЭТАП 25: взгляд
            Command("eyes.toggle", "Отслеживание взгляда", "Фиксации, выбор взглядом, фовеальное зрение",
                "eye", "Сервис/Отслеживание взгляда", delegate { hub.ToggleEyes(); },
                delegate { return hub.Eyes.Enabled; });

            // --- ЭТАП 26: макросы
            Command("script.tab", "Макросы (скрипты)", "Автоматизация действий робота в песочнице", "log",
                "Сервис/Макросы", delegate { hub.OpenTab("script"); });

            // --- ЭТАП 27: дерево поведения
            Command("bt.tab", "Дерево поведения", "Визуальный редактор дерева поведения", "tree",
                "Сервис/Дерево поведения", delegate { hub.OpenTab("behavior"); });

            // --- ЭТАП 28: окружение
            Command("env.next", "Сменить окружение", "Ангар, лаборатория, цех, чистое помещение", "robot",
                "Вид/Окружение/Следующий пресет", delegate { hub.CycleEnvironment(); });

            // --- ЭТАП 29: освещение
            Command("light.next", "Сменить освещение", "День, ночь, студия, драматичный свет", "theme-dark",
                "Вид/Освещение/Следующий пресет", delegate { hub.CycleLighting(); });

            // --- ЭТАП 30: материалы
            Command("mat.tab", "Материалы", "Правка цвета, металла и свечения прямо в работе", "theme-dark",
                "Вид/Материалы", delegate { hub.OpenTab("material"); });

            // --- ЭТАП 31: кинорежим
            Command("cine.toggle", "Кинематографический режим", "Широкий экран и плавное движение камеры",
                "video", "Вид/Кинорежим", delegate { hub.ToggleCinema(); },
                delegate { return hub.Cinema.Enabled; });

            // --- ЭТАП 32: титры
            Command("titles.tab", "Титры и подписи", "Титры, подзаголовки, пояснения и файл субтитров",
                "log", "Вид/Титры и подписи", delegate { hub.OpenTab("titles"); });

            // --- ЭТАП 33: голос диктора
            Command("vo.tab", "Голос диктора", "Запись голоса и синхронизация с видеозаписью", "record",
                "Файл/Голос диктора", delegate { hub.OpenTab("voiceover"); });

            // --- ЭТАП 34: отказы
            Command("fail.tab", "Имитация отказов", "Отказ сустава, потеря связи, перегрузка", "health",
                "Сервис/Имитация отказов", delegate { hub.OpenTab("failures"); });
            Command("fail.comms", "Потеря связи (имитация)", "Проверить поведение системы при потере связи",
                "health", "Сервис/Имитация отказов/Потеря связи", delegate { hub.SimulateCommsLoss(); });
            Command("fail.joint", "Отказ сустава (имитация)", "Проверить поведение при потере момента",
                "health", "Сервис/Имитация отказов/Отказ сустава", delegate { hub.SimulateJointLoss(); });
            Command("fail.clear", "Сброс аварии", "Снять аварийное состояние после имитации", "reset",
                "Сервис/Имитация отказов/Сброс аварии", delegate { hub.ClearFault(); });

            // --- ЭТАП 35: проверка перед пуском
            Command("valid.tab", "Проверка перед пуском", "Замечания по траектории и подтверждение оператора",
                "check", "Робот/Проверка перед пуском", delegate { hub.OpenTab("validate"); });
            Command("valid.run", "ПУСК с проверкой", "Проверить траекторию и запустить движение", "play",
                "Робот/ПУСК с проверкой", delegate { hub.RequestRun(); });
            // ФИКС 9: манекены учитываются в проверке перед пуском (по умолчанию — да).
            Command("valid.mannequins", "Учитывать манекены в проверке",
                "Два манекена рядом с рабочей зоной учитываются вместе с оператором",
                "check", "Робот/Проверка перед пуском/Учитывать манекены",
                delegate
                {
                    if (hub.Mannequins == null) return;
                    hub.Mannequins.Include = !hub.Mannequins.Include;
                    if (hub.PreRun != null) hub.PreRun.PersonSourceNote = hub.Mannequins.SourceNote();
                },
                delegate { return hub.Mannequins != null && hub.Mannequins.Include; });

            // --- ЭТАП 36: журнал
            Command("logtools.tab", "Журнал: уровни и поиск", "Фильтр по уровню, поиск, цвета, выгрузка",
                "log", "Вид/Журнал: уровни и поиск", delegate { hub.OpenTab("logtools"); });
            Command("logtools.export", "Выгрузить видимый журнал", "Сохранить отфильтрованные записи в файл",
                "log", "Вид/Журнал: уровни и поиск/Выгрузить", delegate { hub.ExportLogView(); });

            // --- РАЗДЕЛ «ГРАФИКА» (сессия 18.09.2026): подбор под железо и пресеты качества.
            //     Команды только открывают раздел и вызывают службу — новых горячих клавиш
            //     не добавляется, существующие бинды не затрагиваются.
            KvGraphicsService graphics = hub.Graphics != null ? hub.Graphics : KvGraphicsService.EnsureStarted();
            Command("graphics.tab", KvLocExtra4.T("cmd.graphics.tab", "Графика: настройки"),
                KvLocExtra4.T("cmd.graphics.tab.desc",
                    "Раздел «Графика» панели настроек: пресеты, качество, производительность, диагностика"),
                "layout", "Вид/Графика",
                delegate { OpenGraphicsTab(); });
            Command("graphics.detect", KvLocExtra4.T("cmd.graphics.detect", "Определить графику по железу"),
                KvLocExtra4.T("cmd.graphics.detect.desc",
                    "Повторно определить видеокарту, память и процессор и пересчитать пресет"),
                "features", "Вид/Графика/Определить по железу",
                delegate { if (graphics != null) graphics.Redetect(true); });
            Command("graphics.benchmark", KvLocExtra4.T("cmd.graphics.benchmark", "Бенчмарк графики"),
                KvLocExtra4.T("cmd.graphics.benchmark.desc",
                    "Замер кадровой частоты на 5 секунд: средний, минимальный FPS и просадки"),
                "metrics", "Вид/Графика/Бенчмарк 5 секунд",
                delegate { if (graphics != null) graphics.StartBenchmark(5f); });
            Command("graphics.metrics", KvLocExtra4.T("cmd.graphics.metrics", "Метрики графики"),
                KvLocExtra4.T("cmd.graphics.metrics.desc",
                    "Оверлей: FPS, время кадра, вызовы отрисовки, треугольники, видеопамять"),
                "heatmap", "Вид/Графика/Метрики",
                delegate { KvGraphicsUi.ToggleMetrics(); },
                delegate { return KvGraphicsUi.MetricsVisible; });
            Command("graphics.reset", KvLocExtra4.T("cmd.graphics.reset", "Сбросить графику"),
                KvLocExtra4.T("cmd.graphics.reset.desc",
                    "Вернуть режим «Авто» и настройки графики по умолчанию"),
                "reset", "Вид/Графика/Сбросить по умолчанию",
                delegate { if (graphics != null) graphics.ResetToAuto(); });

            // --- ФИКС 5: пересчёт кинематики SCARA — ТОЛЬКО по явному запросу оператора.
            //     Ход призмы считается один раз при инициализации; этот пункт нужен после
            //     ручной правки модели или сдвига нижней границы призмы (zLowerOffsetM).
            Command("scara.recalibrate", "Пересчитать кинематику SCARA",
                "Пересчитать пределы хода призмы и заново привязать поток (тяжёлая операция — " +
                "по явному запросу, автоматически не выполняется)",
                "reset", "Робот/Пересчитать кинематику SCARA",
                delegate { if (hub.flow != null) hub.flow.RebindKinematics("команда оператора"); });

            Debug.Log("[Stages4] команды этапов 13–36 зарегистрированы · всего команд: " +
                      KvCommands.All.Count);
        }

        /// <summary>
        /// Открыть раздел «Графика» в панели настроек. Номер вкладки берётся по имени,
        /// поэтому добавление вкладок в панель этот вызов не ломает.
        /// </summary>
        public static void OpenGraphicsTab()
        {
            KazistovVvUIManager ui = KazistovVvUIManager.Instance;
            if (ui == null) return;
            ui.ShowSettings(KvSettingsView.GraphicsTabIndex);
        }

        private static void Command(string id, string title, string description, string icon,
            string menuPath, Action execute, Func<bool> isChecked = null)
        {
            KvCommands.Register(new KvCommand
            {
                Id = id,
                Title = title,
                Description = description,
                Icon = icon,
                MenuPath = menuPath,
                Execute = execute,
                IsChecked = isChecked
            });
        }
    }
}
