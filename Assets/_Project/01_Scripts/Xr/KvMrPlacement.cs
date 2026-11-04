using UnityEngine;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// «ПОСТАВИТЬ СЮДА»: ЛУЧ → РЕАЛЬНАЯ ПОВЕРХНОСТЬ → РОБОТ (создано 02.11.2026, сессия S45).
    ///
    /// ЧТО ДЕЛАЕТ. В MR показывает луч с маркером: зелёный — под лучом реальная горизонтальная
    /// поверхность (стол), красный — поверхности нет. По A («подтвердить», стандартная кнопка
    /// Quest/PICO) робот переносится на найденную точку и разворачивается лицом к оператору.
    ///
    /// ПОЧЕМУ КНОПКА A, А НЕ КУРОК. Курок в проекте уже несёт пять смыслов (иконка, лазер,
    /// подтверждение точки, перенос окна, колесо) — добавлять шестой значит получить «одно нажатие
    /// два действия» (дефект E-08). A — стандартное «подтвердить» в Quest/PICO, и подтверждение
    /// здесь по смыслу то же самое. Чтобы и это не задвоилось, перехват идёт в ОДНОМ месте —
    /// `KvXrActions.Confirm`: перехватили → обычное подтверждение не выполняется.
    ///
    /// ЧТО ЗНАЧИТ «ПОСТАВИТЬ». Позиция — точка попадания луча в реальную поверхность; поворот —
    /// лицом к оператору; масштаб — 1 (робот в проекте метрический, 1 юнит = 1 м, см. замер в
    /// `Docs/ОТЧЁТ_MR.md`). Между столом и роботом нет ни щели, ни провала: низ робота ставится
    /// ровно в точку поверхности.
    ///
    /// ЧЕСТНО ОБ АВТОПОСТАНОВКЕ. Пока оператор не подтвердил точку, робот ставится САМ на первую
    /// найденную реальную горизонтальную поверхность перед ним (иначе в MR было бы «пусто и
    /// непонятно, что делать»). Это тот же расчёт, что и по A; в журнале печатается источник:
    /// «авто» или «по кнопке A».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvMrPlacement : MonoBehaviour
    {
        /// <summary>Дальность луча постановки, метры.</summary>
        public float maxDistance = 6f;

        /// <summary>Насколько «горизонтальной» должна быть поверхность, чтобы считаться столом.</summary>
        public float minUpNormalDot = 0.60f;

        /// <summary>Через сколько секунд после старта MR искать поверхность для автопостановки.</summary>
        public float autoPlaceDelaySeconds = 3f;

        /// <summary>Как часто повторять попытку автопостановки на реальную поверхность, секунды.</summary>
        public float autoPlaceRetrySeconds = 4f;

        /// <summary>
        /// Через сколько секунд, если реальных поверхностей нет, ставить робота на пол (запасной
        /// путь). 25 секунд, а не 12: меш комнаты на PICO появляется не мгновенно, и поспешный
        /// «пол вместо стола» выглядел бы как дефект постановки.
        /// </summary>
        public float fallbackFloorDelaySeconds = 25f;

        /// <summary>Камера оператора (нужна для направления «лицом к оператору» и запасного луча).</summary>
        public Camera headCamera;

        /// <summary>MR-комната: она знает, кого двигать.</summary>
        public KvXrMrRoom room;

        /// <summary>Текущий экземпляр (нужен перехвату кнопки A).</summary>
        public static KvMrPlacement Active { get; private set; }

        /// <summary>Робот поставлен хотя бы раз.</summary>
        public bool Placed { get; private set; }

        /// <summary>Сколько раз робота ставили (авто и по кнопке).</summary>
        public int PlacementCount { get; private set; }

        /// <summary>Подтверждал ли оператор точку кнопкой A (а не авто).</summary>
        public bool PlacedByOperator { get; private set; }

        /// <summary>Последняя точка постановки и нормаль поверхности — для журнала и отчёта.</summary>
        public Vector3 LastSurfacePoint { get; private set; }
        public Vector3 LastSurfaceNormal { get; private set; }
        public float LastYawDegrees { get; private set; }
        public string LastCause { get; private set; }

        /// <summary>Строка состояния одной строкой.</summary>
        public string Status
        {
            get
            {
                return "постановка: " + (Placed ? "робот поставлен (" + LastCause + ")" : "ждёт реальную поверхность") +
                       " · луч " + (hasHit ? "попадает в реальную поверхность" : "не попадает") +
                       " · по кнопке подтверждено " + (PlacedByOperator ? "да" : "нет");
            }
        }

        private KvMrSurfaces surfaces;
        private Transform mrWorldRoot;
        private GameObject reticle;
        private LineRenderer beam;
        private Renderer reticleRenderer;
        private Material greenMaterial;
        private Material redMaterial;

        /// <summary>
        /// ПОКАЗЫВАТЬ ЛУЧ И МЕТКУ ПРИЦЕЛА В MR. ПО УМОЛЧАНИЮ ВЫКЛЮЧЕНО — решение владельца
        /// 04.11.2026: «зоны включены, какой ужас — выключи все плюшки по умолчанию». Луч и метка
        /// это подсказка, а не рабочий орган: постановка идёт автопутём и кнопкой A, поэтому без них
        /// всё работает. Включается флагом (в инспекторе компонента), если владелец попросит.
        /// </summary>
        public bool showAimByDefault = false;

        private bool hasHit;
        private Vector3 hitPoint;
        private Vector3 hitNormal;
        private float startedAt;
        private float nextAutoTry;
        private bool fallbackTried;
        private bool placedFromFallback;
        private bool warnedNoSurface;
        private bool aWasDown;

        /// <summary>Создать компонент в рантайме (сцена не правится — её SHA256 закреплён тестом).</summary>
        public static KvMrPlacement Ensure(Transform parent, Camera camera, KvXrMrRoom room)
        {
            KvMrPlacement existing = null;
            if (parent != null) existing = parent.GetComponentInChildren<KvMrPlacement>(true);
            if (existing != null) return existing;

            var go = new GameObject("KvMrPlacement");
            if (parent != null) go.transform.SetParent(parent, false);
            KvMrPlacement placement = go.AddComponent<KvMrPlacement>();
            placement.headCamera = camera;
            placement.room = room;
            return placement;
        }

        /// <summary>
        /// ПЕРЕХВАТ ПОДТВЕРЖДЕНИЯ (A). Вызывается из `KvXrActions.Confirm`.
        /// Возвращает true, если нажатие израсходовано постановкой и обычное действие выполнять нельзя.
        /// </summary>
        public static bool TryConsumeConfirm()
        {
            KvMrPlacement active = Active;
            if (active == null || active.room == null || !active.room.MrActive) return false;

            // Пока робот не поставлен оператором — кнопка A принадлежит постановке.
            if (active.PlacedByOperator) return false;

            // В интерфейсе A — «подтвердить» для меню/окон; отбирать его там нельзя.
            if (KvXrStartMenu.Active != null && KvXrStartMenu.Active.Visible) return false;
            if (KvXrWindowHost.Active != null && KvXrWindowHost.Active.LastWindow != null) return false;

            if (!active.hasHit)
            {
                Debug.Log("[KvMr] A: под лучом НЕТ реальной поверхности — наведите луч на свой стол");
                return true;                 // израсходовано: второе действие по тому же A не нужно
            }

            active.Place(active.hitPoint, active.hitNormal, "по кнопке A");
            active.PlacedByOperator = true;
            return true;
        }

        void Awake()
        {
            Active = this;
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
            if (reticle != null) Destroy(reticle);
            if (beam != null) Destroy(beam.gameObject);
            DestroyMaterial(greenMaterial);
            DestroyMaterial(redMaterial);
        }

        void Start()
        {
            startedAt = Time.unscaledTime;
            if (headCamera == null) headCamera = Camera.main;
            // ТОЛЬКО ЧЕРЕЗ Ensure: второй экземпляр компонента построил бы вторую копию
            // коллайдеров комнаты — луч видел бы комнату дважды.
            surfaces = KvMrSurfaces.Ensure(null);
            surfaces.StartTracking();
            mrWorldRoot = KvXrMrRoom.EnsureWorldRoot();
            CreateVisuals();
            Debug.Log("[KvMr] постановка: наведите луч на реальный стол и нажмите A — «поставить сюда»");
        }

        void Update()
        {
            if (room == null || !room.MrActive) return;

            UpdateAim();

            float elapsed = Time.unscaledTime - startedAt;

            // АВТОПОСТАНОВКА: без неё в MR было бы «пусто и непонятно, что делать».
            //
            // ПОЧЕМУ ПОВТОРЯЕМ, А НЕ ОДИН РАЗ (найдено на устройстве 02.11.2026): меш комнаты на
            // PICO появляется не мгновенно (а если шлем только что надели — и вовсе через несколько
            // секунд). Однократная попытка через 3 с давала «поверхность не найдена» и оставляла
            // робота на полу. Теперь попытка повторяется, пока оператор не подтвердил точку.
            if (!PlacedByOperator && elapsed >= nextAutoTry)
            {
                nextAutoTry = elapsed + Mathf.Max(1f, autoPlaceRetrySeconds);
                TryAutoPlace();
            }

            // ЗАПАСНОЙ ПУТЬ: реальных поверхностей нет — ставим робота на пол перед оператором,
            // но говорим об этом прямо, а не делаем вид, что стол найден. Если поверхности появятся
            // позже, постановка с пола будет перенесена на стол (см. TryAutoPlace).
            if (!Placed && !fallbackTried && elapsed >= fallbackFloorDelaySeconds)
            {
                fallbackTried = true;
                PlaceFallbackOnFloor();
            }

            UpdateVisuals();
        }

        // ---------------------------------------------------------------- луч и маркер

        private void UpdateAim()
        {
            hasHit = false;

            Vector3 origin;
            Vector3 direction;
            if (!TryAimDirection(out origin, out direction)) return;

            RaycastHit hit;
            if (!surfaces.TryRaycast(origin, direction, maxDistance, out hit)) return;
            if (hit.normal.y < minUpNormalDot) return;      // стена/наклон — не стол

            hasHit = true;
            hitPoint = hit.point;
            hitNormal = hit.normal;
        }

        /// <summary>Направление луча: из контроллера руки луча, иначе — из головы вниз-вперёд.</summary>
        private bool TryAimDirection(out Vector3 origin, out Vector3 direction)
        {
            origin = Vector3.zero;
            direction = Vector3.forward;

            if (KvXrInputSource.TryRayAimPose(out origin, out Quaternion rotation))
            {
                direction = rotation * Vector3.forward;
                return true;
            }

            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) return false;

            origin = headCamera.transform.position;
            direction = Quaternion.Euler(30f, headCamera.transform.eulerAngles.y, 0f) * Vector3.forward;
            return true;
        }

        private void CreateVisuals()
        {
            greenMaterial = MakeMaterial(new Color(0.15f, 0.95f, 0.35f, 0.85f));
            redMaterial = MakeMaterial(new Color(0.95f, 0.20f, 0.20f, 0.85f));

            var beamObject = new GameObject("KvMrPlacementBeam");
            beamObject.transform.SetParent(transform, false);
            beam = beamObject.AddComponent<LineRenderer>();
            beam.useWorldSpace = true;
            beam.positionCount = 2;
            beam.startWidth = 0.012f;
            beam.endWidth = 0.006f;
            beam.material = greenMaterial;
            beam.enabled = false;

            reticle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            reticle.name = "KvMrPlacementReticle";
            reticle.transform.SetParent(transform, false);
            // Плоский «блин» на поверхности: цилиндр высотой 2 см, сплющенный и утопленный в стол.
            reticle.transform.localScale = new Vector3(0.20f, 0.005f, 0.20f);
            Collider reticleCollider = reticle.GetComponent<Collider>();
            if (reticleCollider != null) Destroy(reticleCollider);        // маркер не должен ловить лучи
            reticleRenderer = reticle.GetComponent<Renderer>();
            if (reticleRenderer != null) reticleRenderer.sharedMaterial = greenMaterial;
            reticle.SetActive(false);
        }

        private void UpdateVisuals()
        {
            if (beam == null || reticle == null) return;
            if (!showAimByDefault)
            {
                // ЧИСТЫЙ ВИД (решение владельца 04.11.2026): луч и метка скрыты по умолчанию,
                // постановка работает без них. Если флаг включат — идём обычным путём ниже.
                beam.enabled = false;
                reticle.SetActive(false);
                return;
            }

            Vector3 origin;
            Vector3 direction;
            bool hasAim = TryAimDirection(out origin, out direction);
            if (!hasAim)
            {
                beam.enabled = false;
                reticle.SetActive(false);
                return;
            }

            bool show = hasHit || PlacedByOperator == false;
            beam.enabled = hasAim;
            beam.startColor = hasHit ? new Color(0.15f, 0.95f, 0.35f, 0.9f) : new Color(0.95f, 0.2f, 0.2f, 0.6f);
            beam.endColor = beam.startColor;

            Vector3 end = hasHit
                ? hitPoint
                : origin + direction.normalized * Mathf.Min(maxDistance, 3f);
            beam.SetPosition(0, origin);
            beam.SetPosition(1, end);

            reticle.SetActive(hasHit && show);
            if (hasHit && show)
            {
                reticle.transform.position = hitPoint + hitNormal * 0.005f;
                reticle.transform.rotation = Quaternion.FromToRotation(Vector3.up, hitNormal);
                if (reticleRenderer != null)
                    reticleRenderer.sharedMaterial = hasHit ? greenMaterial : redMaterial;
            }
        }

        // ---------------------------------------------------------------- постановка

        private void TryAutoPlace()
        {
            Vector3 origin;
            Vector3 direction;
            if (headCamera == null) headCamera = Camera.main;

            // Сначала рука: оператор обычно уже держит контроллер и смотрит на стол.
            if (KvXrInputSource.TryRayAimPose(out origin, out Quaternion rotation))
            {
                direction = rotation * Vector3.forward;
                RaycastHit fromHand;
                if (surfaces.TryRaycast(origin, direction, maxDistance, out fromHand) &&
                    fromHand.normal.y >= minUpNormalDot)
                {
                    Place(fromHand.point, fromHand.normal, "авто (луч руки)");
                    return;
                }
            }

            // Затем взгляд: сверху-вниз под 30° — так оператор видит стол перед собой.
            if (headCamera != null)
            {
                origin = headCamera.transform.position;
                direction = Quaternion.Euler(30f, headCamera.transform.eulerAngles.y, 0f) * Vector3.forward;
                RaycastHit fromHead;
                if (surfaces.TryRaycast(origin, direction, maxDistance, out fromHead) &&
                    fromHead.normal.y >= minUpNormalDot)
                {
                    Place(fromHead.point, fromHead.normal, "авто (луч взгляда)");
                    return;
                }
            }

            if (!warnedNoSurface)
            {
                warnedNoSurface = true;
                Debug.Log("[KvMr] автопостановка: реальная горизонтальная поверхность перед оператором " +
                          "пока не найдена — повторяю попытку каждые " + autoPlaceRetrySeconds.ToString("0") +
                          " с · можно навести луч на стол и нажать A");
            }
        }

        /// <summary>Запасной путь: реальных поверхностей нет — робот на полу в 1,2 м перед оператором.</summary>
        private void PlaceFallbackOnFloor()
        {
            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) return;

            Vector3 forward = headCamera.transform.forward;
            // ОТСЕВ НЕЧИСЛА (E-21): поза головы приходит от рантайма и при потере трекинга может
            // содержать NaN; сравнение длины с NaN всегда false, поэтому проверяем явно.
            if (!IsFiniteVector(forward)) forward = Vector3.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            Vector3 point = headCamera.transform.position + forward.normalized * 1.2f;
            point.y = 0f;
            Place(point, Vector3.up, "запасной путь: реальных поверхностей нет, пол перед оператором");
            Debug.LogWarning("[KvMr] реальные поверхности не появились — робот поставлен на ПОЛ перед оператором. " +
                             "Сквозной вид включён, но физика комнаты недоступна. ЧТО ПРОВЕРИТЬ: " +
                             "(1) шлем надет; (2) в настройках PICO выполнена разметка комнаты (сквозной вид → " +
                             "разметка помещения); (3) разрешение на пространственные данные выдано. " +
                             "Если поверхности появятся позже, робот будет перенесён с пола на стол автоматически.");
        }

        /// <summary>
        /// ПОСТАВИТЬ РОБОТА ТОЧКОЙ ПОВЕРХНОСТИ. Вызывается авто-путём и по A.
        /// </summary>
        public void Place(Vector3 surfacePoint, Vector3 surfaceNormal, string cause)
        {
            if (room == null)
            {
                Debug.LogWarning("[KvMr] постановка невозможна: нет MR-комнаты (KvXrMrRoom)");
                return;
            }

            float yaw = YawFacingOperator(surfacePoint);
            if (!room.PlaceRobotAtSurface(surfacePoint, yaw))
            {
                Debug.LogWarning("[KvMr] постановка не выполнена: " + room.LastPlacementError);
                return;
            }

            Placed = true;
            PlacementCount++;
            bool fromFallback = cause != null && cause.StartsWith("запасной", System.StringComparison.Ordinal);
            bool wasFallback = placedFromFallback;
            placedFromFallback = fromFallback;
            LastSurfacePoint = surfacePoint;
            LastSurfaceNormal = surfaceNormal;
            LastYawDegrees = yaw;
            LastCause = cause;

            var c = System.Globalization.CultureInfo.InvariantCulture;
            if (wasFallback && !fromFallback)
            {
                Debug.Log("[KvMr] робот ПЕРЕНЕСЁН с пола на реальную поверхность: поверхности комнаты " +
                          "появились позже — постановка исправлена автоматически");
            }
            Debug.Log("[KvMr] РОБОТ ПОСТАВЛЕН НА РЕАЛЬНУЮ ПОВЕРХНОСТЬ (" + cause + "): точка " +
                      surfacePoint.x.ToString("0.000", c) + "; " + surfacePoint.y.ToString("0.000", c) + "; " +
                      surfacePoint.z.ToString("0.000", c) + " м · нормаль вверх " +
                      surfaceNormal.y.ToString("0.00", c) + " · поворот " + yaw.ToString("0", c) + "° · масштаб " +
                      (room != null ? room.RobotSize.y.ToString("0.000", c) : "1.000") + " м (высота робота, замер)");
        }

        /// <summary>Поворот робота лицом к оператору (робот смотрит на него передней стороной).</summary>
        private float YawFacingOperator(Vector3 point)
        {
            if (headCamera == null) headCamera = Camera.main;
            if (headCamera == null) return 0f;

            Vector3 toHead = headCamera.transform.position - point;
            // ОТСЕВ НЕЧИСЛА (E-21): обе точки приходят от рантайма/луча и могут быть NaN.
            if (!IsFiniteVector(toHead)) return 0f;
            toHead.y = 0f;
            if (toHead.sqrMagnitude < 0.0001f) return 0f;
            return Mathf.Atan2(toHead.x, toHead.z) * Mathf.Rad2Deg;
        }

        /// <summary>ЧИСЛО ЛИ ЭТО (отсев NaN/бесконечности — класс E-21).</summary>
        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        // ---------------------------------------------------------------- материалы

        private static Material MakeMaterial(Color color)
        {
            return KvRenderPipelineShaders.CreateUnlit("Материал_MR_маркера", color);
        }

        private static void DestroyMaterial(Material material)
        {
            if (material == null) return;
            if (Application.isEditor) DestroyImmediate(material);
            else Destroy(material);
        }
    }
}
