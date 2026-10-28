using System;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Runtime;
using ObenUIKit.Theme;
using ObenUIKit.Windows;

namespace ObenUIKit;

/// <summary>
/// The kit's front door. Windows belong to the mod that opens them (any <see cref="ITracker"/>:
/// a mod or one of its features) and close when it unloads.
/// </summary>
public static class UiKit
{
    public const float DefaultWidth = 760f;

    /// <summary>The look in use right now (main menu or level, whichever fits).</summary>
    public static UiTheme Theme => Kit.Theme;

    /// <summary>True while a kit window that takes the player's input is open.</summary>
    public static bool AnyWindowOpen => KitRuntime.Current != null && KitRuntime.Current.Kit.Windows.AnyModal;

    /// <summary>
    /// Opens a window and fills it with <paramref name="build"/>. Without a <paramref name="footer"/>
    /// it gets a Back button. A <paramref name="height"/> above 0 makes a centred box instead of the
    /// full-height column. Opening a window with an id that's already open brings back the open one.
    /// </summary>
    public static UiWindow Open(ITracker owner, string id, string title, Action<UiBuilder> build,
        Action<UiBuilder> footer = null, float width = DefaultWidth, float height = 0f) =>
        Open(owner, id, title, build, new WindowOptions { Footer = footer, Width = width, Height = height });

    /// <summary>
    /// Opens a window with the given <paramref name="options"/>: docked to an edge, floating, display-only,
    /// closing on errors. Opening a window with an id that's already open brings back the open one.
    /// </summary>
    public static UiWindow Open(ITracker owner, string id, string title, Action<UiBuilder> build, WindowOptions options)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        foreach (var open in Kit.Windows.Open)
        {
            if (open.Id == id)
            {
                Kit.Windows.Raise(open);
                return open;
            }
        }
        return new UiWindow(Kit, owner, id, title, build, (options ?? new WindowOptions()).Copy());
    }

    /// <summary>The open window with this id, or null.</summary>
    public static UiWindow Find(string id)
    {
        if (KitRuntime.Current == null)
            return null;
        foreach (var open in Kit.Windows.Open)
        {
            if (open.Id == id)
                return open;
        }
        return null;
    }

    /// <summary>
    /// Asks a yes or no question in a small window on top of everything. <paramref name="onYes"/> runs
    /// after the window closes; No, Esc and the pad's B just close it.
    /// </summary>
    public static UiWindow Confirm(ITracker owner, string title, string text, Action onYes,
        string yes = "Yes", string no = "No") =>
        Dialogs.Confirm(Kit, owner, title, text, onYes, yes, no);

    /// <summary>
    /// Shows a short notice: the game's own notification in a level, a small notice in the corner
    /// at the main menu.
    /// </summary>
    public static void Toast(string text, string title = "Notice") => Toasts.Show(Kit, title, text);

    /// <summary>Closes every kit window.</summary>
    public static void CloseAll() => Kit.Windows.CloseAll();

    internal static KitContext Kit =>
        KitRuntime.Current?.Kit ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");
}
