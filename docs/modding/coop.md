# ObenCoop

Co-op for Obenseuer, built in slices. This is the first one: the networking foundation. Two copies of the game can find each other over the network, shake hands, keep a list of who is in, and exchange messages. **Nothing touches game state yet**: no puppets, no shared world. The only visible features are the console commands and plain text chat.

The reference for the whole port is [../research/coop-reference.md](../research/coop-reference.md) (the MIT co-op mod in `example-mp/`, which has no networking code of its own; everything goes through its framework). The design here follows its section 2.4 (an abstract transport) and section 8 (port order), and the structure of the owner's co-op mod for another game.

## Layers

```
mods/ObenCoop/            the mod: CoopMod, console commands, chat, settings, identity file, reload handover
  uses
mods/ObenCoop.Net/        the library: no Unity or game references
  Session/                CoopSession: handshake, slots, roster, relay, persistence
  Messaging/              MessageRouter (channel ids), MessageWriter/Reader, MessagePool
  Transport/              ITransport, DirectTransport (LiteNetLib over UDP)
  uses
LiteNetLib 1.3.1 (MIT)    reliable-ordered and unreliable UDP, connection handling
```

Each layer only knows the one below it:

- **Transport** moves packets between *peers* (plain ints chosen by the transport). It knows nothing about players or channels. Reliable packets arrive once and in order, unreliable ones may be lost. Everything is raised from `Poll()` on the caller's thread; there is no threading above LiteNetLib's own, and the game calls `Poll()` once per frame.
- **Session** turns peers into players: it runs the handshake, gives out slots, keeps the roster, and wraps channel messages in a small envelope so the host can relay them.
- **Messaging** is how features talk: a feature registers a handler for a channel id, and sends pooled binary messages on it.

