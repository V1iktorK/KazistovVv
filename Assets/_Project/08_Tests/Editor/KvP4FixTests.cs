// ================================================================================================
//  KvP4FixTests.cs — ТЕСТЫ ЗАДАЧ P4 (тесты, CI, сборка), 12.11.2026.
//
//  ОТКУДА ЗАДАЧА. Раздел P4 распоряжения — про то, ЧЕМ проект себя проверяет: сборки кода, фильтры
//  тестов, покрытие, конфигурация сборки, CI, гигиена рабочего дерева. Здесь проверяется не
//  «написан ли код», а «нельзя ли снова сделать вид, что проверка есть».
//
//  ПРАВИЛО ТЕСТОВ ТО ЖЕ: каждый обязан ПАДАТЬ на старом коде. Там, где поведение нельзя оторвать
//  от файла настроек или от YAML конвейера, проверяется ФАКТ в файле — и всегда с НЕВАКУУМНОЙ
//  страховкой (пустой или переименованный файл даёт провал, а не зелёный; урок P4-2).
// ================================================================================================

using System.IO;
using System.Text.RegularExpressions;
using KazistovVvFeatures;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;

namespace KazistovVvTests
{
    /// <summary>Задачи P4: сборки кода, категории, покрытие, конфигурация сборки, CI, гигиена.</summary>
        [Category(KvTestCategories.Fast)]
    public class KvP4FixTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadFile(string relativePath)
        {
            string path = Path.Combine(ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Assert.IsTrue(File.Exists(path), "нет файла " + path);
            return File.ReadAllText(path);
        }

        private static string[] FindFiles(string relativeFolder, string pattern, SearchOption option)
        {
            string path = Path.Combine(ProjectRoot, relativeFolder.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(path)) return new string[0];
            return Directory.GetFiles(path, pattern, option);
        }

        // ================================================================ P4-4: сборки кода

