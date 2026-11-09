using UnityEngine;

public class PoseSelector : MonoBehaviour
{
    [Header("Visuals")]
    public LineRenderer laserPointer;
    public TargetMarker targetMarker;
    public LayerMask selectableLayers;
    public float maxDistance = 15f;
    
    void Update()
    {
        if (InputManager.Instance == null || InputManager.Instance.ActiveProvider == null) return;
        
        var input = InputManager.Instance;
        
        if (input.ActiveProvider is ARInputProvider arInput)
        {
            HandleARInput(arInput);
            return;
        }
        
        HandleStandardInput(input);
    }
    
    void HandleStandardInput(InputManager input)
    {
        Vector3 origin = input.PointerPosition;
        Vector3 dir = input.PointerDirection;
        
        if (laserPointer != null)
        {
            laserPointer.SetPosition(0, origin);
        }
        
        if (Physics.Raycast(origin, dir, out RaycastHit hit, maxDistance, selectableLayers))
        {
            if (laserPointer != null) laserPointer.SetPosition(1, LaserEndPoint(origin, dir, maxDistance, true, hit.point));
            if (targetMarker != null)
            {
                targetMarker.transform.position = hit.point;
                targetMarker.gameObject.SetActive(true);
            }
            
            if (ShouldSelect(true, input.SelectDown))
                RobotSelector.Instance?.GetActiveRobot()?.SetTarget(hit.point);
        }
        else
        {
            if (laserPointer != null) laserPointer.SetPosition(1, LaserEndPoint(origin, dir, maxDistance, false, Vector3.zero));
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// КУДА РИСУЕТСЯ КОНЕЦ ЛУЧА (чистая функция — проверяется тестом без шлема и без сцены).
    ///
    /// ПОПАДАНИЕ — конец луча в точке попадания; промах — на полной длине луча. Это не мелочь
    /// оформления: если при промахе конец луча остаётся в точке ПРОШЛОГО попадания, луч «залипает»
    /// на старом месте и оператор целится по неверной картинке.
    /// </summary>
    public static Vector3 LaserEndPoint(Vector3 origin, Vector3 direction, float maxDistance,
        bool hasHit, Vector3 hitPoint)
    {
        return hasHit ? hitPoint : origin + direction * maxDistance;
    }

    /// <summary>
    /// НАЖАТИЕ ВЫБОРА СРАБАТЫВАЕТ ТОЛЬКО ПО ЦЕЛИ (чистая функция).
    ///
    /// ЗАЧЕМ. `SelectDown` приходит каждый раз, когда нажат курок/тап, — в том числе когда луч
    /// ушёл в пустоту. Без этой проверки робот получал бы цель «в никуда» (нулевую точку), и он
    /// уезжал бы в начало координат — а оператор видел бы «робот сам поехал».
    /// </summary>
    public static bool ShouldSelect(bool hasHit, bool selectDown)
    {
        return hasHit && selectDown;
    }
    
    void HandleARInput(ARInputProvider arInput)
    {
        if (Input.touchCount == 0) 
        {
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
            return;
        }
        
        Vector2 touchPos = Input.GetTouch(0).position;
        
        if (arInput.ARRaycastFromTouch(touchPos, out Vector3 hitPoint))
        {
            if (laserPointer != null)
            {
                laserPointer.SetPosition(0, arInput.GetPointerPosition());
                laserPointer.SetPosition(1, hitPoint);
            }
            if (targetMarker != null)
            {
                targetMarker.transform.position = hitPoint;
                targetMarker.gameObject.SetActive(true);
            }
            if (InputManager.Instance.SelectDown)
                RobotSelector.Instance?.GetActiveRobot()?.SetTarget(hitPoint);
        }
        else
        {
            if (targetMarker != null) targetMarker.gameObject.SetActive(false);
        }
    }
}
