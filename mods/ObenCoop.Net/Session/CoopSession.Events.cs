using System;

namespace ObenCoop.Net.Session;

// Event raising. A subscriber that throws is logged and must not break the packet loop.
public sealed partial class CoopSession
{
    private void Raise(Action handlers)
    {
        try { handlers?.Invoke(); }
        catch (Exception e) { Log("A handler threw: " + e); }
    }

    private void Raise(Action<Player> handlers, Player player)
    {
        try { handlers?.Invoke(player); }
        catch (Exception e) { Log("A handler threw: " + e); }
    }

    private void Raise(Action<Player, string> handlers, Player player, string text)
    {
        try { handlers?.Invoke(player, text); }
        catch (Exception e) { Log("A handler threw: " + e); }
    }
}
