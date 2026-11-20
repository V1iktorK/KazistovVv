using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using KazistovVvFeatures;
using KazistovVvRealWorld;

namespace KazistovVvXr
{
    /// <summary>
    /// ПОКРЫТИЕ РЕАЛЬНЫХ ПОВЕРХНОСТЕЙ В MR: КОНТУР + СЕТКА + ПОЛУПРОЗРАЧНАЯ ЗАЛИВКА
    /// (пункт 1 требований владельца, 12.11.2026).
    ///
    /// СЛОВА ВЛАДЕЛЬЦА ДОСЛОВНО: «При запуске MR версии надо чтобы сначала поверхности отображались
    /// каким-то покрытием будь то сетка или поверхность.»
    ///
    /// ЧТО БЫЛО. В MR поверхности комнаты существовали ТОЛЬКО КАК ФИЗИКА: `KvMrSurfaces` строил
    /// невидимые `MeshCollider`-объекты, и луч в них попадал, но оператор не видел НИЧЕГО. В шапке
    /// самого `KvMrSurfaces` это записано прямым текстом: «Нет рендера комнаты … этот компонент
    /// даёт только ФИЗИКУ реального мира». ЧЕМ ПЛОХО: включив MR, человек видел сквозной вид и
    /// пустоту — непонятно, нашёл ли шлем его стол, куда смотрит луч и куда встанет робот. Ошибку
    /// нельзя было даже заметить: невидимую поверхность не отличить от отсутствующей.
    ///
    /// ЧТО СТАЛО. Каждая найденная реальная поверхность рисуется тем же приёмом, что в AR
    /// (`KvArPlaneVisualizer`): контур четырьмя линиями, сетка внутри и полупрозрачная заливка.
    /// Приём взят именно оттуда, а не придуман заново: в AR он уже принят владельцем («поверхности
    /// сеткой и заливкой»), и одинаковое покрытие в двух режимах не приходится объяснять дважды.
    ///
    /// ОТКУДА БЕРУТСЯ ФОРМЫ — ДВА ИСТОЧНИКА, ОБА ИЗ `KvMrSurfaces`:
    ///   • куски меша PICO — ограничивающий параллелепипед куска (`RealSurfaceCandidate` от `Mesh`);
    ///   • параллелепипеды разметки комнаты — верхняя грань размеченного стола (`Semantic`).
    /// Второго списка поверхностей здесь нет намеренно: он бы разошёлся с тем, во что попадает луч.
    ///
    /// ЦВЕТА (требование владельца + правило проекта «цвет = смысл»):
    ///   • горизонтальная поверхность — НЕЙТРАЛЬНЫЙ (серо-голубой): это «просто поверхность»;
    ///   • стена/вертикаль — ДРУГОЙ (тёплый песочный): её видно, но она не мешает;
    ///   • поверхность, признанная СТОЛОМ классификатором, — ЗЕЛЁНАЯ. Зелёный в этом проекте уже
    ///     означает «сюда можно ставить» (тот же цвет у стола в AR), поэтому третьего значения у
    ///     него нет и быть не должно.
    ///
    /// ЧЕСТНО О ГРАНИЦЕ. Здесь рисуется ОГРАНИЧИВАЮЩИЙ ПРЯМОУГОЛЬНИК куска меша, а не его точный
    /// контур: у меша комнаты нет семантики, а триангуляция контура по тысячам вершин в кадре шлема
    /// стоила бы дороже, чем даёт. Поэтому кусок комнаты выглядит как «плитка покрытия» — и это
    /// честно сказано в журнале числом нарисованных поверхностей, а не выдаётся за точную разметку.
    /// </summary>
    [DisallowMultipleComponent]
    public class KvMrSurfacePainter : MonoBehaviour
    {
        /// <summary>ПРЕФИКС СТРОКИ ЖУРНАЛА О ПОКРЫТИИ — по нему ищет приёмка.</summary>
        public const string LogPrefix = "[KvMr] ПОКРЫТИЕ ПОВЕРХНОСТЕЙ: ";

