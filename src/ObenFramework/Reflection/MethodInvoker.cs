using System;
using System.Linq;
using System.Reflection;
using ObenFramework.Values;
using Newtonsoft.Json.Linq;

namespace ObenFramework.Reflection;

/// <summary>Calls a method by name with JSON arguments, picking the overload whose parameters fit.</summary>
public static class MethodInvoker
{
    public static object Call(Type type, object target, string name, JArray args)
    {
        args ??= new JArray();
        var candidates = MemberAccess.FindMethods(type, name)
            .Where(m => m.IsStatic == (target == null))
            .Where(m => Fits(m, args.Count))
            .OrderBy(m => m.GetParameters().Length - args.Count) // Fewest defaulted parameters first.
            .ToList();

        Exception lastError = null;
        foreach (var method in candidates)
        {
            object[] converted;
            try
            {
                converted = Convert(method.GetParameters(), args);
            }
            catch (Exception e)
            {
                lastError = e;
                continue;
            }

            try
            {
                return method.Invoke(target, converted);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException ?? e;
            }
        }

        throw new ArgumentException(
            $"No overload of {type.Name}.{name} takes those {args.Count} argument(s)." +
            (lastError != null ? " " + lastError.Message : ""));
    }

    private static bool Fits(MethodInfo m, int argCount)
    {
        var ps = m.GetParameters();
        return argCount <= ps.Length && ps.Skip(argCount).All(p => p.IsOptional) && !m.IsGenericMethodDefinition;
    }

    private static object[] Convert(ParameterInfo[] ps, JArray args)
    {
        var values = new object[ps.Length];
        for (int i = 0; i < ps.Length; i++)
            values[i] = i < args.Count ? JsonValues.FromJson(args[i], ps[i].ParameterType) : ps[i].DefaultValue;
        return values;
    }
}
