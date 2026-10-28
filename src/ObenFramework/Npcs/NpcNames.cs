using System;
using NPC;

namespace ObenFramework.Npcs;

/// <summary>Readable names for the game's activity objects.</summary>
internal static class NpcNames
{
    /// <summary>The activity's own name, falling back to its class name ("Act_Stand"). Null for no activity.</summary>
    public static string Of(Activity activity)
    {
        if (activity == null)
            return null;
        return string.IsNullOrEmpty(activity.activityName) ? activity.GetType().Name : activity.activityName;
    }

    /// <summary>The scene an activity wants the NPC in, or null. The game's overrides can throw on half-built activities.</summary>
    public static string PlaceOf(Activity activity)
    {
        if (activity == null)
            return null;
        try
        {
            string place = activity.GetTargetScene();
            return string.IsNullOrEmpty(place) ? null : place;
        }
        catch (Exception)
        {
            return null;
        }
    }
}
