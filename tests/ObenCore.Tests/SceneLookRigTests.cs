using ObenFramework.SceneLook;

namespace ObenCore.Tests;

public class SceneLookRigTests
{
    private static readonly string[] Biped =
    {
        "Bip001 Pelvis", "Bip001 Spine", "Bip001 Spine1", "Bip001 Neck", "Bip001 Head",
        "Bip001 L Thigh", "Bip001 L Calf", "Bip001 L Foot", "Bip001 R Thigh", "Bip001 R Calf", "Bip001 R Foot",
        "Bip001 L Clavicle", "Bip001 L UpperArm", "Bip001 L Forearm", "Bip001 L Hand",
        "Bip001 R Clavicle", "Bip001 R UpperArm", "Bip001 R Forearm", "Bip001 R Hand",
    };

    // A look whose one renderer uses the given bones, each a node of the look's tree.
    private static SceneLookLook Look(params string[] bones)
    {
        var look = new SceneLookLook { Root = 1, Nodes = new List<SceneLookNode>(), Renderers = new List<SceneLookLookRenderer>() };
        look.Nodes.Add(SceneLookMathTests.Node(1, 2, "Root", 0, 1f));
        var renderer = new SceneLookLookRenderer { Name = "Body", Bones = new List<SceneLookBone>() };
        for (int i = 0; i < bones.Length; i++)
        {
            long transform = 100 + i * 2;
            look.Nodes.Add(SceneLookMathTests.Node(transform - 1, transform, bones[i], 2, 1f));
            renderer.Bones.Add(new SceneLookBone { Name = bones[i], Transform = new SceneLookRef(0, transform) });
        }
        look.Renderers.Add(renderer);
        return look;
    }

    [Fact]
    public void Same_names_take_the_name_path()
    {
        var plan = SceneLookRig.Decide(Look(Biped), Biped, "Npc", "humanoid");

        Assert.Equal(SceneLookRigPath.Name, plan.Path);
        Assert.Equal(15, plan.RequiredCovered);
        Assert.Equal(Biped.Length, plan.Bones.Count);
    }

    [Fact]
    public void The_duck_head_and_root_bones_map_by_alias()
    {
        var plan = SceneLookRig.Decide(Look("mr_duck_head", "Bip001_root"), new[] { "Bip001 Head", "Npc" }, "Npc", "none");

        Assert.Equal(SceneLookRigPath.Name, plan.Path);
        Assert.Equal(2, plan.Aliases.Count);
        Assert.Contains(plan.Bones, b => b.LookName == "Bip001_root" && b.TargetName == "Npc");
    }

    [Fact]
    public void Other_names_on_a_humanoid_body_are_retargeted()
    {
        var plan = SceneLookRig.Decide(Look(Biped), new[] { "Hips", "Spine" }, "Player", "humanoid");

        Assert.Equal(SceneLookRigPath.Retarget, plan.Path);
        Assert.Equal(19, plan.Roles.Count);
    }

    [Fact]
    public void Retargeting_needs_every_required_role_and_unique_names()
    {
        var partial = SceneLookRig.Decide(Look("Bip001 Pelvis", "Bip001 Head"), new[] { "Hips" }, "Player", "humanoid");
        Assert.Equal(SceneLookRigPath.Refuse, partial.Path);
        Assert.Contains("2 of the 15", partial.Reason);

        var look = Look(Biped);
        look.Nodes.Add(SceneLookMathTests.Node(900, 901, "Bip001 Head", 2, 1f));
        look.Renderers[0].Bones.Add(new SceneLookBone { Name = "Bip001 Head", Transform = new SceneLookRef(0, 901) });
        var duplicate = SceneLookRig.Decide(look, new[] { "Hips" }, "Player", "humanoid");
        Assert.Equal(SceneLookRigPath.Refuse, duplicate.Path);
        Assert.Contains("duplicate", duplicate.Reason);

        var generic = SceneLookRig.Decide(Look(Biped), new[] { "Hips" }, "Crate", "generic");
        Assert.Equal(SceneLookRigPath.Refuse, generic.Path);
    }

    [Fact]
    public void Clashing_names_outside_the_bones_are_numbered_apart()
    {
        // Two footstep objects of one name hang off the root, beside the skeleton.
        var look = Look(Biped);
        look.Nodes.Add(SceneLookMathTests.Node(900, 901, "FootStepAudio", 2, 1f));
        look.Nodes.Add(SceneLookMathTests.Node(902, 903, "FootStepAudio", 2, 1f));

        var plan = SceneLookRig.Decide(look, new[] { "Hips" }, "Player", "humanoid");
        var chain = SceneLookChain.Bones(look);
        var names = SceneLookChain.Names(look);

        Assert.Equal(SceneLookRigPath.Retarget, plan.Path);
        Assert.Contains(2L, chain);
        Assert.DoesNotContain(901L, chain);
        Assert.Equal("FootStepAudio", names[901]);
        Assert.Equal("FootStepAudio (2)", names[903]);
        Assert.Equal("Bip001 Head", names[108]);
    }

