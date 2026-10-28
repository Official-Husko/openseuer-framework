using System;
using System.Collections.Generic;

namespace ObenCoop.Net.Messaging;

/// <summary>
/// Reuses writers and readers so steady-state messaging allocates nothing. Rent, use, return in the
/// same frame. Each thread has its own pool, so rent and return on the same thread (everything the
/// net layer raises is on the thread that calls Poll, which is the main thread in the game).
/// </summary>
public static class MessagePool
{
    // A writer that grew past this (a one-off big message) is dropped instead of kept around.
    private const int MaxPooledCapacity = 64 * 1024;
    private const int MaxPooled = 32;

    [ThreadStatic] private static Stack<MessageWriter> _writers;
    [ThreadStatic] private static Stack<MessageReader> _readers;

    private static Stack<MessageWriter> Writers => _writers ?? (_writers = new Stack<MessageWriter>());
    private static Stack<MessageReader> Readers => _readers ?? (_readers = new Stack<MessageReader>());

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
