using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using KazistovVvXr;

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

        private float nextStateLog;
        private int lastPlanes = -1;
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

            Application.targetFrameRate = Mathf.Clamp(targetFrameRate, 20, 120);
            QualitySettings.vSyncCount = 0;

            ResolveReferences();
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
            }
        }

        void Start()
        {
            if (installObjectFactoryHooks) InstallObjectFactoryHooks();
            EnsureArUiSupport();
            if (ui != null) BuildScreenButtons();
            if (logOnStart) StartCoroutine(LogReadyWhenSessionStarts());
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
            fpsSmoothed = Mathf.Lerp(fpsSmoothed <= 0f ? 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime) : fpsSmoothed,
                1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime), 0.1f);

            if (stateLogIntervalSeconds > 0f && Time.unscaledTime >= nextStateLog)
            {
                nextStateLog = Time.unscaledTime + stateLogIntervalSeconds;
                LogState();
            }

            UpdateStatusLine();
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
            KvXrObjectFactory.TableFloorFixup = ArTableFloor;
            KvXrObjectFactory.RobotPositionFixup = ArRobotPosition;
            Debug.Log("[KvAr] ФАБРИКА ОБЪЕКТОВ: меню ставит столы и роботов на найденную плоскость " +
                      "(хуки включены)");
        }

        /// <summary>Снять хуки при выходе из AR-сцены, чтобы VR/ПК не получили чужие правила.</summary>
        public void ClearObjectFactoryHooks()
        {
            KvXrObjectFactory.SpotOverride = null;
            KvXrObjectFactory.TableFloorFixup = null;
            KvXrObjectFactory.RobotPositionFixup = null;
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
            ui.AddButton("Меню", delegate
            {
                if (KvXrStartMenu.Active != null)
                {
                    KvXrStartMenu.Active.Show();
                    Debug.Log("[KvAr] КНОПКА: стартовое меню показано");
                }
                else
                {
                    KvXrGroupPanel hand = KvXrPanels.HandPanel;
                    if (hand != null) hand.OpenStartMenu();
                    Debug.Log("[KvAr] КНОПКА: меню команд открыто");
                }
            });
            ui.AddButton("Двигать точку", delegate
            {
                if (touch != null) touch.ToggleMovePointMode();
            });
            ui.AddButton("Сброс вида", delegate
            {
                if (touch != null) touch.ResetView();
            });
            Debug.Log("[KvAr] ПУЛЬТ AR: кнопок " + ui.ButtonCount);
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
        private void UpdateStatusLine()
        {
            if (ui == null) return;
            int planes = planeManager != null ? planeManager.trackables.count : 0;
            bool placed = world != null && world.IsPlaced;
            bool robot = world != null && world.Robot != null;
            bool cameraOk = cameraManager != null && cameraManager.permissionGranted;
            bool sessionReady = sessionController != null && sessionController.Ready;
            float anchorHeight = placed && world != null ? world.FloorY : -9999f;
            ui.SetStatus(KvArUi.BuildStatusLine(sessionReady, cameraOk, planes, placed, robot,
                fpsSmoothed, anchorHeight));
            if (touch != null && !string.IsNullOrEmpty(touch.SelectedLabel))
                ui.SetStatus(ui.StatusText + "\nвыбрано: " + touch.SelectedLabel);
        }

        private static string Yes(UnityEngine.Object target)
        {
            return target != null ? "есть" : "НЕТ";
        }

        /// <summary>СТРОКА-ДОКАЗАТЕЛЬСТВО О РЕЖИМЕ (для теста и отчёта).</summary>
        public static string ModeLine()
        {
            return "[KvAr] РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ";
        }
    }
}
