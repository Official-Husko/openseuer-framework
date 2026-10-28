using System;
using System.Text;

namespace ObenFramework.Items;

/// <summary>
/// The id block reserved for items and recipes added by mods. The game's own items stop at 300050;
/// the block ends below 2^24 so an id survives a round trip through a float, which some of the
/// game's dialogue and UI code does.
/// </summary>
public static class ReservedIds
{
    public const int Min = 1_000_000;
    public const int Max = 16_000_000;

    public static bool Contains(int id) => id >= Min && id <= Max;

    /// <summary>
    /// A stable id for a mod's own key, for example <c>For("mymod", "rusty_key")</c>. The same pair always
    /// gives the same id, so saves keep working between sessions. Two different keys can in theory
    /// hash to the same id; registering the second one throws, and renaming its key fixes it.
    /// </summary>
    public static int For(string modId, string key)
    {
        if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(key))
            throw new ArgumentException("A mod id and a key are both needed.");

        // FNV-1a over the lowercase text, so the result doesn't depend on runtime string hashing.
        uint hash = 2166136261;
        foreach (byte b in Encoding.UTF8.GetBytes((modId + ":" + key).ToLowerInvariant()))
            hash = (hash ^ b) * 16777619;
        return Min + (int)(hash % (uint)(Max - Min + 1));
    }
}
