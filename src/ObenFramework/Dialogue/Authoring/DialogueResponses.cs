// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.Story;
using ObenLoader;
using ObenLoader.Runtime;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Dialogue;

/// <summary>
/// Extra player choices on the game's own NPC lines. A response is written into the game conversation in
/// memory (a player entry, an optional NPC reply, and a link from the anchor line), is re-applied after the
/// Dialogue System reloads its database, and is taken out again when the mod unloads. The game's save never
/// holds it.
/// </summary>
public static class DialogueResponses
{
    private sealed class Registered
    {
        public ObenMod Owner;
        public string Key;
        public ResponseDefinition Definition;
        public int AnchorId = -1;
        public int PlayerId = -1;
        public int NpcId = -1;
        public int Priority;
        public bool Linked;
    }

    private static readonly List<Registered> Responses = new List<Registered>();
    private static readonly HashSet<ObenMod> TrackedOwners = new HashSet<ObenMod>();

    /// <summary>
    /// Adds the choice. Returns its key (the same for the same response, so adding it again replaces
    /// <see cref="AnchorDefinition.OnPicked"/>). When the conversation or line isn't loaded yet the response
    /// waits and is linked once it is (<see cref="DialogueEditResult.Waiting"/>).
    /// </summary>
    public static DialogueEditResult Add(ObenMod owner, ResponseDefinition response)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (response == null) throw new ArgumentNullException(nameof(response));
        string problem = Check(response);
        if (problem != null)
            return Refuse(ConversationRefusal.BadInput, problem);
        if (SaveGate.IsBusy)
            return Refuse(ConversationRefusal.Busy, "a save or load is running");
        var master = DialogueAnchors.Master;
        if (master == null)
            return Refuse(ConversationRefusal.NotReady, "the dialogue database is not loaded");

        var conversation = master.GetConversation(response.Conversation);
        DialogueEntry anchor = null;
        if (conversation != null)
        {
            anchor = DialogueAnchors.Find(master, conversation, response.AnchorText, response.AnchorId, out string why, out bool absent);
            if (anchor == null && !absent)
                return Refuse(ConversationRefusal.NoAnchor, why);
        }

        string key = DialoguePlan.ResponseKey(owner.Info.Id, response.Conversation, response.AnchorText, response.AnchorId,
            response.PlayerText, response.NpcReply, response.Condition);
        var record = Responses.Find(r => r.Key == key);
        if (record == null)
        {
            record = new Registered { Owner = owner, Key = key };
            Responses.Add(record);
            if (TrackedOwners.Add(owner))
                owner.Track(Disposable.Create(() => RemoveOwner(owner)));
        }
        record.Definition = response;
        if (anchor == null)
            return new DialogueEditResult(key, ConversationRefusal.None, null, waiting: true);

