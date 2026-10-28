// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Globalization;

namespace ObenFramework.NewGames;

/// <summary>What kind of control a new game option is.</summary>
public enum NewGameOptionKind
{
    Toggle,
    Choice,
    Integer,
    Float,
}

/// <summary>Where a new game option is listed in the New Game options window.</summary>
public enum NewGamePlacement
{
    /// <summary>With the general options, shown for every difficulty.</summary>
    BelowDifficulty = 0,

    /// <summary>In the "custom rules" group, below the general options.</summary>
    CustomPanel = 1,
}

/// <summary>
/// A registered new game option: what the New Game screen shows and how the value is stored.
/// Values are stored as text: "1" or "0" for a toggle, the chosen id for a choice, and the number in
/// invariant culture for integers and floats. No Unity types.
/// </summary>
public sealed class NewGameOption
{
    internal NewGameOption(string modId, string key, string title, string description, NewGameOptionKind kind,
        NewGamePlacement placement, string defaultValue)
    {
        ModId = modId;
        Key = key;
        Title = title;
        Description = description ?? "";
        Kind = kind;
        Placement = placement;
        Default = defaultValue;
    }

    public string ModId { get; }

    public string Key { get; }

    public string Title { get; }

    public string Description { get; }

    public NewGameOptionKind Kind { get; }

    public NewGamePlacement Placement { get; }

    /// <summary>The default value in its stored text form.</summary>
    public string Default { get; internal set; }

    /// <summary>Choice ids (stored) and titles (shown); null for other kinds.</summary>
    public string[] Ids { get; internal set; }

    public string[] Titles { get; internal set; }

    /// <summary>Range of an integer option.</summary>
    public int Min { get; internal set; }

    public int Max { get; internal set; }

    /// <summary>Range and step of a float option.</summary>
    public float FloatMin { get; internal set; }

    public float FloatMax { get; internal set; }

    public float FloatStep { get; internal set; }

    /// <summary>How many steps a float option has from its min to its max.</summary>
    public int FloatSteps { get; internal set; }

    /// <summary>
    /// Turns a stored value into one that fits this option: an unknown choice id, a number outside the
    /// range or off the float grid, or text of the wrong kind gives the nearest valid value or the default.
    /// </summary>
    public string Normalize(string value)
    {
        if (value == null)
            return Default;
        switch (Kind)
        {
            case NewGameOptionKind.Toggle:
                return value == "1" || value == "0" ? value : Default;
            case NewGameOptionKind.Choice:
                return Array.IndexOf(Ids, value) >= 0 ? value : Default;
            case NewGameOptionKind.Integer:
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number))
                    return Default;
                return NewGameOptionRules.Text(Math.Max(Min, Math.Min(Max, number)));
            default:
                if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float real) || float.IsNaN(real) || float.IsInfinity(real))
                    return Default;
                return NewGameOptionRules.Text(NewGameOptionRules.FloatAt(this, NewGameOptionRules.FloatIndex(this, real)));
        }
    }

    /// <summary>The value as the screen shows it: Enabled/Disabled, the choice's title, or the number.</summary>
    public string Display(string value)
    {
        value = Normalize(value);
        switch (Kind)
        {
            case NewGameOptionKind.Toggle:
                return value == "1" ? "Enabled" : "Disabled";
            case NewGameOptionKind.Choice:
                return Titles[Array.IndexOf(Ids, value)];
            default:
                return value;
        }
    }
}
