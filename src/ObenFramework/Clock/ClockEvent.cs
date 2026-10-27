using System;
using ObenFramework.Events;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenFramework.Clock;

/// <summary>
/// An event with an int payload whose source in the game is only hooked while someone is subscribed.
/// Every handler runs in its own try/catch, so one throwing mod can't stop the others.
/// </summary>
public sealed class ClockEvent
{
    private readonly HandlerList<Action<int>> _handlers = new HandlerList<Action<int>>();
    private readonly string _name;
    private readonly Action _changed;
    private readonly Action<Action<int>> _call;
    private int _current;

    internal ClockEvent(string name, Action changed)
    {
        _name = name;
        _changed = changed;
        _call = h => h(_current); // built once so raising doesn't allocate
    }

    /// <summary>True while at least one handler is subscribed.</summary>
    internal bool HasSubscribers => _handlers.Count > 0;

    /// <summary>Subscribes for as long as <paramref name="owner"/> is loaded.</summary>
    public void Subscribe(ITracker owner, Action<int> handler) => owner.Track(Subscribe(handler));

    /// <summary>Subscribes until the returned object is disposed.</summary>
    public IDisposable Subscribe(Action<int> handler)
    {
        var inner = _handlers.Add(handler);
        _changed();
        return Disposable.Create(() =>
        {
            inner.Dispose();
            _changed();
        });
    }

    internal void Raise(int value, ModLogger log)
    {
        _current = value;
        _handlers.ForEach(_call, log, _name);
    }
}
