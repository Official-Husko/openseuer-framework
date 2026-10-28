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
    // A pending load is dropped after this long even if nothing says it ended, so a load the game
    // abandoned can never leave the gate shut for the rest of the session.
    private const float StuckSeconds = 300f;

    private static ModLogger _log;

    // A save runs inside a single call, so it is "in flight" only for the frame it started in.
    // A save that threw never raises Saved, and this still lets go on the next frame.
    private static int _savingFrame = -1;

    // A level change or load was announced and LoadingDone has not come yet.
    private static bool _loadPending;
    private static bool _loadStarted;
    private static int _pendingFrame;
    private static float _pendingSince;

    /// <summary>A save is being written right now.</summary>
    public static bool IsSaving => _savingFrame == Time.frameCount;

    /// <summary>A level change or a save load is under way: from the announcement until the game finished applying it.</summary>
    public static bool IsLoading
    {
        get
        {
            if (_loadPending)
                DropIfAbandoned();
            return _loadPending;
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
        Reset();
        GameEvents.LevelChanging.Subscribe(framework, () => BeginLoad(false));
        GameEvents.Loading.Subscribe(framework, () => BeginLoad(true));
        GameEvents.Loaded.Subscribe(framework, Reset);
        GameEvents.MainMenu.Subscribe(framework, Reset);
        GameEvents.Saving.Subscribe(framework, () => _savingFrame = Time.frameCount);
        GameEvents.Saved.Subscribe(framework, _ => _savingFrame = -1);

        // The game raises this one before the new scene exists; no framework event covers it.
        SaveController.PlayerWillLoadGame += OnWillLoad;
        framework.Track(Disposable.Create(() =>
        {
            SaveController.PlayerWillLoadGame -= OnWillLoad;
            Reset();
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

    private static void BeginLoad(bool started)
    {
        if (!_loadPending)
        {
            _loadPending = true;
            _pendingFrame = Time.frameCount;
            _pendingSince = Time.realtimeSinceStartup;
        }
        if (started)
            _loadStarted = true;
    }

    private static void Reset()
    {
        _loadPending = false;
        _loadStarted = false;
        _savingFrame = -1;
    }

    // Loads the game abandons (file missing, aborted migration, a level change that only saved)
    // never raise LoadingDone. Between LoadingStarted and LoadingDone the game runs in one go
    // apart from a single frame, so an idle loading screen only proves an abandoned load before that.
    private static void DropIfAbandoned()
    {
        if (Time.realtimeSinceStartup - _pendingSince > StuckSeconds)
        {
            _log?.Warn("A save load never finished; the save gate was reopened.");
            Reset();
            return;
        }
        if (_loadStarted || Time.frameCount <= _pendingFrame || SaveController.Loading)
            return;

        var screen = LoadingScreen.instance;
        if (screen != null && screen.LevelLoadDone && !screen.FadeActive && !screen.Fading)
            Reset();
    }
}
