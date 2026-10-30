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
            if (Sink == null) return;

            Vector3 headPosition;
            Quaternion headRotation;
            if (!KvXrInputSource.TryHeadPose(out headPosition, out headRotation)) return;

            // 1. Настоящий взгляд, если платформа его даёт.
            if (GazeSource != null && GazeSource(headPosition, headRotation)) return;

            // 2. Иначе — направление головы (см. пояснение в шапке класса).
            Vector3 origin, direction;
            if (!TryHeadGaze(headPosition, headRotation, out origin, out direction)) return;

            Sink.PushGaze(origin, direction);

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
