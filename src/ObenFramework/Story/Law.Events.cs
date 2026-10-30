// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Story;

public static partial class Law
{
    private sealed class Interceptor
    {
        public ITracker Owner;
        public Action<CrimeAttempt> Handler;
    }

    private static readonly List<Interceptor> Interceptors = new List<Interceptor>();
    private static Interceptor[] _snapshot = Array.Empty<Interceptor>();
    private static bool _intercepting;

    /// <summary>
    /// A crime went on the record (the game's own crimes and <see cref="Commit"/>). Raised at the end of the
    /// frame through the <see cref="StoryPump"/>, not inside the game's call. Each handler runs in its own try/catch.
    /// </summary>
    public static readonly GameEvent<CrimeCommitted> Committed = new GameEvent<CrimeCommitted>(nameof(Committed));

    /// <summary>The player paid the fine; the payload is the amount. Deferred like <see cref="Committed"/>.</summary>
    public static readonly GameEvent<int> Fined = new GameEvent<int>(nameof(Fined));

    /// <summary>The player was arrested and is passing out on the way to prison. Deferred like <see cref="Committed"/>.</summary>
    public static readonly GameEvent Arrested = new GameEvent(nameof(Arrested));

    /// <summary>The player was let out of prison after serving the sentence. Deferred like <see cref="Committed"/>.</summary>
    public static readonly GameEvent Released = new GameEvent(nameof(Released));

    /// <summary>
    /// Sees every crime before it goes on the record and may change its kind, severity or notification, or
    /// cancel it. Handlers run inside the game's call, in the order they were added, each in its own try/catch.
    /// Removed when <paramref name="owner"/> unloads.
    /// </summary>
    public static void Intercept(ITracker owner, Action<CrimeAttempt> handler)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        var entry = new Interceptor { Owner = owner, Handler = handler };
        Interceptors.Add(entry);
        _snapshot = Interceptors.ToArray();
        owner.Track(Disposable.Create(() =>
        {
            Interceptors.Remove(entry);
            _snapshot = Interceptors.ToArray();
        }));
    }

    /// <summary>
    /// Takes out the interceptors <paramref name="owner"/> added, without waiting for it to unload. Returns how many.
    /// </summary>
    public static int StopIntercepting(ITracker owner)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        int removed = Interceptors.RemoveAll(i => i.Owner == owner);
        if (removed > 0)
            _snapshot = Interceptors.ToArray();
        return removed;
    }

    internal static bool HasInterceptors => _snapshot.Length > 0;

    internal static void RunInterceptors(CrimeAttempt attempt)
    {
        var handlers = _snapshot;
        if (handlers.Length == 0 || _intercepting)
            return;
        _intercepting = true;
        try
        {
            bool cancelled = false;
            foreach (var interceptor in handlers)
            {
                try { interceptor.Handler(attempt); }
                catch (Exception e) { StoryLog.Failed("Crime interceptor of " + StoryLog.NameOf(interceptor.Owner), e); }
                cancelled |= attempt.Cancel;
                attempt.Cancel = cancelled;
            }
        }
        finally
        {
            _intercepting = false;
        }
    }

    internal static void Init(ObenMod framework)
    {
        Crime.OnGoingToPrison += OnGoingToPrison;
        framework.Track(Disposable.Create(() =>
        {
            Crime.OnGoingToPrison -= OnGoingToPrison;
            Interceptors.Clear();
            _snapshot = Array.Empty<Interceptor>();
        }));
    }

    internal static void QueueCommitted(CrimeCommitted crime) =>
        StoryPump.Enqueue(null, "Law.Committed", () => Committed.Raise(crime, GameEvents.Log));

    internal static void QueueFined(int amount) =>
        StoryPump.Enqueue(null, "Law.Fined", () => Fined.Raise(amount, GameEvents.Log));

    internal static void QueueReleased() =>
        StoryPump.Enqueue(null, "Law.Released", () => Released.Raise(GameEvents.Log));

    private static void OnGoingToPrison()
    {
        try { StoryPump.Enqueue(null, "Law.Arrested", () => Arrested.Raise(GameEvents.Log)); }
        catch (Exception e) { StoryLog.Failed("Law.Arrested", e); }
    }
}
