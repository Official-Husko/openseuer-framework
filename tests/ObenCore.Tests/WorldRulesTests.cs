using ObenFramework.Scenes;
using ObenFramework.World;
using ObenFramework.World.Logic;

namespace ObenCore.Tests;

public class WorldRulesTests
{
    private sealed class Node
    {
        public Node(string name, bool active, params Node[] children)
        {
            Name = name;
            Active = active;
            Children = children.ToList();
        }

        public string Name { get; }
        public bool Active { get; }
        public List<Node> Children { get; }
    }

    private static Node Find(List<Node> roots, string path, bool includeInactive) =>
        ObjectPath.Descend(roots, ObjectPath.Split(path), 0, n => n.Name, n => includeInactive || n.Active, n => n.Children);

    [Fact]
    public void Paths_split_into_names_and_refuse_empty_segments()
    {
        Assert.Equal(new[] { "Bazaar", "Trader", "Muscle Max" }, ObjectPath.Split("Bazaar/Trader/Muscle Max"));
        Assert.Equal(new[] { "Root" }, ObjectPath.Split("Root"));
        Assert.Null(ObjectPath.Split(""));
        Assert.Null(ObjectPath.Split(null));
        Assert.Null(ObjectPath.Split("/Root"));
        Assert.Null(ObjectPath.Split("Root/"));
        Assert.Null(ObjectPath.Split("A//B"));
    }

    [Fact]
    public void Every_sibling_with_the_name_is_tried()
    {
        var inRoot = new Node("Muscle Max", true);
        var roots = new List<Node> { new("Trader", true), new("Trader", true, inRoot) };
        var hidden = new Node("Muscle Max", false);
        var shown = new Node("Muscle Max", true);
        var inactive = new List<Node> { new("Trader", true, hidden), new("Trader", true, shown) };

        Assert.Same(inRoot, Find(roots, "Trader/Muscle Max", true));
        Assert.Same(shown, Find(inactive, "Trader/Muscle Max", false));
        Assert.Same(hidden, Find(inactive, "Trader/Muscle Max", true));
        Assert.Null(Find(roots, "Trader/Missing", true));
    }

    [Fact]
    public void Scene_state_writes_are_checked()
    {
        Assert.True(SceneStateRules.CounterValue(0));
        Assert.False(SceneStateRules.CounterValue(-1));
        Assert.False(SceneStateRules.CounterStep(0));
        Assert.True(SceneStateRules.StateName(new[] { "Closed", "Open" }, "Open"));
        Assert.False(SceneStateRules.StateName(new[] { "Closed", "Open" }, "open"));
        Assert.False(SceneStateRules.StateName(null, "Open"));
        Assert.True(SceneStateRules.Interval(0.5f));
        Assert.False(SceneStateRules.Interval(0f));
        Assert.False(SceneStateRules.Interval(float.PositiveInfinity));
        Assert.False(SceneStateRules.Interval(SceneStateRules.MaxInterval + 1f));
    }

    [Theory]
    [InlineData(true, false, false, true, true)]
    [InlineData(false, false, false, true, false)]
    [InlineData(true, true, true, true, false)]
    [InlineData(true, false, true, true, true)]
    [InlineData(true, false, false, false, false)]
    public void Relays_fire_like_the_game_decides(bool active, bool fireOnceOnly, bool firedOnce, bool requirements, bool fires)
    {
        Assert.Equal(fires, SceneStateRules.RelayCanFire(active, fireOnceOnly, firedOnce, requirements));
    }

