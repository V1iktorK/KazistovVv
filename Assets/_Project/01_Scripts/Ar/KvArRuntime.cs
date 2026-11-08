using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.UI;
using UnityEngine.Android;
using KazistovVvXr;
using KazistovVvUI;

namespace KazistovVvAr
{
    /// <summary>
    /// ГЛАВНЫЙ КОМПОНЕНТ AR-СЦЕНЫ: ВКЛЮЧАЕТ НУЖНОЕ, ГАСИТ ЛИШНЕЕ И ПИШЕТ ФАКТЫ В ЖУРНАЛ.
    ///
    /// ЗАЧЕМ. AR-сцена собрана из копии VR-сцены (`KvArSceneBuilder`): в ней живут прежние
    /// компоненты шлема, которые в AR не только не нужны, но и вредны — они пишут позу камеры,
    /// телепортируют оператора и ставят ангар 40×50 м поверх реальной комнаты. Отключать их
    /// «руками в сцене» недостаточно: сцену пересобирает инструмент, и любая правка может
    /// потеряться. Поэтому решение принимается В РАНТАЙМЕ, с записью в журнал: что выключено
    /// и почему.
    ///
    /// ПОРЯДОК ЗАПУСКА (`[DefaultExecutionOrder(-900)]`) ВАЖЕН: режим должен быть выставлен в AR
    /// ДО того, как `KvXrRig.Start` начнёт своё определение режима (иначе тот выставит VR —
    /// на телефоне OpenXR не поднимается, и проба честно вернула бы «возможностей нет»).
    ///
    /// ЧТО ДЕЛАЕТ:
    ///   1) выставляет режим `KvXrPlatformModeSelector` = AR (одна строка `РЕЖИМ: …` в журнале);
    ///   2) отключает VR– только: `KvXrHeadTracking`, `KvXrLocomotion`, `KvXrMrRoom`,
    ///      `KvXrHangarAnchor`, `KvXrSceneContent` (сцены 14,4 м и сдвиг стендов);
    ///   3) гасит виртуальный ангар (`XrHangar`) — фон даёт камера;
    ///   4) через `KvXrRig` поднимает интерфейс шлема (меню 110 команд) как есть — это и есть
    ///      «весь функционал»;
    ///   5) настраивает хуки фабрики объектов: столы/роботы из меню встают на найденную
    ///      плоскость, а не «в 1,2 м перед лицом на y = 0»;
    ///   6) печатает строку `СОСТОЯНИЕ` (сессия, плоскости, якорь, кадры) — по ней снимают
    ///      доказательства на устройстве.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [DisallowMultipleComponent]
    public class KvArRuntime : MonoBehaviour
    {
        /// <summary>Целевая частота кадров в AR (приёмка Е1: не ниже 30).</summary>
        public int targetFrameRate = 30;

        /// <summary>Интервал строки состояния в журнале, секунды (0 — не писать).</summary>
        public float stateLogIntervalSeconds = 5f;

        /// <summary>Писать в журнал о запуске и состоянии.</summary>
        public bool logOnStart = true;

        /// <summary>Гасить виртуальный ангар (фон должен давать камеру).</summary>
        public bool hideVirtualHangar = true;

        /// <summary>Отключать VR-компоненты (трекинг головы, телепорт, MR-комнату, ангар, стенды).</summary>
        public bool disableVrOnlyComponents = true;

        /// <summary>Подключить хуки фабрики объектов (ставим мир из меню на найденную плоскость).</summary>
        public bool installObjectFactoryHooks = true;

        /// <summary>Сцена AR-режима запущена и режим выставлен.</summary>
        public bool Ready { get; private set; }

        /// <summary>Режим, который выставлен этой сценой.</summary>
        public KvXrPlatformMode Mode { get; private set; }

        /// <summary>Сколько VR-компонентов отключено (число для отчёта).</summary>
        public int DisabledComponents { get; private set; }

        /// <summary>Ангар выключен (для проверок).</summary>
        public bool HangarHidden { get; private set; }

        /// <summary>Ссылки сцены (заполняются инструментом сборки, проверяются тестом сцены).</summary>
        public ARSession session;
        public ARCameraManager cameraManager;
        public ARPlaneManager planeManager;
        public ARAnchorManager anchorManager;
        public ARRaycastManager raycastManager;
        public Camera arCamera;
        public Camera logicalHead;
        public KvArRigBridge rigBridge;
        public KvArSessionController sessionController;
        public KvArWorld world;
        public KvArContent content;
        public KvArPlacement placement;
        public KvArTouchInput touch;
        public KvArUi ui;

        /// <summary>Сетка найденных плоскостей и выбор самого большого стола (06.11.2026).</summary>
        public KvArPlaneVisualizer planeVisualizer;

        /// <summary>Сторож камеры (окно «ДОСТУП К КАМЕРЕ»).</summary>
        private KvArCameraGate cameraGate;

        /// <summary>Сколько секунд трекинга без единой плоскости считаем сбоем обнаружения.</summary>
        public float planeWatchdogSeconds = 15f;

        /// <summary>Сколько раз за сессию можно перезапускать подсистему AR из-за пустых плоскостей.</summary>
        public int maxPlaneRestarts = 2;

        private float nextStateLog;
        private int lastPlanes = -1;
        private bool pcUiLogged;

        private float planeEmptySince;
        private bool planeWatchdogLogged;
        private int planeRestarts;
        private float nextAspectCheck;
        private bool aspectLogged;
        private int aspectRestarts;
        private bool lastPlaced;
        private float fpsSmoothed;

