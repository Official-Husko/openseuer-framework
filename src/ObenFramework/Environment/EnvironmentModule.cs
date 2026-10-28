using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Env;

/// <summary>
/// Sets up the environment module: forced flags are put back into a new scene when it is ready, and
/// everything a mod changed is undone when the framework unloads. The Harmony patches are applied by the
/// loader like the framework's others.
/// </summary>
internal static class EnvironmentModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.Loaded.Subscribe(framework, FlagOverrides.ReapplyAll);
        GameEvents.WorldReady.Subscribe(framework, OnWorldReady);
        framework.Track(Disposable.Create(Unload));
    }

    private static void OnWorldReady(string scene)
    {
        FlagOverrides.ReapplyAll();
    }

    private static void Unload()
    {
        FlagOverrides.RestoreAll();
    }
}
