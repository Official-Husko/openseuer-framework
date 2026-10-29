namespace ObenLoader.Runtime;

/// <summary>
/// Says what the loader is doing while the game window is still black at start: in the window title, and on a
/// small panel over the window (<see cref="LoadingOverlay"/>, which also shows in fullscreen, where there is no
/// title bar). Both go away once the first scene is up.
/// </summary>
internal static class StartupStatus
{
    public static bool PanelEnabled { get; set; } = true;

    public static void Show(string status, int done = 0, int total = 0)
    {
        WindowTitle.Show("ObenseuerExtended: " + status);
        if (PanelEnabled)
            LoadingOverlay.Show(status, done, total);
    }

    public static void Done()
    {
        WindowTitle.Restore();
        LoadingOverlay.Close();
    }
}
