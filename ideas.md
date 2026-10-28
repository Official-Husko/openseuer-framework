# Mod ideas

Ideas for mods on top of ObenFramework, each one its own mod under `mods/`. They lean on systems the game already has but uses only partly: tenants with requirements but little life, a crime, addiction and banking layer, and a player shop system that was designed and never finished (see Corner Shop below).

Size is a rough guess: **S** is a few days, **M** a week or two, **L** more. "Built on" names the framework APIs that do the heavy lifting; the guides are in `docs/modding/framework/`.

Where to start:

1. **Storage Organizer** (S): small, everyone wants it, and it puts the container APIs through real use.
2. **Odd Jobs Board** (M): a repeatable gameplay loop, and quest authoring end to end.
3. **Corner Shop** (L): the flagship. It fills in a feature the game left unfinished and pulls NPCs, economy and building together.

## Business and money

### Corner Shop (L)

Run your own shop in the tenement or at a Bazaar stall. NPC customers walk in on their routines, browse your shelves and buy at your prices. You restock, empty the till, and deal with the odd robber.

**The game's unfinished player shop system (0.5.13).** The code is there, but it never runs:

- **Built:**
  - `PlayerShopManager` (a SavableScript, so it is saved with the game) holds `PlayerShopData`: an open flag, residents-only, an invalid-area flag, has-cash-register, the shop's currency, a general price factor, fixed prices per item, uncollected earnings with `CollectEarnings()`, "attractiveness" per item category, items on sale and items sold, and the storages that count as stock.
  - Each building zone's `FurnitureManager` has a `Shop` field. The manager hears furniture being placed or taken and keeps track of cash registers (`InteractableCashRegister`).
  - `NPCCanShop(npc)` decides who may shop, with residents only if the shop is set that way.
- **Missing:**
  - `MakeSale()` and `FindItemAndMakeSale()` are empty.
  - `PlayerShopController.BrowseAndMakePurchase(npc)` always returns false, and nothing calls it or `NPCCanShop`.
  - The cash register's `Interact()` does nothing beyond the base interaction (no shop UI).
  - Attractiveness is never calculated.
  - No level contains a shop manager or a cash register (from a scan of the 0.5.13 level files).

**Approach.** Follow the game's own design (open/residents-only, price factor plus fixed prices, attractiveness per category, sales storages, a cash register) and fill in what's missing:

- a cash register as custom furniture;
- NPC customers on routines who browse the sales storages and buy, weighted by attractiveness and price;
- a shop window at the register (ObenUIKit);
- the till and earnings.

The developers may finish their version in an update. If the mod uses their data model, it can switch to the real system then instead of fighting it. `oben check` would flag the change as soon as those empty methods gain code.

Built on: PlayerShops (our own model, already selling through `Sell()`), mod NPCs with orders and routines, Robbers, Prices, CustomFurniture, Containers, ObenUIKit.

### Moonshine Contracts (M)

Bars and traders phone or mail you orders, like "20 bottles of quality 3 or better by Friday". You're paid by invoice when you deliver. Get caught on the cameras and the police raid you. This gives brewing and distilling a purpose beyond selling to traders.

Built on: PhoneNumbers, Mail, Invoices, crafting quality and events, Law, Surveillance, QuestAuthoring.

### Landlord Office (M)

An app on the computer for running the tenement: a rent ledger, tenant mood, complaints, an eviction planner, and a renovation queue with costs.

Built on: ComputerApps, Tenement, Tenants, Renovations, BankLedger.

### Banking+ (S to M)

Savings plans, loans with a credit score built from your ledger history, and an online banking app. It could include the fix for changing Real Money into coins, which pays nothing at a bank short on coins (see TODO.md, BankExchangeCoins).

Built on: Banks, BankAccounts, BankLedger, ComputerApps.

## Life and content loops

### Odd Jobs Board (M)

Small daily jobs posted on a board or the computer:

- cleaning apartments;
- deliveries by metro or train;
- mining quotas;
- fixing a neighbour's power.

Each is a generated quest with real pay.

