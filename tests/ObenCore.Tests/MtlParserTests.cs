using ObenFramework.Assets;

namespace ObenCore.Tests;

public class MtlParserTests
{
    private static readonly string Folder = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "oben-model"));

    private static MtlData Parse(string text) => MtlParser.ParseText(text, Folder, Folder);

    [Fact]
    public void Reads_colours_and_names()
    {
        var mtl = Parse("# exported\nnewmtl Red\nKd 0.8 0.1 0.05\nKe 0 0.5 0\nnewmtl Plain thing\nKd 1 1 1\n");

        Assert.Equal(2, mtl.Materials.Count);
        Assert.Equal(new[] { 0.8f, 0.1f, 0.05f }, mtl.Find("Red")!.Kd);
        Assert.Equal(new[] { 0f, 0.5f, 0f }, mtl.Find("Red")!.Ke);
        Assert.NotNull(mtl.Find("Plain thing"));
        Assert.Null(mtl.Find("Nope"));
        Assert.Null(mtl.Find(null!));
    }

    [Fact]
    public void Defaults_are_white_solid_and_not_emissive()
    {
        var m = Parse("newmtl A\n").Materials[0];

        Assert.Equal(new[] { 1f, 1f, 1f }, m.Kd);
        Assert.Equal(new[] { 0f, 0f, 0f }, m.Ke);
        Assert.Equal(1f, m.Alpha);
        Assert.Null(m.MapKd);
    }

    [Fact]
    public void Statements_before_the_first_material_are_ignored()
    {
        var mtl = Parse("Kd 0 0 0\nnewmtl A\nKd 0.5 0.5 0.5\n");

        Assert.Single(mtl.Materials);
        Assert.Equal(0.5f, mtl.Materials[0].Kd[0]);
    }

    [Fact]
    public void Bad_colours_warn_and_keep_the_default()
    {
        var mtl = Parse("newmtl A\nKd 1 x 0\nKe 1 2\n");

        Assert.Equal(1f, mtl.Materials[0].Kd[1]);
        Assert.Equal(2, mtl.Warnings.Count);
    }

    [Fact]
    public void Dissolve_and_transparency_set_alpha()
    {
        var mtl = Parse("newmtl A\nd 0.25\nnewmtl B\nTr 0.4\nnewmtl C\nd 7\n");

        Assert.Equal(0.25f, mtl.Find("A")!.Alpha);
        Assert.Equal(0.6f, mtl.Find("B")!.Alpha, 4);
        Assert.Equal(1f, mtl.Find("C")!.Alpha);
    }

    [Fact]
    public void Diffuse_map_resolves_inside_the_folder()
    {
        var m = Parse("newmtl A\nmap_Kd tex\\wood.png\n").Materials[0];

        Assert.Equal(Path.Combine(Folder, "tex", "wood.png"), m.MapKd);
    }

    [Fact]
    public void Map_options_are_skipped()
    {
        var m = Parse("newmtl A\nmap_Kd -s 1 1 1 -o 0 0 0 -clamp on -bm 0.5 my texture.jpg\n").Materials[0];

        Assert.Equal(Path.Combine(Folder, "my texture.jpg"), m.MapKd);
    }

    [Theory]
    [InlineData("../outside.png")]
    [InlineData("a/../../outside.png")]
    [InlineData("/etc/passwd.png")]
    [InlineData("C:/windows/x.png")]
    [InlineData("notimage.exe")]
    [InlineData("noextension")]
    [InlineData("CON.png")]
    [InlineData("sub/nul.png")]
    public void Unsafe_or_unsupported_maps_are_dropped_with_a_warning(string map)
    {
        var mtl = Parse("newmtl A\nmap_Kd " + map + "\n");

        Assert.Null(mtl.Materials[0].MapKd);
        Assert.Single(mtl.Warnings);
    }

    [Fact]
    public void A_map_in_the_mtl_subfolder_is_fine_while_it_stays_in_the_model_folder()
    {
        var mtl = MtlParser.ParseText("newmtl A\nmap_Kd ../up.png\n", Path.Combine(Folder, "materials"), Folder);

        Assert.Equal(Path.Combine(Folder, "up.png"), mtl.Materials[0].MapKd);
    }

    [Fact]
    public void Parse_reports_a_missing_file_as_a_warning()
    {
        var mtl = MtlParser.Parse("/definitely/not/here.mtl", Folder);

        Assert.Empty(mtl.Materials);
        Assert.Single(mtl.Warnings);
    }

    [Fact]
    public void Parse_reads_a_file_from_disk()
    {
        string dir = Path.Combine(Path.GetTempPath(), "oben-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            File.WriteAllText(Path.Combine(dir, "a.mtl"), "newmtl A\nKd 0 1 0\nmap_Kd a.png\n");

            var mtl = MtlParser.Parse(Path.Combine(dir, "a.mtl"), dir);

            Assert.Equal(Path.Combine(Path.GetFullPath(dir), "a.png"), mtl.Materials[0].MapKd);
            Assert.Empty(mtl.Warnings);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
