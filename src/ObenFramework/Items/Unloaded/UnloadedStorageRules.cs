// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using System.IO;

namespace ObenFramework.Items;

/// <summary>What happened when a queued change met its loaded container.</summary>
internal enum UnloadedApply
{
    Applied,
    Gone,
    Refused,
}

/// <summary>
/// The rules of the unloaded storage queue: what a valid change is, how changes merge, how many a mod may
/// keep, and when a change is part of the saved scene so it can leave the queue. No Unity types, so it is
/// unit tested on its own.
/// </summary>
internal static class UnloadedStorageRules
{
    public const int MaxEntriesPerMod = 256;
    public const int MaxCount = 100_000;
    public const int MaxModLength = 64;
    public const int MaxSceneLength = 128;
    public const int MaxGuidLength = 128;

    /// <summary>Ids below this are the two currencies, which aren't stacks.</summary>
    public const int FirstItemId = 2;

    public const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>How long a change waits for its container to appear after the scene loaded.</summary>
    public const float RetrySeconds = 10f;

    public static bool ValidMod(string modId) => !string.IsNullOrEmpty(modId) && modId.Length <= MaxModLength;

    public static bool ValidScene(string scene) =>
        !string.IsNullOrEmpty(scene) && scene.Length <= MaxSceneLength && scene.IndexOfAny(Path.GetInvalidFileNameChars()) < 0
        && scene.IndexOfAny(Separators) < 0 && !scene.Contains("..");

    public static bool ValidGuid(string guid) => !string.IsNullOrEmpty(guid) && guid.Length <= MaxGuidLength;

    public static bool ValidItem(int itemId) => itemId >= FirstItemId;

    public static bool ValidCount(int count) => count >= 1 && count <= MaxCount;

    public static bool Valid(UnloadedStorageEntry entry) =>
        entry != null && ValidMod(entry.Mod) && ValidScene(entry.Scene) && ValidGuid(entry.Guid) && ValidItem(entry.ItemId) && ValidCount(entry.Count);

    /// <summary>A copy of a loaded queue with invalid entries and each mod's entries past the limit dropped.</summary>
    public static UnloadedStorageFile Clean(UnloadedStorageFile file)
    {
        var clean = new UnloadedStorageFile();
        if (file == null || file.Entries == null)
            return clean;
        foreach (var entry in file.Entries)
        {
            if (Valid(entry) && CountOf(clean.Entries, entry.Mod) < MaxEntriesPerMod)
                clean.Entries.Add(entry);
        }
        return clean;
    }

    public static int CountOf(List<UnloadedStorageEntry> entries, string modId)
    {
        int count = 0;
        foreach (var entry in entries)
        {
            if (entry.Mod == modId)
                count++;
        }
        return count;
    }

    /// <summary>Two changes in a row to the same container, item and direction become one, as long as the total stays valid.</summary>
    public static bool CanMerge(UnloadedStorageEntry last, UnloadedStorageEntry next) =>
        last.Mod == next.Mod && last.Scene == next.Scene && last.Guid == next.Guid && last.ItemId == next.ItemId
        && last.Give == next.Give && (long)last.Count + next.Count <= MaxCount;

    /// <summary>
    /// Adds a change to the end of the queue, merging it into the last entry when it can. An entry that was
    /// already applied (in <paramref name="settled"/>) is never merged into. False when the mod is at its limit.
    /// </summary>
    public static bool Append(List<UnloadedStorageEntry> entries, UnloadedStorageEntry entry, ICollection<UnloadedStorageEntry> settled)
    {
        var last = entries.Count == 0 ? null : entries[entries.Count - 1];
        if (last != null && !settled.Contains(last) && CanMerge(last, entry))
        {
            last.Count += entry.Count;
            return true;
        }
        if (CountOf(entries, entry.Mod) >= MaxEntriesPerMod)
            return false;
        entries.Add(entry);
        return true;
    }

    public static UnloadedApply Decide(bool found, bool done) =>
        !found ? UnloadedApply.Gone : done ? UnloadedApply.Applied : UnloadedApply.Refused;

    public static bool Expired(float since, float now) => now - since >= RetrySeconds;

    /// <summary>
    /// An applied change is in the scene's state once that scene is saved, so a save taken while it is the
    /// active scene no longer needs to carry it.
    /// </summary>
    public static bool Durable(UnloadedStorageEntry entry, bool settled, string activeScene) => settled && entry.Scene == activeScene;

    private static readonly char[] Separators = { '/', '\\' };
}
