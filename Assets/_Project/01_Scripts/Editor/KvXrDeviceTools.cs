using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// НАСТРОЙКИ ПРИЛОЖЕНИЯ ДЛЯ ЖИВОГО УСТРОЙСТВА (сессия §31: шлем PICO 4 Ultra приехал).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ИНСТРУМЕНТ. Эти настройки нельзя «посмотреть глазами» в редакторе так,
    /// чтобы понять, что попадёт в APK: идентификатор приложения лежит в проектном файле, а
    /// состояние OpenXR-фич зависит от ПЛАТФОРМЫ (включённое для Standalone в сборку под
    /// Android не попадает). Ошибка здесь выглядит так: приложение поставилось, запустилось,
    /// но в шлеме не работают контроллеры — и непонятно, почему.
    ///
    /// ПОДТВЕРЖДЕНИЕ ФАКТА, А НЕ НАМЕРЕНИЯ. Оба метода печатают состояние ДО и ПОСЛЕ и
    /// перечитывают значение — как в §29 с глобальным умолчанием графики: правка считается
    /// сделанной только если её видно в сохранённом файле.
    ///
    /// ВНИМАНИЕ К ИМЕНИ ТИПА. Файл воссоздан после потери копии из `Temp\_s31_ready`
    /// (та копия исчезла при аварийном завершении сборки §30). Меню и имена методов
    /// повторяют прежние — на них ссылается `Tools\_stage_a\СОСТОЯНИЕ_ЭТАПА_А.md`.
    /// </summary>
    public static class KvXrDeviceTools
    {
        private const string MenuRoot = "Tools/KazistovVv XR/";

        /// <summary>Идентификатор приложения проекта (решение владельца, §31).</summary>
        public const string OwnIdentifier = "com.KazistovVv.app";

        /// <summary>Профиль контроллеров, понятный рантайму PICO.</summary>
        public const string SimpleControllerProfile = "KHRSimpleControllerProfile";

        // ==================================================== ИДЕНТИФИКАТОР ПРИЛОЖЕНИЯ

        /// <summary>
        /// ПОСТАВИТЬ СВОЙ ИДЕНТИФИКАТОР ПРИЛОЖЕНИЯ вместо значения шаблона Unity.
        ///
        /// ПОЧЕМУ ЭТО НЕ «ДЛЯ КРАСОТЫ». С шаблонным `com.UnityTechnologies.com.unity.template.
        /// urpblank` приложение в шлеме называется чужим именем, и отличить его от другого проекта,
        /// собранного из того же шаблона, невозможно.
        ///
        /// ИСПОЛЬЗУЕТСЯ НЕУСТАРЕВШИЙ API (`NamedBuildTarget`): старый `BuildTargetGroup`-вариант
        /// компилятор помечает как obsolete, а гейт проекта требует ноль предупреждений CS.
        /// </summary>
        [MenuItem(MenuRoot + "5. Устройство: идентификатор приложения", false, 70)]
        public static void ApplyOwnIdentifier()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvDevice] === ИДЕНТИФИКАТОР ПРИЛОЖЕНИЯ ===");

            NamedBuildTarget[] targets = { NamedBuildTarget.Android, NamedBuildTarget.Standalone };
            foreach (NamedBuildTarget target in targets)
            {
                string before = PlayerSettings.GetApplicationIdentifier(target);
                if (before == OwnIdentifier)
                {
                    sb.AppendLine("  " + target + ": уже " + before + " — правка не нужна");
                    continue;
                }

                PlayerSettings.SetApplicationIdentifier(target, OwnIdentifier);
                string after = PlayerSettings.GetApplicationIdentifier(target);
                sb.AppendLine("  " + target + ": " + before + " → " + after +
                              (after == OwnIdentifier ? " · ПРИМЕНЕНО" : " · НЕ ПРИМЕНИЛОСЬ"));
            }

            AssetDatabase.SaveAssets();
            Debug.Log(sb.ToString());
        }

        // ==================================================== ПРОФИЛЬ КОНТРОЛЛЕРОВ

        /// <summary>
        /// ВКЛЮЧИТЬ ПРОСТОЙ ПРОФИЛЬ КОНТРОЛЛЕРОВ ДЛЯ ANDROID.
        ///
        /// ПОЧЕМУ ИМЕННО ОН. В проекте для Android были включены профили Meta, а простой профиль
        /// Khronos (`KHRSimpleControllerProfile`) — выключен. Профиль Oculus Touch остаётся
        /// включённым намеренно: рантайм PICO реализует его для совместимости с приложениями
        /// под Quest — это описание контроллера в OpenXR, а не SDK платформы, поэтому решение
        /// [R3] («только OpenXR, PICO SDK не ставим») не нарушается.
        ///
        /// ЧТО ЗНАЧИТ «ДЛЯ ANDROID». Состояние фичи хранится ПО ПЛАТФОРМЕ: включённая для
        /// Standalone фича в APK не попадает. Поэтому правится набор именно Android-группы.
        /// </summary>
        [MenuItem(MenuRoot + "6. Устройство: профиль контроллеров PICO", false, 71)]
        public static void ApplyControllerProfile()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvDevice] === ПРОФИЛЬ КОНТРОЛЛЕРОВ ДЛЯ ANDROID ===");

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[KvDevice] нет настроек OpenXR для Android — профиль не изменён.");
                return;
            }

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            if (features == null || features.Length == 0)
            {
                Debug.LogError("[KvDevice] пакет OpenXR не вернул ни одной фичи — профиль не изменён.");
                return;
            }

            bool changed = false;
            foreach (OpenXRFeature feature in features)
            {
                if (feature == null) continue;

                bool isProfile = feature.name.IndexOf("ControllerProfile", StringComparison.OrdinalIgnoreCase) >= 0;
                bool isWanted = feature.name.IndexOf(SimpleControllerProfile, StringComparison.OrdinalIgnoreCase) >= 0;

                if (isProfile)
                    sb.AppendLine("  профиль «" + feature.name + "»: " + (feature.enabled ? "включён" : "выключен"));

                if (!isWanted || feature.enabled) continue;

                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                changed = true;
                sb.AppendLine("  ВКЛЮЧЁН: " + feature.name);
            }

            if (changed) AssetDatabase.SaveAssets();

            // ПЕРЕЧИТЫВАНИЕ: подтверждаем фактом, а не намерением.
            bool confirmed = false;
            OpenXRFeature[] after = OpenXRSettings
                .GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)
                .GetFeatures<OpenXRFeature>();
            if (after != null)
            {
                foreach (OpenXRFeature feature in after)
                {
                    if (feature == null) continue;
                    if (feature.name.IndexOf(SimpleControllerProfile, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    confirmed = feature.enabled;
                    break;
                }
            }

            sb.AppendLine("  ИТОГ: " + SimpleControllerProfile +
                          (confirmed ? " ВКЛЮЧЁН для Android (подтверждено)" : " НЕ ВКЛЮЧИЛСЯ — проверьте вручную"));
            Debug.Log(sb.ToString());
        }

        // ==================================================== ОТЧЁТ

        /// <summary>Печатает всё, что нужно знать перед установкой в шлем.</summary>
        [MenuItem(MenuRoot + "7. Устройство: что попадёт в APK", false, 72)]
        public static void ReportDeviceReadiness()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvDevice] === ЧТО ПОПАДЁТ В APK ===");
            sb.AppendLine("  идентификатор Android:   " +
                          PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android));
            sb.AppendLine("  идентификатор компьютер: " +
                          PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Standalone));
            sb.AppendLine("  min / target SDK:        " +
                          PlayerSettings.Android.minSdkVersion + " / " + PlayerSettings.Android.targetSdkVersion);
            sb.AppendLine("  архитектуры:             " + PlayerSettings.Android.targetArchitectures);
            sb.AppendLine("  графический API:         " +
                          string.Join(", ", Array.ConvertAll(
                              PlayerSettings.GetGraphicsAPIs(BuildTarget.Android), a => a.ToString())));
            sb.AppendLine("  сцен в сборке:           " + EditorBuildSettings.scenes.Length);

            List<string> enabled = new List<string>();
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings != null)
            {
                OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
                if (features != null)
                    foreach (OpenXRFeature f in features)
                        if (f != null && f.enabled) enabled.Add(f.name);
            }

            sb.AppendLine("  OpenXR-фичи для Android (" + enabled.Count + "):");
            foreach (string name in enabled) sb.AppendLine("     " + name);
            Debug.Log(sb.ToString());
        }
    }
}
