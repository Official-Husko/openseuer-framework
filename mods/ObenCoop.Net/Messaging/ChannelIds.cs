namespace ObenCoop.Net.Messaging;

/// <summary>
/// Channel ids of the features that exist. Ids are a byte and are part of the wire format: add new
/// ones at the end, never reuse a number. Session traffic (handshake, roster) doesn't use channels.
/// </summary>
public static class ChannelIds
{
    /// <summary>Plain text chat: a string.</summary>
    public const byte Chat = 1;

    /// <summary>Big transfers (<see cref="Blobs.BlobTransfer"/>). Owned by the session; features register blob handlers, not this channel.</summary>
    public const byte Blob = 2;

    /// <summary>First id free for experiments and tests.</summary>
    public const byte FirstFree = 16;
}
