using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using ObenFramework.World;
using ObenFramework.World.Addressing;
using UnityEngine;

namespace DevBridge.Commands;

/// <summary>Doors, the interactable under the crosshair and placed furniture, through ObenFramework.World.</summary>
internal static class WorldCommands
{
    [BridgeCommand("doors.list", "Doors of the loaded scenes with their state, nearest to the player first.",
        Params = new[] { "radius?: only doors within this many metres of the player", "max?: default 50", "inactive?: include inactive doors, default false" })]
    private static object DoorsList(JObject p)
    {
        float radius = p.Float("radius", 0f);
        int max = Math.Max(1, p.Int("max", 50));
        var origin = PlayerRef.Position;

        var entries = new List<(float distance, Door door)>();
        foreach (var door in Doors.All(p.Bool("inactive", false)))
        {
            float distance = origin.HasValue ? Vector3.Distance(origin.Value, door.transform.position) : 0f;
            if (radius > 0f && origin.HasValue && distance > radius)
                continue;
            entries.Add((distance, door));
        }
        entries.Sort((a, b) => a.distance.CompareTo(b.distance));

        var list = new JArray();
        for (int i = 0; i < entries.Count && i < max; i++)
            list.Add(DescribeDoor(entries[i].door, origin.HasValue ? entries[i].distance : (float?)null));
        return new JObject { ["total"] = entries.Count, ["doors"] = list };
    }

    [BridgeCommand("doors.set", "Open, close, lock or unlock a door by id (see doors.list). Opening goes past the lock.",
        Params = new[] { "id: door id, address or path", "open?: true opens, false closes", "locked?: true locks, false unlocks",
            "swing?: default or opposite", "respectLock?: leave a locked door shut when opening, default false" })]
    private static object DoorsSet(JObject p)
    {
        var door = Doors.Find(p.Str("id")) ?? throw new ArgumentException($"No door '{p.Str("id")}'.");
        if (p["open"] == null && p["locked"] == null)
            throw new ArgumentException("Give 'open' and/or 'locked'.");

        // A door that is to be shut and locked has to be shut first; opening goes past the lock anyway.
        if (p["locked"] != null && p.Bool("locked") && p["open"] != null && !p.Bool("open"))
            Doors.Close(door);
        if (p["locked"] != null)
            Doors.SetLocked(door, p.Bool("locked"));
        if (p["open"] != null)
        {
            if (p.Bool("open"))
            {
                var swing = string.Equals(p.Str("swing", "default"), "opposite", StringComparison.OrdinalIgnoreCase) ? DoorSwing.Opposite : DoorSwing.Default;
                Doors.Open(door, swing, p.Bool("respectLock", false));
            }
            else
            {
                Doors.Close(door);
            }
        }
        return DescribeDoor(door, null);
    }

    [BridgeCommand("interact.target", "The interactable under the crosshair: id, address, title, type and, for doors, lights and switches, state.")]
    private static object InteractTarget(JObject p)
    {
        var info = Interactables.TargetInfo;
        if (info == null)
            return new JObject { ["target"] = null };

        var target = info.Value;
        var result = new JObject
        {
            ["target"] = target.Title,
            ["type"] = target.TypeName,
            ["disabled"] = target.Disabled,
            ["address"] = target.Address.ToString(),
            ["scene"] = target.Address.Scene,
            ["guid"] = target.Address.Guid,
            ["path"] = target.Address.Path,
            ["pos"] = Vec(target.Component.transform.position),
        };

        switch (target.Component)
        {
            case Door door:
                result["id"] = Doors.IdOf(door);
                result["state"] = Doors.StateOf(door).ToString();
                break;
            case Button button:
                result["id"] = Switches.IdOf(button);
                result["pressed"] = Switches.IsPressed(button);
                result["locked"] = Switches.IsLocked(button);
                break;
            default:
                result["id"] = target.Address.Guid ?? target.Address.ToString();
                break;
        }
        return result;
    }

    [BridgeCommand("furniture.list", "Placed furniture with its stable id, kind, zone and position.",
        Params = new[] { "type?: only kinds whose id or title contains this text", "manager?: only this zone (manager GUID)",
            "preplaced?: include furniture that came with the scene, default false", "max?: default 100" })]
    private static object FurnitureList(JObject p)
    {
        string type = p.Str("type", null);
        string manager = p.Str("manager", null);
        int max = Math.Max(1, p.Int("max", 100));

        var list = new JArray();
        int matched = 0;
        foreach (var info in PlacedFurniture.List(p.Bool("preplaced", false)))
        {
            if (manager != null && info.Manager != manager)
                continue;
            if (type != null && info.TypeId.IndexOf(type, StringComparison.OrdinalIgnoreCase) < 0 && info.Title.IndexOf(type, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            matched++;
            if (list.Count < max)
                list.Add(DescribeFurniture(info));
        }
        return new JObject { ["total"] = matched, ["furniture"] = list };
    }

    private static JObject DescribeDoor(Door door, float? distance)
    {
        var state = Doors.StateOf(door);
        var result = new JObject
        {
            ["id"] = Doors.IdOf(door),
            ["name"] = door.name,
            ["path"] = ObjectResolver.PathOf(door.transform),
            ["phase"] = state.Phase.ToString(),
            ["open"] = state.IsOpen,
            ["locked"] = state.Locked,
            ["position"] = state.Position,
            ["swing"] = state.Swing.ToString(),
            ["pos"] = Vec(door.transform.position),
        };
        if (distance.HasValue)
            result["distance"] = Math.Round(distance.Value, 2);
        return result;
    }

    private static JObject DescribeFurniture(FurnitureInfo info) => new JObject
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
        ["preplaced"] = info.Preplaced,
        ["parent"] = info.ParentId,
        ["attachedTo"] = info.AttachedToId,
    };

    private static JArray Vec(Vector3 v) =>
        new JArray(Math.Round(v.x, 2), Math.Round(v.y, 2), Math.Round(v.z, 2));
}
