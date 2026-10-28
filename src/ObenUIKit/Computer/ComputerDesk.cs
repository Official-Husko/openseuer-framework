// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.World;
using ObenLoader;
using ObenLoader.Runtime;
using ObenUIKit.Controls;
using ObenUIKit.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace ObenUIKit.Computer;

/// <summary>
/// Puts the registered apps on the desktops of the in-game computers: a column of tiles at the right edge of
/// the desktop picture, laid out when the player sits down at a computer. The tiles use the computer screen's
/// own font, so they look like they belong to it.
/// </summary>
internal static class ComputerDesk
{
    private const string TilePrefix = "Mod app ";
    private const float TileWidth = 84f;
    private const float TileHeight = 72f;
    private const float Margin = 12f;
    private const float ColumnPitch = 96f;
    private const float RowPitch = 84f;
    private const int Rows = 5;

    private static readonly List<RectTransform> Desks = new List<RectTransform>();

    public static void Init(ObenMod kit)
    {
        Computers.Used.Subscribe(kit, use =>
        {
            if (use.InUse)
                Decorate(Computers.DesktopOf(use.Computer));
        });
        kit.Track(Disposable.Create(() =>
        {
            foreach (var desk in Desks)
                Clear(desk, keep: null);
            Desks.Clear();
        }));
        // A computer already in use when the kit (re)loads.
        var current = Computers.Current;
        if (current != null)
            Decorate(Computers.DesktopOf(current));
    }

    /// <summary>Lays the tiles out again on every decorated desktop (an app came or went).</summary>
    public static void Refresh()
    {
        Desks.RemoveAll(d => d == null);
        foreach (var desk in Desks)
            Layout(desk);
        var current = Computers.Current;
        if (current != null)
            Decorate(Computers.DesktopOf(current));
    }

    private static void Decorate(RectTransform desk)
    {
        if (desk == null)
            return;
        if (!Desks.Contains(desk))
            Desks.Add(desk);
        Layout(desk);
    }

    private static void Layout(RectTransform desk)
    {
        var apps = ComputerApps.All;
        var keep = new HashSet<string>();
        for (int i = 0; i < apps.Count; i++)
        {
            keep.Add(TilePrefix + apps[i].Id);
            var tile = desk.Find(TilePrefix + apps[i].Id) as RectTransform;
            if (tile == null)
                tile = Tile(desk, apps[i]);
            tile.anchorMin = tile.anchorMax = tile.pivot = new Vector2(1f, 1f);
            tile.sizeDelta = new Vector2(TileWidth, TileHeight);
            tile.anchoredPosition = new Vector2(-Margin - (i / Rows) * ColumnPitch, -Margin - (i % Rows) * RowPitch);
            tile.SetAsLastSibling();
        }
        Clear(desk, keep);
    }

    // Removes tiles of apps that are gone (all of them without a keep set).
    private static void Clear(RectTransform desk, HashSet<string> keep)
    {
        if (desk == null)
            return;
        for (int i = desk.childCount - 1; i >= 0; i--)
        {
            var child = desk.GetChild(i);
            if (child.name.StartsWith(TilePrefix, StringComparison.Ordinal) && (keep == null || !keep.Contains(child.name)))
                Object.Destroy(child.gameObject);
        }
    }

    private static RectTransform Tile(RectTransform desk, ComputerApp app)
    {
        var theme = UiKit.Theme;
        var background = UiParts.Image(desk, TilePrefix + app.Id, null, new Color(0f, 0f, 0f, 0.35f), raycast: true);
        var rect = background.rectTransform;
        var button = background.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = background;
        var colors = button.colors;
        colors.highlightedColor = theme.RowHover;
        colors.pressedColor = theme.TextPress;
        button.colors = colors;
        string id = app.Id;
        button.onClick.AddListener(() => Guard(() => ComputerApps.Open(id)));

        var font = FontOf(desk);
        if (app.Icon != null)
        {
            var icon = UiParts.Image(rect, "Icon", app.Icon, Color.white);
            icon.preserveAspect = true;
            var iconRect = icon.rectTransform;
            iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 1f);
            iconRect.sizeDelta = new Vector2(40f, 40f);
            iconRect.anchoredPosition = new Vector2(0f, -4f);
            Label(rect, app.Title, font, 11f, 0f, 0.38f);
        }
        else
        {
            Label(rect, app.Title, font, 13f, 0f, 1f);
        }
        return rect;
    }

    private static void Label(RectTransform parent, string text, TMP_FontAsset font, float size, float bottom, float top)
    {
        var label = UiParts.Text(parent, UiKit.Theme, text, size, Color.white);
        if (font != null)
            label.font = font;
        label.enableAutoSizing = true;
        label.fontSizeMin = 6f;
        label.fontSizeMax = size;
        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, bottom);
        rect.anchorMax = new Vector2(1f, top);
        rect.offsetMin = new Vector2(2f, 0f);
        rect.offsetMax = new Vector2(-2f, 0f);
    }

    // The font the computer screen's own texts use, else the kit's.
    private static TMP_FontAsset FontOf(RectTransform desk)
    {
        var canvas = desk.GetComponentInParent<Canvas>();
        var text = canvas != null ? canvas.GetComponentInChildren<TextMeshProUGUI>(true) : null;
        return text != null ? text.font : null;
    }

    private static void Guard(Action action)
    {
        try { action(); }
        catch (Exception e) { KitRuntime.Current?.Kit.Log.Error("A computer app failed to open", e); }
    }
}

/// <summary>
/// While an app window is open over a computer, the computer must not take the Esc or Interact that closes the
/// window, or the player would also get up. That includes the frame the window closed on.
/// </summary>
[HarmonyPatch(typeof(InteractableComputer), nameof(InteractableComputer.Controls))]
internal static class ComputerControlsGate
{
    [HarmonyPrefix]
    private static bool Before() => !UiKit.AnyWindowOpen && Time.frameCount > ComputerApps.ClosedFrame + 1;
}
