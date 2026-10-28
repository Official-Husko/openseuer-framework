# Phone numbers

`PhoneNumbers` (in `ObenFramework.Npcs`) adds numbers to the game's phone book. The phones in the world (wall and desk phones, `Phone` interactables) dial them like any other number. How the game's phones work is in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) section 5.3.

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

The result is a `PhoneLine` (`Owner`, `Number`, `Name`, `Conversation`, `Network`, `AnswerChance`, `IsRegistered`). `Dispose()` removes the number; the owning mod's unload does the same, so a hot reload leaves nothing behind. Numbers are not saved: register them in `OnLoad` each time. A callback line gives you no spoken text; to have a character talk, point a line at a conversation (see [dialogue.md](dialogue.md) for starting and watching conversations).

## Reading the book

```csharp
foreach (var n in PhoneNumbers.List())
    Log.Info($"{n.Number} {n.Name} [{n.Network}] {n.Conversation} (owner: {n.Owner ?? "game"})");

var entry = PhoneNumbers.Find("112");      // the emergency number, or null
var mine = PhoneNumbers.Registered();      // lines mods have added
```

`List()` returns every entry in the game's `PhoneNumberDatabase.database`: the game's numbers, three special entries with no number (not reachable, not answering, outside the network), numbers only scripts call (`Dialable` false) and the mods'. `Owner` is the registering mod's id, or null for the game's own.

## Things to know

- The game fills its book from `Resources` in `PhoneNumberDatabase.Awake`, but only while the list is empty. Adding a number first would stop that, so `Register` loads the game's numbers itself the same way when it finds the list empty.
- Only `Phone.StartCall` is patched. Incoming calls (`Phone.ReceiveCall`, the ringing phones scene logic uses) are not covered by this API.
- The number assets are created with `ScriptableObject.CreateInstance` and hidden from the engine's unused-asset sweep, then destroyed on removal.

## Dev bridge

```sh
oben call phone.list
oben call phone.list mods=true
```

Each entry shows the number, name, network, answer chance, conversation, whether it is dialable and its owner. See [../dev-tools.md](../dev-tools.md).
