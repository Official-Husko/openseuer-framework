# Housing: the tenement, tenants, hotels, renovations and mail boxes

`ObenFramework.Housing` covers the player's tenement (apartments, tenants, rent, renovations, cleaning), hotels, and the mailboxes and parcel lockers in the world. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Housing;`.

How the game runs the tenement (who changes what, the timers, the upgrade prefabs) is in [../../game/world-building.md](../../game/world-building.md), section 5. Events follow the rules in [world.md](world.md): subscribe with an owner, nothing is raised while a save loads, and payloads carry a `ChangeSource` (`RentPaid` is the one exception: rent is always paid by the game's clock). The game's own screens count as the player: the rent screen's buttons, the contractor's upgrade screen, the hotel desk, and using a mailbox or parcel locker report `LocalPlayer`.

Every change here is refused while the game saves or loads: calls that answer `bool` return false, counts return 0, and `Tenants.Rent`/`CheckRent` answer `RentRefusal.Busy`. `Hotels`, `Renovations`, `Mailboxes` and `ParcelLockers` have `IsAvailable`, false when a game update broke their hooks; `Tenement.IsAvailable` tells whether the tenement's state exists, and `Tenement.HooksAvailable` whether the housing hooks are in place.

## The tenement

The tenement's state is global: it is saved once and is the same in every level, so `Tenement` works anywhere in a gameplay scene, not only in the building. Apartment 1 is the player's own.

```csharp
foreach (var a in Tenement.Apartments())
    Log.Info($"{a.Number} ({a.Size}, floor {a.Floor}): {a.Tenant?.Name ?? "empty"} pays {a.Rent}");

Tenement.StartEviction(7);          // 18 game hours' notice, like the game
Tenement.CancelEviction(7);
RentAmount income = Tenement.TotalIncome;
int roof = Tenement.UpgradeLevel(TenementController.GeneralUpgradeCategory.Roof);

Tenement.TenantChanged.Subscribe(this, c => Log.Info($"{c.Tenant.Name}: {c.Kind} in {c.Apartment} ({c.Source})"));
Tenement.RentPaid.Subscribe(this, total => Log.Info("rent day: " + total));
Tenement.EventStarted.Subscribe(this, e => Log.Info($"{e.NpcId} does {e.Kind} ({e.Source})"));
```

`TenantChanged` kinds: `Rented` (signed, moving in), `MovedIn`, `EvictionStarted`, `EvictionCancelled`, `MovedOut`. Evictions are refused for the player's apartment, an empty one, a tenant still moving in, one already under notice and tenants the story keeps.

## Tenants

```csharp
RentRefusal why = Tenants.CheckRent(npcId, 5);     // None, NotATenant, AlreadyRenting, Occupied, WrongSize, NotGoodEnough, Busy, ...
Tenants.Rent(npcId, 5, moveInHours: 2f);            // quiet by default; 0 moves them in at once
Tenants.Rent(npcId, 5, announce: true);             // the game's rent scene: fade, deposit to the player, achievements
Tenants.OpenRentScreen(npcId);                      // the game's own rent screen, where the player picks the apartment
Tenants.TriggerEvent(npcId, TenantEventKind.MakeNoise);
```

An NPC is a tenant when their character has a tenancy profile (`Tenants.Profile(npcId)`). A quiet rent records no deposit, already when `Rented` is raised: the game would otherwise record one it never paid and take it back from the player when the tenant leaves.

`OpenRentScreen` does what agreeing to rent in a conversation does. It works in the tenement's scenes (where its blueprint screen exists), when the player carries the Tenement Blueprint task item the screen asks for, and when no other game menu, shop or the pause menu is open; a conversation may be running.

## Hotels

```csharp
foreach (var room in Hotels.Rooms()) { /* Id, HotelId, Name, Free, Bookable, RentedByPlayer, NightsLeft, PricePerNight */ }
RentAmount price = Hotels.Price(roomId, 3);         // what Rent charges, today's price factor included
bool ok = Hotels.Rent(roomId, 3);                   // 1 to 7 nights, paid from the wallet
Hotels.RoomChanged.Subscribe(this, c => ...);       // rented (at the desk or by a mod), or the stay ran out
```

The player can hold one room per hotel; `Bookable` is false for every room of a hotel where they already have one. Hotels: the sleeping pods in Interior Tenement Kolhola and the Interior Mines Barrack Hotel. The two share their object ids (the barrack hotel is a copy of Kolhola's), so a room id is only unique within its level.

Stays count down once a day after noon, but in 0.5.13 a hotel only sees one new day per visit to its level: after its first check it keeps comparing the clock with itself (TODO.md, HotelDayOnce). A stay therefore only runs out after the player has left the level and come back on a later day.

## Renovations

```csharp
foreach (var r in Renovations.All()) { /* Id "apartment/upgrade", Name, Apartment (0 = building), State, StartCostOc, FinalCostRm, Hours */ }
float done = Renovations.Progress(id);
if (Renovations.ContractorOpen) Renovations.Start(id);   // pays and takes materials like the upgrade screen
if (Renovations.ContractorOpen) Renovations.PayFinal(id); // the final bill in Real Money, which installs it
Renovations.Changed.Subscribe(this, c => ...);          // Started, WorkDone, Finished, with a source
Renovations.Finished.Subscribe(this, info => ...);
```

States: `Locked` (needs another upgrade first, or its line is busy), `Available`, `InProgress`, `AwaitingFinalPayment`, `Installed`. Starting and paying need the contractor the player is talking to (any contractor takes the final bill), since the contractor decides what can be built and where the materials come from. With the game's debug switch for instant upgrades on, `Start` installs at once like the screen does. Upgrades the story sets up silently raise nothing.

## Cleaning

An apartment's cleaning job is started by the story (a conversation starts a 3-day job for the shop, apartment 2). `Cleaning.StateOf(apartment, out int day)` tells whether it has not started, runs (until game day `day`), is done, or that there is no such apartment (`NoSuchApartment`). `Cleaning.Start(apartment, days)` starts one, once per apartment like the game; `Cleaning.Finish(apartment)` ends a running one now. `Cleaning.Changed` reports both, and the game's own finish.

The game finishes a job a day late: it checks at midnight before its day counter has moved on, so a 1-day job ends at the second midnight (TODO.md, CleaningDayLate).

`Cleaning.TrashPiles()` lists the trash piles of the loaded scenes with the items they turn into (`Items`, and `Stacks` with quality, liquid and owner).

## Mailboxes and parcel lockers

What is in the player's mail is [mail.md](mail.md) (`PlayerMail`). These are the boxes in the world:

```csharp
foreach (var box in Mailboxes.All()) { /* Id, Mode (Receive, Send), Count */ }
int taken = Mailboxes.TakeMail(Mailboxes.Find(id));         // the player's own box
Mailboxes.TakeMail(box, notify: false);                     // without the game's toast per letter
int posted = Mailboxes.Post(postBox);                       // the player's letters go out into the world
Mailboxes.Used.Subscribe(this, u => ...);                   // letters taken or posted, by the player or a mod

foreach (var locker in ParcelLockers.All()) { /* Slots: Index, Open, Parcel (MailInfo) */ }
int opened = ParcelLockers.Open(locker);                    // waiting parcels go into compartments
ParcelLockers.TakeParcel(locker, 0);                        // into the player's hands
ParcelLockers.TakeParcel(locker, 0, notify: false);
ParcelLockers.Used.Subscribe(this, u => ...);               // Claimed (with a count) or Taken (with the parcel)
```

A post box's count leaves out mailable packages, which it doesn't take. `Open` puts each waiting parcel into a free compartment it fits; when the locker is full the rest keep waiting. Both check that every game system mail can hand things to exists before they touch anything (items, notes, recipes, money, furniture, tasks).

## Dev bridge

`housing.apartments`, `housing.evict`, `housing.events`, `tenants.list/check/rent/screen/event`, `cleaning.set`, `hotels.list/rent`, `renovations.list/start/pay`, `mailboxes.list/use`, `lockers.list/use`, `trash.list`.
