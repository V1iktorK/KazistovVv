using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.XR.ARFoundation;
using KazistovVvXr;
using KazistovVvFeatures;
using TrajectoryCore;
using KazistovVv.Core;
using KazistovVvUI;

namespace KazistovVvAr
{
    /// <summary>
    /// УПРАВЛЕНИЕ КАСАНИЯМИ В AR: МЕНЮ, ВЫБОР, ПОСТАНОВКА, ВИД, ТОЧКА.
    ///
    /// ЗАЧЕМ СВОЙ ВВОД. В шлеме руки — контроллеры с курками; на планшете их нет. Но вся логика
    /// интерфейса и потока уже написана и проверена: панель команд умеет наводиться ГЕОМЕТРИЕЙ
    /// луча (`KvXrGroupPanel.TryAim`/`PressIndex`), поток точек принимает прицел и подтверждение
    /// (`TrajectoryFlowController.UpdateAim`). Поэтому здесь нет второй копии логики: пальцем
    /// подаётся тот же ЛУЧ и то же ПОДТВЕРЖДЕНИЕ, что в шлеме даёт контроллер.
    ///
    /// РАСКЛАДКА ЖЕСТОВ (блок Г приёмки):
    ///   • тап по иконке меню        → нажатие команды (луч из точки касания);
    ///   • тап по роботу/столу       → выбор объекта (подсветка кольцом + строка статуса);
    ///   • тап по реальному столу    → постановка робота (пока мир не поставлен);
    ///   • тап по пустому месту      → подтверждение точки (как ЛКМ на компьютере);
    ///   • перетаскивание по экрану  → поворот мира вокруг якоря (вид «вокруг сцены»);
    ///   • два пальца: щипок         → масштаб ВИДА (угол обзора; масштаб робота не меняется);
    ///   • два пальца: поворот       → поворот вида; короткий тап двумя — отмена (Esc);
    ///   • режим «Двигать точку»     → перетаскивание одним пальцем двигает выбранную точку.
    ///
    /// ЧЕГО НЕ ДЕЛАЕТ. Не двигает AR-камеру: в AR позой камеры владеет трекинг устройства.
    /// Поэтому «вращение вида вокруг сцены» реализовано поворотом МИРА вокруг якоря — визуально
    /// это то же самое, а трекинг не ломается.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArTouchInput : MonoBehaviour
    {
        /// <summary>AR-камера (из неё строятся лучи).</summary>
        public Camera arCamera;

        /// <summary>Мост камеры (масштаб вида).</summary>
        public KvArRigBridge bridge;

        /// <summary>Постановка мира.</summary>
        public KvArPlacement placement;

        /// <summary>Мир (поворот вида, точка постановки).</summary>
        public KvArWorld world;

        /// <summary>Экранный интерфейс AR.</summary>
        public KvArUi ui;

        /// <summary>Порог «это тап, а не перетаскивание», пиксели.</summary>
        public float tapMaxMovePixels = 28f;

        /// <summary>Максимальная длительность тапа, секунды.</summary>
        public float tapMaxSeconds = 0.6f;

        /// <summary>Сколько градусов поворота вида на пиксель перетаскивания.</summary>
        public float dragDegreesPerPixel = 0.15f;

        /// <summary>Сколько метров перемещения точки на пиксель в режиме «Двигать точку».</summary>
        public float moveMetersPerPixel = 0.002f;

        /// <summary>Максимальная дальность луча прицела, метры.</summary>
        public float aimMaxDistanceMeters = 8f;

        /// <summary>Писать в журнал о нажатиях, выборе и постановке.</summary>
        public bool logOnInput = true;

        /// <summary>Режим перемещения точки (включается кнопкой на экране).</summary>
        public bool MovePointMode { get; private set; }

        /// <summary>Что выбрано касанием (для строки состояния и журнала).</summary>
        public string SelectedLabel { get; private set; }

        /// <summary>Сколько тапов обработано (для журнала и проверок).</summary>
        public int TapCount { get; private set; }

        /// <summary>Сколько нажатий меню выполнено.</summary>
        public int MenuPressCount { get; private set; }

        /// <summary>Сколько раз выбрали объект.</summary>
        public int SelectCount { get; private set; }

        /// <summary>Сколько раз поворачивали вид.</summary>
        public int ViewRotateCount { get; private set; }

        /// <summary>Сколько раз меняли масштаб вида (щипок).</summary>
        public int PinchCount { get; private set; }

        private int fingerId = -1;
        private Vector2 touchStart;
        private Vector2 touchLast;
        private float touchStartTime;
        /// <summary>
        /// ПОВОРОТ МИРА ПАЛЬЦЕМ ВЫКЛЮЧЕН (06.11.2026). Владелец просил «нормально работающий
        /// гироскоп, без спора с пальцем»: обзор на планшете поворачивается ПОВОРОТОМ УСТРОЙСТВА
        /// (AR-камера/гироскоп), а палец больше не крутит мир — иначе два вращения складывались
        /// и казалось, что гироскоп «врёт». Поле оставлено для режима отладки.
        /// </summary>
        public bool allowDragRotate = false;

        /// <summary>Поворот мира двумя пальцами тоже выключен — два пальца только масштаб.</summary>
        public bool allowTwoFingerRotate = false;

        private bool dragging;
        private bool dragBlockedLogged;
        private bool startedOverUi;
        private bool startedOverPanel;
        private float dragAccumulatedDegrees;

        private Vector2 aimScreen;
        private bool aimHasSurface;
        private KvArSurfaceHit aimSurface;
        private Vector3 aimPoint;
        private Vector3 aimNormal = Vector3.up;
        private bool pendingConfirm;
        private bool pendingCancel;
        private bool pendingMoveToggle;
        private Vector3 moveAxis;

        private int twoFingerId = -1;
        private float twoStartDistance;
        private float twoLastDistance;
        private float twoLastAngle;
        private float twoStartTime;
        private bool twoMoved;
        private int pinchSteps;
        private int rotateSteps;

        private GameObject laserGo;
        private LineRenderer laser;
        private Material laserMaterial;
        private GameObject ringGo;
        private LineRenderer ring;

        /// <summary>Выбранный объект (чтобы не показывать «Выбрано: …» для удалённого объекта).</summary>
        private GameObject selectedObject;

        void Awake()
        {
            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();
            if (bridge == null) bridge = GetComponent<KvArRigBridge>();
            if (placement == null) placement = GetComponent<KvArPlacement>();
            if (world == null) world = GetComponent<KvArWorld>();
            aimScreen = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        void Start()
        {
            EnsureLaser();
            DisableFlowBridgeFeeding();
        }

        void Update()
        {
            DisableFlowBridgeFeeding();
            HandleTouches();
            UpdateAim();
            UpdateLaser();
            FeedFlow();
        }

        // ================================================================ КАСАНИЯ (сырые данные)

        private void HandleTouches()
        {
            // ЛЕГАСИ-ВВОД: в проекте включены ОБА обработчика (`activeInputHandler = Both`),
            // поэтому `Input.touchCount` работает и на Android, и в редакторе. Второй системы
            // не заводим: uGUI уже принимает пальцы своим модулем.
            int count = Input.touchCount;
            if (count == 0)
            {
                if (fingerId >= 0) FinishTouch(false);
                return;
            }

            if (count >= 2)
            {
                HandleTwoFingers();
                return;
            }

            twoFingerId = -1;
            Touch touch = Input.GetTouch(0);
            // После жеста двумя пальцами одиночный палец может «продолжиться» без Began —
            // тогда начинаем его заново, иначе окончание тапа не будет обработано.
            if (fingerId < 0 && (touch.phase == TouchPhase.Began || touch.phase == TouchPhase.Moved))
                BeginTouch(touch);
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    BeginTouch(touch);
                    break;
                case TouchPhase.Moved:
                case TouchPhase.Stationary:
                    MoveTouch(touch);
                    break;
                case TouchPhase.Ended:
                    FinishTouch(true);
                    break;
                case TouchPhase.Canceled:
                    FinishTouch(false);
                    break;
            }
        }

        private void BeginTouch(Touch touch)
        {
            fingerId = touch.fingerId;
            touchStart = touch.position;
            touchLast = touch.position;
            touchStartTime = Time.unscaledTime;
            dragging = false;
            startedOverUi = IsOverUi(touch.fingerId);
            startedOverPanel = false;
            dragAccumulatedDegrees = 0f;

            if (!startedOverUi)
            {
                Ray ray = ScreenRay(touch.position);
                startedOverPanel = TryGetPanelHit(ray, out _, out _, out _, out _);
            }
        }

        private void MoveTouch(Touch touch)
        {
            if (touch.fingerId != fingerId) return;
            touchLast = touch.position;
            float moved = Vector2.Distance(touch.position, touchStart);

            if (MovePointMode)
            {
                Vector2 delta = touch.deltaPosition;
                moveAxis += MoveAxisFromScreenDelta(delta);
                return;
            }

            if (!dragging && moved > tapMaxMovePixels && !startedOverUi && !startedOverPanel)
            {
                dragging = true;
                // ЧЕСТНЫЙ ЛОГ (06.11.2026): поворот пальцем выключен, обзор — поворотом планшета.
                Debug.Log(allowDragRotate
                    ? "[KvAr] КАСАНИЕ: перетаскивание — поворот вида вокруг сцены"
                    : "[KvAr] КАСАНИЕ: перетаскивание (обзор — поворотом планшета, палец мир не крутит)");
            }

            if (dragging && allowDragRotate && world != null)
            {
                float dx = touch.deltaPosition.x;
                float degrees = -dx * dragDegreesPerPixel;
                if (Mathf.Abs(degrees) > 0.0001f)
                {
                    world.RotateView(degrees);
                    dragAccumulatedDegrees += Mathf.Abs(degrees);
                }
            }
        }

        private void FinishTouch(bool wasTap)
        {
            float duration = Time.unscaledTime - touchStartTime;
            float moved = Vector2.Distance(touchLast, touchStart);
            bool tap = wasTap && !dragging && moved <= tapMaxMovePixels && duration <= tapMaxSeconds;

            if (tap && !startedOverUi)
            {
                HandleTap(touchLast);
            }
            else if (dragging && !allowDragRotate && !dragBlockedLogged)
            {
                dragBlockedLogged = true;
                Debug.Log("[KvAr] ОБЗОР: поворот пальцем выключен — поворачивайте планшет (гироскоп/AR-камера)");
            }
            else if (dragging && dragAccumulatedDegrees > 0.5f)
            {
                ViewRotateCount++;
                if (logOnInput)
                    Debug.Log("[KvAr] ВИД: повёрнут вокруг сцены на " +
                              dragAccumulatedDegrees.ToString("0.0",
                                  System.Globalization.CultureInfo.InvariantCulture) + "°");
            }

            fingerId = -1;
            dragging = false;
            startedOverUi = false;
            startedOverPanel = false;
            dragAccumulatedDegrees = 0f;
        }

        /// <summary>ЩИПОК И ПОВОРОТ ДВУМЯ ПАЛЬЦАМИ (Г5/Г6).</summary>
        private void HandleTwoFingers()
        {
            Touch a = Input.GetTouch(0);
            Touch b = Input.GetTouch(1);

            if (twoFingerId < 0)
            {
                // Начало жеста: запоминаем базу. Одиночный тап до этого не считается.
                if (fingerId >= 0) fingerId = -1;
                twoFingerId = a.fingerId;
                twoStartDistance = Vector2.Distance(a.position, b.position);
                twoLastDistance = twoStartDistance;
                twoLastAngle = AngleOf(a.position, b.position);
                twoStartTime = Time.unscaledTime;
                twoMoved = false;
                pinchSteps = 0;
                rotateSteps = 0;
                return;
            }

            float distance = Vector2.Distance(a.position, b.position);
            float angle = AngleOf(a.position, b.position);
            float angleDelta = Mathf.DeltaAngle(twoLastAngle, angle);

            if (!twoMoved && Mathf.Abs(distance - twoStartDistance) > 12f) twoMoved = true;
            if (!twoMoved && Mathf.Abs(angleDelta) > 2f) twoMoved = true;

            if (twoMoved)
            {
                if (twoLastDistance > 1f && Mathf.Abs(distance - twoLastDistance) > 1f)
                {
                    float factor = Mathf.Clamp(distance / twoLastDistance, 0.8f, 1.25f);
                    if (bridge != null && Mathf.Abs(factor - 1f) > 0.01f)
                    {
                        bridge.ZoomView(factor);
                        PinchCount++;
                        pinchSteps++;
                    }
                }

                if (allowTwoFingerRotate && Mathf.Abs(angleDelta) > 0.5f && world != null)
                {
                    world.RotateView(angleDelta);
                    ViewRotateCount++;
                    rotateSteps++;
                    if (logOnInput && rotateSteps == 1)
                        Debug.Log("[KvAr] ВИД: поворот двумя пальцами (щепок — масштаб вида)");
                }
            }

            twoLastDistance = distance;
            twoLastAngle = angle;

            if (a.phase == TouchPhase.Ended || b.phase == TouchPhase.Ended || a.phase == TouchPhase.Canceled)
            {
                bool quick = Time.unscaledTime - twoStartTime <= tapMaxSeconds;
                if (!twoMoved && quick)
                {
                    pendingCancel = true;
                    Debug.Log("[KvAr] КАСАНИЕ: два пальца — отмена действия (как Esc)");
                }
                if (twoMoved && logOnInput)
                    Debug.Log("[KvAr] ВИД: масштаб вида шагов " + pinchSteps + ", поворот шагов " + rotateSteps);
                twoFingerId = -1;
            }
        }

        private static float AngleOf(Vector2 a, Vector2 b)
        {
            Vector2 delta = b - a;
            return Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
        }

        // ================================================================ ОБРАБОТКА ТАПА

        private void HandleTap(Vector2 screenPoint)
        {
            TapCount++;

            // 1. МЕНЮ ШЛЕМА (world-space панели): луч из точки касания — штатный путь шлема.
            Ray ray = ScreenRay(screenPoint);
            int index;
            float fromCenter;
            float along;
            Vector3 hitPoint;
            KvXrGroupPanel panel = FindPanelHit(ray, out index, out fromCenter, out along, out hitPoint);
            if (panel != null && index >= 0)
            {
                string title = panel.TitleAt(index);
                panel.PressIndex(index);
                MenuPressCount++;
                if (logOnInput)
                    Debug.Log("[KvAr] КАСАНИЕ: меню «" + title + "» (иконка " + index + ")");
                return;
            }
            if (panel != null)
            {
                if (logOnInput) Debug.Log("[KvAr] КАСАНИЕ: коснулись плиты меню — действие не выполняется");
                return;
            }

            // 2. ПОСТАНОВКА: пока мир не поставлен, тап по реальному столу ставит робота (Б1).
            if (placement != null && !placement.IsPlaced)
            {
                if (placement.TryPlaceAt(screenPoint)) return;
            }

            // 3. ВЫБОР ОБЪЕКТА: робот/стол под пальцем (Г1).
            if (TrySelectWorld(ray)) return;

            // 4. ПОДТВЕРЖДЕНИЕ ТОЧКИ: как левая кнопка мыши на компьютере.
            pendingConfirm = true;
            if (logOnInput)
                Debug.Log("[KvAr] КАСАНИЕ: подтверждение точки в " +
                          aimPoint.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture));
        }

