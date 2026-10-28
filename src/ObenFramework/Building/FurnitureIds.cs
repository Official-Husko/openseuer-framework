// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.Building;

/// <summary>What an id is to the custom furniture registry.</summary>
public enum FurnitureIdKind
{
    /// <summary>Not a mod id: a piece of the game's own.</summary>
    Vanilla,

    /// <summary><c>oben:modId/key</c>, a mod's furniture.</summary>
    Furniture,

    /// <summary><c>oben:modId/key/blueprint</c>, the blueprint of a mod's furniture.</summary>
    Blueprint,

    /// <summary>Starts like a mod id but doesn't parse.</summary>
    Malformed,
}

/// <summary>
/// The ids of custom furniture and their blueprints, and the checks on definitions, without Unity. A piece is
/// <c>oben:modId/key</c>: stable as long as the mod keeps its id and key, which is what lets a save find it
/// again. The game's own pieces have no such id (their id is their asset path).
/// </summary>
public static class FurnitureIds
{
    public const string Prefix = "oben:";
    public const string BlueprintSuffix = "/blueprint";
    public const int MaxKey = 64;
    public const int MaxInputs = 16;
    public const int MaxInputAmount = 1000;
    public const int MaxFurnitureInputs = 8;
    public const float MaxProcessSeconds = 10_000_000f;
    public const int MaxCraftAmount = 100;
    public const int MaxPools = 8;
    public const int MaxPoolName = 64;
    public const int MaxLootWeight = 100;

    /// <summary>The id of a mod's piece. Throws when the mod id or key isn't 1 to 64 letters, digits, '.', '_' or '-'.</summary>
    public static string For(string modId, string key)
    {
        if (!ValidPart(modId))
            throw new ArgumentException("The mod id must be 1 to 64 letters, digits, '.', '_' or '-'.", nameof(modId));
        if (!ValidPart(key))
            throw new ArgumentException("The key must be 1 to 64 letters, digits, '.', '_' or '-'.", nameof(key));
        return Prefix + modId + "/" + key;
    }

    public static bool IsModId(string id) => id != null && id.StartsWith(Prefix, StringComparison.Ordinal);

    public static FurnitureIdKind Classify(string id)
    {
        if (!IsModId(id))
            return FurnitureIdKind.Vanilla;
        if (id.EndsWith(BlueprintSuffix, StringComparison.Ordinal) && Split(id.Substring(0, id.Length - BlueprintSuffix.Length), out _, out _))
            return FurnitureIdKind.Blueprint;
        return Split(id, out _, out _) ? FurnitureIdKind.Furniture : FurnitureIdKind.Malformed;
    }

    /// <summary>Splits a furniture id into mod id and key. False for anything but a well formed furniture id.</summary>
    public static bool TryParse(string id, out string modId, out string key)
    {
        modId = null;
        key = null;
        return Classify(id) == FurnitureIdKind.Furniture && Split(id, out modId, out key);
    }

    /// <summary>The mod an id belongs to (furniture or blueprint), or null.</summary>
    public static string ModOf(string id)
    {
        if (Classify(id) == FurnitureIdKind.Blueprint)
            id = id.Substring(0, id.Length - BlueprintSuffix.Length);
        return Split(id, out string modId, out _) ? modId : null;
    }

    public static string BlueprintFor(string furnitureId) =>
        Classify(furnitureId) == FurnitureIdKind.Furniture ? furnitureId + BlueprintSuffix : null;

    public static string FurnitureOfBlueprint(string blueprintId) =>
        Classify(blueprintId) == FurnitureIdKind.Blueprint ? blueprintId.Substring(0, blueprintId.Length - BlueprintSuffix.Length) : null;

    /// <summary>Checks the numbers of a blueprint. Null when fine, else why not.</summary>
    public static string CheckBlueprint(float processSeconds, int amount, bool hasSkill, int minimumSkill, int maxSkill)
    {
        if (float.IsNaN(processSeconds) || processSeconds <= 0f || processSeconds > MaxProcessSeconds)
            return "The crafting time must be above 0 and at most 10,000,000 seconds.";
        if (amount < 1 || amount > MaxCraftAmount)
            return $"The amount must be 1 to {MaxCraftAmount}.";
        if (minimumSkill < 0 || minimumSkill > maxSkill)
            return $"The minimum skill must be 0 to {maxSkill}.";
        if (minimumSkill > 0 && !hasSkill)
            return "A minimum skill needs a skill.";
        return null;
    }

