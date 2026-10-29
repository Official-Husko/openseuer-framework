// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>Choices shown all at once: a segmented row, tabs, colour swatches, and a number stepper.</summary>
public sealed partial class UiBuilder
{
    /// <summary>
    /// A header and a row with every option side by side; the picked one is lit and underlined. Left and
    /// right (or a click) pick. Use it instead of <see cref="Cycle"/> when there are only a few short options.
    /// </summary>
    public ChoiceHandle Segmented(string label, string[] options, int index, Action<int> onChange)
    {
        if (options == null || options.Length == 0)
            throw new ArgumentException("A segmented choice needs at least one option.", nameof(options));
        Header(label);
        var kitRow = OptionRow(label);
        var strip = Strip(kitRow.Rect, options, Theme.TextSize, padding: 24);
        ChoiceHandle handle = null;
        handle = new ChoiceHandle(strip, index, i => Guard(label, () => onChange?.Invoke(i))) { Rect = kitRow.Rect };
        strip.Bind(handle.Pick);
        Register(kitRow, d => { _kit.Sounds.Press(); handle.Step(d); return true; },
            () => { _kit.Sounds.Press(); handle.Step(1); });
        return handle;
    }

    /// <summary>
    /// A row of tabs with a page under it for each. <paramref name="page"/> fills tab i through its own
    /// builder; every page is built once, up front, and switching only shows another one. The picked tab
    /// is kept when the window rebuilds. Left and right on the tab row switch tabs.
    /// </summary>
    public TabsHandle Tabs(string[] names, Action<int, UiBuilder> page, int index = 0, Action<int> onChange = null)
    {
        if (names == null || names.Length == 0)
            throw new ArgumentException("Tabs need at least one name.", nameof(names));
        string key = _window.NextKey("tabs");
        int start = Mathf.Clamp(_window.Remembered(key, index), 0, names.Length - 1);

        var kitRow = OptionRow("Tabs");
        var strip = Strip(kitRow.Rect, names, Theme.ButtonSize, padding: 12);
        TabsHandle handle = null;
        // The tab row goes into the navigation before the pages' controls.
        Register(kitRow, d => { _kit.Sounds.Press(); handle.Step(d); return true; },
            () => { _kit.Sounds.Press(); handle.Step(1); });

        var pages = new RectTransform[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            pages[i] = Column(_parent, "Tab " + names[i], 10f);
            int which = i;
            Guard(names[i], () => page?.Invoke(which, new UiBuilder(_kit, _window, pages[which], _inBody, _group)));
        }

        handle = new TabsHandle(strip, pages, start, i =>
        {
            _window.Remember(key, i);
            Guard(names[i], () => onChange?.Invoke(i));
        });
        handle.Rect = kitRow.Rect;
        handle.PageBuilder = column => new UiBuilder(_kit, _window, column, _inBody, _group);
        strip.Bind(handle.Pick);
        _last = kitRow.Rect;
        return handle;
    }

    /// <summary>
    /// A header and colour squares, in rows of up to eight; the picked one has a frame. Without a
    /// <paramref name="label"/> (null or empty) there's no header. Colours are given the usual way (as an image
    /// editor shows them) and drawn to match, even though the game renders in linear space.
    /// </summary>
    public SwatchHandle Swatches(string label, Color[] colors, int index, Action<int> onChange)
    {
        if (colors == null || colors.Length == 0)
            throw new ArgumentException("Swatches need at least one colour.", nameof(colors));
        if (!string.IsNullOrEmpty(label))
            Header(label);
        var kitRow = OptionRow(label ?? "Swatches");
        int rows = (colors.Length + SwatchColumns - 1) / SwatchColumns;
        if (rows == 1)
        {
            var layout = kitRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 9, 9);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
        }
        else
        {
            var grid = kitRow.gameObject.AddComponent<GridLayoutGroup>();
            grid.padding = new RectOffset(24, 24, 9, 9);
            grid.spacing = new Vector2(10f, 10f);
            grid.cellSize = new Vector2(SwatchCell.Size, SwatchCell.Size);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = SwatchColumns;
            grid.childAlignment = TextAnchor.UpperCenter;
            UiParts.Height(kitRow, rows * SwatchCell.Size + (rows - 1) * 10f + 18f);
        }

        var cells = new SwatchCell[colors.Length];
        SwatchHandle handle = null;
        for (int i = 0; i < colors.Length; i++)
        {
            int which = i;
            cells[i] = SwatchCell.Build(kitRow.Rect, _kit, Theme, colors[i], () => handle.Pick(which));
        }
        handle = new SwatchHandle(cells, colors, index, i => Guard(label, () => onChange?.Invoke(i))) { Rect = kitRow.Rect };
        Register(kitRow, d => { _kit.Sounds.Press(); handle.Step(d); return true; },
            () => { _kit.Sounds.Press(); handle.Step(1); });
        return handle;
    }

    /// <summary>
    /// A header and a "&lt; number &gt;" row that counts in <paramref name="step"/>s between
    /// <paramref name="min"/> and <paramref name="max"/> without wrapping around. Held keys repeat.
    /// </summary>
    public StepperHandle Stepper(string label, float value, float min, float max, float step, Action<float> onChange,
        string format = "0")
    {
        if (max < min)
            throw new ArgumentException("max is below min.", nameof(max));
        if (step <= 0f)
            throw new ArgumentException("step must be above 0.", nameof(step));
        Header(label);
        var kitRow = OptionRow(label);
        var row = kitRow.Rect;
        var text = UiParts.Text(row, Theme, "", Theme.TextSize, Theme.Text);
        text.textWrappingMode = TextWrappingModes.NoWrap;
        UiParts.Stretch(text.rectTransform, 70f, 0f, 70f, 0f);

        StepperHandle handle = null;
        var down = Arrow(row, "<", left: true, () => handle.Step(-1));
        var up = Arrow(row, ">", left: false, () => handle.Step(1));
        handle = new StepperHandle(text, down, up, value, min, max, step, format, v => Guard(label, () => onChange?.Invoke(v))) { Rect = row };
        Register(kitRow, d => { if (handle.Step(d)) _kit.Sounds.Press(); return true; },
            () => { if (handle.Step(1)) _kit.Sounds.Press(); });
        return handle;
    }

    // A row of text cells, each its own click target, laid out evenly over the row.
    private ChoiceStrip Strip(RectTransform row, string[] names, float size, int padding)
    {
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(padding, padding, 0, 0);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;

        var cells = new KitButton[names.Length];
        var labels = new TMP_Text[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            var holder = UiParts.Child(row, names[i]);
            UiParts.Width(holder, 0f, 1f);
            holder.gameObject.AddComponent<Image>().color = Color.clear;
            var text = UiParts.Text(holder, Theme, names[i], size, Theme.Text);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.overflowMode = TextOverflowModes.Ellipsis;
            UiParts.Stretch(text.rectTransform);
            cells[i] = holder.gameObject.AddComponent<KitButton>();
            labels[i] = text;
        }
        return new ChoiceStrip(_kit, cells, labels);
    }

    private const int SwatchColumns = 8;

    /// <summary>A plain vertical stack for pages and cards.</summary>
    private static RectTransform Column(Transform parent, string name, float spacing)
    {
        var column = UiParts.Child(parent, name);
        var layout = column.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return column;
    }
}
