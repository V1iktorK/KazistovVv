using System;
using System.Collections.Generic;
using System.Text;
using KazistovVvXr;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.XR.OpenXR.Features;
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

        /// <summary>Простой профиль Khronos: рантайм PICO его принимает (включён в §31).</summary>
        public const string SimpleControllerProfile = "KHRSimpleControllerProfile";

        /// <summary>
        /// НАШ ПРОФИЛЬ КОНТРОЛЛЕРОВ PICO (§34). Найдено на живом шлеме 15.10.2026: рантайм
        /// называет текущим профилем руки `/interaction_profiles/bytedance/pico4s_controller`,
        /// а в пакете Unity OpenXR профилей PICO нет вовсе. Поэтому привязка `khr/simple_controller`
        /// из §31 устройства НЕ создавала: Unity создаёт устройство только под тот профиль,
        /// который рантайм назвал текущим. Контроллеры при этом были живы — система шлема
        /// печатала их позы.
        /// </summary>
        public const string PicoControllerProfile = "KvPicoControllerProfile";

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
        /// ВКЛЮЧИТЬ ПРОФИЛИ КОНТРОЛЛЕРОВ ДЛЯ ANDROID.
        ///
        /// ПОЧЕМУ ПРОСТОЙ ПРОФИЛЬ. В проекте для Android были включены профили Meta, а простой профиль
        /// Khronos (`KHRSimpleControllerProfile`) — выключен. Профиль Oculus Touch остаётся
        /// включённым намеренно: рантайм PICO реализует его для совместимости с приложениями
        /// под Quest — это описание контроллера в OpenXR, а не SDK платформы, поэтому решение
        /// [R3] («только OpenXR, PICO SDK не ставим») не нарушается.
        ///
        /// ПОЧЕМУ ДОБАВЛЕН СВОЙ ПРОФИЛЬ (§34). §31 включил Khronos Simple, и на живом шлеме
        /// выяснилось, что этого НЕ ХВАТАЕТ: рантайм сообщает текущим профилем руки
        /// `/interaction_profiles/bytedance/pico4s_controller`, а Unity создаёт устройство ввода
        /// только под названный профиль. Привязка khr/simple_controller проходила, устройства
        /// не было: `[KvXr] XR-ввод: шлем есть · правый нет · левый нет`. Подробности — в
        /// `KvPicoControllerProfile`.
        ///
        /// ЧТО ЗНАЧИТ «ДЛЯ ANDROID». Состояние фичи хранится ПО ПЛАТФОРМЕ: включённая для
        /// Standalone фича в APK не попадает. Поэтому правится набор именно Android-группы.
        /// </summary>
        [MenuItem(MenuRoot + "6. Устройство: профиль контроллеров PICO", false, 71)]
        public static void ApplyControllerProfile()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvDevice] === ПРОФИЛЬ КОНТРОЛЛЕРОВ ДЛЯ ANDROID ===");

            string[] wanted = { SimpleControllerProfile, PicoControllerProfile };

            // НОВАЯ ФИЧА СНАЧАЛА ДОЛЖНА ПОПАСТЬ В АССЕТ НАСТРОЕК. Список фич OpenXR хранится
            // в ассете (`Assets/XR/Settings/OpenXR Package Settings.asset`) и пополняется
            // обновлением списка: в batchmode само это НЕ происходит. Проверено 15.10.2026 —
            // фича собралась, но в списке для Android её не было вовсе, и включить было нечего.
            // Штатный способ вызвать обновление — `FeatureHelpers.RefreshFeatures`; им пользуются
            // и примеры самого пакета OpenXR.
            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);

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

                bool isProfile = feature is KvPicoControllerProfile ||
                                 feature.name.IndexOf("ControllerProfile", StringComparison.OrdinalIgnoreCase) >= 0;

                if (isProfile)
                    sb.AppendLine("  профиль «" + feature.name + "» (" + feature.GetType().Name + "): " +
                                  (feature.enabled ? "включён" : "выключен"));

                if (!IsWantedProfile(feature, wanted) || feature.enabled) continue;

                feature.enabled = true;
                EditorUtility.SetDirty(feature);
                changed = true;
                sb.AppendLine("  ВКЛЮЧЁН: " + feature.name + " (" + feature.GetType().Name + ")");
            }

            if (changed) AssetDatabase.SaveAssets();

            // ПЕРЕЧИТЫВАНИЕ: подтверждаем фактом, а не намерением.
            OpenXRFeature[] after = OpenXRSettings
                .GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)
                .GetFeatures<OpenXRFeature>();

            foreach (string name in wanted)
            {
                bool found = false;
                bool on = false;
                if (after != null)
                {
                    foreach (OpenXRFeature feature in after)
                    {
                        if (feature == null) continue;
                        if (!IsWantedProfile(feature, new[] { name })) continue;
                        found = true;
                        on = feature.enabled;
                        break;
                    }
                }

                sb.AppendLine("  ИТОГ: " + name +
                              (on ? " ВКЛЮЧЁН для Android (подтверждено)"
                                  : found ? " НЕ ВКЛЮЧИЛСЯ — проверьте вручную"
                                          : " НЕ НАЙДЕН в настройках OpenXR для Android"));
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// ВЫКЛЮЧИТЬ ЧУЖИЕ ПРОФИЛИ ВЗАИМОДЕЙСТВИЯ ДЛЯ ANDROID (сессия §36).
        ///
        /// ПОЧЕМУ ЭТО ВООБЩЕ НУЖНО. В наборе правил собственного SDK PICO есть прямая проверка:
        /// «Only the PICO Touch Interaction Profile is supported right now» — и она требует, чтобы
        /// рядом с профилями PICO (`PICO4UltraControllerProfile`, `PICO4ControllerProfile`,
        /// `PICONeo3ControllerProfile`, `PICOG3ControllerProfile`) и разрешёнными
        /// (`KHRSimpleControllerProfile`, `EyeGazeInteraction`, `HandInteractionProfile`,
        /// `PalmPoseInteraction`) НЕ БЫЛО ВКЛЮЧЕНО НИ ОДНОГО другого профиля.
        /// У нас для Android включён `OculusTouchControllerProfile` — единственный «чужой».
        ///
        /// ЗАЧЕМ ПРОБОВАТЬ. Журнал §36: расширение `XR_BD_controller_interaction` выдано,
        /// привязки нашего профиля приняты (`attach_action_sets … bytedance/pico4s_controller`),
        /// но `xrGetCurrentInteractionProfile` для рук возвращает ПУСТО — и Unity не создаёт
        /// устройство ввода. Лишний чужой профиль в этом же наборе — единственное отличие нашего
        /// набора от того, что PICO считает правильным. Проверка дешёвая: один прогон.
        ///
        /// ЧЕГО ЭТО НЕ ДЕЛАЕТ: не трогает наш профиль и Khronos Simple, не меняет Standalone
        /// (на компьютере остаются профили, нужные для отладки без шлема).
        /// </summary>
        [MenuItem(MenuRoot + "7. Устройство: выключить чужие профили контроллеров", false, 72)]
        public static void DisableForeignControllerProfiles()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[KvDevice] === ЧУЖИЕ ПРОФИЛИ КОНТРОЛЛЕРОВ ДЛЯ ANDROID ===");

            FeatureHelpers.RefreshFeatures(BuildTargetGroup.Android);
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            if (settings == null)
            {
                Debug.LogError("[KvDevice] нет настроек OpenXR для Android — ничего не изменено.");
                return;
            }

            OpenXRFeature[] before = settings.GetFeatures<OpenXRFeature>();
            int disabled = 0;
            if (before != null)
            {
                foreach (OpenXRFeature feature in before)
                {
                    if (feature == null || !feature.enabled) continue;

                    string typeName = feature.GetType().Name;
                    bool isControllerProfile = typeName.IndexOf("ControllerProfile", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!isControllerProfile) continue;
                    if (feature is KvPicoControllerProfile) continue;          // наш — оставляем
                    if (IsWantedProfile(feature, new[] { SimpleControllerProfile })) continue;   // Khronos разрешён

                    feature.enabled = false;
                    EditorUtility.SetDirty(feature);
                    disabled++;
                    sb.AppendLine("  ВЫКЛЮЧЕН: " + feature.name + " (" + typeName + ")");
                }
            }

            if (disabled > 0) AssetDatabase.SaveAssets();

            OpenXRFeature[] after = OpenXRSettings
                .GetSettingsForBuildTargetGroup(BuildTargetGroup.Android)
                .GetFeatures<OpenXRFeature>();

            sb.AppendLine("  выключено профилей: " + disabled);
            if (after != null)
            {
                foreach (OpenXRFeature feature in after)
                {
                    if (feature == null || !feature.enabled) continue;
                    string typeName = feature.GetType().Name;
                    if (typeName.IndexOf("ControllerProfile", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    sb.AppendLine("  ОСТАЛСЯ ВКЛЮЧЁННЫМ: " + feature.name + " (" + typeName + ")");
                }
            }

            Debug.Log(sb.ToString());
        }

        /// <summary>
        /// Подходит ли фича под один из нужных профилей. Наш профиль узнаётся ПО ТИПУ, а не по
        /// имени: `name` у фичи OpenXR — имя ассета, и оно может оказаться не именем класса
        /// (у своих фич это «PICO 4 Ultra (ByteDance) Controller Profile»), а сравнение по типу
        /// ошибиться не может.
        /// </summary>
        private static bool IsWantedProfile(OpenXRFeature feature, string[] wanted)
        {
            if (feature == null) return false;

            for (int i = 0; i < wanted.Length; i++)
            {
                string name = wanted[i];
                if (string.IsNullOrEmpty(name)) continue;

                if (name == PicoControllerProfile && feature is KvPicoControllerProfile) return true;
                if (feature.name != null &&
                    feature.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0) return true;
            }
            return false;
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
                        if (f != null && f.enabled) enabled.Add(f.name + " (" + f.GetType().Name + ")");
            }

            sb.AppendLine("  OpenXR-фичи для Android (" + enabled.Count + "):");
            foreach (string name in enabled) sb.AppendLine("     " + name);
            Debug.Log(sb.ToString());
        }
    }
}
