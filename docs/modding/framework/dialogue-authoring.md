# Adding dialogue

Namespace `ObenFramework.Dialogue`. Reading and writing the Lua state and starting conversations are in [dialogue.md](dialogue.md). This page is about adding to the game's dialogue: conversations of your own, extra choices on the game's NPC lines, whole branches, narration, intercepting a conversation before it starts, and the framework's Lua functions.

Nothing here is written into the game's save (on 0.5.13 the game saves neither conversation fields nor "was shown" marks, so not even the ids of added entries end up there). Added conversations, responses and branches live in memory, are put back after the Dialogue System reloads its database, and are taken out when the mod unloads. What a conversation's scripts set (variables, quest changes) is saved as usual. Removing a mod therefore leaves nothing behind except the variables it set.

## Finding a line to attach to

Responses and branches attach to an *anchor*: an NPC line of a game conversation that leads to the player's choices. List a conversation's lines with the bridge:

```sh
oben call dialogue.conversations filter=Bazaar
oben call dialogue.lines conversation="Bazaar/Trader/Jens Brom"
```

In `Bazaar/Trader/Jens Brom` the START entry picks one of four greetings (entries 1, 4, 5 and 6, such as 1: "Heh heh.\,\, I got stuff!"), and each leads to the same two player choices ("Show me (Trade)", "Not interested (Leave)"). Any of them is an anchor, but a choice added to entry 1 only shows when Jens opens with that greeting. An anchor is picked by `AnchorId`, or by `AnchorText`, matched exactly, then ignoring case and spaces at the ends, then also ignoring the Dialogue System's typing codes (`\,` pauses and the like), and it has to match exactly one line. A line that is the player's, or that only leads to more NPC lines, is refused.

## Responses

```csharp
var result = DialogueResponses.Add(this, new ResponseDefinition
{
    Conversation = "Bazaar/Trader/Jens Brom",
    AnchorText = "Heh heh. I got stuff!",
    PlayerText = "Seen any rubber ducks?",
    NpcReply = "Ducks? What do I look like.",
    Condition = "Variable['MyMod_AskedJens'] ~= true",
    OnPicked = () => LuaVariables.Set("MyMod_AskedJens", true),
});
```

A response is one player choice with an optional one-line NPC reply, after which the conversation ends. `InsertAt` places it among the line's links (-1 last), `Priority` (0 Low to 4 High, -1 for the priority of the line's other choices) decides where it sorts. The key it returns removes it (`DialogueResponses.Remove`); adding the same response again returns the same key and replaces `OnPicked`. `OnPicked` runs from the Dialogue System's script while the conversation is open.

## Branches and topics

```csharp
DialogueBranches.Add(this, new BranchDefinition
{
    Conversation = "Bazaar/Trader/Jens Brom",
    AnchorId = 1,
    Lines = new ConversationDefinition
    {
        Key = "ducks",
        Lines =
        {
            new ConversationLine { Key = "ask", Player = true, Text = "About those ducks...", Next = new[] { "answer" } },
            new ConversationLine { Key = "answer", Text = "Fine. Mr. Duck pays well.", Next = new[] { "thanks", "bye" } },
            new ConversationLine { Key = "thanks", Player = true, Text = "Thanks." },
            new ConversationLine { Key = "bye", Player = true, Text = "Whatever." },
        },
    },
});

DialogueBranches.AddTopic(this, "Bazaar/Trader/Jens Brom", "Any work?", new[] { "Maybe.", "Come back later." },
    showWhen: "Oben_QuestState('MyMod.kebab') == 'Active'");
```

