using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Dialogue;

/// <summary>Sets up the dialogue helpers: load tracking for Lua writes and the conversation events.</summary>
internal static class DialogueModule
{
    public static void Init(ObenMod framework)
    {
        // The game announces a load before it swaps the scene; the framework's own Loading event comes
        // later. Watching the earlier signal keeps writes from slipping in between.
        SaveController.PlayerWillLoadGame += LuaState.LoadAnnounced;
        framework.Track(Disposable.Create(() => SaveController.PlayerWillLoadGame -= LuaState.LoadAnnounced));

        GameEvents.Loaded.Subscribe(framework, LuaState.LoadFinished);
        GameEvents.WorldReady.Subscribe(framework, _ => LuaState.Settled());
        GameEvents.MainMenu.Subscribe(framework, ClearAnnouncedLoad);
        GameEvents.SceneLoaded.Subscribe(framework, _ => LuaState.Settled());

        ConversationHooks.Attach(framework);
    }

    // A load that was announced but never finished (broken save) must not hold writes back for good.
    private static void ClearAnnouncedLoad()
    {
        if (!WorldState.IsLoadingSave)
            LuaState.LoadFinished();
    }
}
