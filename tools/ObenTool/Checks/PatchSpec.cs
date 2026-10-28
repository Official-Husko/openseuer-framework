using Mono.Cecil;

namespace ObenTool.Checks;

/// <summary>
/// What one [HarmonyPatch(...)] attribute says about the target. Attributes on a class and on a patch
/// method are layered the way Harmony does it: the method's values win over the class's.
/// </summary>
public sealed record PatchSpec
{
    public const int Getter = 1, Setter = 2, Constructor = 3, StaticConstructor = 4;

    public TypeReference? Type { get; init; }
    public string? TypeName { get; init; }
    public string? Method { get; init; }
    public List<TypeReference>? Args { get; init; }
    public int MethodType { get; init; }

    public bool HasType => Type != null || TypeName != null;

    /// <summary>Names a type and a method on its own, so it can stand as a target by itself.</summary>
    public bool IsComplete => HasType && (Method != null || MethodType is Constructor or StaticConstructor);

    public static PatchSpec From(CustomAttribute attribute)
    {
        var spec = new PatchSpec();
        var parameters = attribute.Constructor.Parameters;
        bool twoStrings = parameters.Count(p => p.ParameterType.FullName == "System.String") == 2;
        bool firstString = true;
        for (int i = 0; i < parameters.Count; i++)
        {
            object? value = attribute.ConstructorArguments[i].Value;
            switch (parameters[i].ParameterType.FullName)
            {
                case "System.Type":
                    spec = spec with { Type = value as TypeReference };
                    break;
                case "System.String":
                    // (string typeName, string methodName) or just (string methodName).
                    spec = twoStrings && firstString ? spec with { TypeName = value as string } : spec with { Method = value as string };
                    firstString = false;
                    break;
                case "System.Type[]":
                    if (value is CustomAttributeArgument[] items)
                        spec = spec with { Args = items.Select(a => (TypeReference)a.Value).ToList() };
                    break;
                case "HarmonyLib.MethodType":
                    spec = spec with { MethodType = Convert.ToInt32(value) };
                    break;
            }
        }
        return spec;
    }

    /// <summary>This spec with everything <paramref name="over"/> sets laid on top.</summary>
    public PatchSpec Over(PatchSpec over) => new()
    {
        Type = over.Type ?? Type,
        TypeName = over.HasType ? over.TypeName : TypeName,
        Method = over.Method ?? Method,
        Args = over.Args ?? Args,
        MethodType = over.MethodType != 0 ? over.MethodType : MethodType,
    };

    /// <summary>Layers attributes on top of each other: partial ones merge, ones that name a whole target stay separate.</summary>
    public static List<PatchSpec> Layer(PatchSpec baseSpec, IEnumerable<PatchSpec> attributes)
    {
        var all = attributes.ToList();
        var merged = all.Where(a => !a.IsComplete).Aggregate(baseSpec, (acc, a) => acc.Over(a));
        var complete = all.Where(a => a.IsComplete).Select(a => merged.Over(a)).ToList();
        return complete.Count > 0 ? complete : new List<PatchSpec> { merged };
    }

    public string Describe()
    {
        string type = Type?.Name ?? TypeName ?? "?";
        string method = MethodType switch
        {
            Constructor => ".ctor",
            StaticConstructor => ".cctor",
            Getter => "get_" + Method,
            Setter => "set_" + Method,
            _ => Method ?? "?",
        };
        return $"{type}.{method}" + (Args != null ? $"({string.Join(", ", Args.Select(a => a.Name))})" : "");
    }
}
