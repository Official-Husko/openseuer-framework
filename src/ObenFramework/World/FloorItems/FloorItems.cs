using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.Items;
using ObenFramework.Patching;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// Items lying in the world as pickups (the game's <c>CollectibleItem</c> and coins): listing them, ids that
/// last for the session, events for them appearing, being dropped and being picked up, and removing one
/// without giving it to the local player. To put a stack on the ground see <see cref="Spawn"/>.
/// </summary>
/// <remarks>
/// A pickup that came with the scene is identified by the game's GUID; one that was spawned gets a random id the
/// first time anyone asks (or when it appears and someone listens). Spawned pickups are saved with the level by
/// the game, but their ids are not: after a load they get new ones. The list covers pickups that have started,
/// which means those in active objects of loaded scenes.
/// </remarks>
public static class FloorItems
{
    /// <summary>
    /// The framework's world hooks are in place. False when they failed to apply (a game update changed what they
    /// patch); the events then stay quiet and ids given at spawn may not stick.
    /// </summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World");

    /// <summary>A pickup appeared by any means: a drop, a mod's <see cref="Spawn"/>, the game putting an item down.</summary>
    public static readonly WorldEvent<FloorItemEvent> Spawned = new WorldEvent<FloorItemEvent>(nameof(Spawned));

    /// <summary>
    /// A character dropped something (the player from the inventory, a full inventory or storage spilling over, an
    /// NPC). Raised right after <see cref="Spawned"/> for the same pickup; <see cref="ChangeSource.LocalPlayer"/> when the player did.
    /// </summary>
    public static readonly WorldEvent<FloorItemEvent> Dropped = new WorldEvent<FloorItemEvent>(nameof(Dropped));

    /// <summary>
    /// The player picked a pickup up (all or part of it; <see cref="FloorItemInfo.Amount"/> is how much was taken).
    /// Not raised for <see cref="Remove"/>.
    /// </summary>
    public static readonly WorldEvent<FloorItemEvent> PickedUp = new WorldEvent<FloorItemEvent>(nameof(PickedUp));

    private static readonly Dictionary<Collectible, string> Ids = new Dictionary<Collectible, string>();
    private static readonly Dictionary<string, Collectible> ById = new Dictionary<string, Collectible>();
    private static string _pendingId;
    private static int _pruneAt = 512;

    internal static bool AnySubscribers => Spawned.HasSubscribers || Dropped.HasSubscribers || PickedUp.HasSubscribers;

    /// <summary>Every pickup of an item or of coins that has started. A walk over the game's own list.</summary>
    public static List<FloorItemInfo> All()
    {
        var result = new List<FloorItemInfo>();
        foreach (var collectible in Collectible.allCollectibles)
        {
            if (collectible != null && TryDescribe(collectible, out var info))
                result.Add(info);
        }
        return result;
    }

    /// <summary>The pickup with this id, or null.</summary>
    public static Collectible Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (ById.TryGetValue(id, out var known) && known != null)
            return known;

