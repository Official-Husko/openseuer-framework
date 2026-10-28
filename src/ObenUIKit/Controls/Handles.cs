using System;
using TMPro;
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
    private readonly TMP_Text _value;
    private readonly Action<int> _onChange;
    private string[] _options;

    internal CycleHandle(TMP_Text value, string[] options, int index, Action<int> onChange)
    {
        _value = value;
        _options = options;
        _onChange = onChange;
        Set(index);
    }

    public int Index { get; private set; }

    public void Set(int index)
    {
        Index = _options.Length == 0 ? 0 : (index % _options.Length + _options.Length) % _options.Length;
        _value.text = _options.Length == 0 ? "" : _options[Index];
    }

    public void SetOptions(string[] options, int index)
    {
        _options = options;
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

    public bool Value => _cycle.Index == 1;

    public void Set(bool value) => _cycle.Set(value ? 1 : 0);
}

/// <summary>A slider with the game's number readout.</summary>
public sealed class SliderHandle
{
    private readonly Slider _slider;

    internal SliderHandle(Slider slider)
    {
        _slider = slider;
    }

    public float Value => _slider.value;

    public void Set(float value) => _slider.SetValueWithoutNotify(value);
}
