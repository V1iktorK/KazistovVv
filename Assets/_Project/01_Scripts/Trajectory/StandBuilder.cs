using System.Collections.Generic;
using UnityEngine;

namespace TrajectoryCore
{
    /// <summary>
    /// Стенды (две «сцены» с оборудованием):
    ///   Стенд 1: стол в (0,0,−20.7) → 6-осевой робот на нём;
    ///   Стенд 2: стол в (0,0,−31.3) → SCARA на нём.
    ///
    /// ГЕОМЕТРИЯ (ТЗ сессии 13.09.2026 — «увеличить столы в 3 раза»):
    ///   столешница 14.4 × 9.6 юнита (было 4.8 × 3.2, то есть ×3 по ширине и глубине),
    ///   высота НЕ менялась (верхняя плоскость y = 0.98), ножки — по углам столешницы,
    ///   зазор между ближними краями столешниц = 1 юнит:
    ///     10.6 (расстояние между центрами) − 4.8 − 4.8 (половины глубины) = 1.0
    ///
    /// ВАЖНО ПРО РОБОТОВ: столы выросли НАРУЖУ (внутренние кромки остались на прежних
    /// местах z = −25.5 и z = −26.5), а роботы стоят у ВНУТРЕННЕЙ кромки своей столешницы
    /// (`RobotInsetFromInnerEdge` = 1.6, локально z = ∓3.2). Так их мировые координаты
    /// остались прежними — (0, 0.98, −23.9) и (0, 0.98, −28.1): рабочая зона оракула
    /// (±1.6 юнита от базы, `ReachabilityOracle.QuerySixAxis`) и весь отлаженный сценарий
    /// не сдвинулись, хотя столы стали втрое больше.
    ///
    /// Процедура идемпотентна (по именам объектов), масштаб корней = 1.
    /// </summary>
    public static class StandBuilder
    {
        public const string Stand1Name = "Стенд_1_Стол";
        public const string Stand2Name = "Стенд_2_Стол";

        // Геометрия стола (единая точка правды для сцены и пересборки).
        public const float TopHeight = 0.98f;      // верхняя плоскость (роботы стоят на ней)
        public const float TopThickness = 0.05f;   // толщина столешницы
        public const float TableWidth = 14.4f;     // было 4.8 → ×3 (исходно 1.2 → ×12)
        public const float TableDepth = 9.6f;      // было 3.2 → ×3 (исходно 0.8 → ×12)
        public const float LegInset = 0.1f;        // отступ ножки от края столешницы
        public const float GapBetweenTables = 1f;  // зазор между ближними краями (не менялся)
        public const float RobotInsetFromInnerEdge = 1.6f;  // робот у внутренней кромки (как в сцене)

        // Центры столов: разнесены так, чтобы зазор между столешницами был ровно 1 юнит
        // (10.6 = 4.8 + 1.0 + 4.8). Внутренние кромки — на прежних z (−25.5 и −26.5).
        public static readonly Vector3 Stand1Pos = new Vector3(0f, 0f, -20.7f);
        public static readonly Vector3 Stand2Pos = new Vector3(0f, 0f, -31.3f);

        /// <summary>Создать оба стенда, если их ещё нет (идемпотентно).</summary>
        public static void EnsureStands(float tableTopHeight = 0.98f)
        {
            GameObject stand1 = FindByName(Stand1Name);
            if (stand1 == null) stand1 = BuildTable(Stand1Name, Stand1Pos, tableTopHeight);
            GameObject stand2 = FindByName(Stand2Name);
            if (stand2 == null) stand2 = BuildTable(Stand2Name, Stand2Pos, tableTopHeight);

            // Робот — у ВНУТРЕННЕЙ кромки своей столешницы (стенд 1 смотрит в −Z, стенд 2 в +Z):
            // так его мировая позиция совпадает с той, что стоит в MainScene.
            PlaceRobotOnStand(stand1, typeof(SixAxisController), Robot1CopyName,
                Stand1Pos.z - (TableDepth * 0.5f - RobotInsetFromInnerEdge));
            PlaceRobotOnStand(stand2, typeof(SCARAController), Robot2CopyName,
                Stand2Pos.z + (TableDepth * 0.5f - RobotInsetFromInnerEdge));
        }