        foreach (var collectible in Collectible.allCollectibles)
        {
            if (collectible != null && collectible.GUID == id && IsItem(collectible))
                return collectible;
        }
        return null;
    }

    /// <summary>The pickup's id: the id it was given, else the game's GUID where it has one, else a new random one.</summary>
    public static string IdOf(Collectible collectible)
    {
        if (collectible == null)
            return null;
        if (Ids.TryGetValue(collectible, out var id))
            return id;

        id = _pendingId ?? (string.IsNullOrEmpty(collectible.GUID) ? Guid.NewGuid().ToString("N").Substring(0, 8) : collectible.GUID);
        _pendingId = null;
        Assign(collectible, id);
        return id;
    }

    /// <summary>The id of the pickup on <paramref name="obj"/> (what <see cref="WorldItems.Spawn"/> returns), or null when it has none.</summary>
    public static string IdOf(GameObject obj) => obj != null && obj.TryGetComponent<Collectible>(out var collectible) ? IdOf(collectible) : null;

    /// <summary>
    /// Makes <paramref name="id"/> the pickup's id, for a machine that has to agree with another. The id must not
    /// belong to another pickup.
    /// </summary>
    public static void Adopt(Collectible collectible, string id)
    {
        if (collectible == null)
            throw new ArgumentNullException(nameof(collectible));
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("No id given.", nameof(id));
        if (ById.TryGetValue(id, out var holder) && holder != null && holder != collectible)
            throw new ArgumentException($"The id {id} belongs to another pickup.", nameof(id));

        if (Ids.TryGetValue(collectible, out var old))
            ById.Remove(old);
        Assign(collectible, id);
    }

    /// <summary>A snapshot of a pickup, or false when it isn't an item or coin pickup.</summary>
    public static bool TryDescribe(Collectible collectible, out FloorItemInfo info)
    {
        info = default;
        if (collectible == null)
            return false;

        int itemId;
        string title;
        int amount;
        if (collectible is CollectibleItem pickup)
        {
            var item = pickup.Item != null ? pickup.Item.Item : null;
            if (item == null)
                return false;
            itemId = item.ID;
            title = item.Title;
            amount = pickup.amount;
        }
        else if (collectible is CollectibleCoin coin)
        {
            itemId = coin.isRealMoney ? PlayerItems.RealMoneyId : PlayerItems.CoinId;
            title = coin.isRealMoney ? "Real Money" : "Open Sewer Coin";
            amount = Mathf.RoundToInt(coin.Amount);
        }
        else
        {
            return false;
        }

        info = new FloorItemInfo(collectible.gameObject, IdOf(collectible), itemId, title, amount, collectible.transform.position,
            collectible.gameObject.scene.name);
        return true;
    }

    /// <summary>
    /// Puts a stack on the ground at an exact position, as a normal pickup the player can take and the game
    /// saves with the level. Returns null when the item is unknown or has no pickup prefab.
    /// </summary>
    /// <param name="id">The id to give the pickup (see <see cref="Adopt"/>); a random one when null.</param>
    /// <param name="source">What <see cref="Spawned"/> reports as the cause.</param>
    public static FloorItemInfo? Spawn(Item item, int amount, Vector3 position, string id = null, ChangeSource source = ChangeSource.Mod)
    {
        if (item == null || amount <= 0 || (!string.IsNullOrEmpty(id) && Find(id) != null))
            return null;

        _pendingId = string.IsNullOrEmpty(id) ? null : id;
        GameObject spawned;
        try
        {
            using (WorldChanges.Enter(source))
                spawned = WorldItems.Spawn(item, amount, position);
        }
        finally
        {
            _pendingId = null;
        }

        var collectible = spawned != null ? spawned.GetComponent<Collectible>() : null;
        return collectible != null && TryDescribe(collectible, out var info) ? info : (FloorItemInfo?)null;
    }

    /// <summary>
    /// Takes a pickup out of the world without giving it to anyone: it is destroyed and the game remembers it as
    /// gone, so it does not come back after a load. No <see cref="PickedUp"/> is raised. Returns false when
    /// there is no such pickup.
    /// </summary>
    public static bool Remove(string id)
    {
        var collectible = Find(id);
        if (collectible == null || WorldState.IsLoadingSave)
            return false;

        Forget(collectible);
        return collectible.Destroy();
    }

    internal static void Reset()
    {
        Ids.Clear();
        ById.Clear();
        _pendingId = null;
        _pruneAt = 512;
    }

    // Called by the hooks, which know the object just appeared.
    internal static string Register(Collectible collectible)
    {
        // Drop entries of pickups that are gone now and then, so the tables don't grow for a whole play session.
        if (Ids.Count >= _pruneAt)
        {
            Prune();
            _pruneAt = Math.Max(512, Ids.Count * 2);
        }
        return IdOf(collectible);
    }

    internal static void Forget(Collectible collectible)
    {
        if (Ids.TryGetValue(collectible, out var id))
        {
            ById.Remove(id);
            Ids.Remove(collectible);
        }
    }

    internal static bool IsItem(Collectible collectible) => collectible is CollectibleItem || collectible is CollectibleCoin;

    private static void Assign(Collectible collectible, string id)
    {
        Ids[collectible] = id;
        ById[id] = collectible;
    }

    private static void Prune()
    {
        var gone = new List<Collectible>();
        foreach (var pair in Ids)
        {
            if (pair.Key == null)
                gone.Add(pair.Key);
        }
        foreach (var collectible in gone)
        {
            ById.Remove(Ids[collectible]);
            Ids.Remove(collectible);
        }
    }
}
