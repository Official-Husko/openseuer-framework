using System.Collections.Generic;
using ObenFramework.Saves;
using ObenLoader;
using UnityEngine;

namespace ObenFramework.World;

/// <summary>One placed piece of furniture as remembered between sessions.</summary>
internal sealed class FurnitureLedgerEntry
{
    public string Id;
    public string Manager;
    public string Type;
    public float X;
    public float Y;
    public float Z;
}

/// <summary>The ids of placed furniture per scene, stored with the save.</summary>
internal sealed class FurnitureLedgerData
{
    public Dictionary<string, List<FurnitureLedgerEntry>> Scenes = new Dictionary<string, List<FurnitureLedgerEntry>>();
}

/// <summary>
/// Remembers which id belongs to which piece of furniture across saving and loading. The game throws its
/// furniture away and rebuilds it from the save, with different GUIDs each time, so the only things that
/// survive are what the furniture is, which zone it is in and where it stands. The ledger is written
/// with each save (in the save's <c>mods</c> folder) and pieces are matched back by those three things.
/// </summary>
internal static class FurnitureLedger
{
    // Two pieces of the same kind closer than this are told apart only by luck; none are in practice.
    private const float MatchDistanceSquared = 0.02f * 0.02f;

    private static ModSaveData<FurnitureLedgerData> _save;

    public static void Init(ObenMod owner) => _save = ModSaves.Register<FurnitureLedgerData>(owner, "furniture");

    public static void Shutdown() => _save = null;

    /// <summary>
    /// The remembered id for a piece standing at <paramref name="position"/>, or null. <paramref name="taken"/>
    /// says whether an id already belongs to a live piece, so two pieces never share one.
    /// </summary>
    public static string Match(string scene, string manager, string type, Vector3 position, System.Func<string, bool> taken)
    {
        if (_save == null || !_save.Value.Scenes.TryGetValue(scene, out var entries))
            return null;

        FurnitureLedgerEntry best = null;
        float bestDistance = MatchDistanceSquared;
        foreach (var entry in entries)
        {
            if (entry.Manager != manager || entry.Type != type || taken(entry.Id))
                continue;
            float distance = (new Vector3(entry.X, entry.Y, entry.Z) - position).sqrMagnitude;
            if (distance <= bestDistance)
            {
                best = entry;
                bestDistance = distance;
            }
        }
        return best?.Id;
    }

    /// <summary>Replaces the current scene's entries with the furniture standing in it now.</summary>
    public static void Record(string scene, IEnumerable<FurnitureInfo> placed)
    {
        if (_save == null)
            return;

        var entries = new List<FurnitureLedgerEntry>();
        foreach (var info in placed)
        {
            entries.Add(new FurnitureLedgerEntry
            {
                Id = info.Id,
                Manager = info.Manager,
                Type = info.TypeId,
                X = info.Position.x,
                Y = info.Position.y,
                Z = info.Position.z,
            });
        }
        _save.Value.Scenes[scene] = entries;
    }
}
