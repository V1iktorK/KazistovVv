using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvUI;

namespace KazistovVvTests
{
    /// <summary>
    /// EditMode-тесты РАЗДЕЛА «ГРАФИКА» (ТЗ: автоопределение под железо, пресеты качества,
    /// локализация на 7 языков, файл пресетов без перекомпиляции).
    ///
    /// ЧТО ПРОВЕРЯЕТСЯ И ПОЧЕМУ ИМЕННО ЭТО.
    ///
    /// Применение настроек к HDRP (ассет, камера, том, QualitySettings) требует рабочей
    /// графики и режима воспроизведения — в пакетном прогоне без видеокарты служба по
    /// построению ничего не применяет (это её штатное поведение, ТЗ прямо требует
    /// «в headless сервис считает, но не применяет»). Поэтому здесь тестируется вся
    /// РЕШАЮЩАЯ часть, которая от графики не зависит и которую видно в пакетном прогоне:
    ///   • таблица пресетов ТЗ (этап 2) — числа Low/Medium/High/Ultra/VR-Ready;
    ///   • классификация железа и обоснование выбора (этап 1);
    ///   • хеш конфигурации — устойчивость и реакция на смену железа (этап 5);
    ///   • разбор файла пресетов и терпимость к написанию значений (этап 2, «без перекомпиляции»);
    ///   • сам поставленный файл `StreamingAssets/kazistovvv_graphics.json` — он тоже
    ///     артефакт сборки, и его значения обязаны совпадать с таблицей ТЗ;
    ///   • локализация: все ключи `graphics.*` присутствуют во ВСЕХ семи языках (этап 6).
    ///
    /// ПРАВИЛА, СОБЛЮДЁННЫЕ В ТЕСТАХ (как в остальных тестах проекта):
    ///   • ни одного GameObject / MonoBehaviour / Instantiate / корутины;
    ///   • ни одной записи в PlayerPrefs и ни одного изменения файлов проекта —
    ///     поэтому служба <see cref="KvGraphicsService"/> (она пишет PlayerPrefs) в тестах
    ///     НЕ запускается: проверяются её чистые части через модель и опрос железа;
    ///   • нет зависимости от реального времени и случайности.
    /// </summary>
        [Category(KvTestCategories.Fast)]
    public class KvGraphicsTests
    {
        // ================================================================== ЭТАП 2: пресеты ТЗ

        /// <summary>Последний параметр: сверка пресетов с таблицей ТЗ построчно.</summary>
        [Test]
        public void Builtin_Low_MatchesSpec()
        {
            KvGraphicsProfile p = KvGraphicsModel.Builtin(KvGraphicsPreset.Low);
            Assert.AreEqual(0.7f, p.renderScale, 0.0001f, "масштаб отрисовки Low");
            Assert.AreEqual(512, p.shadowResolution, "тени Low");
            Assert.AreEqual(30f, p.shadowDistance, 0.001f, "дистанция теней Low");
            Assert.AreEqual(1, p.shadowCascades, "каскады Low");
            Assert.IsFalse(p.ssao, "SSAO Low — выключен");
            Assert.IsFalse(p.ssr, "SSR Low — выключен");
            Assert.AreEqual(0, p.reflectionMode, "отражения Low — только запечённые");
            Assert.IsFalse(p.volumetricFog, "объёмный туман Low — выключен");
            Assert.AreEqual((int)KvAntiAliasing.FXAA, p.antiAliasing, "AA Low — FXAA");
            Assert.IsFalse(p.bloom, "Bloom Low — выключен");
            Assert.IsFalse(p.motionBlur, "размытие движения Low — выключено");
            Assert.IsFalse(p.depthOfField, "глубина резкости Low — выключена");
            Assert.IsFalse(p.rayTracing, "трассировка лучей Low — выключена");
            Assert.AreEqual(2, p.mipmapLimit, "текстуры Low — 1/4");
            Assert.AreEqual(0.5f, p.lodBias, 0.0001f, "LOD bias Low");
            Assert.AreEqual(0, p.decalQuality, "декали Low");
            Assert.AreEqual(0, p.terrainQuality, "рельеф Low");
            Assert.AreEqual((int)KvVSync.On, p.vsync, "VSync Low — включён");
            Assert.AreEqual((int)KvFrameCap.Fps60, p.frameCap, "предел кадров Low — 60");
        }

        [Test]
        public void Builtin_Medium_MatchesSpec()
        {
            KvGraphicsProfile p = KvGraphicsModel.Builtin(KvGraphicsPreset.Medium);
            Assert.AreEqual(0.85f, p.renderScale, 0.0001f);
            Assert.AreEqual(1024, p.shadowResolution);
            Assert.AreEqual(50f, p.shadowDistance, 0.001f);
            Assert.AreEqual(2, p.shadowCascades);
            Assert.IsTrue(p.ssao, "SSAO Medium — включён");
            Assert.IsFalse(p.ssr, "SSR Medium — выключен");
            Assert.AreEqual(0, p.reflectionMode, "отражения Medium — запечённые");
            Assert.IsTrue(p.volumetricFog, "туман Medium — включён");
            Assert.AreEqual(0, p.volumetricFogQuality, "качество тумана Medium — низкое");
            Assert.AreEqual((int)KvAntiAliasing.FXAA, p.antiAliasing);
            Assert.IsTrue(p.bloom);
            Assert.IsFalse(p.motionBlur);
            Assert.IsFalse(p.depthOfField);
            Assert.AreEqual(1, p.mipmapLimit, "текстуры Medium — 1/2");
            Assert.AreEqual(0.7f, p.lodBias, 0.0001f);
            Assert.AreEqual(1, p.decalQuality);
            Assert.AreEqual(1, p.terrainQuality);
            Assert.AreEqual((int)KvVSync.On, p.vsync);
            Assert.AreEqual((int)KvFrameCap.Fps60, p.frameCap);
        }

        [Test]
        public void Builtin_High_MatchesSpec()
        {
            KvGraphicsProfile p = KvGraphicsModel.Builtin(KvGraphicsPreset.High);
            Assert.AreEqual(1f, p.renderScale, 0.0001f);
            Assert.AreEqual(2048, p.shadowResolution);
            Assert.AreEqual(80f, p.shadowDistance, 0.001f);
            Assert.AreEqual(4, p.shadowCascades);
            Assert.IsTrue(p.ssao);
            Assert.IsTrue(p.ssr, "SSR High — включён");
            Assert.AreEqual(1, p.reflectionMode, "отражения High — запечённые + реального времени");
            Assert.IsTrue(p.volumetricFog);
            Assert.AreEqual(1, p.volumetricFogQuality);
            Assert.AreEqual((int)KvAntiAliasing.TAA, p.antiAliasing, "AA High — TAA");
            Assert.IsTrue(p.bloom);
            Assert.IsTrue(p.motionBlur);
            Assert.IsTrue(p.depthOfField);
            Assert.IsFalse(p.rayTracing, "трассировка лучей High — выключена");
            Assert.AreEqual(0, p.mipmapLimit, "текстуры High — полные");
            Assert.AreEqual(1f, p.lodBias, 0.0001f);
            Assert.AreEqual(2, p.decalQuality);
            Assert.AreEqual(2, p.terrainQuality);
            Assert.AreEqual((int)KvVSync.On, p.vsync);
            Assert.AreEqual((int)KvFrameCap.Fps120, p.frameCap, "предел кадров High — 120");
        }

