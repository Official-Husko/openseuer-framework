using System;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.NewGames;

/// <summary>
/// Play sessions: a session starts when a save is loaded or a new game reaches its first level, and
/// runs until the next load or the main menu. Level changes (doors, fast travel) reload the level from
/// a fresh save but continue the session, so they don't start a new one. Use <see cref="Started"/> for
/// things that should happen once per loaded game rather than once per level.
/// </summary>
public static class Sessions
{
    private const string ReloadKey = "ObenFramework.Sessions.Current";
    private const string IntroScene = "Open Sewer Intro";

    private static readonly SessionRules Rules = new SessionRules();

    /// <summary>
    /// A session started: raised on the first <see cref="GameEvents.WorldReady"/> after a save load, after a
    /// new game's intro, or after a level was loaded straight from the menu. Not raised for level changes.
    /// Each handler runs in its own try/catch.
    /// </summary>
    public static readonly GameEvent<SessionKind> Started = new GameEvent<SessionKind>(nameof(Started));

    /// <summary>How the running session began; <see cref="SessionKind.None"/> in the main menu.</summary>
    public static SessionKind Current => Rules.Current;

    /// <summary>A session is running and its world is ready to play.</summary>
    public static bool IsRunning => Rules.Current != SessionKind.None && WorldState.IsReady;

    /// <summary>The running session began as a new game (and no save has been loaded since).</summary>
    public static bool IsNewGame => Rules.Current == SessionKind.NewGame;

    /// <summary>
    /// Runs <paramref name="action"/> at the start of every session, and right away when one is already
    /// running. Use it for setup that belongs to a loaded game, such as creating a mod's quests.
    /// </summary>
    public static void WhenStarted(ITracker owner, Action<SessionKind> action)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (action == null) throw new ArgumentNullException(nameof(action));
        Started.Subscribe(owner, action);
        if (!IsRunning)
            return;
        try { action(Rules.Current); }
        catch (Exception e) { GameEvents.Log.Error("A Sessions.Started handler threw", e); }
    }

    internal static void Init(ObenMod framework)
    {
        // Kept as an int: the enum type of the old copy is not the new copy's after a reload.
        ReloadState.Take(ReloadKey, out int previous);
        Rules.Resume((SessionKind)previous, WorldState.InWorld);
        framework.Track(Disposable.Create(() => ReloadState.Put(ReloadKey, (int)Rules.Current)));

        GameEvents.MainMenu.Subscribe(framework, Rules.MainMenu);
        GameEvents.LevelChanging.Subscribe(framework, Rules.LevelChanging);
        GameEvents.WorldReady.Subscribe(framework, OnWorldReady);

        // The game announces a load before the old scene goes away; no framework event covers that point.
        SaveController.PlayerWillLoadGame += OnWillLoad;
        framework.Track(Disposable.Create(() => SaveController.PlayerWillLoadGame -= OnWillLoad));
    }

    internal static void NewGameStarted() => Rules.NewGameStarted();

    private static void OnWillLoad()
    {
        try { Rules.WillLoad(); }
        catch (Exception e) { GameEvents.Log.Error("Sessions failed on PlayerWillLoadGame", e); }
    }

    private static void OnWorldReady(string scene)
    {
        var kind = Rules.WorldReady(string.Equals(scene, IntroScene, StringComparison.OrdinalIgnoreCase));
        if (kind != SessionKind.None)
            Started.Raise(kind, GameEvents.Log);
    }
}
