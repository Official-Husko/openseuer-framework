using Mono.Cecil;

namespace ObenTool.Checks;

/// <summary>Loads mod assemblies next to the game's and runs every scanner over them.</summary>
public static class CheckRunner
{
    public static List<ModReport> Run(GameAssemblies game, IEnumerable<string> dlls)
    {
        var paths = dlls.ToList();
        foreach (string dir in paths.Select(p => Path.GetDirectoryName(Path.GetFullPath(p))!).Distinct())
            game.AddSearchDirectory(dir);

        var reports = new List<ModReport>();
        foreach (string path in paths)
        {
            var report = new ModReport(Path.GetFileNameWithoutExtension(path));
            reports.Add(report);
            try
            {
                var module = game.Load(path);
                PatchScanner.Scan(module, game, report);
                AccessToolsScanner.Scan(module, game, report);
            }
            catch (BadImageFormatException)
            {
                report.Add(Severity.Warning, "", "", "not a .NET assembly, skipped");
            }
        }
        return reports;
    }
}
