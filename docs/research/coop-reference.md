# Reference co-op mod: analysis for the port

Source reviewed: `example-mp/` (Obenseuer Co-op 0.12.2, `Plugin.cs:20`, MIT, NomadsReach) and the framework it sits on, `example-framework/` (ObenseuerCore 0.12.1, also MIT). All paths below are relative to `example-mp/` unless they start with `example-framework/`. Game decompile is in `../decompiled/Assembly-CSharp/`; I spot-checked that a dozen hook targets exist there (Storage.EnterExternally, SaveController.LoadGameWithMigration, Door.OnOpen, BuildingSystem.Place, Elevator.TogglePower, Crime.GoToPrison, Prison.ImprisonPlayer and so on) and they do. I did not run anything.

The most important finding first: the mod contains no networking code of its own. Every packet, lobby call and Steam call goes through ObenseuerCore (`Net`, `Lobby`, `Presence`, `NetBlob`, `Steam`). The mod touches those only through one facade, `Session.cs` (about 140 call sites of `Session.Send`, 112 of `Session.Peers`, 112 of `Session.IsHost`). That facade is the seam we replace.

Contents: 1 architecture, 2 transport, 3 framework API surface, 4 BepInEx usage, 5 Harmony table, 6 features and line counts, 7 big files and code quality, 8 port order and risks.

---

## 1. Architecture

### 1.1 Size and build shape

- 176 `.cs` files, 46,757 lines, plus `web/lobby/index.html` (624) and `web/lobby/notice.html` (171).
- `ObenseuerCoop.csproj`: net472, `NoStdLib` with `mscorlib`/`netstandard`/`System*` taken from the game's `Managed` folder, `TreatWarningsAsErrors`. `Directory.Build.props:21` sets `LangVersion 5`, so the whole code base is C# 5 (no `?.`, no string interpolation, no expression-bodied members, no `out var`). That is why you see `out` temporaries and `string.Format` everywhere. We can raise this; nothing in the logic depends on it.
- References: Unity modules, `Assembly-CSharp`, `Assembly-CSharp-firstpass` (Steamworks.NET, FullSerializer, PixelCrushers dialogue), `AstarPathfindingProject`, `Newtonsoft.Json`, `Unity.TextMeshPro`, `0Harmony`, `BepInEx.*`, `ObenseuerCore`, `ObenseuerCore.WebUi`.
- The mod compiles against the original `Assembly-CSharp.dll`, not the publicized one. Private game members are reached with `AccessTools.Field/Method` or `GetField` (about 215 `AccessTools`/`Patch(` lines across the tree). The framework compiles against a publicized copy that its preloader makes (`example-framework/src/ObenseuerCore.csproj`, `PublicizedAssembly`; `example-framework/preloader/ObenseuerCore.Preloader.cs`).

### 1.2 Pattern: Host / Guest / Wire / Hooks / Scan / Sync

Every feature repeats the same set of files. Names are the file stems; not every feature has every piece.

| Piece | Role | Example |
|---|---|---|
| `XWire` | Static message builders (`string.Format` joining fields with a pipe character), a `XMsg` class with all possible fields, a `TryParse(string, out XMsg)` that range-checks every field | `ContainerWire.cs:26` (channel `cont`), `ContainerWire.cs:359` (`TryParse`) |
| `XHooks` | Harmony install, `Ready` flag, `SelfCheck()`, thin prefix/postfix methods that call into Host or Guest | `ContainerHooks.cs:13-30` |
| `XHost` | Authority side: validates requests, owns the lock/state table, broadcasts results | `ContainerHost.cs:127` (`Handle`) |
| `XGuest` | Client side: blocks the vanilla action in a prefix, sends a request, waits for ack/deny, applies the host's answer | `ContainerGuest.cs` |
| `XScan` / `XSync` | Periodic digest or manifest so late joiners and missed packets reconcile | `ContainerScan.cs:9-25` (digest every 20 s) |
| `XState` / `XStore` | Persistence or capture of state (container JSON via `SaveData.Capture/Apply`, prison store files, guest profile files) | `StationState.cs`, `PrisonStore.cs`, `ProfileStore.cs` |

Simple features (pose, sit, look, npc, animal, door, dynamite, power, liquid, harvest, hit) are a single `XSync.cs` with `Channel`, `Tick`, `Handle`.

### 1.3 Startup and wiring

`Plugin : BaseUnityPlugin` (`Plugin.cs:11-17`). `Awake` (`Plugin.cs:41-207`) does everything in order and by hand:

1. `Config.Bind` for four settings and two `Keybinds.Register` (F8 host, F7 leave) (`Plugin.cs:44-49`).
2. About 60 `XHooks.Install()` / `XHost.Init()` / `XGuest.Init()` calls (`Plugin.cs:52-122`). Hooks that return `bool` log an error and leave the feature off if they fail (for example `Plugin.cs:72-73`).
3. Optional self checks behind `[Debug] *SelfCheck` config switches (`Plugin.cs:124-143`).
4. 30 channel registrations with `Session.Register(channel, handler)` (`Plugin.cs:145-173`; `prison` is registered separately in `PrisonStore.cs:49`). A false return is only logged ("channel owned by another mod").
5. A scene-loaded callback fans out to about 25 `OnSceneChanged/Reset` calls (`Plugin.cs:174-204`), and `PlayerLook.Changed` is hooked (`Plugin.cs:205`).

`Update` (`Plugin.cs:214-263`) is one try/catch around about 40 static `Tick()` calls in a fixed order. `OnGUI` draws `CoopUi` (`Plugin.cs:209`). `TickBuild` (`Plugin.cs:325-395`) detects the in-lobby to out-of-lobby edge and calls about 45 `Reset()` methods. There is no registry of "modules"; adding a feature means editing `Plugin.cs` in four places.

### 1.4 Session lifecycle

Lobby and roster come from the framework (`Lobby.Host/Join/Leave`, Steam friends-only lobby, max 4: `Session.cs:19`, `Session.cs:161-199`). On top of that the mod runs its own state machine in `Session*.cs`:

1. Host presses F8 or the main menu panel: `Session.Host()` (`Session.cs:161`) calls `Lobby.Host("obenseuer-coop", Plugin.Version, 4, false)`.
2. Guests join through a Steam invite (`Lobby.JoinRequested` at `Session.cs:47`) or the panel. The framework refuses members whose mod version, framework version, game version or game build differ.
3. While hosting, `SendHost` (`Session.Join.cs:37-46`) sends `HST|inWorld|scene|gameBuild|savesOff|timerScale` on channel `world` every 2 s (`HostInterval`, `Session.cs:13`). Guests treat the host as in-world for 6 s after the last HST (`HostStale`, `Session.cs:14`, `Session.cs:86`).
4. The guest asks to join with `WREQ` (`Session.cs:234`); host answers `WACK|1|reason` (`Session.Join.cs:155`). `JoinBlock()` (`Session.cs:206-217`) lists the reasons a join is refused (host not in world, tutorial, host saves disabled, game build differs, a hook missing).
5. World transfer (the heavy part): `WorldShip.SendTo` (`WorldShip.cs:130`) makes the host autosave (`SaveSlots.SaveNow()`, `WorldShip.cs:145`), packs the save files into a ticket (`TicketManifest` with per-file SHA-256, `WorldShip.cs:82-98`) and ships it as a single `NetBlob` named `join-ticket` (`WorldShip.cs:368`). The guest side (`GuestWorld.Begin`, `GuestWorld.cs:61`) validates the manifest, writes the files under `persistentDataPath/coop/<hostId>_<worldId>` (`GuestWorld.cs:74`), splices the guest's own player globals into the host's `Globals.tnmt` (`GuestSplice.cs`, uses FullSerializer `fsData`), then starts the game's own loader in a coroutine (`GuestWorld.cs:110`, `LoadRoutine` at `:293`). While in the host's world the guest's saves are redirected or blocked (`GuestSaveHooks.cs:25-28`, `GuestGateHooks.cs`).
6. Leaving: host sends `BYE|closed` (`Session.cs:195`) or `BYE` on quit (`SessionHooks.cs`), `BYE|reload` when it loads another save. Guests run `HostGoneTick` (`SessionAlerts.cs:21`): BYE seen, or host missing from the roster for a grace period, then `ReturnToMenu` (`Session.cs:463`) and an alert popup. `Lobby.OwnerChanged` is treated as "host left" (`Session.cs:364-367`). There is no host migration.
7. Reset on lobby exit is by edge detection in `Plugin.TickBuild` (`Plugin.cs:329-390`), not by an event.

### 1.5 Authority model

- Normal case: the host's machine owns the real world; each guest runs a copy of the host's save and every state-changing action goes to the host as a request.
- Complication, "free roam": players may be in different scenes. Authority is per scene, not per session. `FreeRoam.AuthorityOf(scene)` (`FreeRoamHold.cs:40`) returns the host if the host is in that scene, otherwise the first guest that arrived (`HolderOf`, `FreeRoamHold.cs:13`), announced with `HOLD|scene|holder|gen` messages (`FreeRoamHold.cs:SendHold`). `FreeRoam.IAmAuthority` (`FreeRoamHold.cs:35`) is what `Plugin.OnBuild/OnContainer/OnItem/OnElevator` test (`Plugin.cs:402-418`, others). Features that are always host-only (talk, shop, craft, quest, vend, slot, sauna, train) test `Session.IsHost` and `from == Session.HostId` instead.
- Authority hand-off uses a generation counter, a transfer protocol and per-scene checkpoint blobs stored under `coop-scenes` (`FreeRoamHandoff.cs`, `FreeRoamStore.cs:6-30`). A holder lapses after 20 s without HOLD (`FreeRoamHold.cs:HolderLapse`).
- Guests never save into their own slots while in the host's world. The host keeps each guest's inventory and money in a per-player profile (`GuestProfile.cs`, `ProfileStore.cs`, channel `profile`).
- Several cross-checks make the host the source of truth: `SUM`/`DIGEST`/manifest messages (build 10 s, containers 20 s, items 20 s, interactables, crafting 20 s) let a guest notice drift and request a resync.

