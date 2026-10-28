// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Assets;

/// <summary>Where a model's origin ends up.</summary>
public enum ModelPivot
{
    /// <summary>The origin stays where the file put it.</summary>
    Keep = 0,

    /// <summary>The origin moves to the middle of the underside, handy for things that stand on the ground.</summary>
    BottomCenter = 1,

    /// <summary>The origin moves to the centre of the bounding box.</summary>
    Center = 2,
}

/// <summary>How a parsed model is scaled, turned and recentred.</summary>
internal sealed class MeshSettings
{
    public float Scale { get; set; } = 1f;
    public float EulerX { get; set; }
    public float EulerY { get; set; }
    public float EulerZ { get; set; }
    public float OffsetX { get; set; }
    public float OffsetY { get; set; }
    public float OffsetZ { get; set; }
    public ModelPivot Pivot { get; set; } = ModelPivot.Keep;
}

/// <summary>Final vertex data of a model, ready to hand to a mesh.</summary>
internal sealed class MeshArrays
{
    public float[] Positions;
    public float[] Normals;
    public float[] Uvs;
    public int[][] Indices;
    public float[] Min = new float[3];
    public float[] Max = new float[3];
}

/// <summary>
/// Turns a parsed OBJ into the arrays of a mesh: applies scale, rotation and pivot, rotates the normals
/// along, and calculates smooth normals when the file has none. Pure maths on arrays, so the heavy part of
/// loading a model can run on a worker thread.
/// </summary>
internal static class MeshPlan
{
    public static MeshArrays Build(ObjData data, MeshSettings settings)
    {
        int count = data.VertexCount;
        float[] r = Rotation(settings.EulerX, settings.EulerY, settings.EulerZ);
        var positions = new float[count * 3];
        float[] min = { float.MaxValue, float.MaxValue, float.MaxValue };
        float[] max = { float.MinValue, float.MinValue, float.MinValue };
        for (int i = 0; i < count; i++)
        {
            Rotate(r, data.Positions, i * 3, positions, i * 3, settings.Scale);
            for (int axis = 0; axis < 3; axis++)
            {
                float v = positions[i * 3 + axis];
                if (v < min[axis]) min[axis] = v;
                if (v > max[axis]) max[axis] = v;
            }
        }

        float[] shift = PivotShift(settings.Pivot, min, max);
        shift[0] += settings.OffsetX;
        shift[1] += settings.OffsetY;
        shift[2] += settings.OffsetZ;
        for (int i = 0; i < count; i++)
        {
            positions[i * 3] += shift[0];
            positions[i * 3 + 1] += shift[1];
            positions[i * 3 + 2] += shift[2];
        }

        var result = new MeshArrays { Positions = positions, Indices = new int[data.SubMeshes.Count][] };
        for (int axis = 0; axis < 3; axis++)
        {
            result.Min[axis] = count == 0 ? 0f : min[axis] + shift[axis];
            result.Max[axis] = count == 0 ? 0f : max[axis] + shift[axis];
        }
        for (int s = 0; s < data.SubMeshes.Count; s++)
            result.Indices[s] = data.SubMeshes[s].Triangles.ToArray();

        if (data.Uvs.Length == count * 2)
            result.Uvs = data.Uvs;

        if (data.Normals.Length == count * 3)
        {
            result.Normals = new float[count * 3];
            for (int i = 0; i < count; i++)
                Rotate(r, data.Normals, i * 3, result.Normals, i * 3, 1f);
        }
        else
        {
            result.Normals = SmoothNormals(positions, result.Indices);
        }
        return result;
    }

    /// <summary>Rotation matrix (row major, 3x3) for Unity's Euler order: Z, then X, then Y, in degrees.</summary>
    public static float[] Rotation(float x, float y, float z)
    {
        const float ToRad = (float)(Math.PI / 180.0);
        float sx = (float)Math.Sin(x * ToRad), cx = (float)Math.Cos(x * ToRad);
        float sy = (float)Math.Sin(y * ToRad), cy = (float)Math.Cos(y * ToRad);
        float sz = (float)Math.Sin(z * ToRad), cz = (float)Math.Cos(z * ToRad);
        return new[]
        {
            cy * cz + sy * sx * sz, -cy * sz + sy * sx * cz, sy * cx,
            cx * sz, cx * cz, -sx,
            -sy * cz + cy * sx * sz, sy * sz + cy * sx * cz, cy * cx,
        };
    }

    private static void Rotate(float[] m, float[] from, int at, float[] to, int toAt, float scale)
    {
        float x = from[at], y = from[at + 1], z = from[at + 2];
        to[toAt] = (m[0] * x + m[1] * y + m[2] * z) * scale;
        to[toAt + 1] = (m[3] * x + m[4] * y + m[5] * z) * scale;
        to[toAt + 2] = (m[6] * x + m[7] * y + m[8] * z) * scale;
    }

    private static float[] PivotShift(ModelPivot pivot, float[] min, float[] max)
    {
        float cx = (min[0] + max[0]) * 0.5f, cy = (min[1] + max[1]) * 0.5f, cz = (min[2] + max[2]) * 0.5f;
        switch (pivot)
        {
            case ModelPivot.Center: return new[] { -cx, -cy, -cz };
            case ModelPivot.BottomCenter: return new[] { -cx, -min[1], -cz };
            default: return new float[3];
        }
    }

    private static void Add(float[] normals, int at, float x, float y, float z)
    {
        normals[at] += x;
        normals[at + 1] += y;
        normals[at + 2] += z;
    }

    /// <summary>Area-weighted vertex normals; vertices that share no triangle get up.</summary>
    public static float[] SmoothNormals(float[] positions, int[][] indices)
    {
        var normals = new float[positions.Length];
        foreach (int[] tris in indices)
        {
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                int a = tris[t] * 3, b = tris[t + 1] * 3, c = tris[t + 2] * 3;
                float ux = positions[b] - positions[a], uy = positions[b + 1] - positions[a + 1], uz = positions[b + 2] - positions[a + 2];
                float vx = positions[c] - positions[a], vy = positions[c + 1] - positions[a + 1], vz = positions[c + 2] - positions[a + 2];
                float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                Add(normals, a, nx, ny, nz);
                Add(normals, b, nx, ny, nz);
                Add(normals, c, nx, ny, nz);
            }
        }
        for (int i = 0; i < normals.Length; i += 3)
        {
            float length = (float)Math.Sqrt(normals[i] * normals[i] + normals[i + 1] * normals[i + 1] + normals[i + 2] * normals[i + 2]);
            if (length < 1e-12f)
            {
                normals[i] = 0f;
                normals[i + 1] = 1f;
                normals[i + 2] = 0f;
            }
            else
            {
                normals[i] /= length;
                normals[i + 1] /= length;
                normals[i + 2] /= length;
            }
        }
        return normals;
    }
}
