using UnityEngine;
using KazistovVvFeatures;

namespace KazistovVvXr
{
    /// <summary>
    /// ВТОРОЙ ЛАЗЕР — ИЗ ЛЕВОЙ РУКИ, ТОЛЬКО УКАЗАТЕЛЬ (§39).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА: «нет лазера из левого контроллера». Решение из опроса §39: второй луч —
    /// **только указатель, не нажимает**; нажимает рука луча (по умолчанию правая). Причина такого
    /// решения техническая: курок один, и если бы обе руки могли нажимать, одно движение делало бы
    /// два действия — та же болезнь, которую на десктопе лечили арбитражем клавиш в §27.
    ///
    /// ПОЧЕМУ ОТДЕЛЬНЫЙ КОМПОНЕНТ, А НЕ ВТОРАЯ ЛИНИЯ ВНУТРИ `KvXrUiPointer`. У указателя уже есть
    /// свои обязанности (наведение, удержание, нажатие, курсор); второй луч ничего из этого не
    /// делает и не должен случайно начать. Отдельный класс — отдельная ответственность, и
    /// «не нажимает» проверяется тестом (`KvXrSecondaryRayLogic.CanPress() == false`).
    ///
    /// ВЫКЛЮЧАЕТСЯ БЕЗ ПЕРЕСБОРКИ: ключ файла подстройки `ray.secondary=0`.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvXrSecondaryRay : MonoBehaviour
    {
        /// <summary>Длина луча, метры (как у основного).</summary>
        public float rayLength = 10f;

        /// <summary>Камера шлема (для разворота метки к оператору).</summary>
        public Camera headCamera;

        /// <summary>Сообщать в журнал о состоянии второго луча.</summary>
        public bool logOnStart = true;

        /// <summary>ВТОРОЙ ЛУЧ ПОПАЛ В ГЕОМЕТРИЮ СЦЕНЫ (§42, Д16): стол, робот, пол.</summary>
        public bool AimingAtWorld { get { return aimingAtWorld; } }

        private bool aimingAtWorld;
        private LineRenderer line;
        private Transform marker;
        private Renderer markerRenderer;
        private Material material;
        private Material markerMaterial;
        private KvXrGroupPanel panel;
        private bool logged;

        /// <summary>Прошлое состояние курка второго луча (передний фронт нажатия).</summary>
        private bool triggerWasDown;

        /// <summary>ПЕРЕНОС ОКНА ВТОРОЙ РУКОЙ — тот же механизм, что у основного луча.</summary>
        private readonly KvXrWindowDrag windowDrag = new KvXrWindowDrag();

        /// <summary>Длина второго луча в этом кадре (для журнала и отчёта).</summary>
        public float CurrentLength { get; private set; }

        /// <summary>Из какой руки идёт второй луч.</summary>
        public KvXrQuickHand SourceHand { get; private set; }

        /// <summary>Найти или создать второй луч (идемпотентно, сцена не меняется).</summary>
        public static KvXrSecondaryRay Ensure(Transform parent, Camera camera)
        {
            KvXrSecondaryRay existing = Object.FindAnyObjectByType<KvXrSecondaryRay>();
            if (existing != null)
            {
                if (existing.headCamera == null) existing.headCamera = camera;
                return existing;
            }

            var go = new GameObject("XrSecondaryRay");
            if (parent != null) go.transform.SetParent(parent, false);
            KvXrSecondaryRay ray = go.AddComponent<KvXrSecondaryRay>();
            ray.headCamera = camera;
            return ray;
        }

        void Start()
        {
            if (headCamera == null) headCamera = Camera.main;
            EnsureLine();
        }

        void Update()
        {
            // Выключение файлом подстройки — без пересборки.
            if (!KvXrTuning.SecondaryRayEnabled)
            {
                if (line != null) line.enabled = false;
                if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            // ССЫЛКА БОЛЬШЕ НЕ НУЖНА: панель выбирается по лучу ниже (реестр `KvXrPanels`).

            SourceHand = KvXrSecondaryRayLogic.SourceHand(KvXrInputSource.RayHand);

            // РЕЖИМ КИСТЕЙ (§40): контроллер отложен — второй луч не рисуется. Иначе из двух кистей
            // шли бы две длинные линии, и это ровно то, что владелец назвал «лучи идут длинные как у
            // контроллеров (упираются в текстуры)».
            if (KvXrMode.For(SourceHand) == KvXrControlMode.Hands)
            {
                if (line != null) line.enabled = false;
                if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            Vector3 origin;
            Quaternion rotation;
            bool hasPose = KvXrInputSource.TryAimPose(SourceHand, out origin, out rotation);
            if (!hasPose)
            {
                if (line != null) line.enabled = false;
                if (marker != null && marker.gameObject.activeSelf) marker.gameObject.SetActive(false);
                return;
            }

            Vector3 direction = rotation * Vector3.forward;

            // Наведение СЧИТАЕТСЯ, но НЕ НАЖИМАЕТ: панель отвечает на номер иконки, а нажатия здесь
            // нет вовсе — `KvXrSecondaryRayLogic.CanPress()` всегда false.
            // ПАНЕЛЬ ИЩЕТСЯ ПО ЛУЧУ, А НЕ «ОДНА НА ВСЕХ» (дефект 24.10.2026: «левой рукой нельзя
            // выбирать пункты меню»). Раньше здесь стояла ОДНА ссылка (`FindAnyObjectByType`), а
            // панелей теперь несколько — панель у руки, меню и окна. Ссылка могла указывать не на ту,
            // и второй луч «не видел» кнопок меню.
            KvXrGroupPanel aimed = null;
            int index = -1;
            float fromCenter = 0f;
            float along = 0f;
            Vector3 hitPoint = origin;
            float bestAlong = float.MaxValue;
            aimingAtWorld = false;
            Vector3 worldHitPoint = origin;

            for (int i = 0; i < KvXrPanels.Count; i++)
            {
                KvXrGroupPanel candidate = KvXrPanels.At(i);
                if (candidate == null || !candidate.isActiveAndEnabled) continue;

                int pIndex;
                float pFromCenter;
                float pAlong;
                Vector3 pPoint;
                if (!candidate.TryAim(origin, direction, out pIndex, out pFromCenter, out pAlong, out pPoint)) continue;
                if (pIndex < 0 || pAlong <= 0f || pAlong >= bestAlong) continue;

                bestAlong = pAlong;
                aimed = candidate;
                index = pIndex;
                fromCenter = pFromCenter;
                along = pAlong;
                hitPoint = pPoint;
            }

            panel = aimed;

            bool overIcon = index >= 0 && KvXrSecondaryRayLogic.CanPress() == false;   // подсветка, не нажатие

            // ЛЕВЫЙ КОНТРОЛЛЕР ТОЖЕ ВЫБИРАЕТ ПУНКТЫ МЕНЮ И ОКОН (указание владельца 24.10.2026:
            // «левым контроллером, хоть и есть примагничивание, нельзя выбирать пункты меню — исправить»).
            // Нажатие разрешено ТОЛЬКО по панелям в мире (меню, окна): по панели у руки нажимает та
            // рука, из которой идёт основной луч, иначе два луча нажимали бы одну кнопку.
            if (panel != null && !panel.followHand)
            {
                float trigger = SourceHand == KvXrQuickHand.Left
                    ? KvXrInputSource.LeftTrigger
                    : KvXrInputSource.RightTrigger;
                bool down = trigger >= 0.7f;

                if (down && !triggerWasDown && index >= 0)
                {
                    Debug.Log("[KvXr] НАЖАТИЕ ВТОРЫМ ЛУЧОМ: «" + panel.TitleAt(index) + "»");
                    panel.PressIndex(index);
                }

                triggerWasDown = down;
            }
            else
            {
                triggerWasDown = false;
            }
            float length = KvXrSecondaryRayLogic.LengthMeters(index >= 0, along, rayLength);

            // ВТОРОЙ ЛУЧ ТОЖЕ НЕ ПРОХОДИТ СКВОЗЬ ИНТЕРФЕЙС (указание владельца 24.10.2026: «лазер
            // левого контроллера не должен пролетать через меню»). Притягивание к кнопкам остаётся
            // у того луча, который нажимает (правая рука); второй лишь упирается в плиту меню, в окно
            // или в панель и не летит дальше.
            // ВТОРОЙ ЛУЧ ТОЖЕ УПИРАЕТСЯ В МИР (§42, Д16). Найдено 25.10.2026 после жалобы владельца
            // «лазеры не упирались в текстуры»: правый луч исправлен, а левый по-прежнему знал только
            // про панели и пролетал сквозь стол, робота и пол на всю длину. Теперь ближайшее попадание
            // в геометрию сцены так же обрывает луч, как и попадание в панель.
            if (index < 0)
            {
                int mask = ~(1 << KvXrControllerVisuals.IgnoreRaycastLayer);
                RaycastHit worldHit;
                if (Physics.Raycast(origin, direction, out worldHit, Mathf.Max(0.05f, length), mask,
                        QueryTriggerInteraction.Ignore))
                {
                    length = Mathf.Max(0.05f, Vector3.Distance(origin, worldHit.point));
                    worldHitPoint = worldHit.point;
                    aimingAtWorld = true;
                }
            }

            if (index < 0)
            {
                for (int p = 0; p < KvXrPanels.Count; p++)
                {
                    KvXrGroupPanel target = KvXrPanels.At(p);
                    if (target == null || !target.isActiveAndEnabled) continue;

                    float surfaceAlong;
                    if (!target.TryHitPanelSurface(origin, direction, out surfaceAlong)) continue;
                    if (surfaceAlong <= 0f || surfaceAlong >= length) continue;

                    length = Mathf.Max(0.05f, surfaceAlong);
                    aimingAtWorld = false;
                }
            }



            // ПЕРЕНОС ОКНА ЛЮБОЙ РУКОЙ (указание владельца 24.10.2026: «добавь ещё возможность переноса
            // окна и на левый контроллер»): средний палец — в любой точке окна, указательный — за верх окна.
            float gripValue = SourceHand == KvXrQuickHand.Left ? KvXrInputSource.LeftGrip : KvXrInputSource.RightGrip;

            float triggerValue = SourceHand == KvXrQuickHand.Left ? KvXrInputSource.LeftTrigger : KvXrInputSource.RightTrigger;
            bool dragging = windowDrag.Tick(panel, origin, direction,
                gripValue >= KvXrWindowDrag.ButtonThreshold, triggerValue >= KvXrWindowDrag.ButtonThreshold);

            Vector3 end = dragging
                ? windowDrag.CurrentPoint
                : (index >= 0 ? panel.IconWorldPosition(index) : origin + direction.normalized * length);
            CurrentLength = Vector3.Distance(origin, end);

            if (line != null)
            {
                line.enabled = true;
                line.SetPosition(0, origin);
                line.SetPosition(1, end);
            }

            if (marker != null)
            {
                // ШАРИК ВТОРОЙ РУКИ — С ТЕМИ ЖЕ УСЛОВИЯМИ (указание владельца 24.10.2026): только когда луч
                // НЕ на интерфейсе и окно не несут.
                bool showMarker = panel == null && !dragging;
                if (marker.gameObject.activeSelf != showMarker) marker.gameObject.SetActive(showMarker);
                if (!showMarker) return;
                marker.position = end;

                // КАМЕРА БЕРЁТСЯ ИЗ КЭША, А НЕ ЧЕРЕЗ `Camera.main` КАЖДЫЙ КАДР (исправлено
                // 30.10.2026, S44).
                //
                // ЧТО БЫЛО. `Camera.main` — это НЕ свойство, а ПОИСК по тегу `MainCamera` среди
                // всех камер сцены. Вызов стоял внутри `Update`, то есть выполнялся на каждом
                // кадре: в шлеме это 90 раз в секунду. В этом проекте камер несколько (вид сверху,
                // сбоку, от первого лица), поэтому поиск небесплатен, и его цена попадает прямо в
                // бюджет кадра.
                //
                // ЧТО СТАЛО. Камера ищется один раз, а результат ЗАПОМИНАЕТСЯ в то же поле, что
                // заполняет `Start`. После первого кадра поиска нет вовсе.
                if (headCamera == null) headCamera = Camera.main;
                Camera camera = headCamera;
                if (camera != null)
                {
                    Vector3 toEye = camera.transform.position - end;
                    if (toEye.sqrMagnitude > 0.0001f)
                        marker.rotation = Quaternion.LookRotation(toEye.normalized, camera.transform.up);
                }
            }

            if (logOnStart && !logged)
            {
                logged = true;
                Debug.Log("[KvXr] Указатель: второй луч из " +
                          (SourceHand == KvXrQuickHand.Left ? "ЛЕВОЙ" : "ПРАВОЙ") +
                          " руки — ТОЛЬКО УКАЗАТЕЛЬ (нажатие остаётся у руки луча: " +
                          (KvXrInputSource.RayHand == KvXrQuickHand.Right ? "правая" : "левая") + ") · " +
                          "толщина " + KvXrSecondaryRayLogic.WidthMeters.ToString("0.000") + " м · " +
                          "длина " + CurrentLength.ToString("0.00") + " м");
            }

            // ЦВЕТ — РУКИ, КОТОРОЙ ПРИНАДЛЕЖИТ ВТОРОЙ ЛУЧ (§41, указание владельца: «перекрасим лазер
            // правого и левого контроллеров как в комп версии»). Это рука, ПРОТИВОПОЛОЖНАЯ лучу
            // нажатия: если нажимает правая (зелёный), второй луч — левый (красный), и наоборот.
            // Наведение делает тот же цвет чуть ярче — цвет руки не подменяется.
            if (line != null && material != null)
            {
                Color color = KvXrLaserColors.Ray(
                    KvXrLaserColors.Opposite(KvXrInputSource.RayHand), overIcon);
                line.startColor = color;
                line.endColor = color;
            }
        }

        private void EnsureLine()
        {
            if (line != null) return;

            line = GetComponent<LineRenderer>();
            if (line == null) line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.startWidth = KvXrSecondaryRayLogic.WidthMeters;
            line.endWidth = KvXrSecondaryRayLogic.WidthMeters * 0.5f;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            // БЕЛЫЙ МАТЕРИАЛ: цвет задаётся вершинами луча (`startColor`/`endColor`). Иначе цвет
            // материала и цвет вершины перемножаются, и второй луч получается не того цвета, какой
            // задан правилом (та же ошибка, что была у основного луча 24.10.2026).
            material = KvRenderPipelineShaders.CreateUnlit("Материал_второго_луча_XR", Color.white);
            if (material != null) line.material = material;

            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "KvXrSecondaryRayMarker";
            Collider collider = go.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            go.layer = KvXrControllerVisuals.IgnoreRaycastLayer;
            go.transform.localScale = Vector3.one * KvXrSecondaryRayLogic.MarkerMeters;
            go.SetActive(false);

            markerRenderer = go.GetComponent<Renderer>();
            markerMaterial = KvRenderPipelineShaders.CreateUnlit("Материал_метки_второго_луча_XR",
                KvXrLaserColors.Ray(KvXrLaserColors.Opposite(KvXrInputSource.RayHand), true));
            if (markerRenderer != null && markerMaterial != null) markerRenderer.sharedMaterial = markerMaterial;

            marker = go.transform;
        }
    }
}



