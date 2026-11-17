using KazistovVv.Core;
using KazistovVvFeatures;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ГЕНЕРАТОР ПРЕФАБА THOR ИЗ ПАРАМЕТРИЧЕСКОГО ОПИСАТЕЛЯ (задача владельца 17.11.2026).
///
/// ЗАЧЕМ ОН ЕСТЬ, А НЕ «ПРЕФАБ НАРИСОВАН РУКАМИ». Модель Thor должна собираться ИЗ ДАННЫХ:
/// длины звеньев и оси суставов живут в `KvThorModel`, а иерархия `Axis1…Axis6` + TCP — это
/// ровно то, что ищет `KvRobotSpawner` и вращает `SixAxisController`. Если размер поменяется
/// (правка по факту собранного робота или новый экспорт меша), префаб пересобирается здесь же —
/// руками ничего не переносится и расхождение «описатель против иерархии» невозможно.
///
/// ГЕОМЕТРИЯ — ОФИЦИАЛЬНЫЕ МЕШИ THOR (`Assets/_Project/99_ModelSources/Thor/meshes/*.obj`):
/// выгружены из сборки FreeCAD проекта AngelLM/Thor в МИРОВЫХ координатах (миллиметры), поэтому
/// каждый меш висит на своём суставе со сдвигом `−позиция сустава` и масштабом 0,001 (мм → м).
/// Исходники STEP/STL лежат рядом (`Thor-main/step`, `Thor-main/stl`) — путь для будущей замены
/// меша без переделки иерархии: бросьте файл с тем же именем и перегенерируйте.
///
/// КАК ЗАПУСТИТЬ. Меню `Tools/Robots/Build THOR Prefab` в редакторе либо пакетно:
///   Unity -batchmode -projectPath … -executeMethod KvThorPrefabBuilder.Generate -quit
/// </summary>
public static class KvThorPrefabBuilder
{
    /// <summary>Путь префаба модели (тот, что записан в каталоге, без приставки `Resources/`).</summary>
    public const string PrefabPath = "Assets/Resources/Models/Robots/thor.prefab";

    /// <summary>Путь материала звеньев (создаётся один раз и переиспользуется).</summary>
    public const string MaterialPath = "Assets/Resources/Models/Robots/thor_material.mat";

