// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenFramework.Player.Look;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Mods;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// Saved mod NPCs, in the framework's own per-save file (<c>mods/ObenFramework.npcs.json</c>). Positions and
/// looks are taken from the live NPCs when the game saves; the NPCs of a scene are rebuilt when it is
/// ready, and those of a mod when it loads mid-game (a hot reload).
/// </summary>
internal static class ModNpcStore
{
    private static ModSaveData<ModNpcSaveFile> _data;

    private static ModNpcSaveFile File => _data != null ? _data.Value : EmptyFile;

    private static readonly ModNpcSaveFile EmptyFile = new ModNpcSaveFile();

    public static void Init(ObenMod framework)
    {
        _data = ModSaves.Register<ModNpcSaveFile>(framework, "npcs");
        Clean(_data.Value);
        _data.DataLoaded.Subscribe(framework, Clean);
        GameEvents.WhenWorldReady(framework, RestoreScene);

        ModRegistry.ModLoaded += OnModLoaded;
        framework.Track(Disposable.Create(() =>
        {
            ModRegistry.ModLoaded -= OnModLoaded;
            _data = null;
        }));
    }

    public static bool HasRoom(string mod, string name) => File.HasRoom(mod, name);

    /// <summary>
    /// Rebuilds the mod's saved NPC when the save has it in this scene. True with the NPC (null if it could
    /// not be built); false when the save has no such NPC here, so the caller spawns a fresh one.
    /// </summary>
    public static bool TryRestore(ObenMod owner, string name, out ModNpc npc)
    {
        npc = null;
        var entry = File.Find(owner.Info.Id, name);
        if (entry == null || entry.Scene != WorldState.SceneName)
            return false;
        npc = Restore(owner, entry);
        return true;
    }

    /// <summary>Writes a live saved NPC into the file (a new one, or one moved to this scene).</summary>
    public static void Remember(ModNpc npc)
    {
        var entry = new ModNpcSaved
        {
            Mod = npc.Owner,
            Name = npc.Name,
            Template = npc.Template,
            Body = npc.Body,
            FirstName = npc.Clone.Name,
            LastName = npc.Clone.LastName,
            KeepDialogue = KeepsDialogue(npc),
        };
        Fill(entry, npc);
        File.Put(entry);
    }

    /// <summary>Updates the saved entry of a live NPC with where it stands and how it looks.</summary>
    public static void Keep(ModNpc npc)
    {
        var entry = File.Find(npc.Owner, npc.Name);
        if (entry != null && npc.Loaded)
            Fill(entry, npc);
    }

    public static int Forget(string mod, string name) => File.Drop(mod, name);

    // Before the game writes a save: living saved NPCs record their state, dead ones leave the save.
    public static void Snapshot()
    {
        try
        {
            foreach (var npc in ModNpcs.All())
            {
                if (!npc.Saved)
                    continue;
                if (npc.Alive)
                    Keep(npc);
                else
                    Forget(npc.Owner, npc.Name);
            }
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Saving mod NPCs", e);
        }
    }

    private static void RestoreScene(string scene)
    {
        if (SaveGate.IsBusy || NPCManager.instance == null)
            return;
        foreach (var entry in File.InScene(scene, ModRegistry.IsLoaded))
        {
            var owner = ModRegistry.Find(entry.Mod)?.Instance;
            if (owner != null && ModNpcs.Get(entry.Mod, entry.Name) == null)
                Restore(owner, entry);
        }
    }

    private static void OnModLoaded(ModInfo mod)
    {
        try
        {
            if (!WorldState.IsReady || SaveGate.IsBusy || NPCManager.instance == null || mod.Instance == null)
                return;
            foreach (var entry in File.InScene(WorldState.SceneName, id => id == mod.Id))
            {
                if (ModNpcs.Get(entry.Mod, entry.Name) == null)
                    Restore(mod.Instance, entry);
            }
        }
        catch (Exception e)
        {
            NpcsLog.Failed("Restoring mod NPCs after a mod load", e);
        }
    }

    private static ModNpc Restore(ObenMod owner, ModNpcSaved entry)
    {
        LookData look = null;
        if (entry.Look != null)
        {
            try { look = LookData.FromJson(entry.Look); }
            catch (Exception e) { NpcsLog.Failed($"Reading the saved look of {entry.Mod}/{entry.Name}", e); }
        }
        return ModNpcs.Build(owner, new ModNpcBuilder.Plan
        {
            Owner = entry.Mod,
            Name = entry.Name,
            Template = entry.Template,
            Body = entry.Body,
            FirstName = entry.FirstName,
            LastName = entry.LastName,
            Position = new Vector3(entry.X, entry.Y, entry.Z),
            Yaw = entry.Yaw,
            KeepDialogue = entry.KeepDialogue,
            Saved = true,
            Look = look,
            Shapes = entry.Shapes,
        });
    }

    private static void Fill(ModNpcSaved entry, ModNpc npc)
    {
        var at = npc.Root.transform;
        entry.Scene = npc.Scene;
        entry.X = at.position.x;
        entry.Y = at.position.y;
        entry.Z = at.position.z;
        entry.Yaw = at.eulerAngles.y;
        entry.Look = npc.LookJson;
        entry.Shapes = npc.ShapesCopy();
    }

    private static bool KeepsDialogue(ModNpc npc)
    {
        var talk = npc.Info.interactableTalk;
        if (talk == null)
            talk = npc.Root.GetComponentInChildren<InteractableTalk>(true);
        return talk != null && !talk.talkDisabled;
    }

    private static void Clean(ModNpcSaveFile file)
    {
        var clean = ModNpcSaveFile.Clean(file);
        file.Npcs.Clear();
        file.Npcs.AddRange(clean.Npcs);
    }
}
