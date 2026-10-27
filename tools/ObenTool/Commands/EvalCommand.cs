using ObenTool.Bridge;
using ObenTool.Cli;
using ObenTool.Game;
using ObenTool.Scripting;

namespace ObenTool.Commands;

public sealed class EvalCommand : ICommand
{
    public string Name => "eval";
    public string Usage => "eval <code> [--name N] [--depth N]";
    public string Summary => "Run a C# expression or statements in the game, e.g. eval \"GameController.instance.GameIsPaused\"";

    public async Task<int> RunAsync(Args args)
    {
        string code = args.At(0) ?? throw new ArgumentException("Usage: " + Usage);
        var game = GameInstall.Locate(args.Get("game"));
        var bridge = new BridgeClient(port: args.BridgePort);
        var result = await ScriptClient.RunAsync(bridge, game, args.Get("name", "eval"), code, "eval", args.GetInt("depth", 2));
        ScriptOutput.Print(result);
        return 0;
    }
}
