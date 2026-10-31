using System.Collections.Generic;
using ObenUIKit.Controls;

namespace ObenUIKit.Menu;

/// <summary>
/// The "More…" button the main menu gets when there are more mod buttons than rows fit under its grid. It
/// takes the last place on the menu and opens a window listing the buttons that didn't fit, each doing what
/// its menu button would. The window follows the list while it is open and closes once nothing is left in it.
/// </summary>
internal static class MainMenuMore
{
    public const string WindowId = "kit.mainmenu.more";

    private const float Width = 620f;
    private const float Chrome = 260f;
    private const float PerButton = UiBuilder.RowHeight + 8f;
    private const float MaxHeight = 900f;

    private static readonly List<MainMenuButton> Rest = new List<MainMenuButton>();

    /// <summary>The buttons in the window, in menu order.</summary>
    public static IReadOnlyList<MainMenuButton> Buttons => Rest;

    public static bool Contains(MainMenuButton button) => Rest.Contains(button);

    public static string Label => "More" + UiKit.Theme.Glyph('…', "...");

    /// <summary>Takes the buttons that didn't fit on the menu (none when all did).</summary>
    public static void Set(IEnumerable<MainMenuButton> rest)
    {
        Rest.Clear();
        if (rest != null)
            Rest.AddRange(rest);
        var open = UiKit.Find(WindowId);
        if (open == null)
            return;
        if (Rest.Count == 0)
            open.Close();
        else
            open.Rebuild();
    }

    /// <summary>One of the buttons changed its label or state.</summary>
    public static void Changed(MainMenuButton button)
    {
        if (Contains(button))
            UiKit.Find(WindowId)?.Rebuild();
    }

    public static void Open()
    {
        if (Rest.Count == 0)
            return;
        // A box the size of the list, like a dialog; a long list gets the full-height column and scrolls.
        float height = Chrome + Rest.Count * PerButton;
        UiKit.Open(UIKitMod.Instance, WindowId, "More", Build, width: Width, height: height > MaxHeight ? 0f : height);
    }

    private static void Build(UiBuilder ui)
    {
        foreach (var button in Rest)
        {
            var entry = button;
            ui.Button(entry.Label, () => MainMenuRow.Press(entry)).Enabled = entry.Interactable;
        }
    }
}
