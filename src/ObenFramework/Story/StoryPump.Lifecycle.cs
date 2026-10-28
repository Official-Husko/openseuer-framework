// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Dialogue;
using ObenFramework.Events;
using ObenFramework.NewGames;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Story;

public static partial class StoryPump
{
    private static readonly Action<string, Exception> LogFailure = (name, e) => StoryLog.Failed("Story action " + name, e);

    private static bool _levelChange;

    /// <summary>The quest state can take writes: a loaded world with its task panel, and no save or load running.</summary>
    internal static bool QuestsWritable =>
        TaskController.instance != null && TaskPanel.instance != null && Notifications.instance != null
        && !SaveGate.IsBusy && LuaState.CanWrite;

    internal static void Init(ObenMod framework)
    {
        StoryDriver.Create(framework);
        framework.Track(Disposable.Create(() =>
        {
            Queue.Clear();
            ReadyGate.Reset();
            TrackedOwners.Clear();
        }));

        Sessions.Started.Subscribe(framework, OnSessionStarted);
        GameEvents.MainMenu.Subscribe(framework, OnMainMenu);
        GameEvents.LevelChanging.Subscribe(framework, () => _levelChange = true);
        GameEvents.WorldReady.Subscribe(framework, _ => _levelChange = false);
        SaveController.PlayerWillLoadGame += OnWillLoad;
        framework.Track(Disposable.Create(() => SaveController.PlayerWillLoadGame -= OnWillLoad));

        // Loaded into a running world (hot reload): the session is already going, so get ready for it now.
        if (Sessions.IsRunning)
            OnSessionStarted(Sessions.Current);
    }

    /// <summary>The shared tick: runs the queue and tries the ready gate. Returns true while there is more to do.</summary>
    internal static bool Tick()
    {
        if (ReadyGate.Waiting)
            TryReady();
        if (Queue.Count > 0 && !SaveGate.IsBusy)
            Queue.Run(LogFailure);
        return Queue.Count > 0 || ReadyGate.Waiting;
    }

    private static void OnSessionStarted(SessionKind kind)
    {
        _readyKind = kind;
        ReadyGate.Reset();
        ReadyGate.Arm();
        StoryDriver.Wake();
    }

    private static void TryReady()
    {
        if (ReadyGate.Try(QuestsWritable))
        {
            Ready.Raise(_readyKind, GameEvents.Log);
            return;
        }
        if (ReadyGate.GaveUp)
            StoryLog.Refused("StoryPump.Ready", "the task panel or notifications never appeared, so the story ready event was not raised for this session");
    }

    private static void OnMainMenu()
    {
        Queue.Clear();
        ReadyGate.Reset();
        _levelChange = false;
    }

    // A level change saves and loads the same game, so what was queued still belongs to it. Any other load
    // replaces the world, and actions queued for the old one must not run in the new one.
    private static void OnWillLoad()
    {
        try
        {
            if (_levelChange)
                return;
            Queue.Clear();
            ReadyGate.Reset();
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("StoryPump failed on PlayerWillLoadGame", e);
        }
    }
}
