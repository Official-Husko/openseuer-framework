using System;
using ObenUIKit.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>The text cells of a segmented row or a tab row; the picked one is lit and underlined.</summary>
internal sealed class ChoiceStrip
{
    private readonly KitContext _kit;
    private readonly KitButton[] _cells;
    private readonly TMP_Text[] _labels;

    public ChoiceStrip(KitContext kit, KitButton[] cells, TMP_Text[] labels)
    {
        _kit = kit;
        _cells = cells;
        _labels = labels;
    }

    public int Count => _cells.Length;

    public void Bind(Action<int> pick)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            int which = i;
            _cells[i].Init(_labels[i], _kit, () => pick(which));
        }
    }

    public void Show(int index)
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            if (_cells[i] == null)
                continue;
            bool on = i == index;
            _cells[i].Selected = on;
            _labels[i].fontStyle = on ? FontStyles.Underline : FontStyles.Normal;
            _cells[i].Paint();
        }
    }

    public void Rename(int index, string text)
    {
        if (index >= 0 && index < _labels.Length && _labels[index] != null)
            _labels[index].text = text;
    }
}

/// <summary>A segmented choice. <see cref="Set"/> changes the pick without calling the mod back.</summary>
public sealed class ChoiceHandle
{
    private readonly ChoiceStrip _strip;
    private readonly Action<int> _onChange;

    internal ChoiceHandle(ChoiceStrip strip, int index, Action<int> onChange)
    {
        _strip = strip;
        _onChange = onChange;
        Set(index);
    }

    public int Index { get; private set; }

    public void Set(int index)
    {
        Index = Mathf.Clamp(index, 0, _strip.Count - 1);
        _strip.Show(Index);
    }

    internal void Pick(int index)
    {
        if (index == Index)
            return;
        Set(index);
        _onChange?.Invoke(Index);
    }

    internal void Step(int delta) => Pick((Index + delta % _strip.Count + _strip.Count) % _strip.Count);
}

/// <summary>A tab row and its pages. <see cref="Select"/> switches without calling the mod back.</summary>
public sealed class TabsHandle
{
    private readonly ChoiceStrip _strip;
    private readonly RectTransform[] _pages;
    private readonly Action<int> _onChange;

    internal TabsHandle(ChoiceStrip strip, RectTransform[] pages, int index, Action<int> onChange)
    {
        _strip = strip;
        _pages = pages;
        _onChange = onChange;
        Select(index);
    }

    /// <summary>The tab shown.</summary>
    public int Selected { get; private set; }

    public int Count => _pages.Length;

    public void Select(int index)
    {
        Selected = Mathf.Clamp(index, 0, _pages.Length - 1);
        _strip.Show(Selected);
        for (int i = 0; i < _pages.Length; i++)
        {
            if (_pages[i] != null && _pages[i].gameObject.activeSelf != (i == Selected))
                _pages[i].gameObject.SetActive(i == Selected);
        }
        if (_pages[Selected] != null && _pages[Selected].parent is RectTransform parent)
            LayoutRebuilder.MarkLayoutForRebuild(parent);
    }

    /// <summary>Changes a tab's name (a count in brackets, say).</summary>
    public void Rename(int index, string name) => _strip.Rename(index, name);

    internal void Pick(int index)
    {
        if (index == Selected)
            return;
        Select(index);
        _onChange?.Invoke(Selected);
    }

    internal void Step(int delta) => Pick((Selected + delta % _pages.Length + _pages.Length) % _pages.Length);
}

/// <summary>One colour square with the frame that marks the picked or hovered one.</summary>
internal sealed class SwatchCell
{
    private const float Size = 40f;
    private const float Ring = 4f;

    private Image _ring;
    private Color _picked;
    private Color _hover;
    private bool _on;
    private bool _hovered;

    public static SwatchCell Build(RectTransform row, KitContext kit, Theme.UiTheme theme, Color color, Action click)
    {
        var cell = new SwatchCell { _picked = theme.Text, _hover = theme.TextHover };
        var holder = UiParts.Child(row, "Swatch");
        var size = UiParts.Layout(holder);
        size.minWidth = size.preferredWidth = Size;
        size.minHeight = size.preferredHeight = Size;

        cell._ring = UiParts.Image(holder, "Ring", null, Color.clear);
        UiParts.Stretch(cell._ring.rectTransform);
        // The game renders in linear space; converting keeps the square the colour the mod asked for.
        var chip = UiParts.Image(holder, "Chip", null, color.linear, raycast: true);
        UiParts.Stretch(chip.rectTransform, Ring, Ring, Ring, Ring);
        var clicker = chip.gameObject.AddComponent<KitClick>();
        clicker.Init(kit, click);
        clicker.Hovered = on =>
        {
            cell._hovered = on;
            cell.Paint();
        };
        return cell;
    }

    public void SetPicked(bool on)
    {
        _on = on;
        Paint();
    }

    private void Paint()
    {
        if (_ring != null)
            _ring.color = _hovered ? _hover : _on ? _picked : Color.clear;
    }
}

/// <summary>A row of colour swatches. <see cref="Set"/> changes the pick without calling the mod back.</summary>
public sealed class SwatchHandle
{
    private readonly SwatchCell[] _cells;
    private readonly Color[] _colors;
    private readonly Action<int> _onChange;

    internal SwatchHandle(SwatchCell[] cells, Color[] colors, int index, Action<int> onChange)
    {
        _cells = cells;
        _colors = colors;
        _onChange = onChange;
        Set(index);
    }

    public int Index { get; private set; }

    /// <summary>The picked colour, as the mod gave it.</summary>
    public Color Color => _colors[Index];

    public void Set(int index)
    {
        Index = Mathf.Clamp(index, 0, _cells.Length - 1);
        for (int i = 0; i < _cells.Length; i++)
            _cells[i].SetPicked(i == Index);
    }

    internal void Pick(int index)
    {
        if (index == Index)
            return;
        Set(index);
        _onChange?.Invoke(Index);
    }

    internal void Step(int delta) => Pick((Index + delta % _cells.Length + _cells.Length) % _cells.Length);
}
