# Shops, traders and prices

`ObenFramework.Economy` also covers the places the player spends money: list the shops of the loaded scene, change what they charge, put goods into them and react to purchases. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Economy;`. Call everything on the main thread. The player's own money is in [economy.md](economy.md), mail in [mail.md](mail.md).

How the game does this, and why the hooks sit where they do, is in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) (section 7).

## The five ways to buy

| Kind (`ShopKind`) | Game class | How it works |
|---|---|---|
| `Trader` | `Trade` | NPC trader. Items move between the player's inventory and the trader's storage. Sells at 120% of an item's value, buys at 90% (per trader). |
| `Market` | `MarketShop` | Supermarket checkout. Goods ride a conveyor, the cashier scans them, the player pays the total. |
| `Furniture` | `FurnitureShop` | Furniture shop, 110% to buy, 90% to sell. A piece can cost Open Sewer Coin and Real Money at once. |
| `Train` | `TrainShop` | The same screen for train cars. |
| `Vending` | `VendingMachine` | A fixed list of stocked items. |

The game's own player shop (`PlayerShopManager`) is unfinished: no scene or furniture has one, and it never sells. The framework's [player-run shops](#player-run-shops) replace it.

## Listing shops

```csharp
foreach (var shop in Shops.All())
    Log.Info($"{shop.Kind} {shop.Name} {shop.Id}");

var trader = Shops.Find(address);        // address text, GUID, hierarchy path, #instanceId or name
var open = Shops.Open;                   // the trader whose trade window is open, or null
var shop = Shops.Of(someComponent);      // null when the component isn't a shop

foreach (var line in Shops.Stock(trader)) // traders and vending machines
    Log.Info($"{line.Item.Title} x{line.Amount} for {line.Price} {line.Currency}");
```

`ShopInfo.Id` is the shop's [`ObjectAddress`](world.md) as text (`scene|Type|guid|path`). The game gives traders, vending machines, checkouts and furniture shops a GUID in the editor, so the address survives restarts and is the same on every machine. Names are not unique: all vending machines are called "Vending Machine". `ShopInfo.Matches(key)` is true for the address, the GUID or the name.

`Shops.All()` scans the scene; use it for setup and commands, not per frame. A `ShopInfo` is cached per scene, so it can be a dictionary key until the level changes.

## Prices

Register a modifier. It receives the item, the shop and the price so far, and returns the new price:

```csharp
Prices.Register(this, (item, shop, price) =>
    shop.Kind == ShopKind.Vending ? price * 2f : price);
```

- Modifiers of all mods run in registration order, each getting the previous result. Disposing the returned handle, or unloading your mod, removes yours.
- The price is **per unit**, in the shop's currency. The result is rounded the way the game rounds that path (whole coins; Real Money in tenths at traders, whole numbers at vending machines, checkouts and furniture shops) and kept at least 1 OC or 0.1 RM. A modifier cannot make something free.
- It applies to everything the game does with that price: the number shown, what is charged, the trader's "can afford" checks, receipts. Traders, checkouts, vending machines, furniture and train shops are all covered.
- The same registration decides both directions at traders. To tell them apart use the full form: `Prices.Register(this, q => ...)` where `q` is a `PriceQuery` with `Shop`, `Item`, `Furniture`, `PlayerBuys`, `Currency` and `Price`. `PlayerBuys` is false when the shop pays the player.
- `item` is null in furniture and train shops (`q.Furniture` is set instead). A modifier that throws is skipped for that price and logged once.
- Do not ask the shop for prices from inside a modifier; the framework returns unmodified prices there so it cannot loop.
- `Prices.Of(shop, item)` is the price the player pays right now for one of an item at a trader, vending machine or checkout (null when it isn't sold).

Prices are not saved: register in `OnLoad` every session.

## Adding stock

```csharp
// keep a trader stocked with 5 of an item (mod items work the same way)
ShopStock.AddToTrader(this, traderAddress, itemId, 5);

