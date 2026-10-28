// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using OS.Items;

namespace ObenFramework.Items;

/// <summary>
/// A snapshot of one stack with the details the game keeps in its meta data: quality, freshness, liquid
/// contents, stolen flag, charge or ammo, wallet coins and written text. Values that don't apply to the item
/// are -1 (numbers), false or null.
/// </summary>
public sealed class ItemStackInfo
{
    public int ItemId { get; private set; }

    public int Amount { get; private set; }

    /// <summary>Index of the slot in the list it came from, or -1.</summary>
    public int Slot { get; private set; }

    /// <summary>Id of the character the stack belongs to, -1 for nobody.</summary>
    public int OwnerId { get; private set; }

    /// <summary>Quality from 0 to 2 (1 is normal), or -1 for items without quality.</summary>
    public float Quality { get; private set; }

    /// <summary>Freshness of perishable items in percent, or -1.</summary>
    public int IntegrityPercent { get; private set; }

    /// <summary>The liquid in a container, or -1 when it holds none.</summary>
    public int LiquidItemId { get; private set; }

    /// <summary>How much liquid the whole stack holds.</summary>
    public float LiquidAmount { get; private set; }

    public bool IsStolen { get; private set; }

    /// <summary>The owner allowed the player to have it.</summary>
    public bool HasOwnerPermission { get; private set; }

    /// <summary>Ammo or charge, or -1.</summary>
    public float Charge { get; private set; }

    public float MaxCharge { get; private set; }

    /// <summary>Coins in a wallet item, or -1.</summary>
    public float WalletCoins { get; private set; }

    public bool IsWritable { get; private set; }

    public string WrittenTitle { get; private set; }

    public string WrittenText { get; private set; }

    /// <summary>Reads a stack. Returns null for null.</summary>
    public static ItemStackInfo From(ItemStack stack, int slot = -1)
    {
        if (stack == null)
            return null;

        var quality = stack.GetQualitydata();
        var perishable = stack.GetMetaOfType<ItemPerishabledata>();
        var liquid = stack.GetMetaOfType<ItemLiquidData>();
        var stolen = stack.GetMetaOfType<ItemStolenData>();
        var ammo = stack.GetMetaOfType<ItemAmmodata>();
        var wallet = stack.GetMetaOfType<ItemOSCoinWalletData>();
        var writable = stack.GetMetaOfType<ItemWritableData>();
        var permission = stack.GetMetaOfType<ItemOwnerPermission>();
        bool hasLiquid = liquid != null && liquid.liquidItem != null && liquid.liquidAmount > 0f;

        return new ItemStackInfo
        {
            ItemId = stack.itemId,
            Amount = stack.itemAmount,
            Slot = slot,
            OwnerId = stack.ownerId,
            Quality = quality != null ? quality.quality : -1f,
            IntegrityPercent = perishable != null ? perishable.GetIntegrityPercent() : -1,
            LiquidItemId = hasLiquid ? liquid.liquidItem.ID : -1,
            LiquidAmount = hasLiquid ? liquid.GetLiquidAmount(stack.itemAmount) : 0f,
            IsStolen = stolen != null && stolen.playerStolen,
            HasOwnerPermission = permission != null && permission.ownerPermission,
            Charge = ammo != null ? ammo.ammo : -1f,
            MaxCharge = ammo != null ? ammo.maxAmmo : -1f,
            WalletCoins = wallet != null ? wallet.coins : -1f,
            IsWritable = writable != null,
            WrittenTitle = writable != null ? writable.title : null,
            WrittenText = writable != null ? writable.content : null,
        };
    }
}
