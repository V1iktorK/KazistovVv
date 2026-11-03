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
    /// зависит: порядок XR-лоадеров (OpenXR первый, ARCore второй), пакет провайдера ARCore,
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

        [Test]
        public void AndroidLoaders_AreOpenXrThenArCore()
        {
            List<string> names = AndroidLoaderNames();
            Assert.AreEqual(2, names.Count,
                "лоадеров Android должно быть ровно два (OpenXR + ARCore), сейчас: " +
                string.Join(", ", names.ToArray()));
            StringAssert.Contains("OpenXR", names[0],
                "ПЕРВЫЙ лоадер обязан быть OpenXR: иначе шлем уйдёт в AR-режим и VR сломается");
            StringAssert.Contains("ARCore", names[1],
                "ВТОРЫМ обязан быть ARCore: без него на планшете нет ни камеры, ни плоскостей");
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
            // 0 — Optional: приложение ставится и на устройства без ARCore (шлем PICO),
            // 1 — Required: установка на PICO была бы отфильтрована/сломана.
            Assert.AreEqual("0", requirement.Groups[1].Value,
                "ARCore Requirement обязан быть Optional (0): иначе общий APK не встанет на шлем");
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
    }
}
