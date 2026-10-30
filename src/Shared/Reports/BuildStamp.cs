#nullable enable
using System;
using System.Reflection;

namespace Oben.Reports;

/// <summary>
/// The version and commit an assembly was built from, as players see it: "0.1.0 (a185d27)". build/Revision.targets
/// stamps "0.1.0+a185d27" into every assembly's informational version ("-dirty" after uncommitted changes).
/// </summary>
public static class BuildStamp
{
    /// <summary>The stamp of <paramref name="assembly"/>, or its plain version when it has none.</summary>
    public static string Of(Assembly assembly)
    {
        var stamp = (AssemblyInformationalVersionAttribute?)Attribute.GetCustomAttribute(assembly, typeof(AssemblyInformationalVersionAttribute));
        string? text = stamp?.InformationalVersion;
        return string.IsNullOrEmpty(text) ? assembly.GetName().Version?.ToString(3) ?? "?" : Format(text!);
    }

    /// <summary>"0.1.0+a185d27" as "0.1.0 (a185d27)"; a version without a commit stays as it is.</summary>
    public static string Format(string informational)
    {
        int plus = informational.IndexOf('+');
        return plus > 0 ? informational.Substring(0, plus) + " (" + informational.Substring(plus + 1) + ")" : informational;
    }
}
