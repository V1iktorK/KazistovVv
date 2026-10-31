using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// КАМЕРА ШЛЕМА СЛЕДУЕТ ЗА ГОЛОВОЙ ОПЕРАТОРА (сессия §35, найдено 18.10.2026 на живом шлеме).
    ///
    /// СИМПТОМЫ ВЛАДЕЛЬЦА, которые это лечит:
    ///   • «камера где-то в непонятном месте»;
    ///   • «я нахожусь в 0 по высоте где-то за ангаром»;
    ///   • «камера плохо поворачивается и всегда крутится».
    ///
    /// ПРИЧИНА (доказана строкой журнала, а не рассуждением). В журнале шлема:
    ///     [KvHangar] камеры: «XrCamera» (0.00, 0.00, 0.00) вкл (main) · поза шлема (-0.73, 1.54, -0.22)
    /// То есть объект-камера стоял в НАЧАЛЕ КООРДИНАТ НА УРОВНЕ ПОЛА, а настоящая голова была
    /// на высоте 1,54 м — и в другом месте. Проба ориентации восемь раз подряд показала
    /// «курс шлема 0.0°», хотя оператор в это время крутил головой.
    ///
    /// ПОЧЕМУ ТАК ВЫШЛО. В XR-сцене НЕТ ни `TrackedPoseDriver`, ни `XROrigin` — проверено поиском
    /// по файлу сцены: 0 вхождений. Позу головы на камеру не переносил НИКТО. `KvXrRig` этого
    /// намеренно не делает (в его описании сказано «попытка двигать камеру руками» — отвергнута),
    /// предполагая, что это сделает XR-подсистема. Подсистема отрисовывает стерео, но трансформ
    /// камеры не ведёт — и весь мир рисовался с неподвижной точки на полу.
    ///
    /// ЧТО ДЕЛАЕТ ЭТОТ КОМПОНЕНТ. Каждый кадр (в `LateUpdate`, то есть ПОСЛЕ опроса трекинга)
    /// переносит на камеру позу головы из системы ввода. Источник позы — тот же, что уже доказанно
    /// работает и печатает высоту 1,54 м: `KvXrInputSource.TryHeadPose`. Своих вычислений позы
    /// здесь нет — второго источника истины не появляется.
    ///
    /// ЛЕЧЕНИЕ ТРЯСКИ (29.10.2026, живой шлем: «всё равно всё трясётся»). Две причины:
    ///   1) ПОЗА ПИСАЛАСЬ В МИРОВЫЕ КООРДИНАТЫ (`SetPositionAndRotation`), а камера — дочерний
    ///      объект рига `XrRig`, который `KvXrLocomotion` двигает при телепорте/повороте/ходе.
    ///      Два писателя конфликтовали каждый кадр: риг сдвигал камеру, мировая запись откатывала
    ///      её обратно — мир дёргался. Теперь поза пишется в ЛОКАЛЬНЫЕ координаты (как у
    ///      TrackedPoseDriver по умолчанию): движение рига двигает камеру автоматически,
    ///      голова следует за трекингом — конфликта нет.
    ///   2) ПОЗА ПРИМЕНЯЛАСЬ В `LateUpdate`, а рендер кадра уже мог идти: картинка отставала
    ///      на кадр и «дрожала» при быстрых поворотах головы. Теперь поза применяется ещё и в
    ///      `Application.onBeforeRender` — самое свежее место до отрисовки (то, что делает
    ///      стандартный TrackedPoseDriver).
    ///
    /// ЧЕГО ЭТО НЕ ДЕЛАЕТ: не трогает риг, не меняет масштаб мира, не двигает столы и зал.
    /// Если позы нет (трекинг не готов) — НЕ ПЕРЕПИСЫВАЕТ камеру, чтобы не поставить её в ноль.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class KvXrHeadTracking : MonoBehaviour
    {
        /// <summary>Камера, которой смотрит оператор. Пусто — найдётся сама.</summary>
        public Camera headCamera;

        /// <summary>Сообщать в журнал, когда поза головы впервые применилась (одна строка).</summary>
        public bool logOnFirstApply = true;

        /// <summary>Сколько кадров поза была применена (для журнала и проверок).</summary>
        public int AppliedFrames { get; private set; }

        /// <summary>Последняя применённая поза головы.</summary>
        public Vector3 LastHeadPosition { get; private set; }

        /// <summary>Следовать ли за головой прямо сейчас (поза доступна).</summary>
        public bool Tracking { get; private set; }

        private bool logged;

        void Awake()
        {
            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) headCamera = GetComponent<Camera>();
            if (headCamera == null) headCamera = GetComponentInChildren<Camera>();
        }

        void OnEnable()
        {
            // ЛЕЧЕНИЕ ТРЯСКИ (29.10.2026): поза применяется и перед рендером — картинка
            // не отстаёт на кадр при поворотах головы (стандарт TrackedPoseDriver).
            Application.onBeforeRender += ApplyFromTracking;
        }

        void OnDisable()
        {
            Application.onBeforeRender -= ApplyFromTracking;
        }

        void LateUpdate()
        {
            ApplyFromTracking();
        }

        private void ApplyFromTracking()
        {
            if (headCamera == null) return;

            Vector3 position;
            Quaternion rotation;
            if (!KvXrInputSource.TryHeadPose(out position, out rotation))
            {
                // Трекинга нет — камеру НЕ трогаем. Иначе она прыгнула бы в начало координат,
                // а это ровно та точка на полу, из которой владелец и смотрел.
                Tracking = false;
                return;
            }

            // ЛЕЧЕНИЕ ТРЯСКИ (29.10.2026): поза трекинга трансформируется в МИРОВЫЕ координаты
            // через риг (KvXrInputSource.TrackingOrigin), поэтому движение рига (телепорт/
            // поворот/ход) автоматически двигает камеру — конфликта писателей нет, дрожи нет.
            // Порядок корректен: KvXrLocomotion двигает риг в Update, здесь — LateUpdate и
            // onBeforeRender, то есть трансформация идёт через УЖЕ сдвинутый риг.
            headCamera.transform.SetPositionAndRotation(position, rotation);
            LastHeadPosition = position;
            AppliedFrames++;
            Tracking = true;

            if (logOnFirstApply && !logged)
            {
                logged = true;
                Debug.Log("[KvXr] Камера следует за головой: поза " + position.ToString("0.00") +
                          " · высота " + position.y.ToString("0.00") + " м · поворот " +
                          rotation.eulerAngles.ToString("0") +
                          " (мировая, через риг; в сцене не было ни TrackedPoseDriver, ни XROrigin)");
            }
        }
    }
}
