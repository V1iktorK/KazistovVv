using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TrajectoryCore;

namespace KazistovVvFeatures
{
    /// <summary>Форма зоны запрета (ЭТАП 5 ТЗ: куб, сфера, цилиндр).</summary>
    public enum KvZoneShape { Box = 0, Sphere = 1, Cylinder = 2 }

    /// <summary>Данные зоны запрета (сохраняются в JSON — одна зона = один файл).</summary>
    [Serializable]
    public class KvZoneData
    {
        public int version = 1;
        public string id = "";
        public string name = "Зона запрета";
        public string created = "";
        public string notes = "";
        public KvZoneShape shape = KvZoneShape.Box;
        /// <summary>Центр в МИРОВЫХ координатах: x, y, z.</summary>
        public float[] center = new float[3];
        /// <summary>Габарит: куб — (ширина, высота, глубина); сфера — (радиус); цилиндр — (радиус, высота).</summary>
        public float[] size = new float[] { 0.4f, 0.4f, 0.4f };
        /// <summary>Поворот (эйлеры, град) — для куба и цилиндра.</summary>
        public float[] euler = new float[3];
        /// <summary>Показывать ли зону в сцене.</summary>
        public bool visible = true;

        [NonSerialized] public string filePath = "";

        public Vector3 Center { get { return ToVector(center, Vector3.zero); } }
        public Vector3 Size { get { return ToVector(size, Vector3.one * 0.4f); } }
        public Vector3 Euler { get { return ToVector(euler, Vector3.zero); } }

        private static Vector3 ToVector(float[] a, Vector3 fallback)
        {
            if (a == null || a.Length < 3) return fallback;
            return new Vector3(a[0], a[1], a[2]);
        }

        public void SetCenter(Vector3 v) { center = new[] { v.x, v.y, v.z }; }
        public void SetSize(Vector3 v) { size = new[] { v.x, v.y, v.z }; }
        public void SetEuler(Vector3 v) { euler = new[] { v.x, v.y, v.z }; }

        public void Normalize()
        {
            if (version <= 0) version = 1;
            if (center == null || center.Length < 3) center = new float[3];
            if (euler == null || euler.Length < 3) euler = new float[3];
            if (size == null || size.Length < 3)
            {
                float r = size != null && size.Length > 0 ? size[0] : 0.4f;
                size = new[] { r, r, r };
            }
            for (int i = 0; i < 3; i++) size[i] = Mathf.Max(0.02f, Mathf.Abs(size[i]));
            if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
            if (string.IsNullOrEmpty(name)) name = "Зона запрета";
        }

        public string ShapeLabel
        {
            get
            {
                switch (shape)
                {
                    case KvZoneShape.Sphere: return "сфера";
                    case KvZoneShape.Cylinder: return "цилиндр";
                    default: return "куб";
                }
            }
        }

        /// <summary>Габарит одной строкой.</summary>
        public string SizeText
        {
            get
            {
                Vector3 s = Size;
                switch (shape)
                {
                    case KvZoneShape.Sphere: return "R = " + s.x.ToString("0.000") + " м";
                    case KvZoneShape.Cylinder: return "R = " + s.x.ToString("0.000") +
                                                      " · H = " + s.y.ToString("0.000") + " м";
                    default: return s.x.ToString("0.000") + " × " + s.y.ToString("0.000") +
                                   " × " + s.z.ToString("0.000") + " м";
                }
            }
        }

        public string Tooltip
        {
            get
            {
                Vector3 c = Center;
                return name + " · " + ShapeLabel +
                       "\nЦентр: " + c.x.ToString("0.000") + ", " + c.y.ToString("0.000") +
                       ", " + c.z.ToString("0.000") +
                       "\nГабарит: " + SizeText +
                       "\nСоздана: " + created +
                       (string.IsNullOrEmpty(notes) ? "" : "\n" + notes);
            }
        }
    }

    /// <summary>
    /// Зона запрета в сцене: полупрозрачный красный объём БЕЗ коллайдера.
    ///
    /// Коллайдер удаляется намеренно: `CollisionWorld.Rebuild` собирает препятствия из
    /// коллайдеров сцены, и живой коллайдер зоны молча изменил бы и планирование, и вердикты
    /// оракула во всём проекте (правило «не ломать существующее поведение»). Проверка
    /// траекторий на пересечение с зонами выполняется ЯВНО — <see cref="KvZoneService.CheckPlan"/>.
    /// </summary>
    public class KvZone : MonoBehaviour
    {
        public KvZoneData Data { get; private set; }

