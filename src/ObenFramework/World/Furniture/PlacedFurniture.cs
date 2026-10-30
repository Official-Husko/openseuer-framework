using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ObenFramework.World;

/// <summary>
/// Furniture the player placed (and, on request, what came with the scene): listing it, giving each piece an
/// id that survives saving and loading, and events for placing, removing and moving it.
/// </summary>
/// <remarks>
/// The game numbers placed furniture afresh at every save (the manager's GUID plus a counter, in reverse
/// list order), so removing one piece renames others. The ids here are our own: random, kept with the
/// save through <see cref="FurnitureLedger"/> and matched back after a load by kind, zone and position.
/// A piece that is moved keeps its id. Loose items lying on surfaces are not furniture and not covered.
/// </remarks>
public static class PlacedFurniture
{
    private const string PreplacedPrefix = "pre:";

    /// <summary>
    /// A piece was placed: by the build menu, or by the game putting furniture into a zone (a tenant moving
    /// in, a scripted scene). A piece that the build menu's move command put back is a
    /// <see cref="FurnitureMoved"/> instead.
    /// </summary>
    public static readonly WorldEvent<FurnitureChange> FurniturePlaced = new WorldEvent<FurnitureChange>(nameof(FurniturePlaced));

    /// <summary>
    /// A piece was taken into the inventory. The payload describes it as it stood; its object is about to be
    /// destroyed. Pieces standing on or attached to it are removed first, each with its own event.
    /// </summary>
    public static readonly WorldEvent<FurnitureChange> FurnitureRemoved = new WorldEvent<FurnitureChange>(nameof(FurnitureRemoved));

    /// <summary>
    /// A piece was picked up with the build menu's move command and put down again. The game does this as a
    /// take followed by a place; the take is held back until the next place shows what became of it, and is
    /// reported as a removal if the player closes the menu instead.
    /// </summary>
    public static readonly WorldEvent<FurnitureMove> FurnitureMoved = new WorldEvent<FurnitureMove>(nameof(FurnitureMoved));

    private const string ReloadKey = "ObenFramework.PlacedFurniture.Ids";

    private static readonly Dictionary<FurniturePlaceable, string> Ids = new Dictionary<FurniturePlaceable, string>();
    private static readonly Dictionary<string, FurniturePlaceable> ById = new Dictionary<string, FurniturePlaceable>();

    // The ids handed out before the framework reloaded, by the piece's instance id: the pieces outlive the reload.
    private static Dictionary<int, string> _parked;

    internal static bool AnySubscribers =>
        FurniturePlaced.HasSubscribers || FurnitureRemoved.HasSubscribers || FurnitureMoved.HasSubscribers;

    /// <summary>All placed furniture in the loaded scenes, in no particular order.</summary>
    public static List<FurniturePlaceable> All(bool includePreplaced = false)
    {
        var result = new List<FurniturePlaceable>();
        var building = BuildingSystem.instance;
        if (building == null)
            return result;

        var seen = new HashSet<FurniturePlaceable>();
        foreach (var manager in building.furnitureManagers)
        {
            if (manager == null)
                continue;
            Collect(manager.placedFurniture, result, seen);
            if (includePreplaced)
            {
                Collect(manager.preplacedFurniturePrimary, result, seen);
                Collect(manager.preplacedFurnitureSecondary, result, seen);
            }
        }
        return result;
    }

    /// <summary>Snapshots of <see cref="All"/>.</summary>
    public static List<FurnitureInfo> List(bool includePreplaced = false)
    {
        var all = All(includePreplaced);
        var infos = new List<FurnitureInfo>(all.Count);
        foreach (var placeable in all)
            infos.Add(Describe(placeable));
        return infos;
    }

    /// <summary>The piece with this id, or null if it isn't standing in a loaded scene.</summary>
    public static FurniturePlaceable Find(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;
        if (ById.TryGetValue(id, out var known) && known != null && known.manager != null)
            return known;

        // Pieces nobody asked about yet have no id; hand them out (which also claims saved ones).
        foreach (var placeable in All(includePreplaced: true))
            IdOf(placeable);
        return ById.TryGetValue(id, out known) && known != null ? known : null;
    }

    /// <summary>
    /// The id of a piece, given out on first ask. It comes from the save's ledger when the same kind of
    /// furniture stood in the same zone and spot when the game was saved, else it is new.
    /// </summary>
    public static string IdOf(FurniturePlaceable placeable)
    {
        if (placeable == null)
            return null;

        if (Ids.TryGetValue(placeable, out var id))
        {
            ById[id] = placeable;
            return id;
        }

        if (_parked != null && _parked.TryGetValue(placeable.GetInstanceID(), out id) && !IsTaken(id))
            _parked.Remove(placeable.GetInstanceID());
        else if (placeable.isPreplacedFurniture && !string.IsNullOrEmpty(placeable.GUID))
            id = PreplacedPrefix + placeable.GUID;
        else
            id = FurnitureLedger.Match(SceneName(placeable), ManagerOf(placeable), TypeOf(placeable), placeable.transform.position, IsTaken)
                 ?? Guid.NewGuid().ToString("N").Substring(0, 12);
        Assign(placeable, id);
        return id;
    }

