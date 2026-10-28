using System;
using ObenFramework.Events;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// One mod's hold on one NPC (see <see cref="NpcControl.Claim"/>). Pausing the NPC's activity, walking it
/// somewhere and making it look at something are all undone by <see cref="Dispose"/>, which the owning mod's
/// unload also calls. The claim stays valid while the NPC is in the scene; <see cref="IsValid"/> says so.
/// </summary>
public sealed partial class NpcClaim : IDisposable
{
    private readonly NPCInfo _npc;
    private bool _released;
    private bool _paused;
    private bool _restoreEnabled;

    internal NpcClaim(string owner, int npcId, NPCInfo npc)
    {
        Owner = owner;
        NpcId = npcId;
        _npc = npc;
    }

    /// <summary>Id of the mod that holds the NPC.</summary>
    public string Owner { get; }

    /// <summary>The NPC's character id.</summary>
    public int NpcId { get; }

    /// <summary>The NPC's object in the scene, or null once it is gone.</summary>
    public NPCInfo Npc => _npc != null ? _npc : null;

    /// <summary>The claim is still held and the NPC still exists.</summary>
    public bool IsValid => !_released && _npc != null && _npc.npcController != null;

    /// <summary>The NPC's schedule is paused by this claim (or was already paused by someone else).</summary>
    public bool Paused => _paused;

    /// <summary>
    /// Stops the NPC's current activity and keeps its schedule from starting another until
    /// <see cref="Resume"/>. Refused (false) when the activity cannot be interrupted, such as a police
    /// chase. Moving and turning the body pause the NPC themselves.
    /// </summary>
    public bool Pause()
    {
        if (!IsValid)
            return false;
        if (_paused)
            return true;

        var controller = _npc.npcController;
        var state = controller.Data != null ? controller.Data.state : null;
        if (state == null)
            return false;

        if (controller.activityDisabled)
        {
            // Someone else already paused it; leave it that way when we let go.
            _paused = true;
            _restoreEnabled = false;
            return true;
        }

        var activity = state.CurrentActivity;
        if (activity != null && activity.running && (activity.force || !activity.CanBeInterrupted()))
            return false;

        controller.DisableActivity();
        _paused = true;
        _restoreEnabled = true;
        return true;
    }

    /// <summary>
    /// Lets the schedule run again: any walk this claim started is cancelled, and the NPC picks up its
    /// timetable within a game minute. Looking is a separate thing, see <see cref="StopLooking"/>.
    /// </summary>
    public void Resume()
    {
        if (!_paused)
            return;
        CancelMove();
        _paused = false;

        var controller = _npc != null ? _npc.npcController : null;
        if (!_restoreEnabled || controller == null)
            return;
        controller.EnableActivity();
        controller.Data?.scheduler?.ResetNextCheck();
    }

    /// <summary>
    /// Gives the NPC back: stops an animation, puts its appearance back, lifts the holds, stops looking,
    /// cancels a walk and resumes the schedule.
    /// </summary>
    public void Dispose()
    {
        if (_released)
            return;
        StopAnimation(0f);
        RestoreAppearance();
        ReleaseHolds();
        StopLooking();
        Resume();
        _released = true;
        NpcControl.Remove(this);
    }

    // The NPC died or its object is being destroyed: no walking or schedule to give back, but a dead body
    // keeps lying there, so its looks and fade distance are put back.
    internal void Abandon()
    {
        if (_released)
            return;
        _released = true;
        RestoreAppearance();
        LetHide();
        _holds = NpcHolds.None;
        StopAnimation(0f);
        FinishMove(false, halt: false);
        NpcControl.Remove(this);
    }

    internal void Tick()
    {
        TickMove();
        TickLook();
        TickHolds();
    }

    private static void Call(Action<bool> callback, bool result)
    {
        if (callback == null)
            return;
        try { callback(result); }
        catch (Exception e) { GameEvents.Log?.Error("An NPC move callback threw", e); }
    }
}
