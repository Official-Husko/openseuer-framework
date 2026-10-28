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

The player's own shop (`PlayerShopManager`) is unfinished in the game and not covered.

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

## Bridge commands

- `shops.list` (`kind?`): shops of the scene with their address.
- `shops.stock` (`address`): the stock and current prices of a trader or vending machine.
