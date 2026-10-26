using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using KazistovVvFeatures;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПАЙПЛАЙНА РЕНДЕРА (сессия §29, карточка Б1 «сборка APK невозможна
    /// при текущей конфигурации рендера»).
    ///
    /// ЗАЧЕМ ЭТИ ТЕСТЫ. Пакет HDRP не поддерживает Android вообще (`HDUtils.IsSupportedBuildTarget` —
    /// жёсткий список платформ), поэтому сборка под шлем обрывается валидатором. Решение [D9]:
    /// глобальное умолчание графики — URP (шлем), уровень качества «PC» — HDRP (компьютер).
    /// Такую настройку нельзя проверять глазами: она в проектном файле, и она молча откатывается
    /// любым прогоном, который её перезапишет. Поэтому она закреплена тестами.
    ///
    /// ВТОРАЯ ЧАСТЬ — «ГОТОВЫЕ ТЕКСТУРЫ БЕЗ ОШИБОК». Материал, сделанный на шейдере чужого
    /// конвейера, рисуется РОЗОВЫМ, а материал с пропавшей ссылкой — розовым под любым
    /// конвейером. Проверяются оба случая: и правила выбора шейдера (чистая логика), и то,
    /// что реально лежит в сценах.
    ///
    /// ПРАВИЛА, СОБЛЮДЁННЫЕ В ТЕСТАХ (как в остальных тестах проекта):
    ///   • ни одного GameObject / MonoBehaviour / Instantiate / корутины;
    ///   • ни одной записи: ни PlayerPrefs, ни файлов проекта, ни сцен
    ///     (сцены открываются ТОЛЬКО на чтение и закрываются в finally);
    ///   • нет зависимости от реального времени и случайности.
    ///
    /// Эти тесты — КОНТРАКТ задачи: они написаны ДО правок и на прежней конфигурации
    /// обязаны падать (это и есть доказательство «до»). Менять их после прогона нельзя
    /// (правило [K5]) — правится код и настройки.
    /// </summary>
    public class KvRenderPipelineTests
    {
        private const string UrpAssetPath = "Assets/Settings/Mobile_RPAsset.asset";
        private const string HdrpAssetPath = "Assets/ROOMS/HQ Hangar Free/Scene/HDRPHighQuality.asset";
        private const string GraphicsSettingsPath = "ProjectSettings/GraphicsSettings.asset";
        private const string QualitySettingsPath = "ProjectSettings/QualitySettings.asset";
        private const string XrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";
        private const string MainScenePath = "Assets/_Project/00_Scenes/MainScene.unity";
        private const string ScriptsRoot = "Assets/_Project/01_Scripts";
        private const string FactoryFileName = "KvRenderPipelineShaders.cs";
        private const string XrContentScriptPath = "Assets/_Project/01_Scripts/Xr/KvXrSceneContent.cs";

        // ============================================================ 1. КОНФИГУРАЦИЯ ПРОЕКТА

        /// <summary>
        /// ГЛАВНОЕ ТРЕБОВАНИЕ Б1: глобальное умолчание графики — URP.
        /// Если оно осталось на HDRP, валидатор HDRP снова оборвёт сборку под Android.
        /// </summary>
        [Test]
        public void GlobalDefault_IsUrp()
        {
            RenderPipelineAsset def = GraphicsSettings.defaultRenderPipeline;
            Assert.IsNotNull(def,
                "глобальное умолчание графики пустое: сборка под шлем осталась бы без пайплайна");
            Assert.AreEqual(KvPipelineKind.Urp, KvRenderPipelineShaders.KindOf(def),
                "глобальное умолчание графики обязано быть URP (решение [D9]) — иначе сборка под " +
                "Android обрывается валидатором HDRP");
        }

        /// <summary>Десктоп не должен потерять HDRP: уровень качества «PC» остаётся на нём.</summary>
        [Test]
        public void PcQualityLevel_StaysHdrp()
        {
            RenderPipelineAsset pc = PipelineOfQualityLevel("PC");
            Assert.IsNotNull(pc, "уровень качества «PC» без пайплайна — компьютер потерял бы HDRP");
            Assert.AreEqual(KvPipelineKind.Hdrp, KvRenderPipelineShaders.KindOf(pc),
                "уровень «PC» обязан остаться на HDRP: иначе десктопная сцена изменится");
        }

        /// <summary>Уровень качества «Mobile» (он же уровень Android) — URP.</summary>
        [Test]
        public void MobileQualityLevel_IsUrp()
        {
            RenderPipelineAsset mobile = PipelineOfQualityLevel("Mobile");
            Assert.IsNotNull(mobile, "уровень качества «Mobile» без пайплайна — шлем остался бы без рендера");
            Assert.AreEqual(KvPipelineKind.Urp, KvRenderPipelineShaders.KindOf(mobile),
                "уровень «Mobile» обязан быть URP: это конвейер сборки под шлем");
        }

        /// <summary>Android по умолчанию берёт уровень «Mobile» (URP), а не «PC» (HDRP).</summary>
        [Test]
        public void AndroidDefaultQuality_IsMobileLevel()
        {
            int androidDefault = PerPlatformDefaultQuality("Android");
            Assert.GreaterOrEqual(androidDefault, 0, "для Android не задан уровень качества по умолчанию");
            Assert.AreEqual("Mobile", QualitySettings.names[androidDefault],
                "Android обязан стартовать на уровне «Mobile» (URP), иначе в шлеме включился бы HDRP");
        }

        /// <summary>Ассеты на месте и того типа, которого от них ждут.</summary>
        [Test]
        public void PipelineAssets_ExistWithExpectedKind()
        {
            var urp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(UrpAssetPath);
            Assert.IsNotNull(urp, "нет URP-ассета " + UrpAssetPath);
            Assert.AreEqual(KvPipelineKind.Urp, KvRenderPipelineShaders.KindOf(urp),
                UrpAssetPath + " — не URP-ассет");

            var hdrp = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(HdrpAssetPath);
            Assert.IsNotNull(hdrp, "нет HDRP-ассета " + HdrpAssetPath);
            Assert.AreEqual(KvPipelineKind.Hdrp, KvRenderPipelineShaders.KindOf(hdrp),
                HdrpAssetPath + " — не HDRP-ассет");
        }

        /// <summary>
        /// ОБА набора глобальных настроек остались привязаны (HDRP — для десктопа, URP — для шлема).
        /// Если запись HDRP пропадёт из карты, HDRP-вид на компьютере сломается молча.
        /// </summary>
        [Test]
        public void GlobalSettingsMap_KeepsBothPipelines()
        {
            // ЧИТАЕТСЯ ИЗ ФАЙЛА ПРОЕКТА, А НЕ ЧЕРЕЗ SerializedObject. Разведка §29 показала:
            // словарь `m_RenderPipelineGlobalSettingsMap` через свойства не читается (значения
            // теряются — отчёт печатал «? → НЕТ» на живой настройке). Файл же плоский, и в нём
            // видно и ключ, и guid. Заодно проверяется ФАКТ сохранения: настройка на диске.
            string text = ReadProjectSettings(GraphicsSettingsPath);
            Assert.IsNotNull(text, "не прочитан " + GraphicsSettingsPath);

            List<string> block = YamlBlock(text, "m_RenderPipelineGlobalSettingsMap");
            Assert.Greater(block.Count, 0,
                "в " + GraphicsSettingsPath + " нет карты глобальных настроек конвейеров");

            string hdrpGuid = null;
            string urpGuid = null;
            foreach (string line in block)
            {
                string guid = ExtractGuid(line);
                if (string.IsNullOrEmpty(guid)) continue;
                if (line.IndexOf("HDRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0) hdrpGuid = guid;
                if (line.IndexOf("UniversalRenderPipeline", StringComparison.OrdinalIgnoreCase) >= 0) urpGuid = guid;
            }

            Assert.IsNotNull(hdrpGuid,
                "в карте глобальных настроек нет записи HDRP — компьютер остался бы без настроек HDRP");
            Assert.IsNotNull(urpGuid,
                "в карте глобальных настроек нет записи URP — шлем остался бы без настроек URP");
            Assert.IsNotEmpty(AssetDatabase.GUIDToAssetPath(hdrpGuid),
                "guid HDRP из карты глобальных настроек ни на что не указывает");
            Assert.IsNotEmpty(AssetDatabase.GUIDToAssetPath(urpGuid),
                "guid URP из карты глобальных настроек ни на что не указывает");
        }

        // ================================================ чтение файлов проектных настроек

        /// <summary>Текст файла проекта (путь относительно корня проекта).</summary>
        private static string ReadProjectSettings(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>
        /// Строки YAML-блока по имени ключа: от строки «имя:» до первой строки с меньшим или
        /// равным отступом. Разбор намеренно простой — файлы проектных настроек плоские.
        /// </summary>
        private static List<string> YamlBlock(string text, string key)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) return result;

            string[] all = text.Replace("\r\n", "\n").Split('\n');
            int start = -1;
            int indent = 0;
            for (int i = 0; i < all.Length; i++)
            {
                string trimmed = all[i].TrimStart();
                if (!trimmed.StartsWith(key + ":", StringComparison.Ordinal)) continue;
                start = i;
                indent = all[i].Length - trimmed.Length;
                break;
            }
            if (start < 0) return result;

            for (int i = start + 1; i < all.Length; i++)
            {
                string line = all[i];
                if (line.Trim().Length == 0) continue;
                int lineIndent = line.Length - line.TrimStart().Length;
                if (lineIndent <= indent) break;
                result.Add(line.Trim());
            }
            return result;
        }

        /// <summary>Первый guid из строки вида `{fileID: …, guid: 32 hex, type: 2}`.</summary>
        private static string ExtractGuid(string line)
        {
            int at = line.IndexOf("guid: ", StringComparison.Ordinal);
            if (at < 0) return null;
            string tail = line.Substring(at + 6).TrimStart();
            int end = 0;
            while (end < tail.Length && IsHex(tail[end])) end++;
            return end == 32 ? tail.Substring(0, 32) : null;
        }

        private static bool IsHex(char c)
        {
            return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
        }

        // ============================================================ 2. ФАБРИКА ШЕЙДЕРОВ

        /// <summary>
        /// ГЛАВНОЕ ПРАВИЛО, РАДИ КОТОРОГО СДЕЛАНА ФАБРИКА: под URP ни один запасной вариант
        /// не должен привести к шейдеру HDRP — это и есть «розовая» поверхность.
        /// </summary>
        [Test]
        public void UrpChains_NeverFallBackToHdrp()
        {
            AssertNoForeignPipeline(KvRenderPipelineShaders.LitChain(KvPipelineKind.Urp), "HDRP/",
                "цепочка Lit для URP");
            AssertNoForeignPipeline(KvRenderPipelineShaders.UnlitChain(KvPipelineKind.Urp), "HDRP/",
                "цепочка Unlit для URP");
            AssertNoForeignPipeline(KvRenderPipelineShaders.LitChain(KvPipelineKind.Urp),
                "Universal", "цепочка Lit для URP не должна содержать чужих конвейеров", true);
        }

        /// <summary>Симметрично: под HDRP ни один запасной вариант не должен быть шейдером URP.</summary>
        [Test]
        public void HdrpChains_NeverFallBackToUrp()
        {
            AssertNoForeignPipeline(KvRenderPipelineShaders.LitChain(KvPipelineKind.Hdrp), "Universal",
                "цепочка Lit для HDRP");
            AssertNoForeignPipeline(KvRenderPipelineShaders.UnlitChain(KvPipelineKind.Hdrp), "Universal",
                "цепочка Unlit для HDRP");
        }

        /// <summary>Основной шейдер каждого конвейера — тот, который от него ждут.</summary>
        [Test]
        public void MainShaderName_MatchesPipeline()
        {
            Assert.AreEqual("Universal Render Pipeline/Lit",
                KvRenderPipelineShaders.LitShaderName(KvPipelineKind.Urp), "Lit для URP");
            Assert.AreEqual("HDRP/Lit",
                KvRenderPipelineShaders.LitShaderName(KvPipelineKind.Hdrp), "Lit для HDRP");
            Assert.AreEqual("Standard",
                KvRenderPipelineShaders.LitShaderName(KvPipelineKind.BuiltIn), "Lit для встроенного конвейера");

            Assert.AreEqual("Universal Render Pipeline/Unlit",
                KvRenderPipelineShaders.UnlitShaderName(KvPipelineKind.Urp), "Unlit для URP");
            Assert.AreEqual("HDRP/Unlit",
                KvRenderPipelineShaders.UnlitShaderName(KvPipelineKind.Hdrp), "Unlit для HDRP");
        }

        /// <summary>
        /// Имя свойства цвета зависит от шейдера, и его нельзя угадать: запись в несуществующее
        /// свойство молча ничего не делает, а материал остаётся серым.
        /// </summary>
        [Test]
        public void ColorProperty_MatchesShader()
        {
            Assert.AreEqual("_BaseColor",
                KvRenderPipelineShaders.ColorPropertyName("Universal Render Pipeline/Lit", false), "URP/Lit");
            Assert.AreEqual("_BaseColor",
                KvRenderPipelineShaders.ColorPropertyName("HDRP/Lit", false), "HDRP/Lit");
            Assert.AreEqual("_Color",
                KvRenderPipelineShaders.ColorPropertyName("Standard", false), "встроенный Standard");

            Assert.AreEqual("_BaseColor",
                KvRenderPipelineShaders.ColorPropertyName("Universal Render Pipeline/Unlit", true), "URP/Unlit");
            Assert.AreEqual("_UnlitColor",
                KvRenderPipelineShaders.ColorPropertyName("HDRP/Unlit", true), "HDRP/Unlit");
            Assert.AreEqual("_Color",
                KvRenderPipelineShaders.ColorPropertyName("Unlit/Color", true), "встроенный Unlit/Color");
        }

        /// <summary>Суждение аудита: что розовое, а что нет.</summary>
        [Test]
        public void Compatibility_VerdictsAreCorrect()
        {
            Assert.IsFalse(KvRenderPipelineShaders.IsCompatibleName("HDRP/Lit", KvPipelineKind.Urp),
                "шейдер HDRP под URP — это и есть розовая поверхность");
            Assert.IsFalse(KvRenderPipelineShaders.IsCompatibleName("Standard", KvPipelineKind.Urp),
                "встроенный Standard под URP не рисуется");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatibleName("Universal Render Pipeline/Lit", KvPipelineKind.Urp),
                "URP/Lit под URP — норма");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatibleName("Unlit/Color", KvPipelineKind.Urp),
                "встроенные Unlit-шейдеры рисуются в любом конвейере");

            Assert.IsFalse(KvRenderPipelineShaders.IsCompatibleName("Universal Render Pipeline/Lit", KvPipelineKind.Hdrp),
                "шейдер URP под HDRP — тоже розовое");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatibleName("HDRP/Lit", KvPipelineKind.Hdrp),
                "HDRP/Lit под HDRP — норма");
            Assert.IsFalse(KvRenderPipelineShaders.IsCompatibleName("Standard", KvPipelineKind.Hdrp),
                "встроенный Standard под HDRP не рисуется");

            Assert.IsFalse(KvRenderPipelineShaders.IsCompatible(null, KvPipelineKind.Urp),
                "пропавший шейдер обязан считаться несовместимым: Unity рисует его magenta-ошибкой");
        }

        /// <summary>
        /// Под ТЕКУЩИМ конвейером фабрика обязана вернуть рабочий шейдер — и он обязан
        /// принадлежать этому конвейеру. Проверка идёт на живой конфигурации проекта.
        /// </summary>
        [Test]
        public void ActivePipeline_HasUsableShaders()
        {
            KvPipelineKind kind = KvRenderPipelineShaders.ActiveKind();

            Shader lit = KvRenderPipelineShaders.FindLit();
            Assert.IsNotNull(lit, "фабрика не нашла Lit-шейдер для конвейера " + kind +
                " — материалы, создаваемые кодом, станут розовыми");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatible(lit, kind),
                "Lit-шейдер " + lit.name + " несовместим с активным конвейером " + kind);

            Shader unlit = KvRenderPipelineShaders.FindUnlit();
            Assert.IsNotNull(unlit, "фабрика не нашла Unlit-шейдер для конвейера " + kind);
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatible(unlit, kind),
                "Unlit-шейдер " + unlit.name + " несовместим с активным конвейером " + kind);

            // Шейдеры URP обязаны быть в проекте: это конвейер сборки под шлем.
            Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Lit"),
                "в проекте нет шейдера Universal Render Pipeline/Lit — пакет URP вырезан?");
            Assert.IsNotNull(Shader.Find("Universal Render Pipeline/Unlit"),
                "в проекте нет шейдера Universal Render Pipeline/Unlit — пакет URP вырезан?");
        }

        /// <summary>Материал, созданный фабрикой, совместим с конвейером и получает заданный цвет.</summary>
        [Test]
        public void CreatedMaterial_MatchesActivePipelineAndColor()
        {
            KvPipelineKind kind = KvRenderPipelineShaders.ActiveKind();
            Color wanted = new Color(0.25f, 0.5f, 0.75f, 1f);

            Material lit = KvRenderPipelineShaders.CreateLit("тест-лит", wanted);
            Assert.IsNotNull(lit, "фабрика не создала Lit-материал");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatible(lit.shader, kind),
                "созданный материал использует шейдер " + lit.shader.name +
                ", несовместимый с конвейером " + kind);

            string property = KvRenderPipelineShaders.ColorPropertyName(lit.shader.name, false);
            Assert.IsTrue(lit.HasProperty(property),
                "у шейдера " + lit.shader.name + " нет свойства цвета " + property +
                " — цвет молча не применился бы");
            Color got = lit.GetColor(property);
            Assert.AreEqual(wanted.r, got.r, 0.001f, "красный канал цвета");
            Assert.AreEqual(wanted.g, got.g, 0.001f, "зелёный канал цвета");
            Assert.AreEqual(wanted.b, got.b, 0.001f, "синий канал цвета");

            Material unlit = KvRenderPipelineShaders.CreateUnlit("тест-анлит", wanted);
            Assert.IsNotNull(unlit, "фабрика не создала Unlit-материал");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatible(unlit.shader, kind),
                "созданный Unlit-материал использует шейдер " + unlit.shader.name +
                ", несовместимый с конвейером " + kind);
        }

        // ============================================================ 3. ЗАЩИТА ОТ РЕГРЕССИЙ

        /// <summary>
        /// НИ ОДНОГО ПРЯМОГО ОБРАЩЕНИЯ К ШЕЙДЕРАМ HDRP В КОДЕ, кроме самой фабрики.
        /// До сессии §29 такие обращения стояли в 12 файлах, часть — без запасного варианта:
        /// при сборке под шлем это гарантированно розовые линии, метки и индикаторы.
        /// </summary>
        [Test]
        public void Sources_HaveNoDirectHdrpShaderLookups()
        {
            string root = Path.Combine(Application.dataPath, "_Project", "01_Scripts");
            Assert.IsTrue(Directory.Exists(root), "не найдена папка исходников " + root);

            var offenders = new List<string>();
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(files.Length, 0, "в " + root + " нет исходников — тест ничего не проверил");

            foreach (string file in files)
            {
                if (Path.GetFileName(file) == FactoryFileName) continue;   // здесь обращения и живут
                if (LooksUpHdrpShader(File.ReadAllText(file))) offenders.Add(Path.GetFileName(file));
            }

            Assert.AreEqual(0, offenders.Count,
                "прямые обращения к шейдерам HDRP остались в: " + string.Join(", ", offenders) +
                ". Материалы обязан создавать KvRenderPipelineShaders — иначе под URP они розовые.");
        }

        /// <summary>
        /// Есть ли в тексте файла прямой поиск шейдера HDRP. Ищется ИМЕННО вызов `Shader.Find`
        /// с именем на «HDRP/»: упоминание HDRP в комментарии или строке лога — не дефект,
        /// а вот `Shader.Find("HDRP/Lit")` — дефект под URP.
        /// </summary>
        private static bool LooksUpHdrpShader(string text)
        {
            const string call = "Shader.Find(";
            int from = 0;
            while (true)
            {
                int at = text.IndexOf(call, from, StringComparison.Ordinal);
                if (at < 0) return false;

                // Комментарии не считаются: в них имя шейдера упоминается по делу
                // (объяснение, почему так больше не делают), и это не дефект кода.
                if (!IsInsideCommentLine(text, at))
                {
                    int i = at + call.Length;
                    while (i < text.Length && (text[i] == ' ' || text[i] == '\t')) i++;

                    if (i < text.Length && (text[i] == '"' || text[i] == '\''))
                    {
                        char quote = text[i];
                        int end = text.IndexOf(quote, i + 1);
                        if (end > i)
                        {
                            string name = text.Substring(i + 1, end - i - 1);
                            if (name.StartsWith("HDRP/", StringComparison.OrdinalIgnoreCase)) return true;
                        }
                    }
                }
                from = at + call.Length;
            }
        }

        /// <summary>Позиция внутри строки-комментария: `//`, `///` или строка блочного `*`.</summary>
        private static bool IsInsideCommentLine(string text, int position)
        {
            int lineStart = text.LastIndexOf('\n', Math.Max(0, position - 1)) + 1;
            string line = text.Substring(lineStart, position - lineStart).TrimStart();
            return line.StartsWith("//", StringComparison.Ordinal) ||
                   line.StartsWith("*", StringComparison.Ordinal) ||
                   line.StartsWith("/*", StringComparison.Ordinal);
        }

        // ============================================================ 4. ЛИНИИ, ЛУЧИ, СБОРКА (§32/Э2)

        private const string UiRoot = "Assets/_Project/06_KazistovVv_UI";

        /// <summary>
        /// У ЛИНИЙ И ЛУЧЕЙ ЕСТЬ РАБОЧИЙ ШЕЙДЕР. Это материал лазера оператора, меток, зон,
        /// трубок траектории и подсветки. Пока он берётся прямым `Shader.Find("Sprites/Default")`,
        /// в сборке под шлем он может оказаться пустым (шейдер вырезан стриппингом) — и лучи
        /// просто исчезнут, хотя на компьютере видны.
        /// </summary>
        [Test]
        public void LineShader_IsAvailableForActivePipeline()
        {
            KvPipelineKind kind = KvRenderPipelineShaders.ActiveKind();

            Shader line = KvRenderPipelineShaders.FindLine();
            Assert.IsNotNull(line, "фабрика не нашла шейдер для линий и лучей — лазер, метки и зоны " +
                "останутся без материала");
            Assert.IsTrue(KvRenderPipelineShaders.IsCompatible(line, kind),
                "шейдер линий " + line.name + " несовместим с активным конвейером " + kind);
        }

        /// <summary>
        /// ЦЕПОЧКА ЛИНИЙ: первым — встроенный `Sprites/Default` (он рисуется в любом конвейере,
        /// поэтому вид на компьютере не меняется), и НИ ОДНОГО шейдера чужого конвейера.
        /// </summary>
        [Test]
        public void LineChain_PrefersBuiltInSprite_AndNeverForeignPipeline()
        {
            var kinds = new[] { KvPipelineKind.Urp, KvPipelineKind.Hdrp, KvPipelineKind.BuiltIn };
            foreach (KvPipelineKind kind in kinds)
            {
                string[] chain = KvRenderPipelineShaders.LineChain(kind);
                Assert.IsNotNull(chain, "нет цепочки линий для конвейера " + kind);
                Assert.Greater(chain.Length, 0, "пустая цепочка линий для конвейера " + kind);
                Assert.AreEqual(KvRenderPipelineShaders.SpriteDefaultName, chain[0],
                    "первым в цепочке линий для " + kind + " обязан идти встроенный Sprites/Default");

                foreach (string name in chain)
                {
                    if (kind == KvPipelineKind.Urp)
                        Assert.IsFalse(name.StartsWith("HDRP/", StringComparison.Ordinal),
                            "цепочка линий URP не имеет права брать HDRP-шейдер " + name);
                    if (kind == KvPipelineKind.Hdrp)
                        Assert.IsFalse(name.StartsWith(KvRenderPipelineShaders.UrpPrefix, StringComparison.Ordinal),
                            "цепочка линий HDRP не имеет права брать URP-шейдер " + name);
                }
            }
        }

        /// <summary>Список шейдеров для сборки обязан покрывать ОБА конвейера проекта (решение [D9]).</summary>
        [Test]
        public void RuntimeShaderNames_CoverBothPipelines()
        {
            var names = new List<string>(KvRenderPipelineShaders.RuntimeShaderNamesForBuild());
            var needed = new[]
            {
                KvRenderPipelineShaders.SpriteDefaultName,
                KvRenderPipelineShaders.UrpLitName,
                KvRenderPipelineShaders.UrpSimpleLitName,
                KvRenderPipelineShaders.UrpUnlitName
            };
            foreach (string name in needed)
                Assert.IsTrue(names.Contains(name), "в списке шейдеров для сборки нет " + name);
        }

        /// <summary>
        /// ШЕЙДЕРЫ ДЛЯ РАНТАЙМ-МАТЕРИАЛОВ ЛЕЖАТ В СПИСКЕ «ВСЕГДА ВКЛЮЧЁННЫХ».
        /// Без этого материалы, созданные кодом (лучи, метки, зоны, подсветка), в сборке
        /// получаются пустыми: `Shader.Find` их не находит, а `new Material(null)` падает.
        /// На устройстве 08.10.2026 это уже случилось (`KvSelectionHighlight.Build`).
        /// Лечение — меню `Tools/KazistovVv Рендер/8. Шейдеры для материалов из кода — в сборку`.
        /// </summary>
        [Test]
        public void Graphics_AlwaysIncludesRuntimeShaders()
        {
            UnityEngine.Object graphics = GraphicsSettings.GetGraphicsSettings();
            Assert.IsNotNull(graphics, "не найден объект GraphicsSettings");

            var so = new SerializedObject(graphics);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            Assert.IsNotNull(list, "в GraphicsSettings нет свойства m_AlwaysIncludedShaders");

            var present = new List<string>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var shader = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (shader != null) present.Add(shader.name);
            }

            var absent = new List<string>();
            foreach (string name in KvRenderPipelineShaders.RuntimeShaderNamesForBuild())
            {
                if (Shader.Find(name) == null) continue;      // такого шейдера в проекте нет вовсе
                if (!present.Contains(name)) absent.Add(name);
            }

            Assert.AreEqual(0, absent.Count,
                "эти шейдеры создаются кодом в рантайме, но их нет в списке «всегда включённых»: " +
                string.Join(", ", absent.ToArray()) +
                ". В сборке под шлем материалы станут пустыми (лучи и метки исчезнут). " +
                "Лечение: Tools/KazistovVv Рендер/8.");
        }

        /// <summary>
        /// В РАНТАЙМ-КОДЕ НЕТ ПРЯМЫХ ОБРАЩЕНИЙ К ШЕЙДЕРАМ. До §32 их было 10 (лазер, метки,
        /// зоны, призраки, теплокарты, дерево RRT, капсулы, превью окна, подсветка, маркер цели):
        /// каждый такой вызов — либо розовая поверхность под чужим конвейером, либо пропавший
        /// материал в сборке. Материалы обязан создавать `KvRenderPipelineShaders`.
        /// </summary>
        [Test]
        public void Sources_HaveNoDirectShaderLookupsInRuntimeCode()
        {
            var roots = new[] { ScriptsRoot, UiRoot };
            var offenders = new List<string>();
            int scanned = 0;

            foreach (string rootAssetPath in roots)
            {
                string root = Path.Combine(Application.dataPath, "..", rootAssetPath);
                root = Path.GetFullPath(root);
                if (!Directory.Exists(root)) continue;

                foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories))
                {
                    string relative = file.Substring(root.Length).Replace('\\', '/');
                    if (relative.StartsWith("/Editor/", StringComparison.Ordinal)) continue;      // инструменты редактора — можно
                    if (relative.Contains("/Editor/")) continue;                                  // вложенные Editor-папки — можно
                    if (Path.GetFileName(file) == FactoryFileName) continue;                      // здесь поиск и живёт
                    scanned++;

                    string text = File.ReadAllText(file);
                    if (HasDirectShaderLookup(text)) offenders.Add(rootAssetPath + relative);
                }
            }

            Assert.Greater(scanned, 100, "проверено подозрительно мало файлов (" + scanned + ")");
            Assert.AreEqual(0, offenders.Count,
                "прямые обращения к шейдерам в рантайм-коде: " + string.Join(", ", offenders.ToArray()) +
                ". Материал обязан создавать KvRenderPipelineShaders (иначе в шлеме розовое или пусто).");
        }

        /// <summary>Есть ли в тексте прямой поиск шейдера или материал «из ничего» (кроме комментариев).</summary>
        private static bool HasDirectShaderLookup(string text)
        {
            var calls = new[] { "Shader.Find(", "new Material(Shader" };
            foreach (string call in calls)
            {
                int from = 0;
                while (true)
                {
                    int at = text.IndexOf(call, from, StringComparison.Ordinal);
                    if (at < 0) break;
                    if (!IsInsideCommentLine(text, at)) return true;
                    from = at + call.Length;
                }
            }
            return false;
        }

        // ============================================================ 4. СЦЕНЫ

        /// <summary>
        /// В XR-СЦЕНЕ ДОЛЖНА БЫТЬ НАЧИНКА. Отчёт §28 утверждал, что компонент
        /// `KvXrSceneContent` добавлен в сцену; проверка по факту показала, что его там нет,
        /// а роботов и столов в сцене не было вовсе — то есть в шлеме показывать было нечего.
        /// </summary>
        [Test]
        public void XrScene_HasContentComponentAndRobotTemplates()
        {
            Assert.IsTrue(File.Exists(XrScenePath), "нет XR-сцены " + XrScenePath);
            string scene = File.ReadAllText(XrScenePath);

            string contentGuid = AssetDatabase.AssetPathToGUID(XrContentScriptPath);
            Assert.IsFalse(string.IsNullOrEmpty(contentGuid), "не найден guid " + XrContentScriptPath);
            Assert.IsTrue(scene.IndexOf("guid: " + contentGuid, StringComparison.OrdinalIgnoreCase) >= 0,
                "в XR-сцене нет компонента KvXrSceneContent — начинка (столы и роботы) не построится");

            string robotGuid = AssetDatabase.AssetPathToGUID("Assets/Robots/Robot.fbx");
            string scaraGuid = AssetDatabase.AssetPathToGUID("Assets/Robots/ScaraRobot.fbx");
            Assert.IsFalse(string.IsNullOrEmpty(robotGuid), "не найден guid модели Robot.fbx");
            Assert.IsFalse(string.IsNullOrEmpty(scaraGuid), "не найден guid модели ScaraRobot.fbx");

            bool hasRobot = scene.IndexOf("guid: " + robotGuid, StringComparison.OrdinalIgnoreCase) >= 0;
            bool hasScara = scene.IndexOf("guid: " + scaraGuid, StringComparison.OrdinalIgnoreCase) >= 0;
            Assert.IsTrue(hasRobot || hasScara,
                "в XR-сцене нет ни одной модели робота: StandBuilder не найдёт шаблон и стенды " +
                "останутся пустыми (он копирует робота с шаблона в той же сцене)");
        }

        /// <summary>
        /// МАТЕРИАЛЫ XR-СЦЕНЫ ОБЯЗАНЫ БЫТЬ URP-СОВМЕСТИМЫМИ: сборка под шлем идёт на URP,
        /// и любой материал чужого конвейера (или пропавшая ссылка) станет розовым в шлеме.
        /// </summary>
        [Test]
        public void XrScene_MaterialsAreUrpCompatibleWithoutMissingLinks()
        {
            CheckSceneMaterials(XrScenePath, KvPipelineKind.Urp);
        }

        /// <summary>
        /// ОБРАТНАЯ ПРОВЕРКА ДЛЯ КОМПЬЮТЕРА: в десктопной сцене материалы обязаны быть
        /// совместимы с HDRP (уровень «PC» остаётся на нём) и не иметь пропавших ссылок.
        /// </summary>
        [Test]
        public void MainScene_MaterialsAreHdrpCompatibleWithoutMissingLinks()
        {
            CheckSceneMaterials(MainScenePath, KvPipelineKind.Hdrp);
        }

        // ============================================================ вспомогательное

        private static void CheckSceneMaterials(string scenePath, KvPipelineKind kind)
        {
            Assert.IsTrue(File.Exists(scenePath), "нет сцены " + scenePath);

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Additive);
            var problems = new List<string>();
            var names = new List<string>();
            int renderers = 0;
            try
            {
                Renderer[] all = UnityEngine.Object.FindObjectsByType<Renderer>(
                    FindObjectsInactive.Include);
                foreach (Renderer r in all)
                {
                    if (r == null || r.gameObject.scene != scene) continue;
                    renderers++;

                    Material[] mats = r.sharedMaterials;
                    if (mats == null) continue;
                    foreach (Material m in mats)
                    {
                        if (m == null)
                        {
                            problems.Add("пропавшая ссылка на материал у " + r.name);
                            continue;
                        }
                        if (names.Count < 40 && !names.Contains(m.name)) names.Add(m.name);
                        if (!KvRenderPipelineShaders.IsCompatible(m.shader, kind))
                        {
                            problems.Add(m.name + " (шейдер " +
                                         (m.shader != null ? m.shader.name : "ПРОПАЛ") + ") у " + r.name);
                        }
                    }
                }
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
            }

            Assert.AreEqual(0, problems.Count,
                "в сцене " + scenePath + " материалы не годятся для конвейера " + kind + ": " +
                string.Join(" · ", problems.ToArray()));

            if (kind == KvPipelineKind.Urp)
            {
                Assert.Greater(renderers, 0,
                    "в XR-сцене нет ни одного рендерера — показывать в шлеме нечего, " +
                    "и проверка материалов ничего не значит");
            }
        }

        private static void AssertNoForeignPipeline(string[] chain, string forbiddenPrefix,
            string what, bool prefixIsAllowedInstead = false)
        {
            Assert.IsNotNull(chain, what + ": цепочка пустая");
            Assert.Greater(chain.Length, 0, what + ": цепочка пустая");

            for (int i = 0; i < chain.Length; i++)
            {
                Assert.IsNotNull(chain[i], what + ": пустое имя в позиции " + i);
                if (prefixIsAllowedInstead)
                {
                    // режим «в цепочке допустим ТОЛЬКО этот префикс» — используется для URP,
                    // где запасом служат SimpleLit/Unlit ТОГО ЖЕ конвейера и встроенный Unlit.
                    bool own = chain[i].StartsWith(forbiddenPrefix) ||
                               chain[i] == KvRenderPipelineShaders.BuiltInUnlitName ||
                               chain[i] == KvRenderPipelineShaders.SpriteDefaultName;
                    Assert.IsTrue(own, what + ": «" + chain[i] + "» — не шейдер конвейера и не встроенный Unlit");
                    continue;
                }

                Assert.IsFalse(chain[i].StartsWith(forbiddenPrefix, StringComparison.Ordinal),
                    what + ": запасной вариант «" + chain[i] + "» принадлежит чужому конвейеру — " +
                    "под ним материал станет розовым");
            }
        }

        private static RenderPipelineAsset PipelineOfQualityLevel(string levelName)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(QualitySettingsPath);
            Assert.IsNotNull(assets, "не прочитан " + QualitySettingsPath);
            Assert.Greater(assets.Length, 0, "не прочитан " + QualitySettingsPath);

            var so = new SerializedObject(assets[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            Assert.IsNotNull(levels, "в " + QualitySettingsPath + " нет списка уровней качества");

            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty name = level.FindPropertyRelative("name");
                if (name == null || name.stringValue != levelName) continue;

                SerializedProperty pipe = level.FindPropertyRelative("customRenderPipeline");
                return pipe != null ? pipe.objectReferenceValue as RenderPipelineAsset : null;
            }

            Assert.Fail("не найден уровень качества «" + levelName + "»");
            return null;
        }

        private static int PerPlatformDefaultQuality(string platform)
        {
            // Тоже из файла проекта: через SerializedObject словарь `m_PerPlatformDefaultQuality`
            // в Unity 6 читается пустым (проверено §29), и проверка молча возвращала «не найдено».
            string text = ReadProjectSettings(QualitySettingsPath);
            Assert.IsNotNull(text, "не прочитан " + QualitySettingsPath);

            foreach (string line in YamlBlock(text, "m_PerPlatformDefaultQuality"))
            {
                int colon = line.IndexOf(':');
                if (colon <= 0) continue;
                if (line.Substring(0, colon).Trim() != platform) continue;
                int index;
                if (int.TryParse(line.Substring(colon + 1).Trim(), out index)) return index;
            }

            return -1;
        }
        // ==================================================== §30: материал робота (карточка Б11)

        /// <summary>
        /// РОБОТ РИСУЕТСЯ ПРОЕКТНЫМ МАТЕРИАЛОМ, А НЕ МАТЕРИАЛОМ КОНВЕЙЕРА «ПО УМОЛЧАНИЮ» (§30, карточка Б11).
        ///
        /// ЧТО БЫЛО. `Robot.fbx` переназначала свой материал на guid `b90d9d26…`, которого в проекте
        /// нет (ассет утрачен). Поэтому 7 рендереров робота рисовались `DefaultHDMaterial` —
        /// материалом конвейера «по умолчанию»: его нельзя ни настроить, ни перевести на URP
        /// (в шлеме он был бы розовым), и он не принадлежит проекту.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ: (1) оба проектных материала существуют — `Robot_HDRP` (компьютер)
        /// и `Robot_URP` (шлем); (2) в каждой сцене не меньше семи рендереров робота ссылаются
        /// на «свой» материал конвейера; (3) битой ссылки в модели больше нет, а переназначение
        /// в модели указывает на проектный материал. Числа — из §30.
        /// </summary>
        [Test]
        public void RobotMaterials_AreProjectMaterialsNotPipelineDefaults()
        {
            const string hdrpMaterial = "Assets/_Project/04_Materials/Robot/Robot_HDRP.mat";
            const string urpMaterial = "Assets/_Project/04_Materials/Robot/Robot_URP.mat";
            const string modelMeta = "Assets/Robots/Robot.fbx.meta";
            const string lostGuid = "b90d9d26a7b21ee4e93ad5a44c9333be";   // утраченный ассет, найденный в §29

            string hdrpGuid = AssetDatabase.AssetPathToGUID(hdrpMaterial);
            string urpGuid = AssetDatabase.AssetPathToGUID(urpMaterial);
            Assert.IsFalse(string.IsNullOrEmpty(hdrpGuid), "нет проектного материала для компьютера: " + hdrpMaterial);
            Assert.IsFalse(string.IsNullOrEmpty(urpGuid), "нет проектного материала для шлема: " + urpMaterial);

            string mainScene = File.ReadAllText("Assets/_Project/00_Scenes/MainScene.unity");
            string xrScene = File.ReadAllText("Assets/_Project/00_Scenes/XrMain.unity");

            int inMain = CountOccurrences(mainScene, hdrpGuid);
            int inXr = CountOccurrences(xrScene, urpGuid);

            Assert.GreaterOrEqual(inMain, 7,
                "в MainScene ссылок на проектный материал компьютера " + inMain + ", а в §30 их было 7 — " +
                "значит, часть рендереров робота снова рисуется материалом конвейера «по умолчанию»");
            Assert.GreaterOrEqual(inXr, 7,
                "в XrMain ссылок на проектный материал шлема " + inXr + ", а в §30 их было 7 — " +
                "в шлеме часть робота будет розовой");

            string meta = File.ReadAllText(modelMeta);
            StringAssert.DoesNotContain(lostGuid, meta,
                "в " + modelMeta + " осталась ссылка на утраченный материал — любой новый экземпляр модели " +
                "снова получит материал конвейера по умолчанию");
            StringAssert.Contains(hdrpGuid, meta,
                "переназначение материала в модели (" + modelMeta + ") не указывает на проектный " + hdrpMaterial);
        }

        /// <summary>Сколько раз строка встречается в тексте (для ссылок на guid в сценах).</summary>
        private static int CountOccurrences(string text, string needle)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(needle, index, System.StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += needle.Length;
            }
            return count;
        }
    }
}
