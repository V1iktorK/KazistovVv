// ================================================================================================
//  KvMrOptimizationTests.cs — EditMode-тесты ОПТИМИЗАЦИИ MR (17.11.2026, пункт плана «в работе»).
//
//  ОТКУДА ЗАДАЧА. Владелец: «работать в MR не реально — слайд-шоу» (кадровая частота падает
//  ИМЕННО в MR на шлеме PICO). Без шлема FPS не измеряется, поэтому здесь закрепляется то,
//  что можно проверить честно на стенде:
//    1) ПРЕСЕТ КАЧЕСТВА ДЛЯ MR: профиль, который реально применяется, дешевле профиля
//       оператора — масштаб отрисовки ниже, тени упрощены, дорогие эффекты выключены;
//    2) ПРЕСЕТ НЕ ТРОГАЕТ ПРОФИЛЬ ОПЕРАТОРА: в VR остаётся то, что выбрал оператор, и
//       возвращение из MR не «съедает» его настройки;
//    3) ТРОТТЛИНГ UI/HUD: обновления панелей идут по таймеру, и в MR таймер ДЛИННЕЕ, чем в VR —
//       убираются аллокации строк в `Update` (см. Docs/PERFORMANCE_AUDIT.md, §3.1).
//
//  ЧЕГО ЗДЕСЬ НЕТ: ЗАМЕРА FPS. Цифры кадровой частоты даёт только шлем — обещать их без замера
//  нельзя. Владелец замеряет на устройстве; здесь — поведение кода.
//
//  ОКРУЖЕНИЕ: режим и профиль графики живут в PlayerPrefs — тест обязан вернуть всё как было.
// ================================================================================================

