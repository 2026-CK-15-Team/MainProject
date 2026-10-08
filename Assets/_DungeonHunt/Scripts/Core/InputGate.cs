using UnityEngine;
using UnityEngine.InputSystem;

// 같은 틱 입력 우선순위(01-1 §7.2)용 공용 게이트.
// UI가 이번 프레임에 입력을 소비하면 게임 행동 입력은 버림. 모달이 열려 있으면 게임 입력 차단.
public static class InputGate
{
    private static int consumedFrame = -1;

    // UI 쪽(UIManager)이 매 프레임 갱신
    public static bool GameplayBlocked { get; set; }

    public static bool ConsumedThisFrame => consumedFrame == Time.frameCount;

    public static void ConsumeFrame() => consumedFrame = Time.frameCount;

    public static bool KeyDown(Key key)
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null) return false;
        return key switch
        {
            Key.Escape => keyboard.escapeKey.wasPressedThisFrame,
            Key.Tab => keyboard.tabKey.wasPressedThisFrame,
            Key.I => keyboard.iKey.wasPressedThisFrame,
            Key.F => keyboard.fKey.wasPressedThisFrame,
            Key.R => keyboard.rKey.wasPressedThisFrame,
            _ => false,
        };
    }

    public static bool LeftMouseHeld => Mouse.current != null && Mouse.current.leftButton.isPressed;
    public static bool LeftMouseDown => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;
    public static Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
}
