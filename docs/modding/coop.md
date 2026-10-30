# ObenCoop

Co-op for Obenseuer, built in slices. The first ones are in: the networking foundation (two copies of the game find each other, shake hands, keep a list of who is in and exchange messages), lobbies (lists, join codes, invites and version checks, over direct IP and over Steam) and presence (where every player is). **Nothing touches game state yet**: no puppets, no shared world. The visible features are the console commands, plain text chat and the presence list.

The reference for the whole port is [../research/coop-reference.md](../research/coop-reference.md) (the MIT co-op mod in `example-mp/`, which has no networking code of its own; everything goes through its framework, whose `Lobby`, `Steam`, `SteamBridge`, `Net*` and `Presence` are module M12 in [../research/framework-reference.md](../research/framework-reference.md)). The design here follows its section 2.4 (an abstract transport) and section 8 (port order), and the structure of the owner's co-op mod for another game.

## Layers

```
mods/ObenCoop/            the mod: CoopMod, console commands, notices, chat, presence publisher, settings,
                          identity file, reload handover, Steam glue (Steam/), JSON for the dev bridge (Bridge/)
  uses
mods/ObenCoop.Net/        the library: no Unity or game references
  Lobby/                  CoopLobby (state machine), LobbyInfo, LobbyCheck, JoinCode, LobbyAddress, LobbyBrowser,
                          DirectLobbyBackend + LobbyScanner (direct IP), Matchmaking/ (Steam-style lobbies)
  Presence/               CoopPresence: position sharing with interpolation data
  Session/                CoopSession: handshake, slots, roster, relay, persistence
  Messaging/              MessageRouter (channel ids), MessageWriter/Reader, MessagePool
  Blobs/                  BlobTransfer: big byte arrays in chunks (compression, CRC-32, windowed acks)
  Transport/              ITransport, DirectTransport (LiteNetLib over UDP), P2P/P2PTransport (Steam-style P2P)
  uses
LiteNetLib 1.3.1 (MIT)    reliable-ordered and unreliable UDP, connection handling
Steamworks.NET            the game's own (Assembly-CSharp-firstpass), only through mods/ObenCoop/Steam/
```

Each layer only knows the one below it:

- **Transport** moves packets between *peers* (plain ints chosen by the transport). It knows nothing about players or channels. Reliable packets arrive once and in order, unreliable ones may be lost. Everything is raised from `Poll()` on the caller's thread; there is no threading above LiteNetLib's own, and the game calls `Poll()` once per frame.
- **Session** turns peers into players: it runs the handshake, gives out slots, keeps the roster, and wraps channel messages in a small envelope so the host can relay them.
- **Messaging** is how features talk: a feature registers a handler for a channel id, and sends pooled binary messages on it.

