// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using NPC;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenLoader;

namespace ObenFramework.Npcs;

/// <summary>
/// Police pursuits of the player: whether one is on, which officers are on duty in the scene, calling them,
/// calling them all off, and hearing when a chase starts or ends.
/// </summary>
public static class Pursuit
{
    private static readonly AccessTools.FieldRef<Crime, List<Act_Police>> ActivePolices = AccessTools.FieldRefAccess<Crime, List<Act_Police>>("activePolices");
    private static readonly Action<Crime> StopPolicePursuits = AccessTools.MethodDelegate<Action<Crime>>(AccessTools.Method(typeof(Crime), "StopPolicePursuits"));

    private static bool _pursued;

    /// <summary>
    /// A chase started (true) or ended (false): an officer began or stopped chasing the player, the player
    /// was arrested, or the chases were called off. Each handler runs in its own try/catch.
    /// </summary>
    public static readonly WorldEvent<bool> Changed = new WorldEvent<bool>(nameof(Changed));

    /// <summary>An officer in the scene is chasing the player right now.</summary>
    public static bool IsPursued
    {
        get
        {
            var police = Police();
            if (police == null)
                return false;
            foreach (var officer in police)
            {
                if (officer != null && officer.state == Act_Police.State.ChasePlayer)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Character ids of the officers on duty in the scene (the ones a call reaches).</summary>
    public static List<int> ActivePolice()
    {
        var ids = new List<int>();
        var police = Police();
        if (police == null)
            return ids;
        foreach (var officer in police)
        {
            var npc = officer != null ? officer.GetNPCReference() : null;
            if (npc != null && npc.ID > 0 && !ids.Contains(npc.ID))
                ids.Add(npc.ID);
        }
        return ids;
    }

    /// <summary>
    /// Calls the police on the player the way a witness does: <paramref name="severe"/> starts a chase,
    /// otherwise officers come to question. Only officers within 15 m answer unless <paramref name="anyDistance"/>.
    /// False when there are no officers, an arrest or level change is under way, or a save or load runs.
    /// </summary>
    public static bool CallPolice(bool severe, bool anyDistance = false)
    {
        var crime = Crime.instance;
        var player = PlayerLocator.instance;
        var police = Police();
        if (SaveGate.IsBusy || crime == null || player == null || police == null || police.Count == 0 || crime.ArrestInProgress || Changelevel.ChangeLevelActive)
            return false;
        try
        {
            crime.CallPolice(player.transform, severe, anyDistance);
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Pursuit.CallPolice", e);
            return false;
        }
        Refresh();
        return true;
    }

    /// <summary>Calls off every chase, in the scene and off it, the way the game does after an arrest. False outside a level.</summary>
    public static bool StopAll()
    {
        var crime = Crime.instance;
        if (SaveGate.IsBusy || crime == null || NPCDirector.globalDatabase == null)
            return false;
        try
        {
            StopPolicePursuits(crime);
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Pursuit.StopAll", e);
            return false;
        }
        Refresh();
        return true;
    }

    internal static void Init(ObenMod framework)
    {
        _pursued = false;
        GameEvents.Loaded.Subscribe(framework, Refresh);
        GameEvents.SceneLoaded.Subscribe(framework, _ => Refresh());
    }

    // Called by the police hooks; works out whether the chase state changed.
    internal static void Refresh()
    {
        if (!Changed.HasSubscribers)
        {
            _pursued = false;
            return;
        }
        bool now = IsPursued;
        if (now == _pursued)
            return;
        _pursued = now;
        Changed.Raise(now);
    }

    private static List<Act_Police> Police()
    {
        var crime = Crime.instance;
        return crime != null ? ActivePolices(crime) : null;
    }
}
