// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;

namespace ObenFramework.NewGames;

/// <summary>
/// The registered new game options in registration order, and the values picked on the New Game screen
/// before the game starts (the draft). Options can come and go at any time (a mod reloading registers
/// them again); stored values don't depend on them. No Unity types, so it is unit tested on its own.
/// </summary>
internal sealed class NewGameOptionStore
{
    private readonly List<NewGameOption> _options = new List<NewGameOption>();
    private readonly Dictionary<string, string> _draft = new Dictionary<string, string>(StringComparer.Ordinal);

    public int Count => _options.Count;

    /// <summary>Adds an option. False when the mod already registered one with this key (the first is kept).</summary>
    public bool Add(NewGameOption option)
    {
        if (option == null)
            throw new ArgumentNullException(nameof(option));
        if (Find(option.ModId, option.Key) != null)
            return false;
        _options.Add(option);
        return true;
    }

    /// <summary>Removes one option; its draft value stays in case the mod registers it again.</summary>
    public bool Remove(NewGameOption option) => _options.Remove(option);

    public NewGameOption Find(string modId, string key)
    {
        if (modId == null || key == null)
            return null;
        for (int i = 0; i < _options.Count; i++)
        {
            var option = _options[i];
            if (option.ModId == modId && option.Key == key)
                return option;
        }
        return null;
    }

    /// <summary>A copy of the registered options in registration order.</summary>
    public List<NewGameOption> All() => new List<NewGameOption>(_options);

    /// <summary>How many options this mod registered.</summary>
    public int CountFor(string modId)
    {
        int count = 0;
        foreach (var option in _options)
        {
            if (option.ModId == modId)
                count++;
        }
        return count;
    }

    /// <summary>What the player picked on the screen, or null when they haven't touched it.</summary>
    public string Draft(string modId, string key) =>
        _draft.TryGetValue(Name(modId, key), out var value) ? value : null;

    /// <summary>The picked value, else the option's default; null for an option that isn't registered.</summary>
    public string DraftOrDefault(string modId, string key)
    {
        var option = Find(modId, key);
        return option == null ? null : option.Normalize(Draft(modId, key));
    }

    public void Choose(string modId, string key, string value)
    {
        var option = Find(modId, key);
        if (option == null)
            throw new ArgumentException($"No new game option {modId}.{key} is registered.");
        _draft[Name(modId, key)] = option.Normalize(value);
    }

    public void ClearDraft() => _draft.Clear();

    /// <summary>The values a new game starts with: every registered option, picked or default, sorted by mod and key.</summary>
    public NewGameOptionsFile Capture()
    {
        var file = new NewGameOptionsFile();
        foreach (var option in _options)
            file.Set(option.ModId, option.Key, option.Normalize(Draft(option.ModId, option.Key)));
        file.Sort();
        return file;
    }

    private static string Name(string modId, string key) => modId + "\n" + key;
}
