using System;

namespace ObenLoader.Runtime;

/// <summary>
/// Which copy of the game this process is. Normally there's one, but a second can run next to it
/// for testing co-op on one machine (OBEN_INSTANCE=2, started with <c>oben launch --instance 2</c>).
/// Things two processes can't share (log files, the dev bridge's port) are told apart with this.
/// </summary>
public static class Instance
{
    /// <summary>1 for the normal game, 2 and up for extra copies.</summary>
    public static int Number { get; } = Read();

    public static bool IsFirst => Number == 1;

    /// <summary>Added to file names so processes never share one: "" for the first instance, "-2" for the second.</summary>
    public static string Suffix { get; } = Number == 1 ? "" : "-" + Number;

    private static int Read()
    {
        string text = Environment.GetEnvironmentVariable("OBEN_INSTANCE");
        return int.TryParse(text, out int number) && number > 1 ? number : 1;
    }
}
