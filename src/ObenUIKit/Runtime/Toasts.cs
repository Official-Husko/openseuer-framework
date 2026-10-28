using System;
using ObenFramework.Game;

namespace ObenUIKit.Runtime;

/// <summary>
/// Shows a short notice: through the game's own notification panel in a level, and through the
/// kit's small notice at the main menu, where the game has none.
/// </summary>
internal static class Toasts
{
    public static void Show(KitContext kit, string title, string text)
    {
        if (WorldState.InWorld && Notifications.instance != null)
        {
            try
            {
                Notifications.instance.CreateNotification(title, text);
                return;
            }
            catch (Exception e)
            {
                kit.Log.Warn($"The game's notifications weren't usable ({e.Message}), showing the kit's own notice");
            }
        }
        kit.Toasts.Show(kit.Theme, title, text);
    }
}
