using NUnit.Framework;
using UnityEditor;
using UnityEngine.Rendering;
using KazistovVvXrEditor;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты УРОВНЕЙ КАЧЕСТВА И ПЛАТФОРМ (Этап А, пункт A4).
    ///
    /// ЗАЧЕМ ЭТИ ТЕСТЫ. Сборка APK намеренно правит уровни качества: HDRP-уровни исключаются
    /// для Android, а уровень по умолчанию для Android переводится на URP-уровень («Mobile»),
    /// иначе пакет HDRP обрывает сборку под шлем. «Проверить, что сборка вернула состояние» —
    /// это в том числе проверить, что умолчания по платформам читаются и значат то, что должны.
    ///
    /// ЧТО НАШЛОСЬ В ЭТАПЕ А. Отчёт пробы (`Logs\stage-a-return-state.log`, прогон stage-a-red)
    /// печатал по умолчанию для всех шестнадцати платформ «? — индекс -1». Причина оказалась
    /// не в отчёте, а в КОДЕ: `m_PerPlatformDefaultQuality` — СЛОВАРЬ («Android: 0»), а прежний
    /// разбор искал пары `key`/`value`, не находил их и молча ничего не делал. То есть часть
    /// «оставить Android без HDRP-уровней» не выполнялась вовсе, и в журнале об этом молчали.
    /// Тесты закрепляют и чтение значений, и то, ЧТО эти значения означают: у Android — URP,
    /// у компьютера — HDRP (решение [D9]).
    ///
    /// Ни одного GameObject, ни одной записи в настройки: тесты только читают файл проекта.
    /// </summary>
    public class KvQualityLevelTests
    {
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";

        private static SerializedObject OpenQualitySettings()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath);
            Assert.IsNotNull(assets, "не прочитан " + QualitySettingsPath);
            Assert.Greater(assets.Length, 0, "в " + QualitySettingsPath + " нет объектов");
            return new SerializedObject(assets[0]);
        }

        private static SerializedProperty LevelAt(SerializedObject settings, int index)
        {
            SerializedProperty levels = settings.FindProperty("m_QualitySettings");
            Assert.IsNotNull(levels, "в " + QualitySettingsPath + " нет списка уровней");
            Assert.Greater(levels.arraySize, index, "в " + QualitySettingsPath + " нет уровня с индексом " + index);
            return levels.GetArrayElementAtIndex(index);
        }

        private static string PipelineOf(SerializedProperty level)
        {
            SerializedProperty pipeline = level.FindPropertyRelative("customRenderPipeline");
            Assert.IsNotNull(pipeline, "у уровня качества нет свойства customRenderPipeline");
            var asset = pipeline.objectReferenceValue as RenderPipelineAsset;
            Assert.IsNotNull(asset, "у уровня качества не назначен конвейер рендера");
            return asset.GetType().Name;
        }

        // ==================================================== 1. ANDROID → URP

        /// <summary>
        /// УРОВЕНЬ ПО УМОЛЧАНИЮ ДЛЯ ANDROID ЧИТАЕТСЯ И ВЕДЁТ НА URP. Иначе сборка под шлем
        /// либо не соберётся (валидатор HDRP), либо соберётся без нужного конвейера.
        /// </summary>
        [Test]
        public void AndroidDefaultQualityLevel_IsReadable_AndIsUrp()
        {
            SerializedObject settings = OpenQualitySettings();
            SerializedProperty perPlatform = settings.FindProperty("m_PerPlatformDefaultQuality");
            Assert.IsNotNull(perPlatform, "в " + QualitySettingsPath + " нет карты умолчаний по платформам");

            int android = KvXrTools.ReadPlatformDefaultQuality(perPlatform, "Android");
            Assert.GreaterOrEqual(android, 0,
                "уровень качества по умолчанию для Android не читается (получено " + android + "): " +
                "это и был молчаливый дефект разбора словаря m_PerPlatformDefaultQuality");

            string pipeline = PipelineOf(LevelAt(settings, android));
            StringAssert.Contains("Universal", pipeline,
                "уровень по умолчанию для Android — " + pipeline + ", а под шлем нужен URP " +
                "(решение [D9]: шлем — URP, компьютер — HDRP)");
        }

        // ==================================================== 2. КОМПЬЮТЕР → HDRP

        /// <summary>
        /// УРОВЕНЬ ПО УМОЛЧАНИЮ ДЛЯ КОМПЬЮТЕРА ВЕДЁТ НА HDRP: рабочий стол обязан остаться
        /// таким, как был (владелец это подтвердил отдельно в §30).
        /// </summary>
        [Test]
        public void StandaloneDefaultQualityLevel_IsReadable_AndIsHdrp()
        {
            SerializedObject settings = OpenQualitySettings();
            SerializedProperty perPlatform = settings.FindProperty("m_PerPlatformDefaultQuality");

            int standalone = KvXrTools.ReadPlatformDefaultQuality(perPlatform, "Standalone");
            Assert.GreaterOrEqual(standalone, 0,
                "уровень качества по умолчанию для Standalone не читается (получено " + standalone + ")");

            string pipeline = PipelineOf(LevelAt(settings, standalone));
            StringAssert.Contains("HD", pipeline,
                "уровень по умолчанию для компьютера — " + pipeline + ", а рабочий стол обязан " +
                "остаться на HDRP (уровень «PC»); смена конвейера на компьютере не разрешалась");
        }

        // ==================================================== 3. ЧЕСТНЫЙ ОТВЕТ НА НЕИЗВЕСТНОЕ

        /// <summary>
        /// НЕИЗВЕСТНАЯ ПЛАТФОРМА ДАЁТ `-1`, А НЕ «НОЛЬ ПО УМОЛЧАНИЮ». Молчаливый ноль опасен:
        /// он выглядит как настоящий ответ и уводит разбор в сторону — именно так дефект
        /// и прожил: отчёт показывал «индекс -1» вместо ошибки, а код считал, что «всё нашёл».
        /// </summary>
        [Test]
        public void UnknownPlatform_ReportsMinusOne_InsteadOfSilence()
        {
            SerializedObject settings = OpenQualitySettings();
            SerializedProperty perPlatform = settings.FindProperty("m_PerPlatformDefaultQuality");

            Assert.AreEqual(-1, KvXrTools.ReadPlatformDefaultQuality(perPlatform, "ТакойПлатформыНет"),
                "незнакомая платформа обязана давать -1, а не молчаливый индекс");
            Assert.AreEqual(-1, KvXrTools.ReadPlatformDefaultQuality(perPlatform, ""),
                "пустое имя платформы обязано давать -1");
            Assert.AreEqual(-1, KvXrTools.ReadPlatformDefaultQuality(null, "Android"),
                "отсутствующая карта умолчаний обязана давать -1, а не падение");
            Assert.IsFalse(KvXrTools.WritePlatformDefaultQuality(perPlatform, "ТакойПлатформыНет", 0),
                "запись в несуществующую платформу обязана ЧЕСТНО вернуть false, а не сделать вид, что записала");
        }
    }
}