        /// <summary>НАЙТИ ПАНЕЛЬ ПОД ЛУЧОМ. Кнопки — приоритет, плита — запасной вариант.</summary>
        private KvXrGroupPanel FindPanelHit(Ray ray, out int index, out float fromCenter,
                                            out float along, out Vector3 hitPoint)
        {
            index = -1;
            fromCenter = 0f;
            along = 0f;
            hitPoint = ray.origin;
            KvXrGroupPanel found;
            if (!TryGetPanelHit(ray, out found, out index, out fromCenter, out along, out hitPoint))
                return null;
            return found;
        }

        private bool TryGetPanelHit(Ray ray, out KvXrGroupPanel panel, out int index,
                                    out float fromCenter, out float along)
        {
            Vector3 hit;
            return TryGetPanelHit(ray, out panel, out index, out fromCenter, out along, out hit);
        }

        private bool TryGetPanelHit(Ray ray, out KvXrGroupPanel panel, out int index,
                                    out float fromCenter, out float along, out Vector3 hitPoint)
        {
            panel = null;
            index = -1;
            fromCenter = 0f;
            along = 0f;
            hitPoint = ray.origin;

            float bestAlong = float.MaxValue;
            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel candidate = KvXrPanels.At(i);
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                if (!candidate.IsCanvasVisible) continue; // невидимая панель — не цель касания

                int candidateIndex;
                float candidateFromCenter;
                float candidateAlong;
                Vector3 candidatePoint;
                if (candidate.TryAim(ray.origin, ray.direction, out candidateIndex,
                        out candidateFromCenter, out candidateAlong, out candidatePoint))
                {
                    if (candidateIndex < 0) continue;
                    if (candidateAlong <= 0f || candidateAlong > aimMaxDistanceMeters) continue;
                    if (candidateAlong >= bestAlong) continue;
                    bestAlong = candidateAlong;
                    panel = candidate;
                    index = candidateIndex;
                    fromCenter = candidateFromCenter;
                    along = candidateAlong;
                    hitPoint = candidatePoint;
                }
            }
            if (panel != null) return true;

