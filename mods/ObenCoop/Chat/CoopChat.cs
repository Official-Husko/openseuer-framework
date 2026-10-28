using System;
using ObenCoop.Net.Messaging;
using ObenCoop.Net.Session;
using ObenLoader;
using ObenLoader.Logging;

namespace ObenCoop.Chat;

/// <summary>
/// Plain text chat on <see cref="ChannelIds.Chat"/>: a message is one string. It is the first user of the
/// channel layer and a way to check by hand that two games really talk.
/// </summary>
internal sealed class CoopChat
{
    private readonly CoopSession _session;
    private readonly ModLogger _log;

    public CoopChat(ObenMod owner, CoopSession session, ModLogger log)
    {
        _session = session;
        _log = log;
        owner.Track(session.Router.Register(ChannelIds.Chat, OnMessage));
    }

    /// <summary>Sends a line to everyone else and shows it to this player too. False when not in a session.</summary>
    public bool Say(string text)
    {
        text = CoopNotices.Clean(text);
        if (text.Length == 0 || !_session.IsActive)
            return false;

        var writer = MessagePool.RentWriter();
        try
        {
            writer.WriteString(text);
            _session.Broadcast(ChannelIds.Chat, writer);
        }
        finally
        {
            MessagePool.Return(writer);
        }
        CoopNotices.Show(_log, _session.Roster.Local.Name, text);
        return true;
    }

    private void OnMessage(CoopPlayer sender, MessageReader reader)
    {
        string text = reader.ReadString();
        CoopNotices.Show(_log, sender.Name, text);
    }
}
