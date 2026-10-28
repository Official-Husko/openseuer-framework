// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using UnityEngine;

namespace ObenFramework.Npcs;

/// <summary>Finding the game characters and bodies a mod NPC is copied from, and the ground it stands on.</summary>
internal static class ModNpcTemplates
{
    private const float GroundLift = 1f;
    private const float GroundReach = 4f;

    /// <summary>A game character by id: not the player (the database's first entry) and not a mod NPC. Null when there is none.</summary>
    public static Character Find(int id)
    {
        var all = CharacterDatabase.Characters;
        if (all == null || all.Count == 0)
            return null;
        var player = all[0];
        foreach (var character in all)
        {
            if (character != null && character != player && character.ID == id && !ModNpcIds.IsModRange(character.ID))
                return character;
        }
        return null;
    }

    /// <summary>The prefab the game spawns for a character: the one with AI when it has one.</summary>
    public static GameObject PrefabOf(Character character) =>
        character.prefabWithAi != null ? character.prefabWithAi : character.prefab;

    /// <summary>
    /// The character whose body the copy uses: the one asked for, else the template when its prefab has
    /// editor sliders or is a full NPC, else the first NPC body with sliders (for templates with no prefab).
    /// </summary>
    public static Character BodyFor(Character template, int bodyId)
    {
        if (bodyId != 0)
            return Find(bodyId);
        var prefab = PrefabOf(template);
        if (prefab != null && prefab.GetComponent<NPCInfo>() != null)
            return template;
        return DefaultBody();
    }

    /// <summary>The character's body has the game's NPC AI (an <c>NPCController</c>), so a copy of it can walk.</summary>
    public static bool HasAi(Character character)
    {
        var prefab = character != null ? PrefabOf(character) : null;
        return prefab != null && prefab.GetComponent<NPCController>() != null;
    }

    /// <summary>
    /// The point on the ground below <paramref name="position"/> (looking from a metre above it, down to
    /// four metres), or false when there is nothing to stand on.
    /// </summary>
    public static bool Ground(Vector3 position, out Vector3 ground)
    {
        if (Physics.Raycast(position + Vector3.up * GroundLift, Vector3.down, out var hit, GroundReach, ~0, QueryTriggerInteraction.Ignore))
        {
            ground = hit.point;
            return true;
        }
        ground = position;
        return false;
    }

    private static Character DefaultBody()
    {
        var all = CharacterDatabase.Characters;
        var player = all[0];
        Character best = null;
        foreach (var character in all)
        {
            if (character == null || character == player || ModNpcIds.IsModRange(character.ID))
                continue;
            if (best != null && character.ID >= best.ID)
                continue;
            var prefab = PrefabOf(character);
            if (prefab != null && prefab.GetComponent<NPCInfo>() != null && prefab.GetComponentInChildren<Appearance>(true) != null)
                best = character;
        }
        return best;
    }
}
