using UnityEngine;
using UInput = UnityEngine.Input;

namespace ObenUIKit.Input;

/// <summary>
/// What the keyboard and the pad mean in a menu. The arrow keys and WASD move, Enter and the pad's
/// A activate. The pad axes are the ones the game's own menus read.
/// </summary>
internal static class NavKeys
{
    private const float StickThreshold = 0.5f;

    private static bool _padBroken;

    /// <summary>-1 is up, 1 is down, 0 is neither.</summary>
    public static int Vertical()
    {
        if (UInput.GetKey(KeyCode.UpArrow) || UInput.GetKey(KeyCode.W))
            return -1;
        if (UInput.GetKey(KeyCode.DownArrow) || UInput.GetKey(KeyCode.S))
            return 1;
        float pad = Axis("DPadVertical");
        if (pad == 0f)
            pad = Axis("LeftStickInventoryVertical");
        return pad > StickThreshold ? -1 : pad < -StickThreshold ? 1 : 0;
    }

    /// <summary>-1 is left, 1 is right, 0 is neither.</summary>
    public static int Horizontal()
    {
        if (UInput.GetKey(KeyCode.LeftArrow) || UInput.GetKey(KeyCode.A))
            return -1;
        if (UInput.GetKey(KeyCode.RightArrow) || UInput.GetKey(KeyCode.D))
            return 1;
        float pad = Axis("DPadHorizontal");
        if (pad == 0f)
            pad = Axis("LeftStickInventoryHorizontal");
        return pad > StickThreshold ? 1 : pad < -StickThreshold ? -1 : 0;
    }

    public static bool Confirm() =>
        UInput.GetKeyDown(KeyCode.Return) || UInput.GetKeyDown(KeyCode.KeypadEnter) || UInput.GetKeyDown(KeyCode.Joystick1Button0);

    /// <summary>Esc or the pad's B button.</summary>
    public static bool Back() => UInput.GetKeyDown(KeyCode.Escape) || UInput.GetKeyDown(KeyCode.Joystick1Button1);

    // An axis the project's input settings don't define throws; give up on the pad once instead of every frame.
    private static float Axis(string name)
    {
        if (_padBroken)
            return 0f;
        try { return UInput.GetAxisRaw(name); }
        catch (System.ArgumentException)
        {
            _padBroken = true;
            return 0f;
        }
    }
}
