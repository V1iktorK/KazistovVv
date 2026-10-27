using UnityEngine;
using UnityEngine.InputSystem;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ПЕРЕМЕЩЕНИЕ ОПЕРАТОРА В ВИРТУАЛЬНОМ ПРОСТРАНСТВЕ (решение владельца [D2], этап 2 плана XR).
    ///
    /// ДВА СПОСОБА, ОБА — ПО РЕШЕНИЮ ВЛАДЕЛЬЦА:
    ///   • ТЕЛЕПОРТ (по умолчанию) — прицел из контроллера, подтверждение курком, мгновенный
    ///     перенос на выбранное место. Не укачивает: это главная причина, почему телепорт
    ///     выбран основным для человека, который впервые надел шлем.
    ///   • ПЛАВНЫЙ ХОД (включается в настройках) — непрерывное движение по стику.
    ///
    /// ЧТО ЗДЕСЬ ГЛАВНЫЙ РИСК. Плавный ход в VR — источник «укачивания»: движения не
    /// совпадают с тем, что чувствует вестибулярный аппарат. Поэтому скорость жёстко
    /// ограничена (0,5…3 м/с), а режим по умолчанию — телепорт. Это не перестраховка,
    /// а прямое следствие решения [D2].
    ///
    /// ПОВОРОТ. Оператор поворачивается «шагами» по 30° правым стиком: так делают почти все
    /// шлемы, потому что плавный поворот укачивает сильнее всего остального. Поворот идёт
    /// вокруг ГОЛОВЫ, а не вокруг центра рига — иначе оператора «уводит» вбок.
    ///
    /// Компонент вешается на корень XR-рига (на нём же — камера шлема).
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrLocomotion : MonoBehaviour
    {
        /// <summary>Шаг поворота вокруг головы, градусы.</summary>
        public float snapTurnDegrees = 30f;

        /// <summary>Порог стика для поворота (защита от случайных касаний).</summary>
        public float snapTurnThreshold = 0.7f;

        /// <summary>Пауза между шагами поворота, секунды.</summary>
        public float snapTurnCooldown = 0.25f;

        /// <summary>Стик для движения/телепорта: true — правый, false — левый (по умолчанию левый).</summary>
        public bool useRightStickForMove = false;

        /// <summary>Порог нажатия курка для телепорта/подтверждения.</summary>
        public float triggerThreshold = 0.5f;

        /// <summary>Слои, по которым разрешён телепорт. Пусто — все, кроме слоя самого рига.</summary>
        public LayerMask groundMask = ~0;

        /// <summary>Максимальный наклон поверхности для телепорта, градусы (на стену не телепортируемся).</summary>
        public float maxSlopeDegrees = 45f;

        /// <summary>Цвет метки телепорта, когда место подходит.</summary>
        public Color validMarkerColor = new Color(0.20f, 0.85f, 0.35f, 0.75f);

        /// <summary>Цвет метки телепорта, когда место НЕ подходит (склон, ничего не найдено).</summary>
        public Color invalidMarkerColor = new Color(0.90f, 0.25f, 0.20f, 0.60f);

        private Transform rigRoot;
        private Transform head;
        private KvXrUiPointer pointer;
        private float lastSnapTurnTime = -10f;
        private bool triggerWasDown;
        private GameObject teleportMarker;
        private bool teleportValid;
        private Vector3 teleportPoint;

        /// <summary>Прицел телепорта сейчас наведён на допустимое место.</summary>
        public bool TeleportTargetValid { get { return teleportValid; } }

        /// <summary>Точка, куда уйдёт оператор при подтверждении (мировые координаты).</summary>
        public Vector3 TeleportPoint { get { return teleportPoint; } }

        /// <summary>Сколько телепортов выполнено — для журнала и диагностики.</summary>
        public int TeleportCount { get; private set; }

        /// <summary>Сколько шагов поворота выполнено.</summary>
        public int SnapTurnCount { get; private set; }

        /// <summary>Сколько секунд оператор двигался плавным ходом — метрика «укачивания».</summary>
        public float SmoothMovingSeconds { get; private set; }

        void Awake()
        {
            rigRoot = transform;
        }

        void OnEnable()
        {
            KvXrSettingsChanged.SettingsChanged += OnSettingsChanged;
        }

        void OnDisable()
        {
            KvXrSettingsChanged.SettingsChanged -= OnSettingsChanged;
            if (teleportMarker != null) teleportMarker.SetActive(false);
        }

        private void OnSettingsChanged()
        {
            // Смена способа перемещения в настройках должна убирать метку телепорта немедленно,
            // иначе она остаётся висеть в воздухе при плавном ходе — выглядит как дефект.
            if (KvXrSettings.GetMoveMode() != KvXrMoveMode.Teleport && teleportMarker != null)
                teleportMarker.SetActive(false);
        }

        void Update()
        {
            if (rigRoot == null) rigRoot = transform;
            if (head == null) head = FindHead();

            // СТИК ЗАНЯТ ВВОДОМ (§41). Пока открыто колесо символов, стик принадлежит ему: иначе
            // выбор символа ещё и разворачивал бы оператора — стик в шлеме это поворот и ход.
            if (StickOwnedByInput()) return;

            Quaternion rigYaw = Quaternion.Euler(0f, rigRoot.eulerAngles.y, 0f);

            HandleSnapTurn();
            if (KvXrSettings.GetMoveMode() == KvXrMoveMode.Teleport)
                HandleTeleport(rigYaw);
            else
                HandleSmooth(rigYaw);
        }

        /// <summary>
        /// ЗАБРАЛ ЛИ КТО-ТО СТИК. Спрашивается у панели ввода, а не решается здесь: правило захвата
        /// одно (`KvXrWheelLayout.CapturesStick`) и проверено тестом.
        /// </summary>
        private static bool StickOwnedByInput()
        {
            KvXrGroupPanel panel = KvXrGroupPanel.Active;
            return panel != null && panel.StickCaptured;
        }

        // ------------------------------------------------------------------ поворот шагами

        private void HandleSnapTurn()
        {
            Vector2 stick = KvXrInputSource.RightStick;
            if (Time.time - lastSnapTurnTime < snapTurnCooldown) return;
            if (Mathf.Abs(stick.x) < snapTurnThreshold) return;

            float dir = stick.x > 0f ? 1f : -1f;
            Vector3 pivot = head != null ? head.position : rigRoot.position;
            rigRoot.RotateAround(pivot, Vector3.up, dir * snapTurnDegrees);
            lastSnapTurnTime = Time.time;
            SnapTurnCount++;
        }

        // ------------------------------------------------------------------ телепорт

        private void HandleTeleport(Quaternion rigYaw)
        {
            // ПОКА ЛУЧ НАВЕДЁН НА ПАНЕЛЬ — НЕ ТЕЛЕПОРТИРОВАТЬ (исправлено 23.10.2026).
            //
            // Курок в шлеме один: он и нажимает иконку, и подтверждает телепорт. До этой правки
            // каждое нажатие по панели УВОДИЛО ОПЕРАТОРА с места: корень рига смещался, и всё, что
            // считается от него (кисти, стоящая на месте панель), уезжало. Владелец видел это как
            // «иконки перестали быть прикреплены к контроллеру» и «рук не видно». Правило уже
            // описано в шапке `KvXrUiPointer` («при наведении на интерфейс телепорт молчит») —
            // здесь оно наконец реализовано.
            if (pointer == null) pointer = UnityEngine.Object.FindAnyObjectByType<KvXrUiPointer>();
            if (pointer != null && pointer.AimingAtUi)
            {
                SetMarker(false, Vector3.zero, rigYaw);
                triggerWasDown = KvXrInputSource.RayTrigger >= triggerThreshold;
                return;
            }

            // §37: РУКА ТЕЛЕПОРТА — ТА ЖЕ, ЧТО У ЛУЧА. Иначе при смене руки луча курок одной руки
            // нажимал бы иконку, а курок другой — телепортировал: ровно дефект «одно нажатие — два
            // действия», который в §27 лечили на десктопе.
            bool haveAim = KvXrInputSource.TryRayPose(out Vector3 handPos, out Quaternion handRot);
            if (!haveAim)
            {
                SetMarker(false, Vector3.zero, rigYaw);
                return;
            }

            Vector3 dir = handRot * Vector3.forward;
            float maxDistance = KvXrSettings.GetTeleportDistance();
            teleportValid = AimGroundPoint(handPos, dir, maxDistance,
                out Vector3 ground, out Vector3 normal);

            teleportPoint = ground;
            SetMarker(teleportValid, ground, rigYaw, normal);

            bool triggerDown = KvXrInputSource.RayTrigger >= triggerThreshold;
            if (triggerDown && !triggerWasDown && teleportValid)
                TeleportTo(ground);
            triggerWasDown = triggerDown;
        }

        /// <summary>
        /// Точка на полу под прицелом. Проверяются три вещи: попадание в коллайдер,
        /// наклон поверхности и то, что точка не «под ногами» (иначе оператор телепортируется
        /// в себя и не заметит этого).
        /// </summary>
        public bool AimGroundPoint(Vector3 origin, Vector3 direction, float maxDistance,
            out Vector3 point, out Vector3 normal)
        {
            point = origin + direction.normalized * maxDistance;
            normal = Vector3.up;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, maxDistance,
                    groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                normal = hit.normal;
            }
            else
            {
                // Промах: пробуем плоскость пола на нулевой высоте — оператор часто целится
                // в пустоту между объектами, и «ничего не нашлось» здесь излишне строго.
                Plane floor = new Plane(Vector3.up, Vector3.zero);
                if (!floor.Raycast(new Ray(origin, direction), out float distance)) return false;
                if (distance <= 0f || distance > maxDistance) return false;
                point = origin + direction.normalized * distance;
                normal = Vector3.up;
            }

            float slope = Vector3.Angle(normal, Vector3.up);
            if (slope > maxSlopeDegrees) return false;
            if (Vector3.Distance(point, origin) < 0.25f) return false;   // слишком близко — это «в себя»
            return true;
        }

        /// <summary>Перенести риг так, чтобы точка пола оказалась под головой оператора.</summary>
        public void TeleportTo(Vector3 groundPoint)
        {
            if (rigRoot == null) rigRoot = transform;
            Vector3 headOffset = Vector3.zero;
            if (head != null)
            {
                headOffset = head.position - rigRoot.position;
                headOffset.y = 0f;
            }
            rigRoot.position = new Vector3(groundPoint.x - headOffset.x, rigRoot.position.y,
                groundPoint.z - headOffset.z);
            TeleportCount++;
        }

        // ------------------------------------------------------------------ плавный ход

        private void HandleSmooth(Quaternion rigYaw)
        {
            Vector2 stick = useRightStickForMove ? KvXrInputSource.RightStick : KvXrInputSource.LeftStick;
            Vector2 deadzoned = ApplyDeadzone(stick, 0.15f);
            if (deadzoned.sqrMagnitude < 1e-4f) return;

            // Направление — по взгляду оператора: «вперёд по стику» значит «вперёд по взгляду».
            Vector3 forward = rigYaw * Vector3.forward;
            Vector3 right = rigYaw * Vector3.right;
            Vector3 move = forward * deadzoned.y + right * deadzoned.x;

            float speed = KvXrSettings.GetSmoothSpeed();
            rigRoot.position += move * speed * Time.deltaTime;
            SmoothMovingSeconds += Time.deltaTime;
        }

        /// <summary>Мёртвая зона стика: у шлемовых стиков заметный дребезг в нуле.</summary>
        public static Vector2 ApplyDeadzone(Vector2 stick, float deadzone)
        {
            float magnitude = stick.magnitude;
            if (magnitude <= deadzone) return Vector2.zero;
            float scaled = (magnitude - deadzone) / (1f - deadzone);
            return stick.normalized * Mathf.Clamp01(scaled);
        }

        // ------------------------------------------------------------------ метка телепорта

        private void SetMarker(bool valid, Vector3 point, Quaternion rigYaw, Vector3 normal = default(Vector3))
        {
            if (point == Vector3.zero && !valid)
            {
                if (teleportMarker != null) teleportMarker.SetActive(false);
                return;
            }

            if (teleportMarker == null) teleportMarker = BuildMarker();
            teleportMarker.SetActive(valid);
            teleportMarker.transform.position = point + Vector3.up * 0.01f;
            teleportMarker.transform.rotation = Quaternion.identity;

            var renderer = teleportMarker.GetComponent<Renderer>();
            if (renderer != null && renderer.sharedMaterial != null)
                renderer.sharedMaterial.color = valid ? validMarkerColor : invalidMarkerColor;
        }

        /// <summary>
        /// Метка — плоский круг над полом. Материал берётся у шейдера HDRP/Unlit, а если его
        /// нет (другой пайплайн) — у обычного непрозрачного шейдера: падение из-за материала
        /// в VR выглядит как «телепорт не работает», хотя дело в розовом квадрате.
        /// </summary>
        private GameObject BuildMarker()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "KvXrTeleportMarker";
            go.transform.SetParent(rigRoot, true);
            go.transform.localScale = new Vector3(0.35f, 0.005f, 0.35f);

            Collider col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);

            // Материал метки — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP):
            // жёсткий HDRP-шейдер под URP дал бы в шлеме розовый квадрат вместо метки.
            var renderer = go.GetComponent<Renderer>();
            if (renderer != null)
            {
                Material material = KvRenderPipelineShaders.CreateUnlit("Материал_метки_телепорта", validMarkerColor);
                if (material != null) renderer.material = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            return go;
        }

        private Transform FindHead()
        {
            Camera cam = GetComponentInChildren<Camera>();
            return cam != null ? cam.transform : null;
        }

        /// <summary>Строка состояния для журнала и отчёта.</summary>
        public string Describe()
        {
            return string.Format(
                "XR-перемещение: способ {0} · телепортов {1} · поворотов {2} · плавный ход {3:0.0} с · {4}",
                KvXrSettings.MoveModeLabel(KvXrSettings.GetMoveModeIndex()),
                TeleportCount, SnapTurnCount, SmoothMovingSeconds,
                KvXrInputSource.Describe());
        }
    }
}
