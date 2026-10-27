using ObenKit.Game;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine.SceneManagement;

namespace ObenKit.Events;

/// <summary>Turns Unity's scene loads into MainMenu, SceneLoaded and the start of the WorldReady wait.</summary>
internal static class SceneHooks
{
    public static void Attach(ObenMod kit)
    {
        ReadyWatcher.Create(kit);
        SceneManager.sceneLoaded += OnSceneLoaded;
        kit.Track(Disposable.Create(() => SceneManager.sceneLoaded -= OnSceneLoaded));

        // Reloaded mid-game: the world finished loading long ago.
        WorldState.IsReady = WorldState.InWorld && PlayerLocator.instance != null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Additive loads (preloaded neighbours) don't change where the player is.
        if (mode == LoadSceneMode.Additive)
            return;

        WorldState.IsReady = false;
        GameEvents.SceneLoaded.Raise(scene.name, GameEvents.Log);
        if (scene.buildIndex == WorldState.MainMenuScene)
            GameEvents.MainMenu.Raise(GameEvents.Log);
        else
            ReadyWatcher.Arm();
    }
}