    [Fact]
    public void The_blender_rig_fills_every_required_role()
    {
        string[] blender =
        {
            "Spine", "Spine1", "Spine2", "Neck", "Head", "Hip.L", "Thigh.L", "Shin.L", "Ankle.L", "Foot.L",
            "Hip.R", "Thigh.R", "Shin.R", "Ankle.R", "Foot.R", "Shoulder.L", "Arm.L", "Forearm.L", "Hand.L",
            "Shoulder.R", "Arm.R", "Forearm.R", "Hand.R",
        };

        var plan = SceneLookRig.Decide(Look(blender), Biped, "Npc", "humanoid");

        Assert.Equal(SceneLookRigPath.Retarget, plan.Path);
        Assert.Equal(15, plan.RequiredCovered);
        Assert.Equal("Spine", SceneLookRig.Roles(blender)["Hips"]);
        Assert.Equal("Ankle.L", SceneLookRig.Roles(blender)["LeftFoot"]);
    }

    [Fact]
    public void Skeleton_path_maps_by_name_then_by_role()
    {
        var roles = new Dictionary<string, string> { ["Hips"] = "pelvis", ["Head"] = "head" };
        var plan = SceneLookRig.Skeleton(Look("Bip001 Pelvis", "Bip001 Head", "extra"), new[] { "pelvis", "head", "extra" }, roles);

        Assert.Equal(SceneLookRigPath.Skeleton, plan.Path);
        Assert.Equal(2, plan.RoleMapped);
        Assert.Equal(1, plan.NameMapped);

        var missing = SceneLookRig.Skeleton(Look("Bip001 Pelvis", "tail"), new[] { "pelvis" }, roles);
        Assert.Equal(SceneLookRigPath.Refuse, missing.Path);
        Assert.Contains("tail", missing.Reason);
    }

    [Fact]
    public void Roles_come_from_the_table()
    {
        Assert.Equal("Head", SceneLookRig.RoleOf("mr_duck_head"));
        Assert.Equal("LeftHand", SceneLookRig.RoleOf("Bip001 L Hand"));
        Assert.Null(SceneLookRig.RoleOf("Bip001 Ponytail"));
        var roles = SceneLookRig.Roles(new[] { "Bip001 Pelvis", "Bip001 Head", "mr_duck_head" });
        Assert.Equal("Bip001 Pelvis", roles["Hips"]);
        Assert.Equal("mr_duck_head", roles["Head"]);
        Assert.True(SceneLookRig.RequiredRole("Spine"));
        Assert.False(SceneLookRig.RequiredRole("Neck"));
    }

    [Fact]
    public void Nothing_to_map_onto_is_refused()
    {
        Assert.Equal("no look", SceneLookRig.Decide(null, Biped, "x", "humanoid").Reason);
        Assert.Equal("the target root has no names", SceneLookRig.Decide(Look(Biped), new string[0], "x", "humanoid").Reason);
    }

    [Fact]
    public void Live_plan_keeps_or_drops_a_spawn()
    {
        Assert.Equal(SceneLookLiveAction.Refuse, SceneLookLivePlan.Spawn(false, false));
        Assert.Equal(SceneLookLiveAction.Despawn, SceneLookLivePlan.Spawn(true, false));
        Assert.Equal(SceneLookLiveAction.Keep, SceneLookLivePlan.Spawn(true, true));
    }

    [Fact]
    public void Live_plan_watches_what_still_exists_and_finds_the_one_new_child()
    {
        Assert.Equal(new[] { 0, 2 }, SceneLookLivePlan.Watch(new List<int> { 11, 12, 13 }, new List<int> { 13, 11, 21 }));
        Assert.Empty(SceneLookLivePlan.Watch(new List<int> { 11 }, new List<int>()));
        Assert.Equal(3, SceneLookLivePlan.Holder(new List<int> { 1, 2 }, new List<int> { 1, 2, 3 }));
        Assert.Equal(0, SceneLookLivePlan.Holder(new List<int> { 1, 2 }, new List<int> { 2, 1 }));
        Assert.Equal(0, SceneLookLivePlan.Holder(new List<int> { 1, 2 }, new List<int> { 1, 2, 3, 4 }));
    }
}
