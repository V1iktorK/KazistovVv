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

        /// <summary>
        /// НАПЕЧАТАТЬ СТРОКУ ОДИН РАЗ ПРИ ЗАПУСКЕ. `BeforeSceneLoad` — до того, как кто-либо успеет
        /// сообщить о проблеме: строка обязана быть ПЕРВОЙ в журнале запуска.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void LogOnStartup()
        {
            Debug.Log(Describe(SystemInfo.graphicsDeviceType, SystemInfo.graphicsDeviceName,
                SystemInfo.deviceModel, SystemInfo.operatingSystem, Application.identifier));
        }
    }
}
