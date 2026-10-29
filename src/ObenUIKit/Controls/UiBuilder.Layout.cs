// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Grouping and help: cards, tooltips and long lists.</summary>
public sealed partial class UiBuilder
{
    public const float DefaultListRows = 8;

    /// <summary>
    /// A framed box holding its own controls, with an optional title at the top. Good for grouping a few
    /// settings, or for one entry of a list of things (a mod, a save, a quest).
    /// </summary>
    public CardHandle Card(string title, Action<UiBuilder> build)
    {
        var background = UiParts.Image(_parent, "Card " + title, null, Theme.Header);
        var card = background.rectTransform;
        var outline = background.gameObject.AddComponent<Outline>();
        outline.effectColor = Theme.PanelEdge;
        outline.effectDistance = new Vector2(2f, -2f);
        var layout = background.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(18, 18, 14, 18);
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        TMP_Text heading = null;
        if (!string.IsNullOrEmpty(title))
        {
            heading = UiParts.Text(card, Theme, title, Theme.HeaderSize, Theme.TextMuted, TextAlignmentOptions.MidlineLeft);
            heading.textWrappingMode = TextWrappingModes.NoWrap;
            heading.overflowMode = TextOverflowModes.Ellipsis;
            UiParts.Height(heading, HeaderHeight);
        }
        if (build != null)
            Guard(title ?? "Card", () => build(new UiBuilder(_kit, _window, card, _inBody, _group)));
        _last = card;
        return new CardHandle(card, heading) { Rect = card };
    }

    /// <summary>
    /// Gives the control built just before this call a tooltip: a small box with <paramref name="text"/>
    /// that shows after the mouse rests on the control, or when the keyboard highlights it.
    /// </summary>
    public UiBuilder Tooltip(string text)
    {
        var target = _last;
        if (target == null || string.IsNullOrEmpty(text))
            return this;
        // Only something the pointer can hit gets hover events.
        if (target.TryGetComponent(out Graphic graphic))
            graphic.raycastTarget = true;
        else
            target.gameObject.AddComponent<Image>().color = Color.clear;

        if (!target.TryGetComponent(out KitTooltip tip))
            tip = target.gameObject.AddComponent<KitTooltip>();
        tip.Init(_kit.Tooltips, text);
        return this;
    }

    /// <summary>
    /// A long list that only builds the rows on screen, so thousands of entries cost the same as ten.
    /// <paramref name="bind"/> fills a row for an index whenever it scrolls into view (keep it quick and
    /// don't keep the row: it is reused for other indexes). Up and down move the highlight inside the
    /// list, left and right a page at a time, Enter or a click calls <paramref name="onPick"/>.
    /// </summary>
    /// <param name="count">How many entries there are; change it later with <see cref="ListHandle.SetCount"/>.</param>
    /// <param name="bind">Fills the row for an index: <see cref="ListRow.Text"/>, <see cref="ListRow.Detail"/>.</param>
    /// <param name="onPick">Runs with the index of a clicked row, or the highlighted one on Enter.</param>
    /// <param name="rows">How many rows tall the list is on screen.</param>
    public ListHandle List(int count, Action<int, ListRow> bind, Action<int> onPick = null, float rows = DefaultListRows)
    {
        if (bind == null)
            throw new ArgumentNullException(nameof(bind));
        string key = _window.NextKey("list");
        // Read before Init: laying the list out reports scroll position 0, which would overwrite it.
        int top = _window.Remembered(key, 0);
        var frame = UiParts.Image(_parent, "List", null, Color.clear, raycast: true);
        UiParts.Height(frame, Mathf.Max(1f, rows) * KitList.RowHeight);
        var list = frame.gameObject.AddComponent<KitList>();
        var handle = new ListHandle(list) { Rect = frame.rectTransform };
        var window = _window;
        list.Init(_kit, Theme, Mathf.Max(0, count), bind, onPick,
            top => window.Remember(key, top),
            e => window.Fault($"A list in window '{window.Id}' failed", e));
        list.Restore(top);
        _window.Nav.Add(list, _inBody, _group);
        list.BindNav(_window.Nav);
        _last = frame.rectTransform;
        return handle;
    }
}
