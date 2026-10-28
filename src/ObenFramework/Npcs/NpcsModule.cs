using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Npcs;

/// <summary>
/// Sets up the NPC module: claims and mod phone numbers are given back when the framework unloads. The
/// Harmony patches themselves are applied by the loader like the framework's others.
/// </summary>
internal static class NpcsModule
{
    public static void Init(ObenMod framework)
    {
        NpcControl.Init(framework);
        PhoneNumbers.Init(framework);
        framework.Track(Disposable.Create(NpcsLog.Reset));
    }

    /// <summary>Per-frame work for claimed NPCs; nothing happens while there are none.</summary>
    public static void Update() => NpcControl.Tick();
}
