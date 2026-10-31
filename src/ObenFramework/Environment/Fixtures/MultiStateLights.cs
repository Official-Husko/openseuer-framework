// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using ObenFramework.Items;
using ObenFramework.Patching;
using ObenFramework.Saves;
using ObenFramework.World;

namespace ObenFramework.Env;

/// <summary>A light with several states changed, as raised by <see cref="MultiStateLights.StateChanged"/>.</summary>
public readonly struct LightStateChange
{
    public LightStateChange(LightTurnOffOnMultiState light, bool on, int state, ChangeSource source)
    {
        Light = light;
        On = on;
        State = state;
        Source = source;
    }

    public LightTurnOffOnMultiState Light { get; }

    public bool On { get; }

    /// <summary>The state now, -1 for none.</summary>
    public int State { get; }

    public ChangeSource Source { get; }

    public string Id => MultiStateLights.IdOf(Light);
}

/// <summary>A snapshot of one light with several states.</summary>
public sealed class MultiStateLightInfo
{
    internal MultiStateLightInfo(string id, string name, bool on, int state, int stateCount)
    {
        Id = id;
        Name = name;
        On = on;
        State = state;
        StateCount = stateCount;
    }

    public string Id { get; }

    public string Name { get; }

    public bool On { get; }

    /// <summary>The state shown while on, -1 for none.</summary>
    public int State { get; }

    public int StateCount { get; }
}

/// <summary>
/// Lights with several states (<c>LightTurnOffOnMultiState</c>): signs and fixtures that switch between
/// colours or patterns. Plain fixtures are <see cref="Lights"/>. Changes are refused (false) while a save is
/// written or loaded.
/// </summary>
public static class MultiStateLights
{
    /// <summary>A light was switched or changed state. Not raised while a save loads.</summary>
    public static readonly WorldEvent<LightStateChange> StateChanged = new WorldEvent<LightStateChange>(nameof(StateChanged));

    /// <summary>True when the hooks are in place, so <see cref="StateChanged"/> is raised.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "Env");

    public static List<MultiStateLightInfo> All()
    {
        var list = new List<MultiStateLightInfo>();
        foreach (var light in ObjectIds.Active<LightTurnOffOnMultiState>())
            list.Add(Describe(light));
        return list;
    }

    public static LightTurnOffOnMultiState Find(string id) => ObjectIds.Find<LightTurnOffOnMultiState>(id);

    public static string IdOf(LightTurnOffOnMultiState light) => ObjectIds.IdOf(light);

    public static MultiStateLightInfo Describe(LightTurnOffOnMultiState light) =>
        new MultiStateLightInfo(IdOf(light), light.name, light.on, light.currentState, light.lightStates != null ? light.lightStates.Count : 0);

    public static bool SetOn(LightTurnOffOnMultiState light, bool on)
    {
        if (light == null)
            throw new ArgumentNullException(nameof(light));
        if (light.on == on)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
            light.Toggle(on);
        return light.on == on;
    }

    /// <summary>Picks the state shown (-1 for none). It shows once the light is on.</summary>
    public static bool SetState(LightTurnOffOnMultiState light, int state)
    {
        if (light == null)
            throw new ArgumentNullException(nameof(light));
        int count = light.lightStates != null ? light.lightStates.Count : 0;
        if (state < -1 || state >= count)
            throw new ArgumentOutOfRangeException(nameof(state), $"-1 to {count - 1}.");
        if (light.currentState == state)
            return true;
        if (SaveGate.IsBusy)
            return false;
        using (WorldChanges.EnterMod())
        {
            if (light.on)
                light.SetState(state);
            else
                light.currentState = state;
        }
        // An off light only gets the number (the game's SetState would light it), so its change is raised here.
        if (!light.on && StateChanged.HasSubscribers && WorldChanges.Active)
            StateChanged.Raise(new LightStateChange(light, false, state, ChangeSource.Mod));
        return light.currentState == state;
    }
}
