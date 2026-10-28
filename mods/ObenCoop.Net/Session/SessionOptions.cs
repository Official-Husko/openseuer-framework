using System;
using System.Diagnostics;

namespace ObenCoop.Net.Session;

/// <summary>What a session needs to know about this game and player.</summary>
public sealed class SessionOptions
{
    /// <summary>The persistent id of the local player (from the identity file).</summary>
    public Guid PlayerId { get; set; } = Guid.NewGuid();

    public string PlayerName { get; set; } = "Player";

    /// <summary>The game's own version. Host and clients must match.</summary>
    public string GameVersion { get; set; } = "";

    /// <summary>The mods everyone must share. Null means none.</summary>
    public ModSet Mods { get; set; } = ModSet.Empty;

    /// <summary>Players in a hosted session, host included (2 to <see cref="Protocol.MaxPlayers"/>).</summary>
    public int MaxPlayers { get; set; } = 4;

    /// <summary>A connection that sends no valid Hello within this time is dropped (host side).</summary>
    public int HandshakeTimeoutMs { get; set; } = 5000;

    /// <summary>A client gives up when the host does not answer its Hello within this time.</summary>
    public int WelcomeTimeoutMs { get; set; } = 10000;

    /// <summary>Milliseconds from any fixed point. Tests replace it; the default is a stopwatch.</summary>
    public Func<long> Clock { get; set; } = DefaultClock;

    /// <summary>Optional diagnostics sink.</summary>
    public Action<string> Log { get; set; }

    private static readonly Stopwatch Watch = Stopwatch.StartNew();

    private static long DefaultClock() => Watch.ElapsedMilliseconds;
}
