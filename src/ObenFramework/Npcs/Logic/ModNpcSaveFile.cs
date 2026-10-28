// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Npcs;

/// <summary>One blend shape weight a mod NPC's body differs in from its template.</summary>
[Serializable]
public sealed class ModNpcShape
{
    public string Mesh;
    public string Shape;
    public float Weight;
}

/// <summary>A saved mod NPC: who it is, where it stands, what it looks like. Field names are the file format.</summary>
[Serializable]
internal sealed class ModNpcSaved
{
    public string Mod;
    public string Name;
    public int Template;
    public int Body;
    public string FirstName;
    public string LastName;
    public string Scene;
    public float X;
    public float Y;
    public float Z;
    public float Yaw;
    public bool KeepDialogue;

    /// <summary>The look as <c>LookData</c> JSON, or null for the template's own.</summary>
    public string Look;

    public List<ModNpcShape> Shapes = new List<ModNpcShape>();
}

/// <summary>
/// The mod NPCs kept in a save, as the framework writes them next to the save
/// (<c>mods/ObenFramework.npcs.json</c>). Entries of mods that are not installed stay in the file so the
/// NPCs come back with the mod. No Unity types, so it is unit tested.
/// </summary>
[Serializable]
internal sealed class ModNpcSaveFile
{
    public const int MaxPerMod = 256;
    public const int MaxShapes = 256;

    public List<ModNpcSaved> Npcs = new List<ModNpcSaved>();

    public ModNpcSaved Find(string mod, string name)
    {
        foreach (var entry in Npcs)
        {
            if (entry.Mod == mod && entry.Name == name)
                return entry;
        }
        return null;
    }

    /// <summary>Room for one more NPC of this mod (or it is already there and would be replaced).</summary>
    public bool HasRoom(string mod, string name) => Find(mod, name) != null || Count(mod) < MaxPerMod;

    /// <summary>Adds the entry, replacing one of the same mod and name.</summary>
    public void Put(ModNpcSaved entry)
    {
        Npcs.RemoveAll(e => e.Mod == entry.Mod && e.Name == entry.Name);
        Npcs.Add(entry);
    }

    public int Count(string mod)
    {
        int count = 0;
        foreach (var entry in Npcs)
        {
            if (entry.Mod == mod)
                count++;
        }
        return count;
    }

    /// <summary>Removes one NPC, or every NPC of the mod when <paramref name="name"/> is null. Returns how many went.</summary>
    public int Drop(string mod, string name) => Npcs.RemoveAll(e => e.Mod == mod && (name == null || e.Name == name));

    /// <summary>The entries placed in <paramref name="scene"/> whose mod <paramref name="loaded"/> says is there.</summary>
    public List<ModNpcSaved> InScene(string scene, Func<string, bool> loaded)
    {
        var result = new List<ModNpcSaved>();
        foreach (var entry in Npcs)
        {
            if (entry.Scene == scene && loaded(entry.Mod))
                result.Add(entry);
        }
        return result;
    }

    /// <summary>
    /// A copy with broken entries left out (no mod, bad name, no scene, numbers that are not finite), at most
    /// <see cref="MaxPerMod"/> per mod, and shapes cleaned. What a hand-edited or damaged file turns into.
    /// </summary>
    public static ModNpcSaveFile Clean(ModNpcSaveFile file)
    {
        var clean = new ModNpcSaveFile();
        if (file == null || file.Npcs == null)
            return clean;
        foreach (var entry in file.Npcs)
        {
            if (!Valid(entry) || !clean.HasRoom(entry.Mod, entry.Name))
                continue;
            entry.FirstName = string.IsNullOrEmpty(entry.FirstName) ? entry.Name : entry.FirstName;
            entry.LastName = entry.LastName ?? string.Empty;
            entry.Look = string.IsNullOrWhiteSpace(entry.Look) ? null : entry.Look;
            entry.Shapes = CleanShapes(entry.Shapes);
            clean.Put(entry);
        }
        return clean;
    }

    /// <summary>Shapes with a mesh, a name and a finite weight, at most <see cref="MaxShapes"/>, the last value of a repeated one kept.</summary>
    public static List<ModNpcShape> CleanShapes(List<ModNpcShape> shapes)
    {
        var clean = new List<ModNpcShape>();
        if (shapes == null)
            return clean;
        foreach (var shape in shapes)
        {
            if (shape == null || string.IsNullOrEmpty(shape.Mesh) || string.IsNullOrEmpty(shape.Shape) || !NpcWalkMath.Finite(shape.Weight))
                continue;
            clean.RemoveAll(s => s.Mesh == shape.Mesh && s.Shape == shape.Shape);
            if (clean.Count == MaxShapes)
                break;
            clean.Add(new ModNpcShape { Mesh = shape.Mesh, Shape = shape.Shape, Weight = shape.Weight });
        }
        return clean;
    }

    private static bool Valid(ModNpcSaved entry) =>
        entry != null && !string.IsNullOrEmpty(entry.Mod) && ModNpcIds.ValidName(entry.Name) && entry.Template > 0 && entry.Body >= 0
        && !string.IsNullOrEmpty(entry.Scene) && NpcWalkMath.Finite(entry.X) && NpcWalkMath.Finite(entry.Y) && NpcWalkMath.Finite(entry.Z)
        && NpcWalkMath.Finite(entry.Yaw);
}
