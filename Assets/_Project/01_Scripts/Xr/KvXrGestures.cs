using System;
using UnityEngine;
using KazistovVvUI;

namespace KazistovVvXr
{
    /// <summary>
    /// ЖЕСТЫ КИСТЯМИ — РАСПОЗНАВАНИЕ (сессия §37, пункт 5 задания).
    ///
    /// ЧТО СОГЛАСОВАНО С ВЛАДЕЛЬЦЕМ (опрос §37, до начала работ). Владелец сказал: «в жестах я не
    /// разбираюсь; выбирай сам, как специалист», и принял такой список:
    ///   • ЩИПОК правой рукой — нажатие/выбор (то же, что курок);
    ///   • ЩИПОК левой рукой — закрыть веер (выход);
    ///   • РАСКРЫТАЯ ЛАДОНЬ — показать/скрыть панель;
    ///   • УКАЗАТЕЛЬНЫЙ ПАЛЕЦ правой руки — следующая группа.
    /// Перетаскивание удержанием щипка НЕ делается: панель и так жёстко прикреплена к левой руке
    /// (требование [S1.2]), а удержание — главный источник ложных срабатываний.
    ///
    /// ПОЧЕМУ ПОРОГИ В ДОЛЯХ ЛАДОНИ, А НЕ В САНТИМЕТРАХ. Ширина ладони у людей разная, а поза
    /// кисти приходит в метрах. Порог «щипок = 30% ширины ладони» одинаково работает и на большой,
    /// и на маленькой руке; порог «2.5 см» на крупной руке срабатывал бы при почти разведённых
    /// пальцах. Это же правило проекта: никаких размеров «на глаз» ([S3.7]).
    ///
    /// ПОЧЕМУ ЗДЕСЬ СНАЧАЛА ОТКАЗ, А ПОТОМ ЖЕСТ. Ловушка проекта [F2]: поза кисти может прийти
    /// «нулевой», но формально достоверной. Жест по такой позе — ложное срабатывание, поэтому
    /// `IsUsable` проверяется РАНЬШЕ любого распознавания, и нулевая поза не даёт ни одного жеста.
    ///
    /// ПОЧЕМУ ЛОГИКА ОТДЕЛЕНА ОТ UNITY-ОБЪЕКТОВ. Распознавание — чистая арифметика над точками.
    /// Она проверяется тестами без шлема и без кистей (`KvXrS37Tests`), и это единственный способ
    /// не проверять жесты «на глаз в шлеме».
    /// </summary>
    public enum KvGestureKind
    {
        /// <summary>Ни одного жеста (нейтральное положение кисти).</summary>
        None = 0,

        /// <summary>Щипок: большой и указательный сведены.</summary>
        Pinch = 1,

        /// <summary>Раскрытая ладонь: все пять пальцев выпрямлены.</summary>
        OpenPalm = 2,

        /// <summary>Указательный палец: выпрямлен только указательный.</summary>
        IndexPoint = 3
    }

    /// <summary>Что жест делает в интерфейсе. Один жест — одно действие, иначе будет «два действия на одно движение».</summary>
    public enum KvGestureAction
    {
        None = 0,
        /// <summary>Нажатие иконки под лучом (как курок).</summary>
        Press = 1,
        /// <summary>Показать/скрыть панель групп.</summary>
        TogglePanel = 2,
        /// <summary>Следующая группа.</summary>
        NextGroup = 3,
        /// <summary>Закрыть веер.</summary>
        CloseFan = 4
    }

    /// <summary>Сработавший жест: что, какой рукой и что делать.</summary>
    public struct KvGestureEvent
    {
        public KvGestureKind kind;
        public KvXrQuickHand hand;
        public KvGestureAction action;

        /// <summary>Название жеста для журнала («щипок», «раскрытая ладонь», «указательный палец»).</summary>
        public string Name { get { return KvGestureRecognizer.GestureName(kind); } }

        /// <summary>Сторона словами для журнала.</summary>
        public string HandName { get { return hand == KvXrQuickHand.Left ? "левая рука" : "правая рука"; } }
    }

    /// <summary>
    /// ПОЛОЖЕНИЕ КИСТИ, КАК ЕГО ВИДИТ РАСПОЗНАВАНИЕ: точки суставов и — главное — СГИБЫ ПАЛЬЦЕВ.
    ///
    /// ПОЧЕМУ ПОЯВИЛИСЬ СГИБЫ (исправление §37 по живому шлему). Первая версия жестов считала всё
    /// САМА: «расстояние от кончика до ладони», «расстояние большой–указательный». На живом шлеме
    /// 21.10.2026 это дало 63 срабатывания за сеанс, из них 51 — «раскрытая ладонь» на любое
    /// положение руки, 13 — «щипок» на СЖАТОМ КУЛАКЕ (владелец: «работает закрытая и потом
    /// перевёрнутая рука»), и НИ ОДНОГО «указательного пальца». Мои пороги были слишком грубыми.
    ///
    /// Теперь сгибы и сила щипка считает МАТЕМАТИКА САМОГО ПАКЕТА XR HANDS
    /// (`XRFingerShapeMath.CalculateFingerShape`: full curl и pinch по углам суставов, с
    /// настроенными порогами). Здесь только решающие правила над этими числами — их и проверяют
    /// тесты без шлема.
    /// </summary>
    public struct KvHandFrame
    {
        /// <summary>Кисть отслеживается и поза непустая.</summary>
        public bool valid;

