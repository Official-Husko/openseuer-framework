using UnityEngine.SceneManagement;

namespace ObenFramework.Game;

/// <summary>Where the game is right now: main menu, loading, or in a playable world.</summary>
public static class WorldState
{
    /// <summary>Build index of the main menu scene ("Main Menu").</summary>
    public const int MainMenuScene = 0;

    /// <summary>A gameplay scene is loaded (every one has a GameController; the menu and intro don't).</summary>
    public static bool InWorld => GameController.instance != null;

    public static bool InMainMenu => SceneManager.GetActiveScene().buildIndex == MainMenuScene;

    /// <summary>
    /// The world is loaded, the save (if any) has been applied and the player exists. Set just before
    /// <see cref="Events.GameEvents.WorldReady"/> fires, cleared when the scene changes.
    /// </summary>
    public static bool IsReady { get; internal set; }

    /// <summary>Between SaveController's LoadingStarted and LoadingDone (SaveController.Loading isn't reliable).</summary>
    public static bool IsLoadingSave { get; internal set; }

    /// <summary>The game's pause menu or anything else that stopped time.</summary>
    public static bool IsPaused => UnityEngine.Time.timeScale == 0f;

    public static string SceneName => SceneManager.GetActiveScene().name;
}
