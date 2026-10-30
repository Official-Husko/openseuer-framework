using ObenFramework.SceneLook;

namespace ObenCore.Tests;

public class SceneLookMathTests
{
    private const float Close = 1e-4f;

    // 90 degrees about Y: (x, y, z, w) = (0, sin 45, 0, cos 45).
    private static readonly float[] QuarterTurnY = { 0f, 0.70710678f, 0f, 0.70710678f };

    [Fact]
    public void Trs_puts_translation_in_the_last_column()
    {
        var m = SceneLookMatrix.Trs(new[] { 1f, 2f, 3f }, new[] { 0f, 0f, 0f, 1f }, new[] { 2f, 2f, 2f });

        Assert.Equal(new[] { 1f, 2f, 3f }, m.Translation());
        Assert.Equal(2f, m.M00);
        Assert.Equal(2f, SceneLookFrame.Scale(m), 3);
    }

    [Fact]
    public void Rotation_turns_x_into_minus_z()
    {
        var m = SceneLookMatrix.Trs(new[] { 0f, 0f, 0f }, QuarterTurnY, new[] { 1f, 1f, 1f });

        Assert.Equal(0f, m.M00, 4);
        Assert.Equal(-1f, m.M20, 4);
        Assert.Equal(1f, m.M02, 4);
    }

    [Fact]
    public void Inverse_undoes_a_transform()
    {
        var m = SceneLookMatrix.Trs(new[] { 1f, -2f, 0.5f }, QuarterTurnY, new[] { 1.5f, 1.5f, 1.5f });

        Assert.True(m.Multiply(m.Inverse()).MaxDifference(SceneLookMatrix.Identity()) < Close);
        Assert.True(m.Inverse().Multiply(m).MaxDifference(SceneLookMatrix.Identity()) < Close);
    }

    [Fact]
    public void File_matrices_are_read_row_by_row()
    {
        var values = Enumerable.Range(0, 16).Select(i => (float)i).ToArray();
        var m = SceneLookMatrix.FromFile(values);

        Assert.Equal(1f, m.M01);
        Assert.Equal(4f, m.M10);
        Assert.Equal(15f, m.M33);
    }

    [Fact]
    public void Mirrors_have_a_negative_determinant()
    {
        Assert.True(SceneLookMatrix.Trs(new[] { 0f, 0f, 0f }, new[] { 0f, 0f, 0f, 1f }, new[] { -1f, 1f, 1f }).Determinant3() < 0f);
        Assert.Equal(1f, SceneLookMatrix.Identity().Determinant3());
    }

    [Fact]
    public void Rest_poses_join_meshes_through_shared_bones()
    {
        // Bone "a" at x=1 and "b" at x=2 in the first mesh's frame; the second mesh sits 10 units up and shares "b".
        var a = Translate(1, 0, 0);
        var b = Translate(2, 0, 0);
        var c = Translate(3, 0, 0);
        var first = new SceneLookBindMesh<string>();
        first.Add("a", a.Inverse());
        first.Add("b", b.Inverse());
        var lift = Translate(0, 10, 0);
        var second = new SceneLookBindMesh<string>();
        second.Add("b", lift.Inverse().Multiply(b).Inverse());
        second.Add("c", lift.Inverse().Multiply(c).Inverse());
        var lonely = new SceneLookBindMesh<string>();
        lonely.Add("z", SceneLookMatrix.Identity());

        var rest = SceneLookBind.Rest(new[] { first, second, lonely }, out int unresolved);

        Assert.True(rest["a"].MaxDifference(a) < Close);
        Assert.True(rest["c"].MaxDifference(c) < Close);
        Assert.Equal(1, unresolved);
        Assert.False(rest.ContainsKey("z"));
    }

