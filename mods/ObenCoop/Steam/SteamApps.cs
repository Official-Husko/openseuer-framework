using System.Globalization;
using ObenFramework.Game;

namespace ObenCoop.Steam;

/// <summary>
/// The Steam apps co-op can run the game as (config.json "SteamApp"). Lobbies and peer-to-peer networking only
/// meet within one app, so everyone in a game needs the same one.
/// </summary>
internal static class SteamApps
{
    /// <summary>TOXIKK, a free game on Steam: another app anyone can have, for when Spacewar's lobbies misbehave.</summary>
    public const uint ToxikkAppId = 324810;

    /// <summary>The app a setting names: "obenseuer", "spacewar", "toxikk" or an app id. Anything else is Obenseuer.</summary>
    public static uint Parse(string setting)
    {
        string text = (setting ?? "").Trim().ToLowerInvariant();
        switch (text)
        {
            case "spacewar": return GameSteam.SpacewarAppId;
            case "toxikk": return ToxikkAppId;
        }
        return uint.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out uint id) && id > 0 ? id : GameSteam.GameAppId;
    }

    /// <summary>How the app reads in the status: "Spacewar (480)", or "app 123" for one without a name here.</summary>
    public static string Describe(uint app) => app switch
    {
        GameSteam.GameAppId => "Obenseuer",
        GameSteam.SpacewarAppId => "Spacewar (480)",
        ToxikkAppId => "TOXIKK (324810)",
        _ => "app " + app.ToString(CultureInfo.InvariantCulture),
    };
}
