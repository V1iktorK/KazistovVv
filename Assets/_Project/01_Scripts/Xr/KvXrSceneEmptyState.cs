using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПРИЗНАК СТЕНДА СЦЕНЫ И СТРОКА О ПУСТОЙ СЦЕНЕ (§42). Имена — те, что реально лежат в
    /// `XrMain`: «Стенд_1_Стол» и «Стенд_2_Стол»; роботы — их ДЕТИ, поэтому гасятся вместе со столом.
    /// </summary>
    public static class KvXrSceneEmptyState
    {
        /// <summary>Стенд ли это (стол сцены вместе с его роботами).</summary>
        public static bool IsSceneStand(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return false;
            string lower = objectName.ToLowerInvariant();
            if (lower.IndexOf("стенд", System.StringComparison.Ordinal) >= 0) return true;
            if (lower.IndexOf("stand", System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        /// <summary>Строка журнала: сколько выключено (числа обязательны).</summary>
        public static string Describe(int tables, int robots)
        {
            return "[KvXr] СЦЕНА: выключено роботов " + Mathf.Max(0, robots) + " · столов " +
                   Mathf.Max(0, tables) + " — как в ПК до добавления";
        }
    }
}