`ObenCoop.Net` targets `net472` (what the game's Mono loads) and `netstandard2.0` (so the tests run on .NET 10). It has its own `Directory.Build.props` so it does not inherit the game references of `mods/`.

## Transport

`ITransport` is deliberately small: `Host(port, maxPeers)`, `Connect(host, port)`, `Poll()`, `Send(peer, buffer, offset, count, mode)`, `SendToAll`, `DisconnectPeer(peer, reason)`, `PingOf`, `Stats`, `Stop`, plus events `PeerConnected`, `PeerDisconnected(peer, reason)` and `PacketReceived(peer, segment)`. The segment is only valid during the call.

Delivery is `ReliableOrdered` or `Unreliable`, chosen per message. Limits are in `TransportLimits`: 1200 bytes for an unreliable packet (envelope included), 64 KB for a reliable one. Big payloads (a world, a save) will need chunking above the session, like the reference's `NetBlob`; that comes with the join ticket slice.

`DirectTransport` is LiteNetLib: one channel, `ConnectionKey = "ObenCoop"`, IPv4 only (dual-mode sockets misbehave under Wine), 20 s disconnect timeout (a level load can stall the main thread). It has no NAT punch-through or relay: the host has to forward the UDP port, or both players use a VPN or the same LAN. A Steam transport would be a second `ITransport` and needs no change above it.

## Channels and messages

A **channel** is one feature's message stream, identified by a byte. `ChannelIds` lists the ones in use (`Chat = 1`; ids below 16 are reserved for real features, `FirstFree = 16` for experiments). A feature does

```csharp
Track(session.Router.Register(ChannelIds.Chat, (sender, reader) => { ... }));
```

One handler per channel; registering twice throws, and disposing the result frees the channel (hand it to `Track` so it goes away on unload). A handler that throws is reported through `Router.Error` and does not stop the others. Messages for a channel nobody listens on are counted in `Router.Dropped`.

To send:

```csharp
var w = MessagePool.RentWriter();
w.WriteInt32(42);
session.Broadcast(channel, w);            // everyone else
session.SendTo(player, channel, w);       // one player
session.SendToHost(channel, w);           // the host only
MessagePool.Return(w);
```

`MessageWriter` and `MessageReader` are little-endian binary with byte, bool, uint16, int32, uint32, int64, float, GUID, string (length-prefixed UTF-8, at most 4096 bytes) and raw bytes. Writers and readers come from `MessagePool`, so a steady-state send or receive allocates nothing (a test pins this; reading a string necessarily creates the string). Every read is bounds checked and throws `MessageFormatException` on a short or damaged message; the session drops such packets. The pool is per thread.

## Session, handshake and roster

The topology is a star: clients only talk to the host, and the host passes on what the others need. A client's `Broadcast` goes to the host, which delivers it itself and relays it to every other client with the real sender's slot filled in. That keeps direct IP simple (clients never need to reach each other) and matches the reference's full-mesh assumption well enough; guest-to-guest latency is host-to-guest plus guest-to-host.

**Packets** start with a kind byte:

| Kind | Direction | Content |
|---|---|---|
| `Hello` | client to host | protocol version (int), game version, player GUID, name, mod set |
| `Welcome` | host to client | protocol version, the client's slot, the roster |
| `Roster` | host to clients | the full roster, sent whenever it changes |
| `Data` / `DataUnreliable` | both | channel, slot, payload. From a client the slot is the target (a slot, or 255 for "everyone else"); from the host it is the sender |

**Handshake.** After the transport connects, the client sends `Hello`. The host checks, in this order: protocol version (read first, before anything else in the packet, so a future format change still gets a clean refusal), game version (`Application.version`), mod set, duplicate player GUID, free slot. Any failure sends the client away with a `RejectReason` and a sentence in the disconnect text (`"<code>:<message>"`, see `RejectText`), so the joiner's `SessionEnd` says, for example, `Your mods don't match the host's. You are missing: X. Different versions: Y (host 0.2.0, you 0.1.0).` A connection that sends no valid Hello within 5 s is dropped; a client gives up after 10 s without a `Welcome`. The host accepts a few more transport connections than it has slots, so a joiner to a full game gets "The game is full." instead of a bare refusal.

**Mod set.** The mod builds the set from the loaded mods (`ModRegistry`), by id and version, minus the ids in `IgnoredMods` of `config.json` (default `DevBridge` and `ScriptRunner`, which don't change the game for anyone else). Host and client sets must be equal.

**Slots and identity.** The host is slot 0; others get the lowest free slot, which is reused after someone leaves. A slot is only valid for one session. The persistent identity is the player GUID, generated once and stored in `Mods/ObenCoop/identity.json` (`identity-2.json` for a second test instance, otherwise both copies would be "the same player" and the host would refuse the second). Anything that must outlive a session (a guest's inventory, a prison record, a world folder) is keyed by that GUID, not by slot or peer id. This settles the "identity" risk from section 8.2 of the research: Steam ids are not needed. Names are cut to 24 characters by the host, and a duplicate gets a number (`Sam 2`).

**Roster.** `session.Roster` lists players by slot (`Players`, `Local`, `Host`, `Find(slot)`, `Find(guid)`) and is the same on every game. Events: `Started` (hosting, or the host accepted the join), `PlayerJoined`, `PlayerLeft(player, reason)`, `Ended(SessionEnd)`. A joining client gets `PlayerJoined` once for each player already there. `SessionEnd.Kind` is `Left`, `Rejected` (with the `RejectReason`), `ConnectFailed` or `ConnectionLost`. If the host goes away the session ends; there is no host migration.

## The mod

Files in `mods/ObenCoop/`:

| File | Job |
|---|---|
| `CoopMod.cs` | owns transport and session, pumps `Poll()` in `OnUpdate`, wires events to notices, registers the console command |
| `CoopSettings.cs` | `config.json`: `PlayerName`, `Port` (27890), `MaxPlayers` (4), `IgnoredMods` |
| `Identity/CoopIdentity.cs` | reads or creates the identity file |
| `Sessions/SessionKeeper.cs` | parks and resumes the session across hot reloads |
| `Sessions/ModSetBuilder.cs` | builds the mod set from `ModRegistry` |
| `Chat/CoopChat.cs`, `CoopNotices.cs` | chat on `ChannelIds.Chat`; shows text as a game notification in a level and in the console otherwise |
| `Commands/CoopCommands.cs`, `CoopStatus.cs` | the `coop` console command |

Settings are re-read each time you host or join, so editing `config.json` does not need a reload. The default name is `Player` (`Player 2` in a second instance).

### Console commands (F1)

```
coop host [port]        listen on a UDP port (default from config.json)
coop join <ip[:port]>   join a host; the result shows as a message
coop leave              leave, or stop hosting
coop status             state, roster with slots and ping, traffic, message counts
coop say <text>         chat; other players see it as a notification (in a level) or in their console
```

Chat text is stripped of `<` and `>` (the game's text is rich text) and cut to 200 characters.

### Why two assemblies and a library

`mod.json` lists `"assemblies": ["ObenCoop.Net.dll", "ObenCoop.dll"]`. `LiteNetLib.dll` also sits in the mod folder but is **not** listed, which makes the loader treat it as a library: resolved by name, loaded once, never reloaded. Both choices serve hot reload:

- A hot reload throws the mod's assemblies away and loads fresh copies, but a live UDP socket cannot be recreated without dropping every connection. LiteNetLib's `NetManager` is never reloaded, so the old copy of the mod can hand it to the new one. `DirectTransport.Detach()` returns the `NetManager`, its listener and the peer limit as an `object[]` (types from the never-reloaded assembly), `Adopt()` takes them back and re-hooks the events. `CoopSession.SaveState()` is plain bytes (state, roster, peer ids) that `RestoreState()` reads. `SessionKeeper` puts both in `ReloadState` on unload and takes them in `OnLoad`. If nothing takes them within 10 s (the mod was switched off, not reloaded) the connection is closed.
- `ObenCoop.Net.dll` is listed so that the networking code itself reloads too; it is the part that will change most. It is listed **first** on purpose: the loader renames each reloaded assembly and rewrites references only to assemblies it has already renamed, so `ObenCoop.dll` has to come after the library it references.

Anything parked must be plain data or a type from a never-reloaded assembly; a type from a reloaded one would not match in the next copy. A change to the wire format between two builds needs a game restart (the handshake's protocol version only protects connections between games).

## Testing

**Unit and loopback tests.** `tests/ObenCoop.Net.Tests` runs the real LiteNetLib over 127.0.0.1, a session on each end:

```sh
dotnet test tests/ObenCoop.Net.Tests
```

It covers the handshake, slots and roster, rejection by game version, mod set, protocol version, full game and duplicate GUID, a silent connection being dropped, reliable ordering both ways, relay between clients, directed sends, leave and host shutdown, a hot reload handover of a host and a client session, the codec and the zero-allocation pool. No game is needed.

**Two copies of the game on one machine.** `oben launch --instance 2` starts a second copy with `OBEN_INSTANCE=2`: its own loader and player logs (`latest-2.log`, `player-2.log`), the dev bridge one port up, its own `identity-2.json` and the name `Player 2`. Then:

1. In the first copy open the console (F1) and run `coop host`.
2. In the second copy run `coop join 127.0.0.1`.
3. `coop status` in both shows two players. `coop say hello` in one shows up in the other (as a notification once a level is loaded, otherwise in the console).
4. `coop leave` in the second copy; the first sees the player leave. Rebuild the mod while connected (`OBENSEUER_DIR=... dotnet build mods/ObenCoop/ObenCoop.csproj -c Release`, with auto reload on) and the session should carry on: `coop status` still shows both players afterwards, and the log says "Carried the session over a reload".

The same works through the dev bridge: `oben call console.exec command="coop status"`; add `--instance 2` for the second copy.

**The `single-instance=` entry.** The game's `Obenseuer_Data/boot.config` contains the line `single-instance=` (empty value). Unity's single-instance switch may make a second launch exit at once or just focus the first window. This has not been verified here. If the second copy does not stay up: close both, make a backup of `boot.config`, delete that line, and launch again. If it still refuses, the alternative is a second copy of the whole game folder run as a separate install. (The TODO item "Second game instance on one machine" is exactly this check.)

Both copies share the `Mods` folder, so they share `config.json` (port, name) and the hot reload watcher reloads both when a DLL changes. Only one of them can host on a given port; a second hosting copy needs `coop host <other port>`.

## Not done yet

- No Steam transport. `steam_api` is replaced in this install, so lobbies and P2P are out; direct IP is first, Steam is a second `ITransport` later.
- No NAT help. Forward the UDP port, use a VPN, or play on a LAN.
- No flood control. The reference caps packets per peer per frame (4 per frame, queue of 1024) and drops what is over; this belongs in the session's receive path once there are streams worth limiting. Only the size limits and the reader's bounds checks exist now.
- No chunking for large payloads, no request/ack helper (`Shared.NextReqId` in the reference), no timers for periodic state (`HST` announces, digests).
- The host is the only authority and there is no host migration.

## Planned slices

From section 8 of the research, in order:

1. **Session plus players.** Both players load the same save by hand. Presence at 10 Hz on an unreliable channel, puppets for other players (body, pose, sitting, appearance), the roster shown in a window (ObenUIKit). Chat gets a proper UI. Delivers: you see each other walk, sit and look.
2. **World clock and weather**, then the sleep vote. Low risk, visible result.
3. **Join ticket.** Chunked blob transfer over the reliable channel, the host's save shipped to a joining player, guest save redirection and the guest gate. Hardest single slice; needs fixture saves and self checks.
4. **Shared world state**: doors and lights, floor items and coins, containers, building. Requests go to the host, which validates and broadcasts the result; state is idempotent ("the state of X is Y") with periodic digests so late joiners and lost packets heal.
5. **Stations and NPC features**: crafting, liquids, power, shops, vending, dialogue, quests.
6. **The rest**: trains, elevators, sauna, slots, NPC and animal positions, combat, harvest, prison, map markers, players panel.
7. **Last or never**: free roam (players in different levels, authority per scene). Decide early whether version 1 forces everyone into the host's scene; that removes the whole authority-per-scene layer. Keep an `Authority` accessor so it can be added later.

A **Steam transport** can be added at any point after slice 1 without touching the layers above it.