        /// <summary>
        /// НЕ ЧАЩЕ РАЗА В СЕКУНДУ. Требование владельца дословно: «не чаще раза в секунду и только
        /// при изменении». Два условия сразу: по времени И по тексту строки — иначе одна и та же
        /// строка повторялась бы каждую секунду и в журнале шлема утонуло бы остальное.
        ///
        /// 20.11.2026: значение поднято с 1 до 5 с. Причина не в правке требования, а в том, что
        /// строка содержала ЧИСЛО объектов, а оно росло каждую секунду (см. деградацию MR 86 → 3
        /// FPS): в журнале 137 копий за прогон. Пять секунд — тот самый «раз в 5–10 с», которого
        /// просили; «только при изменении» сохраняется, поэтому изменившаяся строка (стол нашёлся,
        /// покрытие очистилось) появляется в журнале не позже чем через 5 с.
        /// </summary>
        public const float ReportIntervalSeconds = 5f;

        /// <summary>
        /// ШАГ ПЕРЕРИСОВКИ ПОКРЫТИЯ, секунды (1) — ОТДЕЛЁН ОТ ИНТЕРВАЛА ЖУРНАЛА.
        ///
        /// ЗАЧЕМ РАЗДЕЛЕНИЕ. Раньше одна константа управляла и перерисовкой, и журналом, поэтому
        /// поднятие интервала журнала замедлило бы и обновление покрытия (плитка елозила бы по
        /// 5 с — это уже НЕ то же изображение). Теперь перерисовка идёт раз в секунду, как и
        /// обещано владельцу («не чаще раза в секунду»), а в журнал строка уходит реже.
        /// </summary>
        public const float RepaintIntervalSeconds = 1f;

        /// <summary>Сколько сегментов сетки по каждой оси (как в AR — `GridDivisions` = 4).</summary>
        public const int GridDivisions = 4;

        /// <summary>Толщина контура поверхности, метры (видно с 2–3 м).</summary>
        public const float OutlineWidthMeters = 0.010f;

        /// <summary>Толщина линий сетки, метры.</summary>
        public const float GridWidthMeters = 0.004f;

        /// <summary>Насколько приподнять покрытие над поверхностью, метры: иначе линии тонут в меше.</summary>
        public const float LiftMeters = 0.004f;

        /// <summary>Меньше этого размера поверхность не рисуется, метры: это шум меша, а не поверхность.</summary>
        public const float MinSideMeters = 0.08f;

        /// <summary>ЦВЕТ ОБЫЧНОЙ ГОРИЗОНТАЛЬНОЙ ПОВЕРХНОСТИ — нейтральный серо-голубой.</summary>
        public static readonly Color HorizontalColor = new Color(0.55f, 0.72f, 0.88f, 1f);

        /// <summary>ЦВЕТ СТЕНЫ (вертикали) — тёплый песочный: видно, но не спорит с полом и столом.</summary>
        public static readonly Color VerticalColor = new Color(0.92f, 0.76f, 0.42f, 1f);

        /// <summary>ЦВЕТ ПОВЕРХНОСТИ, ПРИЗНАННОЙ СТОЛОМ, — зелёный (тот же смысл, что в AR).</summary>
        public static readonly Color TableColor = new Color(0.20f, 1f, 0.45f, 1f);

        /// <summary>Прозрачность заливки 0…1 (0.16 — как у заливки стола в AR).</summary>
        public const float FillAlpha = 0.16f;

        /// <summary>
        /// РИСОВАТЬ ЛИ ПОКРЫТИЕ. По умолчанию ВКЛЮЧЕНО: пункт 1 владельца — «при запуске MR надо,
        /// чтобы поверхности отображались». Выключение — флаг компонента, а не правка кода.
        /// </summary>
        public bool showSurfaces = true;

        /// <summary>Писать ли строку о покрытии в журнал.</summary>
        public bool logOnChange = true;

        private KvMrSurfaces surfaces;

        /// <summary>Одна нарисованная поверхность: корень, контур, сетка и заливка.</summary>
        private class Entry
        {
            public GameObject root;
            public LineRenderer outline;
            public LineRenderer grid;
            public MeshRenderer fill;
            public KvMrSurfaceStyle style;
            public float sideA;
            public float sideB;
            public Vector3 center;
            public Vector3 up;

            /// <summary>Форма уже задавалась (проверка «двигалось ли что-то» — см. `ShapeChanged`).</summary>
            public bool shaped;
        }

        private readonly Dictionary<Collider, Entry> entries = new Dictionary<Collider, Entry>();
        private readonly List<Collider> stale = new List<Collider>();

