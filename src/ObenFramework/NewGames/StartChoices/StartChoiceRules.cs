// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.NewGames;

/// <summary>
/// What the game derives from the start choices (<c>StartManager.GetStartRelationShip</c>): the first
/// family or partner choice decides the relationship. No Unity types, so it is unit tested on its own.
/// </summary>
internal static class StartChoiceRules
{
    public const string Single = "Single";
    public const string Married = "Married";
    public const string InRelationship = "In a relationship";

    public static string Relationship(IEnumerable<string> ids)
    {
        if (ids == null)
            return Single;
        foreach (var id in ids)
        {
            if (id == "Family")
                return Married;
            if (id == "Girlfriend" || id == "Boyfriend")
                return InRelationship;
        }
        return Single;
    }
}
