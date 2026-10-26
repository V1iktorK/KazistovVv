using System;
using System.Collections.Generic;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ТЕПЛОВАЯ КАРТА ДОСТИЖИМОСТИ (ЭТАП 8 ТЗ).
    ///
    /// Вокруг робота строится поверхность (сфера/эллипсоид для робота, КОЛЬЦО для SCARA),
    /// окрашенная по «стоимости» достижения точки: у центра — зелёный (легко), у края —
    /// красный (на пределе). Стоимость берётся у НАСТОЯЩЕГО оракула достижимости
    /// (`ReachabilityOracle`, собственный экземпляр — состояние потока не трогается):
    /// Safe → зелёный, Marginal → жёлтый, Collision/Unreachable → красный, плюс плавный
    /// радиальный градиент.
    ///
    /// ПРОИЗВОДИТЕЛЬНОСТЬ (требование ТЗ «не должна снижать FPS»):
    ///   • карта строится ТОЛЬКО при включении и при смене конфигурации робота;
    ///   • запросы к оракулу размазаны по кадрам (`samplesPerFrame`, по умолчанию 64);
    ///   • пока карта строится, ничего не пересчитывается в кадре, кроме этих 64 запросов;
    ///   • после постройки в кадре не делается НИЧЕГО (статичный меш с текстурой).
    /// </summary>
    public class KvReachabilityHeatmap
    {
        /// <summary>Разрешение карты (текселей по горизонтали/вертикали).</summary>
        public int textureWidth = 96;
        public int textureHeight = 48;
        /// <summary>Сколько запросов к оракулу делать за кадр при построении.</summary>
        public int samplesPerFrame = 64;
        /// <summary>Вес радиального градиента (0 — только вердикт оракула, 1 — только радиус).</summary>
        public float radialWeight = 0.3f;
        /// <summary>Прозрачность карты.</summary>
        public float alpha = 0.30f;

        /// <summary>Готово ли построение (false — карта ещё считается по кадрам).</summary>
        public bool Building { get; private set; }
        public bool Visible { get; private set; }
        /// <summary>Сколько текстур посчитано (диагностика).</summary>
        public int BuiltCount { get; private set; }
        /// <summary>Радиус построенной поверхности (м).</summary>
        public float BuiltRadius { get; private set; }

        public event Action<string> Message;

        private GameObject root;
        private MeshRenderer surfaceRenderer;
        private Material material;
        private Texture2D texture;

        private CollisionWorld world;
        private TrajectoryFlowController flow;
        private ReachabilityOracle oracle;

        private int cursor;
        private string signature = "";
        private Color[] pixels;
        private bool sphereMode = true;
        private Vector3 center;
        private float innerRadius;

        public bool IsVisible { get { return Visible; } }

        /// <summary>Точка поверхности в мире по индексу тексела (для диагностики/подсказки).</summary>
        public string LastSummary { get; private set; }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)
        {
            flow = controller;
            world = collisionWorld;
        }

        private ReachabilityOracle Oracle
        {
            get
            {
                if (oracle == null) oracle = new ReachabilityOracle();
                return oracle;
            }
        }

        /// <summary>Включить/выключить карту (кнопка тулбара).</summary>
        public void SetVisible(bool value)
        {
            Visible = value;
            if (value)
            {
                RequestRebuild(true);
                if (root != null) root.SetActive(true);
            }
            else if (root != null)
            {
                root.SetActive(false);
            }
            if (Message != null) Message("тепловая карта достижимости: " + (value ? "вкл" : "выкл"));
        }

        public void Toggle()
        {
            SetVisible(!Visible);
        }

        /// <summary>Конфигурация робота изменилась — пересчитать карту.</summary>
        public void RequestRebuild(bool force)
        {
            string now = Signature();
            if (!force && now == signature && root != null) return;
            signature = now;
            StartBuild();
        }

        private string Signature()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return "";
            RobotController robot = flow.Robot;
            string name = flow.Validator.RobotName;
            int dof = flow.Validator.Dof;
            return name + "|" + dof + "|" + (robot != null ? robot.transform.position.ToString("0.00") : "?");
        }

        private void StartBuild()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            RobotController robot = flow.Robot;
            if (robot == null) return;

            sphereMode = flow.Validator.Dof > 3;
            Vector3 basePos = flow.Validator.BasePosition;
            innerRadius = Mathf.Max(0.15f, Oracle.bodyZoneRadius + 0.25f);
            BuiltRadius = Mathf.Clamp(Oracle.workZoneRadius, 0.5f, 3.5f);
            center = sphereMode ? basePos + Vector3.up * 0.15f : basePos + Vector3.up * 0.02f;

            if (Oracle.RobotName != flow.Validator.RobotName || !Oracle.Ready)
                Oracle.Init(robot, world);

            int w = Mathf.Clamp(textureWidth, 32, 512);
            int h = Mathf.Clamp(textureHeight, 16, 256);
            if (pixels == null || pixels.Length != w * h) pixels = new Color[w * h];
            if (texture == null || texture.width != w || texture.height != h)
            {
                if (texture != null) UnityEngine.Object.Destroy(texture);
                texture = new Texture2D(w, h, TextureFormat.RGBA32, false);
                texture.hideFlags = HideFlags.HideAndDontSave;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Bilinear;
            }

            EnsureObject();      // форма поверхности: сфера (робот) или кольцо (SCARA)
            ApplyTransform();    // центр = база ТЕКУЩЕГО робота, радиус — рабочей зоны
            cursor = 0;
            Building = true;
        }

        private void EnsureObject()
        {
            // Форма карты зависит от робота: у 6-осевого это СФЕРА, у SCARA — КОЛЬЦО (плоский
            // квадрат с прозрачной серединой). Если робот сменился, примитив обязан
            // пересобраться: иначе после SCARA карта осталась бы сферой (и наоборот).
            if (root != null)
            {
                MeshFilter existing = root.GetComponent<MeshFilter>();
                string want = sphereMode ? "Sphere" : "Quad";
                bool right = existing != null && existing.sharedMesh != null &&
                             existing.sharedMesh.name.StartsWith(want, System.StringComparison.Ordinal);
                if (right) return;
                UnityEngine.Object.Destroy(root);      // форма не та — строим заново
                root = null;
                surfaceRenderer = null;
                material = null;
            }

            GameObject go = GameObject.CreatePrimitive(sphereMode ? PrimitiveType.Sphere : PrimitiveType.Quad);
            go.name = "ТепловаяКартаДостижимости";
            go.hideFlags = HideFlags.HideInHierarchy;      // служебный объект: вне иерархии и вне CollisionWorld
            Collider col = go.GetComponent<Collider>();
            if (col != null) UnityEngine.Object.Destroy(col);
            root = go;

            surfaceRenderer = go.GetComponent<MeshRenderer>();
            if (surfaceRenderer != null)
            {
                surfaceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                surfaceRenderer.receiveShadows = false;
                material = KvRenderPipelineShaders.CreateLit("Материал_теплокарты", new Color(1f, 1f, 1f));
                GhostMaterial.MakeGhost(material, new Color(1f, 1f, 1f), 1f);
                // Текстура кладётся в ПРАВИЛЬНОЕ для конвейера свойство: у HDRP/Lit это
                // `_BaseColorMap`, у URP/Lit — `_BaseMap`. Раньше имя было жёстко HDRP-шным,
                // и под URP теплокарта осталась бы белой (запись в чужое свойство молчит).
                KvRenderPipelineShaders.ApplyTexture(material, texture, false);
                if (material.HasProperty("_EmissiveColorMap")) material.SetTexture("_EmissiveColorMap", texture);
                if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0f);
                if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
                surfaceRenderer.sharedMaterial = material;
            }

            ApplyTransform();
        }

        /// <summary>
        /// Позиция/поворот/масштаб поверхности под текущего робота. Вызывается на КАЖДОЙ
        /// пересборке: центр (база робота) и радиус меняются при смене робота, поэтому
        /// «выставить один раз при создании» недостаточно — карта уезжала бы к прежнему стенду.
        /// </summary>
        private void ApplyTransform()
        {
            if (root == null) return;
            if (sphereMode)
            {
                root.transform.position = center;
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one * (BuiltRadius * 2f);
            }
            else
            {
                // КОЛЬЦО для SCARA: плоский квадрат в плоскости стола, внутренняя зона
                // делается ПРОЗРАЧНОЙ (alpha = 0) — визуально получается именно кольцо.
                root.transform.position = center;
                root.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                root.transform.localScale = new Vector3(BuiltRadius * 2f, BuiltRadius * 2f, 1f);
            }
        }

        /// <summary>Один шаг построения (вызывается из кадрового обновления хаба).</summary>
        public void Tick()
        {
            if (!Building || texture == null) return;

            int w = texture.width;
            int h = texture.height;
            int budget = Mathf.Clamp(samplesPerFrame, 8, 4096);

            while (budget-- > 0 && cursor < w * h)
            {
                int x = cursor % w;
                int y = cursor / w;
                float u = (x + 0.5f) / w;
                float v = (y + 0.5f) / h;

                Vector3 point;
                float radial;
                if (sphereMode)
                {
                    point = SpherePoint(u, v);
                    radial = Mathf.Clamp01((point - center).magnitude / Mathf.Max(0.01f, BuiltRadius));
                }
                else
                {
                    Vector2 planar;
                    point = PlanePoint(u, v, out planar);
                    radial = Mathf.Clamp01(planar.magnitude / Mathf.Max(0.01f, BuiltRadius));
                    if (planar.magnitude < innerRadius)
                    {
                        pixels[y * w + x] = new Color(0f, 0f, 0f, 0f);   // внутреннее отверстие кольца
                        cursor++;
                        continue;
                    }
                }

                float cost = radial * radialWeight +
                             (1f - radialWeight) * VerdictCost(point);
                pixels[y * w + x] = CostColor(Mathf.Clamp01(cost));
                cursor++;
            }

            if (cursor >= w * h)
            {
                texture.SetPixels(pixels);
                texture.Apply(false);
                Building = false;
                BuiltCount++;
                LastSummary = "карта достижимости · " + (sphereMode ? "сфера" : "кольцо") +
                              " R = " + BuiltRadius.ToString("0.00") + " м · текселей " + (w * h);
                Debug.Log("[Heatmap] " + LastSummary);
                if (Message != null) Message(LastSummary);
            }
        }

        private Vector3 SpherePoint(float u, float v)
        {
            float phi = u * Mathf.PI * 2f;
            float theta = v * Mathf.PI;
            float r = BuiltRadius;
            return center + new Vector3(
                Mathf.Sin(theta) * Mathf.Cos(phi) * r,
                Mathf.Cos(theta) * r,
                Mathf.Sin(theta) * Mathf.Sin(phi) * r);
        }

        private Vector3 PlanePoint(float u, float v, out Vector2 planar)
        {
            float x = (u - 0.5f) * 2f * BuiltRadius;
            float z = (v - 0.5f) * 2f * BuiltRadius;
            planar = new Vector2(x, z);
            return center + new Vector3(x, 0f, z);
        }

        private float VerdictCost(Vector3 point)
        {
            if (!Oracle.Ready) return 0.5f;
            try
            {
                ReachResult result = Oracle.Query(point);
                switch (result.verdict)
                {
                    case ReachVerdict.Safe: return 0f;
                    case ReachVerdict.Marginal: return 0.5f;
                    case ReachVerdict.Collision: return 1f;
                    default: return 0.9f;
                }
            }
            catch (Exception)
            {
                return 0.5f;
            }
        }

        /// <summary>Палитра «легко → на пределе»: зелёный → жёлтый → красный.</summary>
        public Color CostColor(float cost)
        {
            Color color;
            if (cost < 0.5f)
                color = Color.Lerp(new Color(0.15f, 0.95f, 0.25f), new Color(1f, 0.90f, 0.15f), cost * 2f);
            else
                color = Color.Lerp(new Color(1f, 0.90f, 0.15f), new Color(1f, 0.12f, 0.10f), (cost - 0.5f) * 2f);
            color.a = alpha;
            return color;
        }

        public void Dispose()
        {
            if (material != null) UnityEngine.Object.Destroy(material);
            if (texture != null) UnityEngine.Object.Destroy(texture);
            if (root != null) UnityEngine.Object.Destroy(root);
            material = null;
            texture = null;
            root = null;
            Building = false;
        }
    }

    /// <summary>
    /// ТЕПЛОВАЯ КАРТА ЗАЗОРОВ (ЭТАП 9 ТЗ).
    ///
    /// Показывает, где траектория проходит близко к препятствиям: цвет участка — от зелёного
    /// (большой запас) до красного (опасно близко). Рисуется ПОВЕРХ траектории: для каждого
    /// варианта считаются зазоры по сэмплам (`PoseValidator.ClearanceAt` на том же мире
    /// столкновений, что у хаба) и участки группируются в уровни — на каждый уровень своя
    /// линия своего цвета, поэтому получается именно цветная «раскраска» пути, а не градиент
    /// по всей линии (LineRenderer умеет только один градиент вдоль линии).
    ///
    /// Все объекты — служебные (`HideInHierarchy`, без коллайдеров), в CollisionWorld не попадают.
    /// </summary>
    public class KvClearanceOverlay
    {
        /// <summary>Пороги зазора (м) для уровней: красный ≤ safety, оранжевый, жёлтый, зелёный.</summary>
        public float redThreshold = 0.02f;
        public float orangeThreshold = 0.05f;
        public float yellowThreshold = 0.10f;
        /// <summary>Толщина линии, м.</summary>
        public float width = 0.022f;

        public static readonly Color ColorRed = new Color(1f, 0.12f, 0.10f);
        public static readonly Color ColorOrange = new Color(1f, 0.52f, 0.10f);
        public static readonly Color ColorYellow = new Color(1f, 0.90f, 0.18f);
        public static readonly Color ColorGreen = new Color(0.25f, 0.95f, 0.30f);

        public bool Visible { get; private set; }
        /// <summary>Последний отчёт для панели (мин. зазор, число опасных участков).</summary>
        public string LastSummary { get; private set; }

        public event Action<string> Message;

        private Transform root;
        private readonly List<LineRenderer> lines = new List<LineRenderer>();
        private readonly List<Material> materials = new List<Material>();
        private CollisionWorld world;
        private TrajectoryFlowController flow;
        private string signature = "";

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld)
        {
            flow = controller;
            world = collisionWorld;
        }

        public void SetVisible(bool value)
        {
            Visible = value;
            if (value) Rebuild(true);
            else HideLines();
            if (Message != null) Message("тепловая карта зазоров: " + (value ? "вкл" : "выкл"));
        }

        public void Toggle()
        {
            SetVisible(!Visible);
        }

        /// <summary>Пересобрать при смене набора траекторий (вызывает хаб).</summary>
        public void RebuildIfNeeded()
        {
            if (!Visible) return;
            string now = Signature();
            if (now == signature) return;
            Rebuild(true);
        }

        private string Signature()
        {
            if (flow == null) return "";
            List<TrajectoryCandidate> list = flow.State.candidates;
            if (list == null || list.Count == 0) return "empty";
            string s = list.Count + ":";
            for (int i = 0; i < list.Count; i++)
                s += (list[i] != null && list[i].plan != null && list[i].plan.Path != null
                    ? list[i].plan.Path.Length : 0) + ",";
            return s + "|" + flow.State.selectedTrajectory;
        }

        /// <summary>Полная пересборка раскраски.</summary>
        public void Rebuild(bool force)
        {
            if (!Visible || flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            List<TrajectoryCandidate> candidates = flow.State.candidates;
            HideLines();
            if (candidates == null || candidates.Count == 0)
            {
                LastSummary = "нет траекторий";
                return;
            }

            int dangerous = 0;
            float worst = float.MaxValue;

            for (int c = 0; c < candidates.Count; c++)
            {
                TrajectoryCandidate cand = candidates[c];
                if (cand == null || cand.plan == null || cand.plan.Path == null) continue;
                Vector3[] points = cand.tube;
                if (points == null || points.Length < 2) continue;

                int n = Mathf.Min(points.Length, cand.plan.Path.Length);
                if (n < 2) continue;

                float[] clearance = new float[n];
                for (int i = 0; i < n; i++)
                {
                    Vector3 tcp;
                    Vector3[] nodes;
                    clearance[i] = flow.Validator.ClearanceAt(cand.plan.Path[i], world, out tcp, out nodes);
                    if (clearance[i] < worst) worst = clearance[i];
                    if (clearance[i] <= redThreshold) dangerous++;
                }

                // Группировка подряд идущих сэмплов одного уровня в отдельные линии.
                int start = 0;
                int level = Level(clearance[0]);
                for (int i = 1; i <= n; i++)
                {
                    int current = i < n ? Level(clearance[i]) : -1;
                    if (current == level) continue;
                    AddSegment(points, start, i - 1, LevelColor(level), c);
                    start = i;
                    level = current;
                }
            }

            signature = Signature();
            LastSummary = "карта зазоров: мин. зазор " +
                          (worst == float.MaxValue ? "—" : (worst * 1000f).ToString("0") + " мм") +
                          " · опасных сэмплов " + dangerous + " · линий " + lines.Count;
        }

        private int Level(float clearance)
        {
            if (clearance <= redThreshold) return 0;
            if (clearance <= orangeThreshold) return 1;
            if (clearance <= yellowThreshold) return 2;
            return 3;
        }

        private static Color LevelColor(int level)
        {
            switch (level)
            {
                case 0: return ColorRed;
                case 1: return ColorOrange;
                case 2: return ColorYellow;
                default: return ColorGreen;
            }
        }

        private void AddSegment(Vector3[] points, int from, int to, Color color, int candidateIndex)
        {
            if (to - from < 1) return;

            GameObject go = new GameObject("Зазор_" + candidateIndex + "_" + from);
            go.hideFlags = HideFlags.HideInHierarchy;
            if (root == null)
            {
                GameObject container = new GameObject("ТепловаяКартаЗазоров");
                container.hideFlags = HideFlags.HideInHierarchy;
                root = container.transform;
            }
            go.transform.SetParent(root, false);

            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = to - from + 1;
            line.startWidth = width;
            line.endWidth = width;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            Material mat = KvRenderPipelineShaders.CreateUnlit("Материал_линии_теплокарты", color);
            GhostMaterial.MakeGhost(mat, color, 0.95f);
            // Неоновый вид: линия светится и видна поверх «колбасок».
            GhostMaterial.SetGhostGlow(mat, color, 0.95f, 2.2f);
            line.material = mat;
            materials.Add(mat);

            for (int i = from; i <= to; i++) line.SetPosition(i - from, points[i]);
            lines.Add(line);
        }

        private void HideLines()
        {
            for (int i = 0; i < lines.Count; i++)
                if (lines[i] != null) UnityEngine.Object.Destroy(lines[i].gameObject);
            lines.Clear();
            for (int i = 0; i < materials.Count; i++)
                if (materials[i] != null) UnityEngine.Object.Destroy(materials[i]);
            materials.Clear();
            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }
        }

        public void Dispose()
        {
            HideLines();
        }
    }
}
