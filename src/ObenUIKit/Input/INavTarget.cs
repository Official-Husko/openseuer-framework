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

/// <summary>
/// A control with rows of its own (a long list): up and down move inside it before they move on to the
/// next control.
/// </summary>
internal interface INavVertical
{
    /// <summary>Up (-1) or down (1). Returns false at the first or last row, so navigation leaves the control.</summary>
    bool StepVertical(int delta);

    /// <summary>The highlight just arrived from above (1) or below (-1).</summary>
    void Enter(int direction);
}
