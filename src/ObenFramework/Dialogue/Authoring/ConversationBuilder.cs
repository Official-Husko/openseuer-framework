// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Turns a <see cref="ConversationDefinition"/> into a Dialogue System conversation: entry 0 is the START
/// node, the definition's lines are entries 1 to n in order, and START leads to the first line.
/// </summary>
internal static class ConversationBuilder
{
    public static Conversation Build(string title, int id, int playerId, int speakerId, ConversationDefinition definition)
    {
        var template = Template.FromDefault();
        var conversation = template.CreateConversation(id, title);
        conversation.ActorID = playerId;
        conversation.ConversantID = speakerId;

        var start = template.CreateDialogueEntry(0, id, "START");
        start.ActorID = playerId;
        start.ConversantID = speakerId;
        start.outgoingLinks.Add(new Link(id, 0, id, 1));
        conversation.dialogueEntries.Add(start);

        var ids = new Dictionary<string, int>(definition.Lines.Count);
        for (int i = 0; i < definition.Lines.Count; i++)
            ids[definition.Lines[i].Key] = i + 1;

        foreach (var line in definition.Lines)
        {
            int entryId = ids[line.Key];
            var entry = template.CreateDialogueEntry(entryId, id, line.Key);
            entry.ActorID = line.Player ? playerId : speakerId;
            entry.ConversantID = line.Player ? speakerId : playerId;
            entry.DialogueText = line.Text ?? "";
            entry.conditionsString = line.Condition ?? "";
            entry.userScript = line.Script ?? "";
            if (line.Next != null)
            {
                foreach (string next in line.Next)
                    entry.outgoingLinks.Add(new Link(id, entryId, id, ids[next]));
            }
            conversation.dialogueEntries.Add(entry);
        }
        return conversation;
    }
}
