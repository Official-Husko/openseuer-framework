using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>
/// One visible row of a long list, handed to the mod's bind function to fill. Rows are reused as the list
/// scrolls, so set every field each time and don't keep the row.
/// </summary>
public sealed class ListRow
{
    private RectTransform _rect;
    private Image _background;
    private TMP_Text _text;
    private TMP_Text _detail;
    private bool _muted;

    /// <summary>The entry this row shows right now.</summary>
    public int Index { get; private set; }

    /// <summary>The main text, on the left.</summary>
    public string Text
    {
        get => _text.text;
        set
        {
            if (_text.text != value)
                _text.text = value ?? "";
        }
    }

    /// <summary>A second, greyer text on the right (a value, a count, a state).</summary>
    public string Detail
    {
        get => _detail.text;
        set
        {
            if (_detail.text != value)
                _detail.text = value ?? "";
        }
    }

    /// <summary>Greys the main text out (an entry that can't be used right now).</summary>
    public bool Muted
    {
        get => _muted;
        set => _muted = value;
    }

    internal static ListRow Build(KitList list, RectTransform content, KitContext kit, UiTheme theme)
    {
        var row = new ListRow();
        row._background = UiParts.Image(content, "Row", theme.RowSprite, Color.clear, raycast: true);
        var rect = row._rect = row._background.rectTransform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = new Vector2(0f, KitList.RowHeight);

        row._text = UiParts.Text(rect, theme, "", theme.TextSize, theme.Text, TextAlignmentOptions.MidlineLeft);
        row._text.textWrappingMode = TextWrappingModes.NoWrap;
        row._text.overflowMode = TextOverflowModes.Ellipsis;
        UiParts.Stretch(row._text.rectTransform, 18f, 0f, 18f, 0f);
        row._text.rectTransform.anchorMax = new Vector2(0.68f, 1f);

        row._detail = UiParts.Text(rect, theme, "", theme.TextSize * 0.85f, theme.TextMuted, TextAlignmentOptions.MidlineRight);
        row._detail.textWrappingMode = TextWrappingModes.NoWrap;
        row._detail.overflowMode = TextOverflowModes.Ellipsis;
        UiParts.Stretch(row._detail.rectTransform, 0f, 0f, 18f, 0f);
        row._detail.rectTransform.anchorMin = new Vector2(0.68f, 0f);

        var click = rect.gameObject.AddComponent<KitClick>();
        click.Init(kit, () => list.Click(row));
        click.Hovered = on => list.Hover(row, on);
        return row;
    }

    internal void Show(bool used, int index, float y)
    {
        Index = used ? index : -1;
        if (_rect.gameObject.activeSelf != used)
            _rect.gameObject.SetActive(used);
        if (used)
            _rect.anchoredPosition = new Vector2(0f, y);
    }

    internal void Paint(UiTheme theme, bool lit)
    {
        if (_background == null)
            return;
        _background.color = lit ? theme.RowHover : Color.clear;
        _text.color = _muted ? theme.TextDisabled : theme.Text;
    }
}

/// <summary>A long list. Change what it shows with <see cref="SetCount"/> and <see cref="Refresh"/>.</summary>
public sealed class ListHandle
{
    private readonly KitList _list;

    internal ListHandle(KitList list)
    {
        _list = list;
    }

    public int Count => _list != null ? _list.Count : 0;

    /// <summary>The highlighted entry (keyboard, controller or the last click).</summary>
    public int Selected => _list != null ? _list.Selected : -1;

    /// <summary>Changes how many entries there are and fills the visible rows again.</summary>
    public void SetCount(int count)
    {
        if (_list != null)
            _list.SetCount(count);
    }

    /// <summary>Fills the visible rows again, for when the entries behind them changed.</summary>
    public void Refresh()
    {
        if (_list != null)
            _list.Refresh();
    }

    /// <summary>Scrolls so <paramref name="index"/> is visible and highlights it.</summary>
    public void ScrollTo(int index)
    {
        if (_list != null)
            _list.ScrollTo(index, select: true);
    }
}
