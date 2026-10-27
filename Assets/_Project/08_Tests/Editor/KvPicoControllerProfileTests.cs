using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;
using KazistovVvXr;

namespace KazistovVvTests
{
    /// <summary>
    /// ПРОФИЛЬ КОНТРОЛЛЕРОВ PICO (сессия §34, дефект с живого шлема 15.10.2026).
    ///
    /// ЧТО БЫЛО. Владелец держал контроллеры в руках, система их ВИДЕЛА (в журнале шлема:
    /// `logDebugControllerPose,left confidence 2`), а приложение — нет: `правый нет · левый нет`.
    /// Причина найдена по журналу рантайма:
    ///   `xrGetCurrentInteractionProfile → /interaction_profiles/bytedance/pico4s_controller`
    /// — у контроллеров PICO 4 Ultra СВОЙ профиль взаимодействия, а в пакете Unity OpenXR
    /// (`com.unity.xr.openxr`) профилей PICO нет вовсе (там 14 профилей: Khronos, Oculus,
    /// Valve, Microsoft, HP, Meta Quest Touch Plus/Pro — и ни одного ByteDance).
    /// Unity создаёт устройство ввода ТОЛЬКО для того профиля, который рантайм назвал текущим
    /// (`Internal_RegisterDeviceDefinition(userPath, desiredInteractionProfile, …)`), поэтому
    /// «привязка khr/simple_controller» из §31 устройства не создавала.
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ ЗДЕСЬ. Ровно те строки, ошибка в которых стоила бы цикла сборки
    /// (сборка APK — десятки минут, а шлем — единственное место, где дефект виден):
    ///   • путь профиля — посимвольно тот, что назвал рантайм шлема;
    ///   • имя расширения OpenXR — то, которое рантайм объявил (`XR_BD_controller_interaction`);
    ///   • пути ввода — ровно те, что перечислены в профиле PICO ;
    ///   • фича объявлена для Android и просит нужное расширение;
    ///   • фича ВКЛЮЧЕНА в настройках OpenXR для Android (иначе в APK её просто нет);
    ///   • у устройства есть управляющие элементы, которые читает проект (`KvXrInputSource`).
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ. Здесь не проверяется, что шлем реально отдал контроллер: это видно
    /// только в журнале устройства строкой `[KvXr] КОНТРОЛЛЕРЫ ПОЯВИЛИСЬ` — тестами без
    /// шлема такое не проверяется, и делать вид, что проверено, нельзя.
    /// </summary>
    public class KvPicoControllerProfileTests
    {
        /// <summary>
        /// ПУТЬ ПРОФИЛЯ — ГЛАВНАЯ СТРОКА ВСЕГО ДЕЛА. Он взят из журнала рантайма шлема
        /// (`xrGetCurrentInteractionProfile … Path /interaction_profiles/bytedance/pico4s_controller`)
        /// и сверен с исходником профиля PICO 4 Ultra из официального SDK:
        /// `PICO4UltraControllerProfile.cs: public const string profile = "/interaction_profiles/bytedance/pico4s_controller";`
        /// Опечатка здесь = «контроллеры снова не найдены» и ещё одна сборка впустую.
        /// </summary>
        [Test]
        public void ProfilePath_IsExactlyTheOneTheRuntimeReports()
        {
            Assert.AreEqual("/interaction_profiles/bytedance/pico4s_controller",
                KvPicoControllerProfile.profile,
                "путь профиля обязан совпадать с тем, что отдаёт xrGetCurrentInteractionProfile");
        }

        /// <summary>Расширение, которое рантайм объявил (`XR_BD_controller_interaction: Version=2`).</summary>
        [Test]
        public void ExtensionString_IsTheByteDanceControllerInteraction()
        {
            Assert.AreEqual("XR_BD_controller_interaction", KvPicoControllerProfile.extensionString,
                "без верного имени расширения фича отключится рантаймом и устройств не будет");
        }

        /// <summary>Идентификатор фичи — свой и в своём пространстве имён, не как у Unity.</summary>
        [Test]
        public void FeatureId_IsOursAndNamespaced()
        {
            string id = KvPicoControllerProfile.featureId;
            Assert.IsFalse(string.IsNullOrEmpty(id), "идентификатор фичи не может быть пустым");
            StringAssert.StartsWith("com.kazistovvv.", id,
                "идентификаторы Unity начинаются с com.unity.openxr.feature.input.* — свой обязан быть своим");
        }

