using System;
using ObenFramework.Events;
using ObenFramework.Game;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Dialogue;

/// <summary>
/// Conversations with NPCs. <see cref="Start(string, string, bool)"/> goes through the NPC's own
/// <c>InteractableTalk</c>, which is what pressing Use does: the NPC stops, looks at the player, registers
/// its dialogue relays and conditions, and unregisters them again at the end.
/// <see cref="StartRaw"/> only opens a conversation from the database with no NPC behaviour.
/// Call these on the main thread, in a playable world.
/// </summary>
public static class Conversations
{
    private static readonly Action NoOp = () => { };

    /// <summary>
    /// A conversation opened (NPC talks, phone calls, narration, anything). Each handler runs in its
    /// own try/catch. Not related to the game's DialogueController.onDialogueClosed.
    /// </summary>
    public static readonly GameEvent<ConversationInfo> Started = new GameEvent<ConversationInfo>(nameof(Started));

    /// <summary>
    /// A conversation closed. Fires once per conversation, after the game's own end-of-talk cleanup was
    /// triggered but possibly before the player has control back (that happens on the next frame).
    /// </summary>
    public static readonly GameEvent<ConversationInfo> Ended = new GameEvent<ConversationInfo>(nameof(Ended));

    /// <summary>True while any conversation is running.</summary>
    public static bool IsActive => DialogueManager.hasInstance && DialogueManager.isConversationActive;

    /// <summary>The title of the running conversation, or null.</summary>
    public static string CurrentTitle => IsActive ? DialogueManager.lastConversationStarted : null;

    /// <summary>True when the dialogue database has a conversation with this title.</summary>
    public static bool Exists(string title)
    {
        if (string.IsNullOrEmpty(title) || !DialogueManager.hasInstance)
            return false;
        var database = DialogueManager.masterDatabase;
        return database != null && database.GetConversation(title) != null;
    }

    /// <summary>
    /// Finds the loaded NPC (the "Talk" interactable) by the name shown when you look at it, such as
    /// "Pentti Penttilä", or by the character's full name. Case-insensitive; null when not in this scene.
    /// </summary>
    public static InteractableTalk FindNpc(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;

        var talks = InteractableTalk.interactableTalks;
        for (int i = 0; i < talks.Count; i++)
        {
            var talk = talks[i];
            if (talk == null || !talk.isActiveAndEnabled)
                continue;
            if (string.Equals(talk.title, name, StringComparison.OrdinalIgnoreCase))
                return talk;
            var info = talk.GetComponent<NPCInfo>();
            if (info != null && info.character != null && string.Equals(info.character.FullName, name, StringComparison.OrdinalIgnoreCase))
                return talk;
        }
        return null;
    }

    /// <summary>Talks to the NPC with this name; see <see cref="Start(InteractableTalk, string, bool)"/>.</summary>
    public static ConversationStart Start(string npcName, string conversation = null, bool force = false)
    {
        if (!CanStart(force, out var blocked))
            return blocked;
        var talk = FindNpc(npcName);
        return talk == null ? ConversationStart.UnknownNpc : Start(talk, conversation, force);
    }

    /// <summary>
    /// Starts a conversation with an NPC the way the game does. <paramref name="conversation"/> is a
    /// conversation title, or empty to use the NPC's default one. Without <paramref name="force"/> the
    /// game's usual rules apply (not while another conversation runs, the NPC must be able to talk).
    /// With it, the NPC talks regardless and a running conversation is stopped first.
    /// </summary>
    public static ConversationStart Start(InteractableTalk talk, string conversation = null, bool force = false)
    {
        if (talk == null)
            return ConversationStart.UnknownNpc;
        if (!CanStart(force, out var blocked))
            return blocked;
        if (!string.IsNullOrEmpty(conversation) && !Exists(conversation))
            return ConversationStart.UnknownConversation;

        if (!talk.CheckStartDialogueInterrupt(conversation ?? "", force, ignoreMenuState: true))
            return ConversationStart.CannotTalk;
        return IsActive ? ConversationStart.Started : ConversationStart.NoValidEntry;
    }

    /// <summary>
    /// Opens a conversation from the database with the player as the first speaker and no NPC behaviour:
    /// the NPC keeps walking and its relays are not registered. The game still locks and unlocks the
    /// player's controls by itself. Use this for narration and phone-style conversations. Before it starts,
    /// the game's leftover end-of-talk callback from the last NPC conversation is cleared, because it would
    /// otherwise run again when this one closes.
    /// </summary>
    public static ConversationStart StartRaw(string title, Transform conversant = null)
    {
        if (!CanStart(false, out var blocked))
            return blocked;
        if (!Exists(title))
            return ConversationStart.UnknownConversation;

        var controller = DialogueController.instance;
        controller.onDialogueClosed = NoOp;
        controller.SetCurrentInteractableTalk(null);
        DialogueManager.StartConversation(title, OpenSewerCharacterController.instance.transform, conversant);
        return IsActive ? ConversationStart.Started : ConversationStart.NoValidEntry;
    }

    /// <summary>Ends every running conversation.</summary>
    public static void Stop()
    {
        if (DialogueManager.hasInstance)
            DialogueManager.StopAllConversations();
    }

    private static bool CanStart(bool force, out ConversationStart blocked)
    {
        blocked = ConversationStart.NotInWorld;
        if (!WorldState.IsReady || LuaState.IsLoading || !DialogueManager.hasInstance
            || DialogueController.instance == null || OpenSewerCharacterController.instance == null)
            return false;

        if (!force && (IsActive || DialogueController.instance.DialogueActive))
        {
            blocked = ConversationStart.Busy;
            return false;
        }
        return true;
    }
}
