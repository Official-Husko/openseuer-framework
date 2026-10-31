// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Events;
using ObenFramework.Saves;
using UnityEngine;

namespace ObenFramework.Player;

/// <summary>A note or book in the player's notes, from <see cref="PlayerNotes.Held"/>.</summary>
public sealed class NoteEntry
{
    internal NoteEntry(CollectedNoteInfo entry)
    {
        IsBook = entry.book != null;
        Id = IsBook ? entry.book.Id ?? "" : entry.note != null ? entry.note.Id ?? "" : "";
        Title = entry.DisplayTitle ?? "";
        Category = IsBook ? entry.book.category : entry.note != null ? entry.note.category : default;
        Read = entry.read;
        PagesCollected = IsBook ? entry.collectedPages.Count : 1;
        PageCount = IsBook && entry.book.pages != null ? entry.book.pages.Count : 1;
    }

    /// <summary>The game's id (its own id, else its asset path, else its title).</summary>
    public string Id { get; }
    public string Title { get; }
    public NoteInfo.NoteCategory Category { get; }
    public bool IsBook { get; }

    /// <summary>Opened on the notes page since it (or its newest page) was collected.</summary>
    public bool Read { get; }

    public int PagesCollected { get; }
    public int PageCount { get; }
}

/// <summary>A note, a book or a book page reached the player's notes, from <see cref="PlayerNotes.Collected"/>.</summary>
public readonly struct NoteCollected
{
    public NoteCollected(NoteInfo note, bool newBook)
    {
        Id = note.Id ?? "";
        Title = note.title ?? "";
        BookId = note.book != null ? note.book.Id ?? "" : null;
        NewBook = newBook;
    }

    /// <summary>The note's (or the page's) id.</summary>
    public string Id { get; }
    public string Title { get; }

    /// <summary>The book the page belongs to, or null for a loose note.</summary>
    public string BookId { get; }

    /// <summary>The first page of a book the player didn't have.</summary>
    public bool NewBook { get; }

    public bool IsBookPage => BookId != null;
}

/// <summary>
/// The player's notes and books (the Notes page of the menu): what they hold, what they have read, give
/// them a note, and hear when one is collected or read. Books are collected page by page.
/// </summary>
public static class PlayerNotes
{
    internal static readonly Func<NoteController, NoteInfo, CollectedNoteInfo> FindNote =
        AccessTools.MethodDelegate<Func<NoteController, NoteInfo, CollectedNoteInfo>>(AccessTools.Method(typeof(NoteController), "FindNote"));
    internal static readonly Func<NoteController, BookInfo, CollectedNoteInfo> FindBook =
        AccessTools.MethodDelegate<Func<NoteController, BookInfo, CollectedNoteInfo>>(AccessTools.Method(typeof(NoteController), "FindBook"));

    /// <summary>A note, a new book or a new page of a book was added to the player's notes.</summary>
    public static readonly GameEvent<NoteCollected> Collected = new GameEvent<NoteCollected>(nameof(Collected));

    /// <summary>The player opened a note or book on the notes page for the first time since it (or a page) arrived.</summary>
    public static readonly GameEvent<NoteEntry> Read = new GameEvent<NoteEntry>(nameof(Read));

    /// <summary>Everything in the player's notes, books included. Empty without a player.</summary>
    public static List<NoteEntry> Held()
    {
        var held = new List<NoteEntry>();
        var controller = NoteController.instance;
        if (controller == null || controller.notes == null)
            return held;
        foreach (var entry in controller.notes)
        {
            if (entry != null && (entry.note != null || entry.book != null))
                held.Add(new NoteEntry(entry));
        }
        return held;
    }

    /// <summary>The held note or book with this id, or null.</summary>
    public static NoteEntry Get(string id)
    {
        foreach (var entry in Held())
        {
            if (entry.Id == id)
                return entry;
        }
        return null;
    }

    public static bool Has(string id) => Get(id) != null;

    public static bool IsRead(string id)
    {
        var entry = Get(id);
        return entry != null && entry.Read;
    }

    /// <summary>
    /// The notes and book pages the game has loaded right now (those referenced by the level and the
    /// player's notes). The game loads the rest on demand by asset path, so this is not every note.
    /// </summary>
    public static List<NoteInfo> Loaded()
    {
        var loaded = new List<NoteInfo>();
        foreach (var note in Resources.FindObjectsOfTypeAll<NoteInfo>())
        {
            if (note != null)
                loaded.Add(note);
        }
        return loaded;
    }

    /// <summary>
    /// Gives the player a note or book page, by its id or its asset path, like picking it up. False when
    /// no such note is found, the player has it already, or there is no player.
    /// </summary>
    public static bool Give(string idOrPath, bool notify = true)
    {
        if (string.IsNullOrEmpty(idOrPath))
            throw new ArgumentException("Give a note id or asset path.", nameof(idOrPath));
        var controller = NoteController.instance;
        if (controller == null || SaveGate.IsBusy)
            return false;
        var note = Resolve(idOrPath);
        if (note == null || Holds(controller, note))
            return false;
        controller.AddNote(note, notify);
        return Holds(controller, note);
    }

    /// <summary>
    /// Opens the menu's notes page at a held note, book or book page, as reading one from the inventory
    /// does. A book opens at the page it was left on. False without a player, while saving or loading, when
    /// the player doesn't hold it, or when the game didn't open the page (another menu or a conversation was
    /// up, where the game's own reading does nothing).
    /// </summary>
    public static bool Open(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("Give the id of a held note, book or page.", nameof(id));
        var controller = NoteController.instance;
        if (controller == null || controller.notes == null || SaveGate.IsBusy)
            return false;
        var page = HeldPage(controller, id);
        if (page == null)
            return false;
        controller.ReadNote(page);
        var ui = GameUIController.instance;
        var pages = UIPageButtons.instance;
        return ui != null && ui.IsMenuVisible && pages != null && pages.currentPage == MenuPageNames.Notes;
    }

    private static NoteInfo HeldPage(NoteController controller, string id)
    {
        foreach (var entry in controller.notes)
        {
            if (entry == null)
                continue;
            if (entry.book == null)
            {
                if (entry.note != null && entry.note.Id == id)
                    return entry.note;
                continue;
            }
            if (entry.book.Id == id)
                return entry.CurrentPage;
            foreach (var page in entry.collectedPages)
            {
                if (page != null && page.Id == id)
                    return page;
            }
        }
        return null;
    }

    /// <summary>Whether the player's notes hold this note (or this page of its book).</summary>
    internal static bool Holds(NoteController controller, NoteInfo note)
    {
        if (note.book == null)
            return FindNote(controller, note) != null;
        var book = FindBook(controller, note.book);
        return book != null && book.collectedPages.Exists(page => page != null && page.Compare(note));
    }

    private static NoteInfo Resolve(string idOrPath)
    {
        foreach (var note in Resources.FindObjectsOfTypeAll<NoteInfo>())
        {
            if (note != null && (note.Id == idOrPath || note.addressableAssetPath == idOrPath))
                return note;
        }
        // Not loaded: an asset path can still be loaded the way the game loads saved notes. Plain ids
        // can't, and asking the asset system for an unknown key logs an error, so only paths are tried.
        if (idOrPath.IndexOf('/') < 0)
            return null;
        try
        {
            return SavableScriptableObject.LoadFromPath(idOrPath) as NoteInfo;
        }
        catch (Exception e)
        {
            GameEvents.Log.Warn($"No note at '{idOrPath}': {e.Message}");
            return null;
        }
    }
}
