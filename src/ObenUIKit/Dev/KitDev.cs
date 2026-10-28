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

    /// <summary>Every mod button on the main menu, with where its copy sits on the sign.</summary>
    public static List<Dictionary<string, object>> MenuButtons()
    {
        var list = new List<Dictionary<string, object>>();
        foreach (var button in MainMenuButtons.All)
        {
            var entry = new Dictionary<string, object>
            {
                ["owner"] = button.OwnerId, ["id"] = button.Id, ["label"] = button.Label, ["order"] = button.Order,
                ["interactable"] = button.Interactable, ["shown"] = button.IsShown,
            };
            if (button.Button != null)
            {
                var rect = (RectTransform)button.Button.transform;
                entry["localPosition"] = rect.localPosition.ToString();
                entry["visible"] = button.Button.gameObject.activeInHierarchy;
                entry["insideParent"] = InsideParent(rect);
            }
            list.Add(entry);
        }
        return list;
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

    // Whether the button's centre lies within its parent's rectangle (a hint that it shows on the sign).
    private static bool InsideParent(RectTransform rect)
    {
        if (!(rect.parent is RectTransform parent))
            return true;
        Vector2 centre = parent.InverseTransformPoint(rect.position);
        return parent.rect.Contains(centre);
    }
}
