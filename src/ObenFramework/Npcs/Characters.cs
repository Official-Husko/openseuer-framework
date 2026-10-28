// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>One character asset of the game (or a mod NPC's copy), copied when read.</summary>
public sealed class CharacterRecord
{
    public int Id { get; }
    public string Name { get; }
    public string FullName { get; }
    public string Job { get; }
    public string Gender { get; }
    public int Age { get; }

    /// <summary>The game has a body (prefab) to spawn for it, so it can be a mod NPC template or body.</summary>
    public bool HasBody { get; }

    /// <summary>Its body has character editor sliders (a mod NPC copying it can wear a <c>LookData</c>).</summary>
    public bool HasSliders { get; }

    /// <summary>
    /// Its body has the game's NPC AI, so a mod NPC copying it can walk and take orders and routines. Only the
    /// characters with an AI prefab have one (11 in 0.5.13); the other bodies stand where they are put.
    /// </summary>
    public bool HasAi { get; }

    /// <summary>The player's own character (the database's first entry).</summary>
    public bool IsPlayer { get; }

    /// <summary>A mod NPC's copy rather than one of the game's characters.</summary>
    public bool IsModNpc { get; }

    internal CharacterRecord(Character character, bool player)
    {
        Id = character.ID;
        Name = character.Name ?? string.Empty;
        FullName = character.FullName ?? string.Empty;
        Job = character.Job ?? string.Empty;
        Gender = character.GenderString ?? string.Empty;
        Age = character.Age;
        IsPlayer = player;
        IsModNpc = ModNpcIds.IsModRange(character.ID);
        var prefab = ModNpcTemplates.PrefabOf(character);
        HasBody = prefab != null && prefab.GetComponent<NPCInfo>() != null;
        HasSliders = prefab != null && prefab.GetComponentInChildren<Appearance>(true) != null;
        HasAi = ModNpcTemplates.HasAi(character);
    }

    public override string ToString() => $"{FullName} ({Id})";
}

/// <summary>
/// The game's character database: every character it has an asset for, the ones that never appear in the
/// scene you are in included. Templates for mod NPCs come from here. At the main menu, before the database
/// is filled, the assets are read from the game's resources.
/// </summary>
public static class Characters
{
    /// <summary>Every character, the player first.</summary>
    public static List<CharacterRecord> All()
    {
        var result = new List<CharacterRecord>();
        var source = CharacterDatabase.Characters;
        if (source != null && source.Count > 0)
        {
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i] != null)
                    result.Add(new CharacterRecord(source[i], i == 0));
            }
            return result;
        }
        foreach (var character in Resources.LoadAll<Character>("Characters"))
        {
            if (character != null)
                result.Add(new CharacterRecord(character, false));
        }
        return result;
    }

    /// <summary>The character with this id, or null.</summary>
    public static CharacterRecord Get(int id)
    {
        foreach (var character in All())
        {
            if (character.Id == id)
                return character;
        }
        return null;
    }
}
