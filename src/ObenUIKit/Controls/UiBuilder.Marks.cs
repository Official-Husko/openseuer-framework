using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Extras the Settings page hangs on controls built by the public methods.</summary>
public sealed partial class UiBuilder
{
    private RectTransform _lastHeader;
    private KitRow _lastRow;

    /// <summary>The title bar of the option control built last.</summary>
    internal RectTransform LastHeader => _lastHeader;

    /// <summary>Small grey text of a fixed width that wraps, for the key hints in a window's footer.</summary>
    internal LabelHandle Hint(string text, float width)
    {
        var label = Built(UiParts.Text(_parent, Theme, text, Theme.TextSize * 0.7f, Theme.TextMuted, TextAlignmentOptions.MidlineLeft));
        UiParts.Width(label, width);
        return new LabelHandle(label);
    }

    /// <summary>
    /// A small underlined Reset at the right end of <paramref name="bar"/>, shown while <paramref name="changed"/>
    /// is true. Hovering it shows <paramref name="tip"/>. The option row built last also resets on Backspace,
    /// Delete or the pad's X while it is highlighted.
    /// </summary>
    internal ResetMark ResetMark(RectTransform bar, string tip, Func<bool> changed, Action reset)
    {
        if (bar == null)
            return null;
        var holder = UiParts.Child(bar, "Reset");
        holder.anchorMin = new Vector2(1f, 0f);
        holder.anchorMax = new Vector2(1f, 1f);
        holder.pivot = new Vector2(1f, 0.5f);
        var label = UiParts.Text(holder, Theme, "Reset", Theme.TextSize * 0.85f, Theme.Text);
        label.fontStyle = FontStyles.Underline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        UiParts.Stretch(label.rectTransform);
        holder.sizeDelta = new Vector2(label.preferredWidth + 24f, 0f);
        holder.anchoredPosition = new Vector2(-6f, 0f);
        holder.gameObject.AddComponent<Image>().color = Color.clear;

        var mark = new ResetMark(holder.gameObject, changed, () => Guard("Reset", reset));
        holder.gameObject.AddComponent<KitButton>().Init(label, _kit, () => mark.Run());
        if (!string.IsNullOrEmpty(tip))
            holder.gameObject.AddComponent<KitTooltip>().Init(_kit.Tooltips, tip);
        _lastRow?.SetReset(mark.Run);
        mark.Refresh();
        return mark;
    }
}

/// <summary>The Reset of one setting: shown while the setting differs from its default.</summary>
internal sealed class ResetMark
{
    private readonly GameObject _button;
    private readonly Func<bool> _changed;
    private readonly Action _reset;

    internal ResetMark(GameObject button, Func<bool> changed, Action reset)
    {
        _button = button;
        _changed = changed;
        _reset = reset;
    }

    /// <summary>Shows or hides the button after the value changed.</summary>
    public void Refresh()
    {
        if (_button != null)
            _button.SetActive(_changed());
    }

    /// <summary>Resets when there is something to reset. False when the value already is the default.</summary>
    public bool Run()
    {
        if (!_changed())
            return false;
        _reset();
        Refresh();
        return true;
    }
}
