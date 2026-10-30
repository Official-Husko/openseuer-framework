// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Building;

/// <summary>
/// New pieces of furniture from mods. Each is a copy of one of the game's pieces with its own id
/// (<c>oben:modId/key</c>), texts, prices, icon, build menu group and optionally its own OBJ model, and it can
/// be sold in furniture shops, found as flatpack loot, and crafted from a blueprint. Placed pieces go through
/// the game's own save and come back by id; when their mod is gone they load as a stand-in that looks like
/// the template and keeps the id, and without the framework the game loads them as the template itself.
/// </summary>
public static partial class CustomFurniture
{
    /// <summary>
    /// The framework's custom furniture hooks are in place. False when they failed to apply (a game update
    /// changed what they patch); defined pieces may then not save, sell, drop as loot or craft as they should.
    /// </summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Building");

    private static readonly string[] ReservedCategories = { "All", "Items", "None", "Wallpaper", "Surfaces" };

    internal static ITracker Owner { get; private set; }

    /// <summary>
    /// A placed custom piece is in the world after a level loaded (raised once per piece when the world is ready,
    /// stand-ins included). Use it to restore per-piece state a mod keeps itself.
    /// </summary>
    public static readonly World.WorldEvent<FurniturePlaceable> Loaded = new World.WorldEvent<FurniturePlaceable>(nameof(Loaded));

    /// <summary>The ids of every defined piece.</summary>
    public static List<string> Ids
    {
        get
        {
            var ids = new List<string>();
            foreach (var entry in FurnitureRegistry.All)
                ids.Add(entry.Id);
            return ids;
        }
    }

    /// <summary>Ids that loaded as stand-ins this session because their mod isn't installed (or didn't build).</summary>
    public static List<string> Missing => new List<string>(FurniturePlaceholders.Ids);

    /// <summary>The id a mod's piece gets for a key.</summary>
    public static string IdFor(ObenMod owner, string key) => FurnitureIds.For(owner.Info.Id, key);

    /// <summary>
    /// Defines a piece for as long as <paramref name="owner"/> is loaded and returns its id. The piece is built
    /// on first use (the build menu, a shop, a save that has one). Throws when the definition is invalid or the
    /// id is taken. Placed copies stay intact when the mod unloads; they load as stand-ins until it is back.
    /// </summary>
    public static string Define(ObenMod owner, string key, FurnitureDefinition definition)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        string id = FurnitureIds.For(owner.Info.Id, key);
        if (FurnitureRegistry.Find(id) != null)
            throw new ArgumentException($"Furniture {id} is already defined.", nameof(key));

