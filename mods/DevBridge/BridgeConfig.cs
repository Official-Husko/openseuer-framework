using System.ComponentModel;

namespace DevBridge;

/// <summary>Stored in Mods/DevBridge/config.json.</summary>
public sealed class BridgeConfig
{
    /// <summary>Only localhost by default. Anyone who can reach the port can control the game.</summary>
    [Description("The address the bridge listens on. Leave it on localhost, because anyone who can reach the port can control the game.")]
    public string Address = "127.0.0.1";
    [Description("The port the bridge listens on.")]
    public int Port = 27860;

    /// <summary>How long a request may wait for the game's main thread, in milliseconds.</summary>
    [Description("How long a request may wait for the game's main thread, in milliseconds.")]
    public int TimeoutMs = 15000;

    /// <summary>
    /// Keep the game running while its window isn't focused, whatever its own "Run In Background"
    /// option says. Without it an unfocused game stops and every bridge call times out, which also
    /// makes testing two copies side by side impossible. The player's saved option isn't changed.
    /// </summary>
    [Description("Keep the game running while its window isn't focused, even if its Run In Background option is off. The saved option isn't changed.")]
    public bool KeepRunningInBackground = true;
}
