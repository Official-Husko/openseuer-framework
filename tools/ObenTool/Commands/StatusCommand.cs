using ObenTool.Bridge;
using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class StatusCommand : ICommand
{
    public string Name => "status";
    public string Usage => "status [--game DIR] [--instance N]";
    public string Summary => "Show install state, installed mods, how the game is launched and whether it's up";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        var installer = new LoaderInstaller(game);

        Out.Info($"Game:        {game.Dir}");
        Out.Info($"Loader DLLs: {(installer.FilesPresent ? "present" : "missing")}");
        Out.Info($"Registered:  {(installer.ManifestsPatched ? "yes" : "no")}");
        Out.Info($"Launch:      {LaunchProfile.Resolve(game)}");
        Out.Info($"Data folder: {game.PersistentDataDir() ?? "(unknown)"}");

        if (Directory.Exists(game.ModsDir))
        {
            var mods = Directory.GetDirectories(game.ModsDir)
                .Where(d => File.Exists(Path.Combine(d, "mod.json")))
                .Select(Path.GetFileName);
            Out.Info($"Mods:        {string.Join(", ", mods)}");
        }

        Out.Info($"Running:     {(GameProcess.IsRunning(args.InstanceFilter) ? "yes" : "no")}");
        Out.Info($"Bridge:      {(await new BridgeClient(port: args.BridgePort).IsUpAsync() ? "up" : "down")}");
        return 0;
    }
}
