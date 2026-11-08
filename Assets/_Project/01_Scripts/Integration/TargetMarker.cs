using UnityEngine;

/// <summary>
/// МАРКЕР ЦЕЛИ (пульсирующая сфера «сюда»).
///
/// ЧТО БЫЛО (исправлено 11.11.2026, задача P2-7 / R1-2).
/// Здесь стояло `rend.material = markerMaterial` при создании и `rend.material.color = …` в
/// `SetValid`. Каждое обращение к `Renderer.material` создаёт ЛИЧНУЮ КОПИЮ материала:
/// первый вызов сделал копию уже созданного материала (две аллокации вместо одной), а
/// `SetValid` при каждом переключении «можно/нельзя» писал в `.color` — и в редакторе это
/// сопровождалось предупреждением Unity «Instantiating material due to calling
/// renderer.material», потому что копии никто не уничтожал.
///
/// ЧЕМ ЭТО ГРОЗИЛО. Тот же класс дефекта, из-за которого прогон однажды записал цвет
/// выделения ПРЯМО В АССЕТ `04_Materials/Robot/Robot_HDRP.mat` (P2-7 → P4-15): цвет,
/// записанный в материал, перестаёт быть свойством объекта и становится свойством файла.
///
/// ЧТО СТАЛО. Материал создаётся ОДИН раз и принадлежит маркеру (`sharedMaterial`,
/// уничтожается в `OnDestroy`), а цвет пишется в `MaterialPropertyBlock` — это per-renderer
/// значения, которые не создают копий и не меняют ни материал, ни ассет.
/// </summary>
public class TargetMarker : MonoBehaviour
{
    public float pulseSpeed = 3f;
    public float baseScale = 0.05f;
    public Color validColor = Color.green;
    public Color invalidColor = Color.red;

    private Renderer rend;

    /// <summary>Собственный материал маркера (создан нами — за него и отвечаем).</summary>
    private Material ownMaterial;

    private MaterialPropertyBlock block;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        if (rend == null)
        {
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var mesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere); // уничтожаем временный объект, меш остаётся в памяти

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            rend = gameObject.AddComponent<MeshRenderer>();
            ownMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
                "Материал_маркера", Color.magenta);
            // sharedMaterial, а НЕ material: материал уже наш собственный, копия не нужна.
            if (ownMaterial != null) rend.sharedMaterial = ownMaterial;
        }
        ApplyColor(validColor);
    }

    void OnDestroy()
    {
        if (ownMaterial != null) Destroy(ownMaterial);
        ownMaterial = null;
    }

    void Update()
    {
        float s = baseScale + Mathf.Sin(Time.time * pulseSpeed) * 0.005f;
        transform.localScale = Vector3.one * s;
    }

    public void SetValid(bool valid)
    {
        ApplyColor(valid ? validColor : invalidColor);
    }

    /// <summary>
    /// ЦВЕТ — ЧЕРЕЗ `MaterialPropertyBlock`, БЕЗ КОПИЙ МАТЕРИАЛА.
    ///
    /// Пишем и `_BaseColor` (HDRP/URP), и `_Color` (встроенный конвейер): если шейдер не знает
    /// свойства, `MaterialPropertyBlock` его просто проигнорирует — падения не будет.
    /// </summary>
    private void ApplyColor(Color color)
    {
        if (rend == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor("_BaseColor", color);
        block.SetColor("_Color", color);
        rend.SetPropertyBlock(block);
    }
}