        private Transform body;
        private Renderer bodyRenderer;
        private Material material;

        public string ZoneName { get { return Data != null ? Data.name : name; } }

        public void Initialize(KvZoneData data)
        {
            Data = data;
            Data.Normalize();
            name = "Зона_" + data.name;
            Rebuild();
        }

        /// <summary>Пересобрать визуал под текущие данные (форма/размер/поворот).</summary>
        public void Rebuild()
        {
            if (Data == null) return;
            if (body != null) DestroyImmediateSafe(body.gameObject);

            PrimitiveType primitive = Data.shape == KvZoneShape.Sphere ? PrimitiveType.Sphere
                : Data.shape == KvZoneShape.Cylinder ? PrimitiveType.Cylinder : PrimitiveType.Cube;
            GameObject go = GameObject.CreatePrimitive(primitive);
            go.name = "ZoneBody";
            go.transform.SetParent(transform, false);
            body = go.transform;

            Collider col = go.GetComponent<Collider>();
            if (col != null)
            {
                // Зона — геометрия, а не препятствие мира: коллайдер ВЫКЛЮЧАЕТСЯ сразу
                // (Destroy срабатывает в конце кадра, и в этом кадре CollisionWorld мог бы
                //  подхватить зону как препятствие), затем уничтожается.
                col.enabled = false;
                DestroySafe(col);
            }

            ApplyTransform();

            bodyRenderer = go.GetComponent<Renderer>();
            if (bodyRenderer != null)
            {
                if (material == null)
                {
                    material = KvRenderPipelineShaders.CreateLit("Материал_запретной_зоны", new Color(1f, 0.15f, 0.15f));
                    GhostMaterial.MakeGhost(material, new Color(1f, 0.15f, 0.15f), 0.28f);
                }
                bodyRenderer.sharedMaterial = material;
                bodyRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                bodyRenderer.receiveShadows = false;
                bodyRenderer.enabled = Data.visible;
            }
        }

        /// <summary>Обновить позицию/поворот/масштаб без пересоздания объекта.</summary>
        public void ApplyTransform()
        {
            if (Data == null || body == null) return;
            body.position = Data.Center;
            body.rotation = Quaternion.Euler(Data.Euler);

            Vector3 s = Data.Size;
            switch (Data.shape)
            {
                case KvZoneShape.Sphere:
                    body.localScale = Vector3.one * Mathf.Max(0.02f, s.x) * 2f;    // у Unity-сферы диаметр 1
                    break;
                case KvZoneShape.Cylinder:
                    body.localScale = new Vector3(Mathf.Max(0.02f, s.x) * 2f,
                        Mathf.Max(0.02f, s.y) * 0.5f, Mathf.Max(0.02f, s.x) * 2f);
                    break;
                default:
                    body.localScale = new Vector3(Mathf.Max(0.02f, s.x), Mathf.Max(0.02f, s.y),
                        Mathf.Max(0.02f, s.z));
                    break;
            }
        }

        public void SetVisible(bool visible)
        {
            if (Data != null) Data.visible = visible;
            if (bodyRenderer != null) bodyRenderer.enabled = visible;
        }

        public void SetColor(Color color)
        {
            if (material == null) return;
            GhostMaterial.MakeGhost(material, color, Mathf.Clamp01(color.a));
        }

        /// <summary>Точка внутри зоны (true) — с учётом формы и поворота.</summary>
        public bool ContainsPoint(Vector3 worldPoint)
        {
            return SignedDistance(worldPoint) <= 0f;
        }

        /// <summary>
        /// Расстояние до зоны со знаком: &lt; 0 внутри, &gt; 0 снаружи (по габариту,
        /// для куба/цилиндра — «шахматная» метрика, достаточная для пометки опасных участков).
        /// </summary>
        public float SignedDistance(Vector3 worldPoint)
        {
            if (Data == null) return float.MaxValue;
            Vector3 c = Data.Center;
            Vector3 s = Data.Size;
            Vector3 local = Quaternion.Euler(Data.Euler) == Quaternion.identity
                ? worldPoint - c
                : Quaternion.Inverse(Quaternion.Euler(Data.Euler)) * (worldPoint - c);

            switch (Data.shape)
            {
                case KvZoneShape.Sphere:
                    return local.magnitude - s.x;

                case KvZoneShape.Cylinder:
                {
                    float radial = Mathf.Sqrt(local.x * local.x + local.z * local.z) - s.x;
                    float vertical = Mathf.Abs(local.y) - s.y * 0.5f;
                    return Mathf.Max(radial, vertical);
                }

                default:
                {
                    Vector3 h = s * 0.5f;
                    float dx = Mathf.Abs(local.x) - h.x;
                    float dy = Mathf.Abs(local.y) - h.y;
                    float dz = Mathf.Abs(local.z) - h.z;
                    if (dx <= 0f && dy <= 0f && dz <= 0f) return Mathf.Max(dx, Mathf.Max(dy, dz));
                    return new Vector3(Mathf.Max(dx, 0f), Mathf.Max(dy, 0f), Mathf.Max(dz, 0f)).magnitude;
                }
            }
        }

