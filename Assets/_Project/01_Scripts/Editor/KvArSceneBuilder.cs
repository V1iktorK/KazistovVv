using System;
using System.Collections.Generic;
using System.IO;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.XR.Management;
using KazistovVvAr;
using KazistovVvXr;

namespace KazistovVvArEditor
{
    /// <summary>
    /// СБОРЩИК AR-СЦЕН: `BootAr` (выбор режима) и `ArMain` (AR на базе XR-стека).
    ///
    /// ПОЧЕМУ СЦЕНЫ СОБИРАЮТСЯ ИНСТРУМЕНТОМ, А НЕ РУКАМИ. `XrMain.unity` — файл на 225 КБ с
    /// десятками ссылок между объектами; `ArMain` обязан быть её КОПИЕЙ (иначе теряются прежние
    /// связи: меню, окна, поток, регистрация команд). Копирование файла и последующая правка
    /// кодом — воспроизводимо: любую правку можно повторить одной командой, а не «вспоминать,
    /// какие галочки стояли». Запрет S3.5 («не менять XrMain») соблюдён: исходная сцена только
    /// читается.
    ///
    /// ЧТО ДЕЛАЕТ:
    ///   1. `ArMain.unity` = копия `XrMain.unity`; в копии выключаются VR-компоненты, гасится
    ///      ангар, выключается рендер логической головы, добавляется AR-риг (сессия, XR Origin,
    ///      AR-камера с CameraManager/Background и TrackedPoseDriver, три менеджера) и
    ///      компоненты AR-режима (`KvArRuntime` и его подсистемы).
    ///   2. `BootAr.unity` — маленькая сцена с `KvArBoot` и камерой для экрана сообщений.
    ///   3. `ArPlaneVisual.prefab` — визуализация найденных плоскостей (ARPlaneMeshVisualizer).
    ///   4. Порядок сцен в Build Settings: `BootAr` → `XrMain` → `MainScene` → `ArMain`.
    ///   5. Рендер-фича `ARCommandBufferSupportRendererFeature` в мобильном URP-рендерере:
    ///      на Android проект собирается под Vulkan, а AR-фон с Vulkan без этой фичи не рисуется
    ///      (документация AR Foundation 6.5, URP + Vulkan).
    ///
    /// ЗАПУСК: пункт меню «KazistovVv/AR/Собрать AR-сцены» или batchmode:
    ///   Unity.exe -batchmode -quit -projectPath &lt;проект&gt;
    ///       -executeMethod KazistovVvArEditor.KvArSceneBuilder.BuildAll
    /// </summary>
    public static class KvArSceneBuilder
    {
        private const string ScenesFolder = "Assets/_Project/00_Scenes";
        private const string ArFolder = "Assets/_Project/01_Scripts/Ar";
        private const string PlaneMaterialPath = ArFolder + "/ArPlaneVisual.mat";
        private const string PlanePrefabPath = ArFolder + "/ArPlaneVisual.prefab";

        [MenuItem("KazistovVv/AR/Собрать AR-сцены (BootAr + ArMain)", false, 20)]
        public static void BuildAll()
        {
            try
            {
                EnsureFolder(ArFolder);
                Material planeMaterial = EnsurePlaneMaterial();
                GameObject planePrefab = EnsurePlanePrefab(planeMaterial);
                BuildArMainScene(planePrefab);
                BuildBootScene();
                EnsureBuildSettings();
                EnsureRendererFeature();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[KvArBuilder] ГОТОВО: ArMain.unity и BootAr.unity собраны, " +
                          "сцены зарегистрированы, рендер-фича проверена");
            }
            catch (Exception error)
            {
                Debug.LogError("[KvArBuilder] СБОРКА AR-СЦЕН НЕ УДАЛАСЬ: " + error);
                throw;
            }
        }

        // ================================================================ ARMain

        private static void BuildArMainScene(GameObject planePrefab)
        {
            string arPath = KvArMode.ArScenePath;
            string vrPath = KvArMode.VrScenePath;
            if (!File.Exists(ToAbsolute(vrPath)))
                throw new FileNotFoundException("Нет VR-сцены для копирования: " + vrPath);

            // Копия исходной сцены. Существующую ArMain заменяем целиком: иначе от прошлой
            // сборки остались бы «хвосты» (объекты, которых уже нет в инструменте).
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(arPath) != null)
                AssetDatabase.DeleteAsset(arPath);

