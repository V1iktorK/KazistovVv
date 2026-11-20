// ================================================================================================
//  KvEnvBanner.cs — СТРОКА ОКРУЖЕНИЯ В ЖУРНАЛЕ ЗАПУСКА (P4-19 п.4, 12.11.2026).
//
//  ОТКУДА ЗАДАЧА. У проекта ДВА варианта сборки из одного кода: шлем (Vulkan) и планшет
//  (OpenGLES3). На планшете Vulkan-сборка даёт ПОЛНОСТЬЮ чёрный экран при живом процессе и нуле
//  ошибок в журнале — то есть «чёрный экран» одинаково выглядит и когда не тот APK, и когда не тот
//  графический API, и когда не тот драйвер. Различить это по журналу было НЕЧЕМ.
//
//  ЧТО ДЕЛАЕТ. Один раз при запуске печатает ФАКТИЧЕСКИЕ значения: графический API, имя
//  графического устройства, модель устройства, версию ОС и идентификатор приложения. После этого
//  «чёрный экран» сразу разделяется на три разных случая:
//    • API = Null/не тот      → собрали/поставили не тот APK;
//    • API верный, кадров нет → драйвер/поверхность (это уже не «не тот APK»);
//    • приложение другое      → стоит чужая сборка.
//
//  ПОЧЕМУ `Describe` ОТДЕЛЬНАЯ ЧИСТАЯ ФУНКЦИЯ. Строку журнала надо уметь проверить тестом без
//  устройства: `Describe` не читает состояние машины, ей всё передают аргументами.
// ================================================================================================

using System.Text;
using UnityEngine;
using UnityEngine.Rendering;

namespace KazistovVvFeatures
{
    /// <summary>Строка окружения в журнале запуска: графический API, устройство, ОС, пакет.</summary>
    public static class KvEnvBanner
    {
        /// <summary>Префикс строки в журнале — по нему её ищут в логах устройства.</summary>
        public const string LogPrefix = "[KvEnv]";

        /// <summary>
        /// СОБРАТЬ СТРОКУ. Чистая функция: всё, что нужно, приходит аргументами — поэтому её можно
        /// проверить тестом без Unity-плеера и без устройства.
        /// </summary>
        public static string Describe(GraphicsDeviceType api, string graphicsDevice, string deviceModel,
            string operatingSystem, string applicationIdentifier)
        {
            StringBuilder sb = new StringBuilder(160);
            sb.Append(LogPrefix).Append(" ЗАПУСК: графика ").Append(ApiName(api));
            if (!string.IsNullOrEmpty(graphicsDevice)) sb.Append(" · устройство GPU ").Append(graphicsDevice);
            if (!string.IsNullOrEmpty(deviceModel)) sb.Append(" · модель ").Append(deviceModel);
            if (!string.IsNullOrEmpty(operatingSystem)) sb.Append(" · ОС ").Append(operatingSystem);
            if (!string.IsNullOrEmpty(applicationIdentifier)) sb.Append(" · пакет ").Append(applicationIdentifier);
            sb.Append(" · ").Append(ApiHint(api));
            return sb.ToString();
        }

        /// <summary>
        /// ИМЯ API ДЛЯ ЧЕЛОВЕКА. `GraphicsDeviceType` печатается как `OpenGLES3`, а в разговоре
        /// владельца и тестировщика звучит «GLES3» — поэтому у двух значений есть короткие имена.
        /// </summary>
        /// <summary>
        /// ВЫВОД СТРОКИ ПРИ СТАРТЕ. Требование P4-19 п.4: строка окружения в журнале ЗАПУСКА.
        /// До 17.11 функция Describe существовала, но её НИКТО не вызывал — [KvEnv] не печатался
        /// и проверка HEADSET честно говорила «нет строки [KvEnv]» (замер: вызывающими были только
        /// тесты). Крючок исполняется в любом билде приложения независимо от сцены.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EmitOnStart()
        {
            Debug.Log(Describe(SystemInfo.graphicsDeviceType, SystemInfo.graphicsDeviceName,
                SystemInfo.deviceModel, SystemInfo.operatingSystem, Application.identifier));
        }

        public static string ApiName(GraphicsDeviceType api)
        {
            switch (api)
            {
                case GraphicsDeviceType.Vulkan: return "Vulkan";
                case GraphicsDeviceType.OpenGLES3: return "OpenGLES3 (GLES3)";
                case GraphicsDeviceType.OpenGLCore: return "OpenGLCore (GL)";
                case GraphicsDeviceType.Direct3D11: return "Direct3D11";
                case GraphicsDeviceType.Direct3D12: return "Direct3D12";
                case GraphicsDeviceType.Metal: return "Metal";
                case GraphicsDeviceType.Null: return "Null (ГРАФИКИ НЕТ)";
                default: return api.ToString();
            }
        }

        /// <summary>
        /// ПОДСКАЗКА «ДЛЯ КАКОГО УСТРОЙСТВА ЭТА СБОРКА». Именно она превращает строку журнала в
        /// ответ на вопрос «не тот APK или не тот драйвер».
        /// </summary>
        public static string ApiHint(GraphicsDeviceType api)
        {
            switch (api)
            {
                case GraphicsDeviceType.Vulkan: return "сборка для ШЛЕМА (VR/MR)";
                case GraphicsDeviceType.OpenGLES3: return "сборка для ПЛАНШЕТА/ТЕЛЕФОНА (AR)";
                case GraphicsDeviceType.Null: return "КАДРОВ НЕ БУДЕТ: графический API не выбран";
                default: return "нестандартный API: сверьтесь с Docs/ПОДКЛЮЧЕНИЕ_ТЕСТИРОВЩИКА.md";
            }
        }

        private static bool logged;

        /// <summary>
        /// НАПЕЧАТАТЬ СТРОКУ ОДИН РАЗ ПРИ ЗАПУСКЕ. `BeforeSceneLoad` — до того, как кто-либо успеет
        /// сообщить о проблеме: строка обязана быть ПЕРВОЙ в журнале запуска. Защита от повторов
        /// стоит ЗДЕСЬ (внутри), потому что у строки три источника (два хука + `EnsureLogged`) —
        /// замер 21.11: без внутренней защиты строка печаталась трижды.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LogOnStartup()
        {
            if (logged) return;
            logged = true;
            Debug.Log(Describe(SystemInfo.graphicsDeviceType, SystemInfo.graphicsDeviceName,
                SystemInfo.deviceModel, SystemInfo.operatingSystem, Application.identifier));
        }

        /// <summary>
        /// ГАРАНТИРОВАННЫЙ ВЫВОД С ПЕРВОГО КАДРА ЖИВОГО КОМПОНЕНТА (страховка тестировщика 20.11).
        ///
        /// ЧТО БЫЛО. Хуки `[RuntimeInitializeOnLoadMethod]` есть в сборке (проверено по
        /// global-metadata.dat), но в прогоне 20.11 строка не печаталась ни разу (271 строка
        /// журнала запуска — [KvEnv] 0). После страховки строка печатается (замер 21.11).
        /// Печать с первого кадра `KvXrFlowBridge.Update` — компонент доказанно жив (его строки
        /// видны в каждом прогоне), значит строка окружения появится всегда.
        /// </summary>
        public static void EnsureLogged()
        {
            LogOnStartup();
        }
    }
}
