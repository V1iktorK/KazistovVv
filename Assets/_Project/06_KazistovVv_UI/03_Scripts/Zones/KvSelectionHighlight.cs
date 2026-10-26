using UnityEngine;

namespace KazistovVvUI
{
    /// <summary>
    /// Подсветка выбранного в дереве объекта в СЦЕНЕ: вокруг объекта рисуется
    /// «инженерная» рамка-габарит (12 тонких цилиндров) цветом акцента темы.
    /// Служебный объект рантайма — `HideFlags.HideInHierarchy`, без коллайдеров,
    /// поэтому в `CollisionWorld` он не попадает (правило проекта).
    /// Обновление габаритов — 5 раз в секунду, геометрия создаётся один раз.
    /// </summary>
    public class KvSelectionHighlight : MonoBehaviour
    {
        /// <summary>Интервал обновления габаритов, с.</summary>
        public float refreshInterval = 0.2f;
        /// <summary>Толщина прутьев рамки, м.</summary>
        public float barRadius = 0.006f;

        private Transform container;
        private Transform[] edges;
        private Material material;
        private Transform target;
        private float timer;
        private Renderer[] renderers;

        private void Awake()
        {
            Build();
        }

        private void Build()
        {
            GameObject root = new GameObject("KvSelectionHighlight");
            root.hideFlags = HideFlags.HideInHierarchy;
            container = root.transform;

            // НА УСТРОЙСТВЕ ЗДЕСЬ ПАДАЛО: 08.10.2026 в журнале шлема — ArgumentNullException
            // в KvSelectionHighlight.Build из-за new Material(null). Прямой Shader.Find("HDRP/Unlit")
            // под URP даёт null, а встроенный Unlit/Color мог быть вырезан стриппингом.
            // Материал создаёт ТОЛЬКО фабрика; вернула null — материала просто не будет.
            material = KazistovVvFeatures.KvRenderPipelineShaders.CreateUnlit(
                "Материал_подсветки", KvTheme.Accent);
            if (material != null)
            {
                material.hideFlags = HideFlags.HideAndDontSave;
                Color c = KvTheme.Accent;
                if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", c);
                if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
                if (material.HasProperty("_Color")) material.SetColor("_Color", c);
                if (material.HasProperty("_EmissiveColor"))
                {
                    material.SetColor("_EmissiveColor", c * 2.5f);
                    material.EnableKeyword("_EMISSION");
                }
            }

            edges = new Transform[12];
            for (int i = 0; i < edges.Length; i++)
            {
                GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
                bar.name = "Edge" + i;
                bar.hideFlags = HideFlags.HideInHierarchy;
                Collider col = bar.GetComponent<Collider>();
                if (col != null) Destroy(col);
                bar.transform.SetParent(container, false);
                Renderer r = bar.GetComponent<Renderer>();
                if (r != null && material != null) r.sharedMaterial = material;
                if (r != null) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                edges[i] = bar.transform;
            }
            container.gameObject.SetActive(false);
        }

        /// <summary>Показать рамку вокруг объекта (null — скрыть).</summary>
        public void SetTarget(Transform value)
        {
            target = value;
            renderers = null;
            timer = 0f;
            if (container == null) return;
            bool on = target != null;
            container.gameObject.SetActive(on);
            if (on) UpdateBox();
        }

        /// <summary>Скрыть подсветку.</summary>
        public void Clear()
        {
            SetTarget(null);
        }

        /// <summary>Текущая цель (диагностика).</summary>
        public Transform Target { get { return target; } }

        private void LateUpdate()
        {
            if (target == null) return;
            timer -= Time.unscaledDeltaTime;
            if (timer > 0f) return;
            timer = refreshInterval;
            UpdateBox();
        }

        private void UpdateBox()
        {
            if (target == null || edges == null) return;
            if (renderers == null)
            {
                renderers = target.GetComponentsInChildren<Renderer>(true);
            }

            Bounds b = new Bounds(target.position, Vector3.one * 0.25f);
            bool any = false;
            if (renderers != null)
            {
                foreach (Renderer r in renderers)
                {
                    if (r == null || !r.enabled) continue;
                    if (!any) { b = r.bounds; any = true; }
                    else b.Encapsulate(r.bounds);
                }
            }
            if (!any)
            {
                b = new Bounds(target.position, Vector3.one * 0.3f);
            }

            Vector3 c = b.center;
            Vector3 e = b.extents + Vector3.one * 0.012f;
            float t = barRadius * 2f;

            // 4 вертикальных
            SetBar(0, c + new Vector3(-e.x, 0f, -e.z), new Vector3(t, e.y * 2f, t));
            SetBar(1, c + new Vector3(e.x, 0f, -e.z), new Vector3(t, e.y * 2f, t));
            SetBar(2, c + new Vector3(-e.x, 0f, e.z), new Vector3(t, e.y * 2f, t));
            SetBar(3, c + new Vector3(e.x, 0f, e.z), new Vector3(t, e.y * 2f, t));
            // 4 по X (низ/верх)
            SetBar(4, c + new Vector3(0f, -e.y, -e.z), new Vector3(e.x * 2f, t, t));
            SetBar(5, c + new Vector3(0f, -e.y, e.z), new Vector3(e.x * 2f, t, t));
            SetBar(6, c + new Vector3(0f, e.y, -e.z), new Vector3(e.x * 2f, t, t));
            SetBar(7, c + new Vector3(0f, e.y, e.z), new Vector3(e.x * 2f, t, t));
            // 4 по Z
            SetBar(8, c + new Vector3(-e.x, -e.y, 0f), new Vector3(t, t, e.z * 2f));
            SetBar(9, c + new Vector3(e.x, -e.y, 0f), new Vector3(t, t, e.z * 2f));
            SetBar(10, c + new Vector3(-e.x, e.y, 0f), new Vector3(t, t, e.z * 2f));
            SetBar(11, c + new Vector3(e.x, e.y, 0f), new Vector3(t, t, e.z * 2f));
        }

        private void SetBar(int index, Vector3 worldPos, Vector3 scale)
        {
            if (edges == null || index >= edges.Length || edges[index] == null) return;
            edges[index].position = worldPos;
            edges[index].rotation = Quaternion.identity;
            edges[index].localScale = scale;
        }

        /// <summary>Перекрасить рамку под текущую тему.</summary>
        public void Repaint()
        {
            if (material == null) return;
            Color c = KvTheme.Accent;
            if (material.HasProperty("_UnlitColor")) material.SetColor("_UnlitColor", c);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", c);
            if (material.HasProperty("_Color")) material.SetColor("_Color", c);
            if (material.HasProperty("_EmissiveColor")) material.SetColor("_EmissiveColor", c * 2.5f);
        }

        private void OnDestroy()
        {
            if (container != null) Destroy(container.gameObject);
            if (material != null) Destroy(material);
        }
    }
}