        private static void DestroySafe(UnityEngine.Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }

        private static void DestroyImmediateSafe(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
        }

        private void OnDestroy()
        {
            if (material != null) DestroySafe(material);
        }
    }

    /// <summary>Результат проверки траектории на пересечение с зонами запрета.</summary>
    public struct KvZoneHit
    {
        public bool hit;
        public string zoneName;
        public string zoneId;
        /// <summary>Насколько глубоко внутрь зашла (м): 0 — коснулась границы.</summary>
        public float penetration;
        /// <summary>Индекс сэмпла траектории, где пересечение самое глубокое.</summary>
        public int sampleIndex;
        /// <summary>Индекс звена робота (0..Dof-1), которое вошло в зону.</summary>
        public int linkIndex;

        public string Describe()
        {
            if (!hit) return "зон запрета на пути нет";
            return "пересекает «" + zoneName + "» (звено " + (linkIndex + 1) +
                   ", сэмпл " + sampleIndex + ", глубина " + (penetration * 1000f).ToString("0") + " мм)";
        }
    }

    /// <summary>
    /// ЗОНЫ ЗАПРЕТА (ЭТАП 5 ТЗ).
    ///
    /// Создание куба/сферы/цилиндра, полупрозрачная красная подсветка, список в дереве моделей,
    /// удаление, изменение размера и перемещение. Планировщик проверяет траектории на пересечение:
    /// каждый сэмпл плана раскладывается в цепочку звеньев робота (`PoseValidator.JointFrames`),
    /// звенья сэмплируются с шагом `linkSampleStep` и проверяются на попадание в зоны.
    /// По ТЗ допускаются оба поведения — «отбросить» и «пометить опасной»; по умолчанию
    /// траектория ПОМЕЧАЕТСЯ (существующие траектории и State Machine не ломаются),
    /// режим «отбрасывать» включается флагом <see cref="discardDangerous"/>.
    /// </summary>
    public class KvZoneService
    {
        /// <summary>Шаг сэмплирования звеньев при проверке, м.</summary>
        public float linkSampleStep = 0.05f;
        /// <summary>Отбрасывать опасные траектории (true) или только помечать (false).</summary>
        public bool discardDangerous = false;
        /// <summary>Не проверять первые/последние сэмплы (робот стоит у цели) — по ТЗ не требуется.</summary>
        public int skipEdgeSamples;

        public event Action Changed;
        public event Action<string> Message;

        private readonly List<KvZone> zones = new List<KvZone>();
        private Transform root;

        public IReadOnlyList<KvZone> Zones { get { return zones; } }
        public int Count { get { return zones.Count; } }

        /// <summary>Контейнер зон в сцене (виден в Hierarchy — оператор должен их находить).</summary>
        public Transform Root
        {
            get
            {
                if (root == null)
                {
                    GameObject go = new GameObject("ЗоныЗапрета");
                    root = go.transform;
                }
                return root;
            }
        }

        // ------------------------------------------------------------------ создание/удаление

        /// <summary>Создать зону заданной формы с центром в точке и габаритом по умолчанию.</summary>
        public KvZone Create(KvZoneShape shape, Vector3 center, Vector3? size = null, string name = null)
        {
            KvZoneData data = new KvZoneData
            {
                id = Guid.NewGuid().ToString("N"),
                name = string.IsNullOrEmpty(name) ? DefaultName(shape) : name,
                created = FeatureStorage.IsoNow(),
                shape = shape
            };
            data.SetCenter(center);
            data.SetSize(size.HasValue ? size.Value : DefaultSize(shape));
            return Create(data);
        }

