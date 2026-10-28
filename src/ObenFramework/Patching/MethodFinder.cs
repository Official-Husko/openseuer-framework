using System;
using System.Reflection;

namespace ObenFramework.Patching;

/// <summary>
/// Finds the method to patch from a type, a name and (optionally) the argument types. Without argument
/// types the name must be unique on the type, so an overload added in a game update is noticed instead
/// of patching the wrong one. ".ctor" and ".cctor" find constructors; property accessors are "get_Name"
/// and "set_Name". Only the type's own members are searched, not its base types'.
/// </summary>
public static class MethodFinder
{
    private const BindingFlags Any = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

    /// <summary>The method, or null with <paramref name="problem"/> saying why.</summary>
    public static MethodBase Find(Type type, string name, Type[] args, out string problem)
    {
        problem = null;
        if (type == null)
        {
            problem = "no type given";
            return null;
        }
        if (string.IsNullOrEmpty(name))
        {
            problem = "no method name given";
            return null;
        }

        if (name == ".cctor")
        {
            var init = type.TypeInitializer;
            if (init == null)
                problem = $"{type.FullName} has no static constructor";
            return init;
        }

        MethodBase found = null;
        bool constructor = name == ".ctor";
        var candidates = constructor ? (MethodBase[])type.GetConstructors(Any) : type.GetMethods(Any);
        foreach (var candidate in candidates)
        {
            if (!constructor && candidate.Name != name)
                continue;
            if (candidate.IsStatic && constructor)
                continue;
            if (args != null && !SameParameters(candidate, args))
                continue;
            if (found != null)
            {
                problem = $"{Describe(type, name, args)} has overloads; pass the argument types";
                return null;
            }
            found = candidate;
        }
        if (found == null)
            problem = $"{Describe(type, name, args)} doesn't exist";
        return found;
    }

    /// <summary>The method, or null when it is missing or ambiguous.</summary>
    public static MethodBase Find(Type type, string name, params Type[] args) =>
        Find(type, name, args == null || args.Length == 0 ? null : args, out _);

    /// <summary>A static method on <paramref name="type"/> to use as a patch handler. Throws when it is missing.</summary>
    public static MethodInfo Handler(Type type, string name)
    {
        if (type == null)
            throw new ArgumentNullException(nameof(type));
        var method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (method == null)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }

    /// <summary>"Type.Method(int, string)" for messages.</summary>
    public static string Describe(Type type, string name, Type[] args)
    {
        string head = (type == null ? "?" : type.FullName) + "." + name;
        if (args == null)
            return head;
        var names = new string[args.Length];
        for (int i = 0; i < args.Length; i++)
            names[i] = args[i] == null ? "?" : args[i].Name;
        return head + "(" + string.Join(", ", names) + ")";
    }

    private static bool SameParameters(MethodBase method, Type[] args)
    {
        var parameters = method.GetParameters();
        if (parameters.Length != args.Length)
            return false;
        for (int i = 0; i < args.Length; i++)
        {
            if (parameters[i].ParameterType != args[i])
                return false;
        }
        return true;
    }
}
