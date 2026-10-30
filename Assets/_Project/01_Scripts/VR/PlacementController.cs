using UnityEngine;

/// <summary>

/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Размещение делает CenterWindow + KvXrMrRoom.

/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.

/// </summary>

[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Размещение делает CenterWindow + KvXrMrRoom.")]

public class PlacementController : MonoBehaviour
{
    [Header("References")]
    public GameObject indicator;            
    public SpatialAnchorManager anchorManager;
    public LayerMask floorLayer;            

    private RaycastHit hit;
    private bool isPointingAtFloor = false;

    void Update()
    {
        // Берем позицию и направление из активного InputProvider
        Vector3 pointerPos = InputManager.Instance.PointerPosition;
        Vector3 pointerDir = InputManager.Instance.PointerDirection;

        // Пускаем луч
        if (Physics.Raycast(pointerPos, pointerDir, out hit, 10f, floorLayer))
        {
            isPointingAtFloor = true;
            indicator.SetActive(true);
            indicator.transform.position = hit.point;
            indicator.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
        }
        else
        {
            isPointingAtFloor = false;
            indicator.SetActive(false);
        }

        // Проверяем нажатие Select (ЛКМ в Keyboard/Mouse, Курок в VR/Геймпад)
        if (isPointingAtFloor && InputManager.Instance.SelectDown)
        {
            PlaceRobot();
        }
    }

    private void PlaceRobot()
    {
        if (anchorManager != null)
        {
            anchorManager.PlaceRobot(indicator.transform.position, indicator.transform.rotation);
            indicator.SetActive(false);
        }
    }
}