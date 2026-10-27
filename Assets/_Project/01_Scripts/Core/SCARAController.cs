using UnityEngine;

/// <summary>
/// Контроллер SCARA-робота LS10-B702S на базе честной DH-кинематики.
///
/// Кинематическая схема SCARA (DH: две вращательные оси Z1||Z2 — вертикали,
/// затем призматическая ось Z3 по вертикали):
///   * Прямая задача — параметры звеньев (a1,a2) измеряются автоматически
///     из геометрии (горизонтальные проекции отрезков J1→J2 и J2→z_5);
///   * Обратная задача — АНАЛИТИЧЕСКОЕ планарное решение 2R (закон косинусов):
///     угол локтя выбирается по ближайшей ветви к текущей позе, далее доворот
///     базы — ровно в целевую горизонтальную проекцию точки;
///   * Призматическая ось Z — плавный вертикальный ход z_5 (ZMin..ZMax),
///     как и раньше.
/// Вращения выполняются строго вокруг вертикали (ось Z DH), поэтому робот
/// не «выкручивается» в 3D — только по своей реальной кинематике.
/// </summary>
public class SCARAController : RobotController
{
    [Header("LS10 planar joints")]
    public Transform joint1;   // LS10-B702S_J1_3
    public Transform joint2;   // LS10-B702S_J2_4
    public Transform joint3;   // LS10-B702S_z_5 — призматическая ось (вертикальный ход)
    public Transform baseTransform;

    [SerializeField] private string baseName = "LS10-B702S_base_1";
    [SerializeField] private string joint1Name = "LS10-B702S_J1_3";
    [SerializeField] private string joint2Name = "LS10-B702S_J2_4";
    [SerializeField] private string joint3Name = "LS10-B702S_z_5";

    [Header("Vertical travel (Z prismatic)")]
    [Tooltip("Максимальное ОПУСКАНИЕ z_5 от стартовой высоты (не даёт «нырять» под базу)")]
    public float ZMin = -0.06f;
    [Tooltip("Максимальный ПОДЪЁМ z_5 от стартовой высоты")]
    public float ZMax = 0.18f;

    [Header("Нижняя граница призмы (ФИКС 6)")]
    [Tooltip("Сдвиг НИЖНЕЙ границы хода призмы ВНИЗ, метры (0…0.10). По умолчанию 0.02: низ " +
             "стержня может опуститься на 2 см ниже уровня столешницы, поэтому точка РОВНО на " +
             "уровне стола снова достижима (без сдвига запас до нижнего предела призмы = 0°, " +
             "а планировщику нужно ≥ 3°). Значение сохраняется в PlayerPrefs и возвращается " +
             "при следующем запуске.")]
    [Range(0f, 0.1f)]
    [SerializeField] private float zLowerOffsetM = 0.02f;

    // --- DH-параметры (замеренные) ---
    private Vector3 verticalAxis;      // ось Z DH (вертикаль базы)
    private float link1Len;            // a1: плечо J1→J2 (горизонтальная проекция)
    private float link2Len;            // a2: предплечье J2→z_5 (горизонтальная проекция)
    private float initialHeight;       // стартовая высота z_5 над базой
    private Vector3 refDir1;           // стартовое направление звена 1 (для замера текущих углов)
    private Vector3 refDir2;           // стартовое направление звена 2
    private bool geometryCached;
    private bool zTravelCalibrated;     // ход Z посчитан РОВНО ОДИН РАЗ (ФИКС 5)
    private bool dhLogged;              // DH уже напечатан (ФИКС 5.8: шум от копий-фантомов)
    private float dhLoggedA1;
    private float dhLoggedA2;
    private bool referencesReported;
    private float lastAngle1;
    private float lastAngle2;

    /// <summary>Ключ PlayerPrefs: сдвиг нижней границы призмы вниз, метры (ФИКС 6).</summary>
    public const string ZLowerOffsetPrefsKey = "KazistovVv.Scara.ZLowerOffsetM";

    /// <summary>Сдвиг нижней границы призмы вниз, метры (ФИКС 6).</summary>
    public float ZLowerOffsetM { get { return zLowerOffsetM; } }

