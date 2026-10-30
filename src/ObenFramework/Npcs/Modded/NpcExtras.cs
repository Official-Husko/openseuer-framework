// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ObenFramework.Npcs;

/// <summary>
/// Extras: bodies that only stand in the scene, for crowds, queues and set dressing. An extra has a game
/// character's look (any of the 79 bodies, also those with no NPC logic at all) but is not an NPC: every
/// script on the body is taken off except the ones that build its look, the game doesn't list it, it can't
/// be talked to and it isn't saved. Its Animator still plays the body's idle. Extras are removed when their
/// mod unloads and when the level changes; spawn them again from <c>GameEvents.WhenWorldReady</c>.
/// Names are per mod, apart from the names of <see cref="ModNpcs"/>.
/// </summary>
public static partial class NpcExtras
{
    public const int MaxPerMod = 64;
    public const int MaxTotal = 256;

    private static readonly List<NpcExtra> Live = new List<NpcExtra>();
    private static readonly HashSet<string> Tracked = new HashSet<string>();

    /// <summary>
    /// Spawns the extra <paramref name="name"/> of <paramref name="owner"/>, or returns it if it is already
    /// here. From <paramref name="spec"/> it uses <c>Template</c> (whose look), <c>Body</c> (whose body, 0 for
    /// the template's or a slider body), <c>FirstName</c> (optional), <c>Position</c>, <c>Yaw</c> and
    /// <c>Look</c> (slider bodies). Null outside a level, while a save or load runs, over the limits, or when
    /// the character has no body or there is no ground; the reason is logged.
    /// </summary>
    public static NpcExtra Spawn(ObenMod owner, string name, ModNpcSpec spec)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (spec == null)
            throw new ArgumentNullException(nameof(spec));
        if (!ModNpcIds.ValidName(name))
            throw new ArgumentException("An extra's name must be 1 to 64 characters without '/'.", nameof(name));
        if (spec.Template <= 0 || spec.Body < 0)
            throw new ArgumentException("Template must be a game character id, Body one or 0.", nameof(spec));
        if (!NpcWalkMath.Finite(spec.Position.x) || !NpcWalkMath.Finite(spec.Position.y) || !NpcWalkMath.Finite(spec.Position.z) || !NpcWalkMath.Finite(spec.Yaw))
            throw new ArgumentException("Position and Yaw must be finite numbers.", nameof(spec));

        string mod = owner.Info.Id;
        var existing = Get(mod, name);
        if (existing != null)
            return existing;
        if (SaveGate.IsBusy || NPCManager.instance == null)
            return null;
        if (Count(mod) >= MaxPerMod || Live.Count >= MaxTotal)
            return Refuse(mod, name, $"a mod can have {MaxPerMod} extras and all mods {MaxTotal}");

