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
            // РАЗРЕШЕНИЕ КАМЕРЫ — ДО ИНИЦИАЛИЗАЦИИ XR. На Android это отдельный диалог; ARCore
            // без него не получает кадров (экран остаётся заливкой, трекинг стоит). Спрашиваем
            // ДО подъёма XR, иначе сессия стартует без камеры и владелец видит «всё жёлтым».
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[KvAr] КАМЕРА: запрашиваю разрешение (до инициализации XR)");
                Permission.RequestUserPermission(Permission.Camera);
                float permissionDeadline = Time.unscaledTime + 20f;
                while (!Permission.HasUserAuthorizedPermission(Permission.Camera) &&
                       Time.unscaledTime < permissionDeadline)
                    yield return null;
                Debug.Log("[KvAr] КАМЕРА: разрешение " +
                          (Permission.HasUserAuthorizedPermission(Permission.Camera)
                              ? "выдано" : "НЕ выдано — AR не сможет смотреть в камеру"));
            }
            else
            {
                Debug.Log("[KvAr] КАМЕРА: разрешение уже выдано");
            }
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
            Decided = KvArMode.Classify(ActiveLoaderName);

            if (logOnStart)
            {
                Debug.Log(KvArMode.ModeLine(Decided, ActiveLoaderName));
                Debug.Log("[KvAr] XR: инициализация " +
                          (XrInitializationComplete ? "завершена" : "НЕ завершилась за " +
                           xrInitTimeoutSeconds.ToString("0") + " с") +
                          " · ожидание " + WaitedSeconds.ToString("0.0") + " с");
            }

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
