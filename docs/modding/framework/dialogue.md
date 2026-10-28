# Dialogue, Lua and conversations

`ObenFramework.Dialogue` wraps the game's PixelCrushers Dialogue System. The game keeps its story flags in the Dialogue System's Lua state (global variables, plus fields on actors), checks them in conversation conditions, and saves the whole thing with every save. Background and the table of the game's own Lua functions are in [../../game/npcs-dialogue-quests.md](../../game/npcs-dialogue-quests.md) section 3.

Everything here runs on the main thread.

## Variables and actor fields

```csharp
using ObenFramework.Dialogue;

int day = LuaVariables.GetInt("Day");
bool met = LuaVariables.GetBool("MyMod_MetPentti");
string name = LuaVariables.GetString("PlayerFirstName", "Nobody");

LuaVariables.Set("MyMod_MetPentti", true);
LuaVariables.SetDefault("MyMod_Reputation", 0);          // only creates it

bool knows = ActorFields.GetBool("Pentti Penttilä", "KnowsTheName");
ActorFields.Set("Pentti Penttilä", "KnowsTheName", true);
```

| Class | Members |
|---|---|
| `LuaVariables` | `Exists`, `Get`, `GetString/GetInt/GetFloat/GetBool(name, fallback)`, `Names()`, `Set`, `SetDefault` |
| `ActorFields` | `ActorExists`, `Exists`, `Get`, `GetString/GetInt/GetFloat/GetBool(actor, field, fallback)`, `Set`, `SetDefault` |
| `DialogueValue` | what `Get` returns: `Exists`, `Type`, `IsNumber/IsString/IsBool/IsTable`, `AsString/AsNumber/AsInt/AsBool`, `StringOr/NumberOr/IntOr/BoolOr(fallback)`, `ToObject()` |

- Reads never throw. A missing variable, a missing actor and a Lua `nil` all give `Exists == false`, and the typed getters give your fallback when the value is missing or has another type.
- Names go through the Dialogue System's own mapping (spaces become underscores), so `"Card Games"` and `"Card_Games"` are the same actor.
- Values are `string`, `bool`, or a number. Numbers are saved by the game as floats, so whole numbers above about 16 million lose precision. `Set` throws `ArgumentException` for anything it cannot store: NaN or infinity, other types, and strings or names with a backslash or a control character (the game writes its save as Lua source and escapes almost nothing, so one backslash can corrupt the dialogue part of a save). Double quotes in strings are turned into single quotes by the game when it saves.
- The game owns some variables (`Day`, `PlayerFirstName`, `Price`, `WakeUpReason`...) and rewrites them itself; the list is in the game notes. Prefix your own with the mod name. `Player.Money` and `Player.RealMoney` are kept in sync by the money code, so change money through `Wallet`.

### Timing around save loads

Loading a save resets the `Variable` and `Actor` tables, refills them from the save, and applies the saved Lua text as the last step before `SaveController.LoadingDone`. A variable set at any point earlier in a load is silently reverted, and variables a mod created are only in saves written while the mod was installed.

So `Set` and `SetDefault` return a `LuaWriteResult`:

- `Applied`: the value is in the Lua state.
- `Deferred`: a load is in progress (from the moment the game announces it) or the Lua tables don't exist yet. The write is queued, and applied right after the load, before `GameEvents.Loaded` handlers run. Later writes to the same target replace queued ones; `SetDefault` is kept in order. Queued writes are not tied to the mod that made them.

`LuaState` exposes the same facts: `IsReady`, `IsLoading`, `CanWrite`. Reads during a load return the old state.

The pattern for a mod's own variables is to create the defaults when the world is ready, which also covers loading an older save:

```csharp
GameEvents.WhenWorldReady(this, _ =>
{
    LuaVariables.SetDefault("MyMod_Reputation", 0);
});
```

## Running Lua

```csharp
var result = LuaScript.Run("return Variable['Day'] + 1");
if (result.Succeeded) Log.Info(result.Value.AsInt.ToString());
else Log.Warn(result.Error);

bool ok = LuaScript.IsTrue("TaskActive('SomeTask') and Variable['Day'] > 3");
```

`Run` returns errors in the result instead of logging them, and turns the Dialogue System's own error log off for the call. `IsTrue` takes a condition the way conversation conditions are written (empty is true, an error is false). Neither is held back during loads. The Lua environment is the one conversations use, so the game's functions (`TaskActive`, `CheckMoneyDialogue`...) are available, as long as the scene object that registers each one is alive.

