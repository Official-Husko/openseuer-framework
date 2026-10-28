// Shows what happens to an item whose mod is gone. In a game (not the main menu) run
//   oben run scripts/examples/missing-item.cs
// It adds "Demo Crate" in the name of the Script Runner mod (a copy of the Paper Bag) and gives you two.
// Then take the mod away: oben call mods.reload id=ScriptRunner. Save, load that save, and the stack shows
// as "Missing item (ScriptRunner: Demo Crate)" with its id kept (oben call registry.placeholders lists it).
// Run this script again and the stand-ins turn back into Demo Crate on the spot.
using ObenFramework.Items;
using ObenLoader;
using ObenLoader.Mods;
using ScriptRunner;

public static class MissingItemDemo
{
    public static object Run(ScriptScope scope)
    {
        ObenMod owner = ModRegistry.Find("ScriptRunner").Instance;
        int id = ReservedIds.For("ScriptRunner", "demo_crate");
        if (!ItemRegistry.IsModded(id))
        {
            var template = ItemLookup.ById(18560); // Paper Bag
            var crate = ItemRegistry.Derive(template, id, "Demo Crate");
            crate.Description = "Added by scripts/examples/missing-item.cs.";
            ItemRegistry.Register(owner, crate);
        }
        bool given = PlayerItems.Give(id, 2, notify: true);
        return $"Demo Crate is item {id}; given: {given}";
    }
}
