using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Добавляет коллайдеры к каждому звену 6-осного манипулятора и
/// отключает столкновения между соседними звеньями (чтобы робот
/// не сталкивался сам с собой, но при этом взаимодействовал с
/// окружающей средой: стол, стены, другие объекты).
/// </summary>
/// <remarks>
/// 6-осный манипулятор приводит к перекрытию соседних звеньев при больших
/// углах поворота (особенно вблизи стыков J2-J3, J4-J5). Этот скрипт решает
/// задачу «самостолкновений» путём:
/// - присвоения каждому Transform звена простого коллайдера (Capsule);
/// - игнорирования столкновений между соседними и «дальними» звеньями
///   (см. RobotConfig.IgnoredCollisionPairs);
/// - включения столкновений с окружающей средой (рабочая поверхность, стол).
/// </remarks>
public class RobotSelfCollision : MonoBehaviour
{
    [Header("Joint chain (filled by SixAxisController)")]
    public Transform[] jointTransforms;

    [Header("Collision settings")]
    [Tooltip("Отключить столкновения между соседними суставами и известными парами")]
    public bool disableSelfCollisions = true;

    [Tooltip("Радиус цилиндров для коллайдеров суставов")]
    public float jointColliderRadius = 0.08f;

    [Tooltip("Слой для коллайдеров робота (обязательно отличный от стола и стен)")]
    public int robotColliderLayer = 15; // "Robot" по умолчанию

    private readonly List<GameObject> spawnedColliders = new List<GameObject>();

    /// <summary>Сколько коллайдеров уже построено (для диагностики и проверки «луч упрётся»).</summary>
    public int ColliderCount { get { return spawnedColliders.Count; } }

    /// <summary>
    /// СОБРАТЬ КОЛЛАЙДЕРЫ ПРЯМО СЕЙЧАС (добавлено 02.11.2026, пункт 2 владельца).
    ///
    /// ЗАЧЕМ ОТДЕЛЬНЫЙ ПУБЛИЧНЫЙ МЕТОД. Компонент добавляется в РАНТАЙМЕ из
    /// `SixAxisController.SetupSelfCollision`, и раньше коллайдеры строились только в `Start()` —
    /// то есть в конце кадра добавления. Из-за этого:
    ///   • луч, посчитанный в том же кадре, проходил сквозь робота;
    ///   • если к моменту `Start()` `jointTransforms` оказывался пуст, компонент тихо выходил
    ///     с одним предупреждением, и робот оставался БЕЗ коллайдеров НАВСЕГДА.
    ///
    /// Проверка журнала шлема 02.11.2026 подтвердила худший вариант: строк `Collider_*` не было
    /// вовсе, а лазер упирался только в стену ангара («Hangar_part_02» на 6,57 м), пролетая мимо
    /// стола и робота.
    ///
    /// ИДЕМПОТЕНТЕН: повторный вызов ничего не дублирует (проверка по списку и по имени).
    /// </summary>
    public void BuildNow()
    {
        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            // ЗАПАСНОЙ ПУТЬ (02.11.2026): даже без суставов робот обязан ловить луч.
            // Собираем коллайдеры по ГАБАРИТАМ ГЕОМЕТРИИ — так луч упрётся в модель,
            // даже если `jointTransforms` не заполнен.
            BuildFromRenderers();
            return;
        }

        SetupColliders();
        SetupCollisionMatrix();

        Debug.Log("[RobotSelfCollision] коллайдеры робота построены: " + spawnedColliders.Count +
                  " шт · радиус " + jointColliderRadius.ToString("0.00") +
                  " м · слой " + robotColliderLayer +
                  (spawnedColliders.Count == 0 ? "  ⚠ НИ ОДНОГО (луч пройдёт сквозь)" : ""));

