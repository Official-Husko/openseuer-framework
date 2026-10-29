using ObenCoop.Net.Messaging;
using ObenCoop.Net.Presence;
using Xunit;

namespace ObenCoop.Net.Tests;

public class PresenceTrackTests
{
    private static PresenceSample At(ushort seq, uint time, float x, string area = "Tenement", PresenceFlags flags = PresenceFlags.Moving, float vx = 0f, float yaw = 0f) =>
        new PresenceSample
        {
            Sequence = seq,
            Time = time,
            Area = area,
            Position = new PresenceVector(x, 0f, 0f),
            Velocity = new PresenceVector(vx, 0f, 0f),
            Yaw = yaw,
            Flags = flags,
        };

    [Fact]
    public void ShowsThePlayerBetweenTwoSamplesBehindTheNewest()
    {
        var track = new PresenceTrack();
        // The sender's clock is 1000 ms behind ours and every packet takes 50 ms.
        track.Add(At(1, 0, 0f), 1050);
        track.Add(At(2, 100, 1f), 1150);

        // At local 1250 with a 150 ms delay the view is at sender time 1250 - 1050 - 150 = 50, halfway.
        Assert.True(track.Sample(1250, 150, 250, out var pose));
        Assert.Equal(0.5f, pose.Position.X, 3);
        Assert.False(pose.Extrapolated);
    }

    [Fact]
    public void LateAndRepeatedSamplesAreDropped()
    {
        var track = new PresenceTrack();
        Assert.True(track.Add(At(5, 500, 5f), 0));
        Assert.False(track.Add(At(4, 400, 4f), 10));
        Assert.False(track.Add(At(5, 500, 5f), 20));
        Assert.True(track.Add(At(6, 600, 6f), 30));

        Assert.Equal(2, track.Count);
        Assert.Equal(2, track.Late);
    }

    [Fact]
    public void RunsOnAlongTheVelocityForAWhileWhenSamplesStop()
    {
        var track = new PresenceTrack();
        track.Add(At(1, 0, 0f, vx: 2f), 0);
        track.Add(At(2, 100, 0.2f, vx: 2f), 100);

        // Far past the newest sample: carried forward at most 250 ms, 2 m/s, so 0.5 m beyond it.
        Assert.True(track.Sample(5000, 100, 250, out var pose));
        Assert.True(pose.Extrapolated);
        Assert.Equal(0.7f, pose.Position.X, 3);
    }

    [Fact]
    public void StandingPlayersAreNotCarriedForward()
    {
        var track = new PresenceTrack();
        track.Add(At(1, 0, 3f, flags: PresenceFlags.None, vx: 2f), 0);

        Assert.True(track.Sample(5000, 100, 250, out var pose));
        Assert.Equal(3f, pose.Position.X);
        Assert.False(pose.Extrapolated);
    }

    [Fact]
    public void JumpsAndLevelChangesSnapInsteadOfSliding()
    {
        var track = new PresenceTrack();
        track.Add(At(1, 0, 0f), 0);
        track.Add(At(2, 100, 50f, flags: PresenceFlags.Moving | PresenceFlags.Teleported), 100);
        track.Add(At(3, 200, 60f, area: "Street"), 200);

        Assert.True(track.Sample(50, 0, 250, out var beforeJump));
        Assert.Equal(0f, beforeJump.Position.X);
        Assert.True(track.Sample(150, 0, 250, out var beforeLevel));
        Assert.Equal(50f, beforeLevel.Position.X);
        Assert.Equal("Tenement", beforeLevel.Area);
        Assert.True(track.Sample(200, 0, 250, out var after));
        Assert.Equal("Street", after.Area);
    }

    [Fact]
    public void ASenderThatStartsOverIsFollowed()
    {
        var track = new PresenceTrack();
        track.Add(At(900, 3600000, 1f), 0);
        // A reload of the other game's mod: its counter and clock begin again.
        Assert.True(track.Add(At(1, 5, 2f), 100));

        Assert.Equal(1, track.Restarts);
        Assert.Equal(1, track.Count);
        Assert.Equal(2f, track.Latest.Position.X);
    }

    [Fact]
    public void ASenderWhoseCounterStartsOverOnTheSameClockIsFollowed()
    {
        var track = new PresenceTrack();
        track.Add(At(500, 10000, 1f), 0);
        track.Add(At(501, 10100, 1.1f), 100);
        // Only the counter starts again (the clock carried over the reload).
        Assert.True(track.Add(At(1, 10300, 2f), 300));
        Assert.True(track.Add(At(2, 10400, 2.1f), 400));

        Assert.Equal(1, track.Restarts);
        Assert.Equal(0, track.Late);
        Assert.Equal(2.1f, track.Latest.Position.X);
    }

