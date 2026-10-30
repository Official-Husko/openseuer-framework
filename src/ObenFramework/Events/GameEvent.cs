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

/// <summary>An event with a payload. A handler that takes no argument can subscribe too and skips the payload.</summary>
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

    /// <summary>Subscribes a handler that doesn't need the payload.</summary>
    public IDisposable Subscribe(Action handler) => _handlers.Add(Ignoring(handler));

    /// <summary>Subscribes a handler that doesn't need the payload, for as long as <paramref name="owner"/> is loaded.</summary>
    public void Subscribe(ITracker owner, Action handler) => _handlers.Add(owner, Ignoring(handler));

    internal void Raise(T value, ModLogger log) => _handlers.ForEach(h => h(value), log, _name);

    private static Action<T> Ignoring(Action handler)
    {
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        return new PayloadSkip(handler).Invoke;
    }

    // A class rather than a lambda so an error still names the mod the handler came from (HandlerList.Origin).
    private sealed class PayloadSkip : IWrappedHandler
    {
        private readonly Action _inner;

        public PayloadSkip(Action inner)
        {
            _inner = inner;
        }

        public Delegate Inner => _inner;

        public void Invoke(T _) => _inner();
    }
}
