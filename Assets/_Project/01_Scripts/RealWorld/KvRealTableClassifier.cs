using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace KazistovVvRealWorld
{
    /// <summary>ЧТО О ПОВЕРХНОСТИ ГОВОРИТ ПЛАТФОРМА (или «Unknown», если она молчит).</summary>
    public enum KvRealSurfaceKind
    {
        /// <summary>Платформа ничего не сказала — решают геометрические правила.</summary>
        Unknown = 0,

        /// <summary>Разметка уверена, что это стол (PICO `PxrSemanticLabel.Table`, ARCore `Table`).</summary>
        Table = 1,

        /// <summary>Разметка уверена, что это пол.</summary>
        Floor = 2,

        /// <summary>Разметка уверена, что это потолок.</summary>
        Ceiling = 3,

        /// <summary>Разметка уверена, что это стена (или дверь/окно в стене).</summary>
        Wall = 4,

        /// <summary>Платформа сказала «что-то другое»: диван, кресло, сиденье.</summary>
        Other = 5
    }

    /// <summary>ОТКУДА ВЗЯЛИСЬ ДАННЫЕ О ПОВЕРХНОСТИ — от этого зависит доверие к ним.</summary>
    public enum KvRealSurfaceSource
    {
        /// <summary>Плоскость ARCore (AR Foundation `ARPlane`).</summary>
        Plane = 0,

        /// <summary>Семантическая разметка комнаты PICO (`PxrSemanticLabel`).</summary>
        Semantic = 1,

        /// <summary>Полигон пространственного меша комнаты.</summary>
        Mesh = 2
    }

    /// <summary>
    /// КАНДИДАТ В СТОЛ — обычные числа, без AR Foundation и без PICO.
    ///
    /// ПОЧЕМУ СВОЯ СТРУКТУРА, А НЕ `ARPlane`/`PxrAnchorComponent`. `ARPlane` нельзя собрать в
    /// EditMode-тесте без запущенной AR-сессии, а `PxrSceneComponentData` — без шлема. Тогда
    /// единственный способ проверить «стол это или пол» — глазами тестировщика на устройстве, а по
    /// правилам проекта правило обязано проверяться машинно. Здесь только числа, поэтому ВСЯ логика
    /// распознавания проверяется тестом без единого устройства.
    /// </summary>
    public struct RealSurfaceCandidate
    {
        /// <summary>Что говорит платформа (Unknown — ничего).</summary>
        public KvRealSurfaceKind Kind;

        /// <summary>Откуда данные: плоскость ARCore, разметка PICO или меш.</summary>
        public KvRealSurfaceSource Source;

        /// <summary>Центр поверхности в мировых координатах, метры.</summary>
        public Vector3 Center;

        /// <summary>Нормаль поверхности в мировых координатах (для стола — вверх).</summary>
        public Vector3 Up;

        /// <summary>Габарит поверхности по первой оси, метры.</summary>
        public float SideA;

        /// <summary>Габарит поверхности по второй оси, метры.</summary>
        public float SideB;

        /// <summary>Сколько секунд поверхность уже отслеживается.</summary>
        public float TrackedSeconds;

        /// <summary>Это самая большая горизонтальная плоскость сцены (в ARCore так выглядит ПОЛ).</summary>
        public bool IsLargestHorizontal;
    }

    /// <summary>ЧТО РЕШЕНО О ПОВЕРХНОСТИ: стол или нет, насколько уверенно и почему (с числами).</summary>
    public struct RealTableVerdict
    {
        /// <summary>Это стол (можно ставить на него робота).</summary>
        public bool IsTable;

        /// <summary>Уверенность 0…1 (0 — данных нет, 0.95 — точная семантика платформы).</summary>
        public float Confidence;

        /// <summary>Кем поверхность признана: стол, пол, потолок, стена или «неизвестно».</summary>
        public KvRealSurfaceKind Kind;

        /// <summary>
        /// Почему решено именно так. ОБЯЗАН содержать числа (площадь, высоту над полом, наклон,
        /// уверенность): по этой строке тестировщик в logcat доказывает результат, а не верит на слово.
        /// </summary>
        public string Reason;
    }

    /// <summary>
    /// КЛАССИФИКАТОР «РЕАЛЬНЫЙ СТОЛ ИЛИ ПРОСТО ПОВЕРХНОСТЬ» — ЧИСТАЯ ФУНКЦИЯ БЕЗ ПОБОЧНЫХ ЭФФЕКТОВ.
    ///
    /// ЧТО БЫЛО (дефект, ради которого появился этот файл). И в AR, и в MR стол выбирался правилом
    /// «самая большая горизонтальная плоскость». Пол горизонтален, огромен и стабилен — поэтому
    /// правило почти всегда выбирало ПОЛ, а подпись на экране говорила «стол найден». Робот вставал
    /// на пол, а оператор был уверен, что стоит на столе. В MR разметка `Table` запрашивалась, но её
    /// результат никто не проверял: строка «получено столов N» ничего не доказывала.
    ///
    /// ЧЕМ ПЛОХО «САМАЯ БОЛЬШАЯ». Площадь ничего не говорит о назначении поверхности: пол всегда
    /// больше любой столешницы. Нужны ещё три признака, которых прежнее правило не проверяло вовсе:
    /// ВЫСОТА над полом (стол ≥ 0.25 м), ВЫСОТА относительно глаз (стол ниже глаз на 0.35…1.5 м) и
    /// ГАБАРИТЫ столешницы (0.25…3 м², стороны ≥ 0.35 м, отношение сторон ≤ 4).
    ///
    /// ЧТО СТАЛО. Один детерминированный разбор: семантика платформы сильнее геометрии, геометрия
    /// сильнее «площади», гистерезис не даёт выбору мерцать между двумя столами. Все пороги —
    /// именованные константы с объяснением; каждое решение объясняется строкой с числами.
    ///
    /// ГРАНИЦА ЧЕСТНОСТИ. Классификатор не «видит» стол — он проверяет числа, которые ему дали.
    /// Если пол не найден и габариты врут, стол не распознается; тогда он говорит «недостаточно
    /// данных» с числами, а не выдаёт пол за стол. Что именно осталось непроверяемым без реального
    /// стола перед камерой — перечислено в отчёте сессии.
    /// </summary>
    public static class KvRealTableClassifier
    {
        // ---------------------------------------------------------------- пороги (числа-правила)

        /// <summary>
        /// НАКЛОН: `dot(Up, вверх)` не меньше 0.90, то есть наклон не больше 25.8°.
        ///
        /// Почему 0.90: столешница бывает наклонена (стол с регулируемыми ножками, кухонный остров),
        /// но пол и стол отличаются от стен принципиально. 0.87 — это уже 30°, при таком наклоне
        /// «верх» поверхности не столешница, а пандус/откидная доска: ставить робота нельзя.
        /// </summary>
        public const float MinUpDot = 0.90f;

        /// <summary>Тот же порог в градусах — для строки причины (тестировщик считает глазами).</summary>
        public const float MaxTiltDegrees = 25.84f;

        /// <summary>
        /// МИНИМАЛЬНАЯ ПЛОЩАДЬ СТОЛЕШНИЦЫ 0.25 м² (например 0.5 × 0.5 м).
        ///
        /// Почему 0.25: робот в проекте метрический, и на столешнице меньше 0.5×0.5 м он физически
        /// не поместится. Меньшие плоскости — шум ARCore (кусок стула, книга, край стола).
        /// </summary>
        public const float MinTableArea = 0.25f;

        /// <summary>
        /// МАКСИМАЛЬНАЯ ПЛОЩАДЬ СТОЛЕШНИЦЫ 3.0 м² (например 2.0 × 1.5 м).
        ///
        /// Почему 3.0: самый большой реальный обеденный стол — около 2×1.5 м. Всё, что больше, —
        /// это пол, площадка или стена, положенная горизонтально; ставить туда робота «на стол»
        /// значит повторять ровно тот дефект, ради которого написано это правило.
        /// </summary>
        public const float MaxTableArea = 3.0f;

        /// <summary>Минимальная сторона столешницы 0.35 м: уже — это полка или подоконник, а не стол.</summary>
        public const float MinTableSideMeters = 0.35f;

        /// <summary>
        /// МАКСИМАЛЬНОЕ ОТНОШЕНИЕ СТОРОН 4:1 (например 1.6 × 0.4 м).
        ///
        /// Почему 4: столешница — прямоугольник, но не «полоса». 0.3 × 5 м = 16:1 — это подоконник,
        /// барная стойка или край подиума; робот с такой поверхности свалится.
        /// </summary>
        public const float MaxTableAspect = 4f;

        /// <summary>
        /// СТОЛ НИЖЕ ГЛАЗ НЕ МЕНЕЕ 0.35 м. Если поверхность почти на уровне глаз — это не стол, а
        /// полка на уровне лица / поднятая столешница, куда робота не поставить.
        /// </summary>
        public const float MinHeightBelowEyesMeters = 0.35f;

        /// <summary>
        /// СТОЛ НИЖЕ ГЛАЗ НЕ БОЛЕЕ 1.5 м. Глубже — это уже уровень колена/пола: поверхность ниже
        /// 1.5 м от глаз взрослого человека почти всегда пол или ступень.
        /// </summary>
        public const float MaxHeightBelowEyesMeters = 1.5f;

        /// <summary>
        /// СТОЛ ВЫШЕ ПОЛА НЕ МЕНЕЕ 0.25 м (высота обычного табурета). Главное правило против
        /// дефекта «пол подписан столом»: если пол известен, всё, что на его уровне, — пол.
        /// </summary>
        public const float MinHeightAboveFloorMeters = 0.25f;

        /// <summary>
        /// СКОЛЬКО СЕКУНД ПОВЕРХНОСТЬ ОБЯЗАНА ОТСЛЕЖИВАТЬСЯ, прежде чем ей верить: 0.5 с.
        ///
        /// Почему: ARCore отдаёт плоскость в первом же кадре с приблизительными габаритами; за
        /// полсекунды они уточняются. Решение, принятое по первому кадру, — это мерцание подписи
        /// «стол найден / стол не найден» на экране оператора.
        /// </summary>
        public const float MinTrackedSeconds = 0.5f;

        /// <summary>
        /// ГИСТЕРЕЗИС 20 %: новый кандидат заменяет текущий стол только если он лучше ЗАМЕТНО.
        ///
        /// Почему: у двух настоящих столов уверенность почти одинаковая, и без гистерезиса каждый
        /// пересчёт мог бы переключать выбор — на экране это выглядит как мерцание зелёной подсветки
        /// между двумя столами, и оператор не понимает, куда встанет робот.
        /// </summary>
        public const float HysteresisMargin = 0.20f;

        /// <summary>Уверенность «платформа прямо сказала: это стол»: семантика сильнее любой геометрии.</summary>
        public const float SemanticTableConfidence = 0.95f;

        /// <summary>Уверенность решения «это точно НЕ стол» (пол, потолок, стена по разметке).</summary>
        public const float DecisiveRejectConfidence = 0.90f;

        /// <summary>Площадь «идеальной» столешницы 0.9 м² (1.2 × 0.75) — от неё считается уверенность.</summary>
        public const float IdealTableArea = 0.9f;

        /// <summary>Сколько секунд устойчивого трекинга считаем полным доверием (для уверенности).</summary>
        public const float StableTrackedSeconds = 4f;

        /// <summary>
        /// ПЛОЩАДЬ ДЛЯ ВЫВОДА ПОЛА: 0.6 м². Пол — большая плоскость; по куску меньше 0.6 м²
        /// уровень пола не определяем (это может быть тот же стол).
        /// </summary>
        public const float FloorInferenceMinArea = 0.6f;

        /// <summary>
        /// ПЕРЕПАД МЕЖДУ УРОВНЯМИ ДЛЯ ВЫВОДА ПОЛА: 0.35 м. Пол выводим, только если в сцене видно
        /// ДВА уровня (пол + что-то над ним). Иначе единственная горизонтальная плоскость перед
        /// камерой — сам стол, и объявлять её полом значит запретить постановку на реальный стол.
        /// </summary>
        public const float FloorInferenceMinGapMeters = 0.35f;

        private const float GeometricBaseConfidence = 0.45f;
        private const float AreaWeight = 0.18f;
        private const float AspectWeight = 0.12f;
        private const float StabilityWeight = 0.10f;
        private const float MaxGeometricConfidence = 0.85f;

        // ---------------------------------------------------------------- разбор одного кандидата

        /// <summary>Разбор без известного пола: остаются наклон, высота относительно глаз и габариты.</summary>
        public static RealTableVerdict Classify(RealSurfaceCandidate candidate, float eyeHeightMeters)
        {
            return Classify(candidate, eyeHeightMeters, 0f, false);
        }

        /// <summary>
        /// РАЗБОР КАНДИДАТА. Порядок правил не случаен: сначала семантика платформы (ей верим
        /// больше всего), затем «данных мало», затем форма (наклон), затем высота (пол/уровень глаз),
        /// и только в конце габариты. Так причина отказа всегда называет САМОЕ ВАЖНОЕ число.
        ///
        /// `eyeHeightMeters` — высота камеры/глаз в ТОЙ ЖЕ системе координат, что `candidate.Center`
        /// (в ARCore это мировые координаты сессии, в шлеме — координаты трекинга). 0 или меньше —
        /// «высота глаз неизвестна», правило уровня глаз пропускается.
        /// `floorHeightMeters` + `floorHeightKnown` — высота пола, если её удалось определить.
        /// </summary>
        public static RealTableVerdict Classify(RealSurfaceCandidate candidate, float eyeHeightMeters,
                                                float floorHeightMeters, bool floorHeightKnown)
        {
            float sideA = Mathf.Abs(candidate.SideA);
            float sideB = Mathf.Abs(candidate.SideB);
            float area = sideA * sideB;

            // 1. ГИГИЕНА ЧИСЕЛ. Данные приходят ИЗВНЕ (ARCore, PICO), а при потере трекинга там
            // штатно появляется NaN. Сравнение `area < MinTableArea` с NaN даёт false — то есть
            // «проверка» пропустила бы мусор и объявила столом точку в никуда. Отсекаем явно.
            if (!IsFinite(candidate.Center) || !IsFinite(candidate.Up) ||
                !IsFinite(candidate.SideA) || !IsFinite(candidate.SideB) ||
                !IsFinite(candidate.TrackedSeconds) || float.IsNaN(area))
            {
                return Reject(KvRealSurfaceKind.Unknown, 0f,
                    "данные поверхности непригодны (NaN/∞): площадь " + Num(area) + " м², нормаль " +
                    Vec3Text(candidate.Up) + " · уверенность 0.00");
            }

            float upDot = 0f;
            if (candidate.Up.sqrMagnitude > 0.000001f)
                upDot = Mathf.Clamp01(Vector3.Dot(candidate.Up.normalized, Vector3.up));
            float tiltDegrees = Mathf.Acos(Mathf.Clamp(upDot, -1f, 1f)) * Mathf.Rad2Deg;
            float heightAboveFloor = candidate.Center.y - floorHeightMeters;
            float heightBelowEyes = eyeHeightMeters - candidate.Center.y;
            float minSide = Mathf.Min(sideA, sideB);
            float maxSide = Mathf.Max(sideA, sideB);
            float aspect = minSide > 0.0001f ? maxSide / minSide : float.MaxValue;

            // 2. СЕМАНТИКА ПЛАТФОРМЫ СИЛЬНЕЕ ГЕОМЕТРИИ. Если PICO/ARCore разметили объект как стол —
            // это стол, даже если наши геометрические правила сомневаются: разметка видит комнату
            // целиком, а мы — один прямоугольник.
            if (candidate.Kind == KvRealSurfaceKind.Table)
            {
                return Accept(SemanticTableConfidence, KvRealSurfaceKind.Table,
                    "разметка платформы: СТОЛ · площадь " + Num(area) + " м² (" + Num(sideA) + "×" +
                    Num(sideB) + " м) · уверенность " + Num(SemanticTableConfidence));
            }

            // 3. Разметка «пол»/«потолок»/«стена» — это ОТКАЗ, и он не обсуждается площадью:
            // огромная горизонтальная плоскость пола больше не выигрывает конкурс «самая большая».
            if (candidate.Kind == KvRealSurfaceKind.Floor)
            {
                return Reject(KvRealSurfaceKind.Floor, DecisiveRejectConfidence,
                    "разметка платформы: ПОЛ, а не стол · площадь " + Num(area) + " м² · уверенность " +
                    Num(DecisiveRejectConfidence));
            }
            if (candidate.Kind == KvRealSurfaceKind.Ceiling)
            {
                return Reject(KvRealSurfaceKind.Ceiling, DecisiveRejectConfidence,
                    "разметка платформы: ПОТОЛОК, а не стол · площадь " + Num(area) + " м² · уверенность " +
                    Num(DecisiveRejectConfidence));
            }
            if (candidate.Kind == KvRealSurfaceKind.Wall)
            {
                return Reject(KvRealSurfaceKind.Wall, DecisiveRejectConfidence,
                    "разметка платформы: СТЕНА · наклон " + Num(tiltDegrees) + "° · уверенность " +
                    Num(DecisiveRejectConfidence));
            }

            // 4. МАЛО ДАННЫХ. Не отказ навсегда: через полсекунды тот же кандидат станет столом —
            // поэтому в причине написано, чего именно ждём.
            if (candidate.TrackedSeconds < MinTrackedSeconds)
            {
                return Reject(candidate.Kind, 0f,
                    "недостаточно данных: поверхность отслеживается " + Num(candidate.TrackedSeconds) +
                    " с из " + Num(MinTrackedSeconds) + " с (поверхность ещё уточняется) · площадь " +
                    Num(area) + " м² · уверенность 0.00");
            }

            // 5. НАКЛОН. Нормаль обязана смотреть вверх.
            if (upDot < MinUpDot)
            {
                return Reject(candidate.Kind, 0f,
                    "наклон " + Num(tiltDegrees) + "° больше допустимых " + Num(MaxTiltDegrees) +
                    "° (нормаль вверх " + Num(upDot) + ") · площадь " + Num(area) +
                    " м² · уверенность 0.00");
            }

            // 6. ГЛАВНОЕ ПРАВИЛО ПРОТИВ ПОЛА. Если пол известен, поверхность на его уровне — пол.
            // Именно это правило прежний код не проверял вовсе, и пол становился «столом».
            if (floorHeightKnown)
            {
                if (heightAboveFloor < MinHeightAboveFloorMeters)
                {
                    return Reject(KvRealSurfaceKind.Floor, DecisiveRejectConfidence,
                        "высота над полом " + Num(heightAboveFloor) + " м меньше " + Num(MinHeightAboveFloorMeters) +
                        " м: это ПОЛ, а не стол · площадь " + Num(area) + " м² · уверенность " +
                        Num(DecisiveRejectConfidence));
                }
            }

            // 7. ВЫСОТА ОТНОСИТЕЛЬНО ГЛАЗ. Поверхность на уровне глаз или выше — не стол.
            if (eyeHeightMeters > 0.01f && IsFinite(eyeHeightMeters))
            {
                if (heightBelowEyes < MinHeightBelowEyesMeters)
                {
                    return Reject(candidate.Kind, 0f,
                        "поверхность на уровне глаз: ниже камеры всего " + Num(heightBelowEyes) +
                        " м (стол обязан быть ниже глаз на " + Num(MinHeightBelowEyesMeters) + "…" +
                        Num(MaxHeightBelowEyesMeters) + " м) · площадь " + Num(area) + " м² · уверенность 0.00");
                }
                if (heightBelowEyes > MaxHeightBelowEyesMeters)
                {
                    return Reject(candidate.Kind, 0f,
                        "слишком низко: " + Num(heightBelowEyes) + " м ниже глаз, а стол — не глубже " +
                        Num(MaxHeightBelowEyesMeters) + " м (похоже на пол) · площадь " + Num(area) +
                        " м² · уверенность 0.00");
                }
            }

            // 8. ГАБАРИТЫ СТОЛЕШНИЦЫ: площадь в коридоре, стороны, отношение сторон.
            if (area < MinTableArea)
            {
                return Reject(candidate.Kind, 0f,
                    "площадь " + Num(area) + " м² меньше " + Num(MinTableArea) + " м² (" + Num(sideA) +
                    "×" + Num(sideB) + " м) — это не столешница · уверенность 0.00");
            }
            if (area > MaxTableArea)
            {
                return Reject(candidate.Kind, 0f,
                    "площадь " + Num(area) + " м² больше " + Num(MaxTableArea) + " м² (" + Num(sideA) +
                    "×" + Num(sideB) + " м) — это не столешница, а пол/площадка · уверенность 0.00");
            }
            if (aspect > MaxTableAspect)
            {
                return Reject(candidate.Kind, 0f,
                    "отношение сторон " + Num(aspect) + " больше " + Num(MaxTableAspect) + " (" + Num(sideA) +
                    "×" + Num(sideB) + " м) — это полоса/подоконник, а не столешница · уверенность 0.00");
            }
            if (minSide < MinTableSideMeters)
            {
                return Reject(candidate.Kind, 0f,
                    "сторона " + Num(minSide) + " м меньше " + Num(MinTableSideMeters) + " м (" + Num(sideA) +
                    "×" + Num(sideB) + " м) — на такой не устоять · уверенность 0.00");
            }

            // 9. СТОЛ ПО ГЕОМЕТРИИ. Уверенность — из площади, пропорций и устойчивости трекинга.
            float confidence = ScoreConfidence(area, aspect, candidate.TrackedSeconds, candidate.Source,
                candidate.IsLargestHorizontal);
            string parts = "стол по геометрии: площадь " + Num(area) + " м² (" + Num(sideA) + "×" +
                           Num(sideB) + " м)";
            if (floorHeightKnown)
                parts += " · высота над полом " + Num(heightAboveFloor) + " м";
            parts += " · наклон " + Num(tiltDegrees) + "° · уверенность " + Num(confidence);
            return Accept(confidence, KvRealSurfaceKind.Table, parts);
        }

        /// <summary>
        /// УВЕРЕННОСТЬ ГЕОМЕТРИЧЕСКОГО СТОЛА (0.45…0.85). Считается по трём признакам, каждый
        /// в своих единицах, и НИКОГДА не достигает 0.95 — точная семантика платформы всегда сверху.
        /// </summary>
        private static float ScoreConfidence(float area, float aspect, float trackedSeconds,
                                             KvRealSurfaceSource source, bool largestHorizontal)
        {
            float areaQuality = Mathf.Clamp01(1f - Mathf.Abs(area - IdealTableArea) /
                                                  (MaxTableArea - MinTableArea));
            float aspectQuality = Mathf.Clamp01(1f - (aspect - 1f) / (MaxTableAspect - 1f));
            float stability = Mathf.Clamp01(trackedSeconds / StableTrackedSeconds);
            float confidence = GeometricBaseConfidence +
                               areaQuality * AreaWeight +
                               aspectQuality * AspectWeight +
                               stability * StabilityWeight;

            // Разметка точнее догадки по мешу: у меша габариты — это ограничивающий
            // параллелепипед куска комнаты, а не настоящая столешница.
            if (source == KvRealSurfaceSource.Semantic) confidence += 0.05f;

            // «Самая большая горизонтальная» — признак ПОЛА, а не стола: чуть меньше доверия.
            if (largestHorizontal) confidence -= 0.05f;

            return Mathf.Clamp(confidence, 0f, MaxGeometricConfidence);
        }

        // ---------------------------------------------------------------- гистерезис выбора

        /// <summary>
        /// ЗАМЕНЯТЬ ЛИ ТЕКУЩИЙ СТОЛ НОВЫМ КАНДИДАТОМ.
        ///
        /// Новый обязан быть лучше больше чем на 20 % — иначе на экране мерцает подсветка между
        /// двумя столами. Исключение одно: точная разметка платформы (0.95) не ждёт гистерезиса,
        /// иначе наша геометрическая догадка удержалась бы против прямого ответа PICO/ARCore.
        /// </summary>
        public static bool ShouldReplace(RealTableVerdict current, RealTableVerdict challenger)
        {
            if (!challenger.IsTable) return false;
            if (!current.IsTable) return true;
            if (challenger.Confidence >= SemanticTableConfidence) return true;
            return challenger.Confidence > current.Confidence * (1f + HysteresisMargin);
        }

        /// <summary>
        /// ВЫБОР ЛУЧШЕГО СТОЛА С ГИСТЕРЕЗИСОМ. Возвращает индекс выбранного кандидата или −1.
        /// `previousIndex` — индекс стола, выбранного на прошлом шаге (−1, если его не было).
        /// При равной уверенности выигрывает ПЕРВЫЙ — выбор детерминирован и не зависит от порядка
        /// обхода словаря в конкретном кадре.
        /// </summary>
        public static int SelectBestIndex(IList<RealTableVerdict> verdicts, int previousIndex)
        {
            if (verdicts == null || verdicts.Count == 0) return -1;

            int best = -1;
            for (int i = 0; i < verdicts.Count; i++)
            {
                if (!verdicts[i].IsTable) continue;
                if (best < 0 || verdicts[i].Confidence > verdicts[best].Confidence) best = i;
            }
            if (best < 0) return -1;

            if (previousIndex >= 0 && previousIndex < verdicts.Count && previousIndex != best &&
                verdicts[previousIndex].IsTable &&
                !ShouldReplace(verdicts[previousIndex], verdicts[best]))
            {
                return previousIndex;    // гистерезис: новый лучше, но недостаточно для переключения
            }
            return best;
        }

        // ---------------------------------------------------------------- вывод уровня пола

        /// <summary>
        /// ВЫВЕСТИ УРОВЕНЬ ПОЛА ИЗ НАБОРА ПОВЕРХНОСТЕЙ (когда платформа его не сообщила).
        ///
        /// Условие сознательно строгое: нужны ДВА уровня, и нижний обязан быть заметно ниже верхнего
        /// (0.35 м). Иначе единственная горизонтальная плоскость перед камерой — сам стол; объявить
        /// её полом значило бы навсегда запретить постановку робота на реальный стол.
        /// </summary>
        public static bool TryInferFloorHeight(IList<RealSurfaceCandidate> candidates,
                                               out float floorHeightMeters)
        {
            floorHeightMeters = 0f;
            if (candidates == null || candidates.Count == 0) return false;

            float lowest = float.MaxValue;
            bool hasHorizontal = false;
            for (int i = 0; i < candidates.Count; i++)
            {
                RealSurfaceCandidate candidate = candidates[i];
                if (!IsHorizontalAndBig(candidate)) continue;
                hasHorizontal = true;
                if (candidate.Center.y < lowest) lowest = candidate.Center.y;
            }
            if (!hasHorizontal) return false;

            for (int i = 0; i < candidates.Count; i++)
            {
                RealSurfaceCandidate candidate = candidates[i];
                if (!IsHorizontalAndBig(candidate)) continue;
                if (candidate.Center.y >= lowest + FloorInferenceMinGapMeters)
                {
                    floorHeightMeters = lowest;
                    return true;
                }
            }
            return false;       // второй уровень не найден — уровень пола неизвестен
        }

        private static bool IsHorizontalAndBig(RealSurfaceCandidate candidate)
        {
            if (!IsFinite(candidate.Center) || !IsFinite(candidate.Up)) return false;
            if (candidate.Up.sqrMagnitude < 0.000001f) return false;
            float upDot = Vector3.Dot(candidate.Up.normalized, Vector3.up);
            if (upDot < MinUpDot) return false;
            float area = Mathf.Abs(candidate.SideA) * Mathf.Abs(candidate.SideB);
            return IsFinite(area) && area >= FloorInferenceMinArea;
        }

        // ---------------------------------------------------------------- строка для экрана и журнала

        /// <summary>
        /// ЧЕСТНАЯ СТРОКА О РЕЗУЛЬТАТЕ — то, что видит оператор и тестировщик в logcat.
        ///
        /// Требование приёмки: «стол найден · площадь X м² · высота над полом Y м» / «пол (не стол) ·
        /// площадь X м²» / «стол не найден: &lt;причина с числами&gt;». Ни одного утверждения без числа:
        /// иначе по журналу нельзя доказать, что нашёлся именно стол.
        /// </summary>
        public static string Describe(RealSurfaceCandidate candidate, RealTableVerdict verdict,
                                      float floorHeightMeters, bool floorHeightKnown)
        {
            float area = Mathf.Abs(candidate.SideA) * Mathf.Abs(candidate.SideB);
            if (verdict.IsTable)
            {
                string text = "стол найден · площадь " + Num(area) + " м²";
                if (floorHeightKnown)
                    text += " · высота над полом " + Num(candidate.Center.y - floorHeightMeters) + " м";
                return text + " · уверенность " + Num(verdict.Confidence);
            }
            if (verdict.Kind == KvRealSurfaceKind.Floor)
                return "пол (не стол) · площадь " + Num(area) + " м²";
            if (verdict.Kind == KvRealSurfaceKind.Ceiling)
                return "потолок (не стол) · площадь " + Num(area) + " м²";
            if (verdict.Kind == KvRealSurfaceKind.Wall)
                return "стена (не стол) · площадь " + Num(area) + " м²";
            return "стол не найден: " + (string.IsNullOrEmpty(verdict.Reason)
                ? "причина не записана · уверенность 0.00"
                : verdict.Reason);
        }

        // ---------------------------------------------------------------- служебное

        private static RealTableVerdict Accept(float confidence, KvRealSurfaceKind kind, string reason)
        {
            RealTableVerdict verdict;
            verdict.IsTable = true;
            verdict.Confidence = Mathf.Clamp01(confidence);
            verdict.Kind = kind;
            verdict.Reason = reason;
            return verdict;
        }

        private static RealTableVerdict Reject(KvRealSurfaceKind kind, float confidence, string reason)
        {
            RealTableVerdict verdict;
            verdict.IsTable = false;
            verdict.Confidence = Mathf.Clamp01(confidence);
            verdict.Kind = kind;
            verdict.Reason = reason;
            return verdict;
        }

        /// <summary>ЧИСЛО В СТРОКЕ: всегда с точкой и всегда с цифрами (NaN печатается словом).</summary>
        private static string Num(float value)
        {
            if (float.IsNaN(value)) return "нечисло";
            if (float.IsInfinity(value)) return "бесконечность";
            return value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private static string Vec3Text(Vector3 vector)
        {
            return "(" + Num(vector.x) + "; " + Num(vector.y) + "; " + Num(vector.z) + ")";
        }

        /// <summary>ЧИСЛО ЛИ ЭТО (отсев NaN/бесконечности — данные приходят извне).</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>ЧИСЛО ЛИ ЭТО (вектор целиком).</summary>
        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }
    }
}