        public KvZone Create(KvZoneData data)
        {
            if (data == null) return null;
            GameObject go = new GameObject();
            go.transform.SetParent(Root, false);
            KvZone zone = go.AddComponent<KvZone>();
            zone.Initialize(data);
            zones.Add(zone);

            Debug.Log("[Zone] создана зона «" + data.name + "» · " + data.ShapeLabel + " · " +
                      data.SizeText + " · центр " + data.Center);
            if (Message != null) Message("зона запрета «" + data.name + "» создана (" + data.ShapeLabel + ")");
            Save(zone);
            Raise();
            return zone;
        }

        public bool Delete(KvZone zone)
        {
            if (zone == null) return false;
            KvZoneData data = zone.Data;
            zones.Remove(zone);
            FeatureStorage.DeleteFile(data != null ? data.filePath : null);
            if (Application.isPlaying) UnityEngine.Object.Destroy(zone.gameObject);
            else UnityEngine.Object.DestroyImmediate(zone.gameObject);

            Debug.Log("[Zone] удалена зона «" + (data != null ? data.name : "?") + "»");
            if (Message != null) Message("зона запрета удалена");
            Raise();
            return true;
        }

        public void Clear()
        {
            for (int i = zones.Count - 1; i >= 0; i--) Delete(zones[i]);
        }

        public KvZone Find(string id)
        {
            for (int i = 0; i < zones.Count; i++)
                if (zones[i] != null && zones[i].Data != null && zones[i].Data.id == id) return zones[i];
            return null;
        }

        /// <summary>Переместить зону (и сохранить).</summary>
        public void Move(KvZone zone, Vector3 center)
        {
            if (zone == null) return;
            zone.Data.SetCenter(center);
            zone.ApplyTransform();
            Save(zone);
        }

        /// <summary>Изменить габарит зоны (и сохранить).</summary>
        public void Resize(KvZone zone, Vector3 size)
        {
            if (zone == null) return;
            zone.Data.SetSize(size);
            zone.Data.Normalize();
            zone.Rebuild();
            Save(zone);
        }

        /// <summary>Повернуть зону (и сохранить).</summary>
        public void Rotate(KvZone zone, Vector3 euler)
        {
            if (zone == null) return;
            zone.Data.SetEuler(euler);
            zone.ApplyTransform();
            Save(zone);
        }

        public void Rename(KvZone zone, string newName)
        {
            if (zone == null || string.IsNullOrEmpty(newName)) return;
            zone.Data.name = newName;
            zone.gameObject.name = "Зона_" + newName;
            Save(zone);
            Raise();
        }

        public void SetVisible(KvZone zone, bool visible)
        {
            if (zone == null) return;
            zone.SetVisible(visible);
            Save(zone);
        }

        /// <summary>Показать/скрыть все зоны разом (кнопка «Зоны запрета» в тулбаре).</summary>
        public void SetAllVisible(bool visible)
        {
            for (int i = 0; i < zones.Count; i++) zones[i].SetVisible(visible);
        }

        /// <summary>Убрать сам контейнер при выходе (как фантомы — служебные объекты не «протекают»).</summary>
        public void DestroyRoot()
        {
            zones.Clear();
            if (root != null)
            {
                if (Application.isPlaying) UnityEngine.Object.Destroy(root.gameObject);
                else UnityEngine.Object.DestroyImmediate(root.gameObject);
                root = null;
            }
        }

        // ------------------------------------------------------------------ проверка траекторий

