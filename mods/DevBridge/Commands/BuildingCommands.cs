using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using ObenFramework.World;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Placing, moving and removing furniture, containers and floor items, through ObenFramework.World.</summary>
internal static class BuildingCommands
{
    [BridgeCommand("furniture.place", "Place a piece of furniture without the build menu or using one from the inventory.",
        Params = new[] { "kind: furniture id (asset path), short name or title, see furniture.kinds", "x, y, z or pos: [x,y,z] world position",
            "yaw?: degrees around the up axis, default 0", "zone?: zone GUID (furniture.zones), default the zone the player builds in",
            "skin?: 1 and up", "alternative?: 1 and up", "modelYaw?: local yaw of the part the rotate key turns", "id?: id to give the piece",
            "parent?: id of the piece it stands on", "attachedTo?: id of the piece it hangs on", "known?: refuse pieces the build menu doesn't offer, default false",
            "source?: mod (default), game or player" })]
    private static object FurniturePlace(JObject p)
    {
        var position = p.Position() ?? throw new ArgumentException("Give x, y and z or pos.");
        var options = new FurniturePlacement
        {
            Skin = p.Int("skin", 0),
            Alternative = p.Int("alternative", 0),
            Id = p.Str("id", null),
            ParentId = p.Str("parent", null),
            AttachedToId = p.Str("attachedTo", null),
            KnownOnly = p.Bool("known", false),
        };
        if (p["modelYaw"] != null)
            options.ModelRotation = Quaternion.Euler(0f, p.Float("modelYaw"), 0f);

        var edit = FurnitureBuilder.Place(p.Str("kind"), position, Quaternion.Euler(0f, p.Float("yaw", 0f), 0f), p.Str("zone", null), options, Source(p));
        return Result(edit);
    }

    [BridgeCommand("furniture.move", "Move a placed piece, keeping its id.",
        Params = new[] { "id: furniture id", "x, y, z or pos: new world position", "yaw?: new root yaw in degrees, default keeps it",
            "modelYaw?: new local yaw of the part the rotate key turns", "source?: mod (default), game or player" })]
    private static object FurnitureMove(JObject p)
    {
        var position = p.Position() ?? throw new ArgumentException("Give x, y and z or pos.");
        Quaternion? rotation = p["yaw"] != null ? Quaternion.Euler(0f, p.Float("yaw"), 0f) : (Quaternion?)null;
        Quaternion? model = p["modelYaw"] != null ? Quaternion.Euler(0f, p.Float("modelYaw"), 0f) : (Quaternion?)null;
        return Result(FurnitureBuilder.Move(p.Str("id"), position, rotation, model, Source(p)));
    }

    [BridgeCommand("furniture.remove", "Remove a placed piece. Nothing goes into the player's furniture inventory.",
        Params = new[] { "id: furniture id", "withPieces?: also remove what stands on it, default false",
            "force?: remove it although it holds items or liquid, default false", "source?: mod (default), game or player" })]
    private static object FurnitureRemove(JObject p) =>
        Result(FurnitureBuilder.Remove(p.Str("id"), Source(p), p.Bool("withPieces", false), p.Bool("force", false)));

    [BridgeCommand("furniture.kinds", "Furniture the game knows, to find the kind for furniture.place.",
        Params = new[] { "text?: only kinds whose id, name or title contains this", "max?: default 50", "known?: also says whether this kind is one the build menu offers" })]
    private static object FurnitureKindList(JObject p)
    {
        string text = p.Str("text", null);
        string asked = p.Str("known", null);
        int max = Math.Max(1, p.Int("max", 50));
        var found = text != null ? ObenFramework.World.FurnitureKinds.Search(text, max) : new List<Furniture>(ObenFramework.World.FurnitureKinds.All);

        var list = new JArray();
        foreach (var furniture in found)
        {
            if (furniture == null || list.Count >= max)
                continue;
            list.Add(new JObject
            {
                ["kind"] = furniture.Id,
                ["name"] = furniture.name,
                ["title"] = furniture.title,
                ["category"] = ObenFramework.Building.FurnitureCategories.NameOf(furniture.category),
                ["skins"] = furniture.skins.Length,
                ["alternatives"] = furniture.AlternativePrefabs.Length,
                ["custom"] = ObenFramework.Building.CustomFurniture.IsCustom(furniture),
            });
        }
        var result = new JObject { ["total"] = found.Count, ["kinds"] = list };
        if (asked != null)
            result["known"] = ObenFramework.World.FurnitureKinds.IsKnown(asked);
        return result;
    }

    [BridgeCommand("furniture.zones", "Building zones (apartments, workshops, shops) of the loaded scenes.",
        Params = new[] { "scene?: only this scene", "placed?: only zones with placed furniture, default false" })]
    private static object FurnitureZones(JObject p)
    {
        bool onlyUsed = p.Bool("placed", false);
        var list = new JArray();
        foreach (var zone in FurnitureBuilder.Zones(p.Str("scene", null)))
        {
            if (onlyUsed && zone.PlacedCount == 0)
                continue;
            list.Add(new JObject
            {
                ["zone"] = zone.Id,
                ["scene"] = zone.Scene,
                ["pos"] = Vec(zone.Position),
                ["placed"] = zone.PlacedCount,
                ["active"] = zone.IsActive,
            });
        }
        return new JObject { ["zones"] = list };
    }

