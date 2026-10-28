using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.NewGames;

/// <summary>
/// Sets up session tracking, the new game hook, new game options, difficulty and the tutorial events.
/// The Harmony patches are applied by the loader.
/// </summary>
internal static class NewGamesModule
{
    public static void Init(ObenMod framework)
    {
        Sessions.Init(framework);
        NewGameSave.Init(framework);
        GameDifficulty.Init(framework);
        framework.Track(Disposable.Create(NewGameEvents.Reset));
        framework.Track(Disposable.Create(NewGameOptions.Reset));
    }
}
