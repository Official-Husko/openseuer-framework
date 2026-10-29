using System;
using ObenUIKit.Input;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using ObenUIKit.Windows;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>
/// Fills a window with controls that look like the game's own, top to bottom. Every control takes
/// its look from the captured theme and its callbacks are guarded: a mod's exception is logged and
/// the window stays open.
/// </summary>
public sealed partial class UiBuilder
{
    public const float RowHeight = 58f;
    public const float HeaderHeight = 46f;

    private readonly KitContext _kit;
    private readonly UiWindow _window;
    private readonly RectTransform _parent;
    private readonly bool _inBody;
    private readonly int _group;
    private RectTransform _last;

    internal UiBuilder(KitContext kit, UiWindow window, RectTransform parent, bool inBody = true, int group = 0)
    {
        _kit = kit;
        _window = window;
        _parent = parent;
        _inBody = inBody;
        _group = group;
    }

    private UiTheme Theme => _kit.Theme;

    /// <summary>The window being built.</summary>
    public UiWindow Window => _window;

    /// <summary>A grey title bar like the ones above the game's settings.</summary>
    public LabelHandle Header(string text)
    {
        var bar = Built(UiParts.Image(_parent, "Header", Theme.HeaderSprite, Theme.Header));
        UiParts.Height(bar, HeaderHeight);
        _lastHeader = bar.rectTransform;
        var label = UiParts.Text(bar.transform, Theme, text, Theme.HeaderSize, Theme.Text);
        UiParts.Stretch(label.rectTransform, 12f, 0f, 12f, 0f);
        return new LabelHandle(label);
    }

    /// <summary>Plain text that wraps. Muted text is greyer, for hints and descriptions.</summary>
    public LabelHandle Label(string text, bool muted = false, TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var label = Built(UiParts.Text(_parent, Theme, text, Theme.TextSize * (muted ? 0.85f : 1f), muted ? Theme.TextMuted : Theme.Text, align));
        var fitter = label.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return new LabelHandle(label);
    }

    /// <summary>A label whose text is read again a few times a second while the window is open.</summary>
    public LabelHandle Live(Func<string> text, bool muted = false)
    {
        var handle = Label(text(), muted);
        _window.AddRefresher(() => handle.Text = text());
        return handle;
    }

    /// <summary>An underlined text button, like Apply and Back in the game's options.</summary>
    public ButtonHandle Button(string text, Action onClick)
    {
        var holder = Built(UiParts.Child(_parent, "Button " + text));
        UiParts.Height(holder, RowHeight);
        var label = UiParts.Text(holder, Theme, text, Theme.ButtonSize, Theme.Text);
        label.fontStyle = FontStyles.Underline;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        UiParts.Stretch(label.rectTransform);
        UiParts.Width(holder, label.preferredWidth + 20f);

        var hit = holder.gameObject.AddComponent<Image>();
        hit.color = Color.clear; // Catches the pointer over the whole button, not just the glyphs.
        var button = holder.gameObject.AddComponent<KitButton>();
        button.Init(label, _kit, onClick);
        button.Bind(_window.Nav);
        _window.Nav.Add(button, _inBody, _group);
        return new ButtonHandle(button, label);
    }

    /// <summary>Empty space.</summary>
    public void Space(float height = 20f) => UiParts.Height(UiParts.Child(_parent, "Space"), height);

    /// <summary>Controls side by side, centred.</summary>
    public void Row(Action<UiBuilder> build)
    {
        var row = Built(UiParts.Child(_parent, "Row"));
        UiParts.Height(row, RowHeight);
        var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.spacing = 40f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        build(new UiBuilder(_kit, _window, row, _inBody, _window.Nav.NewGroup()));
    }

    /// <summary>Makes an option row reachable from the keyboard and controller.</summary>
    private void Register(KitRow row, Func<int, bool> step, Action activate, Action click = null)
    {
        row.Bind(_window.Nav, step, activate, click);
        _window.Nav.Add(row, _inBody, _group);
    }

    private void Guard(string what, Action action) => _window.Run(what, action);

    /// <summary>Remembers the control just built, for <see cref="Tooltip"/>.</summary>
    private T Built<T>(T control) where T : Component
    {
        _last = control != null ? control.transform as RectTransform : null;
        return control;
    }
}
