// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenLoader.Runtime;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace ObenFramework.Assets;

/// <summary>
/// Texts in the player's language: the game's own translations (its Unity Localization string tables, the
/// "Menu" table by default) and translations mods register for their own keys. A missing translation falls
/// back to English, then to the key itself, never to an error text.
/// </summary>
public static class GameText
{
    public const string DefaultTable = "Menu";
    private const string English = "English (en)";
    private const string Missing = "No translation found";

    // language -> key -> text, newest registration last so it wins.
    private static readonly List<(string Language, Dictionary<string, string> Texts)> Tables = new List<(string, Dictionary<string, string>)>();

    /// <summary>The language the player picked, as the game names it (e.g. "English (en)"). Empty before the game set one.</summary>
    public static string Language
    {
        get
        {
            try
            {
                var locale = LocalizationSettings.SelectedLocale;
                return locale != null ? locale.LocaleName ?? "" : "";
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    /// <summary>Every language the game offers.</summary>
    public static List<string> Languages()
    {
        var names = new List<string>();
        try
        {
            foreach (var locale in LocalizationSettings.AvailableLocales.Locales)
            {
                if (locale != null)
                    names.Add(locale.LocaleName);
            }
        }
        catch (Exception)
        {
            // Localization not initialised yet: no languages to report.
        }
        return names;
    }

    /// <summary>
    /// Registers a mod's texts for one language until <paramref name="owner"/> unloads. Name the language as the
    /// game does ("English (en)"), by its name alone ("English") or by its code ("en"). Later registrations win
    /// over earlier ones for the same key.
    /// </summary>
    public static IDisposable Register(ITracker owner, string language, IDictionary<string, string> texts)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (string.IsNullOrEmpty(language))
            throw new ArgumentException("No language given.", nameof(language));
        if (texts == null)
            throw new ArgumentNullException(nameof(texts));
        var entry = (language, new Dictionary<string, string>(texts, StringComparer.Ordinal));
        Tables.Add(entry);
        var registration = Disposable.Create(() => Tables.Remove(entry));
        owner.Track(registration);
        return registration;
    }

    /// <summary>
    /// The text for a key in the player's language: a mod's registered text, else the game's table entry, else
    /// the English text, else the key.
    /// </summary>
    public static string Get(string key, string table = DefaultTable)
    {
        if (string.IsNullOrEmpty(key))
            return key;
        string language = Language;
        if (TryMod(language, key, out string text))
            return text;
        string game = FromGame(table, key);
        if (game != null)
            return game;
        return TryMod(English, key, out text) ? text : key;
    }

    /// <summary>The game's own translation of a key in a string table, or the key when there is none.</summary>
    public static string FromGameOrKey(string key, string table = DefaultTable) => FromGame(table, key) ?? key;

    /// <summary>Raised when the player switches language (for mods that cache texts).</summary>
    public static IDisposable OnLanguageChanged(ITracker owner, Action<string> handler)
    {
        if (owner == null)
            throw new ArgumentNullException(nameof(owner));
        if (handler == null)
            throw new ArgumentNullException(nameof(handler));
        Action<Locale> wrapped = locale =>
        {
            try { handler(locale != null ? locale.LocaleName : ""); }
            catch (Exception e) { AssetsLog.Error("A language change handler threw", e); }
        };
        LocalizationSettings.SelectedLocaleChanged += wrapped;
        var registration = Disposable.Create(() => LocalizationSettings.SelectedLocaleChanged -= wrapped);
        owner.Track(registration);
        return registration;
    }

    internal static void Reset() => Tables.Clear();

    private static bool TryMod(string language, string key, out string text)
    {
        for (int i = Tables.Count - 1; i >= 0; i--)
        {
            if (SameLanguage(Tables[i].Language, language) && Tables[i].Texts.TryGetValue(key, out text))
                return true;
        }
        text = null;
        return false;
    }

    // The game names languages "English (en)"; a mod may say "English", "en" or the full name.
    internal static bool SameLanguage(string registered, string current)
    {
        if (string.IsNullOrEmpty(registered) || string.IsNullOrEmpty(current))
            return false;
        if (string.Equals(registered, current, StringComparison.OrdinalIgnoreCase))
            return true;
        int open = current.LastIndexOf(" (", StringComparison.Ordinal);
        if (open < 0 || !current.EndsWith(")", StringComparison.Ordinal))
            return false;
        string name = current.Substring(0, open);
        string code = current.Substring(open + 2, current.Length - open - 3);
        return string.Equals(registered, name, StringComparison.OrdinalIgnoreCase) || string.Equals(registered, code, StringComparison.OrdinalIgnoreCase);
    }

    private static string FromGame(string table, string key)
    {
        try
        {
            string text = LocalizationSettings.StringDatabase.GetLocalizedString(table, key, null, FallbackBehavior.UseProjectSettings);
            return string.IsNullOrEmpty(text) || text.StartsWith(Missing, StringComparison.Ordinal) ? null : text;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