    [BridgeCommand("containers.list", "Containers (storage) of the loaded scenes with id, title and position, nearest first.",
        Params = new[] { "radius?: only within this many metres of the player", "text?: only titles containing this", "empty?: true only empty, false only filled",
            "inactive?: include inactive ones, default false", "max?: default 50" })]
    private static object ContainersList(JObject p)
    {
        float radius = p.Float("radius", 0f);
        string text = p.Str("text", null);
        int max = Math.Max(1, p.Int("max", 50));
        var origin = PlayerRef.Position;

        var entries = new List<(float distance, ContainerInfo info)>();
        foreach (var info in Containers.List(p.Bool("inactive", false)))
        {
            float distance = origin.HasValue ? Vector3.Distance(origin.Value, info.Position) : 0f;
            if (radius > 0f && origin.HasValue && distance > radius)
                continue;
            if (text != null && info.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (p["empty"] != null && info.IsEmpty != p.Bool("empty"))
                continue;
            entries.Add((distance, info));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        for (int i = 0; i < entries.Count && i < max; i++)
        {
            var row = DescribeContainer(entries[i].info);
            if (origin.HasValue)
                row["distance"] = Math.Round(entries[i].distance, 2);
            list.Add(row);
        }
        return new JObject { ["total"] = entries.Count, ["containers"] = list, ["locks"] = JArray.FromObject(ContainerLocks.All()) };
    }

    [BridgeCommand("containers.get", "One container with the stacks in it.",
        Params = new[] { "id: container id from containers.list" })]
    private static object ContainersGet(JObject p)
    {
        var storage = Containers.Find(p.Str("id")) ?? throw new ArgumentException($"No container '{p.Str("id")}'.");
        var result = DescribeContainer(Containers.Describe(storage));
        var items = new JArray();
        foreach (var item in Containers.GetItems(storage))
        {
            items.Add(new JObject
            {
                ["slot"] = item.Slot,
                ["itemId"] = item.ItemId,
                ["title"] = item.Title,
                ["amount"] = item.Amount,
                ["owner"] = item.Owner,
            });
        }
        result["items"] = items;
        return result;
    }

    [BridgeCommand("floor.list", "Items lying in the world with their session ids, nearest to the player first.",
        Params = new[] { "radius?: only within this many metres of the player, default 10", "item?: only this item id", "text?: only titles containing this",
            "max?: default 50" })]
    private static object FloorList(JObject p)
    {
        float radius = p.Float("radius", 10f);
        string text = p.Str("text", null);
        int itemId = p.Int("item", -1);
        int max = Math.Max(1, p.Int("max", 50));
        var origin = PlayerRef.Position;

        var entries = new List<(float distance, FloorItemInfo info)>();
        foreach (var info in FloorItems.All())
        {
            float distance = origin.HasValue ? Vector3.Distance(origin.Value, info.Position) : 0f;
            if (radius > 0f && origin.HasValue && distance > radius)
                continue;
            if (itemId >= 0 && info.ItemId != itemId)
                continue;
            if (text != null && info.Title.IndexOf(text, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            entries.Add((distance, info));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        for (int i = 0; i < entries.Count && i < max; i++)
        {
            var info = entries[i].info;
            var row = new JObject
            {
                ["id"] = info.Id,
                ["itemId"] = info.ItemId,
                ["title"] = info.Title,
                ["amount"] = info.Amount,
                ["scene"] = info.Scene,
                ["pos"] = Vec(info.Position),
            };
            if (origin.HasValue)
                row["distance"] = Math.Round(entries[i].distance, 2);
            list.Add(row);
        }
        return new JObject { ["total"] = entries.Count, ["items"] = list };
    }

    private static ChangeSource Source(JObject p)
    {
        switch ((p.Str("source", "mod") ?? "mod").ToLowerInvariant())
        {
            case "mod": return ChangeSource.Mod;
            case "game": return ChangeSource.Game;
            case "player": return ChangeSource.LocalPlayer;
            default: throw new ArgumentException("source must be mod, game or player.");
        }
    }

    private static JObject Result(FurnitureEdit edit)
    {
        if (!edit.Success)
            throw new InvalidOperationException(edit.Error);

        var info = edit.Furniture;
        return new JObject
        {
            ["id"] = info.Id,
            ["type"] = info.TypeId,
            ["title"] = info.Title,
            ["manager"] = info.Manager,
            ["pos"] = Vec(info.Position),
            ["yaw"] = Math.Round(info.Rotation.eulerAngles.y, 1),
            ["modelYaw"] = Math.Round(info.ModelRotation.eulerAngles.y, 1),
            ["skin"] = info.Skin,
            ["alternative"] = info.Alternative,
            ["parent"] = info.ParentId,
            ["attachedTo"] = info.AttachedToId,
        };
    }

    private static JObject DescribeContainer(ContainerInfo info) => new JObject
    {
        ["id"] = info.Id,
        ["title"] = info.Title,
        ["scene"] = info.Scene,
        ["pos"] = Vec(info.Position),
        ["slots"] = info.SlotCount,
        ["empty"] = info.IsEmpty,
        ["gameLocked"] = info.GameLocked,
        ["npcOwner"] = info.NpcOwner,
        ["editOwner"] = info.EditOwner,
    };

    private static JArray Vec(Vector3 v) => new JArray(Math.Round(v.x, 2), Math.Round(v.y, 2), Math.Round(v.z, 2));
}
