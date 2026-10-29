using System.Collections.Generic;
using ObenLoader.Runtime;

namespace ObenLoader.Mods;

/// <summary>What is known about the running game: its version, Steam build (null off Steam) and code hash.</summary>
internal readonly struct GameIdentity
{
    public GameIdentity(string version, string buildId = null, string codeHash = null)
    {
        Version = version;
        BuildId = buildId;
        CodeHash = codeHash;
    }

    public string Version { get; }
    public string BuildId { get; }
    public string CodeHash { get; }
}

/// <summary>
/// Checks on a mod's manifest before it loads: a usable id, readable versions and dependencies, and the game
/// version, Steam build and game code it says it was made for. Plain logic with the game's version passed in, so it is
/// tested on its own.
/// </summary>
internal static class ModChecks
{
    /// <inheritdoc cref="Refusal(ModManifest, string, string, bool, out string, out string)"/>
    public static string Refusal(ModManifest manifest, string gameVersion, out string warning) =>
        Refusal(manifest, gameVersion, null, false, out warning, out _);

    /// <inheritdoc cref="Refusal(ModManifest, string, string, bool, out string, out string)"/>
    public static string Refusal(ModManifest manifest, string gameVersion, string buildId, bool strictAll, out string warning) =>
        Refusal(manifest, gameVersion, buildId, strictAll, out warning, out _);

    /// <summary>
    /// Why the mod must not load, or null. <paramref name="warning"/> is set for problems that don't stop it
    /// (made for another game version or build without <c>strictGameVersion</c>, an unreadable version) and
    /// belongs on the Errors page; <paramref name="note"/> is only for the log (a mod that says it works with
    /// every game version, which can't be checked).
    /// </summary>
    /// <param name="buildId">The installed game's Steam build id, or null when it isn't known (no Steam manifest).</param>
    /// <param name="strictAll">The player's loader setting that refuses every mod made for another game version.</param>
    public static string Refusal(ModManifest manifest, string gameVersion, string buildId, bool strictAll, out string warning, out string note) =>
        Refusal(manifest, new GameIdentity(gameVersion, buildId), strictAll, out warning, out note);

    /// <inheritdoc cref="Refusal(ModManifest, string, string, bool, out string, out string)"/>
    public static string Refusal(ModManifest manifest, GameIdentity game, bool strictAll, out string warning, out string note)
    {
        string gameVersion = game.Version;
        warning = null;
        note = null;
        string idProblem = ModIds.Problem(manifest.Id);
        if (idProblem != null)
            return $"Invalid id '{manifest.Id}': {idProblem}. Set a valid \"id\" in its mod.json.";
        string dependency = DependencyProblem(manifest);
        if (dependency != null)
            return dependency;

        var notes = new List<string>();
        if (!SemVer.IsReadable(manifest.Version))
            notes.Add($"Its version '{manifest.Version}' can't be read (use numbers like 1.2.0), so it counts as 0.0 for mods that need it.");

        if (IsAnyGameVersion(manifest))
        {
            // Taken at its word, so neither strictGameVersion nor the player's setting refuses it, but the log says so.
            note = $"It says it works with every game version, so it isn't checked against {(string.IsNullOrEmpty(gameVersion) ? "this one" : gameVersion)}."
                + (manifest.AnyGameVersion && !string.IsNullOrWhiteSpace(manifest.GameVersion) && manifest.GameVersion.Trim() != "*" ? $" (anyGameVersion overrides its gameVersion \"{manifest.GameVersion}\".)" : "");
            warning = notes.Count > 0 ? string.Join(" ", notes) : null;
            return null;
        }

        string mismatch = Mismatch(manifest, game);
        if (mismatch != null)
        {
            bool checkable = string.IsNullOrWhiteSpace(manifest.GameVersion) || VersionRange.TryParse(manifest.GameVersion, out _, out _);
            if (checkable && manifest.StrictGameVersion)
                return mismatch + " It only loads on those versions (strictGameVersion in its mod.json).";
            if (checkable && strictAll)
                return mismatch + " Mods made for other game versions are switched off (RefuseOtherGameVersions in ObenLoader/settings.json).";
            notes.Add(checkable ? mismatch + " It may not work right." : mismatch);
        }
        warning = notes.Count > 0 ? string.Join(" ", notes) : null;
        return null;
    }

    /// <summary>The mod says it works with every game version: <c>anyGameVersion</c>, or a gameVersion of "*".</summary>
    public static bool IsAnyGameVersion(ModManifest manifest) =>
        manifest.AnyGameVersion || (manifest.GameVersion != null && manifest.GameVersion.Trim() == "*");

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

    /// <summary>
    /// Why the running game isn't one the mod was made for (version, Steam build or code), or null when it is,
    /// the mod says it fits every version, or nothing it names can be checked here.
    /// </summary>
    public static string Mismatch(ModManifest manifest, GameIdentity game) =>
        IsAnyGameVersion(manifest) ? null : GameProblem(manifest, game.Version) ?? BuildProblem(manifest, game.BuildId, game.CodeHash);

    /// <summary>
    /// A sentence saying the game is neither a Steam build in <c>gameBuilds</c> nor has a code hash in
    /// <c>gameHashes</c>, or null when one of them matches, or nothing listed can be checked (no lists, or a
    /// copy that didn't come from Steam with only Steam builds listed).
    /// </summary>
    public static string BuildProblem(ModManifest manifest, string buildId, string codeHash = null)
    {
        bool builds = buildId != null && manifest.GameBuilds != null && manifest.GameBuilds.Count > 0;
        bool hashes = codeHash != null && manifest.GameHashes != null && manifest.GameHashes.Count > 0;
        if (!builds && !hashes)
            return null;
        if (builds && manifest.GameBuilds.Exists(b => string.Equals(b?.Trim(), buildId, System.StringComparison.Ordinal)))
            return null;
        if (hashes && manifest.GameHashes.Exists(h => GameFiles.Matches(h, codeHash)))
            return null;

        var madeFor = new List<string>();
        var running = new List<string>();
        if (builds)
        {
            madeFor.Add("Steam build " + string.Join(", ", manifest.GameBuilds));
            running.Add("build " + buildId);
        }
        if (hashes)
        {
            madeFor.Add("game code " + string.Join(", ", manifest.GameHashes));
            running.Add("code " + GameFiles.Short(codeHash));
        }
        return $"Made for {string.Join(" or ", madeFor)}, this is {string.Join(", ", running)}.";
    }

    private static string DependencyProblem(ModManifest manifest)
    {
        if (manifest.Dependencies == null)
            return null;
        foreach (var dependency in manifest.Dependencies)
        {
            string idProblem = ModIds.Problem(dependency.Key);
            if (idProblem != null)
                return $"Its dependency '{dependency.Key}' isn't a valid mod id: {idProblem}.";
            if (!SemVer.IsReadableMinimum(dependency.Value))
                return $"The version '{dependency.Value}' it needs of '{dependency.Key}' can't be read; use a number like \"1.2\" or \"*\".";
        }
        return null;
    }
}
