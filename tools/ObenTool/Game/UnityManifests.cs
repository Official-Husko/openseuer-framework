using System.Text.Json;
using System.Text.Json.Nodes;

namespace ObenTool.Game;

/// <summary>
/// Edits the two JSON files Unity reads at startup so it loads ObenLoader.dll as one of the
/// game's own assemblies and calls ObenLoader.Entry.Init before the first scene. Originals are
/// backed up once.
/// </summary>
public sealed class UnityManifests
{
    public const string LoaderAssembly = "ObenLoader";
    private const int UserAssemblyType = 16;
    private const int BeforeSceneLoad = 1;
    private const string BackupSuffix = ".obenbak";

    private static readonly JsonSerializerOptions Compact = new() { WriteIndented = false };

    private readonly string _assemblies;
    private readonly string _initMethods;

    public UnityManifests(GameInstall game)
    {
        _assemblies = Path.Combine(game.DataDir, "ScriptingAssemblies.json");
        _initMethods = Path.Combine(game.DataDir, "RuntimeInitializeOnLoads.json");
    }

    public bool IsPatched => HasAssembly(Load(_assemblies)) && HasInitMethod(Load(_initMethods));

    public void Patch()
    {
        Backup(_assemblies);
        Backup(_initMethods);

        var assemblies = Load(_assemblies);
        if (!HasAssembly(assemblies))
        {
            assemblies["names"]!.AsArray().Add(LoaderAssembly + ".dll");
            assemblies["types"]!.AsArray().Add(UserAssemblyType);
            Save(_assemblies, assemblies);
        }

        var init = Load(_initMethods);
        if (!HasInitMethod(init))
        {
            init["root"]!.AsArray().Add(new JsonObject
            {
                ["assemblyName"] = LoaderAssembly,
                ["nameSpace"] = LoaderAssembly,
                ["className"] = "Entry",
                ["methodName"] = "Init",
                ["loadTypes"] = BeforeSceneLoad,
                ["isUnityClass"] = false,
            });
            Save(_initMethods, init);
        }
    }

    /// <summary>Restores the originals if we have them, otherwise strips our entries.</summary>
    public void Unpatch()
    {
        foreach (string file in new[] { _assemblies, _initMethods })
        {
            string backup = file + BackupSuffix;
            if (File.Exists(backup))
            {
                File.Copy(backup, file, overwrite: true);
                File.Delete(backup);
            }
        }

        var assemblies = Load(_assemblies);
        var names = assemblies["names"]!.AsArray();
        var types = assemblies["types"]!.AsArray();
        for (int i = names.Count - 1; i >= 0; i--)
        {
            if (names[i]!.GetValue<string>() == LoaderAssembly + ".dll")
            {
                names.RemoveAt(i);
                types.RemoveAt(i);
            }
        }
        Save(_assemblies, assemblies);

        var init = Load(_initMethods);
        var root = init["root"]!.AsArray();
        for (int i = root.Count - 1; i >= 0; i--)
        {
            if (root[i]!["assemblyName"]!.GetValue<string>() == LoaderAssembly)
                root.RemoveAt(i);
        }
        Save(_initMethods, init);
    }

    private static bool HasAssembly(JsonNode doc) =>
        doc["names"]!.AsArray().Any(n => n!.GetValue<string>() == LoaderAssembly + ".dll");

    private static bool HasInitMethod(JsonNode doc) =>
        doc["root"]!.AsArray().Any(n => n!["assemblyName"]!.GetValue<string>() == LoaderAssembly);

    private static JsonNode Load(string path) => JsonNode.Parse(File.ReadAllText(path))!;

    private static void Save(string path, JsonNode doc) => File.WriteAllText(path, doc.ToJsonString(Compact));

    private static void Backup(string path)
    {
        string backup = path + BackupSuffix;
        if (!File.Exists(backup))
            File.Copy(path, backup);
    }
}
