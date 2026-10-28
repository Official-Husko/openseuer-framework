using System;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// Describes a public field of a settings class for the Settings page. Every part is optional: a field
/// without the attribute still gets a control, labelled with its name.
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class SettingAttribute : Attribute
{
    public SettingAttribute(string label = null)
    {
        Label = label;
    }

    /// <summary>Gives a number field the bounds that turn it into a slider.</summary>
    public SettingAttribute(float min, float max)
    {
        Min = min;
        Max = max;
    }

    /// <summary>Shown instead of the prettified field name.</summary>
    public string Label { get; }

    /// <summary>A line of help under the control.</summary>
    public string Description { get; set; }

    /// <summary>Longer help shown as a tooltip when the mouse rests on the control (or the keyboard highlights it).</summary>
    public string Tooltip { get; set; }

    /// <summary>Lowest value of a slider. A slider needs both <see cref="Min"/> and <see cref="Max"/>.</summary>
    public float Min { get; set; } = float.NaN;

    /// <summary>Highest value of a slider.</summary>
    public float Max { get; set; } = float.NaN;

    /// <summary>
    /// For decimal numbers with a slider: values are rounded to this step (0.05 keeps two decimals).
    /// Zero picks a step from the size of the range. Whole numbers always move by one.
    /// </summary>
    public float Step { get; set; }

    /// <summary>Keeps the field off the page (bookkeeping the player shouldn't touch).</summary>
    public bool Hidden { get; set; }

    internal bool HasRange => !float.IsNaN(Min) && !float.IsNaN(Max) && Max > Min;
}
