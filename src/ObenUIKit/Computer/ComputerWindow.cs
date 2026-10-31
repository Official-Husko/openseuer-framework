using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ObenUIKit.Computer;

/// <summary>
/// An app's window on a computer's own screen: a copy of the game's program window (title bar, minimise and
/// close buttons, white body) with a taskbar entry, like the e-mail and text file windows. The app draws into
/// <see cref="Content"/>, whose units make the whole screen about 1800 by 1000.
/// </summary>
public sealed class ComputerWindow
{
    private const string Prefix = "Mod app ";

    private readonly GameObject _root;
    private readonly GameObject _taskbarEntry;
    private readonly Animator _animator;
    private readonly TextMeshProUGUI _title;
    private readonly AudioSource _click;

    private ComputerWindow(ComputerApp app, ComputerScreen screen, GameObject root, GameObject taskbarEntry, RectTransform content,
        TextMeshProUGUI title, AudioSource click)
    {
        App = app;
        Screen = screen;
        _root = root;
        _taskbarEntry = taskbarEntry;
        _animator = root.GetComponent<Animator>();
        _title = title;
        _click = click;
        Content = content;
        Font = ComputerScreenParts.Font(screen);
    }

    public string AppId => App.Id;

    internal ComputerApp App { get; }

    /// <summary>The screen it is on.</summary>
    public ComputerScreen Screen { get; }

    /// <summary>The app's area of the window, below the title bar. Anchored top left; its size is <see cref="Size"/>.</summary>
    public RectTransform Content { get; }

    public Vector2 Size => Content != null ? Content.sizeDelta : Vector2.zero;

    /// <summary>The font the screen's own texts use.</summary>
    public TMP_FontAsset Font { get; }

    /// <summary>On the screen and not closed (it may be minimised).</summary>
    public bool IsOpen => _root != null && !Closing;

    public bool IsMinimized => IsOpen && _animator != null && !_animator.GetBool("Open");

    /// <summary>The window closed: by its close button, the app, the mod unloading or the level going away.</summary>
    public event Action Closed;

    public string Title
    {
        get => _title != null ? _title.text : App.Title;
        set
        {
            if (_title != null)
                _title.text = value ?? "";
        }
    }

    internal bool Closing { get; private set; }

    /// <summary>Plays the screen's own click sound.</summary>
    public void Click()
    {
        if (_click != null)
            _click.Play();
    }

    /// <summary>Hides the window to its taskbar entry, as the game's minimise button does.</summary>
    public void Minimize()
    {
        if (!IsOpen || _animator == null)
            return;
        _animator.SetBool("Open", false);
        _root.transform.SetAsFirstSibling();
    }

    /// <summary>Shows the window again (from minimised) on top of the other windows.</summary>
    public void Restore()
    {
        if (!IsOpen)
            return;
        if (_animator != null)
            _animator.SetBool("Open", true);
        _root.transform.SetAsLastSibling();
    }

    /// <summary>The taskbar entry: minimises the window when it's on top, else brings it up.</summary>
    internal void Toggle()
    {
        if (!IsMinimized && _root.transform.GetSiblingIndex() == _root.transform.parent.childCount - 1)
            Minimize();
        else
            Restore();
    }

    public void Close() => ComputerWindows.Close(this);

    internal void Destroy()
    {
        Closing = true;
        foreach (var part in new[] { _root, _taskbarEntry })
        {
            if (part == null)
                continue;
            part.SetActive(false);
            Object.Destroy(part);
        }
        Closed?.Invoke();
    }

    /// <summary>Builds the window on <paramref name="screen"/>, minimised until <see cref="Restore"/>.</summary>
    internal static ComputerWindow Create(ComputerApp app, ComputerScreen screen)
    {
        var tabs = ComputerScreenParts.ProgramTabs(screen);
        var prefab = ComputerScreenParts.WindowPrefab(screen);
        if (tabs == null || prefab == null)
            throw new InvalidOperationException("This computer's screen has no program window to copy.");

        var root = Object.Instantiate(prefab, tabs, false);
        root.name = Prefix + app.Id;
        var viewer = root.GetComponent<ComputerTextFilePanel>();
        ComputerWindow window = null;
        AudioSource click = null;
        // The text file viewer's buttons call the viewer; theirs go, their click sounds stay.
        foreach (var button in root.GetComponentsInChildren<UnityEngine.UI.Button>(true))
        {
            ComputerScreenParts.Mute(button, viewer);
            if (button.name.StartsWith("Close", StringComparison.Ordinal))
            {
                click = button.GetComponent<AudioSource>();
                button.onClick.AddListener(() => window?.Close());
            }
            else if (button.name.StartsWith("Minimize", StringComparison.Ordinal))
                button.onClick.AddListener(() => window?.Minimize());
        }
        if (viewer != null)
            Object.Destroy(viewer);

        TextMeshProUGUI title = null;
        RectTransform area = null;
        foreach (var text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name == "Name")
            {
                title = text;
                text.text = app.Title;
                var icon = text.GetComponentInChildren<Image>(true);
                if (icon != null && app.Icon != null)
                    icon.sprite = app.Icon;
            }
            else if (text.name == "Content")
            {
                area = text.transform.parent as RectTransform;
                text.gameObject.SetActive(false);
            }
            else if (text.name.StartsWith("Top Text File", StringComparison.Ordinal))
                text.gameObject.SetActive(false);
        }
        if (area == null)
            area = (RectTransform)root.transform;

        var content = new GameObject("Mod content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.gameObject.layer = area.gameObject.layer;
        content.SetParent(area, false);
        content.anchorMin = content.anchorMax = content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one * ComputerScreenParts.ContentScale;
        content.sizeDelta = area.rect.size / ComputerScreenParts.ContentScale;

        var entry = TaskbarEntry(app, screen);
        window = new ComputerWindow(app, screen, root, entry, content, title, click);
        if (entry != null)
            entry.GetComponent<UnityEngine.UI.Button>()?.onClick.AddListener(() => window.Toggle());
        return window;
    }

    private static GameObject TaskbarEntry(ComputerApp app, ComputerScreen screen)
    {
        var taskbar = ComputerScreenParts.Taskbar(screen);
        var prefab = ComputerScreenParts.TaskbarPrefab(screen);
        if (taskbar == null || prefab == null)
            return null;
        var entry = Object.Instantiate(prefab, taskbar, false);
        entry.name = Prefix + app.Id;
        var program = entry.GetComponent<TaskbarProgram>();
        var button = entry.GetComponent<UnityEngine.UI.Button>();
        if (button != null)
            ComputerScreenParts.Mute(button, program);
        if (program != null)
            Object.Destroy(program);
        foreach (var image in entry.GetComponentsInChildren<Image>(true))
        {
            if (image.name == "Logo")
                image.sprite = app.Icon != null ? app.Icon : ComputerScreenParts.DefaultIcon(screen);
        }
        foreach (var text in entry.GetComponentsInChildren<TextMeshProUGUI>(true))
            text.text = app.Title;
        return entry;
    }
}
