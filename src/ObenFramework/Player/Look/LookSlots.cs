// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Player.Look;

/// <summary>One wearable slot: which Resources folder its items live in and how to move it between the three representations.</summary>
internal sealed class LookSlot
{
    private readonly string _folder;
    private readonly string _maleFolder;

    public LookSlot(string name, string folder, Func<LookData, string> get, Action<LookData, string> set,
        Func<Appearance, Wearable> selected, Action<Character, Wearable> put, Func<Appearance, bool, Wearable[]> choices,
        string maleFolder = null)
    {
        Name = name;
        _folder = folder;
        _maleFolder = maleFolder ?? folder;
        Get = get;
        Set = set;
        Selected = selected;
        Put = put;
        Choices = choices;
    }

    public string Name { get; }
    public Func<LookData, string> Get { get; }
    public Action<LookData, string> Set { get; }

    /// <summary>The item currently worn on an Appearance.</summary>
    public Func<Appearance, Wearable> Selected { get; }

    /// <summary>Puts an item into a Character asset.</summary>
    public Action<Character, Wearable> Put { get; }

    /// <summary>The items an Appearance's own list offers for this slot (filled when it starts), by gender for heads.</summary>
    public Func<Appearance, bool, Wearable[]> Choices { get; }

    /// <summary>Resources folder holding this slot's items (heads depend on gender).</summary>
    public string Folder(bool male) => male ? _maleFolder : _folder;
}

/// <summary>Every wearable slot in a fixed order (the binary form relies on it, so only ever append).</summary>
internal static class LookSlots
{
    public static readonly LookSlot[] All =
    {
        new LookSlot("head", "Wearables/HeadsFemale", l => l.head, (l, v) => l.head = v, a => a.selectedHead, (c, w) => c.Head = w, (a, m) => m ? a.maleHeads : a.femaleHeads, "Wearables/HeadsMale"),
        new LookSlot("hat", "Wearables/Hats", l => l.hat, (l, v) => l.hat = v, a => a.selectedHat, (c, w) => c.Hat = w, (a, m) => a.hats),
        new LookSlot("hair", "Wearables/Hairs", l => l.hair, (l, v) => l.hair = v, a => a.selectedHair, (c, w) => c.Hair = w, (a, m) => a.hairs),
        new LookSlot("glasses", "Wearables/Glasses", l => l.glasses, (l, v) => l.glasses = v, a => a.selectedGlasses, (c, w) => c.Glasses = w, (a, m) => a.glasses),
        new LookSlot("beard", "Wearables/Beards", l => l.beard, (l, v) => l.beard = v, a => a.selectedBeard, (c, w) => c.Beard = w, (a, m) => a.beards),
        new LookSlot("jacket", "Wearables/ShirtsT", l => l.jacket, (l, v) => l.jacket = v, a => a.selectedShirtT, (c, w) => c.Jacket = w, (a, m) => a.shirtsT),
        new LookSlot("shirt", "Wearables/ShirtsB", l => l.shirt, (l, v) => l.shirt = v, a => a.selectedShirtB, (c, w) => c.ShirtB = w, (a, m) => a.shirtsB),
        new LookSlot("pants", "Wearables/PantsT", l => l.pants, (l, v) => l.pants = v, a => a.selectedPantsT, (c, w) => c.PantsT = w, (a, m) => a.pantsT),
        new LookSlot("leggings", "Wearables/PantsB", l => l.leggings, (l, v) => l.leggings = v, a => a.selectedPantsB, (c, w) => c.PantsB = w, (a, m) => a.pantsB),
        new LookSlot("socks", "Wearables/Socks", l => l.socks, (l, v) => l.socks = v, a => a.selectedSocks, (c, w) => c.Socks = w, (a, m) => a.socks),
        new LookSlot("shoes", "Wearables/Shoes", l => l.shoes, (l, v) => l.shoes = v, a => a.selectedShoes, (c, w) => c.Shoes = w, (a, m) => a.shoes),
    };
}
