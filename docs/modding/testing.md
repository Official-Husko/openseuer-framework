# Testing

Two things keep the project honest without starting the game: unit tests for the logic that doesn't need Unity, and `oben check`, which reads every mod's Harmony patches and reflection lookups and compares them with the installed game. The second one is what you run after a game update.

## Unit tests

```sh
dotnet test tests/ObenCore.Tests
```

The tests use xunit and need no game install. The loader, framework and tool projects can't be referenced from a test project because they reference Unity, so `tests/ObenCore.Tests/ObenCore.Tests.csproj` compiles the files with pure logic straight from `src/` and `tools/` (`<Compile Include="../../src/..." Link="Linked/...">`). The few Unity-bound types those files touch are replaced by stand-ins in `Stubs/Stubs.cs` (`ObenMod`, `ModRegistry.Find`, `ModLogger`, `UnityEngine.Object`).

| File | What it covers |
| --- | --- |
| `SemVerTests` | `SemVer.Satisfies`: minimums, two-part versions, pre-release tags, `>=` prefixes, junk input |
| `DependencySorterTests` | load order, missing / failed / too-old dependencies, failures spreading, cycles, `loadAfter` hints and loops, mods already running outside the batch |
| `ConsoleTokensTests` | splitting console lines with quoted parts |
| `HandlerListTests` | snapshot behaviour, unsubscribing and subscribing during a raise, owner-tracked subscriptions, a throwing handler not stopping the others |
| `LogBufferTests` | sequence numbers, wraparound, `Read` with after / max / level, concurrent writers |
| `JsonFileTests` | write / read round trip, temp-file replace, missing and broken files, `TryRead` |
| `ArgsTests` | the tool's argument parser, `--instance` and port rules |
| `ScriptSourceTests` | `ScriptSource.Prepare`: expression, statements, whole file, `#line` mapping |
| `HookCheckerTests` | `oben check` against fixture classes (see below) |

To test another file, add a `<Compile Include=... Link=...>` line and, if it needs a type the test project doesn't have, a small stand-in in `Stubs/`. If a stand-in would be large, link the dependency instead or pass it in as a parameter.

Anything that needs the running game (events, patches actually applying, the UI) is still tested in game: `oben run` / `oben eval` and the F1 console.

## oben check

```sh
dotnet build ObenseuerExtended.slnx -c Release     # or the mods you changed
dotnet tools/ObenTool/bin/Release/net10.0/oben.dll check
```

It loads the mod DLLs with Mono.Cecil and checks them against `Obenseuer_Data/Managed/Assembly-CSharp.dll` and `Assembly-CSharp-firstpass.dll` (other game assemblies such as UnityEngine are found in the same folder). Nothing is executed and the game does not need to be running or closed.

Options:

| | |
| --- | --- |
| `--game DIR` | The game to check against (the usual lookup applies otherwise, see Finding the game in [dev-tools.md](dev-tools.md)) |
| `--installed` | Check the DLLs in the game's `Mods/*/` folders instead of the repo builds |
| `--verbose` | Also list every hook that passed |
| `file.dll ...` | Check these DLLs only |

By default it takes the Release builds in `src/*/bin/Release` and `mods/*/bin/Release`, found from the repo root above the current folder. Libraries that sit next to mods (Harmony, Newtonsoft, Cecil, System and Unity assemblies) are skipped.

### What is checked

- Every `[HarmonyPatch]` on a class or patch method, category classes (`[HarmonyPatchCategory]`) included: the target type exists, the method is **declared** on that type (Harmony does not look at base types here), the argument types when given, and `MethodType.Getter`, `Setter`, `Constructor` and `StaticConstructor` targets. Attributes on the class and on the method are layered the way Harmony layers them.
- The parameters of prefix, postfix and finalizer methods: a plain parameter name must be a parameter of the patched method (or `__0`-style index in range), and a `___name` parameter needs a field of that name on the type or a base type.
- `AccessTools.Method`, `DeclaredMethod`, `MethodDelegate`, `Field`, `DeclaredField`, `FieldRefAccess`, `StaticFieldRefAccess`, `Property`, `DeclaredProperty`, `PropertyGetter` and `PropertySetter` when the type is a `typeof(X)` or a generic argument and the name is a string constant, including `new[] { typeof(A), typeof(B) }` argument lists and the `"Namespace.Type:Member"` form. The IL is followed back from each call to find those constants.

A target is reported as **MISSING** when it is not there, or **AMBIGUOUS** when several overloads match and the code does not say which (Harmony throws for that). Each line names the mod, the class in it, the hook and what is wrong. The exit code is 1 if anything is missing or ambiguous, 0 otherwise, so it can be used in a script.

### What is not checked

- Hooks worked out at run time: `TargetMethod` / `TargetMethods`, a variable as the member name, `typeof(X).GetMethod(...)` and `Traverse`. The summary counts these ("worked out at run time").
- Whether a transpiler's IL pattern still matches, or whether a prefix still does the right thing. A name that still exists with a changed meaning passes.
- Parameter and field types are not compared, only names (and the argument types a patch spells out).

## After a game update

1. Update the game, then `oben status`. If Unity's startup files were replaced, run `oben install` again.
2. Rebuild the mods against the new game: `dotnet build ObenseuerExtended.slnx -c Release`. Compile errors are the first list of what changed.
3. Run `oben check --verbose`. Fix every MISSING and AMBIGUOUS line: look the new name up in the decompiled game, update the patch or lookup, rebuild, check again until it exits 0.
4. Run `dotnet test tests/ObenCore.Tests`.
5. Start the game, open a save, and look at `oben logs` and the Player log for Harmony errors. `oben check` finds renamed members; it cannot tell that a patched method now runs at a different time or with different values, so play through the areas the mods touch (the F1 console, the ObenPatches fixes, saving and loading).
6. If the game version changed, update the version noted in the docs under `docs/game/`.

## Reading a result

```
ObenPatches: 5 hooks, 1 broken
  MISSING   ObenPatches.Fixes.RobbersSeePolicePatch [category RobbersSeePolice]
            [HarmonyPatch] NPC_FOV.CheckIfNPCsAreDetected
            Prefix: parameter 'npcs' is not a parameter of NPC_FOV.CheckIfNPCsAreDetected (it has npcList, ignoreCooldown)
```

The class is the mod's class that holds the hook, the second line is the hook as written in the mod, the third says what the game now has. Warnings (yellow) do not change the exit code; they mean something could not be read, for example an assembly that is not in the Managed folder.
