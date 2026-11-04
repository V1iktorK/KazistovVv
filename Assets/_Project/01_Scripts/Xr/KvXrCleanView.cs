using KazistovVvFeatures;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ЧИСТЫЙ ВИД ПО УМОЛЧАНИЮ (решение владельца 04.11.2026: «зоны включены, какой ужас —
    /// выключи все плюшки по умолчанию; оставь их, но не по умолчанию»).
    ///
    /// ПОЧЕМУ ОДИН МЕСТОМ. Владелец на тесте видел поверх рабочего вида шар тепловой карты и
    /// жёлтые/красные элементы. Причина не в одном флаге: визуализаций шесть (зона достижимости,
    /// лимиты суставов, сингулярности, теплокарта достижимости, теплокарта зазоров, зоны запрета),
    /// и каждая включалась своим путём — часть ещё и восстанавливалась из сохранённых флагов
    /// сессии. Поодиночке выключать их бессмысленно: следующая останется. Поэтому чистый вид
    /// применяется ОДНИМ вызовом после того, как поток этапов и службы этапов поднялись.
    ///
    /// ЧТО НЕ ТЕРЯЕТСЯ. Ни одна плюшка не удалена: любую можно включить обратно командами
    /// «Вид → …» / «Тепловые карты» (H — достижимость, J — зазоры), «Зоны запрета». Меняется
    /// только СОСТОЯНИЕ НА СТАРТЕ.
    /// </summary>
    public static class KvXrCleanView
    {
        /// <summary>Причина в журнале — чтобы «почему пусто» не искали заново.</summary>
        public const string Reason =
            "по умолчанию видны только робот и панель (решение владельца 04.11.2026)";

        /// <summary>
        /// ВЫКЛЮЧИТЬ ВСЕ ВИЗУАЛИЗАЦИИ-ПЛЮШКИ. Возвращает число скрытых, чтобы в журнале было
        /// честное число, а не «что-то сделал». Не создаёт ничего и не бросает: отсутствующая
        /// служба просто пропускается (в сцене шлема не все они могут быть).
        /// </summary>
        public static int Apply(FeatureHub features, TrajectoryFlowController flow, KvStageHub hub)
        {
            int hidden = 0;

            if (flow != null)
            {
                flow.SetWorkspaceVisible(false);      // зона достижимости (цветные кольца)
                flow.SetJointLimitsVisible(false);    // лимиты суставов
                flow.SetMetricsPanelVisible(false);   // панель чисел потока
                hidden += 3;
            }

            if (features != null)
            {
                if (features.Heatmap != null)
                {
                    features.Heatmap.SetVisible(false);   // тот самый «шар» + жёлтые точки
                    hidden++;
                }
                if (features.Clearance != null)
                {
                    features.Clearance.SetVisible(false); // теплокарта зазоров
                    hidden++;
                }
                if (features.Zones != null)
                {
                    features.Zones.SetAllVisible(false);  // жёлтые/красные границы запретных зон
                    hidden++;
                }
            }

            if (hub != null && hub.Singularities != null)
            {
                hub.Singularities.SetEnabled(false);      // «пятна» сингулярностей у базы/локтя
                hidden++;
            }

            return hidden;
        }

        /// <summary>Строка журнала о чистом виде (число + причина).</summary>
        public static string Describe(int hidden)
        {
            return "ЧИСТЫЙ ВИД: плюшек скрыто " + Mathf.Max(0, hidden) + " · " + Reason;
        }
    }
}