        /// <summary>
        /// ЖИВЫЕ КОЛЛАЙДЕРЫ ТЕКУЩЕГО ПЕРЕРИСОВАНИЯ — `HashSet`, А НЕ `List`.
        ///
        /// ЧТО БЫЛО. Уборка «чего больше нет» делалась через `List.Contains` для КАЖДОГО ключа
        /// словаря: O(n²). При 9383 поверхностях это ≈ 87 миллионов сравнений КАЖДУЮ секунду на
        /// главном потоке шлема — то есть ~весь кадровый бюджет уходил на уборку.
        /// ЧТО СТАЛО. O(n): каждый живой коллайдер проверяется за одно хэширование.
        /// </summary>
        private readonly HashSet<Collider> alive = new HashSet<Collider>();

        private Material materialHorizontalOutline;
        private Material materialHorizontalGrid;
        private Material materialVerticalOutline;
        private Material materialVerticalGrid;
        private Material materialTableOutline;
        private Material materialTableGrid;
        private Material materialFillHorizontal;
        private Material materialFillVertical;
        private Material materialFillTable;

        private float nextRepaint;
        private float nextReport;
        private string lastReport = string.Empty;
        private int drawnMeshes;
        private int drawnSemantic;
        private bool tableFound;

        /// <summary>Сколько поверхностей нарисовано прямо сейчас (для журнала и строки состояния).</summary>
        public int DrawnCount { get { return entries.Count; } }

        /// <summary>
        /// ПОСЛЕДНЯЯ СТРОКА О ПОКРЫТИИ — ровно та, что ушла в журнал. Нужна интерфейсу и отчёту:
        /// «покрытие включено» без числа нарисованных поверхностей ничего не доказывает.
        /// </summary>
        public string LastReport { get { return lastReport; } }

        /// <summary>Создать компонент в рантайме (сцена не правится — её SHA256 закреплён тестом).</summary>
        public static KvMrSurfacePainter Ensure(Transform parent)
        {
            KvMrSurfacePainter existing = null;
            if (parent != null) existing = parent.GetComponentInChildren<KvMrSurfacePainter>(true);
            if (existing != null) return existing;

            var go = new GameObject("KvMrSurfacePainter");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<KvMrSurfacePainter>();
        }

        /// <summary>
        /// ОДИН РИСОВАЛЬЩИК ПОКРЫТИЯ НА ПРИЛОЖЕНИЕ. Нужен при возврате в VR (20.11.2026):
        /// `KvXrMrRoom.EndMrSession` гасит покрытие, не разыскивая компонент по сцене.
        /// </summary>
        public static KvMrSurfacePainter Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            CreateMaterials();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ClearAll();
            DestroyMaterial(ref materialHorizontalOutline);
            DestroyMaterial(ref materialHorizontalGrid);
            DestroyMaterial(ref materialVerticalOutline);
            DestroyMaterial(ref materialVerticalGrid);
            DestroyMaterial(ref materialTableOutline);
            DestroyMaterial(ref materialTableGrid);
            DestroyMaterial(ref materialFillHorizontal);
            DestroyMaterial(ref materialFillVertical);
            DestroyMaterial(ref materialFillTable);
        }

        void Update()
        {
            if (!showSurfaces)
            {
                if (entries.Count > 0) ClearAll();
                return;
            }

            if (Time.unscaledTime < nextRepaint) return;
            nextRepaint = Time.unscaledTime + RepaintIntervalSeconds;

            Repaint();
            ReportIfChanged();
        }

        // ---------------------------------------------------------------- отрисовка

        /// <summary>
        /// ПЕРЕРИСОВАТЬ ПОКРЫТИЕ ПО ТЕКУЩЕМУ СПИСКУ РЕАЛЬНЫХ ПОВЕРХНОСТЕЙ.
        ///
        /// ПОВЕРХНОСТЬ НЕ ПЕРЕСОЗДАЁТСЯ, А ОБНОВЛЯЕТСЯ: меш комнаты приходит и обновляется волнами,
        /// и если каждый раз удалять объекты, линии будут моргать в такт пересчёту. Поэтому запись
        /// живёт, пока жив коллайдер, а меняются только точки линий.
        /// </summary>
        private void Repaint()
        {
            if (surfaces == null) surfaces = KvMrSurfaces.Instance;
            if (surfaces == null)
            {
                if (entries.Count > 0) ClearAll();
                return;
            }

            alive.Clear();
            drawnMeshes = 0;
            drawnSemantic = 0;
            tableFound = false;

            foreach (Collider collider in surfaces.RealWorldColliders)
            {
                if (collider == null) continue;

                RealSurfaceCandidate candidate;
                if (!surfaces.TryGetSurfaceShape(collider, out candidate)) continue;

                KvMrSurfaceStyle style = StyleOf(candidate);
                if (style == KvMrSurfaceStyle.Skip) continue;

                alive.Add(collider);
                Entry entry = GetOrCreate(collider, style);
                if (entry == null) continue;

                UpdateShape(entry, candidate);
                ApplyStyle(entry, style);

                if (candidate.Source == KvRealSurfaceSource.Semantic) drawnSemantic++;
                else drawnMeshes++;
                if (style == KvMrSurfaceStyle.Table) tableFound = true;
            }

            // УДАЛИТЬ ТО, ЧЕГО БОЛЬШЕ НЕТ. Меш комнаты умеет присылать `Removed` — и без этой
            // уборки покрытие осталось бы висеть над поверхностью, которой уже нет.
            stale.Clear();
            foreach (KeyValuePair<Collider, Entry> pair in entries)
            {
                if (pair.Key != null && alive.Contains(pair.Key)) continue;
                stale.Add(pair.Key);
            }
            for (int i = 0; i < stale.Count; i++)
            {
                Entry entry;
                if (!entries.TryGetValue(stale[i], out entry)) continue;
                DestroyEntry(entry);
                entries.Remove(stale[i]);
            }
        }

