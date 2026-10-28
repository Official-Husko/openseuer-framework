// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using NPC;
using UnityEngine;

namespace ObenFramework.Npcs;

// Walking, arriving, wandering and patrolling.
internal static partial class NpcOrders
{
    private static void StartWalk(NpcOrder order, Vector3 point)
    {
        var controller = order.Npc.Info.npcController;
        if (NpcWalkMath.IsOrigin(point.x, point.y, point.z))
            point.x += OriginNudge;
        order.Target = point;
        order.Walking = true;
        order.Arrived = false;
        SetSpeed(controller, order.Run);
        order.Timer = NpcWalkMath.Timeout(Vector3.Distance(controller.transform.position, point), controller.richAI.maxSpeed);
        controller.SetTargetPosition(point, ArriveDistance, order.OnArrive);
    }

    private static void Arrive(NpcOrder order)
    {
        order.Walking = false;
        if (order.Kind == NpcStepKind.Wander)
        {
            order.Timer = NpcWalkMath.WaitSeconds(Random);
        }
        else if (order.Kind == NpcStepKind.Patrol)
        {
            order.Index = NpcWalkMath.NextPatrolIndex(order.Index, order.Points.Length);
            StartWalk(order, order.Points[order.Index]);
        }
        else
        {
            order.Npc.Root.transform.rotation = Quaternion.Euler(0f, order.Yaw, 0f);
            if (order.Once)
                Finish(order, true);
            else
                Report(order, true);
        }
    }

    // A routine NPC that got stuck is put where it was going; a mod's walk that got stuck fails.
    private static void TimeOut(NpcOrder order)
    {
        if (order.Routine)
            order.Npc.Info.npcController.richAI.Teleport(order.Target);
        if (order.Routine || order.Kind == NpcStepKind.Wander || order.Kind == NpcStepKind.Patrol)
            Arrive(order);
        else
            Finish(order, false);
    }

    private static bool PickWanderPoint(NpcOrder order, out Vector3 point)
    {
        var center = order.Points[0];
        for (int attempt = 0; attempt < WanderAttempts; attempt++)
        {
            NpcWalkMath.DiscPoint(Random, order.Radius, out float dx, out float dz);
            var near = AstarPath.active.GetNearest(new Vector3(center.x + dx, center.y, center.z + dz));
            if (near.node == null)
                continue;
            var offset = near.position - center;
            offset.y = 0f;
            if (offset.magnitude > order.Radius * WanderLeash)
                continue;
            point = near.position;
            return true;
        }
        point = center;
        return false;
    }

    private static void SetSpeed(NPCController controller, bool run)
    {
        if (controller.movementAnimation != null)
            controller.movementAnimation.SetMovementSpeed(run ? OpenSewerNPCMovementAnimation.MovementTypes.Run : OpenSewerNPCMovementAnimation.MovementTypes.SlowWalk);
    }
}
