Co-op for Obenseuer. **It isn't playable yet:** the networking, lobbies, chat and seeing where other players are work, but the shared world, where you'd actually play together, is still being built.

## The Co-op window

The **Co-op** button in the main menu and the pause menu opens it:

- **Host a game:** give it a name and choose **Direct (IP)** or **Steam**. Over Direct, players outside your home network need you to forward UDP port 27890 on your router; Steam doesn't need that.
- **Join a game:** type or paste a join code (or an address like `192.168.1.20`) and press **Join**.
- **Find games:** search your network and this PC, or Steam (your friends' games and public ones). Pick a game in the list to join it. A game you can't join says why, for example "other game version".
- **In a game** you see the join code, **Copy join code**, **Invite friends** (Steam), who is playing with their ping, and **Leave**.

**Streamer mode** is on by default: join codes, IP addresses and Steam ids show as stars (`****-****-****`) on screen, in the window, in notices and in the console. **Copy join code** still copies the real code, and **Show** reveals it until you close the window. Turn it off under MODS → Settings → ObenCoop → Hide join codes.

## Steam

Steam games need the Steam client running. Copies of the game that aren't on your Steam account (bought on GOG or elsewhere) can still play over Steam by running as another Steam app that every account has: MODS → Settings → ObenCoop → **Steam app**, then restart the game.

| Steam app | What it is |
| --- | --- |
| `obenseuer` | The game itself (the default). Needs the game on your Steam account. |
| `spacewar` | Valve's free test app (480). Every Steam account has it. |
| `toxikk` | TOXIKK (324810), a free game, if Spacewar gives you trouble. |

Everyone in a game needs the same choice. While the game runs as another app, Steam shows you playing that app, and Obenseuer's Steam achievements and play time don't count.

**On Linux** (Heroic, and other launchers that start games through umu): the game can't reach the Steam client when it's started from those, so Steam lobbies won't work there. Add the game to Steam as a non-Steam game and start it from Steam instead, or use Direct (IP).

## Console commands

Everything the window does also works in the F1 console:

| Command (F1 console) | What it does |
| --- | --- |
| `coop host [port]` | Host a game on your network (port 27890 unless you give another). |
| `coop join <address or code>` | Join a host by address (`192.168.1.20`, `example.com:27890`) or join code. |
| `coop invite` | Copies your game's join code. |
| `coop lobby create [steam] [name]` | Opens a lobby; with `steam` friends can join it through Steam. |
| `coop lobby list [steam]` | Lists games on your network, or your Steam friends' games. |
| `coop lobby join <number>` | Joins a game from that list. |
| `coop say <text>` | Chat with the other players. |
| `coop presence [on\|off]` | Lists the other players and how far away they are. |
| `coop status`, `coop leave` | Who's connected; leave the game. |

## Coming

The plan is to share the world piece by piece: other players' bodies and movement first, then the world itself, then saves. [What's New](The-MODS-Menu) will say when each part arrives.
