namespace ObenCoop.Net.Transport;

/// <summary>Size limits every transport has to honour.</summary>
public static class TransportLimits
{
    /// <summary>Largest unreliable packet, envelope included. Stays under a typical internet MTU.</summary>
    public const int MaxUnreliableBytes = 1200;

    /// <summary>Largest reliable packet, envelope included. Bigger payloads need chunking above the session.</summary>
    public const int MaxReliableBytes = 64 * 1024;
}