        public Vector3 wrist;
        public Vector3 palm;

        public Vector3 thumbTip;

        public Vector3 indexProximal;
        public Vector3 indexTip;

        public Vector3 middleProximal;
        public Vector3 middleTip;

        public Vector3 ringProximal;
        public Vector3 ringTip;

        public Vector3 littleProximal;
        public Vector3 littleTip;

        public Quaternion palmRotation;

        // ---------------------------------------------------------------- сгибы (XR Hands)

        /// <summary>Сгибы пальцев пришли от XR Hands (0 — прямой палец, 1 — полностью согнут).</summary>
        public bool hasShapes;

        public float thumbCurl;
        public float indexCurl;
        public float middleCurl;
        public float ringCurl;
        public float littleCurl;

        /// <summary>Сила щипка «большой ↔ палец»: 1 — кончики соприкасаются.</summary>
        public float indexPinch;
        public float middlePinch;
        public float ringPinch;
        public float littlePinch;

        /// <summary>Разведение пальцев: 0 — вместе, 1 — максимально разведены.</summary>
        public float indexSpread;
        public float middleSpread;
        public float ringSpread;
    }

    /// <summary>
    /// РЕШАЮЩИЕ ПРАВИЛА ЖЕСТОВ — чистые функции над числами сгибов. Проверяются тестами без шлема.
    ///
    /// ПОЧЕМУ ПРАВИЛА СТРОГИЕ. Ложное срабатывание жеста в шлеме — это переключённая панель или
    /// нажатая команда, то есть вред. Поэтому каждый жест требует НАБОРА условий, а не одного:
    ///   • щипок — сила щипка высокая, но указательный НЕ сжат в кулак (это и отличает щипок от
    ///     кулака, на котором первая версия срабатывала);
    ///   • раскрытая ладонь — все четыре пальца прямые И пальцы разведены (расслабленная кисть
    ///     больше не считается жестом);
    ///   • указательный палец — указательный прямой, три остальных согнуты.
    /// </summary>
    public static class KvGestureMath
    {
        // ==================================================== ПОРОГИ, ПЕРЕКАЛИБРОВАННЫЕ ПО ЧИСЛАМ (§38)
        //
        // ОТКУДА ЧИСЛА. Живой прогон 23.10.2026 (`Logs\device-s38b.log`, 41 образец сгибов правой
        // кисти владельца): указательный 0,11–0,76 · средний 0,10–0,85 · безымянный 0,06–0,82 ·
        // мизинец 0,00–0,81 · щипок 0,00–1,00 · разведение 0,00–0,29. Первые пороги §38
        // («раскрытая ладонь — сгибы ≤ 0,15») оказались строже самой руки владельца: у него
        // раскрытая ладонь даёт 0,15–0,30, поэтому жесты «пропали все». Правка согласована с
        // владельцем 23.10.2026.

        /// <summary>Щипок: сила щипка «большой ↔ указательный» не ниже этого.</summary>
        public const float PinchTouching = 0.80f;

        /// <summary>Щипок: указательный согнут не слабее этого (иначе пальцы просто рядом).</summary>
        public const float PinchMinIndexCurl = 0.30f;

        /// <summary>Щипок: указательный согнут не сильнее этого (иначе это кулак, а не щипок).</summary>
        public const float PinchMaxIndexCurl = 0.85f;

        /// <summary>Кулак: все четыре пальца согнуты не слабее этого.</summary>
        public const float FistCurl = 0.85f;

        /// <summary>Раскрытая ладонь: все четыре пальца прямые не сильнее этого.</summary>
        public const float OpenCurl = 0.30f;

        /// <summary>Раскрытая ладонь: среднее разведение пальцев не меньше этого.</summary>
        public const float OpenSpread = 0.05f;

        /// <summary>Раскрытая ладонь: большой палец выпрямлен не слабее этого (иначе это хват).</summary>
        public const float OpenThumbCurl = 0.55f;

        /// <summary>Раскрытая ладонь: сила щипка не выше этого (ладонь не может быть щипком).</summary>
        public const float OpenMaxPinch = 0.40f;

        /// <summary>Указательный палец: сам указательный прямой не сильнее этого.</summary>
        public const float PointIndexCurl = 0.20f;

        /// <summary>Указательный палец: остальные три согнуты не слабее этого.</summary>
        public const float PointOtherCurl = 0.70f;

        /// <summary>Указательный палец: сила щипка не выше этого (иначе это щипок, а не указание).</summary>
        public const float PointMaxPinch = 0.40f;

        /// <summary>Ниже этой ширины ладони поза считается пустой (метры).</summary>
        public const float MinPalmScaleMeters = 0.010f;

        // ==================================================== МЕСТО РУКИ (§38)

        /// <summary>Рука должна быть впереди не меньше этого, метры.</summary>
        public const float MinForwardMeters = 0.10f;

        /// <summary>Рука должна быть не дальше этого от головы, метры.</summary>
        public const float MaxDistanceFromHeadMeters = 1.00f;

