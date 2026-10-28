using System;
using ObenUIKit.Input;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Controls;

/// <summary>
/// A virtualised list: a scroll view as tall as all the entries, but only enough row objects to cover
/// what's visible, moved and re-filled as it scrolls. Re-filling happens only when the first visible
/// entry changes, and nothing is allocated while scrolling.
/// </summary>
internal sealed class KitList : MonoBehaviour, INavTarget, INavVertical
{
    public const float RowHeight = 50f;

    private KitContext _kit;
    private UiTheme _theme;
    private Action<int, ListRow> _bind;
    private Action<int> _pick;
    private Action<int> _scrolled;
    private Action<Exception> _failed;
    private float _view;
    private ScrollRect _scroll;
    private RectTransform _content;
    private ListRow[] _rows;
    private RectTransform _thumb;
    private UiNav _nav;
    private int _count;
    private int _first = -1;
    private int _selected;
    private int _hovered = -1;
    private bool _focus;

    public RectTransform Rect => (RectTransform)transform;

    public bool Focusable => isActiveAndEnabled && _count > 0;

    public int Count => _count;

    public int Selected => _selected;

    internal void Init(KitContext kit, UiTheme theme, int count, Action<int, ListRow> bind, Action<int> pick,
        Action<int> scrolled, Action<Exception> failed)
    {
        _kit = kit;
        _theme = theme;
        _bind = bind;
        _pick = pick;
        _scrolled = scrolled;
        _failed = failed;
        _count = count;
        // The list's height is fixed when it is built; the rect itself isn't laid out yet at that point.
        _view = Mathf.Max(RowHeight, UiParts.Layout(this).preferredHeight);
        BuildView();
        Layout();
    }

    internal void BindNav(UiNav nav) => _nav = nav;

    public void SetCount(int count)
    {
        _count = Math.Max(0, count);
        _selected = Mathf.Clamp(_selected, 0, Math.Max(0, _count - 1));
        Layout();
    }

    /// <summary>Fills every visible row again (the data behind them changed).</summary>
    public void Refresh()
    {
        _first = -1;
        OnScrolled(Vector2.zero);
    }

    public void ScrollTo(int index, bool select)
    {
        if (_count == 0)
            return;
        index = Mathf.Clamp(index, 0, _count - 1);
        if (select)
            _selected = index;
        Reveal(index);
    }

    /// <summary>Scrolls so <paramref name="first"/> is the top row (after a rebuild of the window).</summary>
    public void Restore(int first)
    {
        float offset = Mathf.Clamp(first * RowHeight, 0f, Mathf.Max(0f, _count * RowHeight - _view));
        _content.anchoredPosition = new Vector2(0f, offset);
        _selected = Mathf.Clamp(first, 0, Math.Max(0, _count - 1));
        Refresh();
    }

    public void SetFocus(bool on)
    {
        _focus = on;
        Paint();
    }

    public void Activate()
    {
        if (_count == 0)
            return;
        _kit.Sounds.Press();
        Pick(_selected);
    }

    /// <summary>Left and right jump a page.</summary>
    public bool Step(int delta)
    {
        if (_count == 0)
            return false;
        Select(_selected + delta * Math.Max(1, _rows.Length - 2));
        _kit.Sounds.Hover();
        return true;
    }

    public bool StepVertical(int delta)
    {
        int next = _selected + delta;
        if (next < 0 || next >= _count)
            return false;
        Select(next);
        return true;
    }

    public void Enter(int direction)
    {
        // Coming from above starts at the top of what's visible, from below at the bottom.
        int first = Mathf.Max(0, _first);
        int last = Mathf.Min(_count - 1, first + VisibleRows - 1);
        _selected = direction > 0 ? first : last;
        Paint();
    }

    private int VisibleRows => Mathf.Max(1, Mathf.FloorToInt(_view / RowHeight));

    private void Select(int index)
    {
        _selected = Mathf.Clamp(index, 0, _count - 1);
        Reveal(_selected);
        Paint();
    }

    private void Reveal(int index)
    {
        float view = _view;
        float top = index * RowHeight;
        float offset = _content.anchoredPosition.y;
        if (top < offset)
            offset = top;
        else if (top + RowHeight > offset + view)
            offset = top + RowHeight - view;
        offset = Mathf.Clamp(offset, 0f, Mathf.Max(0f, _count * RowHeight - view));
        _content.anchoredPosition = new Vector2(0f, offset);
        _scroll.velocity = Vector2.zero;
        OnScrolled(Vector2.zero);
    }

