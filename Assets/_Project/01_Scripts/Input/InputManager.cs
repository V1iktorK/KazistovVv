using UnityEngine;
using UnityEngine.InputSystem;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance;

    [SerializeField] private InputProvider vrProvider;
    [SerializeField] private InputProvider keyboardMouseProvider;
    [SerializeField] private InputProvider gamepadProvider;
    [SerializeField] private InputProvider mrProvider;
    [SerializeField] private InputProvider arProvider;

    private InputProvider activeProvider;

    public enum InputDevice { KeyboardMouse, VR, Gamepad, MR, AR }

    public InputProvider ActiveProvider
    {
        get { return activeProvider; }
    }

    private void EnsureProvidersExist()
    {
        if (keyboardMouseProvider == null)
        {
            GameObject go = new GameObject("KeyboardMouseInputProvider");
            go.transform.SetParent(transform);
            keyboardMouseProvider = go.AddComponent<KeyboardMouseInputProvider>();
        }

        if (gamepadProvider == null)
        {
            GameObject go = new GameObject("GamepadInputProvider");
            go.transform.SetParent(transform);
            gamepadProvider = go.AddComponent<GamepadInputProvider>();
        }

        if (vrProvider == null)
        {
            GameObject go = new GameObject("VRInputProvider");
            go.transform.SetParent(transform);
            vrProvider = go.AddComponent<VRInputProvider>();
        }

        if (mrProvider == null)
        {
            GameObject go = new GameObject("MRInputProvider");
            go.transform.SetParent(transform);
            mrProvider = go.AddComponent<MRInputProvider>();
        }

        if (arProvider == null)
        {
            GameObject go = new GameObject("ARInputProvider");
            go.transform.SetParent(transform);
            arProvider = go.AddComponent<ARInputProvider>();
        }
    }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// P3-10 (11.11.2026). ЧТО БЫЛО. Статическая ссылка `Instance` не обнулялась НИКОГДА:
    /// объекта в сцене уже нет, а `InputManager.Instance` продолжает отдавать уничтоженный
    /// MonoBehaviour. Объект при этом под `DontDestroyOnLoad`, то есть переживает смену сцен.
    /// ЧЕМ ЭТО ГРОЗИЛО. «Фальшиво-null» объект: `Instance != null` истинно, а любое обращение к
    /// его полям бросает `MissingReferenceException`. Проверить такое место взглядом нельзя — и
    /// ровно на этом основании ввод «то работает, то нет» после перезагрузки сцены.
    /// ТЕПЕРЬ ссылка сбрасывается тем же объектом, который её поставил (как в `RobotSelector`,
    /// P1-15): «Instance != null» снова означает «живой экземпляр».
    /// </summary>
    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        EnsureProvidersExist();
        LoadPreferredDevice();
    }

    void Update()
    {
        if (Keyboard.current != null)
        {
            if (Keyboard.current.f1Key.wasPressedThisFrame)
            {
                SetInputDevice(InputDevice.KeyboardMouse);
            }
            if (Keyboard.current.f2Key.wasPressedThisFrame)
            {
                SetInputDevice(InputDevice.VR);
            }
                    if (Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetInputDevice(InputDevice.Gamepad);
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.F1)) SetInputDevice(InputDevice.KeyboardMouse);
            if (Input.GetKeyDown(KeyCode.F2)) SetInputDevice(InputDevice.VR);
            if (Input.GetKeyDown(KeyCode.F3)) SetInputDevice(InputDevice.Gamepad);
        }
    }

    /// <summary>
    /// Провайдер, НАЗНАЧЕННЫЙ устройству (без проверки доступности — её делает `SetInputDevice`).
    /// </summary>
    private InputProvider ProviderFor(InputDevice device)
    {
        switch (device)
        {
            case InputDevice.VR: return vrProvider;
            case InputDevice.Gamepad: return gamepadProvider;
            case InputDevice.MR: return mrProvider;
            case InputDevice.AR: return arProvider;
            case InputDevice.KeyboardMouse: return keyboardMouseProvider;
            default: return keyboardMouseProvider;
        }
    }

    /// <summary>
    /// P3-10 (11.11.2026). ЧТО БЫЛО. Устройство выбиралось БЕЗ проверки доступности: для
    /// `InputDevice.MR` выставлялся `mrProvider`, который возвращает `false`/`zero` во всех
    /// действиях и честно сообщает `IsAvailable() == false`; для AR — провайдер, у которого
    /// «доступен» означало просто «платформа Android». Выбранное устройство при этом ещё и
    /// записывалось в `PlayerPrefs`, то есть нерабочий выбор закреплялся на следующие запуски.
    /// ЧЕМ ЭТО ГРОЗИЛО. Нажатие «MR» (или AR на шлеме PICO) оставляло приложение БЕЗ ВВОДА: ни
    /// осмотра, ни выбора, ни телеоперации — и без единой ошибки в консоли, потому что провайдер
    /// формально существует. Оператор видел «ничего не работает» и не мог понять причину.
    /// ТЕПЕРЬ: провайдер с `IsAvailable() == false` НЕ выбирается. Текущий рабочий провайдер
    /// сохраняется, а если выбранного ещё нет (старт) — берётся клавиатура с мышью: без ввода
    /// приложение бесполезно, и «пусть будет хоть что-то работающее» здесь честнее пустоты.
    /// Нерабочий выбор в `PlayerPrefs` НЕ записывается: сохранять нерабочее — то же обещание.
    /// </summary>
    public void SetInputDevice(InputDevice device)
    {
        InputProvider wanted = ProviderFor(device);
        if (wanted == null)
        {
            Debug.LogError("[InputManager] No valid provider selected.");
            return;
        }

        if (!wanted.IsAvailable())
        {
            Debug.LogWarning("[InputManager] устройство «" + device + "» не выбрано: провайдер " +
                             wanted.GetType().Name + ".IsAvailable() = false (нет оборудования/" +
                             "подсистемы). Текущий ввод: " +
                             (activeProvider != null ? activeProvider.GetType().Name : "нет") +
                             ". Настройка не сохранена, чтобы не закреплять нерабочий выбор.");
            if (activeProvider == null && keyboardMouseProvider != null &&
                keyboardMouseProvider != wanted && keyboardMouseProvider.IsAvailable())
            {
                ApplyProvider(keyboardMouseProvider, InputDevice.KeyboardMouse);
            }
            return;
        }

        ApplyProvider(wanted, device);
    }

    /// <summary>Включить провайдер и запомнить выбор (единственное место записи `PlayerPrefs`).</summary>
    private void ApplyProvider(InputProvider provider, InputDevice device)
    {
        if (activeProvider != null && activeProvider != provider)
            activeProvider.enabled = false;

        activeProvider = provider;
        activeProvider.enabled = true;
        PlayerPrefs.SetInt("InputDevice", (int)device);
        PlayerPrefs.Save();
    }

    void LoadPreferredDevice()
    {
        int saved = PlayerPrefs.GetInt("InputDevice", -1);
        if (saved != -1)
        {
            SetInputDevice((InputDevice)saved);
            return;
        }

        if (vrProvider != null && vrProvider.IsAvailable())
        {
            SetInputDevice(InputDevice.VR);
        }
        else if (gamepadProvider != null && gamepadProvider.IsAvailable())
        {
            SetInputDevice(InputDevice.Gamepad);
        }
        else
        {
            SetInputDevice(InputDevice.KeyboardMouse);
        }
    }

    // --- Прокси-свойства активного провайдера ввода ---

    public Vector3 PointerPosition
    {
        get { return activeProvider != null ? activeProvider.GetPointerPosition() : Vector3.zero; }
    }

    public Vector3 PointerDirection
    {
        get { return activeProvider != null ? activeProvider.GetPointerDirection() : Vector3.forward; }
    }

    public bool SelectDown
    {
        get { return activeProvider != null && activeProvider.GetSelectDown(); }
    }

    public bool SelectHeld
    {
        get { return activeProvider != null && activeProvider.GetSelectHeld(); }
    }

    public bool GrabDown
    {
        get { return activeProvider != null && activeProvider.GetGrabDown(); }
    }

    public bool GrabHeld
    {
        get { return activeProvider != null && activeProvider.GetGrabHeld(); }
    }

    public bool SwitchRobotDown
    {
        get { return activeProvider != null && activeProvider.GetSwitchRobotDown(); }
    }

    public bool RecordDown
    {
        get { return activeProvider != null && activeProvider.GetRecordDown(); }
    }

    public bool PlayDown
    {
        get { return activeProvider != null && activeProvider.GetPlayDown(); }
    }

    public Vector2 Movement
    {
        get { return activeProvider != null ? activeProvider.GetMovement() : Vector2.zero; }
    }

    public Vector2 Rotation
    {
        get { return activeProvider != null ? activeProvider.GetRotation() : Vector2.zero; }
    }
}