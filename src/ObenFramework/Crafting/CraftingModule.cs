using ObenFramework.Events;
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Crafting;

/// <summary>
/// Sets up the crafting module: per-scene caches are dropped when a level goes away and on unload. Speeds,
/// quality factors, filters and sources are owner-tracked and leave with their mods. The Harmony patches are
/// applied by the loader like the framework's others.
/// </summary>
internal static class CraftingModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, ForgetScene);
        GameEvents.MainMenu.Subscribe(framework, ForgetScene);
        framework.Track(Disposable.Create(ForgetScene));
    }

    /// <summary>The crafting patches applied; false once a game update broke one and the area was switched off.</summary>
    internal static bool HooksInPlace => PatchAreas.IsAvailable(PatchAreas.Framework, "Crafting");

    private static void ForgetScene()
    {
        CraftingSources.Forget();
    }
}