        [Test]
        public void Builtin_Ultra_MatchesSpec()
        {
            KvGraphicsProfile p = KvGraphicsModel.Builtin(KvGraphicsPreset.Ultra);
            Assert.AreEqual(1f, p.renderScale, 0.0001f);
            Assert.AreEqual(4096, p.shadowResolution);
            Assert.AreEqual(120f, p.shadowDistance, 0.001f);
            Assert.AreEqual(4, p.shadowCascades);
            Assert.IsTrue(p.ssao);
            Assert.IsTrue(p.ssr);
            Assert.AreEqual(1, p.reflectionMode);
            Assert.IsTrue(p.volumetricFog);
            Assert.AreEqual(2, p.volumetricFogQuality);
            Assert.AreEqual((int)KvAntiAliasing.TAA, p.antiAliasing);
            Assert.IsTrue(p.bloom);
            Assert.IsTrue(p.motionBlur);
            Assert.IsTrue(p.depthOfField);
            Assert.IsTrue(p.rayTracing, "трассировка лучей Ultra — «если поддерживается» (проверяется при применении)");
            Assert.AreEqual(0, p.mipmapLimit, "текстуры Ultra — полные");
            Assert.AreEqual(1.2f, p.lodBias, 0.0001f);
            Assert.AreEqual(3, p.decalQuality);
            Assert.AreEqual(2, p.terrainQuality);
            Assert.AreEqual((int)KvVSync.Off, p.vsync, "VSync Ultra — выключен");
            Assert.AreEqual((int)KvFrameCap.Unlimited, p.frameCap, "предел кадров Ultra — без предела");
        }

        [Test]
        public void Builtin_VRReady_MatchesSpec()
        {
            KvGraphicsProfile p = KvGraphicsModel.Builtin(KvGraphicsPreset.VRReady);
            Assert.AreEqual(0.9f, p.renderScale, 0.0001f);
            Assert.AreEqual(1024, p.shadowResolution, "тени VR-Ready — среднее");
            Assert.AreEqual(40f, p.shadowDistance, 0.001f);
            Assert.AreEqual(2, p.shadowCascades);
            Assert.IsTrue(p.ssao);
            Assert.IsFalse(p.ssr, "SSR VR-Ready — выключен");
            Assert.AreEqual(0, p.reflectionMode);
            Assert.IsFalse(p.volumetricFog, "объёмный туман VR-Ready — выключен");
            Assert.AreEqual((int)KvAntiAliasing.TAA, p.antiAliasing, "AA VR-Ready — TAA (MSAA недоступен в отложенном режиме)");
            Assert.IsTrue(p.bloom);
            Assert.IsFalse(p.motionBlur, "размытие движения VR-Ready — выключено");
            Assert.IsFalse(p.depthOfField, "глубина резкости VR-Ready — выключена");
            Assert.AreEqual(1, p.mipmapLimit, "текстуры VR-Ready — 1/2");
            Assert.AreEqual(0.7f, p.lodBias, 0.0001f);
            Assert.AreEqual(1, p.decalQuality);
            Assert.AreEqual(0, p.terrainQuality);
            Assert.AreEqual((int)KvVSync.On, p.vsync);
            Assert.AreEqual((int)KvFrameCap.Fps90, p.frameCap, "предел кадров VR-Ready — 90 (обязательное требование ТЗ)");
            Assert.IsTrue(p.dynamicResolution, "динамическое разрешение VR-Ready — включено (ТЗ)");
            Assert.IsTrue(p.adaptiveQuality, "адаптивное качество VR-Ready — включено (ТЗ)");
        }

        [Test]
        public void Builtin_AllPresets_HaveUniqueKeysAndCoverFiveModes()
        {
            List<KvGraphicsProfile> all = KvGraphicsModel.BuiltinAll();
            Assert.AreEqual(5, all.Count, "встроенных пресетов — пять (Low…VR-Ready)");
            HashSet<string> keys = new HashSet<string>();
            foreach (KvGraphicsProfile p in all)
            {
                Assert.IsFalse(string.IsNullOrEmpty(p.id), "у пресета есть ключ");
                Assert.IsTrue(keys.Add(p.id), "ключ пресета уникален: " + p.id);
            }
            Assert.IsTrue(keys.Contains("Low") && keys.Contains("Medium") && keys.Contains("High") &&
                          keys.Contains("Ultra") && keys.Contains("VRReady"), "ключи пресетов");
        }

        /// <summary>Каждый пресет обязан быть пригодным: значения в допусках после Normalize.</summary>
        [Test]
        public void Builtin_AllPresets_SurviveNormalizeUnchanged()
        {
            foreach (KvGraphicsProfile p in KvGraphicsModel.BuiltinAll())
            {
                string before = p.Signature();
                p.Normalize(true);
                Assert.AreEqual(before, p.Signature(), "Normalize не должен менять корректный пресет " + p.id);
            }
        }

        // ================================================================== ЭТАП 2: преобразования

        [Test]
        public void ShadowResolution_MapsQualityAndBack()
        {
            Assert.AreEqual(512, KvGraphicsModel.ShadowResolutionOf(0));
            Assert.AreEqual(1024, KvGraphicsModel.ShadowResolutionOf(1));
            Assert.AreEqual(2048, KvGraphicsModel.ShadowResolutionOf(2));
            Assert.AreEqual(4096, KvGraphicsModel.ShadowResolutionOf(3));
            for (int q = 0; q <= 3; q++)
                Assert.AreEqual(q, KvGraphicsModel.ShadowQualityOf(KvGraphicsModel.ShadowResolutionOf(q)),
                    "обратное преобразование качества теней, уровень " + q);
        }

