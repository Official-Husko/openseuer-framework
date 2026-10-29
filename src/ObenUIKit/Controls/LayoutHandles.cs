using System;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>A number stepper. <see cref="Set"/> changes the value without calling the mod back.</summary>
public sealed class StepperHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly TMP_Text _text;
    private readonly KitButton _down;
    private readonly KitButton _up;
    private readonly string _format;
    private readonly Action<float> _onChange;
    private float _min;
    private float _max;
    private float _step;

    internal StepperHandle(TMP_Text text, KitButton down, KitButton up, float value, float min, float max, float step,
        string format, Action<float> onChange)
    {
        _text = text;
        _down = down;
        _up = up;
        _format = format;
        _onChange = onChange;
        _min = min;
        _max = max;
        _step = step;
        Set(value);
    }

    public float Value { get; private set; }

    public void Set(float value)
    {
        Value = Snap(value);
        Show();
    }

    /// <summary>Changes the bounds and step; the value is pulled inside them.</summary>
    public void SetRange(float min, float max, float step)
    {
        if (max < min || step <= 0f)
            throw new ArgumentException("max must not be below min, and step must be above 0.");
        _min = min;
        _max = max;
        _step = step;
        Set(Value);
    }

    /// <summary>Moves one step; returns false at the end of the range.</summary>
    internal bool Step(int delta)
    {
        float next = Snap(Value + delta * _step);
        if (Mathf.Approximately(next, Value))
            return false;
        Value = next;
        Show();
        _onChange?.Invoke(Value);
        return true;
    }

    private float Snap(float value)
    {
        float steps = Mathf.Round((value - _min) / _step);
        return Mathf.Clamp(_min + steps * _step, _min, _max);
    }

    private void Show()
    {
        if (_text != null)
            _text.text = Value.ToString(_format);
        if (_down != null)
            _down.Interactable = Value > _min + _step * 0.001f;
        if (_up != null)
            _up.Interactable = Value < _max - _step * 0.001f;
    }
}

/// <summary>A card: a framed box with its own controls. Hide it to hide them all.</summary>
public sealed class CardHandle
{
    /// <summary>The control's row (or its whole box), to add uGUI of your own to it or adjust its layout.</summary>
    public RectTransform Rect { get; internal set; }

    private readonly RectTransform _card;
    private readonly TMP_Text _title;

    internal CardHandle(RectTransform card, TMP_Text title)
    {
        _card = card;
        _title = title;
    }

    public string Title
    {
        get => _title != null ? _title.text : "";
        set
        {
            if (_title != null)
                _title.text = value ?? "";
        }
    }

    public bool Visible
    {
        get => _card != null && _card.gameObject.activeSelf;
        set
        {
            if (_card != null && _card.gameObject.activeSelf != value)
                _card.gameObject.SetActive(value);
        }
    }
}
