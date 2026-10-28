namespace CheatMenu.Cheats;

/// <summary>The console's <c>noclip</c>: free flight through walls with the movement keys.</summary>
internal static class Noclip
{
    public static bool Available => GameController.instance != null && DeveloperMovement.Instance != null;

    public static bool Active => DeveloperMovement.Instance != null && DeveloperMovement.Instance.shouldHandleMovement;

    public static void Set(bool wanted)
    {
        if (!Available || Active == wanted)
            return;

        // The routine keeps its own on/off flag; going through it keeps that flag and the console in step.
        DeveloperConsoleRoutines.NoClip();

        if (Active == wanted)
            return;
        // The flag had drifted (for example after a level change), so the call went the other way.
        DeveloperConsoleRoutines.NoClip();
    }
}
