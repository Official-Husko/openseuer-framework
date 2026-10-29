// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using UnityEngine;

namespace ObenFramework.World;

/// <summary>Whose a container is (the player's tenement, a tenant's apartment), and taking from one slot.</summary>
public static partial class Containers
{
    /// <summary>The player's own building is apartment 1 in the game's numbering.</summary>
    public const int PlayerApartment = 1;

    /// <summary>
    /// The tenement apartment an object stands in (1 is the player's own building, 2 and up the rented
    /// apartments), or -1 outside the tenement.
    /// </summary>
    public static int ApartmentOf(Component component)
    {
        if (component == null)
            return -1;
        var prefabs = component.GetComponentInParent<TenementApartmentPrefabs>();
        if (prefabs == null)
        {
            var manager = component.GetComponentInParent<FurnitureManager>();
            if (manager != null)
                prefabs = manager.tenementApartmentPrefabs;
        }
        return prefabs != null ? prefabs.apartmentNumber : -1;
    }

    /// <summary>The object is in the player's own part of the tenement.</summary>
    public static bool IsInPlayerTenement(Component component) => ApartmentOf(component) == PlayerApartment;

    /// <summary>The container is the player's: in their part of the tenement and not owned by a character.</summary>
    public static bool IsPlayerOwned(Storage storage) =>
        storage != null && Describe(storage).NpcOwner <= 0 && IsInPlayerTenement(storage);

    /// <summary>The container stands in an apartment whose tenant rents from the player right now.</summary>
    public static bool IsTenantOwned(Storage storage)
    {
        int number = ApartmentOf(storage);
        var tenement = TenementController.instance;
        if (number <= PlayerApartment || tenement == null || tenement.apartments == null)
            return false;
        var apartment = tenement.GetApartmentFromNumber(number);
        var resident = apartment != null && apartment.resident != null ? apartment.resident.resident : null;
        return resident != null && tenement.ResidentRented(resident.ID);
    }

    /// <summary>
    /// Takes up to <paramref name="amount"/> from one slot (the <see cref="ContainerItem.Slot"/> of
    /// <see cref="GetItems"/>), nothing going to the player, and returns how many were taken. Owned stacks
    /// are taken too, without a theft check.
    /// </summary>
    public static int TakeFromSlot(Storage storage, int slot, int amount, string asOwner = null, ChangeSource source = ChangeSource.Mod)
    {
        if (amount <= 0 || storage == null || storage.Slots == null || slot < 0 || slot >= storage.Slots.Length || Refused(storage, asOwner))
            return 0;
        var controller = storage.Slots[slot];
        var stack = controller != null ? controller.itemStack : null;
        if (stack == null || stack.itemId == -1 || stack.itemAmount <= 0)
            return 0;
        using (WorldChanges.Enter(source))
        {
            int remaining = ItemOperations.RemoveItemAmountFromSlot(controller, stack.itemId, amount);
            storage.UpdateSlots();
            return amount - remaining;
        }
    }
}