        /// <summary>
        /// Проверить план на пересечение с зонами запрета (по звеньям робота, не только по TCP).
        /// </summary>
        public KvZoneHit CheckPlan(PlannedTrajectory plan, PoseValidator validator, int dof)
        {
            KvZoneHit result = new KvZoneHit { penetration = -1f, linkIndex = -1, sampleIndex = -1 };
            if (plan == null || plan.Path == null || zones.Count == 0) return result;
            if (validator == null || !validator.Ready) return result;

            int n = plan.Path.Length;
            if (n == 0) return result;

            // Вектор пивотов/осей переиспользуется: JointFrames не создаёт мусор в кадре.
            int count = Mathf.Max(2, dof);
            Vector3[] pivots = new Vector3[count + 1];
            Vector3[] axes = new Vector3[count];
            float step = Mathf.Max(0.01f, linkSampleStep);

            for (int i = Mathf.Max(0, skipEdgeSamples); i < n - skipEdgeSamples; i++)
            {
                double[] q = plan.Path[i];
                if (q == null || q.Length == 0) continue;
                try
                {
                    validator.JointFrames(q, pivots, axes);
                }
                catch (Exception)
                {
                    // Неподходящая длина конфигурации (например, план другого робота) — проверку пропускаем.
                    continue;
                }

                for (int link = 0; link + 1 < pivots.Length && link < count; link++)
                {
                    Vector3 a = pivots[link];
                    Vector3 b = pivots[link + 1];
                    float length = Vector3.Distance(a, b);
                    int steps = Mathf.Clamp(Mathf.CeilToInt(length / step), 1, 200);
                    for (int k = 0; k <= steps; k++)
                    {
                        Vector3 p = Vector3.Lerp(a, b, k / (float)steps);
                        for (int z = 0; z < zones.Count; z++)
                        {
                            KvZone zone = zones[z];
                            if (zone == null || zone.Data == null) continue;
                            float d = zone.SignedDistance(p);
                            if (d > 0f) continue;
                            float pen = -d;
                            if (pen > result.penetration)
                            {
                                result.hit = true;
                                result.penetration = pen;
                                result.zoneName = zone.Data.name;
                                result.zoneId = zone.Data.id;
                                result.sampleIndex = i;
                                result.linkIndex = link;
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>Точка внутри какой-нибудь зоны (для предупреждения при выборе точки).</summary>
        public KvZone ZoneAt(Vector3 worldPoint)
        {
            for (int i = 0; i < zones.Count; i++)
            {
                KvZone z = zones[i];
                if (z != null && z.Data != null && z.ContainsPoint(worldPoint)) return z;
            }
            return null;
        }

        // ------------------------------------------------------------------ хранение

        public void Save(KvZone zone)
        {
            if (zone == null || zone.Data == null) return;
            if (string.IsNullOrEmpty(zone.Data.filePath))
            {
                zone.Data.filePath = Path.Combine(FeatureStorage.ZonesDir,
                    FeatureStorage.SafeName(zone.Data.name, "zone") + "_" + zone.Data.id.Substring(0, 6) +
                    KvZoneStore.Extension);
            }
            FeatureStorage.SaveJson(zone.Data.filePath, zone.Data);
        }

        public void SaveAll()
        {
            for (int i = 0; i < zones.Count; i++) Save(zones[i]);
        }

        /// <summary>Загрузить зоны с диска (при старте и по кнопке «Загрузить зоны»).</summary>
        public int LoadAll()
        {
            List<KvZoneData> data = KvZoneStore.LoadAll();
            int added = 0;
            foreach (KvZoneData d in data)
            {
                if (Find(d.id) != null) continue;
                Create(d);
                added++;
            }
            return added;
        }

        private void Raise()
        {
            if (Changed != null) Changed();
        }

        public static string DefaultName(KvZoneShape shape)
        {
            switch (shape)
            {
                case KvZoneShape.Sphere: return "Сфера запрета";
                case KvZoneShape.Cylinder: return "Цилиндр запрета";
                default: return "Куб запрета";
            }
        }

        public static Vector3 DefaultSize(KvZoneShape shape)
        {
            switch (shape)
            {
                case KvZoneShape.Sphere: return new Vector3(0.25f, 0.25f, 0.25f);
                case KvZoneShape.Cylinder: return new Vector3(0.20f, 0.40f, 0.20f);
                default: return new Vector3(0.30f, 0.30f, 0.30f);
            }
        }
    }

    /// <summary>Хранилище зон запрета на диске.</summary>
    public static class KvZoneStore
    {
        public const string Extension = ".zone.json";

        public static List<KvZoneData> LoadAll()
        {
            List<KvZoneData> result = new List<KvZoneData>();
            foreach (string file in FeatureStorage.ListFiles(FeatureStorage.ZonesDir, "*" + Extension))
            {
                KvZoneData data = FeatureStorage.LoadJson<KvZoneData>(file);
                if (data == null) continue;
                data.filePath = file;
                data.Normalize();
                result.Add(data);
            }
            return result;
        }

        public static string Save(KvZoneData data)
        {
            if (data == null) return null;
            data.Normalize();
            if (string.IsNullOrEmpty(data.filePath))
                data.filePath = Path.Combine(FeatureStorage.ZonesDir,
                    FeatureStorage.SafeName(data.name, "zone") + "_" + data.id.Substring(0, 6) + Extension);
            return FeatureStorage.SaveJson(data.filePath, data) ? data.filePath : null;
        }
    }
}