    /// <summary>Ход Z уже посчитан по геометрии (повторно — только по явному запросу).</summary>
    public bool KinematicsCalibrated { get { return zTravelCalibrated; } }

    protected override void Awake()
    {
        base.Awake();

        // ФИКС 5. СЛУЖЕБНАЯ КОПИЯ МОДЕЛИ (фантом) — НЕ калибрует геометрию.
        // PhantomManager.CreateGhost создаёт копию через Instantiate, поэтому Awake
        // копии успевает отработать ДО того, как копию выключат и снимут с неё скрипты.
        // Раньше каждая копия считала ход Z по СВОИМ Renderer.bounds (они зависят от
        // текущей позы звеньев) и писала 3 строки в консоль — отсюда 165 строк за прогон.
        // Копия наследует уже откалиброванные ZMin/ZMax робота, поэтому ей пересчёт не нужен.
        if (IsServiceCopy)
        {
            geometryCached = true;
            zTravelCalibrated = true;
            return;
        }

        zLowerOffsetM = LoadZLowerOffset();
        ResolveJoints();
        CacheGeometry();
        CalibrateZTravelOnce();
        EnsureCableFollow();
    }

    /// <summary>
    /// Служебная копия модели (фантом). Признак — суффикс «(Clone)»: `Instantiate` даёт его
    /// ДО вызова Awake, а HideInHierarchy копии выставляется уже ПОСЛЕ (см. CreateGhost).
    /// Дополнительно страхуемся флагом HideInHierarchy — у объектов сцены его нет.
    /// </summary>
    private bool IsServiceCopy
    {
        get
        {
            if (name.EndsWith("(Clone)")) return true;
            return (gameObject.hideFlags & HideFlags.HideInHierarchy) != 0;
        }
    }

    /// <summary>
    /// ФИКС 5: ход Z считается ОДИН РАЗ при инициализации робота и запоминается.
    /// Повторный расчёт — только явным запросом оператора
    /// (<see cref="RecalculateKinematics"/> / команда «Пересчитать кинематику SCARA»).
    /// </summary>
    private void CalibrateZTravelOnce()
    {
        if (zTravelCalibrated) return;
        if (!geometryCached) return;      // ссылки на суставы ещё не разрешились — считать нечем
        zTravelCalibrated = true;
        ApplyZTravelFromGeometry();
    }

    /// <summary>
    /// ФИКС 5/6: явный запрос «Пересчитать кинематику SCARA». Заново снимает геометрию
    /// (длины звеньев, стартовую высоту z_5) и заново считает ход Z — например, после
    /// ручной правки модели или изменения <see cref="ZLowerOffsetM"/>.
    /// Сама кинематика (DH, IK, планировщик) не меняется: пересчитываются только пределы хода.
    /// </summary>
    public void RecalculateKinematics()
    {
        ResolveJoints();
        geometryCached = false;
        CacheGeometry();
        zTravelCalibrated = false;
        CalibrateZTravelOnce();
    }

    /// <summary>
    /// ФИКС 6: сдвиг нижней границы призмы вниз, метры (0…0.10). Сохраняется в PlayerPrefs.
    /// Возвращает true, если значение изменилось. После изменения ход Z пересчитывается.
    /// </summary>
    public bool SetZLowerOffsetM(float value)
    {
        float clamped = Mathf.Clamp(value, 0f, 0.1f);
        if (Mathf.Abs(clamped - zLowerOffsetM) < 1e-5f) return false;
        zLowerOffsetM = clamped;
        PlayerPrefs.SetFloat(ZLowerOffsetPrefsKey, zLowerOffsetM);
        PlayerPrefs.Save();
        RecalculateKinematics();
        Debug.Log("[SCARA] нижняя граница призмы сдвинута вниз на " +
                  (zLowerOffsetM * 100f).ToString("0.#") + " см · ZMin = " +
                  ZMin.ToString("0.000") + " м (сохранено в PlayerPrefs)");
        return true;
    }

    /// <summary>Сдвиг нижней границы призмы из PlayerPrefs (иначе — значение из инспектора).</summary>
    private float LoadZLowerOffset()
    {
        if (!PlayerPrefs.HasKey(ZLowerOffsetPrefsKey)) return zLowerOffsetM;
        return Mathf.Clamp(PlayerPrefs.GetFloat(ZLowerOffsetPrefsKey, zLowerOffsetM), 0f, 0.1f);
    }

