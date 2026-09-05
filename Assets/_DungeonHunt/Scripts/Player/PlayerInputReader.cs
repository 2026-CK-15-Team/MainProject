using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInputReader : MonoBehaviour, PlayerControls.IPlayerActions
{
    public Vector2 MoveInput { get; private set; }
    public Vector2 AimScreenPosition { get; private set; }
    public bool IsAttackHeld { get; private set; }

    public event Action DodgePressed;
    public event Action<int> WeaponChangeRequested; // -1 = 이전 무기, +1 = 다음 무기
    private PlayerControls controls;

    private void Awake()
    {
        controls = new PlayerControls();
        controls.Player.SetCallbacks(this);
    }

    private void OnEnable() => controls.Enable();
    private void OnDisable() => controls.Disable();
    

    public void OnMove(InputAction.CallbackContext context)
    {
        MoveInput = context.ReadValue<Vector2>();
    }

    public void OnAimPosition(InputAction.CallbackContext context)
    {
        AimScreenPosition = context.ReadValue<Vector2>();
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed) IsAttackHeld = true;
        if (context.canceled) IsAttackHeld = false;
    }

    public void OnDodge(InputAction.CallbackContext context)
    {
        if (context.performed) DodgePressed?.Invoke();
    }

    public void OnWeaponChange(InputAction.CallbackContext context)
    {
        if (!context.performed) return;
        //Debug.Log($"WeaponChange fired, frame: {Time.frameCount}");
        float value = context.ReadValue<float>();
        int direction = value > 0f ? 1 : -1;
        WeaponChangeRequested?.Invoke(direction);
    }
    
    private void OnDestroy()
    {
        controls.Dispose();
    }
}