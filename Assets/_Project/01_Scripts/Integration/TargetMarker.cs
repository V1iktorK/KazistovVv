using UnityEngine;

public class TargetMarker : MonoBehaviour
{
    public float pulseSpeed = 3f;
    public float baseScale = 0.05f;
    public Color validColor = Color.green;
    public Color invalidColor = Color.red;
    
    private Renderer rend;
    
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
            Material markerMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
                "Материал_маркера", Color.magenta);
            if (markerMaterial != null) rend.material = markerMaterial;
        }
    }
    
    void Update()
    {
        float s = baseScale + Mathf.Sin(Time.time * pulseSpeed) * 0.005f;
        transform.localScale = Vector3.one * s;
    }
    
    public void SetValid(bool valid)
    {
        if (rend != null) rend.material.color = valid ? validColor : invalidColor;
    }
}
