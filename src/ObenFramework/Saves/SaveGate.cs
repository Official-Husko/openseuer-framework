// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Saves;

/// <summary>
/// Knows whether the game is in the middle of a save or a load, built from the save events rather
/// than <c>SaveController.Loading</c> (which turns false before the scene has even loaded). Use it
/// to hold back anything that must not run while the game's save lists are in use: capturing or
/// applying object state, writing to the world, sending a host's state to a player.
/// </summary>
public static class SaveGate
{
    private static readonly SaveGateState State = new SaveGateState();

    private static ModLogger _log;

    /// <summary>A save is being written right now.</summary>
    public static bool IsSaving => State.IsSaving(Time.frameCount);

    /// <summary>A level change or a save load is under way: from the announcement until the game finished applying it.</summary>
    public static bool IsLoading
    {
        get
        {
            bool loading = State.IsLoading(Time.frameCount, Time.realtimeSinceStartup, SaveController.Loading, ScreenIdle());
            if (State.DroppedStuckLoad)
            {
                State.ClearDroppedFlag();
                _log?.Warn("A save load never finished; the save gate was reopened.");
            }
            return loading;
        }
    }

    /// <summary>A save or a load is in flight. Do not touch the game's save lists or the restored world while this is true.</summary>
    public static bool IsBusy => IsSaving || IsLoading;

    /// <summary>
    /// Runs <paramref name="action"/> once the game is idle: now if it already is, otherwise on the
    /// first frame after the save or load ended. It is dropped if <paramref name="owner"/> unloads
    /// first. An exception inside it is logged and does not stop the other waiting actions.
    /// </summary>
    public static void WhenIdle(ITracker owner, Action action)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (action == null) throw new ArgumentNullException(nameof(action));

        if (!IsBusy)
        {
            Run(action);
            return;
        }
        SaveGatePump.Enqueue(owner, action);
    }

    internal static void Init(ObenMod framework)
    {
        _log = framework.Log;
        State.Reset();
        GameEvents.LevelChanging.Subscribe(framework, () => BeginLoad(false));
        GameEvents.Loading.Subscribe(framework, () => BeginLoad(true));
        GameEvents.Loaded.Subscribe(framework, State.LoadDone);
        GameEvents.LoadAborted.Subscribe(framework, _ => State.LoadDone());
        GameEvents.MainMenu.Subscribe(framework, State.Reset);
        GameEvents.Saving.Subscribe(framework, () => State.SavingStarted(Time.frameCount));
        GameEvents.Saved.Subscribe(framework, _ => State.SavingDone());

        // The game raises this one before the new scene exists; no framework event covers it.
        SaveController.PlayerWillLoadGame += OnWillLoad;
        framework.Track(Disposable.Create(() =>
        {
            SaveController.PlayerWillLoadGame -= OnWillLoad;
            State.Reset();
        }));
        SaveGatePump.Create(framework);
    }

    internal static void Run(Action action)
    {
        try { action(); }
        catch (Exception e) { _log.Error("A WhenIdle action threw", e); }
    }

    private static void OnWillLoad()
    {
        try { BeginLoad(false); }
        catch (Exception e) { _log?.Error("SaveGate failed on PlayerWillLoadGame", e); }
    }

    private static void BeginLoad(bool started) =>
        State.BeginLoad(Time.frameCount, Time.realtimeSinceStartup, started);

    private static bool ScreenIdle()
    {
        var screen = LoadingScreen.instance;
        return screen != null && screen.LevelLoadDone && !screen.FadeActive && !screen.Fading;
    }
}