        void Awake()
        {
            // 1. РЕЖИМ — ДО всего остального. На телефоне OpenXR не поднимается, поэтому
            // возможностей «сквозной вид» нет, а камера и пространственные якоря есть: это AR.
            KvXrPlatformModeSelector.Apply(
                passthroughAvailable: false,
                spatialAnchorsAvailable: true,
                cameraAvailable: true);
            Mode = KvXrPlatformModeSelector.Current;
            Debug.Log("[KvAr] РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ — камера устройства, плоскости и " +
                      "якоря AR Foundation, управление касаниями (сцена ArMain)");
            Debug.Log("[KvXr] " + KvXrPlatformModeSelector.Describe(KvXrPlatformMode.Ar,
                false, true, true, KvXrPlatformModeSelector.WorldScale));

            // ОРИЕНТАЦИЯ: ПК-вид планшета — только горизонтально. Без этого при повороте
            // устройства Android мог оставить портретную поверхность и экран превращался в
            // узкую полосу с синими полями (проверено на планшете 06.11.2026).
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Debug.Log("[KvAr] ЭКРАН: ориентация закреплена горизонтально (ПК-вид планшета)");

            // ЭКРАН НЕ ДОЛЖЕН ГАСНУТЬ: у планшета сон экрана останавливает ARCore-трекинг,
            // и владелец видел «не могу двигать экраном» — камера замирала вместе с экраном.
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            // ГИРОСКОП: Android не требует разрешения на датчики, но без явного включения
            // `Input.gyro` не обновляется. AR-трекинг даёт позу устройства сам; гироскоп
            // включаем как резерв и пишем факт в журнал.
            Input.gyro.enabled = true;
            Debug.Log("[KvAr] ДАТЧИКИ: гироскоп " + (Input.gyro.enabled ? "включён" : "НЕ включён") +
                      " · экран не гаснет (SleepTimeout.NeverSleep)");

            // МЕНЮ НЕ ДОЛЖНО ОТКРЫВАТЬСЯ САМО: в AR оно закрывало экран, и камера/обзор были
            // недоступны. Открывается кнопкой «Меню».
            KvXrStartMenu.SuppressAutoShow = true;

            // ДЕСКТОПНЫЙ ОВЕРЛЕЙ ОСТАЁТСЯ ВЫКЛЮЧЕННЫМ (сторож работает): его окна рисовали
            // жёлтую заливку поверх камеры. ПК-вид на планшете даёт СОБСТВЕННЫЙ слой `KvArUi`
            // (меню сверху, дерево слева, статус внизу) — камера при этом видна.

            EnsureCameraPermission();

            ResolveReferences();
            EnsurePlaneVisualizer();
            ApplyPlaneDetection();     // горизонтальные И вертикальные плоскости поиском

            // СТОРОЖ СЕССИИ: в сцене ArMain сохранены старые 8 с и 3 сброса (поле сериализовано).
            // Ставим новые значения в рантайме: ARCore на однотонной поверхности инициализируется
            // дольше 8 с, а частые Reset только мешают (проверено на планшете 05.11.2026).
            if (sessionController != null)
            {
                sessionController.stuckResetSeconds = 20f;
                sessionController.maxAutomaticResets = 1;
            }

            // КОМПАКТНЫЕ AR-КНОПКИ: в сцене ArMain сохранены старые 300×96 px (поле сериализовано),
            // поэтому размеры задаём в рантайме — иначе «компактный ряд» снова занимает пол-экрана.
            if (ui != null)
            {
                ui.buttonWidth = 240f;
                ui.buttonHeight = 64f;
            }

            // СТРАЖ КАМЕРЫ: явный запрос разрешения + доказательство, что камера реально
            // отдаёт кадры, и резервный живой предпросмотр, если ARCore не смог (06.11.2026).
            if (GetComponent<KvArCameraGate>() == null)
                gameObject.AddComponent<KvArCameraGate>();
            cameraGate = GetComponent<KvArCameraGate>();

            // УБРАТЬ ПОСТОРОННИЕ КАНВАСЫ: жёлтая заливка поверх камеры может быть
            // world-space канвасом проекта, который сторож десктопного интерфейса не гасит.
            HideForeignCanvases();
            if (disableVrOnlyComponents) DisableVrOnlyComponents();
            if (hideVirtualHangar) HideVirtualHangar();
            EnsureSingletonAudioListener();

            Ready = true;
            if (logOnStart)
            {
                Debug.Log("[KvAr] AR-СЦЕНА ГОТОВА · камера: " +
                          (arCamera != null ? arCamera.name : "НЕТ") +
                          " · логическая голова: " + (logicalHead != null ? logicalHead.name : "НЕТ") +
                          " · менеджеры: сессия " + Yes(session) + ", камера " + Yes(cameraManager) +
                          ", плоскости " + Yes(planeManager) + ", якоря " + Yes(anchorManager) +
                          ", лучи " + Yes(raycastManager) +
                          " · отключено VR-компонентов: " + DisabledComponents);
                Debug.Log("[KvAr] ЦЕЛЬ: " + Application.targetFrameRate +
                          " кадр/с · разрешение экрана " + Screen.width + "×" + Screen.height);

                // P4-19 (п.4, 11.11.2026). ЧТО БЫЛО: в журнале запуска не было НИ устройства, НИ
                // фактического графического API. Из-за этого «чёрный экран» на планшете нельзя было
                // отличить от «на устройство поставлен APK для шлема»: оба случая выглядят одинаково.
                // ЧТО СТАЛО: печатается модель устройства и тот API, которым реально рисует Unity
                // (у шлема — Vulkan, у планшета — OpenGLES3; несовпадение видно сразу).
                Debug.Log("[KvAr] УСТРОЙСТВО: " + SystemInfo.deviceModel + " · " +
                          SystemInfo.operatingSystem + " · графика: " + SystemInfo.graphicsDeviceType +
                          " («" + SystemInfo.graphicsDeviceName + "»)");
            }
        }

        void Start()
        {
            if (installObjectFactoryHooks) InstallObjectFactoryHooks();
            EnsureArUiSupport();
            if (ui != null) BuildScreenButtons();
            EnsureGyroFallback();
            if (logOnStart) StartCoroutine(LogReadyWhenSessionStarts());
        }

        /// <summary>
        /// ПОВЕРХНОСТИ: включить сетку найденных плоскостей и выбор самого большого стола.
        /// Владелец 06.11.2026: «полноценно работающий AR, автоопределение поверхностей».
        /// </summary>
        private void EnsurePlaneVisualizer()
        {
            if (GetComponent<KvArPlaneVisualizer>() == null)
                gameObject.AddComponent<KvArPlaneVisualizer>();
            planeVisualizer = GetComponent<KvArPlaneVisualizer>();
            if (planeVisualizer == null) return;
            planeVisualizer.planeManager = planeManager;
            planeVisualizer.arCamera = arCamera;
            if (placement != null) placement.planeVisualizer = planeVisualizer;
            Debug.Log("[KvAr] ПОВЕРХНОСТИ: сетка плоскостей включена · зелёный — самый большой стол, " +
                      "робот встанет на него по кнопке «Поставить»");
        }

