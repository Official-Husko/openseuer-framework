using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace ObenTool.Bridge;

/// <summary>
/// Talks to the DevBridge mod inside the game: one JSON object per line in each direction.
/// A new connection is made per call, which keeps things simple and survives game restarts.
/// </summary>
public sealed class BridgeClient
{
    public const int DefaultPort = 27860;

    /// <summary>Each extra game instance listens one port higher (instance 2 on 27851).</summary>
    public static int PortFor(int instance) => DefaultPort + instance - 1;

    private readonly string _host;
    private readonly int _port;
    private int _nextId;

    public BridgeClient(string host = "127.0.0.1", int port = DefaultPort)
    {
        _host = host;
        _port = port;
    }

    public async Task<JsonNode?> CallAsync(string method, JsonObject? parameters = null,
        TimeSpan? timeout = null, CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout ?? TimeSpan.FromSeconds(30));

        using var client = new TcpClient();
        try
        {
            await client.ConnectAsync(_host, _port, cts.Token);
        }
        catch (SocketException)
        {
            throw new BridgeException("Can't reach the game. Is it running with the DevBridge mod installed?");
        }

        var stream = client.GetStream();
        var request = new JsonObject
        {
            ["id"] = Interlocked.Increment(ref _nextId),
            ["method"] = method,
            ["params"] = parameters?.DeepClone() ?? new JsonObject(),
        };
        byte[] payload = Encoding.UTF8.GetBytes(request.ToJsonString() + "\n");
        await stream.WriteAsync(payload, cts.Token);

        using var reader = new StreamReader(stream, Encoding.UTF8);
        string? line = await reader.ReadLineAsync(cts.Token)
            ?? throw new BridgeException("The game closed the connection without answering.");

        var response = JsonNode.Parse(line)!.AsObject();
        if (response["ok"]?.GetValue<bool>() != true)
        {
            string error = response["error"]?.GetValue<string>() ?? "Unknown error";
            string? stack = response["stack"]?.GetValue<string>();
            throw new BridgeException(stack == null ? error : error + "\n" + stack);
        }
        return response["result"];
    }

    public async Task<bool> IsUpAsync(CancellationToken ct = default)
    {
        try
        {
            await CallAsync("ping", timeout: TimeSpan.FromSeconds(2), ct: ct);
            return true;
        }
        catch (Exception e) when (e is BridgeException or OperationCanceledException or IOException)
        {
            return false;
        }
    }

    public static string Pretty(JsonNode? node) => Cli.JsonOut.Format(node);
}

public sealed class BridgeException(string message) : Exception(message);
