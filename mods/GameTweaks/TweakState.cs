// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using GameTweaks.Clock;
using GameTweaks.Crafting;
using GameTweaks.Housing;
using GameTweaks.Items;
using GameTweaks.Needs;
using ObenFramework.Events;
using ObenFramework.Features;
using ObenFramework.Player.Survival;
using ObenLoader.Runtime;

namespace GameTweaks;

/// <summary>
/// What Game Tweaks does right now, for other mods: reference GameTweaks.dll and check
/// <c>ModRegistry.IsLoaded("GameTweaks")</c> first. Every value is the one in effect, so a tweak that is off
/// reads as the game's own (1, or false). <see cref="OnChanged"/> hears when a tweak is switched on or off or
/// its values change.
/// </summary>
public static class TweakState
{
    private static readonly HandlerList<Action> Handlers = new HandlerList<Action>();
    private static readonly Action<Action> Call = h => h();

    /// <summary>Calls <paramref name="handler"/> after a tweak is switched on or off or the values on the Settings page change, until <paramref name="owner"/> unloads.</summary>
    public static void OnChanged(ITracker owner, Action handler) => Handlers.Add(owner, handler);

    /// <summary>True when the tweak with this id (as on the Mods page, e.g. "StackSize") is switched on.</summary>
    public static bool IsOn(string id)
    {
        var mod = GameTweaksMod.Instance;
        return mod != null && mod.IsOn(id);
    }

    /// <summary>How fast a need grows compared with the game (the needs tweak; other mods' multipliers aren't included).</summary>
    public static float NeedRate(PlayerDrain drain) => NeedsTweak.Rate(drain);

    /// <summary>Stack sizes are multiplied by this.</summary>
    public static float StackSize => StackSizeTweak.Factor;

    /// <summary>Food spoils this many times as fast (0 stops spoiling).</summary>
    public static float Spoilage => SpoilageTweak.Factor;

    /// <summary>Every crafting station and blueprint works this many times as fast.</summary>
    public static float CraftingSpeed => CraftingSpeedTweak.Speed;

    /// <summary>Dishwashers work this many times as fast, on top of <see cref="CraftingSpeed"/>.</summary>
    public static float DishwasherSpeed => DishwasherTweak.Speed;

    /// <summary>Recipes ignore their minimum skill.</summary>
    public static bool NoMinimumSkill => IsOn(SkillGateTweak.Key);

    /// <summary>Renovation payments are multiplied by this.</summary>
    public static float RenovationCost => RenovationCostTweak.Factor;

    /// <summary>New renovation orders take this many times as long.</summary>
    public static float RenovationTime => RenovationTimeTweak.Factor;

    /// <summary>New renovation orders (other than the story's) are done at once.</summary>
    public static bool InstantRenovations => RenovationTimeTweak.Instant;

    /// <summary>Renovations need only money, no materials.</summary>
    public static bool RenovationsMoneyOnly => IsOn(RenovationMaterialsTweak.Key);

    /// <summary>The clock runs this many times as fast (days are this many times shorter).</summary>
    public static float TimeSpeed => DayLengthTweak.Speed;

    /// <summary>The F6 third-person key is on. The camera itself is the framework's <c>CameraView</c>.</summary>
    public static bool ThirdPersonKey => IsOn(Camera.ThirdPersonTweak.Key);

    internal static void Init(GameTweaksMod mod)
    {
        void FeatureSwitched(ModFeature feature)
        {
            if (feature.Owner == mod)
                Raise();
        }
        ModFeatures.Changed += FeatureSwitched;
        mod.Track(Disposable.Create(() => ModFeatures.Changed -= FeatureSwitched));
    }

    internal static void Raise()
    {
        var mod = GameTweaksMod.Instance;
        if (mod != null)
            Handlers.ForEach(Call, mod.Log, "TweakState.OnChanged");
    }
}
