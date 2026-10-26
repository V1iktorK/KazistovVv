using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>Форма панели быстрого выбора у руки (решение владельца [D20]).</summary>
    public enum KvXrQuickShape
    {
        /// <summary>БУБЛИК — кнопки по всему кругу (удобно для 6–12 кнопок).</summary>
        Ring = 0,

        /// <summary>ПОЛУМЕСЯЦ — кнопки по дуге сверху (компактно, не закрывает обзор).</summary>
        Crescent = 1
    }

    /// <summary>У какой руки висит панель быстрого выбора ([D20]: «у левой, для левшей правой»).</summary>
    public enum KvXrQuickHand
    {
        Left = 0,
        Right = 1
    }

    /// <summary>
    /// ГЕОМЕТРИЯ ПАНЕЛИ БЫСТРОГО ВЫБОРА У РУКИ (сессия §32, решение владельца [D20]).
    ///
    /// ЧТО ЗДЕСЬ РЕШЕНО ВЛАДЕЛЬЦЕМ: кнопки быстрого выбора живут НЕ на общей панели сверху,
    /// а **у руки** — слева (для левшей справа), в виде **бублика** (кольцо) или **полумесяца**
    /// (дуга). И форма, и рука настраиваются в настройках.
    ///
    /// ПОЧЕМУ РАСКЛАДКА — ОТДЕЛЬНЫЙ СТАТИЧЕСКИЙ КЛАСС. Это чистая математика углов и смещений,
    /// у неё нет ни GameObject, ни кадра, ни состояния — значит её можно проверить тестами
    /// без шлема (что и сделано в `KvXrOverlayTests`). Всё, что связано с построением кнопок,
    /// живёт в `KvXrQuickPanel`, и там логики как можно меньше.
    ///
    /// СИСТЕМА КООРДИНАТ: углы в градусах, 0° — вправо, 90° — вверх, отсчёт против часовой
    /// стрелки (обычная математическая), а кнопки в кольце идут ПО ЧАСОВОЙ от первой сверху.
    /// Так первая кнопка всегда «под большим пальцем сверху», а дальше порядок предсказуем.
    /// </summary>
    public static class KvXrQuickPanelLayout
    {
        /// <summary>Меньше двух кнопок — это не панель выбора.</summary>
        public const int MinButtons = 2;

        /// <summary>Больше двенадцати кнопок в кольце у руки не читаются (проверено на бумаге).</summary>
        public const int MaxButtons = 12;

        /// <summary>Полный круг для формы «бублик».</summary>
        public const float RingSpanDegrees = 360f;

        /// <summary>Дуга «полумесяца»: 150° — помещается у руки и не закрывает обзор.</summary>
        public const float CrescentSpanDegrees = 150f;

        /// <summary>Направление «вверх» в градусах (математические координаты).</summary>
        public const float UpDegrees = 90f;

        /// <summary>Радиус панели по умолчанию, метры (16 см — под ладонь).</summary>
        public const float DefaultRadiusMeters = 0.16f;

        public const float MinRadiusMeters = 0.10f;
        public const float MaxRadiusMeters = 0.35f;

        /// <summary>Число кнопок, приведённое к допустимому диапазону.</summary>
        public static int ClampCount(int count)
        {
            if (count < MinButtons) return MinButtons;
            if (count > MaxButtons) return MaxButtons;
            return count;
        }

        /// <summary>Допустимое ли число кнопок (для проверок и отчётов).</summary>
        public static bool IsValidCount(int count)
        {
            return count >= MinButtons && count <= MaxButtons;
        }

        /// <summary>Радиус, приведённый к допустимому диапазону.</summary>
        public static float ClampRadius(float radiusMeters)
        {
            if (radiusMeters < MinRadiusMeters) return MinRadiusMeters;
            if (radiusMeters > MaxRadiusMeters) return MaxRadiusMeters;
            return radiusMeters;
        }

        /// <summary>Какую дугу занимает форма: у бублика — полный круг, у полумесяца — дуга.</summary>
        public static float SpanDegrees(KvXrQuickShape shape)
        {
            return shape == KvXrQuickShape.Ring ? RingSpanDegrees : CrescentSpanDegrees;
        }

        /// <summary>
        /// УГОЛ КНОПКИ. У бублика первая кнопка сверху, дальше по часовой стрелке с равным шагом
        /// (при 12 кнопках шаг 30°). У полумесяца кнопки раскладываются по дуге от левого края
        /// к правому, середина дуги — сверху.
        /// </summary>
        public static float AngleDegrees(int index, int count, KvXrQuickShape shape)
        {
            int n = ClampCount(count);
            int i = ((index % n) + n) % n;      // индекс вне диапазона заворачивается, а не падает

            if (shape == KvXrQuickShape.Ring)
            {
                return UpDegrees - i * (RingSpanDegrees / n);
            }

            if (n == 1) return UpDegrees;
            float t = (float)i / (n - 1);                                    // 0 … 1
            return UpDegrees + CrescentSpanDegrees * 0.5f - t * CrescentSpanDegrees;
        }

        /// <summary>Смещение кнопки от центра панели в её плоскости, метры.</summary>
        public static Vector2 OffsetMeters(int index, int count, KvXrQuickShape shape, float radiusMeters)
        {
            float a = AngleDegrees(index, count, shape) * Mathf.Deg2Rad;
            float r = ClampRadius(radiusMeters);
            return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
        }

        /// <summary>
        /// СМЕЩЕНИЕ С УЧЁТОМ РУКИ. У правой руки панель зеркалится по горизонтали: иначе
        /// полумесяц уходил бы в сторону от оператора, а не под ладонь.
        /// </summary>
        public static Vector2 OffsetMeters(int index, int count, KvXrQuickShape shape,
                                           float radiusMeters, KvXrQuickHand hand)
        {
            Vector2 local = OffsetMeters(index, count, shape, radiusMeters);
            if (hand != KvXrQuickHand.Right) return local;
            return new Vector2(-local.x, local.y);
        }

        /// <summary>Зеркальный угол для правой руки (та же логика, что у смещения).</summary>
        public static float MirrorForHand(float angleDegrees, KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Right ? 180f - angleDegrees : angleDegrees;
        }

        /// <summary>Название формы для настроек и журнала — переводится через `KvLoc`.</summary>
        public static string ShapeLabel(KvXrQuickShape shape)
        {
            return shape == KvXrQuickShape.Ring
                ? KazistovVvUI.KvLoc.T("xr.quick.shape.ring", "Бублик")
                : KazistovVvUI.KvLoc.T("xr.quick.shape.crescent", "Полумесяц");
        }

        /// <summary>Название руки для настроек и журнала.</summary>
        public static string HandLabel(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Right
                ? KazistovVvUI.KvLoc.T("xr.quick.hand.right", "Правая")
                : KazistovVvUI.KvLoc.T("xr.quick.hand.left", "Левая");
        }

        /// <summary>Строка для журнала: что именно построено (без домыслов).</summary>
        public static string Describe(KvXrQuickShape shape, KvXrQuickHand hand, int count, float radiusMeters)
        {
            return string.Format("панель у руки: форма {0} · рука {1} · кнопок {2} · радиус {3:0.00} м",
                ShapeLabel(shape), HandLabel(hand), ClampCount(count), ClampRadius(radiusMeters));
        }
    }
}
