# Steam

`GameSteam` (namespace `ObenFramework.Game`) is the game's own Steam: whether it is up, who is signed in, names and friends, for any mod. It never starts Steam itself. The game's `SteamManager` starts the Steam API in the first scene; until it did, or when it failed, or when there is no Steam (a copy from another store), every member answers "nothing": `false`, `0`, `""` or an empty list. So a mod can call these at any time without checking first.

```csharp
using ObenFramework.Game;

if (GameSteam.IsReady)
    Log.Info($"Signed in as {GameSteam.LocalName} ({GameSteam.LocalId}), app {GameSteam.AppId}");

string name = GameSteam.Name(steamId);              // "" while Steam fetches it: ask again a moment later
List<ulong> playing = GameSteam.FriendsPlaying();   // friends in Obenseuer right now
```

| Member | What it is |
|---|---|
| `IsReady` | The game started the Steam API. Asks the game at most once a second until it says yes, then stays true, so it is cheap every frame. |
| `Problem` | Why Steam can't be told apart from "not up yet" (the game changed its `SteamManager`), or null. |
| `LocalId`, `LocalName` | The signed-in player's Steam id and name; 0 and "" when Steam isn't up. |
| `AppId` | The app Steam started the game as: `GameAppId` (951240) normally, `SpacewarAppId` (480) after `StartAs`. 0 when Steam isn't up. |
| `OverlayEnabled` | The Steam overlay can open (invite dialogs, profiles). |
| `Name(id)` | Someone's Steam name. Steam knows friends and people met recently (a lobby, a game); for anyone else it asks Steam to fetch the name and returns "". |
| `FriendsPlaying(appId)` | Steam ids of friends playing an app now, this game's by default. |
| `StartAs(owner, appId, out problem)` | Has the game start Steam as another app, see below. |

Names come from other people, so they arrive without rich text tags (`<`, `>`), control characters or invisible formatting characters (direction overrides, zero-width characters), cut to 32 characters without splitting an emoji.

## Running as Spacewar

```csharp
public override void OnLoad()
{
    if (!GameSteam.StartAs(this, GameSteam.SpacewarAppId, out string problem))
        Log.Warn(problem);   // the game started Steam already: the switch needs a restart
}
```

`StartAs` makes the game start Steam as another app: in practice Valve's free test app Spacewar (480), which every Steam account has. Steam lobbies and peer-to-peer networking only meet within one app, so with Spacewar copies of the game from GOG or another store can play over Steam (the Steam client has to run), and testing needs no second copy of the game. ObenCoop uses it for its `SteamApp` setting (docs/modding/coop.md).

It has to happen before the game starts Steam, so call it from `OnLoad`: mods load before the first scene, where the game's `SteamManager` wakes up. It sets the `SteamAppId` and `SteamGameId` environment variables of the game's process (what Steam reads when it starts) and skips the game's `SteamAPI.RestartAppIfNecessary(951240)`, which would otherwise relaunch the game through Steam as Obenseuer. Once the game started Steam, `StartAs` returns true only for the app already running and false with a sentence otherwise; nothing can switch Steam's app in a running game. `StartAs(this, GameSteam.GameAppId, ...)` before Steam starts takes a request back. `RequestedAppId` says what was asked for this run (0 for none).

While the game runs as Spacewar, Steam shows the player in Spacewar, and Obenseuer's achievements, stats and play time don't reach Steam (`GameAchievements` still changes the game's own records). Say so wherever players choose it.
