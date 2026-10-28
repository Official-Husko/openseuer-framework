using ObenSave.Files;
using ObenTool.Cli;
using ObenTool.Game;
using ObenTool.Saves;

namespace ObenTool.Commands;

public sealed class SaveCommand : ICommand
{
    private const string Details =
        "save list | info SLOT | export SLOT DIR | import DIR CHARACTER/SLOT | convert SLOT|--all --to plain|compressed";

    public string Name => "save";
    public string Usage => "save list | info | export | import | convert  (see 'oben save help')";
    public string Summary => "List, inspect and convert save slots between the game's files and the ObenSave container";

    public Task<int> RunAsync(Args args)
    {
        string action = args.At(0) ?? "list";
        return Task.FromResult(action switch
        {
            "list" => List(args),
            "info" => Info(args),
            "export" => Export(args),
            "import" => Import(args),
            "convert" => Convert(args),
            "help" => Help(),
            _ => throw new ArgumentException("Usage: oben " + Details),
        });
    }

    private static int Help()
    {
        Out.Info("""
            oben save list                                  characters and slots with format, size and level
            oben save info SLOT                             every file of a slot: format, sizes, CRC-32, header, object counts
            oben save export SLOT DIR [--format plain|compressed] [--pretty] [--force]
                                                            copy a slot into DIR (plain by default; --pretty indents the JSON)
            oben save import DIR CHARACTER/SLOT [--format compressed|plain] [--force]
                                                            write a folder of save files into a slot (compressed by default)
            oben save convert SLOT --to plain|compressed    change a slot's files in place
            oben save convert --all --to plain|compressed   the same for every slot

            SLOT is Character/Slot3, a bare Slot3 when only one character has it, or a path to a save folder.
            Put options after the arguments. --saves DIR names the Saves folder instead of finding the game's.
            Changing files refuses while the game runs, unless --force is given. Works without the mod installed.
            """);
        return 0;
    }

    private static int List(Args args)
    {
        var store = SaveStore.Open(args);
        Out.Info(store.Root);
        if (!Directory.Exists(store.Root))
        {
            Out.Warn("  No Saves folder yet. Play the game and save once.");
            return 0;
        }

        foreach (string character in store.Characters())
        {
            Out.Info(character);
            foreach (var slot in store.Slots(character))
            {
                var files = SaveFolders.Files(slot.Path);
                long disk = files.Sum(f => new FileInfo(f).Length);
                var info = SaveInspector.Inspect(Path.Combine(slot.Path, "Info.tnmt"));
                string level = info.LevelName ?? (info.Problem is null ? "" : "(unreadable)");
                string date = DateTime.TryParse(info.Date, out var when) ? when.ToString("yyyy-MM-dd HH:mm") : "";
                Out.Info($"  {slot.Name,-10} {SaveInspector.FormatOf(slot.Path),-11} {files.Count,2} files {SaveInspector.Size(disk),9}  {date,-16} {level}");
            }
        }
        return 0;
    }

    private static int Info(Args args)
    {
        var store = SaveStore.Open(args);
        var slot = store.Resolve(Require(args, 1, "info SLOT"));
        Out.Info($"Slot:      {slot.Character}/{slot.Name}");
        Out.Info($"Folder:    {slot.Path}");

        var reports = SaveFolders.Files(slot.Path).Select(SaveInspector.Inspect).ToList();
        var header = reports.FirstOrDefault(r => r.Name == "Info.tnmt" && r.Problem is null)
            ?? reports.FirstOrDefault(r => r.Name == "Globals.tnmt" && r.Problem is null);
        if (header is not null)
        {
            Out.Info($"Character: {header.CharacterName}");
            Out.Info($"Save name: {header.SaveName}");
            Out.Info($"Level:     {header.LevelName}");
            Out.Info($"Saved:     {header.Date}");
            Out.Info($"Game:      {header.ApplicationVersion}");
        }

        Out.Info("Files:");
        foreach (var report in reports)
        {
            if (report.Problem is not null)
            {
                Out.Error($"  {report.Name}: {report.Problem}");
                continue;
            }

            var f = report.Format!;
            string kind = f.IsContainer ? $"container v{f.Version} {f.Codec.ToString().ToLowerInvariant()}" : "plain";
            string crc = f.IsContainer ? $"  CRC-32 {f.Crc:X8}" : "";
            Out.Info($"  {report.Name,-34} {kind,-22} {SaveInspector.Size(f.FileLength),8} on disk, {SaveInspector.Size(f.ContentLength),8} of text{crc}");
            if (report.Objects >= 0)
            {
                string top = string.Join(", ", report.Types.Take(6).Select(t => $"{t.Key} x{t.Value}"));
                Out.Info($"      {report.Objects} objects{(top.Length > 0 ? ": " + top : "")}{(report.Types.Count > 6 ? ", ..." : "")}");
            }
        }
        return reports.Any(r => r.Problem is not null) ? 1 : 0;
    }

    private static int Export(Args args)
    {
        var store = SaveStore.Open(args);
        var slot = store.Resolve(Require(args, 1, "export SLOT DIR"));
        string dir = Path.GetFullPath(Require(args, 2, "export SLOT DIR"));
        var result = SaveTransfer.Export(slot, dir, Wants(args, "plain"), args.Has("pretty"), args.Has("force"));
        Out.Ok($"Wrote {result.Files} files to {dir} ({result.Converted} converted, {SaveInspector.Size(result.BytesIn)} -> {SaveInspector.Size(result.BytesOut)})");
        return 0;
    }

    private static int Import(Args args)
    {
        var store = SaveStore.Open(args);
        string source = Path.GetFullPath(Require(args, 1, "import DIR CHARACTER/SLOT"));
        var target = store.Target(Require(args, 2, "import DIR CHARACTER/SLOT"));
        RefuseWhileRunning(args);
        var result = SaveTransfer.Import(source, target, Wants(args, "compressed"), args.Has("force"));
        Out.Ok($"Imported {result.Files} files into {target.Character}/{target.Name} ({result.Converted} converted, {SaveInspector.Size(result.BytesIn)} -> {SaveInspector.Size(result.BytesOut)})");
        return 0;
    }

    private static int Convert(Args args)
    {
        var store = SaveStore.Open(args);
        bool compress = args.Get("to") switch
        {
            "compressed" => true,
            "plain" => false,
            _ => throw new ArgumentException("Say which way: --to plain or --to compressed."),
        };

        string folder;
        if (args.Has("all"))
            folder = store.Root;
        else
            folder = store.Resolve(Require(args, 1, "convert SLOT --to plain|compressed")).Path;

        RefuseWhileRunning(args);
        var summary = SaveFolders.Convert(folder, compress, (path, e) => Out.Error($"  {path}: {e.Message} (left as it was)"));
        Out.Info($"{(compress ? "Compressed" : "Restored")} {folder}: {summary}");
        return summary.Failed > 0 ? 1 : 0;
    }

    private static bool Wants(Args args, string fallback)
    {
        return args.Get("format", fallback) switch
        {
            "compressed" => true,
            "plain" => false,
            var other => throw new ArgumentException($"Unknown format '{other}'. Use plain or compressed."),
        };
    }

    private static void RefuseWhileRunning(Args args)
    {
        // With --saves the files aren't the running game's, so there is nothing to protect.
        if (!args.Has("force") && !args.Has("saves") && GameProcess.IsRunning())
            throw new InvalidOperationException("The game is running and may be writing saves. Close it first, or pass --force.");
    }

    private static string Require(Args args, int index, string usage) =>
        args.At(index) ?? throw new ArgumentException("Usage: oben save " + usage);
}
