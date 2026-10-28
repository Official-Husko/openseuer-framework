// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Events;
using ObenFramework.Saves;
using ObenLoader;
using ObenLoader.Runtime;

namespace ObenFramework.Player;

/// <summary>The screen effects on the player.</summary>
public enum PlayerEffect
{
    /// <summary>Alcohol in blood above 15.</summary>
    Drunk,
    /// <summary>The ordinary mushroom trip.</summary>
    Mushroom,
    /// <summary>The orange mushroom (faster running, the "bad" variant at high doses).</summary>
    OrangeMushroom,
    /// <summary>The other side, from the SMV disease.</summary>
    Wasteland,
    /// <summary>Tiredness above 50.</summary>
    Sleepy,
    /// <summary>Depression above 50.</summary>
    Depression,
    /// <summary>Health at 50 or less.</summary>
    LowHealth,
}

/// <summary>An effect switched on or off, from <see cref="PlayerEffects.Changed"/>.</summary>
public readonly struct EffectChange
{
    public EffectChange(PlayerEffect effect, bool active)
    {
        Effect = effect;
        Active = active;
    }

    public PlayerEffect Effect { get; }
    public bool Active { get; }
}

/// <summary>
/// What is happening to the player's head: drunk, tripping, on the other side, sleepy, depressed, badly
/// hurt. Read any of them with a strength from 0 to 1; hear when the four with their own start and stop
/// (drunk, both mushrooms, wasteland) switch; send the player to the wasteland and back.
/// </summary>
public static class PlayerEffects
{
    private static readonly bool[] Reported = new bool[Enum.GetValues(typeof(PlayerEffect)).Length];

    /// <summary>
    /// Drunk, Mushroom, OrangeMushroom or Wasteland started or stopped. Not raised while a save loads; the
    /// state after a load is reported once the world is ready if it differs.
    /// </summary>
    public static readonly GameEvent<EffectChange> Changed = new GameEvent<EffectChange>(nameof(Changed));

    public static bool IsActive(PlayerEffect effect)
    {
        var stats = PlayerStats.instance;
        if (stats == null)
            return false;
        switch (effect)
        {
            case PlayerEffect.Drunk: return DrunkEffect.instance != null && DrunkEffect.instance.drunkEnabled;
            case PlayerEffect.Mushroom: return EffectVolumes.MushroomOn();
            case PlayerEffect.OrangeMushroom: return EffectVolumes.OrangeOn();
            case PlayerEffect.Wasteland: return SMVEffects.instance != null && SMVEffects.instance.IsWastelandIsActive();
            case PlayerEffect.Sleepy: return stats.tiredness.currentStatus > 50f;
            case PlayerEffect.Depression: return stats.mentalHealth.currentStatus > 50f;
            case PlayerEffect.LowHealth: return stats.health.currentStatus <= 50f;
            default: throw new ArgumentOutOfRangeException(nameof(effect));
        }
    }

    /// <summary>How strong an effect is, 0 (off) to 1. The trips report their screen effect's weight.</summary>
    public static float Strength(PlayerEffect effect)
    {
        switch (effect)
        {
            case PlayerEffect.Drunk: return EffectVolumes.Drunk();
            case PlayerEffect.Mushroom: return EffectVolumes.Mushroom();
            case PlayerEffect.OrangeMushroom: return EffectVolumes.Orange();
            case PlayerEffect.Wasteland: return IsActive(effect) ? 1f : 0f;
        }
        if (!IsActive(effect))
            return 0f;
        var stats = PlayerStats.instance;
        switch (effect)
        {
            case PlayerEffect.Sleepy: return Clamp01((stats.tiredness.currentStatus / 100f - 0.5f) / 0.5f);
            case PlayerEffect.Depression: return Clamp01((stats.mentalHealth.currentStatus / 100f - 0.5f) / 0.5f);
            case PlayerEffect.LowHealth: return Clamp01(1f - stats.health.currentStatus / 50f);
            default: return 0f;
        }
    }

    /// <summary>Every effect that is on now.</summary>
    public static List<PlayerEffect> Active()
    {
        var active = new List<PlayerEffect>();
        foreach (PlayerEffect effect in Enum.GetValues(typeof(PlayerEffect)))
        {
            if (IsActive(effect))
                active.Add(effect);
        }
        return active;
    }

    /// <summary>Sends the player to the wasteland with the game's transition. False without a player or while saving.</summary>
    public static bool StartWasteland(bool warningEffects = true)
    {
        var smv = SMVEffects.instance;
        if (smv == null || SaveGate.IsBusy)
            return false;
        smv.EnableWasteland(warningEffects);
        return true;
    }

    /// <summary>Brings the player back from the wasteland, even with SMV. The way back takes a few seconds.</summary>
    public static bool StopWasteland()
    {
        var smv = SMVEffects.instance;
        if (smv == null || SaveGate.IsBusy || !smv.IsWastelandIsActive())
            return false;
        smv.StopWasteland(enableStop: true, ignoreSMV: true);
        return true;
    }

    internal static void Init(ObenMod framework)
    {
        GameEvents.WorldReady.Subscribe(framework, _ => Resync());
        GameEvents.MainMenu.Subscribe(framework, Resync);
        framework.Track(Disposable.Create(() => Array.Clear(Reported, 0, Reported.Length)));
    }

    /// <summary>Called by the patches after the game switched an effect.</summary>
    internal static void Report(PlayerEffect effect)
    {
        if (SaveGate.IsLoading)
            return;
        bool active = IsActive(effect);
        if (Reported[(int)effect] == active)
            return;
        Reported[(int)effect] = active;
        Changed.Raise(new EffectChange(effect, active), GameEvents.Log);
    }

    private static void Resync()
    {
        Report(PlayerEffect.Drunk);
        Report(PlayerEffect.Mushroom);
        Report(PlayerEffect.OrangeMushroom);
        Report(PlayerEffect.Wasteland);
    }

    private static float Clamp01(float value) => Math.Min(1f, Math.Max(0f, value));
}
