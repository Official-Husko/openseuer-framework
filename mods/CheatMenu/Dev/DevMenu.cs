// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Controls;

namespace CheatMenu.Dev;

/// <summary>A mod's own part of the developer menu, built like any kit window.</summary>
public sealed class DevSection
{
    internal DevSection(string modId, string title, Action<UiBuilder> build)
    {
        ModId = modId;
        Title = title;
        Build = build;
    }

    public string ModId { get; }
    public string Title { get; }
    internal Action<UiBuilder> Build { get; }
}

/// <summary>A named value a mod wants to see updating in the developer menu.</summary>
public sealed class DevWatch
{
    internal DevWatch(string modId, string name, Func<string> read)
    {
        ModId = modId;
        Name = name;
        Read = read;
    }

    public string ModId { get; }
    public string Name { get; }
    internal Func<string> Read { get; }
}

/// <summary>
/// The developer menu of the Cheat Menu (F8 once its "Developer menu" feature is on): quests, global flags,
/// watches on live values and the session and story state. Other mods add their own sections and watches
/// here; each goes away when its mod unloads. Changes made from the menu mark the save as dev-modified.
/// </summary>
public static class DevMenu
{
    public const int MaxTitle = 64;
    public const int MaxEntries = 100;

    private static readonly List<DevSection> SectionList = new List<DevSection>();
    private static readonly List<DevWatch> WatchList = new List<DevWatch>();

    public static IReadOnlyList<DevSection> Sections => SectionList;
    public static IReadOnlyList<DevWatch> Watches => WatchList;

    /// <summary>The developer menu changed something in the loaded save.</summary>
    public static bool SaveIsDevModified => DevMarks.Modified;

    /// <summary>
    /// Adds a section to the Mods part of the menu for as long as <paramref name="owner"/> is loaded. The same
    /// owner and title again replaces the earlier one.
    /// </summary>
    public static void AddSection(ObenMod owner, string title, Action<UiBuilder> build)
    {
        Check(owner, title);
        if (build == null) throw new ArgumentNullException(nameof(build));
        var section = new DevSection(owner.Info.Id, title, build);
        Replace(SectionList, s => s.ModId == section.ModId && s.Title == title, section);
        owner.Track(Disposable.Create(() => SectionList.Remove(section)));
    }

    /// <summary>
    /// Adds a value shown with the watches, read four times a second while the menu shows them. The same
    /// owner and name again replaces the earlier one.
    /// </summary>
    public static void AddWatch(ObenMod owner, string name, Func<string> read)
    {
        Check(owner, name);
        if (read == null) throw new ArgumentNullException(nameof(read));
        var watch = new DevWatch(owner.Info.Id, name, read);
        Replace(WatchList, w => w.ModId == watch.ModId && w.Name == name, watch);
        owner.Track(Disposable.Create(() => WatchList.Remove(watch)));
    }

    internal static void Clear()
    {
        SectionList.Clear();
        WatchList.Clear();
    }

    private static void Check(ObenMod owner, string title)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrWhiteSpace(title) || title.Length > MaxTitle)
            throw new ArgumentException($"A title or name is 1 to {MaxTitle} characters and not blank.");
    }

    private static void Replace<T>(List<T> list, Predicate<T> same, T entry)
    {
        int index = list.FindIndex(same);
        if (index >= 0)
        {
            list[index] = entry;
            return;
        }
        if (list.Count >= MaxEntries)
            throw new InvalidOperationException($"The developer menu holds at most {MaxEntries} of these.");
        list.Add(entry);
    }
}
