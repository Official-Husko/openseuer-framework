using System;
using System.Collections.Generic;

namespace ObenFramework.Items;

/// <summary>
/// Lookup tables over <see cref="ItemDatabase.database"/>. The game scans the list linearly for
/// every lookup; this keeps an id table, a title table and a category table. It rebuilds itself
/// when the database list is replaced (a reload) or changes size (a mod added or removed items).
/// </summary>
internal static class ItemIndex
{
    private static readonly Dictionary<int, Item> ById = new Dictionary<int, Item>();
    private static readonly Dictionary<string, List<Item>> ByTitle = new Dictionary<string, List<Item>>(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, List<Item>> ByCategory = new Dictionary<string, List<Item>>(StringComparer.Ordinal);
    private static List<Item> _source;
    private static int _count = -1;

    public static IReadOnlyList<Item> All
    {
        get
        {
            Ensure();
            return _source;
        }
    }

    public static Item Get(int id)
    {
        Ensure();
        return ById.TryGetValue(id, out var item) ? item : null;
    }

    public static List<Item> Titled(string title)
    {
        Ensure();
        return ByTitle.TryGetValue(title, out var items) ? items : null;
    }

    public static List<Item> InCategory(string category)
    {
        Ensure();
        return ByCategory.TryGetValue(category, out var items) ? items : null;
    }

    public static void Invalidate() => _count = -1;

    private static void Ensure()
    {
        var database = ItemDatabase.database;
        if (ReferenceEquals(database, _source) && database.Count == _count)
            return;

        ById.Clear();
        ByTitle.Clear();
        ByCategory.Clear();
        _source = database;
        _count = database.Count;
        foreach (var item in database)
        {
            // The first item with an id wins, like ItemDatabase.FetchItemByID.
            if (!ById.ContainsKey(item.ID))
                ById[item.ID] = item;
            if (!string.IsNullOrEmpty(item.Title))
                Add(ByTitle, item.Title, item);
            if (item.Categories != null)
            {
                foreach (var category in item.Categories)
                    Add(ByCategory, category, item);
            }
        }
    }

    private static void Add(Dictionary<string, List<Item>> table, string key, Item item)
    {
        if (key == null)
            return;
        if (!table.TryGetValue(key, out var list))
            table[key] = list = new List<Item>(1);
        list.Add(item);
    }
}
