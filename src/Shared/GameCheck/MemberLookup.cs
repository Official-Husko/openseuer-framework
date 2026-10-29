#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Oben.Checks;

public enum LookupStatus
{
    Found,
    Missing,
    Ambiguous,
}

public readonly record struct Lookup(LookupStatus Status, string Detail, MethodDefinition? Method = null)
{
    public static Lookup Found(MethodDefinition? method = null) => new(LookupStatus.Found, "", method);
}

public enum Accessor
{
    None,
    Get,
    Set,
}

/// <summary>
/// Finds members the way Harmony's AccessTools does: walking up the base types, or only the type
/// itself for the Declared variants. Overloads are told apart by the argument types when given.
/// </summary>
public static class MemberLookup
{
    public static IEnumerable<TypeDefinition> Hierarchy(TypeDefinition type)
    {
        for (TypeDefinition? t = type; t != null; t = BaseOf(t))
            yield return t;
    }

    private static TypeDefinition? BaseOf(TypeDefinition type)
    {
        try
        {
            return type.BaseType?.Resolve();
        }
        catch (AssemblyResolutionException)
        {
            return null;
        }
    }

    public static Lookup Method(TypeDefinition type, string name, IReadOnlyList<TypeReference>? args, bool declaredOnly)
    {
        string? others = null;
        foreach (var t in Hierarchy(type))
        {
            var named = t.Methods.Where(m => m.Name == name).ToList();
            var candidates = args == null ? named : named.Where(m => ParametersMatch(m, args)).ToList();
            if (candidates.Count > 0 && t != type && declaredOnly)
                return new Lookup(LookupStatus.Missing,
                    $"no method '{name}' declared on {type.FullName} (it is declared on {t.FullName}; patch that type instead)");
            if (candidates.Count == 1)
                return Lookup.Found(candidates[0]);
            if (candidates.Count > 1)
                return new Lookup(LookupStatus.Ambiguous,
                    $"{candidates.Count} overloads of '{name}' on {t.FullName}; name the argument types: {Signatures(candidates)}");
            if (named.Count > 0 && args != null && others == null)
                others = $"no overload of '{name}' on {t.FullName} takes ({string.Join(", ", args.Select(a => a.Name))}); it has: {Signatures(named)}";
        }
        return new Lookup(LookupStatus.Missing, others ?? $"no method '{name}' on {type.FullName}");
    }

    public static Lookup Field(TypeDefinition type, string name, bool declaredOnly)
    {
        foreach (var t in Hierarchy(type))
        {
            if (t.Fields.Any(f => f.Name == name))
                return Lookup.Found();
            if (declaredOnly)
                break;
        }
        return new Lookup(LookupStatus.Missing, $"no field '{name}' on {type.FullName}");
    }

    public static Lookup Property(TypeDefinition type, string name, bool declaredOnly, Accessor accessor)
    {
        foreach (var t in Hierarchy(type))
        {
            var property = t.Properties.FirstOrDefault(p => p.Name == name);
            if (property != null)
            {
                bool ok = accessor switch
                {
                    Accessor.Get => property.GetMethod != null,
                    Accessor.Set => property.SetMethod != null,
                    _ => true,
                };
                return ok
                    ? Lookup.Found(accessor == Accessor.Set ? property.SetMethod : property.GetMethod)
                    : new Lookup(LookupStatus.Missing, $"property '{name}' on {t.FullName} has no {accessor.ToString().ToLowerInvariant()}ter");
            }
            if (declaredOnly)
                break;
        }
        return new Lookup(LookupStatus.Missing, $"no property '{name}' on {type.FullName}");
    }

    private static bool ParametersMatch(MethodDefinition method, IReadOnlyList<TypeReference> args)
    {
        if (method.Parameters.Count != args.Count)
            return false;
        for (int i = 0; i < args.Count; i++)
        {
            if (Plain(method.Parameters[i].ParameterType) != Plain(args[i]))
                return false;
        }
        return true;
    }

    // ref and out parameters match the plain type, which is how Harmony's ArgumentType lists are used.
    private static string Plain(TypeReference type) => (type is ByReferenceType r ? r.ElementType : type).FullName;

    private static string Signatures(IEnumerable<MethodDefinition> methods) =>
        string.Join("; ", methods.Select(m => $"{m.Name}({string.Join(", ", m.Parameters.Select(p => p.ParameterType.Name))})"));
}