        /// <summary>
        /// ПУТИ ВВОДА. Каждый — из профиля PICO; проверяются все, которые мы привязываем.
        /// Смысл теста: у профиля есть похожие пути (`/input/trigger/value` и `/input/trigger/click`,
        /// `/input/squeeze/value` и `/input/squeeze/click`) — их легко перепутать, а на шлеме это
        /// выглядит как «курок не нажимается» без единой ошибки в журнале.
        /// </summary>
        [Test]
        public void InputPaths_MatchTheDocumentedPicoProfile()
        {
            var expected = new Dictionary<string, string>
            {
                { "trigger",         "/input/trigger/value" },
                { "triggerClick",    "/input/trigger/click" },
                { "triggerTouch",    "/input/trigger/touch" },
                { "squeeze",         "/input/squeeze/value" },
                { "squeezeClick",    "/input/squeeze/click" },
                { "thumbstick",      "/input/thumbstick" },
                { "thumbstickClick", "/input/thumbstick/click" },
                { "thumbstickTouch", "/input/thumbstick/touch" },
                { "buttonA",         "/input/a/click" },
                { "buttonB",         "/input/b/click" },
                { "buttonX",         "/input/x/click" },
                { "buttonY",         "/input/y/click" },
                { "menu",            "/input/menu/click" },
                { "system",          "/input/system/click" },
                { "grip",            "/input/grip/pose" },
                { "aim",             "/input/aim/pose" },
                { "haptic",          "/output/haptic" },
            };

            foreach (KeyValuePair<string, string> pair in expected)
            {
                FieldInfo field = typeof(KvPicoControllerProfile)
                    .GetField(pair.Key, BindingFlags.Public | BindingFlags.Static);
                Assert.IsNotNull(field, "нет константы пути ввода: " + pair.Key);
                Assert.AreEqual(pair.Value, field.GetValue(null),
                    "путь ввода «" + pair.Key + "» разошёлся с профилем PICO");
            }
        }

        /// <summary>
        /// ФИЧА ОБЪЯВЛЕНА ДЛЯ ANDROID И ПРОСИТ НУЖНОЕ РАСШИРЕНИЕ. Атрибут — единственное место,
        /// где это задаётся; если он без Android, фичи в APK не будет, и никакая правка кода
        /// этого не исправит.
        /// </summary>
        [Test]
        public void FeatureAttribute_DeclaresAndroidAndTheExtension()
        {
            object[] attributes = typeof(KvPicoControllerProfile)
                .GetCustomAttributes(typeof(UnityEditor.XR.OpenXR.Features.OpenXRFeatureAttribute), false);
            Assert.AreEqual(1, attributes.Length, "у фичи обязан быть ровно один атрибут OpenXRFeature");

            var attribute = (UnityEditor.XR.OpenXR.Features.OpenXRFeatureAttribute)attributes[0];
            CollectionAssert.Contains(attribute.BuildTargetGroups, UnityEditor.BuildTargetGroup.Android,
                "без Android фича не попадёт в APK (состояние фичи хранится по платформе)");
            StringAssert.Contains(KvPicoControllerProfile.extensionString, attribute.OpenxrExtensionStrings,
                "фича обязана запросить расширение XR_BD_controller_interaction");
            Assert.AreEqual(KvPicoControllerProfile.featureId, attribute.FeatureId,
                "атрибут и константа обязаны называть одну и ту же фичу");
        }

        /// <summary>
        /// НАШ ПРОФИЛЬ ДЛЯ ANDROID ВЫКЛЮЧЕН, ВМЕСТО НЕГО — ОФИЦИАЛЬНЫЙ ПРОФИЛЬ PICO.
        ///
        /// ЧТО ИЗМЕНИЛОСЬ И ПОЧЕМУ (20.10.2026). Прежняя редакция этого теста требовала, чтобы наш
        /// `KvPicoControllerProfile` был ВКЛЮЧЁН для Android. Это перестало быть верным по факту:
        /// на живом шлеме доказано, что магазинное приложение (собранное на PICO SDK) получает
        /// контроллеры, а наше на чистом OpenXR — нет (`xrGetCurrentInteractionProfile` =
        /// `XR_NULL_PATH`). Владелец снял прежний запрет [R3] и разрешил поставить PICO OpenXR SDK.
        ///
        /// РАЗРЕШЕНИЕ НА ПРАВКУ ЗАПРОШЕНО И ПОЛУЧЕНО отдельно (правило §35: «если тест мешает —
        /// спрашивайте»). Изменён РОВНО ОДИН тест — этот; остальные семь проверок профиля (путь,
        /// расширение, состав элементов устройства) продолжают действовать без изменений, потому
        /// что сам файл профиля остаётся в проекте как проверенное описание контроллера.
        ///
        /// ПОЧЕМУ НАШ ПРОФИЛЬ НЕЛЬЗЯ ДЕРЖАТЬ ВКЛЮЧЁННЫМ РЯДОМ С ОФИЦИАЛЬНЫМ. Правила самого PICO
        /// (`PICOFeature.GetValidationChecks`, `error = true`) не дают собрать APK, если рядом с
        /// профилями PICO включён чужой. Плюс два профиля на один путь взаимодействия спорили бы
        /// за создание одного и того же устройства ввода.
        /// </summary>
        [Test]
        public void Feature_IsDisabledForAndroidInFavourOfPicoProfile()
        {
            OpenXRSettings settings = OpenXRSettings.GetSettingsForBuildTargetGroup(
                UnityEditor.BuildTargetGroup.Android);
            Assert.IsNotNull(settings, "нет настроек OpenXR для Android — проверять нечего");

            OpenXRFeature[] features = settings.GetFeatures<OpenXRFeature>();
            Assert.IsNotNull(features, "пакет OpenXR не вернул список фич");

            bool ourFound = false;
            bool ourEnabled = false;
            bool picoProfileEnabled = false;

            foreach (OpenXRFeature feature in features)
            {
                if (feature == null) continue;

                if (feature is KvPicoControllerProfile)
                {
                    ourFound = true;
                    ourEnabled = feature.enabled;
                    continue;
                }

                string typeName = feature.GetType().Name;
                if (typeName.IndexOf("PICO4UltraControllerProfile", System.StringComparison.Ordinal) >= 0 &&
                    feature.enabled)
                    picoProfileEnabled = true;
            }

            Assert.IsTrue(ourFound, "наш профиль пропал из настроек OpenXR — файл профиля ещё в проекте?");
            Assert.IsFalse(ourEnabled,
                "наш профиль включён для Android: рядом с официальным профилем PICO сборка упадёт на их проверке");
            Assert.IsTrue(picoProfileEnabled,
                "официальный профиль PICO 4 Ultra не включён для Android — контроллеров не будет");
        }