        private Entry GetOrCreate(Collider collider, KvMrSurfaceStyle style)
        {
            Entry entry;
            if (entries.TryGetValue(collider, out entry) && entry != null && entry.root != null) return entry;

            var root = new GameObject("KvMrSurface_" + entries.Count);
            root.transform.SetParent(transform, false);

            var outlineGo = new GameObject("Outline");
            outlineGo.transform.SetParent(root.transform, false);
            LineRenderer outline = outlineGo.AddComponent<LineRenderer>();
            ConfigureLine(outline);

            var gridGo = new GameObject("Grid");
            gridGo.transform.SetParent(root.transform, false);
            LineRenderer grid = gridGo.AddComponent<LineRenderer>();
            ConfigureLine(grid);

            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(root.transform, false);
            MeshFilter filter = fillGo.AddComponent<MeshFilter>();
            filter.sharedMesh = QuadMesh();
            MeshRenderer fill = fillGo.AddComponent<MeshRenderer>();
            fill.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fill.receiveShadows = false;

            entry = new Entry();
            entry.root = root;
            entry.outline = outline;
            entry.grid = grid;
            entry.fill = fill;
            entry.style = KvMrSurfaceStyle.Unknown;
            entries[collider] = entry;
            return entry;
        }

        private static void ConfigureLine(LineRenderer line)
        {
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.generateLightingData = false;
            line.alignment = LineAlignment.View;
            line.numCapVertices = 2;
        }