        /// <summary>
        /// ПОДНЯТЬ ИНТЕРФЕЙС БЕЗ VR-РИГА. `KvXrRig` в ArMain выключен (он создаёт руки/жесты/
        /// облёт камеры — в AR они ломают ARCore), поэтому меню, окна, мост потока, указатель и
        /// гашение виртуальных стендов поднимаются здесь — теми же функциями, что звал риг.
        /// </summary>
        public void EnsureArUiSupport()
        {
            Camera head = logicalHead != null ? logicalHead : arCamera;
            if (head == null)
            {
                Debug.LogWarning("[KvAr] ИНТЕРФЕЙС: нет логической головы — меню и поток не подняты");
                return;
            }

            // 1) ВИРТУАЛЬНЫЕ СТЕНДЫ 14,4 м НЕ ДОЛЖНЫ БЫТЬ ВИДНЫ: в AR стол реальный.
            if (UnityEngine.Object.FindAnyObjectByType<KvXrSceneEmpty>() == null)
            {
                GameObject emptyGo = new GameObject("ArSceneEmpty");
                emptyGo.AddComponent<KvXrSceneEmpty>();
                Debug.Log("[KvAr] СЦЕНА: виртуальные стенды 14,4 м гасятся (в AR стол реальный)");
            }

            // 2) СТАРТОВОЕ МЕНЮ (110 команд) и окна — как в шлеме.
            KvXrStartMenu.Ensure(head.transform, head);
            KvXrWindowHost.Ensure(head);

            // 3) МОСТ ПОТОКА: прицел подаёт палец, поэтому авто-прицел моста выключен.
            KvXrFlowBridge flowBridge = UnityEngine.Object.FindAnyObjectByType<KvXrFlowBridge>();
            if (flowBridge == null)
            {
                GameObject bridgeGo = new GameObject("KvXrFlowBridge");
                flowBridge = bridgeGo.AddComponent<KvXrFlowBridge>();
            }
            flowBridge.headCamera = head;
            flowBridge.feedAim = false;       // прицел подаёт KvArTouchInput
            flowBridge.feedConfirm = false;

            // 4) УКАЗАТЕЛЬ: камера головы (в AR это логическая копия AR-камеры).
            KvXrUiPointer[] pointers = UnityEngine.Object.FindObjectsByType<KvXrUiPointer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < pointers.Length; i++)
                if (pointers[i] != null) pointers[i].headCamera = head;

            // 5) ПАНЕЛЬ 110 КОМАНД В AR (руки нет, XRHMD нет): разрешаем показ без шлема,
            // отцепляем от руки и управляем позицией сами. Без этого веер команд закрыт
            // гейтом `HeadsetPresent` и на планшете недостижим (аудит 05.11.2026).
            KvXrGroupPanel hand = KvXrPanels.HandPanel;
            if (hand != null)
            {
                hand.allowWithoutHeadset = true;
                hand.arDetached = true;
                hand.SetArVisible(false);           // сначала скрыта; открывается кнопкой «Команды»
                Debug.Log("[KvAr] ИНТЕРФЕЙС AR: панель команд доступна без шлема (кнопка «Команды»)");
            }

            // 6) СТАРТОВОЕ МЕНЮ В AR: на планшете «голова» может быть ниже 0,5 м (устройство
            // держат у стола) — разрешаем низкую позу и поднимаем меню до читаемой высоты.
            if (KvXrStartMenu.Active != null)
            {
                KvXrStartMenu.Active.allowLowEyeHeight = true;
            }

            Debug.Log("[KvAr] ИНТЕРФЕЙС AR: меню, окна и поток подняты без VR-рига " +
                      "(руки/жесты/облёт камеры не работают)");
        }

        void OnDestroy()
        {
            ClearObjectFactoryHooks();
        }

        void Update()
        {
            if (!Ready) return;
            DiagnoseYellowOnce();
            CheckPlaneDetection();
            CheckDisplayAspect();
            CheckCameraGate();
            fpsSmoothed = Mathf.Lerp(fpsSmoothed <= 0f ? 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime) : fpsSmoothed,
                1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.1f);

            if (stateLogIntervalSeconds > 0f && Time.unscaledTime >= nextStateLog)
            {
                nextStateLog = Time.unscaledTime + stateLogIntervalSeconds;
                LogState();
            }

