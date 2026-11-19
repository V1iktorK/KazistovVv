using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using KazistovVvFeatures;
using KazistovVvRealWorld;

namespace KazistovVvAr
{
    /// <summary>СОСТОЯНИЕ ПОСТАНОВКИ МИРА (по нему строится подсказка на экране).</summary>
    public enum KvArPlacementState
    {
        /// <summary>Плоскостей ещё нет: оператор водит камерой по столу.</summary>
        WaitingForPlanes = 0,

        /// <summary>Плоскость есть, маркер стоит на столе: тап ставит робота.</summary>
        ReadyToPlace = 1,

        /// <summary>Мир поставлен (робот стоит на столе).</summary>
        Placed = 2,

        /// <summary>Постановка не удалась — причина в журнале и на экране.</summary>
        Failed = 3
    }

    /// <summary>
    /// ПОПАДАНИЕ В РЕАЛЬНУЮ ПОВЕРХНОСТЬ (одно место, где живут все числа постановки).
    ///
    /// Зачем свой тип, а не `ARRaycastHit`: `ARRaycastHit` — структура AR Foundation, её нельзя
    /// собрать в EditMode-тесте без запущенной AR-сессии. А эта структура — обычные числа, и
    /// правила постановки проверяются тестом без телефона (правило проекта).
    /// </summary>
    public struct KvArSurfaceHit
    {
        /// <summary>Попадание в плоскость действительно найдено.</summary>
        public bool valid;

        /// <summary>Точка попадания, мировые координаты, метры.</summary>
        public Vector3 point;

        /// <summary>Нормаль поверхности в точке попадания.</summary>
        public Vector3 normal;

        /// <summary>Поза попадания: позиция + ориентация по плоскости (мир).</summary>
        public Pose worldPose;

        /// <summary>Найденная плоскость (null — попадание без плоскости, запасной путь).</summary>
        public ARPlane plane;

        /// <summary>Площадь плоскости, м² (0 — неизвестна) — для журнала A2.</summary>
        public float planeArea;

        /// <summary>Выравнивание плоскости: Horizontal / Vertical / NotAxisAligned.</summary>
        public PlaneAlignment alignment;

        /// <summary>Расстояние от камеры до точки, метры.</summary>
        public float distance;

        /// <summary>Строка для журнала: где плоскость и насколько далеко.</summary>
        public string Describe()
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("плоскость ").Append(AlignmentLabel(alignment));
            sb.Append(" · точка ").Append(point.ToString("0.000"));
            sb.Append(" · нормаль ").Append(normal.ToString("0.000"));
            sb.Append(" · расстояние ").Append(distance.ToString("0.00",
                System.Globalization.CultureInfo.InvariantCulture)).Append(" м");
            if (planeArea > 0f)
                sb.Append(" · площадь ").Append(planeArea.ToString("0.00",
                    System.Globalization.CultureInfo.InvariantCulture)).Append(" м²");
            if (plane == null) sb.Append(" · БЕЗ плоскости (запасной якорь по позе)");
            return sb.ToString();
        }

