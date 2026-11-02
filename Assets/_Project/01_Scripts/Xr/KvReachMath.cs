using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ВЫЛЕТ РУКИ — ОДНА ФОРМУЛА НА ВСЮ ПРОГРАММУ (§42). «Вылет = длина цепочки пивотов до TCP» —
    /// та же величина, что считают сингулярностные зоны (`KvSingularityZones.cs:141-153`), вынесена в
    /// одно место, чтобы радиус автостола и пороги зон не разъехались.
    /// Измерено по сцене: 6-осевой — 2,368 м, SCARA LS10-B702S — 0,713 м.
    ///
    /// ПУНКТ 8 ВЛАДЕЛЬЦА (02.11.2026): «Автостол … должен быть … шире его максимально достигаемой
    /// точки». Поэтому радиус автостола — ПОЛНЫЙ ВЫЛЕТ + ЗАПАС, без коэффициента 0,95.
    /// Прежняя формула 0,95 × вылет + 0,10 у 6-осевого робота давала 2,3496 м против вылета
    /// 2,368 м — стол был на 18 мм УЖЕ рабочей зоны, и рука доставала за кромку.
    /// </summary>
    public static class KvReachMath
    {
        /// <summary>Наименьшая принимаемая длина цепочки, метры (как в зонах: 0,05).</summary>
        public const float MinChainMeters = 0.05f;

        /// <summary>Длина цепочки пивотов до TCP: сумма расстояний между соседними пивотами + отрезок до TCP.</summary>
        public static float ChainLength(Vector3[] pivots, Vector3 tcp)
        {
            if (pivots == null || pivots.Length == 0) return MinChainMeters;

            float len = 0f;
            Vector3 prev = pivots[0];
            for (int i = 1; i < pivots.Length; i++)
            {
                len += Vector3.Distance(prev, pivots[i]);
                prev = pivots[i];
            }
            len += Vector3.Distance(prev, tcp);
            return Mathf.Max(MinChainMeters, len);
        }

        /// <summary>
        /// Радиус автостола: ПОЛНЫЙ вылет руки + запас 0,10 м. Коэффициента уменьшения здесь быть не
        /// должно: стол обязан быть ШИРЕ максимально достигаемой точки робота, а не уже неё.
        /// </summary>
        public static float AutoRadius(float chainLengthMeters)
        {
            return Mathf.Max(MinChainMeters, chainLengthMeters) + KvXrTableKinds.AutoMarginMeters;
        }

        /// <summary>
        /// Наибольший вылет среди роботов сцены (пусто/нет данных — 0).
        /// Нужен, когда в шлеме при запуске пусто и активного робота ещё нет: автостол строится по
        /// САМОЙ ДАЛЬНЕЙ рабочей зоне, а не по габариту основания (пункт 8 владельца).
        /// </summary>
        public static float MaxReach(float[] reaches)
        {
            if (reaches == null) return 0f;

            float best = 0f;
            for (int i = 0; i < reaches.Length; i++)
                if (reaches[i] > best) best = reaches[i];
            return best;
        }
    }
}
