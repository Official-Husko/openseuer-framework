namespace ObenFramework.Patching;

/// <summary>A snapshot of one patch area: a group of a mod's patches that stands or falls together.</summary>
public sealed class PatchAreaInfo
{
    /// <summary>Id of the mod that owns the patches.</summary>
    public string Owner { get; internal set; }

    /// <summary>The area's name, such as "Shops" or "World".</summary>
    public string Name { get; internal set; }

    /// <summary>How many patches (classes or single handlers) were applied in it.</summary>
    public int Patches { get; internal set; }

    /// <summary>False once one of its patches failed and the area was switched off.</summary>
    public bool IsAvailable { get; internal set; }

    /// <summary>Why the area was switched off, or null.</summary>
    public string Error { get; internal set; }

    public override string ToString() => IsAvailable ? $"{Owner}/{Name}: {Patches} patches" : $"{Owner}/{Name}: off ({Error})";
}
