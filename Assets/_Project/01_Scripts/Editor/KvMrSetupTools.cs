#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using KazistovVv.Core;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// НАСТРОЙКА MR: ВКЛЮЧЕНИЕ ФИЧ OPENXR И ЗАМЕР МАСШТАБА (создано 02.11.2026, S45).
    ///
    /// ЗАЧЕМ ИНСТРУМЕНТ, А НЕ РУЧНАЯ ГАЛОЧКА. Состояние фич OpenXR хранится в
    /// `Assets/XR/Settings/OpenXR Package Settings.asset` и зависит от ПЛАТФОРМЫ: включённая для
    /// Standalone фича в APK не попадает. Ручная галочка в инспекторе не подтверждает, что фича
    /// попала в Android-сборку, — а метод ниже печатает состояние ДО и ПОСЛЕ и перечитывает факт.
    ///
    /// ЧТО ВКЛЮЧАЕТСЯ И ПОЧЕМУ ИМЕННО ЭТО (сверено с пакетом PICO OpenXR 1.4.1):
    ///   • `PassthroughFeature` — единственная фича, объявляющая расширение `XR_FB_passthrough`;
    ///     без неё сквозного вида не будет ни при какой другой настройке;
    ///   • `OpenXRExtensions` (`com.unity.openxr.pico.features`) — ОБЯЗАТЕЛЬНАЯ зависимость набора
    ///     «PICO XR» и источник событий пространственного меша; без неё валидация OpenXR даёт
    ///     ОШИБКУ, а меш не приходит;
    ///   • `PICOSpatialMesh` — реальные поверхности комнаты (по ним луч находит стол);
    ///   • `PICOSpatialAnchor` — пространственные якоря (место робота между запусками);
    ///   • `PICOSceneCapture` — семантическая сцена (стол как объект с габаритами) и разрешение
    ///     `SPATIAL_DATA`, которое пакет PICO добавляет в манифест только при этих фичах.
    ///
    /// СЦЕНА НЕ ПРАВИТСЯ: инструмент трогает только настройки OpenXR и ничего в `XrMain.unity`.
    /// </summary>
    public static class KvMrSetupTools
    {
        private const string MenuRoot = "Tools/KazistovVv XR/MR: ";

        /// <summary>Фичи PICO, без которых MR на PICO 4 Ultra не включается.</summary>
        public static readonly string[] PicoMrFeatureTypeNames =
        {
            "PassthroughFeature",
            "OpenXRExtensions",
            "PICOSpatialMesh",
            "PICOSpatialAnchor",
            "PICOSceneCapture",
        };

        /// <summary>
        /// Фичи Meta, без которых MR на Quest 3 не включается.
        ///
        /// ПОЧЕМУ НЕ ВСЕ СРАЗУ. Meta-фичи имеют обязательную зависимость `Meta Quest: Session`
        /// (`error = true` в валидации — сборка упадёт, если её забыть) и правят манифест
        /// oculus-разрешениями. Поэтому они включаются ОТДЕЛЬНЫМ шагом и отдельной сборкой:
        /// сначала проверяем PICO, потом добавляем Quest, чтобы не гадать, что сломало сборку.
        /// </summary>
        public static readonly string[] MetaMrFeatureTypeNames =
        {
            "MetaQuestFeature",           // com.unity.openxr.feature.metaquest
            "ARSessionFeature",           // обязательная зависимость AR-фич Meta (error-валидация)
            "ARCameraFeature",            // сквозной вид Quest (создаёт passthrough-слой)
            "ARMeshFeature",              // реальные поверхности Quest
            "ARAnchorFeature",            // якоря Quest
            "OculusTouchControllerProfile", // контроллеры Quest для Android (сейчас выключен)
        };

        // ==================================================== ГЛАВНЫЙ ВХОД ДЛЯ BATCHMODE

        /// <summary>
        /// ПОДГОТОВКА MR ОДНОЙ КОМАНДОЙ: включить PICO-фичи и замерить масштаб роботов.
        /// Вызывается из консоли:
        ///   Unity -batchmode -quit -projectPath ... -executeMethod
        ///       KazistovVvXrEditor.KvMrSetupTools.Prepare -logFile Logs/mr-prepare.log
        /// </summary>
        public static void Prepare()
        {
            EnablePicoMrFeatures();
            MeasureRobotScale();
            Debug.Log("[KvMrSetup] ПОДГОТОВКА MR ЗАВЕРШЕНА");
        }

        // ==================================================== ВКЛЮЧЕНИЕ ФИЧ

        [MenuItem(MenuRoot + "1. Включить MR-фичи PICO для Android", false, 80)]
        public static void EnablePicoMrFeatures()
        {
            EnableFeatures(BuildTargetGroup.Android, PicoMrFeatureTypeNames, "PICO");
        }

        [MenuItem(MenuRoot + "2. Включить MR-фичи Meta (Quest) для Android", false, 81)]
        public static void EnableMetaMrFeatures()
        {
            EnableFeatures(BuildTargetGroup.Android, MetaMrFeatureTypeNames, "Meta");
        }

        /// <summary>Отчёт: какие фичи включены для Android сейчас.</summary>
        [MenuItem(MenuRoot + "3. Отчёт: что включено для Android", false, 82)]
        public static void ReportAndroidFeatures()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvMrSetup] === ФИЧИ OPENXR ДЛЯ ANDROID ===");

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[KvMrSetup] нет настроек OpenXR для Android");
                return;
            }

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            int enabled = 0;
            if (features != null)
            {
                foreach (OpenXRFeature feature in features)
                {
                    if (feature == null || !feature.enabled) continue;
                    enabled++;
                    sb.AppendLine("  ВКЛ: " + feature.name + " (" + feature.GetType().Name + ")");
                }
            }
            sb.AppendLine("  всего включено: " + enabled + " из " + (features != null ? features.Length : 0));
            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ВКЛЮЧИТЬ ФИЧИ ПО ИМЕНАМ ТИПОВ. Перечитывание после сохранения — обязательно: правка
        /// считается сделанной только если её видно в перечитанном ассете (урок §29).
        /// </summary>
        private static void EnableFeatures(BuildTargetGroup targetGroup, string[] wantedTypeNames,
            string platformLabel)
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvMrSetup] === ВКЛЮЧЕНИЕ MR-ФИЧ " + platformLabel + " ДЛЯ ANDROID ===");

            // НОВЫЕ ФИЧИ СНАЧАЛА ДОЛЖНЫ ПОПАСТЬ В АССЕТ: в batchmode список сам не обновляется
            // (проверено 15.10.2026 на профилях контроллеров).
            try { FeatureHelpers.RefreshFeatures(targetGroup); }
            catch (Exception e)
            {
                sb.AppendLine("  ПРЕДУПРЕЖДЕНИЕ: обновление списка фич бросило " + e.GetType().Name + ": " + e.Message);
            }

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup);
            if (settings == null)
            {
                Debug.LogError("[KvMrSetup] нет настроек OpenXR для " + targetGroup);
                return;
            }

            OpenXRFeature[] before = settings.GetFeatures<OpenXRFeature>();
            if (before == null || before.Length == 0)
            {
                Debug.LogError("[KvMrSetup] пакет OpenXR не вернул ни одной фичи");
                return;
            }

            bool changed = false;
            for (int i = 0; i < wantedTypeNames.Length; i++)
            {
                string wanted = wantedTypeNames[i];
                OpenXRFeature found = null;
                for (int k = 0; k < before.Length; k++)
                {
                    if (before[k] == null) continue;
                    if (before[k].GetType().Name == wanted) { found = before[k]; break; }
                }

                if (found == null)
                {
                    sb.AppendLine("  НЕ НАЙДЕНА: " + wanted + " — фича отсутствует в настройках (пакет не найден?)");
                    continue;
                }

                if (found.enabled)
                {
                    sb.AppendLine("  уже включена: " + found.name + " (" + wanted + ")");
                    continue;
                }

                found.enabled = true;
                EditorUtility.SetDirty(found);
                EditorUtility.SetDirty(settings);
                changed = true;
                sb.AppendLine("  ВКЛЮЧЕНА: " + found.name + " (" + wanted + ")");
            }

            if (changed)
            {
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            // ПЕРЕЧИТЫВАНИЕ ФАКТА (а не намерения).
            OpenXRFeature[] after = OpenXRSettings.GetSettingsForBuildTargetGroup(targetGroup)
                .GetFeatures<OpenXRFeature>();
            int confirmed = 0;
            for (int i = 0; i < wantedTypeNames.Length; i++)
            {
                string wanted = wantedTypeNames[i];
                bool on = false;
                if (after != null)
                {
                    for (int k = 0; k < after.Length; k++)
                    {
                        if (after[k] == null) continue;
                        if (after[k].GetType().Name != wanted) continue;
                        on = after[k].enabled;
                        break;
                    }
                }
                if (on) confirmed++;
                else sb.AppendLine("  ИТОГ: " + wanted + " НЕ ВКЛЮЧИЛАСЬ — проверить вручную");
            }

            sb.AppendLine("  ПОДТВЕРЖДЕНО ВКЛЮЧЕННЫМИ: " + confirmed + " из " + wantedTypeNames.Length +
                          " (" + platformLabel + ", " + targetGroup + ")");
            Debug.Log(sb.ToString());
        }

        // ==================================================== ЗАМЕР МАСШТАБА (1:1)

        /// <summary>
        /// ЗАМЕР ГАБАРИТОВ ВСЕХ МОДЕЛЕЙ КАТАЛОГА — доказательство масштаба 1:1.
        ///
        /// ЗАЧЕМ. «Масштаб 1:1» — утверждение о факте, а не о намерении. Если модели импортированы
        /// в метрах, высота робота будет сопоставима с реальной (0,5…2,5 м), а не 14 м и не 14 см.
        /// Замер печатает числа, которые попадут в `Docs/ОТЧЁТ_MR.md`.
        ///
        /// ПОЧЕМУ БЕЗ СЦЕНЫ. Модели лежат в `Resources/Models/Robots` и грузятся по каталогу
        /// (`KvRobotCatalog`) — это те же префабы, что создаёт спавнер. Сцену не открываем: её
        /// SHA256 закреплён тестом, лишнее открытие/закрытие может её перезаписать.
        /// </summary>
        [MenuItem(MenuRoot + "4. Замер масштаба роботов (1:1)", false, 83)]
        public static void MeasureRobotScale()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvMrSetup] === ЗАМЕР ГАБАРИТОВ МОДЕЛЕЙ РОБОТОВ (метры, 1 юнит = 1 м) ===");
            sb.AppendLine("  id | высота | ширина | глубина | вердикт 1:1");

            var culture = CultureInfo.InvariantCulture;
            KvRobotModel[] models = KvRobotCatalog.All;
            int measured = 0;
            int metric = 0;

            for (int i = 0; i < models.Length; i++)
            {
                KvRobotModel model = models[i];
                GameObject prefab = null;
                // ПУТЬ ИЗ КАТАЛОГА МОЖЕТ НАЧИНАТЬСЯ С «Resources/»: `Resources.Load` ждёт путь
                // ОТНОСИТЕЛЬНО папки Resources, поэтому приставку надо отбросить (иначе замер
                // молча показал бы «префаб не найден» — так и случилось в первом прогоне).
                string resourcePath = model.PrefabPath;
                if (resourcePath != null && resourcePath.StartsWith("Resources/", StringComparison.Ordinal))
                    resourcePath = resourcePath.Substring("Resources/".Length);
                try { prefab = Resources.Load<GameObject>(resourcePath); }
                catch (Exception e)
                {
                    // Не «проглотить»: путь из каталога мог быть записан неверно — это видно в отчёте.
                    sb.AppendLine("  " + model.Id + " | ОШИБКА ЗАГРУЗКИ: " + e.GetType().Name + ": " + e.Message);
                    prefab = null;
                }

                if (prefab == null)
                {
                    sb.AppendLine("  " + model.Id + " | ПРЕФАБ НЕ НАЙДЕН по пути " + resourcePath);
                    continue;
                }

                GameObject instance = null;
                try
                {
                    instance = UnityEngine.Object.Instantiate(prefab);
                    Vector3 size = MeasureSize(instance.transform);
                    measured++;

                    // Признак метричности: высота робота-манипулятора реальна (0,3…3 м).
                    // 14 м — это «стенд», 0,014 м — «игрушка»: оба варианта означают, что масштаб
                    // модели не совпадает с реальным миром.
                    bool isMetric = size.y > 0.3f && size.y < 3.0f;
                    if (isMetric) metric++;
                    sb.AppendLine(string.Format(culture,
                        "  {0} | {1:0.000} | {2:0.000} | {3:0.000} | {4}",
                        model.Id, size.y, size.x, size.z,
                        isMetric ? "ДА (0,3…3 м)" : "НЕТ — проверить масштаб префаба"));
                }
                catch (Exception e)
                {
                    sb.AppendLine("  " + model.Id + " | ОШИБКА ЗАМЕРА: " + e.GetType().Name + ": " + e.Message);
                }
                finally
                {
                    if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            sb.AppendLine("  ИТОГ: замерено " + measured + " моделей, метрических " + metric +
                          " · каталог " + models.Length);
            Debug.Log(sb.ToString());
        }

        // ==================================================== ЗАМЕР МАСШТАБА В СЦЕНЕ

        /// <summary>
        /// ЗАМЕР ГАБАРИТОВ РОБОТОВ В САМОЙ СЦЕНЕ `XrMain` — то, что видит оператор в шлеме.
        ///
        /// ЗАЧЕМ ВТОРОЙ ЗАМЕР ПОСЛЕ ЗАМЕРА ПРЕФАБОВ. У трёх моделей (`stand_6axis`, `ur5`,
        /// `scara_ls10b702s`) замер префаба дал подозрительно малые габариты: вероятно, часть
        /// мешей подставляется/включается иначе, чем в префабе. Проверять надо НЕ префаб, а
        /// ФАКТИЧЕСКИЙ объект сцены — его и видит оператор.
        ///
        /// СЦЕНА НЕ СОХРАНЯЕТСЯ: открывается, измеряется, закрывается. Пин SHA256 на файл сцены
        /// (тест `KvXrS42Tests`) при этом не нарушается — файл на диске не меняется.
        /// </summary>
        [MenuItem(MenuRoot + "5. Замер роботов в сцене XrMain", false, 84)]
        public static void MeasureSceneRobots()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvMrSetup] === ЗАМЕР РОБОТОВ В СЦЕНЕ XrMain (метры) ===");

            string scenePath = "Assets/_Project/00_Scenes/XrMain.unity";
            UnityEngine.SceneManagement.Scene scene = default(UnityEngine.SceneManagement.Scene);
            try
            {
                scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                    scenePath, UnityEditor.SceneManagement.OpenSceneMode.Additive);
            }
            catch (Exception e)
            {
                Debug.LogError("[KvMrSetup] сцена не открылась: " + e.GetType().Name + ": " + e.Message);
                return;
            }

            try
            {
                var culture = CultureInfo.InvariantCulture;
                int measured = 0;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root == null) continue;
                    string lower = root.name.ToLowerInvariant();
                    bool isRobotOrStand = lower.Contains("робот") || lower.Contains("scara") ||
                                          lower.Contains("стенд");
                    if (!isRobotOrStand) continue;

                    // Корень стенда измеряем целиком, отдельно — робота внутри него.
                    RobotController[] robots = root.GetComponentsInChildren<RobotController>(true);
                    for (int r = 0; r < robots.Length; r++)
                    {
                        if (robots[r] == null) continue;
                        Transform robotRoot = RootOf(robots[r].transform);
                        Vector3 size = MeasureSize(robotRoot);
                        Vector3 worldScale = robotRoot.lossyScale;
                        sb.AppendLine(string.Format(culture,
                            "  объект «{0}» ({1}) | высота {2:0.000} | ширина {3:0.000} | глубина {4:0.000} м | " +
                            "мировой масштаб {5:0.000}; {6:0.000}; {7:0.000}",
                            robotRoot.name, robots[r].GetType().Name, size.y, size.x, size.z,
                            worldScale.x, worldScale.y, worldScale.z));
                        measured++;
                    }
                }
                sb.AppendLine("  ИТОГ: измерено роботов в сцене: " + measured);
            }
            finally
            {
                UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true);
            }

            Debug.Log(sb.ToString());
        }

        private static Transform RootOf(Transform t)
        {
            Transform current = t;
            while (current.parent != null && current.parent.parent != null) current = current.parent;
            return current;
        }

        private static Vector3 MeasureSize(Transform root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return Vector3.zero;
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds.size;
        }
    }
}
#endif
