using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenUIKit;

/// <summary>
/// Hints waiting to be shown, one at a time in the order they came. The first is the one showing.
/// Asking again for a hint that is already queued from the same owner just gives it more time, so a
/// mod that calls <c>Hud.Hint</c> every frame doesn't fill the queue. Pure bookkeeping, no Unity.
/// </summary>
internal sealed class HintQueue
{
    public const int MaxQueued = 6;

    private readonly List<HudHint> _items = new List<HudHint>();

    public HudHint Current => _items.Count > 0 ? _items[0] : null;

    public int Count => _items.Count;

    /// <summary>Adds the hint, or returns the equal one already queued after extending it.</summary>
    public HudHint Add(HudHint hint)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var queued = _items[i];
            if (queued.Owner == hint.Owner && queued.Text == hint.Text && queued.Title == hint.Title)
            {
                if (hint.Remaining > queued.Remaining)
                    queued.Remaining = hint.Remaining;
                return queued;
            }
        }

        _items.Add(hint);
        // Over the limit, the oldest one still waiting makes room.
        if (_items.Count > MaxQueued)
            Drop(1);
        return hint;
    }

    /// <summary>Removes a hint. True when it was the one showing.</summary>
    public bool Remove(HudHint hint)
    {
        int index = _items.IndexOf(hint);
        if (index < 0)
            return false;
        Drop(index);
        return index == 0;
    }

    /// <summary>Removes everything an owner queued. True when the one showing was among them.</summary>
    public bool RemoveOwner(ITracker owner)
    {
        bool wasCurrent = false;
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (_items[i].Owner != owner)
                continue;
            Drop(i);
            wasCurrent |= i == 0;
        }
        return wasCurrent;
    }

    public void Clear()
    {
        for (int i = _items.Count - 1; i >= 0; i--)
            Drop(i);
    }

    /// <summary>
    /// Counts down the hint showing. <paramref name="paused"/> holds the clock while the hint can't be
    /// seen. True when it ran out and the next one is now current.
    /// </summary>
    public bool Tick(float delta, bool paused)
    {
        var current = Current;
        if (current == null || paused)
            return false;
        current.Remaining -= delta;
        if (current.Remaining > 0f)
            return false;
        Drop(0);
        return true;
    }

    private void Drop(int index)
    {
        _items[index].IsActive = false;
        _items.RemoveAt(index);
    }
}
