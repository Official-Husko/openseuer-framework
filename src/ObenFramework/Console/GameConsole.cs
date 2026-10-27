using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Console;

/// <summary>
/// The game's own developer console (F1). Mods add commands here; they show up in "help" and are
/// removed when the mod unloads. Tools can run any console line and get back what it printed.
/// </summary>
public static class GameConsole
{
    private static readonly Dictionary<string, ConsoleCommand> Commands = new Dictionary<string, ConsoleCommand>(StringComparer.Ordinal);
    private static readonly Action<DeveloperConsole, string> HandleInput =
        AccessTools.MethodDelegate<Action<DeveloperConsole, string>>(AccessTools.Method(typeof(DeveloperConsole), "HandleInput"));

    // Set while Execute runs, so everything the console prints is collected.
    private static List<string> _capture;

    public static bool IsOpen => DeveloperConsole.Instance != null && DeveloperConsole.Instance.consoleOpened;

    public static IEnumerable<ConsoleCommand> ModCommands => Commands.Values;

    /// <summary>
    /// Adds a command owned by a mod. Names can't clash with each other; a mod command with the same
    /// name as one of the game's replaces it while the mod is loaded.
    /// </summary>
    public static void Register(ObenMod owner, string name, string usage, Func<string[], string> handler)
    {
        if (Commands.TryGetValue(name, out var existing))
            throw new InvalidOperationException($"Console command '{name}' is already registered by {existing.Owner}.");
        Commands[name] = new ConsoleCommand(name, usage, owner.Info.Id, handler);
        owner.Track(Disposable.Create(() => Commands.Remove(name)));
    }

    /// <summary>Runs a console line as if it was typed in, and returns everything it printed.</summary>
    public static List<string> Execute(string line)
    {
        var console = DeveloperConsole.Instance
            ?? throw new InvalidOperationException("The developer console doesn't exist yet.");
        var printed = new List<string>();
        var previous = _capture;
        _capture = printed;
        try
        {
            HandleInput(console, line);
        }
        finally
        {
            _capture = previous;
        }
        return printed;
    }

    /// <summary>Prints to the console (rich text works). Safe to call before the console exists.</summary>
    public static void Print(string text)
    {
        if (DeveloperConsole.Instance != null)
            DeveloperConsole.Instance.Print(text);
        else
            _capture?.Add(text);
    }

    internal static void Captured(string text) => _capture?.Add(text);

    /// <summary>True when the line is a mod command; runs it and prints what it returns.</summary>
    internal static bool TryRun(string line)
    {
        string[] tokens = ConsoleTokens.Split(line);
        if (tokens.Length == 0 || !Commands.TryGetValue(tokens[0], out var command))
            return false;

        var args = new string[tokens.Length - 1];
        Array.Copy(tokens, 1, args, 0, args.Length);
        string output;
        try
        {
            output = command.Handler(args);
        }
        catch (Exception e)
        {
            output = $"<color=red>{command.Name} failed: {e.GetBaseException().Message}</color>";
        }
        if (!string.IsNullOrEmpty(output))
            Print(output);
        return true;
    }
}
