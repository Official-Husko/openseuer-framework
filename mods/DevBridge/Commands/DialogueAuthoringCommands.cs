using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Dialogue;
using PixelCrushers.DialogueSystem;

namespace DevBridge.Commands;

/// <summary>Looking into the dialogue database and adding responses, branches and conversations (owned by DevBridge).</summary>
internal static class DialogueAuthoringCommands
{
    [BridgeCommand("dialogue.conversations", "List conversation titles of the dialogue database, optionally filtered.",
        Params = new[] { "filter: optional text the title contains (case-insensitive)", "limit: default 100" })]
    private static object ListConversations(JObject p)
    {
        var master = Master();
        string filter = p.Str("filter", null);
        int limit = p.Int("limit", 100);
        var list = new JArray();
        int total = 0;
        foreach (var conversation in master.conversations)
        {
            if (filter != null && conversation.Title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            total++;
            if (list.Count < limit)
                list.Add(new JObject { ["id"] = conversation.id, ["title"] = conversation.Title, ["entries"] = conversation.dialogueEntries.Count });
        }
        return new JObject { ["total"] = total, ["conversations"] = list };
    }

    [BridgeCommand("dialogue.lines", "List the entries of one conversation: id, speaker, NPC or player, text and links. Use it to pick an anchor.",
        Params = new[] { "conversation: title" })]
    private static object Lines(JObject p)
    {
        var master = Master();
        var conversation = master.GetConversation(p.Str("conversation")) ?? throw new ArgumentException("No conversation with that title.");
        var lines = new JArray();
        foreach (var entry in conversation.dialogueEntries)
        {
            var links = new JArray();
            foreach (var link in entry.outgoingLinks)
                links.Add(link.destinationConversationID == conversation.id ? (JToken)link.destinationDialogueID : $"{link.destinationConversationID}:{link.destinationDialogueID}");
            var actor = master.GetActor(entry.ActorID);
            lines.Add(new JObject
            {
                ["id"] = entry.id,
                ["speaker"] = actor != null ? actor.Name : null,
                ["type"] = master.GetCharacterType(entry.ActorID).ToString(),
                ["text"] = entry.DialogueText,
                ["condition"] = string.IsNullOrEmpty(entry.conditionsString) ? null : entry.conditionsString,
                ["links"] = links,
            });
        }
        return new JObject { ["id"] = conversation.id, ["title"] = conversation.Title, ["lines"] = lines };
    }

    [BridgeCommand("dialogue.respond", "Add a DevBridge player choice (with an optional NPC reply) to a game NPC line.",
        Params = new[] { "conversation: title", "anchorId: NPC entry id, or anchorText", "playerText: the choice", "npcReply: optional",
            "condition: optional Lua condition", "insertAt: optional, -1 last", "priority: optional 0-4" })]
    private static object Respond(JObject p)
    {
        var response = p.ToObject<ResponseDefinition>();
        string text = response.PlayerText;
        response.OnPicked = () => DevBridgeMod.Instance.Log.Info($"Dialogue response picked: {text}");
        return Describe(DialogueResponses.Add(DevBridgeMod.Instance, response));
    }

    [BridgeCommand("dialogue.branch", "Join a DevBridge topic to a conversation: a player line on its first NPC line, answered by NPC lines.",
        Params = new[] { "conversation: title", "playerLine: the choice", "npcLines: array of replies", "showWhen: optional Lua condition" })]
    private static object Branch(JObject p)
    {
        var replies = p.Array("npcLines").ToObject<string[]>();
        string line = p.Str("playerLine");
        return Describe(DialogueBranches.AddTopic(DevBridgeMod.Instance, p.Str("conversation"), line, replies, p.Str("showWhen", null),
            () => DevBridgeMod.Instance.Log.Info($"Dialogue topic picked: {line}")));
    }

    [BridgeCommand("dialogue.add", "Add a DevBridge conversation (title DevBridge.<key>) from a definition.",
        Params = new[] { "key: conversation key", "speaker: optional actor name", "lines: array of {key, player, text, condition, script, next}" })]
    private static object Add(JObject p) => Describe(DialogueAuthoring.Add(DevBridgeMod.Instance, p.ToObject<ConversationDefinition>()));

    [BridgeCommand("dialogue.remove", "Remove a DevBridge response or branch by key, or a DevBridge conversation by its key.",
        Params = new[] { "key: response or branch key, or conversation key" })]
    private static object Remove(JObject p)
    {
        string key = p.Str("key");
        var owner = DevBridgeMod.Instance;
        bool removed = DialogueResponses.Remove(owner, key) || DialogueBranches.Remove(owner, key) || DialogueAuthoring.Remove(owner, key);
        return new JObject { ["removed"] = removed };
    }

    [BridgeCommand("dialogue.responses", "List what mods added to the dialogue: conversations, responses and branches.")]
    private static object Added(JObject p)
    {
        var conversations = new JArray();
        foreach (var pair in DialogueAuthoring.All())
            conversations.Add(new JObject { ["title"] = pair.Key, ["owner"] = pair.Value });
        var responses = new JArray();
        foreach (var r in DialogueResponses.All())
            responses.Add(new JObject { ["key"] = r.Key, ["owner"] = r.Owner, ["conversation"] = r.Conversation, ["anchorId"] = r.AnchorId, ["playerText"] = r.PlayerText, ["linked"] = r.Linked });
        var branches = new JArray();
        foreach (var b in DialogueBranches.All())
            branches.Add(new JObject { ["key"] = b.Key, ["owner"] = b.Owner, ["conversation"] = b.Conversation, ["anchorId"] = b.AnchorId, ["branch"] = b.BranchTitle, ["linked"] = b.Linked });
        return new JObject { ["conversations"] = conversations, ["responses"] = responses, ["branches"] = branches };
    }

    [BridgeCommand("dialogue.narrate", "Show text over a black screen like the game's own narration.", Params = new[] { "text: up to 1000 characters" })]
    private static object Narrate(JObject p) => new JObject { ["shown"] = Narration.Show(DevBridgeMod.Instance, p.Str("text")) };

    private static JObject Describe(DialogueEditResult result) => new JObject
    {
        ["added"] = result.Added,
        ["key"] = result.Key,
        ["waiting"] = result.Waiting,
        ["refusal"] = result.Refusal.ToString(),
        ["reason"] = result.Reason,
    };

    private static DialogueDatabase Master()
    {
        if (!DialogueManager.hasInstance || DialogueManager.masterDatabase == null)
            throw new InvalidOperationException("The dialogue database is not loaded.");
        return DialogueManager.masterDatabase;
    }
}