        /// <summary>
        /// P4-4: В ПРОЕКТЕ ЕСТЬ СБОРКИ, И ТЕСТЫ ЖИВУТ В СВОЕЙ.
        ///
        /// ЧТО БЫЛО. В `Assets` не было НИ ОДНОГО файла `*.asmdef` (замер тестировщика — 0). Все
        /// 266 рантайм-скриптов и 109 тестовых файлов жили в предопределённых `Assembly-CSharp` /
        /// `Assembly-CSharp-Editor`. Следствия: правка любого теста перекомпилировала ВСЁ; `internal`
        /// был недоступен тестам (отсюда стиль P4-3 — проверки по тексту исходников); тесты
        /// попадали в путь сборки плеера, и проект обходил это ФИЗИЧЕСКОЙ ПАРКОВКОЙ папки
        /// `Assets/_Project/08_Tests` в `Temp` (однажды потеряв на этом 23 файла).
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Сборки существуют и названы; тестовая сборка помечена `Editor` (в APK
        /// не попадёт) и ограничена `UNITY_INCLUDE_TESTS`; `internal` рантайма открыт тестам.
        /// </summary>
        [Test]
        public void P4_4_AssembliesExist_AndTestsAreSeparate()
        {
            string[] asmdefs = FindFiles("Assets", "*.asmdef", SearchOption.AllDirectories);
            Assert.GreaterOrEqual(asmdefs.Length, 5,
                "в Assets меньше пяти сборок — разделение кода не сделано (P4-4)");

            string runtime = ReadFile("Assets/_Project/KazistovVv.Runtime.asmdef");
            StringAssert.Contains("\"KazistovVv.Runtime\"", runtime, "нет сборки рантайма (P4-4)");

            string tests = ReadFile("Assets/_Project/08_Tests/Editor/KazistovVv.Tests.EditMode.asmdef");
            StringAssert.Contains("\"KazistovVv.Tests.EditMode\"", tests, "нет сборки EditMode-тестов (P4-4)");
            StringAssert.Contains("\"Editor\"", tests,
                "тестовая сборка не ограничена платформой Editor — тесты попадут в путь сборки плеера (P4-4)");
            StringAssert.Contains("UNITY_INCLUDE_TESTS", tests,
                "тестовая сборка не ограничена UNITY_INCLUDE_TESTS — в сборку плеера попадёт NUnit (P4-4)");

            string playmode = ReadFile("Assets/_Project/08_Tests/PlayMode/KazistovVv.Tests.PlayMode.asmdef");
            StringAssert.Contains("\"KazistovVv.Tests.PlayMode\"", playmode, "нет сборки PlayMode-тестов (P4-4)");

            string internals = ReadFile("Assets/_Project/01_Scripts/AssemblyInfo.cs");
            StringAssert.Contains("InternalsVisibleTo(\"KazistovVv.Tests.EditMode\")", internals,
                "внутренние типы рантайма закрыты для тестов — тесты снова будут проверять текст (P4-4)");
        }

        // ================================================================ P4-5: кэш Library

        /// <summary>
        /// P4-5: КОНВЕЙЕР КЭШИРУЕТ ПОДМНОЖЕСТВО `Library`, А НЕ ВЕСЬ КАТАЛОГ.
        ///
        /// ЧТО БЫЛО. Кэшировался весь `Library` — 25,5 ГБ при лимите кэша GitHub Actions 10 ГБ НА
        /// РЕПОЗИТОРИЙ. Такой кэш физически не может сохраниться: каждый прогон начинался с
        /// холодного импорта, то есть «кэш настроен» было неправдой.
        /// </summary>
        [Test]
        public void P4_5_Ci_DoesNotCacheWholeLibrary()
        {
            string yaml = ReadFile(".github/workflows/unity-tests.yml");
            Assert.Greater(yaml.Length, 3000, "файл конвейера подозрительно мал");

            // «path: Library» — ровно та строка, которая кэшировала 25,5 ГБ.
            Assert.IsFalse(Regex.IsMatch(yaml, @"(?m)^\s*path:\s*Library\s*$"),
                "конвейер снова кэширует Library ЦЕЛИКОМ — кэш не сохранится (лимит 10 ГБ, P4-5)");
            StringAssert.Contains("Library/PackageCache", yaml, "кэш не содержит PackageCache (P4-5)");
            StringAssert.Contains("Library/ArtifactDB", yaml, "кэш не содержит ArtifactDB (P4-5)");
        }

        // ================================================================ P4-6: категории

        /// <summary>
        /// P4-6: ТЕСТЫ РАЗМЕЧЕНЫ КАТЕГОРИЯМИ, И БЫСТРЫЙ ПРОГОН ОТЛИЧАЕТСЯ ОТ НОЧНОГО.
        ///
        /// ЧТО БЫЛО. Ни одного `[Category]`, ни `-testFilter`, ни `-assemblyNames`: конвейер гонял
        /// ВСЁ на каждый коммит, а разметить «быстрое» и «долгое» было нечем.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ. Каждая фикстура тестов имеет ровно одну категорию (`Fast` или `Slow`), и
        /// конвейер умеет запускать только быстрые. Проверка НЕВАКУУМНА: фикстур обязано быть много.
        /// </summary>
        [Test]
        public void P4_6_TestFixtures_AreCategorised()
        {
            string[] files = FindFiles("Assets/_Project/08_Tests/Editor", "*.cs", SearchOption.TopDirectoryOnly);
            Assert.GreaterOrEqual(files.Length, 100,
                "тестовых файлов меньше ста — проверка разметки была бы вакуумной (P4-2)");

            string noCategory = "";
            int fast = 0;
            int slow = 0;
            int fixtures = 0;
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                MatchCollection classes = Regex.Matches(text, @"(?m)^\s{4}public class (\w+Tests)\b");
                if (classes.Count == 0) continue;
                fixtures += classes.Count;
                if (Regex.IsMatch(text, @"Category\(KvTestCategories\.Fast\)") ||
                    Regex.IsMatch(text, @"\[Category\(""Fast""\)\]")) fast++;
                if (Regex.IsMatch(text, @"Category\(KvTestCategories\.Slow\)") ||
                    Regex.IsMatch(text, @"\[Category\(""Slow""\)\]")) slow++;
                if (!Regex.IsMatch(text, @"Category\("))
                    noCategory += Path.GetFileName(file) + "\n";
            }

            Assert.GreaterOrEqual(fixtures, 100, "фикстур Found меньше ста — разметка была бы вакуумной");
            Assert.AreEqual("", noCategory,
                "у этих фикстур НЕТ категории — быстрый прогон их не запустит и они станут «мёртвыми» (P4-6):\n" + noCategory);
            Assert.Greater(fast, 0, "ни одной быстрой фикстуры — быстрый прогон был бы пустым (P4-6)");
            Assert.Greater(slow, 0, "ни одной долгой фикстуры — делить прогон на быстрый и ночной нечем (P4-6)");

            string yaml = ReadFile(".github/workflows/unity-tests.yml");
            StringAssert.Contains("testCategory", yaml,
                "конвейер не умеет запускать по категории — разметка ни на что не влияет (P4-6)");
        }

