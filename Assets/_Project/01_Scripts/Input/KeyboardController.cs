using UnityEngine;

/// <summary>

/// УСТАРЕЛО (S44, 30.10.2026). Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Ввод текста — KvXrValueField и KvTextInput.

/// Оставлено помеченным, а не удалено: правило проекта — сначала метка с причиной, удаление отдельным решением.

/// </summary>

[System.Obsolete("Мёртвый код: ни где не создаётся и не вызывается (проверено S44 30.10.2026). Ввод текста — KvXrValueField и KvTextInput.")]

public class KeyboardController : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float rotateSpeed = 90f;

    void Update()
    {
        // Получаем вектор движения (WASD или левый стик) из вашей системы ввода
        Vector2 moveInput = InputManager.Instance.Movement; // вместо GetMovement()
        
        // Поворот робота (влево/вправо)
        transform.Rotate(Vector3.up, moveInput.x * rotateSpeed * Time.deltaTime);
        
        // Движение вперед/назад
        transform.Translate(Vector3.forward * moveInput.y * moveSpeed * Time.deltaTime);

    }
}