        /// <summary>
        /// УСТРОЙСТВО ОТДАЁТ ТЕ ЭЛЕМЕНТЫ, КОТОРЫЕ ЧИТАЕТ ПРОЕКТ. `KvXrInputSource` ищет курок
        /// по списку имён (`triggerPressed`, `trigger`, …), стик — по (`thumbstick`, `primary2DAxis`),
        /// позу — по `devicePose`. Если профиль не объявит хотя бы курок и позу, луч указателя
        /// работать не будет даже при живом контроллере.
        ///
        /// Проверка идёт по ОБЪЯВЛЕНИЮ класса (reflection), а не регистрацией в Input System:
        /// регистрация меняет состояние Input System на весь прогон и способна сломать соседние
        /// тесты — цена такой проверки выше пользы.
        /// </summary>
        [Test]
        public void DeviceLayout_ExposesControlsTheProjectReads()
        {
            Type device = typeof(KvPicoControllerProfile.KvPicoController);

            Assert.IsTrue(typeof(UnityEngine.InputSystem.XR.XRController).IsAssignableFrom(device),
                "устройство обязано быть XRController — иначе KvXrInputSource его не найдёт");

            string[] required =
            {
                "devicePose", "pointer", "trigger", "triggerPressed", "triggerTouched",
                "grip", "gripPressed", "thumbstick", "thumbstickClicked",
                "primaryButton", "secondaryButton", "menu", "haptic"
            };

            foreach (string name in required)
            {
                PropertyInfo property = device.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                Assert.IsNotNull(property, "у устройства нет элемента «" + name + "»");

                object[] attribute = property.GetCustomAttributes(typeof(InputControlAttribute), false);
                Assert.AreEqual(1, attribute.Length, "элемент «" + name + "» без атрибута InputControl");
            }

            // Проверка стыка с проектом: имена курка/стика/позы, которые ищет KvXrInputSource,
            // обязаны быть среди объявленных элементов устройства.
            string[] triggerNames = KvXrInputSource.TriggerControlNames;
            bool triggerFound = false;
            foreach (string name in triggerNames)
            {
                if (name == "trigger" || name == "triggerPressed") { triggerFound = true; break; }
            }
            Assert.IsTrue(triggerFound, "устройство не отдаёт ни одного имени курка из KvXrInputSource.TriggerControlNames");

            bool stickFound = false;
            foreach (string name in KvXrInputSource.StickControlNames)
            {
                if (name == "thumbstick" || name == "primary2DAxis") { stickFound = true; break; }
            }
            Assert.IsTrue(stickFound, "устройство не отдаёт ни одного имени стика из KvXrInputSource.StickControlNames");

            Assert.IsNotNull(device.GetProperty("devicePose"), "без devicePose указатель не получит позу руки");
        }

        /// <summary>
        /// ФИЧА УМЕЕТ ОТДАВАТЬ КАРТУ ДЕЙСТВИЙ. Unity спрашивает её через
        /// `RegisterActionMapsWithRuntime`; тип, не унаследованный от `OpenXRInteractionFeature`,
        /// устройств не создаёт вовсе.
        /// </summary>
        [Test]
        public void Feature_IsAnInteractionFeature()
        {
            Assert.IsTrue(typeof(OpenXRInteractionFeature).IsAssignableFrom(typeof(KvPicoControllerProfile)),
                "профиль обязан наследоваться от OpenXRInteractionFeature");
        }
    }
}
