using Mono.Cecil;

namespace ObenTool.Checks;

/// <summary>
/// Checks the parameters of a patch method against the method it patches: Harmony hands them over by
/// name, so a renamed parameter or field in the game breaks the patch even though the method is still there.
/// </summary>
public static class PatchParameters
{
    private static readonly HashSet<string> Special = new()
    {
        "__instance", "__result", "__resultRef", "__state", "__args", "__originalMethod", "__runOriginal", "__exception",
    };

    public static void Check(ModReport report, string cls, string hook, MethodDefinition patch, TypeDefinition target, MethodDefinition? original)
    {
        foreach (var parameter in patch.Parameters)
        {
            string name = parameter.Name;
            if (name.StartsWith("___"))
                CheckField(report, cls, hook, patch, target, name[3..], name);
            else if (name.StartsWith("__") && Special.Contains(name))
                continue;
            else if (original != null)
                CheckOriginalParameter(report, cls, hook, patch, original, name);
        }
    }

    private static void CheckField(ModReport report, string cls, string hook, MethodDefinition patch, TypeDefinition target,
        string field, string parameterName)
    {
        // ___0 style injection picks the field by index; nothing to look up by name.
        if (field.Length == 0 || field.All(char.IsDigit))
            return;
        var lookup = MemberLookup.Field(target, field, declaredOnly: false);
        if (lookup.Status != LookupStatus.Found)
            report.Add(Severity.Missing, cls, hook, $"{patch.Name}: parameter {parameterName} needs a field '{field}', but there is none on {target.FullName}");
    }

    private static void CheckOriginalParameter(ModReport report, string cls, string hook, MethodDefinition patch,
        MethodDefinition original, string name)
    {
        // __0, __1 ... pick an argument by position.
        if (name.StartsWith("__") && int.TryParse(name[2..], out int index))
        {
            if (index >= original.Parameters.Count)
                report.Add(Severity.Missing, cls, hook, $"{patch.Name}: parameter {name} is past the last argument of {original.Name} ({original.Parameters.Count})");
            return;
        }
        if (original.Parameters.Any(p => p.Name == name))
            return;
        string has = original.Parameters.Count == 0 ? "no parameters" : string.Join(", ", original.Parameters.Select(p => p.Name));
        report.Add(Severity.Missing, cls, hook, $"{patch.Name}: parameter '{name}' is not a parameter of {original.DeclaringType.Name}.{original.Name} (it has {has})");
    }
}
