// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>A place the player can work for pay, from <see cref="Jobs.All"/>.</summary>
public sealed class JobInfo
{
    internal JobInfo(InteractableWaitWork job)
    {
        Job = job;
        Address = ObjectAddress.Of(job);
        Title = string.IsNullOrEmpty(job.title) ? job.name : job.title;
        CoinsPerHour = job.SalaryPerHour.OC;
        RealMoneyPerHour = job.SalaryPerHour.RM;
        Skill = job.Skill;
        RequiredSkill = job.RequiredSkill;
        Hardness = job.JobHardness;
        ShiftMinutes = Jobs.ShiftMinutes(job);
    }

    public InteractableWaitWork Job { get; }

    /// <summary>A stable address; its text form works with <see cref="Jobs.Find"/>.</summary>
    public ObjectAddress Address { get; }

    public string Title { get; }

    /// <summary>Base pay per hour; skilled jobs pay up to twice this with good results.</summary>
    public int CoinsPerHour { get; }

    public float RealMoneyPerHour { get; }

    /// <summary>The skill the job trains and pays for, or None.</summary>
    public PlayerStats.PlayerSkillType Skill { get; }

    public int RequiredSkill { get; }

    /// <summary>How hard the job is for its skill (higher fails more often).</summary>
    public float Hardness { get; }

    /// <summary>How long one shift lasts in game minutes.</summary>
    public int ShiftMinutes { get; }
}

/// <summary>A finished shift, from <see cref="Jobs.Worked"/>.</summary>
public readonly struct WorkedShift
{
    public WorkedShift(InteractableWaitWork job, int seconds, int coins, float realMoney)
    {
        Job = job;
        Seconds = seconds;
        Coins = coins;
        RealMoney = realMoney;
    }

    public InteractableWaitWork Job { get; }

    /// <summary>The job's stable address, worked out when asked for.</summary>
    public ObjectAddress Address => ObjectAddress.Of(Job);

    /// <summary>Game seconds worked (a shift can be cut short).</summary>
    public int Seconds { get; }

    public float Hours => Seconds / 3600f;

    /// <summary>What the shift paid, as it reached the wallet.</summary>
    public int Coins { get; }

    public float RealMoney { get; }
}

/// <summary>
/// Paid work: the work spots of the loaded level (factory lines, odd jobs) and an event for every shift
/// worked, with what it paid. Working itself is the game's wait; see <see cref="PlayerSleep"/>.
/// </summary>
public static class Jobs
{
    private static readonly AccessTools.FieldRef<InteractableWait, int> Shift =
        AccessTools.FieldRefAccess<InteractableWait, int>("WaitInMinutes");

    /// <summary>A shift ended and was paid. Raised after the game paid it.</summary>
    public static readonly GameEvent<WorkedShift> Worked = new GameEvent<WorkedShift>(nameof(Worked));

    /// <summary>Every work spot in the loaded scenes. A scan, so call it on demand.</summary>
    public static List<JobInfo> All()
    {
        var all = new List<JobInfo>();
        foreach (var job in Object.FindObjectsByType<InteractableWaitWork>(FindObjectsSortMode.None))
            all.Add(new JobInfo(job));
        return all;
    }

    /// <summary>The work spot at an address, GUID or path, or null.</summary>
    public static InteractableWaitWork Find(string idOrAddress) => ObjectResolver.Find<InteractableWaitWork>(idOrAddress);

    public static JobInfo Describe(InteractableWaitWork job) => job != null ? new JobInfo(job) : null;

    internal static int ShiftMinutes(InteractableWaitWork job) => Shift(job);
}
