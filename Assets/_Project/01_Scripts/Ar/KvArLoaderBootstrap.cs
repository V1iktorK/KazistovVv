using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Management;

namespace KazistovVvAr
{
    /// <summary>
    /// ПОРЯДОК XR-ЗАГРУЗЧИКОВ ЗАВИСИТ ОТ УСТРОЙСТВА — и решается на устройстве, а не в настройках.
    ///
    /// ПОЧЕМУ ЭТОТ КЛАСС ПОЯВИЛСЯ (05.11.2026). Один APK ставится и на шлем PICO, и на
    /// планшет. XR Management берёт ПЕРВЫЙ УСПЕШНЫЙ лоадер, а порядок в настройках один на
    /// всех. Проверено на живых устройствах:
    ///   • OpenXR ПЕРВЫМ → на планшете PICO-реализация OpenXR зависает в инициализации
    ///     (XR не поднимается за 30–90 с), ARCore до второго места не доходит;
    ///   • ARCore ПЕРВЫМ → на шлеме PICO ломается запуск VR (жалоба MR-агента 05.11.2026).
    /// Поэтому автоинициализация XR выключена (`m_InitManagerOnStart = 0` в настройках
    /// Android), а порядок выставляется здесь по устройству:
    ///   • шлем PICO    → [OpenXR, ARCore]  (VR/MR как раньше);
    ///   • планшет/телефон → [ARCore, OpenXR] (AR поднимается сразу).
    /// </summary>
    public static class KvArLoaderBootstrap
    {
        /// <summary>
        /// Шлем ли это (PICO). Производитель/модель читаются через `android.os.Build` на Android,
        /// плюс запасной путь через `SystemInfo.deviceModel` (на шлеме строка тоже содержит PICO).
        /// Атрибут `Build` из C#-API Unity в этом контексте не существует — проверено компилятором
        /// 05.11.2026, поэтому Java-класс, а не прямая ссылка.
        /// </summary>
        public static bool IsPicoDevice()
        {
            string id = SystemInfo.deviceModel;
            if (Application.platform == RuntimePlatform.Android)
            {
                try
                {
                    using (var javaBuild = new AndroidJavaClass("android.os.Build"))
                    {
                        id += " " + javaBuild.GetStatic<string>("MANUFACTURER");
                        id += " " + javaBuild.GetStatic<string>("MODEL");
                        id += " " + javaBuild.GetStatic<string>("DEVICE");
                    }
                }
                catch (Exception)
                {
                    // Не критично: остаётся SystemInfo.deviceModel (на PICO в нём есть «PICO»).
                }
            }
            return id.IndexOf("pico", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// ЧИСТАЯ ФУНКЦИЯ ПОРЯДКА (для тестов): какие семейства лоадеров и в каком порядке.
        /// </summary>
        public static string[] DesiredLoaderOrder(bool picoDevice)
        {
            return picoDevice
                ? new[] { "OpenXR", "ARCore" }
                : new[] { "ARCore", "OpenXR" };
        }

        /// <summary>
        /// Переставить уже имеющиеся лоадеры в нужном порядке. Неизвестные типы сохраняются
        /// в конце, чтобы ничего не потерять.
        /// </summary>
        public static List<XRLoader> OrderLoaders(IReadOnlyList<XRLoader> available, bool picoDevice)
        {
            var openXr = new List<XRLoader>();
            var arCore = new List<XRLoader>();
            var other = new List<XRLoader>();
            if (available != null)
            {
                for (int i = 0; i < available.Count; i++)
                {
                    XRLoader loader = available[i];
                    if (loader == null) continue;
                    string name = loader.GetType().Name;
                    if (name.IndexOf("OpenXR", StringComparison.OrdinalIgnoreCase) >= 0) openXr.Add(loader);
                    else if (name.IndexOf("ARCore", StringComparison.OrdinalIgnoreCase) >= 0) arCore.Add(loader);
                    else other.Add(loader);
                }
            }

            var result = new List<XRLoader>();
            if (picoDevice)
            {
                result.AddRange(openXr);
                result.AddRange(arCore);
            }
            else
            {
                result.AddRange(arCore);
                result.AddRange(openXr);
            }
            result.AddRange(other);
            return result;
        }

        /// <summary>Имена лоадеров через запятую (для журнала).</summary>
        public static string Describe(List<XRLoader> loaders)
        {
            if (loaders == null || loaders.Count == 0) return "нет";
            var names = new List<string>();
            for (int i = 0; i < loaders.Count; i++)
                names.Add(loaders[i] != null ? loaders[i].GetType().Name : "null");
            return string.Join(", ", names.ToArray());
        }

        /// <summary>
        /// РАННЯЯ ИНИЦИАЛИЗАЦИЯ XR — ДО ПЕРВОЙ СЦЕНЫ, КАК ДЕЛАЕТ САМ XR MANAGEMENT.
        ///
        /// ПОЧЕМУ НЕ В `KvArBoot.Start` (дефект 05.11.2026). На шлеме PICO при инициализации
        /// в первой сцене VR не включался: `XRSettings.loadedDeviceName` = «OpenXR Display»,
        /// но `isDeviceActive` = false, MR-возможности не определялись, камера/PICO-рантайм
        /// считали приложение 2D. PICO-рантайму нужно, чтобы XR-дисплей поднялся ДО первой
        /// сцены — ровно как при автоинициализации. Поэтому зеркалим путь XR Management:
        /// AfterAssembliesLoaded → InitializeLoaderSync, BeforeSplashScreen → StartSubsystems.
        /// Порядок лоадеров к этому моменту выбирается по устройству (шлем/планшет).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterAssembliesLoaded)]
        private static void EarlyInitializeXr()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            XRManagerSettings manager = settings != null ? settings.Manager : null;
            if (manager == null || manager.isInitializationComplete)
                return;

            bool pico = IsPicoDevice();
            List<XRLoader> ordered = OrderLoaders(manager.activeLoaders, pico);
            if (ordered.Count > 0)
                manager.TrySetLoaders(ordered);

            Debug.Log("[KvAr] XR: РАННЯЯ инициализация для " +
                      (pico ? "шлема PICO" : "планшета/телефона") + " — [" + Describe(ordered) + "]");
            manager.InitializeLoaderSync();
        }

        /// <summary>
        /// СТАРТ ПОДСИСТЕМ ДО СПЛЭША (как StartXRSDK у XR Management). Нужен шлему: без него
        /// VR-дисплей остаётся неактивным, хотя лоадер уже загружен.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void EarlyStartXrSubsystems()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            XRManagerSettings manager = settings != null ? settings.Manager : null;
            if (manager == null)
                return; // совсем нет настроек XR: BootAr покажет честный отказ

            // ВТОРОЙ ШАНС НА ИНИЦИАЛИЗАЦИЮ: если на AfterAssembliesLoaded настройки XR
            // ещё не были готовы, поднимаем загрузчик здесь — тоже ДО первой сцены.
            if (!manager.isInitializationComplete)
            {
                bool pico = IsPicoDevice();
                List<XRLoader> ordered = OrderLoaders(manager.activeLoaders, pico);
                if (ordered.Count > 0)
                    manager.TrySetLoaders(ordered);
                Debug.Log("[KvAr] XR: инициализация перед сплэшем для " +
                          (pico ? "шлема PICO" : "планшета/телефона") + " — [" + Describe(ordered) + "]");
                manager.InitializeLoaderSync();
            }

            if (manager.isInitializationComplete && manager.activeLoader != null && !startedSubsystems)
            {
                manager.StartSubsystems();
                startedSubsystems = true;
                Debug.Log("[KvAr] XR: РАННИЙ старт подсистем · активный лоадер " +
                          manager.activeLoader.GetType().Name);
            }
        }