    /// <summary>
    /// Автоматически подключает кабель (LS10-B702S_cable_2). По фото реальных
    /// SCARA: шланг входит в ОТВЕРСТИЕ НАД началом координат LS10-B702S_J2_4 —
    /// точка крепления = (0, ~0.2, 0) в локальных осях J2_4 (крышка корпуса локтя).
    /// </summary>
    private void EnsureCableFollow()
    {
        Transform cable = FindChild(new string[] { "LS10-B702S_cable_2", "cable_2" });
        if (cable == null) return;
        if (cable.GetComponent<KazistovVvUI.ScaraCableFollow>() != null) return;

        var follow = cable.gameObject.AddComponent<KazistovVvUI.ScaraCableFollow>();
        follow.baseAnchor = baseTransform != null ? baseTransform : transform;
        follow.followTarget = joint2; // LS10-B702S_J2_4
        follow.cable = cable;
        // Отверстие для шланга — НАД началом координат J2_4 (на крышке корпуса).
        follow.targetAttachLocal = new Vector3(0f, 0.2f, 0f);
        Debug.Log("[SCARA] Шланг закреплён в отверстии над осью J2_4: " +
                  follow.targetAttachLocal);
    }

    private void Update()
    {
        MoveToTarget(Time.deltaTime);
        UpdateTelemetry(Time.deltaTime);
    }

    public override void SetTarget(Vector3 position)
    {
        targetPosition = position;
        hasTarget = true;
    }

    public override void SetTarget(Vector3 position, Quaternion rotation)
    {
        // SCARA задаётся точкой; ориентация фланца не управляется (ось Z фиксирована).
        SetTarget(position);
        targetRotation = rotation;
    }

    public override void MoveToTarget(float deltaTime)
    {
        if (!hasTarget)
        {
            return;
        }

        ResolveJoints();
        Transform resolvedBase = baseTransform;
        Transform resolvedJoint1 = joint1;
        Transform resolvedJoint2 = joint2;
        Transform resolvedJoint3 = joint3;

        if (resolvedBase == null || resolvedJoint1 == null || resolvedJoint2 == null || resolvedJoint3 == null)
        {
            if (!referencesReported)
            {
                Debug.LogError(
                    "[" + name + "] SCARA references missing. Assign base, J1, J2 and Z joints in the Inspector.",
                    this);
                referencesReported = true;
            }
            return;
        }

        if (!geometryCached)
        {
            CacheGeometry();
        }

        Vector3 axis = verticalAxis.sqrMagnitude > 0.001f ? verticalAxis : resolvedBase.up;
        Vector3 basePos = resolvedBase.position;
        float settingsSpeed = SettingsData.Instance != null ? SettingsData.Instance.robotSpeed : 1f;
        float speedScale = Mathf.Max(0.05f, maxSpeed) * Mathf.Max(0.05f, movementSpeedScale) *
                           Mathf.Max(0.05f, settingsSpeed);
        // Плавность: экспоненциальный шаг к аналитическому решению.
        float step = 1f - Mathf.Exp(-deltaTime * speedScale * 5f);
        step = Mathf.Clamp01(step);

        // Планарная часть: приводим проекцию точки z_5 к проекции цели.
        Vector3 p1 = ProjectToPlane(resolvedJoint1.position, basePos, axis);
        Vector3 p2 = ProjectToPlane(resolvedJoint2.position, basePos, axis);
        Vector3 p3 = ProjectToPlane(resolvedJoint3.position, basePos, axis);
        Vector3 pTarget = ProjectToPlane(targetPosition, basePos, axis);
        Vector3 pT = p1 + (pTarget - p1).normalized * Mathf.Min(
            Vector3.Distance(pTarget, p1), link1Len + link2Len - 0.002f);

        SolvePlanar2R(p1, p2, p3, pT, axis, step);

        // Призматическая ось: вертикальный ход z_5.
        float currentHeight = Vector3.Dot(resolvedJoint3.position - basePos, axis);
        float targetHeight = Vector3.Dot(targetPosition - basePos, axis);
        float desiredHeight = Mathf.Clamp(targetHeight, initialHeight + ZMin, initialHeight + ZMax);
        float heightOffset = Mathf.Clamp(desiredHeight - currentHeight, -ZMax, ZMax);
        Vector3 desiredZPosition = resolvedJoint3.position + axis * heightOffset;

        Transform zParent = resolvedJoint3.parent;
        if (zParent != null)
        {
            float zStep = 1f - Mathf.Exp(-deltaTime * speedScale * 6f);
            resolvedJoint3.localPosition = Vector3.Lerp(
                resolvedJoint3.localPosition,
                zParent.InverseTransformPoint(desiredZPosition),
                Mathf.Clamp01(zStep));
        }
    }

