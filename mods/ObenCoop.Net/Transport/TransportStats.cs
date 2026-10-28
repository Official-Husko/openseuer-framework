namespace ObenCoop.Net.Transport;

/// <summary>Running traffic counts since the transport started. Transports that can't count return zeros.</summary>
public readonly struct TransportStats
{
    public TransportStats(long bytesSent, long bytesReceived, long packetsSent, long packetsReceived, long packetsLost)
    {
        BytesSent = bytesSent;
        BytesReceived = bytesReceived;
        PacketsSent = packetsSent;
        PacketsReceived = packetsReceived;
        PacketsLost = packetsLost;
    }

    public long BytesSent { get; }
    public long BytesReceived { get; }
    public long PacketsSent { get; }
    public long PacketsReceived { get; }
    public long PacketsLost { get; }
}
