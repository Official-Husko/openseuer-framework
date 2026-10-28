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

`ModSounds.Group("Name")` gives any group by name and `ModSounds.GroupNames()` lists all of them (Soundscape, Muffled, Underwater and so on), if you want a sound to be muffled with the rest. Playing goes through the game's own `AudioSourceMaker`, so the output group is resolved the way the game does it, and sources pause with the game. `Play` and `PlayAt` return the `AudioSource` (null if the audio system isn't up yet) and the object is destroyed when the clip ends.

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
- **MTL:** `Kd` (colour), `Ke` (emission), `map_Kd` (diffuse texture, PNG or JPG, which must be inside the model's folder), and `d`/`Tr`. Transparency is read but not supported: those materials draw opaque (a warning says so).
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

Bridge: `items.model` (`item`, `model`, `held`, `clear`) shows a loaded model on an item, owned by the bridge; `text.get` (`key`, `table`) returns a text and the languages.

## What is tracked

`ModAssets.List()` (and the bridge command `assets.list`) reports per mod each texture, sprite, sound and model with its size, source file and the memory Unity reports. Use `oben call assets.list mod=MyMod` to check that nothing is held that shouldn't be, or that a reload released everything.

## Notes

- Both caches are keyed by full file path, so loading the same sprite from two places in your mod costs once. Changing the file on disk doesn't reload it; reload the mod.
- The framework compiles against .NET Framework reference assemblies, which lack `ReadOnlySpan`. Two engine calls that gained span overloads (`ImageConversion.LoadImage`, `AudioClip.SetData`) are called through delegates in `EngineCalls`. If you copy this code into your own mod and the compiler complains about `ReadOnlySpan`, do the same.
- The OBJ, MTL, WAV and image-header readers and the vertex layout (`src/ObenFramework/Assets/Formats/`) use no Unity types and are unit tested in `tests/ObenCore.Tests`.
- The parsers are adapted from the Obenseuer Modding Framework by NomadsReach (MIT); see THIRD-PARTY-NOTICES.md.
