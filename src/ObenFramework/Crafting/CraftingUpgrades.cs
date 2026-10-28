// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Crafting;

/// <summary>A snapshot of a station upgrade (an add-on that gives connected stations recipes, conditions or equipment quality).</summary>
public sealed class UpgradeInfo
{
    internal UpgradeInfo(CraftingUpgrade upgrade)
    {
        Upgrade = upgrade;
        Id = ObjectIds.IdOf(upgrade);
        Name = upgrade.name;
        On = upgrade.Active;
        Passive = upgrade.Passive;
        Position = upgrade.transform.position;
        EquipmentQuality = upgrade.AddEquipmentQuality;
        Recipes = CraftingStations.Names(upgrade.AdditionalRecipes);
        Modifiers = CraftingStations.Names(upgrade.AddModifiers);
        Connected = new List<string>();
        foreach (var station in upgrade.Connected)
        {
            if (station != null)
                Connected.Add(CraftingStations.IdOf(station));
        }
    }

    public CraftingUpgrade Upgrade { get; }

    public string Id { get; }

    public string Name { get; }

    /// <summary>Switched on. A switched-off upgrade gives its stations nothing.</summary>
    public bool On { get; }

    /// <summary>Works without being switched on (it has no switch).</summary>
    public bool Passive { get; }

    public Vector3 Position { get; }

    /// <summary>Equipment quality it adds to each connected station.</summary>
    public float EquipmentQuality { get; }

    /// <summary>Recipe types it adds.</summary>
    public List<string> Recipes { get; }

    /// <summary>Conditions it adds.</summary>
    public List<string> Modifiers { get; }

    /// <summary>Ids of the stations it is connected to.</summary>
    public List<string> Connected { get; }
}

/// <summary>Station upgrades in the loaded scenes: listing them and switching them on or off.</summary>
public static class CraftingUpgrades
{
    /// <summary>Every active upgrade in the loaded scenes. A scan.</summary>
    public static List<UpgradeInfo> All()
    {
        var list = new List<UpgradeInfo>();
        foreach (var upgrade in ObjectIds.Active<CraftingUpgrade>())
            list.Add(new UpgradeInfo(upgrade));
        return list;
    }

    public static CraftingUpgrade Find(string id) => ObjectIds.Find<CraftingUpgrade>(id);

    public static UpgradeInfo Get(string id)
    {
        var upgrade = Find(id);
        return upgrade != null ? new UpgradeInfo(upgrade) : null;
    }

    /// <summary>The upgrades connected to a station.</summary>
    public static List<UpgradeInfo> Of(CraftingBase station)
    {
        var list = new List<UpgradeInfo>();
        if (station == null || station.upgrades == null)
            return list;
        foreach (var upgrade in station.upgrades)
        {
            if (upgrade != null)
                list.Add(new UpgradeInfo(upgrade));
        }
        return list;
    }

    /// <summary>Switches an upgrade on or off as its own switch does. Refuses while a save loads or a station window is open.</summary>
    public static bool SetOn(string id, bool on)
    {
        var upgrade = Find(id);
        if (upgrade == null || CraftingStations.Refused())
            return false;
        try
        {
            using (WorldChanges.EnterMod())
                upgrade.Toggle(on);
            return upgrade.Active == on;
        }
        catch (Exception e)
        {
            ItemsLog.Failed("CraftingUpgrades.SetOn", e);
            return false;
        }
    }
}
