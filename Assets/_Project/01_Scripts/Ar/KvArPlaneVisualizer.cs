using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using KazistovVvFeatures;
using KazistovVvRealWorld;

namespace KazistovVvAr
{
    /// <summary>
    /// ВИЗУАЛИЗАЦИЯ НАЙДЕННЫХ ПОВЕРХНОСТЕЙ И ВЫБОР СТОЛА (06.11.2026).
    ///
    /// ВЛАДЕЛЕЦ: «мне нужен полноценно работающий AR, автоопределение поверхностей…
    /// робот автоматически на самый большой горизонтальный стол».
    ///
    /// ЧТО ДЕЛАЕТ:
    ///   • каждую найденную ARCore плоскость рисует СЕТКОЙ (контур + линии внутри) прямо поверх
    ///     реального стола/пола — видно, что планшет действительно нашёл поверхность;
    ///   • стол выбирает КЛАССИФИКАТОР (`KvRealTableClassifier`), а не «площадь»: зелёным
    ///     подсвечивается только та плоскость, которая прошла проверку наклоном, высотой над полом,
    ///     высотой относительно глаз и габаритами столешницы;
    ///   • остальные горизонтальные — синие, вертикальные — серые (не мешают);
    ///   • отдаёт `TryGetBestTable` — по нему `KvArPlacement` ставит якорь ровно на стол,
    ///     даже если луч из центра экрана не попал в плоскость.
    ///
    /// ЧТО БЫЛО НЕ ТАК (дефект, исправленный после S45). Правилом выбора была «самая большая
    /// горизонтальная плоскость площадью ≥ 0.15 м²». Пол горизонтален, огромен и стабилен, поэтому
    /// правило почти всегда выбирало ПОЛ — и подписывало его «СТОЛ». На приёмке это выглядело как
    /// «робот стоит на полу, а приложение говорит, что на столе».
    ///
    /// ПОЧЕМУ СВОЯ ОТРИСОВКА, А НЕ ПРЕФАБ AR FOUNDATION. Префаб плоскости в сцене может быть
    /// отключён/невидим (в шлеме он не нужен), а на планшете видимость найденных поверхностей —
    /// обязательная часть «полноценного AR». Рисуем линиями URP-материалом проекта, без новых
    /// ассетов и без зависимости от настроек сцены.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvArPlaneVisualizer : MonoBehaviour
    {
        /// <summary>Менеджер плоскостей AR Foundation.</summary>
        public ARPlaneManager planeManager;

        /// <summary>AR-камера — для подписи «СТОЛ» лицом к оператору.</summary>
        public Camera arCamera;

        /// <summary>Показывать сетку поверхностей.</summary>
        public bool showPlanes = true;

        /// <summary>
        /// ПОРОГ ШУМА ARCore, м² — плоскости меньше этого вообще не рассматриваются.
        ///
        /// ЧЕМ ОТЛИЧАЕТСЯ ОТ ПОРОГА КЛАССИФИКАТОРА. Здесь 0.15 м² — «это ещё не поверхность, а
        /// несколько полигонов»; коридор столешницы (0.25…3.0 м²) проверяет уже классификатор.
        /// Порог оставлен публичным и прежним: на него опирается существующая проверка проекта, и
        /// менять его значило бы менять поведение фильтра, не связанного с дефектом «пол вместо стола».
        /// </summary>
        public float minTableAreaMeters = 0.15f;

        /// <summary>Как часто обновлять сетку (секунды).</summary>
        public float refreshSeconds = 0.25f;

        /// <summary>Писать в журнал о найденном столе (обязательная строка-доказательство).</summary>
        public bool logOnChange = true;

        /// <summary>
        /// Лучший найденный СТОЛ (прошедший классификатор). null — стола нет: пол столом больше не
        /// считается, даже если он самый большой.
        /// </summary>
        public ARPlane BestTable { get; private set; }

        /// <summary>Площадь лучшего стола, м².</summary>
        public float BestTableArea { get; private set; }

        /// <summary>Центр лучшего стола в мировых координатах.</summary>
        public Vector3 BestTableCenter { get; private set; }

        /// <summary>Нормаль лучшего стола (вверх).</summary>
        public Vector3 BestTableNormal { get; private set; } = Vector3.up;

        /// <summary>
        /// КЕМ ПРИЗНАНА ВЫБРАННАЯ ПОВЕРХНОСТЬ. Нужно, чтобы на экране и в журнале можно было
        /// отличить «стол по разметке ARCore» от «стол по геометрии» — и увидеть, что пол отвергнут.
        /// </summary>
        public KvRealSurfaceKind BestTableKind { get; private set; } = KvRealSurfaceKind.Unknown;

        /// <summary>УВЕРЕННОСТЬ ВЫБОРА 0…1 — по ней работает гистерезис и по ней видно, насколько выбор шаткий.</summary>
        public float BestTableConfidence { get; private set; }

        /// <summary>
        /// ПРИЧИНА ВЫБОРА С ЧИСЛАМИ (площадь, высота над полом, наклон, уверенность). Это строка-
        /// доказательство для logcat: по ней видно, ПОЧЕМУ поверхность признана столом.
        /// </summary>
        public string BestTableReason { get; private set; } = "плоскости ещё не найдены · уверенность 0.00";

        /// <summary>Пол найден: либо разметкой ARCore, либо по двум уровням поверхностей.</summary>
        public bool FloorFound { get; private set; }

        /// <summary>Высота пола в мировых координатах сессии, метры (имеет смысл только при `FloorFound`).</summary>
        public float FloorHeight { get; private set; }

        /// <summary>Сколько плоскостей сейчас нарисовано (для журнала/строки состояния).</summary>
        public int VisualizedPlanes { get; private set; }

        /// <summary>Человеческое описание для нижней строки состояния.</summary>
        public string DescribeTable { get; private set; } = "стол не найден";

        private class Entry
        {
            public ARPlane plane;
            public GameObject root;
            public LineRenderer outline;
            public LineRenderer grid;
            public TextMesh label;
            public MeshRenderer labelRenderer;
            public GameObject fill;
            public MeshRenderer fillRenderer;
        }

        private readonly Dictionary<TrackableId, Entry> entries =
            new Dictionary<TrackableId, Entry>();

        private GameObject tableFill;
        private Material tableFillMaterial;
        private Material materialTableOutline;
        private Material materialTableGrid;
        private Material materialHorizontalOutline;
        private Material materialHorizontalGrid;
        private Material materialVerticalOutline;
        private Material materialVerticalGrid;
        private float nextRefreshTime;
        private float nextLogTime;
        private bool loggedLabelFont;

        /// <summary>Буферы разбора: переиспользуются, чтобы не сорить сборками мусора каждый пересчёт.</summary>
        private readonly List<RealSurfaceCandidate> candidates = new List<RealSurfaceCandidate>();
        private readonly List<ARPlane> candidatePlanes = new List<ARPlane>();
        private readonly List<RealTableVerdict> verdicts = new List<RealTableVerdict>();
        private readonly List<TrackableId> trackedNow = new List<TrackableId>();
        private readonly List<TrackableId> staleAges = new List<TrackableId>();

        /// <summary>Когда каждая плоскость впервые попала в разбор — из этого считается `TrackedSeconds`.</summary>
        private readonly Dictionary<TrackableId, float> firstSeenAt = new Dictionary<TrackableId, float>();

        /// <summary>Стол, выбранный на прошлом шаге: по нему работает гистерезис (без мерцания выбора).</summary>
        private TrackableId lastBestId;
        private bool hasLastBest;

        /// <summary>
        /// Последнее записанное в журнал состояние (КЛЮЧ, а не текст: см. `SurfaceStateKey`) — чтобы
        /// печатать при смене, а не каждую секунду.
        /// </summary>
        private string loggedSurfaceState = string.Empty;

        /// <summary>
        /// ПОСЛЕДНЯЯ НАПЕЧАТАННАЯ ПОЗА КАМЕРЫ (ключ состояния `Up`/`Down`/`Level`). Пустая строка —
        /// ещё не печатали. Нужна, чтобы строка о позе появлялась при СМЕНЕ, а не каждую секунду:
        /// иначе журнал превращается в спам, который перестают читать.
        /// </summary>
        private string loggedCameraPose = string.Empty;

        /// <summary>Когда в последний раз печатали позу камеры — не чаще раза в секунду.</summary>
        private float nextPoseLogTime;

        /// <summary>
        /// ТЕКУЩАЯ ПРОЕКЦИЯ `dot(forward, up)` AR-камеры. Считается в `Update`, чтобы строка
        /// «плоскостей 0» могла сослаться на позу без второго обращения к камере.
        /// </summary>
        private float cameraDotUp;

        /// <summary>
        /// КОГДА ПЛОСКОСТЕЙ СТАЛО НОЛЬ, `Time.unscaledTime`. Из этого считается «сколько ждём»:
        /// без числа секунд оператор не отличает «пропали только что» от «не было ни минуты», а
        /// приёмка не может сказать, дождались ли мы порога 15 с вообще.
        /// </summary>
        private float planesWithoutSince;

        /// <summary>
        /// СКОЛЬКО СЕКУНД ЖДЁМ ПЛОСКОСТИ, прежде чем печатать диагностику подсистемы. Порог тот же,
        /// что у сторожа обнаружения в `KvArRuntime.planeWatchdogSeconds` (15 с): два разных порога
        /// дали бы в журнале две противоречащие фразы про «плоскостей нет».
        /// </summary>
        public const float SurfaceWaitSeconds = 15f;

        /// <summary>
        /// ПРЕФИКС СТРОК ЖУРНАЛА О ПОВЕРХНОСТЯХ. По нему ищет приёмка
        /// (`Tools/ar-planes-verify.ps1`, `Tools/real-table-device.ps1`), поэтому он вынесен в
        /// константу, а не склеивается в пяти местах по-разному.
        /// </summary>
        public const string SurfaceLogPrefix = "[KvAr] ПОВЕРХНОСТИ: ";

        /// <summary>ПОДСКАЗКА, КОГДА ТРЕКИНГ ИДЁТ, А ПОВЕРХНОСТЕЙ НЕТ (слова владельца с приёмки).</summary>
        public const string SurfaceHint =
            "медленно поводите планшетом над столом, добавьте света, " +
            "поверхность должна быть с текстурой и без бликов";

        /// <summary>
        /// ПОДСКАЗКА, КОГДА ПОТЕРЯН САМ ТРЕКИНГ. Здесь бесполезно советовать «поводите над столом»:
        /// пока ARCore не держит позу, он не строит карту вовсе (журнал планшета 07.11.2026:
        /// `VIO_OUTPUT_NOT_TRACKING`), и лечится это светом, текстурой и плавным движением.
        /// </summary>
        public const string TrackingHint =
            "наведите планшет на освещённую поверхность с текстурой и двигайте плавнее — " +
            "без трекинга ARCore не строит карту";

        /// <summary>
        /// ПРЕФИКС СТРОК ЖУРНАЛА О ПОЗЕ КАМЕРЫ. Вынесен в константу по той же причине, что и
        /// `SurfaceLogPrefix`: по нему ищет приёмка, и он не должен склеиваться в разных местах
        /// по-разному.
        /// </summary>
        public const string PoseLogPrefix = "[KvAr] ПОЗА КАМЕРЫ: ";

        /// <summary>
        /// ПОРОГ «КАМЕРА СМОТРИТ НЕ НА СТОЛ», по проекции `dot(forward, up)`.
        ///
        /// ЧТО БЫЛО. Такого порога не было вовсе, и поза камеры не проверялась нигде. На приёмке
        /// 12.11.2026 планшет лежал ЭКРАНОМ ВВЕРХ: камера смотрела в потолок, в кадре не было стола,
        /// ARCore не мог построить карту — и в журнале появилось «плоскостей 0», что читается как
        /// поломка распознавания. Настоящая причина (камера смотрит в небо) не была видна НИГДЕ.
        ///
        /// ЧЕМ ПЛОХО 0.5. Это ровно 30° от горизонта вверх (acos(0.5) = 60° от вертикали). При
        /// таком наклоне камера заведомо не видит столешницу: чтобы она попала в кадр, объектив
        /// должен быть направлен вниз хотя бы на 10…15°. Порог оставлен грубым намеренно — он
        /// обязан молчать при обычной работе, а не ловить «планшет наклонён на 2°».
        /// </summary>
        public const float CameraPoseThreshold = 0.5f;

        /// <summary>
        /// ПОДСКАЗКА ДЛЯ «КАМЕРА СМОТРИТ ВВЕРХ» — что именно сделать руками (слова с приёмки).
        /// </summary>
        public const string PoseUpHint =
            "опустите планшет, чтобы камера смотрела на столешницу";

        /// <summary>
        /// ПОЗА AR-КАМЕРЫ ОТНОСИТЕЛЬНО ГОРИЗОНТА (enum, а не строка: состояние проверяется тестом).
        ///
        /// ОТКУДА ВЗЯТЫ ЗНАКИ — ПО КОДУ ПРОЕКТА, А НЕ ПО ПАМЯТИ.
        ///   1) В `Assets/_Project/00_Scenes/ArMain.unity` AR-камера `ArCamera` лежит в
        ///      `Camera Offset`, её ЛОКАЛЬНЫЙ поворот — `m_LocalRotation: {x: 0, y: 0, z: 0, w: 1}`
        ///      (строка 792), у `Camera Offset` и у `AR XR Origin` — тоже ноль. Значит ось `+Z`
        ///      камеры в мире — это ось `+Z` Unity, и `Camera.transform.forward` — куда смотрит
        ///      объектив. Ничего не переворачивается.
        ///   2) Мир ARCore в Unity — «правая тройка, Y вверх» (справка multiset: «ARKit (iOS),
        ///      ARCore (Android), WebXR, Three.js, OpenGL — Right-handed, Y-up»,
        ///      https://docs.multiset.ai/fundamentals/localization/coordinate-systems.md), поэтому
        ///      «горизонт» — это `Vector3.up`, и проекция `dot(forward, up) > 0` означает «объектив
        ///      смотрит ВВЕРХ».
        ///   3) Согласованность с собственными правилами проекта: классификатор требует, чтобы стол
        ///      был НИЖЕ камеры на 0.35…1.5 м (`KvRealTableClassifier.MinHeightBelowEyesMeters` /
        ///      `MaxHeightBelowEyesMeters`), то есть рабочая поза — камера НАД столом, взгляд вниз.
        ///      Это тоже даёт `dot(forward, up) < 0`.
        /// </summary>
        public enum KvCameraPoseState
        {
            /// <summary>Камера смотрит ВВЕРХ (в потолок/небо): стола в кадре нет.</summary>
            Up,

            /// <summary>Камера смотрит ВНИЗ, на столешницу или на пол — рабочая поза.</summary>
            Down,

            /// <summary>Камера почти параллельна горизонту: помещается в «норму» без подсказок.</summary>
            Level
        }

        /// <summary>
        /// ПОЗА КАМЕРЫ ИЗ ПРОЕКЦИИ `dot(forward, up)` — ЧИСТАЯ ФУНКЦИЯ (тест без устройства).
        ///
        /// ПОЧЕМУ ОТДЕЛЬНО ОТ `Camera`. `Camera.transform.forward` в EditMode недоступен без сцены,
        /// а решение «на стол смотрит или нет» обязано проверяться машинно. Поэтому вся арифметика
        /// вынесена сюда: на вход одно число, на выход одно из трёх различимых состояний.
        ///
        /// ГРАНИЦЫ. `> +0.5` — вверх, `< −0.5` — вниз, РОВНО `±0.5` — ещё «норма»: порог включающий,
        /// чтобы на границе не появлялось ложной тревоги «смотрит в потолок».
        /// </summary>
        public static KvCameraPoseState CameraPoseState(float dotUp)
        {
            // NaN/∞ приходят из трекинга штатно (потеря позы) — это не «вверх» и не «вниз»:
            // сравнение с NaN дало бы ложь в обе стороны, и состояние осталось бы «Level».
            float dot = IsFinite(dotUp) ? Mathf.Clamp(dotUp, -1f, 1f) : 0f;
            if (dot > CameraPoseThreshold) return KvCameraPoseState.Up;
            if (dot < -CameraPoseThreshold) return KvCameraPoseState.Down;
            return KvCameraPoseState.Level;
        }

        /// <summary>Конечное ли число (NaN/∞ отсекаются до сравнений — иначе правила молчат).</summary>
        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        /// <summary>
        /// УГОЛ НАКЛОНА КАМЕРЫ ОТ ГОРИЗОНТА В ГРАДУСАХ — ЧИСТАЯ ФУНКЦИЯ.
        ///
        /// Это угол от ГОРИЗОНТАЛИ (0° — объектив параллелен столу, 90° — строго в потолок), а не
        /// от вертикали: на приёмке оператор говорит «смотрит вверх на 62°», и это именно угол над
        /// горизонтом. `asin` от `dot(forward, up)` даёт ровно его.
        /// </summary>
        public static float CameraTiltDegrees(float dotUp)
        {
            float dot = IsFinite(dotUp) ? Mathf.Clamp(dotUp, -1f, 1f) : 0f;
            return Mathf.Asin(dot) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О ПОЗЕ КАМЕРЫ — ЧИСТАЯ ФУНКЦИЯ (числа + подсказка только там, где нужна).
        ///
        /// ЧТО БЫЛО. Позы камеры в журнале не было ВООБЩЕ. Планшет на приёмке лежал экраном вверх,
        /// камера смотрела в потолок, и единственной строкой было «плоскостей 0» — по ней нельзя
        /// отличить «ARCore сломался» от «оператор положил планшет не той стороной».
        ///
        /// ЧТО БЫЛО НЕ ТАК В ПЕРВОМ ВАРИАНТЕ ЭТОЙ ЖЕ ПРАВКИ. Для `Level` печаталось «почти
        /// горизонтально» — БЕЗ СЛОВА НАПРАВЛЕНИЯ и с модулем угла. Из-за этого рабочая поза
        /// (планшет над столом, наклон ~25° вниз) выглядела как «25° — норма», где не сказано,
        /// вниз или вверх: два РАЗНЫХ состояния читались одинаково, и приёмка не могла отличить
        /// «камера на столе» от «камера задрана к потолку, но меньше порога».
        ///
        /// ЧТО СТАЛО (12.11.2026). У КАЖДОГО из трёх состояний своё слово направления и ЗНАКОВЫЙ
        /// угол: «ВВЕРХ на 62°», «ВНИЗ на 25°», «РОВНО (горизонт), 3° вниз». Три состояния
        /// различаются ТЕКСТОМ, а не только числом, — это и проверяет
        /// `DescribeCameraPose_StatesDifferByWord`.
        ///
        /// ЗНАК УГЛА ВАЖЕН. В `Down` и `Level` печатается угол со знаком (вниз = минус): при
        /// `Level` направление ещё не дотянуло до порога, и единственное, что отличает «чуть вниз»
        /// от «чуть вверх», — этот минус. В `Up` печатается модуль, потому что слово «ВВЕРХ» уже
        /// задаёт направление (форма строки — из задачи, слово в слово).
        ///
        /// Пустой угол невозможен: число печатается всегда, это же и анти-вакуум для теста.
        /// </summary>
        public static string DescribeCameraPose(float dotUp)
        {
            float degrees = CameraTiltDegrees(dotUp);
            // МОДУЛЬ — для «ВВЕРХ» (направление задано словом, форма строки из задачи).
            string magnitude = Mathf.Abs(degrees).ToString("0", CultureInfo.InvariantCulture);
            // ЗНАКОВЫЙ УГОЛ — для «ВНИЗ» и «РОВНО»: там минус и есть направление.
            string signed = degrees.ToString("0", CultureInfo.InvariantCulture);
            KvCameraPoseState state = CameraPoseState(dotUp);

            if (state == KvCameraPoseState.Up)
                return PoseLogPrefix + "смотрит ВВЕРХ на " + magnitude +
                       "° — в кадре потолок, стол в кадр не попадает; " + PoseUpHint;

            if (state == KvCameraPoseState.Down)
                return PoseLogPrefix + "смотрит ВНИЗ на " + magnitude + "° — норма, стол в кадре";

            // LEVEL — НЕ «просто норма», а отдельная формулировка СО СЛОВОМ НАПРАВЛЕНИЯ:
            // объектив почти параллелен горизонту, и знак угла говорит, куда именно наклонён.
            return PoseLogPrefix + "смотрит РОВНО (горизонт), " + signed + "° — норма";
        }

        /// <summary>
        /// ССЫЛКА НА ПОЗУ ДЛЯ СТРОКИ «ПЛОСКОСТЕЙ 0» — ЧИСТАЯ ФУНКЦИЯ.
        ///
        /// ЗАЧЕМ. Две диагностики («плоскостей 0» и «камера смотрит не туда») по отдельности
        /// заставляют оператора гадать. Здесь они связываются: если камера смотрит вверх, строка о
        /// плоскостях ОБЯЗАНА назвать это вероятной причиной.
        ///
        /// АНТИ-ВАКУУМ: при нормальной позе возвращается ПУСТАЯ строка, и вызывающий код ничего не
        /// дописывает. Иначе ссылка на позу висела бы и на рабочей сессии, и приёмка снова читала
        /// бы «вероятная причина» там, где причины нет.
        /// </summary>
        public static string PoseCulpritSuffix(float dotUp)
        {
            if (CameraPoseState(dotUp) != KvCameraPoseState.Up) return string.Empty;
            return " · ПОЗА: смотрит вверх — это вероятная причина";
        }

        private static readonly Color ColorTable = new Color(0.20f, 1f, 0.45f, 1f);
        private static readonly Color ColorHorizontal = new Color(0.35f, 0.75f, 1f, 0.95f);
        private static readonly Color ColorVertical = new Color(0.85f, 0.85f, 0.9f, 0.55f);
        private const int GridDivisions = 4;

        /// <summary>Толщина контура стола, м (виден с 2–3 м).</summary>
        private const float TableLineWidth = 0.018f;

        /// <summary>Толщина контура прочих плоскостей, м.</summary>
        private const float OtherLineWidth = 0.008f;

        /// <summary>Толщина линий сетки, м.</summary>
        private const float GridLineWidth = 0.005f;

        void Awake()
        {
            liveInstances++;      // меш заливки общий: уничтожаем его, только когда не нужен никому (P2-9)
            if (planeManager == null) planeManager = FindAnyObjectByType<ARPlaneManager>();
            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();

            // ЦВЕТА ЛИНИЙ — ОТДЕЛЬНЫМ МАТЕРИАЛОМ, А НЕ ЧЕРЕЗ LineRenderer.startColor.
            // Дефект планшета 06.11.2026: сетка рисовалась БЕЛОЙ, зелёной подсветки стола и
            // подписи «СТОЛ» не было видно. Причина: единственный материал
            // `CreateUnlit(..., Color.white)` под URP — это `Universal Render Pipeline/Unlit`,
            // который НЕ умножает цвет на вершины линии, а `SetLineColor` пишет именно в вершины.
            // Поэтому цвет задаём свойством материала (`_BaseColor`/`_Color`), по материалу на
            // каждый вид плоскости. Тот же шейдер уже проверен в этой сборке (линии рисуются).
            materialTableOutline = CreateLineMaterial("Материал_контура_стола_AR", ColorTable);
            materialTableGrid = CreateLineMaterial("Материал_сетки_стола_AR", GridColorFor(ColorTable));
            materialHorizontalOutline = CreateLineMaterial("Материал_контура_плоскости_AR",
                ColorHorizontal);
            materialHorizontalGrid = CreateLineMaterial("Материал_сетки_плоскости_AR",
                GridColorFor(ColorHorizontal));
            materialVerticalOutline = CreateLineMaterial("Материал_контура_стены_AR", ColorVertical);
            materialVerticalGrid = CreateLineMaterial("Материал_сетки_стены_AR",
                GridColorFor(ColorVertical));
            tableFillMaterial = CreateFillMaterial("Материал_заливки_стола_AR",
                new Color(ColorTable.r, ColorTable.g, ColorTable.b, 0.16f));
        }

        /// <summary>
        /// МАТЕРИАЛ ЛИНИЙ. Цвет — в свойстве материала: под URP цвет вершин LineRenderer
        /// игнорируется, поэтому один белый материал делал ВСЮ сетку белой (дефект 06.11.2026).
        /// </summary>
        private static Material CreateLineMaterial(string name, Color color)
        {
            Material material = KvRenderPipelineShaders.CreateUnlit(name, color);
            if (material != null) material.name = name;
            return material;
        }

        /// <summary>
        /// ПОЛУПРОЗРАЧНАЯ ЗАЛИВКА СТОЛА: нужен шейдер с прозрачностью.
        ///
        /// ИСПРАВЛЕНО 11.11.2026. Здесь был прямой `Shader.Find("Sprites/Default")` и
        /// `new Material(shader)` — единственное оставшееся нарушение правила «материалы создаёт
        /// только `KvRenderPipelineShaders`». Цена: в сборке шейдер мог быть вырезан вариантами, и
        /// заливка становилась розовой или исчезала, а тест
        /// `Sources_HaveNoDirectShaderLookupsInRuntimeCode` был КРАСНЫМ. Теперь материал создаёт
        /// фабрика, и она же уничтожается вместе с компонентом (см. `DestroyMaterial` в `OnDestroy`).
        /// </summary>
        private static Material CreateFillMaterial(string name, Color color)
        {
            return KvRenderPipelineShaders.CreateUnlitTransparent(name, color);
        }

        /// <summary>ЦВЕТ ЛИНИЙ СЕТКИ ОТ ЦВЕТА КОНТУРА: темнее и непрозрачный (сетка не шумит).</summary>
        public static Color GridColorFor(Color outline)
        {
            return new Color(outline.r * 0.42f, outline.g * 0.42f, outline.b * 0.42f, 1f);
        }

        /// <summary>
        /// ПОВОРОТ ПОДПИСИ ЛИЦОМ К ОПЕРАТОРУ. Перед текста (`+Z`) должен смотреть НА камеру:
        /// `LookRotation(камера − метка)`. В прежнем коде было наоборот (`центр − камера`), и
        /// надпись «СТОЛ» была отвёрнута от оператора (дефект 06.11.2026).
        /// </summary>
        public static Quaternion LabelRotation(Vector3 labelPosition, Vector3 cameraPosition)
        {
            Vector3 direction = cameraPosition - labelPosition;
            if (direction.sqrMagnitude < 0.0001f) return Quaternion.identity;
            Vector3 forward = direction.normalized;
            // Камера СТРОГО над меткой (смотрят на стол сверху): ось «вверх» вырождается,
            // поэтому опорой берём горизонталь — иначе поворот скачет.
            Vector3 up = Vector3.up;
            if (Mathf.Abs(Vector3.Dot(forward, up)) > 0.98f) up = Vector3.forward;
            return Quaternion.LookRotation(forward, up);
        }

        void Update()
        {
            if (Time.unscaledTime < nextRefreshTime) return;
            nextRefreshTime = Time.unscaledTime + Mathf.Max(0.1f, refreshSeconds);

            if (showPlanes && planeManager != null)
            {
                SyncEntries();
                ChooseBestTable();
                RecolorPlanes();
            }

            // ЧТО БЫЛО. Вызов журнала стоял ПОСЛЕ выхода из ветки отрисовки: при выключенной сетке
            // (`showPlanes == false`) или без менеджера плоскостей о поверхностях не печаталось
            // НИЧЕГО. ЧЕМ ПЛОХО. Молчание — тот же дефект, что и раньше: по нему нельзя отличить
            // «поверхностей нет» от «код сломался». ЧТО СТАЛО. Журнал пишется всегда, а сама строка
            // честно говорит, чего не хватает (в т.ч. «менеджер плоскостей НЕ найден»).
            //
            // ПОЗА КАМЕРЫ СЧИТАЕТСЯ ДО СТРОКИ О ПЛОСКОСТЯХ (12.11.2026): строка «плоскостей 0»
            // обязана сослаться на позу, если камера смотрит в потолок — иначе две диагностики
            // снова пришлось бы складывать в голове оператора.
            ReadCameraPose();
            LogTableOncePerSecond();
            LogCameraPoseOnce();
        }

        /// <summary>
        /// ПРОЧИТАТЬ ПОЗУ AR-КАМЕРЫ ОТНОСИТЕЛЬНО ГОРИЗОНТА (12.11.2026).
        ///
        /// ЧТО БЫЛО. Поза камеры не читалась нигде. Планшет на приёмке лежал ЭКРАНОМ ВВЕРХ, камера
        /// смотрела в потолок, стола в кадре не было — и ARCore не мог построить карту. В журнале
        /// это выглядело как «плоскостей 0», то есть как поломка распознавания.
        /// ЧЕМ ПЛОХО. Оператор и приёмка искали дефект в ARCore, а причина была в том, как положили
        /// планшет. Ни одного числа про позу в журнале не было.
        /// ЧТО СТАЛО. Каждый пересчёт берёт `dot(camera.forward, Vector3.up)` — единственное число,
        /// которого достаточно, чтобы отличить «смотрит в потолок» от «смотрит на стол».
        ///
        /// ПОЧЕМУ `forward`, А НЕ `transform.up`. В `ArMain.unity` AR-камера `ArCamera` лежит в
        /// `Camera Offset` с ЛОКАЛЬНЫМ поворотом `{x:0,y:0,z:0,w:1}`, значит её `+Z` — куда смотрит
        /// объектив — совпадает с мировой осью `+Z` Unity, а мир ARCore в Unity Y-вверх. Тот же
        /// смысл заложен в `KvArTouchInput.MoveAxisFromScreenDelta`, где по `camera.forward` строится
        /// горизонтальная ось движения пальца.
        /// </summary>
        private void ReadCameraPose()
        {
            if (arCamera == null) arCamera = KvArRigBridge.FindArCamera();
            cameraDotUp = arCamera != null
                ? Vector3.Dot(arCamera.transform.forward, Vector3.up)
                : 0f;
        }

        /// <summary>
        /// СТРОКА О ПОЗЕ КАМЕРЫ В ЖУРНАЛ — не чаще раза в секунду и ТОЛЬКО при смене состояния.
        ///
        /// ЧТО БЫЛО. Такой строки не было вовсе: при «плоскостей 0» журнал молчал о том, куда
        /// смотрит камера (дефект приёмки 12.11.2026 — планшет лежал экраном вверх).
        /// ЧТО СТАЛО. Одна строка с углом в градусах при смене состояния `Up`/`Down`/`Level`; при
        /// «смотрит вверх» — готовое действие. Смена печатается сразу (в пределах секунды), а не
        /// «когда-нибудь»: именно в первые секунды оператор и решает, что делать.
        /// </summary>
        private void LogCameraPoseOnce()
        {
            if (!logOnChange) return;
            if (Time.unscaledTime < nextPoseLogTime) return;
            nextPoseLogTime = Time.unscaledTime + 1f;

            string key = CameraPoseState(cameraDotUp).ToString();
            if (!ShouldLogSurfaceState(loggedCameraPose, key)) return;
            loggedCameraPose = key;
            Debug.Log(DescribeCameraPose(cameraDotUp));
        }

        /// <summary>
        /// ЛУЧШИЙ СТОЛ (для постановки робота). Возвращает true ТОЛЬКО если поверхность прошла
        /// классификатор: пол и прочие большие горизонтальные плоскости сюда больше не попадают.
        /// </summary>
        public bool TryGetBestTable(out ARPlane plane, out Vector3 center, out Vector3 normal,
                                    out float area)
        {
            plane = BestTable;
            center = BestTableCenter;
            normal = BestTableNormal;
            area = BestTableArea;
            return BestTable != null;
        }

        /// <summary>
        /// ГРАНИЦЫ ЛУЧШЕГО СТОЛА В МИРОВЫХ КООРДИНАТАХ (07.11.2026). Нужны, чтобы объекты из
        /// ПК-меню не создавались «по плоскости якоря» за краем стола (ножки висели над полом).
        /// </summary>
        public bool TryGetBestTableBounds(out Vector3 center, out float halfWidth,
                                          out float halfDepth, out Quaternion rotation)
        {
            center = BestTableCenter;
            halfWidth = 0f;
            halfDepth = 0f;
            rotation = Quaternion.identity;
            ARPlane plane = BestTable;
            if (plane == null) return false;
            rotation = plane.transform.rotation;
            halfWidth = Mathf.Abs(plane.size.x * plane.transform.lossyScale.x) * 0.5f;
            halfDepth = Mathf.Abs(plane.size.y * plane.transform.lossyScale.z) * 0.5f;
            return halfWidth > 0.01f && halfDepth > 0.01f;
        }

        private void SyncEntries()
        {
            VisualizedPlanes = 0;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null) continue;
                Entry entry;
                if (!entries.TryGetValue(plane.trackableId, out entry))
                {
                    entry = CreateEntry(plane);
                    if (entry == null) continue;
                    entries.Add(plane.trackableId, entry);
                }
                if (entry.root == null) continue;
                entry.root.SetActive(true);
                UpdateEntryGeometry(entry);
                VisualizedPlanes++;
            }

            // Удалённые ARCore плоскости убираем со сцены.
            var dead = new List<TrackableId>();
            foreach (KeyValuePair<TrackableId, Entry> pair in entries)
            {
                if (pair.Value.plane == null)
                {
                    dead.Add(pair.Key);
                    continue;
                }
                bool tracked = false;
                foreach (ARPlane plane in planeManager.trackables)
                {
                    if (plane != null && plane.trackableId == pair.Key) { tracked = true; break; }
                }
                if (!tracked) dead.Add(pair.Key);
            }
            for (int i = 0; i < dead.Count; i++)
            {
                Entry entry;
                if (!entries.TryGetValue(dead[i], out entry)) continue;
                if (entry.root != null) Destroy(entry.root);
                entries.Remove(dead[i]);
            }
        }

