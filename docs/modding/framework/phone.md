# Phone numbers

`PhoneNumbers` (in `ObenFramework.Npcs`) adds numbers to the game's phone book, and `Phones` works with the phones in the scene (ringing them, answering, call events). The phones in the world (wall and desk phones, `Phone` interactables) dial them like any other number. How the game's phones work is in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) section 5.3.

Add `"ObenFramework"` to your mod's `dependencies` and `using ObenFramework.Npcs;`.

## Registering a number

A call can do one of two things.

```csharp
// 1. Run code. The phone picks up, your callback runs, the phone hangs up.
PhoneNumbers.Register(this, "5550142", "Dispatch", call =>
{
    Log.Info($"Dialed from {call.Phone.name}");
});

// 2. Start a conversation: the title of one the game (or a database a mod added) has.
PhoneNumbers.Register(this, "5550199", "Pizza", "Phone/Infra/Aziz's Kebab and Burger",
    onCall: call => Log.Info("ringing through"),
    network: PhoneNumber.Network.Obenseuer | PhoneNumber.Network.Outside,
    answerChance: 0.8f);
```

- `number` is 1 to 10 characters of `0-9`, `*` and `#`, the same keys the dialing screen has. A number the book already holds throws `InvalidOperationException`, naming who has it. Invalid text throws `ArgumentException`.
- `network` is the game's `PhoneNumber.Network` flags (`Obenseuer`, `Outside`, `Wasteland`). A phone reaches the number when its own network includes this one; the default is `Obenseuer`, which the in-world phones have.
- `answerChance` is the chance (0 to 1) someone picks up; otherwise the game plays its "not answering" line. The default 1 always answers.
- The conversation title must exist when the call is made. If it does not, a warning is logged and the phone hangs up.
- The callback runs inside the game's call to `Phone.StartCall`, on the main thread, in its own try/catch. For a conversation line it runs just before the conversation starts. It gets a `PhoneCall` with the `Line` and the game's `Phone`.

The result is a `PhoneLine` (`Owner`, `Number`, `Name`, `Conversation`, `Network`, `AnswerChance`, `IsRegistered`). `Dispose()` removes the number; the owning mod's unload does the same, so a hot reload leaves nothing behind. Numbers are not saved: register them in `OnLoad` each time. `Register` throws when the number isn't 1 to 10 of 0-9, * and #, or the phone book already holds it; `PhoneNumbers.CanRegister(number, out reason)` asks first, without throwing, and gives the same reason (with whose number it is, the game's or a mod's). A callback line gives you no spoken text; to have a character talk, point a line at a conversation (see [dialogue.md](dialogue.md) for starting and watching conversations).

## Reading the book

```csharp
foreach (var n in PhoneNumbers.List())
    Log.Info($"{n.Number} {n.Name} [{n.Network}] {n.Conversation} (owner: {n.Owner ?? "game"})");

var entry = PhoneNumbers.Find("112");      // the emergency number, or null
var mine = PhoneNumbers.Registered();      // lines mods have added
```

`List()` returns every entry in the game's `PhoneNumberDatabase.database`: the game's numbers, three special entries with no number (not reachable, not answering, outside the network), numbers only scripts call (`Dialable` false) and the mods'. `Owner` is the registering mod's id, or null for the game's own.

## Phones in the scene

`Phones` works with the phone objects in the scene the player is in (wall phones, desk phones, payphones). Each is named by its `ObjectAddress` string (see [world.md](world.md)).

```csharp
foreach (var phone in Phones.All())
    Log.Info($"{phone.Name} at {phone.Position}: {phone.Network}, in a call {phone.InCall}, ringing {phone.Ringing} ({phone.Caller})");

// Ring a phone with a call from a number in the book; the player has 60 game seconds to pick up.
Phones.Ring(phone.Address, "5550199", 60, answered => Log.Info(answered ? "picked up" : "no answer"));

Phones.Answer(phone.Address);            // pick up for the player
Phones.Decline(phone.Address);           // let it ring out at once
Phones.Call(phone.Address, "112");       // start a call there, as if dialled

Phones.CallStarted.Subscribe(this, c => Log.Info($"call to {c.Number} on {c.Phone}"));
Phones.CallEnded.Subscribe(this, c => Log.Info($"hung up {c.Number}"));
Phones.Ringing.Subscribe(this, c => Log.Info($"{c.Number} is calling {c.Phone}"));
```

- `Ring` goes through the game's own `Phone.ReceiveCall`: the phone rings, and picking it up starts the calling number's conversation (or a mod line's callback). It is refused while the phone is in use or already ringing, while a menu is open, and for numbers the book does not have. The callback gets true when the player picks up and false when the call rings out or is declined.
- `Answer` and `Call` are refused while a menu or a conversation is open. A phone owned by an NPC (`OwnerNpc` above 0) cannot be used by the player but can still ring.
- The events cover every call: the dialing screen, a ringing phone picked up, story calls and your own. `Ringing` fires for story calls too.

## Things to know

- The game fills its book from `Resources` in `PhoneNumberDatabase.Awake`, but only while the list is empty. Adding a number first would stop that, so `Register` loads the game's numbers itself the same way when it finds the list empty.
- The hooks are `Phone.StartCall` (mod lines and the number of the call), `Phone.OnCallStarted`, `Phone.OnCallEnded` and `Phone.ReceiveCall`.
- The number assets are created with `ScriptableObject.CreateInstance` and hidden from the engine's unused-asset sweep, then destroyed on removal.

## Dev bridge

```sh
oben call phone.list check=5550123                     # could this number be registered, and why not
oben call phone.list
oben call phone.list mods=true
oben call phone.scene
oben call phone.scene ring="<address from phone.scene>" number=112 seconds=30
```

`phone.list` shows each number in the book with its name, network, answer chance, conversation, whether it is dialable and its owner. `phone.scene` lists the phones in the scene with their address, network, whether a call is on or it rings and who calls, and with `ring` rings one. See [../dev-tools.md](../dev-tools.md).
