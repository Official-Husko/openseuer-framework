// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>Unity's class ids for the object types the reader understands.</summary>
internal static class SceneLookClass
{
    public const int GameObject = 1;
    public const int Transform = 4;
    public const int Texture2D = 28;
    public const int Mesh = 43;
    public const int Shader = 48;
    public const int Animator = 95;
    public const int MonoBehaviour = 114;
    public const int SkinnedMeshRenderer = 137;
}

/// <summary>A serialized reference: which file (0 is the same file, n is external file n-1) and which object.</summary>
internal struct SceneLookRef
{
    public int FileId;
    public long PathId;

    public SceneLookRef(int fileId, long pathId)
    {
        FileId = fileId;
        PathId = pathId;
    }

    public bool IsNull => PathId == 0;
}

/// <summary>Parsed objects of one file, kept so a scan reads each object once.</summary>
internal sealed class SceneLookMemo
{
    public readonly Dictionary<long, SceneLookGameObject> GameObjects = new Dictionary<long, SceneLookGameObject>();
    public readonly Dictionary<long, SceneLookTransform> Transforms = new Dictionary<long, SceneLookTransform>();
    public readonly Dictionary<long, HashSet<string>> Kinds = new Dictionary<long, HashSet<string>>();
    public readonly Dictionary<long, string> BehaviourClasses = new Dictionary<long, string>();
    public readonly Dictionary<long, string> ScriptClasses = new Dictionary<long, string>();
    public readonly Dictionary<long, string> ShaderLabels = new Dictionary<long, string>();
}

internal sealed class SceneLookGameObject
{
    public long PathId;
    public string Name;
    public List<SceneLookRef> Components;
    public bool Active = true;
}

internal sealed class SceneLookTransform
{
    public long PathId;
    public SceneLookRef GameObject;

    /// <summary>Local rotation as x, y, z, w.</summary>
    public float[] Rotation;

    public float[] Position;
    public float[] Scale;
    public SceneLookRef Father;
    public List<SceneLookRef> Children;
}

/// <summary>The header every MonoBehaviour starts with.</summary>
internal sealed class SceneLookBehaviour
{
    public long PathId;
    public SceneLookRef GameObject;
    public SceneLookRef Script;
    public string Name;
}

/// <summary>The serialized start of the game's <c>NPCInfo</c>, up to the character model.</summary>
internal sealed class SceneLookNpcInfo
{
    public long PathId;
    public SceneLookRef GameObject;
    public string Guid;
    public bool Global;
    public byte[] SavedTimeAndDay;
    public SceneLookRef Character;
    public SceneLookRef CharacterModel;
}

/// <summary>The serialized start of the game's <c>Character</c> asset, up to its prefabs.</summary>
internal sealed class SceneLookCharacter
{
    public int Id;
    public string Name;
    public string LastName;
    public string GenderString;
    public int Age;
    public string Relationship;
    public string Job;
    public string AssetName;
    public string Addressable;
    public SceneLookRef Sprite;
    public SceneLookRef Prefab;
    public SceneLookRef PrefabWithAi;
}

internal sealed class SceneLookAnimator
{
    public long PathId;
    public SceneLookRef Avatar;
    public SceneLookRef Controller;
}

internal sealed class SceneLookAvatarInfo
{
    /// <summary>"humanoid", "generic", "none" or why the avatar couldn't be read.</summary>
    public string Kind;

    public int Roles;
    public string ControllerName;
}
