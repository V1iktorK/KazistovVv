using System.Collections.Generic;
using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// РАЗМЕРЫ ДВУХ СТОЛОВ (§42, решение владельца 25.10.2026; глубина обновлена 01.11.2026).
    /// Стол ПК в сцене — 14,4 × 10,08 м (константы `StandBuilder`); такие размеры
    /// БОЛЬШЕ автостола, поэтому «заметно меньше авто» при них невыполнимо. Взяты числа владельца —
    /// это те же константы, уменьшенные в 20 раз: пропорция стола ПК сохранена.
    ///
    /// ИЗМЕНЕНИЕ 01.11.2026. Раньше проекция была 0,72 × 0,48 (3:2), потому что стол ПК был
    /// 14,4 × 9,6. Владелец решил подогнать виртуальный стол под РЕАЛЬНЫЙ 100 × 70 см (10:7),
    /// поэтому глубина стола стала 10,08 м, а проекция — 0,504 м. Пропорция сохранена точно:
    /// 0,72 / 0,504 = 10/7, как и 14,4 / 10,08.
    /// </summary>
    public static class KvXrTableKinds
    {
        public const float SquareWidth = 0.72f;      // 14,4 / 20
        public const float SquareDepth = 0.504f;     // 10,08 / 20 — обновлено 01.11.2026 (пропорция 10:7)
        public const float TopHeight = TrajectoryCore.StandBuilder.TopHeight;
        public const float AutoReachFactor = 0.95f;
        public const float AutoMarginMeters = 0.10f;

        /// <summary>Толщина столешницы создаваемого стола, метры (как у стендов ПК).</summary>
        public const float TopThickness = 0.05f;

        /// <summary>Диагональ квадратного стола, метры.</summary>
        public static float SquareDiagonal
        {
            get { return Mathf.Sqrt(SquareWidth * SquareWidth + SquareDepth * SquareDepth); }
        }

        /// <summary>Радиус стола: у квадратного — половина диагонали, у авто — по вылету руки.</summary>
        public static float Radius(KvXrTableKind kind, float robotReachMeters)
        {
            if (kind == KvXrTableKind.Square) return SquareDiagonal * 0.5f;
            return KvReachMath.AutoRadius(robotReachMeters);
        }

        /// <summary>Строка меню — «как в меню, только со своими параметрами».</summary>
        public static string Label(KvXrTableKind kind)
        {
            if (kind == KvXrTableKind.Square) return "Стол (квадратный, как в ПК) 0,72 × 0,48 м";
            return "Авто стол (радиус = вылет робота + 0,10 м)";
        }

        /// <summary>Строка журнала после создания стола — числа обязательны.</summary>
        public static string DescribeCreate(KvXrTableKind kind, float robotReachMeters)
        {
            if (kind == KvXrTableKind.Square)
                return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                    "[KvXr] СТОЛ: создан квадратный {0:0.00} × {1:0.00} м · диагональ {2:0.00} м · верх {3:0.00} м",
                    SquareWidth, SquareDepth, SquareDiagonal, TopHeight);

            float radius = KvReachMath.AutoRadius(robotReachMeters);
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "[KvXr] СТОЛ: авторадиус {0:0.00} м = {1:0.00} × вылета {2:0.00} м + {3:0.00} м",
                radius, AutoReachFactor, robotReachMeters, AutoMarginMeters);
        }
    }
}
