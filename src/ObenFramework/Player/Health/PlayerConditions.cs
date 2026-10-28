// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using PlayerStatsConditions;

namespace ObenFramework.Player;

/// <summary>A health condition the game knows, from <see cref="PlayerConditions.All"/>.</summary>
public sealed class ConditionInfo
{
    internal ConditionInfo(BaseHealthCondition condition, int count)
    {
        Name = condition.name;
        DisplayName = condition.conditionName;
        Description = condition.description;
        Hidden = condition.hidden;
        CanStack = condition.canStack;
        Count = count;
        Strength = count > 0 ? condition.strength : 0f;
    }

    /// <summary>The asset name, which is what the other calls take.</summary>
    public string Name { get; }
    public string DisplayName { get; }
    public string Description { get; }

    /// <summary>The game doesn't list it on the health page.</summary>
    public bool Hidden { get; }

    public bool CanStack { get; }

    /// <summary>How many times it is active (0 when it isn't; more than 1 only for stacking ones).</summary>
    public int Count { get; }

    public bool Active => Count > 0;
    public float Strength { get; }
}

/// <summary>A condition was gained, lost or changed strength, from <see cref="PlayerConditions.Changed"/>.</summary>
public readonly struct ConditionChange
{
    public ConditionChange(string name, bool active, float strength)
    {
        Name = name;
        Active = active;
        Strength = strength;
    }

    public string Name { get; }
    public bool Active { get; }
    public float Strength { get; }
}

/// <summary>
/// The player's health conditions (from low hygiene, radiation and the like): list them, check, add and
/// remove by asset name, and hear about changes. The game rolls them from lists on the player's stats;
/// the catalogue is those lists plus whatever is active.
/// </summary>
public static class PlayerConditions
{
    // A Changed handler that adds or removes a condition raises Changed again; past this depth it is refused.
    private const int MaxDepth = 8;
    private static int _depth;

    public static readonly GameEvent<ConditionChange> Changed = new GameEvent<ConditionChange>(nameof(Changed));

    /// <summary>Every condition the player can get or has, with its state. Empty without a player.</summary>
    public static List<ConditionInfo> All()
    {
        var all = new List<ConditionInfo>();
        foreach (var condition in Catalogue())
            all.Add(new ConditionInfo(condition, Count(condition)));
        return all;
    }

    /// <summary>The conditions that are active now.</summary>
    public static List<ConditionInfo> Active()
    {
        var active = new List<ConditionInfo>();
        foreach (var condition in Catalogue())
        {
            int count = Count(condition);
            if (count > 0)
                active.Add(new ConditionInfo(condition, count));
        }
        return active;
    }

    public static bool Has(string name)
    {
        var condition = Find(name);
        return condition != null && Count(condition) > 0;
    }

    /// <summary>
    /// Gives the player a condition, with a strength rolled like the game does. A stacking condition is added
    /// once more; one that doesn't stack gets stronger if it is already active and is added otherwise. False
    /// for an unknown name, without a player or while saving.
    /// </summary>
    public static bool Add(string name)
    {
        var condition = Find(name);
        if (condition == null || Refused())
            return false;

        if (condition.canStack || Count(condition) > 0)
        {
            condition.ApplyCondition(force: true);
            return true;
        }

        // The game's own call only strengthens a non-stacking condition that is already there, so the
        // first one is added here the way the stacking branch would add it.
        condition.strength = UnityEngine.Random.Range(condition.strengthRange.x, condition.strengthRange.y);
        PlayerStats.instance.conditions.activeConditions.Add(condition);
        if (condition.notify && Notifications.instance != null)
            Notifications.instance.CreateNotification("New condition!", condition.conditionName);
        Raise(condition, float.NaN);
        return true;
    }

    /// <summary>Takes one instance of a condition away. False when it isn't active.</summary>
    public static bool Remove(string name)
    {
        var condition = Find(name);
        if (condition == null || Refused() || Count(condition) == 0)
            return false;
        condition.RemoveCondition();
        return true;
    }

    /// <summary>The strength of a condition if it is active, NaN if it isn't (what the patches compare).</summary>
    internal static float Level(BaseHealthCondition condition) => Count(condition) > 0 ? condition.strength : float.NaN;

    internal static void Raise(BaseHealthCondition condition, float before)
    {
        float after = Level(condition);
        if (float.IsNaN(after) && float.IsNaN(before))
            return;
        if (after == before)
            return;
        bool active = !float.IsNaN(after);
        _depth++;
        try
        {
            Changed.Raise(new ConditionChange(condition.name, active, active ? after : 0f), GameEvents.Log);
        }
        finally
        {
            _depth--;
        }
    }

    private static bool Refused()
    {
        if (SaveGate.IsBusy || PlayerStats.instance == null || PlayerStats.instance.conditions == null)
            return true;
        if (_depth <= MaxDepth)
            return false;
        GameEvents.Log.Warn("PlayerConditions: Changed handlers keep changing conditions; the change was refused.");
        return true;
    }

    private static int Count(BaseHealthCondition condition)
    {
        var stats = PlayerStats.instance;
        if (stats == null || stats.conditions == null || stats.conditions.activeConditions == null)
            return 0;
        int count = 0;
        foreach (var active in stats.conditions.activeConditions)
        {
            if (ReferenceEquals(active, condition))
                count++;
        }
        return count;
    }

    private static List<BaseHealthCondition> Catalogue()
    {
        var all = new List<BaseHealthCondition>();
        var stats = PlayerStats.instance;
        if (stats == null)
            return all;
        AddAll(all, stats.hygiene != null ? stats.hygiene.lowHygieneConditions : null);
        AddAll(all, stats.radiation != null ? stats.radiation.highDoseConditions : null);
        AddAll(all, stats.conditions != null ? stats.conditions.activeConditions : null);
        return all;
    }

    private static void AddAll(List<BaseHealthCondition> all, List<BaseHealthCondition> source)
    {
        if (source == null)
            return;
        foreach (var condition in source)
        {
            if (condition != null && !all.Contains(condition))
                all.Add(condition);
        }
    }

    private static BaseHealthCondition Find(string name)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentException("Give a condition's asset name (see PlayerConditions.All).", nameof(name));
        foreach (var condition in Catalogue())
        {
            if (condition.name == name)
                return condition;
        }
        return null;
    }
}
