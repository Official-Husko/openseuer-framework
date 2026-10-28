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

    /// <summary>True while any kit window is open.</summary>
    public static bool AnyWindowOpen => KitRuntime.Current != null && KitRuntime.Current.Kit.Windows.Any;

    /// <summary>
    /// Opens a window and fills it with <paramref name="build"/>. Without a <paramref name="footer"/>
    /// it gets a Back button. A <paramref name="height"/> above 0 makes a centred box instead of the
    /// full-height column. Opening a window with an id that's already open brings back the open one.
    /// </summary>
    public static UiWindow Open(ITracker owner, string id, string title, Action<UiBuilder> build,
        Action<UiBuilder> footer = null, float width = DefaultWidth, float height = 0f)
    {
        foreach (var open in Kit.Windows.Open)
        {
            if (open.Id == id)
                return open;
        }
        return new UiWindow(Kit, owner, id, title, build, footer, width, height);
    }

    /// <summary>Closes every kit window.</summary>
    public static void CloseAll() => Kit.Windows.CloseAll();

    private static KitContext Kit =>
        KitRuntime.Current?.Kit ?? throw new InvalidOperationException("ObenUIKit isn't loaded.");
}
