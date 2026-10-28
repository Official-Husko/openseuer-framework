using System.Collections.Generic;

namespace ObenCoop.Net.Session;

/// <summary>
/// Reusable byte arrays for packets that have to wait a poll or two (see <see cref="InboundQueue"/>).
/// Sizes are rounded up to a power of two so a returned array fits the next packet of that class.
/// Not thread safe; one instance belongs to one session.
/// </summary>
internal sealed class PacketBuffers
{
    private const int MinClassBits = 8;      // 256 bytes
    private const int MaxClassBits = 17;     // 128 KB, above the biggest reliable packet
    private const int MaxPerClass = 16;

    private readonly Stack<byte[]>[] _classes = new Stack<byte[]>[MaxClassBits - MinClassBits + 1];

    public byte[] Rent(int size)
    {
        int index = ClassOf(size);
        if (index < 0)
            return new byte[size];
        var stack = _classes[index];
        return stack != null && stack.Count > 0 ? stack.Pop() : new byte[1 << (index + MinClassBits)];
    }

    public void Return(byte[] buffer)
    {
        if (buffer == null)
            return;
        int index = ClassOf(buffer.Length);
        // Only arrays that are exactly a class size came from Rent.
        if (index < 0 || buffer.Length != 1 << (index + MinClassBits))
            return;
        var stack = _classes[index] ?? (_classes[index] = new Stack<byte[]>());
        if (stack.Count < MaxPerClass)
            stack.Push(buffer);
    }

    private static int ClassOf(int size)
    {
        if (size > 1 << MaxClassBits)
            return -1;
        int bits = MinClassBits;
        while (1 << bits < size)
            bits++;
        return bits - MinClassBits;
    }
}
