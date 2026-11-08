using System;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using KazistovVvUI;

namespace KazistovVvAr
{
    /// <summary>
    /// ПРИВЯЗКА ВИРТУАЛЬНОГО МИРА К РЕАЛЬНОМУ СТОЛУ. СЕРДЦЕ AR-РЕЖИМА.
    ///
    /// ПРАВИЛО, КОТОРОЕ ЗАПРЕЩЕНО НАРУШАТЬ (S3.1/S3.2 промта). Мир привязывается к `ARAnchor`,
    /// созданному на НАЙДЕННОЙ плоскости. Всё содержимое (робот, столы, траектории) лежит ПОД
    /// якорем. Камера в этой иерархии не участвует вообще: поэтому при движении телефона робот
    /// стоит на месте стола, а не «плывёт» следом за камерой.
    ///
    /// ПОЧЕМУ `AttachAnchor`, А НЕ `TryAddAnchorAsync`. В AR Foundation 6.5 у якоря два пути:
    /// `AttachAnchor(ARPlane, Pose)` — синхронный, привязывает к конкретной найденной плоскости;
    /// `TryAddAnchorAsync(Pose)` — асинхронный, создаёт якорь «в воздухе» на позе. Плоскость у
    /// нас уже есть (по ней и был тап), поэтому основной путь — `AttachAnchor`: он синхронный
    /// (мир появляется сразу, без мигания) и привязан именно к плоскости, как требует S3.2.
    /// Асинхронный путь остаётся запасным — если плоскость не отдалась, но попадание было.
    ///
    /// ПОВЕДЕНИЕ ПРИ ПЕРЕСТАНОВКЕ. «Переставить» удаляет прежний якорь и создаёт новый на новой
    /// плоскости. содержимое прежнего мира уничтожается — так исключаются «два робота в разных
    /// местах», которые выглядели бы как ошибка приложения.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArWorld : MonoBehaviour
    {
        /// <summary>Менеджер якорей (обязателен: без него AR — картинка без привязки).</summary>
        public ARAnchorManager anchorManager;

        /// <summary>Содержимое мира: робот, столы.</summary>
        public KvArContent content;

        /// <summary>Писать в журнал о постановке и привязке объектов.</summary>
        public bool logOnPlace = true;

        /// <summary>Радиус, в котором новые объекты меню «усыновляются» под якорь, метры.</summary>
        public float adoptRadiusMeters = 6f;

        /// <summary>Корень всего виртуального мира AR. Дитя якоря; в мировых координатах = поза якоря.</summary>
        public Transform LabRoot { get; private set; }

        /// <summary>Якорь, к которому привязан мир (null — мир не поставлен).</summary>
        public ARAnchor Anchor { get; private set; }

        /// <summary>Мир поставлен на реальную поверхность.</summary>
        public bool IsPlaced { get { return LabRoot != null; } }

        /// <summary>Положение якоря в мировых координатах AR-сессии.</summary>
        public Vector3 AnchorPosition { get { return LabRoot != null ? LabRoot.position : Vector3.zero; } }

        /// <summary>Высота плоскости, на которую поставлен мир (метры, мировые координаты).</summary>
        public float FloorY { get { return LabRoot != null ? LabRoot.position.y : 0f; } }

        /// <summary>Текущий робот (для строки состояния и проверок).</summary>
        public RobotController Robot
        {
            get { return content != null ? content.CurrentRobot : null; }
        }

        /// <summary>Активный экземпляр (единственный в сцене) — к нему обращаются хуки фабрики объектов.</summary>
        public static KvArWorld Active { get; private set; }

        /// <summary>Сколько объектов «усыновлено» под якорь (для журнала и проверок).</summary>
        public int AdoptedObjects { get; private set; }

        /// <summary>Причина последнего отказа постановки (пусто — отказа не было).</summary>
        public string LastFailure { get; private set; }

        private float nextAdoptTime;

        void Awake()
        {
            Active = this;
            if (anchorManager == null) anchorManager = FindAnchorManager();
            if (content == null) content = GetComponent<KvArContent>();
        }

        void OnDestroy()
        {
            if (Active == this) Active = null;
        }

        void Update()
        {
            if (!IsPlaced) return;
            if (Time.unscaledTime < nextAdoptTime) return;
            nextAdoptTime = Time.unscaledTime + 0.5f;
            AdoptLooseObjects();
        }

        /// <summary>
        /// ПОСТАВИТЬ МИР НА ПОВЕРХНОСТЬ. Возвращает true, если якорь создан (или запрошен
        /// асинхронно) и мир появился.
        /// </summary>
        public bool TryPlace(KvArSurfaceHit surface, Vector3 cameraPosition, out string reason)
        {
            LastFailure = null;
            reason = null;

            if (!surface.valid)
            {
                reason = "нет попадания в плоскость";
                LastFailure = reason;
                return false;
            }

            if (anchorManager == null)
            {
                reason = "нет ARAnchorManager в сцене — привязка к миру невозможна";
                LastFailure = reason;
                Debug.LogError("[KvAr] ЯКОРЬ: " + reason);
                return false;
            }

            // ПЕРЕСТАНОВКА: старый мир (и прежний якорь) убираем — два мира не должно быть.
            if (IsPlaced || Anchor != null) Clear("перестановка мира");

            Pose pose = surface.worldPose;

            // ОСНОВНОЙ ПУТЬ: привязка к найденной плоскости, синхронно.
            if (surface.plane != null)
            {
                try
                {
                    ARAnchor attached = anchorManager.AttachAnchor(surface.plane, pose);
                    if (attached != null)
                    {
                        ApplyAnchor(attached, surface, cameraPosition);
                        return true;
                    }
                    Debug.LogWarning("[KvAr] ЯКОРЬ: AttachAnchor вернул null — пробую асинхронный путь");
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[KvAr] ЯКОРЬ: AttachAnchor не сработал (" +
                                     error.GetType().Name + ": " + error.Message +
                                     ") — пробую асинхронный путь");
                }
            }

            // ЗАПАСНОЙ ПУТЬ: асинхронный якорь на позе попадания (плоскость не отдалась).
            CreateAnchorAsync(pose, surface, cameraPosition);
            return true;
        }

        /// <summary>Создать мир по уже созданному якорю: корень, робот, строка журнала.</summary>
        public void ApplyAnchor(ARAnchor anchor, KvArSurfaceHit surface, Vector3 cameraPosition)
        {
            if (anchor == null)
            {
                LastFailure = "якорь не создан";
                Debug.LogError("[KvAr] ЯКОРЬ: " + LastFailure);
                return;
            }

            Anchor = anchor;

            GameObject rootGo = new GameObject("ArLabRoot");
            LabRoot = rootGo.transform;
            // Дитя якоря с нулевыми локальными координатами: мир занимает ровно позу якоря.
            // Именно поэтому движение камеры не двигает мир — двигается только якорь трекингом.
            LabRoot.SetParent(anchor.transform, worldPositionStays: false);
            LabRoot.localPosition = Vector3.zero;
            LabRoot.localRotation = Quaternion.identity;
            LabRoot.localScale = Vector3.one;

            float yaw = KvArPlacementMath.YawTowardsCamera(surface.point, cameraPosition);
            bool spawned = content != null && content.SpawnDefaultRobot(LabRoot, Vector3.zero, yaw);

            if (logOnPlace)
            {
                Debug.Log("[KvAr] ЯКОРЬ: мир привязан к реальной плоскости · " +
                          surface.Describe() + " · высота якоря " +
                          LabRoot.position.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) +
                          " м · поворот робота " +
                          yaw.ToString("0", System.Globalization.CultureInfo.InvariantCulture) +
                          "° · робот: " + (spawned ? "поставлен" : "НЕ создан") +
                          " · якорь в иерархии «" + anchor.name + "»");
                if (spawned) Debug.Log("[KvAr] " + content.DescribeLastSpawn());
            }
        }

