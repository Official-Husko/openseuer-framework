// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace DevBridge.Tracing;

/// <summary>
/// Picks the methods to trace: the public API of the given types. That is every public method of a public
/// type, the setters, and the getters of static classes (GameClock.Hour is an API call; QuestTask.Title is
/// just data). With non-public on, the internal types and the private and internal methods come too, as the
/// reference traced the framework's save and event plumbing. Left out: generic methods and types, data
/// snapshot types (names ending in Info or Args), compiler-made code (lambdas, iterators), events,
/// operators, ToString/Equals/GetHashCode, abstract and extern methods, by-ref returns, which Harmony can't
/// wrap, and the tracer itself. No Unity types, so it is unit tested on its own.
/// </summary>
public static class TraceTargets
{
    private const BindingFlags Declared = BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;
    private const BindingFlags AllDeclared = Declared | BindingFlags.NonPublic;
    private static readonly string[] DataSuffixes = { "Info", "Args" };
    private static readonly string[] ObjectMethods = { "ToString", "Equals", "GetHashCode", "GetType", "Finalize" };

    /// <summary>
    /// The traceable methods of <paramref name="types"/> whose type matches <paramref name="filter"/>; with
    /// <paramref name="nonPublic"/> the internal types and non-public methods too.
    /// </summary>
    public static List<MethodInfo> Select(IEnumerable<Type> types, string filter, bool nonPublic = false)
    {
        var found = new List<MethodInfo>();
        var parts = Filter(filter);
        foreach (var type in types)
        {
            if (type == null || !Eligible(type, nonPublic) || !Matches(type, parts))
                continue;
            bool staticClass = type.IsAbstract && type.IsSealed;
            foreach (var method in type.GetMethods(nonPublic ? AllDeclared : Declared))
            {
                if (Traceable(method, staticClass))
                    found.Add(method);
            }
        }
        return found;
    }

    /// <summary>
    /// The patch handlers (prefixes, postfixes and so on) among <paramref name="handlers"/> that one of the
    /// <paramref name="traced"/> assemblies declares and whose type matches <paramref name="filter"/>: the
    /// traced mods' hooks into the game, private ones included. The tracer's own hooks are never picked.
    /// </summary>
    public static List<MethodInfo> Handlers(IEnumerable<MethodInfo> handlers, ICollection<Assembly> traced, string filter)
    {
        var found = new List<MethodInfo>();
        var parts = Filter(filter);
        foreach (var handler in handlers)
        {
            var type = handler?.DeclaringType;
            if (type == null || !traced.Contains(type.Assembly) || type.Namespace == typeof(TraceTargets).Namespace || !Matches(type, parts))
                continue;
            if (handler.IsAbstract || handler.ContainsGenericParameters || handler.ReturnType.IsByRef)
                continue;
            found.Add(handler);
        }
        return found;
    }

    /// <summary>The types an assembly could load, skipping the ones that fail.</summary>
    public static Type[] TypesOf(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException e) { return e.Types; }
    }

    /// <summary>Comma-separated type name or namespace prefixes, empty for everything.</summary>
    public static string[] Filter(string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return new string[0];
        var parts = new List<string>();
        foreach (string part in filter.Split(','))
        {
            string trimmed = part.Trim();
            if (trimmed.Length > 0)
                parts.Add(trimmed);
        }
        return parts.ToArray();
    }

    /// <summary>
    /// A type matches when no filter is given, or when its name or its full name starts with one of the parts
    /// (so "Quest" picks QuestLog and QuestAuthoring, "ObenFramework.Housing" a whole namespace).
    /// </summary>
    public static bool Matches(Type type, string[] parts)
    {
        if (parts.Length == 0)
            return true;
        foreach (string part in parts)
        {
            if (type.Name.StartsWith(part, StringComparison.OrdinalIgnoreCase)
                || (type.FullName != null && type.FullName.StartsWith(part, StringComparison.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    private static bool Eligible(Type type, bool nonPublic)
    {
        if ((!nonPublic && !IsPublic(type)) || type.IsGenericType || type.IsInterface || type.IsEnum)
            return false;
        if (type.Namespace == typeof(TraceTargets).Namespace)
            return false;
        if (typeof(Delegate).IsAssignableFrom(type) || typeof(Attribute).IsAssignableFrom(type))
            return false;
        if (type.Name.IndexOf('<') >= 0 || type.IsDefined(typeof(CompilerGeneratedAttribute), false))
            return false;
        foreach (string suffix in DataSuffixes)
        {
            if (type.Name.EndsWith(suffix, StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    private static bool IsPublic(Type type) =>
        type.IsPublic || (type.IsNestedPublic && type.DeclaringType != null && IsPublic(type.DeclaringType));

    private static bool Traceable(MethodInfo method, bool staticClass)
    {
        if (method.IsAbstract || method.ContainsGenericParameters || method.ReturnType.IsByRef)
            return false;
        if ((method.Attributes & MethodAttributes.PinvokeImpl) != 0
            || (method.GetMethodImplementationFlags() & MethodImplAttributes.InternalCall) != 0)
            return false;
        string name = method.Name;
        if (name.IndexOf('<') >= 0 || Array.IndexOf(ObjectMethods, name) >= 0)
            return false;
        if (!method.IsSpecialName)
            return true;
        // Of the special names only property accessors are API calls: setters, and getters of static classes.
        return name.StartsWith("set_", StringComparison.Ordinal)
            || (staticClass && name.StartsWith("get_", StringComparison.Ordinal));
    }
}
