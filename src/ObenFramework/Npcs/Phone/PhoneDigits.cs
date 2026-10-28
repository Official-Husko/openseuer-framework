// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Text;

namespace ObenFramework.Npcs;

/// <summary>
/// Phone numbers as the game stores them: an array of key codes, 0 to 9 for the digits, 10 for star and 11 for
/// hash. The dialing screen accepts at most ten keys.
/// </summary>
internal static class PhoneDigits
{
    public const int MaxKeys = 10;

    public static bool TryParse(string text, out int[] keys)
    {
        keys = null;
        if (string.IsNullOrEmpty(text) || text.Length > MaxKeys)
            return false;

        var parsed = new int[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '*')
                parsed[i] = 10;
            else if (c == '#')
                parsed[i] = 11;
            else if (c >= '0' && c <= '9')
                parsed[i] = c - '0';
            else
                return false;
        }
        keys = parsed;
        return true;
    }

    public static string Format(int[] keys)
    {
        if (keys == null)
            return string.Empty;

        var text = new StringBuilder(keys.Length);
        foreach (int key in keys)
            text.Append(key == 10 ? '*' : key == 11 ? '#' : (char)('0' + key));
        return text.ToString();
    }

    public static bool Same(int[] a, int[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }
}
