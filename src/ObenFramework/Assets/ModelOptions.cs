using UnityEngine;

namespace ObenFramework.Assets;

/// <summary>The collider a model's template gets.</summary>
public enum ModelCollider
{
    /// <summary>No collider.</summary>
    None = 0,

    /// <summary>A box around the model.</summary>
    Box = 1,

    /// <summary>A convex mesh collider. Unity reduces the hull of detailed models to 255 faces.</summary>
    Convex = 2,

    /// <summary>A mesh collider that follows the model exactly. Only for things that never move.</summary>
    Mesh = 3,
}

/// <summary>How an OBJ file is turned into a model.</summary>
public sealed class ModelOptions
{
    /// <summary>Uniform scale applied to the file's units.</summary>
    public float Scale = 1f;

    /// <summary>Extra rotation as Euler angles in degrees, applied before the pivot.</summary>
    public Vector3 Rotation;

    /// <summary>Extra shift applied after the pivot, in scaled units.</summary>
    public Vector3 Offset;

    /// <summary>Where the origin ends up. <see cref="ModelPivot.BottomCenter"/> suits things that stand on the ground.</summary>
    public ModelPivot Pivot = ModelPivot.Keep;

    /// <summary>
    /// Mirror the X axis, which turns OBJ's right-handed layout into Unity's left-handed one. Leave on for
    /// files from Blender or similar; turn off for models that already face the right way.
    /// </summary>
    public bool FlipX = true;

    public ModelCollider Collider = ModelCollider.None;

    /// <summary>Layer the instances are put on; 0 is Default.</summary>
    public int Layer;

    /// <summary>Longest texture side accepted for the model's materials.</summary>
    public int MaxTextureSide = 2048;

    /// <summary>How shiny the surface is, 0 (matte) to 1 (mirror). The MTL format has no value the game's shader can use for this.</summary>
    public float Smoothness = 0.2f;

    /// <summary>Texture to use for every material instead of the MTL's <c>map_Kd</c>. Relative paths are inside the mod folder.</summary>
    public string Texture;

    /// <summary>Shader to build the materials with. Null picks the game's Standard shader.</summary>
    public string Shader;

    internal ModelOptions Copy() => (ModelOptions)MemberwiseClone();

    internal string Check()
    {
        if (!(Scale > 0f) || float.IsInfinity(Scale) || Scale > 1000f)
            return "Scale must be above 0 and at most 1000";
        if (!Finite(Rotation) || !Finite(Offset))
            return "Rotation and Offset must be finite numbers";
        if (Layer < 0 || Layer > 31)
            return "Layer must be from 0 to 31";
        return null;
    }

    private static bool Finite(Vector3 v)
    {
        float sum = v.x + v.y + v.z;
        return !float.IsNaN(sum) && !float.IsInfinity(sum);
    }
}