Built on: QuestAuthoring, Cleaning, Trains and TrainTickets, PowerDevices, GameClock.After, Wallet.

### Living Tenement (L)

Tenants become people:

- they knock on your door and call with complaints;
- they throw parties and gossip in barks;
- they have relationships with each other, which change how and whether they pay rent.

Built on: mod NPC routines, NpcActions, Relationships, PhoneNumbers, Tenants, DialogueAuthoring.

### Recovery (M)

The other half of the addiction systems: withdrawal effects, support meetings, the doctor, relapse events, and a mental health arc told through dialogue.

Built on: Survival (StatThresholds, StatDrain), Conditions, Medical, DialogueAuthoring, QuestAuthoring.

### Seasons and Holidays (M)

A calendar that changes the world:

- weather and sky by season;
- Juhannus and winter holidays;
- Bazaar festivals with stalls and contests;
- holiday mail.

Built on: WeatherZones, WorldSky, GameClock dates, Contests, Mail, WorldProps.

## World and companions

### Pets and Companions (M)

Tame a rat or another animal. Or take in a roommate NPC who follows you, carries things for you (an extra inventory source) and needs feeding.

Built on: Animals (taming is saved per save), NpcActions.FollowPlayer, InventorySources, Survival.

### Heists (L)

Scout a shop, pick locks, crack keypads, keep out of the guards' sight and the cameras, then fence the goods. There's also the opposite side: bounty jobs from the police against robbers.

Built on: Locks, Keypads, NPC sight holds, Surveillance, Law, Robbers, Traders.

### Underground Courier (M)

Timed parcel runs between districts by metro and train, with parcel lockers as drop points.

Built on: Trains, TrainTickets, ParcelLockers, Travel, GameClock.After, QuestAuthoring.

## Building and stuff

### Furniture Pack(s) (S per pack)

New furniture from OBJ models, sold in furniture shops and found as flatpack loot, plus a blueprint browser. A pack is also the best real test of the model and custom furniture pipeline.

Built on: CustomFurniture, ModelRegistry, FurnitureShops, Blueprints, flatpack loot.

### Greenhouse (M)

New crops and recipes, greenhouse building pieces, autofarm upgrades, and seeds sold by traders.

Built on: ItemRegistry, RecipeRegistry, GrowingStations, Plants, Autofarms, ShopStock.

## Fun

### Radio Obenseuer (S to M)

Custom radio stations with mod music and fake ads, news bulletins tied to what happens in the world, and a guitar songbook.

Built on: Radios, ModSounds, ModAssets, Instruments.

### Casino Nights (M)

New card game variants, a lottery played by mail, and rat-fight form guides with betting tips.

Built on: CardGames, Payouts, Mail, Contests.

## Quality of life

### Storage Organizer (S)

- Search across all containers, including ones in scenes that aren't loaded.
- Sort a container.
- "Take all of this item."

Built on: Containers, UnloadedStorage, StackActions, ObenUIKit lists.

### Better HUD (S)

Need bars, crafting timers, rent-due and invoice reminders, and a compass showing your own map marks.

Built on: Hud.Bar, Hud.Prompt, Landmarks, CraftingEvents, Invoices, Tenement.

### Photo Mode (S)

Freeze time, a free camera, a hidden HUD, screen effects.

Built on: TimeControl, ScreenFx, Hud.HideGameHud, the Game Tweaks camera.

## Weather

The game has named weathers (Heavy Rain and others through `WeatherManager.SetWeather`), conditions for rain, snow, wind, thunder and temperature, weather zones, and an Azure sky with fog and ambient light. It uses little of it for gameplay. These mods give weather consequences. Seasons and Holidays (above) is the calendar side.

### Living Weather (M)

Weather that moves instead of switching:

- fronts cross the districts one weather zone at a time;
- rain builds up and eases off;
- wind gusts, and fog rolls in at night.

A forecast comes on the radio, the TV and a computer app. A barometer item gives the trend.

Built on: WeatherZones, WeatherConditions, GameWeather, WorldSky, Radios, Televisions, ComputerApps, GameClock.

