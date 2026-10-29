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
    private static readonly AccessTools.FieldRef<GameController, System.Collections.Generic.List<GameObject>> KeyHolders =
        AccessTools.FieldRefAccess<GameController, System.Collections.Generic.List<GameObject>>("disabledKeysList");
    private static readonly AccessTools.FieldRef<GameController, System.Collections.Generic.List<GameObject>> CursorHolders =
        AccessTools.FieldRefAccess<GameController, System.Collections.Generic.List<GameObject>>("disabledCursorList");

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
            CloseGameMenu();
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

    // A game menu (inventory, map) and a mod window both wanting the keys don't mix: the window wins, as in
    // the reference framework. The game's own close also gives back the menu's control lock.
    private static void CloseGameMenu()
    {
        var ui = GameUIController.instance;
        if (ui == null || !ui.IsMenuVisible)
            return;
        try
        {
            ui.HideGameMenu(forceHide: true);
        }
        catch (Exception e)
        {
            UIKitMod.Instance?.Log.Error("Closing the game's menu for a mod window failed", e);
        }
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
        catch (Exception e)
        {
            UIKitMod.Instance?.Log.Error("Giving the controls back failed; freeing the keys and cursor directly", e);
            Recover(game);
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

    // ControlsEnabled drops our entry from its lists first and can throw before it frees the keys (it reaches
    // camera singletons unguarded). Free what no one else holds, so the player isn't left stuck.
    private static void Recover(GameController game)
    {
        try
        {
            if (KeyHolders(game).Count == 0)
                game.keysDisabled = false;
            if (CursorHolders(game).Count == 0)
            {
                game.cursorVisible = false;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        catch (Exception e)
        {
            UIKitMod.Instance?.Log.Error("Freeing the keys failed too", e);
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