            Scene scene = EditorSceneManager.OpenScene(vrPath, OpenSceneMode.Single);
            SceneManager.SetActiveScene(scene);
            if (!EditorSceneManager.SaveScene(scene, arPath))
                throw new InvalidOperationException("Не удалось сохранить копию сцены в " + arPath);
            Debug.Log("[KvArBuilder] ArMain: сцена скопирована из " + vrPath);

            DisableVrOnlyComponentsInScene();
            DisableLogicalHeadRendering();
            AddArRig(planePrefab);
            AddArRuntimeObjects();
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("Не удалось сохранить ArMain.unity");
            Debug.Log("[KvArBuilder] ArMain: сохранена");
        }

        /// <summary>Выключить VR-компоненты прямо в копии сцены (рантайм страхует повторно).</summary>
        private static void DisableVrOnlyComponentsInScene()
        {
            int count = 0;
            count += DisableInScene<KvXrHeadTracking>();
            count += DisableInScene<KvXrLocomotion>();
            count += DisableInScene<KvXrMrRoom>();
            count += DisableInScene<KvXrHangarAnchor>();
            count += DisableInScene<KvXrSceneContent>();
            // VR-РИГ ЦЕЛИКОМ. Его `Start` создаёт в рантайме руки OpenXR (`KvXrHands`),
            // жесты, модели контроллеров, подачу взгляда и облёт простоя камеры. На планшете
            // `KvXrHands` каждые 2 секунды дёргает подсистему рук OpenXR, и это ломает
            // ARCore-сессию: она застревает в «инициализируется», камера не открывается,
            // плоскостей 0 (проверено на планшете 04.11.2026).
            // Меню, окна и мост потока AR-сцена поднимает сама (`KvArRuntime.EnsureArUiSupport`).
            count += DisableInScene<KvXrRig>();
            // 05.11.2026: `KvXrIdleOrbit` УДАЛЁН из проекта по решению владельца («убери вообще эту
            // функцию»), поэтому отдельного гашения больше не требует. Остальные VR-компоненты
            // гасятся выше: в AR камера должна стоять на трекинге устройства, а не «летать».
            Debug.Log("[KvArBuilder] ArMain: выключено VR-компонентов " + count +
                      " (включая KvXrRig: руки/жесты/подача взгляда не должны работать в AR)");
        }

        private static int DisableInScene<T>() where T : Behaviour
        {
            T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            int disabled = 0;
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || !all[i].enabled) continue;
                all[i].enabled = false;
                disabled++;
                Debug.Log("[KvArBuilder] ArMain: выключен " + typeof(T).Name + " на «" +
                          all[i].name + "»");
            }
            return disabled;
        }

        /// <summary>
        /// ЛОГИЧЕСКАЯ ГОЛОВА: камера, которую весь XR-интерфейс считает «головой». Её рендер
        /// выключается (рисует AR-камера), но объект остаётся: поза копируется мостом.
        /// </summary>
        private static void DisableLogicalHeadRendering()
        {
            Camera logical = KvArRigBridge.FindLogicalHead();
            if (logical == null)
            {
                // Резерв: компонент `KvXrRig` висит на камере `XrCamera`.
                KvXrRig rig = UnityEngine.Object.FindAnyObjectByType<KvXrRig>();
                if (rig != null)
                {
                    if (rig.headCamera != null) logical = rig.headCamera;
                    else logical = rig.GetComponent<Camera>();
                }
            }
            if (logical == null)
                throw new InvalidOperationException("В XrMain не найдена логическая голова (камера XrCamera)");

            logical.gameObject.tag = "Untagged";
            Camera camera = logical.GetComponent<Camera>();
            if (camera != null) camera.enabled = false;
            AudioListener listener = logical.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = false;
            Debug.Log("[KvArBuilder] ArMain: логическая голова «" + logical.name +
                      "» — рендер выключен, поза будет копироваться из AR-камеры");

            // Погасить виртуальное окружение (ангар) в сцене: в AR видно реальный мир.
            GameObject hangar = GameObject.Find("XrHangar");
            if (hangar != null)
            {
                hangar.SetActive(false);
                Debug.Log("[KvArBuilder] ArMain: ангар «XrHangar» погашен");
            }
        }

        /// <summary>ДОБАВИТЬ AR-РИГ: сессия, XR Origin, камера, менеджеры плоскостей/якорей/лучей.</summary>
        private static void AddArRig(GameObject planePrefab)
        {
            // 1. AR Session
            GameObject sessionGo = new GameObject("AR Session");
            ARSession session = sessionGo.AddComponent<ARSession>();
            session.attemptUpdate = false;

            // 2. XR Origin (его требует AR Foundation: камерой владеет трекинг)
            GameObject originGo = new GameObject("AR XR Origin");
            XROrigin origin = originGo.AddComponent<XROrigin>();

            GameObject offsetGo = new GameObject("Camera Offset");
            offsetGo.transform.SetParent(originGo.transform, false);

            GameObject cameraGo = new GameObject("ArCamera");
            cameraGo.transform.SetParent(offsetGo.transform, false);
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.black;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 20f;
            cameraGo.AddComponent<AudioListener>();
            ARCameraManager cameraManager = cameraGo.AddComponent<ARCameraManager>();
            cameraGo.AddComponent<ARCameraBackground>();
            AddTrackedPoseDriver(cameraGo);

            origin.CameraFloorOffsetObject = offsetGo;
            origin.Camera = camera;

            // 3. Менеджеры — на объекте XR Origin (стандартная раскладка AR Foundation).
            ARPlaneManager planeManager = originGo.AddComponent<ARPlaneManager>();
            planeManager.planePrefab = planePrefab;
            planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            originGo.AddComponent<ARAnchorManager>();
            originGo.AddComponent<ARRaycastManager>();

            Debug.Log("[KvArBuilder] ArMain: AR-риг добавлен (ARSession, XR Origin, ArCamera, " +
                      "ARPlaneManager, ARAnchorManager, ARRaycastManager)");
        }

        /// <summary>
        /// TRACKED POSE DRIVER — ровно так, как это делает сама AR Foundation
        /// (`XROriginCreateUtil.cs`): поза устройства приходит либо от шлема (`&lt;XRHMD&gt;`),
        /// либо от AR-устройства (`&lt;HandheldARInputDevice&gt;` — он есть в AR Foundation 6.5).
        /// Без этого компонента AR-камера стоит на месте, а картинка «плывёт».
        /// </summary>
        private static void AddTrackedPoseDriver(GameObject cameraGo)
        {
            TrackedPoseDriver driver = cameraGo.AddComponent<TrackedPoseDriver>();

            InputAction positionAction = new InputAction("Position",
                binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            InputAction rotationAction = new InputAction("Rotation",
                binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");

            driver.positionInput = new InputActionProperty(positionAction);
            driver.rotationInput = new InputActionProperty(rotationAction);
            driver.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
        }

        /// <summary>ДОБАВИТЬ КОМПОНЕНТЫ AR-РЕЖИМА и связать ссылки.</summary>
        private static void AddArRuntimeObjects()
        {
            ARSession session = UnityEngine.Object.FindAnyObjectByType<ARSession>();
            ARCameraManager cameraManager = UnityEngine.Object.FindAnyObjectByType<ARCameraManager>();
            ARPlaneManager planeManager = UnityEngine.Object.FindAnyObjectByType<ARPlaneManager>();
            ARAnchorManager anchorManager = UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>();
            ARRaycastManager raycastManager = UnityEngine.Object.FindAnyObjectByType<ARRaycastManager>();
            Camera arCamera = cameraManager != null ? cameraManager.GetComponent<Camera>() : Camera.main;
            Camera logicalHead = KvArRigBridge.FindLogicalHead();

            GameObject runtimeGo = new GameObject("ArRuntime");
            KvArRuntime runtime = runtimeGo.AddComponent<KvArRuntime>();
            KvArUi ui = runtimeGo.AddComponent<KvArUi>();
            KvArRigBridge bridge = runtimeGo.AddComponent<KvArRigBridge>();
            KvArSessionController sessionController = runtimeGo.AddComponent<KvArSessionController>();
            KvArContent content = runtimeGo.AddComponent<KvArContent>();
            KvArWorld world = runtimeGo.AddComponent<KvArWorld>();
            KvArPlacement placement = runtimeGo.AddComponent<KvArPlacement>();
            KvArTouchInput touch = runtimeGo.AddComponent<KvArTouchInput>();

            bridge.arCamera = arCamera;
            bridge.xrCamera = logicalHead;

            sessionController.session = session;
            sessionController.cameraManager = cameraManager;
            sessionController.ui = ui;

            world.anchorManager = anchorManager;
            world.content = content;

            placement.arCamera = arCamera;
            placement.raycastManager = raycastManager;
            placement.planeManager = planeManager;
            placement.world = world;
            placement.ui = ui;

            touch.arCamera = arCamera;
            touch.bridge = bridge;
            touch.placement = placement;
            touch.world = world;
            touch.ui = ui;

            runtime.session = session;
            runtime.cameraManager = cameraManager;
            runtime.planeManager = planeManager;
            runtime.anchorManager = anchorManager;
            runtime.raycastManager = raycastManager;
            runtime.arCamera = arCamera;
            runtime.logicalHead = logicalHead;
            runtime.rigBridge = bridge;
            runtime.sessionController = sessionController;
            runtime.world = world;
            runtime.content = content;
            runtime.placement = placement;
            runtime.touch = touch;
            runtime.ui = ui;

            // Указатель шлема: в AR он остаётся для подсветки, но «нажатие взглядом» отключаем —
            // нажимает палец (иначе случайное нажатие после 0,6 с взгляда).
            KvXrUiPointer pointer = UnityEngine.Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null)
            {
                pointer.gazeDwellSeconds = 9999f;
                pointer.useGazeWhenNoController = true;
                pointer.useEventSystemFallback = false;
                if (pointer.headCamera == null) pointer.headCamera = logicalHead;
                Debug.Log("[KvArBuilder] ArMain: указатель шлема переведён в режим наблюдения " +
                          "(нажатие — пальцем)");
            }

            // Кнопки AR-пульта (надписи и действия) — их создаёт KvArRuntime.BuildScreenButtons,
            // чтобы список кнопок жил рядом с логикой, а не в сцене.

            Debug.Log("[KvArBuilder] ArMain: компоненты AR-режима добавлены и связаны");
        }

        // ================================================================ BOOT

        private static void BuildBootScene()
        {
            string path = KvArMode.BootScenePath;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
                AssetDatabase.DeleteAsset(path);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SceneManager.SetActiveScene(scene);

            GameObject cameraGo = new GameObject("BootCamera");
            Camera camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Color;
            camera.backgroundColor = Color.black;
            camera.cullingMask = 0;
            cameraGo.AddComponent<AudioListener>();

            GameObject bootGo = new GameObject("ArBoot");
            bootGo.AddComponent<KvArBoot>();

            if (!EditorSceneManager.SaveScene(scene, path))
                throw new InvalidOperationException("Не удалось сохранить " + path);
            Debug.Log("[KvArBuilder] BootAr: сцена создана");
        }

        // ================================================================ ASSETS

        private static Material EnsurePlaneMaterial()
        {
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(PlaneMaterialPath);
            if (existing != null) return existing;

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            Material material = new Material(shader) { name = "ArPlaneVisual" };
            Color color = new Color(0.20f, 0.85f, 1f, 0.20f);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            // Прозрачность: найденная плоскость не должна закрывать реальный стол.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, PlaneMaterialPath);
            Debug.Log("[KvArBuilder] создан материал плоскости " + PlaneMaterialPath);
            return material;
        }

        private static GameObject EnsurePlanePrefab(Material material)
        {
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath);
            if (existing != null) return existing;

            GameObject go = new GameObject("ArPlaneVisual");
            go.AddComponent<ARPlane>();
            go.AddComponent<ARPlaneMeshVisualizer>();
            // Меш создаёт сам ARPlaneMeshVisualizer в рантайме: присваивать сюда временный
            // Mesh нельзя — префаб сохранился бы с битой ссылкой.
            go.AddComponent<MeshFilter>();
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, PlanePrefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log("[KvArBuilder] создан префаб плоскости " + PlanePrefabPath);
            return prefab;
        }

        // ================================================================ BUILD SETTINGS

        /// <summary>
        /// ПОРЯДОК СЦЕН: `BootAr` первой, потому что именно она решает, какую сцену грузить.
        /// `XrMain` и `MainScene` сохраняют прежние индексы+1 (VR/ПК не теряются).
        /// </summary>
        private static void EnsureBuildSettings()
        {
            string[] order =
            {
                KvArMode.BootScenePath,
                KvArMode.VrScenePath,
                "Assets/_Project/00_Scenes/MainScene.unity",
                KvArMode.ArScenePath
            };
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>();
            for (int i = 0; i < order.Length; i++)
            {
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(order[i]) == null)
                {
                    Debug.LogWarning("[KvArBuilder] сцена не найдена, пропущена: " + order[i]);
                    continue;
                }
                scenes.Add(new EditorBuildSettingsScene(order[i], true));
            }
            EditorBuildSettings.scenes = scenes.ToArray();
            Debug.Log("[KvArBuilder] Build Settings: сцен " + scenes.Count + " · первая — " +
                      KvArMode.BootScenePath);
        }

        // ================================================================ RENDERER FEATURE

        /// <summary>
        /// ФИЧА AR-ФОНА ДЛЯ VULKAN. Документация AR Foundation 6.5 (URP + Vulkan): без
        /// `ARCommandBufferSupportRendererFeature` камера-фон не выводится. Android в проекте
        /// в 03.11.2026 собиралась ТОЛЬКО под Vulkan, поэтому фича обязательна; 05.11.2026 Android
