using UnityEngine;

/// <summary>
/// Абстрактная основа всех контроллеров роботов.
/// Реализует позиционный CCD (Cyclic Coordinate Descent) для суставов,
/// разрешение ссылок на геометрию по именам и базовую телеметрию.
/// </summary>
public class RobotController : MonoBehaviour
{
    [Header("Base")]
    public string robotName = "Robot";
    public Transform tcp;
    [Range(0.1f, 5f)] public float maxSpeed = 1f;
    [HideInInspector] public bool isActive = false;

    [Header("Скорость перемещения")]
    [Tooltip("Общий множитель скорости всех роботов (0.34 ≈ в 3 раза медленнее стандартной)")]
    public float movementSpeedScale = 0.34f;

    [Header("Industry 4.0 - Telemetry")]
    public bool telemetryEnabled = true;
    public float jointTemperature = 25f;
    public float operatingHours = 0f;

    [Header("Automatic kinematics")]
    public Transform fixedBase;
    public Transform endEffector;
    public string fixedBaseName = "LS10-B702S_base_1";
    public string endEffectorName = "Axis6_2";
    public string alternativeEndEffectorName = "LS10-B702S_z_5";
    [Range(1, 32)] public int ikIterations = 12;
    [Min(0.001f)] public float ikTolerance = 0.01f;

    protected Vector3 targetPosition;
    protected Quaternion targetRotation;
    protected bool hasTarget;

    protected virtual void Awake()
    {
        targetPosition = transform.position;
        targetRotation = transform.rotation;
        ResolveKinematicReferences();

        // Диагностика дубликатов: роботы в рантайме НЕ создаются (все — объекты MainScene).
        // Если в PlayMode роботов больше двух/появился второй робот того же типа —
        // это лишняя копия в открытой сцене (или копия, добавленная через UI).
        RobotInventory.Guard(this);
    }

    /// <summary>Задать цель только по позиции (ориентация не меняется).</summary>
    public virtual void SetTarget(Vector3 position)
    {
        targetPosition = position;
        hasTarget = true;
    }

    /// <summary>Задать цель по позиции и ориентации.</summary>
    public virtual void SetTarget(Vector3 position, Quaternion rotation)
    {
        SetTarget(position);
        targetRotation = rotation;
    }

    /// <summary>
    /// Сбросить цель: пока нет ПОДТВЕРЖДЁННОЙ позиции — робот не двигается
    /// (критично для логики «два лазера»).
    /// </summary>
    public virtual void ClearTarget()
    {
        hasTarget = false;
        targetPosition = tcp != null ? tcp.position : transform.position;
        targetRotation = transform.rotation;
    }

    public virtual void MoveToTarget(float deltaTime)
    {
        if (!hasTarget)
        {
            return;
        }

        ResolveKinematicReferences();
        if (fixedBase == null || endEffector == null)
        {
            return;
        }

        Transform[] joints = BuildJointChain();
        if (joints.Length == 0)
        {
            return;
        }

        Vector3 basePosition = fixedBase.position;
        Quaternion baseRotation = fixedBase.rotation;
        float step = Mathf.Clamp(deltaTime * maxSpeed * 12f, 0.01f, 1f);

        for (int iteration = 0; iteration < ikIterations; iteration++)
        {
            if (Vector3.Distance(endEffector.position, targetPosition) <= ikTolerance)
            {
                break;
            }

            for (int i = joints.Length - 1; i >= 0; i--)
            {
                Transform joint = joints[i];
                Vector3 toEnd = endEffector.position - joint.position;
                Vector3 toTarget = targetPosition - joint.position;
                if (toEnd.sqrMagnitude < 0.000001f || toTarget.sqrMagnitude < 0.000001f)
                {
                    continue;
                }

                Quaternion correction = Quaternion.FromToRotation(toEnd, toTarget);
                joint.rotation = Quaternion.Slerp(joint.rotation, correction * joint.rotation, step);
            }
        }

        fixedBase.SetPositionAndRotation(basePosition, baseRotation);
    }

