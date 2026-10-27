using System.Text.Json.Nodes;

namespace ObenTool.Game;

/// <summary>
/// Reads Heroic Games Launcher's settings: sideloaded games, and the Wine prefix and Proton build
/// each one runs with. That's how the game is set up on Linux here, so the tool launches it the
/// same way Heroic does.
/// </summary>
public static class Heroic
{
    public sealed record SideloadedApp(string AppName, string Title, string Executable);

    public sealed record WineSetup(string? Prefix, string? ProtonBin);

    private static string ConfigDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config", "heroic");

    /// <summary>Heroic's own copy of umu-launcher, which it uses to start Proton games.</summary>
    public static string? UmuRun()
    {
        string path = Path.Combine(ConfigDir, "tools", "runtimes", "umu", "umu-run");
        return File.Exists(path) ? path : null;
    }

    public static IEnumerable<SideloadedApp> SideloadedApps()
    {
        var library = ReadJson(Path.Combine(ConfigDir, "sideload_apps", "library.json"));
        if (library?["games"] is not JsonArray games)
            yield break;
        foreach (var game in games)
        {
            string? exe = game?["install"]?["executable"]?.GetValue<string>();
            string? name = game?["app_name"]?.GetValue<string>();
            if (exe != null && name != null)
                yield return new SideloadedApp(name, game!["title"]?.GetValue<string>() ?? name, exe);
        }
    }

    /// <summary>The prefix and Proton build for an app: its own settings, falling back to Heroic's defaults.</summary>
    public static WineSetup SetupFor(string appName)
    {
        var own = ReadJson(Path.Combine(ConfigDir, "GamesConfig", appName + ".json"))?[appName];
        var defaults = ReadJson(Path.Combine(ConfigDir, "config.json"))?["defaultSettings"];

        string? prefix = Str(own, "winePrefix") ?? Str(defaults, "winePrefix");
        string? proton = own?["wineVersion"]?["bin"]?.GetValue<string>()
                         ?? defaults?["wineVersion"]?["bin"]?.GetValue<string>();
        return new WineSetup(prefix, proton);
    }

    private static string? Str(JsonNode? node, string key) =>
        node?[key] is JsonValue v && v.TryGetValue(out string? s) && !string.IsNullOrEmpty(s) ? s : null;

    private static JsonNode? ReadJson(string path)
    {
        try
        {
            return File.Exists(path) ? JsonNode.Parse(File.ReadAllText(path)) : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }
}
