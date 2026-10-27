using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ВЫЛЕТ РУКИ — ОДНА ФОРМУЛА НА ВСЮ ПРОГРАММУ (§42). «Вылет = длина цепочки пивотов до TCP» —
    /// та же величина, что считают сингулярностные зоны (`KvSingularityZones.cs:141-153`), вынесена в
    /// одно место, чтобы радиус автостола и пороги зон не разъехались.
    /// Измерено по сцене: 6-осевой — 2,368 м, SCARA LS10-B702S — 0,713 м.
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

        /// <summary>Радиус автостола: 0,95 × вылет + 0,10 м.</summary>
        public static float AutoRadius(float chainLengthMeters)
        {
            return KvXrTableKinds.AutoReachFactor * Mathf.Max(MinChainMeters, chainLengthMeters)
                   + KvXrTableKinds.AutoMarginMeters;
        }
    }
}
