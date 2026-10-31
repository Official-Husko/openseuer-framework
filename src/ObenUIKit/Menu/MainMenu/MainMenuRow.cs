// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Game;
using ObenLoader;
using ObenLoader.Logging;
using ObenLoader.Runtime;
using ObenUIKit.Theme;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.Menu;

/// <summary>
/// Puts the mod buttons on the main menu's sign: each is a copy of the game's Credits button (its look,
/// hover and click sounds), stripped of its click action, its relays and its translation component, placed
/// in rows under the game's grid at the grid's own column and row spacing, as many rows as fit on screen
/// (<see cref="MainMenuFit"/>). When there are more buttons than places, the last place is a "More…" button
/// listing the rest (<see cref="MainMenuMore"/>). Rebuilt whenever the list changes or the main menu loads;
/// all copies are removed when the kit unloads.
/// </summary>
internal static class MainMenuRow
{
    private const string Prefix = "Mod button: ";
    private const string MoreName = Prefix + "more";

    private static readonly List<GameObject> Copies = new List<GameObject>();
    private static ModLogger _log;
    private static MainMenuNavigationHelper _grid;

    /// <summary>The game's bottom row, left to right (???/MODS, Credits, Quit), for navigation.</summary>
    internal static readonly UnityEngine.UI.Button[] BottomRow = new UnityEngine.UI.Button[3];

    /// <summary>The buttons on the menu in place order (see <see cref="MainMenuSlots"/>), "More…" last when there.</summary>
    internal static readonly List<UnityEngine.UI.Button> Slots = new List<UnityEngine.UI.Button>();

    public static void Init(ObenMod kit)
    {
        _log = kit.Log;
        _grid = null;
        Copies.Clear();
        GameEvents.MainMenu.Subscribe(kit, () => kit.StartCoroutine(NextFrame()));
        kit.Track(Disposable.Create(Detach));
        if (WorldState.InMainMenu)
            Rebuild();
    }

    /// <summary>The grid the navigation patches look at, or null away from the main menu.</summary>
    internal static MainMenuNavigationHelper Grid => _grid;

    public static void Rebuild()
    {
        Detach();
        if (!WorldState.InMainMenu || MainMenuButtons.All.Count == 0)
        {
            MainMenuMore.Set(null);
            return;
        }
        try
        {
            Attach();
        }
        catch (Exception e)
        {
            Detach();
            MainMenuMore.Set(null);
            _log?.Error("Adding the mod buttons to the main menu failed", e);
        }
    }

    /// <summary>Shows a button's new label or state on its copy.</summary>
    public static void Refresh(MainMenuButton button)
    {
        if (button.Button != null)
            Show(button.Button, button.Label, button.Interactable);
        else
            MainMenuMore.Changed(button);
    }

    private static void Show(UnityEngine.UI.Button copy, string label, bool interactable)
    {
        foreach (var text in copy.GetComponentsInChildren<TMP_Text>(true))
            text.text = label;
        PauseMenuEntry.SetInteractable(copy.gameObject, interactable);
    }

    private static IEnumerator NextFrame()
    {
        // The menu sets its buttons up during its first frame.
        yield return null;
        Rebuild();
    }

