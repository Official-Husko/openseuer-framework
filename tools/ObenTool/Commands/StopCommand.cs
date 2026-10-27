using ObenTool.Bridge;
using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class StopCommand : ICommand
{
    public string Name => "stop";
    public string Usage => "stop [--force] [--instance N]";
    public string Summary => "Close the game (asks nicely through the bridge, kills it with --force); --instance N only closes that copy";

    public async Task<int> RunAsync(Args args)
    {
        int? instance = args.InstanceFilter;
        if (!args.Has("force"))
        {
            try
            {
                await new BridgeClient(port: args.BridgePort)
                    .CallAsync("game.quit", timeout: TimeSpan.FromSeconds(3));
                for (int i = 0; i < 20 && GameProcess.IsRunning(instance); i++)
                    await Task.Delay(500);
            }
            catch (Exception e) when (e is BridgeException or OperationCanceledException or IOException)
            {
                // Fall through to killing it.
            }
        }

        if (GameProcess.IsRunning(instance))
            Out.Info($"Killed {GameProcess.Kill(instance)} process(es)");
        Out.Ok(instance is null ? "Game stopped." : $"Instance {instance} stopped.");
        return 0;
    }
}
