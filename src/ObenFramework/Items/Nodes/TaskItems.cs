// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.World;

namespace ObenFramework.Items;

/// <summary>
/// One of the player's task items: things kept in the journal's item list instead of the inventory (keys,
/// keycards, recipes, blueprints, quest items).
/// </summary>
public readonly struct TaskItemInfo
{
    public TaskItemInfo(string id, string name, string type, int amount, string details)
    {
        Id = id;
        Name = name;
        Type = type;
        Amount = amount;
        Details = details;
    }

    /// <summary>The task item's id (recipes are <c>Recipe_{id}</c>).</summary>
    public string Id { get; }

    public string Name { get; }

    /// <summary>The <c>TaskItem.Type</c> name: TaskItems, Keys, Keycards, Recipes...</summary>
    public string Type { get; }

    public int Amount { get; }

    public string Details { get; }
}

/// <summary>A task item was added, changed amount or removed, raised by <see cref="TaskItems.Changed"/>.</summary>
public readonly struct TaskItemChange
{
    public TaskItemChange(string id, string name, string type, int oldAmount, int newAmount, bool held)
    {
        Id = id;
        Name = name;
        Type = type;
        OldAmount = oldAmount;
        NewAmount = newAmount;
        Held = held;
    }

    public string Id { get; }

    public string Name { get; }

    public string Type { get; }

    public int OldAmount { get; }

    public int NewAmount { get; }

    /// <summary>The player still has it afterwards; false when it was removed.</summary>
    public bool Held { get; }
}

/// <summary>The player's task items: listing them and hearing them change.</summary>
public static class TaskItems
{
    /// <summary>A task item was added, removed or changed amount. Not raised while a save loads.</summary>
    public static readonly WorldEvent<TaskItemChange> Changed = new WorldEvent<TaskItemChange>(nameof(Changed));

    public static List<TaskItemInfo> All()
    {
        var list = new List<TaskItemInfo>();
        var manager = TaskItemsManager.instance;
        if (manager == null)
            return list;
        foreach (var entry in manager.taskItems)
        {
            if (entry != null && entry.taskItem != null)
                list.Add(Describe(entry.taskItem, entry.amount));
        }
        return list;
    }

    public static bool Has(string id) => TaskItemsManager.instance != null && TaskItemsManager.instance.PlayerHasTaskItemId(id);

    /// <summary>How many of a task item the player has, 0 when none.</summary>
    public static int Amount(string id)
    {
        var manager = TaskItemsManager.instance;
        if (manager == null || string.IsNullOrEmpty(id))
            return 0;
        foreach (var entry in manager.taskItems)
        {
            if (entry != null && entry.taskItem != null && entry.taskItem.Id == id)
                return entry.amount;
        }
        return 0;
    }

    internal static TaskItemInfo Describe(TaskItem item, int amount) =>
        new TaskItemInfo(item.Id, item.itemName, item.itemType.ToString(), amount, item.itemDetails ?? "");
}