    /// <summary>
    /// Makes <paramref name="id"/> the piece's id. For a mod that has to agree on ids with another
    /// machine (the guest of a shared world adopts the host's ids). The id must not belong to another live piece.
    /// </summary>
    public static void Adopt(FurniturePlaceable placeable, string id)
    {
        if (placeable == null)
            throw new ArgumentNullException(nameof(placeable));
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("No id given.", nameof(id));
        if (IsTaken(id) && ById[id] != placeable)
            throw new ArgumentException($"The id {id} belongs to another piece of furniture.", nameof(id));

        if (Ids.TryGetValue(placeable, out var old))
            ById.Remove(old);
        Assign(placeable, id);
    }

    public static FurnitureInfo Describe(FurniturePlaceable placeable)
    {
        if (placeable == null)
            throw new ArgumentNullException(nameof(placeable));

        var transform = placeable.transform;
        var model = transform.childCount > 0 ? transform.GetChild(0).localRotation : Quaternion.identity;
        var meta = placeable.meta;
        return new FurnitureInfo(
            placeable, IdOf(placeable), TypeOf(placeable), placeable.furniture != null ? placeable.furniture.title : "",
            ManagerOf(placeable), SceneName(placeable), transform.position, transform.rotation, model,
            meta != null ? meta.currentSkin : 0, meta != null ? meta.currentAlternativePrefab : 0,
            placeable.isPreplacedFurniture,
            placeable.parent != null ? IdOf(placeable.parent) : null,
            placeable.attachedto != null ? IdOf(placeable.attachedto) : null);
    }

    internal static void Init(ObenMod framework)
    {
        FurnitureLedger.Init(framework);
        if (ReloadState.Take(ReloadKey, out Dictionary<int, string> parked))
            _parked = parked;
        GameEvents.Saving.Subscribe(framework, RecordLedger);
        GameEvents.Loading.Subscribe(framework, _ => ForgetIds());
        GameEvents.MainMenu.Subscribe(framework, ForgetIds);
        framework.Track(Disposable.Create(() =>
        {
            Park();
            FurnitureLedger.Shutdown();
            ForgetIds();
            ForgetScene();
        }));
    }

    /// <summary>
    /// The level is going away: drop a held take. The ids stay until the next level loads, because a level
    /// change saves (and records them in the ledger) after it is announced.
    /// </summary>
    internal static void ForgetScene() => FurnitureMoves.Reset();

    private static void ForgetIds()
    {
        Ids.Clear();
        ById.Clear();
        _parked = null;
    }

    // Placed since the last save, a piece is in no ledger; keep every id the next copy of the framework would lose.
    private static void Park()
    {
        var parked = new Dictionary<int, string>();
        foreach (var pair in Ids)
        {
            if (pair.Key != null)
                parked[pair.Key.GetInstanceID()] = pair.Value;
        }
        if (parked.Count > 0)
            ReloadState.Put(ReloadKey, parked);
    }

    internal static void Forget(FurniturePlaceable placeable)
    {
        if (Ids.TryGetValue(placeable, out var id))
            ById.Remove(id);
    }

    private static void Assign(FurniturePlaceable placeable, string id)
    {
        Ids[placeable] = id;
        ById[id] = placeable;
    }

    private static bool IsTaken(string id) => ById.TryGetValue(id, out var holder) && holder != null;

    private static void RecordLedger()
    {
        try
        {
            if (BuildingSystem.instance == null)
                return;
            FurnitureLedger.Record(SceneManager.GetActiveScene().name, List(includePreplaced: false));
        }
        catch (Exception e)
        {
            WorldLog.Failed("Recording furniture ids", e);
        }
    }

    private static void Collect(List<BuildingSystem.FurnitureInfo> source, List<FurniturePlaceable> into, HashSet<FurniturePlaceable> seen)
    {
        foreach (var entry in source)
        {
            if (entry == null || entry.gameObject == null)
                continue;
            var placeable = entry.gameObject.GetComponent<FurniturePlaceable>();
            if (placeable != null && seen.Add(placeable))
                into.Add(placeable);
        }
    }

    private static string TypeOf(FurniturePlaceable placeable) => placeable.furniture != null ? placeable.furniture.Id : "";

    private static string ManagerOf(FurniturePlaceable placeable) =>
        placeable.manager != null ? placeable.manager.GUID : placeable.managerGUID ?? "";

    private static string SceneName(FurniturePlaceable placeable) => placeable.gameObject.scene.name;
}