### Sewer Floods (M to L)

Heavy rain raises the water in the Open Sewer districts.

- Flood gates have to be closed in time (or opened to drain).
- Water reaches low shacks: floor items float away and furniture gets damaged.
- Powered pumps clear it afterwards.

Built on: WeatherConditions, FloodGates, WorldWater, FloorItems, PlacedFurniture, PowerDevices, Hud.Prompt, PhoneNumbers (a warning call).

### Rain Barrels (S)

Barrels and tanks left outside fill with rainwater while it rains. That water has to be boiled or filtered before drinking (new recipes). Useful for brewing and for players without a water supply.

Built on: Tanks, WeatherConditions, RecipeRegistry, CraftingStations.

### Wet and Cold (M)

Being out in rain or snow without cover makes you wet:

- hygiene and mood drop, and tiredness rises faster;
- a raincoat or umbrella helps;
- a shower, the sauna or a heater dries you off.

It shows as a status with its own HUD bar.

Built on: Survival (StatDrain, StatThresholds), Conditions, Wardrobe, Saunas, Showers, Hud.Bar, WeatherConditions.

### Thunderstorms (M)

Lightning hits the power grid: lights flicker, a district blacks out until it's fixed, and generators earn their keep. Thunder shakes the camera slightly. In co-op the host owns the thunder roll, which fixes one of the reference mod's limits.

Built on: WeatherConditions (thunder), PowerGrids and PowerDevices, Blackouts, Lights, ScreenFx shake, ModSounds.

### Fallout Storms (M)

Rare storms from the wasteland:

- the sky turns sickly and radiation rises outdoors;
- sirens and a phone warning tell you to get inside;
- a dosimeter and protective gear matter.

Afterwards, contaminated puddles stay as hazards for a while.

Built on: Radiation, Hazards, WorldSky, ScreenFx (vignette), WeatherConditions, PhoneNumbers, Hud.Prompt, the wasteland effects.

### Weather and the City (M)

The city reacts to the weather:

- **NPCs** head for cover when it rains, and their barks mention the weather.
- **Fewer customers** come out in storms.
- **Prices move:** hot drinks sell better in the cold, umbrellas in the rain, fuel spikes after a storm.
- **Robbers** like foggy nights.

Built on: Npcs and NpcControl (walk to cover), Prices, Traders, FuelStations, PlayerShops (Corner Shop), Robbers, WeatherConditions.

### Weather Control (S)

A weather page in the Cheat Menu or the developer menu: pick a weather, lock it, set rain, wind, fog and thunder by hand, and save presets. Handy for photo mode and for testing the mods above.

Built on: GameWeather, WeatherConditions, WeatherZones, WorldSky, TimeControl-style holds, ObenUIKit.

## Co-op

### What ObenCoop doesn't share yet

Today ObenCoop shares the session itself: joining, the player list, chat, lobbies and join codes, and where everyone is (positions, without bodies). Everything else is still each player's own game. ObenCoop will start from the reference co-op mod, which already shares all of this, and replace it piece by piece (`docs/modding/coop.md`, "Plan: start from the reference mod"); the list below is what each piece is replaced with. Most slices now have the framework APIs they need, because the parity work added the events and setters a sync layer listens to and replays. The rule for all of them: requests go to the host, the host checks them and broadcasts the result as plain state ("door X is open"), and periodic digests heal late joiners and lost packets. To keep changes from echoing back, a mirroring mod applies the network's changes through the framework's setters, which run as `ChangeSource.Mod` (or inside `WorldChanges.Enter`), and forwards only events whose source is `Game` or `LocalPlayer`.

1. **Other players' bodies (L):** puppets with body, pose, sitting and appearance, a roster window, and a proper chat UI.
   Built on: presence (done), PlayerLook capture and apply (bytes, onto any puppet), Seats, ObenUIKit.
2. **Clock, weather and the sleep vote (M):** one shared time and weather, and sleeping only when everyone agrees.
   Built on: GameClock, TimeControl (freeze or speed per mod), GameWeather and WeatherConditions, PlayerSleep and SleepEvents.
