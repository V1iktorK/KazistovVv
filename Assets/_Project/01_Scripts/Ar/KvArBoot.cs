using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Android;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using KazistovVvXr;

namespace KazistovVvAr
{
    /// <summary>
    /// ЗАГРУЗОЧНАЯ СЦЕНА: ОПРЕДЕЛИТЬ РЕЖИМ И ЗАГРУЗИТЬ НУЖНУЮ СЦЕНУ.
    ///
    /// ЗАЧЕМ ОТДЕЛЬНАЯ СЦЕНА. Приложение одно, а сцены две: `XrMain` (шлем, VR/MR — не
    /// трогается) и `ArMain` (планшет/телефон, AR). Решение принимается ПО ФАКТУ поднятого
    /// провайдера XR после того, как XR закончил инициализацию: на шлеме это OpenXR, на
    /// планшете — ARCore. Ни одной настройки перед запуском (S1.7).
    ///
    /// ПОЧЕМУ ЖДЁМ, А НЕ РЕШАЕМ В `Awake`. XR Management поднимает лоадер асинхронно, а на
    /// первом запуске шлем показывает системный диалог разрешений, и приложение на паузе.
    /// Решение в этот момент дало бы «провайдера нет» — и шлем навсегда остался бы без XR.
    /// Этот же дефект уже был в проекте (S45, MR) и лечится ожиданием готовности XR.
    ///
    /// ОТКАЗ (Ж1). Если не поднялось НИ ОДНО: показываем понятное сообщение и НЕ грузим
    /// игровую сцену. Приложение остаётся живым, ничего не падает.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArBoot : MonoBehaviour
    {
        /// <summary>Сколько ждать инициализацию XR, секунды. Хватает на диалог разрешений.</summary>
        // 90, а НЕ 30 (04.11.2026). На планшете первый по порядку OpenXR-лоадер PICO мог
        // занять десятки секунд, и 30-секундный запас истекал раньше, чем XR Management
        // успевал перейти к ARCore: приложение показывало «нет AR», хотя ARCore есть.
        public float xrInitTimeoutSeconds = 90f;

        /// <summary>Писать в журнал строки о принятом решении.</summary>
        public bool logOnStart = true;

        /// <summary>Строить экран сообщения (Canvas) при отказе.</summary>
        public bool buildMessageScreen = true;

        /// <summary>Что решили: VR / AR / отказ (для журнала и проверок).</summary>
        public KvArBootKind Decided { get; private set; }

        /// <summary>Имя типа активного лоадера на момент решения (пусто — ни одного).</summary>
        public string ActiveLoaderName { get; private set; }

        /// <summary>Инициализация XR завершилась до тайм-аута (для журнала).</summary>
        public bool XrInitializationComplete { get; private set; }

        /// <summary>Время ожидания XR, секунды (число для отчёта).</summary>
        public float WaitedSeconds { get; private set; }

        /// <summary>Сцена, которую загрузили (пусто — отказ). Для проверок.</summary>
        public string LoadedScene { get; private set; }

        private KvArUi ui;

        IEnumerator Start()
        {
            // РАЗРЕШЕНИЕ КАМЕРЫ — ТОЛЬКО ДЛЯ AR И ТОЛЬКО ПОСЛЕ ОПРЕДЕЛЕНИЯ РЕЖИМА (12.11.2026).
            //
            // ЧТО БЫЛО. Здесь стояло `yield return EnsureCameraPermissionForAr()` ДО определения
            // режима, а внутри — `while (!Permission.HasUserAuthorizedPermission(Camera))` БЕЗ
            // ТАЙМАУТА (так просил владелец 06.11.2026: «увидел запрос, но не успел нажать»).
            // ЧЕМ ЭТО ГРОЗИЛО. На шлеме этот цикл и был концом запуска: системный диалог разрешения
            // в очках нажать нечем, разрешение не выдаётся никогда, приложение стоит в
            // «КАМЕРА: ждём разрешение (нажатий запроса: 0)» и до VR не доходит. Именно поэтому
            // VR/MR на шлеме ни разу не проверяли — до него просто не доезжало (находка приёмки
            // 12.11.2026, PICO A9210).
            // ЧТО СТАЛО. Камера нужна ТОЛЬКО ветке AR. Разрешение запрашиваем сразу (диалог
            // появляется параллельно с инициализацией XR — на планшете оператор видит его так же
            // быстро, как раньше), но ЖДЁМ его только если режим оказался AR. В шлеме ждать нечего
            // и нечего показывать: OpenXR сам управляет доступом к камере.
#if UNITY_ANDROID && !UNITY_EDITOR
            RequestCameraPermissionNoWait();
#endif

            // XR ПОДНИМАЕМ САМИ, С ПОРЯДКОМ ПОД УСТРОЙСТВО.
            // Автоинициализация XR в настройках Android выключена (`m_InitManagerOnStart = 0`):
            // шлему нужен OpenXR первым, планшету — ARCore первым, иначе одна из платформ
            // зависает в инициализации и показывает чёрный экран/отказ.
            yield return KvArLoaderBootstrap.InitializeForDevice();

            float begin = Time.unscaledTime;
            yield return WaitForXrInitialization();
            WaitedSeconds = Time.unscaledTime - begin;
            XrInitializationComplete = IsXrInitializationComplete();
            ActiveLoaderName = ReadActiveLoaderName();

            // РЕЖИМ ЧИТАЕТСЯ ОДИН РАЗ — ПО ИМЕНИ ПОДНЯТОГО ЛОАДЕРА. Дальше от него зависит
            // ЕДИНСТВЕННОЕ решение про камеру: ждать разрешение (AR) или не ждать вовсе (VR/MR).
            Decided = KvArMode.Classify(ActiveLoaderName);

            if (logOnStart)
            {
                Debug.Log(KvArMode.ModeLine(Decided, ActiveLoaderName));
                Debug.Log("[KvAr] XR: инициализация " +
                          (XrInitializationComplete ? "завершена" : "НЕ завершилась за " +
                           xrInitTimeoutSeconds.ToString("0") + " с") +
                          " · ожидание " + WaitedSeconds.ToString("0.0") + " с");
            }

            // КАМЕРА: ЖДЁМ РАЗРЕШЕНИЕ ТОЛЬКО В AR. В шлеме (VR/MR) — не ждём ни секунды.
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Decided == KvArBootKind.Ar)
            {
                yield return EnsureCameraPermissionForAr();
            }
            else
            {
                Debug.Log("[KvAr] КАМЕРА: режим " + Decided + " — разрешение камеры не требуется " +
                          "(доступом управляет OpenXR), ожидания нет");
            }
#endif

            if (Decided == KvArBootKind.Ar)
            {
                // РЕЖИМ AR ДЛЯ ВСЕЙ ПРОГРАММЫ — здесь, ДО загрузки сцены.
                // Так строка «РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ» появляется в журнале с первого
                // кадра, а `KvXrRig` в AR-сцене уже не сможет переопределить режим на VR
                // (его `EnsureApplied` увидит готовый выбор). Возможности честные: сквозного
                // вида на телефоне нет, камера и пространственные якоря есть.
                KvXrPlatformModeSelector.Apply(
                    passthroughAvailable: false,
                    spatialAnchorsAvailable: true,
                    cameraAvailable: true);
            }

            string scene = KvArMode.SceneFor(Decided);
            if (!string.IsNullOrEmpty(scene))
            {
                LoadedScene = scene;
                if (logOnStart) Debug.Log("[KvAr] ЗАГРУЗКА: сцена «" + scene + "»");
                SceneManager.LoadScene(scene);
                yield break;
            }

            ShowUnsupported();
        }

