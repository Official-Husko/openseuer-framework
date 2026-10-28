using System;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>The outcome of running a piece of Lua.</summary>
public readonly struct LuaResult
{
    internal LuaResult(DialogueValue value, string error)
    {
        Value = value;
        Error = error;
    }

    /// <summary>What the code returned (nothing, for code without a <c>return</c>).</summary>
    public DialogueValue Value { get; }

    /// <summary>The error message when the code failed to parse or threw, otherwise null.</summary>
    public string Error { get; }

    public bool Succeeded => Error == null;
}

/// <summary>
/// Runs Lua in the Dialogue System's environment, the same one dialogue conditions and scripts use.
/// The code is not held back during a save load (see <see cref="LuaState"/>); use
/// <see cref="LuaVariables"/> for plain writes.
/// </summary>
public static class LuaScript
{
    /// <summary>
    /// Runs the code and returns its result. Errors are returned in <see cref="LuaResult.Error"/> and not
    /// logged by the Dialogue System, so this is the call to use from tools. Use <c>return</c> to get a value:
    /// <c>Run("return Variable['Day'] + 1")</c>.
    /// </summary>
    public static LuaResult Run(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return new LuaResult(default, null);

        bool muted = Lua.muteExceptions;
        Lua.muteExceptions = true;
        try
        {
            return new LuaResult(new DialogueValue(Lua.Run(code, debug: false, allowExceptions: true)), null);
        }
        catch (Exception e)
        {
            return new LuaResult(default, e.Message);
        }
        finally
        {
            Lua.muteExceptions = muted;
        }
    }

    /// <summary>
    /// Evaluates a Lua condition such as <c>Variable['Day'] &gt; 3</c>. Empty text is true, like the
    /// Dialogue System's own conditions. False when it throws.
    /// </summary>
    public static bool IsTrue(string condition)
    {
        if (string.IsNullOrWhiteSpace(condition))
            return true;
        var result = Run("return " + condition);
        return result.Succeeded && result.Value.AsBool;
    }
}
