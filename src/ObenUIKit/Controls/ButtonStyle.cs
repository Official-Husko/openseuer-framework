namespace ObenUIKit.Controls;

/// <summary>How a button built with <see cref="UiBuilder.Button(string, ButtonStyle, System.Action)"/> looks.</summary>
public enum ButtonStyle
{
    /// <summary>Underlined text, like Apply and Back in the game's options. The default.</summary>
    Plain,

    /// <summary>Smaller underlined text in the muted color, for actions that matter less.</summary>
    Quiet,

    /// <summary>
    /// The text on a bar in the game's option-row look that lights up red under the mouse or the highlight, for
    /// the main action of a window. Full width in a column; sized to its text in a <see cref="UiBuilder.Row"/>.
    /// </summary>
    Box,
}
