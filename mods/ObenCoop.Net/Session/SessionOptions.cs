using System;
using System.Diagnostics;
using ObenCoop.Net.Blobs;

namespace ObenCoop.Net.Session;

/// <summary>What a session needs to know about this game and player.</summary>
public sealed class SessionOptions
{
    /// <summary>The persistent id of the local player (from the identity file).</summary>
    public Guid PlayerId { get; set; } = Guid.NewGuid();

    public string PlayerName { get; set; } = "Player";

    /// <summary>The game's own version. Host and clients must match.</summary>
    public string GameVersion { get; set; } = "";

    /// <summary>
    /// What tells builds of one game version apart (the game code's hash). Host and clients must match
    /// when both know it; "" when unknown.
    /// </summary>
    public string GameBuild { get; set; } = "";

    /// <summary>The mods everyone must share. Null means none.</summary>
    public ModSet Mods { get; set; } = ModSet.Empty;

    /// <summary>Players in a hosted session, host included (2 to <see cref="Protocol.MaxPlayers"/>).</summary>
    public int MaxPlayers { get; set; } = 4;

    /// <summary>A connection that sends no valid Hello within this time is dropped (host side).</summary>
    public int HandshakeTimeoutMs { get; set; } = 5000;

    /// <summary>A client gives up when the host does not answer its Hello within this time.</summary>
    public int WelcomeTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Most packets handled per peer in one <see cref="CoopSession.Poll"/>. Packets over the budget
    /// wait, in order, for the next poll, so one chatty peer can't hold up the frame. 0 means no limit.
    /// </summary>
    public int MaxMessagesPerPoll { get; set; } = 128;

    /// <summary>
    /// Most packets that may wait in one peer's receive queue. A peer that gets past it is sending
    /// faster than this game can ever handle and is disconnected (unreliable packets are just dropped).
    /// 0 means no limit.
    /// </summary>
    public int MaxQueuedMessages { get; set; } = 8192;

    /// <summary>
    /// Most reliable packets that may wait to be sent to one peer (see <see cref="Transport.ITransport.QueuedPackets"/>).
    /// Over it, sends to that peer are refused and <see cref="CoopSession.SendRefused"/> is raised. 0 means no limit.
    /// </summary>
    public int MaxSendQueue { get; set; } = 2048;

    /// <summary>Settings for big transfers (<see cref="CoopSession.Blobs"/>).</summary>
    public BlobOptions Blobs { get; set; } = new BlobOptions();

    /// <summary>Milliseconds from any fixed point. Tests replace it; the default is a stopwatch.</summary>
    public Func<long> Clock { get; set; } = DefaultClock;

    /// <summary>Optional diagnostics sink.</summary>
    public Action<string> Log { get; set; }

    private static readonly Stopwatch Watch = Stopwatch.StartNew();

    private static long DefaultClock() => Watch.ElapsedMilliseconds;
}
