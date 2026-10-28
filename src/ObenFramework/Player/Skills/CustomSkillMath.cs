// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Player;

/// <summary>Rules and texts for custom skills. No Unity types, so it is unit tested on its own.</summary>
internal static class CustomSkillMath
{
    /// <summary>How many skills one mod may register.</summary>
    public const int MaxPerMod = 64;

    /// <summary>The highest maximum a skill may have.</summary>
    public const float MaxValue = 1000000f;

    /// <summary>The key a skill's value is saved under: "modId:skillId".</summary>
    public static string Key(string modId, string skillId) => modId + ":" + skillId;

    public static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    public static float Clamp(float value, float max)
    {
        if (value < 0f)
            return 0f;
        return value > max ? max : value;
    }

    /// <summary>The whole-number level of a value, like the game's own skills.</summary>
    public static int Level(float value) => (int)Math.Floor(value);

    /// <summary>An id is 1 to 64 letters, digits, '.', '_' or '-'.</summary>
    public static bool ValidId(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > 64)
            return false;
        foreach (char c in id)
        {
            if (!(char.IsLetterOrDigit(c) || c == '.' || c == '_' || c == '-'))
                return false;
        }
        return true;
    }

    /// <summary>The game's level-up banner text for a skill name and its new level.</summary>
    public static string LevelUpText(string name, int level) => $"{name}\nincreased to\nlevel {level}!";

    /// <summary>The game's banner text for a skill learned for the first time.</summary>
    public static string LearnedText(string name) => "New skill\n" + name + "\nlearned!";

    /// <summary>
    /// The banner a change from <paramref name="before"/> to <paramref name="after"/> deserves, or null:
    /// "learned" on the way from level 0 up, "increased" for a later level up, nothing otherwise.
    /// </summary>
    public static string BannerFor(string name, float before, float after)
    {
        int from = Level(before);
        int to = Level(after);
        if (to <= from)
            return null;
        return from == 0 ? LearnedText(name) : LevelUpText(name, to);
    }

    /// <summary>Copies the saved values that make sense (a valid key, a finite value of 0 or more).</summary>
    public static Dictionary<string, float> CleanValues(IDictionary<string, float> saved)
    {
        var clean = new Dictionary<string, float>();
        if (saved == null)
            return clean;
        foreach (var pair in saved)
        {
            if (ValidKey(pair.Key) && Finite(pair.Value) && pair.Value >= 0f)
                clean[pair.Key] = pair.Value;
        }
        return clean;
    }

    // Mod ids follow the mod's own manifest, so only the skill part (after the last colon) is checked.
    private static bool ValidKey(string key)
    {
        if (key == null)
            return false;
        int colon = key.LastIndexOf(':');
        return colon > 0 && ValidId(key.Substring(colon + 1));
    }
}
