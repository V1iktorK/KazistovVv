using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using KazistovVvAr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>
    /// ТЕСТЫ AR: РЕЖИМ ЗАПУСКА, СБОРОЧНАЯ КОНФИГУРАЦИЯ И СОСТАВ СЦЕН.
    ///
    /// ЗАЧЕМ. В задаче AR самое дорогое — «на устройстве не запустилось, а в редакторе всё
    /// зелёное». Поэтому проверяется НЕ «код написан», а ФАЙЛЫ ПРОЕКТА, от которых запуск
    /// зависит: порядок XR-лоадеров (ARCore первый, OpenXR второй), пакет провайдера ARCore,
    /// манифест, состав `ArMain.unity`, порядок сцен в Build Settings, рендер-фича для Vulkan.
    ///
    /// СТИЛЬ — КАК В ПРОЕКТЕ: чтение настоящих файлов и поиск GUID скриптов. Сцену при этом
    /// запускать не нужно (правило: невызываемый код и непроверенные ассеты — дефект).
    /// </summary>
    public class KvArModeTests
    {
        private static string ProjectRoot
        {
            get { return Path.GetFullPath(Path.Combine(Application.dataPath, "..")); }
        }

        private static string ReadAsset(params string[] parts)
        {
            string path = ProjectRoot;
            for (int i = 0; i < parts.Length; i++) path = Path.Combine(path, parts[i]);
            Assert.IsTrue(File.Exists(path), "нет файла " + path);
            return File.ReadAllText(path);
        }

        /// <summary>GUID скрипта по пути файла .cs (рядом лежит .cs.meta).</summary>
        private static string ScriptGuid(string assetPath)
        {
            string full = Path.Combine(ProjectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
            string meta = full + ".meta";
            Assert.IsTrue(File.Exists(meta), "нет .meta у скрипта " + assetPath);
            Match match = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, "в .meta нет guid: " + meta);
            return match.Groups[1].Value;
        }

        /// <summary>GUID скрипта из пакета в Library/PackageCache (имя файла и суффикс пути).</summary>
        private static string PackageScriptGuid(string packagePrefix, string relativePath)
        {
            string cache = Path.Combine(ProjectRoot, "Library", "PackageCache");
            Assert.IsTrue(Directory.Exists(cache), "нет Library/PackageCache — пакеты не разрешены");
            foreach (string dir in Directory.GetDirectories(cache, packagePrefix + "*"))
            {
                string full = Path.Combine(dir, relativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(full)) continue;
                string meta = full + ".meta";
                Assert.IsTrue(File.Exists(meta), "нет .meta у " + full);
                Match match = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline);
                if (match.Success) return match.Groups[1].Value;
            }
            Assert.Fail("не найден скрипт " + relativePath + " в пакете " + packagePrefix);
            return null;
        }

        private static string LoaderNameByGuid(string guid)
        {
            string dir = Path.Combine(ProjectRoot, "Assets", "XR", "Loaders");
            if (!Directory.Exists(dir)) return null;
            foreach (string asset in Directory.GetFiles(dir, "*.asset"))
            {
                string meta = asset + ".meta";
                if (!File.Exists(meta)) continue;
                Match match = Regex.Match(File.ReadAllText(meta), @"^guid: (\w+)", RegexOptions.Multiline);
                if (match.Success && match.Groups[1].Value == guid)
                    return Path.GetFileNameWithoutExtension(asset);
            }
            return null;
        }

        /// <summary>Список лоадеров ТОЛЬКО блока Android (не Standalone: у него может быть свой набор).</summary>
        private static List<string> AndroidLoaderNames()
        {
            string text = ReadAsset("Assets", "XR", "XRGeneralSettingsPerBuildTarget.asset");
            int android = text.IndexOf("m_Name: Android Providers", System.StringComparison.Ordinal);
            Assert.IsTrue(android >= 0, "в XR-настройках нет блока Android Providers");
            int next = text.IndexOf("--- !u!", android, System.StringComparison.Ordinal);
            string block = next > android ? text.Substring(android, next - android) : text.Substring(android);
            var names = new List<string>();
            foreach (Match loader in Regex.Matches(block, @"- \{fileID: 11400000, guid: (\w+), type: 2\}"))
                names.Add(LoaderNameByGuid(loader.Groups[1].Value) ?? ("неизвестный " + loader.Groups[1].Value));
            return names;
        }

        // ================================================================ 1. РЕЖИМ (чистые функции)

        [Test]
        public void Classify_ArCoreLoader_IsAr()
        {
            Assert.AreEqual(KvArBootKind.Ar, KvArMode.Classify("ARCoreLoader"));
            Assert.AreEqual(KvArBootKind.Ar, KvArMode.Classify("UnityEngine.XR.ARCore.ARCoreLoader"));
        }

        [Test]
        public void Classify_OpenXrLoader_IsVr()
        {
            Assert.AreEqual(KvArBootKind.Vr, KvArMode.Classify("OpenXRLoader"));
            Assert.AreEqual(KvArBootKind.Vr, KvArMode.Classify("UnityEngine.XR.OpenXR.OpenXRLoader"));
        }

        [Test]
        public void Classify_EmptyOrUnknown_IsUnknown()
        {
            Assert.AreEqual(KvArBootKind.Unknown, KvArMode.Classify(null));
            Assert.AreEqual(KvArBootKind.Unknown, KvArMode.Classify(string.Empty));
            Assert.AreEqual(KvArBootKind.Unknown, KvArMode.Classify("MockHMDLoader"));
            Assert.AreEqual(KvArBootKind.Unknown, KvArMode.Classify("XRSimulation"));
        }

        [Test]
        public void Classify_ArCoreWinsOverOpenXr_WhenBothWordsPresent()
        {
            // На телефоне OpenXR не поднимается, поэтому при составном имени должен победить AR.
            Assert.AreEqual(KvArBootKind.Ar, KvArMode.Classify("ARCoreLoader (OpenXR fallback)"));
        }

        [Test]
        public void SceneFor_MapsKinds()
        {
            Assert.AreEqual(KvArMode.ArSceneName, KvArMode.SceneFor(KvArBootKind.Ar));
            Assert.AreEqual(KvArMode.VrSceneName, KvArMode.SceneFor(KvArBootKind.Vr));
            Assert.AreEqual(string.Empty, KvArMode.SceneFor(KvArBootKind.Unknown));
        }

        [Test]
        public void DecideScene_ReturnsSceneAndKind()
        {
            KvArBootKind kind;
            Assert.AreEqual("ArMain", KvArBoot.DecideScene("ARCoreLoader", out kind));
            Assert.AreEqual(KvArBootKind.Ar, kind);
            Assert.AreEqual("XrMain", KvArBoot.DecideScene("OpenXRLoader", out kind));
            Assert.AreEqual(KvArBootKind.Vr, kind);
            Assert.AreEqual(string.Empty, KvArBoot.DecideScene(string.Empty, out kind));
            Assert.AreEqual(KvArBootKind.Unknown, kind);
        }

        [Test]
        public void ModeLine_ArContainsExactAcceptanceMarker()
        {
            string line = KvArMode.ModeLine(KvArBootKind.Ar, "ARCoreLoader");
            StringAssert.Contains("РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ", line,
                "строка журнала AR обязана содержать дословный маркер приёмки A3");
            Assert.AreEqual("РЕЖИМ: ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ", KvArMode.ArModeLineMarker);
        }

        [Test]
        public void ModeLine_VrIsHonestAndDoesNotClaimAr()
        {
            string line = KvArMode.ModeLine(KvArBootKind.Vr, "OpenXRLoader");
            StringAssert.Contains("ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ", line);
            StringAssert.DoesNotContain("ДОПОЛНЕННАЯ", line);
        }

        [Test]
        public void UnsupportedMessage_IsHumanAndMentionsArCore()
        {
            string message = KvArMode.UnsupportedMessage();
            StringAssert.Contains("ARCore", message);
            StringAssert.Contains("не упало", message);
            Assert.Greater(message.Length, 80, "сообщение Ж1 обязано быть понятным, а не одной строкой кода");
        }

        // ================================================================ 2. XR-ЛОАДЕРЫ

        /// <summary>
        /// ПОРЯДОК ЛОАДЕРОВ ANDROID: СНАЧАЛА ARCORE, ПОТОМ OPENXR (перевёрнут 04.11.2026).
        ///
        /// ПОЧЕМУ. На планшете первым шёл OpenXRLoader, и его PICO-реализация на не-PICO
        /// устройстве зависала: XR-инициализация не завершалась за 30 с, ARCore до второго
        /// места не доходил, приложение показывало «нет AR». С ARCore первым AR поднимается
        /// сразу. На шлеме ARCore-лоадер падает быстро (ARCore-сервиса нет), и OpenXR вторым
        /// поднимает VR/MR как раньше — проверено на PICO.
        /// </summary>
        [Test]
        public void AndroidLoaders_AreArCoreThenOpenXr()
        {
            List<string> names = AndroidLoaderNames();
            Assert.AreEqual(2, names.Count,
                "лоадеров Android должно быть ровно два (ARCore + OpenXR), сейчас: " +
                string.Join(", ", names.ToArray()));
            StringAssert.Contains("ARCore", names[0],
                "ПЕРВЫЙ лоадер обязан быть ARCore: с OpenXR первым планшет зависает в XR-инициализации");
            StringAssert.Contains("OpenXR", names[1],
                "ВТОРЫМ обязан быть OpenXR: без него не будет VR/MR на шлеме");
        }

        [Test]
        public void ArCoreProviderPackage_IsDeclared()
        {
            string manifest = ReadAsset("Packages", "manifest.json");
            Match match = Regex.Match(manifest, @"""com\.unity\.xr\.arcore""\s*:\s*""([^""]+)""");
            Assert.IsTrue(match.Success,
                "в Packages/manifest.json нет com.unity.xr.arcore — ARCoreLoader.asset остаётся без провайдера, " +
                "и AR на Android не запустится вообще (проверено разведкой 02.11.2026)");
            Assert.AreEqual("6.5.0", match.Groups[1].Value,
                "версия провайдера должна совпадать с AR Foundation 6.5.0");
        }

        [Test]
        public void ArCoreSettings_AreOptional_SoPicoStillInstalls()
        {
            string settings = ReadAsset("Assets", "XR", "Settings", "ARCoreSettings.asset");
            Match requirement = Regex.Match(settings, @"m_Requirement:\s*(\d+)");
            Assert.IsTrue(requirement.Success, "в ARCoreSettings.asset нет m_Requirement");
            // ЗНАЧЕНИЯ ENUM ПРОВЕРЕНЫ ПО ИСХОДНИКАМ ПАКЕТА (ARCoreSettings.cs):
            //   Requirement.Required = 0 (первый элемент enum), Requirement.Optional = 1.
            // Поэтому «Optional» — это 1. Первая версия этого теста считала наоборот и пропустила
            // дефект: собранный манифест получил com.google.ar.core=required и
            // android.hardware.camera.ar required=true — такой APK магазин отдал бы только
            // владельцам ARCore-устройств, а нам нужен один APK и на шлем.
            Assert.AreEqual("1", requirement.Groups[1].Value,
                "ARCore Requirement обязан быть Optional (1): иначе общий APK отфильтрован для шлема");

            Match depth = Regex.Match(settings, @"m_Depth:\s*(\d+)");
            Assert.IsTrue(depth.Success, "в ARCoreSettings.asset нет m_Depth");
            Assert.AreEqual("1", depth.Groups[1].Value,
                "Depth обязан быть Optional (1): иначе APK отфильтрован для телефонов без датчика глубины");
        }

        [Test]
        public void ArCoreLoaderAsset_StillPresent()
        {
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, "Assets", "XR", "Loaders", "ARCoreLoader.asset")),
                "пропал Assets/XR/Loaders/ARCoreLoader.asset — настройки XR ссылаются на него");
        }

        // ================================================================ 3. СЦЕНЫ И BUILD SETTINGS

        [Test]
        public void ArScene_Exists()
        {
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot, KvArMode.ArScenePath)),
                "нет AR-сцены " + KvArMode.ArScenePath + " — AR-режиму нечего грузить");
        }

        [Test]
        public void BootScene_ExistsAndHasBootScript()
        {
            string path = Path.Combine(ProjectRoot, KvArMode.BootScenePath);
            Assert.IsTrue(File.Exists(path), "нет загрузочной сцены " + KvArMode.BootScenePath);
            string scene = File.ReadAllText(path);
            string guid = ScriptGuid("Assets/_Project/01_Scripts/Ar/KvArBoot.cs");
            StringAssert.Contains("guid: " + guid, scene,
                "в BootAr.unity нет компонента KvArBoot — режим не будет определён");
        }

        [Test]
        public void ArScene_HasArFoundationComponents()
        {
            string scene = File.ReadAllText(Path.Combine(ProjectRoot, KvArMode.ArScenePath));
            var required = new Dictionary<string, string>
            {
                { "ARSession", PackageScriptGuid("com.unity.xr.arfoundation", "Runtime/ARFoundation/Session/ARSession.cs") },
                { "ARCameraManager", PackageScriptGuid("com.unity.xr.arfoundation", "Runtime/ARFoundation/Camera/ARCameraManager.cs") },
                { "ARCameraBackground", PackageScriptGuid("com.unity.xr.arfoundation", "Runtime/ARFoundation/Camera/ARCameraBackground.cs") },
                { "ARPlaneManager", PackageScriptGuid("com.unity.xr.arfoundation", "Runtime/ARFoundation/Planes/ARPlaneManager.cs") },
                { "ARAnchorManager", PackageScriptGuid("com.unity.xr.arfoundation", "Runtime/ARFoundation/Anchors/ARAnchorManager.cs") },
                { "ARRaycastManager", PackageScriptGuid("com.unity.xr.arfoundation", "Runtime/ARFoundation/Raycasts/ARRaycastManager.cs") },
                { "XROrigin", PackageScriptGuid("com.unity.xr.core-utils", "Runtime/XROrigin.cs") },
                { "TrackedPoseDriver", PackageScriptGuid("com.unity.inputsystem", "InputSystem/Runtime/Plugins/XR/TrackedPoseDriver.cs") }
            };
            foreach (KeyValuePair<string, string> pair in required)
                StringAssert.Contains("guid: " + pair.Value, scene,
                    "в ArMain.unity нет компонента " + pair.Key + " — AR-режим не заработает");
        }

        [Test]
        public void ArScene_HasKvArComponents()
        {
            string scene = File.ReadAllText(Path.Combine(ProjectRoot, KvArMode.ArScenePath));
            string[] scripts =
            {
                "KvArRuntime", "KvArRigBridge", "KvArSessionController", "KvArWorld",
                "KvArContent", "KvArPlacement", "KvArTouchInput", "KvArUi"
            };
            foreach (string script in scripts)
            {
                string guid = ScriptGuid("Assets/_Project/01_Scripts/Ar/" + script + ".cs");
                StringAssert.Contains("guid: " + guid, scene,
                    "в ArMain.unity нет компонента " + script + " — часть AR-режима не подключена");
            }
        }

        [Test]
        public void ArScene_HeadTrackingIsDisabled()
        {
            string scene = File.ReadAllText(Path.Combine(ProjectRoot, KvArMode.ArScenePath));
            string guid = ScriptGuid("Assets/_Project/01_Scripts/Xr/KvXrHeadTracking.cs");
            int index = scene.IndexOf("guid: " + guid, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0, "в ArMain нет KvXrHeadTracking — проверить выключение невозможно");
            // m_Enabled идёт ДО m_Script в блоке MonoBehaviour: читаем ближайшее значение выше.
            int enabledIndex = scene.LastIndexOf("m_Enabled:", index, System.StringComparison.Ordinal);
            Assert.Greater(enabledIndex, 0, "не найден m_Enabled перед KvXrHeadTracking");
            string value = scene.Substring(enabledIndex, 24);
            StringAssert.Contains("m_Enabled: 0", value,
                "KvXrHeadTracking ВКЛЮЧЁН в ArMain: он будет перезаписывать позу AR-камеры каждый кадр");
        }

        [Test]
        public void ArScene_HasMainCameraTagOnArCamera()
        {
            string scene = File.ReadAllText(Path.Combine(ProjectRoot, KvArMode.ArScenePath));
            Assert.IsTrue(scene.Contains("m_Name: ArCamera"),
                "в ArMain нет объекта ArCamera — AR-камера не добавлена");
            Assert.IsTrue(scene.Contains("m_Name: AR Session"),
                "в ArMain нет объекта AR Session");
        }

        [Test]
        public void BuildSettings_BootFirstThenVrThenAr()
        {
            string text = ReadAsset("ProjectSettings", "EditorBuildSettings.asset");
            MatchCollection scenes = Regex.Matches(text, @"path:\s*(Assets/[^\r\n]+\.unity)");
            Assert.GreaterOrEqual(scenes.Count, 4, "в Build Settings меньше четырёх сцен");
            Assert.AreEqual(KvArMode.BootScenePath, scenes[0].Groups[1].Value.Trim(),
                "первой сценой обязана быть BootAr: она решает, какую сцену грузить");
            var paths = new List<string>();
            foreach (Match match in scenes) paths.Add(match.Groups[1].Value.Trim());
            CollectionAssert.Contains(paths, KvArMode.VrScenePath, "в Build Settings пропала XrMain (VR)");
            CollectionAssert.Contains(paths, KvArMode.ArScenePath, "в Build Settings нет ArMain");
        }

        [Test]
        public void SceneBuilder_CopiesVrSceneAndDisablesVrComponents()
        {
            string source = File.ReadAllText(Path.Combine(ProjectRoot,
                "Assets/_Project/01_Scripts/Editor/KvArSceneBuilder.cs"));
            StringAssert.Contains("KvArMode.VrScenePath", source,
                "сборщик обязан копировать XrMain, а не собирать AR-сцену с нуля");
            StringAssert.Contains("SaveScene", source);
            StringAssert.Contains("KvXrHeadTracking", source);
            StringAssert.Contains("KvXrLocomotion", source);
            StringAssert.Contains("KvXrMrRoom", source);
            StringAssert.Contains("SetActive(false)", source);
        }

        [Test]
        public void Renderer_HasArCommandBufferFeature()
        {
            string renderer = ReadAsset("Assets", "Settings", "Mobile_Renderer.asset");
            string guid = PackageScriptGuid("com.unity.xr.arfoundation",
                "Runtime/ARFoundation/Session/ARCommandBufferSupportRendererFeature.cs");
            StringAssert.Contains(guid, renderer,
                "в мобильном URP-рендерере нет ARCommandBufferSupportRendererFeature: Android собирается " +
                "под Vulkan, и фон AR-камеры без этой фичи не рисуется (документация AR Foundation 6.5)");
        }

        [Test]
        public void PlaneVisualPrefab_Exists()
        {
            Assert.IsTrue(File.Exists(Path.Combine(ProjectRoot,
                "Assets", "_Project", "01_Scripts", "Ar", "ArPlaneVisual.prefab")),
                "нет префаба визуализации плоскости — A2 «появился маркер/сетка» проверять нечем");
        }

        /// <summary>
        /// СБОРЩИК APK НЕ ДОЛЖЕН ВЫКЛЮЧАТЬ ARCORE. Это настоящий дефект, пойманный первой
        /// сборкой AR 03.11.2026: `KvXrTools.ConfigureXrLoaders()` пересобирал список лоадеров
        /// на «только OpenXR» перед сборкой. В результате APK уходил без ARCore-провайдера,
        /// и на планшете AR-режим не мог запуститься ВООБЩЕ, при этом ни одной ошибки сборки
        /// не было. Тест держит контракт: в сборщике есть ARCore и оба лоадера для Android,
        /// а старого «оставить только OpenXR» — нет.
        /// </summary>
        [Test]
        public void ApkBuilder_KeepsArCoreLoaderForAndroid()
        {
            string source = ReadAsset("Assets", "_Project", "01_Scripts", "Editor", "KvXrTools.cs");
            StringAssert.Contains("FindArCoreLoader", source,
                "сборщик не ищет ARCoreLoader — на планшете не будет провайдера ARCore");
            StringAssert.Contains("wanted.Add(arCore)", source,
                "сборщик не добавляет ARCore в список лоадеров Android — AR-режим не запустится");
            StringAssert.Contains("BuildTargetGroup.Android", source,
                "в сборщике нет разделения платформ: OpenXR+ARCore должны ставиться только для Android");
            StringAssert.DoesNotContain("TrySetLoaders(new List<XRLoader> { openXr })", source,
                "сборщик снова оставляет ОДИН лоадер (OpenXR) — ARCore выключен, планшет без AR");

            // ВТОРАЯ ЛОВУШКА ТОГО ЖЕ МЕСТА (найдена 03.11.2026): сцена в APK была ОДНА — XrMain.
            StringAssert.Contains("EnabledBuildScenes", source,
                "сборщик не берёт список сцен из Build Settings — ArMain/BootAr могут не попасть в APK");
            StringAssert.DoesNotContain("scenes = new[] { XrScenePath }", source,
                "сборщик снова жёстко берёт одну XrMain — AR-сцена и BootAr в APK не попадут");
        }

        /// <summary>
        /// В APK ПОПАДАЮТ СЦЕНЫ УСТРОЙСТВА, ПЕРВАЯ — BootAr; ПК-сцена MainScene в APK НЕ ИДЁТ.
        /// Проверяется вызовом настоящей функции сборщика (не разбором исходника): если порядок
        /// сцен в Build Settings сломается, APK начнёт запускаться сразу в VR/AR-сцене в обход
        /// определения режима, и на телефоне это будет чёрный экран вместо AR. ПК-сцена в APK
        /// не нужна: она HDRP/мышь и тянет в устройство компоненты конвейера, которого там нет.
        /// </summary>
        [Test]
        public void ApkBuilder_IncludesBootArFirstAndArScene()
        {
            string[] scenes = KazistovVvXrEditor.KvXrTools.EnabledBuildScenes();
            Assert.GreaterOrEqual(scenes.Length, 3,
                "в APK должно попасть минимум 3 сцены (BootAr, XrMain, ArMain), сейчас: " +
                string.Join(", ", scenes));
            Assert.AreEqual(KvArMode.BootScenePath, scenes[0],
                "первой сценой APK обязана быть BootAr: она решает, какую сцену грузить");
            CollectionAssert.Contains(scenes, KvArMode.VrScenePath, "в APK нет VR-сцены XrMain");
            CollectionAssert.Contains(scenes, KvArMode.ArScenePath, "в APK нет AR-сцены ArMain");
            CollectionAssert.DoesNotContain(scenes, "Assets/_Project/00_Scenes/MainScene.unity",
                "ПК-сцена MainScene (HDRP, мышь) попала в APK — на устройстве она не исполняется " +
                "и тянет компоненты конвейера, которого на Android нет");
            Assert.IsTrue(KazistovVvXrEditor.KvXrTools.IsPcOnlyScene("Assets/_Project/00_Scenes/MainScene.unity"));
            Assert.IsFalse(KazistovVvXrEditor.KvXrTools.IsPcOnlyScene(KvArMode.ArScenePath));
        }

        /// <summary>
        /// VR-РИГ В ArMain ВЫКЛЮЧЕН. Это не косметика: его `Start` создаёт в рантайме руки
        /// OpenXR (`KvXrHands`), жесты и облёт простоя камеры. На планшете 04.11.2026 именно это
        /// ломало ARCore: сессия застревала в «инициализируется», камера не открывалась,
        /// плоскостей 0. Тест не даёт вернуть риг в AR-сцену.
        /// </summary>
        [Test]
        public void ArScene_VrRigIsDisabled()
        {
            string scene = File.ReadAllText(Path.Combine(ProjectRoot, KvArMode.ArScenePath));
            string guid = ScriptGuid("Assets/_Project/01_Scripts/Xr/KvXrRig.cs");
            int index = scene.IndexOf("guid: " + guid, System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(index, 0, "в ArMain нет KvXrRig — проверить его выключение невозможно");
            int enabledIndex = scene.LastIndexOf("m_Enabled:", index, System.StringComparison.Ordinal);
            Assert.Greater(enabledIndex, 0, "не найден m_Enabled перед KvXrRig");
            StringAssert.Contains("m_Enabled: 0", scene.Substring(enabledIndex, 24),
                "KvXrRig ВКЛЮЧЁН в ArMain: он создаст руки OpenXR/жесты/облёт камеры и сломает ARCore");
            StringAssert.Contains("DisableInScene<KvXrRig>", ReadAsset("Assets", "_Project", "01_Scripts",
                "Editor", "KvArSceneBuilder.cs"), "сборщик AR-сцены больше не выключает VR-риг");
        }

        /// <summary>
        /// ИНТЕРФЕЙС AR ПОДНИМАЕТСЯ БЕЗ РИГА: меню, окна, мост потока, гашение стендов 14,4 м.
        /// Без этих вызовов при выключенном риге в AR не будет меню 110 команд и точек.
        /// </summary>
        [Test]
        public void ArRuntime_RaisesUiWithoutVrRig()
        {
            string source = ReadAsset("Assets", "_Project", "01_Scripts", "Ar", "KvArRuntime.cs");
            StringAssert.Contains("EnsureArUiSupport", source);
            StringAssert.Contains("KvXrStartMenu.Ensure", source);
            StringAssert.Contains("KvXrWindowHost.Ensure", source);
            StringAssert.Contains("KvXrSceneEmpty", source,
                "виртуальные стенды 14,4 м не гасятся — в AR они будут видны поверх реального стола");
            StringAssert.Contains("feedAim = false", source,
                "авто-прицел моста должен быть выключен: прицел подаёт палец");
        }

        /// <summary>
        /// СТОРОЖ ЗАСТРЯВШЕЙ СЕССИИ: сброс ARSession.Reset, если инициализация идёт слишком долго.
        /// На планшете это спасает от «чёрного фона без плоскостей» (проверено, что такое бывает).
        /// </summary>
        [Test]
        public void SessionController_ResetsStuckInitialization()
        {
            string source = ReadAsset("Assets", "_Project", "01_Scripts", "Ar", "KvArSessionController.cs");
            StringAssert.Contains("session.Reset()", source,
                "нет сброса застрявшей ARCore-сессии — чёрный фон останется без лечения");
            StringAssert.Contains("инициализация дольше", source);
            StringAssert.Contains("stuckResetSeconds", source);
        }


        /// <summary>
        /// ПОРЯДОК ЛОАДЕРОВ ЗАВИСИТ ОТ УСТРОЙСТВА. Один APK на шлем и планшет: шлему нужен
        /// OpenXR первым (иначе не поднимается VR), планшету — ARCore первым (иначе зависает
        /// инициализация XR и экран остаётся чёрным). Проверяется чистая логика выбора.
        /// </summary>
        [Test]
        public void LoaderBootstrap_PicoFirstOpenXr_TabletFirstArCore()
        {
            string[] pico = KazistovVvAr.KvArLoaderBootstrap.DesiredLoaderOrder(true);
            Assert.AreEqual(2, pico.Length);
            StringAssert.Contains("OpenXR", pico[0],
                "на шлеме ПЕРВЫМ обязан идти OpenXR: с ARCore первым ломается запуск VR (жалоба MR 05.11.2026)");
            StringAssert.Contains("ARCore", pico[1], "на шлеме вторым обязан идти ARCore (запас на всякий случай)");

            string[] tablet = KazistovVvAr.KvArLoaderBootstrap.DesiredLoaderOrder(false);
            Assert.AreEqual(2, tablet.Length);
            StringAssert.Contains("ARCore", tablet[0],
                "на планшете ПЕРВЫМ обязан идти ARCore: с OpenXR первым PICO-загрузчик зависает в инициализации");
            StringAssert.Contains("OpenXR", tablet[1], "на планшете вторым обязан идти OpenXR (запас)");
        }

        /// <summary>
        /// АВТОИНИЦИАЛИЗАЦИЯ XR ДЛЯ ANDROID ВЫКЛЮЧЕНА. Иначе XR Management поднимет XR ДО первой
        /// сцены с тем порядком, что записан в настройках, и `KvArLoaderBootstrap` уже ничего
        /// не сможет переставить: одна из платформ получит неверный порядок и зависнет.
        /// </summary>
        [Test]
        public void AndroidSettings_DoNotAutoInitializeXr()
        {
            string settings = ReadAsset("Assets", "XR", "XRGeneralSettingsPerBuildTarget.asset");
            int android = settings.IndexOf("m_Name: Android Settings", System.StringComparison.Ordinal);
            Assert.GreaterOrEqual(android, 0, "в настройках XR нет блока «Android Settings»");
            int init = settings.IndexOf("m_InitManagerOnStart:", android, System.StringComparison.Ordinal);
            Assert.Greater(init, android, "в Android Settings нет поля m_InitManagerOnStart");
            string value = settings.Substring(init, 40);
            StringAssert.Contains("m_InitManagerOnStart: 0", value,
                "для Android включена автоинициализация XR: порядок загрузчиков по устройству не применится, " +
                "и одна из платформ (шлем или планшет) зависнет в инициализации XR");
        }

        /// <summary>
        /// BOOTAR ПОДНИМАЕТ XR САМ, с порядком по устройству. Без этого вызова при выключенной
        /// автоинициализации XR не поднимется вообще, и приложение всегда показывало бы отказ.
        /// </summary>
        [Test]
        public void Boot_InitializesXrForDevice()
        {
            string source = ReadAsset("Assets", "_Project", "01_Scripts", "Ar", "KvArBoot.cs");
            StringAssert.Contains("KvArLoaderBootstrap.InitializeForDevice", source,
                "BootAr не инициализирует XR с порядком по устройству — при выключенной автоинициализации XR не поднимется");
        }

        /// <summary>
        /// ANDROID-АКТИВНОСТЬ — GameActivity СО ШТАТНОЙ ТЕМОЙ UNITY.
        ///
        /// ПОЧЕМУ (уточнено 05.11.2026 по двум живым устройствам):
        ///  • шлему PICO нужна GameActivity: с классической UnityPlayerActivity OpenXR-лоадер
        ///    поднимается, но VR-дисплей не активируется («XR НЕ активен», MR/AR-возможностей нет);
        ///  • чёрный экран на планшете HyperOS/Android 16 давала НЕ GameActivity, а кастомная
        ///    AppCompat-тема `Theme.AppCompat.DayNight.NoActionBar` — с ней окно получало
        ///    прозрачную поверхность и Unity не выводил кадры. Штатная тема Unity
        ///    `@style/BaseUnityGameActivityTheme` работает на планшете.
        /// Тест держит контракт: GameActivity + тема Unity + applicationEntry 2, и НЕ наша
        /// кастомная AppCompat-тема (иначе снова чёрный экран).
        /// </summary>
        [Test]
        public void AndroidManifest_UsesGameActivityWithUnityTheme()
        {
            string manifest = ReadAsset("Assets", "Plugins", "Android", "AndroidManifest.xml");
            StringAssert.Contains("com.unity3d.player.UnityPlayerGameActivity", manifest,
                "в манифесте нет UnityPlayerGameActivity — на шлеме PICO не включится VR-дисплей");
            StringAssert.Contains("@style/BaseUnityGameActivityTheme", manifest,
                "тема активности не штатная Unity: кастомная AppCompat-тема даёт чёрный экран на HyperOS (проверено)");
            StringAssert.DoesNotContain("@style/Theme.AppCompat", manifest,
                "в манифесте снова кастомная AppCompat-тема — на планшете это давало чёрный экран");

            string project = ReadAsset("ProjectSettings", "ProjectSettings.asset");
            StringAssert.Contains("androidApplicationEntry: 2", project,
                "androidApplicationEntry не 2 (GameActivity): на шлеме PICO не активируется VR-дисплей");
        }    }
}
