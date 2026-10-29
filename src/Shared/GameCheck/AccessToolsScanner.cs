#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Oben.Checks;

/// <summary>
/// Finds calls such as AccessTools.Method(typeof(X), "Name") in a mod's IL and checks the member is
/// still on the game's type. Calls whose type or name isn't a constant are counted as dynamic.
/// </summary>
public static class AccessToolsScanner
{
    private enum Kind
    {
        Method,
        Field,
        Property,
    }

    private readonly record struct Call(Kind Kind, bool DeclaredOnly, Accessor Accessor);

    private static readonly Dictionary<string, Call> Calls = new()
    {
        ["Method"] = new(Kind.Method, false, Accessor.None),
        ["DeclaredMethod"] = new(Kind.Method, true, Accessor.None),
        ["MethodDelegate"] = new(Kind.Method, false, Accessor.None),
        ["Field"] = new(Kind.Field, false, Accessor.None),
        ["DeclaredField"] = new(Kind.Field, true, Accessor.None),
        ["FieldRefAccess"] = new(Kind.Field, false, Accessor.None),
        ["StaticFieldRefAccess"] = new(Kind.Field, false, Accessor.None),
        ["Property"] = new(Kind.Property, false, Accessor.None),
        ["DeclaredProperty"] = new(Kind.Property, true, Accessor.None),
        ["PropertyGetter"] = new(Kind.Property, false, Accessor.Get),
        ["DeclaredPropertyGetter"] = new(Kind.Property, true, Accessor.Get),
        ["PropertySetter"] = new(Kind.Property, false, Accessor.Set),
        ["DeclaredPropertySetter"] = new(Kind.Property, true, Accessor.Set),
    };

    public static void Scan(ModuleDefinition module, GameAssemblies game, ModReport report)
    {
        var seen = new HashSet<string>();
        foreach (var type in module.GetTypes())
        {
            foreach (var method in type.Methods.Where(m => m.HasBody))
            {
                var body = method.Body.Instructions;
                for (int i = 0; i < body.Count; i++)
                {
                    if (body[i].Operand is MethodReference call && call.DeclaringType.FullName == "HarmonyLib.AccessTools"
                        && Calls.TryGetValue(call.Name, out var info))
                    {
                        string cls = TypeChecks.OuterClass(type);
                        Check(game, report, cls, call, info, IlArgs.Slices(body, i), seen);
                    }
                }
            }
        }
    }

    private static void Check(GameAssemblies game, ModReport report, string cls, MethodReference call, Call info,
        Instruction[][]? args, HashSet<string> seen)
    {
        var parameters = call.Parameters.Select(p => p.ParameterType.FullName).ToList();
        int typeParam = parameters.IndexOf("System.Type");
        int nameParam = parameters.IndexOf("System.String");
        if (nameParam < 0)
            return;
        if (args == null)
        {
            report.Dynamic++;
            return;
        }

        string? name = IlArgs.AsString(args[nameParam]);
        TypeReference? owner = null;
        string? ownerName = null;
        if (typeParam >= 0)
        {
            owner = IlArgs.AsType(args[typeParam]);
        }
        else if (name != null && name.IndexOf(':') >= 0)
        {
            // The single-string form: "Namespace.Type:Member".
            ownerName = name.Substring(0, name.IndexOf(':'));
            name = name.Substring(name.IndexOf(':') + 1);
        }
        else if (call is GenericInstanceMethod { GenericArguments.Count: 2 } generic && call.Name.EndsWith("FieldRefAccess"))
        {
            owner = generic.GenericArguments[0];
        }

        if (name == null || (owner == null && ownerName == null))
        {
            report.Dynamic++;
            return;
        }

        string ownerText = owner?.Name ?? ownerName!;
        var argTypes = ArgumentTypes(parameters, args);
        string hook = $"AccessTools.{call.Name}({ownerText}, \"{name}\"" + (argTypes != null ? $", [{string.Join(", ", argTypes.Select(t => t.Name))}]" : "") + ")";
        if (!seen.Add(cls + "|" + hook))
            return;

        var definition = owner != null
            ? TypeChecks.ResolveOrReport(game, report, owner, cls, hook)
            : TypeChecks.FindOrReport(game, report, ownerName!, cls, hook);
        if (definition == null)
            return;

        report.Record(Find(definition, name, info, argTypes), cls, hook);
    }

    // The Type[] argument of Method/MethodDelegate, when it is spelled out in the code.
    private static List<TypeReference>? ArgumentTypes(List<string> parameters, Instruction[][] args)
    {
        int array = parameters.IndexOf("System.Type[]");
        return array >= 0 ? IlArgs.AsTypeArray(args[array]) : null;
    }

    private static Lookup Find(TypeDefinition type, string name, Call info, List<TypeReference>? argTypes) => info.Kind switch
    {
        Kind.Method => MemberLookup.Method(type, name, argTypes, info.DeclaredOnly),
        Kind.Field => MemberLookup.Field(type, name, info.DeclaredOnly),
        _ => MemberLookup.Property(type, name, info.DeclaredOnly, info.Accessor),
    };
}
