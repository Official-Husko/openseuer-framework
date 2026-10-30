using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Mods;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>A mod NPC kept in the loaded save, wherever it is (see <see cref="ModNpcs.Saved(ObenMod)"/>).</summary>
public sealed class SavedModNpc
{
    /// <summary>The mod that keeps it, installed or not.</summary>
    public string Mod { get; internal set; }

    /// <summary>The name the mod gave it.</summary>
    public string Name { get; internal set; }

    public string FirstName { get; internal set; }
    public string LastName { get; internal set; }
    public int Template { get; internal set; }
    public int Body { get; internal set; }

    /// <summary>The scene it lives in.</summary>
    public string Scene { get; internal set; }

    /// <summary>Where it stands: now if it is here, else where the save has it.</summary>
    public Vector3 Position { get; internal set; }

    public float Yaw { get; internal set; }

    /// <summary>It is spawned in the scene you are in right now.</summary>
    public bool Here { get; internal set; }

    /// <summary>Its mod is loaded, so it comes back when its scene loads.</summary>
    public bool ModLoaded { get; internal set; }

    public override string ToString() => $"{FirstName} {LastName} ({Mod}/{Name}) in {Scene}{(Here ? ", here" : "")}";
}

public static partial class ModNpcs
{
    /// <summary>The mod's NPCs kept in the loaded save, in every scene, not only the one you are in.</summary>
    public static List<SavedModNpc> Saved(ObenMod owner) => owner == null ? new List<SavedModNpc>() : Saved(owner.Info.Id);

    /// <summary>Every mod's NPCs kept in the loaded save, also those of mods that aren't installed.</summary>
    public static List<SavedModNpc> SavedAll() => Saved((string)null);

    private static List<SavedModNpc> Saved(string mod)
    {
        var list = new List<SavedModNpc>();
        foreach (var entry in ModNpcStore.Entries(mod))
        {
            var live = Get(entry.Mod, entry.Name);
            bool here = live != null && live.Saved && live.Loaded;
            list.Add(new SavedModNpc
            {
                Mod = entry.Mod,
                Name = entry.Name,
                FirstName = entry.FirstName,
                LastName = entry.LastName,
                Template = entry.Template,
                Body = entry.Body,
                Scene = entry.Scene,
                Position = here ? live.Position : new Vector3(entry.X, entry.Y, entry.Z),
                Yaw = here ? live.Root.transform.eulerAngles.y : entry.Yaw,
                Here = here,
                ModLoaded = ModRegistry.IsLoaded(entry.Mod),
            });
        }
        return list;
    }
}