3. **Join ticket (L, the hardest single slice):** the host's save is shipped to a joining player and loaded in a redirected guest save, so nobody has to copy saves by hand.
   Built on: blob transfer (done), SaveState capture and apply, SaveSlots, SaveGate, Sessions.
4. **Shared world state (L):** doors and lights, floor items and coins, containers, building.
   Built on: Doors (change events, echo-free), Lights and Switches, FloorItems (stable ids), Containers (stable ids, who has one open), PlacedFurniture and FurnitureBuilder (stable ids, place, move and remove from code).
5. **Stations and NPC features (L):** crafting, liquids, power, shops, vending, dialogue, quests.
   Built on: CraftingEvents and CraftingStations, PowerDevices, Shops (Bought/Sold), ShopStock, Conversations, QuestLog with its change events.
6. **The rest (L):**
   - trains, elevators, the sauna, slot machines;
   - NPC and animal positions;
   - combat, harvesting, prison;
   - map markers, a players panel.

   Built on: Trains, Elevators, Saunas, Casino, Npcs and NpcControl, Animals, Weapons, ResourceNodes, Law, Landmarks.
7. **Free roam (last, maybe never):** players in different levels, which needs authority per scene. Decide early whether version 1 keeps everyone in the host's scene; keep an `Authority` accessor so it can be added later.

