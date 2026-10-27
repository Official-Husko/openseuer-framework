# Items, inventory, storage and crafting

Notes on how Obenseuer models items and how they move through inventory, storage and crafting. All code pointers are relative to `decompiled/Assembly-CSharp/` unless they name a data file. Data files live in `Obenseuer_Data/StreamingAssets/`.

Everything below is read from the decompile and the shipped JSON. Nothing was run, so anything marked "as read" is an inference from code and not something I watched happen in game.

## 1. Type map

| Type | File | What it is |
|---|---|---|
| `Item` | `Item.cs:9` | Definition of an item (one row of Items.json). Shared, immutable-ish, looked up by int ID. |
| `ItemDatabase` | `ItemDatabase.cs` | Scene MonoBehaviour with a static `List<Item> database`. |
| `ItemReference` | `ItemReference.cs` | Serializable pointer to an `Item` (`name`, `itemnamespace`, `ID`), resolves lazily. |
| `ItemStack` | `ItemStack.cs` | Plain data: `itemId`, `itemAmount`, `ownerId`, meta array. This is what is saved. |
| `SlotController` | `SlotController.cs` | One logical slot. Holds an `ItemStack` plus category filters and callbacks. |
| `StorageSlot` | `StorageSlot.cs` | Empty subclass of `ItemStack`, used by `Storage.storageSlots`. |
| `Slot` | `Slot.cs` | The UI cell for a `SlotController`. Spawns the draggable `ItemData`. |
| `ItemData` | `ItemData.cs` | **UI** MonoBehaviour for the draggable icon in a slot. Not item data despite the name. |
| `Inventory` | `Inventory.cs` | Player slots, equipment slots, helpers. `SavableScript`. |
| `ItemOperations` | `ItemOperations.cs` | Static helpers: add, drop, spawn in world. The usual entry point. |
| `CollectibleItem` | `CollectibleItem.cs` | World pickup component. |
| `Storage` | `Storage.cs` | Container (fridge, crate, crafting station buffers). |
| `BackpackStorage` | `BackpackStorage.cs` | The three equippable bags, each backed by a scene `Storage`. |
| `LiquidStorage` | `LiquidStorage.cs` | Tanks and barrels, one liquid, one float amount. |
| `Money` | `Money.cs` | Two currencies, not stored as items. |
| `Recipe`, `RecipeDatabase`, `RecipeController` | `Recipe.cs` etc. | Recipe rows, loader, and the player's known recipes. |
| `CraftingBase` -> `manufacturing`, `manufacturingProcess`, `Growing` | `CraftingBase.cs` etc. | The three kinds of crafting station. |

The files in `OS.Items/` are the *meta* classes (`ItemPerishabledata`, `ItemLiquidData`, ...) and the *action* subclasses (`ItemConsumable`, ...), all in the `OS.Items` namespace. The base `Item.ItemAction` is nested inside `Item`.

## 2. Item model

### 2.1 Identity

- Primary key is `Item.ID` (`int`, `Item.cs:295`, default `-1`). `ItemStack.itemId` and `ItemReference.ID` store just this number. Saves contain the ID and nothing else about the item.
- `Title` is a plain display string, **not unique** (Items.json has two "Turnip": 12 the food, 10070 the plant). It is still used as a key in a few places:
  - `SlotController.CheckIfInAllowedCategoy` matches `_item_<title>` entries in the allow/forbid lists (`SlotController.cs:716`).
  - `ItemAppearance.GetPrefabByTitle` falls back to `Prefabs/Items/<Title>` when `PrefabPath` is empty (`Item.cs:269`).
  - `ItemDatabase.FindByString` is a substring match on lowercased Title (`ItemDatabase.cs:144`), used by the console `find_item`.
- `Categories` (`string[]`) is the generic tag system: slot filters, recipe grouping, the `Liquid` / `ItemGroup` / `Backpack` special cases, sort order. `Item.Category` is `Categories[0]`. 114 distinct categories exist in the shipped data.
- Prefab: `Item.Prefab` / `PrefabMany` (`Item.cs:362`) lazily call `Appearance.Loadprefab` (`Item.cs:231`), which does `Resources.Load<GameObject>(PrefabPath)`, retries with `StripPath` (removes `.prefab`, `.png`, ...), then falls back to `Prefabs/Items/ERROR`. `PrefabMany` is what gets spawned when the stack amount is not 1.
- ID ranges in the shipped data are loose: 0..300050, 1240 items. 0 = Open Sewer Coin, 1 = Real Money, 10000-19999 holds most of the game, 20000+ is bulk food/drink. There is no registry of free ranges. A mod should pick IDs far above 300050.

### 2.2 Fields

```csharp
public class Item {
  public int ID; public string Title; public string[] Categories;
  public int BaseValue; public string Description;
  [fsProperty("Stackable")] public int BaseStackable;
  public ItemAppearance Appearance; public ItemAction[] Actions;
  public ItemAttachments Attachments; public object[] Meta;
}
```

- `Value` and `Stackable` are computed properties over `BaseValue` / `BaseStackable`, scaled by `DifficultyController.instance.currentDifficultyInfo` (`GetItemPrices`, `GetStackSize`, `DifficultyInfo.cs:247,270`). Stack size is only scaled for the Custom difficulty and only when base > 1. A `Stackable` of 0 is treated as 1 by the stacking code (`SlotController.cs:817`, `ItemStack.cs:213`).
- `ItemAppearance` (`Item.cs:116`): `Material` (key into `ItemSounds`, e.g. "Food", "GlassBottle"), `SpritePath`, `ColorValue` (string `RGBA(0.000, 0.000, 0.000, 0.000)`), `EmissionStrength`, `PrefabPath`, `PrefabMany`, `SkinSpritePaths`/`SkinNames`. Runtime-only fields (`[fsIgnore]`): `Sprite`, `SkinSprites`, `Color`, `Prefab`, `PrefabMany`.
- `Attachments` is carried in the JSON (`Name`, `Attachment1`, `Attachment2`, all "none" in the samples I checked) and I did not find a consumer for it.
- `Item.Instantiate()` (`Item.cs:659`) makes a new `Item` that **shares** `Appearance`, `Actions`, `Categories` by reference and clones only `Meta`.

### 2.3 Actions

`Item.ItemAction` (`Item.cs:11`) is the "use" behaviour. Subclasses, all polymorphic in JSON via `$type`: `ItemConsumable` (types None, Food, Drink, Smoke, Medicine, Bandage, Container, Slaughter, Hygiene), `ItemEquipable`, `ItemMisc`, `ItemReadable`, `ItemReloadable`, `ItemElectronic`, `ItemWritable`. In the shipped data `ItemConsumable` Food/Drink dominate (604 of 788 actions). `Item.InvokeActions(ItemData, bool worlduse)` (`Item.cs:699`) loops the array and calls the virtual `Invoke(ItemData, bool)`. `Item.CloneItemActions()` is a hand-written copier that only knows Equipable, Consumable, Misc, Readable, Reloadable; new action subclasses are silently dropped by it.

### 2.4 Meta (per-instance state)

`Meta` is `object[]` holding at most one instance of each meta class. The `Item.Meta` array is the *template*; each `ItemStack` gets its own clone. Lookup is by exact type: `Item.GetMetaOfType<T>()`, `ItemMetaUtilities.GetMetaOfType<T>(object[])` (logs a warning on null array), `ItemStack.GetMetaOfType<T>()`. Note these use `GetType() == typeof(T)`, so subclasses do not match.

