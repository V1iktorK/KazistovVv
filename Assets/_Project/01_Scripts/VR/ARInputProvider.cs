using UnityEngine;
using UnityEngine.XR.ARFoundation;  // ARSession / ARSessionState — «есть ли вообще подсистема AR» (P3-10)

// Ввод от AR-устройства (планшет): касания и луч по найденным плоскостям.
// P3-10 (11.11.2026): доступность считается по AR-подсистеме, а луч по касанию больше не
// заглушка — он делегируется в `KvArPlacement.TryRaycast`. Жесты захвата/записи по-прежнему
// не реализованы и честно возвращают false (их нет и в AR-интерфейсе).
public class ARInputProvider : InputProvider
{
    public override Vector3 GetPointerPosition() => Camera.main?.transform.position ?? Vector3.zero;
    public override Vector3 GetPointerDirection() => Camera.main?.transform.forward ?? Vector3.forward;
    
    public override bool GetSelectDown() => Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
    public override bool GetSelectHeld() => Input.touchCount > 0;
    public override bool GetGrabDown() => false;
    public override bool GetGrabHeld() => false;
    public override bool GetSwitchRobotDown() => false;
    public override bool GetRecordDown() => false;
    public override bool GetPlayDown() => false;
    
    public override Vector2 GetMovement() => Vector2.zero;
    public override Vector2 GetRotation() => Vector2.zero;

    /// <summary>
    /// P3-10 (11.11.2026). ЧТО БЫЛО. `IsAvailable()` возвращал «платформа Android или iOS», то
    /// есть `true` и на шлеме PICO, где никакого AR нет вовсе. ЧЕМ ЭТО ГРОЗИЛО. `InputManager`
    /// выбирал этот провайдер как доступный по одному признаку платформы — и всё управление
    /// уходило в ветку, где `ARRaycastFromTouch` всегда `false`, а `GetMovement`/`GetRotation` —
    /// нули. Оператор оставался без осмотра и без выбора, при формально «доступном» вводе.
    /// ТЕПЕРЬ проверяется САМА AR-ПОДСИСТЕМА: без готовой сессии (`None`/`Unsupported`) провайдер
    /// честно недоступен, и `InputManager` его не выберет.
    /// </summary>
    public override bool IsAvailable()
    {
        ARSessionState state = ARSession.state;
        // KvArSessionController.IsReadyState — единственный источник правила «AR пригоден»:
        // Ready / SessionInitializing / SessionTracking. Unsupported, None и «нужна установка»
        // доступными не считаются, иначе мы снова обещали бы ввод, которого нет.
        return KazistovVvAr.KvArSessionController.IsReadyState(state);
    }
    
    /// <summary>
    /// Луч по экранной точке для `PoseSelector.HandleARInput`.
    ///
    /// P3-10 (11.11.2026). ЧТО БЫЛО. Метод был ЗАГЛУШКОЙ: всегда `false`, `hitPoint = zero`.
    /// ЦЕЛИКОМ на нём построен `PoseSelector.HandleARInput` — значит на планшете не работал ни
    /// прицел, ни постановка точки: маркер прятался, робот не получал цели, и никакого сообщения
    /// об этом не было (пустой `false` неотличим от «палец мимо плоскости»).
    /// ЧЕМ ЭТО ГРОЗИЛО. «AR-режим работает» было неправдой: тап по столу ничего не делал, а
    /// причина не называлась вовсе.
    /// ТЕПЕРЬ делегируем в НАСТОЯЩИЙ луч по плоскостям — `KvArPlacement.TryRaycast` (тот же,
    /// которым ставится мир и работает прицел). Ссылку ищем один раз и запоминаем.
    /// </summary>
    public bool ARRaycastFromTouch(Vector2 touchPos, out Vector3 hitPoint)
    {
        hitPoint = Vector3.zero;

        KazistovVvAr.KvArPlacement target = placement;
        if (target == null)
        {
            // Один поиск на компонент: дальше ссылка живёт в поле. `FindAnyObjectByType` только
            // при первом обращении — в кадре его нет.
            target = UnityEngine.Object.FindAnyObjectByType<KazistovVvAr.KvArPlacement>();
            placement = target;
        }

        if (target == null)
        {
            // Честный отказ: объекта AR-постановки в сцене нет — луч строить нечем.
            Debug.LogWarning("[ARInputProvider] ARRaycastFromTouch: в сцене нет KvArPlacement — " +
                             "постановка по касанию невозможна (мир в AR ставится его средствами)");
            return false;
        }

        KazistovVvAr.KvArSurfaceHit surface;
        if (!target.TryRaycast(touchPos, out surface)) return false;

        hitPoint = surface.point;
        return true;
    }

    /// <summary>Кэш ссылки на AR-постановку (см. `ARRaycastFromTouch`).</summary>
    private KazistovVvAr.KvArPlacement placement;
}
