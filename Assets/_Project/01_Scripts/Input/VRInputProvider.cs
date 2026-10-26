using UnityEngine;
using KazistovVvXr;

/// <summary>
/// ВВОД ОТ ШЛЕМА (этап 2 плана XR, сессия §28).
///
/// ЧТО БЫЛО. До этой сессии класс был ЗАГЛУШКОЙ: `IsAvailable()` возвращал false, все
/// действия — false, указатель брался у `Camera.main`. То есть шлем в проекте не работал
/// вообще: `InputManager` выбирал клавиатуру с мышью, и это было правильно, пока устройства
/// не было.
///
/// ЧТО СТАЛО. Поставщик читает настоящее XR-устройство через <see cref="KvXrInputSource"/>
/// и отдаёт проекту ровно тот же набор действий, что мышь и геймпад: позицию и направление
/// указателя, выбор, захват, смену робота, запись, пуск, перемещение.
///
/// ПОЧЕМУ ЭТО ВАЖНО ДЛЯ ВСЕГО ОСТАЛЬНОГО. Действия проекта уже разведены через
/// `InputManager` → `InputProvider`: `FreeFlyCameraController`, `PoseSelector`,
/// `TrajectoryRecorder`, `RobotSelector`, `KazistovVvUIManager` читают ИХ, а не клавиши.
/// Значит, заменив заглушку на живой источник, мы получаем рабочий ввод во всей платформе
/// без правок ядра — это и есть требование [K3] «ядро не переписывать».
///
/// ЧЕСТНОЕ ОГРАНИЧЕНИЕ: без шлема проверить нечем. Компонент сам сообщает `IsAvailable() =
/// false`, когда устройства нет, поэтому десктопный режим не ломается.
/// </summary>
public class VRInputProvider : InputProvider
{
    /// <summary>Порог курка, при котором действие считается нажатым.</summary>
    public float triggerThreshold = 0.5f;

    /// <summary>Порог грипа для захвата.</summary>
    public float gripThreshold = 0.6f;

    /// <summary>Порог стика для дискретных действий (пуск/запись/смена робота).</summary>
    public float stickThreshold = 0.75f;

    /// <summary>Длина луча указателя, метры.</summary>
    public float pointerLength = 10f;

    /// <summary>Сглаживание стика для плавного движения камеры (0 — без сглаживания).</summary>
    public float stickSmoothing = 0.15f;

    /// <summary>Пауза между дискретными действиями стика, секунды (защита от дребезга).</summary>
    public const float DiscreteActionCooldown = 0.35f;

    private Vector2 smoothMove;
    private Vector2 smoothLook;
    private float lastRecordTime = -10f;
    private float lastPlayTime = -10f;
    private float lastSwitchTime = -10f;

    void Update()
    {
        Vector2 move = KvXrInputSource.LeftStick;
        Vector2 look = KvXrInputSource.RightStick;
        float k = stickSmoothing <= 0f ? 1f : 1f - Mathf.Exp(-Time.deltaTime / stickSmoothing);
        smoothMove = Vector2.Lerp(smoothMove, move, k);
        smoothLook = Vector2.Lerp(smoothLook, look, k);
    }

    // ------------------------------------------------------------------ указатель

    /// <summary>
    /// Точка, откуда идёт луч. Сначала правый контроллер, потом голова: у шлема без
    /// контроллеров (только руки или только голова) указатель обязан продолжать работать.
    /// </summary>
    public override Vector3 GetPointerPosition()
    {
        if (KvXrInputSource.TryRightPose(out Vector3 hand, out Quaternion _)) return hand;
        if (KvXrInputSource.TryHeadPose(out Vector3 eyes, out Quaternion _)) return eyes;
        Camera main = Camera.main;
        return main != null ? main.transform.position : Vector3.zero;
    }

    /// <summary>Направление луча.</summary>
    public override Vector3 GetPointerDirection()
    {
        if (KvXrInputSource.TryRightPose(out Vector3 _, out Quaternion handRot)) return handRot * Vector3.forward;
        if (KvXrInputSource.TryHeadPose(out Vector3 _, out Quaternion headRot)) return headRot * Vector3.forward;
        Camera main = Camera.main;
        return main != null ? main.transform.forward : Vector3.forward;
    }

    // ------------------------------------------------------------------ действия

    /// <summary>Выбор: курок. Именно он ставит точку — как Enter на десктопе.</summary>
    public override bool GetSelectDown() { return KvXrInputSource.RightTrigger >= triggerThreshold; }

    public override bool GetSelectHeld() { return GetSelectDown(); }

    /// <summary>Захват: грип. Им оператор «берёт» робота и таскает его по сцене.</summary>
    public override bool GetGrabDown() { return KvXrInputSource.RightGrip >= gripThreshold; }

    public override bool GetGrabHeld() { return GetGrabDown(); }

    /// <summary>Смена робота: левый стик вбок — то же, что F на десктопе.</summary>
    public override bool GetSwitchRobotDown()
    {
        if (Time.time - lastSwitchTime < DiscreteActionCooldown) return false;
        if (Mathf.Abs(KvXrInputSource.LeftStick.x) < stickThreshold) return false;
        lastSwitchTime = Time.time;
        return true;
    }

    /// <summary>Запись позы: левый стик вниз.</summary>
    public override bool GetRecordDown()
    {
        if (Time.time - lastRecordTime < DiscreteActionCooldown) return false;
        if (KvXrInputSource.LeftStick.y > -stickThreshold) return false;
        lastRecordTime = Time.time;
        return true;
    }

    /// <summary>Пуск: левый стик вверх.</summary>
    public override bool GetPlayDown()
    {
        if (Time.time - lastPlayTime < DiscreteActionCooldown) return false;
        if (KvXrInputSource.LeftStick.y < stickThreshold) return false;
        lastPlayTime = Time.time;
        return true;
    }

    /// <summary>Перемещение камеры: сглаженный левый стик.</summary>
    public override Vector2 GetMovement() { return smoothMove; }

    /// <summary>Обзор: сглаженный правый стик.</summary>
    public override Vector2 GetRotation() { return smoothLook; }

    /// <summary>
    /// Доступен ли ввод от шлема. Без устройства — false, и `InputManager` штатно
    /// остаётся на клавиатуре с мышью: десктопный режим не ломается ([K3]).
    /// </summary>
    public override bool IsAvailable()
    {
        return KvXrInputSource.HeadsetPresent && (KvXrInputSource.HasRight || KvXrInputSource.HasLeft);
    }

    /// <summary>Строка состояния — для журнала и отчёта.</summary>
    public string Describe()
    {
        return string.Format("{0} · доступен: {1} · курок {2:0.00} · грип {3:0.00}",
            KvXrInputSource.Describe(), IsAvailable() ? "да" : "нет",
            KvXrInputSource.RightTrigger, KvXrInputSource.RightGrip);
    }
}
