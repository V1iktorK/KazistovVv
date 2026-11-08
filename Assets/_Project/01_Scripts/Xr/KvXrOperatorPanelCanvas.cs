using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ОТНОСИТСЯ ЛИ КАНВАС К ПАНЕЛЯМ ОПЕРАТОРА (R6-1, третий круг приёмки 12.11.2026).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. На один и тот же вопрос «этот канвас — часть десктопного интерфейса,
    /// который надо погасить?» отвечают ДВА сторожа:
    ///   • <see cref="KvXrDesktopOverlayGuard"/> — работает по таймеру в шлеме и в AR;
    ///   • `KvArRuntime.HideForeignCanvases` — разово при старте AR-сцены.
    /// Пока правило было записано в каждом по-своему, они и разошлись: один гасил канвас панелей,
    /// другой (после правки) — уже нет. Панель настроек включалась и тут же гасла, а причина
    /// выглядела как «команда выполнена, панели нет» — ровно находка R6-1, вернувшаяся из приёмки.
    /// Поэтому ответ на этот вопрос живёт в ОДНОМ месте, и оба сторожа зовут его.
    ///
    /// ЧТО СЧИТАЕТСЯ ПАНЕЛЯМИ ОПЕРАТОРА. Канвас с именем
    /// <see cref="KvXrOverlayPolicy.DockCanvasName"/> и ВСЁ, что лежит внутри него, включая
    /// вложенные канвасы самих панелей (`KvDockPanel` создаёт их для порядка сортировки).
    /// Вложенные канвасы отдельно гасить нельзя: включённый вложенный канвас под выключенным
    /// корневым не рисуется, но и «погашенный» вложенный канвас не даст панели нарисоваться,
    /// когда корневой включён. Судьбу всего поддерева решает корень.
    /// </summary>
    public static class KvXrOperatorPanelCanvas
    {
        /// <summary>Сам канвас панелей оператора или любой канвас внутри него.</summary>
        public static bool IsOperatorPanelCanvas(Canvas canvas)
        {
            return canvas != null && IsOperatorPanelCanvas(canvas.transform);
        }

        /// <summary>Тот же ответ по трансформу (канвас панелей — он сам или один из его предков).</summary>
        public static bool IsOperatorPanelCanvas(Transform transform)
        {
            if (transform == null) return false;
            if (KvXrOverlayPolicy.IsDockCanvas(transform.gameObject.name)) return true;

            Transform current = transform.parent;
            int guard = 0;
            while (current != null && guard++ < 32)
            {
                if (KvXrOverlayPolicy.IsDockCanvas(current.gameObject.name)) return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>Именно КОРНЕВОЙ канвас панелей (не вложенный).</summary>
        public static bool IsDockRootCanvas(Canvas canvas)
        {
            return canvas != null && KvXrOverlayPolicy.IsDockCanvas(canvas.gameObject.name);
        }
    }
}
