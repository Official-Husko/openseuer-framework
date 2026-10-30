// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.World.Logic;

/// <summary>A true/false switch of the level's logic (<c>logic_branch</c>).</summary>
public readonly struct BranchInfo
{
    public BranchInfo(string id, bool value)
    {
        Id = id;
        Value = value;
    }

    /// <summary>Id to pass back to <see cref="SceneState"/>, see <c>SceneIds</c>.</summary>
    public string Id { get; }

    public bool Value { get; }
}

/// <summary>A counter of the level's logic (<c>CounterRelay</c>) that fires when it reaches its maximum.</summary>
public readonly struct CounterInfo
{
    public CounterInfo(string id, int value, int max)
    {
        Id = id;
        Value = value;
        Max = max;
    }

    public string Id { get; }
    public int Value { get; }
    public int Max { get; }
}

/// <summary>A named-state machine of the level's logic (<c>logic_state</c>).</summary>
public sealed class StateMachineInfo
{
    internal StateMachineInfo(string id, string current, List<string> names)
    {
        Id = id;
        Current = current;
        Names = names;
    }

    public string Id { get; }

    /// <summary>The current state's name (can be empty before the first change).</summary>
    public string Current { get; }

    /// <summary>The states it knows, in order.</summary>
    public IReadOnlyList<string> Names { get; }
}

/// <summary>A timed list of outputs (<c>Logic_Sequence</c>).</summary>
public readonly struct SequenceInfo
{
    public SequenceInfo(string id, int entries, bool loop)
    {
        Id = id;
        Entries = entries;
        Loop = loop;
    }

    public string Id { get; }
    public int Entries { get; }
    public bool Loop { get; }
}

/// <summary>A repeating timer (<c>logic_timer</c>).</summary>
public readonly struct SceneTimerInfo
{
    public SceneTimerInfo(string id, float interval, bool running, bool disabled)
    {
        Id = id;
        Interval = interval;
        Running = running;
        Disabled = disabled;
    }

    public string Id { get; }

    /// <summary>Seconds between firings.</summary>
    public float Interval { get; }

    public bool Running { get; }
    public bool Disabled { get; }
}

/// <summary>A relay: the game's "fire these outputs" node that most level scripting goes through.</summary>
public readonly struct RelayInfo
{
    public RelayInfo(string id, bool fireOnce, bool fired, bool enabled)
    {
        Id = id;
        FireOnce = fireOnce;
        Fired = fired;
        Enabled = enabled;
    }

    public string Id { get; }

    /// <summary>The relay only ever fires once.</summary>
    public bool FireOnce { get; }

    /// <summary>It has fired at least once (saved by the game).</summary>
    public bool Fired { get; }

    /// <summary>None of its outputs is switched off.</summary>
    public bool Enabled { get; }
}

/// <summary>Raised by <see cref="SceneLogic.Fired"/> after a relay fired its outputs.</summary>
public readonly struct RelayFired
{
    public RelayFired(string id, string name, bool fireOnce, bool enabled = true)
    {
        Id = id;
        Name = name;
        FireOnce = fireOnce;
        Enabled = enabled;
    }

    public string Id { get; }

    /// <summary>The relay object's name, handy for logs.</summary>
    public string Name { get; }

    public bool FireOnce { get; }

    /// <summary>None of its outputs was switched off when it fired.</summary>
    public bool Enabled { get; }
}
