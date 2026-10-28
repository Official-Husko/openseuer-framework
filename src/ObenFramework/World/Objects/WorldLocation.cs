// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine.SceneManagement;

namespace ObenFramework.World;

/// <summary>Where the player is and has been.</summary>
public static class WorldLocation
{
    private static readonly HashSet<string> Session = new HashSet<string>(VisitedSceneRules.NameComparer);
    private static HashSet<string> _fileScenes;
    private static string _fileFolder;

    public static string CurrentScene => SceneManager.GetActiveScene().name;

    /// <summary>Every scene of the game by name, as the game's own scene list has them (empty before it is read).</summary>
    public static List<string> KnownScenes()
    {
        var reader = ReadSceneNames.Instance;
        return reader != null && reader.scenes != null ? new List<string>(reader.scenes) : new List<string>();
    }

    /// <summary>
    /// The places the player has been in this game: what the game records, the scene now, the levels the
    /// loaded save has state for, and the scenes entered since it loaded. The menu and the intro are left out.
    /// </summary>
    public static List<string> VisitedScenes()
    {
        var manager = SaveSceneManager.instance;
        var saved = manager != null ? manager.visitedScenes : null;
        return VisitedSceneRules.Merge(saved, new[] { CurrentScene }, FileScenes(), Session);
    }

    internal static void Init(ObenMod framework)
    {
        Session.Add(CurrentScene);
        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        GameEvents.MainMenu.Subscribe(framework, Reset);
        GameEvents.Loaded.Subscribe(framework, () => _fileScenes = null);
        GameEvents.Saved.Subscribe(framework, _ => _fileScenes = null);
        framework.Track(Disposable.Create(() =>
        {
            SceneManager.activeSceneChanged -= OnActiveSceneChanged;
            Reset();
        }));
    }

    private static void OnActiveSceneChanged(Scene previous, Scene next) => Session.Add(next.name);

    private static void Reset()
    {
        Session.Clear();
        _fileScenes = null;
        _fileFolder = null;
    }

    private static HashSet<string> FileScenes()
    {
        string folder = SaveFolders.LoadedFolder();
        if (string.IsNullOrEmpty(folder))
            return null;
        if (_fileScenes == null || _fileFolder != folder)
        {
            try
            {
                _fileScenes = VisitedSceneRules.LevelFiles(folder);
                _fileFolder = folder;
            }
            catch (Exception e)
            {
                WorldLog.Failed("Visited scenes from the save folder", e);
                return null;
            }
        }
        return _fileScenes;
    }
}
