// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
namespace ObenFramework.Saves;

/// <summary>
/// Cheap text checks on a saved-state blob before it reaches the game's parser. Blobs may come from
/// another player, and the game's reader trusts what it is given, so anything oversized, absurdly
/// nested or hiding structure behind comments is turned away up front. No Unity types, so it is
/// unit tested on its own.
/// </summary>
internal static class SaveBlobLimits
{
    /// <summary>Longest blob accepted, in characters.</summary>
    public const int MaxChars = 4 * 1024 * 1024;

    /// <summary>Deepest object or list nesting accepted.</summary>
    public const int MaxDepth = 256;

    /// <summary>Why the text is refused, or null when it is fine to parse.</summary>
    public static string RefuseReason(string json)
    {
        if (string.IsNullOrEmpty(json))
            return "the blob is empty";
        if (json.Length > MaxChars)
            return $"the blob is {json.Length} characters, over the {MaxChars} character limit";

        int depth = 0;
        bool inString = false;
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            if (inString)
            {
                if (c == '\\') i++;
                else if (c == '"') inString = false;
            }
            else if (c == '"') inString = true;
            else if (c == '/') return "the blob has a slash outside a string, comments are refused";
            else if (c == '[' || c == '{')
            {
                if (++depth > MaxDepth)
                    return $"the blob is nested deeper than {MaxDepth} levels";
            }
            else if (c == ']' || c == '}')
            {
                if (--depth < 0)
                    return "the blob closes more brackets than it opens";
            }
        }
        return null;
    }
}
