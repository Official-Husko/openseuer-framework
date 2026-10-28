namespace ObenCoop.Net.Transport;

/// <summary>How a message travels. Pick per message, not per channel.</summary>
public enum DeliveryMode : byte
{
    /// <summary>Arrives exactly once and in the order it was sent. For anything that changes state.</summary>
    ReliableOrdered = 0,

    /// <summary>
    /// May be lost or arrive late. For streams where the next sample replaces the last (positions).
    /// Must fit in <see cref="TransportLimits.MaxUnreliableBytes"/>.
    /// </summary>
    Unreliable = 1,
}
