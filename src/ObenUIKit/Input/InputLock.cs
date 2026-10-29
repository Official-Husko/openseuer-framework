using System;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenUIKit.Input;

/// <summary>
/// Stops the game from reacting to keys, mouse look and movement while a mod window is open, and
/// shows the cursor. Every open window holds one lease; the lock goes away with the last one.
/// In a level this goes through GameController's own per-object lock, so it stacks safely with the
/// game's menus; at the main menu (no GameController) the menu's keyboard navigation is paused.
/// </summary>
public static class InputLock
{
    private static readonly AccessTools.FieldRef<ThirdPersonCameraController, bool> MoveDisabled =
        AccessTools.FieldRefAccess<ThirdPersonCameraController, bool>("moveDisabled");
    private static readonly AccessTools.FieldRef<ThirdPersonCameraController, bool> FirstPersonMoveDisabled =
        AccessTools.FieldRefAccess<ThirdPersonCameraController, bool>("firstpersonMoveDisabled");

    private static GameObject _owner;
    private static int _leases;
    private static bool _menuNavigationWasOn;
    private static bool _menuLocked;

    public static bool IsLocked => _leases > 0;

    /// <summary>Locks game input until the returned handle is disposed (safe to dispose twice).</summary>
    public static IDisposable Acquire()
    {
        if (_leases++ == 0)
        {
            // The game's ControlsDisabled reaches camera singletons unguarded; a failed lock must not count.
            try { Apply(); }
            catch
            {
                _leases--;
                throw;
            }
        }
        return Disposable.Create(Release);
    }

    internal static void Init(ObenMod kit)
    {
        _owner = kit.CreateObject("InputLock");
        _leases = 0;
        // A level change while a window is open brings a new GameController; lock that one too.
        GameEvents.SceneLoaded.Subscribe(kit, _ => { if (IsLocked) Apply(); });
        kit.Track(Disposable.Create(ReleaseAll));
    }

    private static void Release()
    {
        if (_leases > 0 && --_leases == 0)
            Remove();
    }

    private static void ReleaseAll()
    {
        if (_leases > 0)
            Remove();
        _leases = 0;
    }

    private static void Apply()
    {
        var game = GameController.instance;
        if (game != null)
        {
            game.ControlsDisabled(_owner, ShowCursor: true);
            return;
        }

        var menu = MainMenuNavigationHandler.Instance;
        if (menu != null && !_menuLocked)
        {
            _menuNavigationWasOn = menu.ControlsEnabled;
            menu.ControlsEnabled = false;
            _menuLocked = true;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // GameController.ControlsEnabled also frees the camera unconditionally, which would undo a camera lock
    // something else holds (sitting at a computer, lying in bed, a cutscene). Keep those as they were.
    private static void Unlock(GameController game)
    {
        var camera = ThirdPersonCameraController.instance;
        bool move = camera != null && MoveDisabled(camera);
        bool firstPerson = camera != null && FirstPersonMoveDisabled(camera);
        try
        {
            game.ControlsEnabled(_owner);
        }
        finally
        {
            if (camera != null)
            {
                MoveDisabled(camera) = move;
                FirstPersonMoveDisabled(camera) = firstPerson;
            }
        }
    }

    private static void Remove()
    {
        var game = GameController.instance;
        if (game != null && _owner != null)
            Unlock(game);

        var menu = MainMenuNavigationHandler.Instance;
        if (_menuLocked && menu != null)
            menu.ControlsEnabled = _menuNavigationWasOn;
        _menuLocked = false;
    }
}
