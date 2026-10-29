#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace Oben.Checks;

/// <summary>
/// Reads the [HarmonyPatch] attributes in a mod (category classes included) and checks that each target
/// type and method still exists in the game, then that the patch methods' parameters still fit.
/// </summary>
public static class PatchScanner
{
    private const string PatchAttribute = "HarmonyLib.HarmonyPatch";
    private const string CategoryAttribute = "HarmonyLib.HarmonyPatchCategory";

    // Harmony works out what a patch method is from one of these attributes, or else from its name.
    private static readonly string[] KindNames = { "Prefix", "Postfix", "Finalizer", "Transpiler", "ReversePatch" };

    public static void Scan(ModuleDefinition module, GameAssemblies game, ModReport report)
    {
        foreach (var type in module.GetTypes())
        {
            var classSpecs = Attributes(type).ToList();
            var patches = type.Methods.Where(m => Kind(m) != null).ToList();
            if (classSpecs.Count == 0 && !patches.Any(m => Attributes(m).Any()))
                continue;

            string cls = type.FullName.Replace('/', '+') + CategoryText(type);
            if (type.Methods.Any(m => m.Name is "TargetMethod" or "TargetMethods" || HasAttribute(m, "HarmonyLib.HarmonyTargetMethod")
                    || HasAttribute(m, "HarmonyLib.HarmonyTargetMethods")))
            {
                report.Dynamic++;
                continue;
            }
            ScanClass(game, report, cls, PatchSpec.Layer(new PatchSpec(), classSpecs), patches);
        }
    }

    private static void ScanClass(GameAssemblies game, ModReport report, string cls, List<PatchSpec> classLevel, List<MethodDefinition> patches)
    {
        var checkedTargets = new Dictionary<string, (TypeDefinition?, MethodDefinition?)>();
        var perMethod = patches.Count == 0 ? new List<MethodDefinition?> { null } : patches.Cast<MethodDefinition?>().ToList();
        foreach (var patch in perMethod)
        {
            var specs = classLevel.SelectMany(c => patch == null
                ? new List<PatchSpec> { c }
                : PatchSpec.Layer(c, Attributes(patch))).ToList();
            foreach (var spec in specs)
            {
                string hook = "[HarmonyPatch] " + spec.Describe();
                if (!checkedTargets.TryGetValue(hook, out var target))
                    checkedTargets[hook] = target = Verify(game, report, cls, hook, spec);
                if (patch != null && target.Item1 != null && Kind(patch) is not ("Transpiler" or "ReversePatch"))
                    PatchParameters.Check(report, cls, hook, patch, target.Item1, target.Item2);
            }
        }
    }

    private static (TypeDefinition?, MethodDefinition?) Verify(GameAssemblies game, ModReport report, string cls, string hook, PatchSpec spec)
    {
        if (!spec.HasType)
        {
            report.Add(Severity.Missing, cls, hook, "the patch names no target type");
            return (null, null);
        }
        var type = spec.Type != null
            ? TypeChecks.ResolveOrReport(game, report, spec.Type, cls, hook)
            : TypeChecks.FindOrReport(game, report, spec.TypeName!, cls, hook);
        if (type == null)
            return (null, null);

        var lookup = Find(type, spec);
        report.Record(lookup, cls, hook);
        return (type, lookup.Status == LookupStatus.Found ? lookup.Method : null);
    }

    private static Lookup Find(TypeDefinition type, PatchSpec spec)
    {
        switch (spec.MethodType)
        {
            case PatchSpec.Constructor:
                return MemberLookup.Method(type, ".ctor", spec.Args, declaredOnly: true);
            case PatchSpec.StaticConstructor:
                return MemberLookup.Method(type, ".cctor", spec.Args, declaredOnly: true);
            case PatchSpec.Getter:
            case PatchSpec.Setter:
                return spec.Method == null
                    ? new Lookup(LookupStatus.Missing, "the patch names no property")
                    : MemberLookup.Property(type, spec.Method, true, spec.MethodType == PatchSpec.Getter ? Accessor.Get : Accessor.Set);
            default:
                return spec.Method == null
                    ? new Lookup(LookupStatus.Missing, "the patch names no target method")
                    : MemberLookup.Method(type, spec.Method, spec.Args, declaredOnly: true);
        }
    }

    private static IEnumerable<PatchSpec> Attributes(ICustomAttributeProvider provider) =>
        provider.CustomAttributes.Where(a => a.AttributeType.FullName == PatchAttribute).Select(PatchSpec.From);

    private static bool HasAttribute(ICustomAttributeProvider provider, string fullName) =>
        provider.CustomAttributes.Any(a => a.AttributeType.FullName == fullName);

    private static string? Kind(MethodDefinition method)
    {
        foreach (string kind in KindNames)
        {
            if (method.Name == kind || HasAttribute(method, "HarmonyLib.Harmony" + kind))
                return kind;
        }
        return null;
    }

    private static string CategoryText(TypeDefinition type)
    {
        var category = type.CustomAttributes.FirstOrDefault(a => a.AttributeType.FullName == CategoryAttribute);
        return category?.ConstructorArguments.Count > 0 && category.ConstructorArguments[0].Value is string name
            ? $" [category {name}]"
            : "";
    }
}
