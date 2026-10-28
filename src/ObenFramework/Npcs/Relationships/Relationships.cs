// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Npcs;

/// <summary>
/// The player's relationships (family, an ex, a pen pal...): relationship assets the game adds from start
/// choices and story events. Each has a disposition from -100 to 100 that drifts towards a target once per
/// game day and, depending on it, sends gifts and mail. The game saves them with the rest of the save, so a
/// value set here stays in the save and is not undone when your mod unloads.
/// </summary>
public static class Relationships
{
    /// <summary>Lowest disposition the game uses.</summary>
    public const float MinDisposition = -100f;

    /// <summary>Highest disposition the game uses.</summary>
    public const float MaxDisposition = 100f;

    /// <summary>
    /// A relationship started or ended, or its disposition moved (the daily drift or a mod's
    /// <see cref="SetDisposition"/>). Each handler runs in its own try/catch.
    /// </summary>
    public static readonly WorldEvent<RelationshipChange> Changed = new WorldEvent<RelationshipChange>(nameof(Changed));

    /// <summary>The game's relationship list exists (a level is loaded).</summary>
    public static bool IsAvailable => RelationshipController.instance != null;

    /// <summary>Every relationship the player has, paused ones included. Empty outside a level.</summary>
    public static List<RelationshipRecord> All()
    {
        var result = new List<RelationshipRecord>();
        var list = Infos();
        if (list == null)
            return result;
        foreach (var info in list)
        {
            if (info != null && info.relationship != null)
                result.Add(new RelationshipRecord(info));
        }
        return result;
    }

    /// <summary>The relationship with this id (or name), or null when the player does not have it.</summary>
    public static RelationshipRecord Get(string id)
    {
        var info = Find(id);
        return info != null ? new RelationshipRecord(info) : null;
    }

    /// <summary>The player has this relationship (the same test the game's Lua <c>RelationshipIdActive</c> makes).</summary>
    public static bool Has(string id) => Find(id) != null;

    /// <summary>
    /// Sets the disposition, and the target it drifts to unless <paramref name="target"/> says otherwise, so
    /// it stays put. Values are clamped to -100..100. Refused (false) while a save or load runs and for a
    /// relationship the player does not have.
    /// </summary>
    public static bool SetDisposition(string id, float value, float? target = null)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            throw new ArgumentException("The disposition must be a finite number.", nameof(value));
        if (target.HasValue && (float.IsNaN(target.Value) || float.IsInfinity(target.Value)))
            throw new ArgumentException("The target must be a finite number.", nameof(target));

        var info = Find(id);
        if (info == null || SaveGate.IsBusy)
            return false;

        float before = info.disposition;
        info.disposition = Clamp(value);
        info.dispositionTarget = Clamp(target ?? value);
        if (info.disposition != before)
            Raise(RelationshipChangeKind.Disposition, info.relationship, before, info.disposition);
        return true;
    }

    /// <summary>
    /// Pauses or resumes a relationship. A paused one keeps its values but does not drift or send anything.
    /// Returns false when the player does not have it.
    /// </summary>
    public static bool SetActive(string id, bool active)
    {
        var info = Find(id);
        if (info == null || SaveGate.IsBusy)
            return false;
        info.active = active;
        return true;
    }

    internal static void Raise(RelationshipChangeKind kind, Relationship relationship, float previous, float current)
    {
        if (Changed.HasSubscribers && relationship != null)
            Changed.Raise(new RelationshipChange(kind, relationship, previous, current));
    }

    private static RelationshipController.RelationshipInfo Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        var list = Infos();
        if (list == null)
            return null;

        RelationshipController.RelationshipInfo byName = null;
        foreach (var info in list)
        {
            var relationship = info != null ? info.relationship : null;
            if (relationship == null)
                continue;
            if (relationship.Id == id)
                return info;
            if (byName == null && string.Equals(relationship.relationshipName, id, StringComparison.OrdinalIgnoreCase))
                byName = info;
        }
        return byName;
    }

    private static List<RelationshipController.RelationshipInfo> Infos()
    {
        var controller = RelationshipController.instance;
        return controller != null ? controller.relationships : null;
    }

    private static float Clamp(float value) => Math.Max(MinDisposition, Math.Min(MaxDisposition, value));
}
