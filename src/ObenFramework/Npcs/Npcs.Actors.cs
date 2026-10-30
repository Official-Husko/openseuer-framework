// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Scenes;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>
/// An NPC object in the scene you are in, with or without a character behind it. Some of the game's scene
/// NPCs (extras in a crowd, a body in a story scene) have no character, so they have no id and
/// <see cref="Npcs.InScene"/> leaves them out; <see cref="Handle"/> names them instead.
/// </summary>
public sealed class SceneActor
{
    internal SceneActor(NPCInfo live)
    {
        Live = live;
        Handle = SceneIds.Of(live);
        Name = live.gameObject.name;
        Id = live.character != null ? live.character.ID : -1;
        FullName = live.character != null ? live.character.FullName : null;
        Position = live.transform.position;
        Active = live.gameObject.activeInHierarchy;
        Dead = live.IsDead();
        HasController = live.npcController != null;
    }

    /// <summary>The game's object. Check for Unity null before use.</summary>
    public NPCInfo Live { get; }

    /// <summary>A text id for the object that <see cref="Npcs.FindActor"/> takes back.</summary>
    public string Handle { get; }

    /// <summary>The object's name in the scene.</summary>
    public string Name { get; }

    /// <summary>The character id, or -1 for an actor without a character.</summary>
    public int Id { get; }

    /// <summary>The character's full name, or null without a character.</summary>
    public string FullName { get; }

    public bool HasCharacter => Id >= 0;

    public Vector3 Position { get; }

    /// <summary>The object is switched on (distance culling switches far ones off).</summary>
    public bool Active { get; }

    public bool Dead { get; }

    /// <summary>It has the game's NPC AI, so it can be claimed and walk (if it has a character).</summary>
    public bool HasController { get; }

    public override string ToString() => $"{(HasCharacter ? FullName + " (" + Id + ")" : Name)} {Handle}";
}

public static partial class Npcs
{
    /// <summary>
    /// Every NPC object in the scene you are in, switched off ones included, also those without a character
    /// that <see cref="InScene"/> leaves out. Walks the scene, so call it when something happened.
    /// </summary>
    public static List<SceneActor> Actors()
    {
        var list = new List<SceneActor>();
        foreach (var live in SceneIds.Active<NPCInfo>(true))
            list.Add(new SceneActor(live));
        return list;
    }

    /// <summary>The NPC object with this <see cref="SceneActor.Handle"/>, or null.</summary>
    public static NPCInfo FindActor(string handle) => SceneIds.Find<NPCInfo>(handle);
}
