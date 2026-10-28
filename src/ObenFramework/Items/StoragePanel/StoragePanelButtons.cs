// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

namespace ObenFramework.Items;

/// <summary>A mod button in the container window was pressed, handed to the button's action.</summary>
public readonly struct StoragePanelPress
{
    public StoragePanelPress(ContainerInfo container, int freeSlot)
    {
        Container = container;
        FreeSlot = freeSlot;
    }

    /// <summary>The open container.</summary>
    public ContainerInfo Container { get; }

    /// <summary>The first empty slot of the container (there always is one: a full container doesn't call the action).</summary>
    public int FreeSlot { get; }
}

/// <summary>
/// Adds buttons next to "Take all" in the window of an open container, for actions such as sorting or
/// crafting into it. Buttons are copies of the game's own, hidden while trading and for containers that belong
/// to someone, and removed when their mod unloads. A press on a full container shows the game's "storage
/// full" feedback instead of calling the action.
/// </summary>
public static class StoragePanelButtons
{
    private static readonly AccessTools.FieldRef<DefaultUIButton, UnityEngine.UI.Image> Glyph =
        AccessTools.FieldRefAccess<DefaultUIButton, UnityEngine.UI.Image>("controllerImage");

    private static readonly List<Entry> Entries = new List<Entry>();
    private static Storage _shown;
    private static StoragePanelUI _panel;

    /// <summary>Adds a button until <paramref name="owner"/> unloads or the returned object is disposed.</summary>
    public static IDisposable Add(ITracker owner, string label, Action<StoragePanelPress> pressed)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(label))
            throw new ArgumentException("A button needs a label.", nameof(label));
        if (pressed == null)
            throw new ArgumentNullException(nameof(pressed));

        var entry = new Entry { Label = label, Pressed = pressed };
        Entries.Add(entry);
        if (_panel != null && _shown != null)
            Shown(_panel, _shown);
        var registration = Disposable.Create(() =>
        {
            Entries.Remove(entry);
            if (entry.Clone != null)
                Object.Destroy(entry.Clone);
        });
        owner.Track(registration);
        return registration;
    }

    /// <summary>The first empty slot of a container, or -1 when it is full, locked or not the player's to fill.</summary>
    public static int FreeSlot(Storage storage)
    {
        if (storage == null || storage.Slots == null || storage.storageSlots == null)
            return -1;
        var info = Containers.Describe(storage);
        if (info.NpcOwner > 0 || info.GameLocked)
            return -1;
        int count = Math.Min(storage.Slots.Length, storage.storageSlots.Length);
        for (int i = 0; i < count; i++)
        {
            var stack = storage.storageSlots[i];
            if (storage.Slots[i] != null && (stack == null || stack.itemId == -1 || stack.itemAmount <= 0))
                return i;
        }
        return -1;
    }

    internal static void Shown(StoragePanelUI panel, Storage storage)
    {
        _panel = panel;
        _shown = storage;
        if (Entries.Count == 0 || panel.takeAllButton == null || panel.takeAllButton.transform.parent == null)
            return;

        bool hidden = (TradePanel.instance != null && TradePanel.instance.activeTrade != null) || Containers.Describe(storage).NpcOwner > 0;
        foreach (var entry in Entries)
        {
            if (entry.Clone == null)
                entry.Clone = MakeButton(panel.takeAllButton, entry);
            if (entry.Clone == null)
                continue;
            var text = entry.Clone.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.text = entry.Label;
            entry.Clone.SetActive(!hidden);
        }
    }

    private static GameObject MakeButton(GameObject source, Entry entry)
    {
        var clone = Object.Instantiate(source, source.transform.parent);
        clone.name = "Mod button: " + entry.Label;
        var button = clone.GetComponent<UnityEngine.UI.Button>();
        if (button == null)
        {
            Object.Destroy(clone);
            ItemsLog.WarnOnce("storage-panel-button", "The container window's Take all button has no Button component; mod buttons can't be added.");
            return null;
        }
        for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
            button.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => Press(entry));

        // The copy would otherwise show the take-all controller glyph and answer its key.
        var defaultButton = clone.GetComponent<DefaultUIButton>();
        if (defaultButton != null)
        {
            var glyph = Glyph(defaultButton);
            if (glyph != null && glyph.gameObject != clone && glyph.transform.IsChildOf(clone.transform))
                Object.Destroy(glyph.gameObject);
            defaultButton.ChangeControllerKey(KeyCode.None);
        }
        return clone;
    }

    private static void Press(Entry entry)
    {
        try
        {
            if (_shown == null || _panel == null || (TradePanel.instance != null && TradePanel.instance.activeTrade != null))
                return;
            int slot = FreeSlot(_shown);
            if (slot < 0)
            {
                _panel.StorageFull();
                return;
            }
            entry.Pressed(new StoragePanelPress(Containers.Describe(_shown), slot));
        }
        catch (Exception e)
        {
            ItemsLog.Failed($"The container window button '{entry.Label}'", e);
        }
    }

    private sealed class Entry
    {
        public string Label;
        public Action<StoragePanelPress> Pressed;
        public GameObject Clone;
    }
}

[HarmonyPatch]
internal static class StoragePanelButtonsPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(typeof(StoragePanelUI), nameof(StoragePanelUI.UpdateStoragePanelUI))]
    private static void Updated(StoragePanelUI __instance, Storage storage)
    {
        try { StoragePanelButtons.Shown(__instance, storage); }
        catch (Exception e) { ItemsLog.Failed("Container window buttons hook", e); }
    }
}
