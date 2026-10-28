namespace ObenFramework.NewGames;

/// <summary>
/// Ids used by the new game modules (mod ids, option keys, preset ids): 1 to 64 letters, digits,
/// '.', '_' or '-'. They end up in save files and in "modId.key" names. No Unity types.
/// </summary>
internal static class OptionIds
{
    public const int MaxLength = 64;

    public static bool IsValid(string id)
    {
        if (string.IsNullOrEmpty(id) || id.Length > MaxLength)
            return false;
        foreach (char c in id)
        {
            bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-';
            if (!ok)
                return false;
        }
        return true;
    }

    /// <summary>Like <see cref="IsValid"/> but without dots, for the part after "modId.".</summary>
    public static bool IsValidKey(string key) => IsValid(key) && key.IndexOf('.') < 0;
}
