using UnityEngine;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ОПТИМИЗАЦИЯ MR (пункт плана работ 17.11.2026): «работать в MR не реально — слайд-шоу».
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ КЛАСС, А НЕ ПРАВКА ПРЕСЕТА. Пресет качества — это ВЫБОР ОПЕРАТОРА
    /// (он сохраняется в PlayerPrefs и обязан переживать переключения режима). MR — это другая
    /// ЦЕНА КАДРА: к камере приходит поток реальной комнаты, поэтому тот же профиль в MR стоит
    /// заметно дороже, чем в VR. Класс лежит МЕЖДУ ними: в MR служба графики применяет дешёвую
    /// КОПИЮ профиля, в VR — оригинал. Профиль оператора при этом не переписывается (за это
    /// отвечает `KvGraphicsService.EffectiveProfileFor`).
    ///
    /// ЧТО ДЕШЕВЛЕЕ В MR:
    ///   • дорогие эффекты постобработки — SSAO, SSR, объёмный туман, Bloom, размытие в
    ///     движении, глубина резкости, трассировка лучей, отражения реального времени
    ///     (`ApplyEffectsTo` — вот что реально применяет рантайм);
    ///   • НЕ применяется к рантайму больше: масштаб отрисовки и тени. Требование владельца
    ///     19.11.2026 «не снижать графику/разрешение/тени — качество обратно»: полный пресет
    ///     (`ApplyTo`, потолки <see cref="MaxRenderScale"/> и др.) оставлен как опция,
    ///     закреплённая тестом, но в шове `KvGraphicsService.EffectiveProfileFor` не вызывается.
    ///
    /// ВТОРОЙ КУСОК — ТРОТТЛИНГ UI/HUD: интервал <see cref="UiUpdateIntervalSeconds"/> длиннее
    /// в MR, чем в VR. Он читается из `Update`-путей панелей (`KvGamepadHud`), где иначе каждый
    /// кадр аллоцируются строки состояния (Docs/PERFORMANCE_AUDIT.md, §3.1).
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: ЗАМЕРА FPS. Кадровую частоту даёт только шлем; обещать цифры без замера
    /// нельзя. Здесь — поведение, которое проверяется на стенде без устройства.
    /// </summary>
    public static class KvMrPerformance
    {
        /// <summary>Потолок масштаба отрисовки в MR (ниже — пиксели на кадр дешевле).</summary>
        public const float MaxRenderScale = 0.8f;

        /// <summary>Потолок качества теней в MR (0 — низкое, 3 — ультра).</summary>
        public const int MaxShadowQuality = 1;

        /// <summary>Потолок каскадов теней в MR.</summary>
        public const int MaxShadowCascades = 2;

        /// <summary>Потолок дистанции теней в MR, метры.</summary>
        public const float MaxShadowDistance = 40f;

        /// <summary>Потолок разрешения карты теней в MR, пикселей.</summary>
        public const int MaxShadowResolution = 1024;

        /// <summary>Интервал обновления UI/HUD в MR, секунд.</summary>
        public const float UiIntervalMr = 0.35f;

        /// <summary>
        /// Интервал обновления UI/HUD в VR, секунд. Ненулевой и в VR: строки в `Update`
        /// аллоцируются каждый кадр и кормят сборщик мусора (см. §3.1 аудита).
        /// </summary>
        public const float UiIntervalVr = 0.05f;

        /// <summary>Сейчас выбран режим MR (то же хранилище, что и весь ветка MR).</summary>
        public static bool IsMrMode()
        {
            return KazistovVvXr.KvXrSettings.GetModeIndex() == KazistovVvXr.KvXrSettings.ModeMr;
        }

        /// <summary>
        /// ПРИМЕНИТЬ ПРЕСЕТ К ПРОФИЛЮ. Возвращает `true`, если профиль изменён
        /// (в VR — всегда `false`, профиль не трогается).
        /// </summary>
        public static bool ApplyIfMr(KvGraphicsProfile profile)
        {
            if (!IsMrMode()) return false;
            ApplyTo(profile);
            return true;
        }

        /// <summary>
        /// УДЕШЕВИТЬ ПРОФИЛЬ ДЛЯ MR (ПОЛНЫЙ ПРЕСЕТ — ОПЦИЯ). Вызывается ТОЛЬКО когда режим MR —
        /// сама функция режим не читает, поэтому её можно проверить на любом профиле, без
        /// настройки окружения.
        ///
        /// ЧЕСТНО О РАНЕЙШЕМ РЕЖИМЕ. 17.11.2026 этот метод снижал и масштаб отрисовки, и тени.
        /// Владелец 19.11.2026: «не снижать графику/разрешение/тени — качество обратно».
        /// Поэтому РАНТАЙМ-ШОВ (`KvGraphicsService.EffectiveProfileFor`) больше не зовёт этот
        /// метод: в шлеме масштаб и тени остаются профиля оператора (см. `ApplyEffectsTo`).
        /// Сам метод сохранён как проверяемая опция «дешёвый профиль» — его поведение
        /// закреплено тестом `KvMrOptimizationTests` и менять его без решения владельца нельзя.
        /// </summary>
        public static void ApplyTo(KvGraphicsProfile p)
        {
            if (p == null) return;

            // --- разрешение и тени: меньше пикселей и меньше работы на каскады
            if (p.renderScale > MaxRenderScale) p.renderScale = MaxRenderScale;
            if (p.shadowQuality > MaxShadowQuality) p.shadowQuality = MaxShadowQuality;
            if (p.shadowCascades > MaxShadowCascades) p.shadowCascades = MaxShadowCascades;
            if (p.shadowDistance > MaxShadowDistance) p.shadowDistance = MaxShadowDistance;
            if (p.shadowResolution > MaxShadowResolution) p.shadowResolution = MaxShadowResolution;

            ApplyEffectsTo(p);
        }

        /// <summary>
        /// ВЫКЛЮЧИТЬ ТОЛЬКО ДОРОГУЮ ПОСТОБРАБОТКУ — масштаб отрисовки и тени НЕ ТРОГАЮТСЯ.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ МЕТОД. Это ровно тот кусок пресета, который остаётся в рантайме:
        /// постобработка стоит кадра (полноэкранные проходы), а масштаб и тени владелец
        /// считает качеством и требует вернуть. Метод чистый (без чтения режима) — проверяется
        /// на любом профиле.
        /// </summary>
        public static void ApplyEffectsTo(KvGraphicsProfile p)
        {
            if (p == null) return;

            p.ssao = false;
            p.ssr = false;
            p.reflectionMode = 0;
            p.volumetricFog = false;
            p.bloom = false;
            p.motionBlur = false;
            p.depthOfField = false;
            p.rayTracing = false;
        }

        /// <summary>
        /// НИ ОДИН ДОРОГОЙ ЭФФЕКТ НЕ ОСТАЛСЯ ВКЛЮЧЕННЫМ. Чистая проверка для теста и для
        /// диагностики: «MR-пресет применён» обязано означать именно это, а не «часть выключил».
        /// </summary>
        public static bool ExpensiveEffectsOff(KvGraphicsProfile p)
        {
            if (p == null) return false;
            return !p.ssao && !p.ssr && !p.volumetricFog && !p.bloom && !p.motionBlur &&
                   !p.depthOfField && !p.rayTracing && p.reflectionMode == 0;
        }

        /// <summary>Интервал обновления UI/HUD: в MR дольше, чем в VR (троттлинг).</summary>
        public static float UiUpdateIntervalSeconds()
        {
            return IsMrMode() ? UiIntervalMr : UiIntervalVr;
        }

        /// <summary>
        /// ЗАМОК КАДРА: пропустить ли обновление сейчас. `interval &lt;= 0` — каждый кадр
        /// (без троттлинга). `last` хранится вызывающим и переживает кадры.
        /// </summary>
        public static bool Due(float now, float interval, ref float last)
        {
            if (interval <= 0f)
            {
                last = now;
                return true;
            }
            if (now - last < interval) return false;
            last = now;
            return true;
        }

        /// <summary>
        /// ЗАМОК СТРОКИ: `true` — значение изменилось и его надо записать в текст (и только тогда
        /// аллоцируется строка). Без изменения — ни одной аллокации и ни одного вызова сеттера
        /// `Text.text`, а значит и ни одного лишнего пересобирания канваса (оптимизация MR,
        /// 19.11.2026: `KvUiStates.Update` собирал строку прогресса каждый кадр).
        /// </summary>
        public static bool TextChanged(ref string cache, string value)
        {
            if (cache == value) return false;   // сравнение строк без аллокаций, null-безопасно
            cache = value;
            return true;
        }

        /// <summary>
        /// ЗАМОК ЧИСЛА: тот же смысл для процентов/счётчиков — переписывать текст только при
        /// смене числа, а не каждый кадр.
        /// </summary>
        public static bool Changed(ref int cache, int value)
        {
            if (cache == value) return false;
            cache = value;
            return true;
        }

        /// <summary>
        /// СКОЛЬКО НАСТРОЕК КАЧЕСТВА ПРОФИЛЯ ОСТАЁТСЯ У ОПЕРАТОРА В MR (число для строки метрики
        /// «MR: настроек качества N · GC/кадр ~0 · пересборок UI/сек M»).
        ///
        /// СЧИТАЕТСЯ ОТ РЕФЛЕКСИИ, А НЕ ЗАПОМИНАЕТСЯ РУКОЙ: если в `KvGraphicsProfile` появится
        /// новое поле, число обновится само, а тест сверит его с числом НЕ тронутых пресетом
        /// полей. Вычитаются только 8 флагов постобработки (см. `ApplyEffectsTo`).
        /// </summary>
        public static int TotalProfileSettings()
        {
            return typeof(KvGraphicsProfile).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).Length;
        }

        /// <summary>Флагов постобработки, которые пресет выключает в MR.</summary>
        public const int PostEffectsCutInMr = 8;

        /// <summary>Настроек качества, сохраняемых в MR: всего полей профиля минус постобработка.</summary>
        public static int QualitySettingsKept()
        {
            return TotalProfileSettings() - PostEffectsCutInMr;
        }
    }
}
