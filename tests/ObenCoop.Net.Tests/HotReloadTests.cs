using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenCoop.Net.Transport;
using Xunit;

namespace ObenCoop.Net.Tests;

public class HotReloadTests
{
    // What the mod does on a hot reload: park the connection and the session bytes, build new
    // objects (as the next copy of the assembly would), adopt and restore.
    private static (DirectTransport Transport, CoopSession Session) Reload(Node node)
    {
        byte[] state = node.Session.SaveState();
        object[] parked = node.Transport.Detach();
        Assert.NotNull(state);
        Assert.NotNull(parked);

        var transport = new DirectTransport();
        Assert.True(transport.Adopt(parked));
        var session = new CoopSession(transport, node.Options);
        Assert.True(session.RestoreState(state));
        return (transport, session);
    }

    [Fact]
    public void HostSessionSurvivesAReloadAndKeepsItsClients()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        Rig.Until(() => host.Session.Roster.Count == 2, "client counted", host, client);

        var (transport, session) = Reload(host);
        host.Session = session;
        host.Transport = transport;
        var received = new System.Collections.Generic.List<int>();
        session.Router.Register(ChannelIds.FirstFree, (from, reader) => received.Add(reader.ReadInt32()));

        Assert.Equal(SessionState.Hosting, session.State);
        Assert.Equal(2, session.Roster.Count);
        Assert.Equal("C", session.Roster.Find(1).Name);

        var writer = MessagePool.RentWriter();
        writer.WriteInt32(77);
        client.Session.SendToHost(ChannelIds.FirstFree, writer);
        MessagePool.Return(writer);
        Rig.Until(() => received.Count == 1, "message after reload", host, client);

        // The other direction works too, and a new joiner is accepted by the restored host.
        var answers = new System.Collections.Generic.List<int>();
        client.Session.Router.Register(ChannelIds.FirstFree, (from, reader) => answers.Add(reader.ReadInt32()));
        writer = MessagePool.RentWriter();
        writer.WriteInt32(78);
        session.Broadcast(ChannelIds.FirstFree, writer);
        MessagePool.Return(writer);
        Rig.Until(() => answers.Count == 1, "message to client", host, client);

        using var late = Rig.JoinedClient(host, "Late", client);
        Assert.Equal(2, late.Session.Roster.Local.Slot);
    }

    [Fact]
    public void ClientSessionSurvivesAReload()
    {
        using var host = Rig.StartHost();
        using var client = Rig.JoinedClient(host, "C");
        Rig.Until(() => host.Session.Roster.Count == 2, "client counted", host, client);

        var (transport, session) = Reload(client);
        client.Session = session;
        client.Transport = transport;
        var received = new System.Collections.Generic.List<string>();
        session.Router.Register(ChannelIds.Chat, (from, reader) => received.Add(from.Name + ":" + reader.ReadString()));

        Assert.Equal(SessionState.Joined, session.State);
        Assert.Equal(1, session.Roster.Local.Slot);
        Assert.True(session.Roster.Host.IsHost);

        var writer = MessagePool.RentWriter();
        writer.WriteString("hello again");
        host.Session.Broadcast(ChannelIds.Chat, writer);
        MessagePool.Return(writer);
        Rig.Until(() => received.Count == 1, "chat after reload", host, client);

        Assert.Equal("Host:hello again", received[0]);
    }

    [Fact]
    public void AdoptRefusesJunkAndStoppedConnections()
    {
        var transport = new DirectTransport();
        Assert.False(transport.Adopt(null));
        Assert.False(transport.Adopt(new object[] { 1, 2, 3 }));

        var host = Rig.StartHost();
        object[] parked = host.Transport.Detach();
        DirectTransport.Abandon(parked);
        Assert.False(new DirectTransport().Adopt(parked));
    }

    [Fact]
    public void RestoreStateRejectsGarbageAndOfflineTransports()
    {
        using var node = new Node("x");
        Assert.Null(node.Session.SaveState());
        Assert.False(node.Session.RestoreState(new byte[] { 1, 2, 3 }));
        Assert.False(node.Session.RestoreState(null));
    }
}
