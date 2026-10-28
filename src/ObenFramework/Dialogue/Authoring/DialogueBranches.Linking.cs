// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using PixelCrushers.DialogueSystem;
using ObenFramework.Story;

namespace ObenFramework.Dialogue;

public static partial class DialogueBranches
{
    /// <summary>Links branches that waited for their conversation or line, and re-links all after a database reload.</summary>
    internal static void ApplyAll()
    {
        DialogueAuthoring.Restore();
        RetryWaiting(all: true);
    }

    /// <summary>Tries the branches that are not linked yet (or all of them with <paramref name="all"/>).</summary>
    internal static void RetryWaiting(bool all = false)
    {
        var master = DialogueAnchors.Master;
        if (master == null || _retrying)
            return;
        _retrying = true;
        try
        {
            Retry(master, all);
        }
        finally
        {
            _retrying = false;
        }
    }

    private static bool _retrying;

    private static void Retry(DialogueDatabase master, bool all)
    {
        foreach (var record in Branches.ToArray())
        {
            if (record.Failed || (record.Linked && !all))
                continue;
            try
            {
                var conversation = master.GetConversation(record.Definition.Conversation);
                if (conversation == null)
                {
                    record.Linked = false;
                    continue;
                }
                var anchor = record.AnchorId >= 0 && record.BranchTitle != null
                    ? conversation.GetDialogueEntry(record.AnchorId)
                    : DialogueAnchors.Find(master, conversation, record.Definition.AnchorText, record.Definition.AnchorId, out string why, out bool absent);
                if (anchor == null)
                {
                    record.Linked = false;
                    continue;
                }
                record.AnchorId = anchor.id;
                string failed = Link(master, conversation, anchor, record);
                if (failed != null)
                {
                    record.Failed = true;
                    StoryLog.Refused("Branch " + record.Key, failed);
                }
            }
            catch (Exception e)
            {
                StoryLog.Failed("Linking branch " + record.Key, e);
            }
        }
    }

    /// <summary>Makes the branch conversation if needed and links the anchor to its first lines. Null when it worked.</summary>
    private static string Link(DialogueDatabase master, Conversation conversation, DialogueEntry anchor, Registered record)
    {
        var def = record.Definition;
        if (record.BranchTitle == null)
        {
            if (def.Lines.Speaker != null)
            {
                var speaker = master.GetActor(def.Lines.Speaker);
                if (speaker == null || speaker.id != anchor.ActorID)
                    return "the branch's speaker must be the NPC of the anchor line (leave Speaker null)";
            }
            var added = DialogueAuthoring.AddWith(record.Owner, def.Lines, anchor.ActorID);
            if (!added.Added)
                return "the branch conversation was refused: " + added.Reason;
            record.BranchTitle = added.Key;
        }

        var branch = master.GetConversation(record.BranchTitle);
        var start = branch?.GetDialogueEntry(0);
        if (start == null)
            return "the branch conversation is missing";
        record.BranchId = branch.id;

        bool npcFirst = def.Lines.Lines.Count > 0 && !def.Lines.Lines[0].Player;
        int priority = DialoguePlan.LinkPriority(def.Priority, DialogueAnchors.PlayerPriority(master, anchor), npcFirst);
        string pickScript = def.OnPicked != null ? "Oben_BranchPicked(\"" + record.Key + "\")" : null;
        int at = DialoguePlan.InsertIndex(def.InsertAt, anchor.outgoingLinks.Count);
        foreach (var link in start.outgoingLinks)
        {
            int target = link.destinationDialogueID;
            if (pickScript != null)
                AddScript(branch.GetDialogueEntry(target), pickScript);
            if (anchor.outgoingLinks.Exists(l => l.destinationConversationID == branch.id && l.destinationDialogueID == target))
                continue;
            at = DialoguePlan.InsertIndex(at, anchor.outgoingLinks.Count);
            anchor.outgoingLinks.Insert(at++, new Link(conversation.id, anchor.id, branch.id, target) { priority = (ConditionPriority)priority });
        }
        record.Linked = true;
        return null;
    }

    private static void Unlink(DialogueDatabase master, Registered record)
    {
        var conversation = master.GetConversation(record.Definition.Conversation);
        var anchor = conversation?.GetDialogueEntry(record.AnchorId);
        if (anchor != null && record.BranchId >= 0)
            anchor.outgoingLinks.RemoveAll(l => l.destinationConversationID == record.BranchId);

        if (record.BranchTitle != null)
        {
            var branch = master.GetConversation(record.BranchTitle);
            if (branch != null)
            {
                foreach (var entry in branch.dialogueEntries)
                    DialogueAnchors.ForgetSimStatus(branch.id, entry.id);
            }
            DialogueAuthoring.RemoveTitle(record.Owner, record.BranchTitle);
        }
        record.Linked = false;
    }

    private static void AddScript(DialogueEntry entry, string script)
    {
        if (entry == null)
            return;
        string current = entry.userScript ?? "";
        if (current.IndexOf(script, StringComparison.Ordinal) >= 0)
            return;
        entry.userScript = current.Length == 0 ? script : current + "\n" + script;
    }
}
