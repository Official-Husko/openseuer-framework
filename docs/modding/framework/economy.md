# Economy

`ObenFramework.Economy.Wallet` reads and changes the player's money. Call it on the main thread. Changing money needs a loaded world; reading works as soon as the game has created its money object.

```csharp
using ObenFramework.Economy;

int coins = Wallet.Coins;            // Open Sewer Coin, whole numbers
float rm = Wallet.RealMoney;         // Real Money, one decimal

if (Wallet.CanAfford(Money.Currency.OC, 50))
    Wallet.TryRemove(Money.Currency.OC, 50);

Wallet.Add(Money.Currency.RM, 2.5f);
Wallet.TryRemove(coins: 20, realMoney: 1.5f);   // both or neither
```

## Rules

- Every change goes through the game's `Money.AddMoney` and `RemoveMoney`, so the HUD, dialogue variables (`Money`, `RealMoney`) and the coin sound stay right. Don't set the fields yourself.
- **Real Money is stored in tenths.** The framework rounds amounts to the nearest tenth (12.34 becomes 12.3) and compares in whole tenths, so `CanAfford(RM, 0.3f)` is true for a balance of exactly 0.3. `Wallet.ToTenths` exposes that conversion if you need it. Open Sewer Coin amounts are rounded to whole numbers.
- `TryRemove` takes exactly the amount or nothing. A negative `Add` removes money, but only down to zero (the game never goes negative), and returns true.
- `Add` returns false when the world isn't loaded or the balance would overflow.
- Items 0 and 1 are the two currencies, not stacks: `PlayerItems.Give/Take/Count` with those ids call the wallet. See [items.md](items.md).

Bridge commands: `money.get`, `money.add`.