    private static void Attach()
    {
        var grid = FindGrid();
        if (grid == null)
            return;
        var template = Native.MenuCredits(grid);
        var newGame = Native.MenuNewGame(grid);
        var bottomLeft = Native.MenuUnknown(grid);
        if (template == null || newGame == null || bottomLeft == null)
            return;
        _grid = grid;
        BottomRow[0] = bottomLeft;
        BottomRow[1] = template;
        BottomRow[2] = Native.MenuQuit(grid);

        // The grid's spacing, measured off the game's own buttons in the parent's space.
        var parent = template.transform.parent;
        Vector3 top = parent.InverseTransformPoint(newGame.transform.position);
        Vector3 bottom = parent.InverseTransformPoint(bottomLeft.transform.position);
        Vector3 middle = parent.InverseTransformPoint(template.transform.position);
        float rowStep = top.y - bottom.y;
        float columnStep = middle.x - bottom.x;
        if (Mathf.Abs(rowStep) < 1f)
            rowStep = ((RectTransform)template.transform).rect.height * 1.5f;

        var buttons = MainMenuButtons.All;
        int places = MainMenuFit.Rows((RectTransform)template.transform, parent, new Vector3(middle.x, bottom.y, middle.z), rowStep)
            * MainMenuSlots.Columns;
        int direct = buttons.Count <= places ? buttons.Count : places - 1;
        for (int i = 0; i <= direct && i < buttons.Count; i++)
        {
            var (row, column) = MainMenuSlots.Place(i);
            var place = parent.TransformPoint(new Vector3(bottom.x + column * columnStep, bottom.y - (row + 1) * rowStep, middle.z));
            if (i == direct)
            {
                var more = Copy(template, MoreName, MainMenuMore.Open);
                more.transform.position = place;
                Show(more, MainMenuMore.Label, true);
                break;
            }
            var entry = buttons[i];
            var copy = Copy(template, Prefix + entry.OwnerId + "/" + entry.Id, () => Press(entry));
            copy.transform.position = place;
            entry.Button = copy;
            Show(copy, entry.Label, entry.Interactable);
        }
        var rest = new List<MainMenuButton>();
        for (int i = direct; i < buttons.Count; i++)
            rest.Add(buttons[i]);
        MainMenuMore.Set(rest);
    }

    private static UnityEngine.UI.Button Copy(UnityEngine.UI.Button template, string name, Action onClick)
    {
        // Copied under an inactive holder, so none of the copy's parts wake up before Strip has taken the
        // Credits button's relays and translations out; it wakes in its real place.
        var holder = new GameObject("ObenUIKit copy");
        holder.SetActive(false);
        holder.transform.SetParent(template.transform.parent, false);
        var copy = UnityEngine.Object.Instantiate(template.gameObject, holder.transform, false);
        copy.name = name;
        Strip(copy);
        copy.transform.SetParent(template.transform.parent, false);
        UnityEngine.Object.Destroy(holder);
        // A layout group on the sign would otherwise move the copy into its own idea of a grid.
        if (copy.transform.parent.GetComponent<LayoutGroup>() != null)
        {
            var layout = copy.GetComponent<LayoutElement>();
            if (layout == null)
                layout = copy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
        }
        var button = copy.GetComponent<UnityEngine.UI.Button>();
        button.onClick = new UnityEngine.UI.Button.ButtonClickedEvent();
        button.onClick.AddListener(() => onClick());
        copy.SetActive(true);
        Copies.Add(copy);
        Slots.Add(button);
        return button;
    }

    // The copy must not run the Credits button's actions, fire its relays or be re-translated back to "Credits".
    private static void Strip(GameObject copy)
    {
        var effect = copy.GetComponent<TextButtonColorEffectTMP>();
        if (effect != null)
        {
            Native.ButtonOnSelected(effect) = null;
            Native.ButtonOnUnselected(effect) = null;
        }
        foreach (var relay in copy.GetComponentsInChildren<Relay>(true))
            UnityEngine.Object.DestroyImmediate(relay);
        foreach (var part in copy.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (part != null && part.GetType().Namespace != null && part.GetType().Namespace.StartsWith("UnityEngine.Localization", StringComparison.Ordinal))
                UnityEngine.Object.DestroyImmediate(part);
        }
    }

    internal static void Press(MainMenuButton entry)
    {
        if (!entry.Interactable)
            return;
        try { entry.OnClick(); }
        catch (Exception e) { _log?.Error($"The main menu button '{entry.Id}' of {entry.OwnerId} failed", e); }
    }

    private static void Detach()
    {
        foreach (var copy in Copies)
        {
            if (copy == null)
                continue;
            // Destroy waits for the end of the frame; take it off the menu right away.
            copy.SetActive(false);
            copy.transform.SetParent(null, false);
            UnityEngine.Object.Destroy(copy);
        }
        Copies.Clear();
        Slots.Clear();
        foreach (var button in MainMenuButtons.All)
            button.Button = null;
        _grid = null;
        Array.Clear(BottomRow, 0, BottomRow.Length);
    }

    private static MainMenuNavigationHelper FindGrid()
    {
        foreach (var helper in Resources.FindObjectsOfTypeAll<MainMenuNavigationHelper>())
        {
            if (helper.gameObject.scene.IsValid())
                return helper;
        }
        return null;
    }
}
