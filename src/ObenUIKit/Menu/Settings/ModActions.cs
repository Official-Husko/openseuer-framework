// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenUIKit.Menu.Settings;

/// <summary>
/// Marks a public method of a settings class as an action on the Settings page: a button that calls it and
/// then saves the config. The method takes no arguments and returns nothing or a string to show as its result.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class SettingActionAttribute : Attribute
{
    public SettingActionAttribute(string label = null)
    {
        Label = label;
    }

    /// <summary>The button's text; the method name with spaces when left out.</summary>
    public string Label { get; }

    /// <summary>A line of help under the button.</summary>
    public string Description { get; set; }

    /// <summary>Ask "are you sure?" before running it.</summary>
    public bool Confirm { get; set; }

    /// <summary>Actions with the same group are shown together in a card with this title, after the others.</summary>
    public string Group { get; set; }
}

/// <summary>An action a mod put on the Settings page.</summary>
public sealed class ModAction
{
    internal ModAction(string ownerId, string ownerName, string title, Func<string> run, string description, bool confirm,
        string group)
    {
        OwnerId = ownerId;
        OwnerName = ownerName;
        Title = title;
        Run = run;
        Description = description;
        Confirm = confirm;
        Group = string.IsNullOrEmpty(group) ? null : group;
    }

    public string OwnerId { get; }
    public string OwnerName { get; }
    public string Title { get; }
    public string Description { get; }
    public bool Confirm { get; }

    /// <summary>The card the action is shown in, or null.</summary>
    public string Group { get; }

    internal Func<string> Run { get; }
}

/// <summary>
/// Buttons for things a player may want to do once rather than set: clear a cache, reset a quest, rescan a
/// folder. They show on the Settings page of the mod menu under the mod's settings (or on their own when the
/// mod has none), with the text the action returns shown as its result, and the page's search finds them.
/// </summary>
public static class ModActions
{
    private static readonly List<ModAction> Registered = new List<ModAction>();

    /// <summary>Every registered action, in the order they were added.</summary>
    public static IReadOnlyList<ModAction> All => Registered;

    /// <summary>
    /// Adds an action for as long as <paramref name="owner"/> is loaded. <paramref name="run"/> returns a short
    /// result to show ("Removed 12 files"), or null for "Done". With <paramref name="confirm"/> the player is
    /// asked first. Actions with the same <paramref name="group"/> share a card. Registering a title the mod
    /// already used replaces that action in its place; disposing the returned handle removes the action.
    /// </summary>
    public static IDisposable Register(ObenMod owner, string title, Func<string> run, string description = null,
        bool confirm = false, string group = null)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(title))
            throw new ArgumentException("An action needs a title.", nameof(title));
        if (run == null)
            throw new ArgumentNullException(nameof(run));
        var action = new ModAction(owner.Info.Id, owner.Info.Manifest.DisplayName, title, run, description, confirm, group);
        int index = Registered.FindIndex(a => a.OwnerId == action.OwnerId && a.Title == title);
        if (index >= 0)
            Registered[index] = action;
        else
            Registered.Add(action);
        // Only this registration goes: after a replacement, the old handle no longer finds anything to remove.
        var registration = Disposable.Create(() =>
        {
            if (Registered.Remove(action))
                ModMenu.Rebuild();
        });
        owner.Track(registration);
        ModMenu.Rebuild();
        return registration;
    }

    /// <summary>The action a mod registered with this title, or null.</summary>
    public static ModAction Find(string ownerId, string title) =>
        Registered.Find(a => a.OwnerId == ownerId && a.Title == title);

    /// <summary>A mod's actions.</summary>
    public static List<ModAction> Of(string ownerId)
    {
        var list = new List<ModAction>();
        foreach (var action in Registered)
        {
            if (action.OwnerId == ownerId)
                list.Add(action);
        }
        return list;
    }
}
