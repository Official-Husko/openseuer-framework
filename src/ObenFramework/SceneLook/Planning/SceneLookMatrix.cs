// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>
/// A 4x4 affine matrix with the same layout and conventions as Unity's <c>Matrix4x4</c> (row, column;
/// translation in the last column), so the planning maths runs and is tested without Unity.
/// </summary>
internal struct SceneLookMatrix
{
    public float M00, M01, M02, M03;
    public float M10, M11, M12, M13;
    public float M20, M21, M22, M23;
    public float M30, M31, M32, M33;

    public static SceneLookMatrix Identity() => new SceneLookMatrix { M00 = 1f, M11 = 1f, M22 = 1f, M33 = 1f };

    /// <summary>A matrix as Unity serializes it: sixteen floats, row by row (e00, e01, e02, e03, e10...).</summary>
    public static SceneLookMatrix FromFile(float[] v) => new SceneLookMatrix
    {
        M00 = v[0], M01 = v[1], M02 = v[2], M03 = v[3],
        M10 = v[4], M11 = v[5], M12 = v[6], M13 = v[7],
        M20 = v[8], M21 = v[9], M22 = v[10], M23 = v[11],
        M30 = v[12], M31 = v[13], M32 = v[14], M33 = v[15],
    };

    /// <summary>Translation, rotation (x, y, z, w) and scale, like <c>Matrix4x4.TRS</c>.</summary>
    public static SceneLookMatrix Trs(float[] position, float[] rotation, float[] scale)
    {
        float x = rotation[0], y = rotation[1], z = rotation[2], w = rotation[3];
        float xx = x * x, yy = y * y, zz = z * z;
        float xy = x * y, xz = x * z, yz = y * z;
        float wx = w * x, wy = w * y, wz = w * z;
        return new SceneLookMatrix
        {
            M00 = (1f - 2f * (yy + zz)) * scale[0],
            M01 = 2f * (xy - wz) * scale[1],
            M02 = 2f * (xz + wy) * scale[2],
            M10 = 2f * (xy + wz) * scale[0],
            M11 = (1f - 2f * (xx + zz)) * scale[1],
            M12 = 2f * (yz - wx) * scale[2],
            M20 = 2f * (xz - wy) * scale[0],
            M21 = 2f * (yz + wx) * scale[1],
            M22 = (1f - 2f * (xx + yy)) * scale[2],
            M03 = position[0],
            M13 = position[1],
            M23 = position[2],
            M33 = 1f,
        };
    }

    public SceneLookMatrix Multiply(SceneLookMatrix o) => new SceneLookMatrix
    {
        M00 = M00 * o.M00 + M01 * o.M10 + M02 * o.M20 + M03 * o.M30,
        M01 = M00 * o.M01 + M01 * o.M11 + M02 * o.M21 + M03 * o.M31,
        M02 = M00 * o.M02 + M01 * o.M12 + M02 * o.M22 + M03 * o.M32,
        M03 = M00 * o.M03 + M01 * o.M13 + M02 * o.M23 + M03 * o.M33,
        M10 = M10 * o.M00 + M11 * o.M10 + M12 * o.M20 + M13 * o.M30,
        M11 = M10 * o.M01 + M11 * o.M11 + M12 * o.M21 + M13 * o.M31,
        M12 = M10 * o.M02 + M11 * o.M12 + M12 * o.M22 + M13 * o.M32,
        M13 = M10 * o.M03 + M11 * o.M13 + M12 * o.M23 + M13 * o.M33,
        M20 = M20 * o.M00 + M21 * o.M10 + M22 * o.M20 + M23 * o.M30,
        M21 = M20 * o.M01 + M21 * o.M11 + M22 * o.M21 + M23 * o.M31,
        M22 = M20 * o.M02 + M21 * o.M12 + M22 * o.M22 + M23 * o.M32,
        M23 = M20 * o.M03 + M21 * o.M13 + M22 * o.M23 + M23 * o.M33,
        M30 = M30 * o.M00 + M31 * o.M10 + M32 * o.M20 + M33 * o.M30,
        M31 = M30 * o.M01 + M31 * o.M11 + M32 * o.M21 + M33 * o.M31,
        M32 = M30 * o.M02 + M31 * o.M12 + M32 * o.M22 + M33 * o.M32,
        M33 = M30 * o.M03 + M31 * o.M13 + M32 * o.M23 + M33 * o.M33,
    };