        private static string AlignmentLabel(PlaneAlignment alignment)
        {
            if (alignment == PlaneAlignment.HorizontalUp) return "горизонтальная (вверх)";
            if (alignment == PlaneAlignment.HorizontalDown) return "горизонтальная (вниз)";
            if (alignment == PlaneAlignment.Vertical) return "вертикальная";
            return "не определена";
        }
    }

    /// <summary>
    /// ПРАВИЛА ПОСТАНОВКИ — ЧИСТАЯ МАТЕМАТИКА. Ровно то, что проверяется тестами без устройства.
    ///
    /// Здесь зафиксированы три инварианта приёмки:
    ///   Б1: основание робота = точка попадания (ни «в воздухе», ни «в полу»);
    ///   Б3: положение робота не зависит от камеры (в формулу позы камера не входит);
    ///   В2: пропорции виртуального стола = 10:7, как у реального 100×70 см.
    /// </summary>
    public static class KvArPlacementMath
    {
        /// <summary>Эталонная пропорция стола из промта: 100 × 70 см = 10:7.</summary>
        public const float TableWidthOverDepth = 10f / 7f;

        /// <summary>Допуск на пропорции стола (1 %).</summary>
        public const float TableRatioTolerance = 0.01f;

        /// <summary>Допуск на отклонение основания от плоскости, метры (правило приёмки — 5 мм).</summary>
        public const float BaseHeightToleranceMeters = 0.005f;

        /// <summary>
        /// ПОЗА ОСНОВАНИЯ РОБОТА. Позиция — РОВНО точка поверхности, без смещений и без участия
        /// камеры. Поворот робота считается отдельно (`YawTowardsCamera`), поэтому даже если
        /// камера уедет, позиция останется точкой стола.
        /// </summary>
        public static Vector3 RobotBasePosition(Vector3 surfacePoint)
        {
            return surfacePoint;
        }

        /// <summary>
        /// ПОВОРОТ РОБОТА К ОПЕРАТОРУ: направление от точки робота к камере, спроецированное на
        /// горизонталь. Поворот НЕ влияет на позицию (Б3) — это только ориентация.
        /// </summary>
        public static float YawTowardsCamera(Vector3 surfacePoint, Vector3 cameraPosition)
        {
            Vector3 direction = cameraPosition - surfacePoint;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return 0f;
            return Quaternion.LookRotation(direction.normalized, Vector3.up).eulerAngles.y;
        }

        /// <summary>ОРИЕНТАЦИЯ МАРКЕРА: маркер лежит на плоскости (его +Z смотрит по нормали).</summary>
        public static Quaternion ReticleRotation(Vector3 normal)
        {
            if (normal.sqrMagnitude < 0.0001f) return Quaternion.identity;
            return Quaternion.FromToRotation(Vector3.forward, normal.normalized);
        }

        /// <summary>ОТКЛОНЕНИЕ ОСНОВАНИЯ ОТ ПЛОСКОСТИ, метры (Б2/Б3, порог 5 мм).</summary>
        public static float BaseHeightErrorMeters(Vector3 robotBaseWorld, float planeY)
        {
            return Mathf.Abs(robotBaseWorld.y - planeY);
        }

        /// <summary>
        /// ТОЧКА ВНУТРИ ПЛОСКОСТИ (07.11.2026). Дефект планшета: объекты из ПК-меню (столы)
        /// создавались в 1,2 м перед устройством «по плоскости якоря» без проверки границ, и на
        /// фото их ножки висели над полом за краем настоящего стола. Здесь точка приводится
        /// внутрь найденной плоскости с отступом `marginMeters`; если плоскость неизвестна —
        /// точка возвращается как есть (прежнее поведение, ничего не ломаем).
        /// </summary>
        public static Vector3 ClampInsidePlane(Vector3 point, Vector3 planeCenter, float halfWidth,
                                               float halfDepth, Quaternion planeRotation,
                                               float marginMeters)
        {
            if (halfWidth <= 0f || halfDepth <= 0f) return point;

            float marginX = Mathf.Min(Mathf.Max(0f, marginMeters), halfWidth);
            float marginZ = Mathf.Min(Mathf.Max(0f, marginMeters), halfDepth);
            float limitX = halfWidth - marginX;
            float limitZ = halfDepth - marginZ;
            if (limitX <= 0f || limitZ <= 0f) return planeCenter;

            Vector3 local = Quaternion.Inverse(planeRotation) * (point - planeCenter);
            local.x = Mathf.Clamp(local.x, -limitX, limitX);
            local.z = Mathf.Clamp(local.z, -limitZ, limitZ);
            local.y = 0f;                                   // высоту задаёт плоскость, не точка
            return planeCenter + planeRotation * local;
        }

        /// <summary>Допуск на границу плоскости, метры (0,1 мм — против ошибок округления поворота).</summary>
        public const float PlaneBoundaryEpsilon = 0.0001f;

        /// <summary>ТОЧКА ВНЕ ГРАНИЦ ПЛОСКОСТИ (отступ не учитываем — грубая проверка).</summary>
        public static bool IsOutsidePlane(Vector3 point, Vector3 planeCenter, float halfWidth,
                                          float halfDepth, Quaternion planeRotation)
        {
            if (halfWidth <= 0f || halfDepth <= 0f) return false;
            Vector3 local = Quaternion.Inverse(planeRotation) * (point - planeCenter);
            return Mathf.Abs(local.x) > halfWidth + PlaneBoundaryEpsilon ||
                   Mathf.Abs(local.z) > halfDepth + PlaneBoundaryEpsilon;
        }

        /// <summary>ОСНОВАНИЕ СТОИТ НА ПЛОСКОСТИ (отклонение в пределах 5 мм).</summary>
        public static bool BaseIsOnPlane(Vector3 robotBaseWorld, float planeY)
        {
            return BaseHeightErrorMeters(robotBaseWorld, planeY) <= BaseHeightToleranceMeters;
        }

        /// <summary>ОШИБКА ПРОПОРЦИЙ СТОЛА: |ширина/глубина − 10/7| (В2).</summary>
        public static float TableProportionError(float widthMeters, float depthMeters)
        {
            if (widthMeters <= 0f || depthMeters <= 0f) return float.MaxValue;
            return Mathf.Abs(widthMeters / depthMeters - TableWidthOverDepth);
        }

        /// <summary>ПРОПОРЦИИ СТОЛА СОВПАДАЮТ С РЕАЛЬНЫМ 10:7 (допуск 1 %).</summary>
        public static bool TableProportionsMatch(float widthMeters, float depthMeters)
        {
            return TableProportionError(widthMeters, depthMeters) <= TableRatioTolerance;
        }

        /// <summary>
        /// ПОКАЗЫВАТЬ ЛИ МАРКЕР. Только пока мир не поставлен и есть попадание в плоскость:
        /// после постановки маркер обязан исчезнуть, иначе оператор думает, что «ещё ставится».
        /// </summary>
        public static bool ShouldShowReticle(bool worldPlaced, bool hasHit)
        {
            return !worldPlaced && hasHit;
        }

        /// <summary>ПОДСКАЗКА НА ЭКРАНЕ ПО СОСТОЯНИЮ И ЧИСЛАМ (чистая функция — проверяется тестом).</summary>
        /// <param name="tableFound">
        /// СТОЛ ДЕЙСТВИТЕЛЬНО НАЙДЕН (а не «плоскости есть»). ЧТО БЫЛО: подсказка говорила «Стол
        /// найден (3 плоскости)» уже при появлении ЛЮБЫХ плоскостей — а первым ARCore находит пол.
        /// Оператор читал «стол найден» и ставил робота на пол. ЧТО СТАЛО: отсутствие проверенного
        /// стола проговаривается словами. Параметр необязательный (по умолчанию прежнее поведение),
        /// чтобы не ломать вызовы без классификатора.
        /// </param>
        public static string Hint(KvArPlacementState state, int planesFound, float lastPlaneSeconds,
                                  bool tableFound = true)
        {
            switch (state)
            {
                case KvArPlacementState.WaitingForPlanes:
                    return "Наведите камеру на стол и поводите ею 2–3 секунды · найдено плоскостей: " +
                           planesFound;
                case KvArPlacementState.ReadyToPlace:
                    if (!tableFound)
                        return "Стол не найден: вижу " + planesFound +
                               " поверхностей, но столешницы среди них нет · поводите камерой по столу 2–3 секунды";
                    return "Стол найден (" + planesFound + (planesFound == 1 ? " плоскость" : " плоскостей") +
                           ") · «Поставить» — робот встанет на самый большой стол или коснитесь стола";
                case KvArPlacementState.Placed:
                    return "Робот стоит на столе · управляйте касаниями · «Переставить» — выбрать другое место";
                default:
                    return "Постановка не удалась — коснитесь стола ещё раз";
            }
        }
    }

    /// <summary>
    /// ПОСТАНОВКА МИРА НА РЕАЛЬНЫЙ СТОЛ: СКАНИРОВАНИЕ, МАРКЕР, ТАП, ЯКОРЬ.
    ///
    /// ПОРЯДОК РАБОТЫ (совпадает с чек-листом А2/Б1 приёмки):
    ///   1. ждём, пока ARCore отдаст горизонтальные плоскости (A2);
    ///   2. каждый кадр стреляем из ЦЕНТРА экрана по плоскостям (`ARRaycastManager`);
    ///   3. на попадании показываем маркер (кольцо/пятак), повёрнутый по нормали стола;
    ///   4. тап (по экрану или по кнопке «Поставить») создаёт якорь через `ARAnchorManager`
    ///      и ставит робота основанием в точку стола (Б1/Б2);
    ///   5. дальше мир живёт под якорем и от камеры не зависит (Б3/Б4).
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArPlacement : MonoBehaviour
    {
        /// <summary>AR-камера (из неё идёт луч).</summary>
        public Camera arCamera;

        /// <summary>Менеджер лучей AR Foundation.</summary>
        public ARRaycastManager raycastManager;

        /// <summary>Менеджер плоскостей: по нему считаем найденные плоскости (A2).</summary>
        public ARPlaneManager planeManager;

        /// <summary>Мир, который ставится.</summary>
        public KvArWorld world;

        /// <summary>Экранный интерфейс.</summary>
        public KvArUi ui;

        /// <summary>Визуализация плоскостей и выбор самого большого стола (авто-постановка).</summary>
        public KvArPlaneVisualizer planeVisualizer;

        /// <summary>Разрешить постановку на самый большой найденный стол, если луч в центр не попал.</summary>
        public bool allowAutoTable = true;

        /// <summary>Как часто обновлять строку состояния, секунды.</summary>
        public float statusIntervalSeconds = 0.5f;

        /// <summary>Писать в журнал о найденных плоскостях и постановке.</summary>
        public bool logOnChange = true;

        /// <summary>Размер маркера в метрах (пятак на столе).</summary>
        public float reticleSizeMeters = 0.12f;

        /// <summary>Текущее состояние постановки.</summary>
        public KvArPlacementState State { get; private set; }

        /// <summary>Сколько плоскостей найдено сейчас.</summary>
        public int PlanesFound { get; private set; }

        /// <summary>Текущее попадание под центр экрана (валидность — в поле `valid`).</summary>
        public KvArSurfaceHit Current { get; private set; }

        /// <summary>Сколько секунд от запуска до первой найденной плоскости (A2, число для отчёта).</summary>
        public float FirstPlaneSeconds { get; private set; }

        /// <summary>Последняя созданная плоскость: площадь, м² (для журнала).</summary>
        public float LargestPlaneArea { get; private set; }

        /// <summary>Сколько раз ставили мир (для журнала).</summary>
        public int PlaceAttempts { get; private set; }

        /// <summary>Физический размер QR-маркера на столешнице, метры (сторона кода).</summary>
        public float markerSizeMeters = 0.10f;

        /// <summary>ПОСТАНОВКА ПО МАРКЕРУ: последняя поза (для статуса и журнала).</summary>
        public KvMarkerPoseResult LastMarkerPose { get; private set; }

        /// <summary>ПОСТАНОВКА ПО МАРКЕРУ: последний честный статус — успех или конкретный отказ.</summary>
        public string LastMarkerStatus { get; private set; }

        /// <summary>Буферы кадра для скана QR (переиспользуются, см. `KvMarkerFrame.Buffers`).</summary>
        private readonly KvMarkerFrame.Buffers frameBuffers = new KvMarkerFrame.Buffers();

        /// <summary>Мир поставлен.</summary>
        public bool IsPlaced { get { return world != null && world.IsPlaced; } }

        private GameObject reticle;
        private Material reticleMaterial;
        private bool reticleVisible;
        private float startTime;
        private bool firstPlaneLogged;
        private string lastHint = string.Empty;
        private float nextStatusTime;

        /// <summary>О не-столе предупреждаем ОДИН раз: иначе каждое касание пола засоряет журнал.</summary>
        private bool warnedNotATable;

        /// <summary>До какого времени статус маркера не затирается периодической подсказкой.</summary>
        private float markerHintUntil;

        void Awake()
        {
            startTime = Time.unscaledTime;
            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();
            if (raycastManager == null) raycastManager = FindAny<ARRaycastManager>();
            if (planeManager == null) planeManager = FindAny<ARPlaneManager>();
            if (world == null) world = GetComponent<KvArWorld>();
            if (planeVisualizer == null) planeVisualizer = FindAnyObjectByType<KvArPlaneVisualizer>();
            EnsureReticle();
        }

        void Update()
        {
            PlanesFound = CountPlanes();
            if (!firstPlaneLogged && PlanesFound > 0)
            {
                firstPlaneLogged = true;
                FirstPlaneSeconds = Time.unscaledTime - startTime;
                LogFirstPlane();
            }

            UpdateCurrentHit();
            UpdateReticle();

            if (IsPlaced)
            {
                if (State != KvArPlacementState.Placed) SetState(KvArPlacementState.Placed);
            }
            else if (PlanesFound > 0)
            {
                if (State == KvArPlacementState.WaitingForPlanes) SetState(KvArPlacementState.ReadyToPlace);
            }
            else if (State != KvArPlacementState.Failed)
            {
                if (State != KvArPlacementState.WaitingForPlanes) SetState(KvArPlacementState.WaitingForPlanes);
            }

            if (ui != null && Time.unscaledTime >= nextStatusTime)
            {
                nextStatusTime = Time.unscaledTime + Mathf.Max(0.1f, statusIntervalSeconds);
                // СТАТУС МАРКЕРА ЕЩЁ ЧИТАЮТ: пока не вышло markerHintUntil, периодическая
                // подсказка его не затирает — иначе отказ «маркер не виден» живёт0.5 секунды.
                if (Time.unscaledTime < markerHintUntil) return;
                string hint = KvArPlacementMath.Hint(State, PlanesFound, FirstPlaneSeconds, TableFound);
                if (hint != lastHint)
                {
                    lastHint = hint;
                    ui.SetHint(hint);
                }
            }
        }

        /// <summary>
        /// ЕСТЬ ЛИ ПРОВЕРЕННЫЙ СТОЛ (а не просто плоскости). Считается по классификатору в
        /// `KvArPlaneVisualizer`: пол столом больше не считается. Если визуализатора в сцене нет,
        /// возвращаем true — прежнее поведение (иначе подсказка врала бы «стол не найден» всегда).
        /// </summary>
        public bool TableFound
        {
            get
            {
                if (planeVisualizer == null) return true;
                return planeVisualizer.BestTable != null;
            }
        }

        /// <summary>СЧИТАТЬ ПЛОСКОСТИ (A2): сколько реально найдено сейчас.</summary>
        public int CountPlanes()
        {
            if (planeManager == null) return 0;
            return planeManager.trackables.count;
        }

        /// <summary>Обновить попадание под центром экрана и запомнить самую большую плоскость.</summary>
        private void UpdateCurrentHit()
        {
            KvArSurfaceHit hit;
            Current = TryRaycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out hit) ? hit : default;
        }

        /// <summary>Буфер лучей: один на компонент (P2-6, 11.11.2026).</summary>
        private readonly List<ARRaycastHit> raycastHits = new List<ARRaycastHit>();

        /// <summary>ЛУЧ ПО ПЛОСКОСТЯМ ИЗ ТОЧКИ ЭКРАНА. Публично: этим пользуются касания.</summary>
        public bool TryRaycast(Vector2 screenPoint, out KvArSurfaceHit surface)
        {
            surface = default;
            if (raycastManager == null || arCamera == null) return false;

            // P2-6. ЧТО БЫЛО: `new List<ARRaycastHit>()` на КАЖДЫЙ вызов, а вызывается он из
            // `Update` (прицел) — то есть мусор каждый кадр на устройстве, где кадровый бюджет
            // 30–72 Гц. ЧЕМ ЭТО ГРОЗИЛО: лишние сборки мусора дают «столб» кадра как раз в момент
            // наведения, когда оператор целится. ТЕПЕРЬ буфер переиспользуется.
            List<ARRaycastHit> hits = raycastHits;
            hits.Clear();
            bool hit = raycastManager.Raycast(screenPoint, hits,
                TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds |
                TrackableType.PlaneEstimated);
            if (!hit || hits.Count == 0) return false;

            ARRaycastHit best = hits[0];
            ARPlane plane = best.trackable as ARPlane;
            surface.valid = true;
            surface.point = best.pose.position;
            surface.worldPose = best.pose;
            surface.normal = best.pose.up;
            surface.plane = plane;
            surface.distance = best.distance;
            surface.alignment = plane != null ? plane.alignment : PlaneAlignment.HorizontalUp;
            if (plane != null)
            {
                float width = plane.size.x * plane.transform.lossyScale.x;
                float height = plane.size.y * plane.transform.lossyScale.z;
                surface.planeArea = Mathf.Abs(width * height);
            }
            return true;
        }

        /// <summary>
        /// ПОСТАВИТЬ МИР ТАПОМ ПО ЭКРАНУ (Б1). Возвращает true, если якорь создан.
        /// </summary>
        public bool TryPlaceAt(Vector2 screenPoint)
        {
            if (IsPlaced)
            {
                Debug.Log("[KvAr] ПОСТАНОВКА: мир уже поставлен — сначала «Переставить»");
                return false;
            }

            PlaceAttempts++;
            KvArSurfaceHit surface;
            if (!TryRaycast(screenPoint, out surface))
            {
                Debug.LogWarning("[KvAr] ПОСТАНОВКА: в точке тапа плоскости нет — робот не поставлен");
                if (ui != null) ui.SetHint("Коснитесь именно стола: поверхности под пальцем нет");
                return false;
            }

            return PlaceSurface(surface, "тап по экрану");
        }

        /// <summary>
        /// ПОСТАВИТЬ МИР ПО ЦЕНТРУ ЭКРАНА (кнопка «Поставить»).
        ///
        /// ПОРЯДОК (06.11.2026, запрос владельца — «робот автоматически на самый большой
        /// горизонтальный стол»):
        ///   1. если луч из центра экрана попал в плоскость — ставим туда (точный контроль);
        ///   2. если не попал (камера смотрит мимо/стол большой, а центр на стене) — берём
        ///      самый большой найденный горизонтальный стол и ставим якорь в его центр;
        ///   3. если стола нет — подсказка «поводите камерой».
        /// </summary>
        public bool TryPlaceAtCenter()
        {
            if (IsPlaced)
            {
                Debug.Log("[KvAr] ПОСТАНОВКА: мир уже поставлен — сначала «Переставить»");
                return false;
            }

            PlaceAttempts++;
            KvArSurfaceHit surface;
            if (TryRaycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.5f), out surface))
                return PlaceSurface(surface, "центр экрана");

            if (allowAutoTable && TryAutoTable(out surface))
                return PlaceSurface(surface, "авто: самый большой стол, прошедший проверку");

            // ЧЕСТНЫЙ ОТКАЗ. ЧТО БЫЛО: «стола не видно — поводите камерой», без чисел. Оператор не
            // мог понять, что именно не так: пол найден, но столом не считается; габариты не те;
            // поверхности ещё уточняются. ТЕПЕРЬ причина берётся у классификатора — с числами.
            string reason = planeVisualizer != null
                ? planeVisualizer.DescribeTable
                : "визуализатор плоскостей не найден · уверенность 0.00";
            Debug.LogWarning("[KvAr] ПОСТАНОВКА: стола не видно — " + reason +
                             " · пол столом не считается: нужна столешница выше пола на 0.25 м и ниже глаз на 0.35–1.50 м");
            if (ui != null)
                ui.SetHint("Стол не найден: " + reason + " · поводите камерой по столу 2–3 секунды, затем «Поставить»");
            return false;
        }

        /// <summary>
        /// НАЙТИ СТОЛ ИЗ КЛАССИФИКАТОРА (если луч в центр не попал).
        ///
        /// ЧТО БЫЛО: бралась «самая большая найденная горизонтальная плоскость» — то есть, как
        /// правило, ПОЛ, и робот вставал на пол с подписью «стол».
        /// ЧТО СТАЛО: `TryGetBestTable` отдаёт только поверхность, прошедшую классификатор; её
        /// решение (площадь, высота над полом, уверенность) печатается в журнал рядом с фактом
        /// постановки — по этой строке тестировщик проверяет результат.
        /// </summary>
        private bool TryAutoTable(out KvArSurfaceHit surface)
        {
            surface = default;
            if (planeVisualizer == null) planeVisualizer = FindAnyObjectByType<KvArPlaneVisualizer>();
            if (planeVisualizer == null) return false;

            ARPlane plane;
            Vector3 center;
            Vector3 normal;
            float area;
            if (!planeVisualizer.TryGetBestTable(out plane, out center, out normal, out area))
                return false;

            surface.valid = true;
            surface.point = center;
            surface.normal = normal.sqrMagnitude > 0.01f ? normal.normalized : Vector3.up;
            surface.plane = plane;
            surface.planeArea = area;
            surface.alignment = plane != null ? plane.alignment : PlaneAlignment.HorizontalUp;
            surface.worldPose = new Pose(center, KvArPlacementMath.ReticleRotation(surface.normal));
            surface.distance = arCamera != null
                ? Vector3.Distance(arCamera.transform.position, center)
                : area;
            Debug.Log("[KvAr] ПОСТАНОВКА: луч в центр не попал — ставлю на самый большой стол, " +
                      "прошедший проверку: " + planeVisualizer.DescribeTable +
                      " · причина: " + planeVisualizer.BestTableReason);
            return true;
        }

        private bool PlaceSurface(KvArSurfaceHit surface, string source, bool allowWithoutAnchor = false)
        {
            if (world == null)
            {
                Debug.LogError("[KvAr] ПОСТАНОВКА: нет компонента мира");
                SetState(KvArPlacementState.Failed);
                return false;
            }

            Vector3 cameraPosition = arCamera != null ? arCamera.transform.position : Vector3.zero;
            string reason;
            if (!world.TryPlace(surface, cameraPosition, out reason, allowWithoutAnchor))
            {
                Debug.LogWarning("[KvAr] ПОСТАНОВКА НЕ УДАЛАСЬ (" + source + "): " + reason);
                SetState(KvArPlacementState.Failed);
                if (ui != null) ui.SetHint("Постановка не удалась: " + reason);
                return false;
            }

            SetState(KvArPlacementState.Placed);
            if (ui != null) ui.HideHint();

            // ЧЕСТНАЯ ПРОВЕРКА ПОВЕРХНОСТИ. ЧТО БЫЛО: робот ставился в точку тапа и назывался
            // «поставленным на стол» — даже если тап попал в ПОЛ (пол в ARCore находится первым и
            // занимает пол-экрана). ЧТО СТАЛО: тап по полу по-прежнему работает (это законный
            // запасной путь оператора), но в журнале и на экране сказано, что стол не подтверждён.
            string check = DescribeSurfaceCheck(surface);

            // МАСШТАБ 1:1: реальная высота модели робота в метрах (для владельца — «как настоящий»).
            if (world.Robot != null)
            {
                Bounds bounds = new Bounds(world.Robot.transform.position, Vector3.zero);
                Renderer[] renderers = world.Robot.GetComponentsInChildren<Renderer>();
                bool bound = false;
                for (int r = 0; r < renderers.Length; r++)
                {
                    if (renderers[r] == null || !renderers[r].enabled) continue;
                    if (!bound) { bounds = renderers[r].bounds; bound = true; }
                    else bounds.Encapsulate(renderers[r].bounds);
                }
                if (bound && logOnChange)
                    Debug.Log("[KvAr] МАСШТАБ: робот «" + world.Robot.robotName + "» высотой " +
                              bounds.size.y.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) +
                              " м (1:1, как в ПК-версии) · основание на столе");
            }

            // ЧИСЛА ПРИЁМКИ: основание робота относительно плоскости (Б2) и высота (В1).
            float baseError = world.Robot != null
                ? KvArPlacementMath.BaseHeightErrorMeters(world.Robot.transform.position, world.FloorY)
                : -1f;
            if (logOnChange)
            {
                Debug.Log("[KvAr] ПОСТАНОВКА ВЫПОЛНЕНА (" + source + ") · " + surface.Describe() + check +
                          " · якорь " + world.AnchorPosition.ToString("0.000") +
                          " · отклонение основания от плоскости " +
                          (baseError >= 0f
                              ? baseError.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) + " м"
                              : "не измерено") +
                          (baseError >= 0f && KvArPlacementMath.BaseIsOnPlane(
                              world.Robot.transform.position, world.FloorY) ? " (в пределах 5 мм)" : ""));
            }
            return true;
        }

        // ================================================================ ПОСТАНОВКА ПО МАРКЕРУ

        /// <summary>
        /// ПОСТАНОВКА СТОЛА ПО ПОЗЕ МАРКЕРА (B, волна16.11.2026). Маркер лежит на столешнице —
        /// значит верх стола лежит в плоскости маркера: центр и нормаль маркера переносятся в
        /// мир, дальше штатный путь `PlaceSurface` (журнал, статус, робот — как у тапа).
        ///
        /// ЧЕСТНОСТЬ (C). Каждый отказ возвращается false с конкретной причиной в
        /// `LastMarkerStatus` и в подсказке: маркер не виден / слишком мал / слишком под углом /
        /// интринсики приблизительные / нет камеры / нет мира. Успех — строка
        /// `KvMarkerPose.SuccessStatus` («дистанция … м, наклон …°») и в статус, и в журнал.
        /// </summary>
        public bool TryPlaceByMarker(Vector2[] imageCorners, KvMarkerIntrinsics intrinsics,
            bool requireExactIntrinsics = false)
        {
            return TryPlaceByMarker(KvMarkerPose.Compute(imageCorners, markerSizeMeters,
                intrinsics, requireExactIntrinsics));
        }

        /// <summary>
        /// ПОСТАНОВКА ПО УЖЕ ПОСЧИТАННОЙ ПОЗЕ МАРКЕРА. Отдельная перегрузка нужна, чтобы ОДИН И ТОТ
        /// ЖЕ путь «постановка по позе» шёл и от кнопки «По маркеру», и от `KvMarkerFrame` — покадровый
        /// якорь и разовая постановка считают позу одним кодом и не могут разойтись в порогах.
        /// </summary>
        public bool TryPlaceByMarker(KvMarkerPoseResult pose)
        {
            PlaceAttempts++;
            LastMarkerPose = pose;
            if (!pose.usable)
            {
                return MarkerRefuse(pose.reason);
            }

            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();
            if (arCamera == null)
            {
                return MarkerRefuse("нет AR-камеры — позу маркера не перенести в мир");
            }

            KvArSurfaceHit surface = BuildMarkerSurface(pose, arCamera.transform);
            if (!PlaceSurface(surface, "по маркеру", allowWithoutAnchor: true))
            {
                string why = world != null && !string.IsNullOrEmpty(world.LastFailure)
                    ? world.LastFailure
                    : "причина не названа";
                return MarkerRefuse("постановка по маркеру не удалась: " + why);
            }

            LastMarkerStatus = KvMarkerPose.SuccessStatus(pose.distance, pose.tiltDegrees,
                pose.intrinsicsApproximate);
            Debug.Log("[KvAr] " + LastMarkerStatus);
            if (ui != null)
            {
                ui.SetHint(LastMarkerStatus);
                markerHintUntil = Time.unscaledTime + 4f;
            }
            return true;
        }

        /// <summary>
        /// СКАН QR И ПОСТАНОВКА: свежий кадр AR-камеры → четыре угла кода (`KvQrMarkerDetector`)
        /// → `TryPlaceByMarker`. Путь общий с покадровым якорем (`KvMarkerFrame`): один код
        /// интринсик, порогов и системы координат углов у кнопки и у следования. Каждый шаг
        /// отказывает честно.
        /// </summary>
        public bool TryPlaceByMarkerFromCamera(bool requireExactIntrinsics = false)
        {
            ARCameraManager cameraManager = FindAnyObjectByType<ARCameraManager>();
            if (cameraManager == null)
            {
                return MarkerRefuse(KvMarkerFrame.NoCameraReason);
            }

            // Буферы кадра живут столько, сколько живёт компонент: разовая постановка не должна
            // выделять текстуру и массивы пикселей на каждый вызов (как делала раньше).
            KvMarkerPoseResult pose;
            string reason;
            if (!KvMarkerFrame.TryPoseFromCamera(cameraManager, markerSizeMeters, frameBuffers,
                    requireExactIntrinsics, out pose, out reason))
            {
                return MarkerRefuse(reason);
            }

            return TryPlaceByMarker(pose);
        }

        /// <summary>
        /// ПЛОЩАДКА ИЗ ПОЗЫ МАРКЕРА (чистая): точка — центр маркера в мире, нормаль — нормаль
        /// маркера (маркер на столешнице лицевой стороной вверх → верх стола в плоскости
        /// маркера). Плоскости AR нет (`plane = null`) — это и есть независимость от ARCore.
        /// </summary>
        public static KvArSurfaceHit BuildMarkerSurface(KvMarkerPoseResult pose, Transform camera)
        {
            KvArSurfaceHit hit = default(KvArSurfaceHit);
            hit.valid = true;
            hit.point = camera.TransformPoint(pose.position);
            Vector3 normal = camera.rotation * (pose.rotation * Vector3.forward);
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.up;
            normal.Normalize();
            if (normal.y < 0f) normal = -normal; // маркер на столе виден сверху — нормаль вверх
            hit.normal = normal;
            hit.worldPose = new Pose(hit.point, KvArPlacementMath.ReticleRotation(normal));
            hit.plane = null;
            hit.planeArea = 0f;
            hit.alignment = PlaneAlignment.HorizontalUp;
            hit.distance = Vector3.Distance(camera.position, hit.point);
            return hit;
        }

        /// <summary>Честный отказ маркерной постановки: причина — и в статус, и в подсказку, и в журнал.</summary>
        private bool MarkerRefuse(string reason)
        {
            if (string.IsNullOrEmpty(reason)) reason = "маркер не виден: сканер не вернул четыре угла";
            LastMarkerStatus = reason;
            Debug.LogWarning("[KvAr] ПОСТАНОВКА ПО МАРКЕРУ ОТКЛОНЕНА: " + reason);
            if (ui != null)
            {
                ui.SetHint("Постановка по маркеру: " + reason);
                markerHintUntil = Time.unscaledTime + 4f;
            }
            return false;
        }

        /// <summary>
        /// ПРОВЕРКА ПЛОЩАДКИ ПОСТАНОВКИ КЛАССИФИКАТОРОМ — короткая приписка к строке постановки.
        ///
        /// ЧТО БЫЛО. Любая точка плоскости считалась столом, поэтому «ПОСТАНОВКА ВЫПОЛНЕНА» звучала
        /// одинаково и для столешницы, и для пола. На приёмке это и был вопрос «а он точно на столе?».
        /// ЧТО СТАЛО. В строке появилась проверка: «стол подтверждён (уверенность …)» либо
        /// «НЕ стол (причина с числами)». Постановка при этом не отменяется: оператор коснулся сам.
        /// </summary>
        private string DescribeSurfaceCheck(KvArSurfaceHit surface)
        {
            if (surface.plane == null)
                return " · проверка площадки: плоскости нет — подтвердить стол нечем";

            if (planeVisualizer == null) planeVisualizer = FindAnyObjectByType<KvArPlaneVisualizer>();
            if (planeVisualizer == null)
                return " · проверка площадки: нет визуализатора плоскостей";

            RealTableVerdict verdict = planeVisualizer.ClassifySurface(surface.plane);
            if (verdict.IsTable)
                return " · проверка площадки: СТОЛ подтверждён · уверенность " +
                       verdict.Confidence.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

            if (!warnedNotATable)
            {
                warnedNotATable = true;
                Debug.LogWarning("[KvAr] ПОСТАНОВКА: площадка НЕ признана столом — " + verdict.Reason +
                                 ". Робот поставлен по вашему выбору, но это может быть ПОЛ: столом " +
                                 "считается поверхность выше пола на 0.25 м и ниже глаз на 0.35–1.50 м");
                if (ui != null) ui.SetHint("Внимание: это НЕ стол (" + verdict.Reason + ")");
            }
            return " · проверка площадки: НЕ стол · " + verdict.Reason;
        }

        /// <summary>ПЕРЕСТАВИТЬ МИР: убрать якорь и вернуться к выбору места.</summary>
        public void RePlace()
        {
            if (world == null) return;
            world.Clear("переставить мир");
            SetState(PlanesFound > 0 ? KvArPlacementState.ReadyToPlace : KvArPlacementState.WaitingForPlanes);
            if (ui != null)
            {
                ui.HideHint();
                ui.SetHint(KvArPlacementMath.Hint(State, PlanesFound, FirstPlaneSeconds, TableFound));
            }
            Debug.Log("[KvAr] ПЕРЕСТАНОВКА: мир убран, наведите камеру на новое место и коснитесь стола");
        }

        private void LogFirstPlane()
        {
            if (!logOnChange) return;
            StringBuilder sb = new StringBuilder();
            sb.Append("[KvAr] ПЛОСКОСТЬ НАЙДЕНА через ")
              .Append(FirstPlaneSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))
              .Append(" с · плоскостей: ").Append(PlanesFound);
            if (planeManager != null)
            {
                foreach (ARPlane plane in planeManager.trackables)
                {
                    if (plane == null) continue;
                    float width = Mathf.Abs(plane.size.x * plane.transform.lossyScale.x);
                    float depth = Mathf.Abs(plane.size.y * plane.transform.lossyScale.z);
                    float area = width * depth;
                    if (area > LargestPlaneArea) LargestPlaneArea = area;
                    sb.Append(" · «").Append(plane.name).Append("» ")
                      .Append(width.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                      .Append("×")
                      .Append(depth.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture))
                      .Append(" м (").Append(plane.alignment).Append(")");
                }
            }
            Debug.Log(sb.ToString());
        }

        /// <summary>СОЗДАТЬ МАРКЕР (один раз). Материал — под текущий конвейер проекта (URP в шлеме/AR).</summary>
        private void EnsureReticle()
        {
            if (reticle != null) return;
            reticle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            reticle.name = "ArReticle";
            Collider collider = reticle.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;   // маркер не должен ловить лучи физики
            reticleMaterial = KvRenderPipelineShaders.CreateUnlit(
                "Материал_маркера_AR", new Color(0.20f, 0.92f, 1f, 0.85f));
            Renderer renderer = reticle.GetComponent<Renderer>();
            if (renderer != null && reticleMaterial != null) renderer.sharedMaterial = reticleMaterial;
            reticle.transform.localScale = new Vector3(reticleSizeMeters, reticleSizeMeters, 1f);
            reticle.SetActive(false);
            reticleVisible = false;
        }

        private void UpdateReticle()
        {
            if (reticle == null) return;
            bool show = KvArPlacementMath.ShouldShowReticle(IsPlaced, Current.valid);
            if (show)
            {
                reticle.transform.SetPositionAndRotation(
                    Current.point + Current.normal.normalized * 0.002f,
                    KvArPlacementMath.ReticleRotation(Current.normal));
                reticle.transform.localScale = new Vector3(reticleSizeMeters, reticleSizeMeters, 1f);
            }
            if (show != reticleVisible)
            {
                reticleVisible = show;
                reticle.SetActive(show);
            }
        }

        private void SetState(KvArPlacementState next)
        {
            if (State == next) return;
            State = next;
            if (logOnChange)
                Debug.Log("[KvAr] ПОСТАНОВКА: состояние " + State + " · плоскостей " + PlanesFound);
        }

        private static T FindAny<T>() where T : Component
        {
            return UnityEngine.Object.FindAnyObjectByType<T>();
        }

        /// <summary>Строка-доказательство текущего состояния (для журнала устройства).</summary>
        public string Describe()
        {
            switch (State)
            {
                case KvArPlacementState.WaitingForPlanes:
                    return "ждём плоскости (найдено " + PlanesFound + ")";
                case KvArPlacementState.ReadyToPlace:
                    return "готово к постановке · плоскостей " + PlanesFound +
                           " · попадание: " + (Current.valid ? Current.Describe() : "нет");
                case KvArPlacementState.Placed:
                    return "мир поставлен · " + (world != null ? world.Describe() : "нет мира");
                default:
                    return "ошибка постановки";
            }
        }
    }
}
