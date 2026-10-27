using System;
using ObenKit.Game;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;

namespace ObenKit.Events;

/// <summary>
/// Game-level moments mods usually care about, on the main thread. Every handler runs in its own
/// try/catch: the game raises its save and load events without one, so a single throwing handler
/// there could leave a black screen or block level changes.
/// </summary>
public static class GameEvents
{
    internal static ModLogger Log;

    /// <summary>The main menu scene finished loading.</summary>
    public static readonly GameEvent MainMenu = new GameEvent(nameof(MainMenu));

    /// <summary>Any scene finished loading; the payload is its name.</summary>
    public static readonly GameEvent<string> SceneLoaded = new GameEvent<string>(nameof(SceneLoaded));

    /// <summary>
    /// A playable world is up: the save is applied, the player exists and has been moved to the
    /// entry point. Fires once per level load (doors and fast travel reload the level too). The
    /// payload is the scene name.
    /// </summary>
    public static readonly GameEvent<string> WorldReady = new GameEvent<string>(nameof(WorldReady));

    /// <summary>The player is about to leave the level (door, fast travel, prison). A save and a reload follow.</summary>
    public static readonly GameEvent LevelChanging = new GameEvent(nameof(LevelChanging));

    /// <summary>The game started writing a save.</summary>
    public static readonly GameEvent Saving = new GameEvent(nameof(Saving));

    /// <summary>A save was written. The payload is the save folder; mods keep their own files there.</summary>
    public static readonly GameEvent<string> Saved = new GameEvent<string>(nameof(Saved));

    /// <summary>A save started loading (from the menu, a door, or a reload).</summary>
    public static readonly GameEvent Loading = new GameEvent(nameof(Loading));

    /// <summary>
    /// The save's data was applied. The player may still be moved to the entry point a moment later;
    /// wait for <see cref="WorldReady"/> before touching the world.
    /// </summary>
    public static readonly GameEvent Loaded = new GameEvent(nameof(Loaded));

    /// <summary>
    /// Runs <paramref name="action"/> every time the world becomes ready, and right away if it already
    /// is. Use this instead of subscribing to WorldReady when a mod can be (re)loaded mid-game.
    /// </summary>
    public static void WhenWorldReady(ITracker owner, Action<string> action)
    {
        WorldReady.Subscribe(owner, action);
        if (WorldState.IsReady)
        {
            try { action(WorldState.SceneName); }
            catch (Exception e) { Log.Error("A WorldReady handler threw", e); }
        }
    }

    internal static void Init(ObenMod kit)
    {
        Log = kit.Log;
        SaveHooks.Attach(kit);
        SceneHooks.Attach(kit);
    }
}
