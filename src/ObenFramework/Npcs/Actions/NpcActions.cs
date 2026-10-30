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
/// loaded, is hidden or dead, or a save or load is running. Moving one (teleport, follow, stop its activity)
/// also needs its <c>NPCController</c>, the path finding most of the game's NPCs don't have; killing,
/// hitting, talking and looking work on every NPC. What is said and looked at is in the other half of this
/// class (talk, barks, looks).
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

    /// <summary>
    /// Kills the NPC the way the game does (its state becomes Dead). Raises <see cref="Npcs.Died"/>. The game doesn't
    /// save an NPC's state, so it is alive again the next time its level loads: to keep it dead, remember it in your
    /// mod's save data and kill it again when the world is ready.
    /// </summary>
    public static bool Kill(int npcId)
    {
        var npc = Present(npcId);
        if (npc == null)
            return false;
        return Run("Kill", () =>
        {
            // Only a walking NPC has an activity to stop; the game's own Kill copes without one.
            var controller = npc.npcController;
            if (controller != null && controller.Data != null && controller.Data.state != null)
                controller.ForceStopActivity();
            if (controller != null && controller.richAI != null)
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
        var npc = Present(npcId);
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

            var data = npc.npcController != null ? npc.npcController.Data : null;
            var activity = data != null && data.state != null ? data.state.CurrentActivity : null;
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
    /// Ends whatever the NPC is doing (a chase, a conversation walk, a follow, sitting, working), even an
    /// activity a claim's <c>Pause</c> won't interrupt. Its timetable picks the next activity as usual.
    /// False when it isn't doing anything or isn't in the scene.
    /// </summary>
    public static bool StopActivity(int npcId)
    {
        var npc = Steerable(npcId);
        var controller = npc != null ? npc.npcController : null;
        if (controller == null || controller.Data == null || controller.Data.state == null || controller.Data.state.CurrentActivity == null)
            return false;
        return Run("StopActivity", controller.ForceStopActivity);
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
    /// <summary>The NPC when it is in the scene, shown and alive, and no save or load runs; else null.</summary>
    internal static NPCInfo Present(int npcId)
    {
        if (SaveGate.IsBusy)
            return null;
        var npc = Npcs.LiveObject(npcId);
        if (npc == null || !npc.gameObject.activeInHierarchy || npc.IsDead())
            return null;
        return npc;
    }

    /// <summary>Like <see cref="Present"/>, and the NPC has a controller to move it.</summary>
    internal static NPCInfo Steerable(int npcId)
    {
        var npc = Present(npcId);
        return npc != null && npc.npcController != null ? npc : null;
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
