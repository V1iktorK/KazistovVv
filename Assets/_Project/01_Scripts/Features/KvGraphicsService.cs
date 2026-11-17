using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.XR;
using KazistovVvUI;

namespace KazistovVvFeatures
{
    /// <summary>Ключи PlayerPrefs раздела «Графика» (ТЗ: префикс `KazistovVv.Graphics.*`).</summary>
    public static class KvGraphicsPrefs
    {
        /// <summary>Выбранный режим (значение <see cref="KvGraphicsPreset"/> числом).</summary>
        public const string KeyPreset = "KazistovVv.Graphics.Preset";
        /// <summary>Пресет, выбранный автоопределением (строка: Low…VRReady).</summary>
        public const string KeyAutoPreset = "KazistovVv.Graphics.AutoPreset";
        /// <summary>Флаг «автоопределение уже выполнялось» (0/1).</summary>
        public const string KeyAutoDetected = "KazistovVv.Graphics.AutoDetected";
        /// <summary>Хеш конфигурации железа (MD5).</summary>
        public const string KeyHardwareHash = "KazistovVv.Graphics.HardwareHash";
        /// <summary>Снимок сведений о железе (JSON) — чтобы объяснить выбор без повторного опроса.</summary>
        public const string KeyHardware = "KazistovVv.Graphics.Hardware";
        /// <summary>Обоснование автоопределения (текст).</summary>
        public const string KeyAutoReason = "KazistovVv.Graphics.AutoReason";
        /// <summary>Текущий профиль (JSON) — включая режим «Свой».</summary>
        public const string KeyProfile = "KazistovVv.Graphics.Profile";
        /// <summary>Служебный режим: полный журнал применения настроек (0/1).</summary>
        public const string KeyDebug = "KazistovVv.Graphics.DebugMode";
        /// <summary>ФИКС 1: выбранный оператором режим отрисовки HDRP (0/1/2), −1 — «как в ассете».</summary>
        public const string KeyLitShaderMode = "KazistovVv.Graphics.LitShaderMode";
    }

    /// <summary>
    /// СЛУЖБА ГРАФИКИ (этапы 1–5 и 7 ТЗ): автоопределение железа, пресеты, применение
    /// настроек в HDRP и QualitySettings, хранение выбора, адаптивное качество, бенчмарк
    /// и метрики.
    ///
    /// ГРАНИЦЫ ОТВЕТСТВЕННОСТИ. Служба НЕ трогает роботов, кинематику, планировщик,
    /// State Machine, лазеры и фантомы: она работает только с рендером. Всё, что она
    /// меняет, перечислено в <see cref="ApplyReport"/> построчно — в служебном режиме
    /// (<see cref="DebugMode"/>) это пишется и в журнал действий.
    ///
    /// ЧТО ИМЕННО МЕНЯЕТСЯ (и почему именно так):
    ///   * QualitySettings — синхронизация кадров, предел кадров, смещение LOD, предел
    ///     мип-уровней (текстуры), анизотропия, отражения в реальном времени;
    ///   * HDRP-ассет — опорные возможности конвейера (SSAO, SSR, объёмный туман, декали,
    ///     трассировка лучей, MSAA, разрешение карт теней, размер кэша отражений,
    ///     динамическое разрешение). Ассет правится ОДИН раз за применение: присвоение
    ///     `currentPlatformRenderPipelineSettings` пересобирает конвейер, поэтому все
    ///     изменения собираются в одну структуру;
    ///   * КАМЕРА (`HDAdditionalCameraData`) — режим сглаживания и разрешение динамического
    ///     разрешения. Это объект сцены: в PlayMode правки не сохраняются в сцену;
    ///   * СОБСТВЕННЫЙ ГЛОБАЛЬНЫЙ ТОМ (`Volume`, приоритет 100) — свечение, размытие
    ///     движения, глубина резкости, SSAO, SSR, объёмный туман, дистанция и каскады
    ///     теней. Том создаётся В РАНТАЙМЕ со своим профилем, поэтому НИ ОДИН файл-профиль
    ///     проекта не меняется. Переопределяются ТОЛЬКО те параметры, которыми управляет
    ///     пресет: всё остальное берётся из профилей сцены как раньше;
    ///   * РЕЛЬЕФ — детализация Terrain только в PlayMode.
    ///
    /// ЧЕГО СЛУЖБА НЕ ДЕЛАЕТ НИКОГДА: не меняет формат стерео (Single Pass Instanced),
    /// не трогает `XRDevice.fovZoomFactor`, не перезагружает сцену, не вызывает
    /// `AssetDatabase.SaveAssets`, не выключает URP.
    ///
    /// В РЕЖИМЕ БЕЗ ГРАФИКИ (`KvGraphics.Available == false`, пакетный `-nographics`)
    /// служба ВСЁ СЧИТАЕТ и честно пишет в журнал, что настройки не применены.
    /// </summary>
    public class KvGraphicsService
    {
        /// <summary>Имя файла пресетов в StreamingAssets (правится без перекомпиляции).</summary>
        public const string FileName = "kazistovvv_graphics.json";

        /// <summary>Сколько кадров держать для бенчмарка (страховка от переполнения).</summary>
        private const int BenchmarkCapacity = 20000;

        // ------------------------------------------------------------------ единственный экземпляр

        private static KvGraphicsService current;

        /// <summary>Активная служба (null, пока не запущена).</summary>
        public static KvGraphicsService Current { get { return current; } }

        /// <summary>
        /// Запустить службу (повторный вызов возвращает уже созданную). В PlayMode
        /// создаётся скрытый узел-носитель, который сам вызывает <see cref="Tick"/>.
        /// </summary>
        public static KvGraphicsService EnsureStarted()
        {
            if (current != null) return current;
            current = new KvGraphicsService();
            current.Start();
            return current;
        }

        /// <summary>Остановить службу (снять метрики, вернуть исходные настройки HDRP-ассета).</summary>
        public static void Shutdown()
        {
            if (current == null) return;
            current.Stop();
            current = null;
        }

        // ------------------------------------------------------------------ состояние

        private readonly Dictionary<KvGraphicsPreset, KvGraphicsProfile> presets =
            new Dictionary<KvGraphicsPreset, KvGraphicsProfile>();
        private readonly Dictionary<KvGraphicsPreset, string> presetTitles =
            new Dictionary<KvGraphicsPreset, string>();

        private KvGraphicsProfile active = new KvGraphicsProfile();
        private KvGraphicsPreset preset = KvGraphicsPreset.Auto;
        private KvGraphicsPreset autoPreset = KvGraphicsPreset.High;
        private string autoReason = "";
        private string loadedFile = "";
        private string loadedStatus = "файл пресетов ещё не читался";
        private string applyReport = "";
        private bool started;
        private bool detected;
        private bool warnedNoGraphics;

        private GameObject volumeGo;
        private Volume volume;
        private VolumeProfile volumeProfile;
        private Bloom volumeBloom;
        private MotionBlur volumeMotionBlur;
        private DepthOfField volumeDof;
        private ScreenSpaceAmbientOcclusion volumeSsao;
        private ScreenSpaceReflection volumeSsr;
        private Fog volumeFog;
        private HDShadowSettings volumeShadows;

        private bool assetSnapshotTaken;
        private RenderPipelineSettings assetSnapshot;
        private bool assetTouched;
        private string assetName = "";

        // --- ФИКС 1: режим отрисовки HDRP (MSAA работает только при прямой отрисовке)
        private int litShaderMode = -1;          // −1 — «как в ассете» (оператор ещё не выбирал)
        private int pendingLitShaderMode = -1;   // запрошенный, но НЕ подтверждённый режим
        private float pendingLitShaderTimer;     // сколько осталось на подтверждение
        private string litShaderReport = "";     // что именно произошло при последней смене

        /// <summary>
        /// ФИКС 1: какое сглаживание оператор (или пресет) просил ДО подмены на TAA из-за
        /// отложенного режима. Нужно, чтобы после перехода на прямую отрисовку вернуть
        /// именно MSAA, а не «оставить TAA». −1 — подмены не было.
        /// </summary>
        private int msaaSubstitutedFrom = -1;

        /// <summary>Сколько секунд ждём подтверждения смены режима отрисовки (как у пуска).</summary>
        public const float ConfirmSeconds = 20f;

        private KvGraphicsRuntime host;

        // --- адаптивное качество
        private float adaptiveTimer;
        private float statusTimer;
        private float hardwareTimer = 60f;
        /// <summary>Отложенное сообщение о правке настроек (&gt; 0 — таймер идёт).</summary>
        private float announceDelay;
        private float adaptiveBelowFor;
        private float adaptiveAboveFor;
        private float adaptiveScale = 1f;
        private float warnCooldown;
        private string adaptiveNote = "";

        // --- бенчмарк
        private bool benchmarkRunning;
        private float benchmarkLeft;
        private float benchmarkTotal = 5f;
        private readonly List<float> benchmarkFrames = new List<float>(1024);
        private string benchmarkReport = "";

        // --- метрики
        private ProfilerRecorder drawCalls;
        private ProfilerRecorder triangles;
        private ProfilerRecorder textureMemory;
        private ProfilerRecorder setPassCalls;
        private bool recordersReady;
        private float metricsFps;
        private float metricsFrameMs;

        // ------------------------------------------------------------------ публичные свойства

        /// <summary>Сведения о железе (заполняются при старте и по кнопке «Определить по железу»).</summary>
        public KvGraphicsHardware Hardware { get; private set; }

        /// <summary>Текущий профиль (то, что реально применено).</summary>
        public KvGraphicsProfile Active { get { return active; } }

        /// <summary>Выбранный режим (может быть «Авто» — тогда смотрите <see cref="AutoPresetValue"/>).</summary>
        public KvGraphicsPreset Preset { get { return preset; } }

        /// <summary>Пресет, выбранный автоопределением.</summary>
        public KvGraphicsPreset AutoPresetValue { get { return autoPreset; } }

        /// <summary>Обоснование автоопределения («VRAM 4 ГБ → высокое»).</summary>
        public string AutoReason { get { return autoReason; } }

        /// <summary>Есть ли рабочая графика (иначе настройки только считаются).</summary>
        public bool Available { get { return KvGraphics.Available; } }

        /// <summary>Можно ли применять настройки: нужна графика и режим воспроизведения.</summary>
        public bool CanApply { get { return KvGraphics.Available && Application.isPlaying; } }

