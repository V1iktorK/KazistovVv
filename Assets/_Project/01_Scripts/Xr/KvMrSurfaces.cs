using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.PXR;

namespace KazistovVvXr
{
    /// <summary>
    /// РЕАЛЬНЫЕ ПОВЕРХНОСТИ КОМНАТЫ → КОЛЛАЙДЕРЫ → ЛУЧ (создано 02.11.2026, сессия S45).
    ///
    /// ЗАЧЕМ. В VR «стол» — это объект сцены с коллайдером, и луч попадает в него через обычный
    /// `Physics.Raycast`. В MR стол НАСТОЯЩИЙ: коллайдера у него нет, и `Physics.Raycast` его не
    /// видит вовсе. Именно поэтому «навести луч на свой стол и поставить робота» до сих пор не
    /// работало: попадать было некуда.
    ///
    /// КАК ЭТО РЕШАЕТСЯ. В шлеме есть подсистема пространственного меша (`XRMeshSubsystem`) —
    /// это СТАНДАРТНЫЙ Unity XR API, а не вендорский: PICO создаёт её фичей `PICOSpatialMesh`,
    /// Meta — фичей `arfoundation-meta-mesh`. Здесь меши комнаты превращаются в невидимые
    /// `MeshCollider`-объекты, и лучи начинают попадать в реальную столешницу как в обычную
    /// виртуальную поверхность. Ни один полигон невидим: рендерера у коллайдеров нет.
    ///
    /// ЧЕГО ЗДЕСЬ НЕТ. Нет рендера комнаты («серые стены поверх passthrough» не нужны) и нет
    /// подмены сквозного вида: картинку комнаты даёт passthrough, этот компонент даёт только
    /// ФИЗИКУ реального мира.
    ///
    /// ЧЕСТНО ОБ ОТКАЗЕ. Если меша нет (устройство не умеет, разрешение не выдано, фича выключена),
    /// компонент пишет причину в журнал и говорит `Ready = false`. Тогда постановка робота честно
    /// переходит на запасной путь, а не «висит в воздухе».
    /// </summary>
    [DisallowMultipleComponent]
    public class KvMrSurfaces : MonoBehaviour
    {
        /// <summary>Имя корневого объекта, под которым живут коллайдеры реального мира.</summary>
        public const string ContainerName = "KvMrRealWorld";

        /// <summary>Сколько мешей комнаты максимум принимать (защита памяти).</summary>
        public int maxMeshes = 400;

        /// <summary>Писать в журнал каждый построенный меш (для наладки; по умолчанию — только итог).</summary>
        public bool logEveryMesh = false;

        /// <summary>Сколько ждать подсистему меша, прежде чем честно сказать «её нет» (секунды).</summary>
        public float waitForSubsystemSeconds = 10f;

        /// <summary>
        /// ЗАПАСНОЙ ИСТОЧНИК ПОВЕРХНОСТИ — СЕМАНТИЧЕСКАЯ СЦЕНА PICO, секунды (S45).
        ///
        /// ЗАЧЕМ. Меш комнаты на PICO 4 Ultra отдаёт полигоны не всегда: он требует надетого шлема
        /// и разметки помещения. У PICO есть ВТОРОЙ источник — размеченные объекты комнаты
        /// (семантическая сцена), где стол приходит как параллелепипед с габаритами. Если меш
        /// молчит, этот источник позволяет поставить робота на РЕАЛЬНЫЙ стол, а не на пол.
        /// </summary>
        public float sceneAnchorDelaySeconds = 8f;

        /// <summary>Готово ли принимать лучи: есть хотя бы один коллайдер реального мира.</summary>
        public bool Ready { get; private set; }

        /// <summary>Число коллайдеров реального мира (для журнала и экрана состояния).</summary>
        public int ColliderCount { get { return realWorldColliders.Count; } }

