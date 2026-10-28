using ObenFramework.Assets;

namespace ObenCore.Tests;

public class MeshPlanTests
{
    private static ObjData Parse(string text, bool flip = false)
    {
        var data = ObjParser.Parse(text, flip, out var error);
        Assert.True(data != null, error);
        return data!;
    }

    // A unit square in the XY plane; its calculated normal points along -Z.
    private const string Square = "v 0 0 0\nv 0 1 0\nv 1 1 0\nv 1 0 0\nf 1 2 3 4\n";

    [Fact]
    public void Scale_and_bounds()
    {
        var arrays = MeshPlan.Build(Parse(Square), new MeshSettings { Scale = 2f });

        Assert.Equal(new[] { 0f, 0f, 0f }, arrays.Min);
        Assert.Equal(new[] { 2f, 2f, 0f }, arrays.Max);
        Assert.Equal(2f, arrays.Positions[7]);
    }

    [Fact]
    public void Center_pivot_moves_the_middle_to_the_origin()
    {
        var arrays = MeshPlan.Build(Parse(Square), new MeshSettings { Pivot = ModelPivot.Center });

        Assert.Equal(-0.5f, arrays.Min[0]);
        Assert.Equal(0.5f, arrays.Max[1]);
    }

    [Fact]
    public void Bottom_center_pivot_puts_the_underside_at_zero()
    {
        var arrays = MeshPlan.Build(Parse("v -1 2 -1\nv 1 4 -1\nv 0 3 1\nf 1 2 3\n"), new MeshSettings { Pivot = ModelPivot.BottomCenter });

        Assert.Equal(0f, arrays.Min[1]);
        Assert.Equal(2f, arrays.Max[1]);
        Assert.Equal(-1f, arrays.Min[0]);
        Assert.Equal(1f, arrays.Max[0]);
    }

    [Fact]
    public void Offset_is_applied_after_the_pivot()
    {
        var arrays = MeshPlan.Build(Parse(Square), new MeshSettings { Pivot = ModelPivot.Center, OffsetY = 3f });

        Assert.Equal(2.5f, arrays.Min[1], 4);
        Assert.Equal(3.5f, arrays.Max[1], 4);
    }

    [Fact]
    public void Rotation_follows_unitys_euler_order()
    {
        // 90 degrees about Y turns +X into -Z; about Z turns +X into +Y; X first then Y is not Y then X.
        var y = MeshPlan.Rotation(0, 90, 0);
        Assert.Equal(-1f, y[6], 4);
        Assert.Equal(0f, y[0], 4);

        var z = MeshPlan.Rotation(0, 0, 90);
        Assert.Equal(1f, z[3], 4);

        // Euler(90, 90, 0) is X applied first, then Y. +Z goes down to -Y and stays there; +X is
        // untouched by X and then turns to -Z. Doing Y first would give different answers.
        var xy = MeshPlan.Rotation(90, 90, 0);
        float[] forward = Apply(xy, 0, 0, 1);
        Assert.Equal(0f, forward[0], 4);
        Assert.Equal(-1f, forward[1], 4);
        Assert.Equal(0f, forward[2], 4);
        float[] right = Apply(xy, 1, 0, 0);
        Assert.Equal(0f, right[0], 4);
        Assert.Equal(0f, right[1], 4);
        Assert.Equal(-1f, right[2], 4);
    }

    [Fact]
    public void Rotation_turns_normals_too()
    {
        var data = Parse("v 0 0 0\nv 1 0 0\nv 0 1 0\nvn 0 0 1\nf 1//1 2//1 3//1\n");
        var arrays = MeshPlan.Build(data, new MeshSettings { EulerY = 90f });

        Assert.Equal(1f, arrays.Normals[0], 4);
        Assert.Equal(0f, arrays.Normals[2], 4);
        // Positions: +X maps to -Z.
        Assert.Equal(-1f, arrays.Positions[5], 4);
    }

    [Fact]
    public void Missing_normals_are_calculated()
    {
        var arrays = MeshPlan.Build(Parse(Square), new MeshSettings());

        Assert.Equal(12, arrays.Normals.Length);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(0f, arrays.Normals[i * 3], 4);
            Assert.Equal(0f, arrays.Normals[i * 3 + 1], 4);
            Assert.Equal(1f, Math.Abs(arrays.Normals[i * 3 + 2]), 4);
        }
    }

    [Fact]
    public void Flipping_x_keeps_normals_facing_the_same_way()
    {
        // Mirroring in X and reversing the winding together keep a face looking the same way.
        var plain = MeshPlan.Build(Parse(Square), new MeshSettings());
        var flipped = MeshPlan.Build(Parse(Square, flip: true), new MeshSettings());

        Assert.Equal(plain.Normals[2], flipped.Normals[2], 4);
        Assert.Equal(-1f, flipped.Positions[3 * 2], 4);
    }

    [Fact]
    public void Degenerate_triangles_get_an_up_normal_instead_of_nan()
    {
        var arrays = MeshPlan.Build(Parse("v 0 0 0\nv 0 0 0\nv 0 0 0\nf 1 2 3\n"), new MeshSettings());

        Assert.Equal(new[] { 0f, 1f, 0f }, arrays.Normals.Take(3));
    }

    [Fact]
    public void Uvs_pass_through_and_indices_are_per_submesh()
    {
        var data = Parse("v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nusemtl a\nf 1/1 2/2 3/3\nusemtl b\nf 3/3 2/2 1/1\n");
        var arrays = MeshPlan.Build(data, new MeshSettings());

        Assert.Equal(6, arrays.Uvs.Length);
        Assert.Equal(2, arrays.Indices.Length);
        Assert.Equal(new[] { 0, 1, 2 }, arrays.Indices[0]);
        Assert.Equal(new[] { 2, 1, 0 }, arrays.Indices[1]);
    }

    private static float[] Apply(float[] m, float x, float y, float z) => new[]
    {
        m[0] * x + m[1] * y + m[2] * z,
        m[3] * x + m[4] * y + m[5] * z,
        m[6] * x + m[7] * y + m[8] * z,
    };
}