| Meta class (`OS.Items`) | Purpose | Count in Items.json |
|---|---|---|
| `ItemPerishabledata` | Spoilage: `integrity` 0..1, `integrityLossPerHour`, `refigerationFactor`, `spoiledItem` (ItemReference), `dontDestroyAtZero`, `perishingDisabled`, `lastChecked` | 462 |
| `ItemQualityData` | `quality` (1 = normal), `modifies` flags (Value, Depression, SMV, Health, ...) | 378 |
| `ItemAmmodata` | Durability/charges: `ammo`/`maxAmmo`, `destroyWhenEmpty`, `showAsAmount`, `ammoAffectsPrice`, per-minute drain | 156 |
| `ItemFueldata` | Burn time and ash output | 95 |
| `ItemGrowdata` | `growtime`, `growOnlyDuringDay` (on plant items) | 59 |
| `ItemGroupData` | "Any of these items" group used as recipe input/output | 38 |
| `ItemLiquidData` | Container holding a liquid item (see 2.7) | 21 |
| `ItemPackageData` | Boxes with child items (`ChildItem`) | 13 |
| `ItemEmptyLiquidContainerData`, `ItemAnimalData`, `ItemVariantData`, `ItemWallpaperData`, `ItemConnectivityData`, `ItemMailableData`, `ItemReceiptData`, `ItemScraperData`, ... | niche | few each |
| Runtime-only (never in Items.json) | `ItemStolenData`, `ItemOwnerPermission`, `ItemSkinData`, `ItemShopData`, `ItemTextData`, `ItemTaskItemData` | added by code |

Cloning: `Item.CloneMeta(object[] Meta, bool fullCopy=false, int stackSize=1, Item item=null)` (`Item.cs:873`) is a long `is`-chain that deep-copies known types and **falls through to `list.Add(obj)` (same reference) for anything else**, and also for several types unless `fullCopy` is set (`ItemFueldata`, `ItemFurnitureData`, `ItemGrowdata`, `ItemGroupData`, `ItemSkinData`, ...). Two consequences for mods: a new meta class is shared between stacks unless `CloneMeta` is patched, and new fields added to an existing meta class are not copied unless the matching branch is patched. If `item` is passed and the item has skins, a random `ItemSkinData` is appended (`Item.cs:1226`).

`ItemStack.Meta` (`ItemStack.cs:39`) is lazy: when `_meta` is empty and `itemAmount > 0` the getter/setter clones the item's template meta. Setting it to null/empty on a live stack re-populates defaults.

### 2.5 ItemReference

`ItemReference.Item` (`ItemReference.cs`) resolves through `ItemDatabase.instance.FetchItemByID(ID)` (linear scan) and caches only if the result has `ID > 0`, so the coin (0) and unresolved items re-scan every access. `itemnamespace` selects `ItemDatabase.extendedDatabase[ns]` (`ItemDatabase.cs:127`), which is **declared but never filled** by game code. Stacks save only `itemId`, so namespaced items cannot live in inventories; do not rely on it.

Quirks:
- `new ItemReference(int id, string ns)` looks up with the *field* `itemnamespace` (still null at that point) rather than the argument, so `extendedDatabase.ContainsKey(null)` should throw `ArgumentNullException` (`ItemReference.cs:69`). Do not use that constructor.
- `new ItemReference((Item)null)` yields `ID = 0`, which then resolves to the coin. As read, an item ID that no longer exists in the database (mod removed) will display as Open Sewer Coin in a loaded slot: `Inventory.OnLoadingGame` (`Inventory.cs:139`) and `ItemStack.SetItemReference` (`ItemStack.cs:147`) both wrap `FetchItemByID(id)` in `new ItemReference(...)` without a null check.

### 2.6 ItemStack, SlotController, Slot, ItemData

- `ItemStack` fields: `itemReference` (fsIgnore), `itemId`, `itemAmount`, `ownerId`, `refigerated`, `spoilDisabled`, private `_meta`. `ownerId` is an NPC ID; `owner < 2` means "nobody" in the theft code (`Inventory.cs:384`), `-1` is the default.
- Build one: `ItemStack.CreateStackFromItem(Item item, int amount=1, int ownerId=-1, object[] meta=null)` (`ItemStack.cs:313`). Copy: `new ItemStack(ItemStack)` or `CopyItemStack()`.
- `ItemStack.AddItemToPanel(ref Item, out int remaining, ref object[] meta, int amount, int stackAmount, int owner, bool onlycheck, bool mergingLiquidStackAlways, ItemOperationSettings)` (`ItemStack.cs:168`) is the merge/add primitive. It also swaps an emptied liquid container for its `emptyContainerItem`.
- `ItemStack.RemoveItemAmount(int)` / `RemoveStack()` (`:246`, `:258`). `RemoveStack` fires `SlotController.OnTaken` for whichever inventory slot owns that stack, then resets to `itemId=-1`.
- `SlotController` (`SlotController.cs`): `itemStack`, `AllowedCategories`, `ForbiddenCategories`, `DefaultForbiddenCategories` (`"Storage"`, `"Unstorable"` by default), `Siblings` (all slots of the same container), and four delegates `OnDropped`, `OnTaken`, `OnReturn` (`Action<Item,int,int,object[]>` = item, amount, owner, meta) and `OnDroppedCheck`. Equipment slots wire these to hand/head/face/watch/backpack controllers (`Inventory.cs:265`).
- `Slot` is the UI cell. `Slot.UpdateSlot()` (`Slot.cs:297`) creates or refreshes the `ItemData` child, and `Slot.CheckItemMetas` (`:480`) is where inventory spoilage actually happens (2.7).
- **A stack whose item has no sprite is drawn as an empty slot**: `UpdateSlot` and `SetSlotController` bail out when `item.Appearance.GetSprite(meta) == null`. The data is still there. New items must have a `Sprite`.
- `ItemData` is draggable UI. It has `item`, `amount`, `owner`, `meta` accessors that proxy its own `stack`. Drag and drop ends in `SlotController.DropItem(ItemData data, out int remaining, bool isSibling=false, bool allowItemSwap=false, bool otherStorage=false)` (`SlotController.cs:195`). `ItemData.QuickMove(int)` (`ItemData.cs:543`) is shift-click.

### 2.7 Stacking, durability, spoilage, liquids

Stacking rule (`SlotController.SlotHasSpaceForItem`, `SlotController.cs:788`): same `itemId`, and owners must match (both `< 1`, or equal), `ItemMetaUtilities.MetaMergeAllowed` must accept the meta pair (`ItemMetaUtilities.cs:128`, compares via `CompareItemMetas` at `:167`), and `itemAmount < item.Stackable`. Items owned by different NPCs, stolen vs not, or with different liquid contents do not merge. Owned stacks (`ItemOwnerPermission` meta present) disable liquid merge and quality mix (`SlotController.cs:~70`).

Spoilage (`ItemPerishabledata`):
- Time is game time (`TimeOfDayAzure.instance.currentTimeAndDay`). The meta keeps `lastChecked`; `CountPerishingTime` subtracts `minutes/60 * integrityLossPerHour * factor` (scaled down by refrigeration if `refigerationFactor != 0`) since the last check. A freshly cloned meta has `lastChecked.TotalMinutes == 0` which means "start counting now".
- Evaluation is lazy and driven by whoever touches the item: `Slot.CheckItemMetas` for items in a UI slot (so a slot is only brought up to date when it updates; `Inventory.DeltaSeconds` forces that only while the inventory panel is open, `Inventory.cs:303`), `CollectibleItem.UpdateItem` for world items, `Storage.AddItem` with `existedInSeconds` for freshly produced items.
- When integrity hits 0 and `dontDestroyAtZero` is false, the stack is removed and replaced by `spoiledItem` (same amount).
- Price scales by integrity (`Item.GetItemSalePriceUnrounded`, `Item.cs:575`).
- Fridges/freezers feed `ItemStack.refigerated` via `Storage.RefigerationFactor`; `StorageCategory.Refigerated` sets the default.

