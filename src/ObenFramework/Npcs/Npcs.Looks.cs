// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Player.Look;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>Reading how an NPC looks without claiming it. Changing a look goes through an <see cref="NpcClaim"/>.</summary>
public static partial class Npcs
{
    /// <summary>The meshes of an NPC's body (names, materials, blend shapes). Empty when it isn't in the scene.</summary>
    public static List<NpcMesh> Meshes(int id)
    {
        var npc = LiveObject(id);
        return npc != null ? NpcMesh.Of(npc.gameObject) : new List<NpcMesh>();
    }

    /// <summary>
    /// The look of an NPC whose body has the game's appearance sliders, as <see cref="PlayerLook"/> stores a
    /// look; null for bodies without sliders (most of them) or an NPC that isn't in the scene.
    /// </summary>
    public static LookData LookOf(int id)
    {
        var npc = LiveObject(id);
        var appearance = npc != null ? npc.GetComponentInChildren<Appearance>(true) : null;
        return appearance != null ? PlayerLook.Capture(appearance) : null;
    }

    /// <summary>The names of every material the game has loaded right now, for <see cref="NpcClaim.SetMaterials(string, string[])"/>.</summary>
    public static List<string> MaterialNames()
    {
        var names = new SortedSet<string>(System.StringComparer.Ordinal);
        foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (material != null && !string.IsNullOrEmpty(material.name))
                names.Add(material.name);
        }
        return new List<string>(names);
    }

    internal static Material LoadedMaterial(string name)
    {
        foreach (var material in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (material != null && material.name == name)
                return material;
        }
        return null;
    }
}
