// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.SceneLook;

/// <summary>A character look found in a scene file: a body and what is known about it without loading it.</summary>
public sealed class SceneLookInfo
{
    /// <summary>The scene's name, as in the build settings.</summary>
    public string Scene { get; internal set; }

    /// <summary>The file the scene is stored in (<c>levelN</c>).</summary>
    public string Level { get; internal set; }

    /// <summary>The body object's name.</summary>
    public string Name { get; internal set; }

    /// <summary>The body object's hierarchy path in the scene.</summary>
    public string Path { get; internal set; }

    /// <summary>How many skinned renderers make up the look.</summary>
    public int Renderers { get; internal set; }

    public int Bones { get; internal set; }

    /// <summary>"Bip001" (the game's characters), "Blender-style", "other" or "no bones".</summary>
    public string RigKind { get; internal set; }

    /// <summary>"humanoid", "generic" or "none": the body's animator avatar.</summary>
    public string AvatarKind { get; internal set; }

    public bool HasAnimator { get; internal set; }

    /// <summary>The body is an NPC (it has the game's NPCInfo).</summary>
    public bool IsNpc { get; internal set; }

    /// <summary>Every renderer can be rebuilt from the files; a look that isn't clean won't load.</summary>
    public bool Clean { get; internal set; }

    /// <summary>The NPC is linked to one of the game's characters.</summary>
    public bool HasCharacter { get; internal set; }

    public int CharacterId { get; internal set; }
    public string CharacterName { get; internal set; }

    /// <summary>Why the look isn't clean, when it isn't.</summary>
    public IReadOnlyList<string> Problems { get; internal set; } = new string[0];

    internal long Root { get; set; }
}

/// <summary>
/// A look loaded from a scene file, ready to be put on a body with <see cref="SceneLooks.Apply"/>. Looks
/// are shared: loading the same one twice gives the same object. It stays loaded until every mod that
/// loaded it has released it or unloaded.
/// </summary>
public sealed class LoadedSceneLook
{
    public string Scene { get; internal set; }
    public string Path { get; internal set; }
    public string Name { get; internal set; }

    /// <summary>The look is loaded and can be applied.</summary>
    public bool Ready { get; internal set; }

    /// <summary>Loading failed; <see cref="Reason"/> says why.</summary>
    public bool Failed { get; internal set; }

    public string Reason { get; internal set; }
    public int MeshCount { get; internal set; }
    public int VertexCount { get; internal set; }
    public IReadOnlyList<string> BoneNames { get; internal set; } = new string[0];

    internal SceneLookLook Look { get; set; }
    internal string Key { get; set; }
}
