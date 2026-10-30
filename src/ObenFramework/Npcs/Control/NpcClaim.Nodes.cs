// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using NPC;

namespace ObenFramework.Npcs;

public sealed partial class NpcClaim
{
    // Above anything a timetable starts (they use 1 to a few), so the schedule doesn't take the NPC away
    // while the claim has it walking.
    private const int NodePriority = 100;

    private Activity _started;

    /// <summary>The patrol or stand this claim started is still what the NPC is doing.</summary>
    public bool OnNodes => _started != null && IsValid && _npc.npcController.Data.state.CurrentActivity == _started && _started.running;

    /// <summary>
    /// Walks the NPC round one of the scene's node groups (<see cref="NpcNodes"/>) with the game's own patrol:
    /// the group's order, looping and node events, its doors and waypoints. It goes on until the claim stops
    /// it (<see cref="StopNodes"/>, <see cref="Pause"/>, a walk) or ends. False when the group has no nodes,
    /// the NPC is frozen, another mod paused it, or its activity can't be interrupted.
    /// </summary>
    public bool Patrol(NodeGroup group)
    {
        if (group == null || group.nodes == null || group.nodes.Count == 0)
            return false;
        var patrol = new Act_Patrol();
        patrol.SetNodeGroup(group);
        return StartOnNodes(patrol);
    }

    /// <summary>
    /// Sends the NPC to stand at a node group's stand node (the group's <c>nodeReference</c>) with the game's
    /// own stand activity, until the claim stops it. False when the group has no stand node; see
    /// <see cref="Patrol"/> for the rest.
    /// </summary>
    public bool Stand(NodeGroup group)
    {
        if (group == null || group.nodeReference == null || group.nodeReference.node == null)
            return false;
        return StartOnNodes(new Act_Stand { node = group.nodeReference });
    }

    /// <summary>Ends a patrol or stand this claim started where the NPC is; its schedule takes over again.</summary>
    public void StopNodes()
    {
        var started = _started;
        _started = null;
        if (started == null || _npc == null || _npc.npcController == null)
            return;
        var controller = _npc.npcController;
        if (controller.Data?.state == null || controller.Data.state.CurrentActivity != started)
            return;
        controller.ForceStopActivity();
        // Stopping the activity leaves the path to its next node in place.
        var ai = controller.ai;
        if (ai != null)
        {
            ai.destination = _npc.transform.position;
            ai.SearchPath();
        }
    }

    private bool StartOnNodes(Activity activity)
    {
        if (!IsValid || (_holds & NpcHolds.Freeze) != 0)
            return false;
        var controller = _npc.npcController;
        if (controller.Data?.state == null)
            return false;
        if (controller.activityDisabled && !(_paused && _restoreEnabled))
            return false;

        CancelMove();
        if (_paused)
        {
            // The game only runs activities while they are switched on.
            controller.EnableActivity();
            _paused = false;
        }
        StopNodes();
        activity.priority = NodePriority;
        if (!controller.StartActivity(activity, interrupt: true))
            return false;
        _started = activity;
        return true;
    }
}
