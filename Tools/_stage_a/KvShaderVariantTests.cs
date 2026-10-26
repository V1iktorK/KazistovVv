using System.Collections.Generic;
using System.IO;
using UnityEngine;
using NUnit.Framework;
using KazistovVvXrEditor;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ОТСЕЧЕНИЯ ЛИШНИХ ВАРИАНТОВ ШЕЙДЕРОВ — карточка Б2 (Этап А).
    ///
    /// ЗАЧЕМ ЭТИ ТЕСТЫ. Сборка §30 шла 46 часов и не закончилась: из ~122 000 вариантов шейдеров
    /// ~105 000 приходилось на постобработку (`UberPost` — 92 160 вариантов на каждый из двух
    /// проходов). Причина — в настройках проекта: URP-отсечение собирает набор эффектов
    /// по ВСЕМ профилям объёма в `Assets/`, а в них перечислены Bloom, DepthOfField, MotionBlur,
    /// LensDistortion, ChromaticAberration, FilmGrain, PaniniProjection и Tonemapping.
    /// Пока это не исправлено, КАЖДАЯ сборка APK стоит сутки и больше, а §31–§34 требуют
    /// сборок неоднократно.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ И ПОЧЕМУ ИМЕННО ЭТО:
    ///   1) шейдеры террейна выключены — в проекте НОЛЬ террейнов, а они стоили ~6 часов
    ///      (13 824 + 2 304 + 1 152 + 192 варианта, замер: 18 836 с на один проход);
    ///   2) в профиле объёма по умолчанию нет компонентов с потерянным скриптом — это
    ///      ТЕСТОВЫЙ МУСОР Unity (`CopyPasteTestComponent1/2/3`, `TestVolume`,
    ///      `VolumeComponentSupportedEverywhere` и прочее из `Unity.RenderPipelines.Core.Editor.Tests`),
    ///      который лежит в настройках проекта и ничего не делает;
    ///   3) отсечение разрешено ТОЛЬКО если в сценах сборки ни одна камера не рисует
    ///      постобработку — это проверяется по файлам сцен, а не по памяти;
    ///   4) чистое решение об отсечении: снимаются только варианты с ключевыми словами
    ///      неиспользуемых эффектов, базовые (гамма, преобразование цвета, режим выборки)
    ///      остаются — иначе картинка собралась бы неверного цвета;
    ///   5) если постобработку включат, отсечение обязано САМО перестать работать.
    ///
    /// Тесты 1–3 написаны ДО правки и на текущем состоянии обязаны падать (правило [K5]).
    /// Ни одного GameObject, ни одной записи в настройки, ни зависимости от времени.
    /// </summary>
    public class KvShaderVariantTests
    {
        private const string GlobalSettingsPath = "Assets/Settings/UniversalRenderPipelineGlobalSettings.asset";
        private const string VolumeProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";

        /// <summary>Сцены, которые идут в сборку (§ «сцены сборки» в отчёте KvXrTools).</summary>
        private static readonly string[] BuildScenes =
        {
            "Assets/_Project/00_Scenes/XrMain.unity",
            "Assets/_Project/00_Scenes/MainScene.unity"
        };

        private static string ReadProjectFile(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        // ==================================================== 1. ШЕЙДЕРЫ ТЕРРЕЙНА

        /// <summary>
        /// ШЕЙДЕРЫ ТЕРРЕЙНА ВЫКЛЮЧЕНЫ — потому что террейнов в проекте нет ни одного.
        /// Проверяется ДВА факта: настройка URP выключена И в сценах сборки нет террейна.
        /// Второй факт — причина первого: если террейн появится, тест скажет об этом прямо.
        /// </summary>
        [Test]
        public void UrpGlobalSettings_TerrainShadersAreOff_AndProjectHasNoTerrain()
        {
            string settings = ReadProjectFile(GlobalSettingsPath);
            Assert.IsNotNull(settings, "не прочитан " + GlobalSettingsPath);

            var terrains = new List<string>();
            foreach (string scene in BuildScenes)
            {
                string text = ReadProjectFile(scene);
                if (text == null) { terrains.Add(scene + " (нет файла)"); continue; }
                if (text.IndexOf("Terrain", System.StringComparison.Ordinal) >= 0) terrains.Add(scene);
            }

            Assert.AreEqual(0, terrains.Count,
                "в сценах сборки появился террейн (" + string.Join(", ", terrains.ToArray()) +
                ") — тогда шейдеры террейна НУЖНЫ, и их надо вернуть настройкой " +
                "m_IncludeTerrainShaders: 1 в " + GlobalSettingsPath);

            var values = new List<string>();
            string[] lines = settings.Replace("\r\n", "\n").Split('\n');
            foreach (string line in lines)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("m_IncludeTerrainShaders:", System.StringComparison.Ordinal))
                    values.Add(trimmed.Substring("m_IncludeTerrainShaders:".Length).Trim());
            }

            Assert.Greater(values.Count, 0,
                "в " + GlobalSettingsPath + " нет настройки m_IncludeTerrainShaders — " +
                "проверьте версию пакета URP");

            foreach (string value in values)
                Assert.AreEqual("0", value,
                    "шейдеры террейна включены, а террейнов в проекте нет: каждая сборка платит " +
                    "за них ~6 часов компиляции (замер §30: 13 824 варианта, 18 836 с)");
        }

        // ==================================================== 2. МУСОР В ПРОФИЛЕ ОБЪЁМА

        /// <summary>
        /// В ПРОФИЛЕ ОБЪЁМА ПО УМОЛЧАНИЮ НЕТ КОМПОНЕНТОВ С ПОТЕРЯННЫМ СКРИПТОМ И ТЕСТОВЫХ.
        ///
        /// Найдено в Этапе А: `Assets/Settings/DefaultVolumeProfile.asset` содержит 28 компонентов,
        /// среди них тестовые из сборок Unity (`Unity.RenderPipelines.Core.Editor.Tests`) —
        /// и у шести компонентов `m_Script: {fileID: 0}`, то есть ссылка потеряна.
        /// Это мусор в настройках проекта: он ничего не делает, но путает и мешает читать профиль.
        /// </summary>
        [Test]
        public void DefaultVolumeProfile_HasNoTestOrMissingScriptComponents()
        {
            string profile = ReadProjectFile(VolumeProfilePath);
            Assert.IsNotNull(profile, "не прочитан " + VolumeProfilePath);

            StringAssert.DoesNotContain("Editor.Tests", profile,
                "в профиле объёма по умолчанию лежат компоненты из ТЕСТОВЫХ сборок Unity — это мусор");

            int missing = 0;
            string[] blocks = profile.Replace("\r\n", "\n").Split(new[] { "--- !u!" }, System.StringSplitOptions.None);
            var names = new List<string>();
            foreach (string block in blocks)
            {
                if (block.IndexOf("m_Script: {fileID: 0}", System.StringComparison.Ordinal) < 0) continue;
                missing++;
                int index = block.IndexOf("m_Name:", System.StringComparison.Ordinal);
                names.Add(index >= 0 ? block.Substring(index, System.Math.Min(40, block.Length - index)).Trim() : "?");
            }

            Assert.AreEqual(0, missing,
                "в " + VolumeProfilePath + " компонентов с потерянным скриптом: " + missing +
                " (" + string.Join(" · ", names.ToArray()) + ") — их надо убрать из профиля");
        }

        // ==================================================== 3. РАЗРЕШЕНИЕ НА ОТСЕЧЕНИЕ

        /// <summary>
        /// ПОСТОБРАБОТКУ НИКТО НЕ РИСУЕТ — значит, варианты её эффектов в этой сборке не нужны.
        /// Проверяется по файлам сцен: признак включённой постобработки — `m_RenderPostProcessing: 1`
        /// у камеры. Тест заодно печатает, что именно прочитано, чтобы «разрешение» не было
        /// утверждением из головы.
        /// </summary>
        [Test]
        public void ProjectHasNoPostProcessingCameras()
        {
            var rendered = new List<string>();
            foreach (string scene in BuildScenes)
            {
                string text = ReadProjectFile(scene);
                if (text == null) { rendered.Add(scene + " (нет файла)"); continue; }
                if (text.IndexOf("m_RenderPostProcessing: 1", System.StringComparison.Ordinal) >= 0)
                    rendered.Add(scene);
            }

            Assert.AreEqual(0, rendered.Count,
                "в сценах сборки есть камера с включённой постобработкой (" +
                string.Join(", ", rendered.ToArray()) + ") — отсекать варианты её эффектов НЕЛЬЗЯ");

            Assert.IsTrue(KvPostProcessVariantStripper.ProjectHasNoPostProcessingCameras(),
                "отсечение вариантов не разрешено, хотя в сценах сборки постобработку никто не рисует: " +
                "именно это разрешение снимает ~105 000 вариантов из ~122 000");
        }

        // ==================================================== 4. ЧИСТОЕ РЕШЕНИЕ

        /// <summary>
        /// СНИМАЮТСЯ ТОЛЬКО ВАРИАНТЫ НЕИСПОЛЬЗУЕМЫХ ЭФФЕКТОВ. Базовые ключевые слова
        /// (гамма, преобразование цветового пространства, режим выборки, альфа-вывод)
        /// остаются: они не эффекты, а части правильной картинки.
        /// </summary>
        [Test]
        public void StripVariant_RemovesOnlyUnusedEffectKeywords()
        {
            var bloom = new[] { "_BLOOM_HQ", "_LINEAR_TO_SRGB_CONVERSION" };
            Assert.IsTrue(KvPostProcessVariantStripper.ShouldStripVariant(false, bloom),
                "вариант с эффектом Bloom обязан сниматься, когда постобработку никто не рисует");

            Assert.IsTrue(KvPostProcessVariantStripper.ShouldStripVariant(false, new[] { "_DITHERING" }));
            Assert.IsTrue(KvPostProcessVariantStripper.ShouldStripVariant(false, new[] { "_CHROMATIC_ABERRATION" }));
            Assert.IsTrue(KvPostProcessVariantStripper.ShouldStripVariant(false, new[] { "_TONEMAP_ACES" }));

            var baseline = new[] { "_GAMMA_20", "_LINEAR_TO_SRGB_CONVERSION", "_POINT_SAMPLING", "_ENABLE_ALPHA_OUTPUT" };
            Assert.IsFalse(KvPostProcessVariantStripper.ShouldStripVariant(false, baseline),
                "базовые варианты (цвет и выборка) снимать НЕЛЬЗЯ: картинка соберётся неверного цвета");

            Assert.IsFalse(KvPostProcessVariantStripper.ShouldStripVariant(false, new string[0]));
            Assert.IsFalse(KvPostProcessVariantStripper.ShouldStripVariant(false, null));
        }

        // ==================================================== 5. САМООТКЛЮЧЕНИЕ

        /// <summary>
        /// ЕСЛИ ПОСТОБРАБОТКУ ВКЛЮЧАТ — отсечение обязано перестать работать само.
        /// Иначе эффекты молча исчезли бы из картинки, и разбираться пришлось бы в шлеме.
        /// </summary>
        [Test]
        public void StripVariant_KeepsEverything_WhenPostProcessingIsRendered()
        {
            Assert.IsFalse(KvPostProcessVariantStripper.ShouldStripVariant(true, new[] { "_BLOOM_HQ" }),
                "постобработка рисуется — снимать её варианты нельзя ни при каких условиях");
            Assert.IsFalse(KvPostProcessVariantStripper.ShouldStripVariant(true, new[] { "_DITHERING", "_FXAA" }));

            Assert.IsTrue(KvPostProcessVariantStripper.IsPostProcessingShader(
                KvPostProcessVariantStripper.UberPostShader));
            Assert.IsTrue(KvPostProcessVariantStripper.IsPostProcessingShader(
                KvPostProcessVariantStripper.FinalPostShader));
            Assert.IsFalse(KvPostProcessVariantStripper.IsPostProcessingShader("Universal Render Pipeline/Lit"),
                "материальные шейдеры отсекать нельзя — это уже вопрос вида сцены, а не постобработки");
        }
    }
}