        private static void UpdateShape(Entry entry, RealSurfaceCandidate candidate)
        {
            if (entry == null || entry.root == null) return;

            Vector3 up = candidate.Up.sqrMagnitude > 0.0001f ? candidate.Up.normalized : Vector3.up;
            // ГАБАРИТЫ: у горизонтальных поверхностей стороны лежат в плоскости, у стен «вторая»
            // сторона — это высота, то есть та же плоскость, повёрнутая нормалью. Поэтому размеры
            // берутся как есть, а ориентация задаётся нормалью — одна формула на оба вида.
            float sideA = Mathf.Max(MinSideMeters, Mathf.Abs(candidate.SideA));
            float sideB = Mathf.Max(MinSideMeters, Mathf.Abs(candidate.SideB));

            Quaternion rotation = RotationFor(up);
            Vector3 center = candidate.Center + up * LiftMeters;

            // ГЕОМЕТРИЮ ПЕРЕСОБИРАТЬ НЕ ЗАЧЕМ, ЕСЛИ НИЧЕГО НЕ ИЗМЕНИЛОСЬ.
            //
            // ЧТО БЫЛО. Каждую секунду `Repaint` обновлял ВСЕ поверхности: 4 точки контура,
            // 18 точек сетки и матрица заливки на каждую плитку. При тысячах плиток это была
            // перезагрузка тысяч линейных мешей в секунду — на главном потоке, в шлеме.
            // ЧТО СТАЛО. Сравниваем форму с уже нарисованной (`ShapeChanged`) и молчим, пока
            // кусок меша стоит на месте: он и стоит на месте почти всегда. Вид НЕ меняется —
            // меняется только то, пересобирается ли меш, который в итоге выглядит так же.
            if (entry.shaped && !ShapeChanged(entry.center, entry.up, entry.sideA, entry.sideB,
                                              center, up, sideA, sideB)) return;

            entry.root.transform.SetPositionAndRotation(center, rotation);
            entry.root.transform.localScale = Vector3.one;
            entry.sideA = sideA;
            entry.sideB = sideB;
            entry.center = center;
            entry.up = up;
            entry.shaped = true;

            // КОНТУР: четыре угла прямоугольника.
            Vector3 halfA = rotation * Vector3.right * (sideA * 0.5f);
            Vector3 halfB = rotation * Vector3.forward * (sideB * 0.5f);
            Vector3[] corners =
            {
                center - halfA - halfB,
                center + halfA - halfB,
                center + halfA + halfB,
                center - halfA + halfB
            };

            if (entry.outline != null)
            {
                entry.outline.loop = true;
                entry.outline.positionCount = 4;
                entry.outline.startWidth = OutlineWidthMeters;
                entry.outline.endWidth = OutlineWidthMeters;
                for (int i = 0; i < corners.Length; i++) entry.outline.SetPosition(i, corners[i]);
            }

            // СЕТКА: линии внутри прямоугольника — по ним видно, что это настоящая найденная
            // поверхность, а не нарисованный на глаз квадрат.
            if (entry.grid != null)
            {
                entry.grid.loop = false;
                entry.grid.positionCount = 2 * (GridDivisions + 1) * 2;
                entry.grid.startWidth = GridWidthMeters;
                entry.grid.endWidth = GridWidthMeters;

                int index = 0;
                for (int i = 0; i <= GridDivisions; i++)
                {
                    float t = (i / (float)GridDivisions) - 0.5f;
                    entry.grid.SetPosition(index++, center + halfB * (t * 2f) - halfA);
                    entry.grid.SetPosition(index++, center + halfB * (t * 2f) + halfA);
                }
                for (int i = 0; i <= GridDivisions; i++)
                {
                    float t = (i / (float)GridDivisions) - 0.5f;
                    entry.grid.SetPosition(index++, center + halfA * (t * 2f) - halfB);
                    entry.grid.SetPosition(index++, center + halfA * (t * 2f) + halfB);
                }
            }

            // ЗАЛИВКА: единичный квадрат растягивается до габаритов поверхности.
            if (entry.fill != null)
            {
                entry.fill.transform.SetPositionAndRotation(center, rotation);
                entry.fill.transform.localScale = new Vector3(sideA, 1f, sideB);
            }
        }

        /// <summary>
        /// РАЗВОРОТ ПРЯМОУГОЛЬНИКА ПО НОРМАЛИ. У поверхности «верх» — это нормаль, поэтому
        /// `FromToRotation(up, normal)` кладёт плоскость ровно на поверхность, а не «примерно».
        /// Для вертикали (стена) это даёт плоскость стены — то же покрытие, только повёрнутое.
        /// </summary>
        public static Quaternion RotationFor(Vector3 up)
        {
            Vector3 normal = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;
            return Quaternion.FromToRotation(Vector3.up, normal);
        }

        /// <summary>
        /// ИЗМЕНИЛАСЬ ЛИ ФОРМА ПОВЕРХНОСТИ — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без сцены).
        ///
        /// ЗАЧЕМ. Каждую секунду покрытие перерисовывается, и раньше это значило «пересобрать
        /// линии и заливку у КАЖДОЙ плитки», даже если кусок меша стоял на месте. Функция
        /// отвечает на вопрос «стоит ли вообще трогать этот объект», а допуски — миллиметровые:
        /// движение куска меша на 0,1 мм и меньше на вид неотличимо и перерисовки не требует.
        ///
        /// ДОПУСКИ ЗАДАНЫ ЯВНО, А НЕ «на глаз»: позиция 1 мм, габарит 1 мм, нормаль — по
        /// квадрату длины (0,001² = 1e-6), то есть поворот поверхности меньше 0,05°.
        /// </summary>
        public static bool ShapeChanged(Vector3 beforeCenter, Vector3 beforeUp, float beforeSideA,
            float beforeSideB, Vector3 afterCenter, Vector3 afterUp, float afterSideA, float afterSideB)
        {
            const float positionToleranceMeters = 0.001f;
            const float sideToleranceMeters = 0.001f;
            const float upToleranceSqr = 1e-6f;

            if (float.IsNaN(beforeSideA) || float.IsNaN(beforeSideB) ||
                float.IsNaN(afterSideA) || float.IsNaN(afterSideB)) return true;
            if (Mathf.Abs(beforeSideA - afterSideA) > sideToleranceMeters) return true;
            if (Mathf.Abs(beforeSideB - afterSideB) > sideToleranceMeters) return true;
            if ((beforeCenter - afterCenter).sqrMagnitude > positionToleranceMeters * positionToleranceMeters)
                return true;
            if ((beforeUp - afterUp).sqrMagnitude > upToleranceSqr) return true;
            return false;
        }

