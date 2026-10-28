using ObenFramework.World;

namespace ObenCore.Tests.Transport;

public class GuitarSongTests
{
    [Fact]
    public void Strums_come_after_their_waits()
    {
        var song = new GuitarSong(new[] { 0.5f, 1f, 0f });
        Assert.Equal(0, song.Advance(0.4f));
        Assert.Equal(1, song.Advance(0.1f));
        Assert.Equal(0, song.Advance(0.5f));
        Assert.Equal(2, song.Advance(0.5f));   // the second strum, and the third right after it
        Assert.True(song.Done);
        Assert.Equal(3, song.Played);
        Assert.Equal(0, song.Advance(10f));
    }

    [Fact]
    public void A_long_frame_plays_a_few_strums_at_most()
    {
        var waits = new float[10];
        var song = new GuitarSong(waits);
        Assert.Equal(GuitarSong.MaxStrumsPerFrame, song.Advance(1f));
        Assert.False(song.Done);
        Assert.Equal(GuitarSong.MaxStrumsPerFrame, song.Advance(0f));
        Assert.Equal(2, song.Advance(0f));
        Assert.True(song.Done);
    }

    [Fact]
    public void Bad_songs_are_refused()
    {
        Assert.NotNull(GuitarSong.Check(null));
        Assert.NotNull(GuitarSong.Check(new float[0]));
        Assert.NotNull(GuitarSong.Check(new[] { -1f }));
        Assert.NotNull(GuitarSong.Check(new[] { float.NaN }));
        Assert.NotNull(GuitarSong.Check(new[] { 61f }));
        Assert.Null(GuitarSong.Check(new[] { 0f, 60f }));
        Assert.Throws<ArgumentException>(() => new GuitarSong(new[] { float.PositiveInfinity }));
    }
}
