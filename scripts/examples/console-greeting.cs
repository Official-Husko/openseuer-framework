// Patches the F1 console's "help" to print an extra line. Run it with
//   oben run scripts/examples/console-greeting.cs --watch
// then edit the text and save: the old patch goes away and the new one applies.
using HarmonyLib;
using ScriptRunner;

public static class ConsoleGreeting
{
    public static object Run(ScriptScope scope)
    {
        scope.PatchAll(typeof(ConsoleGreeting));
        return "patched DeveloperConsole.PrintValidCommands";
    }

    [HarmonyPatch(typeof(DeveloperConsole), nameof(DeveloperConsole.PrintValidCommands))]
    private static class AfterHelp
    {
        private static void Postfix(DeveloperConsole __instance) => __instance.Print("Hello from a script.");
    }
}
