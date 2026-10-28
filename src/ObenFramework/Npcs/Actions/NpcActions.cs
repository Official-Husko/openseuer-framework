// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using NPC;
using ObenFramework.Saves;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// One-off things to do to an NPC in the scene you are in: move it, kill it, hit it, make it follow the
/// player, send it to another scene. Each call acts once and leaves the NPC to the game afterwards; to hold
/// an NPC for a while use <see cref="NpcControl.Claim"/>. Every call returns false when the NPC is not
/// loaded, is dead, cannot be steered (no <c>NPCController</c>) or a save or load is running.
/// What is said and looked at is in the other half of this class (talk, barks, looks).
/// </summary>
public static partial class NpcActions
{
    /// <summary>Puts the NPC at a point at once, through its path finding agent so it stays on the navigation mesh.</summary>
    public static bool Teleport(int npcId, Vector3 position)
    {
        var npc = Steerable(npcId);
        var ai = npc != null ? npc.npcController.richAI : null;
        if (ai == null)
            return false;
        return Run("Teleport", () => ai.Teleport(position));
    }

    /// <summary>Kills the NPC the way the game does (its state becomes Dead; the save keeps it). Raises <see cref="Npcs.Died"/>.</summary>
    public static bool Kill(int npcId)
    {
        var npc = Steerable(npcId);
        if (npc == null)
            return false;
        return Run("Kill", () =>
        {
            var controller = npc.npcController;
            if (controller.Data != null && controller.Data.state != null)
                controller.ForceStopActivity();
            if (controller.richAI != null)
                controller.richAI.canMove = false;
            npc.Kill();
        });
    }

    /// <summary>
    /// Does what a punch from the player does, without the damage: the NPC reacts (a comment or a talk),
    /// the hit counts as a violent crime when the NPC reports those, a police officer starts a chase and a
    /// robber gives up. Use it to make NPCs react to things your mod does to them.
    /// </summary>
    public static bool Hit(int npcId)
    {
        var npc = Steerable(npcId);
        if (npc == null || npc.character == null || Crime.instance == null)
            return false;
        return Run("Hit", () =>
        {
            if (npc.onHit != null)
                npc.onHit.triggerOutputs();
            if (!npc.dontCommentOnHit)
                ReactToHit(npc);
            if (!npc.dontGiveCrimeOnHit)
            {
                Crime.instance.CommitCrime(npc.character.NPCReference(), 100, Crime.CrimeType.Violence);
                Witness.IsWitnessed();
            }
            if (npc.setAnimatorValue != null)
                npc.setAnimatorValue.Trigger("hit");

            var activity = npc.npcController.Data != null && npc.npcController.Data.state != null ? npc.npcController.Data.state.CurrentActivity : null;
            if (activity is Act_Police police)
                police.StartChase();
            else if (activity is Act_Robber robber)
                robber.AbortRobbing();
        });
    }

    /// <summary>
    /// Makes the NPC follow the player, walking or running. With <paramref name="untilStopped"/> the game's
    /// schedule cannot interrupt it and only <see cref="StopFollowing"/> (or a level change) ends it.
    /// </summary>
    public static bool FollowPlayer(int npcId, bool run = false, bool untilStopped = true)
    {
        var npc = Steerable(npcId);
        var controller = npc != null ? npc.npcController : null;
        if (controller == null || controller.Data == null || controller.Data.character == null)
            return false;
        bool started = false;
        Run("FollowPlayer", () =>
        {
            var follow = controller.FollowPlayer(run, !untilStopped, false);
            started = controller.Data.state != null && controller.Data.state.CurrentActivity == follow;
        });
        return started;
    }

    /// <summary>Ends a follow started by <see cref="FollowPlayer"/> or by the game. False when the NPC was not following.</summary>
    public static bool StopFollowing(int npcId)
    {
        var npc = Steerable(npcId);
        var controller = npc != null ? npc.npcController : null;
        if (controller == null || controller.Data == null || controller.Data.state == null || !(controller.Data.state.CurrentActivity is Act_Follow))
            return false;
        return Run("StopFollowing", controller.ForceStopActivity);
    }

    /// <summary>
    /// Sends an NPC that is not in the scene you are in to another scene, the way the game's off-scene
    /// simulation moves NPCs. Refused for NPCs in your scene, NPCs on the way somewhere, NPCs following
    /// someone, NPCs whose timetable wants them elsewhere, and scenes that do not exist. The NPC turns up at
    /// the scene's default spot the next time it loads.
    /// </summary>
    public static bool ChangeLevel(int npcId, string scene)
    {
        if (string.IsNullOrEmpty(scene) || SaveGate.IsBusy)
            return false;
        var data = Npcs.DataOf(npcId);
        var state = data != null ? data.state : null;
        var manager = NPCManager.instance;
        if (state == null || manager == null || state.currentScene == scene || state.activePath != null || data.Controller != null)
            return false;
        if (state.currentScene == manager.ActiveScene)
            return false;
        if (state.CurrentActivity != null && state.CurrentActivity.GetFollowTarget() != null)
            return false;
        var wanted = NpcNames.PlaceOf(data.scheduler != null && data.scheduler.currentScheduledAction != null ? data.scheduler.currentScheduledAction.activity : null);
        if (!state.schedulerDisabled && wanted != null && wanted != scene)
            return false;
        if (!SaveController.CheckIfLevelExists(scene))
            return false;

        bool moved = false;
        Run("ChangeLevel", () =>
        {
            NPCSceneUtilities.ChangeNPCLevel(data, scene);
            moved = state.currentScene == scene;
            if (moved)
                state.currentPosition = Vector3.zero;
        });
        return moved;
    }

    // A living, active NPC in this scene with a controller, outside saves and loads.
    internal static NPCInfo Steerable(int npcId)
    {
        if (SaveGate.IsBusy)
            return null;
        var npc = Npcs.LiveObject(npcId);
        if (npc == null || npc.npcController == null || !npc.gameObject.activeInHierarchy || npc.IsDead())
            return null;
        return npc;
    }

    private static void ReactToHit(NPCInfo npc)
    {
        string text = string.IsNullOrEmpty(npc.onHitDialogue) ? "Generic/Hit" : npc.onHitDialogue;
        if (npc.startDialogueOnHit)
        {
            var talk = npc.GetComponent<InteractableTalk>();
            if (talk != null)
                talk.StartDialogue(text);
            return;
        }
        var bark = npc.GetComponent<CharacterBarkStarter>();
        if (bark != null)
            bark.TryToCommentLookAtPlayer5s(text);
    }

    private static bool Run(string what, Action action)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("NpcActions." + what, e);
            return false;
        }
    }
}
