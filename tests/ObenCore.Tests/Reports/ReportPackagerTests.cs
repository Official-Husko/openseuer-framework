using System.IO.Compression;
using System.Text;
using Mono.Cecil;
using Oben.Reports;

namespace ObenCore.Tests.Reports;

public sealed class ReportPackagerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "oben-report-" + Guid.NewGuid().ToString("N"));

    public ReportPackagerTests() => Directory.CreateDirectory(_dir);

    public void Dispose() => Directory.Delete(_dir, true);

    private string File(string name, string text)
    {
        string path = Path.Combine(_dir, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        System.IO.File.WriteAllText(path, text);
        return path;
    }

    private static Dictionary<string, string> Read(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        return zip.Entries.ToDictionary(e => e.FullName, e => new StreamReader(e.Open(), Encoding.UTF8).ReadToEnd());
    }

    [Fact]
    public void TextIsRedactedEverywhereAndBinariesAreCopied()
    {
        string log = File("latest.log", "Signed in as kaarlo\n");
        string player = File("Player.log", @"Loading Z:\home\kaarlo\Games\Obenseuer" + "\n");
        string save = File("save/Info.tnmt", "kaarlo's save");
        string zip = Path.Combine(_dir, "out", "report.zip");

        var outcome = ReportPackager.Write(zip, new[]
        {
            new ReportEntry { Name = "report.md", Text = "# Report\n" },
            new ReportEntry { Name = "logs/latest.log", Source = log },
            new ReportEntry { Name = "logs/Player.log", Source = player },
            new ReportEntry { Name = "save/Info.tnmt", Source = save, IsText = false },
            new ReportEntry { Name = "logs/missing.log", Source = Path.Combine(_dir, "missing.log") },
        }, new ReportRedactor());

        var files = Read(zip);
        Assert.Equal(new[] { "report.md", "logs/latest.log", "logs/Player.log", "save/Info.tnmt" }, outcome.Files);
        Assert.Equal("Signed in as <user>\n", files["logs/latest.log"]);
        Assert.Contains(@"Z:\home\<user>\Games", files["logs/Player.log"]);
        Assert.Equal("kaarlo's save", files["save/Info.tnmt"]);
        Assert.False(System.IO.File.Exists(zip + ".part"));
        Assert.True(outcome.Bytes > 0);
        Assert.False(outcome.TooBigForGitHub);
    }

    [Fact]
    public void ALongLogKeepsItsEndFromAWholeLine()
    {
        var text = new StringBuilder();
        for (int i = 0; i < 2000; i++)
            text.Append("line ").Append(i).Append('\n');
        string log = File("latest.log", text.ToString());
        string zip = Path.Combine(_dir, "report.zip");

        var outcome = ReportPackager.Write(zip, new[] { new ReportEntry { Name = "latest.log", Source = log, MaxBytes = 1024 } }, null);

        string kept = Read(zip)["latest.log"];
        Assert.StartsWith("(The first ", kept);
        Assert.EndsWith("line 1999\n", kept);
        Assert.Matches(@"KB are left out\.\)\nline \d+\n", kept);
        Assert.Single(outcome.Notes);
    }

    [Fact]
    public void ALogTheGameHasOpenCanBeRead()
    {
        string log = File("latest.log", "still being written\n");
        using var writer = new FileStream(log, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
        string zip = Path.Combine(_dir, "report.zip");

        ReportPackager.Write(zip, new[] { new ReportEntry { Name = "latest.log", Source = log } }, null);

        Assert.Equal("still being written\n", Read(zip)["latest.log"]);
    }

    [Fact]
    public void BepInExPluginsAreFoundWithTheirAttribute()
    {
        string game = Path.Combine(_dir, "game");
        Directory.CreateDirectory(Path.Combine(game, "BepInEx", "core"));
        System.IO.File.WriteAllText(Path.Combine(game, "winhttp.dll"), "");
        System.IO.File.WriteAllText(Path.Combine(game, "doorstop_config.ini"), "");
        WritePlugin(Path.Combine(game, "BepInEx", "plugins", "Coop", "Coop.dll"), "nomads.coop", "Obenseuer Coop", "1.2.0");
        System.IO.File.WriteAllText(Path.Combine(game, "BepInEx", "plugins", "native.dll"), "not .NET");

        var info = BepInExScan.Read(game)!;

        Assert.Equal("winhttp.dll", info.Doorstop);
        Assert.Equal(2, info.Plugins.Count);
        var coop = info.Plugins.Single(p => p.File == "plugins/Coop/Coop.dll");
        Assert.Equal(("nomads.coop", "Obenseuer Coop", "1.2.0"), (coop.Guid, coop.Name, coop.Version));
        Assert.Equal(12, coop.Sha256!.Length);
        Assert.Null(info.Plugins.Single(p => p.File == "plugins/native.dll").Guid);
        Assert.Null(BepInExScan.Read(Path.Combine(_dir, "no-bepinex")));
    }

    // A tiny assembly with a class marked [BepInPlugin(guid, name, version)], like a real plugin.
    private static void WritePlugin(string path, string guid, string name, string version)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("Coop", new Version(1, 0, 0, 0)), "Coop", ModuleKind.Dll);
        var module = assembly.MainModule;
        var attribute = new TypeDefinition("BepInEx", "BepInPlugin", TypeAttributes.Public | TypeAttributes.Class, module.ImportReference(typeof(Attribute)));
        var ctor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
        for (int i = 0; i < 3; i++)
            ctor.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
        ctor.Body.GetILProcessor().Emit(Mono.Cecil.Cil.OpCodes.Ret);
        attribute.Methods.Add(ctor);
        module.Types.Add(attribute);
        var plugin = new TypeDefinition("Coop", "Plugin", TypeAttributes.Public | TypeAttributes.Class, module.TypeSystem.Object);
        var mark = new CustomAttribute(ctor);
        foreach (string value in new[] { guid, name, version })
            mark.ConstructorArguments.Add(new CustomAttributeArgument(module.TypeSystem.String, value));
        plugin.CustomAttributes.Add(mark);
        module.Types.Add(plugin);
        assembly.Write(path);
    }
}
