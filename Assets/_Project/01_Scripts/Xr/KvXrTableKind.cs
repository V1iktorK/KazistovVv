using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>ВИД СТОЛА, СОЗДАВАЕМОГО ИЗ МЕНЮ (§42, решение владельца 24–25.10.2026).</summary>
    public enum KvXrTableKind
    {
        /// <summary>Квадратный, как изначальный в ПК-версии: 0,72 × 0,48 м, верх 0,98 м.</summary>
        Square = 0,

        /// <summary>Круглый «авто»: радиус = 0,95 × вылет руки + 0,10 м.</summary>
        Auto = 1
    }
}
