using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ГДЕ ЖИВЁТ РАЗРЕШЕНИЕ НА ГОЛОС (§42). Без `appID` из консоли PICO и без разрешения
    /// `RECORD_AUDIO` распознавания не будет, и приложение обязано СКАЗАТЬ ЭТО СЛОВАМИ ([S2] №7).
    /// </summary>
    public static class KvXrVoiceStatus
    {
        /// <summary>Доступен ли голос при таких условиях (без исключений и без ложного «да»).</summary>
        public static bool Available(bool hasAppId, bool hasMicrophonePermission)
        {
            return hasAppId && hasMicrophonePermission;
        }

        /// <summary>Строка для экрана «Ввод» и журнала.</summary>
        public static string Line(bool hasAppId, bool hasMicrophonePermission)
        {
            if (!hasAppId)
                return "голос недоступен: нет appID PICO (клавиатура и колесо работают)";
            if (!hasMicrophonePermission)
                return "голос недоступен: нет разрешения на микрофон RECORD_AUDIO " +
                       "(клавиатура и колесо работают)";
            return "голос доступен: PICO ASR, микрофон разрешён";
        }
    }
}
