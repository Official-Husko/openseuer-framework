using System;
using ObenCoop.Net.Messaging;

namespace ObenCoop.Net.Presence;

/// <summary>
/// The presence wire format. A sample is: kind, sequence (2), sender time (4), area (string),
/// position (3 floats), yaw (2, a 360th of a turn in 65536 steps), velocity (3 x 2, centimetres per
/// second) and flags (1): about 30 bytes plus the area name. Reading checks every value, so a damaged
/// or hostile packet can't put a player at NaN or a million metres away.
/// </summary>
public static class PresenceCodec
{
    /// <summary>Longest area (scene) name in characters.</summary>
    public const int MaxAreaLength = 64;

    /// <summary>Furthest a coordinate may be from the origin. Obenseuer's levels are far smaller.</summary>
    public const float MaxCoordinate = 100000f;

    /// <summary>Fastest velocity that survives the trip (larger values are clamped): 327 m/s.</summary>
    public const float MaxSpeed = short.MaxValue / 100f;

    internal const byte KindSample = 1;
    internal const byte KindWithdrawn = 2;

    private const int MaxAreaBytes = MaxAreaLength * 4;

    /// <summary>True when a sample's values can be sent: a sensible area and finite, in-range numbers.</summary>
    public static bool IsValid(string area, PresenceVector position, float yaw, PresenceVector velocity, out string problem)
    {
        problem = null;
        if (string.IsNullOrEmpty(area) || area.Length > MaxAreaLength)
            problem = $"The area must be 1 to {MaxAreaLength} characters.";
        else if (!position.IsWithin(MaxCoordinate))
            problem = $"The position must be finite and within {MaxCoordinate} of the origin.";
        else if (float.IsNaN(yaw) || float.IsInfinity(yaw))
            problem = "The yaw must be a finite number.";
        else if (!velocity.IsWithin(float.MaxValue))
            problem = "The velocity must be finite.";
        return problem == null;
    }

    public static void Write(MessageWriter writer, in PresenceSample sample)
    {
        writer.WriteByte(KindSample);
        writer.WriteUInt16(sample.Sequence);
        writer.WriteUInt32(sample.Time);
        writer.WriteString(sample.Area);
        writer.WriteSingle(sample.Position.X);
        writer.WriteSingle(sample.Position.Y);
        writer.WriteSingle(sample.Position.Z);
        writer.WriteUInt16(PackYaw(sample.Yaw));
        writer.WriteUInt16((ushort)PackSpeed(sample.Velocity.X));
        writer.WriteUInt16((ushort)PackSpeed(sample.Velocity.Y));
        writer.WriteUInt16((ushort)PackSpeed(sample.Velocity.Z));
        writer.WriteByte((byte)sample.Flags);
    }

    public static void WriteWithdrawn(MessageWriter writer) => writer.WriteByte(KindWithdrawn);

    /// <summary>
    /// Reads the sample after the kind byte. False when a value is out of range. The area name comes
    /// from <paramref name="names"/>, which hands back the same string while the name doesn't change,
    /// so a steady stream of samples allocates nothing.
    /// </summary>
    /// <exception cref="MessageFormatException">The packet is too short.</exception>
    public static bool TryReadSample(MessageReader reader, AreaNames names, out PresenceSample sample)
    {
        sample = default;
        sample.Sequence = reader.ReadUInt16();
        sample.Time = reader.ReadUInt32();
        var areaBytes = reader.ReadStringBytes();
        float x = reader.ReadSingle(), y = reader.ReadSingle(), z = reader.ReadSingle();
        ushort yaw = reader.ReadUInt16();
        short vx = (short)reader.ReadUInt16(), vy = (short)reader.ReadUInt16(), vz = (short)reader.ReadUInt16();
        byte flags = reader.ReadByte();

        if (areaBytes.Count == 0 || areaBytes.Count > MaxAreaBytes)
            return false;
        sample.Position = new PresenceVector(x, y, z);
        if (!sample.Position.IsWithin(MaxCoordinate))
            return false;
        string area = names.Resolve(areaBytes);
        if (area.Length == 0 || area.Length > MaxAreaLength)
            return false;

        sample.Area = area;
        sample.Yaw = yaw * (360f / 65536f);
        sample.Velocity = new PresenceVector(vx / 100f, vy / 100f, vz / 100f);
        sample.Flags = (PresenceFlags)flags;
        return true;
    }

    internal static ushort PackYaw(float yaw)
    {
        double turns = yaw / 360.0;
        turns -= Math.Floor(turns);
        return (ushort)((int)Math.Round(turns * 65536.0) & 0xFFFF);
    }

    private static short PackSpeed(float speed)
    {
        double cm = Math.Round(speed * 100.0);
        return (short)(cm > short.MaxValue ? short.MaxValue : cm < -short.MaxValue ? -short.MaxValue : cm);
    }
}

/// <summary>
/// Turns area name bytes into a string, reusing the last string while the bytes are the same.
/// One per sender, so each keeps its own name.
/// </summary>
public sealed class AreaNames
{
    private readonly byte[] _bytes = new byte[PresenceCodec.MaxAreaLength * 4];
    private int _length = -1;
    private string _name = "";

    public string Resolve(ArraySegment<byte> bytes)
    {
        if (bytes.Count == _length && Same(bytes))
            return _name;
        if (bytes.Count > _bytes.Length)
            return "";
        Buffer.BlockCopy(bytes.Array, bytes.Offset, _bytes, 0, bytes.Count);
        _length = bytes.Count;
        _name = MessageWriter.Utf8.GetString(bytes.Array, bytes.Offset, bytes.Count);
        return _name;
    }

    private bool Same(ArraySegment<byte> bytes)
    {
        var source = bytes.Array;
        for (int i = 0; i < bytes.Count; i++)
        {
            if (source[bytes.Offset + i] != _bytes[i])
                return false;
        }
        return true;
    }
}
