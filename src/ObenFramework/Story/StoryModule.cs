using ObenFramework.Dialogue;
using ObenFramework.Events;
using ObenFramework.Quests;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Story;

/// <summary>
/// Sets up the story modules: the deferred queue and ready gate first, then quest authoring (the saved
/// definitions, checkpoints, follow-ups), dialogue authoring with its Lua functions, and the law hooks.
/// </summary>
internal static class StoryModule
{
    public static void Init(ObenMod framework)
    {
        StoryLog.Init(framework.Log);
        StoryPump.Init(framework);
        QuestRegistry.Init(framework);
        QuestCheckpoints.Init(framework);
        QuestFollowUp.Init(framework);
        framework.Track(Disposable.Create(QuestPatches.Reset));

        DialogueLuaBridge.Register(framework);
        // The Dialogue System rebuilds its database list on some loads; put added conversations and links back.
        GameEvents.SceneLoaded.Subscribe(framework, _ => ReapplyDialogue());
        GameEvents.Loaded.Subscribe(framework, ReapplyDialogue);
        GameEvents.WorldReady.Subscribe(framework, _ => ReapplyDialogue());
        framework.Track(Disposable.Create(() =>
        {
            DialogueBranches.Clear();
            DialogueResponses.Clear();
            DialogueAuthoring.Clear();
            LineTexts.Clear();
        }));

        Law.Init(framework);
    }

    private static void ReapplyDialogue()
    {
        DialogueBranches.ApplyAll();
        DialogueResponses.ApplyAll();
    }
}
