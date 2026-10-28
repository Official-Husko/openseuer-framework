// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Clock;

/// <summary>
/// The clock overrides mods hold: who freezes time and the speeds they asked for. Any freeze stops time;
/// of the speeds the newest wins, and when its owner lets go the one before applies again. Plain
/// bookkeeping keyed by owner, so it is tested on its own.
/// </summary>
internal sealed class TimeOverrides
{
    public const float MinSpeed = 0.01f;
    public const float MaxSpeed = 100f;

    private readonly List<object> _freezers = new List<object>();
    private readonly List<KeyValuePair<object, float>> _speeds = new List<KeyValuePair<object, float>>();

    /// <summary>Some override is held; the clock hook does nothing otherwise.</summary>
    public bool Any => _freezers.Count > 0 || _speeds.Count > 0;

    public bool IsFrozen => _freezers.Count > 0;

    /// <summary>The speed in force: the newest one asked for, 1 when none.</summary>
    public float Speed => _speeds.Count == 0 ? 1f : _speeds[_speeds.Count - 1].Value;

    public IReadOnlyList<object> Freezers => _freezers;

    public IReadOnlyList<KeyValuePair<object, float>> Speeds => _speeds;

    public bool Freeze(object owner)
    {
        if (_freezers.Contains(owner))
            return false;
        _freezers.Add(owner);
        return true;
    }

    public bool Unfreeze(object owner) => _freezers.Remove(owner);

    public bool IsFrozenBy(object owner) => _freezers.Contains(owner);

    /// <summary>Sets the owner's speed, clamped to <see cref="MinSpeed"/>..<see cref="MaxSpeed"/>, and makes it the newest.</summary>
    public float SetSpeed(object owner, float speed)
    {
        if (float.IsNaN(speed) || speed <= 0f)
            throw new ArgumentOutOfRangeException(nameof(speed), "A speed must be above 0; freeze time to stop it.");
        float clamped = Math.Min(MaxSpeed, Math.Max(MinSpeed, speed));
        RemoveSpeed(owner);
        _speeds.Add(new KeyValuePair<object, float>(owner, clamped));
        return clamped;
    }

    public bool ClearSpeed(object owner) => RemoveSpeed(owner);

    /// <summary>Drops everything one owner holds. True when there was something.</summary>
    public bool Release(object owner)
    {
        bool freeze = _freezers.Remove(owner);
        bool speed = RemoveSpeed(owner);
        return freeze || speed;
    }

    public void Clear()
    {
        _freezers.Clear();
        _speeds.Clear();
    }

    /// <summary>
    /// The day length to hand the time controller, from the one the game asked for. While the game itself
    /// runs time faster (<paramref name="gameFactor"/> above 1: sleeping, waiting, crafting waits) the
    /// overrides stand aside, so those always finish. A day length of 0 stops the clock.
    /// </summary>
    public float DayLength(float requested, float gameFactor)
    {
        if (!Any || gameFactor > 1f)
            return requested;
        if (IsFrozen)
            return 0f;
        return requested / Speed;
    }

    private bool RemoveSpeed(object owner)
    {
        for (int i = 0; i < _speeds.Count; i++)
        {
            if (Equals(_speeds[i].Key, owner))
            {
                _speeds.RemoveAt(i);
                return true;
            }
        }
        return false;
    }
}