        [Test]
        public void FrameCap_MapsIndexValueAndBack()
        {
            Assert.AreEqual(30, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Fps30));
            Assert.AreEqual(60, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Fps60));
            Assert.AreEqual(72, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Fps72));
            Assert.AreEqual(90, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Fps90));
            Assert.AreEqual(120, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Fps120));
            Assert.AreEqual(144, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Fps144));
            Assert.AreEqual(-1, KvGraphicsModel.FrameCapValue((int)KvFrameCap.Unlimited), "без предела — −1");
            foreach (int fps in new[] { 30, 60, 72, 90, 120, 144, -1 })
                Assert.AreEqual(fps, KvGraphicsModel.FrameCapValue(KvGraphicsModel.FrameCapIndex(fps)),
                    "обратное преобразование предела кадров для " + fps);
        }

        [Test]
        public void TextureLabel_And_DecalDistance()
        {
            Assert.AreEqual("Full", KvGraphicsModel.TextureLabel(0));
            Assert.AreEqual("1/2", KvGraphicsModel.TextureLabel(1));
            Assert.AreEqual("1/4", KvGraphicsModel.TextureLabel(2));
            Assert.AreEqual("1/8", KvGraphicsModel.TextureLabel(3));
            Assert.AreEqual(10, KvGraphicsModel.DecalDistanceOf(0));
            Assert.AreEqual(25, KvGraphicsModel.DecalDistanceOf(1));
            Assert.AreEqual(50, KvGraphicsModel.DecalDistanceOf(2));
            Assert.AreEqual(100, KvGraphicsModel.DecalDistanceOf(3));
        }

        [Test]
        public void PresetKey_RoundTrips_AndToleratesSpellings()
        {
            foreach (KvGraphicsPreset p in KvGraphicsModel.Order)
                Assert.AreEqual(p, KvGraphicsModel.PresetOf(KvGraphicsModel.KeyOf(p)),
                    "ключ → пресет для " + p);

            Assert.AreEqual(KvGraphicsPreset.VRReady, KvGraphicsModel.PresetOf("VR-Ready"));
            Assert.AreEqual(KvGraphicsPreset.VRReady, KvGraphicsModel.PresetOf("vr_ready"));
            Assert.AreEqual(KvGraphicsPreset.VRReady, KvGraphicsModel.PresetOf(" vr ready "));
            Assert.AreEqual(KvGraphicsPreset.Auto, KvGraphicsModel.PresetOf("авто"));
            Assert.AreEqual(KvGraphicsPreset.Custom, KvGraphicsModel.PresetOf("Свой"));
            Assert.AreEqual(KvGraphicsPreset.Ultra, KvGraphicsModel.PresetOf("ULTRA"));
            Assert.AreEqual(KvGraphicsPreset.Auto, KvGraphicsModel.PresetOf("ерунда"));
        }

        // ================================================================== ЭТАП 2: разбор файла

        [Test]
        public void Parse_AntiAliasing_Tolerant()
        {
            Assert.AreEqual(KvAntiAliasing.Off, KvGraphicsModel.ParseAntiAliasing("off", KvAntiAliasing.TAA));
            Assert.AreEqual(KvAntiAliasing.Off, KvGraphicsModel.ParseAntiAliasing("Выкл", KvAntiAliasing.TAA));
            Assert.AreEqual(KvAntiAliasing.FXAA, KvGraphicsModel.ParseAntiAliasing("FXAA", KvAntiAliasing.Off));
            Assert.AreEqual(KvAntiAliasing.SMAA, KvGraphicsModel.ParseAntiAliasing("smaa", KvAntiAliasing.Off));
            Assert.AreEqual(KvAntiAliasing.TAA, KvGraphicsModel.ParseAntiAliasing("TAA", KvAntiAliasing.Off));
            Assert.AreEqual(KvAntiAliasing.MSAA2x, KvGraphicsModel.ParseAntiAliasing("MSAA 2x", KvAntiAliasing.Off));
            Assert.AreEqual(KvAntiAliasing.MSAA4x, KvGraphicsModel.ParseAntiAliasing("MSAA 4x", KvAntiAliasing.Off));
            Assert.AreEqual(KvAntiAliasing.MSAA8x, KvGraphicsModel.ParseAntiAliasing("msaa 8x", KvAntiAliasing.Off));
            Assert.AreEqual(KvAntiAliasing.TAA, KvGraphicsModel.ParseAntiAliasing("", KvAntiAliasing.TAA), "пусто — исходное");
        }

        [Test]
        public void Parse_VSync_Mipmap_Quality_Tolerant()
        {
            Assert.AreEqual(0, KvGraphicsModel.ParseVSync("Off", 1));
            Assert.AreEqual(1, KvGraphicsModel.ParseVSync("On", 0));
            Assert.AreEqual(2, KvGraphicsModel.ParseVSync("Every Second V-Blank", 0));
            Assert.AreEqual(1, KvGraphicsModel.ParseVSync("ерунда", 1), "неизвестное — исходное значение");

            Assert.AreEqual(0, KvGraphicsModel.ParseMipmap("Full", 2));
            Assert.AreEqual(1, KvGraphicsModel.ParseMipmap("1/2", 0));
            Assert.AreEqual(2, KvGraphicsModel.ParseMipmap("1/4", 0));
            Assert.AreEqual(3, KvGraphicsModel.ParseMipmap("1/8", 0));

            Assert.AreEqual(0, KvGraphicsModel.ParseQuality("Low", 3));
            Assert.AreEqual(1, KvGraphicsModel.ParseQuality("Среднее", 0));
            Assert.AreEqual(2, KvGraphicsModel.ParseQuality("high", 0));
            Assert.AreEqual(3, KvGraphicsModel.ParseQuality("Ultra", 0));
        }

        [Test]
        public void FromDto_AppliesJsonValues_AndKeepsFallbackForMissing()
        {
            KvGraphicsPresetDto dto = new KvGraphicsPresetDto();
            dto.id = "High";
            dto.renderScale = 0.8f;
            dto.shadows = "2048";
            dto.shadowDistance = 70f;
            dto.shadowCascades = 3;
            dto.ssao = "high";
            dto.ssr = "off";
            dto.reflectionProbes = "Baked+Realtime";
            dto.volumetricFog = "medium";
            dto.antiAliasing = "SMAA";
            dto.bloom = "on";
            dto.motionBlur = "off";
            dto.depthOfField = "on";
            dto.rayTracing = "auto";
            dto.textures = "1/2";
            dto.lodBias = 0.9f;
            dto.anisotropic = "force";
            dto.decals = "Ultra";
            dto.terrain = "Medium";
            dto.vsync = "Off";
            dto.frameCap = "144";
            dto.dynamicResolution = "on";
            dto.adaptiveQuality = "on";
            dto.targetFps = 100;

            KvGraphicsProfile p = KvGraphicsModel.FromDto(dto, KvGraphicsModel.Builtin(KvGraphicsPreset.High));
            Assert.AreEqual(0.8f, p.renderScale, 0.0001f);
            Assert.AreEqual(2048, p.shadowResolution);
            Assert.AreEqual(2, p.shadowQuality, "качество теней выводится из разрешения");
            Assert.AreEqual(70f, p.shadowDistance, 0.001f);
            Assert.AreEqual(3, p.shadowCascades);
            Assert.IsTrue(p.ssao);
            Assert.IsFalse(p.ssr);
            Assert.AreEqual(1, p.reflectionMode);
            Assert.IsTrue(p.volumetricFog);
            Assert.AreEqual(1, p.volumetricFogQuality);
            Assert.AreEqual((int)KvAntiAliasing.SMAA, p.antiAliasing);
            Assert.IsTrue(p.bloom);
            Assert.IsFalse(p.motionBlur);
            Assert.IsTrue(p.depthOfField);
            Assert.IsTrue(p.rayTracing, "«auto» означает «если поддерживается железом»");
            Assert.AreEqual(1, p.mipmapLimit);
            Assert.AreEqual(0.9f, p.lodBias, 0.0001f);
            Assert.AreEqual(2, p.anisotropic);
            Assert.AreEqual(3, p.decalQuality);
            Assert.AreEqual(1, p.terrainQuality);
            Assert.AreEqual(0, p.vsync);
            Assert.AreEqual((int)KvFrameCap.Fps144, p.frameCap);
            Assert.IsTrue(p.dynamicResolution);
            Assert.IsTrue(p.adaptiveQuality);
            Assert.AreEqual(100, p.targetFps);
        }

        [Test]
        public void FromDto_EmptyDto_LeavesBuiltinIntact()
        {
            KvGraphicsProfile basis = KvGraphicsModel.Builtin(KvGraphicsPreset.Medium);
            string before = basis.Signature();
            KvGraphicsProfile p = KvGraphicsModel.FromDto(new KvGraphicsPresetDto(), basis);
            Assert.AreEqual(before, p.Signature(), "пустой JSON-пресет ничего не меняет");
            Assert.AreEqual("Medium", p.id, "ключ берётся из основы");
        }

        // ================================================================== ЭТАП 1: железо

        [Test]
        public void LooksIntegrated_RecognizesIntegratedAndDiscrete()
        {
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("Intel(R) UHD Graphics 630", "Intel"));
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("Intel(R) Iris(R) Xe Graphics", "Intel"));
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("AMD Radeon Graphics", "AMD"));
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("AMD Radeon Vega 8", "AMD"));
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("Apple M2", "Apple"));
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("Microsoft Basic Render Driver", "Microsoft"));
            Assert.IsTrue(KvGraphicsHardwareProbe.LooksIntegrated("llvmpipe (LLVM 15.0.7)", ""));

            Assert.IsFalse(KvGraphicsHardwareProbe.LooksIntegrated("NVIDIA GeForce RTX 3070", "NVIDIA"));
            Assert.IsFalse(KvGraphicsHardwareProbe.LooksIntegrated("NVIDIA GeForce GTX 1660", "NVIDIA"));
            Assert.IsFalse(KvGraphicsHardwareProbe.LooksIntegrated("AMD Radeon RX 6600", "AMD"));
            Assert.IsFalse(KvGraphicsHardwareProbe.LooksIntegrated("Intel(R) Arc(TM) A770 Graphics", "Intel"));
            Assert.IsFalse(KvGraphicsHardwareProbe.LooksIntegrated("", ""), "пустое имя — не встроенная");
        }

        [Test]
        public void Classify_RulesFromSpec()
        {
            // VR-шлем важнее всего (ТЗ: приоритет над Ultra).
            KvGraphicsHardware vr = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, true, "Oculus Quest 3");
            Assert.AreEqual(KvHardwareClass.VRReady, Classify(vr), "подключённый шлем — VR-Ready");

            // VRAM ≥ 8 ГБ + трассировка — Ultra; без трассировки — тоже Ultra, но другая причина.
            KvGraphicsHardware ultra = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, false, "");
            Assert.AreEqual(KvHardwareClass.Ultra, Classify(ultra), "VRAM 8 ГБ — Ультра");
            KvGraphicsHardware ultraNoRt = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, false, false, false, "");
            Assert.AreEqual(KvHardwareClass.Ultra, Classify(ultraNoRt), "VRAM 8 ГБ без трассировки — всё равно Ультра");

            // 4–8 ГБ — High.
            KvGraphicsHardware high = MakeHardware("NVIDIA GeForce GTX 1660", 6144, 32768, 6, false, false, false, "");
            Assert.AreEqual(KvHardwareClass.High, Classify(high), "VRAM 4–8 ГБ — Высокое");

            // 2–4 ГБ — Medium.
            KvGraphicsHardware medium = MakeHardware("NVIDIA GeForce GTX 1050", 3072, 16384, 4, false, false, false, "");
            Assert.AreEqual(KvHardwareClass.Medium, Classify(medium), "VRAM 3 ГБ — Среднее");

            // ГРАНИЦА ДИАПАЗОНОВ ТЗ. В ТЗ «Среднее» описано как 2–4 ГБ, а «Высокое» —
            // как 4–8 ГБ, то есть ровно 4 ГБ попадает в оба описания. Класс видеокарты
            // из SystemInfo не прочитать, поэтому граница выбрана одна и проверяется явно:
            // ровно 4 ГБ считается «Высоким» (карты этого объёма — GTX 1650 / RTX 3050 —
            // уверенно тянут пресет High: тени 2048 и SSR).
            KvGraphicsHardware exactly4 = MakeHardware("NVIDIA GeForce GTX 1650", 4096, 16384, 4, false, false, false, "");
            Assert.AreEqual(KvHardwareClass.High, Classify(exactly4),
                "ровно 4 ГБ — граница диапазонов ТЗ, отнесена к «Высокому»");

            // Меньше 2 ГБ — Low.
            KvGraphicsHardware low = MakeHardware("NVIDIA GeForce GT 710", 1024, 8192, 2, false, false, false, "");
            Assert.AreEqual(KvHardwareClass.Low, Classify(low), "VRAM < 2 ГБ — Низкое");

            // Встроенная графика — Low (ТЗ).
            KvGraphicsHardware igpu = MakeHardware("Intel(R) UHD Graphics 630", 8192, 32768, 8, true, false, false, "");
            igpu.integrated = true;
            Assert.AreEqual(KvHardwareClass.Low, Classify(igpu), "встроенная графика — Низкое");

            // Нет вычислительных шейдеров — Low.
            KvGraphicsHardware noCompute = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, false, "");
            noCompute.computeShaders = false;
            Assert.AreEqual(KvHardwareClass.Low, Classify(noCompute), "без compute shaders — Низкое");
        }

        [Test]
        public void Classify_DowngradesByMemoryAndCpu()
        {
            KvGraphicsHardware hw = MakeHardware("NVIDIA GeForce RTX 3080", 10240, 16384, 8, true, false, false, "");
            Assert.AreEqual(KvHardwareClass.Ultra, Classify(hw), "до понижения — Ультра");

            hw.ramMb = 6144;
            Assert.AreEqual(KvHardwareClass.Medium, Classify(hw), "6 ГБ ОЗУ понижает Ультра до Среднего");

            hw.ramMb = 3072;
            Assert.AreEqual(KvHardwareClass.Low, Classify(hw), "3 ГБ ОЗУ понижает до Низкого");

            hw.ramMb = 32768;
            hw.cpuCores = 2;
            Assert.AreEqual(KvHardwareClass.Medium, Classify(hw), "два ядра — не выше Среднего");
        }

        [Test]
        public void Classify_AlwaysExplainsChoice()
        {
            KvGraphicsHardware[] samples =
            {
                MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, false, ""),
                MakeHardware("NVIDIA GeForce GTX 1660", 6144, 16384, 6, false, false, false, ""),
                MakeHardware("NVIDIA GeForce GT 710", 1024, 4096, 2, false, false, false, ""),
                MakeHardware("Intel(R) UHD Graphics 630", 4096, 8192, 4, false, false, false, ""),
                MakeHardware("NVIDIA GeForce RTX 4090", 24576, 65536, 16, true, false, true, "Valve Index")
            };
            foreach (KvGraphicsHardware hw in samples)
            {
                string reason;
                KvGraphicsHardwareProbe.Classify(hw, out reason);
                Assert.IsFalse(string.IsNullOrEmpty(reason),
                    "обоснование выбора обязано быть: «" + hw.gpu + "»");
            }
        }

        [Test]
        public void Classify_WithoutData_ReturnsUnknown()
        {
            string reason;
            Assert.AreEqual(KvHardwareClass.Unknown, KvGraphicsHardwareProbe.Classify(null, out reason));
            Assert.IsFalse(string.IsNullOrEmpty(reason), "без данных тоже есть обоснование");
            Assert.AreEqual(KvHardwareClass.Unknown,
                KvGraphicsHardwareProbe.Classify(new KvGraphicsHardware(), out reason),
                "неопрошенное железо (detected = false) — «не определён»");
        }

        [Test]
        public void HardwarePreset_MappingIsComplete()
        {
            Assert.AreEqual(KvGraphicsPreset.Low, KvGraphicsHardwareProbe.PresetOf(KvHardwareClass.Low));
            Assert.AreEqual(KvGraphicsPreset.Medium, KvGraphicsHardwareProbe.PresetOf(KvHardwareClass.Medium));
            Assert.AreEqual(KvGraphicsPreset.High, KvGraphicsHardwareProbe.PresetOf(KvHardwareClass.High));
            Assert.AreEqual(KvGraphicsPreset.Ultra, KvGraphicsHardwareProbe.PresetOf(KvHardwareClass.Ultra));
            Assert.AreEqual(KvGraphicsPreset.VRReady, KvGraphicsHardwareProbe.PresetOf(KvHardwareClass.VRReady));
        }

        // ================================================================== ЭТАП 5: хеш железа

        [Test]
        public void HardwareHash_IsStableAndReactsToChanges()
        {
            KvGraphicsHardware a = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, false, "");
            KvGraphicsHardware b = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, false, "");

            string ha = a.hash = KvGraphicsHardwareProbe.Hash(a);
            string hb = b.hash = KvGraphicsHardwareProbe.Hash(b);
            Assert.IsFalse(string.IsNullOrEmpty(ha), "хеш не пуст");
            Assert.AreEqual(ha, hb, "одинаковое железо — одинаковый хеш");
            Assert.AreEqual(32, ha.Length, "MD5 в шестнадцатеричном виде — 32 символа");

            b.vramMb = 4096;
            Assert.AreNotEqual(ha, KvGraphicsHardwareProbe.Hash(b), "смена объёма видеопамяти меняет хеш");

            KvGraphicsHardware c = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 32768, 8, true, false, false, "");
            c.cpu = "AMD Ryzen 9 5950X";
            Assert.AreNotEqual(ha, KvGraphicsHardwareProbe.Hash(c), "смена процессора меняет хеш");

            KvGraphicsHardware d = MakeHardware("NVIDIA GeForce RTX 3070", 8192, 65536, 8, true, false, false, "");
            Assert.AreNotEqual(ha, KvGraphicsHardwareProbe.Hash(d), "добавленная память меняет хеш");
        }

        [Test]
        public void HardwareHash_Probe_IsSafeWithoutGraphics()
        {
            // В пакетном прогоне без видеокарты опрос обязан не падать и вернуть объект.
            KvGraphicsHardware hw = KvGraphicsHardwareProbe.Probe();
            Assert.IsNotNull(hw, "опрос железа всегда возвращает объект");
            Assert.IsTrue(hw.detected, "объект помечен как опрошенный");
            Assert.IsFalse(string.IsNullOrEmpty(hw.hash), "хеш конфигурации заполнен");
            Assert.IsFalse(string.IsNullOrEmpty(hw.Short), "короткая подпись заполнена");
            Assert.IsFalse(string.IsNullOrEmpty(hw.Details()), "подробная карточка заполнена");
        }

        // ================================================================== профиль: нормализация и копия

        [Test]
        public void Normalize_ClampsOutOfRangeValues()
        {
            KvGraphicsProfile p = new KvGraphicsProfile();
            p.renderScale = 3f; p.shadowDistance = 9999f; p.shadowCascades = 9; p.lodBias = 9f;
            p.mipmapLimit = 7; p.antiAliasing = 99; p.ssaoIntensity = 12f; p.bloomIntensity = 5f;
            p.targetFps = 500; p.terrainQuality = 9; p.decalQuality = -3; p.vsync = 7; p.frameCap = 42;
            p.rayTracing = true;
            p.Normalize(false);

            Assert.AreEqual(1.5f, p.renderScale, 0.0001f);
            Assert.AreEqual(300f, p.shadowDistance, 0.001f);
            Assert.AreEqual(4, p.shadowCascades);
            Assert.AreEqual(2f, p.lodBias, 0.0001f);
            Assert.AreEqual(3, p.mipmapLimit);
            Assert.AreEqual(6, p.antiAliasing);
            Assert.AreEqual(4f, p.ssaoIntensity, 0.0001f);
            Assert.AreEqual(1f, p.bloomIntensity, 0.0001f);
            Assert.AreEqual(144, p.targetFps);
            Assert.AreEqual(2, p.terrainQuality);
            Assert.AreEqual(0, p.decalQuality);
            Assert.AreEqual(2, p.vsync);
            Assert.AreEqual(6, p.frameCap);
            Assert.IsFalse(p.rayTracing, "трассировка лучей выключается, если железо её не поддерживает");
        }

        [Test]
        public void Clone_IsIndependentCopy()
        {
            KvGraphicsProfile source = KvGraphicsModel.Builtin(KvGraphicsPreset.Ultra);
            KvGraphicsProfile copy = source.Clone();
            Assert.AreEqual(source.Signature(), copy.Signature(), "копия совпадает с источником");

            copy.renderScale = 0.5f;
            copy.id = "Custom";
            copy.shadowQuality = 0;
            Assert.AreNotEqual(source.Signature(), copy.Signature(), "правка копии не влияет на пресет");
            Assert.AreEqual(1f, source.renderScale, 0.0001f, "пресет не изменился");
        }

        // ================================================================== поставленный файл пресетов

        [Test]
        public void ShippedJsonFile_IsValidAndMatchesSpec()
        {
            string path = Path.Combine(Application.streamingAssetsPath, KvGraphicsService.FileName);
            Assert.IsTrue(File.Exists(path),
                "файл пресетов поставлен вместе с проектом: " + KvGraphicsService.FileName);

            string json = File.ReadAllText(path);
            Assert.IsFalse(string.IsNullOrEmpty(json), "файл пресетов не пуст");

            KvGraphicsFile file = JsonUtility.FromJson<KvGraphicsFile>(json);
            Assert.IsNotNull(file, "файл пресетов разбирается");
            Assert.IsNotNull(file.presets, "в файле есть массив пресетов");
            Assert.AreEqual(5, file.presets.Length, "в файле пять пресетов (Low…VR-Ready)");

            Dictionary<string, KvGraphicsProfile> byKey = new Dictionary<string, KvGraphicsProfile>();
            foreach (KvGraphicsPresetDto dto in file.presets)
            {
                KvGraphicsPreset key = KvGraphicsModel.PresetOf(dto.id);
                Assert.AreNotEqual(KvGraphicsPreset.Auto, key, "пресет имеет известный ключ: " + dto.id);
                KvGraphicsProfile p = KvGraphicsModel.FromDto(dto, KvGraphicsModel.Builtin(key));
                p.Normalize(true);
                byKey[KvGraphicsModel.KeyOf(key)] = p;
            }

            Assert.AreEqual(0.7f, byKey["Low"].renderScale, 0.0001f, "Low: масштаб из файла");
            Assert.AreEqual(0.85f, byKey["Medium"].renderScale, 0.0001f);
            Assert.AreEqual(1f, byKey["High"].renderScale, 0.0001f);
            Assert.AreEqual(1f, byKey["Ultra"].renderScale, 0.0001f);
            Assert.AreEqual(0.9f, byKey["VRReady"].renderScale, 0.0001f);

            // Файл — источник истины после правки «без перекомпиляции», поэтому он обязан
            // совпадать с таблицей ТЗ по ключевым числам.
            Assert.AreEqual(512, byKey["Low"].shadowResolution);
            Assert.AreEqual(1024, byKey["Medium"].shadowResolution);
            Assert.AreEqual(2048, byKey["High"].shadowResolution);
            Assert.AreEqual(4096, byKey["Ultra"].shadowResolution);
            Assert.AreEqual(1024, byKey["VRReady"].shadowResolution);

            Assert.AreEqual(30f, byKey["Low"].shadowDistance, 0.001f);
            Assert.AreEqual(40f, byKey["VRReady"].shadowDistance, 0.001f);
            Assert.AreEqual((int)KvFrameCap.Fps90, byKey["VRReady"].frameCap, "VR-Ready: предел кадров 90");
            Assert.AreEqual((int)KvFrameCap.Unlimited, byKey["Ultra"].frameCap, "Ultra: без предела");
            Assert.AreEqual((int)KvAntiAliasing.TAA, byKey["High"].antiAliasing);
            Assert.AreEqual(2, byKey["Low"].mipmapLimit, "Low: текстуры 1/4");
            Assert.AreEqual(0, byKey["High"].mipmapLimit, "High: полные текстуры");
            Assert.IsFalse(byKey["Low"].ssao, "Low: SSAO выключено и в файле");
            Assert.IsTrue(byKey["High"].ssr, "High: SSR включены и в файле");
            Assert.IsFalse(byKey["VRReady"].volumetricFog, "VR-Ready: объёмный туман выключен");
        }

        // ================================================================== ЭТАП 6: локализация

        /// <summary>
        /// Все ключи раздела зарегистрированы во ВСЕХ семи языках: у каждого ключа семь
        /// непустых переводов (значения могут совпадать — «TAA», «VR-Ready» — языков это
        /// не отменяет, поэтому считаются именно НЕПУСТЫЕ значения).
        /// </summary>
        [Test]
        public void Localization_AllKeysPresentInSevenLanguages()
        {
            KvLocExtra4.Install();
            Assert.AreEqual(KvLocExtra.Codes.Length, 7, "в проекте семь языков");
            Assert.IsTrue(KvLocExtra4.RowCount > 80,
                "строк раздела «Графика» больше 80, всего: " + KvLocExtra4.RowCount);

            List<string> missing = new List<string>();
            for (int i = 0; i < KvLocExtra4.RowCount; i++)
            {
                string key = KvLocExtra4.KeyAt(i);
                if (string.IsNullOrEmpty(key)) continue;
                string all = KvLoc.AllLanguages(key);
                int languages = 0;
                if (!string.IsNullOrEmpty(all))
                {
                    string[] parts = all.Split('|');
                    for (int c = 0; c < parts.Length; c++)
                        if (!string.IsNullOrEmpty(parts[c].Trim())) languages++;
                }
                if (languages < 7) missing.Add(key + " (" + languages + "/7)");
            }
            Assert.AreEqual(0, missing.Count,
                "нет перевода на часть языков: " + string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void Localization_KeysUseGraphicsPrefix()
        {
            int graphics = 0;
            int commands = 0;
            for (int i = 0; i < KvLocExtra4.RowCount; i++)
            {
                string key = KvLocExtra4.KeyAt(i);
                if (string.IsNullOrEmpty(key)) continue;
                if (key.StartsWith("graphics.")) graphics++;
                else if (key.StartsWith("cmd.graphics.")) commands++;
                else Assert.Fail("ключ вне раздела «Графика»: " + key);
            }
            Assert.IsTrue(graphics > 60, "ключей graphics.* больше 60, найдено: " + graphics);
            Assert.AreEqual(10, commands, "у пяти команд раздела есть заголовок и пояснение");
        }

        // ================================================================== ФИКС 13: покрытие ВСЕХ модулей

        /// <summary>
        /// ФИКС 13 (сессия §19). Раньше проверялся ТОЛЬКО раздел «Графика» (`KvLocExtra4`):
        /// у остальных модулей новых строк даже не было доступа к таблице, поэтому «все 7 языков»
        /// фактически означало «7 языков одного раздела». Теперь проверяются ВСЕ ЧЕТЫРЕ модуля
        /// (`KvLocExtra` 1–4): у каждого ключа каждого модуля обязаны быть семь непустых
        /// переводов (RU / EN / ZH / ES / DE / FR / JA). Именно эти модули и добавляли строки
        /// в фиксах 1–12, о которых спрашивал ТЗ.
        /// </summary>
        [Test]
        public void Localization_AllModulesAllKeysInSevenLanguages()
        {
            KvLocExtra.Install();
            KvLocExtra2.Install();
            KvLocExtra3.Install();
            KvLocExtra4.Install();
            Assert.AreEqual(7, KvLocExtra.Codes.Length, "в проекте семь языков");

            List<string> missing = new List<string>();
            HashSet<string>[] maps = LanguageKeys();
            int total = 0;
            total += CheckModule("KvLocExtra", KvLocExtra.RowCount, KvLocExtra.KeyAt, maps, missing);
            total += CheckModule("KvLocExtra2", KvLocExtra2.RowCount, KvLocExtra2.KeyAt, maps, missing);
            total += CheckModule("KvLocExtra3", KvLocExtra3.RowCount, KvLocExtra3.KeyAt, maps, missing);
            total += CheckModule("KvLocExtra4", KvLocExtra4.RowCount, KvLocExtra4.KeyAt, maps, missing);

            Assert.IsTrue(total > 500, "все модули локализации вместе дают больше 500 ключей, есть: " + total);
            Assert.AreEqual(0, missing.Count,
                "нет перевода на часть языков (" + missing.Count + " ключей): " +
                string.Join(", ", missing.ToArray()));
        }

        /// <summary>Проверить один модуль локализации; вернуть число ключей, дописать неполные.</summary>
        private static int CheckModule(string module, int rows, Func<int, string> keyAt,
            HashSet<string>[] maps, List<string> missing)
        {
            int keys = 0;
            for (int i = 0; i < rows; i++)
            {
                string key = keyAt(i);
                if (string.IsNullOrEmpty(key)) continue;
                keys++;
                int languages = LanguagesOf(maps, key);
                if (languages < 7) missing.Add(module + ":" + key + " (" + languages + "/7)");
            }
            return keys;
        }

        /// <summary>
        /// Сколько из СЕМИ языков реально содержат перевод ключа. Проверяем по КАТАЛОГУ языка
        /// (`KvLoc.KeysOf`), а не по склейке `AllLanguages`: склейка считает «части», и один
        /// перевод, содержащий символ «|», дал бы лишний язык. В каталог же попадают только
        /// НЕПУСТЫЕ строки (см. `KvLocExtra.Install`), поэтому наличие ключа = наличие перевода.
        /// Наборы ключей языков берутся один раз — иначе на 500+ ключей × 7 языков тест
        /// пересобирал бы списки сотни раз.
        /// </summary>
        private static HashSet<string>[] LanguageKeys()
        {
            var maps = new HashSet<string>[KvLocExtra.Codes.Length];
            for (int c = 0; c < KvLocExtra.Codes.Length; c++)
                maps[c] = new HashSet<string>(KvLoc.KeysOf(KvLocExtra.Codes[c]));
            return maps;
        }

        private static int LanguagesOf(HashSet<string>[] maps, string key)
        {
            if (string.IsNullOrEmpty(key) || maps == null) return 0;
            int languages = 0;
            for (int c = 0; c < maps.Length; c++)
                if (maps[c] != null && maps[c].Contains(key)) languages++;
            return languages;
        }

        /// <summary>
        /// ФИКС 13. Конкретно НОВЫЕ ключи этой сессии (§19): строка про опцию «сглаживать только
        /// выбранный вариант» (ФИКС 7 §18) и обновлённая честная помета модели отказов
        /// (ФИКС 8 §18) — они обязаны быть переведены на семь языков.
        /// </summary>
        [Test]
        public void Localization_NewKeysOfThisSessionCoverSevenLanguages()
        {
            KvLocExtra.Install();
            KvLocExtra3.Install();
            string[] keys = { "smooth.auto.selected", "smooth.auto.cost", "fail.info" };
            HashSet<string>[] maps = LanguageKeys();
            List<string> missing = new List<string>();
            foreach (string key in keys)
                if (LanguagesOf(maps, key) < 7)
                    missing.Add(key + " (" + LanguagesOf(maps, key) + "/7)");
            Assert.AreEqual(0, missing.Count,
                "новые строки сессии переведены не на все 7 языков: " +
                string.Join(", ", missing.ToArray()));
        }

        [Test]
        public void Localization_SampleStringsDifferPerLanguage()
        {
            KvLocExtra4.Install();
            string en = KvLoc.AllLanguages("graphics.section.quality");
            Assert.IsTrue(en.Contains("Quality"), "английский перевод раздела «Качество»: " + en);
            Assert.IsTrue(en.Contains("画质"), "китайский перевод раздела «Качество»: " + en);
            Assert.IsTrue(en.Contains("Qualität"), "немецкий перевод раздела «Качество»: " + en);
            Assert.IsTrue(en.Contains("画質"), "японский перевод раздела «Качество»: " + en);
        }

        // ================================================================== §19: проверки хвостов

        /// <summary>
        /// ФИКС 6 (§19). Хвост «пальцы гриппера SCARA» закрывался переносом раскрытия при
        /// пересборке захвата. Логика переноса проверяется здесь БЕЗ сцены (сам `Attach` требует
        /// живого робота — он проверяется прогоном `DshScaraDiag`).
        ///
        /// ВАЖНОЕ УТОЧНЕНИЕ, найденное ЭТИМ тестом: только что созданный `KvGripper` находится
        /// в ЗАКРЫТОМ состоянии (`Open01 = 0`, ширина = `closedWidth` = 12 мм). Раскрытие
        /// в «разжат» делает ПЕРВАЯ сборка захвата на роботе (`Attach`: `keepOpen01 = root != null
        /// ? Open01 : 1f`), то есть на живом роботе захват появляется разжатым. Тест фиксирует
        /// оба факта — именно они и объясняют прежний дефект «пальцы сами разжимаются»: важна
        /// не ширина по умолчанию, а то, что ПЕРЕСБОРКА больше не сбрасывает состояние.
        /// </summary>
        [Test]
        public void Gripper_NewInstance_StartsClosedAndKeepsWidthConsistent()
        {
            KvGripper gripper = new KvGripper();
            Assert.IsFalse(gripper.IsOpen, "новый захват считается сжатым (до первой сборки)");
            Assert.AreEqual(0f, gripper.Open01, 0.0001f, "раскрытие нового захвата = 0 (сжат)");
            Assert.AreEqual(gripper.closedWidth, gripper.Width, 0.0001f,
                "ширина нового захвата равна closedWidth (" + gripper.closedWidth + " м)");
            Assert.IsFalse(gripper.Attached, "захват ни на чём не собран");
            Assert.IsNull(gripper.Robot, "захват не помнит робота до сборки");
            Assert.IsFalse(gripper.AttachedTo(null), "на пустом роботе захват не собран");
            Assert.AreEqual(0.075f, gripper.openWidth, 0.0001f, "раскрытие по ТЗ — 75 мм");
            Assert.AreEqual(0.012f, gripper.closedWidth, 0.0001f, "смыкание по ТЗ — 12 мм");
            Assert.IsTrue(gripper.GraspDrop > 0f, "точка захвата смещена вдоль инструмента");
        }

        /// <summary>
        /// ФИКС 7 (§19). Хвост «preset-позы»: интерфейс обязан ФИЛЬТРОВАТЬ позы по числу осей,
        /// иначе чужая поза доходит до переезда и отказ выглядит как «не поехал». Здесь
        /// проверяется контракт данных позы: у позы старого формата (пустое имя робота, 6 углов)
        /// сохраняются все углы, а у позы SCARA — три. Именно по этому числу `ForCurrentRobot`
        /// и отсеивает чужие позы (проверяется прогоном — там нужен живой робот потока).
        /// </summary>
        [Test]
        public void PosePreset_AnglesAndDofContract()
        {
            KvPosePreset six = new KvPosePreset { q = new float[] { 1, 2, 3, 4, 5, 6 } };
            KvPosePreset three = new KvPosePreset { q = new float[] { 10, 20, 30 } };
            six.Normalize();
            three.Normalize();

            Assert.AreEqual(6, six.ToDoubles().Length, "поза робота с кистью — 6 углов");
            Assert.AreEqual(3, three.ToDoubles().Length, "поза SCARA — 3 угла");
            Assert.AreNotEqual(six.ToDoubles().Length, three.ToDoubles().Length,
                "число осей и есть признак, по которому поза фильтруется для текущего робота");
            Assert.IsFalse(string.IsNullOrEmpty(six.id), "у позы есть идентификатор");
            Assert.IsFalse(string.IsNullOrEmpty(six.name), "у позы есть имя");
            Assert.AreEqual("Поза", six.name, "имя по умолчанию — «Поза»");
        }

        /// <summary>
        /// ФИКС 7 (§19). Сценарий ТЗ: «сохранить 3 позы подряд, удалить среднюю, перейти
        /// в первую». Проверяется складская часть (файлы — это и есть хранилище поз проекта):
        /// три позы записываются, читаются, средняя удаляется, первая остаётся на месте.
        /// Живёт это в `persistentDataPath` (вне проекта), поэтому тест не меняет файлы проекта
        /// и убирает за собой все созданные файлы.
        /// </summary>
        [Test]
        public void PoseStore_SaveThreeDeleteMiddleFirstSurvives()
        {
            string tag = "dsh_test_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            List<string> created = new List<string>();
            try
            {
                // `KvPoseStore.Save` возвращает ПУТЬ записанного файла ("" — отказ).
                string pathA = KvPoseStore.Save(new KvPosePreset
                {
                    name = tag + "_1", robot = tag, q = new float[] { 1, 2, 3 },
                    created = FeatureStorage.IsoNow()
                });
                string pathB = KvPoseStore.Save(new KvPosePreset
                {
                    name = tag + "_2", robot = tag, q = new float[] { 4, 5, 6 },
                    created = FeatureStorage.IsoNow()
                });
                string pathC = KvPoseStore.Save(new KvPosePreset
                {
                    name = tag + "_3", robot = tag, q = new float[] { 7, 8, 9 },
                    created = FeatureStorage.IsoNow()
                });

                Assert.IsFalse(string.IsNullOrEmpty(pathA), "первая поза записана: " + pathA);
                Assert.IsFalse(string.IsNullOrEmpty(pathB), "вторая поза записана: " + pathB);
                Assert.IsFalse(string.IsNullOrEmpty(pathC), "третья поза записана: " + pathC);
                created.Add(pathA);
                created.Add(pathB);
                created.Add(pathC);
                Assert.IsTrue(File.Exists(pathA), "файл первой позы существует: " + pathA);

                // Удаляем СРЕДНЮЮ — первая и третья обязаны остаться.
                KvPosePreset middle = FeatureStorage.LoadJson<KvPosePreset>(pathB);
                Assert.IsNotNull(middle, "средняя поза читается обратно");
                middle.filePath = pathB;
                Assert.IsTrue(KvPoseStore.Delete(middle), "средняя поза удалена");
                created.Remove(pathB);

                Assert.IsFalse(File.Exists(pathB), "файла средней позы больше нет");
                Assert.IsTrue(File.Exists(pathA), "первая поза на месте после удаления средней");
                Assert.IsTrue(File.Exists(pathC), "третья поза на месте после удаления средней");

                KvPosePreset first = FeatureStorage.LoadJson<KvPosePreset>(pathA);
                Assert.IsNotNull(first, "первая поза читается (в неё и будет переезд)");
                Assert.AreEqual(3, first.ToDoubles().Length, "у первой позы три угла");
                Assert.AreEqual(1.0, first.ToDoubles()[0], 1e-4, "углы первой позы не изменились");
            }
            finally
            {
                foreach (string path in created)
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) File.Delete(path);
            }
        }

        // ================================================================== вспомогательное

        private static KvGraphicsHardware MakeHardware(string gpu, int vramMb, int ramMb, int cores,
            bool rayTracing, bool integrated, bool xr, string xrDevice)
        {
            KvGraphicsHardware hw = new KvGraphicsHardware();
            hw.gpu = gpu;
            hw.vendor = gpu.StartsWith("NVIDIA") ? "NVIDIA" : (gpu.StartsWith("Intel") ? "Intel" : "AMD");
            hw.api = "Direct3D12";
            hw.apiVersion = "Direct3D 12.0 [level 12.1]";
            hw.vramMb = vramMb;
            hw.ramMb = ramMb;
            hw.cpu = "Intel Core i7";
            hw.cpuCores = cores;
            hw.cpuMhz = 3600;
            hw.rayTracing = rayTracing;
            hw.computeShaders = true;
            hw.asyncReadback = true;
            hw.instancing = true;
            hw.screenWidth = 1920;
            hw.screenHeight = 1080;
            hw.screenHz = 60;
            hw.integrated = integrated;
            hw.xrPresent = xr;
            hw.xrDevice = xrDevice;
            hw.detected = true;
            hw.hash = KvGraphicsHardwareProbe.Hash(hw);
            return hw;
        }

        private static KvHardwareClass Classify(KvGraphicsHardware hw)
        {
            string reason;
            KvHardwareClass cls = KvGraphicsHardwareProbe.Classify(hw, out reason);
            Assert.IsFalse(string.IsNullOrEmpty(reason), "обоснование выбора не пусто");
            return cls;
        }
    }
}

