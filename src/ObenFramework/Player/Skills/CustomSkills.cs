// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>What the framework keeps per save for custom skills: every value by "modId:skillId".</summary>
public sealed class CustomSkillSave
{
    public Dictionary<string, float> Values = new Dictionary<string, float>();
}

/// <summary>
/// Skills of a mod's own (fishing, haggling, plumbing...). Register once when the mod loads; the value is
/// kept per save by the framework, shown with the game's skills on the character page, and levelling up
/// shows the game's banner. Values of a mod that is not installed stay in the save untouched.
/// </summary>
public static class CustomSkills
{
    private static readonly List<CustomSkill> Registered = new List<CustomSkill>();
    private static ModSaveData<CustomSkillSave> _data;

    /// <summary>A custom skill's value changed. Not raised when a save loads.</summary>
    public static readonly GameEvent<CustomSkillChange> Changed = new GameEvent<CustomSkillChange>(nameof(Changed));

    /// <summary>
    /// Adds a skill for <paramref name="owner"/>. It is removed when the mod unloads, and the next copy of the
    /// mod registers it again with the same id and gets the same value back.
    /// </summary>
    /// <param name="id">1 to 64 letters, digits, '.', '_' or '-'; unique within the mod.</param>
    /// <param name="displayName">The name the banner and the character page show.</param>
    /// <param name="max">Highest value, 10 like the game's skills by default.</param>
    public static CustomSkill Register(ObenMod owner, string id, string displayName, float max = 10f, bool showInCharacterPanel = true)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (!CustomSkillMath.ValidId(id))
            throw new ArgumentException("A skill id is 1 to 64 letters, digits, '.', '_' or '-'.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A skill needs a name to show.", nameof(displayName));
        if (!CustomSkillMath.Finite(max) || max <= 0f || max > CustomSkillMath.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(max), $"The maximum must be above 0 and at most {CustomSkillMath.MaxValue}.");

        string modId = owner.Info.Id;
        if (Find(modId, id) != null)
            throw new InvalidOperationException($"{modId} already registered the skill '{id}'.");
        if (Registered.FindAll(s => s.ModId == modId).Count >= CustomSkillMath.MaxPerMod)
            throw new InvalidOperationException($"{modId} can't register more than {CustomSkillMath.MaxPerMod} skills.");

        var skill = new CustomSkill(modId, id, displayName, max, showInCharacterPanel);
        Registered.Add(skill);
        owner.Track(Disposable.Create(() => Unregister(skill)));
        return skill;
    }

    /// <summary>A registered skill, or null.</summary>
    public static CustomSkill Find(string modId, string id)
    {
        string key = CustomSkillMath.Key(modId, id);
        foreach (var skill in Registered)
        {
            if (skill.Key == key)
                return skill;
        }
        return null;
    }

    /// <summary>Every registered skill, in the order they were registered.</summary>
    public static IReadOnlyList<CustomSkill> All => Registered.ToArray();

    /// <summary>The value the loaded save holds for a skill, registered or not (0 when it has none).</summary>
    public static float SavedValue(string modId, string id) =>
        _data != null && _data.Value.Values.TryGetValue(CustomSkillMath.Key(modId, id), out float value) ? value : 0f;

    /// <summary>
    /// Shows a line of text in the skill banner, after whatever banner is showing or waiting. False without
    /// a player or when five texts are already waiting.
    /// </summary>
    public static bool ShowBanner(string text)
    {
        if (string.IsNullOrEmpty(text))
            throw new ArgumentException("The banner needs text.", nameof(text));
        return PlayerSkillIncreaseUI.instance != null && SkillBanners.AddText(text);
    }

    internal static void Init(ObenMod framework)
    {
        _data = ModSaves.Register<CustomSkillSave>(framework, "customskills");
        Clean(_data.Value);
        _data.DataLoaded.Subscribe(framework, Clean);
        framework.Track(Disposable.Create(() =>
        {
            foreach (var skill in Registered)
                skill.IsRegistered = false;
            Registered.Clear();
            _data = null;
        }));
    }

    internal static float ValueOf(CustomSkill skill) =>
        _data != null && _data.Value.Values.TryGetValue(skill.Key, out float value) ? CustomSkillMath.Clamp(value, skill.Max) : 0f;

    internal static bool Change(CustomSkill skill, float target, bool banner)
    {
        if (_data == null || !WorldState.InWorld || SaveGate.IsBusy)
            return false;

        float before = ValueOf(skill);
        float after = CustomSkillMath.Clamp(target, skill.Max);
        if (after == before)
            return false;

        _data.Value.Values[skill.Key] = after;
        if (banner)
        {
            string text = CustomSkillMath.BannerFor(skill.DisplayName, before, after);
            if (text != null)
                SkillBanners.AddSkill(skill.Key, text);
        }
        Changed.Raise(new CustomSkillChange(skill, before, after), GameEvents.Log);
        return true;
    }

    private static void Unregister(CustomSkill skill)
    {
        skill.IsRegistered = false;
        Registered.Remove(skill);
    }

    private static void Clean(CustomSkillSave save)
    {
        save.Values = CustomSkillMath.CleanValues(save.Values);
        SkillBanners.Clear();
    }
}
