using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Env;

/// <summary>
/// Sets up the environment module: forced flags and option overrides are put back into a new scene when
/// it is ready, and everything a mod changed is undone when the framework unloads. The Harmony patches are
/// applied by the loader like the framework's others.
/// </summary>
internal static class EnvironmentModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.Loaded.Subscribe(framework, FlagOverrides.ReapplyAll);
        GameEvents.WorldReady.Subscribe(framework, OnWorldReady);
        GameEvents.LevelChanging.Subscribe(framework, ForgetScene);
        GameEvents.MainMenu.Subscribe(framework, OnMainMenu);
        framework.Track(Disposable.Create(Unload));

        // Loaded into a running world (a hot reload): nothing is forced yet, only the caches need a start.
        ForgetScene();
    }

    private static void OnWorldReady(string scene)
    {
        FlagOverrides.ReapplyAll();
        SettingOverrides.Reapply();
    }

    private static void OnMainMenu()
    {
        ForgetScene();
        SettingOverrides.Reapply();
    }

    private static void ForgetScene()
    {
        FovPatches.Reset();
        SettingsChangedPatches.Reset();
    }

    private static void Unload()
    {
        FlagOverrides.RestoreAll();
        SettingOverrides.ReleaseAll();
        ForgetScene();
    }
}
