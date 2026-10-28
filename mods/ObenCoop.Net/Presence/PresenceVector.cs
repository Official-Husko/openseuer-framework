using System;
using System.Globalization;

namespace ObenCoop.Net.Presence;

/// <summary>
/// A position or velocity in world units. The net library has no Unity references, so this stands in
/// for <c>Vector3</c>; the mod converts at the edge.
/// </summary>
public readonly struct PresenceVector : IEquatable<PresenceVector>
{
    public PresenceVector(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static PresenceVector Zero => default;

    public float X { get; }
    public float Y { get; }
    public float Z { get; }

    public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

    /// <summary>True when every component is a real number no further than <paramref name="limit"/> from zero.</summary>
    public bool IsWithin(float limit) => Fits(X, limit) && Fits(Y, limit) && Fits(Z, limit);

    public float DistanceTo(PresenceVector other) => (this - other).Length;

    /// <summary>Straight line between two points; <paramref name="t"/> is clamped to 0..1.</summary>
    public static PresenceVector Lerp(PresenceVector a, PresenceVector b, float t)
    {
        t = t < 0f ? 0f : t > 1f ? 1f : t;
        return new PresenceVector(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
    }

    public static PresenceVector operator +(PresenceVector a, PresenceVector b) => new PresenceVector(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    public static PresenceVector operator -(PresenceVector a, PresenceVector b) => new PresenceVector(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

    public static PresenceVector operator *(PresenceVector a, float k) => new PresenceVector(a.X * k, a.Y * k, a.Z * k);

    public bool Equals(PresenceVector other) => X == other.X && Y == other.Y && Z == other.Z;

    public override bool Equals(object obj) => obj is PresenceVector other && Equals(other);

    public override int GetHashCode() => X.GetHashCode() ^ (Y.GetHashCode() << 2) ^ (Z.GetHashCode() >> 2);

    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.00}, {1:0.00}, {2:0.00})", X, Y, Z);

    private static bool Fits(float value, float limit) => !float.IsNaN(value) && !float.IsInfinity(value) && Math.Abs(value) <= limit;
}
