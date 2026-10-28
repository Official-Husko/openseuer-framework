using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// An NPC a mod spawned (see <see cref="ModNpcs.Spawn"/>): a copy of a game character with its own name
/// and character id, standing in the scene you are in. It is a real NPC to the game: it shows up in
/// <see cref="Npcs"/>, can be claimed, animated and talked to (if allowed), and dies like any other.
/// The handle stays usable until the NPC is despawned, its mod unloads or the level changes;
/// <see cref="Loaded"/> says whether it still is.
/// </summary>
public sealed partial class ModNpc
{
    internal ModNpc(string owner, string name, Character template, int bodyId, Character clone, GameObject body, NPCInfo info, bool sliders, bool saved)
    {
        Owner = owner;
        Name = name;
        TemplateCharacter = template;
        Template = template.ID;
        Body = bodyId;
        Clone = clone;
        CharacterId = clone.ID;
        Root = body;
        Info = info;
        HasSliders = sliders;
        Saved = saved;
        Scene = body.scene.name;
    }

    /// <summary>Id of the mod it belongs to.</summary>
    public string Owner { get; }

    /// <summary>The name the mod gave it (unique per mod).</summary>
    public string Name { get; }

    /// <summary>Its character id, from 1,000,000 up, the same every session (Lua, mail and the console use it).</summary>
    public int CharacterId { get; }

    /// <summary>Character id of the game NPC it copies.</summary>
    public int Template { get; }

    /// <summary>Character id of the body it uses, or 0 for the template's own.</summary>
    public int Body { get; }

    /// <summary>It is kept in the save (see <see cref="ModNpcSpec.Saved"/>).</summary>
    public bool Saved { get; }

    /// <summary>The scene it was spawned in.</summary>
    public string Scene { get; }

    /// <summary>Its body has character editor sliders, so <see cref="SetLook"/> works; otherwise only blend shapes do.</summary>
    public bool HasSliders { get; }

    /// <summary>First and last name.</summary>
    public string FullName => Clone != null ? Clone.FullName : Name;

    /// <summary>The NPC still exists in the scene.</summary>
    public bool Loaded => Root != null && Info != null;

    /// <summary>Loaded and not dead.</summary>
    public bool Alive => Loaded && !Info.IsDead();

    /// <summary>The game's NPC component, or null once it is gone.</summary>
    public NPCInfo Npc => Loaded ? Info : null;

    /// <summary>Where it stands now, or the zero vector once it is gone.</summary>
    public Vector3 Position => Loaded ? Root.transform.position : Vector3.zero;

    /// <summary>A fresh record of the NPC, the same <see cref="Npcs.Get"/> gives.</summary>
    public NpcRecord Record => Npcs.Get(CharacterId);

    /// <summary>Puts it somewhere else in the scene, on the ground below the point. False when it is gone, dead or nothing is below.</summary>
    public bool Teleport(Vector3 position)
    {
        if (!Alive || !ModNpcTemplates.Ground(position, out var ground))
            return false;
        NpcOrders.End(this, false);
        return NpcActions.Teleport(CharacterId, ground);
    }

    /// <summary>Removes the NPC and, if it was saved, forgets it in the save. Same as <see cref="ModNpcs.Despawn"/>.</summary>
    public bool Despawn() => ModNpcs.Despawn(Owner, Name);

    public override string ToString() => $"{FullName} ({Owner}/{Name}, {CharacterId})";

    internal Character TemplateCharacter { get; }
    internal Character Clone { get; }
    internal GameObject Root { get; }
    internal NPCInfo Info { get; }
}
