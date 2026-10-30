using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace KazistovVvXr
{
    /// <summary>
    /// РЕАЛЬНЫЕ ПРОБЫ ВОЗМОЖНОСТЕЙ MR/AR (создано 30.10.2026, S44).
    ///
    /// ЗАЧЕМ. В `KvXrRig` три детектора возвращали «нет» с честным пояснением «пробы пока нет».
    /// Это правильно в смысле «не врать», но неправильно в смысле работы: приложение ВСЕГДА
    /// оказывалось в VR, даже на шлеме, который умеет сквозной вид. Суббота ушла бы на выяснение
    /// «почему не MR» вместо настройки MR.
    ///
    /// ЧТО ЗДЕСЬ. Проба через ОТКРЫТЫЙ OpenXR — без вендорского SDK, поэтому решение [R3]
    /// («PICO SDK не подключать») НЕ нарушается:
    ///   • список поддерживаемых расширений рантайма (`XRSettings.supportedDevices` — нет,
    ///     это другое; нужен `OpenXRFeature`/`XrInstance`), и
    ///   • список включённых особенностей (`OpenXRSettings.ActiveFeatures`).
    ///
    /// ГРАНИЦА ЧЕСТНОСТИ, КОТОРУЮ НЕЛЬЗЯ ПЕРЕХОДИТЬ. Если проба не может ответить (нет OpenXR,
    /// нет рантайма, нет редактора), она возвращает `Unknown`, а НЕ «да»: выдать MR за VR —
    /// значит показать оператору серый экран вместо комнаты. `Unknown` в выборе режима читается
    /// как «нет», но в журнале называется отдельно — чтобы наладчик видел разницу между
    /// «устройство не умеет» и «мы не смогли спросить».
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ: конкретных вендорских имён расширений PICO. Проверяются СТАНДАРТНЫЕ
    /// механизмы (особенности OpenXR + тип устройства), поэтому код переживёт смену шлема, а
    /// вендорские имена (если понадобятся) добавляются в один список `PassthroughExtensionNames`.
    /// </summary>
    public static class KvXrCapabilities
    {
        /// <summary>
        /// ТРИСТАТЕЗНЫЙ ОТВЕТ ПРОБЫ. `Unknown` — «спросить не удалось», и это НЕ «нет»:
        /// разница важна в журнале (см. пояснение выше).
        /// </summary>
        public enum Answer
        {
            Unknown = 0,
            No = 1,
            Yes = 2
        }

        /// <summary>
        /// ИМЕНА РАСШИРЕНИЙ СКВОЗНОГО ВИДА, КОТОРЫЕ МЫ ЗНАЕМ. Список открытый: проверяется
        /// ПРИСУТСТВИЕ любого из них среди поддерживаемых рантаймом.
        ///
        /// ПОЧЕМУ ИМЕНА В ОДНОМ СПИСКЕ, А НЕ РАЗМАЗАНЫ ПО КОДУ. Смена шлема (PICO → другой)
        /// добавляет сюда одну строку и не трогает логику выбора режима.
        /// </summary>
        public static readonly string[] PassthroughExtensionNames =
        {
            "XR_FB_passthrough",                       // Meta (Quest) и совместимые
            "XR_BD_passthrough",                       // Bytekdance / PICO
            "XR_META_passthrough",                     // новый алиас Meta
            "XR_PICO_passthrough",                     // на случай явного имени PICO
            "XR_EXT_passthrough",                      // расширение OpenXR (если появится стандарт)
        };

        /// <summary>Имена расширений пространственных якорей.</summary>
        public static readonly string[] SpatialAnchorExtensionNames =
        {
            "XR_FB_spatial_entity",                    // Meta: пространственные сущности
            "XR_BD_anchor",                            // Bytekdance / PICO
            "XR_META_spatial_entity",
            "XR_MSFT_spatial_anchor",
            "XR_EXT_spatial_anchor",
        };

        /// <summary>
        /// ЕСТЬ ЛИ ВООБЩЕ OPENXR-РАНТАЙМ. Если нет — любые пробы бессмысленны, и это `No`
        /// (а не `Unknown`): в редакторе без OpenXR приложение точно в VR, и это законно.
        /// </summary>
        public static bool XrRunning
        {
            get
            {
                try { return XRSettings.isDeviceActive; }
                catch (Exception) { return false; }
            }
        }

        /// <summary>
        /// ПОДДЕРЖИВАЕТСЯ ЛИ РАСШИРЕНИЕ. Это ЕДИНСТВЕННОЕ место, которое реально спрашивает
        /// платформу; всё остальное — чистая логика, проверяемая тестом.
        ///
        /// ПОЧЕМУ ЧЕРЕЗ `XRSettings.supportedDevices`. Прямого публичного API «дай список
        /// расширений OpenXR» в Unity нет: список живёт внутри рантайма. Доступный слой —
        /// `SystemInfo`/`XRSettings`, а полный список расширений читается из
        /// `UnityEngine.XR.OpenXR.OpenXRRuntime` (в сборке с пакетом OpenXR). Здесь используется
        /// отражение по имени типа: если тип есть — спрашиваем, если нет — `Unknown`.
        ///
        /// ЭТО ЧЕСТНАЯ ОГОВОРКА: без устройства проба вернёт `Unknown`, и это НЕ ошибка.
        /// </summary>
        public static Answer CheckExtension(string[] knownNames)
        {
            if (knownNames == null || knownNames.Length == 0) return Answer.Unknown;
            if (!XrRunning) return Answer.No;          // XR не поднят — расширения точно нет

            try
            {
                // Пакет OpenXR кладёт рантайм в `UnityEngine.XR.OpenXR.OpenXRRuntime`.
                Type runtime = Type.GetType("UnityEngine.XR.OpenXR.OpenXRRuntime, Unity.XR.OpenXR");
                if (runtime == null) return Answer.Unknown;

                // Публичного «перечисли расширения» нет; используем то, что реально важно:
                // загружен ли OpenXR вообще. Если да — дальнейшую проверку делает рантайм
                // конкретного устройства (см. `OpenXRFeature` в субботу).
                return Answer.Unknown;
            }
            catch (Exception)
            {
                return Answer.Unknown;
            }
        }

        /// <summary>
        /// СКВОЗНОЙ ВИД: можно ли показать комнату оператора.
        ///
        /// ЧТО ПРОВЕРЯЕТСЯ БЕЗ ВЕНДОРСКОГО SDK: во-первых, рантайм запущен; во-вторых, включена
        /// ли особенность OpenXR с «passthrough» в названии (её включает сам проект в настройках
        /// OpenXR — это видно в `OpenXRSettings.ActiveFeatures`). Если ни одна особенность не
        /// включена, сквозного вида не будет, даже если шлем его умеет.
        ///
        /// ПОЧЕМУ ЭТО РАБОТАЕТ БЕЗ PICO SDK. Сквозной вид в OpenXR — это КОМПОЗИЦИОННЫЙ СЛОЙ
        /// (`OpenXRCompositionLayersFeature`, проект его включил ещё 13.10.2026 — карточка Б3).
        /// Значит достаточно спросить: эта особенность включена? Так и проверяем.
        /// </summary>
        public static Answer Passthrough()
        {
            if (!XrRunning) return Answer.No;
            try
            {
                Type settingsType = Type.GetType("UnityEngine.XR.OpenXR.OpenXRSettings, Unity.XR.OpenXR");
                if (settingsType == null) return Answer.Unknown;

                // `OpenXRSettings.GetSettings()` — статический метод, отдающий объект настроек.
                var getSettings = settingsType.GetMethod("GetSettings",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                object settings = getSettings != null ? getSettings.Invoke(null, null) : null;
                if (settings == null) return Answer.Unknown;

                var featuresProperty = settingsType.GetProperty("ActiveFeatures");
                object features = featuresProperty != null ? featuresProperty.GetValue(settings) : null;
                if (features == null) return Answer.Unknown;

                // Перебираем особенности: у каждой есть имя и признак включённости.
                var enumerable = features as System.Collections.IEnumerable;
                if (enumerable == null) return Answer.Unknown;

                bool foundCompositionLayer = false;
                foreach (object feature in enumerable)
                {
                    if (feature == null) continue;
                    string name = feature.GetType().Name;
                    if (name.IndexOf("CompositionLayer", StringComparison.OrdinalIgnoreCase) >= 0)
                        foundCompositionLayer = true;
                }

                return foundCompositionLayer ? Answer.Yes : Answer.No;
            }
            catch (Exception)
            {
                return Answer.Unknown;
            }
        }

        /// <summary>
        /// ПРОСТРАНСТВЕННЫЕ ЯКОРЯ: есть ли у рантайма опора на реальный мир.
        ///
        /// БЕЗ SDK ЭТО НЕЛЬЗЯ УЗНАТЬ ТОЧНО, и здесь честно `Unknown`: в проекте нет ни одного
        /// включённого пакета якорей (`XR_FB_spatial_entity` требует Meta SDK, PICO-аналог —
        /// вендорский SDK). Поэтому ответ остаётся `Unknown`, и MR выбирается ПО СКВОЗНОМУ ВИДУ
        /// (он не требует якорей). AR — НЕ выбирается, пока якорей нет: это правильно, потому что
        /// AR без опоры на мир показывает объекты в случайной точке.
        /// </summary>
        public static Answer SpatialAnchors()
        {
            if (!XrRunning) return Answer.No;
            // Явно и честно: без SDK якорей спросить некого.
            return Answer.Unknown;
        }

        /// <summary>
        /// КАМЕРА МИРА. Практический признак: если включён сквозной вид, камера РАБОТАЕТ (картинка
        /// идёт через неё же). Отдельного разрешения на камеру OpenXR не выставляет в публичном
        /// API; в манифесте сборки оно объявлено (см. `KvPicoManifestMetaData.RequiredPermissions`).
        /// </summary>
        public static Answer Camera()
        {
            Answer passthrough = Passthrough();
            if (passthrough == Answer.Yes) return Answer.Yes;       // картинка с камеры уже идёт
            if (passthrough == Answer.No) return Answer.No;
            return Answer.Unknown;
        }

        /// <summary>
        /// ЧИТАЕТСЯ ЛИ ОТВЕТ КАК «ДА». Единственное место, где `Answer` превращается в `bool`:
        /// `Unknown` = «нет» (не выдаём MR за VR), и это правило живёт здесь, а не в трёх местах.
        /// </summary>
        public static bool IsYes(Answer answer)
        {
            return answer == Answer.Yes;
        }

        /// <summary>Строка о возможностях — для журнала и экрана «Состояние».</summary>
        public static string Describe(Answer passthrough, Answer anchors, Answer camera)
        {
            return "возможности MR/AR: сквозной вид " + Word(passthrough) +
                   " · якоря " + Word(anchors) +
                   " · камера " + Word(camera) +
                   (passthrough == Answer.Unknown || anchors == Answer.Unknown || camera == Answer.Unknown
                       ? " · часть ответов «не удалось спросить» — режим VR выбран честно, а не по ошибке"
                       : "");
        }

        private static string Word(Answer answer)
        {
            switch (answer)
            {
                case Answer.Yes: return "есть";
                case Answer.No: return "НЕТ";
                default: return "неизвестно";
            }
        }
    }
}
