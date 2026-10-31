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

    /// <summary>The line over the bar; empty or null leaves the line out.</summary>
    public string Label
    {
        get => _label;
        set
        {
            _label = value ?? "";
            if (Text != null)
                ShowLabel();
        }
    }

    /// <summary>The fill colour. Null puts the theme's slider colour back.</summary>
    public Color? Color
    {
        get => _color;
        set
        {
            _color = value;
            var theme = HudRuntime.Current?.Theme;
            if (Fill != null && (value.HasValue || theme != null))
                Fill.color = value ?? theme.SliderFill;
        }
    }

    public void Remove() => HudRuntime.Current?.RemoveBar(this);

    internal ITracker Owner { get; }

    internal Func<float> Value { get; }

    // What is shown (lags behind Target while the bar glides) and what the value last read.
    internal float Shown;
    internal float Target;

    internal void ShowLabel()
    {
        Text.text = _label;
        Text.gameObject.SetActive(!string.IsNullOrEmpty(_label));
    }

    internal GameObject Root;
    internal TMP_Text Text;
    internal Image Fill;
    internal RectTransform FillRect;
}