The reference co-op mod ([NomadsReach/Obenseuer-Coop](https://github.com/NomadsReach/Obenseuer-Coop), local copy in `example-mp/`) shares all of the above, from lobbies to free roam. It still has a few limits of its own, which ours should avoid:

- **Thunder** is rolled on each machine, so players can see different storms (its README). The host should own the roll and broadcast it. Built on: WeatherConditions (thunder state).
- **An exhausted guest never passes out** (its README). The guest's exhaustion should run through the same pass-out path as the host's. Built on: Survival events (passed out, breakdown interception), PlayerSleep.
- **Leaving a session sets your clock back** to where it was when the session started (its README). Keep the time the session reached, or ask. Built on: GameClock.SetDate and SetTime, Sessions.
- **Furniture without a known id can't be moved or removed by guests** ("That piece is not shared in co-op", in its code). Our furniture ids are stable and kept with the save, and a guest can adopt the host's ids. Built on: PlacedFurniture ids, `Assign`.
- **Mod furniture isn't shared** ("Mod furniture is not shared in co-op yet"). Custom pieces are defined by id; both sides need the same mod set, which the handshake already checks. Built on: CustomFurniture, ItemPlaceholders for a missing mod.
- **Recipes added by mods can't be crafted by guests** ("This recipe is not shared in co-op"). Built on: RecipeRegistry (stable hashed ids on both sides).

Smaller open items from the same list:

- **Real Steam:** the Steam transport is only tested against a fake Steam, since this install's `steam_api64.dll` is replaced.
- **No NAT help for direct play:** forward the UDP port, use a VPN, play on a LAN, or use Steam.
- **No lobby page in ObenUIKit** (list, codes, invites); everything is console commands.
- **No request/ack helper** and no timers for periodic state (announces, digests); slice 4 needs both.
- **Blobs** aren't resumable and don't survive a hot reload.
- **No host migration:** a Steam lobby whose owner leaves ends the session.

### Shared Tenement (L)

Co-landlords: rent income split between players, shared storage and a shared quest log. This is the natural next step for ObenCoop once its testing pass is done (see to-test.md, section 4).

Built on: ObenCoop sessions, presence and blob transfer, Tenement, Containers, QuestLog.

### Mod and config sync for co-op (M to L)

ObenCoop is meant for friends playing with friends, so the host's mods and setup go straight to the joiners, as in other games with modded multiplayer. It only goes one way: host to joiners. A joiner never sends mods or settings to the host. That would make no sense (the host's world is the real one) and would let any joiner push code onto the host.

**The setup (the same as Neet's co-op, `mods/Coop/Session/ModSync.cs` there):**

- **The host decides** which mods run, their feature switches, and the settings marked as shared (gameplay values such as Game Tweaks, difficulty, payout and price factors).
- **The joiner follows it live** while in the session. The mod menu is locked for those mods.
- **Nothing is written** into the joiner's own config files, and leaving restores the joiner's own setup.
- **Personal mods and settings stay per player:** key binds, UI, volume, HUD and cosmetic mods marked client-only. Dev-only mods (DevBridge, Script Runner) are never synced.

**The mods themselves (beyond Neet, which still asks the joiner to install the same version):**

1. **The handshake compares** the joiner's mods with the host's shared set by id, version and SHA-256.
2. **The joiner confirms first,** in a window that lists every mod to be synced (name, version, author, size). Its warning, in plain words: mods are programs that run on your computer; only join games of people you trust.
   - Accept downloads the mods; Decline leaves the session.
   - "Always trust this host" skips the window next time, keyed by the host's persistent player id (or Steam id).
3. **The host sends the files directly:** the mod's DLL, `mod.json` and assets, through blob transfer (chunked, CRC-checked, acknowledged). After the transfer the joiner checks every file against the hashes in the host's list. That protects against broken transfers, not against a host you shouldn't have trusted; the confirmation is the protection there.
4. **Synced mods are kept apart** from the joiner's own installation (for example `Mods/.synced/<host id>/<mod>@<version>/`). They load for the session, new ones through the loader's rescan, and updated ones in place of the joiner's own copy only for the session. On leaving they're unloaded and the joiner's own mods come back. A "keep this mod" choice can install one for good.
5. **The loader and the framework itself are not hot-swapped.** If the host runs a different loader or framework version, the joiner is told to update (or the updater below does it) and to join again.

Built on:
- **Already there:** the handshake's mod-set check, blob transfer, ModFeatures overrides, `OwnedValues` for session-only settings, `ModRegistry.Rescan`, hot reload and `LoadedAssemblies.LoadFromMemory`, the persistent player id, ObenUIKit dialogs.
- **New:** a shared/client-only flag in `mod.json`, and a `[Shared]` attribute on config fields, as in Neet.

## Tools

### Auto updater, a companion app in Go (M)

A small standalone program that keeps the loader and the mods up to date. Why Go and not the `oben` tool:

- `oben` needs .NET 10, which players don't have; a Go build is one static binary for Windows and Linux with nothing to install.
- It runs outside the game, so it can replace files the game has open.

`oben` stays the developer tool.

**What it does:**

- **Checks the releases.** It reads GitHub releases or a small index file listing, per component (loader, framework, kit, each mod), the version, download URL, SHA-256 and the minimum loader and game version. `build/release.sh` and `build/package.sh` already produce the packages; they would also write the index.
- **Downloads and verifies.** It checks the SHA-256 and a signature (ed25519, for example minisign), with the public key built into the updater, so a hijacked download host can't push code.
- **Installs safely.**
  - It waits for the game to close, or asks.
  - It installs with the same rules as `oben install`: the two Unity JSON files are only touched by the installer, with `.obenbak` backups.
  - It keeps the previous version of everything it replaces, for a one-step rollback.
- **Installs per mod.** It updates only what changed, respects mods the player switched off, and can add or remove mods from the index. That makes it a small mod manager.
- **Shows what's new.** It shows the changelog lines from `changelog.json` (generated from commit trailers) before and after updating. The in-game What's New popup still shows on the next start.
- **Has a game side.** An "Updates" page in the MODS menu (ObenUIKit) checks the index at the main menu, shows what's available, and launches the updater when you quit. The game never installs anything itself.
- **Finds the game.** On Linux it finds Heroic, umu and Steam installs (the same detection as `oben launch`); on Windows, Steam libraries and a manual path. It would live in `tools/obenupdate/`.

Built on: `build/release.sh` and `build/package.sh`, `oben install`'s rules, `changelog.json`, the What's New page, ObenUIKit.

The two can meet later: when a mod the host runs is also in the release index, the joiner could fetch it from there, verified by signature, instead of from the host. Mods that aren't in the index still come from the host.