            UpdateStatusLine();
        }

        /// <summary>
        /// ВОЗВРАТ ИЗ ФОНА. ARCore после сворачивания приложения часто отдаёт ПУСТОЙ набор
        /// плоскостей и сам не восстанавливается: в журнале планшета 07.11.2026 было 19 минут
        /// «SessionTracking · кадр/с 30 · плоскостей 0», помогала только переустановка. На
        /// возврате перезакрепляем ориентацию окна и заново включаем обнаружение поверхностей.
        /// </summary>
        void OnApplicationPause(bool paused)
        {
            if (paused) return;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            ApplyPlaneDetection();
            planeEmptySince = 0f;
            planeWatchdogLogged = false;
            aspectLogged = false;
            Debug.Log("[KvAr] ВОЗВРАТ ИЗ ФОНА: ориентация перезакреплена, обнаружение поверхностей " +
                      "включено · сессия " + KvArSessionController.DescribeState(ARSession.state));
        }

        /// <summary>ВКЛЮЧИТЬ ОБНАРУЖЕНИЕ ПОВЕРХНОСТЕЙ (горизонтальные + вертикальные).</summary>
        private void ApplyPlaneDetection()
        {
            if (planeManager == null) return;
            if (!planeManager.enabled) planeManager.enabled = true;
            planeManager.requestedDetectionMode =
                PlaneDetectionMode.Horizontal | PlaneDetectionMode.Vertical;
        }

        /// <summary>
        /// ЭКРАН «ДОСТУП К КАМЕРЕ / ПРОДОЛЖИТЬ» НЕ ДОЛЖЕН ВИСЕТЬ (07.11.2026). Факт планшета:
        /// окно осталось на экране минутами, хотя кадры шли и трекинг работал — владельцу
        /// приходилось нажимать кнопку при каждом запуске. Как только трекинг идёт 20 с, окно
        /// снимается само (внутренний таймер сторожа камеры при этом остаётся).
        /// </summary>
        private void CheckCameraGate()
        {
            if (cameraGate == null || !cameraGate.AwaitingContinue) return;
            bool tracking = sessionController != null &&
                            sessionController.State == ARSessionState.SessionTracking;
            if (!tracking)
            {
                gateWaitingSince = 0f;
                return;
            }
            if (gateWaitingSince <= 0f)
            {
                gateWaitingSince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - gateWaitingSince < 20f) return;
            gateWaitingSince = 0f;
            cameraGate.ConfirmNow("трекинг идёт 20 с — камера доказана кадрами");
        }

        private float gateWaitingSince;

        /// <summary>
        /// СТОРОЖ ОБНАРУЖЕНИЯ ПЛОСКОСТЕЙ. Факт с планшета 07.11.2026 (журнал устройства): при
        /// `SessionTracking` и 30 кадр/с плоскости не появлялись минутами, а нативная часть ARCore
        /// писала `VIO_OUTPUT_NOT_TRACKING` и `VisualInertialState is kNotTracking. Wait.` — то есть
        /// ARCore работает ТОЛЬКО по инерции и карту не строит (мало движения/света/текстуры).
        /// Поэтому: (1) в журнал идут числа (`currentDetectionMode`, `notTrackingReason`),
        /// (2) обнаружение плоскостей перевключается, (3) подсистема перезапускается ТОЛЬКО если
        /// она реально остановлена — вслепую сбрасывать AR нельзя, это стирает карту VIO и мешает
        /// ARCore, (4) владельцу показывается подсказка, что делать.
        /// </summary>
        private void CheckPlaneDetection()
        {
            if (planeManager == null || sessionController == null) return;
            bool tracking = sessionController.State == ARSessionState.SessionTracking;
            int count = planeManager.trackables.count;

            if (!tracking || count > 0)
            {
                if (planeEmptySince > 0f && count > 0 && planeWatchdogLogged)
                {
                    Debug.Log("[KvAr] ПОВЕРХНОСТИ: обнаружение восстановлено · плоскостей " + count +
                              " (перезапусков подсистемы " + planeRestarts + ")");
                }
                planeEmptySince = 0f;
                planeWatchdogLogged = false;
                return;
            }

            if (planeEmptySince <= 0f)
            {
                planeEmptySince = Time.unscaledTime;
                return;
            }
            if (Time.unscaledTime - planeEmptySince < Mathf.Max(3f, planeWatchdogSeconds)) return;

            planeEmptySince = Time.unscaledTime;
            LogPlaneDiagnosis("плоскостей нет " + planeWatchdogSeconds.ToString("0") + " с при трекинге");
            ApplyPlaneDetection();

            bool running = planeManager.subsystem != null && planeManager.subsystem.running;
            if (!running)
            {
                if (planeRestarts < Mathf.Max(1, maxPlaneRestarts))
                {
                    planeRestarts++;
                    Debug.LogWarning("[KvAr] ПОВЕРХНОСТИ: подсистема ОСТАНОВЛЕНА — перезапускаю " +
                                     "AR (попытка " + planeRestarts + " из " + maxPlaneRestarts + ")");
                    ResetArSubsystem();
                }
                return;
            }

            // Подсистема работает, но поверхностей нет: не сбрасываем AR вслепую.
            if (!planeWatchdogLogged)
            {
                planeWatchdogLogged = true;
                Debug.LogWarning("[KvAr] ПОВЕРХНОСТИ: подсистема работает, поверхностей нет — " +
                                 "ARCore не построил карту. Что делать: медленно поводить " +
                                 "планшетом над столом, добавить света, поверхность должна быть " +
                                 "с текстурой и без бликов");
            }
            if (ui != null)
                ui.SetHint("ARCore ещё строит карту: медленно поводите планшетом над столом · " +
                           "нужен свет и текстурная поверхность");
        }

        /// <summary>ДИАГНОСТИКА ПЛОСКОСТЕЙ В ЖУРНАЛ: числа, а не «не работает».</summary>
        private void LogPlaneDiagnosis(string why)
        {
            var sb = new StringBuilder();
            sb.Append("[KvAr] ПОВЕРХНОСТИ: ДИАГНОСТИКА (").Append(why).Append(")");
            sb.Append(" · сессия ").Append(KvArSessionController.DescribeState(ARSession.state));
            sb.Append(" · причина нетрекинга: ").Append(ARSession.notTrackingReason);
            sb.Append(" · менеджер включён: ").Append(planeManager != null && planeManager.enabled ? "да" : "НЕТ");
            bool running = planeManager != null && planeManager.subsystem != null &&
                           planeManager.subsystem.running;
            sb.Append(" · подсистема: ").Append(running ? "работает" : "ОСТАНОВЛЕНА");
            if (planeManager != null)
            {
                sb.Append(" · запрошено: ").Append(planeManager.requestedDetectionMode);
                sb.Append(" · применяется: ").Append(planeManager.currentDetectionMode);
                sb.Append(" · трекаблов: ").Append(planeManager.trackables.count);
            }
            sb.Append(" · экран ").Append(Screen.width).Append("×").Append(Screen.height);
            if (arCamera != null)
                sb.Append(" · камера ").Append(arCamera.pixelWidth).Append("×").Append(arCamera.pixelHeight);
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// СТОРОЖ КАРТИНКИ: пропорции экрана и камеры. Дефект планшета (кадры tablet-pcui8.png
        /// 05.11 и tablet-ar-surfaces1.png 06.11): приложение оказывалось УЗКОЙ ВЕРТИКАЛЬНОЙ
        /// ПОЛОСОЙ по центру, по бокам — размытый фон, тулбар обрезан: Android держал портретное
        /// окно активности. Здесь пропорции пишутся в журнал, ориентация перезакрепляется и при
        /// явном расхождении один раз перезапускается подсистема AR.
        /// </summary>
        private void CheckDisplayAspect()
        {
            if (arCamera == null) return;
            if (Time.unscaledTime < nextAspectCheck) return;
            nextAspectCheck = Time.unscaledTime + 2f;

            float screenAspect = Screen.height > 0 ? Screen.width / (float)Screen.height : 0f;
            float cameraAspect = arCamera.pixelHeight > 0
                ? arCamera.pixelWidth / (float)arCamera.pixelHeight : 0f;
            float deviation = screenAspect > 0.01f && cameraAspect > 0.01f
                ? Mathf.Abs(cameraAspect - screenAspect) / screenAspect : 0f;

            // Портретное окно или чужие пропорции — вернуть горизонтальную ориентацию.
            if (screenAspect < 1f) Screen.orientation = ScreenOrientation.LandscapeLeft;

            if (!aspectLogged || deviation > 0.05f)
            {
                aspectLogged = true;
                Debug.Log("[KvAr] ЭКРАН: пропорции · экран " + Screen.width + "×" + Screen.height +
                          " (" + screenAspect.ToString("0.000") + ") · камера " +
                          arCamera.pixelWidth + "×" + arCamera.pixelHeight + " (" +
                          cameraAspect.ToString("0.000") + ") · расхождение " +
                          (deviation * 100f).ToString("0.0") + "% · ориентация " + Screen.orientation);
            }

            if (deviation > 0.15f && aspectRestarts < 1)
            {
                aspectRestarts++;
                Debug.LogWarning("[KvAr] ЭКРАН: пропорции камеры не совпадают с экраном — " +
                                 "перезапуск подсистемы AR, чтобы картинка заняла весь экран");
                ResetArSubsystem();
            }
        }

        /// <summary>ПЕРЕЗАПУСК ПОДСИСТЕМЫ AR (`session.Reset()` — тот же путь, что у сторожа сессии).</summary>
        private void ResetArSubsystem()
        {
            if (session == null)
            {
                Debug.LogWarning("[KvAr] ПОВЕРХНОСТИ: сессии AR нет — перезапуск невозможен");
                return;
            }
            session.Reset();
        }

        // ---------------------------------------------------------------- настройка

        /// <summary>НАЙТИ ССЫЛКИ СЦЕНЫ, если инструмент сборки их не заполнил (идемпотентно).</summary>
        public void ResolveReferences()
        {
            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();
            if (logicalHead == null) logicalHead = KvArRigBridge.FindLogicalHead();
            if (session == null) session = UnityEngine.Object.FindAnyObjectByType<ARSession>();
            if (cameraManager == null) cameraManager = UnityEngine.Object.FindAnyObjectByType<ARCameraManager>();
            if (planeManager == null) planeManager = UnityEngine.Object.FindAnyObjectByType<ARPlaneManager>();
            if (anchorManager == null) anchorManager = UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>();
            if (raycastManager == null) raycastManager = UnityEngine.Object.FindAnyObjectByType<ARRaycastManager>();
            if (rigBridge == null) rigBridge = GetComponent<KvArRigBridge>();
            if (sessionController == null) sessionController = GetComponent<KvArSessionController>();
            if (world == null) world = GetComponent<KvArWorld>();
            if (content == null) content = GetComponent<KvArContent>();
            if (placement == null) placement = GetComponent<KvArPlacement>();
            if (touch == null) touch = GetComponent<KvArTouchInput>();
            if (ui == null) ui = GetComponent<KvArUi>();
        }

        /// <summary>
        /// ОТКЛЮЧИТЬ VR-КОМПОНЕНТЫ, КОТОРЫЕ В AR ВРЕДНЫ. Каждый — с причиной в журнале
        /// (правило проекта: выключенное молча — это найденный через неделю дефект).
        /// </summary>
        public void DisableVrOnlyComponents()
        {
            DisabledComponents = 0;
            DisabledComponents += DisableAll<KvXrHeadTracking>(
                "пишет позу головы каждый кадр — в AR камерой владеет AR Foundation");
            DisabledComponents += DisableAll<KvXrLocomotion>(
                "телепорт и повороты стиком — оператор в AR стоит на своём полу");
            DisabledComponents += DisableAll<KvXrMrRoom>(
                "MR-комната шлема ставит робота по константе — в AR постановка по найденной плоскости");
            DisabledComponents += DisableAll<KvXrHangarAnchor>(
                "ангар 40×50 м ставится вокруг оператора — в AR это стена перед лицом");
            DisabledComponents += DisableAll<KvXrSceneContent>(
                "стенды 14,4 м и виртуальный пол — в AR пол и стол реальные");
        }

        private int DisableAll<T>(string reason) where T : Behaviour
        {
            T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int disabled = 0;
            for (int i = 0; i < all.Length; i++)
            {
                T item = all[i];
                if (item == null || !item.enabled) continue;
                item.enabled = false;
                disabled++;
                Debug.Log("[KvAr] ОТКЛЮЧЕНО: " + typeof(T).Name + " («" + item.name + "») — " + reason);
            }
            return disabled;
        }

        /// <summary>ПОГАСИТЬ ВИРТУАЛЬНЫЙ АНГАР: фон обязан давать камера устройства.</summary>
        public void HideVirtualHangar()
        {
            string[] names = { "XrHangar", "Hangar", "Level", "Plane" };
            GameObject[] all = UnityEngine.Object.FindObjectsByType<GameObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject item = all[i];
                if (item == null) continue;
                if (item == gameObject || item.transform.IsChildOf(transform)) continue;
                for (int n = 0; n < names.Length; n++)
                {
                    if (!string.Equals(item.name, names[n], StringComparison.OrdinalIgnoreCase)) continue;
                    item.SetActive(false);
                    HangarHidden = true;
                    Debug.Log("[KvAr] ПОГАШЕНО: виртуальное окружение «" + item.name +
                              "» — в AR видно реальный мир, его не перекрываем");
                    break;
                }
            }
            if (!HangarHidden)
                Debug.Log("[KvAr] окружение: в сцене нет виртуального ангара (это нормально)");
        }

        /// <summary>
        /// ХУКИ ФАБРИКИ ОБЪЕКТОВ. Меню шлема создаёт столы/роботов «в 1,2 м перед оператором на
        /// y = 0». В AR y = 0 — это точка запуска устройства, а не пол. Хук подставляет точку на
        /// найденной плоскости (и высоту якоря), чтобы команды меню работали и в AR.
        /// </summary>
        public void InstallObjectFactoryHooks()
        {
            KvXrObjectFactory.SpotOverride = ArSpawnSpot;
            // ВЫСОТЫ АБСОЛЮТНЫЕ (AR): пол — найденная плоскость, поэтому фабрика НЕ должна
            // прибавлять высоту якоря к позиции робота. Раньше это давало двойную высоту
            // (робот на 0,98 м выше стола); найдено аудитом 05.11.2026.
            KvXrObjectFactory.AbsoluteWorldHeights = true;
            KvXrObjectFactory.RobotPositionFixup = null;
            KvXrObjectFactory.TableFloorFixup = ArTableFloor;
            Debug.Log("[KvAr] ФАБРИКА ОБЪЕКТОВ: меню ставит столы и роботов на найденную плоскость " +
                      "(хуки включены)");
        }

        /// <summary>Снять хуки при выходе из AR-сцены, чтобы VR/ПК не получили чужие правила.</summary>
        public void ClearObjectFactoryHooks()
        {
            KvXrObjectFactory.SpotOverride = null;
            KvXrObjectFactory.TableFloorFixup = null;
            KvXrObjectFactory.RobotPositionFixup = null;
            KvXrObjectFactory.AbsoluteWorldHeights = false;
        }

        private static Vector3? ArSpawnSpot(Camera camera, float distanceMeters)
        {
            Vector3 point;
            if (!KvArWorld.TryGetMenuSpawnPoint(camera, distanceMeters, out point)) return null;
            return point;
        }

        private static Vector3 ArTableFloor(Vector3 floorPoint)
        {
            if (!KvArWorld.MenuSpawnReady) return floorPoint;
            floorPoint.y = KvArWorld.MenuFloorY;
            return floorPoint;
        }

        private static Vector3 ArRobotPosition(Vector3 position)
        {
            if (!KvArWorld.MenuSpawnReady) return position;
            // Вызывающий ставит основание робота на высоту стола (0,98 м) в мировых координатах.
            // В AR это высота над найденной плоскостью, поэтому прибавляем высоту якоря.
            position.y += KvArWorld.MenuFloorY;
            return position;
        }

        /// <summary>СОЗДАТЬ КНОПКИ AR-ПУЛЬТА (крупные, под палец).</summary>
        /// <summary>
        /// КОМПАКТНЫЙ АР-ПУЛЬТ (ПК-ВИД): только те действия, которых нет в ПК-интерфейсе.
        /// Всё остальное — тулбар/меню/окна проекта, поднятые как на компьютере.
        /// </summary>
        /// <summary>
        /// ПК-ВИД ВКЛЮЧАЕМ ТОЛЬКО ПОСЛЕ ТРЕКИНГА (или через 8 с). Включение десктопных канвасов
        /// до старта ARCore мешало инициализации (VIO «kNotTracking», сессия висела в
        /// SessionInitializing — проверено на планшете 05.11.2026). Сначала камера/трекинг,
        /// потом тяжёлый ПК-интерфейс.
        /// </summary>
        private System.Collections.IEnumerator EnablePcUiAfterTracking()
        {
            float deadline = Time.unscaledTime + 8f;
            while (Time.unscaledTime < deadline)
            {
                if (sessionController != null && sessionController.State == ARSessionState.SessionTracking)
                    break;
                yield return null;
            }
            EnablePcUiForTablet();
            if (sessionController == null || sessionController.State != ARSessionState.SessionTracking)
                Debug.LogWarning("[KvAr] ИНТЕРФЕЙС: трекинг ещё не начался — ПК-вид включён без него");
        }

        /// <summary>ГИРО-ФОЛБЭК: пока ARCore не отслеживает, поворот устройства вращает мир.</summary>
        private void EnsureGyroFallback()
        {
            if (GetComponent<KvArGyroFallback>() == null)
                gameObject.AddComponent<KvArGyroFallback>();
        }
        private bool yellowDiagnosed;
        private float yellowDiagAt;

        /// <summary>
        /// ДИАГНОСТИКА «ЖЁЛТОГО ЭКРАНА» (одна строка в журнал). Владелец видел жёлтую заливку
        /// вместо камеры; сканирование канвасов её не нашло, значит источник — 3D-объект или окно,
        /// созданное позже. Смотрим центральный луч и все видимые рендереры у камеры.
        /// </summary>
        private void DiagnoseYellowOnce()
        {
            if (yellowDiagnosed) return;
            if (arCamera == null) return;
            if (yellowDiagAt <= 0f) yellowDiagAt = Time.unscaledTime + 8f;
            if (Time.unscaledTime < yellowDiagAt) return;
            yellowDiagnosed = true;

            Ray center = arCamera.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.5f));
            if (Physics.Raycast(center, out RaycastHit hit, 50f))
                Debug.Log("[KvAr] ДИАГНОСТИКА ЭКРАНА: в центре 3D-объект «" + PathOf(hit.collider.transform) +
                          "» материал «" + (hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.name : "нет") + "»");
            else
                Debug.Log("[KvAr] ДИАГНОСТИКА ЭКРАНА: в центре 3D-объекта нет (жёлтое — UI или камера)");

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            int shown = 0;
            for (int i = 0; i < renderers.Length && shown < 12; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.isVisible) continue;
                Material m = r.sharedMaterial;
                Debug.Log("[KvAr] ДИАГНОСТИКА ЭКРАНА: видимый «" + PathOf(r.transform) +
                          "» материал «" + (m != null ? m.name : "нет") + "»");
                shown++;
            }
        }

        private static string PathOf(Transform t)
        {
            if (t == null) return "null";
            string path = t.name;
            Transform p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 6) { path = p.name + "/" + path; p = p.parent; }
            return path;
        }

        /// <summary>
        /// ПОГАСИТЬ ВСЕ КАНВАСЫ, КРОМЕ НАШИХ (06.11.2026). Владелец видел жёлтую заливку во весь
        /// экран при работающей камере. Причина — посторонний world-space канвас проекта
        /// (`KazistovVv_UI`/окна), который не ловится сторожем. Оставляем только AR-пульт,
        /// страж камеры и панель команд (она должна открываться по кнопке).
        /// </summary>
        private void HideForeignCanvases()
        {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            int hidden = 0;
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.GetComponentInParent<KvArUi>(true) != null) continue;
                if (canvas.GetComponentInParent<KvArCameraGate>(true) != null) continue;
                if (canvas.GetComponentInParent<KvXrGroupPanel>(true) != null) continue;
                if (canvas.GetComponentInParent<KvXrStartMenu>(true) != null) continue;
                if (canvas.GetComponentInParent<KvXrWindowHost>(true) != null) continue;
                // КАНВАС ПАНЕЛЕЙ ОПЕРАТОРА (R6-1, 12.11.2026). Это не «посторонний канвас»:
                // в нём живут панели, которые оператор открывает командой («Настройки» — и есть
                // путь к переключателю VR/MR). Гасить его здесь значило бы воспроизвести тот же
                // дефект с другой стороны: панель включена, канвас выключен. Судьбу этого канваса
                // решает сторож оверлея по правилу `KvXrOverlayPolicy.ClassifyDockCanvas`, и правило
                // у обоих сторожей ОДНО (`KvXrOperatorPanelCanvas`) — иначе они разойдутся.
                if (KvXrOperatorPanelCanvas.IsOperatorPanelCanvas(canvas)) continue;
                if (!canvas.enabled) continue;
                canvas.enabled = false;
                hidden++;
                Debug.Log("[KvAr] ИНТЕРФЕЙС: посторонний канвас «" + canvas.gameObject.name + "» погашен (камера не должна быть закрыта)");
            }
            if (hidden > 0) Debug.Log("[KvAr] ИНТЕРФЕЙС: погашено посторонних канвасов " + hidden);
        }

        /// <summary>
        /// ОЧИСТКА РАБОЧЕГО ПРОСТРАНСТВА (06.11.2026, запрос владельца).
        /// Удаляет ВСЁ, что создано в AR: столы, роботов, точки и траектории; снимает якорь и
        /// возвращает мир к камере. Вызывается из окна подтверждения (кнопка «Очистить» /
        /// «Правка → Очистить рабочее пространство»), поэтому случайного удаления нет.
        /// </summary>
        public void ClearWorkspace()
        {
            Debug.Log("[KvAr] ОЧИСТКА: начало — убираю столы, роботов, точки, траектории и якорь");

            if (ui != null) ui.ClosePanels();

            TrajectoryFlowController flow = UnityEngine.Object.FindAnyObjectByType<TrajectoryFlowController>();
            if (flow != null)
            {
                flow.ResetFlow("очистка рабочего пространства");
                Debug.Log("[KvAr] ОЧИСТКА: траектория и точки сброшены");
            }

            // ШАБЛОНЫ РОБОТОВ НЕ ТРОГАЕМ: иначе после очистки не из чего будет создать нового.
            ObjectSpawner spawner = UnityEngine.Object.FindAnyObjectByType<ObjectSpawner>();
            var templates = new System.Collections.Generic.HashSet<GameObject>();
            if (spawner != null)
            {
                RobotController[] templateRobots = spawner.RefreshRobotTemplates();
                for (int i = 0; i < templateRobots.Length; i++)
                    if (templateRobots[i] != null) templates.Add(templateRobots[i].gameObject);
            }

            int destroyed = 0;
            RobotController[] robots = UnityEngine.Object.FindObjectsByType<RobotController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < robots.Length; i++)
            {
                RobotController robot = robots[i];
                if (robot == null || templates.Contains(robot.gameObject)) continue;
                UnityEngine.Object.Destroy(robot.gameObject);
                destroyed++;
            }

            RegisteredObject[] objects = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < objects.Length; i++)
            {
                RegisteredObject item = objects[i];
                if (item == null) continue;
                UnityEngine.Object.Destroy(item.gameObject);
                destroyed++;
            }

            if (world != null) world.Clear("очистка рабочего пространства");
            if (touch != null)
            {
                touch.ClearSelection();
                touch.ResetView();
            }
            KvArUi.ResetCreatedTableCounter();

            if (placement != null) placement.RePlace();
            if (ui != null)
            {
                ui.HideMessage();
                ui.SetHint("Рабочее пространство очищено — наведите камеру на стол и нажмите «Поставить»");
            }
            Debug.Log("[KvAr] ОЧИСТКА: готово · удалено объектов " + destroyed +
                      " · якорь снят, мир вернулся к камере");
        }

        /// <summary>
        /// ПРАВЫЙ РЯД БЫСТРЫХ ДЕЙСТВИЙ ПЛАНШЕТА (ПК-вид). Всё остальное — меню, инструменты и
        /// дерево проекта в `KvArUi`; здесь только то, чего нет в ПК-меню: постановка мира,
        /// пересборка, панель ВСЕХ команд и сброс вида.
        /// </summary>
        public void BuildScreenButtons()
        {
            if (ui == null) return;

            ui.AddButton("Поставить", delegate
            {
                if (placement != null && !placement.IsPlaced) placement.TryPlaceAtCenter();
                else Debug.Log("[KvAr] КНОПКА: мир уже поставлен — «Переставить» для нового места");
            });
            ui.AddButton("Переставить", delegate
            {
                if (placement != null) placement.RePlace();
            });
            ui.AddButton("Команды", delegate
            {
                if (ui != null) ui.OpenCommands("robot");
            });
            ui.AddButton("Сброс вида", delegate
            {
                if (touch != null) touch.ResetView();
            });

            KvArUi.EnsureEventSystem();
            Debug.Log("[KvAr] ИНТЕРФЕЙС: правый ряд кнопок — 4 · меню/инструменты/дерево в оверлее");
        }
        /// <summary>
        /// РАЗРЕШЕНИЕ КАМЕРЫ (Android). Без него ARCore не получает кадров: экран остаётся
        /// заливкой, а трекинг стоит — владелец видит «всё жёлтым и не двигается».
        /// </summary>
        private void EnsureCameraPermission()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[KvAr] КАМЕРА: разрешение уже выдано системой");
                return;
            }
            Debug.Log("[KvAr] КАМЕРА: запрашиваю разрешение у системы (диалог Android)");
            Permission.RequestUserPermission(Permission.Camera);
