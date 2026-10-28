namespace DevBridge;

/// <summary>Stored in Mods/DevBridge/config.json.</summary>
public sealed class BridgeConfig
{
    /// <summary>Only localhost by default. Anyone who can reach the port can control the game.</summary>
    public string Address = "127.0.0.1";
    public int Port = 27860;

    /// <summary>How long a request may wait for the game's main thread, in milliseconds.</summary>
    public int TimeoutMs = 15000;

    /// <summary>
    /// Keep the game running while its window isn't focused, whatever its own "Run In Background"
    /// option says. Without it an unfocused game stops and every bridge call times out, which also
    /// makes testing two copies side by side impossible. The player's saved option isn't changed.
    /// </summary>
    public bool KeepRunningInBackground = true;
}
