// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Crafting;

/// <summary>
/// A station took ingredients out of an extra source, raised once per slot by <see cref="CraftingSources.Consumed"/>.
/// </summary>
public readonly struct CraftingTaken
{
    public CraftingTaken(string sourceOwner, Storage storage, int slot, int itemId, int amount, int liquidItemId, float liquidAmount, CraftingBase station)
    {
        SourceOwner = sourceOwner;
        Storage = storage;
        Slot = slot;
        ItemId = itemId;
        Amount = amount;
        LiquidItemId = liquidItemId;
        LiquidAmount = liquidAmount;
        Station = station;
    }

    /// <summary>The id of the mod whose provider offered the container.</summary>
    public string SourceOwner { get; }

    public Storage Storage { get; }

    /// <summary>Index of the slot in the container.</summary>
    public int Slot { get; }

    public int ItemId { get; }

    /// <summary>Units taken from the stack; 0 when only liquid was poured out of it.</summary>
    public int Amount { get; }

    /// <summary>The liquid poured out of a container in the slot, or -1.</summary>
    public int LiquidItemId { get; }

    public float LiquidAmount { get; }

    public CraftingBase Station { get; }
}
