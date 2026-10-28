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

`Wallet.Changed` reports every balance change, whoever made it (a shop, a bank, a quest, another mod), once per currency, after the change:

```csharp
Wallet.Changed.Subscribe(this, c => Log.Info($"{c.Currency}: {c.Before} -> {c.After} ({c.Delta:+0.#;-0.#})"));
```

Bridge commands: `money.get`, `money.add`.

## Banks

Banking is in two parts. `Banks` works at a branch of the loaded scene (the game's `Bank`, the clerk's window); `BankAccounts` works on the player's accounts from anywhere, which is what online banking, card payments and mail-order services need. Several branches can share a bank name and then share one account; accounts are keyed by that name (`Stalburg Bank`).

The branches in 0.5.13, read from the scene files (`bank.branches` lists what the loaded scene really has):

| Scene | Branch | Services |
|---|---|---|
| Open Sewer Bazaar, Open Sewer Tenement, Interior Bazaar Painting Shop | Stalburg Bank | withdrawals only, Real Money |
| Interior Tenement Kolhola | Money exchange | exchange (coin supply 10000, daily spending 4000) |
| Interior Bazaar Mentti B | Penni-Pena (the pocket bank) | exchange |
| Interior Bazaar Casino | Casino | exchange, at better rates (0.08 RM per coin, 10.3 coins per RM) |
| Interior Player Tenement, Interior Bazaar Clock Tower | Test Bank, Test Bank 2, Test Bank 3w | the developers' test branches (deposit, both, exchange) |

No branch the player reaches in normal play takes deposits; `BankAccounts.DepositFromWallet` is the way in for mods that want one.

```csharp
foreach (var bank in Banks.All())
    Log.Info($"{bank.Name} {bank.Services} supply {bank.CoinSupply} OC / {bank.RealMoneySupply} RM, account: {bank.Account}");

BankResult r = Banks.OpenAccount("Stalburg Bank");     // name, GUID or address
r = Banks.Withdraw("Stalburg Bank", 0, 25f);
r = Banks.Deposit(branch, coins: 100, realMoney: 0f);   // only where the branch takes deposits

ExchangeQuote? q = Banks.Quote("Money exchange", 100f, toRealMoney: true);   // what 100 OC would buy, changes nothing
r = Banks.Exchange("Money exchange", 100, toRealMoney: true);

int? max = Banks.MaxLoan("Stalburg Bank");             // in the bank's loan currency, null without an account
r = Banks.TakeLoan("Stalburg Bank", 500);
r = Banks.RepayLoan("Stalburg Bank", 100);
```

- Everything goes through the game's own bank methods, so supplies, notifications and the wallet behave exactly as at the desk. That includes **corruption**: a branch can lose up to its `Corruption` share of a deposit. Read the account afterwards for what arrived.
- **Nothing happens partly.** The game quietly does less when the bank or the wallet is short; the framework refuses instead (`Insufficient`, `BankShort`). Every call returns a `BankResult`, and anything but `Ok` changed nothing.
- **Services** follow the branch's window: deposits only where it offers deposits (`Deposit`, `Both`), exchange only at an exchange desk (`Exchange`, `Both`), withdrawals everywhere but exchange-only desks.
- **Exchange** is refused when it would pay back less than 1 (the game would take the money for nothing). `Quote` is worked out the way the exchange itself works, which differs from the game's own quote method (that one checks the wrong supply in one direction). Keep in mind the game's rates: the rate drops while the bank is short of the currency it pays out, and the Real Money to coin rate falls to **0** whenever the bank's coin supply is below its daily spending (a whole-number division in the game); `Quote` shows that, and `Exchange` then refuses with `TooSmall`.
- **Loans** are in the branch's `LoanCurrency`. A bank that lends without an account (`LoanWithoutAccount`) opens one for the loan, as the clerk's dialogue does. `RepayLoan` pays whole units through the game's own method; a Real Money debt left with hundredths (interest rounds to two decimals) is cleared with the next tenth up.

### Accounts from anywhere

```csharp
foreach (var account in BankAccounts.All())
    Log.Info(account.ToString());

bool card = BankAccounts.HasDebitCard;                       // the bank's debit card task item
BankAccounts.Charge("Stalburg Bank", Money.Currency.RM, 12.5f); // an online purchase
BankAccounts.Credit("Stalburg Bank", Money.Currency.OC, 300);   // wages, a refund
BankAccounts.DepositFromWallet("Stalburg Bank", Money.Currency.RM, 20f); // wallet -> account
BankAccounts.WithdrawToWallet("Stalburg Bank", Money.Currency.RM, 5f);   // account -> wallet
BankAccounts.PayInvoice("Stalburg Bank", invoiceId);            // pay an invoice from the account
BankAccounts.RepayDebt("Stalburg Bank", Money.Currency.RM, 50f); // loan debt from the balance
BankAccounts.WithdrawByPost("Stalburg Bank", Money.Currency.OC, 200, fee: 10); // 190 OC arrive by letter
BankAccounts.SetFrozen("Stalburg Bank", false);
```

