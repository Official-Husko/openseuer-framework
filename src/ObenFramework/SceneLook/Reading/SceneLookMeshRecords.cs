// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

internal sealed class SceneLookSubmesh
{
    public uint FirstByte;
    public uint IndexCount;
    public int Topology;
    public uint BaseVertex;
    public uint FirstVertex;
    public uint VertexCount;
}

/// <summary>One vertex attribute: which stream it lives in, at what offset, in what format and how many components.</summary>
internal sealed class SceneLookChannel
{
    public int Stream;
    public int Offset;
    public int Format;
    public int Dimension;
}

internal sealed class SceneLookBlendShape
{
    public uint FirstVertex;
    public uint VertexCount;
    public bool HasNormals;
    public bool HasTangents;
}

internal sealed class SceneLookBlendShapeChannel
{
    public int Hash;
    public int FirstFrame;
    public int Frames;
}

/// <summary>A mesh as serialized. The buffers are only filled when the mesh is loaded to be built.</summary>
internal sealed class SceneLookMesh
{
    public long PathId;
    public string FileName;
    public string Name;
    public List<SceneLookSubmesh> Submeshes;
    public uint VertexCount;
    public List<SceneLookChannel> Channels;
    public List<string> BlendShapeNames;
    public List<SceneLookBlendShape> BlendShapes;
    public List<SceneLookBlendShapeChannel> BlendShapeChannels;
    public float[] BlendShapeFrameWeights;
    public int BlendShapeVertexCount;

    /// <summary>One 4x4 matrix per bone, row by row as Unity writes them.</summary>
    public float[][] BindPoses;

    /// <summary>0 for 16-bit indices, 1 for 32-bit.</summary>
    public int IndexFormat;

    public long IndexDataSize;
    public long VertexDataSize;
    public long ExpectedVertexDataSize;
    public byte[] VertexData;
    public byte[] IndexData;
    public byte[] BlendShapeVertexData;
}

internal sealed class SceneLookRenderer
{
    public long PathId;
    public SceneLookRef GameObject;
    public SceneLookRef Mesh;
    public List<SceneLookRef> Materials;
    public List<SceneLookRef> Bones;
    public SceneLookRef RootBone;
    public float[] BlendShapeWeights;
}

internal sealed class SceneLookNamed<T>
{
    public string Name;
    public T Value;
}

internal sealed class SceneLookTexEnv
{
    public string Name;
    public SceneLookRef Texture;
    public SceneLookTexture Resolved;
    public float[] Scale;
    public float[] Offset;
}

internal sealed class SceneLookMaterial
{
    public long PathId;
    public string FileName;
    public string Name;
    public SceneLookRef Shader;
    public int RenderQueue;
    public List<SceneLookTexEnv> TexEnvs;
    public List<SceneLookNamed<int>> Ints;
    public List<SceneLookNamed<float>> Floats;
    public List<SceneLookNamed<float[]>> Colors;
}

/// <summary>A texture's header and where its pixels are: inside the object or in a <c>.resS</c>/<c>.resource</c> stream.</summary>
internal sealed class SceneLookTexture
{
    public long PathId;
    public string FileName;
    public string Name;
    public int Width;
    public int Height;
    public int Format;
    public int Mips;
    public long CompleteSize;
    public bool Inline;
    public bool InlineValid;
    public string StreamPath;
    public long StreamOffset;
    public long StreamSize;
}
