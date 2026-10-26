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

            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;

            DetectXr();
        }

        void Start()
        {
            if (!logOnStart) return;
            Debug.Log("[KvXr] " + Status);
            Debug.Log("[KvXr] " + KvXrInputSource.Describe());
            Debug.Log("[KvXr] " + KvXrSettings.BuildRigConfig().Describe());
        }

        /// <summary>
        /// ПОКА ИДУТ ПЕРВЫЕ СЕКУНДЫ РАБОТЫ — ПЕЧАТАТЬ СОСТАВ ВВОДА. Шлем надевают уже после
        /// запуска приложения, и контроллеры появляются в системе не сразу: одиночная строка
        /// в `Start()` не отвечает на вопрос «контроллеров нет — это дефект или устройство спит».
        /// Диагностика сама ограничена по времени и числу строк, спама в журнале не будет.
        /// </summary>
        void Update()
        {
            KvXrInputSource.TickDiagnostics();
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
