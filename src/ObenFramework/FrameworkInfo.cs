using System;
using System.Collections.Generic;
using ObenLoader.Mods;

namespace ObenFramework;

/// <summary>
/// Which game the framework was verified on, from its own mod.json (gameVersion, gameBuilds, gameHashes), and
/// whether the running game is that one. On any other game it still loads (with a warning on the Errors page),
/// unless the player set RefuseOtherGameVersions. Mods can use this to warn about their own features.
/// </summary>
public static class FrameworkInfo
{
    public const string Id = "ObenFramework";

    private static ModInfo Self => ModRegistry.Find(Id);

    public static string Version => Self != null ? Self.Manifest.Version : null;

    /// <summary>The game version range the framework was verified on ("0.5.13").</summary>
    public static string VerifiedGameVersion => Self != null ? Self.Manifest.GameVersion : null;

    /// <summary>The Steam builds it was verified on.</summary>
    public static IReadOnlyList<string> VerifiedSteamBuilds => Self != null ? Self.Manifest.GameBuilds : (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>The game code (Assembly-CSharp.dll SHA-256 prefixes) it was verified on, the same for every store.</summary>
    public static IReadOnlyList<string> VerifiedGameHashes => Self != null ? Self.Manifest.GameHashes : (IReadOnlyList<string>)Array.Empty<string>();

    /// <summary>The running game is one the framework was verified on.</summary>
    public static bool IsVerifiedGame => Self != null && Self.GameMismatch == null;

    /// <summary>Why it isn't ("Made for game version 0.5.13, this is 0.5.14."), or null.</summary>
    public static string GameMismatch => Self != null ? Self.GameMismatch : null;
}
