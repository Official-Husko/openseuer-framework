// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A snapshot of one body of water the player can swim in.</summary>
public sealed class WaterVolumeInfo
{
    internal WaterVolumeInfo(string id, string name, float density, float drag, float drownDamage, int swimHygiene, float maxDepth)
    {
        Id = id;
        Name = name;
        Density = density;
        Drag = drag;
        DrownDamage = drownDamage;
        SwimHygiene = swimHygiene;
        MaxDepth = maxDepth;
    }

    public string Id { get; }

    public string Name { get; }

    /// <summary>How strongly it pushes things up.</summary>
    public float Density { get; }

    /// <summary>How much it slows things down.</summary>
    public float Drag { get; }

    /// <summary>Damage per tick once the player is out of breath.</summary>
    public float DrownDamage { get; }

    /// <summary>The hygiene a swim sets the player to, or -1 when swimming leaves it alone (sewage sets it low).</summary>
    public int SwimHygiene { get; }

    public float MaxDepth { get; }
}

/// <summary>
/// Water: the player swimming and diving, their breath, the bodies of water in the scene (<c>Water</c>), and
/// the scene's water supply that taps and sinks draw from. Changes to a body of water last until the level
/// is loaded again. Changes are refused (false) while a save is written or loaded.
/// </summary>
public static class WorldWater
{
    /// <summary>How long the player can stay under before drowning, in the game's breath units.</summary>
    public const float MaxBreath = WaterController.maxBreathHoldingTime;

    /// <summary>The player went under (true) or came up (false). Not raised while a save loads.</summary>
    public static readonly WorldEvent<bool> UnderwaterChanged = new WorldEvent<bool>(nameof(UnderwaterChanged));

    /// <summary>True when the hooks are in place, so <see cref="UnderwaterChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    public static bool IsPlayerSwimming => WaterController.instance != null && WaterController.instance.PlayerIsSwimming;

    public static bool IsPlayerUnderwater => UnderwaterEffect.instance != null && UnderwaterEffect.instance.playerIsUnderWater;

    /// <summary>Breath used up on this dive, 0 to <see cref="MaxBreath"/>.</summary>
    public static float BreathUsed => WaterController.instance != null ? WaterController.instance.timeInWater : 0f;

    /// <summary>Gives the player a full breath. False without a water system.</summary>
    public static bool ResetBreath()
    {
        var controller = WaterController.instance;
        if (controller == null || SaveGate.IsBusy)
            return false;
        controller.timeInWater = 0f;
        return true;
    }

    /// <summary>Water in the scene's supply (the building's tank or a mains source), or 0 when the scene has none.</summary>
    public static float SupplyAmount
    {
        get
        {
            var source = Supply;
            return source != null ? source.LiquidAmount : 0f;
        }
    }

    /// <summary>The supply never runs dry.</summary>
    public static bool SupplyIsInfinite
    {
        get
        {
            var source = Supply;
            return source != null && source.HasInfiniteOutputCapasity();
        }
    }

    /// <summary>The scene's water supply tank, or null.</summary>
    public static LiquidStorage Supply => WaterSourceController.instance != null ? WaterSourceController.instance.globalStorage : null;

    /// <summary>Every active body of water. A scan.</summary>
    public static List<WaterVolumeInfo> Volumes()
    {
        var list = new List<WaterVolumeInfo>();
        foreach (var water in ObjectIds.Active<Water>())
            list.Add(Describe(water));
        return list;
    }

    public static Water Find(string id) => ObjectIds.Find<Water>(id);

    public static WaterVolumeInfo Describe(Water water) =>
        new WaterVolumeInfo(ObjectIds.IdOf(water), water.name, water.density, water.drag, water.drowndamage, water.setPlayerHygiene, water.maxDepth);

    /// <summary>Changes a body of water; parts left null stay as they are. Throws when there is none with that id.</summary>
    public static bool SetVolume(string id, float? density = null, float? drag = null, float? drownDamage = null, int? swimHygiene = null)
    {
        var water = Find(id);
        if (water == null)
            throw new ArgumentException($"No water '{id}'.", nameof(id));
        float? newDensity = density.HasValue ? NotNegative(density.Value, nameof(density)) : (float?)null;
        float? newDrag = drag.HasValue ? NotNegative(drag.Value, nameof(drag)) : (float?)null;
        float? newDamage = drownDamage.HasValue ? NotNegative(drownDamage.Value, nameof(drownDamage)) : (float?)null;
        if (SaveGate.IsBusy)
            return false;
        if (newDensity.HasValue)
            water.density = newDensity.Value;
        if (newDrag.HasValue)
            water.drag = newDrag.Value;
        if (newDamage.HasValue)
            water.drowndamage = newDamage.Value;
        if (swimHygiene.HasValue)
            water.setPlayerHygiene = Math.Max(-1, swimHygiene.Value);
        return true;
    }

    private static float NotNegative(float value, string name)
    {
        if (!(value >= 0f) || float.IsInfinity(value))
            throw new ArgumentOutOfRangeException(name, "Give a finite number, 0 or more.");
        return value;
    }
}
