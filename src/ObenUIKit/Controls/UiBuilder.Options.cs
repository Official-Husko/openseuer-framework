using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>The game's option controls: a title bar over a "&lt; value &gt;" row or a slider.</summary>
public sealed partial class UiBuilder
{
    private static readonly string[] OffOn = { "Off", "On" };

    /// <summary>Pick one of several options with the arrows, or by clicking the row (next option).</summary>
    public CycleHandle Cycle(string label, string[] options, int index, Action<int> onChange)
    {
        Header(label);
        var kitRow = OptionRow(label);
        var row = kitRow.Rect;
        var value = UiParts.Text(row, Theme, "", Theme.TextSize, Theme.Text);
        value.textWrappingMode = TextWrappingModes.NoWrap;
        UiParts.Stretch(value.rectTransform, 70f, 0f, 70f, 0f);

        CycleHandle handle = null;
        handle = new CycleHandle(value, options, index, i => Guard(label, () => onChange?.Invoke(i)));
        Arrow(row, "<", left: true, () => handle.Step(-1));
        Arrow(row, ">", left: false, () => handle.Step(1));
        Register(kitRow, d => { _kit.Sounds.Press(); handle.Step(d); return true; },
            () => { _kit.Sounds.Press(); handle.Step(1); });
        return handle;
    }

    /// <summary>On or off, shown the game's way as an Off/On cycle.</summary>
    public ToggleHandle Toggle(string label, bool value, Action<bool> onChange) =>
        new ToggleHandle(Cycle(label, OffOn, value ? 1 : 0, i => onChange?.Invoke(i == 1)));

    /// <summary>A number between <paramref name="min"/> and <paramref name="max"/> with its value shown left of the bar.</summary>
    public SliderHandle Slider(string label, float min, float max, float value, Action<float> onChange,
        string format = "0", bool wholeNumbers = false)
    {
        Header(label);
        var kitRow = OptionRow(label);
        var row = kitRow.Rect;
        var readout = UiParts.Text(row, Theme, value.ToString(format), Theme.TextSize, Theme.Text, TextAlignmentOptions.MidlineLeft);
        readout.rectTransform.anchorMin = new Vector2(0f, 0f);
        readout.rectTransform.anchorMax = new Vector2(0.22f, 1f);
        readout.rectTransform.offsetMin = new Vector2(24f, 0f);
        readout.rectTransform.offsetMax = Vector2.zero;

        var slider = SliderParts.Build(row, Theme);
        slider.minValue = min;
        slider.maxValue = max;
        slider.wholeNumbers = wholeNumbers;
        slider.SetValueWithoutNotify(value);
        slider.onValueChanged.AddListener(v =>
        {
            readout.text = v.ToString(format);
            Guard(label, () => onChange?.Invoke(v));
        });
        float step = wholeNumbers ? Mathf.Max(1f, Mathf.Round((max - min) / 50f)) : (max - min) / 20f;
        Register(kitRow, d => { slider.value += d * step; return true; }, null);
        return new SliderHandle(slider);
    }

    private KitRow OptionRow(string name)
    {
        var background = Built(UiParts.Image(_parent, "Row " + name, Theme.RowSprite, Color.clear, raycast: true));
        UiParts.Height(background, RowHeight);
        var row = background.gameObject.AddComponent<KitRow>();
        row.Init(background, Theme);
        return row;
    }

    private KitButton Arrow(RectTransform row, string glyph, bool left, Action step)
    {
        var holder = UiParts.Child(row, glyph);
        holder.anchorMin = new Vector2(left ? 0f : 1f, 0f);
        holder.anchorMax = new Vector2(left ? 0f : 1f, 1f);
        holder.pivot = new Vector2(left ? 0f : 1f, 0.5f);
        holder.sizeDelta = new Vector2(64f, 0f);
        holder.anchoredPosition = Vector2.zero;
        holder.gameObject.AddComponent<Image>().color = Color.clear;

        var text = UiParts.Text(holder, Theme, glyph, Theme.TextSize * 1.15f, Theme.Text);
        if (Theme.ArrowFont != null)
            text.font = Theme.ArrowFont;
        UiParts.Stretch(text.rectTransform);
        var button = holder.gameObject.AddComponent<KitButton>();
        button.Init(text, _kit, step);
        return button;
    }
}
