using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ЦВЕТА ЛАЗЕРОВ РУК — КАК В КОМПЬЮТЕРНОЙ ВЕРСИИ (указание владельца 24.10.2026: «перекрасим
    /// лазер правого и левого контроллеров как в комп версии»).
    ///
    /// ОТКУДА ВЗЯТО. На компьютере два инструмента и два лазера (`KazistovVvUIManager.cs:838-870`):
    ///   • ЛЕВАЯ рука — «Выбор точки (красный лазер)», оттенок `KvTheme.LaserRed`;
    ///   • ПРАВАЯ рука — «Выбор траектории (зелёный лазер)», оттенок `KvTheme.LaserGreen`.
    /// В шлеме до сих пор оба луча были голубыми (0.55; 0.75; 1.00), а при наведении красились в
    /// зелёный (0.30; 0.95; 0.55) — отсюда и «зелёное пятно», которое владелец не связывал ни с одной
    /// рукой. Теперь цвет говорит, ЧЬЯ это рука, и не меняет смысл при наведении.
    ///
    /// ПРАВИЛО НАВЕДЕНИЯ (решение владельца): цвет остаётся своим, меняется только яркость —
    /// тусклый луч 0,55 непрозрачности, луч на цели 0,95 и светлее на 15 %. Белым не подсвечиваем:
    /// иначе снова теряется, какая рука чем работает.
    ///
    /// Числа — не «на глаз»: оттенки берутся из темы интерфейса (`KvTheme.LaserRed`/`LaserGreen`),
    /// той же, что и на компьютере, чтобы цвета совпадали в двух местах.
    /// </summary>
    public static class KvXrLaserColors
    {
        /// <summary>Непрозрачность луча, который ни на что не наведён.</summary>
        public const float IdleAlpha = 0.55f;

        /// <summary>Непрозрачность луча на цели.</summary>
        public const float HoverAlpha = 0.95f;

        /// <summary>Во сколько раз светлее луч на цели.</summary>
        public const float HoverGain = 1.15f;

        /// <summary>ЦВЕТ РУКИ БЕЗ НАВЕДЕНИЯ: правая — зелёный, левая — красный (как на компьютере).</summary>
        public static Color Base(KvXrQuickHand hand)
        {
            return hand == KvXrQuickHand.Right
                ? KazistovVvUI.KvTheme.LaserGreen
                : KazistovVvUI.KvTheme.LaserRed;
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
            return hand == KvXrQuickHand.Right ? "зелёный" : "красный";
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
