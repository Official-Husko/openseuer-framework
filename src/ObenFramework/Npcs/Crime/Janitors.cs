// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>What kind of janitor action ran.</summary>
public enum JanitorKind
{
    /// <summary>A storage was filled with (or emptied of) an item or a liquid.</summary>
    Storage,

    /// <summary>A relay fired its outputs in the scene.</summary>
    Relay,
}

/// <summary>One janitor action that ran (see <see cref="Janitors.Acted"/>).</summary>
public sealed class JanitorAction
{
    /// <summary>The tag the story triggered, shared by every action of that kind across levels.</summary>
    public string Tag { get; }

    public JanitorKind Kind { get; }

    /// <summary>Stable address of the object that acted (see <see cref="ObjectAddress"/>).</summary>
    public string Address { get; }

    /// <summary>For storage actions: the item put in, or -1.</summary>
    public int ItemId { get; }

    /// <summary>For storage actions: the liquid put in, or -1.</summary>
    public int LiquidId { get; }

    /// <summary>For storage actions: the storage was emptied first.</summary>
    public bool Cleared { get; }

    /// <summary>
    /// The action was replayed by the game when a level loaded (it was triggered earlier, elsewhere), rather
    /// than triggered just now.
    /// </summary>
    public bool Replay { get; }

    internal JanitorAction(string tag, JanitorKind kind, string address, int itemId, int liquidId, bool cleared, bool replay)
    {
        Tag = tag;
        Kind = kind;
        Address = address;
        ItemId = itemId;
        LiquidId = liquidId;
        Cleared = cleared;
        Replay = replay;
    }
}

/// <summary>
/// The game's janitor actions: changes the story makes to the world in every level, by tag (a storage filled,
/// relays fired). The story triggers a tag from dialogue; the game runs the tag's actions in the scene at once
/// and replays them in each other level the first time it loads. The game saves which tags ran.
/// </summary>
public static class Janitors
{
    private static readonly AccessTools.FieldRef<JanitorController, List<JanitorController.IJanitorable>> Janitorables =
        AccessTools.FieldRefAccess<JanitorController, List<JanitorController.IJanitorable>>("janitorables");

    /// <summary>A janitor action ran, triggered now or replayed at a level load. Each handler runs in its own try/catch.</summary>
    public static readonly WorldEvent<JanitorAction> Acted = new WorldEvent<JanitorAction>(nameof(Acted));

    /// <summary>The tags with actions in the scene you are in, with how many actions each has.</summary>
    public static Dictionary<string, int> Tags()
    {
        var tags = new Dictionary<string, int>();
        var controller = JanitorController.instance;
        if (controller == null)
            return tags;
        foreach (var janitor in Janitorables(controller))
        {
            string tag = janitor != null ? janitor.janitorTag : null;
            if (string.IsNullOrEmpty(tag))
                continue;
            tags.TryGetValue(tag, out int count);
            tags[tag] = count + 1;
        }
        return tags;
    }

    /// <summary>The scene objects carrying janitor actions here, all of them or those with one tag.</summary>
    public static List<GameObject> Objects(string tag = null)
    {
        var objects = new List<GameObject>();
        var controller = JanitorController.instance;
        if (controller == null)
            return objects;
        foreach (var janitor in Janitorables(controller))
        {
            if (janitor is Component component && component != null && (tag == null || janitor.janitorTag == tag))
                objects.Add(component.gameObject);
        }
        return objects;
    }

    /// <summary>Some level the player has visited has actions with this tag (the game's Lua <c>HasJanitorAction</c>).</summary>
    public static bool Has(string tag) =>
        !string.IsNullOrEmpty(tag) && JanitorController.instance != null && JanitorController.instance.HasJanitorAction(tag);

    /// <summary>
    /// Triggers a tag the way the story does: its actions run here now and in each other level when it next
    /// loads. This is saved by the game and not undone. False outside a level or while a save or load runs.
    /// </summary>
    public static bool Trigger(string tag)
    {
        if (string.IsNullOrEmpty(tag))
            throw new ArgumentException("A tag is needed.", nameof(tag));
        var controller = JanitorController.instance;
        if (controller == null || SaveGate.IsBusy)
            return false;
        try
        {
            controller.TriggerJanitorAction(tag);
            return true;
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Janitors.Trigger", e);
            return false;
        }
    }

    internal static void RaiseStorage(StorageJanitorAction janitor, bool replay)
    {
        int item = janitor.itemToFill != null ? janitor.itemToFill.ID : -1;
        int liquid = janitor.liquidToFill != null ? janitor.liquidToFill.ID : -1;
        Acted.Raise(new JanitorAction(janitor.janitorTag, JanitorKind.Storage, ObjectAddress.Of(janitor).ToString(), item, liquid, janitor.overrideStorage, replay));
    }

    internal static void RaiseRelay(RelayJanitor janitor, bool replay) =>
        Acted.Raise(new JanitorAction(janitor.janitorTag, JanitorKind.Relay, ObjectAddress.Of(janitor).ToString(), -1, -1, false, replay));
}
