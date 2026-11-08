using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using KazistovVvFeatures;

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
    ///   • самую большую горизонтальную плоскость подсвечивает ЗЕЛЁНЫМ и подписывает «СТОЛ» —
    ///     это место, куда встанет робот по кнопке «Поставить»;
    ///   • остальные горизонтальные — синие, вертикальные — серые (не мешают);
    ///   • отдаёт `TryGetBestTable` — по нему `KvArPlacement` ставит якорь ровно на стол,
    ///     даже если луч из центра экрана не попал в плоскость.
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

        /// <summary>Минимальная площадь стола, м² — меньше считаем шумом.</summary>
        public float minTableAreaMeters = 0.15f;

        /// <summary>Как часто обновлять сетку (секунды).</summary>
        public float refreshSeconds = 0.25f;

        /// <summary>Писать в журнал о найденном столе (обязательная строка-доказательство).</summary>
        public bool logOnChange = true;

        /// <summary>Лучший найденный стол (самая большая горизонтальная плоскость).</summary>
        public ARPlane BestTable { get; private set; }

        /// <summary>Площадь лучшего стола, м².</summary>
        public float BestTableArea { get; private set; }

        /// <summary>Центр лучшего стола в мировых координатах.</summary>
        public Vector3 BestTableCenter { get; private set; }

        /// <summary>Нормаль лучшего стола (вверх).</summary>
        public Vector3 BestTableNormal { get; private set; } = Vector3.up;

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
        private bool loggedTable;
        private bool loggedNoTable;
        private bool loggedLabelFont;
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
            if (!showPlanes || planeManager == null) return;

            SyncEntries();
            ChooseBestTable();
            RecolorPlanes();
            LogTableOncePerSecond();
        }

        /// <summary>ЛУЧШИЙ СТОЛ: самая большая горизонтальная плоскость (для постановки робота).</summary>
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

        private void ChooseBestTable()
        {
            ARPlane best = null;
            float bestArea = 0f;
            Vector3 bestCenter = Vector3.zero;
            Vector3 bestNormal = Vector3.up;

            foreach (ARPlane plane in planeManager.trackables)
            {
                if (plane == null) continue;
                bool horizontal = plane.alignment == PlaneAlignment.HorizontalUp ||
                                  plane.alignment == PlaneAlignment.HorizontalDown;
                if (!horizontal) continue;

                float width = Mathf.Abs(plane.size.x * plane.transform.lossyScale.x);
                float depth = Mathf.Abs(plane.size.y * plane.transform.lossyScale.z);
                float area = width > 0f && depth > 0f ? width * depth : 0f;
                if (area < minTableAreaMeters) continue;
                if (area <= bestArea) continue;

                best = plane;
                bestArea = area;
                bestCenter = plane.transform.position;
                bestNormal = plane.alignment == PlaneAlignment.HorizontalUp
                    ? plane.transform.up
                    : -plane.transform.up;
            }

            BestTable = best;
            BestTableArea = bestArea;
            BestTableCenter = bestCenter;
            BestTableNormal = bestNormal;
            if (best != null)
            {
                float cameraHeight = arCamera != null
                    ? bestCenter.y - arCamera.transform.position.y
                    : 0f;
                DescribeTable = "стол найден · площадь " + bestArea.ToString("0.00") +
                                " м² · камера выше стола на " + Mathf.Abs(cameraHeight).ToString("0.00") +
                                " м";
            }
            else
            {
                DescribeTable = "стол не найден";
            }
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

        private void LogTableOncePerSecond()
        {
            if (!logOnChange) return;
            if (Time.unscaledTime < nextLogTime) return;
            nextLogTime = Time.unscaledTime + 1f;

            if (BestTable != null && !loggedTable)
            {
                loggedTable = true;
                loggedNoTable = false;
                Debug.Log("[KvAr] ПОВЕРХНОСТИ: " + DescribeTable + " · подсвечен зелёным, " +
                          "робот встанет на него по кнопке «Поставить»");
            }
            else if (BestTable == null && VisualizedPlanes > 0 && !loggedNoTable)
            {
                loggedNoTable = true;
                loggedTable = false;
                Debug.Log("[KvAr] ПОВЕРХНОСТИ: плоскости есть (" + VisualizedPlanes +
                          "), но стола нужной площади нет — вожу камерой");
            }
        }

        /// <summary>УБРАТЬ ВСЮ ОТРИСОВКУ (очистка рабочего пространства).</summary>
        public void ClearVisuals()
        {
            foreach (KeyValuePair<TrackableId, Entry> pair in entries)
                if (pair.Value.root != null) Destroy(pair.Value.root);
            entries.Clear();
            VisualizedPlanes = 0;
            BestTable = null;
            BestTableArea = 0f;
            DescribeTable = "стол не найден";
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
