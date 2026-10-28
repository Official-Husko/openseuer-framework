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

## Co-op

### Shared Tenement (L)

Co-landlords: rent income split between players, shared storage and a shared quest log. This is the natural next step for ObenCoop once its testing pass is done (see to-test.md, section 4).

Built on: ObenCoop sessions, presence and blob transfer, Tenement, Containers, QuestLog.
