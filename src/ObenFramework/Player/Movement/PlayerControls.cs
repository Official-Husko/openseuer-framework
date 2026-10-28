// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>
/// Holds the player still for a mod: <see cref="Lock"/> stops walking, the camera and the game's keys the way
/// dialogue does; <see cref="LockLook"/> only stops the camera turning. Locks are held, not set: the player
/// moves again when the last mod lets go, and a lock still held is put back after a level load. The game
/// keeps its own locks per object, so these never clash with a menu or a chair.
/// </summary>
[HarmonyPatch]
public static class PlayerControls
{
    private static readonly List<object> Locks = new List<object>();
    private static readonly List<object> LookLocks = new List<object>();
    private static GameObject _holder;
    private static GameController _lockedOn;
    private static bool _cursor;
    private static bool _ownCall;

    // The game's own wish for the camera rotation, so letting go of a look lock doesn't undo the game's.
    private static bool _gameStopsRotation;

    /// <summary>A mod holds the player still.</summary>
    public static bool Locked => Locks.Count > 0;

    /// <summary>A mod holds the camera still.</summary>
    public static bool LookLocked => LookLocks.Count > 0;

    /// <summary>
    /// Stops movement, camera and keys until the result is disposed or <paramref name="owner"/> unloads. With
    /// <paramref name="showCursor"/> the mouse cursor shows (for a mod window the player clicks in).
    /// </summary>
    public static IDisposable Lock(ObenMod owner, bool showCursor = false)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var token = new object();
        Locks.Add(token);
        _cursor |= showCursor;
        ApplyLock();
        return owner.Track(Disposable.Create(() =>
        {
            if (Locks.Remove(token) && Locks.Count == 0)
                ReleaseLock();
        }));
    }

    /// <summary>Stops the camera turning (walking still works) until the result is disposed or the owner unloads.</summary>
    public static IDisposable LockLook(ObenMod owner)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        var token = new object();
        LookLocks.Add(token);
        ApplyLook();
        return owner.Track(Disposable.Create(() =>
        {
            if (LookLocks.Remove(token) && LookLocks.Count == 0)
                ApplyLook();
        }));
    }

    internal static void Init(ObenMod framework)
    {
        _holder = framework.CreateObject("PlayerControls");
        GameEvents.WorldReady.Subscribe(framework, _ =>
        {
            _lockedOn = null;
            _gameStopsRotation = false;
            ApplyLock();
            ApplyLook();
        });
        framework.Track(Disposable.Create(() =>
        {
            bool look = LookLocks.Count > 0;
            Locks.Clear();
            LookLocks.Clear();
            ReleaseLock();
            if (look)
                ApplyLook();
            _holder = null;
        }));
    }

    private static void ApplyLock()
    {
        var game = GameController.instance;
        if (Locks.Count == 0 || game == null || _holder == null || game == _lockedOn)
            return;
        try
        {
            game.ControlsDisabled(_holder, _cursor);
            _lockedOn = game;
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Locking the player's controls failed", e);
        }
    }

    private static void ReleaseLock()
    {
        var game = _lockedOn;
        _lockedOn = null;
        _cursor = false;
        if (game == null || _holder == null)
            return;
        try
        {
            game.ControlsEnabled(_holder);
        }
        catch (Exception e)
        {
            GameEvents.Log.Error("Unlocking the player's controls failed", e);
        }
    }

    private static void ApplyLook()
    {
        var camera = ThirdPersonCameraController.instance;
        if (camera == null)
            return;
        _ownCall = true;
        try
        {
            if (LookLocks.Count > 0 || _gameStopsRotation)
                camera.DisableRotation();
            else
                camera.EnableRotation();
        }
        finally
        {
            _ownCall = false;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(ThirdPersonCameraController), nameof(ThirdPersonCameraController.DisableRotation))]
    private static void AfterDisableRotation()
    {
        if (!_ownCall)
            _gameStopsRotation = true;
    }

    // The game turns rotation back on after its own uses (menus, cutscenes); a mod's look lock outlasts those.
    [HarmonyPostfix]
    [HarmonyPatch(typeof(ThirdPersonCameraController), nameof(ThirdPersonCameraController.EnableRotation))]
    private static void AfterEnableRotation(ThirdPersonCameraController __instance)
    {
        if (_ownCall)
            return;
        _gameStopsRotation = false;
        if (LookLocks.Count == 0)
            return;
        _ownCall = true;
        __instance.DisableRotation();
        _ownCall = false;
    }
}