    /// <summary>The inverse of an affine matrix (the last row is taken as 0, 0, 0, 1).</summary>
    public SceneLookMatrix Inverse()
    {
        float a = M00, b = M01, c = M02;
        float d = M10, e = M11, f = M12;
        float g = M20, h = M21, i = M22;
        float determinant = a * (e * i - f * h) - b * (d * i - f * g) + c * (d * h - e * g);
        float inverse = 1f / determinant;
        var m = new SceneLookMatrix
        {
            M00 = (e * i - f * h) * inverse,
            M01 = (c * h - b * i) * inverse,
            M02 = (b * f - c * e) * inverse,
            M10 = (f * g - d * i) * inverse,
            M11 = (a * i - c * g) * inverse,
            M12 = (c * d - a * f) * inverse,
            M20 = (d * h - e * g) * inverse,
            M21 = (b * g - a * h) * inverse,
            M22 = (a * e - b * d) * inverse,
            M33 = 1f,
        };
        m.M03 = -(m.M00 * M03 + m.M01 * M13 + m.M02 * M23);
        m.M13 = -(m.M10 * M03 + m.M11 * M13 + m.M12 * M23);
        m.M23 = -(m.M20 * M03 + m.M21 * M13 + m.M22 * M23);
        return m;
    }

    public float Determinant3() =>
        M00 * (M11 * M22 - M12 * M21) - M01 * (M10 * M22 - M12 * M20) + M02 * (M10 * M21 - M11 * M20);

    public float[] Translation() => new[] { M03, M13, M23 };

    /// <summary>The largest difference of any element, for comparing matrices.</summary>
    public float MaxDifference(SceneLookMatrix o)
    {
        float most = 0f;
        most = Math.Max(most, Math.Abs(M00 - o.M00)); most = Math.Max(most, Math.Abs(M01 - o.M01));
        most = Math.Max(most, Math.Abs(M02 - o.M02)); most = Math.Max(most, Math.Abs(M03 - o.M03));
        most = Math.Max(most, Math.Abs(M10 - o.M10)); most = Math.Max(most, Math.Abs(M11 - o.M11));
        most = Math.Max(most, Math.Abs(M12 - o.M12)); most = Math.Max(most, Math.Abs(M13 - o.M13));
        most = Math.Max(most, Math.Abs(M20 - o.M20)); most = Math.Max(most, Math.Abs(M21 - o.M21));
        most = Math.Max(most, Math.Abs(M22 - o.M22)); most = Math.Max(most, Math.Abs(M23 - o.M23));
        most = Math.Max(most, Math.Abs(M30 - o.M30)); most = Math.Max(most, Math.Abs(M31 - o.M31));
        most = Math.Max(most, Math.Abs(M32 - o.M32));
        return Math.Max(most, Math.Abs(M33 - o.M33));
    }

    /// <summary>Distance between the translations of two matrices.</summary>
    public static float Distance(SceneLookMatrix first, SceneLookMatrix second)
    {
        float x = first.M03 - second.M03;
        float y = first.M13 - second.M13;
        float z = first.M23 - second.M23;
        return (float)Math.Sqrt(x * x + y * y + z * z);
    }
}

/// <summary>One mesh's bones (by any key) and their bind poses, in renderer order.</summary>
internal sealed class SceneLookBindMesh<TKey>
{
    public readonly List<TKey> Bones = new List<TKey>();
    public readonly List<SceneLookMatrix> Poses = new List<SceneLookMatrix>();

    public void Add(TKey bone, SceneLookMatrix pose)
    {
        Bones.Add(bone);
        Poses.Add(pose);
    }
}
