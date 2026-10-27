using System;
using System.Collections.Generic;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenFramework.Events;

/// <summary>
/// Subscriber list shared by <see cref="GameEvent"/> and <see cref="GameEvent{T}"/>. Raising iterates a
/// cached snapshot, so handlers may unsubscribe (or the mod may unload) mid-raise, and nothing
/// allocates unless the list changed.
/// </summary>
public sealed class HandlerList<THandler> where THandler : Delegate
{
    private readonly List<THandler> _handlers = new List<THandler>();
    private THandler[] _snapshot = Array.Empty<THandler>();
    private bool _dirty;

    public int Count => _handlers.Count;

    public IDisposable Add(THandler handler)
    {
        _handlers.Add(handler);
        _dirty = true;
        return Disposable.Create(() => Remove(handler));
    }

    /// <summary>Subscribes and ties the subscription to <paramref name="owner"/>'s lifetime.</summary>
    public void Add(ITracker owner, THandler handler) => owner.Track(Add(handler));

    public void Remove(THandler handler)
    {
        if (_handlers.Remove(handler))
            _dirty = true;
    }

    public void ForEach(Action<THandler> invoke, ModLogger log, string eventName)
    {
        if (_dirty)
        {
            _snapshot = _handlers.ToArray();
            _dirty = false;
        }

        var handlers = _snapshot;
        for (int i = 0; i < handlers.Length; i++)
        {
            try
            {
                invoke(handlers[i]);
            }
            catch (Exception e)
            {
                log.Error($"A {eventName} handler from {handlers[i].Method.DeclaringType?.Assembly.GetName().Name} threw", e);
            }
        }
    }
}
