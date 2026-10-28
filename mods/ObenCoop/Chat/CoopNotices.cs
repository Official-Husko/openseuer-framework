using ObenFramework.Console;
using ObenFramework.Game;
using ObenLoader.Logging;

namespace ObenCoop.Chat;

/// <summary>Tells the player something: a game notification in a level, the console and log otherwise.</summary>
internal static class CoopNotices
{
    private const int MaxLength = 200;

    public static void Show(ModLogger log, string title, string text)
    {
        text = Clean(text);
        log.Info($"{title}: {text}");

        // Notifications are rich text; names and chat come from other players, so no tags get through.
        var notifications = WorldState.InWorld ? Notifications.instance : null;
        if (notifications != null)
        {
            notifications.CreateNotification(Clean(title), text, silent: true);
            return;
        }
        GameConsole.Print($"[Co-op] {Clean(title)}: {text}");
    }

    /// <summary>Strips markup and control characters and cuts the text to a length that fits a notification.</summary>
    public static string Clean(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";
        var chars = new System.Text.StringBuilder(text.Length);
        foreach (char c in text)
        {
            if (c == '<' || c == '>')
                chars.Append(c == '<' ? '(' : ')');
            else if (!char.IsControl(c))
                chars.Append(c);
        }
        string clean = chars.ToString().Trim();
        return clean.Length <= MaxLength ? clean : clean.Substring(0, MaxLength);
    }
}
