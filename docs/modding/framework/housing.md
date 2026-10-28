# Housing: the tenement, tenants, hotels, renovations and mail boxes

`ObenFramework.Housing` covers the player's tenement (apartments, tenants, rent, renovations, cleaning), hotels, and the mailboxes and parcel lockers in the world. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Housing;`.

How the game runs the tenement (who changes what, the timers, the upgrade prefabs) is in [../../game/world-building.md](../../game/world-building.md), section 5. Events follow the rules in [world.md](world.md): subscribe with an owner, nothing is raised while a save loads, and payloads carry a `ChangeSource`.

## The tenement

The tenement's state is global: it is saved once and is the same in every level, so `Tenement` works anywhere in a gameplay scene, not only in the building. Apartment 1 is the player's own.

```csharp
foreach (var a in Tenement.Apartments())
    Log.Info($"{a.Number} ({a.Size}, floor {a.Floor}): {a.Tenant?.Name ?? "empty"} pays {a.Rent}");

Tenement.StartEviction(7);          // 18 game hours' notice, like the game
Tenement.CancelEviction(7);
RentAmount income = Tenement.TotalIncome;
int roof = Tenement.UpgradeLevel(TenementController.GeneralUpgradeCategory.Roof);

Tenement.TenantChanged.Subscribe(this, c => Log.Info($"{c.Tenant.Name}: {c.Kind} in {c.Apartment}"));
Tenement.RentPaid.Subscribe(this, total => Log.Info("rent day: " + total));
Tenement.EventStarted.Subscribe(this, e => Log.Info($"{e.NpcId} does {e.Kind}"));
```

`TenantChanged` kinds: `Rented` (signed, moving in), `MovedIn`, `EvictionStarted`, `EvictionCancelled`, `MovedOut`. Evictions are refused for the player's apartment, an empty one, a tenant still moving in, one already under notice and tenants the story keeps.

## Tenants

```csharp
RentRefusal why = Tenants.CheckRent(npcId, 5);     // None, NotATenant, AlreadyRenting, Occupied, WrongSize, NotGoodEnough, ...
Tenants.Rent(npcId, 5, moveInHours: 2f);            // quiet by default
Tenants.Rent(npcId, 5, announce: true);             // the game's rent scene: fade, deposit to the player, achievements
Tenants.TriggerEvent(npcId, TenantEventKind.MakeNoise);
```

An NPC is a tenant when their character has a tenancy profile (`Tenants.Profile(npcId)`). A quiet rent records no deposit: the game would otherwise record one it never paid and take it back from the player when the tenant leaves.

## Hotels

```csharp
foreach (var room in Hotels.Rooms()) { /* Id, HotelId, Name, Free, RentedByPlayer, NightsLeft, PricePerNight */ }
RentAmount price = Hotels.Price(roomId, 3);         // what Rent would charge, today's price factor included
bool ok = Hotels.Rent(roomId, 3);                   // 1 to 7 nights, paid from the wallet
Hotels.RoomChanged.Subscribe(this, c => ...);       // rented by the player, or the stay ran out
```

The player can hold one room per hotel. Hotels seen in the level files: the sleeping pods in Interior Tenement Kolhola and the Interior Mines Barrack Hotel.

## Renovations

```csharp
foreach (var r in Renovations.All()) { /* Id "apartment/upgrade", Name, Apartment (0 = building), State, StartCostOc, FinalCostRm, Hours */ }
float done = Renovations.Progress(id);
if (Renovations.ContractorOpen) Renovations.Start(id);   // pays and takes materials like the upgrade screen
Renovations.Finished.Subscribe(this, info => ...);
```

States: `Locked` (needs another upgrade first, or its line is busy), `Available`, `InProgress`, `AwaitingFinalPayment`, `Installed`. Starting needs the contractor the player is talking to, since the contractor decides what can be built and where the materials come from.

## Cleaning

`Cleaning.StateOf(apartment, out int day)` tells whether an apartment's cleaning job has not started, runs (until game day `day`) or is done. `Cleaning.TrashPiles()` lists the trash piles of the loaded scenes with the items they turn into.

## Mailboxes and parcel lockers

What is in the player's mail is [mail.md](mail.md) (`PlayerMail`). These are the boxes in the world:

```csharp
foreach (var box in Mailboxes.All()) { /* Id, Mode (Receive, Send), Count */ }
int taken = Mailboxes.TakeMail(Mailboxes.Find(id));         // the player's own box
Mailboxes.Used.Subscribe(this, u => ...);                   // letters taken, or mail dropped in a post box

foreach (var locker in ParcelLockers.All()) { /* Slots: Index, Open, Parcel (MailInfo) */ }
int opened = ParcelLockers.Open(locker);                    // waiting parcels go into compartments
ParcelLockers.TakeParcel(locker, 0);                        // into the player's hands
```

Both check that every game system mail can hand things to exists before they touch anything (items, notes, recipes, money, furniture, tasks).

## Dev bridge

`housing.apartments`, `housing.evict`, `tenants.list/check/rent/event`, `hotels.list/rent`, `renovations.list`, `mailboxes.list`, `lockers.list`, `trash.list`.