    [Fact]
    public void KeepsOnlyTheNewestSamples()
    {
        var track = new PresenceTrack();
        for (int i = 1; i <= 40; i++)
            track.Add(At((ushort)i, (uint)(i * 100), i), i * 100);

        Assert.Equal(PresenceTrack.Capacity, track.Count);
        Assert.Equal(40f, track.Latest.Position.X);
    }

    [Fact]
    public void SequenceAndClockWrapAround()
    {
        var track = new PresenceTrack();
        Assert.True(track.Add(At(ushort.MaxValue, uint.MaxValue - 50, 1f), 0));
        Assert.True(track.Add(At(0, 49, 2f), 100));
        Assert.Equal(2, track.Count);
        Assert.Equal(0, track.Restarts);
    }

    [Theory]
    [InlineData(350f, 10f, 0.5f, 0f)]
    [InlineData(10f, 350f, 0.5f, 0f)]
    [InlineData(90f, 180f, 0.5f, 135f)]
    [InlineData(0f, 180f, 1f, 180f)]
    public void TurnsTheShortWayRound(float from, float to, float t, float expected)
    {
        float angle = PresenceTrack.LerpAngle(from, to, t);
        Assert.True(System.Math.Abs(PresenceTrack.DeltaAngle(expected, angle)) < 0.001f, $"got {angle}");
    }

    [Fact]
    public void CodecRoundTripsASample()
    {
        var writer = new MessageWriter();
        var sent = new PresenceSample
        {
            Sequence = 42,
            Time = 123456,
            Area = "Interior Player Tenement",
            Position = new PresenceVector(12.5f, -3.25f, 800.125f),
            Yaw = 271.3f,
            Velocity = new PresenceVector(1.234f, 0f, -4.5f),
            Flags = PresenceFlags.Moving | PresenceFlags.Crouching,
        };
        PresenceCodec.Write(writer, sent);

        var reader = new MessageReader().Init(writer.AsSegment());
        Assert.Equal(1, reader.ReadByte());
        var names = new AreaNames();
        Assert.True(PresenceCodec.TryReadSample(reader, names, out var got));

        Assert.Equal(sent.Sequence, got.Sequence);
        Assert.Equal(sent.Time, got.Time);
        Assert.Equal(sent.Area, got.Area);
        Assert.Equal(sent.Position, got.Position);
        Assert.True(System.Math.Abs(PresenceTrack.DeltaAngle(sent.Yaw, got.Yaw)) < 0.01f);
        Assert.Equal(1.23f, got.Velocity.X, 2);
        Assert.Equal(-4.5f, got.Velocity.Z, 2);
        Assert.Equal(sent.Flags, got.Flags);
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void TheSameAreaNameIsNotMadeAgain()
    {
        var names = new AreaNames();
        string first = Read(names, "Street");
        string second = Read(names, "Street");
        string third = Read(names, "Bar");

        Assert.Same(first, second);
        Assert.Equal("Bar", third);
    }

    [Fact]
    public void OutOfRangePositionsAreRejected()
    {
        var writer = new MessageWriter();
        PresenceCodec.Write(writer, At(1, 1, float.NaN));
        var reader = new MessageReader().Init(writer.AsSegment());
        reader.ReadByte();
        Assert.False(PresenceCodec.TryReadSample(reader, new AreaNames(), out _));

        Assert.False(PresenceCodec.IsValid("Street", new PresenceVector(0f, 2e6f, 0f), 0f, PresenceVector.Zero, out _));
        Assert.False(PresenceCodec.IsValid("", PresenceVector.Zero, 0f, PresenceVector.Zero, out _));
        Assert.False(PresenceCodec.IsValid(new string('x', 65), PresenceVector.Zero, 0f, PresenceVector.Zero, out _));
        Assert.True(PresenceCodec.IsValid("Street", PresenceVector.Zero, 359f, PresenceVector.Zero, out _));
    }

    private static string Read(AreaNames names, string area)
    {
        var writer = new MessageWriter();
        writer.WriteString(area);
        return names.Resolve(new MessageReader().Init(writer.AsSegment()).ReadStringBytes());
    }
}
