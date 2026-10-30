// Shows what happens to a recipe whose mod is gone. In a game (not the main menu) run
//   oben run scripts/examples/missing-recipe.cs
// It adds the hand-crafting recipe "Demo Sausage" (two Paper Bags into a Sausage) in the name of the Script
// Runner mod and gives it to you. Then take the mod away: oben call mods.reload id=ScriptRunner. Looking the
// recipe up now gives "Missing recipe (ScriptRunner: Demo Sausage)" with its id kept, so blueprints and
// letters that teach it keep working. Save and load that save, and your recipe list shows the stand-in
// (oben call registry.placeholders lists it under recipes). Run this script again and the list shows the
// real recipe on the spot.
using ObenFramework.Crafting;
using ObenFramework.Items;
using ObenLoader;
using ObenLoader.Mods;
using ScriptRunner;

public static class MissingRecipeDemo
{
    public static object Run(ScriptScope scope)
    {
        ObenMod owner = ModRegistry.Find("ScriptRunner").Instance;
        int id = ReservedIds.For("ScriptRunner", "demo_sausage");
        if (!RecipeRegistry.IsModded(id))
        {
            var recipe = RecipeBuilder.Create(id, "Demo Sausage", RecipeType.Basic)
                .Input(ItemLookup.ById(18560), 2) // Paper Bag
                .Output(ItemLookup.ById(14))      // Sausage
                .RequiresLearning()
                .Build();
            RecipeRegistry.Register(owner, recipe);
        }
        bool given = Recipes.Unlock(id);
        return $"Demo Sausage is recipe {id}; given: {given}";
    }
}
