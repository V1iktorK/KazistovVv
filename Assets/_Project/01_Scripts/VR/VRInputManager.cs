using UnityEngine;

/// <summary>

/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Ввод делает KvXrInputSource.

/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.

/// </summary>

[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Ввод делает KvXrInputSource.")]

public class VRInputManager : MonoBehaviour
{
    [Header("OVR Performance")]
    public bool useFoveatedRendering = true;
    
    void Start()
    {
        #if OCULUS_SDK
        if (useFoveatedRendering)
        {
            OVRManager.foveatedRenderingLevel = OVRManager.FoveatedRenderingLevel.High;
            OVRManager.useDynamicFoveatedRendering = true;
        }
        #endif
    }
    
    void Update()
    {
        #if OCULUS_SDK
        if (OVRInput.GetDown(OVRInput.Button.Start))
            OVRManager.display.RecenterPose();
        #endif
    }
}
