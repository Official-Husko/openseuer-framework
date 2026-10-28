using System;
using ObenLoader;
using ObenLoader.Config;

namespace CheatMenu;

/// <summary>
/// What the sections share: the config, a status line they write to, and a guard that turns a
/// failed cheat into a sentence on that line instead of an error.
/// </summary>
internal sealed class CheatContext
{
    public const string NeedsGame = "Load a game to use this.";

    private readonly ObenMod _mod;

    public CheatContext(ObenMod mod, ConfigFile<CheatMenuConfig> config)
    {
        _mod = mod;
        Config = config;
    }

    public ConfigFile<CheatMenuConfig> Config { get; }

    /// <summary>The last thing a cheat said; shown at the top of the panel.</summary>
    public string Status { get; private set; } = "";

    public void Say(string text) => Status = text;

    /// <summary>
    /// Runs a cheat. The framework throws InvalidOperationException with a plain message when
    /// there is nothing to act on (no player, no clock); that message becomes the status.
    /// </summary>
    public void Run(string what, Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException e)
        {
            Say(e.Message);
        }
        catch (Exception e)
        {
            _mod.Log.Error($"{what} failed", e);
            Say($"{what} failed: {e.Message}");
        }
    }
}
