#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using KazistovVvXr;
using KazistovVvFeatures;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ИНСТРУМЕНТЫ СЕССИИ §37: КИСТИ (ПАКЕТ И ФИЧА), ПРОВЕРКА МЕША КОНТРОЛЛЕРОВ, СВОДКА СОСТОЯНИЯ.
    ///
    /// ПОЧЕМУ ПАКЕТ СТАВИТСЯ ЧЕРЕЗ РЕДАКТОР, А НЕ ПРАВКОЙ `manifest.json`. Штатный менеджер пакетов
    /// (`Client.Add`) сам разрешает зависимости, скачивает пакет и пересобирает `Library`; правка
    /// файла руками оставила бы проект в состоянии «в манифесте есть, в кэше нет» — и сборка
    /// упала бы на этапе импорта, а не на понятной строке журнала.
    ///
    /// ПОЧЕМУ ФИЧА КИСТЕЙ ИЩЕТСЯ ПО ИМЕНИ ТИПА, А НЕ ПО ССЫЛКЕ НА ТИП. Пока пакет не установлен,
    /// ссылка на `UnityEngine.XR.Hands.OpenXR.HandTracking` не дала бы даже скомпилировать этот
    /// файл — инструмент установки пакета обязан собираться ДО пакета. Поэтому поиск идёт по
    /// строке имени типа: так же в проекте уже сделана проверка OpenXR-лоадера в `KvXrRig`.
    /// </summary>
    public static class KvXrS37Tools
    {
        /// <summary>Пакет отслеживания кистей: официальный пакет Unity, не вендорский SDK.</summary>
        public const string HandsPackage = "com.unity.xr.hands";
        public const string HandsPackageVersion = "1.9.0";

        /// <summary>Имя типа фичи кистей (провайдер OpenXR из пакета XR Hands).</summary>
        public const string HandTrackingFeatureName = "HandTracking";

        private const string MenuRoot = "KazistovVv/Устройство/";

        // ==================================================== 1. ПАКЕТ КИСТЕЙ

        [MenuItem(MenuRoot + "10. Кисти: поставить пакет XR Hands", false, 90)]
        public static void AddHandTrackingPackage()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvXrS37] === ПАКЕТ ОТСЛЕЖИВАНИЯ КИСТЕЙ ===");

            string installed;
            if (TryGetInstalledVersion(HandsPackage, out installed))
            {
                sb.AppendLine("  пакет уже стоит: " + HandsPackage + " " + installed);
                Debug.Log(sb.ToString());
                return;
            }

            string id = HandsPackage + "@" + HandsPackageVersion;
            sb.AppendLine("  ставим: " + id);
            var request = UnityEditor.PackageManager.Client.Add(id);

            DateTime deadline = DateTime.Now.AddMinutes(20);
            while (request != null && !request.IsCompleted && DateTime.Now < deadline)
                System.Threading.Thread.Sleep(500);

            if (request == null)
            {
                sb.AppendLine("  ОШИБКА: запрос не создан");
                Debug.LogError(sb.ToString());
                return;
            }
            if (!request.IsCompleted)
            {
                sb.AppendLine("  ОШИБКА: таймаут установки пакета");
                Debug.LogError(sb.ToString());
                return;
            }

            var result = request.Result;
            if (request.Status == UnityEditor.PackageManager.StatusCode.Success && result != null)
            {
                sb.AppendLine("  поставлен: " + result.packageId);
                sb.AppendLine("  версия: " + result.version);
                sb.AppendLine("  зависимости пакета: " + DescribeDependencies(result));
            }
            else
            {
                string error = request.Error != null ? request.Error.message : "без описания";
                sb.AppendLine("  ОШИБКА установки: " + error);
            }

            AssetDatabase.Refresh();
            Debug.Log(sb.ToString());
        }

        // ==================================================== 2. ФИЧА КИСТЕЙ ДЛЯ ANDROID

        /// <summary>
        /// ВКЛЮЧИТЬ ОТСЛЕЖИВАНИЕ КИСТЕЙ ДЛЯ ANDROID.
        ///
        /// ЧТО ДЕЛАЕТСЯ И ЗАЧЕМ:
        ///   1. включается фича `HandTracking` (пакет XR Hands) — она объявляет расширение
        ///      `XR_EXT_hand_tracking` и создаёт подсистему кистей;
        ///   2. в `PICOProjectSetting` ставится `isHandTracking = true` — только тогда хук SDK PICO
        ///      (`PICOModifyAndroidManifest`) допишет в манифест `handtracking = 1` и разрешение
        ///      `com.picovr.permission.HAND_TRACKING`;
        ///   3. режим поддержки — «контроллеры И кисти»: владелец кладёт контроллеры и видит кисти,
        ///      а с контроллерами в руках продолжает работать луч ([S1.4]).
        /// </summary>
        [MenuItem(MenuRoot + "11. Кисти: включить HandTracking для Android", false, 91)]
        public static void ApplyHandTrackingForAndroid()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvXrS37] === ОТСЛЕЖИВАНИЕ КИСТЕЙ ДЛЯ ANDROID ===");

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[KvXrS37] нет настроек OpenXR для Android — ничего не изменено.");
                return;
            }

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            bool found = false;
            bool enabledNow = false;
            for (int i = 0; i < features.Length; i++)
            {
                OpenXRFeature feature = features[i];
                if (feature == null) continue;
                string typeName = feature.GetType().Name;

                // ЧУЖИЕ ФИЧИ КИСТЕЙ ВЫКЛЮЧАЮТСЯ. `MetaHandTrackingAim` требует расширения Meta
                // (`XR_FB_hand_tracking_aim`), которого у PICO нет: оно попало в список только
                // потому, что в имени тоже есть «HandTracking». Лишняя запрошенная расширенная
                // возможность — лишний повод рантайму отказать, поэтому она выключается явно.
                if (typeName.IndexOf("Meta", StringComparison.Ordinal) >= 0 && feature.enabled)
                {
                    feature.enabled = false;
                    EditorUtility.SetDirty(feature);
                    sb.AppendLine("  ВЫКЛЮЧЕНА чужая фича кистей: «" + feature.name + "» (" + typeName + ")");
                    continue;
                }

                if (typeName.IndexOf(HandTrackingFeatureName, StringComparison.Ordinal) < 0) continue;

                found = true;
                if (!feature.enabled)
                {
                    feature.enabled = true;
                    EditorUtility.SetDirty(feature);
                    enabledNow = true;
                }
                sb.AppendLine("  фича кистей: «" + feature.name + "» (" + typeName + ") — " +
                              (enabledNow ? "ВКЛЮЧЕНА" : "уже включена"));
            }

            if (!found)
            {
                sb.AppendLine("  ОШИБКА: фича «" + HandTrackingFeatureName + "» не найдена — " +
                              "пакет " + HandsPackage + " не установлен?");
                Debug.LogError(sb.ToString());
                return;
            }

            // Настройки проекта PICO: без isHandTracking SDK не пишет мета-данные кистей.
            try
            {
                Unity.XR.OpenXR.Features.PICOSupport.PICOProjectSetting config =
                    Unity.XR.OpenXR.Features.PICOSupport.PICOProjectSetting.GetProjectConfig();
                if (config != null)
                {
                    config.isHandTracking = true;
                    config.handTrackingSupportType =
                        Unity.XR.PXR.HandTrackingSupport.ControllersAndHands;
                    EditorUtility.SetDirty(config);
                    AssetDatabase.SaveAssets();
                    sb.AppendLine("  PICOProjectSetting: isHandTracking = true · режим «контроллеры и кисти»");
                }
                else
                {
                    sb.AppendLine("  ВНИМАНИЕ: PICOProjectSetting не найден в Resources — мета-данные кистей могут не попасть в манифест");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("  ВНИМАНИЕ: PICOProjectSetting не настроен (" + e.GetType().Name + ": " + e.Message + ")");
            }

            // Строка-доказательство: какие расширения OpenXR теперь объявлены для Android.
            sb.AppendLine("  расширения OpenXR для Android: " + DescribeOpenXrExtensions(settings));

            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        // ==================================================== 3. ПРОВЕРКА МЕША КОНТРОЛЛЕРОВ

        /// <summary>
        /// ПРОВЕРКА МЕША КОНТРОЛЛЕРОВ БЕЗ ШЛЕМА. Отвечает на три вопроса, которые иначе выяснялись
        /// бы в шлеме: загружается ли префаб SDK по нашему пути, какой у него габарит (не пришёл ли
        /// он в сантиметрах) и на каких шейдерах собраны материалы (розовое или нет).
        /// </summary>
        [MenuItem(MenuRoot + "12. Контроллеры: проверить меш SDK", false, 92)]
        public static void VerifySdkControllerModels()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvXrS37] === МЕШ КОНТРОЛЛЕРОВ ИЗ SDK ===");

            VerifyOneModel(KvXrQuickHand.Right, sb);
            VerifyOneModel(KvXrQuickHand.Left, sb);

            sb.AppendLine("  наш материал: " + (KvRenderPipelineShaders.FindLit() != null
                ? KvRenderPipelineShaders.FindLit().name
                : "НЕ НАЙДЕН (будет использован Unlit)"));
            Debug.Log(sb.ToString());
        }

        private static void VerifyOneModel(KvXrQuickHand hand, StringBuilder sb)
        {
            string path = KvXrControllerVisuals.SdkModelPath(hand);
            string side = hand == KvXrQuickHand.Left ? "левая" : "правая";
            GameObject prefab = null;
            try { prefab = Resources.Load<GameObject>(path); }
            catch (Exception e) { sb.AppendLine("  " + side + ": загрузка бросила " + e.GetType().Name); }

            if (prefab == null)
            {
                sb.AppendLine("  " + side + ": ПРЕФАБ НЕ НАЙДЕН по пути «" + path + "»");
                return;
            }

            GameObject instance = null;
            try
            {
                instance = UnityEngine.Object.Instantiate(prefab);
                instance.name = "probe_" + hand;

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
                Animator[] animators = instance.GetComponentsInChildren<Animator>(true);

                Bounds bounds = new Bounds();
                int meshes = 0;
                bool hasBounds = false;
                var shaders = new System.Collections.Generic.List<string>();
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null) continue;
                    if (renderers[i] is MeshRenderer || renderers[i] is SkinnedMeshRenderer) meshes++;
                    if (!hasBounds) { bounds = renderers[i].bounds; hasBounds = true; }
                    else bounds.Encapsulate(renderers[i].bounds);

                    Material[] materials = renderers[i].sharedMaterials;
                    for (int k = 0; k < materials.Length; k++)
                    {
                        if (materials[k] == null) continue;
                        string shaderName = materials[k].shader != null ? materials[k].shader.name : "нет шейдера";
                        if (!shaders.Contains(shaderName)) shaders.Add(shaderName);
                    }
                }

                float size = hasBounds ? Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z)) : 0f;
                sb.AppendLine("  " + side + ": загружен «" + prefab.name + "» · рендереров " + renderers.Length +
                              " · мешей " + meshes + " · коллайдеров " + colliders.Length +
                              " · аниматоров " + animators.Length);
                sb.AppendLine("    габарит " + size.ToString("0.000") + " м (" +
                              (size >= KvXrControllerVisuals.MinPlausibleSizeMeters &&
                               size <= KvXrControllerVisuals.MaxPlausibleSizeMeters
                                  ? "похож на контроллер — годится"
                                  : "НЕ похож на контроллер — будет откат на примитивы") + ")");
                sb.AppendLine("    шейдеры материалов SDK: " + (shaders.Count > 0 ? string.Join(", ", shaders.ToArray()) : "нет"));
                sb.AppendLine("    совместимость с текущим конвейером: " + KvRenderPipelineShaders.Describe());
            }
            catch (Exception e)
            {
                sb.AppendLine("  " + side + ": проверка не удалась (" + e.GetType().Name + ": " + e.Message + ")");
            }
            finally
            {
                if (instance != null) UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        // ==================================================== 4. СВОДКА СОСТОЯНИЯ §37

        /// <summary>
        /// СВОДКА БЕЗ ШЛЕМА: версия пакета кистей, включена ли фича, какие числа сейчас действуют
        /// (рука луча, радиус панели, визуал контроллеров, жесты), хеши сцен. Одна строка на предмет.
        /// </summary>
        public static void ReportS37State()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvXrS37] === СОСТОЯНИЕ §37 (без шлема) ===");

            string version;
            sb.AppendLine("  пакет кистей " + HandsPackage + ": " +
                          (TryGetInstalledVersion(HandsPackage, out version) ? version : "НЕ УСТАНОВЛЕН"));

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            bool handsFeature = false;
            if (settings != null)
            {
                OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
                for (int i = 0; i < features.Length; i++)
                {
                    if (features[i] == null) continue;
                    if (features[i].GetType().Name.IndexOf(HandTrackingFeatureName, StringComparison.Ordinal) >= 0 &&
                        features[i].enabled)
                    {
                        handsFeature = true;
                        sb.AppendLine("  фича кистей: ВКЛЮЧЕНА («" + features[i].name + "»)");
                    }
                }
                sb.AppendLine("  расширения OpenXR для Android: " + DescribeOpenXrExtensions(settings));
            }
            if (!handsFeature) sb.AppendLine("  фича кистей: НЕ включена");

            try
            {
                Unity.XR.OpenXR.Features.PICOSupport.PICOProjectSetting config =
                    Unity.XR.OpenXR.Features.PICOSupport.PICOProjectSetting.GetProjectConfig();
                sb.AppendLine("  PICOProjectSetting.isHandTracking: " + (config != null && config.isHandTracking ? "да" : "НЕТ"));
            }
            catch (Exception)
            {
                sb.AppendLine("  PICOProjectSetting: недоступен");
            }

            sb.AppendLine("  " + KvXrSettings.DescribePointer());
            sb.AppendLine("  рука панели: " + (KvXrSettings.GetQuickPanelHand() == KvXrQuickHand.Left ? "левая" : "правая"));
            sb.AppendLine("  визуал контроллеров: " +
                          KvXrControllerVisuals.ModeName(
                              KvXrControllerVisuals.ModeFromIndex(KvXrSettings.GetControllerVisualMode())));
            sb.AppendLine("  жесты: " + (KvXrSettings.GetGesturesEnabled() ? "включены" : "выключены"));
            sb.AppendLine("  размеры панели при текущем радиусе: " +
                          KvXrGroupPanelLayout.DescribeSizes(8, KvXrGroupPanelLayout.GroupSpanDegrees,
                              KvXrSettings.GetGroupPanelRadius()) +
                          " · веер: " + KvXrGroupPanelLayout.FanRadiusMeters(14, KvXrSettings.GetGroupPanelRadius())
                          .ToString("0.000") + " м");
            sb.AppendLine("  файл подстройки: " + KvXrTuning.FilePath);

            Debug.Log(sb.ToString());
        }

        // ==================================================== СЛУЖЕБНОЕ

        private static bool TryGetInstalledVersion(string packageName, out string version)
        {
            version = null;
            try
            {
                UnityEditor.PackageManager.PackageInfo info =
                    UnityEditor.PackageManager.PackageInfo.FindForPackageName(packageName);
                if (info == null) return false;
                version = info.version;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static string DescribeDependencies(UnityEditor.PackageManager.PackageInfo info)
        {
            if (info == null || info.dependencies == null || info.dependencies.Length == 0) return "нет";
            var sb = new StringBuilder();
            for (int i = 0; i < info.dependencies.Length; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(info.dependencies[i].name).Append(' ').Append(info.dependencies[i].version);
            }
            return sb.ToString();
        }

        private static string DescribeOpenXrExtensions(OpenXRSettings settings)
        {
            if (settings == null) return "настроек нет";

            // ПОЧЕМУ ПО ИМЕНАМ ФИЧ, А НЕ ПО СТРОКАМ РАСШИРЕНИЙ. Список расширений
            // (`openxrExtensionStrings`) в редакторе недоступен как свойство экземпляра — он живёт
            // в атрибуте класса и попадает в манифест при сборке. Поэтому здесь печатается то, что
            // можно проверить честно: КАКИЕ ФИЧИ ВКЛЮЧЕНЫ. Расширения, которые рантайм реально
            // выдал, видны только на устройстве — строкой `KvXrOpenXrDiagnostics` в журнале шлема.
            var names = new System.Collections.Generic.List<string>();
            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            for (int i = 0; i < features.Length; i++)
            {
                if (features[i] == null || !features[i].enabled) continue;
                names.Add(features[i].GetType().Name);
            }
            return names.Count + " (" + string.Join(", ", names.ToArray()) + ")";
        }
    }
}
#endif