    private void Layout()
    {
        _content.sizeDelta = new Vector2(0f, _count * RowHeight);
        Refresh();
    }

    private void OnScrolled(Vector2 _)
    {
        if (_rows == null)
            return;
        float offset = Mathf.Max(0f, _content.anchoredPosition.y);
        int first = Mathf.Clamp(Mathf.FloorToInt(offset / RowHeight), 0, Math.Max(0, _count - 1));
        UpdateThumb(offset);
        if (first == _first)
            return;
        _first = first;
        _scrolled?.Invoke(first);
        for (int k = 0; k < _rows.Length; k++)
        {
            int index = first + k;
            var row = _rows[k];
            bool used = index < _count;
            row.Show(used, index, -index * RowHeight);
            if (!used)
                continue;
            try { _bind(index, row); }
            catch (Exception e) { _failed?.Invoke(e); }
        }
        Paint();
    }

    private void UpdateThumb(float offset)
    {
        float total = _count * RowHeight;
        float view = _view;
        bool needed = total > view + 1f;
        if (_thumb.gameObject.activeSelf != needed)
            _thumb.gameObject.SetActive(needed);
        if (!needed)
            return;
        float share = Mathf.Clamp(view / total, 0.05f, 1f);
        float position = Mathf.Clamp01(offset / (total - view));
        _thumb.anchorMin = new Vector2(1f, (1f - share) * (1f - position));
        _thumb.anchorMax = new Vector2(1f, _thumb.anchorMin.y + share);
    }

    private void Paint()
    {
        if (_rows == null)
            return;
        for (int k = 0; k < _rows.Length; k++)
        {
            var row = _rows[k];
            row.Paint(_theme, row.Index == _hovered || (_focus && row.Index == _selected));
        }
    }

    internal void Hover(ListRow row, bool on)
    {
        if (on)
        {
            _hovered = row.Index;
            _selected = row.Index;
            _nav?.Hover(this);
        }
        else if (_hovered == row.Index)
        {
            _hovered = -1;
            _nav?.Unhover(this);
        }
        Paint();
    }

    internal void Click(ListRow row)
    {
        _selected = row.Index;
        Paint();
        Pick(row.Index);
    }

    private void Pick(int index)
    {
        if (_pick == null)
            return;
        try { _pick(index); }
        catch (Exception e) { _failed?.Invoke(e); }
    }

    private void BuildView()
    {
        var viewport = UiParts.Stretch(UiParts.Child(transform, "Viewport"), 0f, 0f, 10f, 0f);
        viewport.gameObject.AddComponent<RectMask2D>();
        _content = UiParts.Child(viewport, "Rows");
        _content.anchorMin = new Vector2(0f, 1f);
        _content.anchorMax = new Vector2(1f, 1f);
        _content.pivot = new Vector2(0.5f, 1f);

        _scroll = gameObject.AddComponent<ScrollRect>();
        _scroll.horizontal = false;
        _scroll.movementType = ScrollRect.MovementType.Clamped;
        _scroll.scrollSensitivity = RowHeight;
        _scroll.viewport = viewport;
        _scroll.content = _content;
        _scroll.onValueChanged.AddListener(OnScrolled);

        var track = UiParts.Image(transform, "Track", null, _theme.SliderTrack).rectTransform;
        track.anchorMin = new Vector2(1f, 0f);
        track.anchorMax = Vector2.one;
        track.pivot = new Vector2(1f, 0.5f);
        track.sizeDelta = new Vector2(4f, 0f);
        _thumb = UiParts.Image(track, "Thumb", null, _theme.SliderFill).rectTransform;
        _thumb.pivot = new Vector2(1f, 0.5f);
        _thumb.offsetMin = new Vector2(-4f, 0f);
        _thumb.offsetMax = Vector2.zero;

        // Enough rows to cover the view while one is half scrolled out at each end.
        _rows = new ListRow[Mathf.CeilToInt(_view / RowHeight) + 2];
        for (int k = 0; k < _rows.Length; k++)
            _rows[k] = ListRow.Build(this, _content, _kit, _theme);
    }
}