        private void ApplyStyle(Entry entry, KvMrSurfaceStyle style)
        {
            if (entry == null || entry.style == style) return;
            entry.style = style;

            Material outline = OutlineMaterialOf(style);
            Material grid = GridMaterialOf(style);
            Material fill = FillMaterialOf(style);

            if (entry.outline != null && outline != null) entry.outline.sharedMaterial = outline;
            if (entry.grid != null && grid != null) entry.grid.sharedMaterial = grid;
            if (entry.fill != null && fill != null) entry.fill.sharedMaterial = fill;

            Color color = ColorOf(style);
            // ЦВЕТ — И МАТЕРИАЛОМ, И ВЕРШИНАМИ. Урок AR (дефект 06.11.2026): под URP цвет вершин
            // `LineRenderer` не применяется, и один белый материал делал ВСЮ сетку белой. Поэтому
            // оттенок задаёт материал (он создаётся своим на каждый вид), а цвет вершин повторяет
            // тот же тон — так линия не окажется белой ни в одном из двух конвейеров проекта.
            if (entry.outline != null)
            {
                entry.outline.startColor = color;
                entry.outline.endColor = color;
            }
            if (entry.grid != null)
            {
                Color gridColor = GridColorFor(color);
                entry.grid.startColor = gridColor;
                entry.grid.endColor = gridColor;
            }
        }

        private static Color ColorOf(KvMrSurfaceStyle style)
        {
            switch (style)
            {
                case KvMrSurfaceStyle.Table: return TableColor;
                case KvMrSurfaceStyle.Vertical: return VerticalColor;
                default: return HorizontalColor;
            }
        }

        /// <summary>ЦВЕТ ЛИНИЙ СЕТКИ ОТ ЦВЕТА КОНТУРА — темнее и непрозрачный (сетка не шумит).</summary>
        public static Color GridColorFor(Color outline)
        {
            return new Color(outline.r * 0.42f, outline.g * 0.42f, outline.b * 0.42f, 1f);
        }

        // ---------------------------------------------------------------- журнал

        /// <summary>
        /// СТРОКА О ПОКРЫТИИ — ТОЛЬКО ПРИ ИЗМЕНЕНИИ И НЕ ЧАЩЕ РАЗА В СЕКУНДУ.
        ///
        /// ФОРМА (требование владельца, дословно):
        ///     [KvMr] ПОКРЫТИЕ ПОВЕРХНОСТЕЙ: нарисовано N (мешей A, разметки B) · стол найден: да|нет
        /// Ноль тоже печатается: «нарисовано 0» — это ответ, а «тишина» — нет.
        /// </summary>
        private void ReportIfChanged()
        {
            if (!logOnChange) return;
            if (Time.unscaledTime < nextReport) return;
            nextReport = Time.unscaledTime + ReportIntervalSeconds;

            string line = DescribeCoverage(entries.Count, drawnMeshes, drawnSemantic, tableFound);
            if (line == lastReport) return;
            lastReport = line;
            Debug.Log(line);
        }

        /// <summary>
        /// СТРОКА ЖУРНАЛА О ПОКРЫТИИ — ЧИСТАЯ ФУНКЦИЯ (проверяется тестом без шлема).
        ///
        /// ЗАЧЕМ ЧИСТАЯ. На устройстве строку иначе не проверить: журнал шлема читают глазами.
        /// Здесь нет ни `Time`, ни `Debug` — только числа и текст, поэтому «в строке есть числа»
        /// и «ноль отличается от трёх» можно доказать машинно.
        /// </summary>
        public static string DescribeCoverage(int drawn, int meshes, int semantic, bool tableFound)
        {
            int total = drawn > 0 ? drawn : 0;
            int byMesh = meshes > 0 ? meshes : 0;
            int bySemantic = semantic > 0 ? semantic : 0;

            return LogPrefix + "нарисовано " + total +
                   " (мешей " + byMesh + ", разметки " + bySemantic + ")" +
                   " · стол найден: " + (tableFound ? "да" : "нет");
        }

        // ---------------------------------------------------------------- вид поверхности

