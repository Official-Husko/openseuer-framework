using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ObenLoader.Runtime;

/// <summary>
/// Identifies the installed game by its files, whichever store it came from: SHA-256 of the game's code
/// (<c>Assembly-CSharp.dll</c>, what mods depend on) and of its exe. Read once at start.
/// </summary>
internal static class GameFiles
{
    /// <summary>Hex digits shown in logs; mods may list longer prefixes or whole hashes.</summary>
    public const int ShortLength = 12;

    /// <summary>The lowercase hex SHA-256 of a file, or null when it can't be read.</summary>
    public static string Hash(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            using var sha = SHA256.Create();
            return Hex(sha.ComputeHash(stream));
        }
        catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
        {
            return null;
        }
    }

    public static string Short(string hash) => hash == null ? null : hash.Substring(0, Math.Min(ShortLength, hash.Length));

    /// <summary>
    /// <paramref name="listed"/> names this hash: a prefix of at least 8 hex digits (any case), or the whole of it.
    /// </summary>
    public static bool Matches(string listed, string hash)
    {
        if (string.IsNullOrEmpty(listed) || string.IsNullOrEmpty(hash))
            return false;
        listed = listed.Trim();
        return listed.Length >= 8 && listed.Length <= hash.Length && hash.StartsWith(listed, StringComparison.OrdinalIgnoreCase);
    }

    private static string Hex(byte[] bytes)
    {
        var text = new StringBuilder(bytes.Length * 2);
        foreach (byte b in bytes)
            text.Append(b.ToString("x2"));
        return text.ToString();
    }
}
