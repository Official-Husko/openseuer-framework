namespace ObenFramework.NewGames;

/// <summary>What the player chose on the New Game screen. A copy taken when they confirmed it.</summary>
public sealed class NewGameInfo
{
    internal NewGameInfo(string firstName, string lastName, int age, string gender)
    {
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

    internal static NewGameInfo Capture()
    {
        var identity = PlayerIdentityMainMenu.identity;
        if (identity == null)
            return new NewGameInfo(null, null, 0, null);
        return new NewGameInfo(identity.firstName, identity.lastName, identity.age, identity.gender);
    }
}
