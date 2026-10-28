using System;
using System.Collections.Generic;
using System.Reflection;
using Language.Lua;
using ObenLoader;
using ObenLoader.Runtime;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Lua functions a mod adds for dialogue conditions and scripts (<c>Mod_HasKey()</c> in a condition,
/// <c>Mod_Reward(5)</c> in a script). They are removed when the mod unloads. The game's own functions
/// (<c>TaskActive</c>, <c>AddMoneyDialogue</c> and so on, listed in docs/game/npcs-dialogue-quests.md)
/// live in the same table, so give yours a mod prefix.
/// </summary>
public static class LuaFunctions
{
    private sealed class Entry
    {
        public string Owner;
        public object Target;
        public MethodInfo Method;
    }

    private static readonly Dictionary<string, Entry> Registered = new Dictionary<string, Entry>(StringComparer.Ordinal);

    private static readonly HashSet<string> Keywords = new HashSet<string>(StringComparer.Ordinal)
    {
        "and", "break", "do", "else", "elseif", "end", "false", "for", "function", "if", "in",
        "local", "nil", "not", "or", "repeat", "return", "then", "true", "until", "while",
    };

    /// <summary>The names of the functions mods registered, with the id of the mod that owns each.</summary>
    public static IEnumerable<KeyValuePair<string, string>> ModFunctions
    {
        get
        {
            foreach (var pair in Registered)
                yield return new KeyValuePair<string, string>(pair.Key, pair.Value.Owner);
        }
    }

    /// <summary>True when any function (the game's or a mod's) has this name.</summary>
    public static bool Exists(string name) =>
        !string.IsNullOrEmpty(name) && !(Lua.Environment.GetValue(name) is LuaNil);

    /// <summary>
    /// Registers a function under <paramref name="name"/> (a Lua identifier). The Dialogue System passes
    /// numbers as floats and strings and bools as themselves, so the delegate's parameters must be
    /// <c>float</c> or <c>double</c>, <c>string</c> or <c>bool</c>; it may return nothing, one of those, or an int.
    /// Throws when the name is taken (the Dialogue System would silently ignore the new function) or the
    /// signature doesn't fit. An exception inside the function is logged by the Dialogue System and fails the Lua
    /// call that used it. The function is removed when <paramref name="owner"/> unloads.
    /// </summary>
    public static void Register(ObenMod owner, string name, Delegate handler)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (handler == null) throw new ArgumentNullException(nameof(handler));
        CheckName(name);
        CheckSignature(handler);

        if (Exists(name))
        {
            string who = Registered.TryGetValue(name, out var existing) ? $"mod '{existing.Owner}'" : "the game or another script";
            throw new InvalidOperationException($"The Lua function '{name}' already exists (from {who}).");
        }

        var entry = new Entry { Owner = owner.Info.Id, Target = handler.Target, Method = handler.Method };
        Lua.RegisterFunction(name, entry.Target, entry.Method);
        Registered[name] = entry;
        owner.Track(Disposable.Create(() => Remove(name, entry)));
    }

    // Only removes the function when the table still holds ours, in case something replaced it.
    private static void Remove(string name, Entry entry)
    {
        if (Registered.TryGetValue(name, out var current) && current == entry)
            Registered.Remove(name);
        if (Lua.Environment.GetValue(name) is LuaMethodFunction function
            && ReferenceEquals(function.Target, entry.Target) && function.Method == entry.Method)
            Lua.UnregisterFunction(name);
    }

    private static void CheckName(string name)
    {
        if (string.IsNullOrEmpty(name) || Keywords.Contains(name) || !IsIdentifier(name))
            throw new ArgumentException($"'{name}' is not a usable Lua function name; use letters, digits and underscores, not starting with a digit.", nameof(name));
    }

    private static bool IsIdentifier(string name)
    {
        if (!(char.IsLetter(name[0]) && name[0] < 128 || name[0] == '_'))
            return false;
        foreach (char c in name)
        {
            if (!(c < 128 && char.IsLetterOrDigit(c)) && c != '_')
                return false;
        }
        return true;
    }

    private static void CheckSignature(Delegate handler)
    {
        var method = handler.Method;
        if (method.IsStatic && handler.Target != null)
            throw new ArgumentException("Delegates bound to the first argument of a static method are not supported; pass a lambda or an instance method.", nameof(handler));

        foreach (var parameter in method.GetParameters())
        {
            var type = parameter.ParameterType;
            if (type != typeof(float) && type != typeof(double) && type != typeof(string) && type != typeof(bool))
                throw new ArgumentException($"Parameter '{parameter.Name}' is a {type.Name}; Lua functions take float, double, string or bool (numbers arrive as floats).", nameof(handler));
        }
    }
}