    /// <summary>Checks a blueprint's ingredients: item id to amount, and other furniture by id. Null when fine.</summary>
    public static string CheckInputs(ICollection<KeyValuePair<int, int>> items, IList<string> furniture)
    {
        if (items == null || furniture == null)
            return "The item and furniture ingredients must not be null.";
        if (items.Count > MaxInputs)
            return $"A blueprint takes at most {MaxInputs} items.";
        foreach (var item in items)
        {
            if (item.Key < 2)
                return $"Item id {item.Key} is not an ingredient (money and 'nothing' can't be).";
            if (item.Value < 1 || item.Value > MaxInputAmount)
                return $"Item {item.Key}: the amount must be 1 to {MaxInputAmount}.";
        }
        if (furniture.Count > MaxFurnitureInputs)
            return $"A blueprint takes at most {MaxFurnitureInputs} pieces of furniture.";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string piece in furniture)
        {
            if (string.IsNullOrEmpty(piece))
                return "A furniture ingredient is empty.";
            if (!seen.Add(piece))
                return $"The furniture ingredient {piece} is listed twice.";
            var kind = Classify(piece);
            if (kind == FurnitureIdKind.Blueprint)
                return $"The furniture ingredient {piece} is a blueprint.";
            if (kind == FurnitureIdKind.Malformed)
                return $"The furniture ingredient {piece} is not a valid id.";
        }
        return null;
    }

    /// <summary>Checks the blueprint pools: up to 8 distinct names ("" for the general pool, "Start" for the first finds).</summary>
    public static string CheckPools(IList<string> pools)
    {
        if (pools == null)
            return "The pools must not be null.";
        if (pools.Count > MaxPools)
            return $"A blueprint can be in at most {MaxPools} pools.";
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string pool in pools)
        {
            if (pool == null)
                return "A pool name is null.";
            if (pool.Length > MaxPoolName)
                return $"Pool names are at most {MaxPoolName} characters.";
            if (!seen.Add(pool))
                return $"The pool '{pool}' is listed twice.";
        }
        return null;
    }

    public static string CheckLoot(string category, int weight)
    {
        if (string.IsNullOrEmpty(category))
            return "No loot category given.";
        if (weight < 1 || weight > MaxLootWeight)
            return $"The loot weight must be 1 to {MaxLootWeight}.";
        return null;
    }

    /// <summary>
    /// Picks an entry by weight: <paramref name="roll"/> in [0, total weight). Entries of weight 0 or less are
    /// never picked. Returns the index, or -1 when the roll is out of range or nothing has weight.
    /// </summary>
    public static int PickWeighted(IList<int> weights, int roll)
    {
        if (weights == null)
            return -1;
        long total = TotalWeight(weights);
        if (roll < 0 || roll >= total)
            return -1;
        long edge = 0;
        for (int i = 0; i < weights.Count; i++)
        {
            if (weights[i] <= 0)
                continue;
            edge += weights[i];
            if (roll < edge)
                return i;
        }
        return -1;
    }

    /// <summary>The sum of the positive weights.</summary>
    public static long TotalWeight(IList<int> weights)
    {
        long total = 0;
        if (weights == null)
            return 0;
        foreach (int weight in weights)
        {
            if (weight > 0)
                total += weight;
        }
        return total;
    }

    /// <summary>A valid mod id or key: 1 to 64 letters, digits, '.', '_' or '-'.</summary>
    public static bool ValidPart(string part)
    {
        if (string.IsNullOrEmpty(part) || part.Length > MaxKey)
            return false;
        foreach (char c in part)
        {
            if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '_' || c == '-'))
                return false;
        }
        return true;
    }

    private static bool Split(string id, out string modId, out string key)
    {
        modId = null;
        key = null;
        if (!IsModId(id))
            return false;
        int slash = id.IndexOf('/', Prefix.Length);
        if (slash < 0)
            return false;
        string mod = id.Substring(Prefix.Length, slash - Prefix.Length);
        string name = id.Substring(slash + 1);
        if (!ValidPart(mod) || !ValidPart(name))
            return false;
        modId = mod;
        key = name;
        return true;
    }
}
