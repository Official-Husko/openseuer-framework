using System.Text;

namespace ObenUIKit.Controls;

/// <summary>
/// Putting text that isn't markup into the kit's rich-text labels. Pure text work, unit tested.
/// </summary>
internal static class KitText
{
    /// <summary>
    /// The font angle brackets are drawn in. The menu font has "&lt;" and "&gt;", but they come out as stray
    /// marks; the kit sets this to TextMeshPro's default font, which the game ships. Null keeps the label's font.
    /// </summary>
    internal static string BracketFont;

    /// <summary>
    /// Text that shows as it is: nothing in it is read as a tag, and its angle brackets come from
    /// <see cref="BracketFont"/>.
    /// </summary>
    public static string Escape(string text)
    {
        if (string.IsNullOrEmpty(text) || (text.IndexOf('<') < 0 && text.IndexOf('>') < 0))
            return text ?? "";

        var escaped = new StringBuilder(text.Length + 48);
        int start = 0;
        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c != '<' && c != '>')
                continue;
            escaped.Append(text, start, i - start);
            // Every "<" sits alone in a noparse, so no tag can start anywhere in the text; ">" alone is no tag.
            string glyph = c == '<' ? "<noparse><</noparse>" : ">";
            escaped.Append(BracketFont == null ? glyph : $"<font=\"{BracketFont}\">{glyph}</font>");
            start = i + 1;
        }
        escaped.Append(text, start, text.Length - start);
        return escaped.ToString();
    }

    /// <summary>
    /// A number the way the kit shows it (a slider's readout): the same in every language (a point for
    /// decimals), "-" for a value that isn't a number, and no "-0".
    /// </summary>
    public static string Number(float value, string format)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return "-";
        string text = value.ToString(format, System.Globalization.CultureInfo.InvariantCulture);
        return text.StartsWith("-", System.StringComparison.Ordinal) && text.Trim('-', '0', '.').Length == 0 ? text.Substring(1) : text;
    }

    /// <summary>Text in a color ("#RRGGBB"), escaped.</summary>
    public static string Paint(string color, string text) =>
        string.IsNullOrEmpty(text) ? "" : $"<color={color}>{Escape(text)}</color>";
}
