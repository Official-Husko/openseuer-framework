// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;
using ObenFramework.Events;

namespace ObenFramework.NewGames;

/// <summary>One of the backstory choices made at the start of a new game (the cards in the first apartment).</summary>
public sealed class StartChoiceInfo
{
    internal StartChoiceInfo(StartSelection.Selection selection)
    {
        Id = selection.id;
        Title = selection.title;
        Description = selection.description;
        Oc = selection.oc;
        Rm = selection.rm;
    }

    /// <summary>The game's id ("Family", "Girlfriend", ...); the Lua function <c>HasSelectedStart(id)</c> takes it.</summary>
    public string Id { get; }

    public string Title { get; }

    public string Description { get; }

    /// <summary>The money the choice gives, in each currency.</summary>
    public int Oc { get; }

    public int Rm { get; }
}

/// <summary>
/// The start choices: the backstory cards a new character picks in the starting apartment. They are kept
/// by the game's <c>StartManager</c> and saved with the game.
/// </summary>
public static class StartChoices
{
    /// <summary>The player picked a start choice. Each handler runs in its own try/catch.</summary>
    public static readonly GameEvent<StartChoiceInfo> Chosen = new GameEvent<StartChoiceInfo>(nameof(Chosen));

    /// <summary>The start choices exist in this scene (a gameplay scene).</summary>
    public static bool IsAvailable => StartManager.instance != null;

    /// <summary>The choices made so far, in the order they were picked.</summary>
    public static List<StartChoiceInfo> Selected()
    {
        var result = new List<StartChoiceInfo>();
        var manager = StartManager.instance;
        if (manager == null || manager.selectedSelections == null)
            return result;
        foreach (var selection in manager.selectedSelections)
        {
            if (selection != null)
                result.Add(new StartChoiceInfo(selection));
        }
        return result;
    }

    /// <summary>True when a choice with this id was picked, like the game's <c>HasSelectedStart</c>.</summary>
    public static bool Has(string id)
    {
        var manager = StartManager.instance;
        if (manager == null || manager.selectedSelections == null || id == null)
            return false;
        foreach (var selection in manager.selectedSelections)
        {
            if (selection != null && selection.id == id)
                return true;
        }
        return false;
    }

    /// <summary>"Single", "Married" or "In a relationship", worked out from the choices the way the game does.</summary>
    public static string Relationship
    {
        get
        {
            var manager = StartManager.instance;
            if (manager == null || manager.selectedSelections == null)
                return StartChoiceRules.Single;
            var ids = new List<string>(manager.selectedSelections.Count);
            foreach (var selection in manager.selectedSelections)
            {
                if (selection != null)
                    ids.Add(selection.id);
            }
            return StartChoiceRules.Relationship(ids);
        }
    }

    internal static void Raise(StartSelection.Selection selection) => Chosen.Raise(new StartChoiceInfo(selection), GameEvents.Log);
}