        record.AnchorId = anchor.id;
        if (!Apply(master, record))
            return new DialogueEditResult(key, ConversationRefusal.None, null, waiting: true);
        return new DialogueEditResult(key, ConversationRefusal.None, null);
    }

    /// <summary>Takes one of this mod's responses out. False when the key isn't this mod's or a save runs.</summary>
    public static bool Remove(ObenMod owner, string key)
    {
        var record = Responses.Find(r => r.Owner == owner && r.Key == key);
        if (record == null || SaveGate.IsBusy)
            return false;
        Responses.Remove(record);
        Detach(record);
        return true;
    }

    /// <summary>Every registered response: key, owner id, conversation, anchor id and whether it is linked now.</summary>
    public static List<(string Key, string Owner, string Conversation, int AnchorId, string PlayerText, bool Linked)> All()
    {
        var all = new List<(string, string, string, int, string, bool)>(Responses.Count);
        foreach (var r in Responses)
            all.Add((r.Key, r.Owner.Info.Id, r.Definition.Conversation, r.AnchorId, r.Definition.PlayerText, r.Linked));
        return all;
    }

    internal static void Picked(string key)
    {
        var record = Responses.Find(r => r.Key == key);
        var picked = record?.Definition.OnPicked;
        if (picked == null)
            return;
        try { picked(); }
        catch (Exception e) { StoryLog.Failed("Response OnPicked of " + record.Owner.Info.Id, e); }
    }

    /// <summary>Links everything again after the Dialogue System reloaded its database, and anything that waited.</summary>
    internal static void ApplyAll()
    {
        var master = DialogueAnchors.Master;
        if (master == null)
            return;
        foreach (var record in Responses.ToArray())
        {
            try
            {
                if (record.AnchorId < 0 || record.Definition.AnchorText != null)
                {
                    var conversation = master.GetConversation(record.Definition.Conversation);
                    var anchor = conversation == null ? null
                        : DialogueAnchors.Find(master, conversation, record.Definition.AnchorText, record.Definition.AnchorId, out _, out _);
                    if (anchor == null)
                    {
                        record.Linked = false;
                        continue;
                    }
                    record.AnchorId = anchor.id;
                }
                Apply(master, record);
            }
            catch (Exception e)
            {
                StoryLog.Failed("Re-applying response " + record.Key, e);
            }
        }
    }

    internal static void Clear()
    {
        foreach (var record in Responses.ToArray())
            Detach(record);
        Responses.Clear();
        TrackedOwners.Clear();
    }

    private static bool Apply(DialogueDatabase master, Registered record)
    {
        var def = record.Definition;
        var conversation = master.GetConversation(def.Conversation);
        var anchor = conversation?.GetDialogueEntry(record.AnchorId);
        if (anchor == null)
            return record.Linked = false;

        if (record.PlayerId < 0)
        {
            record.Priority = DialoguePlan.RequestedPriority(def.Priority, DialogueAnchors.PlayerPriority(master, anchor));
            record.PlayerId = DialoguePlan.FreeIdFrom(DialoguePlan.StableId(record.Key), id => conversation.GetDialogueEntry(id) != null);
            record.NpcId = def.NpcReply == null ? -1 : DialoguePlan.FreeIdFrom(record.PlayerId + 1, id => conversation.GetDialogueEntry(id) != null);
        }
        var existing = conversation.GetDialogueEntry(record.PlayerId);
        if (existing != null)
            return record.Linked = true;

        int playerActor = DialogueAnchors.PlayerActorId(master);
        var template = Template.FromDefault();
        var player = template.CreateDialogueEntry(record.PlayerId, conversation.id, record.Key);
        player.ActorID = playerActor;
        player.ConversantID = anchor.ActorID;
        player.DialogueText = def.PlayerText;
        player.conditionsString = def.Condition ?? "";
        player.userScript = "Oben_ResponsePicked(\"" + record.Key + "\")";
        conversation.dialogueEntries.Add(player);
        anchor.outgoingLinks.Insert(DialoguePlan.InsertIndex(def.InsertAt, anchor.outgoingLinks.Count),
            new Link(conversation.id, anchor.id, conversation.id, record.PlayerId) { priority = (ConditionPriority)record.Priority });

        if (record.NpcId >= 0)
        {
            var npc = template.CreateDialogueEntry(record.NpcId, conversation.id, record.Key + "_reply");
            npc.ActorID = anchor.ActorID;
            npc.ConversantID = playerActor;
            npc.DialogueText = def.NpcReply;
            conversation.dialogueEntries.Add(npc);
            player.outgoingLinks.Add(new Link(conversation.id, record.PlayerId, conversation.id, record.NpcId) { priority = (ConditionPriority)record.Priority });
        }
        return record.Linked = true;
    }

    private static void Detach(Registered record)
    {
        var master = DialogueAnchors.Master;
        var conversation = master != null ? master.GetConversation(record.Definition.Conversation) : null;
        if (conversation == null || record.PlayerId < 0)
            return;
        try
        {
            var anchor = conversation.GetDialogueEntry(record.AnchorId);
            anchor?.outgoingLinks.RemoveAll(l => l.destinationConversationID == conversation.id && l.destinationDialogueID == record.PlayerId);
            conversation.dialogueEntries.RemoveAll(e => e.id == record.PlayerId || e.id == record.NpcId);
            DialogueAnchors.ForgetSimStatus(conversation.id, record.PlayerId);
            if (record.NpcId >= 0)
                DialogueAnchors.ForgetSimStatus(conversation.id, record.NpcId);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Removing response " + record.Key, e);
        }
        record.Linked = false;
    }

    internal static int RemoveOwner(ObenMod owner)
    {
        TrackedOwners.Remove(owner);
        var mine = Responses.FindAll(r => r.Owner == owner);
        foreach (var record in mine)
        {
            Responses.Remove(record);
            Detach(record);
        }
        return mine.Count;
    }

    private static string Check(ResponseDefinition r)
    {
        if (string.IsNullOrEmpty(r.Conversation))
            return "the conversation title is empty";
        if (r.AnchorText == null && r.AnchorId < 0)
            return "give the anchor line's text or entry id";
        if (r.AnchorText != null && r.AnchorText.Trim().Length == 0)
            return "the anchor text is blank";
        if (!DialoguePlan.ValidText(r.PlayerText, DialoguePlan.MaxText, true))
            return $"the player text must be 1 to {DialoguePlan.MaxText} characters";
        if (r.NpcReply != null && !DialoguePlan.ValidText(r.NpcReply, DialoguePlan.MaxText, true))
            return $"the reply must be 1 to {DialoguePlan.MaxText} characters, or null";
        if (!DialoguePlan.ValidText(r.Condition, DialoguePlan.MaxLua, false))
            return $"the condition must be at most {DialoguePlan.MaxLua} characters";
        if (r.InsertAt < -1)
            return "InsertAt is -1 or a link index";
        if (!DialoguePlan.ValidPriority(r.Priority))
            return "Priority is -1 or 0 to 4";
        return null;
    }

    private static DialogueEditResult Refuse(ConversationRefusal refusal, string reason)
    {
        StoryLog.Refused("DialogueResponses.Add", reason);
        return new DialogueEditResult(null, refusal, reason);
    }
}
