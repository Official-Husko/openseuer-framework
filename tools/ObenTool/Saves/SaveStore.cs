using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Saves;

/// <summary>One save folder: a character's Autosave, Autosave2 or SlotN.</summary>
public sealed record SaveSlot(string Character, string Name, string Path);

/// <summary>The game's Saves folder and the slots in it.</summary>
public sealed class SaveStore
{
    public string Root { get; }

    private SaveStore(string root) => Root = root;

    /// <summary>Uses --saves DIR when given, otherwise the Saves folder in the game's data folder.</summary>
    public static SaveStore Open(Args args)
    {
        string? explicitDir = args.Get("saves");
        if (explicitDir is not null)
            return new SaveStore(Path.GetFullPath(explicitDir));

        string? data = GameInstall.Locate(args.Get("game")).PersistentDataDir();
        if (data is null)
            throw new InvalidOperationException("Couldn't find the game's data folder. Pass --saves <folder> to name the Saves folder.");
        return new SaveStore(Path.Combine(data, "Saves"));
    }

    /// <summary>Character folders, by name. Empty when nothing has been saved yet.</summary>
    public IEnumerable<string> Characters() =>
        Directory.Exists(Root)
            ? Directory.GetDirectories(Root).Select(d => System.IO.Path.GetFileName(d)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            : Enumerable.Empty<string>();

    /// <summary>The slots of one character that hold a save (an Info or Globals file), autosaves first.</summary>
    public IEnumerable<SaveSlot> Slots(string character)
    {
        string dir = System.IO.Path.Combine(Root, character);
        if (!Directory.Exists(dir))
            yield break;

        var folders = Directory.GetDirectories(dir)
            .Select(System.IO.Path.GetFileName)
            .Where(n => n is not null)
            .Select(n => n!)
            .OrderBy(SlotOrder)
            .ThenBy(n => n, StringComparer.OrdinalIgnoreCase);
        foreach (string name in folders)
        {
            string path = System.IO.Path.Combine(dir, name);
            if (HasSave(path))
                yield return new SaveSlot(character, name, path);
        }
    }

    public IEnumerable<SaveSlot> AllSlots() => Characters().SelectMany(Slots);

    /// <summary>
    /// Finds a slot from what the user typed: "Character/Slot3" or "Slot3" (when only one character has it)
    /// under the Saves folder, or the path of a save folder.
    /// </summary>
    public SaveSlot Resolve(string spec)
    {
        string[] parts = spec.Replace('\\', '/').Trim('/').Split('/');
        if (parts.Length == 2)
        {
            string path = System.IO.Path.Combine(Root, parts[0], parts[1]);
            if (HasSave(path))
                return new SaveSlot(parts[0], parts[1], path);
        }

        var byName = AllSlots().Where(s => string.Equals(s.Name, spec, StringComparison.OrdinalIgnoreCase)).ToList();
        if (byName.Count == 1)
            return byName[0];
        if (byName.Count > 1)
        {
            throw new ArgumentException($"More than one character has '{spec}'. Say which: "
                + string.Join(", ", byName.Select(s => $"{s.Character}/{s.Name}")));
        }

        if (HasSave(spec))
        {
            string full = System.IO.Path.GetFullPath(spec);
            return new SaveSlot(System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(full)) ?? "", System.IO.Path.GetFileName(full), full);
        }
        throw new ArgumentException($"No save slot '{spec}'. 'oben save list' shows the slots.");
    }

    /// <summary>The slot an import writes to: always "Character/Slot", and it need not exist yet.</summary>
    public SaveSlot Target(string spec)
    {
        string[] parts = spec.Replace('\\', '/').Trim('/').Split('/');
        if (parts.Length != 2 || parts.Any(p => p.Length == 0 || p is "." or ".."))
            throw new ArgumentException("Name the slot as Character/Slot, for example Esko_Virtanen/Slot3.");
        return new SaveSlot(parts[0], parts[1], System.IO.Path.Combine(Root, parts[0], parts[1]));
    }

    /// <summary>True for a folder that has the game's Info or Globals file in either format.</summary>
    public static bool HasSave(string folder) =>
        Directory.Exists(folder)
        && (File.Exists(System.IO.Path.Combine(folder, "Globals.tnmt")) || File.Exists(System.IO.Path.Combine(folder, "Info.tnmt")));

    private static int SlotOrder(string name)
    {
        if (name.StartsWith("Autosave", StringComparison.OrdinalIgnoreCase))
            return 0;
        return name.StartsWith("Slot", StringComparison.OrdinalIgnoreCase) && int.TryParse(name[4..], out int n) ? n : 100;
    }
}
