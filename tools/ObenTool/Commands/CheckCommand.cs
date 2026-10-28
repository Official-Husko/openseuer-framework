using ObenTool.Checks;
using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class CheckCommand : ICommand
{
    public string Name => "check";
    public string Usage => "check [--game DIR] [--installed] [--verbose] [DLL...]";
    public string Summary => "Check that every Harmony patch and AccessTools lookup in the mods still matches the game (run after a game update)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        var dlls = ChooseDlls(args, game);
        if (dlls.Count == 0)
            throw new InvalidOperationException("No mod DLLs found. Build the mods first (dotnet build -c Release), or use --installed.");

        Out.Info($"Checking {dlls.Count} assemblies against {game.ManagedDir}");
        using var assemblies = new GameAssemblies(game.ManagedDir);
        var reports = CheckRunner.Run(assemblies, dlls);
        return Task.FromResult(CheckPrinter.Print(reports, args.Has("verbose")) > 0 ? 1 : 0);
    }

    // DLLs named on the command line, else what is installed (--installed), else the repo's Release builds.
    private static List<string> ChooseDlls(Args args, GameInstall game)
    {
        // The parser lets a flag swallow the word after it, so "--verbose a.dll" arrives as a value.
        var named = args.Positional.Concat(new[] { args.Get("verbose"), args.Get("installed") }.OfType<string>()).ToList();
        if (named.Count > 0)
            return named.Select(Path.GetFullPath).ToList();
        if (args.Has("installed"))
            return ModDlls.FromGame(game.ModsDir);

        string root = ModDlls.FindRepoRoot()
            ?? throw new InvalidOperationException("Not inside the repo. Run from the repo folder, or pass --installed or DLL paths.");
        return ModDlls.FromRepo(root);
    }
}
