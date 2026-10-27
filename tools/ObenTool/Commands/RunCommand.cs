using ObenTool.Bridge;
using ObenTool.Cli;
using ObenTool.Game;
using ObenTool.Scripting;

namespace ObenTool.Commands;

public sealed class RunCommand : ICommand
{
    public string Name => "run";
    public string Usage => "run <file.cs> [--watch] [--name N] | run --reset [NAME]";
    public string Summary => "Run a C# script file in the game; --watch re-runs it on every save, --reset undoes what scripts set up";

    public async Task<int> RunAsync(Args args)
    {
        var bridge = new BridgeClient(port: args.BridgePort);
        if (args.Has("reset"))
        {
            Console.WriteLine(BridgeClient.Pretty(await ScriptClient.ResetAsync(bridge, args.Get("reset") ?? args.At(0))));
            return 0;
        }

        string file = Path.GetFullPath(args.At(0) ?? throw new ArgumentException("Usage: " + Usage));
        if (!File.Exists(file))
            throw new FileNotFoundException($"No script at {file}", file);
        var game = GameInstall.Locate(args.Get("game"));
        string name = args.Get("name", Path.GetFileNameWithoutExtension(file));

        if (!args.Has("watch"))
            return await RunOnce(bridge, game, name, file) ? 0 : 1;

        Out.Info($"Watching {file}. Every save runs it again (Ctrl+C to stop).");
        DateTime seen = DateTime.MinValue;
        while (true)
        {
            var written = File.GetLastWriteTimeUtc(file);
            if (written != seen)
            {
                seen = written;
                Out.Info($"--- {DateTime.Now:HH:mm:ss} ---");
                await RunOnce(bridge, game, name, file);
            }
            await Task.Delay(400);
        }
    }

    // In watch mode a broken edit shouldn't end the loop, so errors are printed instead of thrown.
    private static async Task<bool> RunOnce(BridgeClient bridge, GameInstall game, string name, string file)
    {
        try
        {
            var result = await ScriptClient.RunAsync(bridge, game, name, await File.ReadAllTextAsync(file), Path.GetFileName(file));
            ScriptOutput.Print(result);
            return true;
        }
        catch (Exception e) when (e is BridgeException or ArgumentException or IOException)
        {
            Out.Error(e.Message);
            return false;
        }
    }
}
