using System;
using System.Collections;
using HarmonyLib;
using ObenFramework.Events;
using ObenLoader;
using ObenLoader.Runtime;
using UnityEngine;

namespace ObenFramework.Player.Look;

/// <summary>
/// Turns the game's "an Appearance was rebuilt" calls into <see cref="PlayerLook.Changed"/>. The patches
/// only mark the look dirty; a coroutine checks four times a second, so dragging a slider in the
/// character editor raises a few events, not one per frame, and nothing allocates while idle.
/// </summary>
[HarmonyPatch]
internal static class PlayerLookWatcher
{
    private const float SettleSeconds = 0.25f;

    private static Appearance _dirty;
    private static LookData _lastAnnounced;

    public static void Init(ObenMod framework)
    {
        framework.StartCoroutine(Watch());
        framework.Track(Disposable.Create(() =>
        {
            _dirty = null;
            _lastAnnounced = null;
            PlayerAppearances.Forget();
            PlayerLook.DestroyAllCostumes();
            WearableTable.Clear();
        }));
        GameEvents.SceneLoaded.Subscribe(framework, _ => PlayerAppearances.Forget());
    }

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Appearance), nameof(Appearance.LoadCharacter))]
    private static void AfterLoadCharacter(Appearance __instance) => Mark(__instance);

    [HarmonyPostfix]
    [HarmonyPatch(typeof(Appearance), nameof(Appearance.UpdateCreatedCharacter))]
    private static void AfterUpdateCreatedCharacter(Appearance __instance) => Mark(__instance);

    // Runs for every NPC the game spawns, so it has to stay cheap.
    private static void Mark(Appearance appearance)
    {
        if (PlayerAppearances.IsPlayer(appearance) || PlayerAppearances.UsesCreatedCharacter(appearance))
            _dirty = appearance;
    }

    private static IEnumerator Watch()
    {
        var settle = new WaitForSecondsRealtime(SettleSeconds);
        while (true)
        {
            yield return settle;
            var appearance = _dirty;
            if (appearance == null)
                continue;

            _dirty = null;
            try
            {
                var look = PlayerLook.Capture(appearance);
                if (look.SameAs(_lastAnnounced))
                    continue;
                _lastAnnounced = look;
                PlayerLook.Changed.Raise(look.Clone(), GameEvents.Log); // handlers may keep or edit theirs
            }
            catch (Exception e)
            {
                GameEvents.Log.Error("Reading the player's look failed", e);
            }
        }
    }
}
