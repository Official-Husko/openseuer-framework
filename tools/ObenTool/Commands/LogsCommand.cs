using ObenTool.Cli;
using ObenTool.Game;

namespace ObenTool.Commands;

public sealed class LogsCommand : ICommand
{
    public string Name => "logs";
    public string Usage => "logs [--player | --runner] [--lines N] [--instance N]";
    public string Summary => "Print the end of the loader log, Unity's Player.log (--player) or umu/Proton's output (--runner)";

    public Task<int> RunAsync(Args args)
    {
        var game = GameInstall.Locate(args.Get("game"));
        int instance = args.Instance;
        string path = args.Has("player") ? game.PlayerLogFile(instance)
            : args.Has("runner") ? Path.Combine(game.LogsDir, "runner" + GameInstall.Suffix(instance) + ".log")
            : game.LoaderLogFile(instance);

        if (!File.Exists(path))
        {
            Out.Error($"No log at {path}");
            return Task.FromResult(1);
        }

        // Share read/write: the game keeps the file open.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        var lines = reader.ReadToEnd().Split('\n');
        int count = args.GetInt("lines", 60);
        foreach (string line in lines.Skip(Math.Max(0, lines.Length - count)))
            Console.WriteLine(line);
        return Task.FromResult(0);
    }
}
