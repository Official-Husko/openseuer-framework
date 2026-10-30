using ObenFramework.Events;
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Items;

/// <summary>
/// Sets up the item module: the inventory recount starts over with each scene, per-save item state is
/// registered, and everything is undone on unload. The Harmony patches are applied by the loader like the
/// framework's others.
/// </summary>
internal static class ItemsModule
{
    public static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, ForgetScene);
        GameEvents.MainMenu.Subscribe(framework, ForgetScene);
        GameEvents.WorldReady.Subscribe(framework, _ => InventoryWatch.Reset());
        // Loaded into a world that is already running (hot reload): take the starting count on the next
        // frame, or the first change would become the starting point instead of an event.
        InventoryWatch.Reset();
        UnloadedStorage.Init(framework);
        ItemConsumables.Init(framework);
        framework.Track(Disposable.Create(() =>
        {
            ForgetScene();
            ItemsLog.Reset();
        }));
    }

    /// <summary>The item patches (one patch area for all of them) are on.</summary>
    internal static bool HooksInPlace => PatchAreas.IsAvailable(PatchAreas.Framework, "Items");

    /// <summary>Per-frame work: one flag check unless a stack changed.</summary>
    public static void Update() => InventoryWatch.Update();

    // Everything cached per scene refers to objects that are about to be destroyed.
    private static void ForgetScene()
    {
        InventoryWatch.Reset();
    }
}
