using System.Collections.Generic;
using ObenUIKit.Menu;
using ObenUIKit.Runtime;
using UnityEngine;

namespace ObenUIKit.Dev;

/// <summary>
/// What the dev bridge reads from and does to the kit. The bridge doesn't reference the kit (it reaches this
/// class by name), so everything here takes and returns plain values.
/// </summary>
public static class KitDev
{
    /// <summary>The open windows, top last.</summary>
    public static List<Dictionary<string, object>> Windows()
    {
        var list = new List<Dictionary<string, object>>();
        var runtime = KitRuntime.Current;
        if (runtime == null)
            return list;
        foreach (var window in runtime.Kit.Windows.Open)
        {
            list.Add(new Dictionary<string, object>
            {
                ["id"] = window.Id, ["title"] = window.Title, ["dock"] = window.Dock.ToString(),
                ["locksInput"] = window.LocksInput, ["closeOnEscape"] = window.CloseOnEscape,
            });
        }
        return list;
    }

    /// <summary>Closes a window by id; false when it isn't open.</summary>
    public static bool CloseWindow(string id)
    {
        var window = UiKit.Find(id);
        window?.Close();
        return window != null;
    }

    /// <summary>Prompts and guides: which shows, where, and what waits.</summary>
    public static Dictionary<string, object> Prompts()
    {
        var result = new Dictionary<string, object>();
        var runtime = PromptRuntime.Current;
        if (runtime == null)
            return result;
        var prompts = new List<Dictionary<string, object>>();
        foreach (var prompt in runtime.Prompts)
        {
            prompts.Add(new Dictionary<string, object>
            {
                ["text"] = prompt.Text, ["key"] = prompt.Key.IsNone ? "" : prompt.Key.Display(),
                ["wanted"] = prompt.Wanted, ["showing"] = prompt.IsShowing, ["seconds"] = prompt.Shown,
            });
        }
        var guides = new List<Dictionary<string, object>>();
        foreach (var guide in runtime.Guides)
            guides.Add(new Dictionary<string, object> { ["id"] = guide.Id, ["step"] = guide.Current, ["steps"] = guide.Count });
        result["prompts"] = prompts;
        result["guides"] = guides;
        result["shown"] = runtime.Shown != null ? runtime.Shown.Text : null;
        result["inGameTutorialPanel"] = runtime.ShownNatively;
        result["gameTutorialBusy"] = ObenFramework.NewGames.TutorialGuide.Active;
        return result;
    }

    /// <summary>Hides every prompt the demo or the bridge made.</summary>
    public static int HideDemoPrompts()
    {
        var runtime = PromptRuntime.Current;
        if (runtime == null)
            return 0;
        int hidden = 0;
        for (int i = runtime.Prompts.Count - 1; i >= 0; i--)
        {
            if (i < runtime.Prompts.Count && runtime.Prompts[i].Owner == UIKitMod.Instance)
            {
                runtime.Prompts[i].Hide();
                hidden++;
            }
        }
        return hidden;
    }

    /// <summary>Every mod button on the main menu, with where its copy sits on the sign, then "More…" when it's there.</summary>
    public static List<Dictionary<string, object>> MenuButtons()
    {
        var list = new List<Dictionary<string, object>>();
        foreach (var button in MainMenuButtons.All)
        {
            var entry = new Dictionary<string, object>
            {
                ["owner"] = button.OwnerId, ["id"] = button.Id, ["label"] = button.Label, ["order"] = button.Order,
                ["interactable"] = button.Interactable, ["shown"] = button.IsShown,
                ["inMore"] = MainMenuMore.Contains(button),
            };
            if (button.Button != null)
                Where(entry, button.Button);
            list.Add(entry);
        }
        if (MainMenuMore.Buttons.Count > 0 && MainMenuRow.Slots.Count > 0)
        {
            var more = new Dictionary<string, object> { ["owner"] = UIKitMod.Instance.Info.Id, ["id"] = "more", ["label"] = MainMenuMore.Label };
            Where(more, MainMenuRow.Slots[MainMenuRow.Slots.Count - 1]);
            list.Add(more);
        }
        return list;
    }

    /// <summary>Clicks a mod button on the main menu by its label, or "More…"; buttons in the More list run as from its window.</summary>
    public static string PressMenuButton(string label)
    {
        if (label == "more" || label == MainMenuMore.Label)
        {
            MainMenuMore.Open();
            return MainMenuMore.Buttons.Count > 0 ? "opened More" : "no More button";
        }
        foreach (var button in MainMenuButtons.All)
        {
            if (button.Label != label)
                continue;
            if (button.Button != null)
                button.Button.onClick.Invoke();
            else
                MainMenuRow.Press(button);
            return button.Button != null ? "pressed on the menu" : "pressed from More";
        }
        return "no button " + label;
    }

    // Where a copy sits: its place on the sign and whether all of it is on screen through the menu camera.
    private static void Where(Dictionary<string, object> entry, UnityEngine.UI.Button copy)
    {
        var rect = (RectTransform)copy.transform;
        entry["localPosition"] = rect.localPosition.ToString();
        entry["visible"] = copy.gameObject.activeInHierarchy;
        entry["onScreen"] = Camera.main != null && MainMenuFit.OnScreen(Camera.main, rect);
    }

    /// <summary>Adds a demo button to the main menu that shows a toast.</summary>
    public static string AddMenuButton(string label)
    {
        string id = "bridge" + (MainMenuButtons.All.Count + 1);
        MainMenuButtons.Add(UIKitMod.Instance, id, label, () => UiKit.Toast(label + " pressed"));
        return id;
    }

    /// <summary>Removes the buttons the bridge and the demo added.</summary>
    public static int RemoveMenuButtons()
    {
        int removed = 0;
        for (int i = MainMenuButtons.All.Count - 1; i >= 0; i--)
        {
            var button = MainMenuButtons.All[i];
            if (button.OwnerId == UIKitMod.Instance.Info.Id && (button.Id.StartsWith("bridge") || button.Id.StartsWith("demo")))
            {
                button.Remove();
                removed++;
            }
        }
        return removed;
    }
}