    private void ResolveKinematicReferences()
    {
        Transform root = transform;
        fixedBase = fixedBase != null ? fixedBase : FindChild(root, fixedBaseName);
        if (fixedBase == null)
        {
            fixedBase = root;
        }

        endEffector = endEffector != null
            ? endEffector
            : FindChild(root, endEffectorName);
        if (endEffector == null)
        {
            endEffector = FindChild(root, alternativeEndEffectorName);
        }
        if (endEffector == null)
        {
            endEffector = tcp;
        }
        if (endEffector == null)
        {
            endEffector = FindDeepestDescendant(root);
        }

        // Если tcp не назначен в инспекторе — берём endEffector, чтобы
        // телеметрия и телеоперация не падали с UnassignedReferenceException.
        if (tcp == null)
        {
            tcp = endEffector;
        }
    }

    /// <summary>Строит цепочку суставов от фланца вверх до фиксированной базы.</summary>
    private Transform[] BuildJointChain()
    {
        Transform node = endEffector != null ? endEffector.parent : null;

        int count = 0;
        while (node != null && node != fixedBase)
        {
            count++;
            node = node.parent;
        }

        if (node != fixedBase)
        {
            return new Transform[0];
        }

        Transform[] chain = new Transform[count];
        node = endEffector != null ? endEffector.parent : null;
        for (int i = count - 1; i >= 0 && node != null && node != fixedBase; i--)
        {
            chain[i] = node;
            node = node.parent;
        }
        return chain;
    }

    /// <summary>Максимально глубокий потомок в иерархии (запасной end effector).</summary>
    protected static Transform FindDeepestDescendant(Transform root)
    {
        Transform deepest = null;
        int deepestLevel = -1;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child == root)
            {
                continue;
            }

            int level = 0;
            Transform parent = child.parent;
            while (parent != null && parent != root)
            {
                level++;
                parent = parent.parent;
            }

            if (parent == root && level > deepestLevel)
            {
                deepest = child;
                deepestLevel = level;
            }
        }

        return deepest;
    }

    /// <summary>Прямой поиск потомка по точному имени.</summary>
    protected static Transform FindChild(Transform root, string objectName)
    {
        if (root == null || objectName == null || objectName.Length == 0)
        {
            return null;
        }

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == objectName)
            {
                return child;
            }
        }

        return null;
    }

    public virtual float[] GetJointAngles()
    {
        return new float[0];
    }

    /// <summary>
    /// ПОДСВЕТКА ВЫБРАННОГО РОБОТА ЧЕРЕЗ `MaterialPropertyBlock` (P2-7 / P4-15, 11.11.2026).
    ///
    /// ЧТО БЫЛО. Здесь стояло `renderer.material.SetColor(...)` в цикле по ВСЕМ рендерерам.
    /// `renderer.material` — это СВОЯ КОПИЯ материала на каждый рендерер, и копии никто не
    /// уничтожал: у робота с десятками частей каждый выбор создавал десятки материалов (утечка
    /// нативной памяти + рост потребления кадра). Плюс в редакторе `renderer.material` может
    /// вернуть сам АССЕТ, если у рендерера нет копии, — и тогда цвет записывался прямо в
    /// `04_Materials/Robot/Robot_HDRP.mat`: прогон «пачкал» отслеживаемый файл (наблюдалось
    /// 11.11.2026, критерий P4-15 «git status чист после прогона»).
    ///
    /// ЧТО СТАЛО. Свойства пишутся в `MaterialPropertyBlock`: это per-renderer значения, которые
    /// НЕ создают материалов, не пачкают ассеты и применяются без пересборки материала.
    ///
    /// ЗАМЕТКА ПРО КЛЮЧЕВОЕ СЛОВО. `_EMISSION` — ключевое слово МАТЕРИАЛА, его нельзя включить
    /// через блок свойств. Поэтому вместо включения ключевого слова используется заведомо
    /// ненулевой цвет свечения: чтобы эмиссия была видна, она должна быть включена в материале
    /// робота (в проекте так и есть). Если материала нет вовсе — рендерер пропускается.
    /// </summary>
    public virtual void SetActive(bool active)
    {
        isActive = active;
        Color emission = active ? Color.green * 0.3f : Color.black;

        foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
        {
            if (renderer == null) continue;
            Material shared = renderer.sharedMaterial;
            if (shared == null || !shared.HasProperty("_EmissionColor")) continue;

            if (highlightBlock == null) highlightBlock = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(highlightBlock);
            highlightBlock.SetColor("_EmissionColor", emission);
            renderer.SetPropertyBlock(highlightBlock);
        }
    }

    /// <summary>Блок свойств подсветки: один на робота, чтобы не выделять память на каждый выбор.</summary>
    private MaterialPropertyBlock highlightBlock;

    public virtual void UpdateTelemetry(float deltaTime)
    {
        if (!telemetryEnabled)
        {
            return;
        }

        operatingHours += deltaTime / 3600f;
        Vector3 reference = tcp != null ? tcp.position : transform.position;
        float movement = Vector3.Distance(reference, targetPosition);
        jointTemperature = Mathf.Lerp(jointTemperature, 25f + movement * 50f, deltaTime * 0.05f);
    }
}

