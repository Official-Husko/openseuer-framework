using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class UninstallCommand : ICommand
{
    public string Name => "uninstall";
    public string Usage => "uninstall [--game DIR] [--remove-mods]";
    public string Summary => "Remove the loader and restore Unity's startup files (mods stay unless --remove-mods)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        if (GameProcess.IsRunning(game))
            throw new InvalidOperationException("Close the game first.");

        if (new BootConfig(game).Restore() is { } restored)
            Out.Info("  " + restored);
        foreach (string step in new LoaderInstaller(game).Uninstall(args.Has("remove-mods")))
            Out.Info("  " + step);
        Out.Ok("Game is back to vanilla.");
        return Task.FromResult(0);
    }
}
