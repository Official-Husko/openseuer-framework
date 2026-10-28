// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Finding the line a response or branch attaches to (the "anchor"): an NPC line in a game conversation
/// that leads to the player's choices, picked by its entry id or its text.
/// </summary>
internal static class DialogueAnchors
{
    /// <summary>The master dialogue database, or null before the Dialogue System is up.</summary>
    public static DialogueDatabase Master => DialogueManager.hasInstance ? DialogueManager.masterDatabase : null;

    /// <summary>
    /// The anchor line, or null with the reason. <paramref name="anchorText"/> wins over
    /// <paramref name="anchorId"/>; text has to match exactly one line (see <see cref="DialoguePlan.MatchAnchor"/>).
    /// <paramref name="absent"/> is true when the line simply isn't there (as opposed to being the wrong kind).
    /// </summary>
    public static DialogueEntry Find(DialogueDatabase master, Conversation conversation, string anchorText, int anchorId, out string reason, out bool absent)
    {
        absent = false;
        DialogueEntry anchor;
        if (anchorText != null)
        {
            var texts = new List<string>(conversation.dialogueEntries.Count);
            foreach (var entry in conversation.dialogueEntries)
                texts.Add(entry.DialogueText);
            var matches = DialoguePlan.MatchAnchor(texts, anchorText);
            if (matches.Count != 1)
            {
                absent = matches.Count == 0;
                reason = matches.Count == 0 ? "no line of the conversation has that text" : $"{matches.Count} lines have that text; use the entry id";
                return null;
            }
            anchor = conversation.dialogueEntries[matches[0]];
        }
        else
        {
            anchor = conversation.GetDialogueEntry(anchorId);
            if (anchor == null)
            {
                absent = true;
                reason = $"the conversation has no entry {anchorId}";
                return null;
            }
        }

        if (master.GetCharacterType(anchor.ActorID) != CharacterType.NPC)
            reason = "the line is the player's, not an NPC line";
        else if (LeadsOnlyToNpc(master, anchor))
            reason = "the line only leads to more NPC lines, so the player never picks from it";
        else if (PlayerActorId(master) < 0)
            reason = "the dialogue database has no player actor";
        else
            reason = null;
        return reason == null ? anchor : null;
    }

    public static int PlayerActorId(DialogueDatabase master)
    {
        var player = master.actors.Find(a => a.IsPlayer);
        return player == null ? -1 : player.id;
    }

    /// <summary>The priority the anchor's player choices use, so an added one sorts with them.</summary>
    public static int PlayerPriority(DialogueDatabase master, DialogueEntry anchor)
    {
        foreach (var link in anchor.outgoingLinks)
        {
            var target = master.GetDialogueEntry(link);
            if (target != null && master.GetCharacterType(target.ActorID) == CharacterType.PC)
                return (int)link.priority;
        }
        return DialoguePlan.Normal;
    }

    /// <summary>The first NPC line of a conversation that can take a topic, or null.</summary>
    public static DialogueEntry FirstNpcLine(DialogueDatabase master, Conversation conversation)
    {
        foreach (var entry in conversation.dialogueEntries)
        {
            if (entry.isGroup || string.IsNullOrEmpty(entry.DialogueText) || entry.DialogueText.Trim().Length == 0)
                continue;
            if (Find(master, conversation, null, entry.id, out _, out _) != null)
                return entry;
        }
        return null;
    }

    /// <summary>Drops what the Dialogue System remembers about an entry that was taken out (its "was shown" mark).</summary>
    public static void ForgetSimStatus(int conversationId, int entryId) =>
        Lua.Run(string.Format("if Conversation[{0}] ~= nil and Conversation[{0}].Dialog ~= nil then Conversation[{0}].Dialog[{1}] = nil end", conversationId, entryId));

    private static bool LeadsOnlyToNpc(DialogueDatabase master, DialogueEntry anchor)
    {
        bool npc = false;
        foreach (var link in anchor.outgoingLinks)
        {
            var target = master.GetDialogueEntry(link);
            if (target == null)
                continue;
            var type = master.GetCharacterType(target.ActorID);
            if (type == CharacterType.PC)
                return false;
            if (type == CharacterType.NPC)
                npc = true;
        }
        return npc;
    }
}
