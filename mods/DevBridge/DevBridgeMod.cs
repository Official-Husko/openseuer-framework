using DevBridge.Server;
using ObenLoader;

namespace DevBridge;

/// <summary>Starts the bridge server and exposes the command table to other mods.</summary>
public sealed class DevBridgeMod : ObenMod
{
    private BridgeServer _server;

    /// <summary>The command table. Other mods can add their own commands here.</summary>
    public CommandRouter Commands { get; } = new CommandRouter();

    internal static DevBridgeMod Instance { get; private set; }

    public override void OnLoad()
    {
        Instance = this;
        Commands.AddFromAssembly(GetType().Assembly);

        var config = LoadConfig<BridgeConfig>();
        config.Save(); // Writes defaults on first run so the file is easy to find and edit.

        _server = Track(new BridgeServer(config.Value, Commands, Log));
        _server.Start();
        BackgroundRunning.Init(this, config.Value.KeepRunningInBackground);
    }

    public override void OnUnload() => Instance = null;
}
