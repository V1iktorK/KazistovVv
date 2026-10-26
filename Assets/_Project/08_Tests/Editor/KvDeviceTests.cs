using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты ПРИЁМКИ НА УСТРОЙСТВЕ (сессия §31: шлем PICO 4 Ultra приехал).
    ///
    /// ЗАЧЕМ ЭТИ ТЕСТЫ. До сих пор всё проверялось логикой и тестами «вслепую». С приездом
    /// устройства появляются два класса ошибок, которые логикой не ловятся вовсе:
    ///   1) **имя и идентификатор приложения** — в шлеме видно не «KazistovVv», а имя шаблона
    ///      Unity (`com.UnityTechnologies.com.unity.template.urpblank`), и такое приложение
    ///      не отличить от любого другого проекта, собранного из шаблона;
    ///   2) **профиль контроллеров**: если в сборке под Android не включено ни одного профиля,
    ///      понятного рантайму PICO, — в шлеме не работают курок, грип и стики. В редакторе
    ///      этого не видно: там ввод идёт с мыши, и всё «работает».
    ///
    /// ПОЧЕМУ СОСТОЯНИЕ ФИЧ ЧИТАЕТСЯ ЧЕРЕЗ API, А НЕ РАЗБОРОМ ФАЙЛА. Правильный вопрос —
    /// «что реально включено для платформы Android», а на него отвечает
    /// `OpenXRSettings.GetSettingsForBuildTargetGroup`. Разбор файла дал бы ответ
    /// «в файле написано», а не «в сборку попадёт» — ровно та ошибка, из-за которой в §29
    /// отчёт печатал «НЕТ» на живой настройке.
    ///
    /// Тесты написаны ДО правки настроек и на прежнем состоянии падали (правило [K5]);
    /// после применения решений §31 — зелёные.
    /// </summary>
    public class KvDeviceTests
    {
        private const string ProjectSettingsPath = "ProjectSettings/ProjectSettings.asset";
        private const string OwnIdentifier = "com.KazistovVv.app";

        /// <summary>
        /// Профили контроллеров, понятные рантайму PICO: простой профиль Khronos и профиль
        /// Oculus Touch (PICO реализует его для совместимости с приложениями под Quest).
        /// Профиль — это описание контроллера в OpenXR, а не SDK платформы, поэтому решение
        /// [R3] («только OpenXR, PICO SDK не ставим») он не нарушает.
        /// </summary>
        private static readonly string[] AcceptableControllerProfiles =
        {
            "KHRSimpleControllerProfile",
            "OculusTouchControllerProfile"
        };

        // ==================================================== 1. ИМЯ И ИДЕНТИФИКАТОР ПРИЛОЖЕНИЯ

        /// <summary>
        /// ИДЕНТИФИКАТОР ПРИЛОЖЕНИЯ — СВОЙ, А НЕ ИЗ ШАБЛОНА UNITY.
        /// Решение владельца (§31): `com.KazistovVv.app`.
        /// </summary>
        [Test]
        public void AppIdentifier_IsProjectOwn()
        {
            string text = ReadProjectFile(ProjectSettingsPath);
            Assert.IsNotNull(text, "не прочитан " + ProjectSettingsPath);

            string android = IdentifierFor(text, "Android");
            Assert.IsNotNull(android, "в " + ProjectSettingsPath + " нет идентификатора для Android");
            Assert.AreEqual(OwnIdentifier, android,
                "идентификатор приложения для Android обязан быть своим: это имя видно в шлеме");

            string standalone = IdentifierFor(text, "Standalone");
            Assert.IsNotNull(standalone, "в " + ProjectSettingsPath + " нет идентификатора для Standalone");
            StringAssert.DoesNotContain("unity.template", standalone,
                "идентификатор для компьютера остался значением шаблона Unity");
        }

        // ==================================================== 2. ПРОФИЛИ КОНТРОЛЛЕРОВ ДЛЯ ANDROID

        /// <summary>
        /// В СБОРКЕ ПОД ШЛЕМ ДОЛЖЕН БЫТЬ ПРОФИЛЬ КОНТРОЛЛЕРОВ, ПОНЯТНЫЙ PICO.
        /// Проверяется набор ИМЕННО ДЛЯ ANDROID: включённые для Standalone профили в APK
        /// не попадают, и наоборот. До §31 для Android были включены только профили Meta,
        /// а `KHRSimpleControllerProfile` — выключен.
        /// </summary>
        [Test]
        public void Android_HasControllerProfileForPico()
        {
            List<string> enabled = EnabledFeatureNames(BuildTargetGroup.Android);

            Assert.Greater(enabled.Count, 0,
                "для Android не включено ни одной OpenXR-фичи — в шлеме не заработает ничего. " +
                "Смотрите Assets/XR/Settings/OpenXR Package Settings.asset");

            foreach (string name in enabled)
            {
                foreach (string acceptable in AcceptableControllerProfiles)
                {
                    if (name.IndexOf(acceptable, StringComparison.OrdinalIgnoreCase) >= 0) return;
                }
            }

            Assert.Fail("для Android не включён ни один профиль контроллеров, понятный PICO (" +
                        string.Join(" или ", AcceptableControllerProfiles) + "). Включены: " +
                        string.Join(", ", enabled.ToArray()) +
                        " — в шлеме не будут работать курок, грип и стики");
        }

        /// <summary>
        /// АКТИВНЫЙ ЛОАДЕР ДЛЯ ANDROID — ТОЛЬКО OpenXR. Mock HMD (заглушка для отладки без
        /// устройства) в сборке под шлем подменяет реальный трекинг — этот дефект уже находили
        /// в §28; тест не даёт ему вернуться.
        /// </summary>
        [Test]
        public void Android_XrLoaderIsOpenXrOnly()
        {
            var perTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(
                "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
            Assert.IsNotNull(perTarget,
                "не найден XRGeneralSettingsPerBuildTarget — настройки XR отсутствуют");

            XRGeneralSettings settings = perTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
            Assert.IsNotNull(settings, "для Android нет настроек XR");
            Assert.IsNotNull(settings.Manager, "у настроек XR для Android нет менеджера");

            // ТИП СПИСКА — `IReadOnlyList`, А НЕ `List`: в этом пакете XR Management свойство
            // `activeLoaders` объявлено только для чтения (ошибка CS0266 ловится гейтом проекта).
            IReadOnlyList<XRLoader> loaders = settings.Manager.activeLoaders;
            Assert.IsNotNull(loaders, "список активных XR-лоадеров пуст");
            Assert.AreEqual(1, loaders.Count,
                "активный XR-лоадер для Android должен быть РОВНО один (OpenXR); сейчас: " +
                loaders.Count);

            string loaderName = loaders[0] != null ? loaders[0].GetType().Name : "null";
            StringAssert.Contains("OpenXR", loaderName,
                "активный лоадер для Android — не OpenXR, а " + loaderName);
        }

        /// <summary>
        /// В XR-СЦЕНЕ ЕСТЬ ВСЁ, ЧТО НУЖНО ДЛЯ ПЕРВОГО ВХОДА: риг с камерой, сторож десктопных
        /// сервисов, начинка (столы и роботы) и система событий для интерфейса. Проверка по
        /// GUID скриптов в файле сцены: без неё «сцена собрана» остаётся утверждением отчёта,
        /// как это было в §28.
        /// </summary>
        [Test]
        public void XrScene_HasEverythingNeededForFirstEntry()
        {
            const string xrScenePath = "Assets/_Project/00_Scenes/XrMain.unity";
            Assert.IsTrue(File.Exists(xrScenePath), "нет XR-сцены " + xrScenePath);
            string scene = File.ReadAllText(xrScenePath);

            string[] required =
            {
                "Assets/_Project/01_Scripts/Xr/KvXrRig.cs",
                "Assets/_Project/01_Scripts/Xr/KvXrCameraGuard.cs",
                "Assets/_Project/01_Scripts/Xr/KvXrLocomotion.cs",
                "Assets/_Project/01_Scripts/Xr/KvXrUiPointer.cs",
                "Assets/_Project/01_Scripts/Xr/KvXrPanelAnchor.cs",
                "Assets/_Project/01_Scripts/Xr/KvXrSceneContent.cs"
            };

            var missing = new List<string>();
            foreach (string scriptPath in required)
            {
                string guid = AssetDatabase.AssetPathToGUID(scriptPath);
                if (string.IsNullOrEmpty(guid))
                {
                    missing.Add(scriptPath + " (нет guid — файл отсутствует)");
                    continue;
                }
                if (scene.IndexOf("guid: " + guid, StringComparison.OrdinalIgnoreCase) < 0)
                    missing.Add(scriptPath);
            }

            Assert.AreEqual(0, missing.Count,
                "в XR-сцене нет компонентов, без которых первый вход не состоится: " +
                string.Join(", ", missing.ToArray()));
        }

        // ==================================================== вспомогательное

        private static string ReadProjectFile(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }

        /// <summary>
        /// Идентификатор приложения для платформы из блока `applicationIdentifier:`.
        /// Блок плоский: `Android: com.example.app` с отступом глубже ключа.
        /// </summary>
        private static string IdentifierFor(string text, string platform)
        {
            string[] lines = text.Replace("\r\n", "\n").Split('\n');
            int start = -1;
            int indent = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed = lines[i].TrimStart();
                if (!trimmed.StartsWith("applicationIdentifier:", StringComparison.Ordinal)) continue;
                start = i;
                indent = lines[i].Length - trimmed.Length;
                break;
            }
            if (start < 0) return null;

            for (int i = start + 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.Trim().Length == 0) continue;
                int lineIndent = line.Length - line.TrimStart().Length;
                if (lineIndent <= indent) break;
                string trimmed = line.Trim();
                if (!trimmed.StartsWith(platform + ":", StringComparison.Ordinal)) continue;
                return trimmed.Substring(platform.Length + 1).Trim();
            }
            return null;
        }

        /// <summary>Имена включённых OpenXR-фич для платформы (через API пакета).</summary>
        private static List<string> EnabledFeatureNames(BuildTargetGroup group)
        {
            var result = new List<string>();
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
            if (settings == null) return result;

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            if (features == null) return result;

            foreach (OpenXRFeature feature in features)
            {
                if (feature == null || !feature.enabled) continue;
                result.Add(feature.name);
            }
            return result;
        }
    }
}
