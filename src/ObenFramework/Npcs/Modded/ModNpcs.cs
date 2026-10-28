// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Npcs;

/// <summary>
/// NPCs of a mod's own: copies of a game character with their own name and id, spawned in the scene you
/// are in. A mod NPC can be kept in the save (<see cref="ModNpcSpec.Saved"/>) and then comes back in its
/// scene with its position and look every time the scene loads, while the mod is installed. Every NPC a
/// mod spawned is removed when the mod unloads (saved ones come back when it loads again) and when the
/// level changes. Names are per mod: two mods can both have a "barkeep".
/// </summary>
public static partial class ModNpcs
{
    private static readonly List<ModNpc> Live = new List<ModNpc>();
    private static readonly HashSet<string> Tracked = new HashSet<string>();

    /// <summary>
    /// Spawns the NPC <paramref name="name"/> of <paramref name="owner"/>, or returns it if it is already
    /// here. For a saved NPC the save wins: if the save already has it in this scene, it comes back the way
    /// it was saved and <paramref name="spec"/>'s position and look are not used (despawn it to start over).
    /// Returns null when the template or ground is missing, outside a level, or while a save or load runs;
    /// the reason is logged.
    /// </summary>
    public static ModNpc Spawn(ObenMod owner, string name, ModNpcSpec spec)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));
        spec.Validate(name);

        var existing = Get(owner.Info.Id, name);
        if (existing != null)
            return existing;
        if (SaveGate.IsBusy || NPCManager.instance == null)
            return null;

        if (spec.Saved && ModNpcStore.TryRestore(owner, name, out var restored))
            return restored;
        if (spec.Saved && !ModNpcStore.HasRoom(owner.Info.Id, name))
        {
            NpcsLog.Warn($"{owner.Info.Id} already keeps {ModNpcSaveFile.MaxPerMod} NPCs in the save; {name} was not spawned.");
            return null;
        }

        var npc = Build(owner, new ModNpcBuilder.Plan
        {
            Owner = owner.Info.Id,
            Name = name,
            Template = spec.Template,
            Body = spec.Body,
            FirstName = spec.FirstName,
            LastName = spec.LastName,
            Position = spec.Position,
            Yaw = spec.Yaw,
            KeepDialogue = spec.KeepDialogue,
            Saved = spec.Saved,
            Look = spec.Look,
        });
        if (npc != null && npc.Saved)
            ModNpcStore.Remember(npc);
        return npc;
    }

    /// <summary>The mod's NPC with this name in the scene, or null.</summary>
    public static ModNpc Get(ObenMod owner, string name) => owner == null ? null : Get(owner.Info.Id, name);

    /// <summary>The mod NPC with this character id, or null.</summary>
    public static ModNpc Get(int characterId)
    {
        Prune();
        foreach (var npc in Live)
        {
            if (npc.CharacterId == characterId)
                return npc;
        }
        return null;
    }

    /// <summary>The mod's NPCs in the scene.</summary>
    public static List<ModNpc> List(ObenMod owner) => owner == null ? new List<ModNpc>() : List(owner.Info.Id);

    /// <summary>Every mod NPC in the scene, whichever mod it belongs to.</summary>
    public static List<ModNpc> All() => List((string)null);

    /// <summary>The character id belongs to a mod NPC (it may be in another scene or not spawned).</summary>
    public static bool IsModNpc(int characterId) => ModNpcIds.IsModRange(characterId);

    /// <summary>
    /// Removes the NPC from the scene and forgets it in the save. Returns false when the mod has no NPC of
    /// that name here or in the save, or a save or load is running.
    /// </summary>
    public static bool Despawn(ObenMod owner, string name) => owner != null && Despawn(owner.Info.Id, name);

    /// <summary>Removes all of the mod's NPCs and forgets them in the save. Returns how many there were.</summary>
    public static int Clear(ObenMod owner)
    {
        if (owner == null || SaveGate.IsBusy)
            return 0;
        var mine = List(owner.Info.Id);
        foreach (var npc in mine)
            Release(npc);
        int forgotten = ModNpcStore.Forget(owner.Info.Id, null);
        return Math.Max(mine.Count, forgotten);
    }

    internal static bool Despawn(string owner, string name)
    {
        if (SaveGate.IsBusy)
            return false;
        var npc = Get(owner, name);
        if (npc != null)
            Release(npc);
        int forgotten = ModNpcStore.Forget(owner, name);
        return npc != null || forgotten > 0;
    }

    internal static ModNpc Get(string owner, string name)
    {
        Prune();
        foreach (var npc in Live)
        {
            if (npc.Owner == owner && npc.Name == name)
                return npc;
        }
        return null;
    }

    internal static List<ModNpc> List(string owner)
    {
        Prune();
        var result = new List<ModNpc>();
        foreach (var npc in Live)
        {
            if (owner == null || npc.Owner == owner)
                result.Add(npc);
        }
        return result;
    }

    // Builds and registers a NPC; the owner's unload removes its NPCs (saved ones stay in the save).
    internal static ModNpc Build(ObenMod owner, ModNpcBuilder.Plan plan)
    {
        ModNpc npc;
        try
        {
            npc = ModNpcBuilder.Build(plan);
        }
        catch (Exception e)
        {
            NpcsLog.Failed($"Spawning mod NPC {plan.Owner}/{plan.Name}", e);
            return null;
        }
        if (npc == null)
            return null;

        npc.StartWith(plan.Look, plan.Shapes);
        Live.Add(npc);
        if (Tracked.Add(owner.Info.Id))
        {
            string id = owner.Info.Id;
            owner.Track(Disposable.Create(() =>
            {
                Tracked.Remove(id);
                RemoveAll(id);
            }));
        }
        return npc;
    }

    internal static void Init(ObenMod framework)
    {
        GameEvents.SceneLoaded.Subscribe(framework, _ => ForgetGone());
        GameEvents.Saving.Subscribe(framework, ModNpcStore.Snapshot);
        ModNpcStore.Init(framework);
        framework.Track(Disposable.Create(() =>
        {
            RemoveAll(null);
            Tracked.Clear();
        }));
    }
}
