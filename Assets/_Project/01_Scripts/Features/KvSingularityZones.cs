using System;
using System.Collections.Generic;
using UnityEngine;
using KazistovVvUI;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Тип сингулярности (для подписи и подсветки).</summary>
    public enum KvSingularityKind
    {
        None = 0,
        Wrist,
        Elbow,
        Shoulder,
        Extend,
        Fold
    }

    /// <summary>
    /// ЭТАП 4 ТЗ — ВИЗУАЛИЗАЦИЯ СИНГУЛЯРНОСТЕЙ.
    ///
    /// ЧТО СЧИТАЕТСЯ (по фактической геометрии проекта, без правок кинематики):
    ///   * ЗАПЯСТЬЕ (`wrist`) — ось 5 близка к нулю (|q5| &lt; 8°), оси 4 и 6 становятся
    ///     коллинеарны: робот теряет возможность поворота вокруг оси инструмента. Тот же
    ///     критерий использует оракул достижимости (`ReachabilityOracle`), поэтому вердикты
    ///     интерфейса не расходятся.
    ///   * ЛОКОТЬ (`elbow`) — рука почти вытянута: расстояние TCP от базы / полная длина
    ///     звеньев → 1. Запас считается по фактическим пивотам суставов (`PoseValidator.PivotAt`)
    ///     и длине звеньев, снятой один раз при привязке.
    ///   * ПЛЕЧО (`shoulder`) — TCP почти над осью базы (горизонтальное расстояние до
    ///     вертикали первой оси мало): рука «складывается» через себя.
    ///   * SCARA — СВОИ сингулярности (ТЗ): ПОЛНОЕ ВЫТЯГИВАНИЕ (`extend`) и ПОЛНОЕ
    ///     СКЛАДЫВАНИЕ (`fold`) в горизонтальной плоскости 2R-руки.
    ///   * Дополнительно считается ЧИСЛО ОБУСЛОВЛЕННОСТИ через сингулярные числа матрицы
    ///     Якоби (`KinematicsJacobian.Compute` + `SigmaMin`): `mobility = σmin / σmin_эталон`.
    ///     Это объективная мера «потери манёвренности» и она же попадает в статус.
    ///
    /// КАК ПОКАЗЫВАЕТСЯ (ТЗ: «зоны вокруг СУСТАВОВ, а не всей сцены», «красный — опасно,
    /// жёлтый — близко»): вокруг пивотов вовлечённых суставов включаются полупрозрачные
    /// сферы-зоны (у плеча — ещё и кольцо у основания), жёлтые при подходе и красные в самой
    /// сингулярности. Геометрия создаётся ОДИН РАЗ (пул), материалы — три общих (нет утечек),
    /// объекты скрыты из иерархии (`HideFlags.HideInHierarchy`) и не попадают в мир столкновений
    /// (правило проекта: `CollisionWorld` игнорирует `HideInHierarchy`).
    ///
    /// ПРОИЗВОДИТЕЛЬНОСТЬ: пересчёт — раз в `interval` (по умолчанию 0.1 с), в кадре делается
    /// только чтение уже посчитанного; при выключенной визуализации не делается вообще ничего.
    /// </summary>
    public class KvSingularityVisualizer
    {
        /// <summary>Сообщение (в журнал/статус).</summary>
        public event Action<string> Message;

        // ------------------------------------------------------------------ параметры
        public bool Enabled;

        [Tooltip("Интервал пересчёта, с.")]
        public float interval = 0.1f;

        [Tooltip("Порог метрики запястья «близко», град.")]
        public float wristWarnDeg = 14f;
        [Tooltip("Порог метрики запястья «опасно», град.")]
        public float wristDangerDeg = 6f;

        [Tooltip("Относительное вытягивание руки «близко» (доля полной длины).")]
        public float extendWarn = 0.94f;
        [Tooltip("Относительное вытягивание руки «опасно».")]
        public float extendDanger = 0.985f;

        [Tooltip("Радиус у оси базы «близко» (доля полной длины) — сингулярность плеча.")]
        public float shoulderWarn = 0.28f;
        [Tooltip("Радиус у оси базы «опасно».")]
        public float shoulderDanger = 0.14f;

        [Tooltip("Радиус сфер-зон у суставов, м (масштабируется по состоянию).")]
        public float zoneRadius = 0.075f;

        public bool logEvents = true;

        // ------------------------------------------------------------------ состояние
        private TrajectoryFlowController flow;
        private Transform root;
        private bool built;
        private float timer;

        private double[][] jac;
        private readonly float[] limitMargins = new float[8];

        // Пул зон: сферы у суставов + кольцо у основания + маркер вытянутой руки.
        private readonly List<Transform> zones = new List<Transform>();
        private readonly List<Renderer> zoneRenderers = new List<Renderer>();
        private Transform baseRing;
        private Renderer baseRingRenderer;
        private Material matSafe, matWarn, matDanger;

        // Длины звеньев и опорные величины (снимаются один раз на робота).
        private float chainLength = 1f;
        private float referenceSigma = 1f;
        private float foldRatioRef;
        private bool isScara;

        // Результат последнего пересчёта (доступен интерфейсу).
        public KvSingularityKind Kind { get; private set; }
        public int Joint { get; private set; }              // вовлечённый сустав (-1 — нет)
        public float Value { get; private set; }            // метрика (град / доля / σ)
        public float Mobility { get; private set; }         // σmin / эталон (1 — отлично)
        public string Reason { get; private set; }
        public bool Danger { get; private set; }
        public bool Warn { get; private set; }
        public int VisibleZones { get; private set; }

        private static readonly string[] KindKeys =
        {
            "", "singularity.wrist", "singularity.elbow", "singularity.shoulder",
            "singularity.extend", "singularity.fold"
        };

        public KvSingularityVisualizer()
        {
            Reason = "";
        }

        public void Bind(TrajectoryFlowController controller)
        {
            flow = controller;
            if (flow != null && flow.Validator != null && flow.Validator.Ready) Rebind(flow.Robot);
        }

        /// <summary>Сменить робота: пересчитать длины звеньев и эталон σmin.</summary>
        public void Rebind(RobotController robot)
        {
            if (robot == null || flow == null || flow.Validator == null || !flow.Validator.Ready) return;
            if (boundRobot == robot) return;
            boundRobot = robot;

            PoseValidator v = flow.Validator;
            isScara = v.Dof == 3;
            double[] q = new double[v.Dof];
            v.CopyCurrentInto(q);

            // Длина «цепочки» — сумма расстояний между соседними пивотами + вылет до TCP.
            // Для жёстких звеньев это ПОСТОЯННАЯ величина, поэтому снимается один раз.
            float len = 0f;
            Vector3 prev = v.PivotAt(0, q);
            for (int i = 1; i < v.Dof; i++)
            {
                Vector3 p = v.PivotAt(i, q);
                len += Vector3.Distance(prev, p);
                prev = p;
            }
            Vector3 tcp = v.TcpAt(q);
            len += Vector3.Distance(prev, tcp);
            chainLength = Mathf.Max(0.05f, len);

            // Для SCARA отдельно помним «полное складывание»: |L1 − L2| / полная длина.
            // Длины звеньев снимаются по фактическим пивотам (жёсткие звенья — постоянны).
            if (v.Dof >= 3)
            {
                float l1 = Vector3.Distance(v.PivotAt(0, q), v.PivotAt(1, q));
                float l2 = Vector3.Distance(v.PivotAt(1, q), v.PivotAt(2, q));
                foldRatioRef = Mathf.Abs(l1 - l2) / chainLength;
            }
            else foldRatioRef = 0f;

            // Эталон σmin — «удобная» поза: берём текущую и слегка отведённую конфигурацию,
            // из них максимум. По нему нормируется манёвренность (1 — как в удобной позе).
            referenceSigma = Mathf.Max(0.0001f, SigmaAt(v, q));
            for (int axis = 1; axis < v.Dof; axis++)
            {
                double[] probe = (double[])q.Clone();
                probe[axis] += v.IsPrismatic(axis) ? 0.05 : 18.0;
                probe = v.ContinueFrom(q, probe);
                if (!v.WithinLimits(probe)) continue;
                referenceSigma = Mathf.Max(referenceSigma, SigmaAt(v, probe));
            }

            Debug.Log("[Singularity] робот «" + robot.robotName + "»: звеньев " + v.Dof +
                      " · длина цепочки " + chainLength.ToString("0.000") + " м · эталон σmin " +
                      referenceSigma.ToString("0.0000") + (isScara ? " · SCARA (вытягивание/складывание)" : ""));
        }

        private RobotController boundRobot;

        private float SigmaAt(PoseValidator v, double[] q)
        {
            if (jac == null || jac.Length < v.Dof) jac = KinematicsJacobian.Allocate(v.Dof);
            Vector3 tcp;
            v.Jacobian(q, jac, out tcp);
            return (float)Math.Max(0.0, KinematicsJacobian.SigmaMin(jac, v.Dof));
        }

        // ================================================================== кадровое обслуживание

        public void Tick(float dt)
        {
            if (!built) return;
            if (!Enabled)
            {
                if (VisibleZones > 0) HideAll();
                return;
            }

            timer -= dt;
            if (timer > 0f) return;
            timer = Mathf.Max(0.02f, interval);
            Evaluate();
        }

        /// <summary>Пересчёт метрики и обновление зон (можно звать вручную — диагностика).</summary>
        public void Evaluate()
        {
            if (flow == null || flow.Validator == null || !flow.Validator.Ready)
            {
                HideAll();
                return;
            }
            if (flow.Robot != null && flow.Robot != boundRobot) Rebind(flow.Robot);

            PoseValidator v = flow.Validator;
            double[] q = new double[v.Dof];
            v.CopyCurrentInto(q);

            Vector3 tcp = v.TcpAt(q);
            Vector3 basePos = v.BasePosition;
            KinematicsJacobian.Compute(v, q, jac != null && jac.Length >= v.Dof
                ? jac : (jac = KinematicsJacobian.Allocate(v.Dof)), out tcp);

            float sigma = (float)KinematicsJacobian.SigmaMin(jac, v.Dof);
            Mobility = Mathf.Clamp01(sigma / referenceSigma);

            Kind = KvSingularityKind.None;
            Joint = -1;
            Value = 0f;
            Reason = "";
            Danger = false;
            Warn = false;

            if (isScara)
                EvaluateScara(v, q, tcp, basePos);
            else
                EvaluateRobot(v, q, tcp, basePos);

            UpdateZones(v, q, tcp, basePos);
            Report();
        }

        /// <summary>Робот с вращательными осями: запястье, локоть (вытягивание), плечо.</summary>
        private void EvaluateRobot(PoseValidator v, double[] q, Vector3 tcp, Vector3 basePos)
        {
            // --- 1) запястье: ось 5 около нуля
            if (v.Dof >= 5)
            {
                float q5 = Mathf.Abs(Mathf.DeltaAngle(0f, (float)q[4]));
                Value = q5;
                if (q5 < wristDangerDeg)
                {
                    Kind = KvSingularityKind.Wrist; Joint = 4; Danger = true;
                    Reason = KvLoc.T("singularity.wrist", "запястье") + " · |q5| = " +
                             q5.ToString("0.0") + "° (оси 4 и 6 совпали)";
                    return;
                }
                if (q5 < wristWarnDeg)
                {
                    Kind = KvSingularityKind.Wrist; Joint = 4; Warn = true;
                    Reason = KvLoc.T("singularity.wrist", "запястье") + " · |q5| = " +
                             q5.ToString("0.0") + "°";
                    return;
                }
            }

            // --- 2) локоть: рука почти вытянута
            float reach = Vector3.Distance(tcp, basePos);
            float ratio = reach / chainLength;
            Value = ratio;
            if (ratio > extendDanger)
            {
                Kind = KvSingularityKind.Elbow; Joint = 2; Danger = true;
                Reason = KvLoc.T("singularity.elbow", "локоть") + " · " +
                         KvLoc.T("singularity.extend", "полное вытягивание") + " (" +
                         (ratio * 100f).ToString("0.0") + " % длины)";
                return;
            }

            // --- 3) плечо: TCP почти над осью базы
            Vector3 flat = tcp - basePos;
            flat.y = 0f;
            float radial = flat.magnitude;
            float rel = radial / chainLength;
            if (rel < shoulderDanger)
            {
                Kind = KvSingularityKind.Shoulder; Joint = 0; Danger = true;
                Value = rel;
                Reason = KvLoc.T("singularity.shoulder", "плечо") + " · TCP у оси базы (" +
                         (rel * 100f).ToString("0.0") + " % длины)";
                return;
            }

            if (ratio > extendWarn)
            {
                Kind = KvSingularityKind.Elbow; Joint = 2; Warn = true;
                Value = ratio;
                Reason = KvLoc.T("singularity.elbow", "локоть") + " · " +
                         (ratio * 100f).ToString("0.0") + " % длины";
                return;
            }
            if (rel < shoulderWarn)
            {
                Kind = KvSingularityKind.Shoulder; Joint = 0; Warn = true;
                Value = rel;
                Reason = KvLoc.T("singularity.shoulder", "плечо") + " · " +
                         (rel * 100f).ToString("0.0") + " % длины";
            }
        }

        /// <summary>SCARA: свои сингулярности — полное вытягивание и полное складывание.</summary>
        private void EvaluateScara(PoseValidator v, double[] q, Vector3 tcp, Vector3 basePos)
        {
            Vector3 flat = tcp - basePos;
            flat.y = 0f;
            float radial = flat.magnitude;
            float ratio = radial / chainLength;      // вылет / полная длина руки
            Value = ratio;

            // Нижняя граница вылета 2R-руки = |L1 − L2| (снята при привязке): при полном
            // складывании направление «вытянуться» теряется так же, как при полном вытягивании.
            float foldRatio = foldRatioRef;
            if (ratio > extendDanger)
            {
                Kind = KvSingularityKind.Extend; Joint = 1; Danger = true;
                Reason = KvLoc.T("singularity.extend", "полное вытягивание") + " · " +
                         (ratio * 100f).ToString("0.0") + " % вылета";
                return;
            }
            if (foldRatio > 0f && ratio < foldRatio * 1.12f)
            {
                Kind = KvSingularityKind.Fold; Joint = 1; Danger = true;
                Reason = KvLoc.T("singularity.fold", "полное складывание") + " · " +
                         (ratio * 100f).ToString("0.0") + " % вылета";
                return;
            }
            if (ratio > extendWarn)
            {
                Kind = KvSingularityKind.Extend; Joint = 1; Warn = true;
                Reason = KvLoc.T("singularity.extend", "полное вытягивание") + " · " +
                         (ratio * 100f).ToString("0.0") + " % вылета";
            }
        }

        // ================================================================== геометрия зон

        private void EnsureBuilt()
        {
            if (built) return;
            built = true;

            GameObject rootGo = new GameObject("KvSingularityZones");
            rootGo.hideFlags = HideFlags.HideInHierarchy;
            root = rootGo.transform;

            matSafe = MakeZoneMaterial(new Color(0.20f, 0.95f, 0.35f));
            matWarn = MakeZoneMaterial(new Color(1f, 0.80f, 0.15f));
            matDanger = MakeZoneMaterial(new Color(1f, 0.16f, 0.12f));

            for (int i = 0; i < 8; i++)
            {
                GameObject sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                sphere.name = "Zone_J" + i;
                sphere.hideFlags = HideFlags.HideInHierarchy;
                Collider col = sphere.GetComponent<Collider>();
                if (col != null) { col.enabled = false; Destroy(col); }
                sphere.transform.SetParent(root, false);
                sphere.transform.localScale = Vector3.one * (zoneRadius * 2f);
                Renderer r = sphere.GetComponent<Renderer>();
                if (r != null)
                {
                    r.sharedMaterial = matSafe;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    r.receiveShadows = false;
                    r.enabled = false;
                }
                zones.Add(sphere.transform);
                zoneRenderers.Add(r);
                sphere.SetActive(false);
            }

            // Кольцо у основания — «зона плеча/складывания» в горизонтальной плоскости.
            GameObject ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Zone_BaseRing";
            ring.hideFlags = HideFlags.HideInHierarchy;
            Collider rc = ring.GetComponent<Collider>();
            if (rc != null) { rc.enabled = false; Destroy(rc); }
            ring.transform.SetParent(root, false);
            baseRing = ring.transform;
            baseRingRenderer = ring.GetComponent<Renderer>();
            if (baseRingRenderer != null)
            {
                baseRingRenderer.sharedMaterial = matSafe;
                baseRingRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                baseRingRenderer.receiveShadows = false;
                baseRingRenderer.enabled = false;
            }
            baseRing.gameObject.SetActive(false);
        }

        private static Material MakeZoneMaterial(Color color)
        {
            // Unlit-материал под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
            Material m = KvRenderPipelineShaders.CreateUnlit("Материал_сингулярной_зоны", color);
            if (m == null) return null;
            m.hideFlags = HideFlags.HideAndDontSave;
            if (m.HasProperty("_EmissiveColor"))
            {
                m.SetColor("_EmissiveColor", color * 2.2f);
                m.EnableKeyword("_EMISSION");
            }
            return m;
        }

        /// <summary>
        /// Показать зоны у ВОВЛЕЧЁННЫХ суставов: у каждого сустава цепочки до «проблемного»
        /// включается сфера, цвет — по состоянию (жёлтый = близко, красный = опасно).
        /// </summary>
        private void UpdateZones(PoseValidator v, double[] q, Vector3 tcp, Vector3 basePos)
        {
            EnsureBuilt();
            if (root == null) return;

            bool show = Danger || Warn;
            VisibleZones = 0;
            if (!show)
            {
                HideAll();
                return;
            }

            Material mat = Danger ? matDanger : matWarn;
            float scale = Danger
                ? zoneRadius * 2.6f
                : zoneRadius * 1.8f;

            // Вовлечённые суставы: сам «проблемный» + его соседи по цепочке (зона —
            // вокруг СУСТАВОВ, как требует ТЗ, а не вокруг всей сцены).
            int main = Mathf.Clamp(Joint, 0, v.Dof - 1);
            for (int i = 0; i < zones.Count; i++)
            {
                bool active = i < v.Dof && (i == main || Mathf.Abs(i - main) == 1);
                GameObject go = zones[i].gameObject;
                if (!active)
                {
                    if (go.activeSelf) go.SetActive(false);
                    if (zoneRenderers[i] != null) zoneRenderers[i].enabled = false;
                    continue;
                }

                Vector3 pivot = v.PivotAt(i, q);
                if (!go.activeSelf) go.SetActive(true);
                zones[i].position = pivot;
                zones[i].rotation = Quaternion.identity;
                float k = i == main ? scale : scale * 0.72f;
                zones[i].localScale = Vector3.one * k;
                if (zoneRenderers[i] != null)
                {
                    zoneRenderers[i].enabled = true;
                    zoneRenderers[i].sharedMaterial = mat;
                }
                VisibleZones++;
            }

            // Кольцо у основания — для сингулярности плеча и складывания SCARA.
            if (baseRing != null)
            {
                bool wantRing = Kind == KvSingularityKind.Shoulder || Kind == KvSingularityKind.Fold;
                if (wantRing)
                {
                    if (!baseRing.gameObject.activeSelf) baseRing.gameObject.SetActive(true);
                    float radius = chainLength * (Kind == KvSingularityKind.Fold ? 0.16f : shoulderWarn);
                    baseRing.position = basePos + Vector3.up * 0.005f;
                    baseRing.rotation = Quaternion.identity;
                    baseRing.localScale = new Vector3(radius * 2f, 0.002f, radius * 2f);
                    if (baseRingRenderer != null)
                    {
                        baseRingRenderer.enabled = true;
                        baseRingRenderer.sharedMaterial = mat;
                    }
                    VisibleZones++;
                }
                else if (baseRing.gameObject.activeSelf)
                {
                    baseRing.gameObject.SetActive(false);
                    if (baseRingRenderer != null) baseRingRenderer.enabled = false;
                }
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < zones.Count; i++)
            {
                if (zones[i] != null && zones[i].gameObject.activeSelf) zones[i].gameObject.SetActive(false);
                if (i < zoneRenderers.Count && zoneRenderers[i] != null) zoneRenderers[i].enabled = false;
            }
            if (baseRing != null && baseRing.gameObject.activeSelf) baseRing.gameObject.SetActive(false);
            if (baseRingRenderer != null) baseRingRenderer.enabled = false;
            VisibleZones = 0;
        }

        // ================================================================== управление

        private string lastReported = "";

        public void SetEnabled(bool value)
        {
            Enabled = value;
            if (!value)
            {
                HideAll();
                Kind = KvSingularityKind.None;
                Danger = Warn = false;
                Reason = "";
            }
            else
            {
                timer = 0f;
                Evaluate();
            }
            Say(value
                ? KvLoc.T("singularity.title", "Сингулярности") + ": " + KvLoc.T("common.on", "включено")
                : KvLoc.T("singularity.title", "Сингулярности") + ": " + KvLoc.T("common.off", "выключено"));
        }

        public bool Toggle()
        {
            SetEnabled(!Enabled);
            return Enabled;
        }

        private void Report()
        {
            string text = Status;
            if (text == lastReported) return;      // без спама: пишем только изменения
            lastReported = text;
            if (!logEvents) return;
            if (Danger) Debug.LogWarning("[Singularity] " + text);
            else Debug.Log("[Singularity] " + text);
        }

        /// <summary>Строка состояния (статус-бар, панель, отчёт).</summary>
        public string Status
        {
            get
            {
                string state = Danger
                    ? KvLoc.T("singularity.danger", "СИНГУЛЯРНОСТЬ")
                    : Warn
                        ? KvLoc.T("singularity.close", "близко к сингулярности")
                        : KvLoc.T("singularity.safe", "манёвренность в норме");
                string kind = Kind == KvSingularityKind.None
                    ? ""
                    : " · " + KvLoc.T(KindKeys[(int)Kind], Kind.ToString());
                return state + kind + (string.IsNullOrEmpty(Reason) ? "" : " · " + Reason) +
                       " · σmin " + (Mobility * 100f).ToString("0") + " %";
            }
        }

        public void Dispose()
        {
            HideAll();
            if (root != null) UnityEngine.Object.Destroy(root.gameObject);
            if (matSafe != null) UnityEngine.Object.Destroy(matSafe);
            if (matWarn != null) UnityEngine.Object.Destroy(matWarn);
            if (matDanger != null) UnityEngine.Object.Destroy(matDanger);
            root = null;
            zones.Clear();
            zoneRenderers.Clear();
            built = false;
        }

        private static void Destroy(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(o);
            else UnityEngine.Object.DestroyImmediate(o);
        }

        private void Say(string message)
        {
            if (logEvents) Debug.Log("[Singularity] " + message);
            if (Message != null) Message(message);
        }
    }
}
