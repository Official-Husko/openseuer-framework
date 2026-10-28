using ObenFramework.Quests;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Story;

/// <summary>
/// Sets up the story modules: the deferred queue and ready gate first, then quest authoring (the saved
/// definitions, checkpoints, follow-ups), dialogue authoring and the law hooks that build on it.
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
    }
}
