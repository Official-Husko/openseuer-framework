using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;

namespace ObenCoop.Net.Tests;

/// <summary>A session plus its transport and a record of the events it raised.</summary>
internal sealed class Node : IDisposable
{
    public Node(string name, SessionOptions options = null)
    {
        Transport = new DirectTransport();
        Options = options ?? new SessionOptions();
        Options.PlayerName = name;
        Options.GameVersion = "1.0";
        Session = new CoopSession(Transport, Options);
        Session.Started += () => StartedCount++;
        Session.PlayerJoined += p => Joined.Add(p);
        Session.PlayerLeft += (p, why) => Left.Add((p, why));
        Session.Ended += e => Ends.Add(e);
    }

    public DirectTransport Transport { get; set; }
    public SessionOptions Options { get; }
    public CoopSession Session { get; set; }
    public int StartedCount;
    public List<CoopPlayer> Joined { get; } = new List<CoopPlayer>();
    public List<(CoopPlayer Player, string Reason)> Left { get; } = new List<(CoopPlayer, string)>();
    public List<SessionEnd> Ends { get; } = new List<SessionEnd>();

    public void Dispose() => Transport.Stop();
}

internal static class Rig
{
    /// <summary>Polls the nodes until the condition holds. Fails the test after the timeout.</summary>
    public static void Until(Func<bool> condition, string what, params object[] pumps)
    {
        var watch = Stopwatch.StartNew();
        while (!condition())
        {
            if (watch.ElapsedMilliseconds > 8000)
                throw new TimeoutException("Timed out waiting for: " + what);
            Pump(pumps);
            Thread.Sleep(2);
        }
    }

    // A pump is a Node (its session is polled) or a bare transport.
    private static void Pump(object[] pumps)
    {
        foreach (var pump in pumps)
        {
            if (pump is Node node)
                node.Session.Poll();
            else
                ((ITransport)pump).Poll();
        }
    }

    /// <summary>Polls for a while without waiting for anything, to show that nothing else happens.</summary>
    public static void Settle(int ms, params object[] pumps)
    {
        var watch = Stopwatch.StartNew();
        while (watch.ElapsedMilliseconds < ms)
        {
            Pump(pumps);
            Thread.Sleep(2);
        }
    }

    public static Node StartHost(string name = "Host", int maxPlayers = 4, SessionOptions options = null)
    {
        var host = new Node(name, options);
        host.Options.MaxPlayers = maxPlayers;
        host.Session.Host(0);
        return host;
    }

    public static string Address(Node host) => "127.0.0.1:" + host.Transport.Port;

    public static Node JoinedClient(Node host, string name, params Node[] others)
    {
        var client = new Node(name);
        client.Session.Join(Address(host));
        var all = new List<object> { host, client };
        all.AddRange(others);
        Until(() => client.Session.State == SessionState.Joined, name + " joined", all.ToArray());
        return client;
    }
}
