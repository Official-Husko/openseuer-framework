using ObenFramework.SceneLook;
using Xunit.Abstractions;

namespace ObenCore.Tests;

/// <summary>
/// Reads the installed game's own scene files, when there is one (OBENSEUER_DIR or the usual Steam
/// folder). These pin the object layouts the reader assumes to the real build; without the game they pass
/// without checking anything.
/// </summary>
public class SceneLookInstalledGameTests
{
    // A few levels with NPCs and posed characters, plus the market (Open Sewer Bazaar).
    private static readonly string[] Levels = { "level1", "level2", "level4", "level6", "level8", "level9", "level112" };

    private readonly ITestOutputHelper _output;

    public SceneLookInstalledGameTests(ITestOutputHelper output)
    {
        _output = output;
    }

    internal static string DataDir()
    {
        var candidates = new List<string>();
        string env = Environment.GetEnvironmentVariable("OBENSEUER_DIR");
        if (!string.IsNullOrEmpty(env))
            candidates.Add(env);
        string home = Environment.GetEnvironmentVariable("HOME") ?? "";
        candidates.Add(Path.Combine(home, ".local/share/Steam/steamapps/common/Obenseuer"));
        candidates.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Obenseuer");
        foreach (string dir in candidates)
        {
            string data = Path.Combine(dir, "Obenseuer_Data");
            if (File.Exists(Path.Combine(data, "level0")))
                return data;
        }
        return null;
    }

    [Fact]
    public void Every_level_header_opens()
    {
        string data = DataDir();
        if (data == null)
            return;
        using var files = new SceneLookFiles(data);
        for (int i = 0; File.Exists(Path.Combine(data, "level" + i)); i++)
        {
            var file = files.Get("level" + i, out string reason);
            Assert.True(file != null, "level" + i + ": " + reason);
            Assert.Equal(SceneLookFile.UnityVersion, file.UnityString);
            files.CloseAllExcept(null);
        }
    }

    [Fact]
    public void Npcs_are_matched_to_bodies_and_characters()
    {
        string data = DataDir();
        if (data == null)
            return;
        using var files = new SceneLookFiles(data);
        int linked = 0;
        foreach (string level in Levels)
        {
            var scan = SceneLookScan.ScanFile(files, level);
            Assert.True(scan.Error == null, level + ": " + scan.Error);
            foreach (var npc in scan.Npcs)
            {
                if (npc.Body == 0 || npc.Character == null || npc.Character.Id <= 0 || string.IsNullOrEmpty(npc.Character.Name))
                    continue;
                linked++;
                _output.WriteLine($"  {level} {npc.Path}: character {npc.Character.Id} {npc.Character.Name}");
            }
            _output.WriteLine($"{level}: {scan.Roots.Count} bodies, {scan.Npcs.Count} npcs, {scan.CleanRenderers}/{scan.Renderers} renderers clean, {scan.Seconds:0.00}s");
        }
        Assert.True(linked > 10, "only " + linked + " NPCs were linked to a body and a character");
    }

    [Fact]
    public void Character_looks_load_with_buildable_meshes_and_blend_shapes()
    {
        string data = DataDir();
        if (data == null)
            return;
        using var files = new SceneLookFiles(data);
        int loaded = 0;
        int shapes = 0;
        foreach (string level in Levels)
        {
            var scan = SceneLookScan.ScanFile(files, level);
            foreach (var root in scan.Roots)
            {
                if (root.Rig != "Bip001" || !root.Clean || loaded >= 12)
                    continue;
                var look = SceneLookLoad.Load(files, level, root.GameObject, out string why);
                Assert.True(look != null, root.Path + ": " + why);
                Assert.Equal(SceneLookRigPath.Name, SceneLookRig.Decide(look, BoneNames(look), root.Name, "humanoid").Path);
                foreach (var renderer in look.Renderers)
                {
                    Assert.True(SceneLookMeshPlan.Plan(renderer.Mesh, out string plan) != null, renderer.Name + ": " + plan);
                    Assert.Equal(renderer.Bones.Count, renderer.Mesh.BindPoses.Length);
                    var frames = SceneLookBlendShapes.Frames(renderer.Mesh, out string shapeProblem);
                    Assert.True(frames != null, renderer.Name + ": " + shapeProblem);
                    shapes += frames.Count;
                    int size = (int)renderer.Mesh.VertexCount * 3;
                    foreach (var frame in frames)
                        Assert.True(SceneLookBlendShapes.Fill(renderer.Mesh, frame, new float[size], new float[size], new float[size]), frame.Name);
                }
                loaded++;
            }
        }
        _output.WriteLine($"{loaded} looks loaded, {shapes} blend shape frames");
        Assert.True(loaded > 0);
    }

    [Fact]
    public void A_kit_character_loads_only_the_clothes_its_scene_shows()
    {
        string data = DataDir();
        if (data == null)
            return;
        using var files = new SceneLookFiles(data);
        var scan = SceneLookScan.ScanFile(files, "level112");
        var topias = scan.Roots.Find(r => r.Path == "Stuff 2/Bazaar NPCs/Topias Iisalmi/Topias Time/Hide/Topias Iisalmi");
        Assert.NotNull(topias);

        var look = SceneLookLoad.Load(files, "level112", topias.GameObject, out string why);

        // The market's Topias carries all 29 meshes of the clothing kit and wears six of them.
        Assert.True(look != null, why);
        var names = look.Renderers.Select(r => r.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "head", "male_arms_wrist", "male_casual_cargo_pants", "male_casual_hoodie_long_sleeves", "male_casual_hoodie_torso_default", "male_casual_shoes_b" }, names);
    }

    // A look's own bone names, as a body with the same skeleton would have them.
    private static List<string> BoneNames(SceneLookLook look)
    {
        var names = new List<string>();
        foreach (var node in look.Nodes)
            names.Add(node.Name);
        return names;
    }
}
