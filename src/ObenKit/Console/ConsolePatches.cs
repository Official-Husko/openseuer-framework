using System.Text;
using HarmonyLib;

namespace ObenKit.Console;

/// <summary>Hooks the game's console: mod commands run before the game's switch, and are listed in help.</summary>
[HarmonyPatch(typeof(DeveloperConsole))]
internal static class ConsolePatches
{
    [HarmonyPrefix]
    [HarmonyPatch("HandleInput")]
    private static bool BeforeHandleInput(string input) => !GameConsole.TryRun(input);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DeveloperConsole.Print))]
    private static void AfterPrint(string text) => GameConsole.Captured(text);

    [HarmonyPostfix]
    [HarmonyPatch(nameof(DeveloperConsole.PrintValidCommands))]
    private static void AfterHelp(DeveloperConsole __instance)
    {
        var text = new StringBuilder("Mod commands:\n");
        int count = 0;
        foreach (var command in GameConsole.ModCommands)
        {
            text.Append(command.Usage).Append("  <color=#888>(").Append(command.Owner).Append(")</color>\n");
            count++;
        }
        if (count > 0)
            __instance.Print(text.ToString());
    }
}
