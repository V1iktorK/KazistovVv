using System.Collections.Generic;
using System.IO;
using UnityEditor.Build;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ОТСЕЧЕНИЕ ВАРИАНТОВ ПОСТОБРАБОТКИ ПРИ СБОРКЕ — карточка Б2 (Этап А).
    ///
    /// ЧТО БЫЛО ИЗМЕРЕНО (сборка §30, лог `Logs\xr-build-apk-s30.log`):
    /// шейдер `Hidden/Universal Render Pipeline/UberPost` компилировался как **92 160 вариантов
    /// на каждый из двух проходов**, `FinalPost` — 2 560 × 4 прохода. Из ~122 000 вариантов всей
    /// сборки на постобработку пришлось ~105 000, и сборка шла 46 часов, не дойдя до конца.
    ///
    /// ПОЧЕМУ ШТАТНОЕ ОТСЕЧЕНИЕ URP НЕ ПОМОГЛО. В URP отсечение постобработки включено
    /// (`UniversalRenderPipelineGlobalSettings` → `m_StripUnusedPostProcessingVariants: 1`),
    /// но работает оно так (`URP/Editor/ShaderBuildPreprocessor.cs`, `GetSupportedFeaturesFromVolumes`):
    /// берутся ВСЕ профили объёма в `Assets/` и объединяются их эффекты. В проекте профилей три,
    /// и в каждом есть Bloom, DepthOfField, MotionBlur, LensDistortion, ChromaticAberration,
    /// FilmGrain, PaniniProjection, Tonemapping — поэтому набор эффектов получается полным,
    /// и отсекать URP нечего. Убирать эти компоненты из профилей нельзя: профиль HDRP
    /// (`Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset`) используется рабочим
    /// столом, и его правка изменила бы вид на компьютере — а этого владелец не разрешал.
    ///
    /// ПОЧЕМУ ОТСЕЧЕНИЕ ЗДЕСЬ БЕЗОПАСНО. В XR-сцене (единственной, что идёт в APK, вместе
    /// с MainScene) ни одна камера не рисует постобработку: у камеры нет компонента
    /// `UniversalAdditionalCameraData`, а умолчание URP для него — `m_RenderPostProcessing = false`
    /// (проверено в исходнике пакета). Значит, варианты эффектов постобработки в этой сборке
    /// не исполняются ВООБЩЕ. Проверка «рисуется ли постобработка» берётся ИЗ ДАННЫХ (по файлам
    /// сцен), а не из головы: если постобработку включат, отсечение само перестанет работать.
    ///
    /// ЭТАП 1 (этот файл — КРАСНАЯ точка): решение об отсечении ещё НЕ реализовано —
    /// `ProjectHasNoPostProcessingCameras` возвращает false, то есть отсечения нет и поведение
    /// сборки прежнее. Тесты `KvShaderVariantTests.cs` на этом обязаны падать (правило [K5]).
    /// </summary>
    public sealed class KvPostProcessVariantStripper : IPreprocessShaders
    {
        /// <summary>Порядок вызова: до стандартных отсечений URP полезно не иметь значения,
        /// важно лишь, что вызов один и результат детерминирован.</summary>
        public int callbackOrder { get { return 0; } }

        /// <summary>Шейдеры постобработки URP, на которых и происходит взрыв вариантов.</summary>
        public const string UberPostShader = "Hidden/Universal Render Pipeline/UberPost";

        /// <summary>Второй дорогой шейдер постобработки (финальный проход).</summary>
        public const string FinalPostShader = "Hidden/Universal Render Pipeline/FinalPost";

        /// <summary>
        /// КЛЮЧЕВЫЕ СЛОВА ЭФФЕКТОВ ПОСТОБРАБОТКИ, КОТОРЫЕ ПРОЕКТ НЕ РИСУЕТ.
        ///
        /// Каждое слово — независимый выключатель в шейдере, и вместе они дают произведение
        /// наборов (отсюда 92 160). Список составлен по объявлениям `#pragma multi_compile`
        /// самих шейдеров URP, а не по догадке: `_DISTORTION`, `_CHROMATIC_ABERRATION`,
        /// `_BLOOM_LQ/_BLOOM_HQ/_BLOOM_LQ_DIRT/_BLOOM_HQ_DIRT`, `_HDR_GRADING`, `_TONEMAP_ACES`,
        /// `_TONEMAP_NEUTRAL`, `_FILM_GRAIN`, `_DITHERING`, `_FXAA`, `HDR_INPUT`, `HDR_ENCODING`.
        ///
        /// ЧТО НЕ ТРОГАЕТСЯ И ПОЧЕМУ: базовые слова (`_GAMMA_20`, `_LINEAR_TO_SRGB_CONVERSION`,
        /// `_USE_FAST_SRGB_LINEAR_CONVERSION`, `_POINT_SAMPLING`, `_ENABLE_ALPHA_OUTPUT`,
        /// `_RCAS`, `_EASU_RCAS_AND_HDR_INPUT`) — это не эффекты, а преобразования цвета
        /// и режимы выборки: без них собралась бы картинка неверного цвета.
        /// </summary>
        public static readonly string[] EffectKeywords =
        {
            "_DISTORTION",
            "_CHROMATIC_ABERRATION",
            "_BLOOM_LQ",
            "_BLOOM_HQ",
            "_BLOOM_LQ_DIRT",
            "_BLOOM_HQ_DIRT",
            "_HDR_GRADING",
            "_TONEMAP_ACES",
            "_TONEMAP_NEUTRAL",
            "_FILM_GRAIN",
            "_DITHERING",
            "_FXAA",
            "HDR_INPUT",
            "HDR_ENCODING"
        };

        // ============================================================ решение по варианту

        /// <summary>
        /// ЧИСТОЕ РЕШЕНИЕ: снимать ли этот вариант. Отдельная функция — чтобы её можно было
        /// проверить тестами без запуска сборки (сборка идёт часами, решение — мгновенно).
        ///
        /// Вариант снимается, только если ОБА условия истинны: постобработку никто не рисует
        /// И вариант требует хотя бы одного ключевого слова неиспользуемого эффекта.
        /// </summary>
        public static bool ShouldStripVariant(bool postProcessingRendered, IEnumerable<string> keywordNames)
        {
            if (postProcessingRendered) return false;
            if (keywordNames == null) return false;

            foreach (string name in keywordNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                for (int i = 0; i < EffectKeywords.Length; i++)
                    if (string.Equals(EffectKeywords[i], name, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Считать ли шейдер шейдером постобработки (по имени — других признаков у шейдера нет).</summary>
        public static bool IsPostProcessingShader(string shaderName)
        {
            if (string.IsNullOrEmpty(shaderName)) return false;
            return string.Equals(shaderName, UberPostShader, System.StringComparison.Ordinal) ||
                   string.Equals(shaderName, FinalPostShader, System.StringComparison.Ordinal);
        }

        /// <summary>
        /// РИСУЕТ ЛИ ПРОЕКТ ПОСТОБРАБОТКУ — берётся ИЗ ДАННЫХ: по файлам сцен, которые идут
        /// в сборку. Признак — включённый флаг `m_RenderPostProcessing: 1` у камеры
        /// (`UniversalAdditionalCameraData`). Если такого флага нет ни в одной сцене сборки —
        /// постобработка не рисуется, и варианты эффектов не нужны.
        ///
        /// ЭТАП 1 (красная точка): реализации нет — считается, что постобработка рисуется,
        /// поэтому отсечение НЕ срабатывает и поведение сборки остаётся прежним.
        /// </summary>
        public static bool ProjectHasNoPostProcessingCameras()
        {
            return false;
        }

        // ============================================================ вызов от сборки

        public void OnProcessShader(Shader shader, ShaderSnippetData snippet, IList<ShaderCompilerData> data)
        {
            if (shader == null || data == null || data.Count == 0) return;
            if (!IsPostProcessingShader(shader.name)) return;

            bool postProcessingRendered = !ProjectHasNoPostProcessingCameras();
            if (postProcessingRendered) return;

            int before = data.Count;
            for (int i = data.Count - 1; i >= 0; i--)
            {
                if (!ShouldStripVariant(false, KeywordNames(data[i].shaderKeywordSet))) continue;
                data.RemoveAt(i);
            }

            int removed = before - data.Count;
            if (removed > 0)
            {
                Debug.Log("[KvStrip] " + shader.name + " · проход " + snippet.passType +
                          ": вариантов " + before + " → " + data.Count + " (снято " + removed +
                          " — эффекты постобработки в этой сборке не рисуются)");
            }
        }

        /// <summary>Имена ключевых слов варианта (для чистого решения нужны именно имена).</summary>
        private static List<string> KeywordNames(ShaderKeywordSet set)
        {
            var names = new List<string>(16);
            foreach (ShaderKeyword keyword in set.GetShaderKeywords())
                names.Add(keyword.name);
            return names;
        }
    }
}
