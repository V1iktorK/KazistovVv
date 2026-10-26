using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>
    /// ЭТАП 17 ТЗ: ВИЗУАЛИЗАЦИЯ СИЛ И МОМЕНТОВ.
    ///
    /// Момент каждого сустава считается ТОЙ ЖЕ моделью, что и калькулятор нагрузки
    /// (этап 10) — вес звеньев и груза на фактических плечах, спроецированный на ось
    /// сустава, — и рисуется стрелкой у оси: длина пропорциональна моменту, цвет — доля
    /// от номинала (зелёный → жёлтый → красный). На инструменте рисуется вектор силы
    /// (вес груза и захвата). Всё служебное (`HideInHierarchy`, без коллайдеров), поэтому
    /// визуализация не попадает ни в иерархию, ни в мир столкновений.
    /// </summary>
    public class KvForceVisualizer
    {
        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private Transform root;
        private readonly List<LineRenderer> arrows = new List<LineRenderer>();
        private bool enabled;
        private float timer;
        private float payloadKg = 1.0f;
        private float lengthPerNm = 0.004f;      // длина стрелки на 1 Н·м
        private float minLength = 0.05f;
        private float maxLength = 0.45f;

        private float[] lastTorque = new float[0];
        private float[] lastLoad = new float[0];
        private Vector3 lastToolForce;

        public bool Enabled { get { return enabled; } }
        public float PayloadKg { get { return payloadKg; } set { payloadKg = Mathf.Clamp(value, 0f, 50f); } }
        public IReadOnlyList<float> Torques { get { return lastTorque; } }
        public IReadOnlyList<float> Loads { get { return lastLoad; } }
        public Vector3 ToolForce { get { return lastToolForce; } }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub)
        {
            flow = controller;
            features = hub;
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (!enabled)
            {
                foreach (LineRenderer lr in arrows) if (lr != null) lr.positionCount = 0;
                Report("векторы сил выключены");
                return;
            }
            Report("векторы сил включены: стрелки моментов на осях и сила на инструменте" +
                   " (груз " + payloadKg.ToString("0.0") + " кг)" +
                   (KvGraphics.Available ? "" : " · показ выключен: " + KvGraphics.Reason));
        }

        public void Toggle()
        {
            SetEnabled(!enabled);
        }

        public void Tick(float deltaTime)
        {
            if (!enabled) return;
            timer -= deltaTime;
            if (timer > 0f) return;
            timer = 0.12f;

            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            PoseValidator v = flow.Validator;
            double[] q = v.CopyCurrent();

            float[] torque;
            float[] load;
            Vector3 toolForce;
            KvPayloadCalculator payload = KvStageHub3.Current != null ? KvStageHub3.Current.Payload : null;
            if (payload == null || !payload.JointTorques(q, payloadKg, out torque, out load, out toolForce))
                return;

            lastTorque = torque;
            lastLoad = load;
            lastToolForce = toolForce;

            // Стрелки — это показ; без графики (пакетный режим) считаем, но не рисуем.
            if (!KvGraphics.Available) return;

            EnsureRoot();
            EnsureArrows(v.Dof + 1);

            for (int j = 0; j < v.Dof && j < arrows.Count; j++)
            {
                Vector3 pivot = v.PivotAt(j, q);
                Vector3 dir;
                float magnitude;
                if (v.IsPrismatic(j))
                {
                    // Призматическая ось: усилие вдоль оси (Н) — рисуем вдоль оси призмы.
                    dir = v.AxisWorld(j, q).normalized;
                    magnitude = Mathf.Abs(torque[j]);
                }
                else
                {
                    Vector3 axis = v.AxisWorld(j, q).normalized;
                    float sign = torque[j] >= 0f ? 1f : -1f;
                    // Момент рисуем «по правилу винта»: вектор момента направлен вдоль оси.
                    dir = axis * sign;
                    magnitude = Mathf.Abs(torque[j]);
                }

                float len = Mathf.Clamp(magnitude * lengthPerNm, minLength, maxLength);
                LineRenderer lr = arrows[j];
                lr.positionCount = 2;
                lr.SetPosition(0, pivot);
                lr.SetPosition(1, pivot + dir * len);

                float k = Mathf.Clamp01(load[j]);
                Color c = k < 0.5f
                    ? Color.Lerp(new Color(0.3f, 1f, 0.4f), new Color(1f, 0.9f, 0.2f), k * 2f)
                    : Color.Lerp(new Color(1f, 0.9f, 0.2f), new Color(1f, 0.25f, 0.2f), (k - 0.5f) * 2f);
                lr.startColor = c;
                lr.endColor = new Color(c.r, c.g, c.b, 0.35f);
            }

            // Сила на инструменте: вектор веса груза и захвата в TCP.
            Vector3 tcp = v.TcpAt(q);
            float forceLen = Mathf.Clamp(toolForce.magnitude * 0.0025f, 0.06f, 0.4f);
            LineRenderer tool = arrows[arrows.Count - 1];
            tool.positionCount = 2;
            tool.SetPosition(0, tcp);
            tool.SetPosition(1, tcp + Vector3.down * forceLen);
            tool.startColor = new Color(1f, 0.55f, 0.15f, 0.95f);
            tool.endColor = new Color(1f, 0.55f, 0.15f, 0.3f);
        }

        private void EnsureRoot()
        {
            if (root != null) return;
            GameObject go = new GameObject("KvForceVectors");
            go.hideFlags = HideFlags.HideInHierarchy;
            root = go.transform;
        }

        private void EnsureArrows(int count)
        {
            while (arrows.Count < count)
            {
                GameObject go = new GameObject("Arrow" + arrows.Count);
                go.hideFlags = HideFlags.HideInHierarchy;
                go.transform.SetParent(root, false);
                LineRenderer lr = go.AddComponent<LineRenderer>();
                lr.useWorldSpace = true;
                lr.numCapVertices = 4;
                lr.widthMultiplier = 0.012f;
                Material arrowMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                    "Материал_стрелки_сил", Color.white);
                if (arrowMaterial != null) lr.material = arrowMaterial;
                lr.positionCount = 0;
                arrows.Add(lr);
            }
        }

        /// <summary>Строка состояния для вкладки (моменты по осям).</summary>
        public string StatusLine()
        {
            if (lastTorque == null || lastTorque.Length == 0)
                return enabled
                    ? "моменты считаются, показ недоступен: " + KvGraphics.Reason
                    : "нет данных";
            string s = "";
            for (int i = 0; i < lastTorque.Length; i++)
            {
                if (i > 0) s += " · ";
                s += "J" + (i + 1) + " " + lastTorque[i].ToString("0.0") +
                     (lastLoad != null && i < lastLoad.Length
                         ? " (" + (lastLoad[i] * 100f).ToString("0") + " %)"
                         : "");
            }
            return s + " · сила на инструменте " + lastToolForce.magnitude.ToString("0.0") + " Н";
        }

        public void Dispose()
        {
            foreach (LineRenderer lr in arrows) if (lr != null) UnityEngine.Object.Destroy(lr.gameObject);
            arrows.Clear();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[Forces] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>
    /// ЭТАП 18 ТЗ: ТЕПЛОВАЯ КАРТА ВРЕМЕНИ ДОСТИЖЕНИЯ.
    ///
    /// Отличие от обычной тепловой карты достижимости: цвет точки показывает НЕ абстрактную
    /// «стоимость», а ВРЕМЯ В СЕКУНДАХ — сколько займёт добраться до неё из текущей позы при
    /// текущих лимитах скорости и ускорения. Для каждой точки выборки решается IK, затем
    /// путь «текущая поза → цель» перепараметризуется по лимитам (`KvTrajMath.Retime`,
    /// тот же метод, что у время-оптимальных траекторий), и берётся его полное время.
    /// Точки без решения IK не рисуются (их и достичь нельзя).
    ///
    /// Расчёт идёт ПОРЦИЯМИ (несколько точек за кадр), поэтому интерфейс не замирает;
    /// пересчёт запускается при смене робота, лимитов или по кнопке.
    /// </summary>
    public class KvTimeHeatmap
    {
        public event Action<string> Message;

        private TrajectoryFlowController flow;
        private FeatureHub features;
        private Transform root;
        private MeshFilter filter;
        private MeshRenderer renderer;
        private Mesh mesh;
        private Material material;

        private bool enabled;
        private bool building;
        private int cursor;
        private readonly List<Vector3> points = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> indices = new List<int>();
        private float scaleMax = 6f;              // верхняя граница шкалы, с
        private int samplesPerFrame = 12;
        private float[] times = new float[0];
        private Vector3[] samplePoints = new Vector3[0];
        private int cellRing = 7;
        private int cellSphere = 5;

        public bool Enabled { get { return enabled; } }
        public bool Building { get { return building; } }
        public float ScaleMax { get { return scaleMax; } set { scaleMax = Mathf.Clamp(value, 0.5f, 60f); } }
        public int PointCount { get { return times != null ? times.Length : 0; } }

        /// <summary>
        /// Время достижимости цели варианта траектории: берётся ближайший посчитанный узел карты
        /// к конечной точке «колбаски» варианта. −1 — карта не рассчитана или цель далеко от узлов.
        /// </summary>
        public float TimeAt(TrajectoryCandidate candidate)
        {
            if (candidate == null || times == null || times.Length == 0 || samplePoints.Length == 0)
                return -1f;
            Vector3 goal = Vector3.zero;
            if (candidate.tube != null && candidate.tube.Length > 0)
                goal = candidate.tube[candidate.tube.Length - 1];
            else if (flow != null && flow.State != null && flow.State.hasPoint)
                goal = flow.State.point;
            else return -1f;

            float best = float.MaxValue;
            int index = -1;
            for (int i = 0; i < samplePoints.Length && i < times.Length; i++)
            {
                float distance = (samplePoints[i] - goal).sqrMagnitude;
                if (distance >= best) continue;
                best = distance;
                index = i;
            }
            if (index < 0 || best > 0.25f * 0.25f) return -1f;   // дальше 25 см — узел не «та» точка
            return times[index];
        }

        public void Bind(TrajectoryFlowController controller, FeatureHub hub)
        {
            flow = controller;
            features = hub;
        }

        public void SetEnabled(bool value)
        {
            enabled = value;
            if (root != null) root.gameObject.SetActive(value);
            if (value)
            {
                RequestRebuild(true);
                Report("тепловая карта времени включена: цвет — секунды до точки (шкала до " +
                       scaleMax.ToString("0.0") + " с)");
            }
            else Report("тепловая карта времени выключена");
        }

        public void Toggle()
        {
            SetEnabled(!enabled);
        }

        public void RequestRebuild(bool immediate = false)
        {
            if (!Enabled) return;
            building = true;
            cursor = 0;
            BuildSamples();
            if (immediate) { /* расчёт порциями всё равно идёт в Tick */ }
            Report("тепловая карта времени: пересчёт начат (" + samplePoints.Length + " точек)");
        }

        private void BuildSamples()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            PoseValidator v = flow.Validator;
            Vector3 basePos = v.BasePosition;
            bool scara = v.Dof <= 3;

            if (scara)
            {
                var pts = new List<Vector3>();
                for (int ri = 1; ri <= cellRing; ri++)
                {
                    float r = 0.12f + (ri - 1) * (0.55f / Mathf.Max(1, cellRing - 1));
                    if (r < 0.08f) continue;
                    int steps = Mathf.Max(12, ri * 8);
                    for (int a = 0; a < steps; a++)
                    {
                        float ang = a / (float)steps * Mathf.PI * 2f;
                        for (int z = 0; z < 3; z++)
                        {
                            float y = TrajectoryCore.StandBuilder.TopHeight + 0.05f + z * 0.11f;
                            pts.Add(new Vector3(basePos.x + Mathf.Cos(ang) * r, y,
                                basePos.z + Mathf.Sin(ang) * r));
                        }
                    }
                }
                samplePoints = pts.ToArray();
            }
            else
            {
                var pts = new List<Vector3>();
                for (int ri = 1; ri <= cellSphere; ri++)
                {
                    float r = 0.16f + (ri - 1) * (0.5f / Mathf.Max(1, cellSphere - 1));
                    int steps = Mathf.Max(10, ri * 7);
                    for (int a = 0; a < steps; a++)
                    {
                        float ang = a / (float)steps * Mathf.PI * 2f;
                        for (int h = 0; h < 3; h++)
                        {
                            float y = TrajectoryCore.StandBuilder.TopHeight + 0.06f + h * 0.13f;
                            Vector3 p = new Vector3(basePos.x + Mathf.Cos(ang) * r, y,
                                basePos.z + Mathf.Sin(ang) * r);
                            pts.Add(p);
                        }
                    }
                }
                samplePoints = pts.ToArray();
            }
            times = new float[samplePoints.Length];
            for (int i = 0; i < times.Length; i++) times[i] = -1f;
        }

        public void Tick(float deltaTime)
        {
            if (!enabled || !building) return;
            if (flow == null || flow.Validator == null || !flow.Validator.Ready) return;

            PoseValidator v = flow.Validator;
            double[] start = v.CopyCurrent();
            KvMotionLimits limits = KvStageHub2.Current != null && KvStageHub2.Current.Limits != null
                ? KvStageHub2.Current.Limits
                : new KvMotionLimits();

            int done = 0;
            while (cursor < samplePoints.Length && done < samplesPerFrame)
            {
                Vector3 point = samplePoints[cursor];
                double[] goal = null;
                if (v.SolveIk(point, start, out goal) && goal != null && v.WithinLimits(goal) &&
                    v.LimitMargin(goal) >= 3f)
                {
                    // Прямой путь по суставам + перепараметризация по лимитам = время в секундах.
                    TrajectoryCore.PlannedTrajectory plan = KvPlanKit.MakeJointPlan(v,
                        features != null ? features.World : null, start, goal, "время", 0.1f, 24);
                    if (plan != null)
                    {
                        TrajectoryCore.PlannedTrajectory retimed = KvTrajMath.Retime(v, plan, limits,
                            1f, 1f, null, false);
                        double time = retimed != null ? retimed.Time : (plan.Time > 0 ? plan.Time : 0.0);
                        times[cursor] = (float)time;
                    }
                }
                cursor++;
                done++;
            }

            if (cursor >= samplePoints.Length)
            {
                building = false;
                int reachable = 0;
                float worst = 0f;
                for (int i = 0; i < times.Length; i++)
                {
                    if (times[i] < 0f) continue;
                    reachable++;
                    worst = Mathf.Max(worst, times[i]);
                }
                if (worst > scaleMax) scaleMax = Mathf.Ceil(worst * 1.1f);
                // Раскрашенная поверхность — это показ: без графики время посчитано,
                // но меш не строится (честно сообщаем об этом в журнале).
                if (KvGraphics.Available) BuildMesh();
                Report("тепловая карта времени построена: точек " + times.Length +
                       " · достижимо " + reachable + " · максимальное время " +
                       worst.ToString("0.00") + " с" +
                       (worst > scaleMax ? " (шкала увеличена)" : "") +
                       (KvGraphics.Available ? "" : " · показ выключен (нет графики)"));
            }
        }

        private void EnsureObject()
        {
            PoseValidator v = flow != null ? flow.Validator : null;
            bool scara = v != null && v.Ready && v.Dof <= 3;

            if (root == null)
            {
                GameObject go = new GameObject("KvTimeHeatmap");
                go.hideFlags = HideFlags.HideInHierarchy;
                root = go.transform;
                filter = go.AddComponent<MeshFilter>();
                renderer = go.AddComponent<MeshRenderer>();
                // Unlit-материал под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
                material = KvRenderPipelineShaders.CreateUnlit("Материал_теплокарты_времени", Color.white);
                if (material != null)
                {
                    material.hideFlags = HideFlags.HideAndDontSave;
                    renderer.material = material;
                }
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            root.gameObject.SetActive(enabled);
            if (scara && mesh != null) { mesh.Clear(); }
        }

        private void BuildMesh()
        {
            EnsureObject();
            if (filter == null || flow == null || flow.Validator == null) return;

            points.Clear();
            colors.Clear();
            indices.Clear();
            var valid = new List<int>();

            for (int i = 0; i < samplePoints.Length; i++)
            {
                bool ok = times[i] >= 0f;
                points.Add(samplePoints[i]);
                colors.Add(ColorFor(ok ? times[i] : -1f));
                if (ok) valid.Add(i);
            }

            // Треугольники между соседними точками одного кольца/уровня (простая сетка).
            for (int i = 0; i + 1 < points.Count; i++)
            {
                // соединение по «соседям» — треугольники между тройками подряд идущих точек
                if (i + 2 >= points.Count) break;
                if (times[i] < 0f || times[i + 1] < 0f) continue;
                indices.Add(i); indices.Add(i + 1); indices.Add(i + 2);
            }

            if (mesh == null) mesh = new Mesh { name = "KvTimeHeatmap" };
            mesh.Clear();
            mesh.SetVertices(points);
            mesh.SetColors(colors);
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
        }

        /// <summary>Цвет по времени: зелёный — быстро, красный — долго, серый — недостижимо.</summary>
        private Color ColorFor(float seconds)
        {
            if (seconds < 0f) return new Color(0.35f, 0.35f, 0.4f, 0.25f);
            float k = Mathf.Clamp01(seconds / Mathf.Max(0.5f, scaleMax));
            Color fast = new Color(0.2f, 0.95f, 0.45f, 0.55f);
            Color mid = new Color(1f, 0.85f, 0.25f, 0.55f);
            Color slow = new Color(1f, 0.25f, 0.2f, 0.55f);
            return k < 0.5f ? Color.Lerp(fast, mid, k * 2f) : Color.Lerp(mid, slow, (k - 0.5f) * 2f);
        }

        /// <summary>Строка состояния для вкладки.</summary>
        public string Status()
        {
            if (!enabled) return "выключена";
            if (building) return "пересчёт: " + cursor + " из " + samplePoints.Length + " точек";
            int reachable = 0;
            float worst = 0f, best = float.MaxValue;
            for (int i = 0; i < times.Length; i++)
            {
                if (times[i] < 0f) continue;
                reachable++;
                worst = Mathf.Max(worst, times[i]);
                best = Mathf.Min(best, times[i]);
            }
            if (reachable == 0) return "точек с решением IK нет";
            return "точек " + times.Length + " · достижимо " + reachable +
                   " · время от " + best.ToString("0.00") + " до " + worst.ToString("0.00") + " с";
        }

        public void Dispose()
        {
            if (mesh != null) UnityEngine.Object.Destroy(mesh);
            if (material != null) UnityEngine.Object.Destroy(material);
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
        }

        private void Report(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            Debug.Log("[TimeHeatmap] " + text);
            if (Message != null) Message(text);
        }
    }

    /// <summary>ВКЛАДКА «СИЛЫ И ВРЕМЯ» (ЭТАПЫ 17–18 ТЗ).</summary>
    public class KvForceHeatTab : IKvWorkbenchTab
    {
        private readonly KvForceVisualizer forces;
        private readonly KvTimeHeatmap heatmap;

        public KvForceHeatTab(KvForceVisualizer forceVisualizer, KvTimeHeatmap timeHeatmap)
        {
            forces = forceVisualizer;
            heatmap = timeHeatmap;
        }

        public string Key { get { return "forces"; } }
        public string Title { get { return KvLocExtra3.T("force.title", "Силы и моменты"); } }

        private static string T(string key, string fallback)
        {
            return KvLocExtra3.T(key, fallback);
        }

        public void Build(KvTabKit kit)
        {
            if (kit == null) return;

            kit.Section(T("force.title", "Силы и моменты"));
            kit.Toggle(T("force.show", "Показывать моменты на осях"),
                forces != null && forces.Enabled, delegate (bool v)
                {
                    if (forces != null) forces.SetEnabled(v);
                });
            kit.Slider(T("energy.payload", "Масса груза, кг"), 0f, 20f,
                forces != null ? forces.PayloadKg : 1f, "0.0", delegate (float v)
                {
                    if (forces != null) forces.PayloadKg = v;
                });
            kit.Info(delegate { return forces != null ? forces.StatusLine() : "—"; }, KvTheme.Accent);

            kit.Divider();
            kit.Section(T("time.title", "Тепловая карта времени"));
            kit.Toggle(T("time.show", "Показывать время достижения (секунды)"),
                heatmap != null && heatmap.Enabled, delegate (bool v)
                {
                    if (heatmap != null) heatmap.SetEnabled(v);
                });
            kit.Slider(T("time.scale", "Верхняя граница шкалы, с"), 0.5f, 30f,
                heatmap != null ? heatmap.ScaleMax : 6f, "0.0", delegate (float v)
                {
                    if (heatmap != null) heatmap.ScaleMax = v;
                });
            kit.Buttons(new[] { T("coll.rebuild", "Пересчитать") },
                new Action[] { delegate { if (heatmap != null) heatmap.RequestRebuild(true); } });
            kit.Info(delegate { return heatmap != null ? heatmap.Status() : "—"; }, KvTheme.TextMain);

            kit.Divider();
            kit.Note(T("force.info",
                "Момент каждого сустава считается по той же модели, что в калькуляторе нагрузки, и " +
                "рисуется вектором у оси сустава; длина пропорциональна моменту, цвет — доля от номинала."),
                KvTheme.TextDim);
            kit.Note(T("time.info",
                "Цвет точки показывает, СКОЛЬКО СЕКУНД нужно роботу, чтобы добраться до неё (по лимитам " +
                "скорости и ускорения), а не абстрактную «стоимость»: зелёный — быстро, красный — долго."),
                KvTheme.TextDim);
        }

        public void Tick() { }
        public void Refresh() { }
    }
}
