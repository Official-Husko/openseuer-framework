using System;
using System.Collections.Generic;

namespace ObenFramework.Console;

/// <summary>Splits a console line the way the game does: on spaces, with "quoted parts" kept whole.</summary>
internal static class ConsoleTokens
{
    private static readonly char[] NotInNames = { ' ', '\t', '"' };

    /// <summary>A command name has to be one word without quotes, or no typed line could reach it.</summary>
    public static bool IsName(string name) => !string.IsNullOrEmpty(name) && name.IndexOfAny(NotInNames) < 0;

    public static string[] Split(string line)
    {
        var tokens = new List<string>();
        if (string.IsNullOrEmpty(line))
            return tokens.ToArray();

        string[] parts = line.Split('"');
        for (int i = 0; i < parts.Length; i++)
        {
            if (i % 2 == 1)
            {
                tokens.Add(parts[i]);
                continue;
            }
            tokens.AddRange(parts[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries));
        }
        return tokens.ToArray();
    }
}
