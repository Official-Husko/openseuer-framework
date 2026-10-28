// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenFramework.Npcs;

/// <summary>
/// Character ids for mod NPCs. The game's own characters use small numbers; a mod NPC gets an id from
/// 1,000,000 up, hashed from the mod id and the NPC's name so it is the same every session (Lua, mail and
/// saves refer to it), moved on by one while another character already has it. No Unity types.
/// </summary>
internal static class ModNpcIds
{
    public const int Base = 1000000;
    public const int Span = 1000000;
    public const int MaxNameLength = 64;

    /// <summary>The id for <paramref name="modId"/>/<paramref name="npcId"/>, skipping ids <paramref name="taken"/> says are in use.</summary>
    public static int Allocate(string modId, string npcId, Func<int, bool> taken)
    {
        int id = Base + (int)(Hash(modId + "/" + npcId) % Span);
        for (int tries = 0; tries < Span && taken(id); tries++)
            id = id + 1 >= Base + Span ? Base : id + 1;
        return id;
    }

    /// <summary>True for a name a mod may give its NPC: 1 to 64 characters, no slash.</summary>
    public static bool ValidName(string npcId) =>
        !string.IsNullOrEmpty(npcId) && npcId.Length <= MaxNameLength && npcId.IndexOf('/') < 0;

    /// <summary>True when <paramref name="id"/> lies in the range mod NPCs use.</summary>
    public static bool IsModRange(int id) => id >= Base && id < Base + Span;

    // FNV-1a, 32 bits: stable across runtimes, unlike string.GetHashCode.
    internal static uint Hash(string text)
    {
        uint hash = 2166136261u;
        foreach (char c in text)
            hash = unchecked((hash ^ c) * 16777619u);
        return hash;
    }
}
