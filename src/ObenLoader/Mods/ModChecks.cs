namespace ObenLoader.Mods;

/// <summary>
/// Checks on a mod's manifest before it loads: a usable id, and the game version it says it was made
/// for. Plain logic with the game version passed in, so it is tested on its own.
/// </summary>
internal static class ModChecks
{
    /// <summary>
    /// Why the mod must not load, or null. <paramref name="warning"/> is set for a problem that doesn't
    /// stop it (made for another game version without <c>strictGameVersion</c>).
    /// </summary>
    public static string Refusal(ModManifest manifest, string gameVersion, out string warning)
    {
        warning = null;
        string idProblem = ModIds.Problem(manifest.Id);
        if (idProblem != null)
            return $"Invalid id '{manifest.Id}': {idProblem}. Set a valid \"id\" in its mod.json.";

        string game = GameProblem(manifest, gameVersion);
        if (game == null)
            return null;
        if (manifest.StrictGameVersion && VersionRange.TryParse(manifest.GameVersion, out _, out _))
            return game + " It only loads on those versions (strictGameVersion in its mod.json).";
        warning = game + " It may not work right.";
        return null;
    }

    /// <summary>A sentence saying the game version doesn't match the mod's range, or null when it does (or there is none).</summary>
    public static string GameProblem(ModManifest manifest, string gameVersion)
    {
        if (string.IsNullOrWhiteSpace(manifest.GameVersion))
            return null;
        if (!VersionRange.TryParse(manifest.GameVersion, out var range, out string error))
            return $"Its mod.json has a gameVersion that can't be read ({error}), so the game version isn't checked.";
        if (range.Matches(gameVersion))
            return null;
        return $"Made for game version {range.Text}, this is {(string.IsNullOrEmpty(gameVersion) ? "unknown" : gameVersion)}.";
    }
}
