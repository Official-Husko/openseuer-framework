// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>
/// The one wait a mod started through <see cref="PlayerSleep"/>, and the flags its options set for
/// <see cref="WaitPatches"/>. It ends when the game wakes the player, when another wait replaces it, on a
/// level change and when the framework unloads; the mod's callback runs exactly once in every case.
/// </summary>
internal static class ModWait
{
    private static Pending _pending;
    private static bool _starting;

    public static bool Active => _pending != null;

    public static bool FreezeNeeds => _pending != null && _pending.FreezeNeeds;

    public static bool BlockPassOut => _pending != null && _pending.NoPassOut;

    public static string Label => _pending?.Label;

    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, FinishInterrupted);
        GameEvents.MainMenu.Subscribe(framework, FinishInterrupted);
        framework.Track(Disposable.Create(FinishInterrupted));
    }

    public static void Start(ObenMod owner, int seconds, WaitKind kind, WaitOptions options, Action<WaitResult> onDone, WaitingController waiting)
    {
        FinishInterrupted();
        var pending = new Pending
        {
            Owner = owner.Info.Id,
            FreezeNeeds = options.FreezeNeeds,
            NoPassOut = options.NoPassOut,
            Label = string.IsNullOrWhiteSpace(options.Label) ? null : options.Label,
            OnDone = onDone,
        };
        _pending = pending;
        // A mod that unloads mid-wait isn't called back; the game's wait just goes on as a plain one.
        owner.Track(Disposable.Create(() =>
        {
            if (_pending == pending)
                _pending = null;
        }));
        _starting = true;
        try
        {
            waiting.Wait(seconds, (WaitingController.Type)kind, 0f, options.FreezeNeeds);
        }
        catch
        {
            _pending = null;
            throw;
        }
        finally
        {
            _starting = false;
        }
    }

    /// <summary>The game is starting a wait; one that isn't ours replaces ours.</summary>
    public static void GameWaitStarting()
    {
        if (!_starting)
            FinishInterrupted();
    }

    /// <summary>The game's wake-up call ended the rest that lasted <paramref name="seconds"/>.</summary>
    public static void Ended(int seconds) => Finish(seconds, false);

    public static void FinishInterrupted()
    {
        if (_pending == null)
            return;
        var waiting = WaitingController.instance;
        Finish(waiting != null ? waiting.sleepingTimeSeconds : 0, true);
    }

    private static void Finish(int seconds, bool interrupted)
    {
        var done = _pending;
        if (done == null)
            return;
        _pending = null;
        try
        {
            done.OnDone(new WaitResult(seconds, interrupted));
        }
        catch (Exception e)
        {
            GameEvents.Log.Error($"The wait callback of {done.Owner} threw", e);
        }
    }

    private sealed class Pending
    {
        public string Owner;
        public bool FreezeNeeds;
        public bool NoPassOut;
        public string Label;
        public Action<WaitResult> OnDone;
    }
}