        /// <summary>Насколько «вверх» должны смотреть пальцы, чтобы это была поднятая ладонь.</summary>
        public const float PalmUpMinVertical = 0.50f;

        /// <summary>
        /// ШИРИНА ЛАДОНИ — признак того, что поза вообще есть. Берётся между основаниями
        /// указательного и мизинца; если этой пары нет, откатываемся на «ладонь–запястье».
        /// </summary>
        public static float PalmScale(KvHandFrame frame)
        {
            float width = Vector3.Distance(frame.indexProximal, frame.littleProximal);
            if (width >= MinPalmScaleMeters) return width;

            float fallback = Vector3.Distance(frame.palm, frame.wrist);
            return fallback >= MinPalmScaleMeters ? fallback : 0f;
        }

        /// <summary>
        /// ГОДИТСЯ ЛИ ПОЗА. Три условия: кисть отслеживается, ладонь имеет размер, все точки —
        /// конечные числа. Нулевая поза (ловушка [F2]) отвергается здесь.
        /// </summary>
        public static bool IsUsable(KvHandFrame frame)
        {
            if (!frame.valid) return false;
            if (PalmScale(frame) <= 0f) return false;

            if (!IsFinite(frame.wrist) || !IsFinite(frame.palm) || !IsFinite(frame.thumbTip) ||
                !IsFinite(frame.indexTip) || !IsFinite(frame.middleTip) || !IsFinite(frame.ringTip) ||
                !IsFinite(frame.littleTip) || !IsFinite(frame.indexProximal) || !IsFinite(frame.littleProximal))
                return false;
            return true;
        }

        /// <summary>Пришли ли сгибы от XR Hands (без них жесты молчат: гадать запрещено).</summary>
        public static bool HasShapes(KvHandFrame frame)
        {
            return frame.valid && frame.hasShapes;
        }

        /// <summary>Среднее разведение пальцев (мизинец в расчёте не участвует — у него нет пары).</summary>
        public static float AverageSpread(KvHandFrame frame)
        {
            return (frame.indexSpread + frame.middleSpread + frame.ringSpread) / 3f;
        }

        /// <summary>
        /// РУКА В ПОЛЕ ЗРЕНИЯ (§38): впереди по взгляду не меньше 0,15 м и не дальше 0,9 м от головы.
        /// Жест опущенной вниз или отведённой за спину рукой больше не существует — именно такие
        /// «жесты» давали ложные переключения панели.
        /// </summary>
        public static bool IsHandInView(Vector3 handPosition, Vector3 headPosition, Quaternion headRotation)
        {
            // ОТСЕВ НЕЧИСЕЛ ДОБАВЛЕН 30.10.2026 (S44) — тот же класс, что найден в
            // KvXrControllerVisuals. Без него дефект был бы здесь ТИШЕ и потому опаснее:
            //   • `offset.magnitude > MaxDistance` для NaN даёт false — проверка «далеко» НЕ срабатывает;
            //   • `Vector3.Dot` для NaN даёт NaN, а `NaN >= MinForward` — тоже false.
            // Итог: жест МОЛЧА игнорируется. Оператор делает движение, панель не реагирует, и понять
            // почему невозможно — ни ошибки, ни строки в журнале.
            //
            // ПОЧЕМУ ОТКАЗ, А НЕ «ПРОПУСТИТЬ»: если поза нечисловая, доверять ей нельзя вообще.
            // Реакция на жест, вычисленный по мусорным координатам, может нажать что угодно.
            if (IsNotFinite(handPosition) || IsNotFinite(headPosition)) return false;

            Vector3 offset = handPosition - headPosition;
            if (IsNotFinite(offset)) return false;              // вычитание нечисла даёт нечисло
            if (offset.magnitude > MaxDistanceFromHeadMeters) return false;

            Vector3 forward = headRotation * Vector3.forward;
            if (IsNotFinite(forward)) return false;             // повреждённый поворот головы
            float dot = Vector3.Dot(offset, forward);
            if (float.IsNaN(dot)) return false;

            return dot >= MinForwardMeters;
        }

        /// <summary>Есть ли в векторе NaN или бесконечность (один вызов вместо шести проверок).</summary>
        private static bool IsNotFinite(Vector3 v)
        {
            return float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                   float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z);
        }

        /// <summary>
        /// ПОЗА «ЛАДОНЬ ПОДНЯТА» (§38): пальцы смотрят ВВЕРХ, как в жесте «стоп». Опущенная рука
        /// этому не удовлетворяет, поэтому «раскрытая ладонь» перестаёт срабатывать сама собой.
        /// </summary>
        public static bool IsPalmUpPosture(KvHandFrame frame)
        {
            Vector3 direction = frame.indexTip - frame.wrist;
            if (direction.sqrMagnitude < 1e-6f) return false;
            return direction.normalized.y >= PalmUpMinVertical;
        }

        /// <summary>ЩИПОК: кончики большого и указательного соприкасаются, но рука не сжата в кулак.</summary>
        public static bool IsPinch(KvHandFrame frame)
        {
            if (!HasShapes(frame)) return false;
            if (frame.indexPinch < PinchTouching) return false;
            if (frame.indexCurl < PinchMinIndexCurl) return false;
            if (frame.indexCurl > PinchMaxIndexCurl) return false;
            return true;
        }

