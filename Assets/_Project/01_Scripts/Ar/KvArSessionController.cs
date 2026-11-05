using System;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace KazistovVvAr
{
    /// <summary>
    /// СОСТОЯНИЕ AR-СЕССИИ И ЧЕСТНЫЕ СООБЩЕНИЯ ОБ ОТКАЗЕ (блок Ж приёмки).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КОМПОНЕНТ. У AR Foundation состояния не «включено/выключено», а лестница
    /// (`ARSessionState`): `Unsupported` — устройство не умеет ARCore; `NeedsInstall` — не
    /// установлены «Google Play Services for AR»; `CheckingAvailability`, `Installing`,
    /// `Ready`, `SessionInitializing`, `SessionTracking`. Каждое состояние требует РАЗНОГО
    /// действия оператора. Если свести их к «чёрному экрану», понять причину на устройстве
    /// невозможно — именно этого промт и запрещает (Ж1/Ж2: «понятное сообщение, не падает»).
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ. Разрешение камеры — не состояние сессии: отдельного `ARSessionState` для
    /// него нет, поэтому камера проверяется по `ARCameraManager.permissionGranted` и по причине
    /// «камера недоступна» (`NotTrackingReason.CameraUnavailable`).
    ///
    /// ТОЛЬКО ЧТЕНИЕ. Компонент ничего не двигает и не создаёт якорей: он сообщает и предлагает
    /// установку ARCore (кнопка), когда это возможно.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArSessionController : MonoBehaviour
    {
        /// <summary>AR-сессия сцены (единственная).</summary>
        public ARSession session;

        /// <summary>Менеджер камеры — по нему проверяется разрешение.</summary>
        public ARCameraManager cameraManager;

        /// <summary>Экранный интерфейс (сообщения и кнопка установки).</summary>
        public KvArUi ui;

        /// <summary>Как часто перечитывать причину «не отслеживается», секунды.</summary>
        public float notTrackingPollSeconds = 1f;

        /// <summary>Писать в журнал переходы состояний.</summary>
        public bool logOnChange = true;

        /// <summary>
        /// СКОЛЬКО ЖДАТЬ «ИНИЦИАЛИЗАЦИЮ» ПЕРЕД СБРОСОМ СЕССИИ, секунды.
        ///
        /// ЗАЧЕМ. На планшете 04.11.2026 ARCore-сессия застряла в `SessionInitializing`:
        /// камера не открывалась, плоскостей 0, а приложение работало. Сброс сессии — штатный
        /// API AR Foundation (`ARSession.Reset`), который перезапускает подсистему. Без него
        /// оператор видит чёрный фон и не понимает, что делать.
        /// </summary>
        public float stuckResetSeconds = 20f;

        /// <summary>Сколько автоматических сбросов допустимо (потом — только сообщение).</summary>
        public int maxAutomaticResets = 1;

        /// <summary>Текущее состояние сессии (для журнала и строки состояния).</summary>
        public ARSessionState State { get; private set; }

        /// <summary>Разрешение камеры выдано.</summary>
        public bool CameraPermissionGranted { get; private set; }

        /// <summary>Сессия готова: идёт отслеживание.</summary>
        public bool Ready
        {
            get
            {
                return State == ARSessionState.SessionTracking ||
                       State == ARSessionState.SessionInitializing;
            }
        }

        private ARSessionState lastLoggedState = (ARSessionState)(-1);
        private NotTrackingReason lastNotTracking = (NotTrackingReason)(-1);
        private float nextPoll;
        private bool cameraMessageShown;
        private bool unsupportedLogged;

        void OnEnable()
        {
            ARSession.stateChanged += OnSessionStateChanged;
        }

        void OnDisable()
        {
            ARSession.stateChanged -= OnSessionStateChanged;
        }

        void Start()
        {
            PollOnce();
            ApplySessionState(ARSession.state);
        }

        void Update()
        {
            if (Time.unscaledTime < nextPoll) return;
            nextPoll = Time.unscaledTime + Mathf.Max(0.25f, notTrackingPollSeconds);
            PollOnce();
            ApplyCameraPermission();
            CheckStuckInitialization();
        }

        /// <summary>
        /// СТОРОЖ «ЗАСТРЯВШЕЙ» ИНИЦИАЛИЗАЦИИ. Если сессия дольше `stuckResetSeconds` остаётся
        /// в `SessionInitializing` — сбрасываем её (`ARSession.Reset`). Это лечит случай, когда
        /// подсистема стартовала, но камера/трекинг не поднялись: без сброса приложение
        /// показывает чёрный фон и не находит плоскостей.
        /// </summary>
        private void CheckStuckInitialization()
        {
            if (State == ARSessionState.SessionInitializing)
            {
                if (initializingSince < 0f) initializingSince = Time.unscaledTime;
                else if (Time.unscaledTime - initializingSince >= Mathf.Max(2f, stuckResetSeconds))
                {
                    initializingSince = -1f;
                    if (automaticResets < Mathf.Max(1, maxAutomaticResets))
                    {
                        automaticResets++;
                        Debug.LogWarning("[KvAr] СЕССИЯ: инициализация дольше " +
                                         stuckResetSeconds.ToString("0") + " с — сброс сессии " +
                                         "(попытка " + automaticResets + " из " + maxAutomaticResets + ")");
                        if (session != null) session.Reset();
                    }
                }
                return;
            }

            initializingSince = -1f;
            if (State == ARSessionState.SessionTracking) automaticResets = 0;
        }

        private float initializingSince = -1f;
        private int automaticResets;

        /// <summary>ПЕРЕЧИТАТЬ ФАКТЫ: состояние сессии и разрешение камеры.</summary>
        public void PollOnce()
        {
            State = ARSession.state;
            CameraPermissionGranted = cameraManager != null && cameraManager.permissionGranted;
        }

        private void OnSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            PollOnce();
            ApplySessionState(args.state);
        }

        /// <summary>ЛОГИКА РЕАКЦИИ НА СОСТОЯНИЕ: сообщение + журнал. Вынесена из события для теста.</summary>
        public void ApplySessionState(ARSessionState state)
        {
            State = state;

            if (logOnChange && state != lastLoggedState)
            {
                lastLoggedState = state;
                Debug.Log("[KvAr] СЕССИЯ: " + DescribeState(state) +
                          " · разрешение камеры: " + (CameraPermissionGranted ? "есть" : "НЕТ"));
            }

            string message = MessageFor(state);
            if (state == ARSessionState.Unsupported)
            {
                if (!unsupportedLogged)
                {
                    unsupportedLogged = true;
                    Debug.LogError("[KvAr] ОТКАЗ Ж1: устройство не поддерживает ARCore");
                }
                ShowUiMessage(message, null, null);
                return;
            }

            if (state == ARSessionState.NeedsInstall)
            {
                Debug.Log("[KvAr] ARCore не установлен: предлагаю установить «Google Play Services for AR»");
                ShowUiMessage(message, "Установить ARCore", TryInstallArCore);
                return;
            }

            // Работающее состояние: любые прежние отказы снимаем.
            if (ui != null) ui.HideMessage();
            cameraMessageShown = false;

            if (state == ARSessionState.Ready || state == ARSessionState.SessionTracking ||
                state == ARSessionState.SessionInitializing)
            {
                return;
            }
        }

        /// <summary>ПРОВЕРКА РАЗРЕШЕНИЯ КАМЕРЫ (Ж2): без него ARCore не начнёт отслеживание.</summary>
        private void ApplyCameraPermission()
        {
            PollOnce();
            NotTrackingReason reason = ARSession.notTrackingReason;

            bool cameraUnavailable = !CameraPermissionGranted ||
                                     reason == NotTrackingReason.CameraUnavailable;
            if (cameraUnavailable)
            {
                if (!cameraMessageShown)
                {
                    cameraMessageShown = true;
                    Debug.LogError("[KvAr] ОТКАЗ Ж2: камера недоступна (" +
                                   (CameraPermissionGranted
                                       ? "разрешение есть, но трекинг сообщает CameraUnavailable"
                                       : "разрешение не выдано") + ")");
                }
                ShowUiMessage(
                    "Нет доступа к камере.\n\n" +
                    "AR без камеры невозможен. Разрешите доступ к камере в настройках " +
                    "приложения и вернитесь в него.",
                    "Повторить", ClearCameraMessage);
                return;
            }

            if (cameraMessageShown)
            {
                cameraMessageShown = false;
                if (ui != null) ui.HideMessage();
                Debug.Log("[KvAr] камера доступна: сообщение об отказе снято");
            }

            if (reason != lastNotTracking)
            {
                lastNotTracking = reason;
                if (reason != NotTrackingReason.None)
                    Debug.Log("[KvAr] ТРЕКИНГ: " + DescribeNotTracking(reason));
                else
                    Debug.Log("[KvAr] ТРЕКИНГ: восстановлен");
            }
        }

        /// <summary>Скрыть сообщение камеры (кнопка «Повторить»).</summary>
        public void ClearCameraMessage()
        {
            cameraMessageShown = false;
            if (ui != null) ui.HideMessage();
        }

        /// <summary>
        /// ПОПЫТАТЬСЯ УСТАНОВИТЬ ARCORE. `ARSession.Install()` бросает исключение в состояниях
        /// `None` и `Unsupported` — это документированное поведение, ловим и сообщаем честно.
        /// </summary>
        public void TryInstallArCore()
        {
            try
            {
                Debug.Log("[KvAr] установка «Google Play Services for AR»: запрос…");
                ARSession.Install();
            }
            catch (InvalidOperationException error)
            {
                Debug.LogError("[KvAr] установка ARCore невозможна: " + error.Message);
                if (ui != null)
                    ui.ShowMessage("Установить ARCore не удалось.\n\n" +
                                   "Откройте Google Play и установите «Google Play Services for AR» вручную.");
            }
            catch (Exception error)
            {
                Debug.LogError("[KvAr] установка ARCore: ошибка " + error.GetType().Name + ": " + error.Message);
            }
        }

        private void ShowUiMessage(string text, string buttonLabel, Action action)
        {
            if (ui == null || string.IsNullOrEmpty(text)) return;
            ui.ShowMessage(text, buttonLabel, action);
        }

        // ---------------------------------------------------------------- чистые функции (тесты)

        /// <summary>СООБЩЕНИЕ ДЛЯ СОСТОЯНИЯ (null — сообщения не нужно).</summary>
        public static string MessageFor(ARSessionState state)
        {
            switch (state)
            {
                case ARSessionState.Unsupported:
                    return "Это устройство не поддерживает дополненную реальность (ARCore).\n\n" +
                           "Нужен телефон или планшет Android с поддержкой ARCore. " +
                           "Приложение продолжает работать и не падает.";
                case ARSessionState.NeedsInstall:
                    return "На устройстве нет «Google Play Services for AR» — без него AR не запустится.\n\n" +
                           "Нажмите «Установить ARCore» и разрешите установку из Google Play.";
                case ARSessionState.CheckingAvailability:
                    return "Проверяю, поддерживает ли устройство дополненную реальность…";
                case ARSessionState.Installing:
                    return "Устанавливаются «Google Play Services for AR»…";
                case ARSessionState.SessionInitializing:
                    return null;
                case ARSessionState.SessionTracking:
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>ГОТОВА ЛИ СЕССИЯ К РАБОТЕ (трекинг или его инициализация).</summary>
        public static bool IsReadyState(ARSessionState state)
        {
            return state == ARSessionState.Ready ||
                   state == ARSessionState.SessionInitializing ||
                   state == ARSessionState.SessionTracking;
        }

        /// <summary>ЭТО ОТКАЗ, А НЕ ОЖИДАНИЕ (для журнала и строки состояния).</summary>
        public static bool IsFailureState(ARSessionState state)
        {
            return state == ARSessionState.Unsupported || state == ARSessionState.None;
        }

        /// <summary>Строка журнала о состоянии сессии.</summary>
        public static string DescribeState(ARSessionState state)
        {
            switch (state)
            {
                case ARSessionState.None: return "нет подсистемы AR (None)";
                case ARSessionState.Unsupported: return "устройство не поддерживает AR (Unsupported)";
                case ARSessionState.CheckingAvailability: return "проверка доступности AR";
                case ARSessionState.NeedsInstall: return "нужна установка «Google Play Services for AR»";
                case ARSessionState.Installing: return "установка ARCore идёт";
                case ARSessionState.Ready: return "AR готов";
                case ARSessionState.SessionInitializing: return "сессия инициализируется";
                case ARSessionState.SessionTracking: return "идёт отслеживание (SessionTracking)";
                default: return "неизвестное состояние " + (int)state;
            }
        }

        /// <summary>Строка журнала о причине «не отслеживается» — что оператору делать.</summary>
        public static string DescribeNotTracking(NotTrackingReason reason)
        {
            switch (reason)
            {
                case NotTrackingReason.None: return "причина не указана";
                case NotTrackingReason.Initializing: return "трекинг инициализируется — подождите";
                case NotTrackingReason.Relocalizing: return "восстановление положения — поводите камерой";
                case NotTrackingReason.InsufficientLight: return "мало света — включите освещение";
                case NotTrackingReason.InsufficientFeatures: return "мало деталей в кадре — водите камерой по столу";
                case NotTrackingReason.ExcessiveMotion: return "слишком быстрое движение — двигайте камеру плавнее";
                case NotTrackingReason.Unsupported: return "устройство не поддерживает трекинг";
                case NotTrackingReason.CameraUnavailable: return "камера недоступна";
                default: return "причина " + (int)reason;
            }
        }
    }
}