/// переведён на OpenGLES3, фича оставлена для совместимости с Vulkan-сборкой.
        /// Добавляется в мобильный рендерер; под OpenXR (шлем) она не делает ничего.
        /// </summary>
        private static void EnsureRendererFeature()
        {
            string[] rendererPaths =
            {
                "Assets/Settings/Mobile_Renderer.asset",
                "Assets/Settings/PC_Renderer.asset"
            };
            int added = 0;
            for (int i = 0; i < rendererPaths.Length; i++)
            {
                UniversalRendererData renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(
                    rendererPaths[i]);
                if (renderer == null) continue;

                bool present = false;
                List<ScriptableRendererFeature> features = renderer.rendererFeatures;
                for (int f = 0; f < features.Count; f++)
                {
                    if (features[f] is ARCommandBufferSupportRendererFeature) { present = true; break; }
                }
                if (present)
                {
                    Debug.Log("[KvArBuilder] рендерер " + rendererPaths[i] + ": фича AR-фона уже есть");
                    continue;
                }

                ARCommandBufferSupportRendererFeature feature =
                    ScriptableObject.CreateInstance<ARCommandBufferSupportRendererFeature>();
                feature.name = "AR Command Buffer Support";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(renderer);
                AssetDatabase.SaveAssets();
                added++;
                Debug.Log("[KvArBuilder] рендерер " + rendererPaths[i] +
                          ": добавлена фича AR-фона (Vulkan)");
            }
            if (added == 0)
                Debug.Log("[KvArBuilder] рендерер: фича AR-фона уже была — изменений нет");
            else
                AssetDatabase.Refresh();
        }

        // ================================================================ СЛУЖЕБНОЕ

        private static void EnsureFolder(string assetPath)
        {
            if (AssetDatabase.IsValidFolder(assetPath)) return;
            string parent = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            string name = Path.GetFileName(assetPath);
            if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static string ToAbsolute(string assetPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string relative = assetPath.StartsWith("Assets/", StringComparison.Ordinal)
                ? assetPath.Substring("Assets/".Length)
                : assetPath;
            return Path.Combine(Path.Combine(projectRoot, "Assets"), relative);
        }

        /// <summary>
        /// ПРОВЕРКА БЕЗ СБОРКИ (для тестов и отчёта): что должно быть в готовой AR-сцене.
        /// Возвращает список проблем (пусто — всё на месте).
        /// </summary>
        public static List<string> VerifyArSceneAssets()
        {
            List<string> problems = new List<string>();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(KvArMode.ArScenePath) == null)
                problems.Add("нет сцены " + KvArMode.ArScenePath);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(KvArMode.BootScenePath) == null)
                problems.Add("нет сцены " + KvArMode.BootScenePath);
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PlanePrefabPath) == null)
                problems.Add("нет префаба плоскости " + PlanePrefabPath);

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);
            if (scenes.Count == 0 || scenes[0].path != KvArMode.BootScenePath)
                problems.Add("первая сцена Build Settings — не BootAr");

            bool arMainInBuild = false;
            for (int i = 0; i < scenes.Count; i++)
                if (scenes[i].path == KvArMode.ArScenePath) arMainInBuild = true;
            if (!arMainInBuild) problems.Add("ArMain не в Build Settings");
            return problems;
        }
    }
}
