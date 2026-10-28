// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;

namespace ObenLoader.Mods;

/// <summary>
/// Rules for mod ids and for file names made from them or from other keys a mod passes in. A mod id
/// ends up in file names (configs, save data, logs) and Harmony ids, so it is kept to characters that are
/// safe everywhere. The checks follow Windows' rules, since that is where the game runs.
/// </summary>
public static class ModIds
{
    public const int MaxLength = 64;

    // Windows refuses these in file names, on top of the control characters.
    private const string InvalidFileChars = "<>:\"/\\|?*";

    private static readonly string[] DeviceNames = { "CON", "PRN", "AUX", "NUL", "CONIN$", "CONOUT$" };

    /// <summary>True for 1 to 64 letters, digits, '.', '_' or '-', not starting or ending with '.' and not a reserved device name.</summary>
    public static bool IsValid(string id) => Problem(id) == null;

    /// <summary>Why <paramref name="id"/> isn't a valid mod id, or null when it is.</summary>
    public static string Problem(string id)
    {
        if (string.IsNullOrEmpty(id))
            return "the id is empty";
        if (id.Length > MaxLength)
            return $"the id is longer than {MaxLength} characters";
        foreach (char c in id)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
            if (!ok)
                return $"'{c}' isn't allowed; use letters, digits, '.', '_' or '-'";
        }
        if (id[0] == '.' || id[id.Length - 1] == '.')
            return "the id can't start or end with '.'";
        if (id.Contains(".."))
            return "the id can't contain '..'";
        if (IsReservedName(id))
            return $"'{id}' is a name Windows reserves for a device";
        return null;
    }

    /// <summary>Throws an <see cref="ArgumentException"/> naming <paramref name="param"/> when the id isn't valid.</summary>
    public static void Require(string id, string param)
    {
        string problem = Problem(id);
        if (problem != null)
            throw new ArgumentException($"Invalid mod id '{id}': {problem}.", param);
    }

    /// <summary>
    /// True when <paramref name="name"/> can be used as a single file name on any system: no path
    /// separators or characters Windows refuses, no "..", no trailing dot or space, not a device name, at
    /// most <paramref name="maxLength"/> characters.
    /// </summary>
    public static bool IsSafeFileName(string name, int maxLength = 128)
    {
        if (string.IsNullOrEmpty(name) || name.Length > maxLength)
            return false;
        foreach (char c in name)
        {
            if (c < 32 || InvalidFileChars.IndexOf(c) >= 0)
                return false;
        }
        if (name.Contains("..") || name[name.Length - 1] == '.' || name[name.Length - 1] == ' ' || name[0] == ' ')
            return false;
        return !IsReservedName(name);
    }

    /// <summary>
    /// True for the device names Windows reserves (CON, NUL, COM1...), also with an extension ("nul.json"),
    /// which can't be used as file names there.
    /// </summary>
    public static bool IsReservedName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return false;
        int dot = name.IndexOf('.');
        string stem = (dot < 0 ? name : name.Substring(0, dot)).TrimEnd(' ');
        foreach (string device in DeviceNames)
        {
            if (string.Equals(stem, device, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        if (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)))
        {
            char n = stem[3];
            return (n >= '0' && n <= '9') || n == '¹' || n == '²' || n == '³';
        }
        return false;
    }
}
