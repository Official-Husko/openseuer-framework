using Mono.Cecil;

namespace ObenTool.Checks;

/// <summary>Shared steps for the scanners: resolving a referenced type and naming where a hook is in the mod.</summary>
public static class TypeChecks
{
    /// <summary>The type's definition, or null after reporting why it can't be checked further.</summary>
    public static TypeDefinition? ResolveOrReport(GameAssemblies game, ModReport report, TypeReference type, string cls, string hook)
    {
        switch (game.Resolve(type, out var definition))
        {
            case TypeStatus.Found:
                return definition;
            case TypeStatus.Missing:
                report.Add(Severity.Missing, cls, hook, $"type {type.FullName} no longer exists in {type.Scope.Name}");
                return null;
            default:
                report.Add(Severity.Warning, cls, hook, $"couldn't load {type.Scope.Name}, so {type.FullName} was not checked");
                return null;
        }
    }

    /// <summary>The same for a type given by name only ("Namespace.Type"), searched in the game's code assemblies.</summary>
    public static TypeDefinition? FindOrReport(GameAssemblies game, ModReport report, string name, string cls, string hook)
    {
        var type = game.FindType(name);
        if (type == null)
            report.Add(Severity.Missing, cls, hook, $"type {name} no longer exists in the game's assemblies");
        return type;
    }

    /// <summary>The outermost class a method sits in (lambdas and iterators live in generated nested classes).</summary>
    public static string OuterClass(TypeDefinition type)
    {
        while (type.DeclaringType != null && type.Name.StartsWith('<'))
            type = type.DeclaringType;
        return type.FullName.Replace('/', '+');
    }

    /// <summary>A method name without the compiler's decoration for lambdas and local functions.</summary>
    public static string PlainName(MethodDefinition method)
    {
        string name = method.Name;
        if (name.StartsWith('<') && name.IndexOf('>') is > 1 and var close)
            return name[1..close];
        return name;
    }
}
