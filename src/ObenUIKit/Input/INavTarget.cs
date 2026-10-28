using UnityEngine;

namespace ObenUIKit.Input;

/// <summary>
/// A control the keyboard and controller can highlight. It registers itself with its window's
/// <see cref="UiNav"/> when built, so navigation never has to search the hierarchy.
/// </summary>
internal interface INavTarget
{
    RectTransform Rect { get; }

    /// <summary>False while the control is disabled; navigation skips it.</summary>
    bool Focusable { get; }

    /// <summary>Shows or hides the highlight, which is the same look as the mouse hover.</summary>
    void SetFocus(bool on);

    /// <summary>Enter or the pad's A button.</summary>
    void Activate();

    /// <summary>Left or right. Returns false when the control has nothing to step.</summary>
    bool Step(int delta);
}