### 1.6 Messages: definition, serialization, routing, dispatch

- Everything is a `string`. Wire format is `channel|payload` where payload is `|`-separated fields starting with a kind tag (`REQOPEN|7|scene|key`). The framework prepends the channel and UTF-8 encodes (`example-framework/src/Api/Net.cs:44-53`). Floats use `InvariantCulture` and fixed `F2/F3/F4` formats (`SitWire.cs:28`, `BuildWire.cs:PoseFormat`).
- Free text is stripped of `|`, `\r`, `\n` by a per-file `Clean` (16 copies) or by `Net.Plain` in the framework.
- Parsing is defensive: field counts, enum-like tags, numeric ranges (`MaxCoordinate`, `MaxReqId`), length caps. Failure means the message is dropped with a once-per-site log (`CoopLog.Once("Plugin.build", ...)`, `Plugin.cs:402-408`).
- Routing: `Net.Register` keeps one handler per channel string (`NetChannels.cs`). The mod's single handler per feature lives in `Plugin.cs` (`OnBuild`, `OnContainer`, ... lines 402-557); it parses once, then picks Host or Guest by role.
- The `world` channel is a catch-all multiplexed by prefix: `HST`, `BYE`, `WREQ`, `WACK`, `TRAVEL`, `LOAD`, `CLK`, `WEA`, `BED`, `WAKE`, `SPEED`, `FAST`, `RELOAD`, `HOLD`, `GOTO`, `GOERR`, `VOTE`, `CFLUSH` (`Session.cs:274-310`, `WorldSync.cs:Handle`, `FreeRoam.cs`, `Solo.cs:343`).
- Dispatch is on the Unity main thread inside the framework's `Update` pump (`NetPump.Tick` then `Net.Drain`), wrapped by `Events.Raise` so a throwing handler does not kill the pump (`NetChannels.cs:Dispatch`). The mod has no threads and no queues of its own.
- Large data: `NetBlob.Send(peer, modId, name, bytes)` (17 call sites) for join tickets, container JSON, interactable state, profiles, scene checkpoints, prison files. Receivers register on the `NetBlob.Received` event and filter by `modId` and name.

### 1.7 Reliability, ordering, idempotence

- The mod assumes Steam P2P reliable sends are delivered and ordered per peer, and uses unreliable only for streams where the next sample replaces the last: presence/pose/sit/npc/animal/elevator-moving/train-position/sauna-values. Everything that changes state or answers a request is sent reliable.
- Request/response with ids: guests allocate `Shared.NextReqId()` (`Shared.cs:63`), host answers `ACK|reqId|ok|reason` (or `DENY`). Hosts cache the last answer per `(peer, channel)` and `Shared.Admit` (`Shared.cs:98`) resends it instead of re-executing a repeated request id; `Shared.Answer` (`Shared.cs:112`) stores and sends. Used by craft, sauna, slot, train hosts (see `CraftHost.cs:197`, `TrainHost.cs:158`). Other features use their own timeouts: build 10 s (`BuildGuest.cs:10`), craft retry 3 s / timeout 10 s (`CraftGuest.cs:9-10`), sauna and slot answer timeout 10 s, container state 5 s (`ContainerGuest.cs:8`), trade 10 s.
- Idempotent state, not event logs: most host broadcasts are "the state of X is Y", so a lost or repeated message heals at the next digest.
- Receive-side flood control: each message type has a per-peer rate window on the host (`BuildHost.cs:11` and `ItemHost.cs:13`, `ElevatorHost.cs:12`, `CoopChat.cs:21`) and the framework drops packets over 1024 bytes and queues past 4 packets per peer per frame (`Net.cs:Budget`).
- Packet budget: a message including `channel|` must fit in 1024 bytes (`Net.MaxPacketBytes`, `Net.cs:12`). Wire files pre-split to fit (`MaxDigestPacket = 880`, `ContainerScan.cs:11`).

### 1.8 Tick rates

| Stream | Rate | Reliable | Where |
|---|---|---|---|
| Presence (area, position, yaw, moving) | 10 Hz | no | framework `Presence.cs` (`SendInterval`), published from `Plugin.Publish` (`Plugin.cs:559`) |
| Pose (speed, crouch, grounded, vspeed) | 10 Hz | no | `PoseSync.cs:10` |
| Sit | check 10 Hz, resend 1 Hz, stand is reliable | mixed | `SitSync.cs:8-9` |
| NPC positions | 5 Hz | no | `NpcSync.cs:10` |
| Animals | 5 Hz | no | `AnimalSync.cs:11` |
| Trains | 5 Hz | yes/no mixed | `TrainHost.cs:7` |
| Elevators | 10 Hz moving, 0.2 Hz idle | per message | `ElevatorHost.cs:8-9` |
| Clock | 1 Hz | per call | `WorldSync.cs:9` |
| Weather | every 5 s | per call | `WorldSync.cs:10` |
| Host announce `HST` | 2 s | yes | `Session.cs:13` |
| Look | check 2 s, resend 12 s | yes | `AppearanceSync.cs:10-11` |
| Container digest | 20 s | yes | `ContainerScan.cs:9` |
| Item manifest | 20 s | yes | `ItemHostManifest.cs:10` |
| Guest profile flush | 60 s plus on change | yes | `GuestProfile.cs:15` |
| Scene checkpoint (free roam) | 30 s | blob | `FreeRoam.cs:17` |
| Mod list exchange | on join and resend | yes | `CoopUi.cs` (`MOD`/`MODEND`) |

All timers use `Time.unscaledTime`, so they keep running while the game is paused.

---

## 2. Transport

### 2.1 What the mod calls

Nothing in the mod's networking path calls Steamworks directly. It calls these ObenseuerCore members and nothing else (all in `Session.cs`, `Shared.cs`, plus `NetBlob` users listed in 1.6):

`Lobby.Host/Join/Leave/Invite`, `Lobby.Status/Owner/IsHost/Members/MemberName/Error`, `Lobby.MemberJoined/MemberLeft/OwnerChanged/JoinRequested`, `Net.Register/Send/Peers/Me`, `Presence.Publish/Remotes`, `NetBlob.Send/Received/Finished/Progress/Cancel`, `Steam.LocalId/IsReady/OverlayEnabled`.

The mod does use Steamworks.NET directly in three UI spots:
- Avatars: `SteamFriends.RequestUserInformation`, `GetMediumFriendAvatar`, `SteamUtils.GetImageSize/GetImageRGBA` (`CoopAvatars.cs:103-129`, `LobbyAvatars.cs:35-42`).
- Overlay callback: `Callback<GameOverlayActivated_t>` to hide the web page while the Steam overlay is up (`LobbyPage.cs:138-156`).
- The `using Steamworks;` in `LobbyPage.cs:7`.

### 2.2 What the framework does with Steam

Wrapper: Steamworks.NET, the one the game itself ships (`decompiled/Assembly-CSharp-firstpass/Steamworks/`, `SteamManager` in `decompiled/Assembly-CSharp/SteamManager.cs`, app id 951240 at line 67). The framework does not initialize Steam; it waits for the game's `SteamManager` and reads its private `m_bInitialized` by reflection (`example-framework/src/Api/SteamBridge.cs:12,23-31`, `Steam.cs:IsReady`, probed once a second). `SteamManager.Initialized` is public and static; we can use that instead.

Lobby calls (`SteamBridge.cs`):
- `SteamMatchmaking.CreateLobby(k_ELobbyTypeFriendsOnly, max)` with a `CallResult<LobbyCreated_t>`; `JoinLobby` with `CallResult<LobbyEnter_t>`; `LeaveLobby`.
- Roster: `GetNumLobbyMembers`, `GetLobbyMemberByIndex`, `GetLobbyOwner`, `GetLobbyMemberLimit`. The roster is polled every 1 s (`Lobby.cs:RefreshInterval`) and also on `LobbyChatUpdate_t` / `LobbyDataUpdate_t`.
- Metadata used for version gating: lobby data keys `v` (mod version), `core` (framework version), `gv` (Application.version), `gb` (game build); member data `v`, `core`, `gv`, `gb`, `name` (`Lobby.cs:Refresh`, around lines 300-440). A member only becomes a "peer" once its tags match; others are listed in `Members` but not `Peers`.
- Invites: `SteamFriends.ActivateGameOverlayInviteDialog`, rich presence `connect = "+connect_lobby <id>"`, callbacks `GameLobbyJoinRequested_t` and `GameRichPresenceJoinRequested_t`.
- Names: `SteamFriends.GetFriendPersonaName`, `GetPersonaName`.

Data path (`Net.cs`):
- Legacy `ISteamNetworking` P2P, not `ISteamNetworkingSockets` or Messages. `SteamNetworking.SendP2PPacket(steamId, bytes, len, reliable ? k_EP2PSendReliable : k_EP2PSendUnreliable, 7)` (`Net.cs:44-53`). One nChannel (`Transport = 7`, `Net.cs:15`) carries every mod channel; mod channels are just the string prefix.
- Receive: `IsP2PPacketAvailable` / `ReadP2PPacket` loop with a 2 ms time budget per frame and a 4096-byte buffer (`Net.cs:Drain`). 4 messages per peer per frame are dispatched immediately, the rest queue (max 1024 per peer) and drain next frame.
- Session requests: `Callback<P2PSessionRequest_t>` accepts with `AcceptP2PSessionWithUser` only for verified lobby peers (`Net.cs:OnSessionRequest`). `CloseP2PSessionWithUser` exists in the bridge.
- Packets from non-peers are dropped (`Net.cs:Drain`). Sender identity is therefore the Steam transport identity; the mod does no authentication of its own.
- Blobs (`NetBlob.cs`): gzip, base64, cut into 960-char chunks, sent reliable on the `blob` mod channel with a 32-chunk window, ack every 8, 120 packets/s cap, 16 MB limit, 60 s total timeout, CRC32 check, up to 16 concurrent transfers, 2 s resend. It needs only a reliable ordered `Send(peer, string)` underneath.
- Framework-owned channels: `presence` and `blob` (`Net.cs:Init`). `Net.Register` refuses the framework's own mod id.

