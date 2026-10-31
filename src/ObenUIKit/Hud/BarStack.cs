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
/// The column of HUD bars at the top left. Values are read five times a second; between reads each bar
/// glides toward its value, which is the only per-frame work and allocates nothing. The column hides
/// with the game's HUD and while a kit window or the pause menu is open.
/// </summary>
internal sealed class BarStack
{
    public const int MaxBars = 6;

    private const float ReadInterval = 0.2f;
    private const float GlideSeconds = 0.15f;
    private const float Width = 320f;
    private const float TrackHeight = 12f;

    private readonly RectTransform _parent;
    private readonly ModLogger _log;
    private readonly List<HudBar> _bars = new List<HudBar>();
    private RectTransform _stack;
    private UiTheme _theme;
    private float _nextRead;

    public BarStack(RectTransform parent, ModLogger log)
    {
        _parent = parent;
        _log = log;
    }

    public int Count => _bars.Count;

    /// <summary>How far down the column reaches while it shows (0 while it doesn't), for what goes under it.</summary>
    public float Bottom => _stack != null && _stack.gameObject.activeSelf ? _stack.rect.height + 12f : 0f;

    public bool Add(HudBar bar, UiTheme theme)
    {
        if (_bars.Count >= MaxBars)
            return false;
        EnsureStack(theme);
        _bars.Add(bar);
        BuildBar(bar, theme);
        // A value that throws right away is logged and the bar comes back no longer active.
        if (!Read(bar))
        {
            Remove(bar);
            return true;
        }
        bar.Shown = bar.Target;
        ApplyFill(bar);
        return true;
    }

    public bool Remove(HudBar bar)
    {
        if (!_bars.Remove(bar))
            return false;
        bar.IsActive = false;
        if (bar.Root != null)
            UnityEngine.Object.Destroy(bar.Root);
        return true;
    }

    public void RemoveOwner(ITracker owner)
    {
        for (int i = _bars.Count - 1; i >= 0; i--)
        {
            if (_bars[i].Owner == owner)
                Remove(_bars[i]);
        }
    }

    public void Tick(float delta, bool hidden, UiTheme theme)
    {
        if (_stack == null)
            return;
        bool visible = _bars.Count > 0 && !hidden;
        if (_stack.gameObject.activeSelf != visible)
            _stack.gameObject.SetActive(visible);
        if (!visible)
            return;

        if (_theme != theme)
            Restyle(theme);
        if (Time.unscaledTime >= _nextRead)
        {
            _nextRead = Time.unscaledTime + ReadInterval;
            ReadAll();
        }
        Glide(delta);
    }

    public void Dispose()
    {
        for (int i = _bars.Count - 1; i >= 0; i--)
            _bars[i].IsActive = false;
        _bars.Clear();
        if (_stack != null)
            UnityEngine.Object.Destroy(_stack.gameObject);
        _stack = null;
    }

    private void ReadAll()
    {
        for (int i = 0; i < _bars.Count; i++)
        {
            if (!Read(_bars[i]))
                Remove(_bars[i--]);
        }
    }

    // False when the mod's value function threw; the bar is dropped so one bug doesn't repeat every read.
    private bool Read(HudBar bar)
    {
        try
        {
            float value = bar.Value();
            bar.Target = float.IsNaN(value) ? 0f : Mathf.Clamp01(value);
            return true;
        }
        catch (Exception e)
        {
            _log.Error($"The value of HUD bar '{bar.Label}' threw, so the bar was removed", e);
            return false;
        }
    }

    private void Glide(float delta)
    {
        float step = delta / GlideSeconds;
        for (int i = 0; i < _bars.Count; i++)
        {
            var bar = _bars[i];
            if (bar.Shown == bar.Target)
                continue;
            bar.Shown = Mathf.MoveTowards(bar.Shown, bar.Target, step);
            ApplyFill(bar);
        }
    }

    private static void ApplyFill(HudBar bar)
    {
        if (bar.FillRect != null)
            bar.FillRect.anchorMax = new Vector2(bar.Shown, 1f);
    }

    private void EnsureStack(UiTheme theme)
    {
        if (_stack != null)
            return;
        _theme = theme;
        _stack = UiParts.Child(_parent, "Bars");
        _stack.anchorMin = _stack.anchorMax = _stack.pivot = new Vector2(0f, 1f);
        _stack.anchoredPosition = new Vector2(30f, -30f);
        _stack.sizeDelta = new Vector2(Width, 0f);
        var layout = _stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 8f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        _stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        _stack.gameObject.SetActive(false);
    }

    private void BuildBar(HudBar bar, UiTheme theme)
    {
        var item = UiParts.Child(_stack, "Bar");
        bar.Root = item.gameObject;
        var layout = bar.Root.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 3f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        bar.Text = UiParts.Text(item, theme, bar.Label, theme.TextSize * 0.75f, theme.Text, TextAlignmentOptions.Left);
        bar.Text.textWrappingMode = TextWrappingModes.NoWrap;
        bar.ShowLabel();

        var track = UiParts.Image(item, "Track", null, theme.SliderTrack);
        UiParts.Height(track, TrackHeight);
        bar.Fill = UiParts.Image(track.transform, "Fill", null, bar.Color ?? theme.SliderFill);
        bar.FillRect = bar.Fill.rectTransform;
        bar.FillRect.anchorMin = Vector2.zero;
        bar.FillRect.anchorMax = new Vector2(bar.Shown, 1f);
        bar.FillRect.offsetMin = bar.FillRect.offsetMax = Vector2.zero;
    }

    // The look was captured again (a new scene): build every bar with the new values.
    private void Restyle(UiTheme theme)
    {
        _theme = theme;
        for (int i = 0; i < _bars.Count; i++)
        {
            var bar = _bars[i];
            if (bar.Root != null)
                UnityEngine.Object.Destroy(bar.Root);
            BuildBar(bar, theme);
            ApplyFill(bar);
        }
    }
}
