using System;

namespace ObenFramework.Patching;

/// <summary>
/// Puts a Harmony patch class (or a module class) in a named patch area. Without it the area is the
/// class's namespace below the mod's root namespace ("ObenFramework.Economy" is "Economy"). When one
/// patch class of an area fails, the whole area is switched off; see <see cref="PatchAreas"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class PatchAreaAttribute : Attribute
{
    public PatchAreaAttribute(string name)
    {
        Name = name;
    }

    public string Name { get; }
}