// sell an item from every vending machine named "Vending Machine" for 8 OC, up to 10 in stock
ShopStock.AddToVending(this, "Vending Machine", item, stock: 10, price: 8f);
```

Both take an address, GUID or name; a name applies to every shop that has it. They return a handle and are removed when your mod unloads (the game never sees stale mod items after a hot reload). You can add before the scene with the shop is loaded; the stock is placed when the shop appears. Adding the same item to the same shop again replaces the earlier entry. Invalid arguments (unknown item, amount below 1) throw.

- **Traders** hold goods in their storage's slots, so the stock fills empty slots only, and a full trader skips the rest (a warning is logged once). Stock goes in once per save, then every time the trader restocks it is topped back up to your amount. What the player buys in between stays bought. Removing the stock takes back up to what this session put in.
- **Vending machines** get a real entry in the machine's list: it refills with the machine and sells at your fixed price (price 0 or less uses the game's default). If the machine already sells the item, its own entry is kept and a warning is logged. The remaining stock is saved with the save in the framework's mod data, not in the game's file, because the game saves the list by position.
- `ShopStock.Remove(this, shop, itemId)` removes one entry now; `ShopStock.Entries()` lists everything registered.

## Events

```csharp
Shops.Bought.Subscribe(this, deal =>
    Log.Info($"{deal.Item?.Title ?? deal.Furniture?.title} x{deal.Amount} for {deal.Price} {deal.Currency} at {deal.Shop.Name} ({deal.Path})"));

Shops.Restocked.Subscribe(this, shop => Log.Info(shop.Name + " has new goods"));
```

- `Shops.Bought` is a purchase by the player, `Shops.Sold` a sale to a trader or furniture shop. Both carry a `ShopDeal`: `Shop`, `Path` (the `ShopKind`), `Item` (null for furniture), `Furniture`, `Amount`, `Oc`, `Rm`, plus `Price` and `Currency` for the usual single-currency deal. Furniture can cost both currencies, so check `Oc` and `Rm` there.
- It is raised after the game finished the deal, whichever path was used, and only for the local player. Deals already include `Prices` modifiers.
- A supermarket checkout reports one `Bought` per kind of item on the conveyor, with the unrounded scan prices; the charged total is rounded once, so the sum can differ from it by less than 1.
- A free piece of furniture changes no balance and is not reported.
- `Shops.Restocked` fires when a trader's storage refills, a vending machine refills or a furniture/train shop draws new goods, not while a save loads.

Handlers run in their own try/catch and nothing is raised, or allocated, while nobody subscribes.

## Traders

`Traders` has what `Shops` doesn't about NPC traders: their money and terms, and per-mod price factors.

```csharp
foreach (var t in Traders.All())
    Log.Info($"{t.Shop.Name}: {t.Money} {t.BuysFor} to buy with, sells at {t.Markup:P0}, buys at {t.Payout:P0}, open {t.Open}");

Traders.SetMoney("Brom's", 5000);      // what the trader can pay the player
Traders.RestockMoney("Brom's");        // refill it the game's way (random in its range, scaled by difficulty)

// This mod's factors: the player pays 10% less at Brom's and gets 20% more everywhere.
Traders.SetPriceFactors(this, "Brom's", buy: 0.9f, sell: 1f);
Traders.SetPriceFactors(this, null, buy: 1f, sell: 1.2f);
Traders.ClearPriceFactors(this, "Brom's");
```

- Price factors are per mod and per trader (or `null` for every trader), kept in the framework's shared factor table: different mods' factors multiply, the same mod and trader again replaces its pair, and a mod's factors go when it unloads. They scale the game's price first; `Prices` modifiers then see the scaled price. They are not saved: set them in `OnLoad`.
- `SetMoney` does nothing (and returns false) while the difficulty gives traders infinite wealth.
- `TraderInfo` also has the categories a trader takes or refuses, whether it buys stolen goods and at what factor, and whether it is a scripted trader (`Special`).

## Opening hours

NPC traders open and close by their hours, checked every game minute (and not while the player is standing at the counter). Vending machines and furniture shops follow no clock; a checkout can be locked.

```csharp
ShopDay[] week = ShopHours.Hours("Brom's");    // Monday first; null for anything but a trader
bool openNow = ShopHours.IsOpen("Brom's");      // traders: open flag; checkouts: not locked; others: true
bool shouldBe = ShopHours.IsScheduledOpen("Brom's");

ShopHours.Opened.Subscribe(this, shop => Log.Info(shop.Name + " opened"));
ShopHours.Closed.Subscribe(this, shop => Log.Info(shop.Name + " closed"));
```

A day whose opening and closing hours are equal is closed all day; a closing hour before the opening hour runs past midnight.

## Checkouts and furniture shop money

```csharp
foreach (var c in MarketShops.All())
    Log.Info($"{c.Name}: {c.State}, total {c.Total} {c.Currency}, locked {c.Locked}");
MarketShops.SetLocked(checkoutId, true);
MarketShops.Paid.Subscribe(this, pay => Log.Info($"paid {pay.Total} {pay.Currency} at {pay.Shop.Name}"));

