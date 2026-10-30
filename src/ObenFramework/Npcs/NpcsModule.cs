using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Npcs;

/// <summary>
/// Sets up the NPC module: claims, animations, mod NPCs, routines and mod phone numbers are given back
/// when the framework unloads. The Harmony patches themselves are applied by the loader like the framework's others.
/// </summary>
internal static class NpcsModule
{
    public static void Init(ObenMod framework)
    {
        NpcControl.Init(framework);
        NpcAnimation.Init(framework);
        ModNpcs.Init(framework);
        NpcExtras.Init(framework);
        NpcRoutines.Init(framework);
        PhoneNumbers.Init(framework);
        Pursuit.Init(framework);
        Surveillance.Init(framework);
        framework.Track(Disposable.Create(NpcOrders.Clear));
        framework.Track(Disposable.Create(NpcsLog.Reset));
    }

    /// <summary>The module's one shared per-frame tick. Each part returns at once while it has nothing to do.</summary>
    public static void Update()
    {
        NpcControl.Tick();
        NpcAnimation.Tick();
        ModNpcs.Tick();
        NpcRoutines.Tick();
        NpcOrders.Tick();
    }
}