        // ================================================================ P4-7: покрытие

        /// <summary>
        /// P4-7: ПАКЕТ ПОКРЫТИЯ ЕСТЬ, И ОТЧЁТ ПУБЛИКУЕТСЯ.
        ///
        /// ЧТО БЫЛО. В `Packages/manifest.json` не было `com.unity.testtools.codecoverage`, хотя
        /// `coverageOptions` в шаге тестов уже передавались: параметр уходил в никуда.
        /// </summary>
        [Test]
        public void P4_7_CoveragePackageIsPresent()
        {
            string manifest = ReadFile("Packages/manifest.json");
            StringAssert.Contains("com.unity.testtools.codecoverage", manifest,
                "пакета покрытия нет в manifest.json — coverageOptions снова уйдут в никуда (P4-7)");

            string yaml = ReadFile(".github/workflows/unity-tests.yml");
            StringAssert.Contains("coverageOptions", yaml, "конвейер не передаёт coverageOptions (P4-7)");
            StringAssert.Contains("CodeCoverage", yaml, "конвейер не публикует отчёт покрытия (P4-7)");
        }

        // ================================================================ P4-9: PlayMode и лог

        /// <summary>
        /// P4-9: PLAY-MODE ТЕСТ НЕ ГЛУШИТ ОШИБКИ ЦЕЛИКОМ.
        ///
        /// ЧТО БЫЛО. На время загрузки сцены ставился `LogAssert.ignoreFailingMessages = true` —
        /// глушились ВСЕ ошибки. Ошибка самой загрузки сцены (сломанный префаб, исключение в Awake)
        /// проглатывалась, и тест «проходил».
        /// ЧТО СТАЛО. Известный шум HDRP отсекается по ПОДСТРОКЕ, остальные ошибки собираются и
        /// валят тест.
        /// </summary>
        [Test]
        public void P4_9_PlayMode_FiltersKnownNoiseInsteadOfSilencingAll()
        {
            string source = ReadFile("Assets/_Project/08_Tests/PlayMode/KvFlowPlayModeTests.cs");
            StringAssert.Contains("KnownSceneLoadNoise", source,
                "нет списка известного шума — значит лог снова глушится целиком (P4-9)");
            StringAssert.Contains("No more space in Reflection Probe Atlas", source,
                "фильтр не содержит той самой строки HDRP, ради которой он и нужен (P4-9)");
            StringAssert.Contains("AssertNoUnexpectedLoadErrors", source,
                "собранные ошибки загрузки никуда не проверяются — глушение вернулось под другим именем (P4-9)");
        }

        // ================================================================ P4-10: диагностика