Accounts keep coins whole and Real Money to two decimals. A frozen account refuses everything but `Credit` (the game's own credits ignore the freeze too). `PayInvoice` does what the payment machine does: runs the invoice's paid action (a loan instalment lowers the debt), removes it and raises `Invoices.Paid`. `WithdrawByPost` takes nothing if the letter can't be sent.

### Ledger

The game keeps no history of an account; the framework does, per save, in its own mod data (`mods/ObenFramework.bank-ledger.json`), at most 500 entries per bank. It starts the first time it sees a save, with the balances the accounts had then (`Opening`), and records every deposit, withdrawal, loan, repayment, weekly interest (with the BankInterest fix of Unofficial ObenPatches), loan invoice payment, subscription fee, forged-invoice payout, freeze, the lawyer unfreezing, and everything done through `BankAccounts`. A change no hook explains (a console command, another mod writing the account) is recorded as `Other` the next time anything is reconciled.

```csharp
foreach (var e in BankLedger.Entries("Stalburg Bank", max: 20))
    Log.Info($"day {e.Day} {e.Kind}: {e.Coins:+0;-0} OC {e.RealMoney:+0.##;-0.##} RM -> {e.CoinsAfter} OC {e.RealMoneyAfter} RM");

BankLedger.Added.Subscribe(this, e => Log.Info($"{e.Bank}: {e.Kind}"));
```

Page further back with `Entries(bank, max, beforeId: smallestIdSoFar)`.

Bridge commands: `bank.branches`, `bank.accounts`, `bank.ledger` (`bank`, `max?`, `before?`), `bank.quote` (`bank`, `amount`, `to`), `bank.exchange` (`bank`, `amount`, `to`: `rm` or `oc`), `bank.open`, `bank.deposit` / `bank.withdraw` (`bank`, `oc?`, `rm?`), `bank.loan` (`bank`, `amount?`; without an amount it shows the maximum), `bank.repay`, `bank.charge` (`bank`, `currency`, `amount`, `credit?`), `bank.transfer` (`bank`, `currency`, `amount`, `to`: `account` or `wallet`), `bank.freeze` (`bank`, `frozen`), `bank.post` (`bank`, `currency`, `amount`, `fee?`, `delay?`).

## Invoices

Rent, loan instalments, legal fees and other bills arrive in the mail as invoices. They are paid at an invoice payment machine, from a bank account, or by a mod. One that expires unpaid becomes an administrative crime worth about its amount (a tenth of it for coin invoices) at the next day change; the game mails a reminder in the last days.

```csharp
foreach (var invoice in Invoices.All())
    Log.Info($"#{invoice.Id} {invoice.Debtor}: {invoice.Amount} {invoice.Currency}, {invoice.DaysLeft} days left");

InvoiceInfo sent = Invoices.Send("Pest Control Ltd", Money.Currency.OC, 250, days: 7);
bool paid = Invoices.Pay(sent.Id);          // from the wallet, like the machine
bool gone = Invoices.Cancel(sent.Id);       // plain invoices only
InvoiceInfo fired = Invoices.FireSender(senderGuid);   // one of the game's scene invoice senders

Invoices.Paid.Subscribe(this, e => Log.Info($"paid {e.Invoice} from {e.Payer}"));
Invoices.Expired.Subscribe(this, e => Log.Info($"{e.Invoice.Debtor} expired, now a crime"));
```

- `Issued`, `Paid` (with `Payer`: `Wallet` or `BankAccount`), `Cancelled` and `Expired` cover every invoice, the game's and mods'.
- `Cancel` refuses invoices that do something when paid or when they expire (`HasPaidAction`, `HasExpiredAction`: loan instalments, legal fees), since skipping them would skip the game's bookkeeping.
- Amounts are whole numbers; a Real Money invoice is for whole Real Money.

## Legal services

The lawyer (Samuel Godman, through dialogue) wipes crimes and unfreezes bank accounts for a fee. Ordering a service mails a fee invoice and starts a timer; when it runs out the service takes effect. A fee invoice that expires unpaid becomes debt with the lawyer, which he bills later at twice the amount.

```csharp
foreach (var action in LegalServices.Actions())
    Log.Info($"{action.Name}: {action.Kind} {action.Target}, {action.PriceCoins} OC / {action.PriceRealMoney} RM, can do: {action.CanTrigger}");

LegalServices.Queue("UnfreezeAccount_Stalburg Bank");
LegalServices.Completed.Subscribe(this, e => Log.Info(e.Name + " done"));
var owed = LegalServices.Debt();             // invoice name -> money
```

The game has two services: `ClearCrime_All` (the fee grows with the crimes on record) and `UnfreezeAccount_Stalburg Bank`. `Queue` refuses a service that is already ordered or has nothing to do.

Bridge commands: `invoices.list`, `invoices.send` (`debtor`, `currency`, `amount`, `days?`), `invoices.pay` (`id`, `bank?`), `invoices.cancel` (`id`), `legal.list`, `legal.queue` (`name`).

## Shops, mail and gambling

Changing prices, listing and stocking shops, traders, opening hours, checkouts, fuel stations, player-run shops and reacting to purchases are in [shops.md](shops.md). Sending the player letters and parcels is in [mail.md](mail.md). Casino machines, card games, contests and payout multipliers are in [gambling.md](gambling.md).
