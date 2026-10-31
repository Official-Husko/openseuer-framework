using System;
using System.Collections.Generic;
using ObenUIKit.Runtime;
using TMPro;
using UnityEngine;

namespace ObenUIKit.Computer;

/// <summary>
/// Draws an app inside its window on a computer screen, top to bottom, in the screen's own font and colours
/// (dark text on the white window, grey buttons). The list scrolls with the mouse wheel when it gets long. Sizes
/// are in the window's units: body text is 30, the window about 850 wide.
/// </summary>
public sealed class ComputerUi
{
    private readonly Action<ComputerUi> _build;
    private readonly ComputerUiLive _live;
    private readonly RectTransform _parent;
    private readonly ComputerUi _root;

    internal ComputerUi(ComputerWindow window, Action<ComputerUi> build)
    {
        Window = window;
        _build = build;
        _parent = ComputerUiParts.ScrollList(window.Content);
        _live = window.Content.gameObject.AddComponent<ComputerUiLive>();
        _root = this;
    }

    private ComputerUi(ComputerUi owner, RectTransform parent)
    {
        Window = owner.Window;
        _build = owner._build;
        _live = owner._live;
        _parent = parent;
        _root = owner._root;
    }

    public ComputerWindow Window { get; }

    /// <summary>Where the next item goes, for drawing something of your own (it's in a layout group).</summary>
    public RectTransform Content => _parent;

    /// <summary>Clears the window and draws it again with current values.</summary>
    public void Rebuild()
    {
        if (_root != this)
        {
            _root.Rebuild();
            return;
        }
        for (int i = _parent.childCount - 1; i >= 0; i--)
        {
            var child = _parent.GetChild(i).gameObject;
            child.SetActive(false);
            UnityEngine.Object.Destroy(child);
        }
        _live.Clear();
        _build(this);
    }

    public TextMeshProUGUI Text(string text, float size = 30f) =>
        ComputerUiParts.Text(_parent, Window.Font, text, size, ComputerUiParts.Ink, FontStyles.Normal);

    public TextMeshProUGUI Header(string text) =>
        ComputerUiParts.Text(_parent, Window.Font, text, 40f, ComputerUiParts.Ink, FontStyles.Bold);

    /// <summary>A line that reads <paramref name="text"/> again a few times a second.</summary>
    public TextMeshProUGUI Live(Func<string> text, float size = 30f)
    {
        var label = Text("", size);
        _live.Add(() => label.text = text() ?? "");
        return label;
    }

    public UnityEngine.UI.Button Button(string label, Action onClick)
    {
        var button = ComputerUiParts.Button(_parent, Window.Font, label);
        string name = label;
        button.onClick.AddListener(() =>
        {
            Window.Click();
            Guard(name, onClick);
        });
        return button;
    }

    /// <summary>A one-line text field. Typing in it doesn't reach the game (Interact won't make the player get up).</summary>
    public TMP_InputField Field(string text, Action<string> onChange, string placeholder = "", Action<string> onEnd = null)
    {
        var field = ComputerUiParts.Field(_parent, Window.Font, text, placeholder);
        string name = placeholder;
        field.onValueChanged.AddListener(v => Guard(name, () => onChange?.Invoke(v)));
        field.onEndEdit.AddListener(v => Guard(name, () => onEnd?.Invoke(v)));
        return field;
    }

    public UnityEngine.UI.Image Image(Sprite sprite, float height) => ComputerUiParts.Picture(_parent, sprite, height);

    public void Space(float height = 20f) => ComputerUiParts.Space(_parent, height);

    /// <summary>Items side by side, sharing the width.</summary>
    public void Row(Action<ComputerUi> build)
    {
        var row = ComputerUiParts.Row(_parent);
        build(new ComputerUi(this, row));
    }

    private void Guard(string what, Action action)
    {
        try { action?.Invoke(); }
        catch (Exception e) { KitRuntime.Current?.Kit.Log.Error($"'{what}' in the computer app '{Window.AppId}' failed", e); }
    }
}

/// <summary>Refreshes an app window's live lines four times a second while it is open.</summary>
internal sealed class ComputerUiLive : MonoBehaviour
{
    private readonly List<Action> _refreshers = new List<Action>();
    private float _next;

    public void Add(Action refresh)
    {
        _refreshers.Add(refresh);
        Run(refresh);
    }

    public void Clear() => _refreshers.Clear();

    private void Update()
    {
        if (_refreshers.Count == 0 || Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + 0.25f;
        for (int i = 0; i < _refreshers.Count; i++)
        {
            if (!Run(_refreshers[i]))
                _refreshers.RemoveAt(i--);
        }
    }

    private static bool Run(Action refresh)
    {
        try
        {
            refresh();
            return true;
        }
        catch (Exception e)
        {
            KitRuntime.Current?.Kit.Log.Error("A live line in a computer app failed; it stops updating", e);
            return false;
        }
    }
}
