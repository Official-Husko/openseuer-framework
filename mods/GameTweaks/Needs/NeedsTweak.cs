// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using ObenFramework.Player.Survival;

namespace GameTweaks.Needs;

/// <summary>
/// Scales how fast hunger, thirst, hygiene and the four cravings grow, through the framework's
/// <see cref="StatDrain"/>, which already sits on the game's rate getters. No patches of its own; while off
/// it has no drain entries at all. Other mods' multipliers combine with these.
/// </summary>
internal sealed class NeedsTweak : TweakFeature
{
    private const float Max = 10f;

    private static readonly PlayerDrain[] Drains =
    {
        PlayerDrain.Hunger, PlayerDrain.Thirst, PlayerDrain.Hygiene, PlayerDrain.AlcoholNeed,
        PlayerDrain.MushroomNeed, PlayerDrain.SmokingNeed, PlayerDrain.GamblingNeed,
    };

    // What is in effect, so an unchanged value isn't set again on every config save.
    private static readonly float[] Applied = { 1f, 1f, 1f, 1f, 1f, 1f, 1f };

    /// <summary>The rate in effect for <paramref name="drain"/>: 1 while the tweak is off or the need isn't one of these.</summary>
    internal static float Rate(PlayerDrain drain)
    {
        int index = Array.IndexOf(Drains, drain);
        return index < 0 ? 1f : Applied[index];
    }

    public override string Id => "Needs";
    public override string Name => "Needs grow slower or faster";
    public override string Description =>
        "Hunger, thirst, hygiene and cravings change at the rates set on the Settings page. 0.5 is half as fast.";

    public override void Apply(TweaksConfig config)
    {
        Set(0, config.Hunger);
        Set(1, config.Thirst);
        Set(2, config.Hygiene);
        Set(3, config.AlcoholCraving);
        Set(4, config.MushroomCraving);
        Set(5, config.SmokingCraving);
        Set(6, config.GamblingCraving);
    }

    protected override void OnDisable()
    {
        for (int i = 0; i < Drains.Length; i++)
        {
            StatDrain.Remove(Owner, Drains[i]);
            Applied[i] = 1f;
        }
    }

    private void Set(int index, float value)
    {
        float factor = TweakMath.Factor(value, 0f, Max);
        if (factor == Applied[index])
            return;
        Applied[index] = factor;
        if (factor == 1f)
            StatDrain.Remove(Owner, Drains[index]);
        else
            StatDrain.Set(Owner, Drains[index], factor);
    }
}
