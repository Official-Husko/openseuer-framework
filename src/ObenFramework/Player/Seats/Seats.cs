// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>A chair, sofa, bench, toilet or exam bed, from <see cref="Seats.All"/>.</summary>
public sealed class SeatInfo
{
    internal SeatInfo(InteractableChair chair)
    {
        Chair = chair;
        Address = ObjectAddress.Of(chair);
        Title = string.IsNullOrEmpty(chair.title) ? chair.name : chair.title;
        Position = chair.transform.position;
        ByPlayer = chair.PlayerSittingHere;
        Occupied = ByPlayer || chair.Occupied();
        IsToilet = chair is Toilet;
    }

    public InteractableChair Chair { get; }

    /// <summary>A stable address; its text form works with <see cref="Seats.Find"/>.</summary>
    public ObjectAddress Address { get; }

    public string Title { get; }
    public Vector3 Position { get; }

    /// <summary>Someone (the player or an NPC) sits there.</summary>
    public bool Occupied { get; }

    public bool ByPlayer { get; }
    public bool IsToilet { get; }
}

/// <summary>Someone sat down or stood up, from <see cref="Seats.Sat"/> and <see cref="Seats.Left"/>.</summary>
public readonly struct SeatChange
{
    public SeatChange(InteractableChair chair, GameObject npc)
    {
        Chair = chair;
        Npc = npc;
    }

    public InteractableChair Chair { get; }

    /// <summary>The seat's stable address, worked out when asked for (it walks the hierarchy).</summary>
    public ObjectAddress Address => ObjectAddress.Of(Chair);

    /// <summary>The NPC who sat or stood, or null when it was the player.</summary>
    public GameObject Npc { get; }

    public bool ByPlayer => Npc == null;
}

/// <summary>
/// Seats: list them, find one by address, sit the player down or stand them up the way the interact key
/// does, and hear when the player or an NPC sits or stands. NPCs placed on a seat while a level loads are
/// not reported.
/// </summary>
public static class Seats
{
    private static readonly AccessTools.FieldRef<InteractableChair, bool> StopSitDisabled =
        AccessTools.FieldRefAccess<InteractableChair, bool>("disableStopSit");
    private static readonly AccessTools.FieldRef<InteractableChair, Coroutine> Moving =
        AccessTools.FieldRefAccess<InteractableChair, Coroutine>("currentCoroutine");

    public static readonly GameEvent<SeatChange> Sat = new GameEvent<SeatChange>(nameof(Sat));
    public static readonly GameEvent<SeatChange> Left = new GameEvent<SeatChange>(nameof(Left));

    /// <summary>The seat the player sits on, or null. Searches the level, so not for per-frame use.</summary>
    public static InteractableChair Current
    {
        get
        {
            if (!InteractableChair.PlayerIsSitting)
                return null;
            // Sitting puts the player under the chair, so the chair is one of the body's parents.
            var body = PlayerRef.Body;
            var chair = body != null ? body.GetComponentInParent<InteractableChair>() : null;
            if (chair != null && chair.PlayerSittingHere)
                return chair;
            foreach (var candidate in Scan())
            {
                if (candidate.PlayerSittingHere)
                    return candidate;
            }
            return null;
        }
    }

    /// <summary>Every seat in the loaded scenes. A scan, so call it on demand.</summary>
    public static List<SeatInfo> All()
    {
        var all = new List<SeatInfo>();
        foreach (var chair in Scan())
            all.Add(new SeatInfo(chair));
        return all;
    }

    public static SeatInfo Describe(InteractableChair chair) =>
        chair != null ? new SeatInfo(chair) : throw new ArgumentNullException(nameof(chair));

    /// <summary>The seat at an address, GUID or path (see <see cref="ObjectResolver.Find{T}"/>), or null.</summary>
    public static InteractableChair Find(string idOrAddress) => ObjectResolver.Find<InteractableChair>(idOrAddress);

    /// <summary>
    /// Sits the player down like pressing the interact key on the seat. False when the player already sits,
    /// can't move, the seat is taken or switched off, or it is farther than <paramref name="maxDistance"/>
    /// metres from the camera (pass <c>float.PositiveInfinity</c> to sit from anywhere).
    /// </summary>
    public static bool Sit(InteractableChair chair, float maxDistance = 2f)
    {
        if (chair == null)
            throw new ArgumentNullException(nameof(chair));
        if (InteractableChair.PlayerIsSitting || !CanAct())
            return false;
        var controller = OpenSewerCharacterController.instance;
        if (controller == null || controller.ControlsDisabled())
            return false;
        if (chair.IsInteractDisabled() || chair.Occupied() || Moving(chair) != null)
            return false;
        var camera = PlayerRef.CameraTransform;
        if (camera != null && Vector3.Distance(camera.position, chair.transform.position) > maxDistance)
            return false;

        chair.Interact();
        return chair.PlayerSittingHere;
    }

    /// <summary>Stands the player up like the interact key. False when not seated or the seat won't let go now.</summary>
    public static bool Leave()
    {
        var chair = Current;
        if (chair == null || !CanAct())
            return false;
        if (chair is Toilet toilet && !toilet.enabled)
            return false;
        if (StopSitDisabled(chair) || Moving(chair) != null)
            return false;
        chair.Interact();
        return true;
    }

    private static bool CanAct()
    {
        var game = GameController.instance;
        return game != null && !game.keysDisabled && !game.GameIsPaused && InteractObjects.instance != null &&
               ThirdPersonCameraController.instance != null && !SaveGate.IsBusy && !PlayerSleep.IsResting;
    }

    private static InteractableChair[] Scan() =>
        UnityEngine.Object.FindObjectsByType<InteractableChair>(FindObjectsSortMode.None);
}
