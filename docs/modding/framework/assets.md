# Assets: images, sounds and models

`ObenFramework.Assets` loads art and audio from your mod's folder. Everything you load is cached per mod and destroyed when the mod unloads, so a hot reload never leaks textures or clips, and you don't write any cleanup.

All calls are for the main thread. Paths are relative to the mod folder (`<game>/Mods/<ModId>/`) and may not climb out of it; an absolute path is used as given. Put your files in an `assets/` folder next to `mod.json` and they are copied along with the build.

## Textures and sprites

```csharp
using ObenFramework.Assets;

Texture2D photo = ModAssets.Texture(this, "assets/icons/photo.png");
Sprite icon = ModAssets.Sprite(this, "assets/icons/thing.png");

// Nine-slice: 12 pixels of border on every side, for an Image set to Sliced.
Sprite panel = ModAssets.Sprite(this, "assets/ui/panel.png", 12f);

Sprite pixelArt = ModAssets.Sprite(this, "assets/ui/cursor.png", new SpriteOptions
{
    Border = new Vector4(2, 2, 2, 2),   // left, bottom, right, top in pixels
    PixelsPerUnit = 16f,
    Pixelated = true,
});
```

- PNG and JPG only. A missing file, a file that isn't an image, or one over 4096 pixels on a side throws with a message naming the file. The size is read from the header before decoding, so an oversized image costs almost nothing.
- Asking for the same file with the same options again returns the cached object.
- Sprites use no mip maps and clamp at the edges, so UI art stays sharp. Textures default to mip maps, bilinear filtering and clamp; `TextureOptions` changes that (`Linear` for normal maps and masks, `Readable` if you call `GetPixels`).
- Textures are not readable by default, which halves their memory.

Item icons: `ItemRegistry` wants a `Sprite`; `ModAssets.Sprite` is how you make one. See [items.md](items.md).

## Sounds

```csharp
// Short WAV, right now.
AudioClip click = ModAssets.Sound(this, "assets/sfx/click.wav");

// Anything, without stalling the frame. OGG and MP3 work here too.
ModAssets.SoundAsync(this, "assets/music/theme.ogg",
    clip => ModSounds.Loop(this, clip, SoundGroup.Music, 0.6f),
    reason => Log.Warn("theme not loaded: " + reason));

// A generated tone.
AudioClip beep = ModAssets.Clip(this, "beep", samples, channels: 1, sampleRate: 44100);
```

- **WAV** is read by the framework's own decoder: PCM 8, 16, 24 and 32 bit, IEEE float 32 and 64 bit, up to 8 channels, extensible headers, and files with extra chunks or a cut-off data chunk. `SoundAsync` decodes on a worker thread and only the clip creation happens on the main thread.
- **OGG and MP3** go through Unity's own loader (`UnityWebRequestMultimedia` on a `file://` address) as a coroutine owned by your mod. Files over 1 MB stay compressed in memory and decode while playing, so a long music track doesn't cost hundreds of megabytes.
- WAV files over 32 MB are handed to Unity's loader as well. Prefer OGG for anything long.
- `Sound` is for WAV only and throws for other formats. `SoundAsync` takes everything and calls `done` on the main thread, or `failed` with the reason. Two requests for the same file share one load. If the mod unloads first, neither callback runs.

### Playing through the game's mixer

```csharp
ModSounds.Play(this, click);                                         // like a sound effect
ModSounds.Play(this, click, SoundGroup.Menu, volume: 0.8f, pitch: 1.1f);
ModSounds.PlayAt(this, thud, crate.position, maxDistance: 20f);      // from a point, fades with distance

IDisposable theme = ModSounds.Loop(this, themeClip);                 // music group, until disposed or unload
theme.Dispose();
```

The game routes everything through one mixer (`Sounds/MainMixer`). `SoundGroup` names the groups mods usually want:

