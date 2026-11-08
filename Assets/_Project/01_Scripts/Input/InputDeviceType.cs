using UnityEngine;

/// <summary>
/// Отдельный enum для устройства ввода (не зависит от InputManager MonoBehaviour).
/// Используется SettingsData и InputManager.
///
/// P3-9 (11.11.2026). ЧТО БЫЛО. Рядом жили ДВА перечисления одного понятия —
/// `InputDeviceType` здесь и `InputManager.InputDevice` в `InputManager.cs`, — а соответствие
/// между ними держалось на СОВПАДЕНИИ ЧИСЕЛ: конвертеры `ToInputManagerDevice`/`ToInputDeviceType`
/// не вызывались ни разу, а `UI/SettingsMenu.cs` делал сырой каст `(InputManager.InputDevice)v`.
/// ЧЕМ ЭТО ГРОЗИЛО. Стоило переставить или вставить элемент в любое из перечислений — и
/// «Геймпад» в выпадающем списке включал бы MR-провайдер: ошибка без исключения, на глаз не
/// видна, а оператор оставался без управления.
/// ТЕПЕРЬ числа НЕ ЗАДАЮТСЯ ВРУЧНУЮ: каждое значение взято ИЗ `InputManager.InputDevice`
/// (единственный источник), поэтому перестановка там автоматически переставляет и здесь.
/// </summary>
public enum InputDeviceType
{
    KeyboardMouse = (int)InputManager.InputDevice.KeyboardMouse,
    VR = (int)InputManager.InputDevice.VR,
    Gamepad = (int)InputManager.InputDevice.Gamepad,
    MR = (int)InputManager.InputDevice.MR,
    AR = (int)InputManager.InputDevice.AR
}

/// <summary>
/// Конвертация между InputDeviceType и InputManager.InputDevice.
///
/// P3-9 (11.11.2026). ЧТО БЫЛО. Оба метода были обычным приведением `(InputManager.InputDevice)
/// type` / `(InputDeviceType)type` — то есть обещанием «числа совпадают», а не проверкой этого.
/// Их к тому же не вызывал никто: вызовы шли сырым кастом в обход конвертеров.
/// ЧЕМ ЭТО ГРОЗИЛО. Тот же тихий сдвиг значений: каст молчит, а действие уходит не тому
/// провайдеру ввода (см. шапку `InputDeviceType`).
/// ТЕПЕРЬ конвертер — ЯВНОЕ соответствие по именам (switch), и он же используется в
/// `UI/SettingsMenu.cs`. Неизвестное значение больше не превращается в «первое попавшееся»:
/// возвращается `KeyboardMouse`, потому что клавиатура с мышью доступны всегда.
/// </summary>
public static class InputDeviceExtensions
{
    /// <summary>Устройство для `InputManager` по значению `InputDeviceType`.</summary>
    public static InputManager.InputDevice ToInputManagerDevice(this InputDeviceType type)
    {
        switch (type)
        {
            case InputDeviceType.VR: return InputManager.InputDevice.VR;
            case InputDeviceType.Gamepad: return InputManager.InputDevice.Gamepad;
            case InputDeviceType.MR: return InputManager.InputDevice.MR;
            case InputDeviceType.AR: return InputManager.InputDevice.AR;
            case InputDeviceType.KeyboardMouse: return InputManager.InputDevice.KeyboardMouse;
            default: return InputManager.InputDevice.KeyboardMouse;
        }
    }

    /// <summary>Значение `InputDeviceType` по устройству `InputManager`.</summary>
    public static InputDeviceType ToInputDeviceType(this InputManager.InputDevice type)
    {
        switch (type)
        {
            case InputManager.InputDevice.VR: return InputDeviceType.VR;
            case InputManager.InputDevice.Gamepad: return InputDeviceType.Gamepad;
            case InputManager.InputDevice.MR: return InputDeviceType.MR;
            case InputManager.InputDevice.AR: return InputDeviceType.AR;
            case InputManager.InputDevice.KeyboardMouse: return InputDeviceType.KeyboardMouse;
            default: return InputDeviceType.KeyboardMouse;
        }
    }
}
