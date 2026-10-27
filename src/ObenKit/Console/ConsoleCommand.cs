using System;

namespace ObenKit.Console;

/// <summary>
/// A command a mod added to the game's F1 console. The handler gets the words after the command
/// name (quotes group words, like the game's own commands) and returns text to print, or null.
/// </summary>
public sealed class ConsoleCommand
{
    public string Name { get; }
    public string Usage { get; }
    public string Owner { get; }
    internal Func<string[], string> Handler { get; }

    internal ConsoleCommand(string name, string usage, string owner, Func<string[], string> handler)
    {
        Name = name;
        Usage = usage;
        Owner = owner;
        Handler = handler;
    }
}