        /// <summary>Асинхронный запасной путь создания якоря.</summary>
        private async void CreateAnchorAsync(Pose pose, KvArSurfaceHit surface, Vector3 cameraPosition)
        {
            try
            {
                Result<ARAnchor> result = await anchorManager.TryAddAnchorAsync(pose);
                if (result.status.IsSuccess() && result.value != null)
                {
                    ApplyAnchor(result.value, surface, cameraPosition);
                    return;
                }
                LastFailure = "TryAddAnchorAsync: статус " + result.status;
                Debug.LogError("[KvAr] ЯКОРЬ: не создан — " + LastFailure);
            }
            catch (Exception error)
            {
                LastFailure = error.GetType().Name + ": " + error.Message;
                Debug.LogError("[KvAr] ЯКОРЬ: исключение при создании — " + LastFailure);
            }
        }

        /// <summary>
        /// УБРАТЬ МИР (для перестановки). Уничтожает содержимое и снимает якорь: иначе на сцене
        /// остались бы «два робота в разных местах», и это выглядело бы как дефект приложения.
        /// </summary>
        public void Clear(string why)
        {
            if (LabRoot != null)
            {
                if (logOnPlace) Debug.Log("[KvAr] МИР: убран (" + why + ")");
                UnityEngine.Object.Destroy(LabRoot.gameObject);
                LabRoot = null;
            }

            if (content != null) content.ForgetCurrentRobot();

            if (Anchor != null)
            {
                try
                {
                    if (anchorManager != null && anchorManager.enabled)
                        anchorManager.TryRemoveAnchor(Anchor);
                }
                catch (Exception error)
                {
                    Debug.LogWarning("[KvAr] ЯКОРЬ: снять не удалось — " + error.Message);
                }
                if (Anchor != null) UnityEngine.Object.Destroy(Anchor.gameObject);
                Anchor = null;
            }

            AdoptedObjects = 0;
        }

