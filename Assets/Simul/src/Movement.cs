using UnityEngine;
using UnityEngine.InputSystem;

public class Movement : MonoBehaviour
{
    [Header("Настройки движения")]
    public float moveSpeed = 10f;
    public float lookSensitivity = 0.1f;

    private Vector2 rotation;

    void Start()
    {
        // Скрываем курсор
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        HandleMovement();
        HandleRotation();
    }

    void HandleMovement()
    {
        // Получаем доступ к клавиатуре
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        Vector3 direction = Vector3.zero;

        // WASD - Движение в плоскости
        if (keyboard.wKey.isPressed) direction += transform.forward;
        if (keyboard.sKey.isPressed) direction -= transform.forward;
        if (keyboard.aKey.isPressed) direction -= transform.right;
        if (keyboard.dKey.isPressed) direction += transform.right;

        // QE - Вверх и вниз
        if (keyboard.eKey.isPressed) direction += transform.up;
        if (keyboard.qKey.isPressed) direction -= transform.up;

        // Применяем перемещение
        transform.position += direction.normalized * (moveSpeed * Time.deltaTime);
    }

    void HandleRotation()
    {
        // Получаем данные мыши
        var mouse = Mouse.current;
        if (mouse == null) return;

        Vector2 mouseDelta = mouse.delta.ReadValue();

        rotation.x += mouseDelta.x * lookSensitivity;
        rotation.y -= mouseDelta.y * lookSensitivity;

        // Ограничиваем вертикальный обзор
        rotation.y = Mathf.Clamp(rotation.y, -90f, 90f);

        transform.localRotation = Quaternion.Euler(rotation.y, rotation.x, 0);
    }
}
