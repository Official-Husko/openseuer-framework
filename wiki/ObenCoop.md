Co-op for Obenseuer. **It isn't playable yet:** the networking, lobbies, chat and seeing where other players are work, but the shared world, where you'd actually play together, is still being built.

There's a **Co-op** button in the main menu and the pause menu. For now it shows whether you're in a co-op game; hosting and joining go through the F1 console.

## What works

| Command (F1 console) | What it does |
| --- | --- |
| `coop host [port]` | Host a game on your network (port 27890 unless you give another). |
| `coop join <address or code>` | Join a host by address (`192.168.1.20`, `example.com:27890`) or join code. |
| `coop invite` | Gives a short join code for your game. |
| `coop lobby create [steam] [name]` | Opens a lobby; with `steam` friends can join it through Steam. |
| `coop lobby list [steam]` | Lists games on your network, or your Steam friends' games. |
| `coop lobby join <number>` | Joins a game from that list. |
| `coop say <text>` | Chat with the other players. |
| `coop presence [on\|off]` | Lists the other players and how far away they are. |
| `coop status`, `coop leave` | Who's connected; leave the game. |

Over the network the host has to forward the port (UDP 27890) for players outside their home network. Steam lobbies don't need that.

## Coming

The plan is to share the world piece by piece: other players' bodies and movement first, then the world itself, then saves. [What's New](The-MODS-Menu) will say when each part arrives.