        var snapshot = Snapshot(definition);
        // Checked now when the game's furniture list is there; otherwise at the first build.
        if (World.FurnitureKinds.Ready)
            FurnitureBuild.Template(snapshot.Template);
        FurnitureRegistry.Add(owner, new FurnitureEntry(id, owner.Info.Id, snapshot));
        FurnitureMenus.Changed();
        return id;
    }

    /// <summary>The game furniture of a custom piece (built now if needed), or null when it isn't defined or failed to build.</summary>
    public static global::Furniture Get(string id) => FurnitureRegistry.Resolve(id);

    /// <summary>True for a mod's piece, defined or a stand-in.</summary>
    public static bool IsCustom(global::Furniture furniture) => furniture != null && FurnitureIds.IsModId(furniture.id);

    /// <summary>True for a stand-in of a piece whose mod is missing.</summary>
    public static bool IsStandIn(global::Furniture furniture) => FurniturePlaceholders.IsPlaceholder(furniture);

    /// <summary>Adds pieces to the player's building inventory. False when it isn't defined or the game isn't ready.</summary>
    public static bool Give(string id, int amount = 1)
    {
        if (amount < 1 || amount > 1000)
            throw new ArgumentOutOfRangeException(nameof(amount), "The amount must be 1 to 1000.");
        var furniture = Get(id);
        if (furniture == null || BuildingSystem.instance == null || SaveGate.IsBusy)
            return false;
        return BuildingSystem.instance.AddFurniture(furniture, amount);
    }

    /// <summary>Puts flatpacks of the piece in the player's item inventory. False when there is no room.</summary>
    public static bool GiveFlatpack(string id, int amount = 1)
    {
        var furniture = Get(id);
        return furniture != null && !SaveGate.IsBusy && FurnitureFlatpacks.Give(furniture, amount);
    }

    /// <summary>
    /// Lets containers, ground spawns and packages of a loot category hold the piece as a flatpack, with a weight
    /// of 1 to 100 against the other mod pieces of the category. Lasts until <paramref name="owner"/> unloads.
    /// </summary>
    public static IDisposable AddFlatpackLoot(ITracker owner, string id, string category, int weight = 1)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        string problem = FurnitureIds.CheckLoot(category, weight);
        if (problem != null)
            throw new ArgumentException(problem);
        if (FurnitureRegistry.Find(id) == null)
            throw new ArgumentException($"Furniture {id} isn't defined.", nameof(id));
        return FurnitureLoot.Add(owner, id, category, weight);
    }

    internal static void Init(ObenMod framework)
    {
        Owner = framework;
        FurnitureShops.Init(framework);
        Events.GameEvents.WorldReady.Subscribe(framework, _ => OnWorldReady());
        Events.GameEvents.LevelChanging.Subscribe(framework, BuildingAreas.Reset);
        Events.GameEvents.MainMenu.Subscribe(framework, BuildingAreas.Reset);
        BuildingAreas.Seed();
        framework.Track(Disposable.Create(() =>
        {
            FurnitureLoot.Reset();
            FurniturePools.Reset();
            FurnitureRegistry.Reset();
            FurniturePlaceholders.Reset();
            FurnitureCategories.Reset();
            FurnitureShops.Reset();
            FurnitureMenus.Reset();
            BuildingAreas.Reset();
            Owner = null;
        }));
    }

    private static void OnWorldReady()
    {
        FurnitureRegistry.ForgetFailures();
        BuildingAreas.Seed();
        if (FurnitureRegistry.All.Count > 0)
            FurniturePools.Changed();
        if (!Loaded.HasSubscribers)
            return;
        foreach (var placeable in World.PlacedFurniture.All())
        {
            if (placeable.furniture != null && FurnitureIds.IsModId(placeable.furniture.id))
                Loaded.Raise(placeable);
        }
    }

    private static FurnitureDefinition Snapshot(FurnitureDefinition d)
    {
        if (string.IsNullOrWhiteSpace(d.Template))
            throw new ArgumentException("Name the game piece to copy in Template.");
        if (d.Title != null && (d.Title.Length == 0 || d.Title.Length > 200))
            throw new ArgumentException("A title is 1 to 200 characters.");
        if (d.Details != null && d.Details.Length > 2000)
            throw new ArgumentException("Details are at most 2000 characters.");
        if (d.PriceOC < -1 || d.PriceRM < -1)
            throw new ArgumentException("Prices are 0 or more, or -1 for the template's.");
        if (d.ShopStock < 1)
            throw new ArgumentException("ShopStock must be 1 or more.");
        if (!Enum.IsDefined(typeof(FurniturePlacement), d.Placement))
            throw new ArgumentException("Placement is not a defined value.");
        if (d.Category != null && d.Category.Trim().Length > 40)
            throw new ArgumentException("A build menu group's name is at most 40 characters.");
        if (d.Category != null && Array.Exists(ReservedCategories, r => string.Equals(r, d.Category.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException($"'{d.Category}' is a build menu group the game keeps for itself; use another name.");
        if (d.Model != null && !d.Model.IsAlive)
            throw new ArgumentException("The model has been unloaded.");
        if (d.Colliders != null)
        {
            if (d.Model == null)
                throw new ArgumentException("Colliders are fitted to a model; set Model too.");
            if (d.Colliders.Length == 0 || d.Colliders.Length > 64)
                throw new ArgumentException("Colliders holds 1 to 64 boxes, or is null.");
            foreach (var box in d.Colliders)
            {
                if (!(box.size.x > 0f && box.size.y > 0f && box.size.z > 0f) || !Finite(box.center) || !Finite(box.size))
                    throw new ArgumentException("Each collider box needs a finite centre and a size above 0 on every axis.");
            }
        }
        foreach (string shop in d.Shops ?? new List<string>())
        {
            if (string.IsNullOrEmpty(shop))
                throw new ArgumentException("Shops has an empty entry.");
        }

        return new FurnitureDefinition
        {
            Template = d.Template.Trim(), Title = d.Title, Details = d.Details, PriceOC = d.PriceOC, PriceRM = d.PriceRM, Icon = d.Icon,
            Model = d.Model, Colliders = d.Colliders != null ? (Bounds[])d.Colliders.Clone() : null, Placement = d.Placement,
            Category = d.Category, Shops = new List<string>(d.Shops ?? new List<string>()), ShopStock = d.ShopStock,
        };
    }

    private static bool Finite(Vector3 v) => !float.IsNaN(v.x + v.y + v.z) && !float.IsInfinity(v.x + v.y + v.z);
}