        /// <summary>
        /// ПРИВЯЗАТЬ НОВЫЕ ОБЪЕКТЫ МЕНЮ К ЯКОРЮ. Команды меню создают столы и роботов в мировых
        /// координатах (так они создавались в шлеме). В AR их место вычисляет хук фабрики, но
        /// родителя у них нет — здесь они переносятся под якорь и дальше не зависят от камеры.
        /// </summary>
        public void AdoptLooseObjects()
        {
            if (LabRoot == null) return;

            RobotController[] robots =
                UnityEngine.Object.FindObjectsByType<RobotController>(
                    FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < robots.Length; i++)
            {
                RobotController robot = robots[i];
                if (robot == null || robot.transform.IsChildOf(LabRoot)) continue;
                if (!NearLab(robot.transform.position)) continue;
                Adopt(robot.transform, "робот «" + robot.robotName + "»");
            }

            RegisteredObject[] registered = UnityEngine.Object.FindObjectsByType<RegisteredObject>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < registered.Length; i++)
            {
                RegisteredObject item = registered[i];
                if (item == null || item.transform.IsChildOf(LabRoot)) continue;
                if (item.GetComponentInParent<RobotController>() != null) continue;
                if (!NearLab(item.transform.position)) continue;
                Adopt(item.transform, "объект «" + (string.IsNullOrEmpty(item.DisplayName)
                    ? item.name : item.DisplayName) + "»");
            }
        }

        private bool NearLab(Vector3 worldPosition)
        {
            return Vector3.Distance(worldPosition, LabRoot.position) <= Mathf.Max(0.5f, adoptRadiusMeters);
        }

        private void Adopt(Transform target, string label)
        {
            target.SetParent(LabRoot, worldPositionStays: true);
            AdoptedObjects++;
            if (logOnPlace)
                Debug.Log("[KvAr] ПРИВЯЗКА: " + label + " перенесён под якорь — не плывёт за камерой " +
                          "(всего привязано " + AdoptedObjects + ")");
        }

        /// <summary>
        /// ГДЕ СОЗДАТЬ ОБЪЕКТ ИЗ МЕНЮ. Вызывается хуком `KvXrObjectFactory`: в AR нельзя ставить
        /// «в 1,2 м перед лицом на y = 0» — реального нуля на планшете нет, а пол уже найден.
        /// Возвращаем точку на поверхности якоря перед устройством. false — мир не поставлен.
        ///
        /// ГРАНИЦЫ ПЛОСКОСТИ (07.11.2026). Дефект планшета: стол из ПК-меню создавался в 1,2 м
        /// перед устройством, найденная плоскость была всего 0,62 м², и ножки нового стола
        /// висели над полом ЗА КРАЕМ настоящего стола. Теперь точка приводится внутрь границ
        /// найденного стола с отступом 8 см; если плоскость ещё не найдена — прежнее поведение.
        /// </summary>
        public static bool TryGetMenuSpawnPoint(Camera camera, float distanceMeters, out Vector3 point)
        {
            point = Vector3.zero;
            KvArWorld world = Active;
            if (world == null || !world.IsPlaced) return false;

            Vector3 forward = Vector3.forward;
            if (camera != null)
            {
                Vector3 flat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
                if (flat.sqrMagnitude > 0.0001f) forward = flat.normalized;
            }

            float distance = Mathf.Clamp(distanceMeters, 0.3f, 3f);
            Vector3 basePoint = world.LabRoot.position;
            Vector3 requested = new Vector3(basePoint.x + forward.x * distance,
                                            basePoint.y,
                                            basePoint.z + forward.z * distance);

            point = ClampSpawnPointToTable(requested, forward, distance);
            return true;
        }

        /// <summary>
        /// ВПИСАТЬ ТОЧКУ СОЗДАНИЯ В НАЙДЕННЫЙ СТОЛ. Если точка вне границ — двигаем её внутрь,
        /// с запасом под габарит объекта (четверть размера стола), и пишем это в журнал.
        /// </summary>
        private static Vector3 ClampSpawnPointToTable(Vector3 requested, Vector3 forward, float distance)
        {
            KvArPlaneVisualizer visualizer =
                UnityEngine.Object.FindAnyObjectByType<KvArPlaneVisualizer>();
            Vector3 center;
            float halfWidth;
            float halfDepth;
            Quaternion rotation;
            if (visualizer == null ||
                !visualizer.TryGetBestTableBounds(out center, out halfWidth, out halfDepth, out rotation))
                return requested;

            const float margin = 0.08f;
            if (!KvArPlacementMath.IsOutsidePlane(requested, center, halfWidth, halfDepth, rotation))
                return requested;

            Vector3 clamped = KvArPlacementMath.ClampInsidePlane(
                requested, center, halfWidth, halfDepth, rotation, margin);
            Debug.Log("[KvAr] ИНТЕРФЕЙС: точка создания вышла за край стола (" +
                      halfWidth.ToString("0.00") + "×" + halfDepth.ToString("0.00") +
                      " м половины) — сдвигаю внутрь плоскости: " + clamped.ToString("0.00"));
            return clamped;
        }

        /// <summary>КОРЕНЬ МИРА ДЛЯ ХУКА ФАБРИКИ (y = высота найденной плоскости).</summary>
        public static float MenuFloorY
        {
            get
            {
                KvArWorld world = Active;
                return world != null && world.IsPlaced ? world.FloorY : 0f;
            }
        }

        /// <summary>Мир поставлен и готов принимать объекты меню.</summary>
        public static bool MenuSpawnReady
        {
            get
            {
                KvArWorld world = Active;
                return world != null && world.IsPlaced;
            }
        }

        /// <summary>ПОВОРОТ ВИДА: вращает мир вокруг вертикали якоря (камера в AR неуправляема).</summary>
        public void RotateView(float deltaDegrees)
        {
            if (LabRoot == null || Mathf.Abs(deltaDegrees) < 0.0001f) return;
            LabRoot.Rotate(0f, deltaDegrees, 0f, Space.World);
        }

        /// <summary>СБРОС ПОВОРОТА ВИДА: поворот мира = поворот якоря (как при постановке).</summary>
        public void ResetViewRotation()
        {
            if (LabRoot == null) return;
            LabRoot.localRotation = Quaternion.identity;
            Debug.Log("[KvAr] ВИД: поворот мира сброшен к позе якоря");
        }

        /// <summary>Найти `ARAnchorManager` в сцене, если ссылка не задана.</summary>
        public static ARAnchorManager FindAnchorManager()
        {
            return UnityEngine.Object.FindAnyObjectByType<ARAnchorManager>();
        }

        /// <summary>Строка состояния для журнала: поставлен ли якорь и где.</summary>
        public string Describe()
        {
            if (!IsPlaced) return "мир не поставлен (якоря нет)";
            StringBuilder sb = new StringBuilder();
            sb.Append("якорь: ").Append(AnchorPosition.ToString("0.000")).Append(" м");
            sb.Append(" · высота плоскости ").Append(FloorY.ToString("0.000")).Append(" м");
            sb.Append(" · робот: ").Append(Robot != null ? Robot.robotName : "нет");
            sb.Append(" · привязано объектов ").Append(AdoptedObjects);
            if (!string.IsNullOrEmpty(LastFailure)) sb.Append(" · отказ: ").Append(LastFailure);
            return sb.ToString();
        }
    }
}
