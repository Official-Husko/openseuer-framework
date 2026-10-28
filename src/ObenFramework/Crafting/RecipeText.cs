using System;
using System.Collections.Generic;

namespace ObenFramework.Crafting;

/// <summary>
/// Text and filter rules for recipe lists. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class RecipeText
{
    /// <summary>True when <paramref name="name"/> contains <paramref name="text"/>, ignoring case. An empty search matches everything.</summary>
    public static bool Contains(string name, string text)
    {
        if (string.IsNullOrEmpty(text))
            return true;
        return name != null && name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Runs every filter on an item; it is kept only if all of them keep it. A filter that throws counts as
    /// keeping it (one broken mod must not empty the list) and is reported through <paramref name="failed"/>.
    /// </summary>
    public static bool KeepAll<T>(IReadOnlyList<Func<T, bool>> filters, T item, Action<Exception> failed)
    {
        for (int i = 0; i < filters.Count; i++)
        {
            bool keep;
            try
            {
                keep = filters[i](item);
            }
            catch (Exception e)
            {
                failed?.Invoke(e);
                keep = true;
            }
            if (!keep)
                return false;
        }
        return true;
    }
}
