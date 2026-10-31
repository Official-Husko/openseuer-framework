// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Game;
using ObenFramework.NewGames;
using ObenLoader.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ObenUIKit.NewGameScreen;

/// <summary>
/// Puts the new game options into the New Game screen's difficulty page as rows like the game's own, so the
/// mouse, keyboard and pad reach them the same way. On Custom the list reads: the general options
/// (<see cref="NewGamePlacement.BelowDifficulty"/>), the game's custom settings, then the options that belong
/// with them (<see cref="NewGamePlacement.CustomPanel"/>). On the other levels the game shows a description
/// instead of the list; while there are general options the list shows there too, holding that description
/// and the general options, with the game's own settings hidden. The pause menu's difficulty page is left alone.
/// </summary>
internal static class NewGameRows
{
    private const string InfoName = NewGameRow.Prefix + "difficulty description";

    private static NewGamePage _page;
    private static readonly List<NewGameRow> Rows = new List<NewGameRow>();
    private static readonly Dictionary<GameObject, bool> GameRows = new Dictionary<GameObject, bool>();
    private static TextMeshProUGUI _info;
    private static bool _tracked;
    private static bool _warned;
    private static string _shownLevel;

    /// <summary>The difficulty page showed: builds the rows once per page, then shows the right ones.</summary>
    public static void Show(DifficultyUI ui)
    {
        var kit = UIKitMod.Instance;
        if (kit == null || !WorldState.InMainMenu)
            return;
        if (!_tracked)
        {
            _tracked = true;
            kit.Track(Disposable.Create(Remove));
            NewGameOptions.ListChanged.Subscribe(kit, Rebuild);
        }
        if (_page == null || !_page.Alive || _page.Ui != ui)
        {
            Remove();
            _page = NewGamePage.Find(ui, out string missing);
            if (_page == null)
            {
                if (!_warned)
                    kit.Log.Warn($"The New Game screen's difficulty page has no {missing} where this kit expects it; new game options aren't shown on it.");
                _warned = true;
                return;
            }
        }
        if (Rows.Count == 0 && _info == null)
            Build();
        Sync();
    }

    /// <summary>The page redrew for another difficulty level.</summary>
    public static void Changed(DifficultyUI ui)
    {
        if (_page != null && _page.Alive && _page.Ui == ui)
            Sync();
    }

    private static void Build()
    {
        var options = NewGameOptions.Options;
        if (options.Count == 0)
            return;
        var template = _page.Template();
        if (template == null)
        {
            UIKitMod.Instance.Log.Warn("The New Game screen's settings list has no row to copy; new game options aren't shown on it.");
            return;
        }
        foreach (Transform child in _page.Panel)
        {
            if (!NewGameRow.IsOwn(child.gameObject))
                GameRows[child.gameObject] = child.gameObject.activeSelf;
        }

        foreach (var option in options)
            Add(template, option);
        if (Rows.Count == 0)
        {
            GameRows.Clear();
            return;
        }

        // The description first, then the general options, the game's rows, and the custom rules' options last.
        _info = MakeInfo();
        _info.transform.SetSiblingIndex(0);
        int index = 1;
        foreach (var row in Rows)
        {
            if (row.Option.Placement == NewGamePlacement.BelowDifficulty)
                row.Root.transform.SetSiblingIndex(index++);
            else
                row.Root.transform.SetAsLastSibling();
        }
    }

    private static void Add(GameObject template, NewGameOption option)
    {
        try
        {
            Rows.Add(NewGameRow.Create(_page, template, option));
        }
        catch (Exception e)
        {
            UIKitMod.Instance.Log.Error($"Adding the new game option {option.ModId}.{option.Key} to the New Game screen failed", e);
        }
    }

    // The game's description of the level, copied into the list for the levels whose list it shows.
    private static TextMeshProUGUI MakeInfo()
    {
        var copy = UnityEngine.Object.Instantiate(_page.InfoText.gameObject, _page.Panel, false);
        copy.name = InfoName;
        var layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
        layout.ignoreLayout = false;
        return copy.GetComponent<TextMeshProUGUI>();
    }

    private static void Sync()
    {
        if (Rows.Count == 0)
            return;
        var info = DifficultyController.instance != null ? DifficultyController.instance.currentDifficultyInfo : null;
        bool custom = info != null && info.difficulty == Difficulty.Custom;
        bool general = Rows.Exists(r => r.Option.Placement == NewGamePlacement.BelowDifficulty);

        foreach (var row in Rows)
        {
            row.Root.SetActive(custom || row.Option.Placement == NewGamePlacement.BelowDifficulty);
            row.Render();
        }
        bool ownList = !custom && general;
        foreach (var pair in GameRows)
        {
            if (pair.Key != null)
                pair.Key.SetActive(!ownList && pair.Value);
        }
        if (ownList)
        {
            _page.Settings.SetActive(true);
            _page.Info.SetActive(false);
            FillInfo();
        }
        _info.gameObject.SetActive(ownList);

        // A new level starts the list at the top.
        string level = info != null ? info.difficultyName : null;
        if (level != _shownLevel && _page.Scroll != null)
        {
            _shownLevel = level;
            Canvas.ForceUpdateCanvases();
            _page.Scroll.verticalNormalizedPosition = 1f;
        }
        _page.RefreshNavigation();
    }

    private static void FillInfo()
    {
        _info.text = _page.InfoText.text;
        var rect = (RectTransform)_info.transform;
        float width = ((RectTransform)_page.Panel).rect.width - 40f;
        float height = _info.GetPreferredValues(_info.text, width, 0f).y + 30f;
        rect.sizeDelta = new Vector2(width, height);
        var layout = _info.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
    }

    // The option list changed while the page is up (a mod loaded, unloaded or registered late).
    private static void Rebuild()
    {
        if (_page == null || !_page.Alive)
            return;
        var page = _page;
        Clear();
        _page = page;
        Build();
        if (Rows.Count > 0)
            Sync();
        else
            Restore(page);
    }

    private static void Remove()
    {
        var page = _page;
        Clear();
        if (page != null && page.Alive)
            Restore(page);
    }

    // Destroys the copies and puts the game's rows back as they were.
    private static void Clear()
    {
        foreach (var row in Rows)
        {
            if (row.Root != null)
                UnityEngine.Object.Destroy(row.Root);
        }
        Rows.Clear();
        if (_info != null)
            UnityEngine.Object.Destroy(_info.gameObject);
        _info = null;
        foreach (var pair in GameRows)
        {
            if (pair.Key != null)
                pair.Key.SetActive(pair.Value);
        }
        GameRows.Clear();
        _page = null;
        _shownLevel = null;
    }

    // The page as the game draws it: the list for Custom, the description for the rest.
    private static void Restore(NewGamePage page)
    {
        var info = DifficultyController.instance != null ? DifficultyController.instance.currentDifficultyInfo : null;
        bool custom = info != null && info.difficulty == Difficulty.Custom;
        page.Settings.SetActive(custom);
        page.Info.SetActive(!custom);
        page.RefreshNavigation();
    }
}
