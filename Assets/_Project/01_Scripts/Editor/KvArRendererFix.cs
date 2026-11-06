using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;

namespace KazistovVvArEditor
{
    /// <summary>
    /// ИСПРАВЛЕНИЕ ФОНА AR-КАМЕРЫ В URP (06.11.2026).
    ///
    /// СИМПТОМ. На планшете камера РАБОТАЛА (`ARCameraManager.frameReceived` — десятки кадров,
    /// `dumpsys media.camera`: device 0 открыт клиентом com.KazistovVv.app), CPU-кадр ARCore
    /// сохранялся нормальным, но на экране вместо камеры была ЖЁЛТАЯ заливка.
    ///
    /// ПРИЧИНА. `ARCameraBackground` в URP требует `ARBackgroundRendererFeature` в списке render
    /// features рендерера — он и рисует фон камеры. В `Mobile_Renderer.asset` был только
    /// `ARCommandBufferSupportRendererFeature` (другая задача — интеграция command buffer), а
    /// `ARBackgroundRendererFeature` отсутствовал. Поэтому фон не рисовался вовсе.
    ///
    /// ЧТО ДЕЛАЕТ. Добавляет `ARBackgroundRendererFeature` в Mobile_Renderer (и PC_Renderer —
    /// на случай ПК) один раз, идемпотентно.
    /// </summary>
    public static class KvArRendererFix
    {
        [MenuItem("KazistovVv/AR/Добавить AR Background Renderer Feature")]
        public static void AddArBackgroundFeature()
        {
            string[] paths =
            {
                "Assets/Settings/Mobile_Renderer.asset",
                "Assets/Settings/PC_Renderer.asset"
            };

            foreach (string path in paths)
            {
                ScriptableRendererData data = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(path);
                if (data == null)
                {
                    Debug.LogWarning("[KvArFix] нет рендерера " + path);
                    continue;
                }

                bool exists = false;
                for (int i = 0; i < data.rendererFeatures.Count; i++)
                {
                    if (data.rendererFeatures[i] is ARBackgroundRendererFeature) { exists = true; break; }
                }
                if (exists)
                {
                    Debug.Log("[KvArFix] " + path + ": ARBackgroundRendererFeature уже есть");
                    continue;
                }

                ARBackgroundRendererFeature feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                feature.name = "AR Background";
                AssetDatabase.AddObjectToAsset(feature, data);
                data.rendererFeatures.Add(feature);
                EditorUtility.SetDirty(data);
                AssetDatabase.SaveAssets();
                Debug.Log("[KvArFix] " + path + ": добавлен ARBackgroundRendererFeature");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
    }
}