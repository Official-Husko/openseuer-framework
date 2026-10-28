using System.Collections.Generic;

namespace ObenFramework.NewGames;

/// <summary>What the player chose on the New Game screen. A copy taken when they confirmed it.</summary>
public sealed class NewGameInfo
{
    internal NewGameInfo(string firstName, string lastName, int age, string gender, DifficultySettings difficulty,
        string difficultyPreset, List<NewGameOptionValue> options)
    {
        Difficulty = difficulty;
        DifficultyPreset = difficultyPreset;
        Options = options ?? new List<NewGameOptionValue>();
        FirstName = firstName;
        LastName = lastName;
        Age = age;
        Gender = gender;
        string full = ((firstName ?? "") + " " + (lastName ?? "")).Trim();
        FullName = full.Length == 0 ? null : full;
    }

    public string FirstName { get; }

    public string LastName { get; }

    /// <summary>First and last name with a space between, or null when both are empty.</summary>
    public string FullName { get; }

    public int Age { get; }

    /// <summary>"male" or "female", as the game stores it.</summary>
    public string Gender { get; }

    /// <summary>The difficulty the game starts with, or null when the game had none selected.</summary>
    public DifficultySettings Difficulty { get; }

    /// <summary>The mod difficulty preset ("modId.id") picked on the screen, or null.</summary>
    public string DifficultyPreset { get; }

    /// <summary>The new game option values the game starts with: every registered option, picked or default.</summary>
    public IReadOnlyList<NewGameOptionValue> Options { get; }

    internal static NewGameInfo Capture(DifficultyPreset preset)
    {
        var identity = PlayerIdentityMainMenu.identity;
        var options = NewGameOptions.Values();
        if (identity == null)
            return new NewGameInfo(null, null, 0, null, null, preset?.Key, options);
        var difficulty = identity.selectedDifficulty != null ? new DifficultySettings(identity.selectedDifficulty) : null;
        return new NewGameInfo(identity.firstName, identity.lastName, identity.age, identity.gender, difficulty, preset?.Key, options);
    }
}
