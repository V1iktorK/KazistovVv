#if UNITY_EDITOR
using System;
using System.Text;
using UnityEditor;
using UnityEditor.XR.OpenXR.Features;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using KazistovVvXr;

namespace KazistovVvXrEditor
{
    /// <summary>
    /// ПЕРЕХОД НА PICO OPENXR SDK (решение владельца от 20.10.2026, снимает прежний запрет [R3]).
    ///
    /// ПОЧЕМУ РЕШЕНИЕ ПРИНЯТО. Проверено на живом шлеме: магазинное приложение (Angry Birds VR)
    /// контроллеры получает, а наше — нет. `xrGetCurrentInteractionProfile` для обеих рук
    /// возвращает `XR_NULL_PATH`, при том что расширение `XR_BD_controller_interaction` выдано,
    /// привязки приняты, профиль при сборке действий выбирается наш, нарушений правила по рукам 0.
    /// Владелец разрешил поставить вендорский SDK — единственную непроверенную гипотезу.
    ///
    /// ЧТО ВКЛЮЧАЕТСЯ:
    ///   • `PICOFeature` — фича PICO: свой загрузчик рантайма для Android и правка AndroidManifest
    ///     (в том числе мета-данная `controller = 1` и `pvr.app.type = vr`);
    ///   • `PICO4UltraControllerProfile` — ОФИЦИАЛЬНЫЙ профиль контроллеров PICO 4 Ultra.
    ///
    /// ЧТО ВЫКЛЮЧАЕТСЯ:
    ///   • наш `KvPicoControllerProfile` — собственные правила PICO («Only the PICO Touch
    ///     Interaction Profile is supported right now», `error = true`) не дают собрать APK,
    ///     если рядом со своим профилем включён чужой/самодельный. Два профиля на один и тот же
    ///     путь взаимодействия к тому же спорили бы за создание одного и того же устройства.
    ///
    /// ФАЙЛ НАШЕГО ПРОФИЛЯ ОСТАЁТСЯ в проекте: он документирует, что путь профиля и расширение
    /// были верны, и его 7 тестов (путь, расширение, состав элементов устройства) продолжают
    /// действовать. Меняется ровно один тест — «включён для Android» → «выключен, вместо него
    /// официальный профиль PICO» (разрешение владельца от 20.10.2026).
    /// </summary>
    public static class KvPicoSdkSwitch
    {
        private const string MenuRoot = "KazistovVv/Устройство/";

        /// <summary>Имя официального профиля контроллеров PICO в настройках OpenXR.</summary>
        public const string PicoControllerProfile = "PICO4UltraControllerProfile";

        /// <summary>Имя фичи PICO (загрузчик рантайма и манифест).</summary>
        public const string PicoFeature = "PICOFeature";

        [MenuItem(MenuRoot + "9. Перейти на PICO OpenXR SDK", false, 74)]
        public static void Apply()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvPicoSdk] === ПЕРЕХОД НА PICO OPENXR SDK (Android) ===");

            // Список фич OpenXR хранится в ассете и пополняется только обновлением списка.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[KvPicoSdk] нет настроек OpenXR для Android — ничего не изменено.");
                return;
            }

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            if (features == null || features.Length == 0)
            {
                Debug.LogError("[KvPicoSdk] пакет OpenXR не вернул ни одной фичи — ничего не изменено.");
                return;
            }

            int changed = 0;
            bool picoProfileFound = false;
            bool picoFeatureFound = false;

            foreach (OpenXRFeature feature in features)
            {
                if (feature == null) continue;
                string typeName = feature.GetType().Name;
                string featureName = feature.name ?? string.Empty;

                // 1. Наш профиль — выключить.
                if (feature is KvPicoControllerProfile)
                {
                    if (feature.enabled)
                    {
                        feature.enabled = false;
                        EditorUtility.SetDirty(feature);
                        changed++;
                        sb.AppendLine("  ВЫКЛЮЧЕН наш профиль: " + featureName);
                    }
                    else
                    {
                        sb.AppendLine("  наш профиль уже выключен: " + featureName);
                    }
                    continue;
                }

                // 2. Официальный профиль PICO 4 Ultra — включить.
                if (typeName.IndexOf(PicoControllerProfile, StringComparison.Ordinal) >= 0)
                {
                    picoProfileFound = true;
                    if (!feature.enabled)
                    {
                        feature.enabled = true;
                        EditorUtility.SetDirty(feature);
                        changed++;
                        sb.AppendLine("  ВКЛЮЧЁН официальный профиль: " + featureName + " (" + typeName + ")");
                    }
                    else
                    {
                        sb.AppendLine("  официальный профиль уже включён: " + featureName);
                    }
                    continue;
                }

                // 3. Фича PICO (загрузчик рантайма + манифест) — включить.
                if (typeName.IndexOf(PicoFeature, StringComparison.Ordinal) >= 0 &&
                    typeName.IndexOf("Profile", StringComparison.Ordinal) < 0)
                {
                    picoFeatureFound = true;
                    if (!feature.enabled)
                    {
                        feature.enabled = true;
                        EditorUtility.SetDirty(feature);
                        changed++;
                        sb.AppendLine("  ВКЛЮЧЕНА фича PICO: " + featureName + " (" + typeName + ")");
                    }
                    else
                    {
                        sb.AppendLine("  фича PICO уже включена: " + featureName);
                    }
                }
            }

            if (changed > 0) AssetDatabase.SaveAssets();

            sb.AppendLine("  изменено фич: " + changed +
                          " · официальный профиль найден: " + (picoProfileFound ? "да" : "НЕТ") +
                          " · фича PICO найдена: " + (picoFeatureFound ? "да" : "НЕТ"));
            if (!picoProfileFound || !picoFeatureFound)
                sb.AppendLine("  ВНИМАНИЕ: пакет PICO не дал нужных фич — проверьте зависимость в Packages/manifest.json");

            Debug.Log(sb.ToString());
        }
    }
}
#endif
