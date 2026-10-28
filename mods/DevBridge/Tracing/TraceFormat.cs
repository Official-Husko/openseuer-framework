// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text;

namespace DevBridge.Tracing;

/// <summary>
/// Turns traced arguments and results into short text. The only code of other types it runs is a
/// ToString override (never traced), and the tracer ignores calls made while it formats, so formatting
/// can't add records of its own. Unity objects are described through <see cref="Describe"/>, which the
/// tracer sets. No Unity types here, so it is unit tested on its own.
/// </summary>
public static class TraceFormat
{
    public const int MaxText = 120;

    /// <summary>Describes values this class doesn't know (Unity objects, vectors), or returns null.</summary>
    public static Func<object, string> Describe;

    public static string Value(object value)
    {
        if (value == null)
            return "null";
        if (value is string text)
            return "\"" + Cut(text) + "\"";
        if (value is bool flag)
            return flag ? "true" : "false";
        if (value is float f)
            return f.ToString("0.###", CultureInfo.InvariantCulture);
        if (value is double d)
            return d.ToString("0.###", CultureInfo.InvariantCulture);
        if (value is Enum)
            return value.ToString();
        string described = Describe?.Invoke(value);
        if (described != null)
            return described;
        if (value is Type type)
            return "typeof(" + Name(type) + ")";
        if (value is Delegate handlers)
            return Name(handlers.GetType()) + "[" + handlers.GetInvocationList().Length + "]";
        if (value is Array array)
            return Name(array.GetType().GetElementType()) + "[" + array.Length + "]";
        if (value is ICollection collection)
            return Name(value.GetType()) + "[" + collection.Count + "]";
        if (value is IFormattable number && value.GetType().IsPrimitive)
            return number.ToString(null, CultureInfo.InvariantCulture);
        // Other objects: their type, unless they override ToString (records and small structs usually do).
        try
        {
            var valueType = value.GetType();
            var toString = valueType.GetMethod("ToString", Type.EmptyTypes);
            return toString != null && toString.DeclaringType != typeof(object) && toString.DeclaringType != typeof(ValueType)
                ? Cut(value.ToString() ?? "null")
                : Name(valueType);
        }
        catch (Exception e)
        {
            return "<ToString threw " + e.GetType().Name + ">";
        }
    }

    /// <summary>"Type.Method", without generic arity marks.</summary>
    public static string Method(MethodBase method) =>
        method.DeclaringType == null ? method.Name : Name(method.DeclaringType) + "." + Accessor(method.Name);

    /// <summary>The arguments as "a, b, out, ref c".</summary>
    public static string Args(ParameterInfo[] parameters, object[] values)
    {
        if (parameters.Length == 0)
            return "";
        var text = new StringBuilder();
        for (int i = 0; i < parameters.Length; i++)
        {
            if (i > 0)
                text.Append(", ");
            object value = values != null && i < values.Length ? values[i] : null;
            var parameter = parameters[i];
            if (!parameter.ParameterType.IsByRef)
                text.Append(Value(value));
            else if (parameter.IsOut)
                text.Append("out");
            else
                text.Append("ref ").Append(Value(value));
        }
        return text.ToString();
    }

    public static string Name(Type type)
    {
        string name = type.Name;
        int tick = name.IndexOf('`');
        return tick < 0 ? name : name.Substring(0, tick);
    }

    public static string Cut(string text) => text.Length > MaxText ? text.Substring(0, MaxText) + "..." : text;

    /// <summary>A mod assembly's name without the "-rN" hot reload suffix.</summary>
    public static string ModName(string assemblyName)
    {
        if (string.IsNullOrEmpty(assemblyName))
            return assemblyName;
        int dash = assemblyName.LastIndexOf("-r", StringComparison.Ordinal);
        if (dash <= 0 || dash + 2 >= assemblyName.Length)
            return assemblyName;
        for (int i = dash + 2; i < assemblyName.Length; i++)
        {
            if (!char.IsDigit(assemblyName[i]))
                return assemblyName;
        }
        return assemblyName.Substring(0, dash);
    }

    /// <summary>The first line of an exception's stack, for an error record.</summary>
    public static string FirstFrame(Exception error)
    {
        string trace = error.StackTrace;
        if (string.IsNullOrEmpty(trace))
            return null;
        string line = trace.Split('\n')[0].Trim();
        return line.StartsWith("at ", StringComparison.Ordinal) ? line.Substring(3) : line;
    }

    // "get_Hour" reads better as "Hour" and "set_TimeSpeed" as "TimeSpeed=".
    private static string Accessor(string name)
    {
        if (name.StartsWith("get_", StringComparison.Ordinal))
            return name.Substring(4);
        if (name.StartsWith("set_", StringComparison.Ordinal))
            return name.Substring(4) + "=";
        return name;
    }
}
