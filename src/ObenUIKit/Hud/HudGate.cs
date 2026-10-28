using ObenFramework.Game;

namespace ObenUIKit;

/// <summary>When the kit's HUD pieces step aside.</summary>
internal static class HudGate
{
    /// <summary>A kit window or the pause menu is open, or a save is loading: nothing of ours shows, and timers wait.</summary>
    public static bool Blocked
    {
        get
        {
            if (UiKit.AnyWindowOpen || WorldState.IsLoadingSave)
                return true;
            var pause = PauseMenu.instance;
            return pause != null && pause.PauseIsActive();
        }
    }

    /// <summary>The game's own HUD is hidden (the hide-HUD key, an inventory or other full menu).</summary>
    public static bool GameUiHidden
    {
        get
        {
            var ui = GameUIController.instance;
            return ui != null && (ui.IsMenuVisible || !ui.GameUIVisible());
        }
    }
}
