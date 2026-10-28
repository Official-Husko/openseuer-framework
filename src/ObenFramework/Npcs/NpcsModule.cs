using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Npcs;

/// <summary>
/// Sets up the NPC module: claims, animations and mod phone numbers are given back when the framework
/// unloads. The Harmony patches themselves are applied by the loader like the framework's others.
/// </summary>
internal static class NpcsModule
{
    public static void Init(ObenMod framework)
    {
        NpcControl.Init(framework);
        NpcAnimation.Init(framework);
        PhoneNumbers.Init(framework);
        framework.Track(Disposable.Create(NpcsLog.Reset));
    }

    /// <summary>The module's one shared per-frame tick. Each part returns at once while it has nothing to do.</summary>
    public static void Update()
    {
        NpcControl.Tick();
        NpcAnimation.Tick();
    }
}
