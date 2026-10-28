using System;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Player;
using ObenFramework.World;

namespace DevBridge.Commands;

/// <summary>Map landmarks, editable signs, saved world objects and visited scenes through ObenFramework.World.</summary>
internal static class LandmarkCommands
{
    [BridgeCommand("landmarks.list", "Landmarks loaded in the game (the game's and mods'), whether the player has them, and the mod landmarks saved in this game.",
        Params = new[] { "owned?: only the ones the player has" })]
    private static object List(JObject p)
    {
        bool ownedOnly = p.Bool("owned", false);
        var list = new JArray();
        foreach (var landmark in Landmarks.All())
        {
            if (!ownedOnly || landmark.Owned)
                list.Add(new JObject { ["id"] = landmark.Id, ["title"] = landmark.Title, ["owned"] = landmark.Owned, ["custom"] = landmark.Custom });
        }
        var saved = new JArray();
        foreach (var entry in Landmarks.Saved())
        {
            saved.Add(new JObject
            {
                ["mod"] = entry.Mod, ["id"] = entry.Id, ["title"] = entry.Title, ["map"] = entry.Map,
                ["position"] = new JArray(entry.X, entry.Y, entry.Z), ["icon"] = entry.Icon,
            });
        }
        return new JObject { ["landmarks"] = list, ["saved"] = saved };
    }

    [BridgeCommand("landmarks.unlock", "Hand out one of the game's landmarks, as discovering it does.",
        Params = new[] { "id: landmark id from landmarks.list" })]
    private static object Unlock(JObject p) => new JObject { ["unlocked"] = Landmarks.Unlock(p.Str("id")) };

    [BridgeCommand("landmarks.mark", "Put a red dot on the current map (owned by DevBridge), or remove one with remove=true.",
        Params = new[] { "id", "title?: default the id", "pos?: [x,y,z], default the player's position", "remove?: take the mark away" })]
    private static object Mark(JObject p)
    {
        string id = p.Str("id");
        if (p.Bool("remove", false))
            return new JObject { ["removed"] = Landmarks.Remove(DevBridgeMod.Instance, id) };
        var position = p.Position() ?? PlayerRef.Position ?? throw new InvalidOperationException("No player; give 'pos'.");
        return new JObject { ["marked"] = Landmarks.Mark(DevBridgeMod.Instance, id, p.Str("title", id), position) };
    }

    [BridgeCommand("signs.list", "Editable signs of the active scene with their text and limits.")]
    private static object Signs(JObject p)
    {
        var list = new JArray();
        foreach (var sign in ObenFramework.World.Signs.All())
            list.Add(new JObject { ["id"] = sign.Id, ["text"] = sign.Text, ["maxLines"] = sign.MaxLines, ["maxLength"] = sign.MaxLength });
        return list;
    }

    [BridgeCommand("signs.set", "Set the text of an editable sign.", Params = new[] { "id: from signs.list", "text" })]
    private static object SetSign(JObject p) => new JObject { ["set"] = ObenFramework.World.Signs.SetText(p.Str("id"), p.Str("text")) };

    [BridgeCommand("objects.near", "Saved world objects (with a GUID or from a saved prefab) near the player or a point.",
        Params = new[] { "radius?: metres, default 5", "pos?: [x,y,z], default the player's position", "max?: default 50" })]
    private static object Near(JObject p)
    {
        var position = p.Position() ?? PlayerRef.Position ?? throw new InvalidOperationException("No player; give 'pos'.");
        int max = Math.Max(1, p.Int("max", 50));
        var list = new JArray();
        foreach (var info in WorldObjects.Near(position, p.Float("radius", 5f)))
        {
            if (list.Count >= max)
                break;
            list.Add(new JObject
            {
                ["guid"] = info.Guid, ["name"] = info.Name, ["kind"] = info.Kind.ToString(), ["scene"] = info.Scene,
                ["distance"] = UnityEngine.Vector3.Distance(position, info.Position),
                ["path"] = WorldObjects.PathOf(info.Object),
            });
        }
        return list;
    }

    [BridgeCommand("world.visited", "The scenes the player has been in this game, and how many scenes the game has.")]
    private static object Visited(JObject p) => new JObject
    {
        ["current"] = WorldLocation.CurrentScene,
        ["visited"] = new JArray(WorldLocation.VisitedScenes()),
        ["known"] = WorldLocation.KnownScenes().Count,
    };
}
