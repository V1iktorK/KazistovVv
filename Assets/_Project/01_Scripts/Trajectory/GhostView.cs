using System.Collections.Generic;
using TrajectoryCore;
using UnityEngine;

/// <summary>
/// Визуализация фантомных траекторий (E5 плана): полилинии TCP для каждого кандидата
/// (первый — зелёный, остальные — синие), маркер самого узкого места и «бегунок»,
/// проигрывающий выбранный вариант. Лёгкая реализация без копий моделей.
/// </summary>
public class GhostView : MonoBehaviour
{
    public float runnerSpeed = 0.35f;   // доля пути в секунду
    public int maxSamplesPerLine = 90;

    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private readonly List<PlannedTrajectory> candidates = new List<PlannedTrajectory>();
    private GameObject runner;
    private GameObject worstMarker;
    private PoseValidator validator;
    private float runnerPhase;

    public int CandidateCount => candidates.Count;

    private void Awake()
    {
        runner = CreateSphere("GhostRunner", 0.03f, new Color(0.2f, 1f, 0.4f));
        worstMarker = CreateSphere("GhostWorstClearance", 0.04f, new Color(1f, 0.2f, 0.1f));
        Clear();
    }

    private static GameObject CreateSphere(string name, float size, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = name;
        go.hideFlags = HideFlags.HideInHierarchy;   // служебная визуализация — не в иерархии
        Collider c = go.GetComponent<Collider>();
        if (c != null) Destroy(c);
        go.transform.localScale = Vector3.one * size;
        Renderer r = go.GetComponent<Renderer>();
        // Материал призрака — под ТЕКУЩИЙ конвейер проекта (в §29 их два: URP и HDRP).
        Material m = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit("Материал_призрака", color);
        if (m != null)
        {
            if (m.HasProperty("_EmissiveColor")) { m.SetColor("_EmissiveColor", color * 2f); m.EnableKeyword("_EMISSION"); }
            r.material = m;
        }
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        go.SetActive(false);
        return go;
    }

    private LineRenderer EnsureLine(int index)
    {
        while (lines.Count <= index)
        {
            GameObject go = new GameObject("GhostTrajectory_" + lines.Count);
            go.hideFlags = HideFlags.HideInHierarchy;
            go.transform.SetParent(transform, false);
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.widthMultiplier = 0.012f;
            Material ghostMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLine(
                "Материал_призрака", Color.white);
            if (ghostMaterial != null) lr.material = ghostMaterial;
            lines.Add(lr);
        }
        return lines[index];
    }

    /// <summary>Показать кандидатов (первый считается оптимальным).</summary>
    public void Show(List<PlannedTrajectory> list, PoseValidator v)
    {
        Clear();
        if (list == null || v == null || !v.Ready) return;
        validator = v;
        candidates.AddRange(list);

        for (int i = 0; i < candidates.Count; i++)
        {
            LineRenderer lr = EnsureLine(i);
            PlannedTrajectory t = candidates[i];
            int count = t.Path.Length;
            int stride = Mathf.Max(1, count / maxSamplesPerLine);
            var pts = new List<Vector3>();
            for (int s = 0; s < count; s += stride)
            {
                Vector3 p = v.TcpAt(t.Path[s]);
                pts.Add(p);
            }
            if (pts.Count < 2) continue;

            lr.positionCount = pts.Count;
            lr.SetPositions(pts.ToArray());
            Color col = i == 0 ? new Color(0.2f, 1f, 0.4f, 0.95f) : new Color(0.35f, 0.6f, 1f, 0.8f);
            lr.startColor = col;
            lr.endColor = new Color(col.r, col.g, col.b, 0.25f);
            lr.enabled = true;

            if (i == 0)
            {
                goalPoint = v.TcpAt(t.Path[count - 1]);
            }
        }

        if (candidates.Count > 0)
        {
            runner.SetActive(true);
            worstMarker.transform.position = goalPoint + Vector3.up * 0.03f;
            worstMarker.SetActive(true);
        }
    }

    private Vector3 goalPoint;

    public void Clear()
    {
        candidates.Clear();
        foreach (LineRenderer lr in lines) lr.enabled = false;
        if (runner != null) runner.SetActive(false);
        if (worstMarker != null) worstMarker.SetActive(false);
    }

    private void Update()
    {
        if (candidates.Count == 0 || validator == null) return;
        PlannedTrajectory t = candidates[0];
        int n = t.Path.Length;
        if (n == 0) return;

        runnerPhase += Time.deltaTime * runnerSpeed;
        if (runnerPhase > 1f) runnerPhase -= 1f;
        int idx = Mathf.Clamp(Mathf.RoundToInt(runnerPhase * (n - 1)), 0, n - 1);
        Vector3 p = validator.TcpAt(t.Path[idx]);
        runner.transform.position = p;
    }
}
