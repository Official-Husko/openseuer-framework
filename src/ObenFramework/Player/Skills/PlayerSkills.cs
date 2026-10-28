// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using SkillType = PlayerStats.PlayerSkillType;

namespace ObenFramework.Player;

/// <summary>A skill's value before and after a change, from <see cref="PlayerSkills.Changed"/>.</summary>
public readonly struct SkillChange
{
    public SkillChange(SkillType skill, float before, float after)
    {
        Skill = skill;
        Before = before;
        After = after;
    }

    public SkillType Skill { get; }
    public float Before { get; }
    public float After { get; }

    public int LevelBefore => (int)Math.Floor(Before);
    public int LevelAfter => (int)Math.Floor(After);

    /// <summary>The whole-number level went up.</summary>
    public bool LeveledUp => LevelAfter > LevelBefore;
}

/// <summary>A skill and its value, from <see cref="PlayerSkills.All"/>.</summary>
public readonly struct SkillValue
{
    public SkillValue(SkillType skill, float value)
    {
        Skill = skill;
        Value = value;
    }

    public SkillType Skill { get; }

    /// <summary>The skill as the game keeps it: the whole part is the level, the fraction the progress to the next.</summary>
    public float Value { get; }

    public int Level => (int)Math.Floor(Value);
}

/// <summary>
/// The player's game skills (cooking, lockpicking, mining...): read, raise, set, hear about changes, and
/// scale how fast they are learned. Skills start at 1 and go to <see cref="MaxLevel"/>; the game shows a
/// skill as learned once it passes 1. For skills of a mod's own see <see cref="CustomSkills"/>.
/// </summary>
public static class PlayerSkills
{
    private static readonly int SkillCount = Enum.GetValues(typeof(SkillType)).Length;

    // Channel 0 (PlayerSkillType.None) scales every skill; the others one skill each.
    internal static readonly MultiplierTable Gains = new MultiplierTable(SkillCount, 100f);

    private static readonly OwnerWatch Owners = new OwnerWatch(id => Gains.RemoveOwner(id));

    /// <summary>A skill's value changed, through the game's own learning or a mod. Not raised while a save loads.</summary>
    public static readonly GameEvent<SkillChange> Changed = new GameEvent<SkillChange>(nameof(Changed));

    /// <summary>The highest level (10 in the game).</summary>
    public static int MaxLevel => PlayerStats.maxLevel;

    /// <summary>A skill's value, or 0 without a player.</summary>
    public static float Get(SkillType skill)
    {
        var entry = Entry(skill);
        return entry != null ? entry.Skill : 0f;
    }

    public static int Level(SkillType skill) => (int)Math.Floor(Get(skill));

    /// <summary>
    /// Raises (or with a negative amount lowers) a skill the way the game does when the player practises:
    /// the level-up banner, skill achievements and, with <paramref name="notify"/>, a toast. Learning
    /// multipliers are applied to a positive amount. False without a player or at the top level.
    /// </summary>
    public static bool Add(SkillType skill, float amount, bool notify = false)
    {
        CheckSkill(skill);
        CheckFinite(amount, nameof(amount));
        var stats = PlayerStats.instance;
        return stats != null && stats.IncreaseSkill(skill, amount, notify);
    }

    /// <summary>
    /// Sets a skill to a value between 0 and <see cref="MaxLevel"/>, quietly: no banner, toast or achievement.
    /// Returns the value stored, or null without a player.
    /// </summary>
    public static float? Set(SkillType skill, float value)
    {
        CheckSkill(skill);
        CheckFinite(value, nameof(value));
        var entry = Entry(skill);
        if (entry == null)
            return null;

        float before = entry.Skill;
        entry.Skill = Math.Max(0f, Math.Min(MaxLevel, value));
        if (entry.Skill != before)
            Changed.Raise(new SkillChange(skill, before, entry.Skill), GameEvents.Log);
        return entry.Skill;
    }

    /// <summary>Every skill and its value, empty without a player.</summary>
    public static List<SkillValue> All()
    {
        var all = new List<SkillValue>();
        var stats = PlayerStats.instance;
        if (stats == null || stats.Skills == null)
            return all;
        foreach (var entry in stats.Skills)
        {
            if (entry != null && entry.Type != SkillType.None)
                all.Add(new SkillValue(entry.Type, entry.Skill));
        }
        return all;
    }

    /// <summary>
    /// Scales how fast the player learns: every positive gain the game (or <see cref="Add"/>) hands out is
    /// multiplied. Pass <c>None</c> for every skill. Mods' multipliers combine by multiplying; the same owner
    /// and skill again replaces the earlier value. Dispose the result to take it away; it also goes when
    /// the mod unloads.
    /// </summary>
    /// <param name="multiplier">0 stops learning, 1 changes nothing, 2 learns twice as fast. At most 100.</param>
    public static IDisposable SetLearningMultiplier(ObenMod owner, float multiplier, SkillType skill = SkillType.None)
    {
        CheckChannel(skill);
        if (!Gains.Accepts(multiplier))
            throw new ArgumentOutOfRangeException(nameof(multiplier), $"A multiplier must be a number from 0 to {Gains.Max}.");
        string id = Owners.Watch(owner);
        Gains.Set(id, (int)skill, null, multiplier);
        return Disposable.Create(() => Gains.Remove(id, (int)skill, null));
    }

    /// <summary>The combined learning multiplier for a skill, the all-skills one included.</summary>
    public static float LearningMultiplier(SkillType skill)
    {
        CheckChannel(skill);
        float all = Gains.Product(0);
        return skill == SkillType.None ? all : all * Gains.Product((int)skill);
    }

    /// <summary>Every learning multiplier mods have set. <see cref="MultiplierEntry.Channel"/> is a skill type.</summary>
    public static MultiplierEntry[] LearningMultipliers => Gains.Entries;

    internal static void Init(ObenMod framework) => framework.Track(Disposable.Create(() =>
    {
        Gains.Clear();
        Owners.Clear();
    }));

    internal static PlayerStats.PlayerSkill Entry(SkillType skill)
    {
        var stats = PlayerStats.instance;
        if (stats == null || stats.Skills == null)
            return null;
        foreach (var entry in stats.Skills)
        {
            if (entry != null && entry.Type == skill)
                return entry;
        }
        return null;
    }

    private static void CheckSkill(SkillType skill)
    {
        if (skill == SkillType.None || !Enum.IsDefined(typeof(SkillType), skill))
            throw new ArgumentOutOfRangeException(nameof(skill), "Pick a real skill.");
    }

    private static void CheckChannel(SkillType skill)
    {
        if ((int)skill < 0 || (int)skill >= SkillCount)
            throw new ArgumentOutOfRangeException(nameof(skill));
    }

    private static void CheckFinite(float value, string name)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "Give a finite number.");
    }
}
