using ObenFramework.Saves;
using ObenLoader;

namespace ObenFramework.NewGames;

/// <summary>
/// The framework's per-save record of how a game was started: the new game option values and the
/// difficulty preset. It lives in the save's mod folder (ObenFramework.newgame.json) and never in the
/// game's own files, so removing a mod or the framework leaves the save loadable as it was.
/// </summary>
internal static class NewGameSave
{
    private static ModSaveData<NewGameOptionsFile> _data;

    /// <summary>The record of the loaded game; empty at the main menu until a new game is started.</summary>
    public static NewGameOptionsFile Data => _data != null ? _data.Value : null;

    public static void Init(ObenMod framework)
    {
        _data = ModSaves.Register<NewGameOptionsFile>(framework, "newgame");
        framework.Track(ObenLoader.Runtime.Disposable.Create(() => _data = null));
    }
}