/// <summary>
/// Учёт роботов сцены: в MainScene их ровно два — 6-осевой на стенде 1 и SCARA на стенде 2.
/// Рантайм роботов не создаёт (создание возможно только явной командой оператора
/// «Добавить робота» на верхней панели интерфейса → ObjectSpawner); этот класс только проверяет инвентарь
/// и пишет предупреждение, если в PlayMode обнаружены дубликаты.
/// </summary>
public static class RobotInventory
{
    private static int awaited;        // сколько роботов Awake'нулось в текущем кадре
    private static int loggedFrame = -1;

    /// <summary>
    /// Пауза проверки на время создания служебных копий (фантомы). Копия создаётся
    /// «тихим» Instantiate: её `Awake` отрабатывает ДО того, как копии выставят
    /// HideInHierarchy, поэтому проверка инвентаря видела «лишнего» робота и писала
    /// ложное предупреждение «Роботов в сцене: 3» на каждое создание фантомов.
    /// </summary>
    public static bool Suppress;

    /// <summary>Проверка инвентаря при появлении робота (вызывается из RobotController.Awake).</summary>
    public static void Guard(RobotController self)
    {
        if (self == null || !Application.isPlaying) return;
        if (Suppress) return;                    // идёт создание копии-фантома — это не робот сцены

        int frame = Time.frameCount;
        if (frame != loggedFrame) { loggedFrame = frame; awaited = 0; }
        awaited++;

        int total = 0, sixAxis = 0, scara = 0, other = 0;
        foreach (RobotController rc in Object.FindObjectsByType<RobotController>(FindObjectsInactive.Include))
        {
            if (rc == null) continue;
            // Фантомы (служебные копии) и копии, уже выставленные оператором через UI, — не сцена.
            if ((rc.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) continue;
            if (rc.GetComponent<KazistovVvUI.RegisteredObject>() != null) continue;
            total++;
            if (rc is SixAxisController) sixAxis++;
            else if (rc is SCARAController) scara++;
            else other++;
        }

        if (awaited < total) return;    // ждём, пока Awake'нутся все — иначе счёт неполный

        if (total == 2 && sixAxis == 1 && scara == 1)
            Debug.Log("[RobotInventory] Роботов в сцене: 2 (6-осевой + SCARA). Рантайм роботов не создаёт.");
        else
            Debug.LogWarning("[RobotInventory] Роботов в сцене: " + total +
                " (6-осевых " + sixAxis + ", SCARA " + scara + ", прочих " + other +
                ") — ожидается ровно 2: 6-осевой на стенде 1 и SCARA на стенде 2. " +
                "Лишние объекты — копии в открытой сцене, а не работа рантайм-скриптов.");
    }
}