using KazistovVvFeatures;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Оптимизация MR: пресет качества, отсутствие побочных эффектов, троттлинг UI.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvMrOptimizationTests
    {
        private int savedPassthrough;
        private bool hadSavedPassthrough;

        [SetUp]
        public void SaveEnvironment()
        {
            hadSavedPassthrough = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
            savedPassthrough = PlayerPrefs.GetInt(KvXrSettings.KeyPassthrough, 0);
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            if (hadSavedPassthrough) PlayerPrefs.SetInt(KvXrSettings.KeyPassthrough, savedPassthrough);
            else PlayerPrefs.DeleteKey(KvXrSettings.KeyPassthrough);
            PlayerPrefs.Save();
        }

        /// <summary>«Дорогой» профиль — такой, каким MR-пресет обязан стать дешевле.</summary>
        private static KvGraphicsProfile ExpensiveProfile()
        {
            return new KvGraphicsProfile
            {
                renderScale = 1f,
                shadowQuality = 3,
                shadowResolution = 4096,
                shadowDistance = 120f,
                shadowCascades = 4,
                ssao = true,
                ssr = true,
                reflectionMode = 1,
                volumetricFog = true,
                bloom = true,
                motionBlur = true,
                depthOfField = true,
                rayTracing = true
            };
        }

        // ==================================================== 1. ПРЕСЕТ ПРИМЕНЯЕТСЯ В MR

        /// <summary>
        /// В MR ПРЕСЕТ ПРИМЕНЯЕТСЯ: ДОРОГИЕ ЭФФЕКТЫ ВЫКЛЮЧЕНЫ, РАЗРЕШЕНИЕ И ТЕНИ НИЖЕ.
        ///
        /// Что ловит: пресет «написан, но никем не применяется» — ровно тот класс дефекта, что
        /// уже был в S44 (`SetPassthrough` не звали ниоткуда). Здесь проверяется сама функция,
        /// которую зовёт служба графики при каждом применении.
        /// </summary>
        [Test]
        public void MrPreset_AppliesAndDisablesExpensiveEffects()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            Assert.IsTrue(KvMrPerformance.IsMrMode(), "подготовка: в MR IsMrMode() обязана быть истиной");

            KvGraphicsProfile p = ExpensiveProfile();
            Assert.IsTrue(p.ssao && p.ssr && p.volumetricFog && p.bloom,
                "подготовка: исходный профиль обязан быть «дорогим», иначе проверка вакуумна");

            Assert.IsTrue(KvMrPerformance.ApplyIfMr(p), "в MR пресет обязан применяться");

            Assert.IsFalse(p.ssao, "SSAO в MR обязан быть выключен");
            Assert.IsFalse(p.ssr, "SSR в MR обязан быть выключен");
            Assert.IsFalse(p.volumetricFog, "объёмный туман в MR обязан быть выключен");
            Assert.IsFalse(p.bloom, "Bloom в MR обязан быть выключен");
            Assert.IsFalse(p.motionBlur, "размытие в движении в MR обязано быть выключено");
            Assert.IsFalse(p.depthOfField, "глубина резкости в MR обязаны быть выключены");
            Assert.IsFalse(p.rayTracing, "трассировка лучей в MR обязаны быть выключены");
            Assert.AreEqual(0, p.reflectionMode, "отражения реального времени в MR обязаны быть выключены");
            Assert.IsTrue(KvMrPerformance.ExpensiveEffectsOff(p),
                "после пресета ни один дорогой эффект не должен остаться включённым");

            Assert.LessOrEqual(p.renderScale, KvMrPerformance.MaxRenderScale,
                "масштаб отрисовки в MR обязан быть ниже единицы (иначе кадр не дешевлеет)");
            Assert.Less(p.renderScale, 1f, "масштаб отрисовки в MR обязан быть строго меньше 1");
            Assert.LessOrEqual(p.shadowQuality, KvMrPerformance.MaxShadowQuality,
                "качество теней в MR обязано быть упрощено");
            Assert.LessOrEqual(p.shadowCascades, KvMrPerformance.MaxShadowCascades,
                "каскады теней в MR обязаны быть уменьшены");
            Assert.LessOrEqual(p.shadowDistance, KvMrPerformance.MaxShadowDistance + 0.001f,
                "дистанция теней в MR обязано быть укорочена");
            Assert.LessOrEqual(p.shadowResolution, KvMrPerformance.MaxShadowResolution,
                "разрешение карты теней в MR обязано быть меньше");
        }

        /// <summary>
        /// В VR ПРЕСЕТ НЕ ПРИМЕНЯЕТСЯ: профиль оператора остаётся нетронутым.
        ///
        /// Что ловит: «MR-пресет съел настройки» — после визита в MR оператор обнаружил бы
        /// выключенные эффекты в обычном VR. Пресет обязан быть ПОВЕРХНОСТНЫМ (на время
        /// применения), а не записью в профиль.
        /// </summary>
        [Test]
        public void VrMode_LeavesOperatorProfileUntouched()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            Assert.IsFalse(KvMrPerformance.IsMrMode(), "подготовка: в VR IsMrMode() обязана быть ложной");

            KvGraphicsProfile p = ExpensiveProfile();
            Assert.IsFalse(KvMrPerformance.ApplyIfMr(p), "в VR пресет применяться не обязан");
            Assert.IsTrue(p.ssao && p.ssr && p.volumetricFog && p.bloom && p.motionBlur && p.depthOfField,
                "в VR профиль оператора обязан остаться прежним");
            Assert.AreEqual(1f, p.renderScale, "масштаб отрисовки в VR меняться не обязан");
            Assert.AreEqual(3, p.shadowQuality, "качество теней в VR меняться не обязано");
        }

        /// <summary>
        /// СЛУЖБА ГРАФИКИ БЕРЁТ ПРОФИЛЬ ЧЕРЕЗ ОДНУ ТОЧКУ: в MR — дешёвую копию, в VR — оригинал.
        ///
        /// Это шов, которым `KvGraphicsService.Apply` подменивает профиль перед применением.
        /// Если шов разорвётся (служба начнёт брать `active` напрямую), пресет перестанет
        /// применяться — и падение FPS в MR вернётся безо всякого изменения в коде пресета.
        /// </summary>
        [Test]
        public void GraphicsService_TakesProfileThroughMrSeam()
        {
            KvGraphicsProfile chosen = ExpensiveProfile();

            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            KvGraphicsProfile inMr = KvGraphicsService.EffectiveProfileFor(chosen);
            Assert.AreNotSame(chosen, inMr, "в MR служба обязана брать ДЕШЁВУЮ КОПИЮ, а не оригинал");
            Assert.IsTrue(KvMrPerformance.ExpensiveEffectsOff(inMr), "копия в MR должна быть без дорогих эффектов");
            Assert.IsTrue(chosen.ssao && chosen.ssr,
                "профиль оператора при подмене обязан остаться прежним — копия, а не перезапись");

            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            KvGraphicsProfile inVr = KvGraphicsService.EffectiveProfileFor(chosen);
            Assert.AreSame(chosen, inVr, "в VR служба обязана применять профиль оператора как есть");
        }

        // ==================================================== 2. ТРОТТЛИНГ UI / HUD

        /// <summary>
        /// В MR ОБНОВЛЕНИЯ UI/HUD ИДУТ РЕЖЕ, ЧЕМ В VR.
        ///
        /// Что ловит: «троттлинг написан, но всегда один и тот же» — интервал обязан зависеть
        /// от режима, иначе MR не получает ничего из этой оптимизации. Ненулевой интервал и в VR
        /// убирает аллокации строк в `Update` (Docs/PERFORMANCE_AUDIT.md §3.1, KvGamepadHud).
        /// </summary>
        [Test]
        public void UiThrottle_IsSlowerInMrThanInVr()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            float mr = KvMrPerformance.UiUpdateIntervalSeconds();

            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            float vr = KvMrPerformance.UiUpdateIntervalSeconds();

            Assert.Greater(mr, vr, "в MR интервал обновления UI обязан быть ДЛИННЕЕ, чем в VR");
            Assert.GreaterOrEqual(mr, 0.3f,
                "в MR интервал обновления UI обязан быть не меньше 0,3 с — иначе это не троттлинг");
            Assert.Greater(vr, 0f,
                "в VR обновления тоже обязаны идти по таймеру (строки в Update аллоцируются каждый кадр)");
            Assert.Less(vr, mr, "в VR интервал обязан быть короче, чем в MR — иначе задержка одинаковая");
        }

        /// <summary>
        /// ЗАМОК КАДРА: до интервала обновления НЕ происходит, после — происходит.
        ///
        /// Что ловит: таймер, который пропускает всё (interval не учитывается) либо всё
        /// блокирует (обновление не наступает никогда) — обе ошибки делают троттлинг фиктивным.
        /// </summary>
        [Test]
        public void Throttle_DueGatesUpdatesByInterval()
        {
            float last = -1000f;
            Assert.IsTrue(KvMrPerformance.Due(0f, 0.35f, ref last), "первое обновление обязано пройти");
            Assert.IsFalse(KvMrPerformance.Due(0.10f, 0.35f, ref last),
                "до истечения интервала обновление обязано быть закрыто");
            Assert.IsFalse(KvMrPerformance.Due(0.34f, 0.35f, ref last),
                "на границе интервала обновление ещё закрыто");
            Assert.IsTrue(KvMrPerformance.Due(0.36f, 0.35f, ref last),
                "после интервала обновление обязано пройти");

            last = -1000f;
            Assert.IsTrue(KvMrPerformance.Due(0f, 0f, ref last), "нулевой интервал = каждый кадр");
            Assert.IsTrue(KvMrPerformance.Due(0.001f, 0f, ref last),
                "нулевой интервал обязан пропускать каждый кадр — иначе VR застынет");
        }

        /// <summary>
        /// HUD ГЕЙМПАДА ЧИТАЕТ ОБЩИЙ ТРОТТЛИНГ, А НЕ СВОЙ КАЖДОККАДНЫЙ ЦИКЛ.
        ///
        /// Что ловит: правка в `KvMrPerformance` мимо панели — интервал был бы «в документации»,
        /// но сам `Update` продолжал бы собирать строки каждый кадр.
        /// </summary>
        [Test]
        public void GamepadHud_ReadsSharedThrottle()
        {
            GameObject host = new GameObject("KvMrOptHudHost");
            try
            {
                GameObject canvasGo = new GameObject("Canvas", typeof(RectTransform));
                canvasGo.transform.SetParent(host.transform, false);

                KvGamepadHud hud = KvGamepadHud.Create((RectTransform)canvasGo.transform);
                Assert.IsNotNull(hud, "индикатор геймпада не собрался — проверка вакуумна");

                KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
                float mr = hud.UpdateInterval;
                KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
                float vr = hud.UpdateInterval;

                Assert.AreEqual(KvMrPerformance.UiUpdateIntervalSeconds(), vr,
                    "HUD обязан читать ОБЩИЙ интервал, а не держать свой");
                Assert.Greater(mr, vr, "интервал HUD в MR обязан быть длиннее, чем в VR");
                Assert.GreaterOrEqual(mr, 0.3f, "в MR HUD обязан обновляться не чаще, чем раз в 0,3 с");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }
    }
}
