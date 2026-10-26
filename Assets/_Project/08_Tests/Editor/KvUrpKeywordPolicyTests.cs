using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NUnit.Framework;
using KazistovVvXrEditor;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПОЛИТИКИ ОТСЕЧЕНИЯ КЛЮЧЕВЫХ СЛОВ URP (карточка Б2, Этап А).
    ///
    /// ЗАЧЕМ. В сборке `Universal Render Pipeline/Lit` (проход `ForwardLit`) даёт 442 368 вариантов,
    /// и URP их не срезает: в его ассете лежат значения по умолчанию, то есть «оставить всё».
    /// Поэтому отсечение делает свой проход (`KvUrpKeywordPolicy`) — по фактам из настроек проекта.
    ///
    /// ЧТО ИМЕННО ПРОВЕРЯЕТСЯ (и почему так, а не «просто числами»): тесты ЧИТАЮТ те же файлы
    /// проекта, что и политика, и требуют СОГЛАСОВАННОСТИ: если в ассете URP мягкие тени выключены —
    /// слово `_SHADOWS_SOFT` обязано сниматься; если в сценах нет тумана — слова тумана обязаны
    /// сниматься; и наоборот: то, что проект ИСПОЛЬЗУЕТ (тени главного света, куки, отражения),
    /// снимать НЕЛЬЗЯ. Так тест не ломается от смены настроек, но ловит рассинхрон «настройка
    /// изменилась, а отсечение осталось прежним».
    /// </summary>
    public class KvUrpKeywordPolicyTests
    {
        private static string ProjectRoot { get { return Directory.GetParent(Application.dataPath).FullName; } }

        private static string Read(string relativePath)
        {
            string path = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        private static int ReadInt(string text, string field)
        {
            if (string.IsNullOrEmpty(text)) return -1;
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(text, field + @":\s*(\d+)");
            if (!match.Success) return -1;
            int value;
            return int.TryParse(match.Groups[1].Value, out value) ? value : -1;
        }

        // ==================================================== 1. ТЕНИ, КОТОРЫХ НЕТ

        /// <summary>
        /// ЧЕГО НЕТ В НАСТРОЙКАХ — ТОГО НЕ ДОЛЖНО БЫТЬ В СБОРКЕ. Ассет URP прямо говорит:
        /// мягкие тени выключены, тени дополнительного света выключены, дополнительный свет
        /// повершинный, каскад один. Значит, соответствующие слова обязаны сниматься.
        /// </summary>
        [Test]
        public void Policy_StripsShadowAndLightKeywords_ThatAssetDisables()
        {
            string asset = Read(KvUrpKeywordPolicy.UrpAssetPath);
            Assert.IsNotNull(asset, "не прочитан " + KvUrpKeywordPolicy.UrpAssetPath);

            int additionalLightShadows = ReadInt(asset, "m_AdditionalLightShadowsSupported");
            int softShadows = ReadInt(asset, "m_SoftShadowsSupported");
            int additionalLights = ReadInt(asset, "m_AdditionalLightsRenderingMode");
            int cascades = ReadInt(asset, "m_ShadowCascadeCount");

            Assert.GreaterOrEqual(additionalLightShadows, 0, "в ассете URP нет поля m_AdditionalLightShadowsSupported");
            Assert.GreaterOrEqual(softShadows, 0, "в ассете URP нет поля m_SoftShadowsSupported");
            Assert.GreaterOrEqual(additionalLights, 0, "в ассете URP нет поля m_AdditionalLightsRenderingMode");
            Assert.GreaterOrEqual(cascades, 0, "в ассете URP нет поля m_ShadowCascadeCount");

            if (additionalLightShadows == 0)
                Assert.IsTrue(ContainsKeyword("_ADDITIONAL_LIGHT_SHADOWS"),
                    "тени дополнительного света выключены в ассете URP, а слово _ADDITIONAL_LIGHT_SHADOWS не снимается");

            if (softShadows == 0)
                foreach (string keyword in new[] { "_SHADOWS_SOFT", "_SHADOWS_SOFT_LOW", "_SHADOWS_SOFT_MEDIUM", "_SHADOWS_SOFT_HIGH" })
                    Assert.IsTrue(ContainsKeyword(keyword),
                        "мягкие тени выключены в ассете URP, а слово " + keyword + " не снимается");

            if (additionalLights == 0 || additionalLights == 1)
                Assert.IsTrue(ContainsKeyword("_ADDITIONAL_LIGHTS"),
                    "дополнительный свет не попиксельный, а слово _ADDITIONAL_LIGHTS (попиксельный путь) не снимается");

            if (cascades == 1)
                Assert.IsTrue(ContainsKeyword("_MAIN_LIGHT_SHADOWS_CASCADE"),
                    "каскад один, а слово _MAIN_LIGHT_SHADOWS_CASCADE не снимается");
        }

        // ==================================================== 2. ТУМАН И КАРТЫ ОСВЕЩЕНИЯ

        /// <summary>
        /// В СЦЕНАХ СБОРКИ НЕТ ТУМАНА И КАРТ ОСВЕЩЕНИЯ — значит, слова тумана и карт освещения
        /// не нужны. Тест читает те же сцены, что и политика.
        /// </summary>
        [Test]
        public void Policy_StripsFogAndLightmapKeywords_WhenScenesDoNotUseThem()
        {
            List<string> scenes = KvUrpKeywordPolicy.BuildScenePaths();
            Assert.Greater(scenes.Count, 0, "список сцен сборки пуст — политику проверить нельзя");

            bool scenesRead = false, fogUsed = false, lightmapsUsed = false;
            foreach (string scene in scenes)
            {
                string text = Read(scene);
                if (text == null) continue;
                scenesRead = true;
                if (text.IndexOf("m_Fog: 1", System.StringComparison.Ordinal) >= 0) fogUsed = true;
                if (text.IndexOf("m_Lightmaps: []", System.StringComparison.Ordinal) < 0) lightmapsUsed = true;
            }
            Assert.IsTrue(scenesRead, "ни одна сцена сборки не прочитана");

            if (!fogUsed)
                Assert.IsTrue(ContainsKeyword("FOG_LINEAR"),
                    "тумана в сценах нет, а слова тумана не снимаются — это лишние варианты каждого шейдера сцены");

            if (!lightmapsUsed)
                Assert.IsTrue(ContainsKeyword("LIGHTMAP_ON"),
                    "карт освещения в сценах нет, а слово LIGHTMAP_ON не снимается");
        }

        // ==================================================== 3. НЕ ТРОГАТЬ ТО, ЧТО РАБОТАЕТ

        /// <summary>
        /// ТО, ЧТО ПРОЕКТ ИСПОЛЬЗУЕТ, СНИМАТЬ НЕЛЬЗЯ: иначе картинка потеряет тени, куки света
        /// или отражения — и это выяснится уже в шлеме. Проверяются слова, которые обязаны остаться.
        /// </summary>
        [Test]
        public void Policy_KeepsKeywordsTheProjectUses()
        {
            Assert.IsFalse(ContainsKeyword("_MAIN_LIGHT_SHADOWS"),
                "тени главного света включены в ассете URP — их слово снимать нельзя");
            Assert.IsFalse(ContainsKeyword("_LIGHT_COOKIES"),
                "куки света включены в ассете URP (m_SupportsLightCookies) — их слово снимать нельзя");
            Assert.IsFalse(ContainsKeyword("_REFLECTION_PROBE_BLENDING"),
                "смешивание зондов отражения включено в ассете URP — его слово снимать нельзя");
            Assert.IsFalse(ContainsKeyword("_REFLECTION_PROBE_BOX_PROJECTION"),
                "проекция зондов отражения включена в ассете URP — её слово снимать нельзя");

            Assert.IsFalse(KvUrpKeywordPolicy.ShouldStrip(new[] { "_НЕСУЩЕСТВУЮЩЕЕ_СЛОВО" }),
                "политика не должна снимать слова, которых в её списке нет");
            Assert.IsFalse(KvUrpKeywordPolicy.ShouldStrip(null), "политика не должна падать на пустом наборе слов");
        }

        // ==================================================== 4. ПРИЧИНА НАЗВАНА

        /// <summary>
        /// У КАЖДОГО СНЯТОГО СЛОВА ЕСТЬ ПРИЧИНА, и она попадает в журнал сборки. «Снято потому что
        /// снято» — это то, из-за чего такие правки потом боятся трогать.
        /// </summary>
        [Test]
        public void Policy_NamesReasonForEveryStrippedKeyword()
        {
            string report = KvUrpKeywordPolicy.Describe();
            Assert.IsNotNull(report, "отчёт политики пуст");

            foreach (string keyword in KvUrpKeywordPolicy.StrippedKeywords)
                StringAssert.Contains(keyword, report, "у снятого слова нет причины в отчёте: " + keyword);

            Assert.Greater(report.Length, 40, "отчёт политики подозрительно короткий: " + report);
        }

        private static bool ContainsKeyword(string keyword)
        {
            return KvUrpKeywordPolicy.StrippedKeywords.Contains(keyword);
        }
    }
}
