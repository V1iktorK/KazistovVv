using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ПОЛИТИКА ОТСЕЧЕНИЯ КЛЮЧЕВЫХ СЛОВ URP — ЧТО ПРОЕКТ НЕ ИСПОЛЬЗУЕТ, С ПРИЧИНОЙ.
    ///
    /// ЗАЧЕМ ЭТО ПОЯВИЛОСЬ. В сборке `Universal Render Pipeline/Lit` (проход `ForwardLit`) даёт
    /// **442 368 вариантов**, и URP их не срезает, хотя обязан: в его собственном коде
    /// (`Editor/ShaderBuildPreprocessor.cs`, `CreatePrefilteringSettings`) режимы отсечения
    /// вычисляются из настроек URP-ассета. Замерено 08.10.2026: в ассете
    /// `Assets/Settings/Mobile_RPAsset.asset` уже лежат ЗНАЧЕНИЯ ПО УМОЛЧАНИЮ
    /// (`mainLightShadows = SelectAll`, `additionalLights = SelectAll`, остальные `Select`) —
    /// то есть URP считает, что «оставить нужно всё»: мягкие тени выключены, тени дополнительного
    /// света выключены, дополнительный свет повершинный, каскадов один, SSAO в мобильном рендерере
    /// нет, тумана и карт освещения в сценах нет. Поэтому отсечение делается здесь — по тем же
    /// фактам, но с печатью причины для каждого снятого слова.
    ///
    /// ПРАВИЛО БЕЗОПАСНОСТИ: если настройка не прочиталась (файла нет, поле не найдено),
    /// слово НЕ снимается. «Не знаю» никогда не превращается в «отсекаем».
    /// </summary>
    public static class KvUrpKeywordPolicy
    {
        /// <summary>Ассет конвейера для шлема (Android) — из него берутся все настройки.</summary>
        public const string UrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";

        /// <summary>Рендерер мобильного уровня — в нём видно, какие фичи включены (например, SSAO).</summary>
        public const string UrpRendererPath = "Assets/Settings/Mobile_Renderer.asset";

        private static readonly List<string> Keywords = new List<string>();
        private static readonly List<string> Reasons = new List<string>();
        private static bool _built;

        /// <summary>Слова, которые снимаются (после <see cref="Build"/>).</summary>
        public static IList<string> StrippedKeywords { get { Build(); return Keywords; } }

        /// <summary>Отчёт «что снимаем и почему» — попадает в журнал сборки.</summary>
        public static string Describe()
        {
            Build();
            return Reasons.Count == 0
                ? "не снимается ничего (настройки не прочитаны или всё используется)"
                : string.Join(" · ", Reasons.ToArray());
        }

        /// <summary>Снимать ли вариант: достаточно одного неиспользуемого слова.</summary>
        public static bool ShouldStrip(IEnumerable<string> keywordNames)
        {
            Build();
            if (keywordNames == null || Keywords.Count == 0) return false;

            foreach (string name in keywordNames)
            {
                if (string.IsNullOrEmpty(name)) continue;
                for (int i = 0; i < Keywords.Count; i++)
                    if (string.Equals(Keywords[i], name, System.StringComparison.Ordinal)) return true;
            }
            return false;
        }

        /// <summary>Построить политику по фактам из файлов проекта (один раз за запуск редактора).</summary>
        public static void Build()
        {
            if (_built) return;
            _built = true;
            Keywords.Clear();
            Reasons.Clear();

            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(UrpAssetPath);
            if (asset == null)
            {
                Reasons.Add("ассет URP не найден (" + UrpAssetPath + ") — отсечение слов URP выключено");
                return;
            }

            var so = new SerializedObject(asset);
            int additionalLights = ReadInt(so, "m_AdditionalLightsRenderingMode");
            int additionalLightShadows = ReadInt(so, "m_AdditionalLightShadowsSupported");
            int softShadows = ReadInt(so, "m_SoftShadowsSupported");
            int cascadeCount = ReadInt(so, "m_ShadowCascadeCount");

            // 1. ДОПОЛНИТЕЛЬНЫЙ СВЕТ. Режим 0 — выключен, 1 — повершинный (PerVertex), 2 — попиксельный.
            // Слова `_ADDITIONAL_LIGHTS` (попиксельный путь) не нужны при 0 и 1.
            if (additionalLights == 0 || additionalLights == 1)
                Add("_ADDITIONAL_LIGHTS", "дополнительный свет: режим " + additionalLights +
                    " (не попиксельный) — попиксельный путь не исполняется");

            // 2. ТЕНИ ДОПОЛНИТЕЛЬНОГО СВЕТА выключены в ассете — их варианты не нужны.
            if (additionalLightShadows == 0)
                Add("_ADDITIONAL_LIGHT_SHADOWS", "тени дополнительного света выключены в ассете URP");

            // 3. МЯГКИЕ ТЕНИ выключены — четыре состояния качества мягких теней не нужны.
            if (softShadows == 0)
            {
                Add("_SHADOWS_SOFT", "мягкие тени выключены в ассете URP");
                Add("_SHADOWS_SOFT_LOW", "мягкие тени выключены в ассете URP");
                Add("_SHADOWS_SOFT_MEDIUM", "мягкие тени выключены в ассете URP");
                Add("_SHADOWS_SOFT_HIGH", "мягкие тени выключены в ассете URP");
            }

            // 4. КАСКАДЫ: один каскад — значит ветка каскадных теней не исполняется.
            if (cascadeCount == 1)
                Add("_MAIN_LIGHT_SHADOWS_CASCADE", "каскадов теней: " + cascadeCount +
                    " — каскадная ветка не исполняется");

            // 5. SSAO: смотрим ФАКТ — есть ли эта фича в мобильном рендерере.
            string rendererText = ReadFile(UrpRendererPath);
            if (rendererText == null)
                Reasons.Add("рендерер URP не прочитан (" + UrpRendererPath + ") — слово _SCREEN_SPACE_OCCLUSION НЕ снимается");
            else if (rendererText.IndexOf("ScreenSpaceAmbientOcclusion", System.StringComparison.Ordinal) < 0)
                Add("_SCREEN_SPACE_OCCLUSION", "в мобильном рендерере нет фичи SSAO");

            // 6. ТУМАН и 7. КАРТЫ ОСВЕЩЕНИЯ — по сценам сборки (факт, а не догадка).
            bool fogUsed = false, lightmapsUsed = false, scenesRead = false;
            foreach (string scene in BuildScenePaths())
            {
                string text = ReadFile(scene);
                if (text == null) continue;
                scenesRead = true;
                if (text.IndexOf("m_Fog: 1", System.StringComparison.Ordinal) >= 0) fogUsed = true;
                if (text.IndexOf("m_Lightmaps: []", System.StringComparison.Ordinal) < 0) lightmapsUsed = true;
            }

            if (!scenesRead)
            {
                Reasons.Add("сцены сборки не прочитаны — слова тумана и карт освещения НЕ снимаются");
            }
            else
            {
                if (!fogUsed)
                {
                    Add("FOG_LINEAR", "туман выключен во всех сценах сборки");
                    Add("FOG_EXP", "туман выключен во всех сценах сборки");
                    Add("FOG_EXP2", "туман выключен во всех сценах сборки");
                }
                if (!lightmapsUsed)
                {
                    Add("LIGHTMAP_ON", "в сценах сборки нет карт освещения");
                    Add("DIRLIGHTMAP_COMBINED", "в сценах сборки нет карт освещения");
                    Add("DYNAMICLIGHTMAP_ON", "в сценах сборки нет карт освещения");
                    Add("LIGHTMAP_SHADOW_MIXING", "в сценах сборки нет карт освещения");
                    Add("SHADOWS_SHADOWMASK", "в сценах сборки нет карт освещения");
                }
            }

            // 8. ПОЗДНЕЕ (deferred) ОСВЕЩЕНИЕ: мобильный рендерер — прямой (Forward).
            int renderingMode = ReadIntFromText(rendererText, "m_RenderingMode");
            if (renderingMode == 0)
            {
                Add("_FORWARD_PLUS", "рендерер мобильного уровня — прямой (Forward)");
                Add("_CLUSTER_LIGHT_LOOP", "рендерер мобильного уровня — прямой (Forward)");
                Add("_DBUFFER_MRT1", "отложенный рендеринг не используется на мобильном уровне");
                Add("_DBUFFER_MRT2", "отложенный рендеринг не используется на мобильном уровне");
                Add("_DBUFFER_MRT3", "отложенный рендеринг не используется на мобильном уровне");
            }
        }

        // ============================================================ вспомогательное

        private static void Add(string keyword, string reason)
        {
            if (Keywords.Contains(keyword)) return;
            Keywords.Add(keyword);
            Reasons.Add(keyword + " (" + reason + ")");
        }

        private static int ReadInt(SerializedObject so, string propertyName)
        {
            SerializedProperty property = so.FindProperty(propertyName);
            return property != null ? property.intValue : -1;
        }

        private static int ReadIntFromText(string text, string field)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(text, field + @":\s*(\d+)");
            if (!match.Success) return -1;
            int value;
            return int.TryParse(match.Groups[1].Value, out value) ? value : -1;
        }

        private static string ReadFile(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>Сцены, которые идут в сборку (включённые в списке сборки).</summary>
        public static List<string> BuildScenePaths()
        {
            var result = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                if (scene != null && scene.enabled) result.Add(scene.path);
            return result;
        }
    }
}
