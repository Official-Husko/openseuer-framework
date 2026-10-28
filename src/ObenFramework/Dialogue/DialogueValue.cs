using System;
using Language.Lua;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// A value read from the Dialogue System's Lua environment. A missing variable, a missing field and a
/// Lua <c>nil</c> all read as <see cref="Exists"/> false. Lua only has one number type, so numbers come
/// back as doubles.
/// </summary>
public readonly struct DialogueValue
{
    private readonly LuaValue _value;

    internal DialogueValue(LuaValue value)
    {
        _value = value is LuaNil ? null : value;
    }

    internal DialogueValue(Lua.Result result) : this(result.luaValue)
    {
    }

    /// <summary>True when there is a value that is not nil.</summary>
    public bool Exists => _value != null;

    /// <summary>Lua's name for the type: "nil", "number", "string", "boolean", "table" or "function".</summary>
    public string Type => _value == null ? "nil" : _value.GetTypeCode();

    public bool IsNumber => _value is LuaNumber;

    public bool IsString => _value is LuaString;

    public bool IsBool => _value is LuaBoolean;

    public bool IsTable => _value is LuaTable;

    /// <summary>The value as text. Tables and functions only give a placeholder; nil gives null.</summary>
    public string AsString => _value?.ToString();

    public double AsNumber => _value is LuaNumber n ? n.Number : 0d;

    public int AsInt => (int)Math.Round(AsNumber);

    /// <summary>True for a Lua true, or for the text "true" in any case.</summary>
    public bool AsBool => _value is LuaBoolean b
        ? b.BoolValue
        : _value is LuaString s && string.Equals(s.Text, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>The text when the value is a string, otherwise <paramref name="fallback"/>.</summary>
    public string StringOr(string fallback) => _value is LuaString s ? s.Text : fallback;

    /// <summary>The number when the value is a number, otherwise <paramref name="fallback"/>.</summary>
    public double NumberOr(double fallback) => _value is LuaNumber n ? n.Number : fallback;

    public int IntOr(int fallback) => _value is LuaNumber ? AsInt : fallback;

    public bool BoolOr(bool fallback) => _value is LuaBoolean b ? b.BoolValue : fallback;

    /// <summary>A boxed bool, double or string, or null for nil, tables and functions.</summary>
    public object ToObject()
    {
        switch (_value)
        {
            case LuaBoolean b: return b.BoolValue;
            case LuaNumber n: return n.Number;
            case LuaString s: return s.Text;
            default: return null;
        }
    }

    public override string ToString() => _value == null ? "nil" : _value.ToString();
}
