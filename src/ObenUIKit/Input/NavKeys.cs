using ObenFramework.Ui;
using UnityEngine;
using UInput = UnityEngine.Input;

namespace ObenUIKit.Input;

/// <summary>
/// What the keyboard and the pad mean in a menu. The arrow keys and WASD move, Enter and the pad's
/// A activate, the game's tab keys and the bumpers turn pages. The pad axes are the ones the game's own menus read.
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

    /// <summary>Backspace, Delete or the pad's X: put the highlighted setting back to its default.</summary>
    public static bool Reset() =>
        UInput.GetKeyDown(KeyCode.Backspace) || UInput.GetKeyDown(KeyCode.Delete) || UInput.GetKeyDown(KeyCode.Joystick1Button2);

    /// <summary>
    /// -1 for the previous page of a window, 1 for the next, 0 for neither: Page Up and Page Down, the keys the
    /// game switches its own menu tabs with (Q by default), and the pad's bumpers.
    /// </summary>
    public static int Page()
    {
        if (UInput.GetKeyDown(KeyCode.PageUp) || TabKey("Switch Tab Left", KeyCode.Joystick1Button4))
            return -1;
        if (UInput.GetKeyDown(KeyCode.PageDown) || TabKey("Switch Tab Right", KeyCode.Joystick1Button5))
            return 1;
        return 0;
    }

    // The game's binding for a tab action, unless it is one of the keys that already move around a window.
    private static bool TabKey(string action, KeyCode bumper)
    {
        var key = GameKeys.Key(action);
        if (key != KeyCode.None && !Navigates(key) && UInput.GetKeyDown(key))
            return true;
        var button = GameKeys.Button(action);
        return UInput.GetKeyDown(button != KeyCode.None ? button : bumper);
    }

    private static bool Navigates(KeyCode key) => key switch
    {
        KeyCode.UpArrow or KeyCode.DownArrow or KeyCode.LeftArrow or KeyCode.RightArrow => true,
        KeyCode.W or KeyCode.A or KeyCode.S or KeyCode.D => true,
        KeyCode.Return or KeyCode.KeypadEnter or KeyCode.Escape or KeyCode.Backspace or KeyCode.Delete => true,
        _ => false,
    };

    /// <summary>The page keys as a window's key hint shows them: "PgUp / PgDn, Q, LB / RB".</summary>
    public static string PageKeysLabel()
    {
        string label = "PgUp / PgDn";
        var left = GameKeys.Key("Switch Tab Left");
        var right = GameKeys.Key("Switch Tab Right");
        bool hasLeft = left != KeyCode.None && !Navigates(left);
        bool hasRight = right != KeyCode.None && !Navigates(right);
        if (hasLeft && hasRight && left != right)
            label += $", {KeyName(left)} / {KeyName(right)}";
        else if (hasLeft || hasRight)
            label += ", " + KeyName(hasRight ? right : left);
        return label + ", LB / RB";
    }

    private static string KeyName(KeyCode key)
    {
        string name = key.ToString();
        return name.StartsWith("Alpha", System.StringComparison.Ordinal) ? name.Substring(5) : name;
    }

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
