// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.NewGames;
using ObenLoader.Runtime;

namespace ObenFramework.Story;

/// <summary>
/// The story modules' timing. Things noticed inside the game's own calls (a crime, a fine, a quest
/// finishing, a dialogue choice) are queued and run at the end of the frame, outside the game's call and
/// never while a save is written or loaded. The queue survives a level change and is dropped at the main
/// menu or when another character's save loads.
/// <see cref="Ready"/> tells mods when the quest and dialogue state of a session can be written.
/// </summary>
public static partial class StoryPump
{
    private static readonly StoryQueue Queue = new StoryQueue();
    private static readonly RetryGate ReadyGate = new RetryGate();
    private static readonly HashSet<ITracker> TrackedOwners = new HashSet<ITracker>();

    private static SessionKind _readyKind;

    /// <summary>
    /// The quests and dialogue variables of a new session can be written: raised once per session (each
    /// save load, a new game's first level, a level loaded from the menu), a few frames after the session
    /// started, once the task panel and notifications exist and no save is in progress. Level changes don't
    /// raise it again. Create a mod's quests and default variables here, and make the handler safe to run more
    /// than once, since loading the same save again raises it again. Each handler runs in its own try/catch.
    /// </summary>
    public static readonly GameEvent<SessionKind> Ready = new GameEvent<SessionKind>(nameof(Ready));

    /// <summary><see cref="Ready"/> was raised for the running session.</summary>
    public static bool IsReady => ReadyGate.Done && Sessions.Current != SessionKind.None;

    /// <summary>Actions waiting to run.</summary>
    public static int Pending => Queue.Count;

    /// <summary>Waiting for the quest system so <see cref="Ready"/> can be raised.</summary>
    public static bool WaitingForReady => ReadyGate.Waiting;

    /// <summary>
    /// Runs <paramref name="action"/> when the running session is ready (now, if <see cref="Ready"/> was
    /// already raised) and again at every later <see cref="Ready"/>.
    /// </summary>
    public static void WhenReady(ITracker owner, Action<SessionKind> action)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (action == null) throw new ArgumentNullException(nameof(action));
        Ready.Subscribe(owner, action);
        if (!IsReady)
            return;
        try { action(_readyKind); }
        catch (Exception e) { GameEvents.Log.Error("A StoryPump.Ready handler threw", e); }
    }

    /// <summary>
    /// Runs <paramref name="action"/> at the end of this frame, or later if a save or load is running. Use it
    /// from inside a Harmony patch or a game event, where changing quests or dialogue would interfere with
    /// the game's own call. It is dropped when <paramref name="owner"/> unloads first, at the main menu, or
    /// when another character's save loads. An exception inside it is logged.
    /// </summary>
    public static void Defer(ITracker owner, Action action, string name = null)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (TrackedOwners.Add(owner))
            owner.Track(Disposable.Create(() => Forget(owner)));
        Enqueue(owner, name ?? action.Method.Name, action);
    }

    /// <summary>The names of the waiting actions, oldest first.</summary>
    public static List<string> PendingNames() => Queue.Names();

    internal static void Enqueue(object owner, string name, Action action)
    {
        Queue.Add(owner, name, action);
        StoryDriver.Wake();
    }

    private static void Forget(ITracker owner)
    {
        TrackedOwners.Remove(owner);
        Queue.RemoveOwner(owner);
    }
}
