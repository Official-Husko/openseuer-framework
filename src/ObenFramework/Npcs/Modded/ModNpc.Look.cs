using System;
using System.Collections.Generic;
using ObenFramework.Player.Look;
using UnityEngine;

namespace ObenFramework.Npcs;

public sealed partial class ModNpc
{
    private LookData _look;
    private readonly List<ModNpcShape> _shapes = new List<ModNpcShape>();

    /// <summary>
    /// The look the mod gave it (<see cref="ModNpcSpec.Look"/> or <see cref="SetLook"/>), else what its
    /// slider body shows now. Null for a body without sliders. A copy: change it and pass it to SetLook.
    /// </summary>
    public LookData Look => _look != null ? _look.Clone() : Loaded ? ModNpcLooks.Capture(Root) : null;

    /// <summary>The look was set by the mod rather than taken from the template.</summary>
    public bool HasOwnLook => _look != null;

    /// <summary>Blend shapes the mod set, in the order they were set.</summary>
    public IReadOnlyList<ModNpcShape> BlendShapes => _shapes;

    /// <summary>
    /// Dresses the NPC in <paramref name="look"/> (sliders and worn items) and rebuilds its body. Only for
    /// bodies with sliders (<see cref="HasSliders"/>). A saved NPC keeps the look in the save.
    /// </summary>
    public bool SetLook(LookData look)
    {
        if (look == null)
            throw new ArgumentNullException(nameof(look));
        if (!Loaded || !HasSliders)
            return false;
        ModNpcLooks.Write(Clone, look);
        ModNpcLooks.Rebuild(Root);
        _look = look.Clone();
        return true;
    }

    /// <summary>
    /// Sets a blend shape weight (up to 200 either way) on one of the body's meshes (a child renderer named
    /// <paramref name="mesh"/>). Mostly for bodies without sliders, whose look is their blend shapes; a slider
    /// body recomputes its shapes when its look changes. False when there is no such mesh or shape.
    /// </summary>
    public bool SetBlendShape(string mesh, string shape, float weight)
    {
        if (string.IsNullOrEmpty(mesh) || string.IsNullOrEmpty(shape) || !NpcWalkMath.Finite(weight) || !Loaded)
            return false;
        weight = Mathf.Clamp(weight, -ModNpcLooks.MaxShapeWeight, ModNpcLooks.MaxShapeWeight);
        if (!ModNpcLooks.SetShape(Root, Clone, mesh, shape, weight))
            return false;
        _shapes.RemoveAll(s => s.Mesh == mesh && s.Shape == shape);
        if (_shapes.Count < ModNpcSaveFile.MaxShapes)
            _shapes.Add(new ModNpcShape { Mesh = mesh, Shape = shape, Weight = weight });
        return true;
    }

    /// <summary>The body's meshes and the blend shapes each has, with their current weights: what <see cref="SetBlendShape"/> accepts.</summary>
    public List<ModNpcShape> AvailableShapes()
    {
        var result = new List<ModNpcShape>();
        if (!Loaded)
            return result;
        foreach (var renderer in Root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            var mesh = renderer != null ? renderer.sharedMesh : null;
            if (mesh == null)
                continue;
            for (int i = 0; i < mesh.blendShapeCount; i++)
                result.Add(new ModNpcShape { Mesh = renderer.gameObject.name, Shape = mesh.GetBlendShapeName(i), Weight = renderer.GetBlendShapeWeight(i) });
        }
        return result;
    }

    /// <summary>The body's meshes with their materials and blend shape names.</summary>
    public List<NpcMesh> Meshes() => Loaded ? NpcMesh.Of(Root) : new List<NpcMesh>();

    /// <summary>Puts the template's look and blend shapes back and forgets the mod's.</summary>
    public bool ResetLook()
    {
        if (!Loaded)
            return false;
        ModNpcLooks.Reset(Clone, TemplateCharacter);
        ModNpcLooks.Rebuild(Root);
        _look = null;
        _shapes.Clear();
        return true;
    }

    // The look and shapes it was built with (from the spec or the save).
    internal void StartWith(LookData look, List<ModNpcShape> shapes)
    {
        _look = look != null && HasSliders ? look.Clone() : null;
        _shapes.Clear();
        if (shapes != null)
            _shapes.AddRange(ModNpcSaveFile.CleanShapes(shapes));
    }

    internal string LookJson => _look != null ? _look.ToJson() : null;

    internal List<ModNpcShape> ShapesCopy() => ModNpcSaveFile.CleanShapes(_shapes);
}