        var extra = Build(mod, name, spec);
        if (extra == null)
            return null;
        Live.Add(extra);
        if (Tracked.Add(mod))
        {
            owner.Track(Disposable.Create(() =>
            {
                Tracked.Remove(mod);
                RemoveAll(mod);
            }));
        }
        return extra;
    }

    public static NpcExtra Get(ObenMod owner, string name) => owner == null ? null : Get(owner.Info.Id, name);

    /// <summary>The mod's extras in the scene.</summary>
    public static List<NpcExtra> List(ObenMod owner)
    {
        Prune();
        var list = new List<NpcExtra>();
        foreach (var extra in Live)
        {
            if (owner != null && string.Equals(extra.Owner, owner.Info.Id, StringComparison.OrdinalIgnoreCase))
                list.Add(extra);
        }
        return list;
    }

    /// <summary>Every mod's extras in the scene.</summary>
    public static List<NpcExtra> All()
    {
        Prune();
        return new List<NpcExtra>(Live);
    }

    public static bool Despawn(ObenMod owner, string name) => owner != null && Despawn(owner.Info.Id, name);

    /// <summary>Removes all of the mod's extras. Returns how many there were.</summary>
    public static int Clear(ObenMod owner) => owner == null ? 0 : RemoveAll(owner.Info.Id);

    internal static bool Despawn(string mod, string name)
    {
        var extra = Get(mod, name);
        if (extra == null)
            return false;
        Live.Remove(extra);
        extra.Destroy();
        return true;
    }

    internal static void Init(ObenMod framework)
    {
        GameEvents.LevelChanging.Subscribe(framework, () => RemoveAll(null));
        GameEvents.MainMenu.Subscribe(framework, () => RemoveAll(null));
        GameEvents.SceneLoaded.Subscribe(framework, _ => Prune());
        framework.Track(Disposable.Create(() =>
        {
            RemoveAll(null);
            Tracked.Clear();
        }));
    }

    private static NpcExtra Get(string mod, string name)
    {
        Prune();
        foreach (var extra in Live)
        {
            if (string.Equals(extra.Owner, mod, StringComparison.OrdinalIgnoreCase) && string.Equals(extra.Name, name, StringComparison.OrdinalIgnoreCase))
                return extra;
        }
        return null;
    }

    private static NpcExtra Build(string mod, string name, ModNpcSpec spec)
    {
        var template = ModNpcTemplates.Find(spec.Template);
        if (template == null)
            return Refuse(mod, name, $"there is no game character {spec.Template}");
        var body = spec.Body != 0 ? ModNpcTemplates.Find(spec.Body)
            : template.prefab != null || template.prefabWithAi != null ? template : ModNpcTemplates.BodyFor(template, 0);
        // The plain prefab when there is one: less to take off than the one with AI.
        var prefab = body == null ? null : body.prefab != null ? body.prefab : body.prefabWithAi;
        if (prefab == null)
            return Refuse(mod, name, $"character {(spec.Body != 0 ? spec.Body : spec.Template)} has no body");
        if (!ModNpcTemplates.Ground(spec.Position, out var ground))
            return Refuse(mod, name, $"nothing to stand on below {spec.Position}");

        bool sliders = prefab.GetComponentInChildren<Appearance>(true) != null;
        var clone = Object.Instantiate(template);
        clone.name = mod + "/extra/" + name;
        clone.ID = ModNpcIds.Allocate(mod, "extra/" + name, ModNpcBuilder.Taken);
        clone.Name = string.IsNullOrEmpty(spec.FirstName) ? template.Name : spec.FirstName;
        clone.LastName = spec.LastName ?? string.Empty;
        clone.resident = null;
        if (spec.Look != null && sliders)
            ModNpcLooks.Write(clone, spec.Look);
        else if (sliders)
            ModNpcLooks.EnsureHead(clone);

        var holder = new GameObject("ObenFramework extra holder");
        holder.SetActive(false);
        GameObject go = null;
        try
        {
            go = Object.Instantiate(prefab, ground, Quaternion.Euler(0f, spec.Yaw, 0f), holder.transform);
            if (!Strip(go))
            {
                Object.DestroyImmediate(holder);
                Object.Destroy(clone);
                return Refuse(mod, name, "its body's scripts couldn't all be taken off");
            }
            ModNpcBuilder.Repoint(go, template, body, clone);
            Quiet(go);
            go.name = clone.name;
            go.transform.SetParent(null, true);
            Object.DestroyImmediate(holder);
            return new NpcExtra(mod, name, template.ID, body.ID, clone, go);
        }
        catch
        {
            if (holder != null)
                Object.DestroyImmediate(holder);
            Object.Destroy(clone);
            throw;
        }
    }

    private static int Count(string mod)
    {
        Prune();
        int count = 0;
        foreach (var extra in Live)
        {
            if (string.Equals(extra.Owner, mod, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static int RemoveAll(string mod)
    {
        int removed = 0;
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            var extra = Live[i];
            if (mod != null && !string.Equals(extra.Owner, mod, StringComparison.OrdinalIgnoreCase))
                continue;
            Live.RemoveAt(i);
            extra.Destroy();
            removed++;
        }
        return removed;
    }

    // Extras whose object something else destroyed.
    private static void Prune()
    {
        for (int i = Live.Count - 1; i >= 0; i--)
        {
            if (!Live[i].Loaded)
            {
                Live[i].Destroy();
                Live.RemoveAt(i);
            }
        }
    }

    private static NpcExtra Refuse(string mod, string name, string reason)
    {
        NpcsLog.Warn($"Extra {mod}/{name} was not spawned: {reason}.");
        return null;
    }
}
