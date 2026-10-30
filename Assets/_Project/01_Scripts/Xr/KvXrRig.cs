using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// КАМЕРА ШЛЕМА (этап 1 плана XR, сессия §28).
    ///
    /// ЧТО ДЕЛАЕТ. Ставит камеру в правильное место и сообщает о состоянии XR одной строкой.
    /// Позу камеры в шлеме выставляет САМ РАНТАЙМ OpenXR (система трекинга пишет в
    /// `Camera.transform`) — здесь ничего не переписывается: попытка «двигать камеру руками»
    /// в VR даёт тряску и «плывущую» картинку.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ ПРИ СТАРТЕ и попадает в журнал:
    ///   • активен ли OpenXR (без него в шлеме чёрный экран — и это первая причина, которую надо исключить);
    ///   • включено ли стерео SinglePassInstanced (иначе картинка для двух глаз считается дважды
    ///     и частота кадров падает почти вдвое);
    ///   • обнаружен ли шлем и контроллеры.
    ///
    /// ПОЧЕМУ НЕ XROrigin ИЗ XRI. `XROrigin` тянет за собой соглашение о префабе рига и
    /// компоненты XRI. Здесь риг собран из трёх собственных компонентов, и это осознанный
    /// минимум: меньше узлов — меньше того, что нельзя проверить без устройства.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrRig : MonoBehaviour
    {
        /// <summary>Камера шлема (обычно этот же объект).</summary>
        public Camera headCamera;

        /// <summary>Сообщать состояние в консоль при старте (в сборке — один раз, без спама).</summary>
        public bool logOnStart = true;

        /// <summary>Целевая частота кадров для шлема: PICO 4 Ultra работает на 72/90 Гц.</summary>
        public int targetFrameRate = 72;

        /// <summary>Резервный масштаб разрешения глаза при низком пресете (1 — без изменений).</summary>
        public float eyeTextureResolutionScale = 1f;

        /// <summary>OpenXR найден и активен.</summary>
        public bool OpenXrActive { get; private set; }

        /// <summary>Стерео включено в режиме SinglePassInstanced.</summary>
        public bool SinglePassInstanced { get; private set; }

        /// <summary>Строка состояния для журнала (та же, что попадает в отчёт).</summary>
        public string Status { get; private set; }

        void Awake()
        {
            if (headCamera == null) headCamera = GetComponent<Camera>();
            if (headCamera == null) headCamera = GetComponentInChildren<Camera>();

            // 29.10.2026: начало отслеживания = этот риг. Без этого позы трекинга
            // не трансформируются в мировые — лучи бьют мимо, руки не едут с камерой.
            KvXrInputSource.TrackingOrigin = transform;
            installedTrackingOrigin = true;          // 30.10.2026: чтобы OnDestroy снял именно свою ссылку

            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;

            DetectXr();
        }

        void Start()
        {
            // 30.10.2026 (S44): РЕЖИМ РАБОТЫ ОПРЕДЕЛЯЕТСЯ ЗДЕСЬ И ОДИН РАЗ.
            //
            // До этого приложение всегда строило виртуальную лабораторию, а сквозной вид считался
            // украшением. Для MR/AR это неверно: у них другие опоры, другой масштаб и другие
            // состояния. Режим вычисляется по ФАКТИЧЕСКИМ возможностям устройства (а не по названию
            // шлема: оператор мог выключить сквозной вид в настройках) и печатается одной строкой.
            // Если возможностей MR нет — приложение честно становится VR и говорит это, а не
            // показывает серый экран.
            KvXrCapabilities.Answer passthrough = KvXrCapabilities.Passthrough();
            KvXrCapabilities.Answer anchors = KvXrCapabilities.SpatialAnchors();
            KvXrCapabilities.Answer camera = KvXrCapabilities.Camera();

            KvXrPlatformModeSelector.Apply(
                passthroughAvailable: KvXrCapabilities.IsYes(passthrough),
                spatialAnchorsAvailable: KvXrCapabilities.IsYes(anchors),
                cameraAvailable: KvXrCapabilities.IsYes(camera));

            // СТРОКА О ВОЗМОЖНОСТЯХ — ОТДЕЛЬНО ОТ СТРОКИ О РЕЖИМЕ (30.10.2026).
            //
            // Разница важна наладчику: «режим VR» может значить и «устройство не умеет», и
            // «мы не смогли спросить». Без этой строки обе ситуации выглядели бы одинаково.
            Debug.Log("[KvXr] " + KvXrCapabilities.Describe(passthrough, anchors, camera));

            // §37: ЧТО ЖИВЁТ ТОЛЬКО В РАНТАЙМЕ. Модели контроллеров и исполнитель жестов создаются
            // ЗДЕСЬ, а не лежат в сцене: файл XrMain.unity не правится вовсе, поэтому принятое в
            // §35–§36 не может сломаться от правки сцены. Оба вызова идемпотентны.
            EnsureRuntimeComponents();

            if (!logOnStart) return;
            Debug.Log("[KvXr] " + Status);
            Debug.Log("[KvXr] " + KvXrInputSource.Describe());
            Debug.Log("[KvXr] " + KvXrSettings.BuildRigConfig().Describe());
            Debug.Log("[KvXr] " + KvXrSettings.DescribePointer());
        }

        /// <summary>
        /// СОЗДАТЬ ТО, ЧТО НЕ ЛЕЖИТ В СЦЕНЕ (§37): модели контроллеров, кисти и жесты. Вынесено
        /// отдельным методом, чтобы это было видно в одном месте: сцена не меняется, всё собирается кодом.
        /// </summary>
        private void EnsureRuntimeComponents()
        {
            Camera camera = headCamera != null ? headCamera : Camera.main;
            KvXrControllerVisuals.Ensure(transform, camera);
            KvXrHands.Ensure(transform, camera);
            KvXrGestures.Ensure(transform);
            KvXrSecondaryRay.Ensure(transform, camera);   // §39: второй луч — только указатель

            // §39: курсору на конце луча нужна камера, чтобы разворачиваться к оператору.
            KvXrUiPointer pointer = Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null && pointer.headCamera == null) pointer.headCamera = camera;

            // §41: СТАРТОВОЕ МЕНЮ перед оператором (указание владельца: «нет стартового меню —
            // сначала сделаем его»). Создаётся в рантайме, сцена не меняется.
            KvXrStartMenu.Ensure(transform, camera);

            // §41: ОКНА В МИРЕ (указание владельца: кнопки должны выдавать окна, которые не крепятся
            // ко взгляду и остаются висеть, когда оператор отходит).
            KvXrWindowHost.Ensure(camera);

            // §42: ПУСТО ПРИ ЗАПУСКЕ — столы и роботы сцены гасятся в рантайме (файл XrMain.unity не
            // меняется), добавляются они уже из стартового меню.
            EnsureSceneEmpty();

            // §42: ПОТОК ЭТАПОВ — без него в шлеме не работала ни одна визуализация (пятна, границы,
            // зоны, сингулярности): службы привязываются к потоку, а его в XR-сцене не было вовсе.
            EnsureFlowBridge(camera);
        }

        /// <summary>Создать компонент «пусто при запуске», если его нет (§42).</summary>
        private static void EnsureSceneEmpty()
        {
            KvXrSceneEmpty existing = Object.FindAnyObjectByType<KvXrSceneEmpty>(FindObjectsInactive.Include);
            if (existing != null) return;

            var go = new GameObject("KvXrSceneEmpty");
            go.AddComponent<KvXrSceneEmpty>();
        }

        /// <summary>Создать мост потока этапов, если его нет (§42).</summary>
        private void EnsureFlowBridge(Camera camera)
        {
            KvXrFlowBridge existing = Object.FindAnyObjectByType<KvXrFlowBridge>(FindObjectsInactive.Include);
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return;
            }

            var go = new GameObject("KvXrFlowBridge");
            KvXrFlowBridge bridge = go.AddComponent<KvXrFlowBridge>();
            bridge.headCamera = camera;
        }

        private float lastRecoverTime = -100f;

        /// <summary>
        /// 30.10.2026: рига больше нет — начало отслеживания обязано быть снято.
        ///
        /// ПОЧЕМУ. `KvXrInputSource.TrackingOrigin` — статическое поле, и через него проходят
        /// ВСЕ позы трекинга (руки, контроллеры, лучи). Если риг пересоздан или выгружен, а поле
        /// осталось, трансформация идёт через уничтоженный объект — это `MissingReferenceException`
        /// каждый кадр, а не «позы не едут». Снимаем ссылку только если она наша: другой риг
        /// мог уже поставить свою.
        ///
        /// ПОЧЕМУ ФЛАГ, А НЕ СРАВНЕНИЕ. У уничтожаемого объекта Unity подменяет результат
        /// сравнения (`obj == null` — истина), поэтому сравнение с `transform` в `OnDestroy`
        /// может не сработать: прогон 30.10.2026 показал, что ссылка оставалась. Флаг ставится
        /// в `Awake` — там риг точно тот самый.
        /// </summary>
        private bool installedTrackingOrigin;

        void OnDestroy()
        {
            KvXrInputSource.ClearOriginIf(installedTrackingOrigin, transform);
            installedTrackingOrigin = false;
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused) Recover();
        }

        void OnApplicationFocus(bool focused)
        {
            if (focused) Recover();
        }

        /// <summary>
        /// ВОССТАНОВЛЕНИЕ ПОСЛЕ ПРОБУЖДЕНИЯ ШЛЕМА (§37). Жалоба владельца: «после ухода в спящий
        /// режим указка на контроллерах не восстанавливается и веер тоже». Сон останавливает
        /// приложение и может пересоздать сеанс, устройства и подсистемы — всё, что было закэшировано
        /// (список устройств, закреплённые стороны, подсистема кистей, состояние указателя,
        /// положение панели), к этому моменту может быть негодным.
        ///
        /// ЧТО ЗДЕСЬ ДЕЛАЕТСЯ: пересканировать ввод, снять закрепление сторон (пусть определятся
        /// заново), поднять подсистему кистей, сбросить жесты и указатель, поставить панель заново —
        /// и напечатать ОДНУ строку с числами, по которой видно, что восстановление произошло.
        /// </summary>
        public void Recover()
        {
            if (Time.realtimeSinceStartup - lastRecoverTime < 1f) return;   // пауза и фокус приходят парой
            lastRecoverTime = Time.realtimeSinceStartup;

            KvXrInputSource.ForceScan();
            KvXrInputSource.UnlockSides();

            KvXrHands hands = Object.FindAnyObjectByType<KvXrHands>();
            if (hands != null) hands.Recover();

            KvXrGestures gestures = Object.FindAnyObjectByType<KvXrGestures>();
            if (gestures != null) gestures.ResetState();

            KvXrGroupPanel panel = Object.FindAnyObjectByType<KvXrGroupPanel>();
            if (panel != null) panel.Reanchor();

            KvXrUiPointer pointer = Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null) pointer.ResetState();

            DetectXr();
            Debug.Log("[KvXr] ПРОБУЖДЕНИЕ: устройства пересмотрены · " + KvXrInputSource.Describe() +
                      " · " + KvXrInputSource.DescribeSides() +
                      " · кисти " + (KvXrHands.SubsystemRunning ? "подняты" : "НЕТ") +
                      " · панель и указатель поставлены заново");
        }

        /// <summary>
        /// ПОКА ИДУТ ПЕРВЫЕ СЕКУНДЫ РАБОТЫ — ПЕЧАТАТЬ СОСТАВ ВВОДА. Шлем надевают уже после
        /// запуска приложения, и контроллеры появляются в системе не сразу: одиночная строка
        /// в `Start()` не отвечает на вопрос «контроллеров нет — это дефект или устройство спит».
        /// Диагностика сама ограничена по времени и числу строк, спама в журнале не будет.
        /// </summary>
        void Update()
        {
            // §40: РЕЖИМ УПРАВЛЕНИЯ — контроллеры в руках или кисти. От него зависят луч (контроллер
            // или палец), крепление панели (контроллер или ладонь) и видимость второго луча.
            KvXrMode.Tick();

            // §37: ФАЙЛ ПОДСТРОЙКИ. Читается не чаще раза в 2 с; если значения изменились —
            // в журнал уходит строка с числами. Это механизм опросов радиуса панели без пересборки.
            string tuning = KvXrTuning.Tick();
            if (!string.IsNullOrEmpty(tuning)) Debug.Log(tuning);

            KvXrInputSource.TickDiagnostics();

            // СТАНДАРТ Quest/PICO (27.10.2026): кнопки A/B/X/Y — подтвердить/назад/меню/панель.
            KvXrActions.Tick();

            // СТРОКИ ОБ OPENXR И ВВОДЕ (сессия §36): выдано ли расширение контроллеров,
            // не нарушено ли правило привязок по рукам, сколько устройств видит XR-подсистема.
            // Без этих строк «контроллеров нет» остаётся догадкой — ровно как было в §35.
            KvXrOpenXrDiagnostics.Tick();
        }

        /// <summary>
        /// Определить состояние XR-подсистемы. Проверки сделаны ЧЕРЕЗ ОТРАЖЕНИЕ и
        /// `XRDisplaySubsystem`, без прямых ссылок на типы OpenXR: иначе сборка зависела бы
        /// от версии пакета, а проект этого избегает (тем же способом сделан `KvFoveatedRendering`).
        /// </summary>
        public void DetectXr()
        {
            bool stereo = false;
            string stereoMode = "нет XR";

            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(displays);

            for (int i = 0; i < displays.Count; i++)
            {
                var d = displays[i];
                if (d == null || !d.running) continue;
                stereo = true;
                stereoMode = "XRDisplaySubsystem активна";
                break;
            }

            // Признак OpenXR — активный лоадер XR Management: подсистема отображения может
            // стартовать позже, а лоадер известен сразу. Проверка идёт по имени типа, без
            // прямой ссылки на сборку OpenXR (так же сделан `KvFoveatedRendering`).
            OpenXrActive = false;
            var settings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (settings != null && settings.Manager != null)
            {
                var loaders = settings.Manager.activeLoaders;
                if (loaders != null)
                {
                    for (int i = 0; i < loaders.Count; i++)
                    {
                        var loader = loaders[i];
                        if (loader == null) continue;
                        string type = loader.GetType().FullName ?? string.Empty;
                        if (type.IndexOf("OpenXR", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            OpenXrActive = true;
                            break;
                        }
                    }
                }
            }

            SinglePassInstanced = stereo;
            Status = string.Format(
                "Камера шлема: OpenXR {0} · стерео {1} ({2}) · цель {3} кадров/с · шлем {4}",
                OpenXrActive ? "активен" : "НЕ АКТИВЕН",
                SinglePassInstanced ? "включено" : "выключено",
                stereoMode,
                targetFrameRate,
                KvXrInputSource.HeadsetPresent ? "обнаружен" : "не обнаружен");
        }

        /// <summary>
        /// Резервный масштаб разрешения глаза. В HDRP на слабом GPU это главный рычаг
        /// производительности в шлеме; значение ограничено, чтобы картинка не превратилась
        /// в кашу (ниже 0,6 читать интерфейс уже нельзя).
        /// </summary>
        public void SetEyeResolutionScale(float scale)
        {
            eyeTextureResolutionScale = Mathf.Clamp(scale, 0.6f, 1.4f);

            var displays = new System.Collections.Generic.List<UnityEngine.XR.XRDisplaySubsystem>();
            UnityEngine.SubsystemManager.GetSubsystems(displays);
            for (int i = 0; i < displays.Count; i++)
            {
                var d = displays[i];
                if (d == null || !d.running) continue;
                d.scaleOfAllRenderTargets = eyeTextureResolutionScale;
            }
        }
    }
}