foreach (var f in FurnitureShops.All())
    Log.Info($"{f.Shop.Name}: can pay {f.Coins} OC / {f.RealMoney} RM");
FurnitureShops.SetMoney(shopId, coins: 20000, realMoney: 2000);
FurnitureShops.Restock(shopId);          // new goods and money now; RestockMoney for money only
```

`MarketShops.Paid` is one event per payment with the charged total; `Shops.Bought` has the goods line by line.

## Fuel stations

The minitrain fuel station (one in the game, in Open Sewer Mines, with two pumps) charges per litre, at a price it changes now and then between its minimum and maximum.

```csharp
foreach (var station in FuelStations.All())
    Log.Info($"{station.Name}: {station.Price} {station.Currency}/l ({station.MinPrice}-{station.MaxPrice}), owed {station.ToPayCoins}");

FuelStations.SetPrice(stationId, 10);   // until the station picks its next price
FuelStations.PriceChanged.Subscribe(this, s => Log.Info("fuel now " + s.Price));
FuelStations.FuelBought.Subscribe(this, f => Log.Info($"{f.Litres:0.0} l for {f.Cost} {f.Currency}"));
```

## Player-run shops

The framework's own shops for the player: a set of shelves, an open sign, a markup or fixed prices, a currency and a till. Your mod decides who buys (simulated customers, a shop worker, a mail-order service) and calls `Sell`; the money waits in the till until it is collected. Shops are stored per save (`mods/ObenFramework.player-shops.json`), so they outlive the mod that made them.

```csharp
PlayerShops.Create("kiosk", "Corner Kiosk", Money.Currency.OC);
PlayerShops.SetArea("kiosk", areaGuid);                 // every container in a building area is a shelf
PlayerShops.AddContainer("kiosk", Containers.IdOf(fridge)); // plus single containers
PlayerShops.SetMarkup("kiosk", 1.5f);                   // 150% of the item's sale value
PlayerShops.SetPrice("kiosk", itemId, 12);              // or a fixed price
PlayerShops.SetOpen("kiosk", true);

foreach (var line in PlayerShops.Stock("kiosk"))
    if (line.Price > 0 && customerWants(line.Item))
        PlayerShops.Sell("kiosk", line.Container, line.Slot, line.Item.ID, 1);

PlayerShops.Sold.Subscribe(this, sale => Log.Info($"sold {sale.Amount} {sale.Item.Title} for {sale.Total}"));
PlayerShops.CollectEarnings("kiosk");                    // the till goes to the wallet
```

- **Shelves** are the containers inside every piece of furniture of a building area (the game's `FurnitureManager`, by GUID: the apartment, a workshop, a shop space) plus containers added one by one by their `Containers.IdOf` id. A trader's storage is never a shelf. Only shelves in loaded scenes can sell; `PlayerShopInfo.ShelvesLoaded` counts them.
- **Prices**: an item's sale value times the markup (1.2 by default), rounded to whole coins; a Real Money shop asks a tenth of that, in tenths. That is the rule of the game's own unused player shop. A fixed price is in the shop's currency; 0 takes the item off sale.
- **Sell** checks the shop is open, the slot holds that many of the item and it has a price, then takes the goods off the shelf and puts the total in the till. It returns a `ShopSaleResult`. Stolen goods sell too; `PlayerShopSale.Stolen` says so.
- **The till** keeps both currencies. `CollectEarnings` pays it out with the game's "Money added" note; `AddEarnings` and `TakeEarnings` move money in and out (tips, wages). `Delete` refuses while the till has money, unless forced.

## Bridge commands

- `shops.list` (`kind?`): shops of the scene with their address.
- `shops.stock` (`address`): the stock and current prices of a trader or vending machine.
- `traders.list`, `traders.money` (`shop`, `amount?`, `restock?`), `shops.hours` (`shop`)
- `markets.list`, `markets.lock` (`shop`, `locked`), `furnitureshops.list`
- `fuel.stations`, `fuel.price` (`station`, `price`)
- `playershops.list`, `playershops.create` (`id`, `name?`, `currency?`), `playershops.set` (`id`, `open?`, `markup?`, `area?`, `addContainer?`, `removeContainer?`, `item?` + `price?`), `playershops.stock` (`id`), `playershops.sell` (`id`, `container`, `slot`, `item`, `count?`), `playershops.collect` (`id`), `playershops.delete` (`id`, `force?`)
