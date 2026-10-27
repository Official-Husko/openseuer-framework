using ObenKit.Game;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenKit.Events;

/// <summary>
/// Subscribes to SaveController's static events once and forwards them as <see cref="GameEvents"/>.
/// The subscriptions are removed when the kit unloads, so a hot reload doesn't leave the old
/// copy's handlers on the game's events.
/// </summary>
internal static class SaveHooks
{
    public static void Attach(ObenMod kit)
    {
        SaveController.PlayerWillChangeLevel += OnLevelChanging;
        SaveController.SavingStarted += OnSaving;
        SaveController.SavingDone += OnSaved;
        SaveController.LoadingStarted += OnLoading;
        SaveController.LoadingDone += OnLoaded;
        kit.Track(Disposable.Create(() =>
        {
            SaveController.PlayerWillChangeLevel -= OnLevelChanging;
            SaveController.SavingStarted -= OnSaving;
            SaveController.SavingDone -= OnSaved;
            SaveController.LoadingStarted -= OnLoading;
            SaveController.LoadingDone -= OnLoaded;
        }));
    }

    private static void OnLevelChanging()
    {
        WorldState.IsReady = false;
        GameEvents.LevelChanging.Raise(GameEvents.Log);
    }

    private static void OnSaving() => GameEvents.Saving.Raise(GameEvents.Log);

    private static void OnSaved() => GameEvents.Saved.Raise(CurrentSaveFolder(), GameEvents.Log);

    private static void OnLoading()
    {
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
