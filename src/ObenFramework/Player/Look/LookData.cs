// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Player.Look;

/// <summary>
/// A compact description of a character's look: the body sliders and the worn items. Worn items are
/// stored by asset name ("Hat small - Black Beanie"), never by index, because the order the game loads
/// them in is not stable. An empty name means the slot's "none" item. Turn it into JSON or bytes with
/// <see cref="ToJson"/> and <see cref="ToBytes"/> to send it over a network or keep it in a save.
/// </summary>
[Serializable]
public sealed class LookData
{
    public bool male = true;

    /// <summary>The character editor's advanced mode (the face sliders are used).</summary>
    public bool advanced;

    // Body, 0 to 1 unless noted.
    public float height = 0.8f;

    /// <summary>-1 muscular to 1 heavy.</summary>
    public float shape;
    public float gender;
    public float headSize = 1f;
    public float faceSize = 1f;
    public float neckLength;
    public float shoulderWidth;
    public float breastSize;
    public float hipWidth;
    public float handSize = 1f;
    public float footSize = 1f;

    // Face.
    public float lipThickness;
    public float jawPosition;
    public float chinPosition;
    public float eyeSize = 1f;
    public float eyeDistance;
    public float eyePosition;
    public float eyeDepth;
    public float earSize = 1f;
    public float earRotation;
    public float noseRootRotation;
    public float noseTipRotation;
    public float noseWidth;

    // Worn items by asset name.
    public string head = "";
    public string hat = "";
    public string hair = "";
    public string glasses = "";
    public string beard = "";
    public string jacket = "";
    public string shirt = "";
    public string pants = "";
    public string leggings = "";
    public string socks = "";
    public string shoes = "";

    public LookData Clone() => (LookData)MemberwiseClone();

    /// <summary>True when both describe the same look (floats within a tiny tolerance).</summary>
    public bool SameAs(LookData other)
    {
        if (other == null || male != other.male || advanced != other.advanced)
            return false;
        foreach (var dial in LookDials.All)
        {
            if (Math.Abs(dial.Get(this) - dial.Get(other)) > 0.0005f)
                return false;
        }
        foreach (var slot in LookSlots.All)
        {
            if (!string.Equals(slot.Get(this), slot.Get(other), StringComparison.Ordinal))
                return false;
        }
        return true;
    }

    /// <summary>Readable JSON, for logs, saves and the dev bridge.</summary>
    public string ToJson(bool indented = false) => LookCodec.ToJson(this, indented);

    /// <summary>Compact binary form: 2 bytes per slider plus the item names. See <see cref="LookCodec"/>.</summary>
    public byte[] ToBytes() => LookCodec.ToBytes(this);

    public static LookData FromJson(string json) => LookCodec.FromJson(json);

    public static LookData FromBytes(byte[] data) => LookCodec.FromBytes(data);
}
