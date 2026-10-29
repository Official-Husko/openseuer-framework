using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Events;

/// <summary>
/// Subscribes to SaveController's static events once and forwards them as <see cref="GameEvents"/>.
/// The subscriptions are removed when the framework unloads, so a hot reload doesn't leave the old
/// copy's handlers on the game's events.
/// </summary>
internal static class SaveHooks
{
    public static void Attach(ObenMod framework)
    {
        LoadAbortWatcher.Create(framework);
        SaveController.PlayerWillChangeLevel += OnLevelChanging;
        SaveController.PlayerWillLoadGame += OnWillLoad;
        SaveController.SavingStarted += OnSaving;
        SaveController.SavingDone += OnSaved;
        SaveController.LoadingStarted += OnLoading;
        SaveController.LoadingDone += OnLoaded;
        framework.Track(Disposable.Create(() =>
        {
            SaveController.PlayerWillChangeLevel -= OnLevelChanging;
            SaveController.PlayerWillLoadGame -= OnWillLoad;
            SaveController.SavingStarted -= OnSaving;
            SaveController.SavingDone -= OnSaved;
            SaveController.LoadingStarted -= OnLoading;
            SaveController.LoadingDone -= OnLoaded;
        }));
    }

    private static void OnLevelChanging()
    {
        // The game invokes this without a try/catch, and a throw would leave the level change blocked.
        try { LoadAbortWatcher.Announced(levelChange: true); }
        catch (System.Exception e) { GameEvents.Log?.Error("Watching the announced level change failed", e); }
        WorldState.IsReady = false;
        GameEvents.LevelChanging.Raise(GameEvents.Log);
    }

    // The game swallows exceptions from this one, but a failure here would still go unnoticed.
    private static void OnWillLoad()
    {
        try { LoadAbortWatcher.Announced(levelChange: false); }
        catch (System.Exception e) { GameEvents.Log?.Error("Watching the announced load failed", e); }
    }

    private static void OnSaving() => GameEvents.Saving.Raise(GameEvents.Log);

    private static void OnSaved() => GameEvents.Saved.Raise(CurrentSaveFolder(), GameEvents.Log);

    private static void OnLoading()
    {
        LoadAbortWatcher.Proceeding();
        WorldState.IsReady = false;
        WorldState.IsLoadingSave = true;
        GameEvents.Loading.Raise(GameEvents.Log);
    }

    private static void OnLoaded()
    {
        WorldState.IsLoadingSave = false;
        GameEvents.Loaded.Raise(GameEvents.Log);
        ReadyWatcher.Arm();
    }

    private static string CurrentSaveFolder()
    {
        try
        {
            return SaveController.instance != null ? SaveController.instance.GetCurrentSavePath() : null;
        }
        catch (System.Exception)
        {
            return SaveController.LastSaveFolderPath;
        }
    }
}