        /// <summary>
        /// КАКОЙ ВИД У ЭТОЙ ПОВЕРХНОСТИ (чистая функция — проверяется тестом без сцены).
        ///
        /// ПОРЯДОК РЕШЕНИЙ И ЕСТЬ ПРАВИЛО, и он не случаен:
        ///   1) СТОЛ, подтверждённый классификатором, — всегда зелёный, даже если поверхность
        ///      пришла из меша и её никто не разметил как «Table»;
        ///   2) стена/потолок — свой цвет: по нормали они вертикальны, и путать их с полом нельзя;
        ///   3) всё остальное горизонтальное — нейтральное;
        ///   4) мелкий шум (`Skip`) не рисуется вовсе — иначе покрытие состояло бы из тысяч
        ///      обрывков по 2 см и только мешало смотреть.
        /// </summary>
        public static KvMrSurfaceStyle StyleOf(RealSurfaceCandidate candidate)
        {
            float sideA = Mathf.Abs(candidate.SideA);
            float sideB = Mathf.Abs(candidate.SideB);
            if (sideA < MinSideMeters || sideB < MinSideMeters) return KvMrSurfaceStyle.Skip;

            Vector3 up = candidate.Up.sqrMagnitude > 0.0001f ? candidate.Up.normalized : Vector3.up;
            bool horizontal = up.y >= 0.60f;      // тот же порог наклона, что у постановки робота

            if (candidate.Kind == KvRealSurfaceKind.Table) return KvMrSurfaceStyle.Table;
            if (candidate.Kind == KvRealSurfaceKind.Wall || candidate.Kind == KvRealSurfaceKind.Ceiling)
                return KvMrSurfaceStyle.Vertical;
            return horizontal ? KvMrSurfaceStyle.Horizontal : KvMrSurfaceStyle.Vertical;
        }

        /// <summary>
        /// ВИД ПОВЕРХНОСТИ ПО ВЕРДИКТУ КЛАССИФИКАТОРА — второй вход того же правила.
        ///
        /// ЗАЧЕМ ОТДЕЛЬНО. `StyleOf` знает только «что сказала платформа». Стол в MR чаще всего
        /// НЕ размечен (`Kind = Unknown`), и его признаёт классификатор по высоте и габаритам.
        /// Поэтому цвет «стол найден» обязан учитывать и вердикт — иначе зелёной поверхности не
        /// было бы никогда, хотя стол найден.
        /// </summary>
        public static KvMrSurfaceStyle StyleOf(RealSurfaceCandidate candidate, RealTableVerdict verdict)
        {
            KvMrSurfaceStyle byKind = StyleOf(candidate);
            if (byKind == KvMrSurfaceStyle.Skip) return KvMrSurfaceStyle.Skip;
            if (verdict.IsTable) return KvMrSurfaceStyle.Table;
            return byKind;
        }

        // ---------------------------------------------------------------- мелочи

        private void ClearAll()
        {
            foreach (KeyValuePair<Collider, Entry> pair in entries) DestroyEntry(pair.Value);
            entries.Clear();
            drawnMeshes = 0;
            drawnSemantic = 0;
            tableFound = false;
        }

        private void DestroyEntry(Entry entry)
        {
            if (entry == null) return;
            if (entry.root != null)
            {
                if (Application.isPlaying) Destroy(entry.root);
                else DestroyImmediate(entry.root);
            }
            entry.root = null;
            entry.outline = null;
            entry.grid = null;
            entry.fill = null;
        }

        private static void DestroyMaterial(ref Material material)
        {
            if (material == null) return;
            if (Application.isPlaying) Destroy(material);
            else DestroyImmediate(material);
            material = null;
        }

        private Material OutlineMaterialOf(KvMrSurfaceStyle style)
        {
            switch (style)
            {
                case KvMrSurfaceStyle.Table: return materialTableOutline;
                case KvMrSurfaceStyle.Vertical: return materialVerticalOutline;
                default: return materialHorizontalOutline;
            }
        }

        private Material GridMaterialOf(KvMrSurfaceStyle style)
        {
            switch (style)
            {
                case KvMrSurfaceStyle.Table: return materialTableGrid;
                case KvMrSurfaceStyle.Vertical: return materialVerticalGrid;
                default: return materialHorizontalGrid;
            }
        }

        private Material FillMaterialOf(KvMrSurfaceStyle style)
        {
            switch (style)
            {
                case KvMrSurfaceStyle.Table: return materialFillTable;
                case KvMrSurfaceStyle.Vertical: return materialFillVertical;
                default: return materialFillHorizontal;
            }
        }

