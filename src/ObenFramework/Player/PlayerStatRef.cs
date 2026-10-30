using System;
using ObenFramework.Saves;

namespace ObenFramework.Player;

/// <summary>
/// One player stat (a need, condition, addiction or tuning rate) that can be read and written by
/// name. Every access goes through <c>PlayerStats.instance</c> at call time, because the game
/// replaces the stat sub-objects whenever a save loads. Instances live in <see cref="PlayerStat"/>.
/// </summary>
public sealed class PlayerStatRef
{
    private readonly Func<PlayerStats, float> _get;
    private readonly Action<PlayerStats, float> _set;

    internal PlayerStatRef(string name, string group, string description, Func<PlayerStats, float> get,
        Action<PlayerStats, float> set, float? min, float? max)
    {
        Name = name;
        Group = group;
        Description = description;
        Min = min;
        Max = max;
        _get = get;
        _set = set;
    }

    /// <summary>The name the F1 console's <c>player_stats</c> command uses, such as "hunger" or "alcohol_in_blood".</summary>
    public string Name { get; }

    /// <summary>"health", "needs", "mind", "alcohol", "mushrooms", "addictions", "body" or "rates".</summary>
    public string Group { get; }

    public string Description { get; }

    /// <summary>Lowest value <see cref="Set"/> accepts, or null when unbounded.</summary>
    public float? Min { get; }

    /// <summary>Highest value <see cref="Set"/> accepts, or null when unbounded.</summary>
    public float? Max { get; }

    /// <summary>The stat can be read right now (a player exists with its stats built).</summary>
    public bool Available => TryGet(out _);

    /// <summary>Current value. Throws when there is no player; use <see cref="TryGet"/> to check.</summary>
    public float Value
    {
        get => TryGet(out float value) ? value : throw NoPlayer();
        set => Set(value);
    }

    public bool TryGet(out float value)
    {
        var stats = PlayerStats.instance;
        if (stats != null)
        {
            try
            {
                value = _get(stats);
                return true;
            }
            catch (NullReferenceException)
            {
                // The sub-object isn't built yet (very early in a load).
            }
        }
        value = 0f;
        return false;
    }

    /// <summary>
    /// Writes the value, clamped to <see cref="Min"/> and <see cref="Max"/>, and refreshes the stat bars.
    /// Throws while a save is written or loaded (<see cref="SaveGate.IsBusy"/>): the load puts the saved
    /// value back, and a write during a save would change what is saved.
    /// </summary>
    /// <returns>The value that was stored.</returns>
    public float Set(float value)
    {
        var stats = PlayerStats.instance;
        if (stats == null)
            throw NoPlayer();
        if (SaveGate.IsBusy)
            throw new InvalidOperationException($"'{Name}' can't be changed while a save is being written or loaded.");

        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), $"'{Name}' can't be set to {value}.");
        if (Min.HasValue && value < Min.Value)
            value = Min.Value;
        if (Max.HasValue && value > Max.Value)
            value = Max.Value;

        try
        {
            _set(stats, value);
        }
        catch (NullReferenceException)
        {
            throw new InvalidOperationException($"The player's '{Name}' stat isn't ready yet.");
        }
        stats.BarUpdate();
        return value;
    }

    /// <summary>Adds to the current value (negative subtracts), with the same clamping as <see cref="Set"/>.</summary>
    public float Add(float delta) => Set(Value + delta);

    public override string ToString() => Name;

    private static InvalidOperationException NoPlayer() =>
        new InvalidOperationException("There is no player right now (main menu or loading).");
}
