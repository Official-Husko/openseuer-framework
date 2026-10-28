using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.NewGames;

/// <summary>Sets up session tracking and the new game hook. The Harmony patch is applied by the loader.</summary>
internal static class NewGamesModule
{
    public static void Init(ObenMod framework)
    {
        Sessions.Init(framework);
        framework.Track(Disposable.Create(NewGameEvents.Reset));
    }
}
