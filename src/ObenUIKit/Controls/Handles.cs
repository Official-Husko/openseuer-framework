using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>A text on a window that can be changed later.</summary>
public sealed class LabelHandle
{
    private readonly TMP_Text _text;

    internal LabelHandle(TMP_Text text)
    {
        _text = text;
    }

    /// <summary>The label's own rect.</summary>
    public RectTransform Rect => _text.rectTransform;

    /// <summary>
    /// The text color, given the usual way (as an image editor shows it); it is drawn to match, even though the
    /// game renders in linear space.
    /// </summary>
    public Color Color
    {
        get => _text.color.gamma;
        set => _text.color = value.linear;
    }

    /// <summary>The font size (the kit's text is 24, a header 26).</summary>
    public float Size
    {
        get => _text.fontSize;
        set => _text.fontSize = value;
    }

    public string Text
    {
        get => _text.text;
        set
        {
            if (_text != null && _text.text != value)
                _text.text = value;
        }
    }
}

/// <summary>A button that can be enabled, disabled or relabelled later.</summary>
public sealed class ButtonHandle
{
    private readonly KitButton _button;
    private readonly TMP_Text _text;

    internal ButtonHandle(KitButton button, TMP_Text text)
    {
        _button = button;
        _text = text;
    }

    /// <summary>The button's rect.</summary>
    public RectTransform Rect => _button.Rect;

    public bool Enabled
    {
        get => _button.Interactable;
        set => _button.Interactable = value;
    }

    public string Text
    {
        get => _text.text;
        set => _text.text = value;
    }
}

/// <summary>
/// A "&lt; value &gt;" row: the game's way of picking one of several options. <see cref="Set"/>
/// changes it without calling the mod back.
/// </summary>
public class CycleHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly TMP_Text _value;
    private readonly Action<int> _onChange;
    private string[] _options;

    internal CycleHandle(TMP_Text value, string[] options, int index, Action<int> onChange)
    {
        _value = value;
        _options = options ?? Array.Empty<string>();
        _onChange = onChange;
        Set(index);
    }

    public int Index { get; private set; }

    public void Set(int index)
    {
        Index = _options.Length == 0 ? 0 : (index % _options.Length + _options.Length) % _options.Length;
        _value.text = _options.Length == 0 ? "" : _options[Index];
    }

    /// <summary>Replaces the options (null is none) and shows <paramref name="index"/>, without calling the mod back.</summary>
    public void SetOptions(string[] options, int index)
    {
        _options = options ?? Array.Empty<string>();
        Set(index);
    }

    internal void Step(int delta)
    {
        Set(Index + delta);
        _onChange?.Invoke(Index);
    }
}

/// <summary>An on/off row, shown the game's way as a cycle between Off and On.</summary>
public sealed class ToggleHandle
{
    private readonly CycleHandle _cycle;

    internal ToggleHandle(CycleHandle cycle)
    {
        _cycle = cycle;
    }

    /// <summary>The toggle's row.</summary>
    public RectTransform Rect => _cycle.Rect;

    public bool Value => _cycle.Index == 1;

    public void Set(bool value) => _cycle.Set(value ? 1 : 0);
}

/// <summary>A slider with the game's number readout.</summary>
public sealed class SliderHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly Slider _slider;
    private readonly TMP_Text _readout;
    private readonly string _format;

    internal SliderHandle(Slider slider, TMP_Text readout, string format)
    {
        _slider = slider;
        _readout = readout;
        _format = format;
    }

    public float Value => _slider.value;

    /// <summary>Moves the slider and its readout without calling the mod back.</summary>
    public void Set(float value)
    {
        _slider.SetValueWithoutNotify(value);
        _readout.text = KitText.Number(_slider.value, _format);
    }
}
