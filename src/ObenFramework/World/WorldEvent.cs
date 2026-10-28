using System;
using ObenFramework.Events;
using ObenLoader.Runtime;

namespace ObenFramework.World;

/// <summary>
/// An event of the world module. Subscribe with an owner (<c>Doors.DoorChanged.Subscribe(this, ...)</c>)
/// so a hot reload removes the handler. Every handler runs in its own try/catch, and raising does not
/// allocate. The game side of each event is a Harmony patch that returns at once while nobody listens.
/// </summary>
public sealed class WorldEvent<T>
{
    private readonly HandlerList<Action<T>> _handlers = new HandlerList<Action<T>>();
    private readonly string _name;
    private readonly Action<Action<T>> _call;
    private T _current;

    internal WorldEvent(string name)
    {
        _name = name;
        _call = h => h(_current); // built once so raising doesn't allocate
    }

    /// <summary>True while at least one handler is subscribed.</summary>
    public bool HasSubscribers => _handlers.Count > 0;

    /// <summary>Subscribes for as long as <paramref name="owner"/> is loaded.</summary>
    public void Subscribe(ITracker owner, Action<T> handler) => _handlers.Add(owner, handler);

    /// <summary>Subscribes until the returned object is disposed.</summary>
    public IDisposable Subscribe(Action<T> handler) => _handlers.Add(handler);

    internal void Raise(T value)
    {
        // A handler may cause the same event again (a mod applying a change); keep the outer value intact.
        var outer = _current;
        _current = value;
        try
        {
            _handlers.ForEach(_call, GameEvents.Log, _name);
        }
        finally
        {
            _current = outer;
        }
    }
}
