using System;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.OpenXR;

namespace KazistovVvXr
{
    /// <summary>
    /// РЕАЛЬНЫЕ ПРОБЫ ВОЗМОЖНОСТЕЙ MR/AR (создано 30.10.2026, S44; переписано 02.11.2026, S45 —
    /// проба сквозного вида).
    ///
    /// ЧТО БЫЛО И ПОЧЕМУ НЕ РАБОТАЛО. Первая версия «спрашивала OpenXR без вендорского SDK» так:
    /// брала `OpenXRSettings.ActiveFeatures` через отражение и искала среди них фичу с «CompositionLayer»
    /// в имени. Это неверно по двум причинам:
    ///   1) `OpenXRSettings.ActiveFeatures` — список фич, собранных ДЛЯ СБОРКИ; в APK он не
    ///      описывает, что РАНТАЙМ реально выдал, а в сборке свойство может быть недоступно вовсе;
    ///   2) фича композиционных слоёв включена ВСЕГДА (она нужна для панелей), поэтому признак
    ///      «есть CompositionLayer» ничего не говорил о сквозном виде.
    /// Итог на устройстве: строка «возможности MR/AR: сквозной вид неизвестно · якоря неизвестно ·
    /// камера неизвестно», режим всегда VR. Это и есть известный дефект, с которого начат перенос MR.
    ///
    /// ЧТО СТАЛО. Единственный честный публичный источник — сам рантайм OpenXR:
    /// `OpenXRRuntime.GetEnabledExtensions()` — список расширений, КОТОРЫЕ РАНТАЙМ ВЫДАЛ этой
    /// сборке. Сквозной вид — это расширение `XR_FB_passthrough` (PICO реализует его же),
    /// пространственные якоря — `XR_FB_spatial_entity` / `XR_PICO_spatial_anchor` и т.д.
    /// Список непустой только при живом XR, поэтому в редакторе проба по-прежнему честно `No`.
    ///
    /// ГРАНИЦА ЧЕСТНОСТИ, КОТОРУЮ НЕЛЬЗЯ ПЕРЕХОДИТЬ. Если рантайм ответил пустым списком (не
    /// успел подняться), проба возвращает `Unknown`, а НЕ «да»: выдать MR за VR — значит показать
    /// оператору серый экран вместо комнаты. `Unknown` в выборе режима читается как «нет», но в
    /// журнале называется отдельно — чтобы наладчик видел разницу между «устройство не умеет» и
    /// «мы не смогли спросить».
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
        /// ПРИСУТСТВИЕ любого из них среди ВЫДАННЫХ рантаймом.
        ///
        /// `XR_FB_passthrough` — Meta и PICO (PICO реализует расширение Meta для совместимости;
        /// так же его включает `PassthroughFeature` из пакета PICO OpenXR: extensionString =
        /// "XR_FB_passthrough"). `XR_BD_passthrough` — Bytekdance/PICO на случай отдельного имени.
        /// </summary>
        public static readonly string[] PassthroughExtensionNames =
        {
            "XR_FB_passthrough",                       // Meta (Quest) и PICO (совместимость)
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
            "XR_PICO_spatial_anchor",                  // явное имя PICO (пакет PICO OpenXR)
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
        /// ВЫДАЛ ЛИ РАНТАЙМ ЭТО РАСШИРЕНИЕ. Это ЕДИНСТВЕННОЕ место, которое реально спрашивает
        /// платформу; всё остальное — чистая логика, проверяемая тестом.
        ///
        /// ИСТОЧНИК ПРАВДЫ — `OpenXRRuntime.GetEnabledExtensions()`. Это публичный API пакета
        /// OpenXR (не вендорский SDK, решение [R3] не нарушается) и он одинаков для PICO и Meta.
        /// Прямое обращение к `OpenXRRuntime.IsExtensionEnabled` было бы короче, но у него есть
        /// подвох: до инициализации XR он возвращает false, то есть «не умеет» и «ещё не спросили»
        /// снова сливаются. Список это различает: пустой список = «спросить не удалось».
        /// </summary>
        public static Answer CheckExtension(string[] knownNames)
        {
            if (knownNames == null || knownNames.Length == 0) return Answer.Unknown;
            if (!XrRunning) return Answer.No;          // XR не поднят — расширения точно нет

            try
            {
                string[] enabled = OpenXRRuntime.GetEnabledExtensions();
                if (enabled == null || enabled.Length == 0) return Answer.Unknown;   // рантайм ещё не ответил

                for (int i = 0; i < knownNames.Length; i++)
                {
                    string name = knownNames[i];
                    if (string.IsNullOrEmpty(name)) continue;
                    for (int k = 0; k < enabled.Length; k++)
                    {
                        if (string.Equals(enabled[k], name, StringComparison.Ordinal)) return Answer.Yes;
                    }
                }

                // Список получен, но нужного расширения в нём нет — это ТОЧНЫЙ ответ «нет»,
                // а не «неизвестно»: мы спросили и получили ответ.
                return Answer.No;
            }
            catch (Exception)
            {
                return Answer.Unknown;
            }
        }

        /// <summary>
        /// СКВОЗНОЙ ВИД: можно ли показать комнату оператора.
        ///
        /// ПРОВЕРЯЕТСЯ ФАКТ, А НЕ НАМЕРЕНИЕ. Расширение `XR_FB_passthrough` попадает в список
        /// выданных только если: (1) фича сквозного вида включена в настройках OpenXR для Android
        /// (её включает `KvMrSetupTools` — иначе приложение честно останется в VR), и (2) рантайм
        /// шлема это расширение поддерживает. Оба условия — настоящие предпосылки MR.
        /// </summary>
        public static Answer Passthrough()
        {
            return CheckExtension(PassthroughExtensionNames);
        }

        /// <summary>
        /// ПРОСТРАНСТВЕННЫЕ ЯКОРЯ: есть ли у рантайма опора на реальный мир.
        ///
        /// ЗАЧЕМ В MR. Якорь нужен, чтобы место робота на реальном столе переживало перезапуск и
        /// уточнение карты комнаты. Без якорей MR всё равно работает — мир привязан к позе
        /// трекинга (это разрешено решением по MR), но переживёт только сеанс.
        /// </summary>
        public static Answer SpatialAnchors()
        {
            return CheckExtension(SpatialAnchorExtensionNames);
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

        /// <summary>Строка о выданных расширениях — для наладки на устройстве (её пишет `KvMrRuntime`).</summary>
        public static string DescribeEnabledExtensions()
        {
            if (!XrRunning) return "расширения OpenXR: XR не запущен";
            try
            {
                string[] enabled = OpenXRRuntime.GetEnabledExtensions();
                if (enabled == null) return "расширения OpenXR: рантайм не ответил";
                return "расширения OpenXR (" + enabled.Length + "): " + string.Join(" ", enabled);
            }
            catch (Exception e)
            {
                return "расширения OpenXR: ошибка чтения (" + e.GetType().Name + ")";
            }
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