## Lua functions

```csharp
public override void OnLoad()
{
    LuaFunctions.Register(this, "MyMod_Greeting", (Func<string, string>)(who => "Hello " + who));
    LuaFunctions.Register(this, "MyMod_Reward", (Action<float>)(amount => Wallet.Add(Money.Currency.OC, amount)));
}
```

Usable in a conversation as `MyMod_Greeting("Pentti")` in text or `MyMod_Reward(5)` in a script.

- The function is removed when the mod unloads, only if the Lua table still holds that exact function.
- Parameters are `float`, `double`, `string` or `bool` (the Dialogue System hands numbers over as floats; an `int` parameter would fail to bind and is refused). Return nothing, one of those types, or an `int`.
- The Dialogue System ignores a registration under a name that already exists, without telling anyone. `Register` checks first and throws `InvalidOperationException` instead, naming the mod that has it. `LuaFunctions.Exists(name)` and `ModFunctions` let you look.
- An exception in the function is logged by the Dialogue System and fails the Lua call that used it.

## Conversations

```csharp
var result = Conversations.Start("Pentti Penttilä");                       // default conversation
var result = Conversations.Start("Pentti Penttilä", "Some Conversation");  // a specific one
var result = Conversations.StartRaw("My Narration");                       // no NPC
```

`Start` goes through the NPC's `InteractableTalk.CheckStartDialogueInterrupt`, the same path as pressing Use. The NPC stops and looks at the player, its relays and conditions are registered, and everything is undone when the talk ends. `StartRaw` calls `DialogueManager.StartConversation` with the player as speaker, which locks and unlocks the player's controls by itself but leaves the NPC alone.

The result is a `ConversationStart`: `Started`, `NotInWorld`, `Busy`, `UnknownNpc`, `UnknownConversation`, `CannotTalk` (the NPC refuses: player in the air, a contest, waiting, an NPC using a door), or `NoValidEntry` (the conversation exists but every entry's condition failed, so nothing opened). `force: true` talks regardless and stops a running conversation first. `FindNpc(name)` finds the loaded `InteractableTalk` by the name shown on the Talk prompt or the character's full name; `Exists(title)`, `IsActive`, `CurrentTitle` and `Stop()` are what they sound like.

Why `StartRaw` clears a callback: the game stores the end-of-talk handler in the single field `DialogueController.onDialogueClosed`, never clears it, and calls it without a null check. After an NPC talk it still points at that NPC, so any later conversation would run that NPC's end-of-talk code again. `StartRaw` replaces it with an empty one (and clears `currentInteractableTalk`) before starting.

### Events

```csharp
Conversations.Started.Subscribe(this, c => Log.Info($"Talking: {c.Title}"));
Conversations.Ended.Subscribe(this, c => ...);
```

The payload is a `ConversationInfo` with `Title`, `Actor` and `Conversant` (the NPC's transform, null for conversations without one). They fire once per conversation of any kind: NPC talks, phone calls, narration. They come from the Dialogue System's own `conversationStarted` and `conversationEnded` events, not from `onDialogueClosed`, so they are not affected by the stale callback, and each handler runs in its own try/catch. The framework re-attaches if the scene replaces the dialogue controller.

`Ended` is raised inside the Dialogue System's end-of-conversation code. The game gives the player back control a frame later (`GameController` notices the conversation is over), so do not assume the player can move yet.

## Dev bridge

```sh
oben call lua.get name=Day
oben call lua.get actor="Player" name=Money
oben call lua.set name=MyMod_Flag value=true
oben call lua.set name=MyMod_Reputation value=0 default=true
oben call lua.run code="return Variable['Day'] + 1"
```

`lua.get` returns `exists`, `type` and `value`. `lua.set` accepts `actor`, and `default=true` for set-if-missing; it returns the value now and `result` (`applied` or `deferred`). `lua.run` returns `ok`, `error` and the returned value. See [../dev-tools.md](../dev-tools.md).

## Adding dialogue

Mod conversations, extra choices and branches on the game's NPC lines, narration, intercepting a conversation before it starts, and the framework's own Lua functions (`Oben_QuestState` and others) are in [dialogue-authoring.md](dialogue-authoring.md).