`ObenCoop.Net` targets `net472` (what the game's Mono loads) and `netstandard2.0` (so the tests run on .NET 10). It has its own `Directory.Build.props` so it does not inherit the game references of `mods/`.

## Transport

`ITransport` is deliberately small: `Host(port, maxPeers)`, `Connect(host, port)`, `Poll()`, `Send(peer, buffer, offset, count, mode)`, `SendToAll`, `DisconnectPeer(peer, reason)`, `PingOf`, `Stats`, `Stop`, plus events `PeerConnected`, `PeerDisconnected(peer, reason)` and `PacketReceived(peer, segment)`. The segment is only valid during the call.

Delivery is `ReliableOrdered` or `Unreliable`, chosen per message. Limits are in `TransportLimits`: 1200 bytes for an unreliable packet (envelope included), 64 KB for a reliable one. Big payloads (a world, a save) go through `session.Blobs`, which chunks them above the session like the reference's `NetBlob` (see "Big payloads" below).

`DirectTransport` is LiteNetLib: one channel, `ConnectionKey = "ObenCoop"`, IPv4 only (dual-mode sockets misbehave under Wine), 20 s disconnect timeout (a level load can stall the main thread). It has no NAT punch-through or relay: the host has to forward the UDP port, or both players use a VPN or the same LAN. It also answers packets that arrive outside any connection through `Responder` (at most 32 replies a second); the lobby uses that to answer lobby queries on the game's own port.

`P2PTransport` (in `Transport/P2P/`) is the second transport: connections over a peer-to-peer packet network addressed by 64-bit ids, `IP2PNetwork`, which is shaped after Steam's legacy `ISteamNetworking` (the API the game's Steamworks.NET has and the reference used). That API has sessions but no connect or disconnect, so the transport adds a small control layer: the first byte of every packet is `Hello` (client to host, with magic bytes), `Accept`, `Data`, `Bye` (with the reason, which carries the session's reject text), `Ping`/`Pong` (round trips, once a second). A host only lets in ids that `Admit` allows (the members of its Steam lobby); a session request from someone the lobby doesn't list yet waits up to 5 s, because Steam delivers lobby membership and P2P requests separately. 20 s of silence or a Steam session failure ends a connection, a client gives up after 15 s without an `Accept`, and sessions are closed 2 s after the goodbye so it still arrives. Steam's unreliable limit is 1200 bytes with the type byte, so the rare packet at exactly 1200 goes reliable. The session can switch transports while offline (`UseTransport`); channels, blob listeners and event handlers stay.

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

Channel ids in use: `Chat = 1`, `Blob = 2` (the session's), `Presence = 3`, `Named = 4`.

**Named channels** are for mods: a channel named by a string (`"mymod.doors"`, up to 64 characters, no spaces), so two mods can't pick the same byte by accident. They work the same, with a string where the byte was:

```csharp
Track(session.Router.Register("mymod.doors", (sender, reader) => { ... }));
session.Broadcast("mymod.doors", w);     // also SendTo(player, "mymod.doors", w) and SendToHost("mymod.doors", w)
```

They all travel on `ChannelIds.Named` with the name's 32-bit key in front (`ChannelNames.Key`, FNV-1a over the name, never below 256), so the name itself never goes over the wire and costs 4 bytes a message. Both sides work the key out from the name. Registering a name that is taken, or whose key another name already has, throws. A message for a name nobody here listens on (the other player has a mod feature this one hasn't) is counted in `Router.Dropped` and otherwise ignored; a handler that throws is reported through `Router.Error` with the channel's name. Other mods reach the session through `Coop` (see "For other mods").

`MessageWriter` and `MessageReader` are little-endian binary with byte, bool, uint16, int32, uint32, int64, float, GUID, string (length-prefixed UTF-8, at most 4096 bytes) and raw bytes. Writers and readers come from `MessagePool`, so a steady-state send or receive allocates nothing (a test pins this; reading a string necessarily creates the string). Every read is bounds checked and throws `MessageFormatException` on a short or damaged message; the session drops such packets. The pool is per thread.

## Big payloads (blobs)

A reliable packet is at most 64 KB; the host's world is megabytes. `session.Blobs` (`BlobTransfer`, in `Blobs/`) sends a `byte[]` of any size to one player and hands it to a listener on the other side. It is the idea of the reference's `NetBlob` (compress, chunk, CRC, acks, progress) reworked for bytes instead of base64 text, per-player instead of Steam ids, and owned by the session so it dies with it.

```csharp
// Receiving: one listener per blob channel (blob channels are their own numbering, not message channels;
// mods use a name instead: session.Blobs.Register("mymod.world", ...) and Send(player, "mymod.world", ...)).
Track(session.Blobs.Register(WorldChannel,
    (sender, data, blob) => LoadWorld(data),                       // complete and CRC-checked; data is yours
    blob => ShowProgress(blob.BytesDone, blob.BytesTotal),         // optional
    blob => ShowError(blob.Failure, blob.Message)));               // optional, any failure

// Sending (host to a client here; a client can send to the host or, relayed by the host, to another client):
BlobStatus send = session.Blobs.Send(player, WorldChannel, bytes, "world",
    progress: s => bar.Value = s.Progress,
    finished: s => { if (!s.Succeeded) Say(s.Message); },
    timeoutMs: 120000);                                            // optional overall limit
send.Cancel();                                                     // either side can cancel
```

`Send` returns at once. The `BlobStatus` it returns is updated in place (`State`, `BytesDone`, `BytesTotal`, `WireBytes`, `Progress`, `Failure`, `Message`) and is the same kind of object the receiver's callbacks get, with `Direction` and `Peer` telling which end you are. `bytes` must not change until the transfer finished. If it can't start (not in the session, over the size limit, too many transfers) the status comes back already `Failed` and `finished` has run.

**How it works.** The data is deflated on a worker thread (sent as it is when that doesn't make it smaller), together with a CRC-32 of the original. An `Open` message says the channel (a number, or a name's key), size, chunk size, window, CRC and a label; the receiver checks it (is anyone listening on that channel, size limit, how many transfers and how much memory it already holds, in all and from that player) and answers with an ack of 0 chunks or an abort. Then the sender keeps up to `WindowChunks` (16) chunks of `ChunkBytes` (16 KB) unacknowledged; the receiver acknowledges every `AckEveryChunks` (4) and at the end, so a slow receiver is never handed more than about 256 KB at a time (its acks also wait behind its own receive budget). When the last chunk is in, the receiver inflates and CRC-checks on a worker thread, replies `Done` or aborts with `Corrupt`, and calls the listener. Chunks are plain reliable messages on `ChannelIds.Blob` (2), which the session owns; the host relays them between clients like any other message.

**Failures.** Every end state other than success is a `BlobFailure` plus a sentence in `Message`, delivered to both sides where they can still be reached: `Cancelled`, `TimedOut` (nothing acknowledged for `StallTimeoutMs`, 30 s, or the `timeoutMs` passed to `Send`), `PeerLeft` (the other player left; raised on the host from `PlayerLeft`), `SessionEnded` (this game left or the connection to the host dropped), `NoHandler` (nobody listens on that channel there, or the listener was disposed), `TooLarge`, `Busy` (too many transfers or too much memory in flight), `Corrupt` (did not unpack, wrong size or CRC), `Protocol`, `NotConnected`, `Internal`. A failure on one transfer never affects another.

**Limits** are `SessionOptions.Blobs` (`BlobOptions`): `MaxBlobBytes` 32 MB, `ChunkBytes`, `WindowChunks`, `AckEveryChunks`, `MaxOutgoing` 16, `MaxIncoming` 16, `MaxIncomingBytes` 96 MB, `MaxIncomingBytesPerPlayer` 64 MB (so one sender can't keep everyone else's blobs out), `StallTimeoutMs`, `Compression` (Fastest). They are read when a transfer starts or an offer arrives. Several transfers can run at once, each with its own id (the sender's counter; the receiver keys by sender and id). Everything is driven by `Poll()`; callbacks run there, never on the worker threads. Chunk handling reuses the pooled writers and reader, so steady state allocates about 70 bytes per chunk on both ends together (a test pins it under 1 KB per 16 KB chunk); each transfer allocates its compressed array and, on the receiver, the result, which is what the data costs. A transfer does not survive a hot reload of the session.

## Flood control

Two limits keep one peer from wrecking the frame or the memory, both in `SessionOptions`:

- **Receive budget.** `MaxMessagesPerPoll` (default 128; 0 for no limit) is how many packets from one peer a `Poll()` handles. A packet over the budget is copied into a pooled buffer and waits in that peer's queue; the next `Poll()` works the queue off first, oldest first, and while a peer has anything queued its new packets go behind it, so order never changes. Budgets are per peer: a burst from one player doesn't delay another's messages. When a peer disconnects, what it had queued is handled first, so a last message before leaving still arrives. `MaxQueuedMessages` (8192) bounds a queue: a peer that gets past it is disconnected with the text "Too many messages were waiting to be handled" (unreliable packets over the bound are just dropped). `session.ReceivesDeferred` and `ReceivesWaiting` show how often the budget bit. The reference's version is 4 packets per peer per frame with a queue of 1024 that drops the overflow; reliable messages can't be dropped here, hence the disconnect.
- **Send queue limit.** `MaxSendQueue` (2048; 0 for no limit) is how many reliable packets may be waiting to go out to one peer (`ITransport.QueuedPackets`, counted in network packets, so a 16 KB chunk is about a dozen). Over it, `Broadcast`/`SendTo`/`SendToHost` return false for that peer and `session.SendRefused(player, text)` is raised once per backed-up spell with a sentence such as "Can't send to Sam: 2048 packets are already waiting to go out (limit 2048)...". `session.SendsRefused` counts every refusal. Blob chunks check for room before sending and simply wait; their small control messages are not limited, so an ack is never lost to a full queue.

## Session, handshake and roster

The topology is a star: clients only talk to the host, and the host passes on what the others need. A client's `Broadcast` goes to the host, which delivers it itself and relays it to every other client with the real sender's slot filled in. That keeps direct IP simple (clients never need to reach each other) and matches the reference's full-mesh assumption well enough; guest-to-guest latency is host-to-guest plus guest-to-host.

**Packets** start with a kind byte:

| Kind | Direction | Content |
|---|---|---|
| `Hello` | client to host | protocol version (int), game version, game build, player GUID, name, mod set |
| `Welcome` | host to client | protocol version, the client's slot, the roster |
| `Roster` | host to clients | the full roster, sent whenever it changes |
| `Data` / `DataUnreliable` | both | channel, slot, payload. From a client the slot is the target (a slot, or 255 for "everyone else"); from the host it is the sender |

**Handshake.** After the transport connects, the client sends `Hello`. The host checks, in this order: protocol version (read first, before anything else in the packet, so a future format change still gets a clean refusal), game version (`Application.version`), game build (the first 16 hex digits of the game code's hash, `Loader.GameCodeHash`: one version from a beta branch, another store or a patched file is refused, unless one side doesn't know its build), mod set, duplicate player GUID, free slot. Any failure sends the client away with a `RejectReason` and a sentence in the disconnect text (`"<code>:<message>"`, see `RejectText`), so the joiner's `SessionEnd` says, for example, `Your mods don't match the host's. You are missing: X. Different versions: Y (host 0.2.0, you 0.1.0).` A connection that sends no valid Hello within 5 s is dropped; a client gives up after 10 s without a `Welcome`. The host accepts a few more transport connections than it has slots, so a joiner to a full game gets "The game is full." instead of a bare refusal.

**Mod set.** The mod builds the set from the loaded mods (`ModRegistry`), by id and version, minus the ids in `IgnoredMods` of `config.json` (default `DevBridge` and `ScriptRunner`, which don't change the game for anyone else). Host and client sets must be equal.

**Slots and identity.** The host is slot 0; others get the lowest free slot, which is reused after someone leaves. A slot is only valid for one session. The persistent identity is the player GUID, generated once and stored in `Mods/ObenCoop/identity.json` (`identity-2.json` for a second test instance, otherwise both copies would be "the same player" and the host would refuse the second). Anything that must outlive a session (a guest's inventory, a prison record, a world folder) is keyed by that GUID, not by slot or peer id. This settles the "identity" risk from section 8.2 of the research: Steam ids are not needed. Names are cut to 24 characters by the host, and a duplicate gets a number (`Sam 2`).

**Roster.** `session.Roster` lists players by slot (`Players`, `Local`, `Host`, `Find(slot)`, `Find(guid)`) and is the same on every game. Events: `Started` (hosting, or the host accepted the join), `PlayerJoined`, `PlayerLeft(player, reason)`, `Ended(SessionEnd)`. A joining client gets `PlayerJoined` once for each player already there. `SessionEnd.Kind` is `Left`, `Rejected` (with the `RejectReason`), `ConnectFailed` or `ConnectionLost`. If the host goes away the session ends; there is no host migration.

## Presence

`CoopPresence` (in `Presence/`, adapted from the reference framework's `Presence`) shares where every player is. It is the data layer that puppets will read; it draws nothing itself.

```csharp
var presence = new CoopPresence(session);                      // registers ChannelIds.Presence
presence.Publish("Interior Player Tenement", position, yaw, velocity, PresenceFlags.Moving);   // every frame
presence.Tick();                                                // every frame, after session.Poll()
foreach (var remote in presence.Remotes)
    if (presence.TryGetPose(remote.Player, out var pose)) Draw(remote.Player, pose.Area, pose.Position, pose.Yaw);
presence.Appeared += r => ...; presence.Vanished += r => ...; presence.AreaChanged += r => ...;
```

- **What a sample holds:** a sequence number, the sender's clock (ms), the area (the scene name, 1 to 64 characters), position (3 floats), yaw (2 bytes), velocity (3 x 2 bytes, cm/s) and flags (`Moving`, `Walking`, `Crouching`, `Sitting`, `Swimming`, `Sleeping`, `Teleported`). About 30 bytes plus the area name; sent unreliable, relayed by the host.
- **Rate:** 10 a second while anything changes (move over 2 cm, turn over 0.5 degrees, other flags or area), once a second while still, and at once when someone joins. When `Publish` hasn't been called for 1 s (main menu, loading) the game sends a reliable "withdrawn" and stops; the others drop it at once. A remote with no sample for 5 s is dropped too. `PresenceOptions` holds all of these.
- **Interpolation:** each remote keeps its last 16 samples on the sender's clock. The gap between the clocks is the smallest delay seen so far (creeping up 1 ms per sample so a slower connection is followed). `TryGetPose` shows the player 200 ms behind the newest sample (two send intervals, so one lost packet doesn't show), interpolated between the two samples around that moment (yaw the short way round). When samples stop it carries a moving player forward along the velocity for at most 250 ms. A move over 8 m between samples is flagged `Teleported` and an area change counts the same: the view snaps instead of sliding through walls or across levels.
- **Robustness:** late and repeated samples are dropped by sequence; NaN, infinite and far-out values (over 100000 from the origin) are rejected; a sender whose clock jumps over 10 s (its mod reloaded, or it was quiet for a long time) starts a fresh track. Steady-state sending and receiving allocate nothing (the area string is only made when the name changes).

In the mod, `Presence/PresencePublisher.cs` publishes the local player every frame while `WorldState.IsReady` and no level change is under way: the scene name (read only when the scene changes), the body's position and yaw, a velocity measured over at least 50 ms, and the flags from `PlayerState`. `SharePresence` in `config.json` (or `coop presence on|off`) switches it.

## Lobbies

`CoopLobby` (in `Lobby/`) is the lobby around a session: how players find a game, check they can join it, and get in. It is one state machine for every way of playing (adapted from the reference's `Lobby`); a backend per kind does the platform's part.

```csharp
var lobby = new CoopLobby(session);
lobby.AddBackend(new DirectLobbyBackend(directTransport, clock));     // always
lobby.AddBackend(new MatchmakingBackend(steamLobbies, p2pTransport, clock));  // when Steam is up
lobby.Host(LobbyKind.Direct, "Sam's flat", 27890);   // false + lobby.Error when it can't start
lobby.Join("7K2Q-9MXA-B4C1");                         // or "192.168.1.20", "host:27891", "steam:109775241234567890"
lobby.Browser.Scan(LobbyKind.Direct);                 // results in lobby.Browser.Results, then Finished
LobbyInvite invite = lobby.Invite();                  // invite.Code, invite.OverlayOpened
lobby.Leave();
lobby.Poll();                                         // every frame, after session.Poll()
```

- **Status:** `Offline`, `Pending` (creating, or looking up and connecting), `Hosting`, `Joined`, with `StatusChanged`, `Error` (a sentence) and `JoinRequested` (an invite accepted outside the game). A request still Pending after 20 s is given up. A generation counter makes answers to an abandoned request harmless (a Steam lobby created after giving up is left at once).
- **What a lobby advertises:** `LobbyInfo` (format byte, protocol version first, then a random lobby id, game version, game build, the mod set's fingerprint and count, name, host name, players, max players, open flag, port). The fingerprint is a 32-bit FNV-1a over the sorted mod ids and versions (`ModSet.Fingerprint`).
- **Version checks before connecting** (`LobbyCheck`, same order as the handshake): a different protocol, game version or game build, a full or closed game is refused with a sentence and no connection is made. Different mods are only marked: the join goes ahead so the host's handshake can name exactly which mods are missing or different. The handshake stays the real gate.
- **Join codes** (`JoinCode`): Crockford base32 with an 8-bit CRC, grouped by four. A direct code (12 characters, `7K2Q-9MXA-B4C1`) holds an IPv4 address and port; a Steam code (16 characters) holds a lobby id. Case, dashes and spaces don't matter, O/I/L read as 0/1/1, and every single wrong character is caught (a typo is reported as one). `LobbyAddress.TryParse` reads codes, `ip`, `ip:port`, host names, `steam:<id>` and bare Steam ids.
- **Asking again** for what is open already is fine: `Host` of the kind being hosted, or `Join` of the lobby this game is in, returns true and changes nothing.
- **Lists** (`LobbyBrowser`): one scan at a time, results with a verdict and a mark (`different mods`, `other game version`, `other game build`, `full`...), deduplicated by lobby id, the host's own lobby left out.
- **Hot reload:** `SaveState`/`RestoreState` carry an open lobby (hosting or joined) next to the session; the backend picks it up again (`Resume`).

### Direct play

`DirectLobbyBackend`. Hosting sets the transport's `Responder`, so the game answers lobby queries on its own UDP port with its `LobbyInfo` (`LobbyWire`: `OBCQ` query, `OBCR` reply with the info). Joining asks the address first (2.5 s); without an answer it connects anyway and lets the handshake decide. A scan (`LobbyScanner`, its own socket, open only while it runs) broadcasts the query on the local network and also sends it to 127.0.0.1, on `Port` and the next `ScanPorts - 1` ports, for 1.5 s; the loopback query is what finds a second copy of the game on the same machine even where broadcasts don't loop back. `coop lobby list <address>` asks one host directly, which also works over the internet. Codes carry `PublicAddress` from `config.json` (or `coop invite <ip>`) when set, otherwise this machine's address on its network (LiteNetLib's `NetUtils.GetLocalIp`; under Proton here it gives the LAN address, 192.168.x.x); such a code works on the same network and on the same machine.

### Steam

Optional and never the default. Steam play needs the game's own Steam to be running: the framework's `GameSteam.IsReady` reads the game's `SteamManager` (`s_instance` and `m_bInitialized`, private, through cached field refs, at most once a second until it says yes). It does **not** use the public `SteamManager.Initialized`, which creates a manager when none exists and so starts Steam itself (or throws if Steam was started once). Until it says yes nothing touches Steamworks; if anything fails to start, direct play carries on alone and the status says so.

The logic is in the library, behind two interfaces shaped after Steamworks so it is tested without Steam:

- `IP2PNetwork` (legacy `ISteamNetworking`) under `P2PTransport` (see Transport). In the mod, `Steam/SteamP2P.cs` implements it on channel 2789.
- `IMatchmaking` (`ISteamMatchmaking` plus friends and rich presence) under `MatchmakingBackend`. In the mod, `Steam/SteamLobbies.cs` implements it.

Hosting creates a lobby (`SteamLobbyType`: friends only by default, `public` or `private`), writes `game=obencoop` and the `LobbyInfo` (base64) into its data, sets rich presence `connect = "+connect_lobby <id>"` so friends can "Join game", and lets only lobby members connect over P2P; members who leave the Steam lobby are dropped. Joining enters the lobby, waits up to 3 s for its data, refuses lobbies that aren't ObenCoop (and leaves them), runs the version checks, then connects to the owner's Steam id. Lists show the lobbies friends playing the same Steam app are in plus public ObenCoop lobbies. `coop invite` opens the overlay's invite dialog. Accepted invites, "Join game" clicks and a `+connect_lobby <id>` on the game's command line (Steam starts the game that way from an invite) come out as `JoinRequested`; with `JoinInvites` on, the mod joins at once.

**Which Steam app.** `SteamApp` in `config.json` (or the Settings page) picks the Steam app the game runs as: `obenseuer` (the default), `spacewar` (Valve's free test app 480, which every Steam account has), `toxikk` (324810, a free game, for when Spacewar's lobbies misbehave) or any app id as a number. With anything but Obenseuer, copies from GOG and other stores can play over Steam (with the Steam client running), and two test accounts don't need two copies of the game. Lobbies and P2P sessions only meet within one app, so everyone in a game needs the same choice; public lobbies are told apart from other people's by `game=obencoop`. The switch goes through the framework's `GameSteam.StartAs` in the mod's `OnLoad`, which runs before the game starts Steam in its first scene: it sets `SteamAppId`/`SteamGameId` for the process and skips the game's `RestartAppIfNecessary(951240)`. That check closes the game at once in a copy that isn't on the player's Steam account (it asks Steam to start Obenseuer instead), so for such a copy another app is the only way to Steam play. A change takes effect at the next start of the game. While the game runs as another app, Steam shows the player in it, and Obenseuer's achievements, stats and play time don't count. `coop status` says "ready as Spacewar (480)" or "ready as TOXIKK (324810)".

**On Linux the game has to reach the Steam client.** Proton only connects a game to the running Steam client when it knows where Steam is installed (`STEAM_COMPAT_CLIENT_INSTALL_PATH`). Steam sets it for what it starts; umu (which Heroic and `oben launch` use) deliberately leaves it empty, so a copy started from Heroic can't use Steam at all, on any app. Add the game to Steam as a non-Steam game and start it from there, or start Proton yourself with the variable set:

```sh
cd <game folder>
STEAM_COMPAT_CLIENT_INSTALL_PATH="$HOME/.local/share/Steam" STEAM_COMPAT_DATA_PATH=<prefix> WINEPREFIX=<prefix> \
  "$HOME/.local/share/Steam/steamapps/common/Proton - Experimental/proton" waitforexitandrun ./Obenseuer.exe
```

(`<prefix>` is the Wine prefix with a `pfx` link to itself, as umu makes them.) The overlay isn't there either way outside a Steam launch, so *Invite friends* can't open it; join codes work.

This install's `steam_api64.dll` is replaced (an emulator answers with a fixed id), so Steam play could not be tried here; two copies on one machine would also share that id. The fake Steam in the tests (`FakeSteam.cs`) runs the whole flow instead: create, list, join, refusals, invites, members leaving, the host leaving, reloads.

## The mod

Files in `mods/ObenCoop/`:

| File | Job |
|---|---|
| `CoopMod.cs` | owns the direct transport, session, lobby (direct backend), Steam support, presence and chat; pumps them in `OnUpdate` (session, lobby, Steam start, presence publisher, presence tick); registers the console command |
| `CoopSettings.cs` | `config.json` (below) |
| `Identity/CoopIdentity.cs` | reads or creates the identity file |
| `Sessions/SessionKeeper.cs` | parks and resumes session and lobby across hot reloads, on either transport |
| `Sessions/ModSetBuilder.cs` | builds the mod set from `ModRegistry` |
| `Lobby/LobbyNotices.cs` | joins, leaves, failures and opened lobbies as notices; prints a finished lobby list to the console; joins on accepted invites |
| `Presence/PresencePublisher.cs` | feeds the local player into presence |
| `Steam/SteamP2P.cs`, `SteamLobbies.cs`, `SteamSupport.cs` | the only code in the mod that touches Steamworks; starts once the framework's `GameSteam` says the game's Steam is up |
| `Chat/CoopChat.cs`, `CoopNotices.cs` | chat on `ChannelIds.Chat`; shows text as a game notification in a level and in the console otherwise |
| `Commands/CoopCommands.cs`, `LobbyCommands.cs`, `PresenceCommands.cs`, `CoopStatus.cs`, `LobbyText.cs` | the `coop` console command |
| `Bridge/CoopMod.Bridge.cs` | `BridgeStatus`, `BridgePresence`, `BridgeLobbies`: JSON for the dev bridge's `coop.*` commands |
| `Ui/CoopMenu.cs`, `CoopPage.cs`, `LobbyListPart.cs`, `PlayerListPart.cs` | Co-op on the main menu and in the pause menu (ObenUIKit's `MainMenuButtons` and `PauseMenuButtons`): the co-op window (below) |
| `Ui/HiddenText.cs` | streamer mode: join codes, IP addresses and Steam ids in a text become stars |
| `Api/Coop.cs` | the entry point for other mods (below) |

### The co-op window

Co-op on the main menu or in the pause menu opens it. It always shows the current state (it builds itself again when the lobby, the roster or a search changes):

- **Not in a game:** *Host a game* (a name, direct play or Steam, and a line on what that means: the port to forward, or the Steam app), *Join a game* (a join code, an address or `steam:<id>`), *Find games* (search the network and this PC, or Steam; the list shows name, players, ping, and a mark such as "other game build" for games that can't be joined; picking one joins it).
- **In a game:** the join code and address, *Copy join code* (to the clipboard), *Invite friends* for a Steam lobby (the overlay's dialog), the players with who hosts, who is you and each one's ping, and *Leave*.
- A line under the state says what the last button did, until the lobby moves on.

**Streamer mode.** `HideJoinCodes` (on by default) shows join codes, IP addresses and Steam ids as stars everywhere co-op prints them: the window, notices in a level, the console's answers and the log lines it mirrors (`****-****-**** (***.***.***.***:27890)`; ports stay). *Copy join code* still copies the real code, and *Show* next to it reveals codes and addresses in the window until it closes; the join field shows stars while typing too. The bridge's `coop.*` JSON is a developer tool and keeps them.

### For other mods

`Coop` (namespace `ObenCoop`) is how another mod plays along: add `"ObenCoop"` to its dependencies in mod.json and reference `ObenCoop.dll` and `ObenCoop.Net.dll`.

```csharp
Coop.Listen(this, "mymod.doors", (sender, reader) => OpenDoor(reader.ReadString()));
Coop.ListenBlobs(this, "mymod.world", (sender, data, blob) => LoadWorld(data));
Coop.OnPlayerJoined(this, player => Log.Info(player.Name + " joined"));

var w = MessagePool.RentWriter();
w.WriteString("Kitchen door");
Coop.Broadcast("mymod.doors", w);                         // also SendTo(player, ...) and SendToHost(...)
MessagePool.Return(w);
Coop.SendBlob(Coop.Host, "mymod.world", bytes, "world");  // a byte array of any size up to 32 MB
```

- **State:** `IsAvailable`, `InGame`, `IsHost`, `Me`, `Host`, `Players`, `PingOf(player)`.
- **Events:** `OnStarted`, `OnPlayerJoined`, `OnPlayerLeft(player, reason)`, `OnEnded(end)`. Each handler runs in its own try/catch; an error is logged against the mod it came from.
- **Messages and blobs** on named channels (see "Named channels" above). Senders return false when not in a game.
- **The rest:** `Coop.Session`, `Coop.Lobby` and `Coop.Presence` are the objects underneath (roster, flood control counters, lobby lists and invites, where the others are).

Everything takes the mod as owner (`this`): subscriptions and listeners go away when the mod unloads. When ObenCoop reloads, the mods that depend on it reload with it and subscribe again. Everything runs on the main thread, from ObenCoop's update.

Settings in `config.json` are re-read each time you host, join or list, so editing it does not need a reload:

| Setting | Default | Meaning |
|---|---|---|
| `PlayerName` | `""` | what others see; empty is `Player` (`Player 2` in a second instance) |
| `Port` | 27890 | UDP port a hosted game listens on, and the first port a lobby list asks |
| `MaxPlayers` | 4 | host included, 2 to 16 |
| `IgnoredMods` | `DevBridge`, `ScriptRunner` | left out of the "same mods" check |
| `LobbyName` | `""` | name in lobby lists; empty is "<name>'s game" |
| `PublicAddress` | `""` | IPv4 put in join codes; empty is this machine's network address |
| `ScanPorts` | 4 | ports a lobby list asks, from `Port` up |
| `Steam` | true | offer Steam lobbies when the game's Steam is running |
| `SteamApp` | `obenseuer` | the Steam app the game runs as: `obenseuer`, `spacewar` (480), `toxikk` (324810) or an app id, for copies from other stores and testing; at the next start |
| `SteamLobbyType` | `friends` | `friends`, `public` or `private` (invite only) |
| `JoinInvites` | true | accepting a Steam invite joins at once |
| `HideJoinCodes` | true | streamer mode: codes, addresses and Steam ids show as stars on screen (above) |
| `SharePresence` | true | share where you are |

### Console commands (F1)

```
coop host [port]                      host a direct-play game (opens a lobby with a join code)
coop join <ip[:port] | code | steam:id>   look the game up, check it, join; the result shows as a message
coop leave                            leave, or stop hosting (also leaves a Steam lobby)
coop status                           state, roster with slots and ping, traffic, lobby, presence, Steam
coop say <text>                       chat; others see it as a notification (in a level) or in their console
coop lobby create [steam] [name...]   host with a lobby name; "steam" makes it a Steam lobby
coop lobby list [steam | <address>]   find games on the network and this machine, Steam friends' and public
                                      lobbies, or ask one address; the numbered list prints to the console
coop lobby join <number | code | address | steam:id>   join from the last list or directly
coop lobby info                       the lobby and Steam state
coop invite [public ip]               the join code (copied to the clipboard); opens Steam's invite dialog
                                      for a Steam lobby
coop presence [on | off]              where the other players are (area, position, distance, flags), or
                                      switch sharing your own
```

Chat text is stripped of `<` and `>` (the game's text is rich text) and cut to 200 characters. Lobby and player names from others lose `<`, `>` and control characters too.

The dev bridge has `coop.status`, `coop.presence` and `coop.lobbies` (JSON of the same, built by `CoopMod.Bridge*`; the bridge calls them by name, so neither mod references the other).

### Why two assemblies and a library

`mod.json` lists `"assemblies": ["ObenCoop.Net.dll", "ObenCoop.dll"]`. `LiteNetLib.dll` also sits in the mod folder but is **not** listed, which makes the loader treat it as a library: resolved by name, loaded once, never reloaded. Both choices serve hot reload:

- A hot reload throws the mod's assemblies away and loads fresh copies, but a live UDP socket cannot be recreated without dropping every connection. LiteNetLib's `NetManager` is never reloaded, so the old copy of the mod can hand it to the new one. `DirectTransport.Detach()` returns the `NetManager`, its listener and the peer limit as an `object[]` (types from the never-reloaded assembly), `Adopt()` takes them back and re-hooks the events. `CoopSession.SaveState()` is plain bytes (state, roster, peer ids) that `RestoreState()` reads. `SessionKeeper` puts both in `ReloadState` on unload and takes them in `OnLoad`. If nothing takes them within 10 s (the mod was switched off, not reloaded) the connection is closed.
- `ObenCoop.Net.dll` is listed so that the networking code itself reloads too; it is the part that will change most. It is listed **first** on purpose: the loader renames each reloaded assembly and rewrites references only to assemblies it has already renamed, so `ObenCoop.dll` has to come after the library it references.

A Steam connection has no socket to keep: Steam holds the sessions and the lobby. `P2PTransport.Detach()` returns who is who as plain arrays and `Adopt()` takes them back; the next copy starts its Steam support at once (the gate says yes straight away) to adopt them. `CoopLobby.SaveState()` carries an open lobby (kind, address, info) and `RestoreState()` hands it back to the backend (`Resume`: the direct backend answers queries again, the Steam one takes its lobby id back). A parcel nobody picks up within 10 s closes its connection and, for Steam, leaves the lobby.

Anything parked must be plain data or a type from a never-reloaded assembly; a type from a reloaded one would not match in the next copy. A change to the wire format between two builds needs a game restart (the handshake's protocol version only protects connections between games).

## Testing

**Unit and loopback tests.** `tests/ObenCoop.Net.Tests` runs the real LiteNetLib over 127.0.0.1, a session on each end:

```sh
dotnet test tests/ObenCoop.Net.Tests
```

It covers the handshake, slots and roster, rejection by game version, game build, mod set, protocol version, full game and duplicate GUID, a silent connection being dropped, reliable ordering both ways, relay between clients, directed sends, leave and host shutdown, a hot reload handover of a host and a client session, the codec and the zero-allocation pool, named channels for messages and blobs (relayed, unknown names dropped quietly, one handler per name), and the blob and flood control behaviour: a 5 MB blob intact, a small blob not held up behind a big one or by a silent player, one sender's share of the receive memory, CRC and damaged-chunk detection, a disconnect mid-transfer failing both sides, concurrent transfers, cancel, timeouts and refusals, the receive budget spreading a burst over polls in order, the overflow disconnect and the send queue limit. No game is needed.

It also covers presence (codec, interpolation, late samples, extrapolation, jumps and level changes, restarts, idle rate, withdraw, timeouts, relay to a third player), lobbies for direct play (a scan finding a game on this machine, joining by code, refusing a different game version before connecting, the host leaving, nobody answering, a lobby carried over a reload, join codes catching every single wrong character), and Steam on an in-memory fake (`FakeSteam.cs`): the session over P2P with relay, refusals with their reason, timeouts, Steam failures, pings, handover; lobby create, list, join by code, version and foreign-lobby refusal, invites, non-members turned away, members leaving the lobby, the host leaving, reloads.

**Two copies of the game on one machine.** `oben install --multi-instance` once, then `oben launch --instance 2` starts a second copy with `OBEN_INSTANCE=2`: its own loader and player logs (`latest-2.log`, `player-2.log`), the dev bridge one port up, its own `identity-2.json` and the name `Player 2`. Everything below works in the F1 console of each copy, or through the bridge: `oben call console.exec command="coop status"` for the first copy, with `--instance 2` for the second.

1. Copy 1: `coop lobby create Test` (or `coop host`). It says "Lobby open: Test. Join code XXXX-XXXX-XXXX (192.168.x.x:27890)".
2. Copy 2: `coop lobby list`. After about 1.5 s the console lists "1. Test 1/4 host Player ..." (`oben call coop.lobbies --instance 2` shows it as JSON).
3. Copy 2: `coop lobby join 1`. Both show the join; `coop status` in both shows two players and the lobby (Hosting / Joined). `coop say hello` in one shows up in the other.
4. Copy 2: `coop leave`, then `coop join <the code from step 1>` (or `coop invite` in copy 1 prints it). Joined again.
5. Presence: load a level in both (`oben call console.exec command="new_scene Interior Player Tenement"`, and the same with `--instance 2`). After the world is ready, `coop presence` in copy 2 lists Player in Interior Player Tenement with a position and distance. `oben call player.get` in copy 1, then `oben call player.teleport x=<x+3> y=<y> z=<z>`; within a second `oben call coop.presence --instance 2` shows the new position under `latest`. A teleport of over 8 m shows `Teleported` in `flags`. `coop presence off` in copy 1 removes it from copy 2's list at once; `coop presence on` brings it back.
6. A dead address: in copy 2 (offline) `coop join 127.0.0.1:27999`. Nobody answers the lookup (2.5 s), the connect is tried anyway and fails; within about 10 s a notice says so and `coop lobby info` shows it as the last problem. (A real version or mod mismatch needs two different installs, since both copies share the game and the `Mods` folder; the unit tests cover those.)
7. Hot reload while connected: rebuild the mod (`OBENSEUER_DIR=... dotnet build mods/ObenCoop/ObenCoop.csproj -c Release`, auto reload on). `coop status` still shows both players and the lobby, the log says "Carried the session over a reload" and "Carried the lobby over a reload", and `coop lobby list` in copy 2 still finds copy 1's game.
8. The co-op window (Co-op in the main or pause menu) does the same with buttons: host, join with a code, *Find games* and pick one, *Copy join code*, *Show*, *Leave*. With `HideJoinCodes` on, codes and addresses are stars in it and in every notice.

**The `single-instance=` entry.** The game's `Obenseuer_Data/boot.config` contains the line `single-instance=` (empty value). Unity's single-instance switch may make a second launch exit at once or just focus the first window. This has not been verified here. If the second copy does not stay up: close both, make a backup of `boot.config`, delete that line, and launch again. If it still refuses, the alternative is a second copy of the whole game folder run as a separate install. (The TODO item "Second game instance on one machine" is exactly this check.)

Both copies share the `Mods` folder, so they share `config.json` (port, name) and the hot reload watcher reloads both when a DLL changes. Only one of them can host on a given port; a second hosting copy needs `coop host <other port>` (lists ask `Port` and the next 3 ports, so it is still found).

## Not done yet

- Steam play between two real Steam accounts is untested. On this machine (one account, a copy that isn't on it) the real `steam_api64.dll` started Steam as Spacewar and as TOXIKK and opened a Steam lobby on each, and Obenseuer closed itself as described above; the install's own `steam_api64.dll` is an emulator that can't create lobbies at all. Everything above the two thin Steamworks wrappers is tested on the fake. It uses legacy P2P (deprecated by Valve but in the game's Steamworks.NET); moving to `SteamNetworkingSockets` means a new `IP2PNetwork`, nothing else.
- No NAT help for direct play. Forward the UDP port, use a VPN, or play on a LAN; or use Steam.
- No request/ack helper (`Shared.NextReqId` in the reference), no timers for periodic state (`HST` announces, digests).
- Blobs are not resumable and don't survive a hot reload; there is no per-message rate window for features (the reference's `BuildHost` and friends), only the per-peer budget and queue limits.
- The host is the only authority and there is no host migration (a Steam lobby whose owner leaves ends the session, as in the reference).

## Plan: start from the reference mod, replace it piece by piece

The reference co-op mod (`example-mp/`, [NomadsReach/Obenseuer-Coop](https://github.com/NomadsReach/Obenseuer-Coop), MIT) already shares nearly the whole game. Its author doesn't plan much more work on it and is happy for us to build on it. So instead of porting slice by slice from scratch, ObenCoop starts from that mod and replaces its insides step by step, keeping it playable after every step. Its MIT notice stays with the code (THIRD-PARTY-NOTICES.md, plus the header in each adapted file).

The reference is about 46,800 lines in 176 files. It is a BepInEx plugin on top of ObenseuerCore (98 files call into it), with about 180 manual Harmony targets written for game 0.5.08, and all its networking goes through `Session.cs` (about 140 send sites) over Steam P2P.

1. **Bring it in as it is.** Copy the sources into `mods/ObenCoop/` (a `Legacy/` folder or similar), each file with the adapted-from header, as one commit with nothing changed, so later diffs show our work.
2. **Make it load on our stack.**
   - `BaseUnityPlugin` becomes `ObenMod`, `ConfigEntry` becomes `LoadConfig<T>`, `ManualLogSource` becomes `ModLogger`, and `CorePatcher` becomes the mod's own Harmony through patch areas (`ModPatches.ApplyClasses`), so a broken area switches off alone.
   - Calls into ObenseuerCore go to ObenFramework; `docs/research/framework-reference.md` maps the modules. Where a call has no equivalent yet, add it to the framework (it belongs there) rather than to the mod.
3. **Put it on our network layer.** `Session.cs` keeps its send methods, but they go through ObenCoop.Net (`CoopSession`, channels, blobs, flood control, lobbies and presence). Direct IP then works too, not only Steam, and the Steam transport is the same code path.
4. **Check against 0.5.13.** Run `oben check` on the result and fix every hook that moved since 0.5.08.
5. **Replace subsystems one at a time**, each a self-contained commit tested with two copies:
   - **Sync on framework events:** doors, containers, floor items, building, crafting, shops, quests and the rest listen to the framework's events (stable ids, `ChangeSource` so nothing echoes) instead of their own hand-written patches.
   - **Cleanup:** split files over 300 lines, undo everything on unload (hot reload), and remove per-frame allocations and reflection.
   - **Fix the reference's limits** (see ideas.md, co-op): thunder owned by the host, exhausted guests passing out, the clock after leaving, furniture without a known id, mod furniture, mod recipes.
6. **Our own additions on top:** the lobby page in ObenUIKit, direct-IP join codes and invites, presence smoothing, and so on.

The old slice plan below is kept for its notes on order and risk; the reference already covers every slice in it.

## Planned slices

From section 8 of the research, in order:

1. **Session plus players.** Both players load the same save by hand. Presence at 10 Hz on an unreliable channel (done), puppets for other players (body, pose, sitting, appearance), the roster shown in a window (ObenUIKit). Chat gets a proper UI. Delivers: you see each other walk, sit and look.
2. **World clock and weather**, then the sleep vote. Low risk, visible result.
3. **Join ticket.** Blob transfer (`session.Blobs`, done) of the host's save, shipped to a joining player, guest save redirection and the guest gate. Hardest single slice; needs fixture saves and self checks.
4. **Shared world state**: doors and lights, floor items and coins, containers, building. Requests go to the host, which validates and broadcasts the result; state is idempotent ("the state of X is Y") with periodic digests so late joiners and lost packets heal.
5. **Stations and NPC features**: crafting, liquids, power, shops, vending, dialogue, quests.
6. **The rest**: trains, elevators, sauna, slots, NPC and animal positions, combat, harvest, prison, map markers, players panel.
7. **Last or never**: free roam (players in different levels, authority per scene). Decide early whether version 1 forces everyone into the host's scene; that removes the whole authority-per-scene layer. Keep an `Authority` accessor so it can be added later.

The **Steam transport** is in (lobbies, P2P, invites); it needed no change above the transport and lobby layers.