### 2.3 Identity

Peer id is a `ulong` everywhere (SteamID64). It is also used as durable data:
- Profile and prison files keyed by id (`PrisonStoreWire.cs:Id`, `ProfileStore.cs`, blob names `prison-<tag>`), `GuestSession.HostId`, world folder `<hostId>_<worldId>` (`GuestWorld.cs:74`), `PrisonStore.cs:441` fallback to `Steam.LocalId`.
- Display names and avatars come from Steam.

### 2.4 What an abstract transport needs

Keep `ulong` as the peer id type so none of the 140 `Session.Send` sites change, but split "transport peer id" from "persistent player id" (see risks). Suggested shape, matching what the mod really uses:

```
interface ITransport {
    ulong LocalId { get; }                 // 0 until usable
    ulong HostId { get; }                  // 0 when none
    bool IsHost { get; }
    TransportStatus Status { get; }        // Offline, Pending, Hosting, Joined
    string Error { get; }
    IList<ulong> Members { get; }          // everyone connected
    IList<ulong> Peers { get; }            // verified, excluding self
    int MaxPayloadBytes { get; }           // 1024 incl. "channel|" today

    bool Host(SessionInfo info);           // gameTag, modVersion, gameBuild, maxMembers
    bool Join(string target, SessionInfo expected);   // lobby id or "ip:port"
    void Leave();
    bool Send(ulong peer, byte[] data, int len, bool reliable);
    string NameOf(ulong peer);
    void Pump();                           // called once per Update, delivers on main thread

    event Action<ulong, byte[], int> Received;
    event Action<ulong> MemberJoined, MemberLeft;
    event Action<ulong, ulong> HostChanged;       // may stay unused
    event Action<string> JoinRequested;           // invite accepted
}
```

Points the interface must settle:
1. Delivery thread: the mod expects callbacks on the main thread from inside `Update`. A socket transport should enqueue from its receive thread and drain in `Pump`.
2. Reliable means ordered per peer. The mod relies on it (for example `BYE` after state, `ACK` after `GIVE`, blob chunks, `HOLD` generations). TCP gives it; UDP needs a sequencing layer.
3. Unreliable means "latest wins is fine". On TCP either send it anyway or drop it when the send buffer is backed up.
4. Version gate before a peer is visible: `Peers` must only contain members whose `(gameTag, modVersion, gameBuild)` matched, and `Send` to a non-peer must return false. Direct IP needs a handshake message for this (Steam used lobby data).
5. Roster is a full mesh. Guests send presence/pose/look/chat to each other and a free-roam authority can be a guest, so guest-to-guest delivery is required. For a direct-IP host this means either a relay in the host or guest-to-guest connections. A host relay is simpler; messages would need a "from" that the host stamps.
6. Per-peer budget and drop policy (4 per frame, 1024 queue) should be transport-agnostic and live above the transport with the channel router.
7. Invites, avatars, overlay are Steam-only. Put them behind optional capabilities (`ITransportInvites`, `IPlayerProfile`) so the lobby UI can hide them for IP sessions.
8. Host leaving ends the session. No migration is needed (`Session.cs:364-367`); the mod already treats an owner change as host-left.
9. Max payload: keep 1024 for now; the wire code and `NetBlob` chunking assume it.

Above the transport, keep as-is: channel router (`Register(channel, handler)`), `NetBlob`, `Presence`. These are about 700 lines of framework code with no Steam dependency except `Net.Send` and `Lobby.IsPeer`.

---

## 3. ObenseuerCore and WebUi API surface used by the mod

Method: I took every public type declared in `example-framework/src` and `webui/src`, intersected with identifiers in the mod, then grepped `Type.Member`. Names that collide with mod-local types (the mod defines its own `Look` and `ShopStock`, and uses `Items`, `Spawn`, `Skill`, `Effect`, `Crafting`, `Cash`, `Travel` as local words or game types) were excluded. All 98 files that `using ObenseuerCore;` do so with that single namespace; `WebUi`/`WebView` live in the same namespace (`example-framework/webui/src/WebUi.cs:13`). The mod does not use `ModUi`, `ModWindow`, `ModControls`, `Quests`, `Dialogue`, `Cash`, `Npcs` or any of the registry/content APIs.

Dependency column: `BepInEx` = the framework file imports BepInEx types; `Publicized` = it needs the preloader-publicized `Assembly-CSharp` (private member access by name at compile time) or other deep game access.

### 3.1 Logging

| API | Uses | What it does | Framework file (lines) | Deps |
|---|---|---|---|---|
| `CoreLog.For(id)` returning a `CoreLog` with `Info/Warn/Error/Debug(key, msg)` | one `CoreLog.For` (`Plugin.cs:620`); then `CoopLog.Log.Info` 471, `.Error` 141, `.Debug` 50 | Per-mod log file `BepInEx/logs/<id>.log`, rate limiting (lines per second, error window), `Debug(key,msg)` logs changes only | `src/CoreLog.cs` (314) | BepInEx: `Paths.BepInExRootPath` (line 49); `Core.Verbose` gate |
| `CoopLog.Once(site, msg/ex)` | ~600 catch blocks | mod-local, not framework (`Plugin.cs:618-632`) | n/a | none |

### 3.2 Config, keybinds, input

| API | Uses | What it does | File | Deps |
|---|---|---|---|---|
| `Keybinds.Register(modId, id, label, default)` returning `ModKeybind`; `ModKeybind.Down()` | 2 register, 1 `Down` (`Plugin.cs:48-49,271`) | Rebindable key shown in the framework's Mods menu; the mod also reads `Input.GetKeyDown` directly 19 times (F9, T, `/`, Esc, chat) | `src/Api/Keybinds.cs` (229) | BepInEx: `ConfigFile`, `BepInEx.Unity.Mono.Configuration`; couples to `GameSettings`, `SettingsGuard`, the Mods menu |
| BepInEx `ConfigEntry<T>` / `Config.Bind` | about 40 binds (`Plugin.cs:44-47,124-140`, `CoopChat.cs:64-65`, `CoopDebug.cs:54-57`, `TimerScale.cs:47`, `CoopMap.cs:58`) | Settings and debug switches; `AcceptableValueRange` once | BepInEx directly | BepInEx |

### 3.3 Events and lifecycle

| API | Uses | What it does | File | Deps |
|---|---|---|---|---|
| `Events.SceneLoaded` (`Action<string>`) | 2 (`Plugin.cs:174`, `CoopChat.cs:68`) | Thin wrapper over `SceneManager.sceneLoaded` | `src/Events.cs` (203, subscribes at `:70-80`) | none; only a small part of the file is needed |
| `Events.Saved` | 1 (`WorldShip.cs:126`) | Wrapper over `SaveController.SavingDone` | same | none |
| `Core.GameBuild` | 9 (`Session.cs:41,215`, `SessionDebug`, `CoopDebug.cs:127`...) | `Application.version + " (buildid N)"`, buildid parsed from `steamapps/appmanifest_*.acf` | `src/Core.cs:23`, `:694` | `Core` is a `BaseUnityPlugin`; the property itself is independent |
| Plugin instance as coroutine host | `Plugin.Instance.StartCoroutine` (`GuestWorld.cs:110,171`) | mod-local | n/a | needs a MonoBehaviour |

### 3.4 Harmony helpers

| API | Uses | What it does | File | Deps |
|---|---|---|---|---|
| `CorePatcher.Postfix(type, method, handlerType, handlerName, owner)` | 3 (`PowerSync.cs:49-51`) | Name-based postfix with per-owner Harmony instance and a kill switch (`Core.PatchingAllowed`, build check) | `src/CorePatcher.cs` (311) | HarmonyLib; uses `Core`, `CoreLog`, `ApiLog` |
| All other patches use `HarmonyLib` directly (`new Harmony(id)`, `Patch`, `AccessTools`, `HarmonyMethod`, `Priority`, `UnpatchSelf`) in 55 files | see section 5 | | | HarmonyLib only |

### 3.5 Networking (the transport stack)

Total about 1,730 lines. This is what the abstract transport plus the channel router replace.

