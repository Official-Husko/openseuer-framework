// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>One transform of a look's hierarchy with its file-local pose.</summary>
internal sealed class SceneLookNode
{
    public long GameObject;
    public long Transform;
    public string Name;
    public SceneLookRef Father;
    public List<SceneLookRef> Children;
    public float[] Rotation;
    public float[] Position;
    public float[] Scale;
}

internal sealed class SceneLookBone
{
    public string Name;
    public SceneLookRef Transform;
}

/// <summary>A skinned renderer of a look with everything it needs resolved: mesh, materials, bones.</summary>
internal sealed class SceneLookLookRenderer
{
    public string Name;
    public SceneLookRenderer Source;
    public SceneLookMesh Mesh;
    public List<SceneLookMaterial> Materials;
    public List<SceneLookBone> Bones;
    public SceneLookBone RootBone;
}

/// <summary>A character look loaded from a scene file, ready to be built.</summary>
internal sealed class SceneLookLook
{
    public string FileName;
    public long Root;
    public List<SceneLookNode> Nodes;
    public List<SceneLookLookRenderer> Renderers;
    public List<SceneLookTexture> Textures;
    public SceneLookFiles Files;
}

/// <summary>A body found by a scan: the object its skinned renderers group under, and what was found about it.</summary>
internal sealed class SceneLookRoot
{
    public long GameObject;
    public string Name;
    public string Path;
    public bool IsNpc;
    public bool HasAnimator;
    public int Renderers;
    public List<string> BoneNames;
    public string Rig;
    public string Avatar;
    public List<SceneLookAvatarInfo> Avatars;
    public long Vertices;
    public long Triangles;
    public bool Clean;
    public List<string> Reasons;
    public List<int> Formats;
    public List<string> Shaders;
    public List<string> Hierarchy;
    public SceneLookCharacter Character;
    public string CharacterNote;
}

/// <summary>An <c>NPCInfo</c> found by a scan, and the body it was matched with (0 when none).</summary>
internal sealed class SceneLookNpc
{
    public long PathId;
    public long GameObject;
    public string Name;
    public string Path;
    public SceneLookCharacter Character;
    public string CharacterNote;
    public long ModelGameObject;
    public long Body;
}

/// <summary>Everything one scan of a scene file found, with counts for diagnostics.</summary>
internal sealed class SceneLookFileScan
{
    public string FileName;
    public string Error;
    public double Seconds;
    public int Renderers;
    public int CleanRenderers;
    public SceneLookCounter<string> ReasonCounts = new SceneLookCounter<string>();
    public int Unrooted;
    public SceneLookCounter<string> UnrootedRigs = new SceneLookCounter<string>();
    public SceneLookCounter<int> FormatsUsed = new SceneLookCounter<int>();
    public SceneLookCounter<string> ShadersUsed = new SceneLookCounter<string>();
    public SceneLookCounter<int> TextureFormats = new SceneLookCounter<int>();
    public SceneLookCounter<string> ShaderObjects = new SceneLookCounter<string>();
    public List<SceneLookRoot> Roots = new List<SceneLookRoot>();
    public List<SceneLookNpc> Npcs = new List<SceneLookNpc>();
}

/// <summary>Counts per key, keeping the order keys were first seen.</summary>
internal sealed class SceneLookCounter<T>
{
    private readonly List<T> _keys = new List<T>();
    private readonly Dictionary<T, int> _counts = new Dictionary<T, int>();

    public void Add(T key, int amount = 1)
    {
        if (_counts.TryGetValue(key, out int count))
        {
            _counts[key] = count + amount;
            return;
        }
        _counts[key] = amount;
        _keys.Add(key);
    }

    public int this[T key] => _counts.TryGetValue(key, out int count) ? count : 0;

    public IList<T> Keys => _keys;
}
