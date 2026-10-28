using System;
using UnityEngine;

namespace ObenFramework.Npcs;

public sealed partial class NpcClaim
{
    private const float DefaultStopDistance = 0.6f;
    private const float MinRunSeconds = 1f;
    private const float PartialPathSlack = 1f;
    private const float FacingToleranceDegrees = 1f;

    private bool _moving;
    private Vector3 _target;
    private float _stopDistance;
    private float _startedAt;
    private float _deadline;
    private Action<bool> _done;

    private Transform _lookTarget;
    private Vector3 _lookPoint;
    private bool _lookAtPoint;
    private bool _headLook;

    /// <summary>A walk started by <see cref="MoveTo"/> is under way.</summary>
    public bool Moving => _moving;

    /// <summary>
    /// Walks the NPC to a point using the game's path finding, pausing its activity first (see
    /// <see cref="Pause"/>). <paramref name="done"/> gets true when the NPC is within
    /// <paramref name="stopDistance"/> metres, and false when the walk is cancelled, times out after
    /// <paramref name="timeoutSeconds"/> or ends short of the point because it is out of reach. A new walk
    /// cancels the one before. Returns false when the NPC cannot be paused or has no way to walk.
    /// </summary>
    public bool MoveTo(Vector3 target, Action<bool> done = null, float stopDistance = DefaultStopDistance, float timeoutSeconds = 120f)
    {
        if (!Pause())
            return false;
        var ai = _npc.npcController.ai;
        if (ai == null)
            return false;

        if (_moving)
            FinishMove(false, halt: false);
        _target = target;
        _stopDistance = Mathf.Max(0.1f, stopDistance);
        _startedAt = Time.time;
        _deadline = _startedAt + Mathf.Max(1f, timeoutSeconds);
        _done = done;
        _moving = true;

        ai.destination = target;
        ai.SearchPath();
        return true;
    }

    /// <summary>Cancels a walk where the NPC stands. The <c>done</c> callback gets false.</summary>
    public void CancelMove()
    {
        if (_moving)
            FinishMove(false, halt: true);
    }

    private void TickMove()
    {
        if (!_moving)
            return;
        var controller = _npc.npcController;
        var ai = controller.ai;
        if (ai == null)
        {
            FinishMove(false, halt: false);
            return;
        }

        float distance = FlatDistance(_npc.transform.position, _target);
        if (distance <= _stopDistance)
            FinishMove(true, halt: false);
        else if (Time.time >= _deadline)
            FinishMove(false, halt: true);
        else if (Time.time - _startedAt >= MinRunSeconds && !ai.pathPending && ai.reachedEndOfPath)
            FinishMove(distance <= _stopDistance + PartialPathSlack, halt: true);
    }

    // Ends the walk. A walk that did not arrive stops the agent where it is; one that arrived leaves it be.
    private void FinishMove(bool arrived, bool halt)
    {
        _moving = false;
        var callback = _done;
        _done = null;

        var ai = _npc != null && _npc.npcController != null ? _npc.npcController.ai : null;
        if (halt && ai != null)
        {
            ai.destination = _npc.transform.position;
            ai.SearchPath();
        }
        Call(callback, arrived);
    }

    /// <summary>
    /// Makes the NPC look at a transform with its eyes and head, and, with <paramref name="turnBody"/>,
    /// turn its body towards it (which pauses the activity). Stays on the transform until
    /// <see cref="StopLooking"/>. Returns false when nothing could be applied.
    /// </summary>
    public bool LookAt(Transform target, bool turnBody = true)
    {
        if (!IsValid || target == null)
            return false;
        StopLooking();

        var head = _npc.characterHeadController;
        if (head != null)
        {
            head.LookAtNPCTarget(target);
            _headLook = true;
        }
        if (turnBody && Pause())
        {
            _lookTarget = target;
            _lookAtPoint = false;
        }
        return _headLook || _lookTarget != null;
    }

    /// <summary>Turns the NPC's body towards a point (pauses the activity). Returns false when it cannot be paused.</summary>
    public bool LookAt(Vector3 point)
    {
        if (!IsValid)
            return false;
        StopLooking();
        if (!Pause())
            return false;
        _lookPoint = point;
        _lookAtPoint = true;
        return true;
    }

    /// <summary>Lets the NPC's eyes and body go back to what they were doing.</summary>
    public void StopLooking()
    {
        _lookTarget = null;
        _lookAtPoint = false;
        if (!_headLook)
            return;
        _headLook = false;
        var head = _npc != null ? _npc.characterHeadController : null;
        if (head == null)
            return;
        head.EnableLookAtPlayer();
        head.StopLookAtTarget();
    }

    private void TickLook()
    {
        if (_moving || !_paused || (_lookTarget == null && !_lookAtPoint))
            return;

        var from = _npc.transform;
        var at = _lookAtPoint ? _lookPoint : _lookTarget.position;
        var flat = new Vector3(at.x - from.position.x, 0f, at.z - from.position.z);
        if (flat.sqrMagnitude < 0.0001f || Vector3.Angle(from.forward, flat) < FacingToleranceDegrees)
            return;
        _npc.npcController.RotateNPC(at);
    }

    private static float FlatDistance(Vector3 a, Vector3 b)
    {
        float dx = a.x - b.x;
        float dz = a.z - b.z;
        return Mathf.Sqrt(dx * dx + dz * dz);
    }
}