| API | Uses | What it does | File (lines) | Deps |
|---|---|---|---|---|
| `Net.Register(modId, channel, handler)` | 1 (`Session.cs:314`) | Channel to `Action<ulong,string>` map, first owner wins | `Api/Net.cs` (215), `NetChannels.cs` (57) | BepInEx (`BepInPlugin` attribute read for `CoreVersion`, `Net.cs:27`); Steamworks |
| `Net.Send(peer, channel, payload, reliable)` | 2 (`Session.cs:319,325`) | UTF-8 of the channel name, a pipe, then the payload; 1024 byte cap, `SendP2PPacket` | `Api/Net.cs:44-53` | Steamworks |
| `Net.Peers`, `Net.Me` | 2, 1 | verified peers, local Steam id | `Api/Net.cs:29-37` | |
| `Lobby.Host/Join/Leave/Invite` | 1 each | see 2.2 | `Api/Lobby.cs` (518) | Steam via `SteamBridge` (159); `Core.GameBuild`; `Net.CoreVersion` |
| `Lobby.Status` (`LobbyStatus`: Offline, Pending, Hosting, Joined) | 14 + 19 enum uses | state | `Lobby.cs:11-17,55` | |
| `Lobby.Owner`, `Lobby.IsHost`, `Lobby.Members`, `Lobby.MemberName`, `Lobby.Error` | 14, 1, 20, 2, 5 | roster | `Lobby.cs` | |
| `Lobby.MemberJoined/MemberLeft/OwnerChanged/JoinRequested` | 2, 9, 2, 1 | events; `MemberLeft` is hooked by about 8 features for cleanup (`ContainerHooks.cs:33`, `VendHooks.cs:22`, `QuestHooks`, `ShopHooks`, `CraftHooks`, `Session.cs:48-49`) | `Lobby.cs:34-45` | |
| `Presence.Publish(area, pos, yaw, moving)`, `Presence.Remotes`, `PresenceRecord` | 1, 3, 20 | 10 Hz unreliable position sharing, records stale after 5 s, `Name` from Steam | `Api/Presence.cs` (125) | none beyond `Net`/`Steam` |
| `NetBlob.Send/Received/Finished/Progress/Cancel` | 17, 9, 3, 1, 1 | chunked reliable byte transfer, see 2.2 | `Api/NetBlob.cs` (486) | none beyond `Net`/`Lobby` |
| `Steam.LocalId/IsReady/OverlayEnabled` | 3, 2, 5 | `SteamUser.GetSteamID`, init probe, overlay flag | `Api/Steam.cs` (65) | Steamworks, `SteamManager` |
| `ModIds.Require` (internal) | via `Net.Register`, `NetBlob.Send` | identifier validation (letters, digits, `.`, `_`, `-`, 32 chars) | `Api/ModIds.cs` (69) | none |

### 3.6 Game-world APIs

| API | Uses | What it does | File (lines) | Deps |
|---|---|---|---|---|
| `Building.Managers(scene)` -> `FurnitureManagerInfo{Guid, SceneName}` | 4 | lists furniture managers per scene | `Api/Building.cs:199` (608) | publicized-style game access; pulls `Housing`, `FurnitureRegistry`, `FurnitureManifestStore`, `FurnitureBuild` |
| `Building.GetPlacedFurniture(areaGuid)` -> `FurnitureItem` (Guid, Id, AreaId, Position, Rotation, ChildRotation, Skin, AltPrefab, ManagerGuid, Object) | 4 | snapshot of placed furniture | `Building.cs:141` | |
| `Building.Place(manager, furnId, pos, rot, childRot, skin, alt, requireKnown)` | 2 | instantiates and registers a piece without the build UI | `Building.cs:283-358` | `FurnitureManager`, `BuildingSystem.FurnitureInfo` internals |
| `Building.Move`, `Building.Remove` | 3, 2 | | `Building.cs:360-480` | |
| `Building.FurniturePlaced/FurnitureRemoved` events | 2 | host-side change feed (`BuildHost.cs:49-50`) | `Building.cs:96-98` | needs the framework's own patches (`FurniturePatches`) |
| `Containers.Opened/Closed` events, `Containers.FindByGuid`, `ContainerInfo{Guid, InstanceId}` | 2, 1 | storage open/close notifications | `Api/Containers.cs` (259) + `ContainersPatches.cs` (64) | `CorePatcher`, `SceneIds`, `ItemRegistry` |
| `WorldItems.IdOf(GameObject)`, `WorldItems.Find(id)` | 1, 2 | stable id for floor items (GUID or `scene/path[idx]`), reverse lookup | `Api/WorldItems.cs:131-170` (324) + `WorldItemsPatches.cs` (210) | `WorldObjects`, `SceneIds`, `DestructibleList` |
| `SaveData.Capture(UnityEngine.Object)` / `Apply(obj, json)` | 7 / 7 | serialize one `SavableScript` with the game's own save serializer by temporarily swapping `GameSave.Global/Level` statics; used for container, station, interactable, liquid and prison state | `src/SaveData.cs:141,179` (644) | Publicized (private `GameSave` fields, `SaveController.Serialize`), `SaveGate`, `FurnitureBlueprints`, `StoryPump` |
| `SaveData.Busy` | 46 | true while a load or save is running | `SaveData.cs:33` -> `Api/SaveGate.cs:55` | `SaveGate` patches the save/load pipeline |
| `SaveSlots.SaveNow()`, `SaveSlots.CanSaveNow` | 1, 1 | force an autosave (`SaveController.Autosave`) | `Api/SaveSlots.cs:55-90` (189) | game save controller |
| `UiState.IsPaused`, `UiState.IsInputBlocked` | 1, 1 | read `GameController.GameIsPaused` / `keysDisabled` | `Api/UiState.cs:32,40` (101) | trivial |
| `PlayerLook.Capture/Apply/IsAvailable/Options/Changed`, `PlayerLook.Look` (37 fields) | 2, 5, 5, 2, 1, 9 | read and write the character creator state of an `Appearance`; `Changed` fires when the player edits the look | `src/PlayerLook.cs` (322), `PlayerLookTable.cs` (278) | game `Appearance`/`LookDial`/`LookSlot`; `Core.PatchingAllowed` |
| `MainMenuButtons.Add("MULTIPLAYER", ...)`, `SetControlsEnabled` | 1, 2 | adds a button to the main menu and keeps pad navigation working | `Api/MainMenuButtons.cs` (517) | patches `MainMenuNavigationHelper` through `CorePatcher` |

### 3.7 UI

| API | Uses | What it does | File | Deps |
|---|---|---|---|---|
| `WebUi.Open(modId, folder, page, modal)` -> `WebView` | 2 (`LobbyPage.cs:112`, `CoopPopupPage.cs:76`) | open an HTML page from the mod folder in an embedded WebView2 overlay | `webui/src/WebUi.cs` (598) | BepInEx (`Paths`, config); Windows only |
| `WebView`: `Show/Hide/SetBounds/Post/Close/IsVisible/IsClosed/EscapeHides`, events `Message`, `KeyPressed` | ~20 | JSON message bridge to the page | `webui/src/WebView.cs` (386) | native `ObxWebView.dll` through `WebUiNative.cs` (265) plus a C++ project |
| `WebUi.SuspendAll/ResumeAll/LastError` | 1, 2, 3 | hide pages while the Steam overlay is up | `WebUi.cs:114-123` | |

The web pages are optional at runtime. `MainMenuButton.cs` tries `LobbyPage.Open()` (`MainMenuButton.cs:339`) and falls back to a uGUI panel it builds itself (`ShowPanel`, `MainMenuButton.cs:346`) when WebView2 fails or the game is in exclusive fullscreen (`MainMenuButton.cs:331-336`, `PageTick` at `:358`). The in-world pieces (`CoopUi`, `CoopAlert`, `CoopChat.Draw`, `CoopPlayersPanel`, `CoopMap`) are IMGUI/uGUI with no web dependency. The README says the same ("without WebUi you get a simpler lobby panel"). WebView2 is a Windows-native component; plan on the uGUI panel only. The build still hard-references the WebUi DLL (`ObenseuerCoop.csproj`, `CheckFrameworkFiles`), so that reference and `LobbyPage`/`CoopPopupPage`/`LobbyInput` and `web/` can simply be dropped.

### 3.8 What the port actually has to provide

Must have (anything less and the mod does not start):
1. Logger with `Info/Warn/Error/Debug(key,msg)` and per-mod file. Trivial.
2. Config binding for bool/float/KeyCode/string plus a way to persist. Trivial.
3. Harmony: a plain `Harmony` instance per feature id is enough; `CorePatcher.Postfix` can be one helper.
4. `GameBuild` string.
5. Scene-loaded and save-finished events.
6. `Net` + `Lobby` + `Presence` + `NetBlob` with the semantics in 2.4.
7. `SaveData.Busy`, `SaveData.Capture/Apply`, `SaveSlots.SaveNow/CanSaveNow`.
8. `PlayerLook` (capture, apply, changed, availability).
9. `Building` change feed plus Place/Move/Remove/GetPlacedFurniture/Managers.
10. `Containers` open/close events and `FindByGuid`; `WorldItems.IdOf/Find`.
11. `UiState.IsPaused/IsInputBlocked`.
12. A main-menu button hook, or just IMGUI/own menu entry.

Can drop for slice 1: WebUi, Keybinds menu integration, `MainMenuButtons` (use F8/F7 or a small IMGUI window), `CorePatcher`.

The heavy items are 7, 9, 10: `SaveData` (644 lines plus `SaveGate`, `FurnitureBlueprints`) and `Building` (608 plus `Housing`, `FurnitureRegistry`, `FurnitureBuild`). They should be reimplemented small, or ported with only the pieces the coop calls (Place/Move/Remove/Describe and the three events).

---

## 4. BepInEx-specific usage in the mod

