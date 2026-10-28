using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>
/// Loot pools are plain id lists where an item's weight is how often it is listed. These helpers add and
/// remove weight and read the weights back. No Unity types, so they are unit tested on their own.
/// </summary>
internal static class LootWeights
{
    public const int MaxWeight = 100;

    /// <summary>Lists the item <paramref name="weight"/> more times.</summary>
    public static void Add(List<int> pool, int itemId, int weight)
    {
        for (int i = 0; i < weight; i++)
            pool.Add(itemId);
    }

    /// <summary>Removes up to <paramref name="weight"/> listings of the item, from the end. Returns how many.</summary>
    public static int Remove(List<int> pool, int itemId, int weight)
    {
        int removed = 0;
        for (; removed < weight; removed++)
        {
            int index = pool.LastIndexOf(itemId);
            if (index < 0)
                break;
            pool.RemoveAt(index);
        }
        return removed;
    }

    /// <summary>Each item of the pool once, with how many times it is listed, in first-listed order.</summary>
    public static List<KeyValuePair<int, int>> Count(List<int> pool)
    {
        var order = new List<int>();
        var counts = new Dictionary<int, int>();
        foreach (int id in pool)
        {
            if (counts.TryGetValue(id, out int n))
            {
                counts[id] = n + 1;
            }
            else
            {
                counts[id] = 1;
                order.Add(id);
            }
        }
        var result = new List<KeyValuePair<int, int>>(order.Count);
        foreach (int id in order)
            result.Add(new KeyValuePair<int, int>(id, counts[id]));
        return result;
    }
}
