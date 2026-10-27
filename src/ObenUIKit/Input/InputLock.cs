using System;
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
    private static GameObject _owner;
    private static int _leases;
    private static bool _menuNavigationWasOn;
    private static bool _menuLocked;

    public static bool IsLocked => _leases > 0;

    /// <summary>Locks game input until the returned handle is disposed (safe to dispose twice).</summary>
    public static IDisposable Acquire()
    {
        if (_leases++ == 0)
            Apply();
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

    private static void Remove()
    {
        var game = GameController.instance;
        if (game != null && _owner != null)
            game.ControlsEnabled(_owner);

        var menu = MainMenuNavigationHandler.Instance;
        if (_menuLocked && menu != null)
            menu.ControlsEnabled = _menuNavigationWasOn;
        _menuLocked = false;
    }
}
