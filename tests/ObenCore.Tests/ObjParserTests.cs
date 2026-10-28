using ObenFramework.Assets;

namespace ObenCore.Tests;

public class ObjParserTests
{
    private const string Cube =
        "v -1 -1 -1\nv 1 -1 -1\nv 1 1 -1\nv -1 1 -1\nv -1 -1 1\nv 1 -1 1\nv 1 1 1\nv -1 1 1\n" +
        "f 1 2 3 4\nf 5 8 7 6\nf 1 5 6 2\nf 2 6 7 3\nf 3 7 8 4\nf 4 8 5 1\n";

    private const string Header = "v 0 0 0\nv 1 0 0\nv 0 1 0\nvt 0 0\nvt 1 0\nvt 0 1\nvn 0 0 1\n";

    private static ObjData Ok(string text, bool flip = false)
    {
        var data = ObjParser.Parse(text, flip, out var error);
        Assert.True(data != null, error);
        return data!;
    }

    private static string Fails(string text)
    {
        Assert.Null(ObjParser.Parse(text, false, out var error));
        Assert.NotNull(error);
        return error!;
    }

    [Fact]
    public void Quads_become_two_triangles_each()
    {
        var cube = Ok(Cube);

        Assert.Equal(8, cube.VertexCount);
        Assert.Single(cube.SubMeshes);
        Assert.Equal(12, cube.TriangleCount);
        Assert.Equal(new[] { 0, 1, 2, 0, 2, 3 }, cube.SubMeshes[0].Triangles.Take(6));
    }

    [Theory]
    [InlineData("f 1 2 3")]
    [InlineData("f 1/1 2/2 3/3")]
    [InlineData("f 1//1 2//1 3//1")]
    [InlineData("f 1/1/1 2/2/1 3/3/1")]
    public void Every_face_corner_format_reads(string face)
    {
        var data = Ok(Header + face + "\n");

        Assert.Equal(new[] { 0, 1, 2 }, data.SubMeshes[0].Triangles);
    }

    [Fact]
    public void Negative_indices_count_from_the_end()
    {
        var data = Ok(Header + "f -3 -2 -1\n");

        Assert.Equal(new[] { 0, 1, 2 }, data.SubMeshes[0].Triangles);
    }

    [Fact]
    public void Corners_with_the_same_triple_share_a_vertex()
    {
        var data = Ok(Header + "f 1/1/1 2/2/1 3/3/1\nf 1/1/1 3/3/1 2/2/1\n");

        Assert.Equal(3, data.VertexCount);
        Assert.Equal(6, data.SubMeshes[0].Triangles.Count);
    }

    [Fact]
    public void Same_position_with_different_uvs_stays_separate()
    {
        var data = Ok(Header + "f 1/1 2/2 3/3\nf 1/3 2/2 3/1\n");

        Assert.Equal(5, data.VertexCount);
    }

    [Fact]
    public void Big_faces_are_fanned_with_a_warning()
    {
        var data = Ok(Header + "f 1 2 3 1 2\nf 1 2 3 1 2\n");

        Assert.Equal(18, data.SubMeshes[0].Triangles.Count);
        Assert.Single(data.Warnings);
    }

    [Fact]
    public void FlipX_negates_x_and_reverses_winding()
    {
        var data = Ok("v 1 0 0\nv 0 1 0\nv 0 0 1\nvn 1 0 0\nf 1//1 2//1 3//1\n", flip: true);

        Assert.Equal(new[] { 0, 2, 1 }, data.SubMeshes[0].Triangles);
        Assert.Equal(-1f, data.Positions[0]);
        Assert.Equal(0f, data.Positions[3]);
        Assert.Equal(-1f, data.Normals[0]);
    }

    [Fact]
    public void Materials_split_into_submeshes_in_first_use_order()
    {
        var data = Ok(Header + "usemtl red\nf 1 2 3\nusemtl blue\nf 1 2 3\nusemtl red\nf 3 2 1\nmtllib stuff.mtl\n");

        Assert.Equal(2, data.SubMeshes.Count);
        Assert.Equal("red", data.SubMeshes[0].Material);
        Assert.Equal("blue", data.SubMeshes[1].Material);
        Assert.Equal(6, data.SubMeshes[0].Triangles.Count);
        Assert.Equal("stuff.mtl", data.MtlLib);
    }

    [Fact]
    public void Faces_before_any_usemtl_have_no_material()
    {
        var data = Ok(Header + "f 1 2 3\n");

        Assert.Null(data.SubMeshes[0].Material);
    }

