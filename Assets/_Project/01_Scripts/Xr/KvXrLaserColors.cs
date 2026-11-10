using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ЦВЕТА ЛАЗЕРОВ РУК — КРАСНЫЙ ВЫБИРАЕТ ТОЧКУ, ЗЕЛЁНЫЙ ВЫБИРАЕТ ТРАЕКТОРИЮ
    /// (указания владельца 24.10.2026, подтверждено 12.11.2026 пунктами 4–5).
    ///
    /// ОТКУДА ВЗЯТО. На компьютере два инструмента и два лазера (`KazistovVvUIManager.cs:838-870`):
    ///   • ЛЕВАЯ рука — «Выбор точки (красный лазер)»;
    ///   • ПРАВАЯ рука — «Выбор траектории (зелёный лазер)».
    /// В шлеме до сих пор оба луча были голубыми (0.55; 0.75; 1.00), а при наведении красились в
    /// зелёный (0.30; 0.95; 0.55) — отсюда и «зелёное пятно», которое владелец не связывал ни с одной
    /// рукой. Теперь цвет говорит, ЧЬЯ это рука и ЧТО она делает, и не меняет смысл при наведении.
    ///
    /// ЧТО ИЗМЕНИЛОСЬ 12.11.2026. Оттенки переехали в ОДНИ именованные константы
    /// `KvXrRayHitRules.PointLaserColor` (красный = точка) и `KvXrRayHitRules.TrajectoryLaserColor`
    /// (зелёный = траектория) — с объяснением, что каждый значит. Здесь остаётся только ПРАВИЛО
    /// ЯРКОСТИ. Причина: цвет задавался в трёх местах (тема, вершины линии, материал), и владелец
    /// дважды видел «не те» лучи. Теперь источник цвета один.
    ///
    /// ПРАВИЛО НАВЕДЕНИЯ (решение владельца): цвет остаётся своим, меняется только яркость —
    /// тусклый луч 0,55 непрозрачности, луч на цели 0,95 и светлее на 15 %. Белым не подсвечиваем:
    /// иначе снова теряется, какая рука чем работает.
    /// </summary>
    public static class KvXrLaserColors
    {
        /// <summary>Непрозрачность луча, который ни на что не наведён.</summary>
        public const float IdleAlpha = 0.55f;

        /// <summary>Непрозрачность луча на цели.</summary>
        public const float HoverAlpha = 0.95f;

        /// <summary>Во сколько раз светлее луч на цели.</summary>
        public const float HoverGain = 1.15f;

        /// <summary>
        /// ЦВЕТ РУКИ БЕЗ НАВЕДЕНИЯ: правая — зелёный, левая — красный (как на компьютере).
        ///
        /// ЧТО ИЗМЕНИЛОСЬ 12.11.2026 (пункты 4–5 владельца). Оттенок больше НЕ берётся из темы
        /// интерфейса «как есть»: он приходит из ОДНИХ констант `KvXrRayHitRules`
        /// (`PointLaserColor` — красный = точка, `TrajectoryLaserColor` — зелёный = траектория).
        /// ПОЧЕМУ: владелец дважды просил перекрасить лучи, и причина каждый раз была одна — цвет
        /// задавался в трёх местах по-разному (тема, вершины линии, материал). Пока значение одно,
        /// «перекрасить обратно» можно только осознанно, и тест видит каждую попытку.
        /// </summary>
        public static Color Base(KvXrQuickHand hand)
        {
            return KvXrRayHitRules.LaserColorOf(hand);
        }

        /// <summary>
        /// ТОТ ЖЕ ОТТЕНОК С ДРУГОЙ ПРОЗРАЧНОСТЬЮ. Нужен там, где оттенок обязан быть из константы
        /// роли (красный = точка, зелёный = траектория), а яркость — по общему правилу наведения.
        /// Иначе пришлось бы держать два независимых источника цвета на один луч.
        /// </summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        /// <summary>ЦВЕТ ЛУЧА: тот же оттенок, разная яркость. Наведение не меняет цвет руки.</summary>
        public static Color Ray(KvXrQuickHand hand, bool hovering)
        {
            Color color = Base(hand);
            float alpha = hovering ? HoverAlpha : IdleAlpha;
            float gain = hovering ? HoverGain : 1f;

            return new Color(
                Mathf.Clamp01(color.r * gain),
                Mathf.Clamp01(color.g * gain),
                Mathf.Clamp01(color.b * gain),
                alpha);
        }

        /// <summary>Название цвета для журнала: «зелёный» (правая) / «красный» (левая).</summary>
        public static string Label(KvXrQuickHand hand)
        {
            return KvXrRayHitRules.LaserColorName(hand);
        }

        /// <summary>
        /// ПЛАВНЫЙ ПЕРЕХОД ОТ ТУСКЛОГО К ЯРКОМУ (для нажатия взглядом, где яркость копится).
        /// Тот же оттенок руки, меняется только яркость — цвет руки не подменяется никогда.
        /// </summary>
        public static Color RayProgress(KvXrQuickHand hand, float progress)
        {
            return Color.Lerp(Ray(hand, false), Ray(hand, true), Mathf.Clamp01(progress));
        }

        /// <summary>Рука, которой принадлежит ВТОРОЙ луч (указатель) — противоположная основной.</summary>
        public static KvXrQuickHand Opposite(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Right ? KvXrQuickHand.Left : KvXrQuickHand.Right;
        }
    }
}