        /// <summary>
        /// P4-10: ДИАГНОСТИКА РАЗРАБОТЧИКА ЖИВЁТ В `Tools`, А НЕ В `Assets`.
        ///
        /// ЧТО БЫЛО. 15 файлов `Dsh*Diag.cs` лежали в `Assets/_Project/01_Scripts/Editor` — это
        /// инструменты, а не ассеты игры: они попадали в импорт проекта и в поле зрения тестов
        /// (KvDeadCodeTests искал «мёртвый код» среди инструментов).
        /// ЧТО СТАЛО. Они перенесены в `Tools/diag`. `KvE2eSettingsDiag` ОСТАЛСЯ в Assets — он
        /// подключён к конвейеру и вызывается как `-executeMethod`.
        /// </summary>
        [Test]
        public void P4_10_DiagnosticsLiveInTools()
        {
            string[] inAssets = FindFiles("Assets", "Dsh*Diag.cs", SearchOption.AllDirectories);
            Assert.AreEqual(0, inAssets.Length,
                "диагностика разработчика снова лежит в Assets (P4-10): " + string.Join(", ", inAssets));

            string[] inTools = FindFiles("Tools/diag", "*.cs", SearchOption.TopDirectoryOnly);
            Assert.GreaterOrEqual(inTools.Length, 10,
                "в Tools/diag меньше десяти файлов — переноса не было (P4-10)");

            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot,
                    "Assets/_Project/01_Scripts/Editor/KvE2eSettingsDiag.cs")),
                "КvE2eSettingsDiag пропал из Assets — он подключён к конвейеру и должен там остаться (P4-10)");
        }

        // ================================================================ P4-11: конфигурация сборки

        /// <summary>
        /// P4-11: КОНФИГУРАЦИЯ СБОРКИ НЕ ШАБЛОННАЯ И ПРОВЕРЯЕТСЯ ЦЕЛИКОМ.
        ///
        /// ЧТО БЫЛО. iPhone bundle id оставался НЕЗАМЕЩЁННЫМ ДЕФОЛТОМ шаблона URP
        /// (`com.Unity-Technologies.com.unity.template.urp-blank`), а тест проверял только
        /// Android-строку. Ни один тест не смотрел на `scriptingBackend`, `playModeTestRunnerEnabled`,
        /// графический API и версию сборки.
        /// ЧТО СТАЛО. Идентификатор приложения един для всех платформ и не шаблонный; сборка
        /// IL2CPP; PlayMode-тесты включены; графические API заданы; версия сборки не нулевая.
        /// </summary>
        [Test]
        public void P4_11_BuildConfig_IsCompleteAndNotTemplate()
        {
            string settings = ReadFile("ProjectSettings/ProjectSettings.asset");
            Assert.Greater(settings.Length, 5000, "ProjectSettings.asset подозрительно мал");

            Match android = Regex.Match(settings, @"(?m)^\s{4}Android:\s*(\S+)\s*$");
            Match iphone = Regex.Match(settings, @"(?m)^\s{4}iPhone:\s*(\S+)\s*$");
            Assert.IsTrue(android.Success, "в настройках нет идентификатора приложения для Android");
            Assert.IsTrue(iphone.Success, "в настройках нет идентификатора приложения для iPhone");

            Assert.IsFalse(iphone.Groups[1].Value.Contains("Unity-Technologies"),
                "iPhone bundle id — незамещённый дефолт шаблона URP: " + iphone.Groups[1].Value +
                " (P4-11); приложение нельзя опубликовать под чужим идентификатором");
            Assert.AreEqual(android.Groups[1].Value, iphone.Groups[1].Value,
                "идентификаторы приложения Android и iPhone расходятся — это две разные программы (P4-11)");

            StringAssert.Contains("AndroidBuildApkPerCpuArchitecture", settings, "настройки Android не прочитаны");
            Assert.IsTrue(Regex.IsMatch(settings, @"AndroidBundleVersionCode:\s*(\d+)"),
                "в настройках нет AndroidBundleVersionCode (P4-11)");
            Match code = Regex.Match(settings, @"AndroidBundleVersionCode:\s*(\d+)");
            Assert.GreaterOrEqual(int.Parse(code.Groups[1].Value), 1,
                "версия сборки Android нулевая — магазин такую не примет (P4-11)");

            // Абсолютный путь к ключу подписи в отслеживаемом файле — это и утечка, и чужая машина.
            Match keystore = Regex.Match(settings, @"(?m)^\s*AndroidKeystoreName:\s*(.*)$");
            Assert.IsTrue(keystore.Success, "нет строки AndroidKeystoreName");
            Assert.IsFalse(keystore.Groups[1].Value.Contains(":/"),
                "в отслеживаемом ProjectSettings.asset АБСОЛЮТНЫЙ путь к ключу подписи (P4-12): " +
                keystore.Groups[1].Value.Trim());
        }

        // ================================================================ P4-12: машинные пути

        /// <summary>
        /// P4-12: В `Tools/*.ps1` НЕТ ЖЁСТКИХ ПУТЕЙ К ЧУЖОЙ МАШИНЕ И ЧУЖОЙ ВЕРСИИ UNITY.
        ///
        /// ЧТО БЫЛО. Десять скриптов имели умолчанием `C:\KazistovVv_Build`, а путь к `Unity.exe`
        /// был вписан версией движка. После обновления Unity или на другой машине скрипты молча
        /// работали не с тем проектом либо падали «не найден Unity».
        /// </summary>
        [Test]
        public void P4_12_Tools_HaveNoMachineSpecificDefaults()
        {
            string[] scripts = FindFiles("Tools", "*.ps1", SearchOption.TopDirectoryOnly);
            Assert.GreaterOrEqual(scripts.Length, 15, "скриптов Tools меньше пятнадцати — проверка вакуумна");

            string bad = "";
            foreach (string script in scripts)
            {
                string text = File.ReadAllText(script);
                string name = Path.GetFileName(script);
                // Умолчание параметра проекта жёстким путём к ASCII-копии.
                if (Regex.IsMatch(text, @"\[string\]\$\w*Project\w*\s*=\s*'C:\\KazistovVv_Build'"))
                    bad += name + ": умолчание проекта — жёсткий путь C:\\KazistovVv_Build\n";
                // Умолчание пути к Unity жёсткой версией движка.
                if (Regex.IsMatch(text, @"\[string\]\$\w*Unity\w*\s*=\s*""C:\\Program Files\\Unity\\Hub\\Editor\\"))
                    bad += name + ": умолчание Unity — жёсткая версия движка\n";
            }
            Assert.AreEqual("", bad,
                "машино-зависимые умолчания в скриптах (P4-12) — на другой машине они молча работают не с тем проектом:\n" + bad);
        }

        // ================================================================ P4-13: триггеры CI

        /// <summary>
        /// P4-13: КОНВЕЙЕР ЗАПУСКАЕТСЯ НА РАБОЧЕЙ ВЕТКЕ, И ЭТО ПРОВЕРЯЕТСЯ ПОВЕДЕНИЕМ, А НЕ СПИСКОМ.
        ///
        /// ЧТО БЫЛО. Мета-тесты искали подстроки `skipped`, `UNITY_LICENSE`, `static-checks` в
        /// тексте YAML и НЕ ПОЙМАЛИ ГЛАВНОГО: триггеры перечисляли `Cline`, `main`, `master`, а вся
        /// работа идёт в `Develop` — конвейер не запускался на настоящих изменениях НИ РАЗУ.
        /// ЧТО ПРОВЕРЯЕТСЯ. Раздел `on:` обязан запускаться на любых ветках (`'**'`) либо явно
        /// включать рабочую ветку проекта.
        /// </summary>
        [Test]
        public void P4_13_Ci_TriggersCoverWorkingBranch()
        {
            string yaml = ReadFile(".github/workflows/unity-tests.yml");

            Match block = Regex.Match(yaml, @"(?ms)^on:\s*$(.*?)^[a-z#]");
            string onBlock = block.Success ? block.Groups[1].Value : yaml;
            Assert.Greater(onBlock.Length, 20, "не удалось прочитать раздел on: конвейера (проверка вакуумна)");

            bool anyBranch = onBlock.Contains("'**'") || onBlock.Contains("\"**\"");
            bool develop = onBlock.Contains("Develop");
            Assert.IsTrue(anyBranch || develop,
                "триггеры конвейера не включают рабочую ветку Develop (P4-13) — конвейер " +
                "не запустится на настоящих изменениях, а зелёная галочка будет ложью");
        }

        // ================================================================ P4-16: эталон символов

        /// <summary>
        /// P4-16: СИМВОЛЫ СБОРКИ ПОД СТОРОЖЕМ, А НЕ «САМИ ПО СЕБЕ».
        ///
        /// ЧТО БЫЛО. Во время прогона в `scriptingDefineSymbols` для Android появлялась строка
        /// `SENTIS_ANALYTICS_ENABLED` и затем исчезала; источник не был найден, а файл настроек
        /// ОТСЛЕЖИВАЕМЫЙ — то есть настройки сборки менялись у проекта за спиной.
        /// ИСТОЧНИК НАЙДЕН 12.11.2026: `com.unity.ai.inference` добавляет символ по настройке
        /// аналитики редактора (`Library/PackageCache/.../Analytics/AnalyticsDefineManager.cs`).
        /// РЕШЕНИЕ: аналитика редактора выключена, символ детерминированно отсутствует, а сторож
        /// `Tools/repo-guards.ps1` сверяет набор с эталоном.
        /// </summary>
        [Test]
        public void P4_16_DefineSymbols_AreGuardedByReference()
        {
            string guards = ReadFile("Tools/repo-guards.ps1");
            StringAssert.Contains("scriptingDefineSymbols", guards,
                "сторож не проверяет символы сборки — P4-16 вернётся молча");
            StringAssert.Contains("expectedAndroidDefines", guards,
                "в стороже нет ЭТАЛОНА символов — сверять не с чем");

            // Символ, которым управляет ПАКЕТ, а не проект, обязан быть назван отдельно: иначе он
            // либо валит сторожа на ровном месте, либо — что хуже — попадает в эталон и перестаёт
            // быть заметным, и «само появилось» возвращается.
            StringAssert.Contains("packageManagedDefines", guards,
                "сторож не различает символы проекта и символы, которые добавляет пакет (P4-16)");
            StringAssert.Contains("SENTIS_ANALYTICS_ENABLED", guards,
                "в стороже не назван символ, который добавляет com.unity.ai.inference (P4-16)");
        }

        // ================================================================ P4-19: два APK

        /// <summary>
        /// P4-19: У КАЖДОГО УСТРОЙСТВА СВОЙ APK, И ЭТО ЗАПИСАНО ДЛЯ ТЕСТИРОВЩИКА.
        ///
        /// ЧТО БЫЛО. Оба варианта сборки писали ОДИН файл `KazistovVv.apk`: поставив не тот APK,
        /// получали «чёрный экран» и искали ошибку в коде. В конвейере вариант был один.
        /// </summary>
        [Test]
        public void P4_19_BothApkVariantAreNamedAndDocumented()
        {
            string script = ReadFile("Tools/xr-build-apk-ascii.ps1");
            StringAssert.Contains("KazistovVv-VR-vulkan.apk", script, "нет имени артефакта шлема (P4-19)");
            StringAssert.Contains("KazistovVv-AR-gles3.apk", script, "нет имени артефакта планшета (P4-19)");

            string yaml = ReadFile(".github/workflows/unity-tests.yml");
            StringAssert.Contains("matrix", yaml, "в конвейере нет матрицы вариантов сборки (P4-19 п.2)");
            StringAssert.Contains("KazistovVv-VR-vulkan", yaml, "конвейер не собирает вариант шлема (P4-19 п.2)");
            StringAssert.Contains("KazistovVv-AR-gles3", yaml, "конвейер не собирает вариант планшета (P4-19 п.2)");

            string doc = ReadFile("Docs/ПОДКЛЮЧЕНИЕ_ТЕСТИРОВЩИКА.md");
            Assert.Greater(doc.Length, 500, "документ для тестировщика подозрительно мал");
            StringAssert.Contains("KazistovVv-VR-vulkan.apk", doc,
                "в документе не сказано, какой APK ставить на шлем (P4-19 п.3)");
            StringAssert.Contains("KazistovVv-AR-gles3.apk", doc,
                "в документе не сказано, какой APK ставить на планшет (P4-19 п.3)");
        }

        /// <summary>
        /// P4-19 п.4: ЖУРНАЛ ЗАПУСКА НАЗЫВАЕТ ФАКТИЧЕСКИЙ ГРАФИЧЕСКИЙ API И УСТРОЙСТВО.
        ///
        /// ЧТО БЫЛО. По журналу нельзя было отличить «не тот APK» от «не тот драйвер»: оба случая
        /// выглядели как чёрный экран при нуле ошибок.
        /// ЧТО ПРОВЕРЯЕТСЯ ПОВЕДЕНИЕМ: чистая функция `KvEnvBanner.Describe` собирает строку с API,
        /// устройством и ПОДСКАЗКОЙ, для какого устройства эта сборка.
        /// </summary>
        [Test]
        public void P4_19_EnvBanner_NamesGraphicsApiAndDevice()
        {
            string line = KvEnvBanner.Describe(GraphicsDeviceType.Vulkan, "Adreno 740",
                "Redmi Pad 2 Pro", "Android 15", "com.KazistovVv.app");

            StringAssert.Contains(KvEnvBanner.LogPrefix, line, "у строки нет метки для поиска в журнале");
            StringAssert.Contains("Vulkan", line, "в строке нет фактического графического API");
            StringAssert.Contains("Adreno 740", line, "в строке нет графического устройства");
            StringAssert.Contains("Redmi Pad 2 Pro", line, "в строке нет модели устройства");
            StringAssert.Contains("com.KazistovVv.app", line, "в строке нет идентификатора приложения");
            StringAssert.Contains("ШЛЕМА", line, "в строке нет подсказки, для какого устройства эта сборка");

            // Второй вариант: GLES3 — это планшет, и подсказка обязана быть ДРУГОЙ.
            string tablet = KvEnvBanner.Describe(GraphicsDeviceType.OpenGLES3, "Mali-G610",
                "Redmi Pad 2 Pro", "Android 15", "com.KazistovVv.app");
            StringAssert.Contains("ПЛАНШЕТА", tablet,
                "для GLES3 нет подсказки про планшет — «не тот APK» снова неотличим (P4-19 п.4)");

            // Третий вариант: графики нет вовсе — это отдельный, самый частый диагноз «чёрного экрана».
            string none = KvEnvBanner.Describe(GraphicsDeviceType.Null, "", "", "", "");
            StringAssert.Contains("КАДРОВ НЕ БУДЕТ", none,
                "отсутствие графического API не названо прямо (P4-19 п.4)");

            // Пустые необязательные поля не должны давать « ·  · » — иначе строка нечитаема.
            Assert.IsFalse(none.Contains("GPU"), "пустое устройство GPU попало в строку");
            Assert.IsFalse(none.Contains("ОС"), "пустая ОС попала в строку");
        }

        // ================================================================ P4-14: новые файлы в git

        /// <summary>
        /// P4-14: НОВЫЕ ФАЙЛЫ ПЕРЕЧИСЛЕНЫ ЯВНО — untracked тест конвейер не увидит.
        ///
        /// Проверка мягкая по форме, но жёсткая по сути: она требует, чтобы файлы, от которых
        /// зависит приёмка (сборки, сторож, Е2Е, новые тесты), СУЩЕСТВОВАЛИ на диске. Попадание в
        /// индекс git проверяет `Tools/repo-guards.ps1` и отчёт.
        /// </summary>
        [Test]
        public void P4_14_AcceptanceCriticalFiles_Exist()
        {
            string[] required =
            {
                "Tools/repo-guards.ps1",
                "Tools/ci-local.ps1",
                ".github/workflows/nightly.yml",
                "Assets/_Project/08_Tests/Editor/KvVrMrSwitchTests.cs",
                "Assets/_Project/08_Tests/Editor/KvP0FixTests.cs",
                "Assets/_Project/08_Tests/Editor/KvR1FixTests.cs",
                "Assets/_Project/08_Tests/Editor/KvP4FixTests.cs",
                "Assets/_Project/01_Scripts/Editor/KvE2eSettingsDiag.cs",
                "Assets/_Project/06_KazistovVv_UI/03_Scripts/Core/KvDockGeometry.cs"
            };
            string missing = "";
            foreach (string relative in required)
            {
                string path = Path.Combine(ProjectRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(path)) missing += relative + "\n";
            }
            Assert.AreEqual("", missing, "файлы приёмки отсутствуют на диске:\n" + missing);
        }
    }
}

