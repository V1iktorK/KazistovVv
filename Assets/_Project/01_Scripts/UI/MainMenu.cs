using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Главное меню: запуск симуляции, настройки и выход.
/// </summary>
/// <summary>
/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Главное меню — KazistovVvUIManager.
/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.
/// </summary>
[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Главное меню — KazistovVvUIManager.")]
public class MainMenu : MonoBehaviour
{
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject loadingPanel;

    public void OnStartSimulation()
    {
        mainPanel.SetActive(false);
        loadingPanel.SetActive(true);
        SceneManager.LoadScene("SimulationScene");
    }

    public void OnOpenSettings()
    {
        settingsPanel.SetActive(true);
        mainPanel.SetActive(false);
    }

    public void OnExit()
    {
        #if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
        #else
            Application.Quit();
        #endif
    }
}
