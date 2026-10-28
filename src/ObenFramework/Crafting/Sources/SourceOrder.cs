using System.Collections.Generic;

namespace ObenFramework.Crafting;

/// <summary>
/// The order extra ingredient sources are used in: higher priority first, then the order they were
/// registered in. A stable insertion sort, because the lists are short and the order must not depend on
/// the sort algorithm. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class SourceOrder
{
    public static void Sort<T>(List<T> items, System.Func<T, int> priority, System.Func<T, int> order)
    {
        for (int i = 1; i < items.Count; i++)
        {
            var item = items[i];
            int j = i - 1;
            while (j >= 0 && After(items[j], item, priority, order))
            {
                items[j + 1] = items[j];
                j--;
            }
            items[j + 1] = item;
        }
    }

    /// <summary>True when <paramref name="a"/> must come after <paramref name="b"/>.</summary>
    private static bool After<T>(T a, T b, System.Func<T, int> priority, System.Func<T, int> order)
    {
        int pa = priority(a), pb = priority(b);
        if (pa != pb)
            return pa < pb;
        return order(a) > order(b);
    }
}