        /// <summary>Строка состояния одной строкой.</summary>
        public string Status
        {
            get
            {
                if (subsystem == null) return "реальные поверхности: подсистема меша не найдена (" + noSubsystemReason + ")";
                if (!Ready) return "реальные поверхности: меш запущен, ждём первые полигоны (коллайдеров 0)";
                return "реальные поверхности: коллайдеров " + realWorldColliders.Count +
                       " · мешей " + builtMeshes + " · размер комнаты " + RoomSizeText();
            }
        }

        private readonly List<XRMeshSubsystem> subsystemBuffer = new List<XRMeshSubsystem>();
        private readonly List<MeshInfo> meshInfos = new List<MeshInfo>();
        private readonly HashSet<MeshId> knownMeshIds = new HashSet<MeshId>();
        private readonly HashSet<MeshId> pendingMeshIds = new HashSet<MeshId>();
        private readonly HashSet<Collider> realWorldColliders = new HashSet<Collider>();
        private readonly Dictionary<MeshId, Mesh> meshesById = new Dictionary<MeshId, Mesh>();
        private readonly Dictionary<MeshId, MeshCollider> collidersById = new Dictionary<MeshId, MeshCollider>();
        private readonly List<MeshId> removedBuffer = new List<MeshId>();

        private XRMeshSubsystem subsystem;
        private Transform container;
        private bool permissionGranted;
        private bool permissionRequested;
        private float startedAt;
        private bool gaveUp;
        private string noSubsystemReason = "ещё не искали";
        private int builtMeshes;
        private int failedMeshes;
        private bool sceneAnchorRequested;
        private bool sceneAnchorRunning;
        private int tableColliders;

        /// <summary>Текущий экземпляр. Один на приложение: иначе два компонента построят по копии
        /// коллайдеров, и луч будет видеть комнату дважды (и память уйдёт вдвое).</summary>
        public static KvMrSurfaces Instance { get; private set; }

        /// <summary>
        /// СОЗДАТЬ КОМПОНЕНТ В РАНТАЙМЕ (сцена не правится — её SHA256 закреплён тестом).
        /// </summary>
        public static KvMrSurfaces Ensure(Transform parent)
        {
            if (Instance != null) return Instance;

            var go = new GameObject("KvMrSurfaces");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvMrSurfaces>();
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // Второй экземпляр не нужен: убираем, чтобы коллайдеры не удвоились.
                Destroy(this);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            // Коллайдеры реального мира — наши объекты; убираем за собой, чтобы повторный запуск
            // не наткнулся на «двойников» и не удвоил память.
            if (container != null) Destroy(container.gameObject);
        }

        /// <summary>
        /// НАЧАТЬ ОТСЛЕЖИВАНИЕ РЕАЛЬНЫХ ПОВЕРХНОСТЕЙ.
        ///
        /// Порядок важен и повторяется в проекте PICO (`PXR_SpatialMeshManager`): сначала
        /// РАЗРЕШЕНИЕ на пространственные данные, только потом `subsystem.Start()`. Без разрешения
        /// подсистема молча не стартует — и это выглядит как «меша нет».
        /// </summary>
        public void StartTracking()
        {
            if (subsystem != null) return;
            startedAt = Time.unscaledTime;

            if (container == null)
            {
                var go = new GameObject(ContainerName);
                go.transform.SetParent(transform, false);
                container = go.transform;
            }

            NextSubsystemScan = 0f;
            Debug.Log("[KvMr] реальные поверхности: начинаю отслеживание");
        }

        private float NextSubsystemScan;

        void Update()
        {
            if (gaveUp && (Ready || sceneAnchorRunning)) return;

            // Подсистему ищем не каждый кадр, а раз в секунду: она появляется только после
            // создания OpenXR-сессии, и опрос каждый кадр ничего не ускорит, но стоит времени.
            if (subsystem == null)
            {
                if (Time.unscaledTime < NextSubsystemScan) return;
                NextSubsystemScan = Time.unscaledTime + 1f;
            }

            PumpSubsystem();

            // ЗАПАСНОЙ ИСТОЧНИК: меш не дал полигонов за отведённое время — спросить у PICO
            // размеченные объекты комнаты (стол приходит параллелепипедом с габаритами).
            if (!Ready && !sceneAnchorRequested && !sceneAnchorRunning &&
                Time.unscaledTime - startedAt >= sceneAnchorDelaySeconds)
            {
                sceneAnchorRequested = true;
                RequestSceneAnchors();
            }
        }

