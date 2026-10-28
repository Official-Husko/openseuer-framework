// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.IO;
using Newtonsoft.Json;

namespace ObenFramework.Player.Look;

/// <summary>
/// The two wire forms of a <see cref="LookData"/>. JSON is for people and saves. The binary form is for
/// the network: a version byte, a flags byte, each slider as a 16-bit step inside its range, then each
/// worn item's asset name as a length-prefixed UTF-8 string. Sliders and slots are counted, so a newer
/// build that appends one can still be read by an older one.
/// </summary>
public static class LookCodec
{
    private const byte Version = 1;
    private const byte MaleFlag = 1;
    private const byte AdvancedFlag = 2;

    public static string ToJson(LookData look, bool indented = false)
    {
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        return JsonConvert.SerializeObject(look, indented ? Formatting.Indented : Formatting.None);
    }

    public static LookData FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new ArgumentException("There is no JSON to read a look from.", nameof(json));
        var look = JsonConvert.DeserializeObject<LookData>(json) ?? throw new FormatException("The JSON holds no look.");
        return Normalize(look);
    }

    public static byte[] ToBytes(LookData look)
    {
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        using var stream = new MemoryStream(160);
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Version);
            writer.Write((byte)((look.male ? MaleFlag : 0) | (look.advanced ? AdvancedFlag : 0)));
            writer.Write((byte)LookDials.All.Length);
            foreach (var dial in LookDials.All)
                writer.Write(Quantize(dial, dial.Get(look)));
            writer.Write((byte)LookSlots.All.Length);
            foreach (var slot in LookSlots.All)
                writer.Write(slot.Get(look) ?? "");
        }
        return stream.ToArray();
    }

    public static LookData FromBytes(byte[] data)
    {
        if (data == null || data.Length < 4)
            throw new FormatException("The look data is too short.");
        try
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            byte version = reader.ReadByte();
            if (version != Version)
                throw new FormatException($"Look data version {version} isn't supported (expected {Version}).");

            var look = new LookData();
            byte flags = reader.ReadByte();
            look.male = (flags & MaleFlag) != 0;
            look.advanced = (flags & AdvancedFlag) != 0;

            int dials = reader.ReadByte();
            for (int i = 0; i < dials; i++)
            {
                ushort step = reader.ReadUInt16();
                if (i < LookDials.All.Length)
                    LookDials.All[i].Set(look, Dequantize(LookDials.All[i], step));
            }

            int slots = reader.ReadByte();
            for (int i = 0; i < slots; i++)
            {
                string name = reader.ReadString();
                if (i < LookSlots.All.Length)
                    LookSlots.All[i].Set(look, name);
            }
            return look;
        }
        catch (EndOfStreamException e)
        {
            throw new FormatException("The look data ends early.", e);
        }
    }

    // JSON from a hand-edited file may leave names out (null) or sliders out of range.
    private static LookData Normalize(LookData look)
    {
        foreach (var dial in LookDials.All)
            dial.Set(look, dial.Clamp(dial.Get(look)));
        foreach (var slot in LookSlots.All)
            slot.Set(look, slot.Get(look) ?? "");
        return look;
    }

    private static ushort Quantize(LookDial dial, float value)
    {
        float t = (dial.Clamp(value) - dial.Min) / (dial.Max - dial.Min);
        return (ushort)Math.Round(t * ushort.MaxValue);
    }

    private static float Dequantize(LookDial dial, ushort step) =>
        dial.Min + step / (float)ushort.MaxValue * (dial.Max - dial.Min);
}