        /// <summary>КУЛАК: все четыре пальца согнуты. Кулак не должен давать ни одного жеста.</summary>
        public static bool IsFist(KvHandFrame frame)
        {
            if (!HasShapes(frame)) return false;
            return frame.indexCurl >= FistCurl && frame.middleCurl >= FistCurl &&
                   frame.ringCurl >= FistCurl && frame.littleCurl >= FistCurl;
        }

        /// <summary>
        /// РАСКРЫТАЯ ЛАДОНЬ: все четыре пальца прямые, разведены, большой выпрямлен, и это не щипок.
        /// Пороги §38: прямые ≤ 0,15, разведение ≥ 0,10, большой ≤ 0,45, щипок ≤ 0,30.
        /// </summary>
        public static bool IsOpenPalm(KvHandFrame frame)
        {
            if (!HasShapes(frame)) return false;
            if (IsPinch(frame) || IsFist(frame)) return false;

            if (frame.indexCurl > OpenCurl) return false;
            if (frame.middleCurl > OpenCurl) return false;
            if (frame.ringCurl > OpenCurl) return false;
            if (frame.littleCurl > OpenCurl) return false;
            if (frame.thumbCurl > OpenThumbCurl) return false;
            if (frame.indexPinch > OpenMaxPinch) return false;

            return AverageSpread(frame) >= OpenSpread;
        }

        /// <summary>УКАЗАТЕЛЬНЫЙ ПАЛЕЦ: указательный прямой, три остальных согнуты сильнее 0,75.</summary>
        public static bool IsIndexPoint(KvHandFrame frame)
        {
            if (!HasShapes(frame)) return false;
            if (IsPinch(frame)) return false;

            if (frame.indexCurl > PointIndexCurl) return false;
            if (frame.indexPinch > PointMaxPinch) return false;
            if (frame.middleCurl < PointOtherCurl) return false;
            if (frame.ringCurl < PointOtherCurl) return false;
            if (frame.littleCurl < PointOtherCurl) return false;
            return true;
        }

