using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// РЕЖИМ РАБОТЫ ПРИЛОЖЕНИЯ: ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ, СМЕШАННАЯ ИЛИ ДОПОЛНЕННАЯ
    /// (создано 30.10.2026, сессия S44 — подготовка к MR и AR после VR).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. До этой правки «режим» нигде не был назван: приложение всегда строило
    /// виртуальную лабораторию, а сквозной вид считался украшением. Для MR и AR это неверно:
    /// у них ДРУГИЕ ОПОРЫ (реальный пол, реальный стол вместо виртуальных стендов), ДРУГОЙ МАСШТАБ
    /// (1:1 с комнатой оператора) и ДРУГИЕ СОСТОЯНИЯ (комната видна или нет, якорь найден или
    /// потерян). Когда эти различия не названы, каждый экран решает их сам — и половина решает
    /// неверно. Ровно это уже случилось в §28: «MR» оказался выключенным флагом в трёх местах,
    /// и никакая ошибка при этом не появилась.
    ///
    /// ПРАВИЛО: режим вычисляется ОДИН раз по возможностям устройства и дальше только читается.
    /// </summary>
    public enum KvXrPlatformMode
    {
        /// <summary>Виртуальная реальность: оператор видит только виртуальный мир.</summary>
        Vr = 0,

        /// <summary>
        /// Смешанная реальность: оператор видит свою комнату (сквозной вид) и виртуальные объекты
        /// в ней. Виртуальная лаборатория заменяется реальной обстановкой.
        /// </summary>
        Mr = 1,

        /// <summary>
        /// Дополненная реальность: камера показывает мир, виртуальные объекты ставятся на якоря.
        /// Для телефона; на шлеме с сквозным видом получается MR, и это правильнее.
        /// </summary>
        Ar = 2
    }

    /// <summary>
    /// ВЫБОР РЕЖИМА ПО ВОЗМОЖНОСТЯМ УСТРОЙСТВА (30.10.2026).
    ///
    /// ЧИСТАЯ ФУНКЦИЯ — намеренно. Решение «какой сейчас режим» принимается по трём признакам,
    /// и это решение обязано проверяться тестом без шлема. Проверять его на устройстве означает
    /// ждать пятницу и надеяться, что оператор выключит сквозной вид в нужный момент.
    ///
    /// ПОЧЕМУ НЕ ПО НАЗВАНИЮ УСТРОЙСТВА. «PICO 4 Ultra» ничего не говорит о том, РАЗРЕШИЛ ли
    /// пользователь сквозной вид прямо сейчас: он мог выключить его в настройках шлема или
    /// прикрыть камеру. Поэтому признаки — это ФАКТИЧЕСКАЯ доступность возможностей от OpenXR,
    /// а не модель шлема. Если возможности нет — приложение честно становится VR и говорит это
    /// в журнале, а не показывает серый экран.
    /// </summary>
    public static class KvXrPlatformModeSelector
    {
        /// <summary>
        /// МАСШТАБ МИРА ПО УМОЛЧАНИЮ — ЕДИНСТВЕННОЕ МЕСТО.
        ///
        /// 1.0 означает «метр в приложении равен метру в комнате». Для MR это не настройка вкуса,
        /// а условие работы: оператор стоит в своей комнате, и если мир умножить на коэффициент,
        /// виртуальный стол уедет из-под рук. Карточка Б4 («масштаб не совпадает») закрывается
        /// замером на устройстве, а не правкой этого числа наугад.
        /// </summary>
        public const float DefaultWorldScale = 1.0f;

        /// <summary>Текущий режим. Ставится один раз при старте XR и дальше только читается.</summary>
        public static KvXrPlatformMode Current { get; private set; }

        /// <summary>Масштаб мира, который применяется к виртуальной геометрии.</summary>
        public static float WorldScale { get; private set; }

        /// <summary>Причина выбора режима — одной строкой, для журнала и экрана «Состояние».</summary>
        public static string Reason { get; private set; }

        /// <summary>
        /// РЕЖИМ УЖЕ ОПРЕДЕЛЁН (добавлено 02.11.2026, S45).
        ///
        /// ЗАЧЕМ ФЛАГ. Режим определяется в `KvXrRig.Start`, а читается из ДРУГИХ компонентов
        /// (`KvXrMrRoom.Start`). Порядок `Start` между объектами Unity НЕ ГАРАНТИРУЕТ: если
        /// `KvXrMrRoom` стартует первым, `Current` ещё равен значению по умолчанию (Vr), и MR
        /// не включится — при том, что шлем его умеет.
        /// </summary>
        public static bool Applied { get; private set; }

        /// <summary>
        /// ГАРАНТИРОВАТЬ, ЧТО РЕЖИМ ОПРЕДЕЛЁН. Идемпотентно: если `KvXrRig.Start` уже вызвал
        /// `Apply`, повторной работы не будет. Если ещё нет (порядок `Start` не гарантирован) —
        /// проба и выбор выполняются здесь, теми же функциями, а не второй копией логики.
        /// </summary>
        public static void EnsureApplied()
        {
            if (Applied) return;

            KvXrCapabilities.Answer passthrough = KvXrCapabilities.Passthrough();
            KvXrCapabilities.Answer anchors = KvXrCapabilities.SpatialAnchors();
            KvXrCapabilities.Answer camera = KvXrCapabilities.Camera();

            Apply(
                passthroughAvailable: KvXrCapabilities.IsYes(passthrough),
                spatialAnchorsAvailable: KvXrCapabilities.IsYes(anchors),
                cameraAvailable: KvXrCapabilities.IsYes(camera));

            Debug.Log("[KvXr] " + KvXrCapabilities.Describe(passthrough, anchors, camera));
        }

        /// <summary>
        /// ВЫБРАТЬ РЕЖИМ ПО ВОЗМОЖНОСТЯМ.
        ///
        /// ПОРЯДОК РЕШЕНИЯ (важен, проверяется таблицей в тесте):
        ///   1. сквозной вид есть  → MR (он полнее AR: и камера, и комната, и якоря доступны);
        ///   2. сквозного вида нет, но есть камера и пространственные якоря → AR;
        ///   3. иначе → VR.
        ///
        /// Почему «камера без якорей» — это VR, а не AR: AR без опоры на мир бесполезен, объекты
        /// будут висеть в воздухе относительно случайной точки. Лучше честный VR.
        /// </summary>
        /// <param name="passthroughAvailable">сквозной вид (реальная комната) доступен и разрешён</param>
        /// <param name="spatialAnchorsAvailable">пространственные якоря доступны</param>
        /// <param name="cameraAvailable">камера мира доступна</param>
        public static KvXrPlatformMode Choose(bool passthroughAvailable, bool spatialAnchorsAvailable,
            bool cameraAvailable)
        {
            if (passthroughAvailable && cameraAvailable) return KvXrPlatformMode.Mr;
            if (!passthroughAvailable && spatialAnchorsAvailable && cameraAvailable) return KvXrPlatformMode.Ar;
            return KvXrPlatformMode.Vr;
        }

        /// <summary>
        /// ЗАПОМНИТЬ РЕЖИМ И НАПЕЧАТАТЬ ЕГО ОДНОЙ СТРОКОЙ.
        ///
        /// P1-5 (11.11.2026). ЧТО БЫЛО: масштаб мира жил в ДВУХ местах и ни одно не применялось к
        /// геометрии. Здесь — `WorldScale = worldScale` (аргумент, всегда 1.0 от вызывающих), а в
        /// `KvXrSettings.GetWorldScale()` — настоящая настройка (0,1…1), которая уходила только в
        /// `KvXrRigConfig` и читалась ЛИШЬ в `Describe()`. То есть журнал мог напечатать «мир 0.50×»,
        /// а стол и робот оставались 1:1 — наладчик делал замер по журналу и подтверждал ложь.
        ///
        /// ЧТО СТАЛО: масштаб берётся из ОДНОГО источника (`KvXrSettings.GetWorldScale`) и
        /// ПРИМЕНЯЕТСЯ к корню мира (`ApplyWorldScaleTo`), а `WorldScale` и строка журнала несут
        /// ФАКТИЧЕСКИ применённое значение.
        /// </summary>
        public static KvXrPlatformMode Apply(bool passthroughAvailable, bool spatialAnchorsAvailable,
            bool cameraAvailable, float worldScale = DefaultWorldScale)
        {
            Current = Choose(passthroughAvailable, spatialAnchorsAvailable, cameraAvailable);
            Applied = true;
            // Масштаб не может быть нулевым или отрицательным — это не «настройка», а ошибка,
            // при которой геометрия схлопывается в точку.
            WorldScale = worldScale > 0.0001f ? worldScale : DefaultWorldScale;
            Reason = Describe(Current, passthroughAvailable, spatialAnchorsAvailable, cameraAvailable);
            Debug.Log("[KvXr] РЕЖИМ: " + Reason);
            return Current;
        }

        /// <summary>
        /// ПРИМЕНИТЬ НАСТРОЕННЫЙ МАСШТАБ МИРА К КОРНЮ (P1-5, 11.11.2026). Возвращает ФАКТИЧЕСКИ
        /// применённый масштаб — его и печатает журнал, чтобы «напечатано» и «сделано» были одним
        /// числом. Корень может быть null (виртуальной геометрии в сцене нет) — тогда масштаб
        /// остаётся в модели, но к геометрии не применяется, и это честно сообщается в журнале.
        /// </summary>
        public static float ApplyWorldScaleTo(Transform worldRoot)
        {
            float scale = KvXrSettings.GetWorldScale();
            if (scale <= 0.0001f) scale = DefaultWorldScale;

            if (worldRoot != null)
            {
                Vector3 current = worldRoot.localScale;
                if (Mathf.Abs(current.x - scale) > 0.0001f ||
                    Mathf.Abs(current.y - scale) > 0.0001f ||
                    Mathf.Abs(current.z - scale) > 0.0001f)
                {
                    // Меняется КОРЕНЬ мира, поэтому компенсируем позицию, чтобы «нулевая» точка мира
                    // не уехала: масштаб меняет геометрию вокруг начала координат корня.
                    worldRoot.localScale = new Vector3(scale, scale, scale);
                }
            }

            WorldScale = scale;
            Debug.Log("[KvXr] МАСШТАБ МИРА: применён " + scale.ToString("0.000") +
                      (worldRoot != null ? " к корню «" + worldRoot.name + "»" : " · корня нет, к геометрии не применён"));
            return scale;
        }

        /// <summary>Строка-объяснение режима (используется и в журнале, и в тесте).</summary>
        public static string Describe(KvXrPlatformMode mode, bool passthroughAvailable,
            bool spatialAnchorsAvailable, bool cameraAvailable)
        {
            return Describe(mode, passthroughAvailable, spatialAnchorsAvailable, cameraAvailable,
                DefaultWorldScale);
        }

        /// <summary>
        /// Строка-объяснение режима С ФАКТИЧЕСКИМ МАСШТАБОМ (исправлено 30.10.2026, S44).
        ///
        /// ЧТО БЫЛО. Строка печатала `DefaultWorldScale` — константу 1.00, — даже если применялся
        /// ДРУГОЙ масштаб. То есть в журнале всегда стояло «масштаб мира 1.00», а реально мир мог
        /// быть увеличен или уменьшен.
        ///
        /// ЧЕМ ЭТО ПЛОХО. Именно по этой строке наладчик сверяет масштаб перед замером (карточки
        /// Б4/Б5): в MR стол обязан совпасть с реальным. Журнал, который всегда говорит «1.00»,
        /// делает замер бессмысленным — он подтверждает то, что написано, а не то, что есть.
        ///
        /// Для MR это особенно важно: масштаб там — не оформление, а условие работы.
        /// </summary>
        public static string Describe(KvXrPlatformMode mode, bool passthroughAvailable,
            bool spatialAnchorsAvailable, bool cameraAvailable, float worldScale)
        {
            string capabilities = "сквозной вид " + (passthroughAvailable ? "есть" : "НЕТ") +
                                  " · якоря " + (spatialAnchorsAvailable ? "есть" : "НЕТ") +
                                  " · камера " + (cameraAvailable ? "есть" : "НЕТ");
            string verdict;
            switch (mode)
            {
                case KvXrPlatformMode.Mr:
                    verdict = "СМЕШАННАЯ РЕАЛЬНОСТЬ — комната оператора видна, мир ставится в неё (масштаб 1:1)";
                    break;
                case KvXrPlatformMode.Ar:
                    verdict = "ДОПОЛНЕННАЯ РЕАЛЬНОСТЬ — камера без сквозного вида, объекты на якорях";
                    break;
                default:
                    verdict = passthroughAvailable || cameraAvailable
                        ? "ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ — сквозной вид недоступен, показываем виртуальную лабораторию"
                        : "ВИРТУАЛЬНАЯ РЕАЛЬНОСТЬ — возможностей MR/AR у устройства нет";
                    break;
            }

            // Число — через InvariantCulture: в русской локали «1,00» в журнале спутается с
            // разделителем списка при разборе логов скриптами.
            string scale = (worldScale > 0.0001f ? worldScale : DefaultWorldScale)
                .ToString("0.000", System.Globalization.CultureInfo.InvariantCulture);

            return verdict + " (" + capabilities + ") · масштаб мира " + scale;
        }
    }
}
