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
            bool hasFlow = flow != null;
            bool hasHeatmap = features != null && features.Heatmap != null;
            bool hasClearance = features != null && features.Clearance != null;
            bool hasZones = features != null && features.Zones != null;
            bool hasSingularities = hub != null && hub.Singularities != null;

            if (hasFlow)
            {
                flow.SetWorkspaceVisible(false);      // зона достижимости (цветные кольца)
                flow.SetJointLimitsVisible(false);    // лимиты суставов
                flow.SetMetricsPanelVisible(false);   // панель чисел потока
            }

            if (hasHeatmap) features.Heatmap.SetVisible(false);     // тот самый «шар» + жёлтые точки
            if (hasClearance) features.Clearance.SetVisible(false); // теплокарта зазоров
            if (hasZones) features.Zones.SetAllVisible(false);      // жёлтые/красные границы запретных зон
            if (hasSingularities) hub.Singularities.SetEnabled(false); // «пятна» сингулярностей у базы/локтя

            // ЧИСЛО СКРЫТОГО СЧИТАЕТ ОДНА ФУНКЦИЯ (см. `PlannedHiddenCount`): тогда «сколько плюшек
            // спрятано» и «что именно спрятано» не могут разъехаться, а правило проверяется тестом.
            return PlannedHiddenCount(hasFlow, hasHeatmap, hasClearance, hasZones, hasSingularities);
        }

        /// <summary>
        /// СКОЛЬКО ВИЗУАЛИЗАЦИЙ ПРЯЧЕТ ЧИСТЫЙ ВИД (чистая функция — проверяется тестом без сцены).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНО. Владелец видел на живой сцене «шар тепловой карты и жёлтые/красные
        /// элементы» — то есть плюшек БОЛЬШЕ ОДНОЙ, и каждая включалась своим путём. Число в журнале
        /// («плюшек скрыто N») обязано соответствовать списку; когда оно считалось инкрементами по
        /// ходу дела, выпавшая строка замечалась только глазами оператора.
        ///
        /// Поток этапов даёт ТРИ скрываемых визуализации (зона достижимости, лимиты суставов, панель
        /// метрик), каждая служба функций — по одной. Всего при полном наборе — семь.
        /// </summary>
        public const int FlowVisualsCount = 3;

        /// <summary>Сколько визуализаций спрячет вызов при таком наборе служб (см. `Apply`).</summary>
        public static int PlannedHiddenCount(bool hasFlow, bool hasHeatmap, bool hasClearance,
            bool hasZones, bool hasSingularities)
        {
            int hidden = 0;
            if (hasFlow) hidden += FlowVisualsCount;
            if (hasHeatmap) hidden++;
            if (hasClearance) hidden++;
            if (hasZones) hidden++;
            if (hasSingularities) hidden++;
            return hidden;
        }

        /// <summary>Строка журнала о чистом виде (число + причина).</summary>
        public static string Describe(int hidden)
        {
            return "ЧИСТЫЙ ВИД: плюшек скрыто " + Mathf.Max(0, hidden) + " · " + Reason;
        }
    }
}
