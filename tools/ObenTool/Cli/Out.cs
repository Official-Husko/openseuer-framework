namespace ObenTool.Cli;

/// <summary>Consistent console output for the CLI.</summary>
public static class Out
{
    public static void Info(string message) => Console.WriteLine(message);

    public static void Ok(string message) => Write(ConsoleColor.Green, message);

    public static void Warn(string message) => Write(ConsoleColor.Yellow, message);

    public static void Error(string message)
    {
        var old = Console.ForegroundColor;
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine(message);
        Console.ForegroundColor = old;
    }

    private static void Write(ConsoleColor color, string message)
    {
        var old = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(message);
        Console.ForegroundColor = old;
    }
}
