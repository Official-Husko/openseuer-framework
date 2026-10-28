// Shows a patch area switching off. Run it with
//   oben run scripts/examples/patch-areas.cs
// It patches the F1 console's "help" in the name of the Script Runner mod, in an area called "demo", then
// tries a method that doesn't exist in the same area. That failure switches "demo" off and takes the
// working patch off again: "help" (oben call console.exec command=help) doesn't print the extra line, the
// log and the Errors page say why, and oben call patches.areas mod=ScriptRunner lists "demo" as off.
// Reloading Script Runner (oben call mods.reload id=ScriptRunner) forgets the area.
using ObenFramework.Patching;
using ObenLoader;
using ObenLoader.Mods;
using ScriptRunner;

public static class PatchAreasDemo
{
    public static object Run(ScriptScope scope)
    {
        ObenMod owner = ModRegistry.Find("ScriptRunner").Instance;

        bool first = ModPatches.Postfix(owner, typeof(DeveloperConsole), nameof(DeveloperConsole.PrintValidCommands), null,
            typeof(PatchAreasDemo), nameof(AfterHelp), area: "demo");
        bool afterFirst = PatchAreas.IsAvailable(owner, "demo");

        bool second = ModPatches.Postfix(owner, typeof(DeveloperConsole), "NoSuchMethod", null,
            typeof(PatchAreasDemo), nameof(AfterHelp), area: "demo");
        bool afterSecond = PatchAreas.IsAvailable(owner, "demo");

        return $"first patch {first}, area on {afterFirst}; missing method patch {second}, area on {afterSecond}";
    }

    private static void AfterHelp(DeveloperConsole __instance) => __instance.Print("Patched through a patch area.");
}
