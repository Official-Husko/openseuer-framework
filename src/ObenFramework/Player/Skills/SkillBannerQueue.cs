// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System.Collections.Generic;

namespace ObenFramework.Player;

/// <summary>
/// Order for the skill banner in the top corner, which the game and mods share. The game simply replaces
/// whatever is on screen, so without this a mod's banner and the game's would cut each other off. Mod
/// banners wait their turn; a game banner shows at once unless a mod banner is showing or waiting, in
/// which case it joins the line. With no mod banners the game behaves exactly as before. Times are
/// whatever clock the caller uses (unscaled seconds in the game). No Unity types.
/// </summary>
internal sealed class SkillBannerQueue
{
    /// <summary>How long one banner holds the spot (the game shows it for 8 s, then fades it).</summary>
    public const float Seconds = 9f;

    /// <summary>Most level-up banners of custom skills kept waiting; the oldest goes first.</summary>
    public const int MaxSkillBanners = 5;

    /// <summary>Most free-text banners from mods kept waiting; more are refused.</summary>
    public const int MaxTextBanners = 5;

    private readonly List<Entry> _pending = new List<Entry>();
    private float _freeAt = float.NegativeInfinity;
    private bool _modShowing;

    public int Count => _pending.Count;

    /// <summary>
    /// The game wants to show a banner. True: let it, it now holds the spot. False: it was queued behind
    /// mod banners and will be shown by <see cref="Next"/>.
    /// </summary>
    public bool ClaimForGame(float now, string text)
    {
        bool modBusy = _modShowing && now < _freeAt;
        if (_pending.Count == 0 && !modBusy)
        {
            _freeAt = now + Seconds;
            _modShowing = false;
            return true;
        }
        _pending.Add(new Entry(null, text, Kind.Game));
        return false;
    }

    /// <summary>A custom skill leveled up. A second banner for the same skill replaces the first.</summary>
    public void AddSkill(string key, string text)
    {
        foreach (var entry in _pending)
        {
            if (entry.Key == key)
            {
                entry.Text = text;
                return;
            }
        }

        if (CountOf(Kind.Skill) >= MaxSkillBanners)
            _pending.RemoveAt(_pending.FindIndex(e => e.Kind == Kind.Skill));
        _pending.Add(new Entry(key, text, Kind.Skill));
    }

    /// <summary>A mod's free text. False when too many are already waiting.</summary>
    public bool AddText(string text)
    {
        if (CountOf(Kind.Text) >= MaxTextBanners)
            return false;
        _pending.Add(new Entry(null, text, Kind.Text));
        return true;
    }

    /// <summary>The next banner to show now, if the spot is free and one is waiting.</summary>
    public bool Next(float now, out string text)
    {
        text = null;
        if (_pending.Count == 0 || now < _freeAt)
            return false;
        text = _pending[0].Text;
        _pending.RemoveAt(0);
        _freeAt = now + Seconds;
        _modShowing = true;
        return true;
    }

    public void Clear()
    {
        _pending.Clear();
        _freeAt = float.NegativeInfinity;
        _modShowing = false;
    }

    private int CountOf(Kind kind)
    {
        int count = 0;
        foreach (var entry in _pending)
        {
            if (entry.Kind == kind)
                count++;
        }
        return count;
    }

    private enum Kind
    {
        Game,
        Skill,
        Text,
    }

    private sealed class Entry
    {
        public Entry(string key, string text, Kind kind)
        {
            Key = key;
            Text = text;
            Kind = kind;
        }

        public string Key { get; }
        public string Text { get; set; }
        public Kind Kind { get; }
    }
}