    /// <summary>
    /// Аналитическое решение плоской 2R-кинематики (DH a1, a2):
    /// q2 (угол локтя) — по закону косинусов, ветвь выбирается ближайшей к текущей;
    /// q1 (база) — доворот так, чтобы точка z_5 встала над целью.
    /// Углы суставов здесь — ПРИРАЩЕНИЯ от текущей позы (DeltaAngle), поэтому
    /// солвер корректен из любого положения, а не только из «нулевого».
    /// </summary>
    private void SolvePlanar2R(Vector3 p1, Vector3 p2, Vector3 p3, Vector3 pT, Vector3 axis, float step)
    {
        // Текущие «абсолютные» углы звеньев (от стартовых направлений) и угол локтя.
        float angle1 = MeasureAngle(refDir1, p2 - p1, axis);
        float angle2 = MeasureAngle(refDir2, p3 - p2, axis);
        float bend = Mathf.DeltaAngle(angle1, angle2); // текущий угол локтя, ±
        lastAngle1 = angle1;
        lastAngle2 = bend;

        float r = Mathf.Clamp(Vector3.Distance(pT, p1), 0.001f, link1Len + link2Len - 0.002f);
        float cosQ2 = Mathf.Clamp(
            (r * r - link1Len * link1Len - link2Len * link2Len) / (2f * link1Len * link2Len),
            -1f, 1f);
        float q2AbsDeg = Mathf.Acos(cosQ2) * Mathf.Rad2Deg; // [0..180°] — модуль нужного угла локтя

        // Две ветви локтя: выбираем ту, что ближе к текущему углу.
        float q2 = Mathf.Abs(bend - q2AbsDeg) <= Mathf.Abs(bend + q2AbsDeg)
            ? q2AbsDeg
            : -q2AbsDeg;

        // Приращение локтя и куда встанет звено 2 после доворота.
        float d2 = Mathf.DeltaAngle(bend, q2);
        Vector3 arm2 = RotateAround(p3 - p2, axis, d2 * Mathf.Deg2Rad);
        Vector3 w = (p2 - p1) + arm2;

        // Доворот базы: совместить (звено1 + новое звено2) с целью.
        float d1 = Vector3.SignedAngle(
            Vector3.ProjectOnPlane(w, axis),
            Vector3.ProjectOnPlane(pT - p1, axis),
            axis);

        if (float.IsNaN(d1)) return;

        // Плавные довороты (частичный шаг каждый кадр — сходится к решению).
        if (joint2 != null && Mathf.Abs(d2) > 0.01f)
            joint2.Rotate(axis, d2 * step, Space.World);
        if (joint1 != null && Mathf.Abs(d1) > 0.01f)
            joint1.Rotate(axis, d1 * step, Space.World);
    }

    private static Vector3 ProjectToPlane(Vector3 point, Vector3 origin, Vector3 normal)
    {
        return origin + Vector3.ProjectOnPlane(point - origin, normal);
    }

    private static float MeasureAngle(Vector3 referenceDir, Vector3 currentDir, Vector3 axis)
    {
        Vector3 a = Vector3.ProjectOnPlane(referenceDir, axis);
        Vector3 b = Vector3.ProjectOnPlane(currentDir, axis);
        if (a.sqrMagnitude < 1e-8f || b.sqrMagnitude < 1e-8f) return 0f;
        return Vector3.SignedAngle(a, b, axis);
    }

    private static Vector3 RotateAround(Vector3 v, Vector3 axis, float radians)
    {
        return Quaternion.AngleAxis(radians * Mathf.Rad2Deg, axis) * v;
    }

