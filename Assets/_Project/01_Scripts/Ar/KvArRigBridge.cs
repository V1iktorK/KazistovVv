using UnityEngine;

namespace KazistovVvAr
{
    /// <summary>
    /// МОСТ МЕЖДУ AR-КАМЕРОЙ И «ГОЛОВОЙ» XR-СТЕКА.
    ///
    /// ЗАЧЕМ. Весь интерфейс шлема (панель команд, стартовое меню, окна, лучи) уже умеет
    /// работать от объекта «голова» — `XrCamera`. В AR голова — это задняя камера планшета,
    /// которой управляет AR Foundation: её позу задаёт трекинг, и записывать её из игры нельзя
    /// (иначе получится «двойное управление»: камера дрожит и уезжает).
    ///
    /// ЧТО ДЕЛАЕТ ЭТОТ КОМПОНЕНТ. Один раз в кадр копирует МИРОВУЮ позу AR-камеры в логическую
    /// `XrCamera` (её собственная `Camera` выключена — рендерит только AR-камера). Все прежние
    /// компоненты XR при этом не меняются ни на строку: они по-прежнему смотрят на «голову» и
    /// получают настоящую позу устройства.
    ///
    /// ПОЧЕМУ НЕ ПРИВЯЗЫВАЕМ КАМЕРУ К ЯКОРЮ. Запрет S3.1: объект, привязанный к камере, «плывёт»
    /// за ней. Здесь наоборот: движется лишь невидимая логическая голова интерфейса, а весь
    /// виртуальный мир привязан к `ARAnchor` и от камеры не зависит.
    ///
    /// МАСШТАБ ВИДА (пункт Г6 приёмки). Щипок двумя пальцами меняет УГОЛ ОБЗОРА камеры, а не
    /// масштаб робота: `fieldOfView` — это объектив. Фон камеры и виртуальные объекты рисуются
    /// одной проекцией, поэтому они остаются совмещены, а робот сохраняет паспортный размер 1:1.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArRigBridge : MonoBehaviour
    {
        /// <summary>AR-камера (её позой владеет AR Foundation).</summary>
        public Camera arCamera;

        /// <summary>Логическая «голова» XR-стека (рендер выключен, поза копируется из AR).</summary>
        public Camera xrCamera;

        /// <summary>Писать в журнал о привязке и смене масштаба вида.</summary>
        public bool logOnStart = true;

        /// <summary>Минимальный угол обзора при «приближении вида» (градусы).</summary>
        public float minFieldOfView = 20f;

        /// <summary>Максимальный угол обзора при «отдалении вида» (градусы).</summary>
        public float maxFieldOfView = 80f;

        /// <summary>Базовый угол обзора — запоминается при старте (к нему возвращает «Сброс вида»).</summary>
        public float BaseFieldOfView { get; private set; }

        /// <summary>Текущий масштаб вида: 1 — как есть, больше — ближе.</summary>
        public float ViewZoom { get; private set; }

        /// <summary>Поза устройства (мировые координаты AR-сессии).</summary>
        public Pose DevicePose
        {
            get
            {
                if (arCamera == null) return new Pose(Vector3.zero, Quaternion.identity);
                Transform t = arCamera.transform;
                return new Pose(t.position, t.rotation);
            }
        }

        void Awake()
        {
            if (arCamera == null) arCamera = FindArCamera();
            if (xrCamera == null) xrCamera = FindLogicalHead();
            if (arCamera != null)
            {
                BaseFieldOfView = arCamera.fieldOfView;
                ViewZoom = 1f;
            }

            // ЛОГИЧЕСКАЯ ГОЛОВА НЕ ДОЛЖНА РЕНДЕРИТЬ: иначе поверх AR-фона появится второй слой
            // сцены (два разных вида, «двоение» и просадка кадров). Отключаем камеру, но объект
            // оставляем живым: его transform — это «голова» для интерфейса.
            if (xrCamera != null)
            {
                Camera logical = xrCamera.GetComponent<Camera>();
                if (logical != null && logical.enabled)
                {
                    logical.enabled = false;
                    if (logOnStart)
                        Debug.Log("[KvAr] ЛОГИЧЕСКАЯ ГОЛОВА " + logical.name +
                                  ": рендер выключен (рисует только AR-камера)");
                }
            }

            DisableDuplicateAudioListener();

            if (logOnStart)
                Debug.Log("[KvAr] МОСТ КАМЕРЫ: AR-камера «" +
                          (arCamera != null ? arCamera.name : "НЕТ") + "» → логическая голова «" +
                          (xrCamera != null ? xrCamera.name : "НЕТ") + "» · базовый угол обзора " +
                          (BaseFieldOfView > 0f
                              ? BaseFieldOfView.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)
                              : "—"));
        }

        /// <summary>
        /// КОПИРОВАТЬ ПОЗУ. `LateUpdate` — после движения камеры AR Foundation в этом же кадре,
        /// чтобы интерфейс не отставал на кадр (иначе панели «дрожат» при движении устройства).
        /// </summary>
        void LateUpdate()
        {
            if (arCamera == null || xrCamera == null) return;
            Transform source = arCamera.transform;
            xrCamera.transform.SetPositionAndRotation(source.position, source.rotation);
        }

