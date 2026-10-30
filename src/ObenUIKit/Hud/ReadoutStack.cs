using System;
using System.Collections.Generic;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit;

/// <summary>
/// The column of HUD readouts at the top left, under the bars. Texts are read four times a second and a
/// label is only written when its text changed; between reads nothing runs. The column hides with the
/// game's HUD and while a kit window or the pause menu is open.
/// </summary>
internal sealed class ReadoutStack
{
    public const int MaxReadouts = 4;
    public const int MaxText = 2000;

    private const float ReadInterval = 0.25f;
    private const float Width = 460f;
    private const float Left = 30f;
    private const float Top = 30f;

    private readonly RectTransform _parent;
    private readonly ModLogger _log;
    private readonly List<HudReadout> _readouts = new List<HudReadout>();
    private RectTransform _stack;
    private UiTheme _theme;
    private float _nextRead;

    public ReadoutStack(RectTransform parent, ModLogger log)
    {
        _parent = parent;
        _log = log;
    }

    public int Count => _readouts.Count;

    public bool Add(HudReadout readout, UiTheme theme)
    {
        if (_readouts.Count >= MaxReadouts)
            return false;
        EnsureStack(theme);
        _readouts.Add(readout);
        Build(readout, theme);
        // A text that throws right away is logged and the readout comes back no longer active.
        if (!Read(readout))
            Remove(readout);
        return true;
    }

    public bool Remove(HudReadout readout)
    {
        if (!_readouts.Remove(readout))
            return false;
        readout.IsActive = false;
        if (readout.Root != null)
            UnityEngine.Object.Destroy(readout.Root);
        return true;
    }

    public void RemoveOwner(ITracker owner)
    {
        for (int i = _readouts.Count - 1; i >= 0; i--)
        {
            if (_readouts[i].Owner == owner)
                Remove(_readouts[i]);
        }
    }

    /// <param name="below">How far down the bars above reach, so the column starts under them.</param>
    public void Tick(bool hidden, float below, UiTheme theme)
    {
        if (_stack == null)
            return;
        bool visible = _readouts.Count > 0 && !hidden;
        if (_stack.gameObject.activeSelf != visible)
            _stack.gameObject.SetActive(visible);
        if (!visible)
            return;

        var position = new Vector2(Left, -Top - below);
        if (_stack.anchoredPosition != position)
            _stack.anchoredPosition = position;
        if (_theme != theme)
            Restyle(theme);
        if (Time.unscaledTime < _nextRead)
            return;
        _nextRead = Time.unscaledTime + ReadInterval;
        for (int i = 0; i < _readouts.Count; i++)
        {
            if (!Read(_readouts[i]))
                Remove(_readouts[i--]);
        }
    }

    public void Dispose()
    {
        for (int i = _readouts.Count - 1; i >= 0; i--)
            _readouts[i].IsActive = false;
        _readouts.Clear();
        if (_stack != null)
            UnityEngine.Object.Destroy(_stack.gameObject);
        _stack = null;
    }

    // False when the mod's text function threw; the readout is dropped so one bug doesn't repeat every read.
    private bool Read(HudReadout readout)
    {
        string text;
        try
        {
            text = readout.Text() ?? "";
        }
        catch (Exception e)
        {
            _log.Error($"The text of HUD readout '{readout.Title}' threw, so the readout was removed", e);
            return false;
        }
        if (text.Length > MaxText)
            text = text.Substring(0, MaxText) + "...";
        if (text == readout.Shown)
            return true;
        readout.Shown = text;
        if (readout.BodyText != null)
            readout.BodyText.text = KitText.Escape(text);
        return true;
    }

    private void EnsureStack(UiTheme theme)
    {
        if (_stack != null)
            return;
        _theme = theme;
        _stack = UiParts.Child(_parent, "Readouts");
        _stack.anchorMin = _stack.anchorMax = _stack.pivot = new Vector2(0f, 1f);
        _stack.anchoredPosition = new Vector2(Left, -Top);
        _stack.sizeDelta = new Vector2(Width, 0f);
        var layout = _stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _stack.gameObject.SetActive(false);
    }

    private void Build(HudReadout readout, UiTheme theme)
    {
        // The hint panel's look: the game's black panel with its edge, a grey title over the text.
        var panel = UiParts.Image(_stack, "Readout", null, theme.Panel);
        readout.Root = panel.gameObject;
        var outline = readout.Root.AddComponent<Outline>();
        outline.effectColor = theme.PanelEdge;
        outline.effectDistance = new Vector2(2f, -2f);
        var layout = readout.Root.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(16, 16, 10, 12);
        layout.spacing = 4f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        float size = theme.TextSize * 0.75f;
        readout.TitleText = UiParts.Text(panel.transform, theme, "", size, theme.TextMuted, TextAlignmentOptions.TopLeft);
        readout.BodyText = UiParts.Text(panel.transform, theme, KitText.Escape(readout.Shown ?? ""), size, theme.Text, TextAlignmentOptions.TopLeft);
        readout.Title = readout.Title;
    }

    // The look was captured again (a new scene): build every readout with the new values.
    private void Restyle(UiTheme theme)
    {
        _theme = theme;
        for (int i = 0; i < _readouts.Count; i++)
        {
            if (_readouts[i].Root != null)
                UnityEngine.Object.Destroy(_readouts[i].Root);
            Build(_readouts[i], theme);
        }
    }
}
