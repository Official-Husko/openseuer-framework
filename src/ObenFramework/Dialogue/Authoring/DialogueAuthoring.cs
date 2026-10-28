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
/// Conversations mods add to the Dialogue System. Each lives in a small database of its own next to the
/// game's, titled "modId.key", and can be started like any other (<see cref="Conversations.StartRaw"/>,
/// <see cref="Conversations.Start(string, string, bool)"/> with an NPC). They exist in memory only: they are
/// gone when the mod unloads, and nothing about them is written into the game's save beyond the variables
/// their scripts set, so removing the mod leaves the save as it was.
/// </summary>
public static class DialogueAuthoring
{
    private sealed class Added
    {
        public ObenMod Owner;
        public DialogueDatabase Database;
    }

    private static readonly Dictionary<string, Added> AddedConversations = new Dictionary<string, Added>(StringComparer.Ordinal);
    private static readonly HashSet<ObenMod> TrackedOwners = new HashSet<ObenMod>();

    /// <summary>The title a key of this mod gets, or null when the key is not allowed.</summary>
    public static string TitleOf(ObenMod owner, string key) =>
        Quests.QuestIds.TaskId(owner?.Info.Id ?? throw new ArgumentNullException(nameof(owner)), key);

    /// <summary>The titles of the conversations mods added, with the id of the mod that owns each.</summary>
    public static List<KeyValuePair<string, string>> All()
    {
        var all = new List<KeyValuePair<string, string>>();
        foreach (var pair in AddedConversations)
            all.Add(new KeyValuePair<string, string>(pair.Key, pair.Value.Owner.Info.Id));
        return all;
    }

    /// <summary>
    /// Adds a conversation. The first line opens it, each line's <see cref="ConversationLine.Next"/> says what
    /// can follow, and several player lines after one line make a choice menu. It is removed when
    /// <paramref name="owner"/> unloads.
    /// </summary>
    public static DialogueEditResult Add(ObenMod owner, ConversationDefinition definition)
    {
        if (owner == null) throw new ArgumentNullException(nameof(owner));
        if (definition != null && definition.Key == Narration.Key)
            return Refuse(ConversationRefusal.BadInput, $"the key '{Narration.Key}' is kept for narration");
        return AddWith(owner, definition, -1);
    }

    /// <summary>Removes one of this mod's conversations. False while it is running or when it isn't this mod's.</summary>
    public static bool Remove(ObenMod owner, string key)
    {
        string title = TitleOf(owner, key);
        if (title == null || DialogueBranches.OwnsConversation(title))
            return false;
        return RemoveTitle(owner, title);
    }

    internal static DialogueEditResult AddWith(ObenMod owner, ConversationDefinition definition, int speakerOverride)
    {
        string problem = DialoguePlan.Check(owner.Info.Id, definition);
        if (problem != null)
            return Refuse(ConversationRefusal.BadInput, problem);
        if (SaveGate.IsBusy)
            return Refuse(ConversationRefusal.Busy, "a save or load is running");
        var master = DialogueAnchors.Master;
        if (master == null)
            return Refuse(ConversationRefusal.NotReady, "the dialogue database is not loaded");

        string title = owner.Info.Id + "." + definition.Key;
        if (AddedConversations.ContainsKey(title) || master.GetConversation(title) != null)
            return Refuse(ConversationRefusal.AlreadyExists, $"there already is a conversation '{title}'");
        int playerId = DialogueAnchors.PlayerActorId(master);
        if (playerId < 0)
            return Refuse(ConversationRefusal.NoPlayerActor, "the dialogue database has no player actor");
        int speakerId = speakerOverride >= 0 ? speakerOverride : SpeakerId(master, definition.Speaker);
        if (speakerId < 0 || speakerId == playerId)
            return Refuse(ConversationRefusal.UnknownSpeaker, $"'{definition.Speaker}' is not an NPC actor of the dialogue database");

        try
        {
            int id = DialoguePlan.FreeIdFrom(DialoguePlan.StableId(title), i => master.GetConversation(i) != null);
            var conversation = ConversationBuilder.Build(title, id, playerId, speakerId, definition);
            var database = DatabaseUtility.CreateDialogueDatabaseInstance(false);
            database.name = "ObenFramework " + title;
            database.emphasisSettings = master.emphasisSettings;
            database.conversations.Add(conversation);
            LineTexts.Prepare(title, conversation, definition);
            DialogueManager.AddDatabase(database);
            if (master.GetConversation(title) == null)
            {
                LineTexts.Drop(title);
                UnityEngine.Object.Destroy(database);
                return Refuse(ConversationRefusal.Failed, "the Dialogue System did not take the conversation");
            }
            AddedConversations[title] = new Added { Owner = owner, Database = database };
            Track(owner);
            DialogueBranches.RetryWaiting();
            return new DialogueEditResult(title, ConversationRefusal.None, null);
        }
        catch (Exception e)
        {
            StoryLog.Failed("DialogueAuthoring.Add", e);
            LineTexts.Drop(title);
            return Refuse(ConversationRefusal.Failed, "a Dialogue System call threw: " + e.Message);
        }
    }

    internal static bool RemoveTitle(ObenMod owner, string title)
    {
        if (!AddedConversations.TryGetValue(title, out var added) || added.Owner != owner || SaveGate.IsBusy)
            return false;
        if (Conversations.IsActive && Conversations.CurrentTitle == title)
            return false;
        Drop(title, added);
        return true;
    }

    /// <summary>Puts the mod databases back after the Dialogue System reset its list (it does on some loads).</summary>
    internal static void Restore()
    {
        var master = DialogueAnchors.Master;
        if (master == null)
            return;
        foreach (var pair in AddedConversations)
        {
            if (pair.Value.Database != null && master.GetConversation(pair.Key) == null)
                DialogueManager.AddDatabase(pair.Value.Database);
        }
    }

    private static void Drop(string title, Added added)
    {
        AddedConversations.Remove(title);
        LineTexts.Drop(title);
        try
        {
            if (DialogueManager.hasInstance)
                DialogueManager.RemoveDatabase(added.Database);
            UnityEngine.Object.Destroy(added.Database);
        }
        catch (Exception e)
        {
            StoryLog.Failed("Removing conversation " + title, e);
        }
    }

    private static void Track(ObenMod owner)
    {
        if (TrackedOwners.Add(owner))
            owner.Track(Disposable.Create(() => RemoveOwner(owner)));
    }

    private static void RemoveOwner(ObenMod owner)
    {
        TrackedOwners.Remove(owner);
        var titles = new List<string>();
        foreach (var pair in AddedConversations)
        {
            if (pair.Value.Owner == owner)
                titles.Add(pair.Key);
        }
        foreach (string title in titles)
            Drop(title, AddedConversations[title]);
    }

    internal static void Clear()
    {
        foreach (var pair in new List<KeyValuePair<string, Added>>(AddedConversations))
            Drop(pair.Key, pair.Value);
        TrackedOwners.Clear();
    }

    private static int SpeakerId(DialogueDatabase master, string speaker)
    {
        if (speaker != null)
        {
            var actor = master.GetActor(speaker);
            return actor == null || actor.IsPlayer ? -1 : actor.id;
        }
        // A nameless voice: an actor id no actor has.
        int max = 0;
        foreach (var actor in master.actors)
            max = Math.Max(max, actor.id);
        return max + 1;
    }

    private static DialogueEditResult Refuse(ConversationRefusal refusal, string reason)
    {
        StoryLog.Refused("DialogueAuthoring.Add", reason);
        return new DialogueEditResult(null, refusal, reason);
    }
}
