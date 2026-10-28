using System;
using System.Collections.Generic;
using DevBridge.Server;
using Newtonsoft.Json.Linq;
using ObenFramework.Game;
using ObenFramework.Saves;
using ObenFramework.Scenes;

namespace DevBridge.Commands;

/// <summary>Read-only views of the save system: object state as the game would save it, slots, and the busy gate.</summary>
internal static class SaveStateCommands
{
    private const int DefaultPreview = 1500;

    [BridgeCommand("save.capture", "An object's saved state as the game would write it (read-only, nothing is applied or written).",
        Params = new[] { "target: path or #id", "component?: only this savable type", "max?: preview characters, default 1500 (0 = all)", "tree?: bool, every savable below the target keyed by GUID" })]
    private static object Capture(JObject p)
    {
        var go = SceneQuery.Resolve(p.Str("target"));
        int max = p.Int("max", DefaultPreview);
        if (p.Bool("tree", false))
            return Tree(go, max);

        var wanted = p.Str("component", null);
        var result = new JArray();
        foreach (var script in go.GetComponents<SavableScript>())
        {
            if (wanted != null && !string.Equals(script.GetType().Name, wanted, StringComparison.OrdinalIgnoreCase))
                continue;
            result.Add(Entry(script.GetType().Name, SaveController.GetObjectGUID(script), SaveState.Capture(script), max));
        }
        if (result.Count == 0)
            throw new InvalidOperationException($"{go.name} has no savable component" + (wanted != null ? $" named {wanted}" : "") + ".");
        return result;
    }

    [BridgeCommand("save.slots", "Characters and save slots on disk, the loaded save's folder, and whether a save can start now.")]
    private static object Slots(JObject p)
    {
        var characters = new JArray();
        foreach (var character in SaveSlots.Characters())
        {
            var slots = new JArray();
            foreach (var slot in SaveSlots.Slots(character))
            {
                slots.Add(new JObject
                {
                    ["slot"] = slot.Slot,
                    ["level"] = slot.Level,
                    ["saved"] = slot.Saved?.ToString("s"),
                    ["gameVersion"] = slot.GameVersion,
                    ["corrupted"] = slot.Corrupted,
                });
            }
            characters.Add(new JObject { ["name"] = character.Name, ["folder"] = character.Folder, ["slots"] = slots });
        }
        return new JObject
        {
            ["savesFolder"] = SaveSlots.SavesFolder,
            ["currentFolder"] = SaveSlots.CurrentFolder,
            ["currentSlot"] = SaveSlots.CurrentSlot,
            ["canSaveNow"] = SaveSlots.CanSaveNow,
            ["characters"] = characters,
        };
    }

    [BridgeCommand("save.busy", "Whether a save or load is in flight, next to the game's own Loading flag for comparison.")]
    private static object Busy(JObject p) => new JObject
    {
        ["busy"] = SaveGate.IsBusy,
        ["saving"] = SaveGate.IsSaving,
        ["loading"] = SaveGate.IsLoading,
        ["gameLoadingFlag"] = SaveController.Loading,
        ["worldReady"] = WorldState.IsReady,
        ["loadingSave"] = WorldState.IsLoadingSave,
    };

    private static JObject Tree(UnityEngine.GameObject root, int max)
    {
        var blobs = SaveState.CaptureTree(root);
        var entries = new JObject();
        foreach (var pair in blobs)
            entries[pair.Key] = Entry(null, pair.Key, pair.Value, max);
        return new JObject { ["count"] = blobs.Count, ["entries"] = entries };
    }

    private static JObject Entry(string type, string guid, string blob, int max)
    {
        var entry = new JObject();
        if (type != null)
            entry["component"] = type;
        entry["guid"] = guid;
        entry["captured"] = blob != null;
        if (blob == null)
            return entry;
        bool cut = max > 0 && blob.Length > max;
        entry["length"] = blob.Length;
        entry["truncated"] = cut;
        entry["blob"] = cut ? blob.Substring(0, max) : blob;
        return entry;
    }
}
