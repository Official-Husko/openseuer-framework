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
/// Whole sub-conversations joined into the game's conversations. The branch is a mod conversation of its own
/// (see <see cref="DialogueAuthoring"/>) spoken by the anchor line's NPC; the anchor gets links to the
/// branch's first lines, so they show up in the player's menu. Branches are re-linked after the Dialogue
/// System reloads its database and are taken out when the mod unloads; the game's save never holds them.
/// </summary>
public static partial class DialogueBranches
{
    private sealed class Registered
    {
        public ObenMod Owner;
        public string Key;
        public BranchDefinition Definition;
        public int AnchorId = -1;
        public string BranchTitle;
        public int BranchId = -1;
        public bool Linked;
        public bool Failed;
    }

    private static readonly List<Registered> Branches = new List<Registered>();
    private static readonly HashSet<ObenMod> TrackedOwners = new HashSet<ObenMod>();

    /// <summary>
    /// Joins a branch to a game NPC line and returns its key. When the conversation or line isn't loaded yet
    /// the branch waits and is linked once it is (<see cref="DialogueEditResult.Waiting"/>).
    /// </summary>
    public static DialogueEditResult Add(ObenMod owner, BranchDefinition branch)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (branch == null) throw new ArgumentNullException(nameof(branch));
        string problem = Check(owner.Info.Id, branch);
        if (problem != null)
            return Refuse(ConversationRefusal.BadInput, problem);
        if (SaveGate.IsBusy)
            return Refuse(ConversationRefusal.Busy, "a save or load is running");
        var master = DialogueAnchors.Master;
        if (master == null)
            return Refuse(ConversationRefusal.NotReady, "the dialogue database is not loaded");

        var conversation = master.GetConversation(branch.Conversation);
        DialogueEntry anchor = null;
        if (conversation != null)
        {
            anchor = DialogueAnchors.Find(master, conversation, branch.AnchorText, branch.AnchorId, out string why, out bool absent);
            if (anchor == null && !absent)
                return Refuse(ConversationRefusal.NoAnchor, why);
        }

        string key = DialoguePlan.BranchKey(owner.Info.Id, branch.Conversation, branch.AnchorText, branch.AnchorId, branch.Lines.Key);
        if (Branches.Exists(b => b.Key == key))
            return Refuse(ConversationRefusal.AlreadyExists, "this branch is already added; remove it first");

        var record = new Registered { Owner = owner, Key = key, Definition = branch, AnchorId = anchor != null ? anchor.id : -1 };
        if (anchor != null)
        {
            string failed = Link(master, conversation, anchor, record);
            if (failed != null)
            {
                Unlink(master, record);
                return Refuse(ConversationRefusal.Failed, failed);
            }
        }
        Branches.Add(record);
        if (TrackedOwners.Add(owner))
            owner.Track(Disposable.Create(() => RemoveOwner(owner)));
        return new DialogueEditResult(key, ConversationRefusal.None, null, waiting: anchor == null);
    }

    /// <summary>
    /// A quick topic: a player line on the first NPC line of a conversation (shown while the Lua
    /// <paramref name="showWhen"/> holds), answered by the NPC's lines in order. <paramref name="onDone"/> runs
    /// when the player picks it.
    /// </summary>
    public static DialogueEditResult AddTopic(ObenMod owner, string conversation, string playerLine, string[] npcLines, string showWhen = null, Action onDone = null)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(conversation) || string.IsNullOrEmpty(playerLine) || npcLines == null || npcLines.Length == 0)
            return Refuse(ConversationRefusal.BadInput, "the conversation, the player line and at least one NPC line are needed");
        var master = DialogueAnchors.Master;
        if (master == null)
            return Refuse(ConversationRefusal.NotReady, "the dialogue database is not loaded");
        var target = master.GetConversation(conversation);
        if (target == null)
            return Refuse(ConversationRefusal.NoAnchor, $"there is no conversation '{conversation}'");
        var anchor = DialogueAnchors.FirstNpcLine(master, target);
        if (anchor == null)
            return Refuse(ConversationRefusal.NoAnchor, "the conversation has no NPC line that leads to player choices");
        return Add(owner, new BranchDefinition
        {
            Conversation = conversation,
            AnchorId = anchor.id,
            OnPicked = onDone,
            Lines = DialoguePlan.Topic(owner.Info.Id, conversation, anchor.id, playerLine, npcLines, showWhen),
        });
    }

    /// <summary>Takes one of this mod's branches out. False while a conversation runs, during a save, or for another mod's key.</summary>
    public static bool Remove(ObenMod owner, string key)
    {
        var record = Branches.Find(b => b.Owner == owner && b.Key == key);
        if (record == null || SaveGate.IsBusy || Conversations.IsActive)
            return false;
        Branches.Remove(record);
        Drop(record);
        return true;
    }

    /// <summary>Every registered branch: key, owner id, conversation, anchor id, the branch's own title and whether it is linked.</summary>
    public static List<(string Key, string Owner, string Conversation, int AnchorId, string BranchTitle, bool Linked)> All()
    {
        var all = new List<(string, string, string, int, string, bool)>(Branches.Count);
        foreach (var b in Branches)
            all.Add((b.Key, b.Owner.Info.Id, b.Definition.Conversation, b.AnchorId, b.BranchTitle, b.Linked));
        return all;
    }

    internal static bool OwnsConversation(string title) => title != null && Branches.Exists(b => b.BranchTitle == title);

    internal static void Picked(string key)
    {
        var record = Branches.Find(b => b.Key == key);
        var picked = record?.Definition.OnPicked;
        if (picked == null)
            return;
        try { picked(); }
        catch (Exception e) { StoryLog.Failed("Branch OnPicked of " + record.Owner.Info.Id, e); }
    }

    internal static int RemoveOwner(ObenMod owner)
    {
        TrackedOwners.Remove(owner);
        var mine = Branches.FindAll(b => b.Owner == owner);
        foreach (var record in mine)
        {
            Branches.Remove(record);
            Drop(record);
        }
        return mine.Count;
    }

    internal static void Clear()
    {
        foreach (var record in Branches.ToArray())
            Drop(record);
        Branches.Clear();
        TrackedOwners.Clear();
    }

    private static void Drop(Registered record)
    {
        var master = DialogueAnchors.Master;
        try
        {
            if (master != null)
                Unlink(master, record);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Removing branch " + record.Key, e);
        }
    }

    private static string Check(string modId, BranchDefinition b)
    {
        if (string.IsNullOrEmpty(b.Conversation))
            return "the conversation title is empty";
        if (b.AnchorText == null && b.AnchorId < 0)
            return "give the anchor line's text or entry id";
        if (b.AnchorText != null && b.AnchorText.Trim().Length == 0)
            return "the anchor text is blank";
        if (b.Lines == null)
            return "the branch has no lines";
        if (b.Lines.Key == Narration.Key)
            return $"the key '{Narration.Key}' is kept for narration";
        if (b.InsertAt < -1)
            return "InsertAt is -1 or a link index";
        if (!DialoguePlan.ValidPriority(b.Priority))
            return "Priority is -1 or 0 to 4";
        return DialoguePlan.Check(modId, b.Lines);
    }

    private static DialogueEditResult Refuse(ConversationRefusal refusal, string reason)
    {
        StoryLog.Refused("DialogueBranches.Add", reason);
        return new DialogueEditResult(null, refusal, reason);
    }
}
