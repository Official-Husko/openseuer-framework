using System;
using System.Collections.Generic;
using System.Linq;
using ObenCoop.Net.Presence;
using Xunit;

namespace ObenCoop.Net.Tests;

public class PresenceSessionTests
{
    private static readonly PresenceVector Here = new PresenceVector(10f, 1f, -4f);

    private static CoopPresence For(Node node, PresenceOptions options = null)
    {
        var presence = new CoopPresence(node.Session, options);
        node.Ticks.Add(presence.Tick);
        return presence;
    }

    [Fact]
    public void TheOthersSeeWhereAPlayerIs()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        using var b = Rig.JoinedClient(host, "B", a);
        var hostPresence = For(host);
        var aPresence = For(a);
        var bPresence = For(b);

        // A client's samples go through the host to the other client.
        Rig.Until(() =>
        {
            aPresence.Publish("Street", Here, 90f, new PresenceVector(1f, 0f, 0f), PresenceFlags.Moving);
            return hostPresence.Remotes.Count == 1 && bPresence.Remotes.Count == 1;
        }, "A's presence arrives", host, a, b);

        var seen = bPresence.Remotes.Single();
        Assert.Equal("A", seen.Player.Name);
        Assert.Equal("Street", seen.Area);
        Assert.Equal(Here, seen.Latest.Position);
        Assert.True(bPresence.TryGetPose(seen.Player, out var pose));
        Assert.Equal("Street", pose.Area);
        Assert.Empty(aPresence.Remotes);
    }

    [Fact]
    public void WithdrawingAndLeavingRemoveThePlayer()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        var hostPresence = For(host);
        var aPresence = For(a);
        var vanished = new List<string>();
        hostPresence.Vanished += r => vanished.Add(r.Player.Name);

        Rig.Until(() => { aPresence.Publish("Bar", Here, 0f, PresenceVector.Zero, PresenceFlags.None); return hostPresence.Remotes.Count == 1; },
            "presence arrives", host, a);
        aPresence.Withdraw();
        Rig.Until(() => hostPresence.Remotes.Count == 0, "withdrawn", host, a);

        Rig.Until(() => { aPresence.Publish("Bar", Here, 0f, PresenceVector.Zero, PresenceFlags.None); return hostPresence.Remotes.Count == 1; },
            "presence again", host, a);
        a.Session.Leave();
        Rig.Until(() => hostPresence.Remotes.Count == 0, "left", host, a);

        Assert.Equal(new[] { "A", "A" }, vanished);
    }

    [Fact]
    public void SamplesStopWhenPublishingStopsAndTheOthersDropThePlayer()
    {
        var options = new PresenceOptions { PublishTimeoutMs = 100, StaleAfterMs = 300 };
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        var hostPresence = For(host, options);
        var aPresence = For(a, options);

        Rig.Until(() => { aPresence.Publish("Bar", Here, 0f, PresenceVector.Zero, PresenceFlags.None); return hostPresence.Remotes.Count == 1; },
            "presence arrives", host, a);
        // Stop calling Publish (the game went to the main menu): A tells the others it stopped.
        Rig.Until(() => !aPresence.IsSharing && hostPresence.Remotes.Count == 0, "dropped", host, a);
    }

    [Fact]
    public void AStillPlayerSendsAtTheIdleRate()
    {
        var options = new PresenceOptions { SendIntervalMs = 20, IdleIntervalMs = 400 };
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        var hostPresence = For(host, options);
        var aPresence = For(a, options);

        Rig.Until(() => { aPresence.Publish("Bar", Here, 0f, PresenceVector.Zero, PresenceFlags.None); return hostPresence.Remotes.Count == 1; },
            "presence arrives", host, a);
        long before = aPresence.Sent;
        var until = DateTime.UtcNow.AddMilliseconds(300);
        while (DateTime.UtcNow < until)
        {
            aPresence.Publish("Bar", Here, 0f, PresenceVector.Zero, PresenceFlags.None);
            Rig.Settle(10, host, a);
        }
        Assert.True(aPresence.Sent - before <= 1, $"sent {aPresence.Sent - before} while standing still");

        float x = 0f;
        before = aPresence.Sent;
        until = DateTime.UtcNow.AddMilliseconds(300);
        while (DateTime.UtcNow < until)
        {
            x += 0.5f;
            aPresence.Publish("Bar", new PresenceVector(x, 0f, 0f), 0f, new PresenceVector(5f, 0f, 0f), PresenceFlags.Moving);
            Rig.Settle(10, host, a);
        }
        Assert.True(aPresence.Sent - before >= 5, $"only {aPresence.Sent - before} samples while moving");
    }

    [Fact]
    public void ABigJumpIsFlaggedSoOthersSnap()
    {
        using var host = Rig.StartHost();
        using var a = Rig.JoinedClient(host, "A");
        var hostPresence = For(host);
        var aPresence = For(a);

        Rig.Until(() => { aPresence.Publish("Bar", Here, 0f, PresenceVector.Zero, PresenceFlags.None); return hostPresence.Remotes.Count == 1; },
            "presence arrives", host, a);
        var far = new PresenceVector(500f, 0f, 0f);
        Rig.Until(() =>
        {
            aPresence.Publish("Bar", far, 0f, PresenceVector.Zero, PresenceFlags.None);
            return hostPresence.Remotes[0].Latest.Position.Equals(far);
        }, "jump arrives", host, a);

        Assert.True((hostPresence.Remotes[0].Latest.Flags & PresenceFlags.Teleported) != 0);
    }

    [Fact]
    public void PublishRefusesNonsense()
    {
        using var host = Rig.StartHost();
        var presence = For(host);
        Assert.Throws<ArgumentException>(() => presence.Publish("", Here, 0f, PresenceVector.Zero, PresenceFlags.None));
        Assert.Throws<ArgumentException>(() => presence.Publish("Bar", new PresenceVector(float.NaN, 0f, 0f), 0f, PresenceVector.Zero, PresenceFlags.None));
    }
}
