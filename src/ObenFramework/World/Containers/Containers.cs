// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>
/// The containers of the loaded scenes (the game's <c>Storage</c>): listing them with ids that survive
/// sessions, reading what is in them, adding and removing items through the game's own methods, and events
/// for the local player opening and closing them and for their contents changing. <see cref="ContainerLocks"/>
/// lets one user hold a container at a time.
/// </summary>
/// <remarks>
/// Only containers that have started (active in a loaded scene) can be used; the backpacks' hidden storages
/// are listed like any other. Contents of containers in scenes that are not loaded are not reachable.
/// </remarks>
public static partial class Containers
{
    /// <summary>The local player opened a container (or a mod or the game did; see <see cref="ContainerEvent.Source"/>).</summary>
    public static readonly WorldEvent<ContainerEvent> Opened = new WorldEvent<ContainerEvent>(nameof(Opened));

    /// <summary>A container was closed, or went away while open.</summary>
    public static readonly WorldEvent<ContainerEvent> Closed = new WorldEvent<ContainerEvent>(nameof(Closed));

    /// <summary>
    /// The contents changed through the game's storage methods, the inventory window, or <see cref="Add"/> and
    /// <see cref="Remove"/>. Loot that a container spawns for itself is not reported.
    /// </summary>
    public static readonly WorldEvent<ContainerEvent> Changed = new WorldEvent<ContainerEvent>(nameof(Changed));

    /// <summary>All started containers in the loaded scenes. A scan; not for per-frame use.</summary>
    public static List<Storage> All(bool includeInactive = false)
    {
        var result = new List<Storage>();
        var mode = includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude;
        foreach (var storage in UnityEngine.Object.FindObjectsByType<Storage>(mode, FindObjectsSortMode.None))
        {
            if (storage.Slots != null)
                result.Add(storage);
        }
        return result;
    }

    /// <summary>Snapshots of <see cref="All"/>.</summary>
    public static List<ContainerInfo> List(bool includeInactive = false)
    {
        var all = All(includeInactive);
        var infos = new List<ContainerInfo>(all.Count);
        foreach (var storage in all)
            infos.Add(Describe(storage));
        return infos;
    }

    /// <summary>
    /// The container's id. Containers in furniture the player placed are <c>furniture:{furnitureId}:{n}</c>
    /// (the n-th container inside that piece), because their prefab GUID is shared by every copy. Every other
    /// container is its game GUID, or its <see cref="ObjectAddress"/> text when it has none.
    /// </summary>
    public static string IdOf(Storage storage) => ObjectIds.IdOf(storage);

    /// <summary>The container with this id (or any text <see cref="ObjectResolver.Find{T}"/> understands), or null.</summary>
    public static Storage Find(string id) => ObjectIds.Find<Storage>(id);

    public static ContainerInfo Describe(Storage storage)
    {
        if (storage == null)
            throw new ArgumentNullException(nameof(storage));

        string id = IdOf(storage);
        var owner = storage.StorageOwner;
        return new ContainerInfo(storage, id, storage.title, storage.gameObject.scene.name, storage.transform.position,
            storage.Slots != null ? storage.Slots.Length : 0, storage.IsEmpty(), storage._Lock != null && storage._Lock.Locked,
            owner != null && owner.ID > 1 ? owner.ID : -1, ContainerLocks.OwnerOf(id));
    }

    /// <summary>The stacks in a container, empty slots left out.</summary>
    public static List<ContainerItem> GetItems(Storage storage)
    {
        var items = new List<ContainerItem>();
        if (storage == null || storage.Slots == null)
            return items;

        for (int i = 0; i < storage.Slots.Length; i++)
        {
            var stack = storage.Slots[i] != null ? storage.Slots[i].itemStack : null;
            if (stack == null || stack.itemId == -1 || stack.itemAmount <= 0)
                continue;
            var item = ItemLookup.ById(stack.itemId);
            items.Add(new ContainerItem(i, stack.itemId, item != null ? item.Title : "#" + stack.itemId, stack.itemAmount, stack.ownerId));
        }
        return items;
    }

    /// <summary>How many of an item the container holds.</summary>
    public static int Count(Storage storage, int itemId) =>
        storage != null && storage.Slots != null ? (int)storage.GetItemAmount(itemId) : 0;

    /// <summary>
    /// Puts items into a container with the game's own storage method: they stack and go to free slots, and the
    /// container's category rules apply. Nothing is dropped on the floor. Returns how many did NOT fit, so 0
    /// means all went in.
    /// </summary>
    /// <param name="asOwner">
    /// Who is editing, checked against <see cref="ContainerLocks"/>: when another owner holds the container
    /// nothing is added and the whole amount is returned. Null means <see cref="ContainerLocks.LocalOwner"/>.
    /// </param>
    /// <param name="source">What the <see cref="Changed"/> event reports as the cause.</param>
    public static int Add(Storage storage, Item item, int amount = 1, string asOwner = null, ChangeSource source = ChangeSource.Mod)
    {
        if (amount <= 0)
            return 0;
        if (storage == null || storage.Slots == null || storage.Slots.Length == 0 || item == null || Refused(storage, asOwner))
            return Math.Max(amount, 0);

        using (WorldChanges.Enter(source))
        {
            int left = storage.AddItemAndReturnRemaining(item, amount, -1, item.CloneMeta(false, amount, item));
            storage.UpdateSlots();
            return left;
        }
    }

    /// <inheritdoc cref="Add(Storage, Item, int, string, ChangeSource)"/>
    public static int Add(Storage storage, int itemId, int amount = 1, string asOwner = null, ChangeSource source = ChangeSource.Mod) =>
        Add(storage, ItemLookup.ById(itemId), amount, asOwner, source);

    /// <summary>
    /// Takes up to <paramref name="amount"/> of an item out of a container (nothing goes to the player) and
    /// returns how many were taken. Stacks owned by other characters are taken too, without a theft check.
    /// </summary>
    /// <param name="asOwner">See <see cref="Add(Storage, Item, int, string, ChangeSource)"/>; a locked-out caller takes nothing.</param>
    public static int Remove(Storage storage, int itemId, int amount = 1, string asOwner = null, ChangeSource source = ChangeSource.Mod)
    {
        if (amount <= 0 || storage == null || storage.Slots == null || Refused(storage, asOwner))
            return 0;

        using (WorldChanges.Enter(source))
        {
            int before = Count(storage, itemId);
            storage.RemoveItem(itemId, out _, amount, false, true, -1);
            storage.UpdateSlots();
            return before - Count(storage, itemId);
        }
    }

    private static bool Refused(Storage storage, string asOwner) =>
        ContainerLocks.Any && ContainerLocks.IsHeldByOther(IdOf(storage), asOwner ?? ContainerLocks.LocalOwner);
}
