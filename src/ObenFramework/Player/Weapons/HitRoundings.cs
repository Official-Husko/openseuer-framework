using System.Collections.Generic;

namespace ObenFramework.Player;

/// <summary>
/// Which rounding each hit multiplier asked for. When mods disagree on a target, the multiplier set most
/// recently decides, as in the reference framework. No Unity types, so it is unit tested.
/// </summary>
internal sealed class HitRoundings
{
    private struct Choice
    {
        public string OwnerId;
        public int Target;
        public string Key;
        public HitRounding Rounding;
    }

    private readonly List<Choice> _choices = new List<Choice>();

    public void Set(string ownerId, int target, string key, HitRounding rounding)
    {
        Remove(ownerId, target, key);
        _choices.Add(new Choice { OwnerId = ownerId, Target = target, Key = key ?? "", Rounding = rounding });
    }

    public void Remove(string ownerId, int target, string key)
    {
        key ??= "";
        _choices.RemoveAll(c => c.OwnerId == ownerId && c.Target == target && c.Key == key);
    }

    public void RemoveOwner(string ownerId) => _choices.RemoveAll(c => c.OwnerId == ownerId);

    public void Clear() => _choices.Clear();

    /// <summary>The rounding for a target: the newest choice on it, <see cref="HitRounding.Chance"/> when none.</summary>
    public HitRounding For(int target)
    {
        for (int i = _choices.Count - 1; i >= 0; i--)
        {
            if (_choices[i].Target == target)
                return _choices[i].Rounding;
        }
        return HitRounding.Chance;
    }
}