| Item | Where | Replacement |
|---|---|---|
| `[BepInPlugin(guid, name, version)]`, `[BepInDependency("obenseuer.moddingframework", ">=0.9.0")]`, BepInEx 5 vs 6 `#if` | `Plugin.cs:5-16` | Our mod manifest (id, name, version, dependency) |
| `class Plugin : BaseUnityPlugin` with `Awake`, `Update`, `OnGUI`, `Config`, `Info`, `Instance` | `Plugin.cs:17-263` | A mod base class or interface with `Load/Update/OnGUI`; plus a MonoBehaviour host for coroutines |
| `Plugin.Instance.StartCoroutine` | `GuestWorld.cs:110,171` | MonoBehaviour provided by the loader |
| `ConfigFile`, `ConfigEntry<T>`, `ConfigDescription`, `AcceptableValueRange`, `ConfigDefinition` iteration | `Plugin.cs:29-32`, `CoopChat.cs:5,41-42`, `CoopChat.Draw.cs:5,12`, `CoopDebug.cs:5,32-36,350`, `TimerScale.cs:4,23,47`, `CoopMap.cs:41,58` | Our config service. `CoopDebug.cs:350` enumerates all entries to print a summary, so it needs enumeration |
| `Paths.ConfigPath` | `LookPin.cs:386` | Loader path service |
| `BepInPlugin` attribute scan over loaded assemblies in a `plugins` folder to exchange mod lists with peers | `CoopUi.cs:26-56` | Our loader's mod registry (id and version list) |
| `Loader()` string "BepInEx 5/6" for diagnostics | `CoopDebug.cs:335-342` | loader name |
| `Assembly.GetExecutingAssembly().Location` to find `web/` | `LobbyPage.cs:111`, `CoopPopupPage.cs:75` | mod folder path from the loader |
| `new HarmonyLib.Harmony(id)` with ids like `obenseuer.coop.cont` | every hooks file, `Plugin.cs:85` (liquid is handed a Harmony instance) | Loader supplies Harmony or we create ours; keep one id per feature so `UnpatchSelf()` rolls back a single feature (used in every `catch`, for example `ContainerHooks.cs:28`) |
| BepInEx log (`BepInEx\logs\obenseuer.coop.log`) | README, `CoreLog` | our log file |
| Dual BepInEx 5/6 build flavours (`Loader` property, `FrameworkFileSuffix`, `BEPINEX5/6` defines) | `Directory.Build.props`, `ObenseuerCoop.csproj` | drop |

Also framework-side BepInEx coupling that the mod inherits by calling it: `Net.CoreVersion` reads the framework's `[BepInPlugin]` (`Net.cs:27`), `CoreLog` uses `Paths.BepInExRootPath` (`CoreLog.cs:49`), `Keybinds` uses `BepInEx.Configuration`.

---

## 5. Harmony patch targets

Facts about how the mod patches:
- No `[HarmonyPatch]` attributes anywhere (grep of `[HarmonyPatch`/`HarmonyPatch(` returns nothing) and no transpilers. Everything is a manual `harmony.Patch(AccessTools.Method(type, name, args), prefix:/postfix:/finalizer:)`, usually through a small per-file `Patch(...)` helper (20 copies).
- Handlers are static methods on the hooks class, found by name string (`new HarmonyMethod(typeof(X), "NamePrefix")`). They use `__instance`, `__exception` and sometimes `ref` arguments. Prefix returns `bool` to skip the original.
- Harmony features needed: `Patch` with prefix/postfix/finalizer, `HarmonyMethod.priority` (`Priority.First` in the gate and guard hooks), `AccessTools.Method/Field/TypeByName`, `UnpatchSelf`. Plain Lib.Harmony 2.x is enough.
- Several hooks patch the mod's own methods, which is debug-only logging: `SessionHooks.cs:13` patches `Session.Leave`; `InteractableStateHooks.cs:26-29` patches four `InteractableSync` methods.
- Roughly 180 fixed targets, plus four groups found at runtime by scanning `Assembly-CSharp` (marked "dynamic" below).
- Role: "G" = guest-only effect (prefix blocks the vanilla action and sends a request), "H" = host/authority capture (postfix reports what happened), "B" = both.

`Pre` prefix, `Post` postfix, `Fin` finalizer. Handler names in parentheses where it helps.

