using ObenFramework.Features;

namespace GameTweaks;

/// <summary>
/// One tweak the player can switch on. Tweaks are off until the player turns them on, and a tweak that
/// is off has no patches and no registrations at all. While on, <see cref="Apply"/> runs again every time
/// the config changes, so moving a slider on the Settings page takes effect right away.
/// </summary>
internal abstract class TweakFeature : ModFeature
{
    public override bool EnabledByDefault => false;

    /// <summary>Reads this tweak's values from the config and puts them into effect.</summary>
    public abstract void Apply(TweaksConfig config);

    protected override void OnEnable() => Apply(GameTweaksMod.Settings);
}
