using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using ObenTool.Game;

namespace ObenTool.Scripting;

/// <summary>
/// What a script compiles against: every managed DLL in the game's Managed folder (the game,
/// Unity, the loader, Harmony) and every mod DLL deployed into Mods/. Cached per game folder.
/// </summary>
public static class ScriptReferences
{
    private static readonly Dictionary<string, List<MetadataReference>> Cache = new();

    public static List<MetadataReference> For(GameInstall game)
    {
        if (Cache.TryGetValue(game.Dir, out var cached))
            return cached;

        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (string dll in Directory.GetFiles(game.ManagedDir, "*.dll"))
            byName.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
        if (Directory.Exists(game.ModsDir))
        {
            foreach (string dll in Directory.GetFiles(game.ModsDir, "*.dll", SearchOption.AllDirectories))
                byName.TryAdd(Path.GetFileNameWithoutExtension(dll), dll);
        }

        var references = byName.Values.Where(IsManaged)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        Cache[game.Dir] = references;
        return references;
    }

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