A branch is a conversation of its own (spoken by the anchor's NPC, so leave `Speaker` null) whose first lines are linked from the anchor, so they appear in the player's menu next to the game's choices. A branch that starts with an NPC line gets one priority step below the menu when no priority is asked for, so it doesn't replace the menu. `AddTopic` is the short form: one player line on the conversation's first NPC line that leads to choices, answered by the NPC's lines in order.

When the conversation or the anchor isn't loaded yet the response or branch waits (`Waiting` in the result) and is linked after the next scene load, save load or added conversation.

## Conversations of your own

```csharp
var result = DialogueAuthoring.Add(this, new ConversationDefinition
{
    Key = "greeting",
    Speaker = "Bazaar/Trader/Jens Brom",
    Lines =
    {
        new ConversationLine { Key = "hello", TextCallback = () => $"Day {LuaVariables.GetInt("Day")} already?", Text = "Hello.", Next = new[] { "ok" } },
        new ConversationLine { Key = "ok", Player = true, Text = "Yeah.", Script = "Variable['MyMod_Greeted'] = true" },
    },
});
Conversations.StartRaw(result.Key);   // "MyMod.greeting"
```

The title is "modId.key". Lines: 1 to 200, keys unique, texts up to 1000 characters, `Condition` and `Script` Lua up to 500. `Next` lists the lines that can follow; several player lines after one line form a choice menu. `TextCallback` is asked each time the line shows (the `Text` is the fallback). `Speaker` is a dialogue database actor name (`dialogue.lines` shows them), or null for a nameless voice. `DialogueEditResult` has `Key`, `Refusal` (`NotReady`, `Busy`, `BadInput`, `AlreadyExists`, `UnknownSpeaker`, `NoPlayerActor`, `NoAnchor`, `Failed`) and a `Reason`. Mod conversations get ids from 1,000,000 up; the game's go to 487 on 0.5.13.

## Narration

```csharp
Narration.Show(this, "You wake up with a headache and a duck in your pocket.");
```

Fades to black, plays the text as a conversation without a speaker, fades back when the player closes it. Refused outside a ready world, during a conversation, a save, or the game's own sleep and arrest sequences.

## Intercepting a conversation

```csharp
Conversations.Intercept(this, start =>
{
    if (start.Title == "Bazaar/Trader/Jens Brom" && LuaVariables.GetBool("MyMod_JensAngry"))
        start.Title = DialogueAuthoring.TitleOf(this, "angry");   // play ours instead
});
```

Every conversation start goes through the interceptors first (NPC talks, phone calls, narration, scripted scenes). A handler may set `Title` to another conversation of the database, or `Cancel` it; once cancelled it stays cancelled. A redirect to an unknown title is ignored with a log line. For an NPC, a cancelled talk gives the player control back about a second later, the game's own safety check.

## Lua functions from the framework

Usable in any condition, script or text (`[lua(...)]`):

| Function | Returns |
|---|---|
| `Oben_QuestState(taskId)` | "Active", "Finished", "Failed", "Abandoned", or "" when not started |
| `Oben_ObjectiveState(taskId, objectiveId)` | the same for a step, plus "Hidden" |
| `Oben_CrimeTotal()` | `Law.Total` |
| `Oben_CrimeAgainst(npcId)` | the record against one NPC |

`Oben_ResponsePicked`, `Oben_BranchPicked` and `Oben_LineText` are what added responses, branches and text callbacks use. All are registered through `LuaFunctions`, so they go away with the framework.

## Dev bridge

```sh
oben call dialogue.respond conversation="Bazaar/Trader/Jens Brom" anchorId=1 playerText="Seen any ducks?" npcReply="No."
oben call dialogue.branch conversation="Bazaar/Trader/Jens Brom" playerLine="Any work?" 'npcLines=["Maybe.","Come back later."]'
oben call dialogue.add '{"key":"hello","speaker":"Bazaar/Trader/Jens Brom","lines":[{"key":"a","text":"Hello from the bridge."}]}'
oben call dialogue.responses
oben call dialogue.remove key=<key from the answer>
oben call dialogue.narrate text="It is quiet. Too quiet."
```

Picking a DevBridge response or topic writes "Dialogue response picked" or "Dialogue topic picked" to the DevBridge log (`oben logs`).
