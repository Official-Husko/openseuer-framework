# Mail

`ObenFramework.Economy.PlayerMail` sends the player letters and parcels through the game's own `MailController`, lists what is waiting or on its way, and raises an event when something arrives. `using ObenFramework.Economy;`, main thread, a world must be loaded to send.

How the game's mail works (mailbox, parcel locker, in-transit timers) is in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) (section 5.1).

```csharp
// a letter with an item attached
PlayerMail.Send("Back rent", "Here is what we owe you.", new[] { new MailAttachment(item, 3) }, oc: 50);

// the same for one item
PlayerMail.Send("A gift", "Enjoy.", item);

// a parcel for the locker, arriving in two game hours
PlayerMail.Send("Delivery", null, new[] { new MailAttachment(item, 10) }, MailKind.Parcel, delayMinutes: 120);

foreach (var mail in PlayerMail.Inbox())           // MailKind.Letter by default
    Log.Info($"{mail.Title}: {mail.Text} ({string.Join(", ", mail.Contents)})");

PlayerMail.Received.Subscribe(this, mail => Log.Info("New mail: " + mail.Title));
```

## Sending

`Send(title, text, attachments, kind, delayMinutes, oc, rm)`:

- **title** is what the game shows when the mail is taken (a notification "Mail: title, Contents: ...").
- **text** is the letter. It arrives as a readable paper item, the same one the game uses for its parcel notices, headed with the title and the in-game date. Without text, no paper is added.
- **attachments** are items with an amount of 1 to 1000. Mod items work. Currencies are not items here: use `oc` and `rm`.
- **kind**: `Letter` goes to the mailbox at home (everything in it is taken out together when the player opens it), `Parcel` to the parcel locker. The game adds its own "Parcel Notification" letter when a parcel arrives.
- **delayMinutes**: game minutes until delivery (0 to 525600). 0 delivers at once, so the mail is in the mailbox when `Send` returns. A positive value queues the game's own timer, which is saved with the game and respects the game's clock.

`Send` returns false when no world is loaded and throws `ArgumentException` for an empty title, nothing to send, an unknown item or an out-of-range amount.

Everything is one `BundleMail`, the game's own mail type for several things at once, so the mailbox, locker and save handle it with no mod code involved. Mail already sent stays in the save if your mod is later removed, but mod items inside it turn into Open Sewer Coin like other mod items (see [items.md](items.md)).

## Reading

`Inbox(kind)` is the mail waiting to be taken. `InTransit()` is what has been sent with a delay and not arrived, by anyone, with `SecondsLeft` in game seconds. Both return `MailInfo`: `Title`, `Kind`, `Text`, `Attachments` (items), `Contents` (display names of everything, including notes and furniture), `Oc`, `Rm`, and for a written note or e-mail `IsNote` (its title is the one entry of `Contents`, its words `Text`) and `Sender` (who wrote an e-mail).

## Receiving

`PlayerMail.Received` fires when mail reaches the mailbox or locker, from any source: quest scripts, achievements, delayed mail whose timer ran out, or your own `Send`. It does not fire again for mail that is already waiting when a save loads (the game's own `MailReceived` event does, which is why it isn't used). Handlers run in their own try/catch.

## Junk mail

Every world has a junk mail sender (the game's `RandommailSender`, on the mail controller): each day but Sunday, in the morning, it mails one or two random letters, recipes, notes or parcels, and with a 15% chance a forged invoice (one of 26 senders, 22 to 500 coins, never a crime to ignore). `MailSchedules.Senders()` lists the senders with their mail names and chances; `MailSchedules.SendNow()` makes one send a day's worth at once and `SendRandom(key, min, max)` a chosen number (the upper end is exclusive, as in the game).

## Bridge commands

- `mail.send` (`title`, `text?`, `item?`, `amount?`, `kind?`, `delay?`)
- `mail.list` (`kind?`: `letter`, `parcel` or `transit`)
- `mail.junk` (`send?`, `sender?`): the junk mail senders; `send=true` sends a day's worth now
