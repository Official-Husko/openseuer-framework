using ObenTool.Bridge;

namespace ObenTool.Cli;

/// <summary>Minimal argument parser: positional values plus --name value / --flag options.</summary>
public sealed class Args
{
    private readonly Dictionary<string, string?> _options = new(StringComparer.OrdinalIgnoreCase);

    public List<string> Positional { get; } = new();

    public Args(IEnumerable<string> raw)
    {
        var list = raw.ToList();
        for (int i = 0; i < list.Count; i++)
        {
            string a = list[i];
            if (!a.StartsWith("--"))
            {
                Positional.Add(a);
                continue;
            }

            string name = a[2..];
            int eq = name.IndexOf('=');
            if (eq >= 0)
                _options[name[..eq]] = name[(eq + 1)..];
            else if (i + 1 < list.Count && !list[i + 1].StartsWith("--"))
                _options[name] = list[++i];
            else
                _options[name] = null;
        }
    }

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) => _options.TryGetValue(name, out var v) ? v : null;

    public string Get(string name, string fallback) => Get(name) ?? fallback;

    public int GetInt(string name, int fallback) => int.TryParse(Get(name), out int v) ? v : fallback;

    /// <summary>Which game instance the command is about: --instance N, 1 when not given.</summary>
    public int Instance
    {
        get
        {
            if (!Has("instance"))
                return 1;
            if (!int.TryParse(Get("instance"), out int n) || n < 1)
                throw new ArgumentException("--instance needs a number from 1 up.");
            return n;
        }
    }

    /// <summary>The instance when --instance was given, otherwise null (meaning every instance for commands that stop things).</summary>
    public int? InstanceFilter => Has("instance") ? Instance : null;

    /// <summary>The dev bridge port: --port, or 27860 plus one per extra instance.</summary>
    public int BridgePort => Has("port") ? GetInt("port", BridgeClient.DefaultPort) : BridgeClient.PortFor(Instance);

    public string? At(int index) => index < Positional.Count ? Positional[index] : null;
}
