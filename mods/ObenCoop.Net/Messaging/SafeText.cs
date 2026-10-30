namespace ObenCoop.Net.Messaging;

/// <summary>
/// Helpers for text that comes from other players. Names are shown as rich text, so bidi controls
/// can flip the text after them and zero-width characters can hide or fake the text they sit in.
/// </summary>
internal static class SafeText
{
    /// <summary>
    /// True for bidi controls and zero-width or invisible characters. Surrogates are checked
    /// with <see cref="IsLoneSurrogate"/> because they only matter per position.
    /// </summary>
    public static bool IsHidden(char c) =>
        c == '\u061C' || c == '\u00AD' || c == '\u2060' || c == '\uFEFF' || (c >= '\u200B' && c <= '\u200F')
        || (c >= '\u202A' && c <= '\u202E') || (c >= '\u2066' && c <= '\u2069');

    /// <summary>
    /// True when the surrogate at <paramref name="index"/> has no partner next to it in the text.
    /// Valid pairs (emoji) return false.
    /// </summary>
    public static bool IsLoneSurrogate(string text, int index)
    {
        char c = text[index];
        if (char.IsHighSurrogate(c))
            return index + 1 >= text.Length || !char.IsLowSurrogate(text[index + 1]);
        if (char.IsLowSurrogate(c))
            return index == 0 || !char.IsHighSurrogate(text[index - 1]);
        return false;
    }

    /// <summary>
    /// Cuts text to at most <paramref name="max"/> characters without leaving a high surrogate at the end.
    /// </summary>
    public static string Cut(string text, int max)
    {
        if (text.Length <= max)
            return text;
        int length = max;
        if (length > 0 && char.IsHighSurrogate(text[length - 1]))
            length--;
        return text.Substring(0, length);
    }
}
