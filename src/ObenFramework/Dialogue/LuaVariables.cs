using System;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// The Dialogue System's global variables (the Lua <c>Variable</c> table): the game keeps story flags,
/// the day counter and the player's name there, and the whole table is part of every save.
/// Reads are safe at any time (a missing variable reads as nothing). Writes made while a save is
/// loading are held back and applied right after it, see <see cref="LuaWriteResult"/>.
/// </summary>
public static class LuaVariables
{
    public static bool Exists(string name) =>
        !string.IsNullOrEmpty(name) && LuaState.IsReady && DialogueLua.DoesVariableExist(name);

    /// <summary>The variable's value, or a value whose <see cref="DialogueValue.Exists"/> is false.</summary>
    public static DialogueValue Get(string name)
    {
        if (string.IsNullOrEmpty(name) || !LuaState.IsReady)
            return default;
        return new DialogueValue(DialogueLua.GetVariable(name));
    }

    /// <summary>The text, or <paramref name="fallback"/> when the variable is missing or isn't a string.</summary>
    public static string GetString(string name, string fallback = null) => Get(name).StringOr(fallback);

    public static int GetInt(string name, int fallback = 0) => Get(name).IntOr(fallback);

    public static float GetFloat(string name, float fallback = 0f) => (float)Get(name).NumberOr(fallback);

    public static bool GetBool(string name, bool fallback = false) => Get(name).BoolOr(fallback);

    /// <summary>Every variable name that exists right now.</summary>
    public static string[] Names() => LuaState.IsReady ? DialogueLua.GetAllVariables() : Array.Empty<string>();

    /// <summary>
    /// True for the variables the game and the Dialogue System write themselves (the day, the player's name,
    /// the running conversation's actors, shop prices and a few more): <see cref="Set"/> and
    /// <see cref="SetDefault"/> refuse them, because the game would overwrite the value. Reading them is fine.
    /// </summary>
    public static bool IsGameVariable(string name) => LuaChecks.IsGameVariable(name);

    /// <summary>
    /// Sets a variable (created when missing). <paramref name="value"/> is a string, bool or number;
    /// numbers are saved as floats. Throws for values the game's save can't store (a backslash in a
    /// string, NaN) and for the game's own variables (<see cref="IsGameVariable"/>). Returns
    /// <see cref="LuaWriteResult.Deferred"/> while a save is loading.
    /// </summary>
    public static LuaWriteResult Set(string name, object value)
    {
        LuaChecks.VariableName(name, nameof(name));
        return LuaWrites.Variable(name, LuaChecks.Value(value, nameof(value)), onlyIfMissing: false);
    }

    /// <summary>
    /// Creates the variable with this value if it doesn't exist, and leaves an existing one alone.
    /// Call it from a <see cref="Events.GameEvents.WorldReady"/> handler: variables a mod made are only
    /// in saves that were written while the mod was installed, so after loading an older save they are gone.
    /// </summary>
    public static LuaWriteResult SetDefault(string name, object value)
    {
        LuaChecks.VariableName(name, nameof(name));
        return LuaWrites.Variable(name, LuaChecks.Value(value, nameof(value)), onlyIfMissing: true);
    }
}
