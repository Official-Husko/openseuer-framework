using ObenLoader;

namespace ObenFramework.Story;

/// <summary>Sets up the story modules: the deferred queue and ready gate first, the rest build on it.</summary>
internal static class StoryModule
{
    public static void Init(ObenMod framework)
    {
        StoryLog.Init(framework.Log);
        StoryPump.Init(framework);
    }
}
