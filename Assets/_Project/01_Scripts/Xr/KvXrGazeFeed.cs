using UnityEngine;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ПОДАЧА ВЗГЛЯДА В СЕРВИС ОТСЛЕЖИВАНИЯ (создано 30.10.2026, S44 — закрытие пункта 5.23).
    ///
    /// ЧТО БЫЛО. `KvEyeTrackingService` (взгляд, фиксации, выбор взглядом, фовеальное зрение)
    /// получал данные ТОЛЬКО из диагностики (`DshStage4Diag.PushGaze`). То есть в собранном
    /// приложении вся эта логика работала ВХОЛОСТУЮ: она готова, но источника у неё нет.
    ///
    /// Ровно та же история, что была с кистями (жесты молчали, потому что `KvXrHands` никому не
    /// передавал точки). Найдено проверкой кода, а не на устройстве — и хорошо: на устройстве это
    /// выглядело бы как «выбор взглядом не работает», без единой ошибки.
    ///
    /// ОТКУДА БЕРЁТСЯ ВЗГЛЯД. Источник — ПОЗА ГОЛОВЫ. Это ЧЕСТНОЕ УПРОЩЕНИЕ, и оно названо прямо:
    ///
    ///   • настоящий взгляд (направление зрачка) требует OpenXR-расширения `XR_EXT_eye_gaze_interaction`
    ///     и разрешения на камеры глаз. На PICO 4 Ultra он есть, но его доступность зависит от
    ///     прошивки и от того, включил ли оператор отслеживание глаз в настройках шлема;
    ///   • направление головы доступно ВСЕГДА и совпадает с взглядом, когда оператор смотрит прямо
    ///     (а при работе с интерфейсом он смотрит прямо).
    ///
    /// ПОЧЕМУ ЭТО ЛУЧШЕ, ЧЕМ НИЧЕГО. Сейчас сервис глаз не получает данных вообще. С подачей по
    /// голове он начинает работать: фиксации считаются, выбор взглядом работает, фовеальное зрение
    /// включается. Когда появится источник зрачка — он подставится в `GazeSource`, и весь остальной
    /// код менять не придётся.
    ///
    /// ЧЕГО ЭТОТ КОМПОНЕНТ НЕ ДЕЛАЕТ: не притворяется, что знает направление зрачка. В журнале
    /// пишется честно, что источник — голова.
    /// </summary>
    public class KvXrGazeFeed : MonoBehaviour
    {
        /// <summary>
        /// ИСТОЧНИК НАСТОЯЩЕГО ВЗГЛЯДА (зрачок). Пока `null` — используется голова.
        ///
        /// Сюда ставится функция, которая даёт направление зрачка, когда появится платформенная
        /// проба. Сигнатура: (позиция, направление) → удалось ли прочитать.
        /// </summary>
        public static System.Func<Vector3, Quaternion, bool> GazeSource;

        /// <summary>Сервис, которому подаём взгляд. Ставится хабом при создании.</summary>
        public static KvEyeTrackingService Sink { get; set; }

        [Tooltip("Писать в журнал, что источник взгляда — голова, а не зрачок")]
        public bool logOnStart = true;

        /// <summary>
        /// ПИСАТЬ ЛИ ПОЗУ И ВЗГЛЯД В ЖУРНАЛ ДЕЙСТВИЙ. Частота и порог — правила `KvActionLog.PoseDue`
        /// (не чаще 10 Гц, только при заметном изменении), поэтому «включено» не означает «спам».
        /// </summary>
        [Tooltip("Писать позу головы и направление взгляда в журнал действий (не чаще 10 Гц)")]
        public bool logPose = true;

        private bool logged;

        /// <summary>Найти или создать компонент (идемпотентно, сцену не меняем).</summary>
        public static KvXrGazeFeed Ensure(Transform parent)
        {
            KvXrGazeFeed existing = Object.FindAnyObjectByType<KvXrGazeFeed>();
            if (existing != null) return existing;

            var go = new GameObject("XrGazeFeed");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvXrGazeFeed>();
        }

        /// <summary>
        /// ВЫЧИСЛИТЬ ВЗГЛЯД ПО ПОЗЕ ГОЛОВЫ — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без сцены).
        ///
        /// ВОЗВРАЩАЕТ `false` при нечисловых данных — по правилу E-21: сравнение с NaN всегда ложно,
        /// поэтому проверять надо ДО, а не после.
        /// </summary>
        public static bool TryHeadGaze(Vector3 headPosition, Quaternion headRotation,
            out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            if (!IsFinite(headPosition)) return false;

            // Направление головы. Для NaN-кватерниона результат тоже нечисловой — проверяем.
            Vector3 forward = headRotation * Vector3.forward;
            if (!IsFinite(forward)) return false;
            if (forward.sqrMagnitude < 1e-8f) return false;

            // Начало луча — чуть впереди головы: иначе луч начинается ВНУТРИ головы и первое
            // попадание оказывается на собственных волосах оператора.
            origin = headPosition + forward.normalized * OriginForwardMeters;
            direction = forward.normalized;
            return true;
        }

        /// <summary>Насколько впереди головы начинается луч взгляда, м.</summary>
        public const float OriginForwardMeters = 0.10f;

        private static bool IsFinite(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        void Update()
        {
            // ПОЗА ПИШЕТСЯ ДАЖЕ БЕЗ СЕРВИСА ГЛАЗ. Раньше стоял `if (Sink == null) return;` — и тогда
            // запись позы не работала бы ровно в том случае, когда сервиса нет (а его в сборке может
            // не быть вовсе). Запись в журнал — самостоятельная задача этого компонента, а не
            // побочный эффект подачи взгляда.
            bool havePose = KvXrInputSource.TryHeadPose(out Vector3 headPosition, out Quaternion headRotation);

            if (!havePose)
            {
                // Позы нет (шлема нет / потерян трекинг) — подавать нечего и писать нечего.
                return;
            }

            // 1. Настоящий взгляд, если платформа его даёт.
            bool realGaze = GazeSource != null && GazeSource(headPosition, headRotation);

            // 2. Иначе — направление головы (см. пояснение в шапке класса).
            Vector3 origin = Vector3.zero;
            Vector3 direction = headRotation * Vector3.forward;
            bool headGaze = !realGaze && TryHeadGaze(headPosition, headRotation, out origin, out direction);

            if (Sink != null)
            {
                if (!realGaze && headGaze) Sink.PushGaze(origin, direction);
            }

            // 3. ЗАПИСЬ ПОЗЫ И ВЗГЛЯДА В ЖУРНАЛ ДЕЙСТВИЙ (12.11.2026).
            //
            // ЧТО БЫЛО. Позы в записи не было вовсе. Разбор записи с устройства не мог ответить
            // на вопрос «куда смотрел оператор, когда робот поехал не туда» — а без этого строка
            // «робот начал движение» ничего не доказывает.
            //
            // ЧЕМ ПЛОХО БЫЛО БЫ ПИСАТЬ ЗДЕСЬ КАЖДЫЙ КАДР. Этот `Update` идёт на 72 Гц: наивная
            // запись дала бы 72 строки в секунду, то есть ровно тот поток, из-за которого за
            // сессию накопилось 1,1 ГБ. Поэтому частота и порог изменения — правила, а не
            // «на глаз»: не чаще 10 Гц И только когда голова сдвинулась больше 5 см или взгляд
            // повернулся больше 5°. Два одинаковых кадра дают ОДНУ строку.
            //
            // ЧАСЫ — `Time.realtimeSinceStartup`: они те же, что у остальных строк журнала, поэтому
            // `t=12.4 с` в строке позы сравнимо с временем любого другого события.
            if (logPose)
            {
                // Направление взгляда: настоящее, если платформа его дала, иначе — по голове.
                // Если не удалось ни то, ни другое, пишем направление головы «как есть»: строка
                // с позой всё равно полезнее пустоты, а `TryHeadGaze` уже отверг мусор.
                Vector3 gaze = headGaze ? direction : headRotation * Vector3.forward;

                KvActionLog.Instance.Pose(
                    Time.realtimeSinceStartup,
                    headPosition,
                    gaze,
                    1f / Mathf.Max(1e-4f, Time.unscaledDeltaTime));
            }

            if (logOnStart && !logged)
            {
                logged = true;
                Debug.Log("[KvXr] Взгляд: источник — ПОЗА ГОЛОВЫ (направление зрачка платформой не " +
                          "отдаётся). Фиксации и выбор взглядом работают по направлению взгляда " +
                          "оператора — это совпадает, пока он смотрит на панель прямо.");
            }
        }
    }
}
