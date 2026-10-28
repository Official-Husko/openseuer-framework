using System;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class CodecTests
{
    [Fact]
    public void EveryTypeRoundTrips()
    {
        var id = Guid.NewGuid();
        var writer = new MessageWriter(16);
        writer.WriteByte(250);
        writer.WriteBool(true);
        writer.WriteUInt16(65000);
        writer.WriteInt32(-123456);
        writer.WriteUInt32(4000000000);
        writer.WriteInt64(long.MinValue + 5);
        writer.WriteSingle(-12.375f);
        writer.WriteGuid(id);
        writer.WriteString("Zoë ✓ 日本");
        writer.WriteString(null);
        writer.WriteBytes(new byte[] { 9, 8, 7 }, 1, 2);

        var reader = new MessageReader().Init(writer.AsSegment());
        Assert.Equal(250, reader.ReadByte());
        Assert.True(reader.ReadBool());
        Assert.Equal(65000, reader.ReadUInt16());
        Assert.Equal(-123456, reader.ReadInt32());
        Assert.Equal(4000000000u, reader.ReadUInt32());
        Assert.Equal(long.MinValue + 5, reader.ReadInt64());
        Assert.Equal(-12.375f, reader.ReadSingle());
        Assert.Equal(id, reader.ReadGuid());
        Assert.Equal("Zoë ✓ 日本", reader.ReadString());
        Assert.Equal("", reader.ReadString());
        var rest = reader.ReadRest();
        Assert.Equal(new byte[] { 8, 7 }, new[] { rest.Array[rest.Offset], rest.Array[rest.Offset + 1] });
        Assert.Equal(0, reader.Remaining);
    }

    [Fact]
    public void ReadingPastTheEndThrowsInsteadOfReturningGarbage()
    {
        var writer = new MessageWriter();
        writer.WriteUInt16(5);
        var reader = new MessageReader().Init(writer.AsSegment());

        Assert.Throws<MessageFormatException>(() => reader.ReadInt32());
        Assert.Equal(5, reader.ReadUInt16());
        Assert.Throws<MessageFormatException>(() => reader.ReadByte());
    }

    [Fact]
    public void StringLengthBeyondTheMessageThrows()
    {
        var writer = new MessageWriter();
        writer.WriteUInt16(200);
        writer.WriteByte(1);
        var reader = new MessageReader().Init(writer.AsSegment());

        Assert.Throws<MessageFormatException>(() => reader.ReadString());
    }

    [Fact]
    public void OversizedStringIsRefusedByTheWriter()
    {
        var writer = new MessageWriter();
        Assert.Throws<ArgumentException>(() => writer.WriteString(new string('x', MessageWriter.MaxStringBytes + 1)));
    }

    [Fact]
    public void WriterGrowsAndKeepsEarlierBytes()
    {
        var writer = new MessageWriter(16);
        for (int i = 0; i < 1000; i++)
            writer.WriteInt32(i);

        var reader = new MessageReader().Init(writer.AsSegment());
        for (int i = 0; i < 1000; i++)
            Assert.Equal(i, reader.ReadInt32());
    }

    [Fact]
    public void SteadyStateRentWriteReadAllocatesNothing()
    {
        var id = Guid.NewGuid();
        Round(id);
        Round(id);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
            Round(id);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void Round(Guid id)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteByte(3);
        writer.WriteInt32(42);
        writer.WriteSingle(1.5f);
        writer.WriteGuid(id);
        writer.WriteString("");
        var reader = MessagePool.RentReader().Init(writer.AsSegment());
        reader.ReadByte();
        reader.ReadInt32();
        reader.ReadSingle();
        reader.ReadGuid();
        reader.ReadString();
        MessagePool.Return(reader);
        MessagePool.Return(writer);
    }

    [Fact]
    public void RouterOneHandlerPerChannelAndDisposeFreesIt()
    {
        var router = new MessageRouter();
        var first = router.Register(20, (p, r) => { });

        Assert.Throws<InvalidOperationException>(() => router.Register(20, (p, r) => { }));
        first.Dispose();
        Assert.False(router.IsRegistered(20));
        router.Register(20, (p, r) => { });
        first.Dispose();
        Assert.True(router.IsRegistered(20));
    }

    [Fact]
    public void ModSetDifferenceSaysWhatDiffers()
    {
        var mine = new ModSet(new[] { new ModEntry("A", "1"), new ModEntry("B", "1") });
        var host = new ModSet(new[] { new ModEntry("A", "2"), new ModEntry("C", "1") });

        string text = mine.DescribeDifference(host);

        Assert.Contains("missing: C", text);
        Assert.Contains("doesn't have: B", text);
        Assert.Contains("A (host 2, you 1)", text);
        Assert.Null(mine.DescribeDifference(new ModSet(new[] { new ModEntry("B", "1"), new ModEntry("A", "1") })));
    }
}
