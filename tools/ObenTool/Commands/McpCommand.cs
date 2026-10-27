using System.Text;
using ObenTool.Bridge;
using ObenTool.Cli;
using ObenTool.Game;
using ObenTool.Mcp;

namespace ObenTool.Commands;

public sealed class McpCommand : ICommand
{
    public string Name => "mcp";
    public string Usage => "mcp [--port N] [--instance N]";
    public string Summary => "Run as an MCP server on stdio (for editors and assistants)";

    public async Task<int> RunAsync(Args args)
    {
        var bridge = new BridgeClient(port: args.BridgePort);
        string? gameDir = args.Get("game");

        Func<GameInstall> game = () => GameInstall.Locate(gameDir);
        var tools = LocalTools.All(bridge, game, args.InstanceFilter)
            .Concat(BridgeTools.All(bridge))
            .Concat(ScriptTools.All(bridge, game));

        // stdout belongs to the protocol; anything else must go to stderr.
        var stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false)) { AutoFlush = false };
        var stdin = new StreamReader(Console.OpenStandardInput(), Encoding.UTF8);

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

        await new McpServer(tools, stdout).RunAsync(stdin, cts.Token);
        return 0;
    }
}
