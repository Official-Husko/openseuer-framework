// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Economy;

/// <summary>One player-run shop as stored with the save.</summary>
internal sealed class PlayerShopRecord
{
    public string Id = "";
    public string Name = "";

    /// <summary>GUID of the building area (the game's <c>FurnitureManager</c>) whose containers are the shelves, or null.</summary>
    public string Area;

    /// <summary>Container ids (see <c>Containers.IdOf</c>) that are shelves besides the area's.</summary>
    public List<string> Containers = new List<string>();

    public bool Open;
    public float Markup = PlayerShopMath.DefaultMarkup;
    public bool RealMoney;
    public int TillCoins;
    public float TillRealMoney;

    /// <summary>Fixed prices per item id, in the shop's currency.</summary>
    public Dictionary<int, float> Prices = new Dictionary<int, float>();
}

/// <summary>Every player-run shop of a save (<c>mods/ObenFramework.player-shops.json</c>).</summary>
internal sealed class PlayerShopSave
{
    public List<PlayerShopRecord> Shops = new List<PlayerShopRecord>();

    public PlayerShopRecord Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        foreach (var shop in Shops)
        {
            if (shop != null && shop.Id == id)
                return shop;
        }
        return null;
    }
}