        /// <summary>
        /// ЖДАТЬ, ПОКА XR ЗАКОНЧИТ ИНИЦИАЛИЗАЦИЮ. Условие готовности — `isInitializationComplete`
        /// (XR Management выставляет его после перебора лоадеров), а не «прошло N секунд».
        /// </summary>
        private int cameraPermissionRequests;

        /// <summary>
        /// ЗАПРОСИТЬ РАЗРЕШЕНИЕ КАМЕРЫ, НЕ ДОЖИДАЯСЬ ОТВЕТА (12.11.2026).
        ///
        /// Диалог Android появляется сразу, параллельно с инициализацией XR, поэтому на планшете
        /// оператор видит его так же быстро, как раньше. Ждём ответа — отдельно и только в AR
        /// (`EnsureCameraPermissionForAr`): в шлеме ждать нечего, диалог там нажать нечем.
        /// </summary>
        private void RequestCameraPermissionNoWait()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[KvAr] КАМЕРА: разрешение уже выдано");
                return;
            }
            Debug.Log("[KvAr] КАМЕРА: запрашиваю разрешение (до инициализации XR)");
            Permission.RequestUserPermission(Permission.Camera);
        }

        /// <summary>
        /// ЖДАТЬ РАЗРЕШЕНИЕ КАМЕРЫ БЕЗ ТАЙМАУТА. Показываем сообщение и кнопку, повторяем запрос
        /// по нажатию; после двух отказов кнопка ведёт в настройки приложения Android.
        ///
        /// ВЫЗЫВАЕТСЯ ТОЛЬКО В РЕЖИМЕ AR (ветка планшета/телефона). В шлеме этот цикл означал бы
        /// вечное ожидание: системный диалог в очках нажать нечем (находка приёмки 12.11.2026).
        /// </summary>
        private IEnumerator EnsureCameraPermissionForAr()
        {
            if (Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[KvAr] КАМЕРА: разрешение уже выдано");
                yield break;
            }

            Debug.Log("[KvAr] КАМЕРА: ждём разрешение в режиме AR (до инициализации сцены)");
            Permission.RequestUserPermission(Permission.Camera);

            ui = GetComponent<KvArUi>();
            if (ui == null) ui = gameObject.AddComponent<KvArUi>();
            ui.buildOnStart = false;
            ui.EnsureBuilt();
            ui.SetButtonsVisible(false);
            ui.SetStatus("Камера: нужен доступ");

            float lastLog = Time.unscaledTime;
            while (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                ui.ShowMessage(
                    "Программе нужен доступ к камере:\nбез него не видно стол и работа с ним невозможна.\n\n" +
                    "Нажмите кнопку и разрешите камеру в системном окне.",
                    cameraPermissionRequests >= 2 ? "ОТКРЫТЬ НАСТРОЙКИ" : "РАЗРЕШИТЬ КАМЕРУ",
                    RequestCameraOrSettings);

                if (Time.unscaledTime - lastLog > 5f)
                {
                    lastLog = Time.unscaledTime;
                    Debug.Log("[KvAr] КАМЕРА: ждём разрешение (нажатий запроса: " + cameraPermissionRequests + ")");
                }
                yield return null;
            }

            ui.HideMessage();
            Debug.Log("[KvAr] КАМЕРА: разрешение ВЫДАНО — продолжаю запуск");
        }

        private void RequestCameraOrSettings()
        {
            cameraPermissionRequests++;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (cameraPermissionRequests >= 2 && !Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                OpenAndroidAppSettings();
                return;
            }
            Permission.RequestUserPermission(Permission.Camera);
            Debug.Log("[KvAr] КАМЕРА: повторный запрос разрешения (" + cameraPermissionRequests + ")");
#endif
        }

        private void OpenAndroidAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var intent = new AndroidJavaObject("android.content.Intent",
                           "android.settings.APPLICATION_DETAILS_SETTINGS"))
                using (var uri = new AndroidJavaClass("android.net.Uri"))
                {
                    var data = uri.CallStatic<AndroidJavaObject>("fromParts", "package",
                        activity.Call<string>("getPackageName"), null);
                    intent.Call<AndroidJavaObject>("setData", data);
                    activity.Call("startActivity", intent);
                }
                Debug.Log("[KvAr] КАМЕРА: открыты настройки приложения — включите «Камера»");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[KvAr] КАМЕРА: не удалось открыть настройки: " + e.Message);
            }