        // Если по суставам ничего не вышло (все null) — всё равно ловим луч габаритами.
        if (spawnedColliders.Count == 0) BuildFromRenderers();
    }

    /// <summary>
    /// КОЛЛАЙДЕРЫ ПО ГАБАРИТАМ ГЕОМЕТРИИ — запасной путь, когда суставы недоступны
    /// (добавлено 02.11.2026, пункт 2 владельца: «лазеры … не упираются в роботов»).
    ///
    /// ПОЧЕМУ ПОНАДОБИЛОСЬ. Коллайдеры по суставам — точные, но зависят от заполнения
    /// `jointTransforms`. В шлеме 02.11.2026 журнал показал, что коллайдеров НЕТ вовсе: луч
    /// пролетал мимо робота и упирался только в стену ангара на 6,57 м.
    ///
    /// ЧТО ДЕЛАЕТ. Обходит все `Renderer` робота, берёт их мировые габариты и ставит на каждое
    /// звено ОРИЕНТИРОВАННЫЙ BoxCollider. Луч по нему попадает всегда — это то, что нужно глазу
    /// оператора. Для физики расталкивания эти боксы не используются (они кинематические).
    ///
    /// СКОЛЬКО СТАВИТЬ. По одному на каждый видимый меш — дорого (у робота десятки мешей).
    /// Поэтому берём КРУПНЫЕ меши (объём больше порога), они и формируют силуэт.
    /// </summary>
    public void BuildFromRenderers()
    {
        if (spawnedColliders.Count > 0) return;

        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            Debug.LogWarning("[RobotSelfCollision] BuildFromRenderers: у робота нет Renderer — " +
                             "коллайдеров не будет, луч пройдёт сквозь.");
            return;
        }

        int made = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null) continue;

            Bounds b = r.bounds;
            Vector3 size = b.size;
            // Мелкие детали пропускаем: их десятки, а силуэт они не формируют.
            if (size.x < 0.03f && size.y < 0.03f && size.z < 0.03f) continue;

            string holderName = "RayCollider_" + r.name;
            if (transform.Find(holderName) != null) continue;   // идемпотентность

            var holder = new GameObject(holderName);
            holder.transform.SetParent(transform, false);
            holder.layer = robotColliderLayer;

            BoxCollider box = holder.AddComponent<BoxCollider>();
            // Габариты меша заданы в МИРОВЫХ осях — переводим в локальные оси держателя.
            holder.transform.position = b.center;
            holder.transform.rotation = Quaternion.identity;
            box.size = size;
            box.isTrigger = false;

            Rigidbody rb = holder.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            spawnedColliders.Add(holder);
            made++;
        }

        Debug.Log("[RobotSelfCollision] коллайдеры по габаритам построены: " + made + " шт" +
                  (made == 0 ? "  ⚠ НИ ОДНОГО (луч пройдёт сквозь)" : " · слой " + robotColliderLayer));
    }

    private void Start()
    {
        if (jointTransforms == null || jointTransforms.Length == 0)
        {
            Debug.LogWarning("[RobotSelfCollision] jointTransforms is null or empty. Skipping collider setup.");
            return;
        }

        // Если BuildNow уже вызвали извне (рантайм-добавление) — второй раз не строим.
        if (spawnedColliders.Count > 0) return;

        SetupColliders();
        SetupCollisionMatrix();
    }

    private void SetupColliders()
    {
        if (jointTransforms == null || jointTransforms.Length == 0)
            return;

        for (int i = 0; i < jointTransforms.Length; i++)
        {
            if (jointTransforms[i] == null)
                continue;

            // ИДЕМПОТЕНТНОСТЬ (02.11.2026): при повторном вызове не плодим вторые коллайдеры.
            // Без этой проверки `BuildNow()` + `Start()` дали бы ДВА коллайдера на сустав.
            string holderName = "Collider_" + jointTransforms[i].name;
            if (jointTransforms[i].Find(holderName) != null) continue;

            GameObject holder = new GameObject(holderName);
            holder.transform.SetParent(jointTransforms[i], false);
            holder.layer = robotColliderLayer;

            CapsuleCollider col = holder.AddComponent<CapsuleCollider>();
            col.center = Vector3.zero;
            col.radius = jointColliderRadius;
            col.height = jointColliderRadius * 2f;
            col.direction = 2; // Y-axis

            // ТРИГГЕР: коллайдер нужен ЛУЧУ (попадание), а не физическому расталкиванию.
            // Иначе кинематическое тело сустава участвовало бы в столкновениях и мешало
            // собственному движению робота.
            col.isTrigger = false;

            Rigidbody rb = holder.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            spawnedColliders.Add(holder);
        }

        // Не добавляем коллайдер на контейнер робота — используем только коллайдеры суставов
    // для избежания конфликтов с существующими коллайдерами FBX
    }

    private void SetupCollisionMatrix()
    {
        if (!disableSelfCollisions || jointTransforms == null || jointTransforms.Length == 0)
            return;

        // Список пар, между которыми нужно отключить столкновения
        HashSet<(int, int)> ignoredPairs = GetIgnoredPairs();

        Collider[] allColliders = GetComponentsInChildren<Collider>();
        Dictionary<Transform, Collider> transformToCollider = new Dictionary<Transform, Collider>();

        foreach (var col in allColliders)
        {
            Transform parentJoint = FindParentJoint(col.transform);
            if (parentJoint != null)
                transformToCollider[parentJoint] = col;
        }

        var jointList = new List<Transform>(jointTransforms);
        for (int i = 0; i < jointList.Count; i++)
        {
            for (int j = i + 1; j < jointList.Count; j++)
            {
                if (ignoredPairs.Contains((i, j)))
                {
                    if (transformToCollider.TryGetValue(jointList[i], out Collider colA) &&
                        transformToCollider.TryGetValue(jointList[j], out Collider colB))
                    {
                        Physics.IgnoreCollision(colA, colB, true);
                    }
                }
            }
        }

        // Отключаем столкновения между родительским и дочерним суставами (всегда)
        for (int i = 0; i < allColliders.Length; i++)
        {
            for (int j = i + 1; j < allColliders.Length; j++)
            {
                Transform tA = allColliders[i].transform;
                Transform tB = allColliders[j].transform;

                if (IsParentOrChildOf(tA, tB) || IsParentOrChildOf(tB, tA))
                {
                    Physics.IgnoreCollision(allColliders[i], allColliders[j], true);
                }
            }
        }
    }

    private HashSet<(int, int)> GetIgnoredPairs()
    {
        var pairs = new HashSet<(int, int)>();

        // Соседние суставы J1-J2, J2-J3, J3-J4, J4-J5, J5-J6
        for (int i = 0; i < 5; i++)
            pairs.Add((i, i + 1));

        // Дальние звенья: J1-J3 (основание + первая часть манипулятора)
        pairs.Add((0, 2));

        // J2-J4 могут перекрываться при больших углах
        pairs.Add((1, 3));

        // J3-J5
        pairs.Add((2, 4));

        return pairs;
    }

    private Transform FindParentJoint(Transform t)
    {
        while (t != null && t != transform)
        {
            foreach (var jt in jointTransforms)
            {
                if (jt == t)
                    return t;
            }
            t = t.parent;
        }
        return transform;
    }

    private static bool IsParentOrChildOf(Transform child, Transform parent)
    {
        Transform current = child;
        while (current != null)
        {
            if (current == parent)
                return true;
            current = current.parent;
        }
        return false;
    }

    private void OnDestroy()
    {
        foreach (var obj in spawnedColliders)
        {
            if (obj != null) Destroy(obj);
        }
    }
}
