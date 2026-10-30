using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mono.Cecil;
using Oben.Reports;
using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

/// <summary>
/// The same bug report the game makes (MODS, Report), built from outside it: for when the game won't start, and for
/// programs that drive the tool (--json). The machine details come from the system.json the game wrote at its last
/// start; the game's hashes and BepInEx are read fresh from disk.
/// </summary>
public sealed class ReportCommand : ICommand
{
    private static readonly JsonSerializerOptions SnapshotJson = new() { IncludeFields = true, PropertyNameCaseInsensitive = true };

    public string Name => "report";
    public string Usage => "report [--cpu] [--gpu] [--ram] [--save] [--all] [--no-crash] [--show-personal] [--out DIR] [--json] [--instance N]";
    public string Summary => "Make a bug report zip (logs, settings, versions, system; optionally the PC and the newest save) without starting the game";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        int instance = args.Instance;
        bool all = args.Has("all");
        var options = new ReportOptions
        {
            Cpu = all || args.Has("cpu"),
            Gpu = all || args.Has("gpu"),
            Ram = all || args.Has("ram"),
            Save = all || args.Has("save"),
            Crash = !args.Has("no-crash"),
            HidePersonal = !args.Has("show-personal"),
        };
        DateTime made = DateTime.Now;

        var full = ReadSnapshot(game, out bool fromGame);
        full.GameCodeHash = Sha256(Path.Combine(game.ManagedDir, "Assembly-CSharp.dll")) ?? full.GameCodeHash;
        full.GameExeHash = Sha256(game.Exe) ?? full.GameExeHash;
        full.BepInEx = BepInExScan.Read(game.Dir);
        var system = full.For(options);

        string? persistent = game.PersistentDataDir();
        var files = new List<ReportEntry>();
        files.AddRange(ReportFiles.Logs(game.LogsDir, GameInstall.Suffix(instance), game.PlayerLogFile(instance)));
        files.AddRange(ReportFiles.Settings(game.LoaderDataDir, game.ModsDir));
        string? save = null;
        if (options.Save && persistent != null)
            files.AddRange(ReportFiles.Save(ReportFiles.NewestSave(Path.Combine(persistent, "Saves")), out save));
        string? crash = null;
        if (options.Crash && persistent != null)
            files.AddRange(ReportFiles.Crash(CrashesFolder(persistent), DateTime.UtcNow, out crash));

        string? errors = ReadShared(game.ErrorLogFile(instance));
        var problems = ReportAssembly.ProblemsFromErrorLog(errors, out int count);
        var entries = ReportAssembly.Entries(system, options, problems, count, files, save, made,
            s => JsonSerializer.Serialize(s, new JsonSerializerOptions(SnapshotJson) { WriteIndented = true }), out string summary, crash);

        var redactor = ReportAssembly.Redactor(options, Environment.UserName);
        string folder = args.Get("out") ?? Path.Combine(game.LoaderDataDir, "reports");
        var outcome = ReportPackager.Write(Path.Combine(folder, ReportAssembly.FileName(made)), entries, redactor);
        string issue = IssueLink.Bug(system);

        if (args.Has("json"))
        {
            var notes = new JsonArray();
            foreach (string note in outcome.Notes)
                notes.Add(note);
            Console.WriteLine(JsonOut.Format(new JsonObject
            {
                ["path"] = outcome.Path,
                ["bytes"] = outcome.Bytes,
                ["tooBigForGitHub"] = outcome.TooBigForGitHub,
                ["issueUrl"] = issue,
                ["save"] = save,
                ["crash"] = crash,
                ["systemFromGame"] = fromGame,
                ["notes"] = notes,
                ["summary"] = redactor == null ? summary : redactor.Apply(summary),
            }));
            return Task.FromResult(0);
        }

        Out.Ok($"Made {outcome.Path} ({Math.Max(1, outcome.Bytes / 1024)} KB, {outcome.Files.Count} files)");
        if (!fromGame)
            Out.Info("The game hasn't written ObenLoader/system.json yet, so the report has less about the PC. Starting the game once fixes that.");
        if (options.Save)
            Out.Info(save == null ? "No save found to add." : "With the save " + save + ".");
        if (crash != null)
            Out.Info("With the last crash, " + crash + ".");
        foreach (string note in outcome.Notes)
            Out.Info(note);
        if (outcome.TooBigForGitHub)
            Out.Error("It's over GitHub's 25 MB limit for attachments; leave the save out or share it another way.");
        Out.Info("Report it here, with the versions filled in: " + issue);
        return Task.FromResult(0);
    }

    // Unity's crash folders sit in the same user's Temp folder as Player.log's LocalLow one:
    // AppData/LocalLow/<company>/<product> and AppData/Local/Temp/<company>/<product>/Crashes.
    private static string CrashesFolder(string persistent) =>
        Path.GetFullPath(Path.Combine(persistent, "..", "..", "..", "Local", "Temp", "Loiste Interactive", "Obenseuer", "Crashes"));

    // The game's own snapshot when there is one, else what can be told from the files on disk.
    private static SystemSnapshot ReadSnapshot(GameInstall game, out bool fromGame)
    {
        string? text = ReadShared(Path.Combine(game.LoaderDataDir, "system.json"));
        var snapshot = text == null ? null : JsonSerializer.Deserialize<SystemSnapshot>(text, SnapshotJson);
        fromGame = snapshot != null;
        if (snapshot != null)
            return snapshot;

        snapshot = new SystemSnapshot
        {
            Written = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss"),
            Build = Stamp(Path.Combine(game.ModsDir, "ObenUIKit", "ObenUIKit.dll")),
            Os = RuntimeInformation.OSDescription,
            CpuCores = Environment.ProcessorCount,
        };
        foreach (string manifest in Directory.Exists(game.ModsDir) ? Directory.GetFiles(game.ModsDir, "mod.json", SearchOption.AllDirectories) : Array.Empty<string>())
        {
            var mod = JsonNode.Parse(File.ReadAllText(manifest));
            snapshot.Mods.Add(new ModEntry { Id = mod?["id"]?.GetValue<string>(), Name = mod?["name"]?.GetValue<string>(), Version = mod?["version"]?.GetValue<string>() });
        }
        return snapshot;
    }

    private static string? Stamp(string assembly)
    {
        if (!File.Exists(assembly))
            return null;
        using var definition = AssemblyDefinition.ReadAssembly(assembly);
        var stamp = definition.CustomAttributes.FirstOrDefault(a => a.AttributeType.Name == "AssemblyInformationalVersionAttribute");
        return stamp?.ConstructorArguments[0].Value is string text ? BuildStamp.Format(text) : definition.Name.Version.ToString(3);
    }

    private static string? Sha256(string path)
    {
        if (!File.Exists(path))
            return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    // The game keeps its files open while it runs.
    private static string? ReadShared(string path)
    {
        if (!File.Exists(path))
            return null;
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
