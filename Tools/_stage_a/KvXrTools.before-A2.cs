using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using UnityEditor.XR.Management;
using KazistovVvXr;
using TrajectoryCore;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ИНСТРУМЕНТЫ XR-ЭТАПА (сессия §28, этапы 0–4 плана XR).
    ///
    /// ПОЧЕМУ ВСЁ ДЕЛАЕТСЯ КОДОМ, А НЕ РУКАМИ В РЕДАКТОРЕ. Шлема нет, а собрать XR-сцену
    /// надо так, чтобы её можно было ПОВТОРИТЬ и ПРОВЕРИТЬ: сцена из кода проверяется
    /// повторным запуском и сравнением, сцена из ручных действий — только памятью.
    /// Плюс: проект ведётся в VCS, и воспроизводимая сборка сцены не создаёт конфликтов
    /// «а у меня сцена другая».
    ///
    /// ЧТО ЗДЕСЬ ГЛАВНОЕ (<see cref="ConfigureXrLoaders"/>) — НАЙДЕННЫЙ ДЕФЕКТ:
    /// в проекте активными лоадерами XR стояли **Mock HMD** и аркадный ARCore,
    /// а OpenXR — просто лежал ассетом. С таким набором собранное приложение в шлеме
    /// НЕ РАБОТАЛО БЫ вообще: Mock HMD — это заглушка для отладки без устройства.
    /// Именно поэтому исправление входит в первый же проход: без него всё остальное
    /// не проверить на устройстве.
    /// </summary>
    public static class KvXrTools
    {
        private const string MenuRoot = "Tools/KazistovVv XR/";

        /// <summary>Имя XR-сцены (решение [D6]: отдельная сцена, MainScene не трогается).</summary>
        public const string XrSceneName = "XrMain";

        /// <summary>Папка сцен проекта.</summary>
        public const string ScenesFolder = "Assets/_Project/00_Scenes";

        /// <summary>Полный путь XR-сцены.</summary>
        public static string XrScenePath { get { return ScenesFolder + "/" + XrSceneName + ".unity"; } }

        // ================================================================== лоадеры XR

        /// <summary>
        /// Включить OpenXR и выключить Mock HMD на всех целевых платформах.
        ///
        /// Проверяемые условия: ровно один активный лоадер — OpenXR; Mock HMD, Simulation
        /// и ARCore выключены. Simulation (XR Device Simulator) — инструмент отладки,
        /// его включение в сборку под шлем даёт «симулятор вместо трекинга».
        /// </summary>
        [MenuItem(MenuRoot + "1. Включить OpenXR (выключить Mock HMD)", false, 10)]
        public static void ConfigureXrLoaders()
        {
            XRGeneralSettingsPerBuildTarget perTarget =
                AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                    "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");

            if (perTarget == null)
            {
                Debug.LogError("[KvXr] Не найден XRGeneralSettingsPerBuildTarget — настройки XR отсутствуют.");
                return;
            }

            XRLoader openXr = FindOpenXrLoader();
            if (openXr == null)
            {
                Debug.LogError("[KvXr] Не найден OpenXRLoader.asset — пакет OpenXR не установлен?");
                return;
            }

            // Группы платформ, а не отдельные платформы: настройки XR Management хранятся
            // именно по группам (Standalone, Android), и `SettingsForBuildTarget` принимает
            // BuildTargetGroup — это выяснено компилятором 02.10.2026.
            BuildTargetGroup[] targets = { BuildTargetGroup.Standalone, BuildTargetGroup.Android };
            var report = new List<string>();

            foreach (BuildTargetGroup target in targets)
            {
                XRGeneralSettings settings = perTarget.SettingsForBuildTarget(target);
                if (settings == null)
                {
                    report.Add(target + ": настроек нет (пропущено)");
                    continue;
                }

                XRManagerSettings manager = settings.Manager;
                if (manager == null)
                {
                    report.Add(target + ": менеджер не создан");
                    continue;
                }

                // Пересобираем список лоадеров: OpenXR — единственный рабочий, остальные
                // (Mock HMD, Simulation, ARCore) убираются.
                // ЧЕРЕЗ `TrySetLoaders`, А НЕ `loaders`: устаревшее свойство `loaders`
                // компилятор помечает как obsolete (уровень CS0618 ловится гейтом проекта),
                // а прямая правка списка в рантайме вообще объявлена неопределённым поведением.
                manager.TrySetLoaders(new List<XRLoader> { openXr });
                EditorUtility.SetDirty(manager);
                EditorUtility.SetDirty(settings);

                report.Add(string.Format("{0}: лоадеров {1}", target, manager.activeLoaders.Count));
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[KvXr] Лоадеры XR пересобраны на активный OpenXR: " + string.Join(" · ", report) +
                      ". Mock HMD/Simulation/ARCore отключены: они нужны для отладки без устройства, " +
                      "а в сборке под PICO 4 Ultra подменяли бы реальный трекинг.");
        }

        /// <summary>
        /// Найти ассет лоадера OpenXR.
        ///
        /// ПОЧЕМУ НЕ `LoadAssetAtPath&lt;XRLoader&gt;`. Этот вызов вернул null на существующем
        /// ассете (проверено 02.10.2026): тип ассета лоадера не является прямым наследником
        /// `XRLoader` в глазах загрузчика ассетов. Поэтому ассет берётся как ScriptableObject,
        /// а тип проверяется во время выполнения — так надёжнее и не зависит от версии пакета.
        /// </summary>
        public static XRLoader FindOpenXrLoader()
        {
            const string path = "Assets/XR/Loaders/OpenXRLoader.asset";
            var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(path);
            if (asset == null) return null;

            XRLoader loader = asset as XRLoader;
            if (loader != null) return loader;

            // Запасной путь: любой ассет лоадера в папке XR/Loaders с «OpenXR» в имени.
            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/XR/Loaders" });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (assetPath == null) continue;
                if (assetPath.IndexOf("OpenXR", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                var candidate = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
                var asLoader = candidate as XRLoader;
                if (asLoader != null) return asLoader;
            }
            return null;
        }

        /// <summary>Проверить, что активен именно OpenXR — вызывается диагностикой и гейтом.</summary>
        public static bool VerifyOpenXrActive(out string details)
        {
            XRGeneralSettingsPerBuildTarget perTarget =
                AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                    "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            if (perTarget == null) { details = "нет XRGeneralSettingsPerBuildTarget"; return false; }

            var lines = new List<string>();
            bool ok = true;
            foreach (BuildTargetGroup target in new[] { BuildTargetGroup.Standalone, BuildTargetGroup.Android })
            {
                XRGeneralSettings settings = perTarget.SettingsForBuildTarget(target);
                if (settings == null || settings.Manager == null)
                {
                    lines.Add(target + ": настроек нет");
                    ok = false;
                    continue;
                }
                var names = new List<string>();
                foreach (XRLoader loader in settings.Manager.activeLoaders)
                    names.Add(loader != null ? loader.name : "null");

                bool hasOpenXr = names.Exists(n => n != null && n.Contains("OpenXR"));
                bool hasMock = names.Exists(n => n != null && n.Contains("MockHMD"));
                if (!hasOpenXr || hasMock) ok = false;
                lines.Add(string.Format("{0}: [{1}]", target, string.Join(", ", names)));
            }

            details = string.Join(" · ", lines);
            return ok;
        }

        // ================================================================== XR-сцена

        /// <summary>
        /// Собрать XR-сцену с нуля: риг шлема, интерфейс, локомоция, указатель, MR-комната.
        /// Сцена СОЗДАЁТСЯ ЗАНОВО и перезаписывается целиком — она генерируемая, ручных
        /// правок в ней быть не должно (иначе они потеряются; так и написано в подсказке).
        /// </summary>
        [MenuItem(MenuRoot + "2. Собрать XR-сцену (VR/MR)", false, 20)]
        public static void BuildXrScene()
        {
            ConfigureXrLoaders();

            if (!Directory.Exists(ScenesFolder))
                Directory.CreateDirectory(ScenesFolder);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // --- корень рига: сюда вешается камера шлема, локомоция и указатель
            var rig = new GameObject("XrRig");
            var cameraGo = new GameObject("XrCamera");
            cameraGo.transform.SetParent(rig.transform, false);
            cameraGo.tag = "MainCamera";

            Camera cam = cameraGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 300f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cameraGo.AddComponent<AudioListener>();

            cameraGo.AddComponent<KvXrRig>();
            // Сторож обязан стоять РАНЬШЕ остальных: он снимает с камеры шлема
            // десктопные сервисы, которые считают, что камерой управляет мышь.
            cameraGo.AddComponent<KvXrCameraGuard>();
            KvXrLocomotion locomotion = rig.AddComponent<KvXrLocomotion>();
            locomotion.useRightStickForMove = false;
            KvXrUiPointer pointer = cameraGo.AddComponent<KvXrUiPointer>();

            // --- панель: канвас интерфейса переводится в мировое пространство
            var panelGo = new GameObject("XrPanelAnchor");
            panelGo.transform.SetParent(rig.transform, false);
            var anchor = panelGo.AddComponent<KvXrPanelAnchor>();
            anchor.targetCamera = cam;

            // --- начинка: столы и роботы (решение владельца «добавить роботов и столы»).
            // Строится РАНЬШЕ MR-комнаты (порядок задан атрибутом DefaultExecutionOrder),
            // иначе MR искал бы робота, которого ещё нет.
            var contentGo = new GameObject("XrSceneContent");
            contentGo.transform.SetParent(rig.transform, false);
            var content = contentGo.AddComponent<KvXrSceneContent>();
            content.createStands = true;
            content.tableTopHeight = StandBuilder.TopHeight;

            // --- MR-комната: реальный стол, робот 1:1, слои виртуального
            var roomGo = new GameObject("XrMrRoom");
            roomGo.transform.SetParent(rig.transform, false);
            var room = roomGo.AddComponent<KvXrMrRoom>();
            room.headCamera = cam;
            room.table = KvXrMrPlacement.DefaultTable;
            room.room = KvXrMrPlacement.DefaultRoom;

            // --- сама оболочка интерфейса проекта: меню, тулбар, дерево, свойства, настройки.
            // Без неё в шлеме нечего показывать: сцена XR отвечает за риг и камеру, а весь
            // интерфейс строит менеджер проекта (он же создаёт канвас, который затем
            // переводит в мировое пространство KvXrPanelAnchor).
            var uiGo = new GameObject("KazistovVv_UI");
            uiGo.AddComponent<KazistovVvUI.KazistovVvUIManager>();

            // --- шина событий интерфейса: без неё луч не найдёт ни одной кнопки.
            // Модуль берётся ТОТ ЖЕ, что в оболочке проекта (InputSystemUIInputModule):
            // в проекте включён только новый Input System, и StandaloneInputModule
            // на нём не работает вовсе.
            var eventsGo = new GameObject("XrEventSystem");
            eventsGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            var uiModule = eventsGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            uiModule.AssignDefaultActions();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, XrScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[KvXr] XR-сцена собрана: " + XrScenePath +
                      ". Компоненты: KvXrRig (камера шлема), KvXrLocomotion (телепорт/плавный ход), " +
                      "KvXrUiPointer (луч и нажатие), KvXrPanelAnchor (плоская панель), KvXrMrRoom (реальный стол). " +
                      "ВНИМАНИЕ: сцена генерируемая — ручные правки в ней будут потеряны при пересборке.");
        }

        /// <summary>Добавить XR-сцену в список сборки (и не тронуть порядок существующих).</summary>
        [MenuItem(MenuRoot + "3. Добавить XR-сцену в сборку", false, 30)]
        public static void AddXrSceneToBuild()
        {
            if (!File.Exists(XrScenePath) && !File.Exists(XrScenePath.Replace("/", Path.DirectorySeparatorChar.ToString())))
            {
                Debug.LogError("[KvXr] Нет XR-сцены по пути " + XrScenePath + " — сначала соберите её (пункт 2).");
                return;
            }

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool exists = scenes.Exists(s => s.path == XrScenePath);
            if (!exists)
            {
                scenes.Add(new EditorBuildSettingsScene(XrScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
                Debug.Log("[KvXr] XR-сцена добавлена в список сборки: " + XrScenePath);
            }
            else
            {
                Debug.Log("[KvXr] XR-сцена уже в списке сборки: " + XrScenePath);
            }

            var names = new List<string>();
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
                names.Add((s.enabled ? "+ " : "− ") + s.path);
            Debug.Log("[KvXr] Сцены сборки: " + string.Join(" | ", names));
        }

        // ================================================================== сборка APK

        /// <summary>
        /// Собрать APK для шлема. Вызывается ИЗ КОМАНДНОЙ СТРОКИ:
        /// `Unity.exe -batchmode -nographics -projectPath &lt;путь&gt; -executeMethod
        /// KazistovVvXrEditor.KvXrTools.BuildAndroidApk -logFile &lt;лог&gt;`.
        ///
        /// ПОЧЕМУ ИМЕННО ТАК: сборка занимает минуты и должна воспроизводиться одной
        /// командой в отчёте, а не «я нажал Build». Путь выходного файла — в `Builds/`.
        /// </summary>
        public static void BuildAndroidApk()
        {
            // КУДА СОБИРАТЬ — И ПОЧЕМУ НЕ В ПАПКУ ПРОЕКТА.
            //
            // Найдено 02.10.2026, и это седьмой слой проблемы: Android-инструменты Unity
            // (Gradle) отказываются работать, если путь проекта содержит НЕ-ASCII символы.
            // Папка проекта — «…\новое пространство\KazistovVv», а выше по пути есть «Ольга»,
            // и сборка падала с «Invalid project path … contains non-ASCII characters».
            //
            // Обойти это, не трогая рабочий каталог владельца, можно так: собирать в путь
            // из одних латинских букв (`C:\KazistovVv_Build\`), а готовый APK возвращать
            // в `Builds\` проекта. Проект при этом не перемещается.
            const string asciiOutputDir = @"C:\KazistovVv_Build";
            string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "Builds");
            if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);
            if (!Directory.Exists(asciiOutputDir)) Directory.CreateDirectory(asciiOutputDir);

            string apkPath = Path.Combine(asciiOutputDir, "KazistovVv.apk");
            string finalApkPath = Path.Combine(outputDir, "KazistovVv.apk");

            ConfigureXrLoaders();
            AddXrSceneToBuildSilent();

            // ПЕРЕКЛЮЧАЕМ АКТИВНУЮ ПЛАТФОРМУ НА ANDROID ПЕРЕД СБОРКОЙ.
            //
            // Почему это обязательно: HDRP-валидатор перед сборкой проверяет графические API
            // всех платформ и падает на «Platform StandaloneWindows64 with graphics API
            // OpenGLES3 is not supported with HDRP». На настройки Windows мы не покушаемся
            // (это рабочая конфигурация проекта), но и собрать APK без переключения нельзя.
            // Исходная платформа запоминается и возвращается на место после сборки.
            BuildTarget previousTarget = EditorUserBuildSettings.activeBuildTarget;
            bool switched = previousTarget != BuildTarget.Android;

            if (switched)
            {
                Debug.Log("[KvXr] Переключение активной платформы: " + previousTarget + " → Android (вернём обратно после сборки)");
                bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Android, BuildTarget.Android);
                if (!ok)
                    Debug.LogWarning("[KvXr] Не удалось переключить платформу — сборка может отказать на проверке HDRP.");
            }

            RenderPipelineAsset originalDefault = GraphicsSettings.defaultRenderPipeline;
            RenderPipelineAsset urpForAndroid = FindUrpAsset();

            // ПОЧЕМУ ПОДМЕНА ГЛОБАЛЬНОГО УМОЛЧАНИЯ ОТКЛЮЧЕНА (замерено 02.10.2026).
            //
            // Идея была разумной: HDRP-валидатор видит пайплайны, применимые к платформе, и
            // глобальное умолчание в этот список входит. Подмена на URP действительно снимала
            // отказ валидатора — но вызывала катастрофу по времени: Unity начинал компилировать
            // ВЕСЬ набор вариантов URP, и вместо 1 536 вариантов на сборку приходило **442 368**
            // (`Universal Render Pipeline/Lit` первым в списке). На четырёх ядрах это порядка
            // четырёх суток компиляции — то есть сборка перестаёт быть возможной вовсе.
            //
            // Оказалось, что подмена и не нужна: HDRP-валидатор обходится тем, что Android
            // уже назначен на уровень качества «Mobile» с URP (см. EnsureAndroidHasNoHdrpLevels),
            // а встроенный плеер берёт пайплайн из уровня качества. Проверка боем: сборка без
            // подмены проходит предварительную обработку в минуты, а не в дни.
            bool useUrpDefaultDuringBuild = false;

            // ОБЪЯВЛЕНИЯ ПЕРЕД try — блок finally их тоже использует (иначе «имя не существует
            // в текущем контексте»: эта ошибка уже ловилась компилятором дважды, 02.10.2026).
            string testsPath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "_Project", "08_Tests");
            string testsParked = Path.Combine(Directory.GetCurrentDirectory(), "Temp", "_tests_parked_for_build");
            bool testsMoved = false;

            try
            {
            // ТЕСТЫ УБИРАЮТСЯ ИЗ СБОРКИ.
            //
            // Найдено 02.10.2026: `Assets/_Project/08_Tests/PlayMode/KvFlowPlayModeTests.cs`
            // использует `UnityPlatformAttribute` из Unity Test Framework, а в сборке плеера
            // этой библиотеки нет — компиляция падает с CS0246 и сборка обрывается.
            // Тесты в APK не нужны вовсе, поэтому папка тестов убирается из Assets на время
            // сборки и ВОЗВРАЩАЕТСЯ в finally (даже если сборка упадёт).
            if (Directory.Exists(testsPath))
            {
                try
                {
                    if (Directory.Exists(testsParked)) Directory.Delete(testsParked, true);
                    Directory.CreateDirectory(Path.GetDirectoryName(testsParked));
                    Directory.Move(testsPath, testsParked);
                    testsMoved = true;
                    Debug.Log("[KvXr] Тесты временно убраны из Assets на время сборки APK (в сборке плеера их нет).");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[KvXr] Не удалось убрать тесты из сборки: " + e.Message);
                }
            }

            // Решение [D9]: шлем собирается на URP, компьютер остаётся на HDRP.
            // Уровни с HDRP убираются из набора Android ДО сборки — иначе пакет HDRP
            // обрывает сборку сообщением «Platform Android is not supported with HDRP».
            EnsureAndroidHasNoHdrpLevels();
            SetAndroidGraphicsApi();

            // Подмена глобального умолчания ВЫКЛЮЧЕНА (useUrpDefaultDuringBuild = false) —
            // причина расписана выше: она вызывала 287-кратный рост числа вариантов шейдеров.
            // Android получает URP через уровень качества «Mobile», и этого достаточно.
            if (useUrpDefaultDuringBuild && urpForAndroid != null && originalDefault != urpForAndroid)
            {
                GraphicsSettings.defaultRenderPipeline = urpForAndroid;
                Debug.Log("[KvXr] На время сборки глобальное умолчание графики переключено на URP: " +
                    urpForAndroid.name + ".");
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { XrScenePath },
                locationPathName = apkPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            };

            UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log(string.Format("[KvXr] APK собран: {0} · {1:0.0} МБ · {2} · предупреждений {3} · ошибок {4}",
                    apkPath, summary.totalSize / (1024f * 1024f), summary.platform, summary.totalWarnings, summary.totalErrors));

                // Копия в проект — чтобы артефакт лежал рядом с проектом, а сама сборка шла
                // по латинскому пути (см. пояснение выше).
                try
                {
                    if (File.Exists(apkPath))
                    {
                        File.Copy(apkPath, finalApkPath, true);
                        Debug.Log("[KvXr] Копия APK для владельца: " + finalApkPath);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[KvXr] APK собран, но скопировать его в Builds не удалось: " + e.Message);
                }
            }
            else
            {
                Debug.LogError(string.Format("[KvXr] СБОРКА APK НЕ УДАЛАСЬ: {0} · ошибок {1} — {2}",
                    summary.result, summary.totalErrors, summary.outputPath));
            }
            }
            finally
            {
                // Тесты возвращаются ВСЕГДА — даже если сборка упала: иначе проект остался бы
                // без папки тестов и следующий прогон EditMode-тестов нашёл бы 0 тестов.
                if (testsMoved)
                {
                    try
                    {
                        if (Directory.Exists(testsParked) && !Directory.Exists(testsPath))
                        {
                            Directory.Move(testsParked, testsPath);
                            Debug.Log("[KvXr] Тесты возвращены в Assets.");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[KvXr] НЕ УДАЛОСЬ вернуть тесты на место: " + e.Message +
                            ". Они лежат в Temp/_tests_parked_for_build — перенесите вручную.");
                    }
                }

                // Возврат глобального умолчания графики — в finally, чтобы оно вернулось
                // даже при падении сборки: иначе следующий запуск редактора остался бы
                // с URP вместо HDRP и десктопная сцена выглядела бы иначе.
                if (originalDefault != null && GraphicsSettings.defaultRenderPipeline != originalDefault)
                {
                    GraphicsSettings.defaultRenderPipeline = originalDefault;
                    Debug.Log("[KvXr] Глобальное умолчание графики возвращено: " + originalDefault.name);
                }

                if (switched)
                {
                    EditorUserBuildSettings.SwitchActiveBuildTarget(
                        BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
                    Debug.Log("[KvXr] Активная платформа возвращена: StandaloneWindows64");
                }
            }
        }

        /// <summary>
        /// Найти URP-ассет для сборки под шлем: берётся `Assets/Settings/Mobile_RPAsset.asset`,
        /// а если его нет — любой URP-ассет в `Assets/Settings`, иначе сборка осталась бы
        /// без пайплайна и картинка в шлеме была бы розовой.
        /// </summary>
        public static RenderPipelineAsset FindUrpAsset()
        {
            var direct = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>("Assets/Settings/Mobile_RPAsset.asset");
            if (direct != null) return direct;

            string[] guids = AssetDatabase.FindAssets("t:RenderPipelineAsset", new[] { "Assets/Settings" });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path == null || path.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) < 0) continue;
                var asset = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(path);
                if (asset != null) return asset;
            }
            return null;
        }

        /// <summary>
        /// ОСТАВИТЬ ANDROID БЕЗ HDRP — решение [D9] (URP для шлема, HDRP для компьютера).
        ///
        /// ПОЧЕМУ ЭТО И ЕСТЬ РАЗГАДКА. Android уже назначен на уровень качества «Mobile»,
        /// а тот использует **URP** — то есть всё было настроено правильно. Но в НАБОРЕ уровней
        /// качества Android лежал и уровень «PC» с HDRP, а пакет HDRP при сборке проверяет
        /// ВСЕ уровни, применимые к целевой платформе
        /// (`HDRPBuildData`: `buildTarget.TryGetRenderPipelineAssets<HDRenderPipelineAsset>`).
        /// Поэтому валидатор видел HDRP в сборке под Android и обрывал её сообщением
        /// «Platform Android is not supported with HDRP» — хотя Android его и не использовал.
        ///
        /// Лечение: убрать HDRP-уровни из набора Android. Компьютер при этом не теряет ничего —
        /// уровни остаются в наборе Windows, и «PC» там по-прежнему с HDRP.
        /// </summary>
        public static void EnsureAndroidHasNoHdrpLevels()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogWarning("[KvXr] Не удалось открыть QualitySettings.asset — уровни качества не менялись.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            SerializedProperty perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (levels == null || perPlatform == null)
            {
                Debug.LogWarning("[KvXr] Не найдены списки уровней качества — пропущено.");
                return;
            }

            // 1. Найти индексы URP-уровней (это те, чей customRenderPipeline наследует URP).
            var urpLevels = new List<int>();
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty pipeline = level.FindPropertyRelative("customRenderPipeline");
                var asset = pipeline != null ? pipeline.objectReferenceValue as ScriptableObject : null;
                string typeName = asset != null ? asset.GetType().FullName : string.Empty;
                string levelName = level.FindPropertyRelative("name") != null
                    ? level.FindPropertyRelative("name").stringValue : ("уровень " + i);

                if (typeName.IndexOf("UniversalRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    urpLevels.Add(i);
                    Debug.Log("[KvXr] Уровень «" + levelName + "» (индекс " + i + ") — URP.");
                }
                else if (typeName.IndexOf("HDRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Debug.Log("[KvXr] Уровень «" + levelName + "» (индекс " + i + ") — HDRP (для Android не подходит).");
                }
            }

            if (urpLevels.Count == 0)
            {
                Debug.LogError("[KvXr] В проекте нет ни одного уровня качества на URP — сборка под шлем " +
                    "не сможет использовать URP. Проверьте Assets/Settings/Mobile_RPAsset.asset.");
                return;
            }

            // 2. Набор Android = только URP-уровни.
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                SerializedProperty key = entry.FindPropertyRelative("key");
                if (key == null || key.stringValue != "Android") continue;

                SerializedProperty value = entry.FindPropertyRelative("value");
                int androidDefault = urpLevels[0];
                for (int k = 0; k < urpLevels.Count; k++)
                {
                    string name = levels.GetArrayElementAtIndex(urpLevels[k])
                        .FindPropertyRelative("name").stringValue;
                    if (name.IndexOf("Mobile", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        androidDefault = urpLevels[k];
                        break;
                    }
                }

                if (value != null) value.intValue = androidDefault;
                Debug.Log("[KvXr] Android: уровень качества по умолчанию — индекс " + androidDefault +
                    " (URP). Уровни с HDRP для Android отключены.");
                break;
            }

            // 3. Отключить HDRP-уровни именно для Android (как это делает редактор галочкой платформы).
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty pipeline = level.FindPropertyRelative("customRenderPipeline");
                var asset = pipeline != null ? pipeline.objectReferenceValue as ScriptableObject : null;
                string typeName = asset != null ? asset.GetType().FullName : string.Empty;
                if (typeName.IndexOf("HDRenderPipelineAsset", StringComparison.OrdinalIgnoreCase) < 0) continue;

                SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");
                if (excluded == null) continue;

                bool already = false;
                for (int k = 0; k < excluded.arraySize; k++)
                {
                    if (excluded.GetArrayElementAtIndex(k).stringValue == "Android") { already = true; break; }
                }
                if (already) continue;

                excluded.InsertArrayElementAtIndex(excluded.arraySize);
                excluded.GetArrayElementAtIndex(excluded.arraySize - 1).stringValue = "Android";
                Debug.Log("[KvXr] Уровень «" + level.FindPropertyRelative("name").stringValue +
                    "» исключён из платформы Android (в нём HDRP).");
            }

            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Поставить Android первым графическим API Vulkan (нужно для HDRP).
        ///
        /// ПЕРВЫЙ ПОДХОД БЫЛ НЕВЕРНЫМ и это стоит помнить: правка через `SerializedObject`
        /// по имени записи «Android» не нашла её в `m_BuildTargetGraphicsAPIs` и молча
        /// ничего не сделала — то есть сборка упала ровно повторно. Штатное API
        /// `PlayerSettings.SetGraphicsAPIs` адресуется ГРУППОЙ платформ и потому надёжнее
        /// ручного разбора сериализованного файла.
        /// </summary>
        public static void SetAndroidGraphicsApi()
        {
            // ТОЛЬКО VULKAN. Сначала здесь стоял список «Vulkan, OpenGLES3» (Vulkan первым,
            // OpenGLES3 в запасе) — и сборка падала ПОВТОРНО, потому что валидатор HDRP
            // (`HDUtils.AreGraphicsAPIsSupported`) проверяет ВЕСЬ список: достаточно одного
            // неподдерживаемого API, чтобы платформа считалась неподходящей. OpenGLES3 для
            // HDRP не поддерживается ни в каком порядке. Замерено 02.10.2026.
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android,
                new[] { UnityEngine.Rendering.GraphicsDeviceType.Vulkan });
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            AssetDatabase.SaveAssets();

            var check = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            Debug.Log("[KvXr] Графический API для Android: " +
                string.Join(", ", System.Array.ConvertAll(check, a => a.ToString())) +
                " (Vulkan обязателен для HDRP — решение [R2]; OpenGLES3 убран намеренно).");
        }

        private static void AddXrSceneToBuildSilent()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (!scenes.Exists(s => s.path == XrScenePath))
            {
                scenes.Add(new EditorBuildSettingsScene(XrScenePath, true));
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }

        // ================================================================== диагностика

        /// <summary>Отчёт о состоянии XR-этапа: лоадеры, сцена, настройки, ограничения.</summary>
        [MenuItem(MenuRoot + "4. Отчёт о состоянии XR", false, 40)]
        public static void ReportXrState()
        {
            string details;
            bool openXr = VerifyOpenXrActive(out details);

            bool sceneExists = File.Exists(XrScenePath);
            var lines = new List<string>
            {
                "[KvXr] === СОСТОЯНИЕ XR-ЭТАПА ===",
                "Лоадеры XR: " + (openXr ? "OpenXR активен" : "ПРОБЛЕМА — не OpenXR") + " · " + details,
                "Сцена VR/MR: " + (sceneExists ? XrScenePath : "НЕ СОБРАНА"),
                "Шлем в системе: " + (KvXrInputSource.HeadsetPresent ? "обнаружен" : "не обнаружен (ожидаемо без устройства)"),
                "Настройки: " + KvXrSettings.Describe(),
                "Риг: " + KvXrSettings.BuildRigConfig().Describe(),
                "MR: " + KvXrMrPlacement.Describe(KvXrMrPlacement.DefaultTable, KvXrMrPlacement.DefaultRoom, 1.0f),
                "Честные ограничения: " +
                "виртуальная геометрия сцены не метрическая (VirtualGeometryIsMetric=" +
                KvXrMrPlacement.VirtualGeometryIsMetric + "), passthrough зависит от рантайма PICO."
            };
            Debug.Log(string.Join("\n", lines));
        }
    }
}