        private Entry CreateEntry(ARPlane plane)
        {
            Entry entry = new Entry();
            entry.plane = plane;
            entry.root = new GameObject("ArPlane_" + plane.trackableId);
            entry.root.transform.SetParent(transform, false);

            entry.outline = entry.root.AddComponent<LineRenderer>();
            ConfigureLine(entry.outline);
            entry.outline.loop = true;
            entry.outline.positionCount = 4;

            GameObject gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(entry.root.transform, false);
            entry.grid = gridGo.AddComponent<LineRenderer>();
            ConfigureLine(entry.grid);
            entry.grid.loop = false;
            entry.grid.positionCount = 2 * (GridDivisions + 1) * 2;

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(entry.root.transform, false);
            entry.label = labelGo.AddComponent<TextMesh>();
            entry.label.text = "СТОЛ";
            entry.label.font = KvArUi.BuiltinFont;
            entry.label.fontSize = 64;
            // Крупнее прежнего (0.02): с 2–3 м надпись должна читаться, а не быть точкой.
            entry.label.characterSize = 0.045f;
            entry.label.anchor = TextAnchor.MiddleCenter;
            entry.label.alignment = TextAlignment.Center;
            entry.label.color = ColorTable;
            entry.label.fontStyle = FontStyle.Bold;
            entry.labelRenderer = labelGo.GetComponent<MeshRenderer>();
            if (entry.labelRenderer != null)
            {
                entry.labelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                entry.labelRenderer.receiveShadows = false;
                if (entry.label.font != null)
                {
                    entry.labelRenderer.sharedMaterial = entry.label.font.material;
                }
                else if (!loggedLabelFont)
                {
                    loggedLabelFont = true;
                    Debug.LogWarning("[KvAr] ПОВЕРХНОСТИ: встроенный шрифт Unity не найден — " +
                                     "подпись «СТОЛ» рисоваться не будет");
                }
            }
            entry.label.gameObject.SetActive(false);

            // ПОЛУПРОЗРАЧНАЯ ЗАЛИВКА (только у лучшего стола) — создаётся один раз на плоскость,
            // показывается в `RecolorPlanes`.
            if (tableFillMaterial != null)
            {
                entry.fill = new GameObject("Fill");
                entry.fill.transform.SetParent(entry.root.transform, false);
                MeshFilter filter = entry.fill.AddComponent<MeshFilter>();
                filter.sharedMesh = QuadMesh();
                entry.fillRenderer = entry.fill.AddComponent<MeshRenderer>();
                entry.fillRenderer.sharedMaterial = tableFillMaterial;
                entry.fillRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                entry.fillRenderer.receiveShadows = false;
                entry.fill.SetActive(false);
            }

            UpdateEntryGeometry(entry);
            return entry;
        }

