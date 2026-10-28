// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Globalization;

namespace ObenFramework.Assets;

/// <summary>Skips the <c>-option value...</c> prefix some exporters put in front of a texture file name.</summary>
internal static class MtlMapOptions
{
    private static readonly char[] Blanks = { ' ', '\t' };

    /// <summary>Returns the file name part of the text after <c>map_Kd</c>.</summary>
    public static string StripOptions(string rest)
    {
        rest = rest.Trim();
        while (rest.StartsWith("-", StringComparison.Ordinal))
        {
            string option = FirstWord(rest);
            rest = SkipWord(rest);
            int count = ArgCount(option);
            for (int i = 0; i < count && IsArg(option, FirstWord(rest)); i++)
                rest = SkipWord(rest);
        }
        return rest;
    }

    private static int ArgCount(string option)
    {
        switch (option)
        {
            case "-blendu":
            case "-blendv":
            case "-cc":
            case "-clamp":
            case "-bm":
            case "-boost":
            case "-texres":
            case "-imfchan":
            case "-type":
                return 1;
            case "-mm":
                return 2;
            default:
                return 3;
        }
    }

    private static bool IsArg(string option, string word)
    {
        switch (option)
        {
            case "-blendu":
            case "-blendv":
            case "-cc":
            case "-clamp":
                return word == "on" || word == "off";
            case "-imfchan":
                return word.Length == 1 && "rgbmlz".IndexOf(word[0]) >= 0;
            case "-type":
                return word.Length > 0;
            default:
                return float.TryParse(word, NumberStyles.Float, CultureInfo.InvariantCulture, out _);
        }
    }

    private static string FirstWord(string text)
    {
        int end = text.IndexOfAny(Blanks);
        return end < 0 ? text : text.Substring(0, end);
    }

    private static string SkipWord(string text)
    {
        int end = text.IndexOfAny(Blanks);
        return end < 0 ? "" : text.Substring(end).TrimStart(Blanks);
    }
}
