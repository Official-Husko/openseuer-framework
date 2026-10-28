using System;
using ObenLoader;
using ObenLoader.Config;
using UnityEngine;

namespace CheatMenu;

/// <summary>
/// What the sections share: the config, a status line they write to, and a guard that turns a
/// failed cheat into a sentence on that line instead of an error.
/// </summary>
internal sealed class CheatContext
{
    public const string NeedsGame = "Load a game to use this.";

    private const float SaveDelay = 0.5f;

    private readonly ObenMod _mod;
    private float _saveAt = -1f;

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
    /// Saves the config a moment after the last change, so dragging a slider writes the file once
    /// instead of every frame.
    /// </summary>
    public void SaveSoon() => _saveAt = Time.unscaledTime + SaveDelay;

    /// <summary>Called every frame by the mod; costs a comparison when there's nothing to save.</summary>
    public void Tick()
    {
        if (_saveAt >= 0f && Time.unscaledTime >= _saveAt)
            Flush();
    }

    /// <summary>Writes a pending change now (on quit and unload).</summary>
    public void Flush()
    {
        if (_saveAt < 0f)
            return;
        _saveAt = -1f;
        Config.Save();
    }

    /// <summary>Logs a failure that has no status line of its own (a menu part that couldn't be built).</summary>
    public void Fail(string what, Exception e) => _mod.Log.Error($"{what} failed", e);

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
