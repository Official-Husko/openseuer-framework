// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.Scenes;
using UnityEngine;

namespace ObenFramework.World.Logic;

/// <summary>
/// The state the level designers keep in a level's logic objects: branches (true/false switches),
/// counters, named-state machines, sequences and timers. Reading lists them for the active scene with
/// ids from <see cref="SceneIds"/>; writing goes through the game's own methods, so whatever the level
/// hangs off them (relays, outputs) reacts as if the game had done it, and the game saves the result.
/// Writes are refused while the game saves or loads and return false when the id matches nothing.
/// </summary>
public static class SceneState
{
    private static readonly AccessTools.FieldRef<CounterRelay, int> CounterValue =
        AccessTools.FieldRefAccess<CounterRelay, int>("currentValue");
    private static readonly AccessTools.FieldRef<CounterRelay, int> CounterMax =
        AccessTools.FieldRefAccess<CounterRelay, int>("maxValue");
    private static readonly AccessTools.FieldRef<logic_state, string> StateCurrent =
        AccessTools.FieldRefAccess<logic_state, string>("currentState");
    private static readonly AccessTools.FieldRef<logic_state, List<logic_state.State>> StateList =
        AccessTools.FieldRefAccess<logic_state, List<logic_state.State>>("states");

    internal static readonly OnceLog Log = new OnceLog("Scene state");

    public static List<BranchInfo> Branches()
    {
        var list = new List<BranchInfo>();
        foreach (var branch in SceneIds.Active<logic_branch>())
            list.Add(new BranchInfo(SceneIds.Of(branch), branch.IsTrue));
        return list;
    }

    /// <summary>Sets a branch. The game tests it right away unless the branch is set to manual testing.</summary>
    public static bool SetBranch(string id, bool value) =>
        Write<logic_branch>("SetBranch", id, branch =>
        {
            branch.IsTrue = value;
            return branch.IsTrue == value;
        });

    public static List<CounterInfo> Counters()
    {
        var list = new List<CounterInfo>();
        foreach (var counter in SceneIds.Active<CounterRelay>())
            list.Add(new CounterInfo(SceneIds.Of(counter), CounterValue(counter), CounterMax(counter)));
        return list;
    }

    /// <summary>Sets a counter. Reaching the maximum fires the counter's relay, as in the game.</summary>
    public static bool SetCounter(string id, int value)
    {
        if (!SceneStateRules.CounterValue(value))
            return Refuse("SetCounter value", "a counter value must be 0 or more");
        return Write<CounterRelay>("SetCounter", id, counter =>
        {
            counter.SetCounterValue(value);
            return true;
        });
    }

    /// <summary>Adds to a counter, firing its relays like the game's own increase.</summary>
    public static bool AddCounter(string id, int amount)
    {
        if (!SceneStateRules.CounterStep(amount))
            return Refuse("AddCounter amount", "a counter only goes up, by 1 or more");
        return Write<CounterRelay>("AddCounter", id, counter =>
        {
            counter.IncreaseCounter(amount);
            return true;
        });
    }

    public static List<StateMachineInfo> States()
    {
        var list = new List<StateMachineInfo>();
        foreach (var machine in SceneIds.Active<logic_state>())
            list.Add(new StateMachineInfo(SceneIds.Of(machine), StateCurrent(machine) ?? "", Names(machine)));
        return list;
    }

    /// <summary>
    /// Changes a state machine to one of its own states. With <paramref name="trigger"/> the new state's
    /// relay fires too (the game's <c>ChangeAndTriggerState</c>); without, only the saved state changes.
    /// </summary>
    public static bool SetState(string id, string stateName, bool trigger = false) =>
        Write<logic_state>("SetState", id, machine =>
        {
            if (!SceneStateRules.StateName(Names(machine), stateName))
                return Refuse("SetState name", $"'{stateName}' is not one of the machine's states");
            if (trigger)
                machine.ChangeAndTriggerState(stateName);
            else
                machine.ChangeState(stateName);
            return StateCurrent(machine) == stateName;
        });

    public static List<SequenceInfo> Sequences()
    {
        var list = new List<SequenceInfo>();
        foreach (var sequence in SceneIds.Active<Logic_Sequence>())
            list.Add(new SequenceInfo(SceneIds.Of(sequence), sequence.sequenceEntries != null ? sequence.sequenceEntries.Count : 0, sequence.LoopSequence));
        return list;
    }

    public static bool StartSequence(string id) =>
        Write<Logic_Sequence>("StartSequence", id, sequence =>
        {
            sequence.TriggerSequence();
            return true;
        });

    public static bool StopSequence(string id) =>
        Write<Logic_Sequence>("StopSequence", id, sequence =>
        {
            sequence.StopSequence();
            return true;
        });

    public static List<SceneTimerInfo> Timers()
    {
        var list = new List<SceneTimerInfo>();
        foreach (var timer in SceneIds.Active<logic_timer>())
            list.Add(new SceneTimerInfo(SceneIds.Of(timer), timer.interval, timer.timerActive, timer.timerDisabled));
        return list;
    }

    public static bool StartTimer(string id) =>
        Write<logic_timer>("StartTimer", id, timer =>
        {
            timer.StartTimer();
            return true;
        });

    public static bool StopTimer(string id) =>
        Write<logic_timer>("StopTimer", id, timer =>
        {
            timer.StopTimer();
            return true;
        });

    /// <summary>Changes a timer's interval; the running wait keeps its old length.</summary>
    public static bool SetInterval(string id, float seconds)
    {
        if (!SceneStateRules.Interval(seconds))
            return Refuse("SetInterval value", $"an interval must be a number of seconds above 0 and at most {SceneStateRules.MaxInterval}");
        return Write<logic_timer>("SetInterval", id, timer =>
        {
            timer.ChangeInterval(seconds);
            return Mathf.Approximately(timer.interval, seconds);
        });
    }

    internal static List<string> Names(logic_state machine)
    {
        var names = new List<string>();
        var states = StateList(machine);
        if (states == null)
            return names;
        foreach (var state in states)
        {
            if (state != null)
                names.Add(state.stateName);
        }
        return names;
    }

    private static bool Write<T>(string call, string id, Func<T, bool> change) where T : Component
    {
        if (SaveGate.IsBusy)
            return Refuse(call + " busy", "the game is saving or loading");
        try
        {
            var target = SceneIds.Find<T>(id);
            return target != null && change(target);
        }
        catch (Exception e)
        {
            Log.Error(call, e);
            return false;
        }
    }

    private static bool Refuse(string where, string why)
    {
        Log.Warn(where, why);
        return false;
    }
}
