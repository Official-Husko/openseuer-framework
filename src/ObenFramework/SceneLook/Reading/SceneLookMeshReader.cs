// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using System.IO;

namespace ObenFramework.SceneLook;

/// <summary>Reads skinned mesh renderers and meshes as Unity 6000.0 serializes them.</summary>
internal static class SceneLookMeshReader
{
    /// <summary>Bytes per component of each <c>VertexAttributeFormat</c> (Float32 to SInt32).</summary>
    internal static readonly int[] VertexFormatSizes = { 4, 2, 1, 1, 2, 2, 1, 1, 2, 2, 4, 4 };

    public static SceneLookRenderer ParseRenderer(long pathId, byte[] bytes)
    {
        var c = new SceneLookCursor(bytes, 0);
        var renderer = new SceneLookRenderer { PathId = pathId, GameObject = c.Pptr() };
        renderer.Enabled = c.U8() != 0;
        c.Skip(59); // shadow and probe settings, lightmap data
        int materials = c.Count();
        renderer.Materials = new List<SceneLookRef>(Math.Min(materials, 64));
        for (int i = 0; i < materials; i++)
            renderer.Materials.Add(c.Pptr());
        c.U16(); // static batch first submesh
        c.U16(); // static batch submesh count
        c.Pptr(); // static batch root
        c.Pptr(); // probe anchor
        c.Pptr(); // light probe volume override
        c.I32(); // sorting layer id
        c.I16(); // sorting layer
        c.I16(); // sorting order
        c.I32(); // quality
        c.U8();  // update when offscreen
        c.U8();  // skinned motion vectors
        c.Align(4);
        renderer.Mesh = c.Pptr();
        int bones = c.Count();
        renderer.Bones = new List<SceneLookRef>(Math.Min(bones, 256));
        for (int i = 0; i < bones; i++)
            renderer.Bones.Add(c.Pptr());
        int weights = c.Count();
        renderer.BlendShapeWeights = c.Floats(weights);
        renderer.RootBone = c.Pptr();
        c.Skip(24); // AABB
        c.U8();     // dirty AABB
        c.Align(4);
        if (c.Position != bytes.Length)
            throw new InvalidDataException("renderer has " + bytes.Length + " bytes and " + c.Position + " were read");
        return renderer;
    }

