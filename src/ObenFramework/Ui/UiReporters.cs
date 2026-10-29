using System;
using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenFramework.Ui;

/// <summary>What a UI mod reports to <see cref="UiState.Report"/>.</summary>
public enum UiActivity
{
    /// <summary>A window that takes the player's input (mouse, keys) is open.</summary>
    ModWindow,

    /// <summary>A text field or a key capture of the mod has the keyboard.</summary>
    Typing,

    /// <summary>Some other menu-like screen of the mod is open (counts for <see cref="UiState.IsMenuOpen"/>).</summary>
    Menu,
}

/// <summary>The functions UI mods registered, by kind. Read when a <see cref="UiState"/> property is asked.</summary>
internal static class UiReporters
{
    private static readonly List<Func<bool>>[] ByKind =
    {
        new List<Func<bool>>(),
        new List<Func<bool>>(),
        new List<Func<bool>>(),
    };

    public static IDisposable Add(UiActivity kind, Func<bool> active)
    {
        var list = ByKind[(int)kind];
        list.Add(active);
        return Disposable.Create(() =>
        {
            list.Remove(active);
            UiState.Changed();
        });
    }

    public static bool Any(UiActivity kind)
    {
        var list = ByKind[(int)kind];
        bool removed = false;
        bool any = false;
        for (int i = 0; i < list.Count && !any; i++)
        {
            try
            {
                any = list[i]();
            }
            catch (Exception e)
            {
                // A broken reporter would throw on every read; drop it.
                Events.GameEvents.Log?.Error($"A UiState {kind} reporter threw and was removed", e);
                list.RemoveAt(i--);
                removed = true;
            }
        }
        // Dropping one can change the answer, and listeners only hear about changes through here.
        if (removed)
            UiState.Changed();
        return any;
    }

    public static void Clear()
    {
        foreach (var list in ByKind)
            list.Clear();
    }
}

/// <summary>Every <see cref="UiState"/> flag at one moment.</summary>
public struct UiSnapshot
{
    public bool InMainMenu;
    public bool InWorld;
    public bool Loading;
    public bool Paused;
    public bool PauseMenu;
    public bool GameMenu;
    public bool Shop;
    public bool Dialogue;
    public bool Console;
    public bool ModWindow;
    public bool Typing;
    public bool RebindingKey;
    public bool InputLocked;
    public bool CursorVisible;
    public bool InventoryDisabled;
    public bool HudHidden;
    public bool Menu;
    public bool Busy;
}
