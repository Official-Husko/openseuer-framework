using System.Text;
using ObenFramework.SceneLook;

namespace ObenCore.Tests;

public class SceneLookReaderTests
{
    [Fact]
    public void Cursor_reads_values_and_aligned_strings()
    {
        byte[] bytes = new ObjectBytes().I32(-5).Text("abcde").Pptr(2, 77).F32(1.5f).U8(9).ToArray();
        var c = new SceneLookCursor(bytes, 0);

        Assert.Equal(-5, c.I32());
        Assert.Equal("abcde", c.Text());
        Assert.Equal(16, c.Position);
        var reference = c.Pptr();
        Assert.Equal(2, reference.FileId);
        Assert.Equal(77, reference.PathId);
        Assert.Equal(1.5f, c.F32());
        Assert.Equal(9, c.U8());
    }

    [Fact]
    public void Cursor_refuses_reads_past_the_end_and_negative_counts()
    {
        var c = new SceneLookCursor(new byte[] { 0xFF, 0xFF, 0xFF, 0xFF, 1 }, 0);

        Assert.Throws<InvalidDataException>(() => c.Count());
        Assert.Throws<InvalidDataException>(() => c.I64());
        Assert.Throws<InvalidDataException>(() => new SceneLookCursor(Encoding.ASCII.GetBytes("abc"), 0).CString());
        Assert.Equal("ab", new SceneLookCursor(Encoding.ASCII.GetBytes("ab\0c"), 0).CString());
    }

    [Fact]
    public void Parses_game_objects_and_transforms()
    {
        var go = SceneLookReader.ParseGameObject(10, ObjectBytes.GameObject("Head", 11, 12));
        Assert.Equal("Head", go.Name);
        Assert.Equal(new long[] { 11, 12 }, go.Components.Select(r => r.PathId));

        var transform = SceneLookReader.ParseTransform(11, ObjectBytes.Transform(10, 5, 20, 21));
        Assert.Equal(10, transform.GameObject.PathId);
        Assert.Equal(5, transform.Father.PathId);
        Assert.Equal(new[] { 0f, 0f, 0f, 1f }, transform.Rotation);
        Assert.Equal(new[] { 1f, 1f, 1f }, transform.Scale);
        Assert.Equal(2, transform.Children.Count);
    }

    [Fact]
    public void Parses_the_character_asset_header()
    {
        byte[] bytes = new ObjectBytes()
            .Pptr(0, 0).U32(1).Pptr(1, 55)
            .Text("Tatyana").Text("Assets/Content/Characters/Tatyana.asset").I32(49)
            .Text("Tatyana").Text("Gopnikova").Text("Female").I32(61).Text("Neighbour").Text("Retired")
            .Pptr(1, 2).Pptr(1, 3).Pptr(0, 0).ToArray();

        var character = SceneLookReader.ParseCharacter(bytes);

        Assert.Equal(49, character.Id);
        Assert.Equal("Tatyana", character.Name);
        Assert.Equal("Gopnikova", character.LastName);
        Assert.Equal(61, character.Age);
        Assert.Equal("Retired", character.Job);
        Assert.Equal(3, character.Prefab.PathId);
        Assert.StartsWith("Assets/", character.Addressable);
    }

    [Fact]
    public void Parses_the_npc_info_header()
    {
        byte[] bytes = new ObjectBytes()
            .Pptr(0, 8).U8(1).Align().Pptr(1, 900).Text("")
            .Text("guid-1").U8(1).Align().Raw(new byte[32]).Pptr(2, 40).Pptr(0, 41).ToArray();

        var info = SceneLookReader.ParseNpcInfo(5, bytes);

        Assert.Equal(8, info.GameObject.PathId);
        Assert.Equal("guid-1", info.Guid);
        Assert.True(info.Global);
        Assert.Equal(40, info.Character.PathId);
        Assert.Equal(41, info.CharacterModel.PathId);
    }

    [Theory]
    [InlineData("Bip001", "Bip001 Pelvis", "Bip001 Spine", "Head")]
    [InlineData("Blender-style", "spine.001", "hand.L", "Root")]
    [InlineData("other", "Pelvis", "Spine", "Bip001 Head")]
    public void Names_the_rig_by_its_bones(string expected, params string[] names)
    {
        Assert.Equal(expected, SceneLookReader.RigKind(names));
    }

    [Fact]
    public void Finds_length_prefixed_strings_in_bytes()
    {
        byte[] haystack = new ObjectBytes().I32(3).Text("Hips").Text("Spine").ToArray();

        Assert.True(SceneLookReader.Contains(haystack, SceneLookReader.LengthPrefixed("Spine")));
        Assert.False(SceneLookReader.Contains(haystack, SceneLookReader.LengthPrefixed("Head")));
        Assert.Equal("no bones", SceneLookReader.RigKind(new string[0]));
    }

    [Theory]
    [InlineData(4, 4, 4, 1, 64)]
    [InlineData(3, 2, 2, 2, 15)]
    [InlineData(10, 8, 8, 1, 32)]
    [InlineData(12, 8, 8, 2, 80)]
    [InlineData(10, 1, 1, 1, 8)]
    public void Texture_sizes_follow_the_format(int format, int width, int height, int mips, long expected)
    {
        Assert.True(SceneLookTextureReader.IsSupported(format));
        Assert.Equal(expected, SceneLookTextureReader.ExpectedBytes(format, width, height, mips));
    }

    [Fact]
    public void Unsupported_texture_formats_are_named()
    {
        Assert.False(SceneLookTextureReader.IsSupported(25));
        Assert.Equal("BC7", SceneLookTextureReader.FormatLabel(25));
        Assert.Equal("999", SceneLookTextureReader.FormatLabel(999));
        Assert.Equal(-1, SceneLookTextureReader.ExpectedBytes(4, 4, 4, 17));
    }
}
