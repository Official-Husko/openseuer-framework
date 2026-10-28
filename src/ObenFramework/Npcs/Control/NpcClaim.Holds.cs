// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using HarmonyLib;
using NPC;

namespace ObenFramework.Npcs;

public sealed partial class NpcClaim
{
    private const float FarAway = 1000000f;

    private static readonly AccessTools.FieldRef<FadeGameObject, bool> Culled = AccessTools.FieldRefAccess<FadeGameObject, bool>("culled");

    private NpcHolds _holds;
    private FadeGameObject _fade;
    private float _hideDistance;
    private bool _couldMove;
    private bool _gameWantsMove;

    /// <summary>The parts of the NPC's own behaviour this claim has switched off.</summary>
    public NpcHolds Holds => _holds;

    /// <summary>
    /// Switches parts of the NPC's own behaviour off for as long as the claim lasts: what it sees, being
    /// talked to, its barks, being hidden at a distance, moving at all (see <see cref="NpcHolds"/>). The
    /// value replaces the previous one, so <c>Hold(NpcHolds.None)</c> gives everything back. Holding
    /// <see cref="NpcHolds.Freeze"/> pauses the NPC and stops walks started with <see cref="MoveTo"/>.
    /// Returns false when the claim is no longer valid, or Freeze was asked for and the NPC cannot be paused.
    /// </summary>
    public bool Hold(NpcHolds holds)
    {
        if (!IsValid)
            return false;
        holds &= NpcHolds.All;
        if ((holds & NpcHolds.Freeze) != 0 && (_holds & NpcHolds.Freeze) == 0 && !Pause())
            return false;

        var removed = _holds & ~holds;
        var added = holds & ~_holds;
        if ((removed & NpcHolds.Visible) != 0)
            LetHide();
        if ((removed & NpcHolds.Freeze) != 0)
            Unfreeze();

        _holds = holds;
        if ((added & NpcHolds.Visible) != 0)
            KeepShown();
        if ((added & NpcHolds.Freeze) != 0)
            Freeze();
        NpcControl.CountHolds();
        return true;
    }

    private void ReleaseHolds()
    {
        if (_holds == NpcHolds.None)
            return;
        Hold(NpcHolds.None);
    }

    private void KeepShown()
    {
        _fade = _npc.GetComponent<FadeGameObject>();
        if (_fade == null)
            return;
        _hideDistance = _fade.hideDistance;
        _fade.hideDistance = FarAway;
        if (Culled(_fade))
        {
            Culled(_fade) = false;
            _fade.Show();
        }
    }

    private void LetHide()
    {
        // Only put the distance back if nobody changed it since.
        if (_fade != null && _fade.hideDistance == FarAway)
            _fade.hideDistance = _hideDistance;
        _fade = null;
    }

    private void Freeze()
    {
        var ai = _npc.npcController.richAI;
        if (ai == null)
            return;
        CancelMove();
        _couldMove = ai.canMove;
        _gameWantsMove = false;
        ai.canMove = false;
    }

    // The game turns movement back on in places (a talk ending, an activity starting); keep it off.
    private void TickHolds()
    {
        if ((_holds & NpcHolds.Freeze) == 0)
            return;
        var ai = _npc.npcController.richAI;
        if (ai == null || !ai.canMove)
            return;
        _gameWantsMove = true;
        ai.canMove = false;
    }

    private void Unfreeze()
    {
        var controller = _npc != null ? _npc.npcController : null;
        var ai = controller != null ? controller.richAI : null;
        if (ai == null || ai.canMove)
            return;
        // A sleeping or seated NPC is held still by the game itself; leave it so.
        if (_npc.Sleeping() || controller.GetComponentInParent<InteractableChair>() != null)
            return;
        ai.canMove = _couldMove || _gameWantsMove;
    }
}
