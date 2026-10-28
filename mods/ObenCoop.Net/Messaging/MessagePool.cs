using System.Collections.Generic;

namespace ObenCoop.Net.Messaging;

/// <summary>
/// Reuses writers and readers so steady-state messaging allocates nothing. Rent, use, return in the
/// same frame. Main thread only (everything the net layer raises is on the main thread).
/// </summary>
public static class MessagePool
{
    // A writer that grew past this (a one-off big message) is dropped instead of kept around.
    private const int MaxPooledCapacity = 64 * 1024;
    private const int MaxPooled = 32;

    private static readonly Stack<MessageWriter> Writers = new Stack<MessageWriter>();
    private static readonly Stack<MessageReader> Readers = new Stack<MessageReader>();

    /// <summary>An empty writer.</summary>
    public static MessageWriter RentWriter()
    {
        if (Writers.Count == 0)
            return new MessageWriter();
        var writer = Writers.Pop();
        writer.Reset();
        return writer;
    }

    public static void Return(MessageWriter writer)
    {
        if (writer != null && writer.Capacity <= MaxPooledCapacity && Writers.Count < MaxPooled)
            Writers.Push(writer);
    }

    public static MessageReader RentReader() => Readers.Count == 0 ? new MessageReader() : Readers.Pop();

    public static void Return(MessageReader reader)
    {
        if (reader == null || Readers.Count >= MaxPooled)
            return;
        reader.Clear();
        Readers.Push(reader);
    }
}