        private static bool startedSubsystems;
        /// <summary>
        /// ИНИЦИАЛИЗИРОВАТЬ XR ПОСЛЕ ВЫБОРА ПОРЯДКА. Вызывается из `KvArBoot` первой сцены.
        /// Автоинициализация XR выключена в настройках Android, поэтому это единственный вход.
        /// </summary>
        public static IEnumerator InitializeForDevice()
        {
            XRGeneralSettings settings = XRGeneralSettings.Instance;
            XRManagerSettings manager = settings != null ? settings.Manager : null;
            if (manager == null)
            {
                Debug.LogWarning("[KvAr] XR: настроек XR Management нет — режим будет «не определён»");
                yield break;
            }

            if (manager.isInitializationComplete)
                yield break; // кто-то уже поднял XR (редактор/автоинициализация) — не мешаем

            bool pico = IsPicoDevice();
            List<XRLoader> ordered = OrderLoaders(manager.activeLoaders, pico);
            if (ordered.Count > 0)
            {
                bool changed = manager.TrySetLoaders(ordered);
                Debug.Log("[KvAr] XR: порядок загрузчиков для " + (pico ? "шлема PICO" : "планшета/телефона") +
                          " — [" + Describe(ordered) + "]" + (changed ? "" : " (XR Management не принял порядок)"));
            }

            // СИНХРОННАЯ ИНИЦИАЛИЗАЦИЯ + ЯВНЫЙ СТАРТ ПОДСИСТЕМ.
            //
            // ПОЧЕМУ НЕ КОРУТИНА `InitializeLoader()`. 05.11.2026 на шлеме PICO после ручной
            // инициализации VR не поднялся: лоадер OpenXR был активен, но `XRSettings.isDeviceActive`
            // остался false — подсистемы не стартовали (на планшете это не видно: AR Foundation
            // поднимает свои подсистемы сам, поэтому ARCore работал). Синхронный путь повторяет
            // ровно то, что делает автоинициализация XR Management: `InitializeLoaderSync()`,
            // затем `StartSubsystems()`.
            if (!manager.isInitializationComplete)
                manager.InitializeLoaderSync();

            if (manager.isInitializationComplete && manager.activeLoader != null)
            {
                if (!startedSubsystems)
                {
                    manager.StartSubsystems();
                    startedSubsystems = true;
                }
                Debug.Log("[KvAr] XR: подсистемы запущены · активный лоадер " +
                          manager.activeLoader.GetType().Name);
                // ОДИН КАДР — и проверяем, что устройство действительно активно.
                // На шлеме без этого нельзя отличить «подсистемы стартовали» от «VR не включился».
                yield return null;
                Debug.Log("[KvAr] XR: устройство активно = " + UnityEngine.XR.XRSettings.isDeviceActive +
                          " · загружено устройство «" + UnityEngine.XR.XRSettings.loadedDeviceName + "»");
            }
            else
            {
                Debug.LogWarning("[KvAr] XR: не удалось поднять загрузчик (инициализация " +
                                 (manager.isInitializationComplete ? "завершена" : "НЕ завершена") + ")");
            }

            yield break;
        }
    }
}