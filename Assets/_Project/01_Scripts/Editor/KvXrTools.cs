using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
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

            // --- панель быстрого выбора У РУКИ (решение владельца [D20]): кнопки у левой руки
            // (для левшей — правой), формой «бублик» или «полумесяц». Форма, рука, радиус и число
            // кнопок берутся из настроек XR (`KvXrSettings`), команды — из реестра `KvCommands`,
            // поэтому нажатие делает ровно то же, что пункт меню.
            var quickGo = new GameObject("XrQuickPanel");
            quickGo.transform.SetParent(rig.transform, false);
            var quick = quickGo.AddComponent<KvXrQuickPanel>();
            quick.headCamera = cam;

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
                      "KvXrUiPointer (луч и нажатие), KvXrPanelAnchor (плоская панель), " +
                      "KvXrQuickPanel (кнопки у руки, [D20]), KvXrMrRoom (реальный стол). " +
                      "ВНИМАНИЕ: сцена генерируемая — ручные правки в ней будут потеряны при пересборке.");
        }

        /// <summary>
        /// ПРОВЕРКА ПАНЕЛИ У РУКИ БЕЗ ШЛЕМА (пакетный прогон, сессия §32, решение владельца [D20]).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ МЕТОД. Панель строит объекты во время работы, а правила проекта
        /// запрещают GameObject в тестах. Поэтому её проверка — это вызов ТОГО ЖЕ кода построения
        /// в пакетном режиме: собираются ВСЕ ЧЕТЫРЕ сочетания (бублик/полумесяц × левая/правая рука)
        /// и печатается состояние каждого с числом построенных кнопок. Если построение сломано,
        /// это видно строкой в журнале, а не «в шлеме почему-то пусто».
        ///
        /// Ничего не сохраняет: сцена создаётся пустая и на диск не записывается.
        /// Пакетный прогон:
        /// `-batchmode -quit -projectPath … -executeMethod KazistovVvXrEditor.KvXrTools.VerifyQuickPanel`
        /// </summary>
        public static void VerifyQuickPanel()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var cameraGo = new GameObject("CheckCamera");
            Camera cam = cameraGo.AddComponent<Camera>();

            var panelGo = new GameObject("CheckQuickPanel");
            KvXrQuickPanel panel = panelGo.AddComponent<KvXrQuickPanel>();
            panel.headCamera = cam;

            // Реестр команд заполняет интерфейс при запуске (`KazistovVvUIManager.Start`), а в пакетном
            // режиме его нет — панель нашла бы НОЛЬ команд и проверила бы «ничего». Чтобы проверялось
            // именно ПОСТРОЕНИЕ, при пустом реестре заводим временные команды с теми же id и в конце
            // убираем их. В журнале это называется честно.
            bool stubbed = false;
            if (KazistovVvUI.KvCommands.All.Count == 0)
            {
                stubbed = true;
                for (int i = 0; i < panel.commandIds.Length; i++)
                {
                    string id = panel.commandIds[i];
                    if (string.IsNullOrEmpty(id)) continue;
                    KazistovVvUI.KvCommands.Register(new KazistovVvUI.KvCommand
                    {
                        Id = id,
                        Title = "Проверка " + (i + 1)
                    });
                }
                Debug.Log("[KvXrCheck] реестр команд пуст (пакетный режим) — для проверки заведены временные команды: " +
                          panel.commandIds.Length);
            }

            int combinations = 0;
            int totalButtons = 0;
            foreach (KvXrQuickShape shape in new[] { KvXrQuickShape.Ring, KvXrQuickShape.Crescent })
            {
                foreach (KvXrQuickHand hand in new[] { KvXrQuickHand.Left, KvXrQuickHand.Right })
                {
                    panel.BuildWith(shape, hand, KvXrSettings.GetQuickPanelButtonCount(),
                        KvXrSettings.GetQuickPanelRadius(), false);
                    Debug.Log("[KvXrCheck] " + panel.Status);
                    combinations++;
                    totalButtons += panel.ButtonCount;
                }
            }

            Debug.Log("[KvXrCheck] панель у руки: проверено сочетаний " + combinations +
                      " · всего построено кнопок " + totalButtons +
                      " · временные команды: " + (stubbed ? "да (убраны)" : "нет, реестр был заполнен"));

            if (stubbed) KazistovVvUI.KvCommands.Clear();
        }

        /// <summary>
        /// ДОБАВИТЬ В XR-СЦЕНУ ПАНЕЛЬ У РУКИ, НЕ ПЕРЕСОБИРАЯ СЦЕНУ (решение владельца [D20], §32).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД, А НЕ ПРАВКА ГЕНЕРАТОРА. `BuildXrScene` собирает сцену С НУЛЯ:
        /// стенды и роботов в неё переносит ОТДЕЛЬНЫЙ инструмент («Рендер/4. Начинка XR-сцены +
        /// URP-материалы»), а сам генератор их не кладёт. Пересборка ради одной панели снесла бы
        /// начинку — это и произошло 13.10.2026: сцена стала 115 348 → 41 179 байт, стенды и роботы
        /// пропали (сцена восстановлена из резерва
        /// `_backups_consolidated\XrMain_before_quickpanel_2026-10-13.unity`). Тест-«сторож»
        /// `KvRenderPipelineTests.XrScene_HasContentComponentAndRobotTemplates` ловит такую потерю,
        /// но лучше её не допускать.
        ///
        /// ЧТО ДЕЛАЕТ. Открывает XR-сцену, находит или создаёт узел `XrQuickPanel` под `XrRig`,
        /// вешает `KvXrQuickPanel`, привязывает камеру шлема и сохраняет сцену. Всё остальное
        /// в сцене не трогается.
        /// </summary>
        public static void AddQuickPanelToXrScene()
        {
            if (!System.IO.File.Exists(XrScenePath))
            {
                Debug.LogError("[KvXr] нет XR-сцены: " + XrScenePath);
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(XrScenePath, OpenSceneMode.Single);

            Camera cam = null;
            Transform rigRoot = null;
            KvXrQuickPanel panel = null;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (cam == null) cam = root.GetComponentInChildren<Camera>(true);
                if (panel == null) panel = root.GetComponentInChildren<KvXrQuickPanel>(true);
                if (rigRoot == null && root.name == "XrRig") rigRoot = root.transform;
            }

            if (panel == null)
            {
                var go = new GameObject("XrQuickPanel");
                if (rigRoot != null) go.transform.SetParent(rigRoot, false);
                panel = go.AddComponent<KvXrQuickPanel>();
                Debug.Log("[KvXr] панель у руки ДОБАВЛЕНА в сцену (узел XrQuickPanel)");
            }
            else
            {
                Debug.Log("[KvXr] панель у руки уже есть в сцене — обновляю привязку к камере");
            }

            if (cam != null) panel.headCamera = cam;

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, XrScenePath);
            AssetDatabase.Refresh();

            Debug.Log("[KvXr] XR-сцена сохранена: " + XrScenePath +
                      " · панель у руки " + (cam != null ? "привязана к камере " + cam.name : "БЕЗ КАМЕРЫ") +
                      " · " + KvXrSettings.DescribeQuickPanel());
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

            // НАСТРОИТЬ ПОДПИСЬ ПЕРЕД СБОРКОЙ (добавлено 31.10.2026, S44).
            //
            // ЧТО БЫЛО. Сборка падала с «No keystore passwords were found». При этом ключ в
            // проекте ЕСТЬ (`keystore/kazistovvv.keystore`), и пароль тоже известен —
            // `KvAndroidSigning.ResolvePassword()` берёт его из переменной окружения
            // `KAZISTOVVV_KEYPASS`, затем из `EditorPrefs`, затем из встроенного значения.
            //
            // ПРИЧИНА. `KvAndroidSigning.Configure()` настраивает `PlayerSettings.Android.*`,
            // но его ВЫЗЫВАЛ ТОЛЬКО ПУНКТ МЕНЮ. В пакетном режиме (`-batchmode -executeMethod`)
            // меню никто не нажимает — значит подпись оставалась НЕ настроенной, и Gradle
            // честно отказывался подписывать.
            //
            // ПОСЛЕДСТВИЕ ДЛЯ РАБОТЫ: собрать APK из командной строки было НЕВОЗМОЖНО вообще,
            // хотя скрипт `Tools/xr-build-apk-ascii.ps1` для этого и написан.
            //
            // ТЕПЕРЬ настройка вызывается здесь, до сборки. Если пароля нет — `Configure()`
            // печатает понятную ошибку, а не молчит.
            try
            {
                KazistovVvXrEditor.KvAndroidSigning.Configure();
            }
            catch (System.Exception error)
            {
                // Подпись — не повод падать здесь: сборка всё равно сообщит о проблеме понятнее,
                // если ключ действительно нужен. Но в журнале причина останется.
                Debug.LogWarning("[KvXr] настройка подписи не удалась: " + error.Message);
            }

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
            // ПАРКОВКА — В КОРНЕ ПРОЕКТА, А НЕ В `Temp`, И ЭТО ИСПРАВЛЕНИЕ ПОТЕРИ ДАННЫХ (08.10.2026).
            // Прежде тесты убирались в `Temp/_tests_parked_for_build`, а оказалось, что Unity при запуске
            // вычищает каталог `Temp` — и 30 файлов тестов были уничтожены именно так (проверено фактом:
            // после запуска редактора в `Temp` не осталось ни одного файла, а папка тестов в `Assets`
            // была пуста). `Temp` — служебный каталог Unity, хранить в нём единственную копию нельзя.
            string testsParked = Path.Combine(Directory.GetCurrentDirectory(), "_tests_parked_for_build");
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
            try
            {
                TestParkingReport parked = ParkTestsForBuild(testsPath, testsParked);
                testsMoved = !parked.NothingToDo;

                if (parked.Success)
                    Debug.Log("[KvXr] Тесты убраны из сборки APK: " + parked.Describe());
                else
                    Debug.LogWarning("[KvXr] Тесты НЕ убраны из сборки APK: " + parked.Describe());
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvXr] Не удалось убрать тесты из сборки: " + e.Message);
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
                        TestParkingReport restored = RestoreParkedTests(testsPath, testsParked);
                        if (restored.Success)
                            Debug.Log("[KvXr] " + restored.Describe());
                        else
                            Debug.LogError("[KvXr] " + restored.Describe() +
                                " Неразобранное осталось здесь: " + testsParked);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("[KvXr] НЕ УДАЛОСЬ вернуть тесты на место: " + e.Message +
                            ". Они лежат в _tests_parked_for_build (в корне проекта) — перенесите вручную.");
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

            // 2. Набор Android = только URP-уровни. Предпочтителен уровень с «Mobile» в имени.
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

            // ЧТЕНИЕ И ЗАПИСЬ — ОТДЕЛЬНЫМИ МЕТОДАМИ, и это исправление дефекта: прежний код искал
            // в словаре свойства `key`/`value`, не находил их и МОЛЧА ничего не делал (в журнале
            // не было ни строки). Найдено в Этапе А по отчёту пробы: «по умолчанию для ? — индекс -1».
            int currentDefault = ReadPlatformDefaultQuality(perPlatform, "Android");
            if (currentDefault == androidDefault)
            {
                Debug.Log("[KvXr] Android: уровень качества по умолчанию уже индекс " + androidDefault +
                    " (URP) — правка не нужна.");
            }
            else if (WritePlatformDefaultQuality(perPlatform, "Android", androidDefault))
            {
                Debug.Log("[KvXr] Android: уровень качества по умолчанию " + currentDefault + " → " +
                    androidDefault + " (URP). Уровни с HDRP для Android отключены.");
            }
            else
            {
                Debug.LogWarning("[KvXr] Android: НЕ удалось записать уровень качества по умолчанию " +
                    "(было " + currentDefault + ", нужно " + androidDefault + ") — проверьте вручную.");
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
        /// УРОВЕНЬ КАЧЕСТВА ПО УМОЛЧАНИЮ ДЛЯ ПЛАТФОРМЫ — чтение из файла проекта.
        ///
        /// ПОЧЕМУ ОТДЕЛЬНЫЙ МЕТОД И ПОЧЕМУ ЭТО ИСПРАВЛЕНИЕ ДЕФЕКТА. `m_PerPlatformDefaultQuality`
        /// в `QualitySettings.asset` — это СЛОВАРЬ («Android: 0», «Standalone: 1»), а не список пар
        /// `key`/`value`. Прежний код искал свойства с именами `key` и `value`, не находил их
        /// и МОЛЧА ничего не делал: часть работы «оставить Android без HDRP-уровней» не выполнялась
        /// вовсе, и в журнале об этом не было ни строки. Найдено в Этапе А по отчёту пробы, которая
        /// печатала «по умолчанию для ? — индекс -1» на всех шестнадцати платформах.
        /// Читаются ОБЕ формы записи — словарь и список пар: правка не зависит от версии Unity.
        /// </summary>
        public static int ReadPlatformDefaultQuality(SerializedProperty perPlatform, string platform)
        {
            if (perPlatform == null || string.IsNullOrEmpty(platform)) return -1;

            // Форма 1: словарь — дочернее свойство названо по ключу.
            SerializedProperty direct = perPlatform.FindPropertyRelative(platform);
            if (direct != null) return direct.intValue;

            // Форма 2: список пар (first — платформа, second — индекс).
            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                SerializedProperty first = entry.FindPropertyRelative("first");
                SerializedProperty second = entry.FindPropertyRelative("second");
                if (first == null || second == null) continue;
                if (string.Equals(first.stringValue, platform, StringComparison.Ordinal)) return second.intValue;
            }
            return -1;
        }

        /// <summary>
        /// ЗАПИСАТЬ УРОВЕНЬ КАЧЕСТВА ПО УМОЛЧАНИЮ ДЛЯ ПЛАТФОРМЫ. `false` — платформа не найдена
        /// ни в одной из форм записи; тогда вызывающий обязан сказать об этом вслух, а не промолчать
        /// (прежний молчаливый пропуск и был дефектом). Сохранение делает вызывающий — через
        /// `ApplyModifiedProperties`.
        /// </summary>
        public static bool WritePlatformDefaultQuality(SerializedProperty perPlatform, string platform, int index)
        {
            if (perPlatform == null || string.IsNullOrEmpty(platform)) return false;

            SerializedProperty direct = perPlatform.FindPropertyRelative(platform);
            if (direct != null) { direct.intValue = index; return true; }

            for (int i = 0; i < perPlatform.arraySize; i++)
            {
                SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                SerializedProperty first = entry.FindPropertyRelative("first");
                SerializedProperty second = entry.FindPropertyRelative("second");
                if (first == null || second == null) continue;
                if (!string.Equals(first.stringValue, platform, StringComparison.Ordinal)) continue;
                second.intValue = index;
                return true;
            }
            return false;
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

        // ================================================================== парковка тестов

        /// <summary>
        /// ИТОГ ПАРКОВКИ ИЛИ ВОЗВРАТА ТЕСТОВ — числами и словами, а не «вроде вернули».
        ///
        /// ЗАЧЕМ ОТЧЁТ, А НЕ ПРОСТО `void`. Строка <see cref="Describe"/> попадает в журнал сборки,
        /// и это единственное доказательство для владельца. «Тесты возвращены» без числа файлов
        /// неотличимо от «возврат не сработал» — ровно так дефект возврата и прожил с §28 по §30:
        /// в журнале не было ни одной строки о том, что возврат пропущен.
        /// </summary>
        public sealed class TestParkingReport
        {
            /// <summary>Работа выполнена полностью (или её и не требовалось).</summary>
            public bool Success;

            /// <summary>Работы не было: парковки нет либо парковать нечего.</summary>
            public bool NothingToDo;

            /// <summary>Папку в Assets пересоздала Unity — она была пуста, содержимое перенесено в неё.</summary>
            public bool TargetRecreated;

            /// <summary>Перенесено файлов (включая `.meta`).</summary>
            public int FilesMoved;

            /// <summary>Из перенесённых — файлов тестов (`*.cs`).</summary>
            public int TestFilesMoved;

            /// <summary>Файлы, которые уже были на месте и НЕ перезаписаны.</summary>
            public int Conflicts;

            /// <summary>Сколько файлов осталось в `Temp` (не разобрано).</summary>
            public int FilesLeftInTemp;

            /// <summary>Подробность для журнала: что именно произошло.</summary>
            public string Detail = string.Empty;

            /// <summary>Строка для журнала сборки: числа, а не намерение.</summary>
            public string Describe()
            {
                string what = TargetRecreated
                    ? "тесты возвращены в Assets (папку пересоздала Unity, пустую сняли)"
                    : (NothingToDo ? "тесты: работы не было" : "тесты обработаны");
                return string.Format(
                    "{0}: перенесено файлов {1} (тестов .cs {2}), конфликтов {3}, осталось в Temp {4}{5}",
                    what, FilesMoved, TestFilesMoved, Conflicts, FilesLeftInTemp,
                    string.IsNullOrEmpty(Detail) ? string.Empty : " · " + Detail);
            }
        }

        /// <summary>Сколько файлов лежит в папке (рекурсивно); 0, если папки нет.</summary>
        private static int CountFilesIn(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, "*", SearchOption.AllDirectories).Length;
        }

        /// <summary>Сколько в папке именно файлов тестов (`*.cs`) — без `.meta`.</summary>
        private static int CountTestFilesIn(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return 0;
            return Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories).Length;
        }

        // ---- ПРАВКА ЭТАПА А (A2): НАЧАЛО БЛОКА ДЛЯ ЗАМЕНЫ (зелёная версия) ----

        /// <summary>
        /// УБРАТЬ ТЕСТЫ ИЗ СБОРКИ — перенос папки тестов в `Temp` на время сборки APK.
        ///
        /// ЗДЕСЬ ЛЕЧИТСЯ ВТОРОЙ ДЕФЕКТ ТОГО ЖЕ МЕСТА, найденный при написании тестов: прежний код
        /// начинался с `Directory.Delete(testsParked, true)`. Если прошлая сборка была убита
        /// (таймаут, снятие процесса) и тесты остались в `Temp`, следующая сборка УНИЧТОЖАЛА их
        /// навсегда и парковала на их место пустую папку. Теперь сначала идёт САМОЛЕЧЕНИЕ —
        /// возврат остатка на место, — и только потом парковка. Если вернуть не удалось,
        /// парковка ОТМЕНЯЕТСЯ: потерять файлы тестов хуже, чем не собрать APK.
        /// </summary>
        public static TestParkingReport ParkTestsForBuild(string testsPath, string testsParked)
        {
            var report = new TestParkingReport();

            // ШАГ 1. САМОЛЕЧЕНИЕ: остаток прошлой (убитой) парковки возвращаем на место.
            if (Directory.Exists(testsParked))
            {
                TestParkingReport healing = RestoreParkedTests(testsPath, testsParked);
                report.Detail = "самолечение: " + healing.Detail;

                if (Directory.Exists(testsParked) && CountFilesIn(testsParked) > 0)
                {
                    report.Success = false;
                    report.FilesLeftInTemp = CountFilesIn(testsParked);
                    report.Detail += " · ПАРКОВКА ОТМЕНЕНА: в Temp остались файлы прошлой парковки, " +
                                     "перенесите их в Assets вручную";
                    return report;
                }
            }

            if (!Directory.Exists(testsPath))
            {
                report.Success = true;
                report.NothingToDo = true;
                report.Detail += " · папки тестов в Assets нет — парковать нечего";
                return report;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(testsParked));
            if (Directory.Exists(testsParked)) Directory.Delete(testsParked, true);
            Directory.Move(testsPath, testsParked);

            report.Success = true;
            report.FilesMoved = CountFilesIn(testsParked);
            report.TestFilesMoved = CountTestFilesIn(testsParked);
            report.Detail += string.Format(" · убрано из Assets {0} файлов в {1}", report.FilesMoved, testsParked);
            return report;
        }

        /// <summary>
        /// ВЕРНУТЬ ТЕСТЫ ИЗ `Temp` В `Assets` — вызывается из `finally`, то есть при ЛЮБОМ исходе сборки.
        ///
        /// ДЕФЕКТ A2 И ЕГО ЛЕЧЕНИЕ. Прежний возврат был условием «перенести, если папки в `Assets`
        /// НЕТ». Unity пересоздаёт папку (её `.meta` остаётся), поэтому на живой сборке §30 условие
        /// не выполнилось, и 23 файла тестов остались бы в `Temp` навсегда — молча, без строки
        /// в журнале. Теперь разбираются ЧЕТЫРЕ случая явно:
        ///   1) парковки нет — работы не было (не ошибка);
        ///   2) папки в `Assets` нет — переносится папка целиком (быстрый путь);
        ///   3) папка есть и ПУСТА (её пересоздала Unity) — пустую снимаем, содержимое переносим:
        ///      ЭТО И ЕСТЬ ДЕФЕКТ, из-за которого тесты не возвращались;
        ///   4) папка есть и НЕ пуста — переносим ПОФАЙЛОВО и ничего не затираем; совпавшие имена
        ///      считаются конфликтами и ОСТАЮТСЯ в `Temp` (потерять файл теста хуже, чем доложить
        ///      о конфликте), поэтому такой исход честно помечается как неполный.
        /// </summary>
        public static TestParkingReport RestoreParkedTests(string testsPath, string testsParked)
        {
            var report = new TestParkingReport();

            // 1. Возвращать нечего.
            if (!Directory.Exists(testsParked))
            {
                report.Success = true;
                report.NothingToDo = true;
                report.Detail = "возвращать нечего: в Temp нет запаркованных тестов";
                return report;
            }

            // 2. Цели нет — переносим папку целиком.
            if (!Directory.Exists(testsPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(testsPath));
                Directory.Move(testsParked, testsPath);
                report.Success = true;
                report.FilesMoved = CountFilesIn(testsPath);
                report.TestFilesMoved = CountTestFilesIn(testsPath);
                report.Detail = string.Format("перенесена папка целиком: файлов {0} (тестов {1})",
                    report.FilesMoved, report.TestFilesMoved);
                return report;
            }

            // 3. ЦЕЛЬ ПЕРЕСОЗДАНА UNITY И ПУСТА — это и есть дефект A2. Пустая папка не данные.
            if (CountFilesIn(testsPath) == 0)
            {
                Directory.Delete(testsPath, true);
                Directory.Move(testsParked, testsPath);
                report.TargetRecreated = true;
                report.Success = true;
                report.FilesMoved = CountFilesIn(testsPath);
                report.TestFilesMoved = CountTestFilesIn(testsPath);
                report.Detail = string.Format("снята пустая папка, пересозданная Unity; перенесено файлов {0} (тестов {1})",
                    report.FilesMoved, report.TestFilesMoved);
                return report;
            }

            // 4. ЦЕЛЬ НЕПУСТА — переносим пофайлово, ничего не затирая.
            string[] files = Directory.GetFiles(testsParked, "*", SearchOption.AllDirectories);
            foreach (string file in files)
            {
                string relative = file.Substring(testsParked.Length).TrimStart(Path.DirectorySeparatorChar);
                string destination = Path.Combine(testsPath, relative);

                string folder = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder)) Directory.CreateDirectory(folder);

                if (File.Exists(destination)) { report.Conflicts++; continue; }

                File.Move(file, destination);
                report.FilesMoved++;
                if (file.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) report.TestFilesMoved++;
            }

            // Опустевшую парковку убираем целиком — но ТОЛЬКО если файлов в ней не осталось.
            // Рекурсивное удаление здесь безопасно именно поэтому: удалять нечего, кроме пустых папок.
            if (CountFilesIn(testsParked) == 0) Directory.Delete(testsParked, true);

            report.FilesLeftInTemp = CountFilesIn(testsParked);
            report.Success = report.FilesLeftInTemp == 0;
            report.Detail = report.Success
                ? "папка в Assets была не пуста: перенесено пофайлово, затираний нет"
                : string.Format("ВНИМАНИЕ: конфликтов {0}, не разобрано файлов {1} — они оставлены в {2}",
                    report.Conflicts, report.FilesLeftInTemp, testsParked);
            return report;
        }

        // ---- ПРАВКА ЭТАПА А (A2): КОНЕЦ БЛОКА ДЛЯ ЗАМЕНЫ (зелёная версия) ----

        /// <summary>
        /// ВЕРНУТЬ АКТИВНУЮ ПЛАТФОРМУ НА КОМПЬЮТЕР (StandaloneWindows64).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ ПУНКТ. Сборка APK переключает активную платформу на Android и возвращает
        /// её обратно в `finally` — но только если переключала ИМЕННО ОНА (`switched`). Если проект
        /// остался на Android после ПРЕРВАННОЙ сборки, эта сборка видит Android как исходный и
        /// возвращать ничего не станет — так и вышло 08.10.2026: три прерванные сборки оставили
        /// платформу на Android, и следующая (успешная) сборка APK её не вернула.
        /// Возврат нужен явным действием: на Android редактор иначе собирает рабочий стол
        /// не тем конвейером, а проба A4 (пункт «активная платформа») считает это расхождением.
        /// </summary>
        [MenuItem(MenuRoot + "9. Вернуть платформу на компьютер (StandaloneWindows64)", false, 50)]
        public static void RestoreStandalonePlatform()
        {
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.StandaloneWindows64)
            {
                Debug.Log("[KvXr] Активная платформа уже StandaloneWindows64 — правка не нужна.");
                return;
            }

            bool ok = EditorUserBuildSettings.SwitchActiveBuildTarget(
                BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);

            // ПЕРЕЧИТЫВАНИЕ: подтверждаем фактом, а не намерением.
            Debug.Log("[KvXr] Возврат платформы на StandaloneWindows64: " +
                      (ok ? "переключение выполнено" : "переключение НЕ выполнилось") +
                      " · активная платформа сейчас: " + EditorUserBuildSettings.activeBuildTarget);
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

        /// <summary>
        /// ОТЧЁТ «ЧТО СБОРКА ОБЯЗАНА БЫЛА ВЕРНУТЬ» (Этап А, пункт A4).
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ ОТЧЁТ. Сборка APK намеренно меняет проект: переключает активную
        /// платформу на Android, правит графический API Android, убирает тесты из `Assets`.
        /// «Сборка прошла» и «проект остался таким, как был» — ДВА разных утверждения, и второе
        /// проверяется фактом. Активная платформа хранится в `Library`, а не в текстовом файле
        /// проекта, поэтому честный ответ даёт только сам редактор — этим методом.
        /// </summary>
        [MenuItem(MenuRoot + "8. Отчёт: состояние после сборки (Этап А)", false, 45)]
        public static void ReportBuildReturnState()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvA] === СОСТОЯНИЕ ПОСЛЕ СБОРКИ (Этап А, A4) ===");

            sb.AppendLine("[KvA] активная платформа: " + EditorUserBuildSettings.activeBuildTarget +
                          " (ожидание Этапа А: StandaloneWindows64)");

            RenderPipelineAsset defaultPipeline = GraphicsSettings.defaultRenderPipeline;
            sb.AppendLine("[KvA] умолчание графики: " +
                (defaultPipeline != null
                    ? defaultPipeline.name + " · " + ShortPipelineName(defaultPipeline)
                    : "НЕ НАЗНАЧЕНО") +
                " (ожидание: Mobile_RPAsset — URP, решение [D9])");

            var apis = PlayerSettings.GetGraphicsAPIs(BuildTarget.Android);
            sb.AppendLine("[KvA] графический API Android: " +
                (apis != null ? string.Join(", ", System.Array.ConvertAll(apis, a => a.ToString())) : "нет") +
                " (ожидание: Vulkan — решение [R2])");

            sb.AppendLine("[KvA] уровни качества: " + DescribeQualityLevels());

            string xrDetails;
            sb.AppendLine("[KvA] лоадеры XR: " +
                (VerifyOpenXrActive(out xrDetails) ? "OpenXR активен · " : "ПРОБЛЕМА · ") + xrDetails);

            var scenes = new List<string>();
            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
                scenes.Add((scene.enabled ? "+" : "−") + scene.path);
            sb.AppendLine("[KvA] сцены сборки (" + scenes.Count + "): " + string.Join(" | ", scenes));

            sb.AppendLine("[KvA] тесты: " + DescribeTestsLocation());

            Debug.Log(sb.ToString());
        }

        /// <summary>Короткое имя конвейера для отчёта: URP / HDRP / встроенный.</summary>
        private static string ShortPipelineName(RenderPipelineAsset asset)
        {
            if (asset == null) return "нет";
            string type = asset.GetType().Name;
            if (type.IndexOf("Universal", StringComparison.OrdinalIgnoreCase) >= 0) return "URP";
            if (type.IndexOf("HD", StringComparison.OrdinalIgnoreCase) >= 0) return "HDRP";
            return type;
        }

        /// <summary>
        /// УРОВНИ КАЧЕСТВА И ПЛАТФОРМЫ: имя, конвейер, для каких платформ исключён, какой уровень
        /// по умолчанию у какой платформы. Читается из файла проекта (`ProjectSettings/QualitySettings.asset`),
        /// а не из памяти редактора: §29 показал, что «в памяти» и «на диске» — разные ответы.
        /// </summary>
        public static string DescribeQualityLevels()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset");
            if (assets == null || assets.Length == 0) return "QualitySettings.asset не прочитан";

            var so = new SerializedObject(assets[0]);
            SerializedProperty levels = so.FindProperty("m_QualitySettings");
            SerializedProperty perPlatform = so.FindProperty("m_PerPlatformDefaultQuality");
            if (levels == null) return "в QualitySettings.asset нет списка уровней";

            var lines = new List<string>();
            for (int i = 0; i < levels.arraySize; i++)
            {
                SerializedProperty level = levels.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = level.FindPropertyRelative("name");
                SerializedProperty pipelineProp = level.FindPropertyRelative("customRenderPipeline");
                SerializedProperty excluded = level.FindPropertyRelative("excludedTargetPlatforms");

                var pipeline = pipelineProp != null ? pipelineProp.objectReferenceValue as RenderPipelineAsset : null;
                var excludedNames = new List<string>();
                if (excluded != null)
                    for (int k = 0; k < excluded.arraySize; k++)
                        excludedNames.Add(excluded.GetArrayElementAtIndex(k).stringValue);

                lines.Add(string.Format("{0}[{1}] {2}{3}",
                    nameProp != null ? nameProp.stringValue : ("уровень " + i),
                    i,
                    pipeline != null ? pipeline.name + " · " + ShortPipelineName(pipeline) : "встроенный конвейер",
                    excludedNames.Count > 0 ? ", исключён для " + string.Join("/", excludedNames.ToArray()) : string.Empty));
            }

            if (perPlatform != null)
            {
                // Ключевые платформы — отдельной строкой (это ровно то, что проверяет пункт A4)…
                lines.Add("умолчание: Android — индекс " + ReadPlatformDefaultQuality(perPlatform, "Android") +
                          ", Standalone — индекс " + ReadPlatformDefaultQuality(perPlatform, "Standalone"));

                // …и весь словарь целиком. Имя платформы берётся из пути свойства, поэтому отчёт
                // читается и для словаря («Android: 0»), и для списка пар — см. ReadPlatformDefaultQuality.
                var defaults = new List<string>();
                for (int i = 0; i < perPlatform.arraySize; i++)
                {
                    SerializedProperty entry = perPlatform.GetArrayElementAtIndex(i);
                    if (entry.propertyType != SerializedPropertyType.Integer) continue;
                    string path = entry.propertyPath;
                    defaults.Add(path.Substring(path.LastIndexOf('.') + 1) + "=" + entry.intValue);
                }
                lines.Add("все умолчания по платформам: " + string.Join(", ", defaults.ToArray()));
            }
            return string.Join(" · ", lines.ToArray());
        }

        /// <summary>
        /// ГДЕ ЛЕЖАТ ТЕСТЫ — прямой ответ на вопрос пункта A3. Отдельно называется случай
        /// «папка есть, а файлов нет»: это и есть след дефекта A2 (Unity пересоздала пустую папку).
        /// </summary>
        public static string DescribeTestsLocation()
        {
            string root = Directory.GetCurrentDirectory();
            string testsPath = Path.Combine(root, "Assets", "_Project", "08_Tests");
            string testsParked = Path.Combine(root, "_tests_parked_for_build");

            int inAssets = CountTestFilesIn(testsPath);
            int parked = CountTestFilesIn(testsParked);
            bool folderExists = Directory.Exists(testsPath);

            return string.Format(
                "в Assets {0} файлов тестов ({1}) · в Temp {2} · папка в Assets {3}{4}",
                inAssets,
                folderExists ? "папка есть" : "папки нет",
                parked,
                folderExists ? "есть" : "отсутствует",
                folderExists && inAssets == 0 ? " — ПАПКА ПУСТА, её пересоздала Unity (дефект A2)" : string.Empty);
        }

    }
}