        private static bool IsFinite(Vector3 value)
        {
            return !float.IsNaN(value.x) && !float.IsInfinity(value.x)
                && !float.IsNaN(value.y) && !float.IsInfinity(value.y)
                && !float.IsNaN(value.z) && !float.IsInfinity(value.z);
        }
    }

    /// <summary>
    /// «ЖЕСТ БЕЗ ДЕЛА НЕ СЧИТАЕТСЯ» (§38). Живой прогон 21.10.2026 показал: 14 «щипков» без цели
    /// под лучом и 12 «выходов из веера» при уже закрытом веере — событий, которым нечего было
    /// делать. Они попадали в журнал и сжигали паузу, из-за чего следующий, НАСТОЯЩИЙ жест
    /// срабатывал не сразу. Правило простое: нет цели — нет жеста.
    /// </summary>
    public static class KvGestureActions
    {
        /// <summary>
        /// Есть ли чем заняться этому действию прямо сейчас.
        /// </summary>
        /// <param name="action">Действие жеста.</param>
        /// <param name="hasTargetIcon">Луч навёл иконку панели.</param>
        /// <param name="fanOpen">Веер раскрыт.</param>
        /// <param name="panelVisible">Панель сейчас видна.</param>
        public static bool IsUseful(KvGestureAction action, bool hasTargetIcon, bool fanOpen, bool panelVisible)
        {
            switch (action)
            {
                case KvGestureAction.Press: return hasTargetIcon;
                case KvGestureAction.CloseFan: return fanOpen;
                case KvGestureAction.NextGroup: return panelVisible;
                case KvGestureAction.TogglePanel: return true;
                default: return false;
            }
        }
    }

    /// <summary>
    /// РАСПОЗНАВАТЕЛЬ ЖЕСТОВ: удержание, пауза, требование нейтрального положения.
    ///
    /// ЗАЧЕМ ТРИ ЗАЩИТЫ, А НЕ ОДНА:
    ///   1) УДЕРЖАНИЕ (0.15 с щипок, 0.40 с ладонь и палец) — короткое случайное движение пальцами
    ///      не срабатывает;
    ///   2) ПАУЗА 0.8 с между срабатываниями — удержанный щипок не превращается в «пулемёт» нажатий;
    ///   3) НЕЙТРАЛЬНОЕ ПОЛОЖЕНИЕ: после срабатывания рука обязана хоть на мгновение вернуться в
    ///      «ни одного жеста». Без этого одно и то же положение срабатывало бы повторно само.
    /// </summary>
    public class KvGestureRecognizer
    {
        // ПОРОГИ УДЕРЖАНИЯ (§37–§38). Щипок и общая пауза остались как в §37 — они закреплены уже
        // запускавшимися тестами (`Gesture_RequiresHold`, `Gesture_DoesNotRepeatWhileHeld`), и менять
        // их без отдельного разрешения владельца нельзя. Ладонь и указательный палец удлинены в §38:
        // «раскрытая ладонь» срабатывала от случайного движения 20 раз за сеанс.
        public const float PinchHoldSeconds = 0.20f;
        public const float PalmHoldSeconds = 0.80f;
        public const float PointHoldSeconds = 0.60f;
        public const float CooldownSeconds = 1.00f;
        public const float NeutralSeconds = 0.15f;

        private class HandState
        {
            public KvGestureKind holdKind = KvGestureKind.None;
            public float holdTime;
            public KvGestureKind lastFired = KvGestureKind.None;
            public float cooldown;
            public float neutralTime;
        }

        private readonly HandState rightState = new HandState();
        private readonly HandState leftState = new HandState();

        /// <summary>Сколько держать жест, чтобы он сработал.</summary>
        public static float HoldSecondsFor(KvGestureKind kind)
        {
            switch (kind)
            {
                case KvGestureKind.Pinch: return PinchHoldSeconds;
                case KvGestureKind.OpenPalm: return PalmHoldSeconds;
                case KvGestureKind.IndexPoint: return PointHoldSeconds;
                default: return float.MaxValue;
            }
        }

        /// <summary>
        /// ЧТО ЖЕСТ ДЕЛАЕТ. Таблица согласована с владельцем; левая рука перелистывать группы не
        /// может — указательный палец левой руки не делает ничего.
        /// </summary>
        public static KvGestureAction ActionFor(KvGestureKind kind, KvXrQuickHand hand)
        {
            switch (kind)
            {
                case KvGestureKind.Pinch:
                    return hand == KvXrQuickHand.Left ? KvGestureAction.CloseFan : KvGestureAction.Press;
                case KvGestureKind.OpenPalm:
                    return KvGestureAction.TogglePanel;
                case KvGestureKind.IndexPoint:
                    return hand == KvXrQuickHand.Right ? KvGestureAction.NextGroup : KvGestureAction.None;
                default:
                    return KvGestureAction.None;
            }
        }

        /// <summary>Название жеста для строки журнала.</summary>
        public static string GestureName(KvGestureKind kind)
        {
            switch (kind)
            {
                case KvGestureKind.Pinch: return "щипок";
                case KvGestureKind.OpenPalm: return "раскрытая ладонь";
                case KvGestureKind.IndexPoint: return "указательный палец";
                default: return "нет";
            }
        }

        /// <summary>
        /// КАКОЙ ЖЕСТ СЕЙЧАС ПОКАЗЫВАЕТ КИСТЬ. Порядок важен: щипок проверяется первым, потому что
        /// в начале щипка пальцы ещё выпрямлены и «указательный палец» сработал бы раньше щипка.
        /// Без сгибов от XR Hands жестов НЕТ ВООБЩЕ: гадать по расстояниям между точками — это и
        /// есть та ошибка, которая дала 63 ложных срабатывания 21.10.2026.
        /// </summary>
        public static KvGestureKind Detect(KvHandFrame frame)
        {
            if (!KvGestureMath.IsUsable(frame)) return KvGestureKind.None;
            if (!KvGestureMath.HasShapes(frame)) return KvGestureKind.None;
            if (KvGestureMath.IsPinch(frame)) return KvGestureKind.Pinch;
            if (KvGestureMath.IsFist(frame)) return KvGestureKind.None;      // кулак — не жест
            if (KvGestureMath.IsOpenPalm(frame)) return KvGestureKind.OpenPalm;
            if (KvGestureMath.IsIndexPoint(frame)) return KvGestureKind.IndexPoint;
            return KvGestureKind.None;
        }

        /// <summary>
        /// ТО ЖЕ, НО С УЧЁТОМ МЕСТА РУКИ (§38). Жест считается только если рука в поле зрения
        /// (впереди не меньше 0,15 м и не дальше 0,9 м от головы), а «раскрытая ладонь» — ещё и
        /// только когда пальцы смотрят ВВЕРХ (жест «стоп»). Именно это убирает ложные переключения
        /// панели от опущенных и расслабленных рук.
        /// </summary>
        public static KvGestureKind Detect(KvHandFrame frame, Vector3 headPosition, Quaternion headRotation)
        {
            if (!KvGestureMath.IsHandInView(frame.palm, headPosition, headRotation)) return KvGestureKind.None;

            KvGestureKind kind = Detect(frame);
            if (kind == KvGestureKind.OpenPalm && !KvGestureMath.IsPalmUpPosture(frame)) return KvGestureKind.None;
            return kind;
        }

        /// <summary>
        /// ОДИН ШАГ. Возвращает true, если жест СРАБОТАЛ в этот кадр (не чаще одного за кадр).
        /// Правая рука проверяется первой: она основная ([S1.3]).
        /// </summary>
        public bool TryTick(KvHandFrame right, KvHandFrame left, float deltaTime, out KvGestureEvent result)
        {
            return TryTickCore(right, left, Vector3.zero, Quaternion.identity, deltaTime, false, out result);
        }

        /// <summary>
        /// ОДИН ШАГ С УЧЁТОМ МЕСТА РУКИ (§38) — так зовёт исполнитель жестов в шлеме.
        /// Голова нужна, чтобы жест опущенной или отведённой рукой не считался.
        /// </summary>
        public bool TryTick(KvHandFrame right, KvHandFrame left,
            Vector3 headPosition, Quaternion headRotation,
            float deltaTime, out KvGestureEvent result)
        {
            return TryTickCore(right, left, headPosition, headRotation, deltaTime, true, out result);
        }

        private bool TryTickCore(KvHandFrame right, KvHandFrame left,
            Vector3 headPosition, Quaternion headRotation,
            float deltaTime, bool checkView, out KvGestureEvent result)
        {
            result = default(KvGestureEvent);
            if (deltaTime < 0f) deltaTime = 0f;
            if (deltaTime > 1f) deltaTime = 1f;         // защита от скачка времени после паузы

            if (TryAdvance(rightState, KvXrQuickHand.Right, right,
                    headPosition, headRotation, deltaTime, checkView, out result)) return true;
            if (TryAdvance(leftState, KvXrQuickHand.Left, left,
                    headPosition, headRotation, deltaTime, checkView, out result)) return true;
            return false;
        }

        /// <summary>Сбросить всё: после включения жестов ничего «не догоняет» задним числом.</summary>
        public void Reset()
        {
            rightState.holdKind = KvGestureKind.None;
            rightState.holdTime = 0f;
            rightState.lastFired = KvGestureKind.None;
            rightState.cooldown = 0f;
            rightState.neutralTime = 0f;

            leftState.holdKind = KvGestureKind.None;
            leftState.holdTime = 0f;
            leftState.lastFired = KvGestureKind.None;
            leftState.cooldown = 0f;
            leftState.neutralTime = 0f;
        }

        private static bool TryAdvance(HandState state, KvXrQuickHand hand, KvHandFrame frame,
            Vector3 headPosition, Quaternion headRotation, float deltaTime, bool checkView,
            out KvGestureEvent result)
        {
            result = default(KvGestureEvent);
            if (state.cooldown > 0f) state.cooldown = Mathf.Max(0f, state.cooldown - deltaTime);

            KvGestureKind kind = checkView
                ? Detect(frame, headPosition, headRotation)
                : Detect(frame);

            if (kind != state.holdKind)
            {
                state.holdKind = kind;
                state.holdTime = 0f;
            }

            if (kind == KvGestureKind.None)
            {
                state.neutralTime += deltaTime;
                return false;
            }

            state.holdTime += deltaTime;
            if (state.holdTime < HoldSecondsFor(kind)) return false;
            if (state.lastFired == kind && state.cooldown > 0f) return false;
            if (state.lastFired != KvGestureKind.None && state.neutralTime < NeutralSeconds) return false;

            state.lastFired = kind;
            state.cooldown = CooldownSeconds;
            state.neutralTime = 0f;
            state.holdTime = 0f;

            result = new KvGestureEvent
            {
                kind = kind,
                hand = hand,
                action = ActionFor(kind, hand)
            };
            return true;
        }
    }

    /// <summary>
    /// ОТКУДА БЕРУТСЯ КИСТИ. Отдельная точка, чтобы распознавание не знало про пакет XR Hands:
    /// поставщик (`KvXrHands`) подписывается здесь, а жесты спрашивают «дай позу этой руки».
    /// Без поставщика жесты просто молчат — и это честное «кистей нет», а не выдуманные данные.
    /// </summary>
    public static class KvXrHandSource
    {
        /// <summary>Поставщик позы кисти. null — кисти недоступны.</summary>
        public static Func<KvXrQuickHand, KvHandFrame?> Provider;

        /// <summary>Поза кисти, если она есть и достоверна.</summary>
        public static bool TryGet(KvXrQuickHand hand, out KvHandFrame frame)
        {
            frame = default(KvHandFrame);
            Func<KvXrQuickHand, KvHandFrame?> provider = Provider;
            if (provider == null) return false;

            KvHandFrame? result;
            try { result = provider(hand); }
            catch (Exception) { return false; }

            if (!result.HasValue) return false;
            frame = result.Value;
            return KvGestureMath.IsUsable(frame);
        }
    }

    /// <summary>
    /// ЖЕСТЫ В ИГРЕ: один шаг распознавания за кадр и выполнение действия.
    ///
    /// КАЖДОЕ СРАБАТЫВАНИЕ — СТРОКА В ЖУРНАЛЕ ([S2.7]): «[KvXr] ЖЕСТ: «…» сработал». Без строки
    /// нельзя ни доказать жест, ни понять, почему сработало не то.
    ///
    /// КОМПОНЕНТ СОЗДАЁТСЯ В РАНТАЙМЕ из `KvXrRig` — сцена `XrMain.unity` не меняется.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrGestures : MonoBehaviour
    {
        /// <summary>Сообщать о срабатываниях в журнал.</summary>
        public bool logOnFire = true;

        private readonly KvGestureRecognizer recognizer = new KvGestureRecognizer();
        private KvXrUiPointer pointer;
        private KvXrGroupPanel panel;
        private int firedCount;

        /// <summary>Сколько жестов сработало — для журнала и отчёта.</summary>
        public int FiredCount { get { return firedCount; } }

        /// <summary>Найти или создать исполнитель жестов (идемпотентно).</summary>
        public static KvXrGestures Ensure(Transform parent)
        {
            KvXrGestures existing = UnityEngine.Object.FindAnyObjectByType<KvXrGestures>();
            if (existing != null) return existing;

            var go = new GameObject("XrGestures");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvXrGestures>();
        }

        void Update()
        {
            if (!KvXrTuning.GesturesEnabled)
            {
                recognizer.Reset();
                return;
            }

            KvHandFrame right;
            KvHandFrame left;
            bool hasRight = KvXrHandSource.TryGet(KvXrQuickHand.Right, out right);
            bool hasLeft = KvXrHandSource.TryGet(KvXrQuickHand.Left, out left);

            // КОНТРОЛЛЕР В РУКЕ — ЖЕСТЫ ЭТОЙ РУКИ МОЛЧАТ (§37). Рантайм PICO умеет отдавать кисти
            // одновременно с контроллерами, и тогда «щипок» срабатывал бы от пальцев, держащих
            // контроллер, — то есть от любого сжатия. Пока в руке контроллер, ввод идёт курком.
            // ЖЕСТЫ РУКИ С КОНТРОЛЛЕРОМ: по умолчанию НЕ глушатся (§37, третья сборка). Владелец,
            // держа контроллеры, не мог показать ни один жест. Глушение возвращается ключом
            // `gesture.muteWithController=1`, если появятся ложные срабатывания.
            bool mute = KvXrTuning.MuteGesturesWithController;
            bool rightHeld = KvXrInputSource.TryTrackedPose(KvXrQuickHand.Right, out _, out _);
            bool leftHeld = KvXrInputSource.TryTrackedPose(KvXrQuickHand.Left, out _, out _);

            if (mute)
            {
                if (hasRight && rightHeld) hasRight = false;
                if (hasLeft && leftHeld) hasLeft = false;
                LogIfMutedByControllers(rightHeld, leftHeld);
            }

            // ПОКА НАЖАТ КУРОК РУКИ ЛУЧА — ЖЕСТ НЕ СРАБАТЫВАЕТ: иначе одно движение делало бы два
            // действия (нажатие курком и нажатие щипком) — та же болезнь, что в §27 лечили на десктопе.
            if (KvXrInputSource.RayTrigger >= 0.5f && KvXrInputSource.RayHand == KvXrQuickHand.Right) hasRight = false;
            else if (KvXrInputSource.RayTrigger >= 0.5f) hasLeft = false;

            if (!hasRight) right = default(KvHandFrame);
            if (!hasLeft) left = default(KvHandFrame);

            lastRightFrame = right;
            lastLeftFrame = left;

            KvGestureEvent evt;

            // §38: жест считается только с учётом МЕСТА РУКИ (опущенная рука не в счёт).
            bool fired;
            if (KvXrInputSource.TryHeadPose(out Vector3 headPosition, out Quaternion headRotation))
                fired = recognizer.TryTick(right, left, headPosition, headRotation, Time.deltaTime, out evt);
            else
                fired = recognizer.TryTick(right, left, Time.deltaTime, out evt);

            if (!fired) return;
            if (evt.action == KvGestureAction.None) return;

            // §38: «ЖЕСТ БЕЗ ДЕЛА НЕ СЧИТАЕТСЯ». Нажатие без цели под лучом, выход из веера при
            // закрытом веере, «следующая группа» при скрытой панели — не события.
            EnsureTargets();
            bool hasTarget = pointer != null && pointer.HoveredIndex >= 0;
            bool fanOpen = panel != null && panel.Level == KvXrPanelLevel.GroupFan;
            bool panelVisible = KvXrSettings.GetQuickPanelVisible();
            if (!KvGestureActions.IsUseful(evt.action, hasTarget, fanOpen, panelVisible))
            {
                LogRejected(evt, hasTarget, fanOpen, panelVisible);
                return;
            }

            firedCount++;
            Perform(evt);
        }

        private float nextRejectedLog;
        private string lastRejected;

        /// <summary>
        /// ПОЧЕМУ ЖЕСТ НЕ ЗАСЧИТАН (§38). Без этой строки «жест не работает» невозможно отличить:
        /// не распознан, не хватило удержания, нет цели под лучом или отвергнут проверкой места.
        /// Печатается не чаще раза в 3 секунды и только при смене причины.
        /// </summary>
        private void LogRejected(KvGestureEvent evt, bool hasTarget, bool fanOpen, bool panelVisible)
        {
            if (!logOnFire) return;

            string reason = evt.action == KvGestureAction.Press && !hasTarget
                ? "луч ни на одну иконку не наведён"
                : evt.action == KvGestureAction.CloseFan && !fanOpen
                    ? "веер и так закрыт"
                    : evt.action == KvGestureAction.NextGroup && !panelVisible
                        ? "панель скрыта"
                        : "нет подходящего действия";

            if (reason == lastRejected && Time.realtimeSinceStartup < nextRejectedLog) return;
            lastRejected = reason;
            nextRejectedLog = Time.realtimeSinceStartup + 3f;

            Debug.Log("[KvXr] ЖЕСТ не засчитан: «" + evt.Name + "» · " + evt.HandName + " · " + reason +
                      " · " + DescribeShapes(evt.hand));
        }

        /// <summary>Сбросить всё: после включения жестов или пробуждения шлема ничего «не догоняет».</summary>
        public void ResetState()
        {
            recognizer.Reset();
            pointer = null;
            panel = null;
            nextMutedLog = 0f;
            lastMutedState = null;
        }

        private float nextMutedLog;
        private string lastMutedState;

        /// <summary>
        /// ЧЕСТНАЯ ПРИЧИНА МОЛЧАНИЯ ЖЕСТОВ: в руке контроллер. Печатается не чаще раза в 10 секунд
        /// и только при смене состояния, чтобы не забивать журнал.
        /// </summary>
        private void LogIfMutedByControllers(bool rightHeld, bool leftHeld)
        {
            if (!rightHeld && !leftHeld) return;
            if (!logOnFire) return;

            string state = rightHeld && leftHeld ? "обе руки" : (rightHeld ? "правая рука" : "левая рука");
            if (state == lastMutedState && Time.realtimeSinceStartup < nextMutedLog) return;

            lastMutedState = state;
            nextMutedLog = Time.realtimeSinceStartup + 10f;

            Debug.Log("[KvXr] ЖЕСТЫ: " + state + " занята контроллером — жесты этой руки молчат " +
                      "(с контроллером в руке сила щипка доходит до 0.86, любой хват выглядел бы щипком) · " +
                      "положите контроллер, чтобы работали жесты");
        }

        /// <summary>ВЫПОЛНИТЬ ДЕЙСТВИЕ ЖЕСТА — и назвать его в журнале тем, что оно сделало.</summary>
        private void Perform(KvGestureEvent evt)
        {
            EnsureTargets();
            string done;

            switch (evt.action)
            {
                case KvGestureAction.Press:
                    done = PressHovered();
                    break;
                case KvGestureAction.CloseFan:
                    done = CloseFan();
                    break;
                case KvGestureAction.TogglePanel:
                    done = TogglePanel();
                    break;
                case KvGestureAction.NextGroup:
                    done = NextGroup();
                    break;
                default:
                    done = "действие не назначено";
                    break;
            }

            if (!logOnFire) return;
            Debug.Log("[KvXr] ЖЕСТ: «" + evt.Name + "» сработал · " + evt.HandName + " · " + done +
                      " · всего жестов " + firedCount + " · " + DescribeShapes(evt.hand));
        }

        private KvHandFrame lastRightFrame;
        private KvHandFrame lastLeftFrame;

        /// <summary>
        /// ЧИСЛА, ПО КОТОРЫМ СРАБОТАЛ ЖЕСТ (21.10.2026). Без них подбор порогов — гадание: в журнале
        /// видно, что жест сработал, но не видно, на каких значениях сгибов/щипка это произошло.
        /// Теперь каждое срабатывание несёт свои числа, и пороги правятся по фактам.
        /// </summary>
        private string DescribeShapes(KvXrQuickHand hand)
        {
            KvHandFrame frame = hand == KvXrQuickHand.Left ? lastLeftFrame : lastRightFrame;
            if (!frame.hasShapes) return "сгибы недоступны";

            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "сгибы {0:0.00}/{1:0.00}/{2:0.00}/{3:0.00} · щипок {4:0.00} · разведение {5:0.00}",
                frame.indexCurl, frame.middleCurl, frame.ringCurl, frame.littleCurl,
                frame.indexPinch, KvGestureMath.AverageSpread(frame));
        }

        private void EnsureTargets()
        {
            if (pointer == null) pointer = UnityEngine.Object.FindAnyObjectByType<KvXrUiPointer>();
            if (panel == null) panel = UnityEngine.Object.FindAnyObjectByType<KvXrGroupPanel>();
        }

        /// <summary>Щипок = нажатие иконки ПОД ЛУЧОМ (а не «первой попавшейся»).</summary>
        private string PressHovered()
        {
            if (pointer == null || panel == null) return "панель не найдена — нажимать нечего";

            int index = pointer.HoveredIndex;
            if (index < 0) return "луч ни на одну иконку не наведён — нажатия нет";

            string title = panel.TitleAt(index);
            panel.PressIndex(index);
            return "нажата иконка «" + title + "»";
        }

        private string CloseFan()
        {
            if (panel == null) return "панель не найдена";
            if (panel.Level != KvXrPanelLevel.GroupFan) return "веер и так закрыт — снова иконки групп";

            panel.CloseFan();
            return "веер закрыт";
        }

        private string TogglePanel()
        {
            bool wasVisible = KvXrSettings.GetQuickPanelVisible();
            KvXrSettings.SetQuickPanelVisible(!wasVisible);
            KvXrSettingsChanged.Notify();
            return wasVisible ? "панель скрыта" : "панель показана";
        }

        private string NextGroup()
        {
            if (panel == null) return "панель не найдена";

            int total = KvToolbarGroups.All.Length;
            if (total <= 0) return "групп в каталоге нет";

            int next = panel.OpenGroupIndex + 1;
            if (next >= total)
            {
                panel.CloseFan();
                return "последняя группа пройдена — снова иконки групп";
            }

            string title = panel.TitleAt(next);
            panel.OpenGroup(next);
            return "раскрыта группа «" + title + "» (" + (next + 1) + " из " + total + ")";
        }
    }
}
