// ================================================================================================
//  KvMrFrameBudgetTests.cs — БЮДЖЕТ КАДРА В MR (19.11.2026, пункт плана «Оптимизация (MR)»).
//
//  ОТКУДА ЗАДАЧА. Владелец: «работать в MR не реально — слайд-шоу», и ТО ЖЕ ЧИСЛО ПРИНЯЛО
//  второе требование: «не снижать графику/разрешение/тени — качество обратно» (пресет
//  KvMrPerformance от 17.11.2026 срезал масштаб отрисовки и тени). Значит, бюджет кадра в MR
//  обязан собираться БЕЗ потери качества, и здесь закрепляется то, что можно проверить честно
//  без шлема:
//
//    1) КАЧЕСТВО ВОЗВРАЩЕНО: рантайм-шов MR (`KvGraphicsService.EffectiveProfileFor`)
//       больше не режет масштаб отрисовки и тени — в копию профиля попадает ТОЛЬКО
//       отключение дорогой постобработки;
//    2) ШОВ НЕ СОРВАН: копия в MR всё ещё отличается от профиля оператора и постобработка
//       выключена (иначе «качество вернули» превратилось бы в «MR-ветка не применяется»);
//    3) ЧИСЛО ДЛЯ СТРОКИ МЕТРИКИ: `QualitySettingsKept()` — сколько настроек качества
//       остаются у оператора; число сверяется с РЕФЛЕКСИЕЙ по типу профиля, а не с константой;
//    4) GC В КАДРЕ: подпись настроек панели у руки больше НЕ собирается строками, строка
//       прогресса пишется только при изменении, периодическая диагностика OpenXR останавливается
//       по жёсткому пределу;
//    5) ИНТЕРВАЛЫ: замок кадра и интервалы UI/HUD остаются теми же (в MR дольше, чем в VR).
//
//  ЧЕГО ЗДЕСЬ НЕТ: ЗАМЕРА FPS И ЗАМЕРА БАЙТ GC. Цифры кадровой частоты даёт только шлем;
//  здесь — поведение кода, из которого эти цифры складываются.
//
//  ОКРУЖЕНИЕ: режим MR/VR живёт в PlayerPrefs — тест обязан вернуть всё как было.
// ================================================================================================

using System;
using KazistovVvFeatures;
using KazistovVvUI;
using KazistovVvXr;
using NUnit.Framework;
using UnityEngine;

namespace KazistovVvTests
{
    /// <summary>Бюджет кадра MR: качество сохранено, мусор в кадре убран, интервалы на месте.</summary>
    [Category(KvTestCategories.Fast)]
    public class KvMrFrameBudgetTests
    {
        private int savedMode;
        private int savedPassthrough;
        private bool hadSavedPassthrough;

        [SetUp]
        public void SaveEnvironment()
        {
            savedMode = KvXrSettings.GetModeIndex();
            hadSavedPassthrough = PlayerPrefs.HasKey(KvXrSettings.KeyPassthrough);
            savedPassthrough = PlayerPrefs.GetInt(KvXrSettings.KeyPassthrough, 0);
        }

        [TearDown]
        public void RestoreEnvironment()
        {
            KvXrSettings.SetModeIndex(savedMode);
            if (hadSavedPassthrough) PlayerPrefs.SetInt(KvXrSettings.KeyPassthrough, savedPassthrough);
            else PlayerPrefs.DeleteKey(KvXrSettings.KeyPassthrough);
            PlayerPrefs.Save();
        }

        /// <summary>«Дорогой» профиль — такой, каким MR-ветка раньше резала качество.</summary>
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

        // ==================================================== 1. КАЧЕСТВО ВОЗВРАЩЕНО

        /// <summary>
        /// В MR РАНТАЙМ-ШОВ НЕ РЕЖЕТ МАСШТАБ ОТРИСОВКИ И ТЕНИ.
        ///
        /// Что ловит: возврат к старому поведению пресета («качество обратно» — и снова
        /// масштаб 0,8 и тени 1024). Шов обязан брать копию (профиль оператора не
        /// переписывается), но в копию должны попасть ТОЛЬКО эффекты.
        /// </summary>
        [Test]
        public void MrSeam_KeepsOperatorRenderScaleAndShadows()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            Assert.IsTrue(KvMrPerformance.IsMrMode(), "подготовка: в MR IsMrMode() обязана быть истиной");

            KvGraphicsProfile chosen = ExpensiveProfile();
            KvGraphicsProfile inMr = KvGraphicsService.EffectiveProfileFor(chosen);

            Assert.AreNotSame(chosen, inMr, "в MR обязана применяться копия — иначе съедается профиль оператора");

