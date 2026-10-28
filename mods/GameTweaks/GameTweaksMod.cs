using System;
using System.Collections.Generic;
using GameTweaks.Camera;
using GameTweaks.Clock;
using GameTweaks.Crafting;
using GameTweaks.Housing;
using GameTweaks.Items;
using GameTweaks.Needs;
using ObenFramework.Features;
using ObenLoader;
using ObenLoader.Config;
using ObenLoader.Runtime;
using ObenUIKit.Menu.Settings;

namespace GameTweaks;

/// <summary>
/// Game Tweaks: optional changes to how the game plays, each one a feature the player switches on by
/// itself, with its values on the Settings page of the MODS menu. Every tweak is off by default and
/// costs nothing while off. Write-ups are in docs/modding/game-tweaks.md.
/// </summary>
public sealed class GameTweaksMod : ObenMod
{
    private readonly List<TweakFeature> _tweaks = new List<TweakFeature>();
    private ConfigFile<TweaksConfig> _config;

    /// <summary>The current values. Only valid while the mod is loaded.</summary>
    internal static TweaksConfig Settings { get; private set; } = new TweaksConfig();

    public override void OnLoad()
    {
        _config = LoadConfig<TweaksConfig>();
        Settings = _config.Value;
        _config.Save(); // Writes the defaults on first run so the file is easy to find.
        _config.Changed += OnChanged;
        Track(Disposable.Create(() => _config.Changed -= OnChanged));
        ModSettings.Register(this, _config, "Game tweaks");

        Add(new NeedsTweak());
        Add(new StackSizeTweak());
        Add(new SpoilageTweak());
        Add(new CraftingSpeedTweak());
        Add(new SkillGateTweak());
        Add(new DishwasherTweak());
        Add(new RenovationCostTweak());
        Add(new RenovationMaterialsTweak());
        Add(new RenovationTimeTweak());
        Add(new DayLengthTweak());
        Add(new ThirdPersonTweak());
    }

    private void Add(TweakFeature tweak)
    {
        _tweaks.Add(tweak);
        ModFeatures.Register(this, tweak);
    }

    private void OnChanged(TweaksConfig config)
    {
        Settings = config;
        foreach (var tweak in _tweaks)
        {
            if (!tweak.Enabled)
                continue;
            try
            {
                tweak.Apply(config);
            }
            catch (Exception e)
            {
                Log.Error($"Applying the new values to {tweak.Id} failed", e);
            }
        }
    }
}
