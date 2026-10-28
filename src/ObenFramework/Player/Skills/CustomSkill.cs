// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Player;

/// <summary>
/// A skill a mod added, from <see cref="CustomSkills.Register"/>. Its value is kept per save. It reads and
/// works like a game skill: the whole part of the value is the level, levelling up shows the game's banner,
/// and it is listed with the game's skills on the character page.
/// </summary>
public sealed class CustomSkill
{
    internal CustomSkill(string modId, string id, string displayName, float max, bool showInCharacterPanel)
    {
        ModId = modId;
        Id = id;
        DisplayName = displayName;
        Max = max;
        ShowInCharacterPanel = showInCharacterPanel;
        Key = CustomSkillMath.Key(modId, id);
    }

    public string ModId { get; }
    public string Id { get; }
    public string DisplayName { get; }

    /// <summary>The highest value (10 like the game's skills unless the mod chose otherwise).</summary>
    public float Max { get; }

    public bool ShowInCharacterPanel { get; }

    /// <summary>False once the owning mod unloaded; the value stays in the save but can't be changed through this.</summary>
    public bool IsRegistered { get; internal set; } = true;

    internal string Key { get; }

    /// <summary>The value, 0 to <see cref="Max"/>. Setting it is quiet (no banner).</summary>
    public float Value
    {
        get => CustomSkills.ValueOf(this);
        set => Change(value, false);
    }

    public int Level => CustomSkillMath.Level(Value);

    /// <summary>
    /// Adds to the value (a negative amount takes away). With <paramref name="banner"/> a level up shows the
    /// game's banner, "learned" for the first level. Returns false when nothing changed.
    /// </summary>
    public bool Add(float amount, bool banner = true) => Change(Value + amount, banner);

    private bool Change(float target, bool banner)
    {
        if (!CustomSkillMath.Finite(target))
            throw new ArgumentOutOfRangeException(nameof(target), "Give a finite number.");
        if (!IsRegistered)
            throw new InvalidOperationException($"{Key} is no longer registered (its mod unloaded).");
        return CustomSkills.Change(this, target, banner);
    }

    public override string ToString() => $"{DisplayName} ({Key}) {Value:0.###}";
}

/// <summary>A custom skill's value before and after a change, from <see cref="CustomSkills.Changed"/>.</summary>
public readonly struct CustomSkillChange
{
    public CustomSkillChange(CustomSkill skill, float before, float after)
    {
        Skill = skill;
        Before = before;
        After = after;
    }

    public CustomSkill Skill { get; }
    public float Before { get; }
    public float After { get; }

    public int LevelBefore => CustomSkillMath.Level(Before);
    public int LevelAfter => CustomSkillMath.Level(After);
    public bool LeveledUp => LevelAfter > LevelBefore;
}