        /// <summary>Служебный режим: полный журнал применения (ТЗ, этап 3.5).</summary>
        public bool DebugMode
        {
            get { return PlayerPrefs.GetInt(KvGraphicsPrefs.KeyDebug, 0) != 0; }
            set
            {
                PlayerPrefs.SetInt(KvGraphicsPrefs.KeyDebug, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Отчёт последнего применения (построчно, только то, что реально изменено).</summary>
        public string ApplyReport { get { return applyReport; } }

        /// <summary>Статус файла пресетов (для вкладки «Диагностика»).</summary>
        public string PresetFileStatus { get { return loadedStatus; } }

        /// <summary>Путь файла пресетов.</summary>
        public string PresetFilePath
        {
            get
            {
                try { return Path.Combine(Application.streamingAssetsPath, FileName); }
                catch (Exception) { return FileName; }
            }
        }

        /// <summary>Идёт ли замер кадровой частоты.</summary>
        public bool BenchmarkRunning { get { return benchmarkRunning; } }

        /// <summary>Отчёт последнего бенчмарка.</summary>
        public string BenchmarkReport { get { return benchmarkReport; } }

        /// <summary>Средняя кадровая частота за последнюю секунду (для оверлея и адаптива).</summary>
        public float Fps { get { return metricsFps; } }

        /// <summary>Время кадра, миллисекунды.</summary>
        public float FrameMs { get { return metricsFrameMs; } }

        /// <summary>Пояснение адаптивного режима (последнее решение).</summary>
        public string AdaptiveNote { get { return adaptiveNote; } }

        /// <summary>Запущена ли служба.</summary>
        public bool Started { get { return started; } }

        /// <summary>Выполнялось ли автоопределение (флаг из PlayerPrefs ИЛИ этой сессии).</summary>
        public bool AutoDetected
        {
            get { return detected || PlayerPrefs.GetInt(KvGraphicsPrefs.KeyAutoDetected, 0) != 0; }
        }

        // ================================================================== ФИКС 1: режим отрисовки

        /// <summary>
        /// ФИКС 1. РЕЖИМ ОТРИСОВКИ, который РЕАЛЬНО стоит в HDRP-ассете (читается из ассета,
        /// а не из памяти): так интерфейс не врёт, даже если ассет правили руками.
        /// `supportedLitShaderMode` — ФЛАГОВОЕ поле (ForwardOnly 1 / DeferredOnly 2 / Both 3).
        /// </summary>
        public KvLitShaderMode AssetLitShaderMode()
        {
            HDRenderPipelineAsset asset = PipelineAsset();
            if (asset == null) return KvLitShaderMode.Deferred;
            RenderPipelineSettings s = asset.currentPlatformRenderPipelineSettings;
            int raw = (int)s.supportedLitShaderMode;
            if ((raw & (int)RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly) != 0 &&
                (raw & (int)RenderPipelineSettings.SupportedLitShaderMode.DeferredOnly) != 0)
                return KvLitShaderMode.Both;
            if ((raw & (int)RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly) != 0)
                return KvLitShaderMode.Forward;
            return KvLitShaderMode.Deferred;
        }

        /// <summary>Выбор оператора (для интерфейса): −1 — «не выбирал, как в ассете».</summary>
        public int LitShaderModePreference
        {
            get
            {
                if (litShaderMode < 0)
                    litShaderMode = PlayerPrefs.GetInt(KvGraphicsPrefs.KeyLitShaderMode, -1);
                return litShaderMode;
            }
        }

        /// <summary>
        /// Есть ли прямая отрисовка в текущем ассете (тогда MSAA реально работает).
        /// Проверка ПОБИТОВАЯ: `Both` (3) тоже содержит прямую отрисовку.
        /// </summary>
        public bool ForwardEnabled
        {
            get
            {
                return ((int)AssetLitShaderMode() &
                        (int)RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly) != 0;
            }
        }

        /// <summary>Идёт ли ожидание подтверждения смены режима отрисовки.</summary>
        public bool LitShaderPending { get { return pendingLitShaderMode >= 0; } }
        /// <summary>Запрошенный (но не подтверждённый) режим отрисовки.</summary>
        public KvLitShaderMode PendingLitShaderMode
        {
            get { return (KvLitShaderMode)Mathf.Clamp(pendingLitShaderMode, 0, 2); }
        }
        /// <summary>Сколько секунд осталось на подтверждение (0 — окно закрыто).</summary>
        public float LitShaderCountdown { get { return Mathf.Max(0f, pendingLitShaderTimer); } }
        /// <summary>Отчёт о последней смене режима отрисовки (для вкладки и журнала).</summary>
        public string LitShaderReport { get { return litShaderReport; } }

        /// <summary>
        /// ФИКС 1. Напоминание оператору в начале сессии: прошлый выбор режима отрисовки
        /// хранится в PlayerPrefs, но САМ HDRP-ассет служба при выходе возвращает в исходное
        /// состояние (§17.4: правки ассета не «залипают»). Поэтому режим НЕ включается сам —
        /// оператор подтверждает его одной кнопкой. Так честно: тяжёлая операция без спроса
        /// не выполняется, но и не забывается.
        /// </summary>
        private void NoteSavedLitShaderMode()
        {
            int saved = PlayerPrefs.GetInt(KvGraphicsPrefs.KeyLitShaderMode, -1);
            if (saved < 0) return;
            KvLitShaderMode assetMode = AssetLitShaderMode();
            if ((int)assetMode == saved) return;
            litShaderReport = KvLocExtra4.F("graphics.raster.saved",
                "В прошлой сессии был выбран режим отрисовки «{0}», сейчас в HDRP-ассете «{1}». " +
                "Режим не включается автоматически (пересборка шейдерных вариантов) — выберите " +
                "его на вкладке «Графика» и подтвердите.",
                KvGraphicsModel.LitShaderModeLabel(saved),
                KvGraphicsModel.LitShaderModeLabel((int)assetMode));
            Log(litShaderReport);
        }

        /// <summary>
        /// ФИКС 1. Запрос смены режима отрисовки. Смена режима — ТЯЖЁЛАЯ операция: HDRP
        /// пересобирает набор шейдерных вариантов, поэтому она НИКОГДА не выполняется
        /// автоматически и требует ЯВНОГО подтверждения оператора:
        ///   1) `RequestLitShaderMode(mode)` — открывает окно подтверждения на
        ///      <see cref="ConfirmSeconds"/> секунд и НИЧЕГО не меняет;
        ///   2) `ConfirmLitShaderMode()` — применяет режим, затем ПЕРЕПРИМЕНЯЕТ профиль,
        ///      чтобы выбранный в пресете MSAA заработал (или честно откатился на TAA);
        ///   3) `CancelLitShaderMode()` — отказ, ничего не меняется.
        /// Возвращает false, если запрос отклонён сразу (нет графики или режим уже такой).
        /// </summary>
        public bool RequestLitShaderMode(KvLitShaderMode mode)
        {
            if (!Available)
            {
                litShaderReport = KvLocExtra4.T("graphics.raster.nogfx",
                    "Смена режима отрисовки недоступна: графики нет (пакетный режим) — " +
                    "настройки только считаются.");
                Log(litShaderReport);
                KvGraphicsUi.ShowToast(litShaderReport);
                return false;
            }

            if (AssetLitShaderMode() == mode && LitShaderModePreference == (int)mode)
            {
                litShaderReport = KvLocExtra4.T("graphics.raster.same",
                    "Режим отрисовки уже такой — перекомпиляция не нужна.");
                Log(litShaderReport);
                return false;
            }

            pendingLitShaderMode = (int)mode;
            pendingLitShaderTimer = ConfirmSeconds;
            litShaderReport = KvLocExtra4.F("graphics.raster.warn",
                "Смена режима отрисовки на «{0}»: HDRP пересоберёт набор шейдерных вариантов — " +
                "это займёт несколько секунд, кадр может замереть. Подтвердите действие.",
                KvGraphicsModel.LitShaderModeLabel((int)mode));
            Log(litShaderReport);
            KvGraphicsUi.ShowToast(litShaderReport);
            return true;
        }

        /// <summary>Отменить запрос смены режима отрисовки (ничего не меняется).</summary>
        public void CancelLitShaderMode()
        {
            if (pendingLitShaderMode < 0) return;
            litShaderReport = KvLocExtra4.T("graphics.raster.cancel",
                "Смена режима отрисовки отменена оператором — HDRP-ассет не изменялся.");
            pendingLitShaderMode = -1;
            pendingLitShaderTimer = 0f;
            Log(litShaderReport);
        }

        /// <summary>
        /// ФИКС 1. Подтверждённое применение режима отрисовки: правит `supportedLitShaderMode`
        /// HDRP-ассета ОДНИМ присвоением (одна пересборка конвейера), запоминает выбор
        /// в PlayerPrefs, затем ПЕРЕПРИМЕНЯЕТ профиль — чтобы MSAA из пресета заработал,
        /// а при отложенном режиме честно откатился на TAA.
        /// </summary>
        public bool ConfirmLitShaderMode()
        {
            if (pendingLitShaderMode < 0) return false;
            KvLitShaderMode mode = PendingLitShaderMode;
            pendingLitShaderMode = -1;
            pendingLitShaderTimer = 0f;
            return ApplyLitShaderMode(mode);
        }

        /// <summary>Применить режим отрисовки (без окна подтверждения — для внутренних нужд).</summary>
        private bool ApplyLitShaderMode(KvLitShaderMode mode)
        {
            HDRenderPipelineAsset asset = PipelineAsset();
            if (asset == null)
            {
                litShaderReport = KvLocExtra4.T("graphics.raster.noasset",
                    "HDRP-ассет не найден: режим отрисовки не изменялся.");
                Log(litShaderReport);
                return false;
            }

            StringBuilder sb = new StringBuilder();
            RenderPipelineSettings s = asset.currentPlatformRenderPipelineSettings;
            RenderPipelineSettings.SupportedLitShaderMode wanted =
                (RenderPipelineSettings.SupportedLitShaderMode)(int)mode;
            bool changed = s.supportedLitShaderMode != wanted;

            // ФИКС 8. ФОРМАТ СТЕРЕО ДО СМЕНЫ РЕЖИМА — снимается ДО правки ассета, чтобы строка
            // «Deferred → Forward → Deferred» печатала РЕАЛЬНОЕ состояние до и после, а служба
            // при этом по-прежнему НИЧЕГО в XR не пишет (только читает).
            string stereoBefore = ReadStereo();

            if (changed)
            {
                s.supportedLitShaderMode = wanted;
                asset.currentPlatformRenderPipelineSettings = s;   // ОДНА пересборка конвейера
                assetTouched = true;
                sb.AppendLine("HDRP.supportedLitShaderMode = " + wanted +
                              " (режим отрисовки: " + KvGraphicsModel.LitShaderModeLabel((int)mode) + ")");
            }

            litShaderMode = (int)mode;
            PlayerPrefs.SetInt(KvGraphicsPrefs.KeyLitShaderMode, litShaderMode);
            PlayerPrefs.Save();

            // MSAA из пресета применяется СРАЗУ после смены режима (ФИКС 1, п. 3 ТЗ):
            // если оператор выбирал MSAA при отложенном режиме, служба честно подменяла его
            // на TAA — теперь прямая отрисовка включена, и запрошенный MSAA возвращается.
            if (msaaSubstitutedFrom > 0 && ForwardEnabled)
            {
                active.antiAliasing = msaaSubstitutedFrom;
                msaaSubstitutedFrom = -1;
            }
            applyReport = "";       // применение профиля пишет отчёт заново
            Apply(false);

            // XR: формат стерео служба НЕ трогает — только проверяет, что смена режима его не сбила.
            string xr = XrStereoReport();
            string stereoAfter = ReadStereo();
            sb.AppendLine("XR до смены режима: " + stereoBefore);
            sb.AppendLine("XR после смены режима: " + xr);
            // ФИКС 8: явная строка «до → после» — по ней видно, что стереоформат не сбился,
            // даже когда шлем не активен и XRSettings отдаёт пустое значение.
            sb.AppendLine("XR: стерео-формат " + StereoTransitionText(stereoBefore, stereoAfter) +
                          " · fovZoomFactor не трогается · eyeTextureResolutionScale — только в VR-Ready");
            sb.AppendLine("MSAA: " + (ForwardEnabled
                ? "доступен (прямая отрисовка включена) — сглаживание камеры отключено, как требует HDRP"
                : "недоступен (отложенная отрисовка) — применён TAA"));
            if (changed)
                sb.AppendLine("Шейдерные варианты HDRP пересобираются: несколько секунд возможны подвисания кадра.");

            litShaderReport = sb.ToString().TrimEnd();
            Log(KvLocExtra4.T("graphics.raster.done", "режим отрисовки применён: ") +
                KvGraphicsModel.LitShaderModeLabel((int)mode) + " · " + xr);
            foreach (string line in litShaderReport.Split('\n'))
                if (!string.IsNullOrEmpty(line)) Debug.Log("[Graphics] " + line);
            if (KvActionLog.Instance != null)
                KvActionLog.Instance.Info("графика: режим отрисовки → " +
                                          KvGraphicsModel.LitShaderModeLabel((int)mode));
            return true;
        }

        /// <summary>Строка о формате стерео XR (служба его НЕ меняет, только сообщает факт).</summary>
        private static string XrStereoReport()
        {
            try
            {
                if (!XRSettings.isDeviceActive)
                    return KvLocExtra4.T("graphics.xr.off", "шлем не активен, стерео-формат не задействован");
                return "стерео-формат " + XRSettings.stereoRenderingMode +
                       " (" + XRSettings.loadedDeviceName + ") — не изменялся";
            }
            catch (Exception e)
            {
                return "XR недоступен: " + e.Message;
            }
        }

        /// <summary>
        /// ФИКС 8. Текущий формат стерео одной строкой (ТОЛЬКО ЧТЕНИЕ). Пустая строка — XR
        /// недоступен (пакетный режим без шлема): тогда смена режима отрисовки физически
        /// не может ничего сбить, и это так и пишется в отчёте.
        /// </summary>
        private static string ReadStereo()
        {
            try
            {
                if (!XRSettings.isDeviceActive && string.IsNullOrEmpty(XRSettings.loadedDeviceName))
                    return "";
                return XRSettings.stereoRenderingMode.ToString();
            }
            catch (Exception)
            {
                return "";
            }
        }

        /// <summary>
        /// ФИКС 8. Строка «до → после» о стереоформате. Раньше печаталось только «после» —
        /// по такому отчёту нельзя было убедиться, что Deferred → Forward → Deferred
        /// НЕ сбивает стерео. Здесь три честных случая: XR не задействован, формат не изменился,
        /// формат изменился (тогда — предупреждение в консоль: это НЕ служба графики).
        /// </summary>
        private static string StereoTransitionText(string before, string after)
        {
            if (string.IsNullOrEmpty(before) && string.IsNullOrEmpty(after))
                return "не задействован (шлем не активен) — смена режима отрисовки его не затрагивает";
            if (before == after)
                return before + " — не изменялся (было " + before + ", стало " + after + ")";
            return before + " → " + after + " — ИЗМЕНИЛСЯ НЕ службой графики, проверьте настройки XR";
        }

        // ================================================================== запуск

        private void Start()
        {
            if (started) return;
            started = true;

            KvLocExtra4.Install();
            LoadPresets();
            LoadHardwareCache();

            int savedPreset = PlayerPrefs.GetInt(KvGraphicsPrefs.KeyPreset, (int)KvGraphicsPreset.Auto);
            preset = (KvGraphicsPreset)Mathf.Clamp(savedPreset, 0, 6);

            bool firstRun = PlayerPrefs.GetInt(KvGraphicsPrefs.KeyAutoDetected, 0) == 0;
            if (firstRun)
            {
                // ЭТАП 5: первый запуск — определить железо, выбрать пресет, применить, сохранить.
                FirstRunSetup();
            }
            else
            {
                Redetect(false);
                active = Resolve(silent: true);
                Apply(false);
                Log("графика: восстановлены настройки («" + KvGraphicsModel.Label(preset) +
                    "» → " + KvGraphicsModel.Label(EffectivePreset()) + ")");
                // Режим «Авто» следит за железом: если хеш изменился, пресет пересчитывается.
                if (AutoFollowHardware())
                {
                    active = ProfileOf(autoPreset);
                    Apply(true);
                }
            }

            // ФИКС 1: напоминаем про прошлый выбор режима отрисовки (сам он не включается).
            NoteSavedLitShaderMode();

            if (Application.isPlaying) EnsureHost();
            PushStatus();
        }

        private void Stop()
        {
            if (volumeProfile != null) UnityEngine.Object.Destroy(volumeProfile);
            if (volumeGo != null) UnityEngine.Object.Destroy(volumeGo);
            volumeGo = null;
            volumeProfile = null;
            volume = null;
            ReleaseRecorders();
            RestoreAsset();
        }

        private void EnsureHost()
        {
            if (host != null) return;
            GameObject go = new GameObject("KvGraphicsRuntime");
            go.hideFlags = HideFlags.HideAndDontSave;
            host = go.AddComponent<KvGraphicsRuntime>();
            host.Service = this;
        }

        /// <summary>
        /// ЭТАП 5: первое включение. Определяем железо, выбираем пресет, применяем,
        /// сохраняем флаг и показываем оператору, что именно настроено.
        /// </summary>
        private void FirstRunSetup()
        {
            KvGraphicsHardware hw = KvGraphicsHardwareProbe.Probe();
            Hardware = hw;

            string reason;
            KvHardwareClass cls = KvGraphicsHardwareProbe.Classify(hw, out reason);
            autoPreset = KvGraphicsHardwareProbe.PresetOf(cls);
            autoReason = reason;
            detected = true;

            preset = KvGraphicsPreset.Auto;
            active = ProfileOf(autoPreset);
            Apply(false);

            PlayerPrefs.SetInt(KvGraphicsPrefs.KeyPreset, (int)KvGraphicsPreset.Auto);
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyAutoPreset, KvGraphicsModel.KeyOf(autoPreset));
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyAutoReason, autoReason ?? "");
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyHardwareHash, hw.hash ?? "");
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyHardware, JsonUtility.ToJson(hw));
            PlayerPrefs.SetInt(KvGraphicsPrefs.KeyAutoDetected, 1);
            PlayerPrefs.Save();

