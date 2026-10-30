// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using UnityEngine;

namespace ObenFramework.Items;

/// <summary>A snapshot of an extractable: something worked loose in several goes (a pipe, a fitting).</summary>
public readonly struct ExtractableInfo
{
    public ExtractableInfo(string id, string name, Vector3 position, int health)
    {
        Id = id;
        Name = name;
        Position = position;
        Health = health;
    }

    public string Id { get; }

    public string Name { get; }

    public Vector3 Position { get; }

    /// <summary>What is left before it comes loose, out of 100.</summary>
    public int Health { get; }
}

/// <summary>Extractables in the loaded scenes: listing them, working on them, and hearing when one comes loose.</summary>
public static class Extractables
{
    /// <summary>
    /// The framework's item hooks are in place. False when they failed to apply (a game update changed what they
    /// patch); the events then stay quiet and changes the game should pick up may not take.
    /// </summary>
    public static bool IsAvailable => ItemsModule.HooksInPlace;

    /// <summary>An extractable came loose. The payload is its id.</summary>
    public static readonly WorldEvent<string> Extracted = new WorldEvent<string>(nameof(Extracted));

    public static List<ExtractableInfo> All()
    {
        var list = new List<ExtractableInfo>();
        foreach (var extractable in ObjectIds.Active<Extractable>())
            list.Add(new ExtractableInfo(ObjectIds.IdOf(extractable), extractable.name, extractable.transform.position, extractable.Health));
        return list;
    }

    public static Extractable Find(string id) => ObjectIds.Find<Extractable>(id);

    /// <summary>Works on an extractable once, as the player's action does. True when its health went down.</summary>
    public static bool Work(string id)
    {
        var extractable = Find(id);
        if (extractable == null || extractable.Health < 1 || SaveGate.IsBusy)
            return false;
        int before = extractable.Health;
        using (WorldChanges.EnterMod())
            extractable.Damage();
        return extractable.Health < before;
    }
}

[HarmonyPatch]
internal static class ExtractablesPatches
{
    [HarmonyPrefix]
    [HarmonyPatch(typeof(Extractable), nameof(Extractable.Damage))]
    private static void Working(Extractable __instance, out bool __state) =>
        __state = Extractables.Extracted.HasSubscribers && __instance.Health >= 1;

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Extractable), nameof(Extractable.Damage))]
    private static void Worked(Extractable __instance, bool __state)
    {
        if (!__state || __instance.Health >= 1 || !WorldChanges.Active)
            return;
        try { Extractables.Extracted.Raise(ObjectIds.IdOf(__instance)); }
        catch (Exception e) { ItemsLog.Failed("Extractable hook", e); }
    }
}
