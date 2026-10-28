// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using System.Globalization;
using ObenFramework.Game;
using ObenLoader;

namespace ObenFramework.NewGames;

/// <summary>One option value of a started game, as handed out by <see cref="NewGameOptions.Values"/>.</summary>
public sealed class NewGameOptionValue
{
    internal NewGameOptionValue(string modId, string key, string title, string value)
    {
        ModId = modId;
        Key = key;
        Title = title;
        Value = value;
    }

    public string ModId { get; }

    public string Key { get; }

    /// <summary>The option's title, or the key when its mod isn't loaded.</summary>
    public string Title { get; }

    /// <summary>The stored text form: "1"/"0", a choice id, or a number in invariant culture.</summary>
    public string Value { get; }
}

public static partial class NewGameOptions
{
    /// <summary>
    /// The option's value for the game being played: what was stored when the game started, else the
    /// option's default (a save started before the option existed). At the main menu it is what the
    /// New Game screen shows. Null when there is neither a stored value nor a registered option.
    /// </summary>
    public static string GetValue(string modId, string key)
    {
        var option = Store.Find(modId, key);
        var stored = NewGameSave.Data?.Get(modId, key);
        if (stored != null)
            return option != null ? option.Normalize(stored) : stored;
        if (option == null)
            return null;
        return WorldState.InMainMenu ? Store.DraftOrDefault(modId, key) : option.Default;
    }

    /// <summary>True when the game being played stored a value for this option when it started.</summary>
    public static bool WasChosen(string modId, string key) => NewGameSave.Data?.Get(modId, key) != null;

    public static bool GetBool(string modId, string key, bool fallback = false)
    {
        string value = GetValue(modId, key);
        return value == "1" || (value != "0" && fallback);
    }

    /// <summary>The chosen id of a choice option.</summary>
    public static string GetChoice(string modId, string key, string fallback = null) => GetValue(modId, key) ?? fallback;

    public static int GetInteger(string modId, string key, int fallback = 0) =>
        int.TryParse(GetValue(modId, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int value) ? value : fallback;

    public static float GetFloat(string modId, string key, float fallback = 0f) =>
        float.TryParse(GetValue(modId, key), NumberStyles.Float, CultureInfo.InvariantCulture, out float value) ? value : fallback;

    public static bool GetBool(ObenMod owner, string key, bool fallback = false) => GetBool(owner.Info.Id, key, fallback);

    public static string GetChoice(ObenMod owner, string key, string fallback = null) => GetChoice(owner.Info.Id, key, fallback);

    public static int GetInteger(ObenMod owner, string key, int fallback = 0) => GetInteger(owner.Info.Id, key, fallback);

    public static float GetFloat(ObenMod owner, string key, float fallback = 0f) => GetFloat(owner.Info.Id, key, fallback);

    /// <summary>Every value stored with the game being played, including those of mods that aren't loaded now.</summary>
    public static List<NewGameOptionValue> Values()
    {
        var values = new List<NewGameOptionValue>();
        var data = NewGameSave.Data;
        if (data?.Options == null)
            return values;
        foreach (var entry in data.Options)
        {
            if (entry == null || entry.ModId == null || entry.Key == null)
                continue;
            var option = Store.Find(entry.ModId, entry.Key);
            values.Add(new NewGameOptionValue(entry.ModId, entry.Key, option != null ? option.Title : entry.Key,
                option != null ? option.Normalize(entry.Value) : entry.Value));
        }
        return values;
    }
}