    [Fact]
    public void Extra_materials_share_the_last_submesh()
    {
        string text = Header;
        for (int i = 0; i < ObjParser.MaxSubMeshes + 2; i++)
            text += "usemtl m" + i + "\nf 1 2 3\n";

        var data = Ok(text);

        Assert.Equal(ObjParser.MaxSubMeshes, data.SubMeshes.Count);
        // m7, m8 and m9 all land in the eighth submesh.
        Assert.Equal(9, data.SubMeshes[ObjParser.MaxSubMeshes - 1].Triangles.Count);
        Assert.Single(data.Warnings);
    }

    [Fact]
    public void Partial_attributes_are_dropped_with_a_warning()
    {
        var partialNormal = Ok(Header + "f 1//1 2//1 3\n");
        Assert.Empty(partialNormal.Normals);
        Assert.Single(partialNormal.Warnings);
        Assert.Equal(9, partialNormal.Positions.Length);

        var partialUv = Ok(Header + "f 1/1 2 3\n");
        Assert.Empty(partialUv.Uvs);
        Assert.Single(partialUv.Warnings);

        var full = Ok(Header + "f 1//1 2//1 3//1\n");
        Assert.Equal(9, full.Normals.Length);
        Assert.Empty(full.Warnings);
    }

    [Fact]
    public void Comments_blank_lines_crlf_and_unknown_statements_are_ignored()
    {
        var data = Ok("# a comment\r\n\r\no thing\r\ng group\r\ns off\r\n" + Header.Replace("\n", "\r\n") + "f 1 2 3\r\n");

        Assert.Equal(1, data.TriangleCount);
    }

    [Fact]
    public void A_leading_byte_order_mark_is_skipped()
    {
        Assert.Equal(8, Ok("\uFEFF" + Cube).VertexCount);
    }

    [Fact]
    public void Numbers_use_the_invariant_culture()
    {
        var old = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("de-DE");
        try
        {
            var data = Ok("v 0.5 0 0\nv 1e-1 0 0\nv 0 2.25 0\nf 1 2 3\n");
            Assert.Equal(0.5f, data.Positions[0]);
            Assert.Equal(0.1f, data.Positions[3]);
            Assert.Equal(2.25f, data.Positions[7]);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = old;
        }
    }

    [Theory]
    [InlineData("f 1 2 9\n", "bad vertex index")]
    [InlineData("f 0 1 2\n", "bad vertex index")]
    [InlineData("f 1 2\n", "at least 3")]
    [InlineData("f 1/9 2 3\n", "bad texture index")]
    [InlineData("f 1//9 2 3\n", "bad normal index")]
    [InlineData("f 1/1/1/1 2 3\n", "bad face corner")]
    [InlineData("f a b c\n", "bad vertex index")]
    public void Bad_faces_name_the_line(string face, string message)
    {
        string error = Fails(Header + face);

        Assert.Contains("line 8", error);
        Assert.Contains(message, error);
    }

    [Theory]
    [InlineData("v NaN 0 0\n")]
    [InlineData("v 1e39 0 0\n")]
    [InlineData("v Infinity 0 0\n")]
    [InlineData("vt 0 -Infinity\n")]
    [InlineData("vn 0 0 NaN\n")]
    [InlineData("v 1 2\n")]
    public void Bad_numbers_are_refused(string line)
    {
        Fails(line + Header + "f 1 2 3\n");
    }

    [Fact]
    public void Null_and_empty_input_are_refused()
    {
        Assert.Null(ObjParser.Parse(null!, false, out var error));
        Assert.NotNull(error);
        Assert.Equal("no faces", Fails("v 0 0 0\n"));
    }

    [Fact]
    public void The_triangle_index_total_is_capped_across_faces()
    {
        string face = "f" + string.Concat(Enumerable.Repeat(" 1", 1000002)) + "\n";

        // Two faces of 1,000,002 corners make exactly 6,000,000 indices: allowed.
        Assert.NotNull(ObjParser.Parse("v 0 0 0\n" + face + face, false, out _));

        // One more triangle than that is not.
        Assert.Contains("triangle indices", Fails("v 0 0 0\n" + face + face + "f 1 1 1\n"));
    }

    [Fact]
    public void A_single_line_with_too_many_values_is_refused()
    {
        string face = "f" + string.Concat(Enumerable.Repeat(" 1", 2000004)) + "\n";

        Assert.Contains("values", Fails("v 0 0 0\n" + face));
    }

    [Fact]
    public void ParseFile_reports_missing_and_reads_real_files()
    {
        Assert.Null(ObjParser.ParseFile("/definitely/not/here.obj", false, out var error));
        Assert.Contains("not found", error);

        string path = Path.Combine(Path.GetTempPath(), "oben-test-" + Guid.NewGuid().ToString("N") + ".obj");
        File.WriteAllText(path, Cube);
        try
        {
            var data = ObjParser.ParseFile(path, false, out error);
            Assert.NotNull(data);
            Assert.Equal(12, data!.TriangleCount);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
