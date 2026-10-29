using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Titles, raw areas, scroll areas of their own, and buttons and Resets on a control's title bar.</summary>
public sealed partial class UiBuilder
{
    /// <summary>A large line of text on the left, for a section of a long page: bigger than a header, without its bar.</summary>
    public LabelHandle Title(string text)
    {
        var label = Built(UiParts.Text(_parent, Theme, text, Theme.TitleSize * 0.9f, Theme.Text, TextAlignmentOptions.MidlineLeft));
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.overflowMode = TextOverflowModes.Ellipsis;
        UiParts.Height(label, RowHeight);
        return new LabelHandle(label);
    }

    /// <summary>
    /// An empty area <paramref name="height"/> tall in the flow, for uGUI of your own (an image, a control the kit
    /// doesn't have): put your objects under the rect it returns. It goes away with the window like the rest.
    /// </summary>
    public RectTransform Area(float height)
    {
        var area = Built(UiParts.Child(_parent, "Area"));
        UiParts.Height(area, height);
        return area;
    }

    /// <summary>
    /// A scrolling area of its own, <paramref name="height"/> tall, filled through its own builder: a long run of
    /// controls beside others that should stay put. The mouse wheel scrolls it while the pointer is over it, it
    /// has its own scrollbar, and keyboard navigation scrolls it to the highlighted control. Returns its rect.
    /// </summary>
    public RectTransform Scroll(float height, Action<UiBuilder> build)
    {
        var area = ScrollArea.Make(_parent, Theme, "Scroll");
        Built(area.Region);
        UiParts.Height(area.Region, height);
        if (build != null)
            Guard("Scroll area", () => build(new UiBuilder(_kit, _window, area.Content, _inBody, _group)));
        _last = area.Region;
        return area.Region;
    }

    /// <summary>
    /// A small underlined button at the right end of the title bar of the control built last (a slider, a cycle,
    /// a text field...): an action that belongs to that one setting, like "Pick..." or "Clear".
    /// </summary>
    public ButtonHandle HeaderButton(string text, Action onClick, string tooltip = null)
    {
        var bar = ControlHeader(nameof(HeaderButton));
        var (holder, label) = HeaderAction(bar, text, tooltip);
        var button = holder.gameObject.AddComponent<KitButton>();
        button.Init(label, _kit, () => Guard(text, onClick));
        return new ButtonHandle(button, label);
    }

    /// <summary>
    /// Gives the control built last a Reset, like the Settings page has: a small "Reset" at the right end of its
    /// title bar while <paramref name="changed"/> is true (checked a few times a second), and Backspace, Delete or
    /// the pad's X on its highlighted row. <paramref name="reset"/> should put the value back and show it
    /// (<c>handle.Set(...)</c>); <paramref name="tooltip"/> shows on hover, say "Default: 2".
    /// </summary>
    public void Resettable(Func<bool> changed, Action reset, string tooltip = null)
    {
        if (changed == null || reset == null)
            throw new ArgumentNullException(changed == null ? nameof(changed) : nameof(reset));
        var mark = ResetMark(ControlHeader(nameof(Resettable)), tooltip, () => _window.IsOpen && changed(), reset);
        if (mark != null)
            _window.AddRefresher(mark.Refresh);
    }

    // The title bar of the option control built last; its row must be the last thing built.
    private RectTransform ControlHeader(string what)
    {
        if (_lastRow == null || _lastHeader == null || _last != _lastRow.Rect)
            throw new InvalidOperationException(what + " goes right after a control with a title bar (a slider, cycle, toggle, text field...).");
        return _lastHeader;
    }

    // A clickable word at the right end of a title bar, left of any already there.
    private (RectTransform Holder, TMP_Text Label) HeaderAction(RectTransform bar, string text, string tooltip)
    {
        float taken = 6f;
        for (int i = 0; i < bar.childCount; i++)
        {
            if (bar.GetChild(i).name == "Header action")
                taken += ((RectTransform)bar.GetChild(i)).sizeDelta.x;
        }
        var holder = UiParts.Child(bar, "Header action");
        holder.anchorMin = new Vector2(1f, 0f);
        holder.anchorMax = new Vector2(1f, 1f);
        holder.pivot = new Vector2(1f, 0.5f);
        var label = UiParts.Text(holder, Theme, text, Theme.TextSize * 0.85f, Theme.Text);
        label.fontStyle = FontStyles.Underline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        UiParts.Stretch(label.rectTransform);
        holder.sizeDelta = new Vector2(label.preferredWidth + 24f, 0f);
        holder.anchoredPosition = new Vector2(-taken, 0f);
        holder.gameObject.AddComponent<Image>().color = Color.clear;
        if (!string.IsNullOrEmpty(tooltip))
            holder.gameObject.AddComponent<KitTooltip>().Init(_kit.Tooltips, tooltip);
        return (holder, label);
    }
}
