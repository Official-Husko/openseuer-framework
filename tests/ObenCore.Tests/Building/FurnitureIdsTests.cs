using ObenFramework.Building;

namespace ObenCore.Tests.Building;

public class FurnitureIdsTests
{
    [Fact]
    public void Ids_are_prefixed_mod_and_key()
    {
        Assert.Equal("oben:MyMod/Blue.Chair", FurnitureIds.For("MyMod", "Blue.Chair"));
        Assert.Throws<ArgumentException>(() => FurnitureIds.For("My Mod", "chair"));
        Assert.Throws<ArgumentException>(() => FurnitureIds.For("MyMod", "chair/2"));
        Assert.Throws<ArgumentException>(() => FurnitureIds.For("MyMod", ""));
        Assert.Throws<ArgumentException>(() => FurnitureIds.For("MyMod", new string('k', 65)));
    }

    [Fact]
    public void Ids_are_classified()
    {
        Assert.Equal(FurnitureIdKind.Vanilla, FurnitureIds.Classify("Assets/Content/Prefabs/Building System/Furniture/Chairs/Red Metal Chair.asset"));
        Assert.Equal(FurnitureIdKind.Vanilla, FurnitureIds.Classify(null));
        Assert.Equal(FurnitureIdKind.Furniture, FurnitureIds.Classify("oben:MyMod/chair"));
        Assert.Equal(FurnitureIdKind.Blueprint, FurnitureIds.Classify("oben:MyMod/chair/blueprint"));
        Assert.Equal(FurnitureIdKind.Malformed, FurnitureIds.Classify("oben:MyMod"));
        Assert.Equal(FurnitureIdKind.Malformed, FurnitureIds.Classify("oben:/chair"));
        Assert.Equal(FurnitureIdKind.Malformed, FurnitureIds.Classify("oben:My Mod/chair"));
    }

    [Fact]
    public void Ids_parse_back_and_blueprints_map_to_their_furniture()
    {
        Assert.True(FurnitureIds.TryParse("oben:MyMod/chair", out string mod, out string key));
        Assert.Equal("MyMod", mod);
        Assert.Equal("chair", key);
        Assert.False(FurnitureIds.TryParse("oben:MyMod/chair/blueprint", out _, out _));

        Assert.Equal("oben:MyMod/chair/blueprint", FurnitureIds.BlueprintFor("oben:MyMod/chair"));
        Assert.Null(FurnitureIds.BlueprintFor("oben:MyMod/chair/blueprint"));
        Assert.Equal("oben:MyMod/chair", FurnitureIds.FurnitureOfBlueprint("oben:MyMod/chair/blueprint"));
        Assert.Equal("MyMod", FurnitureIds.ModOf("oben:MyMod/chair/blueprint"));
        Assert.Null(FurnitureIds.ModOf("Red Metal Chair"));
    }

    [Fact]
    public void Blueprint_numbers_are_checked()
    {
        Assert.Null(FurnitureIds.CheckBlueprint(3600f, 1, false, 0, 10));
        Assert.NotNull(FurnitureIds.CheckBlueprint(0f, 1, false, 0, 10));
        Assert.NotNull(FurnitureIds.CheckBlueprint(float.NaN, 1, false, 0, 10));
        Assert.NotNull(FurnitureIds.CheckBlueprint(60f, 0, false, 0, 10));
        Assert.NotNull(FurnitureIds.CheckBlueprint(60f, 1, false, 3, 10));
        Assert.Null(FurnitureIds.CheckBlueprint(60f, 1, true, 3, 10));
        Assert.NotNull(FurnitureIds.CheckBlueprint(60f, 1, true, 11, 10));
    }

    [Fact]
    public void Ingredients_are_checked()
    {
        var items = new Dictionary<int, int> { [35180] = 5 };
        Assert.Null(FurnitureIds.CheckInputs(items, new List<string> { "Red Metal Chair", "oben:MyMod/table" }));
        Assert.NotNull(FurnitureIds.CheckInputs(new Dictionary<int, int> { [1] = 5 }, new List<string>()));
        Assert.NotNull(FurnitureIds.CheckInputs(new Dictionary<int, int> { [35180] = 0 }, new List<string>()));
        Assert.NotNull(FurnitureIds.CheckInputs(items, new List<string> { "oben:MyMod/table", "oben:MyMod/table" }));
        Assert.NotNull(FurnitureIds.CheckInputs(items, new List<string> { "oben:MyMod/table/blueprint" }));
        Assert.NotNull(FurnitureIds.CheckInputs(items, new List<string> { "oben:broken" }));
        Assert.NotNull(FurnitureIds.CheckInputs(null, new List<string>()));
    }

    [Fact]
    public void Pools_and_loot_are_checked()
    {
        Assert.Null(FurnitureIds.CheckPools(new List<string> { "", "Start", "Workshop" }));
        Assert.NotNull(FurnitureIds.CheckPools(new List<string> { "Start", "Start" }));
        Assert.NotNull(FurnitureIds.CheckPools(new List<string> { null }));
        Assert.Null(FurnitureIds.CheckLoot("Trash", 1));
        Assert.NotNull(FurnitureIds.CheckLoot("", 1));
        Assert.NotNull(FurnitureIds.CheckLoot("Trash", 101));
    }

    [Fact]
    public void Weighted_picks_follow_the_weights()
    {
        var weights = new List<int> { 1, 0, 3 };
        Assert.Equal(4, FurnitureIds.TotalWeight(weights));
        Assert.Equal(0, FurnitureIds.PickWeighted(weights, 0));
        Assert.Equal(2, FurnitureIds.PickWeighted(weights, 1));
        Assert.Equal(2, FurnitureIds.PickWeighted(weights, 3));
        Assert.Equal(-1, FurnitureIds.PickWeighted(weights, 4));
        Assert.Equal(-1, FurnitureIds.PickWeighted(weights, -1));
        Assert.Equal(-1, FurnitureIds.PickWeighted(new List<int> { 0, -2 }, 0));

        var counts = new int[3];
        for (int roll = 0; roll < 4; roll++)
            counts[FurnitureIds.PickWeighted(weights, roll)]++;
        Assert.Equal(new[] { 1, 0, 3 }, counts);
    }
}
