using UnityEngine;

namespace KazistovVvXr
{
    /// <summary>
    /// ПЕРЕНОС ОКНА — ОДИН МЕХАНИЗМ НА ОБЕ РУКИ (указание владельца 24.10.2026: «добавь ещё
    /// возможность переноса окна и на левый контроллер»).
    ///
    /// ЧТО ДЕЛАЕТ. Держит состояние «окно взято и едет»: точку захвата, смещение окна относительно неё,
    /// положение окна в момент захвата и расстояние до него. Обе руки используют ЭТОТ ЖЕ класс — второй
    /// такой же код разошёлся бы, и одна рука тянула бы окно иначе, чем другая.
    ///
    /// ПРАВИЛА ЗАХВАТА (указание владельца):
    ///   • СРЕДНИЙ палец (grip) — в любой точке окна;
    ///   • УКАЗАТЕЛЬНЫЙ (trigger) — только в верхней полосе окна (24 % высоты, `IsTitlePoint`).
    ///
    /// ЧЕГО НЕ ДЕЛАЕТ: не дёргает окно. Из-за смещения `grabOffset` окно продолжает висеть там, где
    /// висело, и едет ровно на столько, на сколько уехала точка захвата.
    /// </summary>
    public class KvXrWindowDrag
    {
        /// <summary>Порог кнопок: курок и сжатие считаются нажатыми выше него (как у нажатия по панели).</summary>
        public const float ButtonThreshold = 0.7f;

        /// <summary>Взятое окно (пусто — переноса нет).</summary>
        public KvXrGroupPanel Window { get; private set; }

        /// <summary>Перенос идёт.</summary>
        public bool Active { get { return Window != null; } }

        private Vector3 holdPoint;
        private Vector3 grabOffset;
        private Vector3 startWindowPosition;
        private float distanceMeters;

        /// <summary>
        /// ГДЕ СЕЙЧАС КОНЧИК ЛУЧА, КОТОРЫЙ ТЯНЕТ ОКНО. Точка едет вместе с окном: окно уехало на Δ —
        /// кончик уехал на то же Δ. Так «лазер следит за перемещением», но в центр кнопки не прыгает.
        /// </summary>
        public Vector3 CurrentPoint
        {
            get
            {
                if (Window == null) return holdPoint;
                return holdPoint + (Window.transform.position - startWindowPosition);
            }
        }

        /// <summary>
        /// 30.10.2026: окно было ВЗЯТО этим кадром (фронт захвата).
        ///
        /// ЗАЧЕМ. Захват окна указательным пальцем и нажатие иконки читают ОДИН и тот же курок.
        /// Без этого признака одно нажатие успевало и взять окно, и нажать кнопку под лучом —
        /// класс дефекта «одно нажатие — два действия» (E-08/Б43), только новая пара. Вызывающий
        /// обязан пропустить нажатие ровно в том кадре, где перенос начался.
        /// </summary>
        public bool GrabbedThisTick { get { return grabbedThisTick; } }

        private bool grabbedThisTick;

        /// <summary>
        /// ОДИН ШАГ. `panel` — панель под лучом (может быть пустой). Возвращает `true`, если перенос
        /// идёт: тогда вызывающий обязан поставить метку и конец луча в <see cref="CurrentPoint"/>.
        /// </summary>
        public bool Tick(KvXrGroupPanel panel, Vector3 origin, Vector3 direction,
                         bool gripHeld, bool triggerHeld)
        {
            grabbedThisTick = false;

            if (Window == null)
            {
                if (panel == null || panel.followHand) return false;      // панель у руки не переносим
                if (direction.sqrMagnitude < 0.0001f) return false;

                bool byGrip = gripHeld;
                bool byTitle = triggerHeld && panel.IsTitlePoint(origin, direction);
                if (!byGrip && !byTitle) return false;

                Window = panel;
                distanceMeters = Mathf.Clamp(Vector3.Distance(origin, panel.transform.position), 0.25f, 2f);
                holdPoint = origin + direction.normalized * distanceMeters;
                grabOffset = panel.transform.position - holdPoint;
                startWindowPosition = panel.transform.position;
                grabbedThisTick = true;                                   // ← фронт захвата (см. выше)

                Debug.Log("[KvXr] ОКНО: взято для переноса · " +
                          (byGrip ? "средним пальцем (любая точка)" : "указательным (верх окна)") +
                          " · было на " + startWindowPosition.ToString("0.00") + " м");
                return true;
            }

            if (!gripHeld && !triggerHeld)
            {
                Debug.Log("[KvXr] ОКНО: перенесено · " + Window.transform.position.ToString("0.00") + " м");
                Window = null;
                return false;
            }

            if (direction.sqrMagnitude < 0.0001f) return true;

            Window.transform.position = origin + direction.normalized * distanceMeters + grabOffset;
            return true;
        }

        /// <summary>Забыть перенос (пробуждение шлема, смена сцены).</summary>
        public void Reset()
        {
            Window = null;
        }
    }
}
