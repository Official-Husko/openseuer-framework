using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class InstallCommand : ICommand
{
    public string Name => "install";
    public string Usage => "install [--game DIR]";
    public string Summary => "Hook the mod loader into the game (backs up the files it changes)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        if (GameProcess.IsRunning(game))
            throw new InvalidOperationException("Close the game first: the loader DLL is in use while it runs.");

        foreach (string step in new LoaderInstaller(game).Install())
            Out.Info("  " + step);

        Out.Ok($"Installed into {game.Dir}");
        return Task.FromResult(0);
    }
}