    /// <summary>Reads a mesh; with <paramref name="keep"/> the vertex, index and blend shape buffers and bind poses too.</summary>
    public static SceneLookMesh ParseMesh(SceneLookFile f, long pathId, bool keep)
    {
        byte[] bytes = f.ReadObject(pathId);
        var c = new SceneLookCursor(bytes, 0);
        var mesh = new SceneLookMesh { PathId = pathId, FileName = f.Name, Name = c.Text() };

        int submeshes = c.Count();
        mesh.Submeshes = new List<SceneLookSubmesh>(Math.Min(submeshes, 64));
        for (int i = 0; i < submeshes; i++)
        {
            mesh.Submeshes.Add(new SceneLookSubmesh
            {
                FirstByte = c.U32(),
                IndexCount = c.U32(),
                Topology = c.I32(),
                BaseVertex = c.U32(),
                FirstVertex = c.U32(),
                VertexCount = c.U32(),
            });
            c.Skip(24); // local AABB
        }

        int shapeVertices = c.Count();
        mesh.BlendShapeVertexCount = shapeVertices;
        int shapeStart = c.Position;
        c.Skip(40L * shapeVertices);
        int shapes = c.Count();
        mesh.BlendShapes = new List<SceneLookBlendShape>(Math.Min(shapes, 256));
        for (int i = 0; i < shapes; i++)
        {
            var shape = new SceneLookBlendShape { FirstVertex = c.U32(), VertexCount = c.U32(), HasNormals = c.U8() != 0, HasTangents = c.U8() != 0 };
            c.Align(4);
            mesh.BlendShapes.Add(shape);
        }
        int shapeNames = c.Count();
        mesh.BlendShapeNames = new List<string>(Math.Min(shapeNames, 256));
        mesh.BlendShapeChannels = new List<SceneLookBlendShapeChannel>(Math.Min(shapeNames, 256));
        for (int i = 0; i < shapeNames; i++)
        {
            mesh.BlendShapeNames.Add(c.Text());
            mesh.BlendShapeChannels.Add(new SceneLookBlendShapeChannel { Hash = c.I32(), FirstFrame = c.I32(), Frames = c.I32() });
        }
        mesh.BlendShapeFrameWeights = c.Floats(c.Count());

        int bindPoses = c.Count();
        if ((long)bindPoses * 64 > c.Length - c.Position)
            throw new InvalidDataException("bind pose count " + bindPoses + " is past the end of the object");
        if (keep)
        {
            mesh.BindPoses = new float[bindPoses][];
            for (int i = 0; i < bindPoses; i++)
                mesh.BindPoses[i] = c.Floats(16);
        }
        else
        {
            c.Skip(64L * bindPoses);
        }
        c.Skip(4L * c.Count()); // bone name hashes
        c.U32();                // root bone name hash
        c.Skip(24L * c.Count()); // bones AABB
        c.Skip(4L * c.Count());  // variable bone count weights
        c.Align(4);
        c.Skip(4);               // mesh compression, readable, keep vertices, keep indices
        c.Align(4);
        mesh.IndexFormat = c.I32();
        int indexBytes = c.Count();
        mesh.IndexDataSize = indexBytes;
        if (keep)
            mesh.IndexData = c.Raw(indexBytes);
        else
            c.Skip(indexBytes);
        c.Align(4);

        mesh.VertexCount = c.U32();
        int channels = c.Count();
        mesh.Channels = new List<SceneLookChannel>(Math.Min(channels, 14));
        for (int i = 0; i < channels; i++)
            mesh.Channels.Add(new SceneLookChannel { Stream = c.U8(), Offset = c.U8(), Format = c.U8(), Dimension = c.U8() });
        int vertexBytes = c.Count();
        mesh.VertexDataSize = vertexBytes;
        if (keep)
            mesh.VertexData = c.Raw(vertexBytes);
        else
            c.Skip(vertexBytes);
        mesh.ExpectedVertexDataSize = ExpectedVertexBytes(mesh);

        if (keep && shapeVertices > 0)
        {
            mesh.BlendShapeVertexData = new byte[40 * shapeVertices];
            Buffer.BlockCopy(bytes, shapeStart, mesh.BlendShapeVertexData, 0, mesh.BlendShapeVertexData.Length);
        }
        return mesh;
    }

    public static long IndexTotal(SceneLookMesh mesh)
    {
        long indices = 0;
        foreach (var submesh in mesh.Submeshes)
            indices += submesh.IndexCount;
        return indices;
    }

    /// <summary>Why a mesh can't be rebuilt from its serialized data (streamed or inconsistent buffers), empty when it can.</summary>
    public static List<string> MeshReasons(SceneLookMesh mesh)
    {
        var reasons = new List<string>();
        long indices = IndexTotal(mesh);
        long indexBytes = indices * (mesh.IndexFormat == 0 ? 2 : 4);
        if (mesh.VertexDataSize == 0 && mesh.ExpectedVertexDataSize > 0)
            reasons.Add("mesh vertex data not inline");
        else if (mesh.VertexDataSize != mesh.ExpectedVertexDataSize)
            reasons.Add("mesh vertex size mismatch");
        else if (mesh.IndexDataSize == 0 && indices > 0)
            reasons.Add("mesh index data not inline");
        else if (mesh.IndexDataSize != indexBytes)
            reasons.Add("mesh index size mismatch");
        return reasons;
    }

    /// <summary>The vertex buffer size the channel layout needs: streams one after another, all but the last padded to 16 bytes.</summary>
    public static long ExpectedVertexBytes(SceneLookMesh mesh)
    {
        var streams = new List<int>();
        var strides = new Dictionary<int, long>();
        foreach (var channel in mesh.Channels)
        {
            if (channel.Dimension == 0)
                continue;
            if (channel.Format < 0 || channel.Format >= VertexFormatSizes.Length)
                throw new InvalidDataException("vertex format " + channel.Format + " is not handled");
            if (!strides.TryGetValue(channel.Stream, out long stride))
            {
                streams.Add(channel.Stream);
                stride = 0;
            }
            strides[channel.Stream] = stride + channel.Dimension * VertexFormatSizes[channel.Format];
        }
        long expected = 0;
        for (int i = 0; i < streams.Count; i++)
        {
            long size = mesh.VertexCount * strides[streams[i]];
            expected += i < streams.Count - 1 ? (size + 15) / 16 * 16 : size;
        }
        return expected;
    }
}
