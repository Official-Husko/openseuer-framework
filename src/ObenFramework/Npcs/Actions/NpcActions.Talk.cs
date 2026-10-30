// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Dialogue;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace ObenFramework.Npcs;

public static partial class NpcActions
{
    /// <summary>
    /// Shows a line over the NPC's head the way its barks appear (a short subtitle, no conversation). The
    /// text is shown as given. Refused during a conversation, when the NPC has no bark display, and when a
    /// mod has claimed the NPC with <see cref="NpcHolds.Barks"/>.
    /// </summary>
    public static bool Say(int npcId, string text)
    {
        if (string.IsNullOrEmpty(text))
            return false;
        var npc = Present(npcId);
        if (npc == null || NpcControl.Holds(npc, NpcHolds.Barks) || !DialogueManager.hasInstance || DialogueManager.isConversationActive || Subtitles.instance == null)
            return false;
        return Run("Say", () =>
        {
            var bark = DialogueActor.GetBarkUI(npc.transform) as MonoBehaviour;
            if (bark == null || !bark.enabled)
                throw new System.InvalidOperationException($"{npc.name} has no bark display");
            if (bark is SubtitleBarkUI subtitle)
                subtitle.Start();
            DialogueManager.BarkString(text, npc.transform);
        });
    }

    /// <summary>
    /// Makes the NPC bark a line picked from a conversation (the game's comments: "Generic/Hit" and the
    /// like). Refused when the conversation does not exist or the NPC's barks are held by a claim.
    /// </summary>
    public static bool Comment(int npcId, string conversation)
    {
        if (string.IsNullOrEmpty(conversation))
            return false;
        var npc = Present(npcId);
        var starter = npc != null ? npc.characterBarkStarter : null;
        if (starter == null || !starter.enabled || starter.cacheBarkLines || NpcControl.Holds(npc, NpcHolds.Barks))
            return false;
        if (!Conversations.Exists(conversation) || (Conversations.IsActive && !starter.allowDuringConversations))
            return false;
        return Run("Comment", () => npc.TryComment(conversation));
    }

    /// <summary>
    /// Starts a conversation with the NPC as if the player had talked to it, with the given conversation
    /// instead of its usual one. Refused while another conversation runs or when the NPC cannot talk.
    /// </summary>
    public static bool StartConversation(int npcId, string conversation)
    {
        if (!Conversations.Exists(conversation) || Conversations.IsActive)
            return false;
        var npc = Present(npcId);
        var talk = npc != null ? npc.interactableTalk : null;
        if (talk == null)
            talk = npc != null ? npc.GetComponent<InteractableTalk>() : null;
        if (talk == null)
            return false;
        bool started = false;
        Run("StartConversation", () => started = talk.CheckStartDialogue(conversation) && Conversations.IsActive);
        return started;
    }

    /// <summary>Turns the NPC's head to the player, for <paramref name="seconds"/> when above zero, else until <see cref="StopLooking"/>.</summary>
    public static bool LookAtPlayer(int npcId, int seconds = 0)
    {
        var npc = Present(npcId);
        var head = npc != null ? npc.characterHeadController : null;
        if (head == null || ThirdPersonCameraCollision.instance == null)
            return false;
        if (seconds > 0)
            return head.isActiveAndEnabled && Run("LookAtPlayer", () => head.LookAtPlayerTime(seconds));
        return Run("LookAtPlayer", head.LookAtPlayerDialogue);
    }

    /// <summary>Turns the NPC's eyes and head to follow a transform until <see cref="StopLooking"/>. The body stays as it is.</summary>
    public static bool LookAt(int npcId, Transform target)
    {
        var npc = Present(npcId);
        var head = npc != null ? npc.characterHeadController : null;
        if (head == null || target == null)
            return false;
        return Run("LookAt", () => head.LookAtNPCTarget(target));
    }

    /// <summary>Lets the NPC's head go back to its usual looking around.</summary>
    public static bool StopLooking(int npcId)
    {
        var npc = Present(npcId);
        var head = npc != null ? npc.characterHeadController : null;
        if (head == null)
            return false;
        return Run("StopLooking", () =>
        {
            head.StopLookAtTarget();
            head.EnableLookAtPlayer();
        });
    }
}