    [MenuItem("Tools/Robots/Build THOR Prefab")]
    public static void Generate()
    {
        // --- 1. меши официальной сборки (импорт Unity, один OBJ = один Mesh)
        var meshes = new Mesh[KvThorModel.MeshTable.Length];
        var missing = new System.Text.StringBuilder();
        for (int i = 0; i < KvThorModel.MeshTable.Length; i++)
        {
            string path = KvThorModel.MeshFolder + "/" + KvThorModel.MeshTable[i].ResourceName + ".obj";
            meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (meshes[i] == null) missing.Append("\n  ").Append(path);
        }
        if (missing.Length > 0)
        {
            Debug.LogError("[KvThor] мешей Thor не найдено (файлы не импортированы?):" + missing);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        // --- 2. иерархия суставов ровно по описателю
        var root = new GameObject("thor");
        try
        {
            Transform[] joints = BuildJoints(root.transform);

            // --- 3. меши на суставы
            Material material = ObtainMaterial();
            for (int i = 0; i < KvThorModel.MeshTable.Length; i++)
            {
                KvThorModel.MeshEntry entry = KvThorModel.MeshTable[i];
                Transform parent = entry.ParentJointIndex < 0
                    ? root.transform
                    : joints[entry.ParentJointIndex];
                AttachMesh(parent, entry.ResourceName, meshes[i], material);
            }

            // --- 4. контроллер: оси и суставы из того же описателя
            SixAxisController six = root.AddComponent<SixAxisController>();
            six.robotName = KvThorModel.Title;
            six.endEffectorName = KvThorModel.EndEffectorName;
            six.jointTransforms = joints;
            six.jointAxesLocal = (Vector3[])KvThorModel.JointAxesLocal.Clone();
            // Меш Thor висит НА суставе, а не на «кривом FBX-пивоте»: компенсация FBX не нужна
            // и переносила бы звено запястья на локоть.
            six.fixLegacyMeshParents = false;

            // --- 5. сохранение
            Object existing = AssetDatabase.LoadAssetAtPath<Object>(PrefabPath);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            if (saved == null)
            {
                Debug.LogError("[KvThor] префаб не сохранён: " + PrefabPath);
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[KvThor] префаб собран: " + PrefabPath +
                      (existing != null ? " (перезаписан)" : " (создан)") +
                      " · звеньев " + KvThorModel.MeshTable.Length +
                      " · суставов " + joints.Length +
                      " · высота вытянутого " + KvThorModel.StretchedHeightM.ToString("0.0000") + " м");
        }
        finally
        {
            Object.DestroyImmediate(root);
        }
    }

    /// <summary>
    /// ЦЕПОЧКА СУСТАВОВ: корень → Axis1 → … → Axis6 → TCP.
    /// Позиции — из описателя (мировые в нулевой позе), локальные считаются разностью, поэтому
    /// в иерархии нет ни одного «магического» числа, кроме данных `KvThorModel`.
    /// </summary>
    private static Transform[] BuildJoints(Transform root)
    {
        var joints = new Transform[KvThorModel.JointNames.Length];

        Transform parent = root;
        Vector3 parentWorld = Vector3.zero;
        Vector3[] worldPositions =
        {
            Vector3.zero,                 // Axis1 — в центре основания (yaw базы)
            KvThorModel.ShoulderPos,      // Axis2
            KvThorModel.ElbowPos,         // Axis3
            KvThorModel.WristColumnPos,   // Axis4
            KvThorModel.WristPos,         // Axis5
            KvThorModel.WristPos          // Axis6 — совпадает с J5 (сферическое запястье)
        };

        for (int i = 0; i < joints.Length; i++)
        {
            if (i > 0)
            {
                // Axis6 висит на Axis5 в той же точке — но родителем всё равно Axis5.
                parent = i == 5 ? joints[4] : parent;
                parentWorld = i == 5 ? worldPositions[4] : parentWorld;
            }
            var go = new GameObject(KvThorModel.JointNames[i]);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = worldPositions[i] - parentWorld;
            go.transform.localRotation = Quaternion.identity;
            joints[i] = go.transform;

            if (i != 5)
            {
                parent = joints[i];
                parentWorld = worldPositions[i];
            }
        }

        // TCP — на фланце, вдоль local Y (ось инструмента в нулевой позе).
        var tcp = new GameObject(KvThorModel.EndEffectorName);
        tcp.transform.SetParent(joints[5], false);
        tcp.transform.localPosition = new Vector3(0f, KvThorModel.WristToTcpM, 0f);
        tcp.transform.localRotation = Quaternion.identity;

        return joints;
    }

    /// <summary>
    /// ПОВЕСИТЬ МЕШ НА СУСТАВ. Меш выгружен в МИРОВЫХ координатах сборки (мм), поэтому объект
    /// меша компенсирует позицию сустава и масштабируется мм → м; поворот в нулевой позе —
    /// identity, потому что оси суставов заданы отдельно (`KvThorModel.JointAxesLocal`).
    /// </summary>
    private static void AttachMesh(Transform parent, string label, Mesh mesh, Material material)
    {
        var go = new GameObject(label);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = -parent.position;   // корень в нуле, повороты identity
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one * 0.001f;   // OBJ в миллиметрах

        var filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        renderer.receiveShadows = true;
    }

    /// <summary>
    /// МАТЕРИАЛ ЗВЕНЬЕВ — ТЕКУЩИЙ КОНВЕЙЕР ПРОЕКТА (`KvRenderPipelineShaders`), а не Standard:
    /// проект держит URP (шлем) и HDRP (компьютер) одновременно, и шейдер «не того»
    /// конвейера даёт розового робота. Создаётся один раз и сохраняется рядом с префабом.
    /// </summary>
    private static Material ObtainMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (material != null) return material;

        material = KvRenderPipelineShaders.CreateLit("Материал_THOR", new Color(0.85f, 0.62f, 0.18f));
        if (material == null)
        {
            Debug.LogError("[KvThor] не удалось создать материал текущего конвейера — " +
                           "звенья будут без материала");
            return null;
        }
        AssetDatabase.CreateAsset(material, MaterialPath);
        AssetDatabase.SaveAssets();
        return material;
    }
}
