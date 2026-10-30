using System;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;

namespace ObenUIKit;

/// <summary>
/// A small box of text on the HUD that follows a function: watched values, a tracker, a timer. It reads the
/// text a few times a second. <see cref="Remove"/> takes it away; it also goes when its mod unloads.
/// </summary>
public sealed class HudReadout
{
    private string _title;

    internal HudReadout(ITracker owner, string title, Func<string> text)
    {
        Owner = owner;
        _title = title;
        Text = text;
    }

    public bool IsActive { get; internal set; } = true;

    /// <summary>The grey line on top. Empty hides it.</summary>
    public string Title
    {
        get => _title;
        set
        {
            _title = value ?? "";
            if (TitleText != null)
            {
                TitleText.text = Controls.KitText.Escape(_title);
                TitleText.gameObject.SetActive(_title.Length > 0);
            }
        }
    }

    public void Remove() => HudRuntime.Current?.RemoveReadout(this);

    internal ITracker Owner { get; }

    internal Func<string> Text { get; }

    // The text last read, so the label is only touched when it changes.
    internal string Shown;

    internal GameObject Root;
    internal TMP_Text TitleText;
    internal TMP_Text BodyText;
}
