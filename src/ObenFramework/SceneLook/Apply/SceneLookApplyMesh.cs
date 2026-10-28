// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace ObenFramework.SceneLook;

/// <summary>Builds Unity meshes from serialized mesh data and converts the planning matrices.</summary>
internal static class SceneLookApplyMesh
{
    public static Matrix4x4[] FileBindPoses(SceneLookMesh source)
    {
        if (source?.BindPoses == null)
            return null;
        var poses = new Matrix4x4[source.BindPoses.Length];
        for (int i = 0; i < poses.Length; i++)
            poses[i] = ToUnity(SceneLookMatrix.FromFile(source.BindPoses[i]));
        return poses;
    }

    public static Matrix4x4 FilePose(SceneLookMesh source, int index)
    {
        if (source?.BindPoses == null || index < 0 || index >= source.BindPoses.Length)
            return Matrix4x4.identity;
        return ToUnity(SceneLookMatrix.FromFile(source.BindPoses[index]));
    }

    public static SceneLookMatrix FromUnity(Matrix4x4 m) => new SceneLookMatrix
    {
        M00 = m.m00, M01 = m.m01, M02 = m.m02, M03 = m.m03,
        M10 = m.m10, M11 = m.m11, M12 = m.m12, M13 = m.m13,
        M20 = m.m20, M21 = m.m21, M22 = m.m22, M23 = m.m23,
        M30 = m.m30, M31 = m.m31, M32 = m.m32, M33 = m.m33,
    };

    public static Matrix4x4 ToUnity(SceneLookMatrix m) => new Matrix4x4
    {
        m00 = m.M00, m01 = m.M01, m02 = m.M02, m03 = m.M03,
        m10 = m.M10, m11 = m.M11, m12 = m.M12, m13 = m.M13,
        m20 = m.M20, m21 = m.M21, m22 = m.M22, m23 = m.M23,
        m30 = m.M30, m31 = m.M31, m32 = m.M32, m33 = m.M33,
    };

    /// <summary>
    /// A mesh with the file's vertex and index buffers uploaded as they are (same attribute layout, same
    /// submeshes), and the given bind poses. Null with the reason when the data doesn't add up.
    /// </summary>
    public static Mesh Build(SceneLookMesh source, Matrix4x4[] bindPoses, out string reason)
    {
        reason = null;
        if (source == null)
        {
            reason = "no mesh";
            return null;
        }
        if (source.VertexData == null || source.IndexData == null)
        {
            reason = "mesh buffers are not loaded";
            return null;
        }
        if (source.VertexCount == 0 || source.VertexCount > int.MaxValue)
        {
            reason = "mesh vertex count " + source.VertexCount + " is not usable";
            return null;
        }
        long indexStride = source.IndexFormat == 0 ? 2 : 4;
        long indexCount = SceneLookMeshReader.IndexTotal(source);
        if (source.IndexDataSize != indexCount * indexStride)
        {
            reason = "mesh index bytes do not match the index count";
            return null;
        }
        var streams = SceneLookMeshPlan.Plan(source, out reason);
        if (streams == null)
            return null;

        var attributes = SceneLookMeshPlan.Attributes(source);
        var descriptors = new VertexAttributeDescriptor[attributes.Count];
        for (int i = 0; i < attributes.Count; i++)
        {
            var channel = source.Channels[attributes[i]];
            descriptors[i] = new VertexAttributeDescriptor((VertexAttribute)attributes[i], (VertexAttributeFormat)channel.Format, channel.Dimension, channel.Stream);
        }

        var mesh = new Mesh { name = source.Name };
        try
        {
            mesh.SetVertexBufferParams((int)source.VertexCount, descriptors);
            foreach (var stream in streams)
                mesh.SetVertexBufferData(source.VertexData, stream.Start, 0, (int)(source.VertexCount * stream.Stride), stream.Stream);
            if (indexCount > 0 && !Indices(mesh, source, indexCount, indexStride, out reason))
            {
                Object.Destroy(mesh);
                return null;
            }
            if (bindPoses != null)
                mesh.bindposes = bindPoses;
            mesh.RecalculateBounds();
            return mesh;
        }
        catch (Exception e)
        {
            Object.Destroy(mesh);
            reason = e.Message;
            return null;
        }
    }

    private static bool Indices(Mesh mesh, SceneLookMesh source, long indexCount, long indexStride, out string reason)
    {
        reason = null;
        mesh.indexFormat = source.IndexFormat == 0 ? IndexFormat.UInt16 : IndexFormat.UInt32;
        mesh.SetIndexBufferParams((int)indexCount, mesh.indexFormat);
        if (source.IndexFormat == 0)
        {
            var indices = new ushort[(int)indexCount];
            Buffer.BlockCopy(source.IndexData, 0, indices, 0, (int)(indexCount * 2));
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
        }
        else
        {
            var indices = new uint[(int)indexCount];
            Buffer.BlockCopy(source.IndexData, 0, indices, 0, (int)(indexCount * 4));
            mesh.SetIndexBufferData(indices, 0, 0, indices.Length);
        }

        mesh.subMeshCount = source.Submeshes.Count;
        for (int i = 0; i < source.Submeshes.Count; i++)
        {
            var submesh = source.Submeshes[i];
            if (submesh.FirstByte % indexStride != 0)
            {
                reason = "submesh " + i + " does not start on an index";
                return false;
            }
            long start = submesh.FirstByte / indexStride;
            if (start + submesh.IndexCount > indexCount)
            {
                reason = "submesh " + i + " is past the index buffer";
                return false;
            }
            var descriptor = new SubMeshDescriptor((int)start, (int)submesh.IndexCount, (MeshTopology)submesh.Topology)
            {
                baseVertex = (int)submesh.BaseVertex,
                firstVertex = (int)submesh.FirstVertex,
                vertexCount = (int)submesh.VertexCount,
            };
            mesh.SetSubMesh(i, descriptor);
        }
        return true;
    }
}
