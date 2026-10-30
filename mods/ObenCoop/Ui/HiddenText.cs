using System.Text.RegularExpressions;

namespace ObenCoop.Ui;

/// <summary>
/// Streamer mode: join codes, IP addresses and Steam ids in a text become stars, so they don't end up on a
/// stream or a screenshot. Ports stay, they say nothing about who is playing.
/// </summary>
internal static class HiddenText
{
    private static readonly Regex Address = new Regex(@"\b\d{1,3}(?:\.\d{1,3}){3}\b", RegexOptions.CultureInvariant);
    private static readonly Regex Code = new Regex(@"\b[0-9A-Za-z]{4}(?:-[0-9A-Za-z]{4}){2,3}\b", RegexOptions.CultureInvariant);
    private static readonly Regex SteamId = new Regex(@"\b\d{15,20}\b", RegexOptions.CultureInvariant);

    public static string Scrub(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;
        text = Code.Replace(text, m => Stars(m.Value));
        text = Address.Replace(text, "***.***.***.***");
        return SteamId.Replace(text, "*****");
    }

    // The code's shape stays (groups and dashes), so it still reads as a code.
    private static string Stars(string code)
    {
        var chars = code.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (chars[i] != '-')
                chars[i] = '*';
        }
        return new string(chars);
    }
}
