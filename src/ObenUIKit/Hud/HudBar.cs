using System;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit;

/// <summary>
/// A small bar on the HUD that follows a value between 0 and 1. It reads the value a few times a
/// second and glides to it. <see cref="Remove"/> takes it away; it also goes when its mod unloads.
/// </summary>
public sealed class HudBar
{
    private string _label;
    private Color? _color;

    internal HudBar(ITracker owner, string label, Func<float> value)
    {
        Owner = owner;
        _label = label;
        Value = value;
    }

    public bool IsActive { get; internal set; } = true;

    public string Label
    {
        get => _label;
        set
        {
            _label = value;
            if (Text != null)
                Text.text = value;
        }
    }

    /// <summary>The fill colour. Unset, the bar uses the theme's slider colour.</summary>
    public Color? Color
    {
        get => _color;
        set
        {
            _color = value;
            if (Fill != null && value.HasValue)
                Fill.color = value.Value;
        }
    }

    public void Remove() => HudRuntime.Current?.RemoveBar(this);

    internal ITracker Owner { get; }

    internal Func<float> Value { get; }

    // What is shown (lags behind Target while the bar glides) and what the value last read.
    internal float Shown;
    internal float Target;

    internal GameObject Root;
    internal TMP_Text Text;
    internal Image Fill;
    internal RectTransform FillRect;
}
