using UnityEngine;
using UnityEngine.InputSystem;

public class GamepadInputProvider : InputProvider
{
    public Camera playerCamera;

    private Camera ResolveCamera()
    {
        if (playerCamera == null)
            playerCamera = Camera.main != null ? Camera.main : Object.FindAnyObjectByType<Camera>();

        if (playerCamera == null)
            Debug.LogWarning("[GamepadInputProvider] Camera not assigned and no camera found.");

        return playerCamera;
    }

    private Gamepad GetGamepad()
    {
        return Gamepad.current;
    }

    public override Vector3 GetPointerPosition()
    {
        var camera = ResolveCamera();
        return camera != null ? camera.transform.position : Vector3.zero;
    }

    public override Vector3 GetPointerDirection()
    {
        var camera = ResolveCamera();
        return camera != null ? camera.transform.forward : Vector3.forward;
    }

    public override bool GetSelectDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonSouth.wasPressedThisFrame || gamepad.buttonEast.wasPressedThisFrame;
        return Input.GetButtonDown("Fire1");
    }

    public override bool GetSelectHeld()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonSouth.isPressed || gamepad.buttonEast.isPressed;
        return Input.GetButton("Fire1");
    }

    public override bool GetGrabDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonWest.wasPressedThisFrame || gamepad.buttonNorth.wasPressedThisFrame;
        return Input.GetButtonDown("Fire2");
    }

    public override bool GetGrabHeld()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonWest.isPressed || gamepad.buttonNorth.isPressed;
        return Input.GetButton("Fire2");
    }

    public override bool GetSwitchRobotDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.leftShoulder.wasPressedThisFrame;
        // ИСПРАВЛЕНО 11.11.2026: раньше здесь читалась legacy-кнопка «LB» — такого имени НЕТ в
        // ProjectSettings/InputManager.asset (там объявлены только Cancel, Fire1..3, Horizontal,
        // Vertical, Jump, Submit, Mouse *). Unity бросала `ArgumentException: Input Button LB is
        // not setup` КАЖДЫЙ КАДР, а `IsAvailable()` на основании legacy-джойстика выбирала этот
        // провайдер на старте — то есть оператор оставался без управления и с потоком ошибок.
        // `GetKeyDown(KeyCode.JoystickButtonN)` — НЕ требует объявления в Input Manager.
        return Input.GetKeyDown(KeyCode.JoystickButton4);
    }

    public override bool GetRecordDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonWest.wasPressedThisFrame;
        return Input.GetKeyDown(KeyCode.JoystickButton2);
    }

    public override bool GetPlayDown()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.buttonNorth.wasPressedThisFrame;
        return Input.GetKeyDown(KeyCode.JoystickButton3);
    }

    public override Vector2 GetMovement()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.leftStick.ReadValue();

        return new Vector2(Input.GetAxis("Horizontal"), Input.GetAxis("Vertical"));
    }

    public override Vector2 GetRotation()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
            return gamepad.rightStick.ReadValue();

        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));   // осей правого стика в проекте нет — они бросали ArgumentException
    }

    public override bool IsAvailable()
    {
        var gamepad = GetGamepad();
        if (gamepad != null)
        {
            return true;
        }

        // «Есть legacy-джойстик» НЕ делает провайдер рабочим (исправлено 11.11.2026): кнопки и
        // стики читаются через `Gamepad.current`, и если он устройства не видит, действия не
        // сработают вовсе. Раньше доступность подтверждалась по legacy-списку джойстиков, и
        // `InputManager` выбирал на старте провайдер, который ничего не умел — оператор оставался
        // без управления, а в консоль каждый кадр летел ArgumentException.
        return false;
    }
}