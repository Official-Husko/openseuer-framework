// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Player.Look;
using UnityEngine;

namespace ObenFramework.Npcs;

public sealed partial class NpcClaim
{
    // Everything the claim changed on the body, so release can put it back in reverse order.
    private readonly List<Action> _undo = new List<Action>();
    private readonly HashSet<string> _touched = new HashSet<string>();

    private Appearance _dressed;
    private Character _ownCharacter;

    /// <summary>
    /// Dresses the NPC in a look (sliders and worn items) for as long as the claim lasts. Only NPCs whose body
    /// has character editor sliders can be dressed; it never touches the game's shared character assets (see
    /// <see cref="PlayerLook.Apply"/>). False for a body without sliders.
    /// </summary>
    public bool ApplyLook(LookData look)
    {
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        if (!IsValid)
            return false;
        var appearance = _npc.GetComponentInChildren<Appearance>(true);
        if (appearance == null)
            return false;
        if (_dressed == null)
        {
            _dressed = appearance;
            _ownCharacter = appearance.createdCharacter;
        }
        return PlayerLook.Apply(appearance, look);
    }

    /// <summary>
    /// Sets a blend shape weight on the NPC's meshes named <paramref name="mesh"/> until the claim ends.
    /// False when the NPC has no such mesh and shape.
    /// </summary>
    public bool SetBlendShape(string mesh, string shape, float weight)
    {
        if (!IsValid || string.IsNullOrEmpty(mesh) || string.IsNullOrEmpty(shape) || !NpcWalkMath.Finite(weight))
            return false;
        weight = Mathf.Clamp(weight, -ModNpcLooks.MaxShapeWeight, ModNpcLooks.MaxShapeWeight);
        bool found = false;
        foreach (var renderer in Renderers(mesh))
        {
            var skinned = renderer as SkinnedMeshRenderer;
            int index = skinned != null && skinned.sharedMesh != null ? skinned.sharedMesh.GetBlendShapeIndex(shape) : -1;
            if (index < 0)
                continue;
            if (_touched.Add("shape:" + skinned.GetInstanceID() + ":" + index))
            {
                float before = skinned.GetBlendShapeWeight(index);
                _undo.Add(() => { if (skinned != null) skinned.SetBlendShapeWeight(index, before); });
            }
            skinned.SetBlendShapeWeight(index, weight);
            found = true;
        }
        return found;
    }

    /// <summary>Shows or hides the NPC's meshes named <paramref name="mesh"/> (a hat, a beard, a jacket) until the claim ends.</summary>
    public bool SetMeshVisible(string mesh, bool visible)
    {
        if (!IsValid || string.IsNullOrEmpty(mesh))
            return false;
        bool found = false;
        foreach (var renderer in Renderers(mesh))
        {
            var go = renderer.gameObject;
            if (_touched.Add("active:" + go.GetInstanceID()))
            {
                bool before = go.activeSelf;
                _undo.Add(() => { if (go != null) go.SetActive(before); });
            }
            go.SetActive(visible);
            found = true;
        }
        return found;
    }

    /// <summary>
    /// Gives the NPC's meshes named <paramref name="mesh"/> other materials until the claim ends. The count
    /// must match the mesh's material slots. Materials can come from the game or from a mod's assets.
    /// </summary>
    public bool SetMaterials(string mesh, params Material[] materials)
    {
        if (!IsValid || string.IsNullOrEmpty(mesh) || materials == null || materials.Length == 0)
            return false;
        var targets = Renderers(mesh);
        foreach (var renderer in targets)
        {
            if (renderer.sharedMaterials.Length != materials.Length)
                return false;
        }
        foreach (var renderer in targets)
        {
            var target = renderer;
            if (_touched.Add("materials:" + target.GetInstanceID()))
            {
                var before = target.sharedMaterials;
                _undo.Add(() => { if (target != null) target.sharedMaterials = before; });
            }
            target.sharedMaterials = (Material[])materials.Clone();
        }
        return targets.Count > 0;
    }

    /// <summary>The NPC's meshes: their names, whether they show, their materials and blend shapes.</summary>
    public List<NpcMesh> Meshes() => IsValid ? NpcMesh.Of(_npc.gameObject) : new List<NpcMesh>();

    /// <summary>Puts back the look, blend shapes, meshes and materials this claim changed. The claim's end does the same.</summary>
    public void RestoreAppearance()
    {
        for (int i = _undo.Count - 1; i >= 0; i--)
        {
            try { _undo[i](); }
            catch (Exception e) { NpcsLog.Failed("Restoring an NPC's appearance", e); }
        }
        _undo.Clear();
        _touched.Clear();

        if (_dressed == null)
            return;
        try
        {
            PlayerLook.Release(_dressed);
            _dressed.createdCharacter = _ownCharacter;
            _dressed.LoadCharacter();
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Restoring an NPC's look", e);
        }
        _dressed = null;
        _ownCharacter = null;
    }

    private List<Renderer> Renderers(string mesh)
    {
        var result = new List<Renderer>();
        foreach (var renderer in _npc.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && renderer.gameObject.name == mesh)
                result.Add(renderer);
        }
        return result;
    }
}
