using System;
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>One frame of a blend shape: its channel name, the weight it is reached at and its vertices.</summary>
internal sealed class SceneLookShapeFrame
{
    public int Channel;
    public string Name;
    public float Weight;

    /// <summary>First vertex of the frame in the mesh's blend shape vertex list.</summary>
    public int First;

    public int Count;
    public bool HasNormals;
    public bool HasTangents;
}

/// <summary>
/// Turns a mesh's serialized blend shapes into frames for <c>Mesh.AddBlendShapeFrame</c>. Unity keeps
/// blend shapes sparse: one list of changed vertices (position, normal and tangent delta and the vertex
/// index, 40 bytes each), frames that are ranges of that list, and channels (the named shapes) that are
/// ranges of frames.
/// </summary>
internal static class SceneLookBlendShapes
{
    public const int VertexBytes = 40;

    /// <summary>Every frame in channel order, or null with the reason when the data doesn't add up.</summary>
    public static List<SceneLookShapeFrame> Frames(SceneLookMesh mesh, out string reason)
    {
        reason = null;
        var frames = new List<SceneLookShapeFrame>();
        if (mesh.BlendShapeNames == null || mesh.BlendShapeNames.Count == 0)
            return frames;
        if (mesh.BlendShapeVertexData == null || mesh.BlendShapeVertexData.Length != (long)VertexBytes * mesh.BlendShapeVertexCount)
        {
            reason = "blend shape vertices are not loaded";
            return null;
        }
        if (mesh.BlendShapeFrameWeights == null || mesh.BlendShapeFrameWeights.Length != mesh.BlendShapes.Count)
        {
            reason = "blend shape frame weights don't match the frames";
            return null;
        }
        for (int c = 0; c < mesh.BlendShapeChannels.Count; c++)
        {
            var channel = mesh.BlendShapeChannels[c];
            if (channel.FirstFrame < 0 || channel.Frames < 0 || (long)channel.FirstFrame + channel.Frames > mesh.BlendShapes.Count)
            {
                reason = "blend shape " + mesh.BlendShapeNames[c] + " has frames past the list";
                return null;
            }
            for (int f = channel.FirstFrame; f < channel.FirstFrame + channel.Frames; f++)
            {
                var shape = mesh.BlendShapes[f];
                if ((long)shape.FirstVertex + shape.VertexCount > mesh.BlendShapeVertexCount)
                {
                    reason = "blend shape " + mesh.BlendShapeNames[c] + " has vertices past the list";
                    return null;
                }
                frames.Add(new SceneLookShapeFrame
                {
                    Channel = c,
                    Name = mesh.BlendShapeNames[c],
                    Weight = mesh.BlendShapeFrameWeights[f],
                    First = (int)shape.FirstVertex,
                    Count = (int)shape.VertexCount,
                    HasNormals = shape.HasNormals,
                    HasTangents = shape.HasTangents,
                });
            }
        }
        return frames;
    }

    /// <summary>
    /// Writes a frame's deltas into full-size arrays of three floats per mesh vertex, which are cleared
    /// first. Returns false when a vertex index is outside the mesh.
    /// </summary>
    public static bool Fill(SceneLookMesh mesh, SceneLookShapeFrame frame, float[] positions, float[] normals, float[] tangents)
    {
        Array.Clear(positions, 0, positions.Length);
        Array.Clear(normals, 0, normals.Length);
        Array.Clear(tangents, 0, tangents.Length);
        byte[] data = mesh.BlendShapeVertexData;
        for (int k = 0; k < frame.Count; k++)
        {
            int at = (frame.First + k) * VertexBytes;
            uint index = BitConverter.ToUInt32(data, at + 36);
            if (index >= mesh.VertexCount || (long)index * 3 + 2 >= positions.Length)
                return false;
            int to = (int)index * 3;
            for (int axis = 0; axis < 3; axis++)
            {
                positions[to + axis] = BitConverter.ToSingle(data, at + axis * 4);
                if (frame.HasNormals)
                    normals[to + axis] = BitConverter.ToSingle(data, at + 12 + axis * 4);
                if (frame.HasTangents)
                    tangents[to + axis] = BitConverter.ToSingle(data, at + 24 + axis * 4);
            }
        }
        return true;
    }
}