| Group | Use | Player slider |
|---|---|---|
| `Effects` | sound effects, the game's default | Sound |
| `Music` | music | Music |
| `Menu` | interface sounds | Sound |
| `Ambience` | ambient loops | Sound |
| `Player`, `Footsteps` | the player's own sounds | Sound |

The slider column was measured in 0.5.13 with a tone on each group. Everything but `Music` (Soundscape, Wasteland and the menus too) follows Sound; the levels' own background music plays through Soundscape, so it follows Sound, not Music.

`ModSounds.Group("Name")` gives any group by name and `ModSounds.GroupNames()` lists all of them (Soundscape, Muffled, Underwater and so on), if you want a sound to be muffled with the rest. Playing goes through the game's own `AudioSourceMaker`, so the output group is resolved the way the game does it, and sources pause with the game. `Play` and `PlayAt` return the `AudioSource` (null if the audio system isn't up yet) and the object is destroyed when the clip ends.

`ModAssets.Clip(owner, name, samples, channels, sampleRate)` makes a clip from samples you generate, as floats from -1 to 1 or as 16-bit `short`s. Samples out of range are clipped and NaN or infinite ones become silence (your array is left as it is), and a clip holds at most `ModAssets.MaxClipSamples` (16,777,216, as many as the largest WAV file).

### Turning the game's volume down

```csharp
VolumeLowering quiet = SoundVolumes.Lower(this, VolumeChannel.Music, 0.3f);   // 30% of the player's volume
quiet.Share = 0.1f;                                                         // change it, e.g. to fade
quiet.Dispose();                                                            // the player's level again
float? db = SoundVolumes.Level(VolumeChannel.Sound);                        // what the mixer plays at now
```

The channels are the game's four sliders: `Master`, `Sound` (which also covers the game's other sounds), `Music` and `Nasty`. A lowering lasts until it is disposed or its mod unloads; with several on one channel the lowest share wins. The player's settings are never touched: the game's volume file keeps the slider values, a slider the player moves still works (the lowering applies on top of the new level), and the options menu, which reads the mixer back into its sliders when it sets itself up, sees the player's own levels. `SoundVolumes.IsAvailable` is false when those hooks failed after a game update; lowerings then have no effect, so the options menu can't save a lowered level as the player's.

### The game's own sounds

```csharp
GameSounds.PlayPickup("Bottle");                     // the sound an item makes when picked up (GameSounds.PickupNames())
GameSounds.PlayLibrary("harvest");                   // a random clip of a library set, heard everywhere
GameSounds.PlayLibraryAt("scarySounds", door.position);
var ids = GameSounds.SoundscapeIds();                // the level's soundscapes
GameSounds.SetSoundscape(ids[0]);                    // ResetSoundscape() goes back to the level's general one
```

The library sets are the fields of the game's `RandomAudioLibrary` (`GameSounds.LibraryNames()`: foley, harvest, plant, arrest, steal, prank, scary and crazy neighbour sounds). A soundscape set from code lasts until the player walks into one of the level's soundscape zones, which switch it as usual. `GameSounds.Soundscape` is the id playing now.

## Models

```csharp
ModelRegistry.Load(this, "crate", "assets/models/crate.obj",
    new ModelOptions { Scale = 0.5f, Pivot = ModelPivot.BottomCenter, Collider = ModelCollider.Box },
    done: model => Log.Info($"crate has {model.TriangleCount} triangles"),
    failed: reason => Log.Warn(reason));
```

Later, anywhere (this is how items, furniture or props get at the model):

```csharp
ModelTemplate crate = ModelRegistry.Find("MyMod/crate");      // modId/key, or null while it isn't loaded
GameObject one = crate.Instantiate(position, rotation);       // active copy, shares mesh and materials
GameObject also = ModelRegistry.Spawn("MyMod/crate", position, Quaternion.identity);
```

`ModelRegistry.LoadNow` does the same synchronously and throws on failure, which is fine for a small model in `OnLoad`.

### What is loaded