        /// <summary>Поиск подсистемы, разрешение, старт и чтение мешей — по шагам, идемпотентно.</summary>
        private void PumpSubsystem()
        {
            if (gaveUp) return;          // уже сказали «меша нет» — не спамим одним и тем же предупреждением

            if (subsystem == null)
            {
                if (Time.unscaledTime - startedAt > waitForSubsystemSeconds)
                {
                    gaveUp = true;
                    noSubsystemReason = "меш не появился за " + waitForSubsystemSeconds.ToString("0") +
                                        " с — устройство не умеет меш, фича выключена или не выдано разрешение";
                    Debug.LogWarning("[KvMr] реальные поверхности: " + noSubsystemReason);
                    return;
                }

                subsystemBuffer.Clear();
                SubsystemManager.GetSubsystems(subsystemBuffer);
                if (subsystemBuffer.Count == 0) return;

                subsystem = subsystemBuffer[0];
                noSubsystemReason = "найдена, но не запущена";
                Debug.Log("[KvMr] реальные поверхности: подсистема меша найдена (" +
                          (subsystem.running ? "уже работает" : "запускаю") + ")");
            }

            if (!permissionGranted)
            {
                RequestSpatialPermission();
                if (!permissionGranted) return;
            }

            if (!subsystem.running)
            {
                try { subsystem.Start(); }
                catch (Exception e)
                {
                    noSubsystemReason = "старт меша бросил " + e.GetType().Name + ": " + e.Message;
                    Debug.LogWarning("[KvMr] реальные поверхности: " + noSubsystemReason);
                    return;
                }
                if (!subsystem.running) return;
                Debug.Log("[KvMr] реальные поверхности: подсистема меша запущена");
            }

            meshInfos.Clear();
            if (!subsystem.TryGetMeshInfos(meshInfos)) return;

            for (int i = 0; i < meshInfos.Count; i++)
            {
                MeshInfo info = meshInfos[i];
                switch (info.ChangeState)
                {
                    case MeshChangeState.Added:
                    case MeshChangeState.Updated:
                        if (pendingMeshIds.Contains(info.MeshId)) continue;
                        if (knownMeshIds.Contains(info.MeshId) && info.ChangeState == MeshChangeState.Added) continue;
                        BuildMesh(info.MeshId);
                        break;

                    case MeshChangeState.Removed:
                        removedBuffer.Add(info.MeshId);
                        break;
                }
            }

            for (int i = 0; i < removedBuffer.Count; i++) RemoveMesh(removedBuffer[i]);
            removedBuffer.Clear();
        }

