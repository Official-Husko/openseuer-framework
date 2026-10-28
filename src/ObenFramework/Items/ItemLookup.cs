using System;
using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>
/// Finds items in the game's item database. Titles are not unique (there are two "Turnip" items), so
/// anything that looks up by title returns a list; use the id when you need exactly one item.
/// </summary>
public static class ItemLookup
{
    private static readonly IReadOnlyList<Item> None = Array.Empty<Item>();

    /// <summary>True once the game has loaded its item database.</summary>
    public static bool IsLoaded => ItemDatabase.database.Count > 0;

    public static int Count => ItemDatabase.database.Count;

    /// <summary>Every item, vanilla and modded. Don't modify the list.</summary>
    public static IReadOnlyList<Item> All => ItemIndex.All;

    /// <summary>The item with this id, or null.</summary>
    public static Item ById(int id) => ItemIndex.Get(id);

    public static bool Exists(int id) => ItemIndex.Get(id) != null;

    /// <summary>All items whose title is exactly <paramref name="title"/>, ignoring case.</summary>
    public static IReadOnlyList<Item> ByTitle(string title)
    {
        if (string.IsNullOrEmpty(title))
            return None;
        return ItemIndex.Titled(title) ?? None;
    }

    /// <summary>
    /// The first item with this exact title, or null. With a duplicated title that's the one with the
    /// lowest position in the database, which is not necessarily the one you mean.
    /// </summary>
    public static Item FirstByTitle(string title)
    {
        var items = ByTitle(title);
        return items.Count > 0 ? items[0] : null;
    }

    /// <summary>Every item that has this category tag, such as "Food" or "Liquid".</summary>
    public static IReadOnlyList<Item> ByCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
            return None;
        return ItemIndex.InCategory(category) ?? None;
    }

    /// <summary>
    /// Items whose title contains <paramref name="text"/>, ignoring case. Exact matches come first,
    /// then titles that start with the text, then the rest in database order.
    /// </summary>
    public static List<Item> Search(string text, int max = int.MaxValue)
    {
        var result = new List<Item>();
        if (string.IsNullOrWhiteSpace(text) || max <= 0)
            return result;

        text = text.Trim();
        var starts = new List<Item>();
        var contains = new List<Item>();
        foreach (var item in ItemIndex.All)
        {
            string title = item.Title;
            if (string.IsNullOrEmpty(title))
                continue;
            if (title.Equals(text, StringComparison.OrdinalIgnoreCase))
                result.Add(item);
            else if (title.StartsWith(text, StringComparison.OrdinalIgnoreCase))
                starts.Add(item);
            else if (title.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
                contains.Add(item);
        }

        result.AddRange(starts);
        result.AddRange(contains);
        if (result.Count > max)
            result.RemoveRange(max, result.Count - max);
        return result;
    }
}