            // ТЗ, этап 5: оператору показывается ОДНО короткое сообщение — что настроено
            // и где это изменить. В журнал идёт то же сообщение плюс строка применения.
            string message = KvLocExtra4.F("graphics.firstrun",
                "Графика настроена автоматически: {0} ({1}). Изменить: Настройки → Графика.",
                KvGraphicsModel.Label(autoPreset), autoReason);
            Log(message);
            if (KvActionLog.Instance != null) KvActionLog.Instance.Info(message);
            KvGraphicsUi.ShowToast(message);
        }

        /// <summary>Чтение сохранённых сведений о железе (чтобы показать обоснование без опроса).</summary>
        private void LoadHardwareCache()
        {
            string json = PlayerPrefs.GetString(KvGraphicsPrefs.KeyHardware, "");
            if (string.IsNullOrEmpty(json)) return;
            try
            {
                KvGraphicsHardware hw = JsonUtility.FromJson<KvGraphicsHardware>(json);
                if (hw != null) Hardware = hw;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] сведения о железе не прочитаны: " + e.Message);
            }
            if (string.IsNullOrEmpty(autoReason))
                autoReason = PlayerPrefs.GetString(KvGraphicsPrefs.KeyAutoReason, "");
            autoPreset = KvGraphicsModel.PresetOf(
                PlayerPrefs.GetString(KvGraphicsPrefs.KeyAutoPreset, KvGraphicsModel.KeyOf(KvGraphicsPreset.High)));
        }

        // ================================================================== пресеты из JSON

        private void LoadPresets()
        {
            presets.Clear();
            presetTitles.Clear();

            foreach (KvGraphicsProfile p in KvGraphicsModel.BuiltinAll())
            {
                KvGraphicsPreset key = KvGraphicsModel.PresetOf(p.id);
                presets[key] = p;
                presetTitles[key] = p.title;
            }

            string json = null;
            try
            {
                string path = PresetFilePath;
                if (File.Exists(path)) json = File.ReadAllText(path);
                loadedFile = path;
            }
            catch (Exception e)
            {
                loadedStatus = "файл пресетов не прочитан: " + e.Message;
                Debug.LogWarning("[Graphics] " + loadedStatus + " — используются встроенные пресеты");
                return;
            }

            if (string.IsNullOrEmpty(json))
            {
                loadedStatus = "файл пресетов не найден — используются встроенные пресеты";
                Debug.Log("[Graphics] " + loadedStatus);
                return;
            }

            KvGraphicsFile file = null;
            try { file = JsonUtility.FromJson<KvGraphicsFile>(json); }
            catch (Exception e)
            {
                loadedStatus = "файл пресетов испорчен (" + e.Message + ") — используются встроенные пресеты";
                Debug.LogWarning("[Graphics] " + loadedStatus);
                return;
            }

            if (file == null || file.presets == null || file.presets.Length == 0)
            {
                loadedStatus = "в файле пресетов нет ни одного пресета — используются встроенные";
                return;
            }

            int applied = 0;
            foreach (KvGraphicsPresetDto dto in file.presets)
            {
                if (dto == null) continue;
                KvGraphicsPreset key = KvGraphicsModel.PresetOf(dto.id);
                if (key == KvGraphicsPreset.Auto || key == KvGraphicsPreset.Custom) continue;
                KvGraphicsProfile basis;
                if (!presets.TryGetValue(key, out basis)) basis = KvGraphicsModel.Builtin(key);
                presets[key] = KvGraphicsModel.FromDto(dto, basis);
                presetTitles[key] = string.IsNullOrEmpty(dto.title) ? presets[key].title : dto.title;
                applied++;
            }
            loadedStatus = "файл пресетов: " + Path.GetFileName(loadedFile) + " · пресетов из файла: " + applied;
            Log("графика: " + loadedStatus);
        }

        /// <summary>Профиль пресета (из файла, иначе встроенный).</summary>
        public KvGraphicsProfile ProfileOf(KvGraphicsPreset p)
        {
            if (p == KvGraphicsPreset.Custom) return active != null ? active.Clone() : KvGraphicsModel.Builtin(KvGraphicsPreset.High);
            if (p == KvGraphicsPreset.Auto) p = autoPreset;
            KvGraphicsProfile found;
            if (presets.TryGetValue(p, out found)) return found.Clone();
            return KvGraphicsModel.Builtin(p);
        }

        /// <summary>Пресет, который реально применён («Авто» разворачивается в результат определения).</summary>
        public KvGraphicsPreset EffectivePreset()
        {
            return preset == KvGraphicsPreset.Auto ? autoPreset : preset;
        }

        // ================================================================== автоопределение

        /// <summary>Определить железо заново (кнопка «Определить по железу»).</summary>
        public string Redetect(bool announce)
        {
            KvGraphicsHardware hw = KvGraphicsHardwareProbe.Probe();
            Hardware = hw;
            detected = true;

            string reason;
            KvHardwareClass cls = KvGraphicsHardwareProbe.Classify(hw, out reason);
            KvGraphicsPreset found = KvGraphicsHardwareProbe.PresetOf(cls);
            string oldHash = PlayerPrefs.GetString(KvGraphicsPrefs.KeyHardwareHash, "");

            autoPreset = found;
            autoReason = reason;

            PlayerPrefs.SetString(KvGraphicsPrefs.KeyAutoPreset, KvGraphicsModel.KeyOf(autoPreset));
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyAutoReason, autoReason ?? "");
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyHardwareHash, hw.hash ?? "");
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyHardware, JsonUtility.ToJson(hw));

            bool changed = !string.IsNullOrEmpty(oldHash) && oldHash != hw.hash;
            if (announce)
            {
                string text = KvLocExtra4.F("graphics.detect.done",
                    "Определение по железу: {0} · автовыбор: {1}",
                    hw.Short, KvGraphicsModel.Label(autoPreset));
                Log(text);
                KvGraphicsUi.ShowToast(KvLocExtra4.F("graphics.toast.detect",
                    "Обнаружено: {0} · автовыбор: {1}", hw.Short, KvGraphicsModel.Label(autoPreset)));
            }
            if (changed)
                Log(KvLocExtra4.T("graphics.hw.changed",
                    "конфигурация железа изменилась — пресет автоопределения пересчитан"));

            PlayerPrefs.Save();
            return hw.Short;
        }

        /// <summary>
        /// ЭТАП 5: если выбран режим «Авто» и хеш железа изменился — пересчитать пресет
        /// (например, оператор сменил видеокарту или добавил память).
        /// </summary>
        private bool AutoFollowHardware()
        {
            if (preset != KvGraphicsPreset.Auto) return false;
            string saved = PlayerPrefs.GetString(KvGraphicsPrefs.KeyHardwareHash, "");
            KvGraphicsHardware hw = KvGraphicsHardwareProbe.Probe();
            Hardware = hw;
            if (string.IsNullOrEmpty(saved) || saved == hw.hash) return false;

            string reason;
            KvHardwareClass cls = KvGraphicsHardwareProbe.Classify(hw, out reason);
            KvGraphicsPreset found = KvGraphicsHardwareProbe.PresetOf(cls);
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyHardwareHash, hw.hash ?? "");
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyAutoReason, reason ?? "");
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyAutoPreset, KvGraphicsModel.KeyOf(found));
            PlayerPrefs.SetString(KvGraphicsPrefs.KeyHardware, JsonUtility.ToJson(hw));
            PlayerPrefs.Save();

            autoReason = reason;
            if (found == autoPreset) return false;
            autoPreset = found;
            Log(KvLocExtra4.F("graphics.hw.auto",
                "хеш железа изменился — пресет «Авто» пересчитан: {0} ({1})",
                KvGraphicsModel.Label(autoPreset), autoReason));
            KvGraphicsUi.ShowToast(KvLocExtra4.F("graphics.toast.hwchanged",
                "Железо изменилось · графика: {0}", KvGraphicsModel.Label(autoPreset)));
            return true;
        }

        // ================================================================== выбор пресета

        /// <summary>Сменить режим. «Авто» разворачивается в результат автоопределения.</summary>
        public void SetPreset(KvGraphicsPreset value, bool save = true)
        {
            preset = value;
            if (value == KvGraphicsPreset.Custom)
            {
                // «Свой» начинается с текущих значений — если профиль ещё не правился, берём пресет.
                if (active == null) active = ProfileOf(EffectivePreset());
            }
            else
            {
                active = ProfileOf(value);
            }
            if (save)
            {
                PlayerPrefs.SetInt(KvGraphicsPrefs.KeyPreset, (int)preset);
                SaveProfile();
            }
            Apply(true);
        }

        /// <summary>
        /// Правка одного параметра из интерфейса. Любая правка переводит режим в «Свой»
        /// (ТЗ, этап 3.1) — пресет при этом не портится, меняется только текущий профиль.
        /// </summary>
        public void Edit(Action<KvGraphicsProfile> change, bool apply = true)
        {
            if (change == null) return;
            if (active == null) active = ProfileOf(EffectivePreset());
            int aaBefore = active.antiAliasing;
            change(active);
            // ФИКС 1: оператор выбрал другое сглаживание сам — прежняя подмена на TAA
            // больше не «должна вернуться», память о ней сбрасывается.
            if (active.antiAliasing != aaBefore && active.antiAliasing != (int)KvAntiAliasing.TAA)
                msaaSubstitutedFrom = -1;

            // Любая правка переводит режим в «Свой» (ТЗ, этап 3.1): сам пресет не портится.
            active.id = KvGraphicsModel.KeyOf(KvGraphicsPreset.Custom);
            preset = KvGraphicsPreset.Custom;
            active.Normalize(KvGraphicsHardwareProbe.SupportsRayTracing());
            PlayerPrefs.SetInt(KvGraphicsPrefs.KeyPreset, (int)preset);
            SaveProfile();
            // ВАЖНО: применение — без немедленного сообщения. Пока оператор тянет ползунок,
            // настройки применяются мгновенно, а плашка и запись в журнал выдаются ОДИН раз
            // через секунду после последней правки (иначе был бы поток одинаковых плашек).
            if (apply) Apply(false);
            announceDelay = 1f;
        }

        /// <summary>Сброс графики по умолчанию: вернуть режим «Авто» (ТЗ, этап 3.4).</summary>
        public void ResetToAuto()
        {
            preset = KvGraphicsPreset.Auto;
            active = ProfileOf(autoPreset);
            PlayerPrefs.SetInt(KvGraphicsPrefs.KeyPreset, (int)KvGraphicsPreset.Auto);
            SaveProfile();
            Apply(true);
            Log(KvLocExtra4.T("graphics.reset.done",
                "графика сброшена к значениям по умолчанию: режим «Авто», пресет " +
                KvGraphicsModel.Label(autoPreset)));
            KvGraphicsUi.ShowToast(KvLocExtra4.F("graphics.toast.reset",
                "Графика сброшена · режим «Авто» · {0}", KvGraphicsModel.Label(autoPreset)));
        }

        private void SaveProfile()
        {
            try { PlayerPrefs.SetString(KvGraphicsPrefs.KeyProfile, JsonUtility.ToJson(active)); }
            catch (Exception e) { Debug.LogWarning("[Graphics] профиль не сохранён: " + e.Message); }
            PlayerPrefs.Save();
        }

        private KvGraphicsProfile Resolve(bool silent)
        {
            if (preset == KvGraphicsPreset.Custom)
            {
                string json = PlayerPrefs.GetString(KvGraphicsPrefs.KeyProfile, "");
                if (!string.IsNullOrEmpty(json))
                {
                    try
                    {
                        KvGraphicsProfile p = JsonUtility.FromJson<KvGraphicsProfile>(json);
                        if (p != null)
                        {
                            p.Normalize(KvGraphicsHardwareProbe.SupportsRayTracing());
                            return p;
                        }
                    }
                    catch (Exception e)
                    {
                        if (!silent) Debug.LogWarning("[Graphics] профиль «Свой» не прочитан: " + e.Message);
                    }
                }
                return ProfileOf(EffectivePreset());
            }
            return ProfileOf(preset);
        }

        // ================================================================== применение

        /// <summary>
        /// ПРОФИЛЬ, КОТОРЫЙ РЕАЛЬНО ПРИМЕНЯЕТСЯ: в MR поверх профиля оператора ложится
        /// MR-пресет (<see cref="KvMrPerformance"/>), в VR — оригинал.
        ///
        /// ПОЧЕМУ КОПИЯ, А НЕ ПЕРЕЗАПИСЬ. Пресет оператора живёт в PlayerPrefs и обязан
        /// пережить визит в MR: иначе после одного запуска в MR он обнаружил бы выключенные
        /// эффекты и пониженный масштаб как СВОИ настройки, а правка любой графической строки
        /// в MR сохраняла бы дешёвые значения в его профиль.
        /// </summary>
        public static KvGraphicsProfile EffectiveProfileFor(KvGraphicsProfile chosen)
        {
            if (chosen == null) return null;
            if (!KvMrPerformance.IsMrMode()) return chosen;
            KvGraphicsProfile cheap = chosen.Clone();
            KvMrPerformance.ApplyTo(cheap);
            return cheap;
        }

        /// <summary>Применить текущий профиль. Возвращает построчный отчёт.</summary>
        public string Apply(bool announce)
        {
            if (active == null) active = ProfileOf(EffectivePreset());
            active.Normalize(KvGraphicsHardwareProbe.SupportsRayTracing());

            // --- MR-ПРЕСЕТ (оптимизация MR, пункт плана 17.11.2026): подмена профиля только
            // НА ВРЕМЯ применения и с обязательным возвратом — см. `EffectiveProfileFor`.
            KvGraphicsProfile operatorProfile = active;
            bool mrPreset = KvMrPerformance.IsMrMode();
            active = EffectiveProfileFor(operatorProfile);

            string report = ApplyInner(announce);

            active = operatorProfile;
            if (mrPreset)
            {
                // Строка в ОТЧЁТЕ: без неё оператор не отличил бы «MR-пресет сработал» от
                // «графика не применялась» (в пакетном режиме служба честно пишет отказ).
                applyReport = applyReport + "\n" + KazistovVvUI.KvLoc.T("mr.preset.applied",
                    "MR-пресет применён: масштаб отрисовки ниже " +
                    KvMrPerformance.MaxRenderScale.ToString("0.0",
                        System.Globalization.CultureInfo.InvariantCulture) +
                    ", тени упрощены, дорогие эффекты выключены");
                report = applyReport;
            }
            return report;
        }

        /// <summary>Тело применения (без изменений) — см. <see cref="Apply"/>.</summary>
        private string ApplyInner(bool announce)
        {
            StringBuilder sb = new StringBuilder();
            if (!KvGraphics.Available)
            {
                applyReport = KvLocExtra4.T("graphics.skip.nographics",
                    "графика недоступна (пакетный режим без видеокарты) — настройки рассчитаны, " +
                    "но НЕ применены: " + KvGraphics.Reason);
                if (!warnedNoGraphics)
                {
                    warnedNoGraphics = true;
                    Log(applyReport);
                }
                PushStatus();
                return applyReport;
            }
            if (!Application.isPlaying)
            {
                applyReport = KvLocExtra4.T("graphics.skip.notplaying",
                    "настройки рассчитаны, но не применены: применение выполняется в режиме " +
                    "воспроизведения (PlayMode), чтобы не менять параметры проекта");
                PushStatus();
                return applyReport;
            }

            ApplyQualitySettings(sb);
            ApplyAsset(sb);
            ApplyCamera(sb);
            ApplyVolume(sb);
            ApplyTerrain(sb);
            ApplyXr(sb);

            adaptiveScale = Mathf.Clamp(active.renderScale, 0.5f, 1.5f);
            adaptiveBelowFor = 0f;
            adaptiveAboveFor = 0f;
            adaptiveNote = "";

            applyReport = sb.Length > 0 ? sb.ToString() : "изменений не потребовалось";

            if (announce) Announce();
            // Служебный режим (ТЗ, этап 3.5): полный список применённых параметров.
            if (DebugMode && !string.IsNullOrEmpty(applyReport))
            {
                string[] lines = applyReport.Split('\n');
                for (int i = 0; i < lines.Length; i++)
                {
                    if (string.IsNullOrEmpty(lines[i])) continue;
                    Debug.Log("[Graphics] " + lines[i]);
                    if (KvActionLog.Instance != null) KvActionLog.Instance.Info("графика: " + lines[i]);
                }
            }
            PushStatus();
            return applyReport;
        }

        /// <summary>
        /// Сообщение оператору о применённых настройках: строка в журнал, плашка в углу,
        /// запись в журнал действий (ТЗ, этап 7).
        /// </summary>
        private void Announce()
        {
            if (active == null) return;
            string summary = KvLocExtra4.F("graphics.applied",
                "Графика: {0} · масштаб {1} · тени {2} · AA {3} · VSync {4} · предел {5}",
                KvGraphicsModel.Label(EffectivePreset()),
                active.renderScale.ToString("0.00"),
                KvGraphicsModel.ShadowLabel(active.shadowQuality),
                KvGraphicsModel.AntiAliasingLabel(active.antiAliasing),
                KvGraphicsModel.VSyncLabel(active.vsync),
                KvGraphicsModel.FrameCapLabel(active.frameCap));
            Log(summary);
            KvGraphicsUi.ShowToast(summary);
            if (KvActionLog.Instance != null) KvActionLog.Instance.Info(summary);
        }

        // ------------------------------------------------------------------ QualitySettings

        private void ApplyQualitySettings(StringBuilder sb)
        {
            try
            {
                if (QualitySettings.vSyncCount != active.vsync)
                {
                    QualitySettings.vSyncCount = active.vsync;
                    sb.AppendLine("QualitySettings.vSyncCount = " + active.vsync);
                }
                int fps = KvGraphicsModel.FrameCapValue(active.frameCap);
                if (Application.targetFrameRate != fps)
                {
                    Application.targetFrameRate = fps;
                    sb.AppendLine("Application.targetFrameRate = " + fps);
                }
                if (!Mathf.Approximately(QualitySettings.lodBias, active.lodBias))
                {
                    QualitySettings.lodBias = active.lodBias;
                    sb.AppendLine("QualitySettings.lodBias = " + active.lodBias.ToString("0.00"));
                }
                if (QualitySettings.globalTextureMipmapLimit != active.mipmapLimit)
                {
                    QualitySettings.globalTextureMipmapLimit = active.mipmapLimit;
                    sb.AppendLine("QualitySettings.globalTextureMipmapLimit = " + active.mipmapLimit +
                                  " (" + KvGraphicsModel.TextureLabel(active.mipmapLimit) + ")");
                }
                AnisotropicFiltering af = (AnisotropicFiltering)Mathf.Clamp(active.anisotropic, 0, 2);
                if (QualitySettings.anisotropicFiltering != af)
                {
                    QualitySettings.anisotropicFiltering = af;
                    sb.AppendLine("QualitySettings.anisotropicFiltering = " + af);
                }
                bool realtimeProbes = active.reflectionMode == 1;
                if (QualitySettings.realtimeReflectionProbes != realtimeProbes)
                {
                    QualitySettings.realtimeReflectionProbes = realtimeProbes;
                    sb.AppendLine("QualitySettings.realtimeReflectionProbes = " + realtimeProbes +
                                  " (отражения: " + (realtimeProbes ? "запечённые + реального времени" : "только запечённые") + ")");
                }
            }
            catch (Exception e)
            {
                sb.AppendLine("QualitySettings: ошибка применения — " + e.Message);
                Debug.LogWarning("[Graphics] QualitySettings: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ HDRP-ассет

        /// <summary>Активный HDRP-ассет (текущий конвейер, иначе конвейер уровня качества).</summary>
        public static HDRenderPipelineAsset PipelineAsset()
        {
            try
            {
                HDRenderPipelineAsset asset = GraphicsSettings.currentRenderPipeline as HDRenderPipelineAsset;
                if (asset != null) return asset;
                return QualitySettings.renderPipeline as HDRenderPipelineAsset;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ApplyAsset(StringBuilder sb)
        {
            HDRenderPipelineAsset asset = PipelineAsset();
            if (asset == null)
            {
                sb.AppendLine("HDRP-ассет не найден: опорные возможности конвейера не изменялись");
                return;
            }

            assetName = asset.name;
            if (!assetSnapshotTaken)
            {
                assetSnapshot = asset.currentPlatformRenderPipelineSettings;
                assetSnapshotTaken = true;
            }

            RenderPipelineSettings s = asset.currentPlatformRenderPipelineSettings;
            int msaa = MsaaSamples(active.antiAliasing);
            bool msaaWanted = msaa > 0;
            // ВНИМАНИЕ: SupportedLitShaderMode — ВЛОЖЕННЫЙ тип RenderPipelineSettings.
            bool forward = (s.supportedLitShaderMode &
                RenderPipelineSettings.SupportedLitShaderMode.ForwardOnly) != 0;

            // MSAA в HDRP работает ТОЛЬКО при прямой отрисовке: если в ассете выбран
            // исключительно отложенный режим, честно сообщаем и откатываемся на TAA,
            // а не «делаем вид», что MSAA включён.
            if (msaaWanted && !forward)
            {
                sb.AppendLine("MSAA недоступен: в HDRP-ассете выбран только отложенный режим " +
                              "(DeferredOnly) — применено сглаживание TAA");
                Debug.LogWarning("[Graphics] MSAA в HDRP требует прямой отрисовки; в ассете «" +
                                 asset.name + "» включён только отложенный режим — применён TAA");
                // ФИКС 1: запоминаем, ЧТО просил оператор/пресет — после перехода на прямую
                // отрисовку (команда «Режим отрисовки → Прямая/Обе») MSAA вернётся сам.
                msaaSubstitutedFrom = active.antiAliasing;
                active.antiAliasing = (int)KvAntiAliasing.TAA;
                msaaWanted = false;
                msaa = 0;
            }
            else if (forward)
            {
                msaaSubstitutedFrom = -1;    // прямая отрисовка включена — подмены нет
            }

            MSAASamples msaaSetting = msaaWanted ? (MSAASamples)msaa : MSAASamples.None;
            if (s.msaaSampleCount != msaaSetting)
            {
                s.msaaSampleCount = msaaSetting;
                sb.AppendLine("HDRP.msaaSampleCount = " + msaaSetting);
            }

            bool ssaoWanted = active.ssao;
            bool ssrWanted = active.ssr && active.ssao;   // SSR без SSAO в HDRP не работает
            if (s.supportSSAO != ssaoWanted)
            {
                s.supportSSAO = ssaoWanted;
                sb.AppendLine("HDRP.supportSSAO = " + ssaoWanted);
            }
            if (s.supportSSR != ssrWanted)
            {
                s.supportSSR = ssrWanted;
                sb.AppendLine("HDRP.supportSSR = " + ssrWanted);
            }
            if (s.supportVolumetrics != active.volumetricFog)
            {
                s.supportVolumetrics = active.volumetricFog;
                sb.AppendLine("HDRP.supportVolumetrics = " + active.volumetricFog);
            }
            bool rtWanted = active.rayTracing && KvGraphicsHardwareProbe.SupportsRayTracing();
            if (s.supportRayTracing != rtWanted)
            {
                s.supportRayTracing = rtWanted;
                sb.AppendLine("HDRP.supportRayTracing = " + rtWanted);
            }

            int decalDistance = KvGraphicsModel.DecalDistanceOf(active.decalQuality);
            if (s.decalSettings.drawDistance != decalDistance)
            {
                s.decalSettings.drawDistance = decalDistance;
                sb.AppendLine("HDRP.decalSettings.drawDistance = " + decalDistance + " м");
            }

            // Разрешение карт теней: значение задаётся ДЛЯ ВСЕХ уровней качества, чтобы
            // результат не зависел от активного уровня (значения ScalableSetting доступны
            // только на чтение, поэтому лестница заменяется целиком).
            int shadowRes = KvGraphicsModel.ShadowResolutionOf(active.shadowQuality);
            int[] ladder = { shadowRes, shadowRes, shadowRes, shadowRes };
            if (s.hdShadowInitParams.shadowResolutionDirectional == null ||
                s.hdShadowInitParams.shadowResolutionDirectional[0] != shadowRes)
            {
                s.hdShadowInitParams.shadowResolutionDirectional =
                    new IntScalableSetting(ladder, ScalableSettingSchemaId.With4Levels);
                s.hdShadowInitParams.shadowResolutionPunctual =
                    new IntScalableSetting(ladder, ScalableSettingSchemaId.With4Levels);
                s.hdShadowInitParams.shadowResolutionArea =
                    new IntScalableSetting(ladder, ScalableSettingSchemaId.With4Levels);
                sb.AppendLine("HDRP.shadowResolution (направленные/точечные/площадные) = " + shadowRes);
            }
            HDShadowFilteringQuality filter = active.shadowQuality >= 2
                ? HDShadowFilteringQuality.High : (HDShadowFilteringQuality)Mathf.Max(0, active.shadowQuality);
            if (s.hdShadowInitParams.directionalShadowFilteringQuality != filter)
            {
                s.hdShadowInitParams.directionalShadowFilteringQuality = filter;
                s.hdShadowInitParams.punctualShadowFilteringQuality = filter;
                sb.AppendLine("HDRP.фильтрация теней = " + filter);
            }

            // Смещение LOD — тоже на всех уровнях качества.
            float[] lodLadder = { active.lodBias, active.lodBias, active.lodBias, active.lodBias };
            if (s.lodBias == null || !Mathf.Approximately(s.lodBias[0], active.lodBias))
            {
                s.lodBias = new FloatScalableSetting(lodLadder, ScalableSettingSchemaId.With4Levels);
                sb.AppendLine("HDRP.lodBias = " + active.lodBias.ToString("0.00"));
            }

            // Отражения: запечённые + реального времени требует большего кэша кубических карт.
            //
            // ФИКС 2 (§22). РАНЬШЕ ЗДЕСЬ БЫЛО ЖЁСТКО ЗАШИТО 512×512 (только запечённые) либо
            // 1024×1024 (запечённые + реального времени) — и это значение ПРИМЕНЯЛОСЬ К HDRP-АССЕТУ
            // НА КАЖДОМ СТАРТЕ. В самом ассете `HDRPHighQuality` атлас был выставлен в 8192×8192,
            // поэтому служба молча ужимала его в 8–16 раз, и HDRP при входе в PlayMode писал
            // `[Error] No more space in Reflection Probe Atlas` — сцена с двумя пробами
            // в ужатый атлас не влезала. Это и ломало прогон PlayMode-теста (§21.2.2).
            // Лечится не «увеличением ассета на шаг»: ассет уже почти на максимуме
            // (8192 из 16384), а размер атласа считается ПО СЦЕНЕ — как это и делает HDRP.
            ReflectionProbeTextureCacheResolution cache;
            int requiredPixels;
            int assetSide = (int)s.lightLoopSettings.reflectionProbeTexCacheSize;
            // Технологический отступ слота в атласе — ровно как в HDRP: (1 << lastValidCubeMip) · 2.
            int cubeMip = Mathf.Clamp((int)s.lightLoopSettings.reflectionProbeTexLastValidCubeMip, 0, 7);
            int cubePadding = 2 * (1 << cubeMip);
            cache = RequiredReflectionCacheSize(active.reflectionMode == 1, assetSide, cubePadding,
                out requiredPixels);
            if (s.lightLoopSettings.reflectionProbeTexCacheSize != cache)
            {
                ReflectionProbeTextureCacheResolution was = s.lightLoopSettings.reflectionProbeTexCacheSize;
                s.lightLoopSettings.reflectionProbeTexCacheSize = cache;
                sb.AppendLine("HDRP.reflectionProbeTexCacheSize = " + was + " → " + cache +
                              " (нужно сцене ≈ " + requiredPixels + " px², " +
                              AtlasMegabytes(cache) + " МБ вместо " + AtlasMegabytes(was) + " МБ)");
            }
            else
            {
                sb.AppendLine("HDRP.reflectionProbeTexCacheSize = " + cache +
                              " (уже достаточен: сцене нужно ≈ " + requiredPixels + " px²)");
            }

            GlobalDynamicResolutionSettings drs = s.dynamicResolutionSettings;
            bool scaleOne = Mathf.Abs(active.renderScale - 1f) < 0.001f;
            bool drEnabled = active.dynamicResolution || !scaleOne;
            drs.enabled = drEnabled;
            drs.forceResolution = !scaleOne;
            drs.forcedPercentage = Mathf.Clamp(active.renderScale, 0.5f, 1.5f) * 100f;
            drs.minPercentage = Mathf.Clamp(active.renderScale, 0.5f, 1f) * 100f;
            drs.maxPercentage = Mathf.Clamp(active.renderScale, 1f, 1.5f) * 100f;
            drs.useMipBias = !scaleOne;
            drs.lowResTransparencyMinimumThreshold = 0f;
            if (!Mathf.Approximately(s.dynamicResolutionSettings.forcedPercentage, drs.forcedPercentage) ||
                s.dynamicResolutionSettings.enabled != drs.enabled)
            {
                s.dynamicResolutionSettings = drs;
                sb.AppendLine("HDRP.динамическое разрешение: enabled=" + drs.enabled +
                              ", масштаб " + drs.forcedPercentage.ToString("0") + " %" +
                              (drs.forceResolution ? " (фиксированный)" : ""));
            }

            // ОДНО присвоение на всё: смена структуры пересобирает конвейер, поэтому
            // изменения собираются заранее (иначе пересборка шла бы на каждое поле).
            try
            {
                asset.currentPlatformRenderPipelineSettings = s;
                assetTouched = true;
            }
            catch (Exception e)
            {
                sb.AppendLine("HDRP-ассет: настройки не применены — " + e.Message);
                Debug.LogWarning("[Graphics] HDRP-ассет: " + e.Message);
            }
        }

        /// <summary>
        /// Размер атласа отражений HDRP, ДОСТАТОЧНЫЙ ДЛЯ ТЕКУЩЕЙ СЦЕНЫ (ФИКС 2, §22).
        ///
        /// Как считает сам HDRP (`Runtime/Lighting/Reflection/ReflectionProbeTextureCache.cs`):
        /// кубическая проба занимает в атласе КВАДРАТ со стороной `GetReflectionProbeSizeInAtlas(res)`,
        /// где res — разрешение пробы, а формула такая: не меньше 32; меньше 512 → ×4, иначе ×2
        /// (так компенсируется потеря площади при развёртке куба в октаэдр). Квадраты упаковываются
        /// без поворота, поэтому суммарная площадь проб — честная нижняя оценка.
        ///
        /// Константы формулы продублированы здесь ОСОЗНАННО: класс `ReflectionProbeTextureCache`
        /// в HDRP — internal, из сборки проекта его не вызвать.
        ///
        /// Границы: «пол» задаёт пресет (512 для запечённых, 1024 для запечённых + реального
        /// времени), «потолок» — 8192 и собственное значение ассета. 16384×16384 (следующая
        /// ступень HDRP) НЕ берём: это ≈1,4 ГБ видеопамяти, а на машине стоит Intel UHD
        /// с 2 ГБ общей памяти — такой атлас не выделится и «No more space» останется навсегда.
        /// </summary>
        private static ReflectionProbeTextureCacheResolution RequiredReflectionCacheSize(
            bool realtimeProbes, int assetCeilingSide, int cubeMipPadding, out int requiredPixels)
        {
            long area = 0;
            int probes = 0;
            int largestPadded = 0;
            try
            {
                // Пробы ищем ВКЛЮЧАЯ неактивные объекты: выключенная проба включается позже
                // и всё равно займёт место в атласе.
                ReflectionProbe[] found = UnityEngine.Object.FindObjectsByType<ReflectionProbe>(
                    FindObjectsInactive.Include);
                for (int i = 0; i < found.Length; i++)
                {
                    ReflectionProbe probe = found[i];
                    if (probe == null) continue;
                    int res = probe.resolution > 0 ? probe.resolution : 128;   // −1 = «по умолчанию» (128)
                    // HDRP кладёт пробу в атлас КВАДРАТОМ со стороной footprint И добавляет
                    // технологический отступ `(1 << lastValidCubeMip) · 2` px на слот.
                    int padded = ReflectionProbeSizeInAtlas(res) + Mathf.Max(0, cubeMipPadding);
                    area += (long)padded * padded;
                    if (padded > largestPadded) largestPadded = padded;
                    probes++;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] атлас отражений: пробы сцены не пересчитаны — " + e.Message);
            }

            // Запас 1,25 — выравнивание и «хвост» упаковки; режим реального времени
            // добавляет ещё 25 %: такие пробы перерисовываются и перекладываются в атласе.
            long need = (long)(area * 1.25 * (realtimeProbes ? 1.25 : 1.0));

            // ВАЖНО (иначе «одного шага» не хватает): атлас должен вмещать НЕ МЕНЕЕ ДВУХ
            // самых крупных слотов по каждой оси — иначе вторая проба физически не влезает,
            // даже когда суммарной площади формально достаточно. Именно это и происходило
            // при 512×512: два слота по 512 (+отступ) в такой атлас не помещаются вообще.
            int layoutSide = NextPowerOfTwo(largestPadded * (probes >= 2 ? 2 : 1));

            int floor = realtimeProbes ? 1024 : 512;   // «пол» пресета: ниже смысла нет
            int ceiling = 8192;                        // 16384 не берём (см. комментарий выше)
            if (assetCeilingSide >= floor && assetCeilingSide < ceiling) ceiling = assetCeilingSide;

            int side = floor;
            while (side < ceiling && ((long)side * side < need || side < layoutSide)) side *= 2;

            requiredPixels = (int)Mathf.Min(need, int.MaxValue);
            if (probes > 0)
                Debug.Log("[Graphics] атлас отражений: проб в сцене " + probes +
                          ", крупнейший слот " + largestPadded + " px → нужно ≈ " +
                          requiredPixels.ToString("N0") + " px², минимальная сторона по упаковке " +
                          layoutSide + " → выбран атлас " + side + "×" + side +
                          " (" + AtlasMegabytesOf(side) + " МБ)");
            return FromSide(side);
        }

        /// <summary>Ближайшая степень двойки не меньше значения (минимум 512).</summary>
        private static int NextPowerOfTwo(int value)
        {
            int side = 512;
            while (side < value && side < 16384) side *= 2;
            return side;
        }

        /// <summary>Сторона квадрата в атласе для разрешения пробы (формула HDRP, см. выше).</summary>
        private static int ReflectionProbeSizeInAtlas(int textureSize)
        {
            textureSize = Mathf.Max(textureSize, 32);
            return textureSize < 512 ? textureSize * 4 : textureSize * 2;
        }

        /// <summary>Перевести сторону квадрата в значение перечисления HDRP-ассета.</summary>
        private static ReflectionProbeTextureCacheResolution FromSide(int side)
        {
            switch (side)
            {
                case 512: return ReflectionProbeTextureCacheResolution.Resolution512x512;
                case 1024: return ReflectionProbeTextureCacheResolution.Resolution1024x1024;
                case 2048: return ReflectionProbeTextureCacheResolution.Resolution2048x2048;
                case 4096: return ReflectionProbeTextureCacheResolution.Resolution4096x4096;
                default: return ReflectionProbeTextureCacheResolution.Resolution8192x8192;
            }
        }

        /// <summary>Оценка памяти атласа (МБ): сторона² · 4 байта (R11G11B10) · 1,33 (мипы).</summary>
        private static string AtlasMegabytes(ReflectionProbeTextureCacheResolution resolution)
        {
            int side = (int)resolution;
            // Не-квадратные ступени (1024×512 и т. п.) кодируются парами байт — не оцениваем.
            if (side < 512 || side > 16384) return "?";
            return AtlasMegabytesOf(side);
        }

        private static string AtlasMegabytesOf(int side)
        {
            double mb = (double)side * side * 4.0 * 1.33 / (1024.0 * 1024.0);
            return mb.ToString("0.0");
        }

        /// <summary>Число выборок MSAA по режиму сглаживания (0 — MSAA не используется).</summary>
        private static int MsaaSamples(int antiAliasing)
        {
            switch (antiAliasing)
            {
                case (int)KvAntiAliasing.MSAA2x: return 2;
                case (int)KvAntiAliasing.MSAA4x: return 4;
                case (int)KvAntiAliasing.MSAA8x: return 8;
                default: return 0;
            }
        }

        // ------------------------------------------------------------------ камера

        /// <summary>Данные HDRP основной камеры (null — камеры HDRP нет).</summary>
        public static HDAdditionalCameraData CameraData()
        {
            try
            {
                Camera cam = Camera.main;
                if (cam == null) cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
                if (cam == null) return null;
                return cam.GetComponent<HDAdditionalCameraData>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private void ApplyCamera(StringBuilder sb)
        {
            HDAdditionalCameraData data = CameraData();
            if (data == null)
            {
                sb.AppendLine("HDRP-камера не найдена: сглаживание камеры не изменялось");
                return;
            }

            // ВНИМАНИЕ: AntialiasingMode / SMAAQualityLevel / TAAQualityLevel — вложенные
            // типы HDAdditionalCameraData, поэтому указываются полностью.
            HDAdditionalCameraData.AntialiasingMode wanted = HDAdditionalCameraData.AntialiasingMode.None;
            switch (active.antiAliasing)
            {
                case (int)KvAntiAliasing.FXAA:
                    wanted = HDAdditionalCameraData.AntialiasingMode.FastApproximateAntialiasing;
                    break;
                case (int)KvAntiAliasing.SMAA:
                    wanted = HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                    break;
                case (int)KvAntiAliasing.TAA:
                    wanted = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                    break;
                default:
                    wanted = HDAdditionalCameraData.AntialiasingMode.None;   // MSAA задаётся в ассете
                    break;
            }
            if (data.antialiasing != wanted)
            {
                data.antialiasing = wanted;
                sb.AppendLine("камера: сглаживание = " + wanted);
            }
            if (wanted == HDAdditionalCameraData.AntialiasingMode.SubpixelMorphologicalAntiAliasing &&
                data.SMAAQuality != HDAdditionalCameraData.SMAAQualityLevel.High)
            {
                data.SMAAQuality = HDAdditionalCameraData.SMAAQualityLevel.High;
                sb.AppendLine("камера: качество SMAA = High");
            }
            if (wanted == HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing &&
                data.TAAQuality != HDAdditionalCameraData.TAAQualityLevel.Medium)
            {
                data.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.Medium;
                sb.AppendLine("камера: качество TAA = Medium");
            }

            bool allowDr = active.dynamicResolution || Mathf.Abs(active.renderScale - 1f) > 0.001f;
            if (data.allowDynamicResolution != allowDr)
            {
                data.allowDynamicResolution = allowDr;
                sb.AppendLine("камера: динамическое разрешение = " + allowDr);
            }
        }

        // ------------------------------------------------------------------ том (свечение, туман, тени)

        /// <summary>
        /// СОБСТВЕННЫЙ ГЛОБАЛЬНЫЙ ТОМ. Профиль создаётся в рантайме
        /// (`HideAndDontSave`), поэтому файлы проекта не меняются; переопределяются
        /// только те параметры, которыми управляет пресет, — остальное берётся из
        /// профилей сцены (Sky and Fog Volume, Post Process Volume) как раньше.
        /// </summary>
        private void EnsureVolume()
        {
            if (volume != null) return;

            volumeGo = new GameObject("KvGraphicsVolume");
            volumeGo.hideFlags = HideFlags.HideAndDontSave;
            Transform parent = null;
            KazistovVvUIManager ui = KazistovVvUIManager.Instance;
            if (ui != null) parent = ui.transform;
            if (parent != null) volumeGo.transform.SetParent(parent, false);

            volume = volumeGo.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;      // выше профилей сцены (у них приоритет 0)
            volume.weight = 1f;

            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volumeProfile.hideFlags = HideFlags.HideAndDontSave;
            volumeProfile.name = "KvGraphicsProfile";
            volume.profile = volumeProfile;

            // overrides: false — «переопределять всё» было бы ошибкой: тот же туман
            // потерял бы цвет и плотность из профиля сцены. Управляем только своим.
            volumeBloom = volumeProfile.Add<Bloom>(false);
            volumeMotionBlur = volumeProfile.Add<MotionBlur>(false);
            volumeDof = volumeProfile.Add<DepthOfField>(false);
            volumeSsao = volumeProfile.Add<ScreenSpaceAmbientOcclusion>(false);
            volumeSsr = volumeProfile.Add<ScreenSpaceReflection>(false);
            volumeFog = volumeProfile.Add<Fog>(false);
            volumeShadows = volumeProfile.Add<HDShadowSettings>(false);
        }

        private void ApplyVolume(StringBuilder sb)
        {
            try
            {
                EnsureVolume();
            }
            catch (Exception e)
            {
                sb.AppendLine("том графики не создан: " + e.Message);
                Debug.LogWarning("[Graphics] том графики не создан: " + e.Message);
                return;
            }

            // --- затенение в экранном пространстве (SSAO)
            if (volumeSsao != null)
            {
                float intensity = active.ssao ? Mathf.Clamp(active.ssaoIntensity, 0.05f, 4f) : 0f;
                Set(volumeSsao.intensity, intensity);
                Set(volumeSsao.rayTracing, false);
                sb.AppendLine("SSAO: сила " + intensity.ToString("0.00"));
            }

            // --- отражения в экранном пространстве (SSR)
            if (volumeSsr != null)
            {
                Set(volumeSsr.enabled, active.ssr && active.ssao);
                sb.AppendLine("SSR: " + (active.ssr && active.ssao ? "включены" : "выключены"));
            }

            // --- свечение
            if (volumeBloom != null)
            {
                float intensity = active.bloom ? Mathf.Clamp(active.bloomIntensity, 0.01f, 1f) : 0f;
                Set(volumeBloom.intensity, intensity);
                sb.AppendLine("Свечение (Bloom): сила " + intensity.ToString("0.00"));
            }

            // --- размытие в движении
            if (volumeMotionBlur != null)
            {
                Set(volumeMotionBlur.intensity, active.motionBlur ? 0.5f : 0f);
                sb.AppendLine("Размытие в движении: " + (active.motionBlur ? "включено" : "выключено"));
            }

            // --- глубина резкости (фокусировка — по физической камере, как принято в HDRP)
            if (volumeDof != null)
            {
                Set(volumeDof.focusMode, active.depthOfField ? DepthOfFieldMode.UsePhysicalCamera : DepthOfFieldMode.Off);
                sb.AppendLine("Глубина резкости: " + (active.depthOfField ? "включена" : "выключена"));
            }

            // --- объёмный туман (слои и разрешение зависят от качества)
            if (volumeFog != null)
            {
                Set(volumeFog.enableVolumetricFog, active.volumetricFog);
                if (active.volumetricFog)
                {
                    float resolution;
                    int slices;
                    switch (Mathf.Clamp(active.volumetricFogQuality, 0, 2))
                    {
                        case 0: resolution = 12.5f; slices = 32; break;
                        case 1: resolution = 25f; slices = 64; break;
                        default: resolution = 50f; slices = 128; break;
                    }
                    Set(volumeFog.screenResolutionPercentage, resolution);
                    Set(volumeFog.volumeSliceCount, slices);
                    Set(volumeFog.multipleScatteringIntensity, active.volumetricFogQuality >= 1 ? 1f : 0f);
                    sb.AppendLine("Объёмный туман: качество " + KvGraphicsModel.QualityLabel(active.volumetricFogQuality) +
                                  " · разрешение " + resolution.ToString("0.#") + " % · слоёв " + slices);
                }
                else
                {
                    sb.AppendLine("Объёмный туман: выключен");
                }
            }

            // --- дистанция и каскады теней
            if (volumeShadows != null)
            {
                Set(volumeShadows.maxShadowDistance, active.shadowDistance);
                Set(volumeShadows.cascadeShadowSplitCount, active.shadowCascades);
                sb.AppendLine("Тени: дистанция " + active.shadowDistance.ToString("0") + " м · каскадов " +
                              active.shadowCascades);
            }
        }

        /// <summary>Записать значение в параметр тома, включив переопределение.</summary>
        private static void Set<T>(VolumeParameter<T> parameter, T value)
        {
            if (parameter == null) return;
            parameter.overrideState = true;
            parameter.value = value;
        }

        // ------------------------------------------------------------------ рельеф

        private void ApplyTerrain(StringBuilder sb)
        {
            try
            {
                Terrain[] terrains = Terrain.activeTerrains;
                if (terrains == null || terrains.Length == 0) return;

                float pixelError, detail, treeDistance, billboard, basemap;
                switch (Mathf.Clamp(active.terrainQuality, 0, 2))
                {
                    case 0: pixelError = 20f; detail = 0.25f; treeDistance = 100f; billboard = 50f; basemap = 200f; break;
                    case 1: pixelError = 10f; detail = 0.5f; treeDistance = 250f; billboard = 100f; basemap = 500f; break;
                    default: pixelError = 5f; detail = 1f; treeDistance = 500f; billboard = 200f; basemap = 1000f; break;
                }

                int changed = 0;
                for (int i = 0; i < terrains.Length; i++)
                {
                    Terrain t = terrains[i];
                    if (t == null) continue;
                    if (!Mathf.Approximately(t.heightmapPixelError, pixelError)) { t.heightmapPixelError = pixelError; changed++; }
                    if (!Mathf.Approximately(t.detailObjectDensity, detail)) { t.detailObjectDensity = detail; changed++; }
                    if (!Mathf.Approximately(t.treeDistance, treeDistance)) { t.treeDistance = treeDistance; changed++; }
                    if (!Mathf.Approximately(t.treeBillboardDistance, billboard)) { t.treeBillboardDistance = billboard; changed++; }
                    if (!Mathf.Approximately(t.basemapDistance, basemap)) { t.basemapDistance = basemap; changed++; }
                }
                if (changed > 0)
                    sb.AppendLine("Рельеф: качество " + KvGraphicsModel.QualityLabel(active.terrainQuality) +
                                  " (" + terrains.Length + " участков, правок " + changed + ")");
            }
            catch (Exception e)
            {
                sb.AppendLine("Рельеф: настройки не применены — " + e.Message);
            }
        }

        // ------------------------------------------------------------------ XR

        private void ApplyXr(StringBuilder sb)
        {
            bool xr = false;
            string device = "";
            string stereo = "";
            try
            {
                xr = XRSettings.isDeviceActive;
                device = XRSettings.loadedDeviceName;
                stereo = XRSettings.stereoRenderingMode.ToString();
            }
            catch (Exception)
            {
                return;
            }

            if (!string.IsNullOrEmpty(stereo))
                sb.AppendLine("XR: стерео-формат " + stereo + " (не изменяется службой графики)");

            if (!xr) return;

            // ТЗ: в VR предел кадров обязателен. Второй по частоте шлем — Quest 2 (72 Гц).
            int xrFps = 90;
            if (!string.IsNullOrEmpty(device) &&
                (device.IndexOf("Quest 2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 device.IndexOf("Quest2", StringComparison.OrdinalIgnoreCase) >= 0))
                xrFps = 72;

            if (Application.targetFrameRate != xrFps)
            {
                Application.targetFrameRate = xrFps;
                sb.AppendLine("XR: предел кадров = " + xrFps + " (шлем " + device + ")");
            }
            sb.AppendLine("XR: частоту кадров задаёт среда XR; " +
                          "значение Application.targetFrameRate=" + xrFps + " выставлено как ориентир");

            // Масштаб отрисовки в шлеме задаётся средой XR. Меняем только в пресете
            // VR-Ready и только коэффициент разрешения глаза — формат стерео не трогаем.
            if (EffectivePreset() == KvGraphicsPreset.VRReady)
            {
                try
                {
                    float wanted = Mathf.Clamp(active.renderScale, 0.5f, 1.5f);
                    if (!Mathf.Approximately(XRSettings.eyeTextureResolutionScale, wanted))
                    {
                        XRSettings.eyeTextureResolutionScale = wanted;
                        sb.AppendLine("XR: масштаб разрешения глаза = " + wanted.ToString("0.00"));
                    }
                    string after = XRSettings.stereoRenderingMode.ToString();
                    if (after != stereo)
                        Debug.LogWarning("[Graphics] стерео-формат изменился сам (" + stereo + " → " +
                                         after + ") — служба графики его не меняла, проверьте настройки XR");
                }
                catch (Exception e)
                {
                    sb.AppendLine("XR: масштаб разрешения не изменён — " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------ возврат настроек ассета

        /// <summary>
        /// Вернуть HDRP-ассету исходные настройки, снятые перед первым применением.
        /// Нужно для честности: правки ассета в редакторе не должны «залипать».
        /// </summary>
        public bool RestoreAsset()
        {
            if (!assetSnapshotTaken || !assetTouched) return false;
            HDRenderPipelineAsset asset = PipelineAsset();
            if (asset == null) return false;
            try
            {
                asset.currentPlatformRenderPipelineSettings = assetSnapshot;
                assetTouched = false;
                Log("графика: исходные настройки HDRP-ассета «" + assetName + "» восстановлены");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] вернуть настройки HDRP-ассета не удалось: " + e.Message);
                return false;
            }
        }

        // ================================================================== кадровое обслуживание

        /// <summary>Кадровое обслуживание: метрики, адаптивное качество, бенчмарк.</summary>
        public void Tick(float dt)
        {
            if (dt <= 0f) return;

            metricsFrameMs = dt * 1000f;
            float instant = 1f / dt;
            metricsFps = metricsFps <= 0f ? instant : Mathf.Lerp(metricsFps, instant, 0.1f);

            if (benchmarkRunning) TickBenchmark(dt);
            if (warnCooldown > 0f) warnCooldown -= dt;

            // ФИКС 1: окно подтверждения смены режима отрисовки. Ничего не меняется само —
            // по истечении времени запрос просто ОТКЛОНЯЕТСЯ (как окно подтверждения пуска).
            if (pendingLitShaderMode >= 0)
            {
                pendingLitShaderTimer -= dt;
                if (pendingLitShaderTimer <= 0f)
                {
                    pendingLitShaderTimer = 0f;
                    litShaderReport = KvLocExtra4.T("graphics.raster.timeout",
                        "Смена режима отрисовки не подтверждена за 20 с — отменена, " +
                        "HDRP-ассет не изменялся.");
                    pendingLitShaderMode = -1;
                    Log(litShaderReport);
                    KvGraphicsUi.ShowToast(litShaderReport);
                }
            }
            if (adaptiveTimer > 0f) adaptiveTimer -= dt;
            else
            {
                adaptiveTimer = 3f;         // редкая проверка: раз в 3 секунды, без влияния на кадр
                TickAdaptive();
            }
            if (KvGraphicsUi.MetricsVisible) KvGraphicsUi.RefreshMetrics(MetricsText());

            // Отложенное сообщение о правке настроек (см. Edit): одно на серию правок.
            if (announceDelay > 0f)
            {
                announceDelay -= dt;
                if (announceDelay <= 0f)
                {
                    announceDelay = 0f;
                    Announce();
                }
            }

            // Индикатор в статус-баре обновляется редко: панель пересобирается при смене
            // темы, языка и раскладки, поэтому подпись восстанавливается сама.
            statusTimer -= dt;
            if (statusTimer <= 0f)
            {
                statusTimer = 2f;
                PushStatus();
            }

            // Режим «Авто» следит за конфигурацией железа (ТЗ, этап 5): проверка редкая,
            // потому что опрос SystemInfo хоть и дешёвый, но не бесплатный.
            hardwareTimer -= dt;
            if (hardwareTimer <= 0f)
            {
                hardwareTimer = 60f;
                if (AutoFollowHardware())
                {
                    active = ProfileOf(autoPreset);
                    Apply(true);
                }
            }
        }

        // ------------------------------------------------------------------ адаптивное качество

        /// <summary>
        /// АДАПТИВНОЕ КАЧЕСТВО (ТЗ, этап 3.3 и 7). Кадровая частота сравнивается с целевой:
        /// при устойчивой просадке масштаб отрисовки снижается шагом 0,05 (не ниже 0,5),
        /// при устойчивом запасе — возвращается к значению пресета. По ТЗ масштаб меняется
        /// ТОЛЬКО в пресете VR-Ready; в остальных режимах служба ограничивается мягким
        /// предупреждением в журнал.
        /// </summary>
        private void TickAdaptive()
        {
            if (active == null || !active.adaptiveQuality) return;
            if (!KvGraphics.Available || !Application.isPlaying) return;

            int target = Mathf.Clamp(active.targetFps, 30, 144);
            float fps = metricsFps;
            if (fps <= 1f) return;

            bool vr = EffectivePreset() == KvGraphicsPreset.VRReady;
            float presetScale = Mathf.Clamp(active.renderScale, 0.5f, 1.5f);

            if (fps < target * 0.9f)
            {
                adaptiveBelowFor += 3f;
                adaptiveAboveFor = 0f;
                if (adaptiveBelowFor >= 6f)
                {
                    adaptiveBelowFor = 0f;
                    int rounded = Mathf.RoundToInt(fps);
                    string hint = active.shadowQuality >= 2
                        ? KvLocExtra4.T("graphics.adaptive.hint.shadows", "рекомендую снизить тени")
                        : KvLocExtra4.T("graphics.adaptive.hint.fog", "рекомендую снизить объёмный туман");
                    string warning = KvLocExtra4.F("graphics.adaptive.low",
                        "FPS {0} < целевого {1} · {2}", rounded, target, hint);
                    if (warnCooldown <= 0f)
                    {
                        warnCooldown = 20f;
                        if (KvActionLog.Instance != null) KvActionLog.Instance.Warning(warning);
                        Log(warning);
                    }

                    if (vr && adaptiveScale > 0.5f)
                    {
                        adaptiveScale = Mathf.Max(0.5f, adaptiveScale - 0.05f);
                        ApplyScaleOnly(adaptiveScale);
                        adaptiveNote = KvLocExtra4.F("graphics.adaptive.down",
                            "масштаб отрисовки снижен до {0}", adaptiveScale.ToString("0.00"));
                        Log(adaptiveNote);
                    }
                    else if (!vr)
                    {
                        adaptiveNote = KvLocExtra4.T("graphics.adaptive.vronly",
                            "адаптивное снижение масштаба по ТЗ работает только в пресете VR-Ready — " +
                            "выполнено только предупреждение в журнал");
                    }
                }
            }
            else if (fps > target * 1.15f)
            {
                adaptiveAboveFor += 3f;
                adaptiveBelowFor = 0f;
                if (adaptiveAboveFor >= 12f && adaptiveScale < presetScale - 0.001f)
                {
                    adaptiveAboveFor = 0f;
                    adaptiveScale = Mathf.Min(presetScale, adaptiveScale + 0.05f);
                    ApplyScaleOnly(adaptiveScale);
                    adaptiveNote = KvLocExtra4.F("graphics.adaptive.up",
                        "запас по кадрам — масштаб отрисовки возвращён к {0}",
                        adaptiveScale.ToString("0.00"));
                }
            }
            else
            {
                adaptiveBelowFor = 0f;
                adaptiveAboveFor = 0f;
            }
        }

        /// <summary>Применить только масштаб отрисовки (адаптивное снижение).</summary>
        private void ApplyScaleOnly(float scale)
        {
            HDRenderPipelineAsset asset = PipelineAsset();
            if (asset == null) return;
            try
            {
                RenderPipelineSettings s = asset.currentPlatformRenderPipelineSettings;
                GlobalDynamicResolutionSettings drs = s.dynamicResolutionSettings;
                drs.enabled = true;
                drs.forceResolution = true;
                drs.forcedPercentage = Mathf.Clamp(scale, 0.5f, 1.5f) * 100f;
                drs.minPercentage = 50f;
                drs.maxPercentage = Mathf.Clamp(scale, 1f, 1.5f) * 100f;
                drs.useMipBias = true;
                s.dynamicResolutionSettings = drs;
                asset.currentPlatformRenderPipelineSettings = s;

                HDAdditionalCameraData data = CameraData();
                if (data != null) data.allowDynamicResolution = true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Graphics] адаптивный масштаб не применён: " + e.Message);
            }
        }

        // ------------------------------------------------------------------ бенчмарк

        /// <summary>Запустить замер кадровой частоты (по умолчанию 5 секунд — ТЗ, этап 3.4).</summary>
        public void StartBenchmark(float seconds = 5f)
        {
            if (benchmarkRunning) return;
            benchmarkRunning = true;
            benchmarkTotal = Mathf.Clamp(seconds, 1f, 30f);
            benchmarkLeft = benchmarkTotal;
            benchmarkFrames.Clear();
            KvUiStates.Begin(KvLocExtra4.T("graphics.benchmark.running", "Замер графики"), 0f);
            Log(KvLocExtra4.F("graphics.benchmark.started", "бенчмарк графики: {0} с",
                benchmarkTotal.ToString("0")));
        }

        /// <summary>Остановить замер досрочно.</summary>
        public void StopBenchmark()
        {
            if (!benchmarkRunning) return;
            benchmarkRunning = false;
            KvUiStates.End();
            FinishBenchmark();
        }

        private void TickBenchmark(float dt)
        {
            benchmarkLeft -= dt;
            benchmarkFrames.Add(1f / Mathf.Max(0.0001f, dt));
            if (benchmarkFrames.Count > BenchmarkCapacity) benchmarkFrames.RemoveAt(0);

            float progress = 1f - Mathf.Clamp01(benchmarkLeft / benchmarkTotal);
            KvUiStates.SetProgress(progress, KvLocExtra4.T("graphics.benchmark.running", "Замер графики"));

            if (benchmarkLeft > 0f) return;
            benchmarkRunning = false;
            KvUiStates.End();
            FinishBenchmark();
        }

        private void FinishBenchmark()
        {
            if (benchmarkFrames.Count < 10)
            {
                benchmarkReport = KvLocExtra4.T("graphics.benchmark.tooshort",
                    "замер слишком короткий — повторите");
                Log(benchmarkReport);
                return;
            }

            List<float> sorted = new List<float>(benchmarkFrames);
            sorted.Sort();

            float sum = 0f;
            float min = float.MaxValue;
            float max = 0f;
            for (int i = 0; i < benchmarkFrames.Count; i++)
            {
                float v = benchmarkFrames[i];
                sum += v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            float avg = sum / benchmarkFrames.Count;
            int lowIndex = Mathf.Clamp(Mathf.FloorToInt(sorted.Count * 0.01f), 0, sorted.Count - 1);
            float low1 = sorted[lowIndex];

            float presetScale = Mathf.Clamp(active.renderScale, 0.5f, 1.5f);
            string verdict;
            if (avg >= active.targetFps)
                verdict = KvLocExtra4.T("graphics.benchmark.ok", "запас есть — пресет подходит");
            else if (avg >= active.targetFps * 0.8f)
                verdict = KvLocExtra4.T("graphics.benchmark.mid",
                    "почти хватает — можно снизить тени или объёмный туман");
            else
                verdict = KvLocExtra4.T("graphics.benchmark.bad",
                    "недостаточно — рекомендуются пресет ниже и масштаб 0,85");

            benchmarkReport =
                KvLocExtra4.F("graphics.benchmark.report",
                    "Замер {0} с · кадров {1}", benchmarkTotal.ToString("0"), benchmarkFrames.Count) + "\n" +
                KvLocExtra4.F("graphics.benchmark.avg", "Средний FPS: {0}", avg.ToString("0.0")) + "\n" +
                KvLocExtra4.F("graphics.benchmark.min", "Минимальный FPS: {0}", min.ToString("0.0")) + "\n" +
                KvLocExtra4.F("graphics.benchmark.max", "Максимальный FPS: {0}", max.ToString("0.0")) + "\n" +
                KvLocExtra4.F("graphics.benchmark.low1", "Просадки (1 %): {0}", low1.ToString("0.0")) + "\n" +
                KvLocExtra4.F("graphics.benchmark.target", "Цель: {0} FPS · масштаб {1}",
                    active.targetFps, presetScale.ToString("0.00")) + "\n" + verdict;

            Log(benchmarkReport.Replace("\n", " · "));
            if (KvActionLog.Instance != null)
                KvActionLog.Instance.Info("бенчмарк графики: средний " + avg.ToString("0.0") +
                                          " FPS, минимальный " + min.ToString("0.0") + ", просадки " +
                                          low1.ToString("0.0"));
            KvGraphicsUi.ShowToast(KvLocExtra4.F("graphics.toast.benchmark",
                "Замер: средний {0} FPS · минимум {1}", avg.ToString("0.0"), min.ToString("0.0")));
        }

        // ------------------------------------------------------------------ метрики

        private void EnsureRecorders()
        {
            if (recordersReady) return;
            recordersReady = true;
            drawCalls = StartRecorder(ProfilerCategory.Render, "Draw Calls Count", "Draw Calls");
            triangles = StartRecorder(ProfilerCategory.Render, "Triangles Count", "Triangles");
            setPassCalls = StartRecorder(ProfilerCategory.Render, "SetPass Calls Count", "SetPass Calls");
            textureMemory = StartRecorder(ProfilerCategory.Memory, "Texture Memory", "Texture Memory");
        }

        private static ProfilerRecorder StartRecorder(ProfilerCategory category, string name, string fallback)
        {
            try
            {
                ProfilerRecorder r = ProfilerRecorder.StartNew(category, name);
                if (r.Valid) return r;
                r.Dispose();
                r = ProfilerRecorder.StartNew(category, fallback);
                return r;
            }
            catch (Exception)
            {
                return default(ProfilerRecorder);
            }
        }

        private void ReleaseRecorders()
        {
            if (!recordersReady) return;
            recordersReady = false;
            try { if (drawCalls.Valid) drawCalls.Dispose(); } catch (Exception) { }
            try { if (triangles.Valid) triangles.Dispose(); } catch (Exception) { }
            try { if (setPassCalls.Valid) setPassCalls.Dispose(); } catch (Exception) { }
            try { if (textureMemory.Valid) textureMemory.Dispose(); } catch (Exception) { }
        }

        /// <summary>Строка метрик для оверлея (ТЗ, этап 3.4).</summary>
        public string MetricsText()
        {
            EnsureRecorders();
            StringBuilder sb = new StringBuilder();
            sb.Append("FPS: ").Append(metricsFps.ToString("0")).Append("   ");
            sb.Append(KvLocExtra4.T("graphics.metrics.frametime", "кадр")).Append(": ")
              .Append(metricsFrameMs.ToString("0.0")).Append(" ").Append("мс").Append('\n');
            sb.Append(KvLocExtra4.T("graphics.metrics.drawcalls", "вызовов отрисовки")).Append(": ")
              .Append(Counter(drawCalls)).Append("   ");
            sb.Append(KvLocExtra4.T("graphics.metrics.triangles", "треугольников")).Append(": ")
              .Append(Counter(triangles)).Append('\n');
            sb.Append(KvLocExtra4.T("graphics.metrics.setpass", "проходов материала")).Append(": ")
              .Append(Counter(setPassCalls)).Append('\n');
            sb.Append(KvLocExtra4.T("graphics.metrics.vram", "видеопамять (карта)")).Append(": ")
              .Append(Hardware != null ? Hardware.vramMb : 0).Append(" МБ");
            long tex = CounterValue(textureMemory);
            if (tex > 0)
                sb.Append("   · ").Append(KvLocExtra4.T("graphics.metrics.textures", "текстуры")).Append(": ")
                  .Append((tex / (1024f * 1024f)).ToString("0")).Append(" МБ");
            return sb.ToString();
        }

        private static string Counter(ProfilerRecorder recorder)
        {
            long value = CounterValue(recorder);
            return value <= 0 ? "n/a" : value.ToString("n0");
        }

        private static long CounterValue(ProfilerRecorder recorder)
        {
            try { return recorder.Valid ? recorder.LastValue : 0L; }
            catch (Exception) { return 0L; }
        }

        // ------------------------------------------------------------------ статус и журнал

        /// <summary>Короткая подпись текущего режима для статус-бара (ТЗ, этап 7).</summary>
        public string StatusShort()
        {
            KvGraphicsPreset p = EffectivePreset();
            string text = KvGraphicsModel.Label(p);
            if (preset == KvGraphicsPreset.Auto)
                text = KvLocExtra4.T("graphics.preset.auto", "Авто") + ": " + text;
            if (!Available)
                text += " · " + KvLocExtra4.T("graphics.badge.off", "без графики");
            return KvLocExtra4.T("graphics.status", "Графика") + ": " + text;
        }

        /// <summary>Подробная строка «Обнаружено: … · Автовыбор: …» (ТЗ, этап 3.1).</summary>
        public string DetectLine()
        {
            if (Hardware == null || !Hardware.detected) return "";
            return KvLocExtra4.F("graphics.detect.result",
                "Обнаружено: {0} · Автовыбор: {1}",
                Hardware.Short, KvGraphicsModel.Label(autoPreset)) +
                (string.IsNullOrEmpty(autoReason) ? "" : " — " + autoReason);
        }

        private void PushStatus()
        {
            KvGraphicsUi.SetStatus(StatusShort());
        }

        private void Log(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            Debug.Log("[Graphics] " + message);
        }
    }

    /// <summary>
    /// Скрытый узел-носитель: вызывает кадровое обслуживание службы графики.
    /// Создаётся только в PlayMode (в редакторе без воспроизведения он не нужен).
    /// </summary>
    public class KvGraphicsRuntime : MonoBehaviour
    {
        /// <summary>Обслуживаемая служба.</summary>
        public KvGraphicsService Service;

        private void Update()
        {
            if (Service == null) return;
            try { Service.Tick(Time.unscaledDeltaTime); }
            catch (Exception e) { Debug.LogWarning("[Graphics] кадровое обслуживание: " + e.Message); }
        }

        private void OnApplicationQuit()
        {
            // Возвращаем HDRP-ассету исходные настройки: правки в редакторе не должны «залипать».
            if (Service != null) Service.RestoreAsset();
        }
    }
}