- **OBJ:** positions, UVs, normals, faces of any size (triangulated as fans, so concave polygons need "Triangulate Faces" in the exporter), negative indices, `usemtl` groups as submeshes (at most 8; extra materials share the last), and a hard limit of 64 MB, 2 million vertices and 6 million indices. Without normals in the file, smooth ones are calculated.
- **MTL:** `Kd` (colour), `Ke` (emission), `map_Kd` (diffuse texture, PNG or JPG, which must be inside the model's folder), and `d`/`Tr`. A material that is partly see-through gets the Standard shader's Fade mode (glass, ghosts); other shaders only get the colour's alpha.
- Colours are treated as the linear values Blender writes and converted for the game's linear colour space.
- The materials use the game's **Standard** shader (the game's build has no scriptable render pipeline and 5,000 of its materials use Standard). A shader can be named in `ModelOptions.Shader`; if Standard were ever missing, Diffuse, Unlit and Sprites/Default are tried in that order.

### Options

| Option | Default | |
|---|---|---|
| `Scale` | 1 | uniform, applied to the file's units |
| `Rotation`, `Offset` | none | Euler degrees and a shift, applied around the pivot |
| `Pivot` | `Keep` | `BottomCenter` for things that stand on the floor, `Center` for the middle |
| `FlipX` | true | OBJ is right-handed and Unity left-handed; leave on for Blender exports |
| `Collider` | `None` | `Box`, `Convex` (Unity reduces the hull of detailed models) or `Mesh` (exact, for fixed things only) |
| `Layer` | 0 | layer of the instances |
| `Texture` | from the MTL | one texture for every material |
| `Smoothness` | 0.2 | the MTL has nothing Standard can use for this |
| `MaxTextureSide` | 2048 | |

Warnings (a texture outside the folder, a material the MTL doesn't define, a partial set of UVs) are logged once with the model's key, and the model still loads.

### How it avoids stalling the game

Reading the file, parsing, laying out vertices and calculating normals run on a worker thread. The main thread then builds the mesh in one frame, and each material (with its texture decode) in a frame of its own, then the template. A 2-million-vertex model costs a few milliseconds per frame instead of one long hitch. `Convex` and `Mesh` colliders are cooked by Unity when assigned, which for a very detailed mesh takes a moment on that frame; use `Box` for big models.

### Templates

`ModelTemplate` has `Mesh`, `Materials`, `Bounds`, `VertexCount`, `TriangleCount` and `Prefab`, an inactive object the copies come from. Add components to the prefab before spawning if every copy should have them. It and its copies' mesh and materials are destroyed when the owning mod unloads: copies placed in the world should be destroyed or replaced by then (the framework doesn't track them).

## Item icons and models

Any item, the game's or a mod's, can get a mod's icon or model for as long as the mod is loaded:

```csharp
ItemIcons.SetFromFile(this, 35180, "icons/scrap.png");                // or ItemIcons.Set(this, id, sprite)
ModelRegistry.Load(this, "mug", "models/mug.obj", done: model =>
{
    ItemModels.SetWorldModel(this, 35180, model);                      // lying in the world
    ItemModels.SetHeldModel(this, someEquipableId, model);             // in the player's hands
});
```

