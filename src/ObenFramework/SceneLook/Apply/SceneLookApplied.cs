// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.SceneLook;

/// <summary>The textures and materials built for one look, shared by every body wearing it.</summary>
internal sealed class SceneLookAssets
{
    public SceneLookFiles Files;
    public SceneLookLook Look;

    /// <summary>How many bodies wear the look; the assets go when it drops to zero.</summary>
    public int Bodies;

    public readonly Dictionary<string, Texture2D> Textures = new Dictionary<string, Texture2D>();
    public readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
}

/// <summary>A renderer of the body that was switched off for the look, and whether it was on.</summary>
internal sealed class SceneLookHidden
{
    public SkinnedMeshRenderer Renderer;
    public bool Enabled;
}

/// <summary>Everything one applied look made or changed on one body, so it can all be undone.</summary>
internal sealed class SceneLookApplied
{
    public string ModId;
    public GameObject Body;
    public SceneLookLook Look;
    public GameObject Holder;
    public Avatar Avatar;
    public SceneLookPoseDriver Driver;
    public SceneLookRigPath Path;
    public readonly List<GameObject> Objects = new List<GameObject>();
    public readonly List<Mesh> Meshes = new List<Mesh>();
    public readonly List<SceneLookHidden> Hidden = new List<SceneLookHidden>();
}