#else
            Debug.Log("[KvAr] КАМЕРА: редактор — системное разрешение не требуется");
#endif
        }

        /// <summary>
        /// ПК-ВИД НА ПЛАНШЕТЕ: включить канвасы проекта (`KazistovVv_UI` — тулбар, меню, окна),
        /// которые штатно гасит `KvXrDesktopOverlayGuard` для шлема. На планшете нужен именно
        /// ПК-подобный интерфейс, поэтому сторож выключен в `Awake`, а канвасы включаются здесь.
        /// </summary>
        /// <summary>
        /// УБРАТЬ ЖЁЛТУЮ ЗАЛИВКУ «ГРАФИЧЕСКОГО ОКНА» ПК-ИНТЕРФЕЙСА. В ПК-версии центральная
        /// область прозрачна (сквозь неё виден 3D). На планшете она рисовалась жёлтой заливкой и
        /// перекрывала камеру AR — владелец видел «всё жёлтым». Гасим самые большие жёлтые
        /// графические элементы: цвет уходит в прозрачный, камера видна, кнопки остаются.
        /// </summary>
        private void ClearYellowViewport(Canvas canvas)
        {
            if (canvas == null) return;
            Graphic[] graphics = canvas.GetComponentsInChildren<Graphic>(true);
            Graphic biggest = null;
            float biggestArea = 0f;
            for (int i = 0; i < graphics.Length; i++)
            {
                Graphic graphic = graphics[i];
                if (graphic == null) continue;
                RectTransform rect = graphic.rectTransform;
                if (rect == null) continue;
                float w = rect.rect.width;
                float h = rect.rect.height;
                float area = w * h;
                if (area > biggestArea) { biggestArea = area; biggest = graphic; }

                Color color = graphic.color;
                bool yellow = color.r > 0.65f && color.g > 0.65f && color.b < 0.35f && color.a > 0.5f;
                if (!yellow) continue;
                bool large = w > Screen.width * 0.3f || h > Screen.height * 0.3f;
                if (!large) continue;
                color.a = 0f;
                graphic.color = color;
                Debug.Log("[KvAr] ИНТЕРФЕЙС: жёлтая заливка «" + graphic.gameObject.name +
                          "» (" + w.ToString("0") + "×" + h.ToString("0") +
                          " px) сделана прозрачной — видна камера AR");
            }

            // САМЫЙ БОЛЬШОЙ ГРАФИЧЕСКИЙ ЭЛЕМЕНТ КАНВАСА — это «окно песочницы» ПК-интерфейса,
            // которое на планшете заливалось жёлтым и перекрывало камеру AR (проверено
            // скриншотом 05.11.2026: жёлтый прямоугольник ровно между панелями ПК-UI). Гасим
            // только заливку (alpha 0): кнопки/текст остаются, а под ней видна камера.
            if (biggest != null && biggestArea > Screen.width * Screen.height * 0.05f)
            {
                Color tint = biggest.color;
                if (tint.a > 0.01f)
                {
                    tint.a = 0f;
                    biggest.color = tint;
                    Debug.Log("[KvAr] ИНТЕРФЕЙС: большая заливка «" + biggest.gameObject.name +
                              "» (" + biggestArea.ToString("0") + " px²) убрана — камера AR видна");
                }
            }
        }

        public void EnablePcUiForTablet()
        {
            int enabledCanvases = 0;
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < canvases.Length; i++)
            {
                Canvas canvas = canvases[i];
                if (canvas == null) continue;
                if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                if (canvas.GetComponentInParent<KazistovVvUI.KazistovVvUIManager>(true) == null) continue;

                if (!canvas.gameObject.activeSelf) canvas.gameObject.SetActive(true);
                if (!canvas.enabled) canvas.enabled = true;
                if (canvas.sortingOrder < 40) canvas.sortingOrder = 40;
                enabledCanvases++;
            }

            // ОКНА ПК-ИНТЕРФЕЙСА МОГУТ БЫТЬ WORLD-SPACE КАНВАСАМИ: они не дети
            // `KazistovVvUIManager`, но именно их заливки перекрывали камеру AR (жёлтый экран).
            // Сканируем ВСЕ канвасы, кроме нашего AR-слоя, и гасим крупные заливки.
            Canvas[] allCanvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include);
            for (int i = 0; i < allCanvases.Length; i++)
            {
                Canvas canvas = allCanvases[i];
                if (canvas == null) continue;
                if (canvas.GetComponentInParent<KvArUi>(true) != null) continue;
                if (canvas.GetComponentInParent<KvArUi>() == ui) continue;
                ClearYellowViewport(canvas);
            }

            if (enabledCanvases > 0 && !pcUiLogged)
            {
                pcUiLogged = true;
                Debug.Log("[KvAr] ИНТЕРФЕЙС: ПК-вид включён — канвасов " + enabledCanvases +
                          " (тулбар/меню/окна как на компьютере) · AR-кнопки справа сверху");
                // Повторы не отменяем: окна ПК-UI создаются кодом ПОЗЖЕ первого включения.
            }
        }

        private void EnsureSingletonAudioListener()
        {
            AudioListener[] listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            if (listeners.Length <= 1) return;
            bool kept = false;
            for (int i = 0; i < listeners.Length; i++)
            {
                if (!kept) { kept = true; continue; }
                listeners[i].enabled = false;
                Debug.Log("[KvAr] ЗВУК: лишний AudioListener на «" + listeners[i].name + "» выключен");
            }
        }

        // ---------------------------------------------------------------- журнал и состояние

        private IEnumerator LogReadyWhenSessionStarts()
        {
            float deadline = Time.unscaledTime + 15f;
            while (Time.unscaledTime < deadline)
            {
                if (sessionController != null && sessionController.Ready)
                {
                    Debug.Log("[KvAr] AR-ПОТОК ГОТОВ: сессия " +
                              KvArSessionController.DescribeState(ARSession.state));
                    yield break;
                }
                yield return null;
            }
            Debug.LogWarning("[KvAr] AR-ПОТОК: за 15 с сессия не перешла в отслеживание — " +
                             "состояние " + KvArSessionController.DescribeState(ARSession.state));
        }

        /// <summary>СТРОКА СОСТОЯНИЯ В ЖУРНАЛ (доказательство на устройстве): числа, не слова.</summary>
        public void LogState()
        {
            int planes = planeManager != null ? planeManager.trackables.count : 0;
            bool placed = world != null && world.IsPlaced;
            Debug.Log("[KvAr] СОСТОЯНИЕ: сессия " +
                      KvArSessionController.DescribeState(ARSession.state) +
                      " · камера " + (cameraManager != null && cameraManager.permissionGranted ? "да" : "НЕТ") +
                      " · плоскостей " + planes +
                      " · якорь " + (placed ? "есть" : "нет") +
                      (placed ? " (" + world.AnchorPosition.ToString("0.000") + " м)" : "") +
                      " · робот " + (world != null && world.Robot != null ? "есть" : "нет") +
                      " · кадр/с " + fpsSmoothed.ToString("0") +
                      (touch != null ? " · " + touch.Describe() : ""));
            lastPlanes = planes;
            lastPlaced = placed;
        }

        /// <summary>Обновить экранную строку состояния (без спама: раз в кадр — дешёвая строка).</summary>
        private float nextFooterTime;
        private float nextCountTime;
        private int uiRobots;
        private int uiTables;

        /// <summary>
        /// НИЖНЯЯ СТРОКА СОСТОЯНИЯ КАК В ПК (06.11.2026). Верхней строки «AR · камера есть…»
        /// больше нет — владелец просил убрать. Внизу: робот/столы/плоскости слева, камера/
        /// трекинг/кадры справа. Подсказка под строкой состояния — нейтральная, без «VR/MR».
        /// </summary>
        private void UpdateStatusLine()
        {
            if (ui == null) return;
            if (Time.unscaledTime < nextFooterTime) return;
            nextFooterTime = Time.unscaledTime + 0.5f;

            int planes = planeManager != null ? planeManager.trackables.count : 0;
            bool placed = world != null && world.IsPlaced;
            bool robotPlaced = world != null && world.Robot != null;
            bool cameraOk = cameraManager != null && cameraManager.permissionGranted;
            bool tracking = sessionController != null &&
                            sessionController.State == ARSessionState.SessionTracking;

            // Счётчики реже строки: поиск объектов по сцене 1 раз в секунду, а не в каждом кадре.
            if (Time.unscaledTime >= nextCountTime)
            {
                nextCountTime = Time.unscaledTime + 1f;
                uiRobots = UnityEngine.Object.FindObjectsByType<RobotController>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None).Length;
                // СТОЛЫ — ФАКТИЧЕСКИ СУЩЕСТВУЮЩИЕ, А НЕ «СКОЛЬКО СОЗДАНО» (07.11.2026).
                // Дефект: после удаления объектов строка показывала «Столов: 8» при пустом мире,
                // потому что раньше печатался счётчик созданий `KvArUi.CreatedTableCount`.
                uiTables = CountExistingTables();
                if (touch != null) touch.ClearSelectionIfGone();
            }

            bool tableFound = planeVisualizer != null && planeVisualizer.BestTable != null;
            string left = "Готово · Робот: " + (robotPlaced ? "поставлен" : (uiRobots > 0 ? uiRobots + " в сцене" : "нет")) +
                          " · Столов: " + uiTables +
                          " · Плоскости: " + planes +
                          " · Стол: " + (tableFound ? "найден" : "нет");
            string right = "Камера: " + (cameraOk ? "вкл" : "ждём") +
                           " · Трекинг: " + (tracking ? "да" : "ждём") +
                           " · " + fpsSmoothed.ToString("0") + " кадр/с";
            ui.SetFooter(left, right);

            // Подсказку о постановке ведёт `KvArPlacement` (у него есть состояние и площади);
            // здесь не перебиваем её, иначе на экране «две разные инструкции».
            if (touch != null && !string.IsNullOrEmpty(touch.SelectedLabel))
            {
                ui.SetHint("Выбрано: " + touch.SelectedLabel);
            }
            else if (placed)
            {
                ui.SetHint("Коснитесь объекта для выбора · «Очистить» — новое рабочее пространство");
            }
        }
        private static string Yes(UnityEngine.Object target)
        {
            return target != null ? "есть" : "НЕТ";
        }

        /// <summary>
        /// СКОЛЬКО СТОЛОВ РЕАЛЬНО СТОИТ В МИРЕ. Считаем по объектам сцены (`RegisteredObject`,
        /// имя «Стол…»), а не по счётчику созданий: иначе после удаления/очистки строка врёт.
        /// </summary>
        public static int CountExistingTables()
        {
            RegisteredObject[] objects = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            int tables = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                RegisteredObject item = objects[i];
                if (item == null) continue;
                string name = string.IsNullOrEmpty(item.DisplayName) ? item.name : item.DisplayName;
                if (name == null) continue;
                if (name.IndexOf("стол", StringComparison.OrdinalIgnoreCase) < 0) continue;
                tables++;
            }
            return tables;
        }

        /// <summary>СТРОКА-ДОКАЗАТЕЛЬСТВО О РЕЖИМЕ (для теста и отчёта).</summary>
        public static string ModeLine()
        {
            return "[KvAr] РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ";
        }
    }
}
