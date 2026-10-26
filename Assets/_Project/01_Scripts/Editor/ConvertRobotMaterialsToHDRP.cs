using UnityEngine;
using UnityEditor;
using UnityEditor.AssetImporters;
using System.Linq;
using System.IO;

/// <summary>
/// Конвертирует встроенные Standard/Legacy-материалы моделей (Robot.fbx, ScaraRobot.fbx)
/// в отдельные Lit-материалы ТЕКУЩЕГО конвейера проекта.
/// НЕ трогает материалы ангара (ROOMS/HQ Hangar Free).
///
/// ЧТО ИЗМЕНИЛОСЬ В §29. Раньше здесь жёстко брался шейдер `HDRP/Lit`: при работе редактора
/// на URP инструмент создавал бы материалы чужого конвейера, то есть РОЗОВЫЕ. Решение [D9]
/// держит в проекте два конвейера сразу (URP для шлема, HDRP для компьютера), поэтому шейдер
/// берётся у `KvRenderPipelineShaders` — той же фабрики, что и в рантайм-коде, — а свойства
/// цвета/текстур пишутся по факту их наличия (`_BaseColor`/`_Color`, `_BaseMap`/`_BaseColorMap`).
/// Имя класса и пункт меню оставлены узнаваемыми, но теперь это «под текущий конвейер».
/// </summary>
public class ConvertRobotMaterialsToHDRP : EditorWindow
{
    private static readonly string[] TARGET_FBX_PATHS = new string[]
    {
        "Assets/Robots/Robot.fbx",
        "Assets/Robots/ScaraRobot.fbx"
    };

    [MenuItem("Tools/Robots/Convert Materials to Pipeline Lit (HDRP/URP)")]
    public static void ShowWindow()
    {
        GetWindow<ConvertRobotMaterialsToHDRP>("Convert Robot Materials");
    }

    [MenuItem("Tools/Robots/Convert Materials to Pipeline Lit (HDRP/URP)", true)]
    public static bool ValidateConvert()
    {
        // FBX загружается как GameObject, поэтому проверяем тип Object.
        return TARGET_FBX_PATHS.All(path => AssetDatabase.LoadAssetAtPath<Object>(path) != null);
    }