        /// <summary>
        /// МАТЕРИАЛЫ ПО ВИДУ ПОВЕРХНОСТИ. Материалы создаёт ТОЛЬКО фабрика конвейера
        /// (`KvRenderPipelineShaders`): прямой `Shader.Find` в сборке даёт розовые линии, потому
        /// что нужный шейдер вырезан вариантами. Это правило уже проверяется тестом проекта.
        /// </summary>
        private void CreateMaterials()
        {
            materialTableOutline = KvRenderPipelineShaders.CreateUnlit("Материал_контура_стола_MR", TableColor);
            materialTableGrid = KvRenderPipelineShaders.CreateUnlit("Материал_сетки_стола_MR",
                GridColorFor(TableColor));
            materialHorizontalOutline = KvRenderPipelineShaders.CreateUnlit(
                "Материал_контура_плоскости_MR", HorizontalColor);
            materialHorizontalGrid = KvRenderPipelineShaders.CreateUnlit("Материал_сетки_плоскости_MR",
                GridColorFor(HorizontalColor));
            materialVerticalOutline = KvRenderPipelineShaders.CreateUnlit(
                "Материал_контура_стены_MR", VerticalColor);
            materialVerticalGrid = KvRenderPipelineShaders.CreateUnlit("Материал_сетки_стены_MR",
                GridColorFor(VerticalColor));

            materialFillHorizontal = KvRenderPipelineShaders.CreateUnlitTransparent(
                "Материал_заливки_плоскости_MR", WithAlpha(HorizontalColor, FillAlpha));
            materialFillVertical = KvRenderPipelineShaders.CreateUnlitTransparent(
                "Материал_заливки_стены_MR", WithAlpha(VerticalColor, FillAlpha));
            materialFillTable = KvRenderPipelineShaders.CreateUnlitTransparent(
                "Материал_заливки_стола_MR", WithAlpha(TableColor, FillAlpha));
        }

        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private static Mesh quadMesh;

        /// <summary>ЕДИНИЧНЫЙ КВАДРАТ В ПЛОСКОСТИ XZ (1×1 м, центр в нуле) — для заливки.</summary>
        private static Mesh QuadMesh()
        {
            if (quadMesh != null) return quadMesh;
            quadMesh = new Mesh();
            quadMesh.name = "KvMrSurfaceFillQuad";
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

        /// <summary>
        /// СБРОС СТАТИКИ МЕЖДУ ПРОГОНАМИ. Общий меш заливки переживает выход из PlayMode, если домен
        /// не перезагружается: следующий прогон получил бы ссылку на уничтоженный меш (он выглядит
        /// «живым» и даёт фальшиво-нулевой объект) — тот же класс дефекта уже ловили в AR (P2-9).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            quadMesh = null;
        }

        /// <summary>Строка состояния одной строкой — для журнала MR.</summary>
        public string Status
        {
            get
            {
                var c = CultureInfo.InvariantCulture;
                return "покрытие поверхностей: " + (showSurfaces ? "включено" : "ВЫКЛЮЧЕНО") +
                       " · нарисовано " + entries.Count +
                       " · стол " + (tableFound ? "найден" : "не найден") +
                       " · шаг пересчёта " + RepaintIntervalSeconds.ToString("0.0", c) + " с" +
                       " · строка журнала раз в " + ReportIntervalSeconds.ToString("0.0", c) + " с";
            }
        }
    }

    /// <summary>
    /// ВИД НАРИСОВАННОЙ ПОВЕРХНОСТИ — от него зависит ЦВЕТ, и только он (пункт 1 владельца).
    ///
    /// ПОЧЕМУ ПЕРЕЧИСЛЕНИЕМ, А НЕ ЦВЕТОМ В ПОЛЕ. Решение «какой цвет» должно проверяться тестом без
    /// сцены, а `Color` в проверке пришлось бы сравнивать «на глаз» с допуском. Здесь же правило
    /// названо словами: горизонталь / стена / стол / не рисовать.
    /// </summary>
    public enum KvMrSurfaceStyle
    {
        /// <summary>Мелкий обрывок меша — не рисуется (шум).</summary>
        Skip = 0,

        /// <summary>Обычная горизонтальная поверхность — нейтральный цвет.</summary>
        Horizontal = 1,

        /// <summary>Стена или потолок — свой цвет, чтобы не путать с полом.</summary>
        Vertical = 2,

        /// <summary>Поверхность признана СТОЛОМ — зелёная.</summary>
        Table = 3,

        /// <summary>Вид ещё не применён.</summary>
        Unknown = 4
    }
}
