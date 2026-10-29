// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using ObenFramework.Crafting;

namespace GameTweaks.Crafting;

/// <summary>
/// Dishwashers wash faster (or slower). A dishwasher is a crafting station of class <c>Dishwasher</c>, so
/// this is one <see cref="CraftingSpeed"/> factor for that class, on top of the crafting speed tweak if both
/// are on. No patches of its own.
/// </summary>
internal sealed class DishwasherTweak : TweakFeature
{
    private const string StationClass = "Dishwasher";

    internal static float Speed = 1f;

    public override string Id => "DishwasherSpeed";
    public override string Name => "Faster (or slower) dishwasher";
    public override string Description =>
        "Dishwashers wash at the speed on the Settings page, on top of the crafting speed tweak. A wash already running keeps its time.";

    public override void Apply(TweaksConfig config)
    {
        Speed = TweakMath.Factor(config.DishwasherSpeed, CraftingSpeed.MinSpeed, CraftingSpeed.MaxSpeed);
        if (Speed == 1f)
            CraftingSpeed.Remove(this, StationClass);
        else
            CraftingSpeed.Set(this, StationClass, Speed);
    }

    protected override void OnDisable()
    {
        Speed = 1f;
        CraftingSpeed.Clear(this);
    }
}
