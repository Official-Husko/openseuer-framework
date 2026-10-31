# Gambling

`ObenFramework.Gambling` covers the game's gambling: the casino's machines and scratch cards, the card tables, the drinking and sauna contests and the rat fights, plus multipliers on what they pay out. Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Gambling;`. Call everything on the main thread. Every event's handlers run in their own try/catch, and the hooks behind them return at once while nobody listens.

## Where it is

Found by reading the scene files of 0.5.13:

| Scene | What |
|---|---|
| Interior Bazaar Casino | 10 slot machines, the pajatso, a card table |
| Interior Bazaar Books | a card table |
| Interior Bazaar Bar | the drinking contest, a slot machine |
| Interior Bazaar Sauna | the sauna contest (4 opponents) |
| Interior Bazaar Ratfighting Arena | the rat fights |
| Interior Tenement O Market, Speakeasy, Open Sewer Metro, Interior Mines Barrack Hotel, Interior Bazaar Mentti B, J Kiosk, Mallment, Open Sewer Mines | a slot machine each |
| Test Map Card Games | a card table (a test level) |

## Casino machines

```csharp
foreach (var m in Casino.Machines())
    Log.Info($"{m.Kind} stake {m.Stake}, {m.MoneyIn} coins in, {m.Winnings} waiting");

Casino.CoinsInserted.Subscribe(this, e => Log.Info($"{e.Amount} coins into {e.Machine.Kind}"));
Casino.Played.Subscribe(this, e => Log.Info($"played {e.Amount} at {e.Machine.Kind}"));
Casino.Won.Subscribe(this, e => Log.Info($"won {e.Amount}"));
Casino.Doubled.Subscribe(this, e => Log.Info($"double or nothing: {e.Before} -> {e.Amount}"));
Casino.CashedOut.Subscribe(this, e => Log.Info($"took {e.Amount} out"));
Casino.Scratched.Subscribe(this, card => Log.Info($"{card.Card.Title}: {(card.Won ? card.Prize.ToString() : "nothing")}"));

CasinoEvent? last = Casino.LastWin;   // the last slot or pajatso win this session
GamblingStats stats = Casino.Stats;   // total won and lost, gambling addiction and urge
bool hooked = Casino.IsAvailable;     // false when the gambling hooks couldn't be put in place
```

Each event carries the machine as it was at that moment (`Machine.Stake`, `MoneyIn`, `Winnings`).

- A **slot machine** takes coins at its coin slot first (`CoinsInserted`; they sit in the machine), each play takes the stake from them (`Played`), a win (`Won`, after payout multipliers) can be redeemed into the machine or doubled on a coin toss, and cashing out pays everything to the wallet (`CashedOut`, describing the machine before it resets). `Doubled` has what went in (`Before`) and what came out (`Amount`): twice as much, 0 for a lost toss, or the same for a coin on its edge, which ends the doubling but keeps the coins in the machine. A won toss raises `Doubled`, not `Won`.
- The **pajatso** takes coins at its slot (`CoinsInserted`) and one per launch (`Played` with 1); a coin through a winning gate drops coins from the gutter onto the win tray (`Won`); collecting pays the tray out (`CashedOut`). If the gutter has run out of coins the gate pays nothing, as in the game.
- A **scratch card** draws its prize on use and spawns a receipt a frame later; a winning receipt is cashed in by talking to the seller. `Scratched` has the card, the prize (coins, or Real Money on a Real Money card), the receipt item and whether it is in Real Money. The game has two: OC Pot (17740, up to 50 000 OC) and Spade of Aces (17760, up to 10 000 RM).

## Card games

China Hand and Ristiseiska are played against NPCs at a card table, for money or, at a table set up for it, for an item (no table in 0.5.13 is; the bookshop's table plays for nothing). The game's dialogue calls them "Shittypants" and "Crosseven".

```csharp
foreach (var t in CardGames.Tables())
    Log.Info($"{t.Kind}: {t.Players} players, playing {t.Playing}, stake {t.Stake} {(t.BetItem != null ? t.BetItem.Title : t.Currency.ToString())}");

