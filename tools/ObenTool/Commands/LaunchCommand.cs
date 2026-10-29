using ObenTool.Bridge;
using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class LaunchCommand : ICommand
{
    public string Name => "launch";
    public string Usage => "launch [--fullscreen] [--instance N] [--wait SECONDS]";
    public string Summary => "Start the game (windowed by default); --instance 2 starts a second copy, --wait waits for the bridge";

    public async Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        int instance = args.Instance;
        var bridge = new BridgeClient(port: args.BridgePort);

        // Instances can't be told apart before the loader is up, so an extra one is checked by its bridge port as well.
        if (GameProcess.IsRunning(instance) || (instance > 1 && await bridge.IsUpAsync()))
        {
            Out.Warn(instance > 1 ? $"Instance {instance} is already running." : "The game is already running.");
            return 0;
        }

        if (instance > 1 && !new BootConfig(game).AllowsMultipleCopies)
            throw new InvalidOperationException(
                "The game only allows one copy at a time. Run 'oben install --multi-instance' first (undo with --single-instance).");

        var gameArgs = args.Has("fullscreen") ? GameProcess.FullscreenArgs : GameProcess.WindowedArgs;
        Out.Info(GameProcess.Launch(game, instance, gameArgs));

        int wait = args.GetInt("wait", 0);
        if (wait <= 0)
            return 0;

        var deadline = DateTime.UtcNow.AddSeconds(wait);
        while (DateTime.UtcNow < deadline)
        {
            if (await bridge.IsUpAsync())
            {
                Out.Ok("Bridge is up.");
                return 0;
            }
            await Task.Delay(1000);
        }
        Out.Error($"Bridge didn't come up within {wait}s. Check {game.LoaderLogFile(instance)}");
        return 1;
    }
}
