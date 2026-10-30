using UnityEngine;
using UnityEngine.UI;          // для Button, Dropdown, Slider, Toggle, InputField
using UnityEngine.SceneManagement; // если понадобится
using System.IO;              // для File, Path
using System;                 // для Convert, но не обязательно
// SettingsMenu.cs
/// <summary>
/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Настройки — KvSettingsView.
/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.
/// </summary>
[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Настройки — KvSettingsView.")]
public class SettingsMenu : MonoBehaviour
{
    [SerializeField] private SettingsData settings;
    
    // --- Вкладки ---
    [SerializeField] private GameObject[] tabContents;
    [SerializeField] private Button[] tabButtons;
    
    // --- Элементы UI: Ввод ---
    [SerializeField] private Dropdown inputDeviceDropdown;
    [SerializeField] private Slider mouseSensSlider;
    [SerializeField] private Slider gamepadSensSlider;
    [SerializeField] private Toggle invertYToggle;
    
    // --- Элементы UI: Робот ---
    [SerializeField] private Slider robotSpeedSlider;
    [SerializeField] private Toggle collisionToggle;
    [SerializeField] private Slider safetyZoneSlider;
    [SerializeField] private Toggle trajectoryPreviewToggle;
    
    // --- Элементы UI: Графика ---
    [SerializeField] private Dropdown qualityDropdown;
    [SerializeField] private Toggle vSyncToggle;
    [SerializeField] private Slider targetFPSSlider;
    [SerializeField] private Toggle showFPSToggle;
    [SerializeField] private Slider vrFOVSlider;
    
    // --- Элементы UI: Сеть ---
    [SerializeField] private InputField robotIPField;
    [SerializeField] private InputField robotPortField;
    [SerializeField] private Toggle useROSToggle;
    [SerializeField] private InputField rosIPField;
    [SerializeField] private InputField rosPortField;
    
    // --- Элементы UI: Звук ---
    [SerializeField] private Slider masterVolSlider;
    [SerializeField] private Slider sfxVolSlider;
    [SerializeField] private Slider musicVolSlider;
    [SerializeField] private Toggle hapticToggle;
    
    // --- Элементы UI: Общие ---
    [SerializeField] private Dropdown languageDropdown;
    [SerializeField] private InputField savePathField;
    [SerializeField] private Toggle autoSaveToggle;
    [SerializeField] private Toggle tutorialToggle;
    
    private string saveFilePath;
    
    void Start()
    {
        saveFilePath = Path.Combine(Application.persistentDataPath, "settings.json");
        LoadSettings();
        SetupUI();
        ShowTab(0); // Показать первую вкладку
    }
    
    private bool uiInitialized = false;

    void SetupUI()
        {
        if (uiInitialized) return;
        uiInitialized = true;
        
        // Ввод
        inputDeviceDropdown.value = (int)settings.preferredDevice;
        inputDeviceDropdown.onValueChanged.AddListener(v => {
            settings.preferredDevice = (InputDeviceType)v;
            InputManager.Instance?.SetInputDevice((InputManager.InputDevice)v);
        });
        
        mouseSensSlider.value = settings.mouseSensitivity;
        mouseSensSlider.onValueChanged.AddListener(v => settings.mouseSensitivity = v);
        
        gamepadSensSlider.value = settings.gamepadSensitivity;
        gamepadSensSlider.onValueChanged.AddListener(v => settings.gamepadSensitivity = v);
        
        invertYToggle.isOn = settings.invertY;
        invertYToggle.onValueChanged.AddListener(v => settings.invertY = v);
        
        // Робот
        robotSpeedSlider.value = settings.robotSpeed;
        robotSpeedSlider.onValueChanged.AddListener(v => settings.robotSpeed = v);
        
        collisionToggle.isOn = settings.collisionDetection;
        collisionToggle.onValueChanged.AddListener(v => settings.collisionDetection = v);
        
        safetyZoneSlider.value = settings.safetyZoneRadius;
        safetyZoneSlider.onValueChanged.AddListener(v => settings.safetyZoneRadius = v);
        
        trajectoryPreviewToggle.isOn = settings.showTrajectoryPreview;
        trajectoryPreviewToggle.onValueChanged.AddListener(v => settings.showTrajectoryPreview = v);
        
        // Графика
        qualityDropdown.value = settings.qualityLevel;
        qualityDropdown.onValueChanged.AddListener(v => {
            settings.qualityLevel = v;
            QualitySettings.SetQualityLevel(v);
        });
        
        vSyncToggle.isOn = settings.vSync;
        vSyncToggle.onValueChanged.AddListener(v => {
            settings.vSync = v;
            QualitySettings.vSyncCount = v ? 1 : 0;
        });
        
        targetFPSSlider.value = settings.targetFPS;
        targetFPSSlider.onValueChanged.AddListener(v => {
            settings.targetFPS = (int)v;
            Application.targetFrameRate = (int)v;
        });
        
        showFPSToggle.isOn = settings.showFPS;
        showFPSToggle.onValueChanged.AddListener(v => settings.showFPS = v);
        
        vrFOVSlider.value = settings.vrFOV;
        vrFOVSlider.onValueChanged.AddListener(v => settings.vrFOV = (int)v);
        
        // Сеть
        robotIPField.text = settings.robotIP;
        robotIPField.onEndEdit.AddListener(v => settings.robotIP = v);
        
        robotPortField.text = settings.robotPort.ToString();
        robotPortField.onEndEdit.AddListener(v => int.TryParse(v, out settings.robotPort));
        
        useROSToggle.isOn = settings.useROS;
        useROSToggle.onValueChanged.AddListener(v => settings.useROS = v);
        
        rosIPField.text = settings.rosBridgeIP;
        rosIPField.onEndEdit.AddListener(v => settings.rosBridgeIP = v);
        
        rosPortField.text = settings.rosBridgePort.ToString();
        rosPortField.onEndEdit.AddListener(v => int.TryParse(v, out settings.rosBridgePort));
        
        // Звук
        masterVolSlider.value = settings.masterVolume;
        masterVolSlider.onValueChanged.AddListener(v => {
            settings.masterVolume = v;
            AudioListener.volume = v;
        });
        
        sfxVolSlider.value = settings.sfxVolume;
        sfxVolSlider.onValueChanged.AddListener(v => settings.sfxVolume = v);
        
        musicVolSlider.value = settings.musicVolume;
        musicVolSlider.onValueChanged.AddListener(v => settings.musicVolume = v);
        
        hapticToggle.isOn = settings.hapticFeedback;
        hapticToggle.onValueChanged.AddListener(v => settings.hapticFeedback = v);
        
        // Общие
        languageDropdown.value = settings.language == "ru" ? 0 : 1; // 0-ru, 1-en
        languageDropdown.onValueChanged.AddListener(v => settings.language = v == 0 ? "ru" : "en");
        
        savePathField.text = settings.trajectorySavePath;
        savePathField.onEndEdit.AddListener(v => settings.trajectorySavePath = v);
        
        autoSaveToggle.isOn = settings.autoSaveTrajectory;
        autoSaveToggle.onValueChanged.AddListener(v => settings.autoSaveTrajectory = v);
        
        tutorialToggle.isOn = settings.showTutorialOnStart;
        tutorialToggle.onValueChanged.AddListener(v => settings.showTutorialOnStart = v);
        
    }
    
    public void ShowTab(int index)
    {
        for (int i = 0; i < tabContents.Length; i++)
            tabContents[i].SetActive(i == index);
            
        for (int i = 0; i < tabButtons.Length; i++)
        {
            var colors = tabButtons[i].colors;
            colors.normalColor = (i == index) ? new Color(0.2f, 0.6f, 1f) : Color.white;
            tabButtons[i].colors = colors;
        }
    }
    
    public void SaveSettings()
    {
        string json = JsonUtility.ToJson(settings, true);
        File.WriteAllText(saveFilePath, json);
        Debug.Log($"[Settings] Сохранено: {saveFilePath}");
    }
    
    public void LoadSettings()
    {
        if (File.Exists(saveFilePath))
        {
            string json = File.ReadAllText(saveFilePath);
            JsonUtility.FromJsonOverwrite(json, settings);
            Debug.Log("[Settings] Загружено из файла");
        }
        else
        {
            Debug.Log("[Settings] Используются значения по умолчанию");
        }
    }
    
    public void ResetToDefaults()
    {
        // Создаём новый ScriptableObject с дефолтами
        var fresh = ScriptableObject.CreateInstance<SettingsData>();
        JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(fresh), settings);
        SetupUI(); // Переинициализируем UI
        SaveSettings();
    }
    
    public void OnBackButton()
    {
        SaveSettings();
        gameObject.SetActive(false);
        // Показать главное меню
    }
}