#endif
        }

        private IEnumerator WaitForXrInitialization()
        {
            float deadline = Time.unscaledTime + Mathf.Max(1f, xrInitTimeoutSeconds);
            while (!IsXrInitializationComplete() && Time.unscaledTime < deadline)
                yield return null;
        }

        /// <summary>
        /// ЗАКОНЧИЛАСЬ ЛИ ИНИЦИАЛИЗАЦИЯ XR. В редакторе и на ПК XR может быть не настроен —
        /// тогда `XRGeneralSettings.Instance` отсутствует, и это НЕ ошибка: возвращаем false,
        /// режим получится «не определён» с честным сообщением.
        /// </summary>
        public static bool IsXrInitializationComplete()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null) return false;
            return settings.Manager.isInitializationComplete;
        }

        /// <summary>
        /// ИМЯ ТИПА АКТИВНОГО ЛОАДЕРА (`OpenXRLoader`, `ARCoreLoader`, …). Пустая строка — ни один
        /// не поднялся. Только чтение: ничего не инициализирует и не меняет.
        /// </summary>
        public static string ReadActiveLoaderName()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            if (settings == null || settings.Manager == null) return string.Empty;
            XRLoader loader = settings.Manager.activeLoader;
            return loader != null ? loader.GetType().Name : string.Empty;
        }

        /// <summary>
        /// ПОКАЗАТЬ ОТКАЗ (Ж1): понятный текст на экране и строка в журнал.
        ///
        /// ПОЧЕМУ ЕСТЬ КНОПКА «ПРОДОЛЖИТЬ В VR». На шлеме провайдер обязан подниматься (OpenXR),
        /// и отказ там означал бы поломку устройства/разрешений. Но оставлять оператора без
        /// выхода нельзя: если XR не поднялся за тайм-аут (например, системный диалог задержался),
        /// кнопка грузит прежнюю сцену шлема — это НЕ ослабление Ж1 (сообщение показано честно),
        /// а страховка от ложного отказа. На телефоне без ARCore эта кнопка приведёт в VR-сцену,
        /// поэтому она названа «для шлема» и пишет предупреждение в журнал.
        /// </summary>
        public void ShowUnsupported()
        {
            Debug.LogError("[KvAr] ОТКАЗ: " + KvArMode.UnsupportedMessage().Replace("\n", " "));
            if (!buildMessageScreen) return;
            ui = GetComponent<KvArUi>();
            if (ui == null) ui = gameObject.AddComponent<KvArUi>();
            ui.buildOnStart = false;
            ui.EnsureBuilt();
            ui.SetButtonsVisible(false);
            ui.SetStatus("AR недоступен");
            ui.ShowMessage(KvArMode.UnsupportedMessage(), "Продолжить в VR (для шлема)", LoadVrFallback);
        }

        /// <summary>
        /// СТРАХОВОЧНЫЙ ПЕРЕХОД В VR. Используется только по кнопке на экране отказа: сам
        /// отказ уже показан и записан в журнал, решение принял человек.
        /// </summary>
        public void LoadVrFallback()
        {
            Debug.LogWarning("[KvAr] РУЧНОЙ ПЕРЕХОД: по кнопке на экране отказа грузим " +
                             KvArMode.VrSceneName + " (если это телефон без ARCore — VR-сцена " +
                             "будет пустой: AR на таком устройстве недоступен)");
            LoadedScene = KvArMode.VrSceneName;
            SceneManager.LoadScene(KvArMode.VrSceneName);
        }

        /// <summary>
        /// РЕШЕНИЕ БЕЗ ОЖИДАНИЯ — чистая функция для тестов: по имени лоадера и факту готовности
        /// XR вернуть режим и сцену. Пустая сцена = отказ.
        /// </summary>
        public static string DecideScene(string activeLoaderName, out KvArBootKind kind)
        {
            kind = KvArMode.Classify(activeLoaderName);
            return KvArMode.SceneFor(kind);
        }
    }
}