        public const string Robot1CopyName = "Робот_6ос_Стенд1";
        public const string Robot2CopyName = "SCARA_Стенд2";

        /// <summary>
        /// Полное пересоздание сцены (для меню редактора): удалить старые столы/роботов
        /// и создать только два стенда. Копии роботов делаются ДО удаления шаблонов.
        /// </summary>
        public static void RebuildStandaloneScene(float tableTopHeight = 0.98f)
        {
            // 1. Сначала создаём новое (копии берутся с существующих роботов-шаблонов).
            EnsureStands(tableTopHeight);

            // 2. Удаляем всё старое: прежние столы/стенды/роботов, кроме новых стендов.
            var toDelete = new List<GameObject>();
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null) continue;
                GameObject go = t.gameObject;
                if (go.name == Stand1Name || go.name == Stand2Name ||
                    go.name == Robot1CopyName || go.name == Robot2CopyName) continue;
                if (go.name == "Phantoms" || go.name.StartsWith("Траектория")) continue;
                if (go.transform.parent != null &&
                    (go.transform.parent.name == Stand1Name || go.transform.parent.name == Stand2Name)) continue;

                bool isOldStand = go.name.StartsWith("Стенд_") || go.name.StartsWith("Стол_");
                bool isOldRobot = go.GetComponent<RobotController>() != null;
                bool isOldTable = go.GetComponent<KazistovVvUI.RegisteredObject>() != null &&
                                  (go.name.ToLowerInvariant().Contains("стол") ||
                                   go.name.ToLowerInvariant().Contains("desk") ||
                                   go.name.ToLowerInvariant().Contains("table"));
                if (isOldStand || isOldRobot || isOldTable) toDelete.Add(go);
            }
            foreach (GameObject go in toDelete)
                if (go != null) DestroyGo(go);

