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
        // Unity reads its startup files only at launch, so a running game just picks this up next time.
        if (GameProcess.IsRunning(game))
            Out.Warn("The game is running. The loader starts with it after a restart.");

        foreach (string step in new LoaderInstaller(game).Install())
            Out.Info("  " + step);

        Out.Ok($"Installed into {game.Dir}");
        return Task.FromResult(0);
    }
}
