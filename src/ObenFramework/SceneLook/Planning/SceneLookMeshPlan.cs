// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>One vertex stream of a mesh: where it starts in the vertex buffer and its stride.</summary>
internal sealed class SceneLookMeshStream
{
    public int Stream;
    public int Start;
    public int Stride;
}

/// <summary>
/// Lays out a serialized mesh's vertex buffer for <c>Mesh.SetVertexBufferData</c>: the attributes in
/// channel order (channel n is <c>VertexAttribute</c> n) and the streams with their starts and strides.
/// </summary>
internal static class SceneLookMeshPlan
{
    public const int AttributeLimit = 14;
    public const int FormatLimit = 12;

    /// <summary>The streams, or null with the reason when the layout isn't the plain one Unity writes.</summary>
    public static List<SceneLookMeshStream> Plan(SceneLookMesh mesh, out string reason)
    {
        reason = null;
        if (mesh.Channels == null)
        {
            reason = "mesh has no channel list";
            return null;
        }
        var streams = new List<SceneLookMeshStream>();
        var known = new Dictionary<int, SceneLookMeshStream>();
        var offsets = new Dictionary<int, int>();
        for (int i = 0; i < mesh.Channels.Count; i++)
        {
            var channel = mesh.Channels[i];
            if (channel.Dimension == 0)
                continue;
            if (i >= AttributeLimit)
            {
                reason = "mesh channel " + i + " is past the attribute limit";
                return null;
            }
            if (channel.Format < 0 || channel.Format >= FormatLimit)
            {
                reason = "vertex format " + channel.Format + " is not handled";
                return null;
            }
            if (!known.TryGetValue(channel.Stream, out var stream))
            {
                stream = new SceneLookMeshStream { Stream = channel.Stream };
                known[channel.Stream] = stream;
                offsets[channel.Stream] = 0;
                streams.Add(stream);
            }
            int offset = offsets[channel.Stream];
            if (offset != channel.Offset)
            {
                reason = "mesh channel offsets are not the layout order";
                return null;
            }
            int size = channel.Dimension * SceneLookMeshReader.VertexFormatSizes[channel.Format];
            offsets[channel.Stream] = offset + size;
            stream.Stride = offset + size;
        }
        if (streams.Count == 0)
        {
            reason = "mesh has no vertex channels";
            return null;
        }

        long start = 0;
        for (int i = 0; i < streams.Count; i++)
        {
            streams[i].Start = (int)start;
            long size = mesh.VertexCount * streams[i].Stride;
            start += i < streams.Count - 1 ? (size + 15) / 16 * 16 : size;
        }
        if (start != mesh.VertexDataSize)
        {
            reason = "mesh vertex bytes are " + mesh.VertexDataSize + " and the layout needs " + start;
            return null;
        }
        return streams;
    }

    /// <summary>The channel indexes that carry data, which are also the <c>VertexAttribute</c> values.</summary>
    public static List<int> Attributes(SceneLookMesh mesh)
    {
        var attributes = new List<int>();
        for (int i = 0; i < mesh.Channels.Count; i++)
        {
            if (mesh.Channels[i].Dimension != 0)
                attributes.Add(i);
        }
        return attributes;
    }
}
