using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using ObenTool.Game;

namespace ObenTool.Scripting;

/// <summary>
/// What a script compiles against: every managed DLL in the game's Managed folder (the game,
/// Unity, the loader, Harmony) and every mod DLL deployed into Mods/. Cached per game folder until a
/// DLL there changes, so the long-running MCP server sees mods deployed after its first script.
/// </summary>
public static class ScriptReferences
{
    private static readonly Dictionary<string, (string Stamp, List<MetadataReference> References)> Cache = new();

    public static List<MetadataReference> For(GameInstall game)
    {
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dll in Directory.GetFiles(game.ManagedDir, "*.dll"))
            byName.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
        if (Directory.Exists(game.ModsDir))
        {
            foreach (string dll in Directory.GetFiles(game.ModsDir, "*.dll", SearchOption.AllDirectories))
                byName.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
        }

        string stamp = Stamp(byName.Values);
        if (Cache.TryGetValue(game.Dir, out var cached) && cached.Stamp == stamp)
            return cached.References;

        var references = byName.Values.Where(IsManaged)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        Cache[game.Dir] = (stamp, references);
        return references;
    }

    // Which files there are and when each was written: a deploy changes it, a second script doesn't.
    private static string Stamp(IEnumerable<string> paths) =>
        string.Join("|", paths.OrderBy(p => p, StringComparer.Ordinal).Select(p => p + "@" + File.GetLastWriteTimeUtc(p).Ticks));

    private static bool IsManaged(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var pe = new PEReader(stream);
            return pe.HasMetadata;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }
}
