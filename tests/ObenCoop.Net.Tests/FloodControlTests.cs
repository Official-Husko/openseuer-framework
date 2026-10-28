using System.Collections.Generic;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using Xunit;

namespace ObenCoop.Net.Tests;

public class FloodControlTests
{
    private const byte Numbers = ChannelIds.FirstFree;

    private static void SendNumber(Node node, int value)
    {
        var writer = MessagePool.RentWriter();
        writer.WriteInt32(value);
        node.Session.Broadcast(Numbers, writer);
        MessagePool.Return(writer);
    }

    [Fact]
    public void TheBudgetSpreadsABurstOverSeveralPollsWithoutReordering()
    {
        using var host = Rig.StartHost();
        host.Options.MaxMessagesPerPoll = 10;
        using var client = Rig.JoinedClient(host, "C");

        int poll = 0;
        var arrivals = new List<(int Poll, int Value)>();
        host.Session.Router.Register(Numbers, (from, reader) => arrivals.Add((poll, reader.ReadInt32())));

        for (int i = 0; i < 300; i++)
            SendNumber(client, i);
        // Only the client runs, so the whole burst is waiting in the host's socket when it polls.
        Rig.Settle(300, client);

        while (arrivals.Count < 300)
        {
            poll++;
            host.Session.Poll();
            Assert.True(poll < 1000, "the queue never emptied");
            System.Threading.Thread.Sleep(1);
        }

        for (int i = 0; i < 300; i++)
            Assert.Equal(i, arrivals[i].Value);
        var perPoll = new Dictionary<int, int>();
        foreach (var (p, _) in arrivals)
            perPoll[p] = perPoll.TryGetValue(p, out int n) ? n + 1 : 1;
        Assert.True(perPoll.Count >= 30, $"expected 30 polls or more, saw {perPoll.Count}");
        Assert.All(perPoll.Values, n => Assert.True(n <= 10));
        Assert.True(host.Session.ReceivesDeferred >= 290 - 10);
        Assert.Equal(0, host.Session.ReceivesWaiting);
    }

    [Fact]
    public void OnePeersBurstDoesNotHoldBackAnother()
    {
        using var host = Rig.StartHost();
        host.Options.MaxMessagesPerPoll = 5;
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);

        int poll = 0;
        host.Session.Router.Register(Numbers, (from, reader) => reader.ReadInt32());
        var seen = new Dictionary<string, int>();
        host.Session.Router.Register(Numbers + 1, (from, reader) => seen[from.Name] = poll);

        for (int i = 0; i < 200; i++)
            SendNumber(a, i);
        var writer = MessagePool.RentWriter();
        b.Session.Broadcast(Numbers + 1, writer);
        MessagePool.Return(writer);
        Rig.Settle(300, a, b);

        poll = 1;
        host.Session.Poll();
        Assert.True(seen.ContainsKey("B") && seen["B"] == 1, "B's message was handled in the first poll");
        Assert.True(host.Session.ReceivesWaiting > 100, "A's burst is still waiting");
    }

    [Fact]
    public void AZeroBudgetMeansNoLimit()
    {
        using var host = Rig.StartHost();
        host.Options.MaxMessagesPerPoll = 0;
        using var client = Rig.JoinedClient(host, "C");
        var values = new List<int>();
        host.Session.Router.Register(Numbers, (from, reader) => values.Add(reader.ReadInt32()));

        for (int i = 0; i < 200; i++)
            SendNumber(client, i);
        Rig.Settle(300, client);
        host.Session.Poll();

        Assert.Equal(200, values.Count);
        Assert.Equal(0, host.Session.ReceivesDeferred);
    }

    [Fact]
    public void AnOverflowingQueueDisconnectsThePeerWithAClearReason()
    {
        using var host = Rig.StartHost();
        host.Options.MaxMessagesPerPoll = 1;
        host.Options.MaxQueuedMessages = 20;
        using var client = Rig.JoinedClient(host, "C");
        host.Session.Router.Register(Numbers, (from, reader) => { });

        for (int i = 0; i < 200; i++)
            SendNumber(client, i);
        Rig.Settle(300, client);
        host.Session.Poll();

        Rig.Until(() => client.Ends.Count == 1, "client cut off", host, client);
        Assert.Equal(EndKind.ConnectionLost, client.Ends[0].Kind);
        Assert.Contains("Too many messages", client.Ends[0].Message);
    }

    [Fact]
    public void ASlowPeersFullSendQueueRefusesSendsWithAClearError()
    {
        using var host = Rig.StartHost();
        host.Options.MaxSendQueue = 8;
        using var client = Rig.JoinedClient(host, "C");
        var refusals = new List<(CoopPlayer Player, string Text)>();
        host.Session.SendRefused += (p, text) => refusals.Add((p, text));

        // The client isn't polled, but its transport still takes in what it can; a pile of big
        // reliable messages outruns the wire and backs up in the host's queue.
        var writer = MessagePool.RentWriter();
        writer.WriteBytes(new byte[60 * 1024], 0, 60 * 1024);
        int accepted = 0;
        bool refused = false;
        for (int i = 0; i < 40 && !refused; i++)
        {
            if (host.Session.Broadcast(Numbers, writer))
                accepted++;
            else
                refused = true;
        }
        MessagePool.Return(writer);

        Assert.True(refused, "the queue limit was never hit");
        Assert.True(accepted > 0);
        Assert.Single(refusals);
        Assert.Equal("C", refusals[0].Player.Name);
        Assert.Contains("waiting to go out", refusals[0].Text);
        Assert.Contains("limit 8", refusals[0].Text);
        Assert.True(host.Session.SendsRefused >= 1);

        // Once the peer catches up, sends work again.
        var received = new List<int>();
        client.Session.Router.Register(Numbers, (from, reader) => received.Add(reader.Remaining));
        Rig.Until(() => received.Count == accepted, "queue drained", host, client);
        var small = MessagePool.RentWriter();
        small.WriteInt32(1);
        Assert.True(host.Session.Broadcast(Numbers, small));
        MessagePool.Return(small);
    }
}
