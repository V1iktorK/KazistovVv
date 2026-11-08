using UnityEngine;

[CreateAssetMenu(fileName = "SettingsData", menuName = "VR Robot/Settings")]
public class SettingsData : ScriptableObject
{
    public static SettingsData Instance;
    
    [Header("Ввод")]
    public InputDeviceType preferredDevice = InputDeviceType.KeyboardMouse;
    [Range(0.1f, 5f)] public float mouseSensitivity = 1f;
    [Range(0.1f, 5f)] public float gamepadSensitivity = 1f;
    public bool invertY = false;
    
    [Header("Робот")]
    [Range(0.1f, 5f)] public float robotSpeed = 1f;
    public bool collisionDetection = true;
    public float safetyZoneRadius = 0.5f;
    public bool showTrajectoryPreview = true;
    
    [Header("Графика")]
    public int qualityLevel = 2;
    public bool vSync = true;
    [Range(30, 144)] public int targetFPS = 60;
    public bool showFPS = false;
    [Range(60, 120)] public int vrFOV = 90;
    
    [Header("Сеть / Industry 4.0")]
    public string robotIP = "192.168.1.100";
    public int robotPort = 5007;
    public bool useROS = false;
    public string rosBridgeIP = "localhost";
    public int rosBridgePort = 9090;
    public bool cloudSync = false;
    
    [Header("Звук")]
    [Range(0f, 1f)] public float masterVolume = 1f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    public bool hapticFeedback = true;
    
    [Header("MR / AR")]
    [Range(0f, 1f)] public float passthroughOpacity = 0.8f;
    public bool useHandTracking = true;
    public bool useSpatialAnchors = true;
    public bool showRealRobotOverlay = true;
    public bool autoCalibrate = false;
    public string calibrationProfile = "";
    
    [Header("Industry 5.0 - Human-Centric")]
    public bool enableBiometry = false;
    public float fatigueThreshold = 300f;
    public bool adaptiveUI = true;
    public bool ecoMode = false;
    
    [Header("Industry 6.0 - AI / Neuro")]
    public bool enableVoiceCommands = false;
    public bool enableEyeTracking = false;
    public bool aiTrajectoryAssist = false;
    
    [Header("Industry 7.0 - Swarm")]
    public bool enableSwarm = false;
    public int swarmSize = 3;
    
    [Header("Общие")]
    public string language = "ru";
    public string trajectorySavePath = "";
    public bool autoSaveTrajectory = false;
    public bool showTutorialOnStart = true;
    
    void OnEnable()
{
    // Защита от перезаписи временными объектами
    if (Instance == null || Instance == this)
        Instance = this;
}

    /// <summary>
    /// СКОРОСТЬ РОБОТА ДЛЯ КОНТУРА УПРАВЛЕНИЯ (P1-8, 11.11.2026).
    ///
    /// ЧТО БЫЛО. `SixAxisController` и `SCARAController` писали
    /// `SettingsData.Instance != null ? Instance.robotSpeed : 1f` — при отсутствии ассета скорость
    /// МОЛЧА становилась 1.0. Оператор выставлял 0.2, ассет не подхватился (или `Instance` уже
    /// перехвачен временным объектом из `ResetToDefaults`) — и робот ехал В ПЯТЬ РАЗ БЫСТРЕЕ
    /// заданного, без единой строки в журнале.
    ///
    /// ЧЕМ ЭТО ГРОЗИЛО. Это ровно тот класс, где «тихо взять по умолчанию» опаснее отказа: предел
    /// скорости — часть безопасности, и подмена его вверх никем не замечается.
    ///
    /// ТЕПЕРЬ: отсутствие настроек ВИДНО (предупреждение один раз на сессию), а значение берётся
    /// из одного места — этого метода, чтобы обе модели роботов не расходились.
    /// </summary>
    public static float RobotSpeedOrWarn()
    {
        SettingsData data = Instance;
        if (data != null) return data.robotSpeed;
        if (!warnedAboutMissingSettings)
        {
            warnedAboutMissingSettings = true;
            Debug.LogWarning("[Settings] ассет SettingsData не найден — скорость робота взята 1.0 " +
                             "по умолчанию. Если оператор задавал другую, она НЕ применена (P1-8)");
        }
        return 1f;
    }

    private static bool warnedAboutMissingSettings;

    /// <summary>СБРОС ПРЕДУПРЕЖДЕНИЯ (для тестов и повторной выдачи после смены сцены).</summary>
    public static void ResetMissingSettingsWarning()
    {
        warnedAboutMissingSettings = false;
    }
}
