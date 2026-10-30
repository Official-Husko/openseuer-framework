using UnityEngine;

namespace ObenUIKit.Controls;

/// <summary>
/// Text from outside (log lines, names, file contents, values) in the kit's labels, which read rich text
/// tags: a "&lt;b&gt;" in a log line would turn the rest bold, and the menu font draws angle brackets as
/// stray marks.
/// </summary>
public static class UiText
{
    /// <summary>The text shown exactly as it is: nothing in it is read as a tag, and angle brackets draw properly.</summary>
    public static string Plain(string text) => KitText.Escape(text);

    /// <summary>
    /// The text shown as it is, in <paramref name="color"/>, given the usual way (as an image editor shows it); it
    /// is drawn to match, even though the game renders in linear space.
    /// </summary>
    public static string Paint(Color color, string text) =>
        KitText.Paint("#" + ColorUtility.ToHtmlStringRGBA(color.linear), text);
}