    [ContextMenu("Convert Embedded Materials to Pipeline Lit")]
    public static void Convert()
    {
        int convertedCount = 0;
        int skippedCount = 0;

        foreach (string fbxPath in TARGET_FBX_PATHS)
        {
            GameObject fbxGO = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (fbxGO == null)
            {
                Debug.LogWarning($"[ConvertRobotMaterials] FBX not found: {fbxPath}");
                skippedCount++;
                continue;
            }

            MeshFilter[] meshFilters = fbxGO.GetComponentsInChildren<MeshFilter>();
            SkinnedMeshRenderer[] skinnedRenderers = fbxGO.GetComponentsInChildren<SkinnedMeshRenderer>();

            foreach (var mf in meshFilters)
            {
                if (mf.sharedMesh == null) continue;
                // Материалы хранятся на Renderer, а не на Mesh.
                Renderer renderer = mf.GetComponent<Renderer>();
                if (renderer == null) continue;
                Material[] mats = renderer.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    newMats[i] = mats[i];
                    if (mats[i] != null && IsStandardOrUnlit(mats[i]))
                    {
                        Material newMat = ConvertToHDRPLit(mats[i], fbxPath, i);
                        if (newMat != null)
                        {
                            newMats[i] = newMat;
                            changed = true;
                            convertedCount++;
                        }
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = newMats;
                }
            }

            foreach (var smr in skinnedRenderers)
            {
                if (smr.sharedMesh == null) continue;
                // SkinnedMeshRenderer — это Renderer, материалы лежат на нём.
                Material[] mats = smr.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    newMats[i] = mats[i];
                    if (mats[i] != null && IsStandardOrUnlit(mats[i]))
                    {
                        Material newMat = ConvertToHDRPLit(mats[i], fbxPath, i);
                        if (newMat != null)
                        {
                            newMats[i] = newMat;
                            changed = true;
                            convertedCount++;
                        }
                    }
                }
                if (changed)
                {
                    smr.sharedMaterials = newMats;
                }
            }

            // Update FBX meta to separate materials on next import
            UpdateFBXMetaForMaterialSeparation(fbxPath);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"[ConvertRobotMaterials] Conversion complete. Converted: {convertedCount}, Skipped: {skippedCount}");
        EditorUtility.DisplayDialog("Conversion Complete",
            $"Converted {convertedCount} material(s) to HDRP/Lit.\nSkipped: {skippedCount} FBX file(s).",
            "OK");
    }

    private static bool IsStandardOrUnlit(Material mat)
    {
        if (mat == null) return false;
        string shaderName = mat.shader.name;
        // Check for Standard, Legacy Shaders, or any non-HDRP shader
        return shaderName.Contains("Standard")
            || shaderName.Contains("Legacy")
            || shaderName.Contains("Diffuse")
            || shaderName.Contains("Bumped")
            || shaderName.Contains("Parallax")
            || shaderName.Contains("Specular")
            || (!shaderName.Contains("HDRP") && !shaderName.Contains("High Definition"));
    }

    private static Material ConvertToHDRPLit(Material oldMat, string fbxPath, int matIndex)
    {
        if (oldMat == null) return null;

        string assetDir = Path.GetDirectoryName(AssetDatabase.GetAssetPath(oldMat));
        if (assetDir == null || assetDir.Length == 0)
            assetDir = Path.Combine("Assets", "Robots", "ConvertedMaterials");

        string newMatName = Path.GetFileName(fbxPath).Replace(".fbx", "") + "_Mat" + matIndex;
        if ((oldMat.name == null || oldMat.name.Length == 0) || oldMat.name == "No Name")
            newMatName = Path.GetFileName(fbxPath).Replace(".fbx", "") + "_Mat" + matIndex;
        else
            newMatName = oldMat.name + "_HDRP";

        string matPath = Path.Combine(assetDir, newMatName + ".mat");
        if (File.Exists(matPath))
        {
            Debug.LogWarning($"[ConvertRobotMaterials] Material already exists: {matPath}");
            return AssetDatabase.LoadAssetAtPath<Material>(matPath);
        }

        // МАТЕРИАЛ СОЗДАЁТСЯ ПОД ТЕКУЩИЙ КОНВЕЙЕР ПРОЕКТА, а не под жёстко названный HDRP:
        // в проекте их два (решение [D9]), и шейдер чужого конвейера — это розовая модель.
        Shader litShader = KazistovVvFeatures.KvRenderPipelineShaders.FindLit();
        if (litShader == null)
        {
            Debug.LogError("[ConvertRobotMaterials] Lit-шейдер текущего конвейера не найден — " +
                           "материал не создан. Проверьте пакеты URP/HDRP.");
            return null;
        }

        Material newMat = new Material(litShader);
        newMat.name = newMatName;

        // Цвет: у Standard он в `_Color`, у URP/HDRP Lit — в `_BaseColor`. Кладётся тем же
        // способом, что и в рантайме: по факту наличия свойства, без угадывания имени.
        Color baseColor = oldMat.HasProperty("_Color") ? oldMat.GetColor("_Color")
            : (oldMat.HasProperty("_BaseColor") ? oldMat.GetColor("_BaseColor") : Color.white);
        if (!KazistovVvFeatures.KvRenderPipelineShaders.ApplyColor(newMat, baseColor, false))
            Debug.LogWarning("[ConvertRobotMaterials] у шейдера " + litShader.name +
                             " нет свойства цвета — цвет не перенесён.");

        // Metallic — только скаляр (у Standard нет упакованной карты)
        if (newMat.HasProperty("_Metallic"))
            newMat.SetFloat("_Metallic", oldMat.HasProperty("_Metallic") ? oldMat.GetFloat("_Metallic") : 0f);

        // Smoothness: _Glossiness (Standard) или _Smoothness
        if (newMat.HasProperty("_Smoothness"))
        {
            float smoothness = oldMat.HasProperty("_Glossiness") ? oldMat.GetFloat("_Glossiness")
                : (oldMat.HasProperty("_Smoothness") ? oldMat.GetFloat("_Smoothness") : 0.5f);
            newMat.SetFloat("_Smoothness", smoothness);
        }

        // Normal map: у HDRP/Lit это `_NormalMap`, у URP/Lit — `_BumpMap` (как у Standard).
        Texture normal = oldMat.HasProperty("_BumpMap") ? oldMat.GetTexture("_BumpMap") : null;
        if (normal == null && oldMat.HasProperty("_NormalMap")) normal = oldMat.GetTexture("_NormalMap");
        if (normal != null)
        {
            if (newMat.HasProperty("_NormalMap")) newMat.SetTexture("_NormalMap", normal);
            if (newMat.HasProperty("_BumpMap")) newMat.SetTexture("_BumpMap", normal);
        }
        float normalScale = oldMat.HasProperty("_BumpScale") ? oldMat.GetFloat("_BumpScale")
            : (oldMat.HasProperty("_NormalScale") ? oldMat.GetFloat("_NormalScale") : 1f);
        if (newMat.HasProperty("_NormalScale")) newMat.SetFloat("_NormalScale", normalScale);
        if (newMat.HasProperty("_BumpScale")) newMat.SetFloat("_BumpScale", normalScale);

        // Текстура альбедо: `_MainTex` (Standard), `_BaseColorMap` (HDRP/Lit), `_BaseMap` (URP/Lit).
        Texture albedo = oldMat.HasProperty("_MainTex") ? oldMat.GetTexture("_MainTex") : null;
        if (albedo == null && oldMat.HasProperty("_BaseColorMap")) albedo = oldMat.GetTexture("_BaseColorMap");
        if (albedo == null && oldMat.HasProperty("_BaseMap")) albedo = oldMat.GetTexture("_BaseMap");
        KazistovVvFeatures.KvRenderPipelineShaders.ApplyTexture(newMat, albedo, false);

        // Emission: ключи у конвейеров разные (`_ENABLE_EMISSION` у HDRP, `_EMISSION` у URP
        // и встроенного) — включаются оба, лишний ключ у шейдера просто не существует.
        if (oldMat.HasProperty("_EmissionColor") && oldMat.GetColor("_EmissionColor") != Color.black)
        {
            Color emission = oldMat.GetColor("_EmissionColor");
            if (newMat.HasProperty("_EmissiveColor")) newMat.SetColor("_EmissiveColor", emission);
            if (newMat.HasProperty("_EmissionColor")) newMat.SetColor("_EmissionColor", emission);
            newMat.EnableKeyword("_EMISSION");
            newMat.EnableKeyword("_ENABLE_EMISSION");
        }

        // Cutoff: `_AlphaCutoff` (HDRP) / `_Cutoff` + `_AlphaClip` (URP и Standard).
        if (oldMat.HasProperty("_Cutoff"))
        {
            float cutoff = oldMat.GetFloat("_Cutoff");
            if (newMat.HasProperty("_AlphaCutoff")) newMat.SetFloat("_AlphaCutoff", cutoff);
            if (newMat.HasProperty("_Cutoff")) newMat.SetFloat("_Cutoff", cutoff);
        }

        // Double-sided: `_DoubleSidedEnable` (HDRP) / `_Cull` = 0 (URP).
        if (oldMat.doubleSidedGI)
        {
            if (newMat.HasProperty("_DoubleSidedEnable")) newMat.SetFloat("_DoubleSidedEnable", 1f);
            if (newMat.HasProperty("_Cull")) newMat.SetFloat("_Cull", 0f);
        }

        AssetDatabase.CreateAsset(newMat, matPath);
        AssetDatabase.SaveAssets();

        Debug.Log("[ConvertRobotMaterials] Создан материал конвейера " + litShader.name + ": " + matPath);
        return newMat;
    }

    /// <summary>
    /// Конвертирует ВСЕ Standard/Unlit материалы в сцене (роботы, стол и т.д.).
    /// </summary>
    [MenuItem("Tools/Robots/Convert Scene Materials to HDRP/Lit")]
    public static void ConvertAllInScene()
    {
        int convertedCount = 0;
        Renderer[] allRenderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include);
        foreach (Renderer renderer in allRenderers)
        {
            Material[] mats = renderer.sharedMaterials;
            Material[] newMats = new Material[mats.Length];
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                newMats[i] = mats[i];
                if (mats[i] != null && IsStandardOrUnlit(mats[i]))
                {
                    Material newMat = ConvertToHDRPLit(mats[i], "Assets", i);
                    if (newMat != null)
                    {
                        newMats[i] = newMat;
                        changed = true;
                        convertedCount++;
                    }
                }
            }
            if (changed)
                renderer.sharedMaterials = newMats;
        }
        Debug.Log("[ConvertRobotMaterials] Converted " + convertedCount + " materials in scene.");
    }

    /// <summary>
    /// Конвертирует материалы выделенных GameObject в HDRP/Lit.
    /// Безопасно: не трогает уже-HDRP материалы, не меняет шейдеры ангара напрямую.
    /// </summary>
    [MenuItem("Tools/Robots/Convert Selected Materials to HDRP/Lit")]
    public static void ConvertSelected()
    {
        int convertedCount = 0;
        foreach (GameObject go in Selection.gameObjects)
        {
            if (go == null) continue;
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer renderer in renderers)
            {
                Material[] mats = renderer.sharedMaterials;
                Material[] newMats = new Material[mats.Length];
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    newMats[i] = mats[i];
                    if (mats[i] != null && IsStandardOrUnlit(mats[i]))
                    {
                        Material newMat = ConvertToHDRPLit(mats[i], "Assets", i);
                        if (newMat != null)
                        {
                            newMats[i] = newMat;
                            changed = true;
                            convertedCount++;
                        }
                    }
                }
                if (changed)
                {
                    renderer.sharedMaterials = newMats;
                }
            }
        }
        Debug.Log("[ConvertRobotMaterials] Converted " + convertedCount + " materials on selection.");
    }

    [MenuItem("Tools/Robots/Convert Selected Materials to HDRP/Lit", true)]
    public static bool ValidateConvertSelected()
    {
        return Selection.gameObjects.Length > 0;
    }

    private static void UpdateFBXMetaForMaterialSeparation(string fbxPath)
    {
        string metaPath = fbxPath + ".meta";
        if (!File.Exists(metaPath)) return;

        string metaContent = File.ReadAllText(metaPath);

        // Change materialImportMode from 0 (embedded) to 2 (separate)
        // and materialLocation from 0 (embedded) to 1 (separate)
        bool modified = false;

        if (metaContent.Contains("materialImportMode: 0"))
        {
            metaContent = metaContent.Replace("materialImportMode: 0", "materialImportMode: 2");
            modified = true;
            Debug.Log($"[ConvertRobotMaterials] Updated materialImportMode -> 2 in {metaPath}");
        }

        if (metaContent.Contains("materialLocation: 0"))
        {
            metaContent = metaContent.Replace("materialLocation: 0", "materialLocation: 1");
            modified = true;
            Debug.Log($"[ConvertRobotMaterials] Updated materialLocation -> 1 in {metaPath}");
        }

        if (modified)
        {
            File.WriteAllText(metaPath, metaContent);
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceUpdate);
        }
    }
}
