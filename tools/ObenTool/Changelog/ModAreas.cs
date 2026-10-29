namespace ObenTool.Changelog;

/// <summary>
/// Which mod a commit scope belongs to, as players know it. This is the one place to add a line when a
/// new mod gets its own commit scope; a scope that isn't listed shows up capitalized as it is.
/// </summary>
public static class ModAreas
{
    private static readonly Dictionary<string, string> Names = new(StringComparer.OrdinalIgnoreCase)
    {
        ["loader"] = "Mod loader",
        ["framework"] = "Framework",
        // "kit" was the framework's scope before ObenKit became ObenFramework; every kit commit with a
        // changelog line came after that and is about the UI kit.
        ["kit"] = "UI kit",
        ["uikit"] = "UI kit",
        ["ui"] = "UI kit",
        ["bridge"] = "Dev bridge",
        ["scripts"] = "Script Runner",
        ["patches"] = "Unofficial ObenPatches",
        ["tool"] = "Command-line tool",
        ["mcp"] = "Command-line tool",
        ["coop"] = "ObenCoop",
        ["perf"] = "Performance Fixes",
        ["save"] = "ObenSave",
        ["cheats"] = "Cheat Menu",
        ["tweaks"] = "Game Tweaks",
    };

    /// <summary>The display name for a commit scope, or null when the commit has none.</summary>
    public static string? NameFor(string? scope)
    {
        if (string.IsNullOrEmpty(scope))
            return null;
        return Names.TryGetValue(scope, out string? name) ? name : char.ToUpperInvariant(scope[0]) + scope[1..];
    }
}
