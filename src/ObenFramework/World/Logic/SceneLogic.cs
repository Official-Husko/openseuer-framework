// Adapted from the Obenseuer Modding Framework by NomadsReach (MIT), see THIRD-PARTY-NOTICES.md.
using System;
using System.Collections.Generic;
using HarmonyLib;
using ObenFramework.Saves;
using ObenFramework.Scenes;
using Outputs;
using ObenFramework.Patching;

namespace ObenFramework.World.Logic;

/// <summary>
/// Relays, the nodes that carry most of a level's scripting ("when this happens, do these things"), and
/// trigger volumes: list them, fire a relay as if the level had, switch outputs on and off, and hear about
/// every relay that fires.
/// </summary>
public static class SceneLogic
{
    private static readonly AccessTools.FieldRef<Relay, List<TaskItem>> TaskItems =
        AccessTools.FieldRefAccess<Relay, List<TaskItem>>("requiredTaskItems");
    private static readonly AccessTools.FieldRef<Relay, List<ObjectiveStatusInfo>> Objectives =
        AccessTools.FieldRefAccess<Relay, List<ObjectiveStatusInfo>>("requiredObjectives");
    private static readonly AccessTools.FieldRef<Relay, List<DialogueVariable>> Variables =
        AccessTools.FieldRefAccess<Relay, List<DialogueVariable>>("requiredDialogueVariables");

    /// <summary>
    /// A relay fired its outputs: raised after the game ran them (outputs with a delay are still pending).
    /// Only relays that actually fired are reported, not calls the relay ignored because it was used up,
    /// inactive or its requirements weren't met.
    /// </summary>
    public static readonly WorldEvent<RelayFired> Fired = new WorldEvent<RelayFired>(nameof(Fired));

    /// <summary>False when the framework's relay hook failed to apply after a game update; <see cref="Fired"/> then stays quiet.</summary>
    public static bool IsAvailable => PatchAreas.IsAvailable(PatchAreas.Framework, "World.Logic");

    public static List<RelayInfo> Relays()
    {
        var list = new List<RelayInfo>();
        foreach (var relay in SceneIds.Active<Relay>())
            list.Add(new RelayInfo(SceneIds.Of(relay), relay.fireOnceOnly, relay.firedOnce, Enabled(relay.outputs)));
        return list;
    }

    /// <summary>Fires a relay. False when it isn't found, wouldn't fire (see <see cref="CanFire"/>) or the game is busy saving.</summary>
    public static bool Fire(string id)
    {
        if (SaveGate.IsBusy)
            return false;
        try
        {
            var relay = SceneIds.Find<Relay>(id);
            if (relay == null || !CanFire(relay))
                return false;
            relay.triggerOutputs();
            return true;
        }
        catch (Exception e)
        {
            SceneState.Log.Error("SceneLogic.Fire", e);
            return false;
        }
    }

    /// <summary>Switches every output of a relay or a trigger volume on or off.</summary>
    public static bool SetEnabled(string id, bool enabled)
    {
        if (SaveGate.IsBusy)
            return false;
        try
        {
            var relay = SceneIds.Find<Relay>(id);
            if (relay != null)
            {
                if (enabled) relay.enableOutputs();
                else relay.disableOutputs();
                return Enabled(relay.outputs) == enabled;
            }

            var trigger = SceneIds.Find<TriggerMultiple>(id);
            if (trigger == null)
                return false;
            if (enabled) trigger.enableOutputs();
            else trigger.disableOutputs();
            return Enabled(trigger.outputs) == enabled;
        }
        catch (Exception e)
        {
            SceneState.Log.Error("SceneLogic.SetEnabled", e);
            return false;
        }
    }

    /// <summary>Whether a relay would fire if triggered now (active, not used up, requirements met).</summary>
    public static bool CanFire(Relay relay)
    {
        if (relay == null)
            return false;
        return SceneStateRules.RelayCanFire(relay.gameObject.activeInHierarchy, relay.fireOnceOnly, relay.firedOnce, RequirementsMet(relay));
    }

    internal static void RaiseFired(Relay relay) =>
        Fired.Raise(new RelayFired(SceneIds.Of(relay), relay.name, relay.fireOnceOnly, Enabled(relay.outputs)));

    private static bool RequirementsMet(Relay relay)
    {
        var items = TaskItems(relay);
        var objectives = Objectives(relay);
        var variables = Variables(relay);
        if (items != null && items.Count > 0 && (TaskItemsManager.instance == null || !TaskItemsManager.instance.CheckIfPlayerHasTaskItem(items)))
            return false;
        if (objectives != null && objectives.Count > 0 && (TaskController.instance == null || !TaskController.instance.CheckObjectiveStatusInfos(objectives)))
            return false;
        return variables == null || variables.Count == 0 || DialogueVariable.IsAllTrue(variables);
    }

    private static bool Enabled(output[] outputs)
    {
        if (outputs == null)
            return true;
        foreach (var item in outputs)
        {
            if (item != null && item.disabled)
                return false;
        }
        return true;
    }

    private static bool Enabled(triggerOutput[] outputs)
    {
        if (outputs == null)
            return true;
        foreach (var item in outputs)
        {
            if (item != null && item.disabled)
                return false;
        }
        return true;
    }
}