    /// <summary>
    /// Вычисляет ход z_5 из геометрии (замер вершин мешей):
    ///   * z_5 (LS10-B702S_z_5): стержень Ø≈40 мм (секция 1.145–1.545) + узкий
    ///     хвостовик Ø≈10 мм снизу; в J2_4 отверстие под стержень Ø≈44 мм
    ///     (узкая часть 1.43–1.45).
    ///   * ВНИЗ: «кончик» (низ стержня) может опускаться до уровня столешницы,
    ///     на которой стоит робот (низ z_5 ≈ уровень базы); верхняя часть при
    ///     этом уходит в отверстие корпуса J2_4 — это нормально (отверстие шире).
    ///     ФИКС 6: дополнительно нижняя граница сдвигается вниз на `zLowerOffsetM`,
    ///     поэтому точка РОВНО на уровне стола перестаёт быть «в пределе» (запас 0° &lt; 3°).
    ///   * ВВЕРХ: низ стержня не должен прятаться в корпусе J2_4 (ход ограничен
    ///     нижней плоскостью корпуса).
    ///
    /// ФИКС 5. Замер ведётся ОТНОСИТЕЛЬНО собственного трансформа деталей и стартовой
    /// высоты z_5, а не «как есть» по мировым `Renderer.bounds`: мировой AABB стержня
    /// едет вместе с призмой и поворачивается вместе с J1/J2, из-за чего один и тот же
    /// робот в разных позах давал разные пределы (в прогоне ZMax менялся 0.043 → 0.182
    /// → 0.127). Теперь результат зависит только от геометрии модели, поэтому повторный
    /// расчёт даёт ТУ ЖЕ величину и в консоль ничего не пишет.
    /// </summary>
    private void ApplyZTravelFromGeometry()
    {
        if (joint3 == null || joint2 == null) return;

        Renderer rod = joint3.GetComponentInChildren<Renderer>(true);
        Renderer housing = joint2.GetComponent<Renderer>();
        if (rod == null || housing == null)
        {
            // Резерв: старые широкие лимиты сжимаем (z_5 не должна глубоко «нырять»).
            if (ZMin < -0.1f) ZMin = -0.06f;
            if (ZMax > 0.25f) ZMax = 0.18f;
            return;
        }

        Vector3 axis = verticalAxis.sqrMagnitude > 0.001f ? verticalAxis : Vector3.up;
        Vector3 basePos = baseTransform != null ? baseTransform.position : transform.position;

        // Низ/верх стержня — в СТАРТОВОЙ высоте z_5: из мирового AABB вычитается
        // собственный трансформ детали (он едет вместе с призмой), затем прибавляется
        // стартовая высота. Разность «AABB − трансформ» от позы не зависит.
        float rodBottom = initialHeight + Vector3.Dot(rod.bounds.min - rod.transform.position, axis);
        float rodTop = initialHeight + Vector3.Dot(rod.bounds.max - rod.transform.position, axis);
        // Корпус локтя вращается только вокруг вертикали, поэтому его мировые границы
        // по высоте от позы не зависят.
        float housingBottom = Vector3.Dot(housing.bounds.min - basePos, axis);
        float housingRoof = Vector3.Dot(housing.bounds.max - basePos, axis);

        // ВНИЗ: низ стержня опускается до уровня базы (столешницы) + сдвиг ФИКС 6.
        float maxDown = Mathf.Max(0.02f, rodBottom - 0.002f) + zLowerOffsetM;
        float physicalMin = -maxDown;
        if (Mathf.Abs(ZMin - physicalMin) > 0.002f)
        {
            Debug.Log("[SCARA] Ход Z вниз: " + ZMin.ToString("0.000") + " -> " +
                      physicalMin.ToString("0.000") +
                      " (низ z_5 " + rodBottom.ToString("0.000") + " м над базой, верх " +
                      rodTop.ToString("0.000") + " м; сдвиг границы вниз " +
                      (zLowerOffsetM * 100f).ToString("0.#") + " см)");
            ZMin = physicalMin;
        }

        // ВВЕРХ: низ стержня упирается в нижнюю плоскость корпуса J2_4.
        float maxUp = Mathf.Clamp(housingBottom - rodBottom - 0.005f, 0.01f, 0.2f);
        if (Mathf.Abs(ZMax - maxUp) > 0.002f)
        {
            Debug.Log("[SCARA] Ход Z вверх: " + ZMax.ToString("0.000") + " -> " +
                      maxUp.ToString("0.000") + " (низ стержня не прячется в корпусе: низ " +
                      housingBottom.ToString("0.000") + " м, верх корпуса " +
                      housingRoof.ToString("0.000") + " м)");
            ZMax = maxUp;
        }
    }