            // ПЛИТА: луч не должен пролетать сквозь интерфейс.
            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel candidate = KvXrPanels.At(i);
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                if (!candidate.IsCanvasVisible) continue; // невидимая панель — не цель касания
                float candidateAlong;
                if (!candidate.TryHitPanelSurface(ray.origin, ray.direction, out candidateAlong)) continue;
                if (candidateAlong <= 0f || candidateAlong > aimMaxDistanceMeters) continue;
                if (candidateAlong >= bestAlong) continue;
                bestAlong = candidateAlong;
                panel = candidate;
                index = -1;
                along = candidateAlong;
                hitPoint = ray.origin + ray.direction.normalized * candidateAlong;
            }
            return panel != null;
        }

        /// <summary>ВЫБОР ОБЪЕКТА (Г1): робот или стол под пальцем — подсветка и строка статуса.</summary>
        private bool TrySelectWorld(Ray ray)
        {
            int mask = ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 30f, mask, QueryTriggerInteraction.Ignore)) return false;

            RegisteredObject registered = hit.collider.GetComponentInParent<RegisteredObject>();
            if (registered == null) return false;

            RobotController robot = registered.GetComponentInParent<RobotController>();
            selectedObject = registered.gameObject;
            SelectedLabel = robot != null
                ? "робот «" + robot.robotName + "»"
                : "объект «" + (string.IsNullOrEmpty(registered.DisplayName)
                    ? registered.name : registered.DisplayName) + "»";
            SelectCount++;
            ShowSelectionRing(robot != null ? robot.transform.position : registered.transform.position,
                              robot != null ? 0.45f : 0.6f);
            if (logOnInput)
                Debug.Log("[KvAr] ВЫБОР: " + SelectedLabel + " · расстояние " +
                          hit.distance.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) + " м");
            return true;
        }

        // ================================================================ ПРИЦЕЛ И ПОТОК

        /// <summary>Обновить прицел: по касанию (пока палец на экране) или по центру экрана.</summary>
        private void UpdateAim()
        {
            aimScreen = fingerId >= 0 ? touchLast : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            aimHasSurface = false;
            aimNormal = Vector3.up;
            aimPoint = ScreenRay(aimScreen).origin + ScreenRay(aimScreen).direction * aimMaxDistanceMeters;

            KvArSurfaceHit surface;
            if (placement != null && placement.TryRaycast(aimScreen, out surface))
            {
                aimHasSurface = true;
                aimSurface = surface;
                aimPoint = surface.point;
                aimNormal = surface.normal;
            }

            Ray ray = ScreenRay(aimScreen);
            int mask = ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, aimMaxDistanceMeters, mask, QueryTriggerInteraction.Ignore))
            {
                // Геометрия сцены (робот, стол) ближе плоскости — прицел на ней (как ЛКМ на ПК).
                if (hit.distance <= Vector3.Distance(ray.origin, aimPoint) + 0.01f)
                {
                    aimPoint = hit.point;
                    aimNormal = hit.normal;
                }
            }

            UpdatePanelHover(ray);
        }

        /// <summary>ПОДСВЕТКА ИКОНКИ ПОД ПРИЦЕЛОМ (наглядно, куда попадёт палец).</summary>
        private void UpdatePanelHover(Ray ray)
        {
            int index;
            float fromCenter;
            float along;
            Vector3 point;
            KvXrGroupPanel hitPanel = FindPanelHit(ray, out index, out fromCenter, out along, out point);
            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel panel = KvXrPanels.At(i);
                if (panel == null) continue;
                panel.SetHoverIndex(panel == hitPanel ? index : -1);
            }
        }

        /// <summary>ПОДАТЬ ПРИЦЕЛ В ПОТОК ТОЧЕК/ТРАЕКТОРИЙ (тот же вход, что у контроллера).</summary>
        private void FeedFlow()
        {
            TrajectoryFlowController flow = ActiveFlow();
            if (flow == null)
            {
                pendingConfirm = false;
                pendingCancel = false;
                pendingMoveToggle = false;
                moveAxis = Vector3.zero;
                return;
            }

            bool overUi = IsOverUi(-1);
            bool confirm = pendingConfirm && !overUi;
            bool cancel = pendingCancel;
            bool moveToggle = pendingMoveToggle;
            Vector3 axis = moveAxis;

            flow.UpdateAim(aimPoint, aimHasSurface && !overUi, confirm, cancel,
                redLaserOn: aimHasSurface,
                greenLaserOn: confirm,
                aimNormal: aimNormal,
                aimOnSurface: aimHasSurface,
                moveToggle: moveToggle,
                moveAxis: axis,
                moveFast: false);

            pendingConfirm = false;
            pendingCancel = false;
            pendingMoveToggle = false;
            moveAxis = Vector3.zero;
        }

        private TrajectoryFlowController ActiveFlow()
        {
            KvXrFlowBridge bridgeActive = KvXrFlowBridge.Active;
            if (bridgeActive != null)
            {
                if (bridgeActive.Flow == null) bridgeActive.EnsureFlow();
                return bridgeActive.Flow;
            }
            return UnityEngine.Object.FindAnyObjectByType<TrajectoryFlowController>();
        }

        /// <summary>
        /// ОТКЛЮЧИТЬ АВТОМАТИЧЕСКИЙ ПРИЦЕЛ МОСТА. Мост шлема каждый кадр подаёт прицел от головы;
        /// в AR его перебивает палец (или центр экрана). Две подачи в один поток дают «дрожащую»
        /// метку — поэтому оставляем одну, нашу, и честно пишем об этом в журнал.
        /// </summary>
        private void DisableFlowBridgeFeeding()
        {
            KvXrFlowBridge active = KvXrFlowBridge.Active;
            if (active == null || !active.feedAim) return;
            active.feedAim = false;
            Debug.Log("[KvAr] ПОТОК: автоматический прицел моста выключен — прицел подаёт палец (AR)");
        }

        // ================================================================ РЕЖИМ ТОЧКИ И ВИД

        /// <summary>ВКЛЮЧИТЬ/ВЫКЛЮЧИТЬ РЕЖИМ ПЕРЕМЕЩЕНИЯ ТОЧКИ (Г4).</summary>
        public void ToggleMovePointMode()
        {
            MovePointMode = !MovePointMode;
            pendingMoveToggle = true;
            Debug.Log("[KvAr] ТОЧКА: режим перемещения точки " +
                      (MovePointMode ? "ВКЛЮЧЁН (ведите пальцем)" : "выключен"));
            if (ui != null)
                ui.SetHint(MovePointMode
                    ? "Режим точки: ведите пальцем — точка движется, два пальца — отмена"
                    : "Обычный режим: тап — подтверждение · обзор — поворотом планшета · щипок — масштаб");
        }

        /// <summary>ВЕКТОР ДВИЖЕНИЯ ТОЧКИ ИЗ ДЕЛЬТЫ ПАЛЬЦА (по осям камеры, в горизонтали/вертикали).</summary>
        private Vector3 MoveAxisFromScreenDelta(Vector2 delta)
        {
            if (arCamera == null) return Vector3.zero;
            Transform camera = arCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up);
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 axis = right * (delta.x * moveMetersPerPixel) +
                           forward * (delta.y * moveMetersPerPixel) +
                           Vector3.up * (delta.y * moveMetersPerPixel * 0.5f);
            return axis;
        }

        // ================================================================ ЛАЗЕР И ПОДСВЕТКА

        private void EnsureLaser()
        {
            if (laser != null) return;
            laserGo = new GameObject("ArAimLaser");
            laser = laserGo.AddComponent<LineRenderer>();
            laser.useWorldSpace = true;
            laser.positionCount = 2;
            laser.startWidth = 0.008f;
            laser.endWidth = 0.003f;
            laser.numCapVertices = 2;
            laser.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            laser.receiveShadows = false;
            laserMaterial = KvRenderPipelineShaders.CreateUnlit(
                "Материал_луча_AR", new Color(0.25f, 0.95f, 0.45f, 1f));
            if (laserMaterial != null) laser.material = laserMaterial;
            laser.enabled = false;
        }

        private void UpdateLaser()
        {
            if (laser == null || arCamera == null) return;
            bool visible = aimHasSurface;
            laser.enabled = visible;
            if (!visible) return;
            Vector3 origin = arCamera.transform.position +
                             arCamera.transform.right * 0.05f - arCamera.transform.up * 0.08f;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, aimPoint);
            Color color = MovePointMode
                ? new Color(1f, 0.85f, 0.2f, 1f)
                : new Color(0.25f, 0.95f, 0.45f, 1f);
            laser.startColor = color;
            laser.endColor = color;
        }

        private void ShowSelectionRing(Vector3 worldPosition, float radius)
        {
            if (ring == null)
            {
                ringGo = new GameObject("ArSelectionRing");
                ring = ringGo.AddComponent<LineRenderer>();
                ring.useWorldSpace = true;
                ring.loop = true;
                ring.positionCount = 48;
                ring.startWidth = 0.012f;
                ring.endWidth = 0.012f;
                Material material = KvRenderPipelineShaders.CreateUnlit(
                    "Материал_выбора_AR", new Color(1f, 0.85f, 0.2f, 1f));
                if (material != null) ring.material = material;
            }
            ringGo.SetActive(true);
            ring.startColor = new Color(1f, 0.85f, 0.2f, 1f);
            ring.endColor = ring.startColor;
            Vector3 center = new Vector3(worldPosition.x, worldPosition.y + 0.02f, worldPosition.z);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        /// <summary>СБРОСИТЬ ВЫБОР ОБЪЕКТА (очистка рабочего пространства).</summary>
        public void ClearSelection()
        {
            SelectedLabel = string.Empty;
            selectedObject = null;
            if (ringGo != null) ringGo.SetActive(false);
            if (ui != null) ui.HideHint();
            Debug.Log("[KvAr] ВЫБОР: сброшен (очистка рабочего пространства)");
        }

        /// <summary>
        /// СБРОС УСТАРЕВШЕГО ВЫБОРА (07.11.2026). Дефект планшета: подсказка показывала
        /// «Выбрано: робот «Robot»», когда робота в мире уже не было (удалён/очищен), — строка
        /// врала. Если выбранный объект уничтожен, выбор снимается сам.
        /// </summary>
        public bool ClearSelectionIfGone()
        {
            if (string.IsNullOrEmpty(SelectedLabel)) return false;
            if (selectedObject != null) return false;
            Debug.Log("[KvAr] ВЫБОР: сброшен — выбранного объекта больше нет в сцене");
            ClearSelection();
            return true;
        }

        /// <summary>СБРОС ВИДА: угол обзора к базовому, поворот мира к позе якоря.</summary>
        public void ResetView()
        {
            if (bridge != null) bridge.ResetView();
            if (world != null) world.ResetViewRotation();
            if (ui != null) ui.SetHint("Вид сброшен: масштаб 1.00, поворот по якорю");
        }

        // ================================================================ СЛУЖЕБНОЕ

        public Ray ScreenRay(Vector2 screenPoint)
        {
            if (arCamera != null) return arCamera.ScreenPointToRay(screenPoint);
            return new Ray(Vector3.zero, Vector3.forward);
        }

        /// <summary>ПАЛЕЦ НАД ЭКРАННЫМ ИНТЕРФЕЙСОМ? Тогда мир не трогаем (кнопка нажимается uGUI).</summary>
        private bool IsOverUi(int pointerId)
        {
            if (EventSystem.current == null) return false;
            if (pointerId >= 0 && EventSystem.current.IsPointerOverGameObject(pointerId)) return true;
            return EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>Строка-доказательство для журнала устройства: что сделано и в каком режиме.</summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("касания: тапов ").Append(TapCount);
            sb.Append(" · меню ").Append(MenuPressCount);
            sb.Append(" · выбор ").Append(SelectCount);
            sb.Append(" · поворот вида ").Append(ViewRotateCount);
            sb.Append(" · щипок ").Append(PinchCount);
            sb.Append(" · режим точки ").Append(MovePointMode ? "вкл" : "выкл");
            if (!string.IsNullOrEmpty(SelectedLabel)) sb.Append(" · выбрано: ").Append(SelectedLabel);
            return sb.ToString();
        }
    }
}
