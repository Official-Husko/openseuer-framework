# Scene looks: character looks from the game's scene files

`ObenFramework.SceneLook.SceneLooks` takes the look of any character the game has placed in a level (every NPC, posed body and shadow figure) straight from the game's scene files and puts it on another body: a mod NPC, a live NPC, a prop with an animator. A look is the character's skinned meshes with their skeleton, bind poses, materials, textures and blend shapes, as the level stores them. Characters made from the game's clothing kit carry every variant (Topias Iisalmi in the market has 29 meshes and wears 6); a look holds the ones the scene shows, the rest sitting on switched-off objects or renderers below the body. No scene has to be loaded to find or load one.

This is separate from the player's own appearance (`ObenFramework.Player.Look`, body sliders and worn items), which describes a look by the character creator's settings rather than by meshes.

## Finding looks

```csharp
using ObenFramework.SceneLook;

SceneLookInfo[] looks = SceneLooks.List("Open Sewer Bazaar");              // characters only
SceneLookInfo[] all = SceneLooks.List("Open Sewer Bazaar", charactersOnly: false);
SceneLookInfo jens = SceneLooks.FindByCharacter(170);                       // any scene, by the game's character id
SceneLookInfo rita = SceneLooks.FindByName("Open Sewer Bazaar", "Rita Korjus"); // body or character name
```

A `SceneLookInfo` tells the body's name and hierarchy path, the file it is in (`levelN`), how many skinned renderers and bones it has, the rig (`Bip001` for the game's characters), the avatar (`humanoid`, `generic`, `none`), whether it is an NPC and which of the game's characters it is, and whether it is `Clean` (every mesh, material and texture can be rebuilt from the files; a look that isn't clean won't load, and `Problems` says why).

The first call for a scene reads its file once, which takes a fraction of a second (a second and a half for the biggest levels), and the result is kept for the session. `FindByCharacter(id)` without a scene may scan many scenes. Do these at a loading moment, not every frame. The F1 console has a read-only `scenelooks` command with the same lookups (`list`, `listall`, `find`, `findchar`, `here`, `status`), and `checks`: the looks of the scene you're in, characters and then every body, with the NPC and character names among them. `SceneLooks.Checks()` writes the same to the framework log and returns a one-line summary; the MODS menu's Settings page has it as "Run scene looks checks" under ObenFramework.

## Loading and applying

```csharp
LoadedSceneLook look = SceneLooks.Load(this, info);                 // or Load(this, "Open Sewer Bazaar", "Jens Brom")
if (!look.Ready)
    Log.Warn("look not loaded: " + look.Reason);
else if (!SceneLooks.Apply(this, look, body, out string reason))
    Log.Warn("look not applied: " + reason);
```

How a look goes on depends on the body:

| Path | When | What happens |
| --- | --- | --- |
| name | every bone of the look has a bone with the same name on the body (the head bone `mr_duck_head` maps to `Bip001 Head`, `Bip001_root` to the body itself) | the meshes are skinned onto the body's own bones, with bind poses rebuilt from the body's rest skeleton so they line up; each mesh is first brought into the body's space through a bone both know, as the kit's clothes are authored standing along Z and its heads along Y |
| retarget | the names differ but the body is humanoid and the look covers the 15 humanoid roles | the look's own skeleton is built under the body, stood in a T-pose for its humanoid avatar (the Blender rig is bound with its arms down), and a driver copies the body's pose onto it every frame, keeping its hips where the body's are (scaled by the two hip heights, so the feet stay on the floor) |
| skeleton | `ApplyToSkeleton` | the meshes are skinned onto the body's humanoid skeleton, matching bones by name or by humanoid role |

The game's characters use two rigs: the 3ds Max biped (`Bip001 ...`, the slider bodies and many NPCs) and a Blender one with dotted names (`Spine`, `Thigh.L`, `Arm.R`, ...; Jens Brom, the market traders). A look goes onto a body of its own rig by name and onto the other one by retargeting; the role table knows both. On the player, apply a look to the model that holds the meshes (the `CharacterModels` object under `CharacterModels`), not to the scripts object above it, whose animator doesn't move those bones. The body's own skinned renderers are switched off (not destroyed) while the look is on. Meshes are rebuilt from the file's vertex and index buffers as stored, with their blend shapes and the blend shape weights the scene gave them, so faces keep their expression. Materials use the body's own shader when it has every property the look's material sets (so the look is lit like the body), otherwise Standard; the main texture, normal map, colours and floats are copied.

- `Reskin(owner, look, npc, out reason, skeleton: false)` is for a live NPC: the game switches an NPC's own renderers back on now and then, so they are checked twice a second and hidden again.
- `Remove(owner, body, out reason)` takes the look off and shows the body's own renderers again.
- `RigOf(owner, body, out reason)` gives the holder object, the avatar, the path taken and, for retargeted looks, `AutoPose` and `SyncPose()` for the pose driver. `SyncPose(owner, body, out reason)` and `SetAutoPose(owner, body, on, out reason)` do the same directly; each of the three also comes without the `out reason`.
- `Describe(look, body?)` is a text report (node tree with file poses, renderers with bones, blend shapes and bind pose scales, and the rig path for a body) for working out why a look comes out wrong.
- `Applied()` lists every body wearing a look; `Status()` counts looks, scans, bodies and open files.

One look per body: a body inside or around another body that wears a look is refused. Only the mod that applied a look can remove it or drive its pose.

## Lifetime

- Loaded looks are shared: loading the same look again, from any mod, gives the same object. `Release(owner, look)` stops holding it; it is freed once no mod holds it and no body wears it.
- Every look a mod applied comes off when that mod unloads (or hot reloads), and its holds are released.
- A body destroyed with its scene takes its look with it; textures and materials are freed when the last body wearing them goes.
- When the framework unloads, every look comes off, everything built (meshes, textures, materials, avatars) is destroyed and the files are closed.

## Limits

The reader understands the build's own serialized format: version 22, little-endian, without type trees, written by Unity 6000.0.77f1 (the game's Unity). Any other file is refused when opened, and on any other Unity version scene looks switch themselves off (`SceneLooks.Unavailable` says so) rather than misread a layout. Textures must be RGB24, RGBA32, DXT1 or DXT5 (all the character textures in 0.5.13 are); meshes must have their buffers inline. References into Unity's built-in resources aren't followed. The reader opens the game's files read-only and never writes anything.

The reading and planning code (file reader, index, mesh layout, blend shapes, rig decision, bind pose maths) has no Unity types and is covered by unit tests, including tests that open every level of an installed game and load real looks.

## Dev bridge

| Command | Parameters | |
| --- | --- | --- |
| `scenelook.list` | `scene?`, `all?`, `match?` | the looks of a scene (default the active one) |
| `scenelook.find` | `character` (and `scene?`), or `scene` and `name` | one look |
| `scenelook.apply` | `scene`, `root`, `target` (`#id`, address, GUID or path), `mode?` (apply, skeleton, reskin), `describe?` | load a look and put it on a body (owned by DevBridge) |
| `scenelook.remove` | `target` | take it off |
| `scenelook.applied` | | every body wearing a look |

```sh
oben call scenelook.list scene="Open Sewer Bazaar" match=Jens
oben call npcs.nearest
oben call scenelook.apply scene="Open Sewer Bazaar" root="Jens Brom" target=#12345 mode=reskin
oben call scenelook.remove target=#12345
```