            Assert.AreEqual(1f, inMr.renderScale, 0.0001f,
                "масштаб отрисовки в MR обязан СОХРАНИТЬСЯ (требование владельца 19.11)");
            Assert.AreEqual(3, inMr.shadowQuality, "качество теней в MR обязано СОХРАНИТЬСЯ");
            Assert.AreEqual(4096, inMr.shadowResolution, "разрешение карты теней в MR обязано СОХРАНИТЬСЯ");
            Assert.AreEqual(120f, inMr.shadowDistance, 0.0001f, "дистанция теней в MR обязан СОХРАНИТЬСЯ");
            Assert.AreEqual(4, inMr.shadowCascades, "каскады теней в MR обязаны СОХРАНИТЬСЯ");

            Assert.IsTrue(chosen.ssao && chosen.ssr && chosen.bloom && chosen.depthOfField,
                "подготовка: профиль оператора обязан остаться прежним — подмена, а не перезапись");
        }

        /// <summary>
        /// В MR ПОСТОБРАБОТКА ВСЁ ЕЩЁ ВЫКЛЮЧЕНА — шов не «перестал работать».
        ///
        /// Что ловит: исправление «верни качество» дошло бы до «MR-ветка не применяется вовсе»,
        /// и кадр снова стал бы дорогим безо всякого видимого изменения в коде пресета.
        /// </summary>
        [Test]
        public void MrSeam_StillDisablesExpensivePostProcessing()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);

            KvGraphicsProfile inMr = KvGraphicsService.EffectiveProfileFor(ExpensiveProfile());

            Assert.IsTrue(KvMrPerformance.ExpensiveEffectsOff(inMr),
                "в MR дорогая постобработка обязана быть выключенной — это единственное, что меняется");
        }

        /// <summary>
        /// ЧИСЛО ДЛЯ СТРОКИ МЕТРИКИ СЧИТАЕТСЯ ОТ ТИПА, А НЕ ЗАПОМНИЛОСЬ РУКОЙ.
        ///
        /// Что ловит: константа «настроек качества N» перестала бы совпадать с профилем после
        /// добавления поля — и строка метрики в отчёте врала бы.
        /// </summary>
        [Test]
        public void QualitySettingsKept_MatchesProfileFields()
        {
            int total = KvMrPerformance.TotalProfileSettings();
            Assert.GreaterOrEqual(total, 20, "в профиле графики обязаны быть настройки качества — иначе проверка вакуумна");

            Assert.AreEqual(KvMrPerformance.PostEffectsCutInMr, 8,
                "пресет выключает ровно 8 флагов постобработки (см. ApplyEffectsTo)");
            Assert.AreEqual(total - KvMrPerformance.PostEffectsCutInMr, KvMrPerformance.QualitySettingsKept(),
                "сохраняемых настроек обязано быть ровно столько, сколько полей минус выключаемые эффекты");
        }

        /// <summary>
        /// В ШОВЕ ПРИМЕНЯЕТСЯ ТОЛЬКО ОТКЛЮЧЕНИЕ ПОСТОБРАБОТКИ, А НЕ ПОЛНЫЙ ПРЕСЕТ.
        ///
        /// Что ловит: правку шва «обратно на ApplyTo» — качество снова срезалось бы, а все
        /// проверки выше (на самой функции) оставались бы зелёными. Проверяется РЕЗУЛЬТАТ
        /// работы `EffectiveProfileFor`, а не текст исходника: прежний греп файла проходил бы
        /// на сломанном коде («строка вызова есть», а вызывается другой метод) и ломался бы
        /// от правки комментария.
        /// </summary>
        [Test]
        public void GraphicsServiceSeam_CallsEffectsOnlyPreset()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            KvGraphicsProfile source = ExpensiveProfile();
            KvGraphicsProfile inMr = KvGraphicsService.EffectiveProfileFor(source);

            Assert.AreNotSame(source, inMr,
                "в MR обязана применяться копия — иначе съедается профиль оператора");
            Assert.IsTrue(KvMrPerformance.ExpensiveEffectsOff(inMr),
                "шов MR обязан выключить дорогую постобработку — это единственное, что он меняет");

            // РОВНО ТО, ЧТО ДЕЛАЕТ ApplyEffectsTo, — И НИЧЕГО БОЛЬШЕ.
            KvGraphicsProfile expected = source.Clone();
            KvMrPerformance.ApplyEffectsTo(expected);
            Assert.AreEqual(expected.renderScale, inMr.renderScale, 0.0001f,
                "масштаб отрисовки в MR обязан СОХРАНИТЬСЯ (требование владельца 19.11)");
            Assert.AreEqual(expected.shadowQuality, inMr.shadowQuality,
                "качество теней в MR обязано СОХРАНИТЬСЯ");
            Assert.AreEqual(expected.shadowResolution, inMr.shadowResolution,
                "разрешение карты теней в MR обязано СОХРАНИТЬСЯ");
            Assert.AreEqual(expected.shadowDistance, inMr.shadowDistance, 0.0001f,
                "дистанция теней в MR обязанa СОХРАНИТЬСЯ");
            Assert.AreEqual(expected.shadowCascades, inMr.shadowCascades,
                "каскады теней в MR обязаны СОХРАНИТЬСЯ");

            // АНТИ-ВАКУУМ: сравнение обязано быть ОТЛИЧАЮЩИМ. Если капы `ApplyTo` перестанут
            // отличаться от профиля оператора, проверка шва ничего не поймает — и это тоже
            // надо увидеть сразу, а не получить тихо зелёный тест.
            Assert.Less(KvMrPerformance.MaxRenderScale, source.renderScale,
                "кап масштаба обязан быть ниже профиля оператора — иначе проверка шва вакуумна");
            Assert.Less(KvMrPerformance.MaxShadowQuality, source.shadowQuality,
                "кап качества теней обязан быть ниже профиля оператора");
            Assert.Less(KvMrPerformance.MaxShadowResolution, source.shadowResolution,
                "кап разрешения теней обязан быть ниже профиля оператора");
            Assert.Less(KvMrPerformance.MaxShadowDistance, source.shadowDistance,
                "кап дистанции теней обязан быть ниже профиля оператора");
            Assert.Less(KvMrPerformance.MaxShadowCascades, source.shadowCascades,
                "кап каскадов теней обязан быть ниже профиля оператора");
        }

        // ==================================================== 2. GC В КАДРЕ

        /// <summary>
        /// ПОДПИСЬ НАСТРОЕК ПАНЕЛИ У РУКИ СРАВНИВАЕТСЯ БЕЗ СТРОК.
        ///
        /// Что ловит: возврат строки-подписи в `Update` — 72 кадра в секунду × 5 аллокаций.
        /// Проверяются и семантика сравнения (радиус — с точностью 0,5 мм, как `ToString("0.000")`),
        /// и сам факт отсутствия строки-подписи в файле.
        /// </summary>
        [Test]
        public void QuickPanelSignature_ComparesWithoutStrings()
        {
            KvXrQuickPanel.Settings a;
            a.shape = 0; a.hand = 1; a.count = 8; a.radius = 0.2f;

            KvXrQuickPanel.Settings same = a;
            Assert.IsTrue(KvXrQuickPanel.SameSettings(a, same), "одинаковые настройки обязаны совпасть");

            KvXrQuickPanel.Settings otherCount = a;
            otherCount.count = 6;
            Assert.IsFalse(KvXrQuickPanel.SameSettings(a, otherCount), "другое число кнопок — другой signature");

            KvXrQuickPanel.Settings radiusStep = a;
            radiusStep.radius = 0.201f;                      // 1 мм — больше порога 0,5 мм
            Assert.IsFalse(KvXrQuickPanel.SameSettings(a, radiusStep),
                "изменение радиуса на 1 мм обязано пересобрать панель");

            KvXrQuickPanel.Settings radiusNoise = a;
            radiusNoise.radius = 0.2001f;                    // 0,1 мм — ниже порога
            Assert.IsTrue(KvXrQuickPanel.SameSettings(a, radiusNoise),
                "дребезг радиуса меньше 0,5 мм НЕ обязан пересобирать панель");

            // СТРОКА-ПОДПИСЬ ОТСУТСТВУЕТ В САМОМ ТИПЕ, А НЕ В ФАЙЛЕ: поле-кэш строки,
            // пересобираемое в `Update`, — ровно тот источник аллокаций, который ловил прежний
            // греп исходника. Рефлексия смотрит на СКОМПИЛИРОВАННЫЙ тип: она проходит только при
            // отсутствии самого поля (переименование не поможет) и не ломается от правки
            // комментария в файле.
            var suspects = new System.Collections.Generic.List<string>();
            foreach (System.Reflection.FieldInfo f in typeof(KvXrQuickPanel).GetFields(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (f.Name.IndexOf('<') >= 0) continue;   // backing-поля автосвойств — не кэш в Update
                if (f.FieldType == typeof(string)) suspects.Add("поле " + f.Name);
                if (f.Name.IndexOf("Signature",
                    StringComparison.OrdinalIgnoreCase) >= 0) suspects.Add("поле " + f.Name);
            }
            foreach (System.Reflection.PropertyInfo p in typeof(KvXrQuickPanel).GetProperties(
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
            {
                if (p.Name.IndexOf("Signature", StringComparison.OrdinalIgnoreCase) >= 0)
                    suspects.Add("свойство " + p.Name);
            }
            Assert.IsEmpty(suspects,
                "строковые поля/подписи в панели быстрых команд — это аллокации в Update: " +
                string.Join(", ", suspects.ToArray()));
        }

        /// <summary>
        /// ЗАМОК СТРОКИ И ЧИСЛА: без изменения — ни аллокации, ни вызова сеттера текста.
        ///
        /// Что ловит: правку `KvUiStates.Update` обратно на сборку строки каждый кадр — именно
        /// так во время загрузки планирования/записи интерфейс пересобирался 72 раза в секунду.
        /// </summary>
        [Test]
        public void TextGate_AllocatesOnlyOnChange()
        {
            string cache = null;
            Assert.IsTrue(KvMrPerformance.TextChanged(ref cache, "Планирование"),
                "первое значение обязано пройти — иначе подпись никогда не появится");
            Assert.AreEqual("Планирование", cache);

            Assert.IsFalse(KvMrPerformance.TextChanged(ref cache, "Планирование"),
                "тот же текст НЕ обязан проходить — иначе строка аллоцируется каждый кадр");

            Assert.IsTrue(KvMrPerformance.TextChanged(ref cache, "Экспорт"),
                "смена текста обязана пройти");
            Assert.AreEqual("Экспорт", cache);

            int percent = -1;
            Assert.IsTrue(KvMrPerformance.Changed(ref percent, 0), "первый процент обязан пройти");
            Assert.IsFalse(KvMrPerformance.Changed(ref percent, 0), "тот же процент НЕ обязан проходить");
            Assert.IsTrue(KvMrPerformance.Changed(ref percent, 50), "новый процент обязан проходить");
        }

        /// <summary>
        /// ПЕРИОДИЧЕСКАЯ ДИАГНОСТИКА OPENXR ОСТАНАВЛИВАЕТСЯ В ЛЮБОМ СЛУЧАЕ.
        ///
        /// Что ловит: старое условие `elapsed > 34 && deviceReports > 0` — при нулевых
        /// отчётах XR-подсистемы (шлем не надет) диагностика опрашивала устройства и
        /// строила строки КАЖДУЮ СЕКУНДУ ВСЮ ЖИЗНЬ приложения.
        /// </summary>
        [Test]
        public void OpenXrDiagnostics_StopsAtHardDeadline()
        {
            Assert.IsFalse(KvXrOpenXrDiagnostics.ShouldStop(10f, 0),
                "в первые секунды диагностика обязана работать");
            Assert.IsFalse(KvXrOpenXrDiagnostics.ShouldStop(40f, 0),
                "до жёсткого предела с пустыми отчётами тоже — контроллеры могут появиться позже");
            Assert.IsTrue(KvXrOpenXrDiagnostics.ShouldStop(40f, 3),
                "после 34 с с отработанными отчётами обязана остановиться");

            float hard = KvXrOpenXrDiagnostics.HardDeadlineSeconds;
            Assert.GreaterOrEqual(hard, 40f, "жёсткий предел обязан быть не меньше окна отчётов");
            Assert.IsTrue(KvXrOpenXrDiagnostics.ShouldStop(hard + 1f, 0),
                "после жёсткого предела диагностика обязана остановиться ДАЖЕ без единого отчёта");
        }

        // ==================================================== 3. ИНТЕРВАЛЫ

        /// <summary>
        /// ИНТЕРВАЛЫ UI/HUD НЕ ПОПЛЫЛИ ПОСЛЕ ВОЗВРАТА КАЧЕСТВА.
        ///
        /// Что ловит: правку троттлинга вместе с пресетом («вернул качество — и заодно убрал
        /// задержки»), из-за которого строки в `Update` снова собирались бы каждый кадр.
        /// </summary>
        [Test]
        public void UiThrottle_SurvivesQualityRestore()
        {
            KvXrSettings.SetModeIndex(KvXrSettings.ModeMr);
            float mr = KvMrPerformance.UiUpdateIntervalSeconds();

            KvXrSettings.SetModeIndex(KvXrSettings.ModeVr);
            float vr = KvMrPerformance.UiUpdateIntervalSeconds();

            Assert.Greater(mr, vr, "в MR интервал UI обязан быть длиннее, чем в VR");
            Assert.GreaterOrEqual(mr, 0.3f, "в MR интервал обязан быть не меньше 0,3 с");
            Assert.Greater(vr, 0f, "в VR тоже обязан быть ненулевой интервал");

            float last = -1000f;
            Assert.IsTrue(KvMrPerformance.Due(0f, mr, ref last), "первое обновление обязано пройти");
            Assert.IsFalse(KvMrPerformance.Due(0.1f, mr, ref last), "до интервала обновление закрыто");
            Assert.IsTrue(KvMrPerformance.Due(mr + 0.01f, mr, ref last), "после интервала обновление пройти");
        }
    }
}
