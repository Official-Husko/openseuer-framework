// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using Achievement = Achievements.Achievement;

namespace ObenFramework.Player;

/// <summary>One of the game's achievements, from <see cref="GameAchievements.All"/>.</summary>
public sealed class AchievementInfo
{
    internal AchievementInfo(string id, float progress, float maxProgress, bool unlocked, bool enabled)
    {
        Id = id;
        Progress = progress;
        MaxProgress = maxProgress;
        Unlocked = unlocked;
        Enabled = enabled;
    }

    /// <summary>The Steam id, for example "ENTER_OTHER_SIDE".</summary>
    public string Id { get; }

    public float Progress { get; }

    /// <summary>0 for an achievement without progress (it unlocks in one go).</summary>
    public float MaxProgress { get; }

    public bool Unlocked { get; }

    /// <summary>False for achievements switched off for this game (they can't unlock).</summary>
    public bool Enabled { get; }
}

/// <summary>An item that counts towards an achievement when picked up or consumed.</summary>
public readonly struct AchievementItem
{
    public AchievementItem(int itemId, string achievementId, bool onConsumed)
    {
        ItemId = itemId;
        AchievementId = achievementId;
        OnConsumed = onConsumed;
    }

    public int ItemId { get; }
    public string AchievementId { get; }
    public bool OnConsumed { get; }
}

/// <summary>
/// The game's achievements: their state, unlocking one and adding progress, and which items count
/// towards which. These are the player's Steam achievements, so unlocking one here unlocks it on Steam
/// as well; only do that for something the player really did.
/// </summary>
public static class GameAchievements
{
    private static readonly AccessTools.FieldRef<Achievement, float> ProgressField =
        AccessTools.FieldRefAccess<Achievement, float>("progress");
    private static readonly AccessTools.FieldRef<Achievement, float> MaxProgressField =
        AccessTools.FieldRefAccess<Achievement, float>("maxprogress");
    private static readonly AccessTools.FieldRef<Achievement, bool> UnlockedField =
        AccessTools.FieldRefAccess<Achievement, bool>("_triggered");
    private static readonly AccessTools.FieldRef<Achievements, bool> Initialized =
        AccessTools.FieldRefAccess<Achievements, bool>("initialized");

    /// <summary>Every achievement, empty before the game set them up.</summary>
    public static List<AchievementInfo> All()
    {
        var all = new List<AchievementInfo>();
        var game = Achievements.instance;
        if (game == null || game.achievements == null)
            return all;
        foreach (var entry in game.achievements)
        {
            if (entry != null)
                all.Add(Describe(entry));
        }
        return all;
    }

    public static AchievementInfo Get(string id)
    {
        var entry = Entry(id);
        return entry != null ? Describe(entry) : null;
    }

    public static bool IsUnlocked(string id)
    {
        var entry = Entry(id);
        return entry != null && UnlockedField(entry);
    }

    /// <summary>Unlocks an achievement (on Steam too). False when it is unknown, switched off or the game isn't ready.</summary>
    public static bool Unlock(string id)
    {
        if (!CanChange(id))
            return false;
        Achievements.instance.TriggerAchievement(id);
        return IsUnlocked(id);
    }

    /// <summary>Adds progress to an achievement that counts up; it unlocks when the progress reaches the maximum.</summary>
    public static bool AddProgress(string id, float amount)
    {
        if (!(amount >= 0f) || float.IsInfinity(amount) || !CanChange(id))
            return false;
        Achievements.instance.TriggerAchievement(id, amount, false);
        return true;
    }

    /// <summary>Sets the progress of an achievement that counts up.</summary>
    public static bool SetProgress(string id, float value)
    {
        if (!(value >= 0f) || float.IsInfinity(value) || !CanChange(id))
            return false;
        Achievements.instance.TriggerAchievement(id, value, true);
        return true;
    }

    /// <summary>The items that count towards achievements when picked up or (with OnConsumed) consumed.</summary>
    public static List<AchievementItem> Items()
    {
        var items = new List<AchievementItem>();
        var list = ItemAchievementList.instance;
        if (list == null)
            return items;
        AddItems(items, list.achievementItems, false);
        AddItems(items, list.achievementItemsOnConsumed, true);
        return items;
    }

    private static bool CanChange(string id)
    {
        var game = Achievements.instance;
        if (game == null || game.globalDisable || !Initialized(game) || SaveGate.IsBusy)
            return false;
        var entry = Entry(id);
        return entry != null && entry.enabled;
    }

    private static Achievement Entry(string id)
    {
        var game = Achievements.instance;
        if (game == null || game.achievements == null || string.IsNullOrEmpty(id))
            return null;
        foreach (var entry in game.achievements)
        {
            if (entry != null && entry.name == id)
                return entry;
        }
        return null;
    }

    private static AchievementInfo Describe(Achievement entry) =>
        new AchievementInfo(entry.name, ProgressField(entry), MaxProgressField(entry), UnlockedField(entry), entry.enabled);

    private static void AddItems(List<AchievementItem> items, List<ItemAchievementList.AchievementItem> source, bool onConsumed)
    {
        if (source == null)
            return;
        foreach (var entry in source)
        {
            if (entry != null && entry.item != null)
                items.Add(new AchievementItem(entry.item.ID, entry.achievementID, onConsumed));
        }
    }
}