        /// <summary>
        /// ПРИБЛИЗИТЬ/ОТДАЛИТЬ ВИД (щипок). Меняется ТОЛЬКО угол обзора камеры: масштаб робота,
        /// стола и мира остаётся 1:1 (пункт Г6 «НЕ масштаб робота»).
        /// </summary>
        public void ZoomView(float factor)
        {
            if (arCamera == null || factor <= 0f) return;
            if (BaseFieldOfView <= 0f) BaseFieldOfView = arCamera.fieldOfView > 0f ? arCamera.fieldOfView : 60f;

            float next = Mathf.Clamp(arCamera.fieldOfView / factor, minFieldOfView, maxFieldOfView);
            float zoom = BaseFieldOfView / Mathf.Max(0.01f, next);
            if (Mathf.Abs(zoom - ViewZoom) < 0.001f && Mathf.Abs(next - arCamera.fieldOfView) < 0.01f) return;
            arCamera.fieldOfView = next;
            ViewZoom = zoom;
            Debug.Log("[KvAr] ВИД: масштаб вида " +
                      ViewZoom.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture) +
                      " · угол обзора " +
                      next.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) +
                      "° (масштаб робота не менялся)");
        }

        /// <summary>СБРОСИТЬ ВИД: базовый угол обзора, масштаб вида 1.00.</summary>
        public void ResetView()
        {
            if (arCamera == null) return;
            float baseFov = BaseFieldOfView > 0f ? BaseFieldOfView : 60f;
            arCamera.fieldOfView = baseFov;
            ViewZoom = 1f;
            Debug.Log("[KvAr] ВИД: сброшен · угол обзора " +
                      baseFov.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + "°");
        }

        /// <summary>ЛУЧ ЧЕРЕЗ ТОЧКУ ЭКРАНА (по AR-камере, а не по логической голове).</summary>
        public Ray ScreenRay(Vector2 screenPoint)
        {
            if (arCamera != null) return arCamera.ScreenPointToRay(screenPoint);
            return new Ray(Vector3.zero, Vector3.forward);
        }

        /// <summary>Найти AR-камеру, если ссылка не задана: камера с `ARCameraManager` в сцене.</summary>
        public static Camera FindArCamera()
        {
            UnityEngine.XR.ARFoundation.ARCameraManager[] managers =
                UnityEngine.Object.FindObjectsByType<UnityEngine.XR.ARFoundation.ARCameraManager>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] == null) continue;
                Camera camera = managers[i].GetComponent<Camera>();
                if (camera != null) return camera;
            }
            return Camera.main;
        }

        /// <summary>
        /// ЛОГИЧЕСКАЯ ГОЛОВА — объект `XrCamera` (камера, которой пользуется интерфейс XR).
        ///
        /// ПОРЯДОК ПОИСКА: сначала `KvXrRig` (он всегда остаётся включённым и хранит ссылку
        /// `headCamera`), затем `KvXrHeadTracking` (в AR-сцене он ВЫКЛЮЧЕН, поэтому компонент
        /// может не попасться), затем объект с именем `XrCamera`. Три пути, потому что цена
        /// ошибки — «интерфейс не следует за устройством» и полностью нерабочий AR.
        /// </summary>
        public static Camera FindLogicalHead()
        {
            KazistovVvXr.KvXrRig[] rigs = UnityEngine.Object.FindObjectsByType<KazistovVvXr.KvXrRig>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < rigs.Length; i++)
            {
                if (rigs[i] == null) continue;
                if (rigs[i].headCamera != null) return rigs[i].headCamera;
                Camera onRig = rigs[i].GetComponent<Camera>();
                if (onRig != null) return onRig;
                Camera inRig = rigs[i].GetComponentInChildren<Camera>(true);
                if (inRig != null) return inRig;
            }

            KazistovVvXr.KvXrHeadTracking[] trackers =
                UnityEngine.Object.FindObjectsByType<KazistovVvXr.KvXrHeadTracking>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < trackers.Length; i++)
            {
                if (trackers[i] == null) continue;
                Camera camera = trackers[i].GetComponent<Camera>();
                if (camera == null) camera = trackers[i].GetComponentInChildren<Camera>(true);
                if (camera != null) return camera;
            }

            GameObject named = GameObject.Find("XrCamera");
            if (named != null) return named.GetComponent<Camera>();
            return null;
        }

        /// <summary>
        /// ОДИН `AudioListener` НА СЦЕНУ. AR-камера получает слушателя, у логической головы он
        /// выключается: два слушателя дают предупреждение Unity и странный звук.
        /// </summary>
        private void DisableDuplicateAudioListener()
        {
            if (xrCamera == null || arCamera == null) return;
            AudioListener logical = xrCamera.GetComponent<AudioListener>();
            if (logical == null) return;
            AudioListener real = arCamera.GetComponent<AudioListener>();
            if (real == null) return;
            if (logical.enabled)
            {
                logical.enabled = false;
                if (logOnStart) Debug.Log("[KvAr] ЗВУК: слушатель логической головы выключен (слушает AR-камера)");
            }
        }

        /// <summary>
        /// ЧИСТАЯ ФУНКЦИЯ ДЛЯ ТЕСТА: как из позы AR-камеры получается поза логической головы.
        /// Именно она доказывает, что мост ничего не придумывает: поза = ровно поза AR-камеры.
        /// </summary>
        public static Pose HeadPoseFrom(Pose arCameraPose)
        {
            return arCameraPose;
        }

        /// <summary>ЧИСТАЯ ФУНКЦИЯ: угол обзора из масштаба вида (базовый/масштаб), с ограничениями.</summary>
        public static float FieldOfViewFor(float baseFieldOfView, float zoom, float minFov, float maxFov)
        {
            if (baseFieldOfView <= 0f) baseFieldOfView = 60f;
            if (zoom <= 0f) zoom = 1f;
            return Mathf.Clamp(baseFieldOfView / zoom, minFov, maxFov);
        }
    }
}
