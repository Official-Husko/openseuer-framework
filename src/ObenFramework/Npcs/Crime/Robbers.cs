// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using NPC;
using ObenFramework.World;
using PixelCrushers.DialogueSystem;

namespace ObenFramework.Npcs;

/// <summary>A robber and what it is up to, copied when read.</summary>
public sealed class RobberRecord
{
    /// <summary>The robber's character id.</summary>
    public int NpcId { get; }

    /// <summary>Idle, Stand, Patrol, Wander, Chase or Aggro.</summary>
    public string State { get; }

    /// <summary>The robber is in the scene you are in.</summary>
    public bool Loaded { get; }

    internal RobberRecord(int npcId, Act_Robber robber, bool loaded)
    {
        NpcId = npcId;
        State = robber.state.ToString();
        Loaded = loaded;
    }
}

/// <summary>
/// The robbers that roam some scenes and hold the player up: who they are, what they do, and a nudge in a
/// direction (go for the player, rob now, give in to haggling, keep to themselves).
/// </summary>
public static class Robbers
{
    private static readonly AccessTools.FieldRef<Act_Robber, Money.money> MinimumMoney = AccessTools.FieldRefAccess<Act_Robber, Money.money>("minimumMoney");

    /// <summary>A robber took the player's money. The payload is its character id. Each handler runs in its own try/catch.</summary>
    public static readonly WorldEvent<int> Robbed = new WorldEvent<int>(nameof(Robbed));

    /// <summary>Every NPC whose activity is robbing, in the scene and off it.</summary>
    public static List<RobberRecord> All()
    {
        var result = new List<RobberRecord>();
        var database = NPCDirector.globalDatabase;
        if (database == null)
            return result;
        foreach (var data in database)
        {
            if (data != null && data.state != null && data.state.CurrentActivity is Act_Robber robber)
                result.Add(new RobberRecord(data.ID, robber, Npcs.LiveObject(data.ID) != null));
        }
        return result;
    }

    /// <summary>The robber's state (see <see cref="RobberRecord.State"/>), or null when the NPC is not robbing.</summary>
    public static string State(int npcId)
    {
        var data = Npcs.DataOf(npcId);
        return data != null && data.state != null && data.state.CurrentActivity is Act_Robber robber ? robber.state.ToString() : null;
    }

    /// <summary>The robber goes for the player.</summary>
    public static bool Aggro(int npcId)
    {
        var robber = Live(npcId);
        return robber != null && Run("Aggro", robber.Aggro) && robber.state == Act_Robber.State.Aggro;
    }

    /// <summary>The robber robs the player now. Refused when the player carries less than the robber wants.</summary>
    public static bool Rob(int npcId)
    {
        var robber = Live(npcId);
        if (robber == null || Money.instance == null || !Money.instance.CheckMoney(MinimumMoney(robber)))
            return false;
        return Run("Rob", robber.RobPlayer);
    }

    /// <summary>Tries the game's haggling: true when the robber gives in.</summary>
    public static bool Negotiate(int npcId)
    {
        var robber = Live(npcId);
        bool gaveIn = false;
        return robber != null && Run("Negotiate", () => gaveIn = robber.TryNegotiate()) && gaveIn;
    }

    /// <summary>Makes the robber keep to itself (aloof) or approach the player again.</summary>
    public static bool SetAloof(int npcId, bool aloof)
    {
        var robber = Live(npcId);
        var npc = robber != null ? Npcs.LiveObject(npcId) : null;
        if (npc == null || npc.GetComponent<ConversationTrigger>() == null)
            return false;
        return Run("SetAloof", () => robber.ChangeRobberAloofStatus(aloof));
    }

    internal static void RaiseRobbed(Act_Robber robber)
    {
        var npc = robber.GetNPCReference();
        if (npc != null && npc.ID > 0)
            Robbed.Raise(npc.ID);
    }

    private static Act_Robber Live(int npcId)
    {
        var npc = NpcActions.Steerable(npcId);
        var state = npc != null && npc.npcController.Data != null ? npc.npcController.Data.state : null;
        return state != null ? state.CurrentActivity as Act_Robber : null;
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
            NpcsLog.Failed("Robbers." + what, e);
            return false;
        }
    }
}
