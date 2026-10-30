using System;
using System.Collections.Generic;

namespace ObenFramework.Dialogue;

/// <summary>
/// Argument checks for what goes into the Lua state. The game saves the state as Lua source text and
/// escapes very little of it, so a backslash in a value or a name can corrupt the whole dialogue save.
/// </summary>
internal static class LuaChecks
{
    // Variables the game and the Dialogue System set themselves: the day, the player's name and age, the
    // running conversation's actors, shop prices, the police and wake-up lines, the level the save was made
    // in. "Converrsant" is the game's own spelling. Dreams store DREAM_<n> when the game saves.
    private static readonly HashSet<string> GameVariables = new HashSet<string>(StringComparer.Ordinal)
    {
        "Actor", "ActorIndex", "Alert", "Conversant", "ConversantIndex", "Conversation", "Converrsant", "Currency",
        "Day", "PlayerAge", "PlayerFirstName", "PlayerGender", "PlayerLastName", "PoliceIdle", "Price",
        "SavedLevelName", "Speaker", "WakeUpReason",
    };

    private const string DreamPrefix = "DREAM_";

    public static bool IsGameVariable(string name) =>
        name != null && (GameVariables.Contains(name) || name.StartsWith(DreamPrefix, StringComparison.Ordinal));

    /// <summary>A variable name a mod may write: <see cref="Name"/>, and not one the game writes itself.</summary>
    public static void VariableName(string value, string paramName)
    {
        Name(value, paramName);
        if (IsGameVariable(value))
            throw new ArgumentException($"'{value}' is written by the game itself, which would overwrite the change; it can only be read.", paramName);
    }

    public static void Name(string value, string paramName)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException("The name can't be empty.", paramName);
        foreach (char c in value)
        {
            if (c == '\\' || char.IsControl(c))
                throw new ArgumentException($"'{value}' has a backslash or control character, which the game's dialogue save can't store.", paramName);
        }
    }

    /// <summary>Returns the value in a form the Lua state stores as is: string, bool, int or double.</summary>
    public static object Value(object value, string paramName)
    {
        switch (value)
        {
            case null:
                throw new ArgumentNullException(paramName, "Lua values can't be null; use a string, bool or number.");
            case bool _:
            case int _:
                return value;
            case string text:
                CheckText(text, paramName);
                return text;
            case float f:
                return Finite(f, paramName);
            case double d:
                return Finite(d, paramName);
            case byte _:
            case sbyte _:
            case short _:
            case ushort _:
            case uint _:
            case long _:
            case ulong _:
                return Convert.ToDouble(value);
            default:
                throw new ArgumentException($"A {value.GetType().Name} can't be stored in the Lua state; use a string, bool or number.", paramName);
        }
    }

    private static void CheckText(string text, string paramName)
    {
        foreach (char c in text)
        {
            // Newlines are escaped by the game's save writer, other control characters and backslashes are not.
            if (c == '\\' || (char.IsControl(c) && c != '\n'))
                throw new ArgumentException("The text has a backslash or control character, which the game's dialogue save can't store.", paramName);
        }
    }

    private static double Finite(double value, string paramName)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentException("The number must be finite.", paramName);
        return value;
    }
}