    [Fact]
    public void Landmark_entries_are_validated_and_capped()
    {
        var good = new LandmarkEntry { Mod = "MyMod", Id = "camp", Map = "map_tenement_001", Icon = "icons/camp.png" };
        Assert.True(LandmarkStore.Valid(good));
        Assert.False(LandmarkStore.Valid(new LandmarkEntry { Mod = "MyMod", Id = "camp fire", Map = "m" }));
        Assert.False(LandmarkStore.Valid(new LandmarkEntry { Mod = "MyMod", Id = "camp", Map = "" }));
        Assert.False(LandmarkStore.Valid(new LandmarkEntry { Mod = "MyMod", Id = "camp", Map = "m", X = float.NaN }));
        Assert.True(LandmarkStore.IconAllowed(LandmarkStore.DotIcon));
        Assert.False(LandmarkStore.IconAllowed("../outside.png"));
        Assert.False(LandmarkStore.IconAllowed("/etc/x.png"));
        Assert.False(LandmarkStore.IconAllowed("C:/x.png"));
        Assert.Equal("MyMod:camp", LandmarkStore.FullId("MyMod", "camp"));

        var file = new LandmarkFile { Entries = new List<LandmarkEntry> { good, new() { Mod = "MyMod", Id = "camp", Map = "map_tenement_001" }, null, new() { Mod = "MyMod", Id = "x y", Map = "m" } } };
        LandmarkStore.Clean(file);
        Assert.Single(file.Entries);

        var many = new List<LandmarkEntry>();
        for (int i = 0; i < LandmarkStore.MaxPerMod; i++)
            many.Add(new LandmarkEntry { Mod = "MyMod", Id = "l" + i, Map = "m" });
        Assert.False(LandmarkStore.HasRoom(many, "MyMod"));
        Assert.True(LandmarkStore.HasRoom(many, "Other"));
        Assert.True(LandmarkStore.ShouldRestore(good, "map_tenement_001", m => m == "MyMod"));
        Assert.False(LandmarkStore.ShouldRestore(good, "other_map", m => true));
        Assert.False(LandmarkStore.ShouldRestore(good, "map_tenement_001", m => false));
    }

    [Fact]
    public void Object_data_keeps_a_budget_per_mod()
    {
        var store = new ObjectDataStore();
        Assert.True(store.Put("MyMod", "Tenement", "guid-1", "hello"));
        Assert.Equal("hello", store.Get("MyMod", "Tenement", "guid-1"));
        Assert.Null(store.Get("Other", "Tenement", "guid-1"));
        Assert.Equal(ObjectDataStore.Cost("hello"), store.Used("MyMod"));

        Assert.True(store.Put("MyMod", "Tenement", "guid-1", "bye"));
        Assert.Equal(ObjectDataStore.Cost("bye"), store.Used("MyMod"));
        Assert.False(store.Put("MyMod", "Tenement", "guid-2", new string('x', ObjectDataStore.MaxBlobLength + 1)));

        string big = new string('x', ObjectDataStore.MaxBlobLength);
        int fits = 0;
        while (store.Put("MyMod", "Tenement", "g" + fits, big))
            fits++;
        Assert.True(store.Used("MyMod") <= ObjectDataStore.MaxPerMod);
        Assert.True(store.Put("Other", "Tenement", "g0", big));

        Assert.True(store.Remove("MyMod", "Tenement", "guid-1"));
        Assert.False(store.Remove("MyMod", "Tenement", "guid-1"));

        var file = new ObjectDataFile();
        store.SaveTo(file);
        var again = new ObjectDataStore();
        again.Load(file);
        Assert.Equal(store.Count, again.Count);
        Assert.Equal(store.Used("MyMod"), again.Used("MyMod"));
    }

    [Fact]
    public void Visited_scenes_merge_without_repeats_or_menus()
    {
        var merged = VisitedSceneRules.Merge(new[] { "Alpha", "Beta" }, null, new[] { "alpha", "", "Gamma", "Main Menu", "open sewer intro" });

        Assert.Equal(new[] { "Alpha", "Beta", "Gamma" }, merged);
    }

    [Fact]
    public void Level_files_of_a_save_folder_are_its_levels()
    {
        using var folder = new TempFolder();
        foreach (string name in new[] { "Interior Start.tnmt", "Open Sewer.tnmt", "Globals.tnmt", "Info.tnmt", "Globals.dialogue" })
            File.WriteAllText(folder.File(name), "");

        var files = VisitedSceneRules.LevelFiles(folder.Path);

        Assert.Equal(2, files.Count);
        Assert.Contains("interior start", files);
        Assert.Empty(VisitedSceneRules.LevelFiles(folder.Path + "-missing"));
    }
}
