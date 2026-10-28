using System;
using System.Collections.Generic;
using ObenUIKit.Runtime;
using UnityEngine.UI;

namespace ObenUIKit.Input;

/// <summary>
/// Keyboard and controller navigation inside one window. Up and down move a highlight over the
/// controls in the order they were built, left and right step cycles and sliders (or move along a
/// row of buttons), Enter and A activate. The highlight is the controls' own hover look, and a
/// mouse hover moves it too, so there's never a second highlighted control.
/// Controls add themselves when built, so nothing is searched or sorted while the window is open.
/// </summary>
internal sealed class UiNav
{
    private struct Entry
    {
        public INavTarget Target;
        public bool InBody;
        public int Group;
    }

    private readonly KitContext _kit;
    private readonly ScrollRect _scroll;
    private readonly List<Entry> _items = new List<Entry>();
    private AxisRepeat _vertical;
    private AxisRepeat _horizontal;
    private int _focus = -1;
    private int _groups;
    private int _remembered = -1;
    private bool _byHover;

    public UiNav(KitContext kit, ScrollRect scroll)
    {
        _kit = kit;
        _scroll = scroll;
    }

    /// <summary>A new id for controls that sit side by side; left and right move between them.</summary>
    public int NewGroup() => ++_groups;

    public void Add(INavTarget target, bool inBody, int group) =>
        _items.Add(new Entry { Target = target, InBody = inBody, Group = group });

    /// <summary>Forgets every control before the window builds them again, keeping the place in the list.</summary>
    public void BeginRebuild()
    {
        _remembered = _byHover ? -1 : _focus;
        _focus = -1;
        _groups = 0;
        _items.Clear();
    }

    public void EndRebuild()
    {
        if (_remembered < 0 || _items.Count == 0)
            return;
        int start = Math.Min(_remembered, _items.Count - 1);
        for (int k = 0; k < _items.Count; k++)
        {
            int i = (start + k) % _items.Count;
            if (_items[i].Target.Focusable)
            {
                SetFocus(i, scroll: false);
                return;
            }
        }
    }

    /// <summary>The mouse is over a control: highlight it instead of whatever was.</summary>
    public void Hover(INavTarget target)
    {
        int i = IndexOf(target);
        if (i < 0 || i == _focus)
            return;
        SetFocus(i, scroll: false);
        _byHover = true;
    }

    /// <summary>The mouse left a control it had highlighted; the highlight goes with it.</summary>
    public void Unhover(INavTarget target)
    {
        if (_byHover && _focus >= 0 && _focus < _items.Count && _items[_focus].Target == target)
        {
            target.SetFocus(false);
            _focus = -1;
        }
    }

    /// <summary>Called every frame by the kit's tick for the top window, unless a field or key capture has the keyboard.</summary>
    public void Tick(float now)
    {
        int vertical = NavKeys.Vertical();
        if (_vertical.Pulse(vertical, now))
            Move(vertical);

        int horizontal = NavKeys.Horizontal();
        if (_horizontal.Pulse(horizontal, now))
            Sideways(horizontal);

        if (NavKeys.Confirm() && _focus >= 0 && _focus < _items.Count && _items[_focus].Target.Focusable)
            Run(_items[_focus].Target, 0);
    }

    private void Move(int direction)
    {
        int n = _items.Count;
        int i = _focus;
        for (int k = 0; k < n; k++)
        {
            i = i < 0 ? (direction > 0 ? 0 : n - 1) : (i + direction + n) % n;
            if (!_items[i].Target.Focusable)
                continue;
            SetFocus(i, scroll: true);
            _byHover = false;
            _kit.Sounds.Hover();
            return;
        }
    }

    private void Sideways(int direction)
    {
        if (_focus < 0 || _focus >= _items.Count)
            return;
        var entry = _items[_focus];
        if (entry.Target.Focusable && Run(entry.Target, direction))
            return;
        if (entry.Group == 0)
            return;

        for (int i = _focus + direction; i >= 0 && i < _items.Count && _items[i].Group == entry.Group; i += direction)
        {
            if (!_items[i].Target.Focusable)
                continue;
            SetFocus(i, scroll: false);
            _byHover = false;
            _kit.Sounds.Hover();
            return;
        }
    }

    // direction 0 activates; anything else steps. Returns whether the control did something.
    private bool Run(INavTarget target, int direction)
    {
        try
        {
            if (direction == 0)
            {
                target.Activate();
                return true;
            }
            return target.Step(direction);
        }
        catch (Exception e)
        {
            _kit.Log.Error("A control failed while navigating with the keyboard", e);
            return true;
        }
    }

    private void SetFocus(int index, bool scroll)
    {
        if (_focus >= 0 && _focus < _items.Count)
            _items[_focus].Target.SetFocus(false);
        _focus = index;
        var entry = _items[index];
        entry.Target.SetFocus(true);
        if (scroll && entry.InBody && _scroll != null)
            NavScroll.Reveal(_scroll, entry.Target.Rect);
    }

    private int IndexOf(INavTarget target)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            if (_items[i].Target == target)
                return i;
        }
        return -1;
    }
}
