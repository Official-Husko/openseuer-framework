// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenUIKit;

/// <summary>The game's tooltip box, the one the inventory shows item details in.</summary>
public static partial class Hud
{
    /// <summary>
    /// Shows the game's tooltip with <paramref name="title"/> and <paramref name="details"/> (rich text, as
    /// the game writes item details). It stays until <see cref="HideTooltip"/>, or until the game puts its
    /// own tooltip up or takes it down (hovering an item does). False where the game has no tooltip (the main menu).
    /// </summary>
    public static bool ShowTooltip(string title, string details)
    {
        if (string.IsNullOrEmpty(title))
            throw new ArgumentException("A tooltip needs a title.", nameof(title));
        var tooltip = ToolTip.instance;
        if (tooltip == null)
            return false;
        tooltip.Activate(title, details ?? "");
        return true;
    }

    /// <summary>Takes the game's tooltip down, whoever put it up.</summary>
    public static void HideTooltip()
    {
        var tooltip = ToolTip.instance;
        if (tooltip != null)
            tooltip.Deactivate();
    }
}