Durability is `ItemAmmodata`: `ammo` of `maxAmmo`. `ChangeAmmoDirect(SlotController|ItemData, float amount)` changes it and consumes the item when empty and `destroyWhenEmpty`. UI shows `ammo/max` or a percent (`ItemData.cs:~280`). `Inventory.FindAndChangeItemAmmo` is the inventory-wide helper.

Liquids: a liquid is an ordinary item with category `"Liquid"` (181 in the data). A container holds one in `ItemLiquidData` (`liquidItem`, `liquidAmount` per container unit, `liquidMaxAmount`, `emptyContainerItem`). Amounts are rounded to 0.1 (`roundValue = 10`) and are **per stack**: `GetLiquidAmount(stackSize)` multiplies. Helpers: `ItemLiquidData.SetLiquidAmount`, `Item.CloneMetaAndSetLiquid` (`Item.cs:834`), `ItemMetaUtilities.CheckMetaEffects`. `ItemOperations.AddLiquid` exists (`ItemOperations.cs:176`) but `ItemOperations.RemoveLiquid` is a stub that logs "TODO" and returns false (`:261`); use `Inventory.RemoveLiquid(int id, float amount, ...)` (`Inventory.cs:1090`).

### 2.8 CollectibleItem (world pickups)

`CollectibleItem : Collectible : Interactable` (`CollectibleItem.cs:9`). Public state: `ItemReference Item`, `int amount`, `object[] meta`, `bool permissionToTake`, `bool disablePerishable`, `int skin`, `NPCReference owner` (from `Collectible`).