CardGames.Started.Subscribe(this, t => Log.Info($"{t.Kind} for {t.Stake}"));
CardGames.Ended.Subscribe(this, r => Log.Info($"place {r.Placement}, paid {r.Payout} for a stake of {r.Stake}"));
CardGames.GaveUp.Subscribe(this, t => Log.Info($"gave up {t.Kind}, {t.Stake} lost"));
```

- `Started` fires when the cards are dealt, about a second after the stake was taken, also at a table that plays for free (stake 0).
- The table pays back twice the stake for first place, 1.2 times for second, 0.8 times for third and nothing for fourth.
- Getting up during a game gives it up: `GaveUp`, the stake is lost and nothing is paid.
- `NetWinnings` is the framework's own count of payouts minus stakes since the player sat down, back to 0 when they get up. The table's own count adds the stake back instead of the payout unless ObenPatches' CardTableNetWinnings is on, so it isn't used.

## Contests

```csharp
foreach (var c in Contests.Active())
    Log.Info(c.ToString());                    // "Drinking Contest vs ...", "Rat fight"

Contests.Started.Subscribe(this, c => Log.Info(c.Name + " started"));
Contests.Finished.Subscribe(this, r => Log.Info($"{r.Contest.Name}: {(r.PlayerWon ? "won" : "lost")} {r.Winner}"));
Contests.BetPlaced.Subscribe(this, b => Log.Info($"bet {b.Amount} {b.Currency} on rat {b.Fighter}"));
Contests.WinningsCollected.Subscribe(this, w => Log.Info($"collected {w.Amount} {w.Currency}"));

float left = Contests.OpponentTolerance();     // 0..1 for the drinking or sauna opponent, -1 when none
Contests.GiveUp();                              // end the drinking or sauna contest as a loss
ContestResult? last = Contests.LastResult;      // how the last contest or rat fight round ended
```

- The drinking and sauna contests pay through their dialogue, not the framework, so their winnings can't be scaled here.
- Every rat fight round fought raises `Started` and `Finished`, with or without a bet of the player's; `PlayerWon` is true only when the player's rat won.
- A won bet pays the stake times the number of rats (4 in the arena); `Finished` carries what waits to be collected.
- The bookie keeps offering bets during the betting time, but a second bet in the same round replaces the first in the game: both are charged and only the last one counts (RatBetOverwrite in TODO.md). `BetPlaced` reports each.

## Payout multipliers

```csharp
var handle = Payouts.Set(this, GambleKind.SlotMachine, 2f);   // slot machine wins doubled
Payouts.Set(this, GambleKind.All, 0.5f);                      // and every game's winnings halved
float now = Payouts.Multiplier(GambleKind.SlotMachine);       // 1.0 with just this mod
handle.Dispose();                                             // or Payouts.Remove(this, GambleKind.SlotMachine)
```

- Games: `SlotMachine`, `Pajatso`, `ChinaHand`, `Ristiseiska`, `RatFight`, `Scratchcard`, or `All`. A game's multiplier is every mod's `All` multipliers times every mod's for that game, capped at 10. Each mod's multipliers go when it unloads (`Payouts.Clear(this)` takes them all away earlier); they are not saved, so set them in `OnLoad`.
- They scale wins where the game works them out: slot machine wins (before doubling), pajatso gate wins, card table payouts (money or the bet item), rat fight winnings and scratch card prizes. The game's gambling statistics (and so gambling addiction and mood) count what was actually paid; stakes and losses stay as they were.
- Results are rounded to whole coins (Real Money rat fight winnings to tenths). A multiplier of 0 pays nothing: a pajatso win leaves the tray dark, a rat fight that pays nothing goes straight back to taking bets, and a winning scratch card's receipt is worth 0.
- A scratch card's prize is a share of the card's top prize, so the top prize is scaled for the length of the draw and put back afterwards; the prize comes out within a coin of the scaled one, and the receipt and the statistics agree with it.
- The pajatso's gutter still drops the game's number of coins; the tray pays the scaled number.
- The card table's payout happens inside the compiler-generated state machine of the game's `async` `OnGameFinished` (`<OnGameFinished>d__66` in 0.5.13). The framework doesn't touch that: its hook on the method works the payout out the same way and scales exactly that one `Money.AddMoney` or `ItemOperations.AddItems` call, so a game update that renames the state machine changes nothing here.

## Bridge commands

- `casino.machines`: machines of the loaded scene, the last win and the gambling record.
- `cards.tables`: card tables and their games.
- `contests.list`: contests going on and the opponent's tolerance.
- `gambling.payout` (`game?`, `multiplier?`, `clear?`): the multipliers in force; with both arguments it sets the bridge's own multiplier for that game (1 removes it).
- `gambling.events` (`clear?`): recorded casino, card game and contest events.
