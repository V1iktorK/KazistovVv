using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПРИЧИНА, ПО КОТОРОЙ ВИЗУАЛИЗАЦИЙ НЕ ВИДНО (§42): одна строка с числом видимых и названной
    /// причиной. Найденная 25.10.2026 причина — в сцене шлема НЕТ потока этапов
    /// (`TrajectoryFlowController` создаётся только десктопным контроллером камеры), поэтому службы
    /// не привязываются, а тумблеры `view.*` молча ничего не делают.
    /// </summary>
    public static class KvXrVisuals
    {
        public const string ReasonNoFlow = "поток этапов не создан";
        public const string ReasonNoRobot = "робот не привязан";
        public const string ReasonToggledOff = "выключено тумблером";
        public const string ReasonNoGeometry = "в этой позе показывать нечего";
        public const string ReasonDesktopCanvas = "панель на десктопном канвасе";
        public const string ReasonVisible = "видно";

        /// <summary>Причина по состоянию визуализатора (порядок проверок — от главной к частной).</summary>
        public static string Reason(bool flowCreated, bool robotBound, bool toggledOn,
                                    bool hasGeometry, bool onDesktopCanvas)
        {
            if (!flowCreated) return ReasonNoFlow;
            if (!robotBound) return ReasonNoRobot;
            if (!toggledOn) return ReasonToggledOff;
            if (!hasGeometry) return ReasonNoGeometry;
            if (onDesktopCanvas) return ReasonDesktopCanvas;
            return ReasonVisible;
        }

        /// <summary>Строка журнала: «[KvXr] ЗОНЫ: видимо N · причина скрытия «…»».</summary>
        public static string Describe(int visible, string reason)
        {
            if (visible > 0 && string.IsNullOrEmpty(reason)) reason = ReasonVisible;
            if (string.IsNullOrEmpty(reason)) reason = ReasonNoFlow;
            return "[KvXr] ЗОНЫ: видимо " + Mathf.Max(0, visible) + " · причина скрытия «" + reason + "»";
        }
    }
}
