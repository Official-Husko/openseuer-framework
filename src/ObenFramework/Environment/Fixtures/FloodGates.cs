// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Items;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A flood gate that started opening or closing, as raised by <see cref="FloodGates.GateChanged"/>.</summary>
public readonly struct FloodGateChange
{
    public FloodGateChange(FloodGateControl gate, bool opening, ChangeSource source)
    {
        Gate = gate;
        Opening = opening;
        Source = source;
    }

    public FloodGateControl Gate { get; }

    /// <summary>True when it started opening, false when it started closing.</summary>
    public bool Opening { get; }

    public ChangeSource Source { get; }

    public string Id => FloodGates.IdOf(Gate);
}

/// <summary>A snapshot of one flood gate.</summary>
public sealed class FloodGateInfo
{
    internal FloodGateInfo(string id, string name, bool open, bool moving, bool powered)
    {
        Id = id;
        Name = name;
        Open = open;
        Moving = moving;
        Powered = powered;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>Open or opening.</summary>
    public bool Open { get; }

    /// <summary>Still opening or closing (its lights blink).</summary>
    public bool Moving { get; }

    /// <summary>Its control has power; without it the gate does not move.</summary>
    public bool Powered { get; }
}

/// <summary>Flood gate controls: open and close them, cut their power. Their state is saved with the game.</summary>
public static class FloodGates
{
    private static readonly AccessTools.FieldRef<FloodGateControl, bool> OpenField = AccessTools.FieldRefAccess<FloodGateControl, bool>("open");
    private static readonly AccessTools.FieldRef<FloodGateControl, bool> PowerOff = AccessTools.FieldRefAccess<FloodGateControl, bool>("powerOff");
    private static readonly AccessTools.FieldRef<FloodGateControl, bool> Blinking = AccessTools.FieldRefAccess<FloodGateControl, bool>("isBlinking");

    /// <summary>A gate started opening or closing. Not raised while a save loads.</summary>
    public static readonly WorldEvent<FloodGateChange> GateChanged = new WorldEvent<FloodGateChange>(nameof(GateChanged));

    public static List<FloodGateInfo> All()
    {
        var list = new List<FloodGateInfo>();
        foreach (var gate in ObjectIds.Active<FloodGateControl>())
            list.Add(Describe(gate));
        return list;
    }

    public static FloodGateControl Find(string id) => ObjectIds.Find<FloodGateControl>(id);

    public static string IdOf(FloodGateControl gate) => ObjectIds.IdOf(gate);

    public static bool IsOpen(FloodGateControl gate) => OpenField(gate);

    public static bool IsPowered(FloodGateControl gate) => !PowerOff(gate);

    public static FloodGateInfo Describe(FloodGateControl gate) =>
        new FloodGateInfo(IdOf(gate), gate.name, IsOpen(gate), Blinking(gate), IsPowered(gate));

    /// <summary>
    /// Starts opening or closing the gate, like its buttons. Returns false when it refused: no power, an alarm,
    /// or it is still moving. True when it already was that way.
    /// </summary>
    public static bool SetOpen(FloodGateControl gate, bool open)
    {
        if (gate == null)
            throw new ArgumentNullException(nameof(gate));
        if (IsOpen(gate) == open)
            return true;
        using (WorldChanges.EnterMod())
        {
            if (open)
                gate.OpenFloodGate();
            else
                gate.CloseFloodGate();
        }
        return IsOpen(gate) == open;
    }

    /// <summary>Gives the gate's control power or cuts it.</summary>
    public static void SetPowered(FloodGateControl gate, bool powered)
    {
        if (gate == null)
            throw new ArgumentNullException(nameof(gate));
        if (IsPowered(gate) == powered)
            return;
        using (WorldChanges.EnterMod())
        {
            if (powered)
                gate.TurnOn();
            else
                gate.TurnOff();
        }
    }
}