- **Icons** replace the item's inventory sprite (and its skins' sprites). They are put back whenever the game reloads the item's sprites, and the original returns when the mod unloads. The newest replacement of an item wins.
- **World models** swap the look of every pickup of the item when it appears, including pickups loaded from a save: the original renderers are hidden and the model shown in their place, centred on them and scaled so its longest side matches (pass `fit: false` for the model's own size). Colliders and behaviour stay the item's own. When the mod unloads, pickups in the world get their own look back.
- **Held models** work for items that can be held (with an `ItemEquipable` action): the framework copies the item's held object and puts the model on its first and third person meshes. It applies the next time the item is taken in hand.
- Placed furniture with a model is in [furniture.md](furniture.md).

`ItemIcons.IsAvailable` and `ItemModels.IsAvailable` are false when their hooks failed to apply after a game update.

### Icons drawn from a model

A mod with a model but no icon art can have the icon drawn from the model:

```csharp
ItemIcons.SetFromModel(this, 35180, model);                            // a ModelTemplate, or any GameObject
Sprite icon = ModelIcons.Render(model, new IconOptions { Size = 128, Yaw = 90f });
ModelIcons.Release(icon);                                              // when it's no longer shown
```

- `ModelIcons.Render` takes a `ModelTemplate`, a game prefab or an object in the world and returns a square sprite with a clear background. Only the visible meshes are copied (the most detailed level of a LOD group), so none of the model's scripts run. The picture is taken far above the level with its own two lights, and with the level's fog, ambient light and lights kept off it, so it looks the same in every level and at any time of day, and the level looks the same afterwards.
- `IconOptions`: `Size` (16 to 1024, default 256, as the game's own icons), `Yaw` (default 35: degrees round the model, 0 faces the model's +Z side), `Pitch` (default 25: degrees looking down) and `Margin` (default 0.06 of the icon on each side).
- A sprite from `Render` belongs to the caller. `SetFromModel` releases its own when the mod unloads.
- Whatever shader the model uses, the edges come out right: the model is drawn over black and over white, and the difference gives each pixel's coverage. A 256 pixel icon takes about 15 to 25 ms, so draw icons once (in `OnLoad`, say), not every frame.
- Two places do it on their own: an item a mod registers without a sprite gets a picture of its `Appearance.Prefab` ([items.md](items.md)), and custom furniture with its own model and no `Icon` gets a picture of the model ([furniture.md](furniture.md)).

## Texts in the player's language

`GameText` reads the game's own translations (its Unity Localization string tables, `Menu` by default) and lets mods register their own:

```csharp
GameText.Register(this, "English", new Dictionary<string, string> { ["mymod.greeting"] = "Hello" });
GameText.Register(this, "fi", new Dictionary<string, string> { ["mymod.greeting"] = "Moi" });
string text = GameText.Get("mymod.greeting");             // mod text, else the game's table, else English, else the key
string language = GameText.Language;                      // "English (en)" as the game names it
GameText.OnLanguageChanged(this, language => Rebuild());
```

A language is matched by the game's full name ("English (en)"), the name alone or the code. 18 languages ship with 0.5.13.

Bridge: `sounds.volume` (`channel`, `share`, `clear`) lists the channels with their level and share, or lowers one (owned by the bridge) or gives it back; `items.model` (`item`, `model`, `held`, `clear`) shows a loaded model on an item, owned by the bridge; `items.drawicon` (`item` or `model`, `size`, `yaw`, `pitch`, `margin`, `file`, `apply`, `clear`) draws an icon of an item's pickup model or a loaded model, writes it as a PNG and/or puts it on the item; `text.get` (`key`, `table`) returns a text and the languages.

## What is tracked

`ModAssets.List()` (and the bridge command `assets.list`) reports per mod each texture, sprite, sound and model with its size, source file and the memory Unity reports. Use `oben call assets.list mod=MyMod` to check that nothing is held that shouldn't be, or that a reload released everything.

## Notes

- Both caches are keyed by full file path, so loading the same sprite from two places in your mod costs once. Changing the file on disk doesn't reload it; reload the mod.
- The framework compiles against .NET Framework reference assemblies, which lack `ReadOnlySpan`. Two engine calls that gained span overloads (`ImageConversion.LoadImage`, `AudioClip.SetData`) are called through delegates in `EngineCalls`. If you copy this code into your own mod and the compiler complains about `ReadOnlySpan`, do the same.
- The OBJ, MTL, WAV and image-header readers and the vertex layout (`src/ObenFramework/Assets/Formats/`) use no Unity types and are unit tested in `tests/ObenCore.Tests`.
- The parsers are adapted from the Obenseuer Modding Framework by NomadsReach (MIT); see THIRD-PARTY-NOTICES.md.