### 5.1 Session, save/load, world, time

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `PauseMenu.QuitMainMenu`, `QuitDesktop` | Pre | `SessionHooks.cs:11-12` | start the free-roam collect/quit protocol, send BYE |
| `PauseMenu.QuitMainMenu`, `QuitDesktop` | Pre | `ProfileHooks.cs:18-19` | flush the guest profile before leaving |
| `Session.Leave` (mod's own) | Pre | `SessionHooks.cs:13` | defer leave while collecting |
| `SaveController.SaveGame(string,string,string)` | Pre+Fin | `WorldHooks.cs:31` | mark saving, swap in the session clock for the file |
| `SaveController.SaveGame(string,string,string)` | Pre | `GuestSaveHooks.cs:26` | guest: redirect/deny saves |
| `SaveController.SaveGame(string,string,string)` | Post | `NewGameAdopt.cs:34` | adopt a new game's first save as host world |
| `SaveController.WriteSaveFile(string,string)` | Pre | `GuestSaveHooks.cs:27` | guest: redirect writes |
| `SaveController.GetCharacterName` | Post | `GuestSaveHooks.cs:25` | guest character name |
| `SaveController.ChangeLevel(string,string)` | Pre | `GuestSaveHooks.cs:28` | guest: block |
| `SaveController.ChangeLevel(string,string)` | Pre | `HostTravel.cs:20-24` | host: tell guests where to go (TRAVEL) |
| `SaveController.LoadGameWithMigration(string,string,bool)` | Pre | `FreeRoam.cs:48` | free roam: load intercept |
| `SaveController.LoadGameWithMigration(string,string,bool)` | Pre | `Solo.cs:35` | host reload announcement |
| `LoadMenu.LoadGameSlot` | Pre | `MenuLoadHooks.cs:17-19` | guest cannot load saves from the menu |
| `DeveloperConsole.HandleInput(string)` | Pre | `DeveloperConsoleHooks.cs:17-19` | guest: block console commands |
| `AzureTimeController.Update` | Post | `WorldHooks.cs:21` | guest clock follow |
| `WeatherManager.SetRandomWeather` | Pre | `WorldHooks.cs:22` | block local roll on guest |
| `WeatherManager.SetWeather(int,bool,float)` | Post | `WorldHooks.cs:23` | host weather capture |
| `WeatherManager.OnSavingGame`, `OnLoadingGame` | Pre / Post | `WorldHooks.cs:24-25` | save own weather |
| `TimeOfDayAzure.OnSavingGame`, `OnLoadingGamePrimary` | Pre / Post | `WorldHooks.cs:26-27` | save own clock |
| `WaitingController.Sleep`, `Wait` | Pre | `WorldHooks.cs:28,30` | sleep/wait vote |
| `WaitingController.PassOut` | Pre | `WorldHooks.cs:29` | |
| `WaitingController.PassOut(bool,WaitingController.Type)` | Post | `Solo.cs:30` | passout handling |
| `WaitingController.PassOut(bool,Type)` | Pre | `GuestGateHooks.cs` (`PassoutPrefix`) | guest gate |
| `WaitingController.StartSleep(float,bool,SleepEvent[],bool,float)` | Post | `WorldHooks.cs:47` | |
| `WaitingController.StartSleep` | Post | `Solo.cs:34` | |
| `WaitingController.Wakeup(bool,bool)` | Pre+Post | `WorldHooks.cs:48` | |
| `TimeOfDayAzure.SetSpeedFactor(float)` | Post | `WorldHooks.cs:49` | time speed sync |
| `TimeOfDayAzure.SetSpeedFactor(float)` | Pre | `Solo.cs:31` | |
| `Sleep.Use`, `Sleep.Wakeup` | Post | `WorldHooks.cs:50-51` | bed vote |
| `WaitingUI.CloseTimeSetUI` | Post | `WorldHooks.cs:52` | |
| `Death.PlayerDeath` | Post | `Solo.cs:32` | death flow |
| `PauseMenu.PausemenuDeath` | Pre | `Solo.cs:33` | death menu |
| `TenementController.UpdateRentPayment(int)` | Pre | `TenementRentHooks.cs:15` | guest not paid rent twice |
| `TenementController.UpdateProgesses(int)` | Pre | `TimerScale.cs:53` | timer speed |
| `TenementUpgradeProgress.GetCurrentTime(bool)` | Pre | `TimerScale.cs:54` | |
| `DifficultyInfo.GetItemCraftingTime(float)`, `GetFurnitureCraftingTime(float)` | Post | `TimerScale.cs:55-56` | |
| `grow.DeltaSeconds(int)` | Pre | `TimerScale.cs:57` | |
| `TimeOfDayAzure.Timer.OnLoading(int,bool)` | Post | `TimerScale.cs:58` | |

### 5.2 Players and avatars

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `Appearance.Start` | Post | `LookPin.cs:42-50` | pin look at spawn |
| (reads) `OpenSewerCharacterController`, `Appearance.bodyModels`, `PlayerLocator` | none | `PoseSync.cs:35`, `GameRefs.cs`, `PuppetBody.cs` | reflection only |

### 5.3 Building

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `BuildingSystem.Place()` | Pre | `BuildHooks.cs:20` | G: request instead of place |
| `BuildingSystem.Take(ref GameObject)` | Pre | `BuildHooks.cs:21` | G |
| `BuildingSystem.Move()` | Pre | `BuildHooks.cs:22` | G |
| `BuildingArea.Enter()` | Post | `BuildGate.cs:20-23` | gate: guest enters a replicated area |

### 5.4 Containers, items, inventory

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `Storage.EnterExternally(bool)` | Pre | `ContainerHooks.cs:20` | G: request lock; H: refuse while held |
| `Storage.ExitExternally()` | Pre+Fin | `ContainerHooks.cs:21` | send state on close |
| `Storage.UseStorage()`, `LockUse()` | Pre+Fin | `ContainerHooks.cs:22-23` | depth counters |
| `LiquidStorage.EnterExternally(bool)`, `UseStorage()`, `LockUse()` | Pre | `UnsharedHooks.cs:17-19` | lease logic |
| `LiquidStorage.ExitExternally()`, `EnterExternally(bool)` | Post | `LiquidSync.cs:35-36` | state send |
| `CollectibleItem.PerformPickup`, `Interact`, `InvokeActions` | Pre | `ItemHooks.cs:20-22` | G: request pickup |
| `CollectibleCoin.Interact`, `Collectible.Interact` | Pre | `ItemHooks.cs:23-24` | |
| `Collectible.Destroy` | Post | `ItemHooks.cs:25` | |
| `ItemOperations.SetCollectibleItemValues(ItemStack,GameObject,bool)` | Post | `ItemHooks.cs:26` | |
| `ItemData.DropItem(int,bool)` | Pre+Fin | `ItemHooks.cs:27` | |
| `ItemOperations.DropItem(ItemStack,GameObject,DropItemValues)` | Pre | `ItemHooks.cs:28` | |
| every `Collectible` subclass declaring `Interact`, plus a named list (`CollectibleNote`, `CollectiblePackage`, `CollectibleTaskItem`, `CollectibleStartPackage`, `CollectibleRecipe*`, `FurnitureCollectable`, `WastelandGlow`) | Pre | `ItemHooks.cs:9,97-119` (dynamic) | guest: refuse unsupported pickups |
| `ItemOperations.AddItemsAndDropRemaining` | Pre | `Harvest.cs:43` | harvest yield |
| `PlayerStats.IncreaseSkill`, `GetSkill` | Pre | `Harvest.cs:44-45` | skill sharing |
| `WorkableResourceSource.Damage`, `SpawnItemOnBreak.Damage(int,Vector3,Vector3,DamageType)` | Pre+Post | `Harvest.cs:28-33` | harvest sync |

### 5.5 Interactables, doors, power

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `Door.OnOpen`, `Lock`, `Unlock` | Post | `DoorSync.cs:34,37,38` | |
| `Door.Close`, `ToggleInterrupt` | Pre+Post | `DoorSync.cs:35-36` | |
| `LightTurnOffOn.turnLightOn`, `turnLightOff` | Post | `DoorSync.cs:39-40` | |
| `InteractableDynamiteCone.PlaceDynamite`, `RemoveDynamite`, `Detonate` | Pre+Post | `DynamiteSync.cs:23-25` | |
| `Interact()` on `Button`, `SelectionButton`, `Keypad`, `Lantern`, `EditableSign`, `InteractableRelay`, `BreakableLock` | Pre+Post | `InteractableSync.cs:29,68-80` (dynamic, resolved by name) | |
| `Interact()` on every other `Interactable` subclass | Pre (`Priority.First`) | `InteractableGuard.cs:71,105-113` (dynamic) | guest: refuse unsupported interactables |
| `CraftingBase.SetPowerSourceStatus`, `PowerSource.FuelStateChange`, `FurnitureStats.setPower` | Post | `PowerSync.cs:49-51` (via `CorePatcher.Postfix`) | |
| `InteractableSync.Apply/Handle/Reject/InteractPostfix` (mod's own) | Pre/Post | `InteractableStateHooks.cs:26-29` | debug logging |
| `Sauna.TurnOn`, `TurnOff`, `ThrowWater` | Pre+Post | `SaunaHooks.cs:19-21` | |

### 5.6 Crafting, shops, vending, slots

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `manufacturing.Craft` | Pre | `CraftHooks.cs:18` | |
| `manufacturing.OnCraftDone`, `manufacturingProcess.EndProcessing` | Post | `CraftHooks.cs:19,23` | |
| `manufacturingProcess.StartProcess`, `OpenLid`; `Growing.StartGrow(int)`, `Harvest(GameObject)`, `Harvest()` | Pre+Post | `CraftHooks.cs:20-22,24-25` | |
| `CraftingBase.Exit` | Post | `CraftHooks.cs:26` | |
| `manufacturingProcess.ProcessStarted` | Post | `CraftHooks.cs:27` | |
| `manufacturing.CheckItems(bool)` | Pre+Fin | `CraftHooks.cs:28` | |
| `PlayerStats.GetSkill`, `GetSkillWithEffects`, `IncreaseSkill(skill,float,bool)` | Pre | `CraftHooks.cs:30-32` | host skill used for guest crafts |
| `CraftingBase.CreateItems` | Pre+Fin | `CraftHooks.cs:33` | |
| `CraftingBase.Interact` | Pre (soft) | `CraftHooks.cs:34` | |
| `SlotController.DropItem(ItemData,ref int,bool,bool,bool)`, `TakeItem(ItemData)` | Pre (soft) | `CraftHooks.cs:35-36` | |
| `ShopController.OpenMenu(BaseShop)` | Pre | `ShopHooks.cs:18` | |
| `ShopController.CloseMenu` | Pre+Post | `ShopHooks.cs:19` | |
| `ShopBaseUI.Buy`, `Sell` | Pre+Post | `ShopHooks.cs:20-21` | |
| `BaseShop.DeltaSeconds(int)` | Pre | `ShopHooks.cs:22` | block guest restock |
| `VendingMachine.Buy(VendingMachineItem)` | Pre | `VendHooks.cs:17` | |
| `VendingMachine.MinutePassed(int)` | Pre | `VendHooks.cs:18` | |
| `SlotMachineGameplay.Interact`, `RaiseStakes`, `DoubleButton(bool)`, `StartDoubling` | Pre | `SlotHooks.cs:17,20-22` | |
| `SlotMachineGameplay.StartRotatingNonDoublingSlots`, `HandleRoundEndSituation(bool)` | Post | `SlotHooks.cs:18-19` | |

### 5.7 Dialogue, quests, NPCs, animals, combat

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `InteractableTalk.OnDialogueStart(string,bool)`, `OnDialogueEnded` | Post | `TalkHooks.cs:25-26` | |
| `Crime.PayFine`, `TryToBribe`; `Bank.CreateAccount`; `CharacterItemManager.GiveItemsToCharacter.GiveItems`; `InteractableItemGiver.Interact`; `CharacterItemManager.TakeItemsFromPlayerInventory` | Pre | `TalkHooks.cs:28-33` | route to host while talking |
| `DialogueLua.SetVariable(string,object)`, `SetTableField(...)` | Post | `DialogueProbe.cs:33-38` | capture Lua variable changes |
| `TaskController.EditTask`, `EditTaskStatus` (x2 overloads), `SetObjectiveFinished`, `EditTaskObjective`, `EditTaskCounter` | Post | `QuestHooks.cs:22-27` | host reports quest changes |
| `EditTask`, `EditTaskStatus` (x2), `SetObjectiveFinished`, `EditTaskCounter` (five of the six) | Pre | `GuestGateHooks.cs` (`QuestEditPrefix` and siblings) | guest: block |
| `TaskController.TaskUpdated`, `ObjectiveUpdated` (events) | subscribe | `QuestHost.cs:48-49` | |
| `Animal.OnHuntTargetDetected`, `AnimalStats.Damage`, `AnimalAttackTrigger.OnTriggerStay` (by name) | Pre | `AnimalSync.cs:34-36` | |
| every `IDamageReceiver` MonoBehaviour declaring `Damage(int,Vector3,Vector3,DamageType)` | Post | `CombatSync.cs:23-33` (dynamic) | hit sync |

### 5.8 Guest gate (one file, 23 targets, all Pre at `Priority.First`)

`GuestGateHooks.cs:21-45` lists them, `:60-72` installs. They exist so a guest in the host's world cannot start things the mod does not share yet:

`Crime.GoToPrison(double)`, `Crime.TeleportToPrison`, `ShopController.OpenMenu`, `ShopBaseUI.OpenMenu`, `MarketShop.Interact`, `Trade.Interact`, `Trade.StartTrade`, `InteractableTicketMachine.Interact`, `BottleRecycling.Interact`, `InteractablePay.Interact`, `InteractableTalk.CheckStartDialogueInterrupt`, `InteractableTalk.Interact`, `InteractableTalk.StartDialogue`, `InteractableTalk.ForseStartDialogue`, `Phone.Interact`, `Mailbox.Interact`, `Bank.EnterBank`, five `TaskController.Edit*`/`SetObjectiveFinished` overloads, `WaitingController.PassOut`.

### 5.9 Prison, trains, elevators

| Game type.method | Kind | File:line | Purpose |
|---|---|---|---|
| `Crime.TeleportToPrison` | Pre | `PrisonLocal.cs:59` | per-player cells |
| `Crime.ConfiscateItems(Storage,bool,Transform)` | Pre+Fin | `PrisonLocal.cs:60` | |
| `Prison.Awake`, `OnLoadingGame` | Post | `PrisonLocal.cs:61,63` | |
| `Prison.ImprisonPlayer` | Pre+Post+Fin | `PrisonLocal.cs:62` | |
| `Prison.Release`, `Escape` | Pre+Post | `PrisonLocal.cs:64-65` | |
| `TrackTrain.CalculateSpeed`, `Minitrain.FixedUpdate`, `StartMinitrain`, `StopMinitrain` | Pre | `TrainHooks.cs:15,16,18,19` | |
| `Minitrain.ChangePlayerUsingState(bool)`, `MinitrainSwitch.ChangeTrack(int)` | Pre+Post | `TrainHooks.cs:17,20` | |
| `MinitrainSwitch.ChangeTrackForce(int)` | Post | `TrainHooks.cs:21` | |
| `Elevator.FixedUpdate` | Pre | `ElevatorHooks.cs:18` | |
| `Elevator.Call(int)`, `MoveToOneFloorUp(bool)`, `MoveToOneFloorDown(bool)`, `SetFloorEnabled(int)`, `SetFloorDisabled(int)`, `TogglePower(bool)` | Pre+Post | `ElevatorHooks.cs:19-24` | |

### 5.10 Overlapping targets

These game methods are patched by more than one feature. Our hook layer must allow several patches per method and keep ordering predictable (the gate and guard hooks use `Priority.First`):

`SaveController.SaveGame` (3 files), `SaveController.ChangeLevel` (2), `SaveController.LoadGameWithMigration` (2), `PauseMenu.QuitMainMenu/QuitDesktop` (2), `WaitingController.PassOut` (3), `WaitingController.StartSleep` (2), `TimeOfDayAzure.SetSpeedFactor` (2), `LiquidStorage.EnterExternally` (2), `PlayerStats.GetSkill/IncreaseSkill` (2), `Crime.TeleportToPrison` (2), `ShopController.OpenMenu` (2), `TaskController.Edit*` (2), `InteractableTalk.*` (2).

### 5.11 Reflection and events (non-patch hooks)

- Private fields and methods read by reflection, for example `Door.direction/position/ReversedDir` (`DoorSync.cs:94,95,118`), `BuildingSystem.currentrot` (`BuildGuestMove.cs:9`), `Prison.inPrison/activeCellIndex/activeCell/clearStorage/DeltaSeconds` (`PrisonLocal.cs:46-51`), `SlotMachineGameplay._state` and 14 more (`SlotRound.cs:68-81`), `Minitrain.needle/lever`, `TrackTrain.TrainData._drivingLocomotive` (`TrainGuest.cs:74-81`), `Map.playerMarker/mapsize/imageCanvas` (`CoopMap.cs:21-23`), `TenementApartmentPrefabs.currentExamineCounter` (`BuildRecon.cs:22-23`), `CraftingBase.selectedRecipe/AllRecipes`, `GrowingSpot.savedPlant`, `TimeOfDayAzure.timers`, `Fuel.DeltaSeconds` (`StationState.cs:12,116,119`). Any game update that renames these breaks silently; the `SelfCheck()` methods (23 of them) exist to find that.
- Subscribes to game events: `SaveController.LoadingDone` (6 sites), `SaveController.SavingDone` (`GuestProfile.cs:56`), `TaskController.TaskUpdated/ObjectiveUpdated`, removes a `TimeOfDayAzure.SecondsPassed` handler with reflection (`PrisonLocal.cs`, `Detach`).

---

## 6. Features mapped to files

Line counts are summed over the listed files in `example-mp/`. Total 46,757.

| Feature | Files | Lines | Channel(s) | Notes |
|---|---|---|---|---|
| Core, session, plugin | `Plugin.cs`, `Shared.cs`, `Session.cs`, `Session.Join.cs`, `SessionReload.cs`, `SessionAlerts.cs`, `SessionDebug.cs`, `SessionHooks.cs` | 1,942 | `world` | the seam to the transport lives here |
| Join and world transfer | `WorldShip*.cs`, `GuestSession.cs`, `GuestWorld*.cs`, `GuestSplice.cs`, `SpliceIds.cs`, `GuestGate*.cs`, `GuestSaveHooks.cs`, `GuestProfile.cs`, `ProfileStore.cs`, `ProfileDate.cs`, `ProfileHooks.cs`, `ProfileMirror.cs`, `HostTravel.cs`, `MenuLoadHooks.cs`, `NewGameAdopt.cs`, `DeveloperConsoleHooks.cs`, `TenementRentHooks.cs`, `UnsharedHooks.cs` | 5,012 | `profile`, blobs | the biggest cluster; depends on the game's save format and FullSerializer |
| Clock, weather, sleep | `WorldSync.cs`, `WorldHooks.cs`, `BedVote.cs`, `TimeSpeed.cs`, `TimerScale.cs` | 1,693 | `world`, `tcfg` | host clock, guests glide (`SnapMinutes`, `SmoothSeconds`) |
| Players and avatars | `Puppet.cs`, `PuppetBody.cs`, `PoseSync.cs`, `SitSync.cs`, `SitWire.cs`, `AppearanceSync.cs`, `Look.cs`, `LookPin.cs`, `CoopAvatars.cs`, `LobbyAvatars.cs`, `GameRefs.cs` | 2,337 | `pose`, `sit`, `look`, framework `presence` | puppet = clone of the local player rig with materials copied and rigging stripped |
| Building | `Build{Gate,Guest,GuestMove,GuestRecon,Hooks,Host,Recon,Wire}.cs` | 2,781 | `build` | host owns furniture, guests send place/take/move, digests every 10 s |
| Containers | `Container{Guest,GuestLock,GuestRevert,Hooks,Host,HostLock,Scan,Wire}.cs` | 2,783 | `cont` + blobs | one holder at a time, state returned as JSON blob on close |
| Floor items, inventory transfers | `Item*.cs` (14 files), `LocalDrops.cs`, `TransferLedger.cs` | 3,844 | `item` | pickups/drops/coins; transfer ledger tracks request, reserve, grant, apply |
| Doors, lights, interactables | `DoorSync*.cs`, `DynamiteSync.cs`, `Interactable*.cs` (9 files) | 2,623 | `door`, `dyn`, `interact` | allow-list of 7 interactable types, guard on the rest |
| Crafting, stations, liquids, power | `Craft{Guest,Hooks,Host,Scan,Wire}.cs`, `LiquidSync*.cs`, `PowerSync.cs`, `StationState.cs` | 2,949 | `craft`, `liquid`, `power` | uses `SaveData.Capture/Apply` for station state |
| NPCs, animals, combat, harvest | `NpcSync.cs`, `AnimalSync.cs`, `CombatSync.cs`, `Harvest.cs` | 1,074 | `npc`, `animal`, `hit`, `harvest` | 5 Hz unreliable |
| Dialogue | `DialogueProbe.cs`, `Talk{Guest,Hooks,Host,Wire}.cs` | 1,618 | `talk` | guest talks run on the host |
| Quests | `Quest{Guest,GuestFlow,Hooks,Host,Start,StartHost,Wire}.cs` | 2,010 | `quest` | only host can start quests |
| Shops, vending | `Shop*.cs` (7), `Vend*.cs` (4) | 3,080 | `shop`, `vend` | |
| Trains, elevators | `Train{Guest,Hooks,Host,Wire}.cs`, `Elevator{Guest,Hooks,Host,Wire}.cs` | 2,662 | `train`, `elev` | |
| Sauna, slot machines | `Sauna*.cs`, `Slot*.cs` | 1,998 | `sauna`, `slot` | |
| Free roam, solo, death | `FreeRoam*.cs` (6), `Solo.cs`, `SoloDeath.cs` | 2,211 | `world` | per-scene authority and hand-off |
| Prison | `Prison{Local,Store,StoreFiles,StoreLocal,StoreWire}.cs` | 1,756 | `prison` + blobs | per-player cells and confiscated item store |
| UI, chat, map, debug | `CoopUi.cs`, `CoopChat*.cs`, `CoopMap.cs`, `CoopPlayersPanel.cs`, `CoopPopup*.cs`, `CoopAlert.cs`, `CoopNotice.cs`, `CoopDebug.cs`, `Lobby{Page,Input,Keys}.cs`, `MainMenuButton*.cs`, `web/lobby/*.html` | 4,384 (+795 HTML) | `chat`, `mods` | chat has `/` commands and rate limits; players panel on F9 |

Smallest files that are already useful on their own: `PoseSync.cs` (103), `SitSync.cs` (82), `SitWire.cs` (55), `AppearanceSync.cs` (101), `NpcSync.cs` (245), `DoorSync.cs` (420).

---

## 7. Large files and code quality

### 7.1 Files over 300 lines

69 of the 176 files are over 300 lines (`wc -l`). Largest first, grouped by size. Anything over 500 is a sign the file mixes several jobs.

| Range | Files |
|---|---|
| 600+ | `GuestSplice.cs` 686, `BuildGuest.cs` 675, `LiquidSync.cs` 640, `CoopPlayersPanel.cs` 634, `Plugin.cs` 632, `TrainGuest.cs` 616, `QuestGuest.cs` 611, `ContainerHost.cs` 609 |
| 500-599 | `PrisonLocal.cs` 576, `ItemHost.cs` 568, `Session.cs` 553, `BuildRecon.cs` 553, `FreeRoam.cs` 545, `WorldShip.cs` 539, `CoopChat.cs` 538, `ContainerWire.cs` 533, `ContainerGuest.cs` 532, `MainMenuButton.cs` 520, `ItemGuest.cs` 520, `ItemMeta.cs` 516, `GuestWorld.cs` 511, `TalkWire.cs` 507, `ShopGuestTrade.cs` 505, `GuestProfile.cs` 503, `InteractableSync.cs` 502 |
| 400-499 | `QuestHost.cs` 485, `PrisonStore.cs` 469, `BuildHost.cs` 468, `ElevatorWire.cs` 467, `Puppet.cs` 461, `CraftHost.cs` 460, `SlotRound.cs` 458, `GuestGateHooks.cs` 440, `WorldSync.cs` 434, `PrisonStoreFiles.cs` 426, `TrainHost.cs` 423, `DoorSync.cs` 420, `TimeSpeed.cs` 412, `Solo.cs` 412, `VendGuest.cs` 411, `LookPin.cs` 406, `PowerSync.cs` 405, `CoopDebug.cs` 404, `TalkHost.cs` 403, `CraftGuest.cs` 402 |
| 300-399 | `QuestWire.cs` 393, `FreeRoamHandoff.cs` 393, `AnimalSync.cs` 386, `ShopHost.cs` 384, `ShopHostTrade.cs` 379, `CoopMap.cs` 379, `ContainerScan.cs` 376, `ProfileStore.cs` 375, `FreeRoamStore.cs` 372, `BuildGuestRecon.cs` 371, `LobbyPage.cs` 368, `TalkGuest.cs` 367, `SaunaGuest.cs` 350, `TimerScale.cs` 349, `DoorSyncScan.cs` 348, `InteractableGuard.cs` 330, `ContainerGuestRevert.cs` 329, `BuildWire.cs` 329, `ShopGuest.cs` 322, `TransferLedger.cs` 320, `PuppetBody.cs` 318, `BedVote.cs` 314, `ElevatorGuest.cs` 311, `ItemWire.cs` 310 |

The two worst for the port are `Plugin.cs` (wiring for every feature) and `Session.cs` plus its four partials (transport facade mixed with join logic). `GuestSplice.cs` is long because of the save-format tree surgery, not because of structure problems.

### 7.2 Reusable as-is or nearly

- Wire files as a pattern and as code: `SitWire`, `PoseSync.Parse`, `AppearanceSync` + `Look.cs` (37-field look codec with ranges), `PrisonStoreWire`, `ContainerWire`, `BuildWire`. They have no BepInEx or framework dependency except the `Session` calls and `CoopLog`.
- `Shared.cs` (request-id, admit/answer cache): 130 lines, only needs `Session.Send/Peers`.
- `WorldShip` ticket pack/verify and `GuestSplice` are self-contained on top of `NetBlob` and the game's file layout, but they encode game save knowledge (`Globals.tnmt`, `Info.tnmt`, `profile.json`, GUID lists in `GuestSplice.RemoveGuids`) that took effort to learn. Keep the logic, rewrite the style.
- `Puppet`/`PuppetBody` (rig clone, animator driving, label) only need `GameRefs` and `Look`.
- The hook-with-selfcheck pattern: every `XHooks.Install` returns false and unpatches itself on failure, and `SelfCheck` resolves all targets. Worth keeping as a startup check against game updates.
- `CoopDebug` tagged diagnostics (system tags, summary every 60 s, perf gap lines) is useful for a first playtest, though it is 404 lines of its own.

### 7.3 Tightly coupled

- `Plugin.cs` knows every feature: init order, channel registration, per-scene reset fan-out, per-frame tick order, lobby-exit reset. Any feature change edits it.
- `Session` is a static god class split across five partial files, mixing transport facade, join state machine, reload wait, alerts, menu return and world id. Features reach into it for `HostId`, `Peers`, `AreaOf`, `IsGuest` instead of getting an interface.
- Role checks are scattered: `Session.IsHost`, `FreeRoam.IAmAuthority`, `GuestSession.InHostWorld`, `from == Session.HostId`. The three authority notions (host, scene holder, guest-in-host-world) are re-derived per call site. A single `Role`/`Authority` service would remove a class of bugs.
- Everything is `static`. State resets are manual (`Reset()` lists in `Plugin.cs:329-390`). A forgotten reset means stale state in the next session.
- UI is welded into logic: notices and popups (`CoopNotice.Show`, `CoopAlert.Show`) are called from deep inside hosts and guests; `MainMenuButton*` (740 lines) reaches into `Session`, `GuestWorld`, `LobbyPage`.
- Duplication: `Patch(Harmony, ...)` helper in 20 files, `Clean` in 16, `MaxReqId` in 12, near-identical `TryParse` plumbing in 13 wire files, 23 `SelfCheck` methods that repeat the Patch target lists. A table-driven hook declaration and a shared wire reader/writer would cut thousands of lines.
- Strings as protocol: kinds are bare string literals compared with `==` in handlers; a typo is a silent drop. A byte opcode plus small writer/reader types would be smaller, faster and catch mistakes at compile time. Packets are capped at 1024 bytes, so format efficiency matters for digests (`MaxDigestPacket = 880`).
- Error handling is catch-all: about 600 `catch (Exception)` blocks that log once per site (`CoopLog.Once`) and continue. Good for not crashing the game, bad for finding why a sync silently stopped.
- Magic numbers for timeouts are scattered as `const float` in each file (listed in 1.7/1.8). A central tuning table would help.
- UI via IMGUI `OnGUI` (`CoopUi`, `CoopChat.Draw`, `CoopPlayersPanel`) with raw `Input.GetKeyDown` (legacy Input; the project references `UnityEngine.InputLegacyModule`).
- Reflection on private game fields is spread through feature code (section 5.11). A single `GameAccess` layer, or our own publicized assembly, would give one place to fix after a game update.

### 7.4 What we would restructure

1. Introduce `ITransport`, a channel router and a `Role/Authority` service; make `Session` a thin state machine over them.
2. Give every feature a small interface (`Install`, `Reset`, `Tick`, `OnSceneChanged`, `OnPeerLeft`) and let the loader drive it instead of the `Plugin.cs` call lists.
3. Table-drive Harmony targets (type, method, args, prefix/postfix/finalizer names, required) and build `SelfCheck` from the same table.
4. Replace string messages with typed messages that serialize through a shared writer, while keeping channels and request-id semantics.
5. Replace per-file timers with scheduler entries.
6. Persist durable identity separately from the transport id.

---

## 8. Suggested port order and risks

### 8.1 Order (smallest useful slice first)

0. Prep (no gameplay): our loader gives Harmony, logger, config, scene/save events, a MonoBehaviour host, `GameBuild`. Write `ITransport`, the channel router (`Register/Send/Dispatch` with the 1024 byte and 4-per-frame limits), request-id helper (`Shared`). Direct IP transport first (TCP, listener on host, handshake carrying `gameTag/modVersion/gameBuild/name`, host relays guest-to-guest). A loopback transport for two-instances-on-one-machine testing is worth having.
1. **Slice 1: session plus players.** `Session` (reduced: host/join/leave, roster, HST announce, BYE), presence (the framework's 125-line `Presence`), `Puppet`/`PuppetBody`/`GameRefs`/`PoseSync`/`SitSync`/`AppearanceSync`/`Look`/`PlayerLook` equivalent. Both players start the same save by hand (same world loaded on both machines) so the join ticket is not needed yet. Chat (`CoopChat`, 538 + 270 lines of draw) is a cheap addition and a good transport test. Delivers: you see each other walk, sit and look.
2. **Slice 2: world clock and weather** (`WorldSync`, `WorldHooks` 5.1), sleep vote (`BedVote`, `TimeSpeed`). 1,693 lines. Low risk, visible result.
3. **Slice 3: join ticket.** `NetBlob`, `WorldShip`, `GuestWorld`, `GuestSession`, `GuestSplice`, `GuestSaveHooks`, `GuestGateHooks`, profile store. 5,012 lines. This removes the "load the same save by hand" step and is the hardest single slice. Do the guest gate in the same step so guests cannot start features that are not shared.
4. **Slice 4: shared world state** in this order: doors/lights (`DoorSync`), floor items and coins (`Item*`), containers, building. These four give most of the "we play in the same base" feeling.
5. **Slice 5: stations and NPC features**: crafting, liquids, power, shops, vending, dialogue, quests.
6. **Slice 6: rest**: trains, elevators, sauna, slots, NPC/animal positions, combat, harvest, prison, map markers, players panel.
7. **Last, or skip:** free roam (authority per scene, hand-off, checkpoint blobs; 2,211 lines plus the `IAmAuthority` branches threaded through build, container, item, elevator). Decide early whether v1 forces everyone into the host's scene; that removes the whole authority-per-scene layer.

### 8.2 Risks

1. **Free roam authority.** Per-scene authority with hand-off and generations is the most complex logic in the mod, and `FreeRoam.IAmAuthority` is tested in many places. Design with a single-authority-per-session model first, keep the `Authority` accessor so free roam can be added later.
2. **Join ticket depends on game save internals.** `GuestSplice` removes named GUID entries from `Globals.tnmt` and renumbers FullSerializer `$id/$ref` (`SpliceIds.cs`); `GuestGateHooks` and `GuestSaveHooks` guard the save pipeline. A game patch can break it silently. Needs fixture saves and the self checks.
3. **Identity.** Steam ID doubles as transport address and durable key for profiles, prisons and world folders. Direct IP has no stable id; invent a persistent player GUID (stored locally, sent in the handshake) and map it to a session peer id. Decide this before slice 3 or the profile/prison files will be keyed wrong.
4. **Topology.** The mod assumes a full mesh with host as authority. A host-only star with relay works for everything except latency of guest-to-guest pose; verify `Session.AreaOf(peer)` and presence still get all peers' data.
5. **Reliability layer.** On UDP we would have to build ordered reliable delivery and large-message handling (`NetBlob` assumes ordered reliable `Send`). TCP is the cheap start; head-of-line blocking with unreliable pose traffic on the same stream will be visible on bad links.
6. **SaveData.Capture/Apply** swaps game statics (`GameSave.Global/Level`) around `OnSavingGame`; the container, station, liquid, interactable and prison features depend on it. This is framework code that needs the publicized assembly; reimplement carefully or isolate behind one class with tests.
7. **Hook surface.** About 180 fixed Harmony targets plus 4 dynamic scans and about 100 reflection lookups (`AccessTools.Field`, `GetField`, `GetMethod`, `GetEvent`) into game privates, all tied to build 0.5.08 (the framework pins `SupportedVersion` and `SupportedBuildId` in `Core.cs:14-15`). Budget for breakage on every game update; keep the table-driven self check.
8. **Legacy Steam API.** The framework uses `ISteamNetworking` P2P, which Valve has deprecated in favour of `ISteamNetworkingMessages`/Sockets. Fine for now; keep Steam as a second `ITransport` implementation and treat it as a future rewrite.
9. **Windows-only UI dependency.** WebUi needs WebView2 and a native DLL. Use IMGUI/uGUI from the start.
10. **Language level and runtime.** The reference stays at C# 5 for old-Mono and BepInEx 5 reasons. Pick our level explicitly and confirm the Unity 6 Mono profile has what we use.
11. **Licensing.** MIT: keep the NomadsReach copyright and licence text in any file we copy or adapt (`LICENSE` in `example-mp/`).
12. **Unverified here.** I did not run the mod, so tick costs, behaviour under packet loss, and whether Steam's reliable mode orders messages across the mixed reliable/unreliable traffic on channel 7 are not confirmed; the mod's design assumes ordering for reliable sends.