- The `Item` reference is **baked into the prefab** by the editor. `Start()` destroys the object with "Collectible item is null!" if it does not resolve (`:111`).
- Pickup path: `Interact` -> `PerformPickup()` (`:333`) -> `GetItem()` (`:705`) -> `ItemOperations.AddItemAndReturnRemaining(Item.Item, amount, owner, slot:null, meta, 0, settings{disableQualityMix=true, ignoreBackpack=true})`. The pickup keeps whatever did not fit in `amount`. World pickups therefore never overflow into the backpack.
- `CollectibleItem.GetItemStack()` (`:823`) builds an `ItemStack` from the pickup. `ChangeItemReference(ItemReference)` (`:608`) just reassigns `Item`.
- Dropped/spawned pickups are persisted by `DestructibleList` (`DestructibleList.cs`). `Collectible.AddToDestructibleList` -> `DestructibleList.SpawnItem(GO, prefabPath)` (`:94`) rebuilds the prefab path from the GameObject's **name** (`name.Split('(')[0]`), tries `Resources.Load(path)`, and otherwise falls back to `GetPrefabByTitle`. On load, `OnLoadingGameDestructibleList` (`:47`) does `Resources.Load(prefab.PrefabPath)` and logs "Not found!" for failures, dropping the object. Effect for modded prefabs: see section 7.

Other spawn-related components: `CollectibleItemSpawner` (category-driven pickup spawner), `SpawnItemsPrefab`, `ItemSpawnTarget` (allowed categories/size for a spot), `InteractableItemGiver`, `AddItemInventory` (inspector-driven `ItemOperations.AddItem`).

## 3. Data files

### 3.1 Items.json

- Path: `Application.dataPath + "/StreamingAssets/Items.json"`, UTF-8, CRLF, a bare JSON array of 1240 items.
- Loaded by `ItemDatabase` (`ItemDatabase.cs`): `Awake` (`:19`), `OnEnable` (`:45`, editor-ish), public `Reload()` (`:63`). Each reads with `File.ReadAllText` and deserializes with FullSerializer (`DeSerialize`, `:200`). The class is `[ExecuteInEditMode]`.
- `Awake` sets `instance = this` and then returns early if the static `database` already has entries, so the file is parsed once per process, not once per scene.
- After parsing, `Awake` calls `Appearance.LoadSprite()` and `GetColor()` for every item and `LoadItemReferencedAssets` for items with meta (`:72`, loads sprites for perishable overlays, liquid masks, packages, furniture).
- Which scene hosts the `ItemDatabase` and `RecipeDatabase` components cannot be told from the decompile; they are scene objects.
- `DeSerialize` ends in `AssertSuccessWithoutWarnings()`, which **throws on any warning**. `Awake` has no try/catch around the per-item loop, and `GetColor` does `Substring(5, len-6)` plus float parsing on `ColorValue` with no guard. One malformed entry can take down database load, so validate added entries.

Item row schema (observed keys; every one of 1240 rows has exactly these):

```json
{ "ID": 12, "Title": "Turnip", "Categories": ["Food","Plant"], "BaseValue": 1,
  "Description": "...", "Stackable": 16,
  "Appearance": { "Material": "Food", "SpritePath": "Sprites/Items/Turnip_spr",
    "ColorValue": "RGBA(0.000, 0.000, 0.000, 0.000)", "EmissionStrength": 0.0,
    "PrefabPath": "Prefabs/Items/Turnip", "PrefabPathMany": "Prefabs/Items/Turnip",
    "SkinSpritePaths": [], "SkinNames": [] },
  "Actions": [ { "$type": "OS.Items.ItemConsumable", "Type": "Food", "Amount": 8, ... "Name": "Eat",
                 "UseSound": "Sounds/use/eating_crisp_001", "UseAnimation": "None", ... } ],
  "Attachments": { "Name": "", "Attachment1": "none", "Attachment2": "none" },
  "Meta": [ { "$type": "OS.Items.ItemPerishabledata", "integrityLossPerHour": 0.001,
              "spoiledItem": { "name": "Moldy waste", "itemnamespace": null, "ID": 10550 }, ... } ] }
```

- Polymorphic members (`Actions[]`, `Meta[]`) carry `"$type": "Namespace.Class"`. Item references are `{name, itemnamespace, ID}`; only `ID` is used for lookup, `name` is a label. FullSerializer also writes `"$id"` markers on some references (harmless).
- `Stackable` in JSON maps to `BaseStackable`; `Value` is not in the file, `BaseValue` is.
- Asset paths (`SpritePath`, `PrefabPath`, `UseSound`, `spritePath` overlays, `wieldPrefabPath`) are `Resources` paths with no extension. They are all `Resources.Load` calls against assets baked into the build.
- Only one shipped item has an empty `PrefabPath` (falls back to `Prefabs/Items/<Title>`).

### 3.2 Recipes.json

- Path: `StreamingAssets/Recipes.json`, **UTF-16 LE with BOM** (the first bytes are `FF FE`). Root is `{ "Recipes": [ ... ] }`. 929 recipes, 34 `Type` values, 113 with `RecipeRequired: true`.
- `RecipeDatabase.LoadDatabase` (`RecipeDatabase.cs:81`, called from `Awake` when the static `Recipes` list is empty) reads with `Encoding.Unicode`, deserializes `RecipesHeader`, then for each recipe calls `Appearance?.LoadSprite()` and `SetItems()`. Quirk: the existence check is on `recipes.json` (lowercase) while the read is `Recipes.json`. It works for the shipped game (Wine resolves case-insensitively); if it ever returned false the database would silently stay empty.
- `SetItems()` (`Recipe.cs:456`) turns the serialized `input` / `output` / `outputFail` (`Dictionary<ItemReference,float>`, written as a list of `{Key, Value}`) into runtime `Input`/`Output`/`OutputFail` (`Dictionary<Item,float>`). It does nothing if `ItemDatabase.instance` is null, and logs "Skipping recipe!" for unknown item IDs (the entry is dropped from that dictionary, the recipe stays).
- Recipe row keys: `ID, Name, Type, useOnlyStolenItems, allowedNonStolenItemsIds, keepItemOwnerOnFail, disableJunkSpawn, Appearance{SpritePath, CraftShowModel, itemToShow}, RecipeRequired, RequiredConditions[], ExcludingConditions[], RecipeTags[], variantItemCategory, variantItemAmount, input[], output[], outputFail[], metaSettings{...}, FailChance, RelevantSkill, MinimumSkill, HardMinimumSkill, SkillFactor, SkillGainFactor, ProcessTime, Meta`.
- `Type` is `RecipeType` (`RecipeType.cs`): Basic, Oven, Stove, Mashing, Fermenter, Distillery, WaterSource, Grinder, Machining, ChemicalLab, Woodworking, Microwave, CoffeeMaker, PaperWork, Grow, GrowMushrooms, BreedAnimals, Misc, SludgeTank, Engineering, Electronics, GrowVines, Debug, Inventory, Furnace, Printer3D, Assembly, Compost, Kitchen, Malting, Sewing, Crusher, Loom, Anvil, ChemicalRefinery, GrowAquatic, Press, Bottler.
- `RelevantSkill` is `PlayerStats.PlayerSkillType` (Cooking, Farming, Brewing, ...). `RequiredConditions`/`ExcludingConditions` are `RecipeCondition` (Clean, Dirty, Crude, Sun, Steam, Large, ...); stations expose a `Modifiers` list that is matched against them. `ProcessTime` is in game seconds (`GetProcessTime`, `Recipe.cs:245`).
- Data oddities in the shipped file: two recipes with `ID: -1` (both `Debug`), and a duplicate `ID: 10421` ("Apple mash" and "Green Orb Mash", both `Mashing`). `FetchRecipesById` returns the first match, so `RecipeReference` / saved recipes resolve to the first one. No recipe references a missing item ID.
- `RecipeDatabase.AddRecipe`, `RemoveRecipe`, `EditRecipe` (`:58-75`) end in `SaveDatabase()`, which **rewrites `Recipes.json` in the game folder**. Do not call them from a mod. Add to the static `RecipeDatabase.Recipes` list directly.

### 3.3 Other data files that touch items

- `StoragePrefabs.json` -> `StorageCategoryDatabase` (`id, Name, Panels, Refigerated`, open/close/lock sounds). `Storage.SetCategorySettings` (`Storage.cs:587`) uses it to size and style a container from its `StorageSettings` name.
- `StorageSpawnCategory.json` -> `StorageSpawnCategoryDatabase`: `ID, Name, ItemCommon[], ItemRare[], MetaValues{...}`. Drives random loot in `Storage.SpawnRandomItemsByCategory` and `CollectibleItemSpawner`. A new item only appears in world loot if added here (or to a spawner's custom list).
- `Wearables.json`, `Characters.json` etc. are separate systems.

## 4. Adding items and recipes from a mod

What works (from reading the loaders):

1. **Items.** Build `Item` objects in code (or deserialize your own JSON through `ItemDatabase.DeSerialize(typeof(List<Item>), json)`) and append to the static `ItemDatabase.database` after the vanilla load. Hook: a Harmony postfix on `ItemDatabase.Awake` (private; runs once per scene instance, so skip IDs that already exist) and on `Reload`. For each new item call `ItemDatabase.LoadItemReferencedAssets(item)` yourself because the vanilla loop in `Awake` already finished. Because `database` is static and the guard is `Count > 0`, once vanilla has loaded you can also mutate it from anywhere before the first lookup of your IDs.
2. **Recipes.** After `RecipeDatabase.LoadDatabase` (private) postfix: `Recipe.Appearance?.LoadSprite()`, `recipe.SetItems()`, then `RecipeDatabase.Recipes.Add(r)`. Skip IDs already present. Recipe objects built in code need the *serialized* dictionaries filled (`input`, `output`, `outputFail` of `ItemReference` -> amount), a non-null `Appearance` (`RecipeController.OnLoadingGameDelay` dereferences it) and `metaSettings`, then `SetItems()`.
3. **Timing matters for stations.** `CraftingBase.UpdateRecipes()` (`CraftingBase.cs:478`, private, called from `Start`) snapshots `RecipeDatabase.FetchRecipesByType(type)` for each of the station's `Recipes` types into `AllRecipes`. A recipe added after a station has started will not show up there until that station is rebuilt. Add recipes before the first gameplay scene loads, or re-invoke `UpdateRecipes` on live `CraftingBase` instances.
4. **Station types are fixed.** The set of `RecipeType`s per station is an inspector array on the scene object. A new recipe has to use an existing `RecipeType`; a new type would need an enum patch plus station edits.
5. **Making a recipe visible.** `RecipeRequired = false` shows it to everyone. `true` needs the player to have it in `RecipeController.collectedRecipes` or `learnedRecipes` (`HasRecipe`/`HasLearnedRecipe`, `RecipeController.cs`; checked in `RecipeListUI.cs:36,52` and `Growing.cs:896`). `RecipeController.AddRecipe(Recipe, bool learned=false, bool notification=false)` grants one. Crafting with a recipe also auto-learns it (`manufacturing.Craft` calls `AddRecipe(selectedRecipe, learned:true)`).

What else a new item needs:

| Need | How it is resolved | Mod options |
|---|---|---|
| Sprite (inventory icon) | `Appearance.LoadSprite` -> `Resources.Load<Sprite>(SpritePath)` | Leave `SpritePath` empty and assign `Appearance.Sprite` yourself (from an AssetBundle or `Sprite.Create`). **Mandatory**: no sprite means the slot renders empty. |
| World prefab | `Appearance.Prefab` getter -> `Resources.Load<GameObject>(PrefabPath)` | Assign `Appearance.Prefab` / `PrefabMany` before first use (the getter returns it as-is when non-null). Otherwise it resolves to `Prefabs/Items/ERROR`. |
| Pickup behaviour | Prefab must carry `CollectibleItem` (+ collider/rigidbody) | Clone an existing prefab (e.g. another item's `Item.Prefab`) as an inactive template and swap the visuals. See section 7. |
| Overlay sprites (spoil mold, liquid mask) | `ItemPerishabledata.spritePath`, `ItemLiquidData.spritePath` via `Resources.Load` | Optional. `damageoverlay` / `liquidMask` are `[fsIgnore]` fields you can set directly. |
| Sounds | `Appearance.Material` picks the pickup/move sound set in `ItemSounds`; `UseSound` is a `Resources` path | Reuse existing Material names and sound paths. |
| Localization | None found | `Title` and `Description` are used as raw strings (`Item.GetItemTitle`, `GetItemDescription`). I found no code path that translates item names. An "Items" table is written by `ImportStringTables` (`persistentDataPath/Localization/*.csv`, console `import_translations`) but nothing in `Assembly-CSharp` reads it, and `MenuLocalization` only reads the "Menu" table. Scene prefabs might use Localize components that the decompile cannot show. |
| Loot / shop placement | `StorageSpawnCategory.json`, `CollectibleItemSpawner`, shop data | Opt in explicitly, or give via code. |
| Icons for crafting UI | `Recipe.Appearance.SpritePath`, `itemToShow` (a prefab shown on the station) | Optional (`SpritePath` is empty in plenty of shipped recipes). |

ID choice: pick a mod ID range well above 300050 and derive IDs deterministically (hash of mod id + item key, mapped into a reserved block) so they stay stable across sessions, since saves store raw IDs.

## 5. Inventory, storage and money

### 5.1 Inventory

`Inventory : SavableScript` (`Inventory.cs`), singleton `Inventory.instance`.

- `Slots` (`SlotController[]`, `slotAmount = 35`): main grid. Each slot's `Siblings = Slots`, so an add on one slot spills across the grid. `CharacterSlots` (`characterSlotAmount = 8`): equipment, **no siblings**. Indices (`Inventory.cs:39-72`): 0 hat, 1 accessory, `head=2`, `face=3`, `watch=4`, `backpack=5`, `rightHand=6`, `leftHand=7`. They are restricted by `AllowedCategories` in `StartInit` (`:265`): `Watch`, `Head`, `Face`, `Backpack`, `Wieldable Primary`, `Wieldable Secondary`.
- `AllSlots(bool ignoreBackpack=false, bool ignoreCharacterSlots=false)` (`:170`): `Slots` + equipped backpack's `Storage.Slots` + `CharacterSlots`.
- `InPlayerSlot(SlotController)` (`:1464`) is true only for `Slots`; not for equipment or the backpack.
- `ForeignSlots` is set to the slots of whichever `Storage` / panel is currently open next to the inventory (`Storage.cs:761,804`); `null` when none. Quick-move and the nav handler key off it.
- Save/load: `OnSavingGame` copies `Slots`/`CharacterSlots` to `_savingSlots`/`_savingCharacterSlots` and serializes with FullSerializer via `SaveController` (`:124`). On load only slots with `itemAmount > 0 && itemId > -1` are restored and `itemReference` rebuilt from the database (`:139`).
- Queries: `CheckForItem(int id, float amount=1, bool checkAmmo=false, bool ignoreOwner=true, int owner=-1, bool liquid=false, bool ignoreBackpack=false)` (`:1258`), `CountItems`, `GetItemAmountInInventory(int id, bool ignoreBackpack=false)` (`:1381`), `FindItem(...)` (`:1044`), `GetPlayerItems(...)` (`:870`), `GetEmptySlotsAmount`, `InventoryIsNotFull(Item itemToAdd, int owner=-1, int amount=1, int stackAmount=1, object[] meta=null, SlotController[] slotControllers=null, bool ignoreBackpack=false)` (`:1431`).
- Removal: `RemoveItem(int id, bool ignoreOwner=true, int owner=-1, bool ignoreCrime=false, bool ignoreBackpack=false, bool removeAmmo=false)` (`:1002`) takes **one** unit from the first matching slot (liquid-in-container also counts as a match) and returns bool. `RemoveItems(int id, float amount, ...)` (`:974`) just loops that. `RemoveLiquid`, `RemoveItemQuality`, `RemoveStack(int id)` for the other cases. Taking from an NPC-owned stack can commit theft when witnessed unless `ignoreCrime`.
- Lua bridge: `Start()` registers dialogue functions `AddItemDialogue`, `AddItemDialogueAmount`, `RemoveItemDialogue`, `CheckForItemDialogue*`, `InventoryIsNotFull`, ... (`Inventory.cs:~195`). Dialogue passes item IDs as doubles, so IDs must stay below 2^53 (any int is fine).
- Events: `Inventory.instance.ItemConsumed` (`OnPlayerConsume`) fires from `ItemAction.Invoke` via `OnItemConsumed` (swallows exceptions). `ItemInfo.ItemDataChanged` is a static event for the info panel.
- Equip logic: `Equip`/`Unequip`/`EquipItem` (`:1685`, `:1723`), `GetEquipSlotIndex(Item)` (`:1661`) maps categories to slot indices.

### 5.2 ItemOperations (the add/remove entry point)

All static, `ItemOperations.cs`:

```csharp
bool AddItem(int id, int owner=-1, SlotController slot=null, bool notify=false, bool ignoreCrime=false)               // :33
bool AddItem(Item item, int owner=-1, SlotController slot=null, object[] meta=null, bool notify=false, bool ignoreCrime=false) // :51
bool AddItems(int id, int amount=1, int owner=-1, object[] meta=null, SlotController slot=null, bool notify=false, bool ignoreCrime=false) // :61
bool AddItems(Item item, int amount=1, int owner=-1, object[] meta=null, SlotController slot=null, bool notify=false, bool ignoreCrime=false) // :66
int  AddItemAndReturnRemaining(Item item, int owner=-1, SlotController slot=null, object[] meta=null, int amount=1, int stackAmount=0, ItemOperationSettings settings=null) // :96
void ForceItemToSlot(Item item, int amount, int owner, object[] meta, SlotController slot, bool add=false, bool forceMeta=false) // :153
void DropItem(Item item, int amount, int owner=-1, object[] meta=null, GameObject dropper=null, DropItemValues values=null)  // :268
int  RemoveItemAmountFromSlot(SlotController slot, int itemId, int amount, bool checkAmmo=false, bool ignoreOwner=true, int owner=-1) // :505
```

Behaviour worth knowing:
- `AddItems(Item,...)` (the one everything funnels into, `:66`): default slot is `Inventory.GetFirstSlot()`. If the slot is a player slot **and the item ID is 0 or 1 it is converted to money** (`Money.AddMoney(amount, rm: id != 0)`) and no stack is created. Otherwise `slot.AddItem(...)` runs with `dropItems: true`, so overflow is **dropped on the ground** and the method returns false if anything was dropped. For the player it also runs `ItemSpecialTriggers` (achievement check) and `CheckForAddedStolenItems` (marks NPC-owned stacks as stolen and may commit a Theft crime if `owner >= 2` and witnessed, unless `ignoreCrime`).
- `AddItemAndReturnRemaining` (`:96`) does not drop. For a player slot it then spills into `CharacterSlots` (subject to their category rules) and then the equipped backpack unless `settings.ignoreBackpack`. Returns how many did not fit.
- `AddItem(int id, ...)` clones `item.CloneMeta()`; `AddItem(Item, ...)` with `meta == null` relies on `ItemStack.Meta` lazily cloning the template. Both work; pass `item.CloneMeta(false, amount, item)` yourself if you want skin randomization.
- `ItemOperationSettings`: `disableStackMerge`, `disableQualityMix`, `disableLiquidMerge`, `ignoreBackpack`.

### 5.3 Money

`Money : SavableScript` (`Money.cs`), singleton `Money.instance`. Two currencies, `enum Currency { OC, RM }` (Open Sewer Coin, Real Money). State: `int OCMoneyAmount`, `RMoneyAmount` (float, stored internally as tenths in `rMoneyAmount`), `bankAccounts`.
- `AddMoney(float amount, Currency currency)` (`:447`), `AddMoney(float, bool RM)`, `AddMoney(money, bool notify)`; `RemoveMoney(float, Currency)` (`:498`, clamps at 0); `CheckMoney(float, Currency, bool showNoMoney)`; `Buy(int amount, Currency currency, bool showNoMoney=false)` (`:369`, checks then removes).
- Both Add and Remove mirror the value into Dialogue System variables (`DialogueLua.SetActorField("Player", "Money"|"RealMoney", ...)`) and play a sound, so call them on the main thread.
- Money is **not** stored in slots. Coin items (ID 0/1) exist as world pickups (`CollectibleCoin`) and in `AddItems` they convert to balance only when going to a player slot; put one into a `Storage` slot and it is a real stack.
- Console: `add_money currency amount`, `remove_money currency amount`.

### 5.4 Storage

`Storage : Interactable, IItemtransferable` (`Storage.cs:11`).
- Slots: authored as `StorageSlot[] storageSlots` (data) and wrapped at `Start()` into `SlotController[] Slots` with `Slots[i].itemStack = storageSlots[i]` and shared `Siblings` (`Storage.cs:417`). Sizing comes from `StorageSettings` (category name in `StoragePrefabs.json`) or `customStorageSlotAmount`.
- Filters: `AllowedCategories` / `ForbiddenCategories` on the storage plus the per-slot defaults.
- Add: `AddItem(Item item, int amount=1, int owner=-1, object[] meta=null, int existedInSeconds=0, bool overflowToPlayer=false)` (`:1227`) first ages the item by `existedInSeconds` (spoil/animal growth, may swap to `spoiledItem`/`fullGrownItem`), then `Slots[0].AddItem(...)`; overflow drops at `overflowSpot` unless `overflowToPlayer`. Also `AddItemToStorage(ItemStack[, bool overflowToPlayer])` (`:1116`), `AddItemAndReturnRemaining` (`:1121`, no drop).
- Remove/read: `RemoveItem(int id, out int remaining, int amount=1, bool checkAmmo=false, bool ignoreOwner=true, int owner=-1)` (`:1306`), `RemoveItem(bool fullStack=false)` returns an `ItemStack`, `CheckForItem(int id, out float missing, ...)` (`:1430`), `GetItemAmount`, `IsEmpty`, `IsFull`, `TakeAll()`, `MoveToOtherStorage(SlotController)`.
- Respawning loot: `SpawnItems()` (`:912`) and `spawnSettings`; random categories use `StorageSpawnCategoryDatabase`.
- Save: `OnSavingGame`/`OnLoadingGame` (`:311`, `:325`) serialize the whole component including `storageSlots`; on load slots are resized to the current prefab's count (`ResizeLoadedSlots`, only drops empty trailing slots) and `SetItemReference()` is called on each.
- `Storage.currentStorage`, `Storage.active` are statics for the open container.

`LiquidStorage` (`LiquidStorage.cs`) is separate from `Storage`: one `ItemReference Liquid`, `float LiquidAmount`, `Capacity`, `Meta`, optional `perishableData` / `qualityData`. API: `SetLiquid(ItemReference liquid, float amount, object[] meta=null, bool force=false)` (`:706`), `AddLiquidFromItem(ref ItemStack)` (`:648`), `FillItem(ref ItemStack)` (`:776`, pours into a held container), `ItemCanBeFilled/Unfilled`, `EmptyTank`, `ClearLiquid`. Tanks can be chained with `externalInputStorage` / `externalOutputStorage`.

### 5.5 BackpackStorage

Not a general container system: exactly three bags (`enum Backpack { None, Default, Rucksack, CoolerBag }`), each a `[SerializeField] Storage` in the scene plus two `ItemReference`s (full and empty item variants) (`BackpackStorage.cs`).
- The equipped bag is whatever stack is in `Inventory.instance.CharacterSlots[5]`. `GetStorage()` (`:263`) returns the scene `Storage` for that item ID via `GetTypeById`. Bag *contents live in that scene Storage*, not in the item stack, and are saved as part of that Storage.
- `UpdateEmptyState()` swaps the item ID between the "empty" and "full" variant depending on `Storage.IsStorageEmpty()`. This rewrites `itemId` on the equipment slot.
- Dropping a bag into the world (`ItemOperations.InstantiateItem`, `:439`) moves the contents into the dropped prefab's `Storage` child.
- A fourth bag type needs a new scene Storage and enum/`GetTypeById` patches; adding items to the three existing bags is just `AddItem` on `BackpackStorage.instance.GetStorage()`.

### 5.6 Inventory UI and `InventoryNavigationHandler`

- The slot grid is built in `Inventory.Start` (`:200-230`): for every `SlotController` it calls `CaptureSlot(parent)` to instantiate a `Slot` prefab, so inventory capacity is bound to UI creation, not just data.
- `InventoryNavigationHandler` (`InventoryNavigationHandler.cs`, singleton `Instance`) is the **keyboard/gamepad cursor** layer. It wraps each `Slot[]` grid in `InventoryNavigationPanelData<T>` (rows, columns, `T[,] SlotsTwoDimensional`) and assigns each `Slot.SimulatedCoordinates`. It exposes `Inventory`, `Storage`, `EquipmentPanel`, `ManufacturingPanelInventory`, `GrowingPanelInventory`, ... and tracks the focus with the `CurrentInventoryWindow` enum (`Inventory, Storage, EquipmentPanel, OtherPanelInventory, OtherPanelRecipe, ...`).
- It treats storage as open when `Inventory.instance.ForeignSlots != null` or `LiquidStorage.currentStorage != null` (`:318-322`). It re-initialises per panel (`InitializeStorageSlots(Slot[])` `:847`, `InitializeManufacturingPanelRecipeList(bool)` `:753`, ...). It does not own item data; mouse drag-and-drop goes through `ItemData`/`Slot.OnDrop`/`SlotController.DropItem` and the nav handler drives the same paths for controller input. Any extra panel a mod adds needs its slots registered here or controllers will not be able to reach them.

## 6. Crafting

### 6.1 CraftingBase

`CraftingBase : Interactable` (`CraftingBase.cs:11`, `[RequireComponent(typeof(Storage))]`) is the shared station logic; it keeps its input/output in its own `Storage` (`InputOutputStorage`, optional `OutputStorage`/`OutputTank`).
- Inspector config: `RecipeType[] Recipes`, `List<RecipeCondition> Modifiers`, `EquipmentQuality`, `SkillGainFactor`, `_Power` (fuel/electric), `upgrades`.
- Derived lists: `AllRecipes`, `ExecutableRecipes`, `NotExecutableVisibleRecipes`; `UpdateRecipes` builds `AllRecipes` (3.2, section 4).
- `RemoveItems(Recipe recipe, out List<KeyValuePair<ItemStack,int>> usedItemReferences, out CraftingInputData inputData, int multiples=1, bool dontRemoveItems=false, ItemStack[] items=null)` (`:914`) consumes inputs, handles item groups, ammo-as-input and liquid containers (`RemoveLiquids`, `RemoveAmmo`), and gathers average quality / ages / skins into `CraftingInputData`.
- `CreateItems(Recipe, List<KeyValuePair<ItemStack,int>> usedItemReferences, int multiples, bool forcefail, CraftingInputData, int hasBeenDoneInSeconds)` (`:544`, `protected virtual`) is the common output step: `recipe.GetCraftingResult(...)` rolls success vs fail, outputs go in with `OutputStorage.AddItem(...)` using `ApplyQuality`, skin meta, `RecipeLiquidItems` for liquid outputs, then reserved containers are returned. This is the single best hook for "an item was crafted".
- `Recipe.GetCraftingResult` (`Recipe.cs:315`): success roll = `PlayerStats.SkillSuccess(RelevantSkill, CurrentFailChance(...))`; picks `Output` or `OutputFail`; for each output item it `CloneMeta`s (note: it assigns the clone back onto the shared database `Item.Meta`), resolves `ItemGroupData` outputs, and applies `metaSettings` (set integrity, ammo fraction, variant, mail target, certificate link, packaged items).
- Fail chance and time scale with the skill: `Recipe.CurrentFailChance` (static + instance), `GetProcessTime(multiplier, equipmentQuality)` (adds time if below `MinimumSkill`, then `DifficultyInfo.GetItemCraftingTime`).
- Group inputs: an `Input` item whose meta has `ItemGroupData` matches any `itemGroup[*].item` (`GetRecipeMultiplier`, `CraftingBase.cs:1443`).

### 6.2 manufacturing (timed, parallel batches)

`manufacturing : CraftingBase` (`manufacturing.cs`). State: `AllowSimultaneous`, `RequirePlayer`, `Autocraft`, `List<CraftInProgress> craftsInProgresses` (each: `TimeOfDayAzure.Timer timer`, `Recipe recipe`, `usedItemReferences`, `inputData`, `hasBeenDoneInSeconds`; `CraftInProgress.cs`).
- `Craft()` (`:348`): requires power; `RemoveItems(selectedRecipe, ...)`; `processTime = GetProcessTime(inputData)`; if > 0 it creates a `TimeOfDayAzure.Timer(CraftDelayed, processTime)` and a `CraftInProgress`, else runs `OnCraftDone` immediately. Finally `RecipeController.AddRecipe(selectedRecipe, learned: true)`.
- `CraftDelayed(Timer)` (`:485`) -> `OnCraftDone(...)` (`:503`, private) -> `CreateItems`, then relay outputs (`onDone`, `onCraftSuccess`, `onCraftFailed`).
- Timers are game time and are serialized: `OnSavingGame` calls `timer.OnSaving()`, `LoadDelayProcess` calls `timer.OnLoading(fuelOutTime)` to catch up elapsed time and re-binds the callback with `UpdateTimerSettings`. `hasBeenDoneInSeconds` is passed to `CreateItems` so freshly produced perishables age by the time they sat done. Stopped timers when unpowered.
- The in-progress `Recipe` object is part of the station's save. After load `recipe.SetItems()` is called per craft.

### 6.3 manufacturingProcess (one big batch with fill-up)

`manufacturingProcess : CraftingBase` (`manufacturingProcess.cs`). Used for fermenters, stills, vats. Stages `LoadingItems -> Processing -> Ready | Failed` (`enum Stage`). Holds `ActiveRecipe` (`KeyValuePair<Recipe,int>`, int = multiples), `process` (a `TimeOfDayAzure.Timer`), `liquidRemaining` (dictionary item ID -> amount, from `CraftingBase`) and `capacity` (deciliters).
- `StartProcess()` (`:159`) -> `RemoveItems` -> `ProcessStarted()`; `FinishProcessing` -> `EndProcessing(Stage)` (`:323`) -> `CreateItems(...)` override, which maps its result to `stage = Ready/Failed`.
- `FillRecipes()` / `SelectedFillRecipe` pick the recipe from the liquid currently in the tank.

### 6.4 Growing and grow (plants)

- `Growing : CraftingBase, IQuickSlotTarget` (`Growing.cs`) is a planter bed. It holds `List<GrowingSpot> GrowingSlots`. Planting: `StartGrow(Recipe, int amount, GrowingSpot, ItemStack[])` (`:344`, private) consumes the seed inputs via `RemoveItems`, then for each free spot calls `PlantExternal(Item plant, GrowingSpot, int age, int ownerid, int agePercentage=-1, int currentSkin=-1)` (`:425`, public). `plant` is `recipe.Output.Keys[0]`: a Grow recipe's **output is the plant item**, whose `Prefab` is the plant GameObject (e.g. item 10070 "Turnip", category `Growable_plant`, prefab `Prefabs/Static/Plants/Turnip_001`, with `ItemGrowdata{growtime: 80000}`), not the harvested food.
- `PlantExternal` -> `GrowingSpot.AddPlant(Item plant, bool destroyOldPlant=false, int skin=-1)` (`GrowingSpot.cs:121`) instantiates `plant.Prefab` as a child and gets its `grow` component. It then sets `FullGrownAge` from `ItemGrowdata.growtime`, divided by `EquipmentQualityWithUpgrades`, +/- `FullGrownAgeVariation`.
- `grow : Interactable` (`grow.cs`) is the plant: `Age`, `FullGrownAge`, `TimeResolution`, `growthStages[]` (models per stage), `grownItems[]`/`uprootItems[]`/`seedItem` (`ItemAmount{ItemReference item, Vector2Int amount}` ranges), `visibleGrownItems[]` (`GrowItem` children that are the harvestable fruits), `growSpoil`, `Rotting`, `breedItem` for animals. `GrowItem.GetItem()` (`GrowItem.cs:234`) is the harvest: it builds meta, applies `CraftingBase.ApplySkillQuality`, and calls `ItemOperations.AddItems(...)` with owner `grow.ownerId`. Spoiled plants give the `spoiledItem` instead.
- `Growing.Harvest(GameObject)` / `Harvest()` (`:157`, `:200`) handles automatic harvest; `ManualHarvest` turns it off.
- Start state of a world bed comes from `StartGrowingItems` (item, slot, growPercentage, owner).
- Growth is driven by game seconds; `GrowOnlyDuringDay` pauses at night, forced off when the bed is powered and not `AlwaysOn` (`Growing.cs:484`, `:715`).
- All plants (including animals via `BreedAnimals`/`GrowAquatic`/`GrowMushrooms`/`GrowVines` types) go through the same classes; the Grow recipe types are what route recipes to a Growing bed.

## 7. Spawning from code

Developer console reference: F1 toggles it (`DeveloperConsole.cs:356`). `add_item id amount` ->`DeveloperConsoleRoutines.TryAddingItemsToInventory(string[])` (`DeveloperConsoleRoutines.cs:60`) does `ItemDatabase.instance.FetchItemByID(id)` and then loops `ItemOperations.AddItem(item)` `amount` times, so it is one unit per call, no meta passed. `find_item name` prints IDs. `add_money`, `remove_money` exist.

Give to the player:

```csharp
var item = ItemDatabase.instance.FetchItemByID(id);
if (item != null)
    ItemOperations.AddItems(item, amount, owner: -1, meta: item.CloneMeta(false, amount, item), notify: true);
// or, no ground drop, returns leftover:
int left = ItemOperations.AddItemAndReturnRemaining(item, -1, null, null, amount);
```

Put into a specific container: `storage.AddItem(item, amount, -1, meta, 0, overflowToPlayer: false)` or `slotController.AddItem(...)`.

Spawn in the world:

```csharp
var stack = ItemStack.CreateStackFromItem(item, amount);
ItemOperations.DropItem(stack, PlayerLocator.instance.gameObject);   // random offset, ground raycast
GameObject go = ItemOperations.SpawnItemToPosition(stack, pos, default(RaycastHit)); // exact position
```

`SpawnItemToPosition` (`:357`) picks `Prefab` if `itemAmount == 1` else `PrefabMany`, `Instantiate`s it with a random Y rotation, and `SetCollectibleItemValues` (`:365`) copies `amount`, `owner`, `meta` onto the `CollectibleItem`, starts the expiration timer and clears `useSpawnInfo`/`GUID`. If the hit is an Elevator/Door/TrackTrain it parents to it; otherwise with a building manager active it attaches a `FurniturePlaceable`.

Gotchas that matter for modded pickups:
- `SetCollectibleItemValues` does **not** set `CollectibleItem.Item`. The prefab's baked reference decides what you pick up, so spawning item B using item A's prefab yields A on pickup. A postfix on `SetCollectibleItemValues` calling `component.ChangeItemReference(item.itemReference)` fixes that and lets many mod items share a prefab.
- Persistence: a spawned pickup is registered by `DestructibleList.SpawnItem`, which stores a `Resources` path built from the GameObject name. A cloned/runtime prefab named something that is not in `Resources` will be saved with the ERROR path or an unloadable path and will not come back after loading. Handle it by patching `DestructibleList.SpawnItem` and `OnLoadingGameDestructibleList` to map your prefab names to your own templates.
- Pickups of items with `Storage` children (bags) get contents moved over in `InstantiateItem`.
- The prefab needs `CollectibleItem`. For other component types: `CollectibleCoin` (coins) is also handled; anything else is instantiated but not initialised.

## 8. Harmony hook points

Overloaded names need the argument list given to Harmony. Methods marked P are private (use `AccessTools.Method`).

| Goal | Target | Notes |
|---|---|---|
| Register items | `ItemDatabase.Awake` (P, `:19`) postfix, `ItemDatabase.Reload()` (`:63`) postfix, or `ItemDatabase.DeSerialize(Type, string)` (static, `:200`) postfix when `type == typeof(List<Item>)` | Append to static `ItemDatabase.database`, call `LoadItemReferencedAssets`. Idempotent. |
| Register recipes | `RecipeDatabase.LoadDatabase` (P, `:81`) postfix | Add to static `RecipeDatabase.Recipes`; do not call `AddRecipe` (writes the file). |
| Refresh stations | `CraftingBase.UpdateRecipes` (P, `:478`) | Call on live instances after adding recipes. |
| Item given to player | `ItemOperations.AddItems(Item,int,int,object[],SlotController,bool,bool)` (`:66`), `AddItemAndReturnRemaining(Item,int,SlotController,object[],int,int,ItemOperationSettings)` (`:96`) | Everything (pickups, rewards, harvests) lands here. `AddItem(...)` overloads just forward. |
| Per-slot add/remove | `SlotController.AddItemAndReturnRemaining(Item,int,int,object[],bool,GameObject,bool,int,DropItemValues,ItemOperationSettings)` (`SlotController.cs:63`), `SlotController.TakeItem(Item,int,int,object[])` (`:586`) | Covers storage and equipment too. Also assign `OnDropped`/`OnTaken` on a slot instead of patching. |
| Drag/drop and quick move | `SlotController.DropItem(ItemData,out int,bool,bool,bool)` (`:195`), `ItemData.QuickMove(int)` (`ItemData.cs:543`), `ItemData.DragDrop()` (`:420`) | UI-initiated moves. |
| Remove | `Inventory.RemoveItem(int,bool,int,bool,bool,bool)` (`Inventory.cs:1002`), `Inventory.RemoveItems(int,float,...)` (`:974`) | One unit per call. |
| World drop | `ItemOperations.DropItem(ItemStack,GameObject,DropItemValues)` (`:302`), `SpawnItemToPosition` (`:357`), `SetCollectibleItemValues` (`:365`) | Three `DropItem` overloads; target the `ItemStack` one for all of them (the others forward). |
| World pickup | `CollectibleItem.PerformPickup()` (`:333`), `GetItem()` (`:705`) | Return value 0 = taken. |
| Money | `Money.AddMoney(float, Currency)` (`Money.cs:447`), `Money.RemoveMoney(float, Currency)` (`:498`), `Money.Buy(int, Currency, bool)` (`:369`) | `AddMoney(float,bool)` forwards to the Currency overload. |
| Storage | `Storage.AddItem(Item,int,int,object[],int,bool)` (`Storage.cs:1227`), `Storage.RemoveItem(int,out int,int,bool,bool,int)` (`:1306`), `Storage.SpawnItems()` (`:912`) | Loot respawn is `SpawnItems`. |
| Crafting result | `CraftingBase.CreateItems(...)` (P/virtual, `:544`; overridden in `manufacturingProcess` `:501`), `manufacturing.Craft()` (`:348`), `manufacturing.OnCraftDone` (P, `:503`), `manufacturingProcess.StartProcess()` (`:159`) | Patch the base virtual and the override separately, Harmony patches by declared method. |
| Recipe gating | `RecipeController.AddRecipe(Recipe,bool,bool)` (`RecipeController.cs:81`), `HasRecipe`, `HasLearnedRecipe` | |
| Plants | `Growing.PlantExternal(...)` (`Growing.cs:425`), `GrowingSpot.AddPlant` (`:121`), `GrowItem.GetItem()` (`GrowItem.cs:234`) | |
| Meta cloning | `Item.CloneMeta(object[],bool,int,Item)` (static, `Item.cs:873`) vs instance `CloneMeta(bool,int,Item)` | Two overloads with the same name; the static one is the real worker. |
| Consumed/used | `Item.ItemAction.Invoke(ItemData,bool)` (virtual, `Item.cs:82`, overridden per action class), `Inventory.OnItemConsumed(...)` | Or subscribe to `Inventory.instance.ItemConsumed`. |
| Spoilage | `ItemPerishabledata.CountPerishingTime(float,bool,float)`, `Slot.CheckItemMetas` (P, `Slot.cs:480`) | |
| Save of stacks | `ItemStack` fields are saved as-is; `Inventory.OnSavingGame` / `OnLoadingGame` (`Inventory.cs:124,139`) | |

Example target with overloads spelled out:

```csharp
[HarmonyPatch(typeof(ItemOperations), nameof(ItemOperations.AddItems),
  new[] { typeof(Item), typeof(int), typeof(int), typeof(object[]), typeof(SlotController), typeof(bool), typeof(bool) })]
```

## 9. Gotchas

- **ItemData is UI.** `ItemData` is the draggable icon, not the item definition. The definition is `Item`; the instance state is `ItemStack`.
- **Empty sprite = invisible stack.** Any item whose `Appearance.GetSprite(meta)` is null renders as an empty slot while still occupying it.
- **Stack size is not a constant.** `Item.Stackable` can differ from the file under Custom difficulty. Compare against the property, not `BaseStackable`.
- **Money is special-cased by ID 0 and 1** inside `AddItems`/`AddItemAndReturnRemaining`; do not reuse those IDs for anything.
- **`AddItems` can silently drop to the ground** and returns false when it does. Use `AddItemAndReturnRemaining` if you need to know how many fit.
- **Owner changes stacking.** Different `ownerId` (NPC id, or stolen state) prevents merging and can produce a Theft crime when taking/adding as the player. Pass `owner: -1` and `ignoreCrime: true` for mod-granted items.
- **Meta sharing.** `CloneMeta` shares unknown meta classes and several known ones when `fullCopy` is false. Mutating a meta you did not clone edits the database template. `Recipe.GetCraftingResult` itself assigns `item.Meta = item.CloneMeta()` on the shared `Item`.
- **Unknown item IDs degrade to the coin** in loaded slots (2.5); there is no "missing item" placeholder. A mod that removes items from saved games should migrate stacks first.
- **Mod `$type`s in saves.** Meta and action classes are serialized with their type name. A mod-defined meta class in a save will fail to deserialize when the mod is absent. Prefer storing mod state in existing meta types or a separate mod save file.
- **Item data is read straight from disk** (`Application.dataPath + "/StreamingAssets/..."`). Replacing `Items.json` on disk works but conflicts with other mods, so patching the in-memory list is the better route. Never write to `Recipes.json` via `RecipeDatabase.AddRecipe` & co.
- **Recipe lookups are by `ID`, linear, first match wins;** vanilla already has a duplicate (10421) and two `-1` ids.
- **Recipes snapshot at station `Start`** (section 4). Late additions do not appear in already-started stations.
- **Backpack is a fixed 3-bag system** tied to scene Storage objects; contents are not inside the item stack.
- **`ItemOperations.RemoveLiquid` is an unimplemented stub.**
- **`ItemDatabase.extendedDatabase`/`itemnamespace` is dead weight:** no loader fills it, the two-arg `ItemReference` constructor looks up with the wrong variable, and stacks save no namespace.
- **No item localization path found;** `Title`/`Description` are displayed as written.
- **`ItemReference.cached` is not refreshed.** After `ItemDatabase.Reload()` the cached `Item` objects in existing references and in `Recipe.Input/Output` point at the old objects; avoid `Reload()` at runtime.
- **Inventory spoilage only updates when slots refresh** (inventory open, item moved, etc.). For deterministic mod logic read `ItemPerishabledata.integrity` only after forcing `Inventory.instance.UpdateAllSlotsNow()` (`Inventory.cs:332`).
