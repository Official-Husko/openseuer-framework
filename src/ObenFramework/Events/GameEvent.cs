using System;
using ObenLoader.Runtime;
using ObenLoader.Logging;

namespace ObenFramework.Events;

/// <summary>An event without a payload. Subscribe with an owner so hot reloads clean up after you.</summary>
public sealed class GameEvent
{
    private static readonly Action<Action> Call = h => h();
    private readonly HandlerList<Action> _handlers = new HandlerList<Action>();
    private readonly string _name;

    public GameEvent(string name)
    {
        _name = name;
    }

    public IDisposable Subscribe(Action handler) => _handlers.Add(handler);

    public void Subscribe(ITracker owner, Action handler) => _handlers.Add(owner, handler);

    internal void Raise(ModLogger log) => _handlers.ForEach(Call, log, _name);
}

/// <summary>An event with a payload.</summary>
public sealed class GameEvent<T>
{
    private readonly HandlerList<Action<T>> _handlers = new HandlerList<Action<T>>();
    private readonly string _name;

    public GameEvent(string name)
    {
        _name = name;
    }

    public IDisposable Subscribe(Action<T> handler) => _handlers.Add(handler);

    public void Subscribe(ITracker owner, Action<T> handler) => _handlers.Add(owner, handler);

    internal void Raise(T value, ModLogger log) => _handlers.ForEach(h => h(value), log, _name);
}
