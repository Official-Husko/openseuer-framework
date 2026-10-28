// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Clock;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player.Survival;

/// <summary>Which way a stat has to move to trigger a threshold callback.</summary>
public enum CrossDirection
{
    Either,
    Rising,
    Falling,
}

/// <summary>What <see cref="StatThresholds.OnCrossed"/> hands to the callback.</summary>
public readonly struct ThresholdCrossing
{
    public ThresholdCrossing(PlayerStatRef stat, float threshold, float previous, float value)
    {
        Stat = stat;
        Threshold = threshold;
        Previous = previous;
        Value = value;
    }

    public PlayerStatRef Stat { get; }
    public float Threshold { get; }

    /// <summary>The value at the previous check, a minute of game time earlier.</summary>
    public float Previous { get; }

    public float Value { get; }

    /// <summary>The stat went up through the threshold (otherwise it went down through it).</summary>
    public bool Rising => Value >= Threshold;
}

/// <summary>
/// "Tell me when hunger reaches 80." Callbacks fire once each time a stat crosses a value, not while it
/// stays beyond it. Stats are sampled on the clock's minute tick, which is how often the game itself
/// updates most of them, so nothing runs per frame and nothing is checked while nobody is watching.
/// A stat that jumps across the threshold and back inside one minute is not seen. Right after a load the
/// first sample only sets the starting point, so a save that is already past the threshold fires nothing.
/// </summary>
public static class StatThresholds
{
    private static readonly List<Watch> Watches = new List<Watch>();
    private static Watch[] _snapshot = Array.Empty<Watch>();
    private static bool _snapshotStale;
    private static IDisposable _tick;

    /// <summary>
    /// Calls <paramref name="handler"/> whenever <paramref name="stat"/> crosses <paramref name="threshold"/>
    /// in the chosen direction. Ends when the returned object is disposed or <paramref name="owner"/> unloads.
    /// </summary>
    public static IDisposable OnCrossed(ObenMod owner, PlayerStatRef stat, float threshold,
        Action<ThresholdCrossing> handler, CrossDirection direction = CrossDirection.Either)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (stat == null)
            throw new ArgumentNullException(nameof(stat));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        if (float.IsNaN(threshold))
            throw new ArgumentOutOfRangeException(nameof(threshold), "The threshold must be a number.");

        var watch = new Watch(owner, stat, threshold, direction, handler);
        Watches.Add(watch);
        _snapshotStale = true;
        if (_tick == null)
            _tick = GameClock.MinutePassed.Subscribe(_ => Check());

        return owner.Track(Disposable.Create(() => Remove(watch)));
    }

    /// <summary>How many callbacks are registered.</summary>
    public static int Count => Watches.Count;

    internal static void Init(ObenMod framework)
    {
        // The game swaps its stat objects on a load, and values jump: start fresh after one.
        GameEvents.Loading.Subscribe(framework, ForgetSamples);
        GameEvents.Loaded.Subscribe(framework, ForgetSamples);
        GameEvents.WorldReady.Subscribe(framework, _ => ForgetSamples());
        framework.Track(Disposable.Create(() =>
        {
            _tick?.Dispose();
            _tick = null;
            Watches.Clear();
            _snapshot = Array.Empty<Watch>();
        }));
    }

    private static void Remove(Watch watch)
    {
        watch.Active = false;
        if (Watches.Remove(watch))
            _snapshotStale = true;
        if (Watches.Count == 0 && _tick != null)
        {
            _tick.Dispose();
            _tick = null;
        }
    }

    private static void ForgetSamples()
    {
        foreach (var watch in Watches)
            watch.HasLast = false;
    }

    private static void Check()
    {
        if (_snapshotStale)
        {
            _snapshot = Watches.ToArray();
            _snapshotStale = false;
        }

        var watches = _snapshot;
        for (int i = 0; i < watches.Length; i++)
        {
            var watch = watches[i];
            if (!watch.Active)
                continue;
            try
            {
                watch.Sample();
            }
            catch (Exception e)
            {
                watch.Owner.Log.Error($"A threshold callback for '{watch.Stat.Name}' threw", e);
            }
        }
    }

    private sealed class Watch
    {
        private readonly float _threshold;
        private readonly CrossDirection _direction;
        private readonly Action<ThresholdCrossing> _handler;
        private float _last;

        public Watch(ObenMod owner, PlayerStatRef stat, float threshold, CrossDirection direction,
            Action<ThresholdCrossing> handler)
        {
            Owner = owner;
            Stat = stat;
            _threshold = threshold;
            _direction = direction;
            _handler = handler;
            Active = true;
        }

        public ObenMod Owner { get; }
        public PlayerStatRef Stat { get; }
        public bool Active { get; set; }
        public bool HasLast { get; set; }

        public void Sample()
        {
            if (!Stat.TryGet(out float value))
            {
                HasLast = false;
                return;
            }

            float previous = _last;
            bool known = HasLast;
            _last = value;
            HasLast = true;
            if (!known)
                return;

            bool rising = previous < _threshold && value >= _threshold;
            bool falling = previous >= _threshold && value < _threshold;
            if ((rising && _direction != CrossDirection.Falling) || (falling && _direction != CrossDirection.Rising))
                _handler(new ThresholdCrossing(Stat, _threshold, previous, value));
        }
    }
}