        /// <summary>Запросить разрешение на пространственные данные (только Android).</summary>
        private void RequestSpatialPermission()
        {
            if (permissionRequested) return;
            permissionRequested = true;

#if UNITY_ANDROID && !UNITY_EDITOR
            string permission = KvMrRuntime.Backend == KvMrBackend.Meta
                ? "com.oculus.permission.USE_SCENE"
                : "com.picovr.permission.SPATIAL_DATA";

            try
            {
                if (UnityEngine.Android.Permission.HasUserAuthorizedPermission(permission))
                {
                    permissionGranted = true;
                    Debug.Log("[KvMr] разрешение на пространственные данные уже выдано: " + permission);
                    return;
                }

                var callbacks = new UnityEngine.Android.PermissionCallbacks();
                callbacks.PermissionGranted += _ =>
                {
                    permissionGranted = true;
                    Debug.Log("[KvMr] разрешение на пространственные данные выдано: " + permission);
                };
                callbacks.PermissionDenied += _ =>
                    Debug.LogWarning("[KvMr] разрешение на пространственные данные ОТКЛОНЕНО: " + permission +
                                     " — реальные поверхности недоступны, постановка на стол повиснет");
                callbacks.PermissionDeniedAndDontAskAgain += _ =>
                    Debug.LogWarning("[KvMr] разрешение на пространственные данные ОТКЛОНЕНО НАВСЕГДА: " + permission);

                UnityEngine.Android.Permission.RequestUserPermission(permission, callbacks);
                Debug.Log("[KvMr] запрошено разрешение на пространственные данные: " + permission);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvMr] не удалось запросить разрешение на пространственные данные: " +
                                 e.GetType().Name + ": " + e.Message);
            }
#else
            // В редакторе и на ПК пространственных данных нет — это не ошибка.
            permissionGranted = true;
#endif
        }

        /// <summary>Построить невидимый коллайдер из меша комнаты.</summary>
        private void BuildMesh(MeshId meshId)
        {
            if (knownMeshIds.Count >= maxMeshes && !knownMeshIds.Contains(meshId))
            {
                Debug.LogWarning("[KvMr] реальные поверхности: достигнут предел " + maxMeshes +
                                 " мешей — новые не принимаются (защита памяти)");
                return;
            }

            pendingMeshIds.Add(meshId);

            var go = new GameObject("KvMrMesh_" + meshId);
            go.transform.SetParent(container, false);
            // Коллайдер живёт в начале координат трекинга: вершины меша от Unity уже в этой системе.
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.convex = false;
            Mesh mesh = new Mesh();
            mesh.name = "KvMrMesh_" + meshId;
            meshesById[meshId] = mesh;
            collidersById[meshId] = collider;

            try
            {
                subsystem.GenerateMeshAsync(meshId, mesh, collider, MeshVertexAttributes.Normals,
                    result => OnMeshGenerated(result, collider, mesh));
            }
            catch (Exception e)
            {
                pendingMeshIds.Remove(meshId);
                failedMeshes++;
                Debug.LogWarning("[KvMr] реальные поверхности: построение меша бросило " +
                                 e.GetType().Name + ": " + e.Message);
            }
        }

        private void OnMeshGenerated(MeshGenerationResult result, MeshCollider collider, Mesh mesh)
        {
            pendingMeshIds.Remove(result.MeshId);

            if (result.Status != MeshGenerationStatus.Success)
            {
                failedMeshes++;
                if (failedMeshes <= 3)
                    Debug.LogWarning("[KvMr] реальные поверхности: меш не построен, статус " + result.Status);
                RemoveMesh(result.MeshId);
                return;
            }

            bool wasReady = Ready;
            knownMeshIds.Add(result.MeshId);
            if (collider != null)
            {
                collider.sharedMesh = mesh;
                realWorldColliders.Add(collider);
            }
            builtMeshes++;
            Ready = true;

            if (logEveryMesh)
                Debug.Log("[KvMr] реальные поверхности: меш " + result.MeshId + " · вершин " + mesh.vertexCount);

            if (!wasReady)
                Debug.Log("[KvMr] " + Status);
        }

        private void RemoveMesh(MeshId meshId)
        {
            MeshCollider collider;
            if (collidersById.TryGetValue(meshId, out collider) && collider != null)
            {
                realWorldColliders.Remove(collider);
                Destroy(collider.gameObject);
            }
            collidersById.Remove(meshId);

            Mesh mesh;
            if (meshesById.TryGetValue(meshId, out mesh) && mesh != null) Destroy(mesh);
            meshesById.Remove(meshId);

            knownMeshIds.Remove(meshId);
            Ready = realWorldColliders.Count > 0;
        }

        /// <summary>
        /// ПОПАДАНИЕ ЛУЧА В РЕАЛЬНУЮ ПОВЕРХНОСТЬ.
        ///
        /// ПОЧЕМУ НЕ ПРОСТО Physics.Raycast. В сцене есть виртуальные коллайдеры (робот, панели);
        /// луч по столу не должен цеплять их. Поэтому берутся ВСЕ попадания и из них выбирается
        /// ближайшее, чей коллайдер принадлежит НАШЕМУ списку реальных поверхностей. Никаких
        /// изменений слоёв проекта (это задело бы VR).
        /// </summary>
        public bool TryRaycast(Vector3 origin, Vector3 direction, float maxDistance, out RaycastHit hit)
        {
            hit = default(RaycastHit);
            if (!Ready || realWorldColliders.Count == 0) return false;

            // ОТСЕВ НЕЧИСЛА (класс E-21). Данные приходят ИЗВНЕ — от позы контроллера; при потере
            // отслеживания там штатно появляется NaN. Любое сравнение длины с NaN даёт false, то
            // есть «проверка» пропустила бы мусор, а луч ушёл бы в никуда (и предмет мог бы
            // «дрожать»). Явные IsNaN/IsInfinity — единственная надёжная отбраковка.
            if (!IsFiniteVector(origin) || !IsFiniteVector(direction) || float.IsNaN(maxDistance) ||
                float.IsInfinity(maxDistance))
                return false;

            if (direction.sqrMagnitude < 0.0001f) return false;

            RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, maxDistance,
                Physics.AllLayers, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;

            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || !realWorldColliders.Contains(c)) continue;
                if (hits[i].distance < best)
                {
                    best = hits[i].distance;
                    hit = hits[i];
                    found = true;
                }
            }
            return found;
        }

        /// <summary>ЧИСЛО ЛИ ЭТО (отсев NaN/бесконечности — класс E-21).</summary>
        private static bool IsFiniteVector(Vector3 v)
        {
            return !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
                   !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z);
        }

        // ---------------------------------------------------------------- запасной источник: сцена PICO

        /// <summary>
        /// СПРОСИТЬ У PICO РАЗМЕЧЕННЫЕ ОБЪЕКТЫ КОМНАТЫ (семантическая сцена) — запасной путь,
        /// когда меш комнаты не дал полигонов.
        ///
        /// ЗАЧЕМ. Меш требует надетого шлема и разметки помещения, а у PICO есть второй источник:
        /// размеченные объекты комнаты, где СТОЛ приходит параллелепипедом с габаритами в метрах.
        /// Для задачи «поставить робота на свой стол» этого достаточно: строим невидимый
        /// BoxCollider по габаритам стола, и луч постановки начинает в него попадать.
        ///
        /// КОНВЕРСИЯ КООРДИНАТ — ТОНКОЕ МЕСТО (проверено по исходникам пакета PICO 1.4.1):
        /// поза СУЩНОСТИ пакет переводит в Unity сам (инверсия Z), а позу БОКСА — НЕТ. Поэтому
        /// бокс переводится здесь: позиция с -z, кватернион с -z/-w, затем накладывается поза
        /// сущности. Иначе стол оказался бы зеркально отражён относительно оператора.
        /// </summary>
        private async void RequestSceneAnchors()
        {
            if (Application.platform != RuntimePlatform.Android)
            {
                Debug.Log("[KvMr] реальные поверхности: семантическая сцена доступна только на Android");
                return;
            }
            if (KvMrRuntime.Backend != KvMrBackend.Pico)
            {
                Debug.Log("[KvMr] реальные поверхности: семантическая сцена PICO не применима к бэкенду " +
                          KvMrRuntime.Backend);
                return;
            }

            sceneAnchorRunning = true;
            try
            {
                Debug.Log("[KvMr] реальные поверхности: меша нет — спрашиваю размеченные объекты комнаты (стол)");

                PxrResult start = await WithTimeout(
                    PXR_MixedReality.StartSenseDataProvider(PxrSenseDataProviderType.SceneCapture), 8000);
                if (start != PxrResult.SUCCESS)
                {
                    Debug.LogWarning("[KvMr] реальные поверхности: провайдер сцены не запустился, код " + start +
                                     " — вероятно, разметка комнаты не выполнялась");
                    return;
                }

                var query = await WithTimeout(
                    PXR_MixedReality.QuerySceneAnchorAsync(new[] { PxrSemanticLabel.Table }), 12000);
                if (query.result != PxrResult.SUCCESS)
                {
                    Debug.LogWarning("[KvMr] реальные поверхности: запрос стола вернул " + query.result);
                    return;
                }

                int created = 0;
                for (int i = 0; i < query.anchorHandleList.Count; i++)
                {
                    ulong handle = query.anchorHandleList[i];

                    PxrSceneComponentType[] types;
                    if (PXR_MixedReality.GetSceneAnchorComponentTypes(handle, out types) != PxrResult.SUCCESS) continue;

                    bool hasBox = false;
                    for (int k = 0; k < types.Length; k++)
                        if (types[k] == PxrSceneComponentType.Box3D) hasBox = true;
                    if (!hasBox) continue;

                    Vector3 boxPos, boxExtent;
                    Quaternion boxRot;
                    Vector3 entityPos;
                    Quaternion entityRot;
                    if (PXR_MixedReality.GetSceneBox3DData(handle, out boxPos, out boxRot, out boxExtent) != PxrResult.SUCCESS)
                        continue;
                    if (PXR_MixedReality.LocateAnchor(handle, out entityPos, out entityRot) != PxrResult.SUCCESS)
                        continue;

                    CreateTableCollider(entityPos, entityRot, boxPos, boxRot, boxExtent);
                    created++;
                }

                tableColliders = created;
                if (created > 0)
                {
                    Ready = true;
                    Debug.Log("[KvMr] реальные поверхности: из размеченной комнаты получено столов " + created);
                    Debug.Log("[KvMr] " + Status);
                }
                else
                {
                    Debug.LogWarning("[KvMr] реальные поверхности: размеченная комната не дала ни одного стола — " +
                                     "нужна разметка помещения (настройки PICO → сквозной вид → разметка)");
                }
            }
            catch (TimeoutException)
            {
                Debug.LogWarning("[KvMr] реальные поверхности: запрос семантической сцены не ответил за отведённое время");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[KvMr] реальные поверхности: ошибка запроса сцены: " + e.GetType().Name + ": " + e.Message);
            }
            finally
            {
                sceneAnchorRunning = false;
            }
        }

        /// <summary>Построить невидимый коллайдер стола по данным семантической сцены.</summary>
        private void CreateTableCollider(Vector3 entityPos, Quaternion entityRot, Vector3 boxPos,
            Quaternion boxRot, Vector3 extent)
        {
            Vector3 localPos = new Vector3(boxPos.x, boxPos.y, -boxPos.z);
            Quaternion localRot = new Quaternion(boxRot.x, boxRot.y, -boxRot.z, -boxRot.w);
            Vector3 worldPos = entityPos + entityRot * localPos;
            Quaternion worldRot = entityRot * localRot;

            var go = new GameObject("KvMrRealTable_" + tableColliders);
            go.transform.SetParent(container, false);
            go.transform.position = worldPos;
            go.transform.rotation = worldRot;

            BoxCollider box = go.AddComponent<BoxCollider>();
            box.size = extent;
            realWorldColliders.Add(box);

            var c = System.Globalization.CultureInfo.InvariantCulture;
            Debug.Log(string.Format(c,
                "[KvMr] реальный стол из разметки: центр ({0:0.00}; {1:0.00}; {2:0.00}) м · " +
                "размеры {3:0.00}×{4:0.00}×{5:0.00} м",
                worldPos.x, worldPos.y, worldPos.z, extent.x, extent.y, extent.z));
        }

        /// <summary>Ждать задачу не дольше тайм-аута: нативные запросы PICO могут не ответить вовсе.</summary>
        private static async Task<T> WithTimeout<T>(Task<T> task, int timeoutMs)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(timeoutMs));
            if (completed != task) throw new TimeoutException();
            return await task;
        }

        /// <summary>Габарит комнаты по построенным мешам — для честной строки журнала.</summary>
        private string RoomSizeText()
        {
            if (realWorldColliders.Count == 0) return "0×0×0 м";
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (Collider c in realWorldColliders)
            {
                if (c == null) continue;
                Bounds b = c.bounds;
                min = Vector3.Min(min, b.min);
                max = Vector3.Max(max, b.max);
            }
            Vector3 size = max - min;
            return size.x.ToString("0.0") + "×" + size.y.ToString("0.0") + "×" + size.z.ToString("0.0") + " м";
        }
    }
}