    private void ResolveJoints()
    {        if (baseTransform == null)
        {
            baseTransform = FindChild(new string[] { baseName });
        }

        if (baseTransform == null)
        {
            baseTransform = transform;
        }

        if (joint1 == null)
        {
            joint1 = FindChild(new string[] { joint1Name, "J1" });
        }
        if (joint2 == null)
        {
            joint2 = FindChild(new string[] { joint2Name, "J2" });
        }
        if (joint3 == null)
        {
            joint3 = FindChild(new string[] { joint3Name, "_z_", "z_5" });
        }

        if (verticalAxis.sqrMagnitude < 0.001f && baseTransform != null)
        {
            verticalAxis = baseTransform.up;
        }
    }

    private void CacheGeometry()
    {
        if (baseTransform == null || joint1 == null || joint2 == null || joint3 == null)
        {
            return;
        }

        if (verticalAxis.sqrMagnitude < 0.001f)
        {
            verticalAxis = baseTransform.up;
        }

        Vector3 basePos = baseTransform.position;
        Vector3 p1 = ProjectToPlane(joint1.position, basePos, verticalAxis);
        Vector3 p2 = ProjectToPlane(joint2.position, basePos, verticalAxis);
        Vector3 p3 = ProjectToPlane(joint3.position, basePos, verticalAxis);

        link1Len = Vector3.Distance(p1, p2);
        link2Len = Vector3.Distance(p2, p3);
        refDir1 = (p2 - p1).normalized;
        refDir2 = (p3 - p2).normalized;
        initialHeight = Vector3.Dot(joint3.position - basePos, verticalAxis);
        geometryCached = true;

        // ФИКС 5: расчёт хода Z из этого метода УБРАН. Раньше он вызывался здесь, а этот
        // метод дёргался из Awake КАЖДОЙ копии-фантома (и повторно из MoveToTarget, если
        // геометрия не снялась) — отсюда 165 строк «Ход Z …» за один прогон.
        // Теперь ход Z считается РОВНО ОДИН РАЗ: SCARAController.Awake → CalibrateZTravelOnce,
        // а повторно — только явным запросом (RecalculateKinematics / команда оператора).

        // ФИКС 5.8 (шум): строка DH печаталась каждой копией-фантомом. Логируем только
        // при ИЗМЕНЕНИИ посчитанных параметров — кэшируем по позе.
        bool changed = !dhLogged ||
            Mathf.Abs(link1Len - dhLoggedA1) > 0.001f ||
            Mathf.Abs(link2Len - dhLoggedA2) > 0.001f;
        if (changed)
        {
            dhLogged = true;
            dhLoggedA1 = link1Len;
            dhLoggedA2 = link2Len;
            Debug.Log("[SCARA] DH: a1=" + link1Len.ToString("0.000") + " a2=" +
                      link2Len.ToString("0.000") + ", вертикаль=" +
                      verticalAxis.ToString("0.00"));
        }
    }

    /// <summary>
    /// Ищет потомка, имя которого целиком совпадает с одним из переданных
    /// вариантов либо содержит его (без учёта регистра).
    /// </summary>
    private Transform FindChild(string[] names)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            foreach (string objectName in names)
            {
                if (objectName != null &&
                    (child.name == objectName || ContainsIgnoreCase(child.name, objectName)))
                {
                    return child;
                }
            }
        }

        return null;
    }

    private static bool ContainsIgnoreCase(string text, string substring)
    {
        return text.ToLower().IndexOf(substring.ToLower()) >= 0;
    }

    public override float[] GetJointAngles()
    {
        return new float[]
        {
            lastAngle1,
            lastAngle2,
            joint3 != null ? joint3.localPosition.y : 0f
        };
    }
}
