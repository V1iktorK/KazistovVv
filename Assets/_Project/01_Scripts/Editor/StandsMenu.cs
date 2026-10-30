using TrajectoryCore;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Инструменты сборки стендов (столы + роботы) в MainScene.
///
/// ВАЖНО: пункты меню Tools/KazistovVv отсюда УДАЛЕНЫ — столы и роботы теперь
/// стоят в MainScene как обычные объекты сцены (см. Assets/_Project/00_Scenes/
/// MainScene.unity) и не должны создаваться ни кнопкой, ни в рантайме.
/// Класс оставлен как утилита: методы можно вызвать из своего кода/консоли,
/// если потребуется пересобрать стенды вручную.
/// </summary>
/// <summary>
/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Меню стендов — KvXrStartMenu (стартовое меню шлема) и дерево моделей KazistovVvUIManager.
/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.
/// </summary>
[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Меню стендов — KvXrStartMenu (стартовое меню шлема) и дерево моделей KazistovVvUIManager.")]
public static class StandsMenu
{
    /// <summary>Пересобрать сцену: удалить старые столы/роботов и создать два стенда.</summary>
    public static void RebuildScene()
    {
        StandBuilder.RebuildStandaloneScene(StandBuilder.TopHeight);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log("[Stands] Сцена пересобрана: (0,0,-20.7) — 6-осевой, (0,0,-31.3) — SCARA; " +
                  "столешницы 14.4×9.6, зазор между столами 1 юнит");
    }

    /// <summary>Разместить два стенда, не удаляя существующие объекты.</summary>
    public static void PlaceStands()
    {
        StandBuilder.EnsureStands(StandBuilder.TopHeight);
        if (!Application.isPlaying)
        {
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
        }
        Debug.Log("[Stands] Стенды размещены: (0,0,-20.7) 6-осевой, (0,0,-31.3) SCARA; " +
                  "столешницы 14.4×9.6, зазор между столами 1 юнит; " +
                  "роботы — у внутренней кромки (мировые z = −23.9 и −28.1)");
    }
}