            // 3. Убираем «сирот» (Кубы-столы от старого спавна).
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
            {
                if (t == null || t.parent == null) continue;
                string n = t.name.ToLowerInvariant();
                if (n.StartsWith("стол_") && t.GetComponent<MeshRenderer>() != null)
                    DestroyGo(t.gameObject);
            }
        }

        /// <summary>В редакторе Destroy отложен — нужен DestroyImmediate, иначе объекты остаются.</summary>
        private static void DestroyGo(Object go)
        {
            if (go == null) return;
            if (Application.isPlaying) Object.Destroy(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Стол: столешница 14.4 × 9.6 (было 4.8 × 3.2, исходно 1.2 × 0.8) + 4 ножки по углам.
        /// Корень scale = (1,1,1), размеры заданы деталями; высота столешницы не менялась (0.98).
        /// </summary>
        private static GameObject BuildTable(string name, Vector3 position, float topHeight)
        {
            return BuildTable(name, position, topHeight, TableWidth, TableDepth);
        }

        /// <summary>
        /// МАТЕРИАЛ СТОЛА: у созданного объекта он ОБЯЗАН быть таким же, как у столов сцены
        /// (дефект Д18, найдено в шлеме 25.10.2026: «стол розовый, робот зелёный, так не должно быть»).
        ///
        /// ПОЧЕМУ РОЗОВЫЙ. `GameObject.CreatePrimitive` даёт материал по умолчанию, а в URP/HDRP его
        /// шейдер не совпадает с активным конвейером — Unity рисует такой материал ЯРКО-РОЗОВЫМ
        /// («missing shader»). Столы сцены этим не болеют: у них назначен `Material_1_URP.mat`.
        ///
        /// ОТКУДА БЕРЁМ. Сначала — с реального стола сцены (значит, гарантированно правильный конвейер
        /// и вид). Если столов в сцене нет — создаём литературный материал тем же путём, что у пола
        /// XR-сцены (`KvRenderPipelineShaders.CreateLit`).
        /// </summary>
        private static Material TableMaterial()
        {
            if (cachedTableMaterial != null) return cachedTableMaterial;

            // 1) материал со сценного стола («Стенд_1_Стол» / «Стенд_2_Стол» и их столешницы)
            GameObject stand = FindByName(Stand1Name);
            if (stand == null) stand = FindByName(Stand2Name);
            if (stand != null)
            {
                MeshRenderer[] renderers = stand.GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (renderers[i] == null || renderers[i].sharedMaterial == null) continue;
                    cachedTableMaterial = renderers[i].sharedMaterial;
                    return cachedTableMaterial;
                }
            }

            // 2) запасной путь: материал того же вида, что у пола XR-сцены
            cachedTableMaterial = KazistovVvFeatures.KvRenderPipelineShaders.CreateLit(
                "Материал_стола_XR", new Color(0.42f, 0.44f, 0.47f));
            return cachedTableMaterial;
        }

        private static Material cachedTableMaterial;

        /// <summary>Назначить материал ВСЕМ частям стола (столешница и ножки).</summary>
        private static void ApplyTableMaterial(GameObject root)
        {
            if (root == null) return;

            Material material = TableMaterial();
            if (material == null) return;

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                renderers[i].sharedMaterial = material;
            }
        }

        /// <summary>
        /// СТОЛ ЗАДАННОГО РАЗМЕРА (§42). Добавлено для шлема: там из меню создаются ДВА вида столов —
        /// квадратный 0,72 × 0,48 м («как в ПК, только меньше») и круглый «авто» (см. `BuildRoundTable`).
        /// Тот же построитель, что у стендов ПК: второй геометрии стола в проекте не появляется.
        /// </summary>
        public static GameObject BuildTable(string name, Vector3 position, float topHeight,
                                            float width, float depth)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            float topThickness = TopThickness;
            float topY = Mathf.Max(0.2f, topHeight) - topThickness * 0.5f;
            float safeWidth = Mathf.Max(0.1f, width);
            float safeDepth = Mathf.Max(0.1f, depth);

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cube);
            top.name = "Столешница";
            top.transform.SetParent(root.transform, false);
            top.transform.localScale = new Vector3(safeWidth, topThickness, safeDepth);
            top.transform.localPosition = new Vector3(0f, topY, 0f);

            float legHeight = Mathf.Max(0.1f, topHeight - topThickness);
            float legX = Mathf.Max(0.02f, safeWidth * 0.5f - LegInset);
            float legZ = Mathf.Max(0.02f, safeDepth * 0.5f - LegInset);
            BuildLegs(root.transform, legHeight, legX, legZ);

            ApplyTableMaterial(root);        // §42/Д18: материал как у столов сцены (не розовый)
            root.AddComponent<KazistovVvUI.RegisteredObject>().DisplayName = name;
            return root;
        }

        /// <summary>
        /// КРУГЛЫЙ СТОЛ «АВТО» (§42): радиус считается от вылета руки робота.
        /// Столешница — цилиндр, ножки — по четырём направлениям, чтобы стол не «парил».
        /// </summary>
        public static GameObject BuildRoundTable(string name, Vector3 position, float topHeight, float radius)
        {
            GameObject root = new GameObject(name);
            root.transform.position = position;
            root.transform.rotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            float topThickness = TopThickness;
            float topY = Mathf.Max(0.2f, topHeight) - topThickness * 0.5f;
            float safeRadius = Mathf.Max(0.15f, radius);

            GameObject top = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            top.name = "Столешница";
            top.transform.SetParent(root.transform, false);
            // Цилиндр Unity имеет высоту 2 при масштабе 1: масштаб Y = толщина / 2.
            top.transform.localScale = new Vector3(safeRadius * 2f, topThickness * 0.5f, safeRadius * 2f);
            top.transform.localPosition = new Vector3(0f, topY, 0f);
            RemoveCollider(top);

            float legHeight = Mathf.Max(0.1f, topHeight - topThickness);
            float legOffset = Mathf.Max(0.02f, safeRadius * 0.55f);
            BuildLegs(root.transform, legHeight, legOffset, legOffset);

            ApplyTableMaterial(root);        // §42/Д18: материал как у столов сцены (не розовый)
            root.AddComponent<KazistovVvUI.RegisteredObject>().DisplayName = name;
            return root;
        }

        /// <summary>Четыре ножки по углам (или по четырём направлениям у круглого стола).</summary>
        private static void BuildLegs(Transform parent, float legHeight, float legX, float legZ)
        {
            for (int i = 0; i < 4; i++)
            {
                GameObject leg = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leg.name = "Ножка_" + (i + 1);
                leg.transform.SetParent(parent, false);
                leg.transform.localScale = new Vector3(0.06f, legHeight, 0.06f);
                leg.transform.localPosition = new Vector3(
                    (i % 2 == 0 ? -legX : legX), legHeight * 0.5f, (i < 2 ? -legZ : legZ));
            }
        }

        /// <summary>Коллайдер столешницы круглого стола не нужен: луч и постановка робота идут по именам.</summary>
        private static void RemoveCollider(GameObject go)
        {
            Collider collider = go.GetComponent<Collider>();
            if (collider == null) return;
            if (Application.isPlaying) Object.Destroy(collider);
            else Object.DestroyImmediate(collider);
        }

        /// <summary>
        /// Поставить копию робота нужного типа на верхнюю плоскость стенда.
        /// robotZ — МИРОВАЯ координата z, куда встаёт робот (у внутренней кромки столешницы,
        /// см. <see cref="RobotInsetFromInnerEdge"/>); null — центр стола.
        /// </summary>
        private static void PlaceRobotOnStand(GameObject stand, System.Type robotType, string copyName,
                                              float? robotZ = null)
        {
            if (stand == null) return;
            if (FindByName(copyName) != null) return;   // уже стоит

            RobotController template = FindTemplate(robotType);
            if (template == null)
            {
                Debug.LogWarning("[StandBuilder] Нет шаблона робота типа " + robotType.Name);
                return;
            }

            GameObject copy = Object.Instantiate(template.gameObject);
            copy.name = copyName;
            Bounds standBounds = GetBounds(stand);
            Vector3 pos = new Vector3(stand.transform.position.x, standBounds.max.y,
                                      robotZ.HasValue ? robotZ.Value : stand.transform.position.z);
            copy.transform.position = pos;
            copy.transform.rotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;

            SixAxisAutoSetup auto = copy.GetComponent<SixAxisAutoSetup>();
            if (auto != null) Object.Destroy(auto);
            copy.AddComponent<KazistovVvUI.RegisteredObject>().DisplayName = copyName;

            RobotController rc = copy.GetComponent<RobotController>();
            if (rc != null) rc.ClearTarget();
        }

        private static RobotController FindTemplate(System.Type robotType)
        {
            foreach (RobotController rc in Object.FindObjectsByType<RobotController>(
                         FindObjectsInactive.Include))
            {
                if (rc == null) continue;
                if (!robotType.IsInstanceOfType(rc)) continue;
                string n = rc.name;
                if (n.StartsWith("Робот_") || n.StartsWith("SCARA_")) continue; // это копии стендов
                if (rc.GetComponent<KazistovVvUI.RegisteredObject>() != null) continue;
                return rc;
            }
            return null;
        }

        /// <summary>Шаблон робота (для редакторского инструмента).</summary>
        public static RobotController TemplateFor(System.Type robotType) => FindTemplate(robotType);

        private static GameObject FindByName(string name)
        {
            foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include))
                if (t != null && t.name == name) return t.gameObject;
            return null;
        }

        private static Bounds GetBounds(GameObject go)
        {
            Renderer[] rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.2f);
            Bounds b = rs[0].bounds;
            for (int i = 1; i < rs.Length; i++) b.Encapsulate(rs[i].bounds);
            return b;
        }
    }
}