        private static Mesh quadMesh;

        /// <summary>Сколько визуализаторов живо сейчас: общий меш заливки нельзя уничтожать,
        /// пока его держит другой экземпляр (P2-9).</summary>
        private static int liveInstances;

        /// <summary>ЕДИНИЧНЫЙ КВАДРАТ В ПЛОСКОСТИ XZ (1×1 м, центр в нуле) — для заливки стола.</summary>
        private static Mesh QuadMesh()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh();
            quadMesh.name = "KvArPlaneFillQuad";
            quadMesh.vertices = new[]
            {
                new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
            };
            quadMesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            quadMesh.RecalculateNormals();
            quadMesh.RecalculateBounds();
            return quadMesh;
        }

        private void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.startWidth = OtherLineWidth;
            line.endWidth = OtherLineWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            line.alignment = LineAlignment.View;
            // Материал назначается в `RecolorPlanes` по виду плоскости: цвет живёт в материале,
            // а не в вершинах линии (иначе под URP всё белое — дефект 06.11.2026).
            if (materialHorizontalOutline != null)
                line.sharedMaterial = materialHorizontalOutline;
        }

        private void UpdateEntryGeometry(Entry entry)
        {
            ARPlane plane = entry.plane;
            if (plane == null || entry.root == null) return;

            float width = Mathf.Abs(plane.size.x * plane.transform.lossyScale.x);
            float depth = Mathf.Abs(plane.size.y * plane.transform.lossyScale.z);
            if (width < 0.02f) width = 0.02f;
            if (depth < 0.02f) depth = 0.02f;

            Vector3 center = plane.transform.position;
            entry.root.transform.SetPositionAndRotation(center, plane.transform.rotation);
            entry.root.transform.localScale = Vector3.one;

            // КОНТУР: четыре угла в плоскости.
            Vector3[] corners =
            {
                LocalToWorld(plane, -0.5f, -0.5f, width, depth),
                LocalToWorld(plane, 0.5f, -0.5f, width, depth),
                LocalToWorld(plane, 0.5f, 0.5f, width, depth),
                LocalToWorld(plane, -0.5f, 0.5f, width, depth)
            };
            for (int i = 0; i < corners.Length; i++) entry.outline.SetPosition(i, corners[i]);

            // СЕТКА: линии внутри плоскости — видно, что это настоящая найденная поверхность.
            int index = 0;
            for (int i = 0; i <= GridDivisions; i++)
            {
                float t = i / (float)GridDivisions;
                Vector3 a = LocalToWorld(plane, -0.5f, t - 0.5f, width, depth);
                Vector3 b = LocalToWorld(plane, 0.5f, t - 0.5f, width, depth);
                entry.grid.SetPosition(index++, a);
                entry.grid.SetPosition(index++, b);
            }
            for (int i = 0; i <= GridDivisions; i++)
            {
                float t = i / (float)GridDivisions;
                Vector3 a = LocalToWorld(plane, t - 0.5f, -0.5f, width, depth);
                Vector3 b = LocalToWorld(plane, t - 0.5f, 0.5f, width, depth);
                entry.grid.SetPosition(index++, a);
                entry.grid.SetPosition(index++, b);
            }

            // Подпись «СТОЛ» — в центре и лицом к оператору.
            if (entry.label != null)
            {
                Vector3 labelPosition = center + Vector3.up * 0.06f;
                entry.label.transform.position = labelPosition;
                entry.label.transform.rotation = arCamera != null
                    ? LabelRotation(labelPosition, arCamera.transform.position)
                    : Quaternion.LookRotation(Vector3.forward);
            }

            // ЗАЛИВКА СТОЛА: полупрозрачный прямоугольник по найденной плоскости — сразу видно,
            // КАКАЯ именно поверхность считается столом, даже если линии тонкие.
            if (entry.fill != null)
            {
                entry.fill.transform.SetPositionAndRotation(
                    center + Vector3.up * 0.004f, plane.transform.rotation);
                entry.fill.transform.localScale = new Vector3(width, 1f, depth);
            }
        }

        private static Vector3 LocalToWorld(ARPlane plane, float xFraction, float zFraction,
                                            float width, float depth)
        {
            Vector3 local = new Vector3(xFraction * width, 0f, zFraction * depth);
            return plane.transform.TransformPoint(local);
        }

        /// <summary>
        /// ВЫБОР СТОЛА — ЧЕРЕЗ КЛАССИФИКАТОР, А НЕ ПО ПЛОЩАДИ.
        ///
        /// ЧТО БЫЛО. Здесь стоял отбор «самая большая горизонтальная плоскость площадью ≥ 0.15 м²».
        /// Пол горизонтален и всегда больше столешницы, поэтому «столом» становился пол: робот
        /// вставал на пол, а зелёная подпись «СТОЛ» — на полу.
        ///
        /// ЧТО СТАЛО. Каждая подходящая плоскость превращается в ЧИСЛОВОГО кандидата (габариты,
        /// нормаль, возраст, что говорит разметка ARCore) и уходит в `KvRealTableClassifier`, где
        /// проверяются наклон, высота над полом, высота относительно глаз и габариты столешницы.
        /// Выбор между прошедшими — с гистерезисом 20 %, чтобы подсветка не мерцала между двумя
        /// столами. Пол отвергается по высоте над полом, а не по площади.
        /// </summary>
        private void ChooseBestTable()
        {
            candidates.Clear();
            candidatePlanes.Clear();
            verdicts.Clear();
            trackedNow.Clear();

            if (planeManager == null)
            {
                ApplyNoTable("стол не найден: менеджер плоскостей AR не найден · уверенность 0.00");
                return;
            }

            float eyeHeight = arCamera != null ? arCamera.transform.position.y : 0f;

            // 1. СБОР КАНДИДАТОВ. Отсекается только шум ARCore (`minTableAreaMeters`): решение
            // «стол это или пол» принимает классификатор, а не площадь.
            int largestIndex = -1;
            float largestArea = 0f;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null) continue;
                trackedNow.Add(plane.trackableId);

                float width = Mathf.Abs(plane.size.x * plane.transform.lossyScale.x);
                float depth = Mathf.Abs(plane.size.y * plane.transform.lossyScale.z);
                float area = width * depth;

                float seenAt;
                if (!firstSeenAt.TryGetValue(plane.trackableId, out seenAt))
                {
                    seenAt = Time.unscaledTime;
                    firstSeenAt[plane.trackableId] = seenAt;
                }

                if (area < minTableAreaMeters) continue;    // шум: это ещё не поверхность

                RealSurfaceCandidate candidate = BuildCandidate(plane, width, depth,
                    Time.unscaledTime - seenAt);
                candidates.Add(candidate);
                candidatePlanes.Add(plane);

                bool horizontalUp = plane.alignment == PlaneAlignment.HorizontalUp;
                if (horizontalUp && area > largestArea)
                {
                    largestArea = area;
                    largestIndex = candidates.Count - 1;
                }
            }

            // Пометка «это самая большая горизонтальная»: в ARCore так выглядит ПОЛ, и классификатор
            // учитывает это как признак против стола (а не как довод «за», как было раньше).
            if (largestIndex >= 0)
            {
                RealSurfaceCandidate largest = candidates[largestIndex];
                largest.IsLargestHorizontal = true;
                candidates[largestIndex] = largest;
            }
            PruneAges();

            // 2. УРОВЕНЬ ПОЛА: сначала верим разметке ARCore, иначе выводим по двум уровням.
            float floorHeight;
            bool floorKnown = TryDetermineFloor(out floorHeight);
            FloorFound = floorKnown;
            FloorHeight = floorKnown ? floorHeight : 0f;

            // 3. РАЗБОР КАЖДОГО КАНДИДАТА.
            for (int i = 0; i < candidates.Count; i++)
                verdicts.Add(KvRealTableClassifier.Classify(candidates[i], eyeHeight, floorHeight, floorKnown));

            // 4. ВЫБОР С ГИСТЕРЕЗИСОМ: новый стол заменяет прежний, только если он лучше заметно.
            int previous = -1;
            if (hasLastBest)
            {
                for (int i = 0; i < candidatePlanes.Count; i++)
                    if (candidatePlanes[i].trackableId == lastBestId) { previous = i; break; }
            }
            int best = KvRealTableClassifier.SelectBestIndex(verdicts, previous);

            if (best >= 0)
            {
                BestTable = candidatePlanes[best];
                BestTableArea = Mathf.Abs(candidates[best].SideA * candidates[best].SideB);
                BestTableCenter = candidates[best].Center;
                BestTableNormal = candidates[best].Up.sqrMagnitude > 0.0001f
                    ? candidates[best].Up.normalized
                    : Vector3.up;
                BestTableKind = verdicts[best].Kind;
                BestTableConfidence = verdicts[best].Confidence;
                BestTableReason = verdicts[best].Reason;
                DescribeTable = KvRealTableClassifier.Describe(candidates[best], verdicts[best],
                    floorHeight, floorKnown);
                lastBestId = candidatePlanes[best].trackableId;
                hasLastBest = true;
                return;
            }

            hasLastBest = false;
            ApplyNoTable(ExplainNoTable(floorHeight, floorKnown));
        }

        /// <summary>
        /// ПОЧЕМУ СТОЛА НЕТ — САМАЯ ВЕСКАЯ ПРИЧИНА ИЗ ВСЕХ ОТКАЗОВ.
        ///
        /// Раньше на экране было просто «стол не найден», и оператор не понимал, что делать: водить
        /// камерой, отойти от стола или дело в том, что найдено только пол. Теперь берётся отказ с
        /// наибольшей уверенностью (пол — 0.90) и показывается его причина с числами.
        /// </summary>
        private string ExplainNoTable(float floorHeight, bool floorKnown)
        {
            int notable = -1;
            float bestReject = -1f;
            for (int i = 0; i < verdicts.Count; i++)
            {
                if (verdicts[i].IsTable) continue;
                if (verdicts[i].Confidence > bestReject)
                {
                    bestReject = verdicts[i].Confidence;
                    notable = i;
                }
            }
            if (notable >= 0)
                return KvRealTableClassifier.Describe(candidates[notable], verdicts[notable],
                    floorHeight, floorKnown);

            int planes = planeManager != null ? planeManager.trackables.count : 0;
            return "стол не найден: подходящих поверхностей нет (плоскостей " + planes +
                   ") — поводите камерой по столу · уверенность 0.00";
        }

        /// <summary>СБРОС ВЫБОРА: стола нет, но причина и уровень пола остаются честными.</summary>
        private void ApplyNoTable(string describe)
        {
            BestTable = null;
            BestTableArea = 0f;
            BestTableKind = KvRealSurfaceKind.Unknown;
            BestTableConfidence = 0f;
            BestTableReason = describe;
            DescribeTable = describe;
        }

        /// <summary>
        /// УРОВЕНЬ ПОЛА. Два источника, в порядке доверия:
        ///   1) разметка ARCore (`PlaneClassifications.Floor`) — точный ответ платформы;
        ///   2) геометрический вывод по ДВУМ уровням поверхностей (см. `TryInferFloorHeight`).
        ///
        /// ПОЧЕМУ НЕ «САМАЯ НИЖНЯЯ ПЛОСКОСТЬ». Если камера видит только столешницу, самая нижняя
        /// плоскость — это она сама; объявить её полом значило бы навсегда запретить постановку на
        /// настоящий стол. Поэтому вывод требует второго уровня.
        /// </summary>
        private bool TryDetermineFloor(out float floorHeight)
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i].Kind != KvRealSurfaceKind.Floor) continue;
                floorHeight = candidates[i].Center.y;
                return true;
            }
            return KvRealTableClassifier.TryInferFloorHeight(candidates, out floorHeight);
        }

        /// <summary>
        /// КАНДИДАТ ИЗ ПЛОСКОСТИ ARCore. `Kind` берётся из разметки плоскости: ARCore умеет
        /// говорить «пол», «стол», «стена» — это ответ платформы, а не наша догадка.
        /// </summary>
        private static RealSurfaceCandidate BuildCandidate(ARPlane plane, float width, float depth,
                                                           float trackedSeconds)
        {
            // Нормаль горизонтальной плоскости, смотрящей ВНИЗ (потолок), разворачиваем: у стола
            // «верх» обязан смотреть вверх, иначе правило наклона отвергло бы даже столешницу.
            Vector3 up = plane.alignment == PlaneAlignment.HorizontalDown
                ? -plane.transform.up
                : plane.transform.up;

            RealSurfaceCandidate candidate;
            candidate.Kind = KindFromClassifications(plane.classifications);
            candidate.Source = KvRealSurfaceSource.Plane;
            candidate.Center = plane.transform.position;
            candidate.Up = up;
            candidate.SideA = width;
            candidate.SideB = depth;
            candidate.TrackedSeconds = trackedSeconds;
            candidate.IsLargestHorizontal = false;
            return candidate;
        }

        /// <summary>
        /// ЧТО СКАЗАЛ ARCORE О ПЛОСКОСТИ. Разметка приходит набором флагов; порядок проверки важен:
        /// «стол» проверяется первым, потому что ARCore может пометить столешницу сразу как
        /// Table + SeatOfAnyType.
        ///
        /// Публично и статически специально: это чистая функция, и она проверяется тестом без
        /// устройства — иначе единственным доказательством был бы скриншот с планшета.
        /// </summary>
        public static KvRealSurfaceKind KindFromClassifications(PlaneClassifications classifications)
        {
            if (classifications == PlaneClassifications.None) return KvRealSurfaceKind.Unknown;
            if ((classifications & PlaneClassifications.Table) != 0) return KvRealSurfaceKind.Table;
            if ((classifications & PlaneClassifications.Floor) != 0) return KvRealSurfaceKind.Floor;
            if ((classifications & PlaneClassifications.Ceiling) != 0) return KvRealSurfaceKind.Ceiling;
            if ((classifications & (PlaneClassifications.WallFace |
                                    PlaneClassifications.InvisibleWallFace |
                                    PlaneClassifications.InnerWallFace |
                                    PlaneClassifications.WallArt |
                                    PlaneClassifications.DoorFrame |
                                    PlaneClassifications.WindowFrame)) != 0)
                return KvRealSurfaceKind.Wall;
            return KvRealSurfaceKind.Other;
        }

        /// <summary>
        /// РАЗОБРАТЬ ОДНУ ПЛОСКОСТЬ (для постановки тапом). Нужна, чтобы `KvArPlacement` не
        /// называл столом то, что классификатор столом не признал: тап по полу — законный запасной
        /// путь, но подписан он обязан честно.
        /// </summary>
        public RealTableVerdict ClassifySurface(ARPlane plane)
        {
            if (plane == null)
            {
                RealTableVerdict none;
                none.IsTable = false;
                none.Confidence = 0f;
                none.Kind = KvRealSurfaceKind.Unknown;
                none.Reason = "плоскость не передана · уверенность 0.00";
                return none;
            }

            float width = Mathf.Abs(plane.size.x * plane.transform.lossyScale.x);
            float depth = Mathf.Abs(plane.size.y * plane.transform.lossyScale.z);
            float seenAt;
            if (!firstSeenAt.TryGetValue(plane.trackableId, out seenAt)) seenAt = Time.unscaledTime;
            RealSurfaceCandidate candidate = BuildCandidate(plane, width, depth,
                Time.unscaledTime - seenAt);

            float eyeHeight = arCamera != null ? arCamera.transform.position.y : 0f;
            return KvRealTableClassifier.Classify(candidate, eyeHeight, FloorHeight, FloorFound);
        }

        /// <summary>СЧЁТЧИКИ ВОЗРАСТА НЕ РАСТУТ: записи по исчезнувшим плоскостям удаляются.</summary>
        private void PruneAges()
        {
            if (firstSeenAt.Count <= trackedNow.Count) return;
            staleAges.Clear();
            foreach (KeyValuePair<TrackableId, float> pair in firstSeenAt)
            {
                bool present = false;
                for (int i = 0; i < trackedNow.Count; i++)
                    if (trackedNow[i] == pair.Key) { present = true; break; }
                if (!present) staleAges.Add(pair.Key);
            }
            for (int i = 0; i < staleAges.Count; i++) firstSeenAt.Remove(staleAges[i]);
        }

        private void RecolorPlanes()
        {
            foreach (KeyValuePair<TrackableId, Entry> pair in entries)
            {
                Entry entry = pair.Value;
                if (entry.plane == null || entry.root == null) continue;

                bool isTable = BestTable != null && entry.plane.trackableId == BestTable.trackableId;
                bool horizontal = entry.plane.alignment == PlaneAlignment.HorizontalUp ||
                                  entry.plane.alignment == PlaneAlignment.HorizontalDown;
                Color color = isTable ? ColorTable : (horizontal ? ColorHorizontal : ColorVertical);

                // ЦВЕТ — МАТЕРИАЛОМ (см. Awake): под URP цвет вершин LineRenderer не применяется.
                Material outline = isTable ? materialTableOutline
                    : (horizontal ? materialHorizontalOutline : materialVerticalOutline);
                Material grid = isTable ? materialTableGrid
                    : (horizontal ? materialHorizontalGrid : materialVerticalGrid);
                if (outline != null) entry.outline.sharedMaterial = outline;
                if (grid != null) entry.grid.sharedMaterial = grid;

                SetLineColor(entry.outline, color, isTable ? TableLineWidth : OtherLineWidth);
                SetLineColor(entry.grid, GridColorFor(color), GridLineWidth);

                if (entry.label != null)
                {
                    entry.label.gameObject.SetActive(isTable);
                    entry.label.color = ColorTable;
                    if (isTable && arCamera != null)
                    {
                        entry.label.transform.rotation = LabelRotation(
                            entry.label.transform.position, arCamera.transform.position);
                    }
                }

                if (entry.fill != null) entry.fill.SetActive(isTable);
            }
        }

        private static void SetLineColor(LineRenderer line, Color color, float width)
        {
            if (line == null) return;
            line.startColor = color;
            line.endColor = color;
            line.startWidth = width;
            line.endWidth = width;
        }

        /// <summary>
        /// СОСТОЯНИЕ РАСПОЗНАВАНИЯ ПОВЕРХНОСТЕЙ — ровно то, что уходит в журнал.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНЫЙ ТИП. Раньше состояний было три («стол» / «пол» / «нет»), и слово «нет»
        /// означало сразу ДВА разных случая: «плоскости есть, но стола нет» и «плоскостей нет
        /// вообще». Второй случай не печатался никогда — на планшете (свежая сборка, 75 с работы,
        /// ARCore работает) в журнале была только стартовая строка «сетка плоскостей включена», и
        /// приёмка не могла отличить «поверхностей нет» от «код сломался».
        ///
        /// ПОЧЕМУ ПЕРЕЧИСЛЕНИЕМ, А НЕ СТРОКОЙ. Состояние теперь проверяется ЧИСТОЙ функцией
        /// (`SurfaceLogStateOf`) без устройства: сам `Update` на планшете не воспроизводится ни в
        /// EditMode, ни в PlayMode, а решение «что печатать» должно быть проверяемым машинно.
        /// </summary>
        public enum KvSurfaceLogState
        {
            /// <summary>Стол найден и подсвечен зелёным — рабочий случай.</summary>
            Table,

            /// <summary>Найден пол, стола нет: пол столом не считается.</summary>
            FloorOnly,

            /// <summary>Плоскости есть, но ни одна не прошла классификатор.</summary>
            PlanesNoTable,

            /// <summary>Плоскостей нет И трекинга нет: искать бессмысленно, нужна причина.</summary>
            TrackingLost,

            /// <summary>Плоскостей нет при живом трекинге — обычная ситуация первых секунд.</summary>
            NoPlanes,

            /// <summary>Плоскостей нет дольше порога — дополнительно печатается диагностика.</summary>
            NoPlanesLong
        }

        /// <summary>
        /// СОСТОЯНИЕ РАСПОЗНАВАНИЯ — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без планшета).
        ///
        /// ПОРЯДОК ПРОВЕРОК И ЕСТЬ ПРАВИЛО ПРИОРИТЕТА, и он не случаен:
        ///   1) СТОЛ — рабочий случай, он перекрывает всё остальное. Это анти-вакуум: правило
        ///      «плоскостей нет» не имеет права перехватить сессию, где стол уже найден и подсвечен
        ///      (иначе оператор получил бы подсказку «поводите планшетом» при зелёном столе);
        ///   2) ПОЛ — тоже находка, пусть и не стол;
        ///   3) ПЛОСКОСТИ ЕСТЬ, СТОЛА НЕТ — своя формулировка, не путать с «плоскостей 0»;
        ///   4) ТРЕКИНГ ПОТЕРЯН — только когда не нашлось вообще ничего: сначала честно называем
        ///      потерю трекинга с причиной, иначе оператор ищет плоскости там, где не работает поза;
        ///   5) ПЛОСКОСТЕЙ НЕТ (трекинг идёт) — и, если ждём дольше порога, 6) с диагностикой.
        /// </summary>
        public static KvSurfaceLogState SurfaceLogStateOf(bool bestTable, bool floorFound, int planeCount,
                                                         ARSessionState trackingState,
                                                         float secondsWithoutPlanes)
        {
            if (bestTable) return KvSurfaceLogState.Table;
            if (floorFound) return KvSurfaceLogState.FloorOnly;
            if (planeCount > 0) return KvSurfaceLogState.PlanesNoTable;
            if (trackingState != ARSessionState.SessionTracking) return KvSurfaceLogState.TrackingLost;
            if (secondsWithoutPlanes >= SurfaceWaitSeconds) return KvSurfaceLogState.NoPlanesLong;
            return KvSurfaceLogState.NoPlanes;
        }

        /// <summary>
        /// КЛЮЧ СТРОКИ ЖУРНАЛА — по нему работает правило «печатать при СМЕНЕ, а не каждую секунду».
        ///
        /// В ключ НЕ входят секунды ожидания, координаты и площади: иначе текст менялся бы каждую
        /// секунду и журнал превратился бы в спам, который оператор перестаёт читать (а приёмка —
        /// считать доказательством). Смена трекинга в ключ входит: о потере позы надо сказать сразу.
        /// </summary>
        public static string SurfaceStateKey(bool bestTable, bool floorFound, int planeCount,
                                             ARSessionState trackingState, float secondsWithoutPlanes)
        {
            return SurfaceLogStateOf(bestTable, floorFound, planeCount, trackingState, secondsWithoutPlanes)
                       .ToString() + "|" + trackingState;
        }

        /// <summary>
        /// ПРАВИЛО «ПЕЧАТАТЬ ПРИ СМЕНЕ» — ЧИСТАЯ ФУНКЦИЯ: одинаковый ключ второй строки не даёт.
        /// Вынесена отдельно, чтобы это правило проверялось тестом, а не чтением `Update` глазами.
        /// </summary>
        public static bool ShouldLogSurfaceState(string previousKey, string currentKey)
        {
            return !string.Equals(previousKey ?? string.Empty, currentKey ?? string.Empty,
                System.StringComparison.Ordinal);
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О СОСТОЯНИИ ПОВЕРХНОСТЕЙ — ЧИСТАЯ ФУНКЦИЯ (без Unity-объектов и статики
        /// AR Foundation), поэтому проверяется тестом целиком, а не поиском слов в исходнике.
        ///
        /// ЧТО БЫЛО. Три состояния и молчание в четвёртом: при нуле плоскостей не печаталось ничего.
        /// ЧЕМ ПЛОХО. Оператор в планшете не понимал, работает ли распознавание, а приёмка не могла
        /// отличить «поверхностей нет» от «код сломался» — оба случая выглядели как тишина.
        /// ЧТО СТАЛО. Печатаются ЧЕТЫРЕ состояния с числами (плоскостей / трекаблов / секунды
        /// ожидания), фактическим состоянием трекинга и подсказкой, что делать. Формулировки про
        /// стол и пол оставлены прежними слово в слово: на них опирается приёмка и
        /// `Tools/real-table-device.ps1`.
        /// </summary>
        /// <param name="details">Готовый разбор классификатора (для строк про стол/пол/отказ).</param>
        /// <param name="notTrackingReason">Причина «не отслеживается» (для состояния «трекинг потерян»).</param>
        /// <param name="cameraDotUp">
        /// `dot(camera.forward, up)`. Добавлено 12.11.2026: при «плоскостей 0» строка ОБЯЗАНА
        /// сослаться на позу, если камера смотрит в потолок — иначе оператор и приёмка ищут поломку
        /// распознавания там, где планшет просто лежит экраном вверх. `null` — поза неизвестна,
        /// ссылки нет (и строка не врёт про причину, которой не видела).
        /// </param>
        public static string DescribeSurfaceState(int planeCount, int trackedPlaneCount,
            ARSessionState trackingState, float secondsWithoutPlanes, bool bestTable, bool floorFound,
            string details = null, string notTrackingReason = null, float? cameraDotUp = null)
        {
            // ССЫЛКА НА ПОЗУ. В отдельную переменную, чтобы правило было одно на все состояния и
            // проверялось одной чистой функцией (`PoseCulpritSuffix`): при нормальной позе — пусто.
            string poseCulprit = cameraDotUp.HasValue
                ? PoseCulpritSuffix(cameraDotUp.Value)
                : string.Empty;

            switch (SurfaceLogStateOf(bestTable, floorFound, planeCount, trackingState, secondsWithoutPlanes))
            {
                case KvSurfaceLogState.Table:
                    return SurfaceLogPrefix + DetailOr(details, "стол найден") +
                           " · подсвечен зелёным, робот встанет на него по кнопке «Поставить»";

                case KvSurfaceLogState.FloorOnly:
                    return SurfaceLogPrefix + DetailOr(details, "пол найден, стола нет") +
                           " · пол НЕ считается столом — вожу камерой по столешнице";

                case KvSurfaceLogState.PlanesNoTable:
                    // Секунд ожидания здесь нет намеренно: плоскости уже найдены, ожидание кончилось,
                    // и «ждём 0 с» было бы шумом, по которому оператор ищет смысл.
                    return SurfaceLogPrefix + "плоскости есть (" + planeCount + "), но стола нужной " +
                           "площади нет — " + DetailOr(details, "классификатор не признал ни одну") +
                           " · трекаблов " + trackedPlaneCount +
                           " · трекинг " + trackingState;

                case KvSurfaceLogState.TrackingLost:
                    return SurfaceLogPrefix + "трекинг НЕ идёт · сессия " + trackingState +
                           (string.IsNullOrEmpty(notTrackingReason)
                               ? string.Empty
                               : " · причина: " + notTrackingReason) +
                           " · плоскостей " + planeCount +
                           " · трекаблов " + trackedPlaneCount +
                           " · ждём " + SecondsText(secondsWithoutPlanes) + " с" +
                           " · что делать: " + TrackingHint;

                default: // NoPlanes и NoPlanesLong: текст один, разница только в дополнительной диагностике
                    // ССЫЛКА НА ПОЗУ ВСТАВЛЯЕТСЯ ПЕРЕД ПОДСКАЗКОЙ «что делать»: именно здесь
                    // «поводите планшетом над столом» бесполезно, если камера смотрит в потолок.
                    // При нормальной позе `poseCulprit` пуст и строка остаётся прежней слово в слово
                    // (на неё опирается `Tools/ar-planes-verify.ps1`).
                    return SurfaceLogPrefix + "плоскостей " + planeCount +
                           " · трекаблов " + trackedPlaneCount +
                           " · трекинг " + trackingState +
                           " · ждём " + SecondsText(secondsWithoutPlanes) + " с" +
                           poseCulprit +
                           " · что делать: " + SurfaceHint;
            }
        }

        /// <summary>
        /// ДОПОЛНИТЕЛЬНАЯ ДИАГНОСТИКА, когда поверхностей нет дольше 15 с при живом трекинге —
        /// ЧИСТАЯ ФУНКЦИЯ. Печатается ОДИН раз за эпизод (правило смены состояния).
        ///
        /// ЗАЧЕМ. «Плоскостей нет» само по себе не отвечает на вопрос приёмки «кто виноват»: то ли
        /// подсистема не запущена, то ли запрошен только вертикальный поиск, то ли ARCore не строит
        /// карту. Поэтому печатаются: состояние подсистемы, запрошенный и ФАКТИЧЕСКИ применяемый
        /// режим обнаружения, число трекаблов и подсказка оператору. Текст намеренно начинается с
        /// «ПОВЕРХНОСТИ: ДИАГНОСТИКА (плоскостей нет N с при трекинге …)» — эту форму ищет гейт
        /// `Tools/ar-planes-verify.ps1`.
        /// </summary>
        public static string DescribeSurfaceDiagnosis(int planeCount, int trackedPlaneCount,
            ARSessionState trackingState, float secondsWithoutPlanes, string requestedDetectionMode,
            string currentDetectionMode, bool subsystemRunning, string notTrackingReason = null)
        {
            string line = SurfaceLogPrefix + "ДИАГНОСТИКА (плоскостей нет " +
                          SecondsText(secondsWithoutPlanes) + " с при трекинге " + trackingState + ")";
            line += " · сессия " + trackingState;
            line += " · подсистема плоскостей: " + (subsystemRunning ? "работает" : "ОСТАНОВЛЕНА");
            line += " · запрошено: " + DetailOr(requestedDetectionMode, "нет данных");
            line += " · применяется: " + DetailOr(currentDetectionMode, "нет данных");
            line += " · трекаблов: " + trackedPlaneCount;
            line += " · плоскостей: " + planeCount;
            if (!string.IsNullOrEmpty(notTrackingReason))
                line += " · причина нетрекинга: " + notTrackingReason;
            line += " · что делать: " + SurfaceHint;
            return line;
        }

        /// <summary>СЕКУНДЫ В СТРОКЕ: целое число, без культуры устройства (иначе «16,3 с» на планшете).</summary>
        private static string SecondsText(float seconds)
        {
            float value = seconds > 0f ? seconds : 0f;
            return value.ToString("0", CultureInfo.InvariantCulture);
        }

        /// <summary>Подробность, если она есть; иначе короткая замена — строка не должна быть пустой.</summary>
        private static string DetailOr(string details, string fallback)
        {
            return string.IsNullOrEmpty(details) ? fallback : details;
        }

        /// <summary>
        /// СТРОКА-ДОКАЗАТЕЛЬСТВО В ЖУРНАЛ — не чаще раза в секунду и только при смене состояния.
        ///
        /// ЧТО БЫЛО (часть 1). Прежний журнал печатал «стол найден · площадь X м²» — то же самое,
        /// что было на экране, и по нему нельзя было понять, найден СТОЛ или ПОЛ.
        ///
        /// ЧТО БЫЛО (часть 2, дефект S45). Состояний осталось три, и четвёртое — «плоскостей НОЛЬ»,
        /// обычная ситуация (планшет лежит на столе, камера не видит поверхностей) — не печаталось
        /// вообще: в журнале с устройства за 75 с была только стартовая строка. Молчание — дефект.
        ///
        /// ЧТО СТАЛО. Печать идёт по чистым функциям выше: четыре состояния с числами, отдельное
        /// состояние «трекинг потерян» с причиной и дополнительная диагностика после 15 с ожидания —
        /// каждая строка ОДИН раз при смене состояния, без спама.
        /// </summary>
        private void LogTableOncePerSecond()
        {
            if (!logOnChange) return;
            if (Time.unscaledTime < nextLogTime) return;
            nextLogTime = Time.unscaledTime + 1f;

            int planes = planeManager != null ? planeManager.trackables.count : 0;
            int tracked = CountTrackingPlanes();

            // СКОЛЬКО ЖДЁМ. Отсчёт начинается в момент, когда плоскостей стало ноль, и обнуляется,
            // как только появилась хоть одна: иначе «ждём 300 с» врало бы после удачного поиска.
            if (planes > 0)
            {
                planesWithoutSince = 0f;
            }
            else if (planesWithoutSince <= 0f)
            {
                planesWithoutSince = Time.unscaledTime;
            }
            float seconds = planes == 0 && planesWithoutSince > 0f
                ? Time.unscaledTime - planesWithoutSince
                : 0f;

            ARSessionState tracking = ARSession.state;
            bool bestTable = BestTable != null;
            string key = SurfaceStateKey(bestTable, FloorFound, planes, tracking, seconds);
            if (!ShouldLogSurfaceState(loggedSurfaceState, key)) return;
            loggedSurfaceState = key;

            KvSurfaceLogState state = SurfaceLogStateOf(bestTable, FloorFound, planes, tracking, seconds);
            if (state == KvSurfaceLogState.NoPlanesLong)
            {
                // ОДНА дополнительная строка на эпизод: состояние подсистемы и режимы обнаружения.
                Debug.Log(DescribeSurfaceDiagnosis(planes, tracked, tracking, seconds,
                    DetectionModeText(true), DetectionModeText(false), SubsystemRunning()));
                return;
            }

            // ПОЗА КАМЕРЫ ПЕРЕДАЁТСЯ В СТРОКУ (12.11.2026). Без неё «плоскостей 0» и «камера
            // смотрит в потолок» остаются двумя несвязанными фактами, и приёмка ищет поломку
            // ARCore вместо того, чтобы опустить планшет.
            string line = DescribeSurfaceState(planes, tracked, tracking, seconds, bestTable, FloorFound,
                SurfaceDetails(), TrackingReasonText(), cameraDotUp);
            if (planeManager == null)
                line += " · менеджер плоскостей ARPlaneManager НЕ найден — распознавание не работает";
            Debug.Log(line);
        }

        /// <summary>
        /// СКОЛЬКО ПЛОСКОСТЕЙ РЕАЛЬНО В ТРЕКИНГЕ. `planeManager.trackables.count` отвечает «сколько
        /// менеджер знает», а оператору важно «сколько из них живы»: после потери позы ARCore
        /// оставляет трекаблы в состоянии Limited, и число «плоскостей 3» без этого уточнения
        /// выглядело бы как работающее распознавание.
        /// </summary>
        private int CountTrackingPlanes()
        {
            if (planeManager == null) return 0;
            int tracked = 0;
            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null) continue;
                if (plane.trackingState == TrackingState.Tracking) tracked++;
            }
            return tracked;
        }

        /// <summary>РЕЖИМ ОБНАРУЖЕНИЯ ПЛОСКОСТЕЙ (AR Foundation 6.5): запрошенный или фактический.</summary>
        private string DetectionModeText(bool requested)
        {
            if (planeManager == null) return "нет менеджера";
            return requested
                ? planeManager.requestedDetectionMode.ToString()
                : planeManager.currentDetectionMode.ToString();
        }

        /// <summary>ПОДСИСТЕМА ПЛОСКОСТЕЙ РАБОТАЕТ? Остановленная подсистема объясняет «плоскостей 0».</summary>
        private bool SubsystemRunning()
        {
            return planeManager != null && planeManager.subsystem != null && planeManager.subsystem.running;
        }

        /// <summary>
        /// ПРИЧИНА «НЕ ОТСЛЕЖИВАЕТСЯ» СЛОВАМИ. `ARSession.notTrackingReason` заполняет сама
        /// подсистема AR Foundation; в рабочем состоянии она равна None — тогда честно пишем, что
        /// причина не указана, а не выдумываем её.
        /// </summary>
        private static string TrackingReasonText()
        {
            NotTrackingReason reason = ARSession.notTrackingReason;
            if (reason == NotTrackingReason.None) return "не указана";
            return KvArSessionController.DescribeNotTracking(reason);
        }

        /// <summary>
        /// ПОДРОБНОСТИ ВЕРДИКТА ДЛЯ СТРОКИ — теми же словами, что печатались раньше: по ним
        /// приёмка (`Tools/real-table-device.ps1`) отличает «стол найден» от «пол (не стол)» и
        /// «стол не найден».
        /// </summary>
        private string SurfaceDetails()
        {
            string floorText = FloorFound
                ? " · уровень пола " + FloorHeight.ToString("0.00", CultureInfo.InvariantCulture) + " м"
                : " · пол пока не найден";
            if (BestTable != null) return DescribeTable + floorText + " · причина: " + BestTableReason;
            if (FloorFound) return DescribeTable + floorText;
            return DescribeTable;
        }

        /// <summary>УБРАТЬ ВСЮ ОТРИСОВКУ (очистка рабочего пространства).</summary>
        public void ClearVisuals()
        {
            foreach (KeyValuePair<TrackableId, Entry> pair in entries)
                if (pair.Value.root != null) Destroy(pair.Value.root);
            entries.Clear();
            VisualizedPlanes = 0;
            candidates.Clear();
            candidatePlanes.Clear();
            verdicts.Clear();
            firstSeenAt.Clear();
            hasLastBest = false;
            FloorFound = false;
            FloorHeight = 0f;
            loggedSurfaceState = string.Empty;
            // Отсчёт «сколько ждём плоскости» тоже сбрасывается: после очистки это новый эпизод,
            // и первая же строка журнала должна быть обычной, а не «плоскостей нет 200 с».
            planesWithoutSince = 0f;
            // ПОЗА КАМЕРЫ — НОВЫЙ ЭПИЗОД (12.11.2026). Ключ сбрасывается, иначе после очистки
            // рабочего пространства оператор не увидел бы в журнале, ЧТО планшет всё ещё смотрит
            // в потолок: состояние не изменилось, значит и строка не печатается.
            loggedCameraPose = string.Empty;
            cameraDotUp = 0f;
            ApplyNoTable("стол не найден");
        }

        void OnDestroy()
        {
            ClearVisuals();
            DestroyMaterial(ref materialTableOutline);
            DestroyMaterial(ref materialTableGrid);
            DestroyMaterial(ref materialHorizontalOutline);
            DestroyMaterial(ref materialHorizontalGrid);
            DestroyMaterial(ref materialVerticalOutline);
            DestroyMaterial(ref materialVerticalGrid);
            DestroyMaterial(ref tableFillMaterial);
            // ОБЩИЙ МЕШ ЗАЛИВКИ (P2-9, 11.11.2026). ЧТО БЫЛО: `quadMesh` — статическое поле,
            // ссылку на него держал каждый `MeshFilter` заливки, и никто его не уничтожал:
            // выйдя из AR, компонент уходил, а меш оставался в нативной памяти до конца прогона
            // (и переживал смену сцены). ЧЕМ ГРОЗИЛО: нативная память шлема течёт на каждом
            // входе в AR — приложение убивается системой без ошибки в журнале.
            // ТЕПЕРЬ меш снимается с фильтров и уничтожается вместе с компонентом — но только
            // когда ушёл ПОСЛЕДНИЙ визуализатор: меш статический, общий на все экземпляры.
            liveInstances--;
            if (liveInstances <= 0)
            {
                liveInstances = 0;
                DestroyQuadMesh();
            }
        }

        private static void DestroyQuadMesh()
        {
            if (quadMesh == null) return;
            if (Application.isPlaying) Destroy(quadMesh);
            else DestroyImmediate(quadMesh);
            quadMesh = null;
        }

        /// <summary>
        /// СБРОС СТАТИКИ МЕЖДУ ПРОГОНАМИ (P2-9). Статический меш переживает выход из PlayMode,
        /// если домен не перезагружается: следующий прогон получил бы ссылку на меш прошлой
        /// сцены (Unity его уже уничтожил, и поле выглядело бы «живым» — фальшиво-нулевой объект).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            quadMesh = null;
            liveInstances = 0;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material != null) Destroy(material);
            material = null;
        }
    }
}
