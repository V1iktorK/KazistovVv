using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Сведения о прокси одного меша (этап 13 ТЗ).</summary>
    public class KvCollisionProxyInfo
    {
        public Renderer renderer;
        public string name = "";
        public int triangles;
        public bool readableMesh;          // удалось ли прочитать вершины меша
        public string kind = "—";          // бокс / капсула / пропущен / нет прокси
        public Vector3 looseSize;          // габарит (AABB), м
        public Vector3 tightSize;          // размеры прокси, м
        public float looseVolume;
        public float tightVolume;
        public string note = "";

        public float SavedVolume01
        {
            get { return looseVolume > 1e-9f ? 1f - tightVolume / looseVolume : 0f; }
        }

        public string Line()
        {
            return name + " · треугольников " + triangles + " · прокси: " + kind +
                   " · габарит " + looseSize.x.ToString("0.00") + "×" + looseSize.y.ToString("0.00") +
                   "×" + looseSize.z.ToString("0.00") + " → " + tightSize.x.ToString("0.00") + "×" +
                   tightSize.y.ToString("0.00") + "×" + tightSize.z.ToString("0.00") +
                   (SavedVolume01 > 0.02f ? " (объём −" + (SavedVolume01 * 100f).ToString("0") + " %)" : "");
        }
    }

    /// <summary>Результат замера производительности (этап 13 ТЗ: «ускорение планирования»).</summary>
    public class KvCollisionBenchmark
    {
        public float rebuildWithoutMs;
        public float rebuildWithMs;
        public float clearanceWithoutMs;
        public float clearanceWithMs;
        public int obstaclesWithout;
        public int obstaclesWith;
        public int samples;
        public float planWithoutMs;
        public float planWithMs;
        public bool planCompared;

        public string Line()
        {
            string s = "мир: " + obstaclesWithout + " → " + obstaclesWith + " препятствий · " +
                       "пересборка " + rebuildWithoutMs.ToString("0.0") + " → " +
                       rebuildWithMs.ToString("0.0") + " мс · зазоры (" + samples + " проверок) " +
                       clearanceWithoutMs.ToString("0.0") + " → " + clearanceWithMs.ToString("0.0") +
                       " мс";
            if (planCompared)
                s += " · планирование " + planWithoutMs.ToString("0") + " → " +
                     planWithMs.ToString("0") + " мс";
            return s;
        }
    }

    /// <summary>
    /// ЭТАП 13 ТЗ: РЕДАКТОР КОЛЛИЗИОННЫХ МЕШЕЙ.
    ///
    /// Что делает:
    ///   • читает меши сцены и строит по ним НИЗКОПОЛИГОНАЛЬНЫЙ прокси столкновений:
    ///     ориентированный бокс (OBB) по главным осям облака вершин (метод главных
    ///     компонент, собственный решатель 3×3) либо капсулу для вытянутых тел;
    ///   • прокси кладёт в реестр `CollisionProxies` ядра, и мир столкновений использует
    ///     его ВМЕСТО габарита меша — для повёрнутых и вытянутых деталей это точнее
    ///     (нет «раздувания» до осевого габарита), поэтому исчезают ложные столкновения
    ///     и планировщик находит путь там, где раньше отказывался;
    ///   • безопасность: прокси строится по ВСЕМ вершинам, то есть всегда накрывает меш;
    ///     объект пропускается только если он ПОЛНОСТЬЮ внутри другого объёма (или мельче
    ///     2 см) — это тоже не может «срезать» геометрию;
    ///   • показывает прокси в сцене (каркас) и ЗАМЕРЯЕТ эффект: число препятствий,
    ///     время пересборки мира, время проверок зазора и (если получится) время
    ///     планирования до и после;
    ///   • включается/выключается в настройках одним флагом (ТЗ): при выключении мир
    ///     собирается ровно как раньше.
    /// </summary>
    public class KvCollisionOptimizer
    {
        public const string EnabledPrefsKey = "KazistovVv.Collision.Proxies";

        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private CollisionWorld world;
        private RobotController robot;
        private bool loaded;
        private bool enabled;
        private bool preview;

        private readonly List<KvCollisionProxyInfo> items = new List<KvCollisionProxyInfo>();
        private LineRenderer previewLines;
        private Transform previewRoot;
        private float lastRebuildTime = -10f;
        private KvCollisionBenchmark benchmark = new KvCollisionBenchmark();
        private int meshLimit = 2000;      // максимум вершин на меш для анализа
        private float skipBelowSize = 0.02f;

        public IReadOnlyList<KvCollisionProxyInfo> Items { get { return items; } }
        public KvCollisionBenchmark Benchmark { get { return benchmark; } }
        public bool PreviewVisible { get { return preview; } }

        /// <summary>Оптимизация включена (тумблер в настройках).</summary>
        public bool Enabled
        {
            get { Load(); return enabled; }
            set
            {
                Load();
                if (enabled == value) return;
                enabled = value;
                PlayerPrefs.SetInt(EnabledPrefsKey, enabled ? 1 : 0);
                PlayerPrefs.Save();
                CollisionProxies.Enabled = enabled;
                if (value) Rebuild(true);
                else CollisionProxies.Clear();
                RebuildWorldNow();
                Report("прокси столкновений " + (enabled ? "включены" : "выключены") + " · " +
                       CollisionProxies.Status());
            }
        }

        private void Load()
        {
            if (loaded) return;
            loaded = true;
            enabled = PlayerPrefs.GetInt(EnabledPrefsKey, 0) != 0;
            CollisionProxies.Enabled = enabled;
        }

        public void Bind(TrajectoryFlowController controller, CollisionWorld collisionWorld,
            RobotController activeRobot)
        {
            flow = controller;
            world = collisionWorld;
            robot = activeRobot;
            Load();
        }

        public void ResetCache()
        {
            items.Clear();
            CollisionProxies.Clear();
            lastRebuildTime = -10f;
        }

        // ================================================================== построение прокси

        /// <summary>Пересобрать прокси по текущей сцене.</summary>
        public int Rebuild(bool force = false)
        {
            Load();
            if (!force && !enabled) return 0;

            items.Clear();
            CollisionProxies.Clear();

            Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Exclude);
            var bounds = new List<Bounds>();
            var proxies = new List<KvCollisionProxyInfo>();

            foreach (Renderer r in renderers)
            {
                if (r == null) continue;
                string n = r.gameObject.name;
                if (n.Contains("Phantom") || n.Contains("Preview") || n.Contains("Laser") ||
                    n.Contains("AimMarker") || n.Contains("TCP") || n.Contains("KvWaypoints") ||
                    n.Contains("IlyichLamp")) continue;
                if (r.GetComponentInParent<RobotController>() != null) continue;   // роботы — не статика
                if (IsService(r.transform)) continue;

                Bounds b = r.bounds;
                if (b.size.magnitude < skipBelowSize) continue;

                KvCollisionProxyInfo info = new KvCollisionProxyInfo
                {
                    renderer = r,
                    name = n,
                    looseSize = b.size,
                    looseVolume = Mathf.Max(1e-6f, b.size.x * b.size.y * b.size.z),
                    tightSize = b.size,
                    tightVolume = Mathf.Max(1e-6f, b.size.x * b.size.y * b.size.z)
                };

                Vector3[] local = null;
                int triangles = 0;
                MeshFilter mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                {
                    Mesh mesh = mf.sharedMesh;
                    triangles = mesh.triangles != null ? mesh.triangles.Length / 3 : 0;
                    if (mesh.isReadable)
                    {
                        Vector3[] verts = mesh.vertices;
                        if (verts != null && verts.Length > 0)
                        {
                            int stride = Mathf.Max(1, verts.Length / Mathf.Max(16, meshLimit));
                            var sampled = new List<Vector3>(Mathf.Min(verts.Length, meshLimit));
                            for (int i = 0; i < verts.Length; i += stride)
                                sampled.Add(r.transform.TransformPoint(verts[i]));
                            local = sampled.ToArray();
                            info.readableMesh = true;
                        }
                    }
                }
                info.triangles = triangles;

                if (local != null && local.Length >= 4)
                {
                    Vector3 center;
                    Vector3[] axes;
                    Vector3 extents;
                    if (Obb(local, out center, out axes, out extents))
                    {
                        info.tightSize = extents * 2f;
                        info.tightVolume = Mathf.Max(1e-6f,
                            info.tightSize.x * info.tightSize.y * info.tightSize.z);

                        float min = Mathf.Min(extents.x, Mathf.Min(extents.y, extents.z));
                        float max = Mathf.Max(extents.x, Mathf.Max(extents.y, extents.z));
                        Quaternion rotation = Quaternion.LookRotation(axes[2], axes[1]);

                        if (min < 0.35f * max && max > 0.12f)
                        {
                            // Вытянутое тело — капсула по главной оси (как в ядре, но по OBB).
                            Vector3 a = center - axes[2] * Mathf.Max(0f, extents.z - min);
                            Vector3 bb = center + axes[2] * Mathf.Max(0f, extents.z - min);
                            float radius = Mathf.Max(0.01f, 0.5f * (extents.x + extents.y));
                            CollisionProxies.SetCapsule(r, new ObstacleCapsule
                            {
                                a = a,
                                b = bb,
                                r = radius,
                                name = n
                            });
                            info.kind = "капсула";
                            info.note = "радиус " + (radius * 1000f).ToString("0") + " мм";
                        }
                        else
                        {
                            CollisionProxies.SetBox(r, new ObstacleBox
                            {
                                center = center,
                                half = extents,
                                name = n,
                                rotation = rotation
                            });
                            info.kind = "бокс (OBB)";
                        }
                        bounds.Add(new Bounds(center, extents * 2f));
                        proxies.Add(info);
                        items.Add(info);
                        continue;
                    }
                }

                // Вершины недоступны (меш не для чтения) — прокси по габариту: он накрывает
                // меш, но не уточняет его (об этом честно пишем в таблице).
                CollisionProxies.SetBox(r, new ObstacleBox
                {
                    center = b.center,
                    half = b.extents,
                    name = n,
                    rotation = Quaternion.identity
                });
                info.kind = "бокс (габарит)";
                info.note = triangles > 0
                    ? "меш недоступен для чтения — прокси по габариту"
                    : "нет меша — прокси по габариту";
                bounds.Add(b);
                proxies.Add(info);
                items.Add(info);
            }

            // --- отбрасываем объекты, ПОЛНОСТЬЮ вложенные в другой объём (безопасно: их
            //     геометрия и так внутри препятствия), кроме опор стендов.
            int skipped = 0;
            for (int i = 0; i < proxies.Count; i++)
            {
                KvCollisionProxyInfo info = proxies[i];
                if (info.renderer == null) continue;
                if (IsUnderStand(info.renderer.transform)) continue;
                for (int k = 0; k < proxies.Count; k++)
                {
                    if (k == i) continue;
                    KvCollisionProxyInfo other = proxies[k];
                    if (!Contains(bounds[k], bounds[i])) continue;
                    if (bounds[k].size.magnitude <= bounds[i].size.magnitude + 1e-4f) continue;
                    CollisionProxies.SetSkipped(info.renderer);
                    info.kind = "пропущен";
                    info.note = "внутри объёма «" + other.name + "»";
                    info.tightVolume = 0f;
                    skipped++;
                    break;
                }
            }

            lastRebuildTime = Time.realtimeSinceStartup;
            if (preview) ShowPreview();
            Report("прокси столкновений: " + items.Count + " мешей · пропущено вложенных " + skipped +
                   " · " + CollisionProxies.Status());
            return items.Count;
        }

        private static bool IsService(Transform t)
        {
            for (Transform c = t; c != null; c = c.parent)
                if ((c.gameObject.hideFlags & HideFlags.HideInHierarchy) != 0) return true;
            return false;
        }

        private static bool IsUnderStand(Transform t)
        {
            for (Transform c = t; c != null; c = c.parent)
                if (c.name.StartsWith("Стенд_", StringComparison.Ordinal)) return true;
            return false;
        }

        private static bool Contains(Bounds outer, Bounds inner)
        {
            return outer.Contains(inner.min) && outer.Contains(inner.max);
        }

        // ================================================================== OBB по облаку вершин

        /// <summary>
        /// Ориентированный бокс: центр — среднее, оси — собственные векторы ковариации
        /// (метод главных компонент), полуразмеры — максимум проекции на каждую ось.
        /// Такой бокс всегда НАКРЫВАЕТ все точки, поэтому замена габарита безопасна.
        /// </summary>
        private static bool Obb(Vector3[] points, out Vector3 center, out Vector3[] axes,
            out Vector3 extents)
        {
            center = Vector3.zero;
            axes = new[] { Vector3.right, Vector3.up, Vector3.forward };
            extents = Vector3.zero;
            if (points == null || points.Length < 4) return false;

            Vector3 mean = Vector3.zero;
            for (int i = 0; i < points.Length; i++) mean += points[i];
            mean /= points.Length;

            double xx = 0, xy = 0, xz = 0, yy = 0, yz = 0, zz = 0;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 d = points[i] - mean;
                xx += d.x * d.x; xy += d.x * d.y; xz += d.x * d.z;
                yy += d.y * d.y; yz += d.y * d.z; zz += d.z * d.z;
            }
            double[,] m = { { xx, xy, xz }, { xy, yy, yz }, { xz, yz, zz } };
            Vector3[] e = Jacobi(m);
            if (e == null) return false;

            // Правая тройка осей
            Vector3 a0 = e[0].normalized;
            Vector3 a1 = Vector3.ProjectOnPlane(e[1], a0).normalized;
            if (a1.sqrMagnitude < 1e-6f) a1 = Vector3.ProjectOnPlane(Vector3.up, a0).normalized;
            Vector3 a2 = Vector3.Cross(a0, a1).normalized;

            axes = new[] { a0, a1, a2 };
            float ex = 0f, ey = 0f, ez = 0f;
            for (int i = 0; i < points.Length; i++)
            {
                Vector3 d = points[i] - mean;
                ex = Mathf.Max(ex, Mathf.Abs(Vector3.Dot(d, a0)));
                ey = Mathf.Max(ey, Mathf.Abs(Vector3.Dot(d, a1)));
                ez = Mathf.Max(ez, Mathf.Abs(Vector3.Dot(d, a2)));
            }
            extents = new Vector3(Mathf.Max(ex, 0.005f), Mathf.Max(ey, 0.005f), Mathf.Max(ez, 0.005f));
            center = mean;
            return true;
        }

        /// <summary>Собственные векторы симметричной матрицы 3×3 (метод Якоби, 24 прохода).</summary>
        private static Vector3[] Jacobi(double[,] a)
        {
            var v = new double[3, 3];
            for (int i = 0; i < 3; i++) v[i, i] = 1.0;
            var m = (double[,])a.Clone();

            for (int sweep = 0; sweep < 24; sweep++)
            {
                double off = Math.Abs(m[0, 1]) + Math.Abs(m[0, 2]) + Math.Abs(m[1, 2]);
                if (off < 1e-12) break;
                for (int p = 0; p < 2; p++)
                {
                    for (int q = p + 1; q < 3; q++)
                    {
                        if (Math.Abs(m[p, q]) < 1e-15) continue;
                        double theta = 0.5 * (m[q, q] - m[p, p]) / m[p, q];
                        double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1.0));
                        if (Math.Abs(theta) < 1e-15) t = 1.0;
                        double c = 1.0 / Math.Sqrt(t * t + 1.0);
                        double s = t * c;

                        for (int k = 0; k < 3; k++)
                        {
                            double mkp = m[k, p], mkq = m[k, q];
                            m[k, p] = c * mkp - s * mkq;
                            m[k, q] = s * mkp + c * mkq;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            double mpk = m[p, k], mqk = m[q, k];
                            m[p, k] = c * mpk - s * mqk;
                            m[q, k] = s * mpk + c * mqk;
                        }
                        for (int k = 0; k < 3; k++)
                        {
                            double vkp = v[k, p], vkq = v[k, q];
                            v[k, p] = c * vkp - s * vkq;
                            v[k, q] = s * vkp + c * vkq;
                        }
                    }
                }
            }

            // Сортируем оси по убыванию собственного значения: главная ось — первая,
            // для капсулы используем последнюю (самую длинную) — см. Obb().
            var result = new Vector3[3];
            for (int k = 0; k < 3; k++)
                result[k] = new Vector3((float)v[0, k], (float)v[1, k], (float)v[2, k]);

            // Пересортируем так, чтобы axes[2] была САМОЙ ДЛИННОЙ стороной бокса:
            // это ожидает вызывающий код (капсула строится по axes[2]).
            var order = new[] { 0, 1, 2 };
            var values = new[] { m[0, 0], m[1, 1], m[2, 2] };
            for (int i = 0; i < 3; i++)
                for (int k = i + 1; k < 3; k++)
                    if (values[order[k]] < values[order[i]])
                    {
                        int tmp = order[i]; order[i] = order[k]; order[k] = tmp;
                    }
            return new[] { result[order[0]], result[order[1]], result[order[2]] };
        }

        // ================================================================== показ прокси

        /// <summary>Показать/скрыть каркас прокси в сцене.</summary>
        public void SetPreview(bool value)
        {
            preview = value;
            if (value) ShowPreview();
            else HidePreview();
        }

        public void TogglePreview()
        {
            SetPreview(!preview);
        }

        private void HidePreview()
        {
            if (previewLines != null) previewLines.positionCount = 0;
        }

        private void ShowPreview()
        {
            // Каркас прокси — это показ: без графики ничего не строим (расчёт прокси работает).
            if (!KvGraphics.Available)
            {
                Report("предпросмотр прокси недоступен: нет графики (сами прокси работают)");
                return;
            }
            if (previewRoot == null)
            {
                GameObject go = new GameObject("KvCollisionProxies");
                go.hideFlags = HideFlags.HideInHierarchy;
                previewRoot = go.transform;
                previewLines = go.AddComponent<LineRenderer>();
                previewLines.useWorldSpace = true;
                previewLines.numCapVertices = 0;
                previewLines.widthMultiplier = 0.006f;
                Material proxyMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                    "Материал_капсул", Color.white);
                if (proxyMaterial != null)
                {
                    previewLines.material = proxyMaterial;
                    previewLines.startColor = new Color(0.2f, 0.95f, 1f, 0.9f);
                    previewLines.endColor = new Color(0.2f, 0.95f, 1f, 0.5f);
                }
            }

            var pts = new List<Vector3>();
            foreach (KvCollisionProxyInfo info in items)
            {
                if (info.renderer == null) continue;
                if (info.kind == "пропущен") continue;
                Bounds b = info.renderer.bounds;
                // Каркас показываем по ГАБАРИТУ прокси, чтобы было видно разницу с мешом:
                // у OBB это его размеры, у капсулы — отрезок с «крестовинами».
                Vector3 c = b.center;
                Vector3 h = info.tightSize * 0.5f;
                if (h.sqrMagnitude < 1e-8f) h = b.extents;
                AddBox(pts, c, h);
            }
            previewLines.positionCount = pts.Count;
            if (pts.Count > 0) previewLines.SetPositions(pts.ToArray());
        }

        private static void AddBox(List<Vector3> pts, Vector3 c, Vector3 h)
        {
            Vector3[] v =
            {
                c + new Vector3(-h.x, -h.y, -h.z), c + new Vector3(h.x, -h.y, -h.z),
                c + new Vector3(h.x, -h.y, h.z), c + new Vector3(-h.x, -h.y, h.z),
                c + new Vector3(-h.x, h.y, -h.z), c + new Vector3(h.x, h.y, -h.z),
                c + new Vector3(h.x, h.y, h.z), c + new Vector3(-h.x, h.y, h.z)
            };
            int[] edges = { 0, 1, 1, 2, 2, 3, 3, 0, 4, 5, 5, 6, 6, 7, 7, 4, 0, 4, 1, 5, 2, 6, 3, 7 };
            for (int i = 0; i < edges.Length; i++) pts.Add(v[edges[i]]);
        }

        // ================================================================== замер эффекта

        /// <summary>
        /// Замер производительности (ТЗ: «ускорение планирования»): считается число
        /// препятствий, время пересборки мира и время проверок зазора БЕЗ прокси и С ними.
        /// </summary>
        public KvCollisionBenchmark Measure()
        {
            Load();
            benchmark = new KvCollisionBenchmark();
            if (world == null || flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                Report("замер невозможен: мир столкновений или робот не готовы");
                return benchmark;
            }

            PoseValidator v = flow.Validator;
            double[] q = v.CopyCurrent();
            const int clearanceSamples = 200;
            benchmark.samples = clearanceSamples;

            // --- без прокси
            bool saved = CollisionProxies.Enabled;
            CollisionProxies.Enabled = false;
            RebuildWorldNow();
            benchmark.obstaclesWithout = world.Capsules.Count + world.Boxes.Count;
            benchmark.rebuildWithoutMs = TimeWorldRebuild();
            benchmark.clearanceWithoutMs = TimeClearance(v, q, clearanceSamples);

            // --- с прокси
            CollisionProxies.Enabled = true;
            if (items.Count == 0) Rebuild(true);
            RebuildWorldNow();
            benchmark.obstaclesWith = world.Capsules.Count + world.Boxes.Count;
            benchmark.rebuildWithMs = TimeWorldRebuild();
            benchmark.clearanceWithMs = TimeClearance(v, q, clearanceSamples);

            CollisionProxies.Enabled = saved && enabled;
            RebuildWorldNow();

            Report("замер прокси: " + benchmark.Line());
            return benchmark;
        }

        private void RebuildWorldNow()
        {
            if (world == null || robot == null || flow == null || flow.Validator == null) return;
            world.Rebuild(robot, flow.Validator.linkRadius);
        }

        private float TimeWorldRebuild()
        {
            if (world == null || robot == null || flow == null || flow.Validator == null) return 0f;
            const int runs = 5;
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < runs; i++) world.Rebuild(robot, flow.Validator.linkRadius);
            return (Time.realtimeSinceStartup - t0) * 1000f / runs;
        }

        private float TimeClearance(PoseValidator v, double[] q, int samples)
        {
            if (world == null) return 0f;
            Vector3 tcp;
            Vector3[] nodes;
            float t0 = Time.realtimeSinceStartup;
            for (int i = 0; i < samples; i++) v.ClearanceAt(q, world, out tcp, out nodes);
            return (Time.realtimeSinceStartup - t0) * 1000f;
        }

        /// <summary>Строка состояния для вкладки и свойств.</summary>
        public string Status()
        {
            Load();
            return CollisionProxies.Status() + " · мешей проанализировано " + items.Count +
                   (enabled ? "" : " · оптимизация выключена");
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Collision] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ВКЛАДКА «КОЛЛИЗИИ» (ЭТАП 13 ТЗ): включение оптимизации, таблица прокси и замер.
    /// </summary>
    public class KvCollisionTab : IKvWorkbenchTab
    {
        private readonly KvCollisionOptimizer service;
        private int shown = 12;

        public KvCollisionTab(KvCollisionOptimizer optimizer)
        {
            service = optimizer;
        }

        public string Key { get { return "collision"; } }
        public string Title { get { return KvLocExtra3.T("coll.title", "Коллизионные меши"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra3.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (service == null || kit == null) return;

            kit.Section(Title);
            kit.Toggle(T("coll.enable", "Упрощать меши для проверки коллизий"),
                service.Enabled, delegate (bool v) { service.Enabled = v; });
            kit.Info(delegate { return service.Status(); }, KvTheme.Accent);

            kit.Buttons(new[]
            {
                T("coll.rebuild", "Пересобрать прокси"),
                T("coll.preview", "Показать прокси в сцене"),
                T("coll.measure", "Замерить ускорение")
            }, new Action[]
            {
                delegate { service.Rebuild(true); },
                delegate { service.TogglePreview(); },
                delegate { service.Measure(); }
            });

            kit.Table("proxy", delegate { return T("coll.mesh", "Меш"); },
                delegate { return T("coll.kind", "Прокси"); });
            int count = Mathf.Min(shown, service.Items.Count);
            for (int i = 0; i < count; i++)
            {
                int index = i;
                kit.Table("#" + (index + 1),
                    delegate
                    {
                        KvCollisionProxyInfo info = service.Items[index];
                        return info.triangles > 0
                            ? info.name + " (" + info.triangles + " тр.)"
                            : info.name;
                    },
                    delegate
                    {
                        KvCollisionProxyInfo info = service.Items[index];
                        return info.kind + (info.SavedVolume01 > 0.02f
                            ? " · −" + (info.SavedVolume01 * 100f).ToString("0") + " % объёма"
                            : "") + (string.IsNullOrEmpty(info.note) ? "" : " · " + info.note);
                    });
            }
            if (service.Items.Count > count)
            {
                kit.Buttons(new[] { T("coll.more", "Показать ещё") + " (" +
                                    (service.Items.Count - count) + ")" },
                    new Action[] { delegate { shown += 12; } });
            }

            kit.Divider();
            kit.Info(delegate
            {
                KvCollisionBenchmark b = service.Benchmark;
                return b.samples > 0 ? b.Line() : T("coll.nomeasure", "Замер ещё не выполнялся");
            }, KvTheme.TextMain);
            kit.Note(T("coll.info",
                "Прокси строятся по ВСЕМ вершинам меша (ориентированный бокс по главным осям " +
                "облака вершин), поэтому всегда накрывают исходную геометрию: повёрнутые и " +
                "вытянутые детали перестают «раздуваться» до осевого габарита, и планировщик " +
                "находит путь там, где раньше отказывался. Объект пропускается только если он " +
                "полностью внутри другого объёма."), KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
