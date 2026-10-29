using System;
using ObenUIKit.Controls;
using UnityEngine;

namespace ObenUIKit.Windows;

/// <summary>Where a window sits on the screen.</summary>
public enum WindowDock
{
    /// <summary>The pause menu's centred column (or a centred box with a height), the scene dimmed behind.</summary>
    Center,

    /// <summary>A column along the left edge; the scene stays visible.</summary>
    Left,

    /// <summary>A column along the right edge; the scene stays visible.</summary>
    Right,

    /// <summary>A box the player can drag by its title, anywhere on the screen.</summary>
    Floating,

    /// <summary>
    /// The whole screen less a margin, the scene dimmed behind. Can have a side panel along the left
    /// (<see cref="WindowOptions.Side"/>), for a list to pick from beside the page that shows the pick.
    /// </summary>
    Full,
}

/// <summary>Why a window closed. Read <see cref="UiWindow.CloseReason"/> in a <see cref="UiWindow.Closed"/> handler.</summary>
public enum WindowCloseReason
{
    /// <summary>Still open.</summary>
    None,

    /// <summary><see cref="UiWindow.Close()"/>, a Back button, <see cref="UiKit.CloseAll"/>.</summary>
    Requested,

    /// <summary>Esc or the pad's B.</summary>
    Escape,

    /// <summary>The mod (or feature) that opened it unloaded or was switched off.</summary>
    OwnerUnloaded,

    /// <summary>The player changed level.</summary>
    LevelChange,

    /// <summary>One of its callbacks threw and the window was opened with <see cref="WindowOptions.CloseOnError"/>.</summary>
    Error,

    /// <summary>The kit itself unloaded.</summary>
    KitUnloaded,

    /// <summary>A save started loading (a level change of its own; <see cref="WindowOptions.KeepOnLevelChange"/> keeps the window).</summary>
    SaveLoad,

    /// <summary>Something destroyed the window's objects without closing it.</summary>
    Destroyed,
}

/// <summary>
/// How a window looks and behaves. Pass one to <see cref="UiKit.Open(ObenLoader.Runtime.ITracker, string, string, Action{UiBuilder}, WindowOptions)"/>;
/// every property has a default, so set only what you need.
/// </summary>
public sealed class WindowOptions
{
    /// <summary>Where the window sits. Default: the centred column.</summary>
    public WindowDock Dock { get; set; } = WindowDock.Center;

    /// <summary>Width in the kit's 1920x1080 units. <see cref="WindowDock.Full"/> windows take the screen's width.</summary>
    public float Width { get; set; } = UiKit.DefaultWidth;

    /// <summary>
    /// Height in the kit's units. 0 means the full height of the screen for <see cref="WindowDock.Center"/>,
    /// <see cref="WindowDock.Left"/> and <see cref="WindowDock.Right"/>, and 640 for a floating window.
    /// </summary>
    public float Height { get; set; }

    /// <summary>Floating windows: where the window's centre goes, measured from the screen's centre in the kit's units. Default: the centre.</summary>
    public Vector2 Position { get; set; }

    /// <summary>
    /// Full-height <see cref="WindowDock.Left"/> and <see cref="WindowDock.Right"/> columns: room kept free above
    /// the column, in the kit's units (150 clears the game's two money counters in the top-right corner). Default 0.
    /// </summary>
    public float Top { get; set; }

    /// <summary>
    /// <see cref="WindowDock.Full"/> windows: builds a panel along the left of the body, with its own scrolling.
    /// It is built again with the body (<see cref="UiWindow.Rebuild"/>), and keyboard navigation runs through it
    /// first.
    /// </summary>
    public Action<UiBuilder> Side { get; set; }

    /// <summary>Width of the <see cref="Side"/> panel in the kit's units. Default 440.</summary>
    public float SideWidth { get; set; } = 440f;

    /// <summary>Builds the bottom bar. Without one the window gets a Back button.</summary>
    public Action<UiBuilder> Footer { get; set; }

    /// <summary>
    /// The window takes the player's input: game keys are locked, the cursor shows, keyboard navigation and
    /// Esc work. Turn it off for a display-only panel (a tracker, a readout) that stays up while the player plays.
    /// </summary>
    public bool LockInput { get; set; } = true;

    /// <summary>Darken the scene behind. Default: only for the centred column and full-screen windows.</summary>
    public bool? Dim { get; set; }

    /// <summary>Esc (or the pad's B) closes the window. Only for windows that lock input.</summary>
    public bool CloseOnEscape { get; set; } = true;

    /// <summary>
    /// Close the window as soon as one of its callbacks throws (the error is logged either way). Off by
    /// default: the window stays open and the failing control keeps its old value.
    /// </summary>
    public bool CloseOnError { get; set; }

    /// <summary>Keep the window open through a level change or a save loading (it closes by default, like the game's menus).</summary>
    public bool KeepOnLevelChange { get; set; }

    internal bool Dims => Dim ?? (Dock == WindowDock.Center || Dock == WindowDock.Full);

    internal float ResolvedHeight => Dock == WindowDock.Full ? 0f : Height > 0f ? Height : Dock == WindowDock.Floating ? 640f : 0f;

    internal WindowOptions Copy() => (WindowOptions)MemberwiseClone();
}