    [Fact]
    public void A_mesh_authored_on_another_axis_is_framed_through_a_shared_bone()
    {
        // The body's meshes stand along Y; the look's mesh was authored standing along Z, and a -90 degree
        // turn about X stands it up. Its frame in the body's rest is that turn, found through the pelvis.
        var pelvis = Translate(0, 1, 0);
        var head = Translate(0, 1.6f, 0);
        var body = new SceneLookBindMesh<string>();
        body.Add("pelvis", pelvis.Inverse());
        body.Add("head", head.Inverse());
        var upright = SceneLookMatrix.Trs(new[] { 0f, 0f, 0f }, new[] { -0.70710678f, 0f, 0f, 0.70710678f }, new[] { 1f, 1f, 1f });
        var look = new SceneLookBindMesh<string>();
        look.Add("unknown", SceneLookMatrix.Identity());
        look.Add("pelvis", pelvis.Inverse().Multiply(upright));
        var rest = SceneLookBind.Rest(new[] { body }, out _);

        var frame = SceneLookBind.Frame(look, rest, out bool known);

        Assert.True(known);
        Assert.True(frame.MaxDifference(upright) < Close);
        // A vertex 1.6 up the look mesh's Z sits on the head bone once skinned with the rebuilt bind pose.
        var vertex = Translate(0, 0, 1.6f);
        var skinned = rest["head"].Multiply(SceneLookBind.BindPose(rest["head"], frame)).Multiply(vertex).Translation();
        Assert.True(Math.Abs(skinned[0]) < Close && Math.Abs(skinned[1] - 1.6f) < Close && Math.Abs(skinned[2]) < Close);
    }

    [Fact]
    public void Bind_pose_is_the_bone_rest_inverse_in_the_renderer_frame()
    {
        var bone = Translate(0, 1, 0);
        var renderer = Translate(0, 0.5f, 0);

        var pose = SceneLookBind.BindPose(bone, renderer);

        Assert.True(bone.Multiply(pose).MaxDifference(renderer) < Close);
        Assert.True(SceneLookBind.Local(renderer, bone).MaxDifference(Translate(0, 0.5f, 0)) < Close);
    }

    [Fact]
    public void Holder_ratio_needs_finite_scales()
    {
        var look = new SceneLookLook { Root = 1, Nodes = new List<SceneLookNode> { Node(1, 2, "Root", 0, 2f) } };

        Assert.True(SceneLookFrame.HolderRatio(look, 1f, out float factor, out _));
        Assert.Equal(2f, factor);
        Assert.False(SceneLookFrame.HolderRatio(look, 0f, out _, out string reason));
        Assert.Contains("target body scale", reason);
        Assert.False(SceneLookFrame.HolderRatio(look, float.NaN, out _, out _));
    }

    [Fact]
    public void Relative_chains_the_parents_up_to_the_root()
    {
        var root = Node(1, 2, "Root", 0, 1f);
        var hips = Node(3, 4, "Hips", 2, 1f, y: 1f);
        var spine = Node(5, 6, "Spine", 4, 1f, y: 0.5f);
        var look = new SceneLookLook { Root = 1, Nodes = new List<SceneLookNode> { root, hips, spine } };
        var byTransform = SceneLookFrame.ByTransform(look);

        Assert.Equal(1.5f, SceneLookFrame.Relative(look, spine, byTransform).M13, 4);
        Assert.Equal(1.5f, SceneLookFrame.RelativeWithoutRoot(look, spine, byTransform).M13, 4);
        Assert.True(SceneLookFrame.RelativeWithoutRoot(look, root, byTransform).MaxDifference(SceneLookMatrix.Identity()) < Close);
    }

    internal static SceneLookNode Node(long go, long transform, string name, long father, float scale, float y = 0f) => new()
    {
        GameObject = go,
        Transform = transform,
        Name = name,
        Father = new SceneLookRef(0, father),
        Children = new List<SceneLookRef>(),
        Position = new[] { 0f, y, 0f },
        Rotation = new[] { 0f, 0f, 0f, 1f },
        Scale = new[] { scale, scale, scale },
    };

    private static SceneLookMatrix Translate(float x, float y, float z) =>
        SceneLookMatrix.Trs(new[] { x, y, z }, new[] { 0f, 0f, 0f, 1f }, new[] { 1f, 1f, 1f });
